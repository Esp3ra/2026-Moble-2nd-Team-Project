"""Synthetic authority/lease/process binding; no actual root/service or sensor."""
from dataclasses import replace
import hashlib
import json
import os
from pathlib import Path
import secrets
import tempfile
import threading
import unittest
from unittest.mock import patch
from types import SimpleNamespace
from uuid import uuid4

import fingerprint_identity_wire as wire
from fingerprint_identity import IdentityProfile
from fingerprint_enrollment_private import ConfigurationError
from identity_restart import RestartObservation
from identity_startup import StartupReadiness, inputs_current, profile_digest, read_root_document, validate_handoff
from identity_transition import (
    claim_value, install_server_sources, require_server_ready, server_sources_document, switch_service,
)


class StartupTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.file = Path(self.directory.name) / "private-input"
        self.file.write_bytes(b"synthetic input")
        self.profile = IdentityProfile(str(uuid4()), "synthetic-key", "1", True, 12, 12, 300,
            {kind: secrets.token_bytes(32) for kind in wire.KINDS}, (secrets.token_bytes(32), secrets.token_bytes(32)))
        self.stamp = [str(uuid4()), "a" * 32, os.getpid(), "123456"]
        self.observed = RestartObservation(str(uuid4()), "b" * 40, True, 0, 1, 1, (), True)
        self.evidence = dict(version=1, cycle_id=self.observed.cycle_id, boot_id=self.stamp[0], invocation_id=self.stamp[1],
            previous_pid=os.getpid() + 10000, previous_invocation_id="c" * 32, previous_entry_sha256="d" * 64,
            previous_exit_status=0, previous_result="success", previous_cgroup_empty=True, lock_available_after_stop=True,
            profile_sha256=profile_digest(self.profile), inputs={str(self.file): hashlib.sha256(self.file.read_bytes()).hexdigest()})
        self.service = SimpleNamespace(stopping=threading.Event())
        self.lease_live = True
        self.root_hash = "e" * 64

    def binding(self, changes=None, observed=None):
        return validate_handoff(self.evidence | (changes or {}), self.observed if observed is None else observed,
                                self.profile, tuple(self.stamp))

    def gate(self):
        return StartupReadiness(self.binding(), self.profile, SimpleNamespace(current=lambda: self.lease_live), self.service,
            evidence_path=self.file, evidence_sha256=self.root_hash, stamp_reader=lambda: tuple(self.stamp),
            root_reader=lambda _: ({}, self.root_hash))

    def test_measured_handoff_and_same_live_process_admit_without_new_capture(self):
        gate = self.gate()
        self.assertTrue(gate(self.profile))
        self.assertTrue(gate(self.profile))
        self.assertTrue(inputs_current(self.binding()))
        self.assertFalse(hasattr(gate, "scan"))

    def test_observation_off_unresolved_or_unpinned_is_never_sufficient(self):
        for observed in (replace(self.observed, identity_enabled=False), replace(self.observed, unresolved_challenges=1),
                         replace(self.observed, sources_compared=False)):
            with self.subTest(observed=repr(observed)), self.assertRaises(ConfigurationError):
                self.binding(observed=observed)

    def test_cycle_boot_invocation_old_pid_exit_and_profile_must_all_match(self):
        changes = [dict(version=True), dict(cycle_id=str(uuid4())), dict(boot_id=str(uuid4())),
            dict(invocation_id="f" * 32), dict(previous_pid=self.stamp[2]), dict(previous_pid=True),
            dict(previous_invocation_id=self.stamp[1]), dict(previous_exit_status=True), dict(previous_exit_status=9),
            dict(previous_result="timeout"), dict(previous_cgroup_empty=False), dict(lock_available_after_stop=False),
            dict(profile_sha256="f" * 64), dict(inputs={}), dict(ready=True)]
        for change in changes:
            with self.subTest(change=list(change)), self.assertRaises(ConfigurationError):
                self.binding(change)

    def test_input_change_and_restore_cannot_reopen_same_gate(self):
        gate = self.gate()
        self.assertTrue(gate(self.profile))
        self.file.write_bytes(b"changed")
        self.assertFalse(gate(self.profile))
        self.file.write_bytes(b"synthetic input")
        self.assertFalse(gate(self.profile))

    def test_root_revocation_is_permanent_even_if_original_file_is_restored(self):
        gate = self.gate()
        previous = self.root_hash
        self.root_hash = "f" * 64
        self.assertFalse(gate(self.profile))
        self.root_hash = previous
        self.assertFalse(gate(self.profile))

    def test_lease_loss_cannot_be_healed_inside_old_instance(self):
        gate = self.gate()
        self.lease_live = False
        self.assertFalse(gate(self.profile))
        self.lease_live = True
        self.assertFalse(gate(self.profile))

    def test_process_start_pid_boot_or_invocation_change_closes_gate(self):
        for index in range(4):
            gate = self.gate()
            previous = self.stamp[index]
            self.stamp[index] = str(uuid4()) if index != 2 else self.stamp[2] + 1
            self.assertFalse(gate(self.profile))
            self.stamp[index] = previous
            self.assertFalse(gate(self.profile))

    def test_other_profile_shutdown_and_explicit_close_are_permanent(self):
        gate = self.gate()
        self.assertFalse(gate(replace(self.profile, keys=dict(self.profile.keys))))
        self.assertFalse(gate(self.profile))
        gate = self.gate()
        self.service.stopping.set()
        self.assertFalse(gate(self.profile))
        self.service.stopping.clear()
        self.assertFalse(gate(self.profile))
        gate = self.gate(); gate.close()
        self.assertFalse(gate(self.profile))

    def test_one_invocation_claim_cannot_be_replayed_or_moved_to_new_boot(self):
        value = self.evidence | {"invocation_id": ""}
        claimed = claim_value(value, self.stamp[1], self.stamp[1], self.stamp[0], False)
        self.assertEqual("", value["invocation_id"])
        self.assertEqual(self.stamp[1], claimed["invocation_id"])
        for data, invocation, live, boot, old_present in (
            (claimed, self.stamp[1], self.stamp[1], self.stamp[0], False),
            (value, "f" * 32, self.stamp[1], self.stamp[0], False),
            (value, self.stamp[1], self.stamp[1], str(uuid4()), False),
            (value, self.stamp[1], self.stamp[1], self.stamp[0], True)):
            with self.assertRaises(RuntimeError):
                claim_value(data, invocation, live, boot, old_present)

    @unittest.skipUnless(os.name == "posix", "Real Linux root ownership check")
    def test_user_private_json_cannot_be_used_as_root_handoff(self):
        with self.assertRaises(ConfigurationError):
            read_root_document(self.file)


