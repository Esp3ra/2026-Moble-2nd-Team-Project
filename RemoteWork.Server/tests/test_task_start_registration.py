"""Cold feature boundaries and the agreed first-mutation validation order."""
from datetime import date, datetime, timezone
import os
import subprocess
import sys
from types import SimpleNamespace

from flask import Flask
import pytest

from remotework.common import AppError
from remotework.features.tasks.start_transition import TaskStartAction
from remotework.features.tasks.starting import _input, _plan, _utc_from_storage
from remotework.write_contract import BIGINT_MAX, ExpectedVersions


NOW = datetime(2026, 10, 19, 3, 4, 5, 123456)


@pytest.mark.parametrize("query,start,common", [
    (False, False, False), (False, True, False), (False, True, True),
    (True, False, False), (True, False, True), (True, True, False), (True, True, True),
])
def test_cold_registration_flags_do_not_connect_or_leak_receipt_metadata(query, start, common):
    source = '''
import sys
from flask import g
from sqlalchemy import event
from remotework import create_app
import remotework.auth as auth
from remotework.models import Base
auth.load_identity = lambda: setattr(g, 'user', object())
app = create_app({'TESTING': True, 'SECRET_KEY': 'owned-cold-check-' + 'x' * 32,
    'DATABASE_URL': 'mysql+pymysql://unused:unused@127.0.0.1:1/remote_work_test_cold',
    'ENABLE_TASK_QUERY_DRAFT': __QUERY__, 'ENABLE_TASK_START_DRAFT': __START__,
    'ENABLE_COMMON_WRITE_DRAFT': __COMMON__, 'ENABLE_TASK_CREATE_DRAFT': False,
    'ENABLE_NOTICE_DRAFT': False})
def no_connection(*args):
    raise AssertionError('Cold setup must never connect or create tables')
event.listen(app.extensions['db_engine'], 'connect', no_connection)
try:
    rules = {r.rule for r in app.url_map.iter_rules()}
    expected = __QUERY__ and __START__
    assert ('/api/v1/tasks/<string:task_id>/accept' in rules) is expected
    assert ('/api/v1/tasks/<string:task_id>/rework-start' in rules) is expected
    web_enabled = expected and __COMMON__
    assert ('/tasks/<int:task_id>/start/<action>' in rules) is web_enabled
    assert ('/tasks/write/execute' in rules) is web_enabled
    assert ('/tasks/write/result' in rules) is web_enabled
    assert '/tasks/new' not in rules
    assert ('task.rework_started' in app.extensions.get('write_action_contracts', {})) is (expected and __COMMON__)
    assert ('write_receipts' in Base.metadata.tables) is __COMMON__
    assert ('remotework.write_requests' in sys.modules) is __COMMON__
    if not __COMMON__:
        assert set(Base.metadata.tables) == ({'users', 'auth_sessions', 'admin_guard',
            'login_failures', 'resource_grants', 'audit_events', 'schema_versions'} |
            ({'tasks', 'task_assignments', 'task_managers'} if __QUERY__ else set()))
    if expected and not __COMMON__:
        response = app.test_client().post('/api/v1/tasks/1/accept', json={})
        assert response.status_code == 503
        assert response.json['error']['code'] == 'WRITE_SUPPORT_DISABLED'
    if expected and __COMMON__:
        from remotework.features.tasks.starting import authorize_rework
        assert app.extensions['write_action_contracts']['task.rework_started'].authorize is authorize_rework
        assert set(app.extensions['write_action_contracts']['task.rework_started'].result_fields) == {
            'task_id', 'row_version', 'assignment_revision', 'task_status', 'updated_at'}
        assert callable(app.extensions['write_authorizers']['task.accepted'])
finally:
    app.extensions['db_engine'].dispose()
'''.replace('__QUERY__', repr(query)).replace('__START__', repr(start)).replace('__COMMON__', repr(common))
    env = {key: value for key, value in os.environ.items()
           if not key.startswith(('REMOTEWORK_', 'MYSQL_', 'DATABASE_', 'SECRET_', 'WRITE_REQUEST_'))}
    env['PYTHON_DOTENV_DISABLED'] = '1'
    completed = subprocess.run([sys.executable, '-c', source], capture_output=True, text=True, env=env)
    assert completed.returncode == 0, completed.stderr


