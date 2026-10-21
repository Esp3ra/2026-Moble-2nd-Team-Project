"""Private keys/profile/file checks only. No database, state preparation or I/O to sensors."""

from dataclasses import dataclass, field
import os
from pathlib import Path
import stat
import sys

from fingerprint_enrollment import SensorProfile
from fingerprint_enrollment_wire import DirectionKeys, decode64, parse_json


class ConfigurationError(ValueError):
    """Fixed public error codes only; never echo configuration or key values."""


@dataclass(frozen=True)
class EnrollmentConfiguration:
    keys: DirectionKeys = field(repr=False)
    profile: SensorProfile
    state_directory: Path = field(repr=False)


def _fields(value, names):
    if type(value) is not dict or set(value) != set(names.split()):
        raise ConfigurationError("invalid_configuration")


def parse_configuration(raw):
    try:
        value = parse_json(raw, 16384)
        _fields(value, "version device_id key_id key_epoch request_key_b64 result_key_b64 profile state_directory")
        if type(value["version"]) is not int or value["version"] != 1:
            raise ValueError()
        epoch = value["key_epoch"]
        if type(epoch) is not int or not 1 <= epoch <= 9223372036854775807:
            raise ValueError()
        request = decode64(value["request_key_b64"], 32)
        result = decode64(value["result_key_b64"], 32)
        if len(request) != 32 or len(result) != 32:
            raise ValueError()
        keys = DirectionKeys(value["device_id"], value["key_id"], str(epoch), request, result)
        _fields(value["profile"], "expected_capacity minimum_slot maximum_slot")
        profile = SensorProfile(**value["profile"])
        path = value["state_directory"]
        if (type(path) is not str or not path or "\x00" in path or not Path(path).is_absolute()
                or ".." in Path(path).parts or Path(path) == Path(path).parent):
            raise ValueError()
        return EnrollmentConfiguration(keys, profile, Path(path))
    except Exception:
        raise ConfigurationError("invalid_configuration") from None


def require_linux():
    if not sys.platform.startswith("linux") or os.name != "posix":
        raise ConfigurationError("linux_service_required")


def _no_symlinks(path):
    if not path.is_absolute() or ".." in path.parts:
        raise ConfigurationError("absolute_private_path_required")
    for part in (path, *path.parents):
        if part.is_symlink():
            raise ConfigurationError("private_path_required")


def _directory(path):
    _no_symlinks(path)
    info = path.stat()
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid()
            or stat.S_IMODE(info.st_mode) & 0o077):
        raise ConfigurationError("private_directory_required")


def _open_private(path, flags=os.O_RDONLY):
    _no_symlinks(path)
    fd = os.open(path, flags | os.O_NOFOLLOW | os.O_NONBLOCK)
    try:
        info = os.fstat(fd)
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid()
                or stat.S_IMODE(info.st_mode) & 0o077 or info.st_nlink != 1):
            raise ConfigurationError("private_file_required")
    except Exception:
        os.close(fd)
        raise
    return fd


def _read_private(path, maximum=16384):
    fd = _open_private(path)
    with os.fdopen(fd, "rb") as stream:
        raw = stream.read(maximum + 1)
    if len(raw) > maximum:
        raise ConfigurationError("private_file_too_large")
    return raw
