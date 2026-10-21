"""Owner 2 SSR writes against an explicitly owned disposable MySQL database.

The task web runner supplies its fresh remote_work_test_ database. These tests
exercise real cookie sessions, CSRF, signed confirmations and W1 transactions;
they never opt in results, install an integration ACK, or use SQLite.
"""
from datetime import datetime, timedelta
from html.parser import HTMLParser
import json
import os
import time
from types import SimpleNamespace

import pytest
from sqlalchemy import delete, func, select
from sqlalchemy.engine import make_url
from werkzeug.datastructures import MultiDict


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Task web writes require the explicitly owned disposable MySQL runner",
)
NOW = datetime(2026, 10, 21, 3, 4, 5)
FIRST = datetime(2026, 10, 20, 3, 4, 5)
ACTIONS = {"accept": "task.accepted", "rework-start": "task.rework_started"}


@pytest.fixture(autouse=True)
def web_write_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_MANAGEMENT", "TASK_LIFECYCLE", "TASK_LIFECYCLE_RESULT_READS",
                 "RESULTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT",
                 "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_START"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    from remotework.features.tasks import starting
    monkeypatch.setattr(starting, "utcnow", lambda: NOW)


@pytest.fixture
def write_rows(request):
    assert (make_url(os.environ["MYSQL_TEST_URL"]).database or "").startswith("remote_work_test_")
    from remotework.auth import create_user
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.models import User
    from remotework.write_requests import WriteReceipt
    # Metadata must be imported before conftest initializes/clears the owned DB.
    assert TaskCancellation and TaskDeadlineExtension and WriteReceipt
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        for name, role in (("staff2", "employee"), ("admin2", "admin")):
            users[name] = create_user(db, {"login_id": name, "password": "Web-write-test-2026!",
                "display_name": name, "employee_no": "WEB-WRITE-" + name, "role": role})
        ids = {name: user.id for name, user in users.items()}
        for task_id, assignee, state, manager in (
                (101, "staff1", "not_started", "admin1"),
                (102, "staff1", "returned", "admin1"),
                (103, "admin1", "not_started", "admin2"),
                (104, "staff2", "not_started", "admin1"),
                (105, "staff1", "submitted", "admin1"),
                (106, "staff1", "approved", "admin1"),
                (107, "staff1", "in_progress", "admin1")):
            db.add(TaskRecord(task_id=task_id, title=f"웹 업무 {task_id}",
                description="원문 본문을 보존합니다.", task_status=state,
                start_date=NOW.date(), due_date=(NOW + timedelta(days=2)).date(),
                current_assignment_revision=3, row_version=7, created_by=ids["admin1"],
                created_at=FIRST, updated_at=FIRST))
            db.flush()
            db.add(TaskAssignment(task_id=task_id, revision=3, assignee_id=ids[assignee],
                assigned_by=ids["admin1"], assigned_at=FIRST,
                accepted_at=None if state == "not_started" else FIRST))
            db.add(TaskManager(task_id=task_id, user_id=ids[manager]))
    return app, ids


