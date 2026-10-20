"""Real isolated MySQL tests. Never use the operator's application database."""
import json
import os
from datetime import datetime, timedelta
from pathlib import Path
from uuid import uuid4
from concurrent.futures import ThreadPoolExecutor

import pytest
from sqlalchemy import delete, func, inspect, select, text
from sqlalchemy.engine import make_url
from sqlalchemy.exc import IntegrityError, OperationalError
from test_task_migration_draft import owned_databases, _mysql_tool, _private_file

from remotework import create_app
from remotework.auth import create_user
from remotework.common import grant_resource
from remotework.db import get_db, init_schema, transaction
from remotework.models import Base, User, ResourceGrant, AuditEvent, AuthSession, SchemaVersion
from remotework.features.enrollment.models import EnrollmentBase, Device, Job, Claim, Mapping
from remotework import enrollment_protocol_draft as wire
from remotework import enrollment_approval_draft as approval

DID = "123e4567-e89b-42d3-a456-426614174000"
KEYS = {"key_id": "test-public-key", "key_epoch": 1, "request_key": bytes(range(32)), "result_key": bytes(range(32, 64))}
ROOT = "/api/v1/device-enrollments"


@pytest.fixture
def env():
    url = os.getenv("MYSQL_TEST_URL")
    if not url:
        pytest.skip("Owned MySQL runner required")
    assert make_url(url).drivername == "mysql+pymysql"
    assert make_url(url).database.startswith("remote_work_test_")
    clock = [datetime(2026, 10, 20, 1, 0, 0)]
    app = create_app({"TESTING": True, "DATABASE_URL": url, "SECRET_KEY": "test-enrollment-" + "a" * 32,
        "ENABLE_DEVICE_ENROLLMENT_DRAFT": True, "ENABLE_TASK_QUERY_DRAFT": False, "ENABLE_COMMON_WRITE_DRAFT": False,
        "ENABLE_CHAT_DRAFT": False, "ENABLE_NOTICE_DRAFT": False, "ENROLLMENT_DEVICE_KEYS": {DID: KEYS.copy()},
        "ENROLLMENT_CLOCK": lambda: clock[0]})
    with app.app_context():
        init_schema()
        EnrollmentBase.metadata.create_all(app.extensions["db_engine"])
        with transaction() as db:
            for t in reversed(EnrollmentBase.metadata.sorted_tables):
                db.execute(delete(t))
            for t in reversed(Base.metadata.sorted_tables):
                if t.name not in ("admin_guard", "schema_versions"):
                    db.execute(delete(t))
            ids = {}
            for name, role in (("admin1", "admin"), ("admin2", "admin"), ("staff1", "employee"), ("staff2", "employee")):
                user = create_user(db, {"login_id": name, "password": "Enrollment-test-2026!", "display_name": name,
                    "employee_no": name, "department": "QA", "role": role})
                ids[name] = user.id
            db.add(Device(device_id=DID, display_name="QA sensor", enabled=True, key_id=KEYS["key_id"], key_epoch=1,
                          minimum_slot=0, maximum_slot=299, capacity=300, candidate_slots_json="[7,8]"))
            grant_resource(db, ids["admin1"], "enrollment_device", DID)
    client = app.test_client()
    def login(name="admin1"):
        r = client.post("/api/v1/auth/login", json={"login_id": name, "password": "Enrollment-test-2026!"})
        assert r.status_code == 200
        return {"Authorization": "Bearer " + r.json["data"]["access_token"]}
    headers = login()
    yield app, client, headers, ids, clock, login
    with app.app_context(), transaction() as db:
        for t in reversed(EnrollmentBase.metadata.sorted_tables):
            db.execute(delete(t))
    app.extensions["db_engine"].dispose()


def issue(env, **changes):
    app, client, headers, ids, *_ = env
    body = dict(client_request_id=str(uuid4()), target_user_id=ids["staff1"], device_id=DID) | changes
    r = client.post(ROOT, headers=headers, json=body)
    assert r.status_code == 200, r.json
    return r.json["data"], body


