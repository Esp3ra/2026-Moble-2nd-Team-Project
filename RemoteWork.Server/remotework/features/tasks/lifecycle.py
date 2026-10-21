"""Opt-in member 2 cancellation and deadline requests.

Policy is approved. Execution remains closed until versioned member 1/3
deployment acknowledgment; no common authorizer or result implementation is
replaced. All domain, audit and receipt writes share the common transaction.
"""
from datetime import timedelta, timezone
import json

from flask import current_app, g, request
from sqlalchemy import select

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...models import iso, utcnow
from ...write_contract import BIGINT_MAX, ExpectedVersions, lock_users, positive_bigint
from .creation import _date, _unicode_text
from .lifecycle_models import EXTENSION_STATUSES, TaskCancellation, TaskDeadlineExtension
from .lifecycle_support import (ensure_not_cancelled, integration_acknowledged,
                                require_lifecycle_schema, require_lifecycle_write_ready)
from .models import TaskAssignment, TaskManager, TaskRecord


RESULT_FIELDS = frozenset({"task_id", "row_version", "assignment_revision", "task_status",
    "updated_at", "extension_id", "requested_due_date", "due_date"})
_VERSIONS = frozenset({"expected_row_version", "expected_assignment_revision"})
_FIELDS = {"task.cancelled": _VERSIONS | {"reason"},
    "task.deadline_extension_requested": _VERSIONS | {"requested_due_date", "reason"},
    "task.deadline_extension_approved": _VERSIONS,
    "task.deadline_extension_rejected": _VERSIONS | {"reason"},
    "task.deadline_extension_withdrawn": _VERSIONS}


def _current(db, target, *, mode="read"):
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
    own = assignment is not None and assignment.assignee_id == g.user.id
    managed = g.user.role == "admin" and g.user.id in managers
    if not (own if mode == "own" else managed if mode == "manage" else own or managed):
        raise AppError(403, "FORBIDDEN", "현재 업무 배정 또는 지정 관리 권한이 없습니다.")
    if assignment is None:
        raise AppError(503, "TASK_RELATION_INCOMPLETE", "현재 업무 배정의 정합성을 확인해 주세요.")
    # Every lifecycle writer follows parent -> assignment -> managers -> sorted
    # users -> request rows. Shared guard protects all cooperating transactions.
    users = lock_users(db, [assignment.assignee_id, *managers])
    return task, assignment, managers, users


def authorize_cancel_result(db, target):
    _current(db, target, mode="manage")
    require_lifecycle_schema(db)
    return True


def authorize_cancel(db, target):
    authorize_cancel_result(db, target)
    require_lifecycle_write_ready(db)
    return True


def authorize_decision_result(db, target):
    task, assignment, _managers, _users = _current(db, target, mode="manage")
    require_lifecycle_schema(db)
    # This also protects receipt lookup, whose shared target is the task ID.
    # Closed rows remain: a recorded success is still lookupable by a current
    # eligible manager, without re-running pending/state/date validation.
    found = db.scalar(select(TaskDeadlineExtension.extension_id).where(
        TaskDeadlineExtension.task_id == task.task_id,
        TaskDeadlineExtension.assignment_revision == assignment.revision,
        TaskDeadlineExtension.requested_by == assignment.assignee_id,
        TaskDeadlineExtension.requested_by != g.user.id).limit(1).with_for_update())
    if found is None:
        raise AppError(403, "FORBIDDEN", "본인의 연장 신청을 직접 판단할 수 없습니다.")
    return True


def authorize_decision(db, target):
    authorize_decision_result(db, target)
    require_lifecycle_write_ready(db)
    return True


def authorize_withdraw_result(db, target):
    task, assignment, _managers, _users = _current(db, target, mode="own")
    require_lifecycle_schema(db)
    found = db.scalar(select(TaskDeadlineExtension.extension_id).where(
        TaskDeadlineExtension.task_id == task.task_id,
        TaskDeadlineExtension.assignment_revision == assignment.revision,
        TaskDeadlineExtension.requested_by == g.user.id).limit(1).with_for_update())
    if found is None:
        raise AppError(403, "FORBIDDEN", "현재 본인의 연장 신청만 철회할 수 있습니다.")
    return True


def authorize_withdraw(db, target):
    authorize_withdraw_result(db, target)
    require_lifecycle_write_ready(db)
    return True


def authorize_request_result(db, target):
    _current(db, target, mode="own")
    require_lifecycle_schema(db)
    return True


