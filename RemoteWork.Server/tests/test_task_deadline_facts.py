"""Current own-deadline read port against an explicitly disposable MySQL.

The only HTTP endpoint below belongs to this test module. It is not a product
route, alert adapter, event source or confirmation implementation. The fixture
applies the original 002/004/006/009 SQL only to MYSQL_TEST_URL's dedicated DB.
"""
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
from dataclasses import FrozenInstanceError, asdict, fields
from datetime import date, datetime, timedelta
import os
from pathlib import Path
import re
from threading import Event
from types import SimpleNamespace

import pytest
from flask import g, request
from sqlalchemy import delete, select
from sqlalchemy.engine import make_url

from remotework import create_app
from remotework.auth import authenticated_transaction, create_user, login_required
from remotework.common import AppError, success
from remotework.db import get_db, transaction
from remotework.features import results
from remotework.features.tasks import deadline_facts as port, lifecycle
from remotework.features.tasks.lifecycle_models import TaskCancellation
from remotework.features.tasks.models import TASK_STATUSES, TaskAssignment, TaskManager, TaskRecord
from remotework.models import AdminGuard, AuthSession, Base, SchemaVersion, User, utcnow
from remotework.write_contract import BIGINT_MAX, lock_users
from remotework.write_requests import WriteReceipt


PASSWORD = "Deadline-facts-test-2026!"
NOW = datetime(2026, 10, 22, 1, 2, 3)
DUE = date(2026, 10, 23)
MIGRATIONS = ("002_task_queries.mysql.sql", "004_write_receipts.mysql.sql",
              "006_results.mysql.sql", "009_task_lifecycle.mysql.sql")
TEST_PATH = "/api/v1/_deadline_facts_test/read"


@pytest.fixture(scope="module")
def deadline_app():
    url = os.getenv("MYSQL_TEST_URL", "")
    if not url.startswith("mysql"):
        pytest.skip("Deadline facts require an explicitly owned disposable MySQL database")
    if not (make_url(url).database or "").startswith("remote_work_test_"):
        raise RuntimeError("Refusing a database outside the disposable test namespace")
    app = create_app(dict(TESTING=True, DATABASE_URL=url,
        SECRET_KEY="deadline-facts-test-only-" + "a" * 32, COOKIE_SECURE=False,
        ENABLE_TASK_QUERY_DRAFT=False, ENABLE_COMMON_WRITE_DRAFT=False,
        ENABLE_TASK_CREATE_DRAFT=False, ENABLE_TASK_START_DRAFT=False,
        ENABLE_TASK_MANAGEMENT_DRAFT=False, ENABLE_TASK_LIFECYCLE_DRAFT=False,
        ENABLE_RESULTS_DRAFT=False))
    engine = app.extensions["db_engine"]
    assert engine.dialect.name == "mysql"
    migration_root = Path(__file__).resolve().parents[1] / "migrations"
    scripts = ["\n".join(line for line in (migration_root / name).read_text(encoding="utf-8").splitlines()
                         if not line.lstrip().startswith("--")) for name in MIGRATIONS]
    owner_tables = {name for script in scripts for name in re.findall(r"CREATE TABLE\s+(\w+)", script)}
    Base.metadata.create_all(engine,
        tables=[table for table in Base.metadata.sorted_tables if table.name not in owner_tables])
    with engine.begin() as db:
        for script in scripts:
            for statement in script.split(";"):
                if statement.strip():
                    db.exec_driver_sql(statement)
    with app.app_context(), transaction() as db:
        db.add_all([AdminGuard(id=1), SchemaVersion(version=1)])

    @app.post(TEST_PATH)
    @login_required
    def read_for_test():
        env = app.extensions["deadline_test"]
        if env.before_guard is not None:
            env.before_guard(get_db())
        body = request.get_json()
        with authenticated_transaction() as db:
            if body.get("single"):
                facts = (port.read_own_deadline_fact(db, body["task_id"]),)
            else:
                facts = port.read_own_deadline_facts(db, max_tasks=body.get("max_tasks", 1000))
            env.observed = facts
            # Explicit test serializer; the internal port does not own HTTP DTOs.
            data = [{**asdict(fact), "due_date": fact.due_date.isoformat()} for fact in facts]
            return success(data)

    try:
        yield app
    finally:
        engine.dispose()


