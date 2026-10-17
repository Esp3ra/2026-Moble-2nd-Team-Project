-- DEVELOPMENT REVIEW DRAFT. Not approved for shared DB application.
-- Member 1 reserved 003 for the basic notices table, N4-02/03/06.
-- See 003_notices.md for prerequisites, preservation and recovery gates.
-- No common schema version is changed. No popup/confirmation table is added.
-- Defaults are supplied by the application ORM, not by this SQL.
-- Stop if notices already exists; compare its actual schema instead of overwriting it.

CREATE TABLE notices (
    id INTEGER NOT NULL AUTO_INCREMENT,
    title VARCHAR(200) NOT NULL,
    content TEXT NOT NULL,
    target VARCHAR(80) NOT NULL,
    is_pinned BOOL NOT NULL,
    author_id INTEGER NOT NULL,
    author VARCHAR(80) NOT NULL,
    created_at DATETIME NOT NULL,
    attachment_names_json TEXT NOT NULL,
    PRIMARY KEY (id),
    FOREIGN KEY (author_id) REFERENCES users (id),
    INDEX ix_notices_target_created (target, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
