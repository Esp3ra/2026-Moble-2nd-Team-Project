"""Member 1 leave service. Explicit MySQL storage; no automatic policy/grants.

The calendar-year reservation policy is an executable DEVELOPMENT proposal.
An explicit matching deployment policy plus per-year calendar/balance/reviewer
configuration is required before mutations. No member5 UI is modified here.
"""
from datetime import date, datetime, timedelta
import hashlib
import json
import re
import uuid

from flask import Blueprint, current_app, g, request
from sqlalchemy import (MetaData, Table, Column, Integer, BigInteger, SmallInteger,
    String, Text, Date, DateTime, ForeignKey, ForeignKeyConstraint,
    CheckConstraint, UniqueConstraint, Index, select, inspect, text)

from .auth import authenticated_transaction, login_required
from .common import AppError, audit, success
from .models import User, iso
from .write_requests import (WriteResult, perform_once, register_write_action,
                             register_write_result_reader)

POLICY = "calendar-year-reserve-v1"
SUBMIT, DECIDE = "leave.submitted", "leave.decided"
TYPES = ("annual", "am_half", "pm_half", "sick", "official")
RECEIPT_FIELDS = frozenset(("request_id", "revision", "status", "charged_units", "updated_at"))
bp = Blueprint("leave", __name__, url_prefix="/api/v1/leave")
metadata = MetaData()
users = Table("users", metadata, Column("id", Integer, primary_key=True))
OPTIONS = dict(mysql_engine="InnoDB", mysql_charset="utf8mb4", mysql_collate="utf8mb4_unicode_ci")
years = Table("leave_years", metadata,
    Column("year", SmallInteger, primary_key=True, autoincrement=False), Column("policy_id", String(32), nullable=False),
    Column("holidays_json", Text, nullable=False), Column("configured_at", DateTime, nullable=False),
    CheckConstraint("year >= 2000", name="ck_leave_year_min"),
    CheckConstraint("year <= 9998", name="ck_leave_year_max"), **OPTIONS)
balances = Table("leave_balances", metadata,
    Column("applicant_id", Integer, ForeignKey("users.id"), primary_key=True),
    Column("year", SmallInteger, ForeignKey("leave_years.year"), primary_key=True),
    Column("granted_units", Integer, nullable=False),
    CheckConstraint("granted_units >= 0", name="ck_leave_grant_min"),
    CheckConstraint("granted_units <= 732", name="ck_leave_grant_max"), **OPTIONS)
reviewers = Table("leave_reviewers", metadata,
    Column("applicant_id", Integer, ForeignKey("users.id"), primary_key=True),
    Column("manager_id", Integer, ForeignKey("users.id"), nullable=False),
    Column("revision", BigInteger, nullable=False),
    CheckConstraint("applicant_id <> manager_id", name="ck_leave_no_self_review"),
    CheckConstraint("revision > 0", name="ck_leave_reviewer_revision"),
    Index("ix_leave_reviewer_manager", "manager_id", "applicant_id"), **OPTIONS)
requests = Table("leave_requests", metadata,
    Column("id", String(36), primary_key=True), Column("applicant_id", Integer, nullable=False),
    Column("year", SmallInteger, nullable=False), Column("type", String(16), nullable=False),
    Column("start_date", Date, nullable=False), Column("end_date", Date, nullable=False),
    Column("charged_units", Integer, nullable=False), Column("reason", Text, nullable=False),
    Column("status", String(16), nullable=False), Column("revision", BigInteger, nullable=False),
    Column("decision_reason", Text, nullable=False),
    Column("decision_by", Integer, ForeignKey("users.id"), nullable=True),
    Column("created_at", DateTime, nullable=False), Column("updated_at", DateTime, nullable=False),
    ForeignKeyConstraint(["applicant_id", "year"], ["leave_balances.applicant_id", "leave_balances.year"]),
    CheckConstraint("charged_units >= 0", name="ck_leave_charge_min"),
    CheckConstraint("charged_units <= 732", name="ck_leave_charge_max"),
    CheckConstraint("revision > 0", name="ck_leave_request_revision"),
    CheckConstraint("end_date >= start_date", name="ck_leave_date_order"),
    CheckConstraint("status IN ('pending','approved','rejected')", name="ck_leave_status"),
    CheckConstraint("type IN ('annual','am_half','pm_half','sick','official')", name="ck_leave_type"),
    Index("ix_leave_applicant_period", "applicant_id", "year", "start_date", "end_date"), **OPTIONS)
