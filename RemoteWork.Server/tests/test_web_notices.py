"""Web cookies + CSRF, shared notice operations and existing API compatibility."""
import re
import pytest
from sqlalchemy import func, select
from sqlalchemy.exc import SQLAlchemyError
from remotework.db import transaction
from remotework.models import AuditEvent, User
from remotework.features.notices.models import Notice


@pytest.fixture(autouse=True)
def notice_on(monkeypatch):
    monkeypatch.setenv("REMOTEWORK_ENABLE_NOTICE_DRAFT", "1")


def csrf(client):
    with client.session_transaction() as session:
        return session["csrf_token"]


def web_login(client, admin=True):
    response = client.get("/login")
    token = re.search(r'name="csrf_token" value="([^"]+)"', response.text).group(1)
    result = client.post("/login", data={"csrf_token": token, "login_id": "admin1" if admin else "staff1",
                                       "password": "Admin-test-2026!" if admin else "Staff-test-2026!"})
    assert result.status_code == 302
    return csrf(client)


def fields(token, **overrides):
    return {"csrf_token": token, "title": "웹 공지", "content": "웹 본문\n두 번째 줄", "target": "전 직원",
            "attachment_names": "일정.pdf\n안내.xlsx", **overrides}


def count(app):
    with app.app_context(), transaction() as db:
        return db.scalar(select(func.count()).select_from(Notice))


def test_login_required_and_cookie_does_not_authenticate_api(client):
    assert client.get("/notices").status_code == 302
    assert client.get("/features/notices").status_code == 302
    web_login(client)
    assert client.get("/features/notices").location.endswith("/notices")
    assert client.get("/notices").status_code == 200
    assert client.get("/api/v1/notices").status_code == 401


def test_web_write_api_read_and_api_write_web_read(app, client, login):
    token = web_login(client)
    created = client.post("/notices/new", data=fields(token, is_pinned="1", author_id="9999", author="가짜 작성자"))
    assert created.status_code == 303
    detail = client.get(created.location.replace("choice=", "selected="))
    assert "웹 본문" in detail.text and "일정.pdf" in detail.text
    assert "다운로드는 아직 지원하지 않습니다" in detail.text
    headers, _ = login("admin1", "Admin-test-2026!")
    rows = client.get("/api/v1/notices", headers=headers).json["data"]["items"]
    assert len(rows) == 1 and rows[0]["author"] == "관리자" and rows[0]["is_pinned"]
    assert rows[0]["attachment_names"] == ["일정.pdf", "안내.xlsx"]
    api = client.post("/api/v1/notices", headers=headers, json={"title": "프로그램 작성 공지", "content": "API 본문",
                      "target": "디자인팀", "is_pinned": False, "attachment_names": []})
    assert api.status_code == 201
    assert "프로그램 작성 공지" in client.get("/notices").text


def test_employee_can_read_all_departments_but_cannot_write(app, client, login):
    headers, _ = login("admin1", "Admin-test-2026!")
    created = client.post("/api/v1/notices", headers=headers, json={"title": "기획팀 소식", "content": "기획팀 본문",
                          "target": "기획팀", "is_pinned": False, "attachment_names": []})
    notice_id = created.json["data"]["notice"]["id"]
    token = web_login(client, False)
    page = client.get(f"/notices?target=기획팀&selected={notice_id}")
    assert page.status_code == 200 and "기획팀 본문" in page.text
    assert 'href="/notices/new"' not in page.text
    assert f'href="/notices/{notice_id}/delete"' not in page.text
    for url in ["/notices/new", f"/notices/{notice_id}/delete"]:
        assert client.get(url).status_code == 403
        assert client.post(url, data=fields(token)).status_code == 403
    assert count(app) == 1


