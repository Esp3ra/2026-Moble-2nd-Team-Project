"""Owner 2 HTML preparation for existing task management mutations.

Draft forms contain no write key. Their signed current-login snapshot keeps the
original versions across candidate searches; W1 receives a key only after an
explicit, currently authorized preparation request.
"""
import hmac

from flask import current_app, g, make_response, render_template, request, url_for
from itsdangerous import BadData, URLSafeSerializer
from sqlalchemy import select, text

from ...auth import authenticated_transaction, login_required
from ...common import AppError
from ...user_candidates import CandidateScope, list_user_candidates
from ...write_contract import ExpectedVersions, lock_users, positive_bigint
from .models import TaskAssignment, TaskManager, TaskRecord
from .queries import _summary
from .web_writes import _authorize, _binding, _form, _id, _page, _prepare


ACTIONS = {"edit": "task.updated", "reassign": "task.reassigned", "managers": "task.managers_changed"}
LABELS = {"edit": "업무 수정", "reassign": "업무 재배정", "managers": "관리자 변경"}
EDITABLE = {"edit": {"title", "description", "start_date", "due_date"},
            "reassign": {"assignee_id"}, "managers": {"manager_user_ids"}}


def _current(db, target):
    """Read display and permission from the same current locked relationships."""
    task_id = positive_bigint(target, "task_id")
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id)
        .with_for_update().execution_options(populate_existing=True))
    if task is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == task_id,
        TaskAssignment.revision == task.current_assignment_revision)
        .with_for_update().execution_options(populate_existing=True))
    managers = list(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == task_id)
        .order_by(TaskManager.user_id).with_for_update()))
    managed = g.user.role == "admin" and g.user.id in managers
    own = assignment is not None and assignment.assignee_id == g.user.id
    if not (own or managed):
        raise AppError(403, "FORBIDDEN", "이 업무를 조회할 권한이 없습니다.")
    if assignment is None:
        raise AppError(503, "TASK_RELATION_INCOMPLETE", "현재 업무 배정의 정합성을 확인해 주세요.")
    users = lock_users(db, [assignment.assignee_id, *managers])
    if assignment.assignee_id not in users:
        raise AppError(503, "TASK_RELATION_INCOMPLETE", "현재 업무 담당자의 정합성을 확인해 주세요.")
    return task, assignment, managers, users, managed


def _cancelled(db, task_id):
    # Persisted markers remain authoritative even with lifecycle routes off.
    # No optional lifecycle ORM metadata is imported by this read adapter.
    from .lifecycle_support import storage_present
    return storage_present(db) and db.execute(text(
        "SELECT task_id FROM task_cancellations WHERE task_id = :task_id FOR UPDATE"),
        {"task_id": task_id}).first() is not None


def _allowed(task, managed, cancelled):
    if not managed or cancelled:
        return []
    if task.task_status == "not_started":
        return ["edit", "reassign", "managers"]
    return ["managers"] if task.task_status in ("in_progress", "returned") else []


def read_detail(db, target):
    """Return current task details and precisely the existing permitted forms."""
    task, assignment, managers, users, managed = _current(db, target)
    cancelled = _cancelled(db, task.task_id)
    summary = _summary((task, assignment, users[assignment.assignee_id].display_name, cancelled))
    return summary | {"description": task.description}, _allowed(task, managed, cancelled)


def _label(user, user_id):
    if user is None:
        return f"#{user_id} · 사용자 정보 확인 필요"
    role = "관리자" if user.role == "admin" else "직원"
    return f"{user.display_name} · {user.department} · {role} · #{user_id}"


def _snapshot(task, assignment, managers, users):
    return {"title": task.title, "description": task.description,
        "start_date": task.start_date.isoformat(), "due_date": task.due_date.isoformat(),
        "task_status": task.task_status, "assignee_id": assignment.assignee_id,
        "assignee_name": users[assignment.assignee_id].display_name,
        "assignee_label": _label(users[assignment.assignee_id], assignment.assignee_id),
        "manager_user_ids": managers,
        "manager_labels": [_label(users.get(user_id), user_id) for user_id in managers],
        "manager_label_by_id": {str(user_id): _label(users.get(user_id), user_id) for user_id in managers}}


