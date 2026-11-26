using System;
using System.Collections.Generic;
using Mono.Data.Sqlite;

namespace CodeBreaker.Database
{

    public class SaveGame
    {
        public int ProgressId;
        public int PlayerId;
        public string PlayerName;
        public int LevelNumber;
        public string LevelName;
        public string CurrentRoom;
        public string TimerStatus;
        public float RemainingTime;
        public float Health;
        public int WrongAttempts;
        public float TotalPlayTime;
        public int Score;
        public string GameStatus;
        public string LastSaved;

        public string LastSavedLocal
        {
            get
            {
                DateTime utc;
                if (!DateTime.TryParse(LastSaved, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out utc))
                    return LastSaved;
                return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
        }
    }

    public class PuzzleInfo
    {
        public int PuzzleId;
        public int LevelNumber;
        public string PuzzleKey;
        public string RoomName;
        public string PuzzleType;
        public string Question;
        public string ChoiceA;
        public string ChoiceB;
        public string CorrectAnswer;
        public string Description;
    }

    public class GameDatabase : IDisposable
    {
        public const int PointsPerSecond = 10;
        public const int HintPenalty = 250;
        public const int LastLevel = 3;

        readonly SqliteConnection connection;

        public GameDatabase(string dbPath)
        {
            connection = new SqliteConnection("URI=file:" + dbPath);
            connection.Open();
            Execute("PRAGMA foreign_keys = ON;");
        }

        public void Dispose()
        {
            connection.Close();
            connection.Dispose();
        }

        public void RunScript(string sqlScript)
        {
            using (SqliteTransaction t = connection.BeginTransaction())
            {
                Execute(sqlScript);
                UpgradeSchema();
                t.Commit();
            }
        }

        void UpgradeSchema()
        {
            AddColumnIfMissing("player_progress", "wrong_attempts", "INTEGER NOT NULL DEFAULT 0 CHECK (wrong_attempts >= 0)");
            AddColumnIfMissing("player_progress", "total_play_time", "REAL NOT NULL DEFAULT 0 CHECK (total_play_time >= 0)");
        }

        void AddColumnIfMissing(string table, string column, string definition)
        {
            object found = Scalar("SELECT COUNT(*) FROM pragma_table_info('" + table + "') WHERE name = @c;", "@c", column);
            if (Convert.ToInt32(found) == 0)
                Execute("ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition + ";");
        }

        SqliteCommand Command(string sql, params object[] nameValuePairs)
        {
            SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
                cmd.Parameters.AddWithValue((string)nameValuePairs[i], nameValuePairs[i + 1] ?? DBNull.Value);
            return cmd;
        }

        int Execute(string sql, params object[] nameValuePairs)
        {
            using (SqliteCommand cmd = Command(sql, nameValuePairs))
                return cmd.ExecuteNonQuery();
        }

        object Scalar(string sql, params object[] nameValuePairs)
        {
            using (SqliteCommand cmd = Command(sql, nameValuePairs))
            {
                object result = cmd.ExecuteScalar();
                return result == DBNull.Value ? null : result;
            }
        }

        int LastInsertId() => Convert.ToInt32(Scalar("SELECT last_insert_rowid();"));

        static string Text(SqliteDataReader r, int column) => r.IsDBNull(column) ? null : r.GetString(column);

        public int GetLevelId(int levelNumber)
        {
            object id = Scalar("SELECT level_id FROM level WHERE level_number = @n;", "@n", levelNumber);
            if (id == null) throw new ArgumentException("Level " + levelNumber + " is not in the database.");
            return Convert.ToInt32(id);
        }

        public int GetTimeLimit(int levelNumber) =>
            Convert.ToInt32(Scalar("SELECT time_limit FROM level WHERE level_number = @n;", "@n", levelNumber));

        public string GetLevelName(int levelNumber) =>
            (string)Scalar("SELECT level_name FROM level WHERE level_number = @n;", "@n", levelNumber);

        public static string FirstRoom(int levelNumber)
        {
            if (levelNumber == 1) return "Room 1";
            if (levelNumber == 2) return "Living Room";
            return "Server Room";
        }

        public int GetOrCreatePlayer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) throw new ArgumentException("Player name is empty.");
            playerName = playerName.Trim();

