import uuid
import click
from flask import current_app, g, request
from sqlalchemy import inspect, select

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, payload, success
from ...db import get_db
from ...models import User, iso, utcnow
from .models import ChatMember, ChatMessage, ChatRoom

TABLES = (ChatRoom.__table__, ChatMember.__table__, ChatMessage.__table__)


def _uuid(value):
    try:
        if not isinstance(value, str) or str(uuid.UUID(value)) != value:
            raise ValueError()
        return value
    except (ValueError, AttributeError):
        raise AppError(400, "INVALID_INPUT", "채팅 식별자가 올바르지 않습니다.") from None


def _text(value, maximum, label):
    if not isinstance(value, str) or not value.strip() or len(value.strip()) > maximum:
        raise AppError(400, "INVALID_INPUT", f"{label}은(는) 1~{maximum}자 이내로 입력해 주세요.")
    return value.strip()


def _ready():
    inspector = inspect(current_app.extensions["db_engine"])
    if any(not inspector.has_table(t.name) for t in TABLES):
        raise AppError(503, "CHAT_SCHEMA_REQUIRED", "채팅 테이블이 없습니다. 서버에서 flask --app remotework chat init-tables를 실행해 주세요.")


def _body(fields):
    data = payload()
    if set(data) != fields:
        raise AppError(400, "INVALID_INPUT", "채팅 입력 항목을 확인해 주세요.")
    return data


def _visible_room(db, room_id, *, locking=False):
    room_query = select(ChatRoom).where(ChatRoom.id == room_id)
    member_query = select(ChatMember).where(ChatMember.room_id == room_id, ChatMember.user_id == g.user.id)
    if locking:
        room_query = room_query.with_for_update().execution_options(populate_existing=True)
        member_query = member_query.with_for_update().execution_options(populate_existing=True)
    room, member = db.scalar(room_query), db.scalar(member_query)
    if room is None or member is None or member.hidden_at is not None:
        raise AppError(404, "CHAT_ROOM_NOT_FOUND", "참여 중인 대화방이 없거나 내 목록에서 삭제된 방입니다.")
    return room, member


def _room_data(db, room):
    # Hidden means a personal list preference; other members remain unchanged.
    members = db.scalars(select(ChatMember).where(ChatMember.room_id == room.id).order_by(ChatMember.user_id)).all()
    return {"id": room.id, "name": room.title, "is_group": room.is_group,
            "participants": [{"id": m.user_id, "name": m.display_name, "department": m.department} for m in members]}


def _message_data(message):
    return {"id": message.message_id, "room_id": message.room_id, "sender_id": message.sender_id,
            "sender_name": message.sender_name, "content": message.content, "sent_at": iso(message.sent_at)}


def _page_keys():
    if set(request.args) - {"after", "limit"} or any(len(v) != 1 for _, v in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "채팅 조회 조건을 확인해 주세요.")
    try:
        limit = int(request.args.get("limit", "100"))
        if not 1 <= limit <= 100:
            raise ValueError()
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "조회 개수를 확인해 주세요.") from None
    return request.args.get("after"), limit


