"""Read-only task-query prerequisites; never initializes, seeds or serves a DB.

Run from RemoteWork.Server with the existing server Python/configuration:
    python -m remotework.features.tasks.readiness
This command checks its own process, not an already running API server.
"""
import json
import sys

from sqlalchemy import and_, column, exists, func, inspect, select, table
from sqlalchemy.exc import SQLAlchemyError

from ... import db


# Lightweight Core clauses have no Base/MetaData registration or reflection.
_TASKS = table("tasks", *(column(name) for name in (
    "task_id", "title", "description", "task_status", "start_date", "due_date",
    "current_assignment_revision", "row_version", "created_by", "created_at", "updated_at")))
_ASSIGNMENTS = table("task_assignments", *(column(name) for name in (
    "task_id", "revision", "assignee_id", "assigned_by", "assigned_at", "accepted_at")))
_MANAGERS = table("task_managers", column("task_id"), column("user_id"))
_USERS = table("users", column("id"), column("display_name"))
_REQUIRED_COLUMNS = {
    "schema_versions": ("version",),
    "users": tuple(item.name for item in _USERS.c),
    "tasks": tuple(item.name for item in _TASKS.c),
    "task_assignments": tuple(item.name for item in _ASSIGNMENTS.c),
    "task_managers": tuple(item.name for item in _MANAGERS.c),
}
_ROUTES = (("/api/v1/tasks", "task_queries.index"),
           ("/api/v1/tasks/<int:task_id>", "task_queries.detail"))
_NOT_CHECKED = (
    "전체 DDL·열 타입/NULL·PK/index/FK/CHECK·엔진/collation 호환성",
    "실행 중 서버의 HTTP·업무 기능 활성화 및 이 프로세스와의 설정 일치",
    "실제 계정 로그인·본인/지정 관리자 권한 및 계정별 기대 업무",
    "공유 적용 승인·백업/복구·두 PC 연결",
    "3번 결과물 화면·제출/검토/첨부/이력 연결",
)
_MESSAGES = {
    "TASK_QUERY_DISABLED": "업무 조회 개발 기능이 꺼져 있습니다. DB에는 접속하지 않았습니다.",
    "TASK_QUERY_ROUTES_REQUIRED": "현재 점검 프로세스의 업무 GET 경로 등록을 확인해 주세요. DB에는 접속하지 않았습니다.",
    "MYSQL_REQUIRED": "MySQL 개발 환경이 필요합니다. SQLite DEMO는 이 점검의 관리 범위가 아닙니다.",
    "TASK_QUERY_SCHEMA_REQUIRED": "업무 조회에 필요한 테이블·열의 준비를 확인해 주세요. 자동 초기화는 하지 않습니다.",
    "COMMON_SCHEMA_VERSION_REQUIRED": "공통 스키마 버전 기록이 필요합니다. 버전 기록을 자동 추가하지 않습니다.",
    "TASK_QUERY_RELATIONS_INVALID": "현재 배정 또는 업무·사용자 참조의 누락을 확인해 주세요. 데이터를 자동 수정하지 않습니다.",
    "TASK_QUERY_DATABASE_UNAVAILABLE": "DB 연결·조회 권한과 준비 상태를 서버 담당자에게 확인해 주세요.",
    "TASK_QUERY_CONFIGURATION_ERROR": "기존 서버 Python 의존성과 공통 환경 설정을 확인해 주세요. 원본 오류·설정값은 출력하지 않습니다.",
    "TASK_QUERY_CHECK_CANCELLED": "점검이 중단됐습니다. 준비 완료로 처리하지 않습니다.",
    "TASK_QUERY_LOCAL_PREREQUISITES_OK": "이 명령 프로세스의 제한된 읽기 선행 조건을 확인했습니다. 실제 서버 연결·권한·전체 스키마 검증 완료를 뜻하지 않습니다.",
}


def _report(code, checks=None, **details):
    ready = code == "TASK_QUERY_LOCAL_PREREQUISITES_OK"
    return {"status": "local_prerequisites_checked" if ready else "preparation_required",
            "code": code, "message": _MESSAGES[code], "checks": checks or {},
            **details, "not_checked": list(_NOT_CHECKED)}