@pytest.fixture
def deadline(deadline_app):
    app = deadline_app
    with app.app_context(), transaction() as db:
        for table in reversed(Base.metadata.sorted_tables):
            if table.name not in {"admin_guard", "schema_versions"}:
                db.execute(delete(table))
        ids = {}
        for name, role in (("admin1", "admin"), ("staff1", "employee"),
                           ("staff2", "employee"), ("admin2", "admin")):
            user = create_user(db, dict(login_id=name, password=PASSWORD, display_name="TEST-" + name,
                employee_no="DEADLINE-" + name, department="TEST", role=role))
            ids[name] = user.id
    env = SimpleNamespace(app=app, ids=ids, tokens={}, before_guard=None, observed=None)
    app.extensions["deadline_test"] = env
    return env


def headers(env, name="staff1"):
    if name not in env.tokens:
        response = env.app.test_client().post("/api/v1/auth/login",
            json={"login_id": name, "password": PASSWORD})
        assert response.status_code == 200, response.json
        env.tokens[name] = response.json["data"]["access_token"]
    return {"Authorization": "Bearer " + env.tokens[name]}


@contextmanager
def identity(env, name="staff1"):
    auth = headers(env, name)
    with env.app.test_request_context(TEST_PATH, headers=auth):
        assert env.app.preprocess_request() is None
        yield get_db()


@contextmanager
def guarded(env, name="staff1"):
    with identity(env, name):
        with authenticated_transaction() as db:
            yield db


def add_task(env, task_id=100, *, owner="staff1", status="in_progress", cancelled=False,
             revision=1, due=DUE, assignment=True):
    with env.app.app_context(), transaction() as db:
        task = TaskRecord(task_id=task_id, title="TEST task " + str(task_id), description="preserved",
            task_status=status, start_date=date(2026, 1, 1), due_date=due,
            current_assignment_revision=revision, row_version=7, created_by=env.ids["admin1"],
            created_at=NOW, updated_at=NOW)
        db.add(task)
        db.flush()
        if assignment:
            db.add(TaskAssignment(task_id=task_id, revision=revision, assignee_id=env.ids[owner],
                assigned_by=env.ids["admin1"], assigned_at=NOW, accepted_at=NOW))
        db.add(TaskManager(task_id=task_id, user_id=env.ids["admin1"]))
        if cancelled:
            db.add(TaskCancellation(task_id=task_id, cancelled_by=env.ids["admin1"],
                reason="TEST retained cancellation", cancelled_at=NOW))
    return task_id


def persisted(env):
    # Includes domain rows, auth, audit, receipts, grants and schema evidence.
    with env.app.extensions["db_engine"].connect() as db:
        return tuple((table.name, tuple(tuple(row) for row in db.execute(
            select(table).order_by(*table.primary_key.columns))))
            for table in Base.metadata.sorted_tables)


@contextmanager
def app_error(status, code):
    with pytest.raises(AppError) as caught:
        yield caught
    assert (caught.value.status, caught.value.code) == (status, code)


def test_all_five_states_and_persisted_cancellations_are_returned_without_date_filter(deadline):
    expected = []
    for index, state in enumerate(TASK_STATUSES):
        for cancelled in (False, True):
            task_id = 100 + index * 2 + int(cancelled)
            due = date(2026, 1, 2) if index % 2 else date(2030, 1, 2)
            add_task(deadline, task_id, status=state, cancelled=cancelled, due=due)
            expected.append((task_id, state, cancelled, due))
    add_task(deadline, 1, owner="staff2")
    with guarded(deadline) as db:
        facts = port.read_own_deadline_facts(db)
        assert isinstance(facts, tuple)
        assert [(f.task_id, f.task_status, f.is_cancelled, f.due_date) for f in facts] == expected
        assert all(f.assignee_id == deadline.ids["staff1"] for f in facts)


