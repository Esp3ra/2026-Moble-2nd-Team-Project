import json
from datetime import datetime
from sqlalchemy import Boolean, DateTime, ForeignKey, Index, String, Text
from sqlalchemy.orm import Mapped, mapped_column
from ...models import Base, iso, utcnow

TARGETS = ("�� ����", "��ȹ��", "������", "��������")


class Notice(Base):
    __tablename__ = "notices"
    id: Mapped[int] = mapped_column(primary_key=True)
    title: Mapped[str] = mapped_column(String(200))
    content: Mapped[str] = mapped_column(Text)
    target: Mapped[str] = mapped_column(String(80))
    is_pinned: Mapped[bool] = mapped_column(Boolean, default=False)
    author_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    author: Mapped[str] = mapped_column(String(80))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    # Metadata only: never store an administrator's local absolute paths.
    attachment_names_json: Mapped[str] = mapped_column(Text, default="[]")
    __table_args__ = (Index("ix_notices_target_created", "target", "created_at"),)

    def public(self):
        return {k: getattr(self, k) for k in
                ("id", "title", "content", "target", "is_pinned", "author_id", "author")} | {
            "created_at": iso(self.created_at),
            "attachment_names": json.loads(self.attachment_names_json)}
