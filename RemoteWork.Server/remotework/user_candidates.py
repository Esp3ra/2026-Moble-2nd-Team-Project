"""Minimal user selection service; the feature supplies server-side authority.

No route is opened and no task creation/reassignment policy is implied here.
Call from authenticated_transaction with a current relationship authorizer.
"""
from dataclasses import dataclass
from sqlalchemy import or_, select
from flask import g

from .common import AppError
from .models import User
from .write_contract import positive_integer


@dataclass(frozen=True)
class CandidateScope:
    # None explicitly means the authorizer allowed the whole organization.
    user_ids: frozenset[int] | None


def list_user_candidates(db, *, authorize, kind, q="", page=1, limit=20):
    if not getattr(g, "write_transaction_active", False):
        raise RuntimeError("Candidate queries require authenticated_transaction")
    scope = authorize(db)
    if not isinstance(scope, CandidateScope):
        raise AppError(403, "FORBIDDEN", "사용자 선택 권한이 없습니다.")
    if (kind not in ("assignment", "manager") or type(page) is not int or not 1 <= page <= 1_000_000
            or type(limit) is not int or not 1 <= limit <= 100
            or not isinstance(q, str) or len(q) > 80):
        raise AppError(400, "INVALID_INPUT", "후보 조회 조건을 확인해 주세요.")
    conditions = [User.is_active.is_(True),
                  User.role.in_(("employee", "admin") if kind == "assignment" else ("admin",))]
    if scope.user_ids is not None:
        ids = [positive_integer(value, "user_id") for value in scope.user_ids]
        conditions.append(User.id.in_(ids))
    if q.strip():
        conditions.append(or_(User.display_name.contains(q.strip(), autoescape=True),
                              User.department.contains(q.strip(), autoescape=True)))
    # This read occurs after the common current-identity guard. It does not
    # promise a stable multi-page snapshot or final permission to save an ID.
    # A plain COUNT can see load_identity's earlier RR snapshot. Derive total
    # from current locking IDs instead, so items/total use the same eligibility.
    eligible_ids = db.scalars(select(User.id).where(*conditions).order_by(User.id).with_for_update()).all()
    rows = db.scalars(select(User).where(User.id.in_(eligible_ids)).order_by(User.display_name, User.id)
                      .offset((page - 1) * limit).limit(limit)
                      .with_for_update().execution_options(populate_existing=True)).all()
    items = [{"id": user.id, "display_name": user.display_name,
              "selection_label": f'{user.display_name} · {user.department} · '
                                 f'{"관리자" if user.role == "admin" else "직원"} · #{user.id}'}
             for user in rows]
    return {"items": items, "page": page, "limit": limit, "total": len(eligible_ids)}
