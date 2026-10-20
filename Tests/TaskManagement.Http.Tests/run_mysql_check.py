"""Member2 management HTTP/UI and server checks on newly owned MySQL databases.

Explicit opt-in only. No existing service, .env, configured database or SQLite.
The HTTP/UI mode creates tasks, accepts, submissions and decisions only through
actual APIs. Only synthetic accounts, explicit development reviewer mappings
and raw DDL are prepared by its fixture. Server pytest is a separate mode with
explicit corruption/state/concurrency fixtures. Private client configuration
travels over child stdin only.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import secrets
import shutil
import subprocess
import sys
import threading

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve()
sys.path.insert(0, str(ROOT / "Tests" / "TaskQueries.Http.Tests"))
import run_mysql_http_check as owned


def raw_ddl(name):
    text = "\n".join(line for line in (ROOT / "RemoteWork.Server" / "migrations" / name)
                     .read_text(encoding="utf-8-sig").splitlines()
                     if not line.lstrip().startswith("--"))
    statements = [part.strip() for part in text.split(";") if part.strip()]
    if not statements or any(not re.match(r"CREATE (?:TABLE|(?:UNIQUE )?INDEX) [a-z_]+\b", item)
                             for item in statements):
        raise owned.shared.RunFailure("fixture accepts only explicit raw CREATE TABLE/INDEX statements")
    if re.search(r"\b(?:INSERT|UPDATE|DELETE|ALTER|DROP|TRUNCATE|SCHEMA_VERSIONS)\b", text, re.I):
        raise owned.shared.RunFailure("fixture DDL must not mutate data or global schema history")
    return statements


def ddl_digest(engine):
    from sqlalchemy import inspect
    with engine.connect() as connection:
        # MySQL omits this table option entirely while an AUTO_INCREMENT table
        # is empty. Remove only its live allocator value, not the column's
        # AUTO_INCREMENT attribute, keys, types or any other table option.
        values = {name: re.sub(r"\sAUTO_INCREMENT=\d+\b", "",
                    connection.exec_driver_sql("SHOW CREATE TABLE `" + name + "`").one()[1])
                  for name in sorted(inspect(engine).get_table_names())}
    return hashlib.sha256(json.dumps(values, sort_keys=True).encode()).hexdigest()


def verify_integration(app, ids, initial_ddl):
    """Check committed rows independently of C# success reports."""
    from datetime import datetime
    from sqlalchemy import select
    from remotework.db import get_db
    from remotework.models import AuditEvent, SchemaVersion
    from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
    from remotework.features import results
    from remotework.write_requests import WriteReceipt
    checks = owned.shared.Checks()
    with app.app_context():
        db = get_db()
        tasks = list(db.scalars(select(TaskRecord).order_by(TaskRecord.task_id)))
        assignments = list(db.scalars(select(TaskAssignment)))
        managers = list(db.scalars(select(TaskManager)))
        receipts = list(db.scalars(select(WriteReceipt)))
        submissions = list(db.scalars(select(results.Submission)))
        decisions = list(db.scalars(select(results.Decision)))
        events = list(db.scalars(select(results.ResultEvent)))
        files = list(db.scalars(select(results.Attachment)))
        checks.check((len(tasks), len(assignments), len(managers), len(receipts), len(submissions),
                      len(decisions), len(events), len(files)) == (5, 6, 5, 18, 2, 2, 4, 1),
                     "only successful HTTP business effects exist, each once")
        checks.check(list(db.scalars(select(SchemaVersion.version))) == [1], "global schema stays at version one")
        checks.check(ddl_digest(app.extensions["db_engine"]) == initial_ddl,
                     "runtime changes no DDL or schema history")
        action_counts = {action: sum(item.action == action for item in receipts) for action in
                        ("task.created", "task.updated", "task.reassigned", "task.managers_changed",
                         "task.accepted", "submission.submitted", "submission.returned", "submission.approved")}
        checks.check(action_counts == {"task.created": 5, "task.updated": 2, "task.reassigned": 1,
                     "task.managers_changed": 3, "task.accepted": 3, "submission.submitted": 2,
                     "submission.returned": 1, "submission.approved": 1},
                     "conflicts no ops and denied requests create no receipts")
        audits = list(db.scalars(select(AuditEvent).where(AuditEvent.action.in_(action_counts))))
        checks.check(len(audits) == 18 and all(sum(item.action == action for item in audits) == amount
                     for action, amount in action_counts.items()), "one success audit per committed receipt action")
        by_title = {task.title: task for task in tasks}
        expected = {"management-edited": ("not_started", 4, 2, ids["other_worker"], ids["upper"]),
                    "management-progress": ("in_progress", 3, 1, ids["worker"], ids["upper"]),
                    "management-returned": ("returned", 5, 1, ids["worker"], ids["upper"]),
                    "management-judged": ("approved", 4, 1, ids["worker"], ids["manager"]),
                    "management-conflict-edited": ("not_started", 2, 1, ids["worker"], ids["manager"])}
        checks.check(set(by_title) == set(expected), "no denied title or duplicate task persisted")
        for title, (state, version, revision, assignee, owner) in expected.items():
            task = by_title[title]
            history = sorted([item for item in assignments if item.task_id == task.task_id], key=lambda item: item.revision)
            current = history[-1]
            checks.check((task.task_status, task.row_version, task.current_assignment_revision,
                          current.assignee_id, current.revision) == (state, version, revision, assignee, revision),
                         "actual management status version and current assignment match")
            checks.check({item.user_id for item in managers if item.task_id == task.task_id} == {owner}
                         and task.created_by == ids["manager"], "creator identity survives actual manager replacement")
            retained = [(item, json.loads(item.result_json)) for item in receipts
                        if json.loads(item.result_json).get("task_id") == str(task.task_id)]
            checks.check(len(retained) == version, "exactly one retained receipt per task version")
            if title == "management-edited":
                checks.check(len(history) == 2 and history[0].assignee_id == ids["worker"]
                             and history[0].revision == 1 and history[0].accepted_at is None
                             and current.accepted_at is None and current.assigned_by == ids["manager"],
                             "reassignment appends a new row and preserves the original unaccepted assignment")
            elif state in ("in_progress", "returned", "approved"):
                accepted = [value for receipt, value in retained if receipt.action == "task.accepted"]
                checks.check(len(history) == 1 and len(accepted) == 1 and current.accepted_at ==
                             datetime.fromisoformat(accepted[0]["updated_at"].replace("Z", "+00:00")).replace(tzinfo=None),
                             "original first acceptance timestamp survives all management changes")
        for item in submissions:
            task = next(task for task in tasks if task.task_id == item.task_id)
            judged = [row for row in decisions if row.submission_id == item.id]
            checks.check(item.round_no == 1 and item.assignment_revision == 1 and item.submitted_by == ids["worker"]
                         and item.reviewer_at_submission == ids["manager"] and len(judged) == 1
                         and judged[0].reviewer_id == ids["manager"] and item.status == task.task_status,
                         "actual original results decisions rounds and reviewers are preserved")
        checks.check(len(files) == 1 and files[0].state == "ready" and files[0].size == 80
                     and files[0].submission_id in {item.id for item in submissions},
                     "actual submitted attachment remains attached and ready")
    return {"checks": checks.total, "tasks": 5, "receipts": 18,
            "submissions": 2, "decisions": 2, "events": 4, "schema_versions": [1]}


