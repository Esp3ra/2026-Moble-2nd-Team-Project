"""Task starting against an owned disposable MySQL database, never SQLite.

Fixture setup creates synthetic tasks only in remote_work_test_ databases. The
product API never seeds tasks, applies DDL, repairs acceptance or submits work.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta
import json
import os
from threading import Barrier, Event, current_thread

import pytest
from sqlalchemy import event, func, inspect, select
from sqlalchemy.exc import OperationalError

from remotework.auth import create_user, token_digest
from remotework.db import get_db, transaction
from remotework.features.tasks import starting
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import AuditEvent, AuthSession, User, utcnow
from remotework.write_requests import WriteReceipt


pytestmark = pytest.mark.skipif(
    not os.getenv('MYSQL_TEST_URL', '').startswith('mysql'),
    reason='Task starting checks require an explicitly owned disposable MySQL database',
)
NOW = datetime(2026, 10, 19, 3, 4, 5, 123456)
FIRST_ACCEPTED = datetime(2026, 10, 18, 12, 30, 20)
ACTIONS = {'accept': 'task.accepted', 'rework-start': 'task.rework_started'}


@pytest.fixture(autouse=True)
def enable_start_draft(monkeypatch):
    monkeypatch.setenv('PYTHON_DOTENV_DISABLED', '1')
    monkeypatch.setenv('REMOTEWORK_ENABLE_TASK_QUERY_DRAFT', '1')
    monkeypatch.setenv('REMOTEWORK_ENABLE_COMMON_WRITE_DRAFT', '1')
    monkeypatch.setenv('REMOTEWORK_ENABLE_TASK_START_DRAFT', '1')
    monkeypatch.setenv('REMOTEWORK_ENABLE_TASK_CREATE_DRAFT', '0')
    monkeypatch.setattr(starting, 'utcnow', lambda: NOW)


@pytest.fixture
def task_env(app):
    assert app.extensions['db_engine'].dialect.name == 'mysql'
    with app.app_context(), transaction() as db:
        users = {u.login_id: u for u in db.scalars(select(User))}
        for login_id, role in [('staff2', 'employee'), ('admin2', 'admin')]:
            users[login_id] = create_user(db, {'login_id': login_id, 'password': 'Starting-test-2026!',
                'display_name': login_id, 'employee_no': 'START-' + login_id, 'role': role})
        ids = {name: user.id for name, user in users.items()}
        task = TaskRecord(title='owned starting fixture', description='keep original text',
            task_status='not_started', start_date=NOW.date(), due_date=(NOW + timedelta(days=2)).date(),
            current_assignment_revision=3, row_version=7, created_by=ids['admin1'],
            created_at=FIRST_ACCEPTED, updated_at=FIRST_ACCEPTED)
        db.add(task)
        db.flush()
        task_id = task.task_id
        db.add(TaskAssignment(task_id=task_id, revision=3, assignee_id=ids['staff1'],
            assigned_by=ids['admin1'], assigned_at=FIRST_ACCEPTED, accepted_at=None))
        db.add(TaskManager(task_id=task_id, user_id=ids['admin1']))
    return app, ids, str(task_id)


def configure(env, state='not_started', accepted='automatic', **changes):
    app, _ids, target = env
    with app.app_context(), transaction() as db:
        task = db.get(TaskRecord, int(target))
        task.task_status = state
        for key, value in changes.items():
            setattr(task, key, value)
        assignment = db.get(TaskAssignment, (int(target), task.current_assignment_revision))
        assignment.accepted_at = (None if state == 'not_started' else FIRST_ACCEPTED) if (
            accepted == 'automatic') else accepted


def key_for(client, headers, target, endpoint='accept'):
    response = client.post('/api/v1/write-requests/keys', headers=headers,
                           json={'action': ACTIONS[endpoint], 'target': target})
    assert response.status_code == 201, response.json
    return response.json['data']['request_key']


def send(client, headers, key, target, endpoint='accept', body=None, **kwargs):
    return client.post('/api/v1/tasks/' + target + '/' + endpoint,
        headers=headers | {'Idempotency-Key': key},
        json={'expected_row_version': '7', 'expected_assignment_revision': 3} if body is None else body,
        **kwargs)


def error(response, status, code):
    assert response.status_code == status, response.json
    assert response.json['ok'] is False and response.json['error']['code'] == code
    assert response.json['request_id'] == response.headers['X-Request-ID']


def recorded(env):
    app, _ids, target = env
    with app.app_context():
        db = get_db()
        task = db.get(TaskRecord, int(target))
        assignment = db.get(TaskAssignment, (int(target), task.current_assignment_revision))
        counts = [db.scalar(select(func.count()).select_from(WriteReceipt)),
                  db.scalar(select(func.count()).select_from(AuditEvent).where(
                      AuditEvent.action.in_(tuple(ACTIONS.values()))))]
        return (task.task_status, task.row_version, task.current_assignment_revision,
                task.updated_at, assignment.accepted_at, tuple(counts))


@pytest.mark.parametrize('endpoint,state', [('accept', 'not_started'), ('rework-start', 'returned')])
@pytest.mark.parametrize('actor', ['staff1', 'admin1'])
def test_success_uses_own_assignment_and_retains_exact_minimal_stored_result(task_env, client, login, endpoint, state, actor):
    app, ids, target = task_env
    configure(task_env, state)
    if actor == 'admin1':
        with app.app_context(), transaction() as db:
            db.get(TaskAssignment, (int(target), 3)).assignee_id = ids[actor]
    headers, _ = login(actor, 'Admin-test-2026!' if actor == 'admin1' else 'Staff-test-2026!')
    key = key_for(client, headers, target, endpoint)
    response = send(client, headers, key, target, endpoint)
    assert response.status_code == 200, response.json
    data = response.json['data']
    assert set(data) == {'task_id', 'row_version', 'assignment_revision', 'task_status', 'updated_at'}
    assert data == {'task_id': target, 'row_version': '8', 'assignment_revision': 3,
                    'task_status': 'in_progress', 'updated_at': '2026-10-19T03:04:05Z'}
    assert recorded(task_env) == ('in_progress', 8, 3, NOW.replace(microsecond=0),
        FIRST_ACCEPTED if state == 'returned' else NOW.replace(microsecond=0), (1, 1))
    with app.app_context():
        db = get_db()
        task = db.get(TaskRecord, int(target))
        assignment = db.get(TaskAssignment, (int(target), 3))
        assert (task.title, task.description, task.created_by, task.created_at) == (
            'owned starting fixture', 'keep original text', ids['admin1'], FIRST_ACCEPTED)
        assert (assignment.assigned_by, assignment.assigned_at) == (ids['admin1'], FIRST_ACCEPTED)
        audit = db.scalar(select(AuditEvent).where(AuditEvent.action == ACTIONS[endpoint]))
        assert (audit.actor_id, audit.resource_type, audit.resource_id) == (ids[actor], 'task', target)
        assert json.loads(audit.details_json) == {'before': {'task_status': state, 'row_version': '7',
            'assignment_revision': 3}, 'after': {'task_status': 'in_progress', 'row_version': '8',
            'assignment_revision': 3}}
        receipt = db.scalar(select(WriteReceipt))
        assert json.loads(receipt.result_json) == data and receipt.http_status == 200


@pytest.mark.parametrize('endpoint', list(ACTIONS))
@pytest.mark.parametrize('state', ['not_started', 'in_progress', 'submitted', 'approved', 'returned'])
def test_only_agreed_source_state_can_start(task_env, client, login, endpoint, state):
    configure(task_env, state)
    headers, _ = login()
    target = task_env[2]
    before = recorded(task_env)
    response = send(client, headers, key_for(client, headers, target, endpoint), target, endpoint)
    if (endpoint, state) in [('accept', 'not_started'), ('rework-start', 'returned')]:
        assert response.status_code == 200, response.json
    else:
        error(response, 409, 'TASK_STATE_CONFLICT')
        assert recorded(task_env) == before


@pytest.mark.parametrize('actor', ['staff2', 'admin1', 'admin2'])
def test_foreign_staff_designated_manager_and_unrelated_admin_cannot_start(task_env, client, login, actor):
    headers, _ = login(actor, 'Admin-test-2026!' if actor == 'admin1' else 'Starting-test-2026!')
    before = recorded(task_env)
    # Access precedes strict JSON parsing and cannot be delegated to a manager.
    response = client.post('/api/v1/tasks/' + task_env[2] + '/accept', headers=headers,
                           data='invalid', content_type='application/json')
    error(response, 403, 'FORBIDDEN')
    assert recorded(task_env) == before


def test_auth_target_and_input_errors_do_not_write(task_env, client, login):
    target = task_env[2]
    error(client.post('/api/v1/tasks/' + target + '/accept', json={}), 401, 'AUTH_REQUIRED')
    headers, _ = login()
    error(send(client, headers, 'unused', '9223372036854775807'), 404, 'TASK_NOT_FOUND')
    for bad in ['0', '01', '-1', '1.0', '9223372036854775808']:
        error(send(client, headers, 'unused', bad), 400, 'INVALID_INPUT')
    key = key_for(client, headers, target)
    response = client.post('/api/v1/tasks/' + target + '/accept?status=not_started',
        headers=headers | {'Idempotency-Key': key}, json={})
    error(response, 400, 'INVALID_INPUT')
    error(send(client, headers, key, target, body={'expected_row_version': '7'}), 400, 'INVALID_INPUT')
    raw = '{"expected_row_version":"7","expected_row_version":"7","expected_assignment_revision":3}'
    error(client.post('/api/v1/tasks/' + target + '/accept',
        headers=headers | {'Idempotency-Key': key}, data=raw, content_type='application/json'), 400, 'INVALID_INPUT')
    assert recorded(task_env)[-1] == (0, 0)


@pytest.mark.parametrize('row,assignment,code', [(6, 2, 'TASK_ASSIGNMENT_CHANGED'),
    (7, 2, 'TASK_ASSIGNMENT_CHANGED'), (6, 3, 'TASK_VERSION_CONFLICT')])
def test_expected_assignment_precedes_row_then_state_and_date(task_env, client, login, row, assignment, code):
    configure(task_env, 'approved', due_date=NOW.date() - timedelta(days=1),
              start_date=NOW.date() - timedelta(days=2))
    headers, _ = login()
    target = task_env[2]
    before = recorded(task_env)
    response = send(client, headers, key_for(client, headers, target), target,
                    body={'expected_row_version': str(row), 'expected_assignment_revision': assignment})
    error(response, 409, code)
    assert recorded(task_env) == before


@pytest.mark.parametrize('endpoint,state,accepted', [
    ('accept', 'not_started', FIRST_ACCEPTED), ('rework-start', 'returned', None),
    ('rework-start', 'returned', NOW + timedelta(days=1)),
])
def test_inconsistent_acceptance_is_never_repaired(task_env, client, login, endpoint, state, accepted):
    configure(task_env, state, accepted=accepted)
    headers, _ = login()
    target = task_env[2]
    before = recorded(task_env)
    error(send(client, headers, key_for(client, headers, target, endpoint), target, endpoint),
          409, 'TASK_START_DATA_INCONSISTENT')
    assert recorded(task_env) == before


@pytest.mark.parametrize('endpoint,state', [('accept', 'not_started'), ('rework-start', 'returned')])
@pytest.mark.parametrize('instant,code', [
    (datetime(2026, 10, 18, 14, 59, 59, 999999), 'TASK_START_BEFORE_START_DATE'),
    (datetime(2026, 10, 18, 15), None),
    (datetime(2026, 10, 21, 14, 59, 59, 999999), None),
    (datetime(2026, 10, 21, 15), 'TASK_START_AFTER_DUE_DATE'),
])
def test_first_mutation_uses_inclusive_kst_dates(task_env, client, login, monkeypatch, endpoint, state, instant, code):
    configure(task_env, state, due_date=datetime(2026, 10, 21).date())
    monkeypatch.setattr(starting, 'utcnow', lambda: instant)
    headers, _ = login()
    target = task_env[2]
    before = recorded(task_env)
    response = send(client, headers, key_for(client, headers, target, endpoint), target, endpoint)
    if code:
        error(response, 409, code)
        assert recorded(task_env) == before
    else:
        assert response.status_code == 200, response.json


def test_version_exhaustion_precedes_date_and_never_wraps(task_env, client, login):
    maximum = (1 << 63) - 1
    configure(task_env, row_version=maximum, start_date=NOW.date() + timedelta(days=1))
    headers, _ = login()
    target = task_env[2]
    before = recorded(task_env)
    error(send(client, headers, key_for(client, headers, target), target,
        body={'expected_row_version': str(maximum), 'expected_assignment_revision': 3}),
        409, 'TASK_VERSION_EXHAUSTED')
    assert recorded(task_env) == before


@pytest.mark.parametrize('endpoint,state', [('accept', 'not_started'), ('rework-start', 'returned')])
def test_replay_and_lookup_preserve_success_after_state_date_change_but_recheck_current_access(task_env, client, login, monkeypatch, endpoint, state):
    configure(task_env, state)
    headers, _ = login()
    app, ids, target = task_env
    key = key_for(client, headers, target, endpoint)
    first = send(client, headers, key, target, endpoint)
    assert first.status_code == 200, first.json
    configure(task_env, 'submitted', row_version=9)
    monkeypatch.setattr(starting, 'utcnow', lambda: NOW + timedelta(days=30))
    again = send(client, headers, key, target, endpoint)
    assert again.status_code == 200 and again.json['data'] == first.json['data']
    error(send(client, headers, key, target, endpoint,
        body={'expected_row_version': '9', 'expected_assignment_revision': 3}), 409, 'IDEMPOTENCY_KEY_REUSED')
    lookup = lambda: client.post('/api/v1/write-requests/result',
        headers=headers | {'Idempotency-Key': key}, json={'action': ACTIONS[endpoint], 'target': target})
    result = lookup()
    assert result.status_code == 200 and result.json['data']['result'] == first.json['data']
    assert recorded(task_env)[-1] == (1, 1)
    with app.app_context(), transaction() as db:
        db.get(TaskAssignment, (int(target), 3)).assignee_id = ids['staff2']
    error(send(client, headers, key, target, endpoint), 403, 'FORBIDDEN')
    error(lookup(), 403, 'FORBIDDEN')
    assert recorded(task_env)[-1] == (1, 1)


@pytest.mark.parametrize('change', ['session_removed', 'session_expired', 'inactive'])
def test_auth_revocation_prevents_new_mutation(task_env, client, login, change):
    app, ids, target = task_env
    headers, data = login()
    key = key_for(client, headers, target)
    token_hash = token_digest(data['access_token'])
    with app.app_context(), transaction() as db:
        if change == 'inactive':
            db.get(User, ids['staff1']).is_active = False
        elif change == 'session_expired':
            db.get(AuthSession, token_hash).expires_at = utcnow() - timedelta(seconds=1)
        else:
            db.delete(db.get(AuthSession, token_hash))
    before = recorded(task_env)
    error(send(client, headers, key, target), 401, 'AUTH_REQUIRED')
    assert recorded(task_env) == before


def test_missing_retained_result_and_expired_key_are_not_a_new_start(task_env, client, login, monkeypatch):
    from remotework import write_requests
    headers, _ = login()
    app, _ids, target = task_env
    key = key_for(client, headers, target)
    lookup = lambda: client.post('/api/v1/write-requests/result',
        headers=headers | {'Idempotency-Key': key}, json={'action': 'task.accepted', 'target': target})
    assert lookup().json['data']['state'] == 'not_recorded'
    assert send(client, headers, key, target).status_code == 200
    with app.app_context(), transaction() as db:
        db.delete(db.scalar(select(WriteReceipt)))
    assert lookup().json['data']['state'] == 'not_recorded'
    before = recorded(task_env)
    # A lost receipt does not bypass the agreed version-before-state check.
    error(send(client, headers, key, target), 409, 'TASK_VERSION_CONFLICT')
    original_time = write_requests.time.time
    monkeypatch.setattr(write_requests.time, 'time', lambda: original_time() + 86401)
    error(lookup(), 410, 'IDEMPOTENCY_KEY_EXPIRED')
    error(send(client, headers, key, target), 410, 'IDEMPOTENCY_KEY_EXPIRED')
    assert recorded(task_env) == before


@pytest.mark.parametrize('failed_table', ['AUDIT_EVENTS', 'WRITE_RECEIPTS'])
def test_audit_or_receipt_failure_rolls_back_every_business_change(task_env, client, login, failed_table):
    app, _ids, target = task_env
    headers, _ = login()
    key = key_for(client, headers, target)
    engine = app.extensions['db_engine']
    before = recorded(task_env)
    failures = []
    def fail(conn, cursor, statement, parameters, context, executemany):
        if statement.lstrip().upper().startswith('INSERT INTO ' + failed_table):
            failures.append(True)
            raise OperationalError('synthetic owned start failure', {}, RuntimeError('owned fixture'))
    event.listen(engine, 'before_cursor_execute', fail)
    try:
        error(send(client, headers, key, target), 503, 'DATABASE_UNAVAILABLE')
    finally:
        event.remove(engine, 'before_cursor_execute', fail)
    assert failures == [True] and recorded(task_env) == before
    assert send(client, headers, key, target).status_code == 200
    assert recorded(task_env)[-1] == (1, 1)


@pytest.mark.parametrize('same_key', [True, False])
def test_mysql_rr_simultaneous_requests_apply_at_most_one_start(task_env, client, login, same_key):
    app, _ids, target = task_env
    headers, _ = login()
    key1 = key_for(client, headers, target)
    key2 = key1 if same_key else key_for(client, headers, target)
    gate = Barrier(2)
    def execute(key):
        gate.wait(timeout=10)
        with app.test_client() as own_client:
            response = send(own_client, headers, key, target)
            return response.status_code, response.json
    with ThreadPoolExecutor(max_workers=2) as pool:
        futures = [pool.submit(execute, key) for key in (key1, key2)]
        results = [f.result(timeout=25) for f in futures]
    if same_key:
        assert [status for status, _ in results] == [200, 200], results
        assert results[0][1]['data'] == results[1][1]['data']
    else:
        assert sorted(status for status, _ in results) == [200, 409], results
        assert next(body for status, body in results if status == 409)['error']['code'] == 'TASK_VERSION_CONFLICT'
    assert recorded(task_env)[:3] == ('in_progress', 8, 3)
    assert recorded(task_env)[-1] == (1, 1)


def test_assignment_change_while_waiting_for_guard_is_current_read_not_old_snapshot(task_env, client, login):
    app, ids, target = task_env
    headers, _ = login()
    key = key_for(client, headers, target)
    engine = app.extensions['db_engine']
    reached, resume = Event(), Event()
    def pause(conn, cursor, statement, parameters, context, executemany):
        if current_thread().name.startswith('waiting-start') and 'FROM admin_guard' in statement:
            reached.set()
            assert resume.wait(15)
    def execute():
        with app.test_client() as own_client:
            return send(own_client, headers, key, target)
    event.listen(engine, 'before_cursor_execute', pause)
    try:
        with ThreadPoolExecutor(max_workers=1, thread_name_prefix='waiting-start') as pool:
            future = pool.submit(execute)
            try:
                assert reached.wait(10)
                with app.app_context(), transaction() as db:
                    db.get(TaskAssignment, (int(target), 3)).assignee_id = ids['staff2']
            finally:
                resume.set()
            error(future.result(timeout=20), 403, 'FORBIDDEN')
    finally:
        event.remove(engine, 'before_cursor_execute', pause)
    assert recorded(task_env)[-1] == (0, 0)


def test_same_employee_new_assignment_revision_rejects_old_expected_versions(task_env, client, login):
    app, ids, target = task_env
    headers, _ = login()
    key = key_for(client, headers, target)
    with app.app_context(), transaction() as db:
        task = db.get(TaskRecord, int(target))
        task.current_assignment_revision = 4
        task.row_version = 8
        db.add(TaskAssignment(task_id=int(target), revision=4, assignee_id=ids['staff1'],
            assigned_by=ids['admin1'], assigned_at=FIRST_ACCEPTED, accepted_at=None))
    before = recorded(task_env)
    error(send(client, headers, key, target), 409, 'TASK_ASSIGNMENT_CHANGED')
    assert recorded(task_env) == before


def test_missing_dependency_and_tables_fail_closed_without_ddl(task_env, client, login):
    app, _ids, target = task_env
    headers, _ = login()
    key = key_for(client, headers, target)
    before = recorded(task_env)
    accepted = app.extensions['write_authorizers'].pop('task.accepted')
    try:
        error(send(client, headers, key, target), 503, 'WRITE_ACTION_NOT_READY')
    finally:
        app.extensions['write_authorizers']['task.accepted'] = accepted
    app.config['ENABLE_COMMON_WRITE_DRAFT'] = False
    try:
        error(send(client, headers, key, target), 503, 'WRITE_SUPPORT_DISABLED')
    finally:
        app.config['ENABLE_COMMON_WRITE_DRAFT'] = True
    engine = app.extensions['db_engine']
    WriteReceipt.__table__.drop(engine)
    try:
        error(send(client, headers, key, target), 503, 'WRITE_SCHEMA_REQUIRED')
        assert not inspect(engine).has_table('write_receipts')
    finally:
        WriteReceipt.__table__.create(engine)
    assert recorded(task_env) == before
