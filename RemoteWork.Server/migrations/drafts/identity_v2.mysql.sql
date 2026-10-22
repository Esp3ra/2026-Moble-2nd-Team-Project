-- REVIEW DRAFT ONLY: no numbered migration reservation or shared DB application.

-- Common users/admin_guard and enrollment 007 must already exist. Do not run init-db to install this.

-- New identity tables only; no ALTER/DROP/seed/schema_versions write.

CREATE TABLE identity_contexts (
	id VARCHAR(36) NOT NULL, 
	user_id INTEGER NOT NULL, 
	login_request_id VARCHAR(36), 
	token_hash VARCHAR(64), 
	auth_hash VARCHAR(64), 
	context_ref VARCHAR(86) NOT NULL, 
	password_stamp VARCHAR(64) NOT NULL, 
	revoked BOOL NOT NULL, 
	created_at DATETIME NOT NULL, 
	expires_at DATETIME NOT NULL, 
	lookup_until DATETIME NOT NULL, 
	PRIMARY KEY (id), 
	CONSTRAINT uq_identity_login_request UNIQUE (user_id, login_request_id), 
	CONSTRAINT ck_identity_context_time CHECK (expires_at > created_at AND lookup_until > created_at), 
	CONSTRAINT ck_identity_context_kind CHECK ((token_hash IS NOT NULL AND auth_hash IS NULL AND login_request_id IS NOT NULL) OR (token_hash IS NULL AND auth_hash IS NOT NULL AND login_request_id IS NULL)), 
	FOREIGN KEY(user_id) REFERENCES users (id), 
	UNIQUE (token_hash), 
	UNIQUE (auth_hash)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_bin;

CREATE TABLE identity_challenges (
	id VARCHAR(36) NOT NULL, 
	context_id VARCHAR(36) NOT NULL, 
	client_request_id VARCHAR(36) NOT NULL, 
	device_id VARCHAR(36) COLLATE utf8mb4_unicode_ci NOT NULL, 
	mapping_id VARCHAR(36) COLLATE utf8mb4_unicode_ci NOT NULL, 
	purpose VARCHAR(16) NOT NULL, 
	request_bytes BLOB(16384) NOT NULL, 
	state VARCHAR(16) NOT NULL, 
	created_at DATETIME NOT NULL, 
	expires_at DATETIME NOT NULL, 
	lookup_until DATETIME NOT NULL, 
	capture_nonce VARCHAR(86), 
	result_id VARCHAR(36), 
	result_bytes BLOB(16384), 
	result_hash VARCHAR(64), 
	receipt_json TEXT, 
	PRIMARY KEY (id), 
	CONSTRAINT uq_identity_issue_request UNIQUE (context_id, client_request_id), 
	CONSTRAINT ck_identity_state CHECK (BINARY state IN ('issued','verified','rejected','cancelled','expired','revoked')), 
	CONSTRAINT ck_identity_purpose CHECK (BINARY purpose IN ('clock_in','noon_check','clock_out')), 
	CONSTRAINT ck_identity_time CHECK (expires_at > created_at AND lookup_until > created_at), 
	CONSTRAINT ck_identity_result CHECK ((result_bytes IS NULL AND result_hash IS NULL AND result_id IS NULL) OR (result_bytes IS NOT NULL AND result_hash IS NOT NULL AND result_id IS NOT NULL)), 
	FOREIGN KEY(context_id) REFERENCES identity_contexts (id), 
	FOREIGN KEY(device_id) REFERENCES enrollment_devices (device_id), 
	FOREIGN KEY(mapping_id) REFERENCES enrollment_mappings (mapping_id), 
	UNIQUE (result_id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_bin;

CREATE INDEX ix_identity_challenges_device_id ON identity_challenges (device_id);

CREATE TABLE identity_operations (
	context_id VARCHAR(36) NOT NULL, 
	client_request_id VARCHAR(36) NOT NULL, 
	operation VARCHAR(16) NOT NULL, 
	target_id VARCHAR(36) NOT NULL, 
	response_json TEXT NOT NULL, 
	expires_at DATETIME NOT NULL, 
	PRIMARY KEY (context_id, client_request_id), 
	FOREIGN KEY(context_id) REFERENCES identity_contexts (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_bin;

CREATE TABLE identity_approvals (
	digest VARCHAR(64) NOT NULL, 
	challenge_id VARCHAR(36) NOT NULL, 
	probe_nonce VARCHAR(86) NOT NULL, 
	probe_bytes BLOB(16384) NOT NULL, 
	approval_bytes BLOB(16384) NOT NULL, 
	scope VARCHAR(16) NOT NULL, 
	expires_at DATETIME NOT NULL, 
	PRIMARY KEY (digest), 
	CONSTRAINT uq_identity_probe UNIQUE (challenge_id, probe_nonce), 
	CONSTRAINT ck_identity_approval_scope CHECK (BINARY scope IN ('capture','result')), 
	FOREIGN KEY(challenge_id) REFERENCES identity_challenges (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_bin;
