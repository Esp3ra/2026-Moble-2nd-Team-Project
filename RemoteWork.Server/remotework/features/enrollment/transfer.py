"""Explicit local operator transfer. No HTTP endpoint, sensor IO or automatic DDL."""
import json
import click
from flask import current_app
from sqlalchemy import BigInteger, CheckConstraint, ForeignKey, String, UniqueConstraint, inspect, select, text
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column
from datetime import datetime

from ... import enrollment_protocol_draft as wire
from ...common import AppError, audit
from ...db import transaction
from ...models import AdminGuard, User, ResourceGrant, iso, utcnow
from .models import Device, Job, Claim, Mapping


class TransferBase(DeclarativeBase):
    pass


class Transfer(TransferBase):
    __tablename__ = "enrollment_mapping_transfers"
    __table_args__ = (
        UniqueConstraint("mapping_id", "from_revision", name="uq_enrollment_transfer_revision"),
        CheckConstraint("from_revision > 0 AND to_revision = from_revision + 1 AND from_user_id <> to_user_id", name="ck_enrollment_transfer_values"),
        {"mysql_engine": "InnoDB", "mysql_charset": "utf8mb4", "mysql_collate": "utf8mb4_unicode_ci"})
    request_id: Mapped[str] = mapped_column(String(36), primary_key=True)
    mapping_id: Mapped[str] = mapped_column(ForeignKey(Mapping.mapping_id))
    actor_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    from_user_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    to_user_id: Mapped[int] = mapped_column(ForeignKey(User.id))
    from_revision: Mapped[int] = mapped_column(BigInteger)
    to_revision: Mapped[int] = mapped_column(BigInteger)
    reason: Mapped[str] = mapped_column(String(255))
    created_at: Mapped[datetime]


def fail(code, status=409):
    raise AppError(status, code, "이전 대상·권한·원 연결과 진행 중 작업을 확인해 주세요.")


def locked(db, model, *where):
    return db.scalar(select(model).where(*where).with_for_update().execution_options(populate_existing=True))


def require_schema(engine):
    """Reject the old four-column binding and incomplete/unsafe installations."""
    ins = inspect(engine)
    table = Transfer.__table__
    if engine.dialect.name != "mysql" or not ins.has_table(table.name):
        fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    cols = {c['name']: c for c in ins.get_columns(table.name)}
    if set(cols) != set(table.c.keys()) or any(cols[c.name]['nullable'] != c.nullable for c in table.c):
        fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    for col in table.c:
        actual = cols[col.name]['type']
        if (not isinstance(actual,type(col.type))
                or getattr(actual,'length',None) != getattr(col.type,'length',None)
                or getattr(actual,'unsigned',False)):
            fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    if ins.get_pk_constraint(table.name)['constrained_columns'] != ['request_id']:
        fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    if ('mapping_id', 'from_revision') not in {tuple(x['column_names']) for x in ins.get_unique_constraints(table.name)}:
        fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    for name, wanted in (
        ('enrollment_mappings', {(('enrollment_id','device_id','slot'), 'enrollment_jobs', ('enrollment_id','device_id','reserved_slot')),
                                 (('user_id',),'users',('id',)),(('device_id',),'enrollment_devices',('device_id',))}),
        (table.name, {(tuple(c.name for c in fk.columns), fk.referred_table.name, tuple(e.column.name for e in fk.elements)) for fk in table.foreign_key_constraints})):
        actual = ins.get_foreign_keys(name)
        valid = {(tuple(f['constrained_columns']),f['referred_table'],tuple(f['referred_columns'])) for f in actual
                 if not f.get('options',{}).get('ondelete') and not f.get('options',{}).get('onupdate')
                 and (f.get('referred_schema') or ins.default_schema_name) == ins.default_schema_name}
        if not wanted <= valid:
            fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
        if name == 'enrollment_mappings' and any(f['constrained_columns'] == ['enrollment_id','device_id','slot','user_id'] for f in actual):
            fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    unique={tuple(x['column_names']) for x in ins.get_unique_constraints('enrollment_mappings')}
    if not {('device_id','slot'),('device_id','user_id')} <= unique:
        fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    with engine.connect() as conn:
        checks=conn.execute(text("SELECT ENFORCED FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='enrollment_mapping_transfers' AND CONSTRAINT_TYPE='CHECK' AND CONSTRAINT_NAME='ck_enrollment_transfer_values'")).scalars().all()
        if checks != ['YES']:
            fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)


