"""MySQL-only durability in owned fixtures; no sensor or application DB writes."""
import hashlib
import subprocess
import sys
from datetime import timedelta
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor

import pytest
from sqlalchemy import delete, func, inspect, select
from sqlalchemy.exc import IntegrityError, OperationalError

from remotework import create_app
from remotework.db import get_db, transaction
from remotework.models import AuditEvent, AuthSession, SchemaVersion, ResourceGrant
from remotework.features.enrollment.durability import DurabilityBase, PhysicalReceipt
from remotework.features.enrollment.models import Mapping, Job, Claim
from remotework import enrollment_protocol_draft as wire, enrollment_approval_draft as approval
from test_device_enrollment import env, issue, result, complete, mutate, DID, KEYS, ROOT
from test_task_migration_draft import owned_databases, _mysql_tool, _private_file


@pytest.fixture
def durable(env):
    app = create_app(dict(env[0].config) | {"ENABLE_ENROLLMENT_DURABILITY_DRAFT": True})
    engine = app.extensions["db_engine"]
    assert engine.url.database.startswith("remote_work_test_")
    DurabilityBase.metadata.create_all(engine)
    value = (app, app.test_client(), *env[2:])
    yield value
    if inspect(engine).has_table(PhysicalReceipt.__tablename__):
        with app.app_context(), transaction() as db:
            db.execute(delete(PhysicalReceipt))
    DurabilityBase.metadata.drop_all(engine)
    engine.dispose()


def intent_body(job):
    return {f: job[f] for f in wire.COMMON - {"enrollment_id"}} | dict(scope="sensor_store",
        ticket_digest=wire.ticket_digest(wire.b64decode(job["request_envelope"])), probe_nonce=wire.b64encode(b"q" * 32))


def post(e, job, suffix, body):
    return e[1].post(ROOT + "/" + job["enrollment_id"] + suffix, json=body, headers=e[2])


def report(e, job, raw):
    return post(e, job, "/physical-result", {"operation_revision": job["operation_revision"], "result_envelope": raw})


def test_first_intent_commit_single_use_and_exact_result_then_mapping(durable):
    app, client, headers, *_ = durable
    job, _ = issue(durable)
    body = intent_body(job)
    first = post(durable, job, "/storage-intent", body)
    assert first.status_code == 200 and first.json["data"]["may_store"] is True
    permit = wire.b64decode(first.json["data"]["approval_envelope"])
    approval.verify(permit, expected={"enrollment_id": job["enrollment_id"]} | body,
                    device_id=DID, key_id=KEYS["key_id"], key=KEYS["request_key"])
    # A fresh probe receiver accepts only its pending, one-use scope/binding.
    again = post(durable, job, "/storage-intent", body)
    assert again.status_code == 200 and again.json["data"]["may_store"] is False
    assert again.json["data"]["approval_envelope"] is None
    changed = post(durable, job, "/storage-intent", body | {"probe_nonce": wire.b64encode(b"r" * 32)})
    assert changed.status_code == 409
    raw = result(job)
    assert complete(durable, job, raw).status_code == 409
    stored = report(durable, job, raw)
    assert stored.status_code == 200 and stored.json["data"]["result_envelope"] == raw
    assert report(durable, job, raw).json["data"] == stored.json["data"]
    with app.app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(Mapping)) == 0
        assert db.scalar(select(PhysicalReceipt)).result_envelope == wire.b64decode(raw)
    assert complete(durable, job, raw).json["data"]["state"] == "completed"
    assert complete(durable, job, raw).status_code == 200
    with app.app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(Mapping)) == 1
        for action in ("enrollment.store_intent", "enrollment.physical_result_recorded", "enrollment.completed"):
            assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == action)) == 1
        assert list(db.scalars(select(SchemaVersion.version))) == [1]
    assert report(durable, job, result(job)).status_code == 409
    read = client.get(ROOT + "/" + job["enrollment_id"] + "/physical-result", headers=headers)
    assert read.status_code == 200 and read.json["data"]["result_envelope"] == raw


