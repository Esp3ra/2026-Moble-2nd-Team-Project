"""Cookie chat + unchanged desktop API against a disposable native MySQL only."""
import json
import os
import re
import uuid

import pytest
from sqlalchemy import func, select, text

from remotework import create_app
from remotework.auth import create_user
from remotework.db import transaction
from remotework.models import AuditEvent, User


@pytest.fixture(autouse=True)
def mysql_chat(monkeypatch):
    assert os.environ.get('MYSQL_TEST_URL', '').startswith('mysql'), 'Native disposable MySQL required'
    monkeypatch.setenv('REMOTEWORK_ENABLE_CHAT_DRAFT', '1')


def open_page(client, login_id='staff1', password='Staff-test-2026!'):
    page=client.get('/login').get_data(as_text=True)
    csrf=re.search(r'name="csrf_token" value="([^"]+)"', page)[1]
    result=client.post('/login',data={'csrf_token':csrf,'login_id':login_id,'password':password})
    assert result.status_code==302
    page=client.get('/chat').get_data(as_text=True)
    return {'X-Chat-Session':re.search(r'data-session="([^"]+)"',page)[1]},re.search(r'data-csrf="([^"]+)"',page)[1]


def post(client,headers,csrf,path,body):
    return client.post(path,headers=headers,data={'csrf_token':csrf,'payload':json.dumps(body,ensure_ascii=False)})


def new_room(client,headers,csrf,**extra):
    user=client.get('/chat/data/participants',headers=headers).json['data']['items'][0]
    body={'room_id':str(uuid.uuid4()),'title':'웹 채팅','is_group':False,'participant_ids':[user['id']],**extra}
    result=post(client,headers,csrf,'/chat/data/rooms',body)
    assert result.status_code==201,result.json
    return body


def test_web_menu_and_client_layout(client):
    headers,csrf=open_page(client)
    assert client.get('/features/chat').location.endswith('/chat')
    page=client.get('/chat').get_data(as_text=True)
    for value in ['chat-rooms','chat-members','chat-messages','chat-content','chat-dialog','chat-filter','chat.js','chat.css']:
        assert value in page
    assert '기능 연결 준비 중' not in page
    assert client.get('/chat/data/rooms',headers=headers).json['data']=={'items':[],'next_cursor':None}


def test_missing_login_json_and_page_redirect(client):
    assert client.get('/chat').status_code==302
    r=client.get('/chat/data/rooms');assert r.status_code==401 and r.json['error']['code']=='AUTH_REQUIRED'


def test_chat_off_does_not_install_web_or_api(app):
    config=dict(app.config);config['ENABLE_CHAT_DRAFT']=False
    other=create_app(config)
    try:
        rules={str(r) for r in other.url_map.iter_rules()}
        assert '/chat' not in rules and '/api/v1/chat/rooms' not in rules
    finally:other.extensions['db_engine'].dispose()


def test_cookie_is_not_bearer_and_bearer_is_not_web(client,login):
    bearer,_=login();headers,csrf=open_page(client)
    assert client.get('/api/v1/chat/rooms').status_code==401
    independent=client.application.test_client()
    assert independent.get('/chat/data/rooms',headers=bearer).status_code==401


@pytest.mark.parametrize('path',['/chat/data/rooms','/chat/data/participants'])
def test_page_session_proof_required_even_for_reads(client,path):
    headers,csrf=open_page(client)
    r=client.get(path);assert r.status_code==403 and r.json['error']['code']=='CHAT_SESSION_CHANGED'


def test_same_account_relogin_rejects_old_page_before_write(client):
    old,oldcsrf=open_page(client);new,newcsrf=open_page(client)
    assert old!=new
    r=post(client,old,newcsrf,'/chat/data/rooms',{})
    assert r.status_code==403 and r.json['error']['code']=='CHAT_SESSION_CHANGED'
    assert client.get('/chat/data/rooms',headers=new).json['data']['items']==[]


