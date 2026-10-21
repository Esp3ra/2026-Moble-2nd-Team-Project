"""Real raw011/HTTP transactions on owned MySQL; never a live server or sensor."""
from concurrent.futures import ThreadPoolExecutor
from datetime import timedelta
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from uuid import uuid4

import pytest
from sqlalchemy import delete, event, func, inspect, select
from sqlalchemy.exc import IntegrityError, OperationalError

from remotework import create_app
from remotework.db import get_db, transaction
from remotework.models import AuditEvent, AuthSession, ResourceGrant, User
from remotework.features.enrollment.models import Device, Job, Claim, Mapping
from remotework.features.enrollment.recovery import Recovery, RecoveryBase
from remotework.features.enrollment.durability import PhysicalReceipt
from remotework import enrollment_protocol_draft as wire
from test_device_enrollment import env, issue, complete, result, mutate, count, DID, KEYS, ROOT
from test_enrollment_durability import durable, post, report, intent_body
from test_task_migration_draft import owned_databases, _mysql_tool, _private_file

SQL = Path(__file__).resolve().parents[1] / "migrations/011_enrollment_recovery.mysql.sql"


@pytest.fixture
def recovery(durable):
    app = create_app(dict(durable[0].config) | {"ENABLE_ENROLLMENT_RECOVERY_DRAFT": True})
    engine = app.extensions["db_engine"]
    assert engine.url.database.startswith("remote_work_test_")
    sql = "\n".join(line for line in SQL.read_text(encoding="utf8").splitlines() if not line.lstrip().startswith("--"))
    statements = [part for part in sql.split(";") if part.strip()]
    assert len(statements) == 1 and statements[0].lstrip().startswith("CREATE TABLE enrollment_recoveries")
    with engine.begin() as db:
        db.exec_driver_sql(statements[0])
    value = (app, app.test_client(), *durable[2:])
    yield value
    RecoveryBase.metadata.drop_all(engine)
    engine.dispose()


def certificate(e, job, tmp_path, **changes):
    stamp = e[4][0] - timedelta(seconds=2)
    raw = json.dumps({"operator_note": "Synthetic private unsigned evidence; no real device"}).encode()
    body = dict(enrollment_id=job["enrollment_id"], device_id=DID, slot=job["reserved_slot"],
        operation_revision=job["operation_revision"], claim_revision=job["claim_revision"], key_epoch=job["key_epoch"],
        report_sha256=hashlib.sha256(raw).hexdigest(), source_report_sha256=hashlib.sha256(raw).hexdigest(), fenced_at=stamp.isoformat()+"Z",
        observed_at=(stamp+timedelta(microseconds=171103)).isoformat()+"Z",
        checked_at=(stamp+timedelta(microseconds=179486)).isoformat()+"Z", writer_fenced=True,
        slot_empty=True, signed_result_present=False, target_binding_verified=False) | changes
    report_file, attestation = tmp_path / (job["enrollment_id"]+".report.json"), tmp_path / (job["enrollment_id"]+".attestation.json")
    report_file.write_bytes(raw); attestation.write_text(json.dumps(body), encoding="utf8")
    return attestation, report_file


def close(e, job, tmp_path, *, apply=True, operator="admin1", **changes):
    attestation, report_file = certificate(e, job, tmp_path, **changes)
    args = ["close-enrollment-for-recovery", "--login-id", operator,
            "--attestation-file", str(attestation), "--report-file", str(report_file)]
    if apply:
        args.append("--apply")
    return e[0].test_cli_runner().invoke(args=args)


def expired(e):
    job, _ = issue(e)
    e[4][0] += timedelta(minutes=4)
    return job


def approved(e, tmp_path):
    job = expired(e)
    response = close(e, job, tmp_path)
    assert response.exit_code == 0, response.output
    return job, json.loads(response.output)


