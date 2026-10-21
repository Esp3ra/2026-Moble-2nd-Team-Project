"""Cookie-authenticated task reads using the existing server query policy."""
from flask import Blueprint, g, render_template, request, url_for

from ...auth import login_required
from ...common import AppError
from .models import TASK_STATUSES
from .queries import get_task, list_tasks


bp = Blueprint("task_web", __name__, url_prefix="/tasks")
STATUS_LABELS = {
    "not_started": "진행전", "in_progress": "진행중", "submitted": "제출완료",
    "approved": "승인완료", "returned": "반려완료",
}


def _filters():
    allowed = {"scope", "status", "include_cancelled"}
    if set(request.args) - allowed or any(len(values) != 1 for _, values in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "지원하지 않거나 중복된 업무 조회 조건입니다.")
    scope = request.args.get("scope", "own")
    status = request.args.get("status")
    include_cancelled = request.args.get("include_cancelled", "false")
    if scope not in ("own", "managed") or (status is not None and status not in TASK_STATUSES):
        raise AppError(400, "INVALID_INPUT", "업무 범위 또는 상태를 확인해 주세요.")
    if include_cancelled not in ("true", "false"):
        raise AppError(400, "INVALID_INPUT", "취소 포함 여부는 true 또는 false로 전달해 주세요.")
    return scope, status, include_cancelled == "true"


def _list_args(scope, status, include_cancelled):
    values = {"scope": scope, "include_cancelled": "true" if include_cancelled else "false"}
    if status is not None:
        values["status"] = status
    return values


@bp.get("")
@login_required
def index():
    scope, status, include_cancelled = _filters()
    items = list_tasks(g.user, scope, status, include_cancelled)
    filters = _list_args(scope, status, include_cancelled)
    status_links = [(None, "전체", url_for("task_web.index", **_list_args(scope, None, include_cancelled)))]
    status_links.extend((value, STATUS_LABELS[value],
                         url_for("task_web.index", **_list_args(scope, value, include_cancelled)))
                        for value in TASK_STATUSES)
    return render_template("tasks/index.html", items=items, scope=scope, status=status,
                           include_cancelled=include_cancelled, filters=filters,
                           status_links=status_links, status_labels=STATUS_LABELS)


@bp.get("/<int:task_id>")
@login_required
def detail(task_id):
    scope, status, include_cancelled = _filters()
    if task_id > 9_223_372_036_854_775_807:
        raise AppError(400, "INVALID_INPUT", "업무 ID 범위를 확인해 주세요.")
    # Filters only retain the return destination. They grant no detail permission;
    # get_task independently rechecks the current own-or-managed relationship.
    task = get_task(g.user, task_id)
    return render_template("tasks/detail.html", task=task, status_labels=STATUS_LABELS,
                           back_url=url_for("task_web.index", **_list_args(scope, status, include_cancelled)))
