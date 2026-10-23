-- 017: Explicit notice original-operation tombstones. No runtime/startup DDL.
-- Apply once after common users/004/notices are ready; preserve schema_versions[1].
-- No notice FK: a deleted notice's original operation MUST NOT recreate it.
CREATE TABLE notice_operations (
    actor_id INTEGER NOT NULL,
    operation_id VARCHAR(36) NOT NULL,
    body_hash VARCHAR(64) NOT NULL,
    notice_id INTEGER NOT NULL,
    receipt_json TEXT NOT NULL,
    created_at DATETIME NOT NULL,
    expires_at DATETIME NOT NULL,
    PRIMARY KEY (actor_id, operation_id),
    FOREIGN KEY (actor_id) REFERENCES users (id)
) ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;
