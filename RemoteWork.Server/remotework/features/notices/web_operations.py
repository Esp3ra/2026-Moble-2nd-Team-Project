"""Cookie/CSRF notice recovery; signed original context, explicit check/retry."""
import hmac
import json
import uuid

from flask import current_app, flash, g, redirect, request, url_for
from itsdangerous import BadData, URLSafeSerializer
from sqlalchemy.exc import SQLAlchemyError

from ...auth import admin_transaction
from ...common import AppError
from ...db import get_db
from ...write_requests import issue_key, lookup_result
from ..results_web import session_scope
from . import operations
from .routes import create_notice_record, validate


def signer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"], salt="notice-original-form-v1")


def encode(context):
    return signer().dumps(context)


def decode(token):
    try:
        if not isinstance(token, str) or not 1 <= len(token) <= 28000:
            raise ValueError()
        value = signer().loads(token)
        if (type(value) is not dict or set(value) != {"actor", "session", "operation_id", "key", "stage", "body"}
                or value["actor"] != g.user.id or not hmac.compare_digest(value["session"], session_scope())
                or value["stage"] not in ("prepared", "unknown", "ready")):
            raise ValueError()
        operations.canonical(value["operation_id"])
        return value
    except (BadData, ValueError, TypeError, KeyError):
        raise AppError(403, "NOTICE_SESSION_CHANGED", "원 공지 요청과 현재 로그인 화면이 다릅니다. 원래 계정의 저장 내역을 확인해 주세요.") from None


def values_from(body):
    return dict(title=body.get("title", ""), content=body.get("content", ""), target=body.get("target", ""),
        is_pinned=body.get("is_pinned", False), attachment_names="\n".join(body.get("attachment_names", [])),
        attachment_ids=json.dumps(body.get("attachment_ids", [])))


def submitted(context):
    if context["body"] is not None:
        return context["body"]
    try:
        attachments = json.loads(request.form.get("attachment_ids", "[]"))
    except ValueError:
        raise AppError(400, "INVALID_INPUT", "첨부 목록을 확인해 주세요.") from None
    if not hmac.compare_digest(request.form.get("attachment_session", ""), session_scope()):
        raise AppError(403, "NOTICE_SESSION_CHANGED", "로그인이 변경되었습니다. 원 공지 작성 화면을 사용해 주세요.")
    return dict(operation_id=context["operation_id"],
        title=request.form.get("title", ""), content=request.form.get("content", ""),
        target=request.form.get("target", ""), is_pinned="is_pinned" in request.form,
        attachment_ids=attachments,
        attachment_names=[n.strip() for n in request.form.get("attachment_names", "").splitlines() if n.strip()])


def completed(result):
    flash(f"공지사항 {result['notice_id']}번의 원 등록 성공을 확인했습니다. 이후 삭제되었을 수 있으므로 현재 목록과 구분합니다.", "notice")
    return redirect(url_for("notice_web.index", choice=result["notice_id"]), code=303)


def handle(render):
    if request.method == "GET":
        identifier = str(uuid.uuid4())
        try:
            with admin_transaction() as db:
                operations.authorize(db, identifier)
                issued = issue_key(action=operations.ACTION, target=identifier)
            context = dict(actor=g.user.id, session=session_scope(), operation_id=identifier,
                key=issued["request_key"], stage="prepared", body=None)
            return render(operation_context=encode(context))
        except AppError as exc:
            if exc.status in (401, 403):
                raise
            return render(error=exc.message, status=exc.status, operation_unavailable=True)
    if request.files or any(len(request.form.getlist(name)) != 1 for name in request.form):
        raise AppError(400, "INVALID_INPUT", "중복 없는 작성 항목과 전송 완료한 첨부 ID만 전달해 주세요.")
    context = decode(request.form.get("operation_context", ""))
    body = submitted(context)
    action = request.form.get("notice_action", "publish")
    if action not in ("publish", "check"):
        raise AppError(400, "INVALID_INPUT", "원 요청의 확인 또는 실행을 선택해 주세요.")
    try:
        validate({k: v for k, v in body.items() if k != "operation_id"})
    except AppError as exc:
        return render(values=values_from(body), error=exc.message, status=exc.status,
            operation_context=encode(context), operation_locked=context["body"] is not None)
    first_body = context["body"] is None
    context["body"] = body
    # Keep a later frozen multipart recovery inside the unchanged common 32KiB
    # request cap. Reject before mutation; never create an unrecoverable draft.
    if first_body and len(encode(context)) > 26000:
        context["body"] = None
        return render(values=values_from(body), error="원 요청 복구 정보를 포함한 요청은32KiB 이하여야 합니다. 내용·파일명 입력을 줄여 주세요.",
            status=400, operation_context=encode(context))
    try:
        if action == "check":
            found = operations.lookup(context["operation_id"])
            if found["state"] == "completed":
                return completed(found["result"])
            with admin_transaction() as db:
                common = lookup_result(db, key=context["key"], action=operations.ACTION,
                    target=context["operation_id"], authorize=operations.authorize)
            if common["state"] == "completed":
                return completed(common["result"])
            context["stage"] = "ready"
            return render(values=values_from(body), error="원 요청의 미저장을 확인했습니다. 같은 내용·파일·키로 다시 등록을 명시 실행할 수 있습니다.",
                operation_context=encode(context), operation_locked=True, operation_can_retry=True)
        if context["stage"] not in ("prepared", "ready"):
            raise AppError(409, "NOTICE_RESULT_CHECK_REQUIRED", "원 요청의 처리 결과를 먼저 확인해 주세요.")
        return completed(create_notice_record(body, request_key=context["key"]))
    except AppError as exc:
        if exc.status in (401, 403):
            raise
        error, status = exc.message, exc.status
    except SQLAlchemyError:
        get_db().rollback()
        error, status = "저장 응답을 확인하지 못했습니다. 원 요청의 처리 결과를 먼저 확인해 주세요.", 503
    context["stage"] = "unknown"
    return render(values=values_from(body), error=error, status=status,
        operation_context=encode(context), operation_locked=True)
