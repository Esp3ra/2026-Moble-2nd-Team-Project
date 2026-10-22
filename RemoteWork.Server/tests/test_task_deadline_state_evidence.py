"""Owner2 evidence from actual owner2/3 writes, on an owned raw-DDL MySQL.

No deadline event identity/store, baseline, confirmation policy or product
reader is added. Real registered HTTP handlers create every task, assignment,
submission and decision. Only accounts/reviewer mappings and a testing-app ACK
are fixture data. No server process, shared environment or file IO is involved.
"""
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
import json
import os
from types import SimpleNamespace

import pytest
from sqlalchemy import delete, inspect, select
from sqlalchemy.engine import make_url

from remotework import create_app
from remotework.auth import authenticated_transaction, create_user
from remotework.common import AppError
from remotework.db import get_db, transaction
from remotework.features import results
from remotework.features.tasks import continuity, creation, lifecycle, management, starting
from remotework.features.tasks.deadline_facts import read_own_deadline_fact, read_own_deadline_facts
from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
from remotework.features.tasks.lifecycle_support import INTEGRATED_ACK, pending_extension_facts
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AdminGuard, AuditEvent, Base, SchemaVersion
from remotework.write_requests import WriteReceipt


PASSWORD = "State-evidence-synthetic-2026!"
NOW = datetime(2026, 10, 22, 3, 4, 5)
TODAY = NOW.replace(tzinfo=timezone.utc).astimezone(timezone(timedelta(hours=9))).date()
DUE = TODAY + timedelta(days=14)


@pytest.fixture(scope="module")
def state_app():
    url = os.getenv("MYSQL_TEST_URL", "")
    if not url.startswith("mysql") or os.getenv("REMOTEWORK_RUN_DEADLINE_EVIDENCE") != "1":
        pytest.skip("Use the explicit owned MySQL deadline-evidence runner")
    if not (make_url(url).database or "").startswith("remote_work_test_"):
        raise RuntimeError("Refusing non-disposable database")
    config = dict(TESTING=True, DATABASE_URL=url, SECRET_KEY="synthetic-state-evidence-" + "a" * 32,
                  COOKIE_SECURE=False, RESULTS_CONTEXT_MODE="local-development")
    for feature in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_START", "TASK_MANAGEMENT",
                    "TASK_LIFECYCLE", "RESULTS"):
        config["ENABLE_" + feature + "_DRAFT"] = True
    for feature in ("TASK_LIFECYCLE_RESULT_READS", "SHARED_ATTACHMENTS", "NOTICE", "NOTICE_POPUP",
                    "CHAT", "COLLABORATION", "COLLABORATION_RESULT_READS", "DEVICE_ENROLLMENT",
                    "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY", "DEVICE_IDENTITY"):
        config["ENABLE_" + feature + "_DRAFT"] = False
    app = create_app(config)
    engine = app.extensions["db_engine"]
    assert engine.dialect.name == "mysql"
    tables = set(inspect(engine).get_table_names())
    assert {"tasks", "task_assignments", "task_managers", "write_receipts", "result_submissions",
            "result_decisions", "result_events", "task_cancellations", "task_deadline_extensions"} <= tables
    assert not any(name.startswith("result_shared_") for name in tables)
    assert "results" in app.blueprints and app.extensions["results_facts"] is results.local_facts
    assert "task_lifecycle_integration_ack" not in app.extensions
    # Scoped to this TESTING app with actual owner3 guards. Never deployment ACK.
    app.extensions["task_lifecycle_integration_ack"] = dict(INTEGRATED_ACK)
    with app.app_context(), transaction() as db:
        db.add_all([AdminGuard(id=1), SchemaVersion(version=1)])
    try:
        yield app
    finally:
        app.extensions.pop("task_lifecycle_integration_ack", None)
        engine.dispose()


