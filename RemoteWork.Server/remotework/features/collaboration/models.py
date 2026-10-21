"""012 collaboration model. Separate metadata keeps common init-db unchanged."""
from sqlalchemy import (BigInteger, CheckConstraint, Computed, Date, DateTime,
    ForeignKey, ForeignKeyConstraint, Index, Integer, String, Text, UniqueConstraint)
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column
from ...models import User
from ..tasks.models import TaskAssignment


class FeatureBase(DeclarativeBase):
    pass


STATES = ("pending", "accepted", "declined", "withdrawn", "completed", "left", "expired", "ended")


class CollaborationRequest(FeatureBase):
    __tablename__ = "collaboration_requests"
    request_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    task_id: Mapped[int] = mapped_column(BigInteger)
    assignment_revision: Mapped[int] = mapped_column(Integer)
    task_row_version: Mapped[int] = mapped_column(BigInteger)
    requester_id: Mapped[int] = mapped_column(ForeignKey(User.__table__.c.id))
    recipient_id: Mapped[int] = mapped_column(ForeignKey(User.__table__.c.id))
    requester_name: Mapped[str] = mapped_column(String(100))
    task_title: Mapped[str] = mapped_column(String(200))
    title: Mapped[str] = mapped_column(String(200))
    request_text: Mapped[str] = mapped_column(Text)
    desired_date: Mapped[object] = mapped_column(Date)
    status: Mapped[str] = mapped_column(String(20))
    row_version: Mapped[int] = mapped_column(BigInteger)
    created_at: Mapped[object] = mapped_column(DateTime)
    updated_at: Mapped[object] = mapped_column(DateTime)
    ended_at: Mapped[object | None] = mapped_column(DateTime, nullable=True)
    end_reason: Mapped[str | None] = mapped_column(String(40), nullable=True)
    active_revision: Mapped[int | None] = mapped_column(Integer, Computed(
        "CASE WHEN status IN ('pending','accepted') THEN assignment_revision ELSE NULL END", persisted=True))
    __table_args__ = (
        ForeignKeyConstraint([task_id, assignment_revision],
            [TaskAssignment.__table__.c.task_id, TaskAssignment.__table__.c.revision]),
        UniqueConstraint("task_id", "active_revision", "recipient_id", name="uq_collaboration_active_target"),
        CheckConstraint("requester_id <> recipient_id", name="ck_collaboration_not_self"),
        CheckConstraint("assignment_revision > 0 AND row_version > 0 AND task_row_version > 0", name="ck_collaboration_versions"),
        CheckConstraint("status IN ('pending','accepted','declined','withdrawn','completed','left','expired','ended') "
            "AND HEX(status) IN (HEX('pending'),HEX('accepted'),HEX('declined'),HEX('withdrawn'),"
            "HEX('completed'),HEX('left'),HEX('expired'),HEX('ended'))", name="ck_collaboration_status"),
        Index("ix_collaboration_recipient_status", "recipient_id", "status", "created_at"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})


class CollaborationEvent(FeatureBase):
    __tablename__ = "collaboration_events"
    event_id: Mapped[int] = mapped_column(BigInteger, primary_key=True, autoincrement=True)
    request_id: Mapped[str] = mapped_column(ForeignKey("collaboration_requests.request_id"))
    actor_id: Mapped[int | None] = mapped_column(ForeignKey(User.__table__.c.id), nullable=True)
    status: Mapped[str] = mapped_column(String(20))
    reason: Mapped[str | None] = mapped_column(String(40), nullable=True)
    occurred_at: Mapped[object] = mapped_column(DateTime)
    __table_args__ = (Index("ix_collaboration_event_request", "request_id", "event_id"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
