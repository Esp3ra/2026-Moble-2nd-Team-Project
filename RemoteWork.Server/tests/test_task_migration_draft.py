"""Member 2's reserved DDL, tested only on newly owned disposable MySQL DBs.

This is not a deployment runner. No shared .env, task create_all, schema-version
update, mutation route or member 3 submission/reviewer table is introduced.
"""
from datetime import date, datetime, timedelta
import os
from pathlib import Path
import re
import shutil
import subprocess
import uuid

import pytest
from sqlalchemy import create_engine, inspect, select, text
from sqlalchemy.engine import make_url
from sqlalchemy.exc import DBAPIError
from sqlalchemy.orm import Session
from werkzeug.security import generate_password_hash

from remotework import create_app
from remotework.auth import token_digest
from remotework.features.tasks.models import TASK_STATUSES, TaskAssignment, TaskManager, TaskRecord
from remotework.models import (
    AdminGuard, AuditEvent, AuthSession, Base, LoginFailure, ResourceGrant, SchemaVersion, User, utcnow,
)


SQL_PATH = Path(__file__).resolve().parents[1] / "migrations" / "002_task_queries.mysql.sql"
COMMON_TABLES = (User.__table__, AuthSession.__table__, LoginFailure.__table__, AuditEvent.__table__,
                 ResourceGrant.__table__, SchemaVersion.__table__, AdminGuard.__table__)
TASK_TABLES = (TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__)
OPT_IN = "REMOTEWORK_RUN_TASK_MIGRATION_DRAFT_TESTS"
TEST_URL = "MYSQL_MIGRATION_TEST_URL"
TOKENS = {user_id: f"task-migration-fixture-token-{user_id}" for user_id in range(1, 7)}


def _guarded_url(env):
    if env.get(OPT_IN) != "1" or not env.get(TEST_URL):
        raise ValueError("Both the explicit migration test URL and opt-in=1 are required")
    try:
        url = make_url(env[TEST_URL])
    except Exception:
        raise ValueError("Invalid migration test URL (value omitted)") from None
    if (url.drivername != "mysql+pymysql" or url.host not in {"127.0.0.1", "localhost", "::1"}
            or not re.fullmatch(r"remote_work_test_[A-Za-z0-9_]+", url.database or "")
            or set(url.query) - {"charset"}
            or url.query.get("charset", "utf8mb4") != "utf8mb4"):
        raise ValueError("Only loopback disposable remote_work_test_ MySQL URLs are allowed (value omitted)")
    return url


@pytest.mark.parametrize("settings", [
    {},
    {TEST_URL: "mysql+pymysql://root@127.0.0.1/remote_work_test_guard"},
    {OPT_IN: "1"},
    {OPT_IN: "0", TEST_URL: "mysql+pymysql://root@127.0.0.1/remote_work_test_guard"},
    {OPT_IN: "1", TEST_URL: "mysql+pymysql://root@192.0.2.1/remote_work_test_guard"},
    {OPT_IN: "1", TEST_URL: "mysql+pymysql://root@127.0.0.1/remote_work"},
    {OPT_IN: "1", TEST_URL: "sqlite:///remote_work_test_guard"},
    {OPT_IN: "1", TEST_URL: "mysql+pymysql://root@127.0.0.1/remote_work_test_guard?read_default_file=other"},
])
def test_migration_guard_rejects_missing_or_unsafe_targets_before_connect(settings):
    with pytest.raises(ValueError):
        _guarded_url(settings)


def test_migration_guard_accepts_explicit_disposable_loopback_target():
    url = _guarded_url({OPT_IN: "1", TEST_URL:
                        "mysql+pymysql://test@127.0.0.1:54321/remote_work_test_guard?charset=utf8mb4"})
    assert url.database == "remote_work_test_guard" and url.port == 54321


def _statements():
    sql = "\n".join(line for line in SQL_PATH.read_text(encoding="utf-8").splitlines()
                    if not line.lstrip().startswith("--"))
    statements = [part.strip() for part in sql.split(";") if part.strip()]
    assert len(statements) == 3
    assert [re.match(r"CREATE TABLE (\w+)", part).group(1) for part in statements] == [
        "tasks", "task_assignments", "task_managers"]
    return statements


