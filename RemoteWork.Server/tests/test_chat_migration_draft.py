"""Member1 raw005 physical-schema checks, no chat application implementation."""
import os
import subprocess
from pathlib import Path
import pytest
from sqlalchemy import inspect, select
from sqlalchemy.engine import make_url
from sqlalchemy.exc import IntegrityError
from remotework.models import SchemaVersion
from test_task_migration_draft import (owned_databases, fixture_password_hash,
    _seed_common, _common_snapshot, _mysql_tool, _private_file)

pytestmark=pytest.mark.skipif(not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"),
                            reason="Explicit disposable MySQL required for raw005")


@pytest.fixture
def chat_ddl(app):
    engine=app.extensions["db_engine"]
    assert engine.dialect.name=="mysql" and make_url(engine.url).database.startswith("remote_work_test_")
    names=("chat_rooms","chat_members","chat_messages")
    assert not any(inspect(engine).has_table(name) for name in names)
    sql=(Path(__file__).resolve().parents[1]/"migrations/005_chat_storage.mysql.sql").read_text(encoding="utf-8")
    sql="\n".join(line for line in sql.splitlines() if not line.startswith("--"))
    try:
        with engine.begin() as connection:
            for statement in sql.split(";"):
                if statement.strip(): connection.exec_driver_sql(statement)
        yield engine
    finally:
        # These tables were absent before this fixture and belong only to its
        # remote_work_test_ database, including any partial CREATE on failure.
        with engine.begin() as connection:
            for name in reversed(names):
                connection.exec_driver_sql("DROP TABLE IF EXISTS "+name)


def test_raw005_types_collation_fk_indexes_and_common_version(chat_ddl):
    inspector=inspect(chat_ddl)
    expected_fks={"chat_rooms":1,"chat_members":2,"chat_messages":2}
    for table in expected_fks:
        assert len(inspector.get_foreign_keys(table))==expected_fks[table]
        assert all(fk.get("options",{}).get("ondelete")!="CASCADE" for fk in inspector.get_foreign_keys(table))
        with chat_ddl.connect() as connection:
            rows=connection.exec_driver_sql("SELECT ENGINE,TABLE_COLLATION FROM information_schema.tables "
                "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=%s",(table,)).one()
            assert rows==("InnoDB","utf8mb4_unicode_ci")
    messages={column["name"]:column for column in inspector.get_columns("chat_messages")}
    assert str(messages["seq"]["type"])=="BIGINT" and str(messages["sender_id"]["type"])=="INTEGER"
    assert any(index["unique"] and index["column_names"]==["message_id"] for index in inspector.get_indexes("chat_messages"))
    assert any(index["column_names"]==["room_id","seq"] for index in inspector.get_indexes("chat_messages"))
    assert any(index["column_names"]==["user_id","hidden_at","room_id"] for index in inspector.get_indexes("chat_members"))
    with chat_ddl.connect() as connection:
        assert connection.execute(select(SchemaVersion.version)).scalars().all()==[1]


def test_raw005_rejects_orphans_and_duplicate_message_uuid(chat_ddl):
    with chat_ddl.begin() as connection:
        uid=connection.exec_driver_sql("SELECT id FROM users ORDER BY id LIMIT 1").scalar_one()
        connection.exec_driver_sql("INSERT INTO chat_rooms VALUES "
            "('10101010-1010-4010-8010-101010101010','fixture',0,%s,UTC_TIMESTAMP())",(uid,))
        connection.exec_driver_sql("INSERT INTO chat_messages(message_id,room_id,sender_id,sender_name,content,sent_at) "
            "VALUES('20202020-2020-4020-8020-202020202020','10101010-1010-4010-8010-101010101010',%s,'fixture','body',UTC_TIMESTAMP())",(uid,))
    with pytest.raises(IntegrityError),chat_ddl.begin() as connection:
        connection.exec_driver_sql("INSERT INTO chat_members VALUES "
            "('10101010-1010-4010-8010-101010101010',2147483647,'fixture','',NULL)")
    with pytest.raises(IntegrityError),chat_ddl.begin() as connection:
        connection.exec_driver_sql("INSERT INTO chat_messages(message_id,room_id,sender_id,sender_name,content,sent_at) "
            "VALUES('20202020-2020-4020-8020-202020202020','10101010-1010-4010-8010-101010101010',%s,'fixture','again',UTC_TIMESTAMP())",(uid,))


