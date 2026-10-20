"""Prepare popup tables explicitly, using existing server configuration or --demo."""
import argparse
import os
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--demo", action="store_true", help="Use ONLY the existing local SQLite demo database")
    args = parser.parse_args()
    config = None
    if args.demo:
        root = Path(__file__).resolve().parents[3]
        database = root / "instance" / "demo.db"
        secret = root / "instance" / "demo-secret.txt"
        if not database.is_file() or not secret.is_file():
            parser.error("먼저 Start-Demo.cmd로 기존 데모 DB를 준비해 주세요.")
        os.environ["PYTHON_DOTENV_DISABLED"] = "1"
        config = {"DATABASE_URL": "sqlite:///" + database.as_posix(),
                  "SECRET_KEY": secret.read_text(encoding="utf-8").strip()}
    from ... import create_app
    from .popup_schema import prepare_popup_schema
    app = create_app(config)
    try:
        with app.app_context():
            prepare_popup_schema(app.extensions["db_engine"])
        print("공지 팝업 DB 준비 완료: 기존 공지 보존, 직원 확인 기록 테이블 추가")
    finally:
        app.extensions["db_engine"].dispose()


if __name__ == "__main__":
    main()
