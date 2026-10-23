"""Owned MySQL only: management transfer, current auth and immutable receipts."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from uuid import uuid4
import pytest
from sqlalchemy import delete, func, inspect, select, text
from remotework.common import AppError
from remotework.db import get_db, transaction
from remotework.models import AuditEvent, ResourceGrant, User
from remotework.features.enrollment.models import Mapping, Job, Claim, Device
from remotework.features.enrollment import transfer as tx
from remotework.features.identity.models import Context, Challenge
from remotework.features.identity.attendance import WorkSession
from test_identity_service import (env,identity,working,clock_in,pending,issue,result,post,mutate,ROOT,PASSWORD,DID,ticket,probe,signed,wire)


@pytest.fixture
def transferring(working):
    e=working; engine=e[0].extensions['db_engine']
    sql=(Path(__file__).resolve().parents[1]/'migrations/drafts/enrollment_owner_transfer.mysql.sql').read_text()
    sql='\n'.join(line for line in sql.splitlines() if not line.startswith('--'))
    try:
        # Exercise migration from the real legacy007 four-column constraint.
        with engine.begin() as conn:
            fks=inspect(conn).get_foreign_keys('enrollment_mappings')
            if any(f['name']=='fk_enrollment_mapping_location' for f in fks):
                conn.execute(text('ALTER TABLE enrollment_mappings DROP FOREIGN KEY fk_enrollment_mapping_location, ADD CONSTRAINT fk_enrollment_mapping_binding FOREIGN KEY(enrollment_id,device_id,slot,user_id) REFERENCES enrollment_jobs(enrollment_id,device_id,reserved_slot,target_user_id)'))
            for statement in sql.split(';'):
                if statement.strip():conn.execute(text(statement))
        tx.require_schema(engine)
        yield e
    finally:tx.TransferBase.metadata.drop_all(engine)


def arguments(**changes):
    return dict(actor_login='admin1',from_login='staff1',to_login='admin1',device_id=DID,slot=7,
                expected_revision=1,request_id=str(uuid4()),reason='Explicit test transfer; retain sensor and history')|changes


def run(e,args):
    with e[0].app_context(),transaction() as db:return tx.transfer(db,**args)


def snapshot(e):
    with e[0].app_context():
        db=get_db();m=db.scalar(select(Mapping))
        return (m.user_id,m.revision,db.scalar(select(func.count()).select_from(tx.Transfer)),
                db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='enrollment.mapping_transferred_cli')))


def test_transfer_and_permanent_exact_request_receipt_preserve_original_evidence(transferring):
    e=transferring;args=arguments()
    with e[0].app_context():
        db=get_db();job=db.scalar(select(Job));claim=db.scalar(select(Claim));m=db.scalar(select(Mapping))
        before=(job.target_user_id,job.result_hash,job.result_json,job.state,claim.enrollment_id,claim.revision,claim.state,m.mapping_id,m.enrollment_id,m.slot)
    reply=run(e,args);assert reply['from_revision']=='1' and reply['to_revision']=='2'
    assert run(e,args)==reply
    assert snapshot(e)==(e[4][3]['admin1'],2,1,1)
    with e[0].app_context():
        db=get_db();job=db.scalar(select(Job));claim=db.scalar(select(Claim));m=db.scalar(select(Mapping))
        assert before==(job.target_user_id,job.result_hash,job.result_json,job.state,claim.enrollment_id,claim.revision,claim.state,m.mapping_id,m.enrollment_id,m.slot)
    with pytest.raises(AppError,match='이전') as err:run(e,args|{'reason':'different intent'})
    assert err.value.code=='ENROLLMENT_TRANSFER_REQUEST_CONFLICT'
    # A later reverse transfer does not allow the original request to execute again.
    run(e,arguments(from_login='admin1',to_login='staff1',expected_revision=2))
    assert run(e,args)==reply
    assert snapshot(e)==(e[4][3]['staff1'],3,2,2)


@pytest.mark.parametrize('change,code',[
    ({'actor_login':'staff1'},'ENROLLMENT_TRANSFER_FORBIDDEN'),
    ({'actor_login':'admin2'},'ENROLLMENT_TRANSFER_FORBIDDEN'),
    ({'expected_revision':2},'ENROLLMENT_TRANSFER_MAPPING_CHANGED'),
    ({'from_login':'staff2'},'ENROLLMENT_TRANSFER_MAPPING_CHANGED'),
    ({'to_login':'staff1'},'ENROLLMENT_TRANSFER_INVALID_INPUT'),
    ({'to_login':'missing'},'ENROLLMENT_TRANSFER_USER_UNAVAILABLE'),
])
def test_current_authority_and_expected_binding_fail_closed(transferring,change,code):
    e=transferring;before=snapshot(e)
    with pytest.raises(AppError) as err:run(e,arguments(**change))
    assert err.value.code==code and snapshot(e)==before


@pytest.mark.parametrize('login',['staff1','admin1'])
def test_disabled_source_or_target_rejected(transferring,login):
    e=transferring
    mutate(e,lambda db:setattr(db.scalar(select(User).where(User.login_id==login)),'is_active',False))
    with pytest.raises(AppError):run(e,arguments())
    assert snapshot(e)[1:]==(1,0,0)


@pytest.mark.parametrize('user',['staff1','admin1'])
def test_open_work_blocks_transfer_without_ending_it(transferring,user):
    e=transferring
    mutate(e,lambda db:db.add(WorkSession(id=str(uuid4()),user_id=e[4][3][user],work_date='2026-10-23',started_at=__import__('datetime').datetime(2026,10,23),ended_at=None)))
    with pytest.raises(AppError) as err:run(e,arguments())
    assert err.value.code=='ENROLLMENT_TRANSFER_BUSY' and snapshot(e)[1:]==(1,0,0)


@pytest.mark.parametrize('verified',[False,True])
def test_outstanding_capture_or_unfinalized_result_blocks_transfer(transferring,verified):
    e=transferring;h,_=pending(e);job,_=issue(e,h)
    if verified:assert post(e,h,job,'complete',result(e,h,job)).status_code==200
    with pytest.raises(AppError) as err:run(e,arguments())
    assert err.value.code=='ENROLLMENT_TRANSFER_BUSY' and snapshot(e)[1:]==(1,0,0)


def test_audit_failure_rolls_back_owner_revision_and_receipt(transferring,monkeypatch):
    e=transferring;before=snapshot(e)
    def broken(*args,**kwargs):raise RuntimeError('audit unavailable')
    monkeypatch.setattr(tx,'audit',broken)
    with pytest.raises(RuntimeError,match='audit unavailable'):run(e,arguments())
    assert snapshot(e)==before


@pytest.mark.parametrize('same_request',[False,True])
def test_concurrent_transfer_serializes_on_common_guard(transferring,same_request):
    e=transferring;a=arguments();b=a if same_request else arguments()
    def attempt(args):
        try:return run(e,args)
        except AppError as err:return err.code
    with ThreadPoolExecutor(2) as pool:answers=list(pool.map(attempt,[a,b]))
    assert snapshot(e)[1:]==(2,1,1)
    if same_request:assert answers[0]==answers[1]
    else:assert sum(isinstance(x,dict) for x in answers)==1 and 'ENROLLMENT_TRANSFER_MAPPING_CHANGED' in answers


def test_new_owner_issues_revision2_old_owner_cannot_capture(transferring):
    e=transferring;oldh,_=pending(e)
    run(e,arguments())
    assert e[1].post(ROOT+'/challenges',headers=oldh,json=dict(client_request_id=str(uuid4()),device_id=DID,purpose='clock_in')).status_code==401
    fresh,_=pending(e)
    assert e[1].post(ROOT+'/challenges',headers=fresh,json=dict(client_request_id=str(uuid4()),device_id=DID,purpose='clock_in')).status_code==403
    r=e[1].post(ROOT+'/login',json=dict(login_id='admin1',password=PASSWORD,client_request_id=str(uuid4())))
    assert r.status_code==200
    h={'Authorization':'IdentityPending '+r.json['data']['pending_token']}
    job,_=issue(e,h);t=ticket(e,job)
    assert t['mapping_revision']=='2' and t['expected_slot']==7
    assert post(e,h,job,'complete',result(e,h,job)).status_code==200
    saved=post(e,h,job,'finalize',dict(client_request_id=str(uuid4())))
    assert saved.status_code==200
    with e[0].app_context():assert get_db().scalar(select(WorkSession)).user_id==e[4][3]['admin1']


def test_completed_original_receipt_replays_without_new_authorization_or_history_rewrite(transferring):
    e=transferring;_,_,_,_,normal=clock_in(e)
    job,_=issue(e,normal,purpose='clock_out');raw=result(e,normal,job)
    assert post(e,normal,job,'complete',raw).status_code==200
    original=dict(client_request_id=str(uuid4()))
    answer=post(e,normal,job,'finalize',original);assert answer.status_code==200
    with e[0].app_context():
        work=get_db().scalar(select(WorkSession));before=(work.id,work.user_id,work.started_at,work.ended_at)
    run(e,arguments())
    assert e[1].get(ROOT+'/challenges/'+job['challenge_id'],headers=normal).status_code==200
    assert post(e,normal,job,'complete',raw).status_code==200
    assert post(e,normal,job,'finalize',original).json['data']==answer.json['data']
    assert post(e,normal,job,'finalize',dict(client_request_id=str(uuid4()))).status_code==409
    old_probe={'probe_b64':wire.b64encode(signed(e,'id_probe',probe(e,job,'capture')))}
    # Clock-out revoked the ordinary Bearer. The receipt-only recovery is not
    # available on approval endpoints, so current auth rejects it with 401.
    assert post(e,normal,job,'approvals',old_probe).status_code==401
    with e[0].app_context():
        work=get_db().scalar(select(WorkSession));assert before==(work.id,work.user_id,work.started_at,work.ended_at)


def test_schema_rejects_legacy_subject_fk_and_raw007_migrates(transferring):
    e=transferring;engine=e[0].extensions['db_engine']
    with engine.begin() as c:
        c.execute(text('ALTER TABLE enrollment_mappings DROP FOREIGN KEY fk_enrollment_mapping_location, ADD CONSTRAINT fk_enrollment_mapping_binding FOREIGN KEY(enrollment_id,device_id,slot,user_id) REFERENCES enrollment_jobs(enrollment_id,device_id,reserved_slot,target_user_id)'))
    with pytest.raises(AppError) as err:tx.require_schema(engine)
    assert err.value.code=='ENROLLMENT_TRANSFER_SCHEMA_REQUIRED'
    with engine.begin() as c:
        c.execute(text('ALTER TABLE enrollment_mappings DROP FOREIGN KEY fk_enrollment_mapping_binding, ADD CONSTRAINT fk_enrollment_mapping_location FOREIGN KEY(enrollment_id,device_id,slot) REFERENCES enrollment_jobs(enrollment_id,device_id,reserved_slot)'))
    tx.require_schema(engine);run(e,arguments())


def test_cli_is_explicit_and_has_no_sensor_or_http_side_effect(transferring):
    e=transferring;args=arguments();cli=[]
    for k,v in args.items():cli.extend(['--'+k.replace('_','-'),str(v)])
    result=e[0].test_cli_runner().invoke(args=['transfer-enrollment-owner',*cli])
    assert result.exit_code==0,result.output
    assert snapshot(e)[1:]==(2,1,1)