def reissue(e, recovery_id, *, headers=None, body=None):
    body = {"client_request_id": str(uuid4()), "target_user_id": e[3]["staff1"]} if body is None else body
    return e[1].post(ROOT+"/recoveries/"+recovery_id+"/reissue", headers=headers or e[2], json=body)


def test_preview_then_explicit_close_preserves_ticket_clock_and_microseconds(recovery, tmp_path):
    job = expired(recovery)
    response = close(recovery, job, tmp_path, apply=False)
    assert response.exit_code == 0 and json.loads(response.output)["action"] == "preview_only"
    assert count(recovery[0], Recovery) == 0
    with recovery[0].app_context():
        original = get_db().get(Job, job["enrollment_id"])
        snapshot = (original.ticket_digest, original.operation_revision, original.claim_revision,
                    original.issuer_session_hash, original.expires_at, original.completed_at)
        assert original.state == "issued"
    first = close(recovery, job, tmp_path)
    assert first.exit_code == 0, first.output
    again = close(recovery, job, tmp_path)
    assert again.exit_code == 0 and json.loads(again.output) == json.loads(first.output)
    with recovery[0].app_context():
        db = get_db(); old = db.get(Job, job["enrollment_id"]); value = db.scalar(select(Recovery))
        assert snapshot == (old.ticket_digest, old.operation_revision, old.claim_revision,
                            old.issuer_session_hash, old.expires_at, old.completed_at)
        assert old.state == "recovery_required" and old.result_hash is None and old.result_json is None
        assert db.get(Claim, (DID, job["reserved_slot"])).state == "quarantined"
        assert (value.observed_at-value.fenced_at).total_seconds() == .171103
        assert (value.checked_at-value.fenced_at).total_seconds() == .179486
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "enrollment.operator_closed")) == 1


def test_new_current_session_reissue_fixed_body_replay_then_mysql_complete(recovery, tmp_path):
    old, approval = approved(recovery, tmp_path)
    headers = recovery[5]()  # Deliberately a NEW admin session; never edit the original.
    body = {"client_request_id": str(uuid4()), "target_user_id": recovery[3]["staff2"]}
    first = reissue(recovery, approval["recovery_id"], headers=headers, body=body)
    assert first.status_code == 200, first.json
    new = first.json["data"]["job"]
    assert new["enrollment_id"] != old["enrollment_id"] and new["reserved_slot"] == old["reserved_slot"]
    assert new["claim_revision"] == "2" and new["operation_revision"] == "1"
    assert reissue(recovery, approval["recovery_id"], headers=headers, body=body).json["data"] == first.json["data"]
    assert reissue(recovery, approval["recovery_id"], headers=headers, body=body | {"target_user_id": recovery[3]["staff1"]}).status_code == 409
    assert reissue(recovery, approval["recovery_id"], body=body).status_code == 403  # Different old session.
    new_env = (recovery[0], recovery[1], headers, *recovery[3:])
    assert post(new_env, new, "/storage-intent", intent_body(new)).status_code == 200
    raw = result(new)
    assert report(new_env, new, raw).status_code == 200
    assert complete(new_env, new, raw).status_code == 200
    with recovery[0].app_context():
        db = get_db(); record = db.scalar(select(Recovery)); mapping = db.scalar(select(Mapping))
        assert record.new_enrollment_id == new["enrollment_id"] and mapping.user_id == recovery[3]["staff2"]
        assert db.scalar(select(func.count()).select_from(Job)) == 2
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "enrollment.reissued")) == 1


@pytest.mark.parametrize("step", [timedelta(seconds=1), timedelta(microseconds=200001)],
                         ids=["second-boundary", "subsecond-clock"])
