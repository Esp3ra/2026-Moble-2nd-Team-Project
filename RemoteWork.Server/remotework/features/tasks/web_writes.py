"""Owner 2 HTML adapters for the existing creation/start contracts.

The browser uses cookie auth and CSRF, never API tokens or internal HTTP.
Signed confirmation forms freeze one intent; only W1 stores business results.
"""
import hashlib
import hmac

from flask import current_app, g, make_response, render_template, request, url_for
from itsdangerous import BadData, URLSafeSerializer
from sqlalchemy.exc import SQLAlchemyError

from ...auth import authenticated_transaction, login_required
from ...common import AppError
from ...user_candidates import CandidateScope, list_user_candidates
from .start_transition import TaskStartAction
from .web_attachments import FORM_FIELDS as ATTACHMENT_FIELDS, form_display, form_selection, page_config


START_ACTIONS = {"accept": TaskStartAction.ACCEPT, "rework-start": TaskStartAction.REWORK_START}
LABELS = {"task.created": "업무 등록", "task.accepted": "업무 수락",
          "task.rework_started": "재작업 시작", "task.updated": "업무 수정",
          "task.reassigned": "담당자 변경", "task.managers_changed": "관리자 변경",
          "task.cancelled": "업무 취소", "task.deadline_extension_requested": "마감 연장 신청",
          "task.deadline_extension_approved": "마감 연장 승인",
          "task.deadline_extension_rejected": "마감 연장 반려",
          "task.deadline_extension_withdrawn": "마감 연장 철회"}
MANAGEMENT_ACTIONS = frozenset({"task.updated", "task.reassigned", "task.managers_changed"})
LIFECYCLE_ACTIONS = frozenset({"task.cancelled", "task.deadline_extension_requested",
    "task.deadline_extension_approved", "task.deadline_extension_rejected", "task.deadline_extension_withdrawn"})
FORM_FIELDS = {"csrf_token", "op", "title", "description", "start_date", "due_date",
               "assignee_id", "manager_user_ids", "q", "page"} | ATTACHMENT_FIELDS


def register(state):
    import os
    if not state.app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        return
    def enabled(name):
        return state.app.config.get("ENABLE_" + name + "_DRAFT",
            os.getenv("REMOTEWORK_ENABLE_" + name + "_DRAFT") == "1") is True
    create, start, manage = enabled("TASK_CREATE"), enabled("TASK_START"), enabled("TASK_MANAGEMENT")
    lifecycle, lifecycle_reads = enabled("TASK_LIFECYCLE"), enabled("TASK_LIFECYCLE_RESULT_READS")
    if create:
        state.add_url_rule("/new", "new", new_task, methods=["GET", "POST"])
    if start:
        state.add_url_rule("/<int:task_id>/start/<action>", "start_prepare", prepare_start, methods=["POST"])
    if manage:
        from .web_management import manage_task
        state.add_url_rule("/<int:task_id>/manage/<action>", "manage", manage_task, methods=["GET", "POST"])
    if lifecycle:
        from .web_lifecycle import lifecycle_action
        state.add_url_rule("/<int:task_id>/lifecycle/<action>", "lifecycle_action", lifecycle_action,
                           methods=["GET", "POST"])
    if create or start or manage or lifecycle:
        state.add_url_rule("/write/execute", "write_execute", execute, methods=["POST"])
    if create or start or manage or lifecycle or lifecycle_reads:
        state.add_url_rule("/write/result", "write_result", result, methods=["POST"])


def _form(allowed, *, repeated=(), query=False):
    if request.files or (not query and request.args) or set(request.form) - allowed or any(
            len(values) != 1 for name, values in request.form.lists() if name not in repeated):
        raise AppError(400, "INVALID_INPUT", "중복되거나 지원하지 않는 화면 입력입니다.")