def test_draft_contains_only_three_table_creates_without_new_server_defaults_or_version_writes():
    sql = "\n".join(_statements()).upper()
    assert all(value not in sql for value in (
        "IF NOT EXISTS", " DEFAULT ", "CASCADE", "SCHEMA_VERSIONS", "CURRENT_TIMESTAMP", "DROP TABLE"))


@pytest.fixture
def owned_databases():
    if not os.getenv(TEST_URL) and not os.getenv(OPT_IN):
        pytest.skip("Disposable MySQL migration URL and explicit opt-in are not configured")
    # Do not print the URL, including on validation errors.
    try:
        target = _guarded_url(os.environ)
    except ValueError as exc:
        # A normal traceback may render the environment argument, including secrets.
        pytest.fail(str(exc), pytrace=False)
    admin = create_engine(target.set(database=None), hide_parameters=True, pool_pre_ping=True)
    owned = []
    engines = []
    try:
        with admin.connect() as connection:
            version = str(connection.exec_driver_sql("SELECT VERSION()").scalar_one())
            numbers = re.match(r"(\d+)\.(\d+)\.(\d+)", version)
            assert numbers and "mariadb" not in version.lower()
            assert tuple(map(int, numbers.groups())) >= (8, 0, 16), "MySQL CHECK support is required"

        def create(label):
            # Names are generated here, never received as cleanup targets.
            name = "remote_work_test_m2ddl_" + label + "_" + uuid.uuid4().hex[:16]
            assert re.fullmatch(r"remote_work_test_m2ddl_[a-z_]+_[0-9a-f]{16}", name)
            with admin.begin() as connection:
                connection.exec_driver_sql(
                    "CREATE DATABASE `" + name + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")
            owned.append(name)  # Only CREATE DATABASE success confers cleanup ownership.
            engine = create_engine(target.set(database=name), hide_parameters=True, pool_pre_ping=True)
            engines.append(engine)
            return engine

        yield create
    finally:
        for engine in engines:
            engine.dispose()
        try:
            for name in reversed(owned):
                assert re.fullmatch(r"remote_work_test_m2ddl_[a-z_]+_[0-9a-f]{16}", name)
                with admin.begin() as connection:
                    connection.exec_driver_sql("DROP DATABASE `" + name + "`")
        finally:
            admin.dispose()


@pytest.fixture(scope="module")
def fixture_password_hash():
    return generate_password_hash("Migration-fixture-only-2026!", method="scrypt")


def _seed_common(engine, password_hash):
    # Explicit selection matters: imported task models already inhabit Base.metadata.
    Base.metadata.create_all(engine, tables=COMMON_TABLES)
    assert set(inspect(engine).get_table_names()) == {table.name for table in COMMON_TABLES}
    now = utcnow()
    with Session(engine) as db, db.begin():
        for user_id, role in ((1, "admin"), (2, "employee"), (3, "employee"),
                              (4, "admin"), (5, "employee"), (6, "employee")):
            db.add(User(id=user_id, login_id=f"migration_{user_id}", password_hash=password_hash,
                        display_name=f"조회 직원 {user_id}", employee_no=f"MIG-{user_id}",
                        department="테스트 부서", role=role, is_active=user_id != 6,
                        created_at=now - timedelta(days=1)))
        db.flush()
        for user_id, token in TOKENS.items():
            db.add(AuthSession(token_hash=token_digest(token), user_id=user_id, channel="api",
                               created_at=now, expires_at=now + timedelta(hours=1)))
        db.add(LoginFailure(account_key="fixture-account", ip_key="fixture-ip", created_at=now))
        db.add(ResourceGrant(user_id=2, resource_type="task", resource_id="102"))
        db.add(AuditEvent(actor_id=1, action="migration.fixture_history", resource_type="user",
                          resource_id="6", details_json='{"fixture":true}', created_at=now))
        db.add(AdminGuard(id=1))
        db.add(SchemaVersion(version=1, applied_at=now))


def _common_snapshot(engine):
    with engine.connect() as connection:
        return {table.name: [tuple(row) for row in connection.execute(
            select(table).order_by(*table.primary_key.columns))] for table in COMMON_TABLES}


def _apply_draft(engine):
    # Test-only helper; the only connections come from owned_databases above.
    assert (engine.url.database or "").startswith("remote_work_test_m2ddl_")
    with engine.begin() as connection:
        for statement in _statements():
            connection.exec_driver_sql(statement)


@pytest.fixture
def migrated_db(owned_databases, fixture_password_hash):
    engine = owned_databases("normal")
    _seed_common(engine, fixture_password_hash)
    before = _common_snapshot(engine)
    _apply_draft(engine)
    assert _common_snapshot(engine) == before
    return engine


def _normal_check(expression):
    # MySQL reflection adds backticks/parentheses/charset introducers.
    expression = re.sub(r"(?<![a-z0-9'])_(?:utf8mb4|utf8mb3|utf8|latin1)(?=')", "",
                        str(expression).lower())
    return re.sub(r"[\s`()]", "", expression)


def test_check_normalization_preserves_status_codes_and_detects_different_values():
    expected = "task_status IN ('not_started','in_progress','submitted','approved','returned') " \
               "AND HEX(task_status) IN (HEX('not_started'),HEX('in_progress')," \
               "HEX('submitted'),HEX('approved'),HEX('returned'))"
    reflected = "((`task_status` in (_utf8mb4'not_started',_utf8mb4'in_progress',_utf8mb4'submitted'," \
                "_utf8mb4'approved',_utf8mb4'returned')) and (hex(`task_status`) in " \
                "(hex(_utf8mb4'not_started'),hex(_utf8mb4'in_progress'),hex(_utf8mb4'submitted')," \
                "hex(_utf8mb4'approved'),hex(_utf8mb4'returned'))))"
    assert _normal_check(expected) == _normal_check(reflected)
    assert "not_started" in _normal_check(reflected)
    assert "in_progress" in _normal_check(reflected)
    assert _normal_check(reflected) != _normal_check(reflected.replace("not_started", "not_deleted"))


def test_raw_ddl_matches_current_orm_and_preserves_common_history_and_version(migrated_db):
    inspector = inspect(migrated_db)
    assert set(inspector.get_table_names()) == {table.name for table in COMMON_TABLES + TASK_TABLES}
    for table in TASK_TABLES:
        columns = {column["name"]: column for column in inspector.get_columns(table.name)}
        assert set(columns) == set(table.columns.keys())
        for column in table.columns:
            actual = columns[column.name]
            actual_type = actual["type"].copy()
            if hasattr(actual_type, "collation") and not getattr(column.type, "collation", None):
                actual_type.collation = None  # ORM inherits the database collation.
            assert str(actual_type.compile(dialect=migrated_db.dialect)) == str(
                column.type.compile(dialect=migrated_db.dialect))
            assert actual["nullable"] == column.nullable
            assert actual["default"] is None
            assert not getattr(actual["type"], "unsigned", False)
        assert columns["task_id"].get("autoincrement", False) == (table.name == "tasks")
        assert inspector.get_pk_constraint(table.name)["constrained_columns"] == [
            column.name for column in table.primary_key.columns]
        foreign_keys = inspector.get_foreign_keys(table.name)
        expected_fk = {(tuple(constraint.column_keys), constraint.referred_table.name,
                        tuple(element.column.name for element in constraint.elements))
                       for constraint in table.foreign_key_constraints}
        assert {(tuple(fk["constrained_columns"]), fk["referred_table"], tuple(fk["referred_columns"]))
                for fk in foreign_keys} == expected_fk
        assert all(fk.get("options", {}).get("ondelete", "RESTRICT") in {"RESTRICT", "NO ACTION"}
                   for fk in foreign_keys)
        assert {check["name"]: _normal_check(check["sqltext"])
                for check in inspector.get_check_constraints(table.name)} == {
                    constraint.name: _normal_check(constraint.sqltext)
                    for constraint in table.constraints if constraint.__class__.__name__ == "CheckConstraint"}
        indexes = {index["name"]: index for index in inspector.get_indexes(table.name)}
        for expected in table.indexes:
            assert indexes[expected.name]["column_names"] == [column.name for column in expected.columns]
            assert not indexes[expected.name]["unique"]
        expected_names = {index.name for index in table.indexes}
        support_columns = {tuple(fk["constrained_columns"]) for fk in foreign_keys}
        assert all(tuple(index["column_names"]) in support_columns for name, index in indexes.items()
                   if name not in expected_names)
    with migrated_db.connect() as connection:
        assert connection.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version").all() == [(1,)]
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM users WHERE is_active=0").scalar_one() == 1
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM audit_events").scalar_one() == 1
        engines = connection.execute(text("SELECT ENGINE FROM information_schema.TABLES "
            "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('tasks','task_assignments','task_managers')"))
        assert [row[0] for row in engines] == ["InnoDB"] * 3
        charset = connection.exec_driver_sql(
            "SELECT DEFAULT_CHARACTER_SET_NAME FROM information_schema.SCHEMATA "
            "WHERE SCHEMA_NAME=DATABASE()").scalar_one()
        assert charset == "utf8mb4"


def _task_values(**changes):
    values = dict(task_id=901, title="제약 검증", description="", task_status="not_started",
                  start_date=date(2026, 10, 17), due_date=date(2026, 10, 20),
                  current_assignment_revision=1, row_version=1, created_by=1,
                  created_at=datetime(2026, 10, 17), updated_at=datetime(2026, 10, 17))
    return values | changes


def _insert_task(connection, values):
    columns = list(values)
    connection.execute(text("INSERT INTO tasks (" + ",".join(columns) + ") VALUES ("
                            + ",".join(":" + column for column in columns) + ")"), values)


@pytest.fixture(params=("sqlite_orm", "mysql_orm", "mysql_raw"))
def status_constraint_db(request, tmp_path, fixture_password_hash):
    """Exercise the actual ORM schema on both DBs and the MySQL SQL file separately."""
    if request.param == "sqlite_orm":
        engine = create_engine("sqlite:///" + (tmp_path / "status-check.db").as_posix(),
                               hide_parameters=True)
    else:
        create_owned = request.getfixturevalue("owned_databases")
        engine = create_owned(request.param)
    try:
        _seed_common(engine, fixture_password_hash)
        before = _common_snapshot(engine)
        if request.param == "mysql_raw":
            _apply_draft(engine)
        else:
            Base.metadata.create_all(engine, tables=TASK_TABLES)
        yield engine, request.param, before
    finally:
        if request.param == "sqlite_orm":
            engine.dispose()


def _write_task_status(engine, schema_source, task_id, status, *, inserting):
    if schema_source == "mysql_raw":
        with engine.begin() as connection:
            if inserting:
                _insert_task(connection, _task_values(task_id=task_id, task_status=status))
            else:
                connection.execute(text("UPDATE tasks SET task_status=:status WHERE task_id=:id"),
                                   {"id": task_id, "status": status})
    else:
        with Session(engine) as db, db.begin():
            if inserting:
                db.add(TaskRecord(**_task_values(task_id=task_id, task_status=status)))
            else:
                db.get(TaskRecord, task_id).task_status = status
            db.flush()  # Exercise the actual DB constraint, not a Python validator.


def test_status_constraint_accepts_only_exact_codes_on_insert_and_update(status_constraint_db):
    engine, source, common_before = status_constraint_db
    canonical = ("not_started", "in_progress", "submitted", "approved", "returned")
    assert TASK_STATUSES == canonical
    with engine.connect() as connection:
        if engine.dialect.name == "mysql":
            collation = connection.exec_driver_sql(
                "SELECT COLLATION_NAME FROM information_schema.COLUMNS "
                "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tasks' "
                "AND COLUMN_NAME='task_status'").scalar_one()
            assert collation == "utf8mb4_unicode_ci"
            # Establish the case/trailing-space hazard in this actual column collation.
            assert connection.exec_driver_sql(
                "SELECT _utf8mb4'APPROVED' COLLATE utf8mb4_unicode_ci = _utf8mb4'approved', "
                "_utf8mb4'approved ' COLLATE utf8mb4_unicode_ci = _utf8mb4'approved'").one() == (1, 1)

    for index, status in enumerate(canonical):
        _write_task_status(engine, source, 1000 + index, status, inserting=True)
        _write_task_status(engine, source, 1000, status, inserting=False)
        with engine.connect() as connection:
            assert connection.execute(text("SELECT task_status FROM tasks WHERE task_id=:id"),
                                      {"id": 1000 + index}).scalar_one() == status
            assert connection.exec_driver_sql(
                "SELECT task_status FROM tasks WHERE task_id=1000").scalar_one() == status

    malformed = [variant for status in canonical for variant in (
        status.upper(), status[0].upper() + status[1:], status + " ", " " + status,
        status + "\t", status + "\0")]
    malformed += ["", "unknown_status", "approvéd", "ａｐｐｒｏｖｅｄ"]
    if engine.dialect.name == "sqlite":
        # HEX alone would accept these same bytes as BLOB, unlike the original TEXT IN check.
        malformed += [factory(status.encode("ascii")) for status in canonical
                      for factory in (bytes, bytearray, memoryview)]
    for status in malformed:
        for inserting in (True, False):
            with pytest.raises(DBAPIError) as caught:
                _write_task_status(engine, source, 2000 if inserting else 1000, status,
                                   inserting=inserting)
            assert "ck_tasks_draft_status" in str(caught.value.orig)
            if engine.dialect.name == "mysql":
                assert caught.value.orig.args[0] == 3819
            with engine.connect() as connection:
                assert connection.exec_driver_sql("SELECT COUNT(*) FROM tasks").scalar_one() == 5
                assert connection.exec_driver_sql(
                    "SELECT task_status FROM tasks WHERE task_id=1000").scalar_one() == "returned"
    assert _common_snapshot(engine) == common_before


@pytest.mark.parametrize("changes", [
    {"task_status": "unknown_status"}, {"due_date": date(2026, 10, 16)},
    {"current_assignment_revision": 0}, {"row_version": 0}, {"created_by": 999999},
])
def test_raw_ddl_rejects_invalid_status_dates_revisions_version_and_user_fk(migrated_db, changes):
    with pytest.raises(DBAPIError), migrated_db.begin() as connection:
        _insert_task(connection, _task_values(**changes))
    with migrated_db.connect() as connection:
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM tasks").scalar_one() == 0


def test_no_implicit_task_status_row_version_or_record_time_defaults(migrated_db):
    for missing in ("task_status", "row_version", "created_at", "updated_at"):
        values = _task_values()
        values.pop(missing)
        with pytest.raises(DBAPIError), migrated_db.begin() as connection:
            _insert_task(connection, values)


def _seed_tasks(engine):
    with Session(engine) as db, db.begin():
        for task_id, assignee, status, manager in (
            (101, 2, "in_progress", 1), (102, 3, "not_started", 4),
            (103, 1, "submitted", 4), (104, 2, "returned", 4),
            (105, 2, "approved", 1), (106, 2, "returned", 1),
            (107, 2, "not_started", 1), (108, 2, "submitted", 1),
        ):
            db.add(TaskRecord(task_id=task_id, title=f"실제 DDL 업무 {task_id}", description="조회 본문",
                task_status=status, start_date=date(2026, 10, 17), due_date=date(2026, 10, 20),
                current_assignment_revision=2 if task_id == 104 else 1, created_by=1))
            db.flush()
            db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=assignee, assigned_by=1))
            db.add(TaskManager(task_id=task_id, user_id=manager))
        db.add(TaskAssignment(task_id=104, revision=2, assignee_id=3, assigned_by=4))