            object id = Scalar("SELECT player_id FROM player WHERE player_name = @name;", "@name", playerName);
            if (id != null) return Convert.ToInt32(id);

            Execute("INSERT INTO player (player_name) VALUES (@name);", "@name", playerName);
            return LastInsertId();
        }

        public int StartNewGame(int playerId)
        {
            using (SqliteTransaction t = connection.BeginTransaction())
            {
                Execute("DELETE FROM player_progress WHERE player_id = @p;", "@p", playerId);
                Execute(@"INSERT INTO player_progress (player_id, current_level_id, current_room, timer_status, remaining_time)
                          VALUES (@p, @level, @room, 'RUNNING', @time);",
                        "@p", playerId, "@level", GetLevelId(1), "@room", FirstRoom(1), "@time", GetTimeLimit(1));
                int progressId = LastInsertId();
                t.Commit();
                return progressId;
            }
        }

        public SaveGame LoadSave(int playerId)
        {
            const string sql = @"
                SELECT pp.progress_id, p.player_id, p.player_name, l.level_number, l.level_name,
                       pp.current_room, pp.timer_status, pp.remaining_time, pp.health, pp.score,
                       pp.game_status, pp.last_saved, pp.wrong_attempts, pp.total_play_time
                FROM player_progress pp
                JOIN player p ON p.player_id = pp.player_id
                JOIN level  l ON l.level_id  = pp.current_level_id
                WHERE pp.player_id = @p;";

            using (SqliteCommand cmd = Command(sql, "@p", playerId))
            using (SqliteDataReader r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                return new SaveGame
                {
                    ProgressId = r.GetInt32(0),
                    PlayerId = r.GetInt32(1),
                    PlayerName = r.GetString(2),
                    LevelNumber = r.GetInt32(3),
                    LevelName = r.GetString(4),
                    CurrentRoom = r.GetString(5),
                    TimerStatus = r.GetString(6),
                    RemainingTime = (float)r.GetDouble(7),
                    Health = (float)r.GetDouble(8),
                    Score = r.GetInt32(9),
                    GameStatus = r.GetString(10),
                    LastSaved = r.GetString(11),
                    WrongAttempts = r.GetInt32(12),
                    TotalPlayTime = (float)r.GetDouble(13),
                };
            }
        }

        public void SaveProgress(int progressId, int levelNumber, string currentRoom,
                                 string timerStatus, float remainingTime, float health)
        {
            Execute(@"UPDATE player_progress
                      SET current_level_id = @level,
                          current_room     = @room,
                          timer_status     = @status,
                          remaining_time   = @time,
                          health           = @health,
                          last_saved       = datetime('now')
                      WHERE progress_id = @id;",
                    "@level", GetLevelId(levelNumber), "@room", currentRoom, "@status", timerStatus,
                    "@time", Math.Max(0f, remainingTime), "@health", Math.Max(0f, Math.Min(100f, health)),
                    "@id", progressId);
        }

        public void SetCurrentLevel(int progressId, int levelNumber)
        {
            Execute(@"UPDATE player_progress
                      SET current_level_id = @level, current_room = @room, last_saved = datetime('now')
                      WHERE progress_id = @id AND current_level_id <> @level;",
                    "@level", GetLevelId(levelNumber), "@room", FirstRoom(levelNumber), "@id", progressId);
        }

        public void SaveTimer(int progressId, string timerStatus, float remainingTime)
        {
            Execute(@"UPDATE player_progress
                      SET timer_status = @status, remaining_time = @time, last_saved = datetime('now')
                      WHERE progress_id = @id;",
                    "@status", timerStatus, "@time", Math.Max(0f, remainingTime), "@id", progressId);
        }

        public void SetInProgress(int progressId)
        {
            Execute("UPDATE player_progress SET game_status = 'IN_PROGRESS' WHERE progress_id = @id;", "@id", progressId);
        }

