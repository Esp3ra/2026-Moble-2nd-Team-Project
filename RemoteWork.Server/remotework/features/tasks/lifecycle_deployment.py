"""Explicit server-owned lifecycle activation; no DDL or business writes.

The operator opts into the reviewed contract for this deployment. Read-only
composition/storage checks are additional safeguards, not proof of a shared
environment's acceptance test. Unknown owner adapters are never replaced.
"""
import os


CONTRACT = "member2-task-lifecycle-v1"
CONFIG_KEY = "TASK_LIFECYCLE_INTEGRATION"
ENV_KEY = "REMOTEWORK_TASK_LIFECYCLE_INTEGRATION"
ACK_KEY = "task_lifecycle_integration_ack"


class ActivationError(RuntimeError):
    """Public startup error containing only a fixed diagnostic code."""


def _require(condition, code):
    if not condition:
        raise ActivationError("Task lifecycle activation failed: " + code + ".")


def _enabled(app, name):
    return app.config.get("ENABLE_" + name + "_DRAFT",
                          os.getenv("REMOTEWORK_ENABLE_" + name + "_DRAFT") == "1") is True


def _check_composition(app):
    for feature in ("TASK_QUERY", "COMMON_WRITE", "TASK_LIFECYCLE", "RESULTS"):
        _require(_enabled(app, feature), feature + "_REQUIRED")

    from ... import task_write_support, write_requests
    from .. import results
    from . import lifecycle, routes

    for name, blueprint in (("task_queries", routes.bp), ("results", results.bp),
                            ("write_requests", write_requests.bp)):
        _require(app.blueprints.get(name) is blueprint, "BLUEPRINT_REQUIRED")
    _require(app.config.get("RESULTS_CONTEXT_MODE") == "local-development"
             and app.extensions.get("results_facts") is results.local_facts,
             "REVIEWED_RESULTS_ADAPTER_REQUIRED")

    contracts = app.extensions.get("write_action_contracts", {})
    authorizers = app.extensions.get("write_authorizers", {})
    readers = app.extensions.get("write_result_readers", {})
    for action, writer, reader in (
            ("task.cancelled", lifecycle.authorize_cancel, lifecycle.authorize_cancel_result),
            ("task.deadline_extension_requested", task_write_support._own, lifecycle.authorize_request_result),
            ("task.deadline_extension_approved", lifecycle.authorize_decision, lifecycle.authorize_decision_result),
            ("task.deadline_extension_rejected", lifecycle.authorize_decision, lifecycle.authorize_decision_result),
            ("task.deadline_extension_withdrawn", lifecycle.authorize_withdraw, lifecycle.authorize_withdraw_result)):
        contract = contracts.get(action)
        actual_writer = contract.authorize if contract is not None else authorizers.get(action)
        result_reader = readers.get(action)
        _require(actual_writer is writer, "REVIEWED_WRITE_CALLBACK_REQUIRED")
        _require(contract is None or contract.result_fields == lifecycle.RESULT_FIELDS,
                 "WRITE_RESULT_CONTRACT_REQUIRED")
        _require(result_reader is not None and result_reader.authorize is reader
                 and result_reader.read_only is False, "WRITE_RESULT_READER_REQUIRED")

    for action, callback in (("submission.submitted", results._authorize_submit),
                             ("submission.approved", results._authorize_decision),
                             ("submission.returned", results._authorize_decision)):
        contract = contracts.get(action)
        _require(contract is not None and contract.authorize is callback,
                 "REVIEWED_SUBMISSION_CALLBACK_REQUIRED")

    shared = app.extensions.get("results_shared_attachments")
    if _enabled(app, "SHARED_ATTACHMENTS"):
        from .. import shared_attachments, shared_resource_attachments
        _require(shared is shared_attachments
                 and app.blueprints.get("shared_attachments") is shared_attachments.bp
                 and callable(shared.release_task_files), "SHARED_ATTACHMENT_PROVIDER_REQUIRED")
        _require(app.extensions.get("results_resource_attachments") is shared_resource_attachments
                 and app.blueprints.get("resource_attachments") is shared_resource_attachments.bp,
                 "RESOURCE_ATTACHMENT_PROVIDER_REQUIRED")
    else:
        _require(shared is None, "SHARED_ATTACHMENT_FLAG_MISMATCH")
    return shared


def _column_type_matches(actual, expected):
    """Match the reviewed MySQL types, ignoring only integer display width.

    MySQL reflects BOOL as signed TINYINT(1), INTEGER as INTEGER/INT, and
    DATETIME with omitted precision as DATETIME(0). These are the only
    dialect-normalized differences needed by the common/006/009/013 models.
    """
    from sqlalchemy import BigInteger, Boolean, Date, DateTime, Integer, String, Text
    from sqlalchemy.dialects import mysql

    expected = expected.dialect_impl(mysql.dialect())
    if isinstance(expected, Boolean):
        return (type(actual) is mysql.TINYINT and actual.display_width == 1
                and not actual.unsigned and not actual.zerofill)
    if isinstance(expected, (BigInteger, Integer)):
        kind = mysql.BIGINT if isinstance(expected, BigInteger) else mysql.INTEGER
        return type(actual) is kind and not actual.unsigned and not actual.zerofill
    if isinstance(expected, Text):
        return type(actual) is mysql.TEXT
    if isinstance(expected, String):
        return type(actual) is mysql.VARCHAR and actual.length == expected.length
    if isinstance(expected, DateTime):
        return type(actual) is mysql.DATETIME and actual.fsp in (None, 0)
    if isinstance(expected, Date):
        return type(actual) is mysql.DATE
    return False


