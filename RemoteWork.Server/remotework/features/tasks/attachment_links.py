"""Member 2 creation/cancellation consumers of the registered file provider.

No provider/model is imported when it is disabled. Persisted task references
must not be stranded by disabling that provider: a current locking read denies
cancellation until the owner reconnects it. This module owns no files or DDL.
"""
from flask import current_app, g
from sqlalchemy import inspect, text

from ...common import AppError
from ...db import get_db
from ...write_contract import ExpectedVersions, positive_bigint


_COLUMNS = {
    "result_shared_files": {"id", "owner_id", "upload_task_id", "name", "size", "sha256",
                            "state", "created_at", "expires_at"},
    "result_shared_chunks": {"attachment_id", "offset", "size", "sha256"},
    "result_shared_links": {"attachment_id", "resource_type", "resource_id", "task_id", "submission_id"},
}
_KEYS = {"result_shared_files": ["id"], "result_shared_chunks": ["attachment_id", "offset"],
         "result_shared_links": ["attachment_id", "resource_type", "resource_id"]}


def attach_created_task_files(db, *, task_id, attachment_ids):
    """Attach completed files after initial task/assignment/managers flush, same TX.

    Called only by the first creation mutation. Omission/[] require no provider;
    replaying a committed receipt must not recheck or mutate its file links.
    """
    if not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the current authenticated_transaction for task attachment creation")
    from .attachment_selection import validate_attachment_selection
    validate_attachment_selection({"attachment_ids": attachment_ids})
    if not attachment_ids:
        return
    provider = current_app.extensions.get("results_shared_attachments")
    replace = getattr(provider, "replace_task_files", None)
    if not callable(replace):
        raise AppError(503, "TASK_ATTACHMENT_PROVIDER_REQUIRED",
                       "업무 등록 첨부를 연결할 공용 첨부 기능이 필요합니다. 담당자에게 연결 상태를 확인해 주세요.")
    replace(db, positive_bigint(str(task_id), "task_id"), attachment_ids,
            expected=ExpectedVersions(row_version=1, assignment_revision=1))


def release_cancelled_task_files(db, *, task_id, expected):
    """Release only task links before cancellation/version mutation, same TX."""
    if not getattr(g, "write_transaction_active", False) or db is not get_db():
        raise RuntimeError("Use the current authenticated_transaction for task attachment release")
    if not isinstance(expected, ExpectedVersions):
        raise TypeError("Use the validated original ExpectedVersions")
    task_id = positive_bigint(str(task_id) if type(task_id) is int else task_id, "task_id")
    provider = current_app.extensions.get("results_shared_attachments")
    release = getattr(provider, "release_task_files", None)
    if callable(release):
        # The owner repeats current task/manager/version checks, locks links and
        # files, and schedules physical cleanup only after the business TX.
        release(db, task_id, expected=expected)
        return

    inspector = inspect(db.connection())
    present = [inspector.has_table(name) for name in _COLUMNS]
    if not any(present):
        return
    if not all(present) or any(
            {column["name"] for column in inspector.get_columns(name)} != columns
            or inspector.get_pk_constraint(name).get("constrained_columns") != _KEYS[name]
            for name, columns in _COLUMNS.items()):
        raise AppError(503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED", "공용 첨부 저장소의 준비 상태를 확인해 주세요.")
    # Current read: auth may have opened an older RR snapshot. Parent/guard
    # locks are already held by the caller, so a provider cannot add a new
    # reference concurrently after this check and before cancellation.
    referenced = db.execute(text(
        "SELECT attachment_id FROM result_shared_links "
        "WHERE resource_type = :resource_type AND (resource_id = :task_id OR task_id = :task_id) "
        "ORDER BY attachment_id FOR UPDATE"),
        {"resource_type": "task", "task_id": task_id}).first()
    if referenced is not None:
        raise AppError(503, "TASK_ATTACHMENT_PROVIDER_REQUIRED",
            "업무 첨부 연결을 안전하게 해제할 공용 첨부 기능이 필요합니다. 담당자에게 연결 상태를 확인해 주세요.")