def view(row):
    return dict(request_id=row.request_id, mapping_id=row.mapping_id, from_user_id=row.from_user_id,
                to_user_id=row.to_user_id, from_revision=str(row.from_revision),
                to_revision=str(row.to_revision), transferred_at=iso(row.created_at))


def transfer(db, *, actor_login, from_login, to_login, device_id, slot, expected_revision, request_id, reason):
    """Caller owns the TX. Acquire the same global guard as identity/enrollment writers."""
    wire.canonical_uuid(request_id); wire.canonical_uuid(device_id)
    if (type(slot) is not int or slot < 0 or type(expected_revision) is not int
            or not 1 <= expected_revision < wire.BIGINT_MAX or type(reason) is not str
            or not 1 <= len(reason.strip()) <= 255 or from_login == to_login):
        fail("ENROLLMENT_TRANSFER_INVALID_INPUT", 400)
    if locked(db, AdminGuard, AdminGuard.id == 1) is None:
        fail("ENROLLMENT_TRANSFER_SCHEMA_REQUIRED", 503)
    users = {u.login_id: u for u in db.scalars(select(User).where(User.login_id.in_(
        [actor_login,from_login,to_login])).order_by(User.id).with_for_update().execution_options(populate_existing=True))}
    actor, old, new = (users.get(x) for x in (actor_login,from_login,to_login))
    if not actor or not actor.is_active or actor.role != 'admin':
        fail("ENROLLMENT_TRANSFER_FORBIDDEN", 403)
    if not old or not new or not old.is_active or not new.is_active:
        fail("ENROLLMENT_TRANSFER_USER_UNAVAILABLE")
    device = locked(db, Device, Device.device_id == device_id)
    if not device or not device.enabled or not device.minimum_slot <= slot <= device.maximum_slot:
        fail("ENROLLMENT_TRANSFER_DEVICE_UNAVAILABLE")
    if locked(db, ResourceGrant, ResourceGrant.user_id == actor.id,
              ResourceGrant.resource_type == 'enrollment_device', ResourceGrant.resource_id == device_id) is None:
        fail("ENROLLMENT_TRANSFER_FORBIDDEN", 403)
    mapping = locked(db, Mapping, Mapping.device_id == device_id, Mapping.slot == slot)
    if not mapping or not mapping.active:
        fail("ENROLLMENT_TRANSFER_MAPPING_CHANGED")
    previous = locked(db, Transfer, Transfer.request_id == request_id)
    if previous:
        if (previous.mapping_id,previous.actor_id,previous.from_user_id,previous.to_user_id,previous.from_revision,previous.reason) != (
                mapping.mapping_id,actor.id,old.id,new.id,expected_revision,reason.strip()):
            fail("ENROLLMENT_TRANSFER_REQUEST_CONFLICT")
        return view(previous)  # Permanent receipt; never repeats a transfer after later changes.
    if mapping.user_id != old.id or mapping.revision != expected_revision:
        fail("ENROLLMENT_TRANSFER_MAPPING_CHANGED")
    job = locked(db, Job, Job.enrollment_id == mapping.enrollment_id)
    claim = locked(db, Claim, Claim.device_id == device_id, Claim.slot == slot)
    if (not job or job.state != 'completed' or not job.result_hash or not job.result_json
            or (job.device_id,job.reserved_slot) != (device_id,slot) or not claim
            or claim.state != 'active' or claim.enrollment_id != job.enrollment_id):
        fail("ENROLLMENT_TRANSFER_EVIDENCE_REQUIRED")
    # Validate the full committed ownership chain against the immutable job subject.
    owner, revision = job.target_user_id, 1
    for row in db.scalars(select(Transfer).where(Transfer.mapping_id == mapping.mapping_id)
            .order_by(Transfer.from_revision).with_for_update().execution_options(populate_existing=True)):
        if (row.from_user_id,row.from_revision,row.to_revision) != (owner,revision,revision+1):
            fail("ENROLLMENT_TRANSFER_HISTORY_INVALID")
        owner,revision = row.to_user_id,row.to_revision
    if (owner,revision) != (mapping.user_id,mapping.revision):
        fail("ENROLLMENT_TRANSFER_HISTORY_INVALID")
    if locked(db, Mapping, Mapping.device_id == device_id, Mapping.user_id == new.id):
        fail("ENROLLMENT_TRANSFER_TARGET_ALREADY_LINKED")
    if locked(db, Job, Job.device_id == device_id, Job.state == 'issued'):
        fail("ENROLLMENT_TRANSFER_BUSY")
    from ..identity.models import Challenge, Context
    from ..identity.attendance import WorkSession
    if locked(db, Challenge, Challenge.device_id == device_id,
              (Challenge.state == 'issued') | ((Challenge.state == 'verified') & Challenge.workflow_completed.is_(False))):
        fail("ENROLLMENT_TRANSFER_BUSY")
    if locked(db, WorkSession, WorkSession.user_id.in_([old.id,new.id]), WorkSession.ended_at.is_(None)):
        fail("ENROLLMENT_TRANSFER_BUSY")
    row = Transfer(request_id=request_id,mapping_id=mapping.mapping_id,actor_id=actor.id,
        from_user_id=old.id,to_user_id=new.id,from_revision=mapping.revision,to_revision=mapping.revision+1,
        reason=reason.strip(),created_at=utcnow().replace(microsecond=0))
    mapping.user_id,mapping.revision = new.id,row.to_revision
    db.add(row)
    # Only unused pending login capabilities are revoked. Completed receipt contexts,
    # ordinary sessions, original evidence and closed attendance remain intact.
    for ctx in db.scalars(select(Context).where(Context.user_id.in_([old.id,new.id]),
            Context.token_hash.is_not(None), Context.receipt_only.is_(False)).with_for_update()):
        ctx.revoked = True
    db.flush()
    audit('enrollment.mapping_transferred_cli','enrollment_mapping',mapping.mapping_id,
          view(row) | {'reason':row.reason,'sensor_changed':False},actor_id=actor.id)
    db.flush()
    return view(row)


