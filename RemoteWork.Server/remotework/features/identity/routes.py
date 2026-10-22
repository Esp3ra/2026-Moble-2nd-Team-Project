"""Current locking reads and durable exact bytes; all IO stays outside TX."""
from datetime import timedelta
from functools import wraps
import hashlib
import hmac
import json
import secrets
import uuid

from flask import Blueprint, current_app, g, request
from sqlalchemy import CheckConstraint, String, UniqueConstraint, inspect, select

from ... import device_identity_contract as wire
from ...auth import token_digest, verify_credentials, authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...db import transaction
from ...models import AdminGuard, AuthSession, User, iso, utcnow
from ..enrollment.models import Device, Mapping
from .models import IdentityBase, Context, Challenge, Approval, Operation

bp = Blueprint("device_identity", __name__, url_prefix="/api/v1/device-identity")


@bp.get("/workday")
@login_required
def read_workday():
    # Explicit normal Bearer only; pending/revoked result contexts do not grant
    # this read, and GET must not create a new identity Context.
    if request.args:
        fail(400, "IDENTITY_INVALID_INPUT")
    if g.auth_session.channel != "api":
        fail(403, "IDENTITY_NOT_ALLOWED")
    with authenticated_transaction() as db:
        storage()
        provider = current_app.extensions["identity_workflow"]
        reader = getattr(provider, "read_workday", None)
        if not callable(reader):
            fail(503, "IDENTITY_WORKFLOW_NOT_READY")
        result = reader(db, g.user)
    return success(result)


def fail(status, code):
    messages = {
        "IDENTITY_FINGERPRINT_REQUIRED": "오늘 첫 출근은 지문 본인 확인이 필요합니다.",
        "ATTENDANCE_DAY_CLOSED": "오늘 퇴근을 완료했습니다. 같은 날 재출근은 아직 지원하지 않습니다.",
        "ATTENDANCE_DAY_REVIEW_REQUIRED": "이전 근무일의 미종료 기록을 담당자와 확인해 주세요.",
        "ATTENDANCE_ALREADY_STARTED": "이미 출근했습니다. 아이디·비밀번호로 기존 근무에 다시 로그인해 주세요.",
        "ATTENDANCE_NOON_NOT_DUE": "정오 본인 확인은 한국 시간 12시 이후에 진행할 수 있습니다.",
        "ATTENDANCE_NOON_ALREADY_RECORDED": "오늘 정오 확인을 이미 완료했습니다.",
        "ATTENDANCE_NOT_STARTED": "먼저 지문 확인 후 오늘 출근을 완료해 주세요.",
    }
    raise AppError(status, code, messages.get(code, "본인 확인 상태 또는 준비 조건을 확인해 주세요."))


def now():
    return utcnow().replace(microsecond=0)


def stamp(user):
    return hmac.new(current_app.config["SECRET_KEY"].encode(), user.password_hash.encode(), hashlib.sha256).hexdigest()


def locked(db, model, *conditions):
    return db.scalar(select(model).where(*conditions).with_for_update().execution_options(populate_existing=True))