def test_close_uses_one_timestamp_for_binding_across_clock_ticks(recovery, tmp_path, step):
    old = expired(recovery)
    with recovery[0].app_context():
        source = get_db().get(Job, old["enrollment_id"])
        original = (source.ticket_digest, source.issuer_session_hash, source.expires_at,
                    source.completed_at, source.operation_revision, source.claim_revision)
    ticks = []
    start = recovery[4][0] + timedelta(microseconds=700000)

    def advancing_clock():
        value = start + step * len(ticks)
        ticks.append(value)
        return value

    recovery[0].config["ENROLLMENT_CLOCK"] = advancing_clock
    response = close(recovery, old, tmp_path)
    assert response.exit_code == 0, response.output
    approval = json.loads(response.output)
    assert len(ticks) >= 2 and ticks[1] > ticks[0]
    with recovery[0].app_context():
        db = get_db(); source = db.get(Job, old["enrollment_id"])
        record = db.get(Recovery, approval["recovery_id"])
        assert record.closed_at == source.updated_at
        assert original == (source.ticket_digest, source.issuer_session_hash, source.expires_at,
                            source.completed_at, source.operation_revision, source.claim_revision)
        assert db.get(Claim, (DID, old["reserved_slot"])).state == "quarantined"
    again = close(recovery, old, tmp_path)
    assert again.exit_code == 0 and json.loads(again.output) == approval
    status = recovery[1].get(ROOT + "/recoveries/" + approval["recovery_id"], headers=recovery[2])
    assert status.status_code == 200, status.json
    headers = recovery[5]()  # New current login, no takeover of the source session.
    body = {"client_request_id": str(uuid4()), "target_user_id": recovery[3]["staff2"]}
    fresh = reissue(recovery, approval["recovery_id"], headers=headers, body=body)
    assert fresh.status_code == 200, fresh.json
    assert fresh.json["data"]["job"]["claim_revision"] == "2"
    assert reissue(recovery, approval["recovery_id"], headers=headers, body=body).json["data"] == fresh.json["data"]
    with recovery[0].app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(Recovery)) == 1
        assert db.scalar(select(func.count()).select_from(Job)) == 2
        assert db.scalar(select(func.count()).select_from(Mapping)) == 0
        for action in ("enrollment.operator_closed", "enrollment.reissued"):
            assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == action)) == 1


def test_old_paths_and_ordinary_issue_never_release_or_reuse_claim(recovery, tmp_path):
    old, approval = approved(recovery, tmp_path)
    assert complete(recovery, old, result(old)).status_code == 409
    assert post(recovery, old, "/approval", intent_body(old) | {"scope":"sensor_write"}).status_code == 409
    # Ordinary issue cannot bypass the history for the same target.
    assert recovery[1].post(ROOT, headers=recovery[2], json={"client_request_id":str(uuid4()),
        "target_user_id":recovery[3]["staff1"],"device_id":DID}).status_code == 409
    response = reissue(recovery, approval["recovery_id"]); assert response.status_code == 200
    assert complete(recovery, old, result(old)).status_code == 409
    assert count(recovery[0], Mapping) == 0


@pytest.mark.parametrize("field,value", [("slot_empty",False),("writer_fenced",False),("writer_fenced",1),
    ("signed_result_present",True),("target_binding_verified",True),("report_sha256","0"*64),
    ("claim_revision","01"),("operation_revision","2"),("slot",True),("key_epoch","2"),
    ("checked_at","2026-10-20T01:02:00+09:00"),("fenced_at","2099-10-20T00:00:00Z")])
def test_operator_evidence_mismatch_never_changes_job(recovery, tmp_path, field, value):
    job = expired(recovery)
    response = close(recovery, job, tmp_path, **{field:value})
    assert response.exit_code != 0
    assert count(recovery[0], Recovery) == 0
    with recovery[0].app_context():assert get_db().get(Job,job["enrollment_id"]).state == "issued"


def test_live_ticket_or_observation_before_job_change_rejected(recovery, tmp_path):
    job, _ = issue(recovery); recovery[4][0] += timedelta(seconds=10)
    assert "TOO_EARLY" in close(recovery,job,tmp_path).output
    recovery[4][0] += timedelta(minutes=4)
    mutate(recovery, lambda db:setattr(db.get(Job,job["enrollment_id"]),"updated_at",recovery[4][0]))
    assert "TOO_EARLY" in close(recovery,job,tmp_path).output


