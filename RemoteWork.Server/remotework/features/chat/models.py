"""Development schema aligned with member 1 reviewed migration 005; no shared deployment approval."""
from datetime import datetime
from sqlalchemy import BigInteger, Boolean, DateTime, ForeignKey, Index, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column
from ...models import Base, utcnow


class ChatRoom(Base):
    __tablename__ = "chat_rooms"
    __table_args__ = {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"}
    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    title: Mapped[str] = mapped_column(String(60))
    is_group: Mapped[bool] = mapped_column(Boolean)
    created_by: Mapped[int] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)


class ChatMember(Base):
    __tablename__ = "chat_members"
    room_id: Mapped[str] = mapped_column(ForeignKey("chat_rooms.id"), primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), primary_key=True)
    display_name: Mapped[str] = mapped_column(String(80))
    department: Mapped[str] = mapped_column(String(80))
    hidden_at: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    __table_args__ = (Index("ix_chat_members_user_hidden", "user_id", "hidden_at", "room_id"), {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})


class ChatMessage(Base):
    __tablename__ = "chat_messages"
    # The public message ID is a UUID. This sequence orders equal-time messages.
    seq: Mapped[int] = mapped_column(BigInteger().with_variant(Integer, "sqlite"), primary_key=True, autoincrement=True)
    message_id: Mapped[str] = mapped_column(String(36), unique=True)
    room_id: Mapped[str] = mapped_column(ForeignKey("chat_rooms.id"))
    sender_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    sender_name: Mapped[str] = mapped_column(String(80))
    content: Mapped[str] = mapped_column(Text)
    sent_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    __table_args__ = (Index("ix_chat_messages_room_seq", "room_id", "seq"), {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
