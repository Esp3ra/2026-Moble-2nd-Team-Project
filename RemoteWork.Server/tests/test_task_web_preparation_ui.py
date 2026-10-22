"""Owner2 templates only: no app fixture, database, account or HTTP transport.

These checks render the actual Jinja sources with explicit display values. They
do not stand in for browser interaction or the existing owned-MySQL web suite.
"""
from html.parser import HTMLParser
from pathlib import Path
from types import SimpleNamespace
from urllib.parse import urlencode

from jinja2 import Environment, FileSystemLoader, StrictUndefined, select_autoescape
import pytest


TEMPLATES = Path(__file__).resolve().parents[1] / "remotework" / "templates"


def render(name, **values):
    environment = Environment(loader=FileSystemLoader(TEMPLATES),
        autoescape=select_autoescape(("html",)), undefined=StrictUndefined)
    defaults = dict(company_name="테스트 회사", g=SimpleNamespace(
        user=SimpleNamespace(id=1, role="admin", display_name="관리자", department="개발팀", employee_no="A1"),
        request_id="template-only-request"), csrf_token=lambda: "template-only-csrf",
        url_for=lambda endpoint, **query: "/" + endpoint.replace(".", "/") + (
            "?" + urlencode(query) if query else ""), filters=dict(scope="managed", status="not_started", include_cancelled="false"),
        error=None, page=1, managers=[], selected_assignees=[], selected_managers=[],
        assignees=dict(items=[], total=0), manager_choices=dict(items=[], total=0),
        draft=dict(title="본문 <script>bad()</script>", description="설명 & 원문", start_date="2026-10-22", due_date="2026-10-23", q="", assignee_id=""))
    defaults.update(values)
    return environment.get_template(name).render(**defaults)


