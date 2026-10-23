"""Actual Designer forms + PR79 adapter/API, synthetic rows in an owned MySQL."""
import json
import os
from pathlib import Path
import subprocess
import sys
import threading

import pytest
from flask import jsonify, request
from sqlalchemy import select, func
from werkzeug.serving import make_server
from remotework import leave_service as service
from remotework.db import transaction
from test_leave_service import leave_app, env as leave_env

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "TaskQueries.Http.Tests"))
import run_mysql_http_check as owned


@pytest.mark.parametrize("mode,decision", [
    ("normal", "approved"), ("loss", "approved"), ("loss", "rejected"), ("policy-off", "approved")])
def test_actual_leave_forms_and_mainform_owner(leave_env, mode, decision):
    assert os.environ.get("MEMBER5_OWNED_LEAVE_CHECK") == "1"
    dll = Path(os.environ["MEMBER5_LEAVE_UI_CLIENT"]).resolve(strict=True)
    assert dll.name == "Leave.Ui.Tests.dll"
    app = leave_env["app"]
    if mode == "policy-off":
        app.config["LEAVE_WRITE_POLICY"] = ""
    counts = dict(submits=0, decisions=0, lookups=0)

    def after(response):
        kind = None
        if request.path == "/api/v1/leave/requests" and request.method == "POST":
            kind = "submits"
        if request.path.startswith("/api/v1/leave/requests/") and request.path.endswith("/decision") and request.method == "POST":
            kind = "decisions"
        if request.path.startswith("/api/v1/leave/operations/"):
            counts["lookups"] += 1
        if kind:
            counts[kind] += 1
            if mode == "loss" and counts[kind] == 1 and response.status_code in (200, 201):
                # Owned fixture only: discard a response AFTER the MySQL commit.
                return app.make_response((jsonify(ok=False, error=dict(code="TEST_REPLY_LOST", message="synthetic loss")), 503))
        return response

    hooks = app.after_request_funcs.setdefault(None, [])
    hooks.append(after)
    server = make_server("127.0.0.1", 0, app, threaded=True)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        child_env = dict(os.environ)
        child_env.pop("DATABASE_URL", None); child_env.pop("MYSQL_TEST_URL", None)
        child_env["MEMBER5_TEST_PASSWORD"] = "Synthetic-pass-2026!"
        result = owned.run_owned_client(["dotnet", str(dll), "--loopback-leave",
            "http://127.0.0.1:" + str(server.server_port), mode, decision], cwd=dll.parent, env=child_env,
            stdin=subprocess.DEVNULL, capture_output=True, text=True, encoding="utf-8",
            creationflags=subprocess.CREATE_NO_WINDOW, timeout=60)
        assert result.returncode == 0, (result.stdout or "") + (result.stderr or "")
        summary = json.loads(result.stdout.strip().splitlines()[-1])
        assert summary["passed"] and summary["created"] == 1
        expected = 0 if mode == "policy-off" else 1
        assert counts["submits"] == counts["decisions"] == expected
        assert counts["lookups"] == (2 if mode == "loss" else 0)
        with app.app_context(), transaction() as db:
            assert db.scalar(select(func.count()).select_from(service.requests)) == expected
            if expected:
                row = db.execute(select(service.requests)).mappings().one()
                assert row["status"] == decision and row["revision"] == 2
                balance = service.totals(db, leave_env["ids"]["worker"], 2026)
                assert balance["reserved_units"] == 0
                assert balance["used_units"] == (1 if decision == "approved" else 0)
    finally:
        server.shutdown(); thread.join(timeout=5); server.server_close()
        hooks.remove(after)
        assert not thread.is_alive()
