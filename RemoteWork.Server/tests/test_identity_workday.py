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
    post, result, mutate, ROOT, DID)


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
    monkeypatch.setattr(routes,'now',lambda:stamp+timedelta(hours=2))
    assert e[1].get(ROOT+'/workday',headers=normal).json['data']['noon_due'] is True
    job,_=issue(e,normal,purpose='noon_check')
    assert post(e,normal,job,'complete',result(e,normal,job)).status_code==200
    assert post(e,normal,job,'finalize',{'client_request_id':str(uuid4())}).status_code==200
    data=e[1].get(ROOT+'/workday',headers=normal).json['data']
    assert data['noon_due'] is False and data['noon_at'] == routes.iso(stamp+timedelta(hours=2))


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
