import json
import subprocess
import sys
from pathlib import Path
from unittest.mock import Mock

import pytest
from sqlalchemy import event

from remotework import create_app
from remotework.integration_readiness import check_readiness


def test_default_does_not_connect_and_does_not_print_configuration(app):
    engine = app.extensions["db_engine"]
    with event_connection_trap(engine):
        result = check_readiness(app)
    assert result["status"] == "configuration_only"
    assert result["database_checked"] is False
    assert "DATABASE_URL" not in json.dumps(result) and "SECRET_KEY" not in json.dumps(result)


class event_connection_trap:
    def __init__(self, engine): self.engine = engine
    def fail(self, *args): raise AssertionError("Unexpected DB connection")
    def __enter__(self): event.listen(self.engine, "engine_connect", self.fail)
    def __exit__(self, *args): event.remove(self.engine, "engine_connect", self.fail)


@pytest.mark.parametrize("feature", ["tasks", "notices", "chat", "writes"])
def test_disabled_feature_fails_before_database(app, feature):
    with event_connection_trap(app.extensions["db_engine"]):
        result = check_readiness(app, features=(feature,), database=True)
    assert result["code"] == "FEATURE_ROUTES_REQUIRED"
    assert not result["database_checked"]


def test_unknown_feature_cannot_be_used_as_sql_name(app):
    with pytest.raises(ValueError): check_readiness(app, features=("users; DROP TABLE users",))


def test_real_mysql_common_is_read_only(app):
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql": pytest.skip("Dedicated MySQL required")
    statements = []
    def record(conn, cursor, statement, *args): statements.append(statement.strip().upper())
    event.listen(engine, "before_cursor_execute", record)
    try:
        result = check_readiness(app, database=True)
    finally:
        event.remove(engine, "before_cursor_execute", record)
    assert result["code"] == "LOCAL_PREREQUISITES_OK", result
    assert result["database_checked"] and result["schema_version_1_recorded"]
    assert all(s.startswith(("SELECT", "SHOW", "DESCRIBE", "SET TRANSACTION READ ONLY")) for s in statements)
    assert any(s == "SET TRANSACTION READ ONLY" for s in statements)


def test_missing_tables_and_columns_are_reported_without_repair(app, monkeypatch):
    from sqlalchemy import inspect
    from remotework.integration_readiness import TABLES
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql": pytest.skip("Dedicated MySQL required")
    before = set(inspect(engine).get_table_names())
    expected = TABLES["common"] | {"users": TABLES["common"]["users"] + ("missing_review_column",),
                                  "missing_review_table": ("id",)}
    monkeypatch.setitem(TABLES, "common", expected)
    result = check_readiness(app, database=True)
    assert result["code"] == "SCHEMA_PREPARATION_REQUIRED"
    assert result["missing_tables"] == ["missing_review_table"]
    assert result["missing_columns"] == {"users": ["missing_review_column"]}
    assert set(inspect(engine).get_table_names()) == before
    assert "missing_review_column" not in {c["name"] for c in inspect(engine).get_columns("users")}


def test_enabled_feature_report_uses_actual_route_and_schema_registration(app):
    from remotework.db import init_schema
    config = {"TESTING": True, "SECRET_KEY": "public-test-only-" + "x" * 32,
              "DATABASE_URL": app.config["DATABASE_URL"], "ENABLE_COMMON_WRITE_DRAFT": True,
              "ENABLE_TASK_QUERY_DRAFT": True, "ENABLE_CHAT_DRAFT": True, "ENABLE_NOTICE_DRAFT": True}
    enabled = create_app(config)
    try:
        if enabled.extensions["db_engine"].dialect.name != "mysql":
            pytest.skip("Dedicated MySQL required")
        with enabled.app_context(): init_schema()  # Explicit test setup only, not performed by checker.
        result = check_readiness(enabled, features=("tasks", "notices", "chat", "writes"), database=True)
        assert result["code"] == "LOCAL_PREREQUISITES_OK", result
        assert all(result["routes"].values())
    finally: enabled.extensions["db_engine"].dispose()


def test_sqlite_is_not_reported_as_mysql(app):
    engine = Mock(); engine.dialect.name = "sqlite"
    old = app.extensions["db_engine"]; app.extensions["db_engine"] = engine
    try: result = check_readiness(app, database=True)
    finally: app.extensions["db_engine"] = old
    assert result["code"] == "MYSQL_REQUIRED"
    engine.connect.assert_not_called()


def test_fresh_cli_configuration_error_is_redacted(tmp_path):
    import os
    env = os.environ.copy()
    env.update(PYTHON_DOTENV_DISABLED="1", SECRET_KEY="private-invalid-value", PYTHONIOENCODING="utf-8")
    completed = subprocess.run([sys.executable, "-m", "remotework.integration_readiness"],
                               cwd=Path(__file__).resolve().parents[1], env=env, capture_output=True,
                               text=True, encoding="utf-8")
    assert completed.returncode == 1
    result = json.loads(completed.stdout)
    assert result["code"] == "CONFIGURATION_ERROR"
    assert "private-invalid-value" not in completed.stdout + completed.stderr


def test_database_errors_do_not_echo_driver_credentials(app, monkeypatch):
    from sqlalchemy.exc import OperationalError
    engine = Mock(); engine.dialect.name = "mysql"
    engine.connect.side_effect = OperationalError("query", {}, Exception("PRIVATE-secret-url"))
    monkeypatch.setitem(app.extensions, "db_engine", engine)
    result = check_readiness(app, database=True)
    assert result["code"] == "DATABASE_UNAVAILABLE"
    assert "PRIVATE" not in json.dumps(result)
