"""Opt-in Pi HTTP transport; never mounted by the existing service implicitly."""
from dataclasses import dataclass, field
import hashlib
import hmac
import threading
import time
from uuid import uuid4

from flask import Blueprint, jsonify, request

import fingerprint_identity_wire as wire
from fingerprint_identity import IdentityAttemptPool, IdentityProfile, IdentityUnavailable, boottime_clock


class HttpProblem(Exception):
    def __init__(self, status, code):
        self.status, self.code = status, code


@dataclass
class _Job:
    relay_id: str
    digest: str
    capability_hash: bytes = field(repr=False)
    attempt: object = field(repr=False)
    cancelled: object = field(default_factory=threading.Event, repr=False)
    state: str = "running"
    probe_id: str | None = None
    probe: bytes | None = field(default=None, repr=False)
    answer: bytes | None = field(default=None, repr=False)
    answers: dict = field(default_factory=dict, repr=False)
    result: bytes | None = field(default=None, repr=False)
    error: str | None = None
    worker: object = field(default=None, repr=False)


class IdentityHttpJobs:
    def __init__(self, service, profile, *, clock=boottime_clock, maximum=64):
        if not isinstance(profile, IdentityProfile):
            raise ValueError("A validated private identity profile is required")
        self._pool = IdentityAttemptPool(service, profile, clock=clock, maximum=maximum)
        self._condition = threading.Condition()
        self._maximum, self._jobs, self._challenges = maximum, {}, {}
        self._closed = False

    @staticmethod
    def _capability(value):
        try:
            raw = wire.b64decode(value, 32)
            if len(raw) != 32:
                raise ValueError()
            return hashlib.sha256(raw).digest()
        except (wire.ProtocolError, ValueError):
            raise HttpProblem(404, "IDENTITY_JOB_NOT_FOUND") from None

    def _find(self, relay_id, capability):
        if self._closed:
            raise HttpProblem(503, "IDENTITY_DEVICE_NOT_READY")
        digest = self._capability(capability)
        job = self._jobs.get(relay_id)
        if job is None or not hmac.compare_digest(job.capability_hash, digest):
            raise HttpProblem(404, "IDENTITY_JOB_NOT_FOUND")
        return job

    @staticmethod
    def _snapshot(job):
        waiting = job.state == "awaiting_approval"
        return dict(relay_id=job.relay_id, request_digest=job.digest, state=job.state,
                    probe_id=job.probe_id if waiting else None,
                    probe_b64=wire.b64encode(job.probe) if waiting else None,
                    result_b64=wire.b64encode(job.result) if job.state == "succeeded" else None,
                    error_code=job.error)

    def submit(self, relay_id, raw, capability):
        wire.canonical_uuid(relay_id)
        capability_hash = self._capability(capability)
        with self._condition:
            if self._closed:
                raise HttpProblem(503, "IDENTITY_DEVICE_NOT_READY")
            if relay_id in self._jobs:
                job = self._find(relay_id, capability)
                if job.attempt._request_bytes != raw:
                    raise HttpProblem(409, "IDENTITY_JOB_CONFLICT")
                return self._snapshot(job)
            if len(self._jobs) >= self._maximum:
                raise HttpProblem(503, "IDENTITY_DEVICE_NOT_READY")
            try:
                attempt = self._pool.prepare(raw)
            except (wire.ProtocolError, ValueError):
                raise HttpProblem(403, "IDENTITY_REQUEST_REJECTED") from None
            except IdentityUnavailable:
                raise HttpProblem(409, "IDENTITY_JOB_CONFLICT") from None
            challenge = attempt._ticket["challenge_id"]
            if challenge in self._challenges:
                raise HttpProblem(409, "IDENTITY_JOB_CONFLICT")
            job = _Job(relay_id, hashlib.sha256(raw).hexdigest(), capability_hash, attempt)
            self._jobs[relay_id], self._challenges[challenge] = job, relay_id
            job.worker = threading.Thread(target=self._run, args=(job,), daemon=True, name="identity-capture")
            try:
                job.worker.start()
            except RuntimeError:
                job.state, job.error = "failed", "IDENTITY_APPROVAL_UNAVAILABLE"
            return self._snapshot(job)

    def status(self, relay_id, capability):
        with self._condition:
            return self._snapshot(self._find(relay_id, capability))

    def approve(self, relay_id, probe_id, raw, capability):
        wire.canonical_uuid(probe_id)
        with self._condition:
            job = self._find(relay_id, capability)
            previous = job.answers.get(probe_id)
            if previous is not None:
                if previous != raw:
                    raise HttpProblem(409, "IDENTITY_APPROVAL_CONFLICT")
                return self._snapshot(job)
            if job.cancelled.is_set() or job.state != "awaiting_approval" or probe_id != job.probe_id:
                raise HttpProblem(409, "IDENTITY_PROBE_NOT_CURRENT")
            job.answer = raw
            job.answers[probe_id] = raw
            job.state = "running"
            self._condition.notify_all()
            return self._snapshot(job)

    def cancel(self, relay_id, capability):
        with self._condition:
            job = self._find(relay_id, capability)
            if job.state not in ("succeeded", "failed"):
                job.cancelled.set()
                self._condition.notify_all()
            return self._snapshot(job)

    def _exchange(self, job, raw, stopping):
        with self._condition:
            if self._closed or stopping():
                raise IdentityUnavailable()
            job.probe_id, job.probe, job.answer = str(uuid4()), raw, None
            job.state = "awaiting_approval"
            deadline = time.monotonic() + wire.MAX_RTT_SECONDS
            while job.answer is None:
                if self._closed or stopping():
                    raise IdentityUnavailable()
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise IdentityUnavailable()
                self._condition.wait(min(remaining, 0.05))
            job.state = "running"
            return job.answer

    def _run(self, job):
        try:
            raw = job.attempt.run(lambda probe, stopping: self._exchange(job, probe, stopping),
                                  cancelled=job.cancelled.is_set)
            with self._condition:
                if job.cancelled.is_set() or self._closed:
                    job.state, job.error = "failed", "IDENTITY_CAPTURE_CANCELLED"
                else:
                    job.result, job.state = raw, "succeeded"
        except Exception:
            with self._condition:
                job.state = "failed"
                job.error = "IDENTITY_CAPTURE_CANCELLED" if job.cancelled.is_set() else "IDENTITY_APPROVAL_UNAVAILABLE"
        finally:
            with self._condition:
                self._condition.notify_all()

    def close(self, timeout=3):
        """Stop only our jobs. Never set the borrowed sensor service's stop event."""
        with self._condition:
            self._closed = True
            jobs = list(self._jobs.values())
            for job in jobs:
                if job.state not in ("succeeded", "failed"):
                    job.cancelled.set()
            self._condition.notify_all()
        deadline = time.monotonic() + timeout
        for job in jobs:
            if job.worker.is_alive():
                job.worker.join(max(0, deadline - time.monotonic()))
        return all(not job.worker.is_alive() for job in jobs)