        public void SetGameOver(int progressId, bool timeUp)
        {
            Execute(@"UPDATE player_progress
                      SET game_status = 'GAME_OVER',
                          timer_status = CASE WHEN @timeUp = 1 THEN 'TIME_UP' ELSE 'STOPPED' END,
                          remaining_time = CASE WHEN @timeUp = 1 THEN 0 ELSE remaining_time END,
                          last_saved = datetime('now')
                      WHERE progress_id = @id;",
                    "@timeUp", timeUp ? 1 : 0, "@id", progressId);
        }

        public void SaveSlot(int progressId, int levelNumber, float health, int wrongAttempts, float totalPlayTime)
        {
            Execute(@"UPDATE player_progress
                      SET current_room     = CASE WHEN current_level_id = @level THEN current_room ELSE @room END,
                          current_level_id = @level,
                          health           = @health,
                          wrong_attempts   = @wrong,
                          total_play_time  = @play,
                          last_saved       = datetime('now')
                      WHERE progress_id = @id;",
                    "@level", GetLevelId(levelNumber), "@room", FirstRoom(levelNumber),
                    "@health", Math.Max(0f, Math.Min(100f, health)), "@wrong", Math.Max(0, wrongAttempts),
                    "@play", Math.Max(0f, totalPlayTime), "@id", progressId);
        }

