"""Owner 2 management HTML workflow against an explicitly disposable MySQL.

These cases use real cookie authentication, CSRF, signed drafts and W1 receipts.
They do not change member 3 code, enable lifecycle writes or use SQLite.
"""
from datetime import datetime, timedelta
import json
import os

import pytest
from sqlalchemy import delete, func, inspect, select, text
from sqlalchemy.engine import make_url
from werkzeug.datastructures import MultiDict

from test_task_web_writes import _hidden


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Management web checks require the explicitly owned disposable MySQL runner",
)
NOW = datetime(2026, 10, 21, 3, 4, 5)
OLD = datetime(2026, 10, 19, 1, 2, 3)
ACTIONS = {"edit": "task.updated", "reassign": "task.reassigned",
           "managers": "task.managers_changed"}
STATES = {101: "not_started", 102: "in_progress", 103: "submitted",
          104: "approved", 105: "returned"}
FILTERS = "?scope=managed&include_cancelled=true&status=not_started"


@pytest.fixture(autouse=True)
def management_web_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_CREATE", "TASK_START", "TASK_LIFECYCLE", "TASK_LIFECYCLE_RESULT_READS",
                 "RESULTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT",
                 "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_MANAGEMENT"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    from remotework.features.tasks import management
    monkeypatch.setattr(management, "utcnow", lambda: NOW)


@pytest.fixture
def management_rows(request):
    assert (make_url(os.environ["MYSQL_TEST_URL"]).database or "").startswith("remote_work_test_")
    from remotework.auth import create_user
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.models import User
    from remotework.write_requests import WriteReceipt
    assert TaskCancellation and TaskDeadlineExtension and WriteReceipt
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        for name, role in (("staff2", "employee"), ("admin2", "admin")):
            users[name] = create_user(db, {"login_id": name, "password": "Manage-web-test-2026!",
                "display_name": name, "employee_no": "MANAGE-WEB-" + name, "role": role})
        ids = {name: user.id for name, user in users.items()}
        for target, state in STATES.items():
            db.add(TaskRecord(task_id=target, title=f"관리 웹 원문 {target}",
                description="기존 설명 <script>보존</script>", task_status=state,
                start_date=NOW.date(), due_date=(NOW + timedelta(days=2)).date(),
                current_assignment_revision=3, row_version=7, created_by=ids["admin1"],
                created_at=OLD, updated_at=OLD))
            db.flush()
            db.add(TaskAssignment(task_id=target, revision=3, assignee_id=ids["staff1"],
                assigned_by=ids["admin1"], assigned_at=OLD,
                accepted_at=None if state == "not_started" else OLD))
            db.add(TaskManager(task_id=target, user_id=ids["admin1"]))
        # Earlier unaccepted assignments must survive a genuine reassignment.
        db.add(TaskAssignment(task_id=101, revision=1, assignee_id=ids["staff2"],
            assigned_by=ids["admin1"], assigned_at=OLD, accepted_at=None))
    try:
        yield app, ids
    finally:
        # A deliberately inconsistent historical-result fixture is raw SQL;
        # remove only its own row before common metadata clears parent records.
        with app.app_context(), transaction() as db:
            if inspect(db.connection()).has_table("result_submissions"):
                db.execute(text("DELETE FROM result_submissions WHERE task_id BETWEEN 101 AND 105"))


def _login(rows, user="admin1"):
    client = rows[0].test_client()
    password = {"admin1": "Admin-test-2026!", "staff1": "Staff-test-2026!"}.get(
        user, "Manage-web-test-2026!")
    response = client.post("/login", data={"csrf_token": _hidden(client.get("/login"), "csrf_token"),
        "login_id": user, "password": password})
    assert response.status_code == 302, response.text
    return client


def _csrf(client):
    return _hidden(client.get("/tasks"), "csrf_token")


def _draft(client, action="edit", target=101):
    response = client.get(f"/tasks/{target}/manage/{action}" + FILTERS)
    assert response.status_code == 200, response.text
    return _hidden(response, "draft_ticket")


