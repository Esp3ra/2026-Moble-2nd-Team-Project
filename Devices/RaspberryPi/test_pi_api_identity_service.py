import contextlib
from dataclasses import replace
import io
from pathlib import Path
import threading
import time
import unittest
from unittest.mock import Mock, patch
from types import SimpleNamespace

import pi_api_identity_service as host
from identity_startup import StartupBinding
from identity_restart import MANIFEST_FILES
import test_identity_startup as startup_fixtures
import test_identity_runtime as runtime_fixtures
from identity_runtime import IdentityRuntime
from identity_startup import profile_digest


class ServiceHostTests(unittest.TestCase):
    args = ["--config", "config.json", "--identity-config", "identity.json", "--restart-bundle", "bundle",
            "--reviewed-server-sources", "sources.json", "--handoff", "handoff.json"]

    def setUp(self):
        self.fixture = startup_fixtures.StartupTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.profile, self.observed = self.fixture.profile, self.fixture.observed
        self.config = SimpleNamespace(keys=object(), profile=object())
        required = {str(Path(name).absolute()) for name in ("config.json", "identity.json", "sources.json", host.__file__)}
        required |= {str((Path("bundle") / name).absolute()) for name in MANIFEST_FILES | {"SHA256SUMS.txt"}}
        self.binding = StartupBinding(self.observed.cycle_id, self.fixture.stamp[0], self.fixture.stamp[1],
                                      "f" * 64, tuple((name, "a" * 64) for name in required))

    def run_host(self):
        with contextlib.redirect_stderr(io.StringIO()) as errors:
            code = host.run(self.args)
        self.assertNotIn("PRIVATE_SECRET", errors.getvalue())
        return code

    @contextlib.contextmanager
    def setup(self, **overrides):
        values = dict(load_mysql_configuration=Mock(return_value=self.config),
            load_identity_configuration=Mock(return_value=self.profile),
            read_root_document=Mock(return_value=({}, "e" * 64)),
            load_restart_bundle=Mock(return_value=self.observed),
            process_stamp=Mock(return_value=tuple(self.fixture.stamp)),
            validate_handoff=Mock(return_value=self.binding), inputs_current=Mock(return_value=True),
            mysql_runtime_session=Mock(), StartupReadiness=Mock(), IdentityRuntime=Mock(), MySqlEnrollmentRuntime=Mock())
        values.update(overrides)
        with contextlib.ExitStack() as stack:
            for name, value in values.items(): stack.enter_context(patch.object(host, name, value))
            yield values

    def test_off_or_unresolved_observation_does_not_open_lease_uart_or_server(self):
        for observed in (replace(self.observed, identity_enabled=False), replace(self.observed, unresolved_challenges=1)):
            with self.setup(load_restart_bundle=Mock(return_value=observed),
                            validate_handoff=Mock(side_effect=RuntimeError("PRIVATE_SECRET"))) as values, patch("pi_api.main") as main:
                self.assertEqual(1, self.run_host())
                values["mysql_runtime_session"].assert_not_called()
                main.assert_not_called()

    def test_unpinned_required_input_cannot_be_replaced_by_valid_boolean(self):
        with self.setup(validate_handoff=Mock(return_value=replace(self.binding, inputs=()))) as values:
            self.assertEqual(1, self.run_host())
            values["mysql_runtime_session"].assert_not_called()

    def test_profile_and_root_input_failure_never_open_runtime(self):
        for name in ("load_identity_configuration", "read_root_document", "load_restart_bundle"):
            with self.setup(**{name: Mock(side_effect=ValueError("PRIVATE_SECRET"))}) as values:
                self.assertEqual(1, self.run_host())
                values["mysql_runtime_session"].assert_not_called()

    def test_same_lease_and_sensor_are_borrowed_and_readiness_is_closed_before_runtime(self):
        events = []
        lease = SimpleNamespace(current=lambda: True)
        @contextlib.contextmanager
        def session(config):
            self.assertIs(self.config, config)
            events.append("lease_acquire")
            yield lease
            events.append("lease_release")
        ready = Mock(return_value=True)
        ready.close.side_effect = lambda: events.append("ready_close")
        runtime = Mock()
        runtime.close.side_effect = lambda: events.append("runtime_close")
        service = SimpleNamespace(stopping=threading.Event())
        with self.setup(mysql_runtime_session=session, StartupReadiness=Mock(return_value=ready),
                        IdentityRuntime=Mock(return_value=runtime)) as values:
            def main(*, enrollment_factory, identity_factory):
                enrollment_factory(service)
                composed = identity_factory(service)
                composed.mount(object())
                composed.close()
                events.append("uart_closed_by_main")
            with patch("pi_api.main", main):
                self.assertEqual(0, self.run_host())
            values["MySqlEnrollmentRuntime"].assert_called_once_with(service, keys=self.config.keys, profile=self.config.profile)
            self.assertIs(lease, values["StartupReadiness"].call_args.args[2])
            self.assertIs(service, values["IdentityRuntime"].call_args.args[0])
        self.assertEqual(["lease_acquire", "ready_close", "runtime_close", "uart_closed_by_main", "lease_release"], events)

    def test_readiness_revocation_cancels_even_without_a_pc_poll(self):
        allowed = threading.Event(); allowed.set()
        ready = Mock(side_effect=lambda profile: allowed.is_set())
        closed = threading.Event()
        runtime = Mock(); runtime.close.side_effect = closed.set
        composed = host.HostedIdentity(runtime, ready, self.profile)
        try:
            composed.mount(object())
            allowed.clear()
            self.assertTrue(closed.wait(2))
            ready.close.assert_called()
        finally:
            composed.close()
        self.assertFalse(composed._watcher.is_alive())

    def test_changed_input_before_uart_is_rejected(self):
        with self.setup(inputs_current=Mock(return_value=False)) as values:
            self.assertEqual(1, self.run_host())
            values["mysql_runtime_session"].assert_not_called()


