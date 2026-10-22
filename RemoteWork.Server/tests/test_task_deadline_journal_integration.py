"""Actual owner2 W1 mutations consume the merged member1 journal.

Explicitly owned MySQL/raw DDL only. No audit-boundary record injection,
baseline, shared ACK, owner1/3/4/5 product edits or SQLite fallback.
"""
import base64
from datetime import datetime, timedelta, timezone
import hashlib
from html.parser import HTMLParser
import json
import os
from types import SimpleNamespace

import pytest
from sqlalchemy import delete, inspect, select
from sqlalchemy.engine import make_url
from sqlalchemy.orm import Session

from remotework import create_app, deadline_events as journal, deadline_http
from remotework.auth import create_user
from remotework.common import AppError
from remotework.db import transaction
from remotework.features import results, shared_attachments as shared
from remotework.features.tasks import creation, lifecycle, management
from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
from remotework.features.tasks.lifecycle_support import INTEGRATED_ACK
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AdminGuard, AuditEvent, Base, SchemaVersion
from remotework.write_requests import WriteReceipt


PASSWORD = "Owner2-journal-synthetic-2026!"
pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_DEADLINE_JOURNAL") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Requires explicit owned MySQL deadline-journal runner; no SQLite fallback")


@pytest.fixture
def env(request, tmp_path):
    url = os.environ["MYSQL_TEST_URL"]
    if not (make_url(url).database or "").startswith("remote_work_test_"):
        raise RuntimeError("Refusing non-disposable database")
    mode = getattr(request, "param", "enabled")
    config = dict(TESTING=True, DATABASE_URL=url, SECRET_KEY="owner2-journal-test-" + "a" * 32,
        COOKIE_SECURE=False, RESULTS_CONTEXT_MODE="local-development",
        RESULTS_STORAGE_ROOT=str(tmp_path / "owned-shared-files"))
    for feature in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_START", "TASK_MANAGEMENT",
                    "TASK_LIFECYCLE", "RESULTS", "SHARED_ATTACHMENTS"):
        config["ENABLE_" + feature + "_DRAFT"] = True
    for feature in ("TASK_LIFECYCLE_RESULT_READS", "NOTICE", "NOTICE_POPUP", "CHAT", "COLLABORATION",
                    "COLLABORATION_RESULT_READS", "DEVICE_ENROLLMENT", "ENROLLMENT_DURABILITY",
                    "ENROLLMENT_RECOVERY", "DEVICE_IDENTITY"):
        config["ENABLE_" + feature + "_DRAFT"] = False
    config["ENABLE_DEADLINE_ALERTS_DRAFT"] = mode == "enabled"
    config["ENABLE_DEADLINE_RESULT_READS_DRAFT"] = mode == "readonly"
    app = create_app(config)
    engine = app.extensions["db_engine"]
    assert engine.dialect.name == "mysql"
    assert app.extensions["results_shared_attachments"] is shared
    assert app.extensions["results_facts"] is results.local_facts
    app.extensions["task_lifecycle_integration_ack"] = dict(INTEGRATED_ACK)  # Owned TESTING app only.
    with app.app_context(), transaction() as db:
        # Draft Core tables intentionally are NOT Base metadata/startup DDL.
        db.execute(deadline_http.confirmations.delete())
        db.execute(journal.heads.delete())
        db.execute(journal.journal.delete())
        existing = set(inspect(db.connection()).get_table_names())
        for table in reversed(Base.metadata.sorted_tables):
            if table.name in existing and table.name not in {"admin_guard", "schema_versions"}:
                db.execute(delete(table))
        if db.get(AdminGuard, 1) is None:
            db.add(AdminGuard(id=1))
        if db.get(SchemaVersion, 1) is None:
            db.add(SchemaVersion(version=1))
        users = {name: create_user(db, dict(login_id=name, password=PASSWORD,
            display_name="TEST-" + name, employee_no="JOURNAL-" + name,
            department="TEST", role=role)) for name, role in
            (("manager", "admin"), ("manager2", "admin"), ("worker", "employee"), ("other", "employee"))}
        ids = {name: user.id for name, user in users.items()}
        db.add(results.DepartmentReviewer(department="TEST", reviewer_id=ids["manager"]))
    now = datetime.now(timezone.utc)
    today = now.astimezone(timezone(timedelta(hours=9))).date()
    value = SimpleNamespace(app=app, ids=ids, headers={}, today=today, mode=mode)
    try:
        yield value
    finally:
        app.extensions.pop("task_lifecycle_integration_ack", None)
        engine.dispose()


