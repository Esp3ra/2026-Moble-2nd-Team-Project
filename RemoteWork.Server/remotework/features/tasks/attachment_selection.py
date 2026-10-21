"""Member 2's agreed optional final attachment list, without changing W1 input."""
import uuid

from ...common import AppError


def validate_attachment_selection(body):
    if "attachment_ids" not in body:
        return
    ids = body["attachment_ids"]
    if not isinstance(ids, list) or len(ids) > 10:
        raise AppError(400, "INVALID_INPUT", "첨부 ID는 최대 10개의 목록으로 전달해 주세요.")
    seen = set()
    for value in ids:
        try:
            valid = isinstance(value, str) and str(uuid.UUID(value)) == value
        except (ValueError, AttributeError):
            valid = False
        if not valid or value in seen:
            raise AppError(400, "INVALID_INPUT", "첨부 ID는 중복 없는 정규 소문자 UUID여야 합니다.")
        seen.add(value)
    # Do not sort, deduplicate, replace omission with [] or modify the caller's
    # body: the confirmed values are the common request fingerprint.
