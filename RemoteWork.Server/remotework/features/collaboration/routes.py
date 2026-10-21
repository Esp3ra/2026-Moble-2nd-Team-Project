"""Local API/DDL proposal: explicit install, current task bridge and W1 receipts.

No task/assignment/file writes; minimal collaboration-only access for recipients.
"""
from datetime import date, timedelta, timezone
import json
import re
import uuid
from flask import Blueprint, current_app, g, request
from sqlalchemy import select, or_
from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...models import User, iso, utcnow
from ...write_requests import WriteResult, perform_once
from .models import CollaborationRequest as Row, CollaborationEvent as Event
from .storage import require_storage

bp = Blueprint("collaboration_requests", __name__, url_prefix="/api/v1/collaboration-requests")
ACTIONS = {"collaboration.created", "collaboration.accepted", "collaboration.declined",
    "collaboration.withdrawn", "collaboration.completed", "collaboration.left"}
RESULT_FIELDS = {"request_id", "status", "row_version", "updated_at"}
DECISIONS = {"accept": "accepted", "decline": "declined", "withdraw": "withdrawn",
    "complete": "completed", "leave": "left"}
KST = timezone(timedelta(hours=9))


def now():
    clock = current_app.config.get("COLLABORATION_CLOCK", utcnow)
    return clock().replace(microsecond=0)


def integer(value, maximum=9223372036854775807):
    if not isinstance(value, str) or re.fullmatch(r"[1-9][0-9]{0,18}", value) is None or int(value) > maximum:
        raise AppError(400, "INVALID_INPUT", "정규 양의 정수 문자열을 확인해 주세요.")
    return int(value)


def canonical_uuid(value):
    try:
        if not isinstance(value, str) or str(uuid.UUID(value)) != value:
            raise ValueError()
    except (ValueError, AttributeError):
        raise AppError(400, "INVALID_INPUT", "요청 식별자를 확인해 주세요.") from None
    return value


def body(fields):
    def unique(pairs):
        value = {}
        for key, item in pairs:
            if key in value:
                raise ValueError()
            value[key] = item
        return value
    if request.args or not request.is_json:
        raise AppError(400, "INVALID_INPUT", "JSON 입력 항목을 확인해 주세요.")
    try:
        value = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=unique,
            parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
    except (ValueError, UnicodeError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "중복 없는 JSON 입력을 확인해 주세요.") from None
    if not isinstance(value, dict) or set(value) != set(fields):
        raise AppError(400, "INVALID_INPUT", "입력 항목을 확인해 주세요.")
    return value


def task_context(db, task_id, since_version=None):
    value = current_app.extensions["collaboration_task_context"](db, task_id, since_version=since_version)
    required = {"task_id", "title", "assignee_id", "assignment_revision", "row_version",
        "status", "due_date", "cancelled", "continuous_in_progress"}
    if not isinstance(value, dict) or set(value) != required or value["task_id"] != task_id:
        raise AppError(503, "COLLABORATION_TASK_BRIDGE_REQUIRED", "현재 업무 연결 계약을 확인해 주세요.")
    return value


def locked(db, request_id):
    require_storage(db)
    item = db.scalar(select(Row).where(Row.request_id == canonical_uuid(request_id))
        .with_for_update().execution_options(populate_existing=True))
    if item is None or g.user.id not in (item.requester_id, item.recipient_id):
        raise AppError(404, "COLLABORATION_NOT_FOUND", "조회 가능한 협업 요청이 없습니다.")
    if item.ended_at is not None and item.ended_at < now() - timedelta(days=90):
        raise AppError(404, "COLLABORATION_NOT_FOUND", "조회 가능한 협업 요청이 없습니다.")
    return item


def authorize_create(db, target):
    require_storage(db)
    context = task_context(db, integer(target))
    if context["assignee_id"] != g.user.id:
        raise AppError(403, "FORBIDDEN", "본인 담당 업무에만 요청할 수 있습니다.")
    # State/version checks are in mutate, allowing an authorized retained result read.
    return True


def authorize_request(db, target):
    locked(db, target)
    return True


def result(item):
    return {"request_id": item.request_id, "status": item.status,
        "row_version": str(item.row_version), "updated_at": iso(item.updated_at)}


def public(item):
    return result(item) | {"task_id": str(item.task_id), "assignment_revision": item.assignment_revision,
        "requester_id": item.requester_id, "recipient_id": item.recipient_id,
        "requester_name": item.requester_name, "task_title": item.task_title,
        "title": item.title, "request_text": item.request_text,
        "desired_date": item.desired_date.isoformat(), "created_at": iso(item.created_at),
        "end_reason": item.end_reason,
        "is_late": item.status == "accepted" and item.desired_date < now().replace(tzinfo=timezone.utc).astimezone(KST).date()}


