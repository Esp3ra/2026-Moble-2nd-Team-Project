"""Unapplied start plans: no application, DB, HTTP, current clock or metadata."""
from dataclasses import FrozenInstanceError, replace
from datetime import date, datetime, timedelta, timezone
import subprocess
import sys

import pytest

from remotework.features.tasks.start_transition import (
    TaskStartAction, TaskStartDecision, TaskStartPlan, TaskStartResult,
    TaskStartSnapshot, prepare_start_transition,
)


NOW = datetime(2026, 10, 19, 3, 4, 5, 123456, tzinfo=timezone.utc)
ACCEPTED = datetime(2026, 10, 18, 16, 0, 0, tzinfo=timezone.utc)


def snapshot(**changes):
    values = dict(task_status="not_started", row_version=7, assignment_revision=3,
                  start_date=date(2026, 10, 19), due_date=date(2026, 10, 22),
                  accepted_at=None)
    values.update(changes)
    return TaskStartSnapshot(**values)


def prepare(current=None, **changes):
    values = dict(action=TaskStartAction.ACCEPT, expected_assignment_revision=3,
                  expected_row_version=7, now_utc=NOW)
    values.update(changes)
    return prepare_start_transition(snapshot() if current is None else current, **values)


def test_accept_plan_sets_first_acceptance_and_preserves_assignment():
    current = snapshot()
    assert prepare(current) == TaskStartResult(TaskStartDecision.READY, TaskStartPlan(
        task_status="in_progress", row_version=8, assignment_revision=3,
        accepted_at=NOW, updated_at=NOW))
    assert current == snapshot()  # A plan does not mutate the input snapshot.


def test_rework_preserves_exact_first_acceptance_instead_of_reaccepting():
    current = snapshot(task_status="returned", accepted_at=ACCEPTED)
    result = prepare(current, action=TaskStartAction.REWORK_START)
    assert result.decision is TaskStartDecision.READY
    assert result.plan == TaskStartPlan("in_progress", 8, 3, ACCEPTED, NOW)
    assert result.plan.accepted_at is ACCEPTED
    assert current.task_status == "returned" and current.row_version == 7


@pytest.mark.parametrize("action", list(TaskStartAction))
@pytest.mark.parametrize("state", ["not_started", "in_progress", "submitted", "approved", "returned"])
def test_only_exact_source_state_can_prepare(action, state):
    current = snapshot(task_status=state, accepted_at=None if state == "not_started" else ACCEPTED)
    expected = (TaskStartDecision.READY if (action, state) in (
        (TaskStartAction.ACCEPT, "not_started"), (TaskStartAction.REWORK_START, "returned"))
        else TaskStartDecision.STATE_NOT_ALLOWED)
    result = prepare(current, action=action)
    assert result.decision is expected
    assert (result.plan is not None) == (expected is TaskStartDecision.READY)


@pytest.mark.parametrize(("assignment", "row", "expected"), [
    (2, 6, TaskStartDecision.ASSIGNMENT_CHANGED),
    (2, 7, TaskStartDecision.ASSIGNMENT_CHANGED),
    (3, 6, TaskStartDecision.TASK_CHANGED),
])
def test_assignment_conflict_precedes_task_and_state_and_period(assignment, row, expected):
    current = snapshot(task_status="approved", accepted_at=ACCEPTED)
    result = prepare(current, expected_assignment_revision=assignment,
                     expected_row_version=row,
                     now_utc=datetime(2026, 10, 23, tzinfo=timezone.utc))
    assert result == TaskStartResult(expected)


@pytest.mark.parametrize(("instant", "expected"), [
    ("2026-10-18T14:59:59.999999+00:00", TaskStartDecision.BEFORE_START),
    ("2026-10-18T15:00:00+00:00", TaskStartDecision.READY),
    ("2026-10-22T14:59:59.999999+00:00", TaskStartDecision.READY),
    ("2026-10-22T15:00:00+00:00", TaskStartDecision.AFTER_DUE),
])
@pytest.mark.parametrize("action", list(TaskStartAction))
def test_start_and_rework_share_inclusive_korean_dates(instant, expected, action):
    # Historical acceptance can precede a later adjusted start date.
    historical = datetime(2026, 10, 17, tzinfo=timezone.utc)
    current = snapshot(task_status="returned", accepted_at=historical) if (
        action is TaskStartAction.REWORK_START) else snapshot()
    result = prepare(current, action=action, now_utc=datetime.fromisoformat(instant))
    assert result.decision is expected
    assert (result.plan is not None) == (expected is TaskStartDecision.READY)


