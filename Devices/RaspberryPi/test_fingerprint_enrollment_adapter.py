"""Synthetic direction keys and fake hardware only; never use these keys live."""

from contextlib import closing
from dataclasses import replace
from datetime import datetime, timezone
import hashlib
import hmac
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import threading
import unittest
from unittest.mock import patch

from fingerprint_enrollment import EnrollmentError, EnrollmentLedger, SensorProfile
from fingerprint_enrollment_adapter import EnrollmentAdapter, SignedResultCache
from fingerprint_enrollment_wire import (
    COMMON, REQUEST, RESULT, DirectionKeys, WireError, encode64, mac_input,
    parse_json, sign_result, ticket_digest, verify, verify_result_binding,
)
from test_fingerprint_enrollment import CODES, FakeSensor


DEVICE = "123e4567-e89b-42d3-a456-426614174000"
KEY_ID = "public-enroll-test-v1"
KEYS = DirectionKeys(DEVICE, KEY_ID, "1", bytes(range(32)), bytes(range(32, 64)))
REQUEST_PAYLOAD = {
    "enrollment_id": "31593412-67fd-40d2-878b-868d15f99a11",
    "target_user_id": 88,
    "operation_revision": "1",
    "reserved_slot": 7,
    "claim_revision": "1",
    "key_epoch": "1",
    "nonce": "AAECAwQFBgcICQoLDA0ODw",
    "purpose": "fingerprint_enrollment",
    "issued_at": "2026-10-20T01:00:00Z",
    "expires_at": "2026-10-20T01:03:00Z",
    "session_ref": "public-admin-session",
}
EXPECTED_DIGEST = "80c53a48913914f188db502d708e03c680e58093f1e0069f61163698ea86a52c"


def compact(value):
    return json.dumps(value, separators=(",", ":")).encode("ascii")


def request_bytes(payload=None, raw_payload=None):
    # Test-only server. No request-signing API is supplied by the Pi codec.
    raw = compact(REQUEST_PAYLOAD if payload is None else payload) if raw_payload is None else raw_payload
    envelope = dict(v=1, kind=REQUEST, device_id=DEVICE, key_id=KEY_ID,
                    payload_b64=encode64(raw), mac_b64="")
    envelope["mac_b64"] = encode64(hmac.digest(KEYS.request_key, mac_input(envelope), "sha256"))
    return compact(envelope)


