"""Pi-only offline codec for PR44's enroll-v1 draft, not current authorization.

Compared with member 1's 2a34e22 proposal; no server package dependency.
No keys, routes, hardware, wall clock or permission defaults are installed.
"""

import base64
from dataclasses import dataclass, field
from datetime import datetime, timezone
import hashlib
import hmac
import json
import re
from uuid import UUID


REQUEST, RESULT = "enroll_request", "enroll_result"
MAX_ENVELOPE, MAX_PAYLOAD = 16384, 8192
COMMON = ("enrollment_id", "operation_revision", "reserved_slot", "claim_revision", "key_epoch")
HEADERS = {"v", "kind", "device_id", "key_id", "payload_b64", "mac_b64"}
REQUEST_FIELDS = set(COMMON) | {
    "target_user_id", "nonce", "purpose", "issued_at", "expires_at", "session_ref",
}
RESULT_FIELDS = set(COMMON) | {
    "ticket_digest", "result_id", "outcome", "failure_code", "elapsed_ms",
}
FAILURES = {
    "busy", "cancelled", "timeout", "sensor_full", "slot_conflict", "duplicate_finger",
    "different_finger", "capacity_mismatch", "sensor_error", "storage_uncertain",
    "ledger_error", "authorization_lost",
}


class WireError(ValueError):
    def __init__(self):
        super().__init__("Invalid enrollment message")


def require(condition):
    if not condition:
        raise WireError()


def _object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result)
        result[key] = value
    return result


def _constant(_):
    raise WireError()


def parse_json(raw, maximum=MAX_ENVELOPE):
    require(type(raw) is bytes and 1 <= len(raw) <= maximum)
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_object, parse_constant=_constant)
        json.dumps(value, ensure_ascii=False, allow_nan=False).encode("utf-8")
        require(type(value) is dict)
        return value
    except (ValueError, UnicodeError, RecursionError, OverflowError):
        raise WireError() from None


