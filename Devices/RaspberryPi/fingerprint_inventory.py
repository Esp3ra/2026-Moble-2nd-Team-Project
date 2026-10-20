"""Read parameters and occupied slot numbers without capturing or changing fingerprints."""

import argparse
from datetime import datetime, timezone
from importlib.metadata import PackageNotFoundError, version
import json
import subprocess
import sys


class InventoryError(Exception):
    """An inconclusive inventory must never be used to allocate a slot."""


def _integer(value, minimum, maximum, field):
    if type(value) is not int or not minimum <= value <= maximum:
        raise InventoryError(f"{field}: 센서 응답을 확인할 수 없습니다.")
    return value


def _check(result, ok, step):
    if type(result) is not int or result != ok:
        raise InventoryError(f"{step} 실패: 센서 응답을 확인해 주세요.")


def read_inventory(sensor, ok):
    """Metadata commands only. Reported capacity is not a confirmed slot range."""
    _check(sensor.read_sysparam(), ok, "장치 정보 조회")
    capacity = _integer(sensor.library_size, 1, 65535, "저장 용량")
    _check(sensor.count_templates(), ok, "등록 개수 조회")
    count_before = _integer(sensor.template_count, 0, capacity, "등록 개수")
    _check(sensor.read_templates(), ok, "등록 번호 조회")
    if sensor.library_size != capacity:
        raise InventoryError("조회 중 센서 용량이 달라졌습니다. 등록에는 사용하지 마세요.")
    if not isinstance(sensor.templates, (list, tuple)):
        raise InventoryError("등록 번호 목록을 확인할 수 없습니다.")
    slots = [_integer(slot, 0, 65535, "등록 번호") for slot in sensor.templates]
    _check(sensor.count_templates(), ok, "등록 개수 재확인")
    count_after = _integer(sensor.template_count, 0, capacity, "등록 개수")
    if count_before != count_after or count_after != len(slots) or len(set(slots)) != len(slots):
        raise InventoryError("등록 개수와 목록이 일치하지 않습니다. 빈 번호를 추정하지 마세요.")
    system_id = getattr(sensor, "system_id", None)
    if system_id is not None:
        system_id = _integer(system_id, 0, 65535, "장치 시스템 ID")
    return {
        "success": True,
        "operation": "read_only_inventory",
        "observed_at": datetime.now(timezone.utc).isoformat(),
        "reported_capacity": capacity,
        "reported_template_count": count_after,
        "occupied_slots": sorted(slots),
        "count_and_list_agree": True,
        "system_id": system_id,
        "sensor_model": None,
        "zero_slot_registration_supported": None,
        "confirmed_slot_upper_bound": None,
        "registration_performed": False,
    }


def _ensure_service_inactive():
    try:
        result = subprocess.run(
            ["systemctl", "is-active", "attendance-device.service"],
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, encoding="utf-8",
            timeout=3, check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as error:
        raise InventoryError("서비스 상태를 확인하지 못했습니다. UART를 열지 않았습니다.") from error
    # Also refuse activating/deactivating/failed/unknown instead of treating all
    # nonzero exit statuses as proof that the sensor is idle.
    if result.returncode != 3 or result.stdout.strip() != "inactive":
        raise InventoryError("attendance-device.service를 완전히 중지한 뒤 실행해 주세요. UART를 열지 않았습니다.")


def _read_connected_inventory(port, baudrate):
    # Import, --help and refused preflight never open hardware.
    import serial
    import adafruit_fingerprint as af

    with serial.Serial(port, baudrate, timeout=1, write_timeout=1, exclusive=True) as uart:
        report = read_inventory(af.Adafruit_Fingerprint(uart), af.OK)
    try:
        report["fingerprint_library_version"] = version("adafruit-circuitpython-fingerprint")
    except PackageNotFoundError:
        report["fingerprint_library_version"] = None
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="지문 센서 용량·등록 개수·번호 읽기 전용 점검")
    parser.add_argument("--port", default="/dev/ttyAMA0")
    parser.add_argument("--baudrate", type=int, choices=(9600, 19200, 38400, 57600, 115200), default=57600)
    parser.add_argument("--confirm-sensor-idle", action="store_true",
                        help="서비스와 수동 pi_api/등록 프로그램·miniterm을 모두 중지했음을 확인")
    args = parser.parse_args(argv)
    try:
        if not args.confirm_sensor_idle:
            raise InventoryError("서비스·수동 센서 프로그램을 중지한 뒤 --confirm-sensor-idle로 실행해 주세요.")
        _ensure_service_inactive()
        report = _read_connected_inventory(args.port, args.baudrate)
        print(json.dumps(report, ensure_ascii=False))
        return 0
    except InventoryError as error:
        print(json.dumps({"success": False, "message": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("점검을 중지했습니다.", file=sys.stderr)
        return 130
    except Exception:
        # Do not print driver exceptions, credentials or arbitrary UART bytes.
        print(json.dumps({"success": False, "message": "센서 연결/라이브러리 오류입니다. 포트·가상환경·중복 실행을 확인해 주세요."},
                         ensure_ascii=False), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
