import json
import pytest
from sqlalchemy import func, select
from remotework import create_app
from remotework.db import get_db
from remotework.models import AuditEvent
from remotework.features.notices.models import Notice


@pytest.fixture(autouse=True)
def enable_notice_draft(monkeypatch):
    monkeypatch.setenv("REMOTEWORK_ENABLE_NOTICE_DRAFT", "1")


@pytest.fixture
def admin(login):
    return login("admin1", "Admin-test-2026!")[0]


def draft(**changes):
    return {"title": "���� �׽�Ʈ", "content": "�ٽ� �����ص� ���� ����", "target": "������",
            "is_pinned": False, "attachment_names": ["�ȳ�.pdf"]} | changes


def publish(client, admin, **changes):
    r = client.post("/api/v1/notices", json=draft(**changes), headers=admin)
    assert r.status_code == 201, r.json
    return r.json["data"]["notice"]


def test_committed_notice_survives_app_recreation_and_uses_server_identity(app, client, admin):
    n = publish(client, admin)
    assert n["id"] > 0 and n["author"] == "������" and n["created_at"].endswith("Z")
    assert n["attachment_names"] == ["�ȳ�.pdf"]
    # New Flask app + connection pool, same file/database: no process-list state.
    other = create_app({"TESTING": True, "SECRET_KEY": app.config["SECRET_KEY"],
                        "DATABASE_URL": app.config["DATABASE_URL"]})
    try:
        r = other.test_client().get(f'/api/v1/notices/{n["id"]}', headers=admin)
        assert r.status_code == 200 and r.json["data"]["notice"] == n
        with other.app_context():
            assert get_db().scalar(select(func.count()).select_from(Notice)) == 1
            assert get_db().scalar(select(AuditEvent).where(AuditEvent.action == "notice.created")) is not None
    finally:
        other.extensions["db_engine"].dispose()


def test_every_target_is_readable_by_employee_with_exact_filter(client, admin, login):
    for target in ("�� ����", "��ȹ��", "������", "��������"):
        publish(client, admin, target=target)
    employee = login()[0]
    assert client.get("/api/v1/notices", headers=employee).json["data"]["total"] == 4
    for target in ("�� ����", "��ȹ��", "������", "��������"):
        r = client.get("/api/v1/notices", query_string={"target": target}, headers=employee)
        assert r.status_code == 200 and r.json["data"]["total"] == 1
        assert r.json["data"]["items"][0]["target"] == target


def test_auth_and_admin_write_permissions(client, admin, login):
    n = publish(client, admin)
    for path in ("/api/v1/notices", f'/api/v1/notices/{n["id"]}'):
        assert client.get(path).status_code == 401
    employee = login()[0]
    assert client.post("/api/v1/notices", headers=employee, json=draft()).status_code == 403
    assert client.post(f'/api/v1/notices/{n["id"]}/delete', headers=employee, json={}).status_code == 403
    assert client.get(f'/api/v1/notices/{n["id"]}', headers=employee).status_code == 200


def test_delete_is_committed_and_audited(app, client, admin):
    n = publish(client, admin)
    path = f'/api/v1/notices/{n["id"]}'
    assert client.post(path + "/delete", headers=admin, json={}).status_code == 200
    assert client.get(path, headers=admin).status_code == 404
    assert client.get("/api/v1/notices", headers=admin).json["data"]["total"] == 0
    assert client.post(path + "/delete", headers=admin, json={}).status_code == 404
    with app.app_context():
        assert get_db().scalar(select(AuditEvent).where(AuditEvent.action == "notice.deleted")) is not None


@pytest.mark.parametrize("changes", [
    {"title": " "}, {"title": "a" * 201}, {"content": "a" * 5001}, {"target": "������"},
    {"is_pinned": "true"}, {"author": "����"}, {"author_id": 100},
    {"attachment_names": [r"C:\private\�ȳ�.pdf"]}, {"attachment_names": ["../�ȳ�.pdf"]},
    {"attachment_names": ["x"] * 21}, {"attachment_names": None}, {"title": None}])
def test_invalid_input_never_writes(app, client, admin, changes):
    r = client.post("/api/v1/notices", json=draft(**changes), headers=admin)
    assert r.status_code == 400
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(Notice)) == 0
        assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(
            AuditEvent.action == "notice.created")) == 0


def test_pagination_and_pinned_order(client, admin):
    first = publish(client, admin)
    pinned = publish(client, admin, title="��� ����", is_pinned=True)
    r = client.get("/api/v1/notices?limit=1&page=1", headers=admin)
    assert r.json["data"]["total"] == 2 and r.json["data"]["items"][0]["id"] == pinned["id"]
    r = client.get("/api/v1/notices?limit=1&page=2", headers=admin)
    assert r.json["data"]["items"][0]["id"] == first["id"]
    for q in ("limit=101", "page=0", "page=abc", "target=������"):
        assert client.get("/api/v1/notices?" + q, headers=admin).status_code == 400


def test_missing_table_is_explicit_and_init_preserves_rows(app, client, admin):
    with app.app_context():
        Notice.__table__.drop(app.extensions["db_engine"])
    r = client.get("/api/v1/notices", headers=admin)
    assert r.status_code == 503 and r.json["error"]["code"] == "NOTICE_SCHEMA_REQUIRED"
    assert app.test_cli_runner().invoke(args=["notices", "init-table"]).exit_code == 0
    n = publish(client, admin)
    assert app.test_cli_runner().invoke(args=["notices", "init-table"]).exit_code == 0
    assert client.get(f'/api/v1/notices/{n["id"]}', headers=admin).status_code == 200


def test_audit_failure_rolls_back_notice(app, client, admin, monkeypatch):
    from remotework.features.notices import routes
    from sqlalchemy.exc import SQLAlchemyError
    def fail(*args, **kwargs):
        raise SQLAlchemyError("synthetic failure")
    monkeypatch.setattr(routes, "audit", fail)
    assert client.post("/api/v1/notices", json=draft(), headers=admin).status_code == 503
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(Notice)) == 0
