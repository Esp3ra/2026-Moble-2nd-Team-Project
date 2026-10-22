"""Actual owner2 management selection -> C# HTTP -> owned MySQL/raw013.

Only synthetic accounts are seeded; tasks/files/edits are made by real APIs.
Reuses existing ownership, process job, reviewed raw DDL and cleanup helpers.
No .env, running server, shared DB, SQLite or operational ACK is used.
"""
from datetime import datetime, timedelta, timezone
import argparse
import importlib.util
import json
from pathlib import Path
import re
import secrets
import subprocess
import sys
import threading

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve()
spec = importlib.util.spec_from_file_location("management_attachment_owned_base",
    ROOT / "Tests/TaskCreationUploads.Tests/run_mysql_check.py")
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
owned, helpers = base.owned, base.helpers


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
            safe = re.fullmatch(r"FAIL TaskManagementAttachments HTTP after ([0-9]+) checks at ([a-z ]+); details omitted\.", stderr.strip())
            if safe:
                report["client_failure_after"] = int(safe.group(1))
                report["client_failure_stage"] = safe.group(2)
            raise owned.shared.RunFailure("C# management attachment checks failed; arbitrary child output suppressed")
        safe = re.fullmatch(r"PASS TaskManagementAttachments HTTP: ([1-9][0-9]*) checks\.", stdout.strip())
        if not safe:
            raise owned.shared.RunFailure("C# client returned no recognized safe summary")
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
    for feature in ("TASK_QUERY", "COMMON_WRITE", "TASK_CREATE", "TASK_MANAGEMENT", "RESULTS", "SHARED_ATTACHMENTS"):
        config["ENABLE_" + feature + "_DRAFT"] = True
    for feature in ("TASK_START", "TASK_LIFECYCLE", "NOTICE", "CHAT", "DEVICE_ENROLLMENT",
                    "ENROLLMENT_DURABILITY", "ENROLLMENT_RECOVERY"):
        config["ENABLE_" + feature + "_DRAFT"] = False
    app = remotework.create_app(config)
    app.logger.disabled = True
    accounts = {name: {"login_id": "management_attachment_" + name,
                      "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("admin", "admin"), ("staff", "employee"))}
    checks = owned.shared.Checks()
    engine = app.extensions["db_engine"]
    server = thread = None
    try:
        checks.check(app.extensions.get("results_shared_attachments") is shared_attachments,
                     "actual shared attachment provider registered")
        checks.check("task_lifecycle_integration_ack" not in app.extensions,
                     "no deployment or lifecycle fixture ACK installed")
        with app.app_context(), transaction() as db:
            db.add(SchemaVersion(version=1)); db.add(AdminGuard(id=1))
            users = {name: create_user(db, {**account, "display_name": "Attachment fixture " + name,
                "employee_no": "EDIT-FILE-" + name, "department": "Explicit fixture"}) for name, account in accounts.items()}
            ids = {name: user.id for name, user in users.items()}
        class QuietHandler(WSGIRequestHandler):
            def log(self, *_a, **_kw):
                pass
        server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=QuietHandler)
        thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.1), daemon=True)
        thread.start()
        settings = {"url": "http://127.0.0.1:" + str(server.server_port) + "/", "scratch_dir": str(runtime),
                    "admin_user_id": ids["admin"], "assignee_id": ids["staff"],
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
            checks.check(counts == dict(tasks=2, assignments=2, managers=2, receipts=5, files=1, chunks=1, links=1),
                         "exact committed rows exclude duplicate uncertain patch and no-change writes")
            tasks = {task.title: task for task in db.scalars(select(TaskRecord))}
            primary = tasks["management-attachment-title-only"]
            retained = tasks["management-attachment-retained"]
            checks.check((primary.row_version, primary.current_assignment_revision, primary.task_status) == (4, 1, "not_started"),
                         "three successful edits change only primary row version")
            checks.check((retained.row_version, retained.current_assignment_revision, retained.task_status) == (1, 1, "not_started"),
                         "other task unchanged")
            audits = list(db.scalars(select(AuditEvent).where(AuditEvent.action == "task.updated").order_by(AuditEvent.id)))
            changes = [json.loads(row.details_json) for row in audits]
            checks.check(len(changes) == 3, "exactly three update audits")
            checks.check([item["changed_fields"] for item in changes] == [["attachment_ids"], ["title"], ["attachment_ids"]],
                         "minimum audit distinguishes attachment-only and title-only changes")
            checks.check(changes[0]["attachment_ids"] == changes[2]["before_attachment_ids"]
                         and changes[2]["attachment_ids"] == [] and len(changes[0]["before_attachment_ids"]) == 1,
                         "before and final ID audit is precise")
            checks.check("attachment_ids" not in changes[1] and "before_attachment_ids" not in changes[1],
                         "omission edit adds no artificial attachment audit")
            links = list(db.scalars(select(shared_attachments.SharedLink)))
            files = list(db.scalars(select(shared_attachments.SharedFile)))
            checks.check(len(files) == 1 and files[0].state == "ready" and files[0].id == changes[0]["before_attachment_ids"][0],
                         "original shared file remains ready and new unreferenced file is cleaned")
            checks.check(links[0].task_id == retained.task_id and links[0].resource_type == "task"
                         and links[0].attachment_id == files[0].id,
                         "remaining reference belongs only to the unaffected task")
            receipts = list(db.scalars(select(WriteReceipt).where(WriteReceipt.action == "task.updated")))
            checks.check(len(receipts) == 3 and sorted(json.loads(row.result_json)["row_version"] for row in receipts) == ["2", "3", "4"],
                         "one retained update receipt per successful version")
            checks.check(list(db.scalars(select(SchemaVersion.version))) == [1], "global schema version1 unchanged")
            report["committed_counts"] = counts
            report["schema_versions"] = [1]
        checks.check(helpers.schema_shape(engine) == shape, "physical schema unchanged after real HTTP")
        report["database_checks"] = checks.total
        report["ddl_unchanged"] = True
        print("PASS owned management attachment database: " + str(checks.total) + " checks; schema1 and physical DDL unchanged.", flush=True)
    finally:
        if server is not None:
            server.shutdown()
            if thread is not None:
                thread.join(timeout=10)
            server.server_close()
            report["http_stopped"] = thread is None or not thread.is_alive()
        engine.dispose()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--client-dll", type=Path,
        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskManagementAttachments.Http.Tests.dll")
    parser.add_argument("--report-dir", type=Path, required=True)
    args = parser.parse_args()
    audit = args.report_dir.resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be fresh and outside the checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = {"mode": "owner2_management_selection_CSharp_TCP_HTTP_owned_MySQL_raw013",
              "shared_environment_verified": False, "deployment_ack_installed": False,
              "reply_loss": "one explicit client fault after real successful PATCH reply",
              "started_utc": datetime.now(timezone.utc).isoformat()}
    code = 0
    try:
        # Swap the test runner hook only; all source helpers and product modules
        # stay unchanged. Base owns binary validation/raw013/lifetime/cleanup.
        base.run_http = run_http
        base.run(args, report)
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