def mount_identity_http(app, service, profile=None, *, enabled=False, clock=boottime_clock, maximum=64):
    """Explicit composition only. The default adds no routes and starts no worker."""
    if type(enabled) is not bool:
        raise ValueError("Identity enable flag must be explicit bool")
    if not enabled:
        return None
    jobs = IdentityHttpJobs(service, profile, clock=clock, maximum=maximum)
    routes = Blueprint("identity_v2", __name__)

    def body(fields):
        if request.mimetype != "application/json":
            raise HttpProblem(415, "IDENTITY_INVALID_INPUT")
        if request.content_length is not None and request.content_length > 32768:
            raise HttpProblem(413, "IDENTITY_INVALID_INPUT")
        raw = request.stream.read(32769)
        if len(raw) > 32768:
            raise HttpProblem(413, "IDENTITY_INVALID_INPUT")
        value = wire.parse_json(raw, 32768)
        if set(value) != set(fields):
            raise HttpProblem(400, "IDENTITY_INVALID_INPUT")
        return value

    def respond(operation):
        try:
            return jsonify(success=True, data=operation())
        except HttpProblem as error:
            return jsonify(success=False, error=dict(code=error.code)), error.status
        except (wire.ProtocolError, ValueError):
            return jsonify(success=False, error=dict(code="IDENTITY_INVALID_INPUT")), 400

    def capability():
        return request.headers.get("X-Identity-Relay")

    @routes.post("/identity/v2/jobs")
    def submit():
        def action():
            value = body(("relay_id", "request_b64"))
            return jobs.submit(value["relay_id"], wire.b64decode(value["request_b64"], 16384), capability())
        return respond(action)

    @routes.get("/identity/v2/jobs/<relay_id>")
    def status(relay_id):
        return respond(lambda: jobs.status(relay_id, capability()))

    @routes.post("/identity/v2/jobs/<relay_id>/approval")
    def approve(relay_id):
        def action():
            value = body(("probe_id", "approval_b64"))
            return jobs.approve(relay_id, value["probe_id"], wire.b64decode(value["approval_b64"], 16384), capability())
        return respond(action)

    @routes.post("/identity/v2/jobs/<relay_id>/cancel")
    def cancel(relay_id):
        def action():
            body(())
            return jobs.cancel(relay_id, capability())
        return respond(action)

    app.register_blueprint(routes)
    return jobs
