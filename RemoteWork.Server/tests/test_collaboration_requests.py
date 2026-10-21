"""Isolated DB tests. Never connect to the member 1/shared development server."""
import os
import uuid
from datetime import date, datetime, timedelta
import pytest
from sqlalchemy import delete, select, func
from sqlalchemy.engine import make_url
from remotework import create_app
from remotework.auth import create_user
from remotework.db import init_schema, transaction, get_db
from remotework.models import Base, User
from remotework.write_requests import WriteReceipt
from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
from remotework.features.collaboration import install
from remotework.features.collaboration.models import FeatureBase, CollaborationRequest as Row, CollaborationEvent
from remotework.features.collaboration.task_bridge import current_task_context


@pytest.fixture
def app(tmp_path):
    url = os.getenv("MYSQL_TEST_URL")
    if not url:
        pytest.skip("MySQL only: set MYSQL_TEST_URL to a dedicated disposable database")
    if make_url(url).get_backend_name() != "mysql" or not (make_url(url).database or "").startswith("remote_work_test_"):
        raise RuntimeError("Dedicated disposable test DB required")
    # Include persisted lifecycle FKs from earlier isolated runs in teardown.
    from remotework.features.tasks import lifecycle_models  # noqa: F401
    app = create_app({"TESTING": True, "SECRET_KEY": "collaboration-isolated-test-" + "x" * 32,
        "DATABASE_URL": url, "ENABLE_TASK_QUERY_DRAFT": False, "ENABLE_COMMON_WRITE_DRAFT": True,
        "ENABLE_COLLABORATION_DRAFT": True, "ENABLE_TASK_LIFECYCLE_DRAFT": False,
        "ENABLE_CHAT_DRAFT": False, "ENABLE_NOTICE_DRAFT": False,
        "COLLABORATION_CLOCK": lambda: datetime(2026, 10, 21, 3), "WRITE_REQUEST_TTL_SECONDS": 3600})
    install(app, task_context=current_task_context)
    with app.app_context():
        engine = app.extensions["db_engine"]
        FeatureBase.metadata.drop_all(engine)
        Base.metadata.drop_all(engine)
        init_schema()
        FeatureBase.metadata.create_all(engine)
        with transaction() as db:
            for table in reversed(Base.metadata.sorted_tables):
                if table.name not in ("admin_guard", "schema_versions"):
                    db.execute(delete(table))
            for index in range(1, 4):
                create_user(db, {"login_id": f"staff{index}", "password": "Staff-test-2026!",
                    "display_name": f"직원{index}", "employee_no": f"E{index}",
                    "department": "개발팀", "role": "employee"})
            db.add(TaskRecord(task_id=1, title="진행중 업무", description="비공개 원문",
                task_status="in_progress", start_date=date(2026, 10, 20), due_date=date(2026, 10, 30),
                current_assignment_revision=1, row_version=1, created_by=1))
            db.flush()
            db.add(TaskAssignment(task_id=1, revision=1, assignee_id=1, assigned_by=1))
    yield app
    FeatureBase.metadata.drop_all(engine)
    engine.dispose()


def headers(client, user=1):
    r = client.post("/api/v1/auth/login", json={"login_id": f"staff{user}", "password": "Staff-test-2026!"})
    assert r.status_code == 200, r.json
    return {"Authorization": "Bearer " + r.json["data"]["access_token"]}


def write(client, who, action, target, path, body, key=None):
    if key is None:
        issued = client.post("/api/v1/write-requests/keys", headers=who, json={"action": action, "target": target})
        assert issued.status_code == 201, issued.json
        key = issued.json["data"]["request_key"]
    return client.post("/api/v1/collaboration-requests" + path,
        headers=who | {"Idempotency-Key": key}, json=body), key