def test_late_result_preserved_without_activating_mapping_or_renewing_write(durable):
    job, _ = issue(durable)
    body = intent_body(job)
    assert post(durable, job, "/storage-intent", body).status_code == 200
    durable[4][0] += timedelta(minutes=10)
    raw = result(job)
    assert report(durable, job, raw).status_code == 200
    assert complete(durable, job, raw).status_code == 409
    repeat = post(durable, job, "/storage-intent", body)
    assert repeat.status_code == 200 and not repeat.json["data"]["may_store"]
    with durable[0].app_context():
        assert get_db().scalar(select(func.count()).select_from(Mapping)) == 0
    durable[4][0] += timedelta(days=2)
    assert report(durable, job, raw).status_code == 410


def test_capture_store_and_legacy_scopes_are_separated(durable):
    job, _ = issue(durable)
    body = intent_body(job)
    assert post(durable, job, "/approval", body | {"scope": "sensor_write"}).status_code == 409
    assert post(durable, job, "/approval", body).status_code == 400  # No unrecorded store permit.
    assert post(durable, job, "/approval", body | {"scope": "sensor_capture"}).status_code == 200
    assert post(durable, job, "/storage-intent", body).status_code == 200
    assert post(durable, job, "/approval", body | {"scope": "sensor_capture"}).status_code == 409
    assert post(durable, job, "/approval", body | {"scope": "result_read"}).status_code == 200


def test_no_intent_success_rejected_but_pre_store_failure_is_preserved(durable):
    job, _ = issue(durable)
    assert report(durable, job, result(job)).status_code == 409
    raw = result(job, outcome="failed", failure="duplicate_finger")
    assert report(durable, job, raw).json["data"]["failure_code"] == "duplicate_finger"
    assert complete(durable, job, raw).json["data"]["state"] == "failed"
    with durable[0].app_context():
        db = get_db()
        assert db.scalar(select(Claim)).state == "quarantined"
        assert db.scalar(select(func.count()).select_from(Mapping)) == 0


def test_previously_committed_legacy_success_still_replays_without_new_mapping(durable):
    job, _ = issue(durable)
    raw = result(job)
    legacy = create_app(dict(durable[0].config) | {"ENABLE_ENROLLMENT_DURABILITY_DRAFT": False})
    try:
        old = (legacy, legacy.test_client(), *durable[2:])
        first = complete(old, job, raw)
        assert first.status_code == 200
        replay = complete(durable, job, raw)
        assert replay.status_code == 200 and replay.json["data"] == first.json["data"]
        with durable[0].app_context():
            assert get_db().scalar(select(func.count()).select_from(Mapping)) == 1
            assert get_db().scalar(select(func.count()).select_from(PhysicalReceipt)) == 0
    finally:
        legacy.extensions["db_engine"].dispose()


@pytest.mark.parametrize("revoked", ["grant", "session", "epoch"])
def test_current_authority_rechecked_for_record_and_read(durable, revoked):
    job, _ = issue(durable)
    raw = result(job, outcome="failed", failure="duplicate_finger")
    assert report(durable, job, raw).status_code == 200
    def revoke(db):
        if revoked == "grant":
            db.execute(delete(ResourceGrant))
        elif revoked == "session":
            db.execute(delete(AuthSession))
        else:
            from remotework.features.enrollment.models import Device
            db.get(Device, DID).key_epoch += 1
    mutate(durable, revoke)
    assert report(durable, job, raw).status_code in (401, 403, 503)
    assert durable[1].get(ROOT + "/" + job["enrollment_id"] + "/physical-result", headers=durable[2]).status_code in (401, 403, 503)


def test_operator_import_after_session_ended_keeps_legacy_evidence_without_mapping(durable, tmp_path):
    job, _ = issue(durable)
    raw = result(job)
    mutate(durable, lambda db: db.execute(delete(AuthSession)))
    evidence = tmp_path / "signed.private.json"
    evidence.write_bytes(wire.b64decode(raw))
    args = ["archive-enrollment-result", "--enrollment-id", job["enrollment_id"],
            "--login-id", "admin1", "--result-file", str(evidence)]
    runner = durable[0].test_cli_runner()
    assert runner.invoke(args=args).exit_code == 0
    assert runner.invoke(args=args).exit_code == 0
    assert runner.invoke(args=args[:-3] + ["staff1", "--result-file", str(evidence)]).exit_code != 0
    with durable[0].app_context():
        db = get_db()
        assert db.scalar(select(PhysicalReceipt)).intent_at is None
        assert db.scalar(select(Job)).state == "issued"
        assert db.scalar(select(Claim)).state == "reserved"
        assert db.scalar(select(func.count()).select_from(Mapping)) == 0
    # A new same-user session is never an automatic job takeover.
    headers = durable[5]()
    assert durable[1].get(ROOT + "/" + job["enrollment_id"] + "/physical-result", headers=headers).status_code == 403


