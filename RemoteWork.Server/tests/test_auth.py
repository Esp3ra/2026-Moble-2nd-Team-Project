import json
from datetime import timedelta
import pytest
from sqlalchemy import select
from remotework import create_app
from remotework.db import get_db, transaction
from remotework.models import User, AuthSession, AuditEvent, utcnow
from remotework.auth import token_digest


def test_health_and_response_contract(client):
    result = client.get("/api/v1/health")
    assert result.status_code == 200
    assert result.json["data"]["database"] == "connected"
    assert result.json["request_id"] == result.headers["X-Request-ID"]


def test_login_me_hash_and_logout(app, client, login):
    headers, data = login()
    assert data["user"]["display_name"] == "직원"
    assert "password_hash" not in data["user"]
    assert client.get("/api/v1/auth/me", headers=headers).json["data"]["user"]["login_id"] == "staff1"
    with app.app_context():
        user = get_db().scalar(select(User).where(User.login_id == "staff1"))
        assert user.password_hash.startswith("scrypt:") and "Staff-test-2026!" not in user.password_hash
        session = get_db().get(AuthSession, token_digest(data["access_token"]))
        assert session and session.token_hash != data["access_token"]
    assert client.post("/api/v1/auth/logout", headers=headers).status_code == 200
    assert client.get("/api/v1/auth/me", headers=headers).status_code == 401


@pytest.mark.parametrize("user,password", [("staff1", "wrong"), ("nobody", "wrong"), ("staff1' OR '1'='1", "wrong")])
def test_bad_credentials_are_generic(client, user, password):
    response = client.post("/api/v1/auth/login", json={"login_id": user, "password": password})
    assert response.status_code == 401
    assert response.json["error"]["code"] == "INVALID_CREDENTIALS"


def test_throttle_persists_in_database(app, client):
    for _ in range(5):
        assert client.post("/api/v1/auth/login", json={"login_id": "staff1", "password": "wrong"}).status_code == 401
    # A new application instance still observes the persisted failures.
    other = create_app({"TESTING": True, "SECRET_KEY": app.config["SECRET_KEY"], "DATABASE_URL": app.config["DATABASE_URL"]})
    assert other.test_client().post("/api/v1/auth/login", json={"login_id": "staff1", "password": "Staff-test-2026!"}).status_code == 429
    other.extensions["db_engine"].dispose()


def test_expiry_and_session_survives_server_restart(app, client, login):
    headers, data = login()
    other = create_app({"TESTING": True, "SECRET_KEY": app.config["SECRET_KEY"], "DATABASE_URL": app.config["DATABASE_URL"]})
    assert other.test_client().get("/api/v1/auth/me", headers=headers).status_code == 200
    other.extensions["db_engine"].dispose()
    with app.app_context(), transaction() as db:
        session = db.get(AuthSession, token_digest(data["access_token"]))
        session.expires_at = utcnow() - timedelta(seconds=1)
    assert client.get("/api/v1/auth/me", headers=headers).status_code == 401


def test_change_password_revokes_all_channels(app, client, login):
    headers1, _ = login()
    headers2, _ = login()
    assert client.post("/api/v1/auth/password", headers=headers1,
        json={"current_password": "wrong", "new_password": "New-password-2026!"}).status_code == 400
    response = client.post("/api/v1/auth/password", headers=headers1,
        json={"current_password": "Staff-test-2026!", "new_password": "New-password-2026!"})
    assert response.status_code == 200
    assert client.get("/api/v1/auth/me", headers=headers1).status_code == 401
    assert client.get("/api/v1/auth/me", headers=headers2).status_code == 401
    login(password="New-password-2026!")


def test_no_bearer_or_wrong_channel(client, login):
    headers, data = login()
    assert client.get("/api/v1/auth/me").status_code == 401
    client.set_cookie("rw_session", data["access_token"])
    assert client.get("/").status_code == 302  # An API bearer cannot become a web session.
    assert client.get("/api/v1/auth/me").status_code == 401  # Cookies cannot authenticate API calls.


def test_json_and_errors(client):
    assert client.post("/api/v1/auth/login", data="x").status_code == 415
    assert client.post("/api/v1/auth/login", json=[]).status_code == 400
    assert client.post("/api/v1/auth/login", data="{", content_type="application/json").status_code == 400
    assert client.post("/api/v1/auth/login", json={"password": "a" * 40000}).status_code == 413
    result = client.get("/api/v1/does-not-exist")
    assert result.status_code == 404 and result.json["ok"] is False


def test_cli_create_user_and_cleanup(app):
    runner = app.test_cli_runner()
    result = runner.invoke(args=["create-user", "--login-id", "staff3", "--display-name", "신입",
        "--employee-no", "E003", "--password", "Staff-three-2026!"])
    assert result.exit_code == 0, result.output
    assert runner.invoke(args=["cleanup-auth"]).exit_code == 0


def test_schema_initialization_preserves_users(app):
    from remotework.db import init_schema
    with app.app_context():
        original = get_db().scalar(select(User).where(User.login_id == "staff1")).id
        init_schema()
        assert get_db().scalar(select(User).where(User.login_id == "staff1")).id == original


def test_missing_schema_version_is_actionable_503(app, client):
    from remotework.models import SchemaVersion
    with app.app_context(), transaction() as db:
        db.delete(db.get(SchemaVersion, 1))
    result = client.get("/api/v1/health")
    assert result.status_code == 503
    assert result.json["error"]["code"] == "DATABASE_UNAVAILABLE"