def storage():
    # No DDL or repair here; a partial migration must not become usable.
    inspector = inspect(current_app.extensions["db_engine"])
    database = inspector.default_schema_name
    if not database:
        fail(503, "IDENTITY_STORAGE_NOT_READY")
    tables = list(IdentityBase.metadata.tables.values())
    extra = getattr(current_app.extensions["identity_workflow"], "schema_tables", None)
    if callable(extra):
        tables.extend(extra())
    for table in tables:
        if not inspector.has_table(table.name):
            fail(503, "IDENTITY_STORAGE_NOT_READY")
        columns = {c["name"]: c for c in inspector.get_columns(table.name)}
        if not set(table.c.keys()) <= set(columns):
            fail(503, "IDENTITY_STORAGE_NOT_READY")
        for col in table.c:
            actual = columns[col.name]
            if actual["nullable"] != col.nullable:
                fail(503, "IDENTITY_STORAGE_NOT_READY")
            if isinstance(col.type, String) and col.type.length is not None:
                if getattr(actual["type"], "length", None) != col.type.length:
                    fail(503, "IDENTITY_STORAGE_NOT_READY")
        pk = set(inspector.get_pk_constraint(table.name)["constrained_columns"])
        if pk != {c.name for c in table.primary_key}:
            fail(503, "IDENTITY_STORAGE_NOT_READY")
        unique = {tuple(v["column_names"]) for v in inspector.get_unique_constraints(table.name)}
        if any(tuple(c.name for c in constraint.columns) not in unique
               for constraint in table.constraints if isinstance(constraint, UniqueConstraint)):
            fail(503, "IDENTITY_STORAGE_NOT_READY")
        fks = {(f.get("referred_schema") or database, tuple(f["constrained_columns"]), f["referred_table"], tuple(f["referred_columns"]))
               for f in inspector.get_foreign_keys(table.name)}
        for fk in table.foreign_key_constraints:
            wanted = (database, tuple(c.name for c in fk.columns), fk.referred_table.name,
                      tuple(e.column.name for e in fk.elements))
            if wanted not in fks:
                fail(503, "IDENTITY_STORAGE_NOT_READY")
        checks = {c["name"] for c in inspector.get_check_constraints(table.name)}
        if any(c.name not in checks for c in table.constraints if isinstance(c, CheckConstraint)):
            fail(503, "IDENTITY_STORAGE_NOT_READY")


def body(operation):
    if not request.is_json:
        fail(415, "JSON_REQUIRED")
    return wire.validate_api_input(operation, request.get_data())


def context(db, *, revoked_result_only=False, receipt_result=False):
    header = request.headers.get("Authorization", "")
    if len(header) > 220:
        fail(401, "IDENTITY_CONTEXT_REQUIRED")
    if header.startswith("IdentityPending "):
        ctx = locked(db, Context, Context.token_hash == token_digest(header[16:]))
    elif header.startswith("Bearer "):
        session_hash = token_digest(header[7:])
        recovered = locked(db, Context, Context.auth_hash == session_hash) if revoked_result_only or receipt_result else None
        # Resolve IDs with a current read; lock actor before session/context.
        sid = db.scalar(select(AuthSession.user_id).where(AuthSession.token_hash == session_hash).with_for_update())
        if sid is None and recovered is not None and ((revoked_result_only and (recovered.revoked or recovered.receipt_only))
                or (receipt_result and recovered.receipt_only)):
            ctx = recovered
        elif sid is None:
            fail(401, "IDENTITY_CONTEXT_REQUIRED")
        else:
            actor = locked(db, User, User.id == sid)
            session = locked(db, AuthSession, AuthSession.token_hash == session_hash)
            if not actor or not actor.is_active or not session or session.channel != "api" or session.expires_at <= now():
                fail(401, "IDENTITY_CONTEXT_REQUIRED")
            ctx = locked(db, Context, Context.auth_hash == session_hash)
            if ctx is None:
                ctx = Context(id=str(uuid.uuid4()), user_id=actor.id, auth_hash=session_hash,
                    token_hash=None, login_request_id=None, context_ref=secrets.token_urlsafe(32),
                    password_stamp=stamp(actor), revoked=False, created_at=now(),
                    expires_at=session.expires_at, lookup_until=now() + timedelta(days=1))
                db.add(ctx); db.flush()
    else:
        fail(401, "IDENTITY_CONTEXT_REQUIRED")
    if ctx is None or (ctx.revoked and not revoked_result_only) or ctx.expires_at <= now():
        fail(401, "IDENTITY_CONTEXT_REQUIRED")
    if ctx.receipt_only and not (receipt_result or revoked_result_only):
        fail(403, "IDENTITY_NOT_ALLOWED")
    actor = locked(db, User, User.id == ctx.user_id)
    if not actor or not actor.is_active or not hmac.compare_digest(ctx.password_stamp, stamp(actor)):
        fail(401, "IDENTITY_CONTEXT_REQUIRED")
    g.user = actor
    return ctx, actor


