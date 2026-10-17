"""Task read model: member 1 accepted its read/type contract on 2026-10-17.

These tables support task reads only. Registering their metadata does not migrate
an existing database. DDL approval and shared application remain separate gates.
Submission, reviewer selection and status transitions await the joint write contract.
"""
from datetime import date, datetime

from sqlalchemy import BigInteger, CheckConstraint, Date, DateTime, ForeignKey, Index, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column

from ...models import Base, utcnow


TASK_STATUSES = ("not_started", "in_progress", "submitted", "approved", "returned")

# SQLite requires the exact INTEGER type for an auto-incrementing primary key.
TASK_ID_TYPE = BigInteger().with_variant(Integer, "sqlite")


class TaskRecord(Base):
    __tablename__ = "tasks"

    task_id: Mapped[int] = mapped_column(TASK_ID_TYPE, primary_key=True, autoincrement=True)
    title: Mapped[str] = mapped_column(String(200))
    description: Mapped[str] = mapped_column(Text)
    # No implicit default: this read-only draft does not implement task creation.
    task_status: Mapped[str] = mapped_column(String(30))
    start_date: Mapped[date] = mapped_column(Date)
    due_date: Mapped[date] = mapped_column(Date)
    current_assignment_revision: Mapped[int] = mapped_column(Integer)
    row_version: Mapped[int] = mapped_column(BigInteger, default=1)
    created_by: Mapped[int] = mapped_column(ForeignKey("users.id"))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)

    __table_args__ = (
        # Keep SQLite's TEXT-vs-BLOB distinction and close MySQL's case/PAD SPACE equivalences.
        CheckConstraint("task_status IN ('not_started','in_progress','submitted','approved','returned') "
                        "AND HEX(task_status) IN (HEX('not_started'),HEX('in_progress'),"
                        "HEX('submitted'),HEX('approved'),HEX('returned'))",
                        name="ck_tasks_draft_status"),
        CheckConstraint("current_assignment_revision > 0", name="ck_tasks_assignment_revision"),
        CheckConstraint("row_version > 0", name="ck_tasks_row_version"),
        CheckConstraint("due_date >= start_date", name="ck_tasks_dates"),
        Index("ix_tasks_due_task", "due_date", "task_id"),
    )


class TaskAssignment(Base):
    __tablename__ = "task_assignments"

    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"), primary_key=True)
    revision: Mapped[int] = mapped_column(Integer, primary_key=True)
    assignee_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    assigned_by: Mapped[int] = mapped_column(ForeignKey("users.id"))
    assigned_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    accepted_at: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)

    __table_args__ = (
        CheckConstraint("revision > 0", name="ck_task_assignments_revision"),
        Index("ix_task_assignments_assignee", "assignee_id", "task_id", "revision"),
    )


class TaskManager(Base):
    """Explicit task managers, not submission reviewers or organization hierarchy."""
    __tablename__ = "task_managers"

    task_id: Mapped[int] = mapped_column(ForeignKey("tasks.task_id"), primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), primary_key=True)

    __table_args__ = (Index("ix_task_managers_user_task", "user_id", "task_id"),)
