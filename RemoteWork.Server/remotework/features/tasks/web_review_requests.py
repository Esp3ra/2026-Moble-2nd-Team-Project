"""Cookie/CSRF HTML adapter for separate manual review messages.

Signed forms retain the original versions, body and W1 key. They are never a
grant: every prepare, execute and receipt lookup refreshes current access.
"""
import hmac
import os

from flask import current_app, make_response, redirect, render_template, request, url_for
from itsdangerous import BadData, URLSafeSerializer
from sqlalchemy.exc import SQLAlchemyError

from ...auth import authenticated_transaction, login_required
from ...common import AppError
from ...write_contract import ExpectedVersions, positive_bigint
from .web_writes import _binding, _form


REQUEST = "task.review_requested"
ACKNOWLEDGE = "task.review_acknowledged"
LABELS = {REQUEST: "검토 요청", ACKNOWLEDGE: "검토 요청 수신 확인"}


def register(state):
    if (state.app.config.get("ENABLE_COMMON_WRITE_DRAFT") is not True
            or state.app.config.get("ENABLE_RESULTS_DRAFT",
                os.getenv("REMOTEWORK_ENABLE_RESULTS_DRAFT") == "1") is not True):
        return
    state.add_url_rule("/review-requests", "review_requests", inbox, methods=["GET"])
    state.add_url_rule("/<string:task_id>/review-request", "review_request", new_request, methods=["GET", "POST"])
    state.add_url_rule("/<string:task_id>/review-requests/<string:request_id>/acknowledge",
                       "review_acknowledge", prepare_acknowledgement, methods=["POST"])
    state.add_url_rule("/review-requests/execute", "review_execute", execute, methods=["POST"])
    state.add_url_rule("/review-requests/result", "review_result", result, methods=["POST"])


def _signer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"], salt="member2-manual-review-web-v1")


def _signed(value):
    return _signer().dumps(value)


def _load(field, *, phase):
    try:
        value = _signer().loads(request.form.get(field, ""))
    except BadData:
        raise AppError(400, "INVALID_INPUT", "검토 요청 화면을 다시 열어 주세요.") from None
    fields = {"binding", "action", "target", "request_id", "body", "summary", "phase"}
    if phase != "draft":
        fields.add("key")
    if not isinstance(value, dict) or set(value) != fields:
        raise AppError(400, "INVALID_INPUT", "검토 요청 확인 내용을 확인해 주세요.")
    if not isinstance(value["binding"], str) or not hmac.compare_digest(value["binding"], _binding()):
        raise AppError(403, "FORBIDDEN", "이전 로그인에서 연 확인 화면은 사용할 수 없습니다.")
    phases = ("draft",) if phase == "draft" else ("prepared", "recovery", "retry")
    if value["action"] not in LABELS or value["phase"] not in phases:
        raise AppError(400, "INVALID_INPUT", "검토 요청 동작을 확인해 주세요.")
    positive_bigint(value["target"], "task_id")
    if value["action"] == ACKNOWLEDGE:
        positive_bigint(value["request_id"], "request_id")
    elif value["request_id"] is not None:
        raise AppError(400, "INVALID_INPUT", "새 검토 요청의 식별자를 확인해 주세요.")
    ExpectedVersions.from_body(value["body"])
    return value


def _draft(action, target, context, *, request_id=None):
    body = {"expected_row_version": context["row_version"],
            "expected_assignment_revision": context["assignment_revision"]}
    if action == REQUEST:
        body["submission_id"] = context["submission_id"]
    summary = {name: context[name] for name in ("title", "submission_id", "round_no", "reviewer_name")}
    if action == ACKNOWLEDGE:
        summary["reason"] = context["reason"]
    return {"binding": _binding(), "action": action, "target": target,
            "request_id": request_id, "body": body, "phase": "draft",
            "summary": summary}


def _authorize(db, intent):
    from .review_requests import authorize_acknowledgement, authorize_request
    callback = authorize_request if intent["action"] == REQUEST else authorize_acknowledgement
    if callback(db, intent["target"]) is not True:
        raise AppError(403, "FORBIDDEN", "현재 검토 요청을 처리할 권한이 없습니다.")
    return callback


def _prepare(db, draft, body):
    from ...write_requests import issue_key
    from .review_requests import validate_review_request_body
    validate_review_request_body(body, acknowledge=draft["action"] == ACKNOWLEDGE)
    _authorize(db, draft)
    key = issue_key(action=draft["action"], target=draft["target"])["request_key"]
    return draft | {"body": body, "key": key, "phase": "prepared"}


def _confirmation(intent, *, error=None, not_recorded=False):
    return make_response(render_template("tasks/review_confirmation.html", intent=intent,
        ticket=_signed(intent), label=LABELS[intent["action"]], error=error,
        access_lost=error is not None and error.status in (401, 403), not_recorded=not_recorded),
        error.status if error else 200)


def _recover(intent, error):
    if intent is None or error.status == 401:
        raise error
    return _confirmation(intent | {"phase": "recovery"}, error=error)


def _completed(intent, saved):
    # Only the completed transaction redirects. Refreshing the inbox repeats a
    # GET, and the URL contains neither the request key nor the submitted text.
    return redirect(url_for("task_web.review_requests",
        scope="own" if intent["action"] == REQUEST else "reviewing",
        _anchor="review-request-" + saved["request_id"]), code=303)


