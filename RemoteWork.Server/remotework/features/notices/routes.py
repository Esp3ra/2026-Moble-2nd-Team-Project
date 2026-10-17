import json
from pathlib import PurePosixPath, PureWindowsPath
import click
from flask import Blueprint, current_app, request
from sqlalchemy import func, inspect, select
from ...auth import admin_transaction, login_required
from ...common import AppError, audit, payload, require_role, success
from ...db import get_db
from flask import g
from .models import Notice, TARGETS

bp = Blueprint("notices", __name__, url_prefix="/api/v1/notices")


@bp.cli.command("init-table")
def init_table():
    """Explicitly create ONLY the missing notice table; preserve existing rows."""
    engine = current_app.extensions["db_engine"]
    if not inspect(engine).has_table("users"):
        raise click.ClickException("먼저 공통 init-db로 users 테이블을 준비해 주세요.")
    Notice.__table__.create(engine, checkfirst=True)
    click.echo("공지사항 테이블 준비 완료 (기존 데이터 보존)")


def require_table():
    # No DDL during a request. Return an actionable error for an older database.
    if not inspect(current_app.extensions["db_engine"]).has_table("notices"):
        raise AppError(503, "NOTICE_SCHEMA_REQUIRED",
                       "공지사항 테이블이 없습니다. 서버에서 flask --app remotework notices init-table을 실행해 주세요.")


def get_notice(notice_id):
    notice = get_db().get(Notice, notice_id)
    if notice is None:
        raise AppError(404, "NOTICE_NOT_FOUND", "삭제되었거나 존재하지 않는 공지사항입니다.")
    return notice


@bp.get("")
@login_required
def list_notices():
    require_table()
    target = request.args.get("target")
    if target is not None and target not in TARGETS:
        raise AppError(400, "INVALID_INPUT", "공지사항 대상을 확인해 주세요.")
    try:
        page = int(request.args.get("page", "1"))
        limit = int(request.args.get("limit", "100"))
        if not 1 <= page <= 1000000 or not 1 <= limit <= 100:
            raise ValueError()
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "페이지와 조회 개수를 확인해 주세요.")
    query = select(Notice)
    if target:
        query = query.where(Notice.target == target)
    session = get_db()
    total = session.scalar(select(func.count()).select_from(query.subquery()))
    items = session.scalars(query.order_by(Notice.is_pinned.desc(), Notice.created_at.desc(),
                                          Notice.id.desc()).offset((page - 1) * limit).limit(limit))
    # User decision: all authenticated employees can read every department.
    return success({"items": [n.public() for n in items], "page": page, "limit": limit, "total": total})


@bp.get("/<int:notice_id>")
@login_required
def detail(notice_id):
    require_table()
    return success({"notice": get_notice(notice_id).public()})


def validate(body):
    allowed = {"title", "content", "target", "is_pinned", "attachment_names"}
    if set(body) - allowed:
        raise AppError(400, "INVALID_INPUT", "지원하지 않는 공지사항 입력 항목입니다.")
    for key, maximum in (("title", 200), ("content", 5000)):
        value = body.get(key)
        if not isinstance(value, str) or not value.strip() or len(value.strip()) > maximum:
            raise AppError(400, "INVALID_INPUT", f"제목은 200자, 내용은 5000자 이내로 입력해 주세요.")
    if body.get("target") not in TARGETS or type(body.get("is_pinned", False)) is not bool:
        raise AppError(400, "INVALID_INPUT", "공지사항 대상과 고정 여부를 확인해 주세요.")
    names = body.get("attachment_names", [])
    if not isinstance(names, list) or len(names) > 20:
        raise AppError(400, "INVALID_INPUT", "첨부파일 이름은 20개까지 저장할 수 있습니다.")
    for name in names:
        if (not isinstance(name, str) or not name.strip() or len(name) > 255
                or name in (".", "..") or ":" in name
                or any(ord(c) < 32 for c in name)
                or PureWindowsPath(name).name != name or PurePosixPath(name).name != name):
            raise AppError(400, "INVALID_INPUT", "첨부파일은 경로 없이 파일명만 전달해 주세요.")
    return names


@bp.post("")
@require_role("admin")
def create_notice():
    require_table()
    body = payload()
    names = validate(body)
    # Reuse the common guard to recheck role/session before an administrator write.
    with admin_transaction() as session:
        notice = Notice(title=body["title"].strip(), content=body["content"].strip(),
                        target=body["target"], is_pinned=body.get("is_pinned", False),
                        author_id=g.user.id, author=g.user.display_name,
                        attachment_names_json=json.dumps(names, ensure_ascii=False))
        session.add(notice)
        session.flush()
        audit("notice.created", "notice", notice.id, {"target": notice.target})
        result = notice.public()
    return success({"notice": result}, 201)


@bp.post("/<int:notice_id>/delete")
@require_role("admin")
def delete_notice(notice_id):
    require_table()
    with admin_transaction() as session:
        notice = get_notice(notice_id)
        session.delete(notice)
        audit("notice.deleted", "notice", notice_id)
    return success({"message": "공지사항을 삭제했습니다."})
