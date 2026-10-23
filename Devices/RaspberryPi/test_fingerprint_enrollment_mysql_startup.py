"""No hardware or server changes. POSIX lease tests run on Linux only."""

from contextlib import contextmanager
import io
import json
import os
from pathlib import Path
import secrets
import tempfile
import unittest
from unittest.mock import patch

import pi_api_enrollment_mysql as entry
from fingerprint_enrollment_private import ConfigurationError
from fingerprint_enrollment_private import parse_configuration
from fingerprint_enrollment_wire import encode64


def document(root):
    """Synthetic settings independent of the retired SQLite startup module."""
    return dict(version=1, device_id="123e4567-e89b-42d3-a456-426614174000",
                key_id="synthetic-startup", key_epoch=1,
                request_key_b64=encode64(secrets.token_bytes(32)),
                result_key_b64=encode64(secrets.token_bytes(32)),
                profile=dict(expected_capacity=300, minimum_slot=1, maximum_slot=299),
                state_directory=str(root / "state"))


class MySqlStartupTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.gettempdir()).absolute() / "synthetic-mysql-configuration"
        self.path = self.root / "private.json"
        self.value = document(self.root) | {"version": 2, "contract": "enrollment-mysql-v1"}

    def load(self, value=None):
        raw = json.dumps(self.value if value is None else value).encode()
        with patch.object(entry, "require_linux"), patch.object(entry, "_directory"), \
                patch.object(entry, "_read_private", return_value=raw):
            return entry.load_mysql_configuration(self.path)

    def test_explicit_version2_reuses_keys_profile_and_shared_private_lease(self):
        parsed = self.load()
        self.assertEqual(parsed.state_directory, self.root / "state")
        self.assertEqual(parsed.keys.key_epoch, "1")
        self.assertNotIn(self.value["request_key_b64"], repr(parsed))

    def test_legacy_version_unknown_contract_missing_extra_and_new_lock_path_rejected(self):
        variants = [document(self.root), self.value | {"version": True},
            self.value | {"contract": "other"}, self.value | {"unexpected": True},
            self.value | {"state_directory": str(self.root / "another-runtime")},
            {k: v for k, v in self.value.items() if k != "result_key_b64"}]
        for value in variants:
            with self.subTest(value=list(value)), self.assertRaises(ConfigurationError):
                self.load(value)

    def test_pure_key_parser_rejects_wrong_types_shared_keys_and_invalid_profile(self):
        value = document(self.root)
        for key, bad in (("key_epoch", True), ("key_epoch", "1"),
                ("result_key_b64", value["request_key_b64"]),
                ("request_key_b64", value["request_key_b64"] + "="),
                ("profile", {"expected_capacity": 300, "minimum_slot": True, "maximum_slot": 299}),
                ("state_directory", "relative")):
            with self.subTest(key=key), self.assertRaises(ConfigurationError):
                parse_configuration(json.dumps(value | {key: bad}).encode())

    def test_duplicate_json_fields_and_invalid_encoding_cannot_select_a_key(self):
        for raw in (b'{"version":1,"version":1}', b'[]', b'\xff', b'{}', b'x' * 16385):
            with self.assertRaises(ConfigurationError) as caught:
                parse_configuration(raw)
            self.assertEqual(str(caught.exception), "invalid_configuration")

    def test_check_never_starts_hardware_or_prepares_any_database(self):
        with patch.object(entry, "load_mysql_configuration"), patch.object(entry, "mysql_runtime_session"), \
                patch.object(entry, "suspend_aware_clock"), patch("pi_api.main") as host, \
                patch("sys.stdout", io.StringIO()):
            self.assertEqual(entry.run(["--config", "synthetic", "--check-config"]), 0)
            host.assert_not_called()

    def test_factory_borrows_sensor_and_holds_lease_until_host_cleanup(self):
        events = []
        @contextmanager
        def lease(_):
            events.append("acquired")
            yield
            events.append("released")
        def host(*, enrollment_factory):
            enrollment_factory("borrowed")
            events.append("host_cleanup")
        with patch.object(entry, "load_mysql_configuration") as config, \
                patch.object(entry, "mysql_runtime_session", lease), \
                patch.object(entry, "suspend_aware_clock"), patch("pi_api.main", host), \
                patch.object(entry, "MySqlEnrollmentRuntime") as runtime:
            self.assertEqual(entry.run(["--config", "synthetic"]), 0)
            runtime.assert_called_once_with("borrowed", keys=config.return_value.keys,
                                            profile=config.return_value.profile)
        self.assertEqual(events, ["acquired", "host_cleanup", "released"])

    def test_failure_is_nonzero_fixed_message_without_secret_details(self):
        with patch.object(entry, "load_mysql_configuration", side_effect=OSError("PRIVATE_SECRET")), \
                patch("pi_api.main") as host, patch("sys.stderr", io.StringIO()) as errors:
            self.assertEqual(entry.run(["--config", "synthetic"]), 1)
            self.assertNotIn("PRIVATE_SECRET", errors.getvalue())
            host.assert_not_called()

    @unittest.skipUnless(os.name == "posix", "Linux service ownership/lease")
    def test_actual_lease_rejects_parallel_writer_and_preserves_old_sqlite_evidence(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            root.chmod(0o700)
            state = root / "state"
            state.mkdir(mode=0o700)
            lock = state / "runtime.lock"
            lock.touch(mode=0o600)
            value = document(root) | {"version": 2, "contract": "enrollment-mysql-v1"}
            config_path = root / "private.json"
            config_path.touch(mode=0o600)
            config_path.write_text(json.dumps(value))
            config = entry.load_mysql_configuration(config_path)
            with entry.mysql_runtime_session(config) as lease:
                self.assertTrue(lease.current())
                with self.assertRaises(ConfigurationError) as caught:
                    with entry.mysql_runtime_session(config): pass
                self.assertEqual(str(caught.exception), "state_in_use")
            self.assertFalse(lease.current())
            legacy = state / "ledger.sqlite3"
            legacy.write_bytes(b"preserved evidence")
            legacy.chmod(0o600)
            with self.assertRaises(ConfigurationError) as caught:
                with entry.mysql_runtime_session(config): pass
            self.assertEqual(str(caught.exception), "legacy_evidence_migration_required")
            self.assertEqual(legacy.read_bytes(), b"preserved evidence")


if __name__ == "__main__":
    unittest.main()
