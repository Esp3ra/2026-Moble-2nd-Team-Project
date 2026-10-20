"""R1: real MySQL requests with a barrier immediately before admin_guard locking."""
from concurrent.futures import ThreadPoolExecutor
from threading import Event, current_thread

import pytest
from sqlalchemy import event, select, text

from remotework.auth import create_user
from remotework.db import get_db, transaction
from remotework.models import AuditEvent, User


@pytest.mark.parametrize("change", [{"role": "employee"}, {"is_active": False}], ids=["demotion", "deactivation"])
def test_waiting_admin_cannot_mutate_after_its_session_was_revoked(app, login, change):
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql":
        pytest.skip("Requires real MySQL REPEATABLE-READ and row locks")
    with engine.connect() as connection:
        assert connection.scalar(text("SELECT @@transaction_isolation")) == "REPEATABLE-READ"
    with app.app_context(), transaction() as db:
        second = create_user(db, {"login_id": "admin2", "password": "Admin-two-2026!",
            "display_name": "관리자 B", "employee_no": "A002", "department": "운영팀", "role": "admin"})
        second_id = second.id
    first_headers, first = login("admin1", "Admin-test-2026!")
    second_headers, _ = login("admin2", "Admin-two-2026!")
    first_id = first["user"]["id"]
    reached_guard, resume = Event(), Event()

    def pause_before_guard(conn, cursor, statement, parameters, context, executemany):
        if current_thread().name.startswith("waiting-admin") and "FROM admin_guard" in statement:
            reached_guard.set()
            assert resume.wait(15), "Timed out waiting to resume the older authenticated request"

    def delayed_request():
        with app.test_client() as client:
            response = client.patch(f"/api/v1/users/{first_id}", headers=second_headers, json=change)
            return response.status_code, response.get_json()

    event.listen(engine, "before_cursor_execute", pause_before_guard)
    try:
        with ThreadPoolExecutor(max_workers=1, thread_name_prefix="waiting-admin") as pool:
            pending = pool.submit(delayed_request)
            try:
                assert reached_guard.wait(10), "Older request never reached the guard"
                with app.test_client() as client:
                    success = client.patch(f"/api/v1/users/{second_id}", headers=first_headers, json=change)
                assert success.status_code == 200
            finally:
                resume.set()
            status, body = pending.result(timeout=15)
    finally:
        event.remove(engine, "before_cursor_execute", pause_before_guard)
    assert status == 401
    assert body["error"]["code"] == "AUTH_REQUIRED"
    with app.app_context():
        db = get_db()
        remaining = db.scalars(select(User.id).where(User.role == "admin", User.is_active.is_(True))).all()
        assert remaining == [first_id]
        assert db.get(User, first_id).display_name == "관리자"
        changes = db.scalars(select(AuditEvent).where(AuditEvent.action == "user.updated")).all()
        assert len(changes) == 1
        assert changes[0].actor_id == first_id
        assert changes[0].resource_id == str(second_id)
    with app.test_client() as client:
        assert client.get("/api/v1/auth/me", headers=second_headers).status_code == 401


def test_rejected_self_demotion_rolls_back_fields_and_audit(app, client, login):
    headers, identity = login("admin1", "Admin-test-2026!")
    user_id = identity["user"]["id"]
    response = client.patch(f"/api/v1/users/{user_id}", headers=headers,
                            json={"display_name": "must roll back", "role": "employee"})
    assert response.status_code == 400
    assert response.json["error"]["code"] == "SELF_LOCKOUT"
    with app.app_context():
        db = get_db()
        user = db.get(User, user_id)
        assert user.role == "admin" and user.is_active
        assert user.display_name == "관리자"
        assert db.scalar(select(AuditEvent).where(AuditEvent.action == "user.updated")) is None