def auth(env, actor="manager"):
    if actor not in env.headers:
        response = env.app.test_client().post("/api/v1/auth/login",
            json=dict(login_id=actor, password=PASSWORD))
        assert response.status_code == 200, response.json
        env.headers[actor] = {"Authorization": "Bearer " + response.json["data"]["access_token"]}
    return env.headers[actor]


def request_key(env, action, target="new", actor="manager"):
    response = env.app.test_client().post("/api/v1/write-requests/keys", headers=auth(env, actor),
        json=dict(action=action, target=str(target)))
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def send(env, action, target, path, body, *, actor="manager", key=None, method="POST", expected=200):
    key = key or request_key(env, action, target, actor)
    response = env.app.test_client().open("/api/v1/" + path, method=method,
        headers=auth(env, actor) | {"Idempotency-Key": key}, json=body)
    assert response.status_code == expected, response.json
    return response.json["data"] if expected < 400 else response.json["error"]["code"]


def creation_body(env, **changes):
    return dict(title="TEST journal original", description="owner2 actual product hook",
        start_date=env.today.isoformat(), due_date=(env.today + timedelta(days=14)).isoformat(),
        assignee_id=env.ids["worker"], manager_user_ids=[env.ids["manager"]]) | changes


def create(env, **changes):
    return send(env, "task.created", "new", "tasks", creation_body(env, **changes), expected=201)["task_id"]


def task(env, target):
    with env.app.extensions["db_engine"].connect() as db:
        return db.execute(select(TaskRecord.__table__).where(TaskRecord.task_id == int(target))).mappings().one()


def versions(env, target):
    row = task(env, target)
    return dict(expected_row_version=str(row.row_version), expected_assignment_revision=row.current_assignment_revision)


def edit_body(env, target, **changes):
    row = task(env, target)
    return dict(title=row.title, description=row.description, start_date=row.start_date.isoformat(),
        due_date=row.due_date.isoformat()) | versions(env, target) | changes


def edit(env, target, **changes):
    return send(env, "task.updated", target, f"tasks/{target}", edit_body(env, target, **changes), method="PATCH")


def events(env, target=None):
    statement = select(journal.journal).order_by(journal.journal.c.task_id, journal.journal.c.event_order)
    if target is not None:
        statement = statement.where(journal.journal.c.task_id == int(target))
    with env.app.extensions["db_engine"].connect() as db:
        return [dict(row) for row in db.execute(statement).mappings()]


def snapshot(env):
    tables = (TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__,
        TaskCancellation.__table__, TaskDeadlineExtension.__table__, AuditEvent.__table__, WriteReceipt.__table__,
        journal.journal, journal.heads, deadline_http.confirmations,
        shared.SharedFile.__table__, shared.SharedLink.__table__, shared.SharedChunk.__table__)
    with env.app.extensions["db_engine"].connect() as db:
        return tuple((table.name, sorted((tuple(row) for row in db.execute(select(table))), key=repr)) for table in tables)


def start(env, target):
    return send(env, "task.accepted", target, f"tasks/{target}/accept", versions(env, target), actor="worker")


def extension(env, target):
    due = task(env, target).due_date + timedelta(days=3)
    answer = send(env, "task.deadline_extension_requested", target, f"tasks/{target}/deadline-extensions",
        versions(env, target) | dict(reason="TEST actual extension", requested_due_date=due.isoformat()), actor="worker")
    return answer["extension_id"], due


def test_registered_creation_records_initial_event_and_retained_replay_once(env):
    body = creation_body(env)
    key = request_key(env, "task.created")
    first = send(env, "task.created", "new", "tasks", body, key=key, expected=201)
    original = snapshot(env)
    assert send(env, "task.created", "new", "tasks", body, key=key, expected=201) == first
    assert snapshot(env) == original
    row, = events(env)
    assert (row["source_action"], row["source_row_version"], row["event_order"], row["before_due_date"]) == ("task.created", 1, 1, None)
    assert row["due_date"].isoformat() == body["due_date"] and row["actor_id"] == env.ids["manager"]