def _authorize(db, action, target, *, for_result=False):
    if action == "task.created" and "task_web.new" in current_app.view_functions:
        from .creation import _creation_authorizer
        callback = _creation_authorizer()
    elif action in ("task.accepted", "task.rework_started") and "task_web.start_prepare" in current_app.view_functions:
        from .starting import _authorizer
        callback = _authorizer(TaskStartAction.ACCEPT if action == "task.accepted" else TaskStartAction.REWORK_START)
    elif action in MANAGEMENT_ACTIONS and "task_web.manage" in current_app.view_functions:
        from .management import _authorizer
        callback = _authorizer(action)
    elif action in LIFECYCLE_ACTIONS and for_result:
        reader = current_app.extensions.get("write_result_readers", {}).get(action)
        if reader is None:
            raise AppError(503, "WRITE_ACTION_NOT_READY", "현재 취소·연장 결과 조회가 연결되지 않았습니다.")
        callback = reader.authorize
    elif action in LIFECYCLE_ACTIONS and "task_web.lifecycle_action" in current_app.view_functions:
        from .lifecycle import _authorizer
        callback = _authorizer(action)
    else:
        raise AppError(503, "WRITE_ACTION_NOT_READY", "이 업무 동작이 현재 서버에서 활성화되지 않았습니다.")
    if callback(db, target) is not True:
        raise AppError(403, "FORBIDDEN", "현재 업무 동작을 수행할 권한이 없습니다.")
    return callback


def _binding():
    # Expose neither the cookie token nor its database digest in the form.
    return hmac.new(current_app.config["SECRET_KEY"].encode(),
        ("member2-task-web-session:" + g.auth_session.token_hash).encode(), hashlib.sha256).hexdigest()


def _signer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"], salt="member2-task-web-intent-v1")


def _ticket(intent):
    return _signer().dumps(intent)


def _load():
    _form({"csrf_token", "ticket"})
    try:
        intent = _signer().loads(request.form.get("ticket", ""))
    except BadData:
        raise AppError(400, "INVALID_INPUT", "업무 확인 화면을 다시 열어 주세요.") from None
    if not isinstance(intent, dict) or set(intent) != {
            "binding", "action", "target", "body", "key", "summary", "filters", "phase"}:
        raise AppError(400, "INVALID_INPUT", "업무 확인 내용을 확인해 주세요.")
    if not hmac.compare_digest(intent["binding"], _binding()):
        raise AppError(403, "FORBIDDEN", "이전 로그인에서 연 업무 확인 화면은 사용할 수 없습니다.")
    if intent["action"] not in LABELS or intent["phase"] not in ("prepared", "recovery", "retry"):
        raise AppError(400, "INVALID_INPUT", "업무 확인 동작을 확인해 주세요.")
    return intent


def _page(intent, *, error=None, completed=None, not_recorded=False, status=200):
    # No short ticket expiry: W1 may retain a success after the execute key expires.
    self_removed = (intent["action"] == "task.managers_changed"
                    and g.user.id not in intent["body"]["manager_user_ids"])
    lifecycle = intent["action"] in LIFECYCLE_ACTIONS
    can_execute = True
    if lifecycle:
        from .lifecycle_support import integration_acknowledged
        can_execute = ("task_web.lifecycle_action" in current_app.view_functions and integration_acknowledged())
    response = make_response(render_template("tasks/write_confirmation.html", intent=intent,
        ticket=_ticket(intent), action_label=LABELS[intent["action"]], error=error,
        completed=completed, not_recorded=not_recorded,
        management=intent["action"] in MANAGEMENT_ACTIONS,
        lifecycle=lifecycle, can_execute=can_execute,
        access_lost=error is not None and error.status == 403,
        self_removed=self_removed,
        can_open_detail=not self_removed or intent["summary"].get("before", {}).get("assignee_id") == g.user.id,
        back_url=url_for("task_web.index", **intent["filters"])), status)
    response.headers["Cache-Control"] = "no-store"
    response.headers["Referrer-Policy"] = "same-origin"
    return response


def _prepare(db, *, action, target, body, summary, filters):
    from ...write_requests import issue_key
    _authorize(db, action, target)
    key = issue_key(action=action, target=target)["request_key"]
    return {"binding": _binding(), "action": action, "target": target, "body": body,
            "key": key, "summary": summary, "filters": filters, "phase": "prepared"}


