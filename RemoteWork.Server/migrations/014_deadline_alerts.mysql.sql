-- 014: explicit reviewed installation only; schema1 is unchanged.
-- Parent tasks/users must exist. No baseline, seed, ALTER or schema1 changes.
CREATE TABLE deadline_event_journal_draft (
 event_id VARCHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 task_id BIGINT NOT NULL,
 event_order BIGINT NOT NULL,
 source_row_version BIGINT NOT NULL,
 source_action VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 before_due_date DATE NULL,
 due_date DATE NOT NULL,
 recorded_at DATETIME(6) NOT NULL,
 actor_id INTEGER NOT NULL,
 PRIMARY KEY (event_id),
 UNIQUE (task_id,event_id),
 UNIQUE (task_id,event_order),
 UNIQUE (task_id,source_row_version),
 CONSTRAINT ck_deadline_event_positive CHECK (event_order > 0 AND source_row_version > 0),
 FOREIGN KEY (task_id) REFERENCES tasks(task_id),
 FOREIGN KEY (actor_id) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE deadline_event_heads_draft (
 task_id BIGINT NOT NULL,
 event_id VARCHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 PRIMARY KEY (task_id),
 FOREIGN KEY (task_id) REFERENCES tasks(task_id),
 FOREIGN KEY (task_id,event_id) REFERENCES deadline_event_journal_draft(task_id,event_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Member1 binding-aware schema, separate from obsolete member4 two-column PK.
-- Explicit installation only; never apply the legacy two-column confirmation schema.
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

