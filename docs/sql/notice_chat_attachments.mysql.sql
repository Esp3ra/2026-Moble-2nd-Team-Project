-- REVIEW PROPOSAL ONLY: member 1 assigns a migration number after review.
-- Requires existing 003 notices, 005 chat and 013 shared attachments.
-- No ALTER/DROP/CASCADE/seed/schema-version change or automatic application.

CREATE TABLE result_resource_uploads (
	attachment_id VARCHAR(36) NOT NULL,
	resource_type VARCHAR(16) NOT NULL,
	context_id VARCHAR(36),
	PRIMARY KEY (attachment_id),
	CONSTRAINT ck_resource_upload_context CHECK ((HEX(resource_type)=HEX('notice') AND context_id IS NULL) OR (HEX(resource_type)=HEX('chat') AND context_id IS NOT NULL)),
	FOREIGN KEY(attachment_id) REFERENCES result_shared_files (id),
	FOREIGN KEY(context_id) REFERENCES chat_rooms (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE result_notice_files (
	notice_id INTEGER NOT NULL,
	attachment_id VARCHAR(36) NOT NULL,
	PRIMARY KEY (notice_id, attachment_id),
	FOREIGN KEY(notice_id) REFERENCES notices (id),
	FOREIGN KEY(attachment_id) REFERENCES result_shared_files (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE result_chat_files (
	message_id VARCHAR(36) NOT NULL,
	attachment_id VARCHAR(36) NOT NULL,
	PRIMARY KEY (message_id, attachment_id),
	FOREIGN KEY(message_id) REFERENCES chat_messages (message_id),
	FOREIGN KEY(attachment_id) REFERENCES result_shared_files (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
