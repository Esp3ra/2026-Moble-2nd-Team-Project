"""Current task detail/cancellation/pending reads on an explicitly owned MySQL.

Fixture writes are test preparation only. The web remains read-only and never
installs an integrated acknowledgment or uses a SQLite fallback.
"""
from datetime import date, datetime
import json
import re
import subprocess
import sys

import pytest
from sqlalchemy import delete, select, update

from test_task_web import _client, _error, _snapshot, web_rows
from test_task_web_lifecycle import inbox_rows


@pytest.fixture(autouse=True)
def context_web_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("COMMON_WRITE", "TASK_CREATE", "TASK_START", "TASK_MANAGEMENT",
                 "TASK_LIFECYCLE_RESULT_READS", "RESULTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT",
                 "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT", "1")


@pytest.fixture
def context_rows(inbox_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation
    app, _ids = inbox_rows
    with app.app_context(), transaction() as db:
        row = db.get(TaskCancellation, 9)
        row.reason = "취소 사유 첫 줄\n취소 사유 다음 줄"
        row.cancelled_at = datetime(2026, 10, 21, 1, 2, 3)
    return inbox_rows


def _section(page, heading_id):
    match = re.search(r'<section\b(?=[^>]*aria-labelledby="' + re.escape(heading_id)
                      + r'")[^>]*>(.*?)</section>', page.text, re.S)
    assert match is not None, heading_id
    return match.group(1)


def _no_cards(page):
    assert "task-cancellation-heading" not in page.text
    assert "task-pending-extension-heading" not in page.text


def _bearer(rows, name="staff1"):
    client = rows[0].test_client()
    password = {"staff1": "Staff-test-2026!", "admin1": "Admin-test-2026!"}.get(name, "Task-web-test-2026!")
    response = client.post("/api/v1/auth/login", json={"login_id": name, "password": password})
    assert response.status_code == 200
    return client, {"Authorization": "Bearer " + response.json["data"]["access_token"]}


def test_detail_query_only_remains_cold_without_lifecycle_import_or_new_methods():
    code = """
import json, sys
from remotework import create_app
from remotework.models import Base
app = create_app({'TESTING': True, 'SECRET_KEY': 'detail-cold-'+'a'*40,
    'DATABASE_URL': 'mysql+pymysql://none:none@127.0.0.1:1/none',
    'ENABLE_TASK_QUERY_DRAFT': True, 'ENABLE_COMMON_WRITE_DRAFT': False,
    'ENABLE_TASK_LIFECYCLE_DRAFT': False})
rules = [r for r in app.url_map.iter_rules() if r.endpoint == 'task_web.detail']
print(json.dumps({'models_imported': 'remotework.features.tasks.lifecycle_models' in sys.modules,
    'lifecycle_tables': sorted(set(Base.metadata.tables)&{'task_cancellations','task_deadline_extensions'}),
    'methods': sorted(set().union(*(r.methods for r in rules))),
    'anonymous': app.test_client().get('/tasks/1').status_code}))
app.extensions['db_engine'].dispose()
"""
    child = subprocess.run([sys.executable, "-c", code], capture_output=True,
                           text=True, encoding="utf-8", check=True)
    result = json.loads(child.stdout)
    assert result == {"models_imported": False, "lifecycle_tables": [],
                      "methods": ["GET", "HEAD", "OPTIONS"], "anonymous": 302}


@pytest.mark.parametrize("status, label", [
    ("not_started", "진행전"), ("in_progress", "진행중"), ("returned", "반려완료"),
])
def test_detail_cancel_information_keeps_the_existing_task_state(context_rows, status, label):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord
    with context_rows[0].app_context(), transaction() as db:
        db.get(TaskRecord, 9).task_status = status
    for name in ("staff1", "admin1"):
        page = _client(context_rows, name).get("/tasks/9?scope=managed&include_cancelled=true&status=" + status)
        assert page.status_code == 200 and label in page.text and "취소됨" in page.text
        card = _section(page, "task-cancellation-heading")
        assert "업무 취소 정보" in card and "취소 일시" in card and "취소 사유" in card
        assert "2026-10-21 01:02:03 UTC" in card
        assert "취소 사유 첫 줄\n취소 사유 다음 줄" in card
        assert "task-pending-extension-heading" not in page.text


def test_detail_pending_card_is_current_request_with_exact_dates_and_reason(context_rows):
    for name in ("staff1", "admin1"):
        page = _client(context_rows, name).get("/tasks/1?scope=own&status=in_progress")
        assert page.status_code == 200 and "진행중" in page.text
        card = _section(page, "task-pending-extension-heading")
        assert "마감일 연장 대기" in card
        assert "신청 당시 마감일" in card and "2026-10-20" in card
        assert "희망 마감일" in card and "2026-10-23" in card
        assert "신청 사유 11\n두 번째 줄" in card
        assert "2026-10-21 02:03:04 UTC" in card
        assert "처리 사유 18" not in card
        assert "task-cancellation-heading" not in page.text
        assert 'href="/tasks?scope=own&amp;include_cancelled=false&amp;status=in_progress"' in page.text


def test_detail_closed_requests_do_not_look_like_pending(context_rows):
    client = _client(context_rows)
    for task_id in (4, 5, 6):
        page = client.get("/tasks/" + str(task_id))
        assert page.status_code == 200
        _no_cards(page)
    cancelled = client.get("/tasks/9")
    assert cancelled.status_code == 200 and "task-pending-extension-heading" not in cancelled.text


def test_detail_previous_assignment_pending_is_not_current_pending(context_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    app, ids = context_rows
    with app.app_context(), transaction() as db:
        db.add(TaskDeadlineExtension(extension_id=19, task_id=8, assignment_revision=1,
            requested_by=ids["staff1"], original_due_date=date(2026, 10, 20),
            requested_due_date=date(2026, 10, 24), reason="이전 배정의 대기 사유",
            status="pending", created_at=datetime(2026, 10, 21, 2, 3, 4)))
    for name in ("staff2", "admin2"):
        page = _client(context_rows, name).get("/tasks/8")
        assert page.status_code == 200
        _no_cards(page)
        assert "이전 배정의 대기 사유" not in page.text
    _error(_client(context_rows).get("/tasks/8"), 403)


def test_detail_context_does_not_expand_own_or_managed_permission(context_rows):
    staff = _client(context_rows)
    other_admin = _client(context_rows, "admin2")
    unrelated = _client(context_rows, "empty1")
    # Generic grants and a TaskManager row for a non-admin still grant no read.
    _error(staff.get("/tasks/2?scope=own", headers={"X-Role": "admin"}), 403)
    for client in (other_admin, unrelated):
        denied = client.get("/tasks/1?scope=managed&include_cancelled=true")
        _error(denied, 403)
        assert "신청 사유 11" not in denied.text
        _no_cards(denied)
    _error(staff.get("/tasks/999999"), 404)


def test_detail_context_revokes_removed_manager_and_old_assignee(context_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    app, ids = context_rows
    admin = _client(context_rows, "admin1")
    staff = _client(context_rows)
    with app.app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == 1, TaskManager.user_id == ids["admin1"]))
    _error(admin.get("/tasks/1"), 403)
    with app.app_context(), transaction() as db:
        db.add(TaskAssignment(task_id=1, revision=2, assignee_id=ids["staff2"], assigned_by=ids["admin2"]))
        db.get(TaskRecord, 1).current_assignment_revision = 2
    _error(staff.get("/tasks/1"), 403)


@pytest.mark.parametrize("change", ["reassignment", "cancellation", "manager_removal"])
def test_detail_reads_current_facts_after_initial_identity_snapshot(context_rows, change):
    """Commit a real competing write after load_identity's consistent read.

    A plain later SELECT on the same REPEATABLE READ connection can still see
    the old join. The rendered task and lifecycle facts must agree on the new
    guarded/current rows, or deny the now-removed relationship entirely.
    """
    from flask import request
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.models import AdminGuard
    app, ids = context_rows
    changed = []

    @app.before_request
    def commit_between_identity_and_view():
        if request.path != "/tasks/1" or changed:
            return
        with app.extensions["db_engine"].begin() as connection:
            connection.execute(select(AdminGuard.id).where(AdminGuard.id == 1).with_for_update()).one()
            connection.execute(select(TaskRecord.task_id).where(TaskRecord.task_id == 1).with_for_update()).one()
            if change == "reassignment":
                connection.execute(TaskAssignment.__table__.insert().values(task_id=1, revision=2,
                    assignee_id=ids["staff2"], assigned_by=ids["admin1"],
                    assigned_at=datetime(2026, 10, 21, 3, 4, 5), accepted_at=None))
                connection.execute(update(TaskRecord).where(TaskRecord.task_id == 1).values(
                    current_assignment_revision=2, row_version=TaskRecord.row_version + 1,
                    due_date=date(2026, 10, 24)))
                connection.execute(TaskDeadlineExtension.__table__.insert().values(extension_id=20,
                    task_id=1, assignment_revision=2, requested_by=ids["staff2"],
                    original_due_date=date(2026, 10, 24), requested_due_date=date(2026, 10, 28),
                    reason="현재 새 배정의 대기 신청", status="pending",
                    created_at=datetime(2026, 10, 21, 3, 4, 5)))
            elif change == "cancellation":
                connection.execute(TaskCancellation.__table__.insert().values(task_id=1,
                    cancelled_by=ids["admin1"], reason="조회 직전 확정된 취소",
                    cancelled_at=datetime(2026, 10, 21, 3, 4, 5)))
                connection.execute(update(TaskDeadlineExtension).where(
                    TaskDeadlineExtension.extension_id == 11).values(status="cancelled",
                    decided_by=ids["admin1"], decided_at=datetime(2026, 10, 21, 3, 4, 5),
                    decision_reason="업무 취소"))
            else:
                connection.execute(delete(TaskManager).where(TaskManager.task_id == 1,
                    TaskManager.user_id == ids["admin1"]))
        changed.append(True)

    page = _client(context_rows, "admin1").get("/tasks/1?scope=managed&include_cancelled=true")
    assert changed == [True]
    if change == "manager_removal":
        _error(page, 403)
        _no_cards(page)
        assert "업무 본문 1" not in page.text and "신청 사유 11" not in page.text
    elif change == "reassignment":
        assert page.status_code == 200
        assert re.search(r"<dt>담당자</dt>\s*<dd>staff2</dd>", page.text)
        card = _section(page, "task-pending-extension-heading")
        assert "현재 새 배정의 대기 신청" in card and "2026-10-24" in card and "2026-10-28" in card
        assert "신청 사유 11" not in page.text
    else:
        assert page.status_code == 200 and "취소됨" in page.text
        assert "조회 직전 확정된 취소" in _section(page, "task-cancellation-heading")
        assert "task-pending-extension-heading" not in page.text


def test_detail_rechecks_cookie_identity_before_showing_context(context_rows):
    from flask import request
    from remotework.models import AdminGuard, User
    app, ids = context_rows
    changed = []

    @app.before_request
    def deactivate_after_identity_load():
        if request.path != "/tasks/1" or changed:
            return
        with app.extensions["db_engine"].begin() as connection:
            connection.execute(select(AdminGuard.id).where(AdminGuard.id == 1).with_for_update()).one()
            connection.execute(update(User).where(User.id == ids["admin1"]).values(is_active=False))
        changed.append(True)

    page = _client(context_rows, "admin1").get("/tasks/1")
    assert changed == [True]
    assert page.status_code == 302 and page.location.endswith("/login")
    assert "신청 사유 11" not in page.text


def test_detail_lifecycle_off_keeps_basic_view_but_no_reason_cards(context_rows):
    from remotework import create_app
    app, _ids = context_rows
    logged_in = _client(context_rows)
    cold = create_app({"TESTING": True, "SECRET_KEY": "context-off-" + "c" * 40,
        "DATABASE_URL": app.config["DATABASE_URL"], "ENABLE_TASK_QUERY_DRAFT": True,
        "ENABLE_TASK_LIFECYCLE_DRAFT": False, "ENABLE_COMMON_WRITE_DRAFT": False})
    try:
        client = cold.test_client()
        client.set_cookie("rw_session", logged_in.get_cookie("rw_session").value)
        for task_id in (1, 9):
            page = client.get("/tasks/" + str(task_id))
            assert page.status_code == 200 and f"업무 본문 {task_id}" in page.text
            _no_cards(page)
            assert "취소 사유 첫 줄" not in page.text and "신청 사유 11" not in page.text
        assert "취소됨" in client.get("/tasks/9").text
    finally:
        cold.extensions["db_engine"].dispose()


@pytest.mark.parametrize("table", ["task_cancellations", "task_deadline_extensions"])
def test_detail_missing_lifecycle_storage_is_503_not_partial_or_empty_success(context_rows, table):
    client = _client(context_rows)
    engine = context_rows[0].extensions["db_engine"]
    temporary = table + "_context_unavailable"
    with engine.begin() as connection:
        connection.exec_driver_sql("RENAME TABLE " + table + " TO " + temporary)
    try:
        page = client.get("/tasks/1")
        _error(page, 503)
        _no_cards(page)
        assert "업무 본문 1" not in page.text and "신청 사유 11" not in page.text
    finally:
        with engine.begin() as connection:
            connection.exec_driver_sql("RENAME TABLE " + temporary + " TO " + table)


def test_detail_escapes_cancellation_and_pending_reasons_with_multiline_text(context_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    with context_rows[0].app_context(), transaction() as db:
        db.get(TaskCancellation, 9).reason = '<script>alert("cancel")</script>\n취소 다음 줄'
        db.get(TaskDeadlineExtension, 11).reason = '<img src=x onerror="alert(1)">\n신청 다음 줄'
    client = _client(context_rows)
    cancelled = client.get("/tasks/9")
    pending = client.get("/tasks/1")
    assert cancelled.status_code == pending.status_code == 200
    cancel_card = _section(cancelled, "task-cancellation-heading")
    pending_card = _section(pending, "task-pending-extension-heading")
    assert "<script>" not in cancel_card and "&lt;script&gt;" in cancel_card and "\n취소 다음 줄" in cancel_card
    assert "<img " not in pending_card and "&lt;img " in pending_card and "\n신청 다음 줄" in pending_card


def test_detail_guarded_reads_require_no_ack_and_write_nothing(context_rows):
    app, _ids = context_rows
    client = _client(context_rows)
    assert not app.config.get("ENABLE_COMMON_WRITE_DRAFT")
    assert app.extensions.get("task_lifecycle_integration_ack") is None
    login_page = client.get("/login")
    csrf = re.search(r'name="csrf_token" value="([^"]+)"', login_page.text).group(1)
    before = _snapshot(app)
    for path in ("/tasks/1", "/tasks/9?scope=managed&include_cancelled=true&status=in_progress"):
        page = client.get(path)
        assert page.status_code == 200 and page.headers["Cache-Control"] == "no-store"
        task_body = page.text.split('<div class="task-page">', 1)[1]
        # The common sidebar's logout form remains its owner's POST action.
        assert not re.search(r'<form\b[^>]*method="post"', task_body, re.I)
        assert "write_enabled" not in page.text and "assignment_revision" not in page.text
        assert "expected_row_version" not in page.text and "ACK" not in page.text
        assert client.head(path).status_code == 200
        for method in ("POST", "PATCH", "DELETE"):
            assert client.open(path, method=method, data={"csrf_token": csrf}).status_code == 405
    assert _snapshot(app) == before


def test_detail_unexpected_context_failure_is_sanitized_500(context_rows, monkeypatch):
    from remotework.features.tasks import lifecycle
    def broken(*_args, **_kwargs):
        raise RuntimeError("private-context-diagnostic-marker")
    monkeypatch.setattr(lifecycle, "read_lifecycle_detail", broken)
    page = _client(context_rows).get("/tasks/1")
    _error(page, 500)
    _no_cards(page)
    assert "private-context-diagnostic-marker" not in page.text and "업무 본문 1" not in page.text


def test_lifecycle_context_api_keeps_exact_fields_envelope_and_strict_query(context_rows):
    client, headers = _bearer(context_rows)
    expected_fields = {"task_id", "row_version", "assignment_revision", "task_status",
        "start_date", "due_date", "updated_at", "assignee_id", "manager_user_ids",
        "is_cancelled", "cancellation", "pending_extension", "write_enabled"}
    pending_fields = {"extension_id", "task_id", "assignment_revision", "requested_by",
        "original_due_date", "requested_due_date", "reason", "status", "created_at",
        "decided_at", "decided_by", "decision_reason"}
    page = client.get("/api/v1/tasks/1/lifecycle", headers=headers)
    assert page.status_code == 200
    assert set(page.json) == {"ok", "data", "request_id"} and page.json["ok"] is True
    assert set(page.json["data"]) == {"context"}
    context = page.json["data"]["context"]
    assert set(context) == expected_fields
    assert context["task_id"] == "1" and context["assignment_revision"] == 1
    assert context["cancellation"] is None and context["is_cancelled"] is False
    assert context["write_enabled"] is False
    assert set(context["pending_extension"]) == pending_fields
    assert context["pending_extension"]["extension_id"] == "11"
    cancelled = client.get("/api/v1/tasks/9/lifecycle", headers=headers).json["data"]["context"]
    assert set(cancelled) == expected_fields and cancelled["pending_extension"] is None
    assert set(cancelled["cancellation"]) == {"cancelled_by", "cancelled_at", "reason"}
    assert cancelled["cancellation"]["cancelled_at"] == "2026-10-21T01:02:03Z"
    for query in ("scope=own", "status=pending", "include_cancelled=true", "task_id=1"):
        rejected = client.get("/api/v1/tasks/1/lifecycle?" + query, headers=headers)
        assert rejected.status_code == 400 and rejected.json["ok"] is False
        assert set(rejected.json) == {"ok", "error", "request_id"}
