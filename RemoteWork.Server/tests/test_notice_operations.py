"""Native MySQL/raw017: durable notice operation, W1, cookie recovery."""
from datetime import timedelta
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import json
import os
import re
import random
import subprocess
import threading
import uuid

import pytest
from sqlalchemy import delete, func, inspect, select
from sqlalchemy.engine import make_url
from sqlalchemy.exc import SQLAlchemyError
from werkzeug.serving import make_server

from remotework import create_app
from remotework.auth import create_user
from remotework.db import transaction
from remotework.models import AdminGuard, AuditEvent, AuthSession, Base, LoginFailure, SchemaVersion, User
from remotework.features.notices.models import Notice
from remotework.features.notices import operations as service
from remotework.write_requests import WriteReceipt


@pytest.fixture(scope="module")
def notice_app():
    url=os.environ["MYSQL_TEST_URL"]
    assert make_url(url).database.startswith("remote_work_test_")
    app=create_app(dict(TESTING=False, SECRET_KEY="notice-owned-test-"+40*"x", DATABASE_URL=url,
        ENABLE_NOTICE_DRAFT=True, ENABLE_COMMON_WRITE_DRAFT=True))
    engine=app.extensions["db_engine"]
    assert engine.dialect.name=="mysql"
    assert not inspect(engine).has_table("notice_operations")
    Base.metadata.create_all(engine)
    app.extensions["notice_test_tables"] = set(inspect(engine).get_table_names())
    raw="\n".join(line for line in (Path(__file__).resolve().parents[1]/"migrations/017_notice_operations.mysql.sql").read_text().splitlines() if not line.lstrip().startswith("--"))
    with engine.begin() as db:
        for sql in raw.split(";"):
            if sql.strip():db.exec_driver_sql(sql)
    yield app
    engine.dispose()


@pytest.fixture
def env(notice_app):
    app=notice_app
    with app.app_context(),transaction() as db:
        db.execute(service.operations.delete())
        for table in reversed(Base.metadata.sorted_tables):
            if table.name in app.extensions["notice_test_tables"] and table.name not in ("admin_guard", "schema_versions"):
                db.execute(table.delete())
        if db.get(SchemaVersion,1) is None:db.add(SchemaVersion(version=1))
        if db.get(AdminGuard,1) is None:db.add(AdminGuard(id=1))
        people={}
        for name,role in (("boss","admin"),("other","admin"),("worker","employee")):
            user=create_user(db,dict(login_id=name,password="Synthetic-notice-2026!",display_name=name,
                employee_no=name,department="개발팀",role=role));people[name]=user.id
    tokens={}
    for name in people:
        result=app.test_client().post("/api/v1/auth/login",json=dict(login_id=name,password="Synthetic-notice-2026!"))
        assert result.status_code==200,result.json
        tokens[name]=result.json["data"]["access_token"]
    return dict(app=app,ids=people,tokens=tokens)


def call(env,path,method="get",body=None,who="boss",key=None):
    headers={"Authorization":"Bearer "+env["tokens"][who]}
    if key:headers["Idempotency-Key"]=key
    return getattr(env["app"].test_client(),method)("/api/v1/"+path,json=body,headers=headers)


def draft(**overrides):
    return dict(operation_id=str(uuid.uuid4()),title="원 공지",content="원 본문",target="전 직원",
        is_pinned=False,attachment_ids=[],**overrides)


def key(env,body,who="boss"):
    result=call(env,"write-requests/keys","post",dict(action=service.ACTION,target=body["operation_id"]),who=who)
    assert result.status_code==201,result.json
    return result.json["data"]["request_key"]


def send(env,body,key_value=None):
    token=key_value or key(env,body)
    result=call(env,"notices","post",body,key=token)
    return result,token


def counts(env):
    with env["app"].app_context(),transaction() as db:
        return (db.scalar(select(func.count()).select_from(Notice)),
            db.scalar(select(func.count()).select_from(service.operations)),
            db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action==service.ACTION)))


def test_original_and_same_key_new_key_one_effect(env):
    body=draft();first,token=send(env,body)
    assert first.status_code==201,first.json
    receipt=first.json["data"]
    assert set(receipt)==service.FIELDS
    assert receipt["operation_id"]==body["operation_id"]
    assert send(env,body,token)[0].json["data"]==receipt
    assert send(env,body)[0].json["data"]==receipt
    assert counts(env)==(1,1,1)
    found=call(env,"notices/operations/"+body["operation_id"])
    assert found.json["data"]["result"]==receipt
    assert "원 본문" not in json.dumps(found.json,ensure_ascii=False)


def test_new_key_and_same_key_body_conflict(env):
    body=draft();_,token=send(env,body)
    changed=body|dict(content="다른 내용")
    assert send(env,changed,token)[0].status_code==409
    assert send(env,changed)[0].status_code==409
    assert counts(env)==(1,1,1)


