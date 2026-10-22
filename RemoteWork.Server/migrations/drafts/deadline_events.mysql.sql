-- REVIEW PROPOSAL ONLY. No number reserved; no automatic/shared installation.
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
