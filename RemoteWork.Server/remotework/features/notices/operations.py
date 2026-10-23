"""Notice-owned durable intent tombstones, using the existing W1 transaction.

No request/startup DDL, file/body retention, permission fallback or automatic
resend. The operation survives notice deletion and common receipt cleanup.
"""
from datetime import timedelta
import hashlib
import json
import uuid

from flask import current_app, g
from sqlalchemy import Column, DateTime, ForeignKey, Integer, MetaData, String, Table, Text, inspect, select, text

from ...auth import admin_transaction
from ...common import AppError
from ...models import iso
from ...write_requests import WriteResult, perform_once, register_write_action, register_write_result_reader

ACTION = "notice.created"
FIELDS = frozenset(("notice_id", "operation_id", "created_at"))
metadata = MetaData()
Table("users", metadata, Column("id", Integer, primary_key=True))
operations = Table("notice_operations", metadata,
    Column("actor_id", Integer, ForeignKey("users.id"), primary_key=True, autoincrement=False),
    Column("operation_id", String(36), primary_key=True),
    Column("body_hash", String(64), nullable=False),
    # Deliberately no notice FK: deleting a notice must preserve its tombstone.
    Column("notice_id", Integer, nullable=False),
    Column("receipt_json", Text, nullable=False),
    Column("created_at", DateTime, nullable=False),
    Column("expires_at", DateTime, nullable=False),
    mysql_engine="InnoDB", mysql_charset="utf8mb4", mysql_collate="utf8mb4_unicode_ci")


def canonical(value):
    try:
        if not isinstance(value, str) or str(uuid.UUID(value)) != value:
            raise ValueError()
    except (ValueError, AttributeError):
        raise AppError(400, "INVALID_INPUT", "원 요청 UUID를 확인해 주세요.") from None
    return value


def strict_payload():
    from flask import request
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError()
            result[key] = value
        return result
    try:
        if request.args:
            raise ValueError()
        value = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=pairs,
            parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
        if type(value) is not dict:
            raise ValueError()
    except (ValueError, UnicodeError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 UTF-8 JSON 객체를 전달해 주세요.") from None
    return value


def storage(db):
    connection = db.connection()
    inspector = inspect(connection)
    fail = lambda: AppError(503, "NOTICE_OPERATION_SCHEMA_REQUIRED", "공지 원 요청 저장소 017의 준비 상태를 확인해 주세요.")
    if connection.dialect.name != "mysql" or not inspector.has_table(operations.name):
        raise fail()
    actual = {c["name"]: c for c in inspector.get_columns(operations.name)}
    if (set(actual) != set(operations.c.keys()) or
            inspector.get_pk_constraint(operations.name)["constrained_columns"] != ["actor_id", "operation_id"] or
            str(inspector.get_table_options(operations.name).get("mysql_engine", "")).lower() != "innodb"):
        raise fail()
    for c in operations.c:
        a = actual[c.name]
        if (str(a["type"]).split()[0].split("(")[0] != str(c.type).split()[0].split("(")[0] or
                getattr(a["type"], "length", None) != getattr(c.type, "length", None) or
                a["nullable"] != c.nullable or a.get("default") is not None or
                a.get("autoincrement") or getattr(a["type"], "unsigned", False) or
                (isinstance(c.type, (String, Text)) and getattr(a["type"], "collation", None) != "utf8mb4_unicode_ci")):
            raise fail()
    fks = inspector.get_foreign_keys(operations.name)
    if (len(fks) != 1 or fks[0]["constrained_columns"] != ["actor_id"] or
            fks[0]["referred_table"] != "users" or fks[0]["referred_columns"] != ["id"] or
            fks[0].get("referred_schema") not in (None, inspector.default_schema_name) or
            any(str(fks[0].get("options", {}).get(k, "RESTRICT")).upper() not in ("RESTRICT", "NO ACTION")
                for k in ("ondelete", "onupdate"))):
        raise fail()


def now(db):
    return db.scalar(text("SELECT UTC_TIMESTAMP(6)"))


def current(db, identifier):
    return db.execute(select(operations).where(operations.c.actor_id == g.user.id,
        operations.c.operation_id == identifier).with_for_update()).mappings().first()


def authorize(db, target):
    canonical(target)
    if g.user.role != "admin":
        raise AppError(403, "FORBIDDEN", "현재 관리자만 원 공지 요청을 확인할 수 있습니다.")
    storage(db)
    old = current(db, target)
    if old is not None and old.expires_at <= now(db):
        raise AppError(410, "NOTICE_OPERATION_EXPIRED", "원 공지 요청의 결과 보관기간이 지났습니다. 새 요청으로 자동 전환하지 않습니다.")
    return True


def body_hash(body):
    return hashlib.sha256(json.dumps(body, sort_keys=True, ensure_ascii=True,
        separators=(",", ":"), allow_nan=False).encode("ascii")).hexdigest()


def create(body, *, key, insert):
    identifier = canonical(body["operation_id"])
    with admin_transaction() as db:
        def mutate(session):
            old = current(session, identifier)
            fingerprint = body_hash(body)
            if old is not None:
                if old.body_hash != fingerprint:
                    raise AppError(409, "NOTICE_OPERATION_CONFLICT", "같은 원 요청 UUID에 다른 내용을 저장할 수 없습니다.")
                return WriteResult(json.loads(old.receipt_json), 201)
            notice = insert(session)
            stamp = now(session)
            receipt = dict(notice_id=notice["id"], operation_id=identifier, created_at=notice["created_at"])
            session.execute(operations.insert().values(actor_id=g.user.id, operation_id=identifier,
                body_hash=fingerprint, notice_id=notice["id"], receipt_json=json.dumps(receipt),
                created_at=stamp, expires_at=stamp+timedelta(seconds=min(current_app.config["WRITE_REQUEST_TTL_SECONDS"], 86400))))
            return WriteResult(receipt, 201)
        result = perform_once(db, key=key, action=ACTION, target=identifier,
            body=body, authorize=authorize, mutate=mutate)
    return result.data


def lookup(identifier):
    canonical(identifier)
    with admin_transaction() as db:
        authorize(db, identifier)
        old = current(db, identifier)
        if old is None:
            return {"state": "not_recorded"}
        return dict(state="completed", http_status=201, result=json.loads(old.receipt_json), expires_at=iso(old.expires_at))


def install(app):
    if app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        register_write_action(app, action=ACTION, authorize=authorize, result_fields=FIELDS)
        register_write_result_reader(app, action=ACTION, authorize=authorize)