@pytest.mark.parametrize('csrf_mode',['missing','bad','json'])
def test_csrf_failure_never_creates_room(client,csrf_mode):
    headers,csrf=open_page(client)
    body={'room_id':str(uuid.uuid4()),'title':'no','is_group':False,'participant_ids':[1]}
    if csrf_mode=='json':r=client.post('/chat/data/rooms',headers=headers,json=body)
    else:r=client.post('/chat/data/rooms',headers=headers,data={'payload':json.dumps(body),**({} if csrf_mode=='missing' else {'csrf_token':'wrong'})})
    assert r.status_code==400 and r.json['error']['code']=='CSRF_FAILED'
    assert r.json['request_id']
    assert client.get('/chat/data/rooms',headers=headers).json['data']['items']==[]


def test_same_room_uuid_replay_conflict_and_shared_audit(app,client):
    headers,csrf=open_page(client);body=new_room(client,headers,csrf)
    assert post(client,headers,csrf,'/chat/data/rooms',body).status_code==200
    assert post(client,headers,csrf,'/chat/data/rooms',{**body,'title':'changed'}).status_code==409
    with app.app_context(),transaction() as db:
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='chat.room_created'))==1


@pytest.mark.parametrize('content,expected',[('😀'*2000,201),('😀'*2001,400),('가😀'*1000,201),('hello\nworld',201),('   ',400),('<img src=x onerror=alert(1)>',201)])
def test_web_message_unicode_validation_and_desktop_visibility(client,content,expected):
    headers,csrf=open_page(client);body=new_room(client,headers,csrf)
    url='/chat/data/rooms/'+body['room_id']+'/messages'
    result=post(client,headers,csrf,url,{'message_id':str(uuid.uuid4()),'content':content})
    assert result.status_code==expected,result.json
    api=client.post('/api/v1/auth/login',json={'login_id':'admin1','password':'Admin-test-2026!'}).json['data']
    rows=client.get('/api/v1/chat/rooms/'+body['room_id']+'/messages',headers={'Authorization':'Bearer '+api['access_token']}).json['data']['items']
    assert len(rows)==(1 if expected==201 else 0)
    if expected==201:assert rows[0]['content']==content.strip()


def test_web_and_desktop_bidirectional_idempotent_messages(app,client):
    headers,csrf=open_page(client);room=new_room(client,headers,csrf)['room_id'];path=f'/chat/data/rooms/{room}/messages'
    message={'message_id':str(uuid.uuid4()),'content':'웹 메시지'}
    assert post(client,headers,csrf,path,message).status_code==201
    assert post(client,headers,csrf,path,message).status_code==200
    assert post(client,headers,csrf,path,{**message,'content':'changed'}).status_code==409
    api=client.post('/api/v1/auth/login',json={'login_id':'admin1','password':'Admin-test-2026!'}).json['data'];bearer={'Authorization':'Bearer '+api['access_token']}
    assert client.post(f'/api/v1/chat/rooms/{room}/messages',headers=bearer,json={'message_id':str(uuid.uuid4()),'content':'클라이언트 답장'}).status_code==201
    rows=client.get(path,headers=headers).json['data']['items'];assert [x['content'] for x in rows]==['웹 메시지','클라이언트 답장']
    with app.app_context(),transaction() as db:
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='chat.message_sent'))==2


def test_personal_hide_keeps_others_and_survives_new_app(app,client):
    headers,csrf=open_page(client);room=new_room(client,headers,csrf)['room_id']
    message={'message_id':str(uuid.uuid4()),'content':'다시 로그인해도 보존'}
    assert post(client,headers,csrf,f'/chat/data/rooms/{room}/messages',message).status_code==201
    for _ in range(2):assert post(client,headers,csrf,f'/chat/data/rooms/{room}/hide',{}).status_code==200
    assert client.get('/chat/data/rooms',headers=headers).json['data']['items']==[]
    assert client.get(f'/chat/data/rooms/{room}/messages',headers=headers).status_code==404
    config=dict(app.config);other=create_app(config)
    try:
        fresh=other.test_client();otherheaders,othercsrf=open_page(fresh,'admin1','Admin-test-2026!')
        assert fresh.get('/chat/data/rooms',headers=otherheaders).json['data']['items'][0]['id']==room
        assert fresh.get(f'/chat/data/rooms/{room}/messages',headers=otherheaders).json['data']['items'][0]['content']==message['content']
    finally:other.extensions['db_engine'].dispose()