def _fields(rows, action):
    if action == "edit":
        return {"title": "  변경 제목 😀  ", "description": "변경 첫 줄\n<script>실행 금지</script>",
                "start_date": NOW.date().isoformat(),
                "due_date": (NOW + timedelta(days=3)).date().isoformat()}
    if action == "reassign":
        return {"assignee_id": str(rows[1]["staff2"])}
    return {"manager_user_ids": [str(rows[1]["admin1"]), str(rows[1]["admin2"])]}


def _prepare(client, rows, action="edit", target=101, draft=None, op="prepare", **changes):
    fields = {"csrf_token": _csrf(client), "draft_ticket": draft or _draft(client, action, target),
              "op": op, "q": "", "page": "1"} | _fields(rows, action)
    fields.update(changes)
    return client.post(f"/tasks/{target}/manage/{action}" + FILTERS, data=fields)


def _send(client, ticket, endpoint="execute"):
    return client.post("/tasks/write/" + endpoint,
        data={"csrf_token": _csrf(client), "ticket": ticket})


def _snapshot(rows, target=101):
    from remotework.db import get_db
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.models import AuditEvent
    from remotework.write_requests import WriteReceipt
    with rows[0].app_context():
        db = get_db()
        task = db.get(TaskRecord, target)
        assignments = tuple((row.revision, row.assignee_id, row.assigned_by,
            row.assigned_at, row.accepted_at) for row in db.scalars(select(TaskAssignment).where(
                TaskAssignment.task_id == target).order_by(TaskAssignment.revision)))
        managers = tuple(db.scalars(select(TaskManager.user_id).where(
            TaskManager.task_id == target).order_by(TaskManager.user_id)))
        return (task.title, task.description, task.start_date, task.due_date,
            task.task_status, task.row_version, task.current_assignment_revision, task.updated_at,
            assignments, managers, db.scalar(select(func.count()).select_from(WriteReceipt)),
            db.scalar(select(func.count()).select_from(AuditEvent).where(
                AuditEvent.action.in_(tuple(ACTIONS.values())))))


def _change_task(rows, target=101, **changes):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord
    with rows[0].app_context(), transaction() as db:
        task = db.get(TaskRecord, target)
        for name, value in changes.items():
            setattr(task, name, value)


@pytest.mark.parametrize("action", ACTIONS)
def test_management_only_flags_register_html_actions_without_create_or_start(management_rows, action):
    app = management_rows[0]
    assert "task_web.new" not in app.view_functions
    assert "task_web.start_prepare" not in app.view_functions
    client = _login(management_rows)
    assert client.get(f"/tasks/101/manage/{action}").status_code == 200
    assert client.get("/tasks/new").status_code == 404
    assert client.post("/tasks/101/start/accept", data={"csrf_token": _csrf(client)}).status_code == 404


@pytest.mark.parametrize("action", ACTIONS)
def test_management_confirmation_and_search_have_no_business_write(management_rows, action):
    client = _login(management_rows)
    before = _snapshot(management_rows)
    draft = _draft(client, action)
    search = _prepare(client, management_rows, action, draft=draft, op="search")
    assert search.status_code == 200, search.text
    prepared = _prepare(client, management_rows, action, draft=_hidden(search, "draft_ticket"))
    assert prepared.status_code == 200, prepared.text
    assert _hidden(prepared, "ticket")
    assert _snapshot(management_rows) == before
    assert "<script>실행 금지</script>" not in prepared.text


