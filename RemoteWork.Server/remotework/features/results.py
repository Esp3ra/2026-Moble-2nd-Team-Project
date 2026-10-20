"""Member 3 results feature. Opt-in only; no DDL or seed runs on import.
Includes explicit local development reviewer/extension settings; final DB can replace one adapter.
"""
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
import hashlib
import base64
import binascii
import json
import click
import os
import uuid

from flask import Blueprint, current_app, g, request, send_file
from sqlalchemy import (Boolean, BigInteger, CheckConstraint, DateTime, ForeignKey,
                        ForeignKeyConstraint, Index, Integer, String, Text, select, cast, or_, and_)
from sqlalchemy.orm import Mapped, mapped_column

from ..auth import authenticated_transaction, login_required
from ..common import AppError, audit, payload, success
from ..db import get_db, transaction
from ..models import Base, AdminGuard, AuditEvent, User, SchemaVersion, iso, utcnow
from ..write_contract import (BIGINT_MAX, ExpectedVersions, lock_users,
                              positive_bigint, positive_integer)
from ..write_requests import WriteResult, perform_once, register_write_action
from .tasks.models import TaskRecord, TaskAssignment, TaskManager, TASK_ID_TYPE

bp = Blueprint("results", __name__, url_prefix="/api/v1/results")
MAX_FILE_BYTES = 25 * 1024 * 1024
MAX_FILES = 10
TABLE_OPTIONS = {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4",
                 "mysql_collate": "utf8mb4_unicode_ci"}


class Submission(Base):
    __tablename__ = "result_submissions"
    id: Mapped[int] = mapped_column(TASK_ID_TYPE, primary_key=True, autoincrement=True)
    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"))
    assignment_revision: Mapped[int] = mapped_column(Integer)
    round_no: Mapped[int] = mapped_column(BigInteger)
    submitted_by: Mapped[int] = mapped_column(ForeignKey("users.id"))
    reviewer_at_submission: Mapped[int] = mapped_column(ForeignKey("users.id"))
    description: Mapped[str] = mapped_column(Text)
    status: Mapped[str] = mapped_column(String(16), default="submitted")
    submitted_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (
        ForeignKeyConstraint(["task_id", "assignment_revision"],
                             ["task_assignments.task_id", "task_assignments.revision"]),
        CheckConstraint("round_no > 0", name="ck_result_round"),
        CheckConstraint("HEX(status) IN (HEX('submitted'),HEX('approved'),HEX('returned'))",
                        name="ck_result_submission_status"),
        Index("uq_result_task_round", "task_id", "round_no", unique=True),
        TABLE_OPTIONS,
    )


class Decision(Base):
    __tablename__ = "result_decisions"
    id: Mapped[int] = mapped_column(TASK_ID_TYPE, primary_key=True, autoincrement=True)
    submission_id: Mapped[int] = mapped_column(ForeignKey("result_submissions.id"), unique=True)
    reviewer_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    outcome: Mapped[str] = mapped_column(String(16))
    reason: Mapped[str] = mapped_column(Text)
    decided_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (
        CheckConstraint("HEX(outcome) IN (HEX('approved'),HEX('returned'))",
                        name="ck_result_decision"),
        TABLE_OPTIONS,
    )


class Attachment(Base):
    __tablename__ = "result_attachments"
    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"))
    owner_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    submission_id: Mapped[int | None] = mapped_column(ForeignKey("result_submissions.id"), nullable=True)
    name: Mapped[str] = mapped_column(String(255))
    size: Mapped[int] = mapped_column(BigInteger, default=0)
    sha256: Mapped[str] = mapped_column(String(64), default="")
    state: Mapped[str] = mapped_column(String(16), default="uploading")
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    expires_at: Mapped[datetime] = mapped_column(DateTime)
    __table_args__ = (
        CheckConstraint("size >= 0 AND size <= 26214400", name="ck_result_file_size"),
        CheckConstraint("HEX(state) IN (HEX('uploading'),HEX('ready'),HEX('deleting'))",
                        name="ck_result_file_state"),
        Index("ix_result_file_expiry", "submission_id", "expires_at"),
        TABLE_OPTIONS,
    )


class ResultEvent(Base):
    __tablename__ = "result_events"
    id: Mapped[int] = mapped_column(TASK_ID_TYPE, primary_key=True, autoincrement=True)
    operation_id: Mapped[str] = mapped_column(String(36), unique=True)
    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"))
    assignment_revision: Mapped[int] = mapped_column(Integer)
    submission_id: Mapped[int] = mapped_column(ForeignKey("result_submissions.id"))
    actor_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    kind: Mapped[str] = mapped_column(String(24))
    summary: Mapped[str] = mapped_column(String(200))
    occurred_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (
        ForeignKeyConstraint(["task_id", "assignment_revision"],
                             ["task_assignments.task_id", "task_assignments.revision"]),
        Index("ix_result_event_task_id", "task_id", "id"),
        TABLE_OPTIONS,
    )



class DepartmentReviewer(Base):
    """Explicitly registered department head; no rank is inferred from a name."""
    __tablename__ = "result_department_reviewers"
    department: Mapped[str] = mapped_column(String(80), primary_key=True)
    reviewer_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    __table_args__ = (TABLE_OPTIONS,)


class UpperReviewer(Base):
    __tablename__ = "result_upper_reviewers"
    submitter_id: Mapped[int] = mapped_column(ForeignKey("users.id"), primary_key=True)
    reviewer_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    __table_args__ = (
        CheckConstraint("submitter_id <> reviewer_id", name="ck_result_upper_other"),
        TABLE_OPTIONS,
    )


class SubmissionGate(Base):
    """Local development adapter for pending extensions, until member 2 binds its model."""
    __tablename__ = "result_submission_gates"
    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"), primary_key=True)
    extension_pending: Mapped[bool] = mapped_column(Boolean)
    __table_args__ = (TABLE_OPTIONS,)


class UploadChunk(Base):
    __tablename__ = "result_upload_chunks"
    attachment_id: Mapped[str] = mapped_column(ForeignKey("result_attachments.id"), primary_key=True)
    offset: Mapped[int] = mapped_column(Integer, primary_key=True)
    size: Mapped[int] = mapped_column(Integer)
    sha256: Mapped[str] = mapped_column(String(64))
    __table_args__ = (
        CheckConstraint("offset >= 0 AND size > 0 AND size <= 16384", name="ck_result_chunk"),
        TABLE_OPTIONS,
    )

