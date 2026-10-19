"""Read-only, local-process integration diagnostics. Never enables or initializes features."""
import argparse
import json
import sys

from sqlalchemy import inspect, text
from sqlalchemy.exc import SQLAlchemyError


# Fixed names only. Importing this checker must not register feature models.
TABLES = {
    "common": {
        "users": ("id", "login_id", "role", "is_active", "department"),
        "auth_sessions": ("token_hash", "user_id", "channel", "expires_at"),
        "schema_versions": ("version",), "admin_guard": ("id",),
        "audit_events": ("actor_id", "action", "resource_type", "resource_id"),
        "resource_grants": ("user_id", "resource_type", "resource_id"),
        "login_failures": ("account_key", "ip_key", "created_at"),
    },
    "tasks": {
        "tasks": ("task_id", "task_status", "row_version", "current_assignment_revision"),
        "task_assignments": ("task_id", "revision", "assignee_id", "accepted_at"),
        "task_managers": ("task_id", "user_id"),
    },
    "notices": {"notices": ("id", "title", "target", "author_id", "attachment_names_json")},
    "chat": {
        "chat_rooms": ("id", "created_by"),
        "chat_members": ("room_id", "user_id", "hidden_at"),
        "chat_messages": ("seq", "message_id", "room_id", "sender_id", "content"),
    },
    "writes": {"write_receipts": ("key_hash", "user_id", "action", "expires_at")},
}
ROUTES = {
    "common": (("/api/v1/auth/login", "POST"), ("/api/v1/auth/me", "GET")),
    "tasks": (("/api/v1/tasks", "GET"), ("/api/v1/tasks/<int:task_id>", "GET")),
    "notices": (("/api/v1/notices", "GET"),),
    "chat": (("/api/v1/chat/rooms", "GET"),),
    "writes": (("/api/v1/write-requests/keys", "POST"),),
}
NOT_CHECKED = [
    "이미 실행 중인 서버의 설정·HTTP·실제 로그인",
    "전체 DDL 타입·PK/FK/index·관계 정합성·백업/복구",
    "계정별 업무/파일 접근·공유 환경·두 PC·실물 장치",
    "미구현 본인 확인 API·근태·제출/판단·보존 정책",
]


def check_readiness(app, *, features=("common",), database=False):
    requested = tuple(dict.fromkeys(("common", *features)))
    if any(name not in TABLES for name in requested):
        raise ValueError("Unknown integration feature")
    rules = list(app.url_map.iter_rules())
    registered = {
        name: all(any(rule.rule == path and method in rule.methods for rule in rules)
                  for path, method in routes)
        for name, routes in ROUTES.items()
    }
    report = {"status": "configuration_only", "database_checked": False,
              "features": requested, "routes": registered, "missing_tables": [],
              "missing_columns": {}, "schema_version_1_recorded": None,
              "not_checked": NOT_CHECKED.copy()}
    if not all(registered[name] for name in requested):
        return report | {"status": "preparation_required", "code": "FEATURE_ROUTES_REQUIRED"}
    if not database:
        return report | {"code": "DATABASE_NOT_CHECKED"}
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql":
        return report | {"status": "preparation_required", "code": "MYSQL_REQUIRED"}
    try:
        with engine.connect() as connection:
            # Reject accidental writes, even if this implementation later regresses.
            connection.exec_driver_sql("SET TRANSACTION READ ONLY")
            inspector = inspect(connection)
            required = {table: columns for name in requested for table, columns in TABLES[name].items()}
            for name, columns in required.items():
                if not inspector.has_table(name):
                    report["missing_tables"].append(name)
                else:
                    actual = {c["name"] for c in inspector.get_columns(name)}
                    missing = sorted(set(columns) - actual)
                    if missing:
                        report["missing_columns"][name] = missing
            if ("schema_versions" not in report["missing_tables"]
                    and "schema_versions" not in report["missing_columns"]):
                report["schema_version_1_recorded"] = connection.execute(
                    text("SELECT version FROM schema_versions WHERE version = 1")).scalar_one_or_none() == 1
            # Deliberately no commit. Connection.close rolls the read transaction back.
        report["database_checked"] = True
        ready = (not report["missing_tables"] and not report["missing_columns"]
                 and report["schema_version_1_recorded"] is True)
        return report | {"status": "local_prerequisites_checked" if ready else "preparation_required",
                         "code": "LOCAL_PREREQUISITES_OK" if ready else "SCHEMA_PREPARATION_REQUIRED"}
    except SQLAlchemyError:
        return report | {"status": "preparation_required", "code": "DATABASE_UNAVAILABLE"}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", action="store_true", help="Read MySQL schema metadata; never apply DDL")
    parser.add_argument("--features", nargs="+", choices=tuple(TABLES), default=["common"])
    args = parser.parse_args(argv)
    app = None
    try:
        from . import create_app
        app = create_app()
        result = check_readiness(app, features=args.features, database=args.database)
    except (Exception, KeyboardInterrupt):
        # Do not echo SQL/URLs, private paths, driver errors or environment values.
        result = {"status": "preparation_required", "code": "CONFIGURATION_ERROR",
                  "database_checked": False, "not_checked": NOT_CHECKED.copy()}
    finally:
        if app is not None:
            app.extensions["db_engine"].dispose()
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0 if result["status"] in ("configuration_only", "local_prerequisites_checked") else 1


if __name__ == "__main__":
    sys.exit(main())
