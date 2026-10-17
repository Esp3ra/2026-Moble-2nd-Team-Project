"""Read-only development API: real ORM rows and common Bearer authentication.

Fixtures use only conftest's temporary SQLite/disposable remote_work_test_ DB.
No feature mutation API, ordinary-run sample seed or reviewer model is created.
"""
from datetime import date, timedelta
import json
import os
import subprocess
import sys

import pytest
from sqlalchemy import delete, func, select

from remotework.auth import create_user, token_digest
from remotework.db import get_db, transaction
from remotework.features.tasks.models import TASK_STATUSES, TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, AuthSession, ResourceGrant, User, utcnow


def test_fresh_process_draft_opt_in_controls_routes_and_created_tables():
    code = """
import json
from sqlalchemy import inspect
from remotework import create_app
from remotework.db import init_schema
from remotework.models import Base
app = create_app({'TESTING': True, 'SECRET_KEY': 'draft-guard-test-' + 'a' * 40,
                  'DATABASE_URL': 'sqlite:///:memory:'})
with app.app_context():
    init_schema()
    print(json.dumps({'routes': [str(r) for r in app.url_map.iter_rules()
                                if str(r).startswith('/api/v1/tasks')],
                      'metadata': sorted(Base.metadata.tables),
                      'tables': sorted(inspect(app.extensions['db_engine']).get_table_names())}))
app.extensions['db_engine'].dispose()
"""
    for enabled in (False, True):
        env = os.environ.copy()
        env.pop("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", None)
        if enabled:
            env["REMOTEWORK_ENABLE_TASK_QUERY_DRAFT"] = "1"
        child = subprocess.run([sys.executable, "-c", code], env=env, capture_output=True,
                               text=True, encoding="utf-8", check=True)
        actual = json.loads(child.stdout)
        assert len(actual["routes"]) == (2 if enabled else 0)
        draft_tables = {"tasks", "task_assignments", "task_managers"}
        for source in ("metadata", "tables"):
            assert (draft_tables <= set(actual[source])) if enabled else draft_tables.isdisjoint(actual[source])


@pytest.fixture(autouse=True)
def enable_task_query_draft(monkeypatch):
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", "1")


@pytest.fixture
def task_rows(app):
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        for login_id, role in (("staff2", "employee"), ("admin2", "admin"), ("empty1", "employee")):
            users[login_id] = create_user(db, {
                "login_id": login_id, "password": "Task-query-test-2026!",
                "display_name": login_id, "employee_no": "TASK-" + login_id,
                "department": "개발팀", "role": role})
        ids = {key: user.id for key, user in users.items()}

        def add(task_id, assignee, status, manager, due_day=20):
            db.add(TaskRecord(task_id=task_id, title=f"업무 {task_id}", description=f"상세 본문 {task_id}",
                task_status=status, start_date=date(2026, 10, 17), due_date=date(2026, 10, due_day),
                current_assignment_revision=1, created_by=ids["admin1"]))
            db.flush()
            db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=ids[assignee],
                                  assigned_by=ids["admin1"]))
            db.add(TaskManager(task_id=task_id, user_id=ids[manager]))

        add(1, "staff1", "in_progress", "admin1")
        add(2, "staff2", "not_started", "admin2")
        add(3, "admin1", "submitted", "admin2")
        add(4, "staff1", "approved", "admin1")
        add(5, "staff1", "returned", "admin1")
        add(6, "staff1", "not_started", "admin1", due_day=19)
        add(7, "staff1", "submitted", "admin1")
        add(8, "staff1", "in_progress", "admin2")
        db.flush()
        # Historic staff1 assignment is retained; current revision belongs to staff2.
        db.add(TaskAssignment(task_id=8, revision=2, assignee_id=ids["staff2"], assigned_by=ids["admin2"]))
        db.get(TaskRecord, 8).current_assignment_revision = 2
        # Neither a stale generic grant nor an employee manager row is authority.
        db.add(ResourceGrant(user_id=ids["staff1"], resource_type="task", resource_id="2"))
        db.add(TaskManager(task_id=2, user_id=ids["staff1"]))
    return ids


