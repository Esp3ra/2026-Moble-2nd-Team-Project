"""Actual owner 2 cancellation consumes owner 3 release in one owned MySQL TX.

This fixture explicitly installs the integrated ACK in a disposable test app,
with real results/shared providers. It is not deployment approval. No result
code is edited, no API attachment-selection field is introduced, and retained
submission files remain readable after cancellation.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import timedelta
import hashlib
import os
from threading import Event
import uuid

import pytest
from sqlalchemy import delete, select, text
from sqlalchemy.engine import make_url

from test_task_lifecycle import life, snapshot, NOW, body, key_for, send


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Actual attachment/cancellation checks require the owned disposable MySQL runner")


@pytest.fixture(autouse=True)
def shared_cancel_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_CREATE", "TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE_RESULT_READS",
                 "NOTICE", "CHAT", "DEVICE_ENROLLMENT", "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_LIFECYCLE", "RESULTS", "SHARED_ATTACHMENTS"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    from remotework.features.tasks import lifecycle
    monkeypatch.setattr(lifecycle, "utcnow", lambda: NOW)


@pytest.fixture
def shared_life(request, tmp_path):
    # Import real provider metadata before the owned conftest resets fixtures.
    from remotework.features import shared_attachments as shared, results
    from remotework.features.tasks.lifecycle_support import INTEGRATED_ACK
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.db import transaction
    from remotework.models import utcnow
    env = request.getfixturevalue("life")
    assert (make_url(env[0].config["DATABASE_URL"]).database or "").startswith("remote_work_test_")
    assert env[0].extensions["results_shared_attachments"] is shared
    env[0].extensions["task_lifecycle_integration_ack"] = dict(INTEGRATED_ACK)
    env[0].config["RESULTS_STORAGE_ROOT"] = str(tmp_path / "results")
    files, content = {}, b"owned cancellation attachment fixture"
    with env[0].app_context(), transaction() as db:
        task = db.get(TaskRecord, int(env[2]))
        task.task_status = "returned"
        previous = results.Submission(task_id=task.task_id, assignment_revision=2, round_no=1,
            submitted_by=env[1]["staff1"], reviewer_at_submission=env[1]["admin1"],
            description="보존할 과거 결과", status="returned", submitted_at=NOW - timedelta(days=1))
        db.add(previous)
        other = TaskRecord(title="다른 업무 참조", description="preserve", task_status="not_started",
            start_date=NOW.date(), due_date=(NOW + timedelta(days=3)).date(),
            current_assignment_revision=1, row_version=1, created_by=env[1]["admin2"])
        db.add(other)
        db.flush()
        db.add(TaskAssignment(task_id=other.task_id, revision=1, assignee_id=env[1]["staff2"],
                              assigned_by=env[1]["admin2"]))
        db.add(TaskManager(task_id=other.task_id, user_id=env[1]["admin2"]))
        for name in ("task_only", "submission", "other_task", "unlinked"):
            attachment_id = str(uuid.uuid4())
            files[name] = attachment_id
            db.add(shared.SharedFile(id=attachment_id, owner_id=env[1]["staff1"],
                upload_task_id=task.task_id, name=name + ".txt", size=len(content),
                sha256=hashlib.sha256(content).hexdigest(), state="ready",
                created_at=utcnow(), expires_at=utcnow() + timedelta(days=30)))
            db.flush()
            if name != "unlinked":
                db.add(shared.SharedLink(attachment_id=attachment_id, resource_type="task",
                    resource_id=task.task_id, task_id=task.task_id))
            if name == "submission":
                db.add(shared.SharedLink(attachment_id=attachment_id, resource_type="submission",
                    resource_id=previous.id, task_id=task.task_id, submission_id=previous.id))
            if name == "other_task":
                db.add(shared.SharedLink(attachment_id=attachment_id, resource_type="task",
                    resource_id=other.task_id, task_id=other.task_id))
        previous_id = previous.id
        root = shared.storage()
        for attachment_id in files.values():
            (root / attachment_id).write_bytes(content)
    return env, files, root, previous_id, content


def _headers(client, user="admin1"):
    password = {"admin1": "Admin-test-2026!", "staff1": "Staff-test-2026!"}.get(user, "Lifecycle-test-2026!")
    response = client.post("/api/v1/auth/login", json={"login_id": user, "password": password})
    assert response.status_code == 200
    return {"Authorization": "Bearer " + response.json["data"]["access_token"]}


def _shared_state(env):
    from remotework.db import get_db
    from remotework.features import shared_attachments as shared
    with env[0].app_context():
        db = get_db()
        return (tuple((row.id, row.state) for row in db.scalars(select(shared.SharedFile).order_by(shared.SharedFile.id))),
            tuple((row.attachment_id, row.resource_type, row.resource_id, row.task_id, row.submission_id)
                for row in db.scalars(select(shared.SharedLink).order_by(shared.SharedLink.attachment_id,
                    shared.SharedLink.resource_type, shared.SharedLink.resource_id))))


def _cancel(env, client, headers, key, version="7"):
    return client.post("/api/v1/tasks/" + env[2] + "/cancel", headers=headers | {"Idempotency-Key": key},
                       json=body(version) | {"reason": "첨부 취소 연결 검증"})


def test_actual_cancellation_releases_only_task_links_and_keeps_submission_download(shared_life):
    from remotework.features import shared_attachments as shared
    env, files, root, submission_id, content = shared_life
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    response = _cancel(env, client, admin, key)
    assert response.status_code == 200, response.json
    assert response.json["data"]["row_version"] == "8"
    assert snapshot(env)[0] == "returned"
    _, links = _shared_state(env)
    assert not any(link[1] == "task" and link[2] == int(env[2]) for link in links)
    assert any(link[0] == files["submission"] and link[1] == "submission" and link[2] == submission_id for link in links)
    assert any(link[0] == files["other_task"] and link[1] == "task" for link in links)
    assert not (root / files["task_only"]).exists()
    for name in ("submission", "other_task", "unlinked"):
        assert (root / files[name]).read_bytes() == content
    staff = _headers(client, "staff1")
    download = client.get("/api/v1/results/shared/files/" + files["submission"] + "/download", headers=staff)
    assert download.status_code == 200 and download.data == content


def test_release_failure_rolls_back_domain_receipt_links_files_and_same_key_can_retry(shared_life, monkeypatch):
    from remotework.common import AppError
    from remotework.features.tasks import lifecycle
    from remotework.features import shared_attachments as shared
    env, files, root, _, content = shared_life
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    before = snapshot(env), _shared_state(env)
    original_audit = lifecycle.audit
    def fail_audit(*args, **kwargs):
        raise AppError(503, "TEST_POST_RELEASE_ROLLBACK", "소유 검증 rollback")
    monkeypatch.setattr(lifecycle, "audit", fail_audit)
    failed = _cancel(env, client, admin, key)
    assert failed.status_code == 503 and failed.json["error"]["code"] == "TEST_POST_RELEASE_ROLLBACK"
    assert (snapshot(env), _shared_state(env)) == before
    assert all((root / item).read_bytes() == content for item in files.values())
    monkeypatch.setattr(lifecycle, "audit", original_audit)
    assert _cancel(env, client, admin, key).status_code == 200
    after = snapshot(env), _shared_state(env)
    def must_not_repeat(*args, **kwargs):
        raise AssertionError("A successful W1 replay must not release task files again")
    monkeypatch.setattr(shared, "release_task_files", must_not_repeat)
    assert _cancel(env, client, admin, key).status_code == 200
    assert (snapshot(env), _shared_state(env)) == after


def test_missing_provider_with_persisted_task_links_blocks_then_same_key_resumes(shared_life):
    env = shared_life[0]
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    provider = env[0].extensions.pop("results_shared_attachments")
    before = snapshot(env), _shared_state(env)
    response = _cancel(env, client, admin, key)
    assert response.status_code == 503 and response.json["error"]["code"] == "TASK_ATTACHMENT_PROVIDER_REQUIRED"
    assert (snapshot(env), _shared_state(env)) == before
    env[0].extensions["results_shared_attachments"] = provider
    assert _cancel(env, client, admin, key).status_code == 200


def test_missing_provider_without_task_references_preserves_submission_file(shared_life):
    from remotework.db import transaction
    from remotework.features import shared_attachments as shared
    env, files, root, _, content = shared_life
    with env[0].app_context(), transaction() as db:
        db.execute(delete(shared.SharedLink).where(shared.SharedLink.resource_type == "task",
                                                   shared.SharedLink.resource_id == int(env[2])))
    env[0].extensions.pop("results_shared_attachments")
    before = _shared_state(env)
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    assert _cancel(env, client, admin, key).status_code == 200
    assert _shared_state(env) == before
    assert (root / files["submission"]).read_bytes() == content


@pytest.mark.parametrize("failure", ("ack", "version", "state", "manager"))
def test_cancellation_policy_failure_happens_before_release(shared_life, monkeypatch, failure):
    from remotework.db import transaction
    from remotework.features import shared_attachments as shared
    from remotework.features.tasks.models import TaskManager, TaskRecord
    env = shared_life[0]
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    if failure == "ack":
        env[0].extensions.pop("task_lifecycle_integration_ack")
    elif failure in ("version", "state", "manager"):
        with env[0].app_context(), transaction() as db:
            task = db.get(TaskRecord, int(env[2]))
            if failure == "version":
                task.row_version += 1
            elif failure == "state":
                task.task_status = "submitted"
            else:
                db.execute(delete(TaskManager).where(TaskManager.task_id == task.task_id,
                                                     TaskManager.user_id == env[1]["admin1"]))
    before = snapshot(env), _shared_state(env)
    def unexpected(*args, **kwargs):
        raise AssertionError("Release must follow current policy/version/ACK checks")
    monkeypatch.setattr(shared, "release_task_files", unexpected)
    response = _cancel(env, client, admin, key)
    assert response.status_code == {"ack": 503, "version": 409, "state": 409, "manager": 403}[failure]
    assert (snapshot(env), _shared_state(env)) == before


def test_missing_provider_partial_schema_fails_closed(shared_life):
    env = shared_life[0]
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    env[0].extensions.pop("results_shared_attachments")
    before = snapshot(env), _shared_state(env)
    engine = env[0].extensions["db_engine"]
    with engine.begin() as connection:
        connection.exec_driver_sql("RENAME TABLE result_shared_chunks TO owner2_test_hidden_chunks")
    try:
        response = _cancel(env, client, admin, key)
        assert response.status_code == 503 and response.json["error"]["code"] == "SHARED_ATTACHMENT_SCHEMA_REQUIRED"
        assert (snapshot(env), _shared_state(env)) == before
    finally:
        with engine.begin() as connection:
            connection.exec_driver_sql("RENAME TABLE owner2_test_hidden_chunks TO result_shared_chunks")


def test_missing_provider_current_read_sees_reference_added_after_auth_snapshot(shared_life):
    from flask import request
    from remotework.common import AppError
    from remotework.db import get_db, transaction
    from remotework.features import shared_attachments as shared
    env, files, _, _, _ = shared_life
    client = env[0].test_client()
    admin = _headers(client)
    key = key_for(client, admin, env, "task.cancelled")
    with env[0].app_context(), transaction() as db:
        db.execute(delete(shared.SharedLink).where(shared.SharedLink.resource_type == "task",
                                                   shared.SharedLink.resource_id == int(env[2])))
    env[0].extensions.pop("results_shared_attachments")
    ready, resume = Event(), Event()
    # Flask setup has already finished. Test-only scheduling attaches directly
    # to this private fixture's request list, never to production app code.
    def pause_after_old_snapshot():
        if request.headers.get("X-Owner2-Test-Snapshot") != "1":
            return
        rows = get_db().execute(text("SELECT attachment_id FROM result_shared_links "
            "WHERE resource_type='task' AND resource_id=:task_id"), {"task_id": int(env[2])}).all()
        assert rows == []
        ready.set()
        if not resume.wait(10):
            raise AppError(503, "TEST_GATE_TIMEOUT", "소유 검증 일정 대기")
    env[0].before_request_funcs.setdefault(None, []).append(pause_after_old_snapshot)
    def cancel_worker():
        return _cancel(env, env[0].test_client(), admin | {"X-Owner2-Test-Snapshot": "1"}, key)
    with ThreadPoolExecutor(max_workers=1) as pool:
        future = pool.submit(cancel_worker)
        try:
            assert ready.wait(10)
            with env[0].app_context(), transaction() as db:
                db.add(shared.SharedLink(attachment_id=files["unlinked"], resource_type="task",
                    resource_id=int(env[2]), task_id=int(env[2])))
            before = snapshot(env), _shared_state(env)
        finally:
            resume.set()
        response = future.result(timeout=15)
    assert response.status_code == 503 and response.json["error"]["code"] == "TASK_ATTACHMENT_PROVIDER_REQUIRED"
    assert (snapshot(env), _shared_state(env)) == before
