"""Actual MySQL/raw016/auth/W1/API tests. Fixtures are synthetic members/calendar."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime,timedelta
import json,os,threading,uuid,subprocess
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.error import HTTPError

import pytest
from sqlalchemy import delete,select,inspect,text
from sqlalchemy.engine import make_url
from werkzeug.serving import make_server

from remotework import create_app,leave_service as s,leave_setup
from remotework.auth import create_user
from remotework.db import transaction
from remotework.models import Base,User,SchemaVersion,AdminGuard,AuditEvent,AuthSession,LoginFailure
from remotework.write_requests import WriteReceipt


@pytest.fixture(scope='module')
def leave_app():
    url=os.environ['MYSQL_TEST_URL'];assert make_url(url).database.startswith('remote_work_test_')
    app=create_app(dict(TESTING=False,SECRET_KEY='leave-synthetic-'+40*'x',DATABASE_URL=url,
        ENABLE_COMMON_WRITE_DRAFT=True,ENABLE_LEAVE_DRAFT=True,LEAVE_WRITE_POLICY=s.POLICY))
    e=app.extensions['db_engine'];assert e.dialect.name=='mysql'
    assert not inspect(e).has_table('leave_requests')
    Base.metadata.create_all(e,tables=[t for t in Base.metadata.sorted_tables if t.name!='write_receipts'])
    with e.begin() as db:
        for name in ('004_write_receipts.mysql.sql','016_leave.mysql.sql'):
            raw='\n'.join(l for l in (Path(__file__).resolve().parents[1]/'migrations'/name).read_text().splitlines() if not l.lstrip().startswith('--'))
            for statement in raw.split(';'):
                if statement.strip():db.exec_driver_sql(statement)
    yield app
    e.dispose()


@pytest.fixture
def env(leave_app,monkeypatch):
    app=leave_app;e=app.extensions['db_engine'];app.config['LEAVE_WRITE_POLICY']=s.POLICY
    monkeypatch.setattr(s,'now',lambda db:datetime(2026,10,22,0,0,0))
    with app.app_context(),transaction() as db:
        for table in reversed(s.TABLES):db.execute(table.delete())
        db.execute(delete(WriteReceipt));db.execute(delete(AuditEvent));db.execute(delete(AuthSession));db.execute(delete(LoginFailure));db.execute(delete(User))
        if db.get(SchemaVersion,1) is None:db.add(SchemaVersion(version=1))
        if db.get(AdminGuard,1) is None:db.add(AdminGuard(id=1))
        people={}
        for login,role in [('worker','employee'),('other','employee'),('boss','admin'),('outsider','admin')]:
            u=create_user(db,dict(login_id=login,password='Synthetic-pass-2026!',display_name=login,employee_no=login,department='test',role=role));people[login]=u.id
    manifest=dict(year=2026,policy_id=s.POLICY,holidays=['2026-10-26'],members=[dict(applicant_id=people['worker'],manager_id=people['boss'])])
    with app.app_context(),transaction() as db:leave_setup.prepare(db,manifest,operator_login='boss')
    tokens={}
    for login in people:
        r=app.test_client().post('/api/v1/auth/login',json=dict(login_id=login,password='Synthetic-pass-2026!'))
        assert r.status_code==200,r.json;tokens[login]=r.json['data']['access_token']
    return dict(app=app,ids=people,tokens=tokens,manifest=manifest)


def call(env,path,method='get',body=None,who='worker',key=None):
    headers={'Authorization':'Bearer '+env['tokens'][who]}
    if key:headers['Idempotency-Key']=key
    return getattr(env['app'].test_client(),method)('/api/v1/'+path,json=body,headers=headers)


def data(r,status=200):
    assert r.status_code==status,r.json
    return r.json['data']


def key(env,action=s.SUBMIT,target='new',who='worker'):
    return data(call(env,'write-requests/keys','post',dict(action=action,target=target),who=who),201)['request_key']


def draft(kind='annual',start='2026-10-22',end=None):
    return dict(intent_id=str(uuid.uuid4()),type=kind,start_date=start,end_date=end or start,reason='synthetic reason')


def submit(env,body=None,k=None):
    body=body or draft();k=k or key(env)
    return call(env,'leave/requests','post',body,key=k),body,k


def decision(env,identifier,decision='approved',revision='1',who='boss',intent=None):
    body=dict(intent_id=intent or str(uuid.uuid4()),expected_revision=revision,decision=decision,decision_reason='review reason')
    k=key(env,s.DECIDE,identifier,who)
    return call(env,'leave/requests/'+identifier+'/decision','post',body,who=who,key=k),body,k


def balance(env):return data(call(env,'leave/balance?year=2026'))


def test_raw_schema_and_initial_default_eight_days(env):
    assert balance(env)==dict(year=2026,granted_units=16,reserved_units=0,used_units=0,remaining_units=16,unit_days='0.5',policy_id=s.POLICY)
    with env['app'].app_context(),transaction() as db:
        s.storage(db);assert db.execute(text('SELECT version FROM schema_versions')).scalars().all()==[1]
        before=db.scalar(select(s.balances.c.granted_units))
        assert leave_setup.prepare(db,env['manifest'],operator_login='boss')['new_balances']==0
        assert db.scalar(select(s.balances.c.granted_units))==before


@pytest.mark.parametrize('kind,units',[('annual',2),('am_half',1),('pm_half',1),('sick',0),('official',0)])
def test_five_types_server_units(env,kind,units):
    item=data(submit(env,draft(kind))[0],201);assert item['charged_units']==units
    assert balance(env)['reserved_units']==units


def test_weekend_and_explicit_holiday_excluded(env):
    item=data(submit(env,draft(start='2026-10-22',end='2026-10-27'))[0],201)
    assert item['charged_units']==6  # Thu,Fri,Tue; weekend + Monday holiday excluded


@pytest.mark.parametrize('body,code',[(draft(start='2026-10-24'), 'LEAVE_NON_WORKING_PERIOD'),
    (draft(start='2026-10-26'),'LEAVE_NON_WORKING_PERIOD'),(draft(start='2026-10-21'),'LEAVE_PAST_DATE')])
def test_nonworking_and_past_denied(env,body,code):
    r=submit(env,body)[0];assert r.status_code==409 and r.json['error']['code']==code
    assert balance(env)['reserved_units']==0


def test_approve_reject_restores_reservation_and_reason(env):
    a=data(submit(env)[0],201);r,_,_=decision(env,a['request_id'],'rejected');assert data(r)['revision']=='2'
    assert balance(env)['remaining_units']==16
    view=data(call(env,'leave/requests/'+a['request_id']));assert view['decision_reason']=='review reason' and view['reason']=='synthetic reason'
    b=data(submit(env)[0],201);data(decision(env,b['request_id'])[0]);assert balance(env)['used_units']==2 and balance(env)['reserved_units']==0


def test_overlap_even_distinct_halfdays_conservative(env):
    data(submit(env,draft('am_half'))[0],201)
    r=submit(env,draft('pm_half'))[0];assert r.status_code==409 and r.json['error']['code']=='LEAVE_PERIOD_OVERLAP'


def test_insufficient_balance_current_read(env):
    with env['app'].app_context(),transaction() as db:db.execute(s.balances.update().values(granted_units=1))
    r=submit(env)[0];assert r.status_code==409 and r.json['error']['code']=='LEAVE_BALANCE_INSUFFICIENT'


def test_w1_replay_and_new_key_same_intent_single_reservation(env):
    r,b,k=submit(env);first=data(r,201)
    assert data(call(env,'leave/requests','post',b,key=k),201)==first
    assert data(call(env,'leave/requests','post',b,key=key(env)),201)==first
    found=data(call(env,'leave/operations/'+b['intent_id']+'?action='+s.SUBMIT+'&target=new'))
    assert found['state']=='completed' and found['result']==first
    found=data(call(env,'write-requests/result','post',dict(action=s.SUBMIT,target='new'),key=k));assert found['result']==first
    assert len(data(call(env,'leave/requests'))['items'])==1 and balance(env)['reserved_units']==2


def test_changed_body_same_key_and_intent_conflict(env):
    _,b,k=submit(env);changed=b|{'reason':'different'}
    r=call(env,'leave/requests','post',changed,key=k);assert r.status_code==409 and r.json['error']['code']=='IDEMPOTENCY_KEY_REUSED'
    r=call(env,'leave/requests','post',changed,key=key(env));assert r.status_code==409 and r.json['error']['code']=='LEAVE_INTENT_CONFLICT'


def test_policy_off_cold_result_recovery_without_mutation(env):
    r,b,k=submit(env);first=data(r,201)
    env['app'].config['LEAVE_WRITE_POLICY']=''
    assert data(call(env,'leave/requests','post',b,key=k),201)==first
    cold=create_app(dict(TESTING=False,SECRET_KEY=env['app'].config['SECRET_KEY'],DATABASE_URL=os.environ['MYSQL_TEST_URL'],ENABLE_COMMON_WRITE_DRAFT=True,ENABLE_LEAVE_DRAFT=True,LEAVE_WRITE_POLICY=''))
    other=env|{'app':cold}
    try:
        assert data(call(other,'leave/operations/'+b['intent_id']+'?action='+s.SUBMIT+'&target=new'))['result']==first
        newkey=key(other);r=call(other,'leave/requests','post',draft(start='2026-10-23'),key=newkey)
        assert r.status_code==503 and r.json['error']['code']=='LEAVE_POLICY_REQUIRED'
    finally:cold.extensions['db_engine'].dispose()


def test_request_and_receipt_audit_whole_rollback(env,monkeypatch):
    def fail(*args,**kwargs):raise RuntimeError('synthetic audit failure')
    with monkeypatch.context() as m:
        m.setattr(s,'audit',fail);r,b,k=submit(env);assert r.status_code==500
    assert balance(env)['reserved_units']==0 and data(call(env,'leave/requests'))['items']==[]
    assert data(call(env,'write-requests/result','post',dict(action=s.SUBMIT,target='new'),key=k))['state']=='not_recorded'


def test_current_manager_and_no_admin_bypass(env):
    a=data(submit(env)[0],201)
    assert call(env,'leave/requests/'+a['request_id'],who='other').status_code==403
    assert call(env,'leave/requests/'+a['request_id'],who='outsider').status_code==403
    assert data(call(env,'leave/requests?scope=review',who='outsider'))['items']==[]
    assert data(call(env,'leave/requests/'+a['request_id'],who='boss'))['can_review']
    assert call(env,'write-requests/keys','post',dict(action=s.DECIDE,target=a['request_id']),who='worker').status_code==403


def test_reviewer_revoked_after_key_before_write_and_old_result(env):
    a=data(submit(env)[0],201);r,b,k=decision(env,a['request_id']);data(r)
    with env['app'].app_context(),transaction() as db:db.execute(s.reviewers.update().values(manager_id=env['ids']['outsider'],revision=2))
    assert call(env,'write-requests/result','post',dict(action=s.DECIDE,target=a['request_id']),who='boss',key=k).status_code==403
    assert call(env,'leave/requests/'+a['request_id']+'/decision','post',b,who='boss',key=k).status_code==403
    assert data(call(env,'leave/requests/'+a['request_id']))['status']=='approved'


def test_inactive_reviewer_does_not_hide_own_history(env):
    a=data(submit(env)[0],201)
    with env['app'].app_context(),transaction() as db:db.execute(User.__table__.update().where(User.id==env['ids']['boss']).values(is_active=False))
    assert not data(call(env,'leave/requests/'+a['request_id']))['can_review']
    r=submit(env,draft(start='2026-10-23'))[0];assert r.status_code==503 and r.json['error']['code']=='LEAVE_REVIEWER_REQUIRED'


def test_concurrent_approvals_or_rejection_only_one_transition(env):
    a=data(submit(env)[0],201);k1=key(env,s.DECIDE,a['request_id'],'boss');k2=key(env,s.DECIDE,a['request_id'],'boss')
    def run(pair):
        k,decision=pair
        return call(env,'leave/requests/'+a['request_id']+'/decision','post',dict(intent_id=str(uuid.uuid4()),expected_revision='1',decision=decision,decision_reason='reason'),who='boss',key=k).status_code
    with ThreadPoolExecutor(2) as pool:statuses=list(pool.map(run,[(k1,'approved'),(k2,'rejected')]))
    assert sorted(statuses)==[200,409];assert balance(env)['reserved_units']==0


def test_concurrent_overlapping_submissions_one_reservation(env):
    keys=[key(env),key(env)]
    with ThreadPoolExecutor(2) as pool:statuses=list(pool.map(lambda k:submit(env,k=k)[0].status_code,keys))
    assert sorted(statuses)==[201,409] and balance(env)['reserved_units']==2


def test_stale_revision_and_blank_rejection(env):
    a=data(submit(env)[0],201)
    assert decision(env,a['request_id'],revision='2')[0].status_code==409
    k=key(env,s.DECIDE,a['request_id'],'boss')
    r=call(env,'leave/requests/'+a['request_id']+'/decision','post',dict(intent_id=str(uuid.uuid4()),expected_revision='1',decision='rejected',decision_reason='  '),who='boss',key=k)
    assert r.status_code==400 and balance(env)['reserved_units']==2


def test_expired_intent_tombstone_cannot_restart(env):
    r,b,_=submit(env);data(r,201)
    with env['app'].app_context(),transaction() as db:db.execute(s.operations.update().values(expires_at=datetime(2026,10,21)))
    r=call(env,'leave/requests','post',b,key=key(env));assert r.status_code==410 and r.json['error']['code']=='LEAVE_INTENT_EXPIRED'
    assert call(env,'leave/operations/'+b['intent_id']+'?action='+s.SUBMIT+'&target=new').status_code==410
    assert balance(env)['reserved_units']==2


@pytest.mark.parametrize('field,value',[('intent_id','BAD'),('type','bogus'),('start_date','2026-1-1'),('end_date','2027-01-01'),('reason',100)])
def test_invalid_inputs_never_write(env,field,value):
    r=submit(env,draft()|{field:value})[0];assert r.status_code==400 and balance(env)['reserved_units']==0


def test_duplicate_json_fields_rejected_and_anonymous(env):
    r=env['app'].test_client().post('/api/v1/leave/requests',data='{"intent_id":"x","intent_id":"y"}',content_type='application/json',headers={'Authorization':'Bearer '+env['tokens']['worker']})
    assert r.status_code==400
    assert env['app'].test_client().get('/api/v1/leave/requests').status_code==401


def test_missing_configuration_explicit_readiness(env):
    with env['app'].app_context(),transaction() as db:
        db.execute(s.reviewers.delete());db.execute(s.balances.delete());db.execute(s.years.delete())
    env['app'].config['LEAVE_WRITE_POLICY']=''
    ready=data(call(env,'leave/readiness'))
    assert not ready['write_ready'] and set(ready['pending'])=={'LEAVE_POLICY_REQUIRED','LEAVE_CALENDAR_REQUIRED','LEAVE_ENTITLEMENT_REQUIRED','LEAVE_REVIEWER_REQUIRED'}


def test_setup_rejects_overwrite_or_self_review_without_partial_changes(env):
    bad=json.loads(json.dumps(env['manifest']));bad['members'][0]['granted_units']=99
    with pytest.raises(ValueError):
        with env['app'].app_context(),transaction() as db:leave_setup.prepare(db,bad,operator_login='boss')
    assert balance(env)['granted_units']==16
    bad['members'][0]['manager_id']=env['ids']['worker']
    with pytest.raises(ValueError):
        with env['app'].app_context(),transaction() as db:leave_setup.prepare(db,bad,operator_login='boss')


def test_native_tcp_lost_response_relogin_original_intent(env):
    server=make_server('127.0.0.1',0,env['app'],threaded=True);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
    base='http://127.0.0.1:'+str(server.server_port);b=draft();k=key(env)
    try:
        req=Request(base+'/api/v1/leave/requests',method='POST',data=json.dumps(b).encode(),headers={'Content-Type':'application/json','Authorization':'Bearer '+env['tokens']['worker'],'Idempotency-Key':k})
        with urlopen(req,timeout=10) as r:assert r.status==201;r.read()  # simulate client discarding committed response
        # New login of the same actor may explicitly query original intent; no auto adoption.
        login=env['app'].test_client().post('/api/v1/auth/login',json=dict(login_id='worker',password='Synthetic-pass-2026!'))
        token=login.json['data']['access_token']
        req=Request(base+'/api/v1/leave/operations/'+b['intent_id']+'?action='+s.SUBMIT+'&target=new',headers={'Authorization':'Bearer '+token})
        with urlopen(req,timeout=10) as r:found=json.load(r)['data']
        assert found['state']=='completed' and balance(env)['reserved_units']==2
    finally:server.shutdown();server.server_close();thread.join(10);assert not thread.is_alive()


def test_bad_fk_delete_policy_is_rejected(env):
    e=env['app'].extensions['db_engine']
    with e.connect() as c:fk=next(f for f in inspect(c).get_foreign_keys('leave_operations') if f['constrained_columns']==['request_id'])
    with e.begin() as c:c.exec_driver_sql('ALTER TABLE leave_operations DROP FOREIGN KEY `'+fk['name']+'`, ADD CONSTRAINT broken_leave_fk FOREIGN KEY (request_id) REFERENCES leave_requests(id) ON DELETE CASCADE')
    try:
        r=call(env,'leave/readiness');assert r.status_code==503 and r.json['error']['code']=='LEAVE_SCHEMA_REQUIRED'
    finally:
        with e.begin() as c:c.exec_driver_sql('ALTER TABLE leave_operations DROP FOREIGN KEY broken_leave_fk, ADD FOREIGN KEY (request_id) REFERENCES leave_requests(id)')


def test_actual_csharp_http_bridge(env):
    server=make_server('127.0.0.1',0,env['app'],threaded=True);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
    try:
        root=Path(__file__).resolve().parents[2]
        result=subprocess.run(['dotnet','run','--project',str(root/'Tests/LeaveService.Tests/LeaveService.Tests.csproj'),'-c','Release','--no-build'],
            cwd=root,env=os.environ|{'LEAVE_TEST_URL':'http://127.0.0.1:'+str(server.server_port)},capture_output=True,timeout=90)
        assert result.returncode==0,(result.stdout+result.stderr).decode(errors='replace')
        print(result.stdout.decode(errors='replace').strip())
        assert balance(env)['remaining_units']==16
    finally:server.shutdown();server.server_close();thread.join(10);assert not thread.is_alive()


def test_decision_audit_failure_rolls_back_reservation_and_receipts(env,monkeypatch):
    a=data(submit(env)[0],201);k=key(env,s.DECIDE,a['request_id'],'boss')
    b=dict(intent_id=str(uuid.uuid4()),expected_revision='1',decision='rejected',decision_reason='reason')
    with monkeypatch.context() as m:
        m.setattr(s,'audit',lambda *a,**k:(_ for _ in ()).throw(RuntimeError('decision audit failure')))
        assert call(env,'leave/requests/'+a['request_id']+'/decision','post',b,who='boss',key=k).status_code==500
    assert balance(env)['reserved_units']==2
    assert data(call(env,'leave/requests/'+a['request_id']))['status']=='pending'
    assert data(call(env,'write-requests/result','post',dict(action=s.DECIDE,target=a['request_id']),who='boss',key=k))['state']=='not_recorded'


def test_different_days_compete_for_last_one_day(env):
    with env['app'].app_context(),transaction() as db:db.execute(s.balances.update().values(granted_units=2))
    keys=[key(env),key(env)]
    def run(pair):return submit(env,draft(start=pair[1]),pair[0])[0].status_code
    with ThreadPoolExecutor(2) as pool:statuses=list(pool.map(run,zip(keys,['2026-10-22','2026-10-23'])))
    assert sorted(statuses)==[201,409] and balance(env)['remaining_units']==0


def test_default_off_no_leave_routes_or_automatic_storage(env):
    cold=create_app(dict(SECRET_KEY='cold-'+40*'x',DATABASE_URL=os.environ['MYSQL_TEST_URL'],ENABLE_LEAVE_DRAFT=False))
    try:assert not any(rule.rule.startswith('/api/v1/leave') for rule in cold.url_map.iter_rules())
    finally:cold.extensions['db_engine'].dispose()


def test_check_literal_case_is_not_normalized_away(env):
    e=env['app'].extensions['db_engine']
    with e.begin() as c:c.exec_driver_sql("ALTER TABLE leave_requests DROP CHECK ck_leave_status, ADD CONSTRAINT ck_leave_status CHECK (status IN ('PENDING','approved','rejected'))")
    try:assert call(env,'leave/readiness').status_code==503
    finally:
        with e.begin() as c:c.exec_driver_sql("ALTER TABLE leave_requests DROP CHECK ck_leave_status, ADD CONSTRAINT ck_leave_status CHECK (status IN ('pending','approved','rejected'))")