def _items(client, headers, query="scope=own"):
    result = client.get("/api/v1/tasks?" + query, headers=headers)
    assert result.status_code == 200, result.json
    assert result.json["ok"] is True
    assert result.json["request_id"] == result.headers["X-Request-ID"]
    assert set(result.json["data"]) == {"items"}
    return result.json["data"]["items"]


def test_employee_own_filter_detail_and_date_contract(task_rows, client, login):
    headers, _ = login()
    items = _items(client, headers)
    assert [row["task_id"] for row in items] == ["6", "1", "4", "5", "7"]
    assert len(_items(client, headers, "")) == 5  # omitted scope safely defaults to own
    for status in TASK_STATUSES:
        selected = _items(client, headers, "scope=own&status=" + status)
        assert len(selected) == 1
        assert selected[0]["task_status"] == status
    assert _items(client, headers, "scope=own&status=approved")[0]["task_id"] == "4"
    row = items[1]
    assert set(row) == {"task_id", "title", "assignee_id", "assignee_name", "task_status", "start_date", "due_date"}
    assert isinstance(row["task_id"], str) and isinstance(row["assignee_id"], int)
    assert row["assignee_id"] == task_rows["staff1"]
    assert row["assignee_name"] == "직원"
    assert row["start_date"] == "2026-10-17" and row["due_date"] == "2026-10-20"
    detail = client.get("/api/v1/tasks/1", headers=headers)
    assert detail.status_code == 200
    assert detail.json["data"] == {"task": row | {"description": "상세 본문 1"}}


def test_admin_own_and_explicit_managed_are_separate(task_rows, client, login):
    admin, _ = login("admin1", "Admin-test-2026!")
    assert [row["task_id"] for row in _items(client, admin)] == ["3"]
    assert [row["task_id"] for row in _items(client, admin, "scope=managed")] == ["6", "1", "4", "5", "7"]
    for status in TASK_STATUSES:
        assert len(_items(client, admin, "scope=managed&status=" + status)) == 1
    assert client.get("/api/v1/tasks/3", headers=admin).status_code == 200  # manager can do own work
    assert client.get("/api/v1/tasks/1", headers=admin).status_code == 200  # explicit manager
    for task_id in (2, 8):
        denied = client.get(f"/api/v1/tasks/{task_id}", headers=admin)
        assert denied.status_code == 403 and denied.json["error"]["code"] == "FORBIDDEN"
        assert "task" not in denied.json.get("data", {})


def test_other_manager_and_employee_spoofing_do_not_expand_scope(task_rows, client, login):
    admin2, _ = login("admin2", "Task-query-test-2026!")
    assert [row["task_id"] for row in _items(client, admin2, "scope=managed")] == ["2", "3", "8"]
    assert _items(client, admin2) == []
    assert client.get("/api/v1/tasks/1", headers=admin2).status_code == 403
    staff, _ = login()
    staff.update({"X-Role": "admin", "X-User-ID": str(task_rows["admin2"])})
    assert client.get("/api/v1/tasks?scope=managed", headers=staff).status_code == 403
    assert client.get("/api/v1/tasks/2", headers=staff).status_code == 403
    assert client.get("/api/v1/tasks/8", headers=staff).status_code == 403
    staff2, _ = login("staff2", "Task-query-test-2026!")
    assert [row["task_id"] for row in _items(client, staff2)] == ["2", "8"]
    assert client.get("/api/v1/tasks/8", headers=staff2).json["data"]["task"]["assignee_id"] == task_rows["staff2"]


def test_manager_relation_removal_revokes_list_and_direct_detail(task_rows, app, client, login):
    headers, _ = login("admin1", "Admin-test-2026!")
    assert client.get("/api/v1/tasks/1", headers=headers).status_code == 200
    with app.app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == 1, TaskManager.user_id == task_rows["admin1"]))
    assert "1" not in [row["task_id"] for row in _items(client, headers, "scope=managed")]
    assert client.get("/api/v1/tasks/1", headers=headers).status_code == 403


def test_empty_and_missing_are_distinct_from_forbidden(task_rows, client, login):
    empty, _ = login("empty1", "Task-query-test-2026!")
    assert _items(client, empty) == []
    missing = client.get("/api/v1/tasks/999999", headers=empty)
    assert missing.status_code == 404 and missing.json["error"]["code"] == "TASK_NOT_FOUND"
    forbidden = client.get("/api/v1/tasks/1", headers=empty)
    assert forbidden.status_code == 403 and forbidden.json["error"]["code"] == "FORBIDDEN"
    assert client.get("/api/v1/non-task-route", headers=empty).json["error"]["code"] == "HTTP_404"


