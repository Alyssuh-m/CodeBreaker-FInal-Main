using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodeBreaker.Database
{
    public static class DatabaseExamples
    {
        static GameDatabase Db => DatabaseManager.Instance.Db;
        static int ProgressId => DatabaseManager.Instance.EnsureProgress();

        public static void RegisterPlayer(string playerName)
        {
            if (!DatabaseManager.IsReady || string.IsNullOrWhiteSpace(playerName)) return;
            DatabaseManager.Instance.LoginPlayer(playerName);
            Debug.Log("[Database] Player '" + playerName + "' has id " + DatabaseManager.Instance.CurrentPlayerId);
        }

        public static void StartNewGame()
        {
            if (!DatabaseManager.IsReady) return;
            DatabaseManager.Instance.StartNewGame();
            SceneLoader.Instance.LoadLevel(DatabaseManager.SceneNameOf(1));
        }

        public static void SaveProgress(string currentRoom)
        {
            if (!DatabaseManager.IsReady) return;

            int level = DatabaseManager.LevelNumberOf(SceneManager.GetActiveScene().name);
            if (level == 0) return;

            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();

            float remaining = timer != null ? timer.RemainingTime : Db.GetTimeLimit(level);
            string status = timer == null ? "STOPPED" : timer.IsRunning ? "RUNNING" : "PAUSED";
            float hp = health != null ? health.CurrentHealth : 100f;

            Db.SaveProgress(ProgressId, level, currentRoom, status, remaining, hp);
        }

        public static void SaveTimer(bool paused)
        {
            if (!DatabaseManager.IsReady) return;
            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            if (timer != null) Db.SaveTimer(ProgressId, paused ? "PAUSED" : "RUNNING", timer.RemainingTime);
        }

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
            SceneManager.sceneLoaded += ApplyLoadedSave;
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

        public static string SaveSummary()
        {
            if (!DatabaseManager.IsReady) return "No saved game";
            SaveGame s = DatabaseManager.Instance.LoadCurrentSave();
            if (s == null) return "No saved game";
            return "LEVEL " + s.LevelNumber + " · " + s.CurrentRoom + " · " +
                   LevelTimer.Format(s.RemainingTime) + " · " + s.LastSavedLocal;
        }

        public static bool OnLevel1DoorChosen(int questionIndex, AnswerLetter chosenDoor)
        {
            if (!DatabaseManager.IsReady) return false;
            string key = "Level 1/q" + (questionIndex + 1).ToString("00");

            bool correct = Db.CheckAnswer(key, chosenDoor.ToString());
            Db.RecordAttempt(ProgressId, key, chosenDoor.ToString(), correct);

            if (!correct)
            {
                PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
                if (health == null || health.CurrentHealth <= 0f)
                    Db.SetGameOver(ProgressId, false);
            }
            SaveProgress(correct ? "Room cleared" : "Trap Room");
            return correct;
        }

        public static void RecordAttempt(string puzzleKey, string submittedAnswer, bool isCorrect)
        {
            if (!DatabaseManager.IsReady) return;
            Db.RecordAttempt(ProgressId, puzzleKey, submittedAnswer, isCorrect);
        }

        public static void OnItemCollected(string itemId)
        {
            if (!DatabaseManager.IsReady) return;
            try { Db.CollectClue(ProgressId, itemId); }
            catch (System.ArgumentException) {  }
        }

        public static int OnHintUsed(string puzzleKey, int hintNumber)
        {
            if (!DatabaseManager.IsReady) return 0;
            string clueKey = "hint_" + puzzleKey.Substring(puzzleKey.IndexOf('/') + 1) + "_" + hintNumber;
            try { Db.CollectClue(ProgressId, clueKey); }
            catch (System.ArgumentException) { }
            int level = DatabaseManager.LevelNumberOf(puzzleKey.Substring(0, puzzleKey.IndexOf('/')));
            return Db.CountHintsUsed(ProgressId, level);
        }

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

        public static void OnKeycardCollected(string itemId, string accessCode)
        {
            if (!DatabaseManager.IsReady) return;
            Db.CollectKeycard(ProgressId, itemId, accessCode);
            Debug.Log("[Database] Keycards: " + Db.GetCollectedKeycards(ProgressId).Count + " / 3");
            SaveProgress("Server Room");
        }

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

        public static List<string> CompletedLevel3Challenges()
        {
            return DatabaseManager.IsReady ? Db.GetSolvedPuzzles(ProgressId, 3) : new List<string>();
        }

        public static void OnLevelCompleted(int levelNumber, bool loadNextNow)
        {
            if (!DatabaseManager.IsReady) return;

            LevelTimer timer = Object.FindAnyObjectByType<LevelTimer>();
            float remaining = timer != null ? timer.RemainingTime : 0f;
            int hints = levelNumber == 2 ? Db.CountHintsUsed(ProgressId, 2) : 0;
            int score = GameDatabase.CalculateLevelScore(remaining, hints);

            int next = Db.CompleteLevel(ProgressId, levelNumber, remaining, score);
            Debug.Log("[Database] Level " + levelNumber + " complete: +" + score + " points. Next: " +
                      (next > 0 ? "Level " + next : "game complete"));

            if (loadNextNow) DatabaseManager.LoadNextLevel(next);
        }

        public static void OnGameOver(bool timeUp)
        {
            if (DatabaseManager.IsReady) Db.SetGameOver(ProgressId, timeUp);
        }
    }
}