@pytest.mark.parametrize("action", ACTIONS)
def test_management_success_reuses_w1_and_preserves_original_assignment(management_rows, action):
    from remotework.db import get_db
    from remotework.models import AuditEvent
    from remotework.write_requests import WriteReceipt
    client = _login(management_rows)
    before = _snapshot(management_rows)
    prepared = _prepare(client, management_rows, action)
    assert prepared.status_code == 200, prepared.text
    ticket = _hidden(prepared, "ticket")
    saved = _send(client, ticket)
    assert saved.status_code == 200 and "처리 결과가 확인되었습니다" in saved.text
    after = _snapshot(management_rows)
    assert after[4:8] == ("not_started", 8, 4 if action == "reassign" else 3, NOW)
    assert after[-2:] == (1, 1)
    if action == "edit":
        assert after[:4] == (_fields(management_rows, action)["title"],
            _fields(management_rows, action)["description"], NOW.date(), (NOW + timedelta(days=3)).date())
        assert after[8:10] == before[8:10]
    elif action == "reassign":
        assert after[8][:-1] == before[8]
        assert after[8][-1] == (4, management_rows[1]["staff2"], management_rows[1]["admin1"], NOW, None)
        assert after[9] == before[9]
    else:
        assert after[8] == before[8]
        assert after[9] == tuple(sorted((management_rows[1]["admin1"], management_rows[1]["admin2"])))
    with management_rows[0].app_context():
        db = get_db()
        receipt = db.scalar(select(WriteReceipt))
        result = json.loads(receipt.result_json)
        assert result == {"task_id": "101", "row_version": "8",
            "assignment_revision": 4 if action == "reassign" else 3,
            "task_status": "not_started", "updated_at": "2026-10-21T03:04:05Z"}
        assert receipt.action == ACTIONS[action] and receipt.target == "101" and receipt.http_status == 200
        event = db.scalar(select(AuditEvent).where(AuditEvent.action == ACTIONS[action]))
        assert event.resource_id == "101"
    # Recorded replay must not rerun old versions/state or create another event.
    assert _send(client, ticket).status_code == 200
    assert _send(client, ticket, "result").status_code == 200
    assert _snapshot(management_rows) == after


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("target,state", STATES.items())
def test_management_five_state_policy_is_rechecked_at_execution(management_rows, action, target, state):
    client = _login(management_rows)
    allowed = state in (("not_started", "in_progress", "returned") if action == "managers" else ("not_started",))
    before = _snapshot(management_rows, target)
    response = client.get(f"/tasks/{target}/manage/{action}" + FILTERS)
    if not allowed:
        assert response.status_code == 409, response.text
        assert _snapshot(management_rows, target) == before
        return
    assert response.status_code == 200, response.text
    prepared = _prepare(client, management_rows, action, target, draft=_hidden(response, "draft_ticket"))
    assert prepared.status_code == 200, prepared.text
    _change_task(management_rows, target, task_status="submitted")
    before = _snapshot(management_rows, target)
    assert _send(client, _hidden(prepared, "ticket")).status_code == 409
    assert _snapshot(management_rows, target) == before


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("actor", ["staff1", "admin2"])
def test_management_requires_current_designated_admin_even_if_actor_is_assignee(management_rows, action, actor):
    client = _login(management_rows, actor)
    before = _snapshot(management_rows)
    assert client.get(f"/tasks/101/manage/{action}").status_code == 403
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_management_persisted_cancellation_blocks_when_lifecycle_is_disabled(management_rows, action):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation
    client = _login(management_rows)
    prepared = _prepare(client, management_rows, action)
    assert prepared.status_code == 200
    with management_rows[0].app_context(), transaction() as db:
        db.add(TaskCancellation(task_id=101, cancelled_by=management_rows[1]["admin1"],
            reason="관리 웹 확인 후 취소", cancelled_at=NOW))
    before = _snapshot(management_rows)
    assert _send(client, _hidden(prepared, "ticket")).status_code == 409
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_management_initial_draft_versions_are_not_silently_rebased_by_search(management_rows, action):
    client = _login(management_rows)
    draft = _draft(client, action)
    _change_task(management_rows, row_version=8, title="다른 관리자가 바꾼 제목")
    before = _snapshot(management_rows)
    searched = _prepare(client, management_rows, action, draft=draft, op="search")
    if searched.status_code == 409:
        assert _snapshot(management_rows) == before
        return
    assert searched.status_code == 200, searched.text
    prepared = _prepare(client, management_rows, action, draft=_hidden(searched, "draft_ticket"))
    if prepared.status_code == 200:
        assert _send(client, _hidden(prepared, "ticket")).status_code == 409
    else:
        assert prepared.status_code == 409, prepared.text
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_management_csrf_tampering_duplicate_fields_and_wrong_target_do_not_write(management_rows, action):
    client = _login(management_rows)
    draft = _draft(client, action)
    before = _snapshot(management_rows)
    path = f"/tasks/101/manage/{action}"
    assert client.post(path, data={"draft_ticket": draft, "op": "prepare"}).status_code == 400
    assert _prepare(client, management_rows, action, draft=draft + "x").status_code == 400
    assert _prepare(client, management_rows, action, draft=draft,
        expected_row_version="7").status_code == 400
    fields = {"csrf_token": _csrf(client), "draft_ticket": draft, "op": "prepare"} | _fields(management_rows, action)
    assert client.post(path, data=MultiDict(list(fields.items()) + [("draft_ticket", draft)])).status_code == 400
    assert client.post(f"/tasks/102/manage/{action}", data=fields).status_code in (400, 403)
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_management_old_login_draft_and_confirmation_cannot_be_reused(management_rows, action):
    first = _login(management_rows)
    draft = _draft(first, action)
    prepared = _prepare(first, management_rows, action, draft=draft)
    assert prepared.status_code == 200
    ticket = _hidden(prepared, "ticket")
    second = _login(management_rows)
    before = _snapshot(management_rows)
    assert _prepare(second, management_rows, action, draft=draft).status_code == 403
    assert _send(second, ticket).status_code == 403
    assert _send(second, ticket, "result").status_code == 403
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action,candidate", [("reassign", "staff2"), ("managers", "admin2")])
def test_management_candidates_are_rechecked_after_confirmation(management_rows, action, candidate):
    from remotework.db import transaction
    from remotework.models import User
    client = _login(management_rows)
    prepared = _prepare(client, management_rows, action)
    assert prepared.status_code == 200, prepared.text
    with management_rows[0].app_context(), transaction() as db:
        db.get(User, management_rows[1][candidate]).is_active = False
    before = _snapshot(management_rows)
    assert _send(client, _hidden(prepared, "ticket")).status_code == 409
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_management_same_values_do_not_create_receipt_or_audit(management_rows, action):
    client = _login(management_rows)
    before = _snapshot(management_rows)
    changes = ({"title": before[0], "description": before[1], "start_date": before[2].isoformat(),
                "due_date": before[3].isoformat()} if action == "edit" else
        {"assignee_id": str(management_rows[1]["staff1"])} if action == "reassign" else
        {"manager_user_ids": [str(management_rows[1]["admin1"])]})
    prepared = _prepare(client, management_rows, action, **changes)
    if prepared.status_code == 200:
        rejected = _send(client, _hidden(prepared, "ticket"))
        assert rejected.status_code == 409 and "TASK_CHANGE_NOT_NEEDED" in rejected.text
    else:
        assert prepared.status_code == 409 and "TASK_CHANGE_NOT_NEEDED" in prepared.text
    assert _snapshot(management_rows) == before


