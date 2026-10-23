import hashlib
import sys
import tempfile
import time
import tkinter as tk
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

from download_model import MODEL_SHA256, read_verified_model, verify_model
from hand_presence import CameraWorker, FrameUpdate, HandDetector, HandPresenceWindow
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


class ModelBufferTests(unittest.TestCase):
    model_bytes = b'public synthetic model buffer for regression only'
    model_sha256 = hashlib.sha256(model_bytes).hexdigest()

    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.model = Path(self.folder.name) / '한글 경로와 공백' / '모델.task'
        self.model.parent.mkdir()
        self.model.write_bytes(self.model_bytes)
        expected = patch('download_model.MODEL_SHA256', self.model_sha256)
        expected.start()
        self.addCleanup(expected.stop)

    def test_verified_bytes_read_once_from_unicode_path(self):
        original_read = Path.read_bytes
        with patch.object(Path, 'read_bytes', autospec=True, side_effect=original_read) as read:
            self.assertEqual(read_verified_model(self.model), self.model_bytes)
        read.assert_called_once_with(self.model)

    def test_detector_passes_exact_verified_buffer_without_native_path(self):
        sdk = Mock()
        original_read = Path.read_bytes
        with patch.dict(sys.modules, {'mediapipe': sdk}), \
                patch.object(Path, 'read_bytes', autospec=True, side_effect=original_read) as read:
            detector = HandDetector(self.model)
        read.assert_called_once_with(self.model)
        sdk.tasks.BaseOptions.assert_called_once_with(model_asset_buffer=self.model_bytes)
        sdk.tasks.vision.HandLandmarkerOptions.assert_called_once_with(
            base_options=sdk.tasks.BaseOptions.return_value,
            running_mode=sdk.tasks.vision.RunningMode.IMAGE,
            num_hands=2, min_hand_detection_confidence=0.5,
            min_hand_presence_confidence=0.5,
        )
        sdk.tasks.vision.HandLandmarker.create_from_options.assert_called_once_with(
            sdk.tasks.vision.HandLandmarkerOptions.return_value)
        detector.close()
        sdk.tasks.vision.HandLandmarker.create_from_options.return_value.close.assert_called_once_with()

    def test_tampered_model_is_rejected_before_sdk_construction(self):
        self.model.write_bytes(self.model_bytes + b'tampered')
        sdk = Mock()
        with patch.dict(sys.modules, {'mediapipe': sdk}), self.assertRaises(ValueError):
            HandDetector(self.model)
        sdk.tasks.BaseOptions.assert_not_called()
        sdk.tasks.vision.HandLandmarkerOptions.assert_not_called()
        sdk.tasks.vision.HandLandmarker.create_from_options.assert_not_called()

    def test_verify_model_keeps_existing_validation_only_contract(self):
        self.assertIsNone(verify_model(self.model))
        self.model.write_bytes(b'tampered')
        with self.assertRaises(ValueError):
            verify_model(self.model)


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

    def test_slow_inference_preserves_frame_reception_time(self):
        import numpy as np
        camera = Mock()
        camera.isOpened.return_value = True
        camera.read.side_effect = [(True, np.zeros((64, 64, 3), dtype=np.uint8)), (False, None)]
        updates = []
        detector = Mock()
        with patch('hand_presence.time.monotonic', return_value=100) as clock:
            def slow_detect(rgb):
                clock.return_value = 110
                return []
            detector.detect.side_effect = slow_detect
            worker = CameraWorker(1, 0, 'DSHOW', Region(0, 0, 100, 100), updates.append,
                                  camera_factory=lambda *_: camera, detector_factory=lambda: detector, interval=0)
            worker.run()
        self.assertEqual(updates[0].observed, 100)
        self.assertEqual(updates[0].evidence.state, 'HAND_NOT_DETECTED')


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
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_NOT_DETECTED', 0, 31), observed=time.monotonic()))
        self.assertEqual('확인 대기', self.ui.status.get())  # Tk has no Pi/fused decision.
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_NOT_DETECTED', 0, 30), observed=time.monotonic()-6))
        self.assertEqual('카메라 연결 지연', self.ui.status.get())
        self.ui.drain()
        self.assertEqual('카메라 연결 지연', self.ui.status.get())
        self.assertIsNone(self.ui.photo)

    def test_slow_inference_and_queue_delay_hide_old_images_then_recover(self):
        import numpy as np
        pixels = np.zeros((64, 64, 3), dtype=np.uint8)
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1), pixels, time.monotonic()))
        self.assertIsNotNone(self.ui.photo)
        old = FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1), pixels, time.monotonic() - 10)
        self.ui.offer(old)
        self.ui.drain()
        self.assertEqual('카메라 연결 지연', self.ui.status.get())
        self.assertIsNone(self.ui.photo)
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1), pixels, time.monotonic()))
        self.assertEqual('활동 중', self.ui.status.get())
        self.assertIsNotNone(self.ui.photo)

    def test_cropped_preview_can_render_from_synthetic_pixels(self):
        import numpy as np
        self.ui.apply_update(FrameUpdate(0, 0, Evidence('HAND_DETECTED', 1),
                                        np.zeros((64, 64, 3), dtype=np.uint8), time.monotonic()))
        self.assertIsNotNone(self.ui.photo)
        self.assertEqual('활동 중', self.ui.status.get())


if __name__ == '__main__':
    unittest.main()
