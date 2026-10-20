-- Development opt-in only. Common users/schema 1 must already exist.
-- No account, template, claim deletion, or schema-version changes.

CREATE TABLE enrollment_devices (
	device_id VARCHAR(36) NOT NULL,
	display_name VARCHAR(80) NOT NULL,
	enabled BOOL NOT NULL,
	key_id VARCHAR(64) NOT NULL,
	key_epoch BIGINT NOT NULL,
	minimum_slot INTEGER NOT NULL,
	maximum_slot INTEGER NOT NULL,
	capacity INTEGER NOT NULL,
	candidate_slots_json TEXT NOT NULL,
	PRIMARY KEY (device_id),
	CONSTRAINT ck_enrollment_profile CHECK (key_epoch > 0 AND minimum_slot >= 0 AND maximum_slot >= minimum_slot AND capacity > 0 AND capacity <= maximum_slot - minimum_slot + 1)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE enrollment_jobs (
	enrollment_id VARCHAR(36) NOT NULL,
	client_request_id VARCHAR(36) NOT NULL,
	issuer_id INTEGER NOT NULL,
	issuer_session_hash VARCHAR(64) NOT NULL,
	target_user_id INTEGER NOT NULL,
	device_id VARCHAR(36) NOT NULL,
	reserved_slot INTEGER NOT NULL,
	operation_revision BIGINT NOT NULL,
	claim_revision BIGINT NOT NULL,
	key_epoch BIGINT NOT NULL,
	ticket_digest VARCHAR(64) NOT NULL,
	state VARCHAR(32) NOT NULL,
	created_at DATETIME NOT NULL,
	expires_at DATETIME NOT NULL,
	updated_at DATETIME NOT NULL,
	completed_at DATETIME,
	result_hash VARCHAR(64),
	result_json TEXT,
	PRIMARY KEY (enrollment_id),
	CONSTRAINT uq_enrollment_issue UNIQUE (issuer_id, issuer_session_hash, client_request_id),
	CONSTRAINT uq_enrollment_claim_binding UNIQUE (enrollment_id, device_id, reserved_slot),
	CONSTRAINT uq_enrollment_user_binding UNIQUE (enrollment_id, device_id, reserved_slot, target_user_id),
	CONSTRAINT ck_enrollment_job_values CHECK (operation_revision > 0 AND claim_revision > 0 AND key_epoch > 0 AND reserved_slot >= 0 AND expires_at > created_at),
	CONSTRAINT ck_enrollment_job_state CHECK (BINARY state IN ('issued','completed','failed','recovery_required','cancelled')),
	FOREIGN KEY(issuer_id) REFERENCES users (id),
	FOREIGN KEY(target_user_id) REFERENCES users (id),
	FOREIGN KEY(device_id) REFERENCES enrollment_devices (device_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE enrollment_claims (
	device_id VARCHAR(36) NOT NULL,
	slot INTEGER NOT NULL,
	enrollment_id VARCHAR(36) NOT NULL,
	revision BIGINT NOT NULL,
	state VARCHAR(24) NOT NULL,
	PRIMARY KEY (device_id, slot),
	CONSTRAINT fk_enrollment_claim_binding FOREIGN KEY(enrollment_id, device_id, slot) REFERENCES enrollment_jobs (enrollment_id, device_id, reserved_slot),
	CONSTRAINT ck_enrollment_claim_values CHECK (revision > 0 AND slot >= 0),
	CONSTRAINT ck_enrollment_claim_state CHECK (BINARY state IN ('reserved','active','quarantined')),
	FOREIGN KEY(device_id) REFERENCES enrollment_devices (device_id),
	UNIQUE (enrollment_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE enrollment_mappings (
	mapping_id VARCHAR(36) NOT NULL,
	enrollment_id VARCHAR(36) NOT NULL,
	device_id VARCHAR(36) NOT NULL,
	user_id INTEGER NOT NULL,
	slot INTEGER NOT NULL,
	revision BIGINT NOT NULL,
	active BOOL NOT NULL,
	created_at DATETIME NOT NULL,
	PRIMARY KEY (mapping_id),
	CONSTRAINT uq_enrollment_mapping_slot UNIQUE (device_id, slot),
	CONSTRAINT uq_enrollment_mapping_user UNIQUE (device_id, user_id),
	CONSTRAINT fk_enrollment_mapping_binding FOREIGN KEY(enrollment_id, device_id, slot, user_id) REFERENCES enrollment_jobs (enrollment_id, device_id, reserved_slot, target_user_id),
	CONSTRAINT ck_enrollment_mapping_values CHECK (revision > 0 AND slot >= 0 AND active IN (0,1)),
	UNIQUE (enrollment_id),
	FOREIGN KEY(device_id) REFERENCES enrollment_devices (device_id),
	FOREIGN KEY(user_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