def test_management_self_removal_success_does_not_become_followup_read_failure(management_rows):
    client = _login(management_rows)
    prepared = _prepare(client, management_rows, "managers",
        manager_user_ids=[str(management_rows[1]["admin2"])])
    assert prepared.status_code == 200
    saved = _send(client, _hidden(prepared, "ticket"))
    assert saved.status_code == 200 and "처리 결과가 확인되었습니다" in saved.text
    assert _snapshot(management_rows)[9] == (management_rows[1]["admin2"],)
    assert _snapshot(management_rows)[-2:] == (1, 1)
    assert '/tasks/101?' not in saved.text  # No invitation to an inaccessible detail.
    assert client.get("/tasks/101").status_code == 403


def test_management_self_removal_lost_success_keeps_unknown_ticket_and_never_retries(management_rows, monkeypatch):
    from sqlalchemy.exc import SQLAlchemyError
    from remotework.features.tasks import web_writes
    client = _login(management_rows)
    prepared = _prepare(client, management_rows, "managers",
        manager_user_ids=[str(management_rows[1]["admin2"])])
    ticket = _hidden(prepared, "ticket")
    real_page, dropped = web_writes._page, False

    def lose_response(intent, **kwargs):
        nonlocal dropped
        if kwargs.get("completed") is not None and not dropped:
            dropped = True
            raise SQLAlchemyError("Synthetic management post-commit response loss")
        return real_page(intent, **kwargs)

    monkeypatch.setattr(web_writes, "_page", lose_response)
    lost = _send(client, ticket)
    assert lost.status_code == 503
    recovery = _hidden(lost, "ticket")
    after = _snapshot(management_rows)
    assert after[-2:] == (1, 1)
    denied = _send(client, recovery, "result")
    assert denied.status_code == 403
    assert _hidden(denied, "ticket")
    assert 'action="/tasks/write/execute"' not in denied.text
    assert "처리 결과가 확인되었습니다" not in denied.text
    assert "관리자" in denied.text and "확인" in denied.text
    assert "관리 웹 원문 101" not in denied.text  # No stale protected summary.
    assert _snapshot(management_rows) == after


