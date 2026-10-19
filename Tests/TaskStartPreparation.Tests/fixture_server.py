"""Private read-only preparation fixture on a new owned MySQL instance.

Rows are synthetic read examples, not successful acceptance/submission records.
The returned rows' historical accepted_at values are seeded directly, without
product actions, submissions or reviews. This does not settle start/write rules.
"""
from datetime import date, datetime
from pathlib import Path
import json
import os
import re
import sys
import threading

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tests" / "TaskQueries.Http.Tests"))
import run_mysql_http_check as owned

CASES = ((101, "staff", "not_started"), (102, "staff", "returned"),
         (103, "admin", "not_started"), (104, "admin", "returned"),
         (105, "other_staff", "returned"))
HISTORICAL_ACCEPTED_UTC = datetime(2026, 10, 15, 9, 0, 0)
SEEDED_UTC = datetime(2026, 10, 14, 0, 0, 0)


def receipt_ddl():
    source = ROOT / "RemoteWork.Server" / "migrations" / "004_write_receipts.mysql.sql"
    text = "\n".join(line for line in source.read_text(encoding="utf-8-sig").splitlines()
                     if not line.lstrip().startswith("--"))
    statements = [part.strip() for part in text.split(";") if part.strip()]
    if (len(statements) != 1 or not statements[0].startswith("CREATE TABLE write_receipts (")
            or any(word in text.upper() for word in ("SCHEMA_VERSIONS", "DROP TABLE", "IF NOT EXISTS"))):
        raise owned.shared.RunFailure("raw004 must contain only the expected receipt table creation")
    return statements


def verify_readonly(app, user_ids, task_tables, expected_tables, baseline):
    """Compare every task column independently of the C# result; return counts only."""
    from sqlalchemy import func, inspect, or_, select
    from remotework.models import AuditEvent, SchemaVersion
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.write_requests import WriteReceipt
    checks = owned.shared.Checks()
    engine = app.extensions["db_engine"]
    with engine.connect() as connection:
        for table in task_tables:
            checks.check(owned._digest_rows(connection, (table,)) == baseline[table.name],
                         "complete task table rows are unchanged")
        counts = tuple(connection.scalar(select(func.count()).select_from(table)) for table in task_tables)
        checks.check(counts == (5, 5, 5), "only the five synthetic task relationships remain")
        checks.check(connection.scalar(select(func.count()).select_from(WriteReceipt)) == 0,
                     "preparation did not retain a write result")
        checks.check(connection.scalar(select(func.count()).select_from(AuditEvent).where(or_(
            AuditEvent.action.like("task.%"), AuditEvent.resource_type == "task"))) == 0,
                     "no task product action audit was written")
        checks.check(list(connection.scalars(select(SchemaVersion.version))) == [1],
                     "global schema version remains one")
        checks.check(set(inspect(connection).get_table_names()) == expected_tables,
                     "only common, task and receipt tables exist")
        for task_id, assignee, status in CASES:
            task = connection.execute(select(TaskRecord.__table__).where(
                TaskRecord.task_id == task_id)).mappings().one()
            assignment = connection.execute(select(TaskAssignment.__table__).where(
                TaskAssignment.task_id == task_id)).mappings().one()
            manager_ids = list(connection.scalars(select(TaskManager.user_id).where(
                TaskManager.task_id == task_id)))
            accepted_at = None if status == "not_started" else HISTORICAL_ACCEPTED_UTC
            checks.check(task["task_status"] == status and task["row_version"] == 1
                         and task["current_assignment_revision"] == 1
                         and task["created_by"] == user_ids["admin"]
                         and assignment["revision"] == 1 and assignment["assignee_id"] == user_ids[assignee]
                         and assignment["assigned_by"] == user_ids["admin"]
                         and assignment["accepted_at"] == accepted_at and manager_ids == [user_ids["admin"]],
                         "synthetic status, current assignee and acceptance example are unchanged")
    return {"checks": checks.total, "tasks": 5, "assignments": 5, "managers": 5,
            "task_audits": 0, "receipts": 0, "schema_versions": [1], "all_task_rows_unchanged": True}


