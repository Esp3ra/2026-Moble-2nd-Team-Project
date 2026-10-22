"""Task web read/write regressions in a newly owned disposable MySQL.

Uses existing member2 identity/Windows Job/cleanup helpers, reviewed raw DDL,
synthetic web users and Flask requests. No user server, .env or SQLite is used.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]
TEST_TARGETS = (
    "test_task_web.py", "test_task_web_lifecycle.py", "test_task_web_context.py",
    "test_task_cancellation_current.py",
    "test_task_web_writes.py", "test_task_creation.py", "test_task_start_api.py",
    "test_task_management.py", "test_task_web_management.py", "test_task_web_results_navigation.py",
    "test_task_web_member3_integration.py",
    "test_task_lifecycle.py::test_request_lists_do_not_grant_old_assignment_or_other_requester_history",
    "test_task_lifecycle.py::test_cancel_closes_pending_and_preserves_task_assignment_and_readable_history",
    "test_task_lifecycle.py::test_missing_or_wrong_ack_denies_writes_and_exposes_readonly_context",
)
REMAINDER_TARGETS = ("test_task_web_lifecycle_writes.py", "test_task_continuity.py",
                     "test_task_shared_attachment_cancel.py")
REMAINDER_REGRESSION_TARGETS = (*REMAINDER_TARGETS, "test_task_lifecycle.py",
    "test_lifecycle_result_reads.py", "test_task_web.py", "test_task_web_writes.py",
    "test_task_web_management.py", "test_task_web_results_navigation.py",
    "test_task_web_member3_integration.py")
ATTACHMENT_TARGETS = ("test_task_creation_attachments.py", "test_task_creation.py",
    "test_task_management.py", "test_task_web_writes.py", "test_task_web_management.py")
T3_CONSUMER_TARGETS = ("test_task_web_attachments.py", "test_task_management_attachments.py",
    "test_task_creation_attachments.py", "test_task_web_results_navigation.py",
    "test_task_web_member3_integration.py", "test_task_web_writes.py",
    "test_task_web_management.py")
helper_spec = importlib.util.spec_from_file_location("member2_web_owned_helpers",
    ROOT / "Tests/TaskLifecycle.Integration.Tests/run_mysql_check.py")
helpers = importlib.util.module_from_spec(helper_spec)
helper_spec.loader.exec_module(helpers)
owned = helpers.owned


def run_pytest(env, runtime, report, targets):
    child_env = env.copy()
    child_env.update(MYSQL_TEST_URL=env["DATABASE_URL"], REMOTEWORK_RUN_TASK_WEB_MYSQL_TESTS="1",
                     PYTHONPATH=str(ROOT / "RemoteWork.Server"))
    process = job = None
    try:
        job = owned.WindowsJob()
        command = [sys.executable, "-m", "pytest", "-q", "--tb=no",
                   *(str(ROOT / "RemoteWork.Server/tests" / name) for name in targets)]
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen(command, cwd=runtime, env=child_env,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, stdin=subprocess.DEVNULL,
                text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        try:
            stdout, _stderr = process.communicate(timeout=1200)
        except subprocess.TimeoutExpired:
            job.close()
            owned.stop_owned_process(process)
            stdout, _stderr = process.communicate(timeout=20)
            report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode, timeout=True)
            raise owned.shared.RunFailure("owned task web pytest exceeded 1200 seconds") from None
        report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode)
        if process.returncode != 0:
            raise owned.shared.RunFailure("owned task web pytest failed: "
                + ", ".join(report["pytest"]["failed_nodes"]))
        if report["pytest"]["safe_summary"] is None:
            raise owned.shared.RunFailure("task web pytest returned no recognized safe summary")
        print("PASS owned task web MySQL/Flask: " + report["pytest"]["safe_summary"], flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def run(args, report):
    if not args.allow_disposable_mysql or os.name != "nt" or args.mysql_bin_dir is None:
        raise owned.shared.RunFailure("Windows, explicit --allow-disposable-mysql and --mysql-bin-dir are required")
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    management_web = ("test_task_web_management.py", "test_task_web_results_navigation.py",
                      "test_task_web_member3_integration.py")
    web_targets = ("test_task_web.py", "test_task_web_writes.py", *management_web)
    targets = (T3_CONSUMER_TARGETS if args.suite == "t3-consumers" else
               ATTACHMENT_TARGETS if args.suite == "attachments" else
               REMAINDER_REGRESSION_TARGETS if args.suite == "remainder-regression" else
               REMAINDER_TARGETS if args.suite == "remainder" else
               management_web if args.suite == "management-web" else web_targets if args.suite == "web" else
               (*web_targets, "test_task_management.py") if args.suite == "management" else TEST_TARGETS)
    report["suite"], report["test_targets"] = args.suite, list(targets)
    if not all((ROOT / "RemoteWork.Server/tests" / name.split("::", 1)[0]).is_file()
               for name in targets):
        raise owned.shared.RunFailure("member2 task web and existing lifecycle regression sources are required")
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                initial_ddl, initial_shape = helpers.prepare_schema(env, runtime)
                if args.suite in ("remainder", "remainder-regression", "attachments", "t3-consumers"):
                    from sqlalchemy import create_engine
                    fixture_engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                    try:
                        with fixture_engine.begin() as connection:
                            for statement in helpers.raw_ddl("013_shared_attachments.mysql.sql"):
                                connection.exec_driver_sql(statement)
                        initial_shape = helpers.schema_shape(fixture_engine)
                        initial_ddl = helpers.management.ddl_digest(fixture_engine)
                        report["additional_raw_fixture"] = "013_shared_attachments.mysql.sql"
                    finally:
                        fixture_engine.dispose()
                report["initial_ddl_sha256"] = initial_ddl
                run_pytest(env, runtime, report, targets)
                from sqlalchemy import create_engine
                engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                try:
                    with engine.connect() as connection:
                        owned.validate_instance(connection.exec_driver_sql(owned.IDENTITY_SQL).one(),
                                                runtime / "mysql-data", mysql.port)
                        versions = [row[0] for row in connection.exec_driver_sql(
                            "SELECT version FROM schema_versions ORDER BY version")]
                    final_shape = helpers.schema_shape(engine)
                    changed = [name for name in sorted(set(initial_shape) | set(final_shape))
                               if initial_shape.get(name) != final_shape.get(name)]
                    report["schema"] = {"ddl_unchanged": not changed, "schema_versions": versions,
                                        "changed_tables": changed}
                    if versions != [1] or changed:
                        raise owned.shared.RunFailure("task web tests changed physical DDL or global schema history")
                    print("PASS task web independent schema: physical DDL unchanged, schema1.", flush=True)
                finally:
                    engine.dispose()
            if not mysql.clean:
                raise owned.shared.RunFailure("owned task web MySQL cleanup was not confirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned task web runtime cleanup was not confirmed")
    finally:
        report["cleanup"] = {"mysql_stopped_and_database_removed": bool(mysql is not None and mysql.clean),
                             "runtime_removed": bool(runtime is not None and not runtime.exists())}
        if all(report["cleanup"].values()):
            print("CLEANUP: owned task web MySQL/process/database/runtime removed.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--report-dir", type=Path)
    parser.add_argument("--suite", choices=("full", "web", "management", "management-web", "remainder", "remainder-regression", "attachments", "t3-consumers"), default="full",
                        help="full: existing task web/API targets; web: HTML reads/writes; management: web plus management API; management-web: management/member3 checks; remainder: lifecycle web/continuity/shared cancellation on raw013; remainder-regression: remainder plus existing lifecycle/web checks; attachments: creation shared provider and existing creation/management/web regressions on raw013; t3-consumers: actual cookie attachments, management attachment writer and signed navigation plus prior task web regressions on raw013")
    args = parser.parse_args()
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "_" + secrets.token_hex(4)
    audit = (args.report_dir or ROOT.parent / ("remote_work_member2_task_web_mysql_" + stamp)).resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be fresh and outside the source checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = {"mode": "task_web_flask_mysql", "lifecycle_sql": helpers.LIFECYCLE_SQL,
              "web_actions": ["task.created", "task.accepted", "task.rework_started",
                              "task.updated", "task.reassigned", "task.managers_changed"],
              "integrated_ack_installed_in_owned_fixture": args.suite in ("remainder", "remainder-regression"),
              "deployment_ack_installed": False,
              "shared_environment_verified": False,
              "started_utc": datetime.now(timezone.utc).isoformat()}
    if args.suite in ("remainder", "remainder-regression"):
        report["web_actions"] = ["task.cancelled", "task.deadline_extension_requested",
            "task.deadline_extension_approved", "task.deadline_extension_rejected",
            "task.deadline_extension_withdrawn"]
        report["member3_consumer_band"] = "actual registered shared-file release with owner2 cancellation"
    elif args.suite == "attachments":
        report["web_actions"] = ["task.created", "task.updated", "task.reassigned", "task.managers_changed"]
        report["member3_consumer_band"] = "Flask creation consumes registered shared provider; actual chunk/files on owned MySQL/raw013; no deployment ACK"
    elif args.suite == "t3-consumers":
        report["web_actions"] = ["task.created", "task.updated", "task.reassigned", "task.managers_changed"]
        report["member3_consumer_band"] = "Actual cookie shared-file upload/download, task creation/management writer and signed read/back navigation on owned MySQL/raw013; no deployment ACK"
    exit_code = 0
    try:
        run(args, report)
    except KeyboardInterrupt:
        exit_code, report["failure"] = 130, "interrupted"
    except Exception as error:
        exit_code = 1
        report["failure"] = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + report["failure"] + "; credentials and arbitrary child output omitted.", file=sys.stderr)
    finally:
        report["exit_code"] = exit_code
        report["finished_utc"] = datetime.now(timezone.utc).isoformat()
        (audit / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("SAFE_REPORT: " + str(audit / "verification.json"), flush=True)
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
