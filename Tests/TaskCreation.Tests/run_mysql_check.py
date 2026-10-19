"""Creation WinForms/C# -> TCP HTTP -> raw002/raw004 on an owned MySQL process.

Requires explicit opt-in, absolute MySQL binary directory and a prebuilt client.
Never uses an existing service, configured DB, .env or SQLite. Credentials go
only through private stdin/the isolated fixture environment; no token is logged.
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
    """Same owned Job confinement, with private stdin supported explicitly."""
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
            raise owned.shared.RunFailure("C# creation HTTP checks failed; child output is suppressed")
        summary = re.fullmatch(r"PASS TaskCreation HTTP: ([1-9][0-9]*) checks\.", stdout.strip())
        if summary is None:
            raise owned.shared.RunFailure("C# creation client did not return the expected safe summary")
        print("PASS TaskCreation HTTP: " + summary.group(1) + " checks.", flush=True)
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
        raise owned.shared.RunFailure("build TaskCreation.Tests Release and install .NET first")
    # Resolve dependencies before any native process/database startup.
    import pymysql  # noqa: F401
    import sqlalchemy  # noqa: F401
    import flask  # noqa: F401
    accounts = {name: {"login_id": "fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("admin", "admin"), ("staff", "employee"),
                                   ("other_admin", "admin"), ("other_staff", "employee"))}
    print("Explicit creation fixture: new owned MySQL datadir/process/database, synthetic accounts, "
          "raw002/raw004 and loopback HTTP. Existing service/DB/.env are not used.", flush=True)
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            env.update(REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT="1", REMOTEWORK_ENABLE_TASK_CREATE_DRAFT="1")
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                if args.exercise_cleanup_failure:
                    raise owned.shared.RunFailure("intentional failure after owned creation DB startup")
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                with owned.shared.FixtureServer(env, runtime, accounts,
                                               server_script=SCRIPT.with_name("fixture_server.py")) as fixture:
                    ids = fixture.command("identity_ids")["users"]
                    settings = {"url": fixture.url, "admin_login_id": accounts["admin"]["login_id"],
                                "admin_password": accounts["admin"]["password"],
                                "staff_login_id": accounts["staff"]["login_id"],
                                "staff_password": accounts["staff"]["password"],
                                "admin_user_id": ids["admin"], "assignee_id": ids["staff"],
                                "additional_manager_id": ids["other_admin"]}
                    run_client(dotnet, dll, env, runtime, settings)
                    verification = fixture.command("verify_created")
                    if ((verification.get("tasks"), verification.get("assignments"),
                         verification.get("managers"), verification.get("creation_audits"),
                         verification.get("receipts")) != (3, 3, 6, 3, 3)
                            or verification.get("schema_versions") != [1]
                            or verification.get("checks") != 24):
                        raise owned.shared.RunFailure("creation fixture verification summary did not match")
                    print("PASS MySQL committed rows: 24 checks; tasks3/assignments3/managers6/"
                          "creation audits3/receipts3, creator retained, initial1/1, schema version1.", flush=True)
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
                        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskCreation.Tests.dll")
    parser.add_argument("--exercise-cleanup-failure", action="store_true",
                        help="Intentionally exit 1 after creating the owned DB to check failure cleanup")
    try:
        run(parser.parse_args())
        return 0
    except KeyboardInterrupt:
        print("FAIL: interrupted; owned-resource cleanup was attempted.", file=sys.stderr)
        return 130
    except Exception as error:
        # Type and static labels only: never expose DB URLs, passwords, rows or child output.
        message = str(error) if isinstance(error, owned.shared.RunFailure) else type(error).__name__
        print("FAIL: " + message + "; credentials and child output omitted.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
