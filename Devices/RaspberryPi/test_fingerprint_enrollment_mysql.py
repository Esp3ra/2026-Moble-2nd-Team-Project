"""PR55-shaped synthetic server, signed permits and fake sensor. No database."""

from datetime import timedelta
import json
import subprocess
import sys
import threading
import unittest
from unittest.mock import patch

from fingerprint_enrollment import EnrollmentError, SensorProfile
from fingerprint_enrollment_mysql import CONTRACT, MySqlEnrollmentAdapter
from fingerprint_enrollment_wire import (
    COMMON, RESULT, WireError, decode64, encode64, parse_json, verify, verify_result_binding,
)
from test_fingerprint_enrollment import CODES, FakeSensor
from test_fingerprint_enrollment_adapter import (
    KEYS, REQUEST_PAYLOAD, EXPECTED_DIGEST, compact, request_bytes,
)
from test_fingerprint_enrollment_online import FakeApprovalRelay, signed_approval


class MySqlContractServer(FakeApprovalRelay):
    """A test double, not a MySQL durability claim or a product request signer."""

    def __init__(self):
        super().__init__()
        self.intent = self.raw = self.intent_nonce = None
        self.events = []
        self.intent_mode = self.record_mode = None
        self.read_change = lambda value: value
        self.store_change = lambda value: value

    def view(self):
        result = verify(self.raw, RESULT, KEYS) if self.raw else None
        return {**{field: REQUEST_PAYLOAD[field] for field in COMMON},
            "contract": CONTRACT, "phase": "result_recorded" if result else "store_intent" if self.intent else "not_recorded",
            "result_envelope": encode64(self.raw) if self.raw else None,
            "result_id": result["result_id"] if result else None,
            "outcome": result["outcome"] if result else None,
            "failure_code": result["failure_code"] if result else None,
            "intent_at": "2026-10-20T01:01:00Z" if self.intent else None,
            "reported_at": "2026-10-20T01:01:01Z" if result else None,
            "may_store": False, "approval_envelope": None}

    def exchange(self, enrollment_id, body, cancellation):
        if not self.allowed or not self.original_session:
            raise RuntimeError("PRIVATE_AUTH_FAILURE")
        if cancellation.is_set():
            raise RuntimeError("cancelled")
        if body.get("action") == "read_physical_result":
            self.events.append("read")
            return compact(self.read_change(self.view()))
        if body.get("action") == "record_physical_result":
            self.events.append("record")
            if self.record_mode == "before":
                raise RuntimeError("private offline")
            raw = decode64(body["result_envelope"], 16384)
            result = verify(raw, RESULT, KEYS)
            verify_result_binding(REQUEST_PAYLOAD, EXPECTED_DIGEST, result)
            if result["outcome"] == "sensor_stored" and not self.intent:
                raise RuntimeError("intent required")
            if self.raw is not None and self.raw != raw:
                raise RuntimeError("conflicting result")
            self.raw = raw
            if self.record_mode == "after":
                raise RuntimeError("response lost")
            return compact(self.view())
        scope = body["scope"]
        self.events.append(scope)
        if scope == "sensor_write":
            raise RuntimeError("old protocol forbidden")
        if scope == "sensor_capture" and (self.intent or self.raw):
            raise RuntimeError("recovery required")
        if scope != "sensor_store":
            return super().exchange(enrollment_id, body, cancellation)
        if self.intent:
            if self.intent_nonce != body["probe_nonce"]:
                raise RuntimeError("new probe forbidden")
            return compact(self.view())
        self.intent, self.intent_nonce = True, body["probe_nonce"]
        self.monotonic += self.delay
        self.hook()
        if self.intent_mode == "lost":
            raise RuntimeError("intent commit response lost")
        payload = dict(body, enrollment_id=enrollment_id, decision="allow",
            server_time=self.utc.strftime("%Y-%m-%dT%H:%M:%SZ"),
            valid_until=(self.utc + timedelta(seconds=5)).strftime("%Y-%m-%dT%H:%M:%SZ"))
        raw = signed_approval(self.change(payload), key=self.signing_key)
        view = self.view() | {"may_store": True, "approval_envelope": encode64(raw)}
        if self.intent_mode == "replayed":
            view.update(may_store=False, approval_envelope=None)
        return compact(self.store_change(view))


