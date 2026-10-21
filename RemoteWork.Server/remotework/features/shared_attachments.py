"""Member 3 shared file provider. Explicit opt-in; no DDL, seed or task writes.

Task consumers call replace/release inside their existing authenticated TX.
Submission references are immutable and created only by the result submit TX.
"""
from datetime import datetime, timedelta
import uuid

from flask import Blueprint, current_app, g, has_request_context
from sqlalchemy import BigInteger, CheckConstraint, DateTime, ForeignKey, Index, Integer, String, inspect, select
from sqlalchemy.orm import Mapped, mapped_column

from ..auth import authenticated_transaction, login_required
from ..common import AppError, success
from ..db import get_db, transaction
from ..models import AdminGuard, Base, utcnow
from ..write_contract import ExpectedVersions, positive_bigint
from . import results as service
from .tasks.lifecycle_support import ensure_not_cancelled

bp = Blueprint("shared_attachments", __name__, url_prefix="/api/v1/results/shared", cli_group="shared-attachments")


class SharedFile(Base):
    __tablename__ = "result_shared_files"
    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    owner_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    upload_task_id: Mapped[int | None] = mapped_column(ForeignKey("tasks.task_id"), nullable=True)
    name: Mapped[str] = mapped_column(String(255))
    size: Mapped[int] = mapped_column(BigInteger)
    sha256: Mapped[str] = mapped_column(String(64))
    state: Mapped[str] = mapped_column(String(16), default="uploading")
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    expires_at: Mapped[datetime] = mapped_column(DateTime)
    __table_args__ = (
        CheckConstraint("size >= 0 AND size <= 26214400", name="ck_shared_file_size"),
        CheckConstraint("HEX(state) IN (HEX('uploading'),HEX('ready'),HEX('deleting'))", name="ck_shared_file_state"),
        Index("ix_shared_file_cleanup", "state", "expires_at"), service.TABLE_OPTIONS)


class SharedChunk(Base):
    __tablename__ = "result_shared_chunks"
    attachment_id: Mapped[str] = mapped_column(ForeignKey("result_shared_files.id"), primary_key=True)
    offset: Mapped[int] = mapped_column(Integer, primary_key=True)
    size: Mapped[int] = mapped_column(Integer)
    sha256: Mapped[str] = mapped_column(String(64))
    __table_args__ = (CheckConstraint("offset >= 0 AND size > 0 AND size <= 16384", name="ck_shared_chunk"), service.TABLE_OPTIONS)


class SharedLink(Base):
    __tablename__ = "result_shared_links"
    attachment_id: Mapped[str] = mapped_column(ForeignKey("result_shared_files.id"), primary_key=True)
    resource_type: Mapped[str] = mapped_column(String(16), primary_key=True)
    resource_id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"))
    submission_id: Mapped[int | None] = mapped_column(ForeignKey("result_submissions.id"), nullable=True)
    __table_args__ = (
        CheckConstraint("(HEX(resource_type)=HEX('task') AND resource_id=task_id AND submission_id IS NULL) OR "
                        "(HEX(resource_type)=HEX('submission') AND resource_id=submission_id AND submission_id IS NOT NULL)", name="ck_shared_link_resource"),
        Index("ix_shared_link_resource", "resource_type", "resource_id"),
        Index("ix_shared_link_task", "task_id"), service.TABLE_OPTIONS)


TABLES = [SharedFile.__table__, SharedChunk.__table__, SharedLink.__table__]


