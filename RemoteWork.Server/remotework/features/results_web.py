"""Member 3 cookie UI. Same result operations; no Bearer/cookie channel mixing."""
import hashlib
import hmac
import json

from flask import Blueprint, current_app, g, jsonify, redirect, render_template, request, url_for
from sqlalchemy import select
from sqlalchemy.exc import SQLAlchemyError
from werkzeug.exceptions import HTTPException

from ..auth import authenticated_transaction, login_required
from ..common import AppError, success
from ..db import get_db
from ..write_requests import issue_key, lookup_result
from . import results as service
from .tasks.queries import list_tasks

bp = Blueprint("results_web", __name__, url_prefix="/results")


def session_scope():
    return hashlib.sha256(("results-web:" + g.auth_session.token_hash).encode()).hexdigest()


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
    if (set(request.args) - {"task_id", "view"} or
            any(len(values) != 1 for _, values in request.args.lists())):
        raise AppError(400, "INVALID_INPUT", "업무 선택 정보를 확인해 주세요.")
    target = request.args.get("task_id")
    target = str(service.positive_bigint(target, "task_id")) if target is not None else ""
    view = request.args.get("view", "review" if g.user.role == "admin" else "own")
    if view not in ("own", "review") or (view == "review" and g.user.role != "admin"):
        raise AppError(403, "FORBIDDEN", "현재 조회할 수 있는 결과물 범위를 선택해 주세요.")
    # A navigation hint only. The data routes still enforce current ownership
    # or reviewer rights; an arbitrary task ID never grants access.
    return render_template("results/index.html", page_scope=session_scope(), initial_task_id=target, initial_view=view)


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