@login_required
def prepare_start(task_id, action):
    from .starting import _ACTIONS, _locked_current, _plan, utcnow
    from .lifecycle_support import ensure_not_cancelled
    from ...write_contract import ExpectedVersions
    from .web import _filters, _list_args
    if action not in START_ACTIONS:
        raise AppError(404, "NOT_FOUND", "업무 시작 동작을 찾을 수 없습니다.")
    _form({"csrf_token"}, query=True)
    filters = _list_args(*_filters())
    selected, target = START_ACTIONS[action], str(task_id)
    with authenticated_transaction() as db:
        _authorize(db, _ACTIONS[selected], target)
        task, assignment = _locked_current(db, target)
        ensure_not_cancelled(db, task.task_id)
        body = {"expected_row_version": str(task.row_version),
                "expected_assignment_revision": task.current_assignment_revision}
        _plan(task, assignment, ExpectedVersions.from_body(body), selected, utcnow())
        intent = _prepare(db, action=_ACTIONS[selected], target=target, body=body,
            summary={"title": task.title, "start_date": task.start_date.isoformat(),
                     "due_date": task.due_date.isoformat()}, filters=filters)
    return _page(intent)


def _id(value):
    if (not value.isascii() or not value.isdecimal() or len(value) > 10
            or value != str(int(value)) or not 1 <= int(value) <= 2_147_483_647):
        raise AppError(400, "INVALID_INPUT", "후보 목록에서 담당자와 관리자를 선택해 주세요.")
    return int(value)


def _candidates(db, kind, *, q="", page=1, selected=None):
    def authorize(session):
        _authorize(session, "task.created", "new")
        return CandidateScope(None if selected is None else frozenset(selected))
    return list_user_candidates(db, authorize=authorize, kind=kind, q=q, page=page, limit=20 if selected is None else 100)


@login_required
def new_task():
    from .creation import validate_creation_body
    from .web import _filters, _list_args
    filters = _list_args(*_filters())
    _form(FORM_FIELDS, repeated=("manager_user_ids",), query=True)
    draft = {name: request.form.get(name, "") for name in
             ("title", "description", "start_date", "due_date", "assignee_id", "q")}
    managers = [_id(value) for value in request.form.getlist("manager_user_ids")]
    if len(managers) != len(set(managers)) or len(managers) > 100:
        raise AppError(400, "INVALID_INPUT", "추가 관리자는 중복 없이 화면당 100명 이내로 선택해 주세요.")
    raw_page = request.form.get("page", "1")
    if not raw_page.isascii() or not raw_page.isdecimal() or len(raw_page) > 7 or not 1 <= int(raw_page) <= 1_000_000:
        raise AppError(400, "INVALID_INPUT", "후보 페이지를 확인해 주세요.")
    page, error = int(raw_page), None
    op = request.form.get("op", "search")
    if op not in ("search", "prepare"):
        raise AppError(400, "INVALID_INPUT", "업무 등록 동작을 확인해 주세요.")
    with authenticated_transaction(roles=("admin",)) as db:
        _authorize(db, "task.created", "new")
        selection = form_selection()
        display = form_display(selection)
        assignees = _candidates(db, "assignment", q=draft["q"], page=page)
        manager_choices = _candidates(db, "manager", q=draft["q"], page=page)
        assignee = None if not draft["assignee_id"] else _id(draft["assignee_id"])
        selected_assignees = _candidates(db, "assignment", selected=[] if assignee is None else [assignee])["items"]
        selected_managers = _candidates(db, "manager", selected=managers)["items"]
        if op == "prepare" and request.method == "POST":
            body = {name: draft[name] for name in ("title", "description", "start_date", "due_date")}
            body.update(assignee_id=assignee, manager_user_ids=managers)
            body.update(selection)
            try:
                validate_creation_body(body)
                if len(selected_assignees) != 1:
                    raise AppError(409, "ASSIGNEE_UNAVAILABLE", "선택한 담당자를 현재 배정할 수 없습니다.")
                if len(selected_managers) != len(managers):
                    raise AppError(409, "TASK_MANAGER_UNAVAILABLE", "선택한 관리자를 현재 지정할 수 없습니다.")
            except AppError as exc:
                error = exc
            else:
                intent = _prepare(db, action="task.created", target="new", body=body,
                    summary=body | {"assignee_label": selected_assignees[0]["selection_label"],
                        "manager_labels": [item["selection_label"] for item in selected_managers],
                        "creator_label": g.user.display_name}, filters=filters)
                return _page(intent)
    return render_template("tasks/new.html", draft=draft, managers=managers,
        assignees=assignees, manager_choices=manager_choices, selected_assignees=selected_assignees,
        selected_managers=selected_managers, filters=filters, page=page, error=error,
        attachment_config=page_config(attachment_ids=selection.get("attachment_ids"),
            explicit="attachment_ids" in selection, display=display)), (error.status if error else 200)


