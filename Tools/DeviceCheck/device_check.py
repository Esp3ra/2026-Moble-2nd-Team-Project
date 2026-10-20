"""Windows-only diagnostic UI for the existing Pi demo API (Python stdlib)."""
import json
import queue
import threading
import time
import tkinter as tk
from dataclasses import dataclass
from tkinter import messagebox, ttk
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import ProxyHandler, Request, build_opener


DEFAULT_URL = "http://raspberrypi.local:8080"
MAX_BODY = 65536


def normalize_url(value):
    value = value.strip().rstrip("/")
    try:
        parsed = urlsplit(value)
        port = parsed.port
    except ValueError as error:
        raise ValueError("서버 주소의 포트 번호를 확인하세요.") from error
    if (parsed.scheme not in ("http", "https") or not parsed.hostname
            or parsed.username is not None or parsed.password is not None
            or parsed.path or parsed.query or parsed.fragment
            or (port is not None and port < 1)):
        raise ValueError("http://raspberrypi.local:8080 형식의 서버 주소를 입력하세요.")
    return value


def api_request(base, path, method="GET", timeout=3):
    request = Request(base + path, data=b"" if method == "POST" else None,
                      method=method, headers={"Accept": "application/json"})
    # Local Pi traffic must not be routed through the PC's HTTP proxy.
    opener = build_opener(ProxyHandler({}))
    try:
        response = opener.open(request, timeout=timeout)
    except HTTPError as error:
        response = error  # 409/408/503 still carry useful JSON from the Pi.
    except (URLError, TimeoutError, OSError) as error:
        raise RuntimeError("연결 실패 또는 응답 시간 초과. 라파 서버와 주소를 확인하세요.") from error
    with response:
        raw = response.read(MAX_BODY + 1)
        if len(raw) > MAX_BODY:
            raise RuntimeError("서버 응답이 너무 큽니다.")
        try:
            body = json.loads(raw.decode("utf-8"))
        except (ValueError, UnicodeError) as error:
            raise RuntimeError("JSON 응답이 아닙니다. 라파 주소와 포트를 확인하세요.") from error
        if not isinstance(body, dict):
            raise RuntimeError("예상한 API 응답 형식이 아닙니다.")
        return response.code, body


@dataclass(frozen=True)
class MotionView:
    connection: str
    raw: str = "—"
    cover: str = "확인 불가"
    recent: str = "확인 불가"
    changed: str = "—"
    sample: str = "—"
    thresholds: str = "—"
    simulation: str = "데이터 없음"


def motion_view(status, body):
    data = body.get("data")
    if not isinstance(data, dict):
        raise RuntimeError("조도 응답에 data가 없습니다.")
    if status == 503 and body.get("success") is False:
        return MotionView("라파 응답 정상 · 조도 데이터 없음 / 연결 확인 필요")
    if status != 200 or body.get("success") is not True:
        raise RuntimeError(f"조도 조회 실패: HTTP {status}")
    if data.get("available") is not True or data.get("connected") is not True:
        return MotionView("라파 응답 정상 · 조도 데이터 없음 / 연결 확인 필요")
    raw = data.get("raw_value")
    if type(raw) is not int or not 0 <= raw <= 1023:
        raise RuntimeError("올바른 조도값이 없는 응답입니다.")
    expected = {"CLEAR": False, "COVERED": True, "LEVEL_UNKNOWN": None}
    state = data.get("state")
    if state not in expected or data.get("detected") is not expected[state]:
        raise RuntimeError("가림 상태 응답 형식이 맞지 않습니다.")
    recent = data.get("recent_motion")
    if recent is not True and recent is not False and recent is not None:
        raise RuntimeError("최근 변화 응답 형식이 맞지 않습니다.")
    seconds = data.get("seconds_since_motion")
    seconds_label = "변화 기록 없음"
    if type(seconds) in (int, float) and seconds >= 0:
        seconds_label = f"{seconds:.1f}초 전"
    thresholds = data.get("thresholds")
    threshold_label = "서버 기준 확인 불가"
    if isinstance(thresholds, dict):
        high, low = thresholds.get("detect_at_least"), thresholds.get("release_at_most")
        if type(high) is int and type(low) is int and 0 <= low < high <= 1023:
            threshold_label = f"{high} 이상 가림 / {low} 이하 해제 / 중간값은 이전 상태 유지"
    return MotionView(
        "라파 연결 정상 · 조도 수신 중", str(raw),
        {"CLEAR": "가림 해제", "COVERED": "가림 감지", "LEVEL_UNKNOWN": "초기 판정 대기"}[state],
        "최근 변화 있음" if recent is True else "최근 변화 없음" if recent is False else "변화 기록 없음",
        seconds_label, str(data.get("observed_at") or "—"), threshold_label,
        "손가락 그림자 모의실험" if data.get("simulation") is True else "장치 데이터",
    )