def test_admin_is_own_only_even_for_explicitly_managed_other_tasks(deadline):
    add_task(deadline, 100)
    add_task(deadline, 200, owner="admin1", status="approved", cancelled=True)
    with guarded(deadline, "admin1") as db:
        assert [f.task_id for f in port.read_own_deadline_facts(db)] == [200]
        with app_error(403, "FORBIDDEN"):
            port.read_own_deadline_fact(db, "100")
    with guarded(deadline, "admin2") as db:
        assert port.read_own_deadline_facts(db) == ()


def test_read_has_no_domain_audit_receipt_or_schema_effects(deadline):
    add_task(deadline, cancelled=True)
    headers(deadline)
    before = persisted(deadline)
    with guarded(deadline) as db:
        assert port.read_own_deadline_facts(db)[0] == port.read_own_deadline_fact(db, 100)
        assert not db.new and not db.dirty and not db.deleted
    assert persisted(deadline) == before


def test_facts_are_immutable_native_copies_without_invented_event_or_confirmation(deadline):
    add_task(deadline)
    with guarded(deadline) as db:
        fact = port.read_own_deadline_fact(db, "100")
        assert {field.name for field in fields(fact)} == {"task_id", "title", "assignee_id",
            "assignment_revision", "row_version", "task_status", "due_date", "is_cancelled"}
        assert type(fact.task_id) is int and type(fact.due_date) is date and type(fact.is_cancelled) is bool
        with pytest.raises(FrozenInstanceError):
            fact.title = "changed"
    with guarded(deadline, "admin1") as db:
        task, *_ = lifecycle._current(db, "100", mode="manage")
        task.title = "TEST later title"
        task.due_date = DUE + timedelta(days=1)
    assert fact.title == "TEST task 100" and fact.due_date == DUE


@pytest.mark.parametrize("target", [9_007_199_254_740_993, "9007199254740993"])
def test_single_accepts_native_int_and_canonical_bigint_without_precision_loss(deadline, target):
    add_task(deadline, 9_007_199_254_740_993)
    with guarded(deadline) as db:
        assert port.read_own_deadline_fact(db, target).task_id == 9_007_199_254_740_993


@pytest.mark.parametrize("target", [True, 0, -1, 1.0, None, "01", "+1", " 1", "1.0", str(BIGINT_MAX + 1)])
def test_single_rejects_noncanonical_or_out_of_range_identifiers(deadline, target):
    with guarded(deadline) as db:
        with app_error(400, "INVALID_INPUT"):
            port.read_own_deadline_fact(db, target)


@pytest.mark.parametrize("target,status,code", [(100, 403, "FORBIDDEN"), (999, 404, "TASK_NOT_FOUND")])
def test_single_distinguishes_current_nonownership_and_absence(deadline, target, status, code):
    add_task(deadline, owner="staff2")
    with guarded(deadline) as db:
        with app_error(status, code):
            port.read_own_deadline_fact(db, target)


def test_complete_bound_returns_empty_and_exact_limit_but_never_partial_overflow(deadline):
    with guarded(deadline) as db:
        assert port.read_own_deadline_facts(db, max_tasks=2) == ()
    add_task(deadline, 102)
    add_task(deadline, 101)
    add_task(deadline, 1, owner="staff2")
    with guarded(deadline) as db:
        assert [f.task_id for f in port.read_own_deadline_facts(db, max_tasks=2)] == [101, 102]
    add_task(deadline, 103)
    with guarded(deadline) as db:
        with app_error(503, "TASK_DEADLINE_FACTS_LIMIT"):
            port.read_own_deadline_facts(db, max_tasks=2)
        assert [f.task_id for f in port.read_own_deadline_facts(db, max_tasks=1000)] == [101, 102, 103]


@pytest.mark.parametrize("bound", [0, -1, 1001, True, False, 1.0, "2", None])
def test_complete_bound_requires_supported_positive_native_integer(deadline, bound):
    with guarded(deadline) as db:
        with pytest.raises(ValueError):
            port.read_own_deadline_facts(db, max_tasks=bound)


