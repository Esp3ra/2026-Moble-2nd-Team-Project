"""Opt-in W1 receipts. Business authorizers/mutations belong to feature owners.

Keys are server-signed and bind actor/action/target/expiry. Expired keys cannot
execute again, even after their receipt is removed. No automatic retries.
"""
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import hashlib
import json
import re
import secrets
import time

from flask import Blueprint, current_app, g, request
from itsdangerous import BadData, URLSafeSerializer
from sqlalchemy import DateTime, ForeignKey, Index, Integer, String, Text, inspect, select
from sqlalchemy.orm import Mapped, mapped_column

from .auth import authenticated_transaction, login_required
from .common import AppError, payload, success
from .models import Base, iso, utcnow

bp = Blueprint("write_requests", __name__, url_prefix="/api/v1/write-requests")
ACTIONS = frozenset(("task.created", "task.updated", "task.reassigned", "task.managers_changed",
    "task.accepted", "task.deadline_extension_requested", "task.deadline_extension_approved",
    "task.deadline_extension_rejected"))
RESULT_FIELDS = frozenset(("task_id", "row_version", "assignment_revision", "task_status", "updated_at",
    "extension_id", "requested_due_date", "due_date"))


@dataclass(frozen=True)
class WriteAction:
    authorize: object
    result_fields: frozenset[str]


@dataclass(frozen=True)
class ResultReader:
    authorize: object
    read_only: bool


def register_write_result_reader(app, *, action, authorize, read_only=False):
    """Explicit current-access reader; never grants mutation or creates storage.

    Owners may retain this contract in a separately opted-in read-only app
    after disabling their business routes. Existing writers are unchanged.
    """
    if app._got_first_request:
        raise RuntimeError("Register result readers during application setup")
    if (not isinstance(action, str) or len(action) > 80 or
            re.fullmatch(r"[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*", action) is None or
            not callable(authorize) or type(read_only) is not bool):
        raise ValueError("An explicit action and current-access result authorizer are required")
    readers = app.extensions.setdefault("write_result_readers", {})
    if action in readers:
        raise ValueError("A result reader cannot replace an existing contract")
    readers[action] = ResultReader(authorize, read_only)


def register_write_action(app, *, action, authorize, result_fields):
    """Feature-owned startup contract; never installs a business route or grants access.

    Register only after agreeing the feature's authorization and minimal result
    schema. No raw request body, file, secret or explanation belongs in a receipt.
    Contracts are per app and immutable after the first request.
    """
    if app._got_first_request:
        raise RuntimeError("Register write actions during application setup")
    if (not isinstance(action, str) or len(action) > 80 or
            re.fullmatch(r"[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*", action) is None or
            action in ACTIONS or not callable(authorize)):
        raise ValueError("An explicit new feature action and current-access authorizer are required")
    if not isinstance(result_fields, (set, frozenset, list, tuple)) or not result_fields:
        raise ValueError("Explicit minimal result fields are required")
    if any(not isinstance(field, str) or len(field) > 64 or
           re.fullmatch(r"[a-z][a-z0-9_]*", field) is None for field in result_fields):
        raise ValueError("Use explicit snake_case result field names")
    contracts = app.extensions.setdefault("write_action_contracts", {})
    if action in contracts or action in app.extensions.get("write_authorizers", {}):
        raise ValueError("A write action cannot replace an existing contract")
    contracts[action] = WriteAction(authorize, frozenset(result_fields))


class WriteReceipt(Base):
    __tablename__ = "write_receipts"
    key_hash: Mapped[str] = mapped_column(String(64), primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    action: Mapped[str] = mapped_column(String(80))
    target: Mapped[str] = mapped_column(String(80))
    request_hash: Mapped[str] = mapped_column(String(64))
    result_json: Mapped[str] = mapped_column(Text)
    http_status: Mapped[int] = mapped_column(Integer)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=utcnow)
    expires_at: Mapped[datetime] = mapped_column(DateTime)
    __table_args__ = (Index("ix_write_receipts_expiry", "expires_at"),)


@dataclass(frozen=True)
class WriteResult:
    data: dict
    status: int = 200


