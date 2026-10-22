import contextlib
import io
import sys
import unittest
from unittest.mock import Mock, patch

import pi_api_identity_mysql as entry


class IdentityMySqlEntryTests(unittest.TestCase):
    args = ["--config", "/private/enrollment.json", "--identity-config", "/private/identity.json"]

    def run_entry(self, extra=(), **kwargs):
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return entry.run(self.args + list(extra), **kwargs)

    def test_config_only_never_opens_state_sensor_or_runtime(self):
        with patch.object(entry, "load_mysql_configuration") as load, patch.object(entry, "load_identity_configuration") as profile, \
                patch.object(entry, "mysql_runtime_session") as session:
            self.assertEqual(0, self.run_entry(["--check-config"]))
            profile.assert_called_once_with("/private/identity.json", load.return_value, retired_keys=())
            session.assert_not_called()

    def test_missing_readiness_blocks_before_lock_and_uart(self):
        with patch.object(entry, "load_mysql_configuration"), patch.object(entry, "load_identity_configuration"), \
                patch.object(entry, "mysql_runtime_session") as session:
            self.assertEqual(1, self.run_entry())
            self.assertEqual(1, self.run_entry(ready=True))
            session.assert_not_called()

    def test_same_single_sensor_and_existing_lock_are_reused(self):
        ready = Mock(return_value=False)
        fake_main = Mock()
        with patch.object(entry, "load_mysql_configuration") as config, patch.object(entry, "load_identity_configuration") as profile, \
                patch.object(entry, "mysql_runtime_session") as session, patch.object(entry, "MySqlEnrollmentRuntime") as enroll, \
                patch.object(entry, "IdentityRuntime") as identity, patch.dict(sys.modules, {"pi_api": Mock(main=fake_main)}):
            self.assertEqual(0, self.run_entry(ready=ready))
            session.assert_called_once_with(config.return_value)
            shared = object()
            fake_main.call_args.kwargs["enrollment_factory"](shared)
            fake_main.call_args.kwargs["identity_factory"](shared)
            enroll.assert_called_once_with(shared, keys=config.return_value.keys, profile=config.return_value.profile)
            identity.assert_called_once_with(shared, profile.return_value, ready=ready)
            ready.assert_not_called()  # Entry does not manufacture a readiness result.

    def test_invalid_private_config_never_opens_runtime(self):
        with patch.object(entry, "load_mysql_configuration", side_effect=RuntimeError("private data")), \
                patch.object(entry, "mysql_runtime_session") as session:
            self.assertEqual(1, self.run_entry(ready=Mock()))
            session.assert_not_called()


if __name__ == "__main__": unittest.main()