class Elements(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.elements = []
        self.feed(source)

    def handle_starttag(self, tag, attrs):
        self.elements.append((tag, dict(attrs)))

    def matching(self, tag, **attrs):
        return [values for kind, values in self.elements if kind == tag and
                all(values.get(key.replace("_", "-")) == value for key, value in attrs.items())]

    def names(self):
        return {attrs["name"] for tag, attrs in self.elements if tag in ("input", "button", "select", "textarea")
                and attrs.get("name")}


def management(action="edit"):
    fields = dict(title="변경 <script>bad()</script>", description="새 설명 & 원문", start_date="2026-10-22", due_date="2026-10-23", assignee_id="2")
    before = dict(title="기존 업무", description="기존 설명", start_date="2026-10-22", due_date="2026-10-23", assignee_label="직원", assignee_name="직원", manager_labels=["관리자"])
    return dict(draft=dict(target="101", before=before), fields=fields, action=action,
        action_label={"edit":"업무 수정", "reassign":"업무 재배정", "managers":"관리자 변경"}[action],
        source_url="/tasks/101/manage/" + action, draft_ticket="signed-display-fixture", q="",
        choices=dict(items=[], total=0), selected=[])


@pytest.mark.parametrize("name,values,names", [
    ("tasks/new.html", {}, {"csrf_token", "title", "description", "start_date", "due_date", "q", "page", "assignee_id", "op"}),
    ("tasks/manage.html", management(), {"csrf_token", "draft_ticket", "title", "description", "start_date", "due_date", "op"}),
])
def test_local_picker_never_becomes_a_post_field(name, values, names):
    source = render(name, **values)
    elements = Elements(source)
    files = elements.matching("input", type="file")
    assert len(files) == 1
    assert "name" not in files[0]
    assert "disabled" in files[0], "file picking remains unavailable without its local JS guard"
    assert "hidden" in files[0], "native empty-selection label must not contradict the retained local list"
    choose = [attrs for attrs in elements.matching("button", type="button") if "data-file-choose" in attrs]
    assert len(choose) == 1 and "disabled" in choose[0]
    assert "name" not in choose[0] and "aria-describedby" in choose[0]
    assert elements.names() == names
    assert all(form.get("enctype") != "multipart/form-data" for form in elements.matching("form"))
    assert "attachment_ids" not in elements.names()
    assert "<script>bad()</script>" not in source
    assert "&lt;script&gt;bad()&lt;/script&gt;" in source
    assert any("task_file_preparation.js" in script.get("src", "") and "defer" in script for script in elements.matching("script"))


@pytest.mark.parametrize("action", ["reassign", "managers"])
def test_non_edit_management_does_not_offer_attachment_preparation(action):
    elements = Elements(render("tasks/manage.html", **management(action)))
    assert not elements.matching("input", type="file")
    assert "attachment_ids" not in elements.names()


def confirmation(**values):
    intent = dict(action="task.created", phase="prepared", target="new",
        summary=dict(title="업무 제목", description="본문", start_date="2026-10-22", due_date="2026-10-23",
            assignee_label="직원", creator_label="관리자", manager_labels=[]), body={}, filters={})
    defaults = dict(intent=intent, ticket="unchanged-ticket", action_label="업무 등록", back_url="/tasks",
        completed=None, self_removed=False, can_open_detail=False, not_recorded=False,
        access_lost=False, management=False, lifecycle=False, can_execute=True)
    defaults.update(values)
    return defaults


def test_success_page_keeps_success_and_has_no_execute_or_picker():
    source = render("tasks/write_confirmation.html", **confirmation(
        completed=dict(task_id="101"), can_open_detail=True,
        error=SimpleNamespace(status=503, code="DB_UNAVAILABLE", message="후속 조회 오류")))
    elements = Elements(source)
    assert "요청이 저장되었습니다" in source
    assert "후속 조회 오류" not in source
    assert not elements.matching("input", type="file")
    assert not any("write_execute" in form.get("action", "") for form in elements.matching("form"))


def test_recovery_does_not_offer_execute_and_keeps_original_ticket():
    values = confirmation()
    values["intent"]["phase"] = "recovery"
    source = render("tasks/write_confirmation.html", **values,
        error=SimpleNamespace(status=503, code="DB_UNAVAILABLE", message="연결 확인 필요"))
    elements = Elements(source)
    assert "처리 결과 확인" in source
    assert not any("write_execute" in form.get("action", "") for form in elements.matching("form"))
    assert elements.matching("input", name="ticket", value="unchanged-ticket")
    assert "저장 성공이나 실패를 판단하지 않습니다" in source


def test_access_lost_hides_original_task_details_and_picker():
    values = confirmation(access_lost=True)
    values["intent"]["phase"] = "recovery"
    source = render("tasks/write_confirmation.html", **values,
        error=SimpleNamespace(status=403, code="FORBIDDEN", message="접근 권한 없음"))
    assert "업무 제목" not in source
    assert not Elements(source).matching("input", type="file")
    assert "template-only-request" in source


@pytest.mark.parametrize("status,code,kind", [
    (401, "AUTH_REQUIRED", "authentication"),
    (403, "FORBIDDEN", "permission"),
    (400, "INVALID_INPUT", "input"),
    (404, "TASK_NOT_FOUND", "missing"),
    (409, "TASK_VERSION_CONFLICT", "conflict"),
    (409, "IDEMPOTENCY_KEY_REUSED", "conflict"),
    (503, "TASK_LIFECYCLE_INTEGRATION_REQUIRED", "unavailable"),
    (500, "INTERNAL_ERROR", "server"),
    (409, "RESULT_CHECK_REQUIRED", "recovery"),
])
def test_error_categories_preserve_server_message_and_request_id(status, code, kind):
    message = "서버 안내 <script>bad()</script> & 원문"
    source = render("tasks/_write_error.html", error=SimpleNamespace(status=status, code=code, message=message))
    assert Elements(source).matching("section", data_error_kind=kind, role="alert")
    assert code in source and "template-only-request" in source
    assert "서버 안내 &lt;script&gt;bad()&lt;/script&gt; &amp; 원문" in source
    assert "<script>bad()</script>" not in source
