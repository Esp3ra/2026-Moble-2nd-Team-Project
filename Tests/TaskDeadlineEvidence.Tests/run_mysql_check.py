"""Owner2 current facts versus actual owner2/3 state paths on owned MySQL.

No running server, .env, shared DB, SQLite, baseline or deadline event store.
The only integrated ACK is in the disposable TESTING Flask fixture.
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
TARGET = "test_task_deadline_state_evidence.py"
spec = importlib.util.spec_from_file_location("deadline_evidence_owned_helpers",
    ROOT / "Tests/TaskLifecycle.Integration.Tests/run_mysql_check.py")
helpers = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helpers)
owned = helpers.owned


def run_pytest(env, runtime, report):
    child_env = env.copy()
    child_env.update(MYSQL_TEST_URL=env["DATABASE_URL"], REMOTEWORK_RUN_DEADLINE_EVIDENCE="1",
        PYTHONPATH=str(ROOT / "RemoteWork.Server"), PYTHON_DOTENV_DISABLED="1", PYTHONDONTWRITEBYTECODE="1")
    process = job = None
    try:
        job = owned.WindowsJob()
        command = [sys.executable, "-X", "utf8", "-B", "-m", "pytest", "-q", "--tb=no",
                   "-p", "no:cacheprovider", str(ROOT / "RemoteWork.Server/tests" / TARGET)]
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen(command, cwd=runtime, env=child_env,
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, stdin=subprocess.DEVNULL,
                text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        try:
            stdout, _stderr = process.communicate(timeout=600)
        except subprocess.TimeoutExpired:
            job.close(); owned.stop_owned_process(process)
            stdout, _stderr = process.communicate(timeout=20)
            report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode, timeout=True)
            raise owned.shared.RunFailure("deadline evidence pytest exceeded 600 seconds") from None
        report["pytest"] = helpers.safe_pytest_report(stdout, process.returncode)
        if process.returncode:
            raise owned.shared.RunFailure("deadline state evidence failed: " + ", ".join(report["pytest"]["failed_nodes"]))
        if report["pytest"]["safe_summary"] is None:
            raise owned.shared.RunFailure("no recognized safe pytest summary")
        print("PASS owned deadline state evidence: " + report["pytest"]["safe_summary"], flush=True)
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
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                initial_ddl, initial_shape = helpers.prepare_schema(env, runtime)
                report["initial_ddl_sha256"] = initial_ddl
                run_pytest(env, runtime, report)
                from sqlalchemy import create_engine
                engine = create_engine(env["DATABASE_URL"], hide_parameters=True)
                try:
                    with engine.connect() as db:
                        owned.validate_instance(db.exec_driver_sql(owned.IDENTITY_SQL).one(), runtime / "mysql-data", mysql.port)
                        versions = [row[0] for row in db.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version")]
                    final_shape = helpers.schema_shape(engine)
                    changed = [name for name in sorted(set(initial_shape) | set(final_shape)) if initial_shape.get(name) != final_shape.get(name)]
                    report["schema"] = {"ddl_unchanged": not changed, "schema_versions": versions, "changed_tables": changed}
                    if versions != [1] or changed:
                        raise owned.shared.RunFailure("deadline evidence changed physical DDL or global schema history")
                finally:
                    engine.dispose()
            if not mysql.clean:
                raise owned.shared.RunFailure("owned MySQL cleanup not confirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned runtime cleanup not confirmed")
    finally:
        report["cleanup"] = {"mysql_stopped_and_database_removed": bool(mysql is not None and mysql.clean),
                             "runtime_removed": bool(runtime is not None and not runtime.exists())}
        if all(report["cleanup"].values()):
            print("CLEANUP: owned MySQL/process/database/runtime removed.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--report-dir", type=Path, required=True)
    args = parser.parse_args()
    audit = args.report_dir.resolve()
    if audit.exists() or audit == ROOT or ROOT in audit.parents:
        print("FAIL: report-dir must be fresh and outside the checkout.", file=sys.stderr)
        return 1
    audit.mkdir(parents=True)
    report = dict(mode="actual_owner2_owner3_handlers_owned_MySQL_current_fact_evidence",
        test_targets=[TARGET], raw_ddl=["002", "004", "006", "009"],
        testing_app_ack=True, deployment_ack_installed=False, shared_environment_verified=False,
        deadline_event_store_verified=False, baseline_policy_finalized=False,
        started_utc=datetime.now(timezone.utc).isoformat())
    exit_code = 0
    try:
        run(args, report)
    except KeyboardInterrupt:
        exit_code, report["failure"] = 130, "interrupted"
    except Exception as error:
        exit_code = 1
        report["failure"] = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + report["failure"] + "; arbitrary child output and credentials omitted.", file=sys.stderr)
    finally:
        report["exit_code"] = exit_code
        report["finished_utc"] = datetime.now(timezone.utc).isoformat()
        (audit / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("SAFE_REPORT: " + str(audit / "verification.json"), flush=True)
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
