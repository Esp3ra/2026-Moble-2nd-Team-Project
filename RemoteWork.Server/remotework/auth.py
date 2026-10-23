import hashlib
import hmac
import re
import secrets
from contextlib import contextmanager
from datetime import timedelta
from functools import wraps
from flask import current_app, g, request
from sqlalchemy import select, func, delete
from werkzeug.security import generate_password_hash, check_password_hash
from .common import AppError, audit
from .db import get_db, transaction
from .models import AdminGuard, AuthSession, LoginFailure, User, utcnow


def token_digest(token):
    return hashlib.sha256(token.encode()).hexdigest()


def validate_password(password):
    if not isinstance(password, str) or not 10 <= len(password) <= 128:
        raise AppError(400, "INVALID_INPUT", "비밀번호는 10~128자로 입력해 주세요.")


def normalize_login(login_id):
    if not isinstance(login_id, str) or not re.fullmatch(r"[A-Za-z0-9_.-]{3,50}", login_id):
        raise AppError(400, "INVALID_INPUT", "아이디는 영문·숫자·밑줄·점·하이픈 3~50자로 입력해 주세요.")
    return login_id.lower()


def text_field(body, name, limit, *, required=True):
    value = body.get(name, "")
    if not isinstance(value, str) or len(value.strip()) > limit or (required and not value.strip()):
        raise AppError(400, "INVALID_INPUT", f"{name} 입력값을 확인해 주세요. (최대 {limit}자)")
    return value.strip()


def create_user(db, body):
    password = body.get("password")
    validate_password(password)
    role = body.get("role", "employee")
    if role not in ("employee", "admin"):
        raise AppError(400, "INVALID_INPUT", "사용자 역할을 확인해 주세요.")
    user = User(login_id=normalize_login(body.get("login_id")),
                password_hash=generate_password_hash(password, method="scrypt"),
                display_name=text_field(body, "display_name", 80),
                employee_no=text_field(body, "employee_no", 40),
                department=text_field(body, "department", 80, required=False), role=role)
    db.add(user)
    db.flush()
    return user


def verify_credentials(login_id, password):
    # Normalize malformed credentials to a generic login failure without exposing accounts.
    if not isinstance(login_id, str) or not isinstance(password, str) or len(login_id) > 50 or len(password) > 128:
        raise AppError(401, "INVALID_CREDENTIALS", "아이디 또는 비밀번호가 올바르지 않습니다.")
    login_id = login_id.strip().lower()
    db = get_db()
    now = utcnow()
    secret = current_app.config["SECRET_KEY"].encode()
    key = lambda value: hmac.new(secret, value.encode(), hashlib.sha256).hexdigest()
    account_key, ip_key = key("account:" + login_id), key("ip:" + (request.remote_addr or "unknown"))
    cutoff = now - timedelta(minutes=15)
    account_count = db.scalar(select(func.count()).select_from(LoginFailure).where(
        LoginFailure.account_key == account_key, LoginFailure.created_at > cutoff))
    ip_count = db.scalar(select(func.count()).select_from(LoginFailure).where(
        LoginFailure.ip_key == ip_key, LoginFailure.created_at > cutoff))
    if account_count >= 5 or ip_count >= 50:
        raise AppError(429, "LOGIN_THROTTLED", "로그인 실패가 반복되었습니다. 15분 후 다시 시도해 주세요.")
    user = db.scalar(select(User).where(User.login_id == login_id).with_for_update())
    valid = check_password_hash(user.password_hash if user else current_app.config["DUMMY_HASH"], password)
    if not user or not valid or not user.is_active:
        db.add(LoginFailure(account_key=account_key, ip_key=ip_key))
        audit("auth.login_failed", "auth", "login")
        db.commit()
        raise AppError(401, "INVALID_CREDENTIALS", "아이디 또는 비밀번호가 올바르지 않습니다.")
    return user


