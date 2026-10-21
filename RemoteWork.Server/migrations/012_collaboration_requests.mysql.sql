-- 012 reserved by member 1 on 2026-10-21. Explicit review/backup/application required.
-- Does not alter existing tables or common schema_versions.
-- Requires existing users/tasks/task_assignments and common W1 storage.


CREATE TABLE collaboration_requests (
	request_id VARCHAR(36) NOT NULL,
	task_id BIGINT NOT NULL,
	assignment_revision INTEGER NOT NULL,
	task_row_version BIGINT NOT NULL,
	requester_id INTEGER NOT NULL,
	recipient_id INTEGER NOT NULL,
	requester_name VARCHAR(100) NOT NULL,
	task_title VARCHAR(200) NOT NULL,
	title VARCHAR(200) NOT NULL,
	request_text TEXT NOT NULL,
	desired_date DATE NOT NULL,
	status VARCHAR(20) NOT NULL,
	row_version BIGINT NOT NULL,
	created_at DATETIME NOT NULL,
	updated_at DATETIME NOT NULL,
	ended_at DATETIME,
	end_reason VARCHAR(40),
	active_revision INTEGER GENERATED ALWAYS AS (CASE WHEN status IN ('pending','accepted') THEN assignment_revision ELSE NULL END) STORED,
	PRIMARY KEY (request_id),
	FOREIGN KEY(task_id, assignment_revision) REFERENCES task_assignments (task_id, revision),
	CONSTRAINT uq_collaboration_active_target UNIQUE (task_id, active_revision, recipient_id),
	CONSTRAINT ck_collaboration_not_self CHECK (requester_id <> recipient_id),
	CONSTRAINT ck_collaboration_versions CHECK (assignment_revision > 0 AND row_version > 0 AND task_row_version > 0),
	CONSTRAINT ck_collaboration_status CHECK (status IN ('pending','accepted','declined','withdrawn','completed','left','expired','ended') AND HEX(status) IN (HEX('pending'),HEX('accepted'),HEX('declined'),HEX('withdrawn'),HEX('completed'),HEX('left'),HEX('expired'),HEX('ended'))),
	FOREIGN KEY(requester_id) REFERENCES users (id),
	FOREIGN KEY(recipient_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE INDEX ix_collaboration_recipient_status ON collaboration_requests (recipient_id, status, created_at);


CREATE TABLE collaboration_events (
	event_id BIGINT NOT NULL AUTO_INCREMENT,
	request_id VARCHAR(36) NOT NULL,
	actor_id INTEGER,
	status VARCHAR(20) NOT NULL,
	reason VARCHAR(40),
	occurred_at DATETIME NOT NULL,
	PRIMARY KEY (event_id),
	FOREIGN KEY(request_id) REFERENCES collaboration_requests (request_id),
	FOREIGN KEY(actor_id) REFERENCES users (id)
)ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci

;

CREATE INDEX ix_collaboration_event_request ON collaboration_events (request_id, event_id);
