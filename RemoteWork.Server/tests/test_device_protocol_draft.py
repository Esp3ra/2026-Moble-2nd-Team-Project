"""Public synthetic contract fixtures; no hardware, database or authentication."""
import hmac
import json
from pathlib import Path

import pytest

from remotework import device_protocol_draft as wire

FIXTURE = json.loads((Path(__file__).resolve().parents[2] / "Tests/Fixtures/device_protocol_draft.json").read_text(encoding="utf-8"))
KEY = bytes.fromhex(FIXTURE["public_test_key_hex"])


def envelope(vector):
    return {"v": 1, **{name: vector[name] for name in ("kind", "device_id", "key_id", "payload_b64", "mac_b64")}}


def verify(e, vector=None, key=KEY):
    v = vector or FIXTURE["vectors"][0]
    return wire.verify_payload(json.dumps(e).encode(), expected_kind=v["kind"],
                               device_id=v["device_id"], key_id=v["key_id"], key=key)


@pytest.mark.parametrize("vector", FIXTURE["vectors"])
def test_public_vectors_and_exact_bytes(vector):
    e = envelope(vector)
    assert wire.mac_input(e).hex() == vector["mac_input_hex"]
    assert wire.b64encode(vector["payload_json"].encode()) == vector["payload_b64"]
    assert verify(e, vector) == json.loads(vector["payload_json"])
    signed = wire.sign_payload(vector["payload_json"].encode(), kind=vector["kind"],
                               device_id=vector["device_id"], key_id=vector["key_id"], key=KEY)
    assert json.loads(signed) == e


def test_request_digest_is_bound_in_result():
    e = envelope(FIXTURE["vectors"][0])
    assert wire.ticket_digest(json.dumps(e).encode()) == json.loads(FIXTURE["vectors"][1]["payload_json"])["ticket_digest"]


@pytest.mark.parametrize("field,value", [
    ("v", True), ("v", 1.0), ("v", 2), ("kind", "result"),
    ("device_id", "123E4567-e89b-42d3-a456-426614174000"),
    ("device_id", "123e4567-e89b-42d3-a456-426614174001"),
    ("key_id", "another-key"), ("key_id", "x\0y"), ("key_id", "한글"),
    ("mac_b64", "A" * 43), ("mac_b64", "A" * 42),
    ("payload_b64", "e30"), ("payload_b64", "e30="), ("payload_b64", "e30\n"),
])
def test_header_or_mac_tampering_is_rejected(field, value):
    e = envelope(FIXTURE["vectors"][0]); e[field] = value
    with pytest.raises(wire.ProtocolError):
        verify(e)


@pytest.mark.parametrize("value", ["A", "AB", "AAAA=", "+AAA", "AAAA ", "", "a" * 12000])
def test_noncanonical_or_oversized_base64_rejected(value):
    with pytest.raises(wire.ProtocolError):
        wire.b64decode(value)


@pytest.mark.parametrize("raw", [
    b'{"a":1,"a":2}', b'{"a":{"b":1,"b":2}}', b'{"a":NaN}',
    b'{"a":1e999}', b'{"a":"\\ud800"}', b'\xff', b'[]', b'null', b'{}' * 10000,
])
def test_strict_json(raw):
    with pytest.raises(wire.ProtocolError):
        wire.parse_json(raw)


@pytest.mark.parametrize("field,value", [
    ("mapping_revision", True), ("mapping_revision", 7), ("mapping_revision", "01"),
    ("mapping_revision", "9223372036854775808"), ("nonce", "e30"),
    ("expires_at", "2026-10-19T05:00:00Z"), ("issued_at", "2026-02-30T06:00:00Z"),
    ("session_ref", ""), ("purpose", "clock_in"),
])
def test_authenticated_but_invalid_draft_payload_rejected(field, value):
    v = FIXTURE["vectors"][0]
    p = json.loads(v["payload_json"]); p[field] = value
    e = envelope(v); e["payload_b64"] = wire.b64encode(json.dumps(p).encode())
    e["mac_b64"] = wire.b64encode(hmac.digest(KEY, wire.mac_input(e), "sha256"))
    with pytest.raises(wire.ProtocolError):
        verify(e)


@pytest.mark.parametrize("outcome,slot,valid", [
    ("matched", True, False), ("matched", -1, False), ("matched", None, False),
    ("matched", 0, True), ("matched", 2147483648, False),
    ("no_match", None, True), ("sensor_error", None, True),
    ("no_match", 7, False), ("unknown", None, False),
])
def test_draft_outcome_and_slot_shape(outcome, slot, valid):
    p = json.loads(FIXTURE["vectors"][1]["payload_json"])
    p.update(outcome=outcome, slot=slot)
    if valid:
        wire.validate_payload(p, "result")
    else:
        with pytest.raises(wire.ProtocolError):
            wire.validate_payload(p, "result")


def test_wrong_key_extra_field_and_duplicate_outer_keys():
    e = envelope(FIXTURE["vectors"][0])
    with pytest.raises(wire.ProtocolError): verify(e, key=b"z" * 32)
    changed = e | {"user_id": 5}
    with pytest.raises(wire.ProtocolError): verify(changed)
    raw = json.dumps(e)[:-1] + ',"v":1}'
    with pytest.raises(wire.ProtocolError):
        wire.verify_payload(raw.encode(), expected_kind="request", device_id=e["device_id"], key_id=e["key_id"], key=KEY)


def test_plain_reformatting_cannot_reuse_mac_and_past_time_is_not_freshness_proof():
    v = FIXTURE["vectors"][0]; e = envelope(v)
    e["payload_b64"] = wire.b64encode(json.dumps(json.loads(v["payload_json"]), indent=2).encode())
    with pytest.raises(wire.ProtocolError): verify(e)
    # A valid historical fixture is intentionally still valid bytes. The SERVER,
    # not this offline helper, must reject expiry/replay and current mapping changes.
    assert verify(envelope(v))["expires_at"] == "2026-10-19T06:01:00Z"