@dataclass(frozen=True)
class ExternalFacts:
    reviewer_id: int | None
    extension_pending: bool | None = None



def local_facts(db, task, assignment, *, lock=False):
    if current_app.config.get("RESULTS_CONTEXT_MODE") != "local-development":
        _error(503, "RESULTS_CONTEXT_MODE_REQUIRED",
               "개발용 관계 저장소 또는 최종 조직/연장 조회 어댑터를 설정해 주세요.")
    user = db.get(User, assignment.assignee_id)
    if user is None or not user.is_active:
        return ExternalFacts(None, False)
    query = (select(UpperReviewer).where(UpperReviewer.submitter_id == user.id)
             if user.role == "admin" else
             select(DepartmentReviewer).where(DepartmentReviewer.department == user.department))
    if lock:
        query = query.with_for_update()
    relation = db.scalar(query.execution_options(populate_existing=True))
    gate_query = select(SubmissionGate).where(SubmissionGate.task_id == task.task_id)
    if lock:
        gate_query = gate_query.with_for_update()
    gate = db.scalar(gate_query.execution_options(populate_existing=True))
    # This explicitly selected development store has no pending request when no gate exists.
    # It is never used as an implicit fallback for an unknown production extension model.
    return ExternalFacts(None if relation is None else relation.reviewer_id,
                         False if gate is None else gate.extension_pending)

def _error(status, code, message):
    raise AppError(status, code, message)


def _uuid(value):
    if not isinstance(value, str):
        _error(400, "INVALID_INPUT", "첨부 ID를 확인해 주세요.")
    try:
        parsed = uuid.UUID(value)
    except (ValueError, AttributeError):
        _error(400, "INVALID_INPUT", "첨부 ID를 확인해 주세요.")
    if str(parsed) != value:
        _error(400, "INVALID_INPUT", "정규 UUID 첨부 ID가 필요합니다.")
    return value


def _fields(names, body=None):
    body = payload() if body is None else body
    if not isinstance(body, dict) or set(body) != set(names):
        _error(400, "INVALID_INPUT", "입력 항목을 확인해 주세요.")
    return body


def _text(value, *, required):
    if not isinstance(value, str) or (required and not value.strip()):
        _error(400, "INVALID_INPUT", "필수 설명 또는 반려 사유를 입력해 주세요.")
    try:
        value.encode("utf-8")
    except UnicodeEncodeError:
        _error(400, "INVALID_INPUT", "문자 형식을 확인해 주세요.")
    return value


def _current(db, task_id):
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id)
                     .with_for_update().execution_options(populate_existing=True))
    if task is None:
        _error(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(
        TaskAssignment.task_id == task_id,
        TaskAssignment.revision == task.current_assignment_revision)
        .with_for_update().execution_options(populate_existing=True))
    if assignment is None:
        _error(409, "TASK_ASSIGNMENT_CHANGED", "현재 업무 배정을 확인해 주세요.")
    managers = list(db.scalars(select(TaskManager).where(TaskManager.task_id == task_id)
        .order_by(TaskManager.user_id).with_for_update()
        .execution_options(populate_existing=True)))
    return task, assignment, [m.user_id for m in managers]


def _facts(db, task, assignment, managers):
    # This adapter must read the current organization/reviewer relationship and
    # extension status from members 1/2. It must not call a remote API in this TX.
    binding = current_app.extensions.get("results_facts")
    if not callable(binding):
        _error(503, "RESULTS_DEPENDENCY_REQUIRED",
               "부서 검토자·상위 검토자·연장 승인 상태 연결이 필요합니다.")
    tentative = binding(db, task, assignment, lock=False)
    if not isinstance(tentative, ExternalFacts):
        _error(503, "RESULTS_DEPENDENCY_REQUIRED", "결과물 연결 자료 형식을 확인해 주세요.")
    if tentative.reviewer_id is None:
        _error(409, "REVIEWER_UNAVAILABLE", "현재 지정된 검토자가 없습니다.")
    if type(tentative.reviewer_id) is not int or tentative.reviewer_id < 1:
        _error(503, "RESULTS_DEPENDENCY_REQUIRED", "검토자 연결 자료를 확인해 주세요.")
    reviewer_id = positive_integer(tentative.reviewer_id, "reviewer_id")
    users = lock_users(db, [assignment.assignee_id, *managers, reviewer_id])
    current = binding(db, task, assignment, lock=True)
    if not isinstance(current, ExternalFacts) or (current.extension_pending is not None and type(current.extension_pending) is not bool):
        _error(503, "RESULTS_DEPENDENCY_REQUIRED", "현재 검토자·연장 상태를 확인할 수 없습니다.")
    if current.reviewer_id != reviewer_id:
        _error(409, "REVIEWER_CHANGED", "검토자가 변경되었습니다. 다시 조회해 주세요.")
    return current, users.get(reviewer_id)


def _owner(assignment):
    if assignment.assignee_id != g.user.id:
        _error(403, "FORBIDDEN", "현재 배정된 본인 업무만 처리할 수 있습니다.")


def _readable(db, task, assignment, managers):
    if assignment.assignee_id == g.user.id:
        return
    if g.user.role != "admin":
        _error(403, "FORBIDDEN", "현재 담당자·검토자만 조회할 수 있습니다.")
    try:
        facts, reviewer = _facts(db, task, assignment, managers)
    except AppError as error:
        if error.code == "REVIEWER_UNAVAILABLE":
            _error(403, "FORBIDDEN", "현재 검토 권한이 없습니다.")
        raise
    if (facts.reviewer_id != g.user.id or reviewer is None or not reviewer.is_active):
        _error(403, "FORBIDDEN", "현재 담당자·검토자만 조회할 수 있습니다.")


def _latest(db, task_id):
    return db.scalar(select(Submission).where(Submission.task_id == task_id)
        .order_by(Submission.round_no.desc()).limit(1)
        .with_for_update().execution_options(populate_existing=True))


def _authorize_submit(db, target):
    task_id = positive_bigint(target, "task_id")
    task, assignment, managers = _current(db, task_id)
    _owner(assignment)
    # State, deadline and reviewer availability belong only in first mutate.
    return True


