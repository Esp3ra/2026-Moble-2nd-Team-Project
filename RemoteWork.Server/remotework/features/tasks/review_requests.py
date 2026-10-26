"""Member 2 manual messages about an already submitted result.

Result/organization structures are read only. Request, audit and W1 receipt
changes use the same authenticated guard/transaction as submission/decision.
"""
import json
import re

from flask import current_app, g, request
from sqlalchemy import inspect, select, text

from ...auth import authenticated_transaction, login_required
from ...common import AppError, audit, success
from ...db import get_db
from ...models import User, iso, utcnow
from ...write_contract import ExpectedVersions, positive_bigint
from ...write_requests import WriteResult, perform_once, register_write_action
from ..results import Submission, _facts
from .lifecycle_support import ensure_not_cancelled
from .models import TaskAssignment, TaskManager, TaskRecord
from .review_request_models import TaskReviewRequest


REQUEST_ACTION = "task.review_requested"
ACK_ACTION = "task.review_acknowledged"
RESULT_FIELDS = frozenset({"request_id", "task_id", "submission_id", "round_no",
                           "request_state", "created_at", "acknowledged_at"})
_VERSIONS = frozenset({"expected_row_version", "expected_assignment_revision"})


def _schema_error():
    return AppError(503, "TASK_REVIEW_REQUEST_SCHEMA_REQUIRED",
                    "검토 요청 저장소가 준비되지 않았습니다. 서버 담당자가 019 검토 요청 스키마를 확인해 주세요.")


def _expression(source):
    """Parse this feature's small SQL grammar without normalizing literal bytes."""
    pattern = r"\s+|`(?:``|[^`])*`|'(?:''|\\.|[^'\\])*'|[A-Za-z_][A-Za-z_0-9]*|[0-9]+|<>|!=|[(),=>]"
    tokens, offset = [], 0
    for match in re.finditer(pattern, source):
        if match.start() != offset:
            return None
        offset = match.end()
        token = match.group()
        if token.isspace():
            continue
        if token.startswith('`'):
            token = token[1:-1].replace('``', '`').lower()
        elif not token.startswith("'"):
            token = token.lower()
        tokens.append(token)
    if offset != len(source):
        return None
    # Only MySQL's actual string introducer is accepted, outside a quoted value.
    for index in range(len(tokens) - 1, -1, -1):
        if tokens[index] == '_utf8mb4':
            if index + 1 == len(tokens) or not tokens[index + 1].startswith("'"):
                return None
            del tokens[index]
    cursor = 0
    def take(value=None):
        nonlocal cursor
        token = tokens[cursor]
        if value is not None and token != value:
            raise ValueError()
        cursor += 1
        return token
    def peek():
        return tokens[cursor] if cursor < len(tokens) else None
    def atom():
        token = take()
        if token == '(':
            result = expression(); take(')'); return result
        if token == 'case':
            take('when'); condition = expression(); take('then'); yes = expression()
            take('else'); no = expression(); take('end')
            return ('case', condition, yes, no)
        if peek() == '(':
            take('('); args = [expression()]
            while peek() == ',':
                take(','); args.append(expression())
            take(')'); return ('call', token, tuple(args))
        return token
    def comparison():
        left = atom()
        if peek() in ('=', '>', '<>', '!='):
            operator = take()
            return ('<>' if operator == '!=' else operator, left, atom())
        if peek() == 'is':
            take('is'); negative = peek() == 'not'
            if negative: take('not')
            take('null'); return ('isnotnull' if negative else 'isnull', left)
        if peek() == 'in':
            take('in'); take('('); args = [expression()]
            while peek() == ',':
                take(','); args.append(expression())
            take(')'); return ('in', left, tuple(args))
        return left
    def conjunction():
        result = comparison()
        while peek() == 'and':
            take('and'); result = ('and', result, comparison())
        return result
    def expression():
        result = conjunction()
        while peek() == 'or':
            take('or'); result = ('or', result, conjunction())
        return result
    try:
        result = expression()
        return result if cursor == len(tokens) else None
    except (IndexError, ValueError, RecursionError):
        return None


