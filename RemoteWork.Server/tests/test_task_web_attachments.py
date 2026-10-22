"""Owner 2 consumes PR68 cookie attachments through signed task HTML writes.

Actual public cookie/CSRF/session adapters and shared files on a newly owned
MySQL/raw013 fixture. No SQLite, shared deployment, operational ACK or owner 3
product edits. Browser JavaScript is tested separately.
"""
import base64
from datetime import timedelta
import hashlib
from html.parser import HTMLParser
import json
import os
import re
import uuid

import pytest
from sqlalchemy import func, select
from sqlalchemy.engine import make_url
from werkzeug.datastructures import MultiDict

from test_task_web_writes import _hidden
from remotework.db import get_db, transaction
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, User, utcnow
from remotework.write_requests import WriteReceipt


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Requires the explicit disposable MySQL T3 consumer suite; no SQLite fallback")


@pytest.fixture(autouse=True)
def flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_MANAGEMENT", "RESULTS", "SHARED_ATTACHMENTS"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")
    for name in ("TASK_LIFECYCLE", "TASK_LIFECYCLE_RESULT_READS", "TASK_START", "NOTICE", "CHAT",
                 "DEVICE_ENROLLMENT", "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")


@pytest.fixture
def web_files(request, tmp_path):
    # Import only fixture metadata before conftest clears the owned raw tables.
    from remotework.features import results, shared_attachments as shared
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    assert results and TaskCancellation and TaskDeadlineExtension
    app = request.getfixturevalue("app")
    assert app.extensions["db_engine"].dialect.name == "mysql"
    assert (make_url(app.config["DATABASE_URL"]).database or "").startswith("remote_work_test_")
    assert app.extensions["results_shared_attachments"] is shared
    app.config["RESULTS_STORAGE_ROOT"] = str(tmp_path / "web-owned-files")
    now = utcnow()
    today = (now + timedelta(hours=9)).date()
    with app.app_context(), transaction() as db:
        ids = {user.login_id: user.id for user in db.scalars(select(User))}
        db.add(TaskRecord(task_id=101, title="원래 웹 첨부 업무", description="원문 설명",
            task_status="not_started", start_date=today, due_date=today + timedelta(days=3),
            current_assignment_revision=3, row_version=7, created_by=ids["admin1"],
            created_at=now, updated_at=now))
        db.flush()
        db.add(TaskAssignment(task_id=101, revision=3, assignee_id=ids["staff1"],
            assigned_by=ids["admin1"], assigned_at=now, accepted_at=None))
        db.add(TaskManager(task_id=101, user_id=ids["admin1"]))
    return app, ids, shared


def login(env, who="admin1", client=None):
    client = client or env[0].test_client()
    password = {"admin1": "Admin-test-2026!", "staff1": "Staff-test-2026!"}[who]
    response = client.post("/login", data={"csrf_token": _hidden(client.get("/login"), "csrf_token"),
        "login_id": who, "password": password})
    assert response.status_code == 302
    return client


class Panels(HTMLParser):
    def __init__(self, text):
        super().__init__()
        self.configs = []
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if "data-task-attachments" in attrs:
            self.configs.append(json.loads(attrs["data-config"]))


def config(response):
    assert response.status_code == 200, response.text[:500]
    panels = Panels(response.text).configs
    assert len(panels) == 1
    result = panels[0]
    assert re.fullmatch(r"[0-9a-f]{64}", result["session"])
    return result


def csrf(client):
    return _hidden(client.get("/tasks"), "csrf_token")


def post_shared(client, scope, path, body):
    return client.post(path, headers={"X-Results-Session": scope},
        data={"csrf_token": csrf(client), "body": json.dumps(body, ensure_ascii=False)},
        content_type="multipart/form-data")