@pytest.mark.parametrize("kind", ["intent","signed_result","mapping"])
def test_physical_evidence_or_any_mapping_requires_separate_review(recovery,tmp_path,kind):
    job,_=issue(recovery)
    if kind=="intent": assert post(recovery,job,"/storage-intent",intent_body(job)).status_code==200
    elif kind=="signed_result": assert report(recovery,job,result(job,outcome="failed",failure="duplicate_finger")).status_code==200
    else:
        mutate(recovery,lambda db:db.add(Mapping(mapping_id=str(uuid4()),enrollment_id=job['enrollment_id'],
            device_id=DID,user_id=recovery[3]['staff1'],slot=job['reserved_slot'],revision=1,active=False,created_at=recovery[4][0])))
    recovery[4][0]+=timedelta(minutes=4)
    response=close(recovery,job,tmp_path)
    assert response.exit_code!=0 and "REQUIRES_REVIEW" in response.output


@pytest.mark.parametrize("change,expected",[("grant",403),("role",403),("session",401),("device",409),("profile",409),("keys",409),("new_target",409)])
def test_current_authority_binding_and_new_target_rechecked(recovery,tmp_path,change,expected):
    old,approval=approved(recovery,tmp_path)
    if change=='keys':
        recovery[0].config['ENROLLMENT_DEVICE_KEYS'][DID]['request_key']=b'x'*32
    else:
        def modify(db):
            if change=='grant':db.execute(delete(ResourceGrant))
            elif change=='role':db.get(User,recovery[3]['admin1']).role='employee'
            elif change=='session':db.execute(delete(AuthSession))
            elif change=='device':db.get(Device,DID).enabled=False
            elif change=='profile':db.get(Device,DID).candidate_slots_json='[8]'
            else:db.get(User,recovery[3]['staff1']).is_active=False
        mutate(recovery,modify)
    assert reissue(recovery,approval['recovery_id']).status_code==expected
    assert count(recovery[0],Job)==1 and count(recovery[0],Mapping)==0


def test_other_admin_even_with_grant_cannot_use_operator_approval(recovery,tmp_path):
    from remotework.common import grant_resource
    _,approval=approved(recovery,tmp_path)
    mutate(recovery,lambda db:grant_resource(db,recovery[3]['admin2'],'enrollment_device',DID))
    assert reissue(recovery,approval['recovery_id'],headers=recovery[5]('admin2')).status_code==403
    assert close(recovery,expired_other(recovery),tmp_path,operator='admin2').exit_code==0


def expired_other(e):
    job,_=issue(e,target_user_id=e[3]['staff2']);e[4][0]+=timedelta(minutes=4);return job


def test_duplicate_reissue_race_is_one_job_one_revision_and_one_audit(recovery,tmp_path):
    _,approval=approved(recovery,tmp_path)
    body={'client_request_id':str(uuid4()),'target_user_id':recovery[3]['staff1']}
    def run(_):
        with recovery[0].test_client() as client:
            response=client.post(ROOT+'/recoveries/'+approval['recovery_id']+'/reissue',headers=recovery[2],json=body)
            return response.status_code,response.json['data']['job']['enrollment_id']
    with ThreadPoolExecutor(2) as pool:responses=list(pool.map(run,range(2)))
    assert all(status==200 for status,_ in responses) and responses[0][1]==responses[1][1]
    assert count(recovery[0],Job)==2
    with recovery[0].app_context():assert get_db().get(Claim,(DID,7)).revision==2