def scan_message(status, body):
    if status == 200 and body.get("success") is True:
        slot, score = body.get("finger_id"), body.get("confidence")
        if type(slot) is not int or slot < 0 or type(score) not in (int, float):
            raise RuntimeError("지문 결과 응답 형식이 맞지 않습니다.")
        return (f"센서 인식 성공 · 슬롯 {slot}\n"
                f"표시 이름: {body.get('name', '이름 없음')} / 센서 점수: {score}\n"
                "회원 계정 일치 확인 전입니다.")
    if status == 200 and body.get("success") is False:
        return "인식 실패 · 등록되지 않은 지문입니다. 다시 요청하세요."
    return {409: "다른 지문 인증이 진행 중입니다. 종료 후 다시 요청하세요.",
            408: "지문 입력 시간이 초과됐습니다. 다시 요청하세요.",
            500: "센서 처리 오류입니다. 라파 터미널을 확인하세요."}.get(
                status, f"지문 요청 실패: HTTP {status}")


class DeviceCheck:
    def __init__(self, root):
        self.root = root
        self.events = queue.Queue()
        self.generation = 0
        self.poll_stop = threading.Event()
        self.closed = False
        self.last_poll = None
        self.reachable = False
        self.scanning = False
        self.base = None
        root.title("5번 · 라즈베리파이 장치 점검")
        root.geometry("760x650")
        root.minsize(720, 620)
        style = ttk.Style(root)
        style.configure("TLabel", font=("맑은 고딕", 10))
        style.configure("Title.TLabel", font=("맑은 고딕", 19, "bold"))
        style.configure("Value.TLabel", font=("맑은 고딕", 22, "bold"))
        frame = ttk.Frame(root, padding=22)
        frame.pack(fill="both", expand=True)
        ttk.Label(frame, text="라파 장치 점검", style="Title.TLabel").pack(anchor="w")
        ttk.Label(frame, text="실제 센서 상태를 확인하는 독립 점검 도구입니다.").pack(anchor="w", pady=(4, 15))
        address_row = ttk.Frame(frame)
        address_row.pack(fill="x")
        self.address = tk.StringVar(value=DEFAULT_URL)
        self.address_box = ttk.Entry(address_row, textvariable=self.address, font=("맑은 고딕", 11))
        self.address_box.pack(side="left", fill="x", expand=True)
        self.connect_button = ttk.Button(address_row, text="연결 / 다시 연결", command=self.connect)
        self.connect_button.pack(side="right", padx=(8, 0))
        self.connection = tk.StringVar(value="연결 전 · 주소를 확인하고 연결 버튼을 누르세요.")
        ttk.Label(frame, textvariable=self.connection, wraplength=680).pack(anchor="w", pady=(10, 14))
        light = ttk.LabelFrame(frame, text="조도 센서 · 1초마다 조회", padding=12)
        light.pack(fill="x")
        self.fields = {}
        for index, (key, title) in enumerate((("raw", "현재 센서값"), ("cover", "현재 가림 상태"),
                                            ("recent", "최근 변화"), ("changed", "마지막 변화"))):
            ttk.Label(light, text=title).grid(row=index, column=0, sticky="w", padx=(0, 25), pady=4)
            self.fields[key] = tk.StringVar(value="—")
            ttk.Label(light, textvariable=self.fields[key], style="Value.TLabel" if index == 0 else "TLabel").grid(
                row=index, column=1, sticky="w")
        self.fields["thresholds"] = tk.StringVar(value="서버 기준 대기")
        self.fields["sample"] = tk.StringVar(value="—")
        self.fields["simulation"] = tk.StringVar(value="데이터 없음")
        for key in ("thresholds", "sample", "simulation"):
            ttk.Label(light, textvariable=self.fields[key], wraplength=640).grid(
                row=4 + ("thresholds", "sample", "simulation").index(key), column=0, columnspan=2, sticky="w", pady=3)
        fingerprint = ttk.LabelFrame(frame, text="지문 센서", padding=12)
        fingerprint.pack(fill="x", pady=(14, 0))
        self.scan_button = ttk.Button(fingerprint, text="지문 인식 요청", command=self.scan, state="disabled")
        self.scan_button.pack(anchor="w")
        self.scan_result = tk.StringVar(value="요청 후 라파 OLED 안내에 따라 손가락을 떼고 다시 올리세요.")
        ttk.Label(fingerprint, textvariable=self.scan_result, wraplength=640).pack(anchor="w", pady=(10, 0))
        ttk.Label(frame, text="가림 해제 후에도 최근 변화 표시는 10초간 남을 수 있습니다.\n"
                             "이 화면은 재석·이석이나 출퇴근을 판정하지 않습니다.",
                  wraplength=680).pack(anchor="w", pady=(14, 0))
        root.protocol("WM_DELETE_WINDOW", self.close)
        root.after(100, self.drain)

    def show_motion(self, view):
        self.connection.set(view.connection)
        for key, variable in self.fields.items():
            variable.set(getattr(view, key))

    def connect(self):
        if self.scanning:
            return
        try:
            base = normalize_url(self.address.get())
        except ValueError as error:
            messagebox.showerror("주소 확인", str(error), parent=self.root)
            return
        self.poll_stop.set()
        self.poll_stop = stop = threading.Event()
        self.generation += 1
        generation = self.generation
        self.base = base
        self.last_poll = None
        self.reachable = False
        self.scan_button.configure(state="disabled")
        self.scan_result.set("지문 요청 대기 · 라파 연결을 확인하고 있습니다.")
        self.show_motion(MotionView("라파 연결 확인 중…"))

        def poll():
            while not stop.is_set():
                try:
                    status, health = api_request(base, "/health")
                    if status != 200 or health.get("success") is not True:
                        raise RuntimeError(f"라파 상태 확인 실패: HTTP {status}")
                    status, body = api_request(base, "/motion")
                    view = motion_view(status, body)
                    event = (generation, "motion", view, time.monotonic())
                except Exception as error:
                    event = (generation, "offline", str(error), time.monotonic())
                self.events.put(event)
                stop.wait(1)
        threading.Thread(target=poll, daemon=True, name="pi-motion-poll").start()

    def scan(self):
        if self.scanning or self.base is None:
            return
        self.scanning = True
        self.scan_button.configure(state="disabled")
        self.connect_button.configure(state="disabled")
        self.address_box.configure(state="disabled")
        self.scan_result.set("지문 인식 중… 라파 OLED 안내를 따라주세요.\n손가락 떼기와 입력은 각각 최대 15초 대기합니다.")
        base, generation = self.base, self.generation

        def request_scan():
            try:
                status, body = api_request(base, "/scan", method="POST", timeout=45)
                message = scan_message(status, body)
            except Exception as error:
                message = (str(error) + "\n요청이 라파에서 계속 처리 중일 수 있습니다. OLED와 터미널을 확인하세요.")
            self.events.put((generation, "scan", message, time.monotonic()))
        threading.Thread(target=request_scan, daemon=True, name="pi-fingerprint-scan").start()

    def apply_event(self, event):
        generation, kind, value, observed = event
        if generation != self.generation:
            return
        if kind == "scan":
            self.scanning = False
            self.connect_button.configure(state="normal")
            self.address_box.configure(state="normal")
            self.scan_result.set(value)
        else:
            self.last_poll = observed
            self.reachable = kind == "motion"
            self.show_motion(value if kind == "motion" else MotionView(value))
        # A 503 motion response still confirms the Pi is reachable for scanning.
        if kind == "offline":
            self.scan_button.configure(state="disabled")
        elif not self.scanning:
            self.scan_button.configure(state="normal" if self.reachable else "disabled")

    def drain(self):
        if self.closed:
            return
        try:
            while True:
                self.apply_event(self.events.get_nowait())
        except queue.Empty:
            pass
        if self.last_poll is not None and time.monotonic() - self.last_poll > 5:
            self.reachable = False
            self.show_motion(MotionView("응답 대기 지연 · 이전 센서값은 숨겼습니다."))
            self.scan_button.configure(state="disabled")
        self.root.after(100, self.drain)

    def close(self):
        self.closed = True
        self.poll_stop.set()
        self.generation += 1
        self.root.destroy()


def main():
    root = tk.Tk()
    DeviceCheck(root)
    root.mainloop()


if __name__ == "__main__":
    main()
