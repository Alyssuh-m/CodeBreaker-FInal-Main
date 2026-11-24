using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeBreaker.Database
{
    // ---------------------------------------------------------------------------------------------
    // DatabaseHooks: the ONE line the game's scripts call when something happens.
    //
    // Where each hook is called from:
    //   NewGame            MainMenuController.BeginNewGame, GameFlowManager.StartNewGame (R key)
    //   LevelStarted       GameFlowManager.BeginLevelBookkeeping (every level start / Try Again / Continue)
    //   Level1DoorChosen   RoomManager.OnDoorChosen and RoomManager.OnThresholdCrossed
    //   ItemCollected      L2Game.Collect (Level 2 clues/keys/items, Level 3 name tags, fuses, KEYCARDS)
    //   Level2Flag         L2Game.SetFlag and L2Game.AssemblePhoto (every Level 2 puzzle that gets solved)
    //   Level2Answer       L2Oven.Submit, L2RecipeCard.Pick, L2UI moon box lock, L2Workstation (checks + password)
    //   Level3Input        L3Keypad.Submit (remembers what was typed)
    //   Level3Result       L3Manager.Solve / L3Manager.AddWrong (every Level 3 challenge)
    //   LevelCompleted     GameFlowManager.LevelCompleted (Level 1), L2Game.CompleteLevel (Level 3)
    //                      Level 2 is saved by SqliteScoreStore (L2LevelSession.FinishLevel).
    //   GameOver           GameFlowManager.GameOver
    //
    // Every hook is wrapped in try/catch: a database problem is logged and the game keeps going.
    // ---------------------------------------------------------------------------------------------
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

        // ------------------------------------------------------------------ game start
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

        // ------------------------------------------------------------------ Level 1
        // questionIndex = index in the QuestionBank (0-14) = puzzle "Level 1/q01".."q15".
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

        // ------------------------------------------------------------------ items, clues and keycards
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
                catch (ArgumentException) { /* not a clue the database tracks (e.g. the flashlight) */ }
            });
        }

        // The value printed on the card in THIS run (Level 3 values are random per run).
        static string KeycardCode(string itemId)
        {
            CodeBreaker.Level3.L3PuzzleSet p = CodeBreaker.Level3.L3Puzzle.Current;
            if (p == null) return "";
            if (itemId == "keycard_red") return "m = " + p.M;
            if (itemId == "keycard_blue") return "s = " + p.S;
            if (itemId == "keycard_green") return "c = " + p.C;
            return "";
        }

        // ------------------------------------------------------------------ Level 2 puzzles
        // A Level 2 puzzle is solved when its flag is set (PuzzleBook.cs uses the same flags).
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

        // Puzzles whose correct answer was already saved by Level2Answer (so the flag doesn't save it twice).
        static readonly HashSet<string> answeredLevel2 = new HashSet<string>();

        // A typed / chosen answer: oven code, moon box arrows, recipe choice, computer checks and password.
        public static void Level2Answer(string puzzle, string answer, bool correct)
        {
            if (string.IsNullOrEmpty(puzzle)) return;
            if (correct) answeredLevel2.Add(puzzle);
            Safe("Level 2 " + puzzle, () => Db.RecordAttempt(ProgressId, "Level 2/" + puzzle, answer ?? "", correct));
        }

        // Called by L2Game whenever a flag is turned on. Only Level 2 puzzle flags are saved.
        public static void Level2Flag(string sceneName, string flag)
        {
            if (sceneName != "Level 2" || string.IsNullOrEmpty(flag)) return;
            string puzzle;
            if (!Level2FlagToPuzzle.TryGetValue(flag, out puzzle)) return;
            if (answeredLevel2.Remove(puzzle)) return;   // already saved with the typed answer
            Safe("Level 2 " + puzzle, () =>
            {
                Db.RecordAttempt(ProgressId, "Level 2/" + puzzle, "(solved)", true);
                if (flag == "photo_assembled") Db.CollectClue(ProgressId, "family_photo");   // the restored photo is a clue too
            });
        }

        // ------------------------------------------------------------------ Level 3 challenges
        static readonly Dictionary<string, string> lastInput = new Dictionary<string, string>();

        // Called by L3Keypad before it checks the answer, so the attempt stores what was typed.
        public static void Level3Input(string station, string typed)
        {
            if (!string.IsNullOrEmpty(station)) lastInput[station] = typed ?? "";
        }

        // Called by L3Manager when a station is solved or answered wrong.
        public static void Level3Result(string station, bool correct)
        {
            if (string.IsNullOrEmpty(station)) return;
            string answer;
            if (lastInput.TryGetValue(station, out answer)) lastInput.Remove(station);
            else answer = correct ? "(solved)" : "(wrong)";   // stations without a keypad (fuses, levers, tags, cameras)

            Safe("Level 3 " + station, () =>
            {
                int id = ProgressId;
                if (station == "term")
                    Debug.Log("[Database] Central terminal: all three keycards collected = " + Db.HasAllLevel3Keycards(id));
                try { Db.RecordAttempt(id, "Level 3/" + station, answer, correct); }
                catch (ArgumentException) { /* a station that is not a puzzle in the database */ }
            });
        }

        // ------------------------------------------------------------------ level end
        public static void LevelCompleted(string sceneName, float remainingTime)
        {
            int level = DatabaseManager.LevelNumberOf(sceneName);
            if (level == 0) return;
            if (level == 2 && CodeBreaker.Level2.ScoreStores.Current is SqliteScoreStore) return;   // saved by SqliteScoreStore
            Safe("Level " + level + " completion", () =>
            {
                int id = ProgressId;
                int hints = level == 2 ? Db.CountHintsUsed(id, 2) : 0;   // only Level 2 hints cost points
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