def send(client, who, **changes):
    body = {"request_id": str(uuid.uuid4()), "task_id": "1", "recipient_id": 2,
        "title": "함께 검토", "request_text": "화면 검토를 부탁드립니다.", "desired_date": "2026-10-25",
        "expected_row_version": "1", "expected_assignment_revision": 1} | changes
    response, key = write(client, who, "collaboration.created", "1", "", body)
    return response, key, body


def rows(client, who, scope="received"):
    r = client.get("/api/v1/collaboration-requests?scope=" + scope, headers=who)
    assert r.status_code == 200, r.json
    return r.json["data"]["items"]


def test_send_accept_persistence_and_no_permission_grant(app, client):
    sender, recipient, outsider = headers(client), headers(client, 2), headers(client, 3)
    r, key, body = send(client, sender)
    assert r.status_code == 201, r.json
    identifier = body["request_id"]
    listed = rows(client, recipient)
    assert len(listed) == 1 and listed[0]["status"] == "pending"
    assert listed[0]["task_id"] == "1" and type(listed[0]["assignment_revision"]) is int
    assert "description" not in listed[0] and "files" not in listed[0]
    assert rows(client, outsider) == []
    response, _ = write(client, recipient, "collaboration.accepted", identifier,
        f"/{identifier}/actions/accept", {"expected_row_version": "1"})
    assert response.status_code == 200, response.json
    # A new DB connection/client sees the stored response, not an in-memory flag.
    app.extensions["db_engine"].dispose()
    with app.test_client() as reopened:
        assert rows(reopened, sender, "sent")[0]["status"] == "accepted"
    restarted = create_app(dict(app.config))
    install(restarted, task_context=current_task_context)
    with restarted.test_client() as other:
        assert rows(other, sender, "sent")[0]["status"] == "accepted"
    restarted.extensions["db_engine"].dispose()
    with app.app_context():
        db = get_db()
        assert db.get(TaskAssignment, (1, 1)).assignee_id == 1
        assert db.get(TaskRecord, 1).row_version == 1
        assert db.scalar(select(func.count()).select_from(TaskManager)) == 0
        assert db.scalar(select(func.count()).select_from(CollaborationEvent)) == 2


def test_decline_and_duplicate_delivery_receipts(app, client):
    a, b = headers(client), headers(client, 2)
    r, key, body = send(client, a)
    again, _ = write(client, a, "collaboration.created", "1", "", body, key)
    assert r.status_code == again.status_code == 201
    identifier = body["request_id"]
    path = f"/{identifier}/actions/decline"
    result, key2 = write(client, b, "collaboration.declined", identifier, path, {"expected_row_version": "1"})
    repeated, _ = write(client, b, "collaboration.declined", identifier, path, {"expected_row_version": "1"}, key2)
    assert result.status_code == repeated.status_code == 200
    assert rows(client, a, "sent")[0]["status"] == "declined"
    assert send(client, a)[0].status_code == 201  # explicit new request after terminal
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(Row)) == 2


def test_one_active_pending_or_accepted(app, client):
    a, b = headers(client), headers(client, 2)
    r, _, body = send(client, a)
    assert r.status_code == 201
    assert send(client, a)[0].status_code == 409
    identifier = body["request_id"]
    assert write(client, b, "collaboration.accepted", identifier,
        f"/{identifier}/actions/accept", {"expected_row_version": "1"})[0].status_code == 200
    assert send(client, a)[0].status_code == 409


def test_wrong_actor_and_stale_version(app, client):
    a, b, c = headers(client), headers(client, 2), headers(client, 3)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    for who, expected in ((a, 403), (b, 409)):
        r, _ = write(client, who, "collaboration.accepted", identifier,
            f"/{identifier}/actions/accept", {"expected_row_version": "2" if who is b else "1"})
        assert r.status_code == expected
    denied = client.post("/api/v1/write-requests/keys", headers=c,
        json={"action": "collaboration.accepted", "target": identifier})
    assert denied.status_code == 404
    assert rows(client, b)[0]["status"] == "pending"