class _Inputs(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.fields = {}
        self.select_name = None
        self.textarea_name = None
        self.feed(source)

    def handle_starttag(self, tag, attributes):
        values = dict(attributes)
        if (tag == "input" and values.get("name") and
                (values.get("type") not in ("checkbox", "radio") or "checked" in values)):
            self.fields.setdefault(values["name"], []).append(values.get("value", ""))
        elif tag == "select":
            self.select_name = values.get("name")
        elif tag == "option" and self.select_name and "selected" in values:
            self.fields.setdefault(self.select_name, []).append(values.get("value", ""))
        elif tag == "textarea" and values.get("name"):
            self.textarea_name = values["name"]
            self.fields.setdefault(self.textarea_name, []).append("")

    def handle_endtag(self, tag):
        if tag == "select":
            self.select_name = None
        elif tag == "textarea":
            self.textarea_name = None

    def handle_data(self, value):
        if self.textarea_name:
            self.fields[self.textarea_name][-1] += value


def _hidden(response, name):
    values = _Inputs(response.text).fields.get(name, [])
    assert values and len(set(values)) == 1, (name, response.status_code, response.text[:500])
    return values[0]


def _login(rows, user="staff1"):
    client = rows[0].test_client()
    passwords = {"staff1": "Staff-test-2026!", "admin1": "Admin-test-2026!"}
    token = _hidden(client.get("/login"), "csrf_token")
    response = client.post("/login", data={"csrf_token": token, "login_id": user,
        "password": passwords.get(user, "Web-write-test-2026!")})
    assert response.status_code == 302
    return client


def _csrf(client):
    return _hidden(client.get("/tasks"), "csrf_token")


def _prepare(client, target=101, action="accept"):
    response = client.post(f"/tasks/{target}/start/{action}", data={"csrf_token": _csrf(client)})
    assert response.status_code == 200, response.text
    return _hidden(response, "ticket")


def _send(client, ticket, endpoint="execute", **kwargs):
    return client.post("/tasks/write/" + endpoint,
        data={"csrf_token": _csrf(client), "ticket": ticket}, **kwargs)


def _snapshot(rows, target=101):
    from remotework.db import get_db
    from remotework.features.tasks.models import TaskAssignment, TaskRecord
    from remotework.models import AuditEvent
    from remotework.write_requests import WriteReceipt
    with rows[0].app_context():
        db = get_db()
        task = db.get(TaskRecord, target)
        assignment = db.get(TaskAssignment, (target, task.current_assignment_revision))
        return (task.task_status, task.row_version, task.current_assignment_revision,
                task.updated_at, assignment.accepted_at,
                db.scalar(select(func.count()).select_from(WriteReceipt)),
                db.scalar(select(func.count()).select_from(AuditEvent).where(
                    AuditEvent.action.in_(tuple(ACTIONS.values()) + ("task.created",)))))


def _changed(rows, target=101, **changes):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord
    with rows[0].app_context(), transaction() as db:
        task = db.get(TaskRecord, target)
        for name, value in changes.items():
            setattr(task, name, value)


@pytest.mark.parametrize("action,target", [("accept", 101), ("rework-start", 102)])
def test_web_start_prepare_is_read_and_explicit_execute_saves_once(write_rows, action, target):
    from remotework.db import get_db
    from remotework.models import AuditEvent
    from remotework.write_requests import WriteReceipt
    client = _login(write_rows)
    before = _snapshot(write_rows, target)
    ticket = _prepare(client, target, action)
    assert _snapshot(write_rows, target) == before
    response = _send(client, ticket)
    assert response.status_code in (200, 302, 303), response.text
    assert _snapshot(write_rows, target) == (
        "in_progress", 8, 3, NOW, FIRST if action == "rework-start" else NOW, 1, 1)
    # Old expected/state/date must not turn a recorded success into new mutation.
    _changed(write_rows, target, task_status="submitted")
    after = _snapshot(write_rows, target)
    assert _send(client, ticket).status_code in (200, 302, 303)
    assert _send(client, ticket, "result").status_code == 200
    assert _snapshot(write_rows, target) == after
    with write_rows[0].app_context():
        db = get_db()
        receipt = db.scalar(select(WriteReceipt))
        assert receipt.action == ACTIONS[action] and receipt.target == str(target)
        result = json.loads(receipt.result_json)
        assert set(result) == {"task_id", "row_version", "assignment_revision", "task_status", "updated_at"}
        assert result["task_id"] == str(target) and result["row_version"] == "8"
        assert db.scalar(select(AuditEvent).where(AuditEvent.action == ACTIONS[action])).resource_id == str(target)
    assert "task_lifecycle_integration_ack" not in write_rows[0].extensions


def test_web_admin_can_accept_own_task_without_being_its_manager(write_rows):
    client = _login(write_rows, "admin1")
    ticket = _prepare(client, 103)
    assert _send(client, ticket).status_code in (200, 302, 303)
    assert _snapshot(write_rows, 103)[:3] == ("in_progress", 8, 3)


@pytest.mark.parametrize("actor", ["staff2", "admin1", "admin2"])
def test_web_foreign_assignee_and_manager_cannot_prepare_start(write_rows, actor):
    client = _login(write_rows, actor)
    before = _snapshot(write_rows)
    response = client.post("/tasks/101/start/accept", data={"csrf_token": _csrf(client)})
    assert response.status_code == 403
    assert _snapshot(write_rows) == before


@pytest.mark.parametrize("target,action", [(105, "accept"), (106, "accept"), (107, "accept"),
                                          (101, "rework-start"), (102, "accept")])
def test_web_start_rejects_wrong_source_state(write_rows, target, action):
    client = _login(write_rows)
    before = _snapshot(write_rows, target)
    response = client.post(f"/tasks/{target}/start/{action}", data={"csrf_token": _csrf(client)})
    assert response.status_code == 409
    assert _snapshot(write_rows, target) == before


@pytest.mark.parametrize("path", ["/tasks/101/start/accept", "/tasks/write/execute", "/tasks/write/result"])
def test_web_write_requires_common_csrf(write_rows, path):
    client = _login(write_rows)
    assert client.post(path, data={"ticket": "unused"}).status_code == 400
    assert _snapshot(write_rows)[-2:] == (0, 0)


@pytest.mark.parametrize("endpoint", ["execute", "result"])
def test_web_confirmation_rejects_tamper_duplicate_extra_and_query(write_rows, endpoint):
    client = _login(write_rows)
    ticket = _prepare(client)
    token = _csrf(client)
    before = _snapshot(write_rows)
    assert client.post("/tasks/write/" + endpoint, data={"csrf_token": token,
        "ticket": ticket + "a"}).status_code == 400
    assert client.post("/tasks/write/" + endpoint, data=MultiDict([
        ("csrf_token", token), ("ticket", ticket), ("ticket", ticket)])).status_code == 400
    assert client.post("/tasks/write/" + endpoint, data={"csrf_token": token,
        "ticket": ticket, "expected_row_version": "8"}).status_code == 400
    assert client.post("/tasks/write/" + endpoint + "?task_id=104", data={"csrf_token": token,
        "ticket": ticket}).status_code == 400
    assert _snapshot(write_rows) == before


@pytest.mark.parametrize("endpoint", ["execute", "result"])
def test_web_ticket_is_bound_to_exact_login_not_only_user(write_rows, endpoint):
    first = _login(write_rows)
    ticket = _prepare(first)
    second = _login(write_rows)  # Same user, distinct web AuthSession.
    before = _snapshot(write_rows)
    assert _send(second, ticket, endpoint).status_code == 403
    assert _snapshot(write_rows) == before
    foreign = _login(write_rows, "staff2")
    assert _send(foreign, ticket, endpoint).status_code == 403
    assert _snapshot(write_rows) == before


def test_web_prepare_and_result_not_recorded_do_not_implicitly_retry(write_rows):
    client = _login(write_rows)
    before = _snapshot(write_rows)
    ticket = _prepare(client)
    result = _send(client, ticket, "result")
    assert result.status_code == 200
    assert "저장된 처리 결과가 없습니다" in result.text
    retry_ticket = _hidden(result, "ticket")
    assert _snapshot(write_rows) == before
    assert _send(client, retry_ticket).status_code == 200
    assert _snapshot(write_rows)[-2:] == (1, 1)


def test_web_execute_rechecks_prepared_version_without_changing_business_data(write_rows):
    client = _login(write_rows)
    ticket = _prepare(client)
    _changed(write_rows, row_version=8)
    before = _snapshot(write_rows)
    assert _send(client, ticket).status_code == 409
    assert _snapshot(write_rows) == before


@pytest.mark.parametrize("endpoint,completed", [("execute", False), ("execute", True), ("result", True)])
def test_web_execute_and_recorded_result_recheck_current_assignment(write_rows, endpoint, completed):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskRecord
    client = _login(write_rows)
    ticket = _prepare(client)
    if completed:
        assert _send(client, ticket).status_code in (200, 302, 303)
    with write_rows[0].app_context(), transaction() as db:
        db.add(TaskAssignment(task_id=101, revision=4, assignee_id=write_rows[1]["staff2"],
            assigned_by=write_rows[1]["admin1"], assigned_at=NOW))
        db.get(TaskRecord, 101).current_assignment_revision = 4
    before = _snapshot(write_rows)
    assert _send(client, ticket, endpoint).status_code == 403
    assert _snapshot(write_rows) == before


def test_web_persisted_cancellation_blocks_start_even_when_lifecycle_flag_off(write_rows):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation
    client = _login(write_rows)
    ticket = _prepare(client)
    with write_rows[0].app_context(), transaction() as db:
        db.add(TaskCancellation(task_id=101, cancelled_by=write_rows[1]["admin1"],
            reason="검증 중 취소", cancelled_at=NOW))
    before = _snapshot(write_rows)
    assert _send(client, ticket).status_code == 409
    assert _snapshot(write_rows) == before


@pytest.mark.parametrize("instant,expected", [
    (datetime(2026, 10, 20, 14, 59, 59), 409),
    (datetime(2026, 10, 20, 15), 200),
    (datetime(2026, 10, 23, 14, 59, 59), 200),
    (datetime(2026, 10, 23, 15), 409),
])
def test_web_execute_uses_current_kst_date_after_prepare(write_rows, monkeypatch, instant, expected):
    from remotework.features.tasks import starting
    client = _login(write_rows)
    ticket = _prepare(client)
    monkeypatch.setattr(starting, "utcnow", lambda: instant)
    before = _snapshot(write_rows)
    response = _send(client, ticket)
    assert response.status_code in ((200, 302, 303) if expected == 200 else (409,)), response.text
    if expected == 409:
        assert _snapshot(write_rows) == before


def test_web_revoked_session_cannot_execute_prepared_confirmation(write_rows):
    from remotework.db import transaction
    from remotework.models import AuthSession
    client = _login(write_rows)
    token = _csrf(client)
    ticket = _prepare(client)
    with write_rows[0].app_context(), transaction() as db:
        db.execute(delete(AuthSession).where(AuthSession.user_id == write_rows[1]["staff1"]))
    before = _snapshot(write_rows)
    response = client.post("/tasks/write/execute", data={"csrf_token": token, "ticket": ticket})
    assert response.status_code == 302 and response.headers["Location"].endswith("/login")
    assert _snapshot(write_rows) == before


def _creation(client, rows, **changes):
    fields = {"csrf_token": _csrf(client), "op": "prepare", "title": "  등록 원문 😀  ",
        "description": "첫 줄\n<script>실행 금지</script>", "start_date": NOW.date().isoformat(),
        "due_date": (NOW + timedelta(days=2)).date().isoformat(),
        "assignee_id": str(rows[1]["staff1"]), "manager_user_ids": [str(rows[1]["admin2"])],
        "q": "", "page": "1"}
    fields.update(changes)
    return client.post("/tasks/new", data=fields)


def test_web_creation_requires_admin_and_remains_separate_from_existing_reads(write_rows):
    staff = _login(write_rows)
    assert staff.get("/tasks/new").status_code == 403
    assert _creation(staff, write_rows).status_code == 403
    admin = _login(write_rows, "admin1")
    page = admin.get("/tasks/new")
    assert page.status_code == 200 and "업무 등록" in page.text
    assert "assignee_id" in page.text and "manager_user_ids" in page.text
    assert _snapshot(write_rows)[-2:] == (0, 0)


def test_web_creation_confirmation_is_read_then_original_body_and_managers_save_once(write_rows):
    from remotework.db import get_db, transaction
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.models import AuditEvent, User
    from remotework.write_requests import WriteReceipt
    client = _login(write_rows, "admin1")
    response = _creation(client, write_rows)
    assert response.status_code == 200 and "업무 등록" in response.text
    assert "<script>실행 금지</script>" not in response.text
    ticket = _hidden(response, "ticket")
    assert _snapshot(write_rows)[-2:] == (0, 0)
    complete = _send(client, ticket)
    assert complete.status_code == 200 and "처리 결과가 확인되었습니다" in complete.text
    with write_rows[0].app_context():
        db = get_db()
        tasks = db.scalars(select(TaskRecord).where(TaskRecord.task_id > 107)).all()
        assert len(tasks) == 1
        task = tasks[0]
        assert (task.title, task.description, task.task_status, task.row_version,
                task.current_assignment_revision, task.created_by) == (
            "  등록 원문 😀  ", "첫 줄\n<script>실행 금지</script>", "not_started", 1, 1, write_rows[1]["admin1"])
        assignment = db.get(TaskAssignment, (task.task_id, 1))
        assert assignment.assignee_id == write_rows[1]["staff1"] and assignment.accepted_at is None
        assert set(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == task.task_id))) == {
            write_rows[1]["admin1"], write_rows[1]["admin2"]}
        receipt = db.scalar(select(WriteReceipt))
        assert receipt.action == "task.created" and receipt.target == "new" and receipt.http_status == 201
        assert json.loads(receipt.result_json)["task_id"] == str(task.task_id)
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "task.created")) == 1
    # A saved success does not recheck now-changed candidate availability.
    with write_rows[0].app_context(), transaction() as db:
        db.get(User, write_rows[1]["staff1"]).is_active = False
    assert _send(client, ticket).status_code == 200
    assert _send(client, ticket, "result").status_code == 200
    with write_rows[0].app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(TaskRecord)) == 8
        assert db.scalar(select(func.count()).select_from(WriteReceipt)) == 1