def test_rejects_unsupported_duplicate_and_invalid_query_conditions(task_rows, client, login):
    headers, _ = login()
    invalid = ("scope=all", "scope=", "scope=own&scope=managed", "status=", "status=0",
               "status=InProgress", "status=approved&status=returned", "page=1", "limit=1",
               "search=secret", "user_id=1", "assignee_id=1", "role=admin", "scope=own&sort=task_id",
               "scope=own&status=approved%27%20OR%201%3D1")
    for query in invalid:
        result = client.get("/api/v1/tasks?" + query, headers=headers)
        assert result.status_code == 400, (query, result.json)
        assert result.json["error"]["code"] == "INVALID_INPUT"
    for query in ("scope=own", "user_id=1", "status=approved"):
        assert client.get("/api/v1/tasks/1?" + query, headers=headers).status_code == 400
    assert client.get("/api/v1/tasks/9223372036854775808", headers=headers).status_code == 400


def test_requires_common_bearer_and_expired_session_is_401(task_rows, app, client, login):
    assert client.get("/api/v1/tasks").status_code == 401
    assert client.get("/api/v1/tasks/1").status_code == 401
    headers, data = login()
    client.set_cookie("rw_session", data["access_token"])
    assert client.get("/api/v1/tasks").status_code == 401
    with app.app_context(), transaction() as db:
        db.get(AuthSession, token_digest(data["access_token"])).expires_at = utcnow() - timedelta(seconds=1)
    result = client.get("/api/v1/tasks", headers=headers)
    assert result.status_code == 401 and result.json["error"]["code"] == "AUTH_REQUIRED"


def test_full_allowed_list_is_not_truncated_to_first_page(task_rows, app, client, login):
    with app.app_context(), transaction() as db:
        for task_id in range(100, 165):
            db.add(TaskRecord(task_id=task_id, title=f"전체 조회 {task_id}", description="test fixture",
                task_status="not_started", start_date=date(2026, 10, 17), due_date=date(2026, 10, 21),
                current_assignment_revision=1, created_by=task_rows["admin1"]))
        db.flush()
        for task_id in range(100, 165):
            db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=task_rows["staff1"],
                                  assigned_by=task_rows["admin1"]))
    headers, _ = login()
    items = _items(client, headers)
    assert len(items) == 70 and items[-1]["task_id"] == "164"


def test_read_does_not_mutate_feature_rows_or_write_success_audit(task_rows, app, client, login):
    headers, _ = login()
    with app.app_context():
        before = get_db().scalar(select(func.count()).select_from(AuditEvent))
        version = get_db().get(TaskRecord, 1).row_version
    _items(client, headers)
    assert client.get("/api/v1/tasks/1", headers=headers).status_code == 200
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(AuditEvent)) == before
        assert get_db().get(TaskRecord, 1).row_version == version


def test_no_mutation_routes_and_missing_table_is_common_503(task_rows, app, client, login):
    headers, _ = login()
    for method, path in (("POST", "/api/v1/tasks"), ("PATCH", "/api/v1/tasks/1"), ("DELETE", "/api/v1/tasks/1")):
        assert client.open(path, method=method, headers=headers, json={"task_status": "approved"}).status_code == 405
    admin, _ = login("admin1", "Admin-test-2026!")
    with app.app_context():
        TaskManager.__table__.drop(app.extensions["db_engine"])
    failed = client.get("/api/v1/tasks?scope=managed", headers=admin)
    assert failed.status_code == 503 and failed.json["error"]["code"] == "DATABASE_UNAVAILABLE"


def test_common_server_error_response_is_preserved(task_rows, client, login, monkeypatch):
    from remotework.features.tasks import routes

    def fail(*_args):
        raise RuntimeError("test-only query failure")

    monkeypatch.setattr(routes, "list_tasks", fail)
    headers, _ = login()
    failed = client.get("/api/v1/tasks", headers=headers)
    assert failed.status_code == 500
    assert failed.json["error"]["code"] == "SERVER_ERROR"
    assert "test-only" not in failed.json["error"]["message"]
