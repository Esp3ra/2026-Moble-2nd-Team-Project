"""Borrowed fake UART and host lifecycle; readiness callbacks are test doubles only."""
import secrets
import threading
import time
import unittest
from unittest.mock import Mock
from types import SimpleNamespace
from uuid import uuid4

import fingerprint_identity_wire as wire
from identity_runtime import IdentityRuntime
from pi_api import create_app, MotionFeed
import test_fingerprint_identity as fixtures
import test_fingerprint_enrollment_runtime as host_fixtures


class RuntimeTests(unittest.TestCase):
    def setUp(self):
        self.env = fixtures.AdapterTests()
        self.env.setUp()
        self.env.sensor.confidence = 100
        self.feed = MotionFeed(lambda: self.env.now)
        self.app = create_app(self.env.service, self.feed)
        self.body = dict(relay_id=str(uuid4()), request_b64=wire.b64encode(self.env.raw))
        self.headers = {"X-Identity-Relay": wire.b64encode(secrets.token_bytes(32))}
        self.path = "/identity/v2/jobs/" + self.body["relay_id"]

    def runtime(self, ready=None):
        runtime = IdentityRuntime(self.env.service, self.env.profile, ready=ready, clock=lambda: self.env.now)
        self.addCleanup(runtime.close)
        runtime.mount(self.app)
        self.client = self.app.test_client()
        return runtime

    def start(self):
        return self.client.post("/identity/v2/jobs", json=self.body, headers=self.headers)

    def wait(self, predicate):
        deadline = time.monotonic() + 2
        while time.monotonic() < deadline:
            response = self.client.get(self.path, headers=self.headers)
            if predicate(response): return response
            time.sleep(0.005)
        self.fail("Expected synthetic runtime state not reached")

    def finish(self):
        while True:
            response = self.wait(lambda r: r.json["data"]["state"] in ("awaiting_approval", "succeeded", "failed"))
            data = response.json["data"]
            if data["state"] != "awaiting_approval": return data
            raw = self.env.exchange(wire.b64decode(data["probe_b64"]), lambda: False)
            ack = self.client.post(self.path + "/approval", json=dict(probe_id=data["probe_id"], approval_b64=wire.b64encode(raw)), headers=self.headers)
            self.assertEqual(ack.status_code, 200)

    def test_default_readiness_blocks_all_identity_routes_without_sensor_or_old_route_changes(self):
        runtime = self.runtime()
        for method, path in (("POST", "/identity/v2/jobs"), ("GET", self.path),
                             ("POST", self.path + "/approval"), ("POST", self.path + "/cancel"),
                             ("OPTIONS", "/identity/v2/jobs")):
            response = self.client.open(path, method=method, json={}, headers=self.headers)
            self.assertEqual(response.status_code, 503)
            self.assertEqual(response.json, {"success": False, "error": {"code": "IDENTITY_DEVICE_NOT_READY"}})
        self.env.sensor.get_image.assert_not_called()
        self.assertEqual(self.client.get("/health").status_code, 200)
        self.assertEqual(self.client.post("/scan").status_code, 200)
        self.assertFalse(self.env.service.stopping.is_set())
        runtime.close(); runtime.close()

    def test_ready_callback_is_strict_and_failure_permanently_blocks_same_instance(self):
        ready = Mock(return_value=1)
        self.runtime(ready)
        self.assertEqual(self.start().status_code, 503)
        ready.return_value = True
        self.assertEqual(self.start().status_code, 503)
        ready.assert_called_once_with(self.env.profile)
        self.env.sensor.get_image.assert_not_called()

    def test_ready_exception_is_sanitized_and_does_not_touch_sensor(self):
        self.runtime(Mock(side_effect=RuntimeError("PRIVATE_DATA")))
        response = self.start()
        self.assertEqual(response.status_code, 503)
        self.assertNotIn("PRIVATE_DATA", response.get_data(as_text=True))
        self.env.sensor.get_image.assert_not_called()

    def test_test_only_readiness_preserves_single_capture_and_exact_result(self):
        ready = Mock(return_value=True)
        self.runtime(ready)
        self.assertEqual(self.start().status_code, 200)
        final = self.finish()
        self.assertEqual(final["state"], "succeeded")
        self.assertEqual(self.start().json["data"]["result_b64"], final["result_b64"])
        result = wire.verify(wire.b64decode(final["result_b64"]), kind="id_result", device_id=self.env.profile.device_id,
                             key_id=self.env.profile.key_id, key=self.env.keys["id_result"])
        self.assertEqual((result["outcome"], result["slot"]), ("matched", 17))
        self.assertEqual(self.env.sensor.finger_fast_search.call_count, 1)
        self.env.sensor.store_model.assert_not_called()
        self.env.sensor.delete_model.assert_not_called()

    def test_revocation_cancels_pending_job_and_never_reopens_pool(self):
        ready = Mock(return_value=True)
        runtime = self.runtime(ready)
        self.assertEqual(self.start().status_code, 200)
        self.wait(lambda r: r.json["data"]["state"] == "awaiting_approval")
        ready.return_value = False
        self.assertEqual(self.client.get(self.path, headers=self.headers).status_code, 503)
        runtime.close()
        ready.return_value = True
        self.assertEqual(self.start().status_code, 503)
        self.env.sensor.get_image.assert_not_called()
        self.assertFalse(self.env.service.lock.locked())
        self.assertFalse(self.env.service.stopping.is_set())

    def test_close_keeps_other_uart_owner_and_motion_alive(self):
        runtime = self.runtime(lambda profile: True)
        self.env.service.lock.acquire()
        try:
            runtime.close()
            self.assertTrue(self.env.service.lock.locked())
            self.assertFalse(self.env.service.stopping.is_set())
            self.feed.connection(True)
            self.feed.receive("186")
            self.assertEqual(self.client.get("/motion").status_code, 200)
        finally:
            self.env.service.lock.release()

    def test_remount_or_new_app_cannot_reconstruct_an_attempt_pool(self):
        runtime = self.runtime(lambda profile: True)
        with self.assertRaises(RuntimeError): runtime.mount(self.app)
        with self.assertRaises(RuntimeError): runtime.mount(create_app(self.env.service, self.feed))
        self.env.sensor.get_image.assert_not_called()

    def test_clock_profile_and_ram_limit_fail_before_any_route_or_sensor_is_created(self):
        for clock in (lambda: float("nan"), lambda: True, lambda: -1):
            with self.assertRaises(ValueError):
                IdentityRuntime(self.env.service, self.env.profile, clock=clock)
        for maximum in (True, 0, 1025):
            with self.assertRaises(ValueError):
                IdentityRuntime(self.env.service, self.env.profile, clock=lambda: 0, maximum=maximum)
        with self.assertRaises(ValueError):
            IdentityRuntime(self.env.service, None, clock=lambda: 0)
        self.assertEqual(len([r for r in self.app.url_map.iter_rules() if r.rule.startswith("/identity/")]), 0)
        self.env.sensor.get_image.assert_not_called()

    def test_mysql_registration_routes_share_owner_and_survive_identity_block_and_close(self):
        from fingerprint_enrollment import SensorProfile
        from fingerprint_enrollment_runtime import MySqlEnrollmentRuntime
        from fingerprint_enrollment_wire import DirectionKeys
        keys = DirectionKeys(self.env.profile.device_id, "synthetic-enroll", "1",
                             secrets.token_bytes(32), secrets.token_bytes(32))
        enrollment = MySqlEnrollmentRuntime(self.env.service, keys=keys, profile=SensorProfile(300, 1, 299))
        self.addCleanup(enrollment.close)
        self.app = create_app(self.env.service, self.feed, enrollment.jobs)
        runtime = self.runtime()
        self.assertIs(enrollment.adapter.sensor_lock.service, self.env.service)
        self.assertIs(enrollment.adapter.sensor, self.env.service.finger)
        self.assertEqual(self.client.open("/enrollments/jobs", method="OPTIONS").status_code, 200)
        self.assertEqual(self.start().status_code, 503)
        runtime.close()
        self.assertEqual(self.client.open("/enrollments/jobs", method="OPTIONS").status_code, 200)
        self.env.sensor.get_image.assert_not_called()
        self.assertFalse(self.env.service.stopping.is_set())


