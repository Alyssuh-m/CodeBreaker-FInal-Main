PRAGMA foreign_keys = ON;

INSERT INTO player (player_name, date_created) VALUES
 ('Juan',  '2026-11-20 09:15:00'),
 ('Maria', '2026-11-21 14:02:00');

INSERT INTO player_progress (player_id, current_level_id, current_room, timer_status, remaining_time, health, score, game_status, last_saved) VALUES
 ((SELECT player_id FROM player WHERE player_name = 'Juan'),
  (SELECT level_id FROM level WHERE level_number = 3),
  'Control Room', 'PAUSED', 1520, 75, 12870, 'IN_PROGRESS', '2026-11-22 10:41:30'),
 ((SELECT player_id FROM player WHERE player_name = 'Maria'),
  (SELECT level_id FROM level WHERE level_number = 1),
  'Room 2', 'TIME_UP', 0, 50, 0, 'GAME_OVER', '2026-11-21 14:09:12');

INSERT INTO puzzle_attempt (progress_id, puzzle_id, submitted_answer, is_correct, attempt_time)
SELECT pp.progress_id, p.puzzle_id, a.answer, a.ok, a.at
FROM (SELECT 'Level 1/q05' AS k, 'A' AS answer, 0 AS ok, '2026-11-20 09:16:40' AS at
      UNION ALL SELECT 'Level 1/q03', 'B', 1, '2026-11-20 09:17:25'
      UNION ALL SELECT 'Level 1/q09', 'B', 1, '2026-11-20 09:18:10'
      UNION ALL SELECT 'Level 1/q13', 'A', 1, '2026-11-20 09:19:02'
      UNION ALL SELECT 'Level 2/kitchen_oven', '4126', 0, '2026-11-21 19:30:15'
      UNION ALL SELECT 'Level 2/kitchen_oven', '4162', 1, '2026-11-21 19:31:00'
      UNION ALL SELECT 'Level 2/study_checks', '3, 5', 1, '2026-11-21 19:40:22'
      UNION ALL SELECT 'Level 2/study_login', 'BYTE2019', 1, '2026-11-21 19:44:05'
      UNION ALL SELECT 'Level 3/m2', 'class', 0, '2026-11-22 10:05:00'
      UNION ALL SELECT 'Level 3/m2', 'fanSpeed', 1, '2026-11-22 10:05:40'
      UNION ALL SELECT 'Level 3/gen', '869', 1, '2026-11-22 10:15:10'
      UNION ALL SELECT 'Level 3/s3', '42', 1, '2026-11-22 10:32:50') AS a
JOIN puzzle p ON p.puzzle_key = a.k
JOIN player_progress pp ON pp.player_id = (SELECT player_id FROM player WHERE player_name = 'Juan');

INSERT INTO collected_clue (progress_id, clue_id, date_collected)
SELECT pp.progress_id, c.clue_id, '2026-11-21 19:20:00'
FROM clue c
JOIN player_progress pp ON pp.player_id = (SELECT player_id FROM player WHERE player_name = 'Juan')
WHERE c.clue_key IN ('photo_1', 'photo_2', 'photo_3', 'photo_4', 'family_photo', 'living_key',
                     'uv_flashlight', 'bedroom_key', 'kitchen_key',
                     'hint_kitchen_oven_1',
                     'tag_fanspeed', 'fuse_2', 'fuse_9', 'code_red_m', 'code_blue_s');

INSERT INTO collected_keycard (progress_id, keycard_id, access_code, date_collected)
SELECT pp.progress_id, k.keycard_id, x.code, x.at
FROM (SELECT 'keycard_red' AS item, 'm = 9' AS code, '2026-11-22 10:15:30' AS at
      UNION ALL SELECT 'keycard_blue', 's = 4', '2026-11-22 10:33:05') AS x
JOIN keycard k ON k.item_key = x.item
JOIN player_progress pp ON pp.player_id = (SELECT player_id FROM player WHERE player_name = 'Juan');

INSERT INTO level_completion (progress_id, level_id, remaining_time, level_score, date_completed)
SELECT pp.progress_id, l.level_id, x.rem, x.pts, x.at
FROM (SELECT 1 AS n, 152 AS rem, 1520 AS pts, '2026-11-20 09:19:05' AS at
      UNION ALL SELECT 2, 1160, 11350, '2026-11-21 19:44:10') AS x
JOIN level l ON l.level_number = x.n
JOIN player_progress pp ON pp.player_id = (SELECT player_id FROM player WHERE player_name = 'Juan');

INSERT INTO puzzle_attempt (progress_id, puzzle_id, submitted_answer, is_correct, attempt_time)
SELECT pp.progress_id, p.puzzle_id, a.answer, a.ok, a.at
FROM (SELECT 'Level 1/q01' AS k, 'A' AS answer, 1 AS ok, '2026-11-21 14:03:30' AS at
      UNION ALL SELECT 'Level 1/q07', 'B', 0, '2026-11-21 14:06:00'
      UNION ALL SELECT 'Level 1/q12', 'B', 0, '2026-11-21 14:08:20') AS a
JOIN puzzle p ON p.puzzle_key = a.k
JOIN player_progress pp ON pp.player_id = (SELECT player_id FROM player WHERE player_name = 'Maria');
