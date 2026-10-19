"""Chat PR21 regression checks. Only conftest-owned temporary/test databases."""
import json
import os
import subprocess
import sys
import uuid
from concurrent.futures import ThreadPoolExecutor

import pytest
from sqlalchemy import func, select
from remotework import create_app
from remotework.db import transaction
from remotework.models import AuditEvent, User

CHAT_TABLES = {"chat_rooms", "chat_members", "chat_messages"}


@pytest.fixture(autouse=True)
def chat_enabled(monkeypatch):
    monkeypatch.setenv("REMOTEWORK_ENABLE_CHAT_DRAFT", "1")


def child(code, env=None):
    result = subprocess.run([sys.executable, "-c", code], env=env or os.environ.copy(),
                            capture_output=True, text=True, encoding="utf-8", timeout=35)
    assert result.returncode == 0, result.stdout + result.stderr
    return json.loads(result.stdout)


@pytest.mark.parametrize("task,notice,chat", [(t,n,c) for t in (0,1) for n in (0,1) for c in (0,1)])
def test_cold_process_registration_and_mysql_mock_ddl_boundaries(task, notice, chat):
    env=os.environ.copy()
    for flag, value in (("TASK_QUERY",task),("NOTICE",notice),("CHAT",chat)):
        env["REMOTEWORK_ENABLE_"+flag+"_DRAFT"]=str(value)
    env["REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT"]="0"
    result=child('''
import json
from contextlib import contextmanager
from unittest.mock import patch
from sqlalchemy import create_mock_engine, inspect
from remotework import create_app, db
from remotework.models import Base
app=create_app({'TESTING':True,'SECRET_KEY':'boundary-test-'+ 'a'*40,'DATABASE_URL':'sqlite:///:memory:'})
sql=[]
mock=create_mock_engine('mysql+pymysql://',lambda statement,*a,**k:sql.append(str(statement.compile(dialect=mock.dialect))))
class Existing:
 def get(self,*a):return object()
@contextmanager
def transaction():yield Existing()
with app.app_context():
 real=app.extensions['db_engine']
 app.extensions['db_engine']=mock
 with patch.object(db,'transaction',transaction):db.init_schema()
 app.extensions['db_engine']=real
 db.init_schema()
 print(json.dumps({'ddl':sql,'metadata':list(Base.metadata.tables),'tables':inspect(real).get_table_names(),'routes':[str(r) for r in app.url_map.iter_rules()]}))
 real.dispose()
''',env)
    for name in CHAT_TABLES:
        assert (name in result["metadata"]) == bool(chat)
        assert (name in result["tables"]) == bool(chat)
        assert any(("CREATE TABLE "+name+" ") in sql.replace("\n", " ") for sql in result["ddl"]) == bool(chat)
    assert ("tasks" in result["tables"]) == bool(task)
    assert ("notices" in result["tables"]) == bool(notice)
    assert "/api/v1/users" in result["routes"]
    assert ("/api/v1/chat/rooms" in result["routes"]) == bool(chat)


def test_explicit_config_false_overrides_environment():
    result=child('''
import json
from remotework import create_app
from remotework.models import Base
app=create_app({'TESTING':True,'SECRET_KEY':'boundary-test-'+ 'a'*40,'DATABASE_URL':'sqlite:///:memory:','ENABLE_CHAT_DRAFT':False})
print(json.dumps({'tables':list(Base.metadata.tables)}))
app.extensions['db_engine'].dispose()
''')
    assert CHAT_TABLES.isdisjoint(result["tables"])