class TransitionTests(unittest.TestCase):
    def test_same_pi_code_accepts_new_server_sources_without_overwriting_old_cycle(self):
        with tempfile.TemporaryDirectory() as folder:
            code = Path(folder)
            legacy = code / "server-sources.json"
            legacy.write_bytes(b"previous deployment evidence")
            before = {"identity_host.py": "a" * 64, "features/identity/routes.py": "b" * 64}
            after = before | {"features/identity/routes.py": "c" * 64}
            # Only a synthetic owned directory; no actual root/service change.
            with patch("identity_transition.os.O_NOFOLLOW", getattr(os, "O_NOFOLLOW", 0), create=True), \
                    patch("identity_transition.os.O_NONBLOCK", getattr(os, "O_NONBLOCK", 0), create=True):
                first = install_server_sources(code, before, gid=None)
                second = install_server_sources(code, after, gid=None)
            self.assertNotEqual(first, second)
            self.assertEqual(before, json.loads(first.read_bytes()))
            self.assertEqual(after, json.loads(second.read_bytes()))
            self.assertEqual(b"previous deployment evidence", legacy.read_bytes())

    def test_reordered_server_map_selects_same_immutable_profile(self):
        before = {"identity_host.py": "a" * 64, "features/identity/routes.py": "b" * 64}
        self.assertEqual(server_sources_document(Path("code"), before),
                         server_sources_document(Path("code"), dict(reversed(list(before.items())))))

    def test_tampered_content_addressed_server_file_still_blocks_instead_of_overwriting(self):
        with tempfile.TemporaryDirectory() as folder:
            code = Path(folder)
            values = {"identity_host.py": "a" * 64}
            path, _ = server_sources_document(code, values)
            path.write_bytes(b"preserve conflicting evidence")
            with patch("identity_transition.os.O_NOFOLLOW", getattr(os, "O_NOFOLLOW", 0), create=True), \
                    patch("identity_transition.os.O_NONBLOCK", getattr(os, "O_NONBLOCK", 0), create=True):
                with self.assertRaisesRegex(RuntimeError, "existing_file_differs"):
                    install_server_sources(code, values, gid=None)
            self.assertEqual(b"preserve conflicting evidence", path.read_bytes())

    class Operations:
        def __init__(self, fail=()):
            self.fail, self.events = set(fail), []
        def __getattr__(self, name):
            def operation():
                self.events.append(name)
                if name in self.fail: raise RuntimeError(name)
            return operation

    def test_off_or_unresolved_preflight_never_stops_existing_service(self):
        ops = self.Operations(["preflight"])
        with self.assertRaises(RuntimeError): switch_service(ops)
        self.assertEqual(["preflight"], ops.events)

    def test_actual_server_guard_requires_explicit_on_and_zero_original_unresolved(self):
        require_server_ready(dict(identity_enabled=True, unresolved=0))
        for values in (dict(identity_enabled=False, unresolved=0), dict(identity_enabled=1, unresolved=0),
                       dict(identity_enabled=True, unresolved=1), dict(identity_enabled=True, unresolved=False)):
            with self.assertRaisesRegex(RuntimeError, "server_off_or_original_work_unresolved_no_stop"):
                require_server_ready(values)

    def test_unclean_previous_stop_never_installs_or_starts_a_second_writer(self):
        ops = self.Operations(["verify_previous_drained"])
        with self.assertRaises(RuntimeError): switch_service(ops)
        self.assertNotIn("install", ops.events)
        self.assertNotIn("start_previous", ops.events)

    def test_success_is_one_stop_and_one_start_without_rollback(self):
        ops = self.Operations()
        switch_service(ops)
        self.assertEqual(["preflight", "backup", "stop_previous", "verify_previous_drained", "install",
                          "start_candidate", "verify_candidate"], ops.events)

    def test_failed_candidate_is_drained_before_previous_service_is_restored(self):
        ops = self.Operations(["verify_candidate"])
        with self.assertRaisesRegex(RuntimeError, "candidate_failed_previous_restored"):
            switch_service(ops)
        self.assertEqual(["stop_candidate", "verify_candidate_drained", "restore_previous", "start_previous", "verify_previous"], ops.events[-5:])

    def test_failed_install_uses_same_rollback_boundary(self):
        ops = self.Operations(["install"])
        with self.assertRaisesRegex(RuntimeError, "candidate_failed_previous_restored"):
            switch_service(ops)
        self.assertNotIn("start_candidate", ops.events)
        self.assertIn("verify_candidate_drained", ops.events)

    def test_hung_or_signal_killed_candidate_never_blindly_starts_old_writer(self):
        ops = self.Operations(["verify_candidate", "verify_candidate_drained"])
        with self.assertRaises(RuntimeError): switch_service(ops)
        self.assertNotIn("restore_previous", ops.events)
        self.assertNotIn("start_previous", ops.events)


if __name__ == "__main__": unittest.main()