@login_required
def inbox():
    from .review_requests import list_review_requests
    if (set(request.args) - {"scope"}
            or any(len(values) != 1 for _, values in request.args.lists())):
        raise AppError(400, "INVALID_INPUT", "검토 요청 조회 조건을 확인해 주세요.")
    scope = request.args.get("scope", "own")
    if scope not in ("own", "reviewing"):
        raise AppError(400, "INVALID_INPUT", "검토 요청 조회 범위를 확인해 주세요.")
    with authenticated_transaction() as db:
        items = list_review_requests(db, scope)
        rows = [(item, _signed(_draft(ACKNOWLEDGE, item["task_id"], item,
                    request_id=item["request_id"])) if scope == "reviewing" and item["state"] == "pending"
                else None) for item in items]
    return render_template("tasks/review_requests.html", rows=rows, scope=scope)


@login_required
def new_request(task_id):
    from .review_requests import get_review_request_context, validate_review_request_body
    from .web import _filters, _list_args
    positive_bigint(task_id, "task_id")
    scope, status, cancelled = _filters()
    filters = _list_args(scope, status, cancelled)
    _form({"csrf_token", "draft_ticket", "reason"}, query=True)
    context = draft = None
    reason = request.form.get("reason", "")
    error = None
    try:
        with authenticated_transaction() as db:
            if request.method == "POST":
                draft = _load("draft_ticket", phase="draft")
                if draft["action"] != REQUEST or draft["target"] != task_id:
                    raise AppError(400, "INVALID_INPUT", "검토 요청 대상이 다릅니다.")
            context = get_review_request_context(db, task_id)
            if request.method == "GET":
                draft = _draft(REQUEST, task_id, context)
            else:
                body = draft["body"] | {"reason": reason}
                validate_review_request_body(body)
                ExpectedVersions.from_body(body).check(row_version=int(context["row_version"]),
                    assignment_revision=context["assignment_revision"])
                if body["submission_id"] != context["submission_id"]:
                    raise AppError(409, "RESULT_SUBMISSION_CHANGED", "제출 결과가 변경되었습니다. 다시 조회해 주세요.")
                if context["request"] and context["request"]["state"] == "pending":
                    raise AppError(409, "REVIEW_REQUEST_PENDING", "이 제출 회차에 대기 중인 검토 요청이 있습니다.")
                intent = _prepare(db, draft, body)
        if request.method == "POST":
            return _confirmation(intent)
    except AppError as exc:
        if context is None or draft is None or exc.status in (401, 403):
            raise
        error = exc
    return make_response(render_template("tasks/review_request.html", context=context,
        draft_ticket=_signed(draft), reason=reason, error=error, filters=filters), error.status if error else 200)


@login_required
def prepare_acknowledgement(task_id, request_id):
    from .review_requests import list_review_requests
    positive_bigint(task_id, "task_id")
    positive_bigint(request_id, "request_id")
    _form({"csrf_token", "draft_ticket"})
    with authenticated_transaction() as db:
        draft = _load("draft_ticket", phase="draft")
        if (draft["action"] != ACKNOWLEDGE or draft["target"] != task_id
                or draft["request_id"] != request_id):
            raise AppError(400, "INVALID_INPUT", "수신 확인 대상이 다릅니다.")
        _authorize(db, draft)
        current = next((item for item in list_review_requests(db, "reviewing")
            if item["task_id"] == task_id and item["request_id"] == request_id), None)
        if current is None:
            raise AppError(404, "REVIEW_REQUEST_NOT_FOUND", "검토 요청을 찾을 수 없습니다.")
        if current["state"] != "pending":
            raise AppError(409, "REVIEW_REQUEST_NOT_PENDING", "이미 수신 확인한 요청입니다.")
        ExpectedVersions.from_body(draft["body"]).check(row_version=int(current["row_version"]),
            assignment_revision=current["assignment_revision"])
        intent = _prepare(db, draft, draft["body"])
    return _confirmation(intent)


@login_required
def execute():
    from .review_requests import acknowledge_review_request, execute_review_request
    _form({"csrf_token", "ticket"})
    intent = None
    try:
        with authenticated_transaction() as db:
            intent = _load("ticket", phase="intent")
            if intent["phase"] not in ("prepared", "retry"):
                raise AppError(409, "RESULT_CHECK_REQUIRED", "기존 요청의 처리 결과를 먼저 확인해 주세요.")
            if intent["action"] == REQUEST:
                saved = execute_review_request(db, target=intent["target"], body=intent["body"], key=intent["key"])
            else:
                saved = acknowledge_review_request(db, target=intent["target"], request_id=intent["request_id"],
                    body=intent["body"], key=intent["key"])
        return _completed(intent, saved.data)
    except AppError as exc:
        return _recover(intent, exc)
    except SQLAlchemyError:
        return _recover(intent, AppError(503, "DATABASE_UNAVAILABLE", "저장 여부를 확인하지 못했습니다. 처리 결과를 확인해 주세요."))


@login_required
def result():
    from ...write_requests import lookup_result
    _form({"csrf_token", "ticket"})
    intent = None
    try:
        with authenticated_transaction() as db:
            intent = _load("ticket", phase="intent")
            callback = _authorize(db, intent)
            saved = lookup_result(db, key=intent["key"], action=intent["action"],
                target=intent["target"], authorize=callback)
        if saved["state"] == "completed":
            return _completed(intent, saved["result"])
        return _confirmation(intent | {"phase": "retry"}, not_recorded=True)
    except AppError as exc:
        return _recover(intent, exc)
    except SQLAlchemyError:
        return _recover(intent, AppError(503, "DATABASE_UNAVAILABLE", "처리 결과를 확인하지 못했습니다. 기존 요청을 보존해 주세요."))
