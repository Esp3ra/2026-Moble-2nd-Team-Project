import json
import pytest
from remotework import enrollment_approval_draft as a
from remotework import enrollment_protocol_draft as e

DID = "123e4567-e89b-42d3-a456-426614174000"
KEY = bytes(range(32))


def value():
    return dict(enrollment_id="31595555-1111-4111-8111-111111111111", operation_revision="1", reserved_slot=7,
        claim_revision="1", key_epoch="1", ticket_digest="a" * 64, probe_nonce=e.b64encode(bytes(32)),
        scope="sensor_write", decision="allow", server_time="2026-10-20T01:00:00Z", valid_until="2026-10-20T01:00:05Z")


def sign(v):
    return a.sign(v, device_id=DID, key_id="public-test", key=KEY)


def test_probe_is_random_single_use_scope_bound_and_fail_closed():
    clock = [100.]
    v = value()
    expected = {f: v[f] for f in e.COMMON | {"ticket_digest", "scope"}}
    probe = a.ApprovalProbe(expected=expected, monotonic=lambda: clock[0])
    other = a.ApprovalProbe(expected=expected, monotonic=lambda: clock[0])
    assert probe.expected["probe_nonce"] != other.expected["probe_nonce"]
    raw = sign(v | probe.expected)
    clock[0] += .2
    permit = probe.accept(raw, device_id=DID, key_id="public-test", key=KEY)
    assert permit.allows("sensor_write", clock[0])
    assert not permit.allows("result_read", clock[0])
    for repeat in (probe, other):
        with pytest.raises(e.ProtocolError):
            repeat.accept(raw, device_id=DID, key_id="public-test", key=KEY)
    with pytest.raises(e.ProtocolError):
        other.accept(sign(v | other.expected), device_id=DID, key_id="public-test", key=KEY)
    with pytest.raises(e.ProtocolError):
        permit.trusted_utc(104.)  # Conservative upper time includes RTT and truncation margin.
    with pytest.raises(e.ProtocolError):
        permit.trusted_utc(99.)


@pytest.mark.parametrize("rtt", [-.1, 2.01, float("nan"), float("inf"), True])
def test_bad_elapsed_clock_never_allows(rtt):
    with pytest.raises(e.ProtocolError):
        a.FreshApproval.accept(value(), sent_mono=0., received_mono=rtt)


@pytest.mark.parametrize("patch", [
    {"scope": "auth"}, {"decision": "deny"}, {"reserved_slot": True}, {"claim_revision": 1},
    {"probe_nonce": "abc="}, {"ticket_digest": "A" * 64}, {"valid_until": "2026-10-20T01:00:06Z"},
    {"valid_until": "2026-10-20T01:00:00Z"}, {"extra": 1}, {"key_epoch": "0"},
])
def test_exact_approval_contract(patch):
    with pytest.raises(e.ProtocolError):
        sign(value() | patch)


def test_mac_domain_direction_binding_and_no_invalidate_fallback():
    v = value()
    expected = {f: v[f] for f in e.COMMON | {"ticket_digest", "scope", "probe_nonce"}}
    raw = sign(v)
    assert a.verify(raw, expected=expected, device_id=DID, key_id="public-test", key=KEY) == v
    for change in ({"scope": "result_read"}, {"key_epoch": "2"}, {"claim_revision": "2"}):
        with pytest.raises(e.ProtocolError):
            a.verify(raw, expected=expected | change, device_id=DID, key_id="public-test", key=KEY)
    with pytest.raises(e.ProtocolError):
        a.verify(raw, expected=expected, device_id=DID, key_id="public-test", key=bytes(range(32, 64)))
    with pytest.raises(e.ProtocolError):
        e.verify_payload(raw, expected_kind=e.REQUEST, device_id=DID, key_id="public-test", key_epoch="1", key=KEY)
    probe = a.ApprovalProbe(expected={k: v for k, v in expected.items() if k != "probe_nonce"}, monotonic=lambda: 100.)
    probe.invalidate()
    with pytest.raises(e.ProtocolError):
        probe.accept(sign(value() | probe.expected), device_id=DID, key_id="public-test", key=KEY)
