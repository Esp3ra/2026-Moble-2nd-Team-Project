"""Cookie-authenticated notice UI; shares notice operations with the API."""
from datetime import datetime, timedelta, timezone
from flask import Blueprint, flash, g, redirect, render_template, request, url_for
from sqlalchemy.exc import SQLAlchemyError
from ...auth import login_required
from ...common import AppError, require_role
from ...db import get_db
from .models import TARGETS
from .routes import create_notice_record, delete_notice_record, get_notice, query_notices, require_table

bp = Blueprint("notice_web", __name__, url_prefix="/notices")


@bp.app_template_filter("notice_date")
def notice_date(value, detailed=False):
    if isinstance(value, str):
        value = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if value.tzinfo is None:
        value = value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone(timedelta(hours=9))).strftime("%Y.%m.%d %H:%M" if detailed else "%Y.%m.%d")


def number(value, label):
    try:
        if not value.isascii() or not value.isdecimal():
            raise ValueError()
        return int(value)
    except (ValueError, AttributeError):
        raise AppError(400, "INVALID_INPUT", f"{label}을 확인해 주세요.") from None


@bp.get("")
@login_required
def index():
    target = request.args.get("target") or None
    page = number(request.args.get("page", "1"), "페이지")
    sort, direction = request.args.get("sort", "created_at"), request.args.get("direction", "desc")
    data = query_notices(target, page, 20, sort=sort, direction=direction)
    selected = None
    chosen_id = data["items"][0]["id"] if data["items"] else None
    # Selecting a row (choice) never loads detail. Only the query form sends selected.
    wanted = request.args.get("selected") or request.args.get("choice")
    if wanted:
        wanted_id = number(wanted, "공지사항 ID")
        if not 1 <= wanted_id <= 2147483647:
            raise AppError(404, "NOTICE_NOT_FOUND", "삭제되었거나 존재하지 않는 공지사항입니다.")
        if any(n["id"] == wanted_id for n in data["items"]):
            chosen_id = wanted_id
    if request.args.get("selected") and chosen_id is not None:
        selected = get_notice(chosen_id).public()
    return render_template("notices/index.html", data=data, selected=selected, chosen_id=chosen_id, targets=TARGETS,
                           target=target or "", sort=sort, direction=direction,
                           pages=max(1, (data["total"] + 19) // 20))


def form(values=None, error=None, status=200):
    return render_template("notices/form.html", targets=TARGETS, values=values or {}, error=error), status


@bp.route("/new", methods=["GET", "POST"])
@require_role("admin")
def create():
    require_table()
    if request.method == "GET":
        return form()
    if request.files:
        raise AppError(400, "INVALID_INPUT", "실제 파일 업로드는 지원하지 않습니다. 파일명만 입력해 주세요.")
    values = {k: request.form.get(k, "") for k in ("title", "content", "target", "attachment_names")}
    values["is_pinned"] = "is_pinned" in request.form
    body = {**values, "attachment_names": [n.strip() for n in values["attachment_names"].splitlines() if n.strip()]}
    try:
        saved = create_notice_record(body)
    except AppError as exc:
        if exc.status in (401, 403):
            raise
        return form(values, exc.message, exc.status)
    except SQLAlchemyError:
        get_db().rollback()
        return form(values, "저장 결과를 확인하지 못했습니다. 재등록하기 전에 공지 목록을 확인해 주세요. 입력 내용은 유지됩니다.", 503)
    flash("공지사항을 등록했습니다.", "notice")
    return redirect(url_for("notice_web.index", choice=saved["id"]), code=303)


@bp.route("/<int:notice_id>/delete", methods=["GET", "POST"])
@require_role("admin")
def delete(notice_id):
    require_table()
    if request.method == "GET":
        return render_template("notices/delete.html", notice=get_notice(notice_id).public())
    if request.files:
        raise AppError(400, "INVALID_INPUT", "실제 파일 업로드는 지원하지 않습니다.")
    delete_notice_record(notice_id)
    flash("공지사항을 삭제했습니다.", "notice")
    return redirect(url_for("notice_web.index"), code=303)