def test_insert_failure_rolls_back_job_claim_link_and_audit(recovery,tmp_path):
    old,approval=approved(recovery,tmp_path);engine=recovery[0].extensions['db_engine']
    def fail(connection,cursor,statement,params,context,many):
        if statement.startswith('UPDATE enrollment_claims'):raise OperationalError(statement,{},RuntimeError('synthetic'))
    event.listen(engine,'before_cursor_execute',fail)
    body={'client_request_id':str(uuid4()),'target_user_id':recovery[3]['staff1']}
    try:assert reissue(recovery,approval['recovery_id'],body=body).status_code==503
    finally:event.remove(engine,'before_cursor_execute',fail)
    with recovery[0].app_context():
        db=get_db();assert db.get(Claim,(DID,7)).enrollment_id==old['enrollment_id']
        assert db.scalar(select(Recovery)).new_enrollment_id is None
        assert db.scalar(select(func.count()).select_from(Job))==1
    assert reissue(recovery,approval['recovery_id'],body=body).status_code==200


def test_repeated_failure_chain_keeps_all_jobs_and_advances_only_claim_revision(recovery,tmp_path):
    old,approval=approved(recovery,tmp_path)
    new=reissue(recovery,approval['recovery_id']).json['data']['job']
    recovery[4][0]+=timedelta(minutes=4)
    closed=close(recovery,new,tmp_path);assert closed.exit_code==0,closed.output
    next_job=reissue(recovery,json.loads(closed.output)['recovery_id']).json['data']['job']
    assert next_job['claim_revision']=='3'
    assert count(recovery[0],Job)==3 and count(recovery[0],Recovery)==2 and count(recovery[0],Mapping)==0


def test_raw_schema_precision_fk_checks_and_missing_schema_never_runtime_ddl(recovery,tmp_path):
    old,approval=approved(recovery,tmp_path);engine=recovery[0].extensions['db_engine'];inspector=inspect(engine)
    assert inspector.get_pk_constraint('enrollment_recoveries')['constrained_columns']==['recovery_id']
    assert all(getattr(c['type'],'fsp',None)==6 for c in inspector.get_columns('enrollment_recoveries') if c['name'] in ('fenced_at','observed_at','checked_at'))
    with pytest.raises(IntegrityError):
        with recovery[0].app_context(),transaction() as db:db.scalar(select(Recovery)).slot=8
    with pytest.raises(OperationalError):
        with recovery[0].app_context(),transaction() as db:db.scalar(select(Recovery)).new_client_request_id=str(uuid4())
    RecoveryBase.metadata.drop_all(engine)
    response=reissue(recovery,approval['recovery_id'])
    assert response.status_code==503 and response.json['error']['code']=='ENROLLMENT_RECOVERY_SCHEMA_REQUIRED'
    assert not inspect(engine).has_table('enrollment_recoveries')


def test_default_off_and_parent_flags_do_not_install_recovery():
    code="""
import os,sys
os.environ['PYTHON_DOTENV_DISABLED']='1'
for key in list(os.environ):
 if key.startswith('REMOTEWORK_'):os.environ.pop(key)
from remotework import create_app
config={'SECRET_KEY':'test-'+'x'*40,'DATABASE_URL':'mysql+pymysql://unused@127.0.0.1:1/unused'}
app=create_app(config)
assert 'remotework.features.enrollment.recovery' not in sys.modules
assert not any('/recoveries/' in str(route) for route in app.url_map.iter_rules())
try:create_app(config|{'ENABLE_ENROLLMENT_RECOVERY_DRAFT':True})
except RuntimeError:pass
else:raise AssertionError('Explicit parents required')
print('PASS recovery off, no DB connection')
"""
    assert 'PASS recovery off' in subprocess.run([sys.executable,'-c',code],capture_output=True,text=True,check=True).stdout


@pytest.mark.parametrize('operator', ['staff1', 'admin2', 'missing'])
def test_operator_requires_current_admin_and_explicit_device_grant(recovery, tmp_path, operator):
    job = expired(recovery)
    response = close(recovery, job, tmp_path, operator=operator)
    assert response.exit_code != 0 and count(recovery[0], Recovery) == 0


