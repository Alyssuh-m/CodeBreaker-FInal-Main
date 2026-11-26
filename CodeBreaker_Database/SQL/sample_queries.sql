PRAGMA foreign_keys = ON;

SELECT level_number, level_name, time_limit / 60 AS minutes FROM level ORDER BY level_number;

SELECT p.player_name, l.level_number, l.level_name, pp.current_room,
       pp.timer_status, pp.remaining_time, pp.health, pp.score, pp.game_status, pp.last_saved
FROM player p
JOIN player_progress pp ON pp.player_id = p.player_id
JOIN level l            ON l.level_id   = pp.current_level_id
WHERE p.player_name = 'Juan';

SELECT puzzle_key, question, choice_a, choice_b, correct_answer
FROM puzzle
WHERE level_id = (SELECT level_id FROM level WHERE level_number = 1)
ORDER BY RANDOM() LIMIT 1;

SELECT pz.puzzle_key, pa.submitted_answer,
       CASE pa.is_correct WHEN 1 THEN 'Correct' ELSE 'Wrong' END AS result, pa.attempt_time
FROM puzzle_attempt pa
JOIN puzzle pz          ON pz.puzzle_id   = pa.puzzle_id
JOIN player_progress pp ON pp.progress_id = pa.progress_id
JOIN player p           ON p.player_id    = pp.player_id
WHERE p.player_name = 'Juan'
ORDER BY pa.attempt_time DESC;

SELECT l.level_number,
       SUM(pa.is_correct)                 AS correct,
       COUNT(*) - SUM(pa.is_correct)      AS wrong
FROM puzzle_attempt pa
JOIN puzzle pz ON pz.puzzle_id = pa.puzzle_id
JOIN level l   ON l.level_id   = pz.level_id
WHERE pa.progress_id = (SELECT progress_id FROM player_progress
                        WHERE player_id = (SELECT player_id FROM player WHERE player_name = 'Juan'))
GROUP BY l.level_number;

SELECT c.clue_key, c.clue_type, c.clue_description, cc.date_collected
FROM collected_clue cc
JOIN clue c ON c.clue_id = cc.clue_id
WHERE cc.progress_id = 1 AND c.level_id = (SELECT level_id FROM level WHERE level_number = 2);

SELECT COUNT(*) AS hints_used
FROM collected_clue cc
JOIN clue c ON c.clue_id = cc.clue_id
WHERE cc.progress_id = 1 AND c.clue_type = 'Hint'
  AND c.level_id = (SELECT level_id FROM level WHERE level_number = 2);

SELECT k.keycard_name,
       CASE WHEN ck.keycard_id IS NULL THEN 'MISSING' ELSE 'COLLECTED' END AS status,
       ck.access_code
FROM keycard k
LEFT JOIN collected_keycard ck ON ck.keycard_id = k.keycard_id AND ck.progress_id = 1
ORDER BY k.keycard_id;

SELECT (COUNT(*) = (SELECT COUNT(*) FROM keycard
                    WHERE level_id = (SELECT level_id FROM level WHERE level_number = 3))) AS has_all_keycards
FROM collected_keycard ck
JOIN keycard k ON k.keycard_id = ck.keycard_id
WHERE ck.progress_id = 1
  AND k.level_id = (SELECT level_id FROM level WHERE level_number = 3);

SELECT DISTINCT pz.room_name, pz.puzzle_key
FROM puzzle_attempt pa
JOIN puzzle pz ON pz.puzzle_id = pa.puzzle_id
WHERE pa.progress_id = 1 AND pa.is_correct = 1
  AND pz.level_id = (SELECT level_id FROM level WHERE level_number = 3);

SELECT l.level_number, l.level_name, lc.remaining_time, lc.level_score, lc.date_completed
FROM level_completion lc
JOIN level l ON l.level_id = lc.level_id
WHERE lc.progress_id = 1
ORDER BY l.level_number;

SELECT clue_key, clue_description
FROM clue
WHERE clue_type = 'Hint'
  AND puzzle_id = (SELECT puzzle_id FROM puzzle WHERE puzzle_key = 'Level 3/term')
ORDER BY clue_id;

UPDATE player_progress
SET current_level_id = (SELECT level_id FROM level WHERE level_number = 3),
    current_room     = 'Control Room',
    timer_status     = 'PAUSED',
    remaining_time   = 1480.5,
    health           = 75,
    last_saved       = datetime('now')
WHERE progress_id = 1;

UPDATE player_progress SET score = score + 1480 WHERE progress_id = 1;

UPDATE player_progress SET score = MAX(score - 250, 0) WHERE progress_id = 1;

UPDATE player_progress
SET game_status = 'GAME_OVER', timer_status = 'TIME_UP', remaining_time = 0,
    last_saved = datetime('now')
WHERE progress_id = 2;

UPDATE player SET player_name = 'Juan D.' WHERE player_name = 'Juan';

DELETE FROM player_progress WHERE progress_id = 2;

DELETE FROM collected_keycard WHERE progress_id = 1
  AND keycard_id = (SELECT keycard_id FROM keycard WHERE item_key = 'keycard_blue');

DELETE FROM player WHERE player_name = 'Juan D.';