def result(job, outcome="sensor_stored", failure=None, **changes):
    value = {f: job[f] for f in wire.COMMON} | dict(ticket_digest=wire.ticket_digest(wire.b64decode(job["request_envelope"])),
        result_id=str(uuid4()), outcome=outcome, failure_code=failure, elapsed_ms=1234) | changes
    return wire.b64encode(wire.sign_payload(json.dumps(value, separators=(",", ":")).encode(), kind=wire.RESULT,
        device_id=DID, key_id=KEYS["key_id"], key=KEYS["result_key"]))


def complete(env, job, raw):
    return env[1].post(ROOT + "/" + job["enrollment_id"] + "/complete", headers=env[2],
                       json={"operation_revision": job["operation_revision"], "result_envelope": raw})


def count(app, model):
    with app.app_context():
        return get_db().scalar(select(func.count()).select_from(model))


def mutate(env, operation):
    with env[0].app_context(), transaction() as db:
        operation(db)


def test_commit_and_identical_lost_response_replay(env):
    app, client, headers, ids, clock, _ = env
    job, body = issue(env)
    same = client.post(ROOT, json=body, headers=headers).json["data"]
    assert same == job
    assert client.get(ROOT + "/by-request/" + body["client_request_id"], headers=headers).json["data"] == job
    raw = result(job)
    first = complete(env, job, raw)
    assert first.status_code == 200 and first.json["data"]["state"] == "completed"
    clock[0] += timedelta(minutes=10)  # Original ticket expired, committed receipt still replays.
    read_body = {f: job[f] for f in wire.COMMON - {"enrollment_id"}} | dict(
        ticket_digest=wire.ticket_digest(wire.b64decode(job["request_envelope"])),
        probe_nonce=wire.b64encode(os.urandom(32)), scope="result_read")
    approval_path = ROOT + "/" + job["enrollment_id"] + "/approval"
    assert client.post(approval_path, headers=headers, json=read_body).status_code == 200
    assert client.post(approval_path, headers=headers, json=read_body | {"scope": "sensor_write"}).status_code == 409
    assert complete(env, job, raw).json["data"] == first.json["data"]
    assert count(app, Job) == count(app, Mapping) == count(app, Claim) == 1
    with app.app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "enrollment.completed")) == 1
        mapping = db.scalar(select(Mapping))
        assert mapping.user_id == ids["staff1"] and mapping.slot == 7 and mapping.active
        assert list(db.scalars(select(SchemaVersion.version))) == [1]
        assert db.scalar(select(Job)).result_json.find("nonce") == -1
    assert complete(env, job, result(job)).status_code == 409
    assert client.post(ROOT, headers=headers, json=body | {"target_user_id": ids["staff2"]}).status_code == 409
    assert client.post(ROOT, headers=headers, json=body | {"client_request_id": str(uuid4())}).status_code == 409


@pytest.mark.parametrize("change,status", [("grant", 403), ("role", 403), ("target", 409), ("device", 409), ("session", 401)])
def test_recheck_current_authority_before_commit(env, change, status):
    app, _, _, ids, *_ = env
    job, _ = issue(env)
    def revoke(db):
        if change == "grant":
            db.execute(delete(ResourceGrant))
        elif change == "role":
            db.get(User, ids["admin1"]).role = "employee"
        elif change == "target":
            db.get(User, ids["staff1"]).is_active = False
        elif change == "device":
            db.get(Device, DID).enabled = False
        else:
            db.execute(delete(AuthSession))
    mutate(env, revoke)
    assert complete(env, job, result(job)).status_code == status
    assert count(app, Mapping) == 0


def test_other_admin_and_new_session_cannot_take_over(env):
    _, client, _, _, _, login = env
    job, _ = issue(env)
    for name in ("admin2", "admin1"):
        assert client.get(ROOT + "/" + job["enrollment_id"], headers=login(name)).status_code == 403


