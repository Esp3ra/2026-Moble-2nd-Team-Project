"""Separate metadata: ordinary init-db never creates identity storage."""
from datetime import datetime
from sqlalchemy import Boolean, CheckConstraint, ForeignKey, LargeBinary, String, Text, UniqueConstraint
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column
from ...models import User
from ..enrollment.models import Device, Mapping


class IdentityBase(DeclarativeBase):
    pass


class Context(IdentityBase):
    __tablename__ = "identity_contexts"
    __table_args__ = (UniqueConstraint("user_id", "login_request_id", name="uq_identity_login_request"),
                     CheckConstraint("expires_at > created_at AND lookup_until > created_at", name="ck_identity_context_time"),
                     CheckConstraint("(token_hash IS NOT NULL AND auth_hash IS NULL AND login_request_id IS NOT NULL) OR (token_hash IS NULL AND auth_hash IS NOT NULL AND login_request_id IS NULL)", name="ck_identity_context_kind"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"})
    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    login_request_id: Mapped[str | None] = mapped_column(String(36))
    token_hash: Mapped[str | None] = mapped_column(String(64), unique=True)
    auth_hash: Mapped[str | None] = mapped_column(String(64), unique=True)
    context_ref: Mapped[str] = mapped_column(String(86))
    password_stamp: Mapped[str] = mapped_column(String(64))
    revoked: Mapped[bool] = mapped_column(Boolean, default=False)
    receipt_only: Mapped[bool] = mapped_column(Boolean, default=False)
    completed_challenge_id: Mapped[str | None] = mapped_column(String(36))
    created_at: Mapped[datetime]
    expires_at: Mapped[datetime]
    lookup_until: Mapped[datetime]


class Challenge(IdentityBase):
    __tablename__ = "identity_challenges"
    __table_args__ = (UniqueConstraint("context_id", "client_request_id", name="uq_identity_issue_request"),
                     CheckConstraint("BINARY state IN ('issued','verified','rejected','cancelled','expired','revoked')", name="ck_identity_state"),
                     CheckConstraint("BINARY purpose IN ('clock_in','noon_check','clock_out')", name="ck_identity_purpose"),
                     CheckConstraint("expires_at > created_at AND lookup_until > created_at", name="ck_identity_time"),
                     CheckConstraint("(result_bytes IS NULL AND result_hash IS NULL AND result_id IS NULL) OR (result_bytes IS NOT NULL AND result_hash IS NOT NULL AND result_id IS NOT NULL)", name="ck_identity_result"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"})
    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    context_id: Mapped[str] = mapped_column(ForeignKey(Context.id))
    client_request_id: Mapped[str] = mapped_column(String(36))
    device_id: Mapped[str] = mapped_column(String(36, collation="utf8mb4_unicode_ci"), ForeignKey(Device.device_id), index=True)
    mapping_id: Mapped[str] = mapped_column(String(36, collation="utf8mb4_unicode_ci"), ForeignKey(Mapping.mapping_id))
    purpose: Mapped[str] = mapped_column(String(16))
    request_bytes: Mapped[bytes] = mapped_column(LargeBinary(16384))
    state: Mapped[str] = mapped_column(String(16), default="issued")
    created_at: Mapped[datetime]
    expires_at: Mapped[datetime]
    lookup_until: Mapped[datetime]
    capture_nonce: Mapped[str | None] = mapped_column(String(86))
    result_id: Mapped[str | None] = mapped_column(String(36), unique=True)
    result_bytes: Mapped[bytes | None] = mapped_column(LargeBinary(16384))
    result_hash: Mapped[str | None] = mapped_column(String(64))
    receipt_json: Mapped[str | None] = mapped_column(Text)
    workflow_completed: Mapped[bool] = mapped_column(Boolean, default=False)


class Approval(IdentityBase):
    __tablename__ = "identity_approvals"
    __table_args__ = (UniqueConstraint("challenge_id", "probe_nonce", name="uq_identity_probe"),
                     CheckConstraint("BINARY scope IN ('capture','result')", name="ck_identity_approval_scope"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"})
    digest: Mapped[str] = mapped_column(String(64), primary_key=True)
    challenge_id: Mapped[str] = mapped_column(ForeignKey(Challenge.id))
    probe_nonce: Mapped[str] = mapped_column(String(86))
    probe_bytes: Mapped[bytes] = mapped_column(LargeBinary(16384))
    approval_bytes: Mapped[bytes] = mapped_column(LargeBinary(16384))
    scope: Mapped[str] = mapped_column(String(16))
    expires_at: Mapped[datetime]


class Operation(IdentityBase):
    __tablename__ = "identity_operations"
    __table_args__ = ({"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"},)
    context_id: Mapped[str] = mapped_column(ForeignKey(Context.id), primary_key=True)
    client_request_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    operation: Mapped[str] = mapped_column(String(16))
    target_id: Mapped[str] = mapped_column(String(36))
    response_json: Mapped[str] = mapped_column(Text)
    expires_at: Mapped[datetime]