@pytest.fixture
def evidence(state_app, monkeypatch):
    # Stable business dates, not a fake initial history or deadline event clock.
    for module in (creation, management, starting, lifecycle, results):
        monkeypatch.setattr(module, "utcnow", lambda: NOW)
    with state_app.app_context(), transaction() as db:
        for table in reversed(Base.metadata.sorted_tables):
            if table.name not in {"admin_guard", "schema_versions"}:
                db.execute(delete(table))
        users = {name: create_user(db, dict(login_id=name, password=PASSWORD,
                    display_name="TEST-" + name, employee_no="EVIDENCE-" + name,
                    department="TEST", role=role))
                 for name, role in (("manager", "admin"), ("worker", "employee"), ("other", "employee"))}
        ids = {name: user.id for name, user in users.items()}
        db.add(results.DepartmentReviewer(department="TEST", reviewer_id=ids["manager"]))
    return SimpleNamespace(app=state_app, ids=ids, headers={})


def headers(env, actor="worker"):
    if actor not in env.headers:
        response = env.app.test_client().post("/api/v1/auth/login", json={"login_id": actor, "password": PASSWORD})
        assert response.status_code == 200
        env.headers[actor] = {"Authorization": "Bearer " + response.json["data"]["access_token"]}
    return env.headers[actor]


@contextmanager
def guarded(env, actor="worker"):
    with env.app.test_request_context("/api/v1/tasks", headers=headers(env, actor)):
        assert env.app.preprocess_request() is None
        with authenticated_transaction() as db:
            yield db


def fact(env, task_id, actor="worker"):
    with guarded(env, actor) as db:
        return read_own_deadline_fact(db, task_id)


def versions(env, task_id, actor="worker"):
    current = fact(env, task_id, actor)
    return dict(expected_row_version=str(current.row_version), expected_assignment_revision=current.assignment_revision)


def write(env, action, target, path, body, *, actor="manager", method="POST", expected=200):
    client = env.app.test_client()
    auth = headers(env, actor)
    issued = client.post("/api/v1/write-requests/keys", headers=auth,
                         json=dict(action=action, target=str(target)))
    assert issued.status_code == 201, issued.json.get("error")
    response = client.open("/api/v1/" + path, method=method,
        headers=auth | {"Idempotency-Key": issued.json["data"]["request_key"]}, json=body)
    assert response.status_code == expected, response.json.get("error")
    return response.json["data"] if expected < 400 else response.json["error"]["code"]


def create(env):
    data = write(env, "task.created", "new", "tasks",
        dict(title="TEST state evidence", description="synthetic owner2/3 flow",
             start_date=TODAY.isoformat(), due_date=DUE.isoformat(), assignee_id=env.ids["worker"],
             manager_user_ids=[env.ids["manager"]]), expected=201)
    return data["task_id"]


def start(env, task_id, rework=False):
    return write(env, "task.rework_started" if rework else "task.accepted", task_id,
                 f"tasks/{task_id}/" + ("rework-start" if rework else "accept"),
                 versions(env, task_id), actor="worker")


def submit(env, task_id, expected=201):
    return write(env, "submission.submitted", task_id, f"results/tasks/{task_id}/submissions",
        versions(env, task_id) | dict(description="TEST actual result", attachment_ids=[]), actor="worker", expected=expected)


def decide(env, task_id, submission_id, outcome):
    return write(env, "submission." + outcome, submission_id, f"results/submissions/{submission_id}/decisions",
                 versions(env, task_id) | dict(outcome=outcome, reason="TEST revise" if outcome == "returned" else ""))


def extend(env, task_id):
    return write(env, "task.deadline_extension_requested", task_id, f"tasks/{task_id}/deadline-extensions",
        versions(env, task_id) | dict(reason="TEST extension", requested_due_date=(DUE + timedelta(days=7)).isoformat()), actor="worker")


