"""Device-side enrollment core; no HTTP route, UART opening, ticket parser or signer.

A future adapter must authenticate enrollment-specific tickets and supply the
server-reserved slot. SensorReceipt is local evidence, never a server verification.
"""

from contextlib import contextmanager
from dataclasses import dataclass
import hashlib
import json
import math
from pathlib import Path
import re
import sqlite3
import time
from uuid import UUID, uuid4

from fingerprint_inventory import read_inventory


class EnrollmentError(Exception):
    def __init__(self, code):
        self.code = code
        super().__init__(code)


def _uuid(value):
    try:
        if type(value) is not str or str(UUID(value)) != value:
            raise ValueError()
    except (ValueError, AttributeError, TypeError) as error:
        raise EnrollmentError("invalid_input") from error


@dataclass(frozen=True)
class EnrollmentOperation:
    enrollment_id: str
    device_id: str
    target_user_id: int
    operation_revision: str
    reserved_slot: int
    ticket_digest: str

    def validate(self):
        _uuid(self.enrollment_id)
        _uuid(self.device_id)
        if type(self.target_user_id) is not int or not 1 <= self.target_user_id <= 2147483647:
            raise EnrollmentError("invalid_input")
        if (type(self.operation_revision) is not str
                or not re.fullmatch(r"[1-9][0-9]{0,18}", self.operation_revision)
                or int(self.operation_revision) > 9223372036854775807):
            raise EnrollmentError("invalid_input")
        if type(self.reserved_slot) is not int or not 0 <= self.reserved_slot <= 65535:
            raise EnrollmentError("invalid_input")
        if type(self.ticket_digest) is not str or not re.fullmatch(r"[0-9a-f]{64}", self.ticket_digest):
            raise EnrollmentError("invalid_input")

    def binding(self):
        # No ticket/nonce, template, name, score or user ID is stored in the ledger.
        value = [self.enrollment_id, self.device_id, self.target_user_id,
                 self.operation_revision, self.reserved_slot, self.ticket_digest]
        return hashlib.sha256(json.dumps(value, separators=(",", ":")).encode("ascii")).hexdigest()


@dataclass(frozen=True)
class SensorProfile:
    expected_capacity: int
    minimum_slot: int
    maximum_slot: int

    def __post_init__(self):
        values = (self.expected_capacity, self.minimum_slot, self.maximum_slot)
        if (any(type(value) is not int for value in values)
                or not 1 <= self.expected_capacity <= 65535
                or not 0 <= self.minimum_slot <= self.maximum_slot < self.expected_capacity):
            raise EnrollmentError("invalid_profile")


@dataclass(frozen=True)
class SensorReceipt:
    enrollment_id: str
    result_id: str
    device_id: str
    reserved_slot: int
    state: str
    elapsed_ms: int


class EnrollmentLedger:
    """Private Pi recovery metadata. Opening this store does not open hardware.

    Successful/uncertain slots stay claimed until a future authorized recovery
    protocol releases them. There is deliberately no automatic delete/reset API.
    """

    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        with self._connect() as db:
            db.execute("""CREATE TABLE IF NOT EXISTS local_enrollments (
                enrollment_id TEXT PRIMARY KEY, binding TEXT NOT NULL,
                device_id TEXT NOT NULL, slot INTEGER NOT NULL,
                result_id TEXT NOT NULL, state TEXT NOT NULL,
                code TEXT, elapsed_ms INTEGER NOT NULL DEFAULT 0)""")

    @contextmanager
    def _connect(self):
        db = sqlite3.connect(self.path, timeout=1)
        try:
            db.row_factory = sqlite3.Row
            db.execute("PRAGMA synchronous=FULL")
            with db:
                yield db
        finally:
            db.close()

    def claim(self, operation):
        with self._connect() as db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT * FROM local_enrollments WHERE enrollment_id=?",
                             (operation.enrollment_id,)).fetchone()
            if row is not None:
                if row["binding"] != operation.binding():
                    raise EnrollmentError("request_conflict")
                return dict(row), False
            if db.execute("""SELECT 1 FROM local_enrollments WHERE device_id=? AND
                    (state IN ('capturing','store_pending','recovery_required')
                     OR (slot=? AND state='sensor_stored')) LIMIT 1""",
                          (operation.device_id, operation.reserved_slot)).fetchone():
                raise EnrollmentError("recovery_required")
            db.execute("INSERT INTO local_enrollments VALUES (?,?,?,?,?,'capturing',NULL,0)",
                       (operation.enrollment_id, operation.binding(), operation.device_id,
                        operation.reserved_slot, str(uuid4())))
            row = db.execute("SELECT * FROM local_enrollments WHERE enrollment_id=?",
                             (operation.enrollment_id,)).fetchone()
            return dict(row), True

    def transition(self, operation, expected, state, code=None, elapsed_ms=0):
        with self._connect() as db:
            result = db.execute("""UPDATE local_enrollments SET state=?,code=?,elapsed_ms=?
                WHERE enrollment_id=? AND binding=? AND state=?""",
                                (state, code, elapsed_ms, operation.enrollment_id, operation.binding(), expected))
            if result.rowcount != 1:
                raise EnrollmentError("recovery_required")


