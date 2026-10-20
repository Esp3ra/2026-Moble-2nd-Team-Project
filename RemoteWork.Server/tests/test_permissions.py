import pytest
from flask import g
from sqlalchemy import select, delete
from remotework.auth import login_required
from remotework.common import require_resource_access, grant_resource, success, audit
from remotework.db import transaction, get_db
from remotework.models import User, AuditEvent, ResourceGrant


def test_employee_cannot_admin_even_with_spoofed_role(client, login):
    headers, _ = login()
    headers["X-Role"] = "admin"
    for path in ("/api/v1/users", "/api/v1/audit-events"):
        assert client.get(path, headers=headers).status_code == 403
    assert client.post("/api/v1/users", headers=headers, json={"role": "admin"}).status_code == 403
    assert client.patch("/api/v1/users/1", headers=headers, json={"role": "admin"}).status_code == 403


def test_admin_create_update_reset_and_inactive(app, client, login):
    admin, _ = login("admin1", "Admin-test-2026!")
    body = {"login_id": "newstaff", "password": "New-staff-2026!", "display_name": "새 직원",
             "employee_no": "E002", "department": "기획", "role": "employee"}
    result = client.post("/api/v1/users", headers=admin, json=body)
    assert result.status_code == 201
    uid = result.json["data"]["user"]["id"]
    assert client.post("/api/v1/users", headers=admin, json=body).status_code == 409
    newuser, _ = login("newstaff", "New-staff-2026!")
    assert client.patch(f"/api/v1/users/{uid}", headers=admin, json={"is_active": False}).status_code == 200
    assert client.get("/api/v1/auth/me", headers=newuser).status_code == 401
    assert client.post("/api/v1/auth/login", json={"login_id": "newstaff", "password": "New-staff-2026!"}).status_code == 401
    assert client.patch(f"/api/v1/users/{uid}", headers=admin, json={"is_active": True}).status_code == 200
    newuser, _ = login("newstaff", "New-staff-2026!")
    assert client.post(f"/api/v1/users/{uid}/password", headers=admin, json={"new_password": "Reset-staff-2026!"}).status_code == 200
    assert client.get("/api/v1/auth/me", headers=newuser).status_code == 401
    login("newstaff", "Reset-staff-2026!")
    events = client.get("/api/v1/audit-events", headers=admin).json["data"]["items"]
    assert any(e["action"] == "user.password_reset" for e in events)


def test_role_change_revokes_existing_session(app, client, login):
    admin, _ = login("admin1", "Admin-test-2026!")
    employee, data = login()
    uid = data["user"]["id"]
    assert client.patch(f"/api/v1/users/{uid}", headers=admin, json={"role": "admin"}).status_code == 200
    assert client.get("/api/v1/auth/me", headers=employee).status_code == 401
    promoted, _ = login()
    assert client.get("/api/v1/users", headers=promoted).status_code == 200


def test_self_lockout_and_input_validation(client, login):
    admin, data = login("admin1", "Admin-test-2026!")
    uid = data["user"]["id"]
    assert client.patch(f"/api/v1/users/{uid}", headers=admin, json={"is_active": False}).status_code == 400
    assert client.patch(f"/api/v1/users/{uid}", headers=admin, json={"role": "employee"}).status_code == 400
    assert client.patch(f"/api/v1/users/{uid}", headers=admin, json={"is_active": "false"}).status_code == 400
    assert client.get("/api/v1/users?page=garbage", headers=admin).status_code == 400
    assert client.post("/api/v1/users", headers=admin, json={"password": "short"}).status_code == 400
    assert client.patch("/api/v1/users/999999", headers=admin, json={"department": "x"}).status_code == 404
    assert client.get("/api/v1/auth/me", headers=admin).json["data"]["user"]["role"] == "admin"


@pytest.mark.parametrize("kind", ["task", "chat", "file"])
def test_resource_access_denies_unassigned_and_supports_revocation(app, kind):
    @app.get("/api/v1/test-resource")
    @login_required
    def resource():
        require_resource_access(kind, "123", allow_admin=False)
        return success({"allowed": True})
    client = app.test_client()
    token = client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"}).json["data"]["access_token"]
    headers = {"Authorization": "Bearer " + token}
    assert client.get("/api/v1/test-resource", headers=headers).status_code == 403
    with app.app_context(), transaction() as db:
        uid = db.scalar(select(User.id).where(User.login_id == "staff1"))
        grant_resource(db, uid, kind, "123")
    assert client.get("/api/v1/test-resource", headers=headers).status_code == 200
    with app.app_context(), transaction() as db:
        db.execute(delete(ResourceGrant).where(ResourceGrant.user_id == uid))
    assert client.get("/api/v1/test-resource", headers=headers).status_code == 403


def test_audit_rolls_back_with_business_change_and_scrubs_secrets(app):
    with app.test_request_context():
        g.user = None
        with pytest.raises(RuntimeError), transaction() as db:
            audit("test.rollback", "task", "3", {"password": "sensitive"})
            raise RuntimeError()
        assert get_db().scalar(select(AuditEvent).where(AuditEvent.action == "test.rollback")) is None
        with transaction():
            audit("test.scrub", "task", "3", {"password": "sensitive", "nested": {"access_token": "secret"}})
        row = get_db().scalar(select(AuditEvent).where(AuditEvent.action == "test.scrub"))
        assert "sensitive" not in row.details_json and '"secret"' not in row.details_json