@pytest.mark.parametrize("outcome,failure", [("failed", "timeout"), ("recovery_required", "storage_uncertain")])
def test_failure_quarantines_without_mapping_and_replays(env, outcome, failure):
    job, _ = issue(env)
    raw = result(job, outcome, failure)
    assert complete(env, job, raw).json["data"]["state"] == outcome
    assert complete(env, job, raw).status_code == 200
    assert count(env[0], Mapping) == 0
    with env[0].app_context():
        assert get_db().scalar(select(Claim)).state == "quarantined"


def test_cancel_and_expiry_never_reclaim_or_activate(env):
    app, client, headers, ids, clock, _ = env
    job, _ = issue(env)
    endpoint = ROOT + "/" + job["enrollment_id"] + "/cancel"
    for _ in range(2):
        assert client.post(endpoint, headers=headers, json={"operation_revision": "1"}).status_code == 200
    assert complete(env, job, result(job)).status_code == 409
    second, _ = issue(env, target_user_id=ids["staff2"])
    assert second["reserved_slot"] == 8
    clock[0] += timedelta(seconds=180)
    assert complete(env, second, result(second)).status_code == 409
    r = client.get(ROOT + "/" + second["enrollment_id"], headers=headers)
    assert r.json["data"]["state"] == "expired" and r.json["data"]["request_envelope"] is None
    assert client.post(ROOT, headers=headers, json=dict(client_request_id=str(uuid4()), target_user_id=ids["staff2"], device_id=DID)).status_code == 409
    assert count(app, Mapping) == 0 and count(app, Claim) == 2
    clock[0] += timedelta(days=1)
    assert client.get(ROOT + "/" + second["enrollment_id"], headers=headers).status_code == 410
    assert count(app, Claim) == 2


@pytest.mark.parametrize("change", ["mac", "binding", "epoch", "revision", "unknown", "duplicate"])
def test_invalid_input_does_not_consume_job(env, change):
    job, _ = issue(env)
    raw = result(job)
    body = {"operation_revision": "1", "result_envelope": raw}
    if change == "mac":
        e = json.loads(wire.b64decode(raw)); e["mac_b64"] = wire.b64encode(bytes(32))
        body["result_envelope"] = wire.b64encode(json.dumps(e).encode())
    elif change == "binding":
        body["result_envelope"] = result(job, reserved_slot=8)
    elif change == "epoch":
        body["result_envelope"] = result(job, key_epoch="2")
    elif change == "revision":
        body["operation_revision"] = 1
    elif change == "unknown":
        body["other"] = 1
    if change == "duplicate":
        raw_body = '{"operation_revision":"2","operation_revision":"1","result_envelope":"' + raw + '"}'
        response = env[1].post(ROOT + "/" + job["enrollment_id"] + "/complete", headers=env[2], data=raw_body, content_type="application/json")
    else:
        response = complete(env, job, body["result_envelope"]) if change not in ("revision", "unknown") else env[1].post(
            ROOT + "/" + job["enrollment_id"] + "/complete", headers=env[2], json=body)
    assert response.status_code == 400, response.json
    assert complete(env, job, raw).status_code == 200
    assert count(env[0], Mapping) == 1


def test_missing_grant_and_staff_cannot_issue(env):
    _, client, headers, ids, _, login = env
    request = dict(client_request_id=str(uuid4()), target_user_id=ids["staff1"], device_id=DID)
    assert client.post(ROOT, json=request).status_code == 401
    assert client.post(ROOT, json=request, headers=login("staff1")).status_code == 403
    assert client.post(ROOT, json=request, headers=login("admin2")).status_code == 403
    assert client.get(ROOT + "/eligible-devices?target_user_id=" + str(ids["staff1"]), headers=headers).json["data"]["devices"][0]["device_id"] == DID
    assert client.get(ROOT + "/eligible-devices?target_user_id=01", headers=headers).status_code == 400
    assert client.post(ROOT, json=request | {"target_user_id": True}, headers=headers).status_code == 400


