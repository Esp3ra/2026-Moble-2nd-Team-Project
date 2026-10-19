"""Download the official model only. No images or camera access."""
import hashlib
from pathlib import Path
from urllib.request import urlopen

MODEL_URL = ("https://storage.googleapis.com/mediapipe-models/hand_landmarker/"
             "hand_landmarker/float16/latest/hand_landmarker.task")
MODEL_SHA256 = "fbc2a30080c3c557093b5ddfc334698132eb341044ccee322ccf8bcf3607cde1"
MODEL_PATH = Path(__file__).parent / "models" / "hand_landmarker.task"


def verify_model(path):
    if hashlib.sha256(Path(path).read_bytes()).hexdigest() != MODEL_SHA256:
        raise ValueError("모델 파일 검증 실패. Setup-Hand-Presence.cmd를 실행하세요.")


def download(path=MODEL_PATH):
    path = Path(path)
    if path.exists():
        verify_model(path)
        return path
    with urlopen(MODEL_URL, timeout=30) as response:
        data = response.read(32 * 1024 * 1024 + 1)
    if len(data) > 32 * 1024 * 1024 or hashlib.sha256(data).hexdigest() != MODEL_SHA256:
        raise ValueError("모델 다운로드 검증 실패. 기존 모델은 변경하지 않았습니다.")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".download")
    try:
        temporary.write_bytes(data)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)
    return path


if __name__ == "__main__":
    print("Model ready:", download())
