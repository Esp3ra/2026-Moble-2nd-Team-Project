"""Two bounded owner 2 consumers of the merged PR52 implementation.

The real results blueprint and local-development reviewer source run against
the explicit disposable MySQL fixture. This is not shared organization, file,
deployment ACK, lifecycle-write or multi-PC verification.
"""
from datetime import datetime, timedelta
import os

import pytest
from sqlalchemy import func, select

from test_task_web import _client, web_rows
from test_task_web_writes import _hidden


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Member 3 consumer checks require the explicitly owned disposable MySQL runner",
)
NOW = datetime(2026, 10, 21, 3, 4, 5)


@pytest.fixture(autouse=True)
def consumer_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    monkeypatch.setenv("REMOTEWORK_RESULTS_CONTEXT", "local-development")
    for name in ("TASK_CREATE", "TASK_START", "TASK_LIFECYCLE", "TASK_LIFECYCLE_RESULT_READS",
                 "SHARED_ATTACHMENTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT",
                 "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_MANAGEMENT", "RESULTS"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    # Import actual result metadata before the owned app fixture initializes it.
    from remotework.features import results
    from remotework.features.tasks import management
    monkeypatch.setattr(results, "utcnow", lambda: NOW)
    monkeypatch.setattr(management, "utcnow", lambda: NOW)


@pytest.fixture
def consumer_rows(web_rows):
    from remotework.db import transaction
    from remotework.features.results import DepartmentReviewer
    from remotework.features.tasks.models import TaskAssignment, TaskRecord
    from remotework.models import User
    app, ids = web_rows
    assert app.extensions["db_engine"].dialect.name == "mysql"
    assert app.config["RESULTS_CONTEXT_MODE"] == "local-development"
    assert "task_lifecycle_integration_ack" not in app.extensions
    with app.app_context(), transaction() as db:
        staff = db.get(User, ids["staff1"])
        db.add(DepartmentReviewer(department=staff.department, reviewer_id=ids["admin1"]))
        db.get(TaskRecord, 1).due_date = (NOW + timedelta(days=3)).date()
        db.get(TaskAssignment, (1, 1)).accepted_at = NOW - timedelta(days=1)
    return web_rows


def _csrf(client):
    return _hidden(client.get("/tasks"), "csrf_token")


def _prepare(client, target, action, **fields):
    opened = client.get(f"/tasks/{target}/manage/{action}")
    assert opened.status_code == 200, opened.text
    response = client.post(f"/tasks/{target}/manage/{action}", data={
        "csrf_token": _csrf(client), "draft_ticket": _hidden(opened, "draft_ticket"),
        "op": "prepare", "q": "", "page": "1", **fields})
    assert response.status_code == 200, response.text
    return _hidden(response, "ticket")


def test_web_management_edit_is_consumed_by_real_member3_activity(consumer_rows):
    from remotework.db import get_db
    from remotework.models import AuditEvent
    from remotework.write_requests import WriteReceipt
    client = _client(consumer_rows, "admin1")
    ticket = _prepare(client, 6, "edit", title="업무6 수정 이력 연결",
        description="실제 업무 웹에서 변경한 설명", start_date="2026-10-17",
        due_date=(NOW + timedelta(days=4)).date().isoformat())
    saved = client.post("/tasks/write/execute", data={"csrf_token": _csrf(client), "ticket": ticket})
    assert saved.status_code == 200 and "처리 결과가 확인되었습니다" in saved.text
    page = client.get("/results/history")
    assert page.status_code == 200
    scope = page.headers["X-Results-Session"]
    activity = client.get("/results/data/tasks/6/activity", headers={"X-Results-Session": scope})
    assert activity.status_code == 200, activity.json
    updated = [item for item in activity.json["data"]["items"] if item["kind"] == "updated"]
    assert len(updated) == 1
    item = updated[0]
    assert item["task_id"] == "6" and item["submission_id"] == ""
    assert item["actor_id"] == consumer_rows[1]["admin1"]
    assert "업무 수정" in item["summary"] and "업무명" in item["summary"]
    assert "업무 설명" in item["summary"] and "마감일" in item["summary"]
    assert "2026-10-19" in item["summary"] and "2026-10-25" in item["summary"]
    assert set(item) == {"event_id", "task_id", "submission_id", "actor_id", "actor_name",
                         "kind", "summary", "occurred_at"}
    with consumer_rows[0].app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(
            AuditEvent.action == "task.updated")) == 1
        assert db.scalar(select(func.count()).select_from(WriteReceipt).where(
            WriteReceipt.action == "task.updated")) == 1


def test_real_member3_submission_invalidates_prepared_web_manager_change(consumer_rows):
    from remotework.db import get_db
    from remotework.features.results import Submission
    from remotework.features.tasks.models import TaskManager, TaskRecord
    from remotework.models import AuditEvent
    from remotework.write_requests import WriteReceipt
    app, ids = consumer_rows
    admin = _client(consumer_rows, "admin1")
    ticket = _prepare(admin, 1, "managers",
        manager_user_ids=[str(ids["admin1"]), str(ids["admin2"])])
    api = app.test_client()
    login = api.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"})
    assert login.status_code == 200
    headers = {"Authorization": "Bearer " + login.json["data"]["access_token"]}
    issued = api.post("/api/v1/write-requests/keys", headers=headers,
        json={"action": "submission.submitted", "target": "1"})
    assert issued.status_code == 201, issued.json
    submitted = api.post("/api/v1/results/tasks/1/submissions",
        headers=headers | {"Idempotency-Key": issued.json["data"]["request_key"]},
        json={"expected_row_version": "1", "expected_assignment_revision": 1,
              "description": "실제3번 최초 제출 성공", "attachment_ids": []})
    assert submitted.status_code == 201, submitted.json
    assert submitted.json["data"]["task_status"] == "submitted"
    denied = admin.post("/tasks/write/execute", data={"csrf_token": _csrf(admin), "ticket": ticket})
    assert denied.status_code == 409
    assert "TASK_VERSION_CONFLICT" in denied.text or "TASK_STATE_CONFLICT" in denied.text
    with app.app_context():
        db = get_db()
        task = db.get(TaskRecord, 1)
        assert task.task_status == "submitted" and task.row_version == 2
        assert tuple(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == 1))) == (ids["admin1"],)
        assert db.scalar(select(func.count()).select_from(Submission).where(Submission.task_id == 1)) == 1
        assert db.scalar(select(func.count()).select_from(WriteReceipt)) == 1
        assert db.scalar(select(func.count()).select_from(WriteReceipt).where(
            WriteReceipt.action == "task.managers_changed")) == 0
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(
            AuditEvent.action == "task.managers_changed")) == 0