@pytest.mark.parametrize("field,value", [("task_id", 1), ("expected_row_version", "01"),
    ("expected_assignment_revision", "1"), ("expected_assignment_revision", True),
    ("recipient_id", 1), ("desired_date", "2026-10-31"), ("desired_date", "2026-10-20")])
def test_input_and_deadline(app, client, field, value):
    a = headers(client)
    assert send(client, a, **{field: value})[0].status_code == 400
    assert rows(client, a, "sent") == []


def test_expiry_is_durable_despite_rejected_accept(app, client):
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    key = client.post("/api/v1/write-requests/keys", headers=b,
        json={"action": "collaboration.accepted", "target": identifier}).json["data"]["request_key"]
    app.config["COLLABORATION_CLOCK"] = lambda: datetime(2026, 10, 26)
    r, _ = write(client, b, "collaboration.accepted", identifier,
        f"/{identifier}/actions/accept", {"expected_row_version": "1"}, key)
    assert r.status_code == 409
    with app.app_context():
        assert get_db().get(Row, identifier).status == "expired"
        assert get_db().scalar(select(func.count()).select_from(WriteReceipt)) == 1


def test_manager_version_change_does_not_end_but_unknown_continuity_blocks_accept(app, client):
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    with app.app_context(), transaction() as db:
        db.get(TaskRecord, 1).row_version = 2
    assert rows(client, b)[0]["status"] == "pending"
    r, _ = write(client, b, "collaboration.accepted", identifier,
        f"/{identifier}/actions/accept", {"expected_row_version": "1"})
    assert r.status_code == 503
    # Explicit owner history double. Production must not install this lambda.
    app.extensions["collaboration_continuity_reader"] = lambda *args: True
    assert write(client, b, "collaboration.accepted", identifier,
        f"/{identifier}/actions/accept", {"expected_row_version": "1"})[0].status_code == 200


def test_interrupted_and_reassigned_tasks_end_relationship(app, client):
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    app.extensions["collaboration_continuity_reader"] = lambda *args: False
    with app.app_context(), transaction() as db:
        db.get(TaskRecord, 1).row_version = 4  # submitted -> returned -> in_progress
    assert rows(client, b)[0]["end_reason"] == "source_task_interrupted"
    app.extensions.pop("collaboration_continuity_reader")
    _, _, body2 = send(client, a, expected_row_version="4")
    with app.app_context(), transaction() as db:
        db.add(TaskAssignment(task_id=1, revision=2, assignee_id=3, assigned_by=1))
        db.get(TaskRecord, 1).current_assignment_revision = 2
    assert next(x for x in rows(client, b) if x["request_id"] == body2["request_id"])["end_reason"] == "source_task_changed"


def test_inactive_recipient_and_90_day_visibility(app, client):
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    assert write(client, b, "collaboration.declined", identifier,
        f"/{identifier}/actions/decline", {"expected_row_version": "1"})[0].status_code == 200
    app.config["COLLABORATION_CLOCK"] = lambda: datetime(2027, 2, 1)
    assert rows(client, a, "sent") == []
    with app.app_context(), transaction() as db:
        assert db.get(Row, identifier) is not None  # no physical purge
        db.get(User, 2).is_active = False
    app.config["COLLABORATION_CLOCK"] = lambda: datetime(2026, 10, 21)
    assert send(client, a)[0].status_code == 409


@pytest.mark.parametrize("decision,actor", [("complete", 1), ("leave", 2), ("withdraw", 1)])
def test_accepted_actions_are_distinct_and_late_is_not_expiry(app, client, decision, actor):
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    assert write(client, b, "collaboration.accepted", identifier,
        f"/{identifier}/actions/accept", {"expected_row_version": "1"})[0].status_code == 200
    app.config["COLLABORATION_CLOCK"] = lambda: datetime(2026, 10, 26)
    assert rows(client, b)[0]["status"] == "accepted" and rows(client, b)[0]["is_late"]
    state = {"complete": "completed", "leave": "left", "withdraw": "withdrawn"}[decision]
    r, _ = write(client, a if actor == 1 else b, "collaboration." + state, identifier,
        f"/{identifier}/actions/{decision}", {"expected_row_version": "2"})
    assert r.status_code == 200 and rows(client, a, "sent")[0]["status"] == state


