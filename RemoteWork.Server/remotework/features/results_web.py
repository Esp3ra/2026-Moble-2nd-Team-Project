"""Member 3 cookie UI. Same result operations; no Bearer/cookie channel mixing."""
import hashlib
import hmac
import json
from datetime import timezone
from itsdangerous import BadData, URLSafeSerializer

from flask import Blueprint, current_app, g, jsonify, redirect, render_template, request, url_for
from sqlalchemy import select
from sqlalchemy.exc import SQLAlchemyError
from werkzeug.exceptions import HTTPException

from ..auth import authenticated_transaction, login_required
from ..common import AppError, success
from ..db import get_db
from ..write_requests import issue_key, lookup_result
from ..models import utcnow
from . import results as service
from .tasks.queries import list_tasks

bp = Blueprint("results_web", __name__, url_prefix="/results")


def session_scope():
    if not getattr(g, "auth_session", None) or g.auth_session.channel != "web":
        raise AppError(401, "AUTH_REQUIRED", "현재 웹 로그인이 필요합니다.")
    return hashlib.sha256(("results-web:" + g.auth_session.token_hash).encode()).hexdigest()


def _back_filters(scope, status, include_cancelled):
    from .tasks.models import TASK_STATUSES
    if scope not in ("own", "managed") or (status is not None and
            (not isinstance(status, str) or status not in TASK_STATUSES)) or type(include_cancelled) is not bool:
        raise AppError(400, "INVALID_INPUT", "원래 업무 조회 조건을 확인해 주세요.")
    return dict(scope=scope, status=status, include_cancelled=include_cancelled)


def _back_signer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"], salt="results-task-back-v1")


def issue_task_back_context(task_id, *, scope, status=None, include_cancelled=False):
    """Issue in the current authenticated TX; expires with this AuthSession."""
    if not getattr(g, "write_transaction_active", False):
        with authenticated_transaction():
            return issue_task_back_context(task_id, scope=scope, status=status, include_cancelled=include_cancelled)
    task_id = str(service.positive_bigint(task_id, "task_id"))
    filters = _back_filters(scope, status, include_cancelled)
    task, assignment, managers = service._current(get_db(), int(task_id))
    if ((scope == "own" and assignment.assignee_id != g.user.id) or
            (scope == "managed" and (g.user.role != "admin" or g.user.id not in managers))):
        raise AppError(403, "FORBIDDEN", "원래 업무의 현재 조회 권한이 필요합니다.")
    return _back_signer().dumps(dict(task_id=task_id, user_id=g.user.id, session=session_scope(),
        expires_at=int(g.auth_session.expires_at.replace(tzinfo=timezone.utc).timestamp()), **filters))


def validate_task_back_context(token, *, task_id):
    if not getattr(g, "write_transaction_active", False):
        with authenticated_transaction():
            return validate_task_back_context(token, task_id=task_id)
    task_id = str(service.positive_bigint(task_id, "task_id"))
    try:
        if not isinstance(token, str) or not 1 <= len(token) <= 4096:
            raise ValueError()
        data = _back_signer().loads(token)
        if not isinstance(data, dict) or set(data) != {"task_id", "user_id", "session", "expires_at", "scope", "status", "include_cancelled"}:
            raise ValueError()
        if (data["task_id"] != task_id or type(data["user_id"]) is not int or data["user_id"] != g.user.id or
                not isinstance(data["session"], str) or not hmac.compare_digest(data["session"], session_scope()) or
                type(data["expires_at"]) is not int or
                data["expires_at"] <= int(utcnow().replace(tzinfo=timezone.utc).timestamp()) or
                data["expires_at"] > int(g.auth_session.expires_at.replace(tzinfo=timezone.utc).timestamp())):
            raise ValueError()
        return _back_filters(data["scope"], data["status"], data["include_cancelled"])
    except (BadData, ValueError, TypeError, KeyError):
        raise AppError(400, "INVALID_BACK_CONTEXT", "원래 업무·로그인 접속의 복귀 정보가 유효하지 않습니다.") from None


