"""Read-only private identity configuration. No activation, keys or state creation."""
import argparse
import sys

from fingerprint_enrollment_private import (
    ConfigurationError, EnrollmentConfiguration, _directory, _read_private, require_linux,
)
from fingerprint_identity import IdentityProfile
import fingerprint_identity_wire as wire
from pathlib import Path


def parse_identity_configuration(raw, enrollment, *, retired_keys=()):
    """The trusted host supplies existing/retired keys, never the new JSON document."""
    try:
        if not isinstance(enrollment, EnrollmentConfiguration):
            raise ValueError()
        if type(retired_keys) is not tuple or any(type(k) is not bytes or len(k) < 32 for k in retired_keys):
            raise ValueError()
        value = wire.parse_json(raw, 16384)
        if (set(value) != {"version", "contract", "device_id", "key_id", "key_epoch", "profile", "keys"}
                or type(value["version"]) is not int or value["version"] != 2
                or value["contract"] != "identity-rw-id-v2"
                or value["device_id"] != enrollment.keys.device_id):
            raise ValueError()
        profile = value["profile"]
        if type(profile) is not dict or set(profile) != {"capacity", "minimum_slot", "maximum_slot"}:
            raise ValueError()
        if type(value["keys"]) is not dict or set(value["keys"]) != wire.KINDS:
            raise ValueError()
        keys = {kind: wire.b64decode(value["keys"][kind], 32) for kind in wire.KINDS}
        parsed = IdentityProfile(
            value["device_id"], value["key_id"], value["key_epoch"], True,
            profile["minimum_slot"], profile["maximum_slot"], profile["capacity"], keys,
            (enrollment.keys.request_key, enrollment.keys.result_key) + retired_keys,
        )
        if (parsed.capacity != enrollment.profile.expected_capacity
                or parsed.minimum_slot < enrollment.profile.minimum_slot
                or parsed.maximum_slot > enrollment.profile.maximum_slot):
            raise ValueError()
        return parsed
    except Exception:
        raise ConfigurationError("invalid_identity_configuration") from None


def load_identity_configuration(path, enrollment, *, retired_keys=()):
    try:
        require_linux()
        path = Path(path)
        _directory(path.parent)
        return parse_identity_configuration(_read_private(path), enrollment, retired_keys=retired_keys)
    except Exception:
        raise ConfigurationError("identity_private_configuration_unavailable") from None


def run(argv=None):
    parser = argparse.ArgumentParser(description="지문 본인 확인 설정 읽기 전용 검사")
    parser.add_argument("--config", required=True)
    parser.add_argument("--enrollment-config", required=True)
    parser.add_argument("--check-config", action="store_true", required=True)
    args = parser.parse_args(argv)
    try:
        from pi_api_enrollment_mysql import load_mysql_configuration
        enrollment = load_mysql_configuration(args.enrollment_config)
        load_identity_configuration(args.config, enrollment)
        print("PASS: private identity profile checked; no service/sensor/server/state changes.")
        print("NOT_READY: retired-key audit, restart reconciliation and real server composition remain separate.")
        return 0
    except Exception:
        print("STOPPED: identity_configuration_check_failed", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(run())
