import json
import secrets
import time
from uuid import uuid4
import unittest
from unittest.mock import patch

from flask import Flask

import fingerprint_identity_wire as wire
import test_fingerprint_identity as fixtures
from identity_http import mount_identity_http
from pi_api import create_app, MotionFeed


class HttpTests(unittest.TestCase):
    def setUp(self):
        self.env = fixtures.AdapterTests()
        self.env.setUp()
        self.app = Flask(__name__)
        self.jobs = mount_identity_http(self.app, self.env.service, self.env.profile,
                                        enabled=True, clock=lambda: self.env.now)
        self.client = self.app.test_client()
        self.relay = str(uuid4())
        self.capability = wire.b64encode(secrets.token_bytes(32))
        self.headers = {"X-Identity-Relay": self.capability}
        self.path = "/identity/v2/jobs/" + self.relay
        self.body = dict(relay_id=self.relay, request_b64=wire.b64encode(self.env.raw))

    def tearDown(self):
        self.assertTrue(self.jobs.close())
        self.assertFalse(self.env.service.stopping.is_set())
        self.assertFalse(self.env.service.lock.locked())

    def start(self):
        r = self.client.post("/identity/v2/jobs", json=self.body, headers=self.headers)
        self.assertEqual(r.status_code, 200)
        return r.json["data"]

    def state(self):
        return self.client.get(self.path, headers=self.headers).json["data"]

    def until(self, predicate):
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            state = self.state()
            if predicate(state): return state
            time.sleep(0.005)
        self.fail("HTTP worker did not reach expected state")

    def approve(self, state):
        raw = self.env.exchange(wire.b64decode(state["probe_b64"]), lambda: False)
        body = dict(probe_id=state["probe_id"], approval_b64=wire.b64encode(raw))
        r = self.client.post(self.path + "/approval", json=body, headers=self.headers)
        self.assertEqual(r.status_code, 200)
        return body

    def finish(self):
        seen = set()
        while True:
            state = self.until(lambda s: s["state"] in ("awaiting_approval", "succeeded", "failed"))
            if state["state"] != "awaiting_approval": return state
            self.assertNotIn(state["probe_id"], seen)
            seen.add(state["probe_id"])
            self.approve(state)

    def test_real_flask_transport_result_and_duplicate_post_do_not_rescan(self):
        self.start()
        final = self.finish()
        self.assertEqual(final["state"], "succeeded")
        raw = wire.b64decode(final["result_b64"])
        p = wire.verify(raw, kind="id_result", device_id=self.env.profile.device_id,
                        key_id=self.env.profile.key_id, key=self.env.keys["id_result"])
        self.assertEqual(p["outcome"], "matched")
        self.assertEqual(self.start()["result_b64"], final["result_b64"])
        self.assertEqual(self.env.sensor.finger_fast_search.call_count, 1)
        self.assertEqual(len(self.env.probes), 2)

    def test_missing_wrong_capability_and_nonexistent_job_have_same_status(self):
        self.start()
        for headers, path in [({}, self.path), ({"X-Identity-Relay": wire.b64encode(bytes(32))}, self.path),
                              (self.headers, "/identity/v2/jobs/" + str(uuid4()))]:
            r = self.client.get(path, headers=headers)
            self.assertEqual(r.status_code, 404)
            self.assertEqual(r.json, {"success": False, "error": {"code": "IDENTITY_JOB_NOT_FOUND"}})

    def test_other_relay_cannot_take_same_challenge_or_capability(self):
        self.start()
        other = dict(self.body, relay_id=str(uuid4()))
        self.assertEqual(self.client.post("/identity/v2/jobs", json=other, headers=self.headers).status_code, 409)
        changed_headers = {"X-Identity-Relay": wire.b64encode(secrets.token_bytes(32))}
        self.assertEqual(self.client.post("/identity/v2/jobs", json=self.body, headers=changed_headers).status_code, 404)
        self.env.sensor.get_image.assert_not_called()

    def test_current_probe_ack_is_idempotent_but_other_bytes_are_rejected(self):
        self.start(); state = self.until(lambda s: s["state"] == "awaiting_approval")
        body = self.approve(state)
        self.assertEqual(self.client.post(self.path + "/approval", json=body, headers=self.headers).status_code, 200)
        changed = dict(body, approval_b64=wire.b64encode(b"changed"))
        self.assertEqual(self.client.post(self.path + "/approval", json=changed, headers=self.headers).status_code, 409)

    def test_bad_signature_never_opens_capture(self):
        e = json.loads(self.env.raw); e["mac_b64"] = wire.b64encode(bytes(32))
        body = dict(self.body, request_b64=wire.b64encode(json.dumps(e).encode()))
        r = self.client.post("/identity/v2/jobs", json=body, headers=self.headers)
        self.assertEqual(r.status_code, 403)
        self.env.sensor.get_image.assert_not_called()

    def test_invalid_approval_is_sanitized_and_does_not_capture(self):
        self.start(); state = self.until(lambda s: s["state"] == "awaiting_approval")
        r = self.client.post(self.path + "/approval", json=dict(probe_id=state["probe_id"], approval_b64=wire.b64encode(b"invalid")), headers=self.headers)
        self.assertEqual(r.status_code, 200)
        final = self.until(lambda s: s["state"] == "failed")
        self.assertIsNone(final["result_b64"])
        self.assertEqual(final["error_code"], "IDENTITY_APPROVAL_UNAVAILABLE")
        self.env.sensor.get_image.assert_not_called()

    def test_cancel_wakes_approval_wait_and_keeps_shared_service_running(self):
        self.start(); self.until(lambda s: s["state"] == "awaiting_approval")
        self.assertEqual(self.client.post(self.path + "/cancel", json={}, headers=self.headers).status_code, 200)
        final = self.until(lambda s: s["state"] == "failed")
        self.assertEqual(final["error_code"], "IDENTITY_CAPTURE_CANCELLED")
        self.env.sensor.get_image.assert_not_called()

    def test_completed_result_survives_cleanup_and_stays_byte_identical(self):
        self.start(); final = self.finish()
        r = self.client.post(self.path + "/cancel", json={}, headers=self.headers)
        self.assertEqual(r.json["data"]["result_b64"], final["result_b64"])
        self.assertEqual(self.state()["result_b64"], final["result_b64"])

    def test_unknown_duplicate_wrong_type_and_large_bodies_rejected(self):
        unknown = dict(self.body, slot=17)
        self.assertEqual(self.client.post("/identity/v2/jobs", json=unknown, headers=self.headers).status_code, 400)
        duplicate = json.dumps(self.body)[:-1] + ',"relay_id":"' + self.relay + '"}'
        self.assertEqual(self.client.post("/identity/v2/jobs", data=duplicate, content_type="application/json", headers=self.headers).status_code, 400)
        self.assertEqual(self.client.post("/identity/v2/jobs", data="{}", headers=self.headers).status_code, 415)
        self.assertEqual(self.client.post("/identity/v2/jobs", data=b"x" * 32769, content_type="application/json", headers=self.headers).status_code, 413)
        self.env.sensor.get_image.assert_not_called()

    def test_timeout_does_not_regenerate_probe_or_capture(self):
        self.start(); final = self.until(lambda s: s["state"] == "failed")
        self.assertEqual(final["error_code"], "IDENTITY_APPROVAL_UNAVAILABLE")
        self.assertEqual(self.start()["state"], "failed")
        self.env.sensor.get_image.assert_not_called()

    def test_default_factory_preserves_old_routes_and_requires_profile_if_enabled(self):
        app = create_app(self.env.service, MotionFeed(lambda: self.env.now))
        before = {r.rule for r in app.url_map.iter_rules()}
        self.assertIsNone(mount_identity_http(app, self.env.service))
        self.assertEqual({r.rule for r in app.url_map.iter_rules()}, before)
        with self.assertRaises(ValueError): mount_identity_http(app, self.env.service, enabled=True)

    def test_closed_manager_rejects_new_jobs_without_stopping_sensor_service(self):
        self.assertTrue(self.jobs.close())
        self.assertEqual(self.client.post("/identity/v2/jobs", json=self.body, headers=self.headers).status_code, 503)
        self.assertFalse(self.env.service.stopping.is_set())

    def test_worker_start_failure_is_terminal_without_sensor_or_shutdown_error(self):
        with patch("identity_http.threading.Thread.start", side_effect=RuntimeError("synthetic thread limit")):
            state = self.start()
        self.assertEqual(state["state"], "failed")
        self.assertIsNone(state["result_b64"])
        self.env.sensor.get_image.assert_not_called()
        self.assertTrue(self.jobs.close())
        self.assertEqual(self.client.get(self.path, headers=self.headers).status_code, 503)

    def test_ram_capacity_cannot_evict_old_request_or_start_another_worker(self):
        self.assertTrue(self.jobs.close())
        self.app = Flask("capacity")
        self.jobs = mount_identity_http(self.app, self.env.service, self.env.profile, enabled=True,
                                        clock=lambda: self.env.now, maximum=1)
        self.client = self.app.test_client()
        self.start()
        ticket = dict(self.env.ticket, challenge_id=str(uuid4()))
        body = dict(relay_id=str(uuid4()), request_b64=wire.b64encode(self.env.sign(ticket, "id_request")))
        self.assertEqual(self.client.post("/identity/v2/jobs", json=body, headers=self.headers).status_code, 503)
        self.assertEqual(self.start()["relay_id"], self.relay)
        self.env.sensor.get_image.assert_not_called()


if __name__ == "__main__": unittest.main()