def test_database_corruption_is_not_returned_or_overwritten(durable):
    job, _ = issue(durable)
    raw = result(job, outcome="failed", failure="duplicate_finger")
    assert report(durable, job, raw).status_code == 200
    mutate(durable, lambda db: setattr(db.get(PhysicalReceipt, job["enrollment_id"]), "result_envelope", b"corrupt"))
    assert report(durable, job, raw).status_code == 409
    assert durable[1].get(ROOT + "/" + job["enrollment_id"] + "/physical-result", headers=durable[2]).status_code == 409


def test_two_intents_issue_only_one_store_permit(durable):
    job, _ = issue(durable)
    def run():
        with durable[0].test_client() as client:
            r = client.post(ROOT + "/" + job["enrollment_id"] + "/storage-intent", headers=durable[2], json=intent_body(job))
            assert r.status_code == 200
            return r.json["data"]["may_store"]
    with ThreadPoolExecutor(max_workers=2) as pool:
        assert sorted(pool.map(lambda _: run(), range(2))) == [False, True]


def test_raw010_and_missing_schema_never_auto_create(durable):
    engine = durable[0].extensions["db_engine"]
    DurabilityBase.metadata.drop_all(engine)
    job, _ = issue(durable)
    assert post(durable, job, "/storage-intent", intent_body(job)).status_code == 503
    assert not inspect(engine).has_table(PhysicalReceipt.__tablename__)
    sql = (Path(__file__).resolve().parents[1] / "migrations/010_enrollment_durability.mysql.sql").read_text(encoding="utf8")
    sql = "\n".join(line for line in sql.splitlines() if not line.lstrip().startswith("--"))
    with engine.begin() as c:
        for statement in sql.split(";"):
            if statement.strip(): c.exec_driver_sql(statement)
    assert post(durable, job, "/storage-intent", intent_body(job)).status_code == 200
    options = inspect(engine).get_table_options(PhysicalReceipt.__tablename__)
    assert options["mysql_engine"] == "InnoDB" and options["mysql_collate"] == "utf8mb4_unicode_ci"


def test_receipt_insert_failure_does_not_return_store_permission_or_commit_audit(durable):
    from sqlalchemy import event
    job, _ = issue(durable)
    engine = durable[0].extensions["db_engine"]
    def fail(conn, cursor, statement, parameters, context, executemany):
        if "insert into enrollment_physical_receipts" in statement.lower():
            raise RuntimeError("Synthetic durability insertion failure")
    event.listen(engine, "before_cursor_execute", fail)
    try:
        response = post(durable, job, "/storage-intent", intent_body(job))
        assert response.status_code == 500 and "approval_envelope" not in str(response.json)
    finally:
        event.remove(engine, "before_cursor_execute", fail)
    with durable[0].app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(PhysicalReceipt)) == 0
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "enrollment.store_intent")) == 0
    assert post(durable, job, "/storage-intent", intent_body(job)).status_code == 200


