import os
import pytest
from sqlalchemy import delete
from sqlalchemy.engine import make_url
from remotework import create_app
from remotework.auth import create_user
from remotework.db import init_schema, transaction
from remotework.models import Base


@pytest.fixture
def app(tmp_path):
    # MYSQL_TEST_URL must point ONLY at a disposable dedicated test database.
    url = os.getenv("MYSQL_TEST_URL", "sqlite:///" + (tmp_path / "test.db").as_posix())
    if not url.startswith("sqlite") and not (make_url(url).database or "").startswith("remote_work_test_"):
        raise RuntimeError("Refusing to clear a database whose name does not start with remote_work_test_")
    app = create_app({"TESTING": True, "SECRET_KEY": "testing-only-secret-" + "a" * 32,
                      "DATABASE_URL": url, "SESSION_HOURS": 1})
    with app.app_context():
        init_schema()
        with transaction() as db:
            for table in reversed(Base.metadata.sorted_tables):
                if table.name not in ("admin_guard", "schema_versions"):
                    db.execute(delete(table))
            create_user(db, {"login_id": "admin1", "password": "Admin-test-2026!", "display_name": "관리자",
                "employee_no": "A001", "department": "운영팀", "role": "admin"})
            create_user(db, {"login_id": "staff1", "password": "Staff-test-2026!", "display_name": "직원",
                "employee_no": "E001", "department": "개발팀", "role": "employee"})
    yield app
    app.extensions["db_engine"].dispose()


@pytest.fixture
def client(app):
    return app.test_client()


@pytest.fixture
def login(client):
    def perform(user="staff1", password="Staff-test-2026!"):
        response = client.post("/api/v1/auth/login", json={"login_id": user, "password": password})
        assert response.status_code == 200, response.json
        data = response.json["data"]
        return {"Authorization": "Bearer " + data["access_token"]}, data
    return perform
