"""Owned MySQL/server/Pi HTTP and actual C# noon form; synthetic UART/monitor only."""
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from uuid import uuid4

import pytest
from flask import request
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


@pytest.mark.parametrize("mode", ["normal", "loss"])
def test_noon_transition_and_retained_result(working, monkeypatch, mode):
    assert os.environ.get("MEMBER5_OWNED_IDENTITY_CHECK") == "1"
    dll = Path(os.environ["MEMBER5_WORKDAY_ACTIVITY_CLIENT"]).resolve(strict=True)
    assert dll.name == "WorkdayActivity.Tests.dll"
    app = working[0]
    clock = [datetime.now(timezone.utc).replace(hour=1, minute=59, second=50, microsecond=0, tzinfo=None)]
    monkeypatch.setattr(routes, "now", lambda: clock[0])
    day = (clock[0] + timedelta(hours=9)).date().isoformat()
    work_id = str(uuid4())
    with app.app_context():
        db = get_db()
        user = db.scalar(select(User).where(User.login_id == "staff1"))
        # The fixture represents an already-open shift, not a production clock-in.
        db.add(WorkSession(id=work_id, user_id=user.id, work_date=day, started_at=clock[0] - timedelta(minutes=1)))
        db.commit()

    def advance_after_first_read(response):
        if request.path == "/api/v1/device-identity/workday":
            clock[0] = clock[0].replace(hour=2, minute=0, second=0)
        return response

    # Fixture-only clock progression after the first response was constructed.
    # No product endpoint, operator config or production time source is changed.
    hooks = app.after_request_funcs.setdefault(None, [])
    hooks.append(advance_after_first_read)
    try:
        with pair(working) as p:
            env = dict(os.environ)
            env.pop("DATABASE_URL", None); env.pop("MYSQL_TEST_URL", None)
            env.update(MEMBER5_TEST_LOGIN_ID="staff1", MEMBER5_TEST_PASSWORD=PASSWORD)
            result = owned.run_owned_client(["dotnet", str(dll), "--loopback-workday-activity",
                p.server_url, p.pi_url, DID, work_id, mode], cwd=dll.parent, env=env,
                stdin=subprocess.DEVNULL, capture_output=True, text=True, encoding="utf-8",
                creationflags=subprocess.CREATE_NO_WINDOW, timeout=45)
            assert result.returncode == 0, (result.stdout or "") + (result.stderr or "")
            summary = json.loads(result.stdout.strip().splitlines()[-1])
            assert summary["passed"] and summary["shown"] == (2 if mode == "loss" else 1)
            assert summary["monitor_starts"] == summary["monitor_stops"] == 1
            assert p.sensor.searches == 1
            with app.app_context():
                db = get_db(); work = db.get(WorkSession, work_id)
                assert work.noon_at is not None and work.ended_at is None
                assert db.scalar(select(func.count()).select_from(WorkflowReceipt)) == 1
                assert db.scalar(select(func.count()).select_from(Challenge)) == 1
                assert db.scalar(select(func.count()).select_from(Approval)) == 2
    finally:
        hooks.remove(advance_after_first_read)
