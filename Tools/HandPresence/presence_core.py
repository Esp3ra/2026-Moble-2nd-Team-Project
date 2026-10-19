"""Detection evidence only: no employee identity, attendance or absence decision."""
from dataclasses import dataclass
import math


@dataclass(frozen=True)
class Region:
    left: int = 0
    top: int = 35
    right: int = 100
    bottom: int = 100

    def __post_init__(self):
        values = (self.left, self.top, self.right, self.bottom)
        if any(type(v) is not int for v in values):
            raise ValueError("분석 영역은 0~100 사이 정수로 입력하세요.")
        if not (0 <= self.left < self.right <= 100 and 0 <= self.top < self.bottom <= 100):
            raise ValueError("분석 영역의 왼쪽/위쪽은 오른쪽/아래쪽보다 작아야 합니다.")

    def bounds(self, width, height):
        x1, y1 = width * self.left // 100, height * self.top // 100
        x2, y2 = width * self.right // 100, height * self.bottom // 100
        if x2 - x1 < 32 or y2 - y1 < 32:
            raise ValueError("분석 영역이 너무 작습니다. 가로·세로 32픽셀 이상으로 넓혀주세요.")
        return x1, y1, x2, y2


@dataclass(frozen=True)
class Evidence:
    state: str
    count: int | None = None
    seconds_without_hand: float | None = None
    seconds_since_hand: float | None = None
    error: str | None = None


class EvidenceTracker:
    def __init__(self):
        self.last_hand = None
        self.missing_since = None

    def observe(self, count, now):
        if type(count) is not int or not 0 <= count <= 2 or not math.isfinite(now):
            raise ValueError("Invalid detection evidence")
        if count:
            self.last_hand = now
            self.missing_since = None
            return Evidence("HAND_DETECTED", count, 0.0, 0.0)
        if self.missing_since is None:
            self.missing_since = now
        return Evidence("HAND_NOT_DETECTED", 0, max(0, now - self.missing_since),
                        None if self.last_hand is None else max(0, now - self.last_hand))
