"""RW-ID-v2 contract, byte verifier and receiver freshness reference.

This is NOT an authentication service. There are no HTTP routes, database writes,
session grants or attendance effects here. See docs/DEVICE_IDENTITY_V2.md.
v1 and enrollment wire/keys are deliberately not reused.
"""
from dataclasses import dataclass
from datetime import date, timedelta
import hashlib
import hmac
import json
import math
import re
import secrets

from .device_protocol_draft import (
    ProtocolError, parse_json, b64encode, b64decode, canonical_uuid,
    MAX_PAYLOAD_BYTES, BIGINT_MAX, _time,
)

VERSION = 2
DOMAIN = b"RW-ID-v2\0"
KINDS = frozenset(("id_request", "id_probe", "id_approval", "id_result"))
PURPOSES = frozenset(("clock_in", "noon_check", "clock_out"))
CHALLENGE_SECONDS = 60
APPROVAL_SECONDS = 20
MAX_RTT_SECONDS = 2
SAFETY_SECONDS = 1.25  # whole-second UTC rounding + short-interval drift
CAPTURE_SECONDS = 15
PENDING_LOGIN_SECONDS = 300
RESULT_LOOKUP_SECONDS = 86400
HEADER = frozenset(("v", "kind", "device_id", "key_id", "payload_b64", "mac_b64"))
COMMON = frozenset(("challenge_id", "nonce", "mapping_revision", "key_epoch"))
BINDING = COMMON | {"ticket_digest"}
PROBE = BINDING | {"probe_nonce", "scope"}
EXTRA = {
    "id_request": {"expected_slot", "purpose", "session_ref", "work_session_ref", "work_date", "issued_at", "expires_at"},
    "id_probe": {"ticket_digest", "probe_nonce", "scope"},
    "id_approval": {"ticket_digest", "probe_nonce", "scope", "decision", "server_time", "valid_until"},
    "id_result": {"ticket_digest", "result_id", "outcome", "slot", "failure_code", "approval_digest"},
}
FAILURES = {"matched": None, "no_match": None, "busy": "SENSOR_BUSY",
            "timeout": "CAPTURE_TIMEOUT", "cancelled": "CAPTURE_CANCELLED", "sensor_error": "SENSOR_ERROR"}


def _fail():
    raise ProtocolError("Invalid identity v2 message")


def _revision(value):
    if (type(value) is not str or re.fullmatch(r"[1-9][0-9]{0,18}", value) is None
            or int(value) > BIGINT_MAX):
        _fail()


def _digest(value):
    if type(value) is not str or re.fullmatch(r"[0-9a-f]{64}", value) is None:
        _fail()


def _nonce(value):
    if not 16 <= len(b64decode(value, 64)) <= 64:
        _fail()


def _slot(value):
    if type(value) is not int or not 0 <= value <= 2147483647:
        _fail()


def validate(value, kind):
    if type(kind) is not str or kind not in KINDS or type(value) is not dict or set(value) != COMMON | EXTRA[kind]:
        _fail()
    canonical_uuid(value["challenge_id"])
    _nonce(value["nonce"])
    _revision(value["mapping_revision"])
    _revision(value["key_epoch"])
    if kind == "id_request":
        _slot(value["expected_slot"])
        if type(value["purpose"]) is not str or value["purpose"] not in PURPOSES:
            _fail()
        _nonce(value["session_ref"])
        if value["purpose"] == "clock_in":
            if value["work_session_ref"] is not None:
                _fail()
        else:
            canonical_uuid(value["work_session_ref"])
        day = value["work_date"]
        if type(day) is not str or re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", day) is None:
            _fail()
        try:
            date.fromisoformat(day)
        except ValueError:
            _fail()
        seconds = (_time(value["expires_at"]) - _time(value["issued_at"])).total_seconds()
        if not 0 < seconds <= CHALLENGE_SECONDS:
            _fail()
    else:
        _digest(value["ticket_digest"])
        if kind in ("id_probe", "id_approval"):
            _nonce(value["probe_nonce"])
            if type(value["scope"]) is not str or value["scope"] not in ("capture", "result"):
                _fail()
            if kind == "id_approval":
                if value["decision"] != "allow":
                    _fail()
                seconds = (_time(value["valid_until"]) - _time(value["server_time"])).total_seconds()
                if not 0 < seconds <= APPROVAL_SECONDS:
                    _fail()
        else:
            canonical_uuid(value["result_id"])
            outcome = value["outcome"]
            if type(outcome) is not str or outcome not in FAILURES or value["failure_code"] != FAILURES[outcome]:
                _fail()
            if outcome == "matched":
                _slot(value["slot"])
            elif value["slot"] is not None:
                _fail()
            if outcome in ("matched", "no_match"):
                _digest(value["approval_digest"])
            elif value["approval_digest"] is not None:
                _fail()
    return value