def test_web_creation_empty_requested_managers_still_includes_current_creator(write_rows):
    from remotework.db import get_db
    from remotework.features.tasks.models import TaskManager, TaskRecord
    client = _login(write_rows, "admin1")
    response = _creation(client, write_rows, manager_user_ids=[])
    assert response.status_code == 200
    assert _send(client, _hidden(response, "ticket")).status_code == 200
    with write_rows[0].app_context():
        db = get_db()
        task = db.scalar(select(TaskRecord).where(TaskRecord.task_id > 107))
        assert list(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == task.task_id))) == [
            write_rows[1]["admin1"]]


def test_web_creation_search_preserves_inputs_without_preparing_or_writing(write_rows):
    client = _login(write_rows, "admin1")
    page = _creation(client, write_rows, op="search", q="staff", page="1")
    assert page.status_code == 200
    assert _hidden(page, "title") == "  등록 원문 😀  "
    assert _hidden(page, "assignee_id") == str(write_rows[1]["staff1"])
    assert str(write_rows[1]["admin2"]) in _Inputs(page.text).fields.get("manager_user_ids", [])
    assert "<script>실행 금지</script>" not in page.text
    assert "ticket" not in _Inputs(page.text).fields
    assert _snapshot(write_rows)[-2:] == (0, 0)


