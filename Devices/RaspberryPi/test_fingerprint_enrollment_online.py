"""Online exchange boundary tests: synthetic signed server replies, fake sensor."""

from contextlib import closing
from datetime import datetime, timedelta, timezone
import hmac
import sqlite3
import unittest
from unittest.mock import patch

import test_fingerprint_enrollment_adapter as fixtures
from fingerprint_enrollment import EnrollmentError
from fingerprint_enrollment_approval import OnlineEnrollmentApproval, verify_approval
from fingerprint_enrollment_wire import COMMON, RESULT, encode64, parse_json, verify


def signed_approval(payload, *, key=None):
    """Independent synthetic server signer; never used by product Pi code."""
    envelope = dict(v=1, kind="enroll_approval", device_id=fixtures.DEVICE,
                    key_id=fixtures.KEY_ID, payload_b64=encode64(fixtures.compact(payload)), mac_b64="")
    message = b"RW-ENROLL-APPROVAL-v1\0" + "\0".join(
        envelope[name] for name in ("kind", "device_id", "key_id", "payload_b64")
    ).encode("ascii")
    envelope["mac_b64"] = encode64(hmac.digest(
        fixtures.KEYS.request_key if key is None else key, message, "sha256"))
    return fixtures.compact(envelope)


class FakeApprovalRelay:
    def __init__(self):
        self.monotonic = 100.0
        self.utc = datetime(2026, 10, 20, 1, 1, tzinfo=timezone.utc)
        self.calls, self.responses = [], []
        self.allowed, self.original_session = True, True
        self.delay, self.change, self.hook = 0.05, lambda value: value, lambda: None
        self.signing_key = fixtures.KEYS.request_key

    def clock(self):
        return self.monotonic

    def exchange(self, enrollment_id, body, cancellation):
        self.calls.append((enrollment_id, dict(body)))
        if not self.allowed or not self.original_session:
            raise RuntimeError("PRIVATE AUTH/SESSION DETAIL")
        if body["scope"] == "sensor_write" and self.utc >= datetime(2026, 10, 20, 1, 3, tzinfo=timezone.utc):
            raise RuntimeError("write expired")
        expected_fields = (set(COMMON) - {"enrollment_id"}) | {"ticket_digest", "probe_nonce", "scope"}
        if set(body) != expected_fields:
            raise AssertionError("Expected actual seven-field approval DTO")
        self.monotonic += self.delay
        self.hook()
        payload = dict(body, enrollment_id=enrollment_id, decision="allow",
                       server_time=self.utc.strftime("%Y-%m-%dT%H:%M:%SZ"),
                       valid_until=(self.utc + timedelta(seconds=5)).strftime("%Y-%m-%dT%H:%M:%SZ"))
        raw = signed_approval(self.change(payload), key=self.signing_key)
        self.responses.append(raw)
        return raw