def test_nonmember_cannot_read_send_hide(app,client):
    headers,csrf=open_page(client);room=new_room(client,headers,csrf)['room_id']
    with app.app_context(),transaction() as db:create_user(db,{'login_id':'outside','password':'Outside-test-2026!','display_name':'outside','employee_no':'OUT','department':'개발팀','role':'admin'})
    outside=app.test_client();oh,oc=open_page(outside,'outside','Outside-test-2026!')
    assert outside.get(f'/chat/data/rooms/{room}/messages',headers=oh).status_code==404
    assert post(outside,oh,oc,f'/chat/data/rooms/{room}/messages',{'message_id':str(uuid.uuid4()),'content':'denied'}).status_code==404
    assert post(outside,oh,oc,f'/chat/data/rooms/{room}/hide',{}).status_code==404


def test_group_active_directory_and_no_self_or_private_fields(app,client):
    headers,csrf=open_page(client)
    with app.app_context(),transaction() as db:
        create_user(db,{'login_id':'design','password':'Design-test-2026!','display_name':'관리자','employee_no':'DUP','department':'디자인팀','role':'employee'})
        create_user(db,{'login_id':'inactive','password':'Inactive-test-2026!','display_name':'inactive','employee_no':'INACTIVE','department':'디자인팀','role':'employee'}).is_active=False
    people=client.get('/chat/data/participants',headers=headers).json['data']['items']
    assert len(people)==2 and all(set(p)=={'id','display_name','department'} for p in people)
    new_room(client,headers,csrf,is_group=True,participant_ids=[p['id'] for p in people])
    room=client.get('/chat/data/rooms',headers=headers).json['data']['items'][0]
    assert room['is_group'] and len(room['participants'])==3


def test_web_pagination_preserves_both_existing_cursor_contracts(app,client):
    from remotework.features.chat.models import ChatMessage
    headers,csrf=open_page(client);room=new_room(client,headers,csrf)['room_id']
    with app.app_context(),transaction() as db:
        user=db.scalar(select(User).where(User.login_id=='staff1'))
        db.add_all([ChatMessage(message_id=str(uuid.uuid4()),room_id=room,sender_id=user.id,sender_name=user.display_name,content=f'message-{i}') for i in range(101)])
    first=client.get(f'/chat/data/rooms/{room}/messages?limit=100',headers=headers).json['data']
    assert len(first['items'])==100 and first['next_cursor'].isdecimal()
    last=client.get(f'/chat/data/rooms/{room}/messages?after='+first['next_cursor'],headers=headers).json['data']
    assert len(last['items'])==1 and last['next_cursor'] is None
    for _ in range(2):new_room(client,headers,csrf)
    first=client.get('/chat/data/rooms?limit=2',headers=headers).json['data']
    assert len(first['items'])==2
    last=client.get('/chat/data/rooms?after='+first['next_cursor'],headers=headers).json['data']
    assert len(last['items'])==1 and not last['next_cursor']


def test_inactive_actor_stops_both_reads_and_writes(app,client):
    headers,csrf=open_page(client)
    with app.app_context(),transaction() as db:db.scalar(select(User).where(User.login_id=='staff1')).is_active=False
    assert client.get('/chat/data/participants',headers=headers).status_code==401
    assert post(client,headers,csrf,'/chat/data/rooms',{}).status_code==401


@pytest.mark.parametrize('extra',[{'unexpected':'x'},{'payload':'[]'},{'payload':'not-json'}])
def test_malformed_form_rejected(client,extra):
    headers,csrf=open_page(client)
    r=client.post('/chat/data/rooms',headers=headers,data={'csrf_token':csrf,'payload':'{}',**extra})
    assert r.status_code==400