@pytest.mark.parametrize("changes", [
    {"title": " \t "}, {"title": "가" * 201},
    {"start_date": "2026-02-30"}, {"due_date": "2026-10-20"},
    {"assignee_id": "01"}, {"assignee_id": "true"},
    {"unknown": "client-owned-state"}, {"q": "가" * 81}, {"page": "0"},
])
def test_web_creation_strict_inputs_never_write(write_rows, changes):
    client = _login(write_rows, "admin1")
    assert _creation(client, write_rows, **changes).status_code == 400
    assert _snapshot(write_rows)[-2:] == (0, 0)


def test_web_creation_rejects_duplicate_manager_and_rechecks_candidates_at_execute(write_rows):
    from remotework.db import transaction
    from remotework.models import User
    client = _login(write_rows, "admin1")
    manager = str(write_rows[1]["admin2"])
    assert _creation(client, write_rows, manager_user_ids=[manager, manager]).status_code == 400
    prepared = _creation(client, write_rows)
    assert prepared.status_code == 200
    ticket = _hidden(prepared, "ticket")
    with write_rows[0].app_context(), transaction() as db:
        db.get(User, write_rows[1]["staff1"]).is_active = False
    assert _send(client, ticket).status_code == 409
    assert _snapshot(write_rows)[-2:] == (0, 0)