class HostTests(unittest.TestCase):
    def setUp(self):
        # Reuse synthetic hardware setup, without inheriting/collecting its tests.
        self.fixture = host_fixtures.MainCompositionTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.events = self.fixture.events
        self.actual_service = None

    def identity(self, service):
        self.actual_service = service
        def mount(app):
            self.assertIs(app, self.fixture.app)
            self.assertIs(service, self.fixture.service)
            self.events.append("identity_mount")
        def close():
            self.assertTrue(service.stopping.is_set())
            self.events.append("identity_close")
        return SimpleNamespace(mount=mount, close=close)

    def test_host_uses_same_uart_and_closes_both_before_uart(self):
        import pi_api
        pi_api.main(enrollment_factory=self.fixture.factory, identity_factory=self.identity)
        self.assertEqual(self.events, ["identity_mount", "identity_close", "enrollment_close", "motion_stop", "uart_close"])

    def test_identity_factory_failure_closes_existing_enrollment_without_starting_server(self):
        import pi_api
        def reject(service): raise ValueError("synthetic identity rejection")
        with self.assertRaises(ValueError):
            pi_api.main(enrollment_factory=self.fixture.factory, identity_factory=reject)
        self.fixture.app.run.assert_not_called()
        self.assertEqual(self.events, ["enrollment_close", "uart_close"])

    def test_mount_failure_still_closes_both_runtimes(self):
        import pi_api
        def factory(service):
            runtime = self.identity(service)
            runtime.mount = Mock(side_effect=ValueError("synthetic mount rejection"))
            return runtime
        with self.assertRaises(ValueError):
            pi_api.main(enrollment_factory=self.fixture.factory, identity_factory=factory)
        self.fixture.app.run.assert_not_called()
        self.assertEqual(self.events, ["identity_close", "enrollment_close", "uart_close"])

    def test_identity_close_failure_does_not_skip_enrollment_motion_or_uart_cleanup(self):
        import pi_api
        def factory(service):
            runtime = self.identity(service)
            runtime.close = Mock(side_effect=RuntimeError("synthetic cleanup failure"))
            return runtime
        with self.assertRaises(RuntimeError):
            pi_api.main(enrollment_factory=self.fixture.factory, identity_factory=factory)
        self.assertEqual(self.events, ["identity_mount", "enrollment_close", "motion_stop", "uart_close"])

    def test_shutdown_does_not_close_uart_until_active_command_releases_shared_lock(self):
        import pi_api
        closing, errors = threading.Event(), []
        def factory(service):
            runtime = self.identity(service)
            original = runtime.close
            def close():
                original()
                closing.set()
            runtime.close = close
            return runtime
        def serve(**kwargs): self.actual_service.lock.acquire()
        self.fixture.app.run.side_effect = serve
        def host():
            try:
                pi_api.main(enrollment_factory=self.fixture.factory, identity_factory=factory)
            except BaseException as error: errors.append(error)
        thread = threading.Thread(target=host, daemon=True)
        thread.start()
        try:
            self.assertTrue(closing.wait(2))
            self.assertNotIn("uart_close", self.events)
            self.assertTrue(thread.is_alive())
        finally:
            if self.actual_service is not None and self.actual_service.lock.locked():
                self.actual_service.lock.release()
            thread.join(2)
        self.assertFalse(thread.is_alive())
        self.assertEqual(errors, [])
        self.assertEqual(self.events[-1], "uart_close")


if __name__ == "__main__":
    unittest.main()