def fixture_server():
    """Private stdin protocol; there are no public test seed/control endpoints."""
    app = server = thread = None
    message_id = None
    try:
        first = json.loads(sys.stdin.readline())
        message_id = first.get("id")
        if (sys.argv[1:] != ["--fixture-server"] or first.get("action") != "start"
                or os.environ.get("PYTHON_DOTENV_DISABLED") != "1"
                or not Path.cwd().name.startswith(owned.PREFIX)):
            raise owned.shared.RunFailure("explicit owned integration fixture startup is required")
        sys.path.insert(0, str(ROOT / "RemoteWork.Server"))
        import remotework
        from remotework.auth import create_user
        from remotework.db import transaction
        from remotework.models import AdminGuard, Base, SchemaVersion
        from remotework.features import results
        from remotework.write_requests import WriteReceipt
        from sqlalchemy import inspect
        from sqlalchemy.engine import make_url
        from werkzeug.serving import WSGIRequestHandler, make_server
        url = make_url(os.environ["DATABASE_URL"])
        if (url.drivername != "mysql+pymysql" or url.host != "127.0.0.1" or not url.port
                or not re.fullmatch(owned.DATABASE_PATTERN, url.database or "")
                or dict(url.query) != {"charset": "utf8mb4"}):
            raise owned.shared.RunFailure("fixture target must be the newly owned MySQL database")
        remotework.load_dotenv = lambda *_args, **_kwargs: False
        app = remotework.create_app({
            "DATABASE_URL": url, "SECRET_KEY": first["secret"], "TESTING": False,
            "SESSION_HOURS": 1, "COOKIE_SECURE": False,
            "ENABLE_TASK_QUERY_DRAFT": True, "ENABLE_COMMON_WRITE_DRAFT": True,
            "ENABLE_TASK_CREATE_DRAFT": True, "ENABLE_TASK_START_DRAFT": True,
            "ENABLE_TASK_MANAGEMENT_DRAFT": True,
            "ENABLE_RESULTS_DRAFT": True, "ENABLE_NOTICE_DRAFT": False,
            "ENABLE_CHAT_DRAFT": False, "ENABLE_DEVICE_ENROLLMENT_DRAFT": False,
            "RESULTS_CONTEXT_MODE": "local-development", "RESULTS_STORAGE_ROOT": str(Path.cwd() / "files")})
        app.logger.disabled = True
        engine = app.extensions["db_engine"]
        common, task_tables = owned._table_sets()
        with engine.connect() as connection:
            owned.validate_instance(connection.exec_driver_sql(owned.IDENTITY_SQL).one(),
                                    Path.cwd() / "mysql-data", url.port)
        if inspect(engine).get_table_names():
            raise owned.shared.RunFailure("refusing a nonempty integration fixture database")
        Base.metadata.create_all(engine, tables=common)
        with engine.begin() as connection:
            for name in ("002_task_queries.mysql.sql", "004_write_receipts.mysql.sql", "006_results.mysql.sql"):
                for statement in raw_ddl(name):
                    connection.exec_driver_sql(statement)
        with app.app_context(), transaction() as db:
            db.add(SchemaVersion(version=1))
            db.add(AdminGuard(id=1))
            users = {name: create_user(db, {**account, "display_name": "Integration fixture " + name,
                                          "employee_no": "INTEGRATION-" + name, "department": "Integration"})
                     for name, account in first["accounts"].items()}
            ids = {name: user.id for name, user in users.items()}
        expected = {table.name for table in common + task_tables + (WriteReceipt.__table__,)} | {
            table.name for table in results.RESULT_TABLES}
        if set(inspect(engine).get_table_names()) != expected:
            raise owned.shared.RunFailure("unexpected tables in the integration fixture")
        cli = app.test_cli_runner()
        for command in (["results", "set-department-reviewer", "--reviewer-login", first["accounts"]["manager"]["login_id"]],
                        ["results", "set-upper-reviewer", "--submitter-login", first["accounts"]["manager"]["login_id"],
                         "--reviewer-login", first["accounts"]["upper"]["login_id"]]):
            if cli.invoke(args=command).exit_code != 0:
                raise owned.shared.RunFailure("explicit development reviewer configuration failed")
        before_ddl = ddl_digest(engine)

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
                data = {"users": ids}
            elif action == "verify_integration":
                try:
                    data = verify_integration(app, ids, before_ddl)
                except owned.shared.RunFailure as error:
                    # Checks use only the constant labels authored above; no DB
                    # rows, SQL exception text or HTTP envelopes enter this field.
                    data = {"failed_effect": str(error)}
            else:
                raise owned.shared.RunFailure("unknown integration fixture action")
            print(json.dumps({"id": message_id, "ok": True, **data}), flush=True)
        else:
            raise owned.shared.RunFailure("fixture input closed before explicit shutdown")
        server.shutdown()
        server.server_close()
        thread.join(timeout=10)
        if thread.is_alive():
            raise owned.shared.RunFailure("owned integration HTTP server did not stop")
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


