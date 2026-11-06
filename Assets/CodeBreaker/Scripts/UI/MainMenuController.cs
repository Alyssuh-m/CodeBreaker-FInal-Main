using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Code Breaker: the main menu (MainMenu scene).
//   START NEW GAME   -> asks first if it would replace an existing save, then Level 1's loading screen
//   LOAD SAVED GAMES -> shows the auto-save (level, HP, play time, date) or "No Saved Games Found"
//   SETTINGS         -> SettingsPanel, Back returns here
//   QUIT             -> confirmation, then quits (in the Editor it only stops Play mode)
// Esc / Backspace goes back from any sub-screen.
public class MainMenuController : MonoBehaviour
{
    [Header("Screens")]
    public CanvasGroup rootGroup;
    public GameObject mainPanel;
    public GameObject loadPanel;
    public GameObject settingsPanel;
    public GameObject newGameConfirmPanel;
    public GameObject quitConfirmPanel;

    [Header("Main menu")]
    public Button startButton;
    public Button loadButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Load Saved Games")]
    public GameObject saveSlot;
    public TMP_Text saveSlotTitle;
    public TMP_Text saveSlotDetails;
    public Button continueButton;
    public GameObject noSavesMessage;
    public Button loadBackButton;

    [Header("Settings")]
    public Button settingsBackButton;

    [Header("Confirmations")]
    public Button newGameYes;
    public Button newGameNo;
    public Button quitYes;
    public Button quitNo;

    [Header("Music")]
    public AudioSource menuMusic;
    [Range(0f, 1f)] public float musicCeiling = 0.45f;   // same ceiling as the in-game music

    GameObject current;
    GameObject returnSelection;
    bool busy;   // a load has started: ignore every further click

    void Awake()
    {
        Bind(startButton, OnStartNewGame);
        Bind(loadButton, () => Open(loadPanel, loadButton.gameObject));
        Bind(settingsButton, () => Open(settingsPanel, settingsButton.gameObject));
        Bind(quitButton, () => Open(quitConfirmPanel, quitButton.gameObject));
        Bind(loadBackButton, Back);
        Bind(settingsBackButton, Back);
        Bind(continueButton, OnContinue);
        Bind(newGameYes, BeginNewGame);
        Bind(newGameNo, Back);
        Bind(quitYes, QuitGame);
        Bind(quitNo, Back);
    }

