"""Synthetic settings only; real POSIX ACL cases are explicitly skipped on Windows."""
import io
import json
import os
from pathlib import Path
import secrets
import tempfile
import unittest
from unittest.mock import patch

from fingerprint_enrollment_private import ConfigurationError, parse_configuration
import fingerprint_identity_wire as wire
import identity_private as private
from test_fingerprint_enrollment_mysql_startup import document


class PrivateTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.gettempdir()).absolute() / "identity-private-synthetic"
        self.enrollment = parse_configuration(json.dumps(document(self.root)).encode())
        self.value = dict(version=2, contract="identity-rw-id-v2", device_id=self.enrollment.keys.device_id,
                          key_id="synthetic-identity", key_epoch="1",
                          profile=dict(capacity=300, minimum_slot=1, maximum_slot=299),
                          keys={kind: wire.b64encode(secrets.token_bytes(32)) for kind in wire.KINDS})

    def parse(self, value=None, **kwargs):
        return private.parse_identity_configuration(
            json.dumps(self.value if value is None else value).encode(), self.enrollment, **kwargs)

    def test_valid_profile_is_bound_to_existing_owner_without_key_output(self):
        profile = self.parse()
        self.assertEqual(profile.device_id, self.enrollment.keys.device_id)
        self.assertEqual(profile.excluded_keys, (self.enrollment.keys.request_key, self.enrollment.keys.result_key))
        for value in self.value["keys"].values():
            self.assertNotIn(value, repr(profile))
            self.assertNotIn(repr(wire.b64decode(value)), repr(profile))
        self.assertEqual(profile.key_epoch, "1")

    def test_missing_unknown_duplicate_wrong_json_and_oversized_settings_are_rejected(self):
        raw = json.dumps(self.value).encode()
        variants = [b'[]', b'\xff', b'{}', b'x' * 16385,
                    raw.replace(b'"version": 2', b'"version": 2,"version": 2'),
                    json.dumps(self.value | {"enabled": True}).encode(),
                    json.dumps({k: v for k, v in self.value.items() if k != "keys"}).encode()]
        for bad in variants:
            with self.subTest(size=len(bad)), self.assertRaises(ConfigurationError) as caught:
                private.parse_identity_configuration(bad, self.enrollment)
            self.assertEqual(str(caught.exception), "invalid_identity_configuration")

    def test_wrong_device_version_contract_epoch_and_profile_cannot_widen_slot_policy(self):
        variants = [self.value | {"device_id": "123e4567-e89b-42d3-a456-426614174001"},
                    self.value | {"version": True}, self.value | {"version": 1},
                    self.value | {"contract": "enrollment-mysql-v1"},
                    self.value | {"key_epoch": 1}, self.value | {"key_epoch": "01"}]
        for change in ({"capacity": 301}, {"minimum_slot": 0}, {"maximum_slot": 300},
                       {"minimum_slot": True}, {"maximum_slot": 0}, {"unexpected": 1}):
            variants.append(self.value | {"profile": self.value["profile"] | change})
        for bad in variants:
            with self.subTest(fields=list(bad)), self.assertRaises(ConfigurationError):
                self.parse(bad)
        narrow = self.parse(self.value | {"profile": dict(capacity=300, minimum_slot=12, maximum_slot=12)})
        self.assertEqual((narrow.minimum_slot, narrow.maximum_slot), (12, 12))

    def test_keys_require_exact_four_private_independent_canonical_32byte_values(self):
        keys = self.value["keys"]
        bad_values = [wire.b64encode(bytes([1]) * 32), keys["id_probe"],
                      wire.b64encode(self.enrollment.keys.request_key),
                      wire.b64encode(self.enrollment.keys.result_key),
                      wire.b64encode(secrets.token_bytes(31)), wire.b64encode(secrets.token_bytes(33)),
                      keys["id_request"] + "=", True]
        for bad in bad_values:
            with self.subTest(kind=type(bad).__name__), self.assertRaises(ConfigurationError) as caught:
                self.parse(self.value | {"keys": keys | {"id_request": bad}})
            self.assertEqual(str(caught.exception), "invalid_identity_configuration")
            if isinstance(bad, str):
                self.assertNotIn(bad, str(caught.exception))
        for bad in (keys | {"unknown": keys["id_probe"]}, {k: v for k, v in keys.items() if k != "id_result"}, []):
            with self.assertRaises(ConfigurationError):
                self.parse(self.value | {"keys": bad})

    def test_retired_keys_come_from_trusted_host_not_new_document(self):
        retired = wire.b64decode(self.value["keys"]["id_result"])
        with self.assertRaises(ConfigurationError):
            self.parse(retired_keys=(retired,))
        for bad in ([retired], (b"short",), ("private",)):
            with self.assertRaises(ConfigurationError):
                self.parse(retired_keys=bad)
        with self.assertRaises(ConfigurationError):
            self.parse(self.value | {"excluded_keys": []})
        with self.assertRaises(ConfigurationError):
            private.parse_identity_configuration(json.dumps(self.value).encode(), object())

    def test_private_loader_requires_linux_and_checks_parent_before_read(self):
        events = []
        with patch.object(private, "require_linux", side_effect=lambda: events.append("linux")), \
                patch.object(private, "_directory", side_effect=lambda p: events.append(p)), \
                patch.object(private, "_read_private", side_effect=lambda p: events.append(p) or json.dumps(self.value).encode()):
            private.load_identity_configuration(self.root / "identity.json", self.enrollment)
        self.assertEqual(events, ["linux", self.root, self.root / "identity.json"])
        with patch.object(private, "require_linux", side_effect=OSError("PRIVATE_PATH_OR_KEY")), \
                patch.object(private, "_read_private") as read:
            with self.assertRaises(ConfigurationError) as caught:
                private.load_identity_configuration(self.root / "identity.json", self.enrollment)
            self.assertEqual(str(caught.exception), "identity_private_configuration_unavailable")
            read.assert_not_called()

    def test_check_cli_is_read_only_and_never_claims_runtime_ready(self):
        with patch("pi_api_enrollment_mysql.load_mysql_configuration", return_value=self.enrollment), \
                patch.object(private, "load_identity_configuration", return_value=self.parse()), \
                patch("pi_api.main") as host, patch("sys.stdout", io.StringIO()) as output:
            self.assertEqual(private.run(["--config", "synthetic", "--enrollment-config", "synthetic", "--check-config"]), 0)
            self.assertIn("NOT_READY", output.getvalue())
            host.assert_not_called()
        with patch("sys.stderr", io.StringIO()), self.assertRaises(SystemExit) as caught:
            private.run(["--config", "synthetic", "--enrollment-config", "synthetic"])
        self.assertEqual(caught.exception.code, 2)

    def test_check_cli_failure_does_not_print_private_exception(self):
        with patch("pi_api_enrollment_mysql.load_mysql_configuration", side_effect=ValueError("SECRET_VALUE")), \
                patch("sys.stderr", io.StringIO()) as output:
            self.assertEqual(private.run(["--config", "synthetic", "--enrollment-config", "synthetic", "--check-config"]), 1)
            self.assertEqual(output.getvalue(), "STOPPED: identity_configuration_check_failed\n")

    @unittest.skipUnless(os.name == "posix", "Actual Linux ownership/ACL/symlink checks")
    def test_real_file_modes_links_missing_and_wrong_owner_are_rejected_without_writes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            root.chmod(0o700)
            path = root / "identity.json"
            raw = json.dumps(self.value).encode()
            path.write_bytes(raw)
            path.chmod(0o600)
            self.assertEqual(private.load_identity_configuration(path, self.enrollment).device_id, self.enrollment.keys.device_id)
            path.chmod(0o644)
            with self.assertRaises(ConfigurationError): private.load_identity_configuration(path, self.enrollment)
            path.chmod(0o600)
            root.chmod(0o755)
            with self.assertRaises(ConfigurationError): private.load_identity_configuration(path, self.enrollment)
            root.chmod(0o700)
            link = root / "link.json"
            link.symlink_to(path)
            with self.assertRaises(ConfigurationError): private.load_identity_configuration(link, self.enrollment)
            link.unlink()
            os.link(path, link)
            with self.assertRaises(ConfigurationError): private.load_identity_configuration(path, self.enrollment)
            link.unlink()
            with patch("fingerprint_enrollment_private.os.getuid", return_value=os.getuid() + 1):
                with self.assertRaises(ConfigurationError): private.load_identity_configuration(path, self.enrollment)
            with self.assertRaises(ConfigurationError): private.load_identity_configuration(root / "missing", self.enrollment)
            self.assertEqual(path.read_bytes(), raw)
            self.assertEqual(path.stat().st_mode & 0o777, 0o600)


if __name__ == "__main__":
    unittest.main()
