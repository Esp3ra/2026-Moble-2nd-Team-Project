"""Cookie-authenticated extension inbox reads on the owned MySQL web fixture.

Run independently or beside test_task_web.py with the same explicit owned DB
runner. No product write, integrated acknowledgment or SQLite fallback is used.
"""
from datetime import date, datetime
import json
import re
import subprocess
import sys

import pytest
from sqlalchemy import delete

from test_task_web import _client, _snapshot, web_rows


@pytest.fixture(autouse=True)
def lifecycle_web_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("COMMON_WRITE", "TASK_CREATE", "TASK_START", "TASK_MANAGEMENT",
                 "TASK_LIFECYCLE_RESULT_READS", "RESULTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT", "1")


@pytest.fixture
def inbox_rows(web_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    app, ids = web_rows
    now = datetime(2026, 10, 21, 2, 3, 4)
    with app.app_context(), transaction() as db:
        for extension_id, task_id, revision, requester, status in (
            (11, 1, 1, "staff1", "pending"), (12, 4, 1, "staff1", "approved"),
            (13, 5, 1, "staff1", "rejected"), (14, 6, 1, "staff1", "withdrawn"),
            (15, 9, 1, "staff1", "cancelled"), (16, 2, 1, "staff2", "pending"),
            (17, 8, 1, "staff1", "rejected"), (18, 1, 1, "staff2", "rejected"),
        ):
            db.add(TaskDeadlineExtension(extension_id=extension_id, task_id=task_id,
                assignment_revision=revision, requested_by=ids[requester],
                original_due_date=date(2026, 10, 19) if status == "approved" else date(2026, 10, 20),
                requested_due_date=date(2026, 10, 20) if status == "approved" else date(2026, 10, 23),
                reason=f"신청 사유 {extension_id}\n두 번째 줄", status=status, created_at=now,
                decided_at=None if status == "pending" else now,
                decided_by=None if status == "pending" else ids["admin1"],
                decision_reason=None if status in ("pending", "approved", "withdrawn") else f"처리 사유 {extension_id}"))
    return app, ids


def _ids(response):
    assert response.status_code == 200
    return re.findall(r'data-extension-id="([0-9]+)"', response.text)


def _error(response, status):
    assert response.status_code == status
    assert response.mimetype == "text/html"
    assert "요청을 확인해 주세요" in response.text
    assert "조회 조건에 맞는 연장 신청이 없습니다." not in response.text


def test_inbox_uses_only_existing_lifecycle_opt_in_and_query_only_imports_stay_cold():
    code = """
import json, sys
from remotework import create_app
from remotework.models import Base
enabled = sys.argv[1] == '1'
app = create_app({'TESTING': True, 'SECRET_KEY': 'inbox-cold-'+'a'*40,
    'DATABASE_URL': 'mysql+pymysql://none:none@127.0.0.1:1/none',
    'ENABLE_TASK_QUERY_DRAFT': True, 'ENABLE_COMMON_WRITE_DRAFT': False,
    'ENABLE_TASK_LIFECYCLE_DRAFT': enabled})
rules = [r for r in app.url_map.iter_rules() if r.endpoint == 'task_web.extension_requests']
print(json.dumps({'rules': [str(r) for r in rules],
    'methods': sorted(set().union(*(r.methods for r in rules))),
    'models_imported': 'remotework.features.tasks.lifecycle_models' in sys.modules,
    'lifecycle_tables': sorted(set(Base.metadata.tables)&{'task_cancellations','task_deadline_extensions'}),
    'status': app.test_client().get('/tasks/extension-requests').status_code}))
app.extensions['db_engine'].dispose()
"""
    for enabled in (False, True):
        child = subprocess.run([sys.executable, "-c", code, "1" if enabled else "0"],
            capture_output=True, text=True, encoding="utf-8", check=True)
        actual = json.loads(child.stdout)
        assert actual["rules"] == (["/tasks/extension-requests"] if enabled else [])
        assert actual["methods"] == (["GET", "HEAD", "OPTIONS"] if enabled else [])
        assert actual["models_imported"] is enabled
        assert actual["lifecycle_tables"] == (["task_cancellations", "task_deadline_extensions"] if enabled else [])
        assert actual["status"] == (302 if enabled else 404)


def test_inbox_own_list_and_five_request_statuses_are_separate_from_task_states(inbox_rows):
    client = _client(inbox_rows)
    assert _ids(client.get("/tasks/extension-requests")) == ["15", "14", "13", "12", "11"]
    for status, extension_id in (("pending", "11"), ("approved", "12"), ("rejected", "13"),
                                 ("withdrawn", "14"), ("cancelled", "15")):
        assert _ids(client.get("/tasks/extension-requests?scope=own&status=" + status)) == [extension_id]
    page = client.get("/tasks/extension-requests?status=pending")
    assert "신청 사유 11\n두 번째 줄" in page.text
    assert "2026-10-20" in page.text and "2026-10-23" in page.text
    assert "2026-10-21 02:03:04 UTC" in page.text
    assert 'href="/tasks/1?' in page.text
    assert 'name="status" value="pending"' in page.text
    assert "마감일 연장 신청함" in client.get("/tasks").text
    assert "진행중" not in page.text and "expected_row_version" not in page.text
    assert "ACK" not in page.text and "write_enabled" not in page.text


def test_inbox_managed_permissions_current_revision_and_other_requester_policy(inbox_rows):
    staff = _client(inbox_rows)
    staff2 = _client(inbox_rows, "staff2")
    admin = _client(inbox_rows, "admin1")
    admin2 = _client(inbox_rows, "admin2")
    _error(staff.get("/tasks/extension-requests?scope=managed", headers={"X-Role": "admin"}), 403)
    assert _ids(staff2.get("/tasks/extension-requests")) == ["16"]
    assert _ids(admin.get("/tasks/extension-requests")) == []
    assert _ids(admin.get("/tasks/extension-requests?scope=managed")) == ["18", "15", "14", "13", "12", "11"]
    assert _ids(admin2.get("/tasks/extension-requests?scope=managed")) == ["16"]
    # Existing managed reads include another requester's current-revision row;
    # own reads require both current assignment and the actor as requester.
    assert "18" not in _ids(staff.get("/tasks/extension-requests"))
    assert "17" not in _ids(staff.get("/tasks/extension-requests"))
    assert _ids(admin.get("/tasks/extension-requests?scope=managed&status=rejected")) == ["18", "13"]


def test_inbox_revokes_manager_and_old_assignment_without_exposing_saved_history(inbox_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    app, ids = inbox_rows
    staff = _client(inbox_rows)
    admin = _client(inbox_rows, "admin1")
    with app.app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == 1, TaskManager.user_id == ids["admin1"]))
    assert "11" not in _ids(admin.get("/tasks/extension-requests?scope=managed"))
    with app.app_context(), transaction() as db:
        db.add(TaskManager(task_id=1, user_id=ids["admin1"]))
        db.add(TaskAssignment(task_id=1, revision=2, assignee_id=ids["staff2"], assigned_by=ids["admin1"]))
        db.get(TaskRecord, 1).current_assignment_revision = 2
    assert "11" not in _ids(staff.get("/tasks/extension-requests"))
    assert "11" not in _ids(admin.get("/tasks/extension-requests?scope=managed"))
    assert "18" not in _ids(admin.get("/tasks/extension-requests?scope=managed"))


