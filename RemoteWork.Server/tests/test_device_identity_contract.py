"""Synthetic protocol tests, NOT proof of a running authentication service."""
import hashlib
import hmac
import importlib.util
import json
from pathlib import Path
from unittest.mock import Mock

import pytest
from remotework import device_identity_contract as wire

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = json.loads((ROOT / "Tests/Fixtures/device_identity_v2.json").read_text())
API = json.loads((ROOT / "Tests/Fixtures/device_identity_v2_api.json").read_text())
VECTORS = {v["name"]: v for v in FIXTURE["vectors"]}
DEVICE, KEY_ID = FIXTURE["device_id"], FIXTURE["key_id"]
KEYS = {k: bytes.fromhex(v) for k, v in FIXTURE["public_test_keys_hex"].items()}


def payload(name): return json.loads(VECTORS[name]["payload_json"])
def raw(name): return VECTORS[name]["envelope_json"].encode()
def encoded(value): return json.dumps(value, separators=(",", ":")).encode()
def sign(kind, p): return wire.sign(encoded(p), kind=kind, device_id=DEVICE, key_id=KEY_ID, key=KEYS[kind])
def verify(kind, data, key=None):
    return wire.verify(data, kind=kind, device_id=DEVICE, key_id=KEY_ID, key=key or KEYS[kind])


@pytest.mark.parametrize("v", FIXTURE["vectors"], ids=lambda v: v["name"])
def test_exact_public_vectors_independent_hmac_and_raw_digest(v):
    e = json.loads(v["envelope_json"])
    expected_input = (b"RW-ID-v2\0" + v["kind"].encode() + b"\0" + DEVICE.encode()
                      + b"\0" + KEY_ID.encode() + b"\0" + e["payload_b64"].encode())
    assert expected_input.hex() == v["mac_input_hex"]
    assert wire.b64encode(hmac.digest(KEYS[v["kind"]], expected_input, "sha256")) == e["mac_b64"]
    assert hashlib.sha256(v["envelope_json"].encode()).hexdigest() == v["digest"]
    assert sign(v["kind"], json.loads(v["payload_json"])) == v["envelope_json"].encode()
    assert verify(v["kind"], v["envelope_json"].encode()) == json.loads(v["payload_json"])


def test_generation_reproduces_committed_fixtures():
    spec = importlib.util.spec_from_file_location("public_vectors", ROOT / "Tests/DeviceIdentity.Contract.Tests/generate_vectors.py")
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    assert module.build() == (FIXTURE, API)


@pytest.mark.parametrize("kind", sorted(wire.KINDS))
@pytest.mark.parametrize("wrong_kind", sorted(wire.KINDS))
def test_directional_keys_cannot_substitute(kind, wrong_kind):
    v = next(v for v in FIXTURE["vectors"] if v["kind"] == kind)
    if kind == wrong_kind:
        assert verify(kind, v["envelope_json"].encode())
    else:
        with pytest.raises(wire.ProtocolError):
            verify(kind, v["envelope_json"].encode(), KEYS[wrong_kind])


@pytest.mark.parametrize("field,value", [
    ("v", 1), ("v", True), ("v", 2.0), ("kind", "enroll_request"), ("kind", []),
    ("device_id", DEVICE.upper()), ("device_id", "10000000-0000-4000-8000-000000000002"),
    ("key_id", "another-key"), ("payload_b64", "e30"), ("mac_b64", "A" * 43),
    ("mac_b64", "AA"), ("payload_b64", "e30="), ("mac_b64", ""),
])
def test_outer_tampering(field, value):
    e = json.loads(raw("clock-in-request")); e[field] = value
    # Numeric-only UUID .upper() is still identical: explicitly use noncanonical braces.
    if field == "device_id" and value == DEVICE: e[field] = "{" + DEVICE + "}"
    with pytest.raises(wire.ProtocolError): verify("id_request", encoded(e))


@pytest.mark.parametrize("bad", [b'{"a":1,"a":2}', b'{"a":{"x":1,"x":2}}', b'{"v":NaN}',
    b'{"v":1e999}', b'{"v":"\\ud800"}', b'[]', b'null', b'\xff', b'{}' * 9000])