def test_assignment_manager_foreign_keys_checks_and_history_restrict_deletes(migrated_db):
    _seed_tasks(migrated_db)
    invalid = (
        "INSERT INTO task_assignments VALUES (999999,1,2,1,'2026-10-17',NULL)",
        "INSERT INTO task_assignments VALUES (101,2,999999,1,'2026-10-17',NULL)",
        "INSERT INTO task_assignments VALUES (101,2,2,999999,'2026-10-17',NULL)",
        "INSERT INTO task_assignments VALUES (101,0,2,1,'2026-10-17',NULL)",
        "INSERT INTO task_managers VALUES (999999,1)",
        "INSERT INTO task_managers VALUES (101,999999)",
        "DELETE FROM users WHERE id=2", "DELETE FROM tasks WHERE task_id=101",
    )
    for statement in invalid:
        with pytest.raises(DBAPIError), migrated_db.begin() as connection:
            connection.exec_driver_sql(statement)
    with migrated_db.connect() as connection:
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM task_assignments WHERE task_id=104").scalar_one() == 2
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM task_managers WHERE task_id=101").scalar_one() == 1


def test_current_get_contract_auth_scope_filters_and_direct_detail_on_raw_ddl(migrated_db, monkeypatch):
    _seed_tasks(migrated_db)
    before = _common_snapshot(migrated_db)
    # create_app normally loads .env; this test must never read actual local settings.
    monkeypatch.setattr("remotework.load_dotenv", lambda *_args, **_kwargs: None)
    app = create_app({"TESTING": True, "SECRET_KEY": "migration-only-" + "x" * 40,
                      "DATABASE_URL": migrated_db.url, "ENABLE_TASK_QUERY_DRAFT": True})
    try:
        client = app.test_client()

        def headers(user_id):
            return {"Authorization": "Bearer " + TOKENS[user_id]}

        def ids(user_id, scope="own", status=None):
            path = "/api/v1/tasks?scope=" + scope + ("&status=" + status if status else "")
            response = client.get(path, headers=headers(user_id))
            assert response.status_code == 200
            assert response.json["ok"] and response.json["request_id"]
            return [item["task_id"] for item in response.json["data"]["items"]]

        assert ids(2) == ["101", "105", "106", "107", "108"]
        assert ids(1) == ["103"]
        assert ids(1, "managed") == ["101", "105", "106", "107", "108"]
        assert ids(4, "managed") == ["102", "103", "104"]
        assert ids(3) == ["102", "104"] and ids(5) == []
        for status in ("not_started", "in_progress", "submitted", "approved", "returned"):
            assert len(ids(2, status=status)) == 1
        assert client.get("/api/v1/tasks/101", headers=headers(2)).json["data"]["task"]["description"] == "조회 본문"
        for user_id, task_id in ((2, 102), (2, 104), (1, 102), (4, 101)):
            response = client.get(f"/api/v1/tasks/{task_id}", headers=headers(user_id))
            assert response.status_code == 403 and response.json["error"]["code"] == "FORBIDDEN"
        assert client.get("/api/v1/tasks?scope=managed", headers=headers(2)).status_code == 403
        assert client.get("/api/v1/tasks/999999", headers=headers(2)).json["error"]["code"] == "TASK_NOT_FOUND"
        assert client.get("/api/v1/tasks").status_code == 401
        assert client.get("/api/v1/tasks", headers=headers(6)).status_code == 401
        assert client.get("/api/v1/health").json["data"]["schema_version"] == 1
        for method in ("POST", "PATCH", "DELETE"):
            assert client.open("/api/v1/tasks/101", method=method, headers=headers(1)).status_code == 405
        assert _common_snapshot(migrated_db) == before
    finally:
        app.extensions["db_engine"].dispose()


