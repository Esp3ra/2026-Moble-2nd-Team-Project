"""Run the existing C# --live suite against a disposable, explicit SQLite fixture.

Windows/.NET 8 and RemoteWork.Server/requirements.txt are required. This runner
does not install dependencies, build the client, start the normal server, load
.env, seed application data, or contact an existing database. Credentials are
random, stay in memory, stdin and the isolated child environment, and are never
printed. The internal stdin control
channel changes test fixtures only; it is not an HTTP mutation API.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import secrets
import shutil
import subprocess
import sys
import tempfile
import threading
from urllib.error import HTTPError, URLError
from urllib.request import ProxyHandler, Request, build_opener


REPOSITORY = Path(__file__).resolve().parents[2]
SERVER = REPOSITORY / "RemoteWork.Server"
STATUSES = ("not_started", "in_progress", "submitted", "approved", "returned")
LARGE_TASK_ID = "9007199254740993"  # Above the exact-integer range of IEEE-754 doubles.
STAFF_OWN = [str(value) for value in range(101, 106)] + [LARGE_TASK_ID]
ADMIN_OWN = [str(value) for value in range(201, 206)]
# The larger IDs have earlier due dates, so an ID-only sort cannot pass.
ADMIN_MANAGED = [str(value) for value in range(301, 306)] + STAFF_OWN


class RunFailure(Exception):
    """Messages contain static test labels, never response or credential data."""


class MissingServerDependencies(RunFailure):
    pass


class Checks:
    def __init__(self):
        self.total = 0
        self.revocation = 0

    def check(self, condition, label, *, revocation=False):
        if not condition:
            raise RunFailure(label)
        self.total += 1
        if revocation:
            self.revocation += 1


def isolated_environment(runtime):
    # Use an allowlist instead of trying to enumerate every possible real DB,
    # API, proxy, Python injection, credential, or application-setting variable.
    allowed = {
        "systemroot", "windir", "systemdrive", "comspec", "path", "pathext",
        "processor_architecture", "processor_identifier", "number_of_processors",
        "programfiles", "programfiles(x86)", "programw6432", "os", "dotnet_root",
        "dotnet_root(x86)", "dotnet_root_x64", "dotnet_root_x86",
    }
    env = {key: value for key, value in os.environ.items() if key.lower() in allowed}
    for name in ("temp", "profile", "appdata", "localappdata", "dotnet"):
        (runtime / name).mkdir()
    env.update({
        "TEMP": str(runtime / "temp"), "TMP": str(runtime / "temp"),
        "USERPROFILE": str(runtime / "profile"),
        "APPDATA": str(runtime / "appdata"), "LOCALAPPDATA": str(runtime / "localappdata"),
        "DOTNET_CLI_HOME": str(runtime / "dotnet"), "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1",
        "PYTHONDONTWRITEBYTECODE": "1", "PYTHONNOUSERSITE": "1",
        "PYTHONUNBUFFERED": "1", "PYTHONUTF8": "1", "PYTHONIOENCODING": "utf-8",
        "PYTHON_DOTENV_DISABLED": "1", "REMOTEWORK_ENABLE_TASK_QUERY_DRAFT": "1",
        "SESSION_HOURS": "1", "COOKIE_SECURE": "false",
        "COMPANY_NAME": "Explicit temporary task fixture", "NO_PROXY": "127.0.0.1,localhost",
    })
    # Both are owned paths under TemporaryDirectory, never inherited settings.
    env["DATABASE_URL"] = "sqlite:///" + (runtime / "fixture.db").as_posix()
    env["REMOTEWORK_API_URL"] = "http://127.0.0.1:1/"
    return env


class FixtureServer:
    def __init__(self, env, runtime, accounts):
        self.env, self.runtime, self.accounts = env, runtime, accounts
        self.process = None
        self.messages = queue.Queue()
        self.request_number = 0

    def __enter__(self):
        self.process = subprocess.Popen(
            [sys.executable, str(Path(__file__).resolve()), "--fixture-server"],
            cwd=self.runtime, env=self.env, stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            text=True, encoding="utf-8", bufsize=1,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )

        def read_messages():
            try:
                for line in self.process.stdout:
                    try:
                        self.messages.put(json.loads(line))
                    except (ValueError, TypeError):
                        self.messages.put({"ok": False, "failure_type": "InvalidControlMessage"})
            finally:
                self.messages.put({"ok": False, "failure_type": "ServerExited"})

        threading.Thread(target=read_messages, daemon=True).start()
        try:
            result = self.command("start", accounts=self.accounts,
                                  secret=secrets.token_urlsafe(48), timeout=60)
            port = result.get("port")
            if not isinstance(port, int) or not 1 <= port <= 65535:
                raise RunFailure("fixture server returned an invalid loopback port")
            self.url = "http://127.0.0.1:" + str(port) + "/"
            return self
        except BaseException:
            self.__exit__(None, None, None)
            raise

    def command(self, action, timeout=20, **payload):
        self.request_number += 1
        try:
            self.process.stdin.write(json.dumps({"id": self.request_number, "action": action, **payload}) + "\n")
            self.process.stdin.flush()
            result = self.messages.get(timeout=timeout)
        except (BrokenPipeError, OSError, queue.Empty):
            raise RunFailure("fixture control channel failed or timed out") from None
        if result.get("failure_type") == "MissingServerDependencies":
            raise RunFailure("missing server dependencies; run python -m pip install -r .\\RemoteWork.Server\\requirements.txt")
        if result.get("id") != self.request_number or result.get("ok") is not True:
            raise RunFailure("fixture server failed during " + action)
        return result

    def __exit__(self, exc_type, _exc, _traceback):
        if self.process is None:
            return
        clean = False
        try:
            if self.process.poll() is None:
                self.command("stop", timeout=20)
                self.process.wait(timeout=20)
                clean = self.process.returncode == 0
            else:
                clean = self.process.returncode == 0
        except (RunFailure, subprocess.TimeoutExpired):
            pass
        finally:
            if self.process.poll() is None:
                self.process.terminate()
                try:
                    self.process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    self.process.kill()
                    self.process.wait(timeout=10)
            for stream in (self.process.stdin, self.process.stdout):
                if stream:
                    stream.close()
        if exc_type is None and not clean:
            raise RunFailure("fixture server did not shut down cleanly")


class HttpChecks:
    def __init__(self, url, checks):
        self.url, self.checks = url, checks
        # Ignore OS/registry proxies too: only this owned loopback server is used.
        self.http = build_opener(ProxyHandler({}))

    def request(self, path, status=200, token=None, body=None, code=None):
        headers = {"Accept": "application/json"}
        if token:
            headers["Authorization"] = "Bearer " + token
        encoded = None
        if body is not None:
            encoded = json.dumps(body).encode("utf-8")
            headers["Content-Type"] = "application/json"
        request = Request(self.url + "api/v1/" + path, data=encoded, headers=headers)
        try:
            response = self.http.open(request, timeout=15)
        except HTTPError as error:
            response = error
        except (URLError, TimeoutError, OSError):
            raise RunFailure("loopback HTTP request failed or timed out") from None
        try:
            with response:
                actual_status = response.status
                request_id = response.headers.get("X-Request-ID")
                raw = response.read(1024 * 1024 + 1)
            if len(raw) > 1024 * 1024:
                raise RunFailure("loopback response exceeded the test limit")
            data = json.loads(raw)
        except (ValueError, UnicodeError, TypeError):
            raise RunFailure("loopback response was not a common JSON envelope") from None
        self.checks.check(actual_status == status, "unexpected HTTP status for " + path)
        self.checks.check(isinstance(data, dict) and data.get("ok") is (status < 400),
                          "common response success/error envelope")
        self.checks.check(bool(request_id) and data.get("request_id") == request_id,
                          "common request ID correlation")
        if status >= 400:
            self.checks.check(isinstance(data.get("error"), dict)
                              and data["error"].get("code") == code,
                              "common error code for " + path)
            self.checks.check("data" not in data, "denied response contains no task data")
        return data.get("data", {})

    def login(self, account):
        data = self.request("auth/login", body={"login_id": account["login_id"], "password": account["password"]})
        token = data.get("access_token")
        self.checks.check(isinstance(token, str) and bool(token), "login supplies a Bearer token")
        return token

    def items(self, token, scope="own", status=None):
        path = "tasks?scope=" + scope + ("&status=" + status if status else "")
        data = self.request(path, token=token)
        self.checks.check(isinstance(data.get("items"), list), "list DTO items array")
        return data["items"]

    def ids(self, token, scope="own", status=None):
        return [item["task_id"] for item in self.items(token, scope, status)]


def run_client(dotnet, dll, env, fixture, account, own, managed, checks):
    live_env = dict(env, REMOTEWORK_TASK_TEST_ALLOW_LIVE="1",
                    REMOTEWORK_TASK_TEST_URL=fixture.url, REMOTEWORK_API_URL=fixture.url,
                    REMOTEWORK_TASK_TEST_LOGIN_ID=account["login_id"],
                    REMOTEWORK_TASK_TEST_PASSWORD=account["password"],
                    REMOTEWORK_TASK_TEST_FORBIDDEN_TASK_ID="401",
                    REMOTEWORK_TASK_TEST_EXPECTED_OWN_IDS=json.dumps(own),
                    REMOTEWORK_TASK_TEST_EXPECTED_MANAGED_IDS=json.dumps(managed))
    try:
        result = subprocess.run([dotnet, str(dll), "--live"], cwd=fixture.runtime,
                                env=live_env, capture_output=True, text=True,
                                encoding="utf-8", errors="replace", timeout=180,
                                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    except subprocess.TimeoutExpired:
        raise RunFailure("C# --live client timed out") from None
    # Never echo arbitrary client output, which may include future exception text.
    failure = re.search(r"^FAIL after ([0-9]{1,9}) assertions: ([A-Za-z][A-Za-z0-9_.]{0,100}):",
                        result.stderr, re.MULTILINE)
    diagnostic = (" (after " + failure[1] + " assertions; " + failure[2] + ")") if failure else ""
    checks.check(result.returncode == 0, "C# --live client failed for " + account["role"] + diagnostic)
    summary = re.search(r"^PASS: (\d+) scenarios / (\d+) assertions\.", result.stdout, re.MULTILINE)
    checks.check(summary is not None, "C# --live client supplied its success summary")
    print("PASS C# " + account["role"] + " --live: " + summary[1]
          + " scenarios / " + summary[2] + " assertions (temporary fixture).", flush=True)


def verify_http(fixture, accounts, checks):
    http = HttpChecks(fixture.url, checks)
    tokens = {name: http.login(account) for name, account in accounts.items()}
    staff, admin, other_staff, other_admin = (tokens[name] for name in ("staff", "admin", "other_staff", "other_admin"))
    before = fixture.command("snapshot")["database"]
    checks.check(http.ids(staff) == STAFF_OWN, "employee exact own set and ordering")
    checks.check(http.ids(admin) == ADMIN_OWN, "admin exact own set and ordering")
    checks.check(http.ids(admin, "managed") == ADMIN_MANAGED, "admin exact explicitly managed set and ordering")
    checks.check(http.ids(other_staff) == [str(value) for value in range(301, 306)] + ["401"],
                 "other employee cannot acquire a different employee's tasks")
    checks.check(http.ids(other_admin, "managed") == ADMIN_OWN + ["401"],
                 "other admin receives only that admin's explicit manager relations")
    checks.check(http.ids(tokens["empty"]) == [], "empty own scope is a successful empty list")
    http.request("tasks/101", status=403, token=tokens["empty"], code="FORBIDDEN")
    for index, status in enumerate(STATUSES):
        large = [LARGE_TASK_ID] if status == "in_progress" else []
        checks.check(http.ids(staff, status=status) == [STAFF_OWN[index]] + large, "employee exact five-status subset")
        checks.check(http.ids(admin, status=status) == [ADMIN_OWN[index]], "admin exact own five-status subset")
        checks.check(http.ids(admin, "managed", status) == [str(301 + index), STAFF_OWN[index]] + large,
                     "admin exact managed five-status subset")
    for token, task_id in ((staff, "101"), (admin, "201"), (admin, "301")):
        detail = http.request("tasks/" + task_id, token=token)
        checks.check(detail.get("task", {}).get("task_id") == task_id,
                     "allowed own or explicitly managed detail identity")
    large_detail = http.request("tasks/" + LARGE_TASK_ID, token=staff)
    checks.check(large_detail.get("task", {}).get("task_id") == LARGE_TASK_ID,
                 "signed BIGINT above 2^53 round-trips as an exact string ID")
    http.request("tasks?scope=managed", status=403, token=staff, code="FORBIDDEN")
    http.request("tasks/401", status=403, token=staff, code="FORBIDDEN")
    http.request("tasks/401", status=403, token=admin, code="FORBIDDEN")
    http.request("tasks/101", status=403, token=other_admin, code="FORBIDDEN")
    http.request("tasks/999999", status=404, token=staff, code="TASK_NOT_FOUND")
    http.request("tasks", status=401, code="AUTH_REQUIRED")
    http.request("tasks/101", status=401, code="AUTH_REQUIRED")
    checks.check(fixture.command("snapshot")["database"] == before,
                 "all application DB rows remain unchanged by authenticated/denied GET requests")

    # Mutations below are solely in this explicit, disposable fixture control
    # channel. No create/reassign/delete/status-write HTTP endpoint is added.
    checks.check(http.request("tasks/301", token=admin)["task"]["task_id"] == "301",
                 "manager has detail access before relation removal", revocation=True)
    fixture.command("remove_manager")
    before = fixture.command("snapshot")["database"]
    checks.check(http.ids(admin, "managed") == [value for value in ADMIN_MANAGED if value != "301"],
                 "removed manager is excluded from the exact managed list", revocation=True)
    http.request("tasks/301", status=403, token=admin, code="FORBIDDEN")
    checks.check(True, "removed manager direct detail is forbidden", revocation=True)
    checks.check(http.ids(admin) == ADMIN_OWN, "manager removal preserves own scope", revocation=True)
    checks.check(fixture.command("snapshot")["database"] == before,
                 "manager-revocation reads leave the same DB unchanged", revocation=True)

    checks.check(http.request("tasks/101", token=staff)["task"]["task_id"] == "101",
                 "old assignee has detail access before reassignment", revocation=True)
    fixture.command("reassign")
    before = fixture.command("snapshot")["database"]
    checks.check(http.ids(staff) == STAFF_OWN[1:], "old assignee loses the exact current own list entry", revocation=True)
    http.request("tasks/101", status=403, token=staff, code="FORBIDDEN")
    checks.check(True, "old assignee direct detail is forbidden", revocation=True)
    checks.check(http.ids(other_staff) == [str(value) for value in range(301, 306)] + ["101", "401"],
                 "new assignee receives the exact new own list and ordering", revocation=True)
    detail = http.request("tasks/101", token=other_staff)["task"]
    checks.check(detail["task_id"] == "101" and detail["assignee_id"] == fixture.user_ids["other_staff"],
                 "new assignee can read the current detail identity", revocation=True)
    checks.check(fixture.command("snapshot")["database"] == before,
                 "reassignment reads leave the same DB unchanged", revocation=True)

    for token in tokens.values():
        http.request("auth/logout", token=token, body={})
        http.request("tasks", status=401, token=token, code="AUTH_REQUIRED")
        http.request("tasks/101", status=401, token=token, code="AUTH_REQUIRED")
    print("PASS direct loopback HTTP: exact own/managed scopes, all five filters, 401/403/404, "
          "logout token rejection, manager removal and reassignment.", flush=True)


def run(dll):
    if os.name != "nt":
        raise RunFailure("Windows is required for the existing WinForms --live client")
    if not dll.is_file():
        print("Build first: dotnet build .\\Tests\\TaskQueries.Http.Tests\\TaskQueries.Http.Tests.csproj -c Release",
              file=sys.stderr)
        raise RunFailure("C# client DLL is missing; no test was run")
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RunFailure(".NET 8 runtime/SDK is missing; no test was run")
    checks = Checks()
    accounts = {name: {"login_id": "fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("staff", "employee"), ("admin", "admin"),
                                   ("other_staff", "employee"), ("other_admin", "admin"), ("empty", "employee"))}
    print("Explicit test fixture: new temporary SQLite database, synthetic accounts, random loopback port. "
          "This is not shared/production or two-PC integration.", flush=True)
    with tempfile.TemporaryDirectory(prefix="remote_work_task_http_") as temporary:
        runtime = Path(temporary)
        env = isolated_environment(runtime)
        with FixtureServer(env, runtime, accounts) as fixture:
            fixture.user_ids = fixture.command("identity_ids")["users"]
            before = fixture.command("snapshot")["feature"]
            run_client(dotnet, dll, env, fixture, accounts["staff"], STAFF_OWN, [], checks)
            run_client(dotnet, dll, env, fixture, accounts["admin"], ADMIN_OWN, ADMIN_MANAGED, checks)
            checks.check(fixture.command("snapshot")["feature"] == before,
                         "C# live reads do not change any task, assignment or manager row")
            verify_http(fixture, accounts, checks)
        checks.check(fixture.process.poll() == 0, "owned fixture server exited cleanly")
    checks.check(not Path(temporary).exists(), "temporary DB and runtime paths were removed")
    print(f"PASS runner: {checks.total} checks; {checks.revocation} explicit permission-revocation checks. "
          "Owned server stopped; temporary DB/runtime removed. No shared DB, application settings or member 3 writes were used.",
          flush=True)


def fixture_server():
    """Internal child: no inherited config, public seed hook, or HTTP write API."""
    app = server = thread = None
    message_id = None
    try:
        first = json.loads(sys.stdin.readline())
        message_id = first.get("id")
        if first.get("action") != "start" or os.environ.get("PYTHON_DOTENV_DISABLED") != "1":
            raise RunFailure("explicit fixture startup is required")
        database_path = Path.cwd() / "fixture.db"
        if database_path.exists() or not database_path.parent.name.startswith("remote_work_task_http_"):
            raise RunFailure("refusing a preexisting or non-fixture database")
        expected_url = "sqlite:///" + database_path.as_posix()
        if os.environ.get("DATABASE_URL") != expected_url:
            raise RunFailure("database URL must be this fixture's new SQLite path")
        sys.path.insert(0, str(SERVER))
        try:
            import remotework
            from remotework.auth import create_user
            from remotework.db import init_schema, transaction
            from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
            from sqlalchemy import delete, select
            from werkzeug.serving import WSGIRequestHandler, make_server
        except ImportError:
            raise MissingServerDependencies() from None
        # Defense in depth: even older python-dotenv cannot read repository .env.
        remotework.load_dotenv = lambda *_args, **_kwargs: False
        app = remotework.create_app({"DATABASE_URL": expected_url, "SECRET_KEY": first["secret"],
                                     "TESTING": False, "SESSION_HOURS": 1, "COOKIE_SECURE": False,
                                     "ENABLE_TASK_QUERY_DRAFT": True})
        app.logger.disabled = True
        from datetime import date

        with app.app_context():
            init_schema()  # Only the previously nonexistent owned temporary DB.
            with transaction() as db:
                users = {}
                for name, account in first["accounts"].items():
                    users[name] = create_user(db, {**account, "display_name": "Fixture " + name,
                                                  "employee_no": "FIX-" + name, "department": "Test fixture"})
                user_ids = {name: user.id for name, user in users.items()}

                def add(task_id, assignee, manager, status, due_day=20):
                    db.add(TaskRecord(task_id=task_id, title="Fixture task " + str(task_id),
                                      description="Explicit temporary fixture description " + str(task_id),
                                      task_status=status, start_date=date(2026, 10, 17), due_date=date(2026, 10, due_day),
                                      current_assignment_revision=1, row_version=1, created_by=user_ids["admin"]))
                    db.flush()
                    db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=user_ids[assignee],
                                          assigned_by=user_ids["admin"]))
                    db.add(TaskManager(task_id=task_id, user_id=user_ids[manager]))

                for offset, status in enumerate(STATUSES):
                    add(101 + offset, "staff", "admin", status)
                    add(201 + offset, "admin", "other_admin", status)
                    add(301 + offset, "other_staff", "admin", status, due_day=19)
                add(int(LARGE_TASK_ID), "staff", "admin", "in_progress")
                add(401, "other_staff", "other_admin", "not_started")

        class QuietRequestHandler(WSGIRequestHandler):
            def log(self, *_args, **_kwargs):
                pass

        server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=QuietRequestHandler)
        thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.1), daemon=True)
        thread.start()
        print(json.dumps({"id": message_id, "ok": True, "port": server.server_port}), flush=True)

        for line in sys.stdin:
            message = json.loads(line)
            message_id, action = message.get("id"), message.get("action")
            result = {}
            if action == "stop":
                break
            if action == "identity_ids":
                result["users"] = user_ids
            elif action == "snapshot":
                import sqlite3
                # Hash the complete application DB without sending password
                # hashes, sessions, raw rows or dumps through the control pipe.
                with sqlite3.connect(database_path.as_uri() + "?mode=ro", uri=True) as connection:
                    digest = hashlib.sha256()
                    for statement in connection.iterdump():
                        digest.update(statement.encode("utf-8"))
                        digest.update(b"\n")
                    result["database"] = digest.hexdigest()
                    rows = {name: connection.execute("SELECT * FROM " + name + " ORDER BY 1, 2").fetchall()
                            for name in ("tasks", "task_assignments", "task_managers")}
                    result["feature"] = hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()
            elif action == "remove_manager":
                with app.app_context(), transaction() as db:
                    changed = db.execute(delete(TaskManager).where(TaskManager.task_id == 301,
                                                                   TaskManager.user_id == user_ids["admin"]))
                    if changed.rowcount != 1:
                        raise RunFailure("expected exactly one fixture manager relation")
            elif action == "reassign":
                with app.app_context(), transaction() as db:
                    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == 101))
                    if task.current_assignment_revision != 1:
                        raise RunFailure("fixture task was already reassigned")
                    db.add(TaskAssignment(task_id=101, revision=2, assignee_id=user_ids["other_staff"],
                                          assigned_by=user_ids["admin"]))
                    task.current_assignment_revision = 2
                    task.row_version += 1
            else:
                raise RunFailure("unknown fixture control action")
            print(json.dumps({"id": message_id, "ok": True, **result}), flush=True)
        else:
            raise RunFailure("fixture control input closed without shutdown")
        server.shutdown()
        server.server_close()
        thread.join(timeout=10)
        if thread.is_alive():
            raise RunFailure("fixture server thread did not stop")
        app.extensions["db_engine"].dispose()
        app = server = None
        print(json.dumps({"id": message_id, "ok": True}), flush=True)
        return 0
    except BaseException as error:
        # No exception message/traceback or response content is exposed.
        print(json.dumps({"id": message_id, "ok": False, "failure_type": type(error).__name__}), flush=True)
        return 1
    finally:
        if server is not None:
            server.shutdown()
            server.server_close()
        if app is not None:
            app.extensions["db_engine"].dispose()


def main():
    if sys.argv[1:] == ["--fixture-server"]:
        return fixture_server()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--client-dll", type=Path,
                        default=Path(__file__).resolve().parent / "bin/Release/net8.0-windows/TaskQueries.Http.Tests.dll")
    args = parser.parse_args()
    try:
        run(args.client_dll.resolve())
        return 0
    except RunFailure as error:
        print("FAIL: " + str(error) + ". Credentials and response bodies are suppressed.", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("FAIL: interrupted; the owned child is stopped and temporary paths are cleaned.", file=sys.stderr)
        return 130
    except Exception as error:
        print("FAIL: " + type(error).__name__ + "; credentials and response bodies are suppressed.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