def guarded(fn):
    @wraps(fn)
    def run(*args, **kwargs):
        try:
            storage()
            with transaction() as db:
                if locked(db, AdminGuard, AdminGuard.id == 1) is None:
                    fail(503, "IDENTITY_STORAGE_NOT_READY")
                result = fn(db, *args, **kwargs)
            # Expiry/cancel transitions commit before reporting their error.
            if isinstance(result, AppError):
                raise result
            return success(result)
        except wire.ProtocolError:
            fail(400, "IDENTITY_INVALID_INPUT")
    return run


def error(status, code):
    return AppError(status, code, "본인 확인 상태 또는 준비 조건을 확인해 주세요.")


def challenge(db, ctx, cid):
    wire.canonical_uuid(cid)
    item = locked(db, Challenge, Challenge.id == cid)
    if item is None or item.context_id != ctx.id:
        fail(404, "IDENTITY_NOT_FOUND")
    if ctx.receipt_only and item.id != ctx.completed_challenge_id:
        fail(403, "IDENTITY_NOT_ALLOWED")
    if item.lookup_until <= now():
        fail(410, "IDENTITY_LOOKUP_EXPIRED")
    ticket = wire.parse_json(wire.b64decode(wire.parse_json(item.request_bytes)["payload_b64"]))
    if ticket["session_ref"] != ctx.context_ref:
        fail(404, "IDENTITY_NOT_FOUND")
    if item.state == "issued" and item.expires_at <= now():
        item.state = "expired"
    return item, ticket


def profile(db, ctx, device_id):
    p = current_app.extensions["identity_profiles"].get(device_id)
    device = locked(db, Device, Device.device_id == device_id)
    mapping = locked(db, Mapping, Mapping.device_id == device_id, Mapping.user_id == ctx.user_id)
    if (p is None or not p["enabled"] or not device or not device.enabled or not mapping or not mapping.active
            or str(device.key_epoch) != p["key_epoch"]
            or (device.minimum_slot, device.maximum_slot, device.capacity) != (p["minimum_slot"], p["maximum_slot"], p["capacity"])
            or not p["minimum_slot"] <= mapping.slot <= p["maximum_slot"]):
        fail(403, "IDENTITY_NOT_ALLOWED")
    return p, mapping


def workflow(db, ctx, actor, purpose):
    if (ctx.token_hash is not None) != (purpose == "clock_in"):
        fail(403, "IDENTITY_NOT_ALLOWED")
    value = current_app.extensions["identity_workflow"].current(db, actor, purpose)
    if type(value) is not dict or set(value) != {"work_date", "work_session_ref"}:
        fail(503, "IDENTITY_WORKFLOW_NOT_READY")
    # Current project work date is KST (UTC+09:00); no host-local timezone or
    # optional Windows tzdata dependency. Use the same server UTC as expiry.
    day = (now() + timedelta(hours=9)).date().isoformat()
    if value["work_date"] != day:
        fail(409, "IDENTITY_REQUEST_CONFLICT")
    if purpose == "clock_in":
        if value["work_session_ref"] is not None:
            fail(409, "IDENTITY_REQUEST_CONFLICT")
    else:
        wire.canonical_uuid(value["work_session_ref"])
    return value


def current_binding(db, ctx, actor, item, ticket):
    p, mapping = profile(db, ctx, item.device_id)
    wire.verify(item.request_bytes, kind="id_request", device_id=item.device_id,
                key_id=p["key_id"], key=p["keys"]["id_request"])
    state = {} if item.workflow_completed else workflow(db, ctx, actor, item.purpose)
    envelope = wire.parse_json(item.request_bytes)
    if (mapping.mapping_id != item.mapping_id or str(mapping.revision) != ticket["mapping_revision"] or mapping.slot != ticket["expected_slot"]
            or p["key_epoch"] != ticket["key_epoch"] or p["key_id"] != envelope["key_id"]
            or any(state[k] != ticket[k] for k in state)):
        fail(403, "IDENTITY_NOT_ALLOWED")
    return p


