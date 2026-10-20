"""Member2 -> member3 real HTTP/UI integration on a newly owned MySQL database.

Explicit opt-in only. No existing service, .env, configured database or SQLite.
Tasks, accepts, submissions, decisions and rework all use the actual HTTP API.
Only synthetic accounts, explicit development reviewer mappings and raw DDL are
prepared by the fixture. Private configuration travels over child stdin only.
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
    """Read committed effects independently of UI/client success reports."""
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
                      len(decisions), len(events), len(files)) == (3, 3, 3, 15, 4, 4, 8, 2),
                     "one effect for each HTTP action, and no fabricated submissions or task rows")
        checks.check(list(db.scalars(select(SchemaVersion.version))) == [1],
                     "global schema remains version one")
        checks.check({task.title for task in tasks} ==
                     {"integration-distractor", "integration-employee", "integration-admin-own"},
                     "only the three intended HTTP creations exist")
        checks.check(ddl_digest(app.extensions["db_engine"]) == initial_ddl,
                     "runtime flow leaves all DDL unchanged apart from allocator position")
        feature_actions = {"task.created", "task.accepted", "task.rework_started",
                           "submission.submitted", "submission.returned", "submission.approved"}
        audits = list(db.scalars(select(AuditEvent).where(AuditEvent.action.in_(feature_actions))))
        checks.check(len(audits) == 15, "each committed business action records exactly one success audit")
        for task in tasks:
            related = [item for item in assignments if item.task_id == task.task_id]
            checks.check(len(related) == 1 and related[0].revision == task.current_assignment_revision == 1,
                         "initial assignment revision and historical assignment row are preserved")
            assignment = related[0]
            checks.check({item.user_id for item in managers if item.task_id == task.task_id} == {ids["manager"]},
                         "registration creator manager is preserved")
            retained = [(item, json.loads(item.result_json)) for item in receipts
                        if json.loads(item.result_json).get("task_id") == str(task.task_id)]
            if task.title == "integration-distractor":
                checks.check(task.task_status == "not_started" and task.row_version == 1
                             and assignment.accepted_at is None and len(retained) == 1,
                             "unselected task remains exactly at its original creation")
                continue
            expected_owner = ids["worker"] if task.title == "integration-employee" else ids["manager"]
            expected_reviewer = ids["manager"] if task.title == "integration-employee" else ids["upper"]
            checks.check(task.task_status == "approved" and task.row_version == 7
                         and assignment.assignee_id == expected_owner and len(retained) == 7,
                         "register accept submit return rework resubmit approve produce version seven")
            accepted = [value for receipt, value in retained if receipt.action == "task.accepted"]
            checks.check(len(accepted) == 1 and assignment.accepted_at ==
                         datetime.fromisoformat(accepted[0]["updated_at"].replace("Z", "+00:00")).replace(tzinfo=None),
                         "first accepted time still equals the original committed accept receipt")
            rounds = sorted([item for item in submissions if item.task_id == task.task_id], key=lambda item: item.round_no)
            checks.check([item.round_no for item in rounds] == [1, 2]
                         and [item.status for item in rounds] == ["returned", "approved"]
                         and [item.description for item in rounds] == ["Integration result 1", "Integration result 2"],
                         "both actual rounds and descriptions survive rework")
            checks.check(all(item.submitted_by == expected_owner and item.reviewer_at_submission == expected_reviewer
                             and item.assignment_revision == 1 for item in rounds),
                         "employee and admin own submissions use their explicit distinct reviewers")
            judged = [item for item in decisions if item.submission_id in {row.id for row in rounds}]
            checks.check(len(judged) == 2 and all(item.reviewer_id == expected_reviewer for item in judged),
                         "only the current nonself reviewer creates the two decisions")
            history = [item for item in events if item.task_id == task.task_id]
            checks.check(sorted(item.kind for item in history) == ["approved", "returned", "submitted", "submitted"],
                         "actual submission and decision events remain exactly once")
            attached = [item for item in files if item.task_id == task.task_id]
            checks.check(len(attached) == 1 and attached[0].submission_id == rounds[0].id
                         and attached[0].state == "ready" and attached[0].size == 4096,
                         "first-round uploaded file remains attached and ready")
    return {"checks": checks.total, "tasks": 3, "receipts": 15,
            "submissions": 4, "decisions": 4, "events": 8, "schema_versions": [1]}


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
            match = re.search(r"FAIL TaskResults integration at ([a-z0-9 -]{1,100}): ([A-Za-z0-9 ._-]{1,200})", stderr)
            suffix = " (" + match.group(1) + ": " + match.group(2) + ")" if match else ""
            raise owned.shared.RunFailure("C# actual task/results integration failed" + suffix)
        summary = re.fullmatch(r"PASS TaskResults integration: ([1-9][0-9]*) checks\.", stdout.strip())
        if summary is None:
            raise owned.shared.RunFailure("integration client did not return its expected safe summary")
        print("PASS TaskResults integration: " + summary.group(1) + " checks.", flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdin, process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def run(args):
    if not args.allow_disposable_mysql:
        raise owned.shared.RunFailure("explicit --allow-disposable-mysql is required; nothing was started")
    if os.name != "nt" or args.mysql_bin_dir is None:
        raise owned.shared.RunFailure("Windows and an explicit --mysql-bin-dir are required")
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    dll, dotnet = args.client_dll.resolve(), shutil.which("dotnet")
    if not dll.is_file() or dotnet is None:
        raise owned.shared.RunFailure("build TaskResults.Integration.Tests Release and install .NET first")
    import pymysql  # noqa: F401
    import sqlalchemy  # noqa: F401
    import flask  # noqa: F401
    accounts = {name: {"login_id": "fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("manager", "admin"), ("worker", "employee"), ("upper", "admin"))}
    print("Explicit task/results fixture: new owned MySQL, raw002/raw004/raw006, synthetic accounts, "
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
                with owned.shared.FixtureServer(env, runtime, accounts, server_script=SCRIPT) as fixture:
                    ids = fixture.command("identity_ids")["users"]
                    settings = {"server": fixture.url, "storage": str(runtime / "files")}
                    for name, account in accounts.items():
                        settings[name] = {"login_id": account["login_id"], "password": account["password"], "id": ids[name]}
                    run_client(dotnet, dll, env, runtime, settings)
                    verification = fixture.command("verify_integration")
                    if (verification.get("tasks"), verification.get("receipts"), verification.get("submissions"),
                        verification.get("decisions"), verification.get("events"), verification.get("schema_versions")) != (3, 15, 4, 4, 8, [1]):
                        suffix = ": " + verification["failed_effect"] if "failed_effect" in verification else ""
                        raise owned.shared.RunFailure("committed integration effects do not match" + suffix)
                    print("PASS independent MySQL effects: " + str(verification["checks"])
                          + " checks; tasks3/receipts15/submissions4/decisions4/events8/schema1.", flush=True)
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
                        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskResults.Integration.Tests.dll")
    parser.add_argument("--exercise-cleanup-failure", action="store_true")
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
