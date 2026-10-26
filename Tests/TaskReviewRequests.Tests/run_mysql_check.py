"""Manual requests, real submissions/decisions and web/HTTP on owned MySQL.

No configured/shared DB, .env, SQLite or deployment acknowledgments are used.
Raw019 is explicitly applied by this isolated fixture, never product startup.
"""
import argparse
from datetime import datetime, timezone
import difflib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
TARGETS = ("test_task_review_request_contract.py", "test_task_review_requests.py", "test_task_review_request_schema.py",
           "test_task_web_review_requests.py")
spec = importlib.util.spec_from_file_location("review_request_owned_helpers",
    ROOT / "Tests/TaskLifecycle.Integration.Tests/run_mysql_check.py")
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
owned = helpers.owned


def run_pytest(env, runtime, report, audit, selector):
    child_env = env.copy()
    child_env.update(MYSQL_TEST_URL=env["DATABASE_URL"], REMOTEWORK_RUN_TASK_REVIEW_REQUESTS="1",
        PYTHONPATH=str(ROOT / "RemoteWork.Server"), PYTHON_DOTENV_DISABLED="1", PYTHONDONTWRITEBYTECODE="1")
    process = job = None
    try:
        job = owned.WindowsJob()
        command = [sys.executable, "-X", "utf8", "-B", "-m", "pytest", "-q", "--tb=short",
            "-p", "no:cacheprovider", "--basetemp", str(runtime / "pytest-temp"),
            *(str(ROOT / "RemoteWork.Server/tests" / target) for target in TARGETS)]
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
            raise owned.shared.RunFailure("review request pytest exceeded 900 seconds") from None
        report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode)
        if process.returncode:
            (audit / "private-failure.txt").write_text(stdout + stderr, encoding="utf-8")
            raise owned.shared.RunFailure("review request tests failed; inspect the local private-failure.txt report")
        if report["pytest"]["safe_summary"] is None:
            raise owned.shared.RunFailure("no recognized safe pytest summary")
        print("PASS review request MySQL/web/HTTP: " + report["pytest"]["safe_summary"], flush=True)
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
                helpers.prepare_schema(env, runtime)
                engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                try:
                    with engine.begin() as db:
                        for statement in helpers.raw_ddl("019_task_review_requests.mysql.sql"):
                            db.exec_driver_sql(statement)
                    initial_shape = helpers.schema_shape(engine)
                    report["initial_ddl_sha256"] = helpers.management.ddl_digest(engine)
                    run_pytest(env, runtime, report, audit, args.select)
                    with engine.connect() as db:
                        owned.validate_instance(db.exec_driver_sql(owned.IDENTITY_SQL).one(), runtime / "mysql-data", mysql.port)
                        versions = [row[0] for row in db.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version")]
                    final_shape = helpers.schema_shape(engine)
                    changed = [name for name in sorted(set(initial_shape) | set(final_shape))
                               if initial_shape.get(name) != final_shape.get(name)]
                    report["schema"] = dict(ddl_unchanged=not changed, schema_versions=versions, changed_tables=changed)
                    if versions != [1] or changed:
                        (audit / "ddl-difference.txt").write_text("\n".join(
                            "\n".join(difflib.unified_diff(initial_shape.get(name, "").splitlines(),
                                final_shape.get(name, "").splitlines(), fromfile=name + "-before", tofile=name + "-after"))
                            for name in changed), encoding="utf-8")
                        raise owned.shared.RunFailure("tests changed physical DDL or global schema history")
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
    parser.add_argument("--select", help="Optional pytest -k diagnosis; not a full suite result")
    args = parser.parse_args()
    audit = args.report_dir.resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be fresh and outside the checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = dict(mode="manual_review_owned_MySQL_web_HTTP", test_targets=list(TARGETS),
        raw_ddl=["002", "004", "006", "009", "019"], selection=args.select,
        actual_results_blueprint=True, actual_loopback_HTTP=True, shared_environment_verified=False,
        deployment_ack_installed=False, started_utc=datetime.now(timezone.utc).isoformat())
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