def test_explicit_cli_adds_only_three_tables_and_preserves_common(tmp_path):
    env=os.environ.copy();env["CHAT_BOUNDARY_DB"]="sqlite:///"+(tmp_path/"boundary.db").as_posix()
    result=child('''
import json,os
from sqlalchemy import inspect,select
from remotework import create_app
from remotework.db import init_schema,transaction
from remotework.auth import create_user
from remotework.models import User,SchemaVersion
config={'TESTING':True,'SECRET_KEY':'boundary-test-'+ 'a'*40,'DATABASE_URL':os.environ['CHAT_BOUNDARY_DB'],'ENABLE_CHAT_DRAFT':False,'ENABLE_TASK_QUERY_DRAFT':False,'ENABLE_NOTICE_DRAFT':False}
app=create_app(config)
with app.app_context():
 init_schema()
 with transaction() as db:create_user(db,{'login_id':'kept','password':'Synthetic-pass-2026!','display_name':'kept','employee_no':'T','department':'dev','role':'admin'})
 before=inspect(app.extensions['db_engine']).get_table_names()
app.extensions['db_engine'].dispose()
config['ENABLE_CHAT_DRAFT']=True
app=create_app(config)
runner=app.test_cli_runner()
first=runner.invoke(args=['chat','init-tables']);second=runner.invoke(args=['chat','init-tables'])
assert first.exit_code==second.exit_code==0,(first.output,second.output)
with app.app_context(),transaction() as db:
 print(json.dumps({'before':before,'after':inspect(app.extensions['db_engine']).get_table_names(),'users':db.scalars(select(User.login_id)).all(),'versions':db.scalars(select(SchemaVersion.version)).all()}))
app.extensions['db_engine'].dispose()
''',env)
    assert set(result["after"])-set(result["before"])==CHAT_TABLES
    assert result["users"]==["kept"] and result["versions"]==[1]


def make_room(client, login):
    staff, data=login()
    admin, admin_data=login("admin1","Admin-test-2026!")
    room_id=str(uuid.uuid4())
    body={"room_id":room_id,"title":"chat","is_group":False,"participant_ids":[admin_data["user"]["id"]]}
    result=client.post("/api/v1/chat/rooms",headers=staff,json=body)
    assert result.status_code==201,result.json
    return room_id,staff,admin,body


@pytest.mark.parametrize("unit,n",[(s,n) for s in ("a","가","😀") for n in (2000,2001)]+[("가😀",1000),("가😀",1001)])
def test_api_store_then_read_unicode_boundaries(client,login,unit,n):
    room,staff,admin,_=make_room(client,login)
    text=unit*n
    response=client.post(f"/api/v1/chat/rooms/{room}/messages",headers=staff,json={"message_id":str(uuid.uuid4()),"content":text})
    if len(text)>2000:
        assert response.status_code==400
    else:
        assert response.status_code==201,response.json
        rows=client.get(f"/api/v1/chat/rooms/{room}/messages",headers=admin).json["data"]["items"]
        assert rows[-1]["content"]==text


def test_hide_is_personal_repeated_and_survives_new_app(app,client,login):
    from remotework.features.chat.models import ChatMember,ChatMessage,ChatRoom
    room,staff,admin,_=make_room(client,login)
    message={"message_id":str(uuid.uuid4()),"content":"persisted"}
    assert client.post(f"/api/v1/chat/rooms/{room}/messages",headers=staff,json=message).status_code==201
    for _ in range(2):assert client.post(f"/api/v1/chat/rooms/{room}/hide",headers=staff,json={}).status_code==200
    config={key:app.config[key] for key in ("DATABASE_URL","SECRET_KEY","TESTING","SESSION_HOURS")}
    config["ENABLE_CHAT_DRAFT"]=True
    second=create_app(config)
    try:
        other=second.test_client()
        assert other.get("/api/v1/chat/rooms",headers=staff).json["data"]["items"]==[]
        assert other.get("/api/v1/chat/rooms",headers=admin).json["data"]["items"][0]["id"]==room
        assert other.get(f"/api/v1/chat/rooms/{room}/messages",headers=admin).json["data"]["items"][0]["content"]=="persisted"
        assert other.get(f"/api/v1/chat/rooms/{room}/messages",headers=staff).status_code==404
        assert other.post(f"/api/v1/chat/rooms/{room}/messages",headers=admin,json={"message_id":str(uuid.uuid4()),"content":"new"}).status_code==201
        assert other.get("/api/v1/chat/rooms",headers=staff).json["data"]["items"]==[]
    finally:second.extensions["db_engine"].dispose()
    with app.app_context(),transaction() as db:
        assert db.get(ChatRoom,room) is not None
        assert db.scalar(select(func.count()).select_from(ChatMember).where(ChatMember.room_id==room))==2
        assert db.scalar(select(func.count()).select_from(ChatMessage).where(ChatMessage.room_id==room))==2
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=="chat.room_hidden"))==1


