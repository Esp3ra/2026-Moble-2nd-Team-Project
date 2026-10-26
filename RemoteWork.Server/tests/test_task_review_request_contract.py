"""Schema grammar and explicit metadata boundaries; no database is opened."""
import pytest

from remotework.features.tasks.review_request_models import TaskReviewRequest
from remotework.features.tasks.review_requests import _expression
from remotework.models import Base


def test_review_model_does_not_join_common_init_db_metadata():
    assert TaskReviewRequest.__table__.metadata is not Base.metadata
    assert "task_review_requests" not in Base.metadata.tables
    assert {foreign.column.table.name for foreign in TaskReviewRequest.__table__.foreign_keys} == {
        "users", "task_assignments", "result_submissions"}


def test_schema_parser_accepts_mysql_syntax_without_erasing_literals_or_grouping():
    assert _expression("CASE WHEN status='pending' THEN submission_id ELSE NULL END") == _expression(
        "(case when (`status` = _utf8mb4'pending') then `submission_id` else NULL end)")
    assert _expression("(status = 'pending' AND acknowledged_at IS NULL AND acknowledged_by IS NULL) "
        "OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND acknowledged_by IS NOT NULL)") == _expression(
        "(((`status` = _utf8mb4'pending') and (`acknowledged_at` is null) and (`acknowledged_by` is null)) "
        "or ((`status` = _utf8mb4'acknowledged') and (`acknowledged_at` is not null) and (`acknowledged_by` is not null)))")
    assert _expression("(a OR b) AND c") != _expression("a OR (b AND c)")


@pytest.mark.parametrize("literal", ["'Pending'", "'pend ing'", "'`pending`'", "'_utf8mb4pending'", "'pending\\n'"])
def test_schema_parser_preserves_literal_bytes(literal):
    assert _expression(f"HEX(status) IN (HEX({literal}),HEX('acknowledged'))") != _expression(
        "HEX(status) IN (HEX('pending'),HEX('acknowledged'))")


@pytest.mark.parametrize("source", ["", "status='pending' trailing", "status='pending'; DROP TABLE t", "_utf8mb4pending", "'pending", "a /* comment */ = b"])
def test_schema_parser_rejects_unknown_or_partial_sql(source):
    # A standalone unknown identifier parses but will never equal a reviewed predicate.
    result = _expression(source)
    assert result is None or result == "_utf8mb4pending"
