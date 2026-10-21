"""Explicit operator-approved recovery of unsigned, pre-store failures only.

No sensor I/O, record deletion, session takeover or automatic slot reclamation.
"""
from datetime import datetime, timedelta
import hashlib
import hmac
import json
from pathlib import Path
import re
from uuid import uuid4

import click
from flask import Blueprint, current_app, g, request
from sqlalchemy import (BigInteger, CheckConstraint, DateTime, ForeignKeyConstraint,
                        Integer, String, inspect, select)
from sqlalchemy.dialects.mysql import DATETIME
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column

from ...common import AppError, audit
from ...db import check_schema, get_db, transaction
from ...models import AdminGuard, ResourceGrant, User, iso
from ... import enrollment_protocol_draft as wire
from . import service as svc
from .models import Device, Job, Claim, Mapping
from .durability import row as physical_row


class RecoveryBase(DeclarativeBase):
    pass


MICRO_TIME = DateTime().with_variant(DATETIME(fsp=6), "mysql")


class Recovery(RecoveryBase):
    __tablename__ = "enrollment_recoveries"
    __table_args__ = (
        ForeignKeyConstraint(["source_enrollment_id", "device_id", "slot"],
            [Job.__table__.c.enrollment_id, Job.__table__.c.device_id, Job.__table__.c.reserved_slot], name="fk_recovery_source"),
        ForeignKeyConstraint(["new_enrollment_id", "device_id", "slot"],
            [Job.__table__.c.enrollment_id, Job.__table__.c.device_id, Job.__table__.c.reserved_slot], name="fk_recovery_new"),
        ForeignKeyConstraint(["operator_id"], [User.__table__.c.id], name="fk_recovery_operator"),
        CheckConstraint("source_operation_revision > 0 AND source_claim_revision > 0 AND key_epoch > 0 AND slot >= 0", name="ck_recovery_values"),
        CheckConstraint("fenced_at < observed_at AND observed_at <= checked_at AND checked_at <= closed_at", name="ck_recovery_times"),
        CheckConstraint("BINARY source_state IN ('issued','failed','cancelled','recovery_required')", name="ck_recovery_source_state"),
        CheckConstraint("(new_enrollment_id IS NULL AND new_client_request_id IS NULL AND reissue_session_hash IS NULL AND reissued_at IS NULL) OR "
            "(new_enrollment_id IS NOT NULL AND new_client_request_id IS NOT NULL AND reissue_session_hash IS NOT NULL AND reissued_at IS NOT NULL AND source_enrollment_id <> new_enrollment_id)", name="ck_recovery_reissue"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    recovery_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    source_enrollment_id: Mapped[str] = mapped_column(String(36), unique=True)
    device_id: Mapped[str] = mapped_column(String(36))
    slot: Mapped[int] = mapped_column(Integer)
    source_operation_revision: Mapped[int] = mapped_column(BigInteger)
    source_claim_revision: Mapped[int] = mapped_column(BigInteger)
    key_epoch: Mapped[int] = mapped_column(BigInteger)
    ticket_digest: Mapped[str] = mapped_column(String(64))
    profile_hash: Mapped[str] = mapped_column(String(64))
    source_state: Mapped[str] = mapped_column(String(32))
    operator_id: Mapped[int] = mapped_column(Integer)
    report_sha256: Mapped[str] = mapped_column(String(64))
    source_report_sha256: Mapped[str] = mapped_column(String(64))
    attestation_sha256: Mapped[str] = mapped_column(String(64))
    fenced_at: Mapped[datetime] = mapped_column(MICRO_TIME)
    observed_at: Mapped[datetime] = mapped_column(MICRO_TIME)
    checked_at: Mapped[datetime] = mapped_column(MICRO_TIME)
    closed_at: Mapped[datetime] = mapped_column(MICRO_TIME)
    new_enrollment_id: Mapped[str | None] = mapped_column(String(36), unique=True)
    new_client_request_id: Mapped[str | None] = mapped_column(String(36))
    reissue_session_hash: Mapped[str | None] = mapped_column(String(64))
    reissued_at: Mapped[datetime | None] = mapped_column(MICRO_TIME)


def require_schema(db):
    inspector = inspect(db.connection())
    name = Recovery.__tablename__
    if db.bind.dialect.name != "mysql" or not inspector.has_table(name):
        svc.fail(503, "ENROLLMENT_RECOVERY_SCHEMA_REQUIRED")
    columns = {c["name"]: c for c in inspector.get_columns(name)}
    fks = inspector.get_foreign_keys(name)
    unique = {tuple(c["column_names"]) for c in inspector.get_unique_constraints(name)}
    options = inspector.get_table_options(name)
    bindings = (["source_enrollment_id", "device_id", "slot"], ["new_enrollment_id", "device_id", "slot"])
    if (set(columns) != set(Recovery.__table__.columns.keys()) or
            inspector.get_pk_constraint(name).get("constrained_columns") != ["recovery_id"] or
            any(not any(f["constrained_columns"] == fields and f["referred_table"] == "enrollment_jobs"
                and f["referred_columns"] == ["enrollment_id", "device_id", "reserved_slot"]
                and f.get("referred_schema") in (None, inspector.default_schema_name)
                and not any(str(f.get("options", {}).get(k, "")).upper() == "CASCADE" for k in ("ondelete", "onupdate"))
                for f in fks) for fields in bindings) or
            not any(f["constrained_columns"] == ["operator_id"] and f["referred_table"] == "users"
                and f["referred_columns"] == ["id"] and f.get("referred_schema") in (None, inspector.default_schema_name)
                and not any(str(f.get("options", {}).get(k, "")).upper() == "CASCADE" for k in ("ondelete", "onupdate")) for f in fks) or
            not {("source_enrollment_id",), ("new_enrollment_id",)} <= unique or
            options.get("mysql_engine", "").lower() != "innodb" or options.get("mysql_collate") != "utf8mb4_unicode_ci" or
            any(columns[c.name]["nullable"] != c.nullable for c in Recovery.__table__.columns) or
            any(getattr(columns[c.name]["type"], "length", None) != c.type.length
                for c in Recovery.__table__.columns if isinstance(c.type, String)) or
            any(type(columns[n]["type"]).__name__ != "INTEGER" or getattr(columns[n]["type"], "unsigned", False)
                for n in ("operator_id", "slot")) or
            any(type(columns[n]["type"]).__name__ != "BIGINT" or getattr(columns[n]["type"], "unsigned", False)
                for n in ("source_operation_revision", "source_claim_revision", "key_epoch")) or
            any(getattr(columns[n]["type"], "fsp", None) != 6 for n in ("fenced_at", "observed_at", "checked_at", "closed_at", "reissued_at"))):
        svc.fail(503, "ENROLLMENT_RECOVERY_SCHEMA_REQUIRED")


def access_device(db, device_id):
    device = db.scalar(select(Device).where(Device.device_id == svc.uuid(device_id))
                       .with_for_update().execution_options(populate_existing=True))
    if device is None:
        svc.fail(404, "ENROLLMENT_DEVICE_NOT_FOUND")
    grant = db.scalar(select(ResourceGrant.id).where(ResourceGrant.user_id == g.user.id,
        ResourceGrant.resource_type == "enrollment_device", ResourceGrant.resource_id == device_id).with_for_update())
    if grant is None:
        svc.fail(403, "ENROLLMENT_DEVICE_FORBIDDEN")
    if not device.enabled:
        svc.fail(409, "ENROLLMENT_UNAVAILABLE")
    svc.keys(device); svc.candidates(device)
    return device


def profile(device):
    return hashlib.sha256(json.dumps([device.key_id, device.key_epoch, device.minimum_slot,
        device.maximum_slot, device.capacity, svc.candidates(device)], separators=(",", ":")).encode()).hexdigest()


def utc(value):
    if type(value) is not str or re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,6})?(?:Z|\+00:00)", value) is None:
        raise ValueError("UTC timestamp required")
    return datetime.fromisoformat(value.replace("Z", "+00:00")).replace(tzinfo=None)


