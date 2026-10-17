"""Reserved 003 raw DDL review. Only newly owned disposable loopback MySQL DBs.

No shared configuration, Notice.create_all, version writes or deployment runner.
"""
from datetime import timedelta
import os
from pathlib import Path
import re
import shutil
import subprocess
import uuid

import pytest
from sqlalchemy import Boolean, create_engine, inspect, select, text
from sqlalchemy.engine import make_url
from sqlalchemy.exc import DBAPIError
from sqlalchemy.orm import Session
from werkzeug.security import generate_password_hash

from remotework import create_app
from remotework.auth import token_digest
from remotework.features.notices.models import Notice
from remotework.models import (Base, User, AuthSession, LoginFailure, AuditEvent,
                              ResourceGrant, SchemaVersion, AdminGuard, utcnow)

SQL = Path(__file__).resolve().parents[1] / "migrations" / "003_notices.mysql.sql"
COMMON = (User.__table__, AuthSession.__table__, LoginFailure.__table__, AuditEvent.__table__,
          ResourceGrant.__table__, SchemaVersion.__table__, AdminGuard.__table__)
URL_ENV = "NOTICE_MIGRATION_TEST_URL"
OPT_IN = "REMOTEWORK_RUN_NOTICE_MIGRATION_DRAFT_TESTS"
TOKENS = {n: f"notice-ddl-synthetic-token-{n}" for n in (1, 2, 3)}


def _target(settings):
    if settings.get(OPT_IN) != "1" or not settings.get(URL_ENV):
        raise ValueError("Explicit notice migration test opt-in and URL are required")
    try:
        url = make_url(settings[URL_ENV])
    except Exception:
        raise ValueError("Invalid notice migration test URL (value omitted)") from None
    if (url.drivername != "mysql+pymysql" or url.host not in {"127.0.0.1", "localhost", "::1"}
            or not re.fullmatch(r"remote_work_test_[A-Za-z0-9_]+", url.database or "")
            or set(url.query) - {"charset"} or url.query.get("charset", "utf8mb4") != "utf8mb4"):
        raise ValueError("Only disposable loopback MySQL targets are permitted (value omitted)")
    return url


@pytest.mark.parametrize("settings", [
    {}, {OPT_IN: "1"},
    {URL_ENV: "mysql+pymysql://test@127.0.0.1/remote_work_test_guard"},
    {OPT_IN: "1", URL_ENV: "mysql+pymysql://test@192.0.2.1/remote_work_test_guard"},
    {OPT_IN: "1", URL_ENV: "mysql+pymysql://test@127.0.0.1/remote_work"},
    {OPT_IN: "1", URL_ENV: "mysql+pymysql://test@127.0.0.1/remote_work_test_guard?read_default_file=x"},
])
def test_notice_ddl_guard_rejects_unapproved_targets_before_connection(settings):
    with pytest.raises(ValueError):
        _target(settings)


def test_notice_ddl_guard_accepts_explicit_disposable_target():
    assert _target({OPT_IN: "1", URL_ENV:
        "mysql+pymysql://test@127.0.0.1:54321/remote_work_test_guard?charset=utf8mb4"}).port == 54321


def _statement():
    sql = "\n".join(line for line in SQL.read_text(encoding="utf-8").splitlines()
                    if not line.lstrip().startswith("--"))
    parts = [s.strip() for s in sql.split(";") if s.strip()]
    assert len(parts) == 1 and parts[0].startswith("CREATE TABLE notices (")
    return parts[0]


def test_notice_sql_is_one_additive_create_without_defaults_versions_or_cascade():
    statement = _statement().upper()
    assert all(word not in statement for word in
        ("IF NOT EXISTS", "CASCADE", "SCHEMA_VERSIONS", "CURRENT_TIMESTAMP", "DROP TABLE", "ALTER TABLE"))
    # DEFAULT CHARSET is an encoding option, not a new field default.
    assert " DEFAULT " not in statement[:statement.index(") ENGINE=")]


