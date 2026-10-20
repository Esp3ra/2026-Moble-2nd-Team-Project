"""Explicit member2 lifecycle verification on a newly owned disposable MySQL.

Reuses the existing identity-checked MySQL/process/runtime ownership helper.
No configured DB, .env, SQLite, public seed route or existing service is used.
The only lifecycle integration acknowledgment is an explicitly labeled TESTING
double with the member3 results blueprint disabled, never real integration.
Safe pytest summaries and cleanup evidence survive outside the deleted runtime.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import difflib
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import sys
import threading
from urllib.error import HTTPError, URLError
from urllib.request import ProxyHandler, Request, build_opener

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve()
sys.path.insert(0, str(ROOT / "Tests" / "TaskQueries.Http.Tests"))
import run_mysql_http_check as owned

_management_spec = importlib.util.spec_from_file_location(
    "member2_management_mysql_helpers", ROOT / "Tests/TaskManagement.Http.Tests/run_mysql_check.py")
management = importlib.util.module_from_spec(_management_spec)
_management_spec.loader.exec_module(management)


def raw_ddl(relative):
    """Accept reviewed CREATE-only draft DDL, without shared migration history."""
    path = ROOT / "RemoteWork.Server" / "migrations" / relative
    source = "\n".join(line for line in path.read_text(encoding="utf-8-sig").splitlines()
                       if not line.lstrip().startswith("--"))
    statements = [part.strip() for part in source.split(";") if part.strip()]
    if not statements or any(not re.match(r"CREATE (?:TABLE|(?:UNIQUE )?INDEX) [a-z_]+\b", part)
                             for part in statements):
        raise owned.shared.RunFailure("only explicit raw CREATE TABLE/INDEX fixture statements are permitted")
    if re.search(r"\b(?:INSERT|UPDATE|DELETE|ALTER|DROP|TRUNCATE|SCHEMA_VERSIONS)\b", source, re.I):
        raise owned.shared.RunFailure("draft fixture DDL must not mutate data or shared schema history")
    return statements


def prepare_schema(env, runtime):
    sys.path.insert(0, str(ROOT / "RemoteWork.Server"))
    from sqlalchemy import create_engine, inspect
    from remotework.models import Base
    engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
    try:
        with engine.connect() as connection:
            owned.validate_instance(connection.exec_driver_sql(owned.IDENTITY_SQL).one(),
                                    runtime / "mysql-data", int(engine.url.port))
        if inspect(engine).get_table_names():
            raise owned.shared.RunFailure("lifecycle checks require a new empty owned database")
        common, _task_tables = owned._table_sets()
        Base.metadata.create_all(engine, tables=common)
        with engine.begin() as connection:
            for name in ("002_task_queries.mysql.sql", "004_write_receipts.mysql.sql",
                         "006_results.mysql.sql", "drafts/task_lifecycle.mysql.sql"):
                for statement in raw_ddl(name):
                    connection.exec_driver_sql(statement)
        return management.ddl_digest(engine), schema_shape(engine)
    finally:
        engine.dispose()


def schema_shape(engine):
    """DDL only, never records, SQL parameters, configured URLs or credentials."""
    from sqlalchemy import inspect
    with engine.connect() as connection:
        return {name: re.sub(r"\sAUTO_INCREMENT=\d+\b", "",
                    connection.exec_driver_sql("SHOW CREATE TABLE `" + name + "`").one()[1])
                for name in sorted(inspect(engine).get_table_names())}


def safe_pytest_report(stdout, exit_code, timeout=False):
    counts = {}
    for amount, label in re.findall(r"\b([0-9]+) (passed|failed|skipped|errors?|warnings?)\b", stdout):
        counts[label.rstrip("s")] = int(amount)
    summary = next((line.strip().strip("= ") for line in reversed(stdout.splitlines())
                    if re.fullmatch(r"[= ]*[0-9]+ (?:passed|failed|skipped|error|errors)"
                                    r"[0-9A-Za-z., :()=+-]* in [0-9.]+s(?: \([0-9:]+\))?[= ]*", line)), None)
    failed = re.findall(r"^(?:FAILED|ERROR) ([A-Za-z0-9_./\\:\[\]-]+)", stdout, re.M)
    nodes = [Path(value.split("::")[0]).name + "::" + value.split("::")[-1] for value in failed[:30]]
    return {"exit_code": exit_code, "timeout": timeout, "counts": counts,
            "safe_summary": summary, "failed_nodes": nodes}


def run_server_pytest(env, runtime, report, selection=None):
    """The child is confined to this run's job and gets only this owned URL."""
    child_env = env.copy()
    child_env.update(MYSQL_TEST_URL=env["DATABASE_URL"],
                     PYTHONPATH=str(ROOT / "RemoteWork.Server"))
    process = job = None
    try:
        job = owned.WindowsJob()
        command = [sys.executable, "-m", "pytest", "-q", "--tb=no",
                   str(ROOT / "RemoteWork.Server/tests/test_task_lifecycle.py")]
        if selection is not None:
            command.extend(["-k", selection])
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen(command, cwd=runtime,
                env=child_env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, stdin=subprocess.DEVNULL,
                text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        try:
            stdout, _stderr = process.communicate(timeout=600)
        except subprocess.TimeoutExpired:
            job.close()
            owned.stop_owned_process(process)
            stdout, _stderr = process.communicate(timeout=20)
            report["pytest"] = safe_pytest_report(stdout, process.returncode, timeout=True)
            raise owned.shared.RunFailure("owned lifecycle server pytest exceeded 600 seconds") from None
        report["pytest"] = safe_pytest_report(stdout, process.returncode)
        if process.returncode != 0:
            labels = ", ".join(report["pytest"]["failed_nodes"])
            raise owned.shared.RunFailure("owned lifecycle server pytest failed" + (": " + labels if labels else ""))
        summary = report["pytest"]["safe_summary"]
        if summary is None:
            raise owned.shared.RunFailure("server pytest succeeded without its expected safe summary")
        print("PASS owned lifecycle MySQL pytest: " + summary + ".", flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


class HttpChecks:
    """Real loopback transport; log only constant assertions, never envelopes."""
    def __init__(self, url, checks):
        self.url, self.checks = url, checks
        self.http = build_opener(ProxyHandler({}))

    def request(self, path, *, method="GET", status=200, token=None, body=None, code=None, key=None):
        headers = {"Accept": "application/json"}
        if token is not None:
            headers["Authorization"] = "Bearer " + token
        if key is not None:
            headers["Idempotency-Key"] = key
        encoded = None
        if body is not None:
            encoded = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        try:
            response = self.http.open(Request(self.url + "api/v1/" + path, data=encoded,
                                               headers=headers, method=method), timeout=20)
        except HTTPError as error:
            response = error
        except (URLError, TimeoutError, OSError):
            raise owned.shared.RunFailure("lifecycle loopback HTTP transport failed") from None
        with response:
            actual, request_id = response.status, response.headers.get("X-Request-ID")
            raw = response.read(1024 * 1024 + 1)
        if len(raw) > 1024 * 1024:
            raise owned.shared.RunFailure("lifecycle loopback response exceeded the test limit")
        try:
            envelope = json.loads(raw)
        except (ValueError, UnicodeError):
            raise owned.shared.RunFailure("lifecycle loopback returned invalid JSON") from None
        self.checks.check(actual == status, "lifecycle HTTP status " + method + " " + path
                          + " expected " + str(status) + " actual " + str(actual))
        self.checks.check(isinstance(envelope, dict) and envelope.get("ok") is (status < 400),
                          "lifecycle common response success flag")
        self.checks.check(bool(request_id) and envelope.get("request_id") == request_id,
                          "lifecycle common request correlation")
        if status >= 400:
            self.checks.check(envelope.get("error", {}).get("code") == code,
                              "lifecycle common error code " + str(code))
            self.checks.check("data" not in envelope, "lifecycle denied response contains no private data")
        return envelope.get("data", {})

    def login(self, account):
        data = self.request("auth/login", method="POST", body={
            "login_id": account["login_id"], "password": account["password"]})
        token = data.get("access_token")
        self.checks.check(isinstance(token, str) and bool(token), "synthetic login supplies a bearer token")
        return token

    def key(self, action, target, token, *, status=201, code=None):
        data = self.request("write-requests/keys", method="POST", status=status, token=token,
                            body={"action": action, "target": target}, code=code)
        if status >= 400:
            return None
        value = data.get("request_key")
        self.checks.check(isinstance(value, str) and bool(value), "common key is explicitly issued")
        return value


def run_http(env, runtime, report, initial_ddl):
    """Exercise real member2 HTTP; external ownership is an explicit test double."""
    from datetime import timedelta
    from sqlalchemy import func, select
    import remotework
    from remotework.auth import create_user
    from remotework.db import get_db, transaction
    from remotework.models import AdminGuard, AuditEvent, SchemaVersion
    from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
    from remotework.features.tasks.lifecycle_support import TEST_DOUBLE_ACK, pending_extension_facts
    from remotework.features.tasks.models import TaskAssignment, TaskRecord
    from remotework.write_requests import WriteReceipt
    from werkzeug.serving import WSGIRequestHandler, make_server

    remotework.load_dotenv = lambda *_args, **_kwargs: False
    app = remotework.create_app({"DATABASE_URL": env["DATABASE_URL"], "SECRET_KEY": secrets.token_urlsafe(48),
        "TESTING": True, "SESSION_HOURS": 1, "COOKIE_SECURE": False,
        "ENABLE_TASK_QUERY_DRAFT": True, "ENABLE_COMMON_WRITE_DRAFT": True,
        "ENABLE_TASK_CREATE_DRAFT": True, "ENABLE_TASK_START_DRAFT": True,
        "ENABLE_TASK_MANAGEMENT_DRAFT": True, "ENABLE_TASK_LIFECYCLE_DRAFT": True,
        "ENABLE_RESULTS_DRAFT": False, "ENABLE_NOTICE_DRAFT": False,
        "ENABLE_CHAT_DRAFT": False, "ENABLE_DEVICE_ENROLLMENT_DRAFT": False})
    app.logger.disabled = True
    app.extensions["task_lifecycle_integration_ack"] = dict(TEST_DOUBLE_ACK)
    checks = owned.shared.Checks()
    accounts = {name: {"login_id": "lifecycle_fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("manager", "admin"), ("worker", "employee"), ("upper", "admin"), ("outsider", "admin"))}
    server = thread = None
    engine = app.extensions["db_engine"]
    try:
        with app.app_context(), transaction() as db:
            db.add(SchemaVersion(version=1))
            db.add(AdminGuard(id=1))
            users = {name: create_user(db, {**account, "display_name": "Lifecycle fixture " + name,
                         "employee_no": "LIFECYCLE-" + name, "department": "Lifecycle fixture"})
                     for name, account in accounts.items()}
            ids = {name: user.id for name, user in users.items()}
        checks.check("results" not in app.blueprints and app.testing is True,
                     "only the explicit TESTING integration double is active")
        checks.check(management.ddl_digest(engine) == initial_ddl,
                     "account preparation and application setup preserve raw DDL")

        class QuietRequestHandler(WSGIRequestHandler):
            def log(self, *_args, **_kwargs):
                pass

        server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=QuietRequestHandler)
        thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.1), daemon=True)
        thread.start()
        http = HttpChecks("http://127.0.0.1:" + str(server.server_port) + "/", checks)
        tokens = {name: http.login(account) for name, account in accounts.items()}
        today = datetime.now(timezone.utc).astimezone(timezone(timedelta(hours=9))).date()
        accepted = {}
        successful = []

        def context(task_id, actor="worker"):
            return http.request("tasks/" + task_id + "/lifecycle", token=tokens[actor])["context"]

        def versions(source):
            return {"expected_row_version": source["row_version"],
                    "expected_assignment_revision": source["assignment_revision"]}

        def write(action, path, actor, body, *, method="POST", status=200, code=None, key=None):
            target = "new" if action == "task.created" else path.split("/")[1]
            key = key or http.key(action, target, tokens[actor])
            result = http.request(path, method=method, status=status, token=tokens[actor], body=body, code=code, key=key)
            if status < 400:
                expected_fields = {"task_id", "row_version", "assignment_revision", "task_status", "updated_at"}
                if action.startswith("task.deadline_extension_") or action == "task.cancelled":
                    expected_fields |= {"extension_id", "requested_due_date", "due_date"}
                checks.check(set(result) == expected_fields, "exact minimal declared success fields")
                successful.append((action, result))
            return result, key

        def create(title, assignee, manager_names):
            result, _key = write("task.created", "tasks", "manager", {
                "title": title, "description": "Lifecycle HTTP fixture original text",
                "start_date": today.isoformat(), "due_date": today.isoformat(),
                "assignee_id": ids[assignee], "manager_user_ids": [ids[name] for name in manager_names]}, status=201)
            return result["task_id"]

        def accept(task_id, actor):
            result, _key = write("task.accepted", "tasks/" + task_id + "/accept", actor, versions(context(task_id, actor)))
            accepted[task_id] = result["updated_at"]

        def request_extension(task_id, days, actor="worker", **options):
            return write("task.deadline_extension_requested", "tasks/" + task_id + "/deadline-extensions", actor,
                {**versions(context(task_id, actor)), "requested_due_date": (today + timedelta(days=days)).isoformat(),
                 "reason": "Explicit requested extension " + str(days)}, **options)

        def decide(task_id, extension_id, outcome, actor, **options):
            body = versions(context(task_id, actor))
            if outcome == "reject":
                body["reason"] = "Explicit rejection reason"
            return write("task.deadline_extension_" + {"approve": "approved", "reject": "rejected", "withdraw": "withdrawn"}[outcome],
                         "tasks/" + task_id + "/deadline-extensions/" + extension_id + "/" + outcome,
                         actor, body, **options)

        main = create("lifecycle-main", "worker", ["manager", "upper"])
        accept(main, "worker")
        first, _ = request_extension(main, 2)
        with app.app_context():
            checks.check(pending_extension_facts(get_db(), int(main), 1) is True,
                         "member2 read provider sees the actual committed pending request")
        request_extension(main, 3, status=409, code="TASK_EXTENSION_PENDING")
        http.request("tasks/" + main + "/lifecycle", status=403, token=tokens["outsider"], code="FORBIDDEN")
        http.request("tasks/extension-requests?scope=managed", status=403, token=tokens["worker"], code="FORBIDDEN")
        checks.check(http.request("tasks/extension-requests?scope=managed", token=tokens["outsider"])["items"] == [],
                     "admin role alone does not expose extension requests")
        before_withdraw = versions(context(main))
        withdrawn, withdraw_key = decide(main, first["extension_id"], "withdraw", "worker")
        replay = http.request("tasks/" + main + "/deadline-extensions/" + first["extension_id"] + "/withdraw",
            method="POST", token=tokens["worker"], body=before_withdraw, key=withdraw_key)
        checks.check(replay == withdrawn, "withdraw replay preserves the original success without a second mutation")
        recovered = http.request("write-requests/result", method="POST", token=tokens["worker"], key=withdraw_key,
            body={"action": "task.deadline_extension_withdrawn", "target": main})
        checks.check(recovered.get("state") == "completed" and recovered.get("result") == withdrawn,
                     "common retained result recovers the successful withdrawal")
        second, _ = request_extension(main, 3)
        approved, _ = decide(main, second["extension_id"], "approve", "manager")
        checks.check(approved["due_date"] == (today + timedelta(days=3)).isoformat()
                     and approved["task_status"] == "in_progress", "approval changes only the exact requested due date")
        third, _ = request_extension(main, 4)
        rejected, _ = decide(main, third["extension_id"], "reject", "upper")
        checks.check(rejected["due_date"] == approved["due_date"], "rejection preserves the previously approved due date")

        with app.app_context():
            db = get_db()
            before_unready = (db.scalar(select(func.count()).select_from(WriteReceipt)),
                              db.scalar(select(func.count()).select_from(TaskDeadlineExtension)),
                              db.get(TaskRecord, int(main)).row_version)
        app.extensions.pop("task_lifecycle_integration_ack")
        checks.check(context(main)["write_enabled"] is False, "unacknowledged lifecycle context clearly disables writes")
        request_extension(main, 5, status=503, code="TASK_LIFECYCLE_INTEGRATION_REQUIRED")
        with app.app_context():
            db = get_db()
            after_unready = (db.scalar(select(func.count()).select_from(WriteReceipt)),
                             db.scalar(select(func.count()).select_from(TaskDeadlineExtension)),
                             db.get(TaskRecord, int(main)).row_version)
        checks.check(before_unready == after_unready, "missing acknowledgment has no task request or receipt effects")
        app.extensions["task_lifecycle_integration_ack"] = dict(TEST_DOUBLE_ACK)

        fourth, _ = request_extension(main, 5)
        write("task.managers_changed", "tasks/" + main + "/managers", "manager",
              {**versions(context(main, "manager")), "manager_user_ids": [ids["manager"]]}, method="PATCH")
        http.key("task.deadline_extension_approved", main, tokens["upper"], status=403, code="FORBIDDEN")
        before_cancel = versions(context(main, "manager"))
        cancel_body = {**before_cancel, "reason": "Explicit permanent cancellation"}
        cancelled, cancel_key = write("task.cancelled", "tasks/" + main + "/cancel", "manager", cancel_body)
        replay = http.request("tasks/" + main + "/cancel", method="POST", token=tokens["manager"], body=cancel_body, key=cancel_key)
        checks.check(replay == cancelled, "cancellation replay remains a retained success after the marker exists")
        current = context(main)
        checks.check(current["is_cancelled"] is True and current["pending_extension"] is None
                     and current["task_status"] == "in_progress", "cancellation preserves five-state workflow and closes pending request")
        request_extension(main, 6, status=409, code="TASK_CANCELLED")
        own_items = http.request("tasks?scope=own", token=tokens["worker"])["items"]
        included = http.request("tasks?scope=own&include_cancelled=true", token=tokens["worker"])["items"]
        checks.check(main not in {row["task_id"] for row in own_items}
                     and any(row["task_id"] == main and row.get("is_cancelled") is True for row in included),
                     "default list hides cancelled tasks while explicit owned history includes them")
        details = http.request("tasks/" + main, token=tokens["worker"])["task"]
        checks.check(details.get("is_cancelled") is True and details["description"] == "Lifecycle HTTP fixture original text",
                     "authorized cancelled detail preserves original task description")
        http.request("tasks/" + main, status=403, token=tokens["outsider"], code="FORBIDDEN")

        admin_own = create("lifecycle-admin-own", "manager", ["manager"])
        accept(admin_own, "manager")
        request_extension(admin_own, 2, "manager", status=409, code="TASK_EXTENSION_DECIDER_REQUIRED")
        write("task.managers_changed", "tasks/" + admin_own + "/managers", "manager",
              {**versions(context(admin_own, "manager")), "manager_user_ids": [ids["manager"], ids["upper"]]}, method="PATCH")
        admin_request, _ = request_extension(admin_own, 2, "manager")
        http.key("task.deadline_extension_approved", admin_own, tokens["manager"], status=403, code="FORBIDDEN")
        decide(admin_own, admin_request["extension_id"], "approve", "upper")
        write("task.cancelled", "tasks/" + admin_own + "/cancel", "upper",
              {**versions(context(admin_own, "upper")), "reason": "Cancel administrator own assignment"})

        unopened = create("lifecycle-unopened", "worker", ["manager"])
        write("task.cancelled", "tasks/" + unopened + "/cancel", "manager",
              {**versions(context(unopened, "manager")), "reason": "Cancel before acceptance"})
        current = context(unopened)
        for action, path, actor, method, extra in (
            ("task.accepted", "tasks/" + unopened + "/accept", "worker", "POST", {}),
            ("task.rework_started", "tasks/" + unopened + "/rework-start", "worker", "POST", {}),
            ("task.updated", "tasks/" + unopened, "manager", "PATCH", {"title": "forbidden cancelled edit", "description": "unchanged",
                "start_date": today.isoformat(), "due_date": today.isoformat()}),
            ("task.reassigned", "tasks/" + unopened + "/reassign", "manager", "POST", {"assignee_id": ids["outsider"]}),
            ("task.managers_changed", "tasks/" + unopened + "/managers", "manager", "PATCH", {"manager_user_ids": [ids["manager"], ids["upper"]]})):
            write(action, path, actor, {**versions(current), **extra}, method=method, status=409, code="TASK_CANCELLED")
        http.request("tasks?include_cancelled=1", status=400, token=tokens["worker"], code="INVALID_INPUT")
        http.request("results/tasks/" + main + "/context", status=404, token=tokens["worker"], code="HTTP_404")

        independent = owned.shared.Checks()
        with app.app_context():
            db = get_db()
            tasks = list(db.scalars(select(TaskRecord).order_by(TaskRecord.task_id)))
            assignments = list(db.scalars(select(TaskAssignment).order_by(TaskAssignment.task_id)))
            extensions = list(db.scalars(select(TaskDeadlineExtension).order_by(TaskDeadlineExtension.extension_id)))
            cancellations = list(db.scalars(select(TaskCancellation)))
            receipts = list(db.scalars(select(WriteReceipt)))
            relevant = {action for action, _result in successful}
            audits = list(db.scalars(select(AuditEvent).where(AuditEvent.action.in_(relevant))))
            independent.check(len(successful) == len(receipts) == len(audits) == 19,
                         "exactly nineteen committed business operations each have one receipt and audit")
            independent.check(len(tasks) == len(assignments) == len(cancellations) == 3 and len(extensions) == 5,
                         "cancellation preserves tasks and original assignments plus all five extension records")
            independent.check([row.status for row in extensions] == ["withdrawn", "approved", "rejected", "cancelled", "approved"],
                         "extension withdrawal approval rejection cancellation and later approval histories remain")
            independent.check(all(row.pending_revision is None and row.decided_at is not None for row in extensions),
                         "every closed request releases the pending uniqueness key without deleting history")
            by_id = {str(row.task_id): row for row in tasks}
            independent.check((by_id[main].row_version, by_id[admin_own].row_version, by_id[unopened].row_version) == (11, 6, 2),
                         "committed row versions count every lifecycle effect exactly once")
            independent.check(all(row.current_assignment_revision == 1 and row.created_by == ids["manager"] for row in tasks),
                         "lifecycle changes preserve original creator and assignment revision")
            independent.check(all(row.description == "Lifecycle HTTP fixture original text" for row in tasks),
                         "cancelled edits never replace original task descriptions")
            for row in assignments:
                key = str(row.task_id)
                expected = None if key not in accepted else datetime.fromisoformat(accepted[key].replace("Z", "+00:00")).replace(tzinfo=None)
                independent.check(row.accepted_at == expected, "first acceptance remains exactly as committed by actual HTTP")
            independent.check(list(db.scalars(select(SchemaVersion.version))) == [1], "global schema version stays one")
            independent.check(management.ddl_digest(engine) == initial_ddl, "all runtime HTTP operations preserve physical DDL")
            independent.check(all(json.loads(row.result_json).get("task_id") in by_id for row in receipts),
                         "retained minimal results refer only to the three actual created tasks")
            independent.check(all(row.action in relevant for row in receipts), "denied and unready writes create no hidden receipt actions")
            from sqlalchemy import text
            independent.check(db.execute(text("SELECT COUNT(*) FROM result_submissions")).scalar_one() == 0
                         and db.execute(text("SELECT COUNT(*) FROM result_decisions")).scalar_one() == 0,
                         "member2 HTTP fixture never writes member3 submission or decision rows")
        report["http"] = {"checks": checks.total + independent.total,
            "http_scenario_checks": checks.total, "independent_database_checks": independent.total,
            "tasks": 3, "assignments": 3, "cancellations": 3,
            "extensions": 5, "receipts": 19, "audits": 19, "schema_versions": [1],
            "ddl_unchanged": True, "results_blueprint": False, "acknowledgment": "explicit TESTING double"}
        print("PASS actual member2 lifecycle loopback HTTP: " + str(checks.total)
              + " scenario checks + " + str(independent.total) + " independent MySQL checks"
              + "; tasks3/extensions5/cancellations3/receipts19/audits19/schema1; explicit TESTING double.", flush=True)
    finally:
        if server is not None:
            server.shutdown()
            server.server_close()
        if thread is not None:
            thread.join(timeout=10)
            if thread.is_alive():
                raise owned.shared.RunFailure("owned lifecycle HTTP thread did not stop")
        engine.dispose()
        report["http_stopped"] = bool(thread is None or not thread.is_alive())


def run(args, report):
    if not args.allow_disposable_mysql:
        raise owned.shared.RunFailure("explicit --allow-disposable-mysql is required; nothing was started")
    if os.name != "nt" or args.mysql_bin_dir is None:
        raise owned.shared.RunFailure("Windows and explicit --mysql-bin-dir are required")
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    required = [ROOT / "RemoteWork.Server/migrations/drafts/task_lifecycle.mysql.sql"]
    if args.server_pytest_only:
        required.append(ROOT / "RemoteWork.Server/tests/test_task_lifecycle.py")
    for path in required:
        if not path.is_file():
            raise owned.shared.RunFailure("member2 lifecycle source and draft SQL must be prepared first")
    import pymysql  # noqa: F401
    import sqlalchemy  # noqa: F401
    import flask  # noqa: F401
    print("Explicit lifecycle fixture: owned MySQL, raw002/raw004/raw006 and unnumbered lifecycle draft; "
          "synthetic accounts and TESTING integration double only; member3 results blueprint disabled.", flush=True)
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                if args.exercise_cleanup_failure:
                    raise owned.shared.RunFailure("intentional failure after owned lifecycle database startup")
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                initial_ddl, initial_shape = prepare_schema(env, runtime)
                report["initial_ddl_sha256"] = initial_ddl
                if args.server_pytest_only:
                    run_server_pytest(env, runtime, report, args.pytest_filter)
                    from sqlalchemy import create_engine
                    engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                    try:
                        with engine.connect() as connection:
                            owned.validate_instance(connection.exec_driver_sql(owned.IDENTITY_SQL).one(),
                                                    runtime / "mysql-data", mysql.port)
                            versions = [row[0] for row in connection.exec_driver_sql(
                                "SELECT version FROM schema_versions ORDER BY version")]
                        final_shape = schema_shape(engine)
                        changed = [name for name in sorted(set(initial_shape) | set(final_shape))
                                   if initial_shape.get(name) != final_shape.get(name)]
                        report["pytest_schema"] = {"ddl_unchanged": not changed, "schema_versions": versions,
                            "changed_tables": changed}
                        if changed:
                            # DDL definitions contain no application rows or
                            # credentials. Preserve only changed lines to make
                            # fixture restoration failures concrete/reviewable.
                            report["pytest_schema"]["definition_differences"] = {
                                name: [line for line in difflib.unified_diff(
                                    initial_shape.get(name, "").splitlines(), final_shape.get(name, "").splitlines(), n=0)
                                    if line.startswith(("+", "-")) and not line.startswith(("+++", "---"))]
                                for name in changed}
                        if versions != [1] or changed:
                            raise owned.shared.RunFailure("server pytest changed physical DDL or global schema history")
                        print("PASS independent pytest schema checks: physical DDL unchanged; global schema1.", flush=True)
                    finally:
                        engine.dispose()
                else:
                    run_http(env, runtime, report, initial_ddl)
            if not mysql.clean:
                raise owned.shared.RunFailure("owned lifecycle MySQL cleanup was not confirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned lifecycle runtime cleanup was not confirmed")
    finally:
        report["cleanup"] = {"mysql_stopped_and_database_removed": bool(mysql is not None and mysql.clean),
                             "runtime_removed": bool(runtime is not None and not runtime.exists())}
        if all(report["cleanup"].values()):
            print("CLEANUP: owned MySQL/process/database and verified temporary runtime removed.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--server-pytest-only", action="store_true")
    parser.add_argument("--pytest-filter", help="Optional pytest -k expression for a justified focused regression")
    parser.add_argument("--exercise-cleanup-failure", action="store_true")
    parser.add_argument("--report-dir", type=Path,
        help="Fresh external folder for safe summary/cleanup evidence; no raw child output or credentials")
    args = parser.parse_args()
    if args.pytest_filter is not None and (not args.server_pytest_only or
            re.fullmatch(r"[A-Za-z0-9_ ()-]{1,200}", args.pytest_filter) is None):
        print("FAIL: pytest-filter requires server-only mode and a simple test-name expression.", file=sys.stderr)
        return 1
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "_" + secrets.token_hex(4)
    audit = (args.report_dir or ROOT.parent / ("remote_work_member2_lifecycle_mysql_" + stamp)).resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be a fresh folder outside the source checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = {"mode": "server_pytest" if args.server_pytest_only else "actual_loopback_http",
              "acknowledgment": "TESTING double; real results blueprint disabled",
              "shared_environment_verified": False, "started_utc": datetime.now(timezone.utc).isoformat()}
    if args.pytest_filter is not None:
        report["pytest_filter"] = args.pytest_filter
    exit_code = 0
    try:
        run(args, report)
    except KeyboardInterrupt:
        exit_code = 130
        report["failure"] = "interrupted"
        print("FAIL: interrupted; owned-resource cleanup was attempted.", file=sys.stderr)
    except Exception as error:
        exit_code = 1
        message = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        report["failure"] = message
        print("FAIL: " + message + "; credentials and arbitrary child output omitted.", file=sys.stderr)
    finally:
        report["exit_code"] = exit_code
        report["finished_utc"] = datetime.now(timezone.utc).isoformat()
        (audit / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("SAFE_REPORT: " + str(audit / "verification.json"), flush=True)
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
