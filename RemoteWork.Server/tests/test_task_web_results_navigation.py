"""Owner 2 consumes PR68's signed own/read navigation, without changing results.

Only the explicit disposable MySQL runner may execute these fixtures. This
module checks navigation/current authorization, not actual result submission,
shared attachment consumption, deployment ACK or cross-PC operation.
"""
from html.parser import HTMLParser
import os
import re
from urllib.parse import parse_qs, urlencode, urlsplit

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
    parts = urlsplit(href)
    assert parts.scheme == parts.netloc == parts.fragment == ""
    values = parse_qs(parts.query)
    assert set(values) == {"task_id", "view", "back_context"}
    assert values["task_id"] == [str(task_id)] and values["view"] == ["own"]
    assert len(values["back_context"]) == 1 and values["back_context"][0]
    assert _snapshot(web_rows[0]) == before
    assert "task_lifecycle_integration_ack" not in web_rows[0].extensions


@pytest.mark.parametrize("user,task_id", [("admin1", 1), ("admin2", 2), ("admin2", 3)])
def test_managed_relationship_does_not_imply_result_submission_or_reviewer_navigation(web_rows, user, task_id):
    client = _client(web_rows, user)
    response = client.get(f"/tasks/{task_id}?scope=managed")
    assert response.status_code == 200
    assert "결과물 보기·제출" not in response.text
    href = _link(response, "결과물 읽기 전용 보기")
    values = parse_qs(urlsplit(href).query)
    assert values["view"] == ["read"] and values["task_id"] == [str(task_id)]
    assert values["back_context"]
    before = _snapshot(web_rows[0])
    page = client.get(href)
    assert page.status_code == 200 and 'data-view="read"' in page.text
    for action in ("submit-open", "rework-start", "approve-open", "return-open",
                   "submission-dialog", "decision-dialog", "upload-files"):
        assert f'id="{action}"' not in page.text
    assert _snapshot(web_rows[0]) == before


def test_cancelled_owner_does_not_get_results_action_link(web_rows):
    response = _client(web_rows).get("/tasks/9?include_cancelled=true")
    assert response.status_code == 200 and "취소됨" in response.text
    assert "결과물 보기·제출" not in response.text
    response = _client(web_rows, "admin1").get("/tasks/9?scope=managed&include_cancelled=true")
    assert response.status_code == 200 and "결과물 읽기 전용 보기" not in response.text


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


@pytest.mark.parametrize("user,task_id,scope,status,include_cancelled", [
    ("staff1", 1, "own", "in_progress", "false"),
    ("admin1", 3, "own", "submitted", "true"),
    ("admin1", 5, "managed", "returned", "true"),
    ("admin2", 2, "managed", None, "false"),
])
def test_signed_return_keeps_original_task_filters_and_reads_current_detail(web_rows, user, task_id, scope, status, include_cancelled):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord

    client = _client(web_rows, user)
    filters = {"scope": scope, "include_cancelled": include_cancelled}
    if status is not None:
        filters["status"] = status
    label = "결과물 보기·제출" if scope == "own" else "결과물 읽기 전용 보기"
    href = _link(client.get(f"/tasks/{task_id}?" + urlencode(filters)), label)
    page = client.get(href)
    back = _link(page, "원래 업무로 돌아가 최신 정보 조회")
    with web_rows[0].app_context(), transaction() as db:
        db.get(TaskRecord, task_id).title = "복귀 시 새로 조회한 업무 제목"
    before = _snapshot(web_rows[0])
    response = client.get(back)
    assert response.status_code == 302
    destination = urlsplit(response.headers["Location"])
    assert destination.scheme == destination.netloc == destination.fragment == ""
    assert destination.path == f"/tasks/{task_id}"
    assert parse_qs(destination.query) == {name: [value] for name, value in filters.items()}
    fresh = client.get(response.headers["Location"])
    assert fresh.status_code == 200 and "복귀 시 새로 조회한 업무 제목" in fresh.text
    assert _snapshot(web_rows[0]) == before


