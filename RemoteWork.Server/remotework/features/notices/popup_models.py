"""Local popup proposal: separate metadata, never part of common init-db."""
from datetime import datetime
from sqlalchemy import DateTime, ForeignKey, Index
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column
from ...models import User, utcnow
from .models import Notice


class PopupBase(DeclarativeBase):
    pass


class NoticeConfirmation(PopupBase):
    __tablename__ = "notice_confirmations"
    notice_id: Mapped[int] = mapped_column(ForeignKey(Notice.id, ondelete="CASCADE"), primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey(User.id), primary_key=True)
    confirmed_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)

    __table_args__ = (Index("ix_notice_confirmations_user", "user_id"),
                      {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