def _relation_counts(session):
    def count(source, condition=None):
        query = select(func.count()).select_from(source)
        if condition is not None:
            query = query.where(condition)
        return int(session.execute(query).scalar_one())

    current_assignment = exists(select(1).select_from(_ASSIGNMENTS).where(and_(
        _ASSIGNMENTS.c.task_id == _TASKS.c.task_id,
        _ASSIGNMENTS.c.revision == _TASKS.c.current_assignment_revision)))
    assignment_parent = exists(select(1).select_from(_TASKS).where(
        _TASKS.c.task_id == _ASSIGNMENTS.c.task_id))
    assignee = exists(select(1).select_from(_USERS).where(
        _USERS.c.id == _ASSIGNMENTS.c.assignee_id))
    manager_parent = exists(select(1).select_from(_TASKS).where(
        _TASKS.c.task_id == _MANAGERS.c.task_id))
    manager_user = exists(select(1).select_from(_USERS).where(
        _USERS.c.id == _MANAGERS.c.user_id))
    return {
        "task_count": count(_TASKS),
        "tasks_missing_current_assignment": count(_TASKS, ~current_assignment),
        "assignments_missing_task": count(_ASSIGNMENTS, ~assignment_parent),
        "assignments_missing_assignee": count(_ASSIGNMENTS, ~assignee),
        "managers_missing_task": count(_MANAGERS, ~manager_parent),
        "managers_missing_user": count(_MANAGERS, ~manager_user),
    }


def check_readiness(app):
    """Return only fixed labels, booleans and counts; make no data mutations."""
    checks = {"query_feature_enabled": app.config.get("ENABLE_TASK_QUERY_DRAFT") is True}
    if not checks["query_feature_enabled"]:
        return _report("TASK_QUERY_DISABLED", checks)
    rules = tuple(app.url_map.iter_rules())
    checks["process_get_routes_registered"] = all(any(
        rule.rule == path and rule.endpoint == endpoint and "GET" in rule.methods
        for rule in rules) for path, endpoint in _ROUTES)
    if not checks["process_get_routes_registered"]:
        return _report("TASK_QUERY_ROUTES_REQUIRED", checks)
    engine = app.extensions["db_engine"]
    checks["mysql"] = engine.dialect.name == "mysql"
    if not checks["mysql"]:
        return _report("MYSQL_REQUIRED", checks)
    try:
        with app.app_context():
            # Reuse the common request-scoped DB session and its rollback-on-close.
            session = db.get_db()
            inspector = inspect(session.connection())
            missing_tables = []
            missing_columns = {}
            for name, required in _REQUIRED_COLUMNS.items():
                if not inspector.has_table(name):
                    missing_tables.append(name)
                    continue
                actual = {item["name"] for item in inspector.get_columns(name)}
                absent = [item for item in required if item not in actual]
                if absent:
                    missing_columns[name] = absent
            checks["required_columns_present"] = not missing_tables and not missing_columns
            if "schema_versions" not in missing_tables and "schema_versions" not in missing_columns:
                version = db.schema_status((db.SCHEMA_VERSION,))
                checks["schema_version_1_recorded"] = not version["missing_versions"]
            if not checks["required_columns_present"]:
                return _report("TASK_QUERY_SCHEMA_REQUIRED", checks,
                               missing_tables=missing_tables, missing_columns=missing_columns)
            if not checks["schema_version_1_recorded"]:
                return _report("COMMON_SCHEMA_VERSION_REQUIRED", checks)
            counts = _relation_counts(session)
            code = ("TASK_QUERY_RELATIONS_INVALID" if any(value for name, value in counts.items()
                                                        if name != "task_count")
                    else "TASK_QUERY_LOCAL_PREREQUISITES_OK")
            return _report(code, checks, counts=counts)
    except SQLAlchemyError:
        return _report("TASK_QUERY_DATABASE_UNAVAILABLE", checks)


def main():
    app = None
    try:
        from ... import create_app
        app = create_app()
        result = check_readiness(app)
    except KeyboardInterrupt:
        result = _report("TASK_QUERY_CHECK_CANCELLED")
    except Exception:
        result = _report("TASK_QUERY_CONFIGURATION_ERROR")
    finally:
        if app is not None:
            try:
                app.extensions["db_engine"].dispose()
            except Exception:
                pass
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0 if result["code"] == "TASK_QUERY_LOCAL_PREREQUISITES_OK" else 1


if __name__ == "__main__":
    sys.exit(main())
