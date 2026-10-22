"""Explicit journal proposal in an owned MySQL; no production installation."""
from datetime import date, timedelta
from pathlib import Path
from uuid import UUID
from concurrent.futures import ThreadPoolExecutor

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session

from remotework import deadline_events as port
from remotework import deadline_bindings as binding
from remotework.common import AppError
from remotework.features.tasks.models import TaskRecord
from remotework.features.tasks.models import TaskAssignment
from remotework.write_requests import register_write_action, register_write_result_reader, issue_key, lookup_result
from test_task_deadline_facts import deadline_app, deadline, guarded, add_task, identity
from remotework.auth import authenticated_transaction
from remotework.write_requests import _authorize, _authorizer


@pytest.fixture(scope="module")
def journal_app(deadline_app):
    ddl = Path(__file__).resolve().parents[1] / "migrations/drafts/deadline_events.mysql.sql"
    script = "\n".join(s for s in ddl.read_text().splitlines() if not s.lstrip().startswith("--"))
    with deadline_app.extensions["db_engine"].begin() as db:
        for statement in script.split(";"):
            if statement.strip():
                db.exec_driver_sql(statement)
    # Explicit TESTING-only registration; product startup installs nothing.
    deadline_app.config['ENABLE_COMMON_WRITE_DRAFT'] = True
    register_write_action(deadline_app, action='deadline_alert.confirmed', authorize=binding.authorize_result,
        result_fields={'event_id','task_id','assignment_revision','confirmed_at'})
    register_write_result_reader(deadline_app, action='deadline_alert.confirmed', authorize=binding.authorize_result)
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


def baseline(env, db, **changes):
    row = task(db, env)
    values = dict(task_id=row.task_id, expected_row_version=row.row_version,
        expected_assignment_revision=row.current_assignment_revision,
        expected_assignee_id=env.ids['staff1'], expected_due_date=row.due_date,
        run_id='aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa', reason='Explicit introduction baseline')
    values.update(changes)
    return port.establish_baseline(db, **values)


def test_baseline_exact_context_then_real_change_and_replay(env):
    env.task_id = add_task(env)
    with guarded(env, 'admin1') as db:
        first = baseline(env, db)
        again = baseline(env, db)
        assert first == again
        assert first.source_action == 'task.deadline_baseline'
        assert first.before_due_date is None and first.source_row_version == 7
        second = change(env, db)
        assert second.event_order == 2 and second.before_due_date == first.due_date
        assert baseline(env, db) == second
    assert len(rows(env)[0]) == 2


@pytest.mark.parametrize('changes', [dict(expected_row_version=8),
    dict(expected_assignment_revision=2), dict(expected_assignee_id=2147483647),
    dict(expected_due_date=date(2030, 1, 1))])
def test_baseline_changed_manifest_rejected_without_writes(env, changes):
    env.task_id = add_task(env)
    with pytest.raises(AppError) as caught:
        with guarded(env, 'admin1') as db:
            baseline(env, db, **changes)
    assert caught.value.code == 'DEADLINE_BASELINE_CHANGED'
    assert rows(env) == ([], [])


def test_baseline_employee_rejected_and_admin_failure_rolls_back(env):
    env.task_id = add_task(env)
    with pytest.raises(AppError) as caught:
        with guarded(env) as db:
            baseline(env, db)
    assert caught.value.status == 403
    with pytest.raises(RuntimeError):
        with guarded(env, 'admin1') as db:
            baseline(env, db)
            raise RuntimeError('downstream manifest failure')
    assert rows(env) == ([], [])


def test_baseline_orphan_is_not_repaired(env):
    env.task_id = add_task(env)
    with guarded(env, 'admin1') as db:
        baseline(env, db)
        db.execute(port.heads.delete())
    with pytest.raises(AppError) as caught:
        with guarded(env, 'admin1') as db:
            baseline(env, db)
    assert caught.value.code == 'DEADLINE_EVENT_SOURCE_MISMATCH'
    assert len(rows(env)[0]) == 1 and rows(env)[1] == []


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


def test_original_binding_reads_old_unconfirmed_event_without_confirmation_rows(env):
    first = create(env)
    with guarded(env) as db:
        original = binding.current_binding(db, task_id=env.task_id)
        assert original.event_id == first.event_id
        change(env, db)
        target = binding.target(original)
        assert binding.authorize_result(db, target) is True
        assert binding.read_bound_event(db, target) == (original, first)
        assert binding.parse_target(target) == original


