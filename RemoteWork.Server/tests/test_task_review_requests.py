"""Manual review request tests against fresh owned MySQL and real results APIs."""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import json
import os
from threading import Barrier, Thread
from types import SimpleNamespace
from urllib.error import HTTPError
from urllib.request import Request, urlopen

import pytest
from sqlalchemy import delete, inspect, select, text
from sqlalchemy.engine import make_url
from sqlalchemy.orm import Session

from remotework import create_app
from remotework.auth import create_user
from remotework.common import AppError
from remotework.db import transaction
from remotework.features import results
from remotework.features.tasks import review_requests as feature
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.features.tasks.review_request_models import TaskReviewRequest
from remotework.models import AdminGuard, AuditEvent, Base, SchemaVersion
from remotework.write_requests import WriteReceipt

PASSWORD = "Owned-review-request-2026!"
pytestmark = pytest.mark.skipif(os.getenv("REMOTEWORK_RUN_TASK_REVIEW_REQUESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Requires explicit owned MySQL review-request runner; no SQLite fallback")


@pytest.fixture
def review_env(tmp_path):
    url = os.environ["MYSQL_TEST_URL"]
    if not (make_url(url).database or "").startswith("remote_work_test_"):
        raise RuntimeError("Refusing non-disposable database")
    config = dict(TESTING=True, DATABASE_URL=url, SECRET_KEY="review-owned-" + "x" * 40,
        COOKIE_SECURE=False, RESULTS_CONTEXT_MODE="local-development", RESULTS_STORAGE_ROOT=str(tmp_path / "files"))
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_START", "RESULTS"):
        config["ENABLE_" + name + "_DRAFT"] = True
    for name in ("TASK_MANAGEMENT", "TASK_LIFECYCLE", "TASK_LIFECYCLE_RESULT_READS", "SHARED_ATTACHMENTS",
                 "NOTICE", "NOTICE_POPUP", "CHAT", "COLLABORATION", "COLLABORATION_RESULT_READS",
                 "DEVICE_ENROLLMENT", "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY", "DEVICE_IDENTITY"):
        config["ENABLE_" + name + "_DRAFT"] = False
    app = create_app(config)
    engine = app.extensions["db_engine"]
    assert engine.dialect.name == "mysql"
    with app.app_context(), transaction() as db:
        existing = set(inspect(db.connection()).get_table_names())
        db.execute(delete(TaskReviewRequest))
        for table in reversed(Base.metadata.sorted_tables):
            if table.name in existing and table.name not in {"admin_guard", "schema_versions"}:
                db.execute(delete(table))
        if db.get(AdminGuard, 1) is None:
            db.add(AdminGuard(id=1))
        if db.get(SchemaVersion, 1) is None:
            db.add(SchemaVersion(version=1))
        users = {name: create_user(db, dict(login_id=name, password=PASSWORD, display_name="TEST-" + name,
            employee_no="REVIEW-" + name, department="TEST", role=role)) for name, role in
            (("manager", "admin"), ("reviewer", "admin"), ("replacement", "admin"),
             ("worker", "employee"), ("other", "employee"))}
        ids = {name: user.id for name, user in users.items()}
        db.add(results.DepartmentReviewer(department="TEST", reviewer_id=ids["reviewer"]))
        db.add(results.UpperReviewer(submitter_id=ids["manager"], reviewer_id=ids["reviewer"]))
    env = SimpleNamespace(app=app, ids=ids, headers={},
        today=datetime.now(timezone.utc).astimezone(timezone(timedelta(hours=9))).date())
    try:
        yield env
    finally:
        engine.dispose()


def auth(env, actor="worker"):
    if actor not in env.headers:
        response = env.app.test_client().post("/api/v1/auth/login", json={"login_id": actor, "password": PASSWORD})
        assert response.status_code == 200, response.json
        env.headers[actor] = {"Authorization": "Bearer " + response.json["data"]["access_token"]}
    return env.headers[actor]


def key(env, action, target, actor="worker"):
    response = env.app.test_client().post("/api/v1/write-requests/keys", headers=auth(env, actor),
        json={"action": action, "target": target})
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def send(env, action, target, path, body, *, actor="worker", key_value=None, expected=201):
    value = key_value or key(env, action, target, actor)
    response = env.app.test_client().post("/api/v1/" + path,
        headers=auth(env, actor) | {"Idempotency-Key": value}, json=body)
    assert response.status_code == expected, response.json
    return response.json["data"] if expected < 400 else response.json["error"]["code"]


def get(env, path, actor="worker", expected=200):
    response = env.app.test_client().get("/api/v1/" + path, headers=auth(env, actor))
    assert response.status_code == expected, response.json
    return response.json["data"] if expected < 400 else response.json["error"]["code"]


def versions(env, target):
    value = get(env, f"tasks/{target}/write-context")["context"]
    return {"expected_row_version": value["row_version"], "expected_assignment_revision": value["assignment_revision"]}


def create_submitted(env, actor="worker", *, stop=None):
    created = send(env, "task.created", "new", "tasks", dict(title="Manual review target", description="Task original",
        start_date=env.today.isoformat(), due_date=(env.today + timedelta(days=4)).isoformat(),
        assignee_id=env.ids[actor], manager_user_ids=[env.ids["manager"]]), actor="manager")
    target = created["task_id"]
    if stop == "not_started":
        return target, None
    context = get(env, f"tasks/{target}/write-context", actor)["context"]
    body = {"expected_row_version": context["row_version"], "expected_assignment_revision": context["assignment_revision"]}
    send(env, "task.accepted", target, f"tasks/{target}/accept", body, actor=actor, expected=200)
    if stop == "in_progress":
        return target, None
    context = get(env, f"results/tasks/{target}/context", actor)
    body = {"expected_row_version": context["row_version"], "expected_assignment_revision": context["assignment_revision"],
            "description": "Submitted original result", "attachment_ids": []}
    submitted = send(env, "submission.submitted", target, f"results/tasks/{target}/submissions", body, actor=actor)
    return target, submitted["submission_id"]


def request_body(env, target, actor="worker"):
    context = get(env, f"tasks/{target}/review-request-context", actor)["context"]
    return dict(expected_row_version=context["row_version"], expected_assignment_revision=context["assignment_revision"],
                submission_id=context["submission_id"], reason="이 결과의 계산 근거를 검토해 주세요.")


def source_snapshot(env):
    with env.app.extensions["db_engine"].connect() as db:
        return tuple((table.name, tuple(tuple(row) for row in db.execute(select(table).order_by(*table.primary_key.columns))))
            for table in (TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__,
                          results.Submission.__table__, results.Decision.__table__, results.ResultEvent.__table__))


def own_counts(env):
    with env.app.extensions["db_engine"].connect() as db:
        return tuple(len(list(db.execute(select(table)))) for table in
                     (TaskReviewRequest.__table__, WriteReceipt.__table__, AuditEvent.__table__))


def decide(env, target, submission, outcome="approved"):
    context = get(env, f"results/submissions/{submission}/context", "reviewer")
    return send(env, "submission." + outcome, submission, f"results/submissions/{submission}/decisions",
        dict(expected_row_version=context["row_version"], expected_assignment_revision=context["assignment_revision"],
             outcome=outcome, reason="Actual reviewer decision"), actor="reviewer", expected=200)


def test_request_acknowledge_replay_and_derived_resolution_preserve_originals(review_env):
    env = review_env
    target, submission = create_submitted(env)
    body = request_body(env, target)
    original = source_snapshot(env)
    request_key = key(env, feature.REQUEST_ACTION, target)
    saved = send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, key_value=request_key)
    assert set(saved) == feature.RESULT_FIELDS and saved["request_state"] == "pending"
    assert source_snapshot(env) == original
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, key_value=request_key) == saved
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, expected=409) == "REVIEW_REQUEST_PENDING"
    item = get(env, "tasks/review-requests?scope=reviewing", "reviewer")["items"][0]
    assert item["reason"] == body["reason"] and item["reviewer_id"] == env.ids["reviewer"]
    assert get(env, "tasks/review-requests?scope=reviewing", "manager")["items"] == []
    ack_body = {"expected_row_version": item["row_version"], "expected_assignment_revision": item["assignment_revision"]}
    ack_key = key(env, feature.ACK_ACTION, target, "reviewer")
    path = f"tasks/{target}/review-requests/{saved['request_id']}/acknowledge"
    acknowledged = send(env, feature.ACK_ACTION, target, path, ack_body, actor="reviewer", key_value=ack_key, expected=200)
    assert acknowledged["request_state"] == "acknowledged" and acknowledged["acknowledged_at"]
    assert source_snapshot(env) == original
    decide(env, target, submission)
    changed = source_snapshot(env)
    assert get(env, "tasks/review-requests?scope=own")["items"][0]["state"] == "resolved"
    assert get(env, f"tasks/{target}/review-request-context", expected=409) == "TASK_REVIEW_REQUEST_NOT_AVAILABLE"
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, key_value=request_key) == saved
    assert send(env, feature.ACK_ACTION, target, path, ack_body, actor="reviewer", key_value=ack_key, expected=200) == acknowledged
    restored = env.app.test_client().post("/api/v1/write-requests/result", headers=auth(env) | {"Idempotency-Key": request_key},
        json={"action": feature.REQUEST_ACTION, "target": target})
    assert restored.status_code == 200 and restored.json["data"]["result"] == saved
    assert source_snapshot(env) == changed