@pytest.fixture
def owned_notice_dbs():
    if not os.getenv(URL_ENV) and not os.getenv(OPT_IN):
        pytest.skip("Explicit notice raw-DDL MySQL opt-in/URL are not set")
    try:
        target = _target(os.environ)
    except ValueError as error:
        pytest.fail(str(error), pytrace=False)
    admin = create_engine(target.set(database=None), hide_parameters=True, pool_pre_ping=True)
    names, engines = [], []
    try:
        with admin.connect() as connection:
            assert "MariaDB" not in connection.exec_driver_sql("SELECT VERSION()").scalar_one()
        def create(label):
            name = "remote_work_test_n4ddl_" + label + "_" + uuid.uuid4().hex[:12]
            assert re.fullmatch(r"remote_work_test_n4ddl_[a-z_]+_[a-f0-9]{12}", name)
            with admin.begin() as connection:
                connection.exec_driver_sql("CREATE DATABASE `" + name + "` CHARACTER SET utf8mb4")
            names.append(name)  # Cleanup only DBs successfully created by this fixture.
            engine = create_engine(target.set(database=name), hide_parameters=True, pool_pre_ping=True)
            engines.append(engine)
            return engine
        yield create
    finally:
        for engine in engines:
            engine.dispose()
        try:
            for name in reversed(names):
                assert re.fullmatch(r"remote_work_test_n4ddl_[a-z_]+_[a-f0-9]{12}", name)
                with admin.begin() as connection:
                    connection.exec_driver_sql("DROP DATABASE `" + name + "`")
        finally:
            admin.dispose()


@pytest.fixture(scope="module")
def notice_ddl_hash():
    return generate_password_hash("Notice-DDL-synthetic-only-2026!", method="scrypt")


def _seed(engine, password_hash):
    Base.metadata.create_all(engine, tables=COMMON)  # Never create the Notice table here.
    assert set(inspect(engine).get_table_names()) == {t.name for t in COMMON}
    now = utcnow().replace(microsecond=0)
    with Session(engine) as db, db.begin():
        for n, role in ((1, "admin"), (2, "employee"), (3, "employee")):
            db.add(User(id=n, login_id=f"notice_ddl_{n}", password_hash=password_hash,
                display_name=f"Synthetic {n}", employee_no=f"NDDL{n}",
                department="기획팀" if n == 3 else "개발팀", role=role, is_active=True, created_at=now))
        db.flush()
        for n, token in TOKENS.items():
            db.add(AuthSession(token_hash=token_digest(token), user_id=n, channel="api",
                created_at=now, expires_at=now + timedelta(hours=1)))
        db.add(LoginFailure(account_key="synthetic-account", ip_key="synthetic-ip", created_at=now))
        db.add(ResourceGrant(user_id=2, resource_type="task", resource_id="synthetic-101"))
        db.add(AuditEvent(actor_id=1, action="notice.ddl.fixture", resource_type="user",
            resource_id="1", details_json="{}", created_at=now))
        db.add(SchemaVersion(version=1, applied_at=now))
        db.add(AdminGuard(id=1))


def _snapshot(engine, tables=COMMON):
    with engine.connect() as connection:
        return {t.name: [tuple(row) for row in connection.execute(select(t).order_by(*t.primary_key.columns))]
                for t in tables}


def _apply(engine):
    assert (engine.url.database or "").startswith("remote_work_test_n4ddl_")
    with engine.begin() as connection:
        connection.exec_driver_sql(_statement())


@pytest.fixture
def notice_raw_db(owned_notice_dbs, notice_ddl_hash):
    engine = owned_notice_dbs("raw")
    _seed(engine, notice_ddl_hash)
    before = _snapshot(engine)
    _apply(engine)
    assert _snapshot(engine) == before
    return engine


def _row(**changes):
    return dict(title="Synthetic notice", content="Persisted by raw DDL", target="개발팀",
        is_pinned=False, author_id=1, author="Synthetic 1", created_at=utcnow(), attachment_names_json="[]") | changes


