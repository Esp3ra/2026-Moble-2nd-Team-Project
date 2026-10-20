from functools import wraps
from flask import Blueprint, request
from sqlalchemy import select
from ...auth import authenticated_transaction, login_required
from ...common import success
from ...db import check_schema
from ... import enrollment_protocol_draft as wire
from .models import Device, Job
from . import service as svc

bp = Blueprint("enrollment_draft", __name__, url_prefix="/api/v1/device-enrollments")


def guarded(fn):
    @wraps(fn)
    @login_required
    def run(*args, **kwargs):
        try:
            with authenticated_transaction(roles=("admin",)) as db:
                check_schema()
                result = fn(db, *args, **kwargs)
            return success(result)
        except wire.ProtocolError:
            svc.fail(400, "INVALID_ENROLLMENT_MESSAGE")
    return run


def body():
    if not request.is_json:
        svc.fail(415, "JSON_REQUIRED")
    # Reject duplicate keys instead of Flask's last-value-wins parser.
    return wire.parse_json(request.get_data(), 30000)


@bp.get("/eligible-devices")
@guarded
def eligible(db):
    if set(request.args) != {"target_user_id"} or len(request.args.getlist("target_user_id")) != 1:
        svc.fail(400, "INVALID_INPUT")
    raw = request.args["target_user_id"]
    if not 1 <= len(raw) <= 10 or not raw.isascii() or not raw.isdecimal() or str(int(raw)) != raw:
        svc.fail(400, "INVALID_INPUT")
    target = int(raw); wire._integer(target, 1)
    available = []
    for device_id in db.scalars(select(Device.device_id).order_by(Device.device_id).with_for_update()):
        # Permission filtering is exact; only explicit device grants are exposed.
        from flask import g
        from ...models import ResourceGrant
        grant = db.scalar(select(ResourceGrant.id).where(ResourceGrant.user_id == g.user.id,
            ResourceGrant.resource_type == "enrollment_device", ResourceGrant.resource_id == device_id).with_for_update())
        if grant:
            device = svc.device_access(db, device_id, target)
            available.append({"device_id": device_id, "display_name": device.display_name,
                              "key_epoch": str(device.key_epoch)})
    return {"devices": available}


@bp.post("")
@guarded
def issue(db):
    return svc.issue(db, body())


@bp.get("/by-request/<client_request_id>")
@guarded
def by_request(db, client_request_id):
    from flask import g
    svc.uuid(client_request_id)
    job = db.scalar(select(Job).where(Job.issuer_id == g.user.id,
        Job.issuer_session_hash == g.auth_session.token_hash, Job.client_request_id == client_request_id).with_for_update())
    if job is None:
        svc.fail(404, "ENROLLMENT_NOT_FOUND")
    job, device, _ = svc.load(db, job.enrollment_id)
    return svc.view(job, device)


@bp.get("/<enrollment_id>")
@guarded
def get_job(db, enrollment_id):
    job, device, _ = svc.load(db, enrollment_id)
    return svc.view(job, device)


@bp.post("/<enrollment_id>/approval")
@guarded
def permit(db, enrollment_id):
    job, device, _ = svc.load(db, enrollment_id)
    return svc.current_permit(job, device, body())


@bp.post("/<enrollment_id>/complete")
@guarded
def complete(db, enrollment_id):
    return svc.complete(db, *svc.load(db, enrollment_id), body())


@bp.post("/<enrollment_id>/cancel")
@guarded
def cancel(db, enrollment_id):
    job, device, claim = svc.load(db, enrollment_id)
    return svc.cancel(job, device, claim, body())
