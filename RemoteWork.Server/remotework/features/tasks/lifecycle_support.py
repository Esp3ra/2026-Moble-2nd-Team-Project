"""Member 2 read adapters and a fail-closed cross-owner deployment gate.

No member 1/3 route or callback is replaced here. Their owner must connect
the helpers in the shared deployment, then explicitly acknowledge its version.
"""
import os
import re

from flask import current_app
from sqlalchemy import column, inspect, select, table, text

from ...common import AppError


CONTRACT = "member2-task-lifecycle-v1"
SCHEMA = "task-lifecycle-draft-v1"
INTEGRATED_ACK = {"contract": CONTRACT, "schema": SCHEMA, "mode": "integrated",
    "member1": "common-write-and-ddl-v1", "member3": "cancel-and-extension-submit-guards-v1"}
TEST_DOUBLE_ACK = {"contract": CONTRACT, "schema": SCHEMA, "mode": "test-double",
    "member1": "explicit-test-double", "member3": "explicit-test-double"}


def lifecycle_enabled():
    return current_app.config.get("ENABLE_TASK_LIFECYCLE_DRAFT",
        os.getenv("REMOTEWORK_ENABLE_TASK_LIFECYCLE_DRAFT") == "1") is True


def integration_acknowledged():
    ack = current_app.extensions.get("task_lifecycle_integration_ack")
    if ack == INTEGRATED_ACK:
        return True
    # A test double must never silently substitute for actual result submission.
    results = current_app.config.get("ENABLE_RESULTS_DRAFT",
        os.getenv("REMOTEWORK_ENABLE_RESULTS_DRAFT") == "1")
    return (ack == TEST_DOUBLE_ACK and current_app.testing is True and not results
            and "results" not in current_app.blueprints)


def storage_present(db):
    """Recognize persisted lifecycle storage even after the draft flag is off.

    No ORM import/metadata registration: old data must never become writable
    again merely because deployment disables the route flag.
    """
    inspector = inspect(db.connection())
    names = ("task_cancellations", "task_deadline_extensions")
    present = [inspector.has_table(name) for name in names]
    if not any(present) and not lifecycle_enabled():
        return False
    require_lifecycle_schema(db)
    return True