@pytest.mark.parametrize("action", ACTIONS)
def test_management_current_relation_revocation_blocks_prepared_execute_and_lookup(management_rows, action):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskManager
    client = _login(management_rows)
    prepared = _prepare(client, management_rows, action)
    ticket = _hidden(prepared, "ticket")
    with management_rows[0].app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == 101))
        db.add(TaskManager(task_id=101, user_id=management_rows[1]["admin2"]))
    before = _snapshot(management_rows)
    for endpoint in ("execute", "result"):
        denied = _send(client, ticket, endpoint)
        assert denied.status_code == 403
        assert 'action="/tasks/write/execute"' not in denied.text
    assert _snapshot(management_rows) == before


@pytest.mark.parametrize("action", ["edit", "reassign"])
@pytest.mark.parametrize("history", ["accepted", "submitted"])
def test_management_inconsistent_unstarted_history_is_never_reset(management_rows, action, history):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment
    client = _login(management_rows)
    prepared = _prepare(client, management_rows, action)
    assert prepared.status_code == 200
    with management_rows[0].app_context(), transaction() as db:
        if history == "accepted":
            db.get(TaskAssignment, (101, 1)).accepted_at = OLD
        else:
            assert inspect(db.connection()).has_table("result_submissions")
            db.execute(text("INSERT INTO result_submissions (task_id, assignment_revision, round_no, submitted_by, "
                "reviewer_at_submission, description, status, submitted_at) VALUES "
                "(101, 3, 1, :worker, :reviewer, 'owned web historical fixture', 'approved', :at)"),
                {"worker": management_rows[1]["staff1"], "reviewer": management_rows[1]["admin1"], "at": OLD})
    before = _snapshot(management_rows)
    rejected = _send(client, _hidden(prepared, "ticket"))
    assert rejected.status_code == 409 and "TASK_MANAGEMENT_DATA_INCONSISTENT" in rejected.text
    assert _snapshot(management_rows) == before


def test_management_audit_failure_rolls_back_and_lookup_precedes_same_key_retry(management_rows, monkeypatch):
    from remotework.common import AppError
    from remotework.features.tasks import management
    client = _login(management_rows)
    prepared = _prepare(client, management_rows)
    ticket = _hidden(prepared, "ticket")
    before = _snapshot(management_rows)
    real_audit = management.audit

    def fail_audit(*_args, **_kwargs):
        raise AppError(503, "TEST_AUDIT_UNAVAILABLE", "검증용 관리 감사 실패")

    monkeypatch.setattr(management, "audit", fail_audit)
    failed = _send(client, ticket)
    assert failed.status_code == 503
    recovery = _hidden(failed, "ticket")
    assert 'action="/tasks/write/execute"' not in failed.text
    assert _snapshot(management_rows) == before
    monkeypatch.setattr(management, "audit", real_audit)
    assert _send(client, recovery).status_code == 409
    checked = _send(client, recovery, "result")
    assert checked.status_code == 200 and "저장된 처리 결과가 없습니다" in checked.text
    assert _snapshot(management_rows) == before
    assert _send(client, _hidden(checked, "ticket")).status_code == 200
    assert _snapshot(management_rows)[-2:] == (1, 1)


@pytest.mark.parametrize("query", ["?scope=all", "?status=unknown", "?include_cancelled=1",
                                  "?scope=managed&scope=own", "?task_id=102"])
def test_management_strict_return_filters_never_grant_or_write(management_rows, query):
    client = _login(management_rows)
    before = _snapshot(management_rows)
    assert client.get("/tasks/101/manage/edit" + query).status_code == 400
    assert _snapshot(management_rows) == before