def build_task_results_url(task_id, *, view, scope, status=None, include_cancelled=False):
    if "task_web.detail" not in current_app.view_functions:
        raise AppError(503, "TASK_WEB_REQUIRED", "현재 업무 웹 상세 화면 연결이 필요합니다.")
    if view not in ("own", "read") or (scope == "managed" and view != "read"):
        raise AppError(400, "INVALID_INPUT", "관리 업무는 읽기 전용으로 연결해 주세요.")
    token = issue_task_back_context(task_id, scope=scope, status=status, include_cancelled=include_cancelled)
    return url_for("results_web.index", task_id=task_id, view=view, back_context=token)


@bp.record_once
def register_task_web_provider(state):
    # Consumers inspect this optional public provider, without importing models
    # when results are OFF. It grants no task mutation or review permission.
    state.app.extensions["results_task_web"] = dict(session_scope=session_scope,
        build_url=build_task_results_url, issue_back_context=issue_task_back_context,
        validate_back_context=validate_task_back_context)


@bp.before_request
def current_page_session():
    if request.path.startswith("/results/data/") and g.user:
        actual = request.headers.get("X-Results-Session", "")
        if not hmac.compare_digest(actual.encode(), session_scope().encode()):
            raise AppError(409, "RESULTS_SESSION_CHANGED", "로그인 접속이 변경되었습니다. 화면을 다시 열어 주세요.")


@bp.after_request
def session_header(response):
    if g.user and g.auth_session:
        response.headers["X-Results-Session"] = session_scope()
    return response


@bp.app_context_processor
def menu_context():
    return {"results_web_enabled": True}


@bp.errorhandler(AppError)
def application_error(error):
    if request.path.startswith("/results/data/"):
        return jsonify(ok=False, error={"code": error.code, "message": error.message},
                       request_id=g.request_id), error.status
    if error.status == 401:
        return redirect(url_for("web.login"))
    return render_template("error.html", message=error.message, status=error.status), error.status


@bp.errorhandler(SQLAlchemyError)
def database_error(error):
    get_db().rollback()
    current_app.logger.error("results_web_database_error request_id=%s type=%s", g.request_id, type(error).__name__)
    return application_error(AppError(503, "DATABASE_UNAVAILABLE", "데이터베이스 연결 또는 준비 상태를 확인해 주세요."))


@bp.errorhandler(HTTPException)
def http_error(error):
    return application_error(AppError(error.code, f"HTTP_{error.code}", "요청 크기와 입력 형식을 확인해 주세요."))


@bp.get("")
@login_required
def index():
    if (set(request.args) - {"task_id", "view", "back_context"} or
            any(len(values) != 1 for _, values in request.args.lists())):
        raise AppError(400, "INVALID_INPUT", "업무 선택 정보를 확인해 주세요.")
    target = request.args.get("task_id")
    target = str(service.positive_bigint(target, "task_id")) if target is not None else ""
    view = request.args.get("view", "review" if g.user.role == "admin" else "own")
    if view not in ("own", "review", "read") or (view == "review" and g.user.role != "admin"):
        raise AppError(403, "FORBIDDEN", "현재 조회할 수 있는 결과물 범위를 선택해 주세요.")
    back_context = request.args.get("back_context")
    if (view == "read" or back_context is not None) and not target:
        raise AppError(400, "INVALID_INPUT", "읽기·복귀할 업무 번호가 필요합니다.")
    if view == "read" or back_context is not None:
        with authenticated_transaction() as db:
            task, assignment, managers = service._current(db, int(target))
            service._readable(db, task, assignment, managers)
            if back_context is not None:
                validate_task_back_context(back_context, task_id=target)
    back_url = url_for("results_web.task_back", task_id=target, back_context=back_context) if back_context is not None else None
    return render_template("results/index.html", page_scope=session_scope(), initial_task_id=target,
                           initial_view=view, back_url=back_url)