def view(item):
    return dict(challenge_id=item.id, state=item.state, expires_at=iso(item.expires_at),
        identity_receipt=json.loads(item.receipt_json) if item.receipt_json else None,
        workflow_state=("completed" if item.workflow_completed else "pending") if item.state == "verified" else "not_started")


def issued_view(item):
    return dict(challenge_id=item.id, client_request_id=item.client_request_id,
                state=item.state, expires_at=iso(item.expires_at), request_b64=wire.b64encode(item.request_bytes))


@bp.post("/login")
@guarded
def login(db):
    data = body("login")
    actor = verify_credentials(data["login_id"], data["password"])
    t = now()
    ctx = locked(db, Context, Context.user_id == actor.id, Context.login_request_id == data["client_request_id"])
    if ctx is not None and ctx.lookup_until <= t:
        fail(410, "IDENTITY_LOOKUP_EXPIRED")
    for prior in db.scalars(select(Context).where(Context.user_id == actor.id, Context.token_hash.is_not(None)).with_for_update()):
        prior.revoked = True
        for item in db.scalars(select(Challenge).where(Challenge.context_id == prior.id, Challenge.state == "issued").with_for_update()):
            item.state = "revoked"
    if ctx is None:
        ctx = Context(id=str(uuid.uuid4()), user_id=actor.id, login_request_id=data["client_request_id"],
                      auth_hash=None, created_at=t, lookup_until=t + timedelta(days=1))
        db.add(ctx)
    token = secrets.token_urlsafe(32)
    ctx.token_hash, ctx.context_ref, ctx.password_stamp = token_digest(token), secrets.token_urlsafe(32), stamp(actor)
    ctx.revoked, ctx.expires_at = False, t + timedelta(seconds=wire.PENDING_LOGIN_SECONDS)
    audit("identity.pending_login", "user", actor.id, actor_id=actor.id)
    return dict(pending_token=token, token_type="IdentityPending", context_ref=ctx.context_ref,
                expires_at=iso(ctx.expires_at), state="fingerprint_required")


@bp.post("/challenges")
@guarded
def issue(db):
    data = body("issue"); ctx, actor = context(db)
    if (ctx.token_hash is not None) != (data["purpose"] == "clock_in"):
        fail(403, "IDENTITY_NOT_ALLOWED")
    p, mapping = profile(db, ctx, data["device_id"])
    old = locked(db, Challenge, Challenge.context_id == ctx.id, Challenge.client_request_id == data["client_request_id"])
    if old:
        old, ticket = challenge(db, ctx, old.id)
        if old.device_id != data["device_id"] or old.purpose != data["purpose"]:
            fail(409, "IDENTITY_REQUEST_CONFLICT")
        current_binding(db, ctx, actor, old, ticket)
        return issued_view(old)
    state = workflow(db, ctx, actor, data["purpose"])
    # Global guard + device lock serializes every cooperating issuer. Old RAM
    # cannot silently become a second scan on another login or PC.
    for active in db.scalars(select(Challenge).where(Challenge.device_id == data["device_id"], Challenge.state == "issued").with_for_update()):
        if active.expires_at <= now():
            active.state = "expired"
        else:
            fail(409, "IDENTITY_REQUEST_CONFLICT")
    t = now(); expiry = min(t + timedelta(seconds=wire.CHALLENGE_SECONDS), ctx.expires_at.replace(microsecond=0))
    cid = str(uuid.uuid4())
    ticket = dict(challenge_id=cid, nonce=secrets.token_urlsafe(32), mapping_revision=str(mapping.revision),
                  key_epoch=p["key_epoch"], expected_slot=mapping.slot, purpose=data["purpose"],
                  session_ref=ctx.context_ref, **state, issued_at=iso(t), expires_at=iso(expiry))
    raw = wire.sign(json.dumps(ticket, separators=(",", ":")).encode(), kind="id_request",
                    device_id=data["device_id"], key_id=p["key_id"], key=p["keys"]["id_request"])
    item = Challenge(id=cid, context_id=ctx.id, client_request_id=data["client_request_id"],
        device_id=data["device_id"], mapping_id=mapping.mapping_id, purpose=data["purpose"], request_bytes=raw, state="issued",
        created_at=t, expires_at=expiry, lookup_until=t + timedelta(days=1))
    db.add(item)
    audit("identity.issued", "identity", cid, {"purpose": item.purpose})
    return issued_view(item)