def main():
    app = server = thread = None
    message_id = None
    try:
        first = json.loads(sys.stdin.readline())
        message_id = first.get("id")
        if (sys.argv[1:] != ["--fixture-server"] or first.get("action") != "start"
                or os.environ.get("PYTHON_DOTENV_DISABLED") != "1"
                or not Path.cwd().name.startswith(owned.PREFIX)):
            raise owned.shared.RunFailure("explicit owned start preparation fixture startup is required")
        sys.path.insert(0, str(ROOT / "RemoteWork.Server"))
        import remotework
        from remotework.auth import create_user
        from remotework.db import transaction
        from remotework.models import AdminGuard, Base, SchemaVersion
        from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
        from remotework.write_requests import WriteReceipt
        from sqlalchemy import inspect
        from sqlalchemy.engine import make_url
        from werkzeug.serving import WSGIRequestHandler, make_server
        url = make_url(os.environ["DATABASE_URL"])
        if (url.drivername != "mysql+pymysql" or url.host != "127.0.0.1" or not url.port
                or not re.fullmatch(owned.DATABASE_PATTERN, url.database or "")
                or dict(url.query) != {"charset": "utf8mb4"}):
            raise owned.shared.RunFailure("fixture target must be the new owned MySQL database")
        remotework.load_dotenv = lambda *_args, **_kwargs: False
        app = remotework.create_app({"DATABASE_URL": url, "SECRET_KEY": first["secret"],
                                    "TESTING": False, "SESSION_HOURS": 1, "COOKIE_SECURE": False,
                                    "ENABLE_TASK_QUERY_DRAFT": True, "ENABLE_COMMON_WRITE_DRAFT": True,
                                    "ENABLE_TASK_CREATE_DRAFT": False, "ENABLE_NOTICE_DRAFT": False,
                                    "WRITE_REQUEST_TTL_SECONDS": 86400})
        app.logger.disabled = True
        engine = app.extensions["db_engine"]
        common, task_tables = owned._table_sets()
        with engine.connect() as connection:
            owned.validate_instance(connection.exec_driver_sql(owned.IDENTITY_SQL).one(),
                                    Path.cwd() / "mysql-data", url.port)
        if inspect(engine).get_table_names():
            raise owned.shared.RunFailure("refusing to initialize a nonempty fixture database")
        Base.metadata.create_all(engine, tables=common)
        with app.app_context(), transaction() as db:
            db.add(SchemaVersion(version=1))
            db.add(AdminGuard(id=1))
            users = {name: create_user(db, {**account, "display_name": "Start fixture " + name,
                                          "employee_no": "START-" + name, "department": "Test fixture"})
                     for name, account in first["accounts"].items()}
            user_ids = {name: user.id for name, user in users.items()}
        with engine.connect() as connection:
            common_before = owned._digest_rows(connection, common)
        with engine.begin() as connection:
            for statement in owned._ddl_statements() + receipt_ddl():
                connection.exec_driver_sql(statement)
        with engine.connect() as connection:
            if owned._digest_rows(connection, common) != common_before:
                raise owned.shared.RunFailure("raw002/raw004 changed common fixture rows")
            if connection.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version").all() != [(1,)]:
                raise owned.shared.RunFailure("fixture schema version must remain one")
        expected_tables = {table.name for table in common + task_tables + (WriteReceipt.__table__,)}
        if set(inspect(engine).get_table_names()) != expected_tables:
            raise owned.shared.RunFailure("unexpected application tables in start preparation fixture")
        with app.app_context(), transaction() as db:
            for task_id, assignee, status in CASES:
                db.add(TaskRecord(task_id=task_id, title="Start preparation fixture " + str(task_id),
                                  description="Synthetic read example; no submission or transition performed.",
                                  task_status=status, start_date=date(2026, 10, 14), due_date=date(2026, 10, 22),
                                  current_assignment_revision=1, row_version=1, created_by=user_ids["admin"],
                                  created_at=SEEDED_UTC, updated_at=SEEDED_UTC))
                db.flush()
                db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=user_ids[assignee],
                                      assigned_by=user_ids["admin"], assigned_at=SEEDED_UTC,
                                      accepted_at=None if status == "not_started" else HISTORICAL_ACCEPTED_UTC))
                db.add(TaskManager(task_id=task_id, user_id=user_ids["admin"]))
        with engine.connect() as connection:
            baseline = {table.name: owned._digest_rows(connection, (table,)) for table in task_tables}

        class QuietRequestHandler(WSGIRequestHandler):
            def log(self, *_args, **_kwargs):
                pass

        server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=QuietRequestHandler)
        thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.1), daemon=True)
        thread.start()
        print(json.dumps({"id": message_id, "ok": True, "port": server.server_port}), flush=True)
        for line in sys.stdin:
            message = json.loads(line)
            message_id, action = message.get("id"), message.get("action")
            if action == "stop":
                break
            if action == "identity_ids":
                data = {"users": user_ids}
            elif action == "verify_readonly":
                data = verify_readonly(app, user_ids, task_tables, expected_tables, baseline)
            else:
                raise owned.shared.RunFailure("unknown start preparation fixture control action")
            print(json.dumps({"id": message_id, "ok": True, **data}), flush=True)
        else:
            raise owned.shared.RunFailure("fixture input closed without explicit shutdown")
        server.shutdown()
        server.server_close()
        thread.join(timeout=10)
        if thread.is_alive():
            raise owned.shared.RunFailure("owned start preparation HTTP server thread did not stop")
        engine.dispose()
        app = server = None
        print(json.dumps({"id": message_id, "ok": True}), flush=True)
        return 0
    except BaseException as error:
        print(json.dumps({"id": message_id, "ok": False, "failure_type": type(error).__name__}), flush=True)
        return 1
    finally:
        if server is not None:
            if thread is not None and thread.is_alive():
                server.shutdown()
            server.server_close()
        if app is not None:
            app.extensions["db_engine"].dispose()


if __name__ == "__main__":
    sys.exit(main())
