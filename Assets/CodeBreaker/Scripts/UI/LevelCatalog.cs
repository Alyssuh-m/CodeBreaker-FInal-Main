using System.Collections.Generic;
using UnityEngine;

// Code Breaker: the list of levels in play order, with what each loading screen shows.
// The asset lives at Resources/CodeBreaker/LevelCatalog so it can be found at runtime.
//  - playable = false: the level exists but isn't ready yet. "Next Level" then says
//    "More levels coming soon" instead of loading an unfinished scene.
[CreateAssetMenu(menuName = "Code Breaker/Level Catalog", fileName = "LevelCatalog")]
public class LevelCatalog : ScriptableObject
{
    [System.Serializable]
    public class LevelInfo
    {
        [Tooltip("Scene name exactly as in Build Settings, e.g. Level 1")]
        public string sceneName;
        [Tooltip("Small label above the title, e.g. LEVEL 1")]
        public string label;
        [Tooltip("Big title on the loading screen")]
        public string title;
        [TextArea(2, 4)] public string description;
        [Tooltip("Loading screen background (a view of this level)")]
        public Sprite loadingImage;
        [Tooltip("Off = not finished yet; Next Level won't load it")]
        public bool playable = true;
    }

    public string mainMenuScene = "MainMenu";
    public List<LevelInfo> levels = new List<LevelInfo>();
    [TextArea(1, 2)] public string[] loadingTips;

    static LevelCatalog cached;
    public static LevelCatalog Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<LevelCatalog>("CodeBreaker/LevelCatalog");
            return cached;
        }
    }

    public string FirstLevelScene => levels.Count > 0 ? levels[0].sceneName : "Level 1";

    public LevelInfo Find(string sceneName) => levels.Find(l => l != null && l.sceneName == sceneName);

    // The next level in order, or null when sceneName is the last one (or not in the list).
    public LevelInfo NextAfter(string sceneName)
    {
        int i = levels.FindIndex(l => l != null && l.sceneName == sceneName);
        return i >= 0 && i + 1 < levels.Count ? levels[i + 1] : null;
    }

    public static bool CanLoad(LevelInfo info) =>
        info != null && info.playable && !string.IsNullOrEmpty(info.sceneName) && Application.CanStreamedLevelBeLoaded(info.sceneName);
}
