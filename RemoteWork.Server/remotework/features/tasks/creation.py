"""Member 2 development-only initial task creation.

The initial revision/version (1/1), accepted_at=None and creation response are
an explicit member 2 development contract accepted in member 1's technical reply.
This module neither applies DDL nor enables a shared deployment. It does not
accept tasks, restart work, submit results or select submission reviewers.
"""
from datetime import date
import json
import re

from flask import current_app, g, request

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...models import iso, utcnow
from ...write_contract import lock_users, positive_integer
from .models import TaskAssignment, TaskManager, TaskRecord
from .attachment_selection import validate_attachment_selection


INPUT_FIELDS = {"title", "description", "start_date", "due_date", "assignee_id", "manager_user_ids"}


def _date(value, label):
    if not isinstance(value, str) or re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value) is None:
        raise AppError(400, "INVALID_INPUT", f"{label}은(는) YYYY-MM-DD 형식이어야 합니다.")
    try:
        parsed = date.fromisoformat(value)
    except ValueError:
        raise AppError(400, "INVALID_INPUT", f"{label}을(를) 확인해 주세요.") from None
    if parsed.year < 1000:
        raise AppError(400, "INVALID_INPUT", f"{label}은(는) 1000~9999년 범위여야 합니다.")
    return parsed


def _unicode_text(value, label):
    if not isinstance(value, str):
        raise AppError(400, "INVALID_INPUT", f"{label}은(는) 문자열이어야 합니다.")
    try:
        value.encode("utf-8")
    except UnicodeEncodeError:
        raise AppError(400, "INVALID_INPUT", f"{label}의 문자 형식을 확인해 주세요.") from None


def validate_creation_body(body):
    """Validate a transport-independent body without normalizing its fingerprint."""
    if not isinstance(body, dict) or set(body) not in (INPUT_FIELDS, INPUT_FIELDS | {"attachment_ids"}):
        raise AppError(400, "INVALID_INPUT", "업무 등록 입력 항목을 확인해 주세요.")
    validate_attachment_selection(body)
    _unicode_text(body["title"], "업무 제목")
    if not body["title"].strip() or len(body["title"]) > 200:
        raise AppError(400, "INVALID_INPUT", "업무 제목은 공백만 제외하고 유니코드 문자 200개 이내로 입력해 주세요.")
    _unicode_text(body["description"], "업무 설명")
    start_date = _date(body["start_date"], "시작일")
    due_date = _date(body["due_date"], "마감일")
    if due_date < start_date:
        raise AppError(400, "INVALID_INPUT", "마감일은 시작일보다 빠를 수 없습니다.")
    assignee_id = positive_integer(body["assignee_id"], "assignee_id")
    manager_ids = body["manager_user_ids"]
    if not isinstance(manager_ids, list):
        raise AppError(400, "INVALID_INPUT", "업무 관리자 ID 목록을 확인해 주세요.")
    for user_id in manager_ids:
        positive_integer(user_id, "manager_user_ids")
    if len(set(manager_ids)) != len(manager_ids):
        raise AppError(400, "INVALID_INPUT", "업무 관리자 ID를 중복해서 전달할 수 없습니다.")
    # Keep the original body for the common request fingerprint. Server-owned
    # values and automatic creator inclusion must not rewrite that fingerprint.
    return body, start_date, due_date, assignee_id, manager_ids


def _input():
    if request.args:
        raise AppError(400, "INVALID_INPUT", "업무 등록에는 조회 조건을 전달하지 않습니다.")
    if not request.is_json:
        raise AppError(415, "JSON_REQUIRED", "JSON 형식으로 요청해 주세요.")
    def unique_object(pairs):
        result = {}
        for name, value in pairs:
            if name in result:
                raise ValueError("Duplicate JSON member")
            result[name] = value
        return result
    def reject_constant(_value):
        raise ValueError("Non-standard JSON constant")
    try:
        body = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=unique_object,
                          parse_constant=reject_constant)
    except (UnicodeError, ValueError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력 항목을 확인해 주세요.") from None
    return validate_creation_body(body)


def _creation_authorizer():
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    authorize = current_app.extensions.get("write_authorizers", {}).get("task.created")
    if not callable(authorize):
        raise AppError(503, "WRITE_ACTION_NOT_READY", "업무 등록 권한 검사가 아직 연결되지 않았습니다.")
    return authorize


def execute_creation(db, *, body, key):
    """Create once inside the caller's existing authenticated transaction.

    Both transports use this mutation. The caller retains commit/rollback and
    must not open a nested transaction or substitute another request DB session.
    """
    from ...db import get_db
    if not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the current authenticated_transaction for task creation")
    authorize = _creation_authorizer()
    if g.user.role != "admin":
        raise AppError(403, "FORBIDDEN", "접근 권한이 없습니다.")
    # Keep receipt registration behind the common opt-in, for either transport.
    from ...write_requests import WriteResult, perform_once
    body, start_date, due_date, assignee_id, requested_managers = validate_creation_body(body)

    def create(session):
        # Candidate checks belong to the first mutation, not to a replay of
        # an already committed request whose candidates may since change.
        manager_ids = sorted(set(requested_managers) | {g.user.id})
        users = lock_users(session, [assignee_id, *manager_ids])
        assignee = users.get(assignee_id)
        if assignee is None or not assignee.is_active or assignee.role not in ("employee", "admin"):
            raise AppError(409, "ASSIGNEE_UNAVAILABLE", "선택한 업무 담당자를 현재 배정할 수 없습니다.")
        for user_id in manager_ids:
            manager = users.get(user_id)
            if manager is None or not manager.is_active or manager.role != "admin":
                raise AppError(409, "TASK_MANAGER_UNAVAILABLE", "선택한 업무 관리자를 현재 지정할 수 없습니다.")

        now = utcnow()
        task = TaskRecord(
            title=body["title"], description=body["description"],
            task_status="not_started", start_date=start_date, due_date=due_date,
            current_assignment_revision=1, row_version=1,
            created_by=g.user.id, created_at=now, updated_at=now)
        session.add(task)
        session.flush()
        session.add(TaskAssignment(
            task_id=task.task_id, revision=1, assignee_id=assignee_id,
            assigned_by=g.user.id, assigned_at=now, accepted_at=None))
        for user_id in manager_ids:
            session.add(TaskManager(task_id=task.task_id, user_id=user_id))
        session.flush()
        if body.get("attachment_ids"):
            from .attachment_links import attach_created_task_files
            attach_created_task_files(session, task_id=task.task_id, attachment_ids=body["attachment_ids"])
        # Use the stored MySQL DATETIME precision for the first response as
        # well as later GETs and the common retained result.
        session.refresh(task)
        audit("task.created", "task", task.task_id, {
            "row_version": str(task.row_version),
            "assignment_revision": task.current_assignment_revision,
            "assignee_id": assignee_id, "manager_user_ids": manager_ids})
        return WriteResult({
            "task_id": str(task.task_id), "row_version": str(task.row_version),
            "assignment_revision": task.current_assignment_revision,
            "task_status": task.task_status, "updated_at": iso(task.updated_at)}, 201)

    return perform_once(db, key=key, action="task.created", target="new", body=body,
                        authorize=authorize, mutate=create)


@login_required
def create_task():
    with authenticated_transaction(roles=("admin",)) as db:
        _creation_authorizer()
        body, *_ = _input()
        result = execute_creation(db, body=body, key=request.headers.get("Idempotency-Key"))
    return success(result.data, result.status)
