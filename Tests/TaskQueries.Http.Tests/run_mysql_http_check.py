"""Run the existing C# live suite against a newly owned, disposable MySQL server.

No existing DB URL, .env, MySQL service, SQLite database, public seed endpoint or
product write contract is used. Requires Windows, MySQL >=8.0.16, server Python
dependencies and a prebuilt TaskQueries.Http.Tests.dll. Never prints credentials,
raw DB rows, HTTP bodies, child output or exception tracebacks.
"""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import ctypes
from ctypes import wintypes
from datetime import date, datetime
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import signal
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time

import run_http_check as shared


PREFIX = "remote_work_task_mysql_http_"
DATABASE_PATTERN = r"remote_work_test_m2http_[0-9a-f]{24}"
SCRIPT = Path(__file__).resolve()
SQL_PATH = shared.SERVER / "migrations" / "002_task_queries.mysql.sql"
CREATE_SUSPENDED = 0x00000004


def validate_runtime_path(runtime, parent):
    """Check the final absolute Windows deletion target, not a computed prefix alone."""
    runtime, parent = Path(runtime), Path(parent).resolve(strict=True)
    if (not runtime.is_absolute() or not runtime.name.startswith(PREFIX)
            or runtime.parent != parent or runtime.is_symlink()
            or runtime.resolve(strict=True) != runtime):
        raise shared.RunFailure("refusing an unowned or redirected temporary runtime")
    # Junctions/reparse points must not redirect the recursive deletion root.
    if getattr(runtime.lstat(), "st_file_attributes", 0) & 0x400:
        raise shared.RunFailure("refusing a reparse-point temporary runtime")
    return runtime


@contextmanager
def owned_runtime():
    parent = Path(tempfile.gettempdir()).resolve(strict=True)
    runtime = Path(tempfile.mkdtemp(prefix=PREFIX, dir=parent)).resolve(strict=True)
    try:
        yield validate_runtime_path(runtime, parent)
    finally:
        validate_runtime_path(runtime, parent)
        shutil.rmtree(runtime)
        if runtime.exists():
            raise shared.RunFailure("owned temporary runtime was not removed")


def validate_binary_directory(directory):
    directory = Path(directory)
    if not directory.is_absolute():
        raise shared.RunFailure("mysql-bin-dir must be an explicit absolute directory")
    executable = directory / "mysqld.exe"
    if not directory.is_dir() or not executable.is_file():
        raise shared.RunFailure("mysqld.exe was not found in mysql-bin-dir")
    return executable.resolve(strict=True)


def validate_instance(values, datadir, port):
    """Must succeed before CREATE, DROP or SHUTDOWN, including during cleanup."""
    actual_dir, actual_port, version, engine, isolation = values
    numbers = re.match(r"(\d+)\.(\d+)\.(\d+)", str(version))
    try:
        same_directory = Path(str(actual_dir)).resolve(strict=True) == Path(datadir).resolve(strict=True)
    except (OSError, ValueError):
        same_directory = False
    if (not same_directory or int(actual_port) != port or not numbers
            or "mariadb" in str(version).lower()
            or tuple(map(int, numbers.groups())) < (8, 0, 16)
            or str(engine).lower() != "innodb"
            or str(isolation).upper() != "REPEATABLE-READ"):
        raise shared.RunFailure("MySQL identity, version, engine or isolation did not match the owned fixture")


IDENTITY_SQL = "SELECT @@datadir, @@port, VERSION(), @@default_storage_engine, @@transaction_isolation"