def test_multiple_tabs_keep_independent_task_and_filter_return_contexts(web_rows):
    client = _client(web_rows)
    first = _link(client.get("/tasks/1?scope=own&status=in_progress&include_cancelled=true"), "결과물 보기·제출")
    second = _link(client.get("/tasks/5?scope=own&status=returned&include_cancelled=false"), "결과물 보기·제출")
    first_back = _link(client.get(first), "원래 업무로 돌아가 최신 정보 조회")
    second_back = _link(client.get(second), "원래 업무로 돌아가 최신 정보 조회")
    assert first_back != second_back
    for link, target, status, cancelled in ((second_back, "5", "returned", "false"),
                                           (first_back, "1", "in_progress", "true")):
        response = client.get(link)
        assert response.status_code == 302
        destination = urlsplit(response.headers["Location"])
        assert destination.path == "/tasks/" + target
        assert parse_qs(destination.query) == {"scope": ["own"], "status": [status],
                                               "include_cancelled": [cancelled]}


@pytest.mark.parametrize("kind", ["task", "signature", "other-user", "same-user-new-session"])
def test_signed_task_navigation_rejects_substitution_and_another_login(web_rows, kind):
    client = _client(web_rows)
    href = _link(client.get("/tasks/1"), "결과물 보기·제출")
    values = parse_qs(urlsplit(href).query)
    if kind == "task":
        values["task_id"] = ["5"]
    elif kind == "signature":
        token = values["back_context"][0]
        values["back_context"] = [token[:-8] + ("A" if token[-8] != "A" else "B") + token[-7:]]
    elif kind == "other-user":
        client = _client(web_rows, "admin1")
    else:
        client = _client(web_rows, "staff1")
    rejected = client.get("/results?" + urlencode(values, doseq=True))
    assert rejected.status_code == 400
    assert "복귀 정보가 유효하지 않습니다" in rejected.text


def test_expired_login_does_not_reuse_a_signed_navigation(web_rows):
    from datetime import timedelta
    from sqlalchemy import select
    from remotework.db import transaction
    from remotework.models import AuthSession, utcnow

    client = _client(web_rows)
    href = _link(client.get("/tasks/1"), "결과물 보기·제출")
    with web_rows[0].app_context(), transaction() as db:
        for login in db.scalars(select(AuthSession).where(AuthSession.user_id == web_rows[1]["staff1"])):
            login.expires_at = utcnow() - timedelta(seconds=1)
    response = client.get(href)
    assert response.status_code == 302 and response.headers["Location"].startswith("/login")


def test_reassigned_task_return_rechecks_access_and_preserves_save_message(web_rows):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment, TaskRecord

    client = _client(web_rows)
    href = _link(client.get("/tasks/1"), "결과물 보기·제출")
    back = _link(client.get(href), "원래 업무로 돌아가 최신 정보 조회")
    with web_rows[0].app_context(), transaction() as db:
        db.add(TaskAssignment(task_id=1, revision=2, assignee_id=web_rows[1]["staff2"],
                              assigned_by=web_rows[1]["admin1"]))
        db.get(TaskRecord, 1).current_assignment_revision = 2
    before = _snapshot(web_rows[0])
    denied = client.get(back)
    assert denied.status_code == 403
    assert "이미 확인한 결과물 저장 성공은 유지됩니다" in denied.text
    assert _snapshot(web_rows[0]) == before


@pytest.mark.parametrize("user,task_id,scope", [("staff1", 1, "managed"), ("admin1", 3, "managed"),
                                               ("admin1", 1, "own")])
def test_unrelated_return_scope_never_grants_result_navigation(web_rows, user, task_id, scope):
    response = _client(web_rows, user).get(f"/tasks/{task_id}?scope={scope}")
    assert response.status_code == 200
    assert "결과물 보기·제출" not in response.text and "결과물 읽기 전용 보기" not in response.text


def test_provider_unavailability_is_not_hidden_as_empty_success(web_rows, monkeypatch):
    from remotework.common import AppError

    def unavailable(*args, **kwargs):
        raise AppError(503, "DATABASE_UNAVAILABLE", "결과물 연결을 확인해 주세요.")
    monkeypatch.setitem(web_rows[0].extensions["results_task_web"], "build_url", unavailable)
    response = _client(web_rows).get("/tasks/1")
    assert response.status_code == 503 and "결과물 연결을 확인해 주세요" in response.text
