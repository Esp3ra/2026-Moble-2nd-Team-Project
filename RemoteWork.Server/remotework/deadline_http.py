"""Member 1 same-TX deadline API. Explicit opt-in; never creates DB/baselines."""
from datetime import datetime, timedelta
import json

from flask import Blueprint, g, request
from sqlalchemy import BigInteger, DateTime, Integer, String, column, inspect, select, table, text

from .auth import authenticated_transaction, login_required
from .common import AppError, audit, success
from .models import iso
from . import deadline_events as events, deadline_bindings as bindings
from .features.tasks.deadline_facts import read_own_deadline_fact, read_own_deadline_facts
from .write_contract import positive_bigint
from .write_requests import WriteResult, perform_once, register_write_action, register_write_result_reader

ACTION = "deadline_alert.confirmed"
bp = Blueprint("deadline_alerts", __name__, url_prefix="/api/v1/deadline-alerts")
confirmations = table("deadline_binding_confirmations_draft",
    column("event_id", String(36)), column("user_id", Integer),
    column("assignment_revision", Integer), column("task_id", BigInteger),
    column("confirmed_at", DateTime))


def storage(db):
    events.storage(db)
    i = inspect(db.connection())
    name = confirmations.name
    if not i.has_table(name):
        events.unavailable("DEADLINE_CONFIRMATION_STORAGE_REQUIRED")
    columns = {c['name']: c for c in i.get_columns(name)}
    expected = {'event_id': 'VARCHAR', 'user_id': 'INTEGER', 'assignment_revision': 'INTEGER',
                'task_id': 'BIGINT', 'confirmed_at': 'DATETIME'}
    fks = i.get_foreign_keys(name)
    wanted = {(("task_id", "event_id"), events.journal.name, ("task_id", "event_id")),
              (("task_id", "assignment_revision"), "task_assignments", ("task_id", "revision")),
              (("user_id",), "users", ("id",))}
    if (set(columns) != set(expected)
        or any(type(columns[n]['type']).__name__ != t or columns[n]['nullable']
               or getattr(columns[n]['type'], 'unsigned', False) for n,t in expected.items())
        or columns['event_id']['type'].length != 36 or columns['event_id']['type'].collation != 'ascii_bin'
        or getattr(columns['confirmed_at']['type'], 'fsp', None) != 6
        or i.get_pk_constraint(name)['constrained_columns'] != ['event_id','user_id','assignment_revision']
        or str(i.get_table_options(name).get('mysql_engine','')).lower() != 'innodb'
        or not any(x['column_names'] == ['user_id','confirmed_at'] for x in i.get_indexes(name))
        or len(fks) != len(wanted)
        or {(tuple(f['constrained_columns']),f['referred_table'],tuple(f['referred_columns'])) for f in fks} != wanted
        or any(f.get('referred_schema') not in (None,i.default_schema_name)
               or any(str(f.get('options',{}).get(k,'RESTRICT')).upper() not in ('RESTRICT','NO ACTION')
                      for k in ('ondelete','onupdate')) for f in fks)):
        events.unavailable("DEADLINE_CONFIRMATION_STORAGE_REQUIRED")


def server_utc(db):
    events.guard(db)
    return db.scalar(text('SELECT UTC_TIMESTAMP(6)'))


def stored(db, binding):
    row = db.execute(select(confirmations).where(confirmations.c.event_id == binding.event_id,
        confirmations.c.user_id == g.user.id,
        confirmations.c.assignment_revision == binding.assignment_revision).with_for_update()).mappings().first()
    if row is not None and row.task_id != binding.task_id:
        events.unavailable('DEADLINE_CONFIRMATION_INCONSISTENT')
    return row


def snapshot(db):
    storage(db)
    stamp = server_utc(db)
    items = []
    for fact in read_own_deadline_facts(db):
        change = events.read_current(db, task_id=fact.task_id)
        if change.source_row_version > fact.row_version:
            events.unavailable('DEADLINE_EVENT_SOURCE_MISMATCH')
        binding = bindings.DeadlineBinding(fact.task_id, change.event_id, fact.assignment_revision)
        row = stored(db, binding)
        items.append(dict(event_id=change.event_id, target=bindings.target(binding), task_id=str(fact.task_id),
            assignment_revision=fact.assignment_revision, assignee_id=fact.assignee_id, title=fact.title,
            status=fact.task_status, cancelled=fact.is_cancelled, due_date=fact.due_date.isoformat(),
            row_version=str(fact.row_version), confirmed_at=iso(row.confirmed_at) if row else None))
    return dict(server_time=iso(stamp), items=items)


