"""User-approved invitation directory and server-persisted chat."""
from flask import Blueprint, g, request
from sqlalchemy import func, select

from ...auth import login_required
from ...common import AppError, success
from ...db import get_db
from ...models import User

bp = Blueprint("chat_directory", __name__, url_prefix="/api/v1/chat")


@bp.get("/participants")
@login_required
def participants():
    if set(request.args) - {"page", "limit"} or any(len(values) != 1 for _, values in request.args.lists()):
        raise AppError(400, "INVALID_INPUT", "참여자 조회 조건을 확인해 주세요.")
    try:
        page = int(request.args.get("page", "1"))
        limit = int(request.args.get("limit", "100"))
        if not 1 <= page <= 1_000_000 or not 1 <= limit <= 100:
            raise ValueError()
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "페이지 번호를 확인해 주세요.") from None
    conditions = (User.is_active.is_(True), User.id != g.user.id,
                  User.role.in_(("employee", "admin")))
    db = get_db()
    total = db.scalar(select(func.count()).select_from(User).where(*conditions))
    rows = db.execute(select(User.id, User.display_name, User.department).where(*conditions)
                      .order_by(User.display_name, User.id).offset((page - 1) * limit).limit(limit))
    return success({"items": [{"id": row.id, "display_name": row.display_name,
                               "department": row.department} for row in rows],
                    "page": page, "limit": limit, "total": total})


from .storage import register_storage
register_storage(bp)