def _signer():
    return URLSafeSerializer(current_app.config["SECRET_KEY"],
                             salt="member2-task-web-management-draft-v1")


def _load_draft(target, action, filters):
    try:
        draft = _signer().loads(request.form.get("draft_ticket", ""))
    except BadData:
        raise AppError(400, "INVALID_INPUT", "업무 변경 화면을 다시 열어 주세요.") from None
    if not isinstance(draft, dict) or set(draft) != {
            "binding", "action", "target", "before", "versions", "filters"}:
        raise AppError(400, "INVALID_INPUT", "업무 변경 내용을 확인해 주세요.")
    if not isinstance(draft["binding"], str) or not hmac.compare_digest(draft["binding"], _binding()):
        raise AppError(403, "FORBIDDEN", "이전 로그인에서 연 업무 변경 화면은 사용할 수 없습니다.")
    if (draft["action"] != action or draft["target"] != target or draft["filters"] != filters
            or not isinstance(draft["before"], dict) or not isinstance(draft["versions"], dict)
            or set(draft["versions"]) != {"expected_row_version", "expected_assignment_revision"}):
        raise AppError(400, "INVALID_INPUT", "업무 변경 대상과 조회 조건을 확인해 주세요.")
    ExpectedVersions.from_body(draft["versions"])
    return draft


def _candidates(db, *, target, action, kind, q="", page=1, selected=None):
    def authorize(session):
        _authorize(session, action, target)
        return CandidateScope(None if selected is None else frozenset(selected))
    return list_user_candidates(db, authorize=authorize, kind=kind, q=q, page=page,
                                limit=20 if selected is None else 100)


def _response(*, draft, fields, action, q, page, filters, error=None,
              choices=None, selected=None, selected_managers=None, managers=None):
    response = make_response(render_template("tasks/manage.html", draft=draft,
        draft_ticket=_signer().dumps(draft), fields=fields, action=action, action_label=LABELS[action],
        q=q, page=page, filters=filters, error=error, choices=choices,
        selected=selected or [], selected_managers=selected_managers or [], managers=managers or [],
        source_url=url_for("task_web.manage", task_id=int(draft["target"]), action=action, **filters)),
        error.status if error else 200)
    response.headers["Cache-Control"] = "no-store"
    response.headers["Referrer-Policy"] = "same-origin"
    return response