@bp.get('')
@bp.get('/<target>')
@login_required
def read(target=None):
    if request.args:
        raise AppError(400,'INVALID_INPUT','마감 목록은 페이지·필터를 받지 않습니다.')
    with authenticated_transaction() as db:
        if target is not None:
            bindings.read_bound_event(db,target)
        result = snapshot(db)  # Full replacement, never singleton masquerading as snapshot.
    return success(result)


def body():
    def unique(pairs):
        value = {}
        for k,v in pairs:
            if k in value:
                raise ValueError()
            value[k]=v
        return value
    try:
        if request.args or not request.is_json:
            raise ValueError()
        value=json.loads(request.get_data().decode('utf-8'),object_pairs_hook=unique,
            parse_constant=lambda _: (_ for _ in ()).throw(ValueError()))
        if type(value) is not dict or set(value) != {'task_id','expected_row_version','expected_assignment_revision'}:
            raise ValueError()
        positive_bigint(value['task_id'],'task_id')
        positive_bigint(value['expected_row_version'],'expected_row_version')
        revision=value['expected_assignment_revision']
        if type(revision) is not int or not 1 <= revision <= 2147483647:
            raise ValueError()
    except (ValueError,UnicodeError,RecursionError):
        raise AppError(400,'INVALID_INPUT','중복 없는 원 업무·버전·정수 배정 차수를 확인해 주세요.') from None
    return value


@bp.post('/<target>/confirm')
@login_required
def confirm(target):
    original=bindings.parse_target(target)
    value=body()
    if value['task_id'] != str(original.task_id) or value['expected_assignment_revision'] != original.assignment_revision:
        raise AppError(400,'INVALID_INPUT','원 확인 대상과 본문이 다릅니다.')
    with authenticated_transaction() as db:
        def mutate(db):
            storage(db)
            fact=read_own_deadline_fact(db,original.task_id)
            current=bindings.current_binding(db,task_id=original.task_id)
            if (current != original or str(fact.row_version) != value['expected_row_version']
                or fact.is_cancelled or fact.task_status not in ('not_started','in_progress','returned')):
                raise AppError(409,'DEADLINE_EVENT_CHANGED','사건·배정·업무 상태가 변경됐습니다. 다시 조회해 주세요.')
            stamp=server_utc(db)
            # Previous day09 KST == previous day00 UTC; overdue confirmation allowed.
            if stamp < datetime.combine(fact.due_date-timedelta(days=1),datetime.min.time()):
                raise AppError(409,'DEADLINE_NOT_OCCURRED','아직 마감 알림 확인 시각이 아닙니다.')
            row=stored(db,original)
            if row is None:
                db.execute(confirmations.insert().values(event_id=original.event_id,user_id=g.user.id,
                    task_id=original.task_id,assignment_revision=original.assignment_revision,confirmed_at=stamp))
                audit(ACTION,'deadline_alert',target)
            else:
                stamp=row.confirmed_at
            return WriteResult(dict(task_id=str(original.task_id),event_id=original.event_id,
                assignment_revision=original.assignment_revision,confirmed_at=iso(stamp)))
        result=perform_once(db,key=request.headers.get('Idempotency-Key'),action=ACTION,target=target,
            body=value,authorize=bindings.authorize_result,mutate=mutate)
    return success(result.data,result.status)


def install(app, *, writes=False):
    """Explicit same-source installation; OFF installs retained W1 reader only."""
    if app._got_first_request or 'deadline_http' in app.extensions:
        raise RuntimeError('Install deadline HTTP exactly once before first request')
    if (type(writes) is not bool or app.extensions['db_engine'].dialect.name != 'mysql'
        or not app.config.get('ENABLE_COMMON_WRITE_DRAFT') or not app.config.get('ENABLE_TASK_QUERY_DRAFT')):
        raise RuntimeError('Deadline HTTP requires MySQL, common W1 and task query support')
    register_write_result_reader(app,action=ACTION,authorize=bindings.authorize_result,read_only=not writes)
    if writes:
        register_write_action(app,action=ACTION,authorize=bindings.authorize_result,
            result_fields={'task_id','event_id','assignment_revision','confirmed_at'})
        app.register_blueprint(bp)
    app.extensions['deadline_http']=writes
