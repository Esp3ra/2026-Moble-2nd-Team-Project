"""Owner 2 consumes the real shared provider on owned MySQL/raw013 only.

Flask transport, actual file chunks and the registered provider; no deployed
server, SQLite, shared settings, owner3 edits or operational ACK changes.
"""
import base64
from datetime import timedelta
import hashlib
import json
import os
import uuid

from flask import g
import pytest
from sqlalchemy import func, select
from sqlalchemy.engine import make_url

from test_task_creation import counts, body_for, issue_key, create, assert_error
from remotework.db import get_db, transaction
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, User, utcnow
from remotework.write_requests import WriteReceipt


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Requires the explicit owned MySQL attachment runner; no SQLite fallback")


@pytest.fixture(autouse=True)
def flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_MANAGEMENT", "RESULTS", "SHARED_ATTACHMENTS"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    for name in ("TASK_LIFECYCLE", "TASK_START", "NOTICE", "CHAT"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")


@pytest.fixture
def files_env(request, tmp_path):
    # Import only test metadata before conftest resets the owned raw-DDL tables.
    from remotework.features import shared_attachments as shared, results
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    assert (make_url(app.config["DATABASE_URL"]).database or "").startswith("remote_work_test_")
    assert app.extensions["results_shared_attachments"] is shared
    app.config["RESULTS_STORAGE_ROOT"] = str(tmp_path / "files")
    with app.app_context():
        ids = {user.login_id: user.id for user in get_db().scalars(select(User))}
    client = app.test_client()
    login = client.post("/api/v1/auth/login", json={"login_id": "admin1", "password": "Admin-test-2026!"})
    assert login.status_code == 200
    return app, client, {"Authorization": "Bearer " + login.json["data"]["access_token"]}, ids, shared


def upload(env, content=b"owned actual file bytes", complete=True):
    _app, client, headers, _ids, _shared = env
    response = client.post("/api/v1/results/shared/uploads/start", headers=headers,
        json={"name": "owned.txt", "size": len(content), "sha256": hashlib.sha256(content).hexdigest()})
    assert response.status_code == 201, response.json
    file_id = response.json["data"]["attachment_id"]
    for offset in range(0, len(content), 16384):
        part = content[offset:offset + 16384]
        chunk = client.post("/api/v1/results/shared/uploads/" + file_id + "/chunks", headers=headers,
            json={"offset": offset, "data": base64.b64encode(part).decode("ascii")})
        assert chunk.status_code == 200, chunk.json
    if complete:
        response = client.post("/api/v1/results/shared/uploads/" + file_id + "/complete", headers=headers, json={})
        assert response.status_code == 200, response.json
    return file_id


def new_body(env, **changes):
    return body_for(env[3], start_date=utcnow().date().isoformat(),
                    due_date=(utcnow() + timedelta(days=3)).date().isoformat(), **changes)


def state(env):
    app, _, _, _, shared = env
    with app.app_context():
        db = get_db()
        links = tuple((row.attachment_id, row.resource_type, row.resource_id)
            for row in db.scalars(select(shared.SharedLink).order_by(shared.SharedLink.attachment_id,
                shared.SharedLink.resource_type, shared.SharedLink.resource_id)))
        files = tuple((row.id, row.state) for row in db.scalars(select(shared.SharedFile).order_by(shared.SharedFile.id)))
        tasks = tuple((row.task_id, row.title, row.row_version, row.current_assignment_revision)
            for row in db.scalars(select(TaskRecord).order_by(TaskRecord.task_id)))
        audit = db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action.in_(("task.created", "task.updated"))))
    return counts(app), tasks, links, files, audit