operations = Table("leave_operations", metadata,
    Column("actor_id", Integer, ForeignKey("users.id"), primary_key=True),
    Column("intent_id", String(36), primary_key=True), Column("action", String(32), nullable=False),
    Column("target", String(36), nullable=False), Column("request_id", String(36), ForeignKey("leave_requests.id"), nullable=False),
    Column("body_hash", String(64), nullable=False), Column("receipt_json", Text, nullable=False),
    Column("http_status", Integer, nullable=False), Column("created_at", DateTime, nullable=False),
    Column("expires_at", DateTime, nullable=False),
    CheckConstraint("http_status IN (200,201)", name="ck_leave_operation_status"), **OPTIONS)
TABLES = (years, balances, reviewers, requests, operations)


def error(status, code, message):
    raise AppError(status, code, message)


def canonical(value):
    if not isinstance(value, str):
        error(400, "INVALID_INPUT", "정규 UUID 문자열이 필요합니다.")
    try:
        if str(uuid.UUID(value)) != value:
            raise ValueError()
    except ValueError:
        error(400, "INVALID_INPUT", "정규 소문자 UUID가 필요합니다.")
    return value


def integer_string(value):
    if not isinstance(value, str) or not re.fullmatch(r"[1-9][0-9]{0,18}", value) or int(value) > 9223372036854775807:
        error(400, "INVALID_INPUT", "revision은 양의 BIGINT 문자열이어야 합니다.")
    return int(value)


