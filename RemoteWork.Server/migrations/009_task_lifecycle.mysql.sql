-- 009 task lifecycle DEVELOPMENT REVIEW DDL, based on member2 alpha73a05d5.
-- Number reservation is now materialized; shared application is NOT approved.
-- Apply either this file OR the original unnumbered draft, never both.
-- MySQL8.0.16+; common+002+004 required; schema version stays1.
-- No ALTER/DROP/data seed/version writes. MySQL DDL implicitly commits.

CREATE TABLE task_cancellations (
    task_id BIGINT NOT NULL,
    cancelled_by INTEGER NOT NULL,
    reason TEXT NOT NULL,
    cancelled_at DATETIME NOT NULL,
    PRIMARY KEY (task_id),
    CONSTRAINT ck_task_cancellation_reason CHECK (LENGTH(TRIM(reason)) > 0),
    FOREIGN KEY (task_id) REFERENCES tasks (task_id),
    FOREIGN KEY (cancelled_by) REFERENCES users (id)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE task_deadline_extensions (
    extension_id BIGINT NOT NULL AUTO_INCREMENT,
    task_id BIGINT NOT NULL,
    assignment_revision INTEGER NOT NULL,
    original_due_date DATE NOT NULL,
    requested_due_date DATE NOT NULL,
    requested_by INTEGER NOT NULL,
    reason TEXT NOT NULL,
    status VARCHAR(20) NOT NULL,
    created_at DATETIME NOT NULL,
    decided_at DATETIME NULL,
    decided_by INTEGER NULL,
    decision_reason TEXT NULL,
    pending_revision INTEGER GENERATED ALWAYS AS
        (CASE WHEN status = 'pending' THEN assignment_revision ELSE NULL END) STORED,
    PRIMARY KEY (extension_id),
    CONSTRAINT ck_task_extension_revision CHECK (assignment_revision > 0),
    CONSTRAINT ck_task_extension_dates CHECK (requested_due_date > original_due_date),
    CONSTRAINT ck_task_extension_reason CHECK (LENGTH(TRIM(reason)) > 0),
    CONSTRAINT ck_task_extension_status CHECK (status IN ('pending','approved','rejected','withdrawn','cancelled')
        AND HEX(status) IN (HEX('pending'),HEX('approved'),HEX('rejected'),HEX('withdrawn'),HEX('cancelled'))),
    CONSTRAINT ck_task_extension_decision CHECK
        ((status = 'pending' AND decided_at IS NULL AND decided_by IS NULL AND decision_reason IS NULL)
         OR (status <> 'pending' AND decided_at IS NOT NULL AND decided_by IS NOT NULL)),
    CONSTRAINT uq_task_extension_one_pending UNIQUE (task_id, pending_revision),
    FOREIGN KEY (task_id, assignment_revision) REFERENCES task_assignments (task_id, revision),
    FOREIGN KEY (requested_by) REFERENCES users (id),
    FOREIGN KEY (decided_by) REFERENCES users (id),
    INDEX ix_task_extension_requested (requested_by, status, extension_id),
    INDEX ix_task_extension_task (task_id, assignment_revision, extension_id)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