def test_fresh_probe_approval_scopes_expiry_and_revocation(env):
    app, client, headers, ids, clock, _ = env
    job, _ = issue(env)
    expected = {f: job[f] for f in wire.COMMON} | dict(ticket_digest=wire.ticket_digest(wire.b64decode(job["request_envelope"])),
        probe_nonce=wire.b64encode(os.urandom(32)), scope="sensor_write")
    body = {k: v for k, v in expected.items() if k != "enrollment_id"}
    path = ROOT + "/" + job["enrollment_id"] + "/approval"
    response = client.post(path, headers=headers, json=body)
    assert response.status_code == 200, response.json
    raw = wire.b64decode(response.json["data"]["approval_envelope"])
    value = approval.verify(raw, expected=expected, device_id=DID, key_id=KEYS["key_id"], key=KEYS["request_key"])
    permit = approval.FreshApproval.accept(value, sent_mono=10, received_mono=10.3)
    assert permit.allows("sensor_write", 10.4) and not permit.allows("result_read", 10.4)
    with pytest.raises(wire.ProtocolError):
        approval.verify(raw, expected=expected | {"probe_nonce": wire.b64encode(os.urandom(32))}, device_id=DID,
                        key_id=KEYS["key_id"], key=KEYS["request_key"])
    clock[0] += timedelta(seconds=180)
    assert client.post(path, headers=headers, json=body).status_code == 409
    assert client.post(path, headers=headers, json=body | {"scope": "result_read"}).status_code == 200
    mutate(env, lambda db: db.execute(delete(ResourceGrant)))
    assert client.post(path, headers=headers, json=body | {"scope": "result_read"}).status_code == 403


def test_atomic_mapping_receipt_audit_rollback(env, monkeypatch):
    from remotework.features.enrollment import service
    job, _ = issue(env)
    original = service.audit
    def broken(*args, **kwargs):
        raise RuntimeError("test interrupted before commit")
    monkeypatch.setattr(service, "audit", broken)
    raw = result(job)
    assert complete(env, job, raw).status_code == 500
    assert count(env[0], Mapping) == 0
    with env[0].app_context():
        assert get_db().scalar(select(Job)).state == "issued"
        assert get_db().scalar(select(Claim)).state == "reserved"
    monkeypatch.setattr(service, "audit", original)
    assert complete(env, job, raw).status_code == 200


def test_parallel_duplicate_issue_and_complete_one_effect(env):
    app, _, headers, ids, *_ = env
    request = dict(client_request_id=str(uuid4()), target_user_id=ids["staff1"], device_id=DID)
    def send(_):
        with app.test_client() as c:
            return c.post(ROOT, headers=headers, json=request)
    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(send, range(2)))
    assert [r.status_code for r in responses] == [200, 200]
    jobs = [r.json["data"] for r in responses]
    assert jobs[0] == jobs[1] and count(app, Claim) == 1
    raw = result(jobs[0])
    def finish(_):
        with app.test_client() as c:
            return c.post(ROOT + "/" + jobs[0]["enrollment_id"] + "/complete", headers=headers,
                          json={"operation_revision": "1", "result_envelope": raw})
    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(finish, range(2)))
    assert [r.status_code for r in responses] == [200, 200]
    assert count(app, Mapping) == count(app, Job) == 1