def fields(names):
    def pairs(items):
        result = {}
        for k, v in items:
            if k in result:
                raise ValueError()
            result[k] = v
        return result
    try:
        if request.args or not request.is_json:
            raise ValueError()
        body = json.loads(request.get_data().decode("utf-8"), object_pairs_hook=pairs,
                          parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
        if type(body) is not dict or set(body) != set(names):
            raise ValueError()
    except (ValueError, UnicodeError):
        error(400, "INVALID_INPUT", "중복 없는 정확한 JSON 필드를 전달해 주세요.")
    return body


def normalized_check(value):
    # Normalize SQL spelling ONLY outside literals. Literal bytes and case are
    # part of the reviewed DDL; e.g. 'PENDING' must never equal 'pending'.
    pieces = re.split(r"('(?:''|[^'])*')", value)
    result = []
    for i, piece in enumerate(pieces):
        if i % 2:
            result.append(piece)
        else:
            if i+1 < len(pieces):piece = re.sub(r"_[a-zA-Z0-9]+$", "", piece)
            result.append(re.sub(r"[\s`()]+", "", piece.lower()).replace("!=", "<>"))
    return ''.join(result)


def storage(db):
    connection = db.connection()
    if connection.dialect.name != "mysql":
        error(503, "LEAVE_SCHEMA_REQUIRED", "연차는 검토된 MySQL 저장소가 필요합니다.")
    inspector = inspect(connection)
    for table in TABLES:
        if not inspector.has_table(table.name):
            error(503, "LEAVE_SCHEMA_REQUIRED", "연차 테이블 준비가 필요합니다.")
        actual = {c['name']: c for c in inspector.get_columns(table.name)}
        pk = list(table.primary_key.columns.keys())
        if (set(actual) != set(table.c.keys()) or inspector.get_pk_constraint(table.name)['constrained_columns'] != pk
                or str(inspector.get_table_options(table.name).get('mysql_engine', '')).lower() != 'innodb'):
            error(503, "LEAVE_SCHEMA_REQUIRED", "연차 저장소 구조가 다릅니다.")
        for c in table.c:
            a = actual[c.name]
            # PK columns here are explicit natural keys; none auto-increments.
            if (str(a['type']).split()[0].split('(')[0] != str(c.type).split()[0].split('(')[0]
                    or getattr(a['type'], 'length', None) != getattr(c.type, 'length', None)
                    or a['nullable'] != c.nullable or a.get('default') is not None
                    or a.get('autoincrement') or getattr(a['type'], 'unsigned', False)
                    or (isinstance(c.type, (String, Text)) and getattr(a['type'], 'collation', None) != 'utf8mb4_unicode_ci')):
                current_app.logger.error('leave_schema_column table=%s column=%s type=%s nullable=%s default=%r autoincrement=%r expected=%s', table.name, c.name, a['type'], a['nullable'], a.get('default'), a.get('autoincrement'), c.type)
                error(503, "LEAVE_SCHEMA_REQUIRED", "연차 열 정의가 다릅니다.")
        wanted = {(tuple(f.column_keys), f.referred_table.name, tuple(e.column.name for e in f.elements))
                  for f in table.foreign_key_constraints}
        fks = inspector.get_foreign_keys(table.name)
        if (len(fks) != len(wanted) or {(tuple(f['constrained_columns']), f['referred_table'], tuple(f['referred_columns'])) for f in fks} != wanted
                or any(f.get('referred_schema') not in (None, inspector.default_schema_name)
                       or any(str(f.get('options', {}).get(k, 'RESTRICT')).upper() not in ('RESTRICT', 'NO ACTION') for k in ('ondelete', 'onupdate')) for f in fks)):
            error(503, "LEAVE_SCHEMA_REQUIRED", "연차 참조 보존 구조가 다릅니다.")
        expected_checks = {c.name: normalized_check(str(c.sqltext)) for c in table.constraints if isinstance(c, CheckConstraint)}
        actual_checks = {c['name']: normalized_check(c['sqltext']) for c in inspector.get_check_constraints(table.name)}
        if expected_checks != actual_checks:
            error(503, "LEAVE_SCHEMA_REQUIRED", "연차 제약 조건이 다릅니다.")
        enforced = db.execute(text("SELECT CONSTRAINT_NAME,ENFORCED FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=:name AND CONSTRAINT_TYPE='CHECK'"), {'name': table.name}).all()
        if any(e != 'YES' for _, e in enforced) or len(enforced) != len(expected_checks):
            error(503, "LEAVE_SCHEMA_REQUIRED", "연차 CHECK가 강제되지 않습니다.")
        indexes = inspector.get_indexes(table.name)
        if any(not any(x['column_names'][:len(i.columns)] == list(i.columns.keys()) for x in indexes) for i in table.indexes):
            error(503, "LEAVE_SCHEMA_REQUIRED", "연차 인덱스 준비가 필요합니다.")


def now(db):
    return db.scalar(text("SELECT UTC_TIMESTAMP(6)"))


def current(db, table, *criteria):
    return db.execute(select(table).where(*criteria).with_for_update()).mappings().first()


def actor(db, user_id):
    row = db.scalar(select(User).where(User.id == user_id).with_for_update().execution_options(populate_existing=True))
    if row is None or not row.is_active:
        error(403, "FORBIDDEN", "현재 활성 회원만 이용할 수 있습니다.")
    return row


def assignment(db, applicant_id, *, required=False):
    row = current(db, reviewers, reviewers.c.applicant_id == applicant_id)
    if row:
        manager = db.scalar(select(User).where(User.id == row.manager_id).with_for_update().execution_options(populate_existing=True))
        if manager is None or not manager.is_active or manager.role != 'admin' or manager.id == applicant_id:
            if required:error(503, "LEAVE_REVIEWER_REQUIRED", "유효한 담당 관리자 지정이 필요합니다.")
            return None  # own history remains readable after reviewer revocation
    elif required:
        error(503, "LEAVE_REVIEWER_REQUIRED", "연차 담당 관리자 지정이 필요합니다.")
    return row


def readable(db, applicant_id):
    actor(db, applicant_id)
    if applicant_id == g.user.id:
        return
    row = assignment(db, applicant_id)
    if g.user.role != 'admin' or row is None or row.manager_id != g.user.id:
        error(403, "FORBIDDEN", "본인 또는 현재 지정 담당자만 조회할 수 있습니다.")


def authorization(db, target):
    storage(db)
    if target == 'new':
        return True  # only the current authenticated actor may submit for self
    canonical(target)
    item = current(db, requests, requests.c.id == target)
    if item is None:
        error(404, "LEAVE_NOT_FOUND", "연차 신청이 없습니다.")
    readable(db, item.applicant_id)
    return True


def decision_authorization(db, target):
    if target == 'new':
        error(400, "INVALID_INPUT", "결정 대상 신청 UUID가 필요합니다.")
    authorization(db, target)
    item = current(db, requests, requests.c.id == target)
    link = assignment(db, item.applicant_id)
    if g.user.role != 'admin' or item.applicant_id == g.user.id or link is None or link.manager_id != g.user.id:
        error(403, 'FORBIDDEN', '현재 지정 담당 관리자만 결정할 수 있습니다.')
    return True


def write_policy():
    if current_app.config.get("LEAVE_WRITE_POLICY") != POLICY:
        error(503, "LEAVE_POLICY_REQUIRED", "연차 계산 정책 합의·배포 확인이 필요합니다.")


def year_data(db, year):
    row = current(db, years, years.c.year == year)
    if row is None or row.policy_id != POLICY:
        error(503, "LEAVE_CALENDAR_REQUIRED", "해당 연도의 확정 휴일 달력이 필요합니다.")
    try:
        raw = json.loads(row.holidays_json)
        if not isinstance(raw, list) or len(raw) > 366 or len(set(raw)) != len(raw):
            raise ValueError()
        holidays = {parse_date(d) for d in raw}
        if any(d.year != year for d in holidays):
            raise ValueError()
    except (ValueError, TypeError, AppError):
        error(503, "LEAVE_CALENDAR_REQUIRED", "연차 휴일 달력을 확인해 주세요.")
    return holidays


def balance(db, applicant_id, year):
    row = current(db, balances, balances.c.applicant_id == applicant_id, balances.c.year == year)
    if row is None:
        error(503, "LEAVE_ENTITLEMENT_REQUIRED", "개인별 연차 부여일수 설정이 필요합니다.")
    return row


def totals(db, applicant_id, year):
    row = balance(db, applicant_id, year)
    items = db.execute(select(requests.c.status, requests.c.charged_units).where(
        requests.c.applicant_id == applicant_id, requests.c.year == year).with_for_update()).all()
    reserved = sum(n for s, n in items if s == 'pending')
    used = sum(n for s, n in items if s == 'approved')
    remaining = row.granted_units - reserved - used
    if remaining < 0:
        error(503, "LEAVE_BALANCE_INCONSISTENT", "연차 예약·사용 내역 점검이 필요합니다.")
    return dict(year=year, granted_units=row.granted_units, reserved_units=reserved, used_units=used, remaining_units=remaining, unit_days="0.5")


def parse_date(value):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value):
        error(400, "INVALID_INPUT", "날짜는 YYYY-MM-DD여야 합니다.")
    try:
        return date.fromisoformat(value)
    except ValueError:
        error(400, "INVALID_INPUT", "유효한 날짜가 필요합니다.")