def test_actual_general_update_a_b_a_records_distinct_events_and_replay_once(env):
    target = create(env)
    original = task(env, target).due_date
    body = edit_body(env, target, due_date=(original + timedelta(days=2)).isoformat())
    key = request_key(env, "task.updated", target)
    first = send(env, "task.updated", target, f"tasks/{target}", body, key=key, method="PATCH")
    before = snapshot(env)
    assert send(env, "task.updated", target, f"tasks/{target}", body, key=key, method="PATCH") == first
    assert snapshot(env) == before
    edit(env, target, due_date=original.isoformat())
    rows = events(env, target)
    assert [row["event_order"] for row in rows] == [1, 2, 3]
    assert len({row["event_id"] for row in rows}) == 3
    assert rows[0]["due_date"] == rows[-1]["due_date"]


@pytest.mark.parametrize("field", ["title", "description", "start_date"])
def test_non_due_general_edit_keeps_existing_event(env, field):
    target = create(env)
    before = events(env)
    changed = {"title": "TEST different title", "description": "TEST new description",
               "start_date": (env.today + timedelta(days=1)).isoformat()}[field]
    edit(env, target, **{field: changed})
    assert events(env) == before and task(env, target).row_version == 2


def test_same_date_with_other_change_never_calls_recorder(env, monkeypatch):
    target = create(env)
    before = events(env)
    monkeypatch.setattr(journal, "record_change", lambda *a, **k: (_ for _ in ()).throw(AssertionError("Unexpected date recorder")))
    edit(env, target, title="TEST same due + title")
    assert events(env) == before


def test_real_attachment_only_edit_keeps_event(env):
    target = create(env)
    data = b"actual owner3 attachment for owner2 journal classification"
    base = "/api/v1/results/shared/uploads"
    client = env.app.test_client()
    response = client.post(base + "/start", headers=auth(env), json=dict(name="journal.txt", size=len(data),
        sha256=hashlib.sha256(data).hexdigest(), task_id=target))
    assert response.status_code == 201, response.json
    identifier = response.json["data"]["attachment_id"]
    assert client.post(base + f"/{identifier}/chunks", headers=auth(env),
        json=dict(offset=0, data=base64.b64encode(data).decode())).status_code == 200
    assert client.post(base + f"/{identifier}/complete", headers=auth(env), json={}).status_code == 200
    before = events(env)
    edit(env, target, attachment_ids=[identifier])
    assert events(env) == before and task(env, target).row_version == 2
    with env.app.extensions["db_engine"].connect() as db:
        assert list(db.execute(select(shared.SharedLink).where(shared.SharedLink.attachment_id == identifier)))


def test_start_request_and_approve_only_records_actual_approval_once(env):
    target = create(env)
    before = events(env)
    start(env, target)
    identifier, due = extension(env, target)
    assert events(env) == before
    body = versions(env, target)
    key = request_key(env, "task.deadline_extension_approved", target)
    path = f"tasks/{target}/deadline-extensions/{identifier}/approve"
    answer = send(env, "task.deadline_extension_approved", target, path, body, key=key)
    after = snapshot(env)
    assert send(env, "task.deadline_extension_approved", target, path, body, key=key) == answer
    assert snapshot(env) == after
    rows = events(env)
    assert len(rows) == 2 and rows[-1]["source_action"] == "task.deadline_extension_approved"
    assert rows[-1]["before_due_date"] == before[0]["due_date"] and rows[-1]["due_date"] == due


@pytest.mark.parametrize("outcome", ["reject", "withdraw", "cancel"])
def test_non_date_lifecycle_actions_preserve_event(env, outcome):
    target = create(env)
    start(env, target)
    identifier, _ = extension(env, target)
    before = events(env)
    if outcome == "cancel":
        action, path, body, actor = "task.cancelled", f"tasks/{target}/cancel", dict(reason="TEST cancel"), "manager"
    else:
        action = "task.deadline_extension_" + ("rejected" if outcome == "reject" else "withdrawn")
        path = f"tasks/{target}/deadline-extensions/{identifier}/{outcome}"
        body, actor = (dict(reason="TEST reject"), "manager") if outcome == "reject" else ({}, "worker")
    send(env, action, target, path, versions(env, target) | body, actor=actor)
    assert events(env) == before


