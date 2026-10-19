"""Member 1 integration review: member 2 creation against the actual raw 002 DDL.

Only the explicitly disposable MySQL fixture is used. No runtime migration or
shared schema approval is implied by these checks.
"""
from pathlib import Path

import pytest
from sqlalchemy import event, func, inspect, select
from sqlalchemy.engine import make_url
from sqlalchemy.exc import IntegrityError

from remotework.db import get_db
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, SchemaVersion, User
from remotework.write_requests import WriteReceipt
from test_task_creation import (creation, enable_creation_draft, body_for, issue_key,
                                pytestmark)


@pytest.fixture
def raw_creation(creation):
    app, ids = creation
    engine = app.extensions["db_engine"]
    assert engine.dialect.name == "mysql"
    assert make_url(engine.url).database.startswith("remote_work_test_")
    tables = (TaskManager.__table__, TaskAssignment.__table__, TaskRecord.__table__)
    with engine.connect() as connection:
        for table in tables:
            assert connection.scalar(select(func.count()).select_from(table)) == 0
    # Drop only these three newly empty fixture tables, never common rows.
    for table in tables:
        table.drop(engine)
    source = (Path(__file__).resolve().parents[1] / "migrations/002_task_queries.mysql.sql").read_text(encoding="utf-8")
    sql = "\n".join(line for line in source.splitlines() if not line.lstrip().startswith("--"))
    with engine.begin() as connection:
        for statement in sql.split(";"):
            if statement.strip():
                connection.exec_driver_sql(statement)
    return app, ids


def test_raw002_creation_initial_relations_replay_and_current_read(raw_creation, client, login):
    app, ids = raw_creation
    headers, _ = login("admin1", "Admin-test-2026!")
    keyed = headers | {"Idempotency-Key": issue_key(client, headers)}
    body = body_for(ids)
    response = client.post("/api/v1/tasks", headers=keyed, json=body)
    assert response.status_code == 201, response.json
    result = response.json["data"]
    assert set(result) == {"task_id", "row_version", "assignment_revision", "task_status", "updated_at"}
    assert result["row_version"] == "1" and result["assignment_revision"] == 1
    assert result["task_status"] == "not_started"
    assert isinstance(result["task_id"], str) and str(int(result["task_id"])) == result["task_id"]
    assert result["updated_at"].endswith("Z")
    assert client.post("/api/v1/tasks", headers=keyed, json=body).json["data"] == result
    assert client.post("/api/v1/tasks", headers=keyed, json=body | {"title": "different"}).status_code == 409
    recovered = client.post("/api/v1/write-requests/result", headers=keyed,
                            json={"action": "task.created", "target": "new"})
    assert recovered.json["data"]["result"] == result
    with app.app_context():
        db = get_db()
        task_id = int(result["task_id"])
        assignment = db.get(TaskAssignment, (task_id, 1))
        assert assignment.assignee_id == ids["staff1"] and assignment.accepted_at is None
        assert db.get(TaskManager, (task_id, ids["admin1"])) is not None
        assert db.scalar(select(func.count()).select_from(TaskRecord)) == 1
        assert db.scalar(select(func.count()).select_from(WriteReceipt)) == 1
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "task.created")) == 1
        assert db.scalars(select(SchemaVersion.version)).all() == [1]
    detail = client.get("/api/v1/tasks/" + result["task_id"], headers=headers)
    assert detail.status_code == 200
    columns = {column["name"]: str(column["type"]) for column in inspect(app.extensions["db_engine"]).get_columns("tasks")}
    assert columns["task_id"] == columns["row_version"] == "BIGINT"
    assert columns["created_by"] == "INTEGER"


def test_raw002_mid_assignment_failure_rolls_back_every_success_row(raw_creation, client, login):
    app, ids = raw_creation
    headers, _ = login("admin1", "Admin-test-2026!")
    keyed = headers | {"Idempotency-Key": issue_key(client, headers)}
    engine = app.extensions["db_engine"]
    def fail(connection, cursor, statement, parameters, context, many):
        if "insert into task_assignments" in statement.lower():
            raise RuntimeError("synthetic assignment insertion failure")
    event.listen(engine, "before_cursor_execute", fail)
    try:
        assert client.post("/api/v1/tasks", headers=keyed, json=body_for(ids)).status_code == 500
    finally:
        event.remove(engine, "before_cursor_execute", fail)
    with app.app_context():
        db = get_db()
        for model in (TaskRecord, TaskAssignment, TaskManager, WriteReceipt):
            assert db.scalar(select(func.count()).select_from(model)) == 0
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "task.created")) == 0
        assert db.scalar(select(func.count()).select_from(User)) >= 2
    assert client.post("/api/v1/tasks", headers=keyed, json=body_for(ids)).status_code == 201


def test_raw002_rejects_missing_user_foreign_key(raw_creation):
    app, _ = raw_creation
    engine = app.extensions["db_engine"]
    with pytest.raises(IntegrityError), engine.begin() as connection:
        connection.exec_driver_sql("INSERT INTO tasks (title,description,task_status,start_date,due_date,"
            "current_assignment_revision,row_version,created_by,created_at,updated_at) VALUES "
            "('fixture','','not_started','2026-10-19','2026-10-20',1,1,2147483647,UTC_TIMESTAMP(),UTC_TIMESTAMP())")
