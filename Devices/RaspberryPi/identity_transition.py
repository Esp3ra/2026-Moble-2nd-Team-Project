"""Root-owned single-service transition/rollback. Standard library only.

Plan is read-only. Apply refuses OFF/unresolved observations BEFORE stopping the
old service. It never edits keys, the existing release/hold, templates or DB rows.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import time

SERVICE = "attendance-device.service"
DROPIN = Path("/etc/systemd/system/attendance-device.service.d/80-member5-identity.conf")
BOOT = Path("/proc/sys/kernel/random/boot_id")


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def root_directory(path, mode, *, exist_ok=True, gid=None):
    path.mkdir(parents=True, mode=mode, exist_ok=exist_ok)
    for parent in (path, *path.parents):
        info = parent.stat()
        if parent.is_symlink() or info.st_uid != 0 or stat.S_IMODE(info.st_mode) & 0o022:
            raise RuntimeError("root_directory_required")
    if stat.S_IMODE(path.stat().st_mode) != mode:
        raise RuntimeError("root_directory_mode")
    if gid is not None: os.chown(path, 0, gid)


def read(path, maximum=65536):
    path = Path(path)
    if not path.is_absolute() or ".." in path.parts or any(p.is_symlink() for p in (path, *path.parents)):
        raise RuntimeError("unsafe_path")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(fd, "rb") as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
            raise RuntimeError("regular_single_link_required")
        raw = stream.read(maximum + 1)
    if len(raw) > maximum:
        raise RuntimeError("file_limit")
    return raw


def write_new_or_same(path, raw, *, mode=0o640, gid=None):
    path = Path(path)
    if path.exists() or path.is_symlink():
        if read(path, max(len(raw), 65536)) != raw:
            raise RuntimeError("existing_file_differs")
        info = path.stat()
        if info.st_uid != 0 or stat.S_IMODE(info.st_mode) != mode or (gid is not None and info.st_gid != gid):
            raise RuntimeError("existing_root_file_permissions")
        return
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, mode)
    with os.fdopen(fd, "wb") as stream:
        stream.write(raw); stream.flush(); os.fsync(stream.fileno())
    if gid is not None:
        os.chown(path, 0, gid)
    os.chmod(path, mode)


def command(args, *, timeout=60):
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout, check=True).stdout


def properties():
    fields = "User MainPID ActiveState SubState InvocationID ControlGroup Result ExecMainCode ExecMainStatus KillSignal TimeoutStopUSec".split()
    text = command(["systemctl", "show", SERVICE, "--no-pager"] + [arg for field in fields for arg in ("-p", field)])
    return dict(line.split("=", 1) for line in text.splitlines() if "=" in line)


def cgroup_empty(group):
    if not Path("/sys/fs/cgroup/cgroup.controllers").exists():
        raise RuntimeError("unified_cgroup_required")
    if not group.startswith("/") or ".." in Path(group).parts:
        raise RuntimeError("cgroup_path")
    path = Path("/sys/fs/cgroup") / group.lstrip("/") / "cgroup.procs"
    return not path.exists() or not path.read_text().strip()


def require_stopped(group, *, clean):
    state = properties()
    if (state["MainPID"] != "0" or state["ActiveState"] not in ("inactive", "failed") or not cgroup_empty(group)):
        raise RuntimeError("service_not_drained_no_fallback")
    if clean and (state["Result"] != "success" or state["ExecMainCode"] != "1" or state["ExecMainStatus"] != "0"):
        raise RuntimeError("unclean_stop_no_fallback")
    if not clean and state["ExecMainCode"] not in ("0", "1"):
        raise RuntimeError("signal_killed_no_fallback")
    return state


def switch_service(ops):
    """Transaction boundary: install only after measured clean stop; no blind fallback."""
    ops.preflight()
    ops.backup()
    ops.stop_previous()
    ops.verify_previous_drained()
    try:
        ops.install()
        ops.start_candidate()
        ops.verify_candidate()
    except Exception:
        ops.stop_candidate()
        ops.verify_candidate_drained()
        ops.restore_previous()
        ops.start_previous()
        ops.verify_previous()
        raise RuntimeError("candidate_failed_previous_restored") from None


def require_server_ready(observed):
    if observed["identity_enabled"] is not True or type(observed["unresolved"]) is not int or observed["unresolved"] != 0:
        raise RuntimeError("server_off_or_original_work_unresolved_no_stop")


PROBE = r'''
import hashlib,json,sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0,sys.argv[1])
from pi_api_enrollment_mysql import load_mysql_configuration
from identity_private import load_identity_configuration
from identity_restart import load_restart_bundle
from identity_startup import profile_digest
c=load_mysql_configuration(sys.argv[2]);p=load_identity_configuration(sys.argv[3],c)
o=load_restart_bundle(sys.argv[4],p,expected_sources=json.loads(Path(sys.argv[5]).read_text()))
p=load_identity_configuration(sys.argv[3],c,retired_keys=o.retired_keys)
print(json.dumps(dict(cycle_id=o.cycle_id,profile_sha256=profile_digest(p),identity_enabled=o.identity_enabled,
 unresolved=o.unresolved_challenges,state_directory=str(c.state_directory))))
'''


class LinuxTransition:
    def __init__(self, manifest_path, manifest_sha):
        import pwd
        if os.geteuid() != 0:
            raise RuntimeError("root_apply_required")
        raw = read(manifest_path)
        if digest(raw) != manifest_sha:
            raise RuntimeError("manifest_changed")
        self.manifest = json.loads(raw)
        m = self.manifest
        if (set(m) != {"version", "source_commit", "files", "user", "python", "config", "identity_config",
                       "bundle_directory", "server_sources", "previous_entry", "previous_entry_sha256"}
                or type(m["version"]) is not int or m["version"] != 1
                or not re.fullmatch("[0-9a-f]{40}", m["source_commit"]) or not 3 <= len(m["files"]) <= 40):
            raise RuntimeError("manifest_fields")
        self.source = Path(manifest_path).parent / "code"
        self.owner = pwd.getpwnam(m["user"])
        self.code = Path("/opt/remotework-device") / m["source_commit"]
        self.handoff_dir = Path("/run/remotework-identity")
        self.handoff = self.handoff_dir / "handoff.json"
        self.backup_path = Path("/var/lib/remotework-device/transitions") / (m["source_commit"] + "-" + manifest_sha[:12])
        self.changed = False
        self.handoff_written = False

    def hold(self):
        m = self.manifest
        root = Path(m["config"]).parent
        hold = Path("/etc/systemd/system/attendance-device.service.d/90-member5-recovery-hold.conf")
        raw = read(hold)
        if (hold.stat().st_uid != 0 or stat.S_IMODE(hold.stat().st_mode) & 0o022
                or b"ConditionPathExists=" + str(root / "recovery-writer-release").encode() not in raw.splitlines()):
            raise RuntimeError("existing_hold_changed")
        release = root / "recovery-writer-release"
        info = release.stat()
        if (info.st_uid != self.owner.pw_uid or stat.S_IMODE(info.st_mode) != 0o600
                or digest(read(release)) != "fdd5a7dec4ee5a1886f2fde7894d98455cd64d55f2c473d543aa5d19a3a37ece"):
            raise RuntimeError("existing_release_changed")

    def preflight(self):
        m = self.manifest
        self.before = properties()
        if (self.before["User"] != m["user"] or self.before["ActiveState"] != "active" or DROPIN.exists() or DROPIN.is_symlink()
                or self.before["KillSignal"] not in ("2", "SIGINT") or self.before["TimeoutStopUSec"] != "45s"):
            raise RuntimeError("unexpected_service_owner_or_override")
        self.old_pid = int(self.before["MainPID"])
        argv = (Path("/proc") / str(self.old_pid) / "cmdline").read_bytes().split(b"\0")
        if (m["previous_entry"].encode() not in argv or m["config"].encode() not in argv
                or digest(read(m["previous_entry"], 131072)) != m["previous_entry_sha256"]):
            raise RuntimeError("previous_entry_changed")
        self.hold()
        self.previous_hashes = {name: digest(read(name, 131072)) for name in
                                (m["config"], m["identity_config"], m["previous_entry"])}
        # Root immutable source installation; no user code is imported by root.
        root_directory(self.code, 0o755)
        for parent in (self.code, self.code.parent):
            if parent.is_symlink() or parent.stat().st_uid != 0 or stat.S_IMODE(parent.stat().st_mode) & 0o022:
                raise RuntimeError("root_code_directory")
        for name, expected in m["files"].items():
            if not re.fullmatch("[a-z_]+[.]py", name) or not re.fullmatch("[0-9a-f]{64}", expected):
                raise RuntimeError("source_name")
            raw = read(self.source / name, 131072)
            if digest(raw) != expected:
                raise RuntimeError("source_changed")
            write_new_or_same(self.code / name, raw, mode=0o644)
        for required in ("identity_transition.py", "pi_api_identity_service.py", "identity_startup.py"):
            if required not in m["files"]:
                raise RuntimeError("source_incomplete")
        sources = self.code / "server-sources.json"
        write_new_or_same(sources, json.dumps(m["server_sources"], sort_keys=True).encode(), gid=self.owner.pw_gid)
        out = command(["runuser", "-u", m["user"], "--", m["python"], "-B", "-c", PROBE,
                       str(self.code), m["config"], m["identity_config"], m["bundle_directory"], str(sources)])
        self.observed = json.loads(out)
        # Explicit new server ON observation AND real OS handoff are both needed.
        require_server_ready(self.observed)
        if properties() != self.before:
            raise RuntimeError("service_changed_during_preflight")

    def backup(self):
        root_directory(self.backup_path, 0o700, exist_ok=False)
        write_new_or_same(self.backup_path / "before.json", json.dumps(self.before).encode(), mode=0o600)
        write_new_or_same(self.backup_path / "manifest.json", json.dumps(self.manifest).encode(), mode=0o600)
        write_new_or_same(self.backup_path / "observed.json", json.dumps(self.observed).encode(), mode=0o600)
        write_new_or_same(self.backup_path / "previous-inputs.json", json.dumps(self.previous_hashes).encode(), mode=0o600)
        self.hold_hash = digest(read(Path("/etc/systemd/system/attendance-device.service.d/90-member5-recovery-hold.conf")))
        write_new_or_same(self.backup_path / "hold.sha256", self.hold_hash.encode(), mode=0o600)

    def stop_previous(self):
        command(["systemctl", "stop", SERVICE])

    def verify_previous_drained(self):
        self.stopped = require_stopped(self.before["ControlGroup"], clean=True)
        if (Path("/proc") / str(self.old_pid)).exists():
            raise RuntimeError("previous_pid_still_present")
        self.lock_available()

    def lock_available(self):
        import fcntl
        lock = Path(self.observed["state_directory"]) / "runtime.lock"
        fd = os.open(lock, os.O_RDWR | os.O_NOFOLLOW | os.O_NONBLOCK)
        try:
            info = os.fstat(fd)
            if (not stat.S_ISREG(info.st_mode) or info.st_uid != self.owner.pw_uid
                    or stat.S_IMODE(info.st_mode) != 0o600 or info.st_nlink != 1):
                raise RuntimeError("runtime_lock_changed")
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        finally:
            os.close(fd)

    def install(self):
        m = self.manifest
        self.hold()
        root_directory(self.handoff_dir, 0o750, gid=self.owner.pw_gid)
        if (self.handoff_dir.is_symlink() or self.handoff_dir.stat().st_uid != 0
                or stat.S_IMODE(self.handoff_dir.stat().st_mode) & 0o022 or self.handoff.exists()):
            raise RuntimeError("handoff_existing_or_unsafe")
        os.chown(self.handoff_dir, 0, self.owner.pw_gid)
        inputs = {str(self.code / name): expected for name, expected in m["files"].items()}
        paths = [m["config"], m["identity_config"], str(self.code / "server-sources.json")]
        paths += [str(Path(m["bundle_directory"]) / name) for name in (
            "deployment-context.private.json", "managed-key-audit.private.json", "restart-observation.private.json",
            "retired-identity-keys.private.json", "SHA256SUMS.txt", "전달_안내.txt")]
        inputs.update({name: digest(read(name, 131072)) for name in paths})
        evidence = dict(version=1, cycle_id=self.observed["cycle_id"], boot_id=BOOT.read_text().strip(), invocation_id="",
            previous_pid=self.old_pid, previous_invocation_id=self.before["InvocationID"],
            previous_entry_sha256=m["previous_entry_sha256"], previous_exit_status=0, previous_result="success",
            previous_cgroup_empty=True, lock_available_after_stop=True, profile_sha256=self.observed["profile_sha256"], inputs=inputs)
        write_new_or_same(self.handoff, json.dumps(evidence, sort_keys=True).encode(), gid=self.owner.pw_gid)
        self.handoff_written = True
        # Preserve display-only labels; never infer an account mapping from them.
        labels = Path(m["previous_entry"]).with_name("user_names.local.json")
        if labels.exists():
            target = self.code / "user_names.local.json"
            write_new_or_same(target, read(labels, 16384), gid=self.owner.pw_gid)
        args = [m["python"], "-B", "-u", str(self.code / "pi_api_identity_service.py"), "--config", m["config"],
                "--identity-config", m["identity_config"], "--restart-bundle", m["bundle_directory"],
                "--reviewed-server-sources", str(self.code / "server-sources.json"), "--handoff", str(self.handoff)]
        if any(not re.fullmatch(r"[a-zA-Z0-9_./:-]+", value) for value in args):
            raise RuntimeError("service_argument_path")
        self.override = ("[Service]\nExecStart=\nExecStart=" + " ".join(args)
            + "\nExecStartPre=+/usr/bin/python3 -I " + str(self.code / "identity_transition.py")
            + " claim --handoff " + str(self.handoff) + "\nKillSignal=SIGINT\nTimeoutStopSec=45\nRestart=no\n").encode()
        write_new_or_same(self.backup_path / "identity-override.conf", self.override, mode=0o600)
        write_new_or_same(DROPIN, self.override, mode=0o644)
        self.changed = True
        command(["systemctl", "daemon-reload"])

    def start_candidate(self):
        command(["systemctl", "start", SERVICE])

    def verify_candidate(self):
        import urllib.request
        for attempt in range(20):
            try:
                state = properties()
                pid = int(state["MainPID"])
                argv = (Path("/proc") / str(pid) / "cmdline").read_bytes()
                if (state["ActiveState"] != "active" or str(self.code / "pi_api_identity_service.py").encode() not in argv):
                    raise RuntimeError("candidate_entry")
                for path in ("/health", "/motion"):
                    with urllib.request.urlopen("http://127.0.0.1:8080" + path, timeout=2) as response:
                        if response.status != 200: raise RuntimeError("candidate_http")
                req = urllib.request.Request("http://127.0.0.1:8080/identity/v2/jobs", method="OPTIONS")
                with urllib.request.urlopen(req, timeout=2) as response:
                    if response.status != 200: raise RuntimeError("candidate_identity_http")
                self.hold()
                print("PASS: single identity service active; health/motion/identity OPTIONS 200; no capture/store/delete.")
                return
            except Exception:
                if attempt == 19: raise
                time.sleep(0.25)

    def stop_candidate(self):
        if self.handoff_written and self.handoff.exists():
            write_new_or_same(self.backup_path / "candidate-handoff.json", read(self.handoff), mode=0o600)
            self.handoff.unlink()  # Root revocation; original evidence remains in backup.
        command(["systemctl", "stop", SERVICE])

    def verify_candidate_drained(self):
        require_stopped(self.before["ControlGroup"], clean=False)
        self.lock_available()

    def restore_previous(self):
        if not self.changed and (DROPIN.exists() or DROPIN.is_symlink()):
            raise RuntimeError("override_changed_no_fallback")
        if self.changed:
            if read(DROPIN) != self.override:
                raise RuntimeError("override_changed_no_fallback")
            DROPIN.unlink()  # Only our exact new override; 60/90 and release stay intact.
        self.hold()
        if (digest(read(Path("/etc/systemd/system/attendance-device.service.d/90-member5-recovery-hold.conf"))) != self.hold_hash
                or any(digest(read(name, 131072)) != expected for name, expected in self.previous_hashes.items())):
            raise RuntimeError("previous_inputs_changed_no_fallback")
        command(["systemctl", "daemon-reload"])

    def start_previous(self):
        command(["systemctl", "start", SERVICE])

    def verify_previous(self):
        import urllib.request
        for attempt in range(20):
            try:
                state = properties()
                if state["ActiveState"] != "active": raise RuntimeError("previous_restore_failed")
                argv = (Path("/proc") / state["MainPID"] / "cmdline").read_bytes()
                if self.manifest["previous_entry"].encode() not in argv: raise RuntimeError("previous_restore_entry")
                for path, method in (("/health", "GET"), ("/motion", "GET"), ("/enrollments/jobs", "OPTIONS")):
                    with urllib.request.urlopen(urllib.request.Request("http://127.0.0.1:8080" + path, method=method), timeout=2) as r:
                        if r.status != 200: raise RuntimeError("previous_restore_http")
                print("RESTORED: exact previous MySQL service and HTTP; existing hold/release/keys/templates preserved.")
                return
            except Exception:
                if attempt == 19: raise
                time.sleep(0.25)

    @classmethod
    def from_backup(cls, directory):
        import pwd
        if os.geteuid() != 0: raise RuntimeError("root_rollback_required")
        directory = Path(directory)
        if (directory.parent != Path("/var/lib/remotework-device/transitions") or directory.is_symlink()
                or directory.stat().st_uid != 0 or stat.S_IMODE(directory.stat().st_mode) != 0o700):
            raise RuntimeError("private_transition_backup_required")
        obj = cls.__new__(cls)
        obj.backup_path = directory
        obj.manifest = json.loads(read(directory / "manifest.json"))
        obj.before = json.loads(read(directory / "before.json"))
        obj.observed = json.loads(read(directory / "observed.json"))
        obj.previous_hashes = json.loads(read(directory / "previous-inputs.json"))
        obj.hold_hash = read(directory / "hold.sha256").decode()
        obj.override = read(directory / "identity-override.conf")
        obj.owner = pwd.getpwnam(obj.manifest["user"])
        obj.code = Path("/opt/remotework-device") / obj.manifest["source_commit"]
        obj.handoff_dir = Path("/run/remotework-identity")
        obj.handoff = obj.handoff_dir / "handoff.json"
        obj.changed, obj.handoff_written = True, True
        if read(DROPIN) != obj.override: raise RuntimeError("different_active_override_no_rollback")
        state = properties()
        if state["MainPID"] != "0":
            argv = (Path("/proc") / state["MainPID"] / "cmdline").read_bytes().split(b"\0")
            if (str(obj.code / "pi_api_identity_service.py").encode() not in argv
                    or obj.manifest["bundle_directory"].encode() not in argv):
                raise RuntimeError("different_active_process_no_rollback")
        if obj.handoff.exists() and json.loads(read(obj.handoff))["cycle_id"] != obj.observed["cycle_id"]:
            raise RuntimeError("different_active_cycle_no_rollback")
        return obj


def claim_value(value, invocation, live_invocation, boot, previous_present):
    if (not re.fullmatch("[0-9a-f]{32}", invocation) or invocation != live_invocation
            or value["invocation_id"] != "" or value["boot_id"] != boot or previous_present):
        raise RuntimeError("handoff_already_claimed_or_other_boot")
    return dict(value, invocation_id=invocation)


def claim(path):
    if os.geteuid() != 0: raise RuntimeError("root_claim_required")
    path = Path(path)
    if path.parent != Path("/run/remotework-identity") or path.name != "handoff.json":
        raise RuntimeError("handoff_path")
    info = path.stat()
    if (info.st_uid != 0 or stat.S_IMODE(info.st_mode) != 0o640 or path.parent.stat().st_uid != 0
            or stat.S_IMODE(path.parent.stat().st_mode) & 0o022):
        raise RuntimeError("root_handoff_required")
    value = json.loads(read(path))
    invocation = os.environ.get("INVOCATION_ID", "")
    value = claim_value(value, invocation, properties()["InvocationID"], BOOT.read_text().strip(),
                        (Path("/proc") / str(value["previous_pid"])).exists())
    for name, expected in value["inputs"].items():
        if digest(read(name, 131072)) != expected: raise RuntimeError("handoff_input_changed")
    temporary = path.with_name("claim.tmp")
    write_new_or_same(temporary, json.dumps(value, sort_keys=True).encode(), gid=info.st_gid)
    os.replace(temporary, path)


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("plan", "apply", "claim", "rollback"))
    parser.add_argument("--manifest")
    parser.add_argument("--manifest-sha256")
    parser.add_argument("--handoff")
    parser.add_argument("--backup")
    args = parser.parse_args(argv)
    if args.action == "claim":
        claim(args.handoff); return 0
    if args.action == "rollback":
        ops = LinuxTransition.from_backup(args.backup)
        ops.stop_candidate(); ops.verify_candidate_drained(); ops.restore_previous()
        ops.start_previous(); ops.verify_previous()
        print("CHECK: device rollback only; server owner must restore coordinated OFF if required.")
        return 0
    if not args.manifest or not re.fullmatch("[0-9a-f]{64}", args.manifest_sha256 or ""):
        raise RuntimeError("pinned_manifest_required")
    if args.action == "plan":
        raw = read(args.manifest)
        if digest(raw) != args.manifest_sha256: raise RuntimeError("manifest_changed")
        print("PLAN: verify profile/source/ON observation -> clean stop -> same lock -> root invocation claim -> one service -> HTTP check.")
        print("PLAN: startup failure -> revoke -> drain -> exact previous MySQL override; no legacy writer/reset/key/template changes.")
        print("NOT_ACTIVE: plan only. Apply requires reviewed source and coordinated server ON/current-cycle evidence.")
        return 0
    ops = LinuxTransition(args.manifest, args.manifest_sha256)
    switch_service(ops)
    print("BACKUP: " + str(ops.backup_path))
    print("DONE: device service transition only; physical login/workday checks are separate.")
    return 0


if __name__ == "__main__":
    try: raise SystemExit(main())
    except Exception as error:
        code = str(error)
        if not re.fullmatch("[a-z_]{1,80}", code): code = "transition_failed_private_review_required"
        print("STOPPED: " + code + "; no sensor reset or legacy fallback.", file=sys.stderr)
        raise SystemExit(1)
