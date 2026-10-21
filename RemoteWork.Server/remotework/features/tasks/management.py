"""Member 2 opt-in task editing, reassignment and manager changes.

Uses the existing current-manager authorizers, guard and receipt transaction.
No DDL, result ORM imports, reviewer updates, cancellation or extension writes.
"""
from datetime import timedelta, timezone
import json
import os

from flask import current_app, g, request
from sqlalchemy import inspect, select, text

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...models import iso, utcnow
from ...write_contract import BIGINT_MAX, INTEGER_MAX, ExpectedVersions, lock_users, positive_integer
from .creation import _date, _unicode_text
from .models import TaskAssignment, TaskManager, TaskRecord


_VERSIONS = frozenset({"expected_row_version", "expected_assignment_revision"})
_FIELDS = {
    "task.updated": _VERSIONS | {"title", "description", "start_date", "due_date"},
    "task.reassigned": _VERSIONS | {"assignee_id"},
    "task.managers_changed": _VERSIONS | {"manager_user_ids"},
}


def _authorizer(action):
    # Importing this feature with common writes disabled never registers receipts.
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    authorize = current_app.extensions.get("write_authorizers", {}).get(action)
    if not callable(authorize):
        raise AppError(503, "WRITE_ACTION_NOT_READY", "업무 관리 권한 검사가 아직 연결되지 않았습니다.")
    return authorize


def _unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON member")
        result[key] = value
    return result


def _reject_constant(_value):
    raise ValueError("Non-standard JSON constant")


def validate_management_body(action, body):
    """Validate exact values shared by API and web without changing the body."""
    if action not in _FIELDS:
        raise ValueError("Use an explicit task management action")
    if not isinstance(body, dict) or set(body) != _FIELDS[action]:
        raise AppError(400, "INVALID_INPUT", "업무 관리 입력 항목을 확인해 주세요.")
    expected = ExpectedVersions.from_body(body)
    if action == "task.updated":
        _unicode_text(body["title"], "업무 제목")
        if not body["title"].strip() or len(body["title"]) > 200:
            raise AppError(400, "INVALID_INPUT", "업무 제목은 공백만 제외하고 유니코드 문자 200개 이내로 입력해 주세요.")
        _unicode_text(body["description"], "업무 설명")
        start, due = _date(body["start_date"], "시작일"), _date(body["due_date"], "마감일")
        if due < start:
            raise AppError(400, "INVALID_INPUT", "마감일은 시작일보다 빠를 수 없습니다.")
    elif action == "task.reassigned":
        positive_integer(body["assignee_id"], "assignee_id")
    else:
        ids = body["manager_user_ids"]
        if not isinstance(ids, list) or not ids:
            raise AppError(400, "INVALID_INPUT", "업무 관리자를 최소 한 명 지정해 주세요.")
        for user_id in ids:
            positive_integer(user_id, "manager_user_ids")
        if len(set(ids)) != len(ids):
            raise AppError(400, "INVALID_INPUT", "업무 관리자 ID를 중복해서 전달할 수 없습니다.")
    # The original immutable JSON values are the common receipt fingerprint.
    return body, expected


def _input(action):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "업무 관리에는 조회 조건을 전달하지 않습니다.")
    if not request.is_json:
        raise AppError(415, "JSON_REQUIRED", "JSON 형식으로 요청해 주세요.")
    try:
        body = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=_unique_object,
                          parse_constant=_reject_constant)
    except (UnicodeError, ValueError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력 항목을 확인해 주세요.") from None
    return validate_management_body(action, body)


def _inconsistent():
    return AppError(409, "TASK_MANAGEMENT_DATA_INCONSISTENT",
                    "업무 배정·수락·과거 제출 자료의 정합성을 관리자에게 확인해 주세요.")


def _locked_current(db, target, expected, action):
    # The current-manager authorizer already took parent/assignment/manager locks.
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == int(target))
                     .with_for_update().execution_options(populate_existing=True))
    if task is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == task.task_id,
        TaskAssignment.revision == task.current_assignment_revision).with_for_update()
        .execution_options(populate_existing=True))
    if assignment is None:
        raise AppError(503, "TASK_RELATION_INCOMPLETE", "현재 업무 배정의 정합성을 확인해 주세요.")
    expected.check(row_version=task.row_version, assignment_revision=task.current_assignment_revision)
    allowed = ("not_started", "in_progress", "returned") if action == "task.managers_changed" else ("not_started",)
    if task.task_status not in allowed:
        raise AppError(409, "TASK_STATE_CONFLICT", "현재 업무 상태에서는 변경할 수 없습니다. 다시 조회해 주세요.")
    if task.row_version >= BIGINT_MAX:
        raise AppError(409, "TASK_VERSION_EXHAUSTED", "업무 버전 상한에 도달했습니다. 관리자에게 확인해 주세요.")
    return task, assignment


