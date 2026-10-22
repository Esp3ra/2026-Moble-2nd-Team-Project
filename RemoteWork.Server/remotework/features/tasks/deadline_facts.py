"""Member 2 current own-task facts for explicit deadline integrations.

This is an internal read port, not an alert source, HTTP DTO or event store.
Call only inside the caller's current authenticated_transaction. No commits,
event identities, server clock, confirmations, writes or registration occur.
"""
from dataclasses import dataclass
from datetime import date

from flask import g, has_request_context
from sqlalchemy import and_, or_, select

from ...common import AppError
from ...db import get_db
from ...write_contract import BIGINT_MAX, INTEGER_MAX, positive_bigint
from .lifecycle import _current
from .lifecycle_models import TaskCancellation
from .lifecycle_support import require_lifecycle_schema
from .models import TASK_STATUSES, TaskAssignment, TaskRecord


MAX_OWN_DEADLINE_TASKS = 1000


@dataclass(frozen=True)
class TaskDeadlineFact:
    """Copied DB values, including all five states and persisted cancellation.

    Integers and DATE stay native internally. The API owner must separately
    define serialization, same-TX server UTC, authoritative events and receipts.
    """
    task_id: int
    title: str
    assignee_id: int
    assignment_revision: int
    row_version: int
    task_status: str
    due_date: date
    is_cancelled: bool


def _guard(db):
    if (not has_request_context() or not getattr(g, "write_transaction_active", False)
            or db is not get_db()):
        raise RuntimeError("Use the caller's current authenticated_transaction session")


def _incomplete():
    raise AppError(503, "TASK_DEADLINE_FACTS_INCOMPLETE", "현재 본인 업무·배정·기한 자료의 정합성을 확인해 주세요.")


def _positive(value, maximum):
    return type(value) is int and 1 <= value <= maximum


def _fact(db, task_id):
    # Same parent -> current assignment -> managers -> sorted users order as
    # existing member 2 lifecycle operations. A managed-only relation is denied.
    task, assignment, _managers, _users = _current(db, str(task_id), mode="own")
    if (not _positive(task.task_id, BIGINT_MAX)
            or not _positive(task.row_version, BIGINT_MAX)
            or not _positive(assignment.revision, INTEGER_MAX)
            or assignment.revision != task.current_assignment_revision
            or not _positive(assignment.assignee_id, INTEGER_MAX)
            or assignment.assignee_id != g.user.id
            or task.task_status not in TASK_STATUSES
            or not isinstance(task.title, str) or not task.title.strip()
            or type(task.due_date) is not date or task.due_date.year < 1000
            or type(task.start_date) is not date or task.start_date > task.due_date):
        _incomplete()
    cancelled = db.scalar(select(TaskCancellation.task_id)
        .where(TaskCancellation.task_id == task.task_id).with_for_update()) is not None
    return TaskDeadlineFact(task_id=task.task_id, title=task.title,
        assignee_id=assignment.assignee_id, assignment_revision=assignment.revision,
        row_version=task.row_version, task_status=task.task_status,
        due_date=task.due_date, is_cancelled=cancelled)


def read_own_deadline_fact(db, task_id):
    """Recheck one exact current own task; absent404, non-own403, unready503.

    This returns current facts only, never evidence of a historical event or a
    successful confirmation. Call again inside the actual confirmation TX.
    """
    _guard(db)
    task_id = positive_bigint(str(task_id) if type(task_id) is int else task_id, "task_id")
    require_lifecycle_schema(db)
    return _fact(db, task_id)


def read_own_deadline_facts(db, *, max_tasks=MAX_OWN_DEADLINE_TASKS):
    """Complete current own set, sorted by task ID; no pagination/partial result.

    Actor is fixed to g.user from the authenticated TX, including administrators.
    All five states/cancellations are returned so the consumer can invalidate
    old candidates before applying A1 eligibility. No date window is inferred.
    More than max_tasks ->503, never a truncated/empty success. A missing current
    assignment anywhere makes ownership unknowable and fails the entire read.
    The current-read enumeration and per-task reads remain inside the caller's
    shared guard; all cooperating task writers must use that same guard.
    """
    _guard(db)
    if type(max_tasks) is not int or not 1 <= max_tasks <= MAX_OWN_DEADLINE_TASKS:
        raise ValueError("max_tasks must be an integer within the supported complete-read bound")
    # A missing optional storage must not masquerade as 'not cancelled', even
    # when flags are OFF or the actor has no tasks. Never initialize/repair it.
    require_lifecycle_schema(db)
    current = and_(TaskAssignment.task_id == TaskRecord.task_id,
                   TaskAssignment.revision == TaskRecord.current_assignment_revision)
    rows = db.execute(select(TaskRecord.task_id, TaskAssignment.assignee_id)
        .outerjoin(TaskAssignment, current)
        .where(or_(TaskAssignment.assignee_id == g.user.id, TaskAssignment.task_id.is_(None)))
        .order_by(TaskRecord.task_id).limit(max_tasks + 1).with_for_update()).all()
    if len(rows) > max_tasks:
        raise AppError(503, "TASK_DEADLINE_FACTS_LIMIT", "본인 업무 전체 조회 상한을 초과했습니다. 부분 목록을 사용하지 않습니다.")
    if any(owner is None for _task_id, owner in rows):
        _incomplete()
    return tuple(_fact(db, task_id) for task_id, _owner in rows)
