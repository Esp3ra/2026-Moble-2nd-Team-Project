"""Actual LoginForm -> real HTTP server/MySQL -> actual Pi HTTP, fake UART only."""
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timedelta
from uuid import uuid4

import pytest
from sqlalchemy import select, func
from remotework.db import get_db
from remotework.features.identity.models import Challenge, Approval
from remotework.features.identity.attendance import WorkSession, WorkflowReceipt
from remotework.features.identity import routes
from remotework.models import User
from test_device_enrollment import DID
from test_identity_service import PASSWORD
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "FingerprintIdentity.Server.Tests"))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "TaskQueries.Http.Tests"))
from test_server_pi import pair, owned_only
import run_mysql_http_check as owned


@pytest.mark.parametrize("mode", ["matched", "unregistered", "matched-loss"])
def test_actual_login_forms_to_server_and_pi(working, mode):
    assert os.environ.get("MEMBER5_OWNED_IDENTITY_CHECK") == "1"
    dll = Path(os.environ["MEMBER5_IDENTITY_LOGIN_CLIENT"]).resolve(strict=True)
    assert dll.name == "IdentityLogin.Ui.Tests.dll"
    with pair(working, "unregistered" if mode == "unregistered" else "matched") as p:
        env = dict(os.environ)
        env.pop("DATABASE_URL", None); env.pop("MYSQL_TEST_URL", None)
        env.update(MEMBER5_TEST_LOGIN_ID="staff1", MEMBER5_TEST_PASSWORD=PASSWORD)
        result = owned.run_owned_client(["dotnet", str(dll), "--loopback-login", p.server_url, p.pi_url, DID, mode],
            cwd=dll.parent, env=env, stdin=subprocess.DEVNULL, capture_output=True, text=True,
            encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW, timeout=45)
        assert result.returncode == 0, result.stdout + result.stderr
        summary = json.loads(result.stdout.strip().splitlines()[-1])
        match = mode != "unregistered"
        assert summary["passed"] and summary["scanner_dialogs"] == 1
        assert summary["workspace_count"] == int(match)
        assert p.sensor.searches == 1
        with working[0].app_context():
            db = get_db()
            assert db.scalar(select(func.count()).select_from(WorkSession)) == int(match)
            assert db.scalar(select(func.count()).select_from(WorkflowReceipt)) == int(match)
            assert db.scalar(select(func.count()).select_from(Challenge)) == 1
            assert db.scalar(select(func.count()).select_from(Approval)) == 2
            if match:
                assert db.scalar(select(WorkSession)).ended_at is None
                assert db.scalar(select(Challenge)).workflow_completed is True


@pytest.mark.parametrize("purpose", ["noon_check", "clock_out"])
@pytest.mark.parametrize("mode", ["normal", "loss"])
def test_actual_workday_forms_to_server_and_pi(working, monkeypatch, purpose, mode):
    assert os.environ.get("MEMBER5_OWNED_IDENTITY_CHECK") == "1"
    dll = Path(os.environ["MEMBER5_IDENTITY_LOGIN_CLIENT"]).resolve(strict=True)
    assert dll.name == "IdentityLogin.Ui.Tests.dll"
    # Explicit owned fixture represents a previously committed open shift, not
    # a proposed production reader or a replacement for first clock-in coverage.
    now = datetime.utcnow()
    if (now + timedelta(hours=9)).hour < 12:
        now = now.replace(hour=3, minute=5, second=0, microsecond=0)
    monkeypatch.setattr(routes, "now", lambda: now)
    day = (now + timedelta(hours=9)).date().isoformat()
    work_id = str(uuid4())
    with working[0].app_context():
        db = get_db()
        user = db.scalar(select(User).where(User.login_id == "staff1"))
        db.add(WorkSession(id=work_id, user_id=user.id, work_date=day, started_at=now - timedelta(minutes=1)))
        db.commit()
    with pair(working) as p:
        env = dict(os.environ)
        env.pop("DATABASE_URL", None); env.pop("MYSQL_TEST_URL", None)
        env.update(MEMBER5_TEST_LOGIN_ID="staff1", MEMBER5_TEST_PASSWORD=PASSWORD)
        result = owned.run_owned_client(["dotnet", str(dll), "--loopback-workday", p.server_url, p.pi_url,
            DID, purpose, work_id, day, mode], cwd=dll.parent, env=env, stdin=subprocess.DEVNULL,
            capture_output=True, text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW, timeout=45)
        assert result.returncode == 0, result.stdout + result.stderr
        summary = json.loads(result.stdout.strip().splitlines()[-1])
        assert summary["passed"] and summary["scanner_dialogs"] == 1
        assert p.sensor.searches == 1
        with working[0].app_context():
            db = get_db(); work = db.get(WorkSession, work_id)
            assert (work.noon_at is not None) == (purpose == "noon_check")
            assert (work.ended_at is not None) == (purpose == "clock_out")
            assert db.scalar(select(func.count()).select_from(WorkflowReceipt)) == 1
            assert db.scalar(select(func.count()).select_from(Challenge)) == 1
