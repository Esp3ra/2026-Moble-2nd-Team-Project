"""Opt-in member 2 acceptance/rework, using the agreed member 1 write contract.

This module declares no tables and imports receipt support only when the common
write dependency is explicitly enabled. No submission/review rows are
read or changed. All business, audit and receipt writes share one transaction.
"""
from datetime import datetime, timezone
import json

from flask import current_app, g, request
from sqlalchemy import select

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...models import iso, utcnow
from ...write_contract import BIGINT_MAX, ExpectedVersions
from .models import TaskAssignment, TaskRecord
from .start_transition import (
    TaskStartAction, TaskStartDecision, TaskStartSnapshot, prepare_start_transition,
)


_FIELDS = frozenset({"expected_row_version", "expected_assignment_revision"})
_RESULT_FIELDS = frozenset({"task_id", "row_version", "assignment_revision", "task_status", "updated_at"})
_ACTIONS = {TaskStartAction.ACCEPT: "task.accepted", TaskStartAction.REWORK_START: "task.rework_started"}


def authorize_rework(db, target):
    # Keep the identical function object in the registered contract and request.
    from ...task_write_support import authorize_task
    return authorize_task(db, target)


def register_start_action(app):
    from ...write_requests import register_write_action
    register_write_action(app, action="task.rework_started", authorize=authorize_rework,
                          result_fields=_RESULT_FIELDS)


def _authorizer(action):
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    if action is TaskStartAction.ACCEPT:
        callback = current_app.extensions.get("write_authorizers", {}).get("task.accepted")
    else:
        contract = current_app.extensions.get("write_action_contracts", {}).get("task.rework_started")
        callback = None if contract is None else contract.authorize
        if callback is not authorize_rework:
            callback = None
    if not callable(callback):
        raise AppError(503, "WRITE_ACTION_NOT_READY", "업무 시작 권한 검사가 아직 연결되지 않았습니다.")
    return callback


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON member")
        result[key] = value
    return result


def _invalid_constant(_value):
    raise ValueError("Non-standard JSON constant")


def validate_start_body(body):
    """Validate exact transport-independent versions without changing the body."""
    if not isinstance(body, dict) or set(body) != _FIELDS:
        raise AppError(400, "INVALID_INPUT", "업무 버전과 배정 버전만 전달해 주세요.")
    return body, ExpectedVersions.from_body(body)


def _input():
    if request.args:
        raise AppError(400, "INVALID_INPUT", "업무 시작에는 조회 조건을 전달하지 않습니다.")
    if not request.is_json:
        raise AppError(415, "JSON_REQUIRED", "JSON 형식으로 요청해 주세요.")
    try:
        body = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=_unique_object,
                          parse_constant=_invalid_constant)
    except (UnicodeError, ValueError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력 항목을 확인해 주세요.") from None
    return validate_start_body(body)


def _utc_from_storage(value):
    """The existing MySQL DATETIME contract is UTC without timezone metadata."""
    if not isinstance(value, datetime) or value.tzinfo is not None:
        raise ValueError("Expected UTC-naive database timestamp")
    return value.replace(tzinfo=timezone.utc)


def _inconsistent():
    return AppError(409, "TASK_START_DATA_INCONSISTENT",
                    "업무 수락 기록의 정합성을 관리자에게 확인해 주세요.")


def _plan(task, assignment, expected, action, now):
    # Precedence belongs to the HTTP agreement, after CURRENT access is locked.
    # Only first mutation calls this function; retained success is not rechecked.
    expected.check(row_version=task.row_version,
                   assignment_revision=task.current_assignment_revision)
    required = "not_started" if action is TaskStartAction.ACCEPT else "returned"
    if task.task_status != required:
        raise AppError(409, "TASK_STATE_CONFLICT", "현재 업무 상태에서는 시작할 수 없습니다. 다시 조회해 주세요.")
    try:
        now_utc = _utc_from_storage(now)
        accepted = None if assignment.accepted_at is None else _utc_from_storage(assignment.accepted_at)
        if ((action is TaskStartAction.ACCEPT and accepted is not None) or
                (action is TaskStartAction.REWORK_START and accepted is None) or
                (accepted is not None and accepted > now_utc)):
            raise ValueError("Inconsistent first acceptance")
    except (TypeError, ValueError):
        raise _inconsistent() from None
    if task.row_version == BIGINT_MAX:
        raise AppError(409, "TASK_VERSION_EXHAUSTED", "업무 버전 상한에 도달했습니다. 관리자에게 확인해 주세요.")
    try:
        result = prepare_start_transition(TaskStartSnapshot(
            task_status=task.task_status, row_version=task.row_version,
            assignment_revision=task.current_assignment_revision,
            start_date=task.start_date, due_date=task.due_date, accepted_at=accepted),
            action=action, expected_assignment_revision=expected.assignment_revision,
            expected_row_version=expected.row_version, now_utc=now_utc)
    except (TypeError, ValueError):
        raise _inconsistent() from None
    if result.decision is TaskStartDecision.BEFORE_START:
        raise AppError(409, "TASK_START_BEFORE_START_DATE", "한국 시간 기준 업무 시작일 전에는 시작할 수 없습니다.")
    if result.decision is TaskStartDecision.AFTER_DUE:
        raise AppError(409, "TASK_START_AFTER_DUE_DATE", "한국 시간 기준 업무 마감일이 지나 시작할 수 없습니다.")
    if result.decision is not TaskStartDecision.READY:
        raise _inconsistent()
    return result.plan


def _locked_current(db, target):
    # authorize_task already validated the canonical positive BIGINT target and
    # acquired these locks. Repeat current reads to retain strong ORM references.
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == int(target))
                     .with_for_update().execution_options(populate_existing=True))
    if task is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(
        TaskAssignment.task_id == task.task_id,
        TaskAssignment.revision == task.current_assignment_revision)
        .with_for_update().execution_options(populate_existing=True))
    if assignment is None or assignment.assignee_id != g.user.id:
        raise AppError(403, "FORBIDDEN", "현재 업무를 배정받은 본인만 시작할 수 있습니다.")
    return task, assignment


