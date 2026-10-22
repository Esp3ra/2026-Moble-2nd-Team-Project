"""Explicit test-only MySQL instance. Never connect using operator configuration."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tests/TaskQueries.Http.Tests"))
import run_mysql_http_check as owned


def capture_pytest(command, runtime, env):
    # This existing helper owns the child from BEFORE Popen/Job assignment.
    # Its finally closes the Job, stops/waits the owned child and closes pipes,
    # including when assignment or primary-thread resume raises.
    return owned.run_owned_client(command, cwd=runtime, env=env,
        stdin=subprocess.DEVNULL, text=True, encoding="utf-8",
        creationflags=subprocess.CREATE_NO_WINDOW, capture_output=True, timeout=300)


def captured_text(value):
    return value.decode("utf-8", errors="replace") if isinstance(value, bytes) else value or ""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--allow-disposable-mysql", action="store_true", required=True)
    parser.add_argument("--mysql-bin-dir", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--report-directory", type=Path, required=True)
    parser.add_argument("--identity-login-client", type=Path)
    parser.add_argument("--workday-activity-client", type=Path)
    parser.add_argument("--workday-clockout-client", type=Path)
    args = parser.parse_args()
    if os.name != "nt": raise RuntimeError("Windows owned-process runner required")
    source = args.source_root.resolve(strict=True)
    if source == ROOT or ROOT in source.parents or (source / "RemoteWork.Server/.env").exists():
        raise RuntimeError("A separate reviewed export without .env is required")
    manifest = json.loads((source / "source-manifest.json").read_text(encoding="utf-8"))
    for item in manifest.get("resolved", []):
        if item["path"] not in {"RemoteWork.Server/.env.example", "RemoteWork.Server/remotework/__init__.py"}:
            raise RuntimeError("Unexpected reviewed conflict path")
        if hashlib.sha256((source / item["path"]).read_bytes()).hexdigest() != item["sha256"]:
            raise RuntimeError("Reviewed source changed after preparation")
    assert (source / "RemoteWork.Server/remotework/features/identity/attendance.py").is_file()
    args.report_directory.mkdir(parents=True, exist_ok=True)
    report = dict(source=manifest, production_changes=False, physical_device=False, sqlite=False)
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    mysql = runtime = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                url = mysql.url.render_as_string(hide_password=False)
                env.update(DATABASE_URL=url, MYSQL_TEST_URL=url, MEMBER5_OWNED_IDENTITY_CHECK="1",
                    REMOTEWORK_ENABLE_TASK_QUERY_DRAFT="0", PYTEST_DISABLE_PLUGIN_AUTOLOAD="1",
                    PYTHONPATH=os.pathsep.join(str(source / p) for p in
                        ("RemoteWork.Server", "RemoteWork.Server/tests", "Devices/RaspberryPi")))
                command = [sys.executable, "-m", "pytest", "-q", "--tb=short", "--no-header", "-p", "no:cacheprovider",
                           str(Path(__file__).with_name("test_server_pi.py"))]
                if args.identity_login_client is not None:
                    client = args.identity_login_client.resolve(strict=True)
                    if client.name != "IdentityLogin.Ui.Tests.dll": raise RuntimeError("Explicit reviewed login client required")
                    env["MEMBER5_IDENTITY_LOGIN_CLIENT"] = str(client)
                    env["PYTHONPATH"] += os.pathsep + str(Path(__file__).parent)
                    report["login_client_sha256"] = hashlib.sha256(client.read_bytes()).hexdigest()
                    command.append(str(ROOT / "Tests/IdentityLogin.Ui.Tests/test_real_server.py"))
                if args.workday_activity_client is not None:
                    activity_client = args.workday_activity_client.resolve(strict=True)
                    if activity_client.name != "WorkdayActivity.Tests.dll": raise RuntimeError("Explicit reviewed activity client required")
                    env["MEMBER5_WORKDAY_ACTIVITY_CLIENT"] = str(activity_client)
                    env["PYTHONPATH"] += os.pathsep + str(Path(__file__).parent)
                    report["activity_client_sha256"] = hashlib.sha256(activity_client.read_bytes()).hexdigest()
                    command.append(str(ROOT / "Tests/WorkdayActivity.Tests/test_real_server.py"))
                if args.workday_clockout_client is not None:
                    clockout_client = args.workday_clockout_client.resolve(strict=True)
                    if clockout_client.name != "WorkdayActivity.Tests.dll": raise RuntimeError("Explicit reviewed clock-out feature client required")
                    env["MEMBER5_WORKDAY_CLOCKOUT_CLIENT"] = str(clockout_client)
                    env["PYTHONPATH"] += os.pathsep + str(Path(__file__).parent)
                    report["clockout_client_sha256"] = hashlib.sha256(clockout_client.read_bytes()).hexdigest()
                    command.append(str(ROOT / "Tests/WorkdayActivity.Tests/test_clockout_real_server.py"))
                try:
                    result = capture_pytest(command, runtime, env)
                    stdout, stderr, code = result.stdout, result.stderr, result.returncode
                except subprocess.TimeoutExpired as error:
                    stdout, stderr, code = captured_text(error.stdout), captured_text(error.stderr), 1
                    report["timed_out"] = True
                if args.identity_login_client is not None and hashlib.sha256(client.read_bytes()).hexdigest() != report["login_client_sha256"]:
                    code = 1
                    report["login_client_changed_during_check"] = True
                if args.workday_activity_client is not None and hashlib.sha256(activity_client.read_bytes()).hexdigest() != report["activity_client_sha256"]:
                    code = 1
                    report["activity_client_changed_during_check"] = True
                if args.workday_clockout_client is not None and hashlib.sha256(clockout_client.read_bytes()).hexdigest() != report["clockout_client_sha256"]:
                    code = 1
                    report["clockout_client_changed_during_check"] = True
                (args.report_directory / "pytest.private.txt").write_text(stdout + "\n" + stderr, encoding="utf-8")
                summaries = re.findall(r"^.*\d+ (?:passed|failed|error).* in [0-9.]+s.*$", stdout, re.M)
                report.update(exit=code, summary=summaries[-1] if summaries else "No completed pytest summary",
                              failed_nodes=re.findall(r"^FAILED (\S+)", stdout, re.M))
                print(json.dumps({k: report[k] for k in ("exit", "summary", "failed_nodes")}), flush=True)
    finally:
        report["cleanup"] = dict(mysql_stopped_and_database_removed=bool(mysql and mysql.clean),
                                  runtime_removed=bool(runtime and not runtime.exists()))
        (args.report_directory / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("CLEANUP: " + json.dumps(report["cleanup"]), flush=True)
    return 0 if report.get("exit") == 0 and not report.get("timed_out") and all(report["cleanup"].values()) else 1


if __name__ == "__main__":
    try:
        code = main()
    except Exception as error:
        print("STOPPED: identity_test_harness_" + type(error).__name__, file=sys.stderr)
        code = 1
    raise SystemExit(code)
