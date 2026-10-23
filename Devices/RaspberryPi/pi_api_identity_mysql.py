"""Explicit single-service MySQL enrollment + identity entry; no readiness bypass."""
import argparse
import sys

from fingerprint_enrollment_private import ConfigurationError
from fingerprint_enrollment_runtime import MySqlEnrollmentRuntime
from identity_private import load_identity_configuration
from identity_runtime import IdentityRuntime
from pi_api_enrollment_mysql import load_mysql_configuration, mysql_runtime_session


def run(argv=None, *, ready=None, retired_keys=()):
    parser = argparse.ArgumentParser(description="MySQL 등록·본인 확인 단일 서비스")
    parser.add_argument("--config", required=True)
    parser.add_argument("--identity-config", required=True)
    parser.add_argument("--check-config", action="store_true")
    parser.add_argument("--restart-bundle", help="Private observation directory; check-config only, never an activation grant")
    args = parser.parse_args(argv)
    try:
        if args.restart_bundle and not args.check_config:
            raise ConfigurationError("restart_observation_check_only")
        config = load_mysql_configuration(args.config)
        profile = load_identity_configuration(args.identity_config, config, retired_keys=retired_keys)
        if args.check_config:
            if args.restart_bundle:
                from identity_restart import load_restart_bundle
                observed = load_restart_bundle(args.restart_bundle, profile)
                combined = tuple(dict.fromkeys(retired_keys + observed.retired_keys))
                load_identity_configuration(args.identity_config, config, retired_keys=combined)
                print("PASS: private restart observation/profile/inventory consistency checked.")
                print("CHECK: server identity=" + ("ON" if observed.identity_enabled else "OFF")
                      + "; unresolved=" + str(observed.unresolved_challenges)
                      + "; retained fences=" + str(observed.persisted_capture_fences))
                print("NOT_READY: observation is not a grant; source/runtime/reconciliation evidence remains separate.")
            print("PASS: private enrollment/identity profiles agree; no runtime/sensor/service changes.")
            print("NOT_READY: restart reconciliation and trusted readiness provider are separate.")
            return 0
        # The reviewed trusted host must provide actual restart reconciliation.
        # No CLI flag, JSON boolean, health response or elapsed wait can replace it.
        if ready is None or not callable(ready):
            raise ConfigurationError("identity_readiness_provider_required")
        with mysql_runtime_session(config):
            from pi_api import main
            main(enrollment_factory=lambda service: MySqlEnrollmentRuntime(
                service, keys=config.keys, profile=config.profile),
                identity_factory=lambda service: IdentityRuntime(service, profile, ready=ready))
        return 0
    except KeyboardInterrupt:
        return 0
    except Exception:
        print("STOPPED: identity_mysql_startup_not_ready; no automatic restart or fallback.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(run())
