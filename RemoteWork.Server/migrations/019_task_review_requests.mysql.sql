-- Manual review requests, explicitly approved 2026-10-26. MySQL 8.0.16+.
-- Requires common users, 002 tasks, 004 receipts and 006 results.
-- Apply explicitly after backup and schema comparison; no data/backfill/state changes.
-- Global schema_versions remains 1. Never rerun over an existing table.
CREATE TABLE task_review_requests (
    request_id BIGINT NOT NULL AUTO_INCREMENT,
    task_id BIGINT NOT NULL,
    assignment_revision INTEGER NOT NULL,
    submission_id BIGINT NOT NULL,
    round_no BIGINT NOT NULL,
    requested_by INTEGER NOT NULL,
    reviewer_id INTEGER NOT NULL,
    reason TEXT NOT NULL,
    status VARCHAR(20) NOT NULL,
    created_at DATETIME NOT NULL,
    acknowledged_at DATETIME NULL,
    acknowledged_by INTEGER NULL,
    pending_submission_id BIGINT GENERATED ALWAYS AS
        (CASE WHEN status = 'pending' THEN submission_id ELSE NULL END) STORED,
    PRIMARY KEY (request_id),
    CONSTRAINT ck_task_review_versions CHECK (assignment_revision > 0 AND round_no > 0),
    CONSTRAINT ck_task_review_other CHECK (requested_by <> reviewer_id),
    CONSTRAINT ck_task_review_reason CHECK (LENGTH(TRIM(reason)) > 0),
    CONSTRAINT ck_task_review_status CHECK (HEX(status) IN (HEX('pending'),HEX('acknowledged'))),
    CONSTRAINT ck_task_review_acknowledgement CHECK
        ((status = 'pending' AND acknowledged_at IS NULL AND acknowledged_by IS NULL)
         OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND acknowledged_by IS NOT NULL)),
    CONSTRAINT uq_task_review_pending_submission UNIQUE (pending_submission_id),
    FOREIGN KEY (task_id, assignment_revision) REFERENCES task_assignments (task_id, revision),
    FOREIGN KEY (submission_id) REFERENCES result_submissions (id),
    FOREIGN KEY (requested_by) REFERENCES users (id),
    FOREIGN KEY (reviewer_id) REFERENCES users (id),
    FOREIGN KEY (acknowledged_by) REFERENCES users (id),
    INDEX ix_task_review_task (task_id, assignment_revision, request_id),
    INDEX ix_task_review_requester (requested_by, request_id)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
