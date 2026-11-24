using System;
using System.IO;
using UnityEngine;

namespace CodeBreaker.Database
{
    // ---------------------------------------------------------------------------------------------
    // DatabaseManager: connects Unity to the SQLite database.
    //
    //  - Created automatically when the game starts (nothing to place in a scene) and kept alive
    //    across scene changes (DontDestroyOnLoad).
    //  - Database file:  <Application.persistentDataPath>/codebreaker_game.db
    //      Windows: C:/Users/<you>/AppData/LocalLow/DefaultCompany/My project (1)/codebreaker_game.db
    //  - On every start it runs StreamingAssets/CodeBreakerDB/codebreaker_database.sql. That script
    //    only creates what is missing, so existing saves are never touched.
    //  - Remembers the current player and save slot (CurrentPlayerId / CurrentProgressId).
    //
    // If SQLite cannot start (for example sqlite3.dll is missing) it logs a warning and IsReady stays
    // false; the game keeps working with its JSON save files, so a database problem never blocks play.
    //
    // Usage from any script:
    //     if (DatabaseManager.IsReady)
    //         DatabaseManager.Instance.Db.RecordAttempt(DatabaseManager.Instance.CurrentProgressId, "Level 3/s3", "42", true);
    // ---------------------------------------------------------------------------------------------
    [DefaultExecutionOrder(-1000)]
    public class DatabaseManager : MonoBehaviour
    {
        // Not "codebreaker.db": an older build of the game left a "CodeBreaker.db" with a different,
        // older table layout in the same folder, and Windows file names ignore capital letters.
        public const string DatabaseFileName = "codebreaker_game.db";
        public const string ScriptRelativePath = "CodeBreakerDB/codebreaker_database.sql";
        const string PlayerNamePref = "CodeBreaker.PlayerName";

        public static DatabaseManager Instance { get; private set; }
        public static bool IsReady => Instance != null && Instance.Db != null;

        public GameDatabase Db { get; private set; }
        public int CurrentPlayerId { get; private set; }
        public int CurrentProgressId { get; private set; }

        public static string DatabasePath => Path.Combine(Application.persistentDataPath, DatabaseFileName);

        // Runs once, before the first scene loads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateOnStartup()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("Database Manager");
            DontDestroyOnLoad(go);
            go.AddComponent<DatabaseManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Connect();
        }

        void OnApplicationQuit() => Close();
        void OnDestroy() { if (Instance == this) Close(); }

        // ------------------------------------------------------------------ connection
        void Connect()
        {
            try
            {
                string scriptPath = Path.Combine(Application.streamingAssetsPath, ScriptRelativePath);
                if (!File.Exists(scriptPath))
                {
                    Debug.LogWarning("[Database] SQL script not found at " + scriptPath + ". The database is off.");
                    return;
                }

                Db = new GameDatabase(DatabasePath);
                Db.RunScript(File.ReadAllText(scriptPath));
                Debug.Log("[Database] Connected: " + DatabasePath);

                // Log in the last player (or a default one) so saving works right away.
                LoginPlayer(PlayerPrefs.GetString(PlayerNamePref, "Player"));

                // The game's existing score system (IScoreStore) now saves into SQLite instead of
                // codebreaker_scores.json. Only switched when the database really started.
                CodeBreaker.Level2.ScoreStores.Current = new SqliteScoreStore();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Database] SQLite could not start, the game keeps using its JSON saves. " + e.Message);
                Close();
            }
        }

        void Close()
        {
            if (Db == null) return;
            Db.Dispose();
            Db = null;
        }

        // ------------------------------------------------------------------ player and save slot
        // Register the player (or find the existing one) and load their save slot if they have one.
        public void LoginPlayer(string playerName)
        {
            if (Db == null) return;
            CurrentPlayerId = Db.GetOrCreatePlayer(playerName);
            PlayerPrefs.SetString(PlayerNamePref, playerName.Trim());
            SaveGame save = Db.LoadSave(CurrentPlayerId);
            CurrentProgressId = save != null ? save.ProgressId : 0;
        }

        public bool HasSave => Db != null && Db.LoadSave(CurrentPlayerId) != null;

        public SaveGame LoadCurrentSave() => Db != null ? Db.LoadSave(CurrentPlayerId) : null;

        // "Start New Game" (replaces the old save, like the game's confirmation prompt says).
        public int StartNewGame()
        {
            if (Db == null) return 0;
            CurrentProgressId = Db.StartNewGame(CurrentPlayerId);
            return CurrentProgressId;
        }

        // The save was deleted ("Start New Game"); the next save creates a new one.
        public void ForgetProgress() => CurrentProgressId = 0;

        // Makes sure there is a save slot (used by code that saves before a new game was started).
        public int EnsureProgress()
        {
            if (Db == null) return 0;
            if (CurrentProgressId == 0) StartNewGame();
            return CurrentProgressId;
        }

        // ------------------------------------------------------------------ level helpers
        // "Level 2" -> 2, anything else -> 0.
        public static int LevelNumberOf(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Level ")) return 0;
            int n;
            return int.TryParse(sceneName.Substring(6), out n) ? n : 0;
        }

        public static string SceneNameOf(int levelNumber) => "Level " + levelNumber;

        // Loads the next level after a completed one (with the game's own loading screen),
        // or the main menu after Level 3.
        public static void LoadNextLevel(int nextLevelNumber)
        {
            if (nextLevelNumber > 0) SceneLoader.Instance.LoadLevel(SceneNameOf(nextLevelNumber));
            else SceneLoader.Instance.LoadMainMenu();
        }
    }
}
