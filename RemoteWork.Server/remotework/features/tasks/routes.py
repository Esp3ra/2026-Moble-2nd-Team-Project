"""Member 2 reads, with separately enabled development-only task writes."""
import os

from flask import Blueprint, g, request

from ...auth import login_required
from ...common import AppError, success
from .models import TASK_STATUSES  # Import registers task tables before init-db.
from .queries import get_task, list_tasks


bp = Blueprint("task_queries", __name__, url_prefix="/api/v1/tasks")


@bp.record_once
def register_continuity_reader(state):
    # Only the existing task-query + common-write opt-in supplies this hook.
    # Collaboration installation, routes and operational settings stay owned
    # by the existing common/member 4 entry point.
    if state.app.config.get("ENABLE_COMMON_WRITE_DRAFT") is True:
        from .continuity import register_reader
        register_reader(state.app)


@bp.record_once
def register_web_reads(state):
    # The query blueprint is explicitly enabled by the existing feature registry.
    # Register a separate web prefix so common cookie authentication still applies.
    from .web import bp as task_web_bp
    state.app.register_blueprint(task_web_bp)


@bp.record_once
def register_creation(state):
    # Keep creation out of ordinary query/preparation deployments. This callback
    # runs only when the existing task-query blueprint is explicitly enabled.
    enabled = state.app.config.get(
        "ENABLE_TASK_CREATE_DRAFT", os.getenv("REMOTEWORK_ENABLE_TASK_CREATE_DRAFT") == "1")
    if enabled is True:
        from .creation import create_task
        state.add_url_rule("", "create", create_task, methods=["POST"])


@bp.record_once
def register_starting(state):
    # The blueprint itself is installed only when task queries are enabled.
    enabled = state.app.config.get(
        "ENABLE_TASK_START_DRAFT", os.getenv("REMOTEWORK_ENABLE_TASK_START_DRAFT") == "1")
    if enabled is True:
        from .starting import accept_task, register_start_action, start_rework
        if state.app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
            register_start_action(state.app)
        state.add_url_rule("/<string:task_id>/accept", "accept", accept_task, methods=["POST"])
        state.add_url_rule("/<string:task_id>/rework-start", "rework_start", start_rework,
                           methods=["POST"])


@bp.record_once
def register_management(state):
    enabled = state.app.config.get("ENABLE_TASK_MANAGEMENT_DRAFT",
        os.getenv("REMOTEWORK_ENABLE_TASK_MANAGEMENT_DRAFT") == "1")
    if enabled is True:
        from .management import change_managers, reassign_task, update_task
        state.add_url_rule("/<string:task_id>", "update", update_task, methods=["PATCH"])
        state.add_url_rule("/<string:task_id>/reassign", "reassign", reassign_task, methods=["POST"])
        state.add_url_rule("/<string:task_id>/managers", "change_managers", change_managers, methods=["PATCH"])


@bp.record_once
def register_review_requests(state):
    enabled = state.app.config.get("ENABLE_RESULTS_DRAFT", os.getenv("REMOTEWORK_ENABLE_RESULTS_DRAFT") == "1")
    if enabled is True and state.app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        from .review_requests import (acknowledge_request, create_review_request,
            register_review_actions, review_request_context, review_request_list)
        register_review_actions(state.app)
        state.add_url_rule("/<string:task_id>/review-request-context", "review_request_context", review_request_context)
        state.add_url_rule("/review-requests", "review_request_list", review_request_list)
        state.add_url_rule("/<string:task_id>/review-requests", "create_review_request", create_review_request, methods=["POST"])
        state.add_url_rule("/<string:task_id>/review-requests/<string:request_id>/acknowledge",
                           "acknowledge_review_request", acknowledge_request, methods=["POST"])


@bp.record_once
def register_lifecycle(state):
    enabled = state.app.config.get("ENABLE_TASK_LIFECYCLE_DRAFT",
        os.getenv("REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT") == "1")
    reads = state.app.config.get("ENABLE_TASK_LIFECYCLE_RESULT_READS_DRAFT",
        os.getenv("REMOTEWORK_ENABLE_TASK_LIFECYCLE_RESULT_READS_DRAFT") == "1")
    if (enabled is True or reads is True) and state.app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        from .lifecycle import register_lifecycle_result_readers
        register_lifecycle_result_readers(state.app, read_only=enabled is not True)
    if enabled is True:
        from .lifecycle import (approve_extension, cancel_task, extension_requests, lifecycle_context,
                                register_lifecycle_actions, reject_extension, request_extension, withdraw_extension)
        if state.app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
            register_lifecycle_actions(state.app)
        state.add_url_rule("/<string:task_id>/cancel", "cancel", cancel_task, methods=["POST"])
        state.add_url_rule("/<string:task_id>/lifecycle", "lifecycle", lifecycle_context, methods=["GET"])
        state.add_url_rule("/extension-requests", "extension_requests", extension_requests, methods=["GET"])
        state.add_url_rule("/<string:task_id>/deadline-extensions", "request_extension", request_extension, methods=["POST"])
        for suffix, handler in (("approve", approve_extension), ("reject", reject_extension), ("withdraw", withdraw_extension)):
            state.add_url_rule("/<string:task_id>/deadline-extensions/<string:extension_id>/" + suffix,
                               suffix + "_extension", handler, methods=["POST"])


def _query_args(allowed):
    if set(request.args) - allowed or any(len(values) != 1 for _, values in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "지원하지 않거나 중복된 업무 조회 조건입니다.")


@bp.get("")
@login_required
def index():
    _query_args({"scope", "status", "include_cancelled"})
    scope = request.args.get("scope", "own")
    status = request.args.get("status")
    include_cancelled = request.args.get("include_cancelled", "false")
    if scope not in ("own", "managed") or (status is not None and status not in TASK_STATUSES):
        raise AppError(400, "INVALID_INPUT", "업무 범위 또는 상태를 확인해 주세요.")
    if include_cancelled not in ("true", "false"):
        raise AppError(400, "INVALID_INPUT", "취소 포함 여부는 true 또는 false로 전달해 주세요.")
    return success({"items": list_tasks(g.user, scope, status, include_cancelled == "true")})


@bp.get("/<int:task_id>")
@login_required
def detail(task_id):
    _query_args(set())
    if task_id > 9_223_372_036_854_775_807:
        raise AppError(400, "INVALID_INPUT", "업무 ID 범위를 확인해 주세요.")
    return success({"task": get_task(g.user, task_id)})
