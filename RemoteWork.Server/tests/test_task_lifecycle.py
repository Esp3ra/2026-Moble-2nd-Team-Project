"""Member 2 lifecycle policy against an explicitly owned disposable MySQL.

The explicit TESTING acknowledgment is a result-disabled test double, not proof
of member 3/shared deployment integration. No SQLite maintenance is included.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta
import json
import os
import subprocess
import sys
from threading import Barrier

import pytest
from sqlalchemy import func, select
from sqlalchemy.exc import OperationalError
from sqlalchemy.schema import CreateTable

from remotework.auth import create_user
from remotework.common import AppError
from remotework.db import get_db, transaction
from remotework.features.tasks import lifecycle
from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
from remotework.features.tasks.lifecycle_support import (TEST_DOUBLE_ACK, integration_acknowledged,
    pending_extension_facts, require_lifecycle_schema, storage_present)
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, User
from remotework.write_contract import BIGINT_MAX
from remotework.write_requests import WriteReceipt


NOW = datetime(2026, 10, 20, 3, 4, 5, 123456)
ACTIONS = ("task.cancelled", "task.deadline_extension_requested", "task.deadline_extension_approved",
           "task.deadline_extension_rejected", "task.deadline_extension_withdrawn")


@pytest.fixture(autouse=True)
def lifecycle_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_RESULTS_DRAFT", "0")
    monkeypatch.setattr(lifecycle, "utcnow", lambda: NOW)


@pytest.fixture
def life(request):
    if not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"):
        pytest.skip("Lifecycle policy checks require an explicitly owned disposable MySQL database")
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    app.extensions["task_lifecycle_integration_ack"] = dict(TEST_DOUBLE_ACK)
    with app.app_context(), transaction() as db:
        users = {u.login_id: u for u in db.scalars(select(User))}
        for name, role in (("staff2", "employee"), ("admin2", "admin")):
            users[name] = create_user(db, {"login_id": name, "password": "Lifecycle-test-2026!",
                "display_name": name, "employee_no": "LIFE-" + name, "role": role})
        ids = {name: user.id for name, user in users.items()}
        task = TaskRecord(title="lifecycle", description="preserved", task_status="in_progress",
            start_date=(NOW - timedelta(days=4)).date(), due_date=(NOW - timedelta(days=1)).date(),
            current_assignment_revision=2, row_version=7, created_by=ids["admin1"],
            created_at=NOW - timedelta(days=4), updated_at=NOW - timedelta(days=2))
        db.add(task)
        db.flush()
        db.add(TaskAssignment(task_id=task.task_id, revision=2, assignee_id=ids["staff1"],
            assigned_by=ids["admin1"], assigned_at=NOW - timedelta(days=4), accepted_at=NOW - timedelta(days=3)))
        db.add(TaskManager(task_id=task.task_id, user_id=ids["admin1"]))
        target = str(task.task_id)
    return app, ids, target


def error(response, status, code):
    assert response.status_code == status, response.json
    assert response.json["ok"] is False and response.json["error"]["code"] == code
    assert response.headers["X-Request-ID"] == response.json["request_id"]


def key_for(client, headers, env, action):
    response = client.post("/api/v1/write-requests/keys", headers=headers,
                           json={"action": action, "target": env[2]})
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def body(version="7"):
    return {"expected_row_version": version, "expected_assignment_revision": 2}


def send(client, headers, env, action, version="7", extension_id=None, key=None, **changes):
    suffix = {"task.cancelled": "/cancel", "task.deadline_extension_requested": "/deadline-extensions",
        "task.deadline_extension_approved": f"/deadline-extensions/{extension_id}/approve",
        "task.deadline_extension_rejected": f"/deadline-extensions/{extension_id}/reject",
        "task.deadline_extension_withdrawn": f"/deadline-extensions/{extension_id}/withdraw"}[action]
    values = body(version)
    if action in ("task.cancelled", "task.deadline_extension_requested", "task.deadline_extension_rejected"):
        values["reason"] = "reason"
    if action == "task.deadline_extension_requested":
        values["requested_due_date"] = (NOW + timedelta(days=3)).date().isoformat()
    values.update(changes)
    return client.post("/api/v1/tasks/" + env[2] + suffix,
        headers=headers | {"Idempotency-Key": key or key_for(client, headers, env, action)}, json=values)


def state(env, value):
    with env[0].app_context(), transaction() as db:
        db.get(TaskRecord, int(env[2])).task_status = value


def snapshot(env):
    with env[0].app_context():
        db = get_db()
        task = db.get(TaskRecord, int(env[2]))
        return (task.task_status, task.row_version, task.due_date, task.updated_at,
            tuple((r.extension_id, r.status, r.requested_due_date, r.decided_at, r.decided_by, r.decision_reason)
                for r in db.scalars(select(TaskDeadlineExtension).order_by(TaskDeadlineExtension.extension_id))),
            db.scalar(select(func.count()).select_from(TaskCancellation)),
            db.scalar(select(func.count()).select_from(WriteReceipt)),
            db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action.in_(ACTIONS))))


def request_one(env, client, login, *, version="7", due=None):
    staff, _ = login("staff1", "Staff-test-2026!")
    changes = {} if due is None else {"requested_due_date": due}
    response = send(client, staff, env, "task.deadline_extension_requested", version, **changes)
    assert response.status_code == 200, response.json
    return staff, response.json["data"]["extension_id"], response.json["data"]


def test_explicit_ack_is_not_shared_or_result_feature_integration(life):
    app = life[0]
    with app.app_context():
        assert integration_acknowledged()
        app.config["TESTING"] = False
        assert not integration_acknowledged()
        app.config["TESTING"] = True
        app.config["ENABLE_RESULTS_DRAFT"] = True
        assert not integration_acknowledged()
        app.config["ENABLE_RESULTS_DRAFT"] = False
        app.blueprints["results"] = object()
        assert not integration_acknowledged()


@pytest.mark.parametrize("ack", [None, {}, {"mode": "integrated"}, {**TEST_DOUBLE_ACK, "contract": "old-version"}])
def test_missing_or_wrong_ack_denies_writes_and_exposes_readonly_context(life, client, login, ack):
    life[0].extensions["task_lifecycle_integration_ack"] = ack
    staff, _ = login("staff1", "Staff-test-2026!")
    before = snapshot(life)
    response = client.post("/api/v1/tasks/" + life[2] + "/deadline-extensions", headers=staff,
        json=body() | {"requested_due_date": "2026-10-23", "reason": "reason"})
    error(response, 503, "TASK_LIFECYCLE_INTEGRATION_REQUIRED")
    assert snapshot(life) == before
    context = client.get("/api/v1/tasks/" + life[2] + "/lifecycle", headers=staff)
    assert context.status_code == 200 and context.json["data"]["context"]["write_enabled"] is False


@pytest.mark.parametrize("value", ["not_started", "in_progress", "submitted", "approved", "returned"])
def test_cancellation_uses_approved_matrix_and_preserves_five_states(life, client, login, value):
    state(life, value)
    admin, _ = login("admin1", "Admin-test-2026!")
    before = snapshot(life)
    key = key_for(client, admin, life, "task.cancelled")
    response = send(client, admin, life, "task.cancelled", key=key)
    if value not in ("not_started", "in_progress", "returned"):
        error(response, 409, "TASK_STATE_CONFLICT")
        assert snapshot(life) == before
        return
    assert response.status_code == 200, response.json
    data = response.json["data"]
    assert set(data) == lifecycle.RESULT_FIELDS
    assert data["task_status"] == value and data["row_version"] == "8"
    assert data["extension_id"] is None and data["requested_due_date"] is None
    assert snapshot(life)[5:] == (1, 1, 1)
    assert send(client, admin, life, "task.cancelled", key=key).json["data"] == data
    assert snapshot(life)[5:] == (1, 1, 1)


@pytest.mark.parametrize("value", ["not_started", "in_progress", "submitted", "approved", "returned"])
def test_request_uses_approved_matrix_even_after_old_due_date(life, client, login, value):
    state(life, value)
    staff, _ = login("staff1", "Staff-test-2026!")
    before = snapshot(life)
    response = send(client, staff, life, "task.deadline_extension_requested")
    if value in ("in_progress", "returned"):
        assert response.status_code == 200, response.json
        assert response.json["data"]["task_status"] == value
        assert response.json["data"]["due_date"] == "2026-10-19"
        assert snapshot(life)[1] == 8 and snapshot(life)[6:] == (1, 1)
    else:
        error(response, 409, "TASK_STATE_CONFLICT")
        assert snapshot(life) == before


@pytest.mark.parametrize("action", ["task.deadline_extension_approved", "task.deadline_extension_rejected",
                                    "task.deadline_extension_withdrawn"])
def test_decision_and_withdraw_preserve_state_and_exact_success_receipt(life, client, login, action):
    staff, ext, requested = request_one(life, client, login)
    headers = staff if action.endswith("withdrawn") else login("admin1", "Admin-test-2026!")[0]
    key = key_for(client, headers, life, action)
    response = send(client, headers, life, action, "8", ext, key)
    assert response.status_code == 200, response.json
    result = response.json["data"]
    assert set(result) == lifecycle.RESULT_FIELDS and result["task_status"] == "in_progress"
    assert result["row_version"] == "9" and result["extension_id"] == ext
    assert result["due_date"] == ("2026-10-23" if action.endswith("approved") else "2026-10-19")
    assert send(client, headers, life, action, "8", ext, key).json["data"] == result
    looked = client.post("/api/v1/write-requests/result", headers=headers | {"Idempotency-Key": key},
        json={"action": action, "target": life[2]})
    assert looked.status_code == 200 and looked.json["data"]["result"] == result
    assert snapshot(life)[6:] == (2, 2)


def test_rejected_or_withdrawn_request_can_be_reapplied_without_history_delete(life, client, login):
    staff, ext, _ = request_one(life, client, login)
    assert send(client, staff, life, "task.deadline_extension_withdrawn", "8", ext).status_code == 200
    staff, next_ext, _ = request_one(life, client, login, version="9")
    assert ext != next_ext
    admin = login("admin1", "Admin-test-2026!")[0]
    assert send(client, admin, life, "task.deadline_extension_rejected", "10", next_ext, reason="").status_code == 200
    request_one(life, client, login, version="11")
    assert [row[1] for row in snapshot(life)[4]] == ["withdrawn", "rejected", "pending"]


def test_cancel_closes_pending_and_preserves_task_assignment_and_readable_history(life, client, login):
    staff, ext, _ = request_one(life, client, login)
    admin = login("admin1", "Admin-test-2026!")[0]
    response = send(client, admin, life, "task.cancelled", "8")
    assert response.status_code == 200, response.json
    after = snapshot(life)
    assert after[0] == "in_progress" and after[1] == 9 and after[4][0][1] == "cancelled"
    listed = client.get("/api/v1/tasks?scope=own", headers=staff)
    assert listed.status_code == 200 and listed.json["data"]["items"] == []
    included = client.get("/api/v1/tasks?scope=own&include_cancelled=true", headers=staff)
    assert included.status_code == 200 and included.json["data"]["items"][0]["is_cancelled"] is True
    detail = client.get("/api/v1/tasks/" + life[2] + "/lifecycle", headers=staff)
    assert detail.status_code == 200 and detail.json["data"]["context"]["is_cancelled"] is True
    assert detail.json["data"]["context"]["pending_extension"] is None
    history = client.get("/api/v1/tasks/extension-requests?scope=own&status=cancelled", headers=staff)
    assert history.status_code == 200 and history.json["data"]["items"][0]["extension_id"] == ext
    error(send(client, staff, life, "task.deadline_extension_requested", "9"), 409, "TASK_CANCELLED")
    assert snapshot(life) == after


@pytest.mark.parametrize("actor", ["staff1", "staff2", "admin2"])
def test_unrelated_roles_cannot_cancel(life, client, login, actor):
    password = "Staff-test-2026!" if actor == "staff1" else "Lifecycle-test-2026!"
    headers = login(actor, password)[0]
    before = snapshot(life)
    response = client.post("/api/v1/tasks/" + life[2] + "/cancel", headers=headers, json={})
    error(response, 403, "FORBIDDEN")
    assert snapshot(life) == before


@pytest.mark.parametrize("actor", ["staff2", "admin1", "admin2"])
def test_only_current_assignee_can_request(life, client, login, actor):
    password = "Admin-test-2026!" if actor == "admin1" else "Lifecycle-test-2026!"
    headers = login(actor, password)[0]
    before = snapshot(life)
    response = client.post("/api/v1/tasks/" + life[2] + "/deadline-extensions", headers=headers, json={})
    error(response, 403, "FORBIDDEN")
    assert snapshot(life) == before


def test_admin_assignee_requires_another_active_current_manager_and_no_self_decision(life, client, login):
    with life[0].app_context(), transaction() as db:
        db.get(TaskAssignment, (int(life[2]), 2)).assignee_id = life[1]["admin1"]
    admin = login("admin1", "Admin-test-2026!")[0]
    before = snapshot(life)
    error(send(client, admin, life, "task.deadline_extension_requested"), 409, "TASK_EXTENSION_DECIDER_REQUIRED")
    assert snapshot(life) == before
    with life[0].app_context(), transaction() as db:
        db.add(TaskManager(task_id=int(life[2]), user_id=life[1]["admin2"]))
    response = send(client, admin, life, "task.deadline_extension_requested")
    assert response.status_code == 200, response.json
    ext = response.json["data"]["extension_id"]
    denied = client.post(f"/api/v1/tasks/{life[2]}/deadline-extensions/{ext}/approve", headers=admin, json=body("8"))
    error(denied, 403, "FORBIDDEN")
    other = login("admin2", "Lifecycle-test-2026!")[0]
    assert send(client, other, life, "task.deadline_extension_approved", "8", ext).status_code == 200


@pytest.mark.parametrize("due", ["2026-10-19", "2026-10-18"])
def test_request_date_must_increase_and_reach_kst_today(life, client, login, due):
    staff = login("staff1", "Staff-test-2026!")[0]
    before = snapshot(life)
    error(send(client, staff, life, "task.deadline_extension_requested", requested_due_date=due),
          409, "TASK_EXTENSION_DATE_INVALID")
    assert snapshot(life) == before


def test_kst_date_boundary_accepts_today_and_expired_request_cannot_be_adjusted(life, client, login, monkeypatch):
    monkeypatch.setattr(lifecycle, "utcnow", lambda: datetime(2026, 10, 19, 15, 0))
    staff, ext, _ = request_one(life, client, login, due="2026-10-20")
    monkeypatch.setattr(lifecycle, "utcnow", lambda: datetime(2026, 10, 20, 15, 0))
    admin = login("admin1", "Admin-test-2026!")[0]
    before = snapshot(life)
    error(send(client, admin, life, "task.deadline_extension_approved", "8", ext),
          409, "TASK_EXTENSION_DATE_INVALID")
    assert snapshot(life) == before
    assert send(client, admin, life, "task.deadline_extension_rejected", "8", ext).status_code == 200


def test_only_one_pending_request_and_conflict_does_not_write_a_receipt(life, client, login):
    staff, ext, _ = request_one(life, client, login)
    before = snapshot(life)
    error(send(client, staff, life, "task.deadline_extension_requested", "8"), 409, "TASK_EXTENSION_PENDING")
    assert snapshot(life) == before


@pytest.mark.parametrize("changes,code", [({"expected_row_version": "6"}, "TASK_VERSION_CONFLICT"),
    ({"expected_assignment_revision": 1}, "TASK_ASSIGNMENT_CHANGED")])
def test_expected_versions_are_checked_after_current_permissions(life, client, login, changes, code):
    staff = login("staff1", "Staff-test-2026!")[0]
    before = snapshot(life)
    error(send(client, staff, life, "task.deadline_extension_requested", **changes), 409, code)
    assert snapshot(life) == before


def test_row_version_overflow_leaves_no_domain_or_audit_or_receipt(life, client, login):
    with life[0].app_context(), transaction() as db:
        db.get(TaskRecord, int(life[2])).row_version = BIGINT_MAX
    staff = login("staff1", "Staff-test-2026!")[0]
    before = snapshot(life)
    error(send(client, staff, life, "task.deadline_extension_requested", str(BIGINT_MAX)),
          409, "TASK_VERSION_EXHAUSTED")
    assert snapshot(life) == before


@pytest.mark.parametrize("raw", [
    '{"expected_row_version":"7","expected_row_version":"7","expected_assignment_revision":2,"reason":"why"}',
    '{"expected_row_version":"7","expected_assignment_revision":true,"reason":"why"}',
    '{"expected_row_version":7,"expected_assignment_revision":2,"reason":"why"}',
    '{"expected_row_version":"7","expected_assignment_revision":2,"reason":" "}',
    '{"expected_row_version":"7","expected_assignment_revision":2,"reason":"why","extra":1}',
    '{"expected_row_version":"7","expected_assignment_revision":2,"reason":NaN}',
])
def test_strict_json_and_mandatory_cancel_reason(life, client, login, raw):
    admin = login("admin1", "Admin-test-2026!")[0]
    before = snapshot(life)
    key = key_for(client, admin, life, "task.cancelled")
    response = client.post("/api/v1/tasks/" + life[2] + "/cancel", headers=admin | {"Idempotency-Key": key},
                           data=raw, content_type="application/json")
    error(response, 400, "INVALID_INPUT")
    assert snapshot(life) == before


def test_manager_removal_and_reassignment_revoke_receipt_access(life, client, login):
    staff, ext, _ = request_one(life, client, login)
    admin = login("admin1", "Admin-test-2026!")[0]
    key = key_for(client, admin, life, "task.deadline_extension_approved")
    assert send(client, admin, life, "task.deadline_extension_approved", "8", ext, key).status_code == 200
    with life[0].app_context(), transaction() as db:
        db.delete(db.get(TaskManager, (int(life[2]), life[1]["admin1"])))
    response = client.post("/api/v1/write-requests/result", headers=admin | {"Idempotency-Key": key},
        json={"action": "task.deadline_extension_approved", "target": life[2]})
    error(response, 403, "FORBIDDEN")
    with life[0].app_context(), transaction() as db:
        db.get(TaskAssignment, (int(life[2]), 2)).assignee_id = life[1]["staff2"]
    error(client.get("/api/v1/tasks/" + life[2] + "/lifecycle", headers=staff), 403, "FORBIDDEN")


def test_changed_assignee_cannot_reuse_an_old_applicants_request_or_decision_receipt(life, client, login):
    staff, ext, _ = request_one(life, client, login)
    admin = login("admin1", "Admin-test-2026!")[0]
    key = key_for(client, admin, life, "task.deadline_extension_approved")
    with life[0].app_context(), transaction() as db:
        # Deliberately inconsistent external update: supported reassignment
        # never changes a running task or rewrites its historical assignment.
        db.get(TaskAssignment, (int(life[2]), 2)).assignee_id = life[1]["staff2"]
    before = snapshot(life)
    error(send(client, admin, life, "task.deadline_extension_approved", "8", ext, key), 403, "FORBIDDEN")
    error(client.post("/api/v1/write-requests/result", headers=admin | {"Idempotency-Key": key},
        json={"action": "task.deadline_extension_approved", "target": life[2]}), 403, "FORBIDDEN")
    assert snapshot(life) == before


def test_cancel_receipt_replay_remains_available_but_new_writes_stay_blocked_when_flag_off(life, client, login):
    admin = login("admin1", "Admin-test-2026!")[0]
    key = key_for(client, admin, life, "task.cancelled")
    assert send(client, admin, life, "task.cancelled", key=key).status_code == 200
    life[0].config["ENABLE_TASK_LIFECYCLE_DRAFT"] = False
    before = snapshot(life)
    # Another member 2 route must still honor persisted cancellation storage.
    change_key = key_for(client, admin, life, "task.managers_changed")
    changed = client.patch("/api/v1/tasks/" + life[2] + "/managers",
        headers=admin | {"Idempotency-Key": change_key},
        json=body("8") | {"manager_user_ids": [life[1]["admin1"], life[1]["admin2"]]})
    error(changed, 409, "TASK_CANCELLED")
    assert snapshot(life) == before
    listed = client.get("/api/v1/tasks?scope=managed", headers=admin)
    assert listed.status_code == 200 and listed.json["data"]["items"] == []


def test_same_key_cannot_replay_a_decision_for_a_different_request(life, client, login):
    staff, ext, _ = request_one(life, client, login)
    admin = login("admin1", "Admin-test-2026!")[0]
    key = key_for(client, admin, life, "task.deadline_extension_rejected")
    first = send(client, admin, life, "task.deadline_extension_rejected", "8", ext, key)
    assert first.status_code == 200
    _staff, second_ext, _ = request_one(life, client, login, version="9")
    before = snapshot(life)
    # Same JSON, actor/action/task/key but different route request ID.
    error(send(client, admin, life, "task.deadline_extension_rejected", "8", second_ext, key),
          409, "IDEMPOTENCY_KEY_REUSED")
    assert snapshot(life) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_every_mutation_is_closed_without_versioned_cross_owner_ack(life, client, login, action):
    ext, version = None, "7"
    staff = login("staff1", "Staff-test-2026!")[0]
    admin = login("admin1", "Admin-test-2026!")[0]
    if action not in ("task.cancelled", "task.deadline_extension_requested"):
        staff, ext, _ = request_one(life, client, login)
        version = "8"
    headers = staff if action in ("task.deadline_extension_requested", "task.deadline_extension_withdrawn") else admin
    key = key_for(client, headers, life, action)
    before = snapshot(life)
    life[0].extensions.pop("task_lifecycle_integration_ack")
    error(send(client, headers, life, action, version, ext, key),
          503, "TASK_LIFECYCLE_INTEGRATION_REQUIRED")
    assert snapshot(life) == before


def test_concurrent_requests_leave_exactly_one_pending_and_one_receipt(life, client, login):
    staff = login("staff1", "Staff-test-2026!")[0]
    keys = [key_for(client, staff, life, "task.deadline_extension_requested") for _ in range(2)]
    barrier = Barrier(2)
    def worker(key):
        with life[0].test_client() as local_client:
            barrier.wait(timeout=10)
            response = send(local_client, staff, life, "task.deadline_extension_requested", key=key)
            return response.status_code, response.json
    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(worker, keys))
    assert sorted(status for status, _data in responses) == [200, 409], responses
    assert len(snapshot(life)[4]) == 1 and snapshot(life)[6:] == (1, 1)


def test_audit_failure_rolls_back_request_and_task_and_receipt(life, client, login, monkeypatch):
    staff = login("staff1", "Staff-test-2026!")[0]
    before = snapshot(life)
    def fail(*_args, **_kwargs):
        raise OperationalError("audit failure", {}, Exception("fixture"))
    monkeypatch.setattr(lifecycle, "audit", fail)
    error(send(client, staff, life, "task.deadline_extension_requested"), 503, "DATABASE_UNAVAILABLE")
    assert snapshot(life) == before


def test_result_disabled_optin_does_not_touch_result_tables(life, client, login):
    assert "results" not in life[0].blueprints
    staff, ext, _ = request_one(life, client, login)
    admin = login("admin1", "Admin-test-2026!")[0]
    assert send(client, admin, life, "task.deadline_extension_approved", "8", ext).status_code == 200
    with life[0].app_context():
        assignment = get_db().get(TaskAssignment, (int(life[2]), 2))
        assert assignment.accepted_at == (NOW - timedelta(days=3)).replace(microsecond=0)


def test_request_lists_do_not_grant_old_assignment_or_other_requester_history(life, client, login):
    staff, current_id, _ = request_one(life, client, login)
    with life[0].app_context(), transaction() as db:
        db.add(TaskAssignment(task_id=int(life[2]), revision=1, assignee_id=life[1]["staff2"],
            assigned_by=life[1]["admin1"], assigned_at=NOW - timedelta(days=6), accepted_at=None))
        db.flush()
        for revision in (1, 2):
            db.add(TaskDeadlineExtension(task_id=int(life[2]), assignment_revision=revision,
                requested_by=life[1]["staff2"], original_due_date=NOW.date(),
                requested_due_date=(NOW + timedelta(days=1)).date(), reason="historical fixture",
                status="rejected", created_at=NOW - timedelta(days=1), decided_at=NOW,
                decided_by=life[1]["admin1"], decision_reason="history"))
    own = client.get("/api/v1/tasks/extension-requests?scope=own", headers=staff)
    assert own.status_code == 200 and [r["extension_id"] for r in own.json["data"]["items"]] == [current_id]
    admin = login("admin1", "Admin-test-2026!")[0]
    managed = client.get("/api/v1/tasks/extension-requests?scope=managed", headers=admin)
    assert managed.status_code == 200 and len(managed.json["data"]["items"]) == 2
    assert all(r["assignment_revision"] == 2 for r in managed.json["data"]["items"])
    with life[0].app_context():
        assert get_db().scalar(select(func.count()).select_from(TaskDeadlineExtension)) == 3


@pytest.mark.parametrize("expression,stored", [
    ("CASE WHEN status = 'pending' THEN assignment_revision + 1 ELSE NULL END", "STORED"),
    ("CASE WHEN status = 'pending' THEN assignment_revision ELSE NULL END", "VIRTUAL"),
])
def test_physical_generated_pending_expression_and_storage_are_checked(life, expression, stored):
    engine = life[0].extensions["db_engine"]
    try:
        with engine.begin() as connection:
            # Recreate only this empty, attempt-owned fixture table. MySQL
            # forbids MODIFY from STORED to VIRTUAL even for equivalent SQL.
            TaskDeadlineExtension.__table__.drop(connection)
            ddl = str(CreateTable(TaskDeadlineExtension.__table__).compile(dialect=engine.dialect))
            ddl = ddl.replace("CASE WHEN status = 'pending' THEN assignment_revision ELSE NULL END", expression)
            ddl = ddl.replace(" STORED", " " + stored)
            connection.exec_driver_sql(ddl)
        with life[0].app_context(), pytest.raises(AppError) as caught:
            require_lifecycle_schema(get_db())
        assert caught.value.status == 503 and caught.value.code == "TASK_LIFECYCLE_SCHEMA_REQUIRED"
    finally:
        # MySQL cannot switch STORED<->VIRTUAL with MODIFY; rebuild this empty
        # fixture-only table while preserving the task/assignment fixtures.
        with engine.begin() as connection:
            TaskDeadlineExtension.__table__.drop(connection, checkfirst=True)
            TaskDeadlineExtension.__table__.create(connection)


def test_missing_storage_is_not_a_false_no_pending_result_fact(life):
    engine = life[0].extensions["db_engine"]
    life[0].config["ENABLE_TASK_LIFECYCLE_DRAFT"] = False
    try:
        with engine.begin() as connection:
            TaskDeadlineExtension.__table__.drop(connection)
            TaskCancellation.__table__.drop(connection)
        with life[0].app_context():
            db = get_db()
            assert storage_present(db) is False
            with pytest.raises(AppError) as caught:
                pending_extension_facts(db, int(life[2]), 2)
            assert caught.value.status == 503 and caught.value.code == "TASK_LIFECYCLE_SCHEMA_REQUIRED"
    finally:
        with engine.begin() as connection:
            TaskCancellation.__table__.create(connection, checkfirst=True)
            TaskDeadlineExtension.__table__.create(connection, checkfirst=True)


def test_lifecycle_is_cold_and_routes_absent_when_default_off():
    code = """
import json, os, sys
os.environ['PYTHON_DOTENV_DISABLED']='1'
for name in list(os.environ):
    if name.startswith('REMOTEWORK_ENABLE_'): os.environ.pop(name)
from remotework import create_app
app=create_app({'TESTING':True,'SECRET_KEY':'cold-test-'+'a'*40,'DATABASE_URL':'mysql+pymysql://none:none@127.0.0.1:1/none'})
from remotework.models import Base
print(json.dumps({'modules':[m for m in sys.modules if '.tasks.lifecycle' in m],
 'tables':[n for n in Base.metadata.tables if n in ('task_cancellations','task_deadline_extensions')],
 'routes':[str(r) for r in app.url_map.iter_rules() if 'deadline-extensions' in str(r) or str(r).endswith('/cancel')]}))
"""
    result = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, check=True)
    assert json.loads(result.stdout) == {"modules": [], "tables": [], "routes": []}