def reason(value, *, mandatory=False):
    if (not isinstance(value, str) or len(value) > 1000 or (mandatory and not value.strip())
            or any(0xD800 <= ord(c) <= 0xDFFF for c in value)):
        error(400, "INVALID_INPUT", "사유는 1,000자 이내이며 반려사유는 필수입니다.")
    return value


def request_view(db, item):
    # A display/action flag is not read authorization. Recheck every serialized
    # row using current identity/relationship reads, including list responses.
    readable(db, item.applicant_id)
    person = actor(db, item.applicant_id)
    link = assignment(db, item.applicant_id)
    can_review = bool(item.status == 'pending' and g.user.role == 'admin' and link and link.manager_id == g.user.id and item.applicant_id != g.user.id)
    return dict(request_id=item.id, revision=str(item.revision), applicant_id=person.id,
        applicant_name=person.display_name, department=person.department, type=item.type,
        start_date=item.start_date.isoformat(), end_date=item.end_date.isoformat(), reason=item.reason,
        status=item.status, charged_units=item.charged_units, unit_days="0.5", can_review=can_review,
        decision_reason=item.decision_reason, decision_by=item.decision_by,
        created_at=iso(item.created_at), updated_at=iso(item.updated_at))


def receipt(item):
    return dict(request_id=item.id, revision=str(item.revision), status=item.status,
                charged_units=item.charged_units, updated_at=iso(item.updated_at))


