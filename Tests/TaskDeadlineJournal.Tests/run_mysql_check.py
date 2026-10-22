"""Owner2 actual mutation -> member1 journal/HTTP on disposable owned MySQL.

Creates raw fixture tables once, outside product startup. Never reads .env,
configured/shared databases, SQLite or operational integration acknowledgments.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]
TARGET = "test_task_deadline_journal_integration.py"
SUITES = {"actual": TARGET, "legacy-writers": "test_deadline_event_writers.py",
          "legacy-http": "test_deadline_http.py"}
RAW = ("013_shared_attachments.mysql.sql", "drafts/deadline_events.mysql.sql",
       "drafts/deadline_confirmations.mysql.sql")
spec = importlib.util.spec_from_file_location("deadline_journal_owned_helpers",
    ROOT / "Tests/TaskLifecycle.Integration.Tests/run_mysql_check.py")
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
owned = helpers.owned


def run_pytest(env, runtime, report, audit, selector=None, suite="actual"):
    child_env = env.copy()
    child_env.update(MYSQL_TEST_URL=env["DATABASE_URL"], REMOTEWORK_RUN_TASK_DEADLINE_JOURNAL="1",
        PYTHONPATH=str(ROOT / "RemoteWork.Server"), PYTHON_DOTENV_DISABLED="1", PYTHONDONTWRITEBYTECODE="1")
    process = job = None
    try:
        job = owned.WindowsJob()
        command = [sys.executable, "-X", "utf8", "-B", "-m", "pytest", "-q", "--tb=short",
            "-p", "no:cacheprovider", "--basetemp", str(runtime / "pytest-temp"),
            str(ROOT / "RemoteWork.Server/tests" / SUITES[suite])]
        if selector:
            command += ["-k", selector]
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen(command, cwd=runtime, env=child_env,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, stdin=subprocess.DEVNULL,
                text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        try:
            stdout, stderr = process.communicate(timeout=900)
        except subprocess.TimeoutExpired:
            job.close(); owned.stop_owned_process(process)
            stdout, stderr = process.communicate(timeout=20)
            report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode, timeout=True)
            raise owned.shared.RunFailure("actual journal integration pytest exceeded 900 seconds") from None
        report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode)
        if process.returncode:
            # Synthetic fixture-only details are local, never published or echoed.
            (audit / "private-failure.txt").write_text(stdout + stderr, encoding="utf-8")
            raise owned.shared.RunFailure("actual journal integration failed: " + ", ".join(report["pytest"]["failed_nodes"]))
        if report["pytest"]["safe_summary"] is None:
            raise owned.shared.RunFailure("no recognized safe pytest summary")
        print("PASS " + suite + " deadline checks: " + report["pytest"]["safe_summary"], flush=True)
    finally:
        if job is not None:
            job.close()
        owned.stop_owned_process(process)
        if process is not None:
            for stream in (process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def run(args, report, audit):
    if not args.allow_disposable_mysql or os.name != "nt" or args.mysql_bin_dir is None:
        raise owned.shared.RunFailure("Windows, --allow-disposable-mysql and --mysql-bin-dir are required")
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                from sqlalchemy import create_engine
                initial_shape = None
                if args.suite == "actual":
                    helpers.prepare_schema(env, runtime)  # Original common+002/004/006/009 exactly once.
                    fixture_engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                    try:
                        with fixture_engine.begin() as db:
                            for name in RAW:
                                for statement in helpers.raw_ddl(name):
                                    db.exec_driver_sql(statement)
                        initial_shape = helpers.schema_shape(fixture_engine)
                        report["initial_ddl_sha256"] = helpers.management.ddl_digest(fixture_engine)
                    finally:
                        fixture_engine.dispose()
                else:
                    # Each legacy module owns its module fixture's original DDL.
                    # It must receive an EMPTY, separate instance to avoid CREATE duplication.
                    blank_engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                    try:
                        if helpers.schema_shape(blank_engine):
                            raise owned.shared.RunFailure("legacy fixture requires an empty owned database")
                    finally:
                        blank_engine.dispose()
                run_pytest(env, runtime, report, audit, args.select, args.suite)
                engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                try:
                    with engine.connect() as db:
                        owned.validate_instance(db.exec_driver_sql(owned.IDENTITY_SQL).one(), runtime / "mysql-data", mysql.port)
                        versions = [row[0] for row in db.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version")]
                    final_shape = helpers.schema_shape(engine)
                    changed = [] if initial_shape is None else [name for name in sorted(set(initial_shape) | set(final_shape))
                        if initial_shape.get(name) != final_shape.get(name)]
                    report["schema"] = dict(ddl_unchanged=None if initial_shape is None else not changed,
                        schema_versions=versions, changed_tables=changed,
                        fixture_owns_ddl=initial_shape is None)
                    if versions != [1] or changed:
                        raise owned.shared.RunFailure("journal integration changed physical DDL or global schema history")
                finally:
                    engine.dispose()
            if not mysql.clean:
                raise owned.shared.RunFailure("owned MySQL cleanup not confirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned runtime cleanup not confirmed")
    finally:
        report["cleanup"] = dict(mysql_stopped_and_database_removed=bool(mysql is not None and mysql.clean),
            runtime_removed=bool(runtime is not None and not runtime.exists()))
        if all(report["cleanup"].values()):
            print("CLEANUP: owned MySQL/process/database/runtime removed.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--report-dir", type=Path, required=True)
    parser.add_argument("--select", help="Optional pytest -k selector for focused diagnosis; not a full suite result")
    parser.add_argument("--suite", choices=tuple(SUITES), default="actual",
        help="Run one suite per fresh owned DB; legacy fixtures create their own DDL")
    args = parser.parse_args()
    audit = args.report_dir.resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be fresh and outside the checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = dict(mode="deadline_owned_MySQL_separate_suite",
        suite=args.suite, test_targets=[SUITES[args.suite]],
        raw_ddl=["002", "004", "006", "009", *RAW] if args.suite == "actual" else "owned_by_existing_test_fixture",
        selection=args.select, uses_actual_product_journal_hook=args.suite == "actual",
        audit_boundary_record_injection=args.suite == "legacy-writers",
        testing_app_ack=args.suite != "legacy-http", deployment_ack_installed=False, shared_environment_verified=False,
        baseline_policy_finalized=False, started_utc=datetime.now(timezone.utc).isoformat())
    code = 0
    try:
        run(args, report, audit)
    except KeyboardInterrupt:
        code, report["failure"] = 130, "interrupted"
    except Exception as error:
        code = 1
        report["failure"] = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + report["failure"] + "; arbitrary child output and credentials omitted.", file=sys.stderr)
    finally:
        report.update(exit_code=code, finished_utc=datetime.now(timezone.utc).isoformat())
        (audit / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("SAFE_REPORT: " + str(audit / "verification.json"), flush=True)
    return code


if __name__ == "__main__":
    sys.exit(main())