@pytest.mark.parametrize("raw", [
    'null', '[]', '{}', '{"expected_row_version":"1"}',
    '{"expected_row_version":"1","expected_assignment_revision":1,"actor_id":1}',
    '{"expected_row_version":"1","expected_row_version":"2","expected_assignment_revision":1}',
    '{"expected_row_version":"1","expected_assignment_revision":1,"expected_assignment_revision":1}',
    '{"expected_row_version":"1","expected_assignment_revision":NaN}',
    '{"expected_row_version":1,"expected_assignment_revision":1}',
    '{"expected_row_version":"01","expected_assignment_revision":1}',
    '{"expected_row_version":"9223372036854775808","expected_assignment_revision":1}',
    '{"expected_row_version":"1","expected_assignment_revision":true}',
    '{"expected_row_version":"1","expected_assignment_revision":1.0}',
    '{"expected_row_version":"1","expected_assignment_revision":2147483648}',
    '{', b'\xff',
])
def test_input_rejects_duplicate_unsupported_or_coerced_versions(raw):
    with Flask(__name__).test_request_context('/', data=raw, content_type='application/json'):
        with pytest.raises(AppError) as exc:
            _input()
        assert (exc.value.status, exc.value.code) == (400, 'INVALID_INPUT')


def test_input_accepts_only_two_exact_fields_and_never_rewrites_body():
    body = {'expected_row_version': str(BIGINT_MAX), 'expected_assignment_revision': 2147483647}
    with Flask(__name__).test_request_context('/', json=body):
        parsed, expected = _input()
    assert parsed == body and expected == ExpectedVersions(BIGINT_MAX, 2147483647)


@pytest.mark.parametrize('path,content_type,status,code', [
    ('/?x=1', 'application/json', 400, 'INVALID_INPUT'),
    ('/?x=1&x=2', 'application/json', 400, 'INVALID_INPUT'),
    ('/', 'text/plain', 415, 'JSON_REQUIRED'),
])
def test_input_rejects_query_and_non_json(path, content_type, status, code):
    with Flask(__name__).test_request_context(path, data='{}', content_type=content_type):
        with pytest.raises(AppError) as exc:
            _input()
        assert (exc.value.status, exc.value.code) == (status, code)


def current(**changes):
    fields = dict(task_status='not_started', row_version=7, current_assignment_revision=3,
                  start_date=date(2026, 10, 19), due_date=date(2026, 10, 22))
    fields.update(changes)
    return SimpleNamespace(**fields)


@pytest.mark.parametrize('row,assignment,state,accepted,expected', [
    (6, 2, 'returned', None, 'TASK_ASSIGNMENT_CHANGED'),
    (6, 3, 'returned', None, 'TASK_VERSION_CONFLICT'),
    (7, 3, 'returned', None, 'TASK_STATE_CONFLICT'),
    (7, 3, 'not_started', NOW, 'TASK_START_DATA_INCONSISTENT'),
    (BIGINT_MAX, 3, 'not_started', None, 'TASK_VERSION_EXHAUSTED'),
])
def test_http_precedence_is_versions_state_acceptance_limit_then_kst(row, assignment, state, accepted, expected):
    task = current(task_status=state, row_version=row)
    requested = BIGINT_MAX if row == BIGINT_MAX else 7
    with pytest.raises(AppError) as exc:
        _plan(task, SimpleNamespace(accepted_at=accepted), ExpectedVersions(requested, assignment),
              TaskStartAction.ACCEPT, datetime(2026, 10, 23))
    assert exc.value.code == expected


@pytest.mark.parametrize('action', list(TaskStartAction))
def test_adapter_sets_first_acceptance_or_preserves_rework_and_uses_utc(action):
    accepted = datetime(2026, 10, 18, 3) if action is TaskStartAction.REWORK_START else None
    task = current(task_status='returned' if accepted else 'not_started')
    plan = _plan(task, SimpleNamespace(accepted_at=accepted), ExpectedVersions(7, 3), action, NOW)
    assert plan.task_status == 'in_progress' and plan.row_version == 8 and plan.assignment_revision == 3
    assert plan.updated_at == NOW.replace(tzinfo=timezone.utc)
    assert plan.accepted_at == (accepted or NOW).replace(tzinfo=timezone.utc)
    assert task.row_version == 7


@pytest.mark.parametrize('value', [None, date(2026, 10, 19), NOW.replace(tzinfo=timezone.utc)])
def test_storage_adapter_does_not_guess_or_double_normalize_timestamps(value):
    with pytest.raises(ValueError):
        _utc_from_storage(value)
