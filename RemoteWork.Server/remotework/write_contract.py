"""W1 primitives only; feature services own authorization and business state."""
import re
from dataclasses import dataclass

from sqlalchemy import select

from .common import AppError
from .models import User

BIGINT_MAX = 9_223_372_036_854_775_807
INTEGER_MAX = 2_147_483_647


def positive_bigint(value, field):
    if (not isinstance(value, str) or len(value) > 19
            or re.fullmatch(r"[1-9][0-9]*", value) is None or int(value) > BIGINT_MAX):
        raise AppError(400, "INVALID_INPUT", f"{field}: 양의 BIGINT 10진 문자열이 필요합니다.")
    return int(value)


def positive_integer(value, field):
    if type(value) is not int or not 1 <= value <= INTEGER_MAX:
        raise AppError(400, "INVALID_INPUT", f"{field}: 양의 INTEGER 정수가 필요합니다.")
    return value


@dataclass(frozen=True)
class ExpectedVersions:
    row_version: int
    assignment_revision: int

    @classmethod
    def from_body(cls, body):
        if not isinstance(body, dict):
            raise AppError(400, "INVALID_INPUT", "JSON 객체가 필요합니다.")
        return cls(positive_bigint(body.get("expected_row_version"), "expected_row_version"),
                   positive_integer(body.get("expected_assignment_revision"), "expected_assignment_revision"))

    def check(self, *, row_version, assignment_revision):
        """Call AFTER current authorization with a locked feature parent row."""
        if self.assignment_revision != assignment_revision:
            raise AppError(409, "TASK_ASSIGNMENT_CHANGED", "배정이 변경되었습니다. 다시 조회해 주세요.")
        if self.row_version != row_version:
            raise AppError(409, "TASK_VERSION_CONFLICT", "업무가 변경되었습니다. 다시 조회해 주세요.")


def lock_users(db, user_ids):
    """Within authenticated_transaction: lock target users in ascending ID order.

    Returns only existing rows; the feature must reject missing/inactive or
    otherwise ineligible candidates. Names are never resolved into identifiers.
    """
    from flask import g
    if not getattr(g, "write_transaction_active", False):
        raise RuntimeError("lock_users requires authenticated_transaction")
    ids = sorted({positive_integer(value, "user_id") for value in user_ids})
    if not ids:
        return {}
    rows = db.scalars(select(User).where(User.id.in_(ids)).order_by(User.id)
                      .with_for_update().execution_options(populate_existing=True))
    return {row.id: row for row in rows}
