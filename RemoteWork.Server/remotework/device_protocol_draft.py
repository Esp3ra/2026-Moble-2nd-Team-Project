"""C10 v1 byte-contract review helper; NOT an authentication service.

No routes, DB tables, keys, expiry decisions or permissions are installed here.
Only server/Pi implementations may hold keys. A relay PC passes bytes unchanged.
"""
import base64
from datetime import datetime
import hashlib
import hmac
import json
import re
import uuid


MAX_ENVELOPE_BYTES = 16 * 1024
MAX_PAYLOAD_BYTES = 8 * 1024
BIGINT_MAX = 9223372036854775807
FIELDS = frozenset(("v", "kind", "device_id", "key_id", "payload_b64", "mac_b64"))


class ProtocolError(ValueError):
    """Malformed draft message; do not log the input, key or ticket."""


def _fail():
    raise ProtocolError("Invalid device message")


def _object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            _fail()
        result[key] = value
    return result


def parse_json(raw, maximum=MAX_ENVELOPE_BYTES):
    if not isinstance(raw, bytes) or not 1 <= len(raw) <= maximum:
        _fail()
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=_object,
                           parse_constant=lambda _: _fail())
        # Surrogate escapes are legal to the stdlib parser but not to this UTF-8 contract.
        json.dumps(value, ensure_ascii=False, allow_nan=False).encode("utf-8")
    except (ValueError, UnicodeError, RecursionError, OverflowError):
        _fail()
    if type(value) is not dict:
        _fail()
    return value


def b64encode(value):
    return base64.urlsafe_b64encode(value).rstrip(b"=").decode("ascii")


def b64decode(value, maximum=MAX_PAYLOAD_BYTES):
    if (type(value) is not str or not value or len(value) > (maximum * 4 + 2) // 3
            or re.fullmatch(r"[A-Za-z0-9_-]+", value) is None):
        _fail()
    try:
        raw = base64.b64decode(value + "=" * (-len(value) % 4), altchars=b"-_", validate=True)
    except ValueError:
        _fail()
    if len(raw) > maximum or b64encode(raw) != value:
        _fail()
    return raw


def canonical_uuid(value):
    try:
        if type(value) is not str or str(uuid.UUID(value)) != value:
            _fail()
    except (ValueError, AttributeError):
        _fail()
    return value


def _header(envelope):
    if (type(envelope) is not dict or set(envelope) != FIELDS
            or type(envelope["v"]) is not int or envelope["v"] != 1
            or envelope["kind"] not in ("request", "result")):
        _fail()
    canonical_uuid(envelope["device_id"])
    if (type(envelope["key_id"]) is not str
            or re.fullmatch(r"[A-Za-z0-9_-]{1,64}", envelope["key_id"]) is None):
        _fail()
    # Validate before ASCII concatenation, including maximum payload size.
    b64decode(envelope["payload_b64"])


def mac_input(envelope):
    _header(envelope)
    return ("RW-ID-v1\0" + envelope["kind"] + "\0" + envelope["device_id"]
            + "\0" + envelope["key_id"] + "\0" + envelope["payload_b64"]).encode("ascii")


def _key(key):
    if type(key) is not bytes or len(key) < 32:
        raise ValueError("A device-specific key of at least 32 bytes is required")


def sign_payload(raw, *, kind, device_id, key_id, key):
    """Server/Pi development helper. Do not use in the relay PC."""
    _key(key)
    validate_payload(parse_json(raw, MAX_PAYLOAD_BYTES), kind)
    envelope = dict(v=1, kind=kind, device_id=device_id, key_id=key_id,
                    payload_b64=b64encode(raw), mac_b64="")
    envelope["mac_b64"] = b64encode(hmac.digest(key, mac_input(envelope), "sha256"))
    return json.dumps(envelope, ensure_ascii=True, separators=(",", ":")).encode("ascii")


def verify_payload(raw, *, expected_kind, device_id, key_id, key):
    """Authenticate exact bytes, then return parsed draft fields.

    Caller MUST select an active trusted key and check current user/session,
    mapping, nonce, expiry and atomic consumption. This function does none of that.
    """
    _key(key)
    if expected_kind not in ("request", "result"):
        raise ValueError("Expected request or result")
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
    return payload


def ticket_digest(raw):
    """Digest of the draft MAC input; NOT proof that the ticket is authentic."""
    return hashlib.sha256(mac_input(parse_json(raw))).hexdigest()


def _time(value):
    if type(value) is not str or re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z", value) is None:
        _fail()
    try:
        return datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        _fail()


def validate_payload(value, kind):
    """Draft DTO checks only; no expiry or hardware slot-capacity assertion."""
    shared = {"challenge_id", "nonce", "mapping_revision"}
    extra = ({"purpose", "issued_at", "expires_at", "session_ref"} if kind == "request"
             else {"ticket_digest", "result_id", "outcome", "slot"} if kind == "result" else None)
    if extra is None or type(value) is not dict or set(value) != shared | extra:
        _fail()
    canonical_uuid(value["challenge_id"])
    if not 16 <= len(b64decode(value["nonce"], 64)) <= 64:
        _fail()
    revision = value["mapping_revision"]
    if (type(revision) is not str or re.fullmatch(r"[1-9][0-9]{0,18}", revision) is None
            or int(revision) > BIGINT_MAX):
        _fail()
    if kind == "request":
        if (value["purpose"] != "identity_check" or type(value["session_ref"]) is not str
                or re.fullmatch(r"[A-Za-z0-9_-]{1,128}", value["session_ref"]) is None
                or _time(value["expires_at"]) <= _time(value["issued_at"])):
            _fail()
    else:
        canonical_uuid(value["result_id"])
        if type(value["ticket_digest"]) is not str or re.fullmatch(r"[0-9a-f]{64}", value["ticket_digest"]) is None:
            _fail()
        if value["outcome"] not in ("matched", "no_match", "sensor_error"):
            _fail()
        slot = value["slot"]
        if value["outcome"] == "matched":
            if type(slot) is not int or not 0 <= slot <= 2147483647:
                _fail()
        elif slot is not None:
            _fail()
