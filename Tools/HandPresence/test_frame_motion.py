import unittest
from unittest.mock import Mock

import numpy as np
from frame_motion import FrameMotion
from hand_presence import CameraWorker
from presence_core import Region


class FrameMotionTests(unittest.TestCase):
    def test_first_frame_and_stationary_scene_are_not_movement(self):
        detector = FrameMotion()
        rgb = np.zeros((120, 160, 3), np.uint8)
        self.assertEqual(detector.observe(rgb), (False, 0.0))
        for _ in range(5):
            self.assertEqual(detector.observe(rgb), (False, 0.0))

    def test_local_movement_detected_then_stationary_scene_clears(self):
        detector = FrameMotion()
        rgb = np.zeros((120, 160, 3), np.uint8)
        detector.observe(rgb)
        moved = rgb.copy()
        moved[30:70, 40:80] = 200
        detected, fraction = detector.observe(moved)
        self.assertTrue(detected)
        self.assertGreaterEqual(fraction, .02)
        self.assertFalse(detector.observe(moved)[0])
        self.assertTrue(detector.observe(rgb)[0])

    def test_small_noise_and_uniform_exposure_shift_do_not_reset_timer(self):
        detector = FrameMotion()
        rgb = np.full((120, 160, 3), 70, np.uint8)
        detector.observe(rgb)
        self.assertFalse(detector.observe(rgb + 40)[0])
        noise = np.random.default_rng(123).integers(0, 4, rgb.shape, dtype=np.uint8)
        self.assertFalse(detector.observe(rgb + 40 + noise)[0])

    def test_new_baseline_does_not_invent_movement_after_restart(self):
        a = np.zeros((120, 160, 3), np.uint8)
        b = a.copy(); b[20:80, 20:80] = 255
        detector = FrameMotion(); detector.observe(a)
        self.assertTrue(detector.observe(b)[0])
        self.assertFalse(FrameMotion().observe(b)[0])

    def test_worker_measures_raw_crop_before_drawing_boxes_and_carries_pulse_age(self):
        a = np.zeros((120, 160, 3), np.uint8)
        b = a.copy(); b[20:80, 20:80] = 255
        camera = Mock(); camera.isOpened.return_value = True
        camera.read.side_effect = [(True, a), (True, b), (True, b), (False, None)]
        detector = Mock(); detector.detect.return_value = [(2, 2, 25, 25)]
        updates = []
        worker = CameraWorker(1, 0, 'DSHOW', Region(0, 0, 100, 100), updates.append,
                              camera_factory=lambda *_: camera, detector_factory=lambda: detector, interval=0)
        worker.run()
        self.assertEqual([u.camera_motion for u in updates[:3]], [False, True, False])
        self.assertIsNotNone(updates[2].seconds_since_camera_motion)
        self.assertGreaterEqual(updates[2].seconds_since_camera_motion, 0)
        self.assertEqual(updates[2].evidence.seconds_since_hand, 0)
        self.assertIsNone(updates[-1].rgb)
        camera.release.assert_called_once()


if __name__ == '__main__': unittest.main()