class WindowsJob:
    """A job handle confines failure cleanup to this run's native process tree."""
    def __init__(self):
        class BasicLimits(ctypes.Structure):
            _fields_ = [("PerProcessUserTimeLimit", ctypes.c_longlong),
                        ("PerJobUserTimeLimit", ctypes.c_longlong), ("LimitFlags", wintypes.DWORD),
                        ("MinimumWorkingSetSize", ctypes.c_size_t), ("MaximumWorkingSetSize", ctypes.c_size_t),
                        ("ActiveProcessLimit", wintypes.DWORD), ("Affinity", ctypes.c_size_t),
                        ("PriorityClass", wintypes.DWORD), ("SchedulingClass", wintypes.DWORD)]

        class IoCounters(ctypes.Structure):
            _fields_ = [(name, ctypes.c_ulonglong) for name in (
                "ReadOperationCount", "WriteOperationCount", "OtherOperationCount", "ReadTransferCount",
                "WriteTransferCount", "OtherTransferCount")]

        class ExtendedLimits(ctypes.Structure):
            _fields_ = [("BasicLimitInformation", BasicLimits), ("IoInfo", IoCounters),
                        ("ProcessMemoryLimit", ctypes.c_size_t), ("JobMemoryLimit", ctypes.c_size_t),
                        ("PeakProcessMemoryUsed", ctypes.c_size_t), ("PeakJobMemoryUsed", ctypes.c_size_t)]

        self.kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
        self.kernel.CreateJobObjectW.restype = wintypes.HANDLE
        self.kernel.SetInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int,
                                                       ctypes.c_void_p, wintypes.DWORD]
        self.kernel.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
        self.kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        self.handle = self.kernel.CreateJobObjectW(None, None)
        limits = ExtendedLimits()
        limits.BasicLimitInformation.LimitFlags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        if not self.handle or not self.kernel.SetInformationJobObject(
                self.handle, 9, ctypes.byref(limits), ctypes.sizeof(limits)):
            self.close()
            raise shared.RunFailure("could not confine owned native processes to a Windows job")

    def assign(self, process):
        if not self.kernel.AssignProcessToJobObject(self.handle, wintypes.HANDLE(process._handle)):
            raise shared.RunFailure("could not confine the owned MySQL process")
        self._resume_primary_thread(process.pid)

    def _resume_primary_thread(self, process_id):
        # Popen closes its primary-thread handle. Reopen only this still-suspended
        # process's single thread after job assignment, before it can spawn children.
        class ThreadEntry(ctypes.Structure):
            _fields_ = [("dwSize", wintypes.DWORD), ("cntUsage", wintypes.DWORD),
                        ("th32ThreadID", wintypes.DWORD), ("th32OwnerProcessID", wintypes.DWORD),
                        ("tpBasePri", wintypes.LONG), ("tpDeltaPri", wintypes.LONG),
                        ("dwFlags", wintypes.DWORD)]
        kernel = self.kernel
        kernel.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
        kernel.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
        kernel.Thread32First.argtypes = [wintypes.HANDLE, ctypes.POINTER(ThreadEntry)]
        kernel.Thread32Next.argtypes = [wintypes.HANDLE, ctypes.POINTER(ThreadEntry)]
        kernel.OpenThread.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel.OpenThread.restype = wintypes.HANDLE
        kernel.ResumeThread.argtypes = [wintypes.HANDLE]
        kernel.ResumeThread.restype = wintypes.DWORD
        snapshot = kernel.CreateToolhelp32Snapshot(0x00000004, 0)  # TH32CS_SNAPTHREAD
        if snapshot == wintypes.HANDLE(-1).value:
            raise shared.RunFailure("could not inspect the owned suspended process thread")
        try:
            entry = ThreadEntry()
            entry.dwSize = ctypes.sizeof(entry)
            found = []
            valid = kernel.Thread32First(snapshot, ctypes.byref(entry))
            while valid:
                if entry.th32OwnerProcessID == process_id:
                    found.append(entry.th32ThreadID)
                valid = kernel.Thread32Next(snapshot, ctypes.byref(entry))
            if len(found) != 1:
                raise shared.RunFailure("owned suspended process did not have exactly one primary thread")
            thread = kernel.OpenThread(0x0002, False, found[0])  # THREAD_SUSPEND_RESUME
            if not thread:
                raise shared.RunFailure("could not open the owned suspended process thread")
            try:
                if kernel.ResumeThread(thread) != 1:
                    raise shared.RunFailure("could not resume the owned process after job assignment")
            finally:
                kernel.CloseHandle(thread)
        finally:
            kernel.CloseHandle(snapshot)

    def close(self):
        if getattr(self, "handle", None):
            self.kernel.CloseHandle(self.handle)
            self.handle = None


@contextmanager
def defer_spawn_interrupt():
    """Do not lose a returned Popen handle while Ctrl+C arrives during registration."""
    pending = False
    def remember(_signum, _frame):
        nonlocal pending
        pending = True
    previous = signal.signal(signal.SIGINT, remember)
    try:
        yield
    finally:
        signal.signal(signal.SIGINT, previous)
    if pending:
        raise KeyboardInterrupt


def stop_owned_process(process):
    if process is None:
        return
    if process.poll() is None:
        process.terminate()
    try:
        process.wait(timeout=20)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=20)