def transition(db, item, state, reason=None, *, actor=True):
    stamp = now()
    item.status, item.row_version, item.updated_at = state, item.row_version + 1, stamp
    item.end_reason = reason
    if state not in ("pending", "accepted"):
        item.ended_at = stamp
    db.add(Event(request_id=item.request_id, actor_id=g.user.id if actor else None,
        status=state, reason=reason, occurred_at=stamp))
    audit("collaboration." + state, "collaboration", item.request_id, {"status": state, "reason": reason})
    db.flush()


def reconcile(db, item, context):
    if item.status not in ("pending", "accepted"):
        return
    if context["cancelled"] or context["status"] != "in_progress" or context["assignee_id"] != item.requester_id or context["assignment_revision"] != item.assignment_revision:
        transition(db, item, "ended", "source_task_changed", actor=False)
    elif context["continuous_in_progress"] is False:
        transition(db, item, "ended", "source_task_interrupted", actor=False)
    elif item.status == "pending" and item.desired_date < now().replace(tzinfo=timezone.utc).astimezone(KST).date():
        transition(db, item, "expired", "desired_date_elapsed", actor=False)


@bp.get("/task-context/<task_id>")
@login_required
def preparation(task_id):
    with authenticated_transaction() as db:
        value = task_context(db, integer(task_id))
        if value["assignee_id"] != g.user.id or value["cancelled"] or value["status"] != "in_progress":
            raise AppError(403, "FORBIDDEN", "진행중인 본인 업무만 요청할 수 있습니다.")
        data = {"task_id": str(value["task_id"]), "row_version": str(value["row_version"]),
            "assignment_revision": value["assignment_revision"], "due_date": value["due_date"].isoformat(), "server_time": iso(now())}
    return success(data)


@bp.post("")
@login_required
def create():
    data = body({"request_id", "task_id", "recipient_id", "title", "request_text", "desired_date",
        "expected_row_version", "expected_assignment_revision"})
    identifier = canonical_uuid(data["request_id"])
    task_id = integer(data["task_id"])
    expected = integer(data["expected_row_version"])
    revision, recipient = data["expected_assignment_revision"], data["recipient_id"]
    if type(revision) is not int or not 1 <= revision <= 2147483647 or type(recipient) is not int or not 1 <= recipient <= 2147483647:
        raise AppError(400, "INVALID_INPUT", "배정 차수·대상을 확인해 주세요.")
    if recipient == g.user.id:
        raise AppError(400, "INVALID_INPUT", "본인에게 요청할 수 없습니다.")
    for field, limit in (("title", 200), ("request_text", 4000)):
        if not isinstance(data[field], str) or not data[field].strip() or len(data[field]) > limit:
            raise AppError(400, "INVALID_INPUT", "제목·요청문 길이를 확인해 주세요.")
    try:
        desired = date.fromisoformat(data["desired_date"])
        if desired.isoformat() != data["desired_date"]:
            raise ValueError()
    except (ValueError, TypeError):
        raise AppError(400, "INVALID_INPUT", "희망일을 확인해 주세요.") from None
    with authenticated_transaction() as db:
        require_storage(db)
        def mutate(session):
            value = task_context(session, task_id)
            if value["cancelled"] or value["status"] != "in_progress" or value["row_version"] != expected or value["assignment_revision"] != revision:
                raise AppError(409, "COLLABORATION_CONTEXT_CHANGED", "업무 상태·배정이 변경됐습니다. 다시 조회해 주세요.")
            target = session.scalar(select(User).where(User.id == recipient).with_for_update().execution_options(populate_existing=True))
            if target is None or not target.is_active or target.role not in ("employee", "admin"):
                raise AppError(409, "RECIPIENT_UNAVAILABLE", "현재 요청할 수 없는 대상입니다.")
            today = now().replace(tzinfo=timezone.utc).astimezone(KST).date()
            if not today <= desired <= value["due_date"]:
                raise AppError(400, "INVALID_INPUT", "희망일은 오늘부터 업무 마감일 사이여야 합니다.")
            active = list(session.scalars(select(Row).where(Row.task_id == task_id, Row.recipient_id == recipient,
                Row.status.in_(("pending", "accepted"))).with_for_update().execution_options(populate_existing=True)))
            for previous in active:
                reconcile(session, previous, task_context(session, task_id, previous.task_row_version))
                if previous.status in ("pending", "accepted"):
                    raise AppError(409, "COLLABORATION_ALREADY_ACTIVE", "같은 대상에게 진행중인 협업 요청이 있습니다.")
            if session.get(Row, identifier) is not None:
                raise AppError(409, "COLLABORATION_ID_REUSED", "이미 사용한 요청 식별자입니다.")
            stamp = now()
            item = Row(request_id=identifier, task_id=task_id, assignment_revision=revision,
                task_row_version=expected, requester_id=g.user.id, recipient_id=recipient,
                requester_name=g.user.display_name, task_title=value["title"], title=data["title"].strip(),
                request_text=data["request_text"].strip(), desired_date=desired, status="pending",
                row_version=1, created_at=stamp, updated_at=stamp)
            session.add(item)
            session.flush()
            session.add(Event(request_id=identifier, actor_id=g.user.id, status="pending", occurred_at=stamp))
            audit("collaboration.created", "collaboration", identifier, {"status": "pending"})
            return WriteResult(result(item), 201)
        value = perform_once(db, key=request.headers.get("Idempotency-Key"), action="collaboration.created",
            target=str(task_id), body=data, authorize=authorize_create, mutate=mutate)
    return success(value.data, value.status)


