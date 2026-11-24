using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodeBreaker.Database
{
    // ---------------------------------------------------------------------------------------------
    // DatabaseExamples: how the game's scripts talk to the database. One method per task.
    //
    // Each method is ready to call from the place named in its comment (a button, a door, a pickup).
    // Every method first checks DatabaseManager.IsReady, so nothing breaks if SQLite is not set up.
    //
    // Example calls:
    //     DatabaseExamples.RegisterPlayer("Juan");
    //     DatabaseExamples.OnLevel1DoorChosen(question, AnswerLetter.B);
    //     DatabaseExamples.OnKeycardCollected("keycard_red", "m = 9");
    // ---------------------------------------------------------------------------------------------
    public static class DatabaseExamples
    {
        static GameDatabase Db => DatabaseManager.Instance.Db;
        static int ProgressId => DatabaseManager.Instance.EnsureProgress();

        // ================================================================= 1. register / create a player
        // Call from the main menu after the player types a name (e.g. a TMP_InputField's onEndEdit).
        public static void RegisterPlayer(string playerName)
        {
            if (!DatabaseManager.IsReady || string.IsNullOrWhiteSpace(playerName)) return;
            DatabaseManager.Instance.LoginPlayer(playerName);
            Debug.Log("[Database] Player '" + playerName + "' has id " + DatabaseManager.Instance.CurrentPlayerId);
        }

        // ================================================================= 2. start a new game
        // Call from "Start New Game" > "Start Over". The old save is replaced.
        public static void StartNewGame()
        {
            if (!DatabaseManager.IsReady) return;
            DatabaseManager.Instance.StartNewGame();
            SceneLoader.Instance.LoadLevel(DatabaseManager.SceneNameOf(1));
        }

        // ================================================================= 3. save progress
        // Save the current level, room, remaining timer and health.
        // Call at checkpoints, when a puzzle is solved, when the player pauses or quits.
        public static void SaveProgress(string currentRoom)
        {
            if (!DatabaseManager.IsReady) return;

            int level = DatabaseManager.LevelNumberOf(SceneManager.GetActiveScene().name);
            if (level == 0) return;   // not inside a level (main menu)

            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();

            float remaining = timer != null ? timer.RemainingTime : Db.GetTimeLimit(level);
            string status = timer == null ? "STOPPED" : timer.IsRunning ? "RUNNING" : "PAUSED";
            float hp = health != null ? health.CurrentHealth : 100f;

            Db.SaveProgress(ProgressId, level, currentRoom, status, remaining, hp);
        }

        // Only the timer, e.g. from the pause menu.
        public static void SaveTimer(bool paused)
        {
            if (!DatabaseManager.IsReady) return;
            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            if (timer != null) Db.SaveTimer(ProgressId, paused ? "PAUSED" : "RUNNING", timer.RemainingTime);
        }

        // ================================================================= 4. load saved progress
        // Call from "Load Saved Games" > "Continue".
        static SaveGame pendingLoad;

        public static void ContinueSavedGame()
        {
            if (!DatabaseManager.IsReady) return;
            SaveGame save = DatabaseManager.Instance.LoadCurrentSave();
            if (save == null || save.GameStatus != "IN_PROGRESS")
            {
                Debug.Log("[Database] No save to continue (or the saved game is over).");
                return;
            }

            pendingLoad = save;
            SceneManager.sceneLoaded += ApplyLoadedSave;            // runs when the level has loaded
            SceneLoader.Instance.LoadLevel(DatabaseManager.SceneNameOf(save.LevelNumber));
        }

        static void ApplyLoadedSave(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= ApplyLoadedSave;
            if (pendingLoad == null) return;

            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            if (timer != null) timer.SetRemainingTime(pendingLoad.RemainingTime);

            PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
            if (health != null) health.RestoreState(pendingLoad.Health, 0);

            Debug.Log("[Database] Continued " + pendingLoad.LevelName + ", " + pendingLoad.CurrentRoom +
                      ", " + LevelTimer.Format(pendingLoad.RemainingTime) + " left, score " + pendingLoad.Score);
            pendingLoad = null;
        }

        // Text for the Load Saved Games screen: "LEVEL 3 · Control Room · 25:20 · 2026-11-22 10:41".
        public static string SaveSummary()
        {
            if (!DatabaseManager.IsReady) return "No saved game";
            SaveGame s = DatabaseManager.Instance.LoadCurrentSave();
            if (s == null) return "No saved game";
            return "LEVEL " + s.LevelNumber + " · " + s.CurrentRoom + " · " +
                   LevelTimer.Format(s.RemainingTime) + " · " + s.LastSavedLocal;
        }

        // ================================================================= 5. Level 1: record the door choice
        // Level 1 keeps its questions in the QuestionBank asset; the database has the same 15
        // questions as puzzles "Level 1/q01" ... "Level 1/q15" (same order). questionIndex is the
        // index RoomManager / QuestionPool used (0-based).
        public static bool OnLevel1DoorChosen(int questionIndex, AnswerLetter chosenDoor)
        {
            if (!DatabaseManager.IsReady) return false;
            string key = "Level 1/q" + (questionIndex + 1).ToString("00");

            bool correct = Db.CheckAnswer(key, chosenDoor.ToString());   // "A" or "B"
            Db.RecordAttempt(ProgressId, key, chosenDoor.ToString(), correct);

            if (!correct)
            {
                // Wrong door = trap room. The game takes health; when health reaches 0 the game ends.
                PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
                if (health == null || health.CurrentHealth <= 0f)
                    Db.SetGameOver(ProgressId, false);
            }
            SaveProgress(correct ? "Room cleared" : "Trap Room");
            return correct;
        }

        // ================================================================= 6. record any puzzle attempt
        // For Level 2 and 3 keypads, locks and terminals. The game already checked the answer
        // (Level 3 values change every run), so pass its result in isCorrect.
        //   e.g. in L2Oven after checking the code:  DatabaseExamples.RecordAttempt("Level 2/kitchen_oven", input, ok);
        public static void RecordAttempt(string puzzleKey, string submittedAnswer, bool isCorrect)
        {
            if (!DatabaseManager.IsReady) return;
            Db.RecordAttempt(ProgressId, puzzleKey, submittedAnswer, isCorrect);
        }

        // ================================================================= 7. Level 2: clues, keys and hints
        // Call when an item is picked up. itemId = the L2Items id, e.g. "photo_1", "living_key", "uv_flashlight".
        public static void OnItemCollected(string itemId)
        {
            if (!DatabaseManager.IsReady) return;
            try { Db.CollectClue(ProgressId, itemId); }
            catch (System.ArgumentException) { /* an item the database does not track */ }
        }

        // Call when a hint is shown. puzzleKey = "Level 2/kitchen_oven", hintNumber = 1, 2, ...
        // Returns the hints used so far in that level (each costs 250 points in Level 2).
        public static int OnHintUsed(string puzzleKey, int hintNumber)
        {
            if (!DatabaseManager.IsReady) return 0;
            string clueKey = "hint_" + puzzleKey.Substring(puzzleKey.IndexOf('/') + 1) + "_" + hintNumber;
            try { Db.CollectClue(ProgressId, clueKey); }
            catch (System.ArgumentException) { }
            int level = DatabaseManager.LevelNumberOf(puzzleKey.Substring(0, puzzleKey.IndexOf('/')));
            return Db.CountHintsUsed(ProgressId, level);
        }

        // ================================================================= 8. update the score
        // Show the live score, or set it directly.
        public static int CurrentScore()
        {
            if (!DatabaseManager.IsReady) return 0;
            SaveGame s = DatabaseManager.Instance.LoadCurrentSave();
            return s != null ? s.Score : 0;
        }

        public static void SetScore(int newScore)
        {
            if (DatabaseManager.IsReady) Db.UpdateScore(ProgressId, newScore);
        }

        // ================================================================= 9. Level 3: keycards
        // Call when a keycard is picked up. itemId = "keycard_red", "keycard_blue" or "keycard_green",
        // accessCode = the text on the card this run, e.g. "m = 9".
        public static void OnKeycardCollected(string itemId, string accessCode)
        {
            if (!DatabaseManager.IsReady) return;
            Db.CollectKeycard(ProgressId, itemId, accessCode);
            Debug.Log("[Database] Keycards: " + Db.GetCollectedKeycards(ProgressId).Count + " / 3");
            SaveProgress("Server Room");
        }

        // ================================================================= 10. Level 3: central terminal
        // Call when the player submits the exit code. Returns true when the exit may open.
        // correctForThisRun = the game's own check (L3Keypad / L3PuzzleSet.ExitCode).
        public static bool OnTerminalSubmit(string enteredCode, bool correctForThisRun)
        {
            if (!DatabaseManager.IsReady) return correctForThisRun;

            if (!Db.HasAllLevel3Keycards(ProgressId))
            {
                Debug.Log("[Database] ACCESS DENIED: insert all three keycards first.");
                return false;
            }
            Db.RecordAttempt(ProgressId, "Level 3/term", enteredCode, correctForThisRun);
            return correctForThisRun;
        }

        // Which side-room challenges are done (e.g. for the objective panel).
        public static List<string> CompletedLevel3Challenges()
        {
            return DatabaseManager.IsReady ? Db.GetSolvedPuzzles(ProgressId, 3) : new List<string>();
        }

        // ================================================================= 11. level complete -> next level
        // Call from the "CONGRATULATIONS!" screen's Next Level button (or when the exit opens).
        public static void OnLevelCompleted(int levelNumber, bool loadNextNow)
        {
            if (!DatabaseManager.IsReady) return;

            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            float remaining = timer != null ? timer.RemainingTime : 0f;
            int hints = levelNumber == 2 ? Db.CountHintsUsed(ProgressId, 2) : 0;   // only Level 2 hints cost points
            int score = GameDatabase.CalculateLevelScore(remaining, hints);

            int next = Db.CompleteLevel(ProgressId, levelNumber, remaining, score);
            Debug.Log("[Database] Level " + levelNumber + " complete: +" + score + " points. Next: " +
                      (next > 0 ? "Level " + next : "game complete"));

            if (loadNextNow) DatabaseManager.LoadNextLevel(next);
        }

        // ================================================================= 12. game over
        // Call from GameFlowManager.TimeUp() / PlayerDied().
        public static void OnGameOver(bool timeUp)
        {
            if (DatabaseManager.IsReady) Db.SetGameOver(ProgressId, timeUp);
        }
    }
}
