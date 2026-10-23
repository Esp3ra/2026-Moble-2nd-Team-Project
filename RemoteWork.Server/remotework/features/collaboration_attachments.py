"""Member 4 current authority and same-transaction consumers of member 3 ports.

No DDL, storage, task permission or deletion policy is installed here.
"""
import hashlib
import json
import os

from flask import current_app, g
from sqlalchemy import select

from ..common import AppError
from ..models import AuditEvent


def provider():
    value = current_app.extensions.get("results_resource_attachments")
    if value is None:
        raise AppError(503, "RESOURCE_ATTACHMENT_ADAPTER_REQUIRED", "서버의 공지·채팅 첨부 기능을 준비해 주세요.")
    return value


def notice_upload(db, context_id):
    return context_id is None and g.user.is_active and g.user.role == "admin"


def notice_resource(db, notice_id, operation):
    from .notices.models import Notice
    notice = db.scalar(select(Notice).where(Notice.id == notice_id).with_for_update()
                       .execution_options(populate_existing=True))
    if notice is None:
        raise AppError(404, "NOTICE_NOT_FOUND", "공지사항이 없습니다.")
    if operation == "release":
        # File retention is separate from the adopted notice-confirmation CASCADE.
        raise AppError(409, "NOTICE_ATTACHMENT_RETENTION_PENDING", "첨부가 있는 공지의 삭제·보존 정책 협의가 필요합니다.")
    if (not g.user.is_active or (operation == "attach" and g.user.role != "admin") or
            (operation == "read" and g.user.role != "admin" and notice.target not in ("전 직원", g.user.department))):
        raise AppError(403, "FORBIDDEN", "현재 공지 대상 또는 관리자 권한이 필요합니다.")
    return provider().ResourceAccess(None)


def chat_upload(db, room_id):
    from .chat.storage import _visible_room
    _visible_room(db, room_id, locking=True)
    return g.user.is_active and g.user.role in ("admin", "employee")


def chat_resource(db, message_id, operation):
    from .chat.models import ChatMessage
    item = db.scalar(select(ChatMessage).where(ChatMessage.message_id == message_id).with_for_update()
                     .execution_options(populate_existing=True))
    if item is None:
        raise AppError(404, "CHAT_MESSAGE_NOT_FOUND", "메시지가 없습니다.")
    if chat_upload(db, item.room_id) is not True:
        raise AppError(403, "FORBIDDEN", "현재 채팅방 참여 권한이 필요합니다.")
    if operation == "release" or (operation == "attach" and item.sender_id != g.user.id):
        raise AppError(403, "FORBIDDEN", "현재 메시지 작성 권한이 필요합니다.")
    return provider().ResourceAccess(item.room_id)


def register(app, kind):
    # Registration alone neither enables routes nor creates tables/default grants.
    if not (app.config.get("ENABLE_RESULTS_DRAFT", os.getenv("REMOTEWORK_ENABLE_RESULTS_DRAFT") == "1")
            and app.config.get("ENABLE_SHARED_ATTACHMENTS_DRAFT", os.getenv("REMOTEWORK_ENABLE_SHARED_ATTACHMENTS_DRAFT") == "1")):
        return
    from . import shared_resource_attachments as resource
    resource.register_adapter(app, kind,
        authorize_upload=notice_upload if kind == "notice" else chat_upload,
        authorize_resource=notice_resource if kind == "notice" else chat_resource)


def fingerprint(body):
    return hashlib.sha256(json.dumps(body, sort_keys=True, ensure_ascii=True,
                                    separators=(",", ":"), allow_nan=False).encode("ascii")).hexdigest()


def original_message_matches(db, message_id, body):
    # The immutable sent audit records the exact original body hash (array order
    # included). Replay never reattaches files, even after another read fails.
    rows = list(db.scalars(select(AuditEvent).where(AuditEvent.action == "chat.message_sent",
                AuditEvent.resource_type == "chat_message", AuditEvent.resource_id == message_id)
                .order_by(AuditEvent.id).with_for_update().execution_options(populate_existing=True)))
    if len(rows) != 1:
        raise AppError(409, "CHAT_REQUEST_CONFLICT", "원래 메시지 요청의 저장 근거를 확인할 수 없습니다.")
    recorded = json.loads(rows[0].details_json).get("request_hash")
    return recorded == fingerprint(body) if recorded else not body.get("attachment_ids")


def block_notice_file_delete(db, notice_id):
    # The member4 creation audit records actual attachment use atomically. Do
    # not reach into member3 private models, and keep the block when transport
    # routes are OFF. Legacy filename-only notices retain their delete behavior.
    rows = db.scalars(select(AuditEvent).where(AuditEvent.action == "notice.created",
        AuditEvent.resource_type == "notice", AuditEvent.resource_id == str(notice_id))
        .with_for_update().execution_options(populate_existing=True))
    if any(json.loads(row.details_json).get("attachment_count", 0) > 0 for row in rows):
        raise AppError(409, "NOTICE_ATTACHMENT_RETENTION_PENDING", "첨부가 있는 공지는 삭제·보존 정책 협의 후 삭제할 수 있습니다.")