def test_real_upload_creation_and_staff_download_with_same_tx_initial_versions(files_env, monkeypatch):
    env = files_env
    app, client, headers, ids, shared = env
    content = b"0123456789" * 2200
    file_id = upload(env, content)
    original, seen = shared.replace_task_files, []
    def checked(db, task_id, attachment_ids, *, expected):
        assert db is get_db() and g.write_transaction_active
        task = db.get(TaskRecord, task_id)
        assert (task.row_version, task.current_assignment_revision) == (1, 1)
        assert (expected.row_version, expected.assignment_revision) == (1, 1)
        assert db.get(TaskAssignment, (task_id, 1)).accepted_at is None
        assert db.get(TaskManager, (task_id, ids["admin1"])) is not None
        seen.append(list(attachment_ids))
        return original(db, task_id, attachment_ids, expected=expected)
    monkeypatch.setattr(shared, "replace_task_files", checked)
    response = create(client, headers, issue_key(client, headers), new_body(env, attachment_ids=[file_id]))
    assert response.status_code == 201, response.json
    result = response.json["data"]
    assert set(result) == {"task_id", "row_version", "assignment_revision", "task_status", "updated_at"}
    assert seen == [[file_id]] and counts(app) == (1, 1, 1, 1, 1)
    staff_login = client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"})
    staff = {"Authorization": "Bearer " + staff_login.json["data"]["access_token"]}
    listed = client.get("/api/v1/results/shared/tasks/" + result["task_id"], headers=staff)
    assert listed.status_code == 200
    assert [item["attachment_id"] for item in listed.json["data"]["items"]] == [file_id]
    downloaded = client.get("/api/v1/results/shared/files/" + file_id + "/download", headers=staff)
    assert downloaded.status_code == 200 and downloaded.data == content


def test_post_provider_audit_failure_rolls_back_and_same_key_retries_once(files_env, monkeypatch):
    from remotework.features.tasks import creation
    from remotework.common import AppError
    env = files_env
    app, client, headers, _, shared = env
    file_id = upload(env)
    body, key = new_body(env, attachment_ids=[file_id]), issue_key(client, headers)
    before, audit = state(env), creation.audit
    def fail(*args, **kwargs):
        raise AppError(503, "TEST_AUDIT_ROLLBACK", "owned rollback")
    monkeypatch.setattr(creation, "audit", fail)
    assert_error(create(client, headers, key, body), 503, "TEST_AUDIT_ROLLBACK")
    assert state(env) == before
    with app.app_context():
        assert (shared.storage() / file_id).read_bytes() == b"owned actual file bytes"
    monkeypatch.setattr(creation, "audit", audit)
    first = create(client, headers, key, body)
    assert first.status_code == 201, first.json
    after = state(env)
    app.extensions.pop("results_shared_attachments")
    again = create(client, headers, key, body)
    assert again.status_code == 201 and again.json["data"] == first.json["data"]
    result = client.post("/api/v1/write-requests/result", headers=headers | {"Idempotency-Key": key},
                         json={"action": "task.created", "target": "new"})
    assert result.status_code == 200 and result.json["data"]["result"] == first.json["data"]
    assert state(env) == after


@pytest.mark.parametrize("empty", (False, True))
def test_omission_and_empty_work_without_registered_provider_and_remain_distinct(files_env, empty):
    env = files_env
    app, client, headers, _, _ = env
    app.extensions.pop("results_shared_attachments")
    body = new_body(env, **({"attachment_ids": []} if empty else {}))
    key = issue_key(client, headers)
    assert create(client, headers, key, body).status_code == 201
    changed = dict(body)
    if empty:
        changed.pop("attachment_ids")
    else:
        changed["attachment_ids"] = []
    assert_error(create(client, headers, key, changed), 409, "IDEMPOTENCY_KEY_REUSED")
    assert counts(app) == (1, 1, 1, 1, 1)


def test_nonempty_missing_provider_then_restore_and_array_order_fingerprint(files_env):
    env = files_env
    app, client, headers, _, _ = env
    ids = [upload(env), upload(env, b"second")]
    body, key = new_body(env, attachment_ids=ids), issue_key(client, headers)
    provider = app.extensions.pop("results_shared_attachments")
    before = state(env)
    assert_error(create(client, headers, key, body), 503, "TASK_ATTACHMENT_PROVIDER_REQUIRED")
    assert state(env) == before
    app.extensions["results_shared_attachments"] = provider
    assert create(client, headers, key, body).status_code == 201
    assert_error(create(client, headers, key, body | {"attachment_ids": list(reversed(ids))}),
                 409, "IDEMPOTENCY_KEY_REUSED")


@pytest.mark.parametrize("failure,code,status", (("missing", "ATTACHMENT_NOT_FOUND", 404),
    ("incomplete", "ATTACHMENT_UNAVAILABLE", 409), ("expired", "FORBIDDEN", 403),
    ("other_owner", "FORBIDDEN", 403), ("schema", "SHARED_ATTACHMENT_SCHEMA_REQUIRED", 503)))
