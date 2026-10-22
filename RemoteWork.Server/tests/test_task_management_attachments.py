"""Owner 2 first-mutation attachment edits with the actual PR68 provider.

Owned MySQL/raw013 only, real uploaded bytes and common authorization/receipts.
No shared deployment, owner3 product edits, test-only provider or SQLite.
"""
import json
from types import SimpleNamespace
import uuid

from flask import g
import pytest
from sqlalchemy import delete, func, insert, select

from test_task_creation_attachments import (
    files_env, flags, pytestmark, upload, new_body, state,
)
from test_task_creation import create, issue_key, assert_error
from remotework.common import AppError
from remotework.db import get_db, transaction
from remotework.features.tasks import management
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent
from remotework.write_requests import WriteReceipt


def task(env, attachment_ids=()):
    _, client, headers, _, _ = env
    original = new_body(env, attachment_ids=list(attachment_ids))
    response = create(client, headers, issue_key(client, headers), original)
    assert response.status_code == 201, response.json
    return response.json["data"]["task_id"], original


def edit_body(original, **changes):
    body = {key: original[key] for key in ("title", "description", "start_date", "due_date")}
    return body | {"expected_row_version": "1", "expected_assignment_revision": 1} | changes


def edit_key(env, target):
    _, client, headers, _, _ = env
    response = client.post("/api/v1/write-requests/keys", headers=headers,
        json={"action": "task.updated", "target": target})
    assert response.status_code == 201, response.json
    return response.json["data"]["request_key"]


def edit(env, target, body, key=None, headers=None):
    return env[1].patch("/api/v1/tasks/" + target,
        headers=(headers or env[2]) | {"Idempotency-Key": key or edit_key(env, target)}, json=body)


def changed_audits(env, target):
    with env[0].app_context():
        rows = list(get_db().scalars(select(AuditEvent).where(
            AuditEvent.action == "task.updated", AuditEvent.resource_id == target)))
        return [json.loads(row.details_json) for row in rows]


@pytest.mark.parametrize("basic_change", (False, True))
def test_real_attachment_replace_uses_original_tx_versions_and_one_audit(files_env, monkeypatch, basic_change):
    env = files_env
    app, _, _, _, shared = env
    first, second, third = upload(env, b"first"), upload(env, b"second"), upload(env, b"third")
    target, original = task(env, [first])
    body = edit_body(original, attachment_ids=[third, second], **({"title": "new title"} if basic_change else {}))
    read, replace, seen = shared.read_task_attachment_ids, shared.replace_task_files, []
    def check(db, target_id, expected):
        assert db is get_db() and g.write_transaction_active
        current = db.get(TaskRecord, int(target_id))
        assert (current.row_version, current.current_assignment_revision) == (1, 1)
        assert (expected.row_version, expected.assignment_revision) == (1, 1)
        assert db.get(TaskAssignment, (int(target_id), 1)).accepted_at is None
    def checked_read(db, target_id, *, expected):
        check(db, target_id, expected)
        seen.append("read")
        return read(db, target_id, expected=expected)
    def checked_replace(db, target_id, ids, *, expected):
        check(db, target_id, expected)
        assert ids == [third, second]
        seen.append("replace")
        return replace(db, target_id, ids, expected=expected)
    monkeypatch.setattr(shared, "read_task_attachment_ids", checked_read)
    monkeypatch.setattr(shared, "replace_task_files", checked_replace)
    response = edit(env, target, body)
    assert response.status_code == 200, response.json
    assert response.json["data"]["row_version"] == "2"
    assert response.json["data"]["assignment_revision"] == 1
    assert seen == ["read", "replace"] and body["attachment_ids"] == [third, second]
    details, = changed_audits(env, target)
    assert details["changed_fields"] == (["title", "attachment_ids"] if basic_change else ["attachment_ids"])
    assert details["before_attachment_ids"] == [first]
    assert details["attachment_ids"] == sorted([second, third])
    assert set(details) == {"before", "after", "changed_fields", "before_start_date", "before_due_date",
                            "start_date", "due_date", "before_attachment_ids", "attachment_ids"}
    with app.app_context():
        db = get_db()
        assert db.scalar(select(func.count()).select_from(WriteReceipt).where(WriteReceipt.action == "task.updated")) == 1
        assert sorted(db.scalars(select(shared.SharedLink.attachment_id).where(
            shared.SharedLink.resource_type == "task", shared.SharedLink.task_id == int(target)))) == sorted([second, third])
        assert db.get(TaskRecord, int(target)).task_status == "not_started"


