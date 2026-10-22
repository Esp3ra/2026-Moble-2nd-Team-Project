"""Owned-MySQL evidence at the existing member2 write transaction boundaries.

No deadline event store, public callback, source adapter, API or migration is
implemented here. Only this test monkeypatches the existing audit boundary and
stores TEST observations in audit_events, in the ORIGINAL writer transaction.
Those probe rows are not alert events or an agreed event identity/schema.
The existing fixture applies original raw002/004/006/009 to a disposable DB.
Attachment-only classification uses an explicitly named provider double; this
suite does not claim raw013, file IO, shared deployment or owner1 hook coverage.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import json
from threading import Event, get_ident
from types import SimpleNamespace

import pytest
from flask import g
from sqlalchemy import event, select
from sqlalchemy.orm import Session

# Reuse the protected, explicitly disposable raw-DDL fixture, not conftest's app.
from test_task_deadline_facts import (deadline_app, deadline, guarded, headers,
                                    persisted)
from remotework import task_write_support, write_requests
from remotework.common import AppError, audit as actual_audit
from remotework.db import get_db
from remotework.features.tasks import creation, lifecycle, management, starting
from remotework.features.tasks.deadline_facts import read_own_deadline_fact
from remotework.features.tasks.lifecycle_models import TaskDeadlineExtension
from remotework.features.tasks.lifecycle_support import TEST_DOUBLE_ACK
from remotework.features.tasks.models import TaskRecord
from remotework.features.tasks.start_transition import TaskStartAction
from remotework.models import AuditEvent
from remotework.write_requests import WriteReceipt


PROBE = "test.deadline_write_probe"


@pytest.fixture(scope="module")
def boundary_app(deadline_app):
    app = deadline_app
    assert app.testing and not app._got_first_request
    app.config.update(ENABLE_COMMON_WRITE_DRAFT=True, ENABLE_TASK_LIFECYCLE_DRAFT=True)
    # Actual common keys/authorizers; no new deadline-specific action or hook.
    app.register_blueprint(write_requests.bp)
    task_write_support.install(app)
    lifecycle.register_lifecycle_actions(app)
    app.extensions["task_lifecycle_integration_ack"] = dict(TEST_DOUBLE_ACK)
    assert not app.config["ENABLE_RESULTS_DRAFT"] and "results" not in app.blueprints
    return app


@pytest.fixture
def writes(boundary_app, deadline, monkeypatch):
    env = deadline
    assert env.app is boundary_app
    env.calls, env.active_db = [], None
    env.fail_action = env.fail_stage = None
    env.probe_gate = None
    today = datetime.now(timezone.utc).astimezone(timezone(timedelta(hours=9))).date()
    env.body = dict(title="TEST original task", description="TEST original body",
        start_date=today.isoformat(), due_date=(today + timedelta(days=14)).isoformat(),
        assignee_id=env.ids["staff1"], manager_user_ids=[env.ids["admin1"]])

    def observed_audit(action, resource_type, resource_id, details=None, actor_id=None):
        db = get_db()
        assert db is env.active_db and g.write_transaction_active
        assert db.connection().in_transaction()
        task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == int(resource_id))
            .with_for_update().execution_options(populate_existing=True))
        assert task is not None
        details = details or {}
        if action == "task.updated":
            old, new = details["before_due_date"], details["due_date"]
            actual_change = "due_date" in details["changed_fields"] and old != new
        elif action.startswith("task.deadline_extension_"):
            old, new = details["before"]["due_date"], details["after"]["due_date"]
            actual_change = action == "task.deadline_extension_approved" and old != new
        else:
            # Creation has no due-bearing audit. Do not invent initial evidence
            # by reading its current task date as an alleged creation event.
            old = new = None
            actual_change = False
        if new is not None:
            assert task.due_date.isoformat() == new
            assert int(details["after"]["row_version"]) == task.row_version
        observation = dict(original_action=action, before_due_date=old,
            due_date=new, date_changed=actual_change,
            stored_row_version=str(task.row_version),
            assignment_revision=task.current_assignment_revision)
        env.calls.append((action, int(resource_id)))
        actual_audit(action, resource_type, resource_id, details, actor_id)
        db.add(AuditEvent(actor_id=g.user.id, action=PROBE,
            resource_type="test_deadline_probe", resource_id=str(resource_id),
            details_json=json.dumps(observation)))
        db.flush()  # Prove actual INSERTs roll back, not just Python objects.
        if env.probe_gate is not None and actual_change:
            env.probe_gate()
        if (env.fail_stage, env.fail_action) == ("callback", action):
            raise AppError(503, "TEST_DEADLINE_PROBE_FAILED", "TEST original-TX probe failure")

    for module in (creation, management, lifecycle):
        monkeypatch.setattr(module, "audit", observed_audit)
    add = Session.add

    def add_with_receipt_failure(db, instance, *args, **kwargs):
        result = add(db, instance, *args, **kwargs)
        if (isinstance(instance, WriteReceipt) and db is env.active_db
                and (env.fail_stage, env.fail_action) == ("receipt", instance.action)):
            db.flush()
            raise AppError(503, "TEST_DEADLINE_RECEIPT_FAILED", "TEST after-receipt failure")
        return result

    monkeypatch.setattr(Session, "add", add_with_receipt_failure)
    return env


def key(env, action, target="new", actor="admin1"):
    response = env.app.test_client().post("/api/v1/write-requests/keys",
        headers=headers(env, actor), json={"action": action, "target": str(target)})
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def invoke(env, action, target, body, original_key, *, actor="admin1", extension_id=None):
    with guarded(env, actor) as db:
        env.active_db = db
        try:
            if action == "task.created":
                return creation.execute_creation(db, body=body, key=original_key)
            if action == "task.updated":
                return management.execute_management(db, target=str(target), action=action,
                    body=body, key=original_key)
            return lifecycle.execute_lifecycle(db, target=str(target), action=action,
                body=body, key=original_key, extension_id=extension_id)
        finally:
            env.active_db = None


def create_task(env):
    return invoke(env, "task.created", "new", dict(env.body), key(env, "task.created")).data["task_id"]


def version_body(env, target):
    with env.app.app_context():
        row = get_db().get(TaskRecord, int(target))
        return dict(expected_row_version=str(row.row_version),
            expected_assignment_revision=row.current_assignment_revision)


def edit_body(env, target, **changes):
    return {name: env.body[name] for name in ("title", "description", "start_date", "due_date")} | version_body(env, target) | changes


def probes(env, *, changed=None):
    with env.app.app_context():
        values = [json.loads(row.details_json) for row in get_db().scalars(
            select(AuditEvent).where(AuditEvent.action == PROBE).order_by(AuditEvent.id))]
        return values if changed is None else [value for value in values if value["date_changed"] is changed]


def audits(env, action):
    with env.app.app_context():
        return [(row.id, json.loads(row.details_json)) for row in get_db().scalars(
            select(AuditEvent).where(AuditEvent.action == action).order_by(AuditEvent.id))]


def current_due(env, target):
    with guarded(env) as db:
        return read_own_deadline_fact(db, target).due_date.isoformat()


def start_task(env, target):
    body = version_body(env, target)
    request_key = key(env, "task.accepted", target, "staff1")
    with guarded(env) as db:
        starting.execute_start(db, target=str(target), action=TaskStartAction.ACCEPT,
            body=body, key=request_key)


def request_extension(env, target):
    body = version_body(env, target) | dict(reason="TEST extension",
        requested_due_date=(datetime.fromisoformat(env.body["due_date"]).date() + timedelta(days=3)).isoformat())
    result = invoke(env, "task.deadline_extension_requested", target, body,
        key(env, "task.deadline_extension_requested", target, "staff1"), actor="staff1")
    return result.data["extension_id"], body["requested_due_date"]


def test_creation_audit_has_no_initial_due_evidence_and_replay_observes_once(writes):
    env = writes
    body, request_key = dict(env.body), key(env, "task.created")
    first = invoke(env, "task.created", "new", body, request_key)
    before = persisted(env)
    again = invoke(env, "task.created", "new", body, request_key)
    assert again == first and persisted(env) == before
    assert env.calls == [("task.created", int(first.data["task_id"]))]
    assert len(probes(env)) == 1 and probes(env, changed=True) == []
    assert "due_date" not in audits(env, "task.created")[0][1]
    assert current_due(env, first.data["task_id"]) == body["due_date"]


def test_actual_general_update_a_to_b_to_a_has_two_distinct_original_changes(writes):
    env = writes
    target = create_task(env)
    original_due = env.body["due_date"]
    later_due = (datetime.fromisoformat(original_due).date() + timedelta(days=2)).isoformat()
    for due in (later_due, original_due):
        invoke(env, "task.updated", target, edit_body(env, target, due_date=due), key(env, "task.updated", target))
    changed = probes(env, changed=True)
    assert [(p["before_due_date"], p["due_date"]) for p in changed] == [(original_due, later_due), (later_due, original_due)]
    assert [p["stored_row_version"] for p in changed] == ["2", "3"]
    originals = audits(env, "task.updated")
    assert len(originals) == 2 and originals[0][0] != originals[1][0]
    assert all(p["assignment_revision"] == 1 for p in changed)
    assert current_due(env, target) == original_due


@pytest.mark.parametrize("field", ["title", "description"])
def test_same_due_title_or_body_update_is_not_a_deadline_change(writes, field):
    env = writes
    target = create_task(env)
    invoke(env, "task.updated", target, edit_body(env, target, **{field: "TEST changed text"}), key(env, "task.updated", target))
    assert audits(env, "task.updated")[0][1]["changed_fields"] == [field]
    assert probes(env, changed=True) == [] and current_due(env, target) == env.body["due_date"]


def test_identical_save_has_no_mutation_audit_probe_or_receipt(writes):
    env = writes
    target = create_task(env)
    request_key, body = key(env, "task.updated", target), edit_body(env, target)
    before, calls = persisted(env), list(env.calls)
    with pytest.raises(AppError) as caught:
        invoke(env, "task.updated", target, body, request_key)
    assert (caught.value.status, caught.value.code) == (409, "TASK_CHANGE_NOT_NEEDED")
    assert persisted(env) == before and env.calls == calls


def test_attachment_only_actual_management_boundary_uses_explicit_provider_double(writes, monkeypatch):
    env = writes
    target = create_task(env)
    old, new = "11111111-1111-4111-8111-111111111111", "22222222-2222-4222-8222-222222222222"
    observed = []
    def read(db, task_id, *, expected):
        assert db is env.active_db and g.write_transaction_active
        assert expected.row_version == 1 and expected.assignment_revision == 1
        return [old]
    def replace(db, task_id, ids, *, expected):
        assert db is env.active_db and g.write_transaction_active
        observed.append((task_id, list(ids), expected.row_version))
    monkeypatch.setitem(env.app.extensions, "results_shared_attachments", SimpleNamespace(
        read_task_attachment_ids=read, replace_task_files=replace))
    invoke(env, "task.updated", target, edit_body(env, target, attachment_ids=[new]), key(env, "task.updated", target))
    assert observed == [(int(target), [new], 1)]
    assert audits(env, "task.updated")[0][1]["changed_fields"] == ["attachment_ids"]
    assert probes(env, changed=True) == [] and current_due(env, target) == env.body["due_date"]


@pytest.mark.parametrize("close", ["rejected", "withdrawn"])
def test_request_and_nonapproval_leave_effective_due_unchanged(writes, close):
    env = writes
    target = create_task(env)
    start_task(env, target)
    extension_id, _ = request_extension(env, target)
    assert current_due(env, target) == env.body["due_date"] and probes(env, changed=True) == []
    actor = "admin1" if close == "rejected" else "staff1"
    action = "task.deadline_extension_" + close
    body = version_body(env, target) | ({"reason": "TEST rejected"} if close == "rejected" else {})
    invoke(env, action, target, body, key(env, action, target, actor), actor=actor, extension_id=extension_id)
    assert probes(env, changed=True) == [] and current_due(env, target) == env.body["due_date"]
    with env.app.app_context():
        assert get_db().get(TaskDeadlineExtension, int(extension_id)).status == close


def test_only_approval_changes_due_and_same_w1_replay_does_not_repeat_probe(writes):
    env = writes
    target = create_task(env)
    start_task(env, target)
    extension_id, due = request_extension(env, target)
    action, body = "task.deadline_extension_approved", version_body(env, target)
    request_key = key(env, action, target)
    result = invoke(env, action, target, body, request_key, extension_id=extension_id)
    before, calls = persisted(env), list(env.calls)
    assert invoke(env, action, target, body, request_key, extension_id=extension_id) == result
    assert persisted(env) == before and env.calls == calls
    changed, = probes(env, changed=True)
    assert (changed["original_action"], changed["before_due_date"], changed["due_date"]) == (action, env.body["due_date"], due)
    assert current_due(env, target) == due
    with env.app.app_context():
        row = get_db().get(TaskDeadlineExtension, int(extension_id))
        assert row.status == "approved" and row.decided_at is not None


def test_general_update_same_w1_replay_reenters_no_mutation_or_probe(writes):
    env = writes
    target = create_task(env)
    later = (datetime.fromisoformat(env.body["due_date"]).date() + timedelta(days=1)).isoformat()
    body, request_key = edit_body(env, target, due_date=later), key(env, "task.updated", target)
    result = invoke(env, "task.updated", target, body, request_key)
    before, calls = persisted(env), list(env.calls)
    assert invoke(env, "task.updated", target, body, request_key) == result
    assert persisted(env) == before and env.calls == calls
    assert len(probes(env, changed=True)) == 1


@pytest.mark.parametrize("writer", ["creation", "edit", "approval"])
@pytest.mark.parametrize("failure", ["callback", "receipt"])
def test_original_writer_audit_probe_and_receipt_roll_back_together(writes, writer, failure):
    env = writes
    extension_id = None
    if writer == "creation":
        action, target, body = "task.created", "new", dict(env.body)
    else:
        target = create_task(env)
        if writer == "edit":
            action = "task.updated"
            body = edit_body(env, target, due_date=(datetime.fromisoformat(env.body["due_date"]).date() + timedelta(days=2)).isoformat())
        else:
            start_task(env, target)
            extension_id, _ = request_extension(env, target)
            action, body = "task.deadline_extension_approved", version_body(env, target)
    request_key = key(env, action, target)
    before, calls = persisted(env), len(env.calls)
    env.fail_action, env.fail_stage = action, failure
    with pytest.raises(AppError) as caught:
        invoke(env, action, target, body, request_key, extension_id=extension_id)
    assert caught.value.status == 503
    assert caught.value.code == ("TEST_DEADLINE_PROBE_FAILED" if failure == "callback" else "TEST_DEADLINE_RECEIPT_FAILED")
    assert len(env.calls) == calls + 1, "the actual mutation reached its original-TX probe"
    assert persisted(env) == before, "domain, original audit, flushed probe and receipt must all roll back"
    env.fail_action = env.fail_stage = None
    result = invoke(env, action, target, body, request_key, extension_id=extension_id)
    committed, calls = persisted(env), list(env.calls)
    assert invoke(env, action, target, body, request_key, extension_id=extension_id) == result
    assert persisted(env) == committed and env.calls == calls


def test_current_fact_reader_waits_for_same_guard_then_observes_atomic_new_due(writes):
    env = writes
    target = create_task(env)
    headers(env)  # No login or account mutation in either paused transaction.
    later = (datetime.fromisoformat(env.body["due_date"]).date() + timedelta(days=2)).isoformat()
    body, request_key = edit_body(env, target, due_date=later), key(env, "task.updated", target)
    entered, release, reading_guard, past_guard = Event(), Event(), Event(), Event()
    reader_threads = set()
    def gate():
        entered.set()
        assert release.wait(10), "owned boundary gate timed out"
    env.probe_gate = gate
    engine = env.app.extensions["db_engine"]
    def observe_guard(_conn, _cursor, statement, _params, context, _many):
        if "admin_guard" in statement.lower() and get_ident() in reader_threads:
            reading_guard.set()
    # The reader thread marker only observes its real AdminGuard SELECT.
    # It does not replace SQL, authorization, isolation or lock behavior.
    def read():
        from test_task_deadline_facts import identity
        from remotework.auth import authenticated_transaction
        reader_threads.add(get_ident())
        with identity(env):
            with authenticated_transaction() as db:
                past_guard.set()
                return read_own_deadline_fact(db, target).due_date.isoformat()
    event.listen(engine, "before_cursor_execute", observe_guard)
    try:
        with ThreadPoolExecutor(max_workers=2) as pool:
            writing = pool.submit(invoke, env, "task.updated", target, body, request_key)
            try:
                assert entered.wait(10)
                reading = pool.submit(read)
                assert reading_guard.wait(10), "reader must attempt the actual guarded SQL"
                assert not past_guard.wait(.2), "reader must remain before the owned guard until release"
                assert not reading.done(), "reader cannot finish while writer owns the guard"
            finally:
                release.set()
            writing.result(timeout=15)
            assert reading.result(timeout=15) == later
        assert len(probes(env, changed=True)) == 1
    finally:
        release.set()
        env.probe_gate = None
        event.remove(engine, "before_cursor_execute", observe_guard)