def run_client(dotnet, dll, env, runtime, settings):
    process = job = None
    try:
        job = owned.WindowsJob()
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen([dotnet, str(dll), "--http"], cwd=runtime, env=env,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        stdout, stderr = process.communicate(json.dumps(settings), timeout=180)
        if process.returncode != 0:
            match = re.search(r"FAIL TaskManagement integration at ([a-z0-9 -]{1,100}): ([A-Za-z0-9 ._-]{1,200})", stderr)
            suffix = " (" + match.group(1) + ": " + match.group(2) + ")" if match else ""
            raise owned.shared.RunFailure("C# actual task management integration failed" + suffix)
        summary = re.fullmatch(r"PASS TaskManagement integration: ([1-9][0-9]*) checks\.", stdout.strip())
        if summary is None:
            raise owned.shared.RunFailure("integration client did not return its expected safe summary")
        print("PASS TaskManagement integration: " + summary.group(1) + " checks.", flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdin, process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def run_server_pytest(env, runtime):
    """Only the newly owned MySQL; pytest has no route to an existing URL."""
    sys.path.insert(0, str(ROOT / "RemoteWork.Server"))
    from sqlalchemy import create_engine, inspect
    engine = create_engine(env["DATABASE_URL"])
    try:
        with engine.connect() as connection:
            owned.validate_instance(connection.exec_driver_sql(owned.IDENTITY_SQL).one(),
                                    runtime / "mysql-data", int(engine.url.port))
        if inspect(engine).get_table_names():
            raise owned.shared.RunFailure("server checks require an empty newly owned database")
        common, _task_tables = owned._table_sets()
        from remotework.models import Base
        Base.metadata.create_all(engine, tables=common)
        with engine.begin() as connection:
            for name in ("002_task_queries.mysql.sql", "004_write_receipts.mysql.sql", "006_results.mysql.sql"):
                for statement in raw_ddl(name):
                    connection.exec_driver_sql(statement)
    finally:
        engine.dispose()
    child_env = env.copy()
    child_env["MYSQL_TEST_URL"] = env["DATABASE_URL"]
    child_env["DATABASE_URL"] = env["DATABASE_URL"]
    child_env["PYTHONPATH"] = str(ROOT / "RemoteWork.Server")
    process = job = None
    try:
        job = owned.WindowsJob()
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen([sys.executable, "-m", "pytest", "-q",
                str(ROOT / "RemoteWork.Server/tests/test_task_management.py")], cwd=runtime,
                env=child_env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, stdin=subprocess.DEVNULL,
                text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        stdout, _stderr = process.communicate(timeout=600)
        safe_summary = next((line.strip() for line in reversed(stdout.splitlines())
                             if re.fullmatch(r"[0-9]+ passed(?:, [0-9]+ skipped)? in [0-9.]+s", line.strip())), None)
        if process.returncode != 0 or safe_summary is None:
            failed = re.findall(r"^FAILED ([A-Za-z0-9_./\\:\[\]-]+)", stdout, re.M)
            labels = ", ".join(Path(value.split("::")[0]).name + "::" + value.split("::")[-1] for value in failed[:20])
            raise owned.shared.RunFailure("owned MySQL server tests failed" + (": " + labels if labels else ""))
        print("PASS owned MySQL server pytest: " + safe_summary + ".", flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def run(args):
    if not args.allow_disposable_mysql:
        raise owned.shared.RunFailure("explicit --allow-disposable-mysql is required; nothing was started")
    if os.name != "nt" or args.mysql_bin_dir is None:
        raise owned.shared.RunFailure("Windows and an explicit --mysql-bin-dir are required")
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    dll, dotnet = args.client_dll.resolve(), shutil.which("dotnet")
    if not args.server_pytest_only and (not dll.is_file() or dotnet is None):
        raise owned.shared.RunFailure("build TaskManagement.Http.Tests Release and install .NET first")
    import pymysql  # noqa: F401
    import sqlalchemy  # noqa: F401
    import flask  # noqa: F401
    accounts = {name: {"login_id": "fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("manager", "admin"), ("worker", "employee"), ("upper", "admin"),
                                    ("other_worker", "employee"), ("outsider", "admin"))}
    if args.server_pytest_only:
        print("Explicit management server pytest: new owned MySQL, raw002/raw004/raw006; "
              "explicit synthetic state, corruption, rollback and concurrency fixtures.", flush=True)
    else:
        print("Explicit task management fixture: new owned MySQL, raw002/raw004/raw006, synthetic accounts, "
              "local-development reviewer settings and loopback HTTP; no seeded tasks/states/submissions.", flush=True)
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                if args.exercise_cleanup_failure:
                    raise owned.shared.RunFailure("intentional failure after owned integration DB startup")
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                if args.server_pytest_only:
                    run_server_pytest(env, runtime)
                    continue_client = False
                else:
                    continue_client = True
                if continue_client:
                    with owned.shared.FixtureServer(env, runtime, accounts, server_script=SCRIPT) as fixture:
                        ids = fixture.command("identity_ids")["users"]
                        settings = {"server": fixture.url, "storage": str(runtime / "files")}
                        for name, account in accounts.items():
                            settings[name] = {"login_id": account["login_id"], "password": account["password"], "id": ids[name]}
                        run_client(dotnet, dll, env, runtime, settings)
                        verification = fixture.command("verify_integration")
                        if (verification.get("tasks"), verification.get("receipts"), verification.get("submissions"),
                            verification.get("decisions"), verification.get("events"), verification.get("schema_versions")) != (5, 18, 2, 2, 4, [1]):
                            suffix = ": " + verification["failed_effect"] if "failed_effect" in verification else ""
                            raise owned.shared.RunFailure("committed integration effects do not match" + suffix)
                        print("PASS independent MySQL effects: " + str(verification["checks"])
                              + " checks; tasks5/receipts18/submissions2/decisions2/events4/schema1.", flush=True)
            if not mysql.clean:
                raise owned.shared.RunFailure("owned MySQL cleanup was not confirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned runtime cleanup was not confirmed")
    finally:
        if mysql is not None and mysql.clean and runtime is not None and not runtime.exists():
            print("CLEANUP: owned HTTP/MySQL stopped, owned database and verified temporary runtime removed.", flush=True)


def main():
    if sys.argv[1:] == ["--fixture-server"]:
        return fixture_server()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--client-dll", type=Path,
                        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskManagement.Http.Tests.dll")
    parser.add_argument("--exercise-cleanup-failure", action="store_true")
    parser.add_argument("--server-pytest-only", action="store_true",
                        help="Run the member2 management server pytest matrix in a separate new owned MySQL")
    try:
        run(parser.parse_args())
        return 0
    except KeyboardInterrupt:
        print("FAIL: interrupted; owned-resource cleanup was attempted.", file=sys.stderr)
        return 130
    except Exception as error:
        message = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + message + "; credentials and arbitrary child output omitted.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