@bp.get("/requests/<rid>")
@guarded
def lookup(db, rid):
    wire.canonical_uuid(rid); ctx, actor = context(db, receipt_result=True)
    item = locked(db, Challenge, Challenge.context_id == ctx.id, Challenge.client_request_id == rid)
    if item is None:
        if ctx.receipt_only:
            fail(403, "IDENTITY_NOT_ALLOWED")
        return {"state": "not_recorded"}
    item, ticket = challenge(db, ctx, item.id)
    current_binding(db, ctx, actor, item, ticket)
    return issued_view(item)


@bp.get("/challenges/<cid>")
@guarded
def get(db, cid):
    ctx, actor = context(db, receipt_result=True); item, ticket = challenge(db, ctx, cid)
    current_binding(db, ctx, actor, item, ticket)
    return view(item)


@bp.post("/challenges/<cid>/approvals")
@guarded
def approve(db, cid):
    data = body("approval"); ctx, actor = context(db)
    item, ticket = challenge(db, ctx, cid)
    p = current_binding(db, ctx, actor, item, ticket)
    if item.state != "issued":
        return error(410 if item.state == "expired" else 409, "IDENTITY_EXPIRED" if item.state == "expired" else "IDENTITY_TERMINAL")
    raw = wire.b64decode(data["probe_b64"], 16384)
    probe = wire.verify(raw, kind="id_probe", device_id=item.device_id, key_id=p["key_id"], key=p["keys"]["id_probe"])
    if any(probe[k] != ticket[k] for k in wire.COMMON) or probe["ticket_digest"] != wire.message_digest(item.request_bytes):
        fail(400, "IDENTITY_INVALID_INPUT")
    old = locked(db, Approval, Approval.challenge_id == cid, Approval.probe_nonce == probe["probe_nonce"])
    if old:
        if old.probe_bytes != raw:
            fail(409, "IDENTITY_REQUEST_CONFLICT")
        if old.expires_at <= now():
            return error(410, "IDENTITY_EXPIRED")
        return {"approval_b64": wire.b64encode(old.approval_bytes)}
    if probe["scope"] == "capture":
        if item.capture_nonce is not None:
            fail(409, "IDENTITY_REQUEST_CONFLICT")
        item.capture_nonce = probe["probe_nonce"]
    elif item.capture_nonce is None:
        fail(409, "IDENTITY_REQUEST_CONFLICT")
    t = now(); expiry = min(t + timedelta(seconds=wire.APPROVAL_SECONDS), item.expires_at)
    payload = probe | dict(decision="allow", server_time=iso(t), valid_until=iso(expiry))
    answer = wire.sign(json.dumps(payload, separators=(",", ":")).encode(), kind="id_approval",
                       device_id=item.device_id, key_id=p["key_id"], key=p["keys"]["id_approval"])
    db.add(Approval(digest=wire.message_digest(answer), challenge_id=cid, probe_nonce=probe["probe_nonce"],
                   probe_bytes=raw, approval_bytes=answer, scope=probe["scope"], expires_at=expiry))
    return {"approval_b64": wire.b64encode(answer)}