def upload(client, scope, *, task_id=None, data=b"\xfb\xef\xbe" * 6000, complete=True):
    body = {"name": "업무 + 원본.txt", "size": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    if task_id is not None:
        body["task_id"] = str(task_id)
    response = post_shared(client, scope, "/results/data/shared/uploads/start", body)
    assert response.status_code == 201, response.json
    item = response.json["data"]
    attachment_id = item["attachment_id"]
    for offset in range(0, len(data), 16384):
        part = data[offset:offset + 16384]
        response = post_shared(client, scope, f"/results/data/shared/uploads/{attachment_id}/chunks",
            {"offset": offset, "data": base64.b64encode(part).decode("ascii")})
        assert response.status_code == 200, response.json
        assert response.json["data"]["next_offset"] == offset + len(part)
    if complete:
        response = post_shared(client, scope, f"/results/data/shared/uploads/{attachment_id}/complete", {})
        assert response.status_code == 200, response.json
        item = response.json["data"]
        assert (item["size"], item["sha256"]) == (len(data), hashlib.sha256(data).hexdigest())
    return item


def create_fields(env):
    today = (utcnow() + timedelta(hours=9)).date()
    return dict(title="웹 실제 첨부 등록", description="첨부 원문", start_date=today.isoformat(),
        due_date=(today + timedelta(days=3)).isoformat(), assignee_id=str(env[1]["staff1"]))


def prepare_new(env, client, **changes):
    fields = dict(csrf_token=csrf(client), op="prepare", **create_fields(env))
    fields.update(changes)
    return client.post("/tasks/new?scope=managed", data=fields)


def prepare_edit(env, client, *, target=101, draft=None, **changes):
    page = client.get(f"/tasks/{target}/manage/edit?scope=managed")
    with env[0].app_context():
        task = get_db().get(TaskRecord, target)
        fields = {name: getattr(task, name) for name in ("title", "description")}
        fields.update(start_date=task.start_date.isoformat(), due_date=task.due_date.isoformat())
    fields.update(csrf_token=csrf(client), op="prepare", draft_ticket=draft or _hidden(page, "draft_ticket"))
    fields.update(changes)
    return client.post(f"/tasks/{target}/manage/edit?scope=managed", data=fields)


def intent(env, response):
    from remotework.features.tasks.web_writes import _signer
    ticket = _hidden(response, "ticket")
    with env[0].app_context():
        return _signer().loads(ticket)


def send(client, ticket, endpoint="execute"):
    return client.post("/tasks/write/" + endpoint, data={"csrf_token": csrf(client), "ticket": ticket})


def snapshot(env):
    app, _, shared = env
    with app.app_context():
        db = get_db()
        tasks = tuple((t.task_id, t.title, t.row_version, t.current_assignment_revision)
            for t in db.scalars(select(TaskRecord).order_by(TaskRecord.task_id)))
        links = tuple((row.attachment_id, row.resource_type, row.resource_id) for row in db.scalars(
            select(shared.SharedLink).order_by(shared.SharedLink.attachment_id,
                shared.SharedLink.resource_type, shared.SharedLink.resource_id)))
        receipts = db.scalar(select(func.count()).select_from(WriteReceipt))
        events = db.scalar(select(func.count()).select_from(AuditEvent).where(
            AuditEvent.action.in_(("task.created", "task.updated"))))
    return tasks, links, receipts, events


def test_current_public_config_is_limited_to_create_and_edit(web_files):
    client = login(web_files)
    new = config(client.get("/tasks/new"))
    edit = config(client.get("/tasks/101/manage/edit?scope=managed"))
    assert (new["mode"], new["taskId"]) == ("create", None)
    assert (edit["mode"], edit["taskId"]) == ("edit", "101")
    assert new["session"] == edit["session"]
    assert not Panels(client.get("/tasks/101/manage/reassign?scope=managed").text).configs
    assert not Panels(client.get("/tasks/101/manage/managers?scope=managed").text).configs


def test_actual_cookie_upload_prepares_frozen_ids_then_creates_once_and_downloads(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    first = upload(client, scope)
    second = upload(client, scope, data=b"second actual shared file")
    selected = [second["attachment_id"], first["attachment_id"]]
    before = snapshot(web_files)
    response = prepare_new(web_files, client, attachment_ids=json.dumps(selected), attachment_scope=scope)
    assert response.status_code == 200
    frozen = intent(web_files, response)
    assert frozen["body"]["attachment_ids"] == selected
    assert "attachment_scope" not in frozen["body"]
    assert snapshot(web_files) == before
    ticket = _hidden(response, "ticket")
    saved = send(client, ticket)
    assert saved.status_code == 200, saved.text[:500]
    after = snapshot(web_files)
    assert len(after[0]) == len(before[0]) + 1
    assert (after[2] - before[2], after[3] - before[3]) == (1, 1)
    assert send(client, ticket).status_code == 200
    assert snapshot(web_files) == after
    target = str(max(row[0] for row in after[0]))
    listing = client.get(f"/results/data/shared/tasks/{target}", headers={"X-Results-Session": scope})
    assert {row["attachment_id"] for row in listing.json["data"]["items"]} == set(selected)
    content = bytearray()
    while True:
        response = client.get(f"/results/data/shared/tasks/{target}/files/{first['attachment_id']}/chunks?offset={len(content)}",
            headers={"X-Results-Session": scope})
        assert response.status_code == 200, response.json
        part = response.json["data"]
        assert part["offset"] == len(content)
        content.extend(base64.b64decode(part["data"], validate=True))
        assert part["next_offset"] == len(content)
        if part["eof"]:
            break
    assert len(content) == first["size"] and hashlib.sha256(content).hexdigest() == first["sha256"]


def test_search_round_trip_keeps_explicit_upload_ids_without_saving(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    attachment_id = upload(client, scope)["attachment_id"]
    before = snapshot(web_files)
    response = prepare_new(web_files, client, op="search", attachment_ids=json.dumps([attachment_id]), attachment_scope=scope)
    parsed = config(response)
    assert parsed["ids"] == [attachment_id] and parsed["explicit"] is True
    assert snapshot(web_files) == before


def test_general_edit_attachment_only_change_then_omission_preserves_and_empty_unlinks(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/101/manage/edit?scope=managed"))["session"]
    attachment_id = upload(client, scope, task_id=101)["attachment_id"]
    before = snapshot(web_files)
    prepared = prepare_edit(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=scope)
    assert intent(web_files, prepared)["body"]["attachment_ids"] == [attachment_id]
    assert snapshot(web_files) == before
    saved = send(client, _hidden(prepared, "ticket"))
    assert saved.status_code == 200, saved.text[:500]
    attached = snapshot(web_files)
    assert attached[0][0][2:] == (8, 3)
    assert (attachment_id, "task", 101) in attached[1]
    same = prepare_edit(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=scope)
    same_result = send(client, _hidden(same, "ticket"))
    assert same_result.status_code == 409 and "TASK_CHANGE_NOT_NEEDED" in same_result.text
    assert snapshot(web_files) == attached
    title_only = prepare_edit(web_files, client, title="첨부 유지 본문 변경")
    assert "attachment_ids" not in intent(web_files, title_only)["body"]
    assert send(client, _hidden(title_only, "ticket")).status_code == 200
    preserved = snapshot(web_files)
    assert preserved[1] == attached[1] and preserved[0][0][2:] == (9, 3)
    empty = prepare_edit(web_files, client, attachment_ids="[]", attachment_scope=scope)
    assert intent(web_files, empty)["body"]["attachment_ids"] == []
    assert send(client, _hidden(empty, "ticket")).status_code == 200
    removed = snapshot(web_files)
    assert removed[1] == () and removed[0][0][2:] == (10, 3)


@pytest.mark.parametrize("raw", ["null", "{}", '"file"', "[1]", '["NOT-A-UUID"]', "[NaN]",
    json.dumps([str(uuid.UUID(int=1))] * 2), json.dumps([str(uuid.UUID(int=i)) for i in range(1, 12)])])
def test_bad_json_selection_never_creates_write_or_changes_files(web_files, raw):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    before = snapshot(web_files)
    response = prepare_new(web_files, client, attachment_ids=raw, attachment_scope=scope)
    assert response.status_code == 400
    assert snapshot(web_files) == before


@pytest.mark.parametrize("scope", [None, "", "0" * 64, "not-session"])
def test_selected_ids_require_current_public_page_session(web_files, scope):
    client = login(web_files)
    current = config(client.get("/tasks/new"))["session"]
    attachment_id = upload(client, current)["attachment_id"]
    values = {"attachment_ids": json.dumps([attachment_id])}
    if scope is not None:
        values["attachment_scope"] = scope
    before = snapshot(web_files)
    response = prepare_new(web_files, client, **values)
    assert response.status_code in (400, 409)
    assert snapshot(web_files) == before


def test_same_user_relogin_rejects_old_scope_and_old_signed_attachment_intent(web_files):
    client = login(web_files)
    old_scope = config(client.get("/tasks/new"))["session"]
    attachment_id = upload(client, old_scope)["attachment_id"]
    prepared = prepare_new(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=old_scope)
    ticket = _hidden(prepared, "ticket")
    before = snapshot(web_files)
    response = client.post("/logout", data={"csrf_token": csrf(client)})
    assert response.status_code == 302
    login(web_files, client=client)
    new_scope = config(client.get("/tasks/new"))["session"]
    assert new_scope != old_scope
    response = prepare_new(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=old_scope)
    assert response.status_code in (400, 409)
    scope_only = prepare_new(web_files, client, attachment_scope=old_scope)
    assert scope_only.status_code == 409
    assert send(client, ticket).status_code == 403
    assert snapshot(web_files) == before


def test_tampered_signed_ids_do_not_save_a_different_file(web_files):
    from remotework.features.tasks.web_writes import _signer
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    attachment_id = upload(client, scope)["attachment_id"]
    prepared = prepare_new(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=scope)
    ticket = _hidden(prepared, "ticket")
    before = snapshot(web_files)
    # Keep the original signature while replacing the serialized payload.
    with web_files[0].app_context():
        body = _signer().loads(ticket)
        body["body"]["attachment_ids"] = []
        modified = _signer().dumps(body)
    tampered = modified.rsplit(".", 1)[0] + "." + ticket.rsplit(".", 1)[1]
    response = send(client, tampered)
    assert response.status_code == 400
    assert snapshot(web_files) == before


@pytest.mark.parametrize("name", ["attachment_ids", "attachment_scope", "attachment_display"])
def test_duplicate_attachment_form_fields_are_rejected(web_files, name):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    fields = dict(csrf_token=csrf(client), op="prepare", **create_fields(web_files),
        attachment_ids="[]", attachment_scope=scope,
        attachment_display=json.dumps({"mode": "create", "task_id": None, "items": []}))
    body = MultiDict(fields)
    body.add(name, fields[name])
    before = snapshot(web_files)
    response = client.post("/tasks/new?scope=managed", data=body)
    assert response.status_code == 400
    assert snapshot(web_files) == before


def test_provider_removed_hides_transport_and_preserves_attachmentless_registration(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    web_files[0].extensions.pop("results_task_web")
    assert not Panels(client.get("/tasks/new").text).configs
    before = snapshot(web_files)
    explicit = prepare_new(web_files, client, attachment_ids=json.dumps([str(uuid.UUID(int=1))]), attachment_scope=scope)
    assert explicit.status_code == 503
    assert snapshot(web_files) == before
    omitted = prepare_new(web_files, client)
    assert omitted.status_code == 200 and "attachment_ids" not in intent(web_files, omitted)["body"]
    assert send(client, _hidden(omitted, "ticket")).status_code == 200


def test_incomplete_upload_keeps_original_signed_ids_and_files_on_failed_save(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    attachment_id = upload(client, scope, complete=False)["attachment_id"]
    prepared = prepare_new(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=scope)
    before = snapshot(web_files)
    response = send(client, _hidden(prepared, "ticket"))
    assert response.status_code == 409
    assert intent(web_files, response)["body"]["attachment_ids"] == [attachment_id]
    assert intent(web_files, response)["key"] == intent(web_files, prepared)["key"]
    assert snapshot(web_files) == before
    # The explicit result check does not upload, delete, or silently save.
    checked = send(client, _hidden(response, "ticket"), endpoint="result")
    assert checked.status_code == 200
    assert intent(web_files, checked)["body"]["attachment_ids"] == [attachment_id]
    assert snapshot(web_files) == before


def test_saved_attachment_result_survives_current_provider_outage(web_files, monkeypatch):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    attachment_id = upload(client, scope)["attachment_id"]
    prepared = prepare_new(web_files, client, attachment_ids=json.dumps([attachment_id]), attachment_scope=scope)
    ticket = _hidden(prepared, "ticket")
    assert send(client, ticket).status_code == 200
    after = snapshot(web_files)
    web_files[0].extensions.pop("results_shared_attachments")
    web_files[0].extensions.pop("results_task_web")
    assert send(client, ticket, endpoint="result").status_code == 200
    assert send(client, ticket).status_code == 200
    assert snapshot(web_files) == after


def display_hint(item, *, mode="create", task_id=None, **changes):
    record = dict(item, temporary=True)
    record.update(changes)
    return json.dumps(dict(mode=mode, task_id=task_id, items=[record]), ensure_ascii=False)


@pytest.mark.parametrize("mode", ["create", "edit"])
@pytest.mark.parametrize("operation", ["search", "invalid_prepare"])
def test_form_rerender_restores_completed_upload_display_without_saving(web_files, mode, operation):
    client = login(web_files)
    path = "/tasks/new" if mode == "create" else "/tasks/101/manage/edit?scope=managed"
    scope = config(client.get(path))["session"]
    uploaded = upload(client, scope, task_id=None if mode == "create" else 101)
    before = snapshot(web_files)
    changes = dict(attachment_scope=scope, attachment_ids=json.dumps([uploaded["attachment_id"]]),
        attachment_display=display_hint(uploaded, mode=mode, task_id=None if mode == "create" else "101"))
    changes["op"] = "search" if operation == "search" else "prepare"
    if operation == "invalid_prepare":
        changes["title"] = ""
    response = (prepare_new if mode == "create" else prepare_edit)(web_files, client, **changes)
    assert response.status_code == (200 if operation == "search" else 400), response.text[:500]
    panels = Panels(response.text).configs
    assert len(panels) == 1
    assert panels[0]["ids"] == [uploaded["attachment_id"]]
    assert panels[0]["explicit"] is True
    assert panels[0]["display"] == [dict(uploaded, temporary=True)]
    assert json.loads(_hidden(response, "attachment_display")) == dict(
        mode=mode, task_id=None if mode == "create" else "101", items=[dict(uploaded, temporary=True)])
    assert snapshot(web_files) == before, "display recovery must not save a task, link, receipt or audit event"


@pytest.mark.parametrize("mode", ["create", "edit"])
def test_display_hints_are_excluded_from_signed_write_body_and_summary(web_files, mode):
    client = login(web_files)
    path = "/tasks/new" if mode == "create" else "/tasks/101/manage/edit?scope=managed"
    scope = config(client.get(path))["session"]
    uploaded = upload(client, scope, task_id=None if mode == "create" else 101)
    before = snapshot(web_files)
    prepared = (prepare_new if mode == "create" else prepare_edit)(web_files, client,
        attachment_scope=scope, attachment_ids=json.dumps([uploaded["attachment_id"]]),
        attachment_display=display_hint(uploaded, mode=mode, task_id=None if mode == "create" else "101"))
    assert prepared.status_code == 200, prepared.text[:500]
    frozen = intent(web_files, prepared)
    assert frozen["body"]["attachment_ids"] == [uploaded["attachment_id"]]
    encoded = json.dumps(frozen, ensure_ascii=False)
    for presentation in ("attachment_display", "temporary", uploaded["name"], uploaded["sha256"]):
        assert presentation not in encoded
    assert snapshot(web_files) == before
    assert send(client, _hidden(prepared, "ticket")).status_code == 200


@pytest.mark.parametrize("mode", ["create", "edit"])
def test_cross_page_display_and_old_scope_cannot_restore_or_create_writes(web_files, mode):
    client = login(web_files)
    path = "/tasks/new" if mode == "create" else "/tasks/101/manage/edit?scope=managed"
    scope = config(client.get(path))["session"]
    uploaded = upload(client, scope, task_id=None if mode == "create" else 101)
    before = snapshot(web_files)
    invoke = prepare_new if mode == "create" else prepare_edit
    mismatch = display_hint(uploaded, mode="edit", task_id="202")
    response = invoke(web_files, client, attachment_scope=scope,
        attachment_ids=json.dumps([uploaded["attachment_id"]]), attachment_display=mismatch)
    assert response.status_code == 400
    old_scope = invoke(web_files, client, attachment_scope="0" * 64,
        attachment_ids=json.dumps([uploaded["attachment_id"]]),
        attachment_display=display_hint(uploaded, mode=mode, task_id=None if mode == "create" else "101"))
    assert old_scope.status_code == 409
    assert snapshot(web_files) == before


def test_restored_temporary_hint_cannot_delete_another_users_upload(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    uploaded = upload(client, scope)
    # A UI hint is not authority, even when copied to another authenticated page.
    other = login(web_files, "staff1")
    other_scope = other.get("/results").headers["X-Results-Session"]
    before = snapshot(web_files)
    response = post_shared(other, other_scope,
        f"/results/data/shared/uploads/{uploaded['attachment_id']}/discard", {})
    assert response.status_code == 403, response.json
    assert snapshot(web_files) == before
    with web_files[0].app_context():
        stored = get_db().get(web_files[2].SharedFile, uploaded["attachment_id"])
        assert stored.state == "ready" and stored.owner_id == web_files[1]["admin1"]


def test_restored_temporary_hint_cannot_delete_a_now_linked_file(web_files):
    client = login(web_files)
    scope = config(client.get("/tasks/new"))["session"]
    uploaded = upload(client, scope)
    prepared = prepare_new(web_files, client, attachment_scope=scope,
        attachment_ids=json.dumps([uploaded["attachment_id"]]), attachment_display=display_hint(uploaded))
    assert send(client, _hidden(prepared, "ticket")).status_code == 200
    before = snapshot(web_files)
    response = post_shared(client, scope,
        f"/results/data/shared/uploads/{uploaded['attachment_id']}/discard", {})
    assert response.status_code == 403, response.json
    assert snapshot(web_files) == before
    with web_files[0].app_context():
        assert get_db().get(web_files[2].SharedFile, uploaded["attachment_id"]).state == "ready"