def intent_hash(body):
    return hashlib.sha256(json.dumps(body, sort_keys=True, ensure_ascii=True, separators=(',', ':'), allow_nan=False).encode('ascii')).hexdigest()


def perform(db, action, target, body, mutate):
    intent = canonical(body['intent_id'])
    def once(session):
        old = current(session, operations, operations.c.actor_id == g.user.id, operations.c.intent_id == intent)
        stamp = now(session)
        if old:
            if old.action != action or old.target != target or old.body_hash != intent_hash(body):
                error(409, "LEAVE_INTENT_CONFLICT", "같은 요청 UUID에 다른 내용을 저장할 수 없습니다.")
            if old.expires_at <= stamp:
                error(410, "LEAVE_INTENT_EXPIRED", "원 요청 결과 보관기간이 지났습니다.")
            return WriteResult(json.loads(old.receipt_json), old.http_status)
        write_policy()
        result = mutate(session)
        # Persistent intent tombstone prevents a new key creating a duplicate.
        ttl = min(current_app.config['WRITE_REQUEST_TTL_SECONDS'], 86400)
        session.execute(operations.insert().values(actor_id=g.user.id, intent_id=intent, action=action,
            target=target, request_id=result.data['request_id'], body_hash=intent_hash(body),
            receipt_json=json.dumps(result.data), http_status=result.status, created_at=stamp,
            expires_at=stamp+timedelta(seconds=ttl)))
        return result
    auth = authorization if action == SUBMIT else decision_authorization
    return perform_once(db, key=request.headers.get('Idempotency-Key'), action=action, target=target,
                        body=body, authorize=auth, mutate=once)


@bp.post('/requests')
@login_required
def submit():
    body = fields({'intent_id', 'type', 'start_date', 'end_date', 'reason'})
    canonical(body['intent_id']);reason(body['reason'])
    start, end = parse_date(body['start_date']), parse_date(body['end_date'])
    if body['type'] not in TYPES or end < start or start.year != end.year or not 2000 <= start.year <= 9998:
        error(400, "INVALID_INPUT", "유형·같은 연도 기간을 확인해 주세요.")
    if body['type'] in ('am_half', 'pm_half') and start != end:
        error(400, "INVALID_INPUT", "반차는 같은 날만 신청할 수 있습니다.")
    with authenticated_transaction() as db:
        def mutate(session):
            today = (now(session)+timedelta(hours=9)).date()
            if start < today:
                error(409, "LEAVE_PAST_DATE", "과거일 신청은 지원하지 않습니다.")
            assignment(session, g.user.id, required=True)
            holidays = year_data(session, start.year)
            available = totals(session, g.user.id, start.year)
            days = [start+timedelta(days=i) for i in range((end-start).days+1)]
            working = [d for d in days if d.weekday() < 5 and d not in holidays]
            if not working:
                error(409, "LEAVE_NON_WORKING_PERIOD", "근무일이 없는 기간입니다.")
            # Conservative proposal: even disjoint half-days on one date conflict.
            overlap = current(session, requests, requests.c.applicant_id == g.user.id,
                requests.c.status.in_(('pending', 'approved')), requests.c.start_date <= end, requests.c.end_date >= start)
            if overlap:
                error(409, "LEAVE_PERIOD_OVERLAP", "대기·승인된 신청 기간과 겹칩니다.")
            units = 0 if body['type'] in ('sick', 'official') else (1 if body['type'] in ('am_half', 'pm_half') else 2*len(working))
            if units > available['remaining_units']:
                error(409, "LEAVE_BALANCE_INSUFFICIENT", "잔여 연차가 부족합니다.")
            stamp = now(session);identifier = str(uuid.uuid4())
            session.execute(requests.insert().values(id=identifier, applicant_id=g.user.id, year=start.year,
                type=body['type'], start_date=start, end_date=end, charged_units=units, reason=body['reason'],
                status='pending', revision=1, decision_reason='', decision_by=None, created_at=stamp, updated_at=stamp))
            audit(SUBMIT, 'leave_request', identifier, {'charged_units': units, 'year': start.year})
            return WriteResult(receipt(current(session, requests, requests.c.id == identifier)), 201)
        result = perform(db, SUBMIT, 'new', body, mutate)
    return success(result.data, result.status)


