"""Run the optional C# HTTP check against a disposable local SQLite fixture only."""
import argparse
import os
from pathlib import Path
import secrets
import subprocess
import sys
import tempfile
import threading

os.environ["PYTHON_DOTENV_DISABLED"] = "1"
os.environ["REMOTEWORK_ENABLE_NOTICE_DRAFT"] = "1"
root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / "RemoteWork.Server"))
from remotework import create_app
from remotework.auth import create_user
from remotework.db import init_schema, transaction
from werkzeug.serving import make_server, WSGIRequestHandler


class QuietHandler(WSGIRequestHandler):
    def log_request(self, *args, **kwargs):
        pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--client-dll", required=True)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="remote-work-notice-test-") as directory:
        app = create_app({"TESTING": True, "SECRET_KEY": secrets.token_hex(32),
                          "DATABASE_URL": "sqlite:///" + (Path(directory) / "test.db").as_posix()})
        with app.app_context():
            init_schema()
            with transaction() as session:
                for login_id, password, name, role, department in (
                    ("admin1", "Admin-test-2026!", "������", "admin", "���"),
                    ("staff1", "Staff-test-2026!", "����", "employee", "������")):
                    create_user(session, {"login_id": login_id, "password": password,
                        "display_name": name, "role": role, "department": department,
                        "employee_no": login_id})
        server = make_server("127.0.0.1", 0, app, threaded=True, request_handler=QuietHandler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            result = subprocess.run(["dotnet", str(Path(args.client_dll).resolve()),
                                     f"http://127.0.0.1:{server.server_port}"], timeout=90)
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
            app.extensions["db_engine"].dispose()
        return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
