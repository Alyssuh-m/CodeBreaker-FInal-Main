using System;
using System.IO;
using UnityEngine;

namespace CodeBreaker.Database
{
    [DefaultExecutionOrder(-1000)]
    public class DatabaseManager : MonoBehaviour
    {
        public const string DatabaseFileName = "codebreaker_game.db";
        public const string ScriptRelativePath = "CodeBreakerDB/codebreaker_database.sql";
        const string PlayerNamePref = "CodeBreaker.PlayerName";

        public static DatabaseManager Instance { get; private set; }
        public static bool IsReady => Instance != null && Instance.Db != null;

        public GameDatabase Db { get; private set; }
        public int CurrentPlayerId { get; private set; }
        public int CurrentProgressId { get; private set; }

        public static string DatabasePath => Path.Combine(Application.persistentDataPath, DatabaseFileName);

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

                LoginPlayer(PlayerPrefs.GetString(PlayerNamePref, "Player"));

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

        public int StartNewGame()
        {
            if (Db == null) return 0;
            CurrentProgressId = Db.StartNewGame(CurrentPlayerId);
            return CurrentProgressId;
        }

        public void ForgetProgress() => CurrentProgressId = 0;

        public int EnsureProgress()
        {
            if (Db == null) return 0;
            if (CurrentProgressId == 0) StartNewGame();
            return CurrentProgressId;
        }

        public static int LevelNumberOf(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Level ")) return 0;
            int n;
            return int.TryParse(sceneName.Substring(6), out n) ? n : 0;
        }

        public static string SceneNameOf(int levelNumber) => "Level " + levelNumber;

        public static void LoadNextLevel(int nextLevelNumber)
        {
            if (nextLevelNumber > 0) SceneLoader.Instance.LoadLevel(SceneNameOf(nextLevelNumber));
            else SceneLoader.Instance.LoadMainMenu();
        }
    }
}