def _active():
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    if not getattr(g, "write_transaction_active", False):
        raise RuntimeError("Use authenticated_transaction before write receipt operations")
    from .db import check_schema
    check_schema()
    inspector = inspect(current_app.extensions["db_engine"])
    if (not inspector.has_table("write_receipts") or
            {c["name"] for c in inspector.get_columns("write_receipts")} != set(WriteReceipt.__table__.columns.keys())):
        raise AppError(503, "WRITE_SCHEMA_REQUIRED", "공통 요청 결과 테이블의 검토·준비 상태를 확인해 주세요.")


def _scope(action, target, *, for_result=False):
    reader = current_app.extensions.get("write_result_readers", {}).get(action) if isinstance(action, str) else None
    if (not isinstance(action, str) or
            (action not in ACTIONS and action not in current_app.extensions.get("write_action_contracts", {})
             and not (reader is not None and (for_result or reader.read_only))) or
            not isinstance(target, str)
            or re.fullmatch(r"[A-Za-z0-9:_-]{1,80}", target) is None):
        raise AppError(400, "INVALID_INPUT", "쓰기 행동과 대상 식별자를 확인해 주세요.")
    if not for_result and reader is not None and reader.read_only:
        raise AppError(503, "WRITE_ACTION_READ_ONLY", "이 기능은 저장된 결과 확인만 가능합니다.")


def _serializer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"], salt="common-write-key-v1")


def _now():
    return datetime.fromtimestamp(time.time(), timezone.utc).replace(tzinfo=None)


def _retention():
    ttl = current_app.config.get("WRITE_REQUEST_TTL_SECONDS", 0)
    if type(ttl) is not int or not 1 <= ttl <= 604800:
        raise AppError(503, "WRITE_POLICY_REQUIRED", "요청 키 보관 기간을 먼저 확정해 주세요.")
    return ttl


def issue_key(*, action, target):
    _active()
    _scope(action, target)
    ttl = _retention()
    expires = int(time.time()) + ttl
    key = _serializer().dumps({"u": g.user.id, "a": action, "t": target, "e": expires,
                               "n": secrets.token_urlsafe(18)})
    return {"request_key": key, "expires_at": iso(datetime.fromtimestamp(expires, timezone.utc).replace(tzinfo=None))}


def _decode(key, action, target, *, allow_expired=False):
    _scope(action, target, for_result=allow_expired)
    if (not isinstance(key, str) or not 32 <= len(key) <= 512
            or re.fullmatch(r"[A-Za-z0-9_.-]+", key) is None):
        raise AppError(400, "INVALID_INPUT", "유효한 요청 키가 필요합니다.")
    try:
        signed = _serializer().loads(key)
    except BadData:
        raise AppError(400, "INVALID_INPUT", "요청 키를 확인해 주세요.") from None
    if (not isinstance(signed, dict) or signed.get("u") != g.user.id
            or signed.get("a") != action or signed.get("t") != target):
        raise AppError(403, "FORBIDDEN", "이 요청 키를 사용할 권한이 없습니다.")
    if type(signed.get("e")) is not int:
        raise AppError(400, "INVALID_INPUT", "요청 키의 만료 시각을 확인해 주세요.")
    if not allow_expired and signed["e"] <= int(time.time()):
        raise AppError(410, "IDEMPOTENCY_KEY_EXPIRED", "요청 키가 만료되었습니다. 저장 상태를 먼저 확인해 주세요.")
    return hashlib.sha256(key.encode("ascii")).hexdigest(), signed["e"]


def _receipt(db, key_hash):
    return db.scalar(select(WriteReceipt).where(WriteReceipt.key_hash == key_hash)
                     .with_for_update().execution_options(populate_existing=True))


def _fingerprint(body):
    if not isinstance(body, dict):
        raise AppError(400, "INVALID_INPUT", "쓰기 본문은 JSON 객체여야 합니다.")
    try:
        canonical = json.dumps(body, sort_keys=True, separators=(",", ":"), ensure_ascii=True, allow_nan=False)
    except (TypeError, ValueError):
        raise AppError(400, "INVALID_INPUT", "JSON 쓰기 본문을 확인해 주세요.") from None
    return hashlib.sha256(canonical.encode("ascii")).hexdigest()


def _authorize(db, target, callback):
    from .db import get_db
    if db is not get_db():
        raise RuntimeError("The receipt and feature must use the current request DB session")
    if callback(db, target) is not True:
        raise AppError(403, "FORBIDDEN", "현재 요청을 수행할 권한이 없습니다.")


