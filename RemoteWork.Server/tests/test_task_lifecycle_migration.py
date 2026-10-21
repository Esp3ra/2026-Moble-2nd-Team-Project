"""Member1 raw009 review in owned MySQL only; no integrated member3 acknowledgment."""
import os
import subprocess
from pathlib import Path

import pytest
from sqlalchemy import inspect, text
from sqlalchemy.exc import IntegrityError, OperationalError

from remotework.db import transaction
from remotework.common import AppError
from remotework.features.tasks.lifecycle_support import require_lifecycle_schema
from test_task_lifecycle import lifecycle_flags, life as original_life, key_for, send
from test_task_migration_draft import owned_databases, _mysql_tool, _private_file

SQL = Path(__file__).resolve().parents[1] / "migrations/009_task_lifecycle.mysql.sql"
TABLES = ("task_cancellations", "task_deadline_extensions")


@pytest.fixture
def raw009(original_life):
    app, ids, task_id = original_life
    engine = app.extensions["db_engine"]
    assert engine.dialect.name == "mysql" and engine.url.database.startswith("remote_work_test_")
    with engine.begin() as conn:
        for name in TABLES:
            assert conn.exec_driver_sql("SELECT COUNT(*) FROM " + name).scalar_one() == 0
        for name in reversed(TABLES):
            conn.exec_driver_sql("DROP TABLE " + name)
        script = "\n".join(line for line in SQL.read_text(encoding="utf8").splitlines() if not line.startswith("--"))
        for statement in script.split(";"):
            if statement.strip(): conn.exec_driver_sql(statement)
    return original_life


def test_raw009_physical_contract_and_real_preparation_check(raw009):
    app, _ids, _task_id = raw009
    engine = app.extensions["db_engine"]
    inspector = inspect(engine)
    for name, count in ((TABLES[0], 2), (TABLES[1], 3)):
        fks = inspector.get_foreign_keys(name)
        assert len(fks) == count
        assert all(f.get("referred_schema") in (None, engine.url.database) for f in fks)
        assert all(f.get("options", {}).get("ondelete", "RESTRICT") in ("RESTRICT", "NO ACTION") for f in fks)
        options = inspector.get_table_options(name)
        assert options["mysql_engine"] == "InnoDB" and options["mysql_collate"] == "utf8mb4_unicode_ci"
    columns = {c["name"]: c for c in inspector.get_columns(TABLES[1])}
    assert str(columns["task_id"]["type"]) == str(columns["extension_id"]["type"]) == "BIGINT"
    assert str(columns["assignment_revision"]["type"]) == str(columns["requested_by"]["type"]) == "INTEGER"
    assert columns["pending_revision"]["computed"]["persisted"] is True
    assert any(i["unique"] and i["column_names"] == ["task_id", "pending_revision"] for i in inspector.get_indexes(TABLES[1]))
    with app.app_context(), transaction() as db:
        require_lifecycle_schema(db)
        assert db.execute(text("SELECT version FROM schema_versions ORDER BY version")).scalars().all() == [1]


@pytest.mark.parametrize("decision", ["task.deadline_extension_approved", "task.deadline_extension_rejected", "task.deadline_extension_withdrawn"])
def test_raw009_actual_actions_receipts_and_closed_history(raw009, client, login, decision):
    env = raw009
    staff, _ = login("staff1", "Staff-test-2026!")
    manager, _ = login("admin1", "Admin-test-2026!")
    request_key = key_for(client, staff, env, "task.deadline_extension_requested")
    requested = send(client, staff, env, "task.deadline_extension_requested", key=request_key)
    assert requested.status_code == 200, requested.json
    result = requested.json["data"]
    assert len(result) == 8 and result["row_version"] == "8"
    assert send(client, staff, env, "task.deadline_extension_requested", key=request_key).json["data"] == result
    actor = staff if decision.endswith("withdrawn") else manager
    key = key_for(client, actor, env, decision)
    response = send(client, actor, env, decision, version="8", extension_id=result["extension_id"], key=key)
    assert response.status_code == 200, response.json
    saved = response.json["data"]
    assert saved["row_version"] == "9" and saved["assignment_revision"] == 2 and saved["task_status"] == "in_progress"
    assert send(client, actor, env, decision, version="8", extension_id=result["extension_id"], key=key).json["data"] == saved
    recovered = client.post("/api/v1/write-requests/result", headers=actor | {"Idempotency-Key": key},
        json={"action": decision, "target": env[2]})
    assert recovered.status_code == 200 and recovered.json["data"]["result"] == saved


