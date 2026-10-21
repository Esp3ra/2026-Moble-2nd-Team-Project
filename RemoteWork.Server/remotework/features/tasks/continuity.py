"""Conservative member 2 history reader for the existing collaboration bridge.

Only a complete, unambiguous chain of known member 2 task audits is proof.
Member 3 submissions, missing/retained-away audits and unknown writes leave a
gap and return None. This grants no task access and never changes a relation.
"""
from dataclasses import dataclass
from datetime import date
import json
import re

from flask import g, has_request_context
from sqlalchemy import select

from ...db import get_db
from ...models import AuditEvent
from ...write_contract import BIGINT_MAX, INTEGER_MAX
from .models import TaskAssignment, TaskRecord


MAX_HISTORY_ROWS = 2048
_STATES = frozenset({"not_started", "in_progress", "submitted", "approved", "returned"})
_LIFECYCLE = frozenset({"task.cancelled", "task.deadline_extension_requested",
    "task.deadline_extension_approved", "task.deadline_extension_rejected",
    "task.deadline_extension_withdrawn"})
_ACTIONS = _LIFECYCLE | {"task.accepted", "task.rework_started", "task.updated",
    "task.reassigned", "task.managers_changed"}


@dataclass(frozen=True)
class _Edge:
    before: int
    after: int
    before_state: str
    after_state: str
    before_revision: int
    after_revision: int
    interrupted: bool


def _integer(value, maximum):
    if type(value) is not int or not 1 <= value <= maximum:
        raise ValueError("noncanonical integer")
    return value


def _version(value):
    if (not isinstance(value, str) or len(value) > 19
            or re.fullmatch(r"[1-9][0-9]*", value) is None):
        raise ValueError("noncanonical version")
    return _integer(int(value), BIGINT_MAX)


def _state(value):
    if not isinstance(value, str) or value not in _STATES:
        raise ValueError("unknown state")
    return value


def _date(value):
    if not isinstance(value, str) or date.fromisoformat(value).isoformat() != value:
        raise ValueError("noncanonical date")


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate audit field")
        result[key] = value
    return result


def _edge(action, raw):
    if not isinstance(raw, str):
        raise ValueError("missing audit")
    body = json.loads(raw, object_pairs_hook=_unique)
    if not isinstance(body, dict):
        raise ValueError("invalid audit")
    if action == "task.created":
        if _version(body["row_version"]) != 1 or _integer(body["assignment_revision"], INTEGER_MAX) != 1:
            raise ValueError("invalid creation")
        return None  # Initial version is not an intervening mutation.
    if action not in _ACTIONS:
        raise ValueError("unknown task mutation")
    before, after = body["before"], body["after"]
    if not isinstance(before, dict) or not isinstance(after, dict):
        raise ValueError("missing before/after")
    old, new = _version(before["row_version"]), _version(after["row_version"])
    old_state, new_state = _state(before["task_status"]), _state(after["task_status"])
    if new != old + 1:
        raise ValueError("non-unit mutation")
    new_revision = _integer(after["assignment_revision"], INTEGER_MAX)
    if action in _LIFECYCLE:
        # Existing lifecycle audits omit the before revision. These five owner
        # actions preserve assignment by contract; other actions cannot infer it.
        old_revision = new_revision
        _date(before["due_date"])
        _date(after["due_date"])
        allowed = {"not_started", "in_progress", "returned"} if action == "task.cancelled" else {"in_progress", "returned"}
        if old_state not in allowed or old_state != new_state:
            raise ValueError("invalid lifecycle transition")
    else:
        old_revision = _integer(before["assignment_revision"], INTEGER_MAX)
        if action == "task.accepted":
            allowed = old_state == "not_started" and new_state == "in_progress"
        elif action == "task.rework_started":
            allowed = old_state == "returned" and new_state == "in_progress"
        elif action == "task.managers_changed":
            allowed = old_state in {"not_started", "in_progress", "returned"} and old_state == new_state
        else:
            allowed = old_state == new_state == "not_started"
        revision_step = 1 if action == "task.reassigned" else 0
        if not allowed or new_revision != old_revision + revision_step:
            raise ValueError("invalid management/start transition")
    interrupted = (old_state != "in_progress" or new_state != "in_progress"
        or old_revision != new_revision or action == "task.cancelled")
    return _Edge(old, new, old_state, new_state, old_revision, new_revision, interrupted)


def _prove(rows, since_version, current_version, current_state, current_revision):
    """Pure proof; bounds also prevent a huge requested range allocating memory."""
    try:
        _integer(since_version, BIGINT_MAX)
        _integer(current_version, BIGINT_MAX)
        _integer(current_revision, INTEGER_MAX)
        _state(current_state)
        gap = current_version - since_version
        if gap < 0 or gap > MAX_HISTORY_ROWS or len(rows) > MAX_HISTORY_ROWS:
            return None
        if gap == 0:
            return current_state == "in_progress"
        edges = {}
        for action, raw in rows:
            item = _edge(action, raw)
            if item is None or item.after <= since_version:
                continue
            # A recorded future version contradicts the locked current parent.
            if item.after > current_version or item.after in edges:
                return None
            edges[item.after] = item
        if len(edges) != gap:
            return None
        previous = None
        interrupted = False
        for version in range(since_version + 1, current_version + 1):
            item = edges.get(version)
            if item is None or item.before != version - 1:
                return None
            if previous is not None and (previous.after_state != item.before_state
                    or previous.after_revision != item.before_revision):
                return None
            interrupted = interrupted or item.interrupted
            previous = item
        if previous.after_state != current_state or previous.after_revision != current_revision:
            return None
        return not interrupted
    except (KeyError, TypeError, ValueError, OverflowError, RecursionError):
        return None


def continuous_in_progress(db, task_id, since_version, current_version):
    """Existing reader(db, task_id, since, current) -> True / False / None.

    The caller separately checks current cancellation, requester/assignment and
    permissions. Use only its current authenticated MySQL transaction. Locking
    reads avoid load_identity's older REPEATABLE READ snapshot. No own commit,
    model registration, schema changes or member 3/4 product imports are made.
    """
    if (not has_request_context() or not getattr(g, "write_transaction_active", False)
            or db is not get_db()):
        raise RuntimeError("Task continuity requires the current authenticated_transaction")
    try:
        _integer(task_id, BIGINT_MAX)
        _integer(since_version, BIGINT_MAX)
        _integer(current_version, BIGINT_MAX)
    except ValueError:
        return None
    if db.bind.dialect.name != "mysql" or not 0 <= current_version - since_version <= MAX_HISTORY_ROWS:
        return None
    task = db.scalar(select(TaskRecord).where(TaskRecord.task_id == task_id)
        .with_for_update().execution_options(populate_existing=True))
    if task is None or task.row_version != current_version:
        return None
    assignment = db.scalar(select(TaskAssignment).where(TaskAssignment.task_id == task_id,
        TaskAssignment.revision == task.current_assignment_revision)
        .with_for_update().execution_options(populate_existing=True))
    if assignment is None:
        return None
    rows = db.execute(select(AuditEvent.action, AuditEvent.details_json)
        .where(AuditEvent.resource_type == "task", AuditEvent.resource_id == str(task_id))
        .order_by(AuditEvent.id).limit(MAX_HISTORY_ROWS + 1).with_for_update()).all()
    return _prove(rows, since_version, current_version, task.task_status, assignment.revision)


def register_reader(app):
    """Provide the existing opt-in hook without replacing another owner's reader."""
    app.extensions.setdefault("collaboration_continuity_reader", continuous_in_progress)
