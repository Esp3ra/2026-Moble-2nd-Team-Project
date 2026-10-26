"""Disposable MySQL: original history/receipts survive absence classification."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path
from uuid import uuid4

import pytest
from sqlalchemy import func, select, text

from remotework.db import get_db, transaction
from remotework.models import AuditEvent, AuthSession, User
from remotework.features.identity import routes
from remotework.features.identity.attendance import WorkSession, WorkflowReceipt
from remotework.features.identity.history import AttendanceException, ExceptionBase
from remotework.features.identity.models import Challenge, Context
from test_identity_service import (env, identity, working, clock_in, pending, issue,
    result, post, mutate, PASSWORD, ROOT, DID)

API = "/api/v1/attendance/records"


@pytest.fixture
def ready(working, monkeypatch):
    e = working
    sql = (Path(__file__).resolve().parents[1] / "migrations/018_attendance_exceptions.mysql.sql").read_text(encoding="utf-8")
    sql = "\n".join(line for line in sql.splitlines() if not line.startswith("--"))
    engine = e[0].extensions["db_engine"]
    with engine.begin() as connection:
        for statement in sql.split(";"):
            if statement.strip():
                connection.execute(text(statement))
    monkeypatch.setitem(e[0].config, "MISSED_CLOCKOUT_POLICY", "absence-v1")
    stamp = datetime.now(timezone.utc).replace(tzinfo=None, hour=1, minute=0, second=0, microsecond=0)
    monkeypatch.setattr(routes, "now", lambda: stamp)
    try:
        yield e
    finally:
        ExceptionBase.metadata.drop_all(engine)


def login(e, password=PASSWORD):
    return e[1].post("/api/v1/auth/login", json={"login_id": "staff1", "password": password})


def seed(e, offset=-1, *, user="staff1", ended=False):
    stamp = routes.now() + timedelta(days=offset)
    work = WorkSession(id=str(uuid4()), user_id=e[4][3][user],
        work_date=(stamp + timedelta(hours=9)).date().isoformat(), started_at=stamp,
        noon_at=None, ended_at=stamp + timedelta(hours=8) if ended else None)
    mutate(e, lambda db: db.add(work))
    return work


def snapshot(e):
    with e[0].app_context():
        db = get_db()
        return ([(r.id, r.user_id, r.work_date, r.started_at, r.noon_at, r.ended_at)
            for r in db.scalars(select(WorkSession).order_by(WorkSession.id))],
            [(r.challenge_id, r.client_request_id, r.ciphertext, r.expires_at)
            for r in db.scalars(select(WorkflowReceipt).order_by(WorkflowReceipt.challenge_id))])


def counts(e):
    with e[0].app_context():
        db = get_db()
        return (db.scalar(select(func.count()).select_from(AttendanceException)),
            db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "attendance.missed_clock_out")))


def test_password_classifies_all_past_rows_atomically_without_granting_login(ready):
    e = ready
    a, b, closed, other = seed(e, -3), seed(e, -2), seed(e, -1, ended=True), seed(e, -1, user="staff2")
    before = snapshot(e)
    for _ in range(2):
        response = login(e)
        assert response.status_code == 409 and response.json["error"]["code"] == "IDENTITY_FINGERPRINT_REQUIRED"
        assert counts(e) == (2, 2)
        assert snapshot(e) == before
    with e[0].app_context():
        db = get_db()
        assert {x.work_session_id for x in db.scalars(select(AttendanceException))} == {a.id, b.id}
        assert db.scalar(select(func.count()).select_from(AuthSession).where(AuthSession.user_id == a.user_id)) == 1


def test_pending_login_then_fresh_fingerprint_opens_only_today(ready):
    e = ready; old = seed(e); before = snapshot(e)
    _, _, _, today, normal = clock_in(e)
    assert today["work_date"] != old.work_date
    assert counts(e) == (1, 1)
    assert snapshot(e)[0][0 if before[0][0][0] < today["work_session_ref"] else 1] == before[0][0]
    assert e[1].get("/api/v1/auth/me", headers=normal).status_code == 200
    assert login(e).status_code == 200


def test_kst_boundary_keeps_today_open_then_classifies_after_midnight(ready, monkeypatch):
    e = ready; today = seed(e, 0)
    before = snapshot(e)
    assert login(e).status_code == 200
    assert counts(e) == (0, 0)
    stamp = routes.now().replace(hour=15)  # Same UTC date, next KST day.
    monkeypatch.setattr(routes, "now", lambda: stamp)
    assert login(e).status_code == 409
    assert counts(e) == (1, 1) and snapshot(e) == before


def test_future_open_work_remains_blocked_and_cleanup_rolls_back(ready):
    e = ready; seed(e); seed(e, 1); before = snapshot(e)
    response = login(e)
    assert response.status_code == 409 and response.json["error"]["code"] == "ATTENDANCE_DAY_REVIEW_REQUIRED"
    assert counts(e) == (0, 0) and snapshot(e) == before


def test_invalid_password_and_inactive_account_cannot_classify(ready):
    e = ready; seed(e); before = snapshot(e)
    assert login(e, "incorrect-owned-test").status_code == 401
    mutate(e, lambda db: setattr(db.get(User, e[4][3]["staff1"]), "is_active", False))
    assert login(e).status_code == 401
    assert counts(e) == (0, 0) and snapshot(e) == before


def test_audit_failure_rolls_back_exception_and_creates_no_session(ready, monkeypatch):
    from remotework.features.identity import history
    e = ready; seed(e); before = snapshot(e)
    monkeypatch.setattr(history, "audit", lambda *_a, **_k: (_ for _ in ()).throw(RuntimeError("owned audit failure")))
    assert login(e).status_code == 500
    assert counts(e) == (0, 0) and snapshot(e) == before


def test_policy_off_preserves_old_block_and_recorded_exceptions(ready, monkeypatch):
    e = ready; seed(e); before = snapshot(e)
    monkeypatch.setitem(e[0].config, "MISSED_CLOCKOUT_POLICY", "")
    response = login(e)
    assert response.json["error"]["code"] == "ATTENDANCE_DAY_REVIEW_REQUIRED" and counts(e) == (0, 0)
    monkeypatch.setitem(e[0].config, "MISSED_CLOCKOUT_POLICY", "absence-v1")
    assert login(e).json["error"]["code"] == "IDENTITY_FINGERPRINT_REQUIRED"
    monkeypatch.setitem(e[0].config, "MISSED_CLOCKOUT_POLICY", "")
    assert login(e).json["error"]["code"] == "IDENTITY_FINGERPRINT_REQUIRED"
    assert counts(e) == (1, 1) and snapshot(e) == before


def test_concurrent_password_retries_classify_once(ready):
    e = ready; seed(e); before = snapshot(e)
    def attempt(_):
        with e[0].test_client() as client:
            return client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": PASSWORD}).status_code
    with ThreadPoolExecutor(max_workers=2) as pool:
        assert list(pool.map(attempt, range(2))) == [409, 409]
    assert counts(e) == (1, 1) and snapshot(e) == before


def test_unexpired_old_operation_requires_recovery_before_classification(ready, monkeypatch):
    e = ready
    base = routes.now().replace(hour=14, minute=59, second=30)
    monkeypatch.setattr(routes, "now", lambda: base)
    _, _, _, _, normal = clock_in(e)
    job, _ = issue(e, normal, purpose="clock_out")
    before = snapshot(e)
    monkeypatch.setattr(routes, "now", lambda: base + timedelta(seconds=31))
    r = login(e)
    assert r.status_code == 409 and r.json["error"]["code"] == "ATTENDANCE_PREVIOUS_OPERATION_PENDING"
    r = e[1].post(ROOT + "/login", json=dict(login_id="staff1", password=PASSWORD, client_request_id=str(uuid4())))
    assert r.status_code == 409 and counts(e) == (0, 0)
    monkeypatch.setattr(routes, "now", lambda: base + timedelta(minutes=10))
    assert login(e).json["error"]["code"] == "IDENTITY_FINGERPRINT_REQUIRED"
    assert counts(e) == (1, 1) and snapshot(e) == before
    with e[0].app_context():
        item = get_db().get(Challenge, job["challenge_id"])
        assert item.state == "issued" and not item.workflow_completed


def test_verified_previous_day_result_cannot_finalize_as_today(ready, monkeypatch):
    e = ready
    base = routes.now().replace(hour=14, minute=59, second=30)
    monkeypatch.setattr(routes, "now", lambda: base)
    _, _, _, _, normal = clock_in(e)
    job, _ = issue(e, normal, purpose="clock_out")
    assert post(e, normal, job, "complete", result(e, normal, job)).status_code == 200
    before = snapshot(e)
    monkeypatch.setattr(routes, "now", lambda: base + timedelta(seconds=31))
    r = post(e, normal, job, "finalize", {"client_request_id": str(uuid4())})
    assert r.status_code == 409
    assert counts(e) == (0, 0) and snapshot(e) == before


def test_classification_keeps_original_receipt_and_fence_without_reauthorizing_today(ready, monkeypatch):
    e = ready
    base = routes.now().replace(hour=14, minute=59, second=30)
    monkeypatch.setattr(routes, "now", lambda: base)
    h, job, key, answer, normal = clock_in(e)
    before = snapshot(e)
    with e[0].app_context():
        item = get_db().get(Challenge, job["challenge_id"])
        original = (item.request_bytes, item.result_bytes, item.result_hash, item.capture_nonce, item.receipt_json)
    monkeypatch.setattr(routes, "now", lambda: base + timedelta(seconds=31))
    assert login(e).json["error"]["code"] == "IDENTITY_FINGERPRINT_REQUIRED"
    replay = post(e, h, job, "finalize", key)
    assert replay.status_code == 200 and replay.json["data"] == answer
    assert e[1].get("/api/v1/auth/me", headers=normal).status_code == 401
    assert counts(e) == (1, 1) and snapshot(e) == before
    with e[0].app_context():
        item = get_db().get(Challenge, job["challenge_id"])
        assert (item.request_bytes, item.result_bytes, item.result_hash, item.capture_nonce, item.receipt_json) == original


def test_history_own_only_admin_all_pagination_and_get_never_classifies(ready):
    e = ready; old = seed(e); seed(e, -2, user="staff2"); seed(e, 0, user="admin1")
    seed(e, 0)
    normal = e[3]  # Pre-identity real API session; today work is now explicitly present.
    admin = e[4][2]
    before = snapshot(e)
    r = e[1].get(API, headers=normal)
    assert r.status_code == 200 and len(r.json["data"]["records"]) == 2
    past = next(row for row in r.json["data"]["records"] if row["work_session_ref"] == old.id)
    assert past["state"] == "review_required" and past["ended_at"] is None and past["reason_code"] is None
    assert counts(e) == (0, 0) and snapshot(e) == before
    assert e[1].get(API + "?scope=all", headers=normal).status_code == 403
    assert e[1].get(API + "?user_id=" + str(e[4][3]["staff2"]), headers=normal).status_code == 403
    r = e[1].get(API + "?scope=all&limit=2", headers=admin)
    assert r.status_code == 200 and len(r.json["data"]["records"]) == 2
    cursor = r.json["data"]["next_cursor"]
    last = e[1].get(API + "?scope=all&limit=2&cursor=" + cursor, headers=admin)
    assert last.status_code == 200 and len(last.json["data"]["records"]) == 2
    assert last.json["data"]["next_cursor"] is None
    assert not {x["work_session_ref"] for x in r.json["data"]["records"]} & {x["work_session_ref"] for x in last.json["data"]["records"]}
    assert login(e).status_code == 200
    own = e[1].get(API, headers=normal).json["data"]["records"]
    past = next(row for row in own if row["work_session_ref"] == old.id)
    assert past["state"] == "absent" and past["reason_code"] == "missed_clock_out"
    assert past["ended_at"] is None and past["started_at"].endswith("Z") and past["recorded_at"]
    assert counts(e) == (1, 1) and snapshot(e) == before
    mutate(e, lambda db: setattr(db.get(User, e[4][3]["admin1"]), "role", "employee"))
    assert e[1].get(API + "?scope=all&cursor=" + cursor, headers=admin).status_code == 403


def test_history_validates_filters_and_requires_normal_active_session(ready):
    e = ready; seed(e, 0)
    normal = e[3]
    assert e[1].get(API).status_code == 401
    h, _ = pending(e)
    assert e[1].get(API, headers=h).status_code == 401
    for query in ("limit=0", "limit=101", "limit=01", "limit=1&limit=2", "bogus=1",
            "scope=team", "from_date=20261001", "from_date=2026-02-30", "from_date=2020-01-01",
            "from_date=2027-01-01&to_date=2026-01-01", "cursor=no", "cursor=" + str(uuid4()), "user_id=-1"):
        r = e[1].get(API + "?" + query, headers=normal)
        assert r.status_code == 400, (query, r.status_code, r.json)
    mutate(e, lambda db: setattr(db.get(User, e[4][3]["staff1"]), "is_active", False))
    assert e[1].get(API, headers=normal).status_code == 401


def test_missing_018_fails_closed_instead_of_creating_ddl(ready):
    e = ready; seed(e)
    ExceptionBase.metadata.drop_all(e[0].extensions["db_engine"])
    r = login(e)
    assert r.status_code == 503 and r.json["error"]["code"] == "IDENTITY_STORAGE_NOT_READY"
    from remotework.identity_host import preflight
    from remotework.common import AppError
    with pytest.raises(AppError, match="근무|본인"):
        preflight(e[0])
