import csv
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock

from pi_api import DisplayCoordinator, FingerprintService, MotionFeed, MotionWorker, create_app


class PiApiTests(unittest.TestCase):
    def setUp(self):
        self.now = 0.0
        self.clock = lambda: self.now
        self.frames = []
        self.display = DisplayCoordinator(lambda lines: self.frames.append(tuple(lines)), self.clock)
        self.codes = SimpleNamespace(OK=0, NOFINGER=2, NOMATCH=8, NOTFOUND=9)
        self.driver = Mock()
        self.driver.get_image.side_effect = [2, 0]
        self.driver.image_2_tz.return_value = 0
        self.driver.finger_fast_search.return_value = 0
        self.driver.finger_id = 5
        self.driver.confidence = 90
        self.fingerprint = FingerprintService(
            self.driver, self.codes, self.display, names={5: "test_user"},
            clock=self.clock, sleep=lambda seconds: None,
        )
        self.feed = MotionFeed(self.clock)
        self.app = create_app(self.fingerprint, self.feed)
        self.client = self.app.test_client()

    def receive(self, value, when):
        self.now = when
        self.feed.receive(str(value))

    def test_health_keeps_original_contract_even_when_mega_is_unavailable(self):
        response = self.client.get("/health")
        self.assertEqual(response.status_code, 200)
        self.assertTrue(response.json["success"])
        self.assertEqual(response.json["message"], "Raspberry Pi connected")
        self.assertFalse(response.json["motion_available"])

    def test_thresholds_release_and_motion_history_are_separate_in_api(self):
        self.feed.connection(True)
        self.receive(186, 0.1)
        self.receive(189, 0.2)
        self.assertFalse(self.client.get("/motion").json["data"]["detected"])
        self.receive(190, 0.3)
        response = self.client.get("/motion")
        self.assertEqual(response.status_code, 200)
        self.assertTrue(response.json["data"]["detected"])
        self.assertEqual(response.json["data"]["raw_value"], 190)
        self.receive(186, 0.4)
        data = self.client.get("/motion").json["data"]
        self.assertFalse(data["detected"])
        self.assertTrue(data["recent_motion"])
        self.assertIsNotNone(data["last_motion_at"])
        self.assertEqual(data["thresholds"], {"detect_at_least": 190, "release_at_most": 186})

    def test_no_data_never_reports_a_clear_or_fresh_value(self):
        self.feed.connection(True)
        self.receive(190, 0.1)
        self.now = 3.1
        response = self.client.get("/motion")
        self.assertEqual(response.status_code, 503)
        self.assertFalse(response.json["success"])
        self.assertEqual(response.json["data"]["state"], "DATA_LOST")
        self.assertIsNone(response.json["data"]["detected"])
        self.assertIsNone(response.json["data"]["raw_value"])
        self.assertEqual(response.headers["Cache-Control"], "no-store")

    def test_reconnect_requires_new_baseline_and_does_not_invent_movement(self):
        self.feed.connection(True)
        self.receive(186, 0.1)
        self.receive(190, 0.2)
        self.feed.connection(False, "MEGA_CONNECTION_ERROR")
        self.assertEqual(self.client.get("/motion").status_code, 503)
        self.feed.connection(True)
        self.receive(186, 0.3)
        data = self.client.get("/motion").json["data"]
        self.assertIsNone(data["last_motion_at"])
        self.assertEqual(data["motion_state"], "READY")

    def test_scan_preserves_success_fields_and_releases_lock(self):
        response = self.client.post("/scan")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json, {"success": True, "finger_id": 5,
                                        "name": "test_user", "confidence": 90})
        self.assertFalse(self.fingerprint.lock.locked())
        self.assertFalse(self.display.scanning)

    def test_scan_busy_does_not_start_a_second_sensor_read(self):
        self.fingerprint.lock.acquire()
        try:
            response = self.client.post("/scan")
            self.assertEqual(response.status_code, 409)
            self.driver.get_image.assert_not_called()
        finally:
            self.fingerprint.lock.release()

    def test_unmatched_fingerprint_keeps_existing_false_200_contract(self):
        self.driver.finger_fast_search.return_value = self.codes.NOTFOUND
        response = self.client.post("/scan")
        self.assertEqual(response.status_code, 200)
        self.assertFalse(response.json["success"])
        self.assertNotIn("finger_id", response.json)

    def test_scan_timeout_and_sensor_error_release_ownership(self):
        self.fingerprint.wait_image = Mock(side_effect=TimeoutError("timeout"))
        self.assertEqual(self.client.post("/scan").status_code, 408)
        self.assertFalse(self.fingerprint.lock.locked())
        self.assertFalse(self.display.scanning)
        self.fingerprint.wait_image = Mock(side_effect=RuntimeError("sensor error"))
        self.assertEqual(self.client.post("/scan").status_code, 500)
        self.assertFalse(self.fingerprint.lock.locked())
        self.assertFalse(self.display.scanning)

    def test_scan_display_priority_does_not_stop_motion_data(self):
        self.feed.connection(True)
        self.receive(186, 0.1)
        self.display.begin_scan()
        self.display.show_scan("scan prompt")
        self.receive(190, 0.2)
        self.assertFalse(self.display.show_motion(self.feed.snapshot()))
        self.assertEqual(self.frames[-1], ("scan prompt",))
        self.assertTrue(self.client.get("/motion").json["data"]["detected"])
        self.display.end_scan()
        self.now = 2
        self.assertFalse(self.display.show_motion(self.feed.snapshot()))
        self.now = 3.3
        self.receive(186, self.now)
        self.assertTrue(self.display.show_motion(self.feed.snapshot()))
        self.assertEqual(self.frames[-1][0], "가림 해제")

    def test_worker_reads_one_usb_stream_and_records_simulation_events(self):
        self.worker = None
        values = iter([b"186\n", b"190\n", b"186\n"])

        class SerialInput:
            def __enter__(inner):
                return inner

            def __exit__(inner, *args):
                return False

            def read_until(inner, expected, size):
                self.now += 0.2
                value = next(values)
                if self.now >= 0.6:
                    self.worker.stopping.set()
                return value

        factory = Mock(return_value=SerialInput())
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "events.csv"
            self.worker = MotionWorker(self.feed, self.display, factory, path)
            self.worker.run()
            factory.assert_called_once_with("/dev/ttyUSB0", 115200, timeout=0.25, exclusive=True)
            with path.open(encoding="utf-8", newline="") as stream:
                rows = list(csv.DictReader(stream))
            events = [row["event"] for row in rows]
            self.assertIn("COVERED", events)
            self.assertIn("CLEAR", events)
            self.assertEqual(events.count("MOTION"), 2)
            self.assertEqual(events[-1], "STOP")
            self.assertTrue(all(row["demo_type"] == "finger_shadow_simulation" for row in rows))
        self.assertEqual(self.feed.snapshot()["state"], "CONNECTION_ERROR")


if __name__ == "__main__":
    unittest.main()
