"""Separate online permit bytes; the server must check current rights first.

The reference lifetime uses a receiver's fresh probe/monotonic interval, never
an untrusted Pi wall clock. A permit is bounded evidence, not an atomic sensor
write or proof that a server mapping committed.
"""
from dataclasses import dataclass
from datetime import timedelta, timezone
import hmac
import json
import math
import re
import secrets

from . import enrollment_protocol_draft as enroll

KIND = "enroll_approval"
DOMAIN = b"RW-ENROLL-APPROVAL-v1\0"
FIELDS = enroll.COMMON | {"ticket_digest", "probe_nonce", "scope", "decision", "server_time", "valid_until"}
MAX_LIFETIME_SECONDS = 5
MAX_RTT_SECONDS = 2
SAFETY_SECONDS = 1.25  # UTC whole-second truncation + short-interval drift margin.


def _fail():
    raise enroll.ProtocolError("Invalid enrollment approval")


def validate(value):
    if type(value) is not dict or set(value) != FIELDS:
        _fail()
    enroll.canonical_uuid(value["enrollment_id"])
    enroll._integer(value["reserved_slot"], 0)
    for field in ("operation_revision", "claim_revision", "key_epoch"):
        enroll._revision(value[field])
    if (type(value["ticket_digest"]) is not str or re.fullmatch(r"[0-9a-f]{64}", value["ticket_digest"]) is None
            or not 16 <= len(enroll.b64decode(value["probe_nonce"], 64)) <= 64
            or value["scope"] not in ("sensor_write", "sensor_capture", "sensor_store", "result_read") or value["decision"] != "allow"):
        _fail()
    lifetime = (enroll._time(value["valid_until"]) - enroll._time(value["server_time"])).total_seconds()
    if not 0 < lifetime <= MAX_LIFETIME_SECONDS:
        _fail()


def mac_input(envelope):
    if (type(envelope) is not dict or set(envelope) != enroll.FIELDS
            or type(envelope["v"]) is not int or envelope["v"] != 1 or envelope["kind"] != KIND):
        _fail()
    enroll.canonical_uuid(envelope["device_id"])
    if type(envelope["key_id"]) is not str or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", envelope["key_id"]) is None:
        _fail()
    enroll.b64decode(envelope["payload_b64"])
    return DOMAIN + (KIND + "\0" + envelope["device_id"] + "\0" + envelope["key_id"]
                     + "\0" + envelope["payload_b64"]).encode("ascii")


def sign(payload, *, device_id, key_id, key):
    enroll._key(key); validate(payload)
    raw = json.dumps(payload, separators=(",", ":")).encode("ascii")
    envelope = dict(v=1, kind=KIND, device_id=device_id, key_id=key_id,
                    payload_b64=enroll.b64encode(raw), mac_b64="")
    envelope["mac_b64"] = enroll.b64encode(hmac.digest(key, mac_input(envelope), "sha256"))
    return json.dumps(envelope, separators=(",", ":")).encode("ascii")


def verify(raw, *, expected, device_id, key_id, key):
    """Verify MAC and exact fresh-probe/job/scope binding; not receiver elapsed time."""
    enroll._key(key)
    e = enroll.parse_json(raw); message = mac_input(e)
    if e["device_id"] != device_id or e["key_id"] != key_id:
        _fail()
    mac = enroll.b64decode(e["mac_b64"], 32)
    if len(mac) != 32 or not hmac.compare_digest(mac, hmac.digest(key, message, "sha256")):
        _fail()
    payload = enroll.parse_json(enroll.b64decode(e["payload_b64"]), enroll.MAX_PAYLOAD_BYTES)
    validate(payload)
    if set(expected) != enroll.COMMON | {"ticket_digest", "probe_nonce", "scope"}:
        raise ValueError("All job/probe/scope bindings are required")
    if any(payload[field] != value for field, value in expected.items()):
        _fail()
    return payload


@dataclass(frozen=True)
class FreshApproval:
    """Receiver reference: use a clock covering suspend, or invalidate on resume.

    A new probe must be generated BEFORE send, kept in RAM and accepted once.
    The owner must also invalidate on reboot/login/target/device/job change.
    """
    server_time: object
    valid_until: object
    received_mono: float
    rtt: float
    scope: str

    @classmethod
    def accept(cls, payload, *, sent_mono, received_mono):
        validate(payload)
        if (not all(type(x) in (int, float) and math.isfinite(x) for x in (sent_mono, received_mono))
                or not 0 <= received_mono - sent_mono <= MAX_RTT_SECONDS):
            _fail()
        result = cls(enroll._time(payload["server_time"]).replace(tzinfo=timezone.utc),
                     enroll._time(payload["valid_until"]).replace(tzinfo=timezone.utc),
                     received_mono, received_mono - sent_mono, payload["scope"])
        result.trusted_utc(received_mono)
        return result

    def trusted_utc(self, now_mono):
        if type(now_mono) not in (int, float) or not math.isfinite(now_mono) or now_mono < self.received_mono:
            _fail()
        upper = self.server_time + timedelta(seconds=SAFETY_SECONDS + self.rtt + now_mono - self.received_mono)
        if upper >= self.valid_until:
            _fail()
        return upper

    def allows(self, scope, now_mono):
        self.trusted_utc(now_mono)
        return self.scope == scope


class ApprovalProbe:
    """One-use in-memory receiver probe. The caller supplies a suspend-aware clock.

    Construct immediately before sending the HTTP request. Discard on any
    cancellation/reboot/login/device/job change; never serialize this object.
    This reference does no HTTP or sensor I/O and owns no persistent key.
    """
    def __init__(self, *, expected, monotonic):
        if set(expected) != enroll.COMMON | {"ticket_digest", "scope"}:
            raise ValueError("All enrollment bindings and scope required")
        self.expected = dict(expected, probe_nonce=enroll.b64encode(secrets.token_bytes(32)))
        self._clock = monotonic
        self._sent = monotonic()
        self._pending = True

    def accept(self, raw, *, device_id, key_id, key):
        if not self._pending:
            _fail()
        self._pending = False  # A failed/tampered response also consumes this probe.
        received = self._clock()
        value = verify(raw, expected=self.expected, device_id=device_id, key_id=key_id, key=key)
        return FreshApproval.accept(value, sent_mono=self._sent, received_mono=received)

    def invalidate(self):
        self._pending = False
