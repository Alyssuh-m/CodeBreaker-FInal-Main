using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Code Breaker: one auto-save slot.
// The game saves automatically every time a level starts (scene, HP, wrong attempts, play time),
// every time a puzzle is solved, and at each checkpoint. "Load Saved Games" continues from there.
// The same slot also keeps the player's overall progress: completed levels, unlocked levels,
// solved puzzles (by unique ID) and one checkpoint per level.
[Serializable]
public class SaveData
{
    public int version = 1;
    public string sceneName;
    public string levelName;
    public float health;
    public float maxHealth;
    public int wrongAttempts;
    public float totalPlayTime;
    public string savedAt;   // ISO 8601, local time

    // In-level checkpoint of the level in sceneName (Levels 1-3). Empty = continue from the level start.
    public string checkpointScene;   // scene the checkpoint belongs to
    public string checkpointLabel;   // e.g. "Hall B"
    public string checkpointJson;    // that level's own progress data

    // ---- overall progress (progressVersion 2). Older save files don't have these; they are
    //      filled in from the old data when the file is loaded (SaveSystem.Upgrade).
    public int progressVersion;
    public List<string> completedLevels = new List<string>();      // scene names, e.g. "Level 1"
    public int highestUnlocked;                                      // number of unlocked levels (1 = Level 1 only)
    public List<string> solvedPuzzles = new List<string>();          // unique IDs, e.g. "Level 3/s2"
    public List<LevelCheckpoint> levelCheckpoints = new List<LevelCheckpoint>();   // checkpoints of the OTHER levels

    public DateTime SavedAtTime =>
        DateTime.TryParse(savedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : DateTime.MinValue;
}

[Serializable]
public class LevelCheckpoint
{
    public string scene;
    public string label;
    public string json;
}

public static class SaveSystem
{
    const string FileName = "codebreaker_save.json";
    public const int ProgressVersion = 2;
    const string Level3CompletedPref = "CodeBreaker.Level3.Completed";

    // Set by the main menu when the player picks a save; GameFlowManager applies it when that level starts.
    public static SaveData PendingLoad;

    // Set before reloading a level that should continue from its saved checkpoint (Level 2 "Try Again").
    // The level clears it once it has started.
    public static bool ResumeCheckpoint;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => Load() != null;

    // ------------------------------------------------------------------ checkpoints (one per level)
    // The checkpoint of a level: the current one (checkpoint* fields) or one kept in levelCheckpoints.
    public static LevelCheckpoint GetCheckpoint(SaveData d, string sceneName)
    {
        if (d == null || string.IsNullOrEmpty(sceneName)) return null;
        if (d.checkpointScene == sceneName && !string.IsNullOrEmpty(d.checkpointJson))
            return new LevelCheckpoint { scene = sceneName, label = d.checkpointLabel, json = d.checkpointJson };
        if (d.levelCheckpoints != null)
            foreach (LevelCheckpoint c in d.levelCheckpoints)
                if (c != null && c.scene == sceneName && !string.IsNullOrEmpty(c.json)) return c;
        return null;
    }

    // True when the save holds a checkpoint for this scene.
    public static bool HasCheckpoint(SaveData d, string sceneName) => GetCheckpoint(d, sceneName) != null;

    // Moves the current checkpoint into the per-level list when it belongs to another level,
    // so playing one level never wipes another level's checkpoint.
    static void StashCurrent(SaveData d, string exceptScene)
    {
        if (d == null || string.IsNullOrEmpty(d.checkpointScene) || d.checkpointScene == exceptScene) return;
        if (!string.IsNullOrEmpty(d.checkpointJson))
        {
            if (d.levelCheckpoints == null) d.levelCheckpoints = new List<LevelCheckpoint>();
            d.levelCheckpoints.RemoveAll(c => c == null || c.scene == d.checkpointScene);
            d.levelCheckpoints.Add(new LevelCheckpoint { scene = d.checkpointScene, label = d.checkpointLabel, json = d.checkpointJson });
        }
        d.checkpointScene = null;
        d.checkpointLabel = null;
        d.checkpointJson = null;
    }

    // Writes a level's checkpoint as the current one.
    public static void SetCheckpoint(SaveData d, string sceneName, string label, string json)
    {
        if (d == null) return;
        StashCurrent(d, sceneName);
        if (d.levelCheckpoints != null) d.levelCheckpoints.RemoveAll(c => c == null || c.scene == sceneName);
        d.checkpointScene = sceneName;
        d.checkpointLabel = label;
        d.checkpointJson = json;
    }