@pytest.mark.parametrize("actor", ["other", "manager", "reviewer"])
def test_non_owner_cannot_read_context_or_create_request(review_env, actor):
    env = review_env
    target, _ = create_submitted(env)
    assert get(env, f"tasks/{target}/review-request-context", actor, expected=403) == "FORBIDDEN"
    response = env.app.test_client().post("/api/v1/write-requests/keys", headers=auth(env, actor),
        json={"action": feature.REQUEST_ACTION, "target": target})
    assert response.status_code == 403


@pytest.mark.parametrize("state", ["not_started", "in_progress"])
def test_unsubmitted_tasks_never_create_review_requests(review_env, state):
    env = review_env
    target, _ = create_submitted(env, stop=state)
    before = source_snapshot(env)
    assert get(env, f"tasks/{target}/review-request-context", expected=409) == "TASK_REVIEW_REQUEST_NOT_AVAILABLE"
    body = versions(env, target) | {"submission_id": "1", "reason": "test"}
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, expected=409) == "TASK_REVIEW_REQUEST_NOT_AVAILABLE"
    assert source_snapshot(env) == before


@pytest.mark.parametrize("changes", [{"reason": " "}, {"reason": "x" * 4001}, {"reason": None},
    {"submission_id": 1}, {"expected_row_version": 3}, {"extra": True}])
