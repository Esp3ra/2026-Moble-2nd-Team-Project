import pytest

@pytest.fixture(autouse=True)
def enable_chat(monkeypatch):
    monkeypatch.setenv("REMOTEWORK_ENABLE_CHAT_DRAFT", "1")

from sqlalchemy import select
from remotework.db import transaction
from remotework.models import User


def test_requires_login_and_uses_no_sample_fallback(client):
    assert client.get('/api/v1/chat/participants').status_code == 401


def test_staff_can_read_active_people_in_all_departments_but_not_private_fields(app, client, login):
    headers, data = login()
    with app.app_context(), transaction() as db:
        base = db.scalar(select(User).where(User.login_id == 'staff1'))
        db.add(User(login_id='design_active', password_hash=base.password_hash,
            display_name='동명이인', employee_no='CHAT-1', department='디자인팀', role='employee'))
        db.add(User(login_id='design_inactive', password_hash=base.password_hash,
            display_name='비활성 사용자', employee_no='CHAT-2', department='디자인팀', role='employee', is_active=False))
    response = client.get('/api/v1/chat/participants', headers=headers)
    assert response.status_code == 200
    rows = response.json['data']['items']
    assert len(rows) == 2 and all(row['id'] != data['user']['id'] for row in rows)
    assert any(row['department'] == '디자인팀' for row in rows)
    assert all(set(row) == {'id', 'display_name', 'department'} for row in rows)
    assert client.get('/api/v1/users', headers=headers).status_code == 403


def test_admin_sees_db_people_and_an_empty_department_is_valid(client, login, app):
    headers, data = login('admin1', 'Admin-test-2026!')
    with app.app_context(), transaction() as db:
        staff = db.scalar(select(User).where(User.login_id == 'staff1'))
        staff.department = ''
    response = client.get('/api/v1/chat/participants?limit=1', headers=headers)
    assert response.status_code == 200 and response.json['data']['total'] == 1
    assert response.json['data']['items'][0]['department'] == ''
    assert response.json['data']['items'][0]['id'] != data['user']['id']
    page2 = client.get('/api/v1/chat/participants?page=2&limit=1', headers=headers)
    assert page2.json['data']['items'] == [] and page2.json['data']['total'] == 1


def test_pages_preserve_distinct_ids_with_duplicate_names_and_empty_result(app, client, login):
    headers, _ = login()
    with app.app_context(), transaction() as db:
        admin = db.scalar(select(User).where(User.login_id == 'admin1'))
        db.add(User(login_id='same_name', password_hash=admin.password_hash,
            display_name=admin.display_name, employee_no='CHAT-DUP', department=admin.department, role='admin'))
    first = client.get('/api/v1/chat/participants?limit=1', headers=headers).json['data']
    second = client.get('/api/v1/chat/participants?page=2&limit=1', headers=headers).json['data']
    assert first['total'] == second['total'] == 2
    assert first['items'][0]['id'] != second['items'][0]['id']
    with app.app_context(), transaction() as db:
        for user in db.scalars(select(User).where(User.login_id != 'staff1')):
            user.is_active = False
    assert client.get('/api/v1/chat/participants', headers=headers).json['data']['items'] == []


def test_invalid_queries_are_rejected(client, login):
    headers, _ = login()
    for query in ('page=0', 'page=x', 'limit=101', 'limit=0', 'page=1&page=2', 'role=admin'):
        assert client.get('/api/v1/chat/participants?' + query, headers=headers).status_code == 400


def test_revoked_session_cannot_list_people(app, client, login):
    headers, _ = login()
    with app.app_context(), transaction() as db:
        db.scalar(select(User).where(User.login_id == 'staff1')).is_active = False
    assert client.get('/api/v1/chat/participants', headers=headers).status_code == 401
