"""Explicit same-TX deadline journal proposal; never installed by app startup.

Owner2 calls record_change only inside an authorized W1 mutation, after task
flush and before audit/receipt. No automatic hooks, baseline or migrations.
"""
from dataclasses import dataclass
from datetime import date, datetime
import uuid

from flask import g
from sqlalchemy import BigInteger, Date, DateTime, Integer, String, column, inspect, select, table, text

from .common import AppError
from .db import get_db
from .features.tasks.models import TaskRecord


journal = table("deadline_event_journal_draft",
    column("event_id", String(36)), column("task_id", BigInteger),
    column("event_order", BigInteger), column("source_row_version", BigInteger),
    column("source_action", String(40)), column("before_due_date", Date),
    column("due_date", Date), column("recorded_at", DateTime), column("actor_id", Integer))
heads = table("deadline_event_heads_draft", column("task_id", BigInteger), column("event_id", String(36)))


@dataclass(frozen=True)
class DeadlineChange:
    event_id: str
    task_id: int
    event_order: int
    source_row_version: int
    source_action: str
    before_due_date: date | None
    due_date: date
    recorded_at: datetime
    actor_id: int


def unavailable(code="DEADLINE_EVENT_STORAGE_REQUIRED"):
    raise AppError(503, code, "마감 사건 기록의 준비·원본 연결 상태를 확인해 주세요.")


def guard(db):
    if not getattr(g, "write_transaction_active", False) or db is not get_db() or not db.in_transaction():
        raise RuntimeError("Use the same authenticated_transaction session")
    if db.bind.dialect.name != "mysql":
        raise RuntimeError("MySQL required")


def storage(db):
    guard(db)
    inspector = inspect(db.connection())
    for name, expected_pk in ((journal.name, ["event_id"]), (heads.name, ["task_id"])):
        if not inspector.has_table(name) or inspector.get_pk_constraint(name)["constrained_columns"] != expected_pk:
            unavailable()
        if str(inspector.get_table_options(name).get("mysql_engine", "")).lower() != "innodb":
            unavailable()
        columns = {c["name"]: c for c in inspector.get_columns(name)}
        wanted_columns = {c.name: c for c in (journal.c if name == journal.name else heads.c)}
        if set(columns) != set(wanted_columns):
            unavailable()
        for field, expected in wanted_columns.items():
            actual = columns[field]
            expected_name = {BigInteger: "BIGINT", Integer: "INTEGER", String: "VARCHAR",
                             Date: "DATE", DateTime: "DATETIME"}[type(expected.type)]
            if (actual["nullable"] != (field == "before_due_date")
                    or getattr(actual["type"], "unsigned", False)
                    or type(actual["type"]).__name__ != expected_name):
                unavailable()
            if isinstance(expected.type, String) and (actual["type"].length != expected.type.length
                    or actual["type"].collation != "ascii_bin"):
                unavailable()
            if field == "recorded_at" and getattr(actual["type"], "fsp", None) != 6:
                unavailable()
        fks = inspector.get_foreign_keys(name)
        wanted = {(("task_id",), "tasks", ("task_id",))}
        if name == journal.name:
            wanted.add((("actor_id",), "users", ("id",)))
        else:
            wanted.add((("task_id", "event_id"), journal.name, ("task_id", "event_id")))
        actual = {(tuple(f["constrained_columns"]), f["referred_table"], tuple(f["referred_columns"])) for f in fks}
        if actual != wanted or any(f.get("referred_schema") not in (None, inspector.default_schema_name)
                or any(str(f.get("options", {}).get(k, "RESTRICT")).upper() not in ("RESTRICT", "NO ACTION")
                       for k in ("ondelete", "onupdate")) for f in fks):
            unavailable()
    unique = {tuple(c["column_names"]) for c in inspector.get_unique_constraints(journal.name)}
    if not {("task_id", "source_row_version"), ("task_id", "event_order"), ("task_id", "event_id")} <= unique:
        unavailable()
    if "ck_deadline_event_positive" not in {c["name"] for c in inspector.get_check_constraints(journal.name)}:
        unavailable()


def _task(db, task_id):
    if type(task_id) is not int or not 1 <= task_id <= 9223372036854775807:
        raise ValueError("Positive signed BIGINT task ID required")
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id)
        .with_for_update().execution_options(populate_existing=True))
    if task is None:
        unavailable("DEADLINE_EVENT_SOURCE_MISMATCH")
    return task


