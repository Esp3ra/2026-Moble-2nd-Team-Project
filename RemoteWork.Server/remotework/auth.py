import hashlib
import hmac
import re
import secrets
from datetime import timedelta
from functools import wraps
from flask import current_app, g, request
from sqlalchemy import select, func, delete
from werkzeug.security import generate_password_hash, check_password_hash
from .common import AppError, audit
from .db import get_db
from .models import AuthSession, LoginFailure, User, utcnow


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


def authenticate(login_id, password, channel):
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


def logout():
    db = get_db()
    if g.auth_session:
        audit("auth.logout", "user", g.user.id)
        db.delete(g.auth_session)
        db.commit()
