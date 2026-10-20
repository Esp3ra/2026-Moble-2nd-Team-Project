"""Existing local popup behavior, registered only by a separate explicit opt-in."""
import click
from flask import Blueprint, current_app, g, request
from sqlalchemy import exists, inspect, or_, select
from ...common import AppError, audit, payload, require_role, success
from ...db import get_db
from ...auth import authenticated_transaction
from .models import Notice
from .popup_models import NoticeConfirmation
from .routes import require_table

bp = Blueprint("notice_popups", __name__, url_prefix="/api/v1/notices", cli_group="notice-popups")


def popup_notice(notice):
    return notice.public() | {"popup_key": f"{notice.id}:{notice.created_at.isoformat(timespec='microseconds')}"}


@bp.cli.command("init-table")
def init_popup_table():
    from .popup_schema import prepare_popup_schema
    try:
        prepare_popup_schema(current_app.extensions["db_engine"])
    except RuntimeError as exc:
        raise click.ClickException(str(exc)) from exc
    click.echo("팝업 확인 테이블 준비 완료 (기존 공지/확인 기록 보존, 로컬 검토안)")


def require_popup_table():
    require_table()
    if not inspect(current_app.extensions["db_engine"]).has_table("notice_confirmations"):
        raise AppError(503, "NOTICE_POPUP_SCHEMA_REQUIRED", "공지 팝업 확인 기록 테이블을 준비해 주세요.")



@bp.get("/popups/pending")
@require_role("employee")
def pending_popups():
    require_popup_table()
    try:
        after_id = int(request.args.get("after_id", "0"))
        if not 0 <= after_id <= 2147483647:
            raise ValueError()
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "공지 팝업 조회 위치를 확인해 주세요.")
    confirmed = exists().where(NoticeConfirmation.notice_id == Notice.id,
                               NoticeConfirmation.user_id == g.user.id)
    items = list(get_db().scalars(select(Notice).where(
        Notice.id > after_id, Notice.is_pinned.is_(True), ~confirmed,
        or_(Notice.target == "전 직원", Notice.target == g.user.department)
    ).order_by(Notice.id).limit(20)))
    return success({"items": [popup_notice(n) for n in items],
                    "next_after_id": items[-1].id if len(items) == 20 else None})



@bp.post("/<int:notice_id>/confirm")
@require_role("employee")
def confirm_popup(notice_id):
    require_popup_table()
    body = payload()
    if set(body) != {"popup_key"} or not isinstance(body["popup_key"], str):
        raise AppError(400, "INVALID_INPUT", "확인할 공지 팝업 정보를 전달해 주세요.")
    with authenticated_transaction(roles=("employee",)) as session:
        # Serialize acknowledgements of the same notice on MySQL; the PK also prevents duplicates.
        notice = session.scalar(select(Notice).where(Notice.id == notice_id).with_for_update()
                                .execution_options(populate_existing=True))
        if notice is None:
            raise AppError(404, "NOTICE_NOT_FOUND", "삭제되었거나 존재하지 않는 공지사항입니다.")
        if not notice.is_pinned or notice.target not in ("전 직원", g.user.department):
            raise AppError(403, "FORBIDDEN", "본인에게 전달된 중요 공지만 확인할 수 있습니다.")
        if body["popup_key"] != popup_notice(notice)["popup_key"]:
            raise AppError(409, "NOTICE_CHANGED", "공지사항이 변경되었습니다. 팝업을 닫고 다시 확인해 주세요.")
        confirmation = session.scalar(select(NoticeConfirmation).where(
            NoticeConfirmation.notice_id == notice_id, NoticeConfirmation.user_id == g.user.id
        ).with_for_update())
        if confirmation is None:
            session.add(NoticeConfirmation(notice_id=notice_id, user_id=g.user.id))
            audit("notice.confirmed", "notice", notice_id)
    return success({"message": "공지사항 확인을 저장했습니다."})
