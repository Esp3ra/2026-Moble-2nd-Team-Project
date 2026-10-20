"""Opt-in MySQL physical-write evidence. No sensor I/O, retries or claim release."""
import hashlib
import hmac
from pathlib import Path

import click
from flask import Blueprint, g
from sqlalchemy import BigInteger, CheckConstraint, DateTime, ForeignKeyConstraint, Integer, LargeBinary, String, inspect, select
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column

from ...common import audit
from ...db import check_schema, get_db, transaction
from ...models import AdminGuard, User, iso
from ... import enrollment_protocol_draft as wire
from .models import Job, Claim, Device
from . import service as svc


class DurabilityBase(DeclarativeBase):
    pass


class PhysicalReceipt(DurabilityBase):
    __tablename__ = "enrollment_physical_receipts"
    __table_args__ = (
        ForeignKeyConstraint(["enrollment_id", "device_id", "reserved_slot"],
            [Job.__table__.c.enrollment_id, Job.__table__.c.device_id, Job.__table__.c.reserved_slot],
            name="fk_enrollment_physical_binding"),
        CheckConstraint("operation_revision > 0 AND claim_revision > 0 AND key_epoch > 0 AND reserved_slot >= 0", name="ck_physical_values"),
        CheckConstraint("(intent_probe_hash IS NULL AND intent_at IS NULL) OR (intent_probe_hash IS NOT NULL AND intent_at IS NOT NULL)", name="ck_physical_intent"),
        CheckConstraint("intent_at IS NOT NULL OR result_hash IS NOT NULL", name="ck_physical_evidence"),
        CheckConstraint("(result_envelope IS NULL AND result_hash IS NULL AND result_id IS NULL AND outcome IS NULL AND failure_code IS NULL AND reported_at IS NULL) OR "
            "(result_envelope IS NOT NULL AND result_hash IS NOT NULL AND result_id IS NOT NULL AND reported_at IS NOT NULL AND outcome IS NOT NULL AND "
            "BINARY outcome IN ('sensor_stored','failed','recovery_required') AND OCTET_LENGTH(result_envelope) BETWEEN 1 AND 16384 AND "
            "((BINARY outcome = 'sensor_stored' AND failure_code IS NULL) OR (BINARY outcome <> 'sensor_stored' AND failure_code IS NOT NULL)))", name="ck_physical_result"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    enrollment_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    device_id: Mapped[str] = mapped_column(String(36))
    reserved_slot: Mapped[int] = mapped_column(Integer)
    operation_revision: Mapped[int] = mapped_column(BigInteger)
    claim_revision: Mapped[int] = mapped_column(BigInteger)
    key_epoch: Mapped[int] = mapped_column(BigInteger)
    ticket_digest: Mapped[str] = mapped_column(String(64))
    intent_probe_hash: Mapped[str | None] = mapped_column(String(64))
    intent_at: Mapped[object | None] = mapped_column(DateTime)
    result_envelope: Mapped[bytes | None] = mapped_column(LargeBinary)
    result_hash: Mapped[str | None] = mapped_column(String(64))
    result_id: Mapped[str | None] = mapped_column(String(36), unique=True)
    outcome: Mapped[str | None] = mapped_column(String(32))
    failure_code: Mapped[str | None] = mapped_column(String(32))
    reported_at: Mapped[object | None] = mapped_column(DateTime)


def require_storage(db):
    inspector = inspect(db.connection())
    name = PhysicalReceipt.__tablename__
    if db.bind.dialect.name != "mysql" or not inspector.has_table(name):
        svc.fail(503, "ENROLLMENT_DURABILITY_SCHEMA_REQUIRED")
    columns = {c["name"]: c for c in inspector.get_columns(name)}
    fks = inspector.get_foreign_keys(name)
    expected = ["enrollment_id", "device_id", "reserved_slot"]
    if (set(columns) != set(PhysicalReceipt.__table__.columns.keys()) or
            inspector.get_pk_constraint(name).get("constrained_columns") != ["enrollment_id"] or
            not any(f["constrained_columns"] == expected and f["referred_table"] == "enrollment_jobs"
                and f["referred_columns"] == expected and f.get("referred_schema") in (None, inspector.default_schema_name)
                and str(f.get("options", {}).get("ondelete", "")).upper() != "CASCADE" for f in fks) or
            any(getattr(columns[n]["type"], "unsigned", False) or type(columns[n]["type"]).__name__ != "BIGINT"
                for n in ("operation_revision", "claim_revision", "key_epoch")) or
            type(columns["result_envelope"]["type"]).__name__ != "BLOB"):
        svc.fail(503, "ENROLLMENT_DURABILITY_SCHEMA_REQUIRED")


def row(db, job):
    require_storage(db)
    value = db.scalar(select(PhysicalReceipt).where(PhysicalReceipt.enrollment_id == job.enrollment_id)
                      .with_for_update().execution_options(populate_existing=True))
    if value and (value.device_id != job.device_id or value.reserved_slot != job.reserved_slot
            or value.operation_revision != job.operation_revision or value.claim_revision != job.claim_revision
            or value.key_epoch != job.key_epoch or value.ticket_digest != job.ticket_digest):
        svc.fail(409, "ENROLLMENT_BINDING_MISMATCH")
    if value and value.result_hash:
        if not hmac.compare_digest(hashlib.sha256(value.result_envelope).hexdigest(), value.result_hash):
            svc.fail(409, "ENROLLMENT_RESULT_CONFLICT")
        device = db.get(Device, job.device_id)
        keys = svc.keys(device)
        result = wire.verify_completion(svc.ticket(job, device), value.result_envelope,
            device_id=device.device_id, key_id=keys["key_id"], key_epoch=str(device.key_epoch),
            request_key=keys["request_key"], result_key=keys["result_key"])
        if (result["result_id"], result["outcome"], result["failure_code"]) != (value.result_id, value.outcome, value.failure_code):
            svc.fail(409, "ENROLLMENT_RESULT_CONFLICT")
    return value


def new_row(job):
    return PhysicalReceipt(enrollment_id=job.enrollment_id, device_id=job.device_id,
        reserved_slot=job.reserved_slot, operation_revision=job.operation_revision,
        claim_revision=job.claim_revision, key_epoch=job.key_epoch, ticket_digest=job.ticket_digest)


def view(job, value):
    return svc.binding(job) | dict(contract="enrollment-mysql-v1",
        phase="not_recorded" if value is None else "result_recorded" if value.result_hash else "store_intent",
        result_envelope=wire.b64encode(value.result_envelope) if value and value.result_envelope else None,
        result_id=value.result_id if value else None, outcome=value.outcome if value else None,
        failure_code=value.failure_code if value else None,
        intent_at=iso(value.intent_at) if value and value.intent_at else None,
        reported_at=iso(value.reported_at) if value and value.reported_at else None,
        may_store=False, approval_envelope=None)


def storage_intent(db, job, device, body):
    svc.strict(body, wire.COMMON - {"enrollment_id"} | {"probe_nonce", "ticket_digest", "scope"})
    if body["scope"] != "sensor_store":
        svc.fail(400, "INVALID_INPUT")
    for name in ("operation_revision", "claim_revision", "key_epoch"):
        wire._revision(body[name])
    wire._integer(body["reserved_slot"], 0)
    nonce = wire.b64decode(body["probe_nonce"], 64)
    if not 16 <= len(nonce) <= 64:
        svc.fail(400, "INVALID_INPUT")
    if any(body[f] != svc.binding(job)[f] for f in wire.COMMON - {"enrollment_id"}) or body["ticket_digest"] != job.ticket_digest:
        svc.fail(409, "ENROLLMENT_BINDING_MISMATCH")
    previous = row(db, job)
    digest = hashlib.sha256(nonce).hexdigest()
    if previous:
        if previous.intent_probe_hash != digest:
            svc.fail(409, "ENROLLMENT_PHYSICAL_RECOVERY_REQUIRED")
        return view(job, previous)  # Never issue another store permit, including same-probe retry.
    permit = svc.current_permit(job, device, body, allowed_scopes=("sensor_store",))
    value = new_row(job)
    value.intent_probe_hash, value.intent_at = digest, svc.now()
    db.add(value)
    audit("enrollment.store_intent", "enrollment", job.enrollment_id)
    db.flush()
    return view(job, value) | dict(may_store=True, approval_envelope=permit["approval_envelope"])


def record_result(db, job, device, body, *, legacy_evidence=False, actor_id=None):
    svc.strict(body, ("operation_revision", "result_envelope"))
    wire._revision(body["operation_revision"])
    if body["operation_revision"] != str(job.operation_revision):
        svc.fail(409, "ENROLLMENT_REVISION_CONFLICT")
    raw = wire.b64decode(body["result_envelope"], 16384)
    keys = svc.keys(device)
    result = wire.verify_completion(svc.ticket(job, device), raw, device_id=device.device_id, key_id=keys["key_id"],
        key_epoch=str(device.key_epoch), request_key=keys["request_key"], result_key=keys["result_key"])
    value = row(db, job)
    digest = hashlib.sha256(raw).hexdigest()
    if value and value.result_hash:
        if not hmac.compare_digest(value.result_hash, digest):
            svc.fail(409, "ENROLLMENT_RESULT_CONFLICT")
        return view(job, value)
    if result["outcome"] == "sensor_stored" and (value is None or value.intent_at is None) and not legacy_evidence:
        svc.fail(409, "ENROLLMENT_STORAGE_INTENT_REQUIRED")
    if value is None:
        value = new_row(job)
        db.add(value)
    value.result_envelope, value.result_hash = raw, digest
    value.result_id, value.outcome, value.failure_code = result["result_id"], result["outcome"], result["failure_code"]
    value.reported_at = svc.now()
    audit("enrollment.physical_result_recorded", "enrollment", job.enrollment_id, actor_id=actor_id)
    db.flush()
    return view(job, value)  # Evidence only. Never changes job/claim or activates a mapping.


def require_recorded_result(db, job, digest):
    value = row(db, job)
    if value is None or not value.result_hash:
        svc.fail(409, "ENROLLMENT_PHYSICAL_RESULT_REQUIRED")
    if not hmac.compare_digest(value.result_hash, digest):
        svc.fail(409, "ENROLLMENT_RESULT_CONFLICT")
    if value.outcome == "sensor_stored" and value.intent_at is None:
        svc.fail(409, "ENROLLMENT_STORAGE_INTENT_REQUIRED")


def install(app):
    from .routes import guarded, body
    bp = Blueprint("enrollment_mysql", __name__, url_prefix="/api/v1/device-enrollments")

    @bp.post("/<enrollment_id>/storage-intent")
    @guarded
    def intent(db, enrollment_id):
        job, device, _ = svc.load(db, enrollment_id)
        return storage_intent(db, job, device, body())

    @bp.post("/<enrollment_id>/physical-result")
    @guarded
    def report(db, enrollment_id):
        job, device, _ = svc.load(db, enrollment_id)
        return record_result(db, job, device, body())

    @bp.get("/<enrollment_id>/physical-result")
    @guarded
    def read(db, enrollment_id):
        job, _, _ = svc.load(db, enrollment_id)
        return view(job, row(db, job))

    app.register_blueprint(bp)

    @app.cli.command("init-enrollment-durability-draft")
    def initialize():
        check_schema()
        if not inspect(app.extensions["db_engine"]).has_table("enrollment_jobs"):
            raise click.ClickException("007 등록 테이블을 먼저 확인해 주세요.")
        DurabilityBase.metadata.create_all(app.extensions["db_engine"])
        click.echo("등록 MySQL 증거 테이블 준비. 기존 작업·예약·센서 변경 없음.")

    @app.cli.command("archive-enrollment-result")
    @click.option("--enrollment-id", required=True)
    @click.option("--login-id", required=True)
    @click.option("--result-file", type=click.Path(exists=True, dir_okay=False), required=True)
    def archive(enrollment_id, login_id, result_file):
        """Operator imports signed raw evidence; no session takeover, mapping or claim release."""
        try:
            raw = Path(result_file).read_bytes()
            if len(raw) > 16384:
                raise ValueError()
            wire.canonical_uuid(enrollment_id)
            with transaction() as db:
                db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
                operator = db.scalar(select(User).where(User.login_id == login_id).with_for_update())
                if operator is None or not operator.is_active or operator.role != "admin":
                    raise ValueError()
                g.user = operator
                job = db.scalar(select(Job).where(Job.enrollment_id == enrollment_id).with_for_update())
                if job is None:
                    raise ValueError()
                device = svc.device_access(db, job.device_id, job.target_user_id)
                claim = db.get(Claim, (job.device_id, job.reserved_slot))
                if (claim is None or claim.enrollment_id != job.enrollment_id or claim.revision != job.claim_revision
                        or job.key_epoch != device.key_epoch):
                    raise ValueError()
                svc.retention(job)
                record_result(db, job, device, {"operation_revision": str(job.operation_revision),
                    "result_envelope": wire.b64encode(raw)}, legacy_evidence=True, actor_id=operator.id)
            click.echo("서명 결과 원문 보관 완료. 접속 인수·매핑·예약 해제 없음.")
        except Exception:
            raise click.ClickException("권한·작업·장치 키·원문 서명과 보관 기간을 확인해 주세요.") from None
