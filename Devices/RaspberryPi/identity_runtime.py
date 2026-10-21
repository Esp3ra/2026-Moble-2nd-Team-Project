"""Borrow the single sensor owner. Admission remains closed without trusted readiness."""
import math
import threading

from flask import jsonify

from fingerprint_identity import IdentityProfile, boottime_clock
from identity_http import mount_identity_http


class IdentityRuntime:
    def __init__(self, service, profile, *, ready=None, clock=boottime_clock, maximum=64):
        if not isinstance(profile, IdentityProfile) or ready is not None and not callable(ready):
            raise ValueError("Trusted identity profile and readiness provider required")
        if type(maximum) is not int or not 1 <= maximum <= 1024:
            raise ValueError("Invalid RAM attempt limit")
        now = clock()
        if type(now) not in (int, float) or not math.isfinite(now) or now < 0:
            raise ValueError("Suspend-aware clock required")
        self._service, self._profile, self._ready = service, profile, ready
        self._clock, self._maximum = clock, maximum
        self._lock = threading.Lock()
        self._closed, self._mounted, self._jobs = False, False, None

    def mount(self, app):
        # Called once by the host before serving; never construct a pool per RPC.
        with self._lock:
            if self._closed or self._mounted:
                raise RuntimeError("Identity runtime cannot be remounted")
            self._mounted = True
            jobs = mount_identity_http(app, self._service, self._profile, enabled=True,
                                       clock=self._clock, maximum=self._maximum)
            self._jobs = jobs

        @app.before_request
        def identity_admission():
            from flask import request
            if request.blueprint != "identity_v2":
                return None
            with self._lock:
                allowed = False
                if not self._closed and not self._service.stopping.is_set() and self._ready is not None:
                    try:
                        allowed = self._ready(self._profile) is True
                    except Exception:
                        pass
                if not allowed:
                    self._closed = True
            if not allowed:
                # Nonblocking cancellation; the host drains workers and UART on close.
                jobs.close(timeout=0)
                return jsonify(success=False, error={"code": "IDENTITY_DEVICE_NOT_READY"}), 503
            return None

    def close(self):
        with self._lock:
            self._closed = True
            jobs = self._jobs
        # Do not mark the shared sensor stopping or clear/recreate the RAM pool.
        if jobs is not None and not jobs.close():
            raise RuntimeError("Identity workers still stopping")
        return True