def _unstarted_history(db, task):
    # Successful submissions must never be discarded to repair a corrupt state.
    # Keep results feature/model metadata cold: inspect and query the existing
    # table with fixed SQL and bound values, without importing member 3's ORM.
    if db.scalar(select(TaskAssignment.revision).where(TaskAssignment.task_id == task.task_id,
            TaskAssignment.accepted_at.is_not(None)).limit(1).with_for_update()) is not None:
        raise _inconsistent()
    inspector = inspect(db.connection())
    if not inspector.has_table("result_submissions"):
        enabled = current_app.config.get("ENABLE_RESULTS_DRAFT",
                                         os.getenv("REMOTEWORK_ENABLE_RESULTS_DRAFT") == "1")
        if enabled:
            raise AppError(503, "TASK_RESULTS_SCHEMA_REQUIRED", "과거 결과물 조회 테이블의 준비 상태를 확인해 주세요.")
        return  # A results-disabled, member-2-only development database is valid.
    columns = {column["name"] for column in inspector.get_columns("result_submissions")}
    if not {"id", "task_id", "assignment_revision", "round_no", "status"}.issubset(columns):
        raise AppError(503, "TASK_RESULTS_SCHEMA_REQUIRED", "과거 결과물 조회 테이블의 준비 상태를 확인해 주세요.")
    found = db.execute(text("SELECT task_id FROM result_submissions "
        "WHERE task_id = :task_id LIMIT 1 FOR UPDATE"), {"task_id": task.task_id}).first()
    if found is not None:
        raise _inconsistent()


def _no_change():
    raise AppError(409, "TASK_CHANGE_NOT_NEEDED", "현재 정보와 같은 변경입니다. 다시 조회해 주세요.")


def _update(db, task, assignment, body, now):
    _unstarted_history(db, task)
    start, due = _date(body["start_date"], "시작일"), _date(body["due_date"], "마감일")
    today = now.replace(tzinfo=timezone.utc).astimezone(timezone(timedelta(hours=9))).date()
    if due < today:
        raise AppError(409, "TASK_DUE_DATE_IN_PAST", "새 마감일은 한국 시간 기준 오늘 또는 그 이후여야 합니다.")
    changed = [name for name, value in (("title", body["title"]), ("description", body["description"]),
        ("start_date", start), ("due_date", due)) if getattr(task, name) != value]
    if not changed:
        _no_change()
    detail = {"changed_fields": changed, "before_start_date": task.start_date.isoformat(),
              "before_due_date": task.due_date.isoformat(), "start_date": start.isoformat(),
              "due_date": due.isoformat()}
    task.title, task.description = body["title"], body["description"]
    task.start_date, task.due_date = start, due
    return detail


def _reassign(db, task, assignment, body, now):
    _unstarted_history(db, task)
    new_id = body["assignee_id"]
    users = lock_users(db, [new_id])
    candidate = users.get(new_id)
    if candidate is None or not candidate.is_active or candidate.role not in ("employee", "admin"):
        raise AppError(409, "ASSIGNEE_UNAVAILABLE", "선택한 업무 담당자를 현재 배정할 수 없습니다.")
    if new_id == assignment.assignee_id:
        _no_change()
    if task.current_assignment_revision >= INTEGER_MAX:
        raise AppError(409, "TASK_ASSIGNMENT_EXHAUSTED", "배정 차수 상한에 도달했습니다. 관리자에게 확인해 주세요.")
    next_revision = task.current_assignment_revision + 1
    # Do not collide with or erase an already-existing, out-of-order assignment.
    if db.scalar(select(TaskAssignment.revision).where(TaskAssignment.task_id == task.task_id,
            TaskAssignment.revision >= next_revision).limit(1).with_for_update()) is not None:
        raise _inconsistent()
    db.add(TaskAssignment(task_id=task.task_id, revision=next_revision, assignee_id=new_id,
        assigned_by=g.user.id, assigned_at=now, accepted_at=None))
    task.current_assignment_revision = next_revision
    return {"before_assignee_id": assignment.assignee_id, "assignee_id": new_id}


