-- Member1 binding-aware schema, separate from obsolete member4 two-column PK.
-- Review/application gate: no migration number or automatic/shared installation.
-- Apply AFTER deadline_events.mysql.sql, same MySQL/schema1. No baseline.
CREATE TABLE deadline_binding_confirmations_draft (
 event_id VARCHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 user_id INTEGER NOT NULL,
 assignment_revision INTEGER NOT NULL,
 task_id BIGINT NOT NULL,
 confirmed_at DATETIME(6) NOT NULL,
 PRIMARY KEY (event_id,user_id,assignment_revision),
 INDEX ix_deadline_confirm_user_time (user_id,confirmed_at),
 FOREIGN KEY (task_id,event_id) REFERENCES deadline_event_journal_draft(task_id,event_id),
 FOREIGN KEY (task_id,assignment_revision) REFERENCES task_assignments(task_id,revision),
 FOREIGN KEY (user_id) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