        public void MarkLevelCompleted(int progressId, int levelNumber)
        {
            Execute(@"INSERT OR IGNORE INTO level_completion (progress_id, level_id, remaining_time, level_score)
                      VALUES (@id, @level, 0, 0);", "@id", progressId, "@level", GetLevelId(levelNumber));
        }

        public void DeleteSave(int playerId)
        {
            Execute("DELETE FROM player_progress WHERE player_id = @p;", "@p", playerId);
        }

        public class CheckpointItem
        {
            public string Type;
            public string Name;
            public int? Value1;
            public int? Value2;
        }

        public class Checkpoint
        {
            public int LevelNumber;
            public string Label;
            public bool IsCurrent;
            public int? RoomsCleared;
            public float? TimeRemaining;
            public int? CheckpointNumber;
            public string HeldItem;
            public bool? FlashlightOn;
            public string Objective;
            public List<CheckpointItem> Items = new List<CheckpointItem>();
        }

        public void ReplaceCheckpoints(int progressId, List<Checkpoint> checkpoints)
        {
            using (SqliteTransaction t = connection.BeginTransaction())
            {
                Execute("DELETE FROM level_checkpoint WHERE progress_id = @id;", "@id", progressId);
                foreach (Checkpoint c in checkpoints)
                {
                    int levelId = GetLevelId(c.LevelNumber);
                    Execute(@"INSERT OR REPLACE INTO level_checkpoint
                                (progress_id, level_id, checkpoint_label, is_current, rooms_cleared, time_remaining,
                                 checkpoint_number, held_item, flashlight_on, objective)
                              VALUES (@id, @level, @label, @cur, @rooms, @time, @num, @held, @light, @obj);",
                            "@id", progressId, "@level", levelId, "@label", c.Label, "@cur", c.IsCurrent ? 1 : 0,
                            "@rooms", c.RoomsCleared, "@time", c.TimeRemaining, "@num", c.CheckpointNumber,
                            "@held", c.HeldItem, "@light", c.FlashlightOn.HasValue ? (object)(c.FlashlightOn.Value ? 1 : 0) : null,
                            "@obj", c.Objective);
                    int order = 0;
                    foreach (CheckpointItem i in c.Items)
                        Execute(@"INSERT OR REPLACE INTO checkpoint_item
                                    (progress_id, level_id, item_type, item_name, value_1, value_2, sort_order)
                                  VALUES (@id, @level, @type, @name, @v1, @v2, @order);",
                                "@id", progressId, "@level", levelId, "@type", i.Type, "@name", i.Name,
                                "@v1", i.Value1, "@v2", i.Value2, "@order", order++);
                }
                t.Commit();
            }
        }

        public List<Checkpoint> LoadCheckpoints(int progressId)
        {
            var list = new List<Checkpoint>();
            var byLevel = new Dictionary<int, Checkpoint>();
            using (SqliteCommand cmd = Command(@"
                    SELECT l.level_number, lc.checkpoint_label, lc.is_current, lc.rooms_cleared, lc.time_remaining,
                           lc.checkpoint_number, lc.held_item, lc.flashlight_on, lc.objective
                    FROM level_checkpoint lc JOIN level l ON l.level_id = lc.level_id
                    WHERE lc.progress_id = @id ORDER BY l.level_number;", "@id", progressId))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var c = new Checkpoint
                    {
                        LevelNumber = r.GetInt32(0),
                        Label = Text(r, 1),
                        IsCurrent = r.GetInt32(2) == 1,
                        RoomsCleared = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                        TimeRemaining = r.IsDBNull(4) ? (float?)null : (float)r.GetDouble(4),
                        CheckpointNumber = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                        HeldItem = Text(r, 6),
                        FlashlightOn = r.IsDBNull(7) ? (bool?)null : r.GetInt32(7) == 1,
                        Objective = Text(r, 8),
                    };
                    list.Add(c);
                    byLevel[c.LevelNumber] = c;
                }

            using (SqliteCommand cmd = Command(@"
                    SELECT l.level_number, ci.item_type, ci.item_name, ci.value_1, ci.value_2
                    FROM checkpoint_item ci JOIN level l ON l.level_id = ci.level_id
                    WHERE ci.progress_id = @id ORDER BY l.level_number, ci.sort_order;", "@id", progressId))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    Checkpoint c;
                    if (!byLevel.TryGetValue(r.GetInt32(0), out c)) continue;
                    c.Items.Add(new CheckpointItem
                    {
                        Type = r.GetString(1),
                        Name = r.GetString(2),
                        Value1 = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                        Value2 = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                    });
                }
            return list;
        }

        public static int CalculateLevelScore(float remainingTime, int hintsUsed) =>
            Math.Max(0, (int)Math.Floor(remainingTime) * PointsPerSecond - hintsUsed * HintPenalty);

        public void UpdateScore(int progressId, int newScore)
        {
            Execute("UPDATE player_progress SET score = @s WHERE progress_id = @id;",
                    "@s", Math.Max(0, newScore), "@id", progressId);
        }

        public void AddScore(int progressId, int points)
        {
            Execute("UPDATE player_progress SET score = MAX(score + @pts, 0) WHERE progress_id = @id;",
                    "@pts", points, "@id", progressId);
        }

        const string PuzzleColumns = @"
            SELECT pz.puzzle_id, l.level_number, pz.puzzle_key, pz.room_name, pz.puzzle_type, pz.question,
                   pz.choice_a, pz.choice_b, pz.correct_answer, pz.puzzle_description
            FROM puzzle pz JOIN level l ON l.level_id = pz.level_id ";

        static PuzzleInfo ReadPuzzle(SqliteDataReader r) => new PuzzleInfo
        {
            PuzzleId = r.GetInt32(0),
            LevelNumber = r.GetInt32(1),
            PuzzleKey = r.GetString(2),
            RoomName = Text(r, 3),
            PuzzleType = r.GetString(4),
            Question = r.GetString(5),
            ChoiceA = Text(r, 6),
            ChoiceB = Text(r, 7),
            CorrectAnswer = r.GetString(8),
            Description = Text(r, 9),
        };

        public PuzzleInfo GetPuzzle(string puzzleKey)
        {
            using (SqliteCommand cmd = Command(PuzzleColumns + "WHERE pz.puzzle_key = @key;", "@key", puzzleKey))
            using (SqliteDataReader r = cmd.ExecuteReader())
                return r.Read() ? ReadPuzzle(r) : null;
        }

        public List<PuzzleInfo> GetPuzzlesForLevel(int levelNumber)
        {
            var list = new List<PuzzleInfo>();
            using (SqliteCommand cmd = Command(PuzzleColumns + "WHERE l.level_number = @n ORDER BY pz.puzzle_id;", "@n", levelNumber))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read()) list.Add(ReadPuzzle(r));
            return list;
        }

        public PuzzleInfo GetRandomLevel1Question(int progressId)
        {
            string sql = PuzzleColumns + @"
                WHERE l.level_number = 1
                ORDER BY (SELECT COUNT(*) FROM puzzle_attempt pa
                          WHERE pa.puzzle_id = pz.puzzle_id AND pa.progress_id = @id), RANDOM()
                LIMIT 1;";
            using (SqliteCommand cmd = Command(sql, "@id", progressId))
            using (SqliteDataReader r = cmd.ExecuteReader())
                return r.Read() ? ReadPuzzle(r) : null;
        }

        public bool CheckAnswer(string puzzleKey, string answer)
        {
            PuzzleInfo p = GetPuzzle(puzzleKey);
            return p != null && Normalize(answer) == Normalize(p.CorrectAnswer);
        }

        static string Normalize(string s) =>
            (s ?? "").Replace(" ", "").Replace("\t", "").Trim().ToUpperInvariant();

        public int RecordAttempt(int progressId, string puzzleKey, string submittedAnswer, bool isCorrect)
        {
            object puzzleId = Scalar("SELECT puzzle_id FROM puzzle WHERE puzzle_key = @key;", "@key", puzzleKey);
            if (puzzleId == null) throw new ArgumentException("Unknown puzzle: " + puzzleKey);

            Execute(@"INSERT INTO puzzle_attempt (progress_id, puzzle_id, submitted_answer, is_correct)
                      VALUES (@id, @puzzle, @answer, @ok);",
                    "@id", progressId, "@puzzle", puzzleId, "@answer", submittedAnswer ?? "", "@ok", isCorrect ? 1 : 0);
            return LastInsertId();
        }

        public List<string> GetSolvedPuzzles(int progressId, int levelNumber)
        {
            var keys = new List<string>();
            const string sql = @"
                SELECT DISTINCT pz.puzzle_key
                FROM puzzle_attempt pa
                JOIN puzzle pz ON pz.puzzle_id = pa.puzzle_id
                JOIN level  l  ON l.level_id   = pz.level_id
                WHERE pa.progress_id = @id AND pa.is_correct = 1 AND l.level_number = @n
                ORDER BY pz.puzzle_id;";
            using (SqliteCommand cmd = Command(sql, "@id", progressId, "@n", levelNumber))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read()) keys.Add(r.GetString(0));
            return keys;
        }

        public bool CollectClue(int progressId, string clueKey)
        {
            object clueId = Scalar("SELECT clue_id FROM clue WHERE clue_key = @key;", "@key", clueKey);
            if (clueId == null) throw new ArgumentException("Unknown clue: " + clueKey);
            return Execute("INSERT OR IGNORE INTO collected_clue (progress_id, clue_id) VALUES (@id, @clue);",
                           "@id", progressId, "@clue", clueId) == 1;
        }

        public List<string> GetCollectedClues(int progressId, int levelNumber)
        {
            var keys = new List<string>();
            const string sql = @"
                SELECT c.clue_key FROM collected_clue cc
                JOIN clue c  ON c.clue_id  = cc.clue_id
                JOIN level l ON l.level_id = c.level_id
                WHERE cc.progress_id = @id AND l.level_number = @n
                ORDER BY cc.date_collected, c.clue_id;";
            using (SqliteCommand cmd = Command(sql, "@id", progressId, "@n", levelNumber))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read()) keys.Add(r.GetString(0));
            return keys;
        }