def evidence(attestation, report):
    if not 1 <= len(report) <= 65536:
        raise ValueError("Private report size")
    if type(wire.parse_json(report, 65536)) is not dict:
        raise ValueError("Private JSON report required")  # Data, never executable instructions.
    body = wire.parse_json(attestation, 16384)
    svc.strict(body, ("enrollment_id", "device_id", "slot", "operation_revision", "claim_revision", "key_epoch",
        "report_sha256", "source_report_sha256", "fenced_at", "observed_at", "checked_at", "writer_fenced", "slot_empty",
        "signed_result_present", "target_binding_verified"))
    for field in ("enrollment_id", "device_id"):
        svc.uuid(body[field])
    for field in ("operation_revision", "claim_revision", "key_epoch"):
        wire._revision(body[field])
    wire._integer(body["slot"], 0)
    if (body["writer_fenced"] is not True or body["slot_empty"] is not True
            or body["signed_result_present"] is not False or body["target_binding_verified"] is not False
            or body["report_sha256"] != hashlib.sha256(report).hexdigest()
            or type(body["source_report_sha256"]) is not str
            or re.fullmatch(r"[0-9a-f]{64}", body["source_report_sha256"]) is None):
        raise ValueError("Operator evidence mismatch")
    times = tuple(utc(body[field]) for field in ("fenced_at", "observed_at", "checked_at"))
    if not times[0] < times[1] <= times[2] <= svc.now():
        raise ValueError("Operator evidence time order")
    digest = hashlib.sha256(json.dumps(body, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return body, times, digest


def summary(value):
    return dict(recovery_id=value.recovery_id, source_enrollment_id=value.source_enrollment_id,
        device_id=value.device_id, reserved_slot=value.slot,
        source_operation_revision=str(value.source_operation_revision), source_claim_revision=str(value.source_claim_revision),
        state="reissued" if value.new_enrollment_id else "closed_awaiting_explicit_reissue",
        new_enrollment_id=value.new_enrollment_id, evidence_kind="operator_observation_not_signed_result",
        closed_at=iso(value.closed_at), reissued_at=iso(value.reissued_at) if value.reissued_at else None)


def check_binding(value, job, device):
    if (job.state != "recovery_required" or job.result_hash is not None or job.result_json is not None
            or job.updated_at != value.closed_at or
            value.device_id != job.device_id or value.slot != job.reserved_slot or
            value.source_operation_revision != job.operation_revision or value.source_claim_revision != job.claim_revision or
            value.key_epoch != job.key_epoch or value.key_epoch != device.key_epoch or
            value.ticket_digest != job.ticket_digest or value.profile_hash != profile(device) or
            job.reserved_slot not in svc.candidates(device) or
            not hmac.compare_digest(wire.ticket_digest(svc.ticket(job, device)), job.ticket_digest)):
        svc.fail(409, "ENROLLMENT_RECOVERY_BINDING_CHANGED")


def close(db, login_id, attestation, report, *, apply=False):
    require_schema(db)
    operator = db.scalar(select(User).where(User.login_id == login_id).with_for_update().execution_options(populate_existing=True))
    if operator is None or not operator.is_active or operator.role != "admin":
        svc.fail(403, "FORBIDDEN")
    g.user = operator
    body, times, digest = evidence(attestation, report)
    job = db.scalar(select(Job).where(Job.enrollment_id == body["enrollment_id"]).with_for_update()
                    .execution_options(populate_existing=True))
    if job is None:
        svc.fail(404, "ENROLLMENT_NOT_FOUND")
    device = access_device(db, body["device_id"])
    expected = (body["device_id"], body["slot"], body["operation_revision"], body["claim_revision"], body["key_epoch"])
    actual = (job.device_id, job.reserved_slot, str(job.operation_revision), str(job.claim_revision), str(job.key_epoch))
    if expected != actual or job.key_epoch != device.key_epoch or job.reserved_slot not in svc.candidates(device):
        svc.fail(409, "ENROLLMENT_RECOVERY_BINDING_CHANGED")
    existing = db.scalar(select(Recovery).where(Recovery.source_enrollment_id == job.enrollment_id).with_for_update())
    if existing:
        check_binding(existing, job, device)
        if existing.operator_id != operator.id or existing.attestation_sha256 != digest:
            svc.fail(409, "ENROLLMENT_RECOVERY_CONFLICT")
        return summary(existing)  # No second close, audit, authorization or new job.
    claim = db.scalar(select(Claim).where(Claim.device_id == device.device_id, Claim.slot == job.reserved_slot)
                      .with_for_update().execution_options(populate_existing=True))
    if (claim is None or claim.enrollment_id != job.enrollment_id or claim.revision != job.claim_revision
            or claim.state not in ("reserved", "quarantined")):
        svc.fail(409, "ENROLLMENT_CLAIM_CHANGED")
    if (job.state == "completed" or job.result_hash is not None or job.result_json is not None
            or physical_row(db, job) is not None
            or db.scalar(select(Mapping.mapping_id).where(Mapping.device_id == device.device_id,
                Mapping.slot == job.reserved_slot).with_for_update()) is not None):
        svc.fail(409, "ENROLLMENT_PHYSICAL_RECOVERY_REQUIRES_REVIEW")
    # Let every old five-second permit expire; evidence must postdate this job's last change.
    if times[1] < max(job.updated_at, job.expires_at + timedelta(seconds=5)):
        svc.fail(409, "ENROLLMENT_RECOVERY_TOO_EARLY")
    if not hmac.compare_digest(wire.ticket_digest(svc.ticket(job, device)), job.ticket_digest):
        svc.fail(409, "ENROLLMENT_RECOVERY_BINDING_CHANGED")
    value = Recovery(recovery_id=str(uuid4()), source_enrollment_id=job.enrollment_id,
        device_id=device.device_id, slot=job.reserved_slot, source_operation_revision=job.operation_revision,
        source_claim_revision=job.claim_revision, key_epoch=job.key_epoch, ticket_digest=job.ticket_digest,
        profile_hash=profile(device), source_state=job.state, operator_id=operator.id,
        report_sha256=body["report_sha256"], source_report_sha256=body["source_report_sha256"], attestation_sha256=digest,
        fenced_at=times[0], observed_at=times[1], checked_at=times[2], closed_at=svc.now())
    if not apply:
        return dict(ready=True, action="preview_only", source_enrollment_id=job.enrollment_id,
                    reserved_slot=job.reserved_slot, evidence_kind="operator_observation_not_signed_result")
    db.add(value)
    job.state, claim.state, job.updated_at = "recovery_required", "quarantined", svc.now()
    # Do not rewrite completed_at/expires_at: the original result window is not extended.
    audit("enrollment.operator_closed", "enrollment", job.enrollment_id,
          {"recovery_id": value.recovery_id, "evidence_kind": "operator_observation", "report_sha256": value.report_sha256,
           "source_report_sha256": value.source_report_sha256})
    db.flush()
    return summary(value)


def load_recovery(db, recovery_id):
    require_schema(db)
    value = db.scalar(select(Recovery).where(Recovery.recovery_id == svc.uuid(recovery_id)).with_for_update()
                      .execution_options(populate_existing=True))
    if value is None:
        svc.fail(404, "ENROLLMENT_RECOVERY_NOT_FOUND")
    device = access_device(db, value.device_id)
    if value.operator_id != g.user.id:
        svc.fail(403, "ENROLLMENT_RECOVERY_OPERATOR_MISMATCH")
    job = db.scalar(select(Job).where(Job.enrollment_id == value.source_enrollment_id).with_for_update()
                    .execution_options(populate_existing=True))
    check_binding(value, job, device)
    if value.new_enrollment_id and value.reissue_session_hash != g.auth_session.token_hash:
        svc.fail(403, "ENROLLMENT_SESSION_MISMATCH")
    return value, job, device


def ancestors(db, source_id, device_id):
    parents = {new: old for old, new in db.execute(select(Recovery.source_enrollment_id, Recovery.new_enrollment_id)
               .where(Recovery.device_id == device_id, Recovery.new_enrollment_id.is_not(None))).all()}
    found = {source_id}
    while source_id in parents:
        source_id = parents[source_id]
        if source_id in found:
            svc.fail(409, "ENROLLMENT_RECOVERY_CONFLICT")
        found.add(source_id)
    return found


def reissue(db, recovery_id, body):
    svc.strict(body, ("client_request_id", "target_user_id"))
    svc.uuid(body["client_request_id"]); wire._integer(body["target_user_id"], 1)
    value, old, device = load_recovery(db, recovery_id)
    svc.active_target(db, body["target_user_id"])
    if value.new_enrollment_id:
        job, device, _ = svc.load(db, value.new_enrollment_id)
        if value.new_client_request_id != body["client_request_id"] or job.target_user_id != body["target_user_id"]:
            svc.fail(409, "ENROLLMENT_REQUEST_CONFLICT")
        return dict(recovery=summary(value), job=svc.view(job, device))
    claim = db.scalar(select(Claim).where(Claim.device_id == device.device_id, Claim.slot == value.slot)
                      .with_for_update().execution_options(populate_existing=True))
    if (old.state != "recovery_required" or claim is None or claim.enrollment_id != old.enrollment_id
            or claim.revision != value.source_claim_revision or claim.state != "quarantined"):
        svc.fail(409, "ENROLLMENT_CLAIM_CHANGED")
    if (db.scalar(select(Mapping.mapping_id).where(Mapping.device_id == device.device_id,
            ((Mapping.slot == value.slot) | (Mapping.user_id == body["target_user_id"]))).with_for_update()) is not None
            or physical_row(db, old) is not None):
        svc.fail(409, "ENROLLMENT_PHYSICAL_RECOVERY_REQUIRES_REVIEW")
    if db.scalar(select(Job.enrollment_id).where(Job.issuer_id == g.user.id,
            Job.issuer_session_hash == g.auth_session.token_hash, Job.client_request_id == body["client_request_id"]).with_for_update()):
        svc.fail(409, "ENROLLMENT_REQUEST_CONFLICT")
    previous_targets = set(db.scalars(select(Job.enrollment_id).where(Job.device_id == device.device_id,
        Job.target_user_id == body["target_user_id"]).with_for_update()))
    if previous_targets - ancestors(db, old.enrollment_id, device.device_id):
        svc.fail(409, "ENROLLMENT_TARGET_REQUIRES_REVIEW")
    if claim.revision >= wire.BIGINT_MAX:
        svc.fail(409, "ENROLLMENT_REVISION_EXHAUSTED")
    stamp = svc.now()
    job = Job(enrollment_id=str(uuid4()), client_request_id=body["client_request_id"], issuer_id=g.user.id,
        issuer_session_hash=g.auth_session.token_hash, target_user_id=body["target_user_id"], device_id=device.device_id,
        reserved_slot=value.slot, operation_revision=1, claim_revision=claim.revision + 1, key_epoch=device.key_epoch,
        ticket_digest="", state="issued", created_at=stamp, updated_at=stamp,
        expires_at=stamp + timedelta(seconds=current_app.config["ENROLLMENT_TICKET_TTL_SECONDS"]))
    job.ticket_digest = wire.ticket_digest(svc.ticket(job, device))
    db.add(job); db.flush()
    claim.enrollment_id, claim.revision, claim.state = job.enrollment_id, job.claim_revision, "reserved"
    value.new_enrollment_id, value.new_client_request_id = job.enrollment_id, body["client_request_id"]
    value.reissue_session_hash, value.reissued_at = g.auth_session.token_hash, stamp
    audit("enrollment.reissued", "enrollment", job.enrollment_id,
          {"recovery_id": value.recovery_id, "source_enrollment_id": old.enrollment_id})
    db.flush()
    return dict(recovery=summary(value), job=svc.view(job, device))


def install(app):
    from .routes import guarded, body
    bp = Blueprint("enrollment_recovery", __name__, url_prefix="/api/v1/device-enrollments/recoveries")

    @bp.get("/<recovery_id>")
    @guarded
    def status(db, recovery_id):
        if request.args:
            svc.fail(400, "INVALID_INPUT")
        value, _, _ = load_recovery(db, recovery_id)
        return {"recovery": summary(value)}

    @bp.post("/<recovery_id>/reissue")
    @guarded
    def issue_again(db, recovery_id):
        if request.args:
            svc.fail(400, "INVALID_INPUT")
        return reissue(db, recovery_id, body())

    app.register_blueprint(bp)

    @app.cli.command("init-enrollment-recovery-draft")
    def initialize():
        from .durability import require_storage
        check_schema(); require_storage(get_db())
        RecoveryBase.metadata.create_all(app.extensions["db_engine"])
        click.echo("복구 이력 테이블 준비. 기존 작업·예약·센서 변경 없음.")

    @app.cli.command("close-enrollment-for-recovery")
    @click.option("--login-id", required=True)
    @click.option("--attestation-file", type=click.Path(exists=True, dir_okay=False), required=True)
    @click.option("--report-file", type=click.Path(exists=True, dir_okay=False), required=True)
    @click.option("--apply", is_flag=True, help="Explicit operator close; default is read-only preview")
    def operator_close(login_id, attestation_file, report_file, apply):
        try:
            # Bound even local operator input before loading it into memory.
            with Path(attestation_file).open("rb") as handle:
                attestation = handle.read(16385)
            with Path(report_file).open("rb") as handle:
                report = handle.read(65537)
            with transaction() as db:
                if db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update()) is None:
                    svc.fail(503, "DATABASE_UNAVAILABLE")
                check_schema()
                result = close(db, login_id, attestation, report, apply=apply)
            click.echo(json.dumps(result, ensure_ascii=False))
        except AppError as error:
            raise click.ClickException("복구 거절: " + error.code) from None
        except Exception:
            raise click.ClickException("비공개 원본 보고·확인서와 현재 권한/스키마를 확인해 주세요.") from None