def run_owned_client(command, **options):
    """subprocess.run-compatible capture used only by this MySQL test runner."""
    timeout = options.pop("timeout", None)
    if options.pop("capture_output", False) is not True:
        raise shared.RunFailure("owned client execution requires captured output")
    process = job = None
    try:
        job = WindowsJob()
        with defer_spawn_interrupt():
            options["creationflags"] = options.get("creationflags", 0) | CREATE_SUSPENDED
            process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, **options)
            job.assign(process)
        stdout, stderr = process.communicate(timeout=timeout)
        return subprocess.CompletedProcess(command, process.returncode, stdout, stderr)
    finally:
        if job is not None:
            job.close()  # Also terminates any dotnet descendants after interrupt/timeout.
        stop_owned_process(process)
        if process is not None:
            for stream in (process.stdout, process.stderr):
                if stream:
                    stream.close()


class OwnedMySQL:
    def __init__(self, executable, runtime, env):
        self.executable, self.runtime, self.env = executable, runtime, env
        self.datadir = runtime / "mysql-data"
        self.process = self.job = None
        self.native_processes = []
        self.port = None
        self.password = ""
        self.database = None  # Only a successful CREATE confers cleanup ownership.
        self.identity_verified = False
        self.clean = False

    def _connect(self):
        import pymysql
        return pymysql.connect(host="127.0.0.1", port=self.port, user="root", password=self.password,
                               charset="utf8mb4", autocommit=True, connect_timeout=2,
                               read_timeout=5, write_timeout=5)

    def _verify(self, connection):
        with connection.cursor() as cursor:
            cursor.execute(IDENTITY_SQL)
            validate_instance(cursor.fetchone(), self.datadir, self.port)

    def _spawn(self, command):
        process = None
        try:
            with defer_spawn_interrupt():
                process = subprocess.Popen(command, cwd=self.runtime, env=self.env,
                                           stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                           stderr=subprocess.DEVNULL,
                                           creationflags=subprocess.CREATE_NO_WINDOW | CREATE_SUSPENDED)
                self.native_processes.append(process)
                self.job.assign(process)
            return process
        except BaseException:
            stop_owned_process(process)
            raise

    def __enter__(self):
        try:
            if self.datadir.exists():
                raise shared.RunFailure("refusing to initialize an existing MySQL datadir")
            self.datadir.mkdir()
            self.job = WindowsJob()
            initialize = self._spawn([str(self.executable), "--no-defaults", "--initialize-insecure",
                                      "--datadir=" + str(self.datadir)])
            try:
                if initialize.wait(timeout=120) != 0:
                    raise shared.RunFailure("owned MySQL initialization failed")
            except subprocess.TimeoutExpired:
                raise shared.RunFailure("owned MySQL initialization timed out") from None
            with socket.socket() as candidate:
                candidate.bind(("127.0.0.1", 0))
                self.port = candidate.getsockname()[1]
            self.process = self._spawn([
                str(self.executable), "--no-defaults", "--datadir=" + str(self.datadir),
                "--bind-address=127.0.0.1", "--port=" + str(self.port), "--mysqlx=0", "--console",
                "--local-infile=0", "--secure-file-priv=NULL", "--skip-log-bin",
                "--default-storage-engine=InnoDB", "--character-set-server=utf8mb4",
                "--collation-server=utf8mb4_unicode_ci",
            ])
            import pymysql
            deadline = time.monotonic() + 60
            while time.monotonic() < deadline:
                if self.process.poll() is not None:
                    raise shared.RunFailure("owned MySQL exited before startup")
                try:
                    connection = self._connect()
                    break
                except pymysql.MySQLError:
                    time.sleep(0.1)
            else:
                raise shared.RunFailure("owned MySQL startup timed out")
            with connection:
                self._verify(connection)  # A port race must never authorize mutations on another server.
                self.identity_verified = True
                password = secrets.token_urlsafe(40)
                with connection.cursor() as cursor:
                    cursor.execute("ALTER USER 'root'@'localhost' IDENTIFIED BY %s", (password,))
                self.password = password
            candidate_name = "remote_work_test_m2http_" + secrets.token_hex(12)
            if not re.fullmatch(DATABASE_PATTERN, candidate_name):
                raise shared.RunFailure("invalid generated fixture database name")
            with self._connect() as connection:
                self._verify(connection)
                with connection.cursor() as cursor:
                    cursor.execute("CREATE DATABASE `" + candidate_name
                                   + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")
                self.database = candidate_name
            from sqlalchemy import URL
            self.url = URL.create("mysql+pymysql", username="root", password=self.password,
                                  host="127.0.0.1", port=self.port, database=self.database,
                                  query={"charset": "utf8mb4"})
            return self
        except BaseException:
            self.__exit__(*sys.exc_info())
            raise

    def _cleanup_database(self):
        # Never connect for SQL cleanup before a successful identity check in startup.
        if not self.identity_verified or self.process is None or self.process.poll() is not None:
            return self.database is None
        with self._connect() as connection:
            self._verify(connection)  # Recheck before each destructive cleanup connection.
            with connection.cursor() as cursor:
                if self.database is not None:
                    if not re.fullmatch(DATABASE_PATTERN, self.database):
                        raise shared.RunFailure("refusing an unowned database cleanup target")
                    cursor.execute("DROP DATABASE `" + self.database + "`")
                    self.database = None
                cursor.execute("SHUTDOWN")
        return True

    def __exit__(self, exc_type, _exc, _traceback):
        cleaned_database = False
        try:
            cleaned_database = self._cleanup_database()
        except Exception:
            # A failed recheck must never fall back to DROP/SHUTDOWN on the port.
            pass
        finally:
            if self.process is not None:
                try:
                    self.process.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    pass
            if self.job is not None:
                self.job.close()  # Only processes assigned to this owned job can be terminated.
            for native in self.native_processes:
                try:
                    native.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    raise shared.RunFailure("owned native process did not stop") from None
            if self.process is not None:
                try:
                    self.process.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    raise shared.RunFailure("owned MySQL process did not stop") from None
            self.clean = cleaned_database and (self.process is None or self.process.poll() is not None)
        if exc_type is None and not self.clean:
            raise shared.RunFailure("owned MySQL database cleanup did not finish normally")


def _table_sets():
    from remotework.models import (AdminGuard, AuditEvent, AuthSession, LoginFailure, ResourceGrant,
                                   SchemaVersion, User)
    from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
    common = tuple(model.__table__ for model in (
        User, AuthSession, LoginFailure, AuditEvent, ResourceGrant, SchemaVersion, AdminGuard))
    task = tuple(model.__table__ for model in (TaskRecord, TaskAssignment, TaskManager))
    return common, task


def _digest_rows(connection, tables):
    from sqlalchemy import select
    def encode(value):
        if isinstance(value, (date, datetime)):
            return value.isoformat()
        if isinstance(value, bytes):
            return {"bytes": value.hex()}
        raise TypeError("unsupported fixture column type")
    rows = {table.name: [list(row) for row in connection.execute(
        select(table).order_by(*table.primary_key.columns))] for table in tables}
    # Raw rows, password hashes and sessions never leave this process.
    return hashlib.sha256(json.dumps(rows, sort_keys=True, ensure_ascii=True,
                                     default=encode, separators=(",", ":")).encode("utf-8")).hexdigest()


def _ddl_statements():
    text = "\n".join(line for line in SQL_PATH.read_text(encoding="utf-8").splitlines()
                     if not line.lstrip().startswith("--"))
    statements = [part.strip() for part in text.split(";") if part.strip()]
    if (len(statements) != 3 or [re.match(r"CREATE TABLE (\w+)", part).group(1)
                                for part in statements] != ["tasks", "task_assignments", "task_managers"]
            or any(value in text.upper() for value in ("SCHEMA_VERSIONS", "DROP TABLE", "IF NOT EXISTS"))):
        raise shared.RunFailure("reserved task DDL did not contain only the expected three creates")
    return statements


def fixture_server():
    """Internal stdin fixture control, never a product mutation HTTP endpoint."""
    app = server = thread = None
    message_id = None
    try:
        first = json.loads(sys.stdin.readline())
        message_id = first.get("id")
        if (first.get("action") != "start" or os.environ.get("PYTHON_DOTENV_DISABLED") != "1"
                or not Path.cwd().name.startswith(PREFIX)):
            raise shared.RunFailure("explicit owned MySQL fixture startup is required")
        sys.path.insert(0, str(shared.SERVER))
        import remotework
        from remotework.auth import create_user
        from remotework.db import transaction
        from remotework.models import AdminGuard, Base, SchemaVersion
        from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
        from sqlalchemy import delete, inspect, select
        from sqlalchemy.engine import make_url
        from werkzeug.serving import WSGIRequestHandler, make_server
        url = make_url(os.environ["DATABASE_URL"])
        if (url.drivername != "mysql+pymysql" or url.host != "127.0.0.1" or not url.port
                or not re.fullmatch(DATABASE_PATTERN, url.database or "")
                or dict(url.query) != {"charset": "utf8mb4"}):
            raise shared.RunFailure("fixture target was not the newly owned MySQL database")
        remotework.load_dotenv = lambda *_args, **_kwargs: False
        app = remotework.create_app({"DATABASE_URL": url, "SECRET_KEY": first["secret"], "TESTING": False,
                                     "SESSION_HOURS": 1, "COOKIE_SECURE": False, "ENABLE_TASK_QUERY_DRAFT": True})
        app.logger.disabled = True
        engine = app.extensions["db_engine"]
        common, task_tables = _table_sets()
        with engine.connect() as connection:
            validate_instance(connection.exec_driver_sql(IDENTITY_SQL).one(), Path.cwd() / "mysql-data", url.port)
        if inspect(engine).get_table_names():
            raise shared.RunFailure("refusing to initialize a nonempty fixture database")
        Base.metadata.create_all(engine, tables=common)  # Never task ORM create_all/init_schema.
        with app.app_context(), transaction() as db:
            db.add(SchemaVersion(version=1))
            db.add(AdminGuard(id=1))
            users = {name: create_user(db, {**account, "display_name": "Fixture " + name,
                                          "employee_no": "FIX-" + name, "department": "Test fixture"})
                     for name, account in first["accounts"].items()}
            user_ids = {name: user.id for name, user in users.items()}
        with engine.connect() as connection:
            before = _digest_rows(connection, common)
        with engine.begin() as connection:
            for statement in _ddl_statements():
                connection.exec_driver_sql(statement)
        with engine.connect() as connection:
            if _digest_rows(connection, common) != before:
                raise shared.RunFailure("reserved DDL changed common fixture rows")
            if connection.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version").all() != [(1,)]:
                raise shared.RunFailure("fixture schema version must remain one")
        if set(inspect(engine).get_table_names()) != {table.name for table in common + task_tables}:
            raise shared.RunFailure("fixture has unexpected application tables")
        with app.app_context(), transaction() as db:
            def add(task_id, assignee, manager, status, due_day=20):
                db.add(TaskRecord(task_id=task_id, title="Fixture task " + str(task_id),
                                  description="Explicit temporary fixture description " + str(task_id),
                                  task_status=status, start_date=date(2026, 10, 17),
                                  due_date=date(2026, 10, due_day), current_assignment_revision=1,
                                  row_version=1, created_by=user_ids["admin"]))
                db.flush()
                db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=user_ids[assignee],
                                      assigned_by=user_ids["admin"]))
                db.add(TaskManager(task_id=task_id, user_id=user_ids[manager]))
            for offset, status in enumerate(shared.STATUSES):
                add(101 + offset, "staff", "admin", status)
                add(201 + offset, "admin", "other_admin", status)
                add(301 + offset, "other_staff", "admin", status, due_day=19)
            add(int(shared.LARGE_TASK_ID), "staff", "admin", "in_progress")
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
                with engine.connect() as connection:
                    result["database"] = _digest_rows(connection, common + task_tables)
                    result["feature"] = _digest_rows(connection, task_tables)
                    if connection.exec_driver_sql("SELECT version FROM schema_versions ORDER BY version").all() != [(1,)]:
                        raise shared.RunFailure("fixture schema version changed")
            elif action == "remove_manager":
                with app.app_context(), transaction() as db:
                    changed = db.execute(delete(TaskManager).where(TaskManager.task_id == 301,
                                                                   TaskManager.user_id == user_ids["admin"]))
                    if changed.rowcount != 1:
                        raise shared.RunFailure("expected exactly one fixture manager relation")
            elif action == "reassign":
                with app.app_context(), transaction() as db:
                    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == 101))
                    if task.current_assignment_revision != 1:
                        raise shared.RunFailure("fixture task was already reassigned")
                    db.add(TaskAssignment(task_id=101, revision=2, assignee_id=user_ids["other_staff"],
                                          assigned_by=user_ids["admin"]))
                    task.current_assignment_revision = 2
                    task.row_version += 1
            else:
                raise shared.RunFailure("unknown fixture control action")
            print(json.dumps({"id": message_id, "ok": True, **result}), flush=True)
        else:
            raise shared.RunFailure("fixture input closed without shutdown")
        server.shutdown()
        server.server_close()
        thread.join(timeout=10)
        if thread.is_alive():
            raise shared.RunFailure("owned HTTP server thread did not stop")
        engine.dispose()
        app = server = None
        print(json.dumps({"id": message_id, "ok": True}), flush=True)
        return 0
    except BaseException as error:
        print(json.dumps({"id": message_id, "ok": False, "failure_type": type(error).__name__}), flush=True)
        return 1
    finally:
        if server is not None:
            server.shutdown()
            server.server_close()
        if app is not None:
            app.extensions["db_engine"].dispose()


