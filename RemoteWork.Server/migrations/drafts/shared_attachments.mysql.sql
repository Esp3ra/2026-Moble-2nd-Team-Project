-- Member 3 shared attachment DDL REVIEW DRAFT. No automatic application.
-- Requires common/002/004/006. No seed, ALTER, DROP or schema-version change.


CREATE TABLE result_shared_files (
	id VARCHAR(36) NOT NULL,
	owner_id INTEGER NOT NULL,
	upload_task_id BIGINT,
	name VARCHAR(255) NOT NULL,
	size BIGINT NOT NULL,
	sha256 VARCHAR(64) NOT NULL,
	state VARCHAR(16) NOT NULL,
	created_at DATETIME NOT NULL,
	expires_at DATETIME NOT NULL,
	PRIMARY KEY (id),
	CONSTRAINT ck_shared_file_size CHECK (size >= 0 AND size <= 26214400),
	CONSTRAINT ck_shared_file_state CHECK (HEX(state) IN (HEX('uploading'),HEX('ready'),HEX('deleting'))),
	FOREIGN KEY(owner_id) REFERENCES users (id),
	FOREIGN KEY(upload_task_id) REFERENCES tasks (task_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
CREATE INDEX ix_shared_file_cleanup ON result_shared_files (state, expires_at);

CREATE TABLE result_shared_chunks (
	attachment_id VARCHAR(36) NOT NULL,
	offset INTEGER NOT NULL,
	size INTEGER NOT NULL,
	sha256 VARCHAR(64) NOT NULL,
	PRIMARY KEY (attachment_id, offset),
	CONSTRAINT ck_shared_chunk CHECK (offset >= 0 AND size > 0 AND size <= 16384),
	FOREIGN KEY(attachment_id) REFERENCES result_shared_files (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE TABLE result_shared_links (
	attachment_id VARCHAR(36) NOT NULL,
	resource_type VARCHAR(16) NOT NULL,
	resource_id BIGINT NOT NULL,
	task_id BIGINT NOT NULL,
	submission_id BIGINT,
	PRIMARY KEY (attachment_id, resource_type, resource_id),
	CONSTRAINT ck_shared_link_resource CHECK ((HEX(resource_type)=HEX('task') AND resource_id=task_id AND submission_id IS NULL) OR (HEX(resource_type)=HEX('submission') AND resource_id=submission_id AND submission_id IS NOT NULL)),
	FOREIGN KEY(attachment_id) REFERENCES result_shared_files (id),
	FOREIGN KEY(task_id) REFERENCES tasks (task_id),
	FOREIGN KEY(submission_id) REFERENCES result_submissions (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
CREATE INDEX ix_shared_link_resource ON result_shared_links (resource_type, resource_id);
CREATE INDEX ix_shared_link_task ON result_shared_links (task_id);
