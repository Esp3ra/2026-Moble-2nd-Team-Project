"""Owner 2 continuity proof and owned-MySQL current-read integration.

Unknown or missing member 3 transitions stay None; no collaboration route,
current relation, deployment ACK or shared database is enabled by these tests.
"""
from datetime import datetime, timedelta
import json
import os
import subprocess
import sys

from flask import Flask
import pytest
from sqlalchemy import select, text
from sqlalchemy.engine import make_url

from remotework.auth import authenticated_transaction, create_user, load_identity
from remotework.db import get_db, transaction
from remotework.features.tasks import continuity
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, User
from remotework.write_contract import BIGINT_MAX


NOW = datetime(2026, 10, 21, 3, 4, 5)


def edge(action="task.managers_changed", before=2, *, old="in_progress", new=None,
         revision=1, new_revision=None):
    data = {"before": {"row_version": str(before), "task_status": old,
                       "assignment_revision": revision},
            "after": {"row_version": str(before + 1), "task_status": new or old,
                      "assignment_revision": new_revision or revision}}
    if action in continuity._LIFECYCLE:
        del data["before"]["assignment_revision"]
        data["before"]["due_date"] = "2026-10-25"
        data["after"]["due_date"] = "2026-10-26"
    return action, json.dumps(data)


def prove(rows, since=2, current=3, status="in_progress", revision=1):
    return continuity._prove(rows, since, current, status, revision)


@pytest.mark.parametrize("action", ["task.managers_changed", "task.deadline_extension_requested",
    "task.deadline_extension_approved", "task.deadline_extension_rejected", "task.deadline_extension_withdrawn"])
def test_pure_complete_noninterrupting_owner2_edge(action):
    assert prove([edge(action)]) is True


def test_pure_complete_chain_uses_versions_not_list_or_timestamp_order():
    assert prove([edge(before=3), edge(before=1), edge(before=2)], current=4) is True


@pytest.mark.parametrize("rows,current,status,revision", [
    ([edge("task.cancelled")], 3, "in_progress", 1),
    ([edge("task.rework_started", old="returned", new="in_progress")], 3, "in_progress", 1),
    ([edge("task.accepted", old="not_started", new="in_progress")], 3, "in_progress", 1),
    ([edge("task.reassigned", old="not_started", new_revision=2)], 3, "not_started", 2),
    ([edge("task.managers_changed", old="returned")], 3, "returned", 1),
])
def test_pure_complete_valid_interruption_proof(rows, current, status, revision):
    assert prove(rows, current=current, status=status, revision=revision) is False


@pytest.mark.parametrize("rows,current,status,revision", [
    ([], 3, "in_progress", 1),
    ([edge(), edge()], 3, "in_progress", 1),
    ([edge(before=1)], 3, "in_progress", 1),
    ([edge(before=3)], 3, "in_progress", 1),
    ([edge(), edge(before=3, revision=2)], 4, "in_progress", 2),
    ([edge()], 3, "returned", 1),
    ([edge()], 3, "in_progress", 2),
    ([edge(), ("task.future_write", "{}")], 3, "in_progress", 1),
    ([("task.managers_changed", "{")], 3, "in_progress", 1),
    ([("task.managers_changed", "[]")], 3, "in_progress", 1),
    ([("task.managers_changed", '{"before":{},"before":{},"after":{}}')], 3, "in_progress", 1),
    ([edge("task.accepted")], 3, "in_progress", 1),
    ([edge("task.reassigned", old="in_progress", new_revision=2)], 3, "in_progress", 2),
    ([edge("task.rework_started", before=4, old="returned", new="in_progress")], 5, "in_progress", 1),
])
def test_pure_missing_ambiguous_malformed_or_member3_gap_is_unknown(rows, current, status, revision):
    assert prove(rows, current=current, status=status, revision=revision) is None


@pytest.mark.parametrize("path,value", [
    (("before", "row_version"), "02"), (("after", "row_version"), 3),
    (("after", "row_version"), str(BIGINT_MAX + 1)), (("after", "row_version"), "4"),
    (("before", "assignment_revision"), True), (("after", "assignment_revision"), "1"),
    (("after", "task_status"), "future_state"),
])
def test_pure_strict_stored_values(path, value):
    action, raw = edge()
    data = json.loads(raw)
    data[path[0]][path[1]] = value
    assert prove([(action, json.dumps(data))]) is None


@pytest.mark.parametrize("value", ["20261025", "2026-10-25T00:00:00", "2026-02-30", None])
def test_pure_lifecycle_dates_are_canonical(value):
    action, raw = edge("task.deadline_extension_requested")
    data = json.loads(raw)
    data["before"]["due_date"] = value
    assert prove([(action, json.dumps(data))]) is None