def test_strict_json(bad):
    with pytest.raises(wire.ProtocolError): wire.parse_json(bad)


@pytest.mark.parametrize("name", ("clock-in-request", "capture-probe", "result-approval", "matched-result"))
def test_unknown_duplicate_fields_and_domain_separation(name):
    v = VECTORS[name]; e = json.loads(raw(name))
    with pytest.raises(wire.ProtocolError): verify(v["kind"], encoded(e | {"user_id": 1}))
    with pytest.raises(wire.ProtocolError): verify(v["kind"], raw(name)[:-1] + b',"v":2}')
    changed = payload(name) | {"user_id": 1}
    with pytest.raises(wire.ProtocolError): sign(v["kind"], changed)
    old_input = bytes.fromhex(v["mac_input_hex"]).replace(b"RW-ID-v2\0", b"RW-ID-v1\0")
    e["mac_b64"] = wire.b64encode(hmac.digest(KEYS[v["kind"]], old_input, "sha256"))
    with pytest.raises(wire.ProtocolError): verify(v["kind"], encoded(e))


@pytest.mark.parametrize("field,value", [
    ("expected_slot", True), ("expected_slot", 1.0), ("expected_slot", "17"), ("expected_slot", -1),
    ("expected_slot", 2147483648), ("mapping_revision", "01"), ("mapping_revision", 1),
    ("mapping_revision", "9223372036854775808"), ("key_epoch", True), ("key_epoch", "0"),
    ("purpose", "identity_check"), ("purpose", []), ("session_ref", "e30"),
    ("nonce", "AA"), ("nonce", "A" * 100), ("work_date", "2026-02-30"), ("work_date", "2026-1-01"),
    ("expires_at", "2026-10-21T08:01:01Z"), ("expires_at", "2026-10-21T08:00:00Z"),
    ("issued_at", "2026-10-21T08:00:00+00:00"), ("work_session_ref", "50000000-0000-4000-8000-000000000001"),
])
def test_signed_but_invalid_request_fields(field, value):
    with pytest.raises(wire.ProtocolError): sign("id_request", payload("clock-in-request") | {field: value})


@pytest.mark.parametrize("purpose", ("noon_check", "clock_out"))
def test_work_context_must_come_from_actual_work_session(purpose):
    p = payload(purpose + "-request")
    assert verify("id_request", sign("id_request", p)) == p
    with pytest.raises(wire.ProtocolError): sign("id_request", p | {"work_session_ref": None})


@pytest.mark.parametrize("outcome", tuple(wire.FAILURES))
def test_exact_result_outcome_fields(outcome):
    p = payload(outcome + "-result")
    assert verify("id_result", sign("id_result", p)) == p
    with pytest.raises(wire.ProtocolError): sign("id_result", p | {"failure_code": "anything"})
    if outcome == "matched":
        with pytest.raises(wire.ProtocolError): sign("id_result", p | {"slot": True})
        with pytest.raises(wire.ProtocolError): sign("id_result", p | {"approval_digest": None})
    else:
        with pytest.raises(wire.ProtocolError): sign("id_result", p | {"slot": 17})


def bind(p, ticket=None, approval=None):
    wire.bind_result(p, payload("clock-in-request") if ticket is None else ticket,
                     ticket_bytes=raw("clock-in-request"),
                     result_approval_bytes=raw("result-approval") if approval is None else approval)


def test_matched_is_only_expected_slot_and_binds_full_original_request():
    p = payload("matched-result"); bind(p)
    for field, value in (("slot", 18), ("key_epoch", "2"), ("mapping_revision", "8"),
                         ("challenge_id", "20000000-0000-4000-8000-000000000002"),
                         ("nonce", wire.b64encode(b"X" * 32)), ("ticket_digest", "0" * 64)):
        with pytest.raises(wire.ProtocolError): bind(p | {field: value})
    for field, value in (("purpose", "clock_out"), ("session_ref", wire.b64encode(b"Z" * 32)),
                         ("work_date", "2026-10-22"), ("expected_slot", 18)):
        with pytest.raises(wire.ProtocolError): bind(p, payload("clock-in-request") | {field: value})