def require_review_request_schema(db):
    inspector = inspect(db.connection())
    name = TaskReviewRequest.__tablename__
    if db.bind.dialect.name != "mysql" or not inspector.has_table(name):
        raise _schema_error()
    columns = {c["name"]: c for c in inspector.get_columns(name)}
    if set(columns) != set(TaskReviewRequest.__table__.columns.keys()):
        raise _schema_error()
    types = {"request_id": "BIGINT", "task_id": "BIGINT", "submission_id": "BIGINT",
             "round_no": "BIGINT", "assignment_revision": "INTEGER", "requested_by": "INTEGER",
             "reviewer_id": "INTEGER", "acknowledged_by": "INTEGER", "pending_submission_id": "BIGINT"}
    if any(type(columns[key]["type"]).__name__ != kind or getattr(columns[key]["type"], "unsigned", False)
           for key, kind in types.items()):
        raise _schema_error()
    if inspector.get_pk_constraint(name).get("constrained_columns") != ["request_id"]:
        raise _schema_error()
    optional = {"acknowledged_at", "acknowledged_by", "pending_submission_id"}
    if any(column["nullable"] != (key in optional) or column.get("default") is not None
           for key, column in columns.items()) or columns["request_id"].get("autoincrement") is not True:
        raise _schema_error()
    for key, kind, length in (("reason", "TEXT", None), ("status", "VARCHAR", 20),
                              ("created_at", "DATETIME", None), ("acknowledged_at", "DATETIME", None)):
        column_type = columns[key]["type"]
        if (type(column_type).__name__ != kind or (length is not None and column_type.length != length)
                or (kind == "DATETIME" and getattr(column_type, "fsp", None) not in (None, 0))):
            raise _schema_error()
    options = inspector.get_table_options(name)
    if options.get("mysql_engine", "").lower() != "innodb" or options.get("mysql_collate") != "utf8mb4_unicode_ci":
        raise _schema_error()
    collations = dict(db.execute(text("SELECT COLUMN_NAME, COLLATION_NAME FROM information_schema.COLUMNS "
        "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = :table_name "
        "AND COLUMN_NAME IN ('reason', 'status')"), {"table_name": name}).all())
    if collations != {"reason": "utf8mb4_unicode_ci", "status": "utf8mb4_unicode_ci"}:
        raise _schema_error()
    computed = columns["pending_submission_id"].get("computed") or {}
    expression = _expression(str(computed.get("sqltext", "")))
    indexes = inspector.get_indexes(name) + inspector.get_unique_constraints(name)
    if (computed.get("persisted") is not True or
            expression != _expression("CASE WHEN status = 'pending' THEN submission_id ELSE NULL END") or
            not any(i.get("column_names") == ["pending_submission_id"] and i.get("unique", True)
                    for i in indexes)):
        raise _schema_error()
    fks = inspector.get_foreign_keys(name)
    actual = {(tuple(f["constrained_columns"]), f["referred_table"], tuple(f["referred_columns"])) for f in fks}
    expected = {(("task_id", "assignment_revision"), "task_assignments", ("task_id", "revision")),
                (("submission_id",), "result_submissions", ("id",)),
                *((tuple([field]), "users", ("id",)) for field in
                  ("requested_by", "reviewer_id", "acknowledged_by"))}
    if actual != expected or any(f.get("referred_schema") not in (None, inspector.default_schema_name)
            or any(str(f.get("options", {}).get(option, "RESTRICT")).upper() not in ("RESTRICT", "NO ACTION")
                   for option in ("ondelete", "onupdate")) for f in fks):
        raise _schema_error()
    checks = {c["name"]: _expression(c["sqltext"]) for c in inspector.get_check_constraints(name)}
    expected_checks = {constraint.name: _expression(str(constraint.sqltext))
                       for constraint in TaskReviewRequest.__table__.constraints if hasattr(constraint, "sqltext")}
    enforced = set(db.execute(text("SELECT CONSTRAINT_NAME FROM information_schema.TABLE_CONSTRAINTS "
        "WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = :table_name "
        "AND CONSTRAINT_TYPE = 'CHECK' AND ENFORCED = 'YES'"), {"table_name": name}).scalars())
    if any(value is None or checks.get(key) != value or key not in enforced for key, value in expected_checks.items()):
        raise _schema_error()


def _require_transaction(db):
    if not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the current authenticated_transaction for review requests")


