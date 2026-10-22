"""Review proposal: original task/event/assignment carried in existing W1 target.

No default action/route registration, confirmation DDL or policy activation.
The event UUID remains unchanged; the target identifies one confirmation scope.
"""
from dataclasses import dataclass
import re
import uuid

from flask import g
from sqlalchemy import select

from .common import AppError
from .deadline_events import guard, read_current, read_event
from .features.tasks.deadline_facts import read_own_deadline_fact
from .features.tasks.models import TaskAssignment


@dataclass(frozen=True)
class DeadlineBinding:
    task_id: int
    event_id: str
    assignment_revision: int


def target(binding):
    if (type(binding) is not DeadlineBinding or type(binding.task_id) is not int
            or not 1 <= binding.task_id <= 9223372036854775807
            or type(binding.assignment_revision) is not int
            or not 1 <= binding.assignment_revision <= 2147483647
            or type(binding.event_id) is not str):
        raise ValueError("Exact task/event/assignment binding required")
    try:
        if str(uuid.UUID(binding.event_id)) != binding.event_id:
            raise ValueError()
    except ValueError:
        raise ValueError("Canonical event UUID required") from None
    # At most 71 ASCII chars: compatible with the EXISTING target column/scope.
    return f"d1:{binding.task_id}:{binding.event_id}:{binding.assignment_revision}"


def parse_target(value):
    if not isinstance(value, str) or len(value) > 80 or re.fullmatch(r"d1:[1-9][0-9]*:[0-9a-f-]{36}:[1-9][0-9]*", value) is None:
        raise AppError(400, "INVALID_INPUT", "원래 마감 확인 대상을 확인해 주세요.")
    _, task_id, event_id, revision = value.split(":")
    binding = DeadlineBinding(int(task_id), event_id, int(revision))
    try:
        if target(binding) != value:
            raise ValueError()
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "원래 마감 확인 대상을 확인해 주세요.") from None
    return binding


def current_binding(db, *, task_id):
    """Build a current confirmation scope after real own-task authorization."""
    guard(db)
    fact = read_own_deadline_fact(db, task_id)
    event = read_current(db, task_id=task_id)
    if event.source_row_version > fact.row_version:
        raise AppError(503, "DEADLINE_EVENT_SOURCE_MISMATCH", "현재 사건과 업무 버전을 확인해 주세요.")
    return DeadlineBinding(task_id, event.event_id, fact.assignment_revision)


def read_bound_event(db, value):
    """Authorize retained-result access even if no confirmation row exists.

    Current own + original assignment's actor + exact immutable task event.
    Call from registered W1 current-access authorizer; core separately checks
    the signed key's actor/action/target and expiry. This does NOT permit a new
    confirmation: mutate must also check current event/revision/state/versions.
    """
    guard(db)
    binding = parse_target(value)
    read_own_deadline_fact(db, binding.task_id)  # Current own, not a global scan.
    original = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == binding.task_id,
        TaskAssignment.revision == binding.assignment_revision).with_for_update()
        .execution_options(populate_existing=True))
    if original is None or original.assignee_id != g.user.id:
        raise AppError(403, "FORBIDDEN", "이 배정의 확인 결과를 조회할 권한이 없습니다.")
    event = read_event(db, event_id=binding.event_id)
    if event is None:
        raise AppError(404, "DEADLINE_EVENT_NOT_FOUND", "마감 사건을 찾을 수 없습니다.")
    if event.task_id != binding.task_id:
        raise AppError(403, "FORBIDDEN", "이 업무의 마감 사건이 아닙니다.")
    return binding, event


def authorize_result(db, value):
    read_bound_event(db, value)
    return True
