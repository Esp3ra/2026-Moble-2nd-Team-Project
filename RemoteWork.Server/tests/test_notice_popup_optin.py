"""Regression: popup registration and DDL never leak into ordinary notice setup."""
import pytest
from sqlalchemy import inspect, select, func
from remotework import create_app
from remotework.db import init_schema, transaction
from remotework.auth import create_user
from remotework.models import Base


def config(url, popup=False, notice=True):
    return {"TESTING": True, "SECRET_KEY": "popup-test-" + "x" * 40,
            "DATABASE_URL": url, "ENABLE_NOTICE_DRAFT": notice,
            "ENABLE_NOTICE_POPUP_DRAFT": popup, "ENABLE_CHAT_DRAFT": False,
            "ENABLE_TASK_QUERY_DRAFT": False, "ENABLE_COMMON_WRITE_DRAFT": False}


@pytest.mark.parametrize("notice,popup,expected", [(False,False,False),(False,True,False),(True,False,False),(True,True,True)])
def test_registration_and_common_init_are_independent(tmp_path, notice, popup, expected):
    app = create_app(config("sqlite:///" + (tmp_path / "case.db").as_posix(), popup, notice))
    routes = {str(r) for r in app.url_map.iter_rules()}
    assert ("/api/v1/notices/popups/pending" in routes) == expected
    assert "notice_confirmations" not in Base.metadata.tables
    with app.app_context():
        init_schema()
        assert not inspect(app.extensions["db_engine"]).has_table("notice_confirmations")
    if notice:
        assert app.test_cli_runner().invoke(args=["notices", "init-table"]).exit_code == 0
        assert not inspect(app.extensions["db_engine"]).has_table("notice_confirmations")


def test_explicit_config_false_overrides_environment(monkeypatch, tmp_path):
    monkeypatch.setenv("REMOTEWORK_ENABLE_NOTICE_POPUP_DRAFT", "1")
    app = create_app(config("sqlite:///" + (tmp_path / "off.db").as_posix()))
    assert "notice_popups" not in app.blueprints
    assert app.test_cli_runner().invoke(args=["notice-popups", "init-table"]).exit_code != 0


def test_same_process_on_then_off_keeps_common_metadata_clean(tmp_path):
    url = "sqlite:///" + (tmp_path / "switch.db").as_posix()
    on = create_app(config(url, True))
    assert "notice_popups" in on.blueprints
    off = create_app(config(url, False))
    assert "notice_popups" not in off.blueprints
    with off.app_context(): init_schema()
    assert not inspect(off.extensions["db_engine"]).has_table("notice_confirmations")
    again = create_app(config(url, True))
    assert "notice_popups" in again.blueprints
    with again.app_context(): init_schema()
    assert not inspect(again.extensions["db_engine"]).has_table("notice_confirmations")


@pytest.fixture
def popup_app(tmp_path):
    app = create_app(config("sqlite:///" + (tmp_path / "popup.db").as_posix(), True))
    with app.app_context():
        init_schema()
        with transaction() as db:
            for login, role, dept in [("admin1","admin","운영팀"),("staff1","employee","개발팀"),("staff2","employee","디자인팀")]:
                create_user(db, {"login_id":login,"password":"popup-test-pass","display_name":login,
                    "employee_no":login,"department":dept,"role":role})
    return app


def headers(client, login):
    r = client.post("/api/v1/auth/login", json={"login_id":login,"password":"popup-test-pass"})
    assert r.status_code == 200
    return {"Authorization":"Bearer " + r.json["data"]["access_token"]}


def add(client, h, target="전 직원", pinned=True):
    r = client.post("/api/v1/notices", headers=h, json={"title":"공지", "content":"내용", "target":target,"is_pinned":pinned})
    assert r.status_code == 201
    assert "popup_key" not in r.json["data"]["notice"]
    return r.json["data"]["notice"]["id"]


def test_request_never_creates_schema_and_explicit_command_preserves_rows(popup_app):
    c = popup_app.test_client(); a = headers(c,"admin1"); e = headers(c,"staff1")
    nid = add(c,a)
    r = c.get("/api/v1/notices/popups/pending",headers=e)
    assert r.status_code == 503 and r.json["error"]["code"] == "NOTICE_POPUP_SCHEMA_REQUIRED"
    assert not inspect(popup_app.extensions["db_engine"]).has_table("notice_confirmations")
    assert popup_app.test_cli_runner().invoke(args=["notice-popups","init-table"]).exit_code == 0
    assert c.get(f"/api/v1/notices/{nid}",headers=e).status_code == 200
    assert popup_app.test_cli_runner().invoke(args=["notice-popups","init-table"]).exit_code == 0