@pytest.mark.parametrize("token", [None, "wrong-token"])
def test_csrf_rejects_create_and_delete(app, client, token):
    good = web_login(client)
    created = client.post("/notices/new", data=fields(good))
    notice_id = int(created.location.split("choice=")[1])
    data = fields(token)
    if token is None:
        data.pop("csrf_token")
    assert client.post("/notices/new", data=data).status_code == 400
    assert client.post(f"/notices/{notice_id}/delete", data=data).status_code == 400
    assert count(app) == 1


@pytest.mark.parametrize("changes", [
    {"title": " "}, {"title": "가" * 201}, {"content": "가" * 5001}, {"target": "없는팀"},
    {"attachment_names": "C:\\private\\report.pdf"}, {"attachment_names": "\n".join(f"{i}.pdf" for i in range(21))}
])
def test_invalid_form_keeps_input(app, client, changes):
    token = web_login(client)
    result = client.post("/notices/new", data=fields(token, **changes), content_type="multipart/form-data")
    assert result.status_code == 400
    assert 'role="alert"' in result.text
    assert "웹 본문" in result.text or "가" * 30 in result.text
    assert count(app) == 0


def test_xss_escaped_and_newlines_preserved(client):
    token = web_login(client)
    result = client.post("/notices/new", data=fields(token, title='<script>alert("x")</script>',
                         content="<img src=x onerror=alert(1)>\n다음 줄"))
    page = client.get(result.location.replace("choice=", "selected="))
    assert page.status_code == 200
    assert "<script>" not in page.text and "<img src=x" not in page.text
    assert "&lt;script&gt;" in page.text and "&lt;img" in page.text and "다음 줄" in page.text


def test_delete_confirmation_get_does_not_delete_and_post_is_shared(app, client, login):
    token = web_login(client)
    result = client.post("/notices/new", data=fields(token))
    notice_id = int(result.location.split("choice=")[1])
    assert client.get(f"/notices/{notice_id}/delete").status_code == 200
    assert count(app) == 1
    assert client.post(f"/notices/{notice_id}/delete", data={"csrf_token": token}).status_code == 303
    assert count(app) == 0
    assert client.post(f"/notices/{notice_id}/delete", data={"csrf_token": token}).status_code == 404
    with app.app_context(), transaction() as db:
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action == "notice.deleted")) == 1


def test_shared_audit_failure_rolls_back_and_keeps_web_form(app, client, monkeypatch):
    from remotework.features.notices import routes
    token = web_login(client)
    def fail(*args, **kwargs):
        raise SQLAlchemyError("synthetic audit failure")
    monkeypatch.setattr(routes, "audit", fail)
    result = client.post("/notices/new", data=fields(token))
    assert result.status_code == 503
    assert 'value="웹 공지"' in result.text and "웹 본문" in result.text
    assert "재등록하기 전에" in result.text
    assert count(app) == 0


def test_filter_pagination_and_pinned_stable_sort(app, client):
    from datetime import datetime, timedelta
    with app.app_context(), transaction() as db:
        user = db.scalar(select(User).where(User.login_id == "admin1"))
        for i in range(24):
            db.add(Notice(title=f"제목{i:02}", content=f"본문{i}", target="개발팀" if i % 2 else "기획팀",
                          author_id=user.id, author=user.display_name, is_pinned=i in (0, 1),
                          created_at=datetime(2026, 10, 19) + timedelta(minutes=i)))
    web_login(client)
    first = client.get("/notices?sort=title&direction=asc")
    assert "총 24건" in first.text and "다음" in first.text
    assert first.text.index("제목01") < first.text.index("제목00") < first.text.index("제목02")
    assert "제목23" in client.get("/notices?page=2&sort=title&direction=asc").text
    filtered = client.get("/notices?target=개발팀")
    assert "총 12건" in filtered.text and "제목00" not in filtered.text
    assert client.get("/notices?sort=bogus").status_code == 400
    assert client.get("/notices?page=0").status_code == 400
    assert client.get("/notices?selected=2147483648").status_code == 404