@pytest.mark.parametrize("action", ["task.created", "task.updated", "task.deadline_extension_approved"])
@pytest.mark.parametrize("stage", ["record", "audit", "receipt"])
def test_record_and_downstream_failure_roll_back_task_journal_audit_and_receipt(env, monkeypatch, action, stage):
    if action == "task.created":
        target, body, path, method = "new", creation_body(env), "tasks", "POST"
    else:
        target = create(env)
        if action == "task.updated":
            body = edit_body(env, target, due_date=(task(env, target).due_date + timedelta(days=1)).isoformat())
            path, method = f"tasks/{target}", "PATCH"
        else:
            start(env, target)
            identifier, _ = extension(env, target)
            body, path, method = versions(env, target), f"tasks/{target}/deadline-extensions/{identifier}/approve", "POST"
    key = request_key(env, action, target)
    before = snapshot(env)
    if stage == "record":
        original = journal.record_change
        def fail_record(*args, **kwargs):
            value = original(*args, **kwargs)
            assert value is not None
            assert args[0].scalar(select(journal.journal.c.event_id).where(journal.journal.c.event_id == value.event_id)) == value.event_id
            raise AppError(503, "TEST_AFTER_RECORD", "TEST after actual event INSERT")
        monkeypatch.setattr(journal, "record_change", fail_record)
    elif stage == "audit":
        module = {"task.created": creation, "task.updated": management, "task.deadline_extension_approved": lifecycle}[action]
        original = module.audit
        def fail_audit(*args, **kwargs):
            original(*args, **kwargs)
            raise AppError(503, "TEST_AFTER_AUDIT", "TEST audit failure after actual event")
        monkeypatch.setattr(module, "audit", fail_audit)
    else:
        original = Session.add
        def fail_receipt(db, instance, *args, **kwargs):
            value = original(db, instance, *args, **kwargs)
            if isinstance(instance, WriteReceipt) and instance.action == action:
                db.flush()
                raise AppError(503, "TEST_AFTER_RECEIPT", "TEST after actual receipt INSERT")
            return value
        monkeypatch.setattr(Session, "add", fail_receipt)
    send(env, action, target, path, body, key=key, method=method, expected=503)
    assert snapshot(env) == before


def test_legacy_missing_head_blocks_changed_due_without_auto_baseline(env):
    target = create(env)
    with env.app.extensions["db_engine"].begin() as db:
        db.execute(journal.heads.delete())
        db.execute(journal.journal.delete())
    key = request_key(env, "task.updated", target)
    before = snapshot(env)
    code = send(env, "task.updated", target, f"tasks/{target}",
        edit_body(env, target, due_date=(task(env, target).due_date + timedelta(days=1)).isoformat()),
        key=key, method="PATCH", expected=503)
    assert code == "DEADLINE_EVENT_UNKNOWN" and snapshot(env) == before


@pytest.mark.parametrize("env", ["disabled", "readonly"], indirect=True)
def test_off_and_result_only_preserve_existing_task_writes_without_recording(env):
    target = create(env)
    edit(env, target, due_date=(env.today + timedelta(days=17)).isoformat())
    assert events(env) == []
    assert task(env, target).row_version == 2
    assert env.app.test_client().get("/api/v1/deadline-alerts", headers=auth(env, "worker")).status_code == 404


def test_enabled_configuration_without_installed_provider_fails_closed(env):
    env.app.extensions.pop("deadline_http")
    key = request_key(env, "task.created")
    before = snapshot(env)
    assert send(env, "task.created", "new", "tasks", creation_body(env), key=key,
        expected=503) == "DEADLINE_EVENT_CONNECTION_REQUIRED"
    assert snapshot(env) == before


def test_actual_created_event_is_consumed_by_deadline_http_confirmation(env):
    target = create(env, due_date=env.today.isoformat())
    client = env.app.test_client()
    response = client.get("/api/v1/deadline-alerts", headers=auth(env, "worker"))
    assert response.status_code == 200, response.json
    item, = response.json["data"]["items"]
    actual, = events(env)
    assert item["task_id"] == target and item["event_id"] == actual["event_id"]
    assert (item["due_date"], item["row_version"], item["assignment_revision"], item["assignee_id"], item["status"]) == (
        env.today.isoformat(), "1", 1, env.ids["worker"], "not_started")
    key = request_key(env, "deadline_alert.confirmed", item["target"], "worker")
    before = task(env, target).row_version
    receipt = send(env, "deadline_alert.confirmed", item["target"],
        f"deadline-alerts/{item['target']}/confirm", dict(task_id=target,
            expected_row_version=item["row_version"], expected_assignment_revision=item["assignment_revision"]),
        actor="worker", key=key)
    assert receipt["event_id"] == actual["event_id"] and receipt["assignment_revision"] == 1
    result = client.post("/api/v1/write-requests/result", headers=auth(env, "worker") | {"Idempotency-Key": key},
        json=dict(action="deadline_alert.confirmed", target=item["target"]))
    assert result.status_code == 200 and result.json["data"]["result"] == receipt
    assert len(events(env)) == 1 and task(env, target).row_version == before


