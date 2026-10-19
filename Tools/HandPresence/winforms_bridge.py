"""Private stdio bridge for the WinForms window. No HTTP, files or Tk window."""
import argparse
import base64
import json
import queue
import sys
import threading
import time

from hand_presence import CameraWorker
from presence_core import Region


def packet(update, now=None):
    """Age includes inference and queue delay; never revive an old frame."""
    import cv2
    age = max(0.0, (time.monotonic() if now is None else now) - update.observed)
    evidence = update.evidence
    result = {"version": 1, "state": evidence.state, "age_seconds": age,
              "observed_seconds": update.observed,
              "seconds_without_hand": evidence.seconds_without_hand,
              "error": evidence.error, "jpeg": None}
    if evidence.state != "CAMERA_ERROR" and age > 5:
        result["state"] = "STALE"
    elif update.rgb is not None:
        # The worker supplies only the selected crop. Bound IPC image size too.
        rgb = update.rgb
        height, width = rgb.shape[:2]
        scale = min(1.0, 640 / width, 480 / height)
        if scale < 1:
            rgb = cv2.resize(rgb, (max(1, int(width * scale)), max(1, int(height * scale))))
        ok, encoded = cv2.imencode(".jpg", cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR),
                                  [cv2.IMWRITE_JPEG_QUALITY, 75])
        if not ok:
            raise RuntimeError("미리보기 변환 실패")
        result["jpeg"] = base64.b64encode(encoded).decode("ascii")
        result["age_seconds"] = max(age, time.monotonic() - update.observed) if now is None else age
        if result["age_seconds"] > 5:
            result.update(state="STALE", jpeg=None)
    return result


def run(index, backend, region, source=sys.stdin, output=sys.stdout, worker_factory=CameraWorker):
    updates = queue.Queue(maxsize=1)
    stopping = threading.Event()

    def offer(update):
        try:
            updates.put_nowait(update)
        except queue.Full:
            try:
                updates.get_nowait()
            except queue.Empty:
                pass
            updates.put_nowait(update)

    worker = worker_factory(1, index, backend, region, offer)

    def read_commands():
        # EOF means the owning C# process/window has gone away.
        for line in source:
            if line.strip() == "stop":
                break
        stopping.set()
        worker.stop()

    threading.Thread(target=read_commands, daemon=True, name="winforms-control").start()
    try:
        if stopping.is_set():
            return
        worker.start()
        while not stopping.is_set():
            try:
                update = updates.get(timeout=0.1)
            except queue.Empty:
                if not worker.thread.is_alive():
                    break
                continue
            output.write(json.dumps(packet(update), ensure_ascii=True, allow_nan=False) + "\n")
            output.flush()
            if update.evidence.state == "CAMERA_ERROR":
                break
    except (BrokenPipeError, OSError):
        pass
    finally:
        stopping.set()
        worker.stop()
        if worker.thread.ident is not None:
            worker.thread.join(timeout=1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--index", type=int, choices=range(5), default=0)
    parser.add_argument("--backend", choices=("DSHOW", "MSMF"), default="DSHOW")
    for name, default in (("left", 0), ("top", 35), ("right", 100), ("bottom", 100)):
        parser.add_argument("--" + name, type=int, default=default)
    args = parser.parse_args()
    region = Region(args.left, args.top, args.right, args.bottom)
    # Explicit UTF-8 protocol; native model diagnostics remain on stderr.
    sys.stdout.reconfigure(encoding="utf-8")
    run(args.index, args.backend, region)


if __name__ == "__main__":
    main()