def test_raw_notice_schema_matches_model_and_preserves_common_data_and_version(notice_raw_db):
    inspector = inspect(notice_raw_db)
    assert set(inspector.get_table_names()) == {t.name for t in COMMON} | {"notices"}
    columns = {c["name"]: c for c in inspector.get_columns("notices")}
    assert set(columns) == set(Notice.__table__.columns.keys())
    for expected in Notice.__table__.columns:
        actual = columns[expected.name]
        assert actual["nullable"] == expected.nullable and actual["default"] is None
        assert not getattr(actual["type"], "unsigned", False)
        if isinstance(expected.type, Boolean):
            assert actual["type"].__class__.__name__ == "TINYINT" and actual["type"].display_width == 1
        else:
            assert str(actual["type"].compile(dialect=notice_raw_db.dialect)) == str(expected.type.compile(dialect=notice_raw_db.dialect))
    assert columns["id"]["autoincrement"]
    assert inspector.get_pk_constraint("notices")["constrained_columns"] == ["id"]
    fks = inspector.get_foreign_keys("notices")
    assert len(fks) == 1 and fks[0]["constrained_columns"] == ["author_id"]
    assert fks[0]["referred_table"] == "users" and fks[0]["referred_columns"] == ["id"]
    assert fks[0].get("options", {}).get("ondelete", "RESTRICT") in {"RESTRICT", "NO ACTION"}
    indexes = {i["name"]: i for i in inspector.get_indexes("notices")}
    assert indexes["ix_notices_target_created"]["column_names"] == ["target", "created_at"]
    assert not inspector.get_check_constraints("notices")  # No unagreed validation/checks added.
    with notice_raw_db.connect() as connection:
        assert connection.exec_driver_sql("SELECT version FROM schema_versions").all() == [(1,)]
        assert connection.exec_driver_sql("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='notices'").scalar_one() == "InnoDB"


@pytest.mark.parametrize("changes", [{"author_id": 999}, {"title": None},
    {"created_at": None}, {"attachment_names_json": None}])
def test_raw_notice_foreign_key_and_not_null_constraints(notice_raw_db, changes):
    with pytest.raises(DBAPIError):
        with notice_raw_db.begin() as connection:
            connection.execute(Notice.__table__.insert(), _row(**changes))


def test_author_deactivation_preserves_notice_snapshot_and_fk_restricts_user_deletion(notice_raw_db, notice_ddl_hash):
    with notice_raw_db.begin() as connection:
        # This user has no session/grant/audit references: the Notice FK must do the restriction.
        connection.execute(User.__table__.insert(), dict(id=10, login_id="author_only",
            password_hash=notice_ddl_hash, display_name="Former author", employee_no="NDDL10",
            department="개발팀", role="employee", is_active=True, created_at=utcnow()))
        connection.execute(Notice.__table__.insert(), _row(author_id=10, author="Former author"))
        connection.execute(User.__table__.update().where(User.id == 10).values(is_active=False, display_name="Changed name"))
    with notice_raw_db.connect() as connection:
        assert connection.execute(select(Notice.author)).scalar_one() == "Former author"
    with pytest.raises(DBAPIError):
        with notice_raw_db.begin() as connection:
            connection.execute(User.__table__.delete().where(User.id == 10))


def test_raw_notice_api_roles_filters_persistence_delete_and_audit(notice_raw_db):
    app = create_app({"TESTING": True, "SECRET_KEY": "notice-ddl-synthetic-secret-" + "x" * 32,
        "DATABASE_URL": notice_raw_db.url.render_as_string(hide_password=False), "ENABLE_NOTICE_DRAFT": True})
    headers = {n: {"Authorization": "Bearer " + token} for n, token in TOKENS.items()}
    try:
        with app.test_client() as client:
            assert client.get("/api/v1/notices").status_code == 401
            created = client.post("/api/v1/notices", headers=headers[1], json={
                "title": "DDL/API", "content": "Stored", "target": "개발팀", "is_pinned": True})
            assert created.status_code == 201
            notice = created.json["data"]["notice"]; path = f'/api/v1/notices/{notice["id"]}'
            assert notice["author"] == "Synthetic 1" and notice["attachment_names"] == []
            assert client.get(path, headers=headers[3]).json["data"]["notice"] == notice
            assert client.get("/api/v1/notices?target=개발팀", headers=headers[3]).json["data"]["total"] == 1
            assert client.get("/api/v1/notices?target=기획팀", headers=headers[3]).json["data"]["total"] == 0
            assert client.post(path + "/delete", headers=headers[2], json={}).status_code == 403
            assert client.post(path + "/delete", headers=headers[1], json={}).status_code == 200
            assert client.post(path + "/delete", headers=headers[1], json={}).status_code == 404
        with Session(notice_raw_db) as db:
            assert len(db.scalars(select(AuditEvent).where(AuditEvent.action == "notice.created")).all()) == 1
            assert len(db.scalars(select(AuditEvent).where(AuditEvent.action == "notice.deleted")).all()) == 1
    finally:
        app.extensions["db_engine"].dispose()


def test_rerun_refuses_existing_notice_table_without_data_loss(notice_raw_db):
    with notice_raw_db.begin() as connection:
        connection.execute(Notice.__table__.insert(), _row())
    before = _snapshot(notice_raw_db, COMMON + (Notice.__table__,))
    with pytest.raises(DBAPIError):
        _apply(notice_raw_db)
    assert _snapshot(notice_raw_db, COMMON + (Notice.__table__,)) == before


