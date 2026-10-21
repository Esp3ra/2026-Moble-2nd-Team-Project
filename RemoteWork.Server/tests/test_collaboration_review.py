"""PR57 regressions on a disposable MySQL database; no shared server writes."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from pathlib import Path
from threading import Event
import os
import subprocess
import time
import uuid

import pytest
from flask import request
from sqlalchemy import select, text, func, create_engine
from sqlalchemy.engine import make_url
from remotework import create_app
from remotework.db import get_db, transaction
from remotework.models import AdminGuard, AuditEvent, SchemaVersion, User
from remotework.write_requests import WriteReceipt
from remotework.features.tasks.models import TaskAssignment, TaskRecord
from remotework.features.tasks.lifecycle_models import TaskCancellation
from remotework.features.collaboration import install
from remotework.features.collaboration.task_bridge import current_task_context
from remotework.features.collaboration.models import CollaborationRequest as Row, CollaborationEvent
from test_collaboration_requests import app, headers, write, send, rows  # noqa: F401


@pytest.mark.parametrize("operation", ["context", "list", "create"])
def test_rr_snapshot_waiting_on_guard_sees_committed_cancellation(app, client, operation):
    ready = Event()
    @app.before_request
    def establish_old_snapshot():
        if request.headers.get("X-Review-Race") == "cancel":
            assert get_db().execute(text("SELECT task_id FROM task_cancellations WHERE task_id=1")).first() is None
            ready.set()
    a, b = headers(client), headers(client, 2)
    key = None
    if operation == "list":
        assert send(client, a)[0].status_code == 201
    if operation == "create":
        key = client.post("/api/v1/write-requests/keys", headers=a,
            json={"action": "collaboration.created", "target": "1"}).json["data"]["request_key"]
    engine = app.extensions["db_engine"]
    engine.update_execution_options(isolation_level="REPEATABLE READ")
    def invoke():
        with app.test_client() as other:
            if operation == "context":
                return other.get("/api/v1/collaboration-requests/task-context/1", headers=a | {"X-Review-Race": "cancel"})
            if operation == "list":
                return other.get("/api/v1/collaboration-requests?scope=received", headers=b | {"X-Review-Race": "cancel"})
            body = {"request_id": str(uuid.uuid4()), "task_id": "1", "recipient_id": 2,
                "title": "취소 경합", "request_text": "보호 TX 대기 재현", "desired_date": "2026-10-25",
                "expected_row_version": "2", "expected_assignment_revision": 1}
            return other.post("/api/v1/collaboration-requests", headers=a | {"X-Review-Race": "cancel", "Idempotency-Key": key}, json=body)
    with ThreadPoolExecutor(max_workers=1) as pool:
        with app.app_context(), transaction() as db:
            db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            future = pool.submit(invoke)
            assert ready.wait(timeout=10), "HTTP request did not establish its old RR snapshot"
            # Actual server-side guard wait, not a mocked response/lock function.
            deadline = time.monotonic() + 5
            while True:
                waiting = db.scalar(text("SELECT COUNT(*) FROM performance_schema.data_lock_waits w "
                    "JOIN performance_schema.data_locks l ON l.ENGINE_LOCK_ID=w.REQUESTING_ENGINE_LOCK_ID "
                    "WHERE l.OBJECT_SCHEMA=DATABASE() AND l.OBJECT_NAME='admin_guard'"))
                if waiting:
                    break
                assert time.monotonic() < deadline, "Expected an actual MySQL guard wait"
                time.sleep(.01)
            task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == 1).with_for_update())
            # Explicit owner-bound cancellation TX double, not member 2's HTTP/ACK.
            db.add(TaskCancellation(task_id=1, cancelled_by=1, reason="소유 DB 경합 검증"))
            task.row_version = 2
        result = future.result(timeout=20)
    if operation == "context":
        assert result.status_code == 403
    elif operation == "list":
        assert result.status_code == 200 and result.json["data"]["items"][0]["status"] == "ended"
    else:
        assert result.status_code == 409 and result.json["error"]["code"] == "COLLABORATION_CONTEXT_CHANGED"
        with app.app_context():
            assert get_db().scalar(select(func.count()).select_from(Row)) == 0


@pytest.mark.parametrize("accepted,decision,actor,state", [(False,"withdraw",1,"withdrawn"),
    (False,"decline",2,"declined"), (True,"withdraw",1,"withdrawn"),
    (True,"leave",2,"left"), (True,"complete",1,"completed")])
def test_current_party_can_end_when_other_is_inactive(app, client, accepted, decision, actor, state):
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    if accepted:
        assert write(client,b,"collaboration.accepted",identifier,f"/{identifier}/actions/accept",{"expected_row_version":"1"})[0].status_code == 200
    with app.app_context(), transaction() as db:
        db.get(User, 2 if actor == 1 else 1).is_active = False
    r, _ = write(client, a if actor == 1 else b, "collaboration."+state, identifier,
        f"/{identifier}/actions/{decision}", {"expected_row_version": "2" if accepted else "1"})
    assert r.status_code == 200, r.json
    with app.app_context():
        assert get_db().get(Row,identifier).status == state
        assert get_db().get(Row,identifier).ended_at is not None


def test_acceptance_still_requires_active_requester(app, client):
    a,b = headers(client),headers(client,2)
    _,_,body = send(client,a)
    with app.app_context(),transaction() as db:
        db.get(User,1).is_active=False
    identifier=body["request_id"]
    r,_=write(client,b,"collaboration.accepted",identifier,f"/{identifier}/actions/accept",{"expected_row_version":"1"})
    assert r.status_code==409 and r.json["error"]["code"]=="RECIPIENT_UNAVAILABLE"


def off_app(app):
    result=create_app(dict(app.config) | {"ENABLE_COLLABORATION_DRAFT":False})
    assert install(result,task_context=current_task_context) is False
    return result


def test_cold_off_result_read_only_current_access_and_ttl(app,client,monkeypatch):
    import remotework.write_requests as common
    clock=[int(time.time())]
    monkeypatch.setattr(common.time,"time",lambda:clock[0])
    a,b,c=headers(client),headers(client,2),headers(client,3)
    r,key,body=send(client,a)
    assert r.status_code==201
    identifier=body["request_id"]
    response,key2=write(client,b,"collaboration.accepted",identifier,f"/{identifier}/actions/accept",{"expected_row_version":"1"})
    assert response.status_code==200
    cold=off_app(app)
    with cold.test_client() as other:
        for who,action,target,request_key in [(a,"collaboration.created","1",key),(b,"collaboration.accepted",identifier,key2)]:
            scope={"action":action,"target":target}
            retained=other.post("/api/v1/write-requests/result",headers=who|{"Idempotency-Key":request_key},json=scope)
            assert retained.status_code==200 and retained.json["data"]["state"]=="completed"
            assert other.post("/api/v1/write-requests/keys",headers=who,json=scope).status_code==503
        assert other.post("/api/v1/collaboration-requests",headers=a|{"Idempotency-Key":key},json=body).status_code==404
        assert other.post("/api/v1/write-requests/result",headers=c|{"Idempotency-Key":key2},
            json={"action":"collaboration.accepted","target":identifier}).status_code==404
        clock[0]+=3601
        assert other.post("/api/v1/write-requests/result",headers=a|{"Idempotency-Key":key},
            json={"action":"collaboration.created","target":"1"}).status_code==410
        clock[0]-=3601
        with app.app_context(),transaction() as db:
            db.get(TaskAssignment,(1,1)).assignee_id=3
        assert other.post("/api/v1/write-requests/result",headers=a|{"Idempotency-Key":key},
            json={"action":"collaboration.created","target":"1"}).status_code==403
        with app.app_context(),transaction() as db:
            db.get(User,2).is_active=False
        assert other.post("/api/v1/write-requests/result",headers=b|{"Idempotency-Key":key2},
            json={"action":"collaboration.accepted","target":identifier}).status_code==401
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(Row))==1
        assert get_db().get(Row,identifier).status=="accepted"
        assert get_db().scalar(select(func.count()).select_from(WriteReceipt))==2
    cold.extensions["db_engine"].dispose()


@pytest.mark.parametrize("change",["update_cascade","delete_cascade","table_collation","default",
    "auto_increment","index","check_enforcement","engine"])
def test_wrong_physical_storage_503_without_repair(app,client,change):
    from sqlalchemy import inspect
    engine=app.extensions["db_engine"]
    a=headers(client)
    with engine.begin() as db:
        if change in ("update_cascade","delete_cascade"):
            fk=next(x for x in inspect(db).get_foreign_keys("collaboration_events") if x["constrained_columns"]==["request_id"])
            name=fk["name"]
            assert name.replace('_','').isalnum()
            db.exec_driver_sql(f"ALTER TABLE collaboration_events DROP FOREIGN KEY `{name}`")
            action="ON UPDATE CASCADE" if change=="update_cascade" else "ON DELETE CASCADE"
            db.exec_driver_sql("ALTER TABLE collaboration_events ADD CONSTRAINT review_changed_fk FOREIGN KEY(request_id) REFERENCES collaboration_requests(request_id) "+action)
        elif change=="table_collation": db.exec_driver_sql("ALTER TABLE collaboration_events DEFAULT COLLATE utf8mb4_general_ci")
        elif change=="default": db.exec_driver_sql("ALTER TABLE collaboration_requests ALTER COLUMN title SET DEFAULT 'fallback'")
        elif change=="auto_increment": db.exec_driver_sql("ALTER TABLE collaboration_events MODIFY event_id BIGINT NOT NULL")
        elif change=="index":
            db.exec_driver_sql("CREATE INDEX review_event_parent ON collaboration_events(request_id)")
            db.exec_driver_sql("DROP INDEX ix_collaboration_event_request ON collaboration_events")
        elif change=="check_enforcement": db.exec_driver_sql("ALTER TABLE collaboration_requests ALTER CHECK ck_collaboration_versions NOT ENFORCED")
        else:
            for fk in inspect(db).get_foreign_keys("collaboration_events"):
                assert fk["name"].replace('_','').isalnum()
                db.exec_driver_sql(f"ALTER TABLE collaboration_events DROP FOREIGN KEY `{fk['name']}`")
            db.exec_driver_sql("ALTER TABLE collaboration_events ENGINE=MyISAM")
        before=db.exec_driver_sql("SHOW CREATE TABLE "+("collaboration_requests" if change in ("default","check_enforcement") else "collaboration_events")).one()[1]
    r=client.get("/api/v1/collaboration-requests",headers=a)
    assert r.status_code==503 and r.json["error"]["code"]=="COLLABORATION_SCHEMA_REQUIRED"
    with engine.connect() as db:
        after=db.exec_driver_sql("SHOW CREATE TABLE "+("collaboration_requests" if change in ("default","check_enforcement") else "collaboration_events")).one()[1]
        assert before==after  # no automatic ALTER/repair


def test_history_is_parties_only_minimal_and_90_day_read(app,client):
    a,b,c=headers(client),headers(client,2),headers(client,3)
    _,_,body=send(client,a)
    identifier=body["request_id"]
    assert write(client,b,"collaboration.declined",identifier,f"/{identifier}/actions/decline",{"expected_row_version":"1"})[0].status_code==200
    for who in (a,b):
        r=client.get(f"/api/v1/collaboration-requests/{identifier}/history",headers=who)
        assert r.status_code==200
        events=r.json["data"]["items"]
        assert [event["status"] for event in events]==["pending","declined"]
        assert all(set(event)=={"event_id","status","reason","actor_id","occurred_at"} for event in events)
        assert all(isinstance(event["event_id"],str) for event in events)
    assert client.get(f"/api/v1/collaboration-requests/{identifier}/history",headers=c).status_code==404
    app.config["COLLABORATION_CLOCK"]=lambda:datetime(2027,2,1)
    assert client.get(f"/api/v1/collaboration-requests/{identifier}/history",headers=a).status_code==404
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(CollaborationEvent))==2


def test_012_only_creator_refuses_existing_and_uses_reserved_sql(app,client):
    from remotework.features.collaboration.models import FeatureBase
    from remotework.features.collaboration.storage import create_missing_storage
    engine=app.extensions["db_engine"]
    with pytest.raises(RuntimeError): create_missing_storage(engine,reviewed=True)
    FeatureBase.metadata.drop_all(engine)
    with pytest.raises(RuntimeError): create_missing_storage(engine)
    create_missing_storage(engine,reviewed=True)
    assert send(client,headers(client))[0].status_code==201
    with app.app_context(): assert list(get_db().scalars(select(SchemaVersion.version)))==[1]


def test_owned_mysql_dump_restores_requests_results_and_history(app,client,tmp_path):
    """Synthetic source DB -> new owned DB, never restores over an existing DB."""
    a,b=headers(client),headers(client,2)
    _,key,body=send(client,a)
    identifier=body["request_id"]
    assert write(client,b,"collaboration.accepted",identifier,f"/{identifier}/actions/accept",{"expected_row_version":"1"})[0].status_code==200
    url=make_url(app.config["DATABASE_URL"])
    assert url.database.startswith("remote_work_test_")
    binaries=Path(os.getenv("MYSQL_TEST_BIN", "C:/Program Files/MySQL/MySQL Server 8.2/bin"))
    assert (binaries/"mysqldump.exe").is_file() and (binaries/"mysql.exe").is_file()
    options=["--no-defaults","--host="+(url.host or "localhost"),"--port="+str(url.port or 3306),
        "--user="+(url.username or "root"),"--default-character-set=utf8mb4"]
    env=dict(os.environ)
    if url.password: env["MYSQL_PWD"]=url.password
    dump=subprocess.run([str(binaries/"mysqldump.exe"),*options,"--single-transaction","--no-tablespaces",
        "--set-gtid-purged=OFF","--column-statistics=0","--skip-comments",url.database],capture_output=True,env=env,check=True).stdout
    (tmp_path/"synthetic-owned.sql").write_bytes(dump)
    restored_name="remote_work_test_collab_restore_"+uuid.uuid4().hex[:16]
    engine=app.extensions["db_engine"]
    restored=None
    try:
        with engine.begin() as db: db.exec_driver_sql(f"CREATE DATABASE `{restored_name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")
        subprocess.run([str(binaries/"mysql.exe"),*options,restored_name],input=dump,capture_output=True,env=env,check=True)
        restored=create_app(dict(app.config)|{"DATABASE_URL":url.set(database=restored_name)})
        install(restored,task_context=current_task_context)
        from sqlalchemy import inspect
        for name in ("collaboration_requests","collaboration_events"):
            def columns(bind):
                return {c["name"]:(str(c["type"]),c["nullable"],c.get("default"),c.get("autoincrement")) for c in inspect(bind).get_columns(name)}
            assert columns(engine)==columns(restored.extensions["db_engine"])
        from remotework.features.collaboration.storage import require_storage
        with restored.app_context(): require_storage(get_db())
        with restored.test_client() as other:
            assert rows(other,a,"sent")[0]["status"]=="accepted"
            assert len(other.get(f"/api/v1/collaboration-requests/{identifier}/history",headers=b).json["data"]["items"])==2
            retained=other.post("/api/v1/write-requests/result",headers=a|{"Idempotency-Key":key},json={"action":"collaboration.created","target":"1"})
            assert retained.status_code==200 and retained.json["data"]["state"]=="completed"
        with restored.app_context():
            assert list(get_db().scalars(select(SchemaVersion.version)))==[1]
            assert get_db().get(TaskAssignment,(1,1)).assignee_id==1
    finally:
        if restored is not None: restored.extensions["db_engine"].dispose()
        with engine.begin() as db: db.exec_driver_sql(f"DROP DATABASE IF EXISTS `{restored_name}`")
