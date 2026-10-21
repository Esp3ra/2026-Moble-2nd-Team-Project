"""Single-owner composition tests; all hardware modules/sensors are synthetic."""

import secrets
import sys
import threading
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import Mock, patch
from uuid import uuid4

import pi_api
import test_fingerprint_enrollment_adapter as fixtures
from test_fingerprint_enrollment import CODES
from test_fingerprint_enrollment_online import FakeApprovalRelay
from fingerprint_enrollment import EnrollmentError, SensorProfile
from fingerprint_enrollment_runtime import EnrollmentRuntime
from fingerprint_enrollment_wire import WireError, encode64


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        self.fx = fixtures.AdapterTests("test_verified_ticket_reaches_two_capture_core_and_signed_minimal_result")
        self.fx.setUp()
        self.addCleanup(self.fx.doCleanups)
        self.frames = []
        self.display = pi_api.DisplayCoordinator(self.frames.append, clock=lambda: self.fx.monotonic)
        self.service = pi_api.FingerprintService(
            self.fx.sensor, CODES, self.display, clock=lambda: self.fx.monotonic, sleep=self.fx.sleep)
        self.server = FakeApprovalRelay()
        self.runtime = EnrollmentRuntime(
            self.service, keys=fixtures.KEYS, profile=SensorProfile(300, 1, 299),
            ledger=self.fx.ledger, cache=self.fx.cache, approval_clock=self.server.clock)
        self.addCleanup(self.runtime.close)
        self.feed = pi_api.MotionFeed(clock=lambda: self.fx.monotonic)
        self.client = pi_api.create_app(self.service, self.feed, self.runtime.jobs).test_client()

    def enroll(self):
        return self.runtime.adapter.enroll_online(
            fixtures.request_bytes(), self.fx.cancel, exchange=self.server.exchange,
            approval_clock=self.server.clock)

    def test_composition_borrows_existing_hardware_and_has_no_initial_io(self):
        self.assertIs(self.runtime.adapter.sensor, self.service.finger)
        self.assertIs(self.runtime.adapter.codes, self.service.codes)
        self.assertIs(self.runtime.adapter.sensor_lock.service.lock, self.service.lock)
        self.assertIs(self.runtime.adapter.keys, fixtures.KEYS)
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertEqual(self.frames, [])

    def test_registration_owns_display_but_motion_continues_and_scan_returns_busy(self):
        def during_model():
            self.assertTrue(self.display.scanning)
            self.assertEqual(self.client.post("/scan").status_code, 409)
            self.feed.connection(True)
            self.feed.receive("186")
            self.fx.monotonic += 0.1
            self.feed.receive("195")
            response = self.client.get("/motion")
            self.assertEqual(response.status_code, 200)
            self.assertTrue(response.json["data"]["detected"])
            self.assertFalse(self.display.show_motion(response.json["data"]))
        self.fx.sensor.on_model = during_model
        self.enroll()
        self.assertEqual(self.fx.sensor.stores, [7])
        self.assertEqual(self.frames[-1], ("지문 저장됨", "계정 연결 확인 중"))
        self.assertFalse(any("등록 완료" in line for frame in self.frames for line in frame))
        self.assertFalse(self.display.scanning)
        self.assertFalse(self.service.lock.locked())
        self.assertFalse(self.display.show_motion(self.feed.snapshot()))
        self.fx.monotonic += 3.1
        self.assertTrue(self.display.show_motion(self.feed.snapshot()))

    def test_scan_lock_owner_is_not_released_or_display_overwritten_by_busy_enrollment(self):
        self.service.lock.acquire()
        self.addCleanup(self.service.lock.release)
        self.display.begin_scan()
        self.display.show_scan("인증 중")
        with self.assertRaises(EnrollmentError) as caught:
            self.enroll()
        self.assertEqual(caught.exception.code, "sensor_busy")
        self.assertTrue(self.service.lock.locked())
        self.assertTrue(self.display.scanning)
        self.assertEqual(self.frames, [("인증 중",)])
        self.assertEqual(self.fx.sensor.calls, [])

    def test_result_read_never_takes_display_or_calls_sensor(self):
        raw = self.enroll()
        self.frames.clear()
        self.fx.sensor.calls.clear()
        result = self.runtime.adapter.read_result_online(
            fixtures.REQUEST_PAYLOAD["enrollment_id"], self.fx.cancel,
            exchange=self.server.exchange, approval_clock=self.server.clock)
        self.assertEqual(result, raw)
        self.assertEqual(self.frames, [])
        self.assertEqual(self.fx.sensor.calls, [])

    def test_model_failure_releases_display_and_shared_lock_without_storing(self):
        self.fx.sensor.model_result = CODES.ENROLLMISMATCH
        with self.assertRaises(EnrollmentError):
            self.enroll()
        self.assertEqual(self.fx.sensor.stores, [])
        self.assertFalse(self.display.scanning)
        self.assertFalse(self.service.lock.locked())

    def test_duplicate_finger_displays_reason_and_never_restarts_or_stores(self):
        self.fx.sensor.search_result = CODES.OK
        with self.assertRaises(EnrollmentError) as caught:
            self.enroll()
        self.assertEqual(caught.exception.code, "already_registered")
        self.assertEqual(self.frames[-1], ("이미 등록된 지문", "등록을 중단합니다", "PC 안내 확인"))
        self.assertEqual(self.fx.sensor.stores, [])
        self.assertFalse(self.display.scanning)
        self.assertFalse(self.service.lock.locked())
        self.fx.sensor.calls.clear()
        with self.assertRaises(EnrollmentError):
            self.enroll()
        self.assertEqual(self.fx.sensor.calls, [])

    def test_oled_failures_never_repeat_store_or_leak_shared_lock(self):
        self.service.display = SimpleNamespace(
            begin_scan=Mock(side_effect=OSError("synthetic display unavailable")),
            show_scan=Mock(side_effect=OSError("synthetic display unavailable")),
            end_scan=Mock(side_effect=OSError("synthetic display unavailable")))
        self.enroll()
        self.assertEqual(self.fx.sensor.stores, [7])
        self.assertFalse(self.service.lock.locked())
        self.service.display.end_scan.assert_called_once()

    def test_stopping_host_never_allows_new_sensor_io(self):
        self.service.stopping.set()
        with self.assertRaises(EnrollmentError):
            self.enroll()
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertEqual(self.frames, [])

    def test_runtime_close_cancels_waiter_and_blocks_jobs_without_stopping_scan_owner(self):
        body = dict(relay_id=str(uuid4()), enrollment_id=fixtures.REQUEST_PAYLOAD["enrollment_id"],
                    mode="enroll", request_envelope=encode64(fixtures.request_bytes()))
        capability = encode64(secrets.token_bytes(32))
        self.runtime.jobs.start(body, capability)
        self.runtime.close()
        self.runtime.close()
        with self.assertRaises(WireError):
            self.runtime.jobs.start(dict(body, relay_id=str(uuid4())), capability)
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertFalse(self.service.stopping.is_set())
        self.assertFalse(self.service.lock.locked())
        self.fx.sensor.images = [CODES.NOFINGER, CODES.OK]
        self.fx.sensor.finger_fast_search = lambda: CODES.OK
        self.fx.sensor.finger_id, self.fx.sensor.confidence = 7, 50
        self.assertEqual(self.client.post("/scan").status_code, 200)