@bp.post("/challenges/<cid>/complete")
@guarded
def complete(db, cid):
    data = body("complete"); ctx, actor = context(db, receipt_result=True)
    item, ticket = challenge(db, ctx, cid)
    p = current_binding(db, ctx, actor, item, ticket)
    raw = wire.b64decode(data["result_b64"], 16384)
    result = wire.verify(raw, kind="id_result", device_id=item.device_id, key_id=p["key_id"], key=p["keys"]["id_result"])
    if item.result_bytes is not None:
        if item.result_bytes != raw:
            fail(409, "IDENTITY_RESULT_CONFLICT")
        return view(item)
    if item.state != "issued":
        return error(410 if item.state == "expired" else 409, "IDENTITY_EXPIRED" if item.state == "expired" else "IDENTITY_TERMINAL")
    approval = None
    if result["approval_digest"] is not None:
        approval = locked(db, Approval, Approval.digest == result["approval_digest"], Approval.challenge_id == cid)
        if approval is None or approval.scope != "result" or approval.expires_at <= now():
            fail(409, "IDENTITY_TERMINAL")
    wire.bind_result(result, ticket, ticket_bytes=item.request_bytes,
                     result_approval_bytes=approval.approval_bytes if approval else None)
    if locked(db, Challenge, Challenge.result_id == result["result_id"]) is not None:
        fail(409, "IDENTITY_RESULT_CONFLICT")
    item.state = "verified" if result["outcome"] == "matched" else "rejected"
    item.result_id, item.result_bytes, item.result_hash = result["result_id"], raw, hashlib.sha256(raw).hexdigest()
    if item.state == "verified":
        item.receipt_json = json.dumps(dict(receipt_id=str(uuid.uuid4()), purpose=item.purpose, verified_at=iso(now())))
    audit("identity." + item.state, "identity", cid, {"purpose": item.purpose, "outcome": result["outcome"]})
    return view(item)


def operation(db, ctx, data, name, target, response):
    old = locked(db, Operation, Operation.context_id == ctx.id, Operation.client_request_id == data["client_request_id"])
    if old:
        if old.expires_at <= now():
            fail(410, "IDENTITY_LOOKUP_EXPIRED")
        if old.operation != name or old.target_id != target:
            fail(409, "IDENTITY_REQUEST_CONFLICT")
        return json.loads(old.response_json)
    db.add(Operation(context_id=ctx.id, client_request_id=data["client_request_id"], operation=name,
                     target_id=target, response_json=json.dumps(response), expires_at=now() + timedelta(days=1)))
    return response


@bp.post("/challenges/<cid>/cancel")
@guarded
def cancel(db, cid):
    data = body("cancel"); ctx, actor = context(db)
    item, ticket = challenge(db, ctx, cid)
    current_binding(db, ctx, actor, item, ticket)
    if item.state == "issued":
        item.state = "cancelled"
        audit("identity.cancelled", "identity", cid)
    return operation(db, ctx, data, "cancel", cid, {"challenge_id": cid, "state": item.state})


@bp.post("/context/revoke")
@guarded
def revoke(db):
    data = body("revoke"); ctx, _ = context(db, revoked_result_only=True)
    if ctx.revoked:
        prior = locked(db, Operation, Operation.context_id == ctx.id, Operation.client_request_id == data["client_request_id"])
        if prior is None or prior.operation != "revoke" or prior.target_id != ctx.id or prior.expires_at <= now():
            fail(401, "IDENTITY_CONTEXT_REQUIRED")
        return json.loads(prior.response_json)
    answer = operation(db, ctx, data, "revoke", ctx.id, {"state": "revoked"})
    ctx.revoked = True
    if ctx.auth_hash:
        session = locked(db, AuthSession, AuthSession.token_hash == ctx.auth_hash)
        if session is not None:
            db.delete(session)
    for item in db.scalars(select(Challenge).where(Challenge.context_id == ctx.id, Challenge.state == "issued").with_for_update()):
        item.state = "revoked"
    audit("identity.context_revoked", "user", ctx.user_id)
    return answer


@bp.post("/challenges/<cid>/finalize")
@guarded
def finalize(db, cid):
    data = body("finalize"); ctx, actor = context(db, receipt_result=True)
    item, ticket = challenge(db, ctx, cid)
    current_binding(db, ctx, actor, item, ticket)
    if item.state != "verified":
        return error(409, "IDENTITY_TERMINAL")
    writer = getattr(current_app.extensions["identity_workflow"], "finalize", None)
    if not callable(writer):
        return error(503, "IDENTITY_WORKFLOW_NOT_READY")
    return writer(db, ctx, actor, item, data["client_request_id"])