def _check_table(inspector, table):
    """Inspect transaction/relational essentials without creating any table."""
    from sqlalchemy import UniqueConstraint

    name = table.name
    _require(inspector.has_table(name), "STORAGE_TABLE_REQUIRED")
    columns = {column["name"]: column for column in inspector.get_columns(name)}
    _require(set(columns) == set(table.columns.keys()), "STORAGE_COLUMNS_MISMATCH")
    for column in table.columns:
        actual = columns[column.name]
        _require(_column_type_matches(actual.get("type"), column.type), "STORAGE_COLUMN_TYPE_MISMATCH")
        _require(actual.get("nullable") is column.nullable, "STORAGE_NULLABILITY_MISMATCH")
        _require(actual.get("autoincrement", False) is (table.autoincrement_column is column),
                 "STORAGE_AUTOINCREMENT_MISMATCH")
    _require(inspector.get_pk_constraint(name).get("constrained_columns") ==
             [column.name for column in table.primary_key], "STORAGE_PRIMARY_KEY_MISMATCH")
    _require(str(inspector.get_table_options(name).get("mysql_engine", "")).lower() == "innodb",
             "TRANSACTIONAL_STORAGE_REQUIRED")
    foreign_keys = inspector.get_foreign_keys(name)
    _require(all(fk.get("referred_schema") in (None, inspector.default_schema_name)
                 and str(fk.get("options", {}).get("ondelete", "")).upper() != "CASCADE"
                 for fk in foreign_keys), "STORAGE_FOREIGN_KEY_MISMATCH")
    actual_fks = {(tuple(fk["constrained_columns"]), fk["referred_table"],
                   tuple(fk["referred_columns"])) for fk in foreign_keys}
    expected_fks = {(tuple(column.name for column in fk.columns), fk.referred_table.name,
                     tuple(element.column.name for element in fk.elements))
                    for fk in table.foreign_key_constraints}
    _require(expected_fks.issubset(actual_fks), "STORAGE_FOREIGN_KEY_MISMATCH")
    actual_unique = {tuple(item.get("column_names") or ())
                     for item in inspector.get_unique_constraints(name)}
    actual_unique.update(tuple(item.get("column_names") or ())
                         for item in inspector.get_indexes(name) if item.get("unique"))
    expected_unique = {tuple(column.name for column in item.columns)
                       for item in table.constraints if isinstance(item, UniqueConstraint)}
    expected_unique.update(tuple(column.name for column in item.columns)
                           for item in table.indexes if item.unique)
    _require(expected_unique.issubset(actual_unique), "STORAGE_UNIQUE_KEY_MISMATCH")


def _check_storage(app, shared):
    from sqlalchemy import inspect, select
    from ...db import check_schema, get_db
    from ...models import AdminGuard, AuditEvent, User
    from ...write_requests import WriteReceipt
    from .. import results
    from .lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from .lifecycle_support import require_lifecycle_schema
    from .models import TaskAssignment, TaskManager, TaskRecord

    with app.app_context():
        db = get_db()
        _require(db.get_bind().dialect.name == "mysql", "MYSQL_REQUIRED")
        check_schema()
        inspector = inspect(db.connection())
        for model in (AdminGuard, User, AuditEvent, TaskRecord, TaskAssignment, TaskManager, WriteReceipt):
            _check_table(inspector, model.__table__)
        for table in results.RESULT_TABLES:
            _check_table(inspector, table)
        _require(db.scalar(select(AdminGuard.id).where(AdminGuard.id == 1)) == 1,
                 "COMMON_GUARD_REQUIRED")
        require_lifecycle_schema(db)
        # The helper checks generated pending/FK constraints; inspect complete
        # column/nullable/identity contracts as well, including decision NULLs.
        for model in (TaskCancellation, TaskDeadlineExtension):
            _check_table(inspector, model.__table__)
        if shared is not None:
            shared.require_schema(db)
            for table in shared.TABLES:
                _check_table(inspector, table)
            resource = app.extensions["results_resource_attachments"]
            # Existing cross-resource references must survive task cancellation.
            # No resource tables remains valid for a task-only shared store.
            if resource.require_schema(db, optional=True):
                for table in resource.TABLES:
                    _check_table(inspector, table)
        # Teardown rolls back these reads; no commit/DDL/seed is performed.


def install(app):
    """Install the reviewed ACK only after explicit opt-in and successful checks."""
    requested = app.config.get(CONFIG_KEY, os.getenv(ENV_KEY, ""))
    if requested == "":
        return
    _require(type(requested) is str and requested == CONTRACT, "EXACT_CONTRACT_REQUIRED")
    _require(not app._got_first_request, "STARTUP_ONLY")
    from .lifecycle_support import INTEGRATED_ACK
    existing = app.extensions.get(ACK_KEY)
    _require(ACK_KEY not in app.extensions or existing == INTEGRATED_ACK, "EXISTING_ACK_CONFLICT")
    shared = _check_composition(app)
    try:
        _check_storage(app, shared)
    except ActivationError:
        raise
    except Exception:
        # Driver messages may include private connection settings or SQL values.
        raise ActivationError("Task lifecycle activation failed: STORAGE_CHECK_FAILED.") from None
    if ACK_KEY not in app.extensions:
        app.extensions[ACK_KEY] = dict(INTEGRATED_ACK)
