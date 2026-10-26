"""Explicit past-day exception ledger and current-authority attendance reader.

No automatic DDL, sensor IO, fabricated clock-out, receipt or history deletion.
All writes run inside the caller's guard -> actor transaction.
"""
from datetime import date, datetime, timedelta
import re
from uuid import UUID

from flask import Blueprint, current_app, g, request, has_app_context
from sqlalchemy import CheckConstraint, ForeignKey, String, inspect, select, tuple_
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column

from ...auth import authenticated_transaction, login_required
from ...common import audit, success
from ...models import User, iso
from .attendance import WorkSession
from .models import Context, Challenge

bp = Blueprint("attendance_history", __name__, url_prefix="/api/v1/attendance")


class ExceptionBase(DeclarativeBase):
    pass


class AttendanceException(ExceptionBase):
    __tablename__ = "identity_attendance_exceptions"
    __table_args__ = (
        CheckConstraint("BINARY state = 'absent'", name="ck_attendance_exception_state"),
        CheckConstraint("BINARY reason_code = 'missed_clock_out'", name="ck_attendance_exception_reason"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_bin"})
    work_session_id: Mapped[str] = mapped_column(ForeignKey(WorkSession.id), primary_key=True)
    state: Mapped[str] = mapped_column(String(16))
    reason_code: Mapped[str] = mapped_column(String(32))
    recorded_at: Mapped[datetime]


def exception_tables():
    # Once installed, recorded exceptions remain effective even if new writes
    # are disabled. A partially installed ledger fails the ordinary validator.
    if not has_app_context():
        return ()
    if (current_app.config.get("MISSED_CLOCKOUT_POLICY") == "absence-v1"
            or inspect(current_app.extensions["db_engine"]).has_table(AttendanceException.__tablename__)):
        return (AttendanceException.__table__,)
    return ()


def classified_ids(db, rows, day):
    from .routes import fail
    if not rows or not exception_tables():
        return set()
    by_id = {row.id: row for row in rows}
    entries = db.scalars(select(AttendanceException).where(
        AttendanceException.work_session_id.in_(by_id)).with_for_update()
        .execution_options(populate_existing=True)).all()
    for entry in entries:
        row = by_id[entry.work_session_id]
        if (entry.state != "absent" or entry.reason_code != "missed_clock_out"
                or row.ended_at is not None or row.work_date >= day):
            fail(503, "IDENTITY_STORAGE_NOT_READY")
    return {entry.work_session_id for entry in entries}


def classify_missed_clockouts(db, user):
    from .routes import now, fail, storage
    if current_app.config.get("MISSED_CLOCKOUT_POLICY") != "absence-v1":
        return
    storage(additional_tables=(AttendanceException.__table__,))
    stamp = now()
    day = (stamp + timedelta(hours=9)).date().isoformat()
    rows = db.scalars(select(WorkSession).where(WorkSession.user_id == user.id,
        WorkSession.work_date < day, WorkSession.ended_at.is_(None))
        .order_by(WorkSession.work_date, WorkSession.id).with_for_update()
        .execution_options(populate_existing=True)).all()
    if not rows:
        return
    settled = classified_ids(db, rows, day)
    targets = [row for row in rows if row.id not in settled]
    if not targets:
        return
    # Do not revoke a previous valid capture/result to make a new login pass.
    # An unexpired operation from a past KST day must recover/expire first.
    active = db.scalar(select(Challenge.id).join(Context, Context.id == Challenge.context_id).where(
        Context.user_id == user.id, Challenge.state.in_(("issued", "verified")),
        Challenge.workflow_completed.is_(False), Challenge.expires_at > stamp,
        Challenge.created_at < datetime.combine(date.fromisoformat(day), datetime.min.time()) - timedelta(hours=9))
        .with_for_update().limit(1))
    if active is not None:
        fail(409, "ATTENDANCE_PREVIOUS_OPERATION_PENDING")
    for row in targets:
        db.add(AttendanceException(work_session_id=row.id, state="absent",
            reason_code="missed_clock_out", recorded_at=stamp))
        audit("attendance.missed_clock_out", "identity_work_session", row.id,
            {"work_date": row.work_date, "state": "absent", "reason_code": "missed_clock_out"}, actor_id=user.id)
    db.flush()


def _arguments(day):
    from .routes import fail
    allowed = {"scope", "user_id", "from_date", "to_date", "limit", "cursor"}
    if set(request.args) - allowed or any(len(request.args.getlist(k)) != 1 for k in request.args):
        fail(400, "ATTENDANCE_INVALID_QUERY")
    value = {k: request.args[k] for k in request.args}
    scope = value.get("scope", "self")
    if scope not in ("self", "all"):
        fail(400, "ATTENDANCE_INVALID_QUERY")
    def number(name, default, maximum):
        raw = value.get(name, str(default))
        if re.fullmatch(r"[1-9][0-9]{0,9}", raw) is None or int(raw) > maximum:
            fail(400, "ATTENDANCE_INVALID_QUERY")
        return int(raw)
    uid = number("user_id", g.user.id, 2147483647)
    if g.user.role != "admin" and (scope != "self" or uid != g.user.id):
        fail(403, "IDENTITY_NOT_ALLOWED")
    if scope == "self" and uid != g.user.id:
        fail(400, "ATTENDANCE_INVALID_QUERY")
    try:
        start = date.fromisoformat(value.get("from_date", (day - timedelta(days=30)).isoformat()))
        end = date.fromisoformat(value.get("to_date", day.isoformat()))
        if start.isoformat() != value.get("from_date", start.isoformat()) or end.isoformat() != value.get("to_date", end.isoformat()):
            raise ValueError()
        if not 0 <= (end - start).days <= 365:
            raise ValueError()
    except ValueError:
        fail(400, "ATTENDANCE_INVALID_QUERY")
    cursor = value.get("cursor")
    if cursor is not None:
        try:
            if str(UUID(cursor)) != cursor:
                raise ValueError()
        except ValueError:
            fail(400, "ATTENDANCE_INVALID_QUERY")
    return scope, (uid if scope == "self" or "user_id" in value else None), start, end, number("limit", 50, 100), cursor


@bp.get("/records")
@login_required
def records():
    from .routes import now, fail, storage
    if g.auth_session.channel != "api":
        fail(403, "IDENTITY_NOT_ALLOWED")
    with authenticated_transaction() as db:
        storage(additional_tables=(AttendanceException.__table__,))
        stamp = now()
        day = (stamp + timedelta(hours=9)).date()
        scope, uid, start, end, limit, cursor = _arguments(day)
        filters = [WorkSession.work_date >= start.isoformat(), WorkSession.work_date <= end.isoformat()]
        if uid is not None:
            filters.append(WorkSession.user_id == uid)
        if cursor is not None:
            previous = db.scalar(select(WorkSession).where(*filters, WorkSession.id == cursor).with_for_update())
            if previous is None:
                fail(400, "ATTENDANCE_INVALID_QUERY")
            filters.append(tuple_(WorkSession.work_date, WorkSession.started_at, WorkSession.id)
                < (previous.work_date, previous.started_at, previous.id))
        rows = db.execute(select(WorkSession, User, AttendanceException).join(User, User.id == WorkSession.user_id)
            .outerjoin(AttendanceException, AttendanceException.work_session_id == WorkSession.id)
            .where(*filters).order_by(WorkSession.work_date.desc(), WorkSession.started_at.desc(), WorkSession.id.desc())
            .limit(limit + 1).with_for_update().execution_options(populate_existing=True)).all()
        data = []
        for work, user, exception in rows[:limit]:
            if exception and (exception.state != "absent" or exception.reason_code != "missed_clock_out"
                    or work.ended_at is not None or work.work_date >= day.isoformat()):
                fail(503, "IDENTITY_STORAGE_NOT_READY")
            state = ("absent" if exception else "closed" if work.ended_at is not None else
                "open" if work.work_date == day.isoformat() else "review_required")
            data.append(dict(work_session_ref=work.id, user_id=user.id, display_name=user.display_name,
                department=user.department, work_date=work.work_date, state=state,
                reason_code=exception.reason_code if exception else None,
                started_at=iso(work.started_at), noon_at=iso(work.noon_at) if work.noon_at else None,
                ended_at=iso(work.ended_at) if work.ended_at else None,
                recorded_at=iso(exception.recorded_at) if exception else None))
        answer = dict(records=data, scope=scope, user_id=uid, from_date=start.isoformat(), to_date=end.isoformat(),
            server_time=iso(stamp), next_cursor=data[-1]["work_session_ref"] if len(rows) > limit else None)
    return success(answer)