class FingerprintEnrollment:
    def __init__(self, sensor, codes, sensor_lock, ledger, device_id, profile,
                 authorize, progress=lambda *_: None, clock=time.monotonic, sleep=time.sleep):
        _uuid(device_id)
        self.sensor, self.codes, self.lock, self.ledger = sensor, codes, sensor_lock, ledger
        self.device_id, self.profile, self.authorize = device_id, profile, authorize
        self.progress, self.clock, self.sleep = progress, clock, sleep

    def _notify(self, stage):
        try:
            self.progress(stage)
        except Exception:
            pass  # A closed progress view must not change the physical operation.

    def _current(self, operation, cancellation, deadline):
        if cancellation.is_set():
            raise EnrollmentError("cancelled")
        if self.clock() >= deadline:
            raise EnrollmentError("timeout")
        try:
            authorized = self.authorize(operation) is True
        except Exception:
            authorized = False
        if not authorized:
            raise EnrollmentError("not_authorized")

    def _ok(self, result, step):
        if type(result) is not int or result != self.codes.OK:
            raise EnrollmentError(step)

    def _inventory(self, slot):
        report = read_inventory(self.sensor, self.codes.OK)
        if report["reported_capacity"] != self.profile.expected_capacity:
            raise EnrollmentError("capacity_changed")
        if slot in report["occupied_slots"]:
            raise EnrollmentError("slot_occupied")
        return set(report["occupied_slots"])

    def _wait(self, want_finger, operation, cancellation, deadline):
        stage_deadline = min(deadline, self.clock() + 15)
        while True:
            self._current(operation, cancellation, min(deadline, stage_deadline))
            result = self.sensor.get_image()
            self._current(operation, cancellation, min(deadline, stage_deadline))
            if type(result) is not int:
                raise EnrollmentError("image_failed")
            if result == (self.codes.OK if want_finger else self.codes.NOFINGER):
                return
            if result not in (self.codes.OK, self.codes.NOFINGER):
                raise EnrollmentError("image_failed")
            self.sleep(0.2)

    def enroll(self, operation, cancellation, *, total_timeout=90):
        operation.validate()
        if operation.device_id != self.device_id:
            raise EnrollmentError("wrong_device")
        if not self.profile.minimum_slot <= operation.reserved_slot <= self.profile.maximum_slot:
            raise EnrollmentError("slot_out_of_range")
        if type(total_timeout) not in (int, float) or not math.isfinite(total_timeout) or not 0 < total_timeout <= 120:
            raise EnrollmentError("invalid_input")
        started = self.clock()
        deadline = started + total_timeout  # Local I/O budget, NOT a ticket TTL.
        self._current(operation, cancellation, deadline)
        if not self.lock.acquire(blocking=False):
            raise EnrollmentError("sensor_busy")
        local_state = None
        try:
            self._current(operation, cancellation, deadline)
            row, fresh = self.ledger.claim(operation)
            if not fresh:
                self._current(operation, cancellation, deadline)
                if row["state"] == "sensor_stored":
                    return SensorReceipt(operation.enrollment_id, row["result_id"], self.device_id,
                                         operation.reserved_slot, "sensor_stored", row["elapsed_ms"])
                raise EnrollmentError(row["code"] if row["state"] == "failed" else "recovery_required")
            local_state = "capturing"
            self._inventory(operation.reserved_slot)
            self._notify("remove_finger")
            self._wait(False, operation, cancellation, deadline)
            self._notify("first_capture")
            self._wait(True, operation, cancellation, deadline)
            self._ok(self.sensor.image_2_tz(1), "first_features_failed")
            self._current(operation, cancellation, deadline)
            result = self.sensor.finger_search()
            if type(result) is not int:
                raise EnrollmentError("search_failed")
            if result == self.codes.OK:
                raise EnrollmentError("already_registered")
            if result not in (self.codes.NOTFOUND, self.codes.NOMATCH):
                raise EnrollmentError("search_failed")
            self._notify("remove_finger")
            self._wait(False, operation, cancellation, deadline)
            self._notify("second_capture")
            self._wait(True, operation, cancellation, deadline)
            self._ok(self.sensor.image_2_tz(2), "second_features_failed")
            self._current(operation, cancellation, deadline)
            self._ok(self.sensor.create_model(), "fingerprints_do_not_match")
            before = self._inventory(operation.reserved_slot)
            self._current(operation, cancellation, deadline)
            # Persist uncertainty BEFORE the irreversible store command.
            self.ledger.transition(operation, "capturing", "store_pending")
            local_state = "store_pending"
            self._current(operation, cancellation, deadline)
            self._ok(self.sensor.store_model(operation.reserved_slot), "store_failed")
            self._current(operation, cancellation, deadline)
            after = read_inventory(self.sensor, self.codes.OK)
            if (after["reported_capacity"] != self.profile.expected_capacity
                    or set(after["occupied_slots"]) != before | {operation.reserved_slot}):
                raise EnrollmentError("store_unconfirmed")
            self._current(operation, cancellation, deadline)
            elapsed = max(0, int((self.clock() - started) * 1000))
            self.ledger.transition(operation, "store_pending", "sensor_stored", elapsed_ms=elapsed)
            local_state = "sensor_stored"
            self._notify("sensor_stored_server_pending")
            self._current(operation, cancellation, deadline)
            return SensorReceipt(operation.enrollment_id, row["result_id"], self.device_id,
                                 operation.reserved_slot, "sensor_stored", elapsed)
        except Exception as error:
            if local_state in ("capturing", "store_pending"):
                code = error.code if isinstance(error, EnrollmentError) else "sensor_or_storage_error"
                state = "recovery_required" if local_state == "store_pending" else "failed"
                try:
                    self.ledger.transition(operation, local_state, state, code=code)
                except Exception:
                    pass  # Existing capturing/store_pending evidence still blocks automatic retry.
                if state == "recovery_required":
                    raise EnrollmentError("recovery_required") from None
            if isinstance(error, EnrollmentError):
                raise
            raise EnrollmentError("sensor_or_storage_error") from None
        finally:
            self.lock.release()
