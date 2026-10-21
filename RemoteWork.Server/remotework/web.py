import hmac
import secrets
from flask import Blueprint, current_app, g, redirect, render_template, request, session, url_for
from sqlalchemy import select
from .auth import authenticate, login_required, logout
from .common import AppError
from .db import get_db
from .models import User

bp = Blueprint("web", __name__)


def csrf_token():
    if "csrf_token" not in session:
        session["csrf_token"] = secrets.token_urlsafe(32)
    return session["csrf_token"]


def protect_forms():
    if request.method in ("POST", "PATCH", "PUT", "DELETE"):
        expected, actual = session.get("csrf_token", ""), request.form.get("csrf_token", "")
        if not expected or not hmac.compare_digest(expected.encode(), actual.encode()):
            raise AppError(400, "CSRF_FAILED", "화면을 새로고침한 뒤 다시 시도해 주세요.")


@bp.app_context_processor
def context():
    return {"csrf_token": csrf_token, "company_name": current_app.config["COMPANY_NAME"]}


@bp.route("/login", methods=["GET", "POST"])
def login():
    if request.method == "GET":
        if g.user:
            return redirect(url_for("web.home"))
        return render_template("login.html")
    try:
        user, token, auth_session = authenticate(request.form.get("login_id", ""), request.form.get("password", ""), "web")
    except AppError as exc:
        return render_template("login.html", error=exc.message), exc.status
    session.clear()  # Prevent fixation and rotate CSRF after login.
    csrf_token()
    response = redirect(url_for("web.home"))
    response.set_cookie("rw_session", token, httponly=True, secure=current_app.config["COOKIE_SECURE"],
                        samesite="Lax", max_age=int(current_app.config["SESSION_HOURS"] * 3600), path="/")
    return response


@bp.post("/logout")
def sign_out():
    if g.user:
        logout()
    session.clear()
    response = redirect(url_for("web.login"))
    response.delete_cookie("rw_session", path="/", secure=current_app.config["COOKIE_SECURE"], samesite="Lax")
    return response


@bp.get("/")
@login_required
def home():
    return render_template("home.html")


@bp.get("/users")
@login_required
def users():
    if g.user.role != "admin":
        raise AppError(403, "FORBIDDEN", "관리자만 사용자 목록을 볼 수 있습니다.")
    # Read-only shared user view. Account changes use the admin API / desktop UI.
    rows = get_db().scalars(select(User).order_by(User.id).limit(200)).all()
    return render_template("users.html", users=rows)


@bp.get("/features/<name>")
@login_required
def feature(name):
    if name == "tasks" and "task_web.index" in current_app.view_functions:
        return redirect(url_for("task_web.index"))
    if name == "notices":
        if "notice_web.index" not in current_app.view_functions:
            raise AppError(503, "NOTICE_DISABLED", "공지사항 기능이 준비되지 않았습니다. 서버의 공지 활성화 설정을 확인해 주세요.")
        return redirect(url_for("notice_web.index"))
    features = {"tasks": ("업무", "팀원 2"), "results": ("결과물", "팀원 3"),
                "notices": ("공지", "팀원 4"), "chat": ("채팅", "팀원 4")}
    if name not in features:
        raise AppError(404, "NOT_FOUND", "화면을 찾을 수 없습니다.")
    title, owner = features[name]
    return render_template("feature.html", title=title, owner=owner)
