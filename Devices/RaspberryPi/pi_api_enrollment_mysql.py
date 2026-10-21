"""Explicit MySQL-only candidate entry. No service install or evidence migration.

Version-1 configurations and directories with legacy DB evidence fail closed.
The same private state/runtime.lock coordinates the historical service owner.
"""

import argparse
from contextlib import contextmanager
import json
import os
from pathlib import Path
import sys

from fingerprint_enrollment_approval import suspend_aware_clock
from fingerprint_enrollment_private import (
    ConfigurationError, _directory, _open_private, _read_private,
    parse_configuration, require_linux,
)
from fingerprint_enrollment_mysql import CONTRACT
from fingerprint_enrollment_runtime import MySqlEnrollmentRuntime
from fingerprint_enrollment_wire import parse_json


def load_mysql_configuration(path):
    require_linux()
    try:
        path = Path(path)
        _directory(path.parent)
        value = parse_json(_read_private(path), 16384)
        if type(value.get("version")) is not int or value["version"] != 2 or value.pop("contract", None) != CONTRACT:
            raise ConfigurationError("mysql_configuration_required")
        value["version"] = 1  # Reuse strict key/profile parsing; no state DB is opened.
        config = parse_configuration(json.dumps(value).encode("utf-8"))
        if config.state_directory != path.parent / "state":
            raise ConfigurationError("shared_runtime_directory_required")
        return config
    except ConfigurationError:
        raise
    except Exception:
        raise ConfigurationError("invalid_mysql_configuration") from None


@contextmanager
def mysql_runtime_session(config):
    require_linux()
    import fcntl
    fd = None
    previous = os.umask(0o077)
    try:
        root = config.state_directory
        _directory(root)
        fd = _open_private(root / "runtime.lock", os.O_RDWR)
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise ConfigurationError("state_in_use") from None
        # Never initialize, delete, repair, or silently ignore the old databases.
        if any(".sqlite3" in child.name for child in root.iterdir()):
            raise ConfigurationError("legacy_evidence_migration_required")
        yield
    except ConfigurationError:
        raise
    except Exception:
        raise ConfigurationError("mysql_runtime_unavailable") from None
    finally:
        if fd is not None:
            os.close(fd)
        os.umask(previous)


def run(argv=None):
    parser = argparse.ArgumentParser(description="MySQL 전용 지문 등록: 검토 후 전환용")
    parser.add_argument("--config", required=True)
    parser.add_argument("--check-config", action="store_true")
    args = parser.parse_args(argv)
    try:
        config = load_mysql_configuration(args.config)
        with mysql_runtime_session(config):
            suspend_aware_clock()
            if args.check_config:
                print("MySQL 전용 로컬 설정 검사 통과. 서버 활성화/이관/실물 검증은 별도입니다.")
                return 0
            from pi_api import main
            main(enrollment_factory=lambda service: MySqlEnrollmentRuntime(
                service, keys=config.keys, profile=config.profile))
        return 0
    except KeyboardInterrupt:
        return 0
    except Exception as error:
        code = str(error) if isinstance(error, ConfigurationError) else "mysql_startup_failed"
        print("등록 서비스 중단: " + code, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(run())