def run(args):
    if not args.allow_disposable_mysql:
        raise shared.RunFailure("explicit --allow-disposable-mysql is required; no server was started")
    if os.name != "nt":
        raise shared.RunFailure("Windows is required for MySQL and the existing WinForms live suite")
    if args.mysql_bin_dir is None:
        raise shared.RunFailure("explicit --mysql-bin-dir is required; no server was started")
    executable = validate_binary_directory(args.mysql_bin_dir)
    dll = args.client_dll.resolve()
    dotnet = shutil.which("dotnet")
    if not dll.is_file() or not dotnet:
        raise shared.RunFailure("build the C# live client and install .NET before running this fixture")
    # Fail before native startup if server dependencies are missing.
    import pymysql  # noqa: F401
    import sqlalchemy  # noqa: F401
    checks = shared.Checks()
    accounts = {name: {"login_id": "fixture_" + name, "password": secrets.token_urlsafe(32), "role": role}
                for name, role in (("staff", "employee"), ("admin", "admin"),
                                   ("other_staff", "employee"), ("other_admin", "admin"), ("empty", "employee"))}
    print("Explicit fixture: new owned MySQL datadir/process/database, synthetic accounts and loopback HTTP. "
          "No existing service, shared DB, .env, SQLite or product write API is used.", flush=True)
    completed = False
    mysql = None
    runtime = None
    try:
        with owned_runtime() as runtime:
            env = shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)  # Never use even the DEMO URL from the legacy environment helper.
            with OwnedMySQL(executable, runtime, env) as mysql:
                if args.exercise_cleanup_failure:
                    raise shared.RunFailure("intentional fixture failure after owned DB creation")
                env["DATABASE_URL"] = mysql.url.render_as_string(hide_password=False)
                with shared.FixtureServer(env, runtime, accounts, server_script=SCRIPT) as fixture:
                    fixture.user_ids = fixture.command("identity_ids")["users"]
                    before = fixture.command("snapshot")["feature"]
                    shared.run_client(dotnet, dll, env, fixture, accounts["staff"], shared.STAFF_OWN, [], checks,
                                      process_runner=run_owned_client)
                    shared.run_client(dotnet, dll, env, fixture, accounts["admin"], shared.ADMIN_OWN,
                                      shared.ADMIN_MANAGED, checks, process_runner=run_owned_client)
                    checks.check(fixture.command("snapshot")["feature"] == before,
                                 "C# reads and local preparation leave task/assignment/manager rows unchanged")
                    shared.verify_http(fixture, accounts, checks)
                checks.check(fixture.process.poll() == 0, "owned HTTP fixture server exited cleanly")
            checks.check(mysql.clean, "owned MySQL database was removed and server stopped")
        checks.check(not runtime.exists(), "owned MySQL datadir/runtime was removed")
        completed = True
    finally:
        if mysql is not None and mysql.clean and runtime is not None and not runtime.exists():
            print("CLEANUP: owned database removed, native server stopped and temporary runtime removed.", flush=True)
    if completed:
        print(f"PASS MySQL runner: {checks.total} checks; {checks.revocation} permission-revocation checks. "
              "Actual C#/TCP HTTP/raw DDL with schema version 1; shared DB/two-PC/write flows remain unverified.",
              flush=True)


def main():
    if sys.argv[1:] == ["--fixture-server"]:
        return fixture_server()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--allow-disposable-mysql", action="store_true")
    parser.add_argument("--mysql-bin-dir", type=Path)
    parser.add_argument("--client-dll", type=Path,
                        default=SCRIPT.parent / "bin/Release/net8.0-windows/TaskQueries.Http.Tests.dll")
    parser.add_argument("--exercise-cleanup-failure", action="store_true",
                        help="Intentionally exit 1 after creating the owned DB, to check failure cleanup")
    args = parser.parse_args()
    try:
        run(args)
        return 0
    except shared.RunFailure as error:
        print("FAIL: " + str(error) + ". Credentials, DB rows and child output are suppressed.", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("FAIL: interrupted; owned-resource cleanup was attempted.", file=sys.stderr)
        return 130
    except Exception as error:
        print("FAIL: " + type(error).__name__ + "; credentials, DB rows and child output are suppressed.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