def test_raw009_unique_pending_fk_and_status_check(raw009):
    engine = raw009[0].extensions["db_engine"]
    task_id, staff = int(raw009[2]), raw009[1]["staff1"]
    insert = ("INSERT INTO task_deadline_extensions(task_id,assignment_revision,original_due_date,requested_due_date,"
        "requested_by,reason,status,created_at) VALUES(%s,%s,'2026-10-19','2026-10-23',%s,'fixture',%s,UTC_TIMESTAMP())")
    for revision, actor, state, errno in [(999, staff, "pending", 1452), (2, 2147483647, "pending", 1452), (2, staff, "PENDING", 3819)]:
        with pytest.raises((IntegrityError, OperationalError)) as failure, engine.begin() as conn:
            conn.exec_driver_sql(insert, (task_id, revision, actor, state))
        assert failure.value.orig.args[0] == errno
    with engine.begin() as conn: conn.exec_driver_sql(insert, (task_id, 2, staff, "pending"))
    with pytest.raises(IntegrityError), engine.begin() as conn:
        conn.exec_driver_sql(insert, (task_id, 2, staff, "pending"))
    with engine.begin() as conn:
        conn.exec_driver_sql("UPDATE task_deadline_extensions SET status='withdrawn',decided_at=UTC_TIMESTAMP(),decided_by=%s", (staff,))
        conn.exec_driver_sql(insert, (task_id, 2, staff, "pending"))
        assert conn.exec_driver_sql("SELECT COUNT(*) FROM task_deadline_extensions").scalar_one() == 2


def test_raw009_preparation_rejects_same_name_user_table_in_other_database(raw009, owned_databases):
    app = raw009[0]; engine = app.extensions["db_engine"]
    other = owned_databases("lifecycle_fk_other")
    with other.begin() as conn:
        conn.exec_driver_sql("CREATE TABLE users(id INTEGER NOT NULL PRIMARY KEY) ENGINE=InnoDB")
    foreign = next(f for f in inspect(engine).get_foreign_keys(TABLES[0]) if f["constrained_columns"] == ["cancelled_by"])
    original = foreign["name"]
    assert original.replace("_", "").isalnum()
    with engine.begin() as conn:
        conn.exec_driver_sql("ALTER TABLE task_cancellations DROP FOREIGN KEY `" + original + "`, "
            "ADD CONSTRAINT review_foreign_user FOREIGN KEY(cancelled_by) REFERENCES `" + other.url.database + "`.users(id)")
    try:
        with app.app_context(), pytest.raises(AppError) as failure, transaction() as db:
            require_lifecycle_schema(db)
        assert failure.value.code == "TASK_LIFECYCLE_SCHEMA_REQUIRED"
    finally:
        with engine.begin() as conn:
            conn.exec_driver_sql("ALTER TABLE task_cancellations DROP FOREIGN KEY review_foreign_user, "
                "ADD CONSTRAINT `" + original + "` FOREIGN KEY(cancelled_by) REFERENCES users(id)")


def test_raw009_cancel_and_result_data_survive_real_new_database_restore(raw009, client, login, owned_databases, tmp_path):
    app, _ids, task_id = raw009
    manager, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, manager, raw009, "task.cancelled")
    response = send(client, manager, raw009, "task.cancelled", key=key)
    assert response.status_code == 200, response.json
    source = app.extensions["db_engine"]
    def snapshot(engine):
        with engine.connect() as conn:
            return {name: conn.exec_driver_sql("SELECT * FROM " + name).all() for name in
                (*TABLES, "tasks", "task_assignments", "task_managers", "write_receipts", "audit_events", "schema_versions")}
    before = snapshot(source)
    assert len(before["task_cancellations"]) == 1 and response.json["data"]["task_status"] == "in_progress"
    config = tmp_path / "lifecycle.private.cnf"
    dump = tmp_path / "lifecycle.private.sql"
    def quote(value): return '"' + str(value).replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'
    try:
        _private_file(config); _private_file(dump)
        config.write_text("[client]\nprotocol=TCP\nhost=" + quote(source.url.host) + "\nport=" + str(source.url.port or 3306)
            + "\nuser=" + quote(source.url.username or "") + "\npassword=" + quote(source.url.password or "")
            + "\ndefault-character-set=utf8mb4\n", encoding="utf8")
        env = os.environ.copy(); env.pop("MYSQL_PWD", None)
        saved = subprocess.run([_mysql_tool("mysqldump"), "--defaults-file=" + config.name, "--no-login-paths",
            "--no-tablespaces", "--set-gtid-purged=OFF", "--single-transaction", "--skip-comments", "--skip-add-locks",
            "--skip-lock-tables", "--result-file=" + dump.name, source.url.database], cwd=tmp_path, env=env, capture_output=True)
        assert saved.returncode == 0, "Owned lifecycle dump failed; credentials/output omitted"
        restored = owned_databases("lifecycle_restore")
        assert inspect(restored).get_table_names() == [] and b"CREATE DATABASE" not in dump.read_bytes().upper()
        loaded = subprocess.run([_mysql_tool("mysql"), "--defaults-file=" + config.name, "--no-login-paths",
            "--binary-mode=1", "--database=" + restored.url.database], cwd=tmp_path, env=env,
            input=dump.read_bytes(), capture_output=True)
        assert loaded.returncode == 0, "Owned lifecycle restore failed; credentials/output omitted"
        assert snapshot(restored) == before
        assert inspect(restored).get_columns(TABLES[1])[-1]["name"] == "pending_revision"
    finally:
        config.unlink(missing_ok=True); dump.unlink(missing_ok=True)
