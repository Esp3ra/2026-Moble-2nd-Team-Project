"""Owner2 C# -> loopback TCP HTTP -> real shared provider/files on owned MySQL.

Only freshly owned resources and synthetic identities are used. Credentials are
passed to the client through private stdin; arbitrary child output is suppressed.
No existing server, .env, operational DB/ACK, SQLite or feature code is changed.
"""
from datetime import datetime, timedelta, timezone
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import sys
import threading

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve()
spec = importlib.util.spec_from_file_location("upload_owned_helpers",
    ROOT / "Tests/TaskLifecycle.Integration.Tests/run_mysql_check.py")
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
owned = helpers.owned


def client(dotnet, dll, env, runtime, settings, report):
    process = job = None
    try:
        job = owned.WindowsJob()
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen([dotnet, str(dll), "--http"], cwd=runtime, env=env,
                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                text=True, encoding="utf-8",
                creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        stdout, stderr = process.communicate(json.dumps(settings), timeout=180)
        if process.returncode:
            safe = re.fullmatch(r"FAIL TaskCreationUploads HTTP after ([0-9]+) checks; details omitted\.", stderr.strip())
            report["client_failure_after"] = int(safe.group(1)) if safe else None
            raise owned.shared.RunFailure("C# upload HTTP checks failed; arbitrary child output suppressed")
        safe = re.fullmatch(r"PASS TaskCreationUploads HTTP: ([1-9][0-9]*) checks\.", stdout.strip())
        if not safe:
            raise owned.shared.RunFailure("C# upload client returned no recognized safe summary")
        report["client_checks"] = int(safe.group(1))
        print(safe.group(0), flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdin, process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def run_http(env, runtime, dll, dotnet, report, shape):
    from sqlalchemy import func, select
    import remotework
    from remotework.auth import create_user
    from remotework.db import get_db, transaction
    from remotework.models import AdminGuard, AuditEvent, SchemaVersion
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    from remotework.write_requests import WriteReceipt
    from remotework.features import shared_attachments
    from werkzeug.serving import WSGIRequestHandler, make_server

    remotework.load_dotenv = lambda *_a, **_kw: False
    config = {"DATABASE_URL": env["DATABASE_URL"], "SECRET_KEY": secrets.token_urlsafe(48),
        "TESTING": True, "SESSION_HOURS": 1, "COOKIE_SECURE": False,
        "RESULTS_STORAGE_ROOT": str(runtime / "files"), "RESULTS_CONTEXT_MODE": ""}
    for feature in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "RESULTS", "SHARED_ATTACHMENTS"):
        config["ENABLE_" + feature + "_DRAFT"] = True
    for feature in ("TASK_START", "TASK_MANAGEMENT", "TASK_LIFECYCLE", "NOTICE", "CHAT",
                    "DEVICE_ENROLLMENT", "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        config["ENABLE_" + feature + "_DRAFT"] = False
    app = remotework.create_app(config)
    app.logger.disabled = True
    accounts = {name: {"login_id": "upload_fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
        for name, role in (("admin", "admin"), ("staff", "employee"), ("outsider", "admin"), ("other_staff", "employee"))}
    checks = owned.shared.Checks()
    engine = app.extensions["db_engine"]
    server = thread = None
    try:
        checks.check(app.extensions.get("results_shared_attachments") is shared_attachments,
                     "actual registered shared attachment provider")
        checks.check("task_lifecycle_integration_ack" not in app.extensions, "no deployment or test lifecycle ACK")
        with app.app_context(), transaction() as db:
            db.add(SchemaVersion(version=1))
            db.add(AdminGuard(id=1))
            users = {name: create_user(db, {**account, "display_name": "Upload fixture " + name,
                "employee_no": "UPLOAD-" + name, "department": "Upload fixture"}) for name, account in accounts.items()}
            ids = {name: user.id for name, user in users.items()}
        class QuietHandler(WSGIRequestHandler):
            def log(self, *_a, **_kw):
                pass
        server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=QuietHandler)
        thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.1), daemon=True)
        thread.start()
        settings = {"url": "http://127.0.0.1:" + str(server.server_port) + "/",
                    "scratch_dir": str(runtime), "admin_user_id": ids["admin"], "assignee_id": ids["staff"],
                    "today": datetime.now(timezone(timedelta(hours=9))).date().isoformat()}
        for name, account in accounts.items():
            settings[name + "_login_id"] = account["login_id"]
            settings[name + "_password"] = account["password"]
        client(dotnet, dll, env, runtime, settings, report)
        with app.app_context():
            db = get_db()
            counts = {name: db.scalar(select(func.count()).select_from(model)) for name, model in (
                ("tasks", TaskRecord), ("assignments", TaskAssignment), ("managers", TaskManager),
                ("receipts", WriteReceipt), ("files", shared_attachments.SharedFile),
                ("chunks", shared_attachments.SharedChunk), ("links", shared_attachments.SharedLink))}
            checks.check(counts == dict(tasks=1, assignments=1, managers=1, receipts=1, files=3, chunks=3, links=2),
                         "committed row counts exclude duplicate chunk and creation writes")
            task = db.scalars(select(TaskRecord)).one()
            checks.check((task.row_version, task.current_assignment_revision, task.task_status) == (1, 1, "not_started"),
                         "creation initial versions and status remain exact")
            files = list(db.scalars(select(shared_attachments.SharedFile)))
            checks.check(sorted(f.state for f in files) == ["ready", "ready", "uploading"],
                         "lost start remains an unlinked temporary upload")
            links = list(db.scalars(select(shared_attachments.SharedLink)))
            checks.check(all(link.task_id == task.task_id and link.resource_type == "task"
                             and link.attachment_id in {f.id for f in files if f.state == "ready"} for link in links),
                         "only completed files link to the created task")
            checks.check(db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "task.created")) == 1,
                         "one creation audit despite receipt replay")
            checks.check(list(db.scalars(select(SchemaVersion.version))) == [1], "global schema version1 unchanged")
            report["committed_counts"] = counts
        checks.check(helpers.schema_shape(engine) == shape, "physical schema unchanged after actual HTTP")
        report["database_checks"] = checks.total
        report["ddl_unchanged"] = True
        print("PASS owned upload database: " + str(checks.total) + " checks; schema1 and physical DDL unchanged.", flush=True)
    finally:
        if server is not None:
            server.shutdown()
            if thread is not None:
                thread.join(timeout=10)
            server.server_close()
            report["http_stopped"] = thread is None or not thread.is_alive()
        engine.dispose()


