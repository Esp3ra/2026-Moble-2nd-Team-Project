"""Task reads only. Scope is derived from the authenticated server identity."""
from sqlalchemy import and_, exists, or_, select

from ...common import AppError
from ...db import get_db
from ...models import User
from .models import TaskAssignment, TaskManager, TaskRecord


def _current_assignment_query():
    return (select(TaskRecord, TaskAssignment, User.display_name)
            .join(TaskAssignment, and_(
                TaskAssignment.task_id == TaskRecord.task_id,
                TaskAssignment.revision == TaskRecord.current_assignment_revision))
            .join(User, User.id == TaskAssignment.assignee_id))


def _manager_condition(actor):
    # Role alone never grants all-task access. A stale generic resource grant
    # never overrides the current assignment or the explicit manager relation.
    if actor.role != "admin":
        return False
    return exists(select(TaskManager.task_id).where(
        TaskManager.task_id == TaskRecord.task_id, TaskManager.user_id == actor.id))


def _summary(row):
    task, assignment, assignee_name = row
    return {"task_id": str(task.task_id), "title": task.title,
            "assignee_id": assignment.assignee_id, "assignee_name": assignee_name,
            "task_status": task.task_status, "start_date": task.start_date.isoformat(),
            "due_date": task.due_date.isoformat()}


def list_tasks(actor, scope, status=None):
    if scope == "managed" and actor.role != "admin":
        raise AppError(403, "FORBIDDEN", "관리 업무를 조회할 권한이 없습니다.")
    allowed = TaskAssignment.assignee_id == actor.id if scope == "own" else _manager_condition(actor)
    query = _current_assignment_query().where(allowed)
    if status is not None:
        query = query.where(TaskRecord.task_status == status)
    # This initial draft explicitly returns the whole allowed list; it does not
    # pretend the first page is complete. Paging/search require a later contract.
    rows = get_db().execute(query.order_by(TaskRecord.due_date, TaskRecord.task_id)).all()
    return [_summary(row) for row in rows]


def get_task(actor, task_id):
    db = get_db()
    if db.scalar(select(TaskRecord.task_id).where(TaskRecord.task_id == task_id)) is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    query = _current_assignment_query().where(TaskRecord.task_id == task_id)
    row = db.execute(query.where(or_(
        TaskAssignment.assignee_id == actor.id, _manager_condition(actor)))).first()
    if row is None:
        raise AppError(403, "FORBIDDEN", "이 업무를 조회할 권한이 없습니다.")
    return _summary(row) | {"description": row[0].description}
