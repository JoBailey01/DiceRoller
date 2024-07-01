-- SQLite
-- Note: SQLite lacks a dedicated datetime data type. Instead, use a string that contains an ISO-8601 date and pass it into SQLite date functions to get other date formats.
-- Interaction and user data is specifically for Discord messages
-- Admin_id is for the admin whose credentials were used to issue the update
CREATE TABLE commands (admin_id INT, interaction_id INT PRIMARY KEY, interaction_type TEXT, command_name TEXT, interaction_data TEXT, interaction_channel_id INT, interaction_channel_name TEXT,
    user_id INT, username TEXT, user_discriminator TEXT, user_display_name TEXT,
    date_time TEXT,
    FOREIGN KEY(admin_id) REFERENCES admins(admin_id)
    );

-- Each individual die rolled generates a new dice_roll entry. For example, 2d8+6 generates two d8 roll entries.
CREATE TABLE rolls (interaction_id INT,
    die_size INT, die_roll INT CHECK (die_roll <= die_size),
    FOREIGN KEY(interaction_id) REFERENCES commands(interaction_id)
    );

-- User table, for use in the log service's authentication
-- Salt is stored in base64
-- Salthash is H(password+salt), stored in base64
CREATE TABLE admins (admin_id INT PRIMARY KEY, admin_name TEXT, salt TEXT, salthash TEXT);

-- Table of login tokens, used in authentication
CREATE TABLE sessiontokens (admin_id INT, token TEXT, date_issued TEXT,
    FOREIGN KEY(admin_id) REFERENCES admins(admin_id)
    );
