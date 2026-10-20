from contextlib import closing
from dataclasses import replace
import sqlite3
import tempfile
import threading
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch
from uuid import uuid4

from fingerprint_enrollment import (
    EnrollmentError, EnrollmentLedger, EnrollmentOperation, FingerprintEnrollment, SensorProfile,
)


CODES = SimpleNamespace(OK=0, NOFINGER=2, NOMATCH=8, NOTFOUND=9, ENROLLMISMATCH=10)
DEVICE = "00000000-0000-4000-8000-000000000001"


class FakeSensor:
    # No template/image download or deletion methods exist in this fake.
    def __init__(self):
        self.slots = {2, 6}
        self.library_size, self.system_id = 300, 0
        self.template_count, self.templates = 2, [2, 6]
        self.images = [2, 0, 2, 0]
        self.search_result, self.model_result = 9, 0
        self.calls, self.stores = [], []
        self.on_model = self.on_store = lambda: None
        self.persist = True

    def read_sysparam(self):
        self.calls.append("parameters")
        return 0

    def count_templates(self):
        self.calls.append("count")
        self.template_count = len(self.slots)
        return 0

    def read_templates(self):
        self.calls.append("index")
        self.templates = sorted(self.slots)
        return 0

    def get_image(self):
        self.calls.append("image")
        return self.images.pop(0) if self.images else 2

    def image_2_tz(self, buffer):
        self.calls.append(("features", buffer))
        return 0

    def finger_search(self):
        self.calls.append("search")
        return self.search_result

    def create_model(self):
        self.calls.append("model")
        self.on_model()
        return self.model_result

    def store_model(self, slot):
        self.calls.append("store")
        self.stores.append(slot)
        if self.persist:
            self.slots.add(slot)
        self.on_store()
        return 0


class EnrollmentTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.path = Path(self.directory.name) / "state.sqlite3"
        self.ledger = EnrollmentLedger(self.path)
        self.sensor, self.lock, self.cancel = FakeSensor(), threading.Lock(), threading.Event()
        self.now, self.allowed = 0.0, True
        self.profile = SensorProfile(300, 1, 299)  # Explicit test policy; no physical slot proof.
        self.operation = EnrollmentOperation(str(uuid4()), DEVICE, 123, "1", 12, "0" * 64)
        self.worker = self.make_worker()

    def make_worker(self, progress=lambda *_: None):
        return FingerprintEnrollment(
            self.sensor, CODES, self.lock, EnrollmentLedger(self.path), DEVICE, self.profile,
            authorize=lambda _: self.allowed, progress=progress,
            clock=lambda: self.now, sleep=self.sleep,
        )

    def sleep(self, seconds):
        self.now += seconds

    def run_operation(self):
        return self.worker.enroll(self.operation, self.cancel)

    def assert_error(self, code, action=None):
        with self.assertRaises(EnrollmentError) as error:
            (action or self.run_operation)()
        self.assertEqual(error.exception.code, code)
        self.assertFalse(self.lock.locked())

    def test_success_uses_two_captures_preserves_old_slots_and_is_only_sensor_evidence(self):
        receipt = self.run_operation()
        self.assertEqual(receipt.state, "sensor_stored")
        self.assertEqual(self.sensor.slots, {2, 6, 12})
        self.assertEqual(self.sensor.stores, [12])
        self.assertEqual(self.sensor.calls.count("image"), 4)
        self.assertIn(("features", 1), self.sensor.calls)
        self.assertIn(("features", 2), self.sensor.calls)

    def test_same_operation_after_restart_returns_same_result_without_any_sensor_io(self):
        original = self.run_operation()
        self.sensor.calls.clear()
        restarted = self.make_worker()
        self.assertEqual(restarted.enroll(self.operation, self.cancel), original)
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.stores, [12])

    def test_changed_binding_and_a_new_operation_on_claimed_slot_are_rejected(self):
        self.run_operation()
        self.sensor.calls.clear()
        changed = replace(self.operation, target_user_id=124)
        self.assert_error("request_conflict", lambda: self.worker.enroll(changed, self.cancel))
        new = replace(self.operation, enrollment_id=str(uuid4()))
        self.assert_error("recovery_required", lambda: self.worker.enroll(new, self.cancel))
        self.assertEqual(self.sensor.calls, [])

    def test_interrupted_capture_or_store_blocks_automatic_rescan_after_restart(self):
        self.ledger.claim(self.operation)
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.calls, [])
        other = replace(self.operation, enrollment_id=str(uuid4()), reserved_slot=13)
        self.assert_error("recovery_required", lambda: self.worker.enroll(other, self.cancel))

    def test_slot_occupied_or_capacity_mismatch_does_not_capture(self):
        self.operation = replace(self.operation, reserved_slot=2)
        self.assert_error("slot_occupied")
        self.assertNotIn("image", self.sensor.calls)
        self.operation = replace(self.operation, enrollment_id=str(uuid4()), reserved_slot=12)
        self.sensor.library_size = 200
        self.assert_error("capacity_changed")
        self.assertEqual(self.sensor.stores, [])

    def test_invalid_input_wrong_device_and_unconfigured_slots_never_touch_hardware(self):
        cases = [
            (replace(self.operation, target_user_id=True), "invalid_input"),
            (replace(self.operation, target_user_id=0), "invalid_input"),
            (replace(self.operation, operation_revision="01"), "invalid_input"),
            (replace(self.operation, ticket_digest="not-a-digest"), "invalid_input"),
            (replace(self.operation, device_id=str(uuid4())), "wrong_device"),
            (replace(self.operation, reserved_slot=0), "slot_out_of_range"),
            (replace(self.operation, reserved_slot=300), "slot_out_of_range"),
        ]
        for operation, expected in cases:
            with self.subTest(expected=expected, operation=operation):
                self.assert_error(expected, lambda: self.worker.enroll(operation, self.cancel))
        self.assertEqual(self.sensor.calls, [])

    def test_shared_scan_lock_rejects_enrollment_without_starting_it(self):
        self.lock.acquire()
        try:
            with self.assertRaisesRegex(EnrollmentError, "sensor_busy"):
                self.run_operation()
            self.assertEqual(self.sensor.calls, [])
        finally:
            self.lock.release()

    def test_authorization_or_cancel_before_start_prevents_all_sensor_io(self):
        self.allowed = False
        self.assert_error("not_authorized")
        self.allowed = True
        self.cancel.set()
        self.assert_error("cancelled")
        self.assertEqual(self.sensor.calls, [])

    def test_already_registered_finger_is_not_copied_into_a_new_slot(self):
        self.sensor.search_result = 0
        self.assert_error("already_registered")
        self.assertEqual(self.sensor.stores, [])

    def test_different_fingers_fail_without_store_or_automatic_retry(self):
        self.sensor.model_result = CODES.ENROLLMISMATCH
        self.assert_error("fingerprints_do_not_match")
        self.sensor.calls.clear()
        self.assert_error("fingerprints_do_not_match")
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.slots, {2, 6})

    def test_slot_is_rechecked_after_two_captures(self):
        self.sensor.on_model = lambda: self.sensor.slots.add(12)
        self.assert_error("slot_occupied")
        self.assertEqual(self.sensor.stores, [])

    def test_permission_revocation_before_store_prevents_flash_write(self):
        self.sensor.on_model = lambda: setattr(self, "allowed", False)
        self.assert_error("not_authorized")
        self.assertEqual(self.sensor.stores, [])

    def test_input_timeout_is_bounded_and_never_stores(self):
        self.sensor.images = []
        self.assert_error("timeout")
        self.assertGreaterEqual(self.now, 15)
        self.assertLess(self.now, 16)
        self.assertEqual(self.sensor.stores, [])

    def test_lost_store_reply_keeps_uncertain_slot_and_never_retries_store(self):
        def lost_reply():
            raise RuntimeError("synthetic lost reply")
        self.sensor.on_store = lost_reply
        self.assert_error("recovery_required")
        self.assertIn(12, self.sensor.slots)
        self.sensor.calls.clear()
        self.worker = self.make_worker()
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.stores, [12])

    def test_cancel_after_store_and_unconfirmed_store_require_recovery(self):
        self.sensor.on_store = self.cancel.set
        self.assert_error("recovery_required")
        self.assertIn(12, self.sensor.slots)

    def test_ack_without_slot_confirmation_is_not_reported_as_stored(self):
        self.sensor.persist = False
        self.assert_error("recovery_required")

    def test_local_journal_is_committed_before_store_and_contains_no_raw_subject_or_ticket(self):
        seen = []
        def inspect():
            with closing(sqlite3.connect(self.path)) as db:
                seen.extend(db.execute("SELECT state FROM local_enrollments").fetchall())
        self.sensor.on_store = inspect
        self.run_operation()
        self.assertEqual(seen, [("store_pending",)])
        with closing(sqlite3.connect(self.path)) as db:
            fields = [row[1] for row in db.execute("PRAGMA table_info(local_enrollments)")]
        self.assertNotIn("target_user_id", fields)
        self.assertNotIn("ticket", fields)

    def test_failed_journal_commit_after_store_does_not_repeat_flash_write(self):
        original = self.worker.ledger.transition
        def fail_final(operation, expected, state, **kwargs):
            if state == "sensor_stored":
                raise OSError("synthetic disk failure")
            return original(operation, expected, state, **kwargs)
        with patch.object(self.worker.ledger, "transition", side_effect=fail_final):
            self.assert_error("recovery_required")
        self.worker = self.make_worker()
        self.assert_error("recovery_required")
        self.assertEqual(self.sensor.stores, [12])

    def test_closed_progress_view_does_not_abort_physical_work(self):
        def closed(*_):
            raise RuntimeError("view closed")
        self.worker = self.make_worker(progress=closed)
        self.assertEqual(self.run_operation().state, "sensor_stored")

    def test_broken_authorizer_cannot_open_sensor_or_expose_exception(self):
        def fail(_):
            raise RuntimeError("synthetic private value")
        self.worker.authorize = fail
        self.assert_error("not_authorized")
        self.assertEqual(self.sensor.calls, [])

    def test_cancellation_after_durable_sensor_result_keeps_receipt_without_rescan(self):
        def cancel_after_commit(stage):
            if stage == "sensor_stored_server_pending":
                self.cancel.set()
        self.worker = self.make_worker(progress=cancel_after_commit)
        self.assert_error("cancelled")
        self.cancel.clear()
        self.sensor.calls.clear()
        self.assertEqual(self.run_operation().state, "sensor_stored")
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.stores, [12])

    def test_pre_store_journal_failure_prevents_the_store_command(self):
        original = self.worker.ledger.transition
        def fail_pending(operation, expected, state, **kwargs):
            if state == "store_pending":
                raise OSError("synthetic disk failure")
            return original(operation, expected, state, **kwargs)
        with patch.object(self.worker.ledger, "transition", side_effect=fail_pending):
            self.assert_error("sensor_or_storage_error")
        self.assertEqual(self.sensor.stores, [])


if __name__ == "__main__":
    unittest.main()