def test_invalid_input_creates_no_request_receipt_or_audit(review_env, changes):
    env = review_env
    target, _ = create_submitted(env)
    body = request_body(env, target) | changes
    before = own_counts(env), source_snapshot(env)
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, expected=400) == "INVALID_INPUT"
    assert (own_counts(env), source_snapshot(env)) == before


@pytest.mark.parametrize("field,value,code", [("expected_row_version", "1", "TASK_VERSION_CONFLICT"),
    ("expected_assignment_revision", 2, "TASK_ASSIGNMENT_CHANGED"), ("submission_id", "999999", "SUBMISSION_CHANGED")])
def test_old_or_mismatched_selection_is_rejected(review_env, field, value, code):
    env = review_env
    target, _ = create_submitted(env)
    body = request_body(env, target) | {field: value}
    before = own_counts(env), source_snapshot(env)
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, expected=409) == code
    assert (own_counts(env), source_snapshot(env)) == before


def test_admin_own_submission_uses_existing_upper_reviewer(review_env):
    env = review_env
    target, _ = create_submitted(env, actor="manager")
    body = request_body(env, target, "manager")
    saved = send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, actor="manager")
    own = get(env, "tasks/review-requests?scope=own", "manager")["items"]
    assert own[0]["request_id"] == saved["request_id"] and own[0]["reviewer_id"] == env.ids["reviewer"]
    denied = env.app.test_client().post("/api/v1/write-requests/keys", headers=auth(env, "manager"),
        json={"action": feature.ACK_ACTION, "target": target})
    assert denied.status_code == 403