class WireTests(unittest.TestCase):
    def test_member1_public_request_and_result_vectors_match_exactly(self):
        raw = request_bytes()
        self.assertEqual(parse_json(raw)["mac_b64"], "V7qffS64heutrm_DPBqj28GQdk722vDyeC3t0Y0W5Hs")
        self.assertEqual(ticket_digest(raw), EXPECTED_DIGEST)
        self.assertEqual(verify(raw, REQUEST, KEYS), REQUEST_PAYLOAD)
        result = {name: REQUEST_PAYLOAD[name] for name in COMMON}
        result.update(ticket_digest=EXPECTED_DIGEST,
                      result_id="af7de985-1df9-4e65-a310-dd86fa4c6862",
                      outcome="sensor_stored", failure_code=None, elapsed_ms=42000)
        signed = sign_result(result, KEYS)
        self.assertEqual(parse_json(signed)["mac_b64"], "RcKC_-4eMAjplwS2Q42C4GW5h9vannDt6_Z3giyPwWU")
        verify_result_binding(REQUEST_PAYLOAD, EXPECTED_DIGEST, verify(signed, RESULT, KEYS))

    def test_digest_is_mac_input_not_envelope_or_reserialized_payload(self):
        raw = request_bytes()
        envelope = parse_json(raw)
        reordered = json.dumps(envelope, indent=2, sort_keys=True).encode("ascii")
        self.assertEqual(ticket_digest(raw), ticket_digest(reordered))
        self.assertNotEqual(ticket_digest(raw), hashlib.sha256(raw).hexdigest())
        self.assertNotEqual(ticket_digest(raw), hashlib.sha256(compact(REQUEST_PAYLOAD)).hexdigest())

    def test_rejects_bad_json_bytes_size_duplicates_constants_and_surrogates(self):
        for raw in (b"", b"x" * 16385, b"\xff", b"[]", b'{"v":1,"v":1}',
                    b'{"x":NaN}', b'{"x":Infinity}', b'{"x":1e999}', b'{"x":"\\ud800"}'):
            with self.subTest(raw=raw[:24]), self.assertRaises(WireError):
                verify(raw, REQUEST, KEYS)

    def test_header_types_domain_direction_base64_and_mac_length_are_strict(self):
        original = parse_json(request_bytes())
        changes = [("v", True), ("kind", "request"), ("kind", []),
                   ("device_id", DEVICE.upper()), ("key_id", "other"),
                   ("payload_b64", original["payload_b64"] + "="),
                   ("mac_b64", encode64(b"x" * 31)), ("mac_b64", "A" * 43)]
        for field, value in changes:
            with self.subTest(field=field, value=value), self.assertRaises(WireError):
                verify(compact(dict(original, **{field: value})), REQUEST, KEYS)
        wrong_direction = replace(KEYS, request_key=KEYS.result_key, result_key=KEYS.request_key)
        with self.assertRaises(WireError):
            verify(request_bytes(), REQUEST, wrong_direction)
        with self.assertRaises(WireError):
            replace(KEYS, result_key=KEYS.request_key)
        self.assertNotIn(repr(KEYS.request_key), repr(KEYS))

    def test_correct_mac_cannot_bypass_request_schema_or_expected_epoch(self):
        changes = [("target_user_id", True), ("target_user_id", 2147483648),
                   ("reserved_slot", False), ("reserved_slot", -1),
                   ("operation_revision", "01"), ("claim_revision", "9223372036854775808"),
                   ("key_epoch", "2"), ("nonce", "AA"), ("purpose", "identity_check"),
                   ("issued_at", "2026-10-20T01:00:00+00:00"),
                   ("expires_at", REQUEST_PAYLOAD["issued_at"]), ("session_ref", "bad space")]
        for field, value in changes:
            with self.subTest(field=field), self.assertRaises(WireError):
                verify(request_bytes(dict(REQUEST_PAYLOAD, **{field: value})), REQUEST, KEYS)
        for payload in ({}, dict(REQUEST_PAYLOAD, unknown=1)):
            with self.assertRaises(WireError):
                verify(request_bytes(payload), REQUEST, KEYS)
        with self.assertRaises(WireError):
            verify(request_bytes(raw_payload=b'{"target_user_id":1,"target_user_id":2}'), REQUEST, KEYS)

    def test_result_binding_and_failure_outcomes_cannot_claim_server_completion(self):
        payload = {name: REQUEST_PAYLOAD[name] for name in COMMON}
        payload.update(ticket_digest=EXPECTED_DIGEST,
                       result_id="af7de985-1df9-4e65-a310-dd86fa4c6862",
                       outcome="sensor_stored", failure_code=None, elapsed_ms=1)
        for field, value in (("outcome", "completed"), ("elapsed_ms", True),
                             ("failure_code", "sensor_error")):
            with self.subTest(field=field), self.assertRaises(WireError):
                sign_result(dict(payload, **{field: value}), KEYS)
        uncertain = dict(payload, outcome="recovery_required", failure_code="storage_uncertain")
        self.assertEqual(verify(sign_result(uncertain, KEYS), RESULT, KEYS), uncertain)
        with self.assertRaises(WireError):
            sign_result(dict(uncertain, outcome="failed"), KEYS)
        for field, value in (("reserved_slot", 8), ("claim_revision", "2"),
                             ("ticket_digest", "0" * 64)):
            other = verify(sign_result(dict(payload, **{field: value}), KEYS), RESULT, KEYS)
            with self.assertRaises(WireError):
                verify_result_binding(REQUEST_PAYLOAD, EXPECTED_DIGEST, other)


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.ledger = EnrollmentLedger(self.root / "core" / "ledger.sqlite3")
        self.cache = SignedResultCache(self.root / "private" / "results.sqlite3")
        self.sensor, self.lock, self.cancel = FakeSensor(), threading.Lock(), threading.Event()
        self.now = datetime(2026, 10, 20, 1, 1, tzinfo=timezone.utc)
        self.allowed, self.monotonic = True, 0.0
        self.approvals = []
        self.worker = self.adapter()

    def approve(self, ticket):
        self.approvals.append(ticket)
        return self.allowed

    def sleep(self, seconds):
        self.monotonic += seconds

    def adapter(self, **overrides):
        options = dict(sensor=self.sensor, codes=CODES, sensor_lock=self.lock, ledger=self.ledger,
                       profile=SensorProfile(300, 1, 299), keys=KEYS, cache=self.cache,
                       current_approval=self.approve, trusted_utc=lambda: self.now,
                       clock=lambda: self.monotonic, sleep=self.sleep)
        options.update(overrides)
        return EnrollmentAdapter(**options)

    def enroll(self):
        return self.worker.enroll(request_bytes(), self.cancel)

    def assert_error(self, code, action=None):
        with self.assertRaises(EnrollmentError) as caught:
            (action or self.enroll)()
        self.assertEqual(caught.exception.code, code)
        return caught.exception

    def test_verified_ticket_reaches_two_capture_core_and_signed_minimal_result(self):
        raw = self.enroll()
        response = verify(raw, RESULT, KEYS)
        verify_result_binding(REQUEST_PAYLOAD, EXPECTED_DIGEST, response)
        self.assertEqual(self.sensor.stores, [7])
        self.assertEqual(self.sensor.slots, {2, 6, 7})
        self.assertEqual(response["outcome"], "sensor_stored")
        self.assertGreater(len(self.approvals), 5)
        self.assertEqual(self.approvals[0].payload["claim_revision"], "1")
        with self.assertRaises(TypeError):
            self.approvals[0].payload["claim_revision"] = "2"
        for name in ("target_user_id", "session_ref", "nonce", "confidence", "name"):
            self.assertNotIn(name, response)

    def test_restart_returns_identical_saved_bytes_without_signing_or_sensor_io(self):
        first = self.enroll()
        self.sensor.calls.clear()
        restarted = self.adapter(cache=SignedResultCache(self.cache.path),
                                 ledger=EnrollmentLedger(self.ledger.path))
        with patch("fingerprint_enrollment_adapter.sign_result", side_effect=AssertionError("no re-sign")):
            self.assertEqual(restarted.enroll(request_bytes(), self.cancel), first)
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.stores, [7])

    def test_missing_false_non_boolean_or_failed_approval_blocks_all_sensor_io(self):
        for provider in (None, lambda _: False, lambda _: 1,
                         lambda _: (_ for _ in ()).throw(RuntimeError("PRIVATE ERROR"))):
            worker = self.adapter(current_approval=provider)
            error = self.assert_error("not_authorized", lambda: worker.enroll(request_bytes(), self.cancel))
            self.assertNotIn("PRIVATE", str(error))
        self.assertEqual(self.sensor.calls, [])

    def test_untrusted_missing_naive_before_start_or_expired_time_blocks_all_io(self):
        for now in (None, datetime(2026, 10, 20),
                    datetime(2026, 10, 20, 0, 59, 59, tzinfo=timezone.utc),
                    datetime(2026, 10, 20, 1, 3, tzinfo=timezone.utc)):
            self.now = now
            self.assert_error("not_authorized")
        worker = self.adapter(trusted_utc=None)
        self.assert_error("not_authorized", lambda: worker.enroll(request_bytes(), self.cancel))
        self.assertEqual(self.sensor.calls, [])

    def test_slow_current_approval_cannot_extend_expiry(self):
        def slow(_):
            self.now = datetime(2026, 10, 20, 1, 3, tzinfo=timezone.utc)
            return True
        worker = self.adapter(current_approval=slow)
        self.assert_error("not_authorized", lambda: worker.enroll(request_bytes(), self.cancel))
        self.assertEqual(self.sensor.calls, [])

    def test_bad_mac_never_calls_current_approval_or_touches_sensor(self):
        envelope = parse_json(request_bytes())
        envelope["mac_b64"] = "A" * 43
        self.assert_error("invalid_enrollment_request",
                          lambda: self.worker.enroll(compact(envelope), self.cancel))
        self.assertEqual(self.approvals, [])
        self.assertEqual(self.sensor.calls, [])

    def test_cached_result_still_requires_current_approval_and_unexpired_request(self):
        self.enroll()
        self.sensor.calls.clear()
        self.allowed = False
        self.assert_error("not_authorized")
        self.allowed = True
        self.now = datetime(2026, 10, 20, 1, 3, tzinfo=timezone.utc)
        self.assert_error("not_authorized")
        self.assertEqual(self.sensor.calls, [])

    def test_same_enrollment_with_other_claim_cannot_reuse_cached_result(self):
        self.enroll()
        self.sensor.calls.clear()
        other = request_bytes(dict(REQUEST_PAYLOAD, claim_revision="2"))
        self.assert_error("request_conflict", lambda: self.worker.enroll(other, self.cancel))
        self.assertEqual(self.sensor.calls, [])

    def test_cache_commit_failure_returns_no_result_and_retry_never_stores_again(self):
        with patch.object(self.cache, "put", side_effect=sqlite3.OperationalError("disk full")):
            self.assert_error("result_persistence_required")
        self.assertEqual(self.sensor.stores, [7])
        self.sensor.calls.clear()
        result = verify(self.enroll(), RESULT, KEYS)
        with closing(sqlite3.connect(self.ledger.path)) as db:
            saved = db.execute("SELECT result_id,elapsed_ms FROM local_enrollments").fetchone()
        self.assertEqual((result["result_id"], result["elapsed_ms"]), saved)
        self.assertEqual(self.sensor.calls, [])

    def test_no_result_is_returned_before_cache_commit(self):
        put = self.cache.put
        persisted = []
        def commit(operation, raw):
            value = put(operation, raw)
            with closing(sqlite3.connect(self.cache.path)) as db:
                persisted.append(db.execute("SELECT envelope FROM signed_results").fetchone()[0])
            return value
        with patch.object(self.cache, "put", side_effect=commit):
            raw = self.enroll()
        self.assertEqual(persisted, [raw])

    def test_cancel_after_sensor_store_blocks_delivery_and_does_not_repeat_storage(self):
        self.sensor.on_store = self.cancel.set
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.stores, [7])
        self.cancel.clear()
        self.sensor.calls.clear()
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.calls, [])

    def test_authority_lost_after_store_blocks_delivery_and_keeps_slot_quarantined(self):
        self.sensor.on_store = lambda: setattr(self, "allowed", False)
        self.assert_error("recovery_required")
        self.allowed = True
        self.sensor.calls.clear()
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.calls, [])

    def test_interrupted_pending_operation_never_rescans(self):
        # Capture a validated operation without ever enabling sensor I/O.
        self.allowed = False
        self.assert_error("not_authorized")
        operation = self.approvals[0].operation
        self.ledger.claim(operation)
        self.ledger.transition(operation, "capturing", "store_pending")
        self.allowed = True
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.calls, [])

    def test_corrupted_cached_bytes_are_not_returned_or_replaced(self):
        self.enroll()
        self.sensor.calls.clear()
        with closing(sqlite3.connect(self.cache.path)) as db, db:
            db.execute("UPDATE signed_results SET envelope=?", (b"broken",))
        self.assert_error("result_cache_invalid")
        self.assertEqual(self.sensor.calls, [])

    def test_cache_cannot_be_overwritten_and_contains_no_raw_request_or_keys(self):
        raw = self.enroll()
        operation = self.approvals[0].operation
        with self.assertRaises(EnrollmentError):
            self.cache.put(operation, raw + b" ")
        self.assertEqual(self.cache.get(operation), raw)
        with closing(sqlite3.connect(self.cache.path)) as db:
            columns = [row[1] for row in db.execute("PRAGMA table_info(signed_results)")]
            row = db.execute("SELECT * FROM signed_results").fetchone()
        self.assertEqual(columns, ["enrollment_id", "binding", "envelope"])
        self.assertNotIn(REQUEST_PAYLOAD["nonce"], repr(row))
        self.assertNotIn(REQUEST_PAYLOAD["session_ref"], repr(row))
        self.assertNotIn(KEYS.request_key, self.cache.path.read_bytes())

    @unittest.skipUnless(os.name == "posix", "POSIX ownership/modes require Linux/Pi")
    def test_cache_requires_private_directory_and_file(self):
        self.assertEqual(self.cache.path.stat().st_mode & 0o777, 0o600)
        self.cache.path.chmod(0o644)
        with self.assertRaises(EnrollmentError):
            SignedResultCache(self.cache.path)
        self.cache.path.chmod(0o600)
        self.cache.path.parent.chmod(0o755)
        with self.assertRaises(EnrollmentError):
            SignedResultCache(self.cache.path)
        self.cache.path.parent.chmod(0o700)


if __name__ == "__main__":
    unittest.main()
