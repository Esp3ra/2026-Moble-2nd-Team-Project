"""Explicit local operator preparation. No DDL or calendar download/seed.

Use a reviewed private JSON manifest after policy/reviewer confirmation. Initial
calendar and entitlements are immutable here; adjustments require a separate
agreement/tool. Existing matching settings are a safe no-op.
"""
import json
from sqlalchemy import select
from .common import AppError, audit
from .models import AdminGuard, User
from . import leave_service as service


def prepare(db, manifest, *, operator_login):
    if not isinstance(manifest, dict) or set(manifest) != {'year', 'policy_id', 'holidays', 'members'}:
        raise ValueError('Exact year/policy_id/holidays/members manifest required')
    year = manifest['year']
    if type(year) is not int or not 2000 <= year <= 9998 or manifest['policy_id'] != service.POLICY:
        raise ValueError('Explicit reviewed policy/year required')
    dates = manifest['holidays']
    if (not isinstance(dates, list) or len(dates) > 366 or any(not isinstance(d, str) for d in dates)
            or len(set(dates)) != len(dates) or any(service.parse_date(d).year != year for d in dates)):
        raise ValueError('A complete explicit calendar is required, even when empty')
    members = manifest['members']
    if not isinstance(members, list) or not members or len(members) > 1000:
        raise ValueError('Explicit member list required')
    if db.scalar(select(AdminGuard).where(AdminGuard.id == 1).with_for_update()) is None:
        raise ValueError('Common guard missing')
    operator = db.scalar(select(User).where(User.login_id == operator_login).with_for_update().execution_options(populate_existing=True))
    if operator is None or not operator.is_active or operator.role != 'admin':
        raise ValueError('Named active administrator operator required')
    service.storage(db)
    planned = []
    for row in members:
        if not isinstance(row, dict) or set(row)-{'applicant_id', 'manager_id', 'granted_units'} or not {'applicant_id', 'manager_id'} <= set(row):
            raise ValueError('Explicit applicant/manager IDs required')
        applicant, manager, units = row['applicant_id'], row['manager_id'], row.get('granted_units', 16)
        if (type(applicant) is not int or type(manager) is not int or min(applicant, manager) < 1
                or applicant == manager or type(units) is not int or not 0 <= units <= 732):
            raise ValueError('Invalid member/manager/half-day units')
        people = {p.id:p for p in db.scalars(select(User).where(User.id.in_([applicant, manager])).order_by(User.id).with_for_update().execution_options(populate_existing=True))}
        if len(people) != 2 or any(not p.is_active for p in people.values()) or people[manager].role != 'admin':
            raise ValueError('Current active applicant and administrator manager required')
        if applicant in [p[0] for p in planned]:raise ValueError('Duplicate applicant')
        planned.append((applicant, manager, units))
    calendar = service.current(db, service.years, service.years.c.year == year)
    holidays = json.dumps(sorted(dates), separators=(',', ':'))
    if calendar and (calendar.policy_id != service.POLICY or calendar.holidays_json != holidays):
        raise ValueError('Existing calendar is immutable; separate review required')
    for applicant, manager, units in planned:
        balance = service.current(db, service.balances, service.balances.c.applicant_id == applicant, service.balances.c.year == year)
        link = service.current(db, service.reviewers, service.reviewers.c.applicant_id == applicant)
        if balance and balance.granted_units != units:raise ValueError('Existing entitlement cannot be overwritten')
        if link and link.manager_id != manager:raise ValueError('Existing reviewer cannot be reassigned by initial preparation')
    if not calendar:
        db.execute(service.years.insert().values(year=year, policy_id=service.POLICY, holidays_json=holidays, configured_at=service.now(db)))
    changed = []
    for applicant, manager, units in planned:
        if not service.current(db, service.balances, service.balances.c.applicant_id == applicant, service.balances.c.year == year):
            db.execute(service.balances.insert().values(applicant_id=applicant, year=year, granted_units=units));changed.append(applicant)
        if not service.current(db, service.reviewers, service.reviewers.c.applicant_id == applicant):
            db.execute(service.reviewers.insert().values(applicant_id=applicant, manager_id=manager, revision=1))
        service.totals(db, applicant, year)
    if not calendar or changed:
        audit('leave.prepared', 'leave_year', str(year), {'member_ids': sorted(changed), 'policy_id': service.POLICY}, actor_id=operator.id)
    return {'year': year, 'new_balances': len(changed), 'policy_id': service.POLICY}


def main():
    import argparse
    from pathlib import Path
    from . import create_app
    from .db import transaction
    parser = argparse.ArgumentParser(description='Explicit initial leave preparation; no DDL/implicit assignments')
    parser.add_argument('--manifest', required=True, type=Path)
    parser.add_argument('--operator-login', required=True)
    parser.add_argument('--ack-policy', required=True, choices=[service.POLICY])
    args = parser.parse_args()
    app = create_app()
    with app.app_context(), transaction() as db:
        result = prepare(db, json.loads(args.manifest.read_text(encoding='utf-8-sig')), operator_login=args.operator_login)
    print(json.dumps(result))


if __name__ == '__main__':main()
