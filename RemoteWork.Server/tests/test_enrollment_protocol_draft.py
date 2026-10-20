"""Offline contract tests; no DB, sensor, authorization, expiry or replay store."""
import hashlib
import hmac
import json
from pathlib import Path

import pytest

from remotework import device_protocol_draft as identity
from remotework import enrollment_protocol_draft as wire

FIXTURE = json.loads((Path(__file__).resolve().parents[2] / "Tests/Fixtures/enrollment_protocol_draft.json").read_text(encoding="utf-8"))
KEYS = {wire.REQUEST: bytes.fromhex(FIXTURE["public_test_request_key_hex"]), wire.RESULT: bytes.fromhex(FIXTURE["public_test_result_key_hex"])}


def envelope(index=0):
    return {field: FIXTURE["vectors"][index][field] for field in wire.FIELDS}


def raw(index=0):
    return json.dumps(envelope(index)).encode()


def verify(value, index=0, **overrides):
    e = envelope(index)
    args = dict(expected_kind=e["kind"], device_id=e["device_id"], key_id=e["key_id"], key_epoch="1", key=KEYS[e["kind"]]) | overrides
    return wire.verify_payload(json.dumps(value).encode(), **args)


def signed_change(index, field, value):
    e = envelope(index); payload = json.loads(FIXTURE["vectors"][index]["payload_json"])
    payload[field] = value
    e["payload_b64"] = wire.b64encode(json.dumps(payload).encode())
    e["mac_b64"] = wire.b64encode(hmac.digest(KEYS[e["kind"]], wire.mac_input(e), "sha256"))
    return e


@pytest.mark.parametrize("index", [0, 1])
def test_public_bytes_and_roundtrip(index):
    e = envelope(index); v = FIXTURE["vectors"][index]
    assert wire.mac_input(e).hex() == v["mac_input_hex"]
    assert wire.b64encode(v["payload_json"].encode()) == e["payload_b64"]
    assert verify(e, index) == json.loads(v["payload_json"])
    assert json.loads(wire.sign_payload(v["payload_json"].encode(), kind=e["kind"], device_id=e["device_id"], key_id=e["key_id"], key=KEYS[e["kind"]])) == e


def test_complete_is_bound_but_not_freshness_or_consumption():
    args = dict(device_id=envelope()["device_id"], key_id=envelope()["key_id"], key_epoch="1", request_key=KEYS[wire.REQUEST], result_key=KEYS[wire.RESULT])
    first = wire.verify_completion(raw(), raw(1), **args)
    assert first["outcome"] == "sensor_stored"
    # Historical bytes/repeated calls intentionally pass: only the real service
    # can validate time/current rights, consume a job and commit a mapping.
    assert wire.verify_completion(raw(), raw(1), **args) == first
    assert wire.ticket_digest(raw()) == hashlib.sha256(wire.mac_input(envelope())).hexdigest()


@pytest.mark.parametrize("field,value", [
    ("v", True), ("v", 1.0), ("v", 2), ("kind", "request"), ("kind", "result"),
    ("kind", wire.RESULT), ("device_id", "123E4567-e89b-42d3-a456-426614174000"),
    ("device_id", "123e4567-e89b-42d3-a456-426614174001"), ("key_id", "other"), ("key_id", "a\0b"),
    ("payload_b64", "e30="), ("payload_b64", "AB"), ("mac_b64", "A" * 43), ("mac_b64", "A" * 42),
])
def test_header_tampering(field, value):
    e = envelope(); e[field] = value
    with pytest.raises(wire.ProtocolError): verify(e)


@pytest.mark.parametrize("index,field,value", [
    (0, "target_user_id", True), (0, "target_user_id", 0), (0, "target_user_id", "88"), (0, "target_user_id", 2147483648),
    (0, "reserved_slot", True), (0, "reserved_slot", -1), (0, "reserved_slot", 2147483648),
    (0, "operation_revision", 1), (0, "operation_revision", "01"), (0, "claim_revision", "0"),
    (0, "key_epoch", "9223372036854775808"), (0, "key_epoch", "2"), (0, "nonce", "e30"),
    (0, "purpose", "identity_check"), (0, "session_ref", ""), (0, "session_ref", "x\0y"),
    (0, "issued_at", "2026-02-30T01:00:00Z"), (0, "expires_at", "2026-10-20T01:00:00Z"),
    (0, "expires_at", "2026-10-20T01:03:00+00:00"), (0, "unexpected", 1),
    (1, "elapsed_ms", True), (1, "elapsed_ms", -1), (1, "elapsed_ms", 2147483648),
    (1, "outcome", "completed"), (1, "failure_code", "busy"), (1, "ticket_digest", "A" * 64),
    (1, "result_id", "bad"),
])
def test_valid_mac_with_bad_payload_rejected(index, field, value):
    with pytest.raises(wire.ProtocolError): verify(signed_change(index, field, value), index)


