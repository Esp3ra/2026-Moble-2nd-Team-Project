import unittest

from pi_api import MotionTracker


class MotionTests(unittest.TestCase):
    def test_initial_measurement_does_not_claim_motion(self):
        tracker = MotionTracker(0)
        self.assertFalse(tracker.receive("190", 1))
        self.assertEqual(tracker.status(1), "READY")

    def test_hysteresis_and_bidirectional_motion(self):
        tracker = MotionTracker(0)
        tracker.receive("186", 0)
        self.assertFalse(tracker.receive("187", 0.2))
        self.assertTrue(tracker.receive("190", 0.4))
        self.assertFalse(tracker.receive("187", 0.6))
        self.assertFalse(tracker.receive("190", 0.8))
        self.assertTrue(tracker.receive("186", 1))
        self.assertEqual(tracker.last_motion, 1)

    def test_static_level_does_not_refresh_motion(self):
        tracker = MotionTracker(0)
        tracker.receive("160", 0)
        tracker.receive("190", 1)
        for moment in range(2, 12):
            tracker.receive("190", moment)
        self.assertEqual(tracker.status(11), "NO_RECENT_MOTION")

    def test_lost_data_is_not_inactivity_and_recovery_sets_baseline(self):
        tracker = MotionTracker(0)
        tracker.receive("160", 0)
        tracker.receive("190", 1)
        self.assertEqual(tracker.status(4), "DATA_LOST")
        self.assertFalse(tracker.receive("160", 5))
        self.assertEqual(tracker.status(5), "READY")
        self.assertIsNone(tracker.last_motion)

    def test_bad_input_does_not_keep_connection_alive(self):
        tracker = MotionTracker(0)
        tracker.receive("160", 0)
        for text in ["garbage", "1024", "-1", "", "188.5"]:
            self.assertFalse(tracker.receive(text, 4))
        self.assertEqual(tracker.last_data, 0)
        self.assertEqual(tracker.status(4), "DATA_LOST")

    def test_start_without_data_has_a_distinct_timeout(self):
        tracker = MotionTracker(10)
        self.assertEqual(tracker.status(11), "WAITING")
        self.assertEqual(tracker.status(13), "DATA_LOST")

    def test_release_is_immediate_while_motion_history_remains_recent(self):
        tracker = MotionTracker(0)
        tracker.receive("180", 0)
        tracker.receive("190", 1)
        self.assertEqual(tracker.current_state(1), "COVERED")
        tracker.receive("187", 1.2)
        self.assertEqual(tracker.current_state(1.2), "COVERED")
        tracker.receive("186", 1.4)
        self.assertEqual(tracker.current_state(1.4), "CLEAR")
        self.assertEqual(tracker.status(1.4), "RECENT_MOTION")

    def test_lost_input_is_neither_covered_nor_clear(self):
        tracker = MotionTracker(0)
        tracker.receive("190", 0)
        self.assertEqual(tracker.current_state(3), "DATA_LOST")


    def test_detection_threshold_190_and_release_threshold_186(self):
        tracker = MotionTracker(0)
        tracker.receive("186", 0)
        for moment, value in enumerate((187, 188, 189), start=1):
            self.assertFalse(tracker.receive(str(value), moment))
            self.assertEqual(tracker.current_state(moment), "CLEAR")
        self.assertTrue(tracker.receive("190", 4))
        self.assertEqual(tracker.current_state(4), "COVERED")
        for moment, value in enumerate((189, 188, 187), start=5):
            self.assertFalse(tracker.receive(str(value), moment))
            self.assertEqual(tracker.current_state(moment), "COVERED")
        self.assertTrue(tracker.receive("186", 8))
        self.assertEqual(tracker.current_state(8), "CLEAR")


if __name__ == "__main__":
    unittest.main()
