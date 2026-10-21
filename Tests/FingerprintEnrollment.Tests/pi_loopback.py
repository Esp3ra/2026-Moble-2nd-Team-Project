"""Owned loopback interoperability harness; synthetic keys, never Pi deployment."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import sys
import threading

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Devices" / "RaspberryPi"))
import test_fingerprint_enrollment_adapter as fixtures
from test_fingerprint_enrollment import FakeSensor, CODES
from fingerprint_enrollment import SensorProfile
from fingerprint_enrollment_mysql import MySqlEnrollmentAdapter
from fingerprint_enrollment_relay import EnrollmentRelayJobs
from fingerprint_enrollment_wire import decode64, encode64, verify, verify_result_binding, RESULT

sensor = FakeSensor()
adapter = MySqlEnrollmentAdapter(sensor=sensor, codes=CODES, sensor_lock=threading.Lock(),
    keys=fixtures.KEYS, profile=SensorProfile(300, 7, 7))
jobs = EnrollmentRelayJobs(adapter,
                          approval_clock=__import__("time").monotonic)


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_GET(self):
        self.dispatch()

    def do_POST(self):
        self.dispatch()

    def dispatch(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 <= length <= 65536:
                raise ValueError()
            body = json.loads(self.rfile.read(length)) if length else {}
            capability = self.headers.get("X-Enrollment-Relay", "")
            parts = self.path.strip("/").split("/")
            if self.path == "/test/verify":
                raw = decode64(body["result_envelope"], 16384)
                value = verify(raw, RESULT, fixtures.KEYS)
                verify_result_binding(fixtures.REQUEST_PAYLOAD, fixtures.EXPECTED_DIGEST, value)
                result = {"valid": True, "stores": len(sensor.stores)}
            elif parts == ["enrollments", "jobs"] and self.command == "POST":
                result = jobs.start(body, capability)
            elif len(parts) in (3, 4) and parts[:2] == ["enrollments", "jobs"]:
                if len(parts) == 3 and self.command == "GET":
                    result = jobs.snapshot(parts[2], capability)
                elif parts[-1] == "approval":
                    result = jobs.answer(parts[2], capability, body)
                elif parts[-1] == "cancel":
                    result = jobs.cancel(parts[2], capability)
                else:
                    raise ValueError()
            else:
                raise ValueError()
            status, response = 200, {"success": True, "data": result}
        except Exception:
            status, response = 409, {"success": False, "message": "synthetic relay rejected"}
        raw = json.dumps(response).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)


server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
server.daemon_threads = True
thread = threading.Thread(target=server.serve_forever, daemon=True)
thread.start()
try:
    print(json.dumps({"port": server.server_port, "request_envelope": encode64(fixtures.request_bytes())}), flush=True)
    sys.stdin.readline()
finally:
    server.shutdown()
    thread.join(timeout=3)
    server.server_close()
    jobs.close()
