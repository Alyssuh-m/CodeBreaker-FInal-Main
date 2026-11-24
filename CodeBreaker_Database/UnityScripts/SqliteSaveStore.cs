using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Database
{
    // ---------------------------------------------------------------------------------------------
    // SqliteSaveStore: the game's save ("Load Saved Games > Continue") stored in SQLite.
    //
    // SaveSystem.cs calls Load / Save / Delete here instead of writing codebreaker_save.json.
    // The rest of the game still works with SaveData objects, so no level script had to change.
    //
    //   SaveData field                     ->  SQLite
    //   sceneName, health, wrongAttempts,  ->  player_progress (current_level_id, health,
    //   totalPlayTime, savedAt                  wrong_attempts, total_play_time, last_saved)
    //   completedLevels                    ->  level_completion
    //   checkpoint + levelCheckpoints      ->  level_checkpoint (+ checkpoint_item for Level 2/3)
    //   highestUnlocked, solvedPuzzles     ->  not stored: worked out again on Load (no duplicate data)
    //
    // The first time the database is used, an existing codebreaker_save.json is copied in once,
    // so nobody loses their progress. The JSON file itself is left alone.
    // ---------------------------------------------------------------------------------------------
    public static class SqliteSaveStore
    {
        const string ImportedPref = "CodeBreaker.JsonSaveImported";

        // True when SQLite is running. If it is not (e.g. sqlite3.dll missing) SaveSystem falls back to JSON.
        public static bool Active => DatabaseManager.IsReady;

        static GameDatabase Db => DatabaseManager.Instance.Db;

        // ------------------------------------------------------------------ load
        public static SaveData Load()
        {
            DatabaseManager m = DatabaseManager.Instance;
            SaveGame s = m.LoadCurrentSave();
            if (s == null) return null;

            var d = new SaveData
            {
                sceneName = DatabaseManager.SceneNameOf(s.LevelNumber),
                health = s.Health,
                maxHealth = 100f,
                wrongAttempts = s.WrongAttempts,
                totalPlayTime = s.TotalPlayTime,
                savedAt = UtcToLocalIso(s.LastSaved),
                progressVersion = SaveSystem.ProgressVersion,
            };
            d.levelName = SaveSystem.LevelName(d.sceneName);

            // completed levels -> unlocked levels and solved puzzles
            int highestDone = 0;
            foreach (int n in Db.GetCompletedLevels(s.ProgressId))
            {
                string scene = DatabaseManager.SceneNameOf(n);
                d.completedLevels.Add(scene);
                d.solvedPuzzles.AddRange(PuzzleBook.AllFor(scene));
                highestDone = Math.Max(highestDone, n);
            }
            d.highestUnlocked = Mathf.Clamp(Math.Max(s.LevelNumber, highestDone + 1), 1, SaveSystem.LevelCount);

            // checkpoints
            foreach (GameDatabase.Checkpoint c in Db.LoadCheckpoints(s.ProgressId))
            {
                string scene = DatabaseManager.SceneNameOf(c.LevelNumber);
                string json = ToJson(c);
                if (json == null) continue;
                if (c.IsCurrent)
                {
                    d.checkpointScene = scene;
                    d.checkpointLabel = c.Label;
                    d.checkpointJson = json;
                }
                else d.levelCheckpoints.Add(new LevelCheckpoint { scene = scene, label = c.Label, json = json });

                if (!d.completedLevels.Contains(scene))
                    foreach (string id in PuzzleBook.SolvedFromCheckpoint(scene, json))
                        if (!d.solvedPuzzles.Contains(id)) d.solvedPuzzles.Add(id);
            }
            return d;
        }

        // ------------------------------------------------------------------ save
        public static void Save(SaveData d)
        {
            int level = DatabaseManager.LevelNumberOf(d.sceneName);
            if (level == 0) return;
            int id = DatabaseManager.Instance.EnsureProgress();

            Db.SaveSlot(id, level, d.health, d.wrongAttempts, d.totalPlayTime);
            if (d.completedLevels != null)
                foreach (string scene in d.completedLevels)
                {
                    int n = DatabaseManager.LevelNumberOf(scene);
                    if (n > 0) Db.MarkLevelCompleted(id, n);
                }

            var list = new List<GameDatabase.Checkpoint>();
            AddCheckpoint(list, d.checkpointScene, d.checkpointLabel, d.checkpointJson, true);
            if (d.levelCheckpoints != null)
                foreach (LevelCheckpoint c in d.levelCheckpoints)
                    if (c != null) AddCheckpoint(list, c.scene, c.label, c.json, false);
            Db.ReplaceCheckpoints(id, list);
        }

        // ------------------------------------------------------------------ delete ("Start New Game")
        public static void Delete()
        {
            DatabaseManager m = DatabaseManager.Instance;
            Db.DeleteSave(m.CurrentPlayerId);
            m.ForgetProgress();
        }

        // ------------------------------------------------------------------ one-time JSON import
        // Copies an existing JSON save into SQLite the first time, then never again.
        public static void ImportJsonOnce(Func<SaveData> loadJson)
        {
            if (PlayerPrefs.GetInt(ImportedPref, 0) == 1) return;
            PlayerPrefs.SetInt(ImportedPref, 1);
            if (DatabaseManager.Instance.LoadCurrentSave() != null) return;   // the database already has a save
            SaveData old = loadJson();
            if (old == null) return;
            Save(old);
            Debug.Log("[Database] The old JSON save (" + old.sceneName + ") was copied into SQLite.");
        }

        // ================================================================== SaveData <-> rows
        static void AddCheckpoint(List<GameDatabase.Checkpoint> list, string scene, string label, string json, bool current)
        {
            int level = DatabaseManager.LevelNumberOf(scene);
            if (level == 0 || string.IsNullOrEmpty(json)) return;
            foreach (GameDatabase.Checkpoint existing in list)
                if (existing.LevelNumber == level) return;   // one checkpoint per level

            var c = new GameDatabase.Checkpoint { LevelNumber = level, Label = label, IsCurrent = current };
            try
            {
                if (level == 1)
                {
                    Level1Progress p = JsonUtility.FromJson<Level1Progress>(json);
                    if (p == null) return;
                    c.RoomsCleared = p.roomsCleared;
                    c.TimeRemaining = p.timeRemaining;
                }
                else
                {
                    L2State s = JsonUtility.FromJson<L2State>(json);
                    if (s == null) return;
                    c.CheckpointNumber = s.checkpoint;
                    if (!string.IsNullOrEmpty(s.checkpointLabel)) c.Label = s.checkpointLabel;
                    c.HeldItem = s.held;
                    c.FlashlightOn = s.flashlightOn;
                    c.Objective = s.objective;
                    foreach (string i in s.inventory) Item(c, "inventory", i, null, null);
                    foreach (string i in s.collected) Item(c, "collected", i, null, null);
                    foreach (string f in s.flags) Item(c, "flag", f, null, null);
                    foreach (L2IntEntry e in s.ints) Item(c, "number", e.key, e.value, null);
                    foreach (L2DoorEntry e in s.doors) Item(c, "door", e.name, e.locked ? 1 : 0, e.open ? 1 : 0);
                    foreach (L2LightEntry e in s.lights) Item(c, "light", e.group, e.on ? 1 : 0, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Database] Checkpoint of " + scene + " could not be read: " + e.Message);
                return;
            }
            list.Add(c);
        }

        static void Item(GameDatabase.Checkpoint c, string type, string name, int? v1, int? v2)
        {
            if (string.IsNullOrEmpty(name)) return;
            c.Items.Add(new GameDatabase.CheckpointItem { Type = type, Name = name, Value1 = v1, Value2 = v2 });
        }

        // Rebuilds the level's own checkpoint data, exactly as the level scripts expect to read it.
        static string ToJson(GameDatabase.Checkpoint c)
        {
            if (c.LevelNumber == 1)
                return JsonUtility.ToJson(new Level1Progress
                {
                    roomsCleared = c.RoomsCleared ?? 0,
                    timeRemaining = c.TimeRemaining ?? -1f,
                });

            var s = new L2State
            {
                checkpoint = c.CheckpointNumber ?? 0,
                checkpointLabel = c.Label,
                held = c.HeldItem,
                flashlightOn = c.FlashlightOn ?? false,
                objective = c.Objective,
            };
            foreach (GameDatabase.CheckpointItem i in c.Items)
            {
                switch (i.Type)
                {
                    case "inventory": s.inventory.Add(i.Name); break;
                    case "collected": s.collected.Add(i.Name); break;
                    case "flag": s.flags.Add(i.Name); break;
                    case "number": s.ints.Add(new L2IntEntry { key = i.Name, value = i.Value1 ?? 0 }); break;
                    case "door": s.doors.Add(new L2DoorEntry { name = i.Name, locked = i.Value1 == 1, open = i.Value2 == 1 }); break;
                    case "light": s.lights.Add(new L2LightEntry { group = i.Name, on = i.Value1 == 1 }); break;
                }
            }
            return JsonUtility.ToJson(s);
        }

        // "2026-11-22 13:39:55" (UTC, from SQLite) -> local ISO time like the JSON save used.
        static string UtcToLocalIso(string utc)
        {
            DateTime t;
            if (DateTime.TryParse(utc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out t))
                return t.ToLocalTime().ToString("o");
            return DateTime.Now.ToString("o");
        }
    }
}