def test_missing_users_fails_without_creating_notice_or_other_tables(owned_notice_dbs):
    engine = owned_notice_dbs("missing")
    with pytest.raises(DBAPIError):
        _apply(engine)
    assert inspect(engine).get_table_names() == []


def test_notice_ddl_preserves_existing_002_task_tables_and_version(owned_notice_dbs, notice_ddl_hash):
    engine = owned_notice_dbs("with_tasks");_seed(engine, notice_ddl_hash)
    task_sql = SQL.with_name("002_task_queries.mysql.sql").read_text(encoding="utf-8")
    statements = "\n".join(l for l in task_sql.splitlines() if not l.lstrip().startswith("--")).split(";")
    from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
    with engine.begin() as connection:
        for statement in statements:
            if statement.strip(): connection.exec_driver_sql(statement)
        connection.exec_driver_sql("INSERT INTO tasks VALUES (101,'Synthetic task','Original','not_started','2026-10-17','2026-10-18',1,1,1,'2026-10-17 00:00:00','2026-10-17 00:00:00')")
        connection.exec_driver_sql("INSERT INTO task_assignments VALUES (101,1,2,1,'2026-10-17 00:00:00',NULL)")
        connection.exec_driver_sql("INSERT INTO task_managers VALUES (101,1)")
    tables = COMMON + (TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__)
    before = _snapshot(engine, tables);_apply(engine)
    assert _snapshot(engine, tables) == before


def _private(path):
    path.touch(exist_ok=False)
    if os.name == "nt":
        who = os.environ.get("USERDOMAIN", "") + "\\" + os.environ["USERNAME"]
        result = subprocess.run(["icacls", str(path), "/inheritance:r", "/grant:r", who + ":F"], capture_output=True)
        assert result.returncode == 0, "Cannot protect the temporary fixture backup/config"
    else:
        path.chmod(0o600)


def _tool(name):
    directory = os.getenv("MYSQL_MIGRATION_TEST_BIN_DIR")
    tool = str(Path(directory) / (name + (".exe" if os.name == "nt" else ""))) if directory else shutil.which(name)
    assert tool and Path(tool).is_file(), "mysql/mysqldump are required for actual fixture recovery"
    return tool


def test_real_pre_ddl_backup_restore_to_new_db_then_apply_003(owned_notice_dbs, notice_ddl_hash, tmp_path):
    source = owned_notice_dbs("backup");_seed(source, notice_ddl_hash);before = _snapshot(source)
    config,dump = tmp_path/"notice.private.cnf",tmp_path/"notice.before.private.sql"
    def quote(v): return '"'+str(v).replace('\\','\\\\').replace('"','\\"').replace('\n','\\n')+'"'
    try:
        _private(config);_private(dump)
        config.write_text("[client]\nprotocol=TCP\nhost="+quote(source.url.host)+"\nport="+str(source.url.port or 3306)+
            "\nuser="+quote(source.url.username or "")+"\npassword="+quote(source.url.password or "")+"\ndefault-character-set=utf8mb4\n",encoding="utf-8")
        env = os.environ.copy();env.pop("MYSQL_PWD",None)
        result = subprocess.run([_tool("mysqldump"),"--defaults-file="+str(config),"--no-login-paths","--no-tablespaces",
            "--set-gtid-purged=OFF","--single-transaction","--skip-comments","--skip-add-locks","--skip-lock-tables",
            "--result-file="+str(dump),source.url.database],env=env,capture_output=True)
        assert result.returncode == 0 and dump.stat().st_size > 0,"Owned fixture backup failed (output omitted)"
        _apply(source)
        with pytest.raises(DBAPIError): _apply(source)
        restored = owned_notice_dbs("restored")
        result = subprocess.run([_tool("mysql"),"--defaults-file="+str(config),"--no-login-paths","--binary-mode=1",
            "--database="+restored.url.database],input=dump.read_bytes(),env=env,capture_output=True)
        assert result.returncode == 0,"Restore into the new owned DB failed (output omitted)"
        assert _snapshot(restored) == before and "notices" not in inspect(restored).get_table_names()
        _apply(restored);assert _snapshot(restored) == before
    finally:
        config.unlink(missing_ok=True);dump.unlink(missing_ok=True)
