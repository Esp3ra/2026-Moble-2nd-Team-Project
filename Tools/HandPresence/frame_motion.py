"""Movement evidence in the selected crop, before any detection boxes are drawn."""
class FrameMotion:
    def __init__(self, pixel_threshold=25, minimum_fraction=0.02):
        self.previous = None
        self.pixel_threshold = pixel_threshold
        self.minimum_fraction = minimum_fraction

    def observe(self, rgb):
        import cv2
        import numpy as np
        gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
        gray = cv2.resize(gray, (160, 120))
        gray = cv2.GaussianBlur(gray, (5, 5), 0)
        previous, self.previous = self.previous, gray
        if previous is None:
            return False, 0.0
        # Remove a uniform exposure shift; local changes still count as movement.
        delta = gray.astype(np.int16) - previous.astype(np.int16)
        delta = np.abs(delta - np.median(delta))
        fraction = float(np.count_nonzero(delta >= self.pixel_threshold) / delta.size)
        return fraction >= self.minimum_fraction, fraction
