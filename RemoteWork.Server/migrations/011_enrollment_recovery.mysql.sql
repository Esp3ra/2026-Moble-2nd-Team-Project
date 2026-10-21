-- DEVELOPMENT REVIEW: explicit recovery of unsigned pre-store failures.
-- Requires common schema1, 007 jobs/claims and 010 physical receipts.
-- Adds only recovery history. No claim deletion, ALTER or version write.

CREATE TABLE enrollment_recoveries (
	recovery_id VARCHAR(36) NOT NULL,
	source_enrollment_id VARCHAR(36) NOT NULL,
	device_id VARCHAR(36) NOT NULL,
	slot INTEGER NOT NULL,
	source_operation_revision BIGINT NOT NULL,
	source_claim_revision BIGINT NOT NULL,
	key_epoch BIGINT NOT NULL,
	ticket_digest VARCHAR(64) NOT NULL,
	profile_hash VARCHAR(64) NOT NULL,
	source_state VARCHAR(32) NOT NULL,
	operator_id INTEGER NOT NULL,
	report_sha256 VARCHAR(64) NOT NULL,
	source_report_sha256 VARCHAR(64) NOT NULL,
	attestation_sha256 VARCHAR(64) NOT NULL,
	fenced_at DATETIME(6) NOT NULL,
	observed_at DATETIME(6) NOT NULL,
	checked_at DATETIME(6) NOT NULL,
	closed_at DATETIME(6) NOT NULL,
	new_enrollment_id VARCHAR(36),
	new_client_request_id VARCHAR(36),
	reissue_session_hash VARCHAR(64),
	reissued_at DATETIME(6),
	PRIMARY KEY (recovery_id),
	CONSTRAINT fk_recovery_source FOREIGN KEY(source_enrollment_id, device_id, slot) REFERENCES enrollment_jobs (enrollment_id, device_id, reserved_slot),
	CONSTRAINT fk_recovery_new FOREIGN KEY(new_enrollment_id, device_id, slot) REFERENCES enrollment_jobs (enrollment_id, device_id, reserved_slot),
	CONSTRAINT fk_recovery_operator FOREIGN KEY(operator_id) REFERENCES users (id),
	CONSTRAINT ck_recovery_values CHECK (source_operation_revision > 0 AND source_claim_revision > 0 AND key_epoch > 0 AND slot >= 0),
	CONSTRAINT ck_recovery_times CHECK (fenced_at < observed_at AND observed_at <= checked_at AND checked_at <= closed_at),
	CONSTRAINT ck_recovery_source_state CHECK (BINARY source_state IN ('issued','failed','cancelled','recovery_required')),
	CONSTRAINT ck_recovery_reissue CHECK ((new_enrollment_id IS NULL AND new_client_request_id IS NULL AND reissue_session_hash IS NULL AND reissued_at IS NULL) OR (new_enrollment_id IS NOT NULL AND new_client_request_id IS NOT NULL AND reissue_session_hash IS NOT NULL AND reissued_at IS NOT NULL AND source_enrollment_id <> new_enrollment_id)),
	UNIQUE (source_enrollment_id),
	UNIQUE (new_enrollment_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
