-- Member 3 result tables only. No seed, ALTER, DROP or global schema-version change.


CREATE TABLE result_department_reviewers (
	department VARCHAR(80) NOT NULL,
	reviewer_id INTEGER NOT NULL,
	PRIMARY KEY (department),
	FOREIGN KEY(reviewer_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE TABLE result_upper_reviewers (
	submitter_id INTEGER NOT NULL,
	reviewer_id INTEGER NOT NULL,
	PRIMARY KEY (submitter_id),
	CONSTRAINT ck_result_upper_other CHECK (submitter_id <> reviewer_id),
	FOREIGN KEY(submitter_id) REFERENCES users (id),
	FOREIGN KEY(reviewer_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE TABLE result_submission_gates (
	task_id BIGINT NOT NULL,
	extension_pending BOOL NOT NULL,
	PRIMARY KEY (task_id),
	FOREIGN KEY(task_id) REFERENCES tasks (task_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE TABLE result_submissions (
	id BIGINT NOT NULL AUTO_INCREMENT,
	task_id BIGINT NOT NULL,
	assignment_revision INTEGER NOT NULL,
	round_no BIGINT NOT NULL,
	submitted_by INTEGER NOT NULL,
	reviewer_at_submission INTEGER NOT NULL,
	description TEXT NOT NULL,
	status VARCHAR(16) NOT NULL,
	submitted_at DATETIME NOT NULL,
	PRIMARY KEY (id),
	FOREIGN KEY(task_id, assignment_revision) REFERENCES task_assignments (task_id, revision),
	CONSTRAINT ck_result_round CHECK (round_no > 0),
	CONSTRAINT ck_result_submission_status CHECK (HEX(status) IN (HEX('submitted'),HEX('approved'),HEX('returned'))),
	FOREIGN KEY(task_id) REFERENCES tasks (task_id),
	FOREIGN KEY(submitted_by) REFERENCES users (id),
	FOREIGN KEY(reviewer_at_submission) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
CREATE UNIQUE INDEX uq_result_task_round ON result_submissions (task_id, round_no);

CREATE TABLE result_attachments (
	id VARCHAR(36) NOT NULL,
	task_id BIGINT NOT NULL,
	owner_id INTEGER NOT NULL,
	submission_id BIGINT,
	name VARCHAR(255) NOT NULL,
	size BIGINT NOT NULL,
	sha256 VARCHAR(64) NOT NULL,
	state VARCHAR(16) NOT NULL,
	created_at DATETIME NOT NULL,
	expires_at DATETIME NOT NULL,
	PRIMARY KEY (id),
	CONSTRAINT ck_result_file_size CHECK (size >= 0 AND size <= 26214400),
	CONSTRAINT ck_result_file_state CHECK (HEX(state) IN (HEX('uploading'),HEX('ready'),HEX('deleting'))),
	FOREIGN KEY(task_id) REFERENCES tasks (task_id),
	FOREIGN KEY(owner_id) REFERENCES users (id),
	FOREIGN KEY(submission_id) REFERENCES result_submissions (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
CREATE INDEX ix_result_file_expiry ON result_attachments (submission_id, expires_at);

CREATE TABLE result_decisions (
	id BIGINT NOT NULL AUTO_INCREMENT,
	submission_id BIGINT NOT NULL,
	reviewer_id INTEGER NOT NULL,
	outcome VARCHAR(16) NOT NULL,
	reason TEXT NOT NULL,
	decided_at DATETIME NOT NULL,
	PRIMARY KEY (id),
	CONSTRAINT ck_result_decision CHECK (HEX(outcome) IN (HEX('approved'),HEX('returned'))),
	UNIQUE (submission_id),
	FOREIGN KEY(submission_id) REFERENCES result_submissions (id),
	FOREIGN KEY(reviewer_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE TABLE result_events (
	id BIGINT NOT NULL AUTO_INCREMENT,
	operation_id VARCHAR(36) NOT NULL,
	task_id BIGINT NOT NULL,
	assignment_revision INTEGER NOT NULL,
	submission_id BIGINT NOT NULL,
	actor_id INTEGER NOT NULL,
	kind VARCHAR(24) NOT NULL,
	summary VARCHAR(200) NOT NULL,
	occurred_at DATETIME NOT NULL,
	PRIMARY KEY (id),
	FOREIGN KEY(task_id, assignment_revision) REFERENCES task_assignments (task_id, revision),
	UNIQUE (operation_id),
	FOREIGN KEY(task_id) REFERENCES tasks (task_id),
	FOREIGN KEY(submission_id) REFERENCES result_submissions (id),
	FOREIGN KEY(actor_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
CREATE INDEX ix_result_event_task_id ON result_events (task_id, id);

CREATE TABLE result_upload_chunks (
	attachment_id VARCHAR(36) NOT NULL,
	offset INTEGER NOT NULL,
	size INTEGER NOT NULL,
	sha256 VARCHAR(64) NOT NULL,
	PRIMARY KEY (attachment_id, offset),
	CONSTRAINT ck_result_chunk CHECK (offset >= 0 AND size > 0 AND size <= 16384),
	FOREIGN KEY(attachment_id) REFERENCES result_attachments (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
