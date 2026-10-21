"""Member 2 cancellation/extension storage, imported only by its explicit flag.

This metadata does not allocate a shared migration number or apply shared DDL.
The generated pending revision also enforces one open request in MySQL.
"""
from datetime import date, datetime

from sqlalchemy import (CheckConstraint, Computed, Date, DateTime, ForeignKey,
                        ForeignKeyConstraint, Index, Integer, String, Text, UniqueConstraint)
from sqlalchemy.orm import Mapped, mapped_column

from ...models import Base, utcnow
from .models import TASK_ID_TYPE


EXTENSION_STATUSES = ("pending", "approved", "rejected", "withdrawn", "cancelled")


class TaskCancellation(Base):
    __tablename__ = "task_cancellations"

    task_id: Mapped[int] = mapped_column(TASK_ID_TYPE, ForeignKey("tasks.task_id"), primary_key=True)
    cancelled_by: Mapped[int] = mapped_column(ForeignKey("users.id"))
    reason: Mapped[str] = mapped_column(Text)
    cancelled_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)

    __table_args__ = (
        CheckConstraint("LENGTH(TRIM(reason)) > 0", name="ck_task_cancellation_reason"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4",
         "mysql_collate": "utf8mb4_unicode_ci"},
    )


class TaskDeadlineExtension(Base):
    __tablename__ = "task_deadline_extensions"

    extension_id: Mapped[int] = mapped_column(TASK_ID_TYPE, primary_key=True, autoincrement=True)
    task_id: Mapped[int] = mapped_column(TASK_ID_TYPE)
    assignment_revision: Mapped[int] = mapped_column(Integer)
    original_due_date: Mapped[date] = mapped_column(Date)
    requested_due_date: Mapped[date] = mapped_column(Date)
    requested_by: Mapped[int] = mapped_column(ForeignKey("users.id"))
    reason: Mapped[str] = mapped_column(Text)
    status: Mapped[str] = mapped_column(String(20))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    decided_at: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    decided_by: Mapped[int | None] = mapped_column(ForeignKey("users.id"), nullable=True)
    decision_reason: Mapped[str | None] = mapped_column(Text, nullable=True)
    # NULL values do not collide in MySQL's UNIQUE index. Closed requests stay.
    pending_revision: Mapped[int | None] = mapped_column(Integer, Computed(
        "CASE WHEN status = 'pending' THEN assignment_revision ELSE NULL END", persisted=True))

    __table_args__ = (
        ForeignKeyConstraint(["task_id", "assignment_revision"],
                             ["task_assignments.task_id", "task_assignments.revision"]),
        CheckConstraint("assignment_revision > 0", name="ck_task_extension_revision"),
        CheckConstraint("requested_due_date > original_due_date", name="ck_task_extension_dates"),
        CheckConstraint("LENGTH(TRIM(reason)) > 0", name="ck_task_extension_reason"),
        CheckConstraint("status IN ('pending','approved','rejected','withdrawn','cancelled') "
            "AND HEX(status) IN (HEX('pending'),HEX('approved'),HEX('rejected'),"
            "HEX('withdrawn'),HEX('cancelled'))", name="ck_task_extension_status"),
        CheckConstraint("(status = 'pending' AND decided_at IS NULL AND decided_by IS NULL "
            "AND decision_reason IS NULL) OR (status <> 'pending' AND decided_at IS NOT NULL "
            "AND decided_by IS NOT NULL)", name="ck_task_extension_decision"),
        UniqueConstraint("task_id", "pending_revision", name="uq_task_extension_one_pending"),
        Index("ix_task_extension_requested", "requested_by", "status", "extension_id"),
        Index("ix_task_extension_task", "task_id", "assignment_revision", "extension_id"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4",
         "mysql_collate": "utf8mb4_unicode_ci"},
    )