        public int CountHintsUsed(int progressId, int levelNumber) => Convert.ToInt32(Scalar(@"
                SELECT COUNT(*) FROM collected_clue cc
                JOIN clue c  ON c.clue_id  = cc.clue_id
                JOIN level l ON l.level_id = c.level_id
                WHERE cc.progress_id = @id AND c.clue_type = 'Hint' AND l.level_number = @n;",
                "@id", progressId, "@n", levelNumber));

        public List<string> GetHints(string puzzleKey)
        {
            var hints = new List<string>();
            const string sql = @"
                SELECT c.clue_description FROM clue c
                JOIN puzzle pz ON pz.puzzle_id = c.puzzle_id
                WHERE pz.puzzle_key = @key AND c.clue_type = 'Hint'
                ORDER BY c.clue_id;";
            using (SqliteCommand cmd = Command(sql, "@key", puzzleKey))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read()) hints.Add(r.GetString(0));
            return hints;
        }

        public bool CollectKeycard(int progressId, string itemKey, string accessCode)
        {
            object keycardId = Scalar("SELECT keycard_id FROM keycard WHERE item_key = @key;", "@key", itemKey);
            if (keycardId == null) throw new ArgumentException("Unknown keycard: " + itemKey);
            return Execute(@"INSERT OR IGNORE INTO collected_keycard (progress_id, keycard_id, access_code)
                             VALUES (@id, @card, @code);",
                           "@id", progressId, "@card", keycardId, "@code", accessCode ?? "") == 1;
        }