def test_raw005_real_dump_restore_preserves_chat_and_common(owned_databases, fixture_password_hash, tmp_path):
    source=owned_databases("chat_backup")
    _seed_common(source,fixture_password_hash)
    common=_common_snapshot(source)
    sql=(Path(__file__).resolve().parents[1]/"migrations/005_chat_storage.mysql.sql").read_text(encoding="utf-8")
    sql="\n".join(line for line in sql.splitlines() if not line.startswith("--"))
    with source.begin() as connection:
        for statement in sql.split(";"):
            if statement.strip(): connection.exec_driver_sql(statement)
        connection.exec_driver_sql("INSERT INTO chat_rooms VALUES "
            "('10101010-1010-4010-8010-101010101010','복원 시험',0,1,UTC_TIMESTAMP())")
        connection.exec_driver_sql("INSERT INTO chat_members VALUES "
            "('10101010-1010-4010-8010-101010101010',1,'합성 사용자','',UTC_TIMESTAMP())")
        connection.exec_driver_sql("INSERT INTO chat_messages(message_id,room_id,sender_id,sender_name,content,sent_at) "
            "VALUES('20202020-2020-4020-8020-202020202020','10101010-1010-4010-8010-101010101010',1,'합성 사용자','복구 😀',UTC_TIMESTAMP())")
    config=tmp_path/"chat.private.cnf"; dump=tmp_path/"chat.private.sql"
    def quote(value): return '"'+str(value).replace('\\','\\\\').replace('"','\\"').replace('\n','\\n')+'"'
    try:
        _private_file(config);_private_file(dump)
        config.write_text("[client]\nprotocol=TCP\nhost="+quote(source.url.host)+
            "\nport="+str(source.url.port or 3306)+"\nuser="+quote(source.url.username or "")+
            "\npassword="+quote(source.url.password or "")+"\ndefault-character-set=utf8mb4\n",encoding="utf-8")
        env=os.environ.copy();env.pop("MYSQL_PWD",None)
        backup=subprocess.run([_mysql_tool("mysqldump"),"--defaults-file="+config.name,"--no-login-paths",
            "--no-tablespaces","--set-gtid-purged=OFF","--single-transaction","--skip-comments",
            "--skip-add-locks","--skip-lock-tables","--result-file="+dump.name,source.url.database],
            cwd=tmp_path,env=env,capture_output=True)
        assert backup.returncode==0,"Owned chat dump failed; credentials/output omitted"
        assert b"CREATE DATABASE" not in dump.read_bytes().upper()
        restored=owned_databases("chat_restore")
        assert inspect(restored).get_table_names()==[]
        result=subprocess.run([_mysql_tool("mysql"),"--defaults-file="+config.name,"--no-login-paths",
            "--binary-mode=1","--database="+restored.url.database],cwd=tmp_path,env=env,
            input=dump.read_bytes(),capture_output=True)
        assert result.returncode==0,"Owned new-database restore failed; credentials/output omitted"
        assert _common_snapshot(restored)==common
        with restored.connect() as connection:
            assert connection.exec_driver_sql("SELECT content FROM chat_messages").scalar_one()=="복구 😀"
            assert connection.exec_driver_sql("SELECT hidden_at IS NOT NULL FROM chat_members").scalar_one()==1
        assert len(inspect(restored).get_foreign_keys("chat_messages"))==2
        assert inspect(restored).get_table_options("chat_messages")["mysql_collate"]=="utf8mb4_unicode_ci"
    finally:
        config.unlink(missing_ok=True);dump.unlink(missing_ok=True)
