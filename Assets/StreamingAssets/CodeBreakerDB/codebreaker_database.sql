PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS level (
    level_id      INTEGER PRIMARY KEY AUTOINCREMENT,
    level_number  INTEGER NOT NULL UNIQUE CHECK (level_number BETWEEN 1 AND 3),
    level_name    TEXT    NOT NULL,
    time_limit    INTEGER NOT NULL CHECK (time_limit > 0),
    description   TEXT
);

CREATE TABLE IF NOT EXISTS player (
    player_id     INTEGER PRIMARY KEY AUTOINCREMENT,
    player_name   TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    date_created  TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS player_progress (
    progress_id       INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id         INTEGER NOT NULL UNIQUE,
    current_level_id  INTEGER NOT NULL,
    current_room      TEXT    NOT NULL,
    timer_status      TEXT    NOT NULL DEFAULT 'RUNNING'
                      CHECK (timer_status IN ('RUNNING', 'PAUSED', 'STOPPED', 'TIME_UP')),
    remaining_time    REAL    NOT NULL CHECK (remaining_time >= 0),
    health            REAL    NOT NULL DEFAULT 100 CHECK (health BETWEEN 0 AND 100),
    wrong_attempts    INTEGER NOT NULL DEFAULT 0 CHECK (wrong_attempts >= 0),
    total_play_time   REAL    NOT NULL DEFAULT 0 CHECK (total_play_time >= 0),
    score             INTEGER NOT NULL DEFAULT 0 CHECK (score >= 0),
    game_status       TEXT    NOT NULL DEFAULT 'IN_PROGRESS'
                      CHECK (game_status IN ('IN_PROGRESS', 'GAME_OVER', 'COMPLETED')),
    last_saved        TEXT    NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (player_id)        REFERENCES player(player_id) ON DELETE CASCADE,
    FOREIGN KEY (current_level_id) REFERENCES level(level_id)
);

CREATE TABLE IF NOT EXISTS puzzle (
    puzzle_id           INTEGER PRIMARY KEY AUTOINCREMENT,
    level_id            INTEGER NOT NULL,
    puzzle_key          TEXT    NOT NULL UNIQUE,
    room_name           TEXT,
    puzzle_type         TEXT    NOT NULL
                        CHECK (puzzle_type IN ('Question', 'Binary Puzzle', 'Symbol Puzzle',
                                               'Code Puzzle', 'Variable Puzzle', 'Logic Challenge')),
    question            TEXT    NOT NULL,
    choice_a            TEXT,
    choice_b            TEXT,
    correct_answer      TEXT    NOT NULL,
    puzzle_description  TEXT,
    UNIQUE (puzzle_id, level_id),
    CHECK ((choice_a IS NULL AND choice_b IS NULL) OR (choice_a IS NOT NULL AND choice_b IS NOT NULL)),
    FOREIGN KEY (level_id) REFERENCES level(level_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS puzzle_attempt (
    attempt_id        INTEGER PRIMARY KEY AUTOINCREMENT,
    progress_id       INTEGER NOT NULL,
    puzzle_id         INTEGER NOT NULL,
    submitted_answer  TEXT    NOT NULL,
    is_correct        INTEGER NOT NULL CHECK (is_correct IN (0, 1)),
    attempt_time      TEXT    NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (progress_id) REFERENCES player_progress(progress_id) ON DELETE CASCADE,
    FOREIGN KEY (puzzle_id)   REFERENCES puzzle(puzzle_id)            ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS clue (
    clue_id           INTEGER PRIMARY KEY AUTOINCREMENT,
    level_id          INTEGER NOT NULL,
    puzzle_id         INTEGER NOT NULL,
    clue_key          TEXT    NOT NULL UNIQUE,
    clue_type         TEXT    NOT NULL
                      CHECK (clue_type IN ('Hidden Clue', 'Key', 'Item', 'Access Code', 'Hint')),
    clue_description  TEXT    NOT NULL,
    FOREIGN KEY (level_id)            REFERENCES level(level_id)                ON DELETE CASCADE,
    FOREIGN KEY (puzzle_id, level_id) REFERENCES puzzle(puzzle_id, level_id)    ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS collected_clue (
    progress_id     INTEGER NOT NULL,
    clue_id         INTEGER NOT NULL,
    date_collected  TEXT    NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (progress_id, clue_id),
    FOREIGN KEY (progress_id) REFERENCES player_progress(progress_id) ON DELETE CASCADE,
    FOREIGN KEY (clue_id)     REFERENCES clue(clue_id)                ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS keycard (
    keycard_id           INTEGER PRIMARY KEY AUTOINCREMENT,
    level_id             INTEGER NOT NULL,
    puzzle_id            INTEGER NOT NULL UNIQUE,
    item_key             TEXT    NOT NULL UNIQUE,
    keycard_name         TEXT    NOT NULL UNIQUE,
    keycard_description  TEXT,
    FOREIGN KEY (level_id)            REFERENCES level(level_id)             ON DELETE CASCADE,
    FOREIGN KEY (puzzle_id, level_id) REFERENCES puzzle(puzzle_id, level_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS collected_keycard (
    progress_id     INTEGER NOT NULL,
    keycard_id      INTEGER NOT NULL,
    access_code     TEXT    NOT NULL,
    date_collected  TEXT    NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (progress_id, keycard_id),
    FOREIGN KEY (progress_id) REFERENCES player_progress(progress_id) ON DELETE CASCADE,
    FOREIGN KEY (keycard_id)  REFERENCES keycard(keycard_id)          ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS level_completion (
    progress_id     INTEGER NOT NULL,
    level_id        INTEGER NOT NULL,
    remaining_time  REAL    NOT NULL CHECK (remaining_time >= 0),
    level_score     INTEGER NOT NULL DEFAULT 0 CHECK (level_score >= 0),
    date_completed  TEXT    NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (progress_id, level_id),
    FOREIGN KEY (progress_id) REFERENCES player_progress(progress_id) ON DELETE CASCADE,
    FOREIGN KEY (level_id)    REFERENCES level(level_id)              ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS level_checkpoint (
    progress_id        INTEGER NOT NULL,
    level_id           INTEGER NOT NULL,
    checkpoint_label   TEXT,
    is_current         INTEGER NOT NULL DEFAULT 0 CHECK (is_current IN (0, 1)),
    rooms_cleared      INTEGER CHECK (rooms_cleared >= 0),
    time_remaining     REAL,
    checkpoint_number  INTEGER,
    held_item          TEXT,
    flashlight_on      INTEGER CHECK (flashlight_on IN (0, 1)),
    objective          TEXT,
    PRIMARY KEY (progress_id, level_id),
    FOREIGN KEY (progress_id) REFERENCES player_progress(progress_id) ON DELETE CASCADE,
    FOREIGN KEY (level_id)    REFERENCES level(level_id)              ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS checkpoint_item (
    progress_id  INTEGER NOT NULL,
    level_id     INTEGER NOT NULL,
    item_type    TEXT    NOT NULL
                 CHECK (item_type IN ('inventory', 'collected', 'flag', 'number', 'door', 'light')),
    item_name    TEXT    NOT NULL,
    value_1      INTEGER,
    value_2      INTEGER,
    sort_order   INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (progress_id, level_id, item_type, item_name),
    FOREIGN KEY (progress_id, level_id) REFERENCES level_checkpoint(progress_id, level_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_progress_level        ON player_progress(current_level_id);
CREATE INDEX IF NOT EXISTS idx_puzzle_level          ON puzzle(level_id);
CREATE INDEX IF NOT EXISTS idx_attempt_progress      ON puzzle_attempt(progress_id);
CREATE INDEX IF NOT EXISTS idx_attempt_puzzle        ON puzzle_attempt(puzzle_id);
CREATE INDEX IF NOT EXISTS idx_clue_level            ON clue(level_id);
CREATE INDEX IF NOT EXISTS idx_clue_puzzle           ON clue(puzzle_id, level_id);
CREATE INDEX IF NOT EXISTS idx_collected_clue_clue   ON collected_clue(clue_id);
CREATE INDEX IF NOT EXISTS idx_keycard_level         ON keycard(level_id);
CREATE INDEX IF NOT EXISTS idx_keycard_puzzle_level  ON keycard(puzzle_id, level_id);
CREATE INDEX IF NOT EXISTS idx_collected_keycard_kc  ON collected_keycard(keycard_id);
CREATE INDEX IF NOT EXISTS idx_completion_level      ON level_completion(level_id);
CREATE INDEX IF NOT EXISTS idx_checkpoint_level      ON level_checkpoint(level_id);

INSERT OR IGNORE INTO level (level_number, level_name, time_limit, description) VALUES
 (1, 'The Holding Rooms', 300,
  'Three sealed rooms stand between you and the exit. Each room has two doors (A and B) and a central lectern with a programming question. The correct door leads on; the wrong door leads to a burning trap room.'),
 (2, 'The Family House', 1800,
  'Four interconnected rooms: Living Room, Bedroom, Kitchen and Study Room. Every door is locked and each room''s puzzles give the key to the next. The Study Room computer is a cumulative puzzle that uses clues from all earlier rooms.'),
 (3, 'The Server Room', 2400,
  'The facility is in lockdown. Solve the challenges in the Maintenance, Security and Control rooms to collect the RED, BLUE and GREEN keycards, then insert them into the central terminal and crack the exit code.');

INSERT OR IGNORE INTO puzzle (level_id, puzzle_key, room_name, puzzle_type, question, choice_a, choice_b, correct_answer, puzzle_description) VALUES
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q01', NULL, 'Question', 'What is an Algorithm?', 'A step-by-step procedure for solving a problem.', 'A device used to run programs.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q02', NULL, 'Question', 'What happens when a loop has no stopping condition?', 'It may run continuously.', 'It automatically stops.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q03', NULL, 'Question', 'Which operator means NOT EQUAL?', '==', '!=', 'B', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q04', NULL, 'Question', 'What is the purpose of a Function?', 'To organize reusable blocks of code.', 'To permanently delete data.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q05', NULL, 'Question', 'What is the result of 10 + 5 * 2?', '30', '20', 'B', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q06', NULL, 'Question', 'What is an Infinite Loop?', 'A loop that continues without stopping.', 'A loop that runs only once.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q07', NULL, 'Question', 'Which is an example of a Boolean value?', 'True', '25', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q08', NULL, 'Question', 'What is the purpose of a Conditional Statement?', 'To make decisions based on conditions.', 'To store multiple pieces of information.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q09', NULL, 'Question', 'What is the output of this code? x = 8; x = x + 2; print(x)', '8', '10', 'B', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q10', NULL, 'Question', 'What is the purpose of Debugging?', 'To find and fix errors in a program.', 'To make a computer run without electricity.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q11', NULL, 'Question', 'Which data structure stores an ordered collection of elements?', 'Array', 'Boolean', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q12', NULL, 'Question', 'What does a Compiler do?', 'Translates source code into executable form.', 'Deletes unnecessary files.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q13', NULL, 'Question', 'What is the result of 20 % 6?', '2', '3', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q14', NULL, 'Question', 'What is the purpose of a Comment in programming?', 'To explain or describe the code.', 'To execute additional commands.', 'A', 'Two-door lectern question.'),
 ((SELECT level_id FROM level WHERE level_number = 1), 'Level 1/q15', NULL, 'Question', 'What is a Syntax Error?', 'When code violates the writing rules of a programming language.', 'When a program automatically fixes itself.', 'A', 'Two-door lectern question.');

INSERT OR IGNORE INTO puzzle (level_id, puzzle_key, room_name, puzzle_type, question, correct_answer, puzzle_description) VALUES
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/living_photo', 'Living Room', 'Binary Puzzle',
  'Find the four torn photo pieces and assemble them. Two pieces have binary on the back: clock = 1000 and channel = 0011.',
  'clock = 8, channel = 3', 'Photo assembled; the binary values decode to 8 and 3.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/living_starlamp', 'Living Room', 'Logic Challenge',
  'Make the Living Room match the restored photo: set the wall clock, put the TV on the right channel, then switch on the star lamp.',
  'Clock 8:00, TV channel 3', 'Matching the photo makes the star lamp throw its shadow on the bookshelf.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/living_star', 'Living Room', 'Logic Challenge',
  'Place the Star Figurine in the slot the star shadow points to.',
  'Star Figurine', 'The star box opens and the blue book releases the Living Room Key.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/living_exit', 'Living Room', 'Logic Challenge',
  'Unlock the Living Room exit door.',
  'Living Room Key', 'Opens the way to the Bedroom.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/bedroom_uv', 'Bedroom', 'Symbol Puzzle',
  'Read the diary (a small program) and find what only shows under UV light.',
  'MOON', 'The UV flashlight reveals a moon on the mirror.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/bedroom_moonbox', 'Bedroom', 'Symbol Puzzle',
  'Open the directional lock of the moon box under the bed. The music box dancer shows the arrows.',
  'UP, RIGHT, RIGHT, DOWN', 'The note inside says: lights off, look up.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/bedroom_stars', 'Bedroom', 'Logic Challenge',
  'Switch off the bedroom light and count the glowing stars on the ceiling.',
  '5', 'stars = 5 is used again by the jewellery box and the Study Room computer.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/bedroom_exit', 'Bedroom', 'Code Puzzle',
  'open(drawer[stars - 1]); arrays start at 0. Which jewellery box drawer holds the key?',
  'drawer[4] (drawer 5)', 'Drawer 5 holds the Bedroom Key.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/kitchen_recipe', 'Kitchen', 'Code Puzzle',
  'Complete Grandma''s recipe function: Add(____);  // the one that comes in a carton of 6',
  'eggs', 'Completing the recipe unlocks the oven timer keypad.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/kitchen_oven', 'Kitchen', 'Variable Puzzle',
  'oven.Set(flour, sugar, eggs, milk). The labels under the ingredients are variables (e.g. milk = sugar * 2). Enter the four values.',
  '4162', 'Flour 4, Sugar 1, Eggs 6, Milk 2. The oven opens and reveals the Kitchen Key.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/kitchen_exit', 'Kitchen', 'Logic Challenge',
  'Unlock the Kitchen exit door.',
  'Kitchen Key', 'Opens the way to the Study Room.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/study_checks', 'Study Room', 'Variable Puzzle',
  'int channel = ?;  then  if (stars > 4) unlock();  stars = ?',
  '3, 5', 'Cumulative: channel from the Living Room, stars from the Bedroom.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/study_picture', 'Study Room', 'Symbol Puzzle',
  'Decode the picture note with the whiteboard picture key: PASSWORD = [heart] + [calendar],  [heart] = [paw].',
  'dog name + best year', 'heart = who I love = the dog, calendar = my best year.'),
 ((SELECT level_id FROM level WHERE level_number = 2), 'Level 2/study_login', 'Study Room', 'Code Puzzle',
  'string password = love + year;  Enter the password.',
  'BYTE2019', 'Final cumulative puzzle of Level 2 (spaces and capital letters are ignored).');

INSERT OR IGNORE INTO puzzle (level_id, puzzle_key, room_name, puzzle_type, question, correct_answer, puzzle_description) VALUES
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/m1', 'Maintenance Room', 'Variable Puzzle',
  'Ramos''s laptop: the lockers are variables (locker1 = 8, locker2 = 2, locker3 = 5). Run the code top to bottom. What does locker2 hold at the end?',
  '8', 'Copying a value does not link the two lockers.'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/m2', 'Maintenance Room', 'Variable Puzzle',
  'The red locker only opens for a name C# will accept. Tags: 2fast, my var, class, fanSpeed.',
  'fanSpeed', 'Valid C# variable names: no starting digit, no spaces, not a keyword. fanSpeed = 6.'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/m3', 'Maintenance Room', 'Variable Puzzle',
  'Fuse box: follow the swap  temp = x;  x = y;  y = temp;  then press TEST.',
  'x = 9, y = 2', 'Variable swap with a temp socket.'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/gen', 'Maintenance Room', 'Logic Challenge',
  'Start the backup generator. Code = laptop answer, then the fanSpeed value, then the fuse now in x.',
  '869', 'Rewards the RED keycard (m = 9).'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/s1', 'Security Room', 'Logic Challenge',
  'Alarm rule: motion > 3 && door == OPEN. Which camera set off the alarm?',
  '4', 'CAM 4 (motion 5, door OPEN).'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/s2', 'Security Room', 'Symbol Puzzle',
  'Shift log lines are missing their operator: 17 ? 5 = 2,  9 ? 3 = 27,  8 ? 2 = 4.',
  '%*/', 'Operators: modulo, multiply, divide.'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/s3', 'Security Room', 'Variable Puzzle',
  'if ((isGuard || hasBadge) && !alarmOn) unlockSafe();   PIN = cam * 10 + (17 % 5)',
  '42', 'Rewards the BLUE keycard (s = 4).'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/c2', 'Control Room', 'Logic Challenge',
  'Monitor 2 counts the server racks whose status light is GREEN. How many servers are online?',
  '5', 'online = 5 (racks 1, 3, 4, 6, 7).'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/fan', 'Control Room', 'Logic Challenge',
  'Monitor 1 is an if / else-if chain. The gauge reads 86 C. Set the fan dial.',
  '3', 'The first true condition (temp > 80) sets fan = 3.'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/c3', 'Control Room', 'Logic Challenge',
  'Follow the REBOOT PROTOCOL: pull the emergency levers in the right order.',
  'COOLING, BACKUP, REBOOT', 'Rewards the GREEN keycard (c = 5).'),
 ((SELECT level_id FROM level WHERE level_number = 3), 'Level 3/term', 'Server Room (Central Terminal)', 'Code Puzzle',
  'm = 9; s = 4; c = 5;  if (m > s && c != 0) exitCode = m * 10 + s; else exitCode = 0;  Enter exitCode.',
  '94', 'Needs all three keycards inserted. Unlocks the final exit.');

INSERT OR IGNORE INTO keycard (level_id, puzzle_id, item_key, keycard_name, keycard_description)
SELECT p.level_id, p.puzzle_id, k.item_key, k.keycard_name, k.keycard_description
FROM (SELECT 'Level 3/gen' AS puzzle_key, 'keycard_red' AS item_key, 'Red Keycard (MAINT)' AS keycard_name,
             'Printed by the Maintenance Room generator. Carries m for the central terminal.' AS keycard_description
      UNION ALL SELECT 'Level 3/s3', 'keycard_blue', 'Blue Keycard (SEC)',
             'Found in the Security Room safe. Carries s for the central terminal.'
      UNION ALL SELECT 'Level 3/c3', 'keycard_green', 'Green Keycard (CTRL)',
             'Printed by the Control Room after the reboot. Carries c for the central terminal.') AS k
JOIN puzzle p ON p.puzzle_key = k.puzzle_key;

INSERT OR IGNORE INTO clue (level_id, puzzle_id, clue_key, clue_type, clue_description)
SELECT p.level_id, p.puzzle_id, c.clue_key, c.clue_type, c.clue_description
FROM (
  SELECT 'Level 2/living_photo' AS puzzle_key, 'photo_1' AS clue_key, 'Hidden Clue' AS clue_type,
         'Torn Photo Piece (1/4). On the back: clock = 1000 (binary).' AS clue_description
  UNION ALL SELECT 'Level 2/living_photo', 'photo_2', 'Hidden Clue', 'Torn Photo Piece (2/4). On the back: channel = 0011 (binary).'
  UNION ALL SELECT 'Level 2/living_photo', 'photo_3', 'Hidden Clue', 'Torn Photo Piece (3/4), rolled up.'
  UNION ALL SELECT 'Level 2/living_photo', 'photo_4', 'Hidden Clue', 'Torn Photo Piece (4/4), a torn corner.'
  UNION ALL SELECT 'Level 2/living_starlamp', 'family_photo', 'Hidden Clue', 'The restored photo: the wall clock shows 8:00 and the TV glows blue on Channel 3.'
  UNION ALL SELECT 'Level 2/living_starlamp', 'binary_chart', 'Hidden Clue', 'Poster by the TV with the binary place values 8 4 2 1.'
  UNION ALL SELECT 'Level 2/living_starlamp', 'batteries', 'Item', 'AA Batteries for the TV remote.'
  UNION ALL SELECT 'Level 2/living_star', 'star_figurine', 'Item', 'Star Figurine: a small brass star whose base fits a slot.'
  UNION ALL SELECT 'Level 2/living_exit', 'living_key', 'Key', 'Living Room Key, released from the blue book.'
  UNION ALL SELECT 'Level 2/living_photo', 'hint_living_photo_1', 'Hint', 'Search every drawer and cabinet in this room for the four torn photo pieces.'
  UNION ALL SELECT 'Level 2/living_photo', 'hint_living_photo_2', 'Hint', 'Once you have all four pieces, open the inventory (V) and press Assemble. The finished photo shows how this room looked that evening.'
  UNION ALL SELECT 'Level 2/living_starlamp', 'hint_living_starlamp_1', 'Hint', 'clock = 1000 and channel = 0011 are binary. The poster by the TV shows the place values 8 4 2 1: add up the places that have a 1. The TV remote needs batteries hidden in this room.'
  UNION ALL SELECT 'Level 2/living_starlamp', 'hint_living_starlamp_2', 'Hint', 'Clock = 8:00 and TV = channel 3, then switch on the star lamp.'
  UNION ALL SELECT 'Level 2/living_star', 'hint_living_star_1', 'Hint', 'The star''s shadow points at a slot on the bookshelf. Something star-shaped is hidden in this room.'
  UNION ALL SELECT 'Level 2/living_star', 'hint_living_star_2', 'Hint', 'Hold the star figurine and use it on the slot the shadow points to.'
  UNION ALL SELECT 'Level 2/living_exit', 'hint_living_exit_1', 'Hint', 'The blue book on the shelf has come loose. Take what it was holding and unlock the Living Room exit.'
  UNION ALL SELECT 'Level 2/bedroom_uv', 'diary', 'Hidden Clue', 'The diary on the bed, written like a small program.'
  UNION ALL SELECT 'Level 2/bedroom_uv', 'uv_flashlight', 'Item', 'UV Flashlight: invisible ink glows under its beam (G switches it on).'
  UNION ALL SELECT 'Level 2/bedroom_moonbox', 'music_box', 'Hidden Clue', 'The music box dancer: every move is an arrow (UP, RIGHT, RIGHT, DOWN).'
  UNION ALL SELECT 'Level 2/bedroom_stars', 'moonbox_note', 'Hidden Clue', 'Note inside the moon box: lights off, look up.'
  UNION ALL SELECT 'Level 2/bedroom_exit', 'bedroom_key', 'Key', 'Bedroom Key, found in drawer 5 of the jewellery box.'
  UNION ALL SELECT 'Level 2/bedroom_uv', 'hint_bedroom_uv_1', 'Hint', 'Read the diary on the bed: it is a little program. Find the UV flashlight hidden in this room. Something here only shows under UV light.'
  UNION ALL SELECT 'Level 2/bedroom_uv', 'hint_bedroom_uv_2', 'Hint', 'Hold the UV flashlight (G switches it on) and shine it on the mirror.'
  UNION ALL SELECT 'Level 2/bedroom_moonbox', 'hint_bedroom_moonbox_1', 'Hint', 'Open and wind the music box, then watch the dancer: every move is an arrow.'
  UNION ALL SELECT 'Level 2/bedroom_moonbox', 'hint_bedroom_moonbox_2', 'Hint', 'The dancer''s moves (UP, RIGHT, RIGHT, DOWN) open the moon box. Enter them on its lock in that order.'
  UNION ALL SELECT 'Level 2/bedroom_stars', 'hint_bedroom_stars_1', 'Hint', 'The note in the moon box says: lights off, look up.'
  UNION ALL SELECT 'Level 2/bedroom_stars', 'hint_bedroom_stars_2', 'Hint', 'Switch off the bedroom light and count the glowing stars on the ceiling.'
  UNION ALL SELECT 'Level 2/bedroom_exit', 'hint_bedroom_exit_1', 'Hint', 'open(drawer[stars - 1]) with arrays starting at 0: count from the bottom drawer, which is drawer[0].'
  UNION ALL SELECT 'Level 2/bedroom_exit', 'hint_bedroom_exit_2', 'Hint', 'Open that jewellery box drawer, take the key and unlock the Bedroom exit.'
  UNION ALL SELECT 'Level 2/kitchen_recipe', 'recipe_card', 'Hidden Clue', 'Grandma''s recipe card: an incomplete function, Add(____); // the one that comes in a carton of 6.'
  UNION ALL SELECT 'Level 2/kitchen_oven', 'ingredient_labels', 'Hidden Clue', 'Labels under the flour, sugar, eggs and milk. Each is a variable (e.g. milk = sugar * 2); only one is a plain number.'
  UNION ALL SELECT 'Level 2/kitchen_exit', 'kitchen_key', 'Key', 'Kitchen Key, hidden inside the oven.'
  UNION ALL SELECT 'Level 2/kitchen_recipe', 'hint_kitchen_recipe_1', 'Hint', 'Read Grandma''s recipe on the counter. One ingredient is missing: the one that comes in a carton of 6. Completing the recipe unlocks the oven timer.'
  UNION ALL SELECT 'Level 2/kitchen_oven', 'hint_kitchen_oven_1', 'Hint', 'Lift the flour, sugar, eggs and milk and read underneath. Each label is a variable. Only one label is a plain number. Start there.'
  UNION ALL SELECT 'Level 2/kitchen_oven', 'hint_kitchen_oven_2', 'Hint', 'sugar is 1. Work out milk and flour from it, then eggs = flour + milk. Type the four values in the order oven.Set(flour, sugar, eggs, milk) asks for them.'
  UNION ALL SELECT 'Level 2/kitchen_exit', 'hint_kitchen_exit_1', 'Hint', 'The oven door is open. Take what is inside and unlock the Kitchen exit.'
  UNION ALL SELECT 'Level 2/study_picture', 'picture_key', 'Hidden Clue', 'Whiteboard picture key: heart = who I love, paw = my dog, calendar = my best year.'
  UNION ALL SELECT 'Level 2/study_login', 'desk_note', 'Hidden Clue', 'Desk note: PASSWORD = [heart] + [calendar], [heart] = [paw].'
  UNION ALL SELECT 'Level 2/study_login', 'dog_collar', 'Hidden Clue', 'The dog''s collar shows its name: BYTE.'
  UNION ALL SELECT 'Level 2/study_login', 'certificate', 'Hidden Clue', 'The certificate on the wall shows the best year: 2019.'
  UNION ALL SELECT 'Level 2/study_checks', 'hint_study_checks_1', 'Hint', 'The computer first checks what you learned: the TV channel (Living Room) and the number of stars (Bedroom). Your notes (J) remember them.'
  UNION ALL SELECT 'Level 2/study_picture', 'hint_study_picture_1', 'Hint', 'The note on the desk is written in pictures. The whiteboard is the picture key: heart = who I love, paw = my dog, calendar = my best year.'
  UNION ALL SELECT 'Level 2/study_picture', 'hint_study_picture_2', 'Hint', 'Match the pictures on the picture wheel to decode the note.'
  UNION ALL SELECT 'Level 2/study_login', 'hint_study_login_1', 'Hint', 'The heart is the dog: its collar has its name. The best year is on the certificate on the wall. Type the name and the year together.'
  UNION ALL SELECT 'Level 3/m1', 'ramos_lockers', 'Hidden Clue', 'Ramos''s three grey lockers: the door is the variable NAME, the card inside is its VALUE (locker1 = 8, locker2 = 2, locker3 = 5).'
  UNION ALL SELECT 'Level 3/m2', 'tag_2fast', 'Item', 'Name tag "2fast" (2fast = 3). Not a valid C# name: starts with a digit.'
  UNION ALL SELECT 'Level 3/m2', 'tag_myvar', 'Item', 'Name tag "my var" (my var = 1). Not a valid C# name: has a space.'
  UNION ALL SELECT 'Level 3/m2', 'tag_class', 'Item', 'Name tag "class" (class = 7). Not a valid C# name: a keyword.'
  UNION ALL SELECT 'Level 3/m2', 'tag_fanspeed', 'Item', 'Name tag "fanSpeed" (fanSpeed = 6). The only valid C# name.'
  UNION ALL SELECT 'Level 3/m3', 'ramos_log', 'Hidden Clue', 'Ramos''s log page in the red locker: temp = x;  x = y;  y = temp;'
  UNION ALL SELECT 'Level 3/m3', 'fuse_2', 'Item', 'Fuse 2: a cartridge fuse stamped with a big 2.'
  UNION ALL SELECT 'Level 3/m3', 'fuse_9', 'Item', 'Fuse 9: a cartridge fuse stamped with a big 9.'
  UNION ALL SELECT 'Level 3/gen', 'generator_note', 'Hidden Clue', 'Note taped to the generator: the start code has three digits, one from each of Ramos''s jobs.'
  UNION ALL SELECT 'Level 3/gen', 'code_red_m', 'Access Code', 'RED keycard value for the terminal: MAINT: m = 9.'
  UNION ALL SELECT 'Level 3/m1', 'hint_m1_1', 'Hint', 'Read the laptop''s code one line at a time, top to bottom. Your locker values are listed under the code.'
  UNION ALL SELECT 'Level 3/m1', 'hint_m1_2', 'Hint', '''locker2 = locker1'' means: put a COPY of locker1''s number into locker2. Only the locker on the LEFT of ''='' changes.'
  UNION ALL SELECT 'Level 3/m1', 'hint_m1_3', 'Hint', 'Copying a value does not link the two lockers. Type what locker2 holds at the very end.'
  UNION ALL SELECT 'Level 3/m2', 'hint_m2_1', 'Hint', 'The pegboard title gives the rules: no starting digit, no spaces, not a C# keyword like class.'
  UNION ALL SELECT 'Level 3/m2', 'hint_m2_2', 'Hint', '2fast starts with a digit, my var has a space, class is a keyword. Hold the remaining tag and use it on the NAME TAG READER.'
  UNION ALL SELECT 'Level 3/m3', 'hint_m3_1', 'Hint', 'Read Ramos''s log page in the red locker: temp = x;  x = y;  y = temp;  Each line moves one fuse.'
  UNION ALL SELECT 'Level 3/m3', 'hint_m3_2', 'Hint', 'Take the fuse out of x and put it in temp. Then move y''s fuse into x, then the fuse in temp into y.'
  UNION ALL SELECT 'Level 3/gen', 'hint_gen_1', 'Hint', 'The start code has three digits, one from each of Ramos''s jobs. Read the note taped to the generator.'
  UNION ALL SELECT 'Level 3/gen', 'hint_gen_2', 'Hint', 'Laptop answer, then the fanSpeed value, then the fuse in socket x, typed in that order.'
  UNION ALL SELECT 'Level 3/s1', 'guard_sticky_note', 'Hidden Clue', 'Sticky note on the guard desk (the alarm rule): motion > 3 && door is OPEN.'
  UNION ALL SELECT 'Level 3/s2', 'shift_log', 'Hidden Clue', 'Last shift log printout: three lines missing their operator. % = what is left over after dividing.'
  UNION ALL SELECT 'Level 3/s3', 'pin_note', 'Hidden Clue', 'PIN note in the cabinet: PIN = cam * 10 + (17 % 5). cam = the camera that saw the alarm.'
  UNION ALL SELECT 'Level 3/s3', 'code_blue_s', 'Access Code', 'BLUE keycard value for the terminal: SEC: s = 4.'
  UNION ALL SELECT 'Level 3/s1', 'hint_s1_1', 'Hint', 'The sticky note on the guard desk is the alarm rule. Each feed shows its motion value and whether its door is OPEN or CLOSED.'
  UNION ALL SELECT 'Level 3/s1', 'hint_s1_2', 'Hint', '&& means BOTH sides must be true: motion must be above 3 AND the door must be OPEN.'
  UNION ALL SELECT 'Level 3/s2', 'hint_s2_1', 'Hint', 'Three lines on the shift log are missing their operator (+  -  *  /  %). Find the one that makes each line true.'
  UNION ALL SELECT 'Level 3/s2', 'hint_s2_2', 'Hint', '% gives what is LEFT OVER after dividing: 17 % 5 = 2 because 17 = 3 x 5 + 2.'
  UNION ALL SELECT 'Level 3/s3', 'hint_s3_1', 'Hint', '|| needs only ONE side true, ! flips true to false. So: isGuard or hasBadge ON, and alarmOn OFF.'
  UNION ALL SELECT 'Level 3/s3', 'hint_s3_2', 'Hint', 'PIN = cam * 10 + (17 % 5). cam is the number of the camera you flagged at the camera wall.'
  UNION ALL SELECT 'Level 3/c2', 'monitor_2', 'Hidden Clue', 'Monitor 2: code that counts the server racks with a GREEN status light.'
  UNION ALL SELECT 'Level 3/fan', 'monitor_1', 'Hidden Clue', 'Monitor 1: an if / else-if chain that picks the fan value from the temperature.'
  UNION ALL SELECT 'Level 3/fan', 'cooling_gauge', 'Hidden Clue', 'Gauge on the cooling unit: 86 C.'
  UNION ALL SELECT 'Level 3/c3', 'reboot_protocol', 'Hidden Clue', 'REBOOT PROTOCOL clipboard: each step has a condition. A wrong order resets the levers.'
  UNION ALL SELECT 'Level 3/c3', 'code_green_c', 'Access Code', 'GREEN keycard value for the terminal: CTRL: c = 5.'
  UNION ALL SELECT 'Level 3/c2', 'hint_c2_1', 'Hint', 'The racks are in the main server hall (the big room). Walk along them and count only the green lights.'
  UNION ALL SELECT 'Level 3/fan', 'hint_fan_1', 'Hint', 'An if / else-if chain stops at the FIRST condition that is true. Compare the gauge reading with 80 first.'
  UNION ALL SELECT 'Level 3/c3', 'hint_c3_1', 'Hint', 'Step 1 applies because the temperature is above 80. Step 2 applies because fewer than 6 servers are online. REBOOT is always last.'
  UNION ALL SELECT 'Level 3/c3', 'hint_c3_2', 'Hint', 'Pull COOLING first, then BACKUP, then REBOOT.'
  UNION ALL SELECT 'Level 3/term', 'hint_term_1', 'Hint', 'Each keycard put its value into the terminal''s code: m, s and c.'
  UNION ALL SELECT 'Level 3/term', 'hint_term_2', 'Hint', 'Check the if first: is m greater than s, and is c not zero? Both must be true.'
  UNION ALL SELECT 'Level 3/term', 'hint_term_3', 'Hint', 'The condition is true, so exitCode = m * 10 + s. Work it out with the values on the screen.'
) AS c
JOIN puzzle p ON p.puzzle_key = c.puzzle_key;
