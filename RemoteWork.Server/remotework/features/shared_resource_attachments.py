"""Member 3 notice/chat file ports. Member 4 supplies current resource authority.

No feature routes, policies, consumer writes, DDL application or default grants.
Reference helpers run in the caller's authenticated TX; only transport owns a TX.
"""
from dataclasses import dataclass
from datetime import timedelta
import uuid

from flask import Blueprint, current_app, g, request
from sqlalchemy import CheckConstraint, Column, ForeignKey, Integer, MetaData, String, Table, inspect, select
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column

from ..auth import authenticated_transaction, login_required
from ..common import AppError, payload, success
from ..models import utcnow
from . import results as service, shared_attachments as shared

bp = Blueprint("resource_attachments", __name__, url_prefix="/api/v1/results/shared/resources")

# FK targets only: do not import/register member 4 models or query their tables.
# These are outside both Base and ResourceBase metadata and are never created.
_targets = MetaData()
_notice = Table("notices", _targets, Column("id", Integer))
_room = Table("chat_rooms", _targets, Column("id", String(36)))
_message = Table("chat_messages", _targets, Column("message_id", String(36)))


class ResourceBase(DeclarativeBase):
    pass


class ResourceUpload(ResourceBase):
    __tablename__ = "result_resource_uploads"
    attachment_id: Mapped[str] = mapped_column(ForeignKey(shared.SharedFile.id), primary_key=True)
    resource_type: Mapped[str] = mapped_column(String(16))
    context_id: Mapped[str | None] = mapped_column(ForeignKey(_room.c.id), nullable=True)
    __table_args__ = (CheckConstraint(
        "(HEX(resource_type)=HEX('notice') AND context_id IS NULL) OR "
        "(HEX(resource_type)=HEX('chat') AND context_id IS NOT NULL)",
        name="ck_resource_upload_context"), service.TABLE_OPTIONS)


class NoticeFile(ResourceBase):
    __tablename__ = "result_notice_files"
    notice_id: Mapped[int] = mapped_column(ForeignKey(_notice.c.id), primary_key=True)
    attachment_id: Mapped[str] = mapped_column(ForeignKey(shared.SharedFile.id), primary_key=True)
    __table_args__ = (service.TABLE_OPTIONS,)


class ChatFile(ResourceBase):
    __tablename__ = "result_chat_files"
    message_id: Mapped[str] = mapped_column(ForeignKey(_message.c.message_id), primary_key=True)
    attachment_id: Mapped[str] = mapped_column(ForeignKey(shared.SharedFile.id), primary_key=True)
    __table_args__ = (service.TABLE_OPTIONS,)


TABLES = [ResourceUpload.__table__, NoticeFile.__table__, ChatFile.__table__]


@dataclass(frozen=True)
class ResourceAccess:
    """Explicit authorization result: notice=None; chat=current room UUID."""
    context_id: str | None = None


def register_adapter(app, kind, *, authorize_upload, authorize_resource):
    """Member 4 installs current locking permission callbacks before serving.

    upload(db, context_id) must return exactly True. resource(db, id, operation)
    must return ResourceAccess after checking current read/attach/release rights.
    Callbacks must not commit, start another TX, perform IO or grant task rights.
    """
    _model(kind)
    if app._got_first_request or not all(map(callable, (authorize_upload, authorize_resource))):
        raise ValueError("Register explicit authority callbacks before the first request")
    adapters = app.extensions.setdefault("resource_attachment_adapters", {})
    if kind in adapters:
        raise ValueError("An existing authority adapter must not be overwritten")
    adapters[kind] = (authorize_upload, authorize_resource)


def _model(kind):
    if kind == "notice":
        return NoticeFile, NoticeFile.notice_id
    if kind == "chat":
        return ChatFile, ChatFile.message_id
    raise AppError(400, "INVALID_INPUT", "공지 또는 채팅 첨부를 선택해 주세요.")


