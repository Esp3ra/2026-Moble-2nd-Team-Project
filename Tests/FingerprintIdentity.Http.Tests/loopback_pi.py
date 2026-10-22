"""TEST ONLY: loopback Pi + RAM issuer, generated synthetic keys/fake UART.

No real config, serial, sensor, database or production authentication is used.
The _test endpoints are NOT a member-1 server implementation.
"""
import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import secrets
import sys
import threading
import time
from types import SimpleNamespace
from uuid import uuid4

parser = argparse.ArgumentParser()
parser.add_argument("--repository", required=True)
args = parser.parse_args()
sys.path.insert(0, str(Path(args.repository) / "Devices/RaspberryPi"))

from flask import Flask, jsonify, request
from werkzeug.serving import make_server, WSGIRequestHandler
import fingerprint_identity_wire as wire
from fingerprint_identity import IdentityProfile
from identity_runtime import IdentityRuntime
from pi_api import DisplayCoordinator, FingerprintService


class Sensor:
    def __init__(self):
        self.mode, self.phase, self.searches, self.images = "matched", 0, 0, 0
        self.finger_id = 17

    def get_image(self):
        self.images += 1
        if self.mode == "wait": return 2
        self.phase += 1
        return 2 if self.phase % 2 else 0

    def image_2_tz(self, buffer): return 0
    def finger_fast_search(self):
        self.searches += 1
        self.finger_id = 18 if self.mode == "no_match" else 17
        return 0


sensor = Sensor()
display = DisplayCoordinator(lambda _: None, time.monotonic)
service = FingerprintService(sensor, SimpleNamespace(OK=0, NOFINGER=2, NOMATCH=8, NOTFOUND=9),
                             display, clock=time.monotonic, sleep=time.sleep)
keys = {k: secrets.token_bytes(32) for k in wire.KINDS}
profile = IdentityProfile("10000000-0000-4000-8000-000000000001", "loopback-generated-only", "1", True,
                          1, 299, 300, keys, (secrets.token_bytes(32),))
app = Flask(__name__)
# Test double only: no production startup reconciliation is claimed by this host.
runtime = IdentityRuntime(service, profile, ready=lambda _: True, clock=time.monotonic)
runtime.mount(app)
lock = threading.RLock()
tickets, approvals, receipts, result_hashes = {}, {}, {}, {}
counters = dict(approval_posts=0, complete_posts=0, completions=0)


def utc(): return datetime.now(timezone.utc).replace(microsecond=0)
def stamp(value): return value.strftime("%Y-%m-%dT%H:%M:%SZ")
def sign(payload, kind):
    return wire.sign(json.dumps(payload, separators=(",", ":")).encode(), kind=kind,
                     device_id=profile.device_id, key_id=profile.key_id, key=keys[kind])
def verify(raw, kind):
    return wire.verify(raw, kind=kind, device_id=profile.device_id, key_id=profile.key_id, key=keys[kind])


@app.post("/_test/new")
def new():
    mode = request.json["mode"]
    if mode not in ("matched", "no_match", "wait", "bad_approval", "slow_approval") or service.lock.locked():
        return jsonify(error="fixture not idle"), 409
    with lock:
        sensor.mode, sensor.phase = mode, 0
        now, challenge = utc(), str(uuid4())
        ticket = dict(challenge_id=challenge, nonce=wire.b64encode(secrets.token_bytes(32)),
                      mapping_revision="7", key_epoch="1", expected_slot=17, purpose="clock_in",
                      session_ref=wire.b64encode(secrets.token_bytes(32)), work_session_ref=None,
                      work_date=now.date().isoformat(), issued_at=stamp(now), expires_at=stamp(now + timedelta(seconds=60)))
        raw = sign(ticket, "id_request")
        tickets[challenge] = (ticket, raw, mode)
        return jsonify(challenge_id=challenge, device_id=profile.device_id, purpose="clock_in", request_b64=wire.b64encode(raw))


@app.post("/_test/<challenge>/approval")
def approve(challenge):
    probe_raw = wire.b64decode(request.json["probe_b64"], 16384)
    p = verify(probe_raw, "id_probe")
    with lock:
        ticket, raw, mode = tickets[challenge]
        assert all(p[k] == ticket[k] for k in wire.COMMON)
        assert p["ticket_digest"] == wire.message_digest(raw)
        counters["approval_posts"] += 1
        digest = hashlib.sha256(probe_raw).hexdigest()
        if digest in approvals: return jsonify(approval_b64=wire.b64encode(approvals[digest]))
        now = utc()
        assert now < wire._time(ticket["expires_at"])
        payload = dict(p, decision="allow", server_time=stamp(now),
                       valid_until=stamp(min(now + timedelta(seconds=20), wire._time(ticket["expires_at"]))))
        answer = sign(payload, "id_approval")
        approvals[digest] = answer
    if mode == "slow_approval": time.sleep(2.2)
    if mode == "bad_approval":
        e = json.loads(answer); e["mac_b64"] = wire.b64encode(bytes(32)); answer = json.dumps(e).encode()
    return jsonify(approval_b64=wire.b64encode(answer))


@app.post("/_test/<challenge>/complete")
def complete(challenge):
    raw = wire.b64decode(request.json["result_b64"], 16384)
    result = verify(raw, "id_result")
    with lock:
        ticket, request_raw, _ = tickets[challenge]
        assert result["challenge_id"] == challenge
        approved = next((a for a in approvals.values() if wire.message_digest(a) == result["approval_digest"]), None)
        wire.bind_result(result, ticket, ticket_bytes=request_raw, result_approval_bytes=approved)
        counters["complete_posts"] += 1
        digest = hashlib.sha256(raw).hexdigest()
        if challenge in result_hashes:
            if result_hashes[challenge] != digest: return jsonify(error="synthetic conflict"), 409
            return jsonify(receipts[challenge])
        matched = result["outcome"] == "matched"
        receipt = dict(challenge_id=challenge, state="verified" if matched else "rejected",
                       workflow_state="pending" if matched else "not_started",
                       identity_receipt=dict(receipt_id=str(uuid4()), purpose="clock_in", verified_at=stamp(utc())) if matched else None)
        result_hashes[challenge], receipts[challenge] = digest, receipt
        counters["completions"] += 1
        return jsonify(receipt)


@app.get("/_test/<challenge>/status")
def status(challenge):
    with lock:
        return jsonify(receipts.get(challenge, dict(challenge_id=challenge, state="issued", workflow_state="not_started", identity_receipt=None)))


@app.get("/_test/stats")
def stats():
    with lock:
        return jsonify(dict(counters, searches=sensor.searches, images=sensor.images, sensor_locked=service.lock.locked(),
                            shared_stopping=service.stopping.is_set(), jobs=len(runtime._jobs._jobs)))


class Quiet(WSGIRequestHandler):
    def log_request(self, *args, **kwargs): pass


server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=Quiet)
thread = threading.Thread(target=server.serve_forever, daemon=True)
thread.start()
print(json.dumps(dict(port=server.server_port, loopback_only=True, synthetic=True)), flush=True)
try:
    sys.stdin.readline()
finally:
    stopped = runtime.close()
    server.shutdown(); server.server_close(); thread.join(3)
    print(json.dumps(dict(stopped=stopped, shared_stopping=service.stopping.is_set())), flush=True)
    if not stopped: raise SystemExit(2)
