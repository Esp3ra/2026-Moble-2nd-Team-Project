"""Private stdin fixture for creation UI checks on a newly owned MySQL instance.

No public seed/control endpoints, configured DB, dotenv, or automatic migrations.
Only the parent run_mysql_check.py creates/owns the native MySQL process and DB.
"""
from pathlib import Path
import json
import os
import re
import sys
import threading

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tests" / "TaskQueries.Http.Tests"))
import run_mysql_http_check as owned


def receipt_ddl():
    source = ROOT / "RemoteWork.Server" / "migrations" / "004_write_receipts.mysql.sql"
    text = "\n".join(line for line in source.read_text(encoding="utf-8-sig").splitlines()
                     if not line.lstrip().startswith("--"))
    statements = [part.strip() for part in text.split(";") if part.strip()]
    if (len(statements) != 1 or not statements[0].startswith("CREATE TABLE write_receipts (")
            or any(word in text.upper() for word in ("SCHEMA_VERSIONS", "DROP TABLE", "IF NOT EXISTS"))):
        raise owned.shared.RunFailure("raw004 must contain only the expected receipt table creation")
    return statements


def verify_created(app, user_ids):
    """Check committed rows independently of C# success flags; expose no rows."""
    from sqlalchemy import select
    from remotework.db import get_db
    from remotework.models import AuditEvent, SchemaVersion, iso
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.write_requests import WriteReceipt
    checks = owned.shared.Checks()
    with app.app_context():
        db = get_db()
        tasks = list(db.scalars(select(TaskRecord).order_by(TaskRecord.task_id)))
        assignments = list(db.scalars(select(TaskAssignment)))
        managers = list(db.scalars(select(TaskManager)))
        audits = list(db.scalars(select(AuditEvent).where(AuditEvent.action == "task.created")))
        receipts = list(db.scalars(select(WriteReceipt)))
        checks.check((len(tasks), len(assignments), len(managers), len(audits), len(receipts)) == (3, 3, 6, 3, 3),
                     "exactly three creations with one assignment/audit/receipt and two managers each")
        checks.check({task.title for task in tasks} ==
                     {"creation-live-ui", "creation-live-lost", "creation-live-retry"},
                     "all three intended scenarios create exactly one task")
        checks.check(list(db.scalars(select(SchemaVersion.version))) == [1],
                     "global schema version remains one")
        expected_managers = {user_ids["admin"], user_ids["other_admin"]}
        for task in tasks:
            checks.check(task.task_status == "not_started" and task.row_version == 1
                         and task.current_assignment_revision == 1 and task.created_by == user_ids["admin"],
                         "initial status/revisions and server creator match")
            related = [item for item in assignments if item.task_id == task.task_id]
            checks.check(len(related) == 1 and related[0].revision == 1
                         and related[0].assignee_id == user_ids["staff"]
                         and related[0].assigned_by == user_ids["admin"]
                         and related[0].accepted_at is None,
                         "only one initial, unaccepted assignment belongs to the intended staff member")
            checks.check({item.user_id for item in managers if item.task_id == task.task_id} == expected_managers,
                         "creator is retained with the selected additional manager")
            related_audits = [item for item in audits if item.resource_id == str(task.task_id)]
            checks.check(len(related_audits) == 1 and related_audits[0].actor_id == user_ids["admin"]
                         and related_audits[0].resource_type == "task",
                         "creation audit is recorded once by the authenticated creator")
            details = json.loads(related_audits[0].details_json)
            checks.check(details == {"row_version": "1", "assignment_revision": 1,
                                    "assignee_id": user_ids["staff"],
                                    "manager_user_ids": sorted(expected_managers)},
                         "creation audit contains the expected minimal relation evidence")
            related_receipts = [item for item in receipts
                                if json.loads(item.result_json).get("task_id") == str(task.task_id)]
            checks.check(len(related_receipts) == 1 and related_receipts[0].user_id == user_ids["admin"]
                         and related_receipts[0].action == "task.created"
                         and related_receipts[0].target == "new" and related_receipts[0].http_status == 201,
                         "one retained result exists per committed creation")
            result = json.loads(related_receipts[0].result_json)
            checks.check(result == {"task_id": str(task.task_id), "row_version": "1",
                                   "assignment_revision": 1, "task_status": "not_started",
                                   "updated_at": iso(task.updated_at)},
                         "retained result agrees with persisted task identity/version/time")
    return {"checks": checks.total, "tasks": 3, "assignments": 3, "managers": 6,
            "creation_audits": 3, "receipts": 3, "schema_versions": [1]}


def main():
    app = server = thread = None
    message_id = None
    try:
        first = json.loads(sys.stdin.readline())
        message_id = first.get("id")
        if (sys.argv[1:] != ["--fixture-server"] or first.get("action") != "start"
                or os.environ.get("PYTHON_DOTENV_DISABLED") != "1"
                or not Path.cwd().name.startswith(owned.PREFIX)):
            raise owned.shared.RunFailure("explicit owned creation fixture startup is required")
        sys.path.insert(0, str(ROOT / "RemoteWork.Server"))
        import remotework
        from remotework.auth import create_user
        from remotework.db import transaction
        from remotework.models import AdminGuard, Base, SchemaVersion
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
                                    "ENABLE_TASK_CREATE_DRAFT": True, "ENABLE_NOTICE_DRAFT": False,
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
            users = {name: create_user(db, {**account, "display_name": "Creation fixture " + name,
                                          "employee_no": "CREATE-" + name, "department": "Test fixture"})
                     for name, account in first["accounts"].items()}
            user_ids = {name: user.id for name, user in users.items()}
        with engine.connect() as connection:
            before = owned._digest_rows(connection, common)
        with engine.begin() as connection:
            for statement in owned._ddl_statements() + receipt_ddl():
                connection.exec_driver_sql(statement)
        with engine.connect() as connection:
            if owned._digest_rows(connection, common) != before:
                raise owned.shared.RunFailure("raw002/raw004 changed common fixture rows")
            if connection.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version").all() != [(1,)]:
                raise owned.shared.RunFailure("fixture schema version must remain one")
        expected_tables = {table.name for table in common + task_tables + (WriteReceipt.__table__,)}
        if set(inspect(engine).get_table_names()) != expected_tables:
            raise owned.shared.RunFailure("unexpected application tables in creation fixture")

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
            elif action == "verify_created":
                data = verify_created(app, user_ids)
            else:
                raise owned.shared.RunFailure("unknown creation fixture control action")
            print(json.dumps({"id": message_id, "ok": True, **data}), flush=True)
        else:
            raise owned.shared.RunFailure("fixture input closed without explicit shutdown")
        server.shutdown()
        server.server_close()
        thread.join(timeout=10)
        if thread.is_alive():
            raise owned.shared.RunFailure("owned creation HTTP server thread did not stop")
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
