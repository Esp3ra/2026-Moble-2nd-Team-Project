"""Owner 2 consumers of the optional member 3 cookie attachment contract."""
import hmac
import json
import re

from flask import current_app, request, url_for

from ...common import AppError
from .attachment_selection import validate_attachment_selection

FORM_FIELDS = {"attachment_ids", "attachment_scope"}


def _scope():
    provider = current_app.extensions.get("results_task_web")
    if not isinstance(provider, dict) or not callable(provider.get("session_scope")):
        return None
    value = provider["session_scope"]()
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{64}", value):
        raise AppError(503, "TASK_ATTACHMENT_PROVIDER_REQUIRED", "첨부 연결을 확인해 주세요.")
    return value


def form_selection():
    """Preserve omission and order; bind even a selected empty list to its login."""
    supplied = FORM_FIELDS.intersection(request.form)
    if not supplied:
        return {}
    scope = _scope()
    if scope is None:
        raise AppError(503, "TASK_ATTACHMENT_PROVIDER_REQUIRED", "현재 첨부 기능을 사용할 수 없습니다.")
    captured = request.form.get("attachment_scope", "")
    if not isinstance(captured, str) or not hmac.compare_digest(scope.encode(), captured.encode()):
        raise AppError(409, "RESULTS_SESSION_CHANGED", "로그인 접속이 변경되었습니다. 업무 화면을 다시 열어 주세요.")
    if "attachment_ids" not in request.form:
        return {}
    raw = request.form["attachment_ids"]
    if len(raw) > 1024:
        raise AppError(400, "INVALID_INPUT", "첨부 목록 형식을 확인해 주세요.")
    try:
        body = {"attachment_ids": json.loads(raw)}
    except (ValueError, TypeError):
        raise AppError(400, "INVALID_INPUT", "첨부 목록 형식을 확인해 주세요.") from None
    validate_attachment_selection(body)
    return body


def page_config(task_id=None, *, mode="create", attachment_ids=None, explicit=False):
    scope = _scope()
    if scope is None:
        return None
    target = None if task_id is None else str(task_id)
    urls = {"start": url_for("results_web.shared_start"),
        "chunk": url_for("results_web.shared_write_chunk", attachment_id="__id__"),
        "complete": url_for("results_web.shared_complete", attachment_id="__id__"),
        "discard": url_for("results_web.shared_discard", attachment_id="__id__"),
        "list": None if target is None else url_for("results_web.shared_task_files", task_id=target),
        "download": None if target is None else url_for("results_web.shared_chunks", task_id=target, attachment_id="__id__")}
    return {"mode": mode, "taskId": target, "session": scope,
            "ids": attachment_ids if explicit else None, "explicit": explicit, "urls": urls}
