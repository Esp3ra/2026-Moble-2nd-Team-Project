"""User-run legacy evidence preservation and read-only inventory, never reset.

Stops the single existing service and installs a reversible startup hold.
Existing SQLite files are examined read-only as historical evidence only.
No database/schema creation, sensor capture/store/delete, or server call.
"""

import argparse
from contextlib import closing
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
from uuid import uuid4

from fingerprint_enrollment import EnrollmentOperation
from fingerprint_enrollment_private import (
    _directory, _open_private, _read_private, parse_configuration, require_linux,
)
from fingerprint_enrollment_wire import COMMON, RESULT, canonical_uuid, parse_json, revision, verify
from fingerprint_inventory import _read_connected_inventory


UNIT = "attendance-device.service"
HOLD = Path("/etc/systemd/system/attendance-device.service.d/90-member5-recovery-hold.conf")
LIMIT = 16 * 1024 * 1024
PUBLIC_STATES = {"capturing", "store_pending", "sensor_stored", "failed", "recovery_required"}
PUBLIC_CODES = {None, "already_registered", "recovery_required", "sensor_or_storage_error",
    "not_authorized", "cancelled", "timeout", "slot_occupied", "capacity_changed", "first_features_failed",
    "second_features_failed", "image_failed", "search_failed", "fingerprints_do_not_match",
    "store_failed", "store_unconfirmed", "sensor_busy", "invalid_input"}


class CheckError(Exception):
    pass


def check(value, code):
    if not value:
        raise CheckError(code)


def utc():
    return datetime.now(timezone.utc).isoformat()


def write_new(path, raw):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "wb") as out:
        out.write(raw)
        out.flush()
        os.fsync(out.fileno())


def run_command(args, timeout=8):
    command = args[1:] if args[0] == "sudo" else args
    if command and command[0] == "-n":
        command = command[1:]
    stage = "service_properties" if command[:2] == ["systemctl", "show"] else (
        "service_unit_read" if command[:2] == ["systemctl", "cat"] else (
        "systemd_reload" if command == ["systemctl", "daemon-reload"] else (
        "protected_hold_read" if command[:2] == ["/usr/bin/python3", "-I"] else "system_command")))
    try:
        result = subprocess.run(args, capture_output=True, timeout=timeout)
    except subprocess.TimeoutExpired:
        raise CheckError(stage + "_timed_out") from None
    except OSError:
        raise CheckError(stage + "_unavailable") from None
    check(result.returncode == 0, stage + "_failed_exit_" + str(result.returncode))
    return result.stdout


def properties():
    names = ("User", "WorkingDirectory", "ExecStart", "ActiveState", "SubState", "MainPID", "KillMode")
    raw = run_command(["systemctl", "show", UNIT, "--no-pager", "--property=" + ",".join(names)])
    return dict(line.split("=", 1) for line in raw.decode().splitlines() if "=" in line)


def unit_contents():
    # Root-owned drop-in directories may intentionally be mode0700. Keep their
    # protection intact; only this fixed unit's text is read with cached sudo.
    return run_command(["sudo", "-n", "systemctl", "cat", UNIT, "--no-pager"])


def protected_hold():
    script = """import json,os,stat,sys
from pathlib import Path
try:
 p=Path(sys.argv[1])
 for part in (p,*p.parents):
  if part.is_symlink():raise ValueError()
 for part in p.parents:
  if part.exists():
   s=part.stat()
   if not stat.S_ISDIR(s.st_mode) or s.st_uid!=0 or s.st_mode&0o022:raise ValueError()
 if p.exists():
  fd=os.open(p,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
  with os.fdopen(fd,'rb') as f:
   s=os.fstat(f.fileno())
   if not stat.S_ISREG(s.st_mode) or s.st_uid!=0 or s.st_nlink!=1 or s.st_mode&0o022:raise ValueError()
   raw=f.read(4097)
  if len(raw)>4096:raise ValueError()
  print(json.dumps({'exists':True,'hex':raw.hex()}))
 else:print(json.dumps({'exists':False,'hex':None}))
except Exception:raise SystemExit(77)
"""
    raw = run_command(["sudo", "-n", "/usr/bin/python3", "-I", "-c", script, str(HOLD)])
    try:
        value = json.loads(raw)
        check(set(value) == {"exists", "hex"} and type(value["exists"]) is bool, "hold_probe_invalid")
        if not value["exists"]:
            check(value["hex"] is None, "hold_probe_invalid")
            return None
        check(type(value["hex"]) is str and len(value["hex"]) <= 8192, "hold_probe_invalid")
        return bytes.fromhex(value["hex"])
    except CheckError:
        raise
    except Exception:
        raise CheckError("hold_probe_invalid") from None


