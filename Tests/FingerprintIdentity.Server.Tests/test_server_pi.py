"""Real member-1 HTTP/MySQL + member-5 HTTP/runtime, fake sensor only.

The runner must provide a newly owned MySQL and a reviewed source export.
There is no production key/config, physical device or deployment readiness here.
"""
from contextlib import contextmanager
import json
import os
import secrets
import threading
import time
from types import SimpleNamespace
from urllib.error import HTTPError
from urllib.request import Request, ProxyHandler, HTTPRedirectHandler, build_opener
from uuid import uuid4

import pytest
from sqlalchemy import func, select
from sqlalchemy.engine import make_url
from werkzeug.serving import make_server, WSGIRequestHandler

from remotework import create_app
from remotework.db import get_db
from remotework.features.identity.models import Challenge, Approval
from remotework.features.identity.attendance import WorkSession, WorkflowReceipt
from test_identity_service import ROOT, PASSWORD
from test_device_enrollment import DID, KEYS
import fingerprint_identity_wire as wire
from fingerprint_identity import IdentityProfile
from identity_runtime import IdentityRuntime
from pi_api import FingerprintService, DisplayCoordinator, MotionFeed, create_app as pi_app

pytest_plugins = ["test_identity_service"]


class Quiet(WSGIRequestHandler):
    def log_request(self, *args, **kwargs): pass


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs): return None


@contextmanager
def serving(app):
    server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=Quiet)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield "http://127.0.0.1:" + str(server.server_port)
    finally:
        server.shutdown(); server.server_close(); thread.join(3)
        assert not thread.is_alive()


def http(base, method, path, body=None, headers=None):
    assert base.startswith("http://127.0.0.1:")
    data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
    req = Request(base + path, data=data, method=method,
                  headers={"Content-Type": "application/json", **(headers or {})})
    opener = build_opener(ProxyHandler({}), NoRedirect())
    try:
        response = opener.open(req, timeout=3)
    except HTTPError as error:
        response = error
    with response:
        raw = response.read(65537)
        assert len(raw) <= 65536
        return response.status, json.loads(raw)


class Sensor:
    def __init__(self, mode="matched"):
        self.mode, self.phase, self.images, self.searches = mode, 0, 0, 0
        self.finger_id = 7
        self.on_search = None

    def get_image(self):
        self.images += 1
        if self.mode == "wait": return 2
        self.phase += 1
        return 2 if self.phase % 2 else 0

    def image_2_tz(self, buffer): return 0
    def finger_fast_search(self):
        self.searches += 1
        self.finger_id = 8 if self.mode == "different" else 7
        if self.on_search: self.on_search()
        return 9 if self.mode == "unregistered" else 0