def test_reviewer_replacement_revokes_old_inbox_and_acknowledgement(review_env):
    env = review_env
    target, _ = create_submitted(env)
    saved = send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", request_body(env, target))
    old_key = key(env, feature.ACK_ACTION, target, "reviewer")
    with env.app.app_context(), transaction() as db:
        db.get(results.DepartmentReviewer, "TEST").reviewer_id = env.ids["replacement"]
    assert get(env, "tasks/review-requests?scope=reviewing", "reviewer")["items"] == []
    item = get(env, "tasks/review-requests?scope=reviewing", "replacement")["items"][0]
    body = {"expected_row_version": item["row_version"], "expected_assignment_revision": item["assignment_revision"]}
    path = f"tasks/{target}/review-requests/{saved['request_id']}/acknowledge"
    assert send(env, feature.ACK_ACTION, target, path, body, actor="reviewer", key_value=old_key, expected=403) == "FORBIDDEN"
    acknowledged = send(env, feature.ACK_ACTION, target, path, body, actor="replacement", expected=200)
    assert acknowledged["request_state"] == "acknowledged"


@pytest.mark.parametrize("stage", ["audit", "receipt"])
def test_failure_rolls_back_request_audit_receipt_and_all_originals(review_env, monkeypatch, stage):
    env = review_env
    target, _ = create_submitted(env)
    body = request_body(env, target)
    before = own_counts(env), source_snapshot(env)
    def fail(*args, **kwargs):
        raise AppError(503, "OWNED_TEST_FAILURE", "Synthetic rollback check")
    if stage == "audit":
        monkeypatch.setattr(feature, "audit", fail)
    else:
        original = Session.add
        def add(session, obj, *args, **kwargs):
            if isinstance(obj, WriteReceipt) and obj.action == feature.REQUEST_ACTION:
                fail()
            return original(session, obj, *args, **kwargs)
        monkeypatch.setattr(Session, "add", add)
    assert send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body, expected=503) == "OWNED_TEST_FAILURE"
    assert (own_counts(env), source_snapshot(env)) == before


@pytest.mark.parametrize("same_key", [True, False])
def test_concurrent_requests_create_exactly_one_pending_row(review_env, same_key):
    env = review_env
    target, _ = create_submitted(env)
    body = request_body(env, target)
    keys = [key(env, feature.REQUEST_ACTION, target) for _ in range(2)]
    if same_key:
        keys[1] = keys[0]
    headers = auth(env)
    barrier = Barrier(2)
    def post(value):
        barrier.wait(timeout=10)
        return env.app.test_client().post(f"/api/v1/tasks/{target}/review-requests",
            headers=headers | {"Idempotency-Key": value}, json=body)
    with ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(post, keys))
    assert sorted(r.status_code for r in responses) == ([201, 201] if same_key else [201, 409])
    assert len(get(env, "tasks/review-requests?scope=own")["items"]) == 1


def test_ack_key_cannot_be_reused_for_another_request_id(review_env):
    env = review_env
    target, _ = create_submitted(env)
    body = request_body(env, target)
    first = send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body)
    ack_body = {name: body[name] for name in ("expected_row_version", "expected_assignment_revision")}
    value = key(env, feature.ACK_ACTION, target, "reviewer")
    send(env, feature.ACK_ACTION, target, f"tasks/{target}/review-requests/{first['request_id']}/acknowledge",
         ack_body, actor="reviewer", key_value=value, expected=200)
    second = send(env, feature.REQUEST_ACTION, target, f"tasks/{target}/review-requests", body)
    assert send(env, feature.ACK_ACTION, target, f"tasks/{target}/review-requests/{second['request_id']}/acknowledge",
                ack_body, actor="reviewer", key_value=value, expected=409) == "IDEMPOTENCY_KEY_REUSED"


def test_missing_019_reports_503_without_creating_storage(review_env):
    env = review_env
    target, _ = create_submitted(env)
    engine = env.app.extensions["db_engine"]
    with engine.begin() as db:
        db.exec_driver_sql("RENAME TABLE task_review_requests TO owned_review_requests_saved")
    try:
        assert get(env, f"tasks/{target}/review-request-context", expected=503) == "TASK_REVIEW_REQUEST_SCHEMA_REQUIRED"
        assert not inspect(engine).has_table("task_review_requests")
    finally:
        with engine.begin() as db:
            db.exec_driver_sql("RENAME TABLE owned_review_requests_saved TO task_review_requests")


