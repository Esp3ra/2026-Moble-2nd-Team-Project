import json
import threading
import time
import tkinter as tk
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from unittest.mock import patch

from device_check import (DeviceCheck, MotionView, api_request, motion_view,
                          normalize_url, scan_message)


def motion_body(**changes):
    data = dict(available=True, connected=True, raw_value=169, state="CLEAR",
                detected=False, recent_motion=True, seconds_since_motion=4.7,
                observed_at="2026-10-19T09:58:53+09:00", simulation=True,
                thresholds={"detect_at_least": 190, "release_at_most": 186})
    data.update(changes)
    return {"success": True, "data": data}


class ContractTests(unittest.TestCase):
    def test_current_clear_and_recent_change_are_distinct(self):
        view = motion_view(200, motion_body())
        self.assertEqual(view.raw, "169")
        self.assertEqual(view.cover, "가림 해제")
        self.assertEqual(view.recent, "최근 변화 있음")
        self.assertEqual(view.changed, "4.7초 전")

    def test_lost_sensor_never_displays_old_value_or_clear(self):
        body = motion_body(available=False, state="DATA_LOST", raw_value=169)
        body["success"] = False
        view = motion_view(503, body)
        self.assertEqual(view.raw, "—")
        self.assertEqual(view.cover, "확인 불가")
        self.assertEqual(view.recent, "확인 불가")

    def test_initial_hysteresis_state_remains_unknown(self):
        view = motion_view(200, motion_body(raw_value=188, state="LEVEL_UNKNOWN",
                                           detected=None, recent_motion=None,
                                           seconds_since_motion=None))
        self.assertEqual(view.cover, "초기 판정 대기")
        self.assertEqual(view.recent, "변화 기록 없음")

    def test_malformed_success_is_rejected(self):
        for changes in ({"raw_value": True}, {"raw_value": 1024},
                        {"state": "COVERED", "detected": False},
                        {"recent_motion": "false"}):
            with self.subTest(changes=changes), self.assertRaises(RuntimeError):
                motion_view(200, motion_body(**changes))

    def test_scan_success_is_not_account_verification(self):
        message = scan_message(200, {"success": True, "finger_id": 5,
                                     "name": "테스트", "confidence": 275})
        self.assertIn("센서 인식 성공", message)
        self.assertIn("회원 계정 일치 확인 전", message)
        self.assertNotIn("로그인 성공", message)

    def test_unmatched_200_and_error_statuses_are_not_success(self):
        for status, body, expected in (
                (200, {"success": False}, "인식 실패"),
                (409, {}, "진행 중"), (408, {}, "시간이 초과"),
                (500, {}, "센서 처리 오류")):
            with self.subTest(status=status):
                self.assertIn(expected, scan_message(status, body))

    def test_server_root_validation(self):
        self.assertEqual(normalize_url(" http://192.0.2.10:8080/ "),
                         "http://192.0.2.10:8080")
        for value in ("192.0.2.10", "http://user:pw@host", "http://host/scan",
                      "http://host?token=secret", "http://host:99999", "file:///test"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                normalize_url(value)


class HttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                self.send_response(503 if self.path == "/motion" else 200)
                self.end_headers()
                self.wfile.write(b'not json' if self.path == "/bad" else
                                 json.dumps({"success": False, "data": {}}).encode())

            def do_POST(self):
                self.send_response(409)
                self.end_headers()
                self.wfile.write(b'{"success":false}')

            def log_message(self, *args):
                pass
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        cls.base = f"http://127.0.0.1:{cls.server.server_port}"
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join(2)

    def test_http_error_json_is_read(self):
        status, body = api_request(self.base, "/motion")
        self.assertEqual(status, 503)
        self.assertFalse(body["success"])
        status, body = api_request(self.base, "/scan", method="POST")
        self.assertEqual(status, 409)

    def test_non_json_is_reported(self):
        with self.assertRaisesRegex(RuntimeError, "JSON"):
            api_request(self.base, "/bad")


class WindowTests(unittest.TestCase):
    def setUp(self):
        self.root = tk.Tk()
        self.root.withdraw()
        self.ui = DeviceCheck(self.root)

    def tearDown(self):
        self.ui.close()

    def event(self, kind, value):
        self.ui.apply_event((self.ui.generation, kind, value, time.monotonic()))

    def test_old_connection_response_is_ignored(self):
        self.ui.generation = 2
        self.ui.apply_event((1, "motion", motion_view(200, motion_body()), time.monotonic()))
        self.assertEqual(self.ui.fields["raw"].get(), "—")

    def test_offline_clears_display_and_scan_completion_does_not_reenable(self):
        self.event("motion", motion_view(200, motion_body()))
        self.assertEqual(self.ui.fields["raw"].get(), "169")
        self.event("offline", "연결 실패")
        self.assertEqual(self.ui.fields["raw"].get(), "—")
        self.event("scan", "스캔 완료")
        self.assertTrue(self.ui.scan_button.instate(["disabled"]))

    def test_scan_busy_does_not_stop_motion_updates(self):
        self.ui.scanning = True
        self.ui.scan_button.configure(state="disabled")
        self.event("motion", motion_view(200, motion_body(raw_value=196, state="COVERED", detected=True)))
        self.assertEqual(self.ui.fields["raw"].get(), "196")
        self.assertTrue(self.ui.scan_button.instate(["disabled"]))

    def test_stalled_poll_hides_old_readings(self):
        self.event("motion", motion_view(200, motion_body()))
        self.ui.last_poll = time.monotonic() - 6
        self.ui.drain()
        self.assertEqual(self.ui.fields["raw"].get(), "—")
        self.assertFalse(self.ui.reachable)

    def test_poll_and_scan_success_via_ui_workers(self):
        def fake_api(base, path, **kwargs):
            if path == "/health":
                return 200, {"success": True}
            if path == "/motion":
                return 200, motion_body()
            return 200, {"success": True, "finger_id": 5, "name": "테스트", "confidence": 275}
        with patch("device_check.api_request", side_effect=fake_api):
            self.ui.connect()
            self.wait_until(lambda: self.ui.fields["raw"].get() == "169")
            self.ui.scan()
            self.wait_until(lambda: not self.ui.scanning)
        self.assertIn("센서 인식 성공", self.ui.scan_result.get())

    def wait_until(self, condition):
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            self.root.update()
            if condition():
                return
            time.sleep(0.02)
        self.fail("UI worker result did not arrive")


if __name__ == "__main__":
    unittest.main()