def test_record_survives_new_web_client(app, client):
    token = web_login(client)
    result = client.post("/notices/new", data=fields(token))
    other = app.test_client()
    web_login(other, False)
    assert "웹 본문" in other.get(result.location.replace("choice=", "selected=")).text


def test_maximum_korean_body_uses_multipart_without_raising_global_size_limit(client):
    token = web_login(client)
    result = client.post("/notices/new", data=fields(token, content="한" * 5000), content_type="multipart/form-data")
    assert result.status_code == 303
    assert "한" * 5000 in client.get(result.location.replace("choice=", "selected=")).text


def test_cold_feature_off_does_not_import_notice_models_or_register_web_writes():
    import json, os, subprocess, sys
    env=os.environ.copy()
    env["PYTHON_DOTENV_DISABLED"]="1"
    env["REMOTEWORK_ENABLE_NOTICE_DRAFT"]="0"
    result=subprocess.run([sys.executable,"-c",'''import json
from remotework import create_app
from remotework.models import Base
app=create_app({'SECRET_KEY':'cold-feature-test-'+'x'*40,'DATABASE_URL':'sqlite:///:memory:','ENABLE_NOTICE_DRAFT':False,'ENABLE_CHAT_DRAFT':False})
print(json.dumps({'models':list(Base.metadata.tables),'routes':[str(r) for r in app.url_map.iter_rules()]}))
'''],env=env,capture_output=True,text=True,encoding="utf-8",timeout=25)
    assert result.returncode==0,result.stderr
    data=json.loads(result.stdout)
    assert "notices" not in data["models"] and "notice_confirmations" not in data["models"]
    assert "/notices/new" not in data["routes"] and "/api/v1/notices" not in data["routes"]


def test_row_choice_does_not_show_detail_until_query(client):
    token = web_login(client)
    saved = client.post("/notices/new", data=fields(token, content="조회 버튼 전에는 보이지 않는 본문"))
    assert "choice=" in saved.location
    notice_id = int(saved.location.split("choice=")[1])
    chosen = client.get(f"/notices?choice={notice_id}")
    assert chosen.status_code == 200
    assert 'id="notice-choice" value="'+str(notice_id)+'"' in chosen.text
    assert "조회 버튼을 눌러 주세요" in chosen.text
    assert "조회 버튼 전에는 보이지 않는 본문" not in chosen.text
    detail = client.get(f"/notices?selected={notice_id}")
    assert "조회 버튼 전에는 보이지 않는 본문" in detail.text
    assert 'id="deadline-heading"' in detail.text
    assert "업무 마감 알림은 아직 연결되지 않았습니다" in detail.text


def test_stale_selection_does_not_load_another_department(client):
    token = web_login(client)
    first=client.post("/notices/new",data=fields(token,target="기획팀",content="기획 상세"))
    second=client.post("/notices/new",data=fields(token,target="개발팀",content="개발 상세"))
    previous=int(first.location.split("choice=")[1])
    result=client.get(f"/notices?target=개발팀&selected={previous}")
    assert result.status_code==200 and "개발 상세" not in result.text and "기획 상세" not in result.text
    assert "조회 버튼을 눌러 주세요" in result.text


@pytest.mark.parametrize("operation", ["create", "delete"])
def test_real_multipart_upload_rejected_without_mutation(app, client, operation):
    from io import BytesIO
    token = web_login(client)
    saved = client.post("/notices/new", data=fields(token))
    notice_id = int(saved.location.split("choice=")[1])
    url = "/notices/new" if operation == "create" else f"/notices/{notice_id}/delete"
    result = client.post(url, data={**fields(token), "upload": (BytesIO(b"test file"), "test.txt")},
                         content_type="multipart/form-data")
    assert result.status_code == 400 and "실제 파일 업로드" in result.text
    assert count(app) == 1


