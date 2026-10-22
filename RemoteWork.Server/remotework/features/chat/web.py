"""Cookie/CSRF adapter for the existing chat operations; no Bearer bridge."""
import hashlib
import hmac
import json

from flask import Blueprint, current_app, g, jsonify, render_template, request
from sqlalchemy.exc import SQLAlchemyError

from ...auth import login_required
from ...common import AppError
from ...db import get_db
from . import storage
from .routes import participants

bp = Blueprint("chat_web", __name__, url_prefix="/chat")


def page_session():
    return hmac.new(current_app.config["SECRET_KEY"].encode(),
                    ("chat-web:" + g.auth_session.token_hash).encode(), hashlib.sha256).hexdigest()


@bp.before_request
@login_required
def current_page():
    if request.endpoint != "chat_web.index":
        supplied = request.headers.get("X-Chat-Session", "")
        if not hmac.compare_digest(supplied.encode(), page_session().encode()):
            raise AppError(403, "CHAT_SESSION_CHANGED", "로그인이 변경되었습니다. 채팅 화면을 다시 열어 주세요.")


@bp.errorhandler(AppError)
def app_error(exc):
    if request.endpoint == "chat_web.index":
        if exc.status == 401:
            from flask import redirect, url_for
            return redirect(url_for("web.login"))
        return render_template("error.html", status=exc.status, message=exc.message), exc.status
    return jsonify(ok=False, error={"code": exc.code, "message": exc.message}, request_id=g.request_id), exc.status


@bp.errorhandler(SQLAlchemyError)
def database_error(exc):
    get_db().rollback()
    current_app.logger.error("chat_web_database_error request_id=%s type=%s", g.request_id, type(exc).__name__)
    return app_error(AppError(503, "DATABASE_UNAVAILABLE", "데이터베이스 상태를 확인해 주세요. 전송 결과는 다시 조회해 주세요."))


def form_payload():
    # protect_forms() has already checked the standard form CSRF token.
    if (request.files or set(request.form) != {"csrf_token", "payload"}
            or any(len(v) != 1 for _, v in request.form.lists())):
        raise AppError(400, "INVALID_INPUT", "채팅 입력 항목을 확인해 주세요.")
    try:
        value = json.loads(request.form["payload"])
    except (ValueError, TypeError):
        raise AppError(400, "INVALID_INPUT", "채팅 입력 형식을 확인해 주세요.") from None
    if not isinstance(value, dict):
        raise AppError(400, "INVALID_INPUT", "채팅 입력 형식을 확인해 주세요.")
    return value


@bp.get("")
def index():
    from ..results_web import session_scope
    return render_template("chat/index.html", chat_session=page_session(), attachment_session=session_scope())


@bp.get("/data/participants")
def directory():
    return participants()


@bp.route("/data/rooms", methods=["GET", "POST"])
def rooms():
    return storage.rooms() if request.method == "GET" else storage.create_room(form_payload())


@bp.route("/data/rooms/<room_id>/messages", methods=["GET", "POST"])
def messages(room_id):
    return storage.messages(room_id) if request.method == "GET" else storage.send_message(room_id, form_payload())


@bp.post("/data/rooms/<room_id>/hide")
def hide(room_id):
    return storage.hide_room(room_id, form_payload())