def _authorize_decision(db, target):
    submission_id = positive_bigint(target, "submission_id")
    source = db.get(Submission, submission_id)
    if source is None:
        _error(404, "SUBMISSION_NOT_FOUND", "제출물을 찾을 수 없습니다.")
    task, assignment, managers = _current(db, source.task_id)
    if (g.user.role != "admin" or source.submitted_by == g.user.id or
            assignment.assignee_id == g.user.id):
        _error(403, "FORBIDDEN", "자기 판단 또는 검토 권한이 없는 요청입니다.")
    facts, reviewer = _facts(db, task, assignment, managers)
    locked = db.scalar(select(Submission).where(Submission.id == submission_id)
        .with_for_update().execution_options(populate_existing=True))
    if locked is None:
        _error(404, "SUBMISSION_NOT_FOUND", "제출물을 찾을 수 없습니다.")
    if (g.user.role != "admin" or facts.reviewer_id != g.user.id or
            locked.submitted_by == g.user.id or assignment.assignee_id == g.user.id):
        _error(403, "FORBIDDEN", "현재 검토자만 판단할 수 있으며 자기 판단은 불가합니다.")
    return True


def _first_submit_conditions(db, task, assignment, managers):
    if task.task_status != "in_progress":
        _error(409, "TASK_STATE_CONFLICT", "진행중 업무만 제출할 수 있습니다. 반려 후에는 재작업 시작이 필요합니다.")
    facts, reviewer = _facts(db, task, assignment, managers)
    if (reviewer is None or not reviewer.is_active or reviewer.role != "admin" or
            reviewer.id == assignment.assignee_id):
        _error(409, "REVIEWER_UNAVAILABLE", "현재 활성 검토자가 필요합니다.")
    if facts.extension_pending is None:
        _error(503, "RESULTS_EXTENSION_REQUIRED", "2번 연장 승인 상태 연결이 필요합니다.")
    if facts.extension_pending:
        _error(409, "DEADLINE_EXTENSION_PENDING", "연장 승인 대기 중에는 제출할 수 없습니다.")
    today = utcnow().replace(tzinfo=timezone.utc).astimezone(
        timezone(timedelta(hours=9))).date()
    if today > task.due_date:
        _error(409, "SUBMISSION_AFTER_DUE_DATE", "마감일이 지났습니다. 연장 승인 후 다시 제출해 주세요.")
    last = _latest(db, task.task_id)
    if last is not None and last.status != "returned":
        _error(409, "TASK_STATE_CONFLICT", "이전 제출의 반려·재작업 기록을 확인해 주세요.")
    if task.row_version >= BIGINT_MAX or (last is not None and last.round_no >= BIGINT_MAX):
        _error(409, "RESULT_VERSION_EXHAUSTED", "저장 번호의 기술 범위에 도달했습니다.")
    return facts, last


def _result(task, submission, decision=None):
    data = dict(task_id=str(task.task_id), submission_id=str(submission.id),
        round_no=str(submission.round_no), row_version=str(task.row_version),
        assignment_revision=task.current_assignment_revision,
        task_status=task.task_status, updated_at=iso(task.updated_at))
    if decision is not None:
        data["decision_id"] = str(decision.id)
    return data


def _event(db, task, submission, kind):
    labels = {"submitted": "제출", "approved": "승인", "returned": "반려"}
    db.add(ResultEvent(operation_id=str(uuid.uuid4()), task_id=task.task_id,
        assignment_revision=task.current_assignment_revision,
        submission_id=submission.id, actor_id=g.user.id, kind=kind,
        summary=f"{submission.round_no}차 {labels[kind]}", occurred_at=utcnow()))
    audit("submission." + kind, "submission", submission.id,
          {"task_id": str(task.task_id), "round_no": str(submission.round_no),
           "row_version": str(task.row_version)})


@bp.get("/tasks/<task_id>/context")
@login_required
def submission_context(task_id):
    task_id = positive_bigint(task_id, "task_id")
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _owner(assignment)
        facts, last = _first_submit_conditions(db, task, assignment, managers)
        data = dict(task_id=str(task.task_id), title=task.title,
            assignee_id=assignment.assignee_id, row_version=str(task.row_version),
            assignment_revision=task.current_assignment_revision,
            task_status=task.task_status, due_date=task.due_date.isoformat(),
            reviewer_id=facts.reviewer_id, extension_pending=facts.extension_pending,
            next_round_no=str(1 if last is None else last.round_no + 1))
    return success(data)


@bp.post("/tasks/<task_id>/submissions")
@login_required
def submit(task_id, *, body=None):
    task_id = positive_bigint(task_id, "task_id")
    body = _fields({"expected_row_version", "expected_assignment_revision",
                    "description", "attachment_ids"}, body)
    expected = ExpectedVersions.from_body(body)
    description = _text(body["description"], required=True)
    ids = body["attachment_ids"]
    if not isinstance(ids, list) or len(ids) > MAX_FILES:
        _error(400, "INVALID_INPUT", "첨부파일은 최대 10개입니다.")
    ids = [_uuid(value) for value in ids]
    if len(set(ids)) != len(ids):
        _error(400, "INVALID_INPUT", "중복 첨부는 허용하지 않습니다.")

    def mutate(db):
        task, assignment, managers = _current(db, task_id)
        expected.check(row_version=task.row_version,
                       assignment_revision=task.current_assignment_revision)
        facts, last = _first_submit_conditions(db, task, assignment, managers)
        files = list(db.scalars(select(Attachment).where(Attachment.id.in_(ids))
            .order_by(Attachment.id).with_for_update()
            .execution_options(populate_existing=True))) if ids else []
        now = utcnow()
        if len(files) != len(ids) or any(
            f.task_id != task_id or f.owner_id != g.user.id or
            f.submission_id is not None or f.state != "ready" or f.expires_at <= now
            for f in files):
            _error(409, "ATTACHMENT_UNAVAILABLE", "현재 업무에 업로드한 유효한 첨부만 연결할 수 있습니다.")
        submission = Submission(task_id=task_id,
            assignment_revision=task.current_assignment_revision,
            round_no=1 if last is None else last.round_no + 1,
            submitted_by=g.user.id, reviewer_at_submission=facts.reviewer_id,
            description=description, status="submitted", submitted_at=now)
        db.add(submission)
        db.flush()
        for attachment in files:
            attachment.submission_id = submission.id
        task.task_status = "submitted"
        task.row_version += 1
        task.updated_at = now
        db.flush()
        db.refresh(task)
        _event(db, task, submission, "submitted")
        return WriteResult(_result(task, submission), 201)

    with authenticated_transaction() as db:
        result = perform_once(db, key=request.headers.get("Idempotency-Key"),
            action="submission.submitted", target=str(task_id), body=body,
            authorize=_authorize_submit, mutate=mutate)
    return success(result.data, result.status)