def test_old_inactive_target_can_be_closed_but_new_target_must_be_active(recovery, tmp_path):
    old = expired(recovery)
    mutate(recovery, lambda db: setattr(db.get(User, recovery[3]['staff1']), 'is_active', False))
    response = close(recovery, old, tmp_path)
    assert response.exit_code == 0, response.output
    approval = json.loads(response.output)
    assert reissue(recovery, approval['recovery_id']).status_code == 409
    response = reissue(recovery, approval['recovery_id'], body={
        'client_request_id': str(uuid4()), 'target_user_id': recovery[3]['staff2']})
    assert response.status_code == 200


def test_close_failure_rolls_back_state_claim_evidence_and_audit(recovery, tmp_path):
    old = expired(recovery)
    engine = recovery[0].extensions['db_engine']
    def fail(connection, cursor, statement, params, context, many):
        if statement.startswith('INSERT INTO enrollment_recoveries'):
            raise OperationalError(statement, {}, RuntimeError('synthetic'))
    event.listen(engine, 'before_cursor_execute', fail)
    try:
        assert close(recovery, old, tmp_path).exit_code != 0
    finally:
        event.remove(engine, 'before_cursor_execute', fail)
    with recovery[0].app_context():
        db = get_db()
        assert db.get(Job, old['enrollment_id']).state == 'issued'
        assert db.get(Claim, (DID, 7)).state == 'reserved'
        assert db.scalar(select(func.count()).select_from(Recovery)) == 0
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == 'enrollment.operator_closed')) == 0
    assert close(recovery, old, tmp_path).exit_code == 0


@pytest.mark.parametrize('kind', ['slot_mapping', 'source_result', 'claim_pointer', 'overflow', 'stale_observation'])
def test_changed_evidence_or_claim_never_reissues(recovery, tmp_path, kind):
    old, approval = approved(recovery, tmp_path)
    def modify(db):
        if kind == 'slot_mapping':
            db.add(Mapping(mapping_id=str(uuid4()), enrollment_id=old['enrollment_id'], device_id=DID,
                user_id=recovery[3]['staff1'], slot=7, revision=1, active=False, created_at=recovery[4][0]))
        elif kind == 'source_result':
            db.get(Job, old['enrollment_id']).result_hash = '0' * 64
        elif kind == 'claim_pointer':
            db.get(Claim, (DID, 7)).state = 'active'
        elif kind == 'overflow':
            db.get(Claim, (DID, 7)).revision = wire.BIGINT_MAX
        else:
            db.get(Job, old['enrollment_id']).updated_at = recovery[4][0] + timedelta(seconds=1)
    mutate(recovery, modify)
    assert reissue(recovery, approval['recovery_id']).status_code == 409
    assert count(recovery[0], Job) == 1


@pytest.mark.parametrize('body', [{}, {'client_request_id':'bad','target_user_id':1},
    {'client_request_id':'123e4567-e89b-42d3-a456-426614174000','target_user_id':True},
    {'client_request_id':'123e4567-e89b-42d3-a456-426614174000','target_user_id':2147483648},
    {'client_request_id':'123e4567-e89b-42d3-a456-426614174000','target_user_id':'1'},
    {'client_request_id':'123e4567-e89b-42d3-a456-426614174000','target_user_id':1,'slot_empty':True}])
def test_http_cannot_attest_or_use_noncanonical_input(recovery, tmp_path, body):
    _, approval = approved(recovery, tmp_path)
    assert reissue(recovery, approval['recovery_id'], body=body).status_code == 400
    assert count(recovery[0], Job) == 1