class Pair:
    def __init__(self, env, server_url, mode="matched"):
        self.env, self.server_url = env, server_url
        p = env[2]
        self.sensor = Sensor(mode)
        self.service = FingerprintService(self.sensor, SimpleNamespace(OK=0, NOFINGER=2, NOMATCH=8, NOTFOUND=9),
            DisplayCoordinator(lambda _: None), clock=time.monotonic, sleep=time.sleep)
        self.profile = IdentityProfile(DID, p["key_id"], p["key_epoch"], True, 0, 299, 300,
                                      dict(p["keys"]), (KEYS["request_key"], KEYS["result_key"]))
        # TEST ONLY: the fixture owns a clean runtime and a brand-new MySQL.
        self.runtime = IdentityRuntime(self.service, self.profile, ready=lambda _: True, clock=time.monotonic)
        self.app = pi_app(self.service, MotionFeed())
        self.runtime.mount(self.app)
        self.cap = {"X-Identity-Relay": wire.b64encode(secrets.token_bytes(32))}
        self.relay_id = str(uuid4())
        self.path = "/identity/v2/jobs/" + self.relay_id

    def server(self, method, path, body=None, headers=None):
        return http(self.server_url, method, ROOT + path, body, headers)

    def login(self):
        code, answer = self.server("POST", "/login", dict(login_id="staff1", password=PASSWORD, client_request_id=str(uuid4())))
        assert code == 200
        assert "access_token" not in answer["data"]
        return {"Authorization": "IdentityPending " + answer["data"]["pending_token"]}

    def issue(self, headers, purpose="clock_in", request_id=None):
        body = dict(device_id=DID, purpose=purpose, client_request_id=request_id or str(uuid4()))
        code, answer = self.server("POST", "/challenges", body, headers)
        assert code == 200
        return answer["data"], body

    def device(self, method, path=None, body=None):
        return http(self.pi_url, method, self.path if path is None else path, body, self.cap)

    def start(self, job):
        code, answer = self.device("POST", "/identity/v2/jobs",
            dict(relay_id=self.relay_id, request_b64=job["request_b64"]))
        assert code == 200
        return answer["data"]

    def until(self, predicate):
        deadline = time.monotonic() + 4
        while time.monotonic() < deadline:
            code, answer = self.device("GET")
            assert code == 200
            if predicate(answer["data"]): return answer["data"]
            time.sleep(0.005)
        raise AssertionError("Pi did not reach expected test state")

    def approve(self, headers, job, state):
        code, answer = self.server("POST", "/challenges/" + job["challenge_id"] + "/approvals",
                                  {"probe_b64": state["probe_b64"]}, headers)
        assert code == 200
        ack = dict(probe_id=state["probe_id"], approval_b64=answer["data"]["approval_b64"])
        assert self.device("POST", self.path + "/approval", ack)[0] == 200
        return ack

    def result(self, headers, job):
        while True:
            state = self.until(lambda s: s["state"] in ("awaiting_approval", "succeeded", "failed"))
            if state["state"] != "awaiting_approval": return state
            self.approve(headers, job, state)

    def complete(self, headers, job, state):
        return self.server("POST", "/challenges/" + job["challenge_id"] + "/complete",
                           {"result_b64": state["result_b64"]}, headers)


@contextmanager
def pair(env, mode="matched", server_url=None):
    if server_url is None:
        with serving(env[0]) as url:
            with pair(env, mode, url) as p: yield p
        return
    p = Pair(env, server_url, mode)
    try:
        with serving(p.app) as url:
            p.pi_url = url
            yield p
    finally:
        assert p.runtime.close()
        assert not p.service.stopping.is_set()
        assert not p.service.lock.locked()


@pytest.fixture(autouse=True)
def owned_only():
    value = make_url(os.environ["MYSQL_TEST_URL"])
    assert value.drivername == "mysql+pymysql" and value.host == "127.0.0.1"
    assert value.database.startswith("remote_work_test_m2http_")
    assert os.environ.get("MEMBER5_OWNED_IDENTITY_CHECK") == "1"


def test_matched_exact_bytes_and_discarded_complete_response_replay(identity):
    with pair(identity) as p:
        h = p.login(); job, _ = p.issue(h); p.start(job)
        state = p.result(h, job)
        assert state["state"] == "succeeded"
        assert p.sensor.searches == 1
        assert p.start(job)["result_b64"] == state["result_b64"]
        code, first = p.complete(h, job, state)
        assert code == 200 and first["data"]["state"] == "verified"
        assert first["data"]["workflow_state"] == "pending"
        assert "access_token" not in first["data"]
        # Model the consumer discarding the received body, then GET before replay.
        code, lookup = p.server("GET", "/challenges/" + job["challenge_id"], headers=h)
        assert code == 200 and lookup["data"]["identity_receipt"] == first["data"]["identity_receipt"]
        assert p.complete(h, job, state)[1]["data"] == first["data"]
        with identity[0].app_context():
            db = get_db(); saved = db.get(Challenge, job["challenge_id"])
            raw = wire.b64decode(state["result_b64"])
            assert saved.result_bytes == raw
            assert db.scalar(select(func.count()).select_from(Approval)) == 2
        assert p.sensor.searches == 1


