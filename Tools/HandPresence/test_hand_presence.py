import tempfile
import time
import tkinter as tk
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

from download_model import MODEL_SHA256, verify_model
from hand_presence import CameraWorker, FrameUpdate, HandPresenceWindow
from presence_core import Evidence, EvidenceTracker, Region


class EvidenceTests(unittest.TestCase):
    def test_stationary_hand_remains_detected_without_movement(self):
        tracker = EvidenceTracker()
        for now in (0, 10, 60, 600):
            evidence = tracker.observe(1, now)
            self.assertEqual(evidence.state, "HAND_DETECTED")
            self.assertEqual(evidence.seconds_without_hand, 0)

    def test_missing_hand_is_not_absence(self):
        tracker = EvidenceTracker()
        tracker.observe(1, 0)
        tracker.observe(0, 1)
        evidence = tracker.observe(0, 61)
        self.assertEqual(evidence.state, "HAND_NOT_DETECTED")
        self.assertEqual(evidence.seconds_without_hand, 60)
        self.assertEqual(evidence.seconds_since_hand, 61)

    def test_no_initial_hand_has_no_fabricated_last_seen(self):
        evidence = EvidenceTracker().observe(0, 10)
        self.assertIsNone(evidence.seconds_since_hand)

    def test_region_percentages_and_minimum_size(self):
        self.assertEqual(Region(10, 35, 90, 100).bounds(640, 480), (64, 168, 576, 480))
        for values in ((0, 0, 0, 100), (0, -1, 100, 100), (0, 0, 101, 100)):
            with self.assertRaises(ValueError):
                Region(*values)
        with self.assertRaises(ValueError):
            Region(0, 0, 1, 1).bounds(640, 480)

    def test_modified_model_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            model = Path(folder) / 'model.task'
            model.write_bytes(b'not the model')
            with self.assertRaises(ValueError):
                verify_model(model)
        self.assertEqual(len(MODEL_SHA256), 64)


class WorkerTests(unittest.TestCase):
    def test_camera_is_not_opened_on_construction_and_failed_open_releases(self):
        camera = Mock()
        camera.isOpened.return_value = False
        factory = Mock(return_value=camera)
        detector = Mock()
        updates = []
        worker = CameraWorker(1, 0, 'DSHOW', Region(), updates.append,
                              camera_factory=factory, detector_factory=lambda: detector)
        factory.assert_not_called()
        worker.run()
        factory.assert_called_once()
        self.assertEqual(updates[-1].evidence.state, 'CAMERA_ERROR')
        self.assertIsNone(updates[-1].rgb)
        camera.release.assert_called_once()
        detector.close.assert_called_once()

    def test_model_failure_does_not_open_camera(self):
        factory = Mock()
        worker = CameraWorker(1, 0, 'DSHOW', Region(), lambda update: None,
                              camera_factory=factory, detector_factory=Mock(side_effect=ValueError('model error')))
        worker.run()
        factory.assert_not_called()

    def test_only_cropped_frames_reach_model_and_display(self):
        import numpy as np
        original = np.zeros((100, 200, 3), dtype=np.uint8)
        original[:, :100] = (0, 0, 255)
        camera = Mock()
        camera.isOpened.return_value = True
        camera.read.side_effect = [(True, original), (False, None)]
        detector = Mock()
        detector.detect.return_value = [(2, 2, 25, 25)]
        updates = []
        worker = CameraWorker(3, 0, 'DSHOW', Region(50, 0, 100, 100), updates.append,
                              camera_factory=lambda index, backend: camera,
                              detector_factory=lambda: detector, interval=0)
        worker.run()
        model_frame = detector.detect.call_args.args[0]
        self.assertEqual(model_frame.shape, (100, 100, 3))
        self.assertFalse(np.shares_memory(model_frame, original))
        self.assertEqual(updates[0].rgb.shape, (100, 100, 3))
        self.assertEqual(updates[0].evidence.state, 'HAND_DETECTED')
        self.assertEqual(updates[1].evidence.state, 'CAMERA_ERROR')
        self.assertIsNone(updates[1].rgb)
        camera.release.assert_called_once()

    def test_stop_during_model_load_does_not_later_open_camera(self):
        factory = Mock()
        detector = Mock()
        worker = None
        def load():
            worker.stop()
            return detector
        worker = CameraWorker(1, 0, 'DSHOW', Region(), lambda update: None,
                              camera_factory=factory, detector_factory=load)
        worker.run()
        factory.assert_not_called()
        detector.close.assert_called_once()


class WindowTests(unittest.TestCase):
    def setUp(self):
        self.root = tk.Tk()
        self.root.withdraw()
        self.factory = Mock()
        self.ui = HandPresenceWindow(self.root, worker_factory=self.factory)

    def tearDown(self):
        self.ui.close()

    def test_launch_does_not_open_camera(self):
        self.root.update_idletasks()
        self.factory.assert_not_called()
        self.assertEqual(self.ui.state, 'STOPPED')

    def test_old_session_and_old_region_are_ignored(self):
        self.ui.session = 2
        self.ui.region_version = 4
        initial = self.ui.status.get()
        for session, region in ((1, 4), (2, 3)):
            self.ui.apply_update(FrameUpdate(session, region, Evidence('HAND_DETECTED', 1), observed=time.monotonic()))
            self.assertEqual(self.ui.status.get(), initial)

    def test_camera_error_is_not_hidden_by_region_change(self):
        self.ui.region_version = 2
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('CAMERA_ERROR', error='camera failed'), observed=time.monotonic()))
        self.assertEqual(self.ui.state, 'CAMERA_ERROR')
        self.assertEqual(self.ui.details.get(), 'camera failed')
        self.assertIsNone(self.ui.photo)

    def test_stop_discards_queued_and_late_detections(self):
        update = FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1), observed=time.monotonic())
        self.ui.offer(update)
        self.ui.stop()
        self.assertTrue(self.ui.updates.empty())
        self.ui.apply_update(update)
        self.assertEqual(self.ui.state, 'STOPPING')

    def test_latest_frame_queue_does_not_accumulate_images(self):
        for n in range(10):
            self.ui.offer(FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1), observed=n))
        self.assertEqual(self.ui.updates.qsize(), 1)
        self.assertEqual(self.ui.updates.get().observed, 9)

    def test_missing_hand_and_input_stall_keep_distinct_states(self):
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_NOT_DETECTED', 0, 30), observed=time.monotonic()-6))
        self.assertEqual('자리 비움', self.ui.status.get())
        self.ui.drain()
        self.assertEqual('카메라 연결 지연', self.ui.status.get())
        self.assertIsNone(self.ui.photo)

    def test_cropped_preview_can_render_from_synthetic_pixels(self):
        import numpy as np
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1),
                                        np.zeros((64, 64, 3), dtype=np.uint8), time.monotonic()))
        self.assertIsNotNone(self.ui.photo)
        self.assertEqual('활동 중', self.ui.status.get())


if __name__ == '__main__':
    unittest.main()