def test_signed_result_bytes_survive_real_dump_restore(durable, owned_databases, tmp_path):
    app = durable[0]; source = app.extensions["db_engine"]
    job, _ = issue(durable)
    raw = result(job, outcome="failed", failure="duplicate_finger")
    assert report(durable, job, raw).status_code == 200
    restored = owned_databases("physical_recovery")
    config, dump = tmp_path / "mysql.private.cnf", tmp_path / "physical.private.sql"
    _private_file(config); _private_file(dump)
    quote = lambda v: '"' + str(v).replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'
    config.write_text("[client]\nprotocol=TCP\nhost=" + quote(source.url.host) + "\nport=" + str(source.url.port) +
        "\nuser=" + quote(source.url.username) + "\npassword=" + quote(source.url.password) + "\n", encoding="utf8")
    try:
        with dump.open("wb") as output:
            backup = subprocess.run([_mysql_tool("mysqldump"), "--defaults-extra-file=" + config.name,
                "--single-transaction", "--skip-lock-tables", "--no-tablespaces", "--hex-blob",
                "--set-gtid-purged=OFF", "--column-statistics=0", source.url.database], cwd=tmp_path,
                stdout=output, stderr=subprocess.PIPE)
        assert backup.returncode == 0, "Owned dump failed; private output omitted"
        with dump.open("rb") as content:
            restore = subprocess.run([_mysql_tool("mysql"), "--defaults-extra-file=" + config.name,
                restored.url.database], cwd=tmp_path, stdin=content, capture_output=True)
        assert restore.returncode == 0, "Owned restore failed; private output omitted"
        with restored.connect() as c:
            value = c.exec_driver_sql("SELECT result_envelope, result_hash, result_id FROM enrollment_physical_receipts").one()
            assert value[0] == wire.b64decode(raw) and hashlib.sha256(value[0]).hexdigest() == value[1]
            assert c.exec_driver_sql("SELECT COUNT(*) FROM enrollment_mappings").scalar_one() == 0
            assert c.exec_driver_sql("SELECT version FROM schema_versions").scalars().all() == [1]
        assert inspect(restored).get_pk_constraint("enrollment_physical_receipts")["constrained_columns"] == ["enrollment_id"]
    finally:
        config.unlink(missing_ok=True); dump.unlink(missing_ok=True)


def test_physical_fk_check_and_result_id_unique_are_enforced(durable):
    job, _ = issue(durable)
    raw = result(job, outcome="failed", failure="duplicate_finger")
    saved = report(durable, job, raw).json["data"]
    with pytest.raises(IntegrityError) as fk:
        with durable[0].app_context(), transaction() as db:
            db.get(PhysicalReceipt, job["enrollment_id"]).reserved_slot = 8
    assert fk.value.orig.args[0] == 1452
    with pytest.raises(OperationalError) as check:
        with durable[0].app_context(), transaction() as db:
            db.get(PhysicalReceipt, job["enrollment_id"]).outcome = None
    assert check.value.orig.args[0] == 3819
    second, _ = issue(durable, target_user_id=durable[3]["staff2"])
    repeated_id = result(second, outcome="failed", failure="duplicate_finger", result_id=saved["result_id"])
    assert report(durable, second, repeated_id).status_code == 409
    assert report(durable, second, result(second, outcome="failed", failure="duplicate_finger")).status_code == 200


def test_durability_cold_default_has_no_module_or_ddl_registration():
    code = """
import os,sys,json
os.environ['PYTHON_DOTENV_DISABLED']='1'
for key in list(os.environ):
    if key.startswith('REMOTEWORK_'):os.environ.pop(key)
from remotework import create_app
app=create_app({'TESTING':True,'SECRET_KEY':'cold-'+'q'*40,'DATABASE_URL':'mysql+pymysql://unused@127.0.0.1:1/unused',
 'ENABLE_DEVICE_ENROLLMENT_DRAFT':True,'ENABLE_ENROLLMENT_DURABILITY_DRAFT':False})
assert 'remotework.features.enrollment.durability' not in sys.modules
assert not any('physical-result' in str(r) or 'storage-intent' in str(r) for r in app.url_map.iter_rules())
assert 'init-enrollment-durability-draft' not in app.cli.commands
try:
 create_app({'SECRET_KEY':'cold-'+'q'*40,'DATABASE_URL':'mysql+pymysql://unused@127.0.0.1:1/unused',
  'ENABLE_DEVICE_ENROLLMENT_DRAFT':False,'ENABLE_ENROLLMENT_DURABILITY_DRAFT':True})
except RuntimeError:pass
else:raise AssertionError('Parent feature must be explicitly enabled')
print('PASS cold durability off, no DB connection/DDL')
"""
    child = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, check=True)
    assert "PASS cold" in child.stdout