def run(args, report):
    if not args.allow_disposable_mysql or os.name != "nt" or args.mysql_bin_dir is None:
        raise owned.shared.RunFailure("Windows, explicit --allow-disposable-mysql and --mysql-bin-dir are required")
    binary = owned.validate_binary_directory(args.mysql_bin_dir)
    dll = args.client_dll.resolve()
    dotnet = shutil.which("dotnet")
    if not dll.is_file() or dotnet is None:
        raise owned.shared.RunFailure("build TaskCreationUploads.Tests Release first")
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(binary, runtime, env) as mysql:
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                helpers.prepare_schema(env, runtime)
                from sqlalchemy import create_engine
                engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                try:
                    with engine.begin() as connection:
                        for statement in helpers.raw_ddl("013_shared_attachments.mysql.sql"):
                            connection.exec_driver_sql(statement)
                    shape = helpers.schema_shape(engine)
                finally:
                    engine.dispose()
                run_http(env, runtime, dll, dotnet, report, shape)
            if not mysql.clean:
                raise owned.shared.RunFailure("owned upload MySQL cleanup unconfirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned upload runtime cleanup unconfirmed")
    finally:
        report["cleanup"] = {"mysql_stopped_and_database_removed": bool(mysql and mysql.clean),
                             "runtime_removed": bool(runtime and not runtime.exists())}
        if all(report["cleanup"].values()):
            print("CLEANUP: owned upload HTTP/MySQL/database/files/runtime removed.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--client-dll", type=Path,
        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskCreationUploads.Tests.dll")
    parser.add_argument("--report-dir", type=Path, required=True)
    args = parser.parse_args()
    audit = args.report_dir.resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be fresh and outside the checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = {"mode": "owner2_CSharp_TCP_HTTP_owned_MySQL_actual_shared_provider_files",
              "shared_environment_verified": False, "deployment_ack_installed": False,
              "reply_loss": "explicit client transport faults after actual successful server replies",
              "started_utc": datetime.now(timezone.utc).isoformat()}
    code = 0
    try:
        run(args, report)
    except KeyboardInterrupt:
        code, report["failure"] = 130, "interrupted"
    except Exception as error:
        code = 1
        report["failure"] = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + report["failure"] + "; credentials omitted.", file=sys.stderr)
    finally:
        report["exit_code"] = code
        report["finished_utc"] = datetime.now(timezone.utc).isoformat()
        (audit / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("SAFE_REPORT: " + str(audit / "verification.json"), flush=True)
    return code


if __name__ == "__main__":
    sys.exit(main())