def test_failure_rolls_back_request_event_and_receipt(app, client, monkeypatch):
    import remotework.features.collaboration.routes as route
    a = headers(client)
    def fail(*args, **kwargs):
        raise RuntimeError("Explicit isolated atomicity failure")
    monkeypatch.setattr(route, "audit", fail)
    assert send(client, a)[0].status_code == 500
    with app.app_context():
        for model in (Row, CollaborationEvent, WriteReceipt):
            assert get_db().scalar(select(func.count()).select_from(model)) == 0


def test_mysql_concurrent_opposite_responses_have_one_winner(app, client):
    from concurrent.futures import ThreadPoolExecutor
    from threading import Barrier
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    identifier = body["request_id"]
    keys = {}
    for action in ("accepted", "declined"):
        keys[action] = client.post("/api/v1/write-requests/keys", headers=b,
            json={"action": "collaboration." + action, "target": identifier}).json["data"]["request_key"]
    gate = Barrier(2)
    def respond(decision, state):
        with app.test_client() as other:
            gate.wait(timeout=10)
            return write(other, b, "collaboration." + state, identifier,
                f"/{identifier}/actions/{decision}", {"expected_row_version": "1"}, keys[state])[0].status_code
    with ThreadPoolExecutor(max_workers=2) as pool:
        first = pool.submit(respond, "accept", "accepted")
        second = pool.submit(respond, "decline", "declined")
        assert sorted((first.result(timeout=20), second.result(timeout=20))) == [200, 409]
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(CollaborationEvent)) == 2
        assert get_db().scalar(select(func.count()).select_from(WriteReceipt)) == 2


def test_mysql_cancelled_task_ends_without_changing_task_status(app, client):
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    a, b = headers(client), headers(client, 2)
    _, _, body = send(client, a)
    engine = app.extensions["db_engine"]
    TaskCancellation.__table__.create(engine, checkfirst=True)
    TaskDeadlineExtension.__table__.create(engine, checkfirst=True)
    with app.app_context(), transaction() as db:
        db.add(TaskCancellation(task_id=1, cancelled_by=1, reason="격리 검증"))
    assert rows(client, b)[0]["status"] == "ended"
    with app.app_context():
        assert get_db().get(TaskRecord, 1).task_status == "in_progress"


def test_missing_storage_fails_closed_without_automatic_creation(app, client):
    from sqlalchemy import inspect
    a = headers(client)
    engine = app.extensions["db_engine"]
    FeatureBase.metadata.drop_all(engine)
    r = client.get("/api/v1/collaboration-requests", headers=a)
    assert r.status_code == 503 and r.json["error"]["code"] == "COLLABORATION_SCHEMA_REQUIRED"
    with app.app_context():
        init_schema()
    assert not inspect(engine).has_table("collaboration_requests")


def test_mysql_missing_unique_constraint_is_not_valid_storage(app, client):
    from sqlalchemy import text
    a = headers(client)
    with app.extensions["db_engine"].begin() as connection:
        connection.execute(text("ALTER TABLE collaboration_requests DROP INDEX uq_collaboration_active_target"))
    r = client.get("/api/v1/collaboration-requests", headers=a)
    assert r.status_code == 503


def test_exact_proposed_mysql_ddl_can_store_request(app, client):
    from pathlib import Path
    a = headers(client)
    engine = app.extensions["db_engine"]
    FeatureBase.metadata.drop_all(engine)
    sql = (Path(__file__).resolve().parents[1] / "migrations/012_collaboration_requests.mysql.sql").read_text(encoding="utf-8")
    with engine.begin() as connection:
        for statement in sql.split(";"):
            if statement.strip():
                connection.exec_driver_sql(statement)
    assert send(client, a)[0].status_code == 201