def _resource_id(kind, value):
    _model(kind)
    if kind == "chat":
        return service._uuid(value)
    value = service.positive_bigint(str(value) if type(value) is int else value, "notice_id")
    if value > 2147483647:
        raise AppError(400, "INVALID_INPUT", "공지 번호 범위를 확인해 주세요.")
    return value


def _context(kind, value):
    _model(kind)
    if kind == "notice":
        if value is not None:
            raise AppError(400, "INVALID_INPUT", "공지 임시 첨부의 context_id는 null입니다.")
        return None
    return service._uuid(value)


def _adapter(kind):
    _model(kind)
    adapter = current_app.extensions.get("resource_attachment_adapters", {}).get(kind)
    if adapter is None:
        raise AppError(503, "RESOURCE_ATTACHMENT_ADAPTER_REQUIRED", "공지·채팅 담당자의 현재 권한 연결이 필요합니다.")
    return adapter


def require_schema(db, *, optional=False):
    if current_app.extensions.get("resource_attachment_schema_verified"):
        return True
    inspector = inspect(db.get_bind())
    present = [inspector.has_table(t.name) for t in TABLES]
    if optional and not any(present):
        return False
    if not all(present) or any(
        {c["name"] for c in inspector.get_columns(t.name)} != set(t.columns.keys()) or
        inspector.get_pk_constraint(t.name).get("constrained_columns") != [c.name for c in t.primary_key]
        for t in TABLES
    ):
        raise AppError(503, "RESOURCE_ATTACHMENT_SCHEMA_REQUIRED", "공지·채팅 첨부 DB 검토·적용이 필요합니다.")
    current_app.extensions["resource_attachment_schema_verified"] = True
    return True


def _access(db, kind, resource_id, operation):
    shared._guard(db)
    resource_id = _resource_id(kind, resource_id)
    access = _adapter(kind)[1](db, resource_id, operation)
    if not isinstance(access, ResourceAccess):
        raise AppError(403, "FORBIDDEN", "현재 자원의 첨부 처리 권한이 없습니다.")
    context = _context(kind, access.context_id)
    require_schema(db)
    return resource_id, context


def _scope(db, attachment_id):
    if not require_schema(db, optional=True):
        return None
    return db.scalar(select(ResourceUpload).where(ResourceUpload.attachment_id == attachment_id)
                     .with_for_update().execution_options(populate_existing=True))


def is_scoped(db, attachment_id):
    return _scope(db, attachment_id) is not None


def references(db, attachment_id):
    if not require_schema(db, optional=True):
        return []
    found = []
    for kind in ("notice", "chat"):
        model, column = _model(kind)
        found.extend((kind, getattr(row, column.key)) for row in db.scalars(
            select(model).where(model.attachment_id == attachment_id).order_by(column)
            .with_for_update().execution_options(populate_existing=True)))
    return found


def remove_upload_scope(db, attachment_id):
    scope = _scope(db, attachment_id)
    if scope is not None:
        db.delete(scope)
        db.flush()


def _upload_owner(db, attachment_id, kind):
    shared._guard(db)
    context_callback = _adapter(kind)[0]
    require_schema(db)
    item = shared._file(db, attachment_id)
    scope = _scope(db, item.id)
    if (scope is None or scope.resource_type != kind or item.owner_id != g.user.id or
            shared._links(db, item.id) or references(db, item.id)):
        raise AppError(403, "FORBIDDEN", "본인 미연결 공지·채팅 임시 파일만 처리할 수 있습니다.")
    if item.expires_at <= utcnow():
        raise AppError(409, "ATTACHMENT_UNAVAILABLE", "임시 파일이 만료되었습니다.")
    if context_callback(db, _context(kind, scope.context_id)) is not True:
        raise AppError(403, "FORBIDDEN", "현재 업로드 권한이 없습니다.")
    return item


