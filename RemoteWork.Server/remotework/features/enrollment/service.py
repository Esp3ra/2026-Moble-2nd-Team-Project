"""Opt-in enrollment coordinator. No sensor I/O or automatic claim reclamation."""
import hashlib
import hmac
import json
from datetime import timedelta
from uuid import uuid4

from flask import current_app, g
from sqlalchemy import select

from ... import enrollment_protocol_draft as wire
from ... import enrollment_approval_draft as approval
from ...common import AppError, audit
from ...models import ResourceGrant, User, iso, utcnow
from .models import Device, Job, Claim, Mapping


def fail(status, code):
    raise AppError(status, code, "등록 상태·권한·장치 설정을 확인해 주세요.")


def now():
    value = current_app.config.get("ENROLLMENT_CLOCK", utcnow)()
    if value.tzinfo is not None:
        raise RuntimeError("Enrollment clock must return naive UTC")
    return value.replace(microsecond=0)


def strict(body, fields):
    if type(body) is not dict or set(body) != set(fields):
        fail(400, "INVALID_INPUT")
    return body


def uuid(value):
    wire.canonical_uuid(value)
    return value


def keys(device):
    value = current_app.config.get("ENROLLMENT_DEVICE_KEYS", {}).get(device.device_id)
    if (not value or value["key_id"] != device.key_id or value["key_epoch"] != device.key_epoch):
        fail(503, "ENROLLMENT_KEYS_UNAVAILABLE")
    return value


def candidates(device):
    try:
        slots = json.loads(device.candidate_slots_json)
        valid = (0 <= device.minimum_slot <= device.maximum_slot < device.capacity
            and type(slots) is list and 1 <= len(slots) <= device.capacity
            and len(set(slots)) == len(slots)
            and all(type(s) is int and device.minimum_slot <= s <= device.maximum_slot for s in slots))
    except (ValueError, TypeError):
        valid = False
    if not valid:
        fail(503, "ENROLLMENT_PROFILE_INVALID")
    return sorted(slots)


def active_target(db, target_id):
    target = db.scalar(select(User).where(User.id == target_id).with_for_update()
                       .execution_options(populate_existing=True))
    if target is None or not target.is_active:
        fail(409, "ENROLLMENT_UNAVAILABLE")
    return target


def device_access(db, device_id, target_id):
    device = db.scalar(select(Device).where(Device.device_id == device_id).with_for_update())
    if device is None:
        fail(404, "ENROLLMENT_DEVICE_NOT_FOUND")
    grant = db.scalar(select(ResourceGrant.id).where(ResourceGrant.user_id == g.user.id,
        ResourceGrant.resource_type == "enrollment_device", ResourceGrant.resource_id == device_id).with_for_update())
    if grant is None:
        fail(403, "ENROLLMENT_DEVICE_FORBIDDEN")
    active_target(db, target_id)
    if not device.enabled:
        fail(409, "ENROLLMENT_UNAVAILABLE")
    keys(device)
    candidates(device)
    return device


def binding(job):
    return dict(enrollment_id=job.enrollment_id, operation_revision=str(job.operation_revision),
                reserved_slot=job.reserved_slot, claim_revision=str(job.claim_revision), key_epoch=str(job.key_epoch))


def ticket(job, device):
    context = keys(device)
    # Deterministic regeneration without storing nonce, raw ticket, or bearer in DB.
    nonce = hmac.digest(context["request_key"], b"RW-ENROLL-NONCE-v1\0" + job.enrollment_id.encode(), "sha256")
    session_ref = hmac.digest(context["request_key"], b"RW-ENROLL-SESSION-v1\0" + job.issuer_session_hash.encode(), "sha256")
    value = binding(job) | dict(target_user_id=job.target_user_id, nonce=wire.b64encode(nonce),
        purpose="fingerprint_enrollment", issued_at=iso(job.created_at), expires_at=iso(job.expires_at),
        session_ref=wire.b64encode(session_ref))
    return wire.sign_payload(json.dumps(value, separators=(",", ":")).encode(), kind=wire.REQUEST,
                             device_id=device.device_id, key_id=context["key_id"], key=context["request_key"])


