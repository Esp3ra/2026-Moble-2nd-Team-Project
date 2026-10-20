"""Current-access receipts survive write shutdown; no product integration ACK."""
from datetime import timedelta

import pytest
from sqlalchemy import inspect

from remotework import create_app
from remotework.db import get_db, transaction
from remotework.features.tasks.models import TaskAssignment, TaskManager
from remotework.models import AuthSession, User
from remotework.write_requests import register_write_result_reader
from test_task_lifecycle import (ACTIONS, life, lifecycle_flags, key_for, request_one,
                                 send, snapshot)


def saved(life, client, login, action):
    admin = login("admin1", "Admin-test-2026!")[0]
    actor, version, extension = admin, "7", None
    if action == "task.deadline_extension_requested":
        actor = login("staff1", "Staff-test-2026!")[0]
    elif action != "task.cancelled":
        staff, extension, _ = request_one(life, client, login)
        version = "8"
        if action == "task.deadline_extension_withdrawn":
            actor = staff
    key = key_for(client, actor, life, action)
    response = send(client, actor, life, action, version, extension, key)
    assert response.status_code == 200, response.json
    return actor, key, response.json["data"]


def lookup(client, actor, key, life, action):
    return client.post("/api/v1/write-requests/result", headers=actor | {"Idempotency-Key": key},
                       json={"action": action, "target": life[2]})


@pytest.mark.parametrize("action", ACTIONS)
def test_committed_result_readable_after_write_flag_and_ack_removed(life, client, login, action):
    actor, key, expected = saved(life, client, login, action)
    before = snapshot(life)
    life[0].config["ENABLE_TASK_LIFECYCLE_DRAFT"] = False
    life[0].extensions.pop("task_lifecycle_integration_ack")
    result = lookup(client, actor, key, life, action)
    assert result.status_code == 200 and result.json["data"]["result"] == expected
    # Even the exact old successful request cannot re-enter the disabled writer.
    replay = send(client, actor, life, action, extension_id=expected["extension_id"], key=key)
    assert replay.status_code == 503
    assert snapshot(life) == before


@pytest.mark.parametrize("action", ACTIONS)
def test_explicit_cold_read_mode_has_no_business_routes_or_key_issuance(life, client, login, action):
    actor, key, expected = saved(life, client, login, action)
    before = snapshot(life)
    config = dict(life[0].config)
    config.update(ENABLE_TASK_LIFECYCLE_DRAFT=False, ENABLE_TASK_LIFECYCLE_RESULT_READS_DRAFT=True)
    engine = life[0].extensions["db_engine"]
    definitions = {n: inspect(engine).get_columns(n) for n in
                   ("task_cancellations", "task_deadline_extensions")}
    cold = create_app(config)
    try:
        assert "task_lifecycle_integration_ack" not in cold.extensions
        assert "task_queries.cancel" not in cold.view_functions
        assert not any("/deadline-extensions" in str(r) for r in cold.url_map.iter_rules())
        other = cold.test_client()
        result = lookup(other, actor, key, life, action)
        assert result.status_code == 200 and result.json["data"]["result"] == expected
        issue = other.post("/api/v1/write-requests/keys", headers=actor,
                           json={"action": action, "target": life[2]})
        assert issue.status_code == 503 and issue.json["error"]["code"] == "WRITE_ACTION_READ_ONLY"
        # Reader registration performs no DDL or result/audit mutation.
        assert snapshot(life) == before
        for n, columns in definitions.items():
            assert [c["name"] for c in inspect(engine).get_columns(n)] == [c["name"] for c in columns]
    finally:
        cold.extensions["db_engine"].dispose()


@pytest.mark.parametrize("action", ACTIONS)
def test_disabled_result_reads_still_recheck_current_relationship(life, client, login, action):
    actor, key, _ = saved(life, client, login, action)
    life[0].config["ENABLE_TASK_LIFECYCLE_DRAFT"] = False
    with life[0].app_context(), transaction() as db:
        if action in ("task.deadline_extension_requested", "task.deadline_extension_withdrawn"):
            db.get(TaskAssignment, (int(life[2]), 2)).assignee_id = life[1]["staff2"]
        else:
            db.delete(db.get(TaskManager, (int(life[2]), life[1]["admin1"])))
    before = snapshot(life)
    result = lookup(client, actor, key, life, action)
    assert result.status_code == 403
    assert snapshot(life) == before


@pytest.mark.parametrize("change", ["inactive", "session", "role", "key", "expiry"])
def test_read_mode_preserves_identity_key_and_retention_checks(life, client, login, monkeypatch, change):
    action = "task.cancelled"
    actor, key, _ = saved(life, client, login, action)
    life[0].config["ENABLE_TASK_LIFECYCLE_DRAFT"] = False
    expected = 403
    if change in ("inactive", "session", "role"):
        with life[0].app_context(), transaction() as db:
            if change == "inactive":
                db.get(User, life[1]["admin1"]).is_active = False
                expected = 401
            elif change == "session":
                for row in db.query(AuthSession).filter_by(user_id=life[1]["admin1"]).all():
                    db.delete(row)
                expected = 401
            else:
                db.get(User, life[1]["admin1"]).role = "employee"
    elif change == "key":
        key += "x"
        expected = 400
    else:
        from remotework import write_requests
        from remotework.write_requests import WriteReceipt
        with life[0].app_context():
            expiry = get_db().query(WriteReceipt).one().expires_at
        monkeypatch.setattr(write_requests, "_now", lambda: expiry + timedelta(seconds=1))
        expected = 410
    before = snapshot(life)
    assert lookup(client, actor, key, life, action).status_code == expected
    assert snapshot(life) == before


def test_result_reader_registration_is_explicit_immutable_and_app_local(app):
    read = lambda db, target: True
    register_write_result_reader(app, action="probe.saved", authorize=read, read_only=True)
    with pytest.raises(ValueError):
        register_write_result_reader(app, action="probe.saved", authorize=read)
    with pytest.raises(ValueError):
        register_write_result_reader(app, action="probe.invalid", authorize=read, read_only=1)
    other = create_app(dict(app.config))
    try:
        assert "probe.saved" not in other.extensions.get("write_result_readers", {})
        app.test_client().get("/api/v1/health")
        with pytest.raises(RuntimeError):
            register_write_result_reader(app, action="probe.late", authorize=read)
    finally:
        other.extensions["db_engine"].dispose()
