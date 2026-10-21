"""Member 2 HTML preparation for the approved cancellation/extension policy.

Drafts freeze the current login, original versions and selected extension. The
common confirmation adapter owns W1 keys; these pages never enable deployment
ACKs, create results, replace reviewers, or call another owner's HTTP endpoint.
"""
import hmac

from flask import current_app, g, make_response, render_template, request, url_for
from itsdangerous import BadData, URLSafeSerializer

from ...auth import authenticated_transaction, login_required
from ...common import AppError
from ...write_contract import ExpectedVersions, positive_bigint
from .lifecycle import (_current, _lifecycle_context_data, prepare_lifecycle,
                        validate_lifecycle_body)
from .web_writes import _binding, _form, _page, _prepare


ACTIONS = {"cancel": "task.cancelled", "request-extension": "task.deadline_extension_requested",
    "approve-extension": "task.deadline_extension_approved",
    "reject-extension": "task.deadline_extension_rejected",
    "withdraw-extension": "task.deadline_extension_withdrawn"}
LABELS = {"cancel": "업무 취소", "request-extension": "마감 연장 신청",
    "approve-extension": "마감 연장 승인", "reject-extension": "마감 연장 반려",
    "withdraw-extension": "마감 연장 철회"}
FIELDS = {"cancel": {"reason"}, "request-extension": {"reason", "requested_due_date"},
    "approve-extension": set(), "reject-extension": {"reason"}, "withdraw-extension": set()}


def available_actions(context, actor):
    """UI hints only; current permissions and policy are checked at execution."""
    if context["is_cancelled"]:
        return []
    own = context["assignee_id"] == actor.id
    managed = actor.role == "admin" and actor.id in context["manager_user_ids"]
    status, pending = context["task_status"], context["pending_extension"]
    actions = []
    if managed and status in ("not_started", "in_progress", "returned"):
        actions.append("cancel")
    if status in ("in_progress", "returned"):
        if pending is None:
            if own:
                actions.append("request-extension")
        elif own and pending["requested_by"] == actor.id:
            actions.append("withdraw-extension")
        elif managed and pending["requested_by"] != actor.id:
            actions.extend(("approve-extension", "reject-extension"))
    return actions


def _signer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"], salt="member2-task-web-lifecycle-draft-v1")


def _load_draft(target, action, filters):
    try:
        draft = _signer().loads(request.form.get("draft_ticket", ""))
    except BadData:
        raise AppError(400, "INVALID_INPUT", "취소·연장 입력 화면을 다시 열어 주세요.") from None
    if not isinstance(draft, dict) or set(draft) != {
            "binding", "action", "target", "before", "versions", "extension_id", "filters"}:
        raise AppError(400, "INVALID_INPUT", "취소·연장 확인 내용을 확인해 주세요.")
    if not isinstance(draft["binding"], str) or not hmac.compare_digest(draft["binding"], _binding()):
        raise AppError(403, "FORBIDDEN", "이전 로그인에서 연 취소·연장 화면은 사용할 수 없습니다.")
    if (draft["action"] != action or draft["target"] != target or draft["filters"] != filters
            or not isinstance(draft["before"], dict) or not isinstance(draft["versions"], dict)
            or set(draft["versions"]) != {"expected_row_version", "expected_assignment_revision"}):
        raise AppError(400, "INVALID_INPUT", "취소·연장 대상과 조회 조건을 확인해 주세요.")
    ExpectedVersions.from_body(draft["versions"])
    needs_row = action in ("approve-extension", "reject-extension", "withdraw-extension")
    if needs_row != (draft["extension_id"] is not None):
        raise AppError(400, "INVALID_INPUT", "연장 신청 대상을 확인해 주세요.")
    if needs_row:
        positive_bigint(draft["extension_id"], "extension_id")
    return draft


def _response(draft, action, fields, filters, *, write_enabled, error=None):
    response = make_response(render_template("tasks/lifecycle_action.html", draft=draft,
        draft_ticket=_signer().dumps(draft), action=action, action_label=LABELS[action],
        fields=fields, filters=filters, error=error, write_enabled=write_enabled,
        source_url=url_for("task_web.lifecycle_action", task_id=int(draft["target"]),
                           action=action, **filters)), error.status if error else 200)
    response.headers["Cache-Control"] = "no-store"
    response.headers["Referrer-Policy"] = "same-origin"
    return response


@login_required
def lifecycle_action(task_id, action):
    from .web import _filters, _list_args

    if action not in ACTIONS:
        raise AppError(404, "NOT_FOUND", "취소·연장 동작을 찾을 수 없습니다.")
    target = str(task_id)
    positive_bigint(target, "task_id")
    filters = _list_args(*_filters())
    _form({"csrf_token", "draft_ticket", "op"} | FIELDS[action], query=True)
    if request.method == "GET" and request.form:
        raise AppError(400, "INVALID_INPUT", "조회에는 취소·연장 입력을 전달하지 않습니다.")
    if request.method == "POST" and request.form.get("op") != "prepare":
        raise AppError(400, "INVALID_INPUT", "취소·연장 확인 동작을 확인해 주세요.")
    mode = "manage" if action in ("cancel", "approve-extension", "reject-extension") else "own"
    with authenticated_transaction() as db:
        task, assignment, managers, _users = _current(db, target, mode=mode)
        context = _lifecycle_context_data(db, task, assignment, managers)
        if request.method == "GET":
            if (action in ("approve-extension", "reject-extension")
                    and context["pending_extension"] is not None
                    and context["pending_extension"]["requested_by"] == g.user.id):
                raise AppError(403, "FORBIDDEN", "본인의 연장 신청을 직접 판단할 수 없습니다.")
            if action not in available_actions(context, g.user):
                raise AppError(409, "TASK_STATE_CONFLICT", "현재 업무·연장 신청에서 이 동작을 수행할 수 없습니다.")
            pending = context["pending_extension"]
            selected_id = pending["extension_id"] if action in (
                "approve-extension", "reject-extension", "withdraw-extension") else None
            before = {name: context[name] for name in ("start_date", "due_date", "task_status",
                "assignee_id", "pending_extension")}
            before["title"] = task.title
            draft = {"binding": _binding(), "action": action, "target": target, "before": before,
                "versions": {"expected_row_version": str(task.row_version),
                             "expected_assignment_revision": assignment.revision},
                "extension_id": selected_id, "filters": filters}
            fields = {name: "" for name in FIELDS[action]}
        else:
            draft = _load_draft(target, action, filters)
            fields = {name: request.form.get(name, "") for name in FIELDS[action]}
            body = fields | draft["versions"]
            try:
                validate_lifecycle_body(ACTIONS[action], body)
                prepare_lifecycle(db, target=target, action=ACTIONS[action], body=body,
                                  extension_id=draft["extension_id"])
                summary = draft["before"] | fields | {"before": draft["before"],
                                                       "extension_id": draft["extension_id"]}
                if draft["extension_id"] is not None:
                    summary["requested_due_date"] = draft["before"]["pending_extension"]["requested_due_date"]
                intent = _prepare(db, action=ACTIONS[action], target=target, body=body,
                                  summary=summary, filters=filters)
            except AppError as exc:
                return _response(draft, action, fields, filters,
                                 write_enabled=context["write_enabled"], error=exc)
            return _page(intent)
    return _response(draft, action, fields, filters, write_enabled=context["write_enabled"])