class HiddenInputs(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.values = {}
        self.feed(source)

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag == "input" and values.get("type") == "hidden" and values.get("name"):
            self.values.setdefault(values["name"], []).append(values.get("value", ""))


def hidden(response, name):
    values = HiddenInputs(response.text).values.get(name, [])
    assert values and len(set(values)) == 1, (response.status_code, name)
    return values[0]


def web_client(env):
    client = env.app.test_client()
    response = client.post("/login", data=dict(csrf_token=hidden(client.get("/login"), "csrf_token"),
        login_id="manager", password=PASSWORD))
    assert response.status_code == 302
    return client


@pytest.mark.parametrize("action", ["create", "edit"])
def test_web_preparation_writes_nothing_and_explicit_execution_uses_real_journal(env, action):
    client = web_client(env)
    if action == "create":
        target, path, body = None, "/tasks/new", creation_body(env)
    else:
        target = create(env)
        path = f"/tasks/{target}/manage/edit?scope=managed"
        body = edit_body(env, target, due_date=(env.today + timedelta(days=16)).isoformat())
        body.pop("expected_row_version"); body.pop("expected_assignment_revision")
    page = client.get(path)
    assert page.status_code == 200
    body.update(csrf_token=hidden(page, "csrf_token"), op="prepare", q="", page="1")
    if action == "edit":
        body["draft_ticket"] = hidden(page, "draft_ticket")
    before = snapshot(env)
    prepared = client.post(path, data=body)
    assert prepared.status_code == 200 and snapshot(env) == before
    ticket = hidden(prepared, "ticket")
    execution = dict(ticket=ticket, csrf_token=hidden(prepared, "csrf_token"))
    complete = client.post("/tasks/write/execute", data=execution)
    assert complete.status_code == 200 and "처리 결과가 확인되었습니다" in complete.text
    after = snapshot(env)
    assert client.post("/tasks/write/execute", data=execution).status_code == 200
    assert client.post("/tasks/write/result", data=execution).status_code == 200
    assert snapshot(env) == after
    rows = events(env)
    assert len(rows) == (1 if action == "create" else 2)
    last = rows[-1]
    assert last["source_action"] == ("task.created" if action == "create" else "task.updated")
    row = task(env, last["task_id"])
    assert last["source_row_version"] == row.row_version and last["due_date"] == row.due_date
    result = env.app.test_client().get("/api/v1/deadline-alerts", headers=auth(env, "worker"))
    item, = result.json["data"]["items"]
    assert (item["event_id"], item["row_version"], item["due_date"]) == (
        last["event_id"], str(row.row_version), row.due_date.isoformat())


def test_actual_reassignment_a_b_a_preserves_event_but_changes_binding_and_current_access(env):
    target = create(env, due_date=env.today.isoformat())
    before = events(env)
    original = env.app.test_client().get("/api/v1/deadline-alerts", headers=auth(env, "worker")).json["data"]["items"][0]
    for actor, revision in (("other", 2), ("worker", 3)):
        send(env, "task.reassigned", target, f"tasks/{target}/reassign",
            versions(env, target) | dict(assignee_id=env.ids[actor]))
        assert events(env) == before
        response = env.app.test_client().get("/api/v1/deadline-alerts", headers=auth(env, actor))
        item, = response.json["data"]["items"]
        assert item["event_id"] == original["event_id"] and item["target"] != original["target"]
        assert (item["assignment_revision"], item["row_version"], item["assignee_id"], item["due_date"]) == (
            revision, str(revision), env.ids[actor], env.today.isoformat())
        previous_actor = "worker" if actor == "other" else "other"
        denied = env.app.test_client().get("/api/v1/deadline-alerts", headers=auth(env, previous_actor))
        assert denied.json["data"]["items"] == []


def test_actual_manager_change_preserves_event_and_assignment(env):
    target = create(env)
    before = events(env)
    send(env, "task.managers_changed", target, f"tasks/{target}/managers", versions(env, target) |
        dict(manager_user_ids=[env.ids["manager"], env.ids["manager2"]]), method="PATCH")
    assert events(env) == before
    assert (task(env, target).row_version, task(env, target).current_assignment_revision) == (2, 1)
    with env.app.extensions["db_engine"].connect() as db:
        assert set(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == int(target)))) == {
            env.ids["manager"], env.ids["manager2"]}


