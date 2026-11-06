using System;
using System.IO;
using UnityEngine;

// Code Breaker: one auto-save slot.
// The game saves automatically every time a level starts (scene, HP, wrong attempts, play time),
// so "Load Saved Games" continues from the start of the last level that was reached.
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

    public DateTime SavedAtTime =>
        DateTime.TryParse(savedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : DateTime.MinValue;
}

public static class SaveSystem
{
    const string FileName = "codebreaker_save.json";

    // Set by the main menu when the player picks a save; GameFlowManager applies it when that level starts.
    public static SaveData PendingLoad;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => Load() != null;

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
