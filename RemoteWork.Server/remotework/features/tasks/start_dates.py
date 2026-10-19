"""Pure member 2 acceptance/rework date rule; no route, clock or DB access.

The user confirmed the inclusive Korean start/due calendar dates. This rule
does not decide submission, extension, authorization or task-state eligibility.
Korea Standard Time is explicitly UTC+09:00; no platform tzdata is required.
"""
from dataclasses import dataclass
from datetime import date, datetime, timedelta, timezone
from enum import Enum


KOREA_STANDARD_TIME = timezone(timedelta(hours=9), name="KST")


class TaskStartPeriod(Enum):
    BEFORE_START = "before_start"
    WITHIN_PERIOD = "within_period"
    AFTER_DUE = "after_due"


@dataclass(frozen=True)
class TaskStartDateResult:
    period: TaskStartPeriod
    korean_date: date


def evaluate_start_period(start_date: date, due_date: date, *,
                          now_utc: datetime) -> TaskStartDateResult:
    """Classify an explicitly supplied UTC instant using Korean calendar dates.

    The interval is start-date 00:00 inclusive through the midnight after the
    due date exclusive. Compare local dates instead of constructing that next
    midnight so that the MySQL DATE upper bound remains supported.
    Invalid inputs raise TypeError/ValueError, never an HTTP/business error.
    """
    for name, value in (("start_date", start_date), ("due_date", due_date)):
        if type(value) is not date:
            raise TypeError(f"{name} must be a date, not a datetime or string")
        if value.year < 1000:
            raise ValueError(f"{name} must be within MySQL DATE years 1000..9999")
    if due_date < start_date:
        raise ValueError("due_date must not precede start_date")
    if not isinstance(now_utc, datetime):
        raise TypeError("now_utc must be a UTC-aware datetime")
    if now_utc.tzinfo is None or now_utc.utcoffset() != timedelta(0):
        raise ValueError("now_utc must be timezone-aware with UTC offset 00:00")
    try:
        korean_date = now_utc.astimezone(KOREA_STANDARD_TIME).date()
    except (OverflowError, ValueError) as exc:
        raise ValueError("now_utc is outside the representable Korean calendar") from exc

    if korean_date < start_date:
        period = TaskStartPeriod.BEFORE_START
    elif korean_date > due_date:
        period = TaskStartPeriod.AFTER_DUE
    else:
        period = TaskStartPeriod.WITHIN_PERIOD
    return TaskStartDateResult(period=period, korean_date=korean_date)