def test_explicit_cli_profile_grant_and_raw_sql(env):
    app, *_ = env
    engine = app.extensions["db_engine"]
    with app.app_context(), transaction() as db:
        for t in reversed(EnrollmentBase.metadata.sorted_tables):
            db.execute(delete(t))
    EnrollmentBase.metadata.drop_all(engine)
    ddl = (Path(__file__).parents[1] / "migrations/007_device_enrollment.mysql.sql").read_text(encoding="utf-8")
    with engine.begin() as conn:
        for statement in ddl.split(";"):
            if statement.strip():
                conn.execute(text(statement))
    assert set(EnrollmentBase.metadata.tables) <= set(inspect(engine).get_table_names())
    for table in EnrollmentBase.metadata.tables:
        assert inspect(engine).get_table_options(table)["mysql_engine"].lower() == "innodb"
    assert any(fk["constrained_columns"] == ["enrollment_id", "device_id", "slot", "user_id"]
               for fk in inspect(engine).get_foreign_keys("enrollment_mappings"))
    assert str(next(c["type"] for c in inspect(engine).get_columns("enrollment_jobs") if c["name"] == "target_user_id")) == "INTEGER"
    runner = app.test_cli_runner()
    args = ["add-enrollment-device", "--device-id", DID, "--display-name", "QA", "--minimum-slot", "0",
            "--maximum-slot", "299", "--capacity", "300", "--candidate-slots", "7,8"]
    assert runner.invoke(args=args).exit_code == 0
    assert runner.invoke(args=args).exit_code != 0  # No overwrite existing templates/profile.
    assert runner.invoke(args=["grant-enrollment-device", "--device-id", DID, "--login-id", "staff1"]).exit_code != 0
    assert runner.invoke(args=["grant-enrollment-device", "--device-id", DID, "--login-id", "admin2"]).exit_code == 0
    job, _ = issue(env)
    assert complete(env, job, result(job)).status_code == 200
    with pytest.raises(IntegrityError), engine.begin() as conn:
        conn.exec_driver_sql("UPDATE enrollment_mappings SET user_id=%s", (env[3]["staff2"],))
    with pytest.raises(OperationalError), engine.begin() as conn:
        conn.exec_driver_sql("UPDATE enrollment_jobs SET state='COMPLETED'")


def test_feature_off_and_missing_keys_fail_closed(env):
    app, client, headers, ids, *_ = env
    app.config["ENROLLMENT_DEVICE_KEYS"] = {}
    assert client.post(ROOT, headers=headers, json=dict(client_request_id=str(uuid4()), target_user_id=ids["staff1"], device_id=DID)).status_code == 503
    off = create_app({"TESTING": True, "SECRET_KEY": "off-test-" + "a" * 40, "DATABASE_URL": app.config["DATABASE_URL"],
        "ENABLE_DEVICE_ENROLLMENT_DRAFT": False, "ENABLE_CHAT_DRAFT": False, "ENABLE_NOTICE_DRAFT": False,
        "ENABLE_TASK_QUERY_DRAFT": False, "ENABLE_COMMON_WRITE_DRAFT": False})
    assert off.test_client().post(ROOT).status_code == 404
    assert not any(n.startswith("enrollment_") for n in Base.metadata.tables)
    engine = app.extensions["db_engine"]
    EnrollmentBase.metadata.drop_all(engine)
    try:
        assert client.get(ROOT + "/" + str(uuid4()), headers=headers).status_code == 503
        with off.app_context():
            init_schema()
        assert not set(EnrollmentBase.metadata.tables) & set(inspect(engine).get_table_names())
    finally:
        EnrollmentBase.metadata.create_all(engine)
    off.extensions["db_engine"].dispose()


@pytest.mark.parametrize("slots", ['[true]', '[300]', '[7,7]', '{}'])
def test_invalid_operator_slot_profile_fails_closed(env, slots):
    mutate(env, lambda db: setattr(db.get(Device, DID), "candidate_slots_json", slots))
    body = dict(client_request_id=str(uuid4()), target_user_id=env[3]["staff1"], device_id=DID)
    assert env[1].post(ROOT, headers=env[2], json=body).status_code == 503
    assert count(env[0], Job) == 0


def test_actual_loopback_http_to_mysql_commit_and_retry(env):
    import threading
    import urllib.request
    from werkzeug.serving import make_server
    app, _, headers, ids, *_ = env
    server = make_server("127.0.0.1", 0, app, threaded=True)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    base = "http://127.0.0.1:" + str(server.server_port) + ROOT
    def post(path, body):
        request = urllib.request.Request(base + path, data=json.dumps(body).encode(),
            headers=headers | {"Content-Type": "application/json"}, method="POST")
        with urllib.request.urlopen(request, timeout=10) as response:
            assert response.status == 200
            return json.load(response)["data"]
    try:
        job = post("", dict(client_request_id=str(uuid4()), target_user_id=ids["staff1"], device_id=DID))
        payload = dict(operation_revision="1", result_envelope=result(job))
        one = post("/" + job["enrollment_id"] + "/complete", payload)
        two = post("/" + job["enrollment_id"] + "/complete", payload)
        assert one == two and two["state"] == "completed" and count(app, Mapping) == 1
    finally:
        server.shutdown(); thread.join(timeout=10); server.server_close()


