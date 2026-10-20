"""Actual raw008/ORM checks, ONLY on an explicitly disposable test MySQL DB."""
import os
import concurrent.futures
import uuid
from pathlib import Path
import pytest
from sqlalchemy import create_engine, inspect, select, text
from sqlalchemy.engine import make_url
from remotework import create_app
from remotework.db import init_schema, transaction, get_db
from remotework.models import Base, User, AuditEvent
from remotework.auth import create_user
from remotework.features.notices.models import Notice
from remotework.features.notices.popup_models import NoticeConfirmation
from remotework.features.notices.popup_schema import prepare_popup_schema


@pytest.fixture
def physical():
    raw=os.getenv("NOTICE_POPUP_MIGRATION_TEST_URL")
    if os.getenv("REMOTEWORK_RUN_NOTICE_POPUP_MIGRATION_TESTS") != "1" or not raw:
        pytest.skip("requires explicit isolated MySQL popup URL and opt-in")
    parsed=make_url(raw)
    if parsed.get_backend_name() != "mysql" or not (parsed.database or "").startswith("remote_work_test_popup_"):
        raise RuntimeError("Refusing a non-disposable MySQL database")
    config={"TESTING":True,"SECRET_KEY":"popup-migration-test-"+"x"*32,"DATABASE_URL":raw,
            "ENABLE_NOTICE_DRAFT":True,"ENABLE_NOTICE_POPUP_DRAFT":True,"ENABLE_CHAT_DRAFT":False,
            "ENABLE_TASK_QUERY_DRAFT":False,"ENABLE_COMMON_WRITE_DRAFT":False}
    app=create_app(config);engine=app.extensions["db_engine"]
    assert inspect(engine).get_table_names() == [], "Use a newly created empty test database"
    try:
        with app.app_context(): init_schema()
        yield app,engine,config
    finally:
        NoticeConfirmation.__table__.drop(engine,checkfirst=True)
        Base.metadata.drop_all(engine)
        engine.dispose()


def raw008(engine):
    sql=(Path(__file__).resolve().parents[1]/"migrations/008_notice_popups.mysql.sql").read_text(encoding="utf-8")
    with engine.begin() as c:c.exec_driver_sql(sql)


def rows(app):
    with app.app_context(),transaction() as db:
        u=create_user(db,dict(login_id="staff1",password="isolated-popup-test",display_name="test",employee_no="TEST1",department="개발팀",role="employee"))
        n=Notice(title="test",content="synthetic",target="전 직원",is_pinned=True,author_id=u.id,author="test")
        db.add(n);db.flush()
        db.add(NoticeConfirmation(notice_id=n.id,user_id=u.id))
        db.flush()
        return u.id,n.id


def test_raw008_and_orm_match_and_repeat_preserves_timestamp(physical):
    app,e,_=physical
    raw008(e);_,nid=rows(app)
    with e.connect() as c: before=c.execute(text("SELECT confirmed_at FROM notice_confirmations")).scalar()
    for _ in range(2):
        with app.app_context():prepare_popup_schema(e)
    with e.connect() as c:
        assert c.execute(text("SELECT confirmed_at FROM notice_confirmations")).scalar()==before
        assert c.execute(text("SELECT version FROM schema_versions ORDER BY version")).scalars().all()==[1]
    NoticeConfirmation.__table__.drop(e)
    with app.app_context(): prepare_popup_schema(e)
    # ORM preparation must pass the same physical checker as raw008.
    with app.app_context(): prepare_popup_schema(e)


@pytest.mark.parametrize("sql",[
    "ALTER TABLE notice_confirmations MODIFY notice_id INT UNSIGNED NOT NULL",
    "ALTER TABLE notice_confirmations MODIFY confirmed_at DATETIME NULL",
    "ALTER TABLE notice_confirmations MODIFY confirmed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP",
    "ALTER TABLE notice_confirmations DROP FOREIGN KEY fk_notice_confirmations_notice, ADD CONSTRAINT changed_fk FOREIGN KEY(notice_id) REFERENCES notices(id) ON DELETE CASCADE ON UPDATE CASCADE",
    "ALTER TABLE notice_confirmations COLLATE utf8mb4_general_ci",
    "ALTER TABLE notice_confirmations ADD UNIQUE KEY wrong_unique (user_id)",
])
def test_physical_mismatch_is_rejected_without_repair(physical,sql):
    app,e,_=physical;raw008(e)
    # A changed signed FK must first be dropped to permit the intentional mismatch.
    with e.begin() as c:
        if "INT UNSIGNED" in sql:c.execute(text("ALTER TABLE notice_confirmations DROP FOREIGN KEY fk_notice_confirmations_notice"))
        c.execute(text(sql))
    with e.connect() as c: before=c.execute(text("SHOW CREATE TABLE notice_confirmations")).one()[1]
    with app.app_context(),pytest.raises(RuntimeError,match="불일치"):prepare_popup_schema(e)
    with e.connect() as c: assert c.execute(text("SHOW CREATE TABLE notice_confirmations")).one()[1]==before


