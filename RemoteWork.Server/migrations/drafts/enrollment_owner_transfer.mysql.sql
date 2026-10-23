-- Reviewed explicit maintenance only, after backup/restore rehearsal and Pi stop/drain.
-- Proposed migration (no allocated formal DDL number). Original 007 is unchanged.
-- Original enrollment_jobs.target_user_id/result/ticket remain immutable evidence.
-- Current mapping user FK, slot/user UNIQUE, mapping ID and claim remain enforced.
CREATE TABLE enrollment_mapping_transfers (
    request_id VARCHAR(36) NOT NULL,
    mapping_id VARCHAR(36) NOT NULL,
    actor_id INTEGER NOT NULL,
    from_user_id INTEGER NOT NULL,
    to_user_id INTEGER NOT NULL,
    from_revision BIGINT NOT NULL,
    to_revision BIGINT NOT NULL,
    reason VARCHAR(255) NOT NULL,
    created_at DATETIME NOT NULL,
    PRIMARY KEY (request_id),
    CONSTRAINT uq_enrollment_transfer_revision UNIQUE (mapping_id, from_revision),
    CONSTRAINT ck_enrollment_transfer_values CHECK (from_revision > 0 AND to_revision = from_revision + 1 AND from_user_id <> to_user_id),
    FOREIGN KEY (mapping_id) REFERENCES enrollment_mappings(mapping_id),
    FOREIGN KEY (actor_id) REFERENCES users(id),
    FOREIGN KEY (from_user_id) REFERENCES users(id),
    FOREIGN KEY (to_user_id) REFERENCES users(id)
) ENGINE=InnoDB CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;

-- One ALTER replaces only the subject-equality constraint with the retained
-- enrollment/location FK. No FOREIGN_KEY_CHECKS=0, job rewrite or constraint gap.
ALTER TABLE enrollment_mappings
    DROP FOREIGN KEY fk_enrollment_mapping_binding,
    ADD CONSTRAINT fk_enrollment_mapping_location
        FOREIGN KEY (enrollment_id, device_id, slot)
        REFERENCES enrollment_jobs(enrollment_id, device_id, reserved_slot);