def test_rerun_rejects_existing_schema_without_changing_existing_rows(migrated_db):
    _seed_tasks(migrated_db)
    before = _common_snapshot(migrated_db)
    with pytest.raises(DBAPIError):
        _apply_draft(migrated_db)
    assert _common_snapshot(migrated_db) == before
    with migrated_db.connect() as connection:
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM tasks").scalar_one() == 8


def _make_conflict(engine):
    with engine.begin() as connection:
        connection.exec_driver_sql("CREATE TABLE task_assignments (collision_marker INTEGER NOT NULL)")
        connection.exec_driver_sql("INSERT INTO task_assignments VALUES (73)")


def _assert_partial_ddl(engine, before):
    with pytest.raises(DBAPIError):
        _apply_draft(engine)
    # engine.begin() rolled back after failure; MySQL's preceding CREATE TABLE remains.
    assert set(inspect(engine).get_table_names()) == {table.name for table in COMMON_TABLES} | {
        "tasks", "task_assignments"}
    assert _common_snapshot(engine) == before
    with engine.connect() as connection:
        assert connection.exec_driver_sql("SELECT collision_marker FROM task_assignments").all() == [(73,)]
        assert connection.exec_driver_sql("SELECT COUNT(*) FROM tasks").scalar_one() == 0