def _managers(db, task, assignment, body, now):
    manager_ids = sorted(body["manager_user_ids"])
    users = lock_users(db, manager_ids)
    if any(user_id not in users or not users[user_id].is_active or users[user_id].role != "admin"
           for user_id in manager_ids):
        raise AppError(409, "TASK_MANAGER_UNAVAILABLE", "활성 관리자만 업무 관리자로 지정할 수 있습니다.")
    rows = list(db.scalars(select(TaskManager).where(TaskManager.task_id == task.task_id)
                          .order_by(TaskManager.user_id).with_for_update()
                          .execution_options(populate_existing=True)))
    previous = [row.user_id for row in rows]
    if previous == manager_ids:
        _no_change()
    for row in rows:
        if row.user_id not in manager_ids:
            db.delete(row)
    for user_id in manager_ids:
        if user_id not in previous:
            db.add(TaskManager(task_id=task.task_id, user_id=user_id))
    return {"before_manager_user_ids": previous, "manager_user_ids": manager_ids}


def execute_management(db, *, target, action, body, key):
    """Manage once in the caller's current authenticated transaction.

    API and server-rendered web forms use this same mutation and receipt.
    The caller owns commit/rollback; no result or reviewer write is added.
    """
    from ...db import get_db
    if not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the current authenticated_transaction for task management")
    if action not in _FIELDS:
        raise ValueError("Use an explicit task management action")
    authorize = _authorizer(action)
    if g.user.role != "admin" or authorize(db, target) is not True:
        raise AppError(403, "FORBIDDEN", "현재 업무를 관리할 권한이 없습니다.")
    body, expected = validate_management_body(action, body)
    from ...write_requests import WriteResult, perform_once

    def mutate(session):
        task, assignment = _locked_current(session, target, expected, action)
        from .lifecycle_support import ensure_not_cancelled
        ensure_not_cancelled(session, task.task_id)
        before = {"task_status": task.task_status, "row_version": str(task.row_version),
                  "assignment_revision": task.current_assignment_revision}
        now = utcnow()
        change = {"task.updated": _update, "task.reassigned": _reassign,
                  "task.managers_changed": _managers}[action](session, task, assignment, body, now)
        task.row_version += 1
        task.updated_at = now
        session.flush()
        session.refresh(task)
        result = {"task_id": str(task.task_id), "row_version": str(task.row_version),
                  "assignment_revision": task.current_assignment_revision,
                  "task_status": task.task_status, "updated_at": iso(task.updated_at)}
        audit(action, "task", task.task_id, {"before": before, "after": {
            "task_status": task.task_status, "row_version": str(task.row_version),
            "assignment_revision": task.current_assignment_revision}, **change})
        return WriteResult(result)

    return perform_once(db, key=key, action=action, target=target, body=body,
                        authorize=authorize, mutate=mutate)


def _manage(target, action):
    authorize = _authorizer(action)
    with authenticated_transaction(roles=("admin",)) as db:
        if authorize(db, target) is not True:
            raise AppError(403, "FORBIDDEN", "현재 업무를 관리할 권한이 없습니다.")
        body, _ = _input(action)
        result = execute_management(db, target=target, action=action, body=body,
                                    key=request.headers.get("Idempotency-Key"))
    return success(result.data, result.status)


@login_required
def update_task(task_id):
    return _manage(task_id, "task.updated")


@login_required
def reassign_task(task_id):
    return _manage(task_id, "task.reassigned")


@login_required
def change_managers(task_id):
    return _manage(task_id, "task.managers_changed")
