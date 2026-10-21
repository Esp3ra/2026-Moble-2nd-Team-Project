"""Synthetic legacy files only. Never execute sudo/systemctl or open hardware."""

from contextlib import closing
from dataclasses import replace
import io
import json
from pathlib import Path
import sqlite3
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import fingerprint_recovery_check as tool
from fingerprint_enrollment import EnrollmentLedger, EnrollmentOperation, SensorProfile
from fingerprint_enrollment_adapter import SignedResultCache
from fingerprint_enrollment_wire import COMMON, sign_result
from test_fingerprint_enrollment_adapter import KEYS, REQUEST_PAYLOAD, EXPECTED_DIGEST


class RecoveryCheckTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.state = self.root / "state"
        self.state.mkdir()
        self.job = dict(enrollment_id=REQUEST_PAYLOAD["enrollment_id"], target_user_id=88,
            slot=7, operation_revision="1", claim_revision="1", mappings=0,
            claim_state="reserved", claim_current_job=True)
        self.config = SimpleNamespace(keys=KEYS, profile=SensorProfile(300, 7, 7))
        self.operation = EnrollmentOperation(self.job["enrollment_id"], KEYS.device_id, 88, "1", 7, EXPECTED_DIGEST)
        self.ledger = EnrollmentLedger(self.state / "ledger.sqlite3")
        self.cache = SignedResultCache(self.state / "results.sqlite3")
        self.ledger.claim(self.operation)
        self.ledger.transition(self.operation, "capturing", "failed", "already_registered")

    def read(self):
        # Linux permissions are covered by the actual source checks; Windows
        # synthetic tests bypass those checks for fixture-file content only.
        with patch.object(tool, "_read_private", side_effect=lambda path, maximum=16384: path.read_bytes()):
            return tool.read_legacy(self.state, self.job, self.config)

    def test_failed_row_without_signed_result_is_metadata_match_not_member_binding_proof(self):
        before = {p.name: p.read_bytes() for p in self.state.iterdir()}
        summary, raw = self.read()
        self.assertTrue(summary["metadata_match"])
        self.assertEqual(summary["ledger_code"], "already_registered")
        self.assertFalse(summary["signed_result_present"])
        self.assertFalse(summary["target_binding_verified"])
        self.assertIsNone(raw)
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.state.iterdir()})

    def test_existing_signed_bytes_are_returned_unmodified_and_target_binding_verified(self):
        with closing(sqlite3.connect(self.ledger.path)) as db:
            result_id = db.execute("SELECT result_id FROM local_enrollments").fetchone()[0]
        result = {k: REQUEST_PAYLOAD[k] for k in COMMON}
        result.update(ticket_digest=EXPECTED_DIGEST, result_id=result_id,
                      outcome="failed", failure_code="duplicate_finger", elapsed_ms=300)
        raw = sign_result(result, KEYS)
        self.cache.put(self.operation, raw)
        summary, actual = self.read()
        self.assertEqual(raw, actual)
        self.assertTrue(summary["signed_result_verified"])
        self.assertTrue(summary["target_binding_verified"])
        self.job["target_user_id"] = 89
        with self.assertRaises(tool.CheckError): self.read()

    def test_other_job_is_not_reported_as_matching(self):
        self.job["enrollment_id"] = "00000000-0000-4000-8000-000000000001"
        summary, _ = self.read()
        self.assertFalse(summary["ledger_found"])
        self.assertFalse(summary["metadata_match"])

    def test_wrong_slot_or_device_does_not_match_metadata(self):
        self.job["slot"] = 8
        self.assertFalse(self.read()[0]["metadata_match"])

    def test_journal_or_wal_requires_review_without_connecting_sqlite(self):
        for suffix in ("-journal", "-wal", "-shm"):
            path = self.state / ("ledger.sqlite3" + suffix)
            path.write_bytes(b"preserve")
            with patch("sqlite3.connect", side_effect=AssertionError("must not open database")):
                with self.assertRaises(tool.CheckError): self.read()
            self.assertEqual(path.read_bytes(), b"preserve")
            path.unlink()

    def test_missing_database_is_not_created(self):
        self.cache.path.unlink()
        with self.assertRaises(FileNotFoundError): self.read()
        self.assertFalse(self.cache.path.exists())

    def test_corrupt_database_is_not_repaired(self):
        self.cache.path.write_bytes(b"preserved corrupt evidence")
        with self.assertRaises(sqlite3.DatabaseError): self.read()
        self.assertEqual(self.cache.path.read_bytes(), b"preserved corrupt evidence")

    def test_private_expected_job_does_not_expand_profile_or_mapping_scope(self):
        value = dict(device_id=KEYS.device_id, jobs=[self.job], device_mappings=0)
        self.assertIs(tool.expected_attempt(value, self.config), self.job)
        for changes in ({"slot": True}, {"slot": 8}, {"target_user_id": True},
                        {"claim_current_job": False}, {"mappings": 1}):
            with self.assertRaises(tool.CheckError):
                tool.expected_attempt(value | {"jobs": [self.job | changes]}, self.config)

    def test_no_explicit_action_never_runs_service_or_sensor(self):
        with patch.object(tool, "stop_and_inspect") as run, patch("sys.stderr", io.StringIO()):
            self.assertEqual(tool.main(["--expected", "synthetic"]), 1)
            run.assert_not_called()

    def test_arbitrary_private_exception_never_reaches_console(self):
        with patch.object(tool, "stop_and_inspect", side_effect=ValueError("PRIVATE_UUID_OR_KEY")), \
                patch("sys.stderr", io.StringIO()) as errors:
            self.assertEqual(tool.main(["--expected", "synthetic", "--stop-service-and-inspect"]), 1)
            self.assertNotIn("PRIVATE_UUID_OR_KEY", errors.getvalue())

    def test_unknown_existing_service_hold_is_never_replaced(self):
        hold = self.root / "existing-hold.conf"
        hold.write_bytes(b"another owner's hold")
        with patch.object(tool, "HOLD", hold), patch.object(tool, "protected_hold", return_value=hold.read_bytes()), \
                patch.object(tool.subprocess, "run") as command:
            with self.assertRaises(tool.CheckError): tool.install_hold(self.root, self.root)
            command.assert_not_called()
        self.assertEqual(hold.read_bytes(), b"another owner's hold")

    def test_release_gate_presence_never_installs_hold_or_runs_sudo(self):
        (self.root / "recovery-writer-release").touch()
        with patch.object(tool.subprocess, "run") as command:
            with self.assertRaises(tool.CheckError): tool.install_hold(self.root, self.root)
            command.assert_not_called()

    def test_install_hold_only_adds_condition_and_reloads_no_start_or_restart(self):
        hold = self.root / "hold.conf"
        calls = []
        def install(arguments, **kwargs):
            calls.append(arguments)
            self.assertEqual(arguments[:6], ["sudo", "-n", "install", "-D", "-m", "0644"])
            Path(arguments[-1]).write_bytes(Path(arguments[-2]).read_bytes())
            return SimpleNamespace(returncode=0)
        with patch.object(tool, "HOLD", hold), patch.object(tool.os, "O_NOFOLLOW", 0, create=True), \
                patch.object(tool, "protected_hold", side_effect=lambda: hold.read_bytes() if hold.exists() else None), \
                patch.object(tool.subprocess, "run", side_effect=install), patch.object(tool, "run_command") as command:
            tool.install_hold(self.root, self.root)
            command.assert_called_once_with(["sudo", "-n", "systemctl", "daemon-reload"], timeout=15)
        self.assertEqual(len(calls), 1)
        self.assertIn(b"ConditionPathExists=", hold.read_bytes())
        self.assertNotIn(b"ExecStart", hold.read_bytes())

    def test_backup_keeps_original_bytes_and_excludes_runtime_lock(self):
        (self.state / "runtime.lock").touch()
        config = self.root / "enrollment.local.json"
        config.write_bytes(b"synthetic private config")
        before = {p.name: p.read_bytes() for p in self.state.iterdir()}
        with patch.object(tool, "_read_private", side_effect=lambda p, maximum=16384: p.read_bytes()), \
                patch.object(tool.os, "O_NOFOLLOW", 0, create=True), \
                patch.object(tool, "run_command", return_value=b"synthetic service"):
            destination = tool.backup_evidence(self.root, self.state, config, {"MainPID": "0"})
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.state.iterdir()})
        self.assertEqual((destination / "original-config.private.json").read_bytes(), config.read_bytes())
        self.assertEqual((destination / "original-state" / "ledger.sqlite3").read_bytes(), self.ledger.path.read_bytes())
        self.assertFalse((destination / "original-state" / "runtime.lock").exists())

    def test_system_failures_identify_stage_and_exit_without_printing_private_stderr(self):
        for args, stage in ((["systemctl", "show", tool.UNIT], "service_properties"),
                            (["systemctl", "cat", tool.UNIT], "service_unit_read"),
                            (["sudo", "systemctl", "daemon-reload"], "systemd_reload")):
            with patch.object(tool.subprocess, "run", return_value=SimpleNamespace(
                    returncode=4, stdout=b"PRIVATE_OUTPUT", stderr=b"PRIVATE_SECRET")):
                with self.assertRaises(tool.CheckError) as caught: tool.run_command(args)
            self.assertEqual(str(caught.exception), stage + "_failed_exit_4")

    def test_properties_uses_explicit_long_property_option(self):
        with patch.object(tool, "run_command", return_value=b"User=moble\nMainPID=0\n") as command:
            self.assertEqual(tool.properties(), {"User": "moble", "MainPID": "0"})
        args = command.call_args.args[0]
        self.assertTrue(args[-1].startswith("--property=User,"))
        self.assertFalse(any(arg.startswith("-pUser") for arg in args))

    def test_protected_hold_read_requires_sudo_and_never_reads_parent_as_regular_user(self):
        raw = b"[Unit]\nConditionPathExists=/synthetic/release\n"
        with patch.object(Path, "read_bytes", side_effect=PermissionError("private root directory")), \
                patch.object(tool, "run_command", return_value=json.dumps({"exists": True, "hex": raw.hex()}).encode()) as command:
            self.assertEqual(tool.protected_hold(), raw)
        args = command.call_args.args[0]
        self.assertEqual(args[:5], ["sudo", "-n", "/usr/bin/python3", "-I", "-c"])
        compile(args[-2], "protected-read", "exec")
        self.assertNotIn("chmod", args[-2])

    def test_protected_hold_missing_and_bad_reply_fail_closed(self):
        with patch.object(tool, "run_command", return_value=b'{"exists":false,"hex":null}'):
            self.assertIsNone(tool.protected_hold())
        for value in (b'{}', b'{"exists":1,"hex":""}', b'{"exists":true,"hex":"not hex"}'):
            with patch.object(tool, "run_command", return_value=value), self.assertRaises(tool.CheckError):
                tool.protected_hold()

    def test_unit_contents_uses_cached_sudo(self):
        with patch.object(tool, "run_command", return_value=b"synthetic private unit") as command:
            self.assertEqual(tool.unit_contents(), b"synthetic private unit")
        command.assert_called_once_with(["sudo", "-n", "systemctl", "cat", tool.UNIT, "--no-pager"])


if __name__ == "__main__":
    unittest.main()
