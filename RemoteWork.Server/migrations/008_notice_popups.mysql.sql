-- RESERVED 008: isolated development review DRAFT ONLY.
-- Member 1 reserved this number on 2026-10-20. DDL/CASCADE/retention NOT approved.
-- Requires common schema1/users and existing notices. Never run on shared DB.
-- No counter/event/session-boundary model; no schema_versions modification.
-- OFF DOES NOT NEUTRALIZE THESE FKs: deleting a notice cascades confirmations.
CREATE TABLE notice_confirmations (
    notice_id INTEGER NOT NULL,
    user_id INTEGER NOT NULL,
    confirmed_at DATETIME NOT NULL,
    PRIMARY KEY (notice_id, user_id),
    CONSTRAINT fk_notice_confirmations_notice FOREIGN KEY (notice_id)
        REFERENCES notices (id) ON DELETE CASCADE ON UPDATE RESTRICT,
    CONSTRAINT fk_notice_confirmations_user FOREIGN KEY (user_id)
        REFERENCES users (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
    INDEX ix_notice_confirmations_user (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
