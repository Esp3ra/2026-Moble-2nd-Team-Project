"""Explicit disposable MySQL + reviewed combined Git export, never operator config."""
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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--allow-disposable-mysql", action="store_true", required=True)
    parser.add_argument("--mysql-bin-dir", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--report-directory", type=Path, required=True)
    args = parser.parse_args()
    source = args.source_root.resolve(strict=True)
    if os.name != "nt" or source == ROOT or ROOT in source.parents or (source / "RemoteWork.Server/.env").exists():
        raise RuntimeError("External reviewed export without operator .env is required")
    manifest = json.loads((source / "source-manifest.json").read_text(encoding="utf-8"))
    if not manifest.get("test_only") or manifest.get("resolved"):
        raise RuntimeError("Conflict-free reviewed source required")
    client = args.client.resolve(strict=True)
    if client.name != "Leave.Ui.Tests.dll": raise RuntimeError("Explicit leave UI client required")
    digest = hashlib.sha256(client.read_bytes()).hexdigest()
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    args.report_directory.mkdir(parents=True, exist_ok=True)
    report = dict(source=manifest, client_sha256=digest, production_changes=False, physical_device=False, sqlite=False)
    runtime = mysql = None
    try:
        with owned.owned_runtime() as runtime:
            env = owned.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            with owned.OwnedMySQL(executable, runtime, env) as mysql:
                url = mysql.url.render_as_string(hide_password=False)
                env.update(DATABASE_URL=url, MYSQL_TEST_URL=url, MEMBER5_OWNED_LEAVE_CHECK="1",
                    MEMBER5_LEAVE_UI_CLIENT=str(client), PYTEST_DISABLE_PLUGIN_AUTOLOAD="1",
                    PYTHONPATH=os.pathsep.join(str(source / p) for p in ("RemoteWork.Server", "RemoteWork.Server/tests")))
                command = [sys.executable, "-m", "pytest", "-q", "--tb=short", "--no-header", "-p", "no:cacheprovider", str(Path(__file__).with_name("test_real_server.py"))]
                try:
                    result = owned.run_owned_client(command, cwd=runtime, env=env, stdin=subprocess.DEVNULL,
                        text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW,
                        capture_output=True, timeout=300)
                    stdout, stderr, code = result.stdout, result.stderr, result.returncode
                except subprocess.TimeoutExpired as error:
                    def decode(value): return value.decode("utf-8", errors="replace") if isinstance(value, bytes) else value or ""
                    stdout, stderr, code = decode(error.stdout), decode(error.stderr), 1
                    report["timed_out"] = True
                if hashlib.sha256(client.read_bytes()).hexdigest() != digest:
                    code = 1; report["client_changed"] = True
                (args.report_directory / "pytest.private.txt").write_text(stdout + "\n" + stderr, encoding="utf-8")
                summaries = re.findall(r"^.*\d+ (?:passed|failed|error).* in [0-9.]+s.*$", stdout, re.M)
                report.update(exit=code, summary=summaries[-1] if summaries else "No completed pytest summary",
                    failed_nodes=re.findall(r"^FAILED (\S+)", stdout, re.M))
                print(json.dumps({k: report[k] for k in ("exit", "summary", "failed_nodes")}), flush=True)
    finally:
        report["cleanup"] = dict(mysql_stopped_and_database_removed=bool(mysql and mysql.clean), runtime_removed=bool(runtime and not runtime.exists()))
        (args.report_directory / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print("CLEANUP: " + json.dumps(report["cleanup"]), flush=True)
    return 0 if report.get("exit") == 0 and all(report["cleanup"].values()) else 1


if __name__ == "__main__":
    try: code = main()
    except Exception as error:
        print("STOPPED: leave_ui_test_harness_" + type(error).__name__, file=sys.stderr); code = 1
    raise SystemExit(code)
