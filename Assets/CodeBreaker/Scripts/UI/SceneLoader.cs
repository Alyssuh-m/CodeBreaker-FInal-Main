using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Code Breaker: every scene change goes through here.
//  - LoadLevel(scene): shows that level's own loading screen (image, title, progress bar),
//    loads the scene asynchronously and fades into it.
//  - LoadMainMenu(): a short black fade, no level artwork.
// Only one load can run at a time, so double clicks can never start two loads.
// The loader survives scene changes and is created on first use (nothing to place in scenes).
public class SceneLoader : MonoBehaviour
{
    const string ScreenPrefabPath = "CodeBreaker/LevelLoadingScreen";

    static SceneLoader instance;
    public static SceneLoader Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Scene Loader");
                instance = go.AddComponent<SceneLoader>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    public static bool IsLoading { get; private set; }

    [Tooltip("The level loading screen stays at least this long so it can be read (seconds, real time).")]
    public float minimumLevelScreenTime = 1.2f;
    public float fadeInTime = 0.35f;
    public float fadeOutTime = 0.5f;
    [Tooltip("Safety net: never stay on a loading screen longer than this (seconds).")]
    public float timeout = 45f;

    LevelLoadingScreen screen;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    public bool LoadLevel(string sceneName) => Begin(sceneName, true);

    public bool LoadMainMenu()
    {
        LevelCatalog cat = LevelCatalog.Instance;
        return Begin(cat != null ? cat.mainMenuScene : "MainMenu", false);
    }

    bool Begin(string sceneName, bool levelScreen)
    {
        if (IsLoading) return false;
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[Code Breaker] Scene '" + sceneName + "' is not in Build Settings, so it can't be loaded.");
            return false;
        }
        StartCoroutine(LoadRoutine(sceneName, levelScreen));
        return true;
    }

    LevelLoadingScreen GetScreen()
    {
        if (screen != null) return screen;
        LevelLoadingScreen prefab = Resources.Load<LevelLoadingScreen>(ScreenPrefabPath);
        if (prefab != null)
        {
            screen = Instantiate(prefab, transform);
            screen.name = "Level Loading Screen";
        }
        else
        {
            Debug.LogWarning("[Code Breaker] Loading screen prefab missing (Resources/" + ScreenPrefabPath + "). Using a plain fade.");
            screen = LevelLoadingScreen.CreateFallback(transform);
        }
        screen.HideImmediate();
        return screen;
    }

    IEnumerator LoadRoutine(string sceneName, bool levelScreen)
    {
        IsLoading = true;
        Time.timeScale = 1f;

        LevelLoadingScreen s = GetScreen();
        LevelCatalog cat = LevelCatalog.Instance;
        if (levelScreen) s.ShowLevel(cat != null ? cat.Find(sceneName) : null, sceneName, cat != null ? cat.loadingTips : null);
        else s.ShowPlain();
        s.SetProgress(0f, true);
        yield return s.Fade(1f, fadeInTime);

        float shownAt = Time.unscaledTime;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("[Code Breaker] Loading '" + sceneName + "' failed to start.");
            yield return s.Fade(0f, fadeOutTime);
            s.HideImmediate();
            IsLoading = false;
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f && Time.unscaledTime - shownAt < timeout)
        {
            s.SetProgress(op.progress / 0.9f, false);
            yield return null;
        }
        s.SetProgress(1f, false);

        float minTime = levelScreen ? minimumLevelScreenTime : 0.1f;
        while (Time.unscaledTime - shownAt < minTime) yield return null;

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;   // let the new scene run Start() once before revealing it

        yield return s.Fade(0f, fadeOutTime);
        s.HideImmediate();
        IsLoading = false;
    }
}
