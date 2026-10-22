"""Disconnected RW-ID-v2 Pi adapter. No HTTP route, config loader or deployment.

One instance owns one verified request and one RAM-only result. The future
transport supplies the approval round trip; no bearer, DB or server address is
accepted here. All four keys must come from a reviewed private Pi profile.
"""
from dataclasses import dataclass, field
import json
import math
import threading
import time
from types import MappingProxyType
from uuid import uuid4

import fingerprint_identity_wire as wire
from fingerprint_match import _compare_owned, CaptureAuthorizationError, SensorComparison


class IdentityUnavailable(RuntimeError):
    def __init__(self):
        super().__init__("Identity approval unavailable; no automatic recapture")


@dataclass(frozen=True)
class IdentityProfile:
    device_id: str
    key_id: str
    key_epoch: str
    enabled: bool
    minimum_slot: int
    maximum_slot: int
    capacity: int
    keys: object = field(repr=False)
    # Integration must supply existing enrollment/v1 key values, never print them.
    excluded_keys: tuple = field(repr=False)

    def __post_init__(self):
        wire.canonical_uuid(self.device_id)
        wire._revision(self.key_epoch)
        if (type(self.enabled) is not bool or not self.enabled
                or any(type(x) is not int for x in (self.minimum_slot, self.maximum_slot, self.capacity))
                or not 0 <= self.minimum_slot <= self.maximum_slot < self.capacity <= 65536
                or type(self.keys) is not dict or set(self.keys) != wire.KINDS
                or type(self.excluded_keys) is not tuple or not self.excluded_keys
                or any(type(k) is not bytes or len(k) < 32 for k in self.excluded_keys)):
            raise ValueError("Invalid identity profile")
        keys = dict(self.keys)
        for key in keys.values():
            wire._key(key)
        # The public PR62 vector keys are never valid runtime profile keys.
        excluded = set(self.excluded_keys) | {bytes([n]) * 32 for n in range(1, 5)}
        if len(set(keys.values())) != 4 or any(k in excluded for k in keys.values()):
            raise ValueError("Identity keys must be private and independent")
        # Validate key_id using the same strict envelope grammar, without logging it.
        wire.mac_input(dict(v=2, kind="id_probe", device_id=self.device_id,
                            key_id=self.key_id, payload_b64="e30", mac_b64=""))
        object.__setattr__(self, "keys", MappingProxyType(keys))


def boottime_clock():
    """Linux CLOCK_BOOTTIME advances during suspend; fail closed elsewhere."""
    if not hasattr(time, "CLOCK_BOOTTIME"):
        raise IdentityUnavailable()
    return time.clock_gettime(time.CLOCK_BOOTTIME)


