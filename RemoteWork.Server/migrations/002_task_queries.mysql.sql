-- DEVELOPMENT REVIEW DRAFT. Not approved for the shared database.
-- Reserved by member 1 on 2026-10-17; see 002_task_queries.md before use.
-- Tests require an explicit disposable MySQL URL and a separate opt-in.
-- No schema_versions row or common expected version is changed here.
-- Execute in order and stop on the first error. DDL is not transactionally reversible.

CREATE TABLE tasks (
    task_id BIGINT NOT NULL AUTO_INCREMENT,
    title VARCHAR(200) NOT NULL,
    description TEXT NOT NULL,
    task_status VARCHAR(30) NOT NULL,
    start_date DATE NOT NULL,
    due_date DATE NOT NULL,
    current_assignment_revision INTEGER NOT NULL,
    row_version BIGINT NOT NULL,
    created_by INTEGER NOT NULL,
    created_at DATETIME NOT NULL,
    updated_at DATETIME NOT NULL,
    PRIMARY KEY (task_id),
    CONSTRAINT ck_tasks_draft_status CHECK (task_status IN ('not_started','in_progress','submitted','approved','returned')
        AND HEX(task_status) IN (HEX('not_started'),HEX('in_progress'),HEX('submitted'),HEX('approved'),HEX('returned'))),
    CONSTRAINT ck_tasks_assignment_revision CHECK (current_assignment_revision > 0),
    CONSTRAINT ck_tasks_row_version CHECK (row_version > 0),
    CONSTRAINT ck_tasks_dates CHECK (due_date >= start_date),
    FOREIGN KEY (created_by) REFERENCES users (id),
    INDEX ix_tasks_due_task (due_date, task_id)
);

CREATE TABLE task_assignments (
    task_id BIGINT NOT NULL,
    revision INTEGER NOT NULL,
    assignee_id INTEGER NOT NULL,
    assigned_by INTEGER NOT NULL,
    assigned_at DATETIME NOT NULL,
    accepted_at DATETIME NULL,
    PRIMARY KEY (task_id, revision),
    CONSTRAINT ck_task_assignments_revision CHECK (revision > 0),
    FOREIGN KEY (task_id) REFERENCES tasks (task_id),
    FOREIGN KEY (assignee_id) REFERENCES users (id),
    FOREIGN KEY (assigned_by) REFERENCES users (id),
    INDEX ix_task_assignments_assignee (assignee_id, task_id, revision)
);

CREATE TABLE task_managers (
    task_id BIGINT NOT NULL,
    user_id INTEGER NOT NULL,
    PRIMARY KEY (task_id, user_id),
    FOREIGN KEY (task_id) REFERENCES tasks (task_id),
    FOREIGN KEY (user_id) REFERENCES users (id),
    INDEX ix_task_managers_user_task (user_id, task_id)
);