@login_required
def manage_task(task_id, action):
    from .management import _locked_current, _unstarted_history, validate_management_body
    from .lifecycle_support import ensure_not_cancelled
    from .web import _filters, _list_args

    if action not in ACTIONS:
        raise AppError(404, "NOT_FOUND", "업무 변경 동작을 찾을 수 없습니다.")
    target, write_action = str(task_id), ACTIONS[action]
    positive_bigint(target, "task_id")
    filters = _list_args(*_filters())
    _form({"csrf_token", "draft_ticket", "op", "q", "page"} | EDITABLE[action],
          repeated=("manager_user_ids",) if action == "managers" else (), query=True)
    if request.method == "GET" and request.form:
        raise AppError(400, "INVALID_INPUT", "업무 변경 조회에는 화면 입력을 전달하지 않습니다.")
    op = request.form.get("op") if request.method == "POST" else "search"
    if op not in ("search", "prepare"):
        raise AppError(400, "INVALID_INPUT", "업무 변경 동작을 확인해 주세요.")
    q, raw_page = request.form.get("q", ""), request.form.get("page", "1")
    if (len(q) > 80 or not raw_page.isascii() or not raw_page.isdecimal()
            or len(raw_page) > 7 or not 1 <= int(raw_page) <= 1_000_000):
        raise AppError(400, "INVALID_INPUT", "후보 검색 조건과 페이지를 확인해 주세요.")
    page, error = int(raw_page), None
    with authenticated_transaction(roles=("admin",)) as db:
        _authorize(db, write_action, target)
        if request.method == "GET":
            task, assignment, manager_ids, users, managed = _current(db, target)
            ensure_not_cancelled(db, task.task_id)
            if action not in _allowed(task, managed, False):
                raise AppError(409, "TASK_STATE_CONFLICT", "현재 업무 상태에서는 변경할 수 없습니다. 다시 조회해 주세요.")
            before = _snapshot(task, assignment, manager_ids, users)
            draft = {"binding": _binding(), "action": action, "target": target,
                "before": before, "versions": {"expected_row_version": str(task.row_version),
                    "expected_assignment_revision": task.current_assignment_revision}, "filters": filters}
            fields = {name: before[name] for name in EDITABLE[action]}
            if action == "reassign":
                fields["assignee_id"] = str(fields["assignee_id"])
        else:
            draft = _load_draft(target, action, filters)
            fields = {name: request.form.get(name, "") for name in EDITABLE[action]}
            if action == "managers":
                fields["manager_user_ids"] = [_id(value) for value in request.form.getlist("manager_user_ids")]

        choices, selected, selected_managers, manager_ids = None, [], [], []
        if action == "reassign":
            assignee = None if not fields["assignee_id"] else _id(fields["assignee_id"])
            choices = _candidates(db, target=target, action=write_action, kind="assignment", q=q, page=page)
            selected = _candidates(db, target=target, action=write_action, kind="assignment",
                                   selected=[] if assignee is None else [assignee])["items"]
            if assignee is not None and not selected:
                selected = [{"id": assignee, "selection_label":
                    draft["before"]["assignee_label"] if assignee == draft["before"]["assignee_id"]
                    else f"#{assignee}", "unavailable": True}]
        elif action == "managers":
            manager_ids = fields["manager_user_ids"]
            if len(manager_ids) != len(set(manager_ids)):
                raise AppError(400, "INVALID_INPUT", "업무 관리자는 중복 없이 선택해 주세요.")
            choices = _candidates(db, target=target, action=write_action, kind="manager", q=q, page=page)
            # Preserve every existing relation, including a task created with
            # more selected managers than this screen permits for a new save.
            # A candidate-page limit must never make an active user look absent.
            eligible = []
            for offset in range(0, len(manager_ids), 100):
                eligible.extend(_candidates(db, target=target, action=write_action, kind="manager",
                                            selected=manager_ids[offset:offset + 100])["items"])
            by_id = {item["id"]: item for item in eligible}
            selected_managers = [by_id.get(user_id) or {"id": user_id, "selection_label":
                draft["before"]["manager_label_by_id"].get(str(user_id), f"#{user_id}"), "unavailable": True}
                for user_id in manager_ids]

        if request.method == "POST" and op == "prepare":
            body = fields | draft["versions"]
            if action == "reassign":
                body["assignee_id"] = assignee
            try:
                validate_management_body(write_action, body)
                if action == "managers" and len(manager_ids) > 100:
                    raise AppError(400, "INVALID_INPUT", "화면에서 업무 관리자는 100명 이내로 선택해 주세요.")
                task, assignment = _locked_current(db, target, ExpectedVersions.from_body(body), write_action)
                ensure_not_cancelled(db, task.task_id)
                if action in ("edit", "reassign"):
                    _unstarted_history(db, task)
                if action == "reassign" and (not selected or selected[0].get("unavailable")):
                    raise AppError(409, "ASSIGNEE_UNAVAILABLE", "선택한 담당자를 현재 배정할 수 없습니다.")
                if action == "managers" and any(item.get("unavailable") for item in selected_managers):
                    raise AppError(409, "TASK_MANAGER_UNAVAILABLE", "사용할 수 없는 관리자는 해제한 뒤 다시 선택해 주세요.")
            except AppError as exc:
                error = exc
            else:
                summary = {name: body.get(name, draft["before"][name])
                           for name in ("title", "description", "start_date", "due_date")}
                summary["before"] = draft["before"]
                if action == "reassign":
                    summary["assignee_label"] = selected[0]["selection_label"]
                elif action == "managers":
                    summary["manager_labels"] = [item["selection_label"] for item in selected_managers]
                intent = _prepare(db, action=write_action, target=target, body=body,
                                  summary=summary, filters=filters)
                return _page(intent)
    return _response(draft=draft, fields=fields, action=action, q=q, page=page, filters=filters,
        error=error, choices=choices, selected=selected, selected_managers=selected_managers, managers=manager_ids)