class IdentityAttempt:
    def __init__(self, service, profile, request_bytes, *, clock=boottime_clock):
        if not isinstance(profile, IdentityProfile) or type(request_bytes) is not bytes:
            raise ValueError("Trusted identity profile and original request bytes required")
        self._profile, self._service, self._clock = profile, service, clock
        self._request_bytes = request_bytes
        self._ticket = wire.verify(request_bytes, kind="id_request", device_id=profile.device_id,
                                   key_id=profile.key_id, key=profile.keys["id_request"])
        ticket = self._ticket
        if (ticket["key_epoch"] != profile.key_epoch
                or not profile.minimum_slot <= ticket["expected_slot"] <= profile.maximum_slot):
            raise ValueError("Identity request does not match approved profile")
        self._binding = {k: ticket[k] for k in wire.COMMON}
        self._binding["ticket_digest"] = wire.message_digest(request_bytes)
        self._gate = threading.Lock()
        self._started, self._result, self._last_time = False, None, None

    def _now(self):
        now = self._clock()
        if (type(now) not in (int, float) or not math.isfinite(now)
                or self._last_time is not None and now < self._last_time):
            raise IdentityUnavailable()
        self._last_time = now
        return now

    def run(self, approval_exchange, *, cancelled=lambda: False):
        """exchange(signed_probe_bytes, cancelled) -> signed approval bytes.

        The transport must bound/cancel its wait. RTT is independently checked
        after every response. Repeating a finished run returns identical bytes;
        an interrupted run cannot start the sensor again.
        """
        if not callable(approval_exchange) or not callable(cancelled):
            raise ValueError("Approval transport and cancellation predicate required")
        if not self._gate.acquire(blocking=False):
            raise IdentityUnavailable()
        try:
            self._now()
            if self._result is not None:
                if cancelled() or self._service.stopping.is_set():
                    raise IdentityUnavailable()
                return self._result
            if self._started:
                raise IdentityUnavailable()
            self._started = True
            stopping = lambda: cancelled() or self._service.stopping.is_set()
            if stopping():
                return self._finish(SensorComparison("cancelled"))
            if not self._service.lock.acquire(blocking=False):
                return self._finish(SensorComparison("busy"))
            try:
                capture, _ = self._fresh("capture", approval_exchange, stopping)

                def authorized():
                    try:
                        capture.remaining(self._now())
                    except (wire.ProtocolError, IdentityUnavailable):
                        raise CaptureAuthorizationError() from None

                def capture_clock():
                    try:
                        return self._now()
                    except IdentityUnavailable:
                        raise CaptureAuthorizationError() from None

                comparison = _compare_owned(self._service, self._ticket["expected_slot"],
                    cancelled=stopping, timeout_seconds=capture.capture_budget(self._now()),
                    ensure_authorized=authorized, clock=capture_clock)
                if stopping():
                    comparison = SensorComparison("cancelled")
                approval_bytes = None
                if comparison.outcome in ("matched", "different_finger", "unregistered"):
                    fresh, approval_bytes = self._fresh("result", approval_exchange, stopping)
                    fresh.remaining(self._now())
                    result = self._finish(comparison, approval_bytes)
                    fresh.remaining(self._now())
                    if stopping():
                        self._result = None
                        raise IdentityUnavailable()
                    return result
                return self._finish(comparison)
            finally:
                self._service.lock.release()
        except (wire.ProtocolError, CaptureAuthorizationError, IdentityUnavailable):
            self._result = None
            raise IdentityUnavailable() from None
        finally:
            self._gate.release()

    def _fresh(self, scope, exchange, stopping):
        if stopping():
            raise IdentityUnavailable()
        probe = wire.ApprovalProbe(self._binding, scope, self._now,
                                   ticket_expires_at=self._ticket["expires_at"])
        try:
            raw_probe = self._sign(probe.payload, "id_probe")
            raw_approval = exchange(raw_probe, stopping)
            if stopping():
                raise IdentityUnavailable()
            fresh = probe.accept(raw_approval, device_id=self._profile.device_id,
                                 key_id=self._profile.key_id, key=self._profile.keys["id_approval"])
            if fresh.server_time < wire._time(self._ticket["issued_at"]):
                raise IdentityUnavailable()
            return fresh, raw_approval
        except Exception:
            probe.invalidate()
            raise IdentityUnavailable() from None

    def _sign(self, payload, kind):
        return wire.sign(json.dumps(payload, separators=(",", ":")).encode("utf-8"),
                         kind=kind, device_id=self._profile.device_id,
                         key_id=self._profile.key_id, key=self._profile.keys[kind])

    def _finish(self, comparison, approval_bytes=None):
        outcome = {"different_finger": "no_match", "unregistered": "no_match"}.get(
            comparison.outcome, comparison.outcome)
        if outcome not in wire.FAILURES:
            raise IdentityUnavailable()
        payload = dict(self._binding, result_id=str(uuid4()), outcome=outcome,
                       slot=self._ticket["expected_slot"] if outcome == "matched" else None,
                       failure_code=wire.FAILURES[outcome],
                       approval_digest=wire.message_digest(approval_bytes) if approval_bytes is not None else None)
        raw = self._sign(payload, "id_result")
        decoded = wire.verify(raw, kind="id_result", device_id=self._profile.device_id,
                              key_id=self._profile.key_id, key=self._profile.keys["id_result"])
        wire.bind_result(decoded, self._ticket, ticket_bytes=self._request_bytes,
                         result_approval_bytes=approval_bytes)
        self._result = raw
        return raw


class IdentityAttemptPool:
    """One process owner must reuse this bounded pool, not rebuild per RPC.

    Same challenge + exact original bytes returns the same one-shot attempt.
    No expiry eviction/reset/reconstruction is provided. A full pool fails closed;
    restart reconciliation with server state is an integration prerequisite.
    """
    def __init__(self, service, profile, *, clock=boottime_clock, maximum=64):
        if type(maximum) is not int or not 1 <= maximum <= 1024:
            raise ValueError("Invalid RAM attempt limit")
        self._service, self._profile, self._clock = service, profile, clock
        self._maximum, self._attempts, self._gate = maximum, {}, threading.Lock()

    def prepare(self, request_bytes):
        candidate = IdentityAttempt(self._service, self._profile, request_bytes, clock=self._clock)
        challenge = candidate._ticket["challenge_id"]
        with self._gate:
            previous = self._attempts.get(challenge)
            if previous is not None:
                if previous._request_bytes != request_bytes:
                    raise IdentityUnavailable()
                return previous
            if len(self._attempts) >= self._maximum:
                raise IdentityUnavailable()
            self._attempts[challenge] = candidate
            return candidate