@bp.get("/tasks/<task_id>/back")
@login_required
def task_back(task_id):
    if "task_web.detail" not in current_app.view_functions:
        raise AppError(503, "TASK_WEB_REQUIRED", "업무 웹 상세 화면이 준비되지 않았습니다. 이미 확인한 결과물 저장 성공은 유지됩니다.")
    if set(request.args) != {"back_context"} or len(request.args.getlist("back_context")) != 1:
        raise AppError(400, "INVALID_BACK_CONTEXT", "원래 업무 복귀 정보를 확인해 주세요.")
    with authenticated_transaction() as db:
        filters = validate_task_back_context(request.args["back_context"], task_id=task_id)
        try:
            task, assignment, managers = service._current(db, int(task_id))
            if assignment.assignee_id != g.user.id and not (g.user.role == "admin" and g.user.id in managers):
                raise AppError(403, "FORBIDDEN", "현재 업무 접근 권한이 없습니다.")
        except AppError as error:
            if error.status not in (403, 404):
                raise
            raise AppError(error.status, error.code, "현재 원래 업무에 접근할 수 없습니다. 이미 확인한 결과물 저장 성공은 유지됩니다.") from None
    values = dict(scope=filters["scope"], include_cancelled="true" if filters["include_cancelled"] else "false")
    if filters["status"] is not None:
        values["status"] = filters["status"]
    return redirect(url_for("task_web.detail", task_id=int(task_id), **values))


@bp.get("/history")
@login_required
def history_page():
    if g.user.role != "admin":
        raise AppError(403, "FORBIDDEN", "관리자만 업무 이력을 조회할 수 있습니다.")
    return render_template("results/history.html", page_scope=session_scope())


@bp.get("/data/tasks")
@login_required
def tasks():
    scope = request.args.get("scope", "own")
    if scope not in ("own", "review") or (scope == "review" and g.user.role != "admin"):
        raise AppError(403, "FORBIDDEN", "조회 범위를 확인해 주세요.")
    if scope == "own":
        with authenticated_transaction() as db:
            items = list_tasks(g.user, "own")
            for item in items:
                latest = service._latest(db, int(item["task_id"]))
                item["round_no"] = str(latest.round_no) if latest else None
    else:
        # The existing reviewer list grants only member 3's minimal task detail.
        response, status = service.review_tasks()
        items = response.get_json()["data"]["items"]
        with authenticated_transaction(roles=("admin",)) as db:
            for item in items:
                task, assignment, managers = service._current(db, int(item["task_id"]))
                service._readable(db, task, assignment, managers)
                latest = service._latest(db, task.task_id)
                item["round_no"] = str(latest.round_no) if latest else None
    return success({"items": items})


for rule, name, view in (
    ("/data/tasks/<task_id>", "task", service.result_task),
    ("/data/tasks/<task_id>/context", "submission_context", service.submission_context),
    ("/data/tasks/<task_id>/submissions", "submissions", service.submissions),
    ("/data/tasks/<task_id>/activity", "activity", service.task_activity),
    ("/data/submissions/<submission_id>/context", "decision_context", service.decision_context),
    ("/data/attachments/<attachment_id>/download", "download", service.download),
    ("/data/history-tasks", "history_tasks", service.history_tasks),
):
    bp.add_url_rule(rule, name, view, methods=["GET"])


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate JSON member")
        result[key] = value
    return result


def invalid_constant(value):
    raise ValueError("non-standard JSON constant")


def shared_provider():
    provider = current_app.extensions.get("results_shared_attachments")
    if provider is None:
        raise AppError(503, "TASK_ATTACHMENT_PROVIDER_REQUIRED", "공용 첨부 기능의 활성화가 필요합니다.")
    return provider


@bp.get("/data/shared/tasks/<task_id>")
@login_required
def shared_task_files(task_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "첨부 목록 조회 조건을 확인해 주세요.")
    return shared_provider().task_files(task_id)


@bp.get("/data/shared/tasks/<task_id>/files/<attachment_id>/download")
@login_required
def shared_download(task_id, attachment_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "첨부 다운로드 조건을 확인해 주세요.")
    return shared_provider().task_download(task_id, attachment_id)


@bp.get("/data/shared/tasks/<task_id>/files/<attachment_id>/chunks")
@login_required
def shared_chunks(task_id, attachment_id):
    return shared_provider().task_chunks(task_id, attachment_id)


