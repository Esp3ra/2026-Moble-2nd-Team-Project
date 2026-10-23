"""Same-TX attendance and encrypted token replay; trusted server construction only."""
from datetime import datetime, timedelta
import json
import os
import secrets
from uuid import uuid4

from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from flask import current_app
from sqlalchemy import CheckConstraint, ForeignKey, LargeBinary, String, UniqueConstraint, select, delete
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column

from ...auth import token_digest
from ...common import audit
from ...models import AuthSession, User, iso
from .models import Context, Challenge, Operation


class AttendanceBase(DeclarativeBase):
    pass


class WorkSession(AttendanceBase):
    __tablename__ = "identity_work_sessions"
    __table_args__ = (UniqueConstraint("user_id", "work_date", name="uq_identity_user_day"),
        CheckConstraint("ended_at IS NULL OR ended_at >= started_at", name="ck_identity_work_time"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"})
    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    work_date: Mapped[str] = mapped_column(String(10))
    started_at: Mapped[datetime]
    noon_at: Mapped[datetime | None]
    ended_at: Mapped[datetime | None]


class WorkflowReceipt(AttendanceBase):
    __tablename__ = "identity_workflow_receipts"
    __table_args__ = (UniqueConstraint("context_id", "client_request_id", name="uq_identity_finalize_request"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"})
    challenge_id: Mapped[str] = mapped_column(ForeignKey(Challenge.id), primary_key=True)
    context_id: Mapped[str] = mapped_column(ForeignKey(Context.id))
    client_request_id: Mapped[str] = mapped_column(String(36))
    work_session_id: Mapped[str] = mapped_column(ForeignKey(WorkSession.id))
    key_id: Mapped[str] = mapped_column(String(64))
    ciphertext: Mapped[bytes] = mapped_column(LargeBinary(4096))
    expires_at: Mapped[datetime]


class AttendanceWorkflow:
    """Receipt key must be independent, private and retained through replay expiry.

    No file loader, automatic key generation, ACK or existing-DB installation.
    All calls use the current common guard transaction; never commit here.
    """
    def __init__(self, receipt_key: bytes, key_id: str):
        import re
        if type(receipt_key) is not bytes or len(receipt_key) != 32 or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", key_id) is None:
            raise ValueError("Independent private workflow key/id required")
        self._key, self._cipher, self._key_id = receipt_key, AESGCM(receipt_key), key_id

    def validate_keys(self, excluded):
        if self._key in excluded:
            raise ValueError("Workflow encryption key must be independent")

    def schema_tables(self):
        return tuple(AttendanceBase.metadata.tables.values())

    def _work(self, db, user, *, at=None):
        from .routes import now, fail
        day = ((at if at is not None else now()) + timedelta(hours=9)).date().isoformat()
        rows = list(db.scalars(select(WorkSession).where(WorkSession.user_id == user.id)
            .order_by(WorkSession.work_date).with_for_update().execution_options(populate_existing=True)))
        if any(row.ended_at is None and row.work_date != day for row in rows):
            fail(409, "ATTENDANCE_DAY_REVIEW_REQUIRED")
        return day, next((row for row in rows if row.work_date == day), None)

    def can_resume(self, db, user):
        """Application access after today's attendance, including closed work.

        This is not permission to resume work; current/read_workday still reject
        a closed session and login never changes attendance.
        """
        from .routes import now
        day = (now() + timedelta(hours=9)).date().isoformat()
        row = db.scalar(select(WorkSession).where(WorkSession.user_id == user.id,
            WorkSession.work_date == day).execution_options(populate_existing=True))
        return row is not None

    def resume(self, db, user):
        from .routes import fail
        _, row = self._work(db, user)
        if row is None:
            fail(409, "IDENTITY_FINGERPRINT_REQUIRED")
        return row

    def read_workday(self, db, user):
        """Current locked work in caller's authenticated TX; no context writes."""
        from .routes import now, fail
        stamp = now()
        day, row = self._work(db, user, at=stamp)
        if row is None:
            fail(409, "ATTENDANCE_NOT_STARTED")
        if row.ended_at is not None:
            fail(409, "ATTENDANCE_DAY_CLOSED")
        return {"server_time": iso(stamp), "work_session_ref": row.id,
                "work_date": day, "state": "open", "started_at": iso(row.started_at),
                "noon_at": iso(row.noon_at) if row.noon_at is not None else None,
                "noon_due": row.noon_at is None and (stamp + timedelta(hours=9)).hour >= 12}

    def current(self, db, user, purpose):
        from .routes import now, fail
        day, row = self._work(db, user)
        if row is not None and row.ended_at is not None:
            fail(409, "ATTENDANCE_DAY_CLOSED")
        if purpose == "clock_in":
            if row is not None:
                fail(409, "ATTENDANCE_ALREADY_STARTED")
            return {"work_date": day, "work_session_ref": None}
        if row is None:
            fail(409, "ATTENDANCE_NOT_STARTED")
        if purpose == "noon_check":
            if (now() + timedelta(hours=9)).hour < 12:
                fail(409, "ATTENDANCE_NOON_NOT_DUE")
            if row.noon_at is not None:
                fail(409, "ATTENDANCE_NOON_ALREADY_RECORDED")
        return {"work_date": day, "work_session_ref": row.id}

    def finalize(self, db, ctx, user, item, client_request_id):
        from .routes import locked, now, fail
        receipt = locked(db, WorkflowReceipt, WorkflowReceipt.challenge_id == item.id)
        aad = (ctx.id + ":" + item.id).encode("ascii")
        if receipt:
            if receipt.client_request_id != client_request_id:
                fail(409, "IDENTITY_REQUEST_CONFLICT")
            if receipt.expires_at <= now():
                fail(410, "IDENTITY_LOOKUP_EXPIRED")
            if receipt.key_id != self._key_id:
                fail(503, "IDENTITY_WORKFLOW_NOT_READY")
            try:
                return json.loads(self._cipher.decrypt(receipt.ciphertext[:12], receipt.ciphertext[12:], aad))
            except Exception:
                fail(503, "IDENTITY_WORKFLOW_NOT_READY")
        if locked(db, Operation, Operation.context_id == ctx.id, Operation.client_request_id == client_request_id):
            fail(409, "IDENTITY_REQUEST_CONFLICT")
        if locked(db, WorkflowReceipt, WorkflowReceipt.context_id == ctx.id, WorkflowReceipt.client_request_id == client_request_id):
            fail(409, "IDENTITY_REQUEST_CONFLICT")
        if item.state != "verified" or item.workflow_completed:
            fail(409, "IDENTITY_TERMINAL")
        if item.expires_at <= now():
            fail(410, "IDENTITY_EXPIRED")
        state = self.current(db, user, item.purpose)
        day, work = self._work(db, user)
        t = now(); retain = t + timedelta(days=1)
        answer = {"state": "completed", "workflow_receipt_id": str(uuid4()),
                  "work_date": day, "work_session_ref": state["work_session_ref"]}
        if item.purpose == "clock_in":
            work = WorkSession(id=str(uuid4()), user_id=user.id, work_date=day, started_at=t)
            db.add(work); db.flush()
            # No pre-identity work token is silently revived by first clock-in.
            db.execute(delete(AuthSession).where(AuthSession.user_id == user.id))
            token = secrets.token_urlsafe(32)
            expiry = t + timedelta(hours=current_app.config["SESSION_HOURS"])
            db.add(AuthSession(token_hash=token_digest(token), user_id=user.id, channel="api", expires_at=expiry))
            answer.update(work_session_ref=work.id, access_token=token, token_type="Bearer", expires_at=iso(expiry))
            ctx.receipt_only = True
        elif item.purpose == "noon_check":
            work.noon_at = t
        elif item.purpose == "clock_out":
            work.ended_at = t
            db.execute(delete(AuthSession).where(AuthSession.user_id == user.id))
            for other in db.scalars(select(Context).where(Context.user_id == user.id, Context.id != ctx.id).with_for_update()):
                other.revoked = True
            ctx.receipt_only = True
        if ctx.receipt_only:
            ctx.completed_challenge_id = item.id
            ctx.expires_at = retain
        item.workflow_completed = True
        encoded = json.dumps(answer, separators=(",", ":")).encode()
        nonce = os.urandom(12)
        db.add(WorkflowReceipt(challenge_id=item.id, context_id=ctx.id, client_request_id=client_request_id,
            work_session_id=work.id, key_id=self._key_id, ciphertext=nonce + self._cipher.encrypt(nonce, encoded, aad),
            expires_at=retain))
        db.add(Operation(context_id=ctx.id, client_request_id=client_request_id, operation="finalize",
            target_id=item.id, response_json=json.dumps({"workflow_receipt_id": answer["workflow_receipt_id"]}), expires_at=retain))
        audit("attendance." + item.purpose, "identity_work_session", work.id, actor_id=user.id)
        return answer
