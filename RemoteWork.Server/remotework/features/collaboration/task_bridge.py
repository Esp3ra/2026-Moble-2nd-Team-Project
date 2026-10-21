"""Member 4 read-only bridge proposal; does not replace member 2's services.

Version equality proves no intervening versioned writes. On a version change,
an owner-reviewed history reader must establish continuity; never infer it from
assignment_revision alone. Unknown continuity blocks acceptance, not rejection.
"""
from flask import current_app
from sqlalchemy import select
from ...common import AppError
from ..tasks.models import TaskRecord, TaskAssignment
from ..tasks.lifecycle_support import ensure_not_cancelled


def current_task_context(db, task_id, *, since_version=None):
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id)
        .with_for_update().execution_options(populate_existing=True))
    if task is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == task_id,
        TaskAssignment.revision == task.current_assignment_revision)
        .with_for_update().execution_options(populate_existing=True))
    if assignment is None:
        raise AppError(503, "TASK_ASSIGNMENT_REQUIRED", "현재 업무 배정 연결을 확인해 주세요.")
    cancelled = False
    try:
        ensure_not_cancelled(db, task_id)
    except AppError as error:
        if error.code != "TASK_CANCELLED":
            raise
        cancelled = True
    continuous = True if since_version is None or since_version == task.row_version else None
    reader = current_app.extensions.get("collaboration_continuity_reader")
    if continuous is None and callable(reader):
        continuous = reader(db, task_id, since_version, task.row_version)
        if continuous is not True and continuous is not False and continuous is not None:
            raise RuntimeError("Continuity reader must return True, False or None")
    return {"task_id": task.task_id, "title": task.title,
        "assignee_id": assignment.assignee_id, "assignment_revision": assignment.revision,
        "row_version": task.row_version, "status": task.task_status,
        "due_date": task.due_date, "cancelled": cancelled, "continuous_in_progress": continuous}