@bp.get("")
@login_required
def listing():
    if set(request.args) - {"scope"} or len(request.args.getlist("scope")) > 1:
        raise AppError(400, "INVALID_INPUT", "조회 조건을 확인해 주세요.")
    scope = request.args.get("scope", "received")
    if scope not in ("received", "sent"):
        raise AppError(400, "INVALID_INPUT", "조회 범위를 확인해 주세요.")
    with authenticated_transaction() as db:
        require_storage(db)
        party = Row.recipient_id if scope == "received" else Row.requester_id
        rows = list(db.scalars(select(Row).where(party == g.user.id,
            or_(Row.ended_at.is_(None), Row.ended_at >= now() - timedelta(days=90))).order_by(Row.created_at, Row.request_id)
            .with_for_update().execution_options(populate_existing=True)))
        data = []
        for item in rows:
            if item.status in ("pending", "accepted"):
                reconcile(db, item, task_context(db, item.task_id, item.task_row_version))
            if item.ended_at is None or item.ended_at >= now() - timedelta(days=90):
                data.append(public(item))
        answer = {"items": data, "server_time": iso(now())}
    return success(answer)


@bp.post("/<request_id>/actions/<decision>")
@login_required
def decide(request_id, decision):
    if decision not in DECISIONS:
        raise AppError(400, "INVALID_INPUT", "응답 종류를 확인해 주세요.")
    data = body({"expected_row_version"})
    expected = integer(data["expected_row_version"])
    action = "collaboration." + DECISIONS[decision]
    # Reconciliation is a durable system transition, even when the subsequent
    # user action is rejected as stale. No success receipt is created for it.
    with authenticated_transaction() as db:
        item = locked(db, request_id)
        if item.status in ("pending", "accepted"):
            reconcile(db, item, task_context(db, item.task_id, item.task_row_version))
    with authenticated_transaction() as db:
        def mutate(session):
            item = locked(session, request_id)
            context = task_context(session, item.task_id, item.task_row_version)
            reconcile(session, item, context)
            if item.row_version != expected:
                raise AppError(409, "COLLABORATION_CHANGED", "요청 상태가 변경됐습니다. 목록을 확인해 주세요.")
            allowed = (decision in ("accept", "decline") and item.status == "pending" and item.recipient_id == g.user.id
                or decision == "withdraw" and item.status in ("pending", "accepted") and item.requester_id == g.user.id
                or decision == "complete" and item.status == "accepted" and item.requester_id == g.user.id
                or decision == "leave" and item.status == "accepted" and item.recipient_id == g.user.id)
            if not allowed:
                raise AppError(403, "COLLABORATION_ACTION_FORBIDDEN", "현재 수행할 수 없는 응답입니다.")
            if decision in ("accept", "complete") and context["continuous_in_progress"] is not True:
                raise AppError(503, "COLLABORATION_CONTINUITY_REQUIRED", "업무 변경 이력 검증이 필요합니다.")
            # B1 requires active parties for new acceptance. B5 lets the current
            # authenticated party terminate even after the other is disabled.
            if decision == "accept":
                for user_id in sorted((item.requester_id, item.recipient_id)):
                    user = session.scalar(select(User).where(User.id == user_id).with_for_update().execution_options(populate_existing=True))
                    if user is None or not user.is_active or user.role not in ("employee", "admin"):
                        raise AppError(409, "RECIPIENT_UNAVAILABLE", "현재 참여할 수 없는 계정입니다.")
            transition(session, item, DECISIONS[decision], "recipient_response" if decision in ("accept", "decline") else decision)
            return WriteResult(result(item))
        value = perform_once(db, key=request.headers.get("Idempotency-Key"), action=action, target=request_id,
            body=data, authorize=authorize_request, mutate=mutate)
    return success(value.data, value.status)


@bp.get("/<request_id>/history")
@login_required
def history(request_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "이력 조회 조건을 확인해 주세요.")
    with authenticated_transaction() as db:
        item = locked(db, request_id)  # Same parties and ended+90-day boundary.
        if item.status in ("pending", "accepted"):
            reconcile(db, item, task_context(db, item.task_id, item.task_row_version))
        events = list(db.scalars(select(Event).where(Event.request_id == item.request_id)
            .order_by(Event.event_id).with_for_update().execution_options(populate_existing=True)))
        data = {"request_id": item.request_id, "items": [{"event_id": str(event.event_id),
            "status": event.status, "reason": event.reason, "actor_id": event.actor_id,
            "occurred_at": iso(event.occurred_at)} for event in events], "server_time": iso(now())}
    return success(data)
