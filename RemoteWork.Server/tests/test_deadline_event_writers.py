"""Test-only adapter at owner2's reviewed mutation boundary, not a live hook."""
from datetime import date, timedelta

import pytest
from sqlalchemy import select

from remotework import deadline_events as port
from remotework.common import AppError
from remotework.db import get_db
from remotework.features.tasks import creation, management, lifecycle
from remotework.features.tasks.models import TaskRecord
from test_task_deadline_facts import deadline_app, deadline, guarded, persisted
from test_task_deadline_write_boundaries import (boundary_app, writes, create_task, edit_body,
    invoke, key, start_task, request_extension, version_body)
from test_deadline_events import journal_app


@pytest.fixture
def linked(boundary_app, journal_app, writes, monkeypatch):
    env = writes
    for module in (creation, management, lifecycle):
        previous = module.audit
        def hook(action, resource, target, details=None, actor_id=None, previous=previous):
            if action in {'task.created','task.updated','task.deadline_extension_approved'}:
                db = get_db()
                row = db.get(TaskRecord, int(target))
                details = details or {}
                before = (None if action == 'task.created' else
                    date.fromisoformat(details['before_due_date']) if action == 'task.updated' else
                    date.fromisoformat(details['before']['due_date']))
                port.record_change(db, task_id=row.task_id, source_row_version=row.row_version,
                    source_action=action, before_due_date=before, due_date=row.due_date)
            return previous(action, resource, target, details, actor_id)
        monkeypatch.setattr(module, 'audit', hook)
    yield env
    with env.app.extensions['db_engine'].begin() as db:
        db.execute(port.heads.delete())
        db.execute(port.journal.delete())


def all_events(env):
    with env.app.extensions['db_engine'].connect() as db:
        return list(db.execute(select(port.journal).order_by(port.journal.c.event_order)))


def test_actual_create_update_replay_and_back_link_once(linked):
    env = linked
    target = create_task(env)
    original = env.body['due_date']
    changed = (date.fromisoformat(original) + timedelta(days=1)).isoformat()
    body = edit_body(env, target, due_date=changed)
    request_key = key(env, 'task.updated', target)
    first = invoke(env, 'task.updated', target, body, request_key)
    before = all_events(env)
    assert invoke(env, 'task.updated', target, body, request_key) == first
    assert all_events(env) == before
    invoke(env, 'task.updated', target, edit_body(env, target, due_date=original), key(env, 'task.updated', target))
    events = all_events(env)
    assert [e.event_order for e in events] == [1,2,3]
    assert events[0].due_date == events[-1].due_date and events[0].event_id != events[-1].event_id


def test_actual_title_only_preserves_original_event(linked):
    env = linked
    target = create_task(env)
    before = all_events(env)
    invoke(env, 'task.updated', target, edit_body(env, target, title='TEST text only'), key(env, 'task.updated', target))
    assert all_events(env) == before


def test_actual_approved_extension_adds_one_event_after_start_and_request(linked):
    env = linked
    target = create_task(env)
    start_task(env, target)
    extension_id, due = request_extension(env, target)
    assert len(all_events(env)) == 1
    invoke(env, 'task.deadline_extension_approved', target, version_body(env,target),
        key(env, 'task.deadline_extension_approved', target), extension_id=extension_id)
    events = all_events(env)
    assert len(events) == 2 and events[-1].due_date.isoformat() == due
    assert events[-1].source_action == 'task.deadline_extension_approved'


@pytest.mark.parametrize('stage', ['callback','receipt'])
@pytest.mark.parametrize('action', ['task.created','task.updated'])
def test_actual_downstream_audit_or_inserted_receipt_failure_rolls_back_journal(linked, stage, action):
    env = linked
    target = 'new' if action == 'task.created' else create_task(env)
    body = dict(env.body) if action == 'task.created' else edit_body(env,target,
        due_date=(date.fromisoformat(env.body['due_date'])+timedelta(days=1)).isoformat())
    request_key = key(env,action,target)
    original_rows, original_events = persisted(env), all_events(env)
    env.fail_stage, env.fail_action = stage, action
    with pytest.raises(AppError):
        invoke(env,action,target,body,request_key)
    assert persisted(env) == original_rows and all_events(env) == original_events