def test_old_assignment_cannot_be_used_by_another_original_actor(env):
    first = create(env)
    value = binding.target(binding.DeadlineBinding(env.task_id, first.event_id, 2))
    with pytest.raises(AppError) as caught, guarded(env) as db:
        binding.authorize_result(db, value)
    assert caught.value.status == 403


def test_retained_binding_requires_current_own_before_loading_event(env, monkeypatch):
    first = create(env)
    calls=[]
    monkeypatch.setattr(binding, 'read_event', lambda *a,**kw:calls.append(1))
    with pytest.raises(AppError) as caught, guarded(env,'staff2') as db:
        binding.authorize_result(db, binding.target(binding.DeadlineBinding(env.task_id,first.event_id,1)))
    assert caught.value.status==403 and calls==[]


def test_same_actor_reassigned_again_gets_separate_binding_without_new_event(env):
    first = create(env)
    original = binding.DeadlineBinding(env.task_id,first.event_id,1)
    with guarded(env) as db:
        previous = db.get(TaskAssignment,(env.task_id,1))
        for revision, actor in [(2,'staff2'),(3,'staff1')]:
            db.add(TaskAssignment(task_id=env.task_id, revision=revision, assignee_id=env.ids[actor],
                assigned_by=env.ids['admin1'], assigned_at=previous.assigned_at))
        db.flush()
        current = task(db,env)
        current.current_assignment_revision=3
        current.row_version=3
        db.flush()
        new = binding.current_binding(db,task_id=env.task_id)
        assert new.event_id==first.event_id and new.assignment_revision==3
        assert binding.target(original)!=binding.target(new)
        assert binding.authorize_result(db,binding.target(original)) is True
        with pytest.raises(AppError) as caught:
            binding.authorize_result(db,binding.target(binding.DeadlineBinding(env.task_id,first.event_id,2)))
        assert caught.value.status==403


def test_actual_w1_original_target_resolves_not_recorded_after_event_changes(env):
    first=create(env)
    with guarded(env) as db:
        original=binding.target(binding.current_binding(db,task_id=env.task_id))
        key=issue_key(action='deadline_alert.confirmed',target=original)['request_key']
        change(env,db)
        answer=lookup_result(db,key=key,action='deadline_alert.confirmed',target=original,
            authorize=binding.authorize_result)
        assert answer=={'state':'not_recorded'}
        assert binding.read_bound_event(db,original)[1].event_id==first.event_id


@pytest.mark.parametrize('value', ['d1:01:aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa:1',
    'd1:1:AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA:1',
    'd1:1:aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa:2147483648'])
def test_binding_target_is_canonical_and_signed_integer_bounded(value):
    with pytest.raises(AppError) as caught:
        binding.parse_target(value)
    assert caught.value.status==400


def test_binding_max_signed_ids_fit_the_existing_w1_target_without_ddl_change():
    value=binding.target(binding.DeadlineBinding(9223372036854775807,
        'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',2147483647))
    assert len(value)<=80 and binding.target(binding.parse_target(value))==value
    with pytest.raises(AppError):
        binding.parse_target('d1:'+('9'*5000)+':aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa:1')


def test_authentication_rr_snapshot_does_not_hide_new_retained_event_or_key_authorization(env):
    first=create(env)
    # load_identity already opened the RR transaction. Force a consistent view.
    with identity(env) as db:
        assert db.scalar(select(port.journal.c.event_id).where(port.journal.c.event_id==first.event_id))==first.event_id
        with ThreadPoolExecutor(1) as workers:
            def writer():
                with guarded(env) as other:
                    return change(env,other)
            new=workers.submit(writer).result(timeout=10)
        # An ordinary RR read still misses the other transaction's new UUID.
        assert db.scalar(select(port.journal.c.event_id).where(port.journal.c.event_id==new.event_id)) is None
        with authenticated_transaction() as same:
            assert same is db
            assert port.read_current(db,task_id=env.task_id)==new
            assert port.read_event(db,event_id=new.event_id)==new
            assert port.read_event(db,event_id=new.event_id,task_id=env.task_id)==new
            original=binding.target(binding.current_binding(db,task_id=env.task_id))
            assert binding.read_bound_event(db,original)[1]==new
            _authorize(db,original,_authorizer('deadline_alert.confirmed'))
            assert issue_key(action='deadline_alert.confirmed',target=original)['request_key']