def test_deleted_original_never_recreated(env):
    body=draft();created,token=send(env,body);receipt=created.json["data"]
    deleted=call(env,f"notices/{receipt['notice_id']}/delete","post",{})
    assert deleted.status_code==200,deleted.json
    assert call(env,"notices/operations/"+body["operation_id"]).json["data"]["result"]==receipt
    assert send(env,body,token)[0].json["data"]==receipt
    assert send(env,body)[0].json["data"]==receipt
    assert counts(env)==(0,1,1)


def test_receipt_cleanup_keeps_operation(env):
    body=draft();original,_=send(env,body)
    with env["app"].app_context(),transaction() as db:db.execute(delete(WriteReceipt))
    assert send(env,body)[0].json["data"]==original.json["data"]
    assert counts(env)==(1,1,1)


def test_two_new_keys_concurrently_have_one_effect(env):
    body=draft();tokens=[key(env,body),key(env,body)]
    with ThreadPoolExecutor(max_workers=2) as pool:
        results=list(pool.map(lambda token:send(env,body,token)[0],tokens))
    assert [result.status_code for result in results]==[201,201]
    assert results[0].json["data"]==results[1].json["data"]
    assert counts(env)==(1,1,1)


def test_restart_reads_persisted_original_without_recreating(env):
    body=draft();created,_=send(env,body)
    other=create_app(dict(TESTING=False,SECRET_KEY=env["app"].config["SECRET_KEY"],
        DATABASE_URL=env["app"].config["DATABASE_URL"],ENABLE_NOTICE_DRAFT=True,ENABLE_COMMON_WRITE_DRAFT=True))
    try:
        r=other.test_client().get("/api/v1/notices/operations/"+body["operation_id"],
            headers={"Authorization":"Bearer "+env["tokens"]["boss"]})
        assert r.status_code==200,r.json
        assert r.json["data"]["result"]==created.json["data"]
    finally:other.extensions["db_engine"].dispose()
    assert counts(env)==(1,1,1)


def test_missing_017_fail_closed_without_runtime_ddl(env):
    body=draft();token=key(env,body);engine=env["app"].extensions["db_engine"]
    with engine.begin() as db:db.exec_driver_sql("RENAME TABLE notice_operations TO notice_operations_absent")
    try:
        r=send(env,body,token)[0]
        assert r.status_code==503 and r.json["error"]["code"]=="NOTICE_OPERATION_SCHEMA_REQUIRED"
        assert not inspect(engine).has_table("notice_operations")
    finally:
        with engine.begin() as db:db.exec_driver_sql("RENAME TABLE notice_operations_absent TO notice_operations")
    assert counts(env)==(0,0,0)


def test_expired_tombstone_blocks_even_live_common_receipt(env):
    body=draft();_,token=send(env,body)
    with env["app"].app_context(),transaction() as db:
        db.execute(service.operations.update().values(expires_at=service.now(db)-timedelta(seconds=1)))
    assert call(env,"notices/operations/"+body["operation_id"]).status_code==410
    assert send(env,body,token)[0].status_code==410
    assert counts(env)==(1,1,1)


def test_current_actor_scope_employee_denied_and_missing(env):
    body=draft();send(env,body)
    assert call(env,"notices/operations/"+body["operation_id"],who="other").json["data"]==dict(state="not_recorded")
    assert call(env,"notices/operations/"+body["operation_id"],who="worker").status_code==403
    assert call(env,"notices/operations/"+str(uuid.uuid4())).json["data"]==dict(state="not_recorded")
    with env["app"].app_context(),transaction() as db:
        user=db.get(User,env["ids"]["boss"]);user.role="employee"
    assert call(env,"notices/operations/"+body["operation_id"]).status_code==403


def test_missing_key_no_legacy_fallback(env):
    body=draft();result=call(env,"notices","post",body)
    assert result.status_code==400,result.json
    assert counts(env)==(0,0,0)


def test_raw_duplicate_fields_rejected(env):
    body=draft();token=key(env,body)
    encoded=json.dumps(body)[:-1]+',"title":"duplicate"}'
    result=env["app"].test_client().post("/api/v1/notices",data=encoded,content_type="application/json",
        headers={"Authorization":"Bearer "+env["tokens"]["boss"],"Idempotency-Key":token})
    assert result.status_code==400,result.json
    assert counts(env)==(0,0,0)


def test_audit_failure_rolls_back_notice_operation_and_receipt(env,monkeypatch):
    from remotework.features.notices import routes
    body=draft();token=key(env,body)
    def failure(*args,**kwargs):raise SQLAlchemyError("synthetic audit failure")
    monkeypatch.setattr(routes,"audit",failure)
    assert send(env,body,token)[0].status_code==503
    assert counts(env)==(0,0,0)


def web(env):
    client=env["app"].test_client();client.get("/login")
    with client.session_transaction() as session:csrf=session["csrf_token"]
    response=client.post("/login",data=dict(csrf_token=csrf,login_id="boss",password="Synthetic-notice-2026!"))
    assert response.status_code==302
    page=client.get("/notices/new");assert page.status_code==200,page.text
    def hidden(name):return re.search('name="'+name+'" value="([^"]*)"',page.text).group(1)
    return client,dict(csrf_token=hidden("csrf_token"),operation_context=hidden("operation_context"),
        attachment_session=hidden("attachment_session"),title="웹 원 공지",content="웹 원 본문",target="전 직원",attachment_ids="[]")


