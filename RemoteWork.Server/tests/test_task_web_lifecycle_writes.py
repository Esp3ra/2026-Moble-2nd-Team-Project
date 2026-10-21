"""Owner 2 lifecycle HTML against the disposable MySQL runner only.

Successful writes use an explicitly labelled result-disabled test ACK. Separate
cases register the real result provider and prove that no ACK still blocks all
five writes. Neither band proves a shared deployment or installs a product ACK.
"""
import os

import pytest
from sqlalchemy import delete
from werkzeug.datastructures import MultiDict

from test_task_lifecycle import life, snapshot, NOW
from test_task_web_writes import _hidden


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Lifecycle HTML requires the explicitly owned disposable MySQL runner")
ACTIONS = ("cancel", "request-extension", "approve-extension", "reject-extension", "withdraw-extension")
DECISIONS = ("approve-extension", "reject-extension", "withdraw-extension")
FILTERS = "?scope=own&include_cancelled=true&status=in_progress"


@pytest.fixture(autouse=True)
def lifecycle_web_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_CREATE", "TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE_RESULT_READS",
                 "RESULTS", "SHARED_ATTACHMENTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT",
                 "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_LIFECYCLE"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    from remotework.features.tasks import lifecycle
    monkeypatch.setattr(lifecycle, "utcnow", lambda: NOW)


def _client(env, action, *, user=None):
    user = user or ("staff1" if action in ("request-extension", "withdraw-extension") else "admin1")
    client = env[0].test_client()
    password = {"staff1": "Staff-test-2026!", "admin1": "Admin-test-2026!"}.get(user, "Lifecycle-test-2026!")
    response = client.post("/login", data={"csrf_token": _hidden(client.get("/login"), "csrf_token"),
        "login_id": user, "password": password})
    assert response.status_code == 302
    return client


def _csrf(client):
    return _hidden(client.get("/tasks"), "csrf_token")


def _pending(env):
    from datetime import timedelta
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    from remotework.features.tasks.models import TaskRecord
    with env[0].app_context(), transaction() as db:
        task = db.get(TaskRecord, int(env[2]))
        row = TaskDeadlineExtension(task_id=task.task_id, assignment_revision=task.current_assignment_revision,
            requested_by=env[1]["staff1"], original_due_date=task.due_date,
            requested_due_date=(NOW + timedelta(days=3)).date(), reason="기존 신청 <script>보존</script>",
            status="pending", created_at=NOW)
        db.add(row)
        db.flush()
        return str(row.extension_id)


def _url(env, action):
    return f"/tasks/{env[2]}/lifecycle/{action}" + FILTERS


def _draft(client, env, action):
    response = client.get(_url(env, action))
    assert response.status_code == 200, response.text
    return _hidden(response, "draft_ticket")


def _prepare(client, env, action, *, draft=None, **changes):
    data = {"csrf_token": _csrf(client), "draft_ticket": draft or _draft(client, env, action), "op": "prepare"}
    if action in ("cancel", "request-extension", "reject-extension"):
        data["reason"] = "  웹 확인 <script>사유</script> 😀  "
    if action == "request-extension":
        data["requested_due_date"] = "2026-10-23"
    data.update(changes)
    return client.post(_url(env, action), data=data)


def _send(client, ticket, endpoint="execute"):
    return client.post("/tasks/write/" + endpoint, data={"csrf_token": _csrf(client), "ticket": ticket})


