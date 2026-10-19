"""Local hand-detection demo. Capture starts only on the user's Start click."""
import queue
import threading
import time
import tkinter as tk
from dataclasses import dataclass
from tkinter import messagebox, ttk

from download_model import MODEL_PATH, verify_model
from presence_core import Evidence, EvidenceTracker, Region


class HandDetector:
    def __init__(self, model=MODEL_PATH):
        import mediapipe as mp
        verify_model(model)
        self.mp = mp
        options = mp.tasks.vision.HandLandmarkerOptions(
            base_options=mp.tasks.BaseOptions(model_asset_path=str(model)),
            running_mode=mp.tasks.vision.RunningMode.IMAGE,
            num_hands=2, min_hand_detection_confidence=0.5,
            min_hand_presence_confidence=0.5,
        )
        self.task = mp.tasks.vision.HandLandmarker.create_from_options(options)

    def detect(self, rgb):
        # IMAGE mode independently examines even identical, stationary frames.
        result = self.task.detect(self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=rgb))
        height, width = rgb.shape[:2]
        boxes = []
        for hand in result.hand_landmarks[:2]:
            xs, ys = [p.x for p in hand], [p.y for p in hand]
            x1, x2 = max(0, int(min(xs) * width)), min(width - 1, int(max(xs) * width))
            y1, y2 = max(0, int(min(ys) * height)), min(height - 1, int(max(ys) * height))
            boxes.append((x1, y1, x2, y2))
        return boxes

    def close(self):
        self.task.close()


@dataclass
class FrameUpdate:
    session: int
    region_version: int
    evidence: Evidence
    rgb: object = None
    observed: float = 0


class CameraWorker:
    def __init__(self, session, index, backend, region, publish,
                 camera_factory=None, detector_factory=HandDetector, interval=0.1):
        self.session = session
        self.index = index
        self.backend = backend
        self.publish = publish
        self.camera_factory = camera_factory
        self.detector_factory = detector_factory
        self.interval = interval
        self.stopping = threading.Event()
        self.config_lock = threading.Lock()
        self.region = region
        self.region_version = 0
        self.thread = threading.Thread(target=self.run, daemon=True, name="local-hand-camera")

    def start(self):
        self.thread.start()

    def stop(self):
        self.stopping.set()

    def set_region(self, region, version):
        with self.config_lock:
            self.region, self.region_version = region, version

    def run(self):
        camera = detector = None
        version = 0
        try:
            import cv2
            detector = self.detector_factory()
            if self.stopping.is_set():
                return
            backend = {"DSHOW": cv2.CAP_DSHOW, "MSMF": cv2.CAP_MSMF}[self.backend]
            factory = self.camera_factory or cv2.VideoCapture
            # One explicitly selected camera only: never scan other devices.
            camera = factory(self.index, backend)
            if not camera.isOpened():
                raise RuntimeError("카메라를 열 수 없습니다. 카메라 앱을 닫고 번호·연결·권한을 확인하세요.")
            camera.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
            camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
            tracker = EvidenceTracker()
            previous_version = -1
            while not self.stopping.is_set():
                ok, frame = camera.read()
                if self.stopping.is_set():
                    break
                if not ok or frame is None:
                    raise RuntimeError("카메라 영상이 끊겼습니다. USB 연결을 확인하고 다시 시작하세요.")
                with self.config_lock:
                    region, version = self.region, self.region_version
                if version != previous_version:
                    tracker = EvidenceTracker()
                    previous_version = version
                height, width = frame.shape[:2]
                x1, y1, x2, y2 = region.bounds(width, height)
                rgb = cv2.cvtColor(frame[y1:y2, x1:x2].copy(), cv2.COLOR_BGR2RGB)
                del frame  # Full frame is never queued, displayed, written or transmitted.
                boxes = detector.detect(rgb)
                observed = time.monotonic()
                evidence = tracker.observe(len(boxes), observed)
                for left, top, right, bottom in boxes:
                    cv2.rectangle(rgb, (left, top), (right, bottom), (30, 220, 100), 2)
                if not self.stopping.is_set():
                    self.publish(FrameUpdate(self.session, version, evidence, rgb, observed))
                self.stopping.wait(self.interval)
        except Exception as error:
            if not self.stopping.is_set():
                self.publish(FrameUpdate(self.session, version, Evidence("CAMERA_ERROR", error=str(error)),
                                         observed=time.monotonic()))
        finally:
            try:
                if camera is not None:
                    camera.release()
            finally:
                if detector is not None:
                    detector.close()


