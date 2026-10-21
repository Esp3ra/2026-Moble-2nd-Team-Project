"""Local sensor comparison only; deliberately not connected to an HTTP route.

The caller must obtain the expected slot from a verified, current server
challenge/mapping, authorize capture, and later have the server verify/consume
the signed result. A local match is NOT login, attendance or identity approval.
This module does not define that pending API/crypto contract or load any keys.
It borrows FingerprintService's UART owner, lock, stop event and OLED.
No enrollment, template write/delete, database, HTTP, name or confidence lookup.
"""

from dataclasses import dataclass
import math


@dataclass(frozen=True)
class SensorComparison:
    outcome: str
    # Only an exact expected-slot match may carry a slot for a future signer.
    slot: int | None = None


class _Stopped(Exception):
    pass


class _Expired(Exception):
    pass


def compare_fingerprint(service, expected_slot, *, cancelled=None, timeout_seconds=15):
    """Compare one fresh placement, without changing templates or account state.

    timeout_seconds is a local I/O budget, not a server challenge TTL. The
    future authorized adapter must also enforce its server deadline/currentness.
    Cancellation/deadline is checked after every potentially blocking command,
    so a late sensor success cannot escape as a match. No automatic rescan.
    """
    if type(expected_slot) is not int or not 0 <= expected_slot <= 65535:
        raise ValueError("expected_slot must be an explicit integer sensor slot")
    if (type(timeout_seconds) not in (int, float)
            or not math.isfinite(timeout_seconds) or timeout_seconds <= 0):
        raise ValueError("timeout_seconds must be finite and positive")
    if cancelled is not None and not callable(cancelled):
        raise ValueError("cancelled must be a callable or None")
    acquired = False

    def stopped():
        return service.stopping.is_set() or cancelled is not None and cancelled()

    def display(method, *lines):
        try:
            getattr(service.display, method)(*lines)
        except Exception:
            # OLED failure cannot repeat sensor commands or leak sensor ownership.
            pass

    try:
        if stopped():
            return SensorComparison("cancelled")
        acquired = service.lock.acquire(blocking=False)
        if not acquired:
            return SensorComparison("busy")
        deadline = service.clock() + timeout_seconds

        def check():
            if stopped():
                raise _Stopped()
            if service.clock() >= deadline:
                raise _Expired()

        def command(operation, *args):
            check()
            value = operation(*args)
            check()
            if type(value) is not int:
                raise RuntimeError("invalid_sensor_response")
            return value

        def wait_image(want_finger):
            while True:
                value = command(service.finger.get_image)
                if value == (service.codes.OK if want_finger else service.codes.NOFINGER):
                    return
                if value not in (service.codes.OK, service.codes.NOFINGER):
                    raise RuntimeError("sensor_image_error")
                service.sleep(min(0.2, max(0, deadline - service.clock())))

        check()
        display("begin_scan")
        display("show_scan", "지문 확인 준비", "손가락을 떼주세요")
        wait_image(False)
        display("show_scan", "지문 확인", "손가락을 올리세요")
        wait_image(True)
        if command(service.finger.image_2_tz, 1) != service.codes.OK:
            raise RuntimeError("sensor_feature_error")
        search = command(service.finger.finger_fast_search)
        if search in (service.codes.NOMATCH, service.codes.NOTFOUND):
            display("show_scan", "미등록 지문", "PC 안내 확인")
            return SensorComparison("unregistered")
        if search != service.codes.OK:
            raise RuntimeError("sensor_search_error")
        observed = service.finger.finger_id
        check()
        if type(observed) is not int or not 0 <= observed <= 65535:
            raise RuntimeError("invalid_sensor_slot")
        if observed != expected_slot:
            display("show_scan", "다른 지문입니다", "등록한 지문을 사용하세요")
            return SensorComparison("different_finger")
        display("show_scan", "지문 일치", "서버 확인 필요")
        check()
        return SensorComparison("matched", expected_slot)
    except _Stopped:
        return SensorComparison("cancelled")
    except _Expired:
        if acquired:
            display("show_scan", "입력 시간 초과", "PC 안내 확인")
        return SensorComparison("timeout")
    except Exception:
        if acquired:
            display("show_scan", "센서 확인 필요", "PC 안내 확인")
        # Neither device exceptions nor private identifiers leave the sensor core.
        return SensorComparison("sensor_error")
    finally:
        if acquired:
            try:
                display("end_scan")
            finally:
                service.lock.release()
