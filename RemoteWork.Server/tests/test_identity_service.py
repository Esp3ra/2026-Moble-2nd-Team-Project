"""Owned MySQL + Flask only. No real profiles, server, sensor or attendance."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta
import hashlib
import json
import os
from pathlib import Path
from uuid import uuid4

import pytest
from sqlalchemy import delete, func, select, text

from remotework import create_app, device_identity_contract as wire
from remotework.db import get_db, transaction
from remotework.models import AuditEvent, AuthSession, SchemaVersion, User
from remotework.features.identity import validate_configuration
from remotework.features.identity.models import IdentityBase, Context, Challenge, Approval
from remotework.features.identity.attendance import AttendanceBase, AttendanceWorkflow, WorkSession, WorkflowReceipt
from remotework.features.identity import routes
from test_device_enrollment import env, DID, issue as enroll, result as enroll_result, complete as enroll_complete

ROOT = "/api/v1/device-identity"
PASSWORD = "Enrollment-test-2026!"


class Workflow:
    def current(self, db, actor, purpose):
        # Explicit test double. Not a deployment ACK or real attendance reader.
        return {"work_date": (routes.now() + timedelta(hours=9)).date().isoformat(),
                "work_session_ref": None if purpose == "clock_in" else "50000000-0000-4000-8000-000000000001"}


@pytest.fixture
def identity(env):
    old, _, _, _, _, login = env
    registered, _ = enroll(env)
    assert enroll_complete(env, registered, enroll_result(registered)).status_code == 200
    normal = login("staff1")
    keys = {kind: os.urandom(32) for kind in wire.KINDS}
    p = dict(device_id=DID, key_id="identity-test-only", key_epoch="1", enabled=True,
             minimum_slot=0, maximum_slot=299, capacity=300, keys=keys)
    config = dict(old.config) | {"ENABLE_DEVICE_IDENTITY_DRAFT": True,
        "IDENTITY_DEVICE_PROFILES": {DID: p}, "IDENTITY_WORKFLOW_PROVIDER": Workflow()}
    app = create_app(config)
    engine = app.extensions["db_engine"]
    assert engine.url.database.startswith("remote_work_test_")
    sql = (Path(__file__).resolve().parents[1] / "migrations/drafts/identity_v2.mysql.sql").read_text(encoding="utf-8")
    sql = "\n".join(line for line in sql.splitlines() if not line.startswith("--"))
    with engine.begin() as conn:
        for statement in sql.split(";"):
            if statement.strip():
                conn.execute(text(statement))
    try:
        yield app, app.test_client(), p, normal, env
    finally:
        AttendanceBase.metadata.drop_all(engine)
        IdentityBase.metadata.drop_all(engine)
        engine.dispose()


def pending(e, rid=None):
    r = e[1].post(ROOT + "/login", json=dict(login_id="staff1", password=PASSWORD, client_request_id=rid or str(uuid4())))
    assert r.status_code == 200, r.json
    d = r.json["data"]
    assert "access_token" not in d
    return {"Authorization": "IdentityPending " + d["pending_token"]}, d


def issue(e, headers, rid=None, purpose="clock_in"):
    data = dict(client_request_id=rid or str(uuid4()), device_id=DID, purpose=purpose)
    r = e[1].post(ROOT + "/challenges", headers=headers, json=data)
    assert r.status_code == 200, r.json
    return r.json["data"], data


def signed(e, kind, value):
    return wire.sign(json.dumps(value, separators=(",", ":")).encode(), kind=kind,
        device_id=DID, key_id=e[2]["key_id"], key=e[2]["keys"][kind])


def ticket(e, job):
    return wire.verify(wire.b64decode(job["request_b64"]), kind="id_request", device_id=DID,
                       key_id=e[2]["key_id"], key=e[2]["keys"]["id_request"])


def probe(e, job, scope):
    t = ticket(e, job)
    return {k: t[k] for k in wire.COMMON} | dict(ticket_digest=wire.message_digest(wire.b64decode(job["request_b64"])),
        probe_nonce=wire.b64encode(os.urandom(32)), scope=scope)


def approval(e, h, job, scope):
    value = probe(e, job, scope)
    body = {"probe_b64": wire.b64encode(signed(e, "id_probe", value))}
    r = e[1].post(ROOT + "/challenges/" + job["challenge_id"] + "/approvals", headers=h, json=body)
    assert r.status_code == 200, r.json
    return r.json["data"]["approval_b64"], body


def result(e, h, job, outcome="matched"):
    t = ticket(e, job)
    digest = None
    if outcome in ("matched", "no_match"):
        approval(e, h, job, "capture")
        a, _ = approval(e, h, job, "result")
        digest = wire.message_digest(wire.b64decode(a))
    value = {k: t[k] for k in wire.COMMON} | dict(ticket_digest=wire.message_digest(wire.b64decode(job["request_b64"])),
        result_id=str(uuid4()), outcome=outcome, slot=t["expected_slot"] if outcome == "matched" else None,
        failure_code=wire.FAILURES[outcome], approval_digest=digest)
    return {"result_b64": wire.b64encode(signed(e, "id_result", value))}


def post(e, h, job, action, data):
    return e[1].post(ROOT + "/challenges/" + job["challenge_id"] + "/" + action, headers=h, json=data)


def mutate(e, fn):
    with e[0].app_context(), transaction() as db:
        fn(db)


def test_pending_cannot_become_bearer_cookie_or_password_only_work_session(identity):
    e = identity; h, d = pending(e)
    assert e[1].get("/api/v1/auth/me", headers=h).status_code == 401
    assert e[1].get("/api/v1/auth/me", headers={"Authorization": "Bearer " + d["pending_token"]}).status_code == 401
    assert e[1].get("/api/v1/auth/me", headers=e[3]).status_code == 401
    assert e[1].post("/api/v1/auth/login", json=dict(login_id="staff1", password=PASSWORD)).status_code == 503
    assert e[1].get(ROOT + "/requests/" + str(uuid4()), headers=e[3]).status_code == 200


@pytest.mark.parametrize("outcome", tuple(wire.FAILURES))
def test_six_outcomes_commit_exact_bytes_receipt_replay_and_no_work_token(identity, outcome):
    e = identity; h, _ = pending(e); job, _ = issue(e, h)
    data = result(e, h, job, outcome)
    first = post(e, h, job, "complete", data)
    assert first.status_code == 200, first.json
    answer = first.json["data"]
    assert answer["state"] == ("verified" if outcome == "matched" else "rejected")
    assert "access_token" not in answer
    assert post(e, h, job, "complete", data).json["data"] == answer
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=h).json["data"] == answer
    changed = wire.parse_json(wire.b64decode(data["result_b64"]))
    payload = wire.parse_json(wire.b64decode(changed["payload_b64"])) | {"result_id": str(uuid4())}
    assert post(e, h, job, "complete", {"result_b64": wire.b64encode(signed(e, "id_result", payload))}).status_code == 409
    with e[0].app_context():
        db = get_db(); item = db.get(Challenge, job["challenge_id"])
        assert item.result_bytes == wire.b64decode(data["result_b64"])
        assert item.result_hash == hashlib.sha256(item.result_bytes).hexdigest()
        assert list(db.scalars(select(SchemaVersion.version))) == [1]
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "identity." + answer["state"])) == 1
    if outcome == "matched":
        assert post(e, h, job, "finalize", {"client_request_id": str(uuid4())}).status_code == 503


def test_issue_response_loss_same_uuid_and_conflicting_body(identity):
    e = identity; h, _ = pending(e); rid = str(uuid4()); job, data = issue(e, h, rid)
    assert e[1].get(ROOT + "/requests/" + rid, headers=h).json["data"] == job
    assert e[1].post(ROOT + "/challenges", headers=h, json=data).json["data"] == job
    assert e[1].post(ROOT + "/challenges", headers=h, json=data | {"purpose": "noon_check"}).status_code == 403
    assert e[1].post(ROOT + "/challenges", headers=h, json=data | {"client_request_id": str(uuid4())}).status_code == 409


def test_capture_fence_survives_new_app_and_does_not_renew_same_probe(identity):
    e = identity; h, _ = pending(e); job, _ = issue(e, h)
    answer, original = approval(e, h, job, "capture")
    # New app/engine: RAM loss does not remove the MySQL fence.
    restarted = create_app(dict(e[0].config))
    try:
        peer = restarted.test_client()
        path = ROOT + "/challenges/" + job["challenge_id"] + "/approvals"
        assert peer.post(path, headers=h, json=original).json["data"]["approval_b64"] == answer
        fresh = {"probe_b64": wire.b64encode(signed(e, "id_probe", probe(e, job, "capture")))}
        assert peer.post(path, headers=h, json=fresh).status_code == 409
        assert peer.get(ROOT + "/challenges/" + job["challenge_id"], headers=h).json["data"]["state"] == "issued"
    finally:
        restarted.extensions["db_engine"].dispose()


def test_cancel_wins_over_late_matched_and_new_request_is_explicit(identity):
    e = identity; h, _ = pending(e); job, _ = issue(e, h); data = result(e, h, job)
    cancel = {"client_request_id": str(uuid4())}
    assert post(e, h, job, "cancel", cancel).json["data"]["state"] == "cancelled"
    assert post(e, h, job, "cancel", cancel).status_code == 200
    assert post(e, h, job, "complete", data).status_code == 409
    assert issue(e, h)[0]["challenge_id"] != job["challenge_id"]


@pytest.mark.parametrize("change", ["user", "password", "device", "mapping", "epoch", "normal_session"])
def test_current_revalidation_defeats_stale_snapshot(identity, change):
    e = identity
    h, _ = pending(e) if change != "normal_session" else (e[3], None)
    job, _ = issue(e, h, purpose="clock_in" if change != "normal_session" else "noon_check")
    data = result(e, h, job)
    def revoke(db):
        from remotework.features.enrollment.models import Device, Mapping
        if change == "user": db.scalar(select(User).where(User.login_id == "staff1")).is_active = False
        if change == "password": db.scalar(select(User).where(User.login_id == "staff1")).password_hash = "revoked"
        if change == "device": db.get(Device, DID).enabled = False
        if change == "mapping": db.scalar(select(Mapping)).revision += 1
        if change == "epoch": db.get(Device, DID).key_epoch += 1
        if change == "normal_session": db.execute(delete(AuthSession))
    mutate(e, revoke)
    assert post(e, h, job, "complete", data).status_code in (401, 403)


def test_expired_request_commits_terminal_and_tombstone_never_reissues(identity, monkeypatch):
    e = identity; h, _ = pending(e); job, data = issue(e, h)
    t = routes.now()
    monkeypatch.setattr(routes, "now", lambda: t + timedelta(seconds=61))
    r = post(e, h, job, "approvals", {"probe_b64": wire.b64encode(signed(e, "id_probe", probe(e, job, "capture")))})
    assert r.status_code == 410
    with e[0].app_context():
        assert get_db().get(Challenge, job["challenge_id"]).state == "expired"
    assert e[1].post(ROOT + "/challenges", headers=h, json=data).json["data"]["state"] == "expired"


def test_login_response_loss_rotates_pending_and_revokes_old_challenges(identity):
    e = identity; rid = str(uuid4()); h, _ = pending(e, rid); job, _ = issue(e, h)
    h2, _ = pending(e, rid)
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=h).status_code == 401
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=h2).status_code == 404
    assert issue(e, h2)[0]["challenge_id"] != job["challenge_id"]


def test_duplicate_complete_concurrency_one_effect(identity):
    e = identity; h, _ = pending(e); job, _ = issue(e, h); data = result(e, h, job)
    def worker(_):
        with e[0].test_client() as client:
            r = client.post(ROOT + "/challenges/" + job["challenge_id"] + "/complete", headers=h, json=data)
            return r.status_code, r.json["data"]
    with ThreadPoolExecutor(max_workers=2) as pool:
        replies = list(pool.map(worker, range(2)))
    assert replies[0] == replies[1] and replies[0][0] == 200


def test_storage_partial_schema_and_unknown_input_fail_closed(identity):
    e = identity; h, _ = pending(e)
    assert e[1].post(ROOT + "/challenges", headers=h, json=dict(client_request_id=str(uuid4()), device_id=DID, purpose="clock_in", slot=7)).status_code == 400
    assert e[1].post(ROOT + "/challenges", headers=h, data='{"device_id":"x","device_id":"y"}', content_type="application/json").status_code == 400
    with e[0].extensions["db_engine"].begin() as conn:
        conn.execute(text("ALTER TABLE identity_operations RENAME COLUMN operation TO missing_operation"))
    try:
        assert e[1].get(ROOT + "/requests/" + str(uuid4()), headers=h).status_code == 503
    finally:
        with e[0].extensions["db_engine"].begin() as conn:
            conn.execute(text("ALTER TABLE identity_operations RENAME COLUMN missing_operation TO operation"))


def test_profile_excludes_public_and_registration_keys(identity):
    cfg = dict(identity[0].config)
    for key in (bytes([1]) * 32, next(iter(cfg["ENROLLMENT_DEVICE_KEYS"].values()))["request_key"]):
        p = dict(identity[2]) | {"keys": dict(identity[2]["keys"]) | {"id_request": key}}
        with pytest.raises(ValueError): validate_configuration(cfg | {"IDENTITY_DEVICE_PROFILES": {DID: p}})


def test_result_cannot_use_capture_approval_or_approval_from_another_challenge(identity):
    e = identity; h, _ = pending(e); job, _ = issue(e, h)
    a, _ = approval(e, h, job, "capture")
    t = ticket(e, job)
    value = {k: t[k] for k in wire.COMMON} | dict(ticket_digest=wire.message_digest(wire.b64decode(job["request_b64"])),
        result_id=str(uuid4()), outcome="matched", slot=t["expected_slot"], failure_code=None,
        approval_digest=wire.message_digest(wire.b64decode(a)))
    assert post(e, h, job, "complete", {"result_b64": wire.b64encode(signed(e, "id_result", value))}).status_code == 409
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=h).json["data"]["state"] == "issued"


def test_missing_unique_constraint_is_storage_not_ready(identity):
    e = identity; h, _ = pending(e)
    engine = e[0].extensions["db_engine"]
    # Supply a nonunique FK support index, then remove only the UNIQUE evidence.
    with engine.begin() as conn:
        conn.execute(text("CREATE INDEX identity_test_user_fk ON identity_contexts(user_id)"))
        conn.execute(text("ALTER TABLE identity_contexts DROP INDEX uq_identity_login_request"))
    try:
        assert e[1].get(ROOT + "/requests/" + str(uuid4()), headers=h).status_code == 503
    finally:
        with engine.begin() as conn:
            conn.execute(text("ALTER TABLE identity_contexts ADD CONSTRAINT uq_identity_login_request UNIQUE(user_id,login_request_id)"))
            conn.execute(text("DROP INDEX identity_test_user_fk ON identity_contexts"))


def test_login_rate_limit_applies_without_issuing_normal_sessions(identity):
    e = identity
    for _ in range(5):
        r = e[1].post(ROOT + "/login", json=dict(login_id="staff1", password="incorrect", client_request_id=str(uuid4())))
        assert r.status_code == 401
    r = e[1].post(ROOT + "/login", json=dict(login_id="staff1", password=PASSWORD, client_request_id=str(uuid4())))
    assert r.status_code == 429


def test_actual_loopback_http_to_owned_mysql(identity):
    from threading import Thread
    from types import SimpleNamespace
    from urllib.error import HTTPError
    from urllib.request import build_opener, ProxyHandler, Request
    from werkzeug.serving import make_server
    e = identity
    server = make_server("127.0.0.1", 0, e[0], threaded=True)
    thread = Thread(target=server.serve_forever, daemon=True)
    thread.start()
    class TcpClient:
        def send(self, path, headers=None, json=None, method="GET"):
            import json as codec
            request = Request("http://127.0.0.1:" + str(server.server_port) + path,
                              data=codec.dumps(json).encode() if json is not None else None,
                              headers=(headers or {}) | {"Content-Type": "application/json"}, method=method)
            opener = build_opener(ProxyHandler({}))
            try: response = opener.open(request, timeout=5)
            except HTTPError as exc: response = exc
            with response:
                return SimpleNamespace(status_code=response.code, json=codec.loads(response.read()))
        def post(self, path, **kwargs): return self.send(path, method="POST", **kwargs)
        def get(self, path, **kwargs): return self.send(path, **kwargs)
    try:
        tcp = (e[0], TcpClient(), *e[2:])
        h, _ = pending(tcp); job, _ = issue(tcp, h); data = result(tcp, h, job)
        answer = post(tcp, h, job, "complete", data)
        assert answer.status_code == 200 and answer.json["data"]["state"] == "verified"
        assert post(tcp, h, job, "complete", data).json["data"] == answer.json["data"]
        assert post(tcp, h, job, "finalize", {"client_request_id": str(uuid4())}).status_code == 503
    finally:
        server.shutdown(); server.server_close(); thread.join(5)
        assert not thread.is_alive()


@pytest.mark.parametrize("normal", [False, True])
def test_revoke_lost_response_only_replays_same_operation_and_does_not_restore_rights(identity, normal):
    e = identity; h = e[3] if normal else pending(e)[0]
    job, _ = issue(e, h, purpose="noon_check" if normal else "clock_in")
    data = {"client_request_id": str(uuid4())}
    first = e[1].post(ROOT + "/context/revoke", headers=h, json=data)
    assert first.status_code == 200 and first.json["data"] == {"state": "revoked"}
    assert e[1].post(ROOT + "/context/revoke", headers=h, json=data).json["data"] == first.json["data"]
    assert e[1].post(ROOT + "/context/revoke", headers=h, json={"client_request_id": str(uuid4())}).status_code == 401
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=h).status_code == 401


def test_original_approval_expiry_does_not_reissue_twenty_seconds(identity, monkeypatch):
    e = identity; h, _ = pending(e); job, _ = issue(e, h)
    _, data = approval(e, h, job, "capture")
    t = routes.now()
    monkeypatch.setattr(routes, "now", lambda: t + timedelta(seconds=21))
    assert post(e, h, job, "approvals", data).status_code == 410


def test_noop_workflow_is_not_synthesized(identity):
    e = identity; h, _ = pending(e)
    e[0].extensions["identity_workflow"] = type("NotReady", (), {"current": lambda *_: None})()
    r = e[1].post(ROOT + "/challenges", headers=h, json=dict(device_id=DID, purpose="clock_in", client_request_id=str(uuid4())))
    assert r.status_code == 503 and r.json["error"]["code"] == "IDENTITY_WORKFLOW_NOT_READY"


@pytest.fixture
def working(identity):
    e = identity
    provider = AttendanceWorkflow(os.urandom(32), "owned-test-workflow-key")
    cfg = e[0].config
    cfg["IDENTITY_WORKFLOW_PROVIDER"] = provider
    validate_configuration(cfg)
    e[0].extensions["identity_workflow"] = provider
    return e


def clock_in(e):
    h, _ = pending(e); job, _ = issue(e, h)
    assert post(e, h, job, "complete", result(e, h, job)).status_code == 200
    key = {"client_request_id": str(uuid4())}
    response = post(e, h, job, "finalize", key)
    assert response.status_code == 200, response.json
    data = response.json["data"]
    return h, job, key, data, {"Authorization": "Bearer " + data["access_token"]}


def test_actual_attendance_commit_encrypted_replay_and_resume_without_fingerprint(working):
    e = working
    first_login = e[1].post("/api/v1/auth/login", json={"login_id": "staff1", "password": PASSWORD})
    assert first_login.status_code == 409 and first_login.json["error"]["code"] == "IDENTITY_FINGERPRINT_REQUIRED"
    h, job, key, data, normal = clock_in(e)
    assert data["state"] == "completed" and data["token_type"] == "Bearer"
    assert post(e, h, job, "finalize", key).json["data"] == data
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=h).json["data"]["workflow_state"] == "completed"
    assert e[1].get("/api/v1/auth/me", headers=normal).status_code == 200
    assert e[1].post("/api/v1/auth/logout", headers=normal, json={}).status_code == 200
    resumed = e[1].post("/api/v1/auth/login", json={"login_id": "staff1", "password": PASSWORD})
    assert resumed.status_code == 200 and resumed.json["data"]["access_token"] != data["access_token"]
    with e[0].app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(WorkSession)) == 1
        receipt = db.get(WorkflowReceipt, job["challenge_id"])
        assert data["access_token"].encode() not in receipt.ciphertext
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "attendance.clock_in")) == 1
    assert e[1].post(ROOT + "/challenges", headers=h, json={"device_id": DID, "purpose": "clock_in", "client_request_id": str(uuid4())}).status_code == 403
    assert post(e, h, job, "finalize", {"client_request_id": str(uuid4())}).status_code == 409


def test_clock_out_is_atomic_revokes_sessions_replays_receipt_and_no_same_day_reentry(working):
    e = working; _, _, _, _, normal = clock_in(e)
    job, _ = issue(e, normal, purpose="clock_out")
    assert post(e, normal, job, "complete", result(e, normal, job)).status_code == 200
    key = {"client_request_id": str(uuid4())}
    done = post(e, normal, job, "finalize", key)
    assert done.status_code == 200 and "access_token" not in done.json["data"]
    assert e[1].get("/api/v1/auth/me", headers=normal).status_code == 401
    assert post(e, normal, job, "finalize", key).json["data"] == done.json["data"]
    assert e[1].get(ROOT + "/challenges/" + job["challenge_id"], headers=normal).json["data"]["workflow_state"] == "completed"
    again = e[1].post("/api/v1/auth/login", json={"login_id": "staff1", "password": PASSWORD})
    assert again.status_code == 409 and again.json["error"]["code"] == "ATTENDANCE_DAY_CLOSED"
    with e[0].app_context():
        assert get_db().scalar(select(WorkSession)).ended_at is not None
        actor = get_db().scalar(select(User).where(User.login_id == "staff1"))
        assert get_db().scalar(select(func.count()).select_from(AuthSession).where(AuthSession.user_id == actor.id)) == 0
        assert get_db().scalar(select(func.count()).select_from(AuthSession).where(AuthSession.user_id != actor.id)) > 0


def test_noon_server_time_once_only_and_same_work_session(working, monkeypatch):
    e = working
    t = routes.now().replace(hour=1, minute=0, second=0)
    monkeypatch.setattr(routes, "now", lambda: t)
    _, _, _, first, normal = clock_in(e)
    r = e[1].post(ROOT + "/challenges", headers=normal, json={"device_id": DID, "purpose": "noon_check", "client_request_id": str(uuid4())})
    assert r.status_code == 409 and r.json["error"]["code"] == "ATTENDANCE_NOON_NOT_DUE"
    monkeypatch.setattr(routes, "now", lambda: t + timedelta(hours=2))
    job, _ = issue(e, normal, purpose="noon_check")
    assert ticket(e, job)["work_session_ref"] == first["work_session_ref"]
    assert post(e, normal, job, "complete", result(e, normal, job)).status_code == 200
    key = {"client_request_id": str(uuid4())}
    completed = post(e, normal, job, "finalize", key)
    assert completed.status_code == 200 and post(e, normal, job, "finalize", key).json["data"] == completed.json["data"]
    assert e[1].get("/api/v1/auth/me", headers=normal).status_code == 200
    r = e[1].post(ROOT + "/challenges", headers=normal, json={"device_id": DID, "purpose": "noon_check", "client_request_id": str(uuid4())})
    assert r.status_code == 409 and r.json["error"]["code"] == "ATTENDANCE_NOON_ALREADY_RECORDED"


def test_finalize_rollback_does_not_leak_token_or_partial_clock_in(working):
    e = working; h, _ = pending(e); job, _ = issue(e, h)
    assert post(e, h, job, "complete", result(e, h, job)).status_code == 200
    provider = e[0].extensions["identity_workflow"]
    original = provider._cipher
    provider._cipher = type("FailedVault", (), {"encrypt": lambda *_: (_ for _ in ()).throw(RuntimeError("synthetic rollback"))})()
    key = {"client_request_id": str(uuid4())}
    assert post(e, h, job, "finalize", key).status_code == 500
    with e[0].app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(WorkSession)) == 0
        assert db.get(WorkflowReceipt, job["challenge_id"]) is None
        assert not db.get(Challenge, job["challenge_id"]).workflow_completed
    provider._cipher = original
    assert post(e, h, job, "finalize", key).status_code == 200


def test_finalize_key_cipher_rotation_and_password_revocation_fail_closed(working):
    e = working; h, job, key, _, _ = clock_in(e)
    e[0].extensions["identity_workflow"] = AttendanceWorkflow(os.urandom(32), "other-epoch")
    assert post(e, h, job, "finalize", key).status_code == 503
    mutate(e, lambda db: setattr(db.scalar(select(User).where(User.login_id == "staff1")), "password_hash", "revoked"))
    assert post(e, h, job, "finalize", key).status_code == 401


def test_concurrent_finalize_one_work_session_and_one_encrypted_receipt(working):
    e = working; h, _ = pending(e); job, _ = issue(e, h)
    assert post(e, h, job, "complete", result(e, h, job)).status_code == 200
    key = {"client_request_id": str(uuid4())}
    def worker(_):
        with e[0].test_client() as client:
            r = client.post(ROOT + "/challenges/" + job["challenge_id"] + "/finalize", headers=h, json=key)
            return r.status_code, r.json["data"]
    with ThreadPoolExecutor(max_workers=2) as pool:
        answers = list(pool.map(worker, range(2)))
    assert answers[0] == answers[1] and answers[0][0] == 200
    with e[0].app_context():
        assert get_db().scalar(select(func.count()).select_from(WorkSession)) == 1
        assert get_db().scalar(select(func.count()).select_from(WorkflowReceipt)) == 1