@bp.post("/submissions/<submission_id>/decisions")
@login_required
def decide(submission_id, *, body=None):
    submission_id = positive_bigint(submission_id, "submission_id")
    body = _fields({"expected_row_version", "expected_assignment_revision", "outcome", "reason"}, body)
    expected = ExpectedVersions.from_body(body)
    outcome = body["outcome"]
    if outcome not in ("approved", "returned"):
        _error(400, "INVALID_INPUT", "승인 또는 반려를 선택해 주세요.")
    reason = _text(body["reason"], required=outcome == "returned")

    def mutate(db):
        source = db.get(Submission, submission_id)
        task, assignment, managers = _current(db, source.task_id)
        expected.check(row_version=task.row_version,
                       assignment_revision=task.current_assignment_revision)
        last = _latest(db, task.task_id)
        if (last is None or last.id != submission_id or
                last.status != "submitted" or task.task_status != "submitted"):
            _error(409, "TASK_STATE_CONFLICT", "최신 검토 대기 제출물만 판단할 수 있습니다.")
        if task.row_version >= BIGINT_MAX:
            _error(409, "RESULT_VERSION_EXHAUSTED", "업무 버전의 기술 범위에 도달했습니다.")
        decision = Decision(submission_id=submission_id, reviewer_id=g.user.id,
                            outcome=outcome, reason=reason, decided_at=utcnow())
        db.add(decision)
        last.status = outcome
        task.task_status = outcome
        task.row_version += 1
        task.updated_at = utcnow()
        db.flush()
        db.refresh(task)
        _event(db, task, last, outcome)
        return WriteResult(_result(task, last, decision))

    with authenticated_transaction() as db:
        result = perform_once(db, key=request.headers.get("Idempotency-Key"),
            action="submission." + outcome, target=str(submission_id), body=body,
            authorize=_authorize_decision, mutate=mutate)
    return success(result.data, result.status)


def _submission_json(db, item):
    decision = db.scalar(select(Decision).where(Decision.submission_id == item.id))
    files = list(db.scalars(select(Attachment).where(Attachment.submission_id == item.id)
                           .order_by(Attachment.id)))
    return dict(submission_id=str(item.id), task_id=str(item.task_id),
        assignment_revision=item.assignment_revision, round_no=str(item.round_no),
        submitted_by=item.submitted_by,
        submitted_by_name=db.get(User, item.submitted_by).display_name,
        description=item.description,
        status=item.status, submitted_at=iso(item.submitted_at),
        reviewer_id=decision.reviewer_id if decision else None,
        reason=decision.reason if decision else "",
        reviewed_at=iso(decision.decided_at) if decision else None,
        attachments=[dict(attachment_id=f.id, name=f.name, size=f.size,
                          sha256=f.sha256) for f in files])


@bp.get("/tasks/<task_id>/submissions")
@login_required
def submissions(task_id):
    task_id = positive_bigint(task_id, "task_id")
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _readable(db, task, assignment, managers)
        rows = list(db.scalars(select(Submission).where(Submission.task_id == task_id)
                              .order_by(Submission.round_no.desc())))
        data = {"items": [_submission_json(db, item) for item in rows]}
    return success(data)


@bp.get("/tasks/<task_id>/history")
@login_required
def history(task_id):
    task_id = positive_bigint(task_id, "task_id")
    before = request.args.get("before_event_id")
    before_id = positive_bigint(before, "before_event_id") if before else None
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _readable(db, task, assignment, managers)
        query = select(ResultEvent).where(ResultEvent.task_id == task_id)
        if before_id is not None:
            query = query.where(ResultEvent.id < before_id)
        rows = list(db.scalars(query.order_by(ResultEvent.id.desc()).limit(100)))
        data = dict(items=[dict(event_id=str(e.id), task_id=str(e.task_id),
            submission_id=str(e.submission_id), actor_id=e.actor_id,
            actor_name=db.get(User, e.actor_id).display_name, kind=e.kind, summary=e.summary, occurred_at=iso(e.occurred_at))
            for e in rows],
            next_before_event_id=str(rows[-1].id) if len(rows) == 100 else None)
    return success(data)


def _activity_item(db, task_id, event):
    # Read the existing shared audit log; never synthesize or duplicate events.
    try:
        details = json.loads(event.details_json)
    except (ValueError, TypeError):
        details = {}
    if not isinstance(details, dict):
        details = {}
    kinds = {"task.created": "created", "task.accepted": "status_changed",
        "task.rework_started": "status_changed", "task.assigned": "assignee_changed",
        "task.assignee_changed": "assignee_changed", "task.status_changed": "status_changed",
        "task.progress_changed": "progress_changed", "submission.submitted": "submitted",
        "submission.returned": "returned", "submission.approved": "approved"}
    labels = {"task.created": "업무 생성", "task.accepted": "업무 수락",
        "task.rework_started": "반려된 업무 재작업 시작", "task.assigned": "담당자 변경",
        "task.assignee_changed": "담당자 변경", "task.status_changed": "상태 변경",
        "task.progress_changed": "진행률 변경", "submission.submitted": "결과 제출",
        "submission.returned": "반려", "submission.approved": "승인"}
    summary = labels.get(event.action, event.action)
    if event.resource_type == "submission":
        item = db.get(Submission, int(event.resource_id))
        if item is not None:
            summary = f"{item.round_no}차 {summary}"
            decision = db.scalar(select(Decision).where(Decision.submission_id == item.id))
            if event.action in ("submission.returned", "submission.approved") and decision and decision.reason:
                summary += " · " + decision.reason
    else:
        before, after = details.get("before"), details.get("after")
        if isinstance(before, dict) and isinstance(after, dict):
            states = {"not_started": "진행전", "in_progress": "진행중", "submitted": "제출완료",
                "returned": "반려완료", "approved": "승인완료"}
            changes = []
            for key, label in (("task_status", "상태"), ("assignee_id", "담당자 ID"),
                ("progress", "진행률"), ("due_date", "마감일")):
                if key in before and key in after and before[key] != after[key]:
                    old, new = str(before[key]), str(after[key])
                    if key == "task_status": old, new = states.get(old, old), states.get(new, new)
                    changes.append(f"{label}: {old} → {new}")
            if changes: summary += " · " + " / ".join(changes)
    actor = db.get(User, event.actor_id) if event.actor_id else None
    return dict(event_id=str(event.id), task_id=str(task_id),
        submission_id=event.resource_id if event.resource_type == "submission" else "",
        actor_id=event.actor_id or 0, actor_name=actor.display_name if actor else "시스템",
        kind=kinds.get(event.action, "other"), summary=summary, occurred_at=iso(event.created_at))