@pytest.mark.parametrize("since,current", [(True, 3), (2, False), (0, 3), ("2", 3),
    (4, 3), (2, BIGINT_MAX + 1), (1, continuity.MAX_HISTORY_ROWS + 2)])
def test_pure_input_and_range_bounds(since, current):
    assert prove([], since=since, current=current) is None


def test_pure_bound_never_returns_partial_success(monkeypatch):
    monkeypatch.setattr(continuity, "MAX_HISTORY_ROWS", 1)
    assert prove([edge(), edge(before=1)]) is None


def test_pure_registration_keeps_owner_supplied_reader():
    app = Flask(__name__)
    callback = object()
    app.extensions["collaboration_continuity_reader"] = callback
    continuity.register_reader(app)
    assert app.extensions["collaboration_continuity_reader"] is callback
    del app.extensions["collaboration_continuity_reader"]
    continuity.register_reader(app)
    assert app.extensions["collaboration_continuity_reader"] is continuity.continuous_in_progress


@pytest.mark.parametrize("query,common", [(False, True), (True, False)])
def test_pure_cold_or_query_only_does_not_register_reader(query, common):
    env = {key: value for key, value in os.environ.items() if not key.startswith("REMOTEWORK_")}
    env["PYTHON_DOTENV_DISABLED"] = "1"
    script = "\n".join([
        "import sys",
        "from remotework import create_app",
        "app = create_app({'TESTING': True, 'SECRET_KEY': 'continuity-cold-' + 'x'*40,",
        "  'DATABASE_URL': 'mysql+pymysql://unused:unused@127.0.0.1:9/continuity_no_connection',",
        f"  'ENABLE_TASK_QUERY_DRAFT': {query!r}, 'ENABLE_COMMON_WRITE_DRAFT': {common!r}}})",
        "assert 'collaboration_continuity_reader' not in app.extensions",
        "assert 'remotework.features.tasks.continuity' not in sys.modules",
        "assert 'collaboration' not in app.blueprints",
    ])
    result = subprocess.run([sys.executable, "-c", script], env=env,
        capture_output=True, text=True, timeout=30)
    assert result.returncode == 0, result.stderr


@pytest.fixture
def continuity_rows(request, monkeypatch):
    url = os.getenv("MYSQL_TEST_URL", "")
    if os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1" or not url.startswith("mysql"):
        pytest.skip("Continuity DB checks require the explicit owned disposable MySQL runner")
    assert (make_url(url).database or "").startswith("remote_work_test_")
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_MANAGEMENT"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    for name in ("TASK_START", "TASK_CREATE", "TASK_LIFECYCLE", "TASK_LIFECYCLE_RESULT_READS",
                 "RESULTS", "SHARED_ATTACHMENTS", "COLLABORATION"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    assert app.extensions["collaboration_continuity_reader"] is continuity.continuous_in_progress
    with app.app_context(), transaction() as db:
        users = {user.login_id: user for user in db.scalars(select(User))}
        users["admin2"] = create_user(db, {"login_id": "admin2", "password": "Continuity-test-2026!",
            "display_name": "인계 관리자", "employee_no": "CONT-ADMIN2", "role": "admin"})
        ids = {name: user.id for name, user in users.items()}
        task = TaskRecord(title="continuity fixture", description="", task_status="in_progress",
            start_date=NOW.date(), due_date=(NOW + timedelta(days=3)).date(),
            current_assignment_revision=1, row_version=2, created_by=ids["admin1"],
            created_at=NOW, updated_at=NOW)
        db.add(task)
        db.flush()
        target = task.task_id
        db.add(TaskAssignment(task_id=target, revision=1, assignee_id=ids["staff1"],
            assigned_by=ids["admin1"], assigned_at=NOW, accepted_at=NOW))
        db.add(TaskManager(task_id=target, user_id=ids["admin1"]))
    return app, ids, target


def _headers(app):
    response = app.test_client().post("/api/v1/auth/login", json={
        "login_id": "admin1", "password": "Admin-test-2026!"})
    assert response.status_code == 200
    return {"Authorization": "Bearer " + response.json["data"]["access_token"]}


def _read(env, headers, *, since=2, current=3):
    app, _ids, target = env
    with app.test_request_context("/api/v1/tasks/context", headers=headers):
        load_identity()
        with authenticated_transaction() as db:
            return continuity.continuous_in_progress(db, target, since, current)


