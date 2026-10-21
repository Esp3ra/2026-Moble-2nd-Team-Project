"""Unnumbered DDL proposal. Never called by common init-db or application startup."""
import re
from sqlalchemy import inspect
from ...common import AppError
from .models import CollaborationRequest as Row, CollaborationEvent as Event, FeatureBase


def normalized(expression):
    value = re.sub(r"_[a-z0-9]+(?=')", "", str(expression).lower())
    return re.sub(r"[\s`()]+", "", value)


def require_storage(db):
    inspector = inspect(db.connection())
    def fail():
        raise AppError(503, "COLLABORATION_SCHEMA_REQUIRED", "협업 요청 DB 구조의 검토·준비가 필요합니다.")
    for model in (Row, Event):
        table = model.__table__
        if not inspector.has_table(table.name):
            fail()
        columns = {c["name"]: c for c in inspector.get_columns(table.name)}
        if set(columns) != set(table.columns.keys()):
            fail()
        for expected in table.columns:
            actual = columns[expected.name]
            actual_type = actual["type"].copy()
            if getattr(actual_type, "collation", None):
                if db.bind.dialect.name == "mysql" and actual_type.collation != "utf8mb4_unicode_ci":
                    fail()
                actual_type.collation = None
            if (str(actual_type.compile(dialect=db.bind.dialect)).upper() !=
                    str(expected.type.compile(dialect=db.bind.dialect)).upper()
                    or actual["nullable"] != expected.nullable):
                fail()
        if inspector.get_pk_constraint(table.name).get("constrained_columns") != [c.name for c in table.primary_key.columns]:
            fail()
        expected_fks = {(tuple(c.name for c in fk.columns), next(iter(fk.elements)).column.table.name,
            tuple(e.column.name for e in fk.elements)) for fk in table.foreign_key_constraints}
        actual_fks = inspector.get_foreign_keys(table.name)
        if any(fk.get("referred_schema") not in (None, inspector.default_schema_name)
            or fk.get("options", {}).get("ondelete", "").upper() == "CASCADE" for fk in actual_fks):
            fail()
        if {(tuple(fk["constrained_columns"]), fk["referred_table"], tuple(fk["referred_columns"])) for fk in actual_fks} != expected_fks:
            fail()
        expected_checks = {c.name: normalized(c.sqltext) for c in table.constraints if c.__class__.__name__ == "CheckConstraint"}
        actual_checks = {c["name"]: normalized(c["sqltext"]) for c in inspector.get_check_constraints(table.name)}
        if any(actual_checks.get(name) != expression for name, expression in expected_checks.items()):
            fail()
    generated = inspector.get_columns(Row.__tablename__)
    actual = next(c for c in generated if c["name"] == "active_revision").get("computed") or {}
    if actual.get("persisted") is not True or normalized(actual.get("sqltext", "")) != normalized(Row.__table__.c.active_revision.computed.sqltext):
        fail()
    unique = inspector.get_unique_constraints(Row.__tablename__) + [x for x in inspector.get_indexes(Row.__tablename__) if x.get("unique")]
    if not any(x.get("column_names") == ["task_id", "active_revision", "recipient_id"] for x in unique):
        fail()


def create_missing_storage(engine, *, reviewed=False):
    """Owner executes after backup/review. Refuses to alter any existing table."""
    if reviewed is not True:
        raise RuntimeError("Explicit DDL review required; no shared migration number is allocated")
    inspector = inspect(engine)
    existing = [inspector.has_table(model.__tablename__) for model in (Row, Event)]
    if any(existing):
        raise RuntimeError("Existing collaboration storage: compare it; no automatic repair/alter")
    FeatureBase.metadata.create_all(engine)