def replace_resource_files(db, kind, resource_id, attachment_ids):
    """Final complete set in the caller's existing resource/UUID/audit TX.

    Do not call again when replaying a successful receipt. Preserve the original
    request array/body/key on unknown outcome; no automatic discard/reupload.
    """
    resource_id, context = _access(db, kind, resource_id, "attach")
    if not isinstance(attachment_ids, list) or len(attachment_ids) > service.MAX_FILES:
        raise AppError(400, "INVALID_INPUT", "첨부는 최대 10개입니다.")
    ids = [service._uuid(value) for value in attachment_ids]
    if len(set(ids)) != len(ids):
        raise AppError(400, "INVALID_INPUT", "중복 첨부는 허용하지 않습니다.")
    files = []
    for attachment_id in sorted(ids):
        item = shared._file(db, attachment_id)
        scope = _scope(db, item.id)
        if scope is None or scope.resource_type != kind or scope.context_id != context:
            raise AppError(403, "FORBIDDEN", "같은 공지 또는 채팅방 범위의 파일만 연결할 수 있습니다.")
        if item.state != "ready":
            raise AppError(409, "ATTACHMENT_UNAVAILABLE", "전송 완료된 파일만 연결할 수 있습니다.")
        links = references(db, item.id)
        if not links:
            if item.owner_id != g.user.id or item.expires_at <= utcnow():
                raise AppError(403, "FORBIDDEN", "본인이 올린 유효한 임시 파일만 연결할 수 있습니다.")
        else:
            allowed = False
            for source_kind, source_id in links:
                try:
                    _access(db, source_kind, source_id, "read")
                    allowed = True
                    break
                except AppError as error:
                    if error.status not in (403, 404):
                        raise
            if not allowed:
                raise AppError(403, "FORBIDDEN", "원래 첨부 자원의 현재 열람 권한이 필요합니다.")
        files.append(item)
    model, column = _model(kind)
    current = list(db.scalars(select(model).where(column == resource_id)
                   .order_by(model.attachment_id).with_for_update().execution_options(populate_existing=True)))
    present = {row.attachment_id for row in current}
    for row in current:
        if row.attachment_id not in ids:
            db.delete(row)
    for item in files:
        if item.id not in present:
            db.add(model(**{column.key: resource_id, "attachment_id": item.id}))
    db.flush()
    shared._mark_unreferenced(db, present - set(ids))
    return [service._attachment_json(item) for item in files]


def read_resource_attachment_ids(db, kind, resource_id):
    """Current sorted IDs in the caller's authenticated TX; no commit or IO.

    Resource authority remains the owner's current read callback. This exposes
    only this resource's IDs, never other resources or their readers.
    """
    resource_id, _ = _access(db, kind, resource_id, "read")
    model, column = _model(kind)
    return tuple(db.scalars(select(model.attachment_id).where(column == resource_id)
                 .order_by(model.attachment_id).with_for_update()))


def release_resource_files(db, kind, resource_id):
    """Explicit resource deletion only after owner policy approval; not hide."""
    resource_id, _ = _access(db, kind, resource_id, "release")
    model, column = _model(kind)
    rows = list(db.scalars(select(model).where(column == resource_id)
                .order_by(model.attachment_id).with_for_update().execution_options(populate_existing=True)))
    for row in rows:
        db.delete(row)
    db.flush()
    shared._mark_unreferenced(db, {row.attachment_id for row in rows})


@bp.get("/<kind>/<resource_id>/files")
@login_required
def resource_files(kind, resource_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "첨부 목록 조회 조건을 확인해 주세요.")
    with authenticated_transaction() as db:
        resource_id, _ = _access(db, kind, resource_id, "read")
        model, column = _model(kind)
        items = list(db.scalars(select(shared.SharedFile).join(model)
                     .where(column == resource_id).order_by(shared.SharedFile.id)
                     .with_for_update().execution_options(populate_existing=True)))
        if any(item.state != "ready" for item in items):
            raise AppError(409, "ATTACHMENT_UNAVAILABLE", "첨부 연결 상태를 확인해 주세요.")
        data = [service._attachment_json(item) for item in items]
    return success({"items": data})