def test_result_scope_cannot_be_replaced_with_capture_or_other_ticket_approval():
    p = payload("matched-result")
    changed = p | {"approval_digest": wire.message_digest(raw("capture-approval"))}
    with pytest.raises(wire.ProtocolError): bind(changed, approval=raw("capture-approval"))
    a = payload("result-approval") | {"mapping_revision": "8"}
    signed = sign("id_approval", a)
    with pytest.raises(wire.ProtocolError): bind(p | {"approval_digest": wire.message_digest(signed)}, approval=signed)
    a = payload("result-approval") | {"server_time": "2026-10-21T07:59:59Z", "valid_until": "2026-10-21T08:00:19Z"}
    signed = sign("id_approval", a)
    with pytest.raises(wire.ProtocolError): bind(p | {"approval_digest": wire.message_digest(signed)}, approval=signed)
    a = payload("result-approval") | {"server_time": "2026-10-21T08:00:50Z", "valid_until": "2026-10-21T08:01:10Z"}
    signed = sign("id_approval", a)
    with pytest.raises(wire.ProtocolError): bind(p | {"approval_digest": wire.message_digest(signed)}, approval=signed)


def test_exact_envelope_digest_prevents_reformatted_result_reuse():
    ticket = json.loads(raw("clock-in-request"))
    # Outer formatting can retain its payload MAC, but WHOLE original envelope binding differs.
    reformatted = json.dumps(ticket, indent=2).encode()
    assert verify("id_request", reformatted) == payload("clock-in-request")
    assert wire.message_digest(reformatted) != wire.message_digest(raw("clock-in-request"))
    with pytest.raises(wire.ProtocolError):
        wire.bind_result(payload("matched-result"), payload("clock-in-request"), ticket_bytes=reformatted,
                         result_approval_bytes=raw("result-approval"))


class Clock:
    value = 10.0
    def __call__(self): return self.value


def probe(clock, scope="capture"):
    t = payload("clock-in-request")
    return wire.ApprovalProbe({k: t[k] for k in wire.COMMON} | {"ticket_digest": wire.message_digest(raw("clock-in-request"))},
                              scope, clock, ticket_expires_at=t["expires_at"])


def answer(p, **changes):
    return sign("id_approval", p.payload | {"decision": "allow", "server_time": "2026-10-21T08:00:10Z",
                                           "valid_until": "2026-10-21T08:00:30Z"} | changes)


def accept(p, data): return p.accept(data, device_id=DEVICE, key_id=KEY_ID, key=KEYS["id_approval"])


def test_twenty_second_approval_allows_fifteen_second_single_budget_then_expires():
    clock = Clock(); p = probe(clock); data = answer(p)
    clock.value = 11.0; a = accept(p, data)
    assert a.capture_budget(11.0) == 15
    assert a.capture_budget(26.0) == pytest.approx(2.75)
    with pytest.raises(wire.ProtocolError): a.remaining(28.75)
    with pytest.raises(wire.ProtocolError): accept(p, data)


@pytest.mark.parametrize("delay", (-0.1, 2.01, float("nan"), float("inf")))
def test_stale_or_invalid_round_trip_consumes_probe(delay):
    clock = Clock(); p = probe(clock); data = answer(p); clock.value += delay
    with pytest.raises(wire.ProtocolError): accept(p, data)
    clock.value = 11
    with pytest.raises(wire.ProtocolError): accept(p, data)


def test_new_probe_nonce_renewal_scope_and_invalidations():
    clock = Clock(); p = probe(clock); another = probe(clock)
    assert p.payload["probe_nonce"] != another.payload["probe_nonce"]
    with pytest.raises(wire.ProtocolError): accept(another, answer(p))
    p.invalidate()
    with pytest.raises(wire.ProtocolError): accept(p, answer(p))
    p = probe(clock, "result"); clock.value = 11; result_only = accept(p, answer(p))
    with pytest.raises(wire.ProtocolError): result_only.capture_budget(clock.value)
    with pytest.raises(wire.ProtocolError): result_only.remaining(10.9)
    with pytest.raises(wire.ProtocolError): result_only.remaining(float("nan"))


