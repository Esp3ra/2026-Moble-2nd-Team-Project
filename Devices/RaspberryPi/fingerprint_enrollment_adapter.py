"""Isolated Pi adapter. No HTTP route, UART opening, automatic permission or TLS.

Product integration must supply verified CURRENT server approval and trustworthy
UTC, selected keys, the single UART owner and the SAME scan/enroll sensor lock.
"""

from contextlib import closing
from dataclasses import dataclass, field
from datetime import datetime, timedelta
import os
from pathlib import Path
import sqlite3
import stat
import time
from types import MappingProxyType

from fingerprint_enrollment import EnrollmentError, EnrollmentOperation, FingerprintEnrollment
from fingerprint_enrollment_approval import OnlineEnrollmentApproval, suspend_aware_clock
from fingerprint_enrollment_wire import (
    COMMON, REQUEST, RESULT, WireError, canonical_uuid, sign_result, ticket_digest, timestamp,
    verify, verify_result_binding,
)


@dataclass(frozen=True)
class VerifiedEnrollment:
    operation: EnrollmentOperation = field(repr=False)
    payload: object = field(repr=False)


class SignedResultCache:
    """Write-once signed bytes; no raw request, names, nonce, keys or templates.

    Use a dedicated private directory. POSIX owner/mode are checked; Windows
    production ACLs are an external deployment requirement, not verified here.
    """

    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
        if self.path.parent.is_symlink() or self.path.is_symlink():
            raise EnrollmentError("private_cache_required")
        if os.name == "posix":
            info = self.path.parent.stat()
            if info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) & 0o077:
                raise EnrollmentError("private_cache_required")
        try:
            descriptor = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        except FileExistsError:
            pass
        else:
            os.close(descriptor)
        info = self.path.stat()
        if not stat.S_ISREG(info.st_mode):
            raise EnrollmentError("private_cache_required")
        if os.name == "posix" and (info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) & 0o077):
            raise EnrollmentError("private_cache_required")
        with closing(self._connect()) as db, db:
            db.execute("""CREATE TABLE IF NOT EXISTS signed_results (
                enrollment_id TEXT PRIMARY KEY, binding TEXT NOT NULL,
                envelope BLOB NOT NULL)""")

    def _connect(self):
        db = sqlite3.connect(self.path, timeout=5)
        try:
            db.execute("PRAGMA synchronous=FULL")
            return db
        except Exception:
            db.close()
            raise

    def get(self, operation):
        with closing(self._connect()) as db:
            row = db.execute("SELECT binding,envelope FROM signed_results WHERE enrollment_id=?",
                             (operation.enrollment_id,)).fetchone()
        if row is None:
            return None
        if row[0] != operation.binding():
            raise EnrollmentError("request_conflict")
        if type(row[1]) is not bytes:
            raise EnrollmentError("result_cache_invalid")
        return row[1]

    def put(self, operation, raw):
        with closing(self._connect()) as db, db:
            db.execute("BEGIN IMMEDIATE")
            row = db.execute("SELECT binding,envelope FROM signed_results WHERE enrollment_id=?",
                             (operation.enrollment_id,)).fetchone()
            if row is not None:
                if row != (operation.binding(), raw):
                    raise EnrollmentError("result_conflict")
                return row[1]
            db.execute("INSERT INTO signed_results VALUES (?,?,?)",
                       (operation.enrollment_id, operation.binding(), raw))
        return raw  # Return only AFTER the transaction has committed.

    def read_local(self, enrollment_id):
        """Internal lookup only; caller must authenticate before external delivery."""
        canonical_uuid(enrollment_id)
        with closing(self._connect()) as db:
            row = db.execute("SELECT envelope FROM signed_results WHERE enrollment_id=?",
                             (enrollment_id,)).fetchone()
        if row is None:
            raise EnrollmentError("result_not_available")
        if type(row[0]) is not bytes:
            raise EnrollmentError("result_cache_invalid")
        return row[0]