@pytest.mark.parametrize("query", [
    "scope=all", "scope=", "scope=own&scope=managed", "status=", "status=not_started",
    "status=in_progress", "status=pending&status=approved", "status=Pending", "include_cancelled=true",
    "task_id=1", "extension_id=11", "user_id=1", "page=1", "sort=created_at",
])
def test_inbox_rejects_unsupported_duplicate_and_task_status_filters(inbox_rows, query):
    _error(_client(inbox_rows).get("/tasks/extension-requests?" + query), 400)


def test_inbox_cookie_auth_expiry_and_api_bearer_do_not_cross_channels(inbox_rows):
    from remotework.auth import token_digest
    from remotework.db import transaction
    from remotework.models import AuthSession
    app, _ = inbox_rows
    client = app.test_client()
    response = client.get("/tasks/extension-requests")
    assert response.status_code == 302 and response.location.endswith("/login")
    bearer = client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"}).json["data"]["access_token"]
    assert client.get("/tasks/extension-requests", headers={"Authorization": "Bearer " + bearer}).status_code == 302
    client.set_cookie("rw_session", bearer)
    assert client.get("/tasks/extension-requests").status_code == 302
    client = _client(inbox_rows)
    with app.app_context(), transaction() as db:
        db.delete(db.get(AuthSession, token_digest(client.get_cookie("rw_session").value)))
    assert client.get("/tasks/extension-requests").status_code == 302


def test_inbox_off_has_no_path_or_link_with_existing_lifecycle_storage(inbox_rows):
    from remotework import create_app
    app, _ = inbox_rows
    client = _client(inbox_rows)
    cold = create_app({"TESTING": True, "SECRET_KEY": "inbox-off-" + "b" * 40,
        "DATABASE_URL": app.config["DATABASE_URL"], "ENABLE_TASK_QUERY_DRAFT": True,
        "ENABLE_TASK_LIFECYCLE_DRAFT": False, "ENABLE_COMMON_WRITE_DRAFT": False})
    try:
        cold_client = cold.test_client()
        cold_client.set_cookie("rw_session", client.get_cookie("rw_session").value)
        assert cold_client.get("/tasks/extension-requests").status_code == 404
        page = cold_client.get("/tasks")
        assert page.status_code == 200 and "마감일 연장 신청함" not in page.text
    finally:
        cold.extensions["db_engine"].dispose()