def test_actual_submit_return_rework_resubmit_approve_preserves_event_and_exposes_original_facts(env):
    target = create(env, due_date=env.today.isoformat())
    original = events(env)
    states = [("not_started", 1)]

    def observe(status, version):
        assert events(env) == original
        response = env.app.test_client().get("/api/v1/deadline-alerts", headers=auth(env, "worker"))
        assert response.status_code == 200, response.json
        item, = response.json["data"]["items"]
        assert (item["event_id"], item["assignment_revision"], item["status"], item["row_version"], item["due_date"]) == (
            original[0]["event_id"], 1, status, str(version), env.today.isoformat())
        states.append((status, version))
        return item

    start(env, target); observe("in_progress", 2)
    first = send(env, "submission.submitted", target, f"results/tasks/{target}/submissions",
        versions(env, target) | dict(description="TEST actual first round", attachment_ids=[]), actor="worker", expected=201)
    submitted = observe("submitted", 3)
    key = request_key(env, "deadline_alert.confirmed", submitted["target"], "worker")
    before = snapshot(env)
    assert send(env, "deadline_alert.confirmed", submitted["target"], f"deadline-alerts/{submitted['target']}/confirm",
        dict(task_id=target, expected_row_version="3", expected_assignment_revision=1), actor="worker", key=key,
        expected=409) == "DEADLINE_EVENT_CHANGED"
    assert snapshot(env) == before
    send(env, "submission.returned", first["submission_id"], f"results/submissions/{first['submission_id']}/decisions",
        versions(env, target) | dict(outcome="returned", reason="TEST revise"))
    observe("returned", 4)
    with env.app.extensions["db_engine"].connect() as db:
        accepted = db.scalar(select(TaskAssignment.accepted_at).where(TaskAssignment.task_id == int(target)))
    send(env, "task.rework_started", target, f"tasks/{target}/rework-start", versions(env, target), actor="worker")
    observe("in_progress", 5)
    second = send(env, "submission.submitted", target, f"results/tasks/{target}/submissions",
        versions(env, target) | dict(description="TEST actual second round", attachment_ids=[]), actor="worker", expected=201)
    observe("submitted", 6)
    send(env, "submission.approved", second["submission_id"], f"results/submissions/{second['submission_id']}/decisions",
        versions(env, target) | dict(outcome="approved", reason=""))
    observe("approved", 7)
    assert states == [("not_started", 1), ("in_progress", 2), ("submitted", 3), ("returned", 4),
        ("in_progress", 5), ("submitted", 6), ("approved", 7)]
    with env.app.extensions["db_engine"].connect() as db:
        assert db.scalar(select(TaskAssignment.accepted_at).where(TaskAssignment.task_id == int(target))) == accepted
        assert [(row.round_no, row.status) for row in db.execute(select(results.Submission.round_no,
            results.Submission.status).where(results.Submission.task_id == int(target)).order_by(results.Submission.round_no))] == [
                (1, "returned"), (2, "approved")]


@pytest.mark.parametrize("env", ["disabled", "readonly"], indirect=True)
def test_off_and_result_only_keep_writes_working_without_physical_draft_tables(env):
    # Owned-test DDL hiding only; restore every original name even after failure.
    names = (deadline_http.confirmations.name, journal.heads.name, journal.journal.name)
    engine = env.app.extensions["db_engine"]
    with engine.begin() as db:
        db.exec_driver_sql("RENAME TABLE " + ", ".join(f"`{name}` TO `{name}_owned_hidden`" for name in names))
    try:
        target = create(env)
        edit(env, target, due_date=(env.today + timedelta(days=18)).isoformat())
        assert task(env, target).row_version == 2
        with engine.connect() as db:
            assert not any(inspect(db).has_table(name) for name in names)
    finally:
        with engine.begin() as db:
            db.exec_driver_sql("RENAME TABLE " + ", ".join(f"`{name}_owned_hidden` TO `{name}`" for name in names))
