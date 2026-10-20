"""Controlled prerequisite checks; no shared DB, real login, DDL or seed."""
import importlib
import json
import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock

import pytest
from flask import Flask
from sqlalchemy.dialects import mysql
from sqlalchemy.exc import OperationalError

from remotework.features.tasks import readiness


def make_app(enabled=True, *, routes=True, dialect="mysql"):
    app = Flask(__name__)
    app.config["ENABLE_TASK_QUERY_DRAFT"] = enabled
    if routes:
        for path, endpoint in readiness._ROUTES:
            app.add_url_rule(path, endpoint=endpoint, view_func=lambda: "", methods=["GET"])
    app.extensions["db_engine"] = SimpleNamespace(
        dialect=SimpleNamespace(name=dialect), dispose=Mock())
    return app


def no_db(monkeypatch):
    session = Mock(side_effect=AssertionError("must not open database"))
    monkeypatch.setattr(readiness.db, "get_db", session)
    return session


def controlled_db(monkeypatch, *, missing_tables=(), missing_columns=None,
                  missing_versions=(), counts=(0, 0, 0, 0, 0, 0), connection_error=None):
    inspector = Mock()
    inspector.has_table.side_effect = lambda name: name not in missing_tables
    inspector.get_columns.side_effect = lambda name: [
        {"name": column} for column in (*readiness._REQUIRED_COLUMNS[name], "private_extra_column")
        if column not in (missing_columns or {}).get(name, ())]
    session = Mock()
    if connection_error is not None:
        session.connection.side_effect = connection_error
    result = iter(counts)
    statements = []

    def execute(statement):
        assert statement.is_select
        statements.append(str(statement.compile(dialect=mysql.dialect())))
        return SimpleNamespace(scalar_one=lambda: next(result))

    session.execute.side_effect = execute
    monkeypatch.setattr(readiness, "inspect", Mock(return_value=inspector))
    monkeypatch.setattr(readiness.db, "get_db", Mock(return_value=session))
    versions = Mock(return_value={"required_versions": [1], "recorded_versions": [] if missing_versions else [1],
                                  "missing_versions": list(missing_versions)})
    monkeypatch.setattr(readiness.db, "schema_status", versions)
    return session, inspector, versions, statements


def test_disabled_feature_never_opens_database(monkeypatch):
    session = no_db(monkeypatch)
    result = readiness.check_readiness(make_app(False))
    assert result["code"] == "TASK_QUERY_DISABLED"
    assert result["checks"] == {"query_feature_enabled": False}
    assert "counts" not in result
    session.assert_not_called()


def test_missing_get_route_never_opens_database(monkeypatch):
    session = no_db(monkeypatch)
    app = make_app(routes=False)
    # A POST or another feature's similarly named URL is not our GET route.
    app.add_url_rule("/api/v1/tasks", endpoint="other", view_func=lambda: "", methods=["POST"])
    assert readiness.check_readiness(app)["code"] == "TASK_QUERY_ROUTES_REQUIRED"
    session.assert_not_called()


@pytest.mark.parametrize("dialect", ["sqlite", "postgresql"])
def test_unsupported_database_never_opens_it(monkeypatch, dialect):
    session = no_db(monkeypatch)
    result = readiness.check_readiness(make_app(dialect=dialect))
    assert result["code"] == "MYSQL_REQUIRED"
    assert not result["checks"]["mysql"]
    session.assert_not_called()


def test_missing_table_is_not_proved_by_common_version(monkeypatch):
    session, inspector, versions, _ = controlled_db(monkeypatch, missing_tables=("task_assignments",))
    result = readiness.check_readiness(make_app())
    assert result["code"] == "TASK_QUERY_SCHEMA_REQUIRED"
    assert result["missing_tables"] == ["task_assignments"]
    assert result["checks"]["schema_version_1_recorded"] is True
    versions.assert_called_once_with((1,))
    session.execute.assert_not_called()
    assert "task_assignments" not in [call.args[0] for call in inspector.get_columns.call_args_list]


def test_missing_version_table_does_not_attempt_version_query(monkeypatch):
    session, _, versions, _ = controlled_db(monkeypatch, missing_tables=("schema_versions",))
    result = readiness.check_readiness(make_app())
    assert result["code"] == "TASK_QUERY_SCHEMA_REQUIRED"
    assert "schema_version_1_recorded" not in result["checks"]
    versions.assert_not_called()
    session.execute.assert_not_called()


def test_missing_required_columns_are_fixed_labels_only(monkeypatch):
    session, _, _, _ = controlled_db(monkeypatch, missing_columns={
        "tasks": ("description",), "users": ("display_name",)})
    result = readiness.check_readiness(make_app())
    assert result["missing_columns"] == {"users": ["display_name"], "tasks": ["description"]}
    assert "private_extra_column" not in json.dumps(result)
    session.execute.assert_not_called()


def test_required_columns_cover_actual_orm_query_projection():
    # The existing query selects full ORM entities, including fields omitted
    # from its JSON. Product readiness must not import/register those models.
    from remotework.features.tasks.queries import _current_assignment_query

    for selected in _current_assignment_query().selected_columns:
        assert selected.name in readiness._REQUIRED_COLUMNS[selected.table.name]