def test_actual_loopback_http_request_and_acknowledgement(review_env):
    from werkzeug.serving import make_server
    env = review_env
    target, submission = create_submitted(env)
    original = source_snapshot(env)
    server = make_server("127.0.0.1", 0, env.app, threaded=True)
    thread = Thread(target=server.serve_forever, daemon=True)
    thread.start()
    base = f"http://127.0.0.1:{server.server_port}/api/v1/"
    def http(path, actor="worker", body=None, request_key=None):
        headers = auth(env, actor) | {"Content-Type": "application/json"}
        if request_key:
            headers["Idempotency-Key"] = request_key
        req = Request(base + path, headers=headers, data=None if body is None else json.dumps(body).encode())
        with urlopen(req, timeout=10) as response:
            return json.loads(response.read())["data"]
    try:
        ctx = http(f"tasks/{target}/review-request-context")["context"]
        body = dict(expected_row_version=ctx["row_version"], expected_assignment_revision=ctx["assignment_revision"],
                    submission_id=submission, reason="Actual loopback request")
        value = http("write-requests/keys", body={"action": feature.REQUEST_ACTION, "target": target})["request_key"]
        saved = http(f"tasks/{target}/review-requests", body=body, request_key=value)
        inbox = http("tasks/review-requests?scope=reviewing", "reviewer")["items"]
        assert len(inbox) == 1 and inbox[0]["request_id"] == saved["request_id"]
        ack_key = http("write-requests/keys", "reviewer", {"action": feature.ACK_ACTION, "target": target})["request_key"]
        acknowledged = http(f"tasks/{target}/review-requests/{saved['request_id']}/acknowledge", "reviewer",
            {"expected_row_version": ctx["row_version"], "expected_assignment_revision": ctx["assignment_revision"]}, ack_key)
        assert acknowledged["request_state"] == "acknowledged"
        assert http("tasks/review-requests?scope=own")["items"][0]["state"] == "acknowledged"
        assert source_snapshot(env) == original
    finally:
        server.shutdown()
        thread.join(timeout=10)
        server.server_close()
        assert not thread.is_alive()


@pytest.mark.parametrize("action", ["request", "acknowledge"])
@pytest.mark.parametrize("outcome", ["approved", "returned"])
def test_manual_request_or_ack_racing_real_decision_never_changes_submission(review_env, action, outcome):
    """Race two independently authenticated requests using the real shared guard.

    The result decision must always succeed because a manual message does not
    change the task version. The message either commits first or is rejected
    after the decision; neither ordering creates a second result or decision.
    """
    env = review_env
    target, submission = create_submitted(env)
    body = request_body(env, target)
    action_name, actor = feature.REQUEST_ACTION, "worker"
    path = f"tasks/{target}/review-requests"
    request_id = None
    if action == "acknowledge":
        saved = send(env, action_name, target, path, body)
        request_id = saved["request_id"]
        action_name, actor = feature.ACK_ACTION, "reviewer"
        path += f"/{request_id}/acknowledge"
        body = {name: body[name] for name in ("expected_row_version", "expected_assignment_revision")}
    manual_key = key(env, action_name, target, actor)
    decision_context = get(env, f"results/submissions/{submission}/context", "reviewer")
    decision_body = dict(expected_row_version=decision_context["row_version"],
        expected_assignment_revision=decision_context["assignment_revision"], outcome=outcome,
        reason="Actual decision concurrent with a manual request")
    decision_key = key(env, "submission." + outcome, submission, "reviewer")
    # Obtain both credentials before the concurrent calls; the fixture does not
    # log in during the race and no test seed changes a result or task status.
    manual_headers = auth(env, actor) | {"Idempotency-Key": manual_key}
    decision_headers = auth(env, "reviewer") | {"Idempotency-Key": decision_key}
    before = source_snapshot(env)
    counts = own_counts(env)
    barrier = Barrier(2)

    def post(spec):
        route, headers, payload = spec
        barrier.wait(timeout=10)
        return env.app.test_client().post("/api/v1/" + route, headers=headers, json=payload)

    with ThreadPoolExecutor(max_workers=2) as pool:
        manual, decision = list(pool.map(post, [
            (path, manual_headers, body),
            (f"results/submissions/{submission}/decisions", decision_headers, decision_body),
        ]))
    successful = manual.status_code == (201 if action == "request" else 200)
    assert manual.status_code in ((201, 409) if action == "request" else (200, 409)), manual.json
    assert decision.status_code == 200, decision.json
    if not successful:
        assert manual.json["error"]["code"] in ("TASK_VERSION_CONFLICT", "TASK_REVIEW_REQUEST_NOT_AVAILABLE")
    after = source_snapshot(env)
    assert before[1:3] == after[1:3]  # All assignment and manager columns remain unchanged.
    with Session(env.app.extensions["db_engine"]) as db:
        task = db.get(TaskRecord, int(target))
        results_rows = list(db.scalars(select(results.Submission).where(results.Submission.task_id == task.task_id)))
        decisions = list(db.scalars(select(results.Decision).where(results.Decision.submission_id == int(submission))))
        request_rows = list(db.scalars(select(TaskReviewRequest).where(TaskReviewRequest.task_id == task.task_id)))
        assert task.task_status == outcome and task.row_version == 4 and task.current_assignment_revision == 1
        assert len(results_rows) == 1 and results_rows[0].id == int(submission)
        assert results_rows[0].round_no == 1 and results_rows[0].status == outcome
        assert results_rows[0].description == "Submitted original result"
        assert len(decisions) == 1 and decisions[0].outcome == outcome
        if action == "request":
            assert len(request_rows) == int(successful)
        else:
            assert len(request_rows) == 1 and request_rows[0].request_id == int(request_id)
            assert request_rows[0].status == ("acknowledged" if successful else "pending")
    after_counts = own_counts(env)
    assert after_counts[0] == counts[0] + int(action == "request" and successful)
    assert after_counts[1:] == (counts[1] + 1 + int(successful), counts[2] + 1 + int(successful))
    rows = get(env, "tasks/review-requests?scope=own")["items"]
    assert all(item["state"] == "resolved" for item in rows)


