"""Offline enroll-v1 proposal for server/Pi review, NOT an enrollment service.

No routes/DB/keys/clock/permissions/replay ledger are installed. Valid bytes do
not authorize a sensor write or activate a mapping. Never put keys in PC relays.
"""
import hashlib
import hmac
import json
import re
from datetime import datetime

from .device_protocol_draft import (
    MAX_PAYLOAD_BYTES, BIGINT_MAX, FIELDS, ProtocolError,
    b64decode, b64encode, canonical_uuid, parse_json,
)

REQUEST = "enroll_request"
RESULT = "enroll_result"
DOMAIN = b"RW-ENROLL-v1\0"
COMMON = frozenset(("enrollment_id", "operation_revision", "reserved_slot", "claim_revision", "key_epoch"))
REQUEST_FIELDS = COMMON | {"target_user_id", "nonce", "purpose", "issued_at", "expires_at", "session_ref"}
RESULT_FIELDS = COMMON | {"ticket_digest", "result_id", "outcome", "failure_code", "elapsed_ms"}
FAILURES = frozenset(("busy", "cancelled", "timeout", "sensor_full", "slot_conflict",
                      "duplicate_finger", "different_finger", "capacity_mismatch",
                      "sensor_error", "storage_uncertain", "ledger_error", "authorization_lost"))


def _fail():
    raise ProtocolError("Invalid enrollment message")


def _revision(value):
    if (type(value) is not str or re.fullmatch(r"[1-9][0-9]{0,18}", value) is None
            or int(value) > BIGINT_MAX):
        _fail()


def _integer(value, minimum):
    if type(value) is not int or not minimum <= value <= 2147483647:
        _fail()


def _time(value):
    if type(value) is not str or re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", value) is None:
        _fail()
    try:
        return datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        _fail()


def validate_payload(payload, kind):
    expected = REQUEST_FIELDS if kind == REQUEST else RESULT_FIELDS if kind == RESULT else None
    if expected is None or type(payload) is not dict or set(payload) != expected:
        _fail()
    canonical_uuid(payload["enrollment_id"])
    # Parser bound only; not AS608 capacity/slot-zero authorization.
    _integer(payload["reserved_slot"], 0)
    for field in ("operation_revision", "claim_revision", "key_epoch"):
        _revision(payload[field])
    if kind == REQUEST:
        _integer(payload["target_user_id"], 1)
        if not 16 <= len(b64decode(payload["nonce"], 64)) <= 64:
            _fail()
        if (payload["purpose"] != "fingerprint_enrollment"
                or type(payload["session_ref"]) is not str
                or re.fullmatch(r"[A-Za-z0-9_-]{1,128}", payload["session_ref"]) is None
                or _time(payload["expires_at"]) <= _time(payload["issued_at"])):
            _fail()
    else:
        canonical_uuid(payload["result_id"])
        if (type(payload["ticket_digest"]) is not str
                or re.fullmatch(r"[0-9a-f]{64}", payload["ticket_digest"]) is None):
            _fail()
        _integer(payload["elapsed_ms"], 0)
        outcome, failure = payload["outcome"], payload["failure_code"]
        if outcome == "sensor_stored":
            if failure is not None:
                _fail()
        elif outcome in ("failed", "recovery_required"):
            if type(failure) is not str or failure not in FAILURES:
                _fail()
            if outcome == "failed" and failure in ("storage_uncertain", "ledger_error"):
                _fail()
        else:
            _fail()


def mac_input(envelope):
    if (type(envelope) is not dict or set(envelope) != FIELDS
            or type(envelope["v"]) is not int or envelope["v"] != 1
            or envelope["kind"] not in (REQUEST, RESULT)):
        _fail()
    canonical_uuid(envelope["device_id"])
    if (type(envelope["key_id"]) is not str
            or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", envelope["key_id"]) is None):
        _fail()
    b64decode(envelope["payload_b64"])
    return DOMAIN + (envelope["kind"] + "\0" + envelope["device_id"] + "\0"
                     + envelope["key_id"] + "\0" + envelope["payload_b64"]).encode("ascii")


def _key(key):
    if type(key) is not bytes or len(key) < 32:
        raise ValueError("An active device/direction key of at least 32 bytes is required")


def sign_payload(raw, *, kind, device_id, key_id, key):
    """Development server/Pi only. Caller supplies its selected direction key."""
    _key(key)
    validate_payload(parse_json(raw, MAX_PAYLOAD_BYTES), kind)
    envelope = dict(v=1, kind=kind, device_id=device_id, key_id=key_id,
                    payload_b64=b64encode(raw), mac_b64="")
    envelope["mac_b64"] = b64encode(hmac.digest(key, mac_input(envelope), "sha256"))
    return json.dumps(envelope, separators=(",", ":")).encode("ascii")


def verify_payload(raw, *, expected_kind, device_id, key_id, key_epoch, key):
    """Authenticate exact bytes/shape/epoch only; no freshness or current rights."""
    _key(key)
    _revision(key_epoch)
    if expected_kind not in (REQUEST, RESULT):
        raise ValueError("Expected enroll_request or enroll_result")
    envelope = parse_json(raw)
    message = mac_input(envelope)
    if (envelope["kind"] != expected_kind or envelope["device_id"] != device_id
            or envelope["key_id"] != key_id):
        _fail()
    received = b64decode(envelope["mac_b64"], 32)
    if len(received) != 32 or not hmac.compare_digest(received, hmac.digest(key, message, "sha256")):
        _fail()
    payload = parse_json(b64decode(envelope["payload_b64"]), MAX_PAYLOAD_BYTES)
    validate_payload(payload, expected_kind)
    if payload["key_epoch"] != key_epoch:
        _fail()
    return payload


def ticket_digest(raw):
    """SHA256 of request MAC input, not JSON envelope bytes or authenticity proof."""
    envelope = parse_json(raw)
    message = mac_input(envelope)
    if envelope["kind"] != REQUEST:
        _fail()
    return hashlib.sha256(message).hexdigest()


def verify_completion(ticket, result, *, device_id, key_id, key_epoch, request_key, result_key):
    """Offline binding check. Does NOT consume a job or commit an active mapping."""
    expected = dict(device_id=device_id, key_id=key_id, key_epoch=key_epoch)
    request = verify_payload(ticket, expected_kind=REQUEST, key=request_key, **expected)
    response = verify_payload(result, expected_kind=RESULT, key=result_key, **expected)
    if (any(request[field] != response[field] for field in COMMON)
            or response["ticket_digest"] != ticket_digest(ticket)):
        _fail()
    return response
