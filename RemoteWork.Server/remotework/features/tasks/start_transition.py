"""Pure member 2 acceptance/rework plans; no authorization or persistence.

The caller must already have authenticated the user, checked current assignment
ownership and locked the current task/assignment. A ready plan is only data: it
does not update a record or establish an HTTP contract. All instants are explicit
UTC-aware datetimes. A future DB adapter must handle UTC-naive storage explicitly.
"""
from dataclasses import dataclass
from datetime import date, datetime, timedelta
from enum import Enum

from .start_dates import TaskStartPeriod, evaluate_start_period


_MAX_ROW_VERSION = (1 << 63) - 1
_MAX_ASSIGNMENT_REVISION = (1 << 31) - 1
_TASK_STATUSES = frozenset({"not_started", "in_progress", "submitted", "approved", "returned"})


class TaskStartAction(Enum):
    ACCEPT = "accept"
    REWORK_START = "rework_start"


class TaskStartDecision(Enum):
    READY = "ready"
    ASSIGNMENT_CHANGED = "assignment_changed"
    TASK_CHANGED = "task_changed"
    STATE_NOT_ALLOWED = "state_not_allowed"
    BEFORE_START = "before_start"
    AFTER_DUE = "after_due"


@dataclass(frozen=True)
class TaskStartSnapshot:
    task_status: str
    row_version: int
    assignment_revision: int
    start_date: date
    due_date: date
    accepted_at: datetime | None


@dataclass(frozen=True)
class TaskStartPlan:
    task_status: str
    row_version: int
    assignment_revision: int
    accepted_at: datetime
    updated_at: datetime


@dataclass(frozen=True)
class TaskStartResult:
    decision: TaskStartDecision
    plan: TaskStartPlan | None = None


def _positive_integer(name: str, value: int, maximum: int) -> None:
    if type(value) is not int:
        raise TypeError(f"{name} must be an integer, not a boolean or string")
    if not 1 <= value <= maximum:
        raise ValueError(f"{name} is outside the positive signed database range")


def _utc_instant(name: str, value: datetime) -> None:
    if not isinstance(value, datetime):
        raise TypeError(f"{name} must be a UTC-aware datetime")
    if value.tzinfo is None or value.utcoffset() != timedelta(0):
        raise ValueError(f"{name} must be timezone-aware with UTC offset 00:00")
    if value.year < 1000:
        raise ValueError(f"{name} must be within MySQL DATETIME years 1000..9999")


def prepare_start_transition(snapshot: TaskStartSnapshot, *, action: TaskStartAction,
                             expected_assignment_revision: int,
                             expected_row_version: int,
                             now_utc: datetime) -> TaskStartResult:
    """Validate a snapshot and return an immutable, unapplied transition plan.

    For valid inputs, assignment conflicts take precedence over row conflicts,
    followed by state and Korean calendar eligibility. Invalid or inconsistent
    data raises TypeError/ValueError; these exceptions are not HTTP error choices.
    No missing accepted_at is repaired and no persisted state is changed.
    """
    if not isinstance(snapshot, TaskStartSnapshot):
        raise TypeError("snapshot must be a TaskStartSnapshot")
    if not isinstance(action, TaskStartAction):
        raise TypeError("action must be a TaskStartAction")
    if type(snapshot.task_status) is not str:
        raise TypeError("task_status must be a string")
    if snapshot.task_status not in _TASK_STATUSES:
        raise ValueError("task_status must be one of the five task states")
    _positive_integer("row_version", snapshot.row_version, _MAX_ROW_VERSION)
    _positive_integer("expected_row_version", expected_row_version, _MAX_ROW_VERSION)
    _positive_integer("assignment_revision", snapshot.assignment_revision,
                      _MAX_ASSIGNMENT_REVISION)
    _positive_integer("expected_assignment_revision", expected_assignment_revision,
                      _MAX_ASSIGNMENT_REVISION)
    _utc_instant("now_utc", now_utc)
    if snapshot.accepted_at is not None:
        _utc_instant("accepted_at", snapshot.accepted_at)
    if snapshot.task_status == "not_started" and snapshot.accepted_at is not None:
        raise ValueError("not_started must not have accepted_at")
    if snapshot.task_status == "returned" and snapshot.accepted_at is None:
        raise ValueError("returned must preserve an existing accepted_at")
    if snapshot.accepted_at is not None and snapshot.accepted_at > now_utc:
        raise ValueError("accepted_at must not be later than now_utc")

    period = evaluate_start_period(snapshot.start_date, snapshot.due_date, now_utc=now_utc)
    if expected_assignment_revision != snapshot.assignment_revision:
        return TaskStartResult(TaskStartDecision.ASSIGNMENT_CHANGED)
    if expected_row_version != snapshot.row_version:
        return TaskStartResult(TaskStartDecision.TASK_CHANGED)
    required_status = "not_started" if action is TaskStartAction.ACCEPT else "returned"
    if snapshot.task_status != required_status:
        return TaskStartResult(TaskStartDecision.STATE_NOT_ALLOWED)
    if period.period is TaskStartPeriod.BEFORE_START:
        return TaskStartResult(TaskStartDecision.BEFORE_START)
    if period.period is TaskStartPeriod.AFTER_DUE:
        return TaskStartResult(TaskStartDecision.AFTER_DUE)
    if snapshot.row_version == _MAX_ROW_VERSION:
        raise ValueError("row_version cannot be incremented beyond signed BIGINT")

    accepted_at = now_utc if action is TaskStartAction.ACCEPT else snapshot.accepted_at
    return TaskStartResult(TaskStartDecision.READY, TaskStartPlan(
        task_status="in_progress", row_version=snapshot.row_version + 1,
        assignment_revision=snapshot.assignment_revision,
        accepted_at=accepted_at, updated_at=now_utc))
