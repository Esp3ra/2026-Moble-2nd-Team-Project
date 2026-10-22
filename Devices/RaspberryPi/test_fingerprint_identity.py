import hashlib
import json
from pathlib import Path
import secrets
import threading
from types import SimpleNamespace
import unittest
from unittest.mock import Mock

import fingerprint_identity_wire as wire
from fingerprint_identity import IdentityAttempt, IdentityAttemptPool, IdentityProfile, IdentityUnavailable
from pi_api import DisplayCoordinator, FingerprintService


FIXTURE = json.loads((Path(__file__).parent / "fixtures/identity_v2_peer1.json").read_text(encoding="utf-8"))
VECTORS = {v["name"]: v for v in FIXTURE["vectors"]}


class WireTests(unittest.TestCase):
    def test_thirteen_peer1_vectors_exact_bytes_mac_and_digest(self):
        for vector in FIXTURE["vectors"]:
            with self.subTest(name=vector["name"]):
                raw, payload = vector["envelope_json"].encode(), vector["payload_json"].encode()
                key = bytes.fromhex(FIXTURE["public_test_keys_hex"][vector["kind"]])
                self.assertEqual(wire.sign(payload, kind=vector["kind"], device_id=FIXTURE["device_id"],
                                           key_id=FIXTURE["key_id"], key=key), raw)
                self.assertEqual(wire.verify(raw, kind=vector["kind"], device_id=FIXTURE["device_id"],
                                            key_id=FIXTURE["key_id"], key=key), json.loads(payload))
                self.assertEqual(wire.mac_input(json.loads(raw)).hex(), vector["mac_input_hex"])
                self.assertEqual(wire.message_digest(raw), vector["digest"])
                self.assertEqual(wire.message_digest(raw), hashlib.sha256(raw).hexdigest())

    def test_duplicate_fields_wrong_domain_direction_and_noncanonical_base64(self):
        raw = VECTORS["clock-in-request"]["envelope_json"].encode()
        args = dict(kind="id_request", device_id=FIXTURE["device_id"], key_id=FIXTURE["key_id"],
                    key=bytes.fromhex(FIXTURE["public_test_keys_hex"]["id_request"]))
        cases = [raw.replace(b'"v":2', b'"v":2,"v":2'), raw.replace(b'"v":2', b'"v":true'),
                 raw.replace(b'"id_request"', b'"enroll_request"'), raw.replace(b'"mac_b64":"', b'"mac_b64":"=')]
        for changed in cases:
            with self.assertRaises(wire.ProtocolError): wire.verify(changed, **args)
        with self.assertRaises(wire.ProtocolError):
            wire.verify(raw, **dict(args, key=bytes.fromhex(FIXTURE["public_test_keys_hex"]["id_result"])))

    def test_full_envelope_digest_not_reserialized_json(self):
        raw = VECTORS["clock-in-request"]["envelope_json"].encode()
        spaced = json.dumps(json.loads(raw), indent=2).encode()
        self.assertNotEqual(wire.message_digest(raw), wire.message_digest(spaced))


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.now = 0.0
        self.frames, self.probes = [], []
        self.display = DisplayCoordinator(lambda x: self.frames.append(x), lambda: self.now)
        self.sensor = Mock()
        self.sensor.get_image.side_effect = [2, 0]
        self.sensor.image_2_tz.return_value = 0
        self.sensor.finger_fast_search.return_value = 0
        self.sensor.finger_id = 17
        self.service = FingerprintService(self.sensor, SimpleNamespace(OK=0, NOFINGER=2, NOMATCH=8, NOTFOUND=9),
                                          self.display, clock=lambda: self.now, sleep=self.advance)
        self.keys = {kind: secrets.token_bytes(32) for kind in wire.KINDS}
        self.legacy_keys = (secrets.token_bytes(32),)
        self.profile = IdentityProfile(FIXTURE["device_id"], "test-private-epoch", "1", True, 1, 299, 300, self.keys, self.legacy_keys)
        self.ticket = json.loads(VECTORS["clock-in-request"]["payload_json"])
        self.raw = self.sign(self.ticket, "id_request")
        self.delay = 0
        self.bad_scope = False
        self.on_exchange = None
        self.attempt = self.new_attempt()

    def advance(self, seconds): self.now += seconds
    def sign(self, payload, kind):
        return wire.sign(json.dumps(payload, separators=(",", ":")).encode(), kind=kind,
                         device_id=self.profile.device_id, key_id=self.profile.key_id, key=self.keys[kind])
    def new_attempt(self):
        return IdentityAttempt(self.service, self.profile, self.raw, clock=lambda: self.now)
    def exchange(self, raw, stopping):
        self.assertTrue(self.service.lock.locked(), "Fresh approval must occur under shared UART ownership")
        p = wire.verify(raw, kind="id_probe", device_id=self.profile.device_id,
                        key_id=self.profile.key_id, key=self.keys["id_probe"])
        self.probes.append(p)
        self.advance(self.delay)
        if self.on_exchange: self.on_exchange(p)
        approval = dict(p, decision="allow", server_time="2026-10-21T08:00:10Z", valid_until="2026-10-21T08:00:30Z")
        if self.bad_scope: approval["scope"] = "result" if p["scope"] == "capture" else "capture"
        return self.sign(approval, "id_approval")
    def run_attempt(self, **kwargs):
        raw = self.attempt.run(self.exchange, **kwargs)
        result = wire.verify(raw, kind="id_result", device_id=self.profile.device_id,
                             key_id=self.profile.key_id, key=self.keys["id_result"])
        self.assertFalse(self.service.lock.locked())
        self.assertFalse(self.display.scanning)
        return raw, result

    def test_matched_uses_separate_capture_and_result_nonce_and_no_writes(self):
        _, result = self.run_attempt()
        self.assertEqual((result["outcome"], result["slot"], result["failure_code"]), ("matched", 17, None))
        self.assertEqual([p["scope"] for p in self.probes], ["capture", "result"])
        self.assertNotEqual(self.probes[0]["probe_nonce"], self.probes[1]["probe_nonce"])
        self.assertIsNotNone(result["approval_digest"])
        self.sensor.store_model.assert_not_called(); self.sensor.delete_model.assert_not_called()
        self.assertEqual(self.sensor.finger_fast_search.call_count, 1)

    def test_repeated_delivery_returns_same_result_id_and_exact_bytes_without_rescan(self):
        first, _ = self.run_attempt(); second, _ = self.run_attempt()
        self.assertEqual(first, second)
        self.assertEqual(len(self.probes), 2)
        self.assertEqual(self.sensor.finger_fast_search.call_count, 1)

    def test_other_registered_finger_is_no_match_without_other_slot(self):
        self.sensor.finger_id = 18
        _, result = self.run_attempt()
        self.assertEqual((result["outcome"], result["slot"]), ("no_match", None))

    def test_unregistered_is_same_no_match(self):
        self.sensor.finger_fast_search.return_value = 9
        _, result = self.run_attempt()
        self.assertEqual((result["outcome"], result["slot"]), ("no_match", None))

    def test_busy_is_terminal_failure_without_interrupting_owner_or_approval(self):
        self.service.lock.acquire()
        try:
            raw = self.attempt.run(self.exchange)
            p = wire.verify(raw, kind="id_result", device_id=self.profile.device_id,
                            key_id=self.profile.key_id, key=self.keys["id_result"])
            self.assertEqual((p["outcome"], p["failure_code"], p["slot"]), ("busy", "SENSOR_BUSY", None))
            self.assertTrue(self.service.lock.locked()); self.assertEqual(self.probes, [])
            self.sensor.get_image.assert_not_called()
        finally: self.service.lock.release()

    def test_timeout_single_fifteen_second_budget_includes_removal(self):
        self.sensor.get_image.side_effect = None; self.sensor.get_image.return_value = 0
        _, result = self.run_attempt()
        self.assertEqual(result["outcome"], "timeout")
        self.assertAlmostEqual(self.now, 15)
        self.assertEqual(len(self.probes), 1); self.sensor.finger_fast_search.assert_not_called()

    def test_cancel_after_search_prevents_matched_result(self):
        def search(): self.service.stopping.set(); return 0
        self.sensor.finger_fast_search.side_effect = search
        _, result = self.run_attempt()
        self.assertEqual((result["outcome"], result["slot"]), ("cancelled", None))
        self.assertEqual(len(self.probes), 1)

    def test_sensor_error_has_no_result_approval_or_driver_details(self):
        self.sensor.get_image.side_effect = RuntimeError("private driver detail")
        raw, result = self.run_attempt()
        self.assertEqual(result["outcome"], "sensor_error")
        self.assertNotIn("private driver detail", json.dumps(result)); self.assertEqual(len(self.probes), 1)

    def test_invalid_scope_or_rtt_blocks_capture_and_automatic_retry(self):
        for scope, delay in [(True, 0), (False, 2.001)]:
            with self.subTest(scope=scope, delay=delay):
                self.attempt = self.new_attempt(); self.bad_scope, self.delay = scope, delay
                with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
                with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
                self.sensor.get_image.assert_not_called(); self.assertFalse(self.service.lock.locked())

    def test_result_approval_failure_never_returns_local_matched_or_recaptures(self):
        def invalidate(p):
            if p["scope"] == "result": self.bad_scope = True
        self.on_exchange = invalidate
        with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
        with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
        self.assertEqual(self.sensor.finger_fast_search.call_count, 1)
        self.assertFalse(self.service.lock.locked())

    def test_suspend_jump_and_clock_rollback_after_blocking_search(self):
        for target in (40, -1):
            with self.subTest(target=target):
                self.now = 0; self.attempt = self.new_attempt()
                self.sensor.get_image.side_effect = [2, 0]
                def search(): self.now = target; return 0
                self.sensor.finger_fast_search.side_effect = search
                if target < 0:
                    with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
                else:
                    _, p = self.run_attempt(); self.assertEqual(p["outcome"], "timeout")
                self.assertFalse(self.service.lock.locked())

    def test_profile_rejects_public_reused_and_legacy_keys(self):
        cases = [dict(self.keys, id_result=self.keys["id_probe"]),
                 {k: bytes.fromhex(v) for k, v in FIXTURE["public_test_keys_hex"].items()}]
        for keys in cases:
            with self.assertRaises(ValueError):
                IdentityProfile(self.profile.device_id, "test", "1", True, 1, 299, 300, keys, self.legacy_keys)
        with self.assertRaises(ValueError):
            IdentityProfile(self.profile.device_id, "test", "1", True, 1, 299, 300, self.keys,
                            excluded_keys=(self.keys["id_request"],))
        with self.assertRaises(ValueError):
            IdentityProfile(self.profile.device_id, "test", "1", True, 1, 299, 300, self.keys, ())

    def test_profile_slot_epoch_and_bool_are_not_accepted_from_request(self):
        for fields in ({"expected_slot": 300}, {"expected_slot": True}, {"key_epoch": "2"}):
            t = dict(self.ticket, **fields)
            with self.assertRaises(ValueError):
                raw = self.sign(t, "id_request")
                IdentityAttempt(self.service, self.profile, raw, clock=lambda: self.now)
        self.sensor.get_image.assert_not_called()

    def test_cancellation_during_approval_blocks_uart_and_late_success(self):
        self.on_exchange = lambda _: self.service.stopping.set()
        with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
        self.sensor.get_image.assert_not_called(); self.assertFalse(self.service.lock.locked())

    def test_pool_reuses_one_shot_attempt_and_rejects_changed_bytes_and_full_pool(self):
        pool = IdentityAttemptPool(self.service, self.profile, clock=lambda: self.now, maximum=1)
        first = pool.prepare(self.raw)
        self.assertIs(first, pool.prepare(self.raw))
        self.assertEqual(first.run(self.exchange), pool.prepare(self.raw).run(self.exchange))
        self.assertEqual(self.sensor.finger_fast_search.call_count, 1)
        with self.assertRaises(IdentityUnavailable): pool.prepare(json.dumps(json.loads(self.raw), indent=2).encode())
        ticket = dict(self.ticket, challenge_id="20000000-0000-4000-8000-000000000002")
        with self.assertRaises(IdentityUnavailable): pool.prepare(self.sign(ticket, "id_request"))

    def test_duplicate_probe_is_consumed_once_even_if_first_reply_invalid(self):
        binding = {k: self.ticket[k] for k in wire.COMMON}
        binding["ticket_digest"] = wire.message_digest(self.raw)
        probe = wire.ApprovalProbe(binding, "capture", lambda: self.now, ticket_expires_at=self.ticket["expires_at"])
        bad = self.sign(dict(probe.payload, scope="result", decision="allow",
                            server_time="2026-10-21T08:00:10Z", valid_until="2026-10-21T08:00:30Z"), "id_approval")
        args = dict(device_id=self.profile.device_id, key_id=self.profile.key_id, key=self.keys["id_approval"])
        with self.assertRaises(wire.ProtocolError): probe.accept(bad, **args)
        good = self.sign(dict(probe.payload, decision="allow", server_time="2026-10-21T08:00:10Z",
                             valid_until="2026-10-21T08:00:30Z"), "id_approval")
        with self.assertRaises(wire.ProtocolError): probe.accept(good, **args)

    def test_short_approval_never_extends_capture_budget(self):
        def short_exchange(raw, stopping):
            p = wire.verify(raw, kind="id_probe", device_id=self.profile.device_id, key_id=self.profile.key_id,
                            key=self.keys["id_probe"])
            return self.sign(dict(p, decision="allow", server_time="2026-10-21T08:00:10Z",
                                  valid_until="2026-10-21T08:00:14Z"), "id_approval")
        self.sensor.get_image.side_effect = None; self.sensor.get_image.return_value = 0
        raw = self.attempt.run(short_exchange)
        p = wire.verify(raw, kind="id_result", device_id=self.profile.device_id, key_id=self.profile.key_id,
                        key=self.keys["id_result"])
        self.assertEqual(p["outcome"], "timeout"); self.assertAlmostEqual(self.now, 2.75)
        self.sensor.finger_fast_search.assert_not_called()

    def test_other_scan_remains_busy_during_approval_and_motion_feed_keeps_updating(self):
        from pi_api import MotionFeed
        motion = MotionFeed(clock=lambda: self.now)
        motion.connection(True)
        def during(_):
            response = self.service.scan()
            self.assertEqual(response[1], 409)
            self.advance(0.2)
            motion.receive("195")
            self.assertTrue(motion.snapshot()["available"])
        self.on_exchange = during
        self.run_attempt()

    def test_changed_request_and_bad_approval_mac_never_touch_sensor(self):
        envelope = json.loads(self.raw); envelope["mac_b64"] = wire.b64encode(bytes(32))
        with self.assertRaises(wire.ProtocolError):
            IdentityAttempt(self.service, self.profile, json.dumps(envelope).encode(), clock=lambda: self.now)
        def invalid_mac(raw, stopping):
            valid = json.loads(self.exchange(raw, stopping)); valid["mac_b64"] = wire.b64encode(bytes(32))
            return json.dumps(valid).encode()
        with self.assertRaises(IdentityUnavailable): self.attempt.run(invalid_mac)
        self.sensor.get_image.assert_not_called(); self.assertFalse(self.service.lock.locked())

    def test_concurrent_duplicate_run_cannot_terminalize_current_capture_as_busy(self):
        def during(_):
            with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
        self.on_exchange = during
        _, p = self.run_attempt()
        self.assertEqual(p["outcome"], "matched"); self.assertEqual(self.sensor.finger_fast_search.call_count, 1)

    def test_cancellation_during_result_approval_discards_success_and_no_recapture(self):
        def during(p):
            if p["scope"] == "result": self.service.stopping.set()
        self.on_exchange = during
        with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
        self.service.stopping.clear()
        with self.assertRaises(IdentityUnavailable): self.attempt.run(self.exchange)
        self.assertEqual(self.sensor.finger_fast_search.call_count, 1); self.assertFalse(self.service.lock.locked())


if __name__ == "__main__": unittest.main()