def test_web_original_same_request_success_and_replay(env):
    client,body=web(env)
    result=client.post("/notices/new",data=body)
    assert result.status_code==303,result.text
    result=client.post("/notices/new",data=body)
    assert result.status_code==303,result.text
    assert counts(env)==(1,1,1)


def test_web_unknown_check_then_explicit_retry_preserves_body(env,monkeypatch):
    from remotework.features.notices import web_operations
    client,body=web(env);real=web_operations.create_notice_record
    def fail(*args,**kwargs):raise SQLAlchemyError("synthetic response failure")
    monkeypatch.setattr(web_operations,"create_notice_record",fail)
    result=client.post("/notices/new",data=body)
    assert result.status_code==503 and "readonly" in result.text
    token=re.search('name="operation_context" value="([^"]*)"',result.text).group(1)
    body["operation_context"]=token;body["notice_action"]="check";body["content"]="changed after unknown"
    checked=client.post("/notices/new",data=body)
    assert checked.status_code==200 and "같은 요청 다시 등록" in checked.text
    assert "웹 원 본문" in checked.text and "changed after unknown" not in checked.text
    assert counts(env)==(0,0,0)
    body["operation_context"]=re.search('name="operation_context" value="([^"]*)"',checked.text).group(1)
    body["notice_action"]="publish";monkeypatch.setattr(web_operations,"create_notice_record",real)
    assert client.post("/notices/new",data=body).status_code==303
    assert counts(env)==(1,1,1)


def test_web_success_response_lost_explicit_lookup_no_resend(env,monkeypatch):
    from remotework.features.notices import web_operations
    client,body=web(env);real=web_operations.create_notice_record
    def lose(*args,**kwargs):real(*args,**kwargs);raise SQLAlchemyError("synthetic response loss after commit")
    monkeypatch.setattr(web_operations,"create_notice_record",lose)
    result=client.post("/notices/new",data=body);assert result.status_code==503
    body["operation_context"]=re.search('name="operation_context" value="([^"]*)"',result.text).group(1)
    body["notice_action"]="check"
    assert client.post("/notices/new",data=body).status_code==303
    assert counts(env)==(1,1,1)


def test_web_original_context_cannot_cross_login(env):
    client,body=web(env)
    client.post("/logout",data={"csrf_token":body["csrf_token"]})
    client.get("/login")
    with client.session_transaction() as session:csrf=session["csrf_token"]
    client.post("/login",data=dict(csrf_token=csrf,login_id="boss",password="Synthetic-notice-2026!"))
    with client.session_transaction() as session:body["csrf_token"]=session["csrf_token"]
    assert client.post("/notices/new",data=body).status_code==403
    assert counts(env)==(0,0,0)


def test_large_utf8_locked_multipart_recovery_stays_under_common_cap(env,monkeypatch):
    from remotework.features.notices import web_operations
    client,body=web(env);rng=random.Random(174)
    body["title"]="".join(chr(rng.randrange(0x4e00,0x9fff)) for _ in range(200))
    body["content"]="".join(chr(rng.randrange(0x4e00,0x9fff)) for _ in range(5000))
    body["attachment_ids"]=json.dumps([str(uuid.uuid4()) for _ in range(10)])
    def fail(*args,**kwargs):raise SQLAlchemyError("synthetic pre-commit transport failure")
    monkeypatch.setattr(web_operations,"create_notice_record",fail)
    response=client.post("/notices/new",data=body,content_type="multipart/form-data")
    assert response.status_code==503,response.text[:400]
    assert not re.search(r'id="title"\s+name=',response.text)
    assert not re.search(r'id="content"\s+name=',response.text)
    # A browser submits only named enabled controls; locked text is display only.
    fields={name:re.search('name="'+name+'" value="([^"]*)"',response.text).group(1)
        for name in ("csrf_token","attachment_session","operation_context","attachment_ids")}
    import html
    fields={name:html.unescape(value) for name,value in fields.items()}
    fields["notice_action"]="check"
    checked=client.post("/notices/new",data=fields,content_type="multipart/form-data")
    assert checked.status_code==200,checked.text[:400]
    assert "같은 요청 다시 등록" in checked.text
    assert counts(env)==(0,0,0)


def test_real_csharp_http_original_operations(env):
    dll=os.environ.get("NOTICE_CSHARP_DLL")
    if not dll:
        pytest.skip("NOTICE_CSHARP_DLL is required for the separately built real C# HTTP consumer")
    assert Path(dll).is_file()
    server=make_server("127.0.0.1",0,env["app"],threaded=True)
    thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
    try:
        result=subprocess.run(["dotnet",dll,"--operations-e2e",f"http://127.0.0.1:{server.server_port}"],
            capture_output=True,text=True,encoding="utf-8",timeout=90)
        assert result.returncode==0,result.stdout+result.stderr
        print(result.stdout)
    finally:
        server.shutdown();server.server_close();thread.join(timeout=5)
