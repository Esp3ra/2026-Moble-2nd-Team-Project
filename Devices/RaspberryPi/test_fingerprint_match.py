import math
import threading
import unittest
from types import SimpleNamespace
from unittest.mock import Mock

from fingerprint_match import compare_fingerprint
from pi_api import DisplayCoordinator, FingerprintService, MotionFeed, create_app


class SensorComparisonTests(unittest.TestCase):
    def setUp(self):
        self.now = 0.0
        self.clock = lambda: self.now
        self.frames = []
        self.display = DisplayCoordinator(lambda lines: self.frames.append(tuple(lines)), self.clock)
        self.codes = SimpleNamespace(OK=0, NOFINGER=2, NOMATCH=8, NOTFOUND=9)
        self.sensor = Mock()
        self.sensor.get_image.side_effect = [2, 0]
        self.sensor.image_2_tz.return_value = 0
        self.sensor.finger_fast_search.return_value = 0
        self.sensor.finger_id = 7
        self.service = FingerprintService(self.sensor, self.codes, self.display,
                                          clock=self.clock, sleep=self.advance)

    def advance(self, seconds):
        self.now += seconds

    def compare(self, **kwargs):
        return compare_fingerprint(self.service, 7, **kwargs)

    def assert_released(self):
        self.assertFalse(self.service.lock.locked())
        self.assertFalse(self.display.scanning)

    def test_expected_slot_matches_without_account_or_confidence_lookup(self):
        self.sensor.confidence = object()
        result = self.compare()
        self.assertEqual((result.outcome, result.slot), ("matched", 7))
        self.assertTrue(any("서버 확인 필요" in frame for frame in self.frames))
        self.assertFalse(any("인증 성공" in frame for frame in self.frames))
        self.assert_released()

    def test_different_registered_finger_is_not_a_match_and_hides_its_slot(self):
        self.sensor.finger_id = 8
        result = self.compare()
        self.assertEqual((result.outcome, result.slot), ("different_finger", None))
        self.assert_released()

    def test_unregistered_finger_codes(self):
        for code in (self.codes.NOTFOUND, self.codes.NOMATCH):
            with self.subTest(code=code):
                self.sensor.get_image.side_effect = [2, 0]
                self.sensor.finger_fast_search.return_value = code
                self.sensor.finger_id = 7  # Stale previous match must be ignored.
                self.assertEqual(self.compare().outcome, "unregistered")
                self.assert_released()

    def test_existing_sensor_owner_is_never_interrupted(self):
        self.service.lock.acquire()
        try:
            self.assertEqual(self.compare().outcome, "busy")
            self.sensor.get_image.assert_not_called()
            self.assertTrue(self.service.lock.locked())
            self.assertEqual(self.frames, [])
        finally:
            self.service.lock.release()

    def test_shutdown_or_cancel_before_capture_has_no_sensor_io(self):
        self.service.stopping.set()
        self.assertEqual(self.compare().outcome, "cancelled")
        self.service.stopping.clear()
        self.assertEqual(self.compare(cancelled=lambda: True).outcome, "cancelled")
        self.sensor.get_image.assert_not_called()
        self.assert_released()

    def test_old_finger_must_be_removed_before_new_capture(self):
        self.sensor.get_image.side_effect = [0, 0, 2, 2, 0]
        self.assertEqual(self.compare().outcome, "matched")
        self.assertEqual(self.sensor.get_image.call_count, 5)
        self.assertAlmostEqual(self.now, 0.6)

    def test_single_budget_includes_removal_and_placement(self):
        self.sensor.get_image.side_effect = None
        self.sensor.get_image.return_value = 0
        self.assertEqual(self.compare(timeout_seconds=0.3).outcome, "timeout")
        self.sensor.image_2_tz.assert_not_called()
        self.assertAlmostEqual(self.now, 0.3)
        self.assert_released()

    def test_absent_finger_times_out_without_searching(self):
        self.sensor.get_image.side_effect = None
        self.sensor.get_image.return_value = 2
        self.assertEqual(self.compare(timeout_seconds=0.3).outcome, "timeout")
        self.sensor.finger_fast_search.assert_not_called()
        self.assert_released()

    def test_cancel_during_wait_blocks_feature_extraction(self):
        self.sensor.get_image.side_effect = None
        self.sensor.get_image.return_value = 0
        self.assertEqual(self.compare(cancelled=lambda: self.now >= 0.2).outcome, "cancelled")
        self.sensor.image_2_tz.assert_not_called()
        self.assert_released()

    def test_late_success_after_cancel_or_deadline_is_not_returned(self):
        for reason in ("timeout", "cancelled"):
            with self.subTest(reason=reason):
                self.now = 0
                self.service.stopping.clear()
                self.sensor.get_image.side_effect = [2, 0]
                def delayed_search():
                    if reason == "timeout":
                        self.now = 15
                    else:
                        self.service.stopping.set()
                    return 0
                self.sensor.finger_fast_search.side_effect = delayed_search
                self.assertEqual(self.compare().outcome, reason)
                self.assert_released()

    def test_image_feature_search_and_driver_errors_are_sanitized(self):
        for stage in ("image", "feature", "search", "exception"):
            with self.subTest(stage=stage):
                self.sensor.get_image.side_effect = [2, 0]
                self.sensor.image_2_tz.return_value = 0
                self.sensor.finger_fast_search.return_value = 0
                if stage == "image":
                    self.sensor.get_image.side_effect = [1]
                if stage == "feature":
                    self.sensor.image_2_tz.return_value = 6
                if stage == "search":
                    self.sensor.finger_fast_search.return_value = 1
                if stage == "exception":
                    self.sensor.get_image.side_effect = RuntimeError("private-driver-details")
                result = self.compare()
                self.assertEqual((result.outcome, result.slot), ("sensor_error", None))
                self.assertNotIn("private", repr(result))
                self.assert_released()

    def test_invalid_requested_slots_are_refused_before_io(self):
        for value in (True, False, "7", 7.0, -1, 65536, None):
            with self.subTest(value=value), self.assertRaises(ValueError):
                compare_fingerprint(self.service, value)
        self.sensor.get_image.assert_not_called()

    def test_invalid_budgets_are_refused_before_io(self):
        for value in (True, 0, -1, math.nan, math.inf, "15", None):
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.compare(timeout_seconds=value)
        self.sensor.get_image.assert_not_called()

    def test_boolean_sensor_codes_or_invalid_slot_never_match(self):
        for field, value in (("image_2_tz", False), ("finger_fast_search", False),
                             ("finger_id", True), ("finger_id", "7"), ("finger_id", -1)):
            with self.subTest(field=field):
                self.sensor.get_image.side_effect = [2, 0]
                self.sensor.image_2_tz.return_value = 0
                self.sensor.finger_fast_search.return_value = 0
                self.sensor.finger_id = 7
                if field == "finger_id":
                    self.sensor.finger_id = value
                else:
                    getattr(self.sensor, field).return_value = value
                self.assertEqual(self.compare().outcome, "sensor_error")
                self.assert_released()

    def test_display_failure_does_not_rescan_or_leak_lock(self):
        self.service.display = Mock()
        self.service.display.begin_scan.side_effect = RuntimeError("OLED")
        self.service.display.show_scan.side_effect = RuntimeError("OLED")
        self.service.display.end_scan.side_effect = RuntimeError("OLED")
        self.assertEqual(self.compare().outcome, "matched")
        self.assertEqual(self.sensor.finger_fast_search.call_count, 1)
        self.assertFalse(self.service.lock.locked())

    def test_no_template_write_delete_or_biometric_transfer(self):
        self.assertEqual(self.compare().outcome, "matched")
        self.assertEqual([call[0] for call in self.sensor.method_calls],
                         ["get_image", "get_image", "image_2_tz", "finger_fast_search"])

    def test_core_does_not_install_any_identity_http_endpoint(self):
        app = create_app(self.service, MotionFeed(self.clock))
        self.assertEqual({r.rule for r in app.url_map.iter_rules()},
                         {"/static/<path:filename>", "/health", "/scan", "/motion"})
        self.sensor.get_image.assert_not_called()

    def test_scan_is_busy_while_comparison_owns_the_same_sensor(self):
        nested = []
        def search():
            nested.append(self.service.scan())
            return 0
        self.sensor.finger_fast_search.side_effect = search
        self.assertEqual(self.compare().outcome, "matched")
        self.assertEqual(nested[0][1], 409)
        self.assertFalse(nested[0][0]["success"])
        self.assertEqual(self.sensor.get_image.call_count, 2)
        self.assert_released()

    def test_motion_feed_continues_without_overwriting_comparison_prompt(self):
        feed = MotionFeed(self.clock)
        feed.connection(True)
        feed.receive("180")
        observed = []
        def search():
            self.advance(0.2)
            feed.receive("195")
            observed.append((feed.snapshot()["detected"], self.display.show_motion(feed.snapshot())))
            return 0
        self.sensor.finger_fast_search.side_effect = search
        self.assertEqual(self.compare().outcome, "matched")
        self.assertEqual(observed, [(True, False)])
        self.assert_released()

    def test_cancellation_after_feature_extraction_never_searches(self):
        def extract(_):
            self.service.stopping.set()
            return 0
        self.sensor.image_2_tz.side_effect = extract
        self.assertEqual(self.compare().outcome, "cancelled")
        self.sensor.finger_fast_search.assert_not_called()
        self.assert_released()


if __name__ == "__main__":
    unittest.main()