def test_web_cookie_does_not_authenticate_api_and_api_token_does_not_authenticate_forms(write_rows):
    web = _login(write_rows)
    assert web.post("/api/v1/tasks/101/accept", json={
        "expected_row_version": "7", "expected_assignment_revision": 3}).status_code == 401
    api = write_rows[0].test_client()
    login = api.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"})
    assert login.status_code == 200
    csrf = _hidden(api.get("/login"), "csrf_token")
    attempt = api.post("/tasks/101/start/accept", data={"csrf_token": csrf},
        headers={"Authorization": "Bearer " + login.json["data"]["access_token"]})
    assert attempt.status_code == 302 and attempt.headers["Location"].endswith("/login")
    assert _snapshot(write_rows)[-2:] == (0, 0)


def test_web_failed_audit_rolls_back_and_requires_result_check_before_retry(write_rows, monkeypatch):
    from remotework.common import AppError
    from remotework.features.tasks import starting
    client = _login(write_rows)
    ticket = _prepare(client)
    before = _snapshot(write_rows)
    original = starting.audit

    def fail_audit(*_args, **_kwargs):
        raise AppError(503, "TEST_AUDIT_UNAVAILABLE", "검증용 감사 저장 실패")

    monkeypatch.setattr(starting, "audit", fail_audit)
    failed = _send(client, ticket)
    assert failed.status_code == 503
    recovery = _hidden(failed, "ticket")
    assert 'action="/tasks/write/execute"' not in failed.text
    assert _snapshot(write_rows) == before
    monkeypatch.setattr(starting, "audit", original)
    assert _send(client, recovery).status_code == 409
    assert _snapshot(write_rows) == before
    checked = _send(client, recovery, "result")
    assert checked.status_code == 200 and "저장된 처리 결과가 없습니다" in checked.text
    assert _snapshot(write_rows) == before
    assert _send(client, _hidden(checked, "ticket")).status_code == 200
    assert _snapshot(write_rows)[-2:] == (1, 1)