def _current(db, target):
    _require_transaction(db)
    task_id = positive_bigint(target, "task_id")
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id).with_for_update()
                     .execution_options(populate_existing=True))
    if task is None:
        raise AppError(404, "TASK_NOT_FOUND", "업무를 찾을 수 없습니다.")
    assignment = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == task_id,
        TaskAssignment.revision == task.current_assignment_revision).with_for_update()
        .execution_options(populate_existing=True))
    if assignment is None:
        raise AppError(503, "TASK_RELATION_INCOMPLETE", "현재 업무 배정을 확인해 주세요.")
    managers = list(db.scalars(select(TaskManager.user_id).where(TaskManager.task_id == task_id)
                              .order_by(TaskManager.user_id).with_for_update()))
    return task, assignment, managers


def _reviewer(db, task, assignment, managers):
    # Use exactly the same locked current-manager permission as result decisions.
    if "results" not in current_app.blueprints:
        raise AppError(503, "RESULTS_DEPENDENCY_REQUIRED", "기존 결과물 검토자 연결이 필요합니다.")
    _current_facts, reviewer = _facts(db, task, assignment, managers)
    owner = db.get(User, assignment.assignee_id)
    if (reviewer is None or not reviewer.is_active or reviewer.role != "admin" or
            reviewer.id == assignment.assignee_id or owner is None or not owner.is_active):
        raise AppError(409, "REVIEWER_UNAVAILABLE", "본인과 다른 현재 활성 검토자가 필요합니다.")
    return reviewer


def authorize_request(db, target):
    task, assignment, _managers = _current(db, target)
    if assignment.assignee_id != g.user.id:
        raise AppError(403, "FORBIDDEN", "현재 배정된 본인 업무만 검토 요청할 수 있습니다.")
    require_review_request_schema(db)
    return True


def authorize_acknowledgement(db, target):
    task, assignment, managers = _current(db, target)
    if g.user.role != "admin" or assignment.assignee_id == g.user.id:
        raise AppError(403, "FORBIDDEN", "현재 지정 검토자만 요청을 확인할 수 있습니다.")
    reviewer = _reviewer(db, task, assignment, managers)
    if reviewer.id != g.user.id:
        raise AppError(403, "FORBIDDEN", "현재 지정 검토자만 요청을 확인할 수 있습니다.")
    require_review_request_schema(db)
    return True


def _latest(db, task_id):
    return db.scalar(select(Submission).where(Submission.task_id == task_id)
                     .order_by(Submission.round_no.desc()).limit(1).with_for_update()
                     .execution_options(populate_existing=True))


def _submitted(db, task, assignment):
    ensure_not_cancelled(db, task.task_id)
    latest = _latest(db, task.task_id)
    if (task.task_status != "submitted" or latest is None or latest.status != "submitted" or
            latest.assignment_revision != assignment.revision or latest.submitted_by != assignment.assignee_id):
        raise AppError(409, "TASK_REVIEW_REQUEST_NOT_AVAILABLE", "현재 제출완료된 최신 결과에만 검토 요청할 수 있습니다.")
    return latest


def _state(db, row, task, assignment, latest):
    try:
        ensure_not_cancelled(db, task.task_id)
    except AppError as error:
        if error.code != "TASK_CANCELLED":
            raise
        return "resolved"
    if (task.task_status != "submitted" or latest is None or latest.id != row.submission_id or
            latest.status != "submitted" or assignment.revision != row.assignment_revision):
        return "resolved"
    return row.status


def _item(db, row, task, assignment, reviewer, latest):
    return dict(request_id=str(row.request_id), task_id=str(task.task_id), title=task.title,
        task_status=task.task_status, row_version=str(task.row_version),
        submission_id=str(row.submission_id), round_no=str(row.round_no),
        assignment_revision=row.assignment_revision, requested_by=row.requested_by,
        requester_name=db.get(User, row.requested_by).display_name,
        reviewer_id=reviewer.id, reviewer_name=reviewer.display_name, reason=row.reason,
        state=_state(db, row, task, assignment, latest), created_at=iso(row.created_at),
        acknowledged_at=iso(row.acknowledged_at), acknowledged_by=row.acknowledged_by)


def get_review_request_context(db, target):
    authorize_request(db, target)
    task, assignment, managers = _current(db, target)
    reviewer = _reviewer(db, task, assignment, managers)
    latest = _submitted(db, task, assignment)
    row = db.scalar(select(TaskReviewRequest).where(TaskReviewRequest.submission_id == latest.id)
        .order_by(TaskReviewRequest.request_id.desc()).limit(1).with_for_update()
        .execution_options(populate_existing=True))
    return dict(task_id=str(task.task_id), title=task.title, task_status=task.task_status,
        due_date=task.due_date.isoformat(), row_version=str(task.row_version),
        assignment_revision=assignment.revision, submission_id=str(latest.id), round_no=str(latest.round_no),
        description=latest.description, reviewer_id=reviewer.id, reviewer_name=reviewer.display_name,
        request=None if row is None else _item(db, row, task, assignment, reviewer, latest))


