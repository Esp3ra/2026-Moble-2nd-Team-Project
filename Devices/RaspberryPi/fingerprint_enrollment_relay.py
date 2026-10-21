"""Opt-in asynchronous PC/Pi transport; signed approval still authorizes writes.

Relay capabilities are per-attempt RAM-only transport secrets, not device MAC
keys or account bearers. This module never opens UART or creates key material.
"""

from dataclasses import dataclass, field
import hashlib
import hmac
import math
import threading
import time

from fingerprint_enrollment import EnrollmentError
from fingerprint_enrollment_wire import canonical_uuid, decode64, encode64, parse_json, require


@dataclass
class _Job:
    relay_id: str
    enrollment_id: str
    mode: str
    capability: str = field(repr=False)
    request_hash: str
    cancellation: object = field(default_factory=threading.Event, repr=False)
    condition: object = field(default_factory=threading.Condition, repr=False)
    state: str = "running"
    challenge: object = None
    answer: object = field(default=None, repr=False)
    last_answer: object = field(default=None, repr=False)
    result: object = field(default=None, repr=False)
    delivery_deadline: object = None
    delivery_started: object = None
    error: object = None
    thread: object = field(default=None, repr=False)


class EnrollmentRelayJobs:
    def __init__(self, adapter, *, approval_clock, max_jobs=32):
        self.adapter, self.approval_clock, self.max_jobs = adapter, approval_clock, max_jobs
        self._lock, self._jobs, self._closed = threading.Lock(), {}, False

    def _capability(self, capability):
        require(len(decode64(capability, 32)) == 32)

    def start(self, body, capability):
        require(type(body) is dict and set(body) == {"relay_id", "enrollment_id", "mode", "request_envelope"})
        self._capability(capability)
        canonical_uuid(body["relay_id"])
        canonical_uuid(body["enrollment_id"])
        require(body["mode"] in ("enroll", "read_result"))
        raw = decode64(body["request_envelope"], 16384) if body["mode"] == "enroll" else None
        if raw is None:
            require(body["request_envelope"] is None)
        else:
            ticket = self.adapter._ticket(raw)
            require(ticket.operation.enrollment_id == body["enrollment_id"])
        digest = hashlib.sha256(raw or b"").hexdigest()
        with self._lock:
            require(not self._closed)
            old = self._jobs.get(body["relay_id"])
            if old is not None:
                require(hmac.compare_digest(old.capability, capability))
                require((old.enrollment_id, old.mode, old.request_hash) ==
                        (body["enrollment_id"], body["mode"], digest))
                return self.snapshot(old.relay_id, capability)
            if len(self._jobs) >= self.max_jobs:
                completed = next((key for key, job in self._jobs.items()
                                  if job.state in ("succeeded", "delivered", "failed")), None)
                if completed is None:
                    raise EnrollmentError("relay_busy")
                del self._jobs[completed]
            job = _Job(body["relay_id"], body["enrollment_id"], body["mode"], capability, digest)
            self._jobs[job.relay_id] = job
            job.thread = threading.Thread(target=self._run, args=(job, raw), daemon=True)
            job.thread.start()
        return self.snapshot(job.relay_id, capability)

    def _get(self, relay_id, capability):
        self._capability(capability)
        canonical_uuid(relay_id)
        # Caller either holds _lock or accepts a local reference to a finished job.
        job = self._jobs.get(relay_id)
        if job is None or not hmac.compare_digest(job.capability, capability):
            raise EnrollmentError("relay_not_found")
        return job

    def snapshot(self, relay_id, capability, deliver=True):
        job = self._get(relay_id, capability)
        with job.condition:
            state, result = job.state, None
            if job.result is not None and deliver:
                now = self.approval_clock()
                valid = (type(now) in (int, float) and math.isfinite(now)
                         and job.delivery_started is not None and job.delivery_deadline is not None
                         and job.delivery_started <= now < job.delivery_deadline)
                if valid:
                    result = encode64(job.result)
                else:
                    state, job.error = "delivered", "result_delivery_expired"
                # Original delivery is one-shot and bounded by the signed permit.
                # Future queries must start a NEW result_read approval job.
                job.result, job.state = None, "delivered"
            return dict(relay_id=relay_id, state=state,
                        challenge=dict(job.challenge) if job.challenge is not None else None,
                        result_envelope=result, error_code=job.error)

    def answer(self, relay_id, capability, body):
        require(type(body) is dict and set(body) == {"probe_nonce", "approval_envelope"})
        # MySQL receipt RPCs can contain a base64url signed result. These bytes
        # are transport data; only the adapter's permit verifier grants writes.
        raw = decode64(body["approval_envelope"], 32768)
        digest = hashlib.sha256(raw).hexdigest()
        job = self._get(relay_id, capability)
        with job.condition:
            if job.last_answer == (body["probe_nonce"], digest):
                return self.snapshot(relay_id, capability)
            require(not job.cancellation.is_set() and job.challenge is not None)
            require(job.challenge["probe_nonce"] == body["probe_nonce"])
            require(job.answer is None)
            job.answer = raw
            job.last_answer = (body["probe_nonce"], digest)
            job.challenge, job.state = None, "running"
            job.condition.notify_all()
        return self.snapshot(relay_id, capability)

    def cancel(self, relay_id, capability):
        job = self._get(relay_id, capability)
        with job.condition:
            job.cancellation.set()
            if job.state == "succeeded":
                job.result, job.state, job.error = None, "delivered", "result_delivery_cancelled"
            job.condition.notify_all()
        return self.snapshot(relay_id, capability, deliver=False)

    def _exchange(self, job, enrollment_id, body, cancellation):
        require(enrollment_id == job.enrollment_id)
        deadline = time.monotonic() + 2
        with job.condition:
            job.challenge, job.answer, job.state = dict(body), None, "awaiting_approval"
            job.condition.notify_all()
            while job.answer is None:
                remaining = deadline - time.monotonic()
                if cancellation.is_set() or remaining <= 0:
                    job.challenge = None
                    raise EnrollmentError("not_authorized")
                job.condition.wait(remaining)
            answer = job.answer
            job.challenge, job.answer, job.state = None, None, "running"
            return answer

    def _run(self, job, raw):
        exchange = lambda enrollment_id, body, cancellation: self._exchange(job, enrollment_id, body, cancellation)
        def delivery(seconds):
            job.delivery_started = self.approval_clock()
            job.delivery_deadline = job.delivery_started + seconds
        try:
            if job.mode == "enroll":
                result = self.adapter.enroll_online(raw, job.cancellation, exchange=exchange,
                                                    approval_clock=self.approval_clock, on_delivery=delivery)
            else:
                result = self.adapter.read_result_online(job.enrollment_id, job.cancellation,
                                                         exchange=exchange, approval_clock=self.approval_clock, on_delivery=delivery)
            with job.condition:
                if job.cancellation.is_set():
                    raise EnrollmentError("cancelled")
                job.result, job.state = result, "succeeded"
        except Exception as error:
            with job.condition:
                job.error = error.code if isinstance(error, EnrollmentError) else "relay_error"
                job.state = "failed"
        finally:
            with job.condition:
                job.challenge = job.answer = None
                job.condition.notify_all()

    def close(self):
        with self._lock:
            self._closed = True
            jobs = list(self._jobs.values())
        for job in jobs:
            self.cancel(job.relay_id, job.capability)
        for job in jobs:
            job.thread.join(timeout=2)


