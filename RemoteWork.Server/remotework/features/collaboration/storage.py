"""012 physical contract; no invocation from common init-db/application startup."""
import re
from pathlib import Path
from sqlalchemy import inspect, text
from ...common import AppError
from .models import CollaborationRequest as Row, CollaborationEvent as Event


def normalized(expression):
    value = re.sub(r"_[a-z0-9]+(?=')", "", str(expression).lower())
    return re.sub(r"[\s`()]+", "", value)


def require_storage(db):
    if db.bind.dialect.name != "mysql":
        raise AppError(503, "COLLABORATION_SCHEMA_REQUIRED", "협업 요청은 MySQL 저장소가 필요합니다.")
    inspector = inspect(db.connection())
    def fail():
        raise AppError(503, "COLLABORATION_SCHEMA_REQUIRED", "협업 요청 DB 구조의 검토·준비가 필요합니다.")
    for model in (Row, Event):
        table = model.__table__
        if not inspector.has_table(table.name):
            fail()
        options = inspector.get_table_options(table.name)
        if (str(options.get("mysql_engine", "")).lower() != "innodb"
                or options.get("mysql_default charset") != "utf8mb4"
                or options.get("mysql_collate") != "utf8mb4_unicode_ci"):
            fail()
        columns = {c["name"]: c for c in inspector.get_columns(table.name)}
        if set(columns) != set(table.columns.keys()):
            fail()
        for expected in table.columns:
            actual = columns[expected.name]
            actual_type = actual["type"].copy()
            if getattr(actual_type, "charset", None):
                if actual_type.charset != "utf8mb4":
                    fail()
                actual_type.charset = None  # explicit/inherited utf8mb4 are equivalent
            if getattr(actual_type, "collation", None):
                if db.bind.dialect.name == "mysql" and actual_type.collation != "utf8mb4_unicode_ci":
                    fail()
                actual_type.collation = None
            if (str(actual_type.compile(dialect=db.bind.dialect)).upper() !=
                    str(expected.type.compile(dialect=db.bind.dialect)).upper()
                    or actual["nullable"] != expected.nullable or actual.get("default") is not None
                    or (actual.get("autoincrement") is True) != (model is Event and expected.name == "event_id")):
                fail()
        if inspector.get_pk_constraint(table.name).get("constrained_columns") != [c.name for c in table.primary_key.columns]:
            fail()
        expected_fks = {(tuple(c.name for c in fk.columns), next(iter(fk.elements)).column.table.name,
            tuple(e.column.name for e in fk.elements)) for fk in table.foreign_key_constraints}
        actual_fks = inspector.get_foreign_keys(table.name)
        if any(fk.get("referred_schema") not in (None, inspector.default_schema_name)
            or any(str(fk.get("options", {}).get(option, "")).upper() not in ("", "RESTRICT", "NO ACTION")
                for option in ("ondelete", "onupdate")) for fk in actual_fks):
            fail()
        if {(tuple(fk["constrained_columns"]), fk["referred_table"], tuple(fk["referred_columns"])) for fk in actual_fks} != expected_fks:
            fail()
        expected_checks = {c.name: normalized(c.sqltext) for c in table.constraints if c.__class__.__name__ == "CheckConstraint"}
        actual_checks = {c["name"]: normalized(c["sqltext"]) for c in inspector.get_check_constraints(table.name)}
        if any(actual_checks.get(name) != expression for name, expression in expected_checks.items()):
            fail()
        enforced = set(db.execute(text("SELECT CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS "
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = :table_name AND CONSTRAINT_TYPE = 'CHECK' "
            "AND ENFORCED = 'YES'"), {"table_name": table.name}).scalars())
        if not set(expected_checks).issubset(enforced):
            fail()
        indexes = {index["name"]: index for index in inspector.get_indexes(table.name)}
        for expected in table.indexes:
            actual = indexes.get(expected.name)
            if (actual is None or actual.get("column_names") != [c.name for c in expected.columns]
                    or bool(actual.get("unique")) != expected.unique
                    or actual.get("dialect_options", {}).get("mysql_length")):
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
        raise RuntimeError("Explicit 012 DDL review/backup required")
    if engine.dialect.name != "mysql":
        raise RuntimeError("MySQL required")
    inspector = inspect(engine)
    existing = [inspector.has_table(model.__tablename__) for model in (Row, Event)]
    if any(existing):
        raise RuntimeError("Existing collaboration storage: compare it; no automatic repair/alter")
    sql = (Path(__file__).resolve().parents[3] / "migrations/012_collaboration_requests.mysql.sql").read_text(encoding="utf-8")
    with engine.begin() as connection:
        for statement in sql.split(";"):
            if statement.strip():
                connection.exec_driver_sql(statement)
