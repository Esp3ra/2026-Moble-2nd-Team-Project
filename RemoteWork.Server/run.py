"""Run with Waitress. --demo explicitly uses a separate SQLite database."""
import argparse
import os
import secrets
from pathlib import Path
from dotenv import load_dotenv


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--demo", action="store_true", help="Use local SQLite and create sample accounts on first run")
    parser.add_argument("--init", action="store_true", help="Create missing common tables without deleting data")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    load_dotenv(root / ".env")
    if args.demo:
        instance = root / "instance"
        instance.mkdir(exist_ok=True)
        secret_file = instance / "demo-secret.txt"
        if not secret_file.exists():
            secret_file.write_text(secrets.token_hex(32), encoding="utf-8")
        os.environ["SECRET_KEY"] = secret_file.read_text(encoding="utf-8").strip()
        os.environ["DATABASE_URL"] = "sqlite:///" + (instance / "demo.db").as_posix()
        os.environ["HOST"] = "127.0.0.1"
        os.environ["COOKIE_SECURE"] = "false"
        print("[DEMO] SQLite mode. Shared team use requires the MySQL setup in docs/SETUP.md.", flush=True)
    from remotework import create_app
    from remotework.db import init_schema, get_db, transaction, check_schema
    from remotework.auth import create_user
    from remotework.models import User
    from sqlalchemy import select
    from waitress import serve
    app = create_app()
    with app.app_context():
        if args.init or args.demo:
            init_schema()
        check_schema()
        if args.demo:
            for login_id, name, employee_no, role in (
                ("admin1", "관리자", "ADM-001", "admin"),
                ("staff1", "직원 1", "EMP-001", "employee"),
                ("staff2", "직원 2", "EMP-002", "employee")):
                if get_db().scalar(select(User).where(User.login_id == login_id)) is None:
                    password = secrets.token_urlsafe(12)
                    with transaction() as db:
                        create_user(db, {"login_id": login_id, "password": password, "display_name": name,
                                         "employee_no": employee_no, "department": "개발팀", "role": role})
                    print(f"NEW DEMO ACCOUNT  {login_id} / {password} (save this password)", flush=True)
    host, port = os.getenv("HOST", "127.0.0.1"), int(os.getenv("PORT", "5000"))
    print(f"Web: http://{host}:{port}/login | API: http://{host}:{port}/api/v1/health", flush=True)
    serve(app, host=host, port=port, threads=8, ident="RemoteWork")


if __name__ == "__main__":
    main()
