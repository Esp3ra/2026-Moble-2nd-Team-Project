"""MySQL-backed receipt atomicity, authorization-before-replay and signed expiry."""
from concurrent.futures import ThreadPoolExecutor
from threading import Barrier

from flask import g, request
import pytest
from sqlalchemy import delete, func, select

from remotework import create_app
from remotework.auth import authenticated_transaction, login_required
from remotework.common import AppError, audit, payload, success
from remotework.db import get_db, init_schema, transaction
from remotework.models import AuditEvent, SchemaVersion, User
from remotework.write_requests import WriteReceipt, WriteResult, issue_key, perform_once


@pytest.fixture
def writes(app):
    # Explicit synthetic test retention, not a decision about team/shared policy.
    app.config.update(ENABLE_COMMON_WRITE_DRAFT=True, WRITE_REQUEST_TTL_SECONDS=60)
    from remotework.write_requests import bp
    app.register_blueprint(bp)
    def allowed(db, target):
        return g.user.role == "employee" and target == "self"
    app.extensions['write_authorizers'] = {'task.accepted': allowed}
    app.config['PROBE_FAIL'] = False
    app.config['PROBE_RENDEZVOUS'] = None
    @app.post('/api/v1/_receipt_probe')
    @login_required
    def mutate():
        body = payload()
        rendezvous = app.config['PROBE_RENDEZVOUS']
        if rendezvous:
            rendezvous.wait(timeout=15)
        with authenticated_transaction() as db:
            def change(session):
                g.user.department = body['department']
                audit('test.once', 'user', g.user.id)
                if app.config['PROBE_FAIL']:
                    raise AppError(409, 'PROBE_CONFLICT', 'rollback')
                return WriteResult({'task_id': '1', 'row_version': '2'})
            result = perform_once(db, key=request.headers.get('Idempotency-Key'), action='task.accepted',
                target='self', body=body, authorize=allowed, mutate=change)
        return success(result.data, result.status)
    with app.app_context():
        WriteReceipt.__table__.create(app.extensions['db_engine'], checkfirst=True)
    return app


def new_key(client, headers):
    r=client.post('/api/v1/write-requests/keys', headers=headers, json={'action':'task.accepted','target':'self'})
    assert r.status_code==201, r.json
    return r.json['data']['request_key']


def test_receipt_repeat_different_body_result_and_permissions(writes, login):
    headers, _ = login()
    with writes.test_client() as client:
        key=new_key(client,headers)
        keyed=headers|{'Idempotency-Key':key}
        data={'department':'changed'}
        for _ in range(2):
            assert client.post('/api/v1/_receipt_probe',headers=keyed,json=data).status_code==200
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'different'}).status_code==409
        query={'action':'task.accepted','target':'self'}
        result=client.post('/api/v1/write-requests/result',headers=keyed,json=query)
        assert result.json['data']['state']=='completed'
        assert result.json['data']['http_status']==200
        assert result.json['data']['result']=={'task_id':'1','row_version':'2'}
        assert result.json['data']['expires_at'].endswith('Z')
        with writes.app_context():
            assert get_db().scalar(select(func.count()).select_from(WriteReceipt))==1
            assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='test.once'))==1
        # A current valid admin identity does not inherit the staff receipt.
        admin,_=login('admin1','Admin-test-2026!')
        assert client.post('/api/v1/write-requests/result',headers=admin|{'Idempotency-Key':key},json=query).status_code==403
        writes.extensions['write_authorizers']['task.accepted']=lambda db,target: False
        assert client.post('/api/v1/write-requests/result',headers=keyed,json=query).status_code==403


def test_receipt_rollback_and_retry_same_key(writes, login):
    headers, identity=login()
    with writes.test_client() as client:
        keyed=headers|{'Idempotency-Key':new_key(client,headers)}
        writes.config['PROBE_FAIL']=True
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'rollback'}).status_code==409
        with writes.app_context():
            assert get_db().get(User,identity['user']['id']).department=='개발팀'
            assert get_db().scalar(select(func.count()).select_from(WriteReceipt))==0
            assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='test.once'))==0
        writes.config['PROBE_FAIL']=False
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'rollback'}).status_code==200