    // Makes a level's kept checkpoint the current one again (continuing that level).
    public static void ActivateCheckpoint(SaveData d, string sceneName)
    {
        LevelCheckpoint c = GetCheckpoint(d, sceneName);
        if (c == null) { StashCurrent(d, sceneName); return; }
        SetCheckpoint(d, sceneName, c.label, c.json);
    }

    // The level starts over: its checkpoint and its (unfinished) puzzle progress are dropped.
    // A level that was already completed keeps its solved puzzles.
    public static void ClearCheckpoint(SaveData d, string sceneName)
    {
        if (d == null) return;
        if (d.checkpointScene == sceneName)
        {
            d.checkpointScene = null;
            d.checkpointLabel = null;
            d.checkpointJson = null;
        }
        if (d.levelCheckpoints != null) d.levelCheckpoints.RemoveAll(c => c == null || c.scene == sceneName);
        if (!IsCompleted(d, sceneName) && d.solvedPuzzles != null)
            d.solvedPuzzles.RemoveAll(id => PuzzleBook.IsPuzzleOf(id, sceneName));
    }

    // Records which puzzles of a level are solved right now (unique IDs, no duplicates).
    public static void SetSolvedPuzzles(SaveData d, string sceneName, IEnumerable<string> ids)
    {
        if (d == null) return;
        if (d.solvedPuzzles == null) d.solvedPuzzles = new List<string>();
        if (!IsCompleted(d, sceneName)) d.solvedPuzzles.RemoveAll(id => PuzzleBook.IsPuzzleOf(id, sceneName));
        foreach (string id in ids)
            if (!string.IsNullOrEmpty(id) && !d.solvedPuzzles.Contains(id)) d.solvedPuzzles.Add(id);
    }

    // ------------------------------------------------------------------ levels: completed / unlocked
    public static int LevelCount
    {
        get { LevelCatalog cat = LevelCatalog.Instance; return cat != null && cat.levels.Count > 0 ? cat.levels.Count : 3; }
    }

    public static int LevelIndex(string sceneName)
    {
        LevelCatalog cat = LevelCatalog.Instance;
        if (cat == null) return -1;
        return cat.levels.FindIndex(l => l != null && l.sceneName == sceneName);
    }

    public static string LevelScene(int index)
    {
        LevelCatalog cat = LevelCatalog.Instance;
        return cat != null && index >= 0 && index < cat.levels.Count && cat.levels[index] != null ? cat.levels[index].sceneName : null;
    }

    public static bool IsCompleted(SaveData d, string sceneName) =>
        d != null && d.completedLevels != null && d.completedLevels.Contains(sceneName);

    // Levels unlocked so far (Level 1 is always unlocked; with no save only Level 1 is).
    public static int UnlockedCount(SaveData d)
    {
        if (d == null) return 1;
        return Mathf.Clamp(d.highestUnlocked, 1, LevelCount);
    }

    public static bool IsUnlocked(SaveData d, string sceneName)
    {
        int i = LevelIndex(sceneName);
        return i >= 0 && i < UnlockedCount(d);
    }

    // The level finished: mark it completed and unlock the next one. Continue then starts the next
    // level (or, after the last level, that level again). The finished level's checkpoint is removed.
    public static void RecordLevelCompleted(SaveData d, string sceneName)
    {
        if (d == null || string.IsNullOrEmpty(sceneName)) return;
        if (d.completedLevels == null) d.completedLevels = new List<string>();
        if (!d.completedLevels.Contains(sceneName)) d.completedLevels.Add(sceneName);
        SetSolvedPuzzles(d, sceneName, PuzzleBook.AllFor(sceneName));

        int index = LevelIndex(sceneName);
        if (index >= 0) d.highestUnlocked = Mathf.Clamp(Mathf.Max(d.highestUnlocked, index + 2), 1, LevelCount);

        ClearCheckpoint(d, sceneName);
        LevelCatalog cat = LevelCatalog.Instance;
        LevelCatalog.LevelInfo next = cat != null ? cat.NextAfter(sceneName) : null;
        if (LevelCatalog.CanLoad(next))
        {
            StashCurrent(d, next.sceneName);
            d.sceneName = next.sceneName;
            d.levelName = LevelName(next.sceneName);
            ActivateCheckpoint(d, next.sceneName);
        }
        else
        {
            d.sceneName = sceneName;
            d.levelName = LevelName(sceneName);
        }
        d.progressVersion = ProgressVersion;
    }