@bp.get("/tasks/<task_id>/activity")
@login_required
def task_activity(task_id):
    task_id = positive_bigint(task_id, "task_id")
    before = request.args.get("before_event_id")
    before_id = positive_bigint(before, "before_event_id") if before else None
    with authenticated_transaction(roles=("admin",)) as db:
        task, assignment, managers = _current(db, task_id)
        _readable(db, task, assignment, managers)
        submission_id_text = cast(Submission.id, String)
        if db.bind.dialect.name == "mysql":
            # Compare numeric IDs as text without changing common-table collations.
            submission_id_text = submission_id_text.collate("utf8mb4_unicode_ci")
        submission_ids = select(submission_id_text).where(Submission.task_id == task_id)
        query = select(AuditEvent).where(or_(
            and_(AuditEvent.resource_type == "task", AuditEvent.resource_id == str(task_id)),
            and_(AuditEvent.resource_type == "submission", AuditEvent.resource_id.in_(submission_ids))))
        if before_id is not None: query = query.where(AuditEvent.id < before_id)
        rows = list(db.scalars(query.order_by(AuditEvent.id.desc()).limit(100)))
        data = dict(items=[_activity_item(db, task_id, item) for item in rows],
            next_before_event_id=str(rows[-1].id) if len(rows) == 100 else None)
    return success(data)


def _task_json(db, task, assignment):
    assignee = db.get(User, assignment.assignee_id)
    return dict(task_id=str(task.task_id), title=task.title,
        assignee_name=assignee.display_name if assignee else "",
        task_status=task.task_status, start_date=task.start_date.isoformat(),
        due_date=task.due_date.isoformat())


@bp.get("/review-tasks")
@login_required
def review_tasks():
    with authenticated_transaction(roles=("admin",)) as db:
        ids = list(db.scalars(select(TaskRecord.task_id).order_by(
            TaskRecord.due_date, TaskRecord.task_id)))
        items = []
        for task_id in ids:
            task, assignment, managers = _current(db, task_id)
            if assignment.assignee_id == g.user.id:
                continue
            try:
                facts, reviewer = _facts(db, task, assignment, managers)
            except AppError as error:
                if error.code == "REVIEWER_UNAVAILABLE":
                    continue
                raise
            if facts.reviewer_id == g.user.id and reviewer is not None and reviewer.is_active:
                items.append(_task_json(db, task, assignment))
    return success({"items": items})


@bp.get("/tasks/<task_id>")
@login_required
def result_task(task_id):
    task_id = positive_bigint(task_id, "task_id")
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _readable(db, task, assignment, managers)
        data = _task_json(db, task, assignment)
        # Reviewer access is kept separate from member2's full task GET.
    return success(data)


@bp.get("/submissions/<submission_id>/context")
@login_required
def decision_context(submission_id):
    submission_id = positive_bigint(submission_id, "submission_id")
    with authenticated_transaction() as db:
        _authorize_decision(db, str(submission_id))
        item = db.get(Submission, submission_id)
        task, assignment, managers = _current(db, item.task_id)
        last = _latest(db, task.task_id)
        if last is None or last.id != item.id or item.status != "submitted" or task.task_status != "submitted":
            _error(409, "TASK_STATE_CONFLICT", "최신 검토 대기 제출물만 판단할 수 있습니다.")
        data = dict(task_id=str(task.task_id), submission_id=str(item.id),
            row_version=str(task.row_version), assignment_revision=task.current_assignment_revision,
            round_no=str(item.round_no), task_status=task.task_status)
    return success(data)


@bp.get("/history-tasks")
@login_required
def history_tasks():
    with authenticated_transaction(roles=("admin",)) as db:
        ids = list(db.scalars(select(TaskRecord.task_id).order_by(
            TaskRecord.due_date, TaskRecord.task_id)))
        items = []
        for task_id in ids:
            task, assignment, managers = _current(db, task_id)
            if assignment.assignee_id != g.user.id:
                try:
                    _readable(db, task, assignment, managers)
                except AppError as error:
                    if error.status == 403 or error.code == "REVIEWER_UNAVAILABLE":
                        continue
                    raise
            items.append(_task_json(db, task, assignment))
    return success({"items": items})


def _storage():
    value = current_app.config.get("RESULTS_STORAGE_ROOT")
    if not isinstance(value, str) or not Path(value).is_absolute():
        _error(503, "RESULTS_STORAGE_REQUIRED", "서버 전용 첨부 저장 위치 설정이 필요합니다.")
    root = Path(value).resolve()
    source = Path(current_app.root_path).resolve().parent.parent
    if root == source or source in root.parents:
        _error(503, "RESULTS_STORAGE_REQUIRED", "첨부는 프로젝트 밖의 전용 폴더에 저장해야 합니다.")
    root.mkdir(parents=True, exist_ok=True)
    return root


