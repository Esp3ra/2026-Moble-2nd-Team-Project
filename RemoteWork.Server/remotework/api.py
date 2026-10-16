import json
from flask import Blueprint, g, current_app, request
from sqlalchemy import select, func, delete
from .auth import (authenticate, login_required, logout, create_user, text_field,
                   validate_password, revoke_all, admin_transaction)
from werkzeug.security import generate_password_hash, check_password_hash
from .common import AppError, success, payload, require_role, audit
from .db import get_db, transaction, check_schema
from .models import User, AuditEvent, iso

bp = Blueprint("api", __name__, url_prefix="/api/v1")


@bp.get("/health")
def health():
    check_schema()
    return success({"status": "ok", "database": "connected", "schema_version": 1,
                    "version": "1.0.0"})


@bp.post("/auth/login")
def login():
    data = payload()
    user, token, auth_session = authenticate(data.get("login_id"), data.get("password"), "api")
    return success({"access_token": token, "token_type": "Bearer", "expires_at": iso(auth_session.expires_at),
                    "user": user.public()})


@bp.get("/auth/me")
@login_required
def me():
    return success({"user": g.user.public(), "expires_at": iso(g.auth_session.expires_at)})


@bp.post("/auth/logout")
@login_required
def sign_out():
    logout()
    return success({"message": "로그아웃했습니다."})


@bp.post("/auth/password")
@login_required
def change_password():
    data = payload()
    old = data.get("current_password")
    if not isinstance(old, str) or len(old) > 128 or not check_password_hash(g.user.password_hash, old):
        raise AppError(400, "INVALID_PASSWORD", "현재 비밀번호가 올바르지 않습니다.")
    validate_password(data.get("new_password"))
    with transaction() as db:
        g.user.password_hash = generate_password_hash(data["new_password"], method="scrypt")
        revoke_all(db, g.user.id)
        audit("user.password_changed", "user", g.user.id)
    return success({"message": "비밀번호를 변경했습니다. 모든 기기에서 다시 로그인해 주세요."})


def page_args():
    try:
        page = max(1, int(request.args.get("page", "1")))
        limit = min(100, max(1, int(request.args.get("limit", "20"))))
        if page > 1_000_000:
            raise ValueError()
        return page, limit
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "페이지 번호를 확인해 주세요.")


@bp.get("/users")
@require_role("admin")
def users():
    page, limit = page_args()
    db = get_db()
    rows = db.scalars(select(User).order_by(User.id).offset((page - 1) * limit).limit(limit))
    return success({"items": [u.public() for u in rows], "page": page, "limit": limit,
                    "total": db.scalar(select(func.count()).select_from(User))})


@bp.post("/users")
@require_role("admin")
def add_user():
    data = payload()
    with admin_transaction() as db:
        user = create_user(db, data)
        audit("user.created", "user", user.id, {"login_id": user.login_id, "role": user.role})
    return success({"user": user.public()}, 201)


@bp.patch("/users/<int:user_id>")
@require_role("admin")
def update_user(user_id):
    data = payload()
    allowed = {"display_name", "department", "employee_no", "role", "is_active"}
    if not data or set(data) - allowed:
        raise AppError(400, "INVALID_INPUT", "수정할 사용자 항목을 확인해 주세요.")
    with admin_transaction() as db:
        user = db.scalar(select(User).where(User.id == user_id).with_for_update()
                         .execution_options(populate_existing=True))
        if user is None:
            raise AppError(404, "NOT_FOUND", "사용자를 찾을 수 없습니다.")
        before = user.public()
        for field, limit in (("display_name", 80), ("employee_no", 40), ("department", 80)):
            if field in data:
                setattr(user, field, text_field(data, field, limit, required=field != "department"))
        if "role" in data:
            if data["role"] not in ("admin", "employee"):
                raise AppError(400, "INVALID_INPUT", "사용자 역할을 확인해 주세요.")
            user.role = data["role"]
        if "is_active" in data:
            if type(data["is_active"]) is not bool:
                raise AppError(400, "INVALID_INPUT", "활성 여부는 true 또는 false여야 합니다.")
            user.is_active = data["is_active"]
        if user.id == g.user.id and (user.role != "admin" or not user.is_active):
            raise AppError(400, "SELF_LOCKOUT", "본인의 관리자 권한 또는 활성 상태는 해제할 수 없습니다.")
        db.flush()
        # Locking reads observe the latest committed rows, not load_identity's snapshot.
        active_admins = db.scalars(select(User.id).where(
            User.role == "admin", User.is_active.is_(True)).with_for_update()).all()
        if not active_admins:
            raise AppError(409, "LAST_ADMIN", "활성 관리자가 한 명 이상 필요합니다.")
        if before["role"] != user.role or before["is_active"] != user.is_active:
            revoke_all(db, user.id)
        audit("user.updated", "user", user.id, {"before": before, "after": user.public()})
    return success({"user": user.public()})


@bp.post("/users/<int:user_id>/password")
@require_role("admin")
def reset_password(user_id):
    data = payload()
    validate_password(data.get("new_password"))
    with admin_transaction() as db:
        user = db.scalar(select(User).where(User.id == user_id).with_for_update()
                         .execution_options(populate_existing=True))
        if user is None:
            raise AppError(404, "NOT_FOUND", "사용자를 찾을 수 없습니다.")
        user.password_hash = generate_password_hash(data["new_password"], method="scrypt")
        revoke_all(db, user.id)
        audit("user.password_reset", "user", user.id)
    return success({"message": "비밀번호를 초기화하고 모든 접속을 종료했습니다."})


@bp.get("/audit-events")
@require_role("admin")
def events():
    page, limit = page_args()
    query = select(AuditEvent)
    filters = []
    for field in ("resource_type", "resource_id"):
        if field in request.args:
            filters.append(getattr(AuditEvent, field) == request.args[field])
    db = get_db()
    rows = db.scalars(query.where(*filters).order_by(AuditEvent.id.desc()).offset((page - 1) * limit).limit(limit))
    return success({"items": [{"id": row.id, "actor_id": row.actor_id, "action": row.action,
                    "resource_type": row.resource_type, "resource_id": row.resource_id,
                    "details": json.loads(row.details_json), "created_at": iso(row.created_at)} for row in rows],
                    "page": page, "limit": limit,
                    "total": db.scalar(select(func.count()).select_from(AuditEvent).where(*filters))})
