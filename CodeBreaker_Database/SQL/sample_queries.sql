-- =====================================================================
--  CODE BREAKER - SAMPLE SELECT / UPDATE / DELETE STATEMENTS
--
--  WHERE THIS RUNS
--   DB Browser for SQLite > Execute SQL, on a test database that already
--   has codebreaker_database.sql + sample_data.sql. Run one query at a time
--   (select it, then press Ctrl+Enter / "Execute current line").
--   The same queries are used inside GameDatabase.cs with @parameters.
-- =====================================================================
PRAGMA foreign_keys = ON;

-- ===================================================================== SELECT
-- S1. All levels with their time limit in minutes
SELECT level_number, level_name, time_limit / 60 AS minutes FROM level ORDER BY level_number;

-- S2. Load Saved Games screen: a player's save (level, room, timer, score)
SELECT p.player_name, l.level_number, l.level_name, pp.current_room,
       pp.timer_status, pp.remaining_time, pp.health, pp.score, pp.game_status, pp.last_saved
FROM player p
JOIN player_progress pp ON pp.player_id = p.player_id
JOIN level l            ON l.level_id   = pp.current_level_id
WHERE p.player_name = 'Juan';

-- S3. Level 1: pick a lectern question (two-door) at random
SELECT puzzle_key, question, choice_a, choice_b, correct_answer
FROM puzzle
WHERE level_id = (SELECT level_id FROM level WHERE level_number = 1)
ORDER BY RANDOM() LIMIT 1;

-- S4. A player's puzzle attempts, newest first
SELECT pz.puzzle_key, pa.submitted_answer,
       CASE pa.is_correct WHEN 1 THEN 'Correct' ELSE 'Wrong' END AS result, pa.attempt_time
FROM puzzle_attempt pa
JOIN puzzle pz          ON pz.puzzle_id   = pa.puzzle_id
JOIN player_progress pp ON pp.progress_id = pa.progress_id
JOIN player p           ON p.player_id    = pp.player_id
WHERE p.player_name = 'Juan'
ORDER BY pa.attempt_time DESC;

-- S5. Correct / wrong attempts per level
SELECT l.level_number,
       SUM(pa.is_correct)                 AS correct,
       COUNT(*) - SUM(pa.is_correct)      AS wrong
FROM puzzle_attempt pa
JOIN puzzle pz ON pz.puzzle_id = pa.puzzle_id
JOIN level l   ON l.level_id   = pz.level_id
WHERE pa.progress_id = (SELECT progress_id FROM player_progress
                        WHERE player_id = (SELECT player_id FROM player WHERE player_name = 'Juan'))
GROUP BY l.level_number;

-- S6. Level 2: clues collected in the save
SELECT c.clue_key, c.clue_type, c.clue_description, cc.date_collected
FROM collected_clue cc
JOIN clue c ON c.clue_id = cc.clue_id
WHERE cc.progress_id = 1 AND c.level_id = (SELECT level_id FROM level WHERE level_number = 2);

-- S7. Hints used in a level (each hint costs 250 points in Level 2)
SELECT COUNT(*) AS hints_used
FROM collected_clue cc
JOIN clue c ON c.clue_id = cc.clue_id
WHERE cc.progress_id = 1 AND c.clue_type = 'Hint'
  AND c.level_id = (SELECT level_id FROM level WHERE level_number = 2);

-- S8. Level 3: which keycards are collected / missing
SELECT k.keycard_name,
       CASE WHEN ck.keycard_id IS NULL THEN 'MISSING' ELSE 'COLLECTED' END AS status,
       ck.access_code
FROM keycard k
LEFT JOIN collected_keycard ck ON ck.keycard_id = k.keycard_id AND ck.progress_id = 1
ORDER BY k.keycard_id;

-- S9. Level 3: has the player collected ALL three keycards? (1 = yes, 0 = no)
SELECT (COUNT(*) = (SELECT COUNT(*) FROM keycard
                    WHERE level_id = (SELECT level_id FROM level WHERE level_number = 3))) AS has_all_keycards
FROM collected_keycard ck
JOIN keycard k ON k.keycard_id = ck.keycard_id
WHERE ck.progress_id = 1
  AND k.level_id = (SELECT level_id FROM level WHERE level_number = 3);

-- S10. Level 3: side-room challenges completed (solved at least once)
SELECT DISTINCT pz.room_name, pz.puzzle_key
FROM puzzle_attempt pa
JOIN puzzle pz ON pz.puzzle_id = pa.puzzle_id
WHERE pa.progress_id = 1 AND pa.is_correct = 1
  AND pz.level_id = (SELECT level_id FROM level WHERE level_number = 3);

-- S11. Completed levels with time left and points earned
SELECT l.level_number, l.level_name, lc.remaining_time, lc.level_score, lc.date_completed
FROM level_completion lc
JOIN level l ON l.level_id = lc.level_id
WHERE lc.progress_id = 1
ORDER BY l.level_number;

-- S12. Hints of a puzzle (shown by the hint/journal screen)
SELECT clue_key, clue_description
FROM clue
WHERE clue_type = 'Hint'
  AND puzzle_id = (SELECT puzzle_id FROM puzzle WHERE puzzle_key = 'Level 3/term')
ORDER BY clue_id;

-- ===================================================================== UPDATE
-- U1. Save progress (current level, room, timer) - the auto-save
UPDATE player_progress
SET current_level_id = (SELECT level_id FROM level WHERE level_number = 3),
    current_room     = 'Control Room',
    timer_status     = 'PAUSED',
    remaining_time   = 1480.5,
    health           = 75,
    last_saved       = datetime('now')
WHERE progress_id = 1;

-- U2. Add points to the score (e.g. level finished: seconds left x 10)
UPDATE player_progress SET score = score + 1480 WHERE progress_id = 1;

-- U3. Take points away for a hint, never below 0
UPDATE player_progress SET score = MAX(score - 250, 0) WHERE progress_id = 1;

-- U4. Game over (trap room / health 0 / time up)
UPDATE player_progress
SET game_status = 'GAME_OVER', timer_status = 'TIME_UP', remaining_time = 0,
    last_saved = datetime('now')
WHERE progress_id = 2;

-- U5. Rename a player
UPDATE player SET player_name = 'Juan D.' WHERE player_name = 'Juan';

-- ===================================================================== DELETE
-- D1. "Start New Game": delete the old save. ON DELETE CASCADE also deletes
--     its attempts, collected clues, collected keycards and completed levels.
DELETE FROM player_progress WHERE progress_id = 2;

-- D2. Remove one keycard from a save (e.g. undo for testing)
DELETE FROM collected_keycard WHERE progress_id = 1
  AND keycard_id = (SELECT keycard_id FROM keycard WHERE item_key = 'keycard_blue');

-- D3. Delete a player completely (their save and everything in it goes too)
DELETE FROM player WHERE player_name = 'Juan D.';