def _find(db, identifier):
    row = db.execute(select(journal).where(journal.c.event_id == identifier).with_for_update()).mappings().first()
    return DeadlineChange(**row) if row else None


def read_current(db, *, task_id):
    """Internal trusted-reader port, NOT an authorization or HTTP endpoint."""
    storage(db)
    task = _task(db, task_id)  # Existing guard/parent before journal locks.
    identifier = db.scalar(select(heads.c.event_id).where(heads.c.task_id == task_id).with_for_update())
    if identifier is None:
        unavailable("DEADLINE_EVENT_UNKNOWN")
    event = _find(db, identifier)
    if event is None or event.task_id != task_id or event.due_date != task.due_date:
        unavailable("DEADLINE_EVENT_SOURCE_MISMATCH")
    return event


def read_event(db, *, event_id):
    """Trusted retained lookup; caller must authorize before exposing a value."""
    storage(db)
    if not isinstance(event_id, str):
        raise ValueError("Canonical event UUID required")
    try:
        if str(uuid.UUID(event_id)) != event_id:
            raise ValueError()
    except ValueError:
        raise ValueError("Canonical event UUID required") from None
    # Guard serializes cooperating writers. Discover parent without a journal
    # row lock, then preserve the parent-before-journal lock order.
    task_id = db.scalar(select(journal.c.task_id).where(journal.c.event_id == event_id))
    if task_id is None:
        return None
    _task(db, task_id)
    return _find(db, event_id)


def record_change(db, *, task_id, source_row_version, source_action, before_due_date, due_date):
    """Record a trusted validated mutation; exception must roll back caller TX.

    Creation: row_version1, before=None. Updates/approved extensions: explicit
    original/new DATE. A same-date call returns None. Existing tasks without a
    head stay unknown; this function never establishes a legacy baseline.
    """
    guard(db)
    actions = {"task.created", "task.updated", "task.deadline_extension_approved"}
    if source_action not in actions or type(source_row_version) is not int or not 1 <= source_row_version <= 9223372036854775807:
        raise ValueError("Reviewed mutation action and positive source version required")
    if type(due_date) is not date or due_date.year < 1000 or (before_due_date is not None and type(before_due_date) is not date):
        raise ValueError("Exact DATE values required")
    creation = source_action == "task.created"
    if creation != (before_due_date is None) or creation and source_row_version != 1:
        raise ValueError("Creation requires original version1 and no previous date")
    task = _task(db, task_id)
    if task.row_version != source_row_version or task.due_date != due_date:
        unavailable("DEADLINE_EVENT_SOURCE_MISMATCH")
    if not creation and before_due_date == due_date:
        return None  # No new dependency for body/title/start/attachment-only changes.
    storage(db)
    old_id = db.scalar(select(heads.c.event_id).where(heads.c.task_id == task_id).with_for_update())
    old = _find(db, old_id) if old_id is not None else None
    prior = db.execute(select(journal).where(journal.c.task_id == task_id,
        journal.c.source_row_version == source_row_version).with_for_update()).mappings().first()
    if prior is not None:
        prior = DeadlineChange(**prior)
        if (prior.event_id != old_id or prior.source_action != source_action
                or prior.before_due_date != before_due_date or prior.due_date != due_date):
            unavailable("DEADLINE_EVENT_SOURCE_MISMATCH")
        return prior
    if creation:
        if old_id is not None:
            unavailable("DEADLINE_EVENT_SOURCE_MISMATCH")
    elif old is None:
        unavailable("DEADLINE_EVENT_UNKNOWN")
    elif old.due_date != before_due_date or old.source_row_version >= source_row_version:
        unavailable("DEADLINE_EVENT_SOURCE_MISMATCH")
    order = old.event_order + 1 if old else 1
    if order > 9223372036854775807:
        unavailable("DEADLINE_EVENT_ORDER_EXHAUSTED")
    stamp = db.scalar(text("SELECT UTC_TIMESTAMP(6)"))  # TX observation, not commit time.
    value = DeadlineChange(str(uuid.uuid4()), task_id, order, source_row_version,
        source_action, before_due_date, due_date, stamp, g.user.id)
    db.execute(journal.insert().values(**value.__dict__))
    if old_id is None:
        db.execute(heads.insert().values(task_id=task_id, event_id=value.event_id))
    else:
        db.execute(heads.update().where(heads.c.task_id == task_id).values(event_id=value.event_id))
    return value
