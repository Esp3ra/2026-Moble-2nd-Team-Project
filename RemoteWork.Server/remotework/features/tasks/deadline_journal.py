"""Owner2's explicit mutation connection to the installed common deadline writer.

No new configuration, registration, DDL or legacy baseline. The caller retains
its original authenticated transaction and must propagate provider failures.
"""
from flask import current_app

from ...common import AppError


def record_task_deadline(db, *, task, action, before_due_date):
    """Call after task flush/refresh, before audit/receipt, in the same W1 mutate.

    A retained W1 result never enters this path. Only creation and actual DATE
    changes call it; an inactive or result-only deadline installation keeps the
    existing task writes independent of the optional journal storage.
    """
    if current_app.extensions.get("deadline_http") is not True:
        if current_app.config.get("ENABLE_DEADLINE_ALERTS_DRAFT") is True:
            raise AppError(503, "DEADLINE_EVENT_CONNECTION_REQUIRED",
                           "마감 사건 기록의 실행 연결을 확인해 주세요.")
        return None

    from ...deadline_events import record_change
    return record_change(db, task_id=task.task_id, source_row_version=task.row_version,
                         source_action=action, before_due_date=before_due_date,
                         due_date=task.due_date)