def _recover(intent, exc):
    if exc.status == 403 and intent["action"] in MANAGEMENT_ACTIONS:
        return _page(intent | {"phase": "recovery"}, error=AppError(403, exc.code,
            "현재 관리 권한이 없어 처리 결과를 확인할 수 없습니다. 성공이나 실패로 단정하지 말고 현재 업무 관리자에게 확인해 주세요. 기존 확인 요청은 보존하며 자동으로 재전송하지 않습니다."), status=403)
    if exc.status == 403 and intent["action"] in LIFECYCLE_ACTIONS:
        return _page(intent | {"phase": "recovery"}, error=AppError(403, exc.code,
            "현재 업무 접근 권한이 없어 처리 결과를 확인할 수 없습니다. 기존 요청을 보존하고 현재 업무 관리자에게 확인해 주세요. 자동으로 다시 전송하지 않습니다."), status=403)
    if exc.status in (401, 403, 404):
        raise exc
    return _page(intent | {"phase": "recovery"}, error=exc, status=exc.status)


@login_required
def execute():
    intent = None
    try:
        with authenticated_transaction() as db:
            intent = _load()
            _authorize(db, intent["action"], intent["target"])
            if intent["phase"] not in ("prepared", "retry"):
                raise AppError(409, "RESULT_CHECK_REQUIRED", "처리 결과를 먼저 확인해 주세요.")
            if intent["action"] == "task.created":
                from .creation import execute_creation
                saved = execute_creation(db, body=intent["body"], key=intent["key"])
            elif intent["action"] in MANAGEMENT_ACTIONS:
                from .management import execute_management
                saved = execute_management(db, target=intent["target"], action=intent["action"],
                                           body=intent["body"], key=intent["key"])
            elif intent["action"] in LIFECYCLE_ACTIONS:
                from .lifecycle import execute_lifecycle
                saved = execute_lifecycle(db, target=intent["target"], action=intent["action"],
                    body=intent["body"], key=intent["key"], extension_id=intent["summary"]["extension_id"])
            else:
                from .starting import execute_start
                action = TaskStartAction.ACCEPT if intent["action"] == "task.accepted" else TaskStartAction.REWORK_START
                saved = execute_start(db, target=intent["target"], action=action,
                                      body=intent["body"], key=intent["key"])
        return _page(intent, completed=saved.data)
    except AppError as exc:
        if intent is None:
            raise
        return _recover(intent, exc)
    except SQLAlchemyError:
        if intent is None:
            raise
        return _recover(intent, AppError(503, "DATABASE_UNAVAILABLE",
            "저장 결과를 확인하지 못했습니다. 같은 요청의 처리 결과를 확인해 주세요."))


@login_required
def result():
    intent = None
    try:
        with authenticated_transaction() as db:
            intent = _load()
            callback = _authorize(db, intent["action"], intent["target"], for_result=True)
            from ...write_requests import lookup_result
            found = lookup_result(db, key=intent["key"], action=intent["action"],
                                  target=intent["target"], authorize=callback)
        if found["state"] == "completed":
            return _page(intent, completed=found["result"])
        return _page(intent | {"phase": "retry"}, not_recorded=True)
    except AppError as exc:
        if intent is None:
            raise
        return _recover(intent, exc)
    except SQLAlchemyError:
        if intent is None:
            raise
        return _recover(intent, AppError(503, "DATABASE_UNAVAILABLE",
            "처리 결과 조회에 실패했습니다. 저장되지 않았다고 판단하지 말고 다시 확인해 주세요."))