def expected_attempt(value, config):
    check(type(value) is dict and value.get("device_id") == config.keys.device_id, "device_mismatch")
    jobs = value.get("jobs")
    check(type(jobs) is list and len(jobs) == 1, "single_expected_job_required")
    job = jobs[0]
    canonical_uuid(job["enrollment_id"])
    for field in ("operation_revision", "claim_revision"):
        revision(job[field])
    check(type(job["target_user_id"]) is int and 1 <= job["target_user_id"] <= 2147483647, "invalid_target")
    check(type(job["slot"]) is int and config.profile.minimum_slot <= job["slot"] <= config.profile.maximum_slot,
          "slot_outside_approved_profile")
    check(job.get("claim_current_job") is True and job.get("claim_state") == "reserved", "expected_claim_requires_review")
    check(value.get("device_mappings") == 0 and job.get("mappings") == 0, "reported_mapping_requires_review")
    return job


def read_legacy(state, expected, config):
    """Only immutable read-only connections, after stop/lease and sidecar checks.

    Immutable is appropriate only for the quiescent DELETE-journal store. A
    nonempty/unknown WAL or hot journal requires separate preservation/review.
    No connection is allowed to create or repair the original database.
    """
    import sqlite3
    for item in state.iterdir():
        check(not item.name.endswith(("-wal", "-shm", "-journal")), "database_sidecar_requires_review")
    records = []
    for name, query in (
        ("ledger.sqlite3", "SELECT * FROM local_enrollments WHERE enrollment_id=?"),
        ("results.sqlite3", "SELECT * FROM signed_results WHERE enrollment_id=?"),
    ):
        path = state / name
        _read_private(path, LIMIT)  # Owner, no symlink/hardlink, size bound.
        with closing(sqlite3.connect(path.as_uri() + "?mode=ro&immutable=1", uri=True)) as db:
            db.execute("PRAGMA query_only=ON")
            check(db.execute("PRAGMA quick_check").fetchall() == [("ok",)], "legacy_integrity_failed")
            db.row_factory = sqlite3.Row
            rows = db.execute(query, (expected["enrollment_id"],)).fetchall()
            check(len(rows) <= 1, "ambiguous_legacy_rows")
            records.append(dict(rows[0]) if rows else None)
    row, cached = records
    metadata_match = (row is not None and row["device_id"] == config.keys.device_id
                      and type(row["slot"]) is int and row["slot"] == expected["slot"])
    summary = dict(ledger_found=row is not None, metadata_match=metadata_match,
                   ledger_state=row["state"] if row else None, ledger_code=row["code"] if row else None,
                   signed_result_present=cached is not None, signed_result_verified=False,
                   target_binding_verified=False)
    raw = None
    if cached is not None:
        check(metadata_match and type(cached["envelope"]) is bytes, "cached_evidence_requires_review")
        raw = cached["envelope"]
        result = verify(raw, RESULT, config.keys)
        wanted = dict(enrollment_id=expected["enrollment_id"], operation_revision=expected["operation_revision"],
                      reserved_slot=expected["slot"], claim_revision=expected["claim_revision"],
                      key_epoch=config.keys.key_epoch)
        check(all(result[field] == wanted[field] for field in COMMON), "signed_binding_mismatch")
        op = EnrollmentOperation(expected["enrollment_id"], config.keys.device_id, expected["target_user_id"],
            expected["operation_revision"], expected["slot"], result["ticket_digest"])
        check(row["binding"] == cached["binding"] == op.binding(), "target_binding_mismatch")
        check(row["result_id"] == result["result_id"], "fixed_result_id_mismatch")
        check(row["state"] == result["outcome"], "ledger_result_state_mismatch")
        summary.update(signed_result_verified=True, target_binding_verified=True,
                       outcome=result["outcome"], failure_code=result["failure_code"])
    return summary, raw


