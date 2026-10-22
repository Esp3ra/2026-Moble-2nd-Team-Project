"""Owned MySQL + actual Flask/auth/W1/journal HTTP handlers, no shared DB."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path

import pytest
from sqlalchemy import select

from remotework import create_app, deadline_http as api, deadline_events as events, deadline_bindings as bindings
from remotework.features.tasks.models import TaskAssignment
from remotework.write_requests import WriteReceipt
from test_task_deadline_facts import deadline_app, deadline, guarded, headers, add_task
from test_deadline_events import create, change, task
REAL_CLOCK=api.server_utc


@pytest.fixture(scope='module')
def http_app(deadline_app):
    root=Path(__file__).resolve().parents[1]/'migrations/drafts'
    with deadline_app.extensions['db_engine'].begin() as db:
        for name in ('deadline_events.mysql.sql','deadline_confirmations.mysql.sql'):
            sql='\n'.join(l for l in (root/name).read_text().splitlines() if not l.lstrip().startswith('--'))
            for statement in sql.split(';'):
                if statement.strip(): db.exec_driver_sql(statement)
    deadline_app.config['ENABLE_COMMON_WRITE_DRAFT']=True
    deadline_app.config['ENABLE_TASK_QUERY_DRAFT']=True
    from remotework.write_requests import bp as write_bp
    deadline_app.register_blueprint(write_bp)
    api.install(deadline_app,writes=True)
    return deadline_app


@pytest.fixture
def env(http_app,deadline,monkeypatch):
    assert deadline.app is http_app
    monkeypatch.setattr(api,'server_utc',lambda db:datetime(2026,10,22,1,2,3,456789))
    yield deadline
    with http_app.extensions['db_engine'].begin() as db:
        db.execute(api.confirmations.delete());db.execute(events.heads.delete());db.execute(events.journal.delete())


def call(env,path='',method='get',body=None,extra=None,who='staff1'):
    return getattr(env.app.test_client(),method)(path if path.startswith('/api/') else '/api/v1/deadline-alerts'+path,
        json=body,headers={**headers(env,who),**(extra or {})})


def operation(env):
    response=call(env)
    assert response.status_code==200,response.json
    item=response.json['data']['items'][0]
    response=call(env,'/api/v1/write-requests/keys','post',{'action':api.ACTION,'target':item['target']})
    assert response.status_code==201,response.json
    return item, response.json['data']['request_key'],dict(task_id=item['task_id'],
        expected_row_version=item['row_version'],expected_assignment_revision=item['assignment_revision'])


def confirm(env,item,key,body):
    return call(env,'/'+item['target']+'/confirm','post',body,{'Idempotency-Key':key})


def resolve(env,item,key,who='staff1'):
    return call(env,'/api/v1/write-requests/result','post',{'action':api.ACTION,'target':item['target']},
                {'Idempotency-Key':key},who=who)


def test_snapshot_confirm_relogin_result_preserves_task_version(env):
    create(env)
    item,key,body=operation(env)
    response=confirm(env,item,key,body)
    assert response.status_code==200,response.json
    receipt=response.json['data']
    assert set(receipt)=={'task_id','event_id','assignment_revision','confirmed_at'}
    assert receipt['assignment_revision']==1 and receipt['event_id']==item['event_id']
    assert receipt['confirmed_at'].startswith('2026-10-22T01:02:03')
    assert confirm(env,item,key,body).json['data']==receipt
    call(env,'/api/v1/auth/logout','post',{})
    env.tokens.clear()
    assert resolve(env,item,key).json['data']['result']==receipt
    current=call(env).json['data']['items'][0]
    assert current['confirmed_at']==receipt['confirmed_at'] and current['row_version']==body['expected_row_version']


def test_unrecorded_original_binding_stays_resolvable_after_new_event(env):
    create(env);item,key,body=operation(env)
    with guarded(env) as db: change(env,db)
    assert resolve(env,item,key).json['data']=={'state':'not_recorded'}
    assert confirm(env,item,key,body).status_code==409


def test_success_receipt_survives_new_event_but_new_snapshot_unconfirmed(env):
    create(env);item,key,body=operation(env)
    saved=confirm(env,item,key,body).json['data']
    with guarded(env) as db:change(env,db)
    assert resolve(env,item,key).json['data']['result']==saved
    snapshot=call(env,'/'+item['target']).json['data']
    assert snapshot['items'][0]['event_id']!=item['event_id']
    assert snapshot['items'][0]['confirmed_at'] is None


def test_same_user_assignment_return_has_two_rows_one_event(env):
    create(env);item,key,body=operation(env)
    assert confirm(env,item,key,body).status_code==200
    with guarded(env) as db:
        previous=db.get(TaskAssignment,(env.task_id,1))
        for revision,who in ((2,'staff2'),(3,'staff1')):
            db.add(TaskAssignment(task_id=env.task_id,revision=revision,assignee_id=env.ids[who],
                assigned_by=env.ids['admin1'],assigned_at=previous.assigned_at))
        db.flush();row=task(db,env);row.current_assignment_revision=3;row.row_version=3
    new,newkey,newbody=operation(env)
    assert new['event_id']==item['event_id'] and new['target']!=item['target'] and new['confirmed_at'] is None
    assert confirm(env,new,newkey,newbody).status_code==200
    assert resolve(env,item,key).json['data']['result']['assignment_revision']==1
    with env.app.extensions['db_engine'].connect() as db:
        assert len(list(db.execute(select(api.confirmations))))==2


def test_current_own_revocation_denies_before_result_replay(env):
    create(env);item,key,body=operation(env)
    assert confirm(env,item,key,body).status_code==200
    with guarded(env) as db:db.get(TaskAssignment,(env.task_id,1)).assignee_id=env.ids['staff2']
    assert resolve(env,item,key).status_code==403


def test_audit_failure_rolls_back_confirmation_and_w1(env,monkeypatch):
    create(env);item,key,body=operation(env)
    def broken(*args,**kwargs):raise RuntimeError('test audit rollback')
    monkeypatch.setattr(api,'audit',broken)
    # TESTING propagates unexpected exceptions; production handler maps to500.
    response=confirm(env,item,key,body)
    assert response.status_code==500
    assert resolve(env,item,key).json['data']=={'state':'not_recorded'}
    with env.app.extensions['db_engine'].connect() as db:
        assert list(db.execute(select(api.confirmations)))==[]
        assert list(db.execute(select(WriteReceipt)))==[]


def test_parallel_same_binding_is_single_confirmation(env):
    create(env);item,key,body=operation(env)
    with ThreadPoolExecutor(2) as pool:
        answers=list(pool.map(lambda _:confirm(env,item,key,body),range(2)))
    assert [r.status_code for r in answers]==[200,200]
    assert answers[0].json['data']==answers[1].json['data']
    with env.app.extensions['db_engine'].connect() as db:
        assert len(list(db.execute(select(api.confirmations))))==1


def test_unknown_existing_task_is503_no_baseline(env):
    env.task_id=add_task(env)
    response=call(env)
    assert response.status_code==503 and response.json['error']['code']=='DEADLINE_EVENT_UNKNOWN'
    with env.app.extensions['db_engine'].connect() as db:assert list(db.execute(select(events.journal)))==[]


def test_other_unknown_task_does_not_block_exact_original_result(env):
    create(env);item,key,body=operation(env)
    add_task(env,task_id=101)
    assert call(env).status_code==503
    assert resolve(env,item,key).json['data']=={'state':'not_recorded'}


@pytest.mark.parametrize('update',[
    {'task_id':100},{'expected_row_version':1},{'expected_assignment_revision':'1'},
    {'expected_assignment_revision':True},{'task_id':'101'},{'extra':1}])
def test_invalid_wire_rejected_before_write(env,update):
    create(env);item,key,body=operation(env);body.update(update)
    assert confirm(env,item,key,body).status_code==400


def test_duplicate_json_keys_are_rejected(env):
    create(env);item,key,_=operation(env)
    response=env.app.test_client().post('/api/v1/deadline-alerts/'+item['target']+'/confirm',
        headers={**headers(env),'Idempotency-Key':key},content_type='application/json',
        data='{"task_id":"100","task_id":"100","expected_row_version":"1","expected_assignment_revision":1}')
    assert response.status_code==400


def test_not_yet_occurred_is409(env,monkeypatch):
    create(env);item,key,body=operation(env)
    monkeypatch.setattr(api,'server_utc',lambda db:datetime(2026,10,21,23,59,59))
    response=confirm(env,item,key,body)
    assert response.status_code==409 and response.json['error']['code']=='DEADLINE_NOT_OCCURRED'


def test_off_application_reads_existing_receipts_but_has_no_routes_or_keys(env):
    create(env);item,key,body=operation(env)
    saved=confirm(env,item,key,body).json['data']
    app=create_app({**env.app.config,'ENABLE_DEADLINE_ALERTS_DRAFT':False,'ENABLE_DEADLINE_RESULT_READS_DRAFT':True})
    client=app.test_client()
    try:
        auth=headers(env)
        assert client.get('/api/v1/deadline-alerts',headers=auth).status_code==404
        response=client.post('/api/v1/write-requests/result',headers={**auth,'Idempotency-Key':key},
            json={'action':api.ACTION,'target':item['target']})
        assert response.status_code==200,response.json
        assert response.json['data']['result']==saved
        assert client.post('/api/v1/write-requests/keys',headers=auth,
            json={'action':api.ACTION,'target':item['target']}).status_code==503
    finally:app.extensions['db_engine'].dispose()


def test_confirmation_storage_damage_returns503_not_empty_success(env):
    create(env)
    engine=env.app.extensions['db_engine']
    with engine.begin() as db:db.exec_driver_sql('RENAME TABLE deadline_binding_confirmations_draft TO deadline_binding_confirmations_missing')
    try:
        response=call(env)
        assert response.status_code==503 and response.json['error']['code']=='DEADLINE_CONFIRMATION_STORAGE_REQUIRED'
    finally:
        with engine.begin() as db:db.exec_driver_sql('RENAME TABLE deadline_binding_confirmations_missing TO deadline_binding_confirmations_draft')


def test_real_tcp_http_snapshot_confirmation_and_result(env):
    import json
    from threading import Thread
    from urllib.request import Request, urlopen
    from werkzeug.serving import make_server
    create(env)
    item,key,body=operation(env)
    server=make_server('127.0.0.1',0,env.app,threaded=True)
    worker=Thread(target=server.serve_forever,daemon=True)
    worker.start()
    def http(path,body=None,key=None):
        auth={**headers(env),'Content-Type':'application/json'}
        if key:auth['Idempotency-Key']=key
        request=Request('http://127.0.0.1:'+str(server.server_port)+path,
            data=json.dumps(body).encode() if body is not None else None,headers=auth)
        with urlopen(request,timeout=8) as response:
            assert response.status==200
            return json.load(response)['data']
    try:
        assert http('/api/v1/deadline-alerts')['items'][0]['target']==item['target']
        receipt=http('/api/v1/deadline-alerts/'+item['target']+'/confirm',body,key)
        assert http('/api/v1/write-requests/result',{'action':api.ACTION,'target':item['target']},key)['result']==receipt
    finally:
        server.shutdown();worker.join(timeout=8);server.server_close()
    assert not worker.is_alive()


def test_actual_mysql_utc_is_returned_in_same_authenticated_transaction(env,monkeypatch):
    monkeypatch.setattr(api,'server_utc',REAL_CLOCK)
    create(env)
    before=datetime.now(timezone.utc).replace(tzinfo=None)
    response=call(env)
    assert response.status_code==200,response.json
    value=datetime.fromisoformat(response.json['data']['server_time'].replace('Z','+00:00')).replace(tzinfo=None)
    assert before-timedelta(seconds=1)<=value<=datetime.now(timezone.utc).replace(tzinfo=None)+timedelta(seconds=1)