@pytest.mark.parametrize("table", ["task_cancellations", "task_deadline_extensions"])
@pytest.mark.parametrize("has_owned_task", [False, True])
def test_missing_009_storage_is_503_even_with_empty_set_and_flags_off(deadline, table, has_owned_task):
    if has_owned_task:
        add_task(deadline)
    engine = deadline.app.extensions["db_engine"]
    hidden = "TEST_hidden_" + table
    with engine.begin() as db:
        db.exec_driver_sql(f"RENAME TABLE {table} TO {hidden}")
    try:
        with guarded(deadline) as db:
            with app_error(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED"):
                port.read_own_deadline_facts(db)
            with app_error(503, "TASK_LIFECYCLE_SCHEMA_REQUIRED"):
                port.read_own_deadline_fact(db, 100)
        with engine.connect() as db:
            assert db.exec_driver_sql("SHOW TABLES LIKE '" + table + "'").first() is None
    finally:
        with engine.begin() as db:
            db.exec_driver_sql(f"RENAME TABLE {hidden} TO {table}")


@pytest.mark.parametrize("old_assignment", [False, True])
def test_any_missing_current_assignment_fails_whole_set_instead_of_guessing_owner(deadline, old_assignment):
    add_task(deadline, 100)
    add_task(deadline, 200, owner="staff2", assignment=old_assignment)
    if old_assignment:
        with guarded(deadline, "admin1") as db:
            task, *_ = lifecycle._current(db, "200", mode="manage")
            task.current_assignment_revision = 2
    with guarded(deadline) as db:
        with app_error(503, "TASK_DEADLINE_FACTS_INCOMPLETE"):
            port.read_own_deadline_facts(db)


@pytest.mark.parametrize("mode", ["no_request", "request_only", "ordinary_transaction", "foreign_session", "finished_guard"])
def test_requires_exact_current_authenticated_transaction_session(deadline, mode):
    if mode == "no_request":
        with pytest.raises(RuntimeError):
            port.read_own_deadline_facts(None)
        return
    with identity(deadline) as db:
        if mode == "request_only":
            with pytest.raises(RuntimeError):
                port.read_own_deadline_facts(db)
        elif mode == "ordinary_transaction":
            with transaction() as ordinary:
                with pytest.raises(RuntimeError):
                    port.read_own_deadline_facts(ordinary)
        elif mode == "foreign_session":
            with authenticated_transaction():
                with deadline.app.extensions["db_factory"]() as other:
                    with pytest.raises(RuntimeError):
                        port.read_own_deadline_facts(other)
        else:
            with authenticated_transaction() as completed:
                pass
            with pytest.raises(RuntimeError):
                port.read_own_deadline_facts(completed)


@pytest.mark.parametrize("reader", [port.read_own_deadline_fact, port.read_own_deadline_facts])
def test_caller_cannot_supply_another_actor(deadline, reader):
    add_task(deadline, owner="staff2")
    with guarded(deadline) as db:
        args = (db, "100") if reader is port.read_own_deadline_fact else (db,)
        with pytest.raises(TypeError):
            reader(*args, actor_id=deadline.ids["staff2"])


def delayed_read(env, before, write, *, single=False):
    ready, resume = Event(), Event()
    sender = headers(env)
    headers(env, "admin1")  # Do not introduce a login inside the paused reader.
    def gate(db):
        before(db, "before")
        ready.set()
        if not resume.wait(10):
            raise RuntimeError("Timed out waiting for the owned competing writer")
        before(db, "after")
    env.before_guard = gate
    try:
        with ThreadPoolExecutor(max_workers=1) as pool:
            future = pool.submit(lambda: env.app.test_client().post(TEST_PATH, headers=sender,
                json={"single": single, "task_id": "100"}))
            try:
                assert ready.wait(10), "authenticated old snapshot must exist before the competing commit"
                write()
            finally:
                resume.set()
            return future.result(timeout=15)
    finally:
        resume.set()
        env.before_guard = None


@pytest.mark.parametrize("single", [False, True], ids=["whole-set", "one-task"])
@pytest.mark.parametrize("change", ["facts-and-cancel", "assignment-out", "assignment-in"])
def test_old_rr_snapshot_and_orm_cache_do_not_override_current_committed_facts(deadline, single, change):
    add_task(deadline, owner="staff2" if change == "assignment-in" else "staff1")
    snapshots, cached = {}, {}
    def before(db, phase):
        if phase == "before":
            cached["task"] = db.get(TaskRecord, 100)
            cached["assignment"] = db.get(TaskAssignment, (100, 1))
        snapshots[phase] = (db.scalar(select(TaskRecord.due_date).where(TaskRecord.task_id == 100)),
            db.scalar(select(TaskAssignment.assignee_id).where(TaskAssignment.task_id == 100, TaskAssignment.revision == 1)),
            db.scalar(select(TaskCancellation.task_id).where(TaskCancellation.task_id == 100)))
        assert cached["task"].row_version == 7 and cached["assignment"].revision == 1
    def write():
        with guarded(deadline, "admin1") as db:
            task, _assignment, *_ = lifecycle._current(db, "100", mode="manage")
            lock_users(db, list(deadline.ids.values()))
            task.row_version = 8
            if change == "facts-and-cancel":
                task.due_date = DUE + timedelta(days=3)
                task.title = "TEST current title"
                task.task_status = "returned"
                db.add(TaskCancellation(task_id=100, cancelled_by=deadline.ids["admin1"],
                    reason="TEST concurrent cancellation", cancelled_at=NOW))
            else:
                task.current_assignment_revision = 2
                db.add(TaskAssignment(task_id=100, revision=2,
                    assignee_id=deadline.ids["staff1" if change == "assignment-in" else "staff2"],
                    assigned_by=deadline.ids["admin1"], assigned_at=NOW, accepted_at=NOW))
    response = delayed_read(deadline, before, write, single=single)
    assert snapshots["before"] == snapshots["after"], "plain reads must actually retain the older RR snapshot"
    if change == "assignment-out":
        if single:
            assert response.status_code == 403 and response.json["error"]["code"] == "FORBIDDEN"
        else:
            assert response.status_code == 200 and response.json["data"] == []
    else:
        assert response.status_code == 200, response.json
        fact = response.json["data"][0]
        assert len(response.json["data"]) == 1 and fact["task_id"] == 100 and fact["row_version"] == 8
        if change == "facts-and-cancel":
            assert fact["due_date"] == (DUE + timedelta(days=3)).isoformat()
            assert fact["title"] == "TEST current title" and fact["task_status"] == "returned" and fact["is_cancelled"] is True
        else:
            assert fact["assignment_revision"] == 2 and fact["assignee_id"] == deadline.ids["staff1"]


@pytest.mark.parametrize("change", ["actor-inactive", "session-deleted", "session-expired", "session-other-owner"])
def test_common_guard_rechecks_actor_and_session_after_authenticated_snapshot(deadline, change):
    add_task(deadline)
    cached = {}
    def before(db, phase):
        if phase == "before":
            cached.update(token=g.auth_session.token_hash, user=g.user, session=g.auth_session)
        assert cached["user"].is_active and cached["session"].user_id == deadline.ids["staff1"]
    def write():
        with guarded(deadline, "admin1") as db:
            lock_users(db, list(deadline.ids.values()))
            if change == "actor-inactive":
                db.get(User, deadline.ids["staff1"]).is_active = False
            else:
                session = db.scalar(select(AuthSession).where(AuthSession.token_hash == cached["token"]).with_for_update())
                if change == "session-deleted":
                    db.delete(session)
                elif change == "session-expired":
                    session.expires_at = utcnow() - timedelta(seconds=1)
                else:
                    session.user_id = deadline.ids["staff2"]
    response = delayed_read(deadline, before, write)
    assert response.status_code == 401 and response.json["error"]["code"] == "AUTH_REQUIRED"
    assert deadline.observed is None


def test_incomplete_current_title_is_not_a_successful_empty_or_partial_fact(deadline):
    add_task(deadline)
    with guarded(deadline, "admin1") as db:
        task, *_ = lifecycle._current(db, "100", mode="manage")
        task.title = " \t "
    with guarded(deadline) as db:
        with app_error(503, "TASK_DEADLINE_FACTS_INCOMPLETE"):
            port.read_own_deadline_facts(db)


def test_read_port_does_not_install_a_product_deadline_route(deadline):
    routes = {rule.rule for rule in deadline.app.url_map.iter_rules()}
    assert not any("deadline-facts" in route or "deadline_facts" in route for route in routes if route != TEST_PATH)
    assert "task_lifecycle_integration_ack" not in deadline.app.extensions
