"""Password application access after clock-out, using owned MySQL only."""
import json
import re
from uuid import uuid4

import pytest
from sqlalchemy import select,func

from remotework.db import get_db
from remotework.models import AuditEvent,User
from remotework.features.identity import routes
from remotework.features.identity.attendance import WorkSession,WorkflowReceipt
from remotework.features.identity.models import Context,Challenge
from test_identity_service import (identity,working,env,clock_in,issue,pending,post,result,mutate,PASSWORD,ROOT,DID)


def finish(e):
    _,_,_,saved,normal=clock_in(e)
    job,_=issue(e,normal,purpose='clock_out')
    assert post(e,normal,job,'complete',result(e,normal,job)).status_code==200
    key={'client_request_id':str(uuid4())}
    done=post(e,normal,job,'finalize',key)
    assert done.status_code==200
    return saved,normal,job,key,done.json['data']


def attendance(e):
    with e[0].app_context():
        db=get_db();work=db.scalar(select(WorkSession))
        return (work.id,work.work_date,work.started_at,work.noon_at,work.ended_at,
            *(db.scalar(select(func.count()).select_from(model)) for model in (WorkSession,Context,Challenge,WorkflowReceipt)),
            db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action.like('attendance.%'))))


def password_login(e):
    reply=e[1].post('/api/v1/auth/login',json={'login_id':'staff1','password':PASSWORD})
    assert reply.status_code==200,reply.json
    return {'Authorization':'Bearer '+reply.json['data']['access_token']}


def test_repeated_password_login_does_not_reopen_attendance_or_reuse_revoked_session(working):
    e=working;_,old,job,key,done=finish(e);before=attendance(e)
    assert before[4] is not None
    for _ in range(2):
        normal=password_login(e)
        assert e[1].get('/api/v1/auth/me',headers=normal).status_code==200
        assert e[1].get('/api/v1/auth/me',headers=old).status_code==401
        closed=e[1].get(ROOT+'/workday',headers=normal)
        assert closed.status_code==409 and closed.json['error']['code']=='ATTENDANCE_DAY_CLOSED'
        for purpose in ('noon_check','clock_out'):
            reply=e[1].post(ROOT+'/challenges',headers=normal,json={
                'device_id':DID,'purpose':purpose,'client_request_id':str(uuid4())})
            assert reply.status_code==409 and reply.json['error']['code']=='ATTENDANCE_DAY_CLOSED'
        assert e[1].post('/api/v1/auth/logout',headers=normal,json={}).status_code==200
    assert attendance(e)==before
    assert post(e,old,job,'finalize',key).json['data']==done
    with e[0].app_context():
        records=get_db().scalars(select(AuditEvent).where(AuditEvent.action=='auth.login').order_by(AuditEvent.id.desc()).limit(2)).all()
        assert all(json.loads(row.details_json)['resumed_work'] is False for row in records)


def test_web_cookie_login_after_clockout_preserves_closed_attendance(working):
    e=working;finish(e);before=attendance(e)
    web=e[0].test_client()
    html=web.get('/login').get_data(as_text=True)
    csrf=re.search(r'name="csrf_token" value="([^"]+)"',html).group(1)
    reply=web.post('/login',data={'csrf_token':csrf,'login_id':'staff1','password':PASSWORD})
    assert reply.status_code==302 and not reply.location.endswith('/login')
    home=web.get('/')
    assert home.status_code==200
    csrf=re.search(r'name="csrf_token" value="([^"]+)"',home.get_data(as_text=True)).group(1)
    assert attendance(e)==before
    assert web.post('/logout',data={'csrf_token':csrf}).status_code==302


def test_next_kst_day_requires_first_fingerprint_again_without_changing_closed_history(working,monkeypatch):
    e=working
    at=routes.now().replace(hour=1,minute=0,second=0)
    monkeypatch.setattr(routes,'now',lambda:at)
    finish(e);normal=password_login(e);before=attendance(e)
    # 15:00 UTC is the next midnight in KST, on the same UTC date.
    monkeypatch.setattr(routes,'now',lambda:at.replace(hour=15))
    assert e[1].get('/api/v1/auth/me',headers=normal).status_code==401
    reply=e[1].post('/api/v1/auth/login',json={'login_id':'staff1','password':PASSWORD})
    assert reply.status_code==409 and reply.json['error']['code']=='IDENTITY_FINGERPRINT_REQUIRED'
    assert attendance(e)==before


def test_closed_day_cannot_issue_a_second_clockin_even_with_fresh_pending_login(working):
    e=working;finish(e)
    h,_=pending(e)
    reply=e[1].post(ROOT+'/challenges',headers=h,json={
        'device_id':DID,'purpose':'clock_in','client_request_id':str(uuid4())})
    assert reply.status_code==409 and reply.json['error']['code']=='ATTENDANCE_DAY_CLOSED'
    with e[0].app_context():assert get_db().scalar(select(func.count()).select_from(WorkSession))==1


@pytest.mark.parametrize('disabled',[False,True])
def test_closed_attendance_never_bypasses_password_or_active_user_checks(working,disabled):
    e=working;finish(e)
    if disabled:
        mutate(e,lambda db:setattr(db.scalar(select(User).where(User.login_id=='staff1')),'is_active',False))
    before=attendance(e)
    reply=e[1].post('/api/v1/auth/login',json={'login_id':'staff1','password':PASSWORD if disabled else 'incorrect-owned-test'})
    assert reply.status_code==401
    assert attendance(e)==before
