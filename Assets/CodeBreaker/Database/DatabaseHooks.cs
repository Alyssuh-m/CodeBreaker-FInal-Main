using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeBreaker.Database
{
    public static class DatabaseHooks
    {
        static bool Ready => DatabaseManager.IsReady;
        static GameDatabase Db => DatabaseManager.Instance.Db;
        static int ProgressId => DatabaseManager.Instance.EnsureProgress();

        static void Safe(string what, Action action)
        {
            if (!Ready) return;
            try { action(); }
            catch (Exception e) { Debug.LogWarning("[Database] " + what + " not saved: " + e.Message); }
        }

        public static void NewGame() => Safe("New game", () => DatabaseManager.Instance.StartNewGame());

        public static void LevelStarted(string sceneName, float health)
        {
            int level = DatabaseManager.LevelNumberOf(sceneName);
            if (level == 0) return;
            Safe("Level start", () =>
            {
                int id = ProgressId;
                LevelTimer timer = UnityEngine.Object.FindAnyObjectByType<LevelTimer>();
                float remaining = timer != null && timer.CurrentLevel == sceneName ? timer.RemainingTime : Db.GetTimeLimit(level);
                Db.SetInProgress(id);
                Db.SaveProgress(id, level, GameDatabase.FirstRoom(level), "RUNNING", remaining, health);
            });
        }

        public static void Level1DoorChosen(int questionIndex, string chosenLetter, bool correct, string currentRoom)
        {
            if (questionIndex < 0) return;
            Safe("Level 1 door", () =>
            {
                int id = ProgressId;
                Db.RecordAttempt(id, "Level 1/q" + (questionIndex + 1).ToString("00"), chosenLetter, correct);

                LevelTimer timer = UnityEngine.Object.FindAnyObjectByType<LevelTimer>();
                PlayerHealth health = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
                Db.SaveProgress(id, 1, string.IsNullOrEmpty(currentRoom) ? "Room 1" : currentRoom,
                                timer != null && timer.IsRunning ? "RUNNING" : "STOPPED",
                                timer != null ? timer.RemainingTime : Db.GetTimeLimit(1),
                                health != null ? health.CurrentHealth : 100f);
            });
        }

        public static void ItemCollected(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            Safe("Item " + itemId, () =>
            {
                if (itemId.StartsWith("keycard_"))
                {
                    Db.CollectKeycard(ProgressId, itemId, KeycardCode(itemId));
                    Debug.Log("[Database] Keycards collected: " + Db.GetCollectedKeycards(ProgressId).Count + " / 3");
                    return;
                }
                try { Db.CollectClue(ProgressId, itemId); }
                catch (ArgumentException) {  }
            });
        }

        static string KeycardCode(string itemId)
        {
            CodeBreaker.Level3.L3PuzzleSet p = CodeBreaker.Level3.L3Puzzle.Current;
            if (p == null) return "";
            if (itemId == "keycard_red") return "m = " + p.M;
            if (itemId == "keycard_blue") return "s = " + p.S;
            if (itemId == "keycard_green") return "c = " + p.C;
            return "";
        }

        static readonly Dictionary<string, string> Level2FlagToPuzzle = new Dictionary<string, string>
        {
            { "photo_assembled",  "living_photo" },
            { "star_shadow_seen", "living_starlamp" },
            { "star_placed",      "living_star" },
            { "exit_living",      "living_exit" },
            { "moon_seen",        "bedroom_uv" },
            { "moonbox_open",     "bedroom_moonbox" },
            { "stars_seen",       "bedroom_stars" },
            { "exit_bedroom",     "bedroom_exit" },
            { "recipe_complete",  "kitchen_recipe" },
            { "oven_open",        "kitchen_oven" },
            { "exit_kitchen",     "kitchen_exit" },
            { "picture_decoded",  "study_picture" },
        };

        static readonly HashSet<string> answeredLevel2 = new HashSet<string>();

        public static void Level2Answer(string puzzle, string answer, bool correct)
        {
            if (string.IsNullOrEmpty(puzzle)) return;
            if (correct) answeredLevel2.Add(puzzle);
            Safe("Level 2 " + puzzle, () => Db.RecordAttempt(ProgressId, "Level 2/" + puzzle, answer ?? "", correct));
        }

        public static void Level2Flag(string sceneName, string flag)
        {
            if (sceneName != "Level 2" || string.IsNullOrEmpty(flag)) return;
            string puzzle;
            if (!Level2FlagToPuzzle.TryGetValue(flag, out puzzle)) return;
            if (answeredLevel2.Remove(puzzle)) return;
            Safe("Level 2 " + puzzle, () =>
            {
                Db.RecordAttempt(ProgressId, "Level 2/" + puzzle, "(solved)", true);
                if (flag == "photo_assembled") Db.CollectClue(ProgressId, "family_photo");
            });
        }

        static readonly Dictionary<string, string> lastInput = new Dictionary<string, string>();

        public static void Level3Input(string station, string typed)
        {
            if (!string.IsNullOrEmpty(station)) lastInput[station] = typed ?? "";
        }

        public static void Level3Result(string station, bool correct)
        {
            if (string.IsNullOrEmpty(station)) return;
            string answer;
            if (lastInput.TryGetValue(station, out answer)) lastInput.Remove(station);
            else answer = correct ? "(solved)" : "(wrong)";

            Safe("Level 3 " + station, () =>
            {
                int id = ProgressId;
                if (station == "term")
                    Debug.Log("[Database] Central terminal: all three keycards collected = " + Db.HasAllLevel3Keycards(id));
                try { Db.RecordAttempt(id, "Level 3/" + station, answer, correct); }
                catch (ArgumentException) {  }
            });
        }

        public static void LevelCompleted(string sceneName, float remainingTime)
        {
            int level = DatabaseManager.LevelNumberOf(sceneName);
            if (level == 0) return;
            if (level == 2 && CodeBreaker.Level2.ScoreStores.Current is SqliteScoreStore) return;
            Safe("Level " + level + " completion", () =>
            {
                int id = ProgressId;
                int hints = level == 2 ? Db.CountHintsUsed(id, 2) : 0;
                int score = GameDatabase.CalculateLevelScore(remainingTime, hints);
                int next = Db.CompleteLevel(id, level, remainingTime, score);
                Debug.Log("[Database] Level " + level + " complete: +" + score + " points" +
                          (next > 0 ? ". Save moved to Level " + next + "." : ". Game complete."));
            });
        }

        public static void GameOver(string reason)
        {
            bool timeUp = reason != null && reason.StartsWith("Time", StringComparison.OrdinalIgnoreCase);
            Safe("Game over", () => Db.SetGameOver(ProgressId, timeUp));
        }
    }
}