class MySqlAdapterTests(unittest.TestCase):
    def setUp(self):
        self.sensor, self.server = FakeSensor(), MySqlContractServer()
        self.cancel, self.sensor_lock = threading.Event(), threading.Lock()
        self.progress = []
        self.worker = self.make_worker()

    def make_worker(self):
        return MySqlEnrollmentAdapter(sensor=self.sensor, codes=CODES, sensor_lock=self.sensor_lock,
            keys=KEYS, profile=SensorProfile(300, 7, 7), progress=self.progress.append,
            clock=self.server.clock, sleep=lambda seconds: setattr(self.server, "monotonic", self.server.monotonic + seconds))

    def enroll(self, worker=None):
        return (worker or self.worker).enroll_online(request_bytes(), self.cancel,
            exchange=self.server.exchange, approval_clock=self.server.clock)

    def read(self, worker=None):
        return (worker or self.worker).read_result_online(REQUEST_PAYLOAD["enrollment_id"], self.cancel,
            exchange=self.server.exchange, approval_clock=self.server.clock)

    def test_capture_store_commit_result_read_order_and_single_store_without_local_db(self):
        self.sensor.on_store = lambda: self.assertTrue(self.server.intent)
        with patch("sqlite3.connect", side_effect=AssertionError("no SQLite allowed")):
            raw = self.enroll()
        result = verify(raw, RESULT, KEYS)
        self.assertEqual(result["outcome"], "sensor_stored")
        self.assertEqual(self.sensor.stores, [7])
        self.assertEqual(raw, self.server.raw)
        events = self.server.events
        self.assertLess(events.index("sensor_capture"), events.index("sensor_store"))
        self.assertLess(events.index("sensor_store"), events.index("record"))
        self.assertEqual(events.count("sensor_store"), 1)
        self.assertNotIn("sensor_capture", events[events.index("sensor_store") + 1:])
        self.assertNotIn("sensor_write", events)

    def test_same_process_and_restart_recover_identical_bytes_without_sensor_io(self):
        raw = self.enroll()
        self.sensor.calls.clear()
        for worker in (self.worker, self.make_worker()):
            self.assertEqual(self.read(worker), raw)
            self.assertEqual(self.enroll(worker), raw)
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.stores, [7])

    def test_lost_result_before_commit_retries_same_ram_bytes_without_resigning(self):
        self.server.record_mode = "before"
        with self.assertRaises(RuntimeError):
            self.enroll()
        row = self.worker.ledger.rows[REQUEST_PAYLOAD["enrollment_id"]]
        frozen = row["envelope"]
        self.server.record_mode = None
        self.sensor.calls.clear()
        with patch("fingerprint_enrollment_mysql.sign_result", side_effect=AssertionError("must not resign")):
            self.assertEqual(self.read(), frozen)
        self.assertEqual(self.server.raw, frozen)
        self.assertEqual(self.sensor.calls, [])

    def test_lost_result_after_commit_survives_pi_ram_loss(self):
        self.server.record_mode = "after"
        with self.assertRaises(RuntimeError):
            self.enroll()
        self.server.record_mode = None
        self.sensor.calls.clear()
        self.assertEqual(self.read(self.make_worker()), self.server.raw)
        self.assertEqual(self.sensor.calls, [])

    def test_restart_after_store_before_result_commit_stops_on_intent(self):
        self.server.record_mode = "before"
        with self.assertRaises(RuntimeError):
            self.enroll()
        restarted = self.make_worker()
        self.sensor.calls.clear()
        for action in (lambda: self.enroll(restarted), lambda: self.read(restarted)):
            with self.assertRaises(EnrollmentError) as error:
                action()
            self.assertEqual(error.exception.code, "recovery_required")
        self.assertEqual(self.sensor.calls, [])
        self.assertEqual(self.sensor.stores, [7])

    def test_intent_response_loss_or_repeat_never_stores(self):
        for mode in ("lost", "replayed"):
            with self.subTest(mode=mode):
                self.setUp()
                self.server.intent_mode = mode
                raw = self.enroll()
                result = verify(raw, RESULT, KEYS)
                self.assertEqual(result["outcome"], "recovery_required")
                self.assertEqual(result["failure_code"], "storage_uncertain")
                self.assertEqual(self.sensor.stores, [])

    def test_store_mac_scope_nonce_binding_rtt_and_expiry_fail_closed(self):
        mutations = [lambda p: p | {"scope": "sensor_capture"},
            lambda p: p | {"probe_nonce": encode64(bytes(32))},
            lambda p: p | {"reserved_slot": 8},
            lambda p: p | {"valid_until": p["server_time"]}]
        for change in mutations:
            with self.subTest(change=change):
                self.setUp()
                self.server.change = lambda p, fn=change: fn(p) if p["scope"] == "sensor_store" else p
                result = verify(self.enroll(), RESULT, KEYS)
                self.assertEqual(result["outcome"], "recovery_required")
                self.assertEqual(self.sensor.stores, [])
        self.setUp()
        self.server.store_change = lambda v: v | {"approval_envelope": encode64(b"bad MAC")}
        self.assertEqual(verify(self.enroll(), RESULT, KEYS)["outcome"], "recovery_required")
        self.assertEqual(self.sensor.stores, [])

    def test_store_roundtrip_over_budget_and_non_boolean_permission_do_not_store(self):
        for mode in ("slow", "boolean"):
            self.setUp()
            if mode == "slow":
                def slow_store_only():
                    if self.server.intent:
                        self.server.monotonic += 2.1
                        self.server.hook = lambda: None
                self.server.hook = slow_store_only
            else:
                self.server.store_change = lambda v: v | {"may_store": 1}
            result = verify(self.enroll(), RESULT, KEYS)
            self.assertEqual(result["outcome"], "recovery_required")
            self.assertEqual(self.sensor.stores, [])

    def test_store_response_loss_and_unconfirmed_inventory_are_fixed_uncertain_results(self):
        for mode in ("reply_lost", "not_in_inventory"):
            self.setUp()
            if mode == "reply_lost":
                def lost(): raise TimeoutError("private UART detail")
                self.sensor.on_store = lost
            else: self.sensor.persist = False
            raw = self.enroll()
            result = verify(raw, RESULT, KEYS)
            self.assertEqual((result["outcome"], result["failure_code"]), ("recovery_required", "storage_uncertain"))
            self.assertEqual(self.read(self.make_worker()), raw)
            self.assertEqual(self.sensor.stores, [7])

    def test_mysql_runtime_uses_same_sensor_lock_oled_and_motion_feed(self):
        import pi_api
        from fingerprint_enrollment_runtime import MySqlEnrollmentRuntime
        frames = []
        display = pi_api.DisplayCoordinator(frames.append, clock=self.server.clock)
        service = pi_api.FingerprintService(self.sensor, CODES, display,
                                           clock=self.server.clock, sleep=lambda _: None)
        runtime = MySqlEnrollmentRuntime(service, keys=KEYS, profile=SensorProfile(300, 7, 7),
                                        approval_clock=self.server.clock)
        self.addCleanup(runtime.close)
        feed = pi_api.MotionFeed(clock=self.server.clock)
        client = pi_api.create_app(service, feed, runtime.jobs).test_client()
        def during_capture():
            self.assertTrue(service.lock.locked())
            self.assertTrue(display.scanning)
            self.assertEqual(client.post("/scan").status_code, 409)
            feed.connection(True)
            feed.receive("196")
            self.assertEqual(client.get("/motion").status_code, 200)
        self.sensor.on_model = during_capture
        raw = runtime.adapter.enroll_online(request_bytes(), self.cancel,
            exchange=self.server.exchange, approval_clock=self.server.clock)
        self.assertEqual(verify(raw, RESULT, KEYS)["outcome"], "sensor_stored")
        self.assertFalse(service.lock.locked())
        self.assertFalse(display.scanning)
        self.assertIn(("지문 저장됨", "계정 연결 확인 중"), frames)
        self.assertFalse(any("등록 완료" in line for frame in frames for line in frame))

    def test_capture_authority_revoked_prevents_sensor_io(self):
        self.server.allowed = False
        with self.assertRaises(RuntimeError):
            self.enroll()
        self.assertEqual(self.sensor.calls, [])

    def test_duplicate_finger_is_signed_failure_without_intent_or_store(self):
        self.sensor.search_result = CODES.OK
        raw = self.enroll()
        result = verify(raw, RESULT, KEYS)
        self.assertEqual((result["outcome"], result["failure_code"]), ("failed", "duplicate_finger"))
        self.assertIsNone(self.server.intent)
        self.assertIn("already_registered", self.progress)
        self.sensor.calls.clear()
        self.assertEqual(self.read(self.make_worker()), raw)
        self.assertEqual(self.sensor.calls, [])

    def test_cancel_during_intent_never_stores_or_deletes(self):
        def hook():
            if self.server.intent:
                self.cancel.set()
        self.server.hook = hook
        with self.assertRaises((RuntimeError, WireError)):
            self.enroll()
        self.assertEqual(self.sensor.stores, [])
        self.assertTrue(self.server.intent)
        self.assertEqual(self.worker.ledger.rows[REQUEST_PAYLOAD["enrollment_id"]]["state"], "recovery_required")

    def test_slot_conflict_and_different_finger_fail_before_intent(self):
        for mode in ("occupied", "different"):
            self.setUp()
            if mode == "occupied": self.sensor.slots.add(7)
            else: self.sensor.model_result = CODES.ENROLLMISMATCH
            result = verify(self.enroll(), RESULT, KEYS)
            self.assertEqual(result["failure_code"], "slot_conflict" if mode == "occupied" else "different_finger")
            self.assertIsNone(self.server.intent)
            self.assertEqual(self.sensor.stores, [])

    def test_read_only_missing_result_does_not_capture(self):
        with self.assertRaises(EnrollmentError) as error:
            self.read()
        self.assertEqual(error.exception.code, "result_not_available")
        self.assertEqual(self.sensor.calls, [])

    def test_protocol_and_wrong_binding_receipts_do_not_start_sensor(self):
        for field, bad in (("contract", "legacy"), ("reserved_slot", True),
                           ("operation_revision", "2"), ("may_store", True)):
            self.setUp()
            self.server.read_change = lambda v, f=field, b=bad: v | {f: b}
            with self.assertRaises(WireError): self.enroll()
            self.assertEqual(self.sensor.calls, [])

    def test_corrupted_server_result_never_rescans(self):
        self.enroll()
        self.server.raw = b"damaged"
        self.sensor.calls.clear()
        with self.assertRaises(WireError): self.read(self.make_worker())
        self.assertEqual(self.sensor.calls, [])

    def test_concurrent_writer_is_rejected_before_any_server_or_sensor_call(self):
        self.worker._writer.acquire()
        try:
            with self.assertRaises(EnrollmentError) as error: self.enroll()
            self.assertEqual(error.exception.code, "sensor_busy")
        finally: self.worker._writer.release()
        self.assertEqual(self.server.events, [])

    def test_product_import_does_not_load_sqlite_or_hardware_libraries(self):
        script = "import pi_api_enrollment_mysql,sys; assert 'sqlite3' not in sys.modules; assert 'serial' not in sys.modules"
        result = subprocess.run([sys.executable, "-c", script], cwd=__import__("pathlib").Path(__file__).parent,
                                capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)


if __name__ == "__main__":
    unittest.main()
