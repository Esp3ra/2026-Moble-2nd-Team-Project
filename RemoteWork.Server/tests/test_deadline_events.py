"""Explicit journal proposal in an owned MySQL; no production installation."""
from datetime import date, timedelta
from pathlib import Path
from uuid import UUID
from concurrent.futures import ThreadPoolExecutor

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session

from remotework import deadline_events as port
from remotework.common import AppError
from remotework.features.tasks.models import TaskRecord
from test_task_deadline_facts import deadline_app, deadline, guarded, add_task


@pytest.fixture(scope="module")
def journal_app(deadline_app):
    ddl = Path(__file__).resolve().parents[1] / "migrations/drafts/deadline_events.mysql.sql"
    script = "\n".join(s for s in ddl.read_text().splitlines() if not s.lstrip().startswith("--"))
    with deadline_app.extensions["db_engine"].begin() as db:
        for statement in script.split(";"):
            if statement.strip():
                db.exec_driver_sql(statement)
    return deadline_app


@pytest.fixture
def env(journal_app, deadline):
    assert journal_app is deadline.app
    yield deadline
    with journal_app.extensions["db_engine"].begin() as db:
        db.execute(port.heads.delete())
        db.execute(port.journal.delete())


def task(db, env):
    return db.scalar(select(TaskRecord).where(TaskRecord.task_id == env.task_id)
        .with_for_update().execution_options(populate_existing=True))


def create(env):
    env.task_id = add_task(env, status="not_started")
    with guarded(env) as db:
        row = task(db, env)
        row.row_version = 1
        db.flush()
        return port.record_change(db, task_id=row.task_id, source_row_version=1,
            source_action="task.created", before_due_date=None, due_date=row.due_date)


def change(env, db, *, action="task.updated", due=None):
    row = task(db, env)
    before = row.due_date
    row.due_date = due or before + timedelta(days=1)
    row.row_version += 1
    db.flush()
    return port.record_change(db, task_id=row.task_id, source_row_version=row.row_version,
        source_action=action, before_due_date=before, due_date=row.due_date)


def rows(env):
    with env.app.extensions["db_engine"].connect() as db:
        return list(db.execute(select(port.journal))), list(db.execute(select(port.heads)))


def test_create_change_back_are_distinct_ordered_original_records(env):
    first = create(env)
    with guarded(env) as db:
        second = change(env, db)
        third = change(env, db, action="task.deadline_extension_approved", due=first.due_date)
        assert port.read_current(db, task_id=env.task_id) == third
        assert port.read_event(db, event_id=first.event_id) == first
        assert port.read_event(db, event_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa') is None
    assert len({first.event_id, second.event_id, third.event_id}) == 3
    assert [v.event_order for v in (first, second, third)] == [1, 2, 3]
    assert all(str(UUID(v.event_id)) == v.event_id for v in (first, second, third))
    assert third.due_date == first.due_date and second.before_due_date == first.due_date
    assert first.actor_id == env.ids['staff1']  # Synthetic internal-provider caller, not writer authorization.
    assert len(rows(env)[0]) == 3


def test_same_version_same_input_is_idempotent(env):
    first = create(env)
    with guarded(env) as db:
        repeated = port.record_change(db, task_id=env.task_id, source_row_version=1,
            source_action="task.created", before_due_date=None, due_date=first.due_date)
    assert repeated == first and len(rows(env)[0]) == 1


def test_same_version_different_original_is_rejected(env):
    first = create(env)
    with pytest.raises(AppError) as caught, guarded(env) as db:
        port.record_change(db, task_id=env.task_id, source_row_version=1,
            source_action="task.updated", before_due_date=first.due_date-timedelta(days=1), due_date=first.due_date)
    assert caught.value.code == "DEADLINE_EVENT_SOURCE_MISMATCH"
    assert len(rows(env)[0]) == 1


def test_no_date_change_does_not_create_event(env):
    first = create(env)
    with guarded(env) as db:
        assert change(env, db, due=first.due_date) is None
        assert port.read_current(db, task_id=env.task_id) == first
    assert len(rows(env)[0]) == 1


def test_unknown_existing_task_is_not_baselined(env):
    env.task_id = add_task(env)
    with pytest.raises(AppError) as caught, guarded(env) as db:
        change(env, db)
    assert caught.value.code == "DEADLINE_EVENT_UNKNOWN"
    assert rows(env) == ([], [])


def test_later_failure_rolls_back_task_event_and_current_head(env):
    first = create(env)
    with pytest.raises(RuntimeError), guarded(env) as db:
        change(env, db)
        raise RuntimeError("simulated downstream audit/receipt failure")
    with guarded(env) as db:
        assert task(db, env).due_date == first.due_date
        assert task(db, env).row_version == 1
        assert port.read_current(db, task_id=env.task_id) == first
    assert len(rows(env)[0]) == 1


def test_storage_loss_rolls_back_original_mutation(env):
    first = create(env)
    engine = env.app.extensions["db_engine"]
    with engine.begin() as db:
        db.exec_driver_sql("RENAME TABLE deadline_event_heads_draft TO deadline_event_heads_missing")
    try:
        with pytest.raises(AppError) as caught, guarded(env) as db:
            change(env, db)
        assert caught.value.code == "DEADLINE_EVENT_STORAGE_REQUIRED"
    finally:
        with engine.begin() as db:
            db.exec_driver_sql("RENAME TABLE deadline_event_heads_missing TO deadline_event_heads_draft")
    with guarded(env) as db:
        assert task(db, env).row_version == 1
        assert port.read_current(db, task_id=env.task_id) == first


def test_unrecorded_current_date_disagreement_is_unknown(env):
    create(env)
    with guarded(env) as db:
        task(db, env).due_date += timedelta(days=1)
        db.flush()
    with pytest.raises(AppError) as caught, guarded(env) as db:
        port.read_current(db, task_id=env.task_id)
    assert caught.value.code == "DEADLINE_EVENT_SOURCE_MISMATCH"


@pytest.mark.parametrize('action', ['task.reassigned','task.deadline_extension_requested','task.deadline_extension_rejected'])
def test_non_due_actions_are_not_accepted(env, action):
    first = create(env)
    with pytest.raises(ValueError), guarded(env) as db:
        port.record_change(db, task_id=env.task_id, source_row_version=1,
            source_action=action, before_due_date=first.due_date, due_date=first.due_date)
    assert len(rows(env)[0]) == 1


def test_different_session_or_unprotected_transaction_is_refused(env):
    create(env)
    with guarded(env), Session(env.app.extensions['db_engine']) as other:
        with pytest.raises(RuntimeError):
            port.read_current(other, task_id=env.task_id)
    with env.app.test_request_context('/'), Session(env.app.extensions['db_engine']) as other:
        with pytest.raises(RuntimeError):
            port.read_current(other, task_id=env.task_id)


def test_two_guarded_mutations_keep_distinct_ordered_current_events(env):
    create(env)
    def mutate(_):
        with guarded(env) as db:
            return change(env, db)
    with ThreadPoolExecutor(max_workers=2) as workers:
        values = list(workers.map(mutate, range(2)))
    assert sorted(v.event_order for v in values) == [2,3]
    assert len({v.event_id for v in values}) == 2
    with guarded(env) as db:
        assert port.read_current(db, task_id=env.task_id).event_order == 3
