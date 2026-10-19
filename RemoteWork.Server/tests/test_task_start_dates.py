"""Pure date-policy checks; no app fixture, database, HTTP or real clock."""
from dataclasses import FrozenInstanceError
from datetime import date, datetime, timedelta, timezone

import pytest

from remotework.features.tasks.start_dates import (
    TaskStartDateResult, TaskStartPeriod, evaluate_start_period,
)


@pytest.mark.parametrize(("instant", "expected_day", "expected_period"), [
    # Korean start midnight is 15:00 UTC on the previous date.
    ("2026-10-18T14:59:59.999999+00:00", date(2026, 10, 18), TaskStartPeriod.BEFORE_START),
    ("2026-10-18T15:00:00+00:00", date(2026, 10, 19), TaskStartPeriod.WITHIN_PERIOD),
    ("2026-10-18T15:00:00.000001+00:00", date(2026, 10, 19), TaskStartPeriod.WITHIN_PERIOD),
    ("2026-10-19T00:00:00+00:00", date(2026, 10, 19), TaskStartPeriod.WITHIN_PERIOD),
    # Due midnight starts an allowed day; only its following midnight closes it.
    ("2026-10-21T14:59:59.999999+00:00", date(2026, 10, 21), TaskStartPeriod.WITHIN_PERIOD),
    ("2026-10-21T15:00:00+00:00", date(2026, 10, 22), TaskStartPeriod.WITHIN_PERIOD),
    ("2026-10-22T14:59:59.999999+00:00", date(2026, 10, 22), TaskStartPeriod.WITHIN_PERIOD),
    ("2026-10-22T15:00:00+00:00", date(2026, 10, 23), TaskStartPeriod.AFTER_DUE),
    ("2026-10-22T15:00:00.000001+00:00", date(2026, 10, 23), TaskStartPeriod.AFTER_DUE),
])
def test_korean_start_and_due_boundaries(instant, expected_day, expected_period):
    result = evaluate_start_period(date(2026, 10, 19), date(2026, 10, 22),
                                   now_utc=datetime.fromisoformat(instant))
    assert result == TaskStartDateResult(expected_period, expected_day)


@pytest.mark.parametrize(("day", "before", "start", "last", "after"), [
    (date(2026, 10, 19), "2026-10-18T14:59:59.999999+00:00", "2026-10-18T15:00:00+00:00",
     "2026-10-19T14:59:59.999999+00:00", "2026-10-19T15:00:00+00:00"),
    (date(2028, 2, 29), "2028-02-28T14:59:59.999999+00:00", "2028-02-28T15:00:00+00:00",
     "2028-02-29T14:59:59.999999+00:00", "2028-02-29T15:00:00+00:00"),
    (date(2026, 12, 31), "2026-12-30T14:59:59.999999+00:00", "2026-12-30T15:00:00+00:00",
     "2026-12-31T14:59:59.999999+00:00", "2026-12-31T15:00:00+00:00"),
    (date(2027, 1, 1), "2026-12-31T14:59:59.999999+00:00", "2026-12-31T15:00:00+00:00",
     "2027-01-01T14:59:59.999999+00:00", "2027-01-01T15:00:00+00:00"),
])
def test_single_day_leap_day_and_year_turn(day, before, start, last, after):
    results = [evaluate_start_period(day, day, now_utc=datetime.fromisoformat(instant)).period
               for instant in (before, start, last, after)]
    assert results == [TaskStartPeriod.BEFORE_START, TaskStartPeriod.WITHIN_PERIOD,
                       TaskStartPeriod.WITHIN_PERIOD, TaskStartPeriod.AFTER_DUE]


@pytest.mark.parametrize(("day", "instant"), [
    (date(1000, 1, 1), datetime(999, 12, 31, 15, tzinfo=timezone.utc)),
    (date(9999, 12, 31), datetime(9999, 12, 31, 14, 59, 59, 999999, tzinfo=timezone.utc)),
])
def test_mysql_date_limits_do_not_require_next_day_arithmetic(day, instant):
    assert evaluate_start_period(day, day, now_utc=instant) == TaskStartDateResult(
        TaskStartPeriod.WITHIN_PERIOD, day)


@pytest.mark.parametrize("field", ["start_date", "due_date"])
@pytest.mark.parametrize("invalid", [None, "2026-10-19", 20261019, True,
                                    datetime(2026, 10, 19),
                                    datetime(2026, 10, 19, tzinfo=timezone.utc)])
def test_requires_calendar_dates_without_coercion(field, invalid):
    values = {"start_date": date(2026, 10, 19), "due_date": date(2026, 10, 22)}
    values[field] = invalid
    with pytest.raises(TypeError):
        evaluate_start_period(**values, now_utc=datetime(2026, 10, 19, tzinfo=timezone.utc))


@pytest.mark.parametrize(("start", "due"), [
    (date(999, 12, 31), date(2026, 10, 22)),
    (date(1, 1, 1), date(999, 12, 31)),
    (date(2026, 10, 22), date(2026, 10, 19)),
])
def test_invalid_date_range_is_not_reordered_or_clamped(start, due):
    with pytest.raises(ValueError):
        evaluate_start_period(start, due, now_utc=datetime(2026, 10, 19, tzinfo=timezone.utc))


@pytest.mark.parametrize("invalid", [None, "2026-10-19T00:00:00Z", date(2026, 10, 19), 0])
def test_requires_datetime_for_explicit_server_instant(invalid):
    with pytest.raises(TypeError):
        evaluate_start_period(date(2026, 10, 19), date(2026, 10, 22), now_utc=invalid)


@pytest.mark.parametrize("invalid", [
    datetime(2026, 10, 19),
    datetime(2026, 10, 19, tzinfo=timezone(timedelta(hours=9))),
    datetime(2026, 10, 19, tzinfo=timezone(timedelta(hours=-4))),
])
def test_naive_or_non_utc_instant_is_not_implicitly_normalized(invalid):
    with pytest.raises(ValueError):
        evaluate_start_period(date(2026, 10, 19), date(2026, 10, 22), now_utc=invalid)


def test_unrepresentable_korean_date_has_explicit_error():
    with pytest.raises(ValueError, match="representable Korean calendar"):
        evaluate_start_period(date(9999, 12, 31), date(9999, 12, 31),
                              now_utc=datetime.max.replace(tzinfo=timezone.utc))


def test_result_is_immutable():
    result = evaluate_start_period(date(2026, 10, 19), date(2026, 10, 22),
                                   now_utc=datetime(2026, 10, 19, tzinfo=timezone.utc))
    with pytest.raises(FrozenInstanceError):
        result.period = TaskStartPeriod.AFTER_DUE
