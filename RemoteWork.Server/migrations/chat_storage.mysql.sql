-- LOCAL DEVELOPMENT PROPOSAL. Migration number and shared application pending member 1 review.
-- Common schema version is unchanged. No existing table or row is deleted.
-- Application supplies UUIDs, booleans and UTC timestamps; no server field defaults.
CREATE TABLE chat_rooms (
    id VARCHAR(36) NOT NULL,
    title VARCHAR(60) NOT NULL,
    is_group BOOL NOT NULL,
    created_by INTEGER NOT NULL,
    created_at DATETIME NOT NULL,
    PRIMARY KEY (id),
    FOREIGN KEY (created_by) REFERENCES users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE chat_members (
    room_id VARCHAR(36) NOT NULL,
    user_id INTEGER NOT NULL,
    display_name VARCHAR(80) NOT NULL,
    department VARCHAR(80) NOT NULL,
    hidden_at DATETIME NULL,
    PRIMARY KEY (room_id, user_id),
    FOREIGN KEY (room_id) REFERENCES chat_rooms(id),
    FOREIGN KEY (user_id) REFERENCES users(id),
    INDEX ix_chat_members_user_hidden (user_id, hidden_at, room_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE chat_messages (
    seq BIGINT NOT NULL AUTO_INCREMENT,
    message_id VARCHAR(36) NOT NULL,
    room_id VARCHAR(36) NOT NULL,
    sender_id INTEGER NOT NULL,
    sender_name VARCHAR(80) NOT NULL,
    content TEXT NOT NULL,
    sent_at DATETIME NOT NULL,
    PRIMARY KEY (seq),
    UNIQUE (message_id),
    FOREIGN KEY (room_id) REFERENCES chat_rooms(id),
    FOREIGN KEY (sender_id) REFERENCES users(id),
    INDEX ix_chat_messages_room_seq (room_id, seq)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