class MainCompositionTests(unittest.TestCase):
    def setUp(self):
        self.events = []
        self.uart = SimpleNamespace(close=lambda: self.events.append("uart_close"))
        serial = SimpleNamespace(Serial=Mock(return_value=self.uart))
        modules = {name: ModuleType(name) for name in (
            "serial", "adafruit_fingerprint", "PIL", "luma", "luma.core",
            "luma.core.interface", "luma.core.interface.serial", "luma.core.render",
            "luma.oled", "luma.oled.device")}
        modules["serial"].Serial = serial.Serial
        modules["adafruit_fingerprint"].Adafruit_Fingerprint = lambda uart: object()
        modules["PIL"].ImageFont = SimpleNamespace(truetype=lambda *args: object())
        modules["luma.core.interface.serial"].i2c = lambda **kwargs: object()
        modules["luma.core.render"].canvas = Mock()
        modules["luma.oled.device"].sh1106 = lambda *args, **kwargs: object()
        self.addCleanup(patch.stopall)
        patch.dict(sys.modules, modules).start()
        patch.object(pi_api, "load_user_names", return_value={}).start()
        patch.object(pi_api.DisplayCoordinator, "show_scan").start()
        self.worker = SimpleNamespace(thread=SimpleNamespace(ident=None),
                                      start=lambda: setattr(self.worker.thread, "ident", 1),
                                      stop=lambda: self.events.append("motion_stop"))
        patch.object(pi_api, "MotionWorker", return_value=self.worker).start()
        self.app = Mock()
        self.make_app = patch.object(pi_api, "create_app", return_value=self.app).start()
        self.jobs = object()
        self.service = None

    def factory(self, service):
        self.service = service
        def close():
            self.assertTrue(service.stopping.is_set())
            self.events.append("enrollment_close")
        return SimpleNamespace(jobs=self.jobs, close=close)

    def test_default_main_keeps_enrollment_disabled(self):
        pi_api.main()
        self.assertIsNone(self.make_app.call_args.args[2])
        self.assertEqual(self.events, ["motion_stop", "uart_close"])

    def test_explicit_factory_gets_actual_service_and_shutdown_drains_its_lock(self):
        closing, errors = threading.Event(), []
        def factory(service):
            runtime = self.factory(service)
            original_close = runtime.close
            def close():
                original_close()
                closing.set()
            runtime.close = close
            return runtime
        def run(**kwargs):
            self.assertIs(self.make_app.call_args.args[0], self.service)
            self.assertIs(self.make_app.call_args.args[2], self.jobs)
            self.service.lock.acquire()
        def host():
            try:
                pi_api.main(enrollment_factory=factory)
            except BaseException as error:
                errors.append(error)
        self.app.run.side_effect = run
        thread = threading.Thread(target=host, daemon=True)
        thread.start()
        try:
            self.assertTrue(closing.wait(timeout=2))
            # Host is shutting down while a simulated UART command holds the
            # shared lock. It must wait here instead of closing UART early.
            self.assertNotIn("uart_close", self.events)
            self.assertTrue(thread.is_alive())
        finally:
            if self.service is not None and self.service.lock.locked():
                self.service.lock.release()
            thread.join(timeout=2)
        self.assertFalse(thread.is_alive())
        self.assertEqual(errors, [])
        self.assertEqual(self.events, ["enrollment_close", "motion_stop", "uart_close"])

    def test_factory_failure_cleans_uart_without_starting_server(self):
        def reject(service):
            raise ValueError("synthetic configuration failure")
        with self.assertRaises(ValueError):
            pi_api.main(enrollment_factory=reject)
        self.app.run.assert_not_called()
        self.assertEqual(self.events, ["uart_close"])

    def test_server_failure_still_closes_enrollment_before_uart(self):
        self.app.run.side_effect = OSError("synthetic bind failure")
        with self.assertRaises(OSError):
            pi_api.main(enrollment_factory=self.factory)
        self.assertEqual(self.events, ["enrollment_close", "motion_stop", "uart_close"])

    def test_motion_stop_failure_still_closes_uart(self):
        self.worker.stop = Mock(side_effect=RuntimeError("synthetic stop failure"))
        with self.assertRaises(RuntimeError):
            pi_api.main(enrollment_factory=self.factory)
        self.assertEqual(self.events, ["enrollment_close", "uart_close"])

    def test_enrollment_close_failure_still_stops_motion_and_drains_uart(self):
        def factory(service):
            runtime = self.factory(service)
            runtime.close = Mock(side_effect=RuntimeError("synthetic close failure"))
            return runtime
        with self.assertRaises(RuntimeError):
            pi_api.main(enrollment_factory=factory)
        self.assertEqual(self.events, ["motion_stop", "uart_close"])


if __name__ == "__main__":
    unittest.main()