def test_same_attachment_set_is_no_change_but_title_change_does_not_replace(files_env, monkeypatch):
    env = files_env
    first, second = upload(env), upload(env, b"second")
    target, original = task(env, [first, second])
    def forbidden_replace(*args, **kwargs):
        raise AssertionError("Same set must not replace references")
    monkeypatch.setattr(env[4], "replace_task_files", forbidden_replace)
    body = edit_body(original, attachment_ids=[second, first])
    before = state(env)
    assert_error(edit(env, target, body), 409, "TASK_CHANGE_NOT_NEEDED")
    assert state(env) == before
    response = edit(env, target, body | {"title": "title only"})
    assert response.status_code == 200
    details, = changed_audits(env, target)
    assert details["changed_fields"] == ["title"]
    assert "attachment_ids" not in details and "before_attachment_ids" not in details
    assert state(env)[2:4] == before[2:4]


def test_empty_final_list_removes_only_target_reference_and_retains_other_task(files_env):
    env = files_env
    app, client, headers, _, shared = env
    content = b"shared across two tasks"
    file_id = upload(env, content)
    target, original = task(env, [file_id])
    other, _ = task(env, [file_id])
    response = edit(env, target, edit_body(original, attachment_ids=[]))
    assert response.status_code == 200, response.json
    assert client.get("/api/v1/results/shared/tasks/" + target, headers=headers).json["data"]["items"] == []
    assert client.get("/api/v1/results/shared/files/" + file_id + "/download", headers=headers).data == content
    with app.app_context():
        links = list(get_db().scalars(select(shared.SharedLink)))
        assert len(links) == 1 and str(links[0].task_id) == other
        assert (shared.storage() / file_id).read_bytes() == content
    details, = changed_audits(env, target)
    assert details["before_attachment_ids"] == [file_id] and details["attachment_ids"] == []


def test_omission_never_calls_file_provider_and_keeps_existing_links(files_env, monkeypatch):
    env = files_env
    target, original = task(env, [upload(env)])
    before = state(env)
    def unexpected(*args, **kwargs):
        raise AssertionError("Omitted attachment_ids must not read or replace files")
    monkeypatch.setattr(env[4], "read_task_attachment_ids", unexpected)
    monkeypatch.setattr(env[4], "replace_task_files", unexpected)
    response = edit(env, target, edit_body(original, title="omitted files"))
    assert response.status_code == 200
    assert state(env)[2:4] == before[2:4]


@pytest.mark.parametrize("missing", ("provider", "reader", "replace"))
@pytest.mark.parametrize("ids", ([], ["00000000-0000-0000-0000-000000000001"]))
def test_missing_public_provider_functions_fail_closed_for_explicit_selection(files_env, missing, ids):
    env = files_env
    target, original = task(env, [upload(env)])
    provider = env[4]
    if missing == "provider":
        env[0].extensions.pop("results_shared_attachments")
    else:
        env[0].extensions["results_shared_attachments"] = SimpleNamespace(**{
            "read_task_attachment_ids": provider.read_task_attachment_ids if missing != "reader" else None,
            "replace_task_files": provider.replace_task_files if missing != "replace" else None,
        })
    before = state(env)
    assert_error(edit(env, target, edit_body(original, title="must not save", attachment_ids=ids)),
                 503, "TASK_ATTACHMENT_REPLACEMENT_NOT_READY")
    assert state(env) == before


