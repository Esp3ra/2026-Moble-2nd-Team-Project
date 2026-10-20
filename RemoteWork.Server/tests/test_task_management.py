"""Member 2 management contract; DB cases require an owned disposable MySQL.

Cold/input checks never connect. DB fixtures alter only remote_work_test_* data,
and synthetic historical submissions intentionally represent inconsistent data.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta
import json
import os
import subprocess
import sys
from threading import Barrier, Event, current_thread

from flask import Flask
import pytest
from sqlalchemy import event, func, inspect, select, text
from sqlalchemy.exc import OperationalError

from remotework.auth import create_user
from remotework.common import AppError
from remotework.db import get_db, transaction
from remotework.features.tasks import management
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, User
from remotework.write_contract import BIGINT_MAX, INTEGER_MAX
from remotework.write_requests import WriteReceipt


NOW = datetime(2026, 10, 20, 3, 4, 5, 123456)
OLD = datetime(2026, 10, 18, 1, 2, 3)
METHODS = {"task.updated": "patch", "task.reassigned": "post", "task.managers_changed": "patch"}
SUFFIXES = {"task.updated": "", "task.reassigned": "/reassign", "task.managers_changed": "/managers"}
ACTIONS = tuple(METHODS)


@pytest.fixture(autouse=True)
def management_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_MANAGEMENT_DRAFT", "1")
    monkeypatch.setenv("REMOTEWORK_ENABLE_RESULTS_DRAFT", "0")
    monkeypatch.setattr(management, "utcnow", lambda: NOW)


@pytest.fixture
def managed(request):
    if not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"):
        pytest.skip("Management DB checks require an explicitly owned disposable MySQL database")
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        for name, role in (("staff2", "employee"), ("admin2", "admin"), ("admin3", "admin")):
            users[name] = create_user(db, {"login_id": name, "password": "Management-test-2026!",
                "display_name": name, "employee_no": "MANAGE-" + name, "role": role})
        ids = {name: user.id for name, user in users.items()}
        task = TaskRecord(title="original title", description="original description",
            task_status="not_started", start_date=NOW.date(), due_date=(NOW + timedelta(days=2)).date(),
            current_assignment_revision=3, row_version=7, created_by=ids["admin1"],
            created_at=OLD, updated_at=OLD)
        db.add(task)
        db.flush()
        target = str(task.task_id)
        db.add(TaskAssignment(task_id=task.task_id, revision=3, assignee_id=ids["staff1"],
            assigned_by=ids["admin1"], assigned_at=OLD, accepted_at=None))
        db.add(TaskManager(task_id=task.task_id, user_id=ids["admin1"]))
    try:
        yield app, ids, target
    finally:
        # The historical-submission fixture never imports member 3's ORM. Remove
        # its own raw row before common fixtures clear parent task/user records.
        with app.app_context(), transaction() as db:
            if inspect(db.connection()).has_table("result_submissions"):
                columns = {column["name"] for column in inspect(db.connection()).get_columns("result_submissions")}
                if "task_id" in columns:
                    db.execute(text("DELETE FROM result_submissions WHERE task_id = :task_id"),
                               {"task_id": int(target)})


def body_for(env, action):
    _app, ids, _target = env
    body = {"expected_row_version": "7", "expected_assignment_revision": 3}
    if action == "task.updated":
        body |= {"title": "changed title", "description": "changed description",
                 "start_date": NOW.date().isoformat(), "due_date": (NOW + timedelta(days=3)).date().isoformat()}
    elif action == "task.reassigned":
        body["assignee_id"] = ids["staff2"]
    else:
        body["manager_user_ids"] = [ids["admin2"], ids["admin1"]]
    return body


def key_for(client, headers, target, action):
    response = client.post("/api/v1/write-requests/keys", headers=headers,
                           json={"action": action, "target": target})
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def send(client, headers, key, env, action, body=None, target=None, **kwargs):
    target = env[2] if target is None else target
    return getattr(client, METHODS[action])("/api/v1/tasks/" + target + SUFFIXES[action],
        headers=headers | {"Idempotency-Key": key},
        json=body_for(env, action) if body is None else body, **kwargs)


def error(response, status, code):
    assert response.status_code == status, response.json
    assert response.json["ok"] is False and response.json["error"]["code"] == code
    assert response.headers["X-Request-ID"] == response.json["request_id"]


def snapshot(env):
    app, _ids, target = env
    with app.app_context():
        db = get_db()
        task = db.get(TaskRecord, int(target))
        assignments = tuple((row.revision, row.assignee_id, row.assigned_by, row.assigned_at, row.accepted_at)
            for row in db.scalars(select(TaskAssignment).where(TaskAssignment.task_id == int(target))
                                  .order_by(TaskAssignment.revision)))
        managers = tuple(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == int(target))
                                   .order_by(TaskManager.user_id)))
        counts = (db.scalar(select(func.count()).select_from(WriteReceipt)),
            db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action.in_(ACTIONS))))
        return (task.title, task.description, task.start_date, task.due_date, task.task_status,
                task.row_version, task.current_assignment_revision, task.updated_at,
                assignments, managers, counts)


def set_state(env, state):
    app, _ids, target = env
    with app.app_context(), transaction() as db:
        task = db.get(TaskRecord, int(target))
        task.task_status = state
        db.get(TaskAssignment, (int(target), task.current_assignment_revision)).accepted_at = (
            None if state == "not_started" else OLD)


@pytest.mark.parametrize("action", ACTIONS)
def test_success_is_atomic_and_returns_only_the_actual_stored_minimal_result(managed, client, login, action):
    app, ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    before = snapshot(managed)
    result = send(client, headers, key, managed, action)
    assert result.status_code == 200, result.json
    data = result.json["data"]
    assert data == {"task_id": target, "row_version": "8", "assignment_revision": 4 if action == "task.reassigned" else 3,
                    "task_status": "not_started", "updated_at": "2026-10-20T03:04:05Z"}
    after = snapshot(managed)
    assert after[4:8] == ("not_started", 8, data["assignment_revision"], NOW.replace(microsecond=0))
    assert after[-1] == (1, 1)
    if action == "task.updated":
        assert after[:4] == ("changed title", "changed description", NOW.date(), (NOW + timedelta(days=3)).date())
        assert after[8:10] == before[8:10]
    elif action == "task.reassigned":
        assert after[8][0] == before[8][0] and len(after[8]) == 2
        assert after[8][1] == (4, ids["staff2"], ids["admin1"], NOW.replace(microsecond=0), None)
        assert after[:4] == before[:4] and after[9] == before[9]
    else:
        assert after[9] == tuple(sorted([ids["admin1"], ids["admin2"]]))
        assert after[:4] == before[:4] and after[8] == before[8]
    # A retained success does not re-run state/date/candidate/no-op eligibility.
    with app.app_context(), transaction() as db:
        db.get(TaskRecord, int(target)).task_status = "approved"
    assert send(client, headers, key, managed, action).json["data"] == data
    assert snapshot(managed)[-1] == (1, 1)
    lookup = client.post("/api/v1/write-requests/result", headers=headers | {"Idempotency-Key": key},
        json={"action": action, "target": target})
    assert lookup.status_code == 200 and lookup.json["data"]["result"] == data


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("state", ["not_started", "in_progress", "submitted", "approved", "returned"])
def test_each_of_five_states_uses_the_approved_action_matrix(managed, client, login, action, state):
    set_state(managed, state)
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, managed[2], action)
    before = snapshot(managed)
    response = send(client, headers, key, managed, action)
    allowed = state in ("not_started", "in_progress", "returned") if action == "task.managers_changed" else state == "not_started"
    if allowed:
        assert response.status_code == 200, response.json
        assert response.json["data"]["task_status"] == state
        if action != "task.reassigned":
            assert snapshot(managed)[8] == before[8]
    else:
        error(response, 409, "TASK_STATE_CONFLICT")
        assert snapshot(managed) == before


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("actor", ["staff1", "staff2", "admin2"])
def test_assignee_other_staff_and_unrelated_admin_are_not_task_managers(managed, client, login, action, actor):
    password = "Staff-test-2026!" if actor == "staff1" else "Management-test-2026!"
    headers, _ = login(actor, password)
    before = snapshot(managed)
    response = getattr(client, METHODS[action])("/api/v1/tasks/" + managed[2] + SUFFIXES[action],
        headers=headers, data="invalid", content_type="application/json")
    error(response, 403, "FORBIDDEN")
    assert snapshot(managed) == before


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("change", ["manager_removed", "role_changed", "deactivated"])
def test_old_key_never_overrides_current_actor_role_or_manager_relation(managed, client, login, action, change):
    app, ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    with app.app_context(), transaction() as db:
        if change == "manager_removed":
            db.delete(db.get(TaskManager, (int(target), ids["admin1"])))
        elif change == "role_changed":
            db.get(User, ids["admin1"]).role = "employee"
        else:
            db.get(User, ids["admin1"]).is_active = False
    before = snapshot(managed)
    error(send(client, headers, key, managed, action), 401 if change == "deactivated" else 403,
          "AUTH_REQUIRED" if change == "deactivated" else "FORBIDDEN")
    assert snapshot(managed) == before


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("field,value,code", [
    ("expected_row_version", "6", "TASK_VERSION_CONFLICT"),
    ("expected_assignment_revision", 2, "TASK_ASSIGNMENT_CHANGED"),
])
def test_version_and_assignment_conflicts_never_mutate(managed, client, login, action, field, value, code):
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, managed[2], action)
    body = body_for(managed, action) | {field: value}
    before = snapshot(managed)
    error(send(client, headers, key, managed, action, body), 409, code)
    assert snapshot(managed) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_no_op_is_not_a_new_receipt_audit_or_version(managed, client, login, action):
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, managed[2], action)
    body = body_for(managed, action)
    if action == "task.updated":
        body |= {"title": "original title", "description": "original description",
                 "due_date": (NOW + timedelta(days=2)).date().isoformat()}
    elif action == "task.reassigned":
        body["assignee_id"] = managed[1]["staff1"]
    else:
        body["manager_user_ids"] = [managed[1]["admin1"]]
    before = snapshot(managed)
    error(send(client, headers, key, managed, action, body), 409, "TASK_CHANGE_NOT_NEEDED")
    assert snapshot(managed) == before


@pytest.mark.parametrize("action", ["task.updated", "task.reassigned"])
@pytest.mark.parametrize("historical", [False, True])
def test_not_started_with_any_acceptance_is_not_reset_or_reassigned(managed, client, login, action, historical):
    app, ids, target = managed
    with app.app_context(), transaction() as db:
        if historical:
            db.add(TaskAssignment(task_id=int(target), revision=1, assignee_id=ids["staff2"],
                assigned_by=ids["admin1"], assigned_at=OLD, accepted_at=OLD))
        else:
            db.get(TaskAssignment, (int(target), 3)).accepted_at = OLD
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    before = snapshot(managed)
    error(send(client, headers, key, managed, action), 409, "TASK_MANAGEMENT_DATA_INCONSISTENT")
    assert snapshot(managed) == before


@pytest.mark.parametrize("action", ["task.updated", "task.reassigned"])
def test_not_started_with_successful_result_is_rejected_without_result_orm_import(managed, client, login, action):
    app, ids, target = managed
    with app.app_context(), transaction() as db:
        assert inspect(db.connection()).has_table("result_submissions"), "Owned runner must prepare reviewed 006 SQL"
        db.execute(text("INSERT INTO result_submissions (task_id, assignment_revision, round_no, submitted_by, "
            "reviewer_at_submission, description, status, submitted_at) VALUES "
            "(:task_id, 3, 1, :worker, :reviewer, 'owned historical fixture', 'approved', :at)"),
            {"task_id": int(target), "worker": ids["staff1"], "reviewer": ids["admin1"], "at": OLD})
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    before = snapshot(managed)
    error(send(client, headers, key, managed, action), 409, "TASK_MANAGEMENT_DATA_INCONSISTENT")
    assert snapshot(managed) == before
    with app.app_context():
        assert get_db().scalar(text("SELECT COUNT(*) FROM result_submissions WHERE task_id = :task_id"),
                               {"task_id": int(target)}) == 1


@pytest.mark.parametrize("role,active", [("employee", False), ("admin", False), ("unknown", True)])
def test_unavailable_assignee_is_rejected_under_target_user_lock(managed, client, login, role, active):
    app, ids, target = managed
    with app.app_context(), transaction() as db:
        user = db.get(User, ids["staff2"])
        user.role, user.is_active = role, active
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, "task.reassigned")
    before = snapshot(managed)
    error(send(client, headers, key, managed, "task.reassigned"), 409, "ASSIGNEE_UNAVAILABLE")
    assert snapshot(managed) == before


@pytest.mark.parametrize("candidate", ["staff1", "inactive", "missing"])
def test_manager_candidates_must_all_be_current_active_admins(managed, client, login, candidate):
    app, ids, target = managed
    if candidate == "inactive":
        with app.app_context(), transaction() as db:
            db.get(User, ids["admin2"]).is_active = False
    candidate_id = ids["staff1"] if candidate == "staff1" else ids["admin2"] if candidate == "inactive" else INTEGER_MAX
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, "task.managers_changed")
    before = snapshot(managed)
    body = body_for(managed, "task.managers_changed") | {"manager_user_ids": [ids["admin1"], candidate_id]}
    error(send(client, headers, key, managed, "task.managers_changed", body), 409, "TASK_MANAGER_UNAVAILABLE")
    assert snapshot(managed) == before


def test_creator_can_remove_self_and_retained_result_requires_current_manager(managed, client, login):
    app, ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, "task.managers_changed")
    body = body_for(managed, "task.managers_changed") | {"manager_user_ids": [ids["admin2"]]}
    response = send(client, headers, key, managed, "task.managers_changed", body)
    assert response.status_code == 200 and snapshot(managed)[9] == (ids["admin2"],)
    before = snapshot(managed)
    error(send(client, headers, key, managed, "task.managers_changed", body), 403, "FORBIDDEN")
    error(client.post("/api/v1/write-requests/result", headers=headers | {"Idempotency-Key": key},
        json={"action": "task.managers_changed", "target": target}), 403, "FORBIDDEN")
    assert snapshot(managed) == before
    with app.app_context():
        assert get_db().get(TaskRecord, int(target)).created_by == ids["admin1"]


@pytest.mark.parametrize("action", ACTIONS)
def test_row_version_exhaustion_has_no_partial_change(managed, client, login, action):
    app, _ids, target = managed
    with app.app_context(), transaction() as db:
        db.get(TaskRecord, int(target)).row_version = BIGINT_MAX
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    before = snapshot(managed)
    body = body_for(managed, action) | {"expected_row_version": str(BIGINT_MAX)}
    error(send(client, headers, key, managed, action, body), 409, "TASK_VERSION_EXHAUSTED")
    assert snapshot(managed) == before


def test_assignment_revision_exhaustion_does_not_overflow_or_delete_old_rows(managed, client, login):
    app, ids, target = managed
    with app.app_context(), transaction() as db:
        task = db.get(TaskRecord, int(target))
        task.current_assignment_revision = INTEGER_MAX
        db.add(TaskAssignment(task_id=int(target), revision=INTEGER_MAX, assignee_id=ids["staff1"],
            assigned_by=ids["admin1"], assigned_at=OLD, accepted_at=None))
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, "task.reassigned")
    before = snapshot(managed)
    body = body_for(managed, "task.reassigned") | {"expected_assignment_revision": INTEGER_MAX}
    error(send(client, headers, key, managed, "task.reassigned", body), 409, "TASK_ASSIGNMENT_EXHAUSTED")
    assert snapshot(managed) == before


@pytest.mark.parametrize("day_offset,status", [(-1, 409), (0, 200), (1, 200)])
def test_update_checks_kst_today_at_the_server_not_client_calendar(managed, client, login, monkeypatch, day_offset, status):
    # UTC 15:00 is the next KST day; a server-local/UTC date would accept yesterday.
    instant = datetime(2026, 10, 19, 15, 0, 0)
    monkeypatch.setattr(management, "utcnow", lambda: instant)
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, managed[2], "task.updated")
    body = body_for(managed, "task.updated") | {"start_date": "2026-10-18",
        "due_date": (datetime(2026, 10, 20) + timedelta(days=day_offset)).date().isoformat()}
    before = snapshot(managed)
    response = send(client, headers, key, managed, "task.updated", body)
    if status == 409:
        error(response, 409, "TASK_DUE_DATE_IN_PAST")
        assert snapshot(managed) == before
    else:
        assert response.status_code == status, response.json


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("failed_table", ["AUDIT_EVENTS", "WRITE_RECEIPTS"])
def test_failed_audit_or_receipt_rolls_back_every_business_row(managed, client, login, action, failed_table):
    app, _ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    engine, before, reached = app.extensions["db_engine"], snapshot(managed), []
    def fail(conn, cursor, statement, parameters, context, executemany):
        if statement.lstrip().upper().startswith("INSERT INTO " + failed_table):
            reached.append(True)
            raise OperationalError("owned management rollback fixture", {}, RuntimeError("owned fixture"))
    event.listen(engine, "before_cursor_execute", fail)
    try:
        error(send(client, headers, key, managed, action), 503, "DATABASE_UNAVAILABLE")
    finally:
        event.remove(engine, "before_cursor_execute", fail)
    assert reached == [True] and snapshot(managed) == before
    assert send(client, headers, key, managed, action).status_code == 200
    assert snapshot(managed)[-1] == (1, 1)


@pytest.mark.parametrize("action", ACTIONS)
@pytest.mark.parametrize("same_key", [True, False])
def test_mysql_rr_racing_requests_have_one_effect(managed, client, login, action, same_key):
    app, _ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key1 = key_for(client, headers, target, action)
    key2 = key1 if same_key else key_for(client, headers, target, action)
    barrier = Barrier(2)
    def execute(key):
        barrier.wait(timeout=10)
        with app.test_client() as own_client:
            response = send(own_client, headers, key, managed, action)
            return response.status_code, response.json
    with ThreadPoolExecutor(max_workers=2) as pool:
        results = [future.result(timeout=25) for future in
                   [pool.submit(execute, key) for key in (key1, key2)]]
    if same_key:
        assert [status for status, _ in results] == [200, 200], results
        assert results[0][1]["data"] == results[1][1]["data"]
    else:
        assert sorted(status for status, _ in results) == [200, 409], results
        conflict = "TASK_ASSIGNMENT_CHANGED" if action == "task.reassigned" else "TASK_VERSION_CONFLICT"
        assert next(data for status, data in results if status == 409)["error"]["code"] == conflict
    assert snapshot(managed)[-1] == (1, 1)


def test_permission_change_while_waiting_for_guard_uses_current_relationship(managed, client, login):
    app, ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, "task.updated")
    engine, reached, resume = app.extensions["db_engine"], Event(), Event()
    def pause(conn, cursor, statement, parameters, context, executemany):
        if current_thread().name.startswith("waiting-management") and "FROM admin_guard" in statement:
            reached.set()
            assert resume.wait(15)
    def execute():
        with app.test_client() as own_client:
            return send(own_client, headers, key, managed, "task.updated")
    event.listen(engine, "before_cursor_execute", pause)
    try:
        with ThreadPoolExecutor(max_workers=1, thread_name_prefix="waiting-management") as pool:
            future = pool.submit(execute)
            try:
                assert reached.wait(10)
                with app.app_context(), transaction() as db:
                    db.delete(db.get(TaskManager, (int(target), ids["admin1"])))
            finally:
                resume.set()
            error(future.result(timeout=20), 403, "FORBIDDEN")
    finally:
        event.remove(engine, "before_cursor_execute", pause)
    assert snapshot(managed)[-1] == (0, 0)


@pytest.mark.parametrize("action", ["task.updated", "task.reassigned"])
@pytest.mark.parametrize("condition", ["disabled_absent", "enabled_absent", "malformed"])
def test_result_dependency_gate_allows_only_legitimate_disabled_absence(managed, client, login, monkeypatch, action, condition):
    app, _ids, target = managed
    real_inspect = management.inspect
    class View:
        def __init__(self, connection):
            self.actual = real_inspect(connection)
        def has_table(self, name):
            return condition == "malformed" if name == "result_submissions" else self.actual.has_table(name)
        def get_columns(self, name):
            return [{"name": "task_id"}] if name == "result_submissions" else self.actual.get_columns(name)
    # This controlled inspector double tests the dependency branch, not DDL migration.
    monkeypatch.setattr(management, "inspect", View)
    app.config["ENABLE_RESULTS_DRAFT"] = condition == "enabled_absent"
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, action)
    before = snapshot(managed)
    response = send(client, headers, key, managed, action)
    if condition == "disabled_absent":
        assert response.status_code == 200, response.json
    else:
        error(response, 503, "TASK_RESULTS_SCHEMA_REQUIRED")
        assert snapshot(managed) == before


def test_result_history_query_is_fixed_sql_with_bound_identifier(managed, client, login):
    app, _ids, target = managed
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, target, "task.updated")
    seen = []
    def capture(conn, cursor, statement, parameters, context, executemany):
        if statement.startswith("SELECT task_id FROM result_submissions "):
            seen.append((statement, parameters))
    engine = app.extensions["db_engine"]
    event.listen(engine, "before_cursor_execute", capture)
    try:
        assert send(client, headers, key, managed, "task.updated").status_code == 200
        # Invalid target never becomes an interpolated result-table WHERE clause.
        error(send(client, headers, key, managed, "task.updated", target="1%20OR%201=1"), 400, "INVALID_INPUT")
    finally:
        event.remove(engine, "before_cursor_execute", capture)
    assert len(seen) == 1
    statement, parameters = seen[0]
    assert "WHERE task_id = %(task_id)s LIMIT 1 FOR UPDATE" in statement
    assert parameters == {"task_id": int(target)}


@pytest.mark.parametrize("action", ACTIONS)
def test_same_success_key_with_changed_payload_is_rejected_without_second_effect(managed, client, login, action):
    headers, _ = login("admin1", "Admin-test-2026!")
    key = key_for(client, headers, managed[2], action)
    assert send(client, headers, key, managed, action).status_code == 200
    before = snapshot(managed)
    body = body_for(managed, action)
    if action == "task.updated":
        body["title"] = "another title"
    elif action == "task.reassigned":
        body["assignee_id"] = managed[1]["admin2"]
    else:
        body["manager_user_ids"] = [managed[1]["admin1"], managed[1]["admin3"]]
    error(send(client, headers, key, managed, action, body), 409, "IDEMPOTENCY_KEY_REUSED")
    assert snapshot(managed) == before


@pytest.mark.parametrize("query,management_enabled,common", [
    (False, False, False), (False, True, False), (False, True, True),
    (True, False, False), (True, False, True), (True, True, False), (True, True, True),
])
def test_cold_registration_never_connects_applies_ddl_or_imports_results(query, management_enabled, common):
    source = '''
import sys
from flask import g
from sqlalchemy import event
from remotework import create_app
import remotework.auth as auth
from remotework.models import Base
auth.load_identity = lambda: setattr(g, 'user', object())
app = create_app({'TESTING': True, 'SECRET_KEY': 'owned-management-cold-' + 'x' * 32,
 'DATABASE_URL': 'mysql+pymysql://unused:unused@127.0.0.1:1/remote_work_test_cold',
 'ENABLE_TASK_QUERY_DRAFT': __QUERY__, 'ENABLE_TASK_MANAGEMENT_DRAFT': __MANAGEMENT__,
 'ENABLE_COMMON_WRITE_DRAFT': __COMMON__, 'ENABLE_TASK_CREATE_DRAFT': False,
 'ENABLE_TASK_START_DRAFT': False, 'ENABLE_RESULTS_DRAFT': False, 'ENABLE_NOTICE_DRAFT': False})
def no_connection(*args):
    raise AssertionError('Cold management setup must never connect or create tables')
event.listen(app.extensions['db_engine'], 'connect', no_connection)
try:
    rules = {(r.rule, method) for r in app.url_map.iter_rules() for method in r.methods}
    installed = __QUERY__ and __MANAGEMENT__
    assert (('/api/v1/tasks/<string:task_id>', 'PATCH') in rules) is installed
    assert (('/api/v1/tasks/<string:task_id>/reassign', 'POST') in rules) is installed
    assert (('/api/v1/tasks/<string:task_id>/managers', 'PATCH') in rules) is installed
    assert ('remotework.features.tasks.management' in sys.modules) is installed
    assert 'remotework.features.results' not in sys.modules
    assert 'result_submissions' not in Base.metadata.tables
    assert ('remotework.write_requests' in sys.modules) is __COMMON__
    assert ('write_receipts' in Base.metadata.tables) is __COMMON__
    if installed and not __COMMON__:
        response = app.test_client().patch('/api/v1/tasks/1', json={})
        assert response.status_code == 503 and response.json['error']['code'] == 'WRITE_SUPPORT_DISABLED'
    if installed and __COMMON__:
        for action in ('task.updated', 'task.reassigned', 'task.managers_changed'):
            assert callable(app.extensions['write_authorizers'][action])
        assert not any(action in app.extensions.get('write_action_contracts', {})
                       for action in ('task.updated', 'task.reassigned', 'task.managers_changed'))
finally:
    app.extensions['db_engine'].dispose()
'''.replace("__QUERY__", repr(query)).replace("__MANAGEMENT__", repr(management_enabled)).replace("__COMMON__", repr(common))
    env = {name: value for name, value in os.environ.items()
           if not name.startswith(("REMOTEWORK_", "MYSQL_", "DATABASE_", "SECRET_", "WRITE_REQUEST_"))}
    env["PYTHON_DOTENV_DISABLED"] = "1"
    result = subprocess.run([sys.executable, "-c", source], capture_output=True, text=True, env=env)
    assert result.returncode == 0, result.stderr


@pytest.mark.parametrize("action,extra", [
    ("task.updated", {"title": "text", "description": "", "start_date": "2026-10-20", "due_date": "2026-10-20"}),
    ("task.reassigned", {"assignee_id": 1}),
    ("task.managers_changed", {"manager_user_ids": [1]}),
])
@pytest.mark.parametrize("bad", ["missing", "extra", "duplicate", "bool_revision", "number_version", "nonstandard", "invalid_utf8"])
def test_strict_input_preserves_exact_types_and_rejects_duplicate_fields(action, extra, bad):
    body = {"expected_row_version": "1", "expected_assignment_revision": 1, **extra}
    if bad == "missing":
        body.pop(next(iter(extra)))
    elif bad == "extra":
        body["actor_id"] = 1
    elif bad == "bool_revision":
        body["expected_assignment_revision"] = True
    elif bad == "number_version":
        body["expected_row_version"] = 1
    raw = json.dumps(body)
    if bad == "duplicate":
        raw = raw[:-1] + ',"expected_row_version":"1"}'
    elif bad == "nonstandard":
        raw = raw.replace('"expected_assignment_revision": 1', '"expected_assignment_revision": NaN')
    elif bad == "invalid_utf8":
        raw = b"\xff"
    with Flask(__name__).test_request_context("/", data=raw, content_type="application/json"):
        with pytest.raises(AppError) as caught:
            management._input(action)
        assert (caught.value.status, caught.value.code) == (400, "INVALID_INPUT")


@pytest.mark.parametrize("body", [
    {"manager_user_ids": []}, {"manager_user_ids": [1, 1]}, {"manager_user_ids": [True]},
    {"manager_user_ids": ["1"]}, {"manager_user_ids": [2147483648]},
])
def test_manager_input_rejects_empty_duplicate_or_non_integer_ids(body):
    body |= {"expected_row_version": "1", "expected_assignment_revision": 1}
    with Flask(__name__).test_request_context("/", json=body):
        with pytest.raises(AppError) as caught:
            management._input("task.managers_changed")
        assert caught.value.code == "INVALID_INPUT"