@pytest.mark.parametrize("action", ACTIONS)
def test_lifecycle_web_confirmation_and_exact_replay(life, action):
    from remotework.db import get_db
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from remotework.features.tasks.models import TaskRecord
    if action in DECISIONS:
        _pending(life)
    client = _client(life, action)
    before = snapshot(life)
    prepared = _prepare(client, life, action)
    assert prepared.status_code == 200, prepared.text
    ticket = _hidden(prepared, "ticket")
    assert snapshot(life) == before
    assert "<script>사유</script>" not in prepared.text
    response = _send(client, ticket)
    assert response.status_code == 200, response.text
    after = snapshot(life)
    assert after[0] == "in_progress" and after[1] == 8
    assert after[-2:] == (before[-2] + 1, before[-1] + 1)
    with life[0].app_context():
        db = get_db()
        task = db.get(TaskRecord, int(life[2]))
        if action == "cancel":
            assert db.get(TaskCancellation, task.task_id).reason == "  웹 확인 <script>사유</script> 😀  "
        elif action == "request-extension":
            assert after[4][0][1] == "pending"
        else:
            expected = {"approve-extension": "approved", "reject-extension": "rejected",
                        "withdraw-extension": "withdrawn"}[action]
            assert after[4][0][1] == expected
            assert task.due_date.isoformat() == ("2026-10-23" if action == "approve-extension" else "2026-10-19")
    assert _send(client, ticket).status_code == 200
    assert _send(client, ticket, "result").status_code == 200
    assert snapshot(life) == after


@pytest.mark.parametrize("action", ACTIONS)
def test_lifecycle_no_ack_keeps_draft_and_issues_no_key(life, action):
    if action in DECISIONS:
        _pending(life)
    life[0].extensions.pop("task_lifecycle_integration_ack")
    client = _client(life, action)
    before = snapshot(life)
    response = client.get(_url(life, action))
    assert response.status_code == 200 and "기술 연결 확인 필요" in response.text
    assert "disabled" in response.text
    prepared = _prepare(client, life, action, draft=_hidden(response, "draft_ticket"))
    assert prepared.status_code == 503 and "TASK_LIFECYCLE_INTEGRATION_REQUIRED" in prepared.text
    assert _hidden(prepared, "draft_ticket") == _hidden(response, "draft_ticket")
    assert 'name="ticket"' not in prepared.text
    assert snapshot(life) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_real_member3_registered_still_requires_deployment_ack(life, action):
    from remotework import create_app
    if action in DECISIONS:
        _pending(life)
    app = create_app(dict(life[0].config) | {"ENABLE_RESULTS_DRAFT": True})
    try:
        assert "results" in app.blueprints
        assert "task_lifecycle_integration_ack" not in app.extensions
        actual = (app, life[1], life[2])
        client = _client(actual, action)
        before = snapshot(life)
        response = _prepare(client, actual, action)
        assert response.status_code == 503 and "TASK_LIFECYCLE_INTEGRATION_REQUIRED" in response.text
        assert snapshot(life) == before
    finally:
        app.extensions["db_engine"].dispose()


@pytest.mark.parametrize("action", ACTIONS)
def test_lifecycle_original_versions_survive_changed_task(life, action):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord
    if action in DECISIONS:
        _pending(life)
    client = _client(life, action)
    draft = _draft(client, life, action)
    with life[0].app_context(), transaction() as db:
        db.get(TaskRecord, int(life[2])).row_version += 1
    before = snapshot(life)
    response = _prepare(client, life, action, draft=draft)
    assert response.status_code == 409
    assert _hidden(response, "draft_ticket") == draft
    assert snapshot(life) == before


def test_lifecycle_cancel_closes_pending_without_changing_task_status(life):
    _pending(life)
    client = _client(life, "cancel")
    ticket = _hidden(_prepare(client, life, "cancel"), "ticket")
    assert _send(client, ticket).status_code == 200
    after = snapshot(life)
    assert after[0] == "in_progress" and after[4][0][1] == "cancelled"


@pytest.mark.parametrize("action", ("cancel", "request-extension", "reject-extension"))
def test_lifecycle_strict_fields_csrf_and_login_binding(life, action):
    if action in DECISIONS:
        _pending(life)
    client = _client(life, action)
    draft = _draft(client, life, action)
    before = snapshot(life)
    assert _prepare(client, life, action, draft=draft, unexpected="bad").status_code == 400
    assert _prepare(client, life, action, draft=draft, csrf_token="bad").status_code == 400
    assert _prepare(client, life, action, draft=draft + "broken").status_code == 400
    duplicate = MultiDict([("csrf_token", _csrf(client)), ("draft_ticket", draft),
        ("op", "prepare"), ("reason", "first"), ("reason", "second")])
    assert client.post(_url(life, action), data=duplicate).status_code == 400
    other = _client(life, action)
    assert _prepare(other, life, action, draft=draft).status_code == 403
    assert snapshot(life) == before