def view(job, device):
    expired = job.state == "issued" and now() >= job.expires_at
    return binding(job) | dict(client_request_id=job.client_request_id, device_id=job.device_id,
        state="expired" if expired else job.state, expires_at=iso(job.expires_at),
        recovery_required=expired or job.state in ("failed", "cancelled", "recovery_required"),
        request_envelope=wire.b64encode(ticket(job, device)) if job.state == "issued" and not expired else None,
        result=json.loads(job.result_json) if job.result_json else None)


def retention(job):
    anchor = job.completed_at or job.expires_at
    if now() >= anchor + timedelta(seconds=current_app.config["ENROLLMENT_RESULT_TTL_SECONDS"]):
        fail(410, "ENROLLMENT_RESULT_EXPIRED")


def load(db, enrollment_id):
    # Shared guard serializes enrollment writers; locator and feature rows use current reads.
    seed = db.scalar(select(Job).where(Job.enrollment_id == uuid(enrollment_id)).with_for_update()
                     .execution_options(populate_existing=True))
    if seed is None:
        fail(404, "ENROLLMENT_NOT_FOUND")
    if seed.issuer_id != g.user.id or seed.issuer_session_hash != g.auth_session.token_hash:
        fail(403, "ENROLLMENT_SESSION_MISMATCH")
    device = device_access(db, seed.device_id, seed.target_user_id)
    job = db.scalar(select(Job).where(Job.enrollment_id == enrollment_id).with_for_update()
                    .execution_options(populate_existing=True))
    claim = db.scalar(select(Claim).where(Claim.device_id == device.device_id,
        Claim.slot == job.reserved_slot).with_for_update().execution_options(populate_existing=True))
    if (job.key_epoch != device.key_epoch or claim is None or claim.enrollment_id != job.enrollment_id
            or claim.revision != job.claim_revision
            or claim.state != ("reserved" if job.state == "issued" else "active" if job.state == "completed" else "quarantined")):
        fail(409, "ENROLLMENT_CLAIM_CHANGED")
    retention(job)
    return job, device, claim


def issue(db, body):
    strict(body, ("client_request_id", "target_user_id", "device_id"))
    uuid(body["client_request_id"]); uuid(body["device_id"]); wire._integer(body["target_user_id"], 1)
    device = device_access(db, body["device_id"], body["target_user_id"])
    previous = db.scalar(select(Job).where(Job.issuer_id == g.user.id,
        Job.issuer_session_hash == g.auth_session.token_hash, Job.client_request_id == body["client_request_id"])
        .with_for_update().execution_options(populate_existing=True))
    if previous:
        if previous.target_user_id != body["target_user_id"] or previous.device_id != body["device_id"]:
            fail(409, "ENROLLMENT_REQUEST_CONFLICT")
        retention(previous)
        return view(previous, device)
    if db.scalar(select(Mapping.mapping_id).where(Mapping.device_id == device.device_id,
                                                 Mapping.user_id == body["target_user_id"]).with_for_update()):
        fail(409, "ENROLLMENT_ALREADY_LINKED")
    if db.scalar(select(Job.enrollment_id).where(Job.device_id == device.device_id,
            Job.target_user_id == body["target_user_id"]).with_for_update()):
        fail(409, "ENROLLMENT_TARGET_REQUIRES_REVIEW")
    # Includes cancelled/expired/quarantined claims: never automatically reuse.
    occupied = set(db.scalars(select(Claim.slot).where(Claim.device_id == device.device_id).with_for_update()))
    slot = next((s for s in candidates(device) if s not in occupied), None)
    if slot is None:
        fail(409, "ENROLLMENT_NO_FREE_CLAIM")
    stamp = now()
    job = Job(enrollment_id=str(uuid4()), client_request_id=body["client_request_id"], issuer_id=g.user.id,
        issuer_session_hash=g.auth_session.token_hash, target_user_id=body["target_user_id"],
        device_id=device.device_id, reserved_slot=slot, operation_revision=1, claim_revision=1,
        key_epoch=device.key_epoch, ticket_digest="", state="issued", created_at=stamp, updated_at=stamp,
        expires_at=stamp + timedelta(seconds=current_app.config["ENROLLMENT_TICKET_TTL_SECONDS"]))
    job.ticket_digest = wire.ticket_digest(ticket(job, device))
    db.add(job); db.flush()
    db.add(Claim(device_id=device.device_id, slot=slot, enrollment_id=job.enrollment_id, revision=1, state="reserved"))
    audit("enrollment.issued", "enrollment", job.enrollment_id, {"device_id": device.device_id, "slot": slot})
    return view(job, device)