def form_body():
    if request.files or request.args or set(request.form) != {"csrf_token", "body"} or any(len(values) != 1 for _, values in request.form.lists()):
        raise AppError(400, "INVALID_INPUT", "화면의 입력 항목을 확인해 주세요.")
    try:
        body = json.loads(request.form["body"], object_pairs_hook=unique_object, parse_constant=invalid_constant)
    except (ValueError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력을 확인해 주세요.") from None
    if not isinstance(body, dict):
        raise AppError(400, "INVALID_INPUT", "입력 형식을 확인해 주세요.")
    return body


@bp.post("/data/shared/uploads/start")
@login_required
def shared_start():
    return shared_provider().start_upload(body=form_body())


@bp.post("/data/shared/uploads/<attachment_id>/chunks")
@login_required
def shared_write_chunk(attachment_id):
    return shared_provider().write_chunk(attachment_id, body=form_body())


@bp.post("/data/shared/uploads/<attachment_id>/complete")
@login_required
def shared_complete(attachment_id):
    return shared_provider().complete_upload(attachment_id, body=form_body())


@bp.post("/data/shared/uploads/<attachment_id>/discard")
@login_required
def shared_discard(attachment_id):
    service._fields(set(), form_body())
    return shared_provider().discard_upload(attachment_id)


def rework_support():
    if "task_queries.rework_start" not in current_app.view_functions:
        raise AppError(503, "TASK_START_DISABLED", "기존 업무 재작업 시작 기능의 활성화가 필요합니다.")
    from .tasks import starting
    return starting


@bp.get("/data/tasks/<task_id>/rework-context")
@login_required
def rework_context(task_id):
    rework_support()
    task_id = service.positive_bigint(task_id, "task_id")
    with authenticated_transaction() as db:
        task, assignment, managers = service._current(db, task_id)
        service._owner(assignment)
        if task.task_status != "returned":
            raise AppError(409, "TASK_STATE_CONFLICT", "반려완료 업무만 재작업을 시작할 수 있습니다.")
        data = dict(row_version=str(task.row_version), assignment_revision=task.current_assignment_revision)
    return success(data)


@bp.post("/data/actions/<operation>/<target>")
@login_required
def action(operation, target):
    # Common protect_forms validates this same session's csrf_token first.
    if request.files or set(request.form) != {"csrf_token", "body"}:
        raise AppError(400, "INVALID_INPUT", "화면의 입력 항목을 확인해 주세요.")
    try:
        body = json.loads(request.form["body"], object_pairs_hook=unique_object,
                          parse_constant=invalid_constant)
    except (ValueError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력을 확인해 주세요.") from None
    if not isinstance(body, dict):
        raise AppError(400, "INVALID_INPUT", "입력 형식을 확인해 주세요.")
    if operation in ("key", "lookup"):
        callbacks = {"submission.submitted": service._authorize_submit,
                     "submission.approved": service._authorize_decision,
                     "submission.returned": service._authorize_decision}
        if isinstance(body.get("action"), str) and body["action"] == "task.rework_started":
            callbacks["task.rework_started"] = rework_support().authorize_rework
        if set(body) != {"action"} or not isinstance(body["action"], str) or body["action"] not in callbacks:
            raise AppError(400, "INVALID_INPUT", "결과물 처리 행동을 확인해 주세요.")
        callback = callbacks[body["action"]]
        with authenticated_transaction() as db:
            if callback(db, target) is not True:
                raise AppError(403, "FORBIDDEN", "현재 처리 권한이 없습니다.")
            if operation == "key":
                data = issue_key(action=body["action"], target=target)
            else:
                data = lookup_result(db, key=request.headers.get("Idempotency-Key"),
                    action=body["action"], target=target, authorize=callback)
        return success(data, 201 if operation == "key" else 200)
    operations = {"submit": service.submit, "decide": service.decide,
                  "upload-start": service.start_json_upload,
                  "upload-chunk": service.write_json_chunk,
                  "upload-complete": service.complete_json_upload}
    if operation == "rework":
        starting = rework_support()
        return starting._start(target, starting.TaskStartAction.REWORK_START, body=body)
    if operation not in operations:
        raise AppError(404, "NOT_FOUND", "결과물 처리 경로를 확인해 주세요.")
    return operations[operation](target, body=body)
