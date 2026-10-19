"""Member 2 task reads, with separately enabled development-only creation."""
import os

from flask import Blueprint, g, request

from ...auth import login_required
from ...common import AppError, success
from .models import TASK_STATUSES  # Import registers task tables before init-db.
from .queries import get_task, list_tasks


bp = Blueprint("task_queries", __name__, url_prefix="/api/v1/tasks")


@bp.record_once
def register_creation(state):
    # Keep creation out of ordinary query/preparation deployments. This callback
    # runs only when the existing task-query blueprint is explicitly enabled.
    enabled = state.app.config.get(
        "ENABLE_TASK_CREATE_DRAFT", os.getenv("REMOTEWORK_ENABLE_TASK_CREATE_DRAFT") == "1")
    if enabled is True:
        from .creation import create_task
        state.add_url_rule("", "create", create_task, methods=["POST"])


def _query_args(allowed):
    if set(request.args) - allowed or any(len(values) != 1 for _, values in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "지원하지 않거나 중복된 업무 조회 조건입니다.")


@bp.get("")
@login_required
def index():
    _query_args({"scope", "status"})
    scope = request.args.get("scope", "own")
    status = request.args.get("status")
    if scope not in ("own", "managed") or (status is not None and status not in TASK_STATUSES):
        raise AppError(400, "INVALID_INPUT", "업무 범위 또는 상태를 확인해 주세요.")
    return success({"items": list_tasks(g.user, scope, status)})


@bp.get("/<int:task_id>")
@login_required
def detail(task_id):
    _query_args(set())
    if task_id > 9_223_372_036_854_775_807:
        raise AppError(400, "INVALID_INPUT", "업무 ID 범위를 확인해 주세요.")
    return success({"task": get_task(g.user, task_id)})
