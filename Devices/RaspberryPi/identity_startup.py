"""Process-bound readiness from reviewed server observation AND root OS handoff.

No operator ready boolean, new key, DB, HTTP route or template write. The root
transition tool records actual stop/exit/lock evidence and claims ONE systemd
invocation. A transferred server observation can never create this evidence.
"""
from dataclasses import dataclass, field
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import threading

from fingerprint_enrollment_private import ConfigurationError, _no_symlinks
import fingerprint_identity_wire as wire


def read_root_document(path):
    """Root-owned, service-readable evidence; service user cannot rewrite parent."""
    path = Path(path)
    _no_symlinks(path)
    for parent in (path.parent, path.parent.parent):
        info = parent.stat()
        if info.st_uid != 0 or stat.S_IMODE(info.st_mode) & 0o022:
            raise ConfigurationError("identity_root_evidence_required")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(fd, "rb") as stream:
        info = os.fstat(stream.fileno())
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or info.st_nlink != 1
                or stat.S_IMODE(info.st_mode) & 0o027):
            raise ConfigurationError("identity_root_evidence_required")
        raw = stream.read(65537)
    return wire.parse_json(raw, 65536), hashlib.sha256(raw).hexdigest()


def process_stamp():
    boot = Path("/proc/sys/kernel/random/boot_id").read_text().strip()
    wire.canonical_uuid(boot)
    invocation = os.environ.get("INVOCATION_ID", "")
    if not re.fullmatch("[0-9a-f]{32}", invocation):
        raise ConfigurationError("identity_systemd_invocation_required")
    start = Path("/proc/self/stat").read_text().rsplit(") ", 1)[1].split()[19]
    return boot, invocation, os.getpid(), start


def profile_digest(profile):
    # Private hash only. Keys are excluded from repr/stdout but their exact bytes
    # participate so an epoch-preserving profile replacement also closes admission.
    values = {name: getattr(profile, name) for name in
              ("device_id", "key_id", "key_epoch", "capacity", "minimum_slot", "maximum_slot")}
    values["keys"] = {name: value.hex() for name, value in profile.keys.items()}
    return hashlib.sha256(json.dumps(values, sort_keys=True).encode()).hexdigest()


def inputs_current(binding):
    try:
        for name, digest in binding.inputs:
            path = Path(name)
            _no_symlinks(path)
            if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                return False
        return True
    except Exception:
        return False


@dataclass(frozen=True)
class StartupBinding:
    cycle_id: str = field(repr=False)
    boot_id: str = field(repr=False)
    invocation_id: str = field(repr=False)
    profile_sha256: str = field(repr=False)
    inputs: tuple = field(repr=False)


def validate_handoff(document, observed, profile, stamp):
    """Root handoff contains measured OS facts, not an admission-ready setting."""
    try:
        fields = {"version", "cycle_id", "boot_id", "invocation_id", "previous_pid", "previous_invocation_id",
                  "previous_entry_sha256", "previous_exit_status", "previous_result", "previous_cgroup_empty",
                  "lock_available_after_stop", "profile_sha256", "inputs"}
        if type(document) is not dict or set(document) != fields or type(document["version"]) is not int or document["version"] != 1:
            raise ValueError()
        wire.canonical_uuid(document["cycle_id"])
        wire.canonical_uuid(document["boot_id"])
        if (document["cycle_id"] != observed.cycle_id or not observed.sources_compared
                or observed.identity_enabled is not True or observed.unresolved_challenges != 0
                or document["boot_id"] != stamp[0] or document["invocation_id"] != stamp[1]
                or type(document["previous_pid"]) is not int or document["previous_pid"] <= 0
                or document["previous_pid"] == stamp[2]
                or not re.fullmatch("[0-9a-f]{32}", document["previous_invocation_id"])
                or document["previous_invocation_id"] == document["invocation_id"]
                or not re.fullmatch("[0-9a-f]{64}", document["previous_entry_sha256"])
                or type(document["previous_exit_status"]) is not int or document["previous_exit_status"] != 0
                or document["previous_result"] != "success" or document["previous_cgroup_empty"] is not True
                or document["lock_available_after_stop"] is not True
                or document["profile_sha256"] != profile_digest(profile)):
            raise ValueError()
        inputs = document["inputs"]
        if type(inputs) is not dict or not inputs or len(inputs) > 64:
            raise ValueError()
        for name, digest in inputs.items():
            path = Path(name)
            if (not path.is_absolute() or ".." in path.parts or type(digest) is not str
                    or not re.fullmatch("[0-9a-f]{64}", digest)):
                raise ValueError()
        return StartupBinding(observed.cycle_id, stamp[0], stamp[1], document["profile_sha256"], tuple(sorted(inputs.items())))
    except Exception:
        raise ConfigurationError("identity_startup_reconciliation_required") from None


class StartupReadiness:
    """Synchronous checks only; false/exception/close permanently seals this host."""
    def __init__(self, binding, profile, lease, service, *, evidence_path, evidence_sha256,
                 stamp_reader=process_stamp, root_reader=read_root_document):
        self._binding, self._profile, self._lease, self._service = binding, profile, lease, service
        self._evidence_path, self._evidence_sha256 = evidence_path, evidence_sha256
        self._stamp_reader, self._root_reader = stamp_reader, root_reader
        self._stamp = stamp_reader()
        self._closed, self._lock = False, threading.Lock()

    def __call__(self, profile):
        with self._lock:
            if self._closed:
                return False
            try:
                if (profile is not self._profile or profile_digest(profile) != self._binding.profile_sha256
                        or not self._lease.current() or self._service.stopping.is_set()
                        or self._stamp_reader() != self._stamp
                        or self._stamp[:2] != (self._binding.boot_id, self._binding.invocation_id)
                        or self._root_reader(self._evidence_path)[1] != self._evidence_sha256):
                    raise ValueError()
                if not inputs_current(self._binding):
                    raise ValueError()
                return True
            except Exception:
                self._closed = True
                return False

    def close(self):
        with self._lock:
            self._closed = True
