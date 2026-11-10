using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Code Breaker: the game session.
//
// Lives on the "Game Session" object in Level 1 and survives scene loads (DontDestroyOnLoad),
// together with its HUD and result screens. PlayerHealth and LevelTimer sit on the same
// object, so health carries over from Level 1 to Level 2 and beyond. Each level gets its
// own fresh time limit (LevelTimer.levelTimeLimits).
//
//  - Level finished (not the last one) -> CONGRATULATIONS screen with TRY AGAIN / NEXT LEVEL / EXIT TO HOME.
//                                         (Without a Level Complete Menu assigned, the old countdown is used.)
//  - Final level finished              -> GAME COMPLETE screen (a different ending), timer stops.
//  - HP 0 or time up                   -> GAME OVER; R starts a completely new game
//                                         (health, timer, rooms and questions all reset).
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Session Systems (on this object)")]
    public PlayerHealth playerHealth;
    public LevelTimer levelTimer;

    [Header("HUD")]
    public TMP_Text locationText;

    [Header("Result UI")]
    public GameObject resultCanvas;
    public TMP_Text resultTitle;
    public TMP_Text resultDescription;
    public GameObject tryAgainButton;
    [Tooltip("Dark full-screen panel behind the big screens (Congratulations, Game Over, Game Complete).")]
    public Image resultBackdrop;
    [Tooltip("Full-screen black image used for the fade between rooms.")]
    public Image fadeOverlay;
    public AudioSettingsMenu settingsMenu;
    [Tooltip("Framed panel behind the full-screen results (Congratulations, Game Over, Game Complete).")]
    public GameObject resultPanel;
    [Tooltip("Smaller framed panel behind short messages (wrong door, you're alive...).")]
    public GameObject messagePanel;

    [Header("Level Complete (Congratulations buttons)")]
    [Tooltip("TRY AGAIN / NEXT LEVEL / EXIT TO HOME. Leave empty to use the old automatic countdown.")]
    public LevelCompleteMenu levelCompleteMenu;

    [Header("Levels")]
    [Tooltip("Scene loaded when a completely new game starts.")]
    public string firstLevelScene = "Level 1";
    [Tooltip("Finishing this scene shows the final GAME COMPLETE screen.")]
    public string finalLevelScene = "Level 3";
    [Tooltip("Seconds the level Congratulations screen stays before the next level loads.")]
    public float levelTransitionDelay = 6f;

    public bool IsGameOver { get; private set; }
    public bool GameCompleted { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool LevelCompleteShown { get; private set; }
    public bool IsBusy => RoomManager.Instance != null && RoomManager.Instance.IsBusy;
    public bool SettingsOpen => settingsMenu != null && settingsMenu.IsOpen;
    public bool InteractionsBlocked => IsGameOver || GameCompleted || IsTransitioning || LevelCompleteShown || IsBusy || SettingsOpen;

    float totalPlayTime;   // time used in finished levels (for the final screen)
    Behaviour[] playerControls = new Behaviour[0];
    Coroutine hideRoutine;
    bool shuttingDown;
    bool exitingToHome;
    string pendingNextScene;

    // State at the start of the current level, restored by Try Again.
    float healthAtLevelStart = -1f;
    int wrongAttemptsAtLevelStart;
    float playTimeAtLevelStart;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);   // a session is already running (came from an earlier level)
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        FindPlayerControls();
        if (resultCanvas != null && !InteractionsBlocked)
            resultCanvas.SetActive(false);
        if (levelCompleteMenu != null && !LevelCompleteShown)
            levelCompleteMenu.Hide();
        BeginLevelBookkeeping(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (exitingToHome)
        {
            // Arrived at the main menu: the session (HUD, music, timer) ends here.
            shuttingDown = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
            Destroy(gameObject);
            return;
        }
        if (shuttingDown) return;

        IsTransitioning = false;
        LevelCompleteShown = false;
        if (levelCompleteMenu != null) levelCompleteMenu.Hide();
        SetFade(0f);

        // A new level starts with its own time limit (Level 1 = 3:00, Level 2 = 20:00).
        if (!IsGameOver && !GameCompleted && levelTimer != null)
            levelTimer.BeginLevel(scene.name);

        FindPlayerControls();
        SetControlsEnabled(!IsGameOver && !GameCompleted);

        if (!IsGameOver && !GameCompleted && resultCanvas != null)
            resultCanvas.SetActive(false);

        if (RoomManager.Instance == null)
            SetLocation(scene.name.ToUpper());

        BeginLevelBookkeeping(scene);
    }

    // ---------------------------------------------------------------- level start: load / remember / auto-save
    // Runs when a level starts (from OnSceneLoaded and Start; running twice is harmless).
    void BeginLevelBookkeeping(Scene scene)
    {
        if (IsGameOver || GameCompleted || shuttingDown) return;
        LevelCatalog cat = LevelCatalog.Instance;

        // Continuing a saved game: health, wrong attempts and play time come from the save.
        SaveData pending = SaveSystem.PendingLoad;
        // Resuming an in-level checkpoint (Load Saved Games or Level 2 Try Again): keep it in the new auto-save.
        bool resumingCheckpoint = (pending != null && pending.sceneName == scene.name) || SaveSystem.ResumeCheckpoint;
        SaveData previous = resumingCheckpoint ? SaveSystem.Load() : null;
        if (pending != null && pending.sceneName == scene.name)
        {
            SaveSystem.PendingLoad = null;
            if (playerHealth != null) playerHealth.RestoreState(pending.health, pending.wrongAttempts);
            totalPlayTime = Mathf.Max(0f, pending.totalPlayTime);
        }

        if (playerHealth != null)
        {
            healthAtLevelStart = playerHealth.CurrentHealth;
            wrongAttemptsAtLevelStart = playerHealth.WrongAttempts;
        }
        playTimeAtLevelStart = totalPlayTime;

        // Auto-save: Load Saved Games continues from the start of this level.
        // Only real levels from the Level Catalog are saved (not test scenes or the menu).
        LevelCatalog.LevelInfo info = cat != null ? cat.Find(scene.name) : null;
        if (info == null) return;
        SaveData data = new SaveData
        {
            sceneName = scene.name,
            levelName = info != null && !string.IsNullOrEmpty(info.label) ? ToTitle(info.label) : scene.name,
            health = playerHealth != null ? playerHealth.CurrentHealth : 100f,
            maxHealth = playerHealth != null ? playerHealth.maxHealth : 100f,
            wrongAttempts = playerHealth != null ? playerHealth.WrongAttempts : 0,
            totalPlayTime = totalPlayTime,
        };
        if (SaveSystem.HasCheckpoint(previous, scene.name))
        {
            data.checkpointScene = previous.checkpointScene;
            data.checkpointLabel = previous.checkpointLabel;
            data.checkpointJson = previous.checkpointJson;
        }
        SaveSystem.Save(data);
    }

    static string ToTitle(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    void Update()
    {
        if ((IsGameOver || GameCompleted) && Input.GetKeyDown(KeyCode.R))
            StartNewGame();
    }

    // ---------------------------------------------------------------- end states
    public void PlayerDied(string reason) => GameOver(reason);

    // A level can show its own time-up screen instead of Game Over (Level 2: Retry / Quit).
    // Return true from the handler when it took care of it. Null (Level 1) = the normal Game Over.
    public static System.Func<bool> TimeUpHandler;

    public void TimeUp()
    {
        if (TimeUpHandler != null && TimeUpHandler())
        {
            StopTimer();
            return;
        }
        GameOver("Time's up! You ran out of time before escaping.");
    }

    public void GameOver(string reason)
    {
        if (IsGameOver || GameCompleted) return;

        IsGameOver = true;
        StopTimer();
        CloseSettings();
        if (AudioManager.Instance != null) AudioManager.Instance.StopGameplayAudio();
        SetControlsEnabled(false);
        CloseLectern();
        ShowResult("GAME OVER", "#F14141", reason, "PRESS R TO START A NEW GAME", true);
    }

    // Called by RoomManager only when the LAST room of a level is solved (never after a single room).
    public void LevelCompleted(string levelDisplayName, int roomsCleared = 0)
    {
        if (IsGameOver || GameCompleted || IsTransitioning) return;
        CloseSettings();

        // The level is over: stop its clock and remember how long it took.
        float levelTime = levelTimer != null ? levelTimer.ElapsedTime : 0f;
        totalPlayTime += levelTime;
        StopTimer();

        Scene scene = SceneManager.GetActiveScene();

        if (levelCompleteMenu != null)
        {
            // Congratulations screen with Try Again / Next Level / Exit to Home.
            if (scene.name == finalLevelScene) CompleteGame();
            else ShowLevelComplete(levelDisplayName, NextPlayableScene(scene), levelTime, roomsCleared);
            return;
        }

        // Old flow (no menu assigned): countdown, then the next scene in Build Settings.
        string next = NextSceneName(scene);
        if (scene.name == finalLevelScene || string.IsNullOrEmpty(next))
            CompleteGame();
        else
            StartCoroutine(GoToNextLevel(levelDisplayName, next, levelTime, roomsCleared));
    }

    void ShowLevelComplete(string levelDisplayName, string nextScene, float levelTime, int roomsCleared)
    {
        LevelCompleteShown = true;
        pendingNextScene = nextScene;
        SetControlsEnabled(false);
        CloseLectern();

        string rooms = roomsCleared > 0 ? "All " + roomsCleared + " rooms cleared" : "All rooms cleared";
        string hp = playerHealth != null ? " with " + Mathf.CeilToInt(playerHealth.CurrentHealth) + " HP left" : "";
        string body = "You escaped " + levelDisplayName + " in " + LevelTimer.Format(levelTime) + ".\n" + rooms + hp + ".";
        if (!string.IsNullOrEmpty(nextScene))
        {
            string nextLimit = levelTimer != null ? LevelTimer.Format(levelTimer.LimitFor(nextScene)) : "--:--";
            body += "\n<size=85%><color=#9A918A>Next: " + nextScene + "  ·  " + nextLimit + " on the clock</color></size>";
        }
        else
        {
            body += "\n<size=85%><color=#9A918A>More levels are on the way.</color></size>";
        }

        ShowResult("CONGRATULATIONS!", "#E4D8C8", body, null, true);
        levelCompleteMenu.Show(!string.IsNullOrEmpty(nextScene));
    }

    // ---------------------------------------------------------------- Congratulations buttons
    // TRY AGAIN: the same level from the beginning. Rooms, questions, doors and the timer reset with
    // the scene; health and wrong attempts go back to what they were when the level started.
    public bool RetryLevel()
    {
        if (SceneLoader.IsLoading || shuttingDown) return false;
        string scene = SceneManager.GetActiveScene().name;

        IsTransitioning = true;   // blocks interactions until the reloaded level starts
        if (!SceneLoader.Instance.LoadLevel(scene)) { IsTransitioning = false; return false; }

        if (playerHealth != null && healthAtLevelStart > 0f) playerHealth.RestoreState(healthAtLevelStart, wrongAttemptsAtLevelStart);
        totalPlayTime = playTimeAtLevelStart;
        IsGameOver = false;
        GameCompleted = false;
        LevelCompleteShown = false;
        if (AudioManager.Instance != null) AudioManager.Instance.ResumeGameplayAudio();
        if (levelCompleteMenu != null) levelCompleteMenu.Hide();
        return true;
    }

    // NEXT LEVEL: only when the next level exists, is in Build Settings and is marked playable.
    public bool GoToNextLevel()
    {
        if (SceneLoader.IsLoading || shuttingDown || string.IsNullOrEmpty(pendingNextScene)) return false;
        IsTransitioning = true;
        if (!SceneLoader.Instance.LoadLevel(pendingNextScene)) { IsTransitioning = false; return false; }
        LevelCompleteShown = false;
        if (levelCompleteMenu != null) levelCompleteMenu.Hide();
        return true;
    }

    // EXIT TO HOME: back to the main menu. Gameplay stops now; the session object is removed once
    // the menu has loaded, so nothing keeps running in the background. Progress stays saved.
    public bool ExitToHome()
    {
        if (SceneLoader.IsLoading || shuttingDown) return false;
        IsTransitioning = true;
        StopTimer();
        SetControlsEnabled(false);
        CloseLectern();
        CloseSettings();
        if (!SceneLoader.Instance.LoadMainMenu()) { IsTransitioning = false; return false; }

        exitingToHome = true;
        if (AudioManager.Instance != null) AudioManager.Instance.StopGameplayAudio();
        if (levelCompleteMenu != null) levelCompleteMenu.Hide();
        return true;
    }

    // Next level from the Level Catalog (falls back to Build Settings order without a catalog).
    string NextPlayableScene(Scene scene)
    {
        LevelCatalog cat = LevelCatalog.Instance;
        if (cat != null && cat.Find(scene.name) != null)
        {
            LevelCatalog.LevelInfo next = cat.NextAfter(scene.name);
            return LevelCatalog.CanLoad(next) ? next.sceneName : null;
        }
        return NextSceneName(scene);
    }

    // Kept for older DoorInteraction setups (isFinalDoor).
    public void LevelComplete() => LevelCompleted(SceneManager.GetActiveScene().name);

    // Level Congratulations screen, then the next level loads with its own fresh time limit.
    IEnumerator GoToNextLevel(string levelDisplayName, string nextScene, float levelTime, int roomsCleared)
    {
        IsTransitioning = true;
        SetControlsEnabled(false);
        CloseLectern();

        string rooms = roomsCleared > 0 ? "All " + roomsCleared + " rooms cleared" : "All rooms cleared";
        string hp = playerHealth != null ? " with " + Mathf.CeilToInt(playerHealth.CurrentHealth) + " HP left" : "";
        string nextLimit = levelTimer != null ? LevelTimer.Format(levelTimer.LimitFor(nextScene)) : "--:--";
        string body = "You completed " + levelDisplayName + " in " + LevelTimer.Format(levelTime) + ".\n" +
                      rooms + hp + ".\n\n" +
                      "Next: " + nextScene + "  ·  " + nextLimit + " on the clock";

        for (int s = Mathf.CeilToInt(levelTransitionDelay); s > 0; s--)
        {
            ShowResult("CONGRATULATIONS!", "#43C979", body + "\n<size=80%>Starting in " + s + "...</size>", null, true);
            yield return new WaitForSeconds(1f);
        }

        SceneManager.LoadScene(nextScene);
    }

    void CompleteGame()
    {
        GameCompleted = true;
        StopTimer();
        CloseSettings();
        if (AudioManager.Instance != null) AudioManager.Instance.StopGameplayAudio();
        SetControlsEnabled(false);
        CloseLectern();

        ShowResult("GAME COMPLETE!", "#E8C547",
            "You escaped every level of Code Breaker!\nTotal time: " + LevelTimer.Format(totalPlayTime),
            levelCompleteMenu != null ? null : "PRESS R TO PLAY AGAIN", true);

        if (levelCompleteMenu != null)
        {
            LevelCompleteShown = true;
            pendingNextScene = null;
            levelCompleteMenu.Show(false, false);   // Try Again + Exit to Home (there is no next level)
        }
    }

    // Full reset: health, timer, completed rooms, checkpoints and questions all start over
    // because the session is destroyed and Level 1 is loaded fresh.
    public void StartNewGame()
    {
        if (SceneLoader.IsLoading) return;
        Time.timeScale = 1f;
        shuttingDown = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
        SaveSystem.PendingLoad = null;
        Destroy(gameObject);
        if (!SceneLoader.Instance.LoadLevel(firstLevelScene))   // Level 1 loading screen
            SceneManager.LoadScene(firstLevelScene);
    }

    // ---------------------------------------------------------------- helpers
    string NextSceneName(Scene scene)
    {
        int next = scene.buildIndex + 1;
        if (scene.buildIndex < 0 || next >= SceneManager.sceneCountInBuildSettings) return null;
        string path = SceneUtility.GetScenePathByBuildIndex(next);
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    // Fade the screen to black (1) or back (0). Used for the dark-vestibule passages.
    public IEnumerator FadeTo(float target, float duration)
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.gameObject.SetActive(true);
        float start = fadeOverlay.color.a;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetFade(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetFade(target);
    }

    void SetFade(float a)
    {
        if (fadeOverlay == null) return;
        Color c = fadeOverlay.color;
        c.a = a;
        fadeOverlay.color = c;
        fadeOverlay.gameObject.SetActive(a > 0.001f);
    }

    void CloseSettings()
    {
        if (settingsMenu != null) settingsMenu.Close();
    }

    void StopTimer()
    {
        if (levelTimer != null) levelTimer.StopTimer();
    }

    void CloseLectern()
    {
        if (LecternQuestion.OpenLectern != null)
            LecternQuestion.OpenLectern.Close();
    }

    void FindPlayerControls()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) { playerControls = new Behaviour[0]; return; }

        var list = new System.Collections.Generic.List<Behaviour>();
        foreach (MonoBehaviour mb in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "PlayerMovement" || n == "MouseMovement") list.Add(mb);
        }
        playerControls = list.ToArray();
    }

    public void SetControlsEnabled(bool enabled)
    {
        foreach (Behaviour b in playerControls)
            if (b != null) b.enabled = enabled;
    }

    public void SetLocation(string text)
    {
        if (locationText != null) locationText.text = text;
    }

    // Short message on the result canvas (wrong door, try again). duration <= 0 keeps it on screen.
    public void ShowMessage(string title, string titleHex, string description, string buttonLabel, float duration)
    {
        if (IsGameOver || GameCompleted || LevelCompleteShown) return;   // never cover the end screens

        ShowResult(title, titleHex, description, buttonLabel, false);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        if (duration > 0f) hideRoutine = StartCoroutine(HideAfter(duration));
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!IsGameOver && !GameCompleted && !IsTransitioning && !LevelCompleteShown && resultCanvas != null)
            resultCanvas.SetActive(false);
        hideRoutine = null;
    }

    void ShowResult(string title, string titleHex, string description, string buttonLabel, bool fullScreen)
    {
        if (resultBackdrop != null)
            resultBackdrop.gameObject.SetActive(fullScreen);
        if (resultPanel != null) resultPanel.SetActive(fullScreen);
        if (messagePanel != null) messagePanel.SetActive(!fullScreen);

        if (hideRoutine != null && (IsGameOver || GameCompleted || IsTransitioning || LevelCompleteShown))
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (resultCanvas != null)
            resultCanvas.SetActive(true);

        if (resultTitle != null)
        {
            resultTitle.text = title;
            if (ColorUtility.TryParseHtmlString(titleHex, out Color c))
                resultTitle.color = c;
        }

        if (resultDescription != null)
        {
            resultDescription.text = description;
            if (ColorUtility.TryParseHtmlString("#D7D7D7", out Color d))
                resultDescription.color = d;
        }

        if (tryAgainButton != null)
        {
            tryAgainButton.SetActive(!string.IsNullOrEmpty(buttonLabel));

            TMP_Text label = tryAgainButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && !string.IsNullOrEmpty(buttonLabel))
            {
                label.text = buttonLabel;
                if (ColorUtility.TryParseHtmlString("#CFCFCF", out Color b))
                    label.color = b;   // same colour DoorInteraction uses
            }
        }
    }
}
