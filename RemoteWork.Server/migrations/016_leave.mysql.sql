-- 016: Member1 opt-in leave DEVELOPMENT policy storage. Explicit application only.
-- No default member/calendar/reviewer seed, no schema_versions change.
CREATE TABLE leave_years (
	year SMALLINT NOT NULL,
	policy_id VARCHAR(32) NOT NULL,
	holidays_json TEXT NOT NULL,
	configured_at DATETIME NOT NULL,
	PRIMARY KEY (year),
	CONSTRAINT ck_leave_year_min CHECK (year >= 2000),
	CONSTRAINT ck_leave_year_max CHECK (year <= 9998)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE leave_balances (
	applicant_id INTEGER NOT NULL,
	year SMALLINT NOT NULL,
	granted_units INTEGER NOT NULL,
	PRIMARY KEY (applicant_id, year),
	CONSTRAINT ck_leave_grant_min CHECK (granted_units >= 0),
	CONSTRAINT ck_leave_grant_max CHECK (granted_units <= 732),
	FOREIGN KEY(applicant_id) REFERENCES users (id),
	FOREIGN KEY(year) REFERENCES leave_years (year)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE leave_reviewers (
	applicant_id INTEGER NOT NULL,
	manager_id INTEGER NOT NULL,
	revision BIGINT NOT NULL,
	PRIMARY KEY (applicant_id),
	CONSTRAINT ck_leave_no_self_review CHECK (applicant_id <> manager_id),
	CONSTRAINT ck_leave_reviewer_revision CHECK (revision > 0),
	FOREIGN KEY(applicant_id) REFERENCES users (id),
	FOREIGN KEY(manager_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE INDEX ix_leave_reviewer_manager ON leave_reviewers (manager_id, applicant_id);

CREATE TABLE leave_requests (
	id VARCHAR(36) NOT NULL,
	applicant_id INTEGER NOT NULL,
	year SMALLINT NOT NULL,
	type VARCHAR(16) NOT NULL,
	start_date DATE NOT NULL,
	end_date DATE NOT NULL,
	charged_units INTEGER NOT NULL,
	reason TEXT NOT NULL,
	status VARCHAR(16) NOT NULL,
	revision BIGINT NOT NULL,
	decision_reason TEXT NOT NULL,
	decision_by INTEGER,
	created_at DATETIME NOT NULL,
	updated_at DATETIME NOT NULL,
	PRIMARY KEY (id),
	FOREIGN KEY(applicant_id, year) REFERENCES leave_balances (applicant_id, year),
	CONSTRAINT ck_leave_charge_min CHECK (charged_units >= 0),
	CONSTRAINT ck_leave_charge_max CHECK (charged_units <= 732),
	CONSTRAINT ck_leave_request_revision CHECK (revision > 0),
	CONSTRAINT ck_leave_date_order CHECK (end_date >= start_date),
	CONSTRAINT ck_leave_status CHECK (status IN ('pending','approved','rejected')),
	CONSTRAINT ck_leave_type CHECK (type IN ('annual','am_half','pm_half','sick','official')),
	FOREIGN KEY(decision_by) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE INDEX ix_leave_applicant_period ON leave_requests (applicant_id, year, start_date, end_date);

CREATE TABLE leave_operations (
	actor_id INTEGER NOT NULL,
	intent_id VARCHAR(36) NOT NULL,
	action VARCHAR(32) NOT NULL,
	target VARCHAR(36) NOT NULL,
	request_id VARCHAR(36) NOT NULL,
	body_hash VARCHAR(64) NOT NULL,
	receipt_json TEXT NOT NULL,
	http_status INTEGER NOT NULL,
	created_at DATETIME NOT NULL,
	expires_at DATETIME NOT NULL,
	PRIMARY KEY (actor_id, intent_id),
	CONSTRAINT ck_leave_operation_status CHECK (http_status IN (200,201)),
	FOREIGN KEY(actor_id) REFERENCES users (id),
	FOREIGN KEY(request_id) REFERENCES leave_requests (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