class ActualIdentityPipelineTests(unittest.TestCase):
    def setUp(self):
        self.env = runtime_fixtures.RuntimeTests()
        self.env.setUp()
        self.startup = startup_fixtures.StartupTests()
        self.startup.setUp()
        self.addCleanup(self.startup.doCleanups)
        self.startup.profile = self.env.env.profile
        self.startup.evidence["profile_sha256"] = profile_digest(self.env.env.profile)
        self.gate = self.startup.gate()
        self.runtime = IdentityRuntime(self.env.env.service, self.env.env.profile,
                                       ready=self.gate, clock=lambda: self.env.env.now)
        self.composed = host.HostedIdentity(self.runtime, self.gate, self.env.env.profile)
        self.composed.mount(self.env.app)
        self.addCleanup(self.composed.close)
        self.env.client = self.env.app.test_client()

    def test_actual_http_fresh_signed_approval_match_and_same_result_replay(self):
        self.assertEqual(200, self.env.start().status_code)
        final = self.env.finish()
        self.assertEqual("succeeded", final["state"])
        self.assertEqual(final["result_b64"], self.env.start().json["data"]["result_b64"])
        self.assertEqual(1, self.env.env.sensor.finger_fast_search.call_count)
        self.env.env.sensor.store_model.assert_not_called()
        self.env.env.sensor.delete_model.assert_not_called()

    def test_revoking_input_cancels_real_pending_job_without_pc_poll_or_sensor(self):
        self.assertEqual(200, self.env.start().status_code)
        self.env.wait(lambda r: r.json["data"]["state"] == "awaiting_approval")
        self.startup.file.write_bytes(b"revoked")
        deadline = time.monotonic() + 2
        while self.composed._watcher.is_alive() and time.monotonic() < deadline:
            time.sleep(0.01)
        self.assertFalse(self.composed._watcher.is_alive())
        self.assertEqual(503, self.env.client.get(self.env.path, headers=self.env.headers).status_code)
        self.startup.file.write_bytes(b"synthetic input")
        self.assertEqual(503, self.env.start().status_code)
        self.env.env.sensor.get_image.assert_not_called()
        self.assertFalse(self.env.env.service.lock.locked())
        self.assertEqual(200, self.env.client.get("/health").status_code)


if __name__ == "__main__": unittest.main()
