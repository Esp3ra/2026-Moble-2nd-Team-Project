-- Explicit operator migration after identity_v2.mysql.sql. MySQL 8 only.
-- Back up and rehearse restore first. No backfill or original-work UPDATE.
CREATE TABLE identity_attendance_exceptions (
    work_session_id VARCHAR(36) NOT NULL,
    state VARCHAR(16) NOT NULL,
    reason_code VARCHAR(32) NOT NULL,
    recorded_at DATETIME NOT NULL,
    PRIMARY KEY (work_session_id),
    CONSTRAINT ck_attendance_exception_state CHECK (BINARY state = 'absent'),
    CONSTRAINT ck_attendance_exception_reason CHECK (BINARY reason_code = 'missed_clock_out'),
    FOREIGN KEY (work_session_id) REFERENCES identity_work_sessions (id)
) ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_bin;