def list_review_requests(db, scope):
    _require_transaction(db)
    if scope not in ("own", "reviewing"):
        raise AppError(400, "INVALID_INPUT", "검토 요청 조회 범위를 확인해 주세요.")
    if scope == "reviewing" and g.user.role != "admin":
        raise AppError(403, "FORBIDDEN", "검토자만 받은 요청을 조회할 수 있습니다.")
    require_review_request_schema(db)
    query = select(TaskReviewRequest.task_id).distinct().order_by(TaskReviewRequest.task_id)
    if scope == "own":
        query = query.where(TaskReviewRequest.requested_by == g.user.id)
    items = []
    for task_id in db.scalars(query):
        task, assignment, managers = _current(db, str(task_id))
        if scope == "own" and assignment.assignee_id != g.user.id:
            continue
        try:
            reviewer = _reviewer(db, task, assignment, managers)
        except AppError as error:
            if error.code in ("REVIEWER_UNAVAILABLE", "REVIEWER_CHANGED"):
                continue
            raise
        if scope == "reviewing" and reviewer.id != g.user.id:
            continue
        latest = _latest(db, task_id)
        rows = db.scalars(select(TaskReviewRequest).where(TaskReviewRequest.task_id == task_id,
            TaskReviewRequest.assignment_revision == assignment.revision,
            TaskReviewRequest.requested_by == assignment.assignee_id)
            .order_by(TaskReviewRequest.request_id.desc()).with_for_update()
            .execution_options(populate_existing=True))
        items.extend(_item(db, row, task, assignment, reviewer, latest) for row in rows)
    return sorted(items, key=lambda item: int(item["request_id"]), reverse=True)


def validate_review_request_body(body, *, acknowledge=False):
    fields = _VERSIONS if acknowledge else _VERSIONS | {"submission_id", "reason"}
    if not isinstance(body, dict) or set(body) != fields:
        raise AppError(400, "INVALID_INPUT", "검토 요청 입력 항목을 확인해 주세요.")
    expected = ExpectedVersions.from_body(body)
    if not acknowledge:
        positive_bigint(body["submission_id"], "submission_id")
        reason = body["reason"]
        if not isinstance(reason, str) or not reason.strip() or len(reason) > 4000:
            raise AppError(400, "INVALID_INPUT", "검토 요청 내용을 1~4000자로 입력해 주세요.")
        try:
            reason.encode("utf-8")
        except UnicodeEncodeError:
            raise AppError(400, "INVALID_INPUT", "검토 요청 내용의 문자 형식을 확인해 주세요.") from None
    return expected


def _result(row):
    return dict(request_id=str(row.request_id), task_id=str(row.task_id),
        submission_id=str(row.submission_id), round_no=str(row.round_no), request_state=row.status,
        created_at=iso(row.created_at), acknowledged_at=iso(row.acknowledged_at))


def execute_review_request(db, *, target, body, key):
    authorize_request(db, target)
    expected = validate_review_request_body(body)

    def mutate(session):
        task, assignment, managers = _current(session, target)
        expected.check(row_version=task.row_version, assignment_revision=assignment.revision)
        reviewer = _reviewer(session, task, assignment, managers)
        latest = _submitted(session, task, assignment)
        if str(latest.id) != body["submission_id"]:
            raise AppError(409, "SUBMISSION_CHANGED", "최신 제출 결과가 변경되었습니다. 다시 조회해 주세요.")
        if session.scalar(select(TaskReviewRequest.request_id).where(
                TaskReviewRequest.submission_id == latest.id, TaskReviewRequest.status == "pending")
                .limit(1).with_for_update()) is not None:
            raise AppError(409, "REVIEW_REQUEST_PENDING", "같은 제출 회차에 확인 대기 중인 검토 요청이 있습니다.")
        row = TaskReviewRequest(task_id=task.task_id, assignment_revision=assignment.revision,
            submission_id=latest.id, round_no=latest.round_no, requested_by=g.user.id,
            reviewer_id=reviewer.id, reason=body["reason"], status="pending",
            created_at=utcnow().replace(microsecond=0))
        session.add(row)
        session.flush()
        audit(REQUEST_ACTION, "task_review_request", row.request_id,
              {"task_id": target, "submission_id": str(latest.id), "reviewer_id": reviewer.id})
        return WriteResult(_result(row), 201)

    return perform_once(db, key=key, action=REQUEST_ACTION, target=target, body=body,
                        authorize=authorize_request, mutate=mutate)


