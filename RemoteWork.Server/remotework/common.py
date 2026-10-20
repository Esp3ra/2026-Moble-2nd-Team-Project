import json
from functools import wraps
from flask import g, jsonify, request
from sqlalchemy import select
from .db import get_db
from .models import AuditEvent, ResourceGrant


class AppError(Exception):
    def __init__(self, status, code, message):
        self.status, self.code, self.message = status, code, message
        super().__init__(message)


def success(data=None, status=200):
    return jsonify(ok=True, data=data, request_id=g.request_id), status


def payload():
    if not request.is_json:
        raise AppError(415, "JSON_REQUIRED", "JSON 형식으로 요청해 주세요.")
    body = request.get_json()
    if not isinstance(body, dict):
        raise AppError(400, "INVALID_INPUT", "입력 형식을 확인해 주세요.")
    return body


def audit(action, resource_type, resource_id, details=None, actor_id=None):
    """Does not commit. The caller commits the business change and audit together."""
    def scrub(value):
        if isinstance(value, dict):
            return {k: ("[redacted]" if any(s in k.lower() for s in
                    ("password", "token", "secret", "credential")) else scrub(v))
                    for k, v in value.items()}
        if isinstance(value, list):
            return [scrub(v) for v in value]
        return value
    get_db().add(AuditEvent(actor_id=actor_id if actor_id is not None else
                (g.user.id if getattr(g, "user", None) else None),
                action=action, resource_type=resource_type, resource_id=str(resource_id),
                details_json=json.dumps(scrub(details or {}), ensure_ascii=False)))


def require_role(*roles):
    from .auth import login_required
    def decorate(fn):
        @wraps(fn)
        @login_required
        def wrapped(*args, **kwargs):
            if g.user.role not in roles:
                raise AppError(403, "FORBIDDEN", "접근 권한이 없습니다.")
            return fn(*args, **kwargs)
        return wrapped
    return decorate


def grant_resource(db, user_id, resource_type, resource_id):
    existing = db.scalar(select(ResourceGrant).where(
        ResourceGrant.user_id == user_id, ResourceGrant.resource_type == resource_type,
        ResourceGrant.resource_id == str(resource_id)))
    if existing is None:
        db.add(ResourceGrant(user_id=user_id, resource_type=resource_type,
                             resource_id=str(resource_id)))


def require_resource_access(resource_type, resource_id, *, allow_admin=True):
    """Call after login_required, BEFORE reading/writing/downloading feature data."""
    if not getattr(g, "user", None):
        raise AppError(401, "AUTH_REQUIRED", "로그인이 필요합니다.")
    if allow_admin and g.user.role == "admin":
        return
    grant = get_db().scalar(select(ResourceGrant.id).where(
        ResourceGrant.user_id == g.user.id, ResourceGrant.resource_type == resource_type,
        ResourceGrant.resource_id == str(resource_id)))
    if grant is None:
        raise AppError(403, "FORBIDDEN", "이 항목에 접근할 권한이 없습니다.")
