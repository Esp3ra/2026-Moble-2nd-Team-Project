"""Single-process Pi demo: fingerprint /scan and light-change /motion.

Run with python pi_api.py. One process must own the UART, USB and OLED.
Hardware is initialized only by main(), so tests can use simulated devices.
"""

import csv
import json
import threading
import time
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo


HTTP_PORT = 8080
MEGA_PORT = "/dev/ttyUSB0"
FINGER_PORT = "/dev/ttyAMA0"
LOG_PATH = Path.home() / "attendance" / "light_motion_events.csv"
USER_NAMES = {}  # Display labels only; never account IDs.


def load_user_names(path):
    """Optional local display labels; no fingerprint templates or account IDs."""
    path = Path(path)
    if not path.exists():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("Display labels must be a JSON object")
    names = {}
    for slot, name in data.items():
        if (not isinstance(slot, str) or not slot.isascii() or not slot.isdecimal()
                or str(int(slot)) != slot or not 0 <= int(slot) <= 65535
                or not isinstance(name, str) or not name.strip()
                or len(name) > 40 or any(ord(c) < 32 for c in name)):
            raise ValueError("Invalid local fingerprint display label")
        names[int(slot)] = name.strip()
    return names


def timestamp():
    return datetime.now(ZoneInfo("Asia/Seoul")).isoformat()


class MotionTracker:
    def __init__(self, started_at, bright_max=186, dark_min=190,
                 idle_seconds=10.0, data_timeout=3.0):
        self.started_at = started_at
        self.bright_max = bright_max
        self.dark_min = dark_min
        self.idle_seconds = idle_seconds
        self.data_timeout = data_timeout
        self.last_data = None
        self.last_motion = None
        self.light = None
        self.value = None

    def receive(self, text, now):
        try:
            value = int(text.strip())
        except (ValueError, TypeError):
            return False
        if not 0 <= value <= 1023:
            return False
        if self.last_data is not None and now - self.last_data >= self.data_timeout:
            # Reconnection establishes a new baseline, not a movement event.
            self.light = None
            self.last_motion = None
        self.last_data = now
        self.value = value
        new_light = self.light
        if value <= self.bright_max:
            new_light = "BRIGHT"
        elif value >= self.dark_min:
            new_light = "DARK"
        moved = self.light is not None and new_light != self.light
        if moved:
            self.last_motion = now
        self.light = new_light
        return moved

    def status(self, now):
        reference = self.started_at if self.last_data is None else self.last_data
        if now - reference >= self.data_timeout:
            return "DATA_LOST"
        if self.last_data is None:
            return "WAITING"
        if self.last_motion is None:
            return "READY"
        if now - self.last_motion >= self.idle_seconds:
            return "NO_RECENT_MOTION"
        return "RECENT_MOTION"

    def current_state(self, now):
        freshness = self.status(now)
        if freshness in ("WAITING", "DATA_LOST"):
            return freshness
        return {"DARK": "COVERED", "BRIGHT": "CLEAR"}.get(
            self.light, "LEVEL_UNKNOWN"
        )

LABELS = {
    "WAITING": "수신 대기",
    "READY": "감지 대기",
    "RECENT_MOTION": "최근 움직임 있음",
    "NO_RECENT_MOTION": "최근 움직임 없음",
    "DATA_LOST": "데이터 끊김",
    "CONNECTION_ERROR": "USB 연결 오류",
    "COVERED": "가림 감지",
    "CLEAR": "가림 해제",
    "LEVEL_UNKNOWN": "명암 확인 중",
}


class MotionFeed:
    def __init__(self, clock=time.monotonic):
        self.clock = clock
        self.lock = threading.Lock()
        self.tracker = MotionTracker(clock())
        self.connected = False
        self.error = None
        self.observed_at = None
        self.last_motion_at = None

    def connection(self, connected, error=None):
        with self.lock:
            self.connected = connected
            self.error = error
            self.tracker = MotionTracker(self.clock())
            self.observed_at = None
            self.last_motion_at = None

    def receive(self, text):
        with self.lock:
            now = self.clock()
            previous = self.tracker.last_data
            moved = self.tracker.receive(text, now)
            if self.tracker.last_data != previous:
                self.observed_at = timestamp()
                if previous is None or now - previous >= self.tracker.data_timeout:
                    self.last_motion_at = None
                if moved:
                    self.last_motion_at = self.observed_at
            return moved

    def snapshot(self):
        with self.lock:
            now = self.clock()
            state = self.tracker.current_state(now)
            history = self.tracker.status(now)
            if not self.connected and self.error:
                state = history = "CONNECTION_ERROR"
            available = self.connected and history not in ("WAITING", "DATA_LOST")
            return {
                "state": state,
                "detected": {"COVERED": True, "CLEAR": False}.get(state),
                "raw_value": self.tracker.value if available else None,
                "motion_state": history,
                "recent_motion": {"RECENT_MOTION": True, "NO_RECENT_MOTION": False}.get(history),
                "seconds_since_motion": round(now - self.tracker.last_motion, 1)
                if available and self.tracker.last_motion is not None else None,
                "last_motion_at": self.last_motion_at,
                "observed_at": self.observed_at,
                "connected": self.connected,
                "available": available,
                "error": self.error,
                "thresholds": {"detect_at_least": 190, "release_at_most": 186},
                "simulation": True,
            }


