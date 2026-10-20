"""Threaded Pi job transport with real signed fake approval replies, no hardware."""

import secrets
import importlib.util
import time
import unittest
from uuid import uuid4

import test_fingerprint_enrollment_adapter as fixtures
from test_fingerprint_enrollment_online import FakeApprovalRelay
from fingerprint_enrollment import EnrollmentError
from fingerprint_enrollment_relay import EnrollmentRelayJobs
from fingerprint_enrollment_wire import WireError, decode64, encode64


class RelayTests(unittest.TestCase):
    def setUp(self):
        self.fx = fixtures.AdapterTests("test_verified_ticket_reaches_two_capture_core_and_signed_minimal_result")
        self.fx.setUp()
        self.addCleanup(self.fx.doCleanups)
        self.server = FakeApprovalRelay()
        self.jobs = EnrollmentRelayJobs(self.fx.adapter(current_approval=None, trusted_utc=None),
                                       approval_clock=self.server.clock)
        self.addCleanup(self.jobs.close)
        self.capability = encode64(secrets.token_bytes(32))
        self.body = dict(relay_id=str(uuid4()), enrollment_id=fixtures.REQUEST_PAYLOAD["enrollment_id"],
                         mode="enroll", request_envelope=encode64(fixtures.request_bytes()))

    def pump(self, body=None, capability=None):
        body, capability = body or self.body, capability or self.capability
        self.jobs.start(body, capability)
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            snapshot = self.jobs.snapshot(body["relay_id"], capability)
            if snapshot["state"] in ("succeeded", "failed"):
                return snapshot
            if snapshot["challenge"] is not None:
                challenge = snapshot["challenge"]
                reply = self.server.exchange(body["enrollment_id"], challenge, self.fx.cancel)
                ack = dict(probe_nonce=challenge["probe_nonce"], approval_envelope=encode64(reply))
                result = self.jobs.answer(body["relay_id"], capability, ack)
                if result["state"] in ("succeeded", "failed"):
                    return result
                # A lost ACK is retried idempotently, never answers the next probe.
                result = self.jobs.answer(body["relay_id"], capability, ack)
                if result["state"] in ("succeeded", "failed"):
                    return result
            else:
                time.sleep(0.001)
        self.fail("Job did not finish")

    def test_async_registration_relay_and_duplicate_start_never_store_twice(self):
        result = self.pump()
        self.assertEqual(result["state"], "succeeded")
        self.assertEqual(self.fx.sensor.stores, [7])
        replay = self.jobs.start(self.body, self.capability)
        self.assertEqual(replay["state"], "delivered")
        self.assertIsNone(replay["result_envelope"])
        self.assertEqual(self.fx.sensor.stores, [7])

    def test_expired_read_job_returns_same_signed_bytes_with_read_only_approval(self):
        first = self.pump()
        from datetime import datetime, timezone
        self.server.utc = datetime(2026, 10, 20, 1, 4, tzinfo=timezone.utc)
        self.fx.sensor.calls.clear()
        self.server.calls.clear()
        read_body = dict(self.body, relay_id=str(uuid4()), mode="read_result", request_envelope=None)
        result = self.pump(read_body)
        self.assertEqual(result["state"], "succeeded")
        self.assertEqual(result["result_envelope"], first["result_envelope"])
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertTrue(all(body["scope"] == "result_read" for _, body in self.server.calls))

    def test_wrong_capability_and_relay_id_cannot_read_or_cancel_job(self):
        self.jobs.start(self.body, self.capability)
        other = encode64(secrets.token_bytes(32))
        for action in (self.jobs.snapshot, self.jobs.cancel):
            with self.assertRaises(EnrollmentError):
                action(self.body["relay_id"], other)
        with self.assertRaises(EnrollmentError):
            self.jobs.snapshot(str(uuid4()), self.capability)

    def test_cancel_while_waiting_approval_never_captures_or_stores(self):
        self.jobs.start(self.body, self.capability)
        self.jobs.cancel(self.body["relay_id"], self.capability)
        self.jobs.close()
        result = self.jobs.snapshot(self.body["relay_id"], self.capability)
        self.assertEqual(result["state"], "failed")
        self.assertEqual(self.fx.sensor.calls, [])

    def test_wrong_approval_nonce_and_changed_start_body_are_rejected(self):
        self.jobs.start(self.body, self.capability)
        with self.assertRaises(WireError):
            self.jobs.answer(self.body["relay_id"], self.capability,
                dict(probe_nonce=encode64(bytes(32)), approval_envelope=encode64(b"bad")))
        with self.assertRaises(WireError):
            self.jobs.start(dict(self.body, mode="read_result", request_envelope=None), self.capability)

    def test_bad_ticket_and_unknown_input_do_not_create_worker(self):
        for body in (dict(self.body, request_envelope=encode64(b"bad")), dict(self.body, extra=True)):
            with self.assertRaises((WireError, EnrollmentError)):
                self.jobs.start(body, self.capability)
        self.assertEqual(self.fx.sensor.calls, [])
        self.assertEqual(self.jobs._jobs, {})

    def test_corrupt_approval_fails_without_exposing_original_exception_or_saving(self):
        self.server.signing_key = fixtures.KEYS.result_key
        result = self.pump()
        self.assertEqual(result["state"], "failed")
        self.assertEqual(result["error_code"], "not_authorized")
        self.assertIsNone(result["result_envelope"])
        self.assertEqual(self.fx.sensor.calls, [])

    @unittest.skipUnless(importlib.util.find_spec("flask") is not None, "Flask test dependency required")
    def test_flask_routes_are_explicit_opt_in_and_match_pc_job_protocol(self):
        from types import SimpleNamespace
        from pi_api import create_app
        feed = SimpleNamespace(snapshot=lambda: {"available": False})
        unused_sensor = SimpleNamespace(scan=lambda: ({"success": False}, 409))
        default = create_app(unused_sensor, feed).test_client()
        self.assertEqual(default.post("/enrollments/jobs", json=self.body).status_code, 404)
        client = create_app(unused_sensor, feed, self.jobs).test_client()
        headers = {"X-Enrollment-Relay": self.capability}
        response = client.post("/enrollments/jobs", json=self.body, headers=headers)
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.headers["Cache-Control"], "no-store")
        path = "/enrollments/jobs/" + self.body["relay_id"]
        self.assertEqual(client.get(path).status_code, 409)
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            state = client.get(path, headers=headers).json["data"]
            if state["state"] in ("succeeded", "failed"):
                break
            if state["challenge"] is not None:
                challenge = state["challenge"]
                raw = self.server.exchange(self.body["enrollment_id"], challenge, self.fx.cancel)
                response = client.post(path + "/approval", json={"probe_nonce": challenge["probe_nonce"],
                    "approval_envelope": encode64(raw)}, headers=headers)
                self.assertEqual(response.status_code, 200)
                state = response.json["data"]
                if state["state"] in ("succeeded", "failed"):
                    break
            else:
                time.sleep(0.001)
        self.assertEqual(state["state"], "succeeded")
        self.assertEqual(self.fx.sensor.stores, [7])
        self.assertEqual(client.post(path + "/cancel", headers=headers).status_code, 200)

    def test_old_transport_capability_cannot_re_read_an_already_delivered_result(self):
        result = self.pump()
        self.assertIsNotNone(result["result_envelope"])
        again = self.jobs.snapshot(self.body["relay_id"], self.capability)
        self.assertEqual(again["state"], "delivered")
        self.assertIsNone(again["result_envelope"])

    def test_expired_delivery_window_drops_transport_copy_but_keeps_private_cache(self):
        self.pump()
        job = self.jobs._jobs[self.body["relay_id"]]
        # Simulate a queued final HTTP response that was not consumed before expiry.
        with job.condition:
            job.state = "succeeded"
            job.result = self.fx.cache.read_local(job.enrollment_id)
        self.server.monotonic = job.delivery_deadline
        result = self.jobs.snapshot(job.relay_id, self.capability)
        self.assertIsNone(result["result_envelope"])
        self.assertEqual(result["error_code"], "result_delivery_expired")
        self.assertIsNotNone(self.fx.cache.read_local(job.enrollment_id))


if __name__ == "__main__":
    unittest.main()