def test_schema_failure_and_stale_version_precedence_do_not_write(files_env, monkeypatch):
    env = files_env
    target, original = task(env, [upload(env)])
    calls = []
    def schema_missing(db):
        calls.append(True)
        raise AppError(503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED", "owned schema boundary")
    monkeypatch.setattr(env[4], "require_schema", schema_missing)
    before = state(env)
    body = edit_body(original, attachment_ids=[])
    assert_error(edit(env, target, body | {"expected_row_version": "2"}), 409, "TASK_VERSION_CONFLICT")
    assert calls == [] and state(env) == before
    assert_error(edit(env, target, body), 503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED")
    assert calls == [True] and state(env) == before


@pytest.mark.parametrize("bad", (None, "", {}, [None], ["BAD"],
    ["00000000-0000-0000-0000-000000000001"] * 2, [str(uuid.uuid4()) for _ in range(11)]))
def test_invalid_explicit_edit_selection_does_not_change_task_or_links(files_env, bad):
    env = files_env
    target, original = task(env, [upload(env)])
    before = state(env)
    assert_error(edit(env, target, edit_body(original, attachment_ids=bad)), 400, "INVALID_INPUT")
    assert state(env) == before


def test_provider_failure_and_audit_failure_roll_back_then_original_key_retries(files_env, monkeypatch):
    env = files_env
    old, new = upload(env, b"old"), upload(env, b"new")
    target, original = task(env, [old])
    key = edit_key(env, target)
    before = state(env)
    assert_error(edit(env, target, edit_body(original, attachment_ids=[new, str(uuid.uuid4())]), key),
                 404, "ATTACHMENT_NOT_FOUND")
    assert state(env) == before
    body = edit_body(original, title="combined change", attachment_ids=[new])
    audit = management.audit
    def audit_failure(*args, **kwargs):
        raise AppError(503, "TEST_AUDIT_ROLLBACK", "owned rollback")
    monkeypatch.setattr(management, "audit", audit_failure)
    assert_error(edit(env, target, body, key), 503, "TEST_AUDIT_ROLLBACK")
    assert state(env) == before
    with env[0].app_context():
        assert (env[4].storage() / old).read_bytes() == b"old"
    monkeypatch.setattr(management, "audit", audit)
    saved = edit(env, target, body, key)
    assert saved.status_code == 200
    after = state(env)
    env[0].extensions.pop("results_shared_attachments")
    replayed = edit(env, target, body, key)
    assert replayed.status_code == 200 and replayed.json["data"] == saved.json["data"]
    assert state(env) == after


def test_replay_retains_original_array_order_and_requires_current_manager(files_env):
    env = files_env
    app, client, headers, ids, _ = env
    first, second = upload(env), upload(env, b"second")
    target, original = task(env)
    key, body = edit_key(env, target), edit_body(original, attachment_ids=[second, first])
    saved = edit(env, target, body, key)
    assert saved.status_code == 200
    before = state(env)
    assert_error(edit(env, target, body | {"attachment_ids": [first, second]}, key), 409, "IDEMPOTENCY_KEY_REUSED")
    assert state(env) == before
    with app.app_context(), transaction() as db:
        db.execute(delete(TaskManager).where(TaskManager.task_id == int(target), TaskManager.user_id == ids["admin1"]))
    revoked = state(env)
    assert_error(edit(env, target, body, key), 403, "FORBIDDEN")
    assert state(env) == revoked


def test_attachment_audit_is_consumed_by_real_member3_history(files_env):
    env = files_env
    first, second = upload(env), upload(env, b"second")
    target, original = task(env, [first])
    assert edit(env, target, edit_body(original, attachment_ids=[first, second])).status_code == 200
    response = env[1].get("/api/v1/results/tasks/" + target + "/activity", headers=env[2])
    assert response.status_code == 200, response.json
    updated = [item for item in response.json["data"]["items"] if item["kind"] == "updated"]
    assert len(updated) == 1
    assert "변경 항목: 첨부 자료" in updated[0]["summary"]
    assert "첨부 자료: 1개 → 2개" in updated[0]["summary"]
    assert "owned.txt" not in updated[0]["summary"]


def test_attachment_before_audit_reads_current_links_after_old_auth_snapshot(files_env):
    env = files_env
    app, _, _, _, shared = env
    first, concurrent, final = upload(env), upload(env, b"concurrent"), upload(env, b"final")
    target, original = task(env, [first])
    body, key = edit_body(original, attachment_ids=[final]), edit_key(env, target)
    authorizers = app.extensions["write_authorizers"]
    authorize, seen = authorizers["task.updated"], []
    def after_snapshot(db, requested):
        if not seen:
            before = tuple(db.scalars(select(shared.SharedLink.attachment_id).where(
                shared.SharedLink.resource_type == "task", shared.SharedLink.task_id == int(target))))
            assert before == (first,)
            with app.extensions["db_engine"].begin() as other:
                other.execute(insert(shared.SharedLink).values(attachment_id=concurrent,
                    resource_type="task", resource_id=int(target), task_id=int(target)))
            old = tuple(db.scalars(select(shared.SharedLink.attachment_id).where(
                shared.SharedLink.resource_type == "task", shared.SharedLink.task_id == int(target))))
            assert old == (first,)  # The reader truly holds an older RR snapshot.
            seen.append(True)
        return authorize(db, requested)
    authorizers["task.updated"] = after_snapshot
    response = edit(env, target, body, key)
    assert response.status_code == 200, response.json
    details, = changed_audits(env, target)
    assert details["before_attachment_ids"] == sorted([first, concurrent])
    assert details["attachment_ids"] == [final] and seen == [True]