def register_storage(bp):
    # CLI is explicit; normal application requests never execute CREATE/ALTER.
    bp.cli_group = "chat"

    @bp.cli.command("init-tables")
    def init_tables():
        engine = current_app.extensions["db_engine"]
        inspector = inspect(engine)
        if any(not inspector.has_table(name) for name in ("users", "auth_sessions", "admin_guard", "schema_versions")):
            raise click.ClickException("공통 DB 초기화를 먼저 완료해 주세요.")
        for table in TABLES:
            if inspector.has_table(table.name):
                actual = {c["name"] for c in inspector.get_columns(table.name)}
                if actual != set(table.columns.keys()):
                    raise click.ClickException(f"기존 {table.name} 구조가 다릅니다. 삭제하지 말고 DDL 검토가 필요합니다.")
        ChatRoom.metadata.create_all(engine, tables=list(TABLES))
        click.echo("채팅 테이블 준비 완료 (기존 데이터/공통 버전 보존)")

    @bp.get("/rooms")
    @login_required
    def rooms():
        _ready()
        after, limit = _page_keys()
        if after is not None:
            after = _uuid(after)
        db = get_db()
        query = select(ChatRoom).join(ChatMember, ChatRoom.id == ChatMember.room_id).where(
            ChatMember.user_id == g.user.id, ChatMember.hidden_at.is_(None))
        if after:
            query = query.where(ChatRoom.id > after)
        rows = db.scalars(query.order_by(ChatRoom.id).limit(limit + 1)).all()
        return success({"items": [_room_data(db, r) for r in rows[:limit]],
                        "next_cursor": rows[limit-1].id if len(rows) > limit else None})

    @bp.post("/rooms")
    @login_required
    def create_room():
        _ready()
        body = _body({"room_id", "title", "is_group", "participant_ids"})
        room_id = _uuid(body["room_id"])
        title = _text(body["title"], 60, "대화방 제목")
        if any(ord(c) < 32 or ord(c) == 127 for c in title) or type(body["is_group"]) is not bool:
            raise AppError(400, "INVALID_INPUT", "대화방 제목/종류를 확인해 주세요.")
        ids = body["participant_ids"]
        if (not isinstance(ids, list) or any(type(n) is not int or not 1 <= n <= 2147483647 for n in ids)
                or len(set(ids)) != len(ids) or g.user.id in ids
                or (not body["is_group"] and len(ids) != 1) or (body["is_group"] and len(ids) < 2)):
            raise AppError(400, "INVALID_INPUT", "개인은 상대방 1명, 그룹은 2명 이상을 선택해 주세요.")
        with authenticated_transaction() as db:
            existing = db.scalar(select(ChatRoom).where(ChatRoom.id == room_id).with_for_update().execution_options(populate_existing=True))
            if existing is not None:
                members = set(db.scalars(select(ChatMember.user_id).where(ChatMember.room_id == room_id).with_for_update()))
                if existing.created_by != g.user.id or existing.title != title or existing.is_group != body["is_group"] or members != set(ids) | {g.user.id}:
                    raise AppError(409, "CHAT_REQUEST_CONFLICT", "같은 요청 ID에 다른 대화방 내용이 전달됐습니다.")
                _visible_room(db, room_id, locking=True)
                result = _room_data(db, existing)
                status = 200
            else:
                users = db.scalars(select(User).where(User.id.in_(ids)).order_by(User.id).with_for_update().execution_options(populate_existing=True)).all()
                if len(users) != len(ids) or any(not u.is_active or u.role not in ("employee", "admin") for u in users):
                    raise AppError(409, "CHAT_PARTICIPANT_UNAVAILABLE", "선택한 참여자가 비활성화되었거나 사용할 수 없습니다.")
                room = ChatRoom(id=room_id, title=title, is_group=body["is_group"], created_by=g.user.id)
                db.add(room)
                db.flush()
                for user in sorted([g.user, *users], key=lambda u: u.id):
                    db.add(ChatMember(room_id=room_id, user_id=user.id, display_name=user.display_name, department=user.department))
                db.flush()
                audit("chat.room_created", "chat_room", room_id)
                result, status = _room_data(db, room), 201
        return success({"room": result}, status)

    @bp.post("/rooms/<room_id>/hide")
    @login_required
    def hide_room(room_id):
        _ready()
        room_id = _uuid(room_id)
        _body(set())
        with authenticated_transaction() as db:
            # Also accept repeated hide requests without a second audit event.
            room = db.scalar(select(ChatRoom).where(ChatRoom.id == room_id).with_for_update())
            member = db.scalar(select(ChatMember).where(ChatMember.room_id == room_id, ChatMember.user_id == g.user.id)
                               .with_for_update().execution_options(populate_existing=True))
            if room is None or member is None:
                raise AppError(404, "CHAT_ROOM_NOT_FOUND", "참여 중인 대화방이 없습니다.")
            if member.hidden_at is None:
                member.hidden_at = utcnow()
                audit("chat.room_hidden", "chat_room", room_id)
        return success({"message": "내 대화방 목록에서만 삭제했습니다. 다른 참여자의 대화는 유지됩니다."})

    @bp.get("/rooms/<room_id>/messages")
    @login_required
    def messages(room_id):
        _ready()
        room_id = _uuid(room_id)
        after, limit = _page_keys()
        if after is None:
            after = "0"
        if not after.isascii() or not after.isdecimal() or len(after) > 19 or int(after) > 9223372036854775807:
            raise AppError(400, "INVALID_INPUT", "메시지 조회 커서를 확인해 주세요.")
        db = get_db()
        _visible_room(db, room_id)
        rows = db.scalars(select(ChatMessage).where(ChatMessage.room_id == room_id, ChatMessage.seq > int(after))
                          .order_by(ChatMessage.seq).limit(limit + 1)).all()
        return success({"items": [_message_data(m) for m in rows[:limit]],
                        "next_cursor": str(rows[limit-1].seq) if len(rows) > limit else None})

    @bp.post("/rooms/<room_id>/messages")
    @login_required
    def send_message(room_id):
        _ready()
        room_id = _uuid(room_id)
        body = _body({"message_id", "content"})
        message_id = _uuid(body["message_id"])
        content = _text(body["content"], 2000, "메시지")
        with authenticated_transaction() as db:
            _visible_room(db, room_id, locking=True)
            message = db.scalar(select(ChatMessage).where(ChatMessage.message_id == message_id).with_for_update().execution_options(populate_existing=True))
            if message is not None:
                if message.room_id != room_id or message.sender_id != g.user.id or message.content != content:
                    raise AppError(409, "CHAT_REQUEST_CONFLICT", "같은 메시지 ID에 다른 내용이 전달됐습니다.")
                result, status = _message_data(message), 200
            else:
                message = ChatMessage(message_id=message_id, room_id=room_id, sender_id=g.user.id,
                                      sender_name=g.user.display_name, content=content)
                db.add(message)
                db.flush()
                db.refresh(message)
                audit("chat.message_sent", "chat_message", message_id, {"room_id": room_id})
                result, status = _message_data(message), 201
        return success({"message": result}, status)
