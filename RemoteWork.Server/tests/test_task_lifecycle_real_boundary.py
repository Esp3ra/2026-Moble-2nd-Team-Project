"""Real results registration must not unlock member 2 lifecycle writes.

Only an explicitly owned disposable MySQL is used. These are refusal-boundary
checks with the real results blueprint and its existing development reviewer
store, not proof of member 3 cancellation/pending guards or integrated readiness.
No integrated acknowledgment, replacement route or facts callback is installed.
"""
from datetime import timedelta
import os

import pytest
from sqlalchemy import select

from remotework.db import get_db, transaction
from remotework.features import results
from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
from remotework.features.tasks.lifecycle_support import TEST_DOUBLE_ACK, integration_acknowledged
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, User, utcnow
from remotework.write_requests import WriteReceipt


ACTIONS = ("cancel", "request", "approve", "reject", "withdraw")


@pytest.fixture(autouse=True)
def real_boundary_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_LIFECYCLE", "RESULTS"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    for name in ("TASK_CREATE", "TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE_RESULT_READS",
                 "NOTICE", "NOTICE_POPUP", "CHAT", "DEVICE_ENROLLMENT"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    monkeypatch.setenv("REMOTEWORK_RESULTS_CONTEXT", "local-development")


@pytest.fixture
def real_boundary(request):
    if not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"):
        pytest.skip("Real-results boundary checks require an explicitly owned disposable MySQL database")
    # Use normal create_app/registry registration through the shared fixture.
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    assert "results" in app.blueprints
    assert app.config["RESULTS_CONTEXT_MODE"] == "local-development"
    assert app.extensions["results_facts"] is results.local_facts
    assert "task_lifecycle_integration_ack" not in app.extensions
    now = utcnow()
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        owner, manager = users["staff1"], users["admin1"]
        tasks = []
        for title in ("real-results refusal boundary", "real-results context probe"):
            task = TaskRecord(title=title, description="preserved fixture",
                task_status="in_progress", start_date=(now - timedelta(days=1)).date(),
                due_date=(now + timedelta(days=7)).date(), current_assignment_revision=2,
                row_version=7, created_by=manager.id, created_at=now, updated_at=now)
            db.add(task)
            db.flush()
            db.add(TaskAssignment(task_id=task.task_id, revision=2, assignee_id=owner.id,
                assigned_by=manager.id, assigned_at=now, accepted_at=now))
            db.add(TaskManager(task_id=task.task_id, user_id=manager.id))
            tasks.append(task)
        task, context_task = tasks
        db.add(results.DepartmentReviewer(department=owner.department, reviewer_id=manager.id))
        db.flush()
        # Synthetic member 2 data supplies current decision/withdraw authority.
        # No member 3 submission, gate or attachment is fabricated or modified.
        extension = TaskDeadlineExtension(task_id=task.task_id, assignment_revision=2,
            original_due_date=task.due_date, requested_due_date=task.due_date + timedelta(days=3),
            requested_by=owner.id, reason="Synthetic pending boundary fixture", status="pending",
            created_at=now)
        db.add(extension)
        db.flush()
        fixture = {"task_id": str(task.task_id), "extension_id": str(extension.extension_id),
                   "requested_due_date": extension.requested_due_date.isoformat(),
                   "reviewer_id": manager.id, "context_task_id": str(context_task.task_id)}
    return app, fixture


def snapshot(app):
    """Compare committed values, including every real-results storage table."""
    tables = [TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__,
              TaskCancellation.__table__, TaskDeadlineExtension.__table__,
              AuditEvent.__table__, WriteReceipt.__table__, *results.RESULT_TABLES]
    with app.app_context():
        db = get_db()
        return {table.name: tuple(tuple(row) for row in db.execute(
                    select(table).order_by(*table.primary_key.columns)))
                for table in tables}


@pytest.mark.parametrize("ack_mode", ("missing", "wrong", "testing-double"))
@pytest.mark.parametrize("action", ACTIONS)
def test_real_results_registration_does_not_unlock_lifecycle(real_boundary, client, login, ack_mode, action):
    app, fixture = real_boundary
    if ack_mode == "wrong":
        app.extensions["task_lifecycle_integration_ack"] = {"contract": "wrong-contract"}
    elif ack_mode == "testing-double":
        app.extensions["task_lifecycle_integration_ack"] = dict(TEST_DOUBLE_ACK)
    with app.app_context():
        assert not integration_acknowledged()
    staff, _ = login("staff1", "Staff-test-2026!")
    manager, _ = login("admin1", "Admin-test-2026!")
    before = snapshot(app)

    # Exercise the registered handler on a separate task WITHOUT a pending row.
    # A 200 here must not require member 3's missing pending guard to stay absent.
    prepared = client.get("/api/v1/results/tasks/" + fixture["context_task_id"] + "/context", headers=staff)
    assert prepared.status_code == 200, prepared.json
    assert prepared.json["data"]["task_id"] == fixture["context_task_id"]
    assert prepared.json["data"]["reviewer_id"] == fixture["reviewer_id"]
    assert prepared.json["data"]["row_version"] == "7"

    base = "/api/v1/tasks/" + fixture["task_id"]
    path = {"cancel": "/cancel", "request": "/deadline-extensions"}.get(action)
    if path is None:
        path = "/deadline-extensions/" + fixture["extension_id"] + "/" + action
    body = {"expected_row_version": "7", "expected_assignment_revision": 2}
    if action in ("cancel", "request", "reject"):
        body["reason"] = "Explicit boundary refusal check"
    if action == "request":
        body["requested_due_date"] = fixture["requested_due_date"]
    actor = manager if action in ("cancel", "approve", "reject") else staff
    # Refusal must happen before idempotency-key/body processing or any mutation.
    response = client.post(base + path, headers=actor, json=body)
    assert response.status_code == 503, response.json
    assert response.json["ok"] is False
    assert response.json["error"]["code"] == "TASK_LIFECYCLE_INTEGRATION_REQUIRED"
    assert response.headers["X-Request-ID"] == response.json["request_id"]
    assert "data" not in response.json
    for headers in (staff, manager):
        context = client.get(base + "/lifecycle", headers=headers)
        assert context.status_code == 200, context.json
        assert context.json["data"]["context"]["write_enabled"] is False
    assert snapshot(app) == before
