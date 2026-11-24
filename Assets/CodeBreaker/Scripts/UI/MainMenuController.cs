using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Code Breaker: the main menu (MainMenu scene).
//   START NEW GAME   -> asks first if it would replace an existing save, then Level 1's loading screen
//   LOAD SAVED GAMES -> shows the auto-save (level, HP, play time, date, completed levels) or
//                       "No Saved Games Found", plus SELECT LEVEL: every level, locked ones greyed out
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

    [Header("Select Level (built at runtime inside Load Saved Games when left empty)")]
    public RectTransform levelSelectRoot;
    public float levelButtonWidth = 280f;
    public float levelButtonHeight = 66f;

    readonly System.Collections.Generic.List<Button> levelButtons = new System.Collections.Generic.List<Button>();
    readonly System.Collections.Generic.List<string> levelScenes = new System.Collections.Generic.List<string>();

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
        BuildLevelSelect();
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
        if (panel == loadPanel) return saveSlot != null && saveSlot.activeSelf ? continueButton : FirstUnlockedLevelButton() ?? loadBackButton;
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
        if (panel == loadPanel) { RefreshSaveSlot(); RefreshLevelSelect(); }
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
            CodeBreaker.Database.DatabaseHooks.NewGame();   // SQLite: the old save slot is replaced too
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
 (PlayerHealth.UsedIn(d.sceneName)
                    ? "Health  " + Mathf.CeilToInt(d.health) + " / " + Mathf.CeilToInt(d.maxHealth > 0 ? d.maxHealth : 100) + "     "
                    : "") +
                "Play time  " + LevelTimer.Format(d.totalPlayTime) + "\n" +
                "Saved  " + (when == System.DateTime.MinValue ? "-" : when.ToString("MMM d, yyyy  h:mm tt")) + "\n" +
                ProgressLine(d) +
                "\n<size=80%><color=#8C847C>" + ContinueLine(d, name) + "</color></size>";
        }
    }

    // "Completed: Level 1, Level 2  ·  Next: Level 3"
    static string ProgressLine(SaveData d)
    {
        var done = new System.Collections.Generic.List<string>();
        string next = null;
        int count = SaveSystem.LevelCount;
        for (int i = 0; i < count; i++)
        {
            string s = SaveSystem.LevelScene(i);
            if (s == null) continue;
            if (SaveSystem.IsCompleted(d, s)) done.Add(SaveSystem.LevelName(s));
            else if (next == null && SaveSystem.IsUnlocked(d, s)) next = SaveSystem.LevelName(s);
        }
        string line = "Completed  " + (done.Count == 0 ? "none yet" : done.Count == count ? "all " + count + " levels" : string.Join(", ", done));
        if (next != null) line += "     Next  " + next;
        return line;
    }

    static string ContinueLine(SaveData d, string name)
    {
        LevelCheckpoint cp = SaveSystem.GetCheckpoint(d, d.sceneName);
        string where = cp != null && !string.IsNullOrEmpty(cp.label) && cp.label != d.sceneName && !cp.label.StartsWith(d.sceneName + " (")
            ? "Continues from the " + cp.label + " checkpoint."
            : cp != null ? "Continues where you left off." : "Continues from the start of " + name + ".";
        int total = PuzzleBook.For(d.sceneName).Count;
        if (total > 0 && cp != null)
        {
            int solved = 0;
            foreach (string id in d.solvedPuzzles) if (PuzzleBook.IsPuzzleOf(id, d.sceneName)) solved++;
            where += "\nPuzzles solved in " + name + ":  " + solved + " / " + total;
        }
        return where;
    }

    // ------------------------------------------------------------------ select level
    // One button per level from the Level Catalog. Locked levels are greyed out and can't be pressed.
    // A level with a saved checkpoint continues from it; otherwise it starts from the beginning.
    void BuildLevelSelect()
    {
        LevelCatalog cat = LevelCatalog.Instance;
        if (cat == null || continueButton == null || saveSlot == null) return;
        RectTransform panel = saveSlot.transform.parent as RectTransform;
        if (panel == null) return;

        if (levelSelectRoot == null)
        {
            // Make room under the save slot: the panel grows, the slot moves up.
            const float extra = 150f;
            panel.sizeDelta += new Vector2(0f, extra);
            foreach (GameObject g in new[] { saveSlot, noSavesMessage })
                if (g != null) ((RectTransform)g.transform).anchoredPosition += new Vector2(0f, extra * 0.5f - 15f);
            RectTransform slot = (RectTransform)saveSlot.transform;
            slot.sizeDelta += new Vector2(0f, 30f);
            if (saveSlotDetails != null) saveSlotDetails.rectTransform.sizeDelta += new Vector2(20f, 30f);   // stays clear of CONTINUE

            GameObject root = new GameObject("Level Select", typeof(RectTransform));
            levelSelectRoot = (RectTransform)root.transform;
            levelSelectRoot.SetParent(panel, false);
            levelSelectRoot.anchorMin = levelSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
            levelSelectRoot.pivot = new Vector2(0.5f, 0.5f);
            levelSelectRoot.sizeDelta = new Vector2(920f, 120f);
            float slotBottom = slot.anchoredPosition.y - slot.sizeDelta.y * 0.5f;
            levelSelectRoot.anchoredPosition = new Vector2(0f, slotBottom - 78f);

            if (saveSlotTitle != null)
            {
                TMP_Text header = Instantiate(saveSlotTitle, levelSelectRoot);
                header.name = "Header";
                header.text = "SELECT LEVEL";
                header.alignment = TextAlignmentOptions.Center;
                RectTransform h = header.rectTransform;
                h.anchorMin = h.anchorMax = new Vector2(0.5f, 1f);
                h.pivot = new Vector2(0.5f, 1f);
                h.anchoredPosition = new Vector2(0f, 0f);
                h.sizeDelta = new Vector2(920f, 40f);
                header.fontSize = 26f;
            }
        }

        float gap = 24f;
        int n = cat.levels.Count;
        float total = n * levelButtonWidth + (n - 1) * gap;
        for (int i = 0; i < n; i++)
        {
            LevelCatalog.LevelInfo info = cat.levels[i];
            if (info == null || string.IsNullOrEmpty(info.sceneName)) continue;
            Button b = Instantiate(continueButton, levelSelectRoot);
            b.name = "Level Button " + (i + 1);
            b.onClick = new Button.ButtonClickedEvent();   // drop anything copied from Continue
            RectTransform r = (RectTransform)b.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(levelButtonWidth, levelButtonHeight);
            r.anchoredPosition = new Vector2(-total * 0.5f + levelButtonWidth * 0.5f + i * (levelButtonWidth + gap), 0f);
            string scene = info.sceneName;
            b.onClick.AddListener(() => OnSelectLevel(scene));
            levelButtons.Add(b);
            levelScenes.Add(scene);
        }
    }

    void RefreshLevelSelect()
    {
        SaveData d = SaveSystem.Load();
        for (int i = 0; i < levelButtons.Count; i++)
        {
            Button b = levelButtons[i];
            string scene = levelScenes[i];
            LevelCatalog.LevelInfo info = LevelCatalog.Instance != null ? LevelCatalog.Instance.Find(scene) : null;
            bool unlocked = SaveSystem.IsUnlocked(d, scene) && LevelCatalog.CanLoad(info);
            bool completed = SaveSystem.IsCompleted(d, scene);
            bool inProgress = SaveSystem.HasCheckpoint(d, scene);
            string status = !unlocked ? "<color=#6E6660>LOCKED</color>"
                : inProgress ? "<color=#E8C547>CONTINUE</color>"
                : completed ? "<color=#5FD383>COMPLETED</color>"
                : "<color=#9A918A>START</color>";
            b.interactable = unlocked;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = (unlocked ? "" : "<color=#6E6660>") + SaveSystem.LevelName(scene).ToUpper() + (unlocked ? "" : "</color>") +
                             "\n<size=62%>" + status + "</size>";
                label.alpha = unlocked ? 1f : 0.75f;
            }
            CanvasGroup cg = b.GetComponent<CanvasGroup>();
            if (cg == null) cg = b.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = unlocked ? 1f : 0.45f;
        }
    }

    Selectable FirstUnlockedLevelButton()
    {
        foreach (Button b in levelButtons) if (b != null && b.interactable) return b;
        return null;
    }

    void OnSelectLevel(string scene)
    {
        if (busy) return;
        SaveData d = SaveSystem.Load();
        LevelCatalog.LevelInfo info = LevelCatalog.Instance != null ? LevelCatalog.Instance.Find(scene) : null;
        if (!SaveSystem.IsUnlocked(d, scene) || !LevelCatalog.CanLoad(info)) { RefreshLevelSelect(); return; }   // locked: never loads

        // Same as Continue, but for the chosen level. Its own checkpoint (if any) is continued.
        SaveData p = d != null ? JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(d)) : SaveSystem.NewData();
        if (d == null || d.sceneName != scene)
        {
            p.health = p.maxHealth > 0f ? p.maxHealth : 100f;   // health only carries over inside the saved level
            p.wrongAttempts = 0;
        }
        p.sceneName = scene;
        SaveSystem.PendingLoad = p;
        if (SceneLoader.Instance.LoadLevel(scene))
        {
            busy = true;
            StartCoroutine(FadeMusicOut());
        }
        else SaveSystem.PendingLoad = null;
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
