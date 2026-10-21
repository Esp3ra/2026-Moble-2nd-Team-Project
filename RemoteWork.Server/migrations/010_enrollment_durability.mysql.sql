-- Explicit MySQL-only development opt-in. 007 must exist. Common schema remains 1.
-- No old-table ALTER, claim release, sensor command or automatic migration.

CREATE TABLE enrollment_physical_receipts (
	enrollment_id VARCHAR(36) NOT NULL,
	device_id VARCHAR(36) NOT NULL,
	reserved_slot INTEGER NOT NULL,
	operation_revision BIGINT NOT NULL,
	claim_revision BIGINT NOT NULL,
	key_epoch BIGINT NOT NULL,
	ticket_digest VARCHAR(64) NOT NULL,
	intent_probe_hash VARCHAR(64),
	intent_at DATETIME,
	result_envelope BLOB,
	result_hash VARCHAR(64),
	result_id VARCHAR(36),
	outcome VARCHAR(32),
	failure_code VARCHAR(32),
	reported_at DATETIME,
	PRIMARY KEY (enrollment_id),
	CONSTRAINT fk_enrollment_physical_binding FOREIGN KEY(enrollment_id, device_id, reserved_slot) REFERENCES enrollment_jobs (enrollment_id, device_id, reserved_slot),
	CONSTRAINT ck_physical_values CHECK (operation_revision > 0 AND claim_revision > 0 AND key_epoch > 0 AND reserved_slot >= 0),
	CONSTRAINT ck_physical_intent CHECK ((intent_probe_hash IS NULL AND intent_at IS NULL) OR (intent_probe_hash IS NOT NULL AND intent_at IS NOT NULL)),
	CONSTRAINT ck_physical_evidence CHECK (intent_at IS NOT NULL OR result_hash IS NOT NULL),
	CONSTRAINT ck_physical_result CHECK ((result_envelope IS NULL AND result_hash IS NULL AND result_id IS NULL AND outcome IS NULL AND failure_code IS NULL AND reported_at IS NULL) OR (result_envelope IS NOT NULL AND result_hash IS NOT NULL AND result_id IS NOT NULL AND reported_at IS NOT NULL AND outcome IS NOT NULL AND BINARY outcome IN ('sensor_stored','failed','recovery_required') AND OCTET_LENGTH(result_envelope) BETWEEN 1 AND 16384 AND ((BINARY outcome = 'sensor_stored' AND failure_code IS NULL) OR (BINARY outcome <> 'sensor_stored' AND failure_code IS NOT NULL)))),
	UNIQUE (result_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;