def current_permit(job, device, body):
    strict(body, wire.COMMON - {"enrollment_id"} | {"probe_nonce", "ticket_digest", "scope"})
    for field in ("operation_revision", "claim_revision", "key_epoch"):
        wire._revision(body[field])
    wire._integer(body["reserved_slot"], 0)
    if not 16 <= len(wire.b64decode(body["probe_nonce"], 64)) <= 64:
        fail(400, "INVALID_INPUT")
    if any(body[f] != binding(job)[f] for f in wire.COMMON - {"enrollment_id"}) or body["ticket_digest"] != job.ticket_digest:
        fail(409, "ENROLLMENT_BINDING_MISMATCH")
    stamp = now()
    if body["scope"] == "sensor_write":
        if job.state != "issued" or stamp >= job.expires_at:
            fail(409, "ENROLLMENT_WRITE_NOT_ALLOWED")
        deadline = job.expires_at
    elif body["scope"] == "result_read":
        deadline = (job.completed_at or job.expires_at) + timedelta(seconds=current_app.config["ENROLLMENT_RESULT_TTL_SECONDS"])
    else:
        fail(400, "INVALID_INPUT")
    until = min(stamp + timedelta(seconds=approval.MAX_LIFETIME_SECONDS), deadline)
    if (until - stamp).total_seconds() < 2:
        fail(410, "ENROLLMENT_APPROVAL_EXPIRED")
    value = binding(job) | dict(ticket_digest=job.ticket_digest, probe_nonce=body["probe_nonce"],
        scope=body["scope"], decision="allow", server_time=iso(stamp), valid_until=iso(until))
    context = keys(device)
    raw = approval.sign(value, device_id=device.device_id, key_id=context["key_id"], key=context["request_key"])
    return {"approval_envelope": wire.b64encode(raw)}


def complete(db, job, device, claim, body):
    strict(body, ("operation_revision", "result_envelope"))
    wire._revision(body["operation_revision"])
    if body["operation_revision"] != str(job.operation_revision):
        fail(409, "ENROLLMENT_REVISION_CONFLICT")
    raw = wire.b64decode(body["result_envelope"], 16384)
    context = keys(device)
    result = wire.verify_completion(ticket(job, device), raw, device_id=device.device_id, key_id=context["key_id"],
        key_epoch=str(device.key_epoch), request_key=context["request_key"], result_key=context["result_key"])
    digest = hashlib.sha256(raw).hexdigest()
    if job.result_hash:
        if not hmac.compare_digest(job.result_hash, digest):
            fail(409, "ENROLLMENT_RESULT_CONFLICT")
        return view(job, device)
    if job.state != "issued" or now() >= job.expires_at:
        # Late physical storage is never silently activated. Keep its claim reserved.
        fail(409, "ENROLLMENT_RECOVERY_REQUIRED")
    state = "completed" if result["outcome"] == "sensor_stored" else result["outcome"]
    if state == "completed":
        db.add(Mapping(mapping_id=str(uuid4()), enrollment_id=job.enrollment_id, device_id=device.device_id,
            user_id=job.target_user_id, slot=job.reserved_slot, revision=1, active=True, created_at=now()))
    claim.state = "active" if state == "completed" else "quarantined"
    job.state = state
    job.completed_at = job.updated_at = now()
    job.result_hash = digest
    job.result_json = json.dumps({"result_id": result["result_id"], "state": state,
        "failure_code": result["failure_code"]}, separators=(",", ":"))
    audit("enrollment." + state, "enrollment", job.enrollment_id,
          {"device_id": device.device_id, "slot": job.reserved_slot, "failure_code": result["failure_code"]})
    db.flush()  # Mapping, terminal receipt and audit still commit together in the caller.
    return view(job, device)


def cancel(job, device, claim, body):
    strict(body, ("operation_revision",)); wire._revision(body["operation_revision"])
    if body["operation_revision"] != str(job.operation_revision):
        fail(409, "ENROLLMENT_REVISION_CONFLICT")
    if job.state == "cancelled":
        return view(job, device)
    if job.state != "issued":
        fail(409, "ENROLLMENT_ALREADY_TERMINAL")
    job.state, claim.state = "cancelled", "quarantined"
    job.updated_at = job.completed_at = now()
    audit("enrollment.cancelled", "enrollment", job.enrollment_id)
    return view(job, device)
