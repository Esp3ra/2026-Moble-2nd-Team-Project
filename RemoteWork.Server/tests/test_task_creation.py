"""Member 2 development-only task creation, against an explicitly owned MySQL DB.

This is not shared migration approval or submission/review coverage. There is no
SQLite fallback: the module skips before application fixtures without MySQL.
Run only with an owned remote_work_test_ database and dotenv loading disabled.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import date, timedelta
import json
import os
from threading import Barrier, Event, current_thread

import pytest
from sqlalchemy import event, func, select
from sqlalchemy.exc import OperationalError

from remotework import create_app
from remotework.auth import create_user, token_digest
from remotework.db import get_db, transaction
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, AuthSession, SchemaVersion, User, utcnow
from remotework.write_requests import WriteReceipt


pytestmark = pytest.mark.skipif(
    not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Task creation checks require an explicitly owned disposable MySQL database",
)


@pytest.fixture(autouse=True)
def enable_creation_draft(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_CREATE_DRAFT", "1")


@pytest.fixture
def creation(app):
    assert app.extensions["db_engine"].dialect.name == "mysql"
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        for name, role, active in (("manager2", "admin", True),
                                   ("inactiveadmin", "admin", False),
                                   ("inactivestaff", "employee", False)):
            user = create_user(db, {
                "login_id": name, "password": "Creation-test-2026!",
                "display_name": name, "employee_no": "CREATE-" + name,
                "department": "개발팀", "role": role,
            })
            user.is_active = active
            users[name] = user
        ids = {name: user.id for name, user in users.items()}
    return app, ids


def body_for(ids, **changes):
    body = {"title": "신규 업무", "description": "", "start_date": "2026-10-19",
            "due_date": "2026-10-22", "assignee_id": ids["staff1"],
            "manager_user_ids": []}
    body.update(changes)
    return body


def issue_key(client, headers):
    response = client.post("/api/v1/write-requests/keys", headers=headers,
                           json={"action": "task.created", "target": "new"})
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def create(client, headers, key, body):
    return client.post("/api/v1/tasks", headers=headers | {"Idempotency-Key": key}, json=body)


def counts(app):
    with app.app_context():
        db = get_db()
        result = [db.scalar(select(func.count()).select_from(model))
                  for model in (TaskRecord, TaskAssignment, TaskManager, WriteReceipt)]
        result.append(db.scalar(select(func.count()).select_from(AuditEvent)
                                .where(AuditEvent.action == "task.created")))
        return tuple(result)


def assert_error(response, status, code):
    assert response.status_code == status, response.json
    assert response.json["ok"] is False
    assert response.json["error"]["code"] == code
    assert response.json["request_id"] == response.headers["X-Request-ID"]


@pytest.mark.parametrize("chosen", ("none", "creator", "additional"))
def test_create_stores_required_creator_and_initial_assignment(creation, client, login, chosen):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    selected = [] if chosen == "none" else [ids["admin1"]] if chosen == "creator" else [ids["manager2"]]
    title = "  원문 제목 😀  "
    body = body_for(ids, title=title, description="\n원문 설명\n", manager_user_ids=selected)
    response = create(client, headers, issue_key(client, headers), body)
    assert response.status_code == 201, response.json
    result = response.json["data"]
    assert set(result) == {"task_id", "row_version", "assignment_revision", "task_status", "updated_at"}
    assert result["task_id"].isdigit() and int(result["task_id"]) > 0
    assert result["row_version"] == "1" and result["assignment_revision"] == 1
    assert result["task_status"] == "not_started" and result["updated_at"].endswith("Z")
    assert response.json["request_id"] == response.headers["X-Request-ID"]
    task_id = int(result["task_id"])
    expected_managers = sorted(set(selected) | {ids["admin1"]})
    with app.app_context():
        db = get_db()
        task = db.get(TaskRecord, task_id)
        assignment = db.get(TaskAssignment, (task_id, 1))
        assert task.title == title and task.description == body["description"]
        assert task.created_by == ids["admin1"] and task.current_assignment_revision == 1
        assert task.start_date == date(2026, 10, 19) and task.due_date == date(2026, 10, 22)
        assert task.task_status == "not_started" and task.row_version == 1
        assert assignment.assignee_id == ids["staff1"] and assignment.assigned_by == ids["admin1"]
        assert assignment.accepted_at is None
        assert list(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == task_id)
                               .order_by(TaskManager.user_id))) == expected_managers
        audit = db.scalar(select(AuditEvent).where(AuditEvent.action == "task.created"))
        assert audit.actor_id == ids["admin1"] and audit.resource_type == "task"
        assert audit.resource_id == str(task_id)
        receipt = db.scalar(select(WriteReceipt))
        assert receipt.action == "task.created" and receipt.target == "new" and receipt.http_status == 201
        assert json.loads(receipt.result_json) == result
        assert "원문" not in audit.details_json and "원문" not in receipt.result_json
        assert list(db.scalars(select(SchemaVersion.version))) == [1]
    assert counts(app) == (1, 1, len(expected_managers), 1, 1)
    managed = client.get("/api/v1/tasks?scope=managed&status=not_started", headers=headers)
    assert managed.status_code == 200 and [item["task_id"] for item in managed.json["data"]["items"]] == [str(task_id)]


def test_admin_can_be_the_assignee_and_title_uses_code_points(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    body = body_for(ids, title="😀" * 200, assignee_id=ids["admin1"],
                    start_date="1000-01-01", due_date="9999-12-31")
    response = create(client, headers, issue_key(client, headers), body)
    assert response.status_code == 201, response.json
    with app.app_context():
        task = get_db().get(TaskRecord, int(response.json["data"]["task_id"]))
        assert len(task.title) == 200 and task.description == ""
        assert task.start_date == date(1000, 1, 1) and task.due_date == date(9999, 12, 31)
    own = client.get("/api/v1/tasks?scope=own", headers=headers)
    assert own.status_code == 200 and len(own.json["data"]["items"]) == 1


def test_invalid_input_and_spoofed_fields_have_no_partial_effect(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    changes = [
        {"title": " \t\n"}, {"title": "😀" * 201}, {"title": None}, {"description": None},
        {"start_date": "2026-1-01"}, {"start_date": "0999-12-31"},
        {"start_date": "2026-02-30"}, {"due_date": "2026-10-18"},
        {"due_date": "2026-10-22T00:00:00Z"}, {"assignee_id": True},
        {"assignee_id": str(ids["staff1"])}, {"assignee_id": 0},
        {"manager_user_ids": None}, {"manager_user_ids": [True]},
        {"manager_user_ids": [ids["manager2"], ids["manager2"]]},
        {"created_by": ids["manager2"]}, {"task_status": "approved"},
        {"row_version": "8"}, {"accepted_at": "2026-10-19T00:00:00Z"},
    ]
    for change in changes:
        assert_error(create(client, headers, key, body_for(ids, **change)), 400, "INVALID_INPUT")
        assert counts(app) == (0, 0, 0, 0, 0)
    missing = body_for(ids)
    del missing["description"]
    assert_error(create(client, headers, key, missing), 400, "INVALID_INPUT")
    assert_error(client.post("/api/v1/tasks", headers=headers, json=body_for(ids)), 400, "INVALID_INPUT")
    assert counts(app) == (0, 0, 0, 0, 0)


def test_candidate_state_is_rechecked_on_server(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    for assignee in (ids["inactivestaff"], ids["inactiveadmin"], 2147483647):
        assert_error(create(client, headers, key, body_for(ids, assignee_id=assignee)),
                     409, "ASSIGNEE_UNAVAILABLE")
    for manager in (ids["staff1"], ids["inactiveadmin"], 2147483647):
        assert_error(create(client, headers, key, body_for(ids, manager_user_ids=[manager])),
                     409, "TASK_MANAGER_UNAVAILABLE")
    assert counts(app) == (0, 0, 0, 0, 0)
    # A previously listed administrator can lose that role before the save.
    with app.app_context(), transaction() as db:
        db.get(User, ids["manager2"]).role = "employee"
    assert_error(create(client, headers, key, body_for(ids, manager_user_ids=[ids["manager2"]])),
                 409, "TASK_MANAGER_UNAVAILABLE")
    assert counts(app) == (0, 0, 0, 0, 0)


def test_authentication_and_current_admin_role_are_required(creation, client, login):
    app, ids = creation
    admin, _ = login("admin1", "Admin-test-2026!")
    employee, _ = login()
    key = issue_key(client, admin)
    assert_error(create(client, {}, key, body_for(ids)), 401, "AUTH_REQUIRED")
    assert_error(create(client, employee, key, body_for(ids)), 403, "FORBIDDEN")
    with app.app_context(), transaction() as db:
        db.get(User, ids["admin1"]).role = "employee"
    assert_error(create(client, admin, key, body_for(ids)), 403, "FORBIDDEN")
    assert counts(app) == (0, 0, 0, 0, 0)


@pytest.mark.parametrize("reason", ("expired", "disabled"))
def test_expired_or_disabled_admin_cannot_use_an_issued_key(creation, client, login, reason):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    with app.app_context(), transaction() as db:
        if reason == "expired":
            session = db.get(AuthSession, token_digest(headers["Authorization"].split(" ", 1)[1]))
            session.expires_at = utcnow() - timedelta(seconds=1)
        else:
            db.get(User, ids["admin1"]).is_active = False
    assert_error(create(client, headers, key, body_for(ids)), 401, "AUTH_REQUIRED")
    assert counts(app) == (0, 0, 0, 0, 0)


def test_same_key_replay_conflict_result_lookup_and_authorization(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    body = body_for(ids)
    first = create(client, headers, key, body)
    again = create(client, headers, key, body)
    assert first.status_code == again.status_code == 201
    assert first.json["data"] == again.json["data"]
    assert_error(create(client, headers, key, body_for(ids, title="다른 업무")),
                 409, "IDEMPOTENCY_KEY_REUSED")
    result = client.post("/api/v1/write-requests/result", headers=headers | {"Idempotency-Key": key},
                         json={"action": "task.created", "target": "new"})
    assert result.status_code == 200 and result.json["data"]["state"] == "completed"
    assert result.json["data"]["result"] == first.json["data"]
    assert counts(app) == (1, 1, 1, 1, 1)
    with app.app_context(), transaction() as db:
        db.get(User, ids["admin1"]).role = "employee"
    assert_error(create(client, headers, key, body), 403, "FORBIDDEN")
    assert counts(app) == (1, 1, 1, 1, 1)


def test_audit_failure_rolls_back_task_relations_receipt_and_allows_same_key_retry(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    engine = app.extensions["db_engine"]
    failures = []

    def fail_audit(conn, cursor, statement, parameters, context, executemany):
        if statement.lstrip().upper().startswith("INSERT INTO AUDIT_EVENTS"):
            failures.append(True)
            raise OperationalError("synthetic owned audit failure", {}, RuntimeError("fixture"))

    event.listen(engine, "before_cursor_execute", fail_audit)
    try:
        assert_error(create(client, headers, key, body_for(ids)), 503, "DATABASE_UNAVAILABLE")
    finally:
        event.remove(engine, "before_cursor_execute", fail_audit)
    assert failures == [True] and counts(app) == (0, 0, 0, 0, 0)
    response = create(client, headers, key, body_for(ids))
    assert response.status_code == 201, response.json
    assert counts(app) == (1, 1, 1, 1, 1)


def test_concurrent_same_key_has_one_creation(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    rendezvous = Barrier(2)

    def send():
        rendezvous.wait(timeout=10)
        with app.test_client() as own_client:
            response = create(own_client, headers, key, body_for(ids))
            return response.status_code, response.json

    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(send) for _ in range(2)]
        responses = [future.result(timeout=25) for future in futures]
    assert [status for status, _ in responses] == [201, 201], responses
    assert responses[0][1]["data"] == responses[1][1]["data"]
    assert counts(app) == (1, 1, 1, 1, 1)


def test_role_revoked_while_waiting_for_common_guard_is_rechecked(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    engine = app.extensions["db_engine"]
    reached, resume = Event(), Event()

    def pause(conn, cursor, statement, parameters, context, executemany):
        if current_thread().name.startswith("waiting-create") and "FROM admin_guard" in statement:
            reached.set()
            assert resume.wait(15)

    def send():
        with app.test_client() as own_client:
            return create(own_client, headers, key, body_for(ids))

    event.listen(engine, "before_cursor_execute", pause)
    try:
        with ThreadPoolExecutor(max_workers=1, thread_name_prefix="waiting-create") as pool:
            future = pool.submit(send)
            try:
                assert reached.wait(10)
                # The owned fixture commits the role change after the initial auth read.
                with app.app_context(), transaction() as db:
                    db.get(User, ids["admin1"]).role = "employee"
            finally:
                resume.set()
            assert_error(future.result(timeout=20), 403, "FORBIDDEN")
    finally:
        event.remove(engine, "before_cursor_execute", pause)
    assert counts(app) == (0, 0, 0, 0, 0)


@pytest.mark.parametrize("query_enabled,create_enabled", ((True, False), (False, True), (False, False)))
def test_creation_is_not_registered_without_both_explicit_flags(creation, query_enabled, create_enabled):
    app, ids = creation
    disabled = create_app({"TESTING": True, "SECRET_KEY": app.config["SECRET_KEY"],
                           "DATABASE_URL": app.config["DATABASE_URL"],
                           "ENABLE_TASK_QUERY_DRAFT": query_enabled,
                           "ENABLE_TASK_CREATE_DRAFT": create_enabled,
                           "ENABLE_COMMON_WRITE_DRAFT": True, "ENABLE_NOTICE_DRAFT": False})
    try:
        with disabled.test_client() as client:
            response = client.post("/api/v1/tasks", json=body_for(ids))
            assert response.status_code == (405 if query_enabled else 404), response.json
        assert not any(rule.rule == "/api/v1/tasks" and "POST" in rule.methods
                       for rule in disabled.url_map.iter_rules())
        assert counts(app) == (0, 0, 0, 0, 0)
    finally:
        disabled.extensions["db_engine"].dispose()


def test_common_write_disabled_and_missing_receipt_fail_without_creating(creation, client, login):
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    app.config["ENABLE_COMMON_WRITE_DRAFT"] = False
    assert_error(create(client, headers, key, body_for(ids)), 503, "WRITE_SUPPORT_DISABLED")
    app.config["ENABLE_COMMON_WRITE_DRAFT"] = True
    engine = app.extensions["db_engine"]
    WriteReceipt.__table__.drop(engine)
    try:
        assert_error(create(client, headers, key, body_for(ids)), 503, "WRITE_SCHEMA_REQUIRED")
    finally:
        WriteReceipt.__table__.create(engine)
    assert counts(app) == (0, 0, 0, 0, 0)


def test_missing_manager_table_is_schema_error_and_not_automatic_ddl(creation, client, login):
    from sqlalchemy import inspect
    app, ids = creation
    headers, _ = login("admin1", "Admin-test-2026!")
    key = issue_key(client, headers)
    engine = app.extensions["db_engine"]
    TaskManager.__table__.drop(engine)
    try:
        response = create(client, headers, key, body_for(ids))
        assert response.status_code == 503, response.json
        assert not inspect(engine).has_table("task_managers")
    finally:
        TaskManager.__table__.create(engine)
    assert counts(app) == (0, 0, 0, 0, 0)