class DisplayCoordinator:
    """Only one owner draws at a time; fingerprint prompts take priority."""
    def __init__(self, draw_lines, clock=time.monotonic):
        self.draw_lines = draw_lines
        self.clock = clock
        self.lock = threading.Lock()
        self.scanning = False
        self.hold_until = 0

    def _draw(self, lines):
        try:
            self.draw_lines(lines)
        except OSError as error:
            print(f"OLED 표시 오류: {error}", flush=True)

    def begin_scan(self):
        with self.lock:
            self.scanning = True

    def show_scan(self, *lines):
        with self.lock:
            self._draw(lines)

    def end_scan(self):
        with self.lock:
            self.scanning = False
            self.hold_until = self.clock() + 3

    def show_motion(self, data):
        with self.lock:
            if self.scanning or self.clock() < self.hold_until:
                return False
            value = "--" if data["raw_value"] is None else str(data["raw_value"])
            elapsed = data["seconds_since_motion"]
            elapsed_text = "--" if elapsed is None else f"{elapsed:.0f}초"
            self._draw([
                LABELS[data["state"]], f"조도: {value}",
                f"변화 후: {elapsed_text}", LABELS[data["motion_state"]],
            ])
            return True


class FingerprintService:
    def __init__(self, finger, codes, display, names=None,
                 clock=time.monotonic, sleep=time.sleep):
        self.finger = finger
        self.codes = codes
        self.display = display
        self.names = dict(USER_NAMES if names is None else names)
        self.clock = clock
        self.sleep = sleep
        self.lock = threading.Lock()
        self.stopping = threading.Event()

    def wait_image(self, want_finger, seconds=15):
        deadline = self.clock() + seconds
        while self.clock() < deadline:
            if self.stopping.is_set():
                raise RuntimeError("서버 종료 중")
            result = self.finger.get_image()
            if want_finger and result == self.codes.OK:
                return
            if not want_finger and result == self.codes.NOFINGER:
                return
            if result not in (self.codes.OK, self.codes.NOFINGER):
                raise RuntimeError(f"지문 이미지 오류: {result}")
            self.sleep(0.2)
        raise TimeoutError("지문 입력 시간이 초과되었습니다")

    def scan(self):
        if not self.lock.acquire(blocking=False):
            return {"success": False, "message": "인증 진행 중입니다"}, 409
        try:
            self.display.begin_scan()
            self.display.show_scan("인증 시작", "손가락을 떼주세요")
            self.wait_image(False)
            self.display.show_scan("지문 인증 대기", "손가락을 올리세요")
            self.wait_image(True)
            result = self.finger.image_2_tz(1)
            if result != self.codes.OK:
                raise RuntimeError(f"특징 추출 실패: {result}")
            result = self.finger.finger_fast_search()
            if result in (self.codes.NOTFOUND, self.codes.NOMATCH):
                self.display.show_scan("인증 실패", "미등록 지문입니다")
                return {"success": False, "message": "등록되지 않은 지문입니다"}, 200
            if result != self.codes.OK:
                raise RuntimeError(f"지문 검색 오류: {result}")
            finger_id = self.finger.finger_id
            name = self.names.get(finger_id, f"사용자 {finger_id}")
            self.display.show_scan(f"{name}님", "인증 성공", f"ID: {finger_id}")
            return {"success": True, "finger_id": finger_id, "name": name,
                    "confidence": self.finger.confidence}, 200
        except TimeoutError as error:
            self.display.show_scan("입력 시간 초과", "다시 요청하세요")
            return {"success": False, "message": str(error)}, 408
        except Exception as error:
            print(f"센서 오류: {error}", flush=True)
            self.display.show_scan("센서 오류", "터미널 확인")
            return {"success": False, "message": "센서 처리 오류"}, 500
        finally:
            self.display.end_scan()
            self.lock.release()


