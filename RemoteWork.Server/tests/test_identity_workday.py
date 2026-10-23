"""Current work from actual identity/attendance MySQL, never operational data."""
from datetime import timedelta
from uuid import uuid4
import pytest

from sqlalchemy import select, func

from remotework.db import get_db
from remotework.models import AuthSession, User
from remotework.features.identity import routes
from remotework.features.identity.attendance import WorkSession
from remotework.features.identity.models import Context
from test_identity_service import (identity, working, env, clock_in, pending, issue,
    post, result, mutate, probe, ROOT, DID)


@pytest.fixture(autouse=True)
def synthetic_long_lived_login(working):
    # Controlled server time crosses noon. Do not confuse the test's time travel
    # with a production session-lifetime change.
    working[0].config['SESSION_HOURS'] = 24


def counts(e):
    with e[0].app_context():
        db = get_db()
        return tuple(db.scalar(select(func.count()).select_from(m)) for m in (Context, WorkSession, AuthSession))


def test_current_workday_matches_real_clock_in_and_get_has_no_context_writes(working, monkeypatch):
    e = working
    stamp = routes.now().replace(hour=1, minute=0, second=0)
    monkeypatch.setattr(routes, 'now', lambda: stamp)
    _, _, _, saved, normal = clock_in(e)
    before = counts(e)
    answer = e[1].get(ROOT+'/workday',headers=normal)
    assert answer.status_code == 200, answer.json
    data = answer.json['data']
    assert data == dict(server_time=routes.iso(stamp), work_session_ref=saved['work_session_ref'],
        work_date=saved['work_date'], state='open', started_at=routes.iso(stamp), noon_at=None, noon_due=False)
    assert e[1].get(ROOT+'/workday',headers=normal).json['data'] == answer.json['data']
    assert counts(e) == before


def test_noon_due_uses_server_kst_and_records_other_pc_completion(working, monkeypatch):
    e = working
    stamp = routes.now().replace(hour=1,minute=0,second=0)
    monkeypatch.setattr(routes,'now',lambda:stamp)
    _,_,_,_,normal = clock_in(e)
    monkeypatch.setattr(routes,'now',lambda:stamp+timedelta(hours=1))
    assert e[1].get(ROOT+'/workday',headers=normal).json['data']['noon_due'] is True
    job,_=issue(e,normal,purpose='noon_check')
    assert post(e,normal,job,'complete',result(e,normal,job)).status_code==200
    assert post(e,normal,job,'finalize',{'client_request_id':str(uuid4())}).status_code==200
    data=e[1].get(ROOT+'/workday',headers=normal).json['data']
    assert data['noon_due'] is False and data['noon_at'] == routes.iso(stamp+timedelta(hours=1))


@pytest.mark.parametrize("utc_hour,minute,second,due", [(1,59,59,False),(2,0,0,True),(2,59,59,True),(3,0,0,False)])
def test_afternoon_new_challenge_and_workday_share_window(working, monkeypatch, utc_hour, minute, second, due):
    e=working
    morning=routes.now().replace(hour=1,minute=0,second=0)
    monkeypatch.setattr(routes,'now',lambda:morning)
    _,_,_,_,normal=clock_in(e)
    stamp=morning.replace(hour=utc_hour,minute=minute,second=second)
    monkeypatch.setattr(routes,'now',lambda:stamp)
    assert e[1].get(ROOT+'/workday',headers=normal).json['data']['noon_due'] is due
    response=e[1].post(ROOT+'/challenges',headers=normal,json=dict(device_id=DID,purpose='noon_check',client_request_id=str(uuid4())))
    assert response.status_code == (200 if due else 409), response.json
    if not due:
        assert response.json['error']['code']=='ATTENDANCE_NOON_NOT_DUE'


@pytest.mark.parametrize("stage",['approval','complete','finalize'])
def test_no_new_capture_complete_or_save_after_window(working, monkeypatch, stage):
    e=working
    clock=[routes.now().replace(hour=1,minute=0,second=0)]
    monkeypatch.setattr(routes,'now',lambda:clock[0])
    _,_,_,saved,normal=clock_in(e)
    clock[0]=clock[0].replace(hour=2,minute=59,second=50)
    job,_=issue(e,normal,purpose='noon_check')
    if stage=='approval':
        from test_identity_service import signed, wire
        body={'probe_b64':wire.b64encode(signed(e,'id_probe',probe(e,job,'capture')))}
        operation='approvals'
    else:
        body=result(e,normal,job)
        operation='complete'
        if stage=='finalize':
            assert post(e,normal,job,'complete',body).status_code==200
            body={'client_request_id':str(uuid4())}
            operation='finalize'
    clock[0]=clock[0].replace(hour=3,minute=0,second=0)
    denied=post(e,normal,job,operation,body)
    assert denied.status_code==409 and denied.json['error']['code']=='ATTENDANCE_NOON_NOT_DUE'
    with e[0].app_context():
        from remotework.features.identity.models import Challenge
        from remotework.features.identity.attendance import WorkflowReceipt
        db=get_db()
        assert db.get(WorkSession,saved['work_session_ref']).noon_at is None
        assert not db.get(Challenge,job['challenge_id']).workflow_completed
        assert db.get(WorkflowReceipt,job['challenge_id']) is None