def acknowledge_review_request(db, *, target, request_id, body, key):
    authorize_acknowledgement(db, target)
    identifier = positive_bigint(request_id, "request_id")
    expected = validate_review_request_body(body, acknowledge=True)

    def mutate(session):
        task, assignment, _managers = _current(session, target)
        expected.check(row_version=task.row_version, assignment_revision=assignment.revision)
        latest = _submitted(session, task, assignment)
        row = session.scalar(select(TaskReviewRequest).where(TaskReviewRequest.request_id == identifier,
            TaskReviewRequest.task_id == task.task_id).with_for_update().execution_options(populate_existing=True))
        if row is None or row.requested_by != assignment.assignee_id or row.assignment_revision != assignment.revision:
            raise AppError(404, "REVIEW_REQUEST_NOT_FOUND", "현재 업무의 검토 요청을 찾을 수 없습니다.")
        if row.submission_id != latest.id or row.status != "pending":
            raise AppError(409, "REVIEW_REQUEST_STATE_CONFLICT", "이미 확인했거나 종료된 검토 요청입니다. 다시 조회해 주세요.")
        row.status = "acknowledged"
        row.acknowledged_by = g.user.id
        row.acknowledged_at = utcnow().replace(microsecond=0)
        session.flush()
        audit(ACK_ACTION, "task_review_request", row.request_id,
              {"task_id": target, "submission_id": str(row.submission_id)})
        return WriteResult(_result(row), 200)

    return perform_once(db, key=key, action=ACK_ACTION, target=target,
                        body={**body, "request_id": request_id}, authorize=authorize_acknowledgement, mutate=mutate)


def _input():
    if request.args:
        raise AppError(400, "INVALID_INPUT", "검토 요청에는 조회 조건을 전달하지 않습니다.")
    if not request.is_json:
        raise AppError(415, "JSON_REQUIRED", "JSON 형식으로 요청해 주세요.")
    data = request.get_data(cache=True)
    if len(data) > 32768:
        raise AppError(413, "HTTP_413", "검토 요청 입력이 너무 큽니다.")
    def unique(pairs):
        value = {}
        for name, item in pairs:
            if name in value:
                raise ValueError("duplicate field")
            value[name] = item
        return value
    try:
        return json.loads(data.decode("utf-8"), object_pairs_hook=unique,
                          parse_constant=lambda value: (_ for _ in ()).throw(ValueError(value)))
    except (ValueError, UnicodeError, RecursionError):
        raise AppError(400, "INVALID_INPUT", "올바른 JSON 입력이 필요합니다.") from None


@login_required
def review_request_context(task_id):
    if request.args:
        raise AppError(400, "INVALID_INPUT", "검토 요청 준비에는 조회 조건을 전달하지 않습니다.")
    with authenticated_transaction() as db:
        data = get_review_request_context(db, task_id)
    return success({"context": data})


@login_required
def review_request_list():
    if set(request.args) - {"scope"} or len(request.args.getlist("scope")) > 1:
        raise AppError(400, "INVALID_INPUT", "검토 요청 조회 조건을 확인해 주세요.")
    with authenticated_transaction() as db:
        items = list_review_requests(db, request.args.get("scope", "own"))
    return success({"items": items})


@login_required
def create_review_request(task_id):
    with authenticated_transaction() as db:
        result = execute_review_request(db, target=task_id, body=_input(), key=request.headers.get("Idempotency-Key"))
    return success(result.data, result.status)


@login_required
def acknowledge_request(task_id, request_id):
    with authenticated_transaction() as db:
        result = acknowledge_review_request(db, target=task_id, request_id=request_id,
            body=_input(), key=request.headers.get("Idempotency-Key"))
    return success(result.data, result.status)


def register_review_actions(app):
    register_write_action(app, action=REQUEST_ACTION, authorize=authorize_request, result_fields=RESULT_FIELDS)
    register_write_action(app, action=ACK_ACTION, authorize=authorize_acknowledgement, result_fields=RESULT_FIELDS)