def _check_contract_authorizer(action, authorize, *, for_result=False):
    contract = (current_app.extensions.get("write_result_readers", {}).get(action) if for_result else None)
    if contract is None:
        contract = current_app.extensions.get("write_action_contracts", {}).get(action)
    if contract is not None and authorize is not contract.authorize:
        raise RuntimeError("Use the registered current-access authorizer for every feature action")


def perform_once(db, *, key, action, target, body, authorize, mutate):
    """Authorize CURRENT access before replay; check versions only in mutate.

    The caller must use the same DB/transaction for feature rows, audit and
    this receipt. authorize locks current target/relationships and must raise
    on denied access; a replay is never a grant based on old permissions.
    """
    _active()
    _check_contract_authorizer(action, authorize)
    _authorize(db, target, authorize)
    key_hash, _ = _decode(key, action, target)
    fingerprint = _fingerprint(body)
    previous = _receipt(db, key_hash)
    if previous is not None:
        if previous.request_hash != fingerprint:
            raise AppError(409, "IDEMPOTENCY_KEY_REUSED", "같은 요청 키로 다른 내용을 보낼 수 없습니다.")
        return WriteResult(json.loads(previous.result_json), previous.http_status)
    retention = _retention()
    result = mutate(db)
    if not isinstance(result, WriteResult) or result.status not in (200, 201) or not isinstance(result.data, dict):
        raise RuntimeError("Mutation must return WriteResult with minimal JSON data and status200/201")
    contract = current_app.extensions.get("write_action_contracts", {}).get(action)
    fields = contract.result_fields if contract else RESULT_FIELDS
    if set(result.data) - fields:
        raise RuntimeError("Only the action's declared minimal result fields may be retained")
    encoded = json.dumps(result.data, ensure_ascii=True, allow_nan=False, separators=(",", ":"))
    now = _now()
    db.add(WriteReceipt(key_hash=key_hash, user_id=g.user.id, action=action, target=target,
        request_hash=fingerprint, result_json=encoded, http_status=result.status,
        created_at=now, expires_at=now + timedelta(seconds=retention)))
    return result


def lookup_result(db, *, key, action, target, authorize):
    _active()
    _check_contract_authorizer(action, authorize, for_result=True)
    _authorize(db, target, authorize)
    key_hash, key_expires = _decode(key, action, target, allow_expired=True)
    receipt = _receipt(db, key_hash)
    if receipt is None:
        if key_expires <= int(time.time()):
            raise AppError(410, "IDEMPOTENCY_KEY_EXPIRED", "요청 키가 만료되었습니다. 현재 업무 상태를 확인해 주세요.")
        return {"state": "not_recorded"}
    if receipt.expires_at <= _now():
        raise AppError(410, "IDEMPOTENCY_KEY_EXPIRED", "요청 결과의 보관 기간이 지났습니다.")
    return {"state": "completed", "http_status": receipt.http_status, "result": json.loads(receipt.result_json),
            "expires_at": iso(receipt.expires_at)}


def _authorizer(action, *, for_result=False):
    if for_result:
        reader = current_app.extensions.get("write_result_readers", {}).get(action)
        if reader is not None:
            return reader.authorize
    contract = current_app.extensions.get("write_action_contracts", {}).get(action)
    if contract is not None:
        return contract.authorize
    callback = current_app.extensions.get("write_authorizers", {}).get(action)
    if not callable(callback):
        raise AppError(503, "WRITE_ACTION_NOT_READY", "담당 기능의 현재 권한 검사가 아직 연결되지 않았습니다.")
    return callback


def _input(*, for_result=False):
    body = payload()
    if set(body) != {"action", "target"}:
        raise AppError(400, "INVALID_INPUT", "action과 target만 전달해 주세요.")
    action, target = body["action"], body["target"]
    if not isinstance(action, str):
        raise AppError(400, "INVALID_INPUT", "쓰기 행동을 확인해 주세요.")
    _scope(action, target, for_result=for_result)
    return action, target


@bp.post("/keys")
@login_required
def keys():
    action, target = _input()
    with authenticated_transaction() as db:
        _authorize(db, target, _authorizer(action))
        data = issue_key(action=action, target=target)
    return success(data, 201)


@bp.post("/result")
@login_required
def result():
    action, target = _input(for_result=True)
    with authenticated_transaction() as db:
        data = lookup_result(db, key=request.headers.get("Idempotency-Key"), action=action,
                             target=target, authorize=_authorizer(action, for_result=True))
    return success(data)
