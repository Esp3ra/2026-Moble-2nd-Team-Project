"""Explicit composition for the existing single UART/OLED owner.

No hardware opening, key generation, configuration discovery or service install.
The caller supplies approved keys/profile and private ledger/cache objects.
"""

import threading

from fingerprint_enrollment_adapter import EnrollmentAdapter
from fingerprint_enrollment_approval import suspend_aware_clock
from fingerprint_enrollment_relay import EnrollmentRelayJobs


PROMPTS = {
    "remove_finger": ("지문 등록", "손가락을 떼주세요"),
    "first_capture": ("지문 등록 1/2", "손가락을 올리세요"),
    "second_capture": ("지문 등록 2/2", "같은 손가락을", "다시 올리세요"),
    "sensor_stored_server_pending": ("지문 저장됨", "계정 연결 확인 중"),
}


class _DisplaySensorLock:
    """Use the actual scan lock; display ownership ends BEFORE releasing it."""

    def __init__(self, service):
        self.service = service
        self.active = False

    def _display(self, method, *args):
        try:
            getattr(self.service.display, method)(*args)
        except Exception:
            # Display failures must never repeat a physical store or leak a lock.
            pass

    def acquire(self, blocking=True):
        if self.service.stopping.is_set():
            return False
        if not self.service.lock.acquire(blocking=blocking):
            return False
        if self.service.stopping.is_set():
            self.service.lock.release()
            return False
        self.active = True
        self._display("begin_scan")
        return True

    def progress(self, stage):
        if self.active and stage in PROMPTS:
            self._display("show_scan", *PROMPTS[stage])

    def release(self):
        try:
            self._display("end_scan")
        finally:
            self.active = False
            self.service.lock.release()


class EnrollmentRuntime:
    """Borrow an existing FingerprintService, with explicit trusted dependencies.

    close() cancels enrollment workers only. The host owns the scan service,
    UART, OLED and motion worker, and drains the shared sensor lock before UART
    close. No request/result keys or slot policy have production defaults here.
    """

    def __init__(self, fingerprint_service, *, keys, profile, ledger, cache,
                 approval_clock=suspend_aware_clock):
        self._lock = threading.Lock()
        self._closed = False
        display_lock = _DisplaySensorLock(fingerprint_service)
        self.adapter = EnrollmentAdapter(
            sensor=fingerprint_service.finger, codes=fingerprint_service.codes,
            sensor_lock=display_lock, ledger=ledger, profile=profile, keys=keys,
            cache=cache, progress=display_lock.progress,
            clock=fingerprint_service.clock, sleep=fingerprint_service.sleep,
        )
        self.jobs = EnrollmentRelayJobs(self.adapter, approval_clock=approval_clock)

    def close(self):
        with self._lock:
            if not self._closed:
                self.jobs.close()
                self._closed = True