@bp.post("/tasks/<task_id>/uploads")
@login_required
def upload(task_id):
    task_id = positive_bigint(task_id, "task_id")
    # Flask 3.1: only this endpoint gets a multipart allowance.
    # The common 32KiB JSON limit is unchanged.
    request.max_content_length = MAX_FILE_BYTES + 1024 * 1024
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _owner(assignment)
    if len(request.files) != 1 or "file" not in request.files or request.form:
        _error(400, "INVALID_INPUT", "file 항목에 파일 하나만 첨부해 주세요.")
    part = request.files["file"]
    name = part.filename
    if (not isinstance(name, str) or not 1 <= len(name) <= 255 or
            name in (".", "..") or any(c in name for c in ("/", "\\", "\r", "\n", "\0"))):
        _error(400, "INVALID_INPUT", "첨부파일 이름을 확인해 주세요.")
    root = _storage()
    attachment_id = str(uuid.uuid4())
    path = root / attachment_id
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _owner(assignment)
        now = utcnow()
        db.add(Attachment(id=attachment_id, task_id=task_id, owner_id=g.user.id,
            name=name, size=0, sha256="", state="uploading",
            created_at=now, expires_at=now + timedelta(hours=24)))
    size, digest = 0, hashlib.sha256()
    try:
        # All filesystem I/O is outside DB transactions.
        with path.open("xb") as target:
            while chunk := part.stream.read(64 * 1024):
                size += len(chunk)
                if size > MAX_FILE_BYTES:
                    _error(413, "ATTACHMENT_TOO_LARGE", "파일 하나는 25MiB 이하여야 합니다.")
                digest.update(chunk)
                target.write(chunk)
            target.flush()
            os.fsync(target.fileno())
        with authenticated_transaction() as db:
            task, assignment, managers = _current(db, task_id)
            _owner(assignment)
            item = db.scalar(select(Attachment).where(Attachment.id == attachment_id)
                .with_for_update().execution_options(populate_existing=True))
            if item is None or item.state != "uploading" or item.expires_at <= utcnow():
                _error(409, "ATTACHMENT_UNAVAILABLE", "임시 업로드가 만료되었습니다.")
            item.size, item.sha256, item.state = size, digest.hexdigest(), "ready"
        return success(dict(attachment_id=attachment_id, name=name, size=size,
                            sha256=digest.hexdigest()), 201)
    except Exception:
        # Marked uploads are retried by cleanup; no submitted file is removed.
        with transaction() as db:
            guard = db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            item = db.scalar(select(Attachment).where(Attachment.id == attachment_id)
                .with_for_update().execution_options(populate_existing=True))
            if guard is not None and item is not None and item.submission_id is None:
                item.state, item.expires_at = "deleting", utcnow()
        raise


@bp.get("/attachments/<attachment_id>/download")
@login_required
def download(attachment_id):
    attachment_id = _uuid(attachment_id)
    root = _storage()
    with authenticated_transaction() as db:
        item = db.get(Attachment, attachment_id)
        if item is None or item.submission_id is None:
            _error(404, "ATTACHMENT_NOT_FOUND", "제출된 첨부를 찾을 수 없습니다.")
        task, assignment, managers = _current(db, item.task_id)
        _readable(db, task, assignment, managers)
        item = db.scalar(select(Attachment).where(Attachment.id == attachment_id)
            .with_for_update().execution_options(populate_existing=True))
        if item.state != "ready":
            _error(404, "ATTACHMENT_NOT_FOUND", "첨부를 사용할 수 없습니다.")
        name = item.name
    path = root / attachment_id
    if not path.is_file() or path.is_symlink():
        _error(404, "ATTACHMENT_NOT_FOUND", "첨부파일을 찾을 수 없습니다.")
    return send_file(path, as_attachment=True, download_name=name,
                     mimetype="application/octet-stream", conditional=False, max_age=0)



CHUNK_BYTES = 16384


def _attachment_json(item):
    return dict(attachment_id=item.id, name=item.name, size=item.size, sha256=item.sha256)


def _upload_owner(db, attachment_id):
    source = db.get(Attachment, attachment_id)
    if source is None:
        _error(404, "ATTACHMENT_NOT_FOUND", "업로드를 찾을 수 없습니다.")
    task, assignment, managers = _current(db, source.task_id)
    _owner(assignment)
    item = db.scalar(select(Attachment).where(Attachment.id == attachment_id)
                     .with_for_update().execution_options(populate_existing=True))
    if item.owner_id != g.user.id or item.submission_id is not None:
        _error(403, "FORBIDDEN", "현재 본인 임시 업로드만 처리할 수 있습니다.")
    if item.expires_at <= utcnow() or item.state == "deleting":
        _error(409, "ATTACHMENT_UNAVAILABLE", "업로드가 만료되었습니다.")
    return item


def _valid_file_name(name):
    if (not isinstance(name, str) or not 1 <= len(name) <= 255 or
        name in (".", "..") or any(c in name for c in ("/", "\\", "\r", "\n", "\0"))):
        _error(400, "INVALID_INPUT", "첨부 이름을 확인해 주세요.")
    return name


@bp.post("/tasks/<task_id>/uploads/start")
@login_required
def start_json_upload(task_id, *, body=None):
    task_id = positive_bigint(task_id, "task_id")
    body = _fields({"name", "size", "sha256"}, body)
    name = _valid_file_name(body["name"])
    size, digest = body["size"], body["sha256"]
    if type(size) is not int or not 0 <= size <= MAX_FILE_BYTES:
        _error(413, "ATTACHMENT_TOO_LARGE", "파일 하나는 25MiB 이하여야 합니다.")
    if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
        _error(400, "INVALID_INPUT", "파일 해시를 확인해 주세요.")
    _storage()
    with authenticated_transaction() as db:
        task, assignment, managers = _current(db, task_id)
        _owner(assignment)
        _first_submit_conditions(db, task, assignment, managers)
        now = utcnow()
        item = Attachment(id=str(uuid.uuid4()), task_id=task_id, owner_id=g.user.id,
            name=name, size=size, sha256=digest, state="uploading",
            created_at=now, expires_at=now + timedelta(hours=24))
        db.add(item)
        data = _attachment_json(item)
    return success(data, 201)