def authenticate(login_id, password, channel):
    # First attendance still requires identity; later application login does not
    # start/reopen work, including when today's work has already ended.
    if current_app.config.get("ENABLE_DEVICE_IDENTITY_DRAFT"):
        provider = current_app.extensions.get("identity_workflow")
        if not callable(getattr(provider, "resume", None)):
            raise AppError(503, "IDENTITY_WORKFLOW_NOT_READY", "지문 본인 확인 로그인 연결이 필요합니다.")
        from .features.identity.routes import storage
        storage()
        with transaction() as db:
            guard = db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
            if guard is None:
                raise AppError(503, "IDENTITY_STORAGE_NOT_READY", "근무 저장소 준비가 필요합니다.")
            user = verify_credentials(login_id, password)
            work = provider.resume(db, user)  # Actual same-day attendance, never a PC flag.
            token = secrets.token_urlsafe(32)
            auth_session = AuthSession(token_hash=token_digest(token), user_id=user.id, channel=channel,
                expires_at=utcnow() + timedelta(hours=current_app.config["SESSION_HOURS"]))
            db.add(auth_session)
            audit("auth.login", "user", user.id,
                {"channel": channel, "resumed_work": work.ended_at is None}, actor_id=user.id)
        return user, token, auth_session
    user = verify_credentials(login_id, password)
    db = get_db()
    now = utcnow()
    token = secrets.token_urlsafe(32)
    auth_session = AuthSession(token_hash=token_digest(token), user_id=user.id, channel=channel,
                               expires_at=now + timedelta(hours=current_app.config["SESSION_HOURS"]))
    db.add(auth_session)
    audit("auth.login", "user", user.id, {"channel": channel}, actor_id=user.id)
    db.commit()
    return user, token, auth_session


def load_identity():
    g.user = None
    g.auth_session = None
    if request.path.startswith("/api/"):
        authorization = request.headers.get("Authorization", "")
        token = authorization[7:] if authorization.startswith("Bearer ") else ""
        channel = "api"
    else:
        token = request.cookies.get("rw_session", "")
        channel = "web"
    if not token or len(token) > 200:
        return
    db = get_db()
    auth_session = db.get(AuthSession, token_digest(token))
    if auth_session is None or auth_session.channel != channel or auth_session.expires_at <= utcnow():
        return
    user = db.get(User, auth_session.user_id)
    if user and current_app.config.get("ENABLE_DEVICE_IDENTITY_DRAFT"):
        provider = current_app.extensions.get("identity_workflow")
        if not callable(getattr(provider, "can_resume", None)) or not provider.can_resume(db, user):
            return
    if user and user.is_active:
        g.user, g.auth_session = user, auth_session


def login_required(fn):
    @wraps(fn)
    def wrapped(*args, **kwargs):
        if not getattr(g, "user", None):
            raise AppError(401, "AUTH_REQUIRED", "로그인이 필요하거나 로그인 시간이 만료되었습니다.")
        return fn(*args, **kwargs)
    return wrapped


def revoke_all(db, user_id):
    db.execute(delete(AuthSession).where(AuthSession.user_id == user_id))


@contextmanager
def authenticated_transaction(*, roles=()):
    """Recheck a current identity under the shared write guard, including staff.

    load_identity() may have established an older MySQL REPEATABLE READ snapshot.
    Refresh ORM objects too: a locking SELECT alone can reuse a cached User.
    Cooperating writes acquire guard -> actor -> session before feature rows.
    Use one parent/child order across feature services and sort target user IDs.
    The shared guard serializes cooperating writers. Feature services must
    check their own CURRENT relationships/versions/state before any mutation.
    This guard grants no feature permission and must not contain network calls.
    """
    if not getattr(g, "user", None) or not getattr(g, "auth_session", None):
        raise AppError(401, "AUTH_REQUIRED", "로그인이 필요하거나 로그인 시간이 만료되었습니다.")
    if getattr(g, "write_transaction_active", False):
        raise RuntimeError("Write transactions must not be nested")
    actor_id, token_hash = g.user.id, g.auth_session.token_hash
    channel = g.auth_session.channel
    with transaction() as db:
        guard = db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update())
        if guard is None:
            raise AppError(503, "DATABASE_UNAVAILABLE", "관리자 보호 테이블 초기화 상태를 확인해 주세요.")
        actor = db.scalar(select(User).where(User.id == actor_id).with_for_update()
                          .execution_options(populate_existing=True))
        session = db.scalar(select(AuthSession).where(AuthSession.token_hash == token_hash).with_for_update()
                            .execution_options(populate_existing=True))
        if (actor is None or not actor.is_active or session is None
                or session.user_id != actor_id or session.channel != channel
                or session.expires_at <= utcnow()):
            raise AppError(401, "AUTH_REQUIRED", "로그인이 필요하거나 로그인 시간이 만료되었습니다.")
        if roles and actor.role not in roles:
            raise AppError(403, "FORBIDDEN", "접근 권한이 없습니다.")
        g.user, g.auth_session = actor, session
        g.write_transaction_active = True
        try:
            yield db
        finally:
            g.write_transaction_active = False


@contextmanager
def admin_transaction():
    """Existing admin contract, now sharing the staff-capable current-read guard."""
    with authenticated_transaction(roles=("admin",)) as db:
        yield db


def logout():
    db = get_db()
    if g.auth_session:
        audit("auth.logout", "user", g.user.id)
        db.delete(g.auth_session)
        db.commit()