def snapshot(env):
    tables = [TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__, TaskCancellation.__table__,
        TaskDeadlineExtension.__table__, results.Submission.__table__, results.Decision.__table__,
        results.ResultEvent.__table__, AuditEvent.__table__, WriteReceipt.__table__]
    with env.app.extensions["db_engine"].connect() as db:
        return tuple((table.name, tuple(tuple(row) for row in db.execute(select(table).order_by(*table.primary_key.columns)))) for table in tables)


def audits(env, task_id):
    with env.app.app_context():
        rows = list(get_db().scalars(select(AuditEvent).order_by(AuditEvent.id)))
        return [(row.action, row.resource_type, row.resource_id, json.loads(row.details_json)) for row in rows
                if (row.resource_type == "task" and row.resource_id == str(task_id))
                or (row.resource_type == "submission" and json.loads(row.details_json).get("task_id") == str(task_id))]


def result_evidence(env, task_id):
    with env.app.app_context():
        db = get_db()
        events = [(e.operation_id, e.assignment_revision, e.submission_id, e.kind) for e in db.scalars(
            select(results.ResultEvent).where(results.ResultEvent.task_id == int(task_id)).order_by(results.ResultEvent.id))]
        submissions = [(s.id, s.assignment_revision, s.round_no, s.status) for s in db.scalars(
            select(results.Submission).where(results.Submission.task_id == int(task_id)).order_by(results.Submission.round_no))]
        return events, submissions


def test_actual_submit_return_rework_resubmit_approve_preserves_due_and_exposes_intermediate_states(evidence):
    env = evidence
    target = create(env)
    observed = [fact(env, target)]
    start(env, target); observed.append(fact(env, target))
    first = submit(env, target); observed.append(fact(env, target))
    decide(env, target, first["submission_id"], "returned"); observed.append(fact(env, target))
    start(env, target, True); observed.append(fact(env, target))
    second = submit(env, target); observed.append(fact(env, target))
    decide(env, target, second["submission_id"], "approved"); observed.append(fact(env, target))
    assert [row.task_status for row in observed] == ["not_started", "in_progress", "submitted", "returned", "in_progress", "submitted", "approved"]
    assert [row.row_version for row in observed] == list(range(1, 8))
    assert all(row.assignment_revision == 1 and row.assignee_id == env.ids["worker"]
               and row.due_date == DUE and not row.is_cancelled for row in observed)
    events, submissions = result_evidence(env, target)
    assert [e[3] for e in events] == ["submitted", "returned", "submitted", "approved"]
    assert len({e[0] for e in events}) == 4 and all(e[1] == 1 for e in events)
    assert [(s[1], s[2], s[3]) for s in submissions] == [(1, 1, "returned"), (1, 2, "approved")]
    original = audits(env, target)
    assert [row[0] for row in original] == ["task.created", "task.accepted", "submission.submitted",
        "submission.returned", "task.rework_started", "submission.submitted", "submission.approved"]
    assert [int(row[3]["row_version"]) for row in original if row[1] == "submission"] == [3, 4, 6, 7]
    assert "due_date" not in original[0][3]  # Real initial audit cannot reconstruct first due date.
    with env.app.app_context():
        assert get_db().get(TaskAssignment, (int(target), 1)).accepted_at == NOW
    before = snapshot(env)
    assert fact(env, target) == observed[-1]
    assert snapshot(env) == before  # Reading adds no synthetic event/history.