def mac_input(envelope):
    if (type(envelope) is not dict or set(envelope) != HEADER
            or type(envelope["v"]) is not int or envelope["v"] != VERSION
            or type(envelope["kind"]) is not str or envelope["kind"] not in KINDS):
        _fail()
    canonical_uuid(envelope["device_id"])
    if (type(envelope["key_id"]) is not str
            or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", envelope["key_id"]) is None):
        _fail()
    b64decode(envelope["payload_b64"])
    return DOMAIN + (envelope["kind"] + "\0" + envelope["device_id"] + "\0"
                     + envelope["key_id"] + "\0" + envelope["payload_b64"]).encode("ascii")


def _key(key):
    if type(key) is not bytes or len(key) != 32:
        raise ValueError("An identity-only 32-byte directional key is required")


def sign(payload_bytes, *, kind, device_id, key_id, key):
    """Server/Pi only. PC never holds keys or serializes the payload again."""
    _key(key)
    validate(parse_json(payload_bytes, MAX_PAYLOAD_BYTES), kind)
    e = dict(v=VERSION, kind=kind, device_id=device_id, key_id=key_id,
             payload_b64=b64encode(payload_bytes), mac_b64="")
    e["mac_b64"] = b64encode(hmac.digest(key, mac_input(e), "sha256"))
    return json.dumps(e, separators=(",", ":")).encode("ascii")


def verify(raw, *, kind, device_id, key_id, key):
    _key(key)
    e = parse_json(raw)
    signed = mac_input(e)
    if e["kind"] != kind or e["device_id"] != device_id or e["key_id"] != key_id:
        _fail()
    mac = b64decode(e["mac_b64"], 32)
    if len(mac) != 32 or not hmac.compare_digest(mac, hmac.digest(key, signed, "sha256")):
        _fail()
    return validate(parse_json(b64decode(e["payload_b64"]), MAX_PAYLOAD_BYTES), kind)


def message_digest(raw):
    """SHA256 of the COMPLETE envelope bytes; call verify first at trust boundaries."""
    mac_input(parse_json(raw))
    return hashlib.sha256(raw).hexdigest()


def bind_result(result, ticket, *, ticket_bytes, result_approval_bytes=None):
    """After independently verifying all MACs, check cross-message bindings.

    Current rights, DB single consumption, request expiry and the issued approval
    ledger are service responsibilities; this reference cannot assert them.
    """
    validate(result, "id_result"); validate(ticket, "id_request")
    e = parse_json(ticket_bytes)
    if e.get("kind") != "id_request" or parse_json(b64decode(e["payload_b64"])) != ticket:
        _fail()
    if any(result[k] != ticket[k] for k in COMMON) or result["ticket_digest"] != message_digest(ticket_bytes):
        _fail()
    if result["outcome"] == "matched" and result["slot"] != ticket["expected_slot"]:
        _fail()
    if result["outcome"] in ("matched", "no_match"):
        if result_approval_bytes is None or result["approval_digest"] != message_digest(result_approval_bytes):
            _fail()
        approval_envelope = parse_json(result_approval_bytes)
        if approval_envelope.get("kind") != "id_approval":
            _fail()
        approval = parse_json(b64decode(approval_envelope["payload_b64"]))
        validate(approval, "id_approval")
        if (approval["scope"] != "result" or any(approval[k] != result[k] for k in BINDING)
                or _time(approval["valid_until"]) > _time(ticket["expires_at"])
                or _time(approval["server_time"]) < _time(ticket["issued_at"])):
            _fail()


@dataclass(frozen=True)
class FreshApproval:
    """RAM-only receiver budget. A suspend-aware monotonic clock is mandatory."""
    server_time: object
    valid_until: object
    received: float
    rtt: float
    scope: str

    def remaining(self, now):
        if type(now) not in (int, float) or not math.isfinite(now) or now < self.received:
            _fail()
        upper = self.server_time + timedelta(seconds=SAFETY_SECONDS + self.rtt + now - self.received)
        seconds = (self.valid_until - upper).total_seconds()
        if seconds <= 0:
            _fail()
        return seconds

    def capture_budget(self, now):
        if self.scope != "capture":
            _fail()
        return min(CAPTURE_SECONDS, self.remaining(now))


class ApprovalProbe:
    """One new unpredictable Pi nonce BEFORE every send; never persist/replay it."""
    def __init__(self, binding, scope, clock, *, ticket_expires_at):
        if type(binding) is not dict or set(binding) != BINDING:
            raise ValueError("All challenge/ticket bindings required")
        self.payload = dict(binding, scope=scope, probe_nonce=b64encode(secrets.token_bytes(32)))
        validate(self.payload, "id_probe")
        self._expected = dict(self.payload)
        self._expires = _time(ticket_expires_at)
        self._clock, self._sent, self._pending = clock, clock(), True
        if type(self._sent) not in (int, float) or not math.isfinite(self._sent):
            _fail()

    def accept(self, raw, *, device_id, key_id, key):
        if not self._pending:
            _fail()
        self._pending = False
        received = self._clock()
        if (type(received) not in (int, float) or not math.isfinite(received)
                or not 0 <= received - self._sent <= MAX_RTT_SECONDS):
            _fail()
        p = verify(raw, kind="id_approval", device_id=device_id, key_id=key_id, key=key)
        if any(p[k] != v for k, v in self._expected.items()):
            _fail()
        if _time(p["valid_until"]) > self._expires:
            _fail()
        result = FreshApproval(_time(p["server_time"]), _time(p["valid_until"]),
                               received, received - self._sent, p["scope"])
        result.remaining(received)
        return result

    def invalidate(self):
        self._pending = False


def validate_api_input(operation, raw):
    """Request DTO validation for adapter tests, not an installed HTTP handler."""
    fields = {"login": {"login_id", "password", "client_request_id"},
              "issue": {"device_id", "purpose", "client_request_id"},
              "approval": {"probe_b64"}, "complete": {"result_b64"},
              "cancel": {"client_request_id"}, "finalize": {"client_request_id"},
              "revoke": {"client_request_id"}}
    if type(operation) is not str or operation not in fields:
        raise ValueError("Unknown identity API operation")
    value = parse_json(raw, 32 * 1024)
    if set(value) != fields[operation]:
        _fail()
    if "client_request_id" in value:
        canonical_uuid(value["client_request_id"])
    if operation == "login":
        if (type(value["login_id"]) is not str or not 1 <= len(value["login_id"]) <= 50
                or type(value["password"]) is not str or not 1 <= len(value["password"]) <= 128):
            _fail()
    if operation == "issue":
        canonical_uuid(value["device_id"])
        if type(value["purpose"]) is not str or value["purpose"] not in PURPOSES:
            _fail()
    if operation in ("approval", "complete"):
        name = "probe_b64" if operation == "approval" else "result_b64"
        envelope = parse_json(b64decode(value[name], 16 * 1024))
        mac_input(envelope)
        if envelope["kind"] != ("id_probe" if operation == "approval" else "id_result"):
            _fail()
    return value


def readiness():
    """Configuration-only report: no keys/config values, HTTP or DB access."""
    return {"code": "IDENTITY_WORKFLOW_NOT_READY", "contract": "RW-ID-v2",
            "contract_available": True, "http_api_implemented": True,
            "limited_login_implemented": True, "mysql_storage_implemented": True,
            "attendance_implemented": True,
            "attendance_connected": False, "enabled": False, "ready": False}


if __name__ == "__main__":
    print(json.dumps(readiness(), separators=(",", ":")))
