"""Read-only start/rework preparation UI over TCP on a newly owned MySQL.

Requires explicit opt-in, an absolute MySQL binary directory and a built client.
Existing services, configured databases, dotenv and SQLite are never used.
Synthetic credentials reach the client through private stdin only.
"""
from pathlib import Path
import argparse
import json
import os
import re
import secrets
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = Path(__file__).resolve()
sys.path.insert(0, str(ROOT / "Tests" / "TaskQueries.Http.Tests"))
import run_mysql_http_check as owned


def run_client(dotnet, dll, env, runtime, settings):
    """Keep credentials private and confine the prebuilt client to an owned job."""
    process = job = None
    try:
        job = owned.WindowsJob()
        with owned.defer_spawn_interrupt():
            process = subprocess.Popen([dotnet, str(dll), "--http"], cwd=runtime, env=env,
                                       stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                       text=True, encoding="utf-8",
                                       creationflags=subprocess.CREATE_NO_WINDOW | owned.CREATE_SUSPENDED)
            job.assign(process)
        stdout, _stderr = process.communicate(json.dumps(settings), timeout=120)
        if process.returncode != 0:
            raise owned.shared.RunFailure("C# start preparation HTTP checks failed; child output is suppressed")
        summary = re.fullmatch(r"PASS TaskStartPreparation HTTP: ([1-9][0-9]*) checks\.", stdout.strip())
        if summary is None:
            raise owned.shared.RunFailure("C# start preparation client did not return the expected safe summary")
        print("PASS TaskStartPreparation HTTP: " + summary.group(1) + " checks.", flush=True)
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
    dll = args.client_dll.resolve()
    dotnet = shutil.which("dotnet")
    if not dll.is_file() or dotnet is None:
        raise owned.shared.RunFailure("build TaskStartPreparation.Tests Release and install .NET first")
    # Resolve dependencies before any native process/database startup.
    import pymysql  # noqa: F401
    import sqlalchemy  # noqa: F401
    import flask  # noqa: F401
    accounts = {name: {"login_id": "fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("admin", "admin"), ("staff", "employee"), ("other_staff", "employee"))}
    print("Explicit read-only start fixture: new owned MySQL datadir/process/database, synthetic rows, "
          "raw002/raw004 and loopback HTTP. Existing service/DB/.env are not used.", flush=True)
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            env.update(REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT="1", REMOTEWORK_ENABLE_TASK_CREATE_DRAFT="0")
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                if args.exercise_cleanup_failure:
                    raise owned.shared.RunFailure("intentional failure after owned start preparation DB startup")
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                with owned.shared.FixtureServer(env, runtime, accounts,
                                               server_script=SCRIPT.with_name("fixture_server.py")) as fixture:
                    ids = fixture.command("identity_ids")["users"]
                    settings = {"url": fixture.url, "admin_login_id": accounts["admin"]["login_id"],
                                "admin_password": accounts["admin"]["password"],
                                "staff_login_id": accounts["staff"]["login_id"],
                                "staff_password": accounts["staff"]["password"],
                                "admin_user_id": ids["admin"], "staff_user_id": ids["staff"]}
                    run_client(dotnet, dll, env, runtime, settings)
                    verification = fixture.command("verify_readonly")
                    expected = {"checks": 13, "tasks": 5, "assignments": 5, "managers": 5,
                                "task_audits": 0, "receipts": 0, "schema_versions": [1],
                                "all_task_rows_unchanged": True}
                    if any(verification.get(key) != value for key, value in expected.items()):
                        raise owned.shared.RunFailure("start preparation fixture verification summary did not match")
                    print("PASS MySQL read-only preservation: 13 checks; tasks5/assignments5/managers5 "
                          "unchanged, task audits0/receipts0, synthetic states retained, schema version1.", flush=True)
            if not mysql.clean:
                raise owned.shared.RunFailure("owned MySQL cleanup was not confirmed")
        if runtime.exists():
            raise owned.shared.RunFailure("owned runtime cleanup was not confirmed")
    finally:
        if mysql is not None and mysql.clean and runtime is not None and not runtime.exists():
            print("CLEANUP: owned HTTP/MySQL stopped, owned database and temporary runtime removed.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--client-dll", type=Path,
                        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskStartPreparation.Tests.dll")
    parser.add_argument("--exercise-cleanup-failure", action="store_true",
                        help="Intentionally exit 1 after creating the owned DB to check failure cleanup")
    try:
        run(parser.parse_args())
        return 0
    except KeyboardInterrupt:
        print("FAIL: interrupted; owned-resource cleanup was attempted.", file=sys.stderr)
        return 130
    except Exception as error:
        # Only static labels or types: never print DB URLs, passwords, rows or child output.
        message = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + message + "; credentials and child output omitted.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
