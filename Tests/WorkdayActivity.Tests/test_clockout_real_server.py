"""Explicit reviewed PR73 combined C# source; owned MySQL and Pi HTTP, fake UART."""
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import timedelta
from uuid import uuid4

import pytest
from flask import request, jsonify
from sqlalchemy import select, func
from remotework.db import get_db
from remotework.models import User, AuthSession
from remotework.features.identity.models import Challenge, Approval
from remotework.features.identity.attendance import WorkSession, WorkflowReceipt
from remotework.features.identity import routes
from test_device_enrollment import DID
from test_identity_service import PASSWORD
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "FingerprintIdentity.Server.Tests"))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "TaskQueries.Http.Tests"))
from test_server_pi import pair, owned_only
import run_mysql_http_check as owned


@pytest.mark.parametrize("mode", ["normal", "loss"])
def test_feature_clockout_common_owner_and_real_pi(working, mode):
    assert os.environ.get("MEMBER5_OWNED_IDENTITY_CHECK") == "1"
    dll = Path(os.environ["MEMBER5_WORKDAY_CLOCKOUT_CLIENT"]).resolve(strict=True)
    assert dll.name == "WorkdayActivity.Tests.dll"
    app = working[0]; work_id = str(uuid4())
    now = routes.now(); day = (now + timedelta(hours=9)).date().isoformat()
    with app.app_context():
        db = get_db(); user = db.scalar(select(User).where(User.login_id == "staff1")); uid = user.id
        db.add(WorkSession(id=work_id, user_id=uid, work_date=day, started_at=now - timedelta(minutes=1)))
        db.commit()
    counts = {"finalizes": 0, "logouts": 0}

    def inspect_response(response):
        if request.path == "/api/v1/auth/logout": counts["logouts"] += 1
        if request.path.endswith("/finalize") and request.path.startswith("/api/v1/device-identity/"):
            counts["finalizes"] += 1
            if mode == "loss" and counts["finalizes"] == 1 and response.status_code == 200:
                # Only this owned fixture discards the successful committed reply.
                return jsonify(ok=False, error=dict(code="TEST_REPLY_LOST", message="synthetic response loss")), 503
        return response

    def after(response):
        changed = inspect_response(response)
        return app.make_response(changed)

    hooks = app.after_request_funcs.setdefault(None, []); hooks.append(after)
    try:
        with pair(working) as p:
            env = dict(os.environ); env.pop("DATABASE_URL", None); env.pop("MYSQL_TEST_URL", None)
            env.update(MEMBER5_TEST_LOGIN_ID="staff1", MEMBER5_TEST_PASSWORD=PASSWORD)
            result = owned.run_owned_client(["dotnet", str(dll), "--loopback-clockout-activity", p.server_url,
                p.pi_url, DID, work_id, mode], cwd=dll.parent, env=env, stdin=subprocess.DEVNULL,
                capture_output=True, text=True, encoding="utf-8", creationflags=subprocess.CREATE_NO_WINDOW, timeout=45)
            assert result.returncode == 0, (result.stdout or "") + (result.stderr or "")
            summary = json.loads(result.stdout.strip().splitlines()[-1])
            assert summary["passed"] and summary["prepares"] == 1 and summary["shown"] == (2 if mode == "loss" else 1)
            assert counts == dict(finalizes=2 if mode == "loss" else 1, logouts=0)
            assert p.sensor.searches == 1
            with app.app_context():
                db = get_db(); work = db.get(WorkSession, work_id)
                assert work.ended_at is not None
                assert db.scalar(select(func.count()).select_from(WorkflowReceipt)) == 1
                assert db.scalar(select(func.count()).select_from(Challenge)) == 1
                assert db.scalar(select(func.count()).select_from(Approval)) == 2
                assert db.scalar(select(func.count()).select_from(AuthSession).where(AuthSession.user_id == uid)) == 0
    finally:
        hooks.remove(after)