def test_state_failure_precedes_date_failure():
    assert prepare(snapshot(task_status="approved", accepted_at=ACCEPTED),
                   now_utc=datetime(2026, 10, 23, tzinfo=timezone.utc)) == TaskStartResult(
                       TaskStartDecision.STATE_NOT_ALLOWED)


@pytest.mark.parametrize("action", list(TaskStartAction))
def test_signed_bigint_last_increment_and_assignment_integer_maximum(action):
    current = snapshot(row_version=(1 << 63) - 2, assignment_revision=(1 << 31) - 1,
                       task_status="returned" if action is TaskStartAction.REWORK_START else "not_started",
                       accepted_at=ACCEPTED if action is TaskStartAction.REWORK_START else None)
    result = prepare(current, action=action, expected_row_version=(1 << 63) - 2,
                     expected_assignment_revision=(1 << 31) - 1)
    assert result.plan.row_version == (1 << 63) - 1
    assert result.plan.assignment_revision == (1 << 31) - 1


def test_signed_bigint_overflow_is_invalid_data_without_wrapping_or_clamping():
    with pytest.raises(ValueError, match="cannot be incremented"):
        prepare(snapshot(row_version=(1 << 63) - 1), expected_row_version=(1 << 63) - 1)


@pytest.mark.parametrize("field", ["row_version", "assignment_revision"])
@pytest.mark.parametrize("invalid", [True, False, "7", 1.0, None])
def test_snapshot_versions_do_not_coerce(field, invalid):
    with pytest.raises(TypeError):
        prepare(snapshot(**{field: invalid}))


@pytest.mark.parametrize("field", ["expected_row_version", "expected_assignment_revision"])
@pytest.mark.parametrize("invalid", [True, "7", 1.0, None])
def test_expected_versions_do_not_coerce(field, invalid):
    with pytest.raises(TypeError):
        prepare(**{field: invalid})


@pytest.mark.parametrize(("field", "invalid"), [
    ("row_version", 0), ("row_version", -1), ("row_version", 1 << 63),
    ("assignment_revision", 0), ("assignment_revision", -1), ("assignment_revision", 1 << 31),
])
def test_snapshot_version_ranges_are_not_clamped(field, invalid):
    with pytest.raises(ValueError):
        prepare(snapshot(**{field: invalid}))


@pytest.mark.parametrize(("field", "invalid"), [
    ("expected_row_version", 0), ("expected_row_version", -1), ("expected_row_version", 1 << 63),
    ("expected_assignment_revision", 0), ("expected_assignment_revision", -1),
    ("expected_assignment_revision", 1 << 31),
])
def test_expected_version_ranges_are_not_clamped(field, invalid):
    with pytest.raises(ValueError):
        prepare(**{field: invalid})


@pytest.mark.parametrize("action", ["accept", "rework_start", None, True])
def test_action_must_be_explicit_internal_enum(action):
    with pytest.raises(TypeError):
        prepare(action=action)


@pytest.mark.parametrize("invalid", [None, {}, "not_started", True])
def test_snapshot_is_not_inferred_from_arbitrary_input(invalid):
    with pytest.raises(TypeError):
        prepare_start_transition(invalid, action=TaskStartAction.ACCEPT,
                                 expected_assignment_revision=3, expected_row_version=7, now_utc=NOW)


@pytest.mark.parametrize("invalid", [None, True, 1])
def test_status_type_is_not_coerced(invalid):
    with pytest.raises(TypeError):
        prepare(snapshot(task_status=invalid))