def test_provider_errors_are_preserved_without_partial_task_or_file_changes(files_env, monkeypatch, failure, code, status):
    from remotework.common import AppError
    env = files_env
    app, client, headers, ids, shared = env
    file_id = upload(env, complete=failure != "incomplete") if failure != "missing" else str(uuid.uuid4())
    if failure in ("expired", "other_owner"):
        with app.app_context(), transaction() as db:
            item = db.get(shared.SharedFile, file_id)
            if failure == "expired":
                item.expires_at = utcnow() - timedelta(seconds=1)
            else:
                item.owner_id = ids["staff1"]
    if failure == "schema":
        def unavailable(_db):
            raise AppError(503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED", "explicit schema boundary injection")
        monkeypatch.setattr(shared, "require_schema", unavailable)
    before = state(env)
    assert_error(create(client, headers, issue_key(client, headers), new_body(env, attachment_ids=[file_id])), status, code)
    assert state(env) == before


@pytest.mark.parametrize("bad", (None, "", 4, {}, [None], [str(uuid.uuid4()).upper()],
    ["{" + str(uuid.uuid4()) + "}"], [str(uuid.uuid4()).replace("-", "")],
    ["00000000-0000-0000-0000-000000000001"] * 2,
    [str(uuid.uuid4()) for _ in range(11)]))
def test_invalid_attachment_selection_does_not_write(files_env, bad):
    env = files_env
    _, client, headers, _, _ = env
    before = state(env)
    assert_error(create(client, headers, issue_key(client, headers), new_body(env, attachment_ids=bad)), 400, "INVALID_INPUT")
    assert state(env) == before


def test_duplicate_json_attachment_member_is_rejected(files_env):
    env = files_env
    _, client, headers, _, _ = env
    body = json.dumps(new_body(env, attachment_ids=[]))
    duplicate = body[:-1] + ', "attachment_ids": []}'
    before = state(env)
    response = client.post("/api/v1/tasks", headers=headers | {"Idempotency-Key": issue_key(client, headers)},
                           data=duplicate, content_type="application/json")
    assert_error(response, 400, "INVALID_INPUT")
    assert state(env) == before


def test_new_task_reuses_readable_file_without_removing_original_task_link(files_env):
    env = files_env
    _, client, headers, _, _ = env
    file_id = upload(env)
    body = new_body(env, attachment_ids=[file_id])
    first = create(client, headers, issue_key(client, headers), body)
    second = create(client, headers, issue_key(client, headers), body)
    assert first.status_code == second.status_code == 201
    assert len(state(env)[2]) == 2


@pytest.mark.parametrize("explicit", ([], ["00000000-0000-0000-0000-000000000001"]))
def test_explicit_edit_files_fail_closed_and_omission_retains_link(files_env, explicit):
    env = files_env
    app, client, headers, _, _ = env
    file_id = upload(env)
    response = create(client, headers, issue_key(client, headers), new_body(env, attachment_ids=[file_id]))
    assert response.status_code == 201
    target = response.json["data"]["task_id"]
    body = {name: value for name, value in new_body(env).items()
            if name in ("title", "description", "start_date", "due_date")}
    body |= {"expected_row_version": "1", "expected_assignment_revision": 1, "title": "changed"}
    issued = client.post("/api/v1/write-requests/keys", headers=headers, json={"action": "task.updated", "target": target})
    assert issued.status_code == 201
    key_headers = headers | {"Idempotency-Key": issued.json["data"]["request_key"]}
    before = state(env)
    assert_error(client.patch("/api/v1/tasks/" + target, headers=key_headers,
        json=body | {"attachment_ids": explicit}), 503, "TASK_ATTACHMENT_REPLACEMENT_NOT_READY")
    assert state(env) == before
    assert_error(client.patch("/api/v1/tasks/" + target, headers=key_headers,
        json=body | {"attachment_ids": explicit, "expected_row_version": "2"}), 409, "TASK_VERSION_CONFLICT")
    assert state(env) == before
    response = client.patch("/api/v1/tasks/" + target, headers=key_headers, json=body)
    assert response.status_code == 200 and response.json["data"]["row_version"] == "2"
    assert state(env)[2] == before[2] and state(env)[3] == before[3]


def test_staff_cannot_create_even_with_valid_attachment_body(files_env):
    env = files_env
    _, client, _, _, _ = env
    file_id = upload(env)
    login = client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"})
    headers = {"Authorization": "Bearer " + login.json["data"]["access_token"]}
    before = state(env)
    assert_error(create(client, headers, str(uuid.uuid4()), new_body(env, attachment_ids=[file_id])), 403, "FORBIDDEN")
    assert state(env) == before