    // A level starts (from the menu, Next Level, Try Again...). The save now points to it.
    //  resuming = true: its checkpoint (if any) is kept and continued.
    //  resuming = false: the level starts over, so its checkpoint is dropped (other levels keep theirs).
    // Starting a level never unlocks anything.
    public static void RecordLevelStart(string sceneName, float health, float maxHealth, int wrongAttempts, float totalPlayTime, bool resuming)
    {
        SaveData d = Load() ?? NewData();
        d.sceneName = sceneName;
        d.levelName = LevelName(sceneName);
        d.health = health;
        d.maxHealth = maxHealth;
        d.wrongAttempts = wrongAttempts;
        d.totalPlayTime = totalPlayTime;
        if (resuming) ActivateCheckpoint(d, sceneName);
        else { StashCurrent(d, sceneName); ClearCheckpoint(d, sceneName); }
        Save(d);
    }

    public static SaveData NewData() => new SaveData
    {
        health = 100f,
        maxHealth = 100f,
        progressVersion = ProgressVersion,
        highestUnlocked = 1,
    };

    public static string LevelName(string sceneName)
    {
        LevelCatalog cat = LevelCatalog.Instance;
        LevelCatalog.LevelInfo info = cat != null ? cat.Find(sceneName) : null;
        return info != null && !string.IsNullOrEmpty(info.label)
            ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(info.label.ToLowerInvariant())
            : sceneName;
    }

    // ------------------------------------------------------------------ older save files
    // Save files from before progress tracking have no completed / unlocked / solved data. It is
    // worked out from what they do have, without changing the file until the game saves again:
    //  - every level before the saved one was completed (that is the only way to reach it);
    //  - the Level 2 score file's levels_completed and the Level 3 "completed" setting count too;
    //  - solved puzzles are read from the saved checkpoint.
    static void Upgrade(SaveData d)
    {
        if (d.completedLevels == null) d.completedLevels = new List<string>();
        if (d.solvedPuzzles == null) d.solvedPuzzles = new List<string>();
        if (d.levelCheckpoints == null) d.levelCheckpoints = new List<LevelCheckpoint>();
        if (d.progressVersion >= ProgressVersion) return;

        int count = LevelCount;
        int savedIndex = Mathf.Max(0, LevelIndex(d.sceneName));
        int done = savedIndex;
        try
        {
            CodeBreaker.Level2.ProgressRecord p = CodeBreaker.Level2.ScoreStores.Current.LoadProgress();
            if (p != null) done = Mathf.Max(done, p.levels_completed);
        }
        catch (Exception e) { Debug.LogWarning("[Code Breaker] Could not read the score file while upgrading the save: " + e.Message); }
        if (PlayerPrefs.GetInt(Level3CompletedPref, 0) == 1) done = Mathf.Max(done, LevelIndex(PuzzleBook.Level3) + 1);
        done = Mathf.Clamp(done, 0, count);

        for (int i = 0; i < done; i++)
        {
            string s = LevelScene(i);
            if (s != null && !d.completedLevels.Contains(s)) d.completedLevels.Add(s);
        }
        d.highestUnlocked = Mathf.Clamp(Mathf.Max(d.highestUnlocked, savedIndex + 1, done + 1), 1, count);
        foreach (string s in d.completedLevels) SetSolvedPuzzles(d, s, PuzzleBook.AllFor(s));
        if (!string.IsNullOrEmpty(d.checkpointScene) && !IsCompleted(d, d.checkpointScene))
            SetSolvedPuzzles(d, d.checkpointScene, PuzzleBook.SolvedFromCheckpoint(d.checkpointScene, d.checkpointJson));
        d.progressVersion = ProgressVersion;
    }

    // ------------------------------------------------------------------ file
    // Returns null when there is no save, or when it is unreadable / points to a scene that no longer exists.
    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (d == null || string.IsNullOrEmpty(d.sceneName)) return null;
            if (!Application.CanStreamedLevelBeLoaded(d.sceneName))
            {
                Debug.LogWarning("[Code Breaker] Save points to scene '" + d.sceneName + "', which is not in the build.");
                return null;
            }
            Upgrade(d);
            return d;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Code Breaker] Could not read the save file: " + e.Message);
            return null;
        }
    }

    public static void Save(SaveData data)
    {
        if (data == null || string.IsNullOrEmpty(data.sceneName)) return;
        try
        {
            if (data.progressVersion < ProgressVersion) Upgrade(data);
            data.savedAt = DateTime.Now.ToString("o");
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);   // write-then-swap: a crash never leaves a half-written save
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Code Breaker] Could not write the save file: " + e.Message);
        }
    }

    public static void Delete()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch (Exception e) { Debug.LogWarning("[Code Breaker] Could not delete the save file: " + e.Message); }
    }
}