def test_partial_ddl_failure_is_not_undone_by_transaction_rollback(owned_databases, fixture_password_hash):
    engine = owned_databases("partial")
    _seed_common(engine, fixture_password_hash)
    before = _common_snapshot(engine)
    _make_conflict(engine)
    _assert_partial_ddl(engine, before)


def _private_file(path):
    path.touch(exist_ok=False)
    if os.name == "nt":
        account = os.environ.get("USERDOMAIN", "") + "\\" + os.environ["USERNAME"]
        result = subprocess.run(["icacls", str(path), "/inheritance:r", "/grant:r", account + ":F"],
                                capture_output=True)
        assert result.returncode == 0, "Cannot protect temporary test credential/dump file"
    else:
        path.chmod(0o600)
    return path


def _mysql_tool(name):
    directory = os.getenv("MYSQL_MIGRATION_TEST_BIN_DIR")
    suffix = ".exe" if os.name == "nt" else ""
    tool = str(Path(directory) / (name + suffix)) if directory else shutil.which(name)
    assert tool and Path(tool).is_file(), "mysql and mysqldump are required for actual recovery verification"
    return tool


def test_real_dump_restore_into_new_owned_db_then_retry_ddl(owned_databases, fixture_password_hash, tmp_path):
    source = owned_databases("recovery_source")
    _seed_common(source, fixture_password_hash)
    before = _common_snapshot(source)

    def quoted(value):
        return '"' + str(value).replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'

    config = tmp_path / "mysql.private.cnf"
    dump = tmp_path / "common-before.private.sql"
    try:
        _private_file(config)
        config.write_text("[client]\nprotocol=TCP\nhost=" + quoted(source.url.host)
                          + "\nport=" + str(source.url.port or 3306)
                          + "\nuser=" + quoted(source.url.username or "")
                          + "\npassword=" + quoted(source.url.password or "")
                          + "\ndefault-character-set=utf8mb4\n", encoding="utf-8")
        _private_file(dump)
        child_env = os.environ.copy()
        child_env.pop("MYSQL_PWD", None)
        dumped = subprocess.run([
            _mysql_tool("mysqldump"), "--defaults-file=" + str(config), "--no-login-paths", "--no-tablespaces",
            "--set-gtid-purged=OFF", "--single-transaction", "--skip-comments", "--skip-add-locks",
            "--skip-lock-tables", "--result-file=" + str(dump), source.url.database,
        ], env=child_env, capture_output=True)
        assert dumped.returncode == 0, "Dedicated fixture backup failed (credential/output values omitted)"
        assert dump.stat().st_size > 0
        assert b"CREATE DATABASE" not in dump.read_bytes().upper()
        _make_conflict(source)
        _assert_partial_ddl(source, before)

        restored = owned_databases("recovery_target")
        assert inspect(restored).get_table_names() == []
        result = subprocess.run([
            _mysql_tool("mysql"), "--defaults-file=" + str(config), "--no-login-paths", "--binary-mode=1",
            "--database=" + restored.url.database,
        ], env=child_env, input=dump.read_bytes(), capture_output=True)
        assert result.returncode == 0, "Restore into a new owned DB failed (credential/output values omitted)"
        assert _common_snapshot(restored) == before
        assert set(inspect(restored).get_table_names()) == {table.name for table in COMMON_TABLES}
        _apply_draft(restored)
        assert _common_snapshot(restored) == before
        assert set(inspect(restored).get_table_names()) == {table.name for table in COMMON_TABLES + TASK_TABLES}
    finally:
        config.unlink(missing_ok=True)
        dump.unlink(missing_ok=True)