@pytest.mark.parametrize("mode", ["different", "unregistered"])
def test_nonmatching_sensor_result_is_rejected_without_member_slot_or_token(identity, mode):
    with pair(identity, mode) as p:
        h = p.login(); job, _ = p.issue(h); p.start(job); state = p.result(h, job)
        result = wire.verify(wire.b64decode(state["result_b64"]), kind="id_result", device_id=DID,
                             key_id=p.profile.key_id, key=p.profile.keys["id_result"])
        assert result["outcome"] == "no_match" and result["slot"] is None
        code, answer = p.complete(h, job, state)
        assert code == 200 and answer["data"]["state"] == "rejected"
        assert answer["data"]["identity_receipt"] is None


def test_issue_response_recovery_keeps_same_request_and_single_capture(identity):
    with pair(identity) as p:
        h = p.login(); rid = str(uuid4()); job, body = p.issue(h, request_id=rid)
        code, lookup = p.server("GET", "/requests/" + rid, headers=h)
        assert code == 200 and lookup["data"]["request_b64"] == job["request_b64"]
        code, duplicate = p.server("POST", "/challenges", body, h)
        assert code == 200 and duplicate["data"] == job
        p.start(job); assert p.result(h, job)["state"] == "succeeded"
        assert p.sensor.searches == 1


@pytest.mark.parametrize("action", ["cancel", "revoke"])
def test_current_context_cancel_or_revoke_prevents_late_result_approval(identity, action):
    with pair(identity) as p:
        h = p.login(); job, _ = p.issue(h)
        def invalidate():
            path = "/context/revoke" if action == "revoke" else "/challenges/" + job["challenge_id"] + "/cancel"
            assert p.server("POST", path, {"client_request_id": str(uuid4())}, h)[0] == 200
        p.sensor.on_search = invalidate
        p.start(job)
        first = p.until(lambda s: s["state"] == "awaiting_approval")
        p.approve(h, job, first)
        late = p.until(lambda s: s["state"] == "awaiting_approval" and s["probe_id"] != first["probe_id"])
        code, _ = p.server("POST", "/challenges/" + job["challenge_id"] + "/approvals", {"probe_b64": late["probe_b64"]}, h)
        assert code in (401, 409)
        assert p.device("POST", p.path + "/cancel", {})[0] == 200
        terminal = p.until(lambda s: s["state"] == "failed")
        assert terminal["result_b64"] is None
        with identity[0].app_context():
            assert get_db().get(Challenge, job["challenge_id"]).result_bytes is None


def test_new_pi_and_new_server_app_cannot_capture_old_request_again(identity):
    with pair(identity, "wait") as p:
        h = p.login(); job, _ = p.issue(h); p.start(job)
        probe = p.until(lambda s: s["state"] == "awaiting_approval")
        p.approve(h, job, probe)
        deadline = time.monotonic() + 2
        while p.sensor.images == 0 and time.monotonic() < deadline: time.sleep(0.005)
        assert p.sensor.images > 0
    # New real app/engine, same newly owned MySQL and current auth context.
    recreated = create_app(dict(identity[0].config))
    try:
        with serving(recreated) as url, pair(identity, server_url=url) as fresh:
            fresh.start(job)
            second = fresh.until(lambda s: s["state"] == "awaiting_approval")
            assert second["probe_b64"] != probe["probe_b64"]
            code, answer = fresh.server("POST", "/challenges/" + job["challenge_id"] + "/approvals", {"probe_b64": second["probe_b64"]}, h)
            assert code == 409 and answer["error"]["code"] == "IDENTITY_REQUEST_CONFLICT"
            assert fresh.sensor.images == fresh.sensor.searches == 0
    finally:
        recreated.extensions["db_engine"].dispose()


def test_bad_request_mac_does_not_touch_sensor(identity):
    with pair(identity) as p:
        h = p.login(); job, _ = p.issue(h)
        raw = json.loads(wire.b64decode(job["request_b64"]))
        raw["mac_b64"] = wire.b64encode(bytes(32))
        bad = wire.b64encode(json.dumps(raw).encode())
        code, _ = p.device("POST", "/identity/v2/jobs", {"relay_id": p.relay_id, "request_b64": bad})
        assert code == 403 and p.sensor.images == 0


