"""PR7 R1/R2 regression. Uses the common disposable DB fixture; never a shared DB."""
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
from datetime import datetime
from threading import Event, current_thread

import pytest
from sqlalchemy import event, func, select, text

from remotework.auth import create_user
from remotework.db import get_db, transaction
from remotework.models import AuditEvent
from remotework.features.notices.models import Notice
from remotework.features.notices import routes


@pytest.fixture(autouse=True)
def enable_notices(monkeypatch):
    monkeypatch.setenv("REMOTEWORK_ENABLE_NOTICE_DRAFT", "1")


def publish(client, headers):
    response = client.post("/api/v1/notices", headers=headers,
        json={"title": "review regression", "content": "synthetic", "target": "전 직원"})
    assert response.status_code == 201
    return response.json["data"]["notice"]["id"]


@pytest.mark.parametrize("prime_identity_map", [False, True], ids=["auth-snapshot", "cached-notice"])
def test_r1_two_authenticated_admin_requests_delete_once(app, client, login, monkeypatch, prime_identity_map):
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql":
        pytest.skip("Requires actual MySQL REPEATABLE-READ/current reads")
    with engine.connect() as connection:
        assert connection.scalar(text("SELECT @@transaction_isolation")) == "REPEATABLE-READ"
    with app.app_context(), transaction() as db:
        create_user(db, {"login_id": "review_admin2", "password": "Review-test-only-2026!",
            "display_name": "synthetic admin2", "employee_no": "REVIEW002", "department": "운영팀", "role": "admin"})
    first_headers, first = login("admin1", "Admin-test-2026!")
    second_headers, _ = login("review_admin2", "Review-test-only-2026!")
    notice_id = publish(client, first_headers)
    reached_guard, resume = Event(), Event()
    actual_guard = routes.admin_transaction

    @contextmanager
    def guarded_with_optional_cache():
        # Additional case: retain an ORM identity loaded before the other delete.
        if prime_identity_map and current_thread().name.startswith("notice-waiting"):
            db = get_db()
            db.info["review_cached_notice"] = db.get(Notice, notice_id)
            assert db.info["review_cached_notice"] is not None
        with actual_guard() as db:
            yield db

    monkeypatch.setattr(routes, "admin_transaction", guarded_with_optional_cache)

    def before_guard(conn, cursor, statement, parameters, context, executemany):
        if current_thread().name.startswith("notice-waiting") and "FROM admin_guard" in statement:
            reached_guard.set()
            assert resume.wait(15), "Second authenticated request did not resume"

    def waiting_delete():
        with app.test_client() as other:
            response = other.post(f"/api/v1/notices/{notice_id}/delete", headers=second_headers, json={})
            return response.status_code, response.json

    event.listen(engine, "before_cursor_execute", before_guard)
    try:
        with ThreadPoolExecutor(max_workers=1, thread_name_prefix="notice-waiting") as pool:
            pending = pool.submit(waiting_delete)
            try:
                assert reached_guard.wait(10), "Second request never completed authentication before guard"
                first_response = client.post(f"/api/v1/notices/{notice_id}/delete", headers=first_headers, json={})
                assert first_response.status_code == 200
            finally:
                resume.set()
            status, body = pending.result(timeout=15)
    finally:
        event.remove(engine, "before_cursor_execute", before_guard)
    assert status == 404, body
    assert body["error"]["code"] == "NOTICE_NOT_FOUND"
    with app.app_context():
        db = get_db()
        assert db.get(Notice, notice_id) is None
        audits = db.scalars(select(AuditEvent).where(AuditEvent.action == "notice.deleted",
            AuditEvent.resource_id == str(notice_id))).all()
        assert len(audits) == 1
        assert audits[0].actor_id == first["user"]["id"]
    assert client.post(f"/api/v1/notices/{notice_id}/delete", headers=first_headers, json={}).status_code == 404


@pytest.mark.parametrize("notice_id", [0, 2147483647, 2147483648, 9223372036854775807,
                                      9223372036854775808, 10**100])
def test_r2_id_boundaries_preserve_auth_and_never_mutate(app, client, login, notice_id):
    admin, _ = login("admin1", "Admin-test-2026!")
    employee, _ = login()
    existing_id = publish(client, admin)
    path = f"/api/v1/notices/{notice_id}"
    # Authentication/authorization must run before the feature's ID check.
    assert client.get(path).status_code == 401
    assert client.post(path + "/delete", headers=employee, json={}).status_code == 403
    queries = []
    def observe(conn, cursor, statement, parameters, context, executemany):
        if "FROM notices" in statement:
            queries.append(statement)
    engine = app.extensions["db_engine"]
    event.listen(engine, "before_cursor_execute", observe)
    try:
        for response in (client.get(path, headers=employee),
                         client.post(path + "/delete", headers=admin, json={})):
            assert response.status_code == 404, response.json
            assert response.json["error"]["code"] == "NOTICE_NOT_FOUND"
    finally:
        event.remove(engine, "before_cursor_execute", observe)
    if not 1 <= notice_id <= 2147483647:
        assert not queries, "Out-of-range IDs must not be bound to the notice query"
    assert client.get(f"/api/v1/notices/{existing_id}", headers=employee).status_code == 200
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(Notice)) == 1
        assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(
            AuditEvent.action == "notice.deleted")) == 0


def test_created_response_matches_persisted_timestamp_precision(app, client, login):
    admin, _ = login("admin1", "Admin-test-2026!")
    def fractional_created_at(mapper, connection, target):
        target.created_at = datetime(2026, 10, 17, 6, 0, 0, 750000)
    event.listen(Notice, "before_insert", fractional_created_at)
    try:
        response = client.post("/api/v1/notices", headers=admin,
            json={"title": "precision regression", "content": "synthetic", "target": "전 직원"})
        assert response.status_code == 201
    finally:
        event.remove(Notice, "before_insert", fractional_created_at)
    stored = response.json["data"]["notice"]
    assert client.get(f'/api/v1/notices/{stored["id"]}', headers=admin).json["data"]["notice"] == stored


def test_delete_audit_failure_rolls_back_the_actual_delete(app, client, login, monkeypatch):
    from sqlalchemy.exc import SQLAlchemyError
    admin, _ = login("admin1", "Admin-test-2026!")
    notice_id = publish(client, admin)
    def fail_audit(*args, **kwargs):
        raise SQLAlchemyError("synthetic audit failure")
    monkeypatch.setattr(routes, "audit", fail_audit)
    assert client.post(f"/api/v1/notices/{notice_id}/delete", headers=admin, json={}).status_code == 503
    assert client.get(f"/api/v1/notices/{notice_id}", headers=admin).status_code == 200
    with app.app_context():
        assert get_db().scalar(select(func.count()).select_from(AuditEvent).where(
            AuditEvent.action == "notice.deleted")) == 0