def test_renewal_never_extends_ticket_or_accepts_oversized_signed_lifetime():
    clock = Clock(); p = probe(clock)
    with pytest.raises(wire.ProtocolError): answer(p, valid_until="2026-10-21T08:00:31Z")
    p = probe(clock)
    beyond = answer(p, server_time="2026-10-21T08:00:50Z", valid_until="2026-10-21T08:01:10Z")
    with pytest.raises(wire.ProtocolError): accept(p, beyond)
    p = probe(clock)
    clamp = answer(p, server_time="2026-10-21T08:00:50Z", valid_until="2026-10-21T08:01:00Z")
    clock.value = 11; a = accept(p, clamp)
    assert a.capture_budget(11) == pytest.approx(7.75)


def test_bad_signature_consumes_probe_and_payload_mutation_cannot_change_expected_scope():
    clock = Clock(); p = probe(clock); tampered = json.loads(answer(p)); tampered["mac_b64"] = "A" * 43
    with pytest.raises(wire.ProtocolError): accept(p, encoded(tampered))
    with pytest.raises(wire.ProtocolError): accept(p, answer(p))
    p = probe(clock); p.payload["scope"] = "result"
    with pytest.raises(wire.ProtocolError): accept(p, answer(p))


@pytest.mark.parametrize("operation", tuple(API["input_examples"]))
def test_machine_readable_api_dto_and_no_client_slot_user_token_injection(operation):
    p = API["input_examples"][operation]
    assert wire.validate_api_input(operation, encoded(p)) == p
    for key in ("user_id", "expected_slot", "access_token", "server_url", "work_date"):
        with pytest.raises(wire.ProtocolError): wire.validate_api_input(operation, encoded(p | {key: 1}))


def test_api_byte_relay_keeps_exact_message_and_rejects_duplicate_uuid_and_wrong_kind():
    p = API["input_examples"]["complete"]
    assert wire.b64decode(wire.validate_api_input("complete", encoded(p))["result_b64"], 16384) == raw("matched-result")
    with pytest.raises(wire.ProtocolError): wire.validate_api_input("complete", encoded({"result_b64": wire.b64encode(raw("capture-probe"))}))
    p = API["input_examples"]["issue"]
    with pytest.raises(wire.ProtocolError): wire.validate_api_input("issue", encoded(p)[:-1] + b',"client_request_id":"other"}')


def test_matched_response_explicitly_has_no_normal_token_or_attendance_completion():
    p = API["response_examples"]["complete_matched"]
    assert p["state"] == "verified" and p["workflow_state"] == "pending"
    assert "access_token" not in p and "work_session_ref" not in p
    report = wire.readiness()
    assert report["contract_available"] and not report["ready"]
    assert not report["http_api_implemented"] and not report["mysql_storage_implemented"]


def test_enabling_contract_without_actual_service_fails_before_database_or_routes(monkeypatch):
    from remotework import create_app, db
    connect = Mock(side_effect=AssertionError("No DB initialization allowed"))
    monkeypatch.setattr(db, "init_app", connect)
    with pytest.raises(RuntimeError, match="IDENTITY_IMPLEMENTATION_REQUIRED"):
        create_app({"TESTING": True, "ENABLE_DEVICE_IDENTITY_DRAFT": True})
    connect.assert_not_called()


def test_default_app_keeps_identity_routes_and_ddl_absent(app):
    from sqlalchemy import event
    engine = app.extensions["db_engine"]
    def fail(*_): raise AssertionError("Unexpected DB access")
    event.listen(engine, "engine_connect", fail)
    try:
        assert not app.config["ENABLE_DEVICE_IDENTITY_DRAFT"]
        assert not any("device-identity" in r.rule for r in app.url_map.iter_rules())
        assert wire.readiness()["ready"] is False
    finally: event.remove(engine, "engine_connect", fail)