def retained_completed_binding(db, ctx, item, mapping, ticket):
    """Old owner may replay ONLY an already completed original identity result."""
    if not item.workflow_completed or item.state != 'verified' or not item.result_bytes or not item.receipt_json:
        return False
    engine = current_app.extensions['db_engine']
    if not inspect(engine).has_table(Transfer.__tablename__):
        return False
    require_schema(engine)
    row = locked(db, Transfer, Transfer.mapping_id == mapping.mapping_id,
                 Transfer.from_revision == int(ticket['mapping_revision']),Transfer.from_user_id == ctx.user_id)
    return row is not None and mapping.revision >= row.to_revision


def install(app):
    @app.cli.command('transfer-enrollment-owner')
    @click.option('--actor-login',required=True)
    @click.option('--from-login',required=True)
    @click.option('--to-login',required=True)
    @click.option('--device-id',required=True)
    @click.option('--slot',type=int,required=True)
    @click.option('--expected-revision',type=int,required=True)
    @click.option('--request-id',required=True)
    @click.option('--reason',required=True)
    def command(**kwargs):
        """After reviewed migration and Pi stop/drain; never touches sensor or keys."""
        try:
            require_schema(app.extensions['db_engine'])
            with transaction() as db:
                answer=transfer(db,**kwargs)
            click.echo(json.dumps(answer))
        except (AppError,wire.ProtocolError) as error:
            raise click.ClickException(getattr(error,'code',str(error))) from error