def test_committed_afternoon_receipt_recovers_after_12_and_relogin_is_not_a_new_check(working, monkeypatch):
    e=working
    clock=[routes.now().replace(hour=1,minute=0,second=0)]
    monkeypatch.setattr(routes,'now',lambda:clock[0])
    _,_,_,saved,normal=clock_in(e)
    clock[0]=clock[0].replace(hour=2,minute=59,second=50)
    job,_=issue(e,normal,purpose='noon_check')
    body=result(e,normal,job)
    assert post(e,normal,job,'complete',body).status_code==200
    key={'client_request_id':str(uuid4())}
    original=post(e,normal,job,'finalize',key)
    assert original.status_code==200
    login=e[1].post('/api/v1/auth/login',json={'login_id':'staff1','password':__import__('test_identity_service').PASSWORD})
    assert login.status_code==200
    other={'Authorization':'Bearer '+login.json['data']['access_token']}
    data=e[1].get(ROOT+'/workday',headers=other).json['data']
    assert data['noon_at']==routes.iso(clock[0]) and not data['noon_due'] and data['work_session_ref']==saved['work_session_ref']
    duplicate=e[1].post(ROOT+'/challenges',headers=other,json=dict(device_id=DID,purpose='noon_check',client_request_id=str(uuid4())))
    assert duplicate.status_code==409 and duplicate.json['error']['code']=='ATTENDANCE_NOON_ALREADY_RECORDED'
    clock[0]=clock[0].replace(hour=3,minute=0,second=0)
    assert post(e,normal,job,'finalize',key).json['data']==original.json['data']
    assert post(e,normal,job,'complete',body).json['data']['workflow_state']=='completed'
    assert e[1].get(ROOT+'/challenges/'+job['challenge_id'],headers=normal).json['data']['workflow_state']=='completed'


def test_pending_anonymous_and_query_cannot_select_work_or_user(working):
    e=working
    h,_=pending(e)
    assert e[1].get(ROOT+'/workday').status_code==401
    assert e[1].get(ROOT+'/workday',headers=h).status_code==401
    _,_,_,_,normal=clock_in(e)
    assert e[1].get(ROOT+'/workday?user_id=1',headers=normal).status_code==400


def test_same_day_password_relogin_reads_same_server_work(working):
    e=working
    _,_,_,saved,_=clock_in(e)
    response=e[1].post('/api/v1/auth/login',json={'login_id':'staff1','password':__import__('test_identity_service').PASSWORD})
    assert response.status_code==200
    headers={'Authorization':'Bearer '+response.json['data']['access_token']}
    data=e[1].get(ROOT+'/workday',headers=headers).json['data']
    assert data['work_session_ref']==saved['work_session_ref'] and data['work_date']==saved['work_date']


def test_other_workday_and_disabled_user_are_not_reused(working, monkeypatch):
    e=working
    stamp=routes.now().replace(hour=1,minute=0,second=0)
    monkeypatch.setattr(routes,'now',lambda:stamp)
    _,_,_,_,normal=clock_in(e)
    monkeypatch.setattr(routes,'now',lambda:stamp+timedelta(days=1))
    assert e[1].get(ROOT+'/workday',headers=normal).status_code==401
    monkeypatch.setattr(routes,'now',lambda:stamp)
    mutate(e, lambda db:setattr(db.scalar(select(User).where(User.login_id=='staff1')),'is_active',False))
    assert e[1].get(ROOT+'/workday',headers=normal).status_code==401


def test_clock_out_removes_normal_workday_access_but_exact_receipt_replays(working):
    e=working
    _,_,_,_,normal=clock_in(e)
    job,_=issue(e,normal,purpose='clock_out')
    assert post(e,normal,job,'complete',result(e,normal,job)).status_code==200
    original={'client_request_id':str(uuid4())}
    answer=post(e,normal,job,'finalize',original)
    assert answer.status_code==200
    assert e[1].get(ROOT+'/workday',headers=normal).status_code==401
    assert post(e,normal,job,'finalize',original).json['data']==answer.json['data']


def test_other_users_work_is_never_returned_to_the_current_account(working):
    e=working
    _,_,_,saved,normal=clock_in(e)
    def add_other(db):
        other_id=db.scalar(select(User.id).where(User.login_id=='admin1'))
        db.add(WorkSession(id=str(uuid4()),user_id=other_id,
            work_date=saved['work_date'],started_at=routes.now()))
    mutate(e,add_other)
    data=e[1].get(ROOT+'/workday',headers=normal).json['data']
    assert data['work_session_ref']==saved['work_session_ref']


def test_missing_identity_storage_is_503_not_an_empty_current_work(working):
    e=working
    _,_,_,_,normal=clock_in(e)
    engine=e[0].extensions['db_engine']
    with engine.begin() as db:
        db.exec_driver_sql('RENAME TABLE identity_approvals TO identity_approvals_missing')
    try:
        response=e[1].get(ROOT+'/workday',headers=normal)
        assert response.status_code==503 and response.json['error']['code']=='IDENTITY_STORAGE_NOT_READY'
    finally:
        with engine.begin() as db:
            db.exec_driver_sql('RENAME TABLE identity_approvals_missing TO identity_approvals')


def test_midnight_between_identity_load_and_current_read_requires_review(working, monkeypatch):
    e=working
    before=routes.now().replace(hour=14,minute=59,second=59)
    monkeypatch.setattr(routes,'now',lambda:before)
    _,_,_,_,normal=clock_in(e)
    calls=iter([before,before+timedelta(seconds=1)])
    monkeypatch.setattr(routes,'now',lambda:next(calls))
    response=e[1].get(ROOT+'/workday',headers=normal)
    assert response.status_code==409
    assert response.json['error']['code']=='ATTENDANCE_DAY_REVIEW_REQUIRED'