def test_real_workflow_clock_in_and_out_with_pi_results_and_encrypted_replay(working):
    with pair(working) as p:
        h = p.login(); job, _ = p.issue(h); p.start(job); state = p.result(h, job)
        assert p.complete(h, job, state)[0] == 200
        key = {"client_request_id": str(uuid4())}
        path = "/challenges/" + job["challenge_id"] + "/finalize"
        code, done = p.server("POST", path, key, h)
        assert code == 200 and done["data"]["state"] == "completed"
        normal = {"Authorization": "Bearer " + done["data"]["access_token"]}
        assert p.server("POST", path, key, h)[1]["data"] == done["data"]
        assert http(p.server_url, "GET", "/api/v1/auth/me", headers=normal)[0] == 200
        with working[0].app_context():
            db = get_db()
            assert db.scalar(select(func.count()).select_from(WorkSession)) == 1
            receipt = db.get(WorkflowReceipt, job["challenge_id"])
            assert done["data"]["access_token"].encode() not in receipt.ciphertext
    with pair(working) as p:
        job, _ = p.issue(normal, purpose="clock_out"); p.start(job); state = p.result(normal, job)
        assert p.complete(normal, job, state)[0] == 200
        key = {"client_request_id": str(uuid4())}; path = "/challenges/" + job["challenge_id"] + "/finalize"
        code, done = p.server("POST", path, key, normal)
        assert code == 200 and "access_token" not in done["data"]
        assert p.server("POST", path, key, normal)[1]["data"] == done["data"]
        assert http(p.server_url, "GET", "/api/v1/auth/me", headers=normal)[0] == 401
        with working[0].app_context():
            assert get_db().scalar(select(WorkSession)).ended_at is not None


def test_noon_uses_same_open_work_session_and_commits_only_once(working, monkeypatch):
    from datetime import timedelta
    from remotework.features.identity import routes
    # The shared owned runner defaults to a one-hour login. This scenario moves
    # the server clock one hour; use an explicitly longer synthetic session.
    monkeypatch.setitem(working[0].config, "SESSION_HOURS", 8)
    morning = routes.now().replace(hour=1, minute=0, second=0)
    monkeypatch.setattr(routes, "now", lambda: morning)
    with pair(working) as p:
        h = p.login(); job, _ = p.issue(h); p.start(job)
        assert p.complete(h, job, p.result(h, job))[0] == 200
        code, answer = p.server("POST", "/challenges/" + job["challenge_id"] + "/finalize",
                                {"client_request_id": str(uuid4())}, h)
        assert code == 200
        normal = {"Authorization": "Bearer " + answer["data"]["access_token"]}
        work_ref = answer["data"]["work_session_ref"]
        code, denied = p.server("POST", "/challenges", dict(device_id=DID, purpose="noon_check", client_request_id=str(uuid4())), normal)
        assert code == 409 and denied["error"]["code"] == "ATTENDANCE_NOON_NOT_DUE"
    # Controlled server UTC clock; do not wait one hour or use the PC calendar.
    monkeypatch.setattr(routes, "now", lambda: morning + timedelta(hours=1))
    with pair(working) as p:
        job, _ = p.issue(normal, purpose="noon_check"); p.start(job)
        assert p.complete(normal, job, p.result(normal, job))[0] == 200
        key = {"client_request_id": str(uuid4())}; path = "/challenges/" + job["challenge_id"] + "/finalize"
        code, done = p.server("POST", path, key, normal)
        assert code == 200 and done["data"]["work_session_ref"] == work_ref
        assert "access_token" not in done["data"]
        assert p.server("POST", path, key, normal)[1]["data"] == done["data"]
        code, denied = p.server("POST", "/challenges", dict(device_id=DID, purpose="noon_check", client_request_id=str(uuid4())), normal)
        assert code == 409 and denied["error"]["code"] == "ATTENDANCE_NOON_ALREADY_RECORDED"
        with working[0].app_context():
            db = get_db()
            assert db.scalar(select(func.count()).select_from(WorkSession)) == 1
            assert db.get(WorkSession, work_ref).noon_at is not None