def test_enrollment_actual_dump_new_database_restore(env, owned_databases, tmp_path):
    import subprocess
    app = env[0]
    source = app.extensions["db_engine"]
    job, _ = issue(env)
    assert complete(env, job, result(job)).status_code == 200
    def snapshot(engine):
        with engine.connect() as conn:
            return {table.name: list(conn.execute(select(table)).tuples())
                    for table in list(Base.metadata.sorted_tables) + list(EnrollmentBase.metadata.sorted_tables)}
    before = snapshot(source)
    config, dump = tmp_path / "enrollment.private.cnf", tmp_path / "enrollment.private.sql"
    def quote(value):
        return '"' + str(value).replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n') + '"'
    try:
        _private_file(config); _private_file(dump)
        config.write_text("[client]\nprotocol=TCP\nhost=" + quote(source.url.host) + "\nport=" + str(source.url.port) +
            "\nuser=" + quote(source.url.username) + "\npassword=" + quote(source.url.password) +
            "\ndefault-character-set=utf8mb4\n", encoding="utf-8")
        child_env = os.environ.copy(); child_env.pop("MYSQL_PWD", None)
        backup = subprocess.run([_mysql_tool("mysqldump"), "--defaults-file=" + config.name, "--no-login-paths",
            "--no-tablespaces", "--set-gtid-purged=OFF", "--single-transaction", "--skip-comments",
            "--skip-add-locks", "--skip-lock-tables", "--result-file=" + dump.name, source.url.database],
            cwd=tmp_path, env=child_env, capture_output=True)
        assert backup.returncode == 0, "Owned enrollment dump failed; private output omitted"
        restored = owned_databases("enrollment_restore")
        assert inspect(restored).get_table_names() == []
        recovered = subprocess.run([_mysql_tool("mysql"), "--defaults-file=" + config.name, "--no-login-paths",
            "--binary-mode=1", "--database=" + restored.url.database], cwd=tmp_path, env=child_env,
            input=dump.read_bytes(), capture_output=True)
        assert recovered.returncode == 0, "Owned enrollment restore failed; private output omitted"
        assert snapshot(restored) == before
    finally:
        config.unlink(missing_ok=True); dump.unlink(missing_ok=True)


@pytest.mark.parametrize("schema", ["orm", "raw"])
@pytest.mark.parametrize("capacity,minimum,maximum,slot,valid", [
    (300, 1, 299, 12, True),  # Allowed subset, not a claim that slot0/299 was measured.
    (300, 299, 299, 299, True),
    (300, 0, 299, 7, True),
    (1, 0, 0, 0, True),
    (300, 0, 999, 999, False),
    (300, 1, 300, 12, False),
    (300, -1, 299, 12, False),
    (300, 20, 10, 12, False),
    (0, 0, 0, 0, False),
])
def test_reported_capacity_and_allowed_subset_cli_and_mysql(env, schema, capacity, minimum, maximum, slot, valid):
    app = env[0]
    engine = app.extensions["db_engine"]
    if schema == "raw":
        with app.app_context(), transaction() as db:
            for table in reversed(EnrollmentBase.metadata.sorted_tables):
                db.execute(delete(table))
        EnrollmentBase.metadata.drop_all(engine)
        ddl = (Path(__file__).parents[1] / "migrations/007_device_enrollment.mysql.sql").read_text(encoding="utf-8")
        with engine.begin() as connection:
            for statement in ddl.split(";"):
                if statement.strip():
                    connection.exec_driver_sql(statement)
    else:
        mutate(env, lambda db: db.delete(db.get(Device, DID)))
    args = ["add-enrollment-device", "--device-id", DID, "--display-name", "Synthetic profile",
        "--capacity", str(capacity), "--minimum-slot", str(minimum), "--maximum-slot", str(maximum),
        "--candidate-slots", str(slot)]
    result_cli = app.test_cli_runner().invoke(args=args)
    assert (result_cli.exit_code == 0) is valid, result_cli.output
    assert count(app, Device) == int(valid)
    # Bypass CLI and exercise the independently created physical CHECK too.
    def insert_direct():
        with app.app_context(), transaction() as db:
            db.add(Device(device_id=str(uuid4()), display_name="Direct CHECK probe", enabled=True,
                key_id=KEYS["key_id"], key_epoch=1, capacity=capacity, minimum_slot=minimum,
                maximum_slot=maximum, candidate_slots_json=json.dumps([slot])))
            db.flush()
    if valid:
        insert_direct()
        assert count(app, Device) == 2
        mutate(env, lambda db: grant_resource(db, env[3]["admin1"], "enrollment_device", DID))
        job, _ = issue(env)
        assert job["reserved_slot"] == slot
        assert complete(env, job, result(job)).status_code == 200
    else:
        with pytest.raises(OperationalError) as error:
            insert_direct()
        assert error.value.orig.args[0] == 3819
        assert count(app, Device) == 0