def test_expired_signed_key_cannot_reexecute_after_receipt_cleanup(writes, login, monkeypatch):
    import remotework.write_requests as support
    clock=[1800000000]
    monkeypatch.setattr(support.time,'time',lambda:clock[0])
    headers,_=login()
    with writes.test_client() as client:
        key=new_key(client,headers)
        keyed=headers|{'Idempotency-Key':key}
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'once'}).status_code==200
        clock[0]+=61
        with writes.app_context(),transaction() as db:
            db.execute(delete(WriteReceipt))
        r=client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'again'})
        assert r.status_code==410 and r.json['error']['code']=='IDEMPOTENCY_KEY_EXPIRED'
        assert client.post('/api/v1/_receipt_probe',headers=headers|{'Idempotency-Key':key+'x'},json={}).status_code==400


def test_policy_and_authorizer_fail_closed(writes, login):
    headers,_=login()
    with writes.test_client() as client:
        writes.config['WRITE_REQUEST_TTL_SECONDS']=0
        r=client.post('/api/v1/write-requests/keys',headers=headers,json={'action':'task.accepted','target':'self'})
        assert r.status_code==503 and r.json['error']['code']=='WRITE_POLICY_REQUIRED'
        r=client.post('/api/v1/write-requests/keys',headers=headers,json={'action':'task.created','target':'new'})
        assert r.status_code==503 and r.json['error']['code']=='WRITE_ACTION_NOT_READY'


def test_success_result_retention_starts_at_success_not_key_issue(writes,login,monkeypatch):
    import remotework.write_requests as support
    clock=[1800000000]
    monkeypatch.setattr(support.time,'time',lambda:clock[0])
    headers,_=login()
    with writes.test_client() as client:
        key=new_key(client,headers)
        keyed=headers|{'Idempotency-Key':key}
        clock[0]+=50
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'late'}).status_code==200
        clock[0]+=11  # Issued key has expired, but success result has 49 seconds left.
        query={'action':'task.accepted','target':'self'}
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'again'}).status_code==410
        assert client.post('/api/v1/write-requests/result',headers=keyed,json=query).json['data']['state']=='completed'
        clock[0]+=50
        assert client.post('/api/v1/write-requests/result',headers=keyed,json=query).status_code==410


def test_mysql_two_concurrent_requests_and_lost_response_have_one_effect(writes, login):
    if writes.extensions['db_engine'].dialect.name!='mysql':
        pytest.skip('MySQL current locks required')
    headers,_=login()
    with writes.test_client() as client:
        key=new_key(client,headers)
    keyed=headers|{'Idempotency-Key':key}
    writes.config['PROBE_RENDEZVOUS']=Barrier(2)
    def send():
        with writes.test_client() as client:
            r=client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'concurrent'})
            return r.status_code,r.json['data']
    with ThreadPoolExecutor(max_workers=2) as pool:
        futures=[pool.submit(send) for _ in range(2)]
        results=[future.result(timeout=25) for future in futures]
    writes.config['PROBE_RENDEZVOUS']=None
    assert results==[(200,{'task_id':'1','row_version':'2'})]*2
    with writes.app_context():
        assert get_db().scalar(select(func.count()).select_from(WriteReceipt))==1
        assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='test.once'))==1
    # Ignore one response as if lost: later query observes the committed result.
    with writes.test_client() as client:
        r=client.post('/api/v1/write-requests/result',headers=keyed,json={'action':'task.accepted','target':'self'})
        assert r.json['data']['state']=='completed'