@bp.post("/uploads/<attachment_id>/chunks")
@login_required
def write_json_chunk(attachment_id, *, body=None):
    attachment_id = _uuid(attachment_id)
    body = _fields({"offset", "data"}, body)
    offset, encoded = body["offset"], body["data"]
    if type(offset) is not int or offset < 0 or offset % CHUNK_BYTES:
        _error(400, "INVALID_INPUT", "파일 조각 위치를 확인해 주세요.")
    if not isinstance(encoded, str) or len(encoded) > 4 * ((CHUNK_BYTES + 2) // 3):
        _error(400, "INVALID_INPUT", "파일 조각 크기를 확인해 주세요.")
    try:
        content = base64.b64decode(encoded, validate=True)
    except (ValueError, binascii.Error):
        _error(400, "INVALID_INPUT", "파일 조각 형식이 올바르지 않습니다.")
    with authenticated_transaction() as db:
        item = _upload_owner(db, attachment_id)
        if item.state != "uploading":
            _error(409, "ATTACHMENT_UNAVAILABLE", "이미 완료된 업로드입니다.")
        if len(content) != min(CHUNK_BYTES, item.size - offset) or not content:
            _error(400, "INVALID_INPUT", "파일 조각의 위치·크기가 다릅니다.")
    digest = hashlib.sha256(content).hexdigest()
    root = _storage()
    part = root / f"{attachment_id}.{offset}.{digest}.part"
    temp = root / f"{attachment_id}.{uuid.uuid4()}.writing"
    try:
        with temp.open("xb") as output:
            output.write(content)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temp, part)
        with authenticated_transaction() as db:
            item = _upload_owner(db, attachment_id)
            if item.state != "uploading":
                _error(409, "ATTACHMENT_UNAVAILABLE", "업로드 상태가 변경되었습니다.")
            existing = db.get(UploadChunk, (attachment_id, offset))
            if existing is None:
                db.add(UploadChunk(attachment_id=attachment_id, offset=offset,
                                   size=len(content), sha256=digest))
            elif existing.size != len(content) or existing.sha256 != digest:
                _error(409, "UPLOAD_CHUNK_CONFLICT", "같은 위치에 다른 파일 내용이 있습니다.")
    finally:
        temp.unlink(missing_ok=True)
    return success(dict(attachment_id=attachment_id, next_offset=offset + len(content)))


@bp.post("/uploads/<attachment_id>/complete")
@login_required
def complete_json_upload(attachment_id, *, body=None):
    attachment_id = _uuid(attachment_id)
    _fields(set(), body)
    with authenticated_transaction() as db:
        item = _upload_owner(db, attachment_id)
        data = _attachment_json(item)
        if item.state == "ready":
            return success(data)
        parts = [(c.offset, c.size, c.sha256) for c in db.scalars(
            select(UploadChunk).where(UploadChunk.attachment_id == attachment_id)
            .order_by(UploadChunk.offset))]
    cursor = 0
    for offset, size, _ in parts:
        if offset != cursor:
            _error(409, "UPLOAD_INCOMPLETE", "전송되지 않은 파일 조각이 있습니다.")
        cursor += size
    if cursor != data["size"]:
        _error(409, "UPLOAD_INCOMPLETE", "파일 전송이 끝나지 않았습니다.")
    root = _storage()
    temp = root / f"{attachment_id}.{uuid.uuid4()}.assembling"
    digest = hashlib.sha256()
    total = 0
    try:
        with temp.open("xb") as output:
            for offset, size, expected_hash in parts:
                part = root / f"{attachment_id}.{offset}.{expected_hash}.part"
                if part.is_symlink():
                    _error(409, "ATTACHMENT_CORRUPTED", "파일 조각을 확인해 주세요.")
                content = part.read_bytes()
                if len(content) != size or hashlib.sha256(content).hexdigest() != expected_hash:
                    _error(409, "ATTACHMENT_CORRUPTED", "파일 조각이 손상되었습니다.")
                total += len(content)
                if total > MAX_FILE_BYTES:
                    _error(413, "ATTACHMENT_TOO_LARGE", "첨부 크기를 초과했습니다.")
                digest.update(content)
                output.write(content)
            output.flush()
            os.fsync(output.fileno())
        if total != data["size"] or digest.hexdigest() != data["sha256"]:
            _error(409, "ATTACHMENT_CORRUPTED", "파일 크기·해시가 일치하지 않습니다.")
        os.replace(temp, root / attachment_id)
        with authenticated_transaction() as db:
            item = _upload_owner(db, attachment_id)
            if item.state not in ("uploading", "ready"):
                _error(409, "ATTACHMENT_UNAVAILABLE", "업로드 상태가 변경되었습니다.")
            item.state = "ready"
        return success(data)
    finally:
        temp.unlink(missing_ok=True)


@bp.get("/attachments/<attachment_id>/chunks")
@login_required
def read_json_chunk(attachment_id):
    attachment_id = _uuid(attachment_id)
    raw = request.args.get("offset", "0")
    if not raw.isascii() or not raw.isdigit() or (len(raw) > 1 and raw.startswith("0")):
        _error(400, "INVALID_INPUT", "다운로드 위치를 확인해 주세요.")
    offset = int(raw)
    root = _storage()
    with authenticated_transaction() as db:
        item = db.get(Attachment, attachment_id)
        if item is None or item.submission_id is None:
            _error(404, "ATTACHMENT_NOT_FOUND", "제출된 첨부가 없습니다.")
        task, assignment, managers = _current(db, item.task_id)
        _readable(db, task, assignment, managers)
        size = item.size
        if item.state != "ready" or offset > size:
            _error(400, "INVALID_INPUT", "첨부 상태·위치를 확인해 주세요.")
    path = root / attachment_id
    if path.is_symlink() or not path.is_file() or path.stat().st_size != size:
        _error(404, "ATTACHMENT_NOT_FOUND", "첨부가 없거나 손상되었습니다.")
    with path.open("rb") as source:
        source.seek(offset)
        content = source.read(min(CHUNK_BYTES, size - offset))
    return success(dict(attachment_id=attachment_id, offset=offset,
        next_offset=offset + len(content), eof=offset + len(content) == size,
        data=base64.b64encode(content).decode("ascii")))


def cleanup_uploads():
    """Invoke explicitly with an app context. Never deletes submitted attachments."""
    root = _storage()
    with transaction() as db:
        if db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update()) is None:
            _error(503, "DATABASE_UNAVAILABLE", "공통 보호 정보가 필요합니다.")
        items = list(db.scalars(select(Attachment).where(
            Attachment.submission_id.is_(None),
            (Attachment.expires_at <= utcnow()) | (Attachment.state == "deleting"))
            .order_by(Attachment.id).with_for_update()
            .execution_options(populate_existing=True)))
        ids = [item.id for item in items]
        for item in items:
            item.state = "deleting"
    removed = 0
    for attachment_id in ids:
        path = root / _uuid(attachment_id)
        try:
            path.unlink(missing_ok=True)
            for part in root.glob(attachment_id + ".*"):
                if part.is_file() or part.is_symlink():
                    part.unlink(missing_ok=True)
        except OSError:
            continue
        with transaction() as db:
            db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            item = db.scalar(select(Attachment).where(Attachment.id == attachment_id)
                .with_for_update().execution_options(populate_existing=True))
            if item is not None and item.submission_id is None and item.state == "deleting":
                for chunk in db.scalars(select(UploadChunk).where(UploadChunk.attachment_id == attachment_id)):
                    db.delete(chunk)
                db.flush()
                db.delete(item)
                removed += 1
    return removed