def test_combined_metadata_over_request_limit_returns_413_without_saving(app, client):
    token = web_login(client)
    data = fields(token, content="😀" * 5000,
                  attachment_names="\n".join("가" * 245 + f"{i:02}.pdf" for i in range(20)))
    result = client.post("/notices/new", data=data, content_type="multipart/form-data")
    assert result.status_code == 413 and count(app) == 0


@pytest.mark.parametrize("change", ["role", "inactive", "expired"])
def test_current_web_authority_checked_before_write(app, client, change):
    from datetime import timedelta
    from remotework.models import AuthSession, utcnow
    token = web_login(client)
    with app.app_context(), transaction() as db:
        actor = db.scalar(select(User).where(User.login_id == "admin1"))
        if change == "role":
            actor.role = "employee"
        elif change == "inactive":
            actor.is_active = False
        else:
            auth = db.scalar(select(AuthSession).where(AuthSession.user_id == actor.id, AuthSession.channel == "web"))
            auth.expires_at = utcnow() - timedelta(seconds=1)
    result = client.post("/notices/new", data=fields(token))
    assert result.status_code == (403 if change == "role" else 302)
    assert count(app) == 0


def test_api_defaults_and_reads_have_no_write_side_effect(app, client, login):
    headers, _ = login("admin1", "Admin-test-2026!")
    token = web_login(client)
    saved = client.post("/notices/new", data=fields(token))
    notice_id = int(saved.location.split("choice=")[1])
    api = client.get("/api/v1/notices", headers=headers).json
    assert set(api["data"]) == {"items", "page", "limit", "total"}
    assert api["data"]["page"] == 1 and api["data"]["limit"] == 100
    for suffix in ["?choice=", "?selected="]:
        assert client.get("/notices" + suffix + str(notice_id)).status_code == 200
    with app.app_context(), transaction() as db:
        assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action.like("notice.%"))) == 1


@pytest.mark.parametrize("notice,task,chat,explicit_off", [
    (False,False,False,False),(False,True,True,False),
    (True,False,False,False),(True,True,False,False),
    (True,False,True,False),(True,True,True,False),
    (True,True,True,True),
])
def test_cold_registration_combinations_preserve_other_features(notice, task, chat, explicit_off):
    import json, os, subprocess, sys
    env = os.environ.copy()
    env.update(PYTHON_DOTENV_DISABLED="1", REMOTEWORK_ENABLE_NOTICE_DRAFT=str(int(notice)))
    config = {"SECRET_KEY":"cold-feature-test-"+"x"*40,"DATABASE_URL":"sqlite:///:memory:",
              "ENABLE_TASK_QUERY_DRAFT":task,"ENABLE_CHAT_DRAFT":chat}
    if explicit_off:
        config["ENABLE_NOTICE_DRAFT"] = False
    script = "from remotework import create_app;from remotework.models import Base;import json;" + \
             "app=create_app(" + repr(config) + ");" + \
             "print(json.dumps({'routes':[str(r) for r in app.url_map.iter_rules()],'models':list(Base.metadata.tables)}))"
    run = subprocess.run([sys.executable,"-c",script],env=env,capture_output=True,text=True,encoding="utf-8",timeout=25)
    assert run.returncode == 0, run.stderr
    data = json.loads(run.stdout)
    assert ("/notices/new" in data["routes"]) == (notice and not explicit_off)
    assert ("notices" in data["models"]) == (notice and not explicit_off)
    assert ("/api/v1/tasks" in data["routes"]) == task
    assert ("/api/v1/chat/rooms" in data["routes"]) == chat
    assert "notice_confirmations" not in data["models"]