@pytest.mark.parametrize("malformed,status", [
    ("boolean-assignment", 400), ("boolean-row-version", 400), ("boolean-submission", 400),
    ("boolean-reason", 400), ("duplicate-reason", 400), ("invalid-utf8", 400),
    ("nan", 400), ("array", 400), ("surrogate", 400), ("oversize", 413),
    ("unknown-query", 400), ("text-media-type", 415),
])
def test_raw_manual_request_input_rejects_ambiguous_payload_without_effects(review_env, malformed, status):
    env = review_env
    target, _ = create_submitted(env)
    body = request_body(env, target)
    value = key(env, feature.REQUEST_ACTION, target)
    path = f"/api/v1/tasks/{target}/review-requests"
    content_type = "application/json"
    if malformed.startswith("boolean-"):
        field = {"boolean-assignment": "expected_assignment_revision",
                 "boolean-row-version": "expected_row_version",
                 "boolean-submission": "submission_id", "boolean-reason": "reason"}[malformed]
        body[field] = True
    elif malformed == "surrogate":
        body["reason"] = "\ud800"
    raw = json.dumps(body, ensure_ascii=True, separators=(",", ":")).encode("utf-8")
    if malformed == "duplicate-reason":
        raw = raw[:-1] + b',"reason":"ambiguous second value"}'
    elif malformed == "invalid-utf8":
        raw = b'{"reason":"\xff"}'
    elif malformed == "nan":
        raw = raw.replace(b'"reason":', b'"ignored":NaN,"reason":')
    elif malformed == "array":
        raw = b'[]'
    elif malformed == "oversize":
        raw += b' ' * 32769
    elif malformed == "unknown-query":
        path += "?unknown=1"
    elif malformed == "text-media-type":
        content_type = "text/plain"
    before = own_counts(env), source_snapshot(env)
    response = env.app.test_client().post(path, headers=auth(env) | {"Idempotency-Key": value},
        data=raw, content_type=content_type)
    assert response.status_code == status, response.json
    assert response.json["error"]["code"] == {
        400: "INVALID_INPUT", 413: "HTTP_413", 415: "JSON_REQUIRED"}[status]
    assert (own_counts(env), source_snapshot(env)) == before