@bp.post('/requests/<identifier>/decision')
@login_required
def decide(identifier):
    canonical(identifier)
    body = fields({'intent_id', 'expected_revision', 'decision', 'decision_reason'})
    canonical(body['intent_id']);revision = integer_string(body['expected_revision'])
    if body['decision'] not in ('approved', 'rejected'):
        error(400, 'INVALID_INPUT', '승인·반려 중 하나를 선택해 주세요.')
    reason(body['decision_reason'], mandatory=body['decision'] == 'rejected')
    with authenticated_transaction() as db:
        def mutate(session):
            item = current(session, requests, requests.c.id == identifier)
            link = assignment(session, item.applicant_id, required=True)
            if g.user.role != 'admin' or link.manager_id != g.user.id or item.applicant_id == g.user.id:
                error(403, 'FORBIDDEN', '현재 지정 담당 관리자만 검토할 수 있습니다.')
            totals(session, item.applicant_id, item.year)
            if item.revision != revision or item.status != 'pending':
                error(409, 'LEAVE_VERSION_CONFLICT', '현재 신청 상태·버전을 다시 확인해 주세요.')
            if revision == 9223372036854775807:
                error(409, 'LEAVE_VERSION_CONFLICT', '신청 버전 한도를 확인해 주세요.')
            session.execute(requests.update().where(requests.c.id == identifier).values(status=body['decision'],
                revision=revision+1, decision_reason=body['decision_reason'], decision_by=g.user.id, updated_at=now(session)))
            audit(DECIDE, 'leave_request', identifier, {'decision': body['decision'], 'revision': str(revision+1)})
            return WriteResult(receipt(current(session, requests, requests.c.id == identifier)), 200)
        result = perform(db, DECIDE, identifier, body, mutate)
    return success(result.data, result.status)


def query(allowed):
    if set(request.args)-set(allowed) or any(len(v) != 1 for _, v in request.args.lists()):
        error(400, 'INVALID_INPUT', '지원하지 않거나 중복된 조회 조건입니다.')