@pytest.mark.parametrize(("table_name", "column_name"), [
    ("tasks", "row_version"), ("tasks", "created_by"),
    ("tasks", "created_at"), ("tasks", "updated_at"),
    ("task_assignments", "assigned_by"), ("task_assignments", "assigned_at"),
    ("task_assignments", "accepted_at"),
])
def test_missing_non_json_orm_column_blocks_before_relation_queries(monkeypatch, table_name, column_name):
    session, _, versions, _ = controlled_db(monkeypatch, missing_columns={table_name: (column_name,)})
    result = readiness.check_readiness(make_app())
    assert result["code"] == "TASK_QUERY_SCHEMA_REQUIRED"
    assert result["missing_columns"] == {table_name: [column_name]}
    assert result["checks"]["schema_version_1_recorded"] is True
    versions.assert_called_once_with((1,))
    session.execute.assert_not_called()


def test_missing_common_version_does_not_run_relation_queries(monkeypatch):
    session, _, versions, _ = controlled_db(monkeypatch, missing_versions=(1,))
    result = readiness.check_readiness(make_app())
    assert result["code"] == "COMMON_SCHEMA_VERSION_REQUIRED"
    assert result["checks"]["required_columns_present"] is True
    assert result["checks"]["schema_version_1_recorded"] is False
    versions.assert_called_once_with((1,))
    session.execute.assert_not_called()


@pytest.mark.parametrize("task_count", [0, 3])
def test_empty_or_existing_tasks_need_no_seed_or_commit(monkeypatch, task_count):
    session, inspector, _, statements = controlled_db(monkeypatch, counts=(task_count, 0, 0, 0, 0, 0))
    result = readiness.check_readiness(make_app())
    assert result["code"] == "TASK_QUERY_LOCAL_PREREQUISITES_OK"
    assert result["status"] == "local_prerequisites_checked"
    assert result["counts"]["task_count"] == task_count
    assert len(statements) == 6
    assert all(statement.startswith("SELECT count(") for statement in statements)
    assert all(word not in " ".join(statements).upper() for word in ("INSERT ", "UPDATE ", "DELETE ", "CREATE ", "ALTER ", "DROP "))
    session.commit.assert_not_called()
    session.add.assert_not_called()
    inspector.get_foreign_keys.assert_not_called()
    assert len(result["not_checked"]) == 5


@pytest.mark.parametrize("index", range(1, 6))
def test_each_missing_relation_blocks_readiness_without_row_details(monkeypatch, index):
    counts = [3, 0, 0, 0, 0, 0]
    counts[index] = 1
    session, _, _, _ = controlled_db(monkeypatch, counts=counts)
    result = readiness.check_readiness(make_app())
    assert result["code"] == "TASK_QUERY_RELATIONS_INVALID"
    assert list(result["counts"].values()) == counts
    assert "rows" not in result and "user_id" not in result and "task_id" not in result
    session.commit.assert_not_called()


def test_database_errors_do_not_expose_original_error(monkeypatch, capsys):
    secret = "mysql+pymysql://private_user:private_password@private_host/private_database"
    controlled_db(monkeypatch, connection_error=OperationalError(secret, {}, Exception(secret)))
    result = readiness.check_readiness(make_app())
    assert result["code"] == "TASK_QUERY_DATABASE_UNAVAILABLE"
    assert "private_" not in json.dumps(result)
    assert capsys.readouterr() == ("", "")


def test_main_hides_configuration_error_and_returns_nonzero(monkeypatch, capsys):
    common = importlib.import_module("remotework")
    monkeypatch.setattr(common, "create_app", Mock(side_effect=RuntimeError("private_password private_host")))
    assert readiness.main() == 1
    captured = capsys.readouterr()
    result = json.loads(captured.out)
    assert result["code"] == "TASK_QUERY_CONFIGURATION_ERROR"
    assert "private_" not in captured.out and not captured.err


def test_main_prints_one_result_and_disposes_engine(monkeypatch, capsys):
    common = importlib.import_module("remotework")
    app = make_app()
    controlled_db(monkeypatch)
    monkeypatch.setattr(common, "create_app", Mock(return_value=app))
    assert readiness.main() == 0
    captured = capsys.readouterr()
    assert len(captured.out.splitlines()) == 1 and not captured.err
    assert json.loads(captured.out)["code"] == "TASK_QUERY_LOCAL_PREREQUISITES_OK"
    app.extensions["db_engine"].dispose.assert_called_once_with()


def test_cold_module_import_does_not_register_task_metadata():
    code = """
import json, sys
from remotework.models import Base
before = set(Base.metadata.tables)
from remotework.features.tasks import readiness
print(json.dumps({'same': set(Base.metadata.tables) == before,
 'task_models_imported': 'remotework.features.tasks.models' in sys.modules}))
"""
    result = subprocess.run([sys.executable, "-c", code],
                            cwd=Path(__file__).resolve().parents[1],
                            capture_output=True, text=True, timeout=20, check=True)
    assert json.loads(result.stdout) == {"same": True, "task_models_imported": False}
    assert not result.stderr