@pytest.mark.parametrize("invalid", ["done", "RETURNED", "", "in progress"])
def test_unknown_state_is_invalid_instead_of_new_state(invalid):
    with pytest.raises(ValueError):
        prepare(snapshot(task_status=invalid))


@pytest.mark.parametrize(("state", "accepted"), [
    ("not_started", ACCEPTED), ("returned", None),
    ("returned", NOW + timedelta(microseconds=1)),
])
def test_inconsistent_acceptance_is_never_repaired(state, accepted):
    with pytest.raises(ValueError):
        prepare(snapshot(task_status=state, accepted_at=accepted))


@pytest.mark.parametrize("field", ["now_utc", "accepted_at"])
@pytest.mark.parametrize("invalid", ["2026-10-19T00:00:00Z", 0, date(2026, 10, 19)])
def test_timestamp_types_are_not_coerced(field, invalid):
    current = snapshot(task_status="returned", accepted_at=ACCEPTED)
    with pytest.raises(TypeError):
        prepare(replace(current, accepted_at=invalid)) if field == "accepted_at" else prepare(
            current, now_utc=invalid)


@pytest.mark.parametrize("field", ["now_utc", "accepted_at"])
@pytest.mark.parametrize("invalid", [
    datetime(2026, 10, 19),
    datetime(2026, 10, 19, tzinfo=timezone(timedelta(hours=9))),
    datetime(2026, 10, 19, tzinfo=timezone(timedelta(hours=-4))),
    datetime(999, 12, 31, tzinfo=timezone.utc),
])
def test_naive_non_utc_and_unstorable_instants_are_not_implicitly_normalized(field, invalid):
    current = snapshot(task_status="returned", accepted_at=ACCEPTED)
    with pytest.raises(ValueError):
        prepare(replace(current, accepted_at=invalid)) if field == "accepted_at" else prepare(
            current, now_utc=invalid)


@pytest.mark.parametrize(("start", "due", "error"), [
    ("2026-10-19", date(2026, 10, 22), TypeError),
    (date(2026, 10, 19), datetime(2026, 10, 22), TypeError),
    (date(2026, 10, 23), date(2026, 10, 22), ValueError),
    (date(999, 12, 31), date(2026, 10, 22), ValueError),
])
def test_calendar_validation_is_reused_without_coercion(start, due, error):
    with pytest.raises(error):
        prepare(snapshot(start_date=start, due_date=due))


def test_all_returned_objects_are_immutable():
    current = snapshot()
    result = prepare(current)
    for value, field, replacement in ((current, "task_status", "returned"),
                                      (result, "decision", TaskStartDecision.TASK_CHANGED),
                                      (result.plan, "row_version", 9)):
        with pytest.raises(FrozenInstanceError):
            setattr(value, field, replacement)


def test_repeated_planning_uses_only_supplied_input_without_state_changes():
    current = snapshot()
    assert prepare(current) == prepare(current)
    assert current == snapshot()


def test_cold_import_and_plan_do_not_register_orm_metadata_or_load_task_models():
    # The server package itself imports existing common dependencies. Importing
    # this pure feature must not opt task ORM mappings into shared metadata.
    source = """
import sys
from datetime import date, datetime, timezone
from remotework.models import Base
before = set(Base.metadata.tables)
from remotework.features.tasks.start_transition import (
    TaskStartAction, TaskStartDecision, TaskStartSnapshot, prepare_start_transition)
current = TaskStartSnapshot('not_started', 1, 1, date(2026,10,19), date(2026,10,22), None)
result = prepare_start_transition(current, action=TaskStartAction.ACCEPT,
    expected_assignment_revision=1, expected_row_version=1,
    now_utc=datetime(2026,10,19,tzinfo=timezone.utc))
assert result.decision is TaskStartDecision.READY
assert set(Base.metadata.tables) == before
assert 'remotework.features.tasks.models' not in sys.modules
assert 'remotework.features.tasks.creation' not in sys.modules
assert 'remotework.features.tasks.routes' not in sys.modules
"""
    completed = subprocess.run([sys.executable, "-c", source], check=False,
                               capture_output=True, text=True)
    assert completed.returncode == 0, completed.stderr
