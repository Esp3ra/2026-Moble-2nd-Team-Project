-- W1 opt-in support draft. Execute only after review, backup and isolated verification.
-- This script creates ONE new table; it does not migrate 002/003 or change schema_versions.
CREATE TABLE write_receipts (
    key_hash VARCHAR(64) NOT NULL,
    user_id INTEGER NOT NULL,
    action VARCHAR(80) NOT NULL,
    target VARCHAR(80) NOT NULL,
    request_hash VARCHAR(64) NOT NULL,
    result_json TEXT NOT NULL,
    http_status INTEGER NOT NULL,
    created_at DATETIME NOT NULL,
    expires_at DATETIME NOT NULL,
    PRIMARY KEY (key_hash),
    FOREIGN KEY (user_id) REFERENCES users (id),
    INDEX ix_write_receipts_expiry (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
