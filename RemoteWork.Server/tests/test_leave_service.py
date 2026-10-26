"""Actual MySQL/raw016/auth/W1/API tests. Fixtures are synthetic members/calendar."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime,timedelta
import json,os,threading,uuid,subprocess
from pathlib import Path
from urllib.request import Request,urlopen
from urllib.error import HTTPError

import pytest
from flask import g,request
from sqlalchemy import delete,select,inspect,text
from sqlalchemy.engine import make_url
from werkzeug.serving import make_server

from remotework import create_app,leave_service as s,leave_setup
from remotework.auth import create_user
from remotework.auth import load_identity,authenticated_transaction
from remotework.common import AppError
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
    monkeypatch.setitem(app.config, 'LEAVE_NEW_USER_MANAGER', '')
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


def prepare_other_applicant(env):
    manifest=env['manifest']|{'members':[dict(applicant_id=env['ids']['other'],manager_id=env['ids']['boss'])]}
    with env['app'].app_context(),transaction() as db:
        leave_setup.prepare(db,manifest,operator_login='boss')


def other_submission(env,*,start='2026-10-22'):
    body=draft(start=start);k=key(env,who='other')
    return data(call(env,'leave/requests','post',body,who='other',key=k),201)


def cooperating_change(env,*,inactive=False):
    # A second real connection commits after load_identity's RR snapshot, but
    # before the feature acquires its guard. Preserve shared writer lock order.
    with env['app'].extensions['db_engine'].begin() as db:
        db.execute(select(AdminGuard).where(AdminGuard.id==1).with_for_update())
        db.execute(select(User).where(User.id==env['ids']['outsider']).with_for_update())
        db.execute(select(AuthSession).where(AuthSession.user_id==env['ids']['outsider']).with_for_update())
        targets=sorted([env['ids']['worker'],env['ids']['boss']])
        db.execute(select(User).where(User.id.in_(targets)).order_by(User.id).with_for_update())
        if inactive:
            db.execute(User.__table__.update().where(User.id==env['ids']['worker']).values(is_active=False))
        else:
            db.execute(s.reviewers.update().where(s.reviewers.c.applicant_id==env['ids']['worker']).values(manager_id=env['ids']['outsider'],revision=2))


def test_review_list_current_assignment_after_identity_snapshot(env):
    created=data(submit(env)[0],201);app=env['app'];done=[]
    def change_after_identity():
        if request.path=='/api/v1/leave/requests' and request.args.get('scope')=='review' and not done:
            assert g.user.id==env['ids']['boss'] and g.auth_session is not None
            cooperating_change(env);done.append(True)
    app.before_request_funcs[None].append(change_after_identity)
    try:
        response=call(env,'leave/requests?scope=review',who='boss')
    finally:app.before_request_funcs[None].remove(change_after_identity)
    assert done==[True]
    assert data(response)['items']==[]  # no stale reason/name/department disclosure
    assert data(call(env,'leave/requests?scope=review',who='boss'))['items']==[]
    assert call(env,'leave/requests/'+created['request_id'],who='boss').status_code==403
    current=data(call(env,'leave/requests?scope=review',who='outsider'))['items']
    assert [i['request_id'] for i in current]==[created['request_id']] and current[0]['can_review']


def test_review_list_inactive_applicant_does_not_hide_active_applicant(env):
    old=data(submit(env)[0],201);prepare_other_applicant(env);live=other_submission(env)
    cooperating_change(env,inactive=True)
    result=data(call(env,'leave/requests?scope=review',who='boss'))
    assert [i['request_id'] for i in result['items']]==[live['request_id']]
    assert result['next_cursor'] is None and result['items'][0]['can_review']
    assert call(env,'leave/requests/'+old['request_id'],who='boss').status_code==403


@pytest.mark.parametrize('hidden',['inactive','reassigned'])
def test_review_pagination_filters_before_limit_and_cursor(env,monkeypatch,hidden):
    prepare_other_applicant(env)
    # Interleave hidden and readable canonical UUIDs independently of randomness.
    # Construct intents/keys before fixing only the request's generated UUID.
    identifiers={n:f'00000000-0000-4000-8000-{n:012d}' for n in range(1,7)}
    for who,positions in [('worker',(1,3,6)),('other',(2,4,5))]:
        for n,start in zip(positions,['2026-10-22','2026-10-23','2026-10-27']):
            body=draft(start=start);k=key(env,who=who);identifier=identifiers[n]
            with monkeypatch.context() as m:
                m.setattr(s.uuid,'uuid4',lambda value=identifier:uuid.UUID(value))
                assert data(call(env,'leave/requests','post',body,who=who,key=k),201)['request_id']==identifier
    cooperating_change(env,inactive=hidden=='inactive')
    cursor=None;seen=[]
    for n in (2,4,5):
        path='leave/requests?scope=review&limit=1'+('&after='+cursor if cursor else '')
        page=data(call(env,path,who='boss'))
        assert [i['request_id'] for i in page['items']]==[identifiers[n]]
        assert page['items'][0]['can_review']
        cursor=page['next_cursor'];seen.extend(i['request_id'] for i in page['items'])
        assert cursor==(identifiers[n] if n!=5 else None)
    assert seen==[identifiers[n] for n in (2,4,5)]
    empty=data(call(env,'leave/requests?scope=review&limit=1&after='+identifiers[5],who='boss'))
    assert empty['items']==[] and empty['next_cursor'] is None


def test_request_view_rechecks_current_read_permission_before_serialization(env):
    item=data(submit(env)[0],201)
    with env['app'].test_request_context('/api/v1/leave/requests',headers={'Authorization':'Bearer '+env['tokens']['outsider']}):
        load_identity()
        with pytest.raises(AppError) as denied:
            with authenticated_transaction() as db:
                row=s.current(db,s.requests,s.requests.c.id==item['request_id'])
                s.request_view(db,row)
        assert denied.value.status==403 and denied.value.code=='FORBIDDEN'


@pytest.mark.parametrize('reviewer_state',['removed','reassigned','inactive'])
def test_own_history_survives_current_reviewer_change(env,reviewer_state):
    created=data(submit(env)[0],201)
    with env['app'].app_context(),transaction() as db:
        if reviewer_state=='removed':db.execute(s.reviewers.delete())
        elif reviewer_state=='reassigned':db.execute(s.reviewers.update().values(manager_id=env['ids']['outsider'],revision=2))
        else:db.execute(User.__table__.update().where(User.id==env['ids']['boss']).values(is_active=False))
    own=data(call(env,'leave/requests'))
    assert [i['request_id'] for i in own['items']]==[created['request_id']]
    detail=data(call(env,'leave/requests/'+created['request_id']))
    assert detail==own['items'][0] and not detail['can_review']
    assert detail['reason']=='synthetic reason' and detail['applicant_id']==env['ids']['worker']


def test_inactive_own_identity_and_ungranted_actor_remain_denied(env):
    created=data(submit(env)[0],201)
    assert call(env,'leave/requests?scope=review').status_code==403
    assert call(env,'leave/requests/'+created['request_id'],who='outsider').status_code==403
    cooperating_change(env,inactive=True)
    for path in ('leave/requests','leave/requests/'+created['request_id']):
        response=call(env,path)
        assert response.status_code==401 and response.json['error']['code']=='AUTH_REQUIRED'


@pytest.mark.parametrize('path_kind',['list','detail'])
def test_current_identity_recheck_after_load_identity_inactivation(env,path_kind):
    created=data(submit(env)[0],201);app=env['app'];done=[]
    path='/api/v1/leave/requests'+('/'+created['request_id'] if path_kind=='detail' else '')
    def deactivate_after_identity():
        if request.path==path and not done:
            assert g.user.id==env['ids']['worker'] and g.auth_session is not None
            cooperating_change(env,inactive=True);done.append(True)
    app.before_request_funcs[None].append(deactivate_after_identity)
    try:response=call(env,path.removeprefix('/api/v1/'))
    finally:app.before_request_funcs[None].remove(deactivate_after_identity)
    assert done==[True]
    assert response.status_code==401 and response.json['error']['code']=='AUTH_REQUIRED'


def new_member_body(name='newmember',role='employee'):
    return dict(login_id=name,password='Synthetic-new-member-2026!',display_name=name,
                employee_no='new-'+name,department='fixture',role=role)


def member_setup_counts(app):
    with app.extensions['db_engine'].connect() as db:
        return tuple(db.execute(text('SELECT COUNT(*) FROM '+name)).scalar_one()
                     for name in ('users','leave_balances','leave_reviewers','audit_events','leave_requests','leave_operations'))


@pytest.mark.parametrize('role',['employee','admin'])
def test_new_member_automatically_receives_eight_days_and_can_submit(env,monkeypatch,role):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    before=balance(env)
    member=data(call(env,'users','post',new_member_body(role=role),who='boss'),201)['user']
    response=app.test_client().post('/api/v1/auth/login',json=dict(login_id='newmember',password='Synthetic-new-member-2026!'))
    env['tokens']['newmember']=data(response)['access_token']
    ready=data(call(env,'leave/readiness',who='newmember'))
    assert ready['schema_ready'] and ready['write_ready'] and ready['pending']==[]
    assert data(call(env,'leave/balance?year=2026',who='newmember'))['remaining_units']==16
    with app.app_context(),transaction() as db:
        link=s.assignment(db,member['id'],required=True)
        assert link.manager_id==env['ids']['boss'] and link.manager_id!=member['id']
        event=db.scalar(select(AuditEvent).where(AuditEvent.action=='leave.auto_prepared'))
        assert event.actor_id==env['ids']['boss'] and event.resource_id==str(member['id'])
    body=draft('am_half');k=key(env,s.SUBMIT,who='newmember')
    item=data(call(env,'leave/requests','post',body,who='newmember',key=k),201)
    assert data(call(env,'leave/balance?year=2026',who='newmember'))['remaining_units']==15
    assert data(decision(env,item['request_id'])[0])['status']=='approved'
    result=data(call(env,'leave/balance?year=2026',who='newmember'))
    assert result['used_units']==1 and result['reserved_units']==0 and balance(env)==before


def test_new_member_default_off_preserves_manual_preparation(env):
    member=data(call(env,'users','post',new_member_body(),who='boss'),201)['user']
    with env['app'].app_context(),transaction() as db:
        assert s.current(db,s.balances,s.balances.c.applicant_id==member['id']) is None
        assert s.assignment(db,member['id']) is None


@pytest.mark.parametrize('problem,code',[
    ('missing-manager','LEAVE_REVIEWER_REQUIRED'),('employee-manager','LEAVE_REVIEWER_REQUIRED'),
    ('inactive-manager','LEAVE_REVIEWER_REQUIRED'),('invalid-setting','LEAVE_NEW_USER_SETUP_REQUIRED'),
    ('policy-off','LEAVE_POLICY_REQUIRED'),('unprepared-year','LEAVE_CALENDAR_REQUIRED'),
    ('unprepared-schema','LEAVE_SCHEMA_REQUIRED')])
def test_new_member_preparation_missing_rolls_back_entire_creation(env,monkeypatch,problem,code):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    if problem=='missing-manager':monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','missing')
    elif problem=='employee-manager':monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','worker')
    elif problem=='inactive-manager':
        with app.app_context(),transaction() as db:db.execute(User.__table__.update().where(User.id==env['ids']['boss']).values(is_active=False))
    elif problem=='invalid-setting':monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER',True)
    elif problem=='policy-off':monkeypatch.setitem(app.config,'LEAVE_WRITE_POLICY','')
    elif problem=='unprepared-year':monkeypatch.setattr(s,'now',lambda db:datetime(2026,12,31,15,0))
    elif problem=='unprepared-schema':
        def missing(db):raise AppError(503,'LEAVE_SCHEMA_REQUIRED','fixture schema missing')
        monkeypatch.setattr(s,'storage',missing)
    before=member_setup_counts(app)
    response=call(env,'users','post',new_member_body(),who='outsider')
    assert response.status_code==503 and response.json['error']['code']==code
    assert member_setup_counts(app)==before


def test_new_member_audit_failure_rolls_back_user_grant_and_reviewer(env,monkeypatch):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    def fail(*args,**kwargs):raise RuntimeError('synthetic new-member audit failure')
    monkeypatch.setattr(leave_setup,'audit',fail)
    before=member_setup_counts(app)
    response=call(env,'users','post',new_member_body(),who='boss')
    assert response.status_code==500 and member_setup_counts(app)==before


def test_new_member_cli_uses_same_automatic_setup_without_fake_operator(env,monkeypatch):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    result=app.test_cli_runner().invoke(args=['create-user','--login-id','climember','--display-name','CLI fixture',
        '--employee-no','CLI-FIXTURE','--department','fixture','--role','employee','--password','Synthetic-new-member-2026!'])
    assert result.exit_code==0,result.output
    with app.app_context(),transaction() as db:
        person=db.scalar(select(User).where(User.login_id=='climember'))
        assert s.totals(db,person.id,2026)['remaining_units']==16
        assert s.assignment(db,person.id,required=True).manager_id==env['ids']['boss']
        event=db.scalar(select(AuditEvent).where(AuditEvent.action=='leave.auto_prepared'))
        assert event.actor_id is None


def test_new_member_duplicate_or_login_or_update_does_not_regrant(env,monkeypatch):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    person=data(call(env,'users','post',new_member_body(),who='boss'),201)['user']
    counts=member_setup_counts(app)
    assert call(env,'users','post',new_member_body(),who='boss').status_code==409
    assert member_setup_counts(app)==counts
    data(call(env,'users/'+str(person['id']),'patch',dict(display_name='Updated fixture'),who='boss'))
    response=app.test_client().post('/api/v1/auth/login',json=dict(login_id='newmember',password='Synthetic-new-member-2026!'))
    assert response.status_code==200
    with app.app_context(),transaction() as db:
        assert s.totals(db,person['id'],2026)['granted_units']==16
        assert len(db.scalars(select(AuditEvent).where(AuditEvent.action=='leave.auto_prepared')).all())==1


def test_new_member_untrusted_input_and_employee_cannot_control_preparation(env,monkeypatch):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    assert call(env,'users','post',new_member_body(),who='worker').status_code==403
    body=new_member_body()|dict(granted_units=732,manager_id=env['ids']['outsider'],year=2027)
    person=data(call(env,'users','post',body,who='boss'),201)['user']
    with app.app_context(),transaction() as db:
        assert s.totals(db,person['id'],2026)['granted_units']==16
        assert s.assignment(db,person['id'],required=True).manager_id==env['ids']['boss']


def test_new_member_kst_year_uses_only_explicit_confirmed_calendar(env,monkeypatch):
    app=env['app'];monkeypatch.setitem(app.config,'LEAVE_NEW_USER_MANAGER','boss')
    monkeypatch.setattr(s,'now',lambda db:datetime(2026,12,31,14,59,59))
    first=data(call(env,'users','post',new_member_body('yearbefore'),who='boss'),201)['user']
    monkeypatch.setattr(s,'now',lambda db:datetime(2026,12,31,15,0))
    response=call(env,'users','post',new_member_body('yearafter'),who='boss')
    assert response.status_code==503 and response.json['error']['code']=='LEAVE_CALENDAR_REQUIRED'
    with app.app_context(),transaction() as db:
        assert s.totals(db,first['id'],2026)['granted_units']==16
        db.execute(s.years.insert().values(year=2027,policy_id=s.POLICY,holidays_json='[]',configured_at=datetime(2026,12,31)))
    second=data(call(env,'users','post',new_member_body('yearafter'),who='boss'),201)['user']
    with app.app_context(),transaction() as db:
        assert s.totals(db,second['id'],2027)['granted_units']==16
        assert s.current(db,s.balances,s.balances.c.applicant_id==first['id'],s.balances.c.year==2027) is None