def test_duplicate_requests_conflicts_permissions_and_logout(app,client,login):
    room,staff,admin,body=make_room(client,login)
    assert client.post("/api/v1/chat/rooms",headers=staff,json=body).status_code==200
    assert client.post("/api/v1/chat/rooms",headers=staff,json={**body,"title":"other"}).status_code==409
    message={"message_id":str(uuid.uuid4()),"content":"hello"}
    url=f"/api/v1/chat/rooms/{room}/messages"
    assert client.post(url,headers=staff,json=message).status_code==201
    assert client.post(url,headers=staff,json=message).status_code==200
    assert client.post(url,headers=admin,json=message).status_code==409
    assert client.post(url,headers=staff,json={**message,"content":"changed"}).status_code==409
    assert client.get(url).status_code==401
    with app.app_context(),transaction() as db:
        from remotework.auth import create_user
        create_user(db,{"login_id":"outsider","password":"Synthetic-pass-2026!","display_name":"outsider","employee_no":"OUT","department":"dev","role":"admin"})
    outsider,_=login("outsider","Synthetic-pass-2026!")
    assert client.get(url,headers=outsider).status_code==404
    assert client.post(url,headers=outsider,json={"message_id":str(uuid.uuid4()),"content":"no"}).status_code==404
    assert client.post(f"/api/v1/chat/rooms/{room}/hide",headers=outsider,json={}).status_code==404
    assert client.post("/api/v1/auth/logout",headers=staff,json={}).status_code==200
    assert client.get(url,headers=staff).status_code==401


def test_concurrent_same_message_has_one_row_and_audit(app,client,login):
    if app.extensions["db_engine"].dialect.name != "mysql":
        pytest.skip("MySQL REPEATABLE READ locking test; SQLite is only a demo smoke check")
    from remotework.features.chat.models import ChatMessage
    room,staff,_,_=make_room(client,login)
    message_id=str(uuid.uuid4())
    def send(_):
        with app.test_client() as caller:
            return caller.post(f"/api/v1/chat/rooms/{room}/messages",headers=staff,json={"message_id":message_id,"content":"same"}).status_code
    with ThreadPoolExecutor(max_workers=2) as pool:statuses=list(pool.map(send,range(2)))
    assert sorted(statuses)==[200,201],statuses
    with app.app_context(),transaction() as db:
        assert db.scalar(select(func.count()).select_from(ChatMessage).where(ChatMessage.message_id==message_id))==1
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=="chat.message_sent"))==1


def test_message_pagination_and_inactive_participant(app,client,login):
    from remotework.features.chat.models import ChatMessage
    room,staff,admin,body=make_room(client,login)
    with app.app_context(),transaction() as db:
        author=db.scalar(select(User).where(User.login_id=="staff1"))
        db.add_all([ChatMessage(message_id=str(uuid.uuid4()),room_id=room,sender_id=author.id,sender_name=author.display_name,content=str(i)) for i in range(101)])
    first=client.get(f"/api/v1/chat/rooms/{room}/messages",headers=admin).json["data"]
    second=client.get(f"/api/v1/chat/rooms/{room}/messages?after={first['next_cursor']}",headers=admin).json["data"]
    assert len(first["items"])==100 and len(second["items"])==1 and second["next_cursor"] is None
    with app.app_context(),transaction() as db:db.scalar(select(User).where(User.login_id=="admin1")).is_active=False
    response=client.post("/api/v1/chat/rooms",headers=staff,json={**body,"room_id":str(uuid.uuid4())})
    assert response.status_code==409,response.json
    assert response.json["error"]["code"]=="CHAT_PARTICIPANT_UNAVAILABLE"