def _download_info(db, kind, resource_id, attachment_id):
    resource_id, _ = _access(db, kind, resource_id, "read")
    item = shared._file(db, attachment_id)
    if item.state != "ready" or (kind, resource_id) not in references(db, item.id):
        raise AppError(404, "ATTACHMENT_NOT_FOUND", "현재 자원에 연결된 첨부가 없습니다.")
    return item, True


@bp.get("/<kind>/<resource_id>/files/<attachment_id>/download")
@login_required
def download(kind, resource_id, attachment_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "첨부 다운로드 조건을 확인해 주세요.")
    return service.download(attachment_id, reader=lambda db, value: _download_info(db, kind, resource_id, value))


@bp.get("/<kind>/<resource_id>/files/<attachment_id>/chunks")
@login_required
def chunks(kind, resource_id, attachment_id):
    return service.read_json_chunk(attachment_id, reader=lambda db, value: _download_info(db, kind, resource_id, value))


@bp.post("/<kind>/uploads/start")
@login_required
def start_upload(kind, *, body=None):
    body = service._fields({"name", "size", "sha256", "context_id"}, payload() if body is None else body)
    context = _context(kind, body["context_id"])
    callback = _adapter(kind)[0]
    name = service._valid_file_name(body["name"])
    size, digest = body["size"], body["sha256"]
    if type(size) is not int or not 0 <= size <= service.MAX_FILE_BYTES:
        raise AppError(413, "ATTACHMENT_TOO_LARGE", "파일 하나는 25MiB 이하여야 합니다.")
    if not isinstance(digest, str) or len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
        raise AppError(400, "INVALID_INPUT", "파일 해시를 확인해 주세요.")
    shared.storage()
    with authenticated_transaction() as db:
        shared.require_schema(db)
        require_schema(db)
        if callback(db, context) is not True:
            raise AppError(403, "FORBIDDEN", "현재 업로드 권한이 없습니다.")
        now = utcnow()
        item = shared.SharedFile(id=str(uuid.uuid4()), owner_id=g.user.id, name=name,
            size=size, sha256=digest, state="uploading", created_at=now, expires_at=now+timedelta(hours=24))
        db.add(item)
        db.flush()
        db.add(ResourceUpload(attachment_id=item.id, resource_type=kind, context_id=context))
        data = service._attachment_json(item)
    return success(data, 201)


@bp.get("/<kind>/uploads/<attachment_id>")
@login_required
def upload_status(kind, attachment_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "업로드 조회 조건을 확인해 주세요.")
    with authenticated_transaction() as db:
        item = _upload_owner(db, attachment_id, kind)
        offsets = list(db.scalars(select(shared.SharedChunk.offset)
                       .where(shared.SharedChunk.attachment_id == item.id).order_by(shared.SharedChunk.offset)))
        data = dict(service._attachment_json(item), state=item.state, received_offsets=offsets)
    return success(data)


@bp.post("/<kind>/uploads/<attachment_id>/chunks")
@login_required
def write_chunk(kind, attachment_id, *, body=None):
    return service._write_chunk(attachment_id, body=body, owner=lambda db, value: _upload_owner(db, value, kind),
                                chunks=shared.SharedChunk, storage=shared.storage)


@bp.post("/<kind>/uploads/<attachment_id>/complete")
@login_required
def complete_upload(kind, attachment_id, *, body=None):
    return service._complete_upload(attachment_id, body=body, owner=lambda db, value: _upload_owner(db, value, kind),
                                   chunks=shared.SharedChunk, storage=shared.storage)


@bp.delete("/<kind>/uploads/<attachment_id>")
@login_required
def discard_upload(kind, attachment_id):
    with authenticated_transaction() as db:
        item = _upload_owner(db, attachment_id, kind)
        item.state = "deleting"
        g.shared_attachment_cleanup_due = True
    return success({"attachment_id": item.id})
