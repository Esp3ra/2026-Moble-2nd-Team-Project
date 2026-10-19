import io
import threading
import time
import unittest
from unittest.mock import patch

import numpy as np
from hand_presence import FrameUpdate
from presence_core import Evidence, Region
from winforms_bridge import packet, run


class BridgeTests(unittest.TestCase):
    def test_fresh_crop_is_bounded_jpeg_and_stale_frame_has_no_image(self):
        frame = FrameUpdate(1, 0, Evidence('HAND_DETECTED', 1),
                            np.zeros((1000, 1000, 3), dtype=np.uint8), 100)
        fresh = packet(frame, now=101)
        self.assertEqual(fresh['state'], 'HAND_DETECTED')
        import base64
        import cv2
        decoded = cv2.imdecode(np.frombuffer(base64.b64decode(fresh['jpeg']), np.uint8), cv2.IMREAD_COLOR)
        self.assertLessEqual(decoded.shape[0], 480)
        self.assertLessEqual(decoded.shape[1], 640)
        stale = packet(frame, now=110)
        self.assertEqual(stale['state'], 'STALE')
        self.assertEqual(stale['age_seconds'], 10)
        self.assertIsNone(stale['jpeg'])

    def test_camera_error_is_not_converted_to_missing_or_stale(self):
        result = packet(FrameUpdate(1, 0, Evidence('CAMERA_ERROR', error='unplugged'), observed=100), now=110)
        self.assertEqual(result['state'], 'CAMERA_ERROR')
        self.assertIsNone(result['jpeg'])

    def test_progress_packet_keeps_stage_and_has_no_image(self):
        result = packet(FrameUpdate(1, 0, Evidence('STARTING', error='model loading'), observed=100), now=101)
        self.assertEqual(result['state'], 'STARTING')
        self.assertEqual(result['error'], 'model loading')
        self.assertIsNone(result['jpeg'])

    def test_encoding_delay_drops_image(self):
        frame = FrameUpdate(1, 0, Evidence('HAND_DETECTED', 1), np.zeros((64, 64, 3), np.uint8), 100)
        with patch('winforms_bridge.time.monotonic', side_effect=[101, 110]):
            result = packet(frame)
        self.assertEqual(result['state'], 'STALE')
        self.assertIsNone(result['jpeg'])

    def test_owner_eof_stops_worker_and_leaves_no_output(self):
        released = threading.Event()
        class Worker:
            def __init__(self, *args): self.thread = threading.Thread(target=lambda: released.wait(3))
            def start(self): self.thread.start()
            def stop(self): released.set()
        output = io.StringIO()
        run(0, 'DSHOW', Region(), source=io.StringIO(''), output=output, worker_factory=Worker)
        self.assertTrue(released.is_set())
        self.assertEqual(output.getvalue(), '')

    def test_stdio_only_synthetic_frame_and_stop_release_worker(self):
        finished = threading.Event()
        source_ready = threading.Event()
        class Source:
            def __iter__(self):
                source_ready.wait(3)
                yield 'stop\n'
        class Output(io.StringIO):
            def flush(self): source_ready.set()
        class Worker:
            def __init__(self, session, index, backend, region, publish):
                self.stop_event = threading.Event()
                def work():
                    publish(FrameUpdate(1, 0, Evidence('HAND_NOT_DETECTED', 0, 0),
                                        np.zeros((64, 64, 3), np.uint8), time.monotonic()))
                    self.stop_event.wait(3)
                    finished.set()
                self.thread = threading.Thread(target=work)
            def start(self): self.thread.start()
            def stop(self): self.stop_event.set()
        output = Output()
        run(0, 'DSHOW', Region(), Source(), output, Worker)
        import json
        result = json.loads(output.getvalue())
        self.assertEqual(result['state'], 'HAND_NOT_DETECTED')
        self.assertTrue(finished.is_set())


if __name__ == '__main__': unittest.main()