class OnlineTests(unittest.TestCase):
    def setUp(self):
        self.fx = fixtures.AdapterTests("test_verified_ticket_reaches_two_capture_core_and_signed_minimal_result")
        self.fx.setUp()
        self.addCleanup(self.fx.doCleanups)
        self.relay = FakeApprovalRelay()
        self.worker = self.fx.adapter(current_approval=None, trusted_utc=None)

    def enroll(self):
        return self.worker.enroll_online(
            fixtures.request_bytes(), self.fx.cancel, exchange=self.relay.exchange,
            approval_clock=self.relay.clock,
        )

    def read(self, enrollment_id=None):
        return self.worker.read_result_online(
            enrollment_id or fixtures.REQUEST_PAYLOAD["enrollment_id"], self.fx.cancel,
            exchange=self.relay.exchange, approval_clock=self.relay.clock,
        )

    def context(self):
        value = {name: fixtures.REQUEST_PAYLOAD[name] for name in COMMON}
        return dict(value, ticket_digest=fixtures.EXPECTED_DIGEST)

    def session(self, scope="sensor_write", **changes):
        options = dict(context=self.context(), keys=fixtures.KEYS, scope=scope,
                       exchange=self.relay.exchange, cancellation=self.fx.cancel,
                       monotonic=self.relay.clock)
        options.update(changes)
        return OnlineEnrollmentApproval(**options)

    def assert_error(self, code, action):
        with self.assertRaises(EnrollmentError) as caught:
            action()
        self.assertEqual(caught.exception.code, code)
        self.assertNotIn("PRIVATE", str(caught.exception))

    def test_online_bootstrap_works_without_any_pi_wall_clock(self):
        self.fx.now = None
        result = verify(self.enroll(), RESULT, fixtures.KEYS)
        self.assertEqual(result["outcome"], "sensor_stored")
        self.assertEqual(self.fx.sensor.stores, [7])
        self.assertGreater(len(self.relay.calls), 5)
        self.assertTrue(all(body["scope"] == "sensor_write" for _, body in self.relay.calls))
        nonces = [body["probe_nonce"] for _, body in self.relay.calls]
        self.assertEqual(len(nonces), len(set(nonces)))

    def test_approval_refresh_can_replace_expired_initial_utc_without_opening_expired_ticket(self):
        state = {"utc": None}
        def current(_):
            state["utc"] = self.fx.now
            return True
        worker = self.fx.adapter(current_approval=current, trusted_utc=lambda: state["utc"])
        self.assertEqual(verify(worker.enroll(fixtures.request_bytes(), self.fx.cancel), RESULT,
                                fixtures.KEYS)["outcome"], "sensor_stored")
        self.fx.now = datetime(2026, 10, 20, 1, 3, tzinfo=timezone.utc)
        self.assert_error("not_authorized", lambda: worker.enroll(fixtures.request_bytes(), self.fx.cancel))

    def test_bad_request_mac_never_starts_online_exchange(self):
        envelope = parse_json(fixtures.request_bytes())
        envelope["mac_b64"] = "A" * 43
        self.assert_error("invalid_enrollment_request", lambda: self.worker.enroll_online(
            fixtures.compact(envelope), self.fx.cancel, exchange=self.relay.exchange,
            approval_clock=self.relay.clock))
        self.assertEqual(self.relay.calls, [])
        self.assertEqual(self.fx.sensor.calls, [])

    def test_permission_or_original_session_loss_blocks_before_sensor_io(self):
        for field in ("allowed", "original_session"):
            setattr(self.relay, field, False)
            self.assert_error("not_authorized", self.enroll)
            setattr(self.relay, field, True)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_wrong_job_claim_epoch_digest_nonce_scope_or_decision_blocks_before_io(self):
        changes = [("enrollment_id", "00000000-0000-4000-8000-000000000002"),
                   ("claim_revision", "2"), ("key_epoch", "2"), ("reserved_slot", True),
                   ("ticket_digest", "0" * 64), ("probe_nonce", encode64(bytes(32))),
                   ("scope", "result_read"), ("decision", "deny")]
        for name, value in changes:
            with self.subTest(name=name):
                self.relay.change = lambda payload: dict(payload, **{name: value})
                self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_result_direction_key_cannot_sign_an_approval(self):
        self.relay.signing_key = fixtures.KEYS.result_key
        self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_rtt_over_two_seconds_and_overlong_lifetime_are_rejected(self):
        self.relay.delay = 2.01
        self.assert_error("not_authorized", self.enroll)
        self.relay.delay = 0.05
        self.relay.change = lambda payload: dict(payload, valid_until="2026-10-20T01:01:06Z")
        self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_expiry_during_signature_processing_is_included(self):
        def slow(*args, **kwargs):
            result = verify_approval(*args, **kwargs)
            self.relay.monotonic += 5
            return result
        with patch("fingerprint_enrollment_approval.verify_approval", side_effect=slow):
            self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_clock_nan_bool_and_backwards_fail_closed(self):
        for value in (float("nan"), float("inf"), True, -1):
            session = self.session(monotonic=lambda: value)
            self.assert_error("not_authorized", session.refresh)
        session = self.session()
        session.refresh()
        session.trusted_utc()
        self.relay.monotonic -= 0.01
        self.assert_error("not_authorized", session.trusted_utc)

    def test_failed_refresh_cannot_fall_back_to_previous_approval(self):
        session = self.session()
        session.refresh()
        session.trusted_utc()
        self.relay.allowed = False
        self.assert_error("not_authorized", session.refresh)
        self.assert_error("not_authorized", session.trusted_utc)

    def test_old_probe_response_cannot_satisfy_a_new_probe_or_restart(self):
        session = self.session()
        session.refresh()
        old = self.relay.responses[-1]
        session.exchange = lambda *_: old
        self.assert_error("not_authorized", session.refresh)
        restarted = self.session(exchange=lambda *_: old)
        self.assert_error("not_authorized", restarted.refresh)

    def test_cancellation_during_approval_and_late_reply_invalidation_are_rejected(self):
        self.relay.hook = self.fx.cancel.set
        self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])
        self.fx.cancel.clear()
        session = self.session()
        self.relay.hook = session.invalidate
        self.assert_error("not_authorized", session.refresh)
        self.assert_error("not_authorized", session.trusted_utc)

    def test_second_operation_never_reuses_previous_online_approval(self):
        self.enroll()
        self.fx.sensor.calls.clear()
        self.relay.allowed = False
        self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertEqual(self.fx.sensor.stores, [7])

    def test_revocation_after_sensor_store_keeps_uncertainty_and_no_result_delivery(self):
        self.fx.sensor.on_store = lambda: setattr(self.relay, "allowed", False)
        self.assert_error("recovery_required", self.enroll)
        self.relay.allowed = True
        self.fx.sensor.calls.clear()
        self.assert_error("recovery_required", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertEqual(self.fx.sensor.stores, [7])

    def test_expired_ticket_cannot_start_a_new_scan_even_with_read_approval(self):
        self.relay.utc = datetime(2026, 10, 20, 1, 4, tzinfo=timezone.utc)
        self.assert_error("not_authorized", self.enroll)
        self.relay.utc = datetime(2026, 10, 20, 1, 1, tzinfo=timezone.utc)
        self.relay.change = lambda payload: dict(payload, scope="result_read")
        self.assert_error("not_authorized", self.enroll)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_expired_result_read_returns_identical_bytes_without_ticket_sensor_or_signer(self):
        original = self.enroll()
        self.relay.utc = datetime(2026, 10, 20, 1, 4, tzinfo=timezone.utc)
        self.fx.sensor.calls.clear()
        self.relay.calls.clear()
        restarted = self.fx.adapter(current_approval=None, trusted_utc=None)
        with patch("fingerprint_enrollment_adapter.FingerprintEnrollment", side_effect=AssertionError("no core")), \
                patch("fingerprint_enrollment_adapter.sign_result", side_effect=AssertionError("no signing")):
            saved = restarted.read_result_online(
                fixtures.REQUEST_PAYLOAD["enrollment_id"], self.fx.cancel,
                exchange=self.relay.exchange, approval_clock=self.relay.clock)
        self.assertEqual(saved, original)
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertTrue(all(body["scope"] == "result_read" for _, body in self.relay.calls))

    def test_read_permission_session_and_retention_denials_never_return_saved_bytes(self):
        self.enroll()
        self.fx.sensor.calls.clear()
        for field in ("allowed", "original_session"):
            setattr(self.relay, field, False)
            self.assert_error("not_authorized", self.read)
            setattr(self.relay, field, True)
        worker = self.worker
        def expired(*_):
            raise RuntimeError("server result window expired")
        self.assert_error("not_authorized", lambda: worker.read_result_online(
            fixtures.REQUEST_PAYLOAD["enrollment_id"], self.fx.cancel,
            exchange=expired, approval_clock=self.relay.clock))
        self.assertEqual(self.fx.sensor.calls, [])

    def test_read_scope_cannot_be_satisfied_by_sensor_write_approval(self):
        self.enroll()
        self.fx.sensor.calls.clear()
        self.relay.change = lambda payload: dict(payload, scope="sensor_write")
        self.assert_error("not_authorized", self.read)
        self.assertEqual(self.fx.sensor.calls, [])

    def test_missing_cache_never_creates_a_result_or_requests_sensor_access(self):
        self.assert_error("result_not_available", self.read)
        self.assertEqual(self.relay.calls, [])
        self.assertEqual(self.fx.sensor.calls, [])

    def test_cache_moved_under_wrong_operation_or_corrupted_is_rejected(self):
        self.enroll()
        other = "00000000-0000-4000-8000-000000000002"
        with closing(sqlite3.connect(self.fx.cache.path)) as db, db:
            db.execute("UPDATE signed_results SET enrollment_id=?", (other,))
        self.relay.calls.clear()
        self.assert_error("result_cache_invalid", lambda: self.read(other))
        with closing(sqlite3.connect(self.fx.cache.path)) as db, db:
            db.execute("UPDATE signed_results SET envelope=?", (b"broken",))
        self.assert_error("result_cache_invalid", lambda: self.read(other))
        self.assertEqual(self.relay.calls, [])

    def test_returned_result_does_not_contain_approval_probe_or_sensitive_request_fields(self):
        raw = self.enroll()
        result = verify(raw, RESULT, fixtures.KEYS)
        for name in ("nonce", "probe_nonce", "target_user_id", "session_ref", "server_time"):
            self.assertNotIn(name, result)
        with closing(sqlite3.connect(self.fx.cache.path)) as db:
            self.assertEqual(db.execute("SELECT envelope FROM signed_results").fetchone()[0], raw)


if __name__ == "__main__":
    unittest.main()