RESULT_TABLES = [DepartmentReviewer.__table__, UpperReviewer.__table__,
    SubmissionGate.__table__, Submission.__table__, Decision.__table__,
    Attachment.__table__, UploadChunk.__table__, ResultEvent.__table__]


def _cli_guard(db):
    guard = db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
    if guard is None:
        raise click.ClickException("공통 DB 초기화가 먼저 필요합니다.")


def _cli_user(db, login_id, admin=False):
    user = db.scalar(select(User).where(User.login_id == login_id)
                     .with_for_update().execution_options(populate_existing=True))
    if user is None or not user.is_active or (admin and user.role != "admin"):
        raise click.ClickException("현재 활성 사용자/관리자 계정을 확인해 주세요.")
    return user


@bp.cli.command("init-results")
def init_results():
    """Create only member 3 tables; never seed users/tasks or change schema version."""
    from sqlalchemy import inspect
    engine = current_app.extensions["db_engine"]
    inspector = inspect(engine)
    required = {"users", "admin_guard", "schema_versions", "tasks",
                "task_assignments", "task_managers", "write_receipts"}
    missing = required - set(inspector.get_table_names())
    if missing:
        raise click.ClickException("선행 공통/002/004 테이블이 필요합니다: " + ", ".join(sorted(missing)))
    with transaction() as db:
        if db.get(SchemaVersion, 1) is None:
            raise click.ClickException("공통 schema1 확인이 필요합니다.")
    # Existing incompatible tables must not be silently treated as initialized.
    for table in RESULT_TABLES:
        if inspector.has_table(table.name):
            actual = {c["name"] for c in inspector.get_columns(table.name)}
            if actual != set(table.columns.keys()):
                raise click.ClickException("기존 테이블 구조가 다릅니다: " + table.name)
    Base.metadata.create_all(engine, tables=RESULT_TABLES)
    click.echo("결과물 테이블 준비 완료. 사용자/업무/검토자 자동 생성 없음. 공통 schema1 유지.")


@bp.cli.command("set-department-reviewer")
@click.option("--reviewer-login", required=True)
def set_department_reviewer(reviewer_login):
    """Operator explicitly registers the department's highest-ranking reviewer."""
    with transaction() as db:
        _cli_guard(db)
        reviewer = _cli_user(db, reviewer_login, admin=True)
        if not reviewer.department.strip():
            raise click.ClickException("검토자의 실제 부서 정보가 필요합니다.")
        row = db.get(DepartmentReviewer, reviewer.department)
        if row is None:
            db.add(DepartmentReviewer(department=reviewer.department, reviewer_id=reviewer.id))
        else:
            row.reviewer_id = reviewer.id
        db.add(AuditEvent(actor_id=None, action="results.department_reviewer_configured",
            resource_type="department", resource_id=reviewer.department, details_json="{}"))
    click.echo("해당 부서의 검토자를 설정했습니다. 직급은 자동 추정하지 않습니다.")


@bp.cli.command("set-upper-reviewer")
@click.option("--submitter-login", required=True)
@click.option("--reviewer-login", required=True)
def set_upper_reviewer(submitter_login, reviewer_login):
    with transaction() as db:
        _cli_guard(db)
        submitter = _cli_user(db, submitter_login, admin=True)
        reviewer = _cli_user(db, reviewer_login, admin=True)
        if reviewer.id == submitter.id:
            raise click.ClickException("자기 검토자는 지정할 수 없습니다.")
        row = db.get(UpperReviewer, submitter.id)
        if row is None:
            db.add(UpperReviewer(submitter_id=submitter.id, reviewer_id=reviewer.id))
        else:
            row.reviewer_id = reviewer.id
        db.add(AuditEvent(actor_id=None, action="results.upper_reviewer_configured",
            resource_type="user", resource_id=str(submitter.id), details_json="{}"))
    click.echo("관리자 본인 제출물의 상위 검토자를 설정했습니다.")


@bp.cli.command("set-extension-pending")
@click.option("--task-id", required=True)
@click.option("--pending/--clear", default=True)
def set_extension_pending(task_id, pending):
    """Development adapter only; does not approve extensions or invent a new due date."""
    if current_app.config.get("RESULTS_CONTEXT_MODE") != "local-development":
        raise click.ClickException("개발용 연결 모드에서만 사용할 수 있습니다.")
    task_id = positive_bigint(task_id, "task_id")
    with transaction() as db:
        _cli_guard(db)
        task, assignment, managers = _current(db, task_id)
        if task.row_version >= BIGINT_MAX:
            raise click.ClickException("업무 버전 범위에 도달했습니다.")
        row = db.get(SubmissionGate, task_id)
        if row is None:
            db.add(SubmissionGate(task_id=task_id, extension_pending=pending))
        elif row.extension_pending == pending:
            click.echo("이미 같은 연장 대기 상태입니다.")
            return
        else:
            row.extension_pending = pending
        task.row_version += 1
        task.updated_at = utcnow()
    click.echo("연장 대기 상태를 반영했습니다. 마감일/승인 기록은 변경하지 않았습니다.")


@bp.cli.command("cleanup-result-uploads")
def cleanup_result_uploads():
    click.echo(f"미연결 임시 첨부 정리: {cleanup_uploads()}건")


@bp.record_once
def register_results(state):
    app = state.app
    if not (app.config.get("ENABLE_COMMON_WRITE_DRAFT") and
            app.config.get("ENABLE_TASK_QUERY_DRAFT")):
        raise RuntimeError("결과물은 기존 공통 쓰기·업무 조회 활성화가 필요합니다.")
    app.config.setdefault("RESULTS_CONTEXT_MODE", os.getenv("REMOTEWORK_RESULTS_CONTEXT", ""))
    app.config.setdefault("RESULTS_STORAGE_ROOT", os.getenv("REMOTEWORK_RESULTS_STORAGE_ROOT"))
    app.extensions.setdefault("results_facts", local_facts)
    fields = {"task_id", "submission_id", "round_no", "row_version",
              "assignment_revision", "task_status", "updated_at", "decision_id"}
    register_write_action(app, action="submission.submitted",
                          authorize=_authorize_submit, result_fields=fields)
    for action in ("submission.approved", "submission.returned"):
        register_write_action(app, action=action,
                              authorize=_authorize_decision, result_fields=fields)