    static void Bind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b != null) b.onClick.AddListener(a);
    }

    void OnEnable() => CodeBreakerSettings.Changed += ApplyMusicVolume;
    void OnDisable() => CodeBreakerSettings.Changed -= ApplyMusicVolume;

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        UIUtil.EnsureEventSystem();
        UIUtil.ShowCursor();
        SaveSystem.PendingLoad = null;

        ShowOnly(mainPanel);
        current = mainPanel;
        ApplyMusicVolume();
        if (menuMusic != null && menuMusic.clip != null && !menuMusic.isPlaying) menuMusic.Play();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { rootGroup.alpha = t / 0.6f; yield return null; }
            rootGroup.alpha = 1f;
        }
        if (startButton != null) UIUtil.Select(startButton.gameObject);
    }

    void Update()
    {
        if (busy) return;
        if (UIUtil.NeedsKeyboardReselect())
        {
            Selectable d = DefaultSelectable(current);
            if (d != null) UIUtil.Select(d.gameObject);
        }
        if (current == mainPanel) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Back();
    }

    // The button that is highlighted first on each screen (always the safe choice on confirmations).
    Selectable DefaultSelectable(GameObject panel)
    {
        if (panel == null) return null;
        if (panel == mainPanel) return startButton;
        if (panel == loadPanel) return saveSlot != null && saveSlot.activeSelf ? continueButton : loadBackButton;
        if (panel == newGameConfirmPanel) return newGameNo;
        if (panel == quitConfirmPanel) return quitNo;
        return panel.GetComponentInChildren<Selectable>();
    }

    // ------------------------------------------------------------------ navigation
    void ShowOnly(GameObject panel)
    {
        foreach (GameObject p in new[] { mainPanel, loadPanel, settingsPanel, newGameConfirmPanel, quitConfirmPanel })
            if (p != null) p.SetActive(p == panel);
    }

    void Open(GameObject panel, GameObject from)
    {
        if (busy || panel == null) return;
        returnSelection = from;
        if (panel == loadPanel) RefreshSaveSlot();
        ShowOnly(panel);
        current = panel;

        Selectable first = DefaultSelectable(panel);
        if (first != null) UIUtil.Select(first.gameObject);
    }

    public void Back()
    {
        if (busy) return;
        ShowOnly(mainPanel);
        current = mainPanel;
        UIUtil.Select(returnSelection != null ? returnSelection : startButton != null ? startButton.gameObject : null);
    }

    // ------------------------------------------------------------------ new game
    void OnStartNewGame()
    {
        if (busy) return;
        if (SaveSystem.HasSave) Open(newGameConfirmPanel, startButton.gameObject);   // never overwrite a save silently
        else BeginNewGame();
    }

    void BeginNewGame()
    {
        if (busy) return;
        LevelCatalog cat = LevelCatalog.Instance;
        string first = cat != null ? cat.FirstLevelScene : "Level 1";

        SaveSystem.PendingLoad = null;
        if (SceneLoader.Instance.LoadLevel(first))
        {
            busy = true;
            SaveSystem.Delete();   // confirmed: the old progress is replaced by this new game
            StartCoroutine(FadeMusicOut());
        }
    }

    // ------------------------------------------------------------------ load
    void RefreshSaveSlot()
    {
        SaveData d = SaveSystem.Load();
        bool has = d != null;
        if (saveSlot != null) saveSlot.SetActive(has);
        if (noSavesMessage != null) noSavesMessage.SetActive(!has);
        if (!has) return;

        LevelCatalog.LevelInfo info = LevelCatalog.Instance != null ? LevelCatalog.Instance.Find(d.sceneName) : null;
        string name = !string.IsNullOrEmpty(d.levelName) ? d.levelName : d.sceneName;
        if (saveSlotTitle != null)
            saveSlotTitle.text = "AUTO-SAVE  ·  " + name.ToUpper() + (info != null && !string.IsNullOrEmpty(info.title) ? "  <color=#8E1B17>—</color>  " + info.title.ToUpper() : "");
        if (saveSlotDetails != null)
        {
            System.DateTime when = d.SavedAtTime;
            saveSlotDetails.text =
                "Health  " + Mathf.CeilToInt(d.health) + " / " + Mathf.CeilToInt(d.maxHealth > 0 ? d.maxHealth : 100) +
                "     Play time  " + LevelTimer.Format(d.totalPlayTime) + "\n" +
                "Saved  " + (when == System.DateTime.MinValue ? "-" : when.ToString("MMM d, yyyy  h:mm tt")) +
                "\n<size=80%><color=#8C847C>Continues from the start of " + name + ".</color></size>";
        }
    }

    void OnContinue()
    {
        if (busy) return;
        SaveData d = SaveSystem.Load();
        if (d == null) { RefreshSaveSlot(); return; }   // the file vanished meanwhile

        SaveSystem.PendingLoad = d;
        if (SceneLoader.Instance.LoadLevel(d.sceneName))
        {
            busy = true;
            StartCoroutine(FadeMusicOut());
        }
        else SaveSystem.PendingLoad = null;
    }

    // ------------------------------------------------------------------ quit
    void QuitGame()
    {
        busy = true;
        CodeBreakerSettings.Save();
#if UNITY_EDITOR
        Debug.Log("[Code Breaker] Quit selected: stopping Play mode (a built game closes here).");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------ music
    void ApplyMusicVolume()
    {
        if (menuMusic != null) menuMusic.volume = musicCeiling * CodeBreakerSettings.MusicVolume;
    }

    IEnumerator FadeMusicOut()
    {
        if (menuMusic == null) yield break;
        float from = menuMusic.volume;
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { menuMusic.volume = Mathf.Lerp(from, 0f, t / 0.6f); yield return null; }
        menuMusic.volume = 0f;
    }
}