@pytest.mark.parametrize("action,user", (("cancel", "staff1"), ("cancel", "admin2"),
    ("request-extension", "admin1"), ("withdraw-extension", "admin1")))
def test_lifecycle_current_relation_not_role_controls_access(life, action, user):
    if action in DECISIONS:
        _pending(life)
    client = _client(life, action, user=user)
    before = snapshot(life)
    assert client.get(_url(life, action)).status_code == 403
    assert snapshot(life) == before


def test_lifecycle_lost_response_result_still_readable_after_ack_removed(life):
    client = _client(life, "request-extension")
    ticket = _hidden(_prepare(client, life, "request-extension"), "ticket")
    assert _send(client, ticket).status_code == 200
    after = snapshot(life)
    life[0].extensions.pop("task_lifecycle_integration_ack")
    result = _send(client, ticket, "result")
    assert result.status_code == 200, result.text
    assert snapshot(life) == after
    assert _send(client, ticket).status_code == 503


@pytest.mark.parametrize("completed", (False, True))
def test_lifecycle_retained_result_survives_read_only_restart(life, completed):
    from remotework import create_app
    client = _client(life, "request-extension")
    ticket = _hidden(_prepare(client, life, "request-extension"), "ticket")
    if completed:
        assert _send(client, ticket).status_code == 200
    before = snapshot(life)
    app = create_app(dict(life[0].config) | {"ENABLE_TASK_LIFECYCLE_DRAFT": False,
        "ENABLE_TASK_LIFECYCLE_RESULT_READS_DRAFT": True})
    try:
        assert "task_web.lifecycle_action" not in app.view_functions
        assert "task_web.write_execute" not in app.view_functions
        restored = app.test_client()
        for name in ("rw_session", app.config["SESSION_COOKIE_NAME"]):
            restored.set_cookie(name, client.get_cookie(name).value)
        result = _send(restored, ticket, "result")
        assert result.status_code == 200, result.text
        assert ("처리 결과가 확인되었습니다" if completed else "저장된 처리 결과가 없습니다") in result.text
        assert 'action="/tasks/write/execute"' not in result.text
        assert snapshot(life) == before
    finally:
        app.extensions["db_engine"].dispose()


def test_lifecycle_prepared_decision_keeps_original_extension(life):
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
    first = _pending(life)
    client = _client(life, "approve-extension")
    draft = _draft(client, life, "approve-extension")
    with life[0].app_context(), transaction() as db:
        row = db.get(TaskDeadlineExtension, int(first))
        row.status, row.decided_at, row.decided_by = "withdrawn", NOW, life[1]["staff1"]
    second = _pending(life)
    assert second != first
    before = snapshot(life)
    response = _prepare(client, life, "approve-extension", draft=draft)
    assert response.status_code == 409 and "TASK_EXTENSION_NOT_PENDING" in response.text
    assert snapshot(life) == before


def test_lifecycle_current_authority_loss_preserves_uncertain_confirmation(life):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskManager
    client = _client(life, "cancel")
    ticket = _hidden(_prepare(client, life, "cancel"), "ticket")
    with life[0].app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == int(life[2]),
                                           TaskManager.user_id == life[1]["admin1"]))
    before = snapshot(life)
    response = _send(client, ticket)
    assert response.status_code == 403, response.text
    assert _hidden(response, "ticket")
    assert "자동으로 재전송하지 않습니다" in response.text
    assert snapshot(life) == before


@pytest.mark.parametrize("action,changes", (("cancel", {"reason": " "}),
    ("request-extension", {"requested_due_date": "2026-10-19"}),
    ("request-extension", {"requested_due_date": "2026-10-18"})))
def test_lifecycle_reason_and_kst_date_checks_before_key(life, action, changes):
    client = _client(life, action)
    before = snapshot(life)
    response = _prepare(client, life, action, **changes)
    assert response.status_code in (400, 409)
    assert 'name="ticket"' not in response.text
    assert snapshot(life) == before
