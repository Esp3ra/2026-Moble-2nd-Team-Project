"""Real Flask cookie/CSRF review-request pages on the owned MySQL fixture.

The fixture creates actual tasks and submissions through their existing APIs.
This is local integration evidence, not shared-server/browser acceptance.
"""
from html import unescape
import os
import re

import pytest
from sqlalchemy import select
from sqlalchemy.exc import SQLAlchemyError
from werkzeug.datastructures import MultiDict

from test_task_review_requests import (
    PASSWORD, create_submitted, decide, get, own_counts, review_env, source_snapshot,
)
from test_task_web_writes import _hidden


pytestmark = pytest.mark.skipif(os.getenv("REMOTEWORK_RUN_TASK_REVIEW_REQUESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
    reason="Requires explicit owned MySQL review-request runner; no SQLite fallback")
REASON = "  계산 근거 <script>검토</script> & 추가 설명 😀\n원문 유지  "


def _client(env, actor="worker"):
    client = env.app.test_client()
    response = client.post("/login", data={"csrf_token": _hidden(client.get("/login"), "csrf_token"),
        "login_id": actor, "password": PASSWORD})
    assert response.status_code == 302, response.text
    return client


def _csrf(client):
    with client.session_transaction() as session:
        return session["csrf_token"]


def _link(response, label):
    match = re.search(r'<a\b[^>]*href="([^"]+)"[^>]*>' + re.escape(label) + r'</a>', response.text)
    assert match, (label, response.text)
    return unescape(match.group(1))


def _prepare(client, target, *, reason=REASON, draft=None):
    url = f"/tasks/{target}/review-request"
    if draft is None:
        page = client.get(url)
        assert page.status_code == 200, page.text
        draft = _hidden(page, "draft_ticket")
    return client.post(url, data={"csrf_token": _csrf(client), "draft_ticket": draft, "reason": reason})


def _send(client, ticket, endpoint="execute"):
    return client.post("/tasks/review-requests/" + endpoint,
        data={"csrf_token": _csrf(client), "ticket": ticket})


def _intent(env, ticket):
    from remotework.features.tasks.web_review_requests import _signer
    with env.app.app_context():
        return _signer().loads(ticket)


def test_web_linked_request_acknowledgement_prg_and_receipt_replay(review_env):
    env = review_env
    target, submission = create_submitted(env)
    owner, reviewer = _client(env), _client(env, "reviewer")
    original = source_snapshot(env)
    counts = own_counts(env)
    index = owner.get("/tasks")
    assert owner.get(_link(index, "검토 요청함 →")).status_code == 200
    detail = owner.get(f"/tasks/{target}")
    page = owner.get(_link(detail, "검토 요청 작성"))
    assert page.status_code == 200 and "Submitted original result" in page.text
    assert "TEST-reviewer" in page.text and "지정 검토자" in page.text
    prepared = _prepare(owner, target, draft=_hidden(page, "draft_ticket"))
    assert prepared.status_code == 200, prepared.text
    assert "<script>검토</script>" not in prepared.text and "&lt;script&gt;검토&lt;/script&gt;" in prepared.text
    ticket = _hidden(prepared, "ticket")
    intent = _intent(env, ticket)
    assert intent["body"]["reason"] == REASON and intent["body"]["submission_id"] == submission
    assert own_counts(env) == counts and source_snapshot(env) == original
    response = _send(owner, ticket)
    assert response.status_code == 303, response.text
    assert "scope=own" in response.location and ticket not in response.location
    assert "reason" not in response.location and intent["key"] not in response.location
    inbox = owner.get(response.location)
    assert inbox.status_code == 200 and "&lt;script&gt;검토&lt;/script&gt;" in inbox.text
    assert 'name="draft_ticket"' not in inbox.text
    assert owner.get(_link(inbox, "결과물 보기")).status_code == 200
    after = own_counts(env)
    assert after == tuple(value + 1 for value in counts)
    assert _send(owner, ticket).status_code == 303
    assert _send(owner, ticket, "result").status_code == 303
    assert own_counts(env) == after
    pending = owner.get(f"/tasks/{target}/review-request")
    assert pending.status_code == 200 and "대기 중인 검토 요청" in pending.text
    assert 'name="reason"' not in pending.text
    received = reviewer.get("/tasks/review-requests?scope=reviewing")
    assert received.status_code == 200 and "수신 확인은" in received.text
    assert reviewer.get(_link(received, "결과물 검토 화면 열기")).status_code == 200
    request_id = get(env, "tasks/review-requests?scope=own")["items"][0]["request_id"]
    ack = reviewer.post(f"/tasks/{target}/review-requests/{request_id}/acknowledge",
        data={"csrf_token": _csrf(reviewer), "draft_ticket": _hidden(received, "draft_ticket")})
    assert ack.status_code == 200 and "결과 승인·반려와 업무 상태 변경은 수행하지 않습니다" in ack.text
    ack_ticket = _hidden(ack, "ticket")
    assert own_counts(env) == after
    response = _send(reviewer, ack_ticket)
    assert response.status_code == 303 and "scope=reviewing" in response.location
    acknowledged = reviewer.get(response.location)
    assert "수신 확인됨" in acknowledged.text and 'name="draft_ticket"' not in acknowledged.text
    after_ack = own_counts(env)
    assert after_ack == (after[0], after[1] + 1, after[2] + 1)
    assert _send(reviewer, ack_ticket).status_code == 303
    assert _send(reviewer, ack_ticket, "result").status_code == 303
    assert source_snapshot(env) == original and own_counts(env) == after_ack
    decide(env, target, submission)
    resolved = reviewer.get("/tasks/review-requests?scope=reviewing")
    assert "종료 (현재 검토 대상 아님)" in resolved.text and 'name="draft_ticket"' not in resolved.text


def test_web_strict_inputs_csrf_binding_and_current_relation(review_env):
    env = review_env
    target, _ = create_submitted(env)
    owner, second = _client(env), _client(env)
    foreign, manager = _client(env, "other"), _client(env, "manager")
    url = f"/tasks/{target}/review-request"
    draft = _hidden(owner.get(url), "draft_ticket")
    data = {"csrf_token": _csrf(owner), "draft_ticket": draft, "reason": REASON}
    counts, original = own_counts(env), source_snapshot(env)
    assert owner.post(url, data=data | {"csrf_token": "wrong"}).status_code == 400
    assert owner.post(url, data=data | {"unexpected": "bad"}).status_code == 400
    assert owner.post(url, data=data | {"draft_ticket": draft + "bad"}).status_code == 400
    duplicates = MultiDict(list(data.items()) + [("reason", "second value")])
    assert owner.post(url, data=duplicates).status_code == 400
    assert _prepare(second, target, draft=draft).status_code == 403
    assert foreign.get(url).status_code == 403 and manager.get(url).status_code == 403
    assert owner.get("/tasks/review-requests?scope=reviewing").status_code == 403
    assert owner.get("/tasks/review-requests?scope=own&scope=reviewing").status_code == 400
    assert owner.get("/tasks/review-requests?scope=managed").status_code == 400
    assert 'data-review-request-id=' not in manager.get("/tasks/review-requests?scope=reviewing").text
    assert own_counts(env) == counts and source_snapshot(env) == original


@pytest.mark.parametrize("reason", ["   ", "x" * 4001], ids=["whitespace", "too_long"])
def test_web_invalid_reason_keeps_original_draft_and_issues_no_write(review_env, reason):
    env = review_env
    target, _ = create_submitted(env)
    owner = _client(env)
    draft = _hidden(owner.get(f"/tasks/{target}/review-request"), "draft_ticket")
    counts = own_counts(env)
    response = _prepare(owner, target, draft=draft, reason=reason)
    assert response.status_code == 400 and _hidden(response, "draft_ticket") == draft
    assert _hidden(response, "reason") == reason
    assert 'name="ticket"' not in response.text and own_counts(env) == counts


def test_web_stale_versions_preserve_draft_text(review_env):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskRecord
    env = review_env
    target, _ = create_submitted(env)
    owner = _client(env)
    draft = _hidden(owner.get(f"/tasks/{target}/review-request"), "draft_ticket")
    with env.app.app_context(), transaction() as db:
        db.get(TaskRecord, int(target)).row_version += 1
    counts = own_counts(env)
    response = _prepare(owner, target, draft=draft)
    assert response.status_code == 409 and "TASK_VERSION_CONFLICT" in response.text
    assert _hidden(response, "draft_ticket") == draft and _hidden(response, "reason") == REASON
    assert own_counts(env) == counts


def test_web_duplicate_tab_pending_recovery_keeps_exact_key_body(review_env):
    env = review_env
    target, _ = create_submitted(env)
    owner = _client(env)
    first = _hidden(_prepare(owner, target), "ticket")
    second = _hidden(_prepare(owner, target, reason="다른 창에서 작성한 원문"), "ticket")
    original = _intent(env, second)
    assert _send(owner, first).status_code == 303
    counts = own_counts(env)
    response = _send(owner, second)
    assert response.status_code == 409 and "REVIEW_REQUEST_PENDING" in response.text
    recovery = _hidden(response, "ticket")
    frozen = _intent(env, recovery)
    assert frozen["phase"] == "recovery" and frozen["key"] == original["key"]
    assert frozen["body"] == original["body"]
    assert 'action="/tasks/review-requests/execute"' not in response.text
    assert _send(owner, recovery).status_code == 409
    result = _send(owner, recovery, "result")
    assert result.status_code == 200 and "저장된 처리 결과가 없습니다" in result.text
    retry = _hidden(result, "ticket")
    assert _intent(env, retry)["key"] == original["key"]
    assert _send(owner, retry).status_code == 409 and own_counts(env) == counts


def test_web_transaction_failure_then_receipt_checked_retry(review_env, monkeypatch):
    from remotework.features.tasks import review_requests
    env = review_env
    target, _ = create_submitted(env)
    owner = _client(env)
    ticket = _hidden(_prepare(owner, target), "ticket")
    counts, original = own_counts(env), source_snapshot(env)
    actual = review_requests.execute_review_request

    def failed(db, **kwargs):
        actual(db, **kwargs)
        raise SQLAlchemyError("owned test rollback before commit")

    monkeypatch.setattr(review_requests, "execute_review_request", failed)
    response = _send(owner, ticket)
    assert response.status_code == 503 and "DATABASE_UNAVAILABLE" in response.text
    recovery = _hidden(response, "ticket")
    assert own_counts(env) == counts and source_snapshot(env) == original
    monkeypatch.setattr(review_requests, "execute_review_request", actual)
    checked = _send(owner, recovery, "result")
    assert checked.status_code == 200 and "저장된 처리 결과가 없습니다" in checked.text
    retry = _hidden(checked, "ticket")
    for field in ("key", "body"):
        assert _intent(env, retry)[field] == _intent(env, ticket)[field]
    assert _send(owner, retry).status_code == 303
    assert source_snapshot(env) == original


def test_web_current_assignment_loss_blocks_execute_and_receipt_lookup(review_env):
    from remotework.db import transaction
    from remotework.features.tasks.models import TaskAssignment
    env = review_env
    target, _ = create_submitted(env)
    owner = _client(env)
    ticket = _hidden(_prepare(owner, target), "ticket")
    with env.app.app_context(), transaction() as db:
        db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == int(target))).assignee_id = env.ids["other"]
    counts = own_counts(env)
    response = _send(owner, ticket)
    assert response.status_code == 403 and "Manual review target" not in response.text
    recovery = _hidden(response, "ticket")
    assert _send(owner, recovery, "result").status_code == 403
    assert own_counts(env) == counts