class HandPresenceWindow:
    def __init__(self, root, worker_factory=CameraWorker):
        self.root = root
        self.worker_factory = worker_factory
        self.worker = None
        self.session = 0
        self.region_version = 0
        self.updates = queue.Queue(maxsize=1)
        self.closed = False
        self.last_frame = None
        self.photo = None
        self.state = "STOPPED"
        root.title("5번 · 손 검출 재석 보조 시연")
        root.geometry("860x760")
        root.minsize(820, 720)
        frame = ttk.Frame(root, padding=18)
        frame.pack(fill="both", expand=True)
        ttk.Label(frame, text="손 검출 · 재석 보조 확인", font=("맑은 고딕", 20, "bold")).pack(anchor="w")
        ttk.Label(frame, text="먼저 웹캠을 아래로 향해 손·책상만 찍도록 고정하고 Windows 카메라 앱을 닫으세요.",
                  font=("맑은 고딕", 10)).pack(anchor="w", pady=(8, 12))
        row = ttk.Frame(frame)
        row.pack(fill="x")
        ttk.Label(row, text="카메라 번호").pack(side="left")
        self.index = tk.StringVar(value="0")
        self.index_box = ttk.Combobox(row, textvariable=self.index, values=tuple(str(n) for n in range(5)),
                                      width=3, state="readonly")
        self.index_box.pack(side="left", padx=(8, 18))
        self.backend = tk.StringVar(value="DSHOW")
        self.backend_box = ttk.Combobox(row, textvariable=self.backend, values=("DSHOW", "MSMF"),
                                        width=8, state="readonly")
        self.backend_box.pack(side="left")
        self.start_button = ttk.Button(row, text="카메라 시작", command=self.start)
        self.start_button.pack(side="left", padx=12)
        self.stop_button = ttk.Button(row, text="중지", command=self.stop, state="disabled")
        self.stop_button.pack(side="left")
        region_box = ttk.LabelFrame(frame, text="분석·표시 영역 (%) · 얼굴/배경을 자동 제거하는 기능은 아닙니다", padding=10)
        region_box.pack(fill="x", pady=12)
        self.region_values = {}
        for key, label, default in (("left", "왼쪽", 0), ("top", "위쪽", 35),
                                    ("right", "오른쪽", 100), ("bottom", "아래쪽", 100)):
            ttk.Label(region_box, text=label).pack(side="left")
            variable = tk.StringVar(value=str(default))
            self.region_values[key] = variable
            ttk.Entry(region_box, textvariable=variable, width=4).pack(side="left", padx=(5, 12))
        ttk.Button(region_box, text="영역 적용", command=self.apply_region).pack(side="left")
        self.status = tk.StringVar(value="중지됨 · 시작 버튼을 누르기 전에는 카메라를 열지 않습니다.")
        ttk.Label(frame, textvariable=self.status, font=("맑은 고딕", 14, "bold"),
                  wraplength=790).pack(anchor="w", pady=(2, 8))
        self.details = tk.StringVar(value="손 검출만 지원합니다. 몸통 검출·본인인증·출퇴근 기록은 연결 전입니다.")
        ttk.Label(frame, textvariable=self.details, wraplength=790).pack(anchor="w", pady=(0, 10))
        preview_frame = tk.Frame(frame, bg="#17212e", height=340)
        preview_frame.pack(fill="both", expand=True)
        preview_frame.pack_propagate(False)
        self.preview = tk.Label(preview_frame, text="카메라 꺼짐", bg="#17212e", fg="white",
                                font=("맑은 고딕", 16))
        self.preview.pack(fill="both", expand=True)
        ttk.Label(frame, text="표시한 분석 영역만 검출합니다. 영상·사진·손 특징점은 저장하거나 서버로 보내지 않습니다.\n"
                             "손 미검출은 이석/결근 판정이 아닙니다. 가만히 있는 손도 검출하는지 실제로 확인하세요.",
                  wraplength=790).pack(anchor="w", pady=(12, 0))
        root.protocol("WM_DELETE_WINDOW", self.close)
        root.after(100, self.drain)

    def selected_region(self):
        return Region(**{key: int(value.get()) for key, value in self.region_values.items()})

    def offer(self, update):
        try:
            self.updates.put_nowait(update)
        except queue.Full:
            try:
                self.updates.get_nowait()
            except queue.Empty:
                pass
            try:
                self.updates.put_nowait(update)
            except queue.Full:
                pass

    def clear_preview(self, text):
        self.preview.configure(image="", text=text)
        self.photo = None

    def start(self):
        if self.worker is not None and self.worker.thread.is_alive():
            return
        try:
            region = self.selected_region()
            index = int(self.index.get())
            if not 0 <= index <= 4:
                raise ValueError("카메라 번호는 0~4 중에서 선택하세요.")
        except ValueError as error:
            messagebox.showerror("입력 확인", str(error), parent=self.root)
            return
        self.session += 1
        self.region_version = 0
        self.last_frame = time.monotonic()
        self.state = "STARTING"
        self.status.set("카메라·모델 준비 중…")
        self.clear_preview("준비 중")
        self.index_box.configure(state="disabled")
        self.backend_box.configure(state="disabled")
        self.start_button.configure(state="disabled")
        self.stop_button.configure(state="normal")
        self.worker = self.worker_factory(self.session, index, self.backend.get(), region, self.offer)
        self.worker.start()

    def apply_region(self):
        try:
            region = self.selected_region()
        except ValueError as error:
            messagebox.showerror("영역 확인", str(error), parent=self.root)
            return
        self.region_version += 1
        if self.worker is not None and self.worker.thread.is_alive():
            self.worker.set_region(region, self.region_version)
            self.status.set("영역 변경 · 새 영상 대기 중")
            self.last_frame = time.monotonic()
        self.clear_preview("새 영역 대기" if self.state != "STOPPED" else "카메라 꺼짐")
        self.details.set("새 영역의 검출 이력은 처음부터 확인합니다.")

    def stop(self):
        self.session += 1  # Discard every late image or detection from the old capture.
        if self.worker is not None:
            self.worker.stop()
        self.state = "STOPPING"
        self.last_frame = None
        self.status.set("중지 요청 · 카메라 입력 종료를 기다리는 중")
        self.details.set("검출 상태를 초기화했습니다.")
        self.clear_preview("영상 숨김")
        self.stop_button.configure(state="disabled")
        self.empty_updates()

    def empty_updates(self):
        try:
            while True:
                self.updates.get_nowait()
        except queue.Empty:
            pass

    def apply_update(self, update):
        if update.session != self.session:
            return
        if update.evidence.state != "CAMERA_ERROR" and update.region_version != self.region_version:
            return
        self.last_frame = update.observed
        self.state = update.evidence.state
        evidence = update.evidence
        if self.state == "CAMERA_ERROR":
            self.status.set("카메라/검출 오류 · 재석 여부 확인 불가")
            self.details.set(evidence.error or "오류가 발생했습니다.")
            self.clear_preview("영상 없음")
            return
        if self.state == "HAND_DETECTED":
            self.status.set(f"손 감지됨 · {evidence.count}개")
            self.details.set("현재 영상에서 손이 검출됐습니다. 본인 확인이나 출퇴근 기록은 아닙니다.")
        else:
            self.status.set("손 미검출 · 이석 판정 아님")
            self.details.set(f"연속 미검출 {evidence.seconds_without_hand:.1f}초 · 손이 영역 밖에 있거나 가려졌을 수 있습니다.")
        if update.rgb is not None:
            from PIL import Image, ImageTk
            image = Image.fromarray(update.rgb)
            width = self.preview.winfo_width()
            height = self.preview.winfo_height()
            image.thumbnail((width - 8 if width > 10 else 760, height - 8 if height > 10 else 340))
            self.photo = ImageTk.PhotoImage(image, master=self.root)
            self.preview.configure(image=self.photo, text="")

    def drain(self):
        if self.closed:
            return
        try:
            while True:
                self.apply_update(self.updates.get_nowait())
        except queue.Empty:
            pass
        if self.worker is not None and not self.worker.thread.is_alive():
            self.index_box.configure(state="readonly")
            self.backend_box.configure(state="readonly")
            self.start_button.configure(state="normal")
            self.stop_button.configure(state="disabled")
            if self.state == "STOPPING":
                self.state = "STOPPED"
                self.status.set("중지됨 · 카메라 입력 종료")
                self.clear_preview("카메라 꺼짐")
        if (self.last_frame is not None and self.state not in ("STOPPED", "STOPPING", "CAMERA_ERROR")
                and time.monotonic() - self.last_frame > 5):
            self.status.set("영상 응답 지연 · 재석 여부 확인 불가")
            self.details.set("이전 검출값과 영상을 숨겼습니다. 중지 후 카메라 상태를 확인하세요.")
            self.clear_preview("영상 지연")
        self.root.after(100, self.drain)

    def close(self):
        self.closed = True
        self.session += 1
        if self.worker is not None:
            self.worker.stop()
        self.empty_updates()
        self.root.destroy()


def main():
    root = tk.Tk()
    HandPresenceWindow(root)
    root.mainloop()


if __name__ == "__main__":
    main()