def test_actual_reassignment_a_b_a_keeps_distinct_assignment_generations(evidence):
    env = evidence
    target = create(env)
    original = fact(env, target)
    write(env, "task.reassigned", target, f"tasks/{target}/reassign",
          versions(env, target) | dict(assignee_id=env.ids["other"]))
    changed = fact(env, target, "other")
    assert (changed.assignment_revision, changed.row_version, changed.due_date) == (2, 2, DUE)
    with guarded(env) as db:
        assert read_own_deadline_facts(db) == ()
        with pytest.raises(AppError) as caught:
            read_own_deadline_fact(db, target)
        assert (caught.value.status, caught.value.code) == (403, "FORBIDDEN")
    write(env, "task.reassigned", target, f"tasks/{target}/reassign",
          versions(env, target, "other") | dict(assignee_id=env.ids["worker"]))
    restored = fact(env, target)
    assert restored.assignee_id == original.assignee_id and restored.due_date == original.due_date
    assert (restored.assignment_revision, restored.row_version) == (3, 3)
    with guarded(env, "other") as db:
        assert read_own_deadline_facts(db) == ()
    with env.app.app_context():
        assigned = [(a.revision, a.assignee_id, a.accepted_at) for a in get_db().scalars(
            select(TaskAssignment).where(TaskAssignment.task_id == int(target)).order_by(TaskAssignment.revision))]
    assert assigned == [(1, env.ids["worker"], None), (2, env.ids["other"], None), (3, env.ids["worker"], None)]
    changes = [row[3] for row in audits(env, target) if row[0] == "task.reassigned"]
    assert [(x["before_assignee_id"], x["assignee_id"], x["after"]["assignment_revision"]) for x in changes] == [
        (env.ids["worker"], env.ids["other"], 2), (env.ids["other"], env.ids["worker"], 3)]
    assert all("before_due_date" not in x and "due_date" not in x for x in changes)


def test_actual_edit_then_extension_then_submission_has_original_version_order(evidence):
    env = evidence
    target = create(env)
    changed_due = DUE + timedelta(days=2)
    write(env, "task.updated", target, f"tasks/{target}", versions(env, target) | dict(
        title="TEST state evidence", description="synthetic owner2/3 flow", start_date=TODAY.isoformat(),
        due_date=changed_due.isoformat()), method="PATCH")
    after_edit = fact(env, target)
    start(env, target)
    pending = extend(env, target)
    after_request = fact(env, target)
    denied = env.app.test_client().get(f"/api/v1/results/tasks/{target}/context", headers=headers(env))
    assert denied.status_code == 409 and denied.json["error"]["code"] == "DEADLINE_EXTENSION_PENDING"
    with guarded(env) as db:
        assert pending_extension_facts(db, int(target), 1, lock=True) is True
    approved = write(env, "task.deadline_extension_approved", target,
        f"tasks/{target}/deadline-extensions/{pending['extension_id']}/approve", versions(env, target))
    after_approval = fact(env, target)
    submitted = submit(env, target)
    final = fact(env, target)
    assert (after_edit.row_version, after_request.row_version, after_approval.row_version, final.row_version) == (2, 4, 5, 6)
    assert after_edit.due_date == after_request.due_date == changed_due
    assert after_approval.due_date == final.due_date == DUE + timedelta(days=7)
    assert final.task_status == "submitted" and submitted["task_status"] == "submitted"
    originals = audits(env, target)
    edit = next(x[3] for x in originals if x[0] == "task.updated")
    approval = next(x[3] for x in originals if x[0] == "task.deadline_extension_approved")
    assert (edit["before_due_date"], edit["due_date"], edit["after"]["row_version"]) == (DUE.isoformat(), changed_due.isoformat(), "2")
    assert (approval["before"]["due_date"], approval["after"]["due_date"], approval["after"]["row_version"]) == (
        changed_due.isoformat(), approved["due_date"], "5")
    assert [x[0] for x in originals] == ["task.created", "task.updated", "task.accepted",
        "task.deadline_extension_requested", "task.deadline_extension_approved", "submission.submitted"]
    before = snapshot(env)
    rejected = write(env, "task.updated", target, f"tasks/{target}", versions(env, target) | dict(
        title="TEST after submission", description="synthetic", start_date=TODAY.isoformat(), due_date=DUE.isoformat()),
        method="PATCH", expected=409)
    assert rejected == "TASK_STATE_CONFLICT" and snapshot(env) == before


