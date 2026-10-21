"""Real Flask task GETs using cookie auth and an explicitly owned MySQL fixture.

The runner must set REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS=1 and MYSQL_TEST_URL
for its disposable remote_work_test_ database. No SQLite fallback or product
sample seed is used. Fixture writes are not task web capabilities.
"""
from datetime import date, timedelta
from html import unescape
import json
import os
import re
import subprocess
import sys
from urllib.parse import parse_qs, urlsplit

import pytest
from sqlalchemy import delete, inspect, select
from sqlalchemy.engine import make_url


@pytest.fixture(autouse=True)
def task_web_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("COMMON_WRITE", "TASK_CREATE", "TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE",
                 "TASK_LIFECYCLE_RESULT_READS", "RESULTS", "NOTICE", "CHAT", "DEVICE_ENROLLMENT"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    monkeypatch.setenv("REMOTEWORK_ENABLE_TASK_QUERY_DRAFT", "1")


@pytest.fixture
def web_rows(request):
    value = os.getenv("MYSQL_TEST_URL", "")
    if os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1" or not value.startswith("mysql"):
        pytest.skip("Task web checks require the explicit owned disposable MySQL runner")
    assert (make_url(value).database or "").startswith("remote_work_test_")
    # Import fixture metadata before conftest creates/clears this owned DB.
    from remotework.auth import create_user
    from remotework.db import transaction
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.models import ResourceGrant, User, utcnow

    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        for name, role in (("staff2", "employee"), ("admin2", "admin"), ("empty1", "employee")):
            users[name] = create_user(db, {
                "login_id": name, "password": "Task-web-test-2026!", "display_name": name,
                "employee_no": "WEB-" + name, "role": role})
        ids = {name: user.id for name, user in users.items()}

        def add(task_id, assignee, status, manager, due_day=20):
            db.add(TaskRecord(task_id=task_id, title=f"업무 {task_id}", description=f"업무 본문 {task_id}",
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
        add(9, "staff1", "in_progress", "admin1")
        db.flush()
        db.add(TaskAssignment(task_id=8, revision=2, assignee_id=ids["staff2"], assigned_by=ids["admin2"]))
        db.get(TaskRecord, 8).current_assignment_revision = 2
        db.add(ResourceGrant(user_id=ids["staff1"], resource_type="task", resource_id="2"))
        db.add(TaskManager(task_id=2, user_id=ids["staff1"]))
        db.add(TaskCancellation(task_id=9, cancelled_by=ids["admin1"], reason="조회용 취소 자료", cancelled_at=utcnow()))
    return app, ids


def _client(web_rows, user="staff1", password=None):
    client = web_rows[0].test_client()
    login_page = client.get("/login")
    token = re.search(r'name="csrf_token" value="([^"]+)"', login_page.text).group(1)
    if password is None:
        password = {"staff1": "Staff-test-2026!", "admin1": "Admin-test-2026!"}.get(user, "Task-web-test-2026!")
    response = client.post("/login", data={"csrf_token": token, "login_id": user, "password": password})
    assert response.status_code == 302
    return client


def _ids(response):
    assert response.status_code == 200
    return re.findall(r'data-task-id="([0-9]+)"', response.text)


def _error(response, status):
    assert response.status_code == status
    assert response.mimetype == "text/html"
    assert "요청을 확인해 주세요" in response.text
    assert "조회 조건에 맞는 업무가 없습니다." not in response.text


def _snapshot(app):
    engine = app.extensions["db_engine"]
    inspector = inspect(engine)
    result = {}
    with engine.connect() as connection:
        for name in sorted(inspector.get_table_names()):
            assert re.fullmatch(r"[a-z_]+", name)
            keys = inspector.get_pk_constraint(name)["constrained_columns"]
            assert keys and all(re.fullmatch(r"[a-z_]+", key) for key in keys)
            ordering = ", ".join("`" + key + "`" for key in keys)
            result[name] = connection.exec_driver_sql("SELECT * FROM `" + name + "` ORDER BY " + ordering).all()
    return result


def test_task_web_registration_is_cold_by_default_and_keeps_two_api_reads():
    code = """
import json, sys
from remotework import create_app
from remotework.models import Base
enabled = sys.argv[1] == '1'
app = create_app({'TESTING': True, 'SECRET_KEY': 'task-web-cold-' + 'a' * 40,
    'DATABASE_URL': 'mysql+pymysql://none:none@127.0.0.1:1/none',
    'ENABLE_TASK_QUERY_DRAFT': enabled, 'ENABLE_COMMON_WRITE_DRAFT': False})
rules = list(app.url_map.iter_rules())
client = app.test_client()
print(json.dumps({'web': [str(r) for r in rules if str(r).startswith('/tasks')],
    'api': [str(r) for r in rules if str(r).startswith('/api/v1/tasks')],
    'web_methods': sorted(set().union(*(r.methods for r in rules if str(r).startswith('/tasks')))),
    'task_tables': sorted(set(Base.metadata.tables) & {'tasks','task_assignments','task_managers'}),
    'web_imported': 'remotework.features.tasks.web' in sys.modules,
    'unauth_status': client.get('/tasks').status_code}))
app.extensions['db_engine'].dispose()
"""
    for enabled in (False, True):
        child = subprocess.run([sys.executable, "-c", code, "1" if enabled else "0"],
                               capture_output=True, text=True, encoding="utf-8", check=True)
        actual = json.loads(child.stdout)
        assert sorted(actual["web"]) == (["/tasks", "/tasks/<int:task_id>"] if enabled else [])
        assert len(actual["api"]) == (2 if enabled else 0)
        assert actual["web_methods"] == (["GET", "HEAD", "OPTIONS"] if enabled else [])
        assert actual["task_tables"] == (["task_assignments", "task_managers", "tasks"] if enabled else [])
        assert actual["web_imported"] is enabled
        assert actual["unauth_status"] == (302 if enabled else 404)


def test_web_own_list_all_five_status_filters_and_real_detail(web_rows):
    from remotework.features.tasks.models import TASK_STATUSES
    client = _client(web_rows)
    response = client.get("/tasks")
    assert _ids(response) == ["6", "1", "4", "5", "7"]
    assert "총 5건" in response.text and "취소됨" not in response.text
    for status, task_id in zip(TASK_STATUSES, ("6", "1", "7", "4", "5")):
        assert _ids(client.get("/tasks?scope=own&status=" + status)) == [task_id]
    detail = client.get("/tasks/1")
    assert detail.status_code == 200 and "업무 본문 1" in detail.text
    assert "직원" in detail.text and "2026-10-17" in detail.text and "2026-10-20" in detail.text
    assert "저장" not in detail.text and "취소됨" not in detail.text
    # Shared navigation reaches the registered read-only task screen.
    navigation = client.get("/features/tasks")
    assert navigation.status_code == 302 and navigation.headers["Location"] == "/tasks"
    assert _ids(client.get(navigation.headers["Location"])) == ["6", "1", "4", "5", "7"]


@pytest.mark.parametrize("enabled", [False, True])
def test_shared_task_navigation_keeps_authentication_and_activation_boundary(web_rows, enabled):
    from remotework import create_app
    source = web_rows[0]
    app = create_app(dict(source.config) | {"ENABLE_TASK_QUERY_DRAFT": enabled})
    try:
        anonymous = app.test_client()
        response = anonymous.get("/features/tasks")
        assert response.status_code == 302 and response.headers["Location"].startswith("/login")
        for user in ("staff1", "admin1"):
            client = _client((app, web_rows[1]), user)
            before = _snapshot(app)
            response = client.get("/features/tasks")
            home = client.get("/")
            assert home.status_code == 200 and 'href="/features/tasks"' in home.text
            if enabled:
                assert response.status_code == 302 and response.headers["Location"] == "/tasks"
                assert _ids(client.get(response.headers["Location"])) == (["6", "1", "4", "5", "7"] if user == "staff1" else ["3"])
            else:
                assert response.status_code == 200 and "기능 연결 준비 중" in response.text
                assert "task_web.index" not in app.view_functions
                assert client.get("/tasks").status_code == 404
            assert _snapshot(app) == before
    finally:
        app.extensions["db_engine"].dispose()


def test_web_managed_scope_and_direct_detail_use_current_server_authorization(web_rows):
    staff = _client(web_rows)
    admin = _client(web_rows, "admin1")
    admin2 = _client(web_rows, "admin2")
    assert _ids(admin.get("/tasks")) == ["3"]
    assert _ids(admin.get("/tasks?scope=managed")) == ["6", "1", "4", "5", "7"]
    assert _ids(admin2.get("/tasks?scope=managed")) == ["2", "3", "8"]
    assert admin.get("/tasks/1").status_code == 200
    assert admin.get("/tasks/3?scope=managed").status_code == 200
    # The scope in a detail URL is return context, never a relationship claim.
    assert staff.get("/tasks/1?scope=managed").status_code == 200
    _error(staff.get("/tasks?scope=managed", headers={"X-Role": "admin", "X-User-ID": str(web_rows[1]["admin1"])}), 403)
    for path in ("/tasks/2", "/tasks/8"):
        _error(staff.get(path), 403)
        _error(admin.get(path), 403)
    _error(admin2.get("/tasks/1"), 403)


def test_web_revokes_removed_manager_and_previous_assignee(web_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    app, ids = web_rows
    staff = _client(web_rows)
    admin = _client(web_rows, "admin1")
    assert staff.get("/tasks/1").status_code == 200
    assert admin.get("/tasks/1").status_code == 200
    with app.app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == 1, TaskManager.user_id == ids["admin1"]))
        db.add(TaskAssignment(task_id=1, revision=2, assignee_id=ids["staff2"], assigned_by=ids["admin2"]))
        db.get(TaskRecord, 1).current_assignment_revision = 2
    assert "1" not in _ids(staff.get("/tasks"))
    assert "1" not in _ids(admin.get("/tasks?scope=managed"))
    _error(staff.get("/tasks/1"), 403)
    _error(admin.get("/tasks/1"), 403)
    current = _client(web_rows, "staff2")
    assert current.get("/tasks/1").status_code == 200


def test_web_cancelled_marker_and_return_filter_context_do_not_change_status(web_rows):
    client = _client(web_rows)
    assert _ids(client.get("/tasks?status=in_progress&include_cancelled=true")) == ["1", "9"]
    listing = client.get("/tasks?scope=own&status=in_progress&include_cancelled=true")
    assert 'href="/tasks/9?' in listing.text and "취소됨" in listing.text
    detail = client.get("/tasks/9?scope=own&status=in_progress&include_cancelled=true")
    assert detail.status_code == 200 and "취소됨" in detail.text
    assert "진행중" in detail.text and "업무 본문 9" in detail.text
    back = unescape(re.search(r'class="task-back" href="([^"]+)"', detail.text).group(1))
    assert parse_qs(urlsplit(back).query) == {"scope": ["own"], "status": ["in_progress"], "include_cancelled": ["true"]}
    assert _ids(client.get(back)) == ["1", "9"]
    # Reading cancellation history does not require any write integration ACK.
    assert "task_lifecycle_integration_ack" not in web_rows[0].extensions
    assert web_rows[0].config["ENABLE_COMMON_WRITE_DRAFT"] is False


def test_web_empty_missing_and_forbidden_are_distinct(web_rows):
    client = _client(web_rows, "empty1")
    empty = client.get("/tasks")
    assert _ids(empty) == [] and "조회 조건에 맞는 업무가 없습니다." in empty.text
    _error(client.get("/tasks/999999"), 404)
    _error(client.get("/tasks/1"), 403)
    _error(client.get("/tasks/0"), 404)
    _error(client.get("/tasks/-1"), 404)


@pytest.mark.parametrize("query", [
    "scope=all", "scope=", "scope=own&scope=managed", "status=", "status=unknown",
    "status=approved&status=returned", "include_cancelled=1", "include_cancelled=True",
    "include_cancelled=true&include_cancelled=false", "page=1", "limit=1", "search=secret",
    "user_id=1", "assignee_id=1", "role=admin", "scope=own&sort=task_id",
    "status=approved%27%20OR%201%3D1",
])
def test_web_strict_query_allowlist_types_and_duplicates(web_rows, query):
    client = _client(web_rows)
    _error(client.get("/tasks?" + query), 400)
    _error(client.get("/tasks/1?" + query), 400)


def test_web_task_id_bounds_and_path_types(web_rows):
    client = _client(web_rows)
    _error(client.get("/tasks/9223372036854775808"), 400)
    for value in ("1.0", "abc", "1e0"):
        _error(client.get("/tasks/" + value), 404)


def test_web_cookie_channel_csrf_expiry_and_bearer_separation(web_rows):
    from remotework.auth import token_digest
    from remotework.db import transaction
    from remotework.models import AuthSession, utcnow
    app, _ = web_rows
    client = app.test_client()
    for path in ("/tasks", "/tasks/1"):
        unauth = client.get(path)
        assert unauth.status_code == 302 and urlsplit(unauth.location).path == "/login"
    assert client.post("/login", data={"login_id": "staff1", "password": "Staff-test-2026!"}).status_code == 400
    bearer = client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"}).json["data"]["access_token"]
    assert client.get("/tasks", headers={"Authorization": "Bearer " + bearer}).status_code == 302
    client.set_cookie("rw_session", bearer)
    assert client.get("/tasks").status_code == 302
    client = _client(web_rows)
    token = client.get_cookie("rw_session").value
    assert client.get("/tasks").status_code == 200
    assert client.get("/api/v1/tasks").status_code == 401
    with app.app_context(), transaction() as db:
        db.get(AuthSession, token_digest(token)).expires_at = utcnow() - timedelta(seconds=1)
    assert client.get("/tasks").status_code == 302
    assert client.get("/tasks/1").status_code == 302


def test_web_escapes_titles_assignees_and_multiline_descriptions(web_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord
    from remotework.models import User
    app, ids = web_rows
    with app.app_context(), transaction() as db:
        task = db.get(TaskRecord, 1)
        task.title = '<script>alert("title")</script>'
        task.description = '<img src=x onerror="alert(1)">\n두 번째 줄 & 본문'
        db.get(User, ids["staff1"]).display_name = '<svg onload="alert(2)">'
    client = _client(web_rows)
    for response in (client.get("/tasks"), client.get("/tasks/1")):
        assert response.status_code == 200
        assert "<script>" not in response.text and "<svg " not in response.text and "<img " not in response.text
        assert "&lt;script&gt;" in response.text and "&lt;svg " in response.text
        assert "default-src 'self'" in response.headers["Content-Security-Policy"]
        assert response.headers["Cache-Control"] == "no-store"
    detail = client.get("/tasks/1")
    assert "&lt;img " in detail.text and "\n두 번째 줄 &amp; 본문" in detail.text
    assert '<link rel="stylesheet" href="/static/tasks.css">' in detail.text


def test_web_full_allowed_list_is_sorted_and_not_truncated(web_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskRecord
    app, ids = web_rows
    with app.app_context(), transaction() as db:
        for task_id in range(100, 165):
            db.add(TaskRecord(task_id=task_id, title=f"전체 업무 {task_id}", description="전체 목록 자료",
                task_status="not_started", start_date=date(2026, 10, 17), due_date=date(2026, 10, 21),
                current_assignment_revision=1, created_by=ids["admin1"]))
        db.flush()
        for task_id in range(100, 165):
            db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=ids["staff1"], assigned_by=ids["admin1"]))
    client = _client(web_rows)
    expected = ["6", "1", "4", "5", "7"] + [str(value) for value in range(100, 165)]
    assert _ids(client.get("/tasks")) == expected
    assert _ids(client.get("/tasks?status=not_started")) == ["6"] + expected[5:]


def test_web_reads_and_rejected_methods_leave_database_rows_unchanged(web_rows):
    client = _client(web_rows)
    before = _snapshot(web_rows[0])
    assert client.get("/tasks").status_code == 200
    assert client.get("/tasks?include_cancelled=true").status_code == 200
    detail = client.get("/tasks/9")
    assert detail.status_code == 200
    token = re.search(r'name="csrf_token" value="([^"]+)"', detail.text).group(1)
    for method in ("POST", "PATCH", "PUT", "DELETE"):
        for path in ("/tasks", "/tasks/1"):
            _error(client.open(path, method=method, data={"csrf_token": token, "task_status": "approved"}), 405)
    if _snapshot(web_rows[0]) != before:
        pytest.fail("Read-only task pages or rejected methods changed owned fixture database rows")


def test_web_missing_task_table_is_common_503_not_empty_list(web_rows):
    app, _ = web_rows
    client = _client(web_rows, "admin1")
    engine = app.extensions["db_engine"]
    with engine.begin() as connection:
        connection.exec_driver_sql("RENAME TABLE task_managers TO task_web_test_managers_unavailable")
    try:
        response = client.get("/tasks?scope=managed")
        _error(response, 503)
        assert "task_web_test_managers_unavailable" not in response.text
    finally:
        with engine.begin() as connection:
            connection.exec_driver_sql("RENAME TABLE task_web_test_managers_unavailable TO task_managers")


def test_web_partial_lifecycle_storage_fails_closed_without_fallback(web_rows):
    app, _ = web_rows
    client = _client(web_rows)
    engine = app.extensions["db_engine"]
    with engine.begin() as connection:
        connection.exec_driver_sql("RENAME TABLE task_deadline_extensions TO task_web_test_extensions_unavailable")
    try:
        for path in ("/tasks", "/tasks/1"):
            _error(client.get(path), 503)
    finally:
        with engine.begin() as connection:
            connection.exec_driver_sql("RENAME TABLE task_web_test_extensions_unavailable TO task_deadline_extensions")


def test_web_unexpected_query_failure_is_common_sanitized_500(web_rows, monkeypatch):
    from remotework.features.tasks import web
    client = _client(web_rows)

    def fail(*_args):
        raise RuntimeError("task-web-private-test-message")

    monkeypatch.setattr(web, "list_tasks", fail)
    monkeypatch.setattr(web, "get_task", fail)
    for path in ("/tasks", "/tasks/1"):
        response = client.get(path)
        _error(response, 500)
        assert "task-web-private-test-message" not in response.text