def register_lifecycle_result_readers(app, *, read_only=False):
    from ...write_requests import register_write_result_reader
    for action, authorize in (("task.cancelled", authorize_cancel_result),
            ("task.deadline_extension_requested", authorize_request_result),
            ("task.deadline_extension_approved", authorize_decision_result),
            ("task.deadline_extension_rejected", authorize_decision_result),
            ("task.deadline_extension_withdrawn", authorize_withdraw_result)):
        register_write_result_reader(app, action=action, authorize=authorize, read_only=read_only)


def register_lifecycle_actions(app):
    from ...write_requests import register_write_action
    authorizers = app.extensions.setdefault("write_authorizers", {})
    # Built-in member 1 slots are installed only when no owner supplied one.
    for action in ("task.deadline_extension_approved", "task.deadline_extension_rejected"):
        authorizers.setdefault(action, authorize_decision)
    register_write_action(app, action="task.cancelled", authorize=authorize_cancel,
                          result_fields=RESULT_FIELDS)
    register_write_action(app, action="task.deadline_extension_withdrawn", authorize=authorize_withdraw,
                          result_fields=RESULT_FIELDS)


def _authorizer(action):
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    contract = current_app.extensions.get("write_action_contracts", {}).get(action)
    callback = (contract.authorize if contract is not None else
                current_app.extensions.get("write_authorizers", {}).get(action))
    if not callable(callback):
        raise AppError(503, "WRITE_ACTION_NOT_READY", "취소·연장용 현재 권한 검사가 연결되지 않았습니다.")
    return callback


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON field")
        result[key] = value
    return result


def _invalid_constant(_value):
    raise ValueError("Non-standard JSON value")