class EnrollmentAdapter:
    def __init__(self, *, sensor, codes, sensor_lock, ledger, profile, keys,
                 cache, current_approval=None, trusted_utc=None, progress=lambda *_: None,
                 clock=time.monotonic, sleep=time.sleep):
        self.sensor, self.codes, self.sensor_lock = sensor, codes, sensor_lock
        self.ledger, self.profile, self.keys, self.cache = ledger, profile, keys, cache
        self.current_approval, self.trusted_utc = current_approval, trusted_utc
        self.progress, self.clock, self.sleep = progress, clock, sleep

    def _authorized(self, ticket, cancellation):
        try:
            if cancellation.is_set():
                return False
            # Refresh first; an expired/missing prior UTC must not prevent an
            # authenticated online provider from obtaining a NEW approval.
            if self.current_approval(ticket) is not True:
                return False
            # A slow approval provider must not extend the ticket's lifetime.
            after = self.trusted_utc()
            return (type(after) is datetime and after.utcoffset() == timedelta(0)
                    and timestamp(ticket.payload["issued_at"]) <= after
                    < timestamp(ticket.payload["expires_at"]) and not cancellation.is_set())
        except Exception:
            return False

    def _cached(self, ticket):
        try:
            raw = self.cache.get(ticket.operation)
            if raw is not None:
                result = verify(raw, RESULT, self.keys)
                verify_result_binding(ticket.payload, ticket.operation.ticket_digest, result)
                if result["outcome"] != "sensor_stored":
                    raise EnrollmentError("result_cache_invalid")
            return raw
        except EnrollmentError:
            raise
        except Exception:
            raise EnrollmentError("result_cache_invalid") from None

    def enroll(self, raw_request, cancellation, *, total_timeout=90):
        ticket = self._ticket(raw_request)
        operation, payload = ticket.operation, ticket.payload
        if not self._authorized(ticket, cancellation):
            raise EnrollmentError("not_authorized")
        cached = self._cached(ticket)
        if cached is not None:
            if not self._authorized(ticket, cancellation):
                raise EnrollmentError("not_authorized")
            return cached  # Exact saved bytes: no new scan, result ID or signing.

        worker = FingerprintEnrollment(
            self.sensor, self.codes, self.sensor_lock, self.ledger, self.keys.device_id,
            self.profile, authorize=lambda current: current == operation
            and self._authorized(ticket, cancellation), progress=self.progress,
            clock=self.clock, sleep=self.sleep,
        )
        receipt = worker.enroll(operation, cancellation, total_timeout=total_timeout)
        result = {name: payload[name] for name in COMMON}
        result.update(ticket_digest=operation.ticket_digest, result_id=receipt.result_id,
                      outcome="sensor_stored", failure_code=None, elapsed_ms=receipt.elapsed_ms)
        try:
            raw_result = sign_result(result, self.keys)
            raw_result = self.cache.put(operation, raw_result)
        except Exception:
            raise EnrollmentError("result_persistence_required") from None
        if not self._authorized(ticket, cancellation):
            raise EnrollmentError("not_authorized")
        return raw_result

    def _ticket(self, raw_request):
        try:
            payload = verify(raw_request, REQUEST, self.keys)
            operation = EnrollmentOperation(
                payload["enrollment_id"], self.keys.device_id, payload["target_user_id"],
                payload["operation_revision"], payload["reserved_slot"], ticket_digest(raw_request),
            )
            operation.validate()
        except (WireError, EnrollmentError):
            raise EnrollmentError("invalid_enrollment_request") from None
        return VerifiedEnrollment(operation, MappingProxyType(payload))

    def enroll_online(self, raw_request, cancellation, *, exchange,
                      approval_clock=suspend_aware_clock, total_timeout=90, on_delivery=None):
        """Bootstrap verified online UTC, then refresh at every core auth check."""
        ticket = self._ticket(raw_request)
        context = {name: ticket.payload[name] for name in COMMON}
        context["ticket_digest"] = ticket.operation.ticket_digest
        session = OnlineEnrollmentApproval(
            context=context, keys=self.keys, scope="sensor_write", exchange=exchange,
            cancellation=cancellation, monotonic=approval_clock,
        )
        try:
            session.refresh()  # No Pi wall-clock prerequisite or offline fallback.
            session.trusted_utc()
            worker = EnrollmentAdapter(
                sensor=self.sensor, codes=self.codes, sensor_lock=self.sensor_lock,
                ledger=self.ledger, profile=self.profile, keys=self.keys, cache=self.cache,
                current_approval=session.current_approval, trusted_utc=session.trusted_utc,
                progress=self.progress, clock=self.clock, sleep=self.sleep,
            )
            result = worker.enroll(raw_request, cancellation, total_timeout=total_timeout)
            if on_delivery is not None:
                on_delivery(session.delivery_seconds())
            return result
        finally:
            session.invalidate()

    def read_result_online(self, enrollment_id, cancellation, *, exchange,
                           approval_clock=suspend_aware_clock, on_delivery=None):
        """Read existing signed bytes with result_read only; no original ticket needed.

        Minimal local signed binding is inspected before requesting approval.
        No bytes are delivered until current server scope/rights are verified.
        This method never opens the sensor, calls the core, or signs a result.
        """
        try:
            canonical_uuid(enrollment_id)
            raw = self.cache.read_local(enrollment_id)
            result = verify(raw, RESULT, self.keys)
            if result["enrollment_id"] != enrollment_id or result["outcome"] != "sensor_stored":
                raise EnrollmentError("result_cache_invalid")
        except EnrollmentError:
            raise
        except Exception:
            raise EnrollmentError("result_cache_invalid") from None
        context = {name: result[name] for name in COMMON}
        context["ticket_digest"] = result["ticket_digest"]
        session = OnlineEnrollmentApproval(
            context=context, keys=self.keys, scope="result_read", exchange=exchange,
            cancellation=cancellation, monotonic=approval_clock,
        )
        try:
            session.refresh()
            session.trusted_utc()  # Approval's lifetime, NOT the original write TTL.
            if on_delivery is not None:
                on_delivery(session.delivery_seconds())
            return raw
        finally:
            session.invalidate()