def test_existing_confirmation_fk_still_cascades_when_popup_off(physical):
    app,e,cfg=physical;raw008(e);uid,nid=rows(app)
    off=create_app(cfg|{"ENABLE_NOTICE_POPUP_DRAFT":False})
    assert "notice_popups" not in off.blueprints
    try:
        with off.app_context(), transaction() as db:
            # Exercise the same base deletion helper through a real admin request below.
            u=db.get(User,uid);u.role="admin"
        c=off.test_client()
        login=c.post("/api/v1/auth/login",json={"login_id":"staff1","password":"isolated-popup-test"})
        h={"Authorization":"Bearer "+login.json["data"]["access_token"]}
        assert c.post(f"/api/v1/notices/{nid}/delete",headers=h,json={}).status_code==200
        assert c.post(f"/api/v1/notices/{nid}/delete",headers=h,json={}).status_code==404
        with e.connect() as conn:
            assert conn.execute(text("SELECT COUNT(*) FROM notice_confirmations")).scalar()==0
            assert conn.execute(text("SELECT COUNT(*) FROM audit_events WHERE action='notice.deleted'")).scalar()==1
    finally:off.extensions["db_engine"].dispose()
    # Deletion matches the latest local user choice; shared rollout still needs approval.


def test_concurrent_explicit_initialization_is_serialized(physical):
    app,e,cfg=physical
    def prepare(_):
        other=create_app(cfg)
        try:
            with other.app_context():prepare_popup_schema(other.extensions["db_engine"])
        finally:other.extensions["db_engine"].dispose()
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(prepare,range(2)))
    with app.app_context():prepare_popup_schema(e)
    assert inspect(e).has_table("notice_confirmations")


def test_explicit_current_database_foreign_keys_are_allowed(physical):
    app,e,_=physical
    database=e.url.database
    sql=(Path(__file__).resolve().parents[1]/"migrations/008_notice_popups.mysql.sql").read_text(encoding="utf-8")
    sql=sql.replace("REFERENCES notices",f"REFERENCES `{database}`.notices").replace("REFERENCES users",f"REFERENCES `{database}`.users")
    with e.begin() as c:c.exec_driver_sql(sql)
    with app.app_context():prepare_popup_schema(e)


@pytest.mark.parametrize("column,parent",[("user_id","users"),("notice_id","notices")])
def test_foreign_database_parent_is_rejected_without_ddl_or_row_changes(physical,column,parent):
    app,e,_=physical;raw008(e);uid,nid=rows(app)
    other="remote_work_test_popup_foreign_"+uuid.uuid4().hex
    quote=e.dialect.identifier_preparer.quote_identifier
    with e.begin() as c:c.exec_driver_sql(f"CREATE DATABASE {quote(other)} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")
    try:
        value=uid if column=="user_id" else nid
        with e.begin() as c:
            c.exec_driver_sql(f"CREATE TABLE {quote(other)}.{parent} (id INTEGER NOT NULL PRIMARY KEY) ENGINE=InnoDB")
            c.execute(text(f"INSERT INTO {quote(other)}.{parent} (id) VALUES (:id)"),{"id":value})
            fk=next(f for f in inspect(c).get_foreign_keys("notice_confirmations") if f["constrained_columns"]==[column])
            action="RESTRICT" if column=="user_id" else "CASCADE"
            c.exec_driver_sql(f"ALTER TABLE notice_confirmations DROP FOREIGN KEY {quote(fk['name'])}, ADD CONSTRAINT foreign_parent_check FOREIGN KEY ({column}) REFERENCES {quote(other)}.{parent}(id) ON DELETE {action}")
        assert next(f for f in inspect(e).get_foreign_keys("notice_confirmations") if f["constrained_columns"]==[column])["referred_schema"]==other
        with e.connect() as c:
            before=c.exec_driver_sql("SHOW CREATE TABLE notice_confirmations").one()[1]
            saved=c.exec_driver_sql("SELECT * FROM notice_confirmations").all()
        with app.app_context(),pytest.raises(RuntimeError,match="FK schema"):
            prepare_popup_schema(e)
        with e.connect() as c:
            assert c.exec_driver_sql("SHOW CREATE TABLE notice_confirmations").one()[1]==before
            assert c.exec_driver_sql("SELECT * FROM notice_confirmations").all()==saved
    finally:
        # Only this test's child table and UUID-owned temporary database.
        NoticeConfirmation.__table__.drop(e,checkfirst=True)
        with e.begin() as c:c.exec_driver_sql(f"DROP DATABASE {quote(other)}")