@bp.get('/requests')
@login_required
def listing():
    query({'scope', 'after', 'limit'})
    scope = request.args.get('scope', 'own');after = request.args.get('after');raw = request.args.get('limit', '50')
    if scope not in ('own', 'review') or not re.fullmatch(r'[1-9][0-9]{0,2}', raw) or int(raw) > 100:
        error(400, 'INVALID_INPUT', 'scope·limit를 확인해 주세요.')
    if after is not None:canonical(after)
    with authenticated_transaction() as db:
        storage(db)
        source = requests
        where = [requests.c.applicant_id == g.user.id]
        if scope == 'review':
            if g.user.role != 'admin':error(403, 'FORBIDDEN', '관리자 검토 목록입니다.')
            # Locking the outer query does not make an IN subquery a current
            # read under RR. Join the relation and active applicant directly,
            # before limit/cursor so revoked/inactive rows consume no page.
            source = requests.join(User.__table__, requests.c.applicant_id == User.id).join(
                reviewers, reviewers.c.applicant_id == requests.c.applicant_id)
            where = [reviewers.c.manager_id == g.user.id, User.is_active.is_(True),
                     requests.c.applicant_id != g.user.id]
        if after:where.append(requests.c.id > after)
        rows = db.execute(select(requests).select_from(source).where(*where).order_by(requests.c.id).limit(int(raw)+1).with_for_update()).mappings().all()
        data = {'items': [request_view(db, row) for row in rows[:int(raw)]],
                'next_cursor': rows[int(raw)-1].id if len(rows) > int(raw) else None, 'server_time': iso(now(db))}
    return success(data)


@bp.get('/requests/<identifier>')
@login_required
def detail(identifier):
    query(set());canonical(identifier)
    with authenticated_transaction() as db:
        authorization(db, identifier)
        data = request_view(db, current(db, requests, requests.c.id == identifier))
    return success(data)


@bp.get('/balance')
@login_required
def read_balance():
    query({'year'})
    with authenticated_transaction() as db:
        storage(db);year = request.args.get('year', str((now(db)+timedelta(hours=9)).year))
        if not re.fullmatch(r'[0-9]{4}', year) or not 2000 <= int(year) <= 9998:error(400, 'INVALID_INPUT', '유효한 연도가 필요합니다.')
        year_data(db, int(year));data = totals(db, g.user.id, int(year));data['policy_id'] = POLICY
    return success(data)


@bp.get('/operations/<intent_id>')
@login_required
def original_result(intent_id):
    query({'action', 'target'});canonical(intent_id)
    action, target = request.args.get('action'), request.args.get('target')
    if action not in (SUBMIT, DECIDE) or not isinstance(target, str) or (action == SUBMIT and target != 'new') or (action == DECIDE and target == 'new'):
        error(400, 'INVALID_INPUT', '원 action/target이 필요합니다.')
    with authenticated_transaction() as db:
        (authorization if action == SUBMIT else decision_authorization)(db, target)
        row = current(db, operations, operations.c.actor_id == g.user.id, operations.c.intent_id == intent_id)
        if row is None:data = {'state': 'not_recorded'}
        else:
            if row.action != action or row.target != target:error(409, 'LEAVE_INTENT_CONFLICT', '원 action/target이 다릅니다.')
            if row.expires_at <= now(db):error(410, 'LEAVE_INTENT_EXPIRED', '원 요청 결과 보관기간이 지났습니다.')
            data = dict(state='completed', http_status=row.http_status, result=json.loads(row.receipt_json), expires_at=iso(row.expires_at))
    return success(data)


@bp.get('/readiness')
@login_required
def readiness():
    query(set())
    with authenticated_transaction() as db:
        storage(db);stamp = now(db);year = (stamp+timedelta(hours=9)).year
        problems = []
        for check in (lambda: year_data(db, year), lambda: balance(db, g.user.id, year), lambda: assignment(db, g.user.id, required=True), write_policy):
            try:check()
            except AppError as exc:problems.append(exc.code)
        data = dict(schema_ready=True, write_ready=not problems, pending=problems, year=year, server_time=iso(stamp), policy_id=POLICY)
    return success(data)


def install(app):
    if not app.config.get('ENABLE_COMMON_WRITE_DRAFT'):
        raise RuntimeError('Leave requires explicit common W1 support')
    app.register_blueprint(bp)
    register_write_action(app, action=SUBMIT, authorize=authorization, result_fields=RECEIPT_FIELDS)
    register_write_action(app, action=DECIDE, authorize=decision_authorization, result_fields=RECEIPT_FIELDS)
    # Readers remain usable when the policy acknowledgement is removed.
    register_write_result_reader(app, action=SUBMIT, authorize=authorization)
    register_write_result_reader(app, action=DECIDE, authorize=decision_authorization)
