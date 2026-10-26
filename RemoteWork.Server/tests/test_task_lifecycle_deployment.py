"""Activation boundaries with explicit storage doubles; no deployment approval.

The real MySQL/HTTP lifecycle runner separately exercises the actual installer.
These tests never connect to an existing database or install a shared ACK.
"""
from dataclasses import replace
import os
import subprocess
import sys
from types import SimpleNamespace

import pytest

from remotework import create_app
from remotework.features.tasks import lifecycle_deployment as deployment
from remotework.features.tasks.lifecycle_support import INTEGRATED_ACK, TEST_DOUBLE_ACK


@pytest.fixture(autouse=True)
def clean_activation_environment(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in tuple(os.environ):
        if name.startswith("REMOTEWORK_"):
            monkeypatch.delenv(name)


def config(**changes):
    return dict(TESTING=True, SECRET_KEY="owned-activation-test-" + "x" * 32,
                DATABASE_URL="sqlite:///:memory:", ENABLE_TASK_QUERY_DRAFT=True,
                ENABLE_COMMON_WRITE_DRAFT=True, ENABLE_TASK_LIFECYCLE_DRAFT=True,
                ENABLE_RESULTS_DRAFT=True, RESULTS_CONTEXT_MODE="local-development",
                TASK_LIFECYCLE_INTEGRATION="", **changes)


@pytest.fixture
def app():
    value = create_app(config())
    yield value
    value.extensions["db_engine"].dispose()


@pytest.fixture
def storage_double(monkeypatch):
    calls = []
    monkeypatch.setattr(deployment, "_check_storage", lambda app, shared: calls.append((app, shared)))
    return calls


def activate(app):
    app.config[deployment.CONFIG_KEY] = deployment.CONTRACT
    deployment.install(app)


def test_default_off_does_not_register_feature_metadata_or_connect():
    source = '''
import sys
from sqlalchemy.engine import Engine
def blocked(*args, **kwargs):
    raise AssertionError("Default startup must not connect")
Engine.connect = blocked
from remotework import create_app
from remotework.models import Base
app = create_app({"SECRET_KEY":"owned-cold-activation-" + "x" * 32,
    "DATABASE_URL":"mysql+pymysql://unused:unused@127.0.0.1:1/unused"})
assert "task_lifecycle_integration_ack" not in app.extensions
assert "tasks" not in Base.metadata.tables
assert "remotework.features.results" not in sys.modules
assert "remotework.features.tasks.lifecycle" not in sys.modules
app.extensions["db_engine"].dispose()
'''
    env = {key: value for key, value in os.environ.items() if not key.startswith("REMOTEWORK_")}
    completed = subprocess.run([sys.executable, "-c", source], capture_output=True, text=True, env=env)
    assert completed.returncode == 0, completed.stderr


def test_default_off_preserves_owner_ack_and_does_not_check_storage(app, storage_double):
    marker = {"owner": "custom"}
    app.extensions[deployment.ACK_KEY] = marker
    deployment.install(app)
    assert app.extensions[deployment.ACK_KEY] is marker
    assert not storage_double


@pytest.mark.parametrize("value", [None, True, 1, "1", "integrated", "member2-task-lifecycle-v2",
                                   " member2-task-lifecycle-v1", "member2-task-lifecycle-v1\n"])
def test_only_exact_contract_is_accepted(app, storage_double, value):
    app.config[deployment.CONFIG_KEY] = value
    with pytest.raises(deployment.ActivationError, match="EXACT_CONTRACT_REQUIRED"):
        deployment.install(app)
    assert deployment.ACK_KEY not in app.extensions
    assert not storage_double


def test_environment_opt_in_is_read_and_config_empty_can_disable_it(app, storage_double, monkeypatch):
    monkeypatch.setenv(deployment.ENV_KEY, deployment.CONTRACT)
    deployment.install(app)  # explicit empty config wins
    assert not storage_double
    del app.config[deployment.CONFIG_KEY]
    deployment.install(app)
    assert storage_double == [(app, None)]
    assert app.extensions[deployment.ACK_KEY] == INTEGRATED_ACK


def test_create_app_runs_checks_after_real_common_and_result_registration(storage_double):
    settings = config()
    settings[deployment.CONFIG_KEY] = deployment.CONTRACT
    value = create_app(settings)
    try:
        assert storage_double == [(value, None)]
        assert value.extensions[deployment.ACK_KEY] == INTEGRATED_ACK
    finally:
        value.extensions["db_engine"].dispose()


@pytest.mark.parametrize("failure", ["config", "composition", "storage"])
def test_create_app_disposes_engine_when_activation_fails(monkeypatch, failure):
    from remotework import db
    captured, disposed = [], []
    original = db.init_app
    def capture(app):
        original(app)
        captured.append(app)
        engine = app.extensions["db_engine"]
        dispose = engine.dispose
        def record_dispose():
            disposed.append(engine)
            dispose()
        monkeypatch.setattr(engine, "dispose", record_dispose)
    monkeypatch.setattr(db, "init_app", capture)
    settings = config()
    settings[deployment.CONFIG_KEY] = "invalid" if failure == "config" else deployment.CONTRACT
    if failure == "composition": settings["RESULTS_CONTEXT_MODE"] = ""
    if failure == "storage":
        def fail(*args): raise RuntimeError("private database details")
        monkeypatch.setattr(deployment, "_check_storage", fail)
    with pytest.raises(deployment.ActivationError) as error:
        create_app(settings)
    assert len(captured) == 1
    assert disposed == [captured[0].extensions["db_engine"]]
    assert deployment.ACK_KEY not in captured[0].extensions
    assert "private" not in str(error.value)


@pytest.mark.parametrize("feature", ["TASK_QUERY", "COMMON_WRITE", "TASK_LIFECYCLE", "RESULTS"])
def test_required_feature_flags_fail_before_storage_or_ack(app, storage_double, feature):
    app.config["ENABLE_" + feature + "_DRAFT"] = False
    with pytest.raises(deployment.ActivationError, match=feature + "_REQUIRED"):
        activate(app)
    assert deployment.ACK_KEY not in app.extensions
    assert not storage_double


@pytest.mark.parametrize("blueprint", ["task_queries", "write_requests", "results"])
def test_actual_registered_blueprints_are_required(app, storage_double, blueprint):
    del app.blueprints[blueprint]
    with pytest.raises(deployment.ActivationError, match="BLUEPRINT_REQUIRED"):
        activate(app)
    assert not storage_double


def test_unknown_facts_owner_is_preserved_and_not_trusted(app, storage_double):
    owner = lambda *args, **kwargs: None
    app.extensions["results_facts"] = owner
    with pytest.raises(deployment.ActivationError, match="REVIEWED_RESULTS_ADAPTER_REQUIRED"):
        activate(app)
    assert app.extensions["results_facts"] is owner
    assert deployment.ACK_KEY not in app.extensions
    assert not storage_double


def test_known_adapter_requires_explicit_reviewer_context(app, storage_double):
    app.config["RESULTS_CONTEXT_MODE"] = ""
    with pytest.raises(deployment.ActivationError, match="REVIEWED_RESULTS_ADAPTER_REQUIRED"):
        activate(app)


@pytest.mark.parametrize("action", ["task.deadline_extension_requested", "task.deadline_extension_approved",
                                    "task.deadline_extension_rejected"])
def test_owner_write_callbacks_are_not_overwritten(app, storage_double, action):
    owner = lambda *args, **kwargs: True
    app.extensions["write_authorizers"][action] = owner
    with pytest.raises(deployment.ActivationError, match="REVIEWED_WRITE_CALLBACK_REQUIRED"):
        activate(app)
    assert app.extensions["write_authorizers"][action] is owner
    assert not storage_double


def test_action_contract_and_result_reader_are_checked(app, storage_double):
    action = "task.cancelled"
    original = app.extensions["write_action_contracts"][action]
    app.extensions["write_action_contracts"][action] = replace(original, result_fields=frozenset({"wrong"}))
    with pytest.raises(deployment.ActivationError, match="WRITE_RESULT_CONTRACT_REQUIRED"):
        activate(app)
    app.extensions["write_action_contracts"][action] = original
    reader = app.extensions["write_result_readers"][action]
    app.extensions["write_result_readers"][action] = replace(reader, read_only=True)
    with pytest.raises(deployment.ActivationError, match="WRITE_RESULT_READER_REQUIRED"):
        activate(app)
    assert not storage_double


def test_submission_owner_callback_is_not_replaced(app, storage_double):
    original = app.extensions["write_action_contracts"]["submission.submitted"]
    custom = replace(original, authorize=lambda *args: True)
    app.extensions["write_action_contracts"]["submission.submitted"] = custom
    with pytest.raises(deployment.ActivationError, match="REVIEWED_SUBMISSION_CALLBACK_REQUIRED"):
        activate(app)
    assert app.extensions["write_action_contracts"]["submission.submitted"] is custom
    assert not storage_double


@pytest.mark.parametrize("existing", [None, {"contract": "wrong"}, TEST_DOUBLE_ACK])
def test_conflicting_existing_ack_is_preserved(app, storage_double, existing):
    app.extensions[deployment.ACK_KEY] = existing
    with pytest.raises(deployment.ActivationError, match="EXISTING_ACK_CONFLICT"):
        activate(app)
    assert app.extensions[deployment.ACK_KEY] is existing
    assert not storage_double


def test_same_owner_ack_is_preserved_but_still_requires_checks(app, storage_double):
    existing = dict(INTEGRATED_ACK)
    app.extensions[deployment.ACK_KEY] = existing
    activate(app)
    assert app.extensions[deployment.ACK_KEY] is existing
    assert storage_double == [(app, None)]


def test_storage_failure_never_installs_ack_or_exposes_driver_details(app, monkeypatch):
    def fail(*args):
        raise RuntimeError("mysql://private-user:private-password@private-host/db")
    monkeypatch.setattr(deployment, "_check_storage", fail)
    with pytest.raises(deployment.ActivationError, match="STORAGE_CHECK_FAILED") as error:
        activate(app)
    assert "private" not in str(error.value)
    assert deployment.ACK_KEY not in app.extensions


def test_sqlite_is_not_silently_accepted_or_initialized(app):
    with pytest.raises(deployment.ActivationError, match="MYSQL_REQUIRED"):
        activate(app)
    from sqlalchemy import inspect
    assert inspect(app.extensions["db_engine"]).get_table_names() == []
    assert deployment.ACK_KEY not in app.extensions


def test_activation_is_startup_only(app, storage_double):
    app._got_first_request = True
    with pytest.raises(deployment.ActivationError, match="STARTUP_ONLY"):
        activate(app)
    assert not storage_double


def test_shared_enabled_requires_real_registered_provider(app, storage_double):
    app.config["ENABLE_SHARED_ATTACHMENTS_DRAFT"] = True
    with pytest.raises(deployment.ActivationError, match="SHARED_ATTACHMENT_PROVIDER_REQUIRED"):
        activate(app)
    assert not storage_double


def test_shared_flag_off_does_not_silently_accept_an_unregistered_provider(app, storage_double):
    app.extensions["results_shared_attachments"] = object()
    with pytest.raises(deployment.ActivationError, match="SHARED_ATTACHMENT_FLAG_MISMATCH"):
        activate(app)


def test_shared_real_provider_is_passed_to_storage_validator(storage_double):
    settings = config(ENABLE_SHARED_ATTACHMENTS_DRAFT=True)
    settings[deployment.CONFIG_KEY] = deployment.CONTRACT
    value = create_app(settings)
    try:
        from remotework.features import shared_attachments
        assert storage_double == [(value, shared_attachments)]
        assert value.extensions[deployment.ACK_KEY] == INTEGRATED_ACK
    finally:
        value.extensions["db_engine"].dispose()


def test_shared_custom_resource_provider_is_preserved_and_rejected(storage_double):
    value = create_app(config(ENABLE_SHARED_ATTACHMENTS_DRAFT=True))
    owner = object()
    value.extensions["results_resource_attachments"] = owner
    try:
        with pytest.raises(deployment.ActivationError, match="RESOURCE_ATTACHMENT_PROVIDER_REQUIRED"):
            activate(value)
        assert value.extensions["results_resource_attachments"] is owner
        assert not storage_double
    finally:
        value.extensions["db_engine"].dispose()


def inspector_double(table):
    from sqlalchemy import UniqueConstraint
    from sqlalchemy.dialects import mysql
    def reflected_type(column):
        ddl = str(column.type.compile(dialect=mysql.dialect()))
        if ddl.startswith("VARCHAR("): return mysql.VARCHAR(int(ddl[8:-1]))
        return {"INTEGER": mysql.INTEGER, "BIGINT": mysql.BIGINT, "TEXT": mysql.TEXT,
                "DATETIME": mysql.DATETIME, "DATE": mysql.DATE,
                "BOOL": lambda: mysql.TINYINT(display_width=1)}[ddl]()
    return SimpleNamespace(default_schema_name="owned",
        has_table=lambda name: True,
        get_columns=lambda name: [{"name": column.name, "type": reflected_type(column),
            "nullable": column.nullable, "autoincrement": table.autoincrement_column is column}
            for column in table.columns],
        get_pk_constraint=lambda name: {"constrained_columns": [c.name for c in table.primary_key]},
        get_table_options=lambda name: {"mysql_engine": "InnoDB"},
        get_foreign_keys=lambda name: [{"constrained_columns": [c.name for c in fk.columns],
            "referred_table": fk.referred_table.name,
            "referred_columns": [element.column.name for element in fk.elements]}
            for fk in table.foreign_key_constraints],
        get_unique_constraints=lambda name: [{"column_names": [c.name for c in constraint.columns]}
            for constraint in table.constraints if isinstance(constraint, UniqueConstraint)],
        get_indexes=lambda name: [{"unique": index.unique, "column_names": [c.name for c in index.columns]}
                                  for index in table.indexes])


@pytest.mark.parametrize("failure,code", [("table", "STORAGE_TABLE_REQUIRED"),
    ("columns", "STORAGE_COLUMNS_MISMATCH"), ("pk", "STORAGE_PRIMARY_KEY_MISMATCH"),
    ("engine", "TRANSACTIONAL_STORAGE_REQUIRED"), ("fk", "STORAGE_FOREIGN_KEY_MISMATCH"),
    ("cascade", "STORAGE_FOREIGN_KEY_MISMATCH"), ("cross-db", "STORAGE_FOREIGN_KEY_MISMATCH"),
    ("unique", "STORAGE_UNIQUE_KEY_MISMATCH")])
def test_physical_storage_boundary_checks(failure, code):
    from remotework.features.results import Submission
    table = Submission.__table__
    inspector = inspector_double(table)
    deployment._check_table(inspector, table)
    if failure == "table": inspector.has_table = lambda name: False
    elif failure == "columns": inspector.get_columns = lambda name: [{"name": "id"}]
    elif failure == "pk": inspector.get_pk_constraint = lambda name: {"constrained_columns": []}
    elif failure == "engine": inspector.get_table_options = lambda name: {"mysql_engine": "MyISAM"}
    elif failure == "fk": inspector.get_foreign_keys = lambda name: []
    elif failure in ("cascade", "cross-db"):
        keys = inspector.get_foreign_keys(table.name)
        keys[0].update({"options": {"ondelete": "CASCADE"}} if failure == "cascade" else {"referred_schema": "another"})
        inspector.get_foreign_keys = lambda name: keys
    else: inspector.get_indexes = lambda name: []
    with pytest.raises(deployment.ActivationError, match=code):
        deployment._check_table(inspector, table)


@pytest.mark.parametrize("column,field,value,code", [
    ("extension_id", "autoincrement", False, "STORAGE_AUTOINCREMENT_MISMATCH"),
    ("task_id", "autoincrement", True, "STORAGE_AUTOINCREMENT_MISMATCH"),
    ("decided_at", "nullable", False, "STORAGE_NULLABILITY_MISMATCH"),
    ("decided_by", "nullable", False, "STORAGE_NULLABILITY_MISMATCH"),
    ("decision_reason", "nullable", False, "STORAGE_NULLABILITY_MISMATCH"),
    ("requested_due_date", "nullable", True, "STORAGE_NULLABILITY_MISMATCH")])
def test_lifecycle_identity_and_decision_nullability_must_match(column, field, value, code):
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    table = TaskDeadlineExtension.__table__
    inspector = inspector_double(table)
    deployment._check_table(inspector, table)
    columns = inspector.get_columns(table.name)
    next(item for item in columns if item["name"] == column)[field] = value
    inspector.get_columns = lambda name: columns
    with pytest.raises(deployment.ActivationError, match=code):
        deployment._check_table(inspector, table)


def test_nullable_and_generated_columns_cannot_hide_type_mismatch():
    from sqlalchemy.dialects import mysql
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    table = TaskDeadlineExtension.__table__
    for column, actual_type in (("decided_at", mysql.TIMESTAMP()),
                               ("pending_revision", mysql.BIGINT()),
                               ("extension_id", mysql.BIGINT(unsigned=True)),
                               ("reason", mysql.VARCHAR(100)),
                               ("status", mysql.VARCHAR(255))):
        inspector = inspector_double(table)
        columns = inspector.get_columns(table.name)
        next(item for item in columns if item["name"] == column)["type"] = actual_type
        inspector.get_columns = lambda name: columns
        with pytest.raises(deployment.ActivationError, match="STORAGE_COLUMN_TYPE_MISMATCH"):
            deployment._check_table(inspector, table)


def test_mysql_reflection_equivalence_is_narrow():
    from sqlalchemy import BigInteger, Boolean, Date, DateTime, Integer, String, Text
    from sqlalchemy.dialects import mysql
    accepted = [(mysql.INTEGER(display_width=11), Integer()),
                (mysql.INTEGER(), Integer()), (mysql.BIGINT(display_width=20), BigInteger()),
                (mysql.TINYINT(display_width=1), Boolean()),
                (mysql.DATETIME(fsp=0), DateTime()), (mysql.DATETIME(), DateTime()),
                (mysql.DATE(), Date()), (mysql.VARCHAR(20), String(20)), (mysql.TEXT(), Text())]
    rejected = [(mysql.BIGINT(), Integer()), (mysql.INTEGER(unsigned=True), Integer()),
                (mysql.INTEGER(zerofill=True), Integer()), (mysql.SMALLINT(), Integer()),
                (mysql.TINYINT(display_width=2), Boolean()),
                (mysql.TINYINT(display_width=1, unsigned=True), Boolean()),
                (mysql.TIMESTAMP(), DateTime()), (mysql.DATETIME(fsp=6), DateTime()),
                (mysql.DATE(), DateTime()), (mysql.CHAR(20), String(20)),
                (mysql.VARCHAR(21), String(20)), (mysql.LONGTEXT(), Text()), (None, Integer())]
    for actual, expected in accepted:
        assert deployment._column_type_matches(actual, expected), (actual, expected)
    for actual, expected in rejected:
        assert not deployment._column_type_matches(actual, expected), (actual, expected)
