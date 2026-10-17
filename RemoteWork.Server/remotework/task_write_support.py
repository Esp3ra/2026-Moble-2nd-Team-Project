"""Member 1 W1 preparation only. No task/status/deadline mutation endpoint.

User approved the role/manager policy on 2026-10-17. State/submit/reviewer/deadline
semantics still belong to members 2/3; a context or key is never final permission.
"""
from flask import Blueprint, g, request
from sqlalchemy import select

from .auth import authenticated_transaction, login_required
from .common import AppError, success
from .models import iso
from .user_candidates import CandidateScope, list_user_candidates
from .write_contract import positive_bigint

bp = Blueprint("task_write_support", __name__, url_prefix="/api/v1/tasks")


def _task(db, target):
    from .features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    task_id = positive_bigint(target, "task_id")
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id).with_for_update()
                     .execution_options(populate_existing=True))
    if task is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == task_id,
        TaskAssignment.revision == task.current_assignment_revision).with_for_update()
        .execution_options(populate_existing=True))
    managers = list(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == task_id)
                               .order_by(TaskManager.user_id).with_for_update()))
    return task, assignment, managers


def authorize_task(db, target, *, manage=False):
    if manage and g.user.role != "admin":
        raise AppError(403, "FORBIDDEN", "업무 관리 권한이 없습니다.")
    task, assignment, managers = _task(db, target)
    managed = g.user.role == "admin" and g.user.id in managers
    own = assignment is not None and assignment.assignee_id == g.user.id
    if not (managed if manage else own):
        raise AppError(403, "FORBIDDEN", "현재 업무 배정 또는 지정 관리 권한이 없습니다.")
    return True


def _creation(db, target):
    return g.user.role == "admin" and target == "new"


def _manage(db, target):
    return authorize_task(db, target, manage=True)


def _own(db, target):
    return authorize_task(db, target)


def _query(allowed):
    if set(request.args) - allowed or any(len(v) != 1 for _, v in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "지원하지 않거나 중복된 준비 조회 조건입니다.")


def _page(name, default, maximum):
    raw = request.args.get(name, str(default))
    if not raw.isascii() or not raw.isdecimal() or len(raw) > 7 or not 1 <= int(raw) <= maximum:
        raise AppError(400, "INVALID_INPUT", "후보 페이지 조건을 확인해 주세요.")
    return int(raw)


def _candidates(kind):
    with authenticated_transaction(roles=("admin",)) as db:
        _query({"task_id", "q", "page", "limit"})
        target = request.args.get("task_id")
        def authorize(session):
            if target is not None:
                _manage(session, target)
            return CandidateScope(None)  # Approved active-admin creation/current-manager selection policy.
        data = list_user_candidates(db, authorize=authorize, kind=kind,
            q=request.args.get("q", ""), page=_page("page", 1, 1_000_000), limit=_page("limit", 20, 100))
    return success(data)


@bp.get("/assignment-candidates")
@login_required
def assignment_candidates():
    return _candidates("assignment")


@bp.get("/manager-candidates")
@login_required
def manager_candidates():
    return _candidates("manager")


@bp.get("/<int:task_id>/write-context")
@login_required
def write_context(task_id):
    with authenticated_transaction() as db:
        _query(set())
        task, assignment, managers = _task(db, str(task_id))
        own = assignment is not None and assignment.assignee_id == g.user.id
        managed = g.user.role == "admin" and g.user.id in managers
        if not own and not managed:
            raise AppError(403, "FORBIDDEN", "준비 정보를 조회할 권한이 없습니다.")
        if assignment is None:
            raise AppError(503, "TASK_RELATION_INCOMPLETE", "현재 업무 배정의 정합성을 확인해 주세요.")
        data = {"task_id": str(task.task_id), "assignee_id": assignment.assignee_id,
            "manager_user_ids": managers, "row_version": str(task.row_version),
            "assignment_revision": task.current_assignment_revision, "task_status": task.task_status,
            "updated_at": iso(task.updated_at)}
    return success({"context": data})


def install(app):
    app.register_blueprint(bp)
    authorizers = app.extensions.setdefault("write_authorizers", {})
    authorizers.update({"task.created": _creation, "task.updated": _manage, "task.reassigned": _manage,
        "task.managers_changed": _manage, "task.accepted": _own, "task.deadline_extension_requested": _own})
    # No approval/rejection authorizer is invented before W3's reviewer/deadline contract.
