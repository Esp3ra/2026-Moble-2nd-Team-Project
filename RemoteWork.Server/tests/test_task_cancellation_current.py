"""Current cancellation checks in an explicitly owned disposable MySQL.

The independent fixture commit below models cancellation after load_identity's
REPEATABLE READ snapshot but before the protected action. It is not a shared
deployment or member3 integration test, and never enables an integrated ACK.
"""
import os

from flask import g, request
import pytest
from sqlalchemy import select, text
from sqlalchemy.engine import make_url

from remotework.auth import authenticated_transaction, load_identity
from remotework.common import AppError
from remotework.db import get_db
from remotework.features.tasks import lifecycle
from remotework.features.tasks.lifecycle_models import TaskCancellation
from remotework.features.tasks.lifecycle_support import ensure_not_cancelled
from remotework.features.tasks.models import TaskRecord
from test_task_lifecycle import (NOW, error, key_for, life, lifecycle_flags,
                                 send, snapshot)


@pytest.fixture(autouse=True)
def owned_cancellation_mysql():
    value = os.getenv("MYSQL_TEST_URL", "")
    if os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1" or not value.startswith("mysql"):
        pytest.skip("Current cancellation checks require the explicit owned disposable MySQL runner")
    assert (make_url(value).database or "").startswith("remote_work_test_")


def _commit_cancellation(app, target, actor_id, previous_connection):
    # A separate connection commits only the named synthetic fixture's marker
    # and matching version; it does not call or replace the action under test.
    with app.extensions["db_factory"]() as other:
        assert other.scalar(text("SELECT CONNECTION_ID()")) != previous_connection
        task = other.scalar(select(TaskRecord).where(TaskRecord.task_id == int(target)).with_for_update())
        assert task.row_version == 7
        task.row_version, task.updated_at = 8, NOW
        other.add(TaskCancellation(task_id=task.task_id, cancelled_by=actor_id,
                                   reason="independent cancellation fixture", cancelled_at=NOW))
        other.commit()


def test_extension_post_rechecks_cancellation_after_identity_snapshot_with_current_expected_version(life, client, login):
    app, ids, target = life
    observations = {}
    path = "/api/v1/tasks/" + target + "/deadline-extensions"

    @app.before_request
    def cancel_after_identity_before_action():
        if request.path != path or request.headers.get("X-Cancel-Window-Fixture") != "1":
            return
        assert g.user.id == ids["staff1"]
        db = get_db()
        assert db.scalar(select(TaskRecord.row_version).where(TaskRecord.task_id == int(target))) == 7
        connection = db.scalar(text("SELECT CONNECTION_ID()"))
        _commit_cancellation(app, target, ids["admin1"], connection)
        # Prove the vulnerable old snapshot is still present. A plain SELECT
        # cannot be the authoritative guard, despite a later current version.
        assert db.execute(text("SELECT task_id FROM task_cancellations WHERE task_id=:id"),
                          {"id": int(target)}).first() is None
        observations["after_fixture_commit"] = snapshot(life)

    headers, _ = login("staff1", "Staff-test-2026!")
    key = key_for(client, headers, life, "task.deadline_extension_requested")
    response = send(client, headers | {"X-Cancel-Window-Fixture": "1"}, life,
                    "task.deadline_extension_requested", version="8", key=key)
    error(response, 409, "TASK_CANCELLED")
    assert "after_fixture_commit" in observations
    assert snapshot(life) == observations["after_fixture_commit"]
    # Version 8 was intentionally current, so version conflict cannot mask a
    # missing cancellation check. No pending request, receipt or action audit.
    assert snapshot(life)[1] == 8
    assert snapshot(life)[4:] == ((), 1, 0, 0)


@pytest.mark.parametrize("lifecycle_enabled", [True, False])
def test_current_guard_rejects_persisted_marker_even_when_lifecycle_routes_are_disabled(life, client, login, lifecycle_enabled):
    app, ids, target = life
    headers, _ = login("staff1", "Staff-test-2026!")
    app.config["ENABLE_TASK_LIFECYCLE_DRAFT"] = lifecycle_enabled
    with app.test_request_context("/api/v1/tasks/" + target + "/lifecycle", headers=headers):
        load_identity()
        db = get_db()
        assert db.scalar(select(TaskRecord.row_version).where(TaskRecord.task_id == int(target))) == 7
        connection = db.scalar(text("SELECT CONNECTION_ID()"))
        _commit_cancellation(app, target, ids["admin1"], connection)
        before = snapshot(life)
        with pytest.raises(AppError) as caught:
            with authenticated_transaction() as guarded:
                task, _assignment, _managers, _users = lifecycle._current(guarded, target, mode="own")
                assert task.row_version == 8
                ensure_not_cancelled(guarded, task.task_id)
        assert (caught.value.status, caught.value.code) == (409, "TASK_CANCELLED")
    assert snapshot(life) == before