@pytest.mark.parametrize("candidate", [0, 300])
def test_cli_candidate_must_stay_in_allowed_subset(env, candidate):
    mutate(env, lambda db: db.delete(db.get(Device, DID)))
    result_cli = env[0].test_cli_runner().invoke(args=["add-enrollment-device", "--device-id", DID,
        "--display-name", "Synthetic subset", "--capacity", "300", "--minimum-slot", "1",
        "--maximum-slot", "299", "--candidate-slots", str(candidate)])
    assert result_cli.exit_code != 0 and count(env[0], Device) == 0


def test_legacy_check_requires_explicit_review_and_preserves_rows(env):
    app, client, headers, ids, *_ = env
    engine = app.extensions["db_engine"]
    old = "key_epoch > 0 AND minimum_slot >= 0 AND maximum_slot >= minimum_slot AND capacity > 0 AND capacity <= maximum_slot - minimum_slot + 1"
    new = "key_epoch > 0 AND capacity > 0 AND minimum_slot >= 0 AND maximum_slot >= minimum_slot AND maximum_slot < capacity"
    def replace_check(expression):
        with engine.begin() as connection:
            connection.exec_driver_sql("ALTER TABLE enrollment_devices DROP CHECK ck_enrollment_profile, "
                "ADD CONSTRAINT ck_enrollment_profile CHECK (" + expression + ")")
    replace_check(old)
    # Reproduce old over-acceptance in this owned DB; the runtime must still deny.
    mutate(env, lambda db: (setattr(db.get(Device, DID), "maximum_slot", 999),
                           setattr(db.get(Device, DID), "candidate_slots_json", "[999]")))
    body = dict(client_request_id=str(uuid4()), device_id=DID, target_user_id=ids["staff1"])
    assert client.post(ROOT, headers=headers, json=body).status_code == 503
    with pytest.raises(OperationalError) as error:
        replace_check(new)
    assert error.value.orig.args[0] == 3819
    with engine.connect() as connection:
        assert connection.exec_driver_sql("SELECT capacity,maximum_slot,candidate_slots_json FROM enrollment_devices").one() == (300, 999, "[999]")
    # Explicit fixture correction only: no application startup auto-ALTER or auto-repair.
    mutate(env, lambda db: (setattr(db.get(Device, DID), "maximum_slot", 299),
                           setattr(db.get(Device, DID), "candidate_slots_json", "[7,8]")))
    with engine.connect() as connection:
        before = list(connection.exec_driver_sql("SELECT * FROM enrollment_devices").tuples())
    replace_check(new)
    with engine.connect() as connection:
        assert list(connection.exec_driver_sql("SELECT * FROM enrollment_devices").tuples()) == before
    mutate(env, lambda db: setattr(db.get(Device, DID), "minimum_slot", 1))
    job, _ = issue(env)
    assert complete(env, job, result(job)).status_code == 200