def register_enrollment_relay(app, jobs):
    """Explicit installation only. Existing pi_api main does not call this.

    Deployment must supply a trusted TLS/private transport. Message MACs do not
    encrypt the connection. Never log request bodies or X-Enrollment-Relay.
    """
    from flask import Blueprint, jsonify, request
    from fingerprint_enrollment_wire import WireError

    bp = Blueprint("enrollment_relay", __name__, url_prefix="/enrollments/jobs")

    def respond(action):
        try:
            value = action(request.headers.get("X-Enrollment-Relay", ""))
            response = jsonify(success=True, data=value)
            response.headers["Cache-Control"] = "no-store"
            return response
        except (EnrollmentError, WireError):
            return jsonify(success=False, message="등록 통신 상태를 확인해 주세요"), 409

    def body():
        require(request.is_json and request.content_length is not None and request.content_length <= 65536)
        return parse_json(request.get_data(), 65536)

    @bp.post("")
    def start():
        return respond(lambda capability: jobs.start(body(), capability))

    @bp.get("/<relay_id>")
    def poll(relay_id):
        return respond(lambda capability: jobs.snapshot(relay_id, capability))

    @bp.post("/<relay_id>/approval")
    def answer(relay_id):
        return respond(lambda capability: jobs.answer(relay_id, capability, body()))

    @bp.post("/<relay_id>/cancel")
    def cancel(relay_id):
        return respond(lambda capability: jobs.cancel(relay_id, capability))

    app.register_blueprint(bp)