def execute_start(db, *, target, action, body, key):
    """Start once in the caller's existing current-identity transaction.

    This is shared by the API and server-rendered web forms. The caller owns
    commit/rollback; no result submission, review or lifecycle write is added.
    """
    from ...db import get_db
    if not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the current authenticated_transaction for task starting")
    if not isinstance(action, TaskStartAction):
        raise ValueError("Use an explicit task start action")
    authorize = _authorizer(action)  # common-off never imports receipt support
    if authorize(db, target) is not True:
        raise AppError(403, "FORBIDDEN", "현재 업무를 시작할 권한이 없습니다.")
    body, expected = validate_start_body(body)
    from ...write_requests import WriteResult, perform_once

    def mutate(session):
        task, assignment = _locked_current(session, target)
        # A retained success may still be read, but a new start must never
        # bypass a persisted cancellation by switching the feature flag off.
        from .lifecycle_support import ensure_not_cancelled
        ensure_not_cancelled(session, task.task_id)
        plan = _plan(task, assignment, expected, action, utcnow())
        before = {"task_status": task.task_status, "row_version": str(task.row_version),
                  "assignment_revision": task.current_assignment_revision}
        task.task_status, task.row_version = plan.task_status, plan.row_version
        task.updated_at = plan.updated_at.replace(tzinfo=None)
        # Rework must preserve the exact original stored value.
        if action is TaskStartAction.ACCEPT:
            assignment.accepted_at = plan.accepted_at.replace(tzinfo=None)
        session.flush()
        session.refresh(task)  # Return the actual MySQL DATETIME precision.
        result = {"task_id": str(task.task_id), "row_version": str(task.row_version),
                  "assignment_revision": task.current_assignment_revision,
                  "task_status": task.task_status, "updated_at": iso(task.updated_at)}
        audit(_ACTIONS[action], "task", task.task_id, {"before": before, "after": {
            "task_status": task.task_status, "row_version": str(task.row_version),
            "assignment_revision": task.current_assignment_revision}})
        return WriteResult(result)

    return perform_once(db, key=key, action=_ACTIONS[action], target=target, body=body,
                        authorize=authorize, mutate=mutate)


def _start(target, action, *, body=None):
    authorize = _authorizer(action)  # Retain the API's common-off precedence.
    with authenticated_transaction() as db:
        if authorize(db, target) is not True:
            raise AppError(403, "FORBIDDEN", "현재 업무를 시작할 권한이 없습니다.")
        if body is None:
            body, _ = _input()
        else:
            body, _ = validate_start_body(body)
        result = execute_start(db, target=target, action=action, body=body,
                               key=request.headers.get("Idempotency-Key"))
    return success(result.data, result.status)


@login_required
def accept_task(task_id):
    return _start(task_id, TaskStartAction.ACCEPT)


@login_required
def start_rework(task_id):
    return _start(task_id, TaskStartAction.REWORK_START)
