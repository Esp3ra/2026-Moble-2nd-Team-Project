"""Independent metadata: ordinary init-db must not create these draft tables."""
from datetime import datetime
from sqlalchemy import BigInteger, Boolean, ForeignKey, ForeignKeyConstraint, CheckConstraint, Integer, String, Text, UniqueConstraint
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column
from ...models import User


class EnrollmentBase(DeclarativeBase):
    pass


class Device(EnrollmentBase):
    __tablename__ = "enrollment_devices"
    __table_args__ = (CheckConstraint("key_epoch > 0 AND capacity > 0 AND minimum_slot >= 0 AND maximum_slot >= minimum_slot AND maximum_slot < capacity", name="ck_enrollment_profile"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    device_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    display_name: Mapped[str] = mapped_column(String(80))
    enabled: Mapped[bool] = mapped_column(Boolean, default=True)
    key_id: Mapped[str] = mapped_column(String(64))
    key_epoch: Mapped[int] = mapped_column(BigInteger)
    minimum_slot: Mapped[int] = mapped_column(Integer)
    maximum_slot: Mapped[int] = mapped_column(Integer)
    capacity: Mapped[int] = mapped_column(Integer)
    candidate_slots_json: Mapped[str] = mapped_column(Text)


class Job(EnrollmentBase):
    __tablename__ = "enrollment_jobs"
    __table_args__ = (UniqueConstraint("issuer_id", "issuer_session_hash", "client_request_id", name="uq_enrollment_issue"),
                     UniqueConstraint("enrollment_id", "device_id", "reserved_slot", name="uq_enrollment_claim_binding"),
                     UniqueConstraint("enrollment_id", "device_id", "reserved_slot", "target_user_id", name="uq_enrollment_user_binding"),
                     CheckConstraint("operation_revision > 0 AND claim_revision > 0 AND key_epoch > 0 AND reserved_slot >= 0 AND expires_at > created_at", name="ck_enrollment_job_values"),
                     CheckConstraint("BINARY state IN ('issued','completed','failed','recovery_required','cancelled')", name="ck_enrollment_job_state"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    enrollment_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    client_request_id: Mapped[str] = mapped_column(String(36))
    issuer_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    issuer_session_hash: Mapped[str] = mapped_column(String(64))
    target_user_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    device_id: Mapped[str] = mapped_column(ForeignKey(Device.device_id))
    reserved_slot: Mapped[int] = mapped_column(Integer)
    operation_revision: Mapped[int] = mapped_column(BigInteger, default=1)
    claim_revision: Mapped[int] = mapped_column(BigInteger, default=1)
    key_epoch: Mapped[int] = mapped_column(BigInteger)
    ticket_digest: Mapped[str] = mapped_column(String(64))
    state: Mapped[str] = mapped_column(String(32), default="issued")
    created_at: Mapped[datetime]
    expires_at: Mapped[datetime]
    updated_at: Mapped[datetime]
    completed_at: Mapped[datetime | None]
    result_hash: Mapped[str | None] = mapped_column(String(64))
    result_json: Mapped[str | None] = mapped_column(Text)


class Claim(EnrollmentBase):
    __tablename__ = "enrollment_claims"
    __table_args__ = (ForeignKeyConstraint(["enrollment_id", "device_id", "slot"],
                        ["enrollment_jobs.enrollment_id", "enrollment_jobs.device_id", "enrollment_jobs.reserved_slot"], name="fk_enrollment_claim_binding"),
                     CheckConstraint("revision > 0 AND slot >= 0", name="ck_enrollment_claim_values"),
                     CheckConstraint("BINARY state IN ('reserved','active','quarantined')", name="ck_enrollment_claim_state"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    device_id: Mapped[str] = mapped_column(ForeignKey(Device.device_id), primary_key=True)
    slot: Mapped[int] = mapped_column(Integer, primary_key=True)
    enrollment_id: Mapped[str] = mapped_column(String(36), unique=True)
    revision: Mapped[int] = mapped_column(BigInteger)
    state: Mapped[str] = mapped_column(String(24), default="reserved")


class Mapping(EnrollmentBase):
    __tablename__ = "enrollment_mappings"
    __table_args__ = (UniqueConstraint("device_id", "slot", name="uq_enrollment_mapping_slot"),
                     UniqueConstraint("device_id", "user_id", name="uq_enrollment_mapping_user"),
                     ForeignKeyConstraint(["enrollment_id", "device_id", "slot", "user_id"],
                        ["enrollment_jobs.enrollment_id", "enrollment_jobs.device_id", "enrollment_jobs.reserved_slot", "enrollment_jobs.target_user_id"], name="fk_enrollment_mapping_binding"),
                     CheckConstraint("revision > 0 AND slot >= 0 AND active IN (0,1)", name="ck_enrollment_mapping_values"),
                     {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    mapping_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    enrollment_id: Mapped[str] = mapped_column(String(36), unique=True)
    device_id: Mapped[str] = mapped_column(ForeignKey(Device.device_id))
    user_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    slot: Mapped[int] = mapped_column(Integer)
    revision: Mapped[int] = mapped_column(BigInteger, default=1)
    active: Mapped[bool] = mapped_column(Boolean, default=True)
    created_at: Mapped[datetime]