def backup_evidence(root, state, config_path, before):
    destination = root / ("recovery-check-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid4().hex[:8])
    destination.mkdir(mode=0o700)
    target = destination / "original-state"
    target.mkdir(mode=0o700)
    manifest = {}
    for source in [config_path, *sorted(state.iterdir())]:
        if source.name == "runtime.lock":
            continue
        raw = _read_private(source, LIMIT)
        name = "original-config.private.json" if source == config_path else source.name
        output = destination / name if source == config_path else target / name
        write_new(output, raw)
        check(hashlib.sha256(output.read_bytes()).digest() == hashlib.sha256(raw).digest(), "backup_mismatch")
        manifest[name] = hashlib.sha256(raw).hexdigest()
    unit = unit_contents()
    write_new(destination / "service-with-hold.private.txt", unit)
    write_new(destination / "properties-before.private.json", json.dumps(before).encode())
    write_new(destination / "backup-manifest.private.json", json.dumps(manifest, sort_keys=True).encode())
    return destination


def install_hold(root, private_run):
    release = root / "recovery-writer-release"
    check(not release.exists() and not release.is_symlink(), "writer_release_gate_already_present")
    raw = ("[Unit]\n# Member 5 recovery hold; remove only after reviewed transition.\nConditionPathExists="
           + str(release) + "\n").encode()
    previous = protected_hold()
    if previous is not None:
        check(previous == raw, "existing_hold_conflict")
    else:
        staged = private_run / "recovery-hold.conf"
        write_new(staged, raw)
        result = subprocess.run(["sudo", "-n", "install", "-D", "-m", "0644", str(staged), str(HOLD)], timeout=15)
        check(result.returncode == 0, "hold_install_failed")
    run_command(["sudo", "-n", "systemctl", "daemon-reload"], timeout=15)
    check(protected_hold() == raw, "hold_verification_failed")
    return hashlib.sha256(raw).hexdigest()


def stop_and_inspect(expected_path):
    require_linux()
    import fcntl
    import pwd
    os.umask(0o077)
    home = Path.home()
    check(home == Path("/home/moble") and pwd.getpwuid(os.getuid()).pw_name == "moble", "service_user_required")
    root = home / ".config" / "remotework-enrollment"
    _directory(root)
    config_path = root / "enrollment.local.json"
    config = parse_configuration(_read_private(config_path))
    state = root / "state"
    check(config.state_directory == state, "unexpected_state_directory")
    _directory(state)
    expected_document = parse_json(_read_private(Path(expected_path)))
    job = expected_attempt(expected_document, config)
    before = properties()
    check(before.get("User") == "moble" and before.get("WorkingDirectory") == str(home / "attendance"), "service_identity_changed")
    check(before.get("KillMode") == "control-group", "service_kill_mode_requires_review")
    check(str(home / "attendance" / "enrollment_startup_check_20261020" / "pi_api_enrollment.py") in before.get("ExecStart", ""),
          "running_source_requires_review")
    check(Path("/usr/bin/fuser").is_file() and Path("/dev/ttyAMA0").is_char_device(), "uart_owner_check_unavailable")
    print("Administrator access: reading protected service settings; directory permissions stay unchanged.", flush=True)
    authenticated = subprocess.run(["sudo", "-v"], timeout=90)
    check(authenticated.returncode == 0, "administrator_authentication_failed")
    # First make a service/config record. Database bytes are copied only after
    # the writer stops and its process lease is held by this inspector.
    private_run = root / ("recovery-fence-" + uuid4().hex)
    private_run.mkdir(mode=0o700)
    write_new(private_run / "before.private.json", json.dumps(before).encode())
    write_new(private_run / "unit-before.private.txt", unit_contents())
    hold_hash = install_hold(root, private_run)
    print("1/3: stopping the old service; fingerprint API will stay stopped.", flush=True)
    result = subprocess.run(["sudo", "-n", "systemctl", "stop", UNIT], timeout=60)
    check(result.returncode == 0, "service_stop_failed")
    after = properties()
    check(after.get("ActiveState") == "inactive" and after.get("MainPID") == "0", "service_not_fully_stopped")
    stopped_at = utc()
    fd = _open_private(state / "runtime.lock", os.O_RDWR)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        directory = backup_evidence(root, state, config_path, before)
        write_new(directory / "server-attempt.private.json", _read_private(Path(expected_path)))
        owner = subprocess.run(["sudo", "-n", "/usr/bin/fuser", "-s", "/dev/ttyAMA0"], capture_output=True, timeout=5)
        check(owner.returncode == 1, "uart_still_owned_or_check_failed")
        print("2/3: private backup verified; reading preserved legacy evidence.", flush=True)
        evidence, raw = read_legacy(state, job, config)
        if raw is not None:
            write_new(directory / "signed-result.original.private.json", raw)
        # This sends only parameter/count/index metadata commands. No captures,
        # feature extraction, template upload/download/store, or delete exists here.
        print("3/3: reading sensor inventory only; no fingerprint registration/deletion.", flush=True)
        inventory = _read_connected_inventory("/dev/ttyAMA0", 57600)
        check(inventory["reported_capacity"] == config.profile.expected_capacity, "sensor_capacity_changed")
        empty = job["slot"] not in inventory["occupied_slots"]
        report = dict(checked_at=utc(), stopped_at=stopped_at, service_after=after,
            startup_hold_sha256=hold_hash, hold_path=str(HOLD), evidence=evidence,
            pre_stop_evidence_directory=str(private_run),
            inventory=inventory, expected_slot_empty=empty, server_observed_at=expected_document.get("observed_at"),
            metadata_only_match=not evidence["target_binding_verified"],
            fingerprints_deleted=False, server_changed=False, service_restarted=False)
        write_new(directory / "report.private.json", json.dumps(report, ensure_ascii=False, indent=2).encode())
        print("PASS: service stopped; persistent recovery hold installed.")
        print("CHECK: expected job/device/slot metadata matched=" + str(evidence["metadata_match"]).lower())
        public_state = evidence["ledger_state"] if evidence["ledger_state"] in PUBLIC_STATES else "unrecognized"
        public_code = evidence["ledger_code"] if evidence["ledger_code"] in PUBLIC_CODES else "unrecognized"
        print("CHECK: ledger state=" + str(public_state) + "; code=" + str(public_code))
        print("CHECK: signed result present=" + str(evidence["signed_result_present"]).lower()
              + "; target binding verified=" + str(evidence["target_binding_verified"]).lower())
        print("CHECK: sensor capacity=" + str(inventory["reported_capacity"])
              + "; template count=" + str(inventory["reported_template_count"])
              + "; reserved slot empty=" + str(empty).lower())
        print("PRIVATE_REPORT: " + str(directory / "report.private.json"))
        print("DONE: inspection only. Fingerprints retained; service stays stopped. Do not restart the old writer.")
    finally:
        os.close(fd)


def main(argv=None):
    parser = argparse.ArgumentParser(description="기존 등록 중지·비공개 백업·읽기 전용 대조; 지문 삭제 없음")
    parser.add_argument("--expected", required=True)
    parser.add_argument("--stop-service-and-inspect", action="store_true")
    args = parser.parse_args(argv)
    try:
        check(args.stop_service_and_inspect, "explicit_stop_and_inspect_required")
        stop_and_inspect(args.expected)
        return 0
    except KeyboardInterrupt:
        print("STOPPED: retain evidence and service hold; review before resuming.", file=sys.stderr)
        return 130
    except Exception as error:
        code = str(error) if isinstance(error, CheckError) else "recovery_inspection_failed"
        print("STOPPED: " + code + ". No automatic restart/reset. Send this status only.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
