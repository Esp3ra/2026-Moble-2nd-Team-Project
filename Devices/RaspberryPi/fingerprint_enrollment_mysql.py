"""PR55 enrollment-mysql-v1 client. Durable evidence lives on the server only.

The PC relays authenticated HTTP; the Pi verifies signed capture/store/read
permits. A bare receipt or a retry never grants sensor access. No SQL client,
filesystem result store, fallback writer, or template deletion is provided.
"""

from dataclasses import dataclass
import secrets
import threading
import time
from types import MappingProxyType
from uuid import uuid4

from fingerprint_enrollment import EnrollmentError, EnrollmentOperation, FingerprintEnrollment
from fingerprint_enrollment_approval import OnlineEnrollmentApproval, suspend_aware_clock
from fingerprint_enrollment_wire import (
    COMMON, REQUEST, RESULT, canonical_uuid, decode64, encode64, integer,
    parse_json, require, revision, sign_result, ticket_digest, timestamp, verify,
    verify_result_binding,
)


CONTRACT = "enrollment-mysql-v1"
VIEW_FIELDS = set(COMMON) | {
    "contract", "phase", "result_envelope", "result_id", "outcome", "failure_code",
    "intent_at", "reported_at", "may_store", "approval_envelope",
}
FAILURE_CODES = {
    "already_registered": "duplicate_finger", "sensor_busy": "busy",
    "cancelled": "cancelled", "timeout": "timeout", "slot_occupied": "slot_conflict",
    "fingerprints_do_not_match": "different_finger", "capacity_changed": "capacity_mismatch",
    "not_authorized": "authorization_lost",
}


@dataclass(frozen=True)
class _Ticket:
    operation: EnrollmentOperation
    payload: object


class _RamLedger:
    """Single-process duplicate guard, NOT a recovery database.

    After restart, the server's intent/result is consulted before any capture.
    Unresolved operations are never evicted to make room for another writer.
    """

    def __init__(self, maximum=32):
        self.rows, self.maximum = {}, maximum

    def claim(self, operation):
        old = self.rows.get(operation.enrollment_id)
        if old is not None:
            require(old["binding"] == operation.binding())
            return old, False
        if len(self.rows) >= self.maximum or any(
                row["state"] in ("capturing", "store_pending", "recovery_required")
                for row in self.rows.values()):
            raise EnrollmentError("recovery_required")
        row = dict(binding=operation.binding(), result_id=str(uuid4()), state="capturing",
                   code=None, elapsed_ms=0, envelope=None)
        self.rows[operation.enrollment_id] = row
        return row, True

    def transition(self, operation, expected, state, code=None, elapsed_ms=0):
        row = self.rows.get(operation.enrollment_id)
        if row is None or row["binding"] != operation.binding() or row["state"] != expected:
            raise EnrollmentError("recovery_required")
        row.update(state=state, code=code, elapsed_ms=elapsed_ms)


