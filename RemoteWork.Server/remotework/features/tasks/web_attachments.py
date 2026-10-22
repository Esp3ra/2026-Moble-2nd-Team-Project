"""Owner 2 consumers of the optional member 3 cookie attachment contract."""
import hmac
import json
import re

from flask import current_app, request, url_for

from ...common import AppError
from .attachment_selection import validate_attachment_selection

FORM_FIELDS = {"attachment_ids", "attachment_scope", "attachment_display"}


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


def form_display(selection, task_id=None, *, mode="create"):
    """Copy bounded, untrusted presentation hints; never authorize or write them.

    The caller checks the existing login scope with form_selection first. These
    hints describe a previous screen, not current ownership/readiness/linkage.
    """
    raw = request.form.get("attachment_display")
    if raw is None:
        return []
    target = None if task_id is None else str(task_id)

    def invalid():
        return AppError(400, "INVALID_INPUT", "첨부 표시 정보를 확인해 주세요. 업무 화면을 다시 열어 주세요.")

    def unique_object(pairs):
        value = dict(pairs)
        if len(value) != len(pairs):
            raise ValueError("duplicate presentation key")
        return value

    try:
        if len(raw.encode("utf-8")) > 16384:
            raise ValueError("presentation size")
        value = json.loads(raw, object_pairs_hook=unique_object)
        if (not isinstance(value, dict) or set(value) != {"mode", "task_id", "items"}
                or value["mode"] != mode or value["task_id"] != target
                or not isinstance(value["items"], list) or len(value["items"]) > 10):
            raise ValueError("presentation context")
        selected = set(selection.get("attachment_ids", []))
        seen, items = set(), []
        for item in value["items"]:
            if (not isinstance(item, dict) or set(item) != {
                    "attachment_id", "name", "size", "sha256", "temporary"}
                    or not isinstance(item["attachment_id"], str)
                    or item["attachment_id"] not in selected or item["attachment_id"] in seen
                    or not isinstance(item["name"], str) or not 1 <= len(item["name"]) <= 255
                    or "\x00" in item["name"]
                    or type(item["size"]) is not int or not 0 <= item["size"] <= 25 * 1024 * 1024
                    or not isinstance(item["sha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", item["sha256"])
                    or type(item["temporary"]) is not bool):
                raise ValueError("presentation item")
            item["name"].encode("utf-8")
            seen.add(item["attachment_id"])
            items.append(dict(item))
        return items
    except (ValueError, TypeError, UnicodeError, RecursionError):
        raise invalid() from None


def page_config(task_id=None, *, mode="create", attachment_ids=None, explicit=False, display=None):
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
            "ids": attachment_ids if explicit else None, "explicit": explicit,
            "display": display if explicit and display is not None else [], "urls": urls}
