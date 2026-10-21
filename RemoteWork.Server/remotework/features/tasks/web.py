"""Cookie-authenticated task reads using the existing server query policy."""
import os
from flask import Blueprint, current_app, g, render_template, request, url_for

from ...auth import login_required
from ...common import AppError
from .models import TASK_STATUSES
from .queries import get_task, list_tasks


bp = Blueprint("task_web", __name__, url_prefix="/tasks")
STATUS_LABELS = {
    "not_started": "진행전", "in_progress": "진행중", "submitted": "제출완료",
    "approved": "승인완료", "returned": "반려완료",
}
EXTENSION_LABELS = {
    "pending": "대기", "approved": "승인", "rejected": "반려",
    "withdrawn": "철회", "cancelled": "취소",
}


@bp.record_once
def register_writes(state):
    if state.app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        from .web_writes import register
        register(state)


@bp.record_once
def register_lifecycle_inbox(state):
    # Keep lifecycle imports and routes behind their existing explicit opt-in.
    enabled = state.app.config.get("ENABLE_TASK_LIFECYCLE_DRAFT",
        os.getenv("REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT") == "1")
    if enabled is True:
        state.add_url_rule("/extension-requests", "extension_requests", extension_requests, methods=["GET"])


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
                           status_links=status_links, status_labels=STATUS_LABELS,
                           has_creation="task_web.new" in current_app.view_functions,
                           has_lifecycle_inbox="task_web.extension_requests" in current_app.view_functions)


@bp.get("/<int:task_id>")
@login_required
def detail(task_id):
    scope, status, include_cancelled = _filters()
    if task_id > 9_223_372_036_854_775_807:
        raise AppError(400, "INVALID_INPUT", "업무 ID 범위를 확인해 주세요.")
    # Filters retain only the return destination. Both read paths independently
    # recheck the current own-or-managed relationship.
    lifecycle_context = None
    management_actions = []
    lifecycle_actions = []
    if "task_web.manage" in current_app.view_functions:
        from ...auth import authenticated_transaction
        from .web_management import read_detail
        with authenticated_transaction() as db:
            task, management_actions = read_detail(db, str(task_id))
            if "task_web.extension_requests" in current_app.view_functions:
                from .lifecycle import read_lifecycle_detail
                lifecycle_context, task = read_lifecycle_detail(db, str(task_id))
    elif "task_web.extension_requests" in current_app.view_functions:
        from ...auth import authenticated_transaction
        from .lifecycle import read_lifecycle_detail

        # Current identity, task/assignment locks and the displayed summary use
        # one guarded transaction. Read failures never become a partial page.
        with authenticated_transaction() as db:
            lifecycle_context, task = read_lifecycle_detail(db, str(task_id))
    else:
        task = get_task(g.user, task_id)
    if lifecycle_context is not None and "task_web.lifecycle_action" in current_app.view_functions:
        from .web_lifecycle import available_actions
        lifecycle_actions = available_actions(lifecycle_context, g.user)
    return render_template("tasks/detail.html", task=task, status_labels=STATUS_LABELS,
                           lifecycle_context=lifecycle_context,
                           has_start="task_web.start_prepare" in current_app.view_functions,
                           management_actions=management_actions,
                           lifecycle_actions=lifecycle_actions,
                           has_results="results_web.index" in current_app.view_functions,
                           filters=_list_args(scope, status, include_cancelled),
                           back_url=url_for("task_web.index", **_list_args(scope, status, include_cancelled)))


@login_required
def extension_requests():
    from ...auth import authenticated_transaction
    from .lifecycle import EXTENSION_STATUSES, _query, list_extension_requests

    # Reuse the API's current identity refresh and exact read policy. These
    # guarded reads neither grant an action nor require a write integration ACK.
    with authenticated_transaction() as db:
        _query({"scope", "status"})
        scope, status = request.args.get("scope", "own"), request.args.get("status")
        items = list_extension_requests(db, g.user, scope, status)
    status_links = [(None, "전체", url_for("task_web.extension_requests", scope=scope))]
    status_links.extend((value, EXTENSION_LABELS[value],
                         url_for("task_web.extension_requests", scope=scope, status=value))
                        for value in EXTENSION_STATUSES)
    return render_template("tasks/extension_requests.html", items=items, scope=scope, status=status,
                           status_links=status_links, extension_labels=EXTENSION_LABELS)
