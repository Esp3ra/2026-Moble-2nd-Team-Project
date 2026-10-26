"""Member 2 manual review messages; never a result submission or decision."""
from datetime import datetime

from sqlalchemy import (BigInteger, CheckConstraint, Computed, DateTime, ForeignKey,
                        ForeignKeyConstraint, Index, Integer, String, Text, UniqueConstraint)
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column

from ...models import User, utcnow
from ..results import Submission
from .models import TASK_ID_TYPE, TaskAssignment


class ReviewRequestBase(DeclarativeBase):
    """Explicit raw019 only; common init-db must never create this table."""
    pass


class TaskReviewRequest(ReviewRequestBase):
    __tablename__ = "task_review_requests"
    request_id: Mapped[int] = mapped_column(TASK_ID_TYPE, primary_key=True, autoincrement=True)
    task_id: Mapped[int] = mapped_column(TASK_ID_TYPE)
    assignment_revision: Mapped[int] = mapped_column(Integer)
    submission_id: Mapped[int] = mapped_column(ForeignKey(Submission.id))
    round_no: Mapped[int] = mapped_column(BigInteger)
    requested_by: Mapped[int] = mapped_column(ForeignKey(User.id))
    reviewer_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    reason: Mapped[str] = mapped_column(Text)
    status: Mapped[str] = mapped_column(String(20))
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    acknowledged_at: Mapped[datetime | None] = mapped_column(DateTime, nullable=True)
    acknowledged_by: Mapped[int | None] = mapped_column(ForeignKey(User.id), nullable=True)
    pending_submission_id: Mapped[int | None] = mapped_column(BigInteger, Computed(
        "CASE WHEN status = 'pending' THEN submission_id ELSE NULL END", persisted=True))

    __table_args__ = (
        ForeignKeyConstraint(["task_id", "assignment_revision"],
                             [TaskAssignment.task_id, TaskAssignment.revision]),
        CheckConstraint("assignment_revision > 0 AND round_no > 0", name="ck_task_review_versions"),
        CheckConstraint("requested_by <> reviewer_id", name="ck_task_review_other"),
        CheckConstraint("LENGTH(TRIM(reason)) > 0", name="ck_task_review_reason"),
        CheckConstraint("HEX(status) IN (HEX('pending'),HEX('acknowledged'))", name="ck_task_review_status"),
        CheckConstraint("(status = 'pending' AND acknowledged_at IS NULL AND acknowledged_by IS NULL) "
                        "OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND acknowledged_by IS NOT NULL)",
                        name="ck_task_review_acknowledgement"),
        UniqueConstraint("pending_submission_id", name="uq_task_review_pending_submission"),
        Index("ix_task_review_task", "task_id", "assignment_revision", "request_id"),
        Index("ix_task_review_requester", "requested_by", "request_id"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"},
    )