def test_web_response_loss_after_commit_recovers_recorded_success_without_second_write(write_rows, monkeypatch):
    from sqlalchemy.exc import SQLAlchemyError
    from remotework.features.tasks import web_writes
    client = _login(write_rows)
    ticket = _prepare(client)
    original = web_writes._page
    dropped = False

    def lose_first_success(intent, **kwargs):
        nonlocal dropped
        if kwargs.get("completed") is not None and not dropped:
            dropped = True
            raise SQLAlchemyError("Synthetic post-commit response loss")
        return original(intent, **kwargs)

    monkeypatch.setattr(web_writes, "_page", lose_first_success)
    failed = _send(client, ticket)
    assert failed.status_code == 503
    assert _snapshot(write_rows)[:3] == ("in_progress", 8, 3)
    assert _snapshot(write_rows)[-2:] == (1, 1)
    recovery = _hidden(failed, "ticket")
    assert 'action="/tasks/write/execute"' not in failed.text
    recovered = _send(client, recovery, "result")
    assert recovered.status_code == 200 and "처리 결과가 확인되었습니다" in recovered.text
    assert 'class="task-page task-write-page task-write-completed"' in recovered.text
    assert _snapshot(write_rows)[-2:] == (1, 1)


def test_web_result_failure_never_becomes_not_recorded_or_enables_retry(write_rows, monkeypatch):
    from remotework.common import AppError
    from remotework import write_requests
    client = _login(write_rows)
    ticket = _prepare(client)
    original = write_requests.lookup_result

    def unavailable(*_args, **_kwargs):
        raise AppError(503, "TEST_RESULT_UNAVAILABLE", "검증용 결과 조회 실패")

    monkeypatch.setattr(write_requests, "lookup_result", unavailable)
    failed = _send(client, ticket, "result")
    assert failed.status_code == 503
    assert "저장된 처리 결과가 없습니다" not in failed.text
    assert 'action="/tasks/write/execute"' not in failed.text
    recovery = _hidden(failed, "ticket")
    assert _snapshot(write_rows)[-2:] == (0, 0)
    assert _send(client, recovery).status_code == 409
    assert _snapshot(write_rows)[-2:] == (0, 0)
    monkeypatch.setattr(write_requests, "lookup_result", original)
    checked = _send(client, recovery, "result")
    assert checked.status_code == 200 and "저장된 처리 결과가 없습니다" in checked.text
    assert _snapshot(write_rows)[-2:] == (0, 0)


def test_web_expired_execute_key_can_still_recover_within_success_retention(write_rows, monkeypatch):
    from remotework import write_requests
    # Fixture-only TTL and virtual key clock; no sleeping or production policy change.
    write_rows[0].config["WRITE_REQUEST_TTL_SECONDS"] = 60
    clock = [int(time.time())]
    monkeypatch.setattr(write_requests, "time", SimpleNamespace(time=lambda: clock[0]))
    client = _login(write_rows)
    ticket = _prepare(client)
    clock[0] += 30
    assert _send(client, ticket).status_code == 200
    before = _snapshot(write_rows)
    clock[0] += 31  # Key has expired, successful receipt still has 29 seconds.
    expired = _send(client, ticket)
    assert expired.status_code == 410
    assert _snapshot(write_rows) == before
    found = _send(client, _hidden(expired, "ticket"), "result")
    assert found.status_code == 200 and "처리 결과가 확인되었습니다" in found.text
    assert _snapshot(write_rows) == before
