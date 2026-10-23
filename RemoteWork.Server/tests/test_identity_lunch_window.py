"""Server-clock policy and exact saved receipts; explicit in-memory TX doubles.

No operator DB/profile, schema install, keys or physical service is accessed.
Actual HTTP/MySQL behavior remains in test_identity_workday/test_identity_service.
"""
from datetime import datetime, timedelta
import json
from types import SimpleNamespace
from unittest.mock import Mock
from uuid import uuid4

import pytest

from remotework.common import AppError
from remotework.features.identity import routes
from remotework.features.identity.attendance import AttendanceWorkflow, afternoon_check_window


@pytest.fixture
def workflow(monkeypatch):
    provider = AttendanceWorkflow(bytes(range(32)), "synthetic-receipt")
    row = SimpleNamespace(id=str(uuid4()), work_date="2026-10-23", noon_at=None,
        started_at=datetime(2026, 10, 23, 1), ended_at=None)
    provider._work = Mock(return_value=(row.work_date, row))
    return provider, row


@pytest.mark.parametrize("stamp,due", [
    (datetime(2026, 10, 23, 1, 59, 59), False),
    (datetime(2026, 10, 23, 2, 0, 0), True),
    (datetime(2026, 10, 23, 2, 59, 59), True),
    (datetime(2026, 10, 23, 3, 0, 0), False),
])
def test_read_and_new_attendance_use_same_kst_window(workflow, monkeypatch, stamp, due):
    provider, row = workflow
    monkeypatch.setattr(routes, "now", lambda: stamp)
    assert afternoon_check_window(stamp) is due
    data = provider.read_workday(None, None)
    assert data["noon_due"] is due and data["noon_at"] is None
    if due:
        assert provider.current(None, None, "noon_check")["work_session_ref"] == row.id
    else:
        with pytest.raises(AppError) as error:
            provider.current(None, None, "noon_check")
        assert error.value.code == "ATTENDANCE_NOON_NOT_DUE"
    assert row.noon_at is None


def test_success_on_other_pc_suppresses_new_check_without_changing_work(workflow, monkeypatch):
    provider, row = workflow
    row.noon_at = datetime(2026, 10, 23, 2, 1)
    monkeypatch.setattr(routes, "now", lambda: datetime(2026, 10, 23, 2, 2))
    data = provider.read_workday(None, None)
    assert not data["noon_due"] and data["noon_at"] == "2026-10-23T02:01:00Z"
    with pytest.raises(AppError) as error:
        provider.current(None, None, "noon_check")
    assert error.value.code == "ATTENDANCE_NOON_ALREADY_RECORDED"


@pytest.mark.parametrize("stamp", [datetime(2026, 10, 23, 1, 59, 59), datetime(2026, 10, 23, 3)])
def test_verified_but_unsaved_result_cannot_create_receipt_outside_window(workflow, monkeypatch, stamp):
    provider, row = workflow
    monkeypatch.setattr(routes, "now", lambda: stamp)
    monkeypatch.setattr(routes, "locked", lambda *args, **kwargs: None)
    item = SimpleNamespace(id=str(uuid4()), state="verified", workflow_completed=False,
        purpose="noon_check", expires_at=stamp + timedelta(minutes=1))
    db = Mock()
    with pytest.raises(AppError) as error:
        provider.finalize(db, SimpleNamespace(id=str(uuid4())), None, item, str(uuid4()))
    assert error.value.code == "ATTENDANCE_NOON_NOT_DUE"
    assert row.noon_at is None and not item.workflow_completed
    db.add.assert_not_called()


def test_exact_committed_receipt_replays_outside_window_without_new_write(workflow, monkeypatch):
    provider, row = workflow
    stamp = datetime(2026, 10, 23, 3)
    monkeypatch.setattr(routes, "now", lambda: stamp)
    ctx, item = SimpleNamespace(id=str(uuid4())), SimpleNamespace(id=str(uuid4()))
    rid = str(uuid4())
    answer = dict(state="completed", work_session_ref=row.id, work_date=row.work_date, workflow_receipt_id=str(uuid4()))
    nonce = bytes(range(12))
    ciphertext = nonce + provider._cipher.encrypt(nonce, json.dumps(answer).encode(), (ctx.id + ":" + item.id).encode("ascii"))
    receipt = SimpleNamespace(client_request_id=rid, expires_at=stamp + timedelta(hours=1), key_id=provider._key_id, ciphertext=ciphertext)
    monkeypatch.setattr(routes, "locked", lambda *args, **kwargs: receipt)
    provider.current = Mock(side_effect=AssertionError("Saved receipt must precede new-write time policy"))
    db = Mock()
    assert provider.finalize(db, ctx, None, item, rid) == answer
    provider.current.assert_not_called()
    db.add.assert_not_called()


def test_afternoon_window_never_changes_clockout_or_workday_identity(workflow, monkeypatch):
    provider, row = workflow
    monkeypatch.setattr(routes, "now", lambda: datetime(2026, 10, 23, 9))
    assert provider.current(None, None, "clock_out") == dict(work_date=row.work_date, work_session_ref=row.id)
    assert provider.read_workday(None, None)["noon_due"] is False