class MotionWorker:
    def __init__(self, feed, display, serial_factory, log_path=LOG_PATH):
        self.feed = feed
        self.display = display
        self.serial_factory = serial_factory
        self.log_path = Path(log_path)
        self.stopping = threading.Event()
        self.thread = threading.Thread(target=self.run, name="mega-light-reader", daemon=True)

    def start(self):
        self.thread.start()

    def stop(self):
        self.stopping.set()
        self.thread.join(timeout=4)

    def run(self):
        try:
            self.log_path.parent.mkdir(parents=True, exist_ok=True)
            with self.log_path.open("a", encoding="utf-8", newline="") as stream:
                writer = csv.writer(stream)
                if stream.tell() == 0:
                    writer.writerow(["time_kst", "event", "raw_value", "demo_type"])

                def record(event, data):
                    writer.writerow([timestamp(), event, data["raw_value"], "finger_shadow_simulation"])
                    stream.flush()

                record("START", self.feed.snapshot())
                while not self.stopping.is_set():
                    try:
                        with self.serial_factory(MEGA_PORT, 115200, timeout=0.25,
                                                 exclusive=True) as arduino:
                            self.feed.connection(True)
                            pending = b""
                            discard = False
                            last_key = None
                            last_display = float("-inf")
                            while not self.stopping.is_set():
                                chunk = arduino.read_until(b"\n", size=64)
                                moved = False
                                if not discard:
                                    pending += chunk
                                    if len(pending) > 64:
                                        pending = b""
                                        discard = True
                                if chunk.endswith(b"\n"):
                                    if not discard:
                                        moved = self.feed.receive(pending.decode("ascii", errors="replace"))
                                    pending = b""
                                    discard = False
                                data = self.feed.snapshot()
                                key = (data["state"], data["motion_state"])
                                if moved:
                                    record("MOTION", data)
                                if key != last_key:
                                    if last_key is None or key[0] != last_key[0]:
                                        record(key[0], data)
                                    if last_key is None or key[1] != last_key[1]:
                                        record(key[1], data)
                                now = time.monotonic()
                                if key != last_key or now - last_display >= 1:
                                    self.display.show_motion(data)
                                    last_display = now
                                last_key = key
                    except (OSError, RuntimeError) as error:
                        self.feed.connection(False, "MEGA_CONNECTION_ERROR")
                        record("CONNECTION_ERROR", self.feed.snapshot())
                        print(f"메가 연결 오류: {error}", flush=True)
                        self.display.show_motion(self.feed.snapshot())
                        self.stopping.wait(2)
                record("STOP", self.feed.snapshot())
        except Exception as error:
            self.feed.connection(False, "MOTION_WORKER_ERROR")
            print(f"조도 처리 중단: {error}", flush=True)
        finally:
            self.feed.connection(False, "MOTION_STOPPED")


def create_app(fingerprint_service, motion_feed, enrollment_jobs=None):
    from flask import Flask, jsonify

    app = Flask(__name__)

    @app.get("/health")
    def health():
        data = motion_feed.snapshot()
        return jsonify(success=True, message="Raspberry Pi connected",
                       motion_available=data["available"])

    @app.post("/scan")
    def scan():
        body, status = fingerprint_service.scan()
        return jsonify(body), status

    @app.get("/motion")
    def motion():
        data = motion_feed.snapshot()
        response = jsonify(success=data["available"], data=data)
        response.headers["Cache-Control"] = "no-store"
        return response, 200 if data["available"] else 503

    if enrollment_jobs is not None:
        from fingerprint_enrollment_relay import register_enrollment_relay
        register_enrollment_relay(app, enrollment_jobs)

    return app


def main(enrollment_factory=None):
    """Default demo, or an explicitly composed enrollment runtime.

    enrollment_factory borrows the existing FingerprintService. It must return
    an object with jobs/close(), without opening another UART or generating keys.
    No environment variable or local file enables enrollment automatically.
    """
    import serial
    import adafruit_fingerprint as af
    from PIL import ImageFont
    from luma.core.interface.serial import i2c
    from luma.core.render import canvas
    from luma.oled.device import sh1106

    font = ImageFont.truetype("/usr/share/fonts/truetype/nanum/NanumGothic.ttf", 13)
    oled = sh1106(i2c(port=1, address=0x3C), width=128, height=64)

    def draw_lines(lines):
        with canvas(oled) as draw:
            for row, line in enumerate(lines[:4]):
                draw.text((0, row * 16), line, font=font, fill="white")

    display = DisplayCoordinator(draw_lines)
    uart = None
    worker = None
    fingerprint = None
    enrollment = None
    try:
        uart = serial.Serial(FINGER_PORT, 57600, timeout=1, exclusive=True)
        finger = af.Adafruit_Fingerprint(uart)
        names = load_user_names(Path(__file__).with_name("user_names.local.json"))
        fingerprint = FingerprintService(finger, af, display, names=names)
        feed = MotionFeed()
        if enrollment_factory is not None:
            enrollment = enrollment_factory(fingerprint)
            if enrollment is None:
                raise RuntimeError("지문 등록 실행 구성이 필요합니다")
        worker = MotionWorker(feed, display, serial.Serial)
        app = create_app(fingerprint, feed, None if enrollment is None else enrollment.jobs)
        display.show_scan("시스템 준비 완료", "PC 요청 대기")
        worker.start()
        app.run(host="0.0.0.0", port=HTTP_PORT, debug=False,
                use_reloader=False, threaded=True)
    finally:
        if fingerprint is not None:
            fingerprint.stopping.set()
        try:
            if enrollment is not None:
                enrollment.close()
        finally:
            try:
                if worker is not None and worker.thread.ident is not None:
                    worker.stop()
            finally:
                if fingerprint is not None:
                    # A cancelled enrollment must release the SAME lock before
                    # the single owner closes UART; never race an active command.
                    with fingerprint.lock:
                        if uart is not None:
                            uart.close()
                elif uart is not None:
                    uart.close()


if __name__ == "__main__":
    main()