def test_inbox_empty_and_missing_schema_failures_are_distinct(inbox_rows):
    client = _client(inbox_rows, "empty1")
    page = client.get("/tasks/extension-requests")
    assert _ids(page) == [] and "조회 조건에 맞는 연장 신청이 없습니다." in page.text
    engine = inbox_rows[0].extensions["db_engine"]
    with engine.begin() as connection:
        connection.exec_driver_sql("RENAME TABLE task_deadline_extensions TO task_web_inbox_extensions_unavailable")
    try:
        _error(client.get("/tasks/extension-requests"), 503)
    finally:
        with engine.begin() as connection:
            connection.exec_driver_sql("RENAME TABLE task_web_inbox_extensions_unavailable TO task_deadline_extensions")


def test_inbox_escapes_request_and_decision_reason_without_extra_dto_fields(inbox_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    with inbox_rows[0].app_context(), transaction() as db:
        row = db.get(TaskDeadlineExtension, 13)
        row.reason = '<script>alert("request")</script>\n다음 줄'
        row.decision_reason = '<img src=x onerror="alert(1)">'
    page = _client(inbox_rows).get("/tasks/extension-requests?status=rejected")
    assert page.status_code == 200
    assert "<script>" not in page.text and "<img " not in page.text
    assert "&lt;script&gt;" in page.text and "&lt;img " in page.text and "\n다음 줄" in page.text
    assert page.headers["Cache-Control"] == "no-store"


def test_inbox_full_list_is_not_truncated_and_gets_leave_all_rows_unchanged(inbox_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    app, ids = inbox_rows
    with app.app_context(), transaction() as db:
        for extension_id in range(100, 165):
            db.add(TaskDeadlineExtension(extension_id=extension_id, task_id=1, assignment_revision=1,
                requested_by=ids["staff1"], original_due_date=date(2026, 10, 20),
                requested_due_date=date(2026, 10, 23), reason="전체 목록 자료", status="rejected",
                created_at=datetime(2026, 10, 21), decided_at=datetime(2026, 10, 21),
                decided_by=ids["admin1"], decision_reason="조회용 처리 사유"))
    client = _client(inbox_rows)
    before = _snapshot(app)
    page = client.get("/tasks/extension-requests")
    assert _ids(page) == [str(value) for value in reversed(range(100, 165))] + ["15", "14", "13", "12", "11"]
    assert len(_ids(client.get("/tasks/extension-requests?status=rejected"))) == 66
    token = re.search(r'name="csrf_token" value="([^"]+)"', page.text).group(1)
    for method in ("POST", "PATCH", "PUT", "DELETE"):
        _error(client.open("/tasks/extension-requests", method=method, data={"csrf_token": token}), 405)
    if _snapshot(app) != before:
        pytest.fail("Extension inbox reads or rejected methods changed owned fixture database rows")
    assert "task_lifecycle_integration_ack" not in app.extensions
    assert app.config["ENABLE_COMMON_WRITE_DRAFT"] is False


def test_existing_extension_api_contract_matches_inbox_policy_after_helper_extraction(inbox_rows):
    app, _ = inbox_rows
    client = app.test_client()
    token = client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"}).json["data"]["access_token"]
    headers = {"Authorization": "Bearer " + token}
    result = client.get("/api/v1/tasks/extension-requests", headers=headers)
    assert result.status_code == 200 and result.json["ok"] is True
    assert result.json["request_id"] == result.headers["X-Request-ID"]
    assert set(result.json["data"]) == {"items"}
    items = result.json["data"]["items"]
    assert [row["extension_id"] for row in items] == ["15", "14", "13", "12", "11"]
    assert set(items[-1]) == {"extension_id", "task_id", "assignment_revision", "requested_by",
        "original_due_date", "requested_due_date", "reason", "status", "created_at", "decided_at",
        "decided_by", "decision_reason"}
    assert items[-1]["task_id"] == "1" and items[-1]["assignment_revision"] == 1
    assert isinstance(items[-1]["requested_by"], int) and items[-1]["created_at"].endswith("Z")
    assert items[-1]["decided_at"] is None and items[-1]["decision_reason"] is None
    for query in ("scope=all", "scope=own&scope=managed", "status=in_progress", "include_cancelled=true"):
        failed = client.get("/api/v1/tasks/extension-requests?" + query, headers=headers)
        assert failed.status_code == 400 and failed.json["error"]["code"] == "INVALID_INPUT"
    managed = client.get("/api/v1/tasks/extension-requests?scope=managed", headers=headers)
    assert managed.status_code == 403 and managed.json["error"]["code"] == "FORBIDDEN"
    context = client.get("/api/v1/tasks/1/lifecycle", headers=headers)
    assert context.status_code == 200 and context.json["data"]["context"]["write_enabled"] is False


def test_inbox_uses_common_sanitized_unexpected_error_screen(inbox_rows, monkeypatch):
    from remotework.features.tasks import lifecycle
    def fail(*_args):
        raise RuntimeError("private-inbox-query-failure")
    monkeypatch.setattr(lifecycle, "list_extension_requests", fail)
    response = _client(inbox_rows).get("/tasks/extension-requests")
    _error(response, 500)
    assert "private-inbox-query-failure" not in response.text
