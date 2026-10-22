"""Reproduce PUBLIC SYNTHETIC RW-ID-v2 vectors; never deploy these keys."""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "RemoteWork.Server"))
from remotework import device_identity_contract as wire

DEVICE = "10000000-0000-4000-8000-000000000001"
CHALLENGE = "20000000-0000-4000-8000-000000000001"
REQUEST_ID = "30000000-0000-4000-8000-000000000001"
KEY_ID = "public-identity-vector-epoch-1"
# Four intentionally public, distinct test keys. NOT private runtime credentials.
KEYS = {kind: bytes([i]) * 32 for i, kind in enumerate(sorted(wire.KINDS), start=1)}


def build():
    vectors = []
    common = dict(challenge_id=CHALLENGE, nonce=wire.b64encode(b"N" * 32),
                  mapping_revision="7", key_epoch="1")
    ticket = common | dict(expected_slot=17, purpose="clock_in", session_ref=wire.b64encode(b"C" * 32),
        work_session_ref=None, work_date="2026-10-21", issued_at="2026-10-21T08:00:00Z",
        expires_at="2026-10-21T08:01:00Z")

    def add(name, kind, payload):
        payload_bytes = json.dumps(payload, separators=(",", ":")).encode("ascii")
        raw = wire.sign(payload_bytes, kind=kind, device_id=DEVICE, key_id=KEY_ID, key=KEYS[kind])
        vectors.append(dict(name=name, kind=kind, payload_json=payload_bytes.decode("ascii"),
                            envelope_json=raw.decode("ascii"), digest=wire.message_digest(raw),
                            mac_input_hex=wire.mac_input(json.loads(raw)).hex()))
        return raw

    raw_ticket = add("clock-in-request", "id_request", ticket)
    binding = common | dict(ticket_digest=wire.message_digest(raw_ticket))
    for scope, nonce_byte in (("capture", b"P"), ("result", b"Q")):
        probe = binding | dict(probe_nonce=wire.b64encode(nonce_byte * 32), scope=scope)
        add(scope + "-probe", "id_probe", probe)
        approval = probe | dict(decision="allow", server_time="2026-10-21T08:00:10Z",
                                valid_until="2026-10-21T08:00:30Z")
        raw_approval = add(scope + "-approval", "id_approval", approval)
    for index, outcome in enumerate(wire.FAILURES, start=1):
        add(outcome + "-result", "id_result", binding | dict(
            result_id=f"40000000-0000-4000-8000-{index:012d}", outcome=outcome,
            slot=17 if outcome == "matched" else None, failure_code=wire.FAILURES[outcome],
            approval_digest=wire.message_digest(raw_approval) if outcome in ("matched", "no_match") else None))
    for purpose in ("noon_check", "clock_out"):
        add(purpose + "-request", "id_request", ticket | dict(
            purpose=purpose, work_session_ref="50000000-0000-4000-8000-000000000001"))
    fixture = {"warning": "PUBLIC SYNTHETIC TEST KEYS. NEVER USE IN RUNTIME CONFIG.",
               "contract": "RW-ID-v2", "device_id": DEVICE, "key_id": KEY_ID,
               "public_test_keys_hex": {k: v.hex() for k, v in KEYS.items()}, "vectors": vectors}
    by_name = {v["name"]: v for v in vectors}
    api = {"implementation_status": "contract-only; no HTTP routes installed", "prefix": "/api/v1/device-identity",
           "input_examples": {
        "login": dict(login_id="synthetic-user", password="public-example-not-a-real-password", client_request_id=REQUEST_ID),
        "issue": dict(device_id=DEVICE, purpose="clock_in", client_request_id=REQUEST_ID),
        "approval": dict(probe_b64=wire.b64encode(by_name["capture-probe"]["envelope_json"].encode())),
        "complete": dict(result_b64=wire.b64encode(by_name["matched-result"]["envelope_json"].encode())),
        "cancel": dict(client_request_id=REQUEST_ID), "finalize": dict(client_request_id=REQUEST_ID),
        "revoke": dict(client_request_id=REQUEST_ID)},
        "response_examples": {
            "unknown_request": {"state": "not_recorded"},
            "issue": dict(challenge_id=CHALLENGE, client_request_id=REQUEST_ID, state="issued",
                          expires_at=ticket["expires_at"], request_b64=wire.b64encode(raw_ticket)),
            "complete_matched": dict(challenge_id=CHALLENGE, state="verified", workflow_state="pending",
                identity_receipt=dict(receipt_id="60000000-0000-4000-8000-000000000001", purpose="clock_in", verified_at="2026-10-21T08:00:15Z")),
            "complete_no_match": dict(challenge_id=CHALLENGE, state="rejected", workflow_state="not_started", identity_receipt=None),
        }}
    return fixture, api


if __name__ == "__main__":
    fixture, api = build()
    for name, content in (("device_identity_v2.json", fixture), ("device_identity_v2_api.json", api)):
        (ROOT / "Tests/Fixtures" / name).write_text(json.dumps(content, indent=2, ensure_ascii=True) + "\n", encoding="utf-8", newline="\n")