@pytest.mark.parametrize("field,value", [
    ("enrollment_id", "31593412-67fd-40d2-878b-868d15f99a12"),
    ("operation_revision", "2"), ("reserved_slot", 8), ("claim_revision", "2"),
    ("ticket_digest", "a" * 64),
])
def test_trusted_but_different_binding_rejected(field, value):
    changed = signed_change(1, field, value)
    assert verify(changed, 1)  # Shape/MAC alone is not sufficient.
    with pytest.raises(wire.ProtocolError):
        wire.verify_completion(raw(), json.dumps(changed).encode(), device_id=envelope()["device_id"], key_id=envelope()["key_id"], key_epoch="1", request_key=KEYS[wire.REQUEST], result_key=KEYS[wire.RESULT])


@pytest.mark.parametrize("outcome,failure,valid", [
    ("sensor_stored", None, True), ("failed", "different_finger", True),
    ("failed", "duplicate_finger", True), ("recovery_required", "storage_uncertain", True),
    ("recovery_required", "ledger_error", True), ("failed", "storage_uncertain", False),
    ("failed", "ledger_error", False), ("failed", None, False), ("recovery_required", "unknown", False),
])
def test_outcome_is_sensor_evidence_not_server_commit(outcome, failure, valid):
    p = json.loads(FIXTURE["vectors"][1]["payload_json"]);p.update(outcome=outcome, failure_code=failure)
    if valid: wire.validate_payload(p, wire.RESULT)
    else:
        with pytest.raises(wire.ProtocolError): wire.validate_payload(p, wire.RESULT)


@pytest.mark.parametrize("bad", [b'{"v":1,"v":1}', b'{"a":{"b":1,"b":2}}', b'{"n":NaN}', b'{"n":1e999}', b'{"a":"\\ud800"}', b'\xff', b'[]', b'null', b'{}' * 10000])
def test_strict_json(bad):
    with pytest.raises(wire.ProtocolError):
        wire.verify_payload(bad, expected_kind=wire.REQUEST, device_id=envelope()["device_id"], key_id=envelope()["key_id"], key_epoch="1", key=KEYS[wire.REQUEST])


def test_no_cross_protocol_or_direction_key_reuse():
    e = envelope()
    # A MAC from the identity domain cannot be accepted in enroll-v1.
    data = wire.mac_input(e).replace(wire.DOMAIN, b"RW-ID-v1\0", 1)
    e["mac_b64"] = wire.b64encode(hmac.digest(KEYS[wire.REQUEST], data, "sha256"))
    with pytest.raises(wire.ProtocolError): verify(e)
    with pytest.raises(wire.ProtocolError): verify(envelope(), key=KEYS[wire.RESULT])
    with pytest.raises(wire.ProtocolError): identity.mac_input(envelope())
    with pytest.raises(wire.ProtocolError): wire.ticket_digest(raw(1))


@pytest.mark.parametrize("field,value", [("target_user_id",89),("nonce","EBESExQVFhcYGRobHB0eHw")])
def test_result_cannot_add_raw_user_or_nonce(field,value):
    with pytest.raises(wire.ProtocolError): verify(signed_change(1,field,value),1)


@pytest.mark.parametrize("field,value", [("target_user_id",89),("nonce","EBESExQVFhcYGRobHB0eHw")])
def test_ticket_digest_binds_target_and_nonce_without_persisting_them_in_result(field,value):
    changed=signed_change(0,field,value)
    assert verify(changed)
    with pytest.raises(wire.ProtocolError):
        wire.verify_completion(json.dumps(changed).encode(),raw(1),device_id=envelope()["device_id"],key_id=envelope()["key_id"],key_epoch="1",request_key=KEYS[wire.REQUEST],result_key=KEYS[wire.RESULT])


def test_unknown_missing_duplicate_keys_and_reformatting():
    e = envelope()
    with pytest.raises(wire.ProtocolError): verify(e | {"actor_user_id": 1})
    missing = e.copy();del missing["mac_b64"]
    with pytest.raises(wire.ProtocolError): verify(missing)
    duplicate = json.dumps(e)[:-1] + ',"v":1}'
    with pytest.raises(wire.ProtocolError):
        wire.verify_payload(duplicate.encode(), expected_kind=wire.REQUEST, device_id=e["device_id"], key_id=e["key_id"], key_epoch="1", key=KEYS[wire.REQUEST])
    e["payload_b64"] = wire.b64encode(json.dumps(json.loads(FIXTURE["vectors"][0]["payload_json"]), indent=2).encode())
    with pytest.raises(wire.ProtocolError): verify(e)


@pytest.mark.parametrize("slot", [0, 299, 300, 2147483647])
def test_parser_bounds_are_not_hardware_authorization(slot):
    p = json.loads(FIXTURE["vectors"][0]["payload_json"]);p["reserved_slot"] = slot
    wire.validate_payload(p, wire.REQUEST)  # Real device profile/claim check is mandatory outside this helper.
