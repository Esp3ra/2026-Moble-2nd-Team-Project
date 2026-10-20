"""Reviewed 005 and ORM must explicitly use the same MySQL table options."""
import os

import pytest
from sqlalchemy import inspect
from sqlalchemy.dialects.mysql import dialect
from sqlalchemy.schema import CreateTable


def test_chat_mysql_table_options():
    from remotework.features.chat.storage import TABLES
    for table in TABLES:
        ddl = str(CreateTable(table).compile(dialect=dialect()))
        assert "ENGINE=InnoDB" in ddl
        assert "CHARSET=utf8mb4" in ddl
        assert "COLLATE utf8mb4_unicode_ci" in ddl or "COLLATE=utf8mb4_unicode_ci" in ddl


@pytest.mark.skipif(not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"), reason="dedicated MySQL required")
def test_chat_actual_mysql_table_options(monkeypatch, tmp_path):
    # Reuse the common fixture safety boundary and do not touch a shared DB.
    from sqlalchemy.engine import make_url
    from remotework import create_app
    from remotework.db import init_schema
    url = os.environ["MYSQL_TEST_URL"]
    assert (make_url(url).database or "").startswith("remote_work_test_")
    app = create_app({"TESTING": True, "SECRET_KEY": "collation-test-" + "a" * 40,
                      "DATABASE_URL": url, "ENABLE_CHAT_DRAFT": True})
    engine = app.extensions["db_engine"]
    try:
        with app.app_context():
            init_schema()
            for name in ("chat_rooms", "chat_members", "chat_messages"):
                options = inspect(engine).get_table_options(name)
                assert options["mysql_engine"] == "InnoDB"
                assert options["mysql_default charset"] == "utf8mb4"
                assert options["mysql_collate"] == "utf8mb4_unicode_ci"
    finally:
        engine.dispose()