def _seed(db, target, actor_id, *, version=3, action="task.managers_changed", raw=None):
    task = db.get(TaskRecord, target)
    task.row_version = version
    event_action, details = edge(action, before=version - 1)
    db.add(AuditEvent(actor_id=actor_id, action=event_action, resource_type="task",
        resource_id=str(target), details_json=details if raw is None else raw))


def test_mysql_real_management_audit_proves_continuity_without_effects(continuity_rows):
    app, ids, target = continuity_rows
    headers = _headers(app)
    client = app.test_client()
    issued = client.post("/api/v1/write-requests/keys", headers=headers,
        json={"action": "task.managers_changed", "target": str(target)})
    assert issued.status_code == 201
    changed = client.patch(f"/api/v1/tasks/{target}/managers",
        headers=headers | {"Idempotency-Key": issued.json["data"]["request_key"]},
        json={"expected_row_version": "2", "expected_assignment_revision": 1,
              "manager_user_ids": [ids["admin1"], ids["admin2"]]})
    assert changed.status_code == 200, changed.json
    assert _read(continuity_rows, headers) is True
    # Consume the registered hook through member 4's existing unchanged bridge.
    from remotework.features.collaboration.task_bridge import current_task_context
    with app.test_request_context("/api/v1/tasks/context", headers=headers):
        load_identity()
        with authenticated_transaction() as db:
            value = current_task_context(db, target, since_version=2)
            assert value["continuous_in_progress"] is True
            assert value["row_version"] == 3 and value["assignment_revision"] == 1
    with app.app_context():
        db = get_db()
        assert db.get(TaskRecord, target).row_version == 3
        assert len(list(db.scalars(select(AuditEvent).where(AuditEvent.resource_type == "task")))) == 1


def test_mysql_current_audit_and_parent_after_old_identity_snapshot(continuity_rows):
    app, ids, target = continuity_rows
    headers = _headers(app)
    with app.test_request_context("/api/v1/tasks/context", headers=headers):
        load_identity()
        db = get_db()
        assert db.get(TaskRecord, target).row_version == 2
        assert list(db.scalars(select(AuditEvent).where(AuditEvent.resource_type == "task"))) == []
        first_connection = db.scalar(text("SELECT CONNECTION_ID()"))
        with app.extensions["db_factory"]() as other:
            assert other.scalar(text("SELECT CONNECTION_ID()")) != first_connection
            _seed(other, target, ids["admin1"])
            other.commit()
        # Prove old snapshot/cache before calling the real locked reader.
        assert db.get(TaskRecord, target).row_version == 2
        assert list(db.scalars(select(AuditEvent).where(AuditEvent.resource_type == "task"))) == []
        with authenticated_transaction() as current:
            assert continuity.continuous_in_progress(current, target, 2, 3) is True


@pytest.mark.parametrize("kind", ["missing", "duplicate", "malformed", "unknown", "stale", "member3_gap", "cap"])
def test_mysql_unknown_never_authorizes_continuity(continuity_rows, monkeypatch, kind):
    app, ids, target = continuity_rows
    headers = _headers(app)
    current = 3
    with app.app_context(), transaction() as db:
        db.get(TaskRecord, target).row_version = current
        if kind == "member3_gap":
            db.get(TaskRecord, target).row_version = current = 5
            db.add(AuditEvent(actor_id=ids["admin1"], action="submission.submitted",
                resource_type="submission", resource_id="1",
                details_json=json.dumps({"task_id": str(target), "row_version": "3"})))
            action, raw = edge("task.rework_started", before=4, old="returned", new="in_progress")
            db.add(AuditEvent(actor_id=ids["admin1"], action=action,
                resource_type="task", resource_id=str(target), details_json=raw))
        elif kind != "missing":
            _seed(db, target, ids["admin1"], action="task.future_write" if kind == "unknown" else "task.managers_changed",
                  raw="{" if kind == "malformed" else None)
            if kind in ("duplicate", "cap"):
                _seed(db, target, ids["admin1"])
    if kind == "stale":
        current = 4
    if kind == "cap":
        monkeypatch.setattr(continuity, "MAX_HISTORY_ROWS", 1)
    assert _read(continuity_rows, headers, current=current) is None


def test_mysql_requires_current_authenticated_session(continuity_rows):
    app, _ids, target = continuity_rows
    headers = _headers(app)
    with app.test_request_context("/api/v1/tasks/context", headers=headers):
        load_identity()
        with pytest.raises(RuntimeError, match="authenticated_transaction"):
            continuity.continuous_in_progress(get_db(), target, 2, 2)
        with authenticated_transaction():
            with app.extensions["db_factory"]() as different:
                with pytest.raises(RuntimeError, match="authenticated_transaction"):
                    continuity.continuous_in_progress(different, target, 2, 2)
