"""W1 strict wire types and staff-capable current-read transactions."""
from concurrent.futures import ThreadPoolExecutor
from threading import Event, current_thread

import pytest
from flask import g
from sqlalchemy import event, select, text

from remotework.auth import authenticated_transaction, admin_transaction, login_required
from remotework.common import AppError, audit, success
from remotework.db import check_schema, get_db, schema_status, transaction
from remotework.models import AuditEvent, AuthSession, User
from remotework.write_contract import ExpectedVersions, lock_users, positive_bigint, positive_integer


@pytest.mark.parametrize("value", [None, True, 1, 1.0, "", "0", "01", "+1", "-1", " 1", "1 ",
                                  "1.0", "１", "١", "1\n", "9223372036854775808", "9" * 10000])
def test_bigint_invalid_wire_values(value):
    with pytest.raises(AppError) as caught:
        positive_bigint(value, "value")
    assert caught.value.status == 400


@pytest.mark.parametrize("value", [None, True, "1", 0, -1, 1.0, 2147483648])
def test_integer_invalid_wire_values(value):
    with pytest.raises(AppError):
        positive_integer(value, "value")


def test_versions_canonical_range_and_conflict_precedence():
    assert positive_bigint("9223372036854775807", "id") == 9223372036854775807
    assert positive_integer(2147483647, "revision") == 2147483647
    expected = ExpectedVersions.from_body({"expected_row_version": "9007199254740993",
                                           "expected_assignment_revision": 2})
    expected.check(row_version=9007199254740993, assignment_revision=2)
    for row, revision, code in [(1, 1, "TASK_ASSIGNMENT_CHANGED"), (1, 2, "TASK_VERSION_CONFLICT")]:
        with pytest.raises(AppError) as caught:
            expected.check(row_version=row, assignment_revision=revision)
        assert (caught.value.status, caught.value.code) == (409, code)
    with pytest.raises(AppError):
        ExpectedVersions.from_body([])


def test_schema_requirements_do_not_guess_or_change_applied_versions(app):
    with app.app_context():
        assert schema_status()=={'required_versions':[1],'recorded_versions':[1],'missing_versions':[]}
        with pytest.raises(AppError) as caught:
            check_schema((1,2))
        assert caught.value.status==503
        assert schema_status((1,2))['missing_versions']==[2]
        assert schema_status()['recorded_versions']==[1]


def install_probe(app):
    # This route is test-only; no business mutation route is installed by the product.
    @app.post("/api/v1/_write_probe")
    @login_required
    def probe():
        with authenticated_transaction() as db:
            g.user.display_name = "committed by staff"
            audit("test.staff_write", "user", g.user.id)
        return success({"actor_id": g.user.id})


def test_staff_guard_commits_and_admin_wrapper_still_denies(app, login):
    install_probe(app)
    headers, identity = login()
    with app.test_client() as client:
        assert client.post("/api/v1/_write_probe", headers=headers).status_code == 200
    with app.app_context():
        assert get_db().get(User, identity["user"]["id"]).display_name == "committed by staff"
    with app.test_request_context():
        with pytest.raises(AppError) as caught:
            with authenticated_transaction():
                pytest.fail("unauthenticated caller entered")
        assert caught.value.status == 401


def test_guard_rollback_and_sorted_user_lock(app, login):
    _, identity = login()
    with app.test_request_context():
        db = get_db()
        g.user = db.get(User, identity["user"]["id"])
        g.auth_session = db.scalar(select(AuthSession).where(AuthSession.user_id == g.user.id))
        with pytest.raises(RuntimeError, match="abort"):
            with authenticated_transaction() as locked:
                users = lock_users(locked, [g.user.id, g.user.id])
                assert list(users) == [g.user.id]
                g.user.display_name = "must rollback"
                audit("test.must_rollback", "user", g.user.id)
                raise RuntimeError("abort")
        assert not g.write_transaction_active
        assert db.get(User, g.user.id).display_name == "직원"
        assert db.scalar(select(AuditEvent).where(AuditEvent.action == "test.must_rollback")) is None
        with pytest.raises(AppError) as caught:
            with admin_transaction():
                pytest.fail("staff entered admin transaction")
        assert caught.value.status == 403
        with pytest.raises(RuntimeError):
            lock_users(db, [g.user.id])
        with authenticated_transaction():
            with pytest.raises(RuntimeError, match="nested"):
                with authenticated_transaction():
                    pytest.fail("nested commit")


@pytest.mark.parametrize("change", ["deactivation", "role_change", "logout", "expiry"])
def test_mysql_waiting_staff_current_identity_after_guard(app, login, change):
    install_probe(app)
    engine = app.extensions["db_engine"]
    if engine.dialect.name != "mysql":
        pytest.skip("Requires MySQL REPEATABLE-READ locks")
    with engine.connect() as conn:
        assert conn.scalar(text("SELECT @@transaction_isolation")) == "REPEATABLE-READ"
    headers, staff = login()
    admin_headers, _ = login("admin1", "Admin-test-2026!")
    reached, resume = Event(), Event()

    def pause(conn, cursor, statement, parameters, context, executemany):
        if current_thread().name.startswith("waiting-staff") and "FROM admin_guard" in statement:
            reached.set()
            assert resume.wait(20)

    def delayed():
        with app.test_client() as client:
            response = client.post("/api/v1/_write_probe", headers=headers)
            return response.status_code, response.json

    event.listen(engine, "before_cursor_execute", pause)
    try:
        with ThreadPoolExecutor(max_workers=1, thread_name_prefix="waiting-staff") as pool:
            future = pool.submit(delayed)
            try:
                assert reached.wait(15)
                with app.test_client() as client:
                    if change in ("deactivation", "role_change"):
                        # Role mutation revokes all sessions even for employee -> admin.
                        body = {"is_active": False} if change == "deactivation" else {"role": "admin"}
                        assert client.patch(f'/api/v1/users/{staff["user"]["id"]}',
                                            headers=admin_headers, json=body).status_code == 200
                    elif change == "logout":
                        assert client.post("/api/v1/auth/logout", headers=headers).status_code == 200
                    else:
                        from datetime import timedelta
                        from remotework.models import utcnow
                        with app.app_context(), transaction() as db:
                            session = db.scalar(select(AuthSession).where(AuthSession.user_id == staff["user"]["id"]))
                            session.expires_at = utcnow() - timedelta(seconds=1)
            finally:
                resume.set()
            status, body = future.result(timeout=20)
    finally:
        event.remove(engine, "before_cursor_execute", pause)
    assert status == 401 and body["error"]["code"] == "AUTH_REQUIRED"
    with app.app_context():
        assert get_db().scalar(select(AuditEvent).where(AuditEvent.action == "test.staff_write")) is None
        assert get_db().get(User, staff["user"]["id"]).display_name == "직원"