def test_targets_permissions_and_single_confirmation_effect(popup_app):
    c=popup_app.test_client();a=headers(c,"admin1");e=headers(c,"staff1")
    assert popup_app.test_cli_runner().invoke(args=["notice-popups","init-table"]).exit_code == 0
    all_id=add(c,a); own_id=add(c,a,"개발팀");other_id=add(c,a,"디자인팀");plain_id=add(c,a,pinned=False)
    assert c.get("/api/v1/notices/popups/pending").status_code == 401
    assert c.get("/api/v1/notices/popups/pending",headers=a).status_code == 403
    items=c.get("/api/v1/notices/popups/pending",headers=e).json["data"]["items"]
    assert [n["id"] for n in items] == [all_id,own_id]
    key=items[0]["popup_key"]
    assert c.post(f"/api/v1/notices/{all_id}/confirm",headers=e,json={"popup_key":"old"}).status_code == 409
    for _ in range(2):
        assert c.post(f"/api/v1/notices/{all_id}/confirm",headers=e,json={"popup_key":key}).status_code == 200
    with popup_app.app_context():
        from remotework.features.notices.popup_models import NoticeConfirmation
        from remotework.db import get_db
        assert get_db().scalar(select(func.count()).select_from(NoticeConfirmation)) == 1
    assert [n["id"] for n in c.get("/api/v1/notices/popups/pending",headers=e).json["data"]["items"]] == [own_id]
    for nid in (other_id,plain_id):
        assert c.post(f"/api/v1/notices/{nid}/confirm",headers=e,json={"popup_key":"unused"}).status_code == 403


def test_missing_parent_and_incompatible_table_fail_without_rewriting(tmp_path):
    from sqlalchemy import text
    app=create_app(config("sqlite:///"+(tmp_path/"bad.db").as_posix(),True))
    runner=app.test_cli_runner()
    assert runner.invoke(args=["notice-popups","init-table"]).exit_code != 0
    assert inspect(app.extensions["db_engine"]).get_table_names() == []
    with app.app_context(): init_schema()
    with app.extensions["db_engine"].begin() as c:
        c.execute(text("CREATE TABLE notice_confirmations (legacy_id INTEGER PRIMARY KEY)"))
    assert runner.invoke(args=["notice-popups","init-table"]).exit_code != 0
    assert [c["name"] for c in inspect(app.extensions["db_engine"]).get_columns("notice_confirmations")] == ["legacy_id"]


def test_disabled_pending_does_not_hide_deleted_confirmation_error(popup_app):
    c=popup_app.test_client(); a=headers(c,"admin1");e=headers(c,"staff1")
    assert popup_app.test_cli_runner().invoke(args=["notice-popups","init-table"]).exit_code == 0
    one=add(c,a); two=add(c,a)
    items=c.get("/api/v1/notices/popups/pending",headers=e).json["data"]["items"]
    assert c.post(f"/api/v1/notices/{one}/delete",headers=a,json={}).status_code == 200
    missing=c.post(f"/api/v1/notices/{one}/confirm",headers=e,json={"popup_key":items[0]["popup_key"]})
    assert missing.status_code == 404 and missing.json["error"]["code"] == "NOTICE_NOT_FOUND"
    later=c.get("/api/v1/notices/popups/pending",headers=e)
    assert later.status_code == 200 and [n["id"] for n in later.json["data"]["items"]] == [two]


@pytest.mark.parametrize("change,status", [("role",403),("inactive",401),("department",403),("expired",401)])
def test_confirmation_rechecks_actor_after_request_identity_was_loaded(popup_app, change, status):
    from sqlalchemy import text
    c=popup_app.test_client(); a=headers(c,"admin1");e=headers(c,"staff1")
    assert popup_app.test_cli_runner().invoke(args=["notice-popups","init-table"]).exit_code == 0
    nid=add(c,a,"개발팀")
    key=c.get("/api/v1/notices/popups/pending",headers=e).json["data"]["items"][0]["popup_key"]
    changed=False
    def concurrent_authority_change():
        nonlocal changed
        from flask import request
        if not changed and request.path.endswith("/confirm"):
            changed=True
            # A second connection changes authoritative state after load_identity cached it.
            with popup_app.extensions["db_engine"].begin() as db:
                if change=="role": db.execute(text("UPDATE users SET role='admin' WHERE login_id='staff1'"))
                elif change=="inactive": db.execute(text("UPDATE users SET is_active=0 WHERE login_id='staff1'"))
                elif change=="department": db.execute(text("UPDATE users SET department='디자인팀' WHERE login_id='staff1'"))
                else: db.execute(text("UPDATE auth_sessions SET expires_at='2000-01-01 00:00:00' WHERE user_id=(SELECT id FROM users WHERE login_id='staff1')"))
    popup_app.before_request_funcs[None].append(concurrent_authority_change)
    result=c.post(f"/api/v1/notices/{nid}/confirm",headers=e,json={"popup_key":key})
    assert result.status_code==status
    with popup_app.extensions["db_engine"].connect() as db:
        assert db.execute(text("SELECT COUNT(*) FROM notice_confirmations")).scalar()==0
