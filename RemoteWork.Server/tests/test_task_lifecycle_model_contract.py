"""Member 2 ORM table options follow reviewed 009 despite MySQL defaults.

The physical regression uses only a newly owned disposable database. It does
not install lifecycle flags, an integration acknowledgment, or shared DDL.
"""
from datetime import date

import pytest
from sqlalchemy import inspect, select
from sqlalchemy.dialects.mysql import dialect
from sqlalchemy.orm import Session
from sqlalchemy.schema import CreateTable

from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
from remotework.features.tasks.models import TaskAssignment, TaskRecord
from remotework.models import Base, User
from test_task_migration_draft import owned_databases


LIFECYCLE_TABLES = (TaskCancellation.__table__, TaskDeadlineExtension.__table__)
PARENT_TABLES = (User.__table__, TaskRecord.__table__, TaskAssignment.__table__)


@pytest.mark.parametrize("table", LIFECYCLE_TABLES, ids=lambda table: table.name)
def test_lifecycle_mysql_ddl_has_reviewed_009_table_options(table):
    ddl = str(CreateTable(table).compile(dialect=dialect()))
    assert "ENGINE=InnoDB" in ddl
    assert "CHARSET=utf8mb4" in ddl
    assert "COLLATE utf8mb4_unicode_ci" in ddl or "COLLATE=utf8mb4_unicode_ci" in ddl


def test_lifecycle_orm_overrides_other_mysql_defaults_and_preserves_unicode(owned_databases):
    engine = owned_databases("lifecycle_model_defaults")
    with engine.begin() as connection:
        # This database was created by the guarded fixture and is removed by it.
        connection.exec_driver_sql("ALTER DATABASE CHARACTER SET latin1 COLLATE latin1_swedish_ci")
        defaults = connection.exec_driver_sql(
            "SELECT DEFAULT_CHARACTER_SET_NAME, DEFAULT_COLLATION_NAME "
            "FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=DATABASE()").one()
        assert defaults == ("latin1", "latin1_swedish_ci")
        previous_engine = connection.exec_driver_sql("SELECT @@SESSION.default_storage_engine").scalar_one()
        try:
            # The referenced parents require InnoDB; only the two owned models
            # are created while the session default differs from their contract.
            connection.exec_driver_sql("SET SESSION default_storage_engine='InnoDB'")
            Base.metadata.create_all(connection, tables=PARENT_TABLES)
            connection.exec_driver_sql("SET SESSION default_storage_engine='MyISAM'")
            connection.exec_driver_sql("CREATE TABLE inherited_defaults_probe (id INTEGER PRIMARY KEY)")
            Base.metadata.create_all(connection, tables=LIFECYCLE_TABLES)
        finally:
            connection.exec_driver_sql("SET SESSION default_storage_engine=%s", (previous_engine,))

    inspector = inspect(engine)
    inherited = inspector.get_table_options("inherited_defaults_probe")
    assert inherited["mysql_engine"] == "MyISAM"
    assert inherited["mysql_default charset"] == "latin1"
    # SHOW CREATE may omit a charset's default collation, so inspect its actual
    # value independently rather than requiring a rendered COLLATE clause.
    with engine.connect() as connection:
        assert connection.exec_driver_sql(
            "SELECT TABLE_COLLATION FROM information_schema.TABLES "
            "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inherited_defaults_probe'"
        ).scalar_one() == "latin1_swedish_ci"
    for table in LIFECYCLE_TABLES:
        options = inspector.get_table_options(table.name)
        assert options["mysql_engine"] == "InnoDB"
        assert options["mysql_default charset"] == "utf8mb4"
        assert options["mysql_collate"] == "utf8mb4_unicode_ci"

    reason = "업무 취소·마감 연장 🕘"
    with Session(engine) as db, db.begin():
        actor = User(login_id="model_fixture", password_hash="fixture-only", display_name="fixture",
                     employee_no="MODEL-1", department="fixture", role="admin")
        db.add(actor)
        db.flush()
        task = TaskRecord(title="fixture", description="", task_status="in_progress",
                          start_date=date(2026, 10, 20), due_date=date(2026, 10, 21),
                          current_assignment_revision=1, created_by=actor.id)
        db.add(task)
        db.flush()
        db.add(TaskAssignment(task_id=task.task_id, revision=1, assignee_id=actor.id,
                              assigned_by=actor.id))
        db.flush()
        db.add(TaskCancellation(task_id=task.task_id, cancelled_by=actor.id, reason=reason))
        db.add(TaskDeadlineExtension(task_id=task.task_id, assignment_revision=1,
            original_due_date=date(2026, 10, 21), requested_due_date=date(2026, 10, 22),
            requested_by=actor.id, reason=reason, status="pending"))

    with Session(engine) as db:
        assert db.scalar(select(TaskCancellation.reason)) == reason
        assert db.scalar(select(TaskDeadlineExtension.reason)) == reason
