"""Actual single-service host; root-measured one-invocation handoff is required."""
import argparse
from pathlib import Path
import sys
import threading

from fingerprint_enrollment_private import ConfigurationError
from fingerprint_enrollment_runtime import MySqlEnrollmentRuntime
from identity_private import load_identity_configuration
from identity_restart import load_restart_bundle, MANIFEST_FILES
from identity_runtime import IdentityRuntime
from identity_startup import (
    StartupReadiness, inputs_current, process_stamp, read_root_document, validate_handoff,
)
from pi_api_enrollment_mysql import load_mysql_configuration, mysql_runtime_session


class HostedIdentity:
    def __init__(self, runtime, readiness, profile):
        self.runtime, self.readiness = runtime, readiness
        self.profile = profile
        self._stop = threading.Event()
        self._watcher = threading.Thread(target=self._watch, name="identity-startup-watch", daemon=True)

    def mount(self, app):
        self.runtime.mount(app)
        self._watcher.start()

    def _watch(self):
        while not self._stop.wait(0.25):
            if not self.readiness(self.profile):
                self.readiness.close()
                # Cancels workers even if the PC has disconnected and no poll
                # reaches IdentityRuntime.before_request. The host still owns
                # final drain/UART close; no scan or runtime is recreated here.
                try:
                    self.runtime.close()
                except Exception:
                    pass
                return

    def close(self):
        self._stop.set()
        self.readiness.close()
        if self._watcher.ident is not None and self._watcher is not threading.current_thread():
            self._watcher.join(4)
            if self._watcher.is_alive():
                raise RuntimeError("Identity startup watcher still draining")
        return self.runtime.close()


def run(argv=None):
    parser = argparse.ArgumentParser(description="검토된 단일 등록·지문 인증 서비스")
    parser.add_argument("--config", required=True)
    parser.add_argument("--identity-config", required=True)
    parser.add_argument("--restart-bundle", required=True)
    parser.add_argument("--reviewed-server-sources", required=True)
    parser.add_argument("--handoff", required=True)
    args = parser.parse_args(argv)
    try:
        config = load_mysql_configuration(args.config)
        profile = load_identity_configuration(args.identity_config, config)
        sources, _ = read_root_document(args.reviewed_server_sources)
        observed = load_restart_bundle(args.restart_bundle, profile, expected_sources=sources)
        profile = load_identity_configuration(args.identity_config, config, retired_keys=observed.retired_keys)
        document, evidence_sha = read_root_document(args.handoff)
        binding = validate_handoff(document, observed, profile, process_stamp())
        required = {str(Path(name).absolute()) for name in
                    (args.config, args.identity_config, args.reviewed_server_sources, __file__)}
        required |= {str((Path(args.restart_bundle) / name).absolute()) for name in MANIFEST_FILES | {"SHA256SUMS.txt"}}
        if not required <= {name for name, _ in binding.inputs} or not inputs_current(binding):
            raise ConfigurationError("identity_startup_inputs_changed")
        with mysql_runtime_session(config) as lease:
            if not lease.current():
                raise ConfigurationError("identity_shared_lease_required")
            from pi_api import main

            def identity(service):
                ready = StartupReadiness(binding, profile, lease, service,
                    evidence_path=args.handoff, evidence_sha256=evidence_sha)
                if not ready(profile):
                    ready.close()
                    raise ConfigurationError("identity_process_binding_failed")
                return HostedIdentity(IdentityRuntime(service, profile, ready=ready), ready, profile)

            main(enrollment_factory=lambda service: MySqlEnrollmentRuntime(
                     service, keys=config.keys, profile=config.profile), identity_factory=identity)
        return 0
    except KeyboardInterrupt:
        return 0
    except Exception:
        print("STOPPED: identity_service_startup_not_reconciled; no automatic fallback.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(run())
