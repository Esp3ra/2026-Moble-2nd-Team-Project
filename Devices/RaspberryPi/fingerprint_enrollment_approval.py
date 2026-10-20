"""Pi receiver for PR44 ba6a37a approval contract; no PC bearer or HTTP client.

exchange(enrollment_id, seven_field_body, cancellation) must relay through the
original authenticated PC session and return DECODED approval envelope bytes.
It must preserve those bytes, propagate errors, and never implement offline allow.
"""

from dataclasses import dataclass
from datetime import timedelta
import hmac
import math
import re
import secrets
import threading
import time
from types import MappingProxyType

from fingerprint_enrollment import EnrollmentError
from fingerprint_enrollment_wire import (
    COMMON, HEADERS, MAX_PAYLOAD, canonical_uuid, decode64, encode64, integer,
    parse_json, require, revision, timestamp,
)


KIND = "enroll_approval"
DOMAIN = b"RW-ENROLL-APPROVAL-v1\0"
CONTEXT = set(COMMON) | {"ticket_digest"}
APPROVAL_FIELDS = CONTEXT | {"probe_nonce", "scope", "decision", "server_time", "valid_until"}
MAX_RTT, MAX_LIFETIME, SAFETY = 2.0, 5.0, 1.25


def suspend_aware_clock():
    """Fail closed off Linux unless caller supplies a verified clock/resume policy."""
    if not hasattr(time, "CLOCK_BOOTTIME"):
        raise EnrollmentError("approval_clock_unavailable")
    return time.clock_gettime(time.CLOCK_BOOTTIME)


def validate_context(value):
    require(type(value) is dict and set(value) == CONTEXT)
    canonical_uuid(value["enrollment_id"])
    integer(value["reserved_slot"])
    for name in ("operation_revision", "claim_revision", "key_epoch"):
        revision(value[name])
    require(type(value["ticket_digest"]) is str and re.fullmatch(
        r"[0-9a-f]{64}", value["ticket_digest"]) is not None)


def verify_approval(raw, *, keys, expected):
    envelope = parse_json(raw)
    require(set(envelope) == HEADERS)
    require(type(envelope["v"]) is int and envelope["v"] == 1)
    require(envelope["kind"] == KIND and envelope["device_id"] == keys.device_id
            and envelope["key_id"] == keys.key_id)
    decoded = decode64(envelope["payload_b64"])
    message = DOMAIN + "\0".join(
        envelope[name] for name in ("kind", "device_id", "key_id", "payload_b64")
    ).encode("ascii")
    received = decode64(envelope["mac_b64"], 32)
    require(len(received) == 32 and hmac.compare_digest(
        received, hmac.digest(keys.request_key, message, "sha256")))
    payload = parse_json(decoded, MAX_PAYLOAD)
    require(set(payload) == APPROVAL_FIELDS)
    validate_context({name: payload[name] for name in CONTEXT})
    require(payload["key_epoch"] == keys.key_epoch)
    require(type(payload["scope"]) is str and payload["scope"] in ("sensor_write", "result_read"))
    require(payload["decision"] == "allow")
    require(16 <= len(decode64(payload["probe_nonce"], 64)) <= 64)
    require(set(expected) == CONTEXT | {"scope", "probe_nonce"})
    require(all(payload[name] == expected[name] for name in expected))
    stamp, until = timestamp(payload["server_time"]), timestamp(payload["valid_until"])
    require(0 < (until - stamp).total_seconds() <= MAX_LIFETIME)
    return stamp, until


@dataclass(frozen=True)
class _Permit:
    server_time: object
    valid_until: object
    received: float
    rtt: float

    def utc(self, now):
        require(now >= self.received)
        upper = self.server_time + timedelta(seconds=SAFETY + self.rtt + now - self.received)
        require(upper < self.valid_until)
        return upper


class OnlineEnrollmentApproval:
    """One job and one scope per in-memory session; never persisted or shared.

    A fresh nonce is generated for EVERY exchange. Any failed refresh clears
    the old permit. Invalidation/cancellation also rejects in-flight replies.
    """

    def __init__(self, *, context, keys, scope, exchange, cancellation,
                 monotonic=suspend_aware_clock):
        validate_context(context)
        require(scope in ("sensor_write", "result_read"))
        require(context["key_epoch"] == keys.key_epoch)
        self.context = MappingProxyType(dict(context))
        self.keys, self.scope, self.exchange = keys, scope, exchange
        self.cancellation, self.clock = cancellation, monotonic
        self._lock = threading.RLock()
        self._generation, self._closed, self._permit, self._last_clock = 0, False, None, None

    def _now(self):
        value = self.clock()
        require(type(value) in (int, float) and math.isfinite(value) and value >= 0)
        with self._lock:
            require(self._last_clock is None or value >= self._last_clock)
            self._last_clock = value
        return value

    def _live(self):
        require(not self._closed and not self.cancellation.is_set())

    def refresh(self):
        try:
            with self._lock:
                self._live()
                self._generation += 1
                generation = self._generation
                self._permit = None
                expected = dict(self.context, scope=self.scope,
                                probe_nonce=encode64(secrets.token_bytes(32)))
                body = {name: value for name, value in expected.items() if name != "enrollment_id"}
                sent = self._now()
            raw = self.exchange(self.context["enrollment_id"], body, self.cancellation)
            received = self._now()
            require(0 <= received - sent <= MAX_RTT)
            stamp, until = verify_approval(raw, keys=self.keys, expected=expected)
            permit = _Permit(stamp, until, received, received - sent)
            with self._lock:
                self._live()
                require(generation == self._generation)
                permit.utc(self._now())  # Verification time is part of the elapsed budget.
                self._permit = permit
            return True
        except Exception:
            with self._lock:
                # No fallback to a previous valid approval after a failed refresh.
                self._permit = None
            raise EnrollmentError("not_authorized") from None

    def current_approval(self, ticket):
        try:
            expected = {name: ticket.payload[name] for name in COMMON}
            expected["ticket_digest"] = ticket.operation.ticket_digest
            require(expected == dict(self.context) and self.scope == "sensor_write")
            return self.refresh()
        except Exception:
            self.invalidate()
            return False

    def trusted_utc(self):
        try:
            with self._lock:
                self._live()
                require(self._permit is not None)
                return self._permit.utc(self._now())
        except Exception:
            raise EnrollmentError("not_authorized") from None

    def delivery_seconds(self):
        """Remaining conservative permit lifetime for one immediate transport delivery."""
        try:
            with self._lock:
                self._live()
                require(self._permit is not None)
                upper = self._permit.utc(self._now())
                return (self._permit.valid_until - upper).total_seconds()
        except Exception:
            raise EnrollmentError("not_authorized") from None

    def invalidate(self):
        with self._lock:
            self._generation += 1
            self._closed, self._permit = True, None
