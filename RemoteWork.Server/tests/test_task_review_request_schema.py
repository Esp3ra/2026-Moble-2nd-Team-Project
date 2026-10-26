"""Physical raw019 corruption is rejected on the explicitly owned fixture."""
import os
from pathlib import Path

import pytest

from test_task_review_requests import create_submitted, get, review_env

pytestmark = pytest.mark.skipif(os.getenv("REMOTEWORK_RUN_TASK_REVIEW_REQUESTS") != "1"
    or not os.getenv("MYSQL_TEST_URL", "").startswith("mysql"), reason="Owned MySQL only")


@pytest.mark.parametrize("change", [
    "DROP CHECK ck_task_review_status, ADD CONSTRAINT ck_task_review_status CHECK "
        "(HEX(status) IN (HEX('Pending'),HEX('acknowledged')))",
    "DROP CHECK ck_task_review_acknowledgement, ADD CONSTRAINT ck_task_review_acknowledgement CHECK "
        "(status='pending' OR acknowledged_at IS NOT NULL)",
    "MODIFY pending_submission_id BIGINT GENERATED ALWAYS AS "
        "(CASE WHEN status='Pending' THEN submission_id ELSE NULL END) STORED",
    "ALTER CHECK ck_task_review_status NOT ENFORCED",
    "MODIFY reason TEXT NULL",
    "MODIFY reason TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL",
    "MODIFY status VARCHAR(21) NOT NULL",
    "MODIFY created_at DATETIME(6) NOT NULL",
    "DROP INDEX uq_task_review_pending_submission",
    "MODIFY request_id BIGINT NOT NULL",
], ids=["status_literal", "ack_expression", "computed_literal", "not_enforced", "nullable",
        "column_collation", "status_length", "datetime_precision", "missing_unique", "not_autoincrement"])
def test_wrong_019_structure_is_503_without_runtime_repair(review_env, change):
    env = review_env
    target, _ = create_submitted(env)
    engine = env.app.extensions["db_engine"]
    assert engine.url.database.startswith("remote_work_test_")
    raw = Path(__file__).resolve().parents[1] / "migrations/019_task_review_requests.mysql.sql"
    original = "\n".join(line for line in raw.read_text(encoding="utf-8").splitlines()
                         if not line.lstrip().startswith("--")).strip().rstrip(";")
    try:
        with engine.begin() as db:
            db.exec_driver_sql("ALTER TABLE task_review_requests " + change)
            changed = db.exec_driver_sql("SHOW CREATE TABLE task_review_requests").one()[1]
        assert get(env, f"tasks/{target}/review-request-context", expected=503) == "TASK_REVIEW_REQUEST_SCHEMA_REQUIRED"
        with engine.connect() as db:
            assert db.exec_driver_sql("SHOW CREATE TABLE task_review_requests").one()[1] == changed
    finally:
        # This fixture alone owns this empty table. Use original raw019 because
        # replaying SHOW CREATE changes MySQL's implicit charset annotations.
        with engine.begin() as db:
            db.exec_driver_sql("DROP TABLE task_review_requests")
            db.exec_driver_sql(original)