@pytest.mark.parametrize("active_candidate", [True, False])
def test_eligible_omits_disabled_grants_without_hiding_active_device(env, active_candidate):
    app, client, headers, ids, *_ = env
    disabled = "223e4567-e89b-42d3-a456-426614174000"
    def change(db):
        db.get(Device, DID).enabled = active_candidate
        db.add(Device(device_id=disabled, display_name="Disabled synthetic device", enabled=False,
            key_id=KEYS["key_id"], key_epoch=1, minimum_slot=0, maximum_slot=299, capacity=300,
            candidate_slots_json="[7,8]"))
        grant_resource(db, ids["admin1"], "enrollment_device", disabled)
    mutate(env, change)
    response = client.get(ROOT + "/eligible-devices?target_user_id=" + str(ids["staff1"]), headers=headers)
    assert response.status_code == 200, response.json
    assert [d["device_id"] for d in response.json["data"]["devices"]] == ([DID] if active_candidate else [])


@pytest.mark.parametrize("invalid,status", [("target", 409), ("missing_target", 409),
    ("actor", 401), ("role", 403), ("session", 401)])
def test_empty_eligible_list_does_not_hide_invalid_target_or_identity(env, invalid, status):
    app, client, headers, ids, *_ = env
    def change(db):
        db.get(Device, DID).enabled = False
        if invalid == "target":
            db.get(User, ids["staff1"]).is_active = False
        elif invalid == "actor":
            db.get(User, ids["admin1"]).is_active = False
        elif invalid == "role":
            db.get(User, ids["admin1"]).role = "employee"
        elif invalid == "session":
            db.execute(delete(AuthSession))
    mutate(env, change)
    target = 2147483647 if invalid == "missing_target" else ids["staff1"]
    assert client.get(ROOT + "/eligible-devices?target_user_id=" + str(target), headers=headers).status_code == status


def test_disabled_device_still_denies_issue_approval_completion(env):
    job, _ = issue(env)
    raw = result(job)
    mutate(env, lambda db: setattr(db.get(Device, DID), "enabled", False))
    approval_body = {f: job[f] for f in wire.COMMON - {"enrollment_id"}} | dict(
        ticket_digest=wire.ticket_digest(wire.b64decode(job["request_envelope"])),
        probe_nonce=wire.b64encode(os.urandom(32)), scope="sensor_write")
    client, headers = env[1:3]
    assert client.post(ROOT, headers=headers, json=dict(client_request_id=str(uuid4()),
        target_user_id=env[3]["staff2"], device_id=DID)).status_code == 409
    assert client.post(ROOT + "/" + job["enrollment_id"] + "/approval", headers=headers, json=approval_body).status_code == 409
    assert complete(env, job, raw).status_code == 409
    assert count(env[0], Mapping) == 0


def test_eligible_does_not_expose_ungranted_device(env):
    def change(db):
        db.add(Device(device_id="223e4567-e89b-42d3-a456-426614174000", display_name="Ungrant synthetic device",
            enabled=True, key_id=KEYS["key_id"], key_epoch=1, minimum_slot=0, maximum_slot=299,
            capacity=300, candidate_slots_json="[7,8]"))
    mutate(env, change)
    response = env[1].get(ROOT + "/eligible-devices?target_user_id=" + str(env[3]["staff1"]), headers=env[2])
    assert response.status_code == 200
    assert [d["device_id"] for d in response.json["data"]["devices"]] == [DID]


@pytest.mark.parametrize("invalid", ["keys", "profile"])
def test_enabled_granted_device_keeps_key_and_profile_validation(env, invalid):
    if invalid == "keys":
        env[0].config["ENROLLMENT_DEVICE_KEYS"] = {}
    else:
        mutate(env, lambda db: setattr(db.get(Device, DID), "candidate_slots_json", "[999]"))
    response = env[1].get(ROOT + "/eligible-devices?target_user_id=" + str(env[3]["staff1"]), headers=env[2])
    assert response.status_code == 503