def require_lifecycle_schema(db):
    inspector = inspect(db.connection())
    # This draft supports MySQL only; no SQLite compatibility work is implied.
    if db.bind.dialect.name != "mysql":
        raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "업무 취소·연장용 MySQL 스키마를 먼저 대조해 주세요.")
    expected = {"task_cancellations": {"task_id", "cancelled_by", "reason", "cancelled_at"},
        "task_deadline_extensions": {"extension_id", "task_id", "assignment_revision", "original_due_date",
            "requested_due_date", "requested_by", "reason", "status", "created_at", "decided_at",
            "decided_by", "decision_reason", "pending_revision"}}
    for name, fields in expected.items():
        if (not inspector.has_table(name) or
                {c["name"] for c in inspector.get_columns(name)} != fields):
            raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "업무 취소·연장 테이블의 준비 상태를 확인해 주세요.")
        expected_pk = ["task_id"] if name == "task_cancellations" else ["extension_id"]
        if inspector.get_pk_constraint(name).get("constrained_columns") != expected_pk:
            raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "취소·연장 테이블의 기본키를 확인해 주세요.")
    columns = {c["name"]: c for c in inspector.get_columns("task_deadline_extensions")}
    uniques = [index for index in inspector.get_indexes("task_deadline_extensions") if index.get("unique")]
    uniques += inspector.get_unique_constraints("task_deadline_extensions")
    computed = columns["pending_revision"].get("computed") or {}
    expression = re.sub(r"_[a-z0-9]+(?='pending')", "", str(computed.get("sqltext", "")).lower())
    expression = re.sub(r"[\s`()]+", "", expression)
    if (computed.get("persisted") is not True or
            expression != "casewhenstatus='pending'thenassignment_revisionelsenullend" or
            not any(index.get("column_names") == ["task_id", "pending_revision"] for index in uniques)):
        raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "연장 신청의 단일 대기 제약을 확인해 주세요.")
    for name, expected_fks in (("task_cancellations", {
            (("task_id",), "tasks", ("task_id",)), (("cancelled_by",), "users", ("id",))}),
            ("task_deadline_extensions", {
                (("task_id", "assignment_revision"), "task_assignments", ("task_id", "revision")),
                (("requested_by",), "users", ("id",)), (("decided_by",), "users", ("id",))})):
        fks = inspector.get_foreign_keys(name)
        if any(fk.get("referred_schema") not in (None, inspector.default_schema_name)
               or str(fk.get("options", {}).get("ondelete", "")).upper() == "CASCADE" for fk in fks):
            raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "현재 DB의 보존형 외래키 연결을 확인해 주세요.")
        actual_fks = {(tuple(fk["constrained_columns"]), fk["referred_table"], tuple(fk["referred_columns"])) for fk in fks}
        if not expected_fks.issubset(actual_fks):
            raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "취소·연장 자료의 외래키 연결을 확인해 주세요.")
    for name, type_names in (("task_cancellations", {"task_id": "BIGINT", "cancelled_by": "INTEGER"}),
            ("task_deadline_extensions", {"extension_id": "BIGINT", "task_id": "BIGINT",
                "assignment_revision": "INTEGER", "requested_by": "INTEGER", "decided_by": "INTEGER"})):
        actual = {c["name"]: c for c in inspector.get_columns(name)}
        if any(type(actual[field]["type"]).__name__ != typename or getattr(actual[field]["type"], "unsigned", False)
               for field, typename in type_names.items()):
            raise AppError(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED", "취소·연장 식별자와 배정 차수의 signed 타입을 확인해 주세요.")


def require_lifecycle_write_ready(db):
    if not lifecycle_enabled() or not integration_acknowledged():
        raise AppError(503, "TASK_LIFECYCLE_INTEGRATION_REQUIRED",
            "취소·연장 정책은 확정됐습니다. 공통 저장·제출 차단의 기술 연결 확인 후 실행할 수 있습니다.")
    if not current_app.config.get("ENABLE_COMMON_WRITE_DRAFT"):
        raise AppError(503, "WRITE_SUPPORT_DISABLED", "공통 쓰기 지원이 활성화되지 않았습니다.")
    require_lifecycle_schema(db)


def cancellation_exists_query(task_id):
    """SQL EXISTS expression; caller controls list visibility and ownership."""
    cancellation = table("task_cancellations", column("task_id"))
    return select(cancellation.c.task_id).where(cancellation.c.task_id == task_id).exists()


def ensure_not_cancelled(db, task_id):
    """Call after current authorization, inside the shared guarded transaction."""
    if not storage_present(db):
        return
    if db.execute(text("SELECT task_id FROM task_cancellations WHERE task_id = :task_id FOR UPDATE"),
                  {"task_id": task_id}).first() is not None:
        raise AppError(409, "TASK_CANCELLED", "취소된 업무에서는 더 이상 작업할 수 없습니다.")


def pending_extension_facts(db, task_id, assignment_revision, *, lock=False):
    """Read-only member 3 adapter: no reviewer or submission-gate writes.

    Caller must acquire shared guard -> task/assignment -> sorted user locks
    before lock=True. Bind to the CURRENT assignment; never trust client IDs.
    """
    # The result owner must distinguish a missing integration source from a
    # genuine absence of pending requests. Never disguise missing storage=False.
    require_lifecycle_schema(db)
    sql = ("SELECT extension_id FROM task_deadline_extensions WHERE task_id = :task_id "
           "AND assignment_revision = :revision AND status = 'pending'")
    if lock:
        sql += " FOR UPDATE"
    return db.execute(text(sql), {"task_id": task_id, "revision": assignment_revision}).first() is not None