def _input(action):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "쓰기 요청에는 조회 조건을 전달하지 않습니다.")
    if not request.is_json:
        raise AppError(415, "JSON_REQUIRED", "JSON 형식으로 요청해 주세요.")
    try:
        body = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=_unique_object,
                          parse_constant=_invalid_constant)
    except (UnicodeError, ValueError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력을 확인해 주세요.") from None
    if not isinstance(body, dict) or set(body) != _FIELDS[action]:
        raise AppError(400, "INVALID_INPUT", "취소·연장 입력 항목을 확인해 주세요.")
    expected = ExpectedVersions.from_body(body)
    if "reason" in body:
        _unicode_text(body["reason"], "사유")
        if ((action != "task.deadline_extension_rejected" and not body["reason"].strip())
                or len(body["reason"].encode("utf-8")) > 60_000):
            raise AppError(400, "INVALID_INPUT", "사유는 공백만 제외하고 UTF-8 60,000바이트 이내로 입력해 주세요.")
    if "requested_due_date" in body:
        _date(body["requested_due_date"], "희망 마감일")
    return body, expected


def _today(now):
    return now.replace(tzinfo=timezone.utc).astimezone(timezone(timedelta(hours=9))).date()


def _pending(db, task, *, lock=True):
    query = select(TaskDeadlineExtension).where(TaskDeadlineExtension.task_id == task.task_id,
        TaskDeadlineExtension.assignment_revision == task.current_assignment_revision,
        TaskDeadlineExtension.status == "pending")
    if lock:
        query = query.with_for_update().execution_options(populate_existing=True)
    return db.scalar(query)


def _request_row(db, task, extension_id):
    row = db.scalar(select(TaskDeadlineExtension).where(TaskDeadlineExtension.extension_id == extension_id,
        TaskDeadlineExtension.task_id == task.task_id,
        TaskDeadlineExtension.assignment_revision == task.current_assignment_revision)
        .with_for_update().execution_options(populate_existing=True))
    if row is None:
        raise AppError(404, "TASK_EXTENSION_NOT_FOUND", "현재 배정의 연장 신청을 찾을 수 없습니다.")
    return row


def _result(task, extension):
    return {"task_id": str(task.task_id), "row_version": str(task.row_version),
        "assignment_revision": task.current_assignment_revision, "task_status": task.task_status,
        "updated_at": iso(task.updated_at),
        "extension_id": None if extension is None else str(extension.extension_id),
        "requested_due_date": None if extension is None else extension.requested_due_date.isoformat(),
        "due_date": task.due_date.isoformat()}


def _close(row, state, now, reason):
    row.status, row.decided_at, row.decided_by, row.decision_reason = state, now, g.user.id, reason


def _mutate(db, task, assignment, managers, users, action, body, extension_id, now):
    if action == "task.cancelled":
        if task.task_status not in ("not_started", "in_progress", "returned"):
            raise AppError(409, "TASK_STATE_CONFLICT", "현재 업무 상태에서는 취소할 수 없습니다.")
        db.add(TaskCancellation(task_id=task.task_id, cancelled_by=g.user.id,
                                reason=body["reason"], cancelled_at=now))
        pending = _pending(db, task)
        if pending is not None:
            _close(pending, "cancelled", now, body["reason"])
        return None
    if task.task_status not in ("in_progress", "returned"):
        raise AppError(409, "TASK_STATE_CONFLICT", "진행중 또는 반려완료 업무에서만 연장을 처리할 수 있습니다.")
    if action == "task.deadline_extension_requested":
        if _pending(db, task) is not None:
            raise AppError(409, "TASK_EXTENSION_PENDING", "처리 중인 연장 신청이 이미 있습니다.")
        if not any(user_id != g.user.id and user_id in users and users[user_id].is_active
                   and users[user_id].role == "admin" for user_id in managers):
            raise AppError(409, "TASK_EXTENSION_DECIDER_REQUIRED", "본인 외 활성 지정 관리자를 먼저 배정해 주세요.")
        due = _date(body["requested_due_date"], "희망 마감일")
        if due <= task.due_date or due < _today(now):
            raise AppError(409, "TASK_EXTENSION_DATE_INVALID", "희망 마감일은 현재 마감일보다 늦고 한국 시간 기준 오늘 이후여야 합니다.")
        row = TaskDeadlineExtension(task_id=task.task_id, assignment_revision=assignment.revision,
            original_due_date=task.due_date, requested_due_date=due, requested_by=g.user.id,
            reason=body["reason"], status="pending", created_at=now)
        db.add(row)
        db.flush()
        return row
    row = _request_row(db, task, extension_id)
    if row.status != "pending":
        raise AppError(409, "TASK_EXTENSION_NOT_PENDING", "이미 처리된 연장 신청입니다. 다시 조회해 주세요.")
    if row.original_due_date != task.due_date:
        raise AppError(409, "TASK_EXTENSION_BASE_CHANGED", "신청 당시 마감일과 현재 마감일이 달라 다시 확인해야 합니다.")
    if action == "task.deadline_extension_approved":
        if row.requested_due_date <= task.due_date or row.requested_due_date < _today(now):
            raise AppError(409, "TASK_EXTENSION_DATE_INVALID", "희망 마감일이 지났습니다. 반려 또는 철회 후 다시 신청해 주세요.")
        task.due_date = row.requested_due_date
        _close(row, "approved", now, None)
    elif action == "task.deadline_extension_rejected":
        _close(row, "rejected", now, body["reason"])
    else:
        _close(row, "withdrawn", now, None)
    return row


def _execute(target, action, raw_extension_id=None):
    authorize = _authorizer(action)
    with authenticated_transaction() as db:
        if authorize(db, target) is not True:
            raise AppError(403, "FORBIDDEN", "현재 요청을 수행할 권한이 없습니다.")
        # Always apply our current business relation even when member 1 supplied
        # a compatible built-in callback. Never override that callback.
        mode = "manage" if action in ("task.cancelled", "task.deadline_extension_approved",
                                      "task.deadline_extension_rejected") else "own"
        task, assignment, managers, users = _current(db, target, mode=mode)
        require_lifecycle_write_ready(db)
        extension_id = None if raw_extension_id is None else positive_bigint(raw_extension_id, "extension_id")
        if extension_id is not None:
            row = _request_row(db, task, extension_id)
            if (row.requested_by != assignment.assignee_id or
                    (mode == "manage" and row.requested_by == g.user.id) or
                    (mode == "own" and row.requested_by != g.user.id)):
                raise AppError(403, "FORBIDDEN", "현재 신청의 처리 권한이 없습니다.")
        body, expected = _input(action)
        # Shared keys target a task, so bind the request row in the internal
        # fingerprint too. Reusing a key on another request must conflict.
        fingerprint_body = body if extension_id is None else {**body, "extension_id": str(extension_id)}
        from ...write_requests import WriteResult, perform_once

        def mutate(session):
            task, assignment, managers, users = _current(session, target, mode=mode)
            expected.check(row_version=task.row_version, assignment_revision=assignment.revision)
            ensure_not_cancelled(session, task.task_id)
            if task.row_version >= BIGINT_MAX:
                raise AppError(409, "TASK_VERSION_EXHAUSTED", "업무 버전 상한에 도달했습니다.")
            before = {"row_version": str(task.row_version), "task_status": task.task_status,
                      "due_date": task.due_date.isoformat()}
            now = utcnow()
            extension = _mutate(session, task, assignment, managers, users, action, body, extension_id, now)
            task.row_version += 1
            task.updated_at = now
            session.flush()
            session.refresh(task)
            if extension is not None:
                session.refresh(extension)
            result = _result(task, extension)
            audit(action, "task", task.task_id, {"before": before, "after": result})
            return WriteResult(result)

        result = perform_once(db, key=request.headers.get("Idempotency-Key"), action=action,
            target=target, body=fingerprint_body, authorize=authorize, mutate=mutate)
    return success(result.data, result.status)


@login_required
def cancel_task(task_id):
    return _execute(task_id, "task.cancelled")


@login_required
def request_extension(task_id):
    return _execute(task_id, "task.deadline_extension_requested")


@login_required
def approve_extension(task_id, extension_id):
    return _execute(task_id, "task.deadline_extension_approved", extension_id)


@login_required
def reject_extension(task_id, extension_id):
    return _execute(task_id, "task.deadline_extension_rejected", extension_id)


@login_required
def withdraw_extension(task_id, extension_id):
    return _execute(task_id, "task.deadline_extension_withdrawn", extension_id)


def _serialize(row):
    return {"extension_id": str(row.extension_id), "task_id": str(row.task_id),
        "assignment_revision": row.assignment_revision, "requested_by": row.requested_by,
        "original_due_date": row.original_due_date.isoformat(), "requested_due_date": row.requested_due_date.isoformat(),
        "reason": row.reason, "status": row.status, "created_at": iso(row.created_at),
        "decided_at": None if row.decided_at is None else iso(row.decided_at),
        "decided_by": row.decided_by, "decision_reason": row.decision_reason}


def _query(allowed):
    if set(request.args) - allowed or any(len(values) != 1 for _, values in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "지원하지 않거나 중복된 조회 조건입니다.")


@login_required
def lifecycle_context(task_id):
    with authenticated_transaction() as db:
        _query(set())
        task, assignment, managers, _users = _current(db, task_id)
        require_lifecycle_schema(db)
        cancellation = db.get(TaskCancellation, task.task_id)
        pending = _pending(db, task)
        data = {"task_id": str(task.task_id), "row_version": str(task.row_version),
            "assignment_revision": assignment.revision, "task_status": task.task_status,
            "start_date": task.start_date.isoformat(), "due_date": task.due_date.isoformat(),
            "updated_at": iso(task.updated_at), "assignee_id": assignment.assignee_id,
            "manager_user_ids": managers, "is_cancelled": cancellation is not None,
            "cancellation": None if cancellation is None else {"cancelled_by": cancellation.cancelled_by,
                "cancelled_at": iso(cancellation.cancelled_at), "reason": cancellation.reason},
            "pending_extension": None if pending is None else _serialize(pending),
            "write_enabled": bool(current_app.config.get("ENABLE_COMMON_WRITE_DRAFT") and integration_acknowledged())}
    return success({"context": data})


def list_extension_requests(db, actor, scope, status=None):
    """Existing read policy; callers must first enter authenticated_transaction."""
    if scope not in ("own", "managed") or (status is not None and status not in EXTENSION_STATUSES):
        raise AppError(400, "INVALID_INPUT", "연장 요청의 범위 또는 상태를 확인해 주세요.")
    if scope == "managed" and actor.role != "admin":
        raise AppError(403, "FORBIDDEN", "지정 관리자만 관리 요청을 조회할 수 있습니다.")
    require_lifecycle_schema(db)
    query = select(TaskDeadlineExtension).join(TaskRecord,
        TaskRecord.task_id == TaskDeadlineExtension.task_id).join(TaskAssignment,
        (TaskAssignment.task_id == TaskRecord.task_id) &
        (TaskAssignment.revision == TaskRecord.current_assignment_revision)).where(
            TaskDeadlineExtension.assignment_revision == TaskRecord.current_assignment_revision)
    if scope == "own":
        query = query.where(TaskAssignment.assignee_id == actor.id,
                            TaskDeadlineExtension.requested_by == actor.id)
    else:
        query = query.join(TaskManager, (TaskManager.task_id == TaskRecord.task_id) &
                           (TaskManager.user_id == actor.id))
    if status is not None:
        query = query.where(TaskDeadlineExtension.status == status)
    rows = list(db.scalars(query.order_by(TaskDeadlineExtension.extension_id.desc())))
    return [_serialize(row) for row in rows]


@login_required
def extension_requests():
    with authenticated_transaction() as db:
        _query({"scope", "status"})
        scope, status = request.args.get("scope", "own"), request.args.get("status")
        data = {"items": list_extension_requests(db, g.user, scope, status)}
    return success(data)