class NoticePageParser(__import__("html.parser", fromlist=["HTMLParser"]).HTMLParser):
    """Read real rendered controls rather than supplying missing query fields."""
    def __init__(self, html):
        super().__init__()
        self.form = {}; self.action = None; self.in_form = False
        self.select = None; self.link = None; self.links = {}; self.feed(html)
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "form" and attrs.get("class") == "notice-toolbar":
            self.in_form = True; self.action = attrs["action"]
        if self.in_form and tag == "input" and attrs.get("name"):
            self.form[attrs["name"]] = attrs.get("value", "")
        if self.in_form and tag == "select": self.select = attrs.get("name")
        if self.select and tag == "option" and ("selected" in attrs or self.select not in self.form):
            self.form[self.select] = attrs.get("value", "")
        if tag == "a": self.link = [attrs.get("href", ""), ""]
    def handle_data(self, data):
        if self.link is not None: self.link[1] += data
    def handle_endtag(self, tag):
        if tag == "form": self.in_form = False
        if tag == "select": self.select = None
        if tag == "a" and self.link is not None:
            self.links[self.link[1].strip()] = self.link[0]; self.link = None


def seed_paged_notices(app, size=60):
    with app.app_context(), transaction() as db:
        actor = db.scalar(select(User).where(User.login_id == "admin1"))
        for i in range(size):
            db.add(Notice(title=f"paged-{i:02}", content=f"unique-body-{i:02}",
                          target="기획팀" if i < 6 else "개발팀", author_id=actor.id,
                          author=actor.display_name, is_pinned=False))


@pytest.mark.parametrize("page,index", [(2,20),(2,30),(2,39),(3,40),(3,50),(3,59)])
def test_rendered_query_form_preserves_page_and_exact_selection(app, client, page, index):
    seed_paged_notices(app)
    web_login(client)
    listing = NoticePageParser(client.get(f"/notices?page={page}&sort=title&direction=asc").text)
    # Following the real row link is also the no-JS/keyboard anchor path.
    selected_page = client.get(listing.links[f"paged-{index:02}"])
    assert "unique-body-" not in selected_page.text
    controls = NoticePageParser(selected_page.text)
    submitted = client.get(controls.action, query_string=controls.form)
    assert f"unique-body-{index:02}" in submitted.text
    assert f"{page} / 3 페이지" in submitted.text
    assert "unique-body-00" not in submitted.text


def test_rendered_filter_change_resets_page_without_loading_another_notice(app, client):
    seed_paged_notices(app)
    web_login(client)
    controls = NoticePageParser(client.get("/notices?page=3&sort=title&direction=asc").text)
    controls.form["target"] = "기획팀"
    result = client.get(controls.action, query_string=controls.form)
    assert result.status_code == 200 and "총 6건" in result.text and "paged-00" in result.text
    assert 'name="page" value="1"' in result.text and "unique-body-" not in result.text


def test_sort_link_resets_page_and_keeps_selection_only(app, client):
    seed_paged_notices(app)
    web_login(client)
    controls = NoticePageParser(client.get("/notices?page=3&sort=title&direction=asc&target=개발팀").text)
    result = client.get(controls.links["제목"])
    assert 'name="page" value="1"' in result.text and "paged-59" in result.text
    assert "unique-body-" not in result.text


def test_deleted_selection_never_displays_replacement_body(app, client):
    seed_paged_notices(app, 21)
    token = web_login(client)
    controls = NoticePageParser(client.get("/notices?page=2&sort=title&direction=asc").text)
    chosen = controls.form["selected"]
    assert client.post(f"/notices/{chosen}/delete", data={"csrf_token":token}).status_code == 303
    result = client.get(controls.action, query_string=controls.form)
    assert result.status_code == 200 and 'name="page" value="1"' in result.text
    assert "unique-body-" not in result.text


def test_empty_list_after_deletion_keeps_detail_empty(app, client):
    seed_paged_notices(app, 1)
    token = web_login(client)
    controls = NoticePageParser(client.get("/notices").text)
    client.post(f"/notices/{controls.form['selected']}/delete", data={"csrf_token":token})
    result = client.get(controls.action, query_string=controls.form)
    assert "조회할 공지사항이 없습니다" in result.text and "unique-body-" not in result.text
