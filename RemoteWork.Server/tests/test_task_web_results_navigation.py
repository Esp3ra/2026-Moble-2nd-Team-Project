"""Owner 2 consumes PR52's existing web entry point, without changing results.

Only the explicit disposable MySQL runner may execute these fixtures. This
module checks navigation/current authorization, not actual result submission,
shared attachment consumption, deployment ACK or cross-PC operation.
"""
from html.parser import HTMLParser
import os
import re
from urllib.parse import parse_qs, urlsplit

import pytest

from test_task_web import _client, _snapshot, web_rows


pytestmark = pytest.mark.skipif(
    os.getenv("REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Results navigation requires the explicit owned disposable MySQL runner",
)


@pytest.fixture(autouse=True)
def navigation_flags(monkeypatch):
    monkeypatch.setenv("PYTHON_DOTENV_DISABLED", "1")
    for name in ("TASK_CREATE", "TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE",
                 "TASK_LIFECYCLE_RESULT_READS", "SHARED_ATTACHMENTS", "NOTICE", "CHAT",
                 "DEVICE_ENROLLMENT", "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "0")
    for name in ("TASK_QUERY", "COMMON_WRITE", "RESULTS"):
        monkeypatch.setenv("REMOTEWORK_ENABLE_" + name + "_DRAFT", "1")


class _Links(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.items = []
        self.current = None
        self.feed(source)

    def handle_starttag(self, tag, attributes):
        if tag == "a":
            self.current = [dict(attributes), ""]

    def handle_data(self, text):
        if self.current is not None:
            self.current[1] += text

    def handle_endtag(self, tag):
        if tag == "a" and self.current is not None:
            self.items.append(self.current)
            self.current = None


def _link(response, label):
    assert response.status_code == 200
    matches = [attributes for attributes, text in _Links(response.text).items
               if text.strip() == label]
    assert len(matches) == 1, (label, matches)
    return matches[0]["href"]


@pytest.mark.parametrize("user,task_id", [
    ("staff1", 1), ("staff1", 4), ("staff1", 5), ("staff1", 6),
    ("staff1", 7), ("admin1", 3),
])
def test_current_owner_gets_exact_results_own_navigation_without_writes(web_rows, user, task_id):
    client = _client(web_rows, user)
    before = _snapshot(web_rows[0])
    response = client.get(f"/tasks/{task_id}")
    href = _link(response, "결과물 보기·제출")
    assert href == f"/results?task_id={task_id}&view=own"
    parts = urlsplit(href)
    assert parts.scheme == parts.netloc == parts.fragment == ""
    assert parse_qs(parts.query) == {"task_id": [str(task_id)], "view": ["own"]}
    assert _snapshot(web_rows[0]) == before
    assert "task_lifecycle_integration_ack" not in web_rows[0].extensions


@pytest.mark.parametrize("user,task_id", [("admin1", 1), ("admin2", 2), ("admin2", 3)])
def test_managed_relationship_does_not_imply_result_submission_or_reviewer_navigation(web_rows, user, task_id):
    response = _client(web_rows, user).get(f"/tasks/{task_id}?scope=managed")
    assert response.status_code == 200
    assert "결과물 보기·제출" not in response.text
    assert not any(urlsplit(attributes.get("href", "")).path == "/results"
                   and "task_id" in parse_qs(urlsplit(attributes.get("href", "")).query)
                   for attributes, _ in _Links(response.text).items)


def test_cancelled_owner_does_not_get_results_action_link(web_rows):
    response = _client(web_rows).get("/tasks/9?include_cancelled=true")
    assert response.status_code == 200 and "취소됨" in response.text
    assert "결과물 보기·제출" not in response.text


def test_results_off_omits_feature_link_and_keeps_task_refresh(web_rows):
    from remotework import create_app

    app = create_app(dict(web_rows[0].config) | {"ENABLE_RESULTS_DRAFT": False})
    try:
        assert "results_web.index" not in app.view_functions
        client = _client((app, web_rows[1]))
        response = client.get("/tasks/1")
        assert response.status_code == 200 and "결과물 보기·제출" not in response.text
        assert urlsplit(_link(response, "업무 정보 새로고침")).path == "/tasks/1"
        assert client.get("/results?task_id=1&view=own").status_code == 404
    finally:
        app.extensions["db_engine"].dispose()


@pytest.mark.parametrize("scope,status,include_cancelled", [
    ("own", "in_progress", "false"), ("managed", "returned", "true"),
])
def test_refresh_is_current_detail_get_with_exact_return_filters(web_rows, scope, status, include_cancelled):
    client = _client(web_rows)
    response = client.get(f"/tasks/1?scope={scope}&status={status}&include_cancelled={include_cancelled}")
    href = _link(response, "업무 정보 새로고침")
    parts = urlsplit(href)
    assert parts.scheme == parts.netloc == parts.fragment == ""
    assert parts.path == "/tasks/1"
    assert parse_qs(parts.query) == {"scope": [scope], "status": [status],
                                   "include_cancelled": [include_cancelled]}
    assert client.get(href).status_code == 200


@pytest.mark.parametrize("user,task_id", [("staff1", 1), ("admin1", 3)])
def test_navigation_reaches_existing_results_hint_and_current_authorized_data(web_rows, user, task_id):
    client = _client(web_rows, user)
    href = _link(client.get(f"/tasks/{task_id}"), "결과물 보기·제출")
    before = _snapshot(web_rows[0])
    page = client.get(href)
    assert page.status_code == 200 and f'data-task-id="{task_id}"' in page.text
    if user == "admin1":
        assert '<option value="own" selected>' in page.text
    scope = re.search(r'data-session="([^"]+)"', page.text).group(1)
    headers = {"X-Results-Session": scope}
    listing = client.get("/results/data/tasks?scope=own", headers=headers)
    assert listing.status_code == 200
    assert str(task_id) in {item["task_id"] for item in listing.json["data"]["items"]}
    detail = client.get(f"/results/data/tasks/{task_id}", headers=headers)
    assert detail.status_code == 200 and detail.json["data"]["task_id"] == str(task_id)
    assert _snapshot(web_rows[0]) == before


def test_old_navigation_hint_does_not_bypass_reassignment_or_wrong_session(web_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskRecord

    app, ids = web_rows
    client = _client(web_rows)
    href = _link(client.get("/tasks/1"), "결과물 보기·제출")
    page = client.get(href)
    scope = re.search(r'data-session="([^"]+)"', page.text).group(1)
    with app.app_context(), transaction() as db:
        db.add(TaskAssignment(task_id=1, revision=2, assignee_id=ids["staff2"],
                              assigned_by=ids["admin1"]))
        db.get(TaskRecord, 1).current_assignment_revision = 2
    response = client.get("/results/data/tasks/1", headers={"X-Results-Session": scope})
    assert response.status_code == 403 and response.json["error"]["code"] == "FORBIDDEN"
    wrong = client.get("/results/data/tasks/1", headers={"X-Results-Session": "old-session"})
    assert wrong.status_code == 409 and wrong.json["error"]["code"] == "RESULTS_SESSION_CHANGED"
    assert client.get("/tasks/1").status_code == 403