        public List<string> GetCollectedKeycards(int progressId)
        {
            var keys = new List<string>();
            using (SqliteCommand cmd = Command(@"
                    SELECT k.item_key FROM collected_keycard ck
                    JOIN keycard k ON k.keycard_id = ck.keycard_id
                    WHERE ck.progress_id = @id ORDER BY k.keycard_id;", "@id", progressId))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read()) keys.Add(r.GetString(0));
            return keys;
        }

        public bool HasAllLevel3Keycards(int progressId)
        {
            int collected = Convert.ToInt32(Scalar(@"
                SELECT COUNT(*) FROM collected_keycard ck
                JOIN keycard k ON k.keycard_id = ck.keycard_id
                WHERE ck.progress_id = @id AND k.level_id = @level;",
                "@id", progressId, "@level", GetLevelId(3)));
            int total = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM keycard WHERE level_id = @level;", "@level", GetLevelId(3)));
            return total > 0 && collected == total;
        }

        public int CompleteLevel(int progressId, int levelNumber, float remainingTime, int levelScore)
        {
            using (SqliteTransaction t = connection.BeginTransaction())
            {
                object oldScore = Scalar(@"SELECT level_score FROM level_completion
                                           WHERE progress_id = @id AND level_id = @level;",
                                         "@id", progressId, "@level", GetLevelId(levelNumber));
                int previous = oldScore == null ? 0 : Convert.ToInt32(oldScore);

                Execute(@"INSERT OR REPLACE INTO level_completion (progress_id, level_id, remaining_time, level_score)
                          VALUES (@id, @level, @time, @score);",
                        "@id", progressId, "@level", GetLevelId(levelNumber),
                        "@time", Math.Max(0f, remainingTime), "@score", Math.Max(0, levelScore));
                AddScore(progressId, Math.Max(0, levelScore) - previous);

                int next = levelNumber < LastLevel ? levelNumber + 1 : 0;
                if (next > 0)
                    Execute(@"UPDATE player_progress
                              SET current_level_id = @level, current_room = @room, timer_status = 'RUNNING',
                                  remaining_time = @time, last_saved = datetime('now')
                              WHERE progress_id = @id;",
                            "@level", GetLevelId(next), "@room", FirstRoom(next), "@time", GetTimeLimit(next), "@id", progressId);
                else
                    Execute(@"UPDATE player_progress
                              SET game_status = 'COMPLETED', timer_status = 'STOPPED',
                                  remaining_time = @time, last_saved = datetime('now')
                              WHERE progress_id = @id;",
                            "@time", Math.Max(0f, remainingTime), "@id", progressId);

                t.Commit();
                return next;
            }
        }

        public List<int> GetCompletedLevels(int progressId)
        {
            var levels = new List<int>();
            using (SqliteCommand cmd = Command(@"
                    SELECT l.level_number FROM level_completion lc
                    JOIN level l ON l.level_id = lc.level_id
                    WHERE lc.progress_id = @id ORDER BY l.level_number;", "@id", progressId))
            using (SqliteDataReader r = cmd.ExecuteReader())
                while (r.Read()) levels.Add(r.GetInt32(0));
            return levels;
        }
    }
}