def require_schema(db):
    if current_app.extensions.get("shared_attachment_schema_verified"):
        return
    inspector = inspect(db.get_bind())
    for table in TABLES:
        if (not inspector.has_table(table.name) or
            {c["name"] for c in inspector.get_columns(table.name)} != set(table.columns.keys()) or
            inspector.get_pk_constraint(table.name).get("constrained_columns") != [c.name for c in table.primary_key]):
            raise AppError(503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED", "공용 첨부 DB 검토·적용이 필요합니다.")
    current_app.extensions["shared_attachment_schema_verified"] = True


def _guard(db):
    if not has_request_context() or not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the caller's authenticated_transaction session; do not nest or commit here.")
    require_schema(db)


def storage():
    base = service._storage()
    root = base / "shared"
    if root.is_symlink():
        raise AppError(503, "RESULTS_STORAGE_REQUIRED", "공용 첨부 저장 위치를 확인해 주세요.")
    root.mkdir(exist_ok=True)
    return root


def _links(db, attachment_id):
    return list(db.scalars(select(SharedLink).where(SharedLink.attachment_id == attachment_id)
                          .order_by(SharedLink.task_id, SharedLink.resource_type, SharedLink.resource_id)))


def _file(db, attachment_id):
    require_schema(db)
    item = db.scalar(select(SharedFile).where(SharedFile.id == service._uuid(attachment_id))
                     .with_for_update().execution_options(populate_existing=True))
    if item is None or item.state == "deleting":
        raise AppError(404, "ATTACHMENT_NOT_FOUND", "공용 첨부를 찾을 수 없습니다.")
    return item


def _read_link(db, link):
    if link.resource_type == "submission":
        submission = db.get(service.Submission, link.submission_id)
        if submission is None or submission.task_id != link.task_id:
            raise AppError(409, "SHARED_ATTACHMENT_DATA_INCONSISTENT", "제출물·업무 첨부 연결을 확인해 주세요.")
    task, assignment, managers = service._current(db, link.task_id)
    service._readable(db, task, assignment, managers)
    if link.resource_type == "task":
        ensure_not_cancelled(db, task.task_id)


def _source(db, item):
    links = _links(db, item.id)
    if not links:
        if item.owner_id == g.user.id and item.expires_at > utcnow():
            if item.upload_task_id is not None:
                _upload_task(db, item.upload_task_id)
            return
    else:
        for link in links:
            try:
                _read_link(db, link)
                return
            except AppError as error:
                if error.status not in (403, 404) and error.code not in ("TASK_CANCELLED", "REVIEWER_UNAVAILABLE"):
                    raise
    raise AppError(403, "FORBIDDEN", "현재 파일이 연결된 업무의 열람 권한이 필요합니다.")


def upload_owner(db, attachment_id):
    item = _file(db, attachment_id)
    if item.owner_id != g.user.id or _links(db, item.id):
        raise AppError(403, "FORBIDDEN", "본인 미연결 임시 파일만 처리할 수 있습니다.")
    if item.expires_at <= utcnow():
        raise AppError(409, "ATTACHMENT_UNAVAILABLE", "임시 파일이 만료되었습니다.")
    if item.upload_task_id is not None:
        _upload_task(db, item.upload_task_id)
    return item


def _upload_task(db, task_id):
    task, assignment, managers = service._current(db, task_id)
    ensure_not_cancelled(db, task_id)
    if g.user.role == "admin" and g.user.id in managers:
        return
    if assignment.assignee_id == g.user.id:
        service._first_submit_conditions(db, task, assignment, managers)
        return
    raise AppError(403, "FORBIDDEN", "현재 담당자·업무 관리자만 업로드할 수 있습니다.")


def selection(db, ids):
    _guard(db)
    if not isinstance(ids, list) or len(ids) > service.MAX_FILES:
        raise AppError(400, "INVALID_INPUT", "첨부는 최대 10개입니다.")
    ids = [service._uuid(value) for value in ids]
    if len(set(ids)) != len(ids):
        raise AppError(400, "INVALID_INPUT", "중복 첨부는 허용하지 않습니다.")
    files = []
    for attachment_id in sorted(ids):
        item = _file(db, attachment_id)
        if item.state != "ready":
            raise AppError(409, "ATTACHMENT_UNAVAILABLE", "전송 완료된 파일만 연결할 수 있습니다.")
        _source(db, item)
        files.append(item)
    return files


def replace_task_files(db, task_id, attachment_ids, *, expected):
    """Caller owns task versions/receipt/audit and commits this same TX."""
    _guard(db)
    task_id = positive_bigint(str(task_id) if type(task_id) is int else task_id, "task_id")
    task, assignment, managers = service._current(db, task_id)
    if g.user.role != "admin" or g.user.id not in managers:
        raise AppError(403, "FORBIDDEN", "현재 업무 관리자만 첨부를 변경할 수 있습니다.")
    if not isinstance(expected, ExpectedVersions):
        raise AppError(400, "INVALID_INPUT", "현재 업무 버전·배정 정보가 필요합니다.")
    expected.check(row_version=task.row_version, assignment_revision=task.current_assignment_revision)
    ensure_not_cancelled(db, task_id)
    files = selection(db, attachment_ids)
    wanted = {f.id for f in files}
    current = list(db.scalars(select(SharedLink).where(SharedLink.resource_type == "task", SharedLink.resource_id == task_id)))
    present = {link.attachment_id for link in current}
    for link in current:
        if link.attachment_id not in wanted:
            db.delete(link)
    for item in files:
        if item.id not in present:
            db.add(SharedLink(attachment_id=item.id, resource_type="task", resource_id=task_id, task_id=task_id))
    db.flush()
    _mark_unreferenced(db, present - wanted)
    return [service._attachment_json(item) for item in files]


def _mark_unreferenced(db, ids):
    for attachment_id in sorted(ids):
        item = db.scalar(select(SharedFile).where(SharedFile.id == attachment_id).with_for_update())
        if item is not None and not _links(db, attachment_id):
            item.state = "deleting"
            g.shared_attachment_cleanup_due = True


def release_task_files(db, task_id, *, expected):
    """Cancellation releases task references only; submission links survive."""
    _guard(db)
    task_id = positive_bigint(str(task_id) if type(task_id) is int else task_id, "task_id")
    task, assignment, managers = service._current(db, task_id)
    if g.user.role != "admin" or g.user.id not in managers:
        raise AppError(403, "FORBIDDEN", "현재 업무 관리자만 첨부 연결을 해제할 수 있습니다.")
    if not isinstance(expected, ExpectedVersions):
        raise AppError(400, "INVALID_INPUT", "현재 업무 버전·배정 정보가 필요합니다.")
    expected.check(row_version=task.row_version, assignment_revision=task.current_assignment_revision)
    links = list(db.scalars(select(SharedLink).where(SharedLink.resource_type == "task", SharedLink.resource_id == task_id)))
    ids = {link.attachment_id for link in links}
    for link in links:
        db.delete(link)
    db.flush()
    _mark_unreferenced(db, ids)


def attach_new_submission(db, submission, ids):
    _guard(db)
    if submission not in db.new or submission.submitted_by != g.user.id:
        raise AppError(409, "SUBMISSION_IMMUTABLE", "새 제출 트랜잭션에서만 파일을 연결할 수 있습니다.")
    files = selection(db, ids)
    db.flush()
    for item in files:
        db.add(SharedLink(attachment_id=item.id, resource_type="submission", resource_id=submission.id,
                          task_id=submission.task_id, submission_id=submission.id))


def submission_files(db, submission_id):
    require_schema(db)
    return [service._attachment_json(item) for item in db.scalars(select(SharedFile).join(SharedLink)
            .where(SharedLink.resource_type == "submission", SharedLink.resource_id == submission_id)
            .order_by(SharedFile.id))]


def download_info(db, attachment_id):
    item = _file(db, attachment_id)
    if not _links(db, item.id) or item.state != "ready":
        raise AppError(404, "ATTACHMENT_NOT_FOUND", "연결된 첨부가 없습니다.")
    _source(db, item)
    return item


@bp.post("/uploads/start")
@login_required
def start_upload():
    from ..common import payload
    body = payload()
    if set(body) not in ({"name", "size", "sha256"}, {"name", "size", "sha256", "task_id"}):
        raise AppError(400, "INVALID_INPUT", "파일 정보·업무 번호를 확인해 주세요.")
    task_id = positive_bigint(body["task_id"], "task_id") if "task_id" in body else None
    name = service._valid_file_name(body["name"])
    size, digest = body["size"], body["sha256"]
    if type(size) is not int or not 0 <= size <= service.MAX_FILE_BYTES:
        raise AppError(413, "ATTACHMENT_TOO_LARGE", "파일 하나는 25MiB 이하여야 합니다.")
    if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
        raise AppError(400, "INVALID_INPUT", "파일 해시를 확인해 주세요.")
    storage()
    with authenticated_transaction() as db:
        require_schema(db)
        if task_id is not None:
            _upload_task(db, task_id)
        elif g.user.role != "admin":
            raise AppError(403, "FORBIDDEN", "직원 업로드에는 현재 담당 업무 번호가 필요합니다.")
        item = SharedFile(id=str(uuid.uuid4()), owner_id=g.user.id, name=name, size=size, sha256=digest,
                          upload_task_id=task_id,
                          state="uploading", created_at=utcnow(), expires_at=utcnow()+timedelta(hours=24))
        db.add(item)
        data = service._attachment_json(item)
    return success(data, 201)


@bp.post("/uploads/<attachment_id>/chunks")
@login_required
def write_chunk(attachment_id):
    return service._write_chunk(attachment_id, body=None, owner=upload_owner, chunks=SharedChunk, storage=storage)


@bp.post("/uploads/<attachment_id>/complete")
@login_required
def complete_upload(attachment_id):
    return service._complete_upload(attachment_id, body=None, owner=upload_owner, chunks=SharedChunk, storage=storage)


@bp.delete("/uploads/<attachment_id>")
@login_required
def discard_upload(attachment_id):
    with authenticated_transaction() as db:
        item = upload_owner(db, attachment_id)
        item.state = "deleting"
        g.shared_attachment_cleanup_due = True
    return success({"attachment_id": attachment_id})


@bp.get("/tasks/<task_id>")
@login_required
def task_files(task_id):
    task_id = positive_bigint(task_id, "task_id")
    with authenticated_transaction() as db:
        require_schema(db)
        task, assignment, managers = service._current(db, task_id)
        service._readable(db, task, assignment, managers)
        ensure_not_cancelled(db, task_id)
        items = [service._attachment_json(item) for item in db.scalars(select(SharedFile).join(SharedLink)
                 .where(SharedLink.resource_type == "task", SharedLink.resource_id == task_id).order_by(SharedFile.id))]
    return success({"items": items})


@bp.get("/files/<attachment_id>/download")
@login_required
def download(attachment_id):
    return service.download(attachment_id)


@bp.get("/files/<attachment_id>/chunks")
@login_required
def chunks(attachment_id):
    return service.read_json_chunk(attachment_id)


def cleanup_shared_files():
    root = storage()
    with transaction() as db:
        if db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update()) is None:
            raise AppError(503, "DATABASE_UNAVAILABLE", "공통 보호 정보가 필요합니다.")
        require_schema(db)
        candidates = list(db.scalars(select(SharedFile).where(
            (SharedFile.state == "deleting") | (SharedFile.expires_at <= utcnow()))
            .order_by(SharedFile.id).with_for_update()))
        ids = []
        for item in candidates:
            if not _links(db, item.id):
                item.state = "deleting"
                ids.append(item.id)
    removed = 0
    for attachment_id in ids:
        try:
            (root / service._uuid(attachment_id)).unlink(missing_ok=True)
            for part in root.glob(attachment_id + ".*"):
                if part.is_file() or part.is_symlink():
                    part.unlink(missing_ok=True)
        except OSError:
            continue
        with transaction() as db:
            db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            item = db.scalar(select(SharedFile).where(SharedFile.id == attachment_id).with_for_update())
            if item is not None and item.state == "deleting" and not _links(db, attachment_id):
                for part in db.scalars(select(SharedChunk).where(SharedChunk.attachment_id == attachment_id)):
                    db.delete(part)
                db.flush()
                db.delete(item)
                removed += 1
    return removed


@bp.teardown_app_request
def cleanup_after_request(error):
    if getattr(g, "shared_attachment_cleanup_due", False):
        try:
            cleanup_shared_files()
        except Exception as failure:
            # A committed reference change remains successful; deletion is retryable.
            current_app.logger.warning("shared_attachment_cleanup_retry type=%s", type(failure).__name__)


@bp.cli.command("cleanup")
def cleanup_command():
    import click
    click.echo(f"미연결 공용 첨부 정리: {cleanup_shared_files()}건")


@bp.record_once
def register(state):
    state.app.extensions["results_shared_attachments"] = __import__(__name__, fromlist=["bp"])