@pytest.mark.parametrize("state", ("in_progress", "returned"))
@pytest.mark.parametrize("with_pending", (False, True))
def test_actual_cancel_retains_state_due_and_result_history_but_closes_pending(evidence, state, with_pending):
    env = evidence
    target = create(env); start(env, target)
    if state == "returned":
        submitted = submit(env, target)
        decide(env, target, submitted["submission_id"], "returned")
    pending = extend(env, target) if with_pending else None
    before = fact(env, target)
    preserved_results = result_evidence(env, target)
    write(env, "task.cancelled", target, f"tasks/{target}/cancel", versions(env, target) | dict(reason="TEST cancel"))
    cancelled = fact(env, target)
    assert cancelled.is_cancelled and cancelled.task_status == state
    assert cancelled.row_version == before.row_version + 1
    assert (cancelled.due_date, cancelled.assignment_revision, cancelled.assignee_id) == (
        before.due_date, before.assignment_revision, before.assignee_id)
    assert result_evidence(env, target) == preserved_results
    with guarded(env) as db:
        assert read_own_deadline_facts(db) == (cancelled,)
        assert pending_extension_facts(db, int(target), 1, lock=True) is False
    if pending:
        with env.app.app_context():
            assert get_db().get(TaskDeadlineExtension, int(pending["extension_id"])).status == "cancelled"
    before_rejection = snapshot(env)
    denied = env.app.test_client().get(f"/api/v1/results/tasks/{target}/context", headers=headers(env))
    assert denied.status_code == 409 and denied.json["error"]["code"] == "TASK_CANCELLED"
    assert submit(env, target, expected=409) == "TASK_CANCELLED"
    assert snapshot(env) == before_rejection
    latest = audits(env, target)[-1]
    assert latest[0] == "task.cancelled" and latest[3]["before"]["task_status"] == latest[3]["after"]["task_status"] == state


@pytest.mark.parametrize("state", ("submitted", "approved"))
def test_real_submission_and_approval_remain_visible_to_facts_while_cancellation_is_denied(evidence, state):
    env = evidence
    target = create(env); start(env, target)
    submitted = submit(env, target)
    if state == "approved":
        decide(env, target, submitted["submission_id"], "approved")
    before, current = snapshot(env), fact(env, target)
    denied = write(env, "task.cancelled", target, f"tasks/{target}/cancel",
                   versions(env, target) | dict(reason="TEST forbidden state"), expected=409)
    assert denied == "TASK_STATE_CONFLICT" and snapshot(env) == before
    assert fact(env, target) == current and current.task_status == state and not current.is_cancelled


def test_same_assignment_return_to_progress_and_missing_audit_do_not_prove_continuity(evidence):
    env = evidence
    target = create(env); start(env, target)
    initial = fact(env, target)
    submitted = submit(env, target)
    decide(env, target, submitted["submission_id"], "returned")
    start(env, target, True)
    current = fact(env, target)
    assert (initial.task_status, initial.assignee_id, initial.assignment_revision, initial.due_date) == (
        current.task_status, current.assignee_id, current.assignment_revision, current.due_date)
    assert initial.row_version == 2 and current.row_version == 5
    original_results = result_evidence(env, target)
    with guarded(env) as db:
        # The existing owner2 continuity reader deliberately does not consume
        # owner3 ResultEvent/submission audits as a complete task version chain.
        assert continuity.continuous_in_progress(db, int(target), 2, 5) is None
    with env.app.app_context(), transaction() as db:
        rows = list(db.scalars(select(AuditEvent).where(AuditEvent.resource_type == "submission",
            AuditEvent.resource_id == submitted["submission_id"])))
        assert len(rows) == 2
        # Explicit retention-loss test only; no product cleanup/backfill exists.
        for row in rows:
            db.delete(row)
    assert fact(env, target) == current and result_evidence(env, target) == original_results
    with guarded(env) as db:
        assert continuity.continuous_in_progress(db, int(target), 2, 5) is None
    assert not any(row[1] == "submission" for row in audits(env, target))