def encode64(raw):
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def decode64(value, maximum=MAX_PAYLOAD):
    require(type(value) is str and 1 <= len(value) <= (maximum * 4 + 2) // 3)
    require(re.fullmatch(r"[A-Za-z0-9_-]+", value) is not None)
    try:
        raw = base64.b64decode(value + "=" * (-len(value) % 4), altchars=b"-_", validate=True)
    except ValueError:
        raise WireError() from None
    require(len(raw) <= maximum and encode64(raw) == value)
    return raw


def canonical_uuid(value):
    try:
        require(type(value) is str and str(UUID(value)) == value)
    except (ValueError, AttributeError, TypeError):
        raise WireError() from None


def revision(value):
    require(type(value) is str and re.fullmatch(r"[1-9][0-9]{0,18}", value) is not None)
    require(int(value) <= 9223372036854775807)


def integer(value, minimum=0):
    require(type(value) is int and minimum <= value <= 2147483647)


def timestamp(value):
    require(type(value) is str and re.fullmatch(
        r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z", value) is not None)
    try:
        return datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError:
        raise WireError() from None


def validate_payload(payload, kind):
    require(kind in (REQUEST, RESULT) and type(payload) is dict)
    require(set(payload) == (REQUEST_FIELDS if kind == REQUEST else RESULT_FIELDS))
    canonical_uuid(payload["enrollment_id"])
    integer(payload["reserved_slot"])
    for name in ("operation_revision", "claim_revision", "key_epoch"):
        revision(payload[name])
    if kind == REQUEST:
        integer(payload["target_user_id"], 1)
        require(16 <= len(decode64(payload["nonce"], 64)) <= 64)
        require(payload["purpose"] == "fingerprint_enrollment")
        require(type(payload["session_ref"]) is str and re.fullmatch(
            r"[A-Za-z0-9_-]{1,128}", payload["session_ref"]) is not None)
        require(timestamp(payload["issued_at"]) < timestamp(payload["expires_at"]))
    else:
        canonical_uuid(payload["result_id"])
        require(type(payload["ticket_digest"]) is str and re.fullmatch(
            r"[0-9a-f]{64}", payload["ticket_digest"]) is not None)
        integer(payload["elapsed_ms"])
        outcome, failure = payload["outcome"], payload["failure_code"]
        require(type(outcome) is str)
        if outcome == "sensor_stored":
            require(failure is None)
        else:
            require(outcome in ("failed", "recovery_required") and type(failure) is str)
            require(failure in FAILURES)
            require(outcome != "failed" or failure not in ("storage_uncertain", "ledger_error"))


@dataclass(frozen=True)
class DirectionKeys:
    """Selected from trusted Pi configuration, never from an incoming key ID."""
    device_id: str
    key_id: str
    key_epoch: str
    request_key: bytes = field(repr=False)
    result_key: bytes = field(repr=False)

    def __post_init__(self):
        canonical_uuid(self.device_id)
        require(type(self.key_id) is str and re.fullmatch(r"[A-Za-z0-9_-]{1,64}", self.key_id) is not None)
        revision(self.key_epoch)
        require(all(type(k) is bytes and len(k) >= 32 for k in (self.request_key, self.result_key)))
        require(not hmac.compare_digest(self.request_key, self.result_key))


def mac_input(envelope):
    require(type(envelope) is dict and set(envelope) == HEADERS)
    require(type(envelope["v"]) is int and envelope["v"] == 1)
    require(type(envelope["kind"]) is str and envelope["kind"] in (REQUEST, RESULT))
    canonical_uuid(envelope["device_id"])
    require(type(envelope["key_id"]) is str and re.fullmatch(
        r"[A-Za-z0-9_-]{1,64}", envelope["key_id"]) is not None)
    decode64(envelope["payload_b64"])
    return b"RW-ENROLL-v1\0" + "\0".join(
        envelope[name] for name in ("kind", "device_id", "key_id", "payload_b64")
    ).encode("ascii")


def verify(raw, kind, keys):
    """Byte/schema/MAC checks only; caller must check current approval and UTC."""
    require(kind in (REQUEST, RESULT))
    envelope = parse_json(raw)
    message = mac_input(envelope)
    require(envelope["kind"] == kind and envelope["device_id"] == keys.device_id
            and envelope["key_id"] == keys.key_id)
    received = decode64(envelope["mac_b64"], 32)
    key = keys.request_key if kind == REQUEST else keys.result_key
    require(len(received) == 32 and hmac.compare_digest(received, hmac.digest(key, message, "sha256")))
    payload = parse_json(decode64(envelope["payload_b64"]), MAX_PAYLOAD)
    validate_payload(payload, kind)
    require(payload["key_epoch"] == keys.key_epoch)
    return payload


def ticket_digest(raw):
    """SHA256(request MAC input); not a replacement for verify()."""
    envelope = parse_json(raw)
    message = mac_input(envelope)
    require(envelope["kind"] == REQUEST)
    return hashlib.sha256(message).hexdigest()


def sign_result(payload, keys):
    """Pi-side result signing only; never distribute direction keys to a PC."""
    validate_payload(payload, RESULT)
    require(payload["key_epoch"] == keys.key_epoch)
    raw = json.dumps(payload, separators=(",", ":"), allow_nan=False).encode("ascii")
    require(len(raw) <= MAX_PAYLOAD)
    envelope = dict(v=1, kind=RESULT, device_id=keys.device_id, key_id=keys.key_id,
                    payload_b64=encode64(raw), mac_b64="")
    envelope["mac_b64"] = encode64(hmac.digest(keys.result_key, mac_input(envelope), "sha256"))
    return json.dumps(envelope, separators=(",", ":")).encode("ascii")


def verify_result_binding(request, digest, result):
    require(all(request[name] == result[name] for name in COMMON))
    require(hmac.compare_digest(result["ticket_digest"], digest))