class MySqlEnrollmentAdapter:
    def __init__(self, *, sensor, codes, sensor_lock, profile, keys,
                 progress=lambda *_: None, clock=time.monotonic, sleep=time.sleep):
        self.sensor, self.codes, self.sensor_lock = sensor, codes, sensor_lock
        self.profile, self.keys, self.progress = profile, keys, progress
        self.clock, self.sleep = clock, sleep
        self.ledger = _RamLedger()
        self._writer = threading.Lock()

    def _ticket(self, raw):
        try:
            payload = verify(raw, REQUEST, self.keys)
            operation = EnrollmentOperation(payload["enrollment_id"], self.keys.device_id,
                payload["target_user_id"], payload["operation_revision"],
                payload["reserved_slot"], ticket_digest(raw))
            operation.validate()
            return _Ticket(operation, MappingProxyType(payload))
        except Exception:
            raise EnrollmentError("invalid_enrollment_request") from None

    def _view(self, raw, enrollment_id, expected=None, *, store_reply=False):
        value = parse_json(raw, 32768)
        require(set(value) == VIEW_FIELDS and value["contract"] == CONTRACT)
        canonical_uuid(value["enrollment_id"])
        require(value["enrollment_id"] == enrollment_id)
        integer(value["reserved_slot"])
        for field in ("operation_revision", "claim_revision", "key_epoch"):
            revision(value[field])
        require(value["key_epoch"] == self.keys.key_epoch)
        if expected is not None:
            require(all(value[field] == expected[field] for field in COMMON))
        require(type(value["may_store"]) is bool)
        require(value["phase"] in ("not_recorded", "store_intent", "result_recorded"))
        for field in ("intent_at", "reported_at"):
            if value[field] is not None:
                timestamp(value[field])
        if not store_reply:
            require(value["may_store"] is False and value["approval_envelope"] is None)
        if value["phase"] == "not_recorded":
            require(all(value[field] is None for field in (
                "result_envelope", "result_id", "outcome", "failure_code", "intent_at", "reported_at")))
            require(value["may_store"] is False and value["approval_envelope"] is None)
        elif value["phase"] == "store_intent":
            require(value["intent_at"] is not None and all(value[field] is None for field in (
                "result_envelope", "result_id", "outcome", "failure_code", "reported_at")))
        else:
            require(value["reported_at"] is not None and value["may_store"] is False
                    and value["approval_envelope"] is None)
            result = verify(decode64(value["result_envelope"], 16384), RESULT, self.keys)
            require(all(result[field] == value[field] for field in COMMON))
            require(all(result[field] == value[field] for field in ("result_id", "outcome", "failure_code")))
            if result["outcome"] == "sensor_stored":
                require(value["intent_at"] is not None)
        return value

    def _rpc(self, enrollment_id, revision_value, action, cancellation, exchange, raw=None):
        require(not cancellation.is_set())
        body = dict(action=action, operation_revision=revision_value,
                    probe_nonce=encode64(secrets.token_bytes(32)))
        if raw is not None:
            body["result_envelope"] = encode64(raw)
        return exchange(enrollment_id, body, cancellation)

    def _fetch(self, enrollment_id, cancellation, exchange, expected=None):
        # read_result has no original ticket; the PC pins its already-known job revision.
        raw = self._rpc(enrollment_id, expected["operation_revision"] if expected else None,
                        "read_physical_result", cancellation, exchange)
        return self._view(raw, enrollment_id, expected)

    def _session(self, context, scope, cancellation, exchange, clock):
        return OnlineEnrollmentApproval(context=context, keys=self.keys, scope=scope,
            exchange=exchange, cancellation=cancellation, monotonic=clock)

    def _deliver(self, raw, cancellation, exchange, approval_clock, on_delivery, ticket=None):
        result = verify(raw, RESULT, self.keys)
        if ticket is not None:
            verify_result_binding(ticket.payload, ticket.operation.ticket_digest, result)
        context = {field: result[field] for field in COMMON}
        context["ticket_digest"] = result["ticket_digest"]
        session = self._session(context, "result_read", cancellation, exchange, approval_clock)
        try:
            session.refresh()
            if on_delivery is not None:
                on_delivery(session.delivery_seconds())
            return raw
        finally:
            session.invalidate()

    def _report(self, ticket, raw, cancellation, exchange):
        reply = self._rpc(ticket.operation.enrollment_id, ticket.operation.operation_revision,
                          "record_physical_result", cancellation, exchange, raw)
        value = self._view(reply, ticket.operation.enrollment_id, ticket.payload)
        require(value["phase"] == "result_recorded"
                and decode64(value["result_envelope"], 16384) == raw)

    def enroll_online(self, raw_request, cancellation, *, exchange,
                      approval_clock=suspend_aware_clock, total_timeout=90, on_delivery=None):
        ticket = self._ticket(raw_request)
        if not self._writer.acquire(blocking=False):
            raise EnrollmentError("sensor_busy")
        try:
            return self._enroll(ticket, cancellation, exchange, approval_clock, total_timeout, on_delivery)
        finally:
            self._writer.release()

    def _enroll(self, ticket, cancellation, exchange, approval_clock, total_timeout, on_delivery):
        op = ticket.operation
        value = self._fetch(op.enrollment_id, cancellation, exchange, ticket.payload)
        local = self.ledger.rows.get(op.enrollment_id)
        if local is not None:
            require(local["binding"] == op.binding())
        if value["phase"] == "result_recorded":
            raw = decode64(value["result_envelope"], 16384)
            require(local is None or local["envelope"] is None or local["envelope"] == raw)
            return self._deliver(raw, cancellation,
                                 exchange, approval_clock, on_delivery, ticket)
        if local is not None and local["envelope"] is not None:
            self._report(ticket, local["envelope"], cancellation, exchange)
            return self._deliver(local["envelope"], cancellation, exchange, approval_clock, on_delivery, ticket)
        if value["phase"] != "not_recorded" or local is not None:
            raise EnrollmentError("recovery_required")

        context = {field: ticket.payload[field] for field in COMMON}
        context["ticket_digest"] = op.ticket_digest
        capture = self._session(context, "sensor_capture", cancellation, exchange, approval_clock)
        store = None
        stage = "capture"
        started = self.clock()

        def current(operation):
            if operation != op or cancellation.is_set():
                return False
            try:
                active = capture if stage == "capture" else store
                if stage == "capture":
                    active.refresh()
                now = active.trusted_utc()
                return timestamp(ticket.payload["issued_at"]) <= now < timestamp(ticket.payload["expires_at"])
            except Exception:
                return False

        def before_store(operation):
            nonlocal store, stage
            require(operation == op and stage == "capture")
            # A possible commit is uncertainty even when the response is lost.
            stage = "store"
            capture.invalidate()
            def commit(enrollment_id, body, cancel):
                value = self._view(exchange(enrollment_id, body, cancel), enrollment_id,
                                   ticket.payload, store_reply=True)
                require(value["phase"] == "store_intent" and value["may_store"] is True)
                return decode64(value["approval_envelope"], 16384)
            store = self._session(context, "sensor_store", cancellation, commit, approval_clock)
            store.refresh()  # Verifies MAC, exact nonce/scope/binding, RTT and expiry.

        try:
            core = FingerprintEnrollment(self.sensor, self.codes, self.sensor_lock,
                self.ledger, self.keys.device_id, self.profile, authorize=current,
                progress=self.progress, clock=self.clock, sleep=self.sleep, before_store=before_store)
            try:
                core.enroll(op, cancellation, total_timeout=total_timeout)
            except EnrollmentError:
                # Only a claimed attempt has a fixed result ID and physical phase.
                if op.enrollment_id not in self.ledger.rows:
                    raise
            row = self.ledger.rows[op.enrollment_id]
            if row["state"] not in ("sensor_stored", "failed", "recovery_required"):
                raise EnrollmentError("recovery_required")
            result = {field: ticket.payload[field] for field in COMMON}
            failure = None if row["state"] == "sensor_stored" else (
                "storage_uncertain" if stage == "store" else FAILURE_CODES.get(row["code"], "sensor_error"))
            result.update(ticket_digest=op.ticket_digest, result_id=row["result_id"],
                outcome=row["state"], failure_code=failure,
                elapsed_ms=max(0, int((self.clock() - started) * 1000)))
            row["envelope"] = sign_result(result, self.keys)  # Freeze once, before network delivery.
            self._report(ticket, row["envelope"], cancellation, exchange)
            return self._deliver(row["envelope"], cancellation, exchange, approval_clock, on_delivery, ticket)
        finally:
            capture.invalidate()
            if store is not None:
                store.invalidate()

    def read_result_online(self, enrollment_id, cancellation, *, exchange,
                           approval_clock=suspend_aware_clock, on_delivery=None):
        canonical_uuid(enrollment_id)
        value = self._fetch(enrollment_id, cancellation, exchange)
        if value["phase"] == "result_recorded":
            raw = decode64(value["result_envelope"], 16384)
            local = self.ledger.rows.get(enrollment_id)
            require(local is None or local["envelope"] is None or local["envelope"] == raw)
        else:
            row = self.ledger.rows.get(enrollment_id)
            if row is None or row["envelope"] is None:
                raise EnrollmentError("recovery_required" if value["phase"] == "store_intent"
                                      else "result_not_available")
            raw = row["envelope"]
            result = verify(raw, RESULT, self.keys)
            reply = self._rpc(enrollment_id, result["operation_revision"],
                              "record_physical_result", cancellation, exchange, raw)
            value = self._view(reply, enrollment_id)
            require(value["phase"] == "result_recorded"
                    and decode64(value["result_envelope"], 16384) == raw)
        return self._deliver(raw, cancellation, exchange, approval_clock, on_delivery)