def test_private_handoff_hash_is_distinct_from_original_source_hash(recovery, tmp_path):
    old = expired(recovery)
    response = close(recovery, old, tmp_path, source_report_sha256='a'*64)
    assert response.exit_code == 0
    recovery_id = json.loads(response.output)['recovery_id']
    with recovery[0].app_context():
        value = get_db().get(Recovery, recovery_id)
        assert value.report_sha256 != value.source_report_sha256 == 'a'*64
    status = recovery[1].get(ROOT+'/recoveries/'+recovery_id, headers=recovery[2])
    assert status.status_code == 200
    assert not any(key in status.json['data']['recovery'] for key in ('report_sha256','source_report_sha256','reissue_session_hash'))


def test_reissued_job_is_mysql_only_even_before_intent_when_options_are_off(recovery, tmp_path):
    from test_enrollment_durability_review import cold_off
    _, approval = approved(recovery, tmp_path)
    job = reissue(recovery, approval['recovery_id']).json['data']['job']
    legacy = create_app(dict(recovery[0].config) | {
        'ENABLE_ENROLLMENT_DURABILITY_DRAFT':False, 'ENABLE_ENROLLMENT_RECOVERY_DRAFT':False})
    old = (legacy, legacy.test_client(), *recovery[2:])
    try:
        assert post(old, job, '/approval', intent_body(job) | {'scope':'sensor_write'}).status_code == 409
        assert complete(old, job, result(job)).status_code == 409
        assert cold_off(recovery, job, result(job)) == {'write':409, 'complete':409}
        assert count(recovery[0], Mapping) == 0
    finally:
        legacy.extensions['db_engine'].dispose()


def test_real_dump_restore_preserves_old_job_chain_and_microsecond_evidence(recovery, tmp_path, owned_databases):
    old, approval = approved(recovery, tmp_path)
    new = reissue(recovery, approval['recovery_id']).json['data']['job']
    source = recovery[0].extensions['db_engine']
    restored = owned_databases('explicit_recovery')
    config, dump = tmp_path / 'mysql.private.cnf', tmp_path / 'recovery.private.sql'
    _private_file(config); _private_file(dump)
    quote = lambda v: '"' + str(v).replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'
    config.write_text('[client]\nprotocol=TCP\nhost='+quote(source.url.host)+'\nport='+str(source.url.port)+
        '\nuser='+quote(source.url.username)+'\npassword='+quote(source.url.password)+'\n', encoding='utf8')
    try:
        with dump.open('wb') as output:
            backup = subprocess.run([_mysql_tool('mysqldump'), '--defaults-extra-file='+config.name,
                '--single-transaction','--skip-lock-tables','--no-tablespaces','--hex-blob',
                '--set-gtid-purged=OFF','--column-statistics=0',source.url.database], cwd=tmp_path,
                stdout=output, stderr=subprocess.PIPE)
        assert backup.returncode == 0, 'Owned dump failed; private output omitted'
        with dump.open('rb') as content:
            restore = subprocess.run([_mysql_tool('mysql'),'--defaults-extra-file='+config.name,
                restored.url.database], cwd=tmp_path, stdin=content, capture_output=True)
        assert restore.returncode == 0, 'Owned restore failed; private output omitted'
        with restored.connect() as db:
            value = db.exec_driver_sql('SELECT source_enrollment_id,new_enrollment_id,observed_at,fenced_at FROM enrollment_recoveries').one()
            assert value[0] == old['enrollment_id'] and value[1] == new['enrollment_id']
            assert (value[2]-value[3]).total_seconds() == .171103
            assert db.exec_driver_sql('SELECT COUNT(*) FROM enrollment_jobs').scalar_one() == 2
            assert db.exec_driver_sql('SELECT revision FROM enrollment_claims').scalar_one() == 2
            assert db.exec_driver_sql('SELECT COUNT(*) FROM enrollment_mappings').scalar_one() == 0
            assert db.exec_driver_sql('SELECT version FROM schema_versions').scalars().all() == [1]
        assert len(inspect(restored).get_foreign_keys('enrollment_recoveries')) == 3
    finally:
        config.unlink(missing_ok=True); dump.unlink(missing_ok=True)