def test_raw004_matches_columns_and_preserves_common_schema(writes):
    from pathlib import Path
    from sqlalchemy import inspect
    engine=writes.extensions['db_engine']
    if engine.dialect.name!='mysql':
        pytest.skip('Actual MySQL raw DDL verification')
    inspector=inspect(engine)
    before={col['name']:(str(col['type']),col['nullable']) for col in inspector.get_columns('write_receipts')}
    with writes.app_context():
        versions=get_db().scalars(select(SchemaVersion.version)).all()
        user_count=get_db().scalar(select(func.count()).select_from(User))
    sql=(Path(__file__).resolve().parents[1]/'migrations/004_write_receipts.mysql.sql').read_text(encoding='utf-8')
    sql='\n'.join(line for line in sql.splitlines() if not line.startswith('--')).strip().rstrip(';')
    # Only the dedicated conftest remote_work_test_ DB's newly empty support table.
    WriteReceipt.__table__.drop(engine)
    with engine.begin() as conn:
        conn.exec_driver_sql(sql)
    after={col['name']:(str(col['type']),col['nullable']) for col in inspect(engine).get_columns('write_receipts')}
    assert before==after
    with writes.app_context():
        assert get_db().scalars(select(SchemaVersion.version)).all()==versions==[1]
        assert get_db().scalar(select(func.count()).select_from(User))==user_count
    assert inspect(engine).get_foreign_keys('write_receipts')[0]['referred_table']=='users'


def test_fresh_process_write_support_is_independent_and_default_off():
    import os, subprocess, sys, json
    code="""
import json,os
from remotework import create_app
from remotework.models import Base
app=create_app({'TESTING':True,'SECRET_KEY':'test-'+'a'*40,'DATABASE_URL':'mysql+pymysql://unused@127.0.0.1/unused'})
print(json.dumps({'tables':sorted(Base.metadata.tables),'routes':[str(r) for r in app.url_map.iter_rules() if '/write-requests' in str(r)]}))
"""
    for enabled in (False,True):
        env=os.environ.copy()
        env.pop('REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT',None)
        env.pop('REMOTEWORK_ENABLE_TASK_QUERY_DRAFT',None)
        if enabled:
            env['REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT']='1'
        child=subprocess.run([sys.executable,'-c',code],env=env,capture_output=True,encoding='utf-8',check=True)
        data=json.loads(child.stdout)
        assert len(data['routes'])==(2 if enabled else 0)
        assert ('write_receipts' in data['tables'])==enabled
        assert 'tasks' not in data['tables']


def test_receipt_insert_failure_rolls_back_mutation_and_success_audit(writes,login):
    from sqlalchemy import event
    headers,identity=login()
    engine=writes.extensions['db_engine']
    def fail(conn,cursor,statement,parameters,context,executemany):
        if 'insert into write_receipts' in statement.lower():
            raise RuntimeError('synthetic receipt insert failure')
    with writes.test_client() as client:
        keyed=headers|{'Idempotency-Key':new_key(client,headers)}
        event.listen(engine,'before_cursor_execute',fail)
        try:
            assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'rollback'}).status_code==500
        finally:
            event.remove(engine,'before_cursor_execute',fail)
        with writes.app_context():
            assert get_db().get(User,identity['user']['id']).department=='개발팀'
            assert get_db().scalar(select(func.count()).select_from(WriteReceipt))==0
            assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='test.once'))==0
        assert client.post('/api/v1/_receipt_probe',headers=keyed,json={'department':'rollback'}).status_code==200


def test_missing_receipt_schema_is_reported_without_runtime_ddl(writes,login):
    from sqlalchemy import inspect
    headers,_=login()
    engine=writes.extensions['db_engine']
    WriteReceipt.__table__.drop(engine)
    try:
        with writes.test_client() as client:
            r=client.post('/api/v1/write-requests/keys',headers=headers,json={'action':'task.accepted','target':'self'})
            assert r.status_code==503 and r.json['error']['code']=='WRITE_SCHEMA_REQUIRED'
            assert not inspect(engine).has_table('write_receipts')
    finally:
        WriteReceipt.__table__.create(engine)
