"""Own a disposable Windows MySQL; never load the operator .env or use SQLite."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tests/TaskQueries.Http.Tests"))
import run_mysql_http_check as owned


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--allow-disposable-mysql", action="store_true", required=True)
    parser.add_argument("--mysql-bin-dir", type=Path, required=True)
    args = parser.parse_args()
    binary = owned.validate_binary_directory(args.mysql_bin_dir)
    with owned.owned_runtime() as runtime:
        env = owned.shared.isolated_environment(runtime)
        env.update(PYTHON_DOTENV_DISABLED="1", PYTEST_DISABLE_PLUGIN_AUTOLOAD="1", PYTHONIOENCODING="utf-8",
            PYTHONPATH=os.pathsep.join(str(ROOT / p) for p in ("RemoteWork.Server", "RemoteWork.Server/tests")))
        with owned.OwnedMySQL(binary, runtime, env) as mysql:
            env["MYSQL_TEST_URL"] = mysql.url.render_as_string(hide_password=False)
            result = owned.run_owned_client([sys.executable, "-m", "pytest", "-q",
                str(ROOT / "RemoteWork.Server/tests/test_identity_host.py"), "--basetemp", str(runtime / "pytest"),
                "--tb=short", "-p", "no:cacheprovider"], cwd=runtime, env=env,
                stdin=subprocess.DEVNULL, capture_output=True, text=True, encoding="utf-8", timeout=150)
            print(result.stdout)
            if result.returncode:
                print(result.stderr, file=sys.stderr)
        assert mysql.clean
    assert not runtime.exists()
    print(json.dumps(dict(mysql_database_runtime_cleaned=True, physical_device=False, sqlite=False)))
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
