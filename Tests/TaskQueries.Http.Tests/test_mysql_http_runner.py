"""Safety regressions for the MySQL-only runner; these never start a DB server."""
import argparse
from pathlib import Path
import os
import signal
import subprocess
import tempfile
import unittest
from unittest.mock import Mock, patch

import run_mysql_http_check as runner


class RunnerSafetyTests(unittest.TestCase):
    def test_missing_opt_in_stops_before_binary_or_native_access(self):
        with patch.object(runner, "validate_binary_directory") as binary:
            with self.assertRaisesRegex(runner.shared.RunFailure, "allow-disposable-mysql"):
                runner.run(argparse.Namespace(allow_disposable_mysql=False))
            binary.assert_not_called()

    def test_runtime_delete_guard_rejects_parent_and_foreign_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary).resolve()
            foreign = parent / "foreign"
            foreign.mkdir()
            marker = foreign / "preserve.txt"
            marker.write_text("preserve", encoding="utf-8")
            for target in (parent, foreign, Path("relative")):
                with self.subTest(target=target.name), self.assertRaises(runner.shared.RunFailure):
                    runner.validate_runtime_path(target, parent)
            self.assertEqual(marker.read_text(encoding="utf-8"), "preserve")

    def test_runtime_cleanup_on_exception_and_keyboard_interrupt(self):
        for failure in (RuntimeError, KeyboardInterrupt):
            with self.subTest(failure=failure.__name__):
                runtime = None
                with self.assertRaises(failure):
                    with runner.owned_runtime() as runtime:
                        (runtime / "owned.txt").write_text("fixture", encoding="utf-8")
                        raise failure()
                self.assertFalse(runtime.exists())

    def test_existing_datadir_is_preserved_and_no_native_process_is_started(self):
        with tempfile.TemporaryDirectory() as temporary:
            runtime = Path(temporary).resolve()
            data = runtime / "mysql-data"
            data.mkdir()
            marker = data / "preserve.txt"
            marker.write_text("preserve", encoding="utf-8")
            instance = runner.OwnedMySQL(Path("unused.exe"), runtime, {})
            with patch.object(runner, "WindowsJob") as job:
                with self.assertRaisesRegex(runner.shared.RunFailure, "existing MySQL datadir"):
                    instance.__enter__()
                job.assert_not_called()
            self.assertEqual(marker.read_text(encoding="utf-8"), "preserve")

    def test_port_race_or_foreign_datadir_rejected_before_mutation(self):
        with tempfile.TemporaryDirectory() as temporary:
            expected = Path(temporary).resolve()
            foreign = expected / "other"
            foreign.mkdir()
            valid = (str(expected), 54321, "8.2.0", "InnoDB", "REPEATABLE-READ")
            runner.validate_instance(valid, expected, 54321)
            cases = [(str(foreign), *valid[1:]), (str(expected), 54322, *valid[2:]),
                     (str(expected), 54321, "10.6.0-MariaDB", *valid[3:]),
                     (str(expected), 54321, "8.0.15", *valid[3:]),
                     (*valid[:3], "MyISAM", valid[4]), (*valid[:4], "READ-COMMITTED")]
            for values in cases:
                with self.subTest(values=values[1:]), self.assertRaises(runner.shared.RunFailure):
                    runner.validate_instance(values, expected, 54321)

    def test_unverified_instance_cleanup_never_connects_or_shuts_down_port(self):
        instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
        instance.process = Mock()
        instance.process.poll.return_value = None
        instance.job = Mock()
        with patch.object(instance, "_connect") as connect:
            instance.__exit__(RuntimeError, RuntimeError(), None)
            connect.assert_not_called()
        instance.job.close.assert_called_once()

    def test_failed_create_confers_no_drop_ownership(self):
        instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
        instance.identity_verified = True
        instance.process = Mock()
        instance.process.poll.return_value = None
        connection = Mock()
        connection.__enter__ = Mock(return_value=connection)
        connection.__exit__ = Mock(return_value=False)
        cursor = Mock()
        connection.cursor.return_value.__enter__ = Mock(return_value=cursor)
        connection.cursor.return_value.__exit__ = Mock(return_value=False)
        with patch.object(instance, "_connect", return_value=connection), patch.object(instance, "_verify"):
            self.assertTrue(instance._cleanup_database())
        self.assertEqual([call.args[0] for call in cursor.execute.call_args_list], ["SHUTDOWN"])

    def test_cleanup_rechecks_identity_before_drop_or_shutdown(self):
        instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
        instance.database = "remote_work_test_m2http_" + "a" * 24
        instance.identity_verified = True
        instance.process = Mock()
        instance.process.poll.return_value = None
        connection = Mock()
        connection.__enter__ = Mock(return_value=connection)
        connection.__exit__ = Mock(return_value=False)
        with patch.object(instance, "_connect", return_value=connection), patch.object(
                instance, "_verify", side_effect=runner.shared.RunFailure("port changed")):
            with self.assertRaises(runner.shared.RunFailure):
                instance._cleanup_database()
        connection.cursor.assert_not_called()

    def test_cleanup_only_drops_successfully_recorded_generated_name(self):
        instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
        instance.database = "remote_work_test_m2http_" + "a" * 24
        expected = instance.database
        instance.identity_verified = True
        instance.process = Mock()
        instance.process.poll.return_value = None
        connection = Mock()
        connection.__enter__ = Mock(return_value=connection)
        connection.__exit__ = Mock(return_value=False)
        cursor = Mock()
        connection.cursor.return_value.__enter__ = Mock(return_value=cursor)
        connection.cursor.return_value.__exit__ = Mock(return_value=False)
        with patch.object(instance, "_connect", return_value=connection), patch.object(instance, "_verify"):
            self.assertTrue(instance._cleanup_database())
        self.assertEqual([call.args[0] for call in cursor.execute.call_args_list],
                         ["DROP DATABASE `" + expected + "`", "SHUTDOWN"])
        self.assertIsNone(instance.database)

    def test_cleanup_rejects_non_generated_database_name(self):
        instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
        instance.database = "remote_work"
        instance.identity_verified = True
        instance.process = Mock()
        instance.process.poll.return_value = None
        connection = Mock()
        connection.__enter__ = Mock(return_value=connection)
        connection.__exit__ = Mock(return_value=False)
        cursor = Mock()
        connection.cursor.return_value.__enter__ = Mock(return_value=cursor)
        connection.cursor.return_value.__exit__ = Mock(return_value=False)
        with patch.object(instance, "_connect", return_value=connection), patch.object(instance, "_verify"):
            with self.assertRaises(runner.shared.RunFailure):
                instance._cleanup_database()
        cursor.execute.assert_not_called()

    def test_real_settings_and_python_injection_do_not_reach_child_environment(self):
        with runner.owned_runtime() as runtime, patch.dict(os.environ, {
                "DATABASE_URL": "do-not-contact-real-database", "MYSQL_HOST": "do-not-contact-host",
                "MYSQL_TEST_URL": "do-not-contact-test-database", "MYSQL_PWD": "do-not-forward",
                "PYTHONPATH": "do-not-import", "REMOTEWORK_API_URL": "do-not-contact-api",
                "HTTP_PROXY": "do-not-contact-proxy"}):
            env = runner.shared.isolated_environment(runtime)
            env.pop("DATABASE_URL", None)
            for key in ("DATABASE_URL", "MYSQL_HOST", "MYSQL_TEST_URL", "MYSQL_PWD", "PYTHONPATH", "HTTP_PROXY"):
                self.assertNotIn(key, env)
            self.assertEqual(env["PYTHON_DOTENV_DISABLED"], "1")
            self.assertEqual(env["REMOTEWORK_API_URL"], "http://127.0.0.1:1/")

    def test_native_spawn_assignment_failure_or_interrupt_stops_returned_process(self):
        for failure in (runner.shared.RunFailure("assign failed"), KeyboardInterrupt()):
            with self.subTest(failure=type(failure).__name__):
                instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
                instance.job = Mock()
                instance.job.assign.side_effect = failure
                process = Mock()
                process.poll.return_value = None
                with patch.object(runner.subprocess, "Popen", return_value=process) as launch:
                    with self.assertRaises(type(failure)):
                        instance._spawn(["unused.exe"])
                self.assertTrue(launch.call_args.kwargs["creationflags"] & runner.CREATE_SUSPENDED)
                self.assertIn(process, instance.native_processes)
                process.terminate.assert_called_once()
                process.wait.assert_called_once()

    def test_ctrl_c_during_popen_is_deferred_until_handle_is_tracked(self):
        instance = runner.OwnedMySQL(Path("unused.exe"), Path("unused"), {})
        instance.job = Mock()
        process = Mock()
        process.poll.return_value = None
        previous_handler = signal.getsignal(signal.SIGINT)
        def spawn(*_args, **_kwargs):
            signal.raise_signal(signal.SIGINT)
            return process
        with patch.object(runner.subprocess, "Popen", side_effect=spawn):
            with self.assertRaises(KeyboardInterrupt):
                instance._spawn(["unused.exe"])
        self.assertIn(process, instance.native_processes)
        instance.job.assign.assert_called_once_with(process)
        process.terminate.assert_called_once()
        process.wait.assert_called_once()
        self.assertEqual(signal.getsignal(signal.SIGINT), previous_handler)

    def test_csharp_interrupt_or_timeout_closes_owned_job_and_process(self):
        for failure in (KeyboardInterrupt(), subprocess.TimeoutExpired("synthetic-client", 1)):
            with self.subTest(failure=type(failure).__name__):
                job, process = Mock(), Mock()
                process.poll.return_value = None
                process.communicate.side_effect = failure
                with patch.object(runner, "WindowsJob", return_value=job), patch.object(
                        runner.subprocess, "Popen", return_value=process) as launch:
                    with self.assertRaises(type(failure)):
                        runner.run_owned_client(["synthetic-client"], capture_output=True, timeout=1)
                self.assertTrue(launch.call_args.kwargs["creationflags"] & runner.CREATE_SUSPENDED)
                job.assign.assert_called_once_with(process)
                job.close.assert_called_once()
                process.terminate.assert_called_once()
                process.wait.assert_called_once()

    def test_legacy_default_fixture_script_is_unchanged(self):
        default = runner.shared.FixtureServer({}, Path("unused"), {})
        self.assertEqual(default.server_script, Path(runner.shared.__file__).resolve())
        override = runner.shared.FixtureServer({}, Path("unused"), {}, server_script=runner.SCRIPT)
        self.assertEqual(override.server_script, runner.SCRIPT)

    def test_reserved_ddl_is_three_creates_with_no_version_write(self):
        statements = runner._ddl_statements()
        self.assertEqual(len(statements), 3)
        self.assertTrue(all(statement.startswith("CREATE TABLE ") for statement in statements))


if __name__ == "__main__":
    unittest.main()
