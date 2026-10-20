"""Reserved 008 local-review preparation. No automatic migration or request-time DDL."""
import hashlib
import os
from flask import current_app
from sqlalchemy import inspect, text


def _failure(item):
    raise RuntimeError("기존 팝업 테이블 불일치: " + item + ". 자동 변경 없이 DDL 검토가 필요합니다.")


def _action(value):
    return "RESTRICT" if value in (None, "NO ACTION", "RESTRICT") else value.upper()


def _check(connection):
    from .popup_models import NoticeConfirmation
    table = NoticeConfirmation.__table__
    inspector = inspect(connection)
    actual = {c["name"]: c for c in inspector.get_columns(table.name)}
    expected = {c.name: c for c in table.columns}
    if set(actual) != set(expected): _failure("columns")
    for name, column in expected.items():
        seen = actual[name]
        if str(seen["type"].compile(dialect=connection.dialect)).upper() != str(column.type.compile(dialect=connection.dialect)).upper(): _failure(name + " type")
        if bool(getattr(seen["type"], "unsigned", False)): _failure(name + " signed INTEGER")
        if seen["nullable"] != column.nullable: _failure(name + " nullable")
        if seen.get("default") is not None: _failure(name + " default")
        if name in ("notice_id", "user_id") and seen.get("autoincrement"): _failure(name + " autoincrement")
    if inspector.get_pk_constraint(table.name)["constrained_columns"] != ["notice_id", "user_id"]: _failure("PK order")
    actual_fks = {(tuple(f["constrained_columns"]), f["referred_table"], tuple(f["referred_columns"]),
                   _action(f.get("options", {}).get("ondelete")), _action(f.get("options", {}).get("onupdate")))
                  for f in inspector.get_foreign_keys(table.name)}
    if actual_fks != {(("notice_id",), "notices", ("id",), "CASCADE", "RESTRICT"),
                      (("user_id",), "users", ("id",), "RESTRICT", "RESTRICT")}:
        _failure("FK target/ondelete/onupdate")
    # The PK provides notice_id lookup/uniqueness; user lookup must have its own leading index.
    indexes = inspector.get_indexes(table.name)
    if not any(i["column_names"] and i["column_names"][0] == "user_id" and not i.get("unique") for i in indexes):
        _failure("user_id index")
    if inspector.get_unique_constraints(table.name) or any(i.get("unique") for i in indexes):
        _failure("unexpected UNIQUE")
    if connection.dialect.name == "mysql":
        options = connection.execute(text("SELECT ENGINE, TABLE_COLLATION FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='notice_confirmations'")).one()
        if options[0].upper() != "INNODB" or options[1] != "utf8mb4_unicode_ci": _failure("engine/charset/collation")
    return table


def prepare_popup_schema(engine):
    enabled = current_app.config.get("ENABLE_NOTICE_POPUP_DRAFT", os.getenv("REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT") == "1")
    if not enabled or "notice_popups" not in current_app.blueprints:
        raise RuntimeError("공지와 팝업의 별도 개발 옵션을 모두 활성화해야 합니다.")
    with engine.connect() as connection:
        lock = None
        try:
            if connection.dialect.name == "mysql":
                # Serialize only cooperating CLI preparations for this database, not feature requests.
                lock = "rw-popup-init:" + hashlib.sha256((engine.url.database or "").encode()).hexdigest()[:32]
                if connection.execute(text("SELECT GET_LOCK(:name, 10)"), {"name":lock}).scalar() != 1:
                    raise RuntimeError("다른 초기화가 진행 중입니다. 기존 작업 종료 후 같은 명령으로 확인해 주세요.")
            inspector = inspect(connection)
            if any(not inspector.has_table(name) for name in ("users", "notices", "schema_versions")):
                raise RuntimeError("먼저 공통 init-db와 notices init-table을 명시적으로 실행해 주세요.")
            if not connection.execute(text("SELECT version FROM schema_versions WHERE version=1")).first():
                raise RuntimeError("공통 schema1 준비를 먼저 확인해 주세요.")
            if inspector.has_table("notice_confirmations"):
                _check(connection)
            else:
                from .popup_models import NoticeConfirmation
                NoticeConfirmation.__table__.create(connection)
                _check(connection)
            connection.commit()
        finally:
            if lock is not None:
                connection.execute(text("SELECT RELEASE_LOCK(:name)"), {"name":lock})
