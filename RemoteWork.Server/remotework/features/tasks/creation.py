"""Member 2 development-only initial task creation.

The initial revision/version (1/1), accepted_at=None and creation response are
an explicit member 2 development contract pending member 1 joint review.
This module neither applies DDL nor enables a shared deployment. It does not
accept tasks, restart work, submit results or select submission reviewers.
"""
from datetime import date
import re

from flask import current_app, g, request

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, payload, success
from ...models import iso, utcnow
from ...write_contract import lock_users, positive_integer
from .models import TaskAssignment, TaskManager, TaskRecord


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


def _input():
    if request.args:
        raise AppError(400, "INVALID_INPUT", "업무 등록에는 조회 조건을 전달하지 않습니다.")
    body = payload()
    if set(body) != INPUT_FIELDS:
        raise AppError(400, "INVALID_INPUT", "업무 등록 입력 항목을 확인해 주세요.")
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


def _creation_authorizer():
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    authorize = current_app.extensions.get("write_authorizers", {}).get("task.created")
    if not callable(authorize):
        raise AppError(503, "WRITE_ACTION_NOT_READY", "업무 등록 권한 검사가 아직 연결되지 않았습니다.")
    return authorize


@login_required
def create_task():
    with authenticated_transaction(roles=("admin",)) as db:
        authorize = _creation_authorizer()
        # The shared write flag owns receipt model registration. Do not import
        # that module merely because the task creation blueprint was enabled.
        from ...write_requests import WriteResult, perform_once
        body, start_date, due_date, assignee_id, requested_managers = _input()

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

        result = perform_once(
            db, key=request.headers.get("Idempotency-Key"), action="task.created",
            target="new", body=body, authorize=authorize, mutate=create)
    return success(result.data, result.status)
