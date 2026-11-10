using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace CodeBreaker.Level2
{
    // All Level 2 interface, built at runtime from the project's own UI kit (CB_ sprites, Oswald and
    // Special Elite fonts) so it matches the existing menus:
    //   HUD (crosshair, prompt, objective, notifications, held item), inventory, document close-ups,
    //   puzzle screens, pause menu, settings, confirmations and the completion screen.
    // This file: shared kit, HUD, documents, pause, settings, confirm, completion.
    // L2UI.Panels.cs: inventory and puzzle screens.
    public partial class L2UI : MonoBehaviour
    {
        [Header("Style (project UI kit, assigned by the setup tool)")]
        public TMP_FontAsset headingFont;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset noteFont;
        public Sprite panelSprite;
        public Sprite buttonSprite;
        public Sprite accentSprite;
        public Sprite dividerSprite;
        public Sprite vignetteSprite;
        public Sprite trackSprite;
        public Sprite fillSprite;
        public Sprite handleSprite;

        [Header("Images")]
        public Texture[] photoPieces = new Texture[4];
        public Texture familyPhoto;

        public bool IsPaused { get; private set; }
        [System.NonSerialized] public string completionExtra;   // e.g. Level 2 score line
        public bool IsTyping => passwordInput != null && passwordInput.isFocused;

        static readonly Color Ink = new Color(0.92f, 0.88f, 0.81f);
        static readonly Color Dim = new Color(0.64f, 0.6f, 0.55f);
        static readonly Color Gold = new Color(0.91f, 0.77f, 0.28f);
        static readonly Color Good = new Color(0.38f, 0.84f, 0.52f);
        static readonly Color Bad = new Color(0.95f, 0.36f, 0.32f);
        static readonly Color Shade = new Color(0.07f, 0.064f, 0.058f, 0.94f);

        L2Game G => L2Game.Instance;

        RectTransform root;
        GameObject currentModal;
        System.Action onModalClosed;

        // HUD
        Image crosshair;
        GameObject promptBox;
        TMP_Text promptText;
        TMP_Text objectiveText, objectiveHint;
        RectTransform noteStack;
        TMP_Text heldText;
        readonly List<Note> notes = new List<Note>();

        class Note { public GameObject go; public CanvasGroup cg; public float age; }

        // Document
        GameObject docPanel;
        TMP_Text docTitle, docBody;
        RawImage docImage;
        AspectRatioFitter docFitter;
        GameObject docImageFrame;

        // Pause / settings / confirm / completion
        GameObject pausePanel, settingsPanel, confirmPanel, completionPanel;
        TMP_Text confirmTitle, confirmBody;
        System.Action confirmYes;
        Button nextLevelButton;
        TMP_Text nextLevelLabel, completionBody;

        Sprite circleSprite;

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            circleSprite = MakeCircleSprite(64);
            BuildCanvas();
            BuildHud();
            BuildDocument();
            BuildInventory();
            BuildPhoto();
            BuildClock();
            BuildRemote();
            BuildLock();
            BuildDrawers();
            BuildKeypad();
            BuildPassword();
            BuildPause();
            BuildSettings();
            BuildCompletion();
            BuildConfirm();   // last = drawn on top
        }

        IEnumerator Start()
        {
            UIUtil.EnsureEventSystem();
            if (G != null) G.Changed += OnGameChanged;
            OnGameChanged();
            yield return null;
            if (G != null && G.LoadedState == null)
                Notify("WASD move · Mouse look · E interact · F pick up · V inventory · G flashlight · Esc pause", L2NoteKind.Info, 7f);
        }

        void OnDestroy()
        {
            if (G != null) G.Changed -= OnGameChanged;
            if (IsPaused) Time.timeScale = 1f;
        }

        void OnGameChanged()
        {
            RefreshHeld();
            if (currentModal == invPanel) RefreshInventory();
        }

        void Update()
        {
            L2Game g = G;
            if (g == null) return;

            GameFlowManager gm = GameFlowManager.Instance;
            bool sessionMenu = gm != null && gm.SettingsOpen;

            if (Input.GetKeyDown(KeyCode.Escape) && !sessionMenu)
            {
                if (confirmPanel.activeSelf) CloseConfirm();
                else if (settingsPanel.activeSelf) CloseSettings();
                else if (currentModal != null) CloseModal();
                else if (IsPaused) Resume();
                else if (!g.Completed && !g.InteractionsBlocked) Pause();
            }

            if (Input.GetKeyDown(KeyCode.V) && !IsTyping && !IsPaused && !g.Completed && !confirmPanel.activeSelf)
            {
                if (currentModal == invPanel) CloseModal();
                else if (currentModal == null && !g.InteractionsBlocked) OpenInventory();
            }

            UpdateNotes();
            crosshair.gameObject.SetActive(currentModal == null && !IsPaused && !g.Completed);
            if (lockFlash > 0f) UpdateLockFlash();
        }

        // ------------------------------------------------------------------ kit
        void BuildCanvas()
        {
            var go = new GameObject("L2 UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            Canvas c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 40;
            CanvasScaler s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.matchWidthOrHeight = 0.5f;
            root = (RectTransform)go.transform;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        Image Img(Transform parent, string name, Sprite s, Color c)
        {
            RectTransform rt = NewRect(name, parent);
            Image i = rt.gameObject.AddComponent<Image>();
            i.sprite = s;
            i.color = c;
            if (s != null && s.border != Vector4.zero) i.type = Image.Type.Sliced;
            return i;
        }

        TMP_Text Txt(Transform parent, string name, string text, float size, Color c, TextAlignmentOptions align, TMP_FontAsset font = null)
        {
            RectTransform rt = NewRect(name, parent);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset f = font != null ? font : (bodyFont != null ? bodyFont : headingFont);
            if (f != null) t.font = f;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        Button Btn(Transform parent, string label, UnityAction onClick, Vector2 size, out TMP_Text text, float fontSize = 28f)
        {
            Image bg = Img(parent, label + " Button", buttonSprite, Color.white);
            bg.rectTransform.sizeDelta = size;
            Button b = bg.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = new Color(0.82f, 0.8f, 0.78f);
            cb.highlightedColor = new Color(1f, 0.93f, 0.75f);
            cb.pressedColor = new Color(1f, 0.8f, 0.45f);
            cb.selectedColor = new Color(0.95f, 0.9f, 0.8f);
            cb.disabledColor = new Color(0.4f, 0.38f, 0.36f, 0.7f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.targetGraphic = bg;
            text = Txt(bg.transform, "Label", label, fontSize, Ink, TextAlignmentOptions.Center, headingFont);
            Stretch(text.rectTransform, 10f, 4f, 10f, 4f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = 14f;
            b.onClick.AddListener(() => { if (G != null && G.sfx != null) G.sfx.Click(); });
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }

        Button Btn(Transform parent, string label, UnityAction onClick, Vector2 size, float fontSize = 28f) =>
            Btn(parent, label, onClick, size, out _, fontSize);

        // A centred framed window on a dimmed backdrop. Returns the backdrop (shown / hidden as a whole).
        GameObject Window(string name, string title, Vector2 size, out RectTransform content, bool closeButton = true)
        {
            RectTransform overlay = NewRect(name, root);
            Stretch(overlay);
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);

            Image panel = Img(overlay, "Panel", panelSprite, panelSprite != null ? Color.white : Shade);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);

            if (!string.IsNullOrEmpty(title))
            {
                TMP_Text t = Txt(panel.transform, "Title", title, 40f, Gold, TextAlignmentOptions.Center, headingFont);
                Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(size.x - 160f, 56f));
                t.characterSpacing = 4f;
                if (dividerSprite != null)
                {
                    Image d = Img(panel.transform, "Divider", dividerSprite, new Color(1f, 1f, 1f, 0.6f));
                    Place(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(Mathf.Min(560f, size.x - 120f), 22f));
                }
            }

            content = NewRect("Content", panel.transform);
            Stretch(content, 46f, 40f, 46f, string.IsNullOrEmpty(title) ? 40f : 118f);

            if (closeButton)
            {
                Button x = Btn(panel.transform, "X", CloseModal, new Vector2(64f, 56f), 30f);
                Place((RectTransform)x.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -26f), new Vector2(64f, 56f));
                x.transform.SetAsLastSibling();   // above the content
            }
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ modal handling
        void OpenModal(GameObject m, System.Action onClosed = null)
        {
            if (m == null || G == null) return;
            if (currentModal != null && currentModal != m) currentModal.SetActive(false);
            currentModal = m;
            onModalClosed = onClosed;
            m.SetActive(true);
            m.transform.SetAsLastSibling();
            confirmPanel.transform.SetAsLastSibling();
            SetPrompt(null);
            G.SetModal(true);
        }

        public void CloseModal()
        {
            if (currentModal == null) return;
            currentModal.SetActive(false);
            currentModal = null;
            System.Action cb = onModalClosed;
            onModalClosed = null;
            cb?.Invoke();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (G != null) G.SetModal(false);
        }

        public bool AnyModalOpen => currentModal != null;

        // ------------------------------------------------------------------ HUD
        void BuildHud()
        {
            crosshair = Img(root, "Crosshair", circleSprite, new Color(1f, 1f, 1f, 0.85f));
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            crosshair.raycastTarget = false;

            Image pb = Img(root, "Prompt", null, new Color(0f, 0f, 0f, 0.55f));
            Place(pb.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(620f, 52f));
            pb.raycastTarget = false;
            promptText = Txt(pb.transform, "Text", "", 28f, Ink, TextAlignmentOptions.Center, headingFont);
            Stretch(promptText.rectTransform, 16f, 0f, 16f, 0f);
            promptText.textWrappingMode = TextWrappingModes.NoWrap;
            promptBox = pb.gameObject;
            promptBox.SetActive(false);

            // objective (top-left, below the session HUD)
            Image ob = Img(root, "Objective", null, new Color(0f, 0f, 0f, 0.45f));
            Place(ob.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -150f), new Vector2(560f, 118f));
            ob.raycastTarget = false;
            if (accentSprite != null)
            {
                Image a = Img(ob.transform, "Accent", accentSprite, Gold);
                Place(a.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 118f));
                a.type = Image.Type.Simple;
                a.raycastTarget = false;
            }
            TMP_Text head = Txt(ob.transform, "Label", "OBJECTIVE", 18f, Gold, TextAlignmentOptions.TopLeft, headingFont);
            Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(520f, 24f));
            head.characterSpacing = 6f;
            objectiveText = Txt(ob.transform, "Text", "", 26f, Ink, TextAlignmentOptions.TopLeft, headingFont);
            Place(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -36f), new Vector2(526f, 40f));
            objectiveHint = Txt(ob.transform, "Hint", "", 20f, Dim, TextAlignmentOptions.TopLeft);
            Place(objectiveHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -76f), new Vector2(526f, 40f));

            // notifications (right side)
            noteStack = NewRect("Notifications", root);
            Place(noteStack, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 260f), new Vector2(560f, 420f));
            VerticalLayoutGroup v = noteStack.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.LowerRight;
            v.spacing = 8f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;

            // held item (bottom-right, above the held model)
            heldText = Txt(root, "Held Item", "", 22f, Ink, TextAlignmentOptions.BottomRight, headingFont);
            Place(heldText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 24f), new Vector2(620f, 64f));
        }

        public void SetPrompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text) && currentModal == null && !IsPaused;
            if (promptBox.activeSelf != show) promptBox.SetActive(show);
            if (show && promptText.text != text)
            {
                promptText.text = text;
                float w = Mathf.Clamp(promptText.GetPreferredValues(text).x + 48f, 260f, 1100f);
                ((RectTransform)promptBox.transform).sizeDelta = new Vector2(w, 52f);
            }
            crosshair.rectTransform.sizeDelta = show ? new Vector2(14f, 14f) : new Vector2(8f, 8f);
            crosshair.color = show ? Gold : new Color(1f, 1f, 1f, 0.85f);
        }

        public void SetObjective(string text, string hint)
        {
            objectiveText.text = text;
            objectiveHint.text = hint ?? "";
        }

        void RefreshHeld()
        {
            L2Game g = G;
            if (g == null || heldText == null) return;
            if (string.IsNullOrEmpty(g.Held)) { heldText.text = ""; return; }
            if (L2Items.IsLight(g.Held))
                heldText.text = L2Items.NameOf(g.Held).ToUpperInvariant() + "  ·  " + (g.FlashlightOn ? "<color=#B48CFF>ON</color>" : "OFF") + "  <color=#9A918A>(G)</color>";
            else
                heldText.text = "IN HAND: " + L2Items.NameOf(g.Held).ToUpperInvariant() + "  <color=#9A918A>(E on a target)</color>";
        }

        public void Notify(string text, L2NoteKind kind, float life = 4f)
        {
            Color accent = kind == L2NoteKind.Success ? Good : kind == L2NoteKind.Warning ? Bad : kind == L2NoteKind.Item ? Gold : new Color(0.7f, 0.68f, 0.64f);
            Image bg = Img(noteStack, "Note", null, new Color(0.05f, 0.045f, 0.04f, 0.86f));
            bg.raycastTarget = false;
            Image bar = Img(bg.transform, "Bar", null, accent);
            Place(bar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5f, 0f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            TMP_Text t = Txt(bg.transform, "Text", text, 23f, Ink, TextAlignmentOptions.MidlineLeft);
            Stretch(t.rectTransform, 20f, 8f, 14f, 8f);
            float h = t.GetPreferredValues(text, 560f - 34f, 0f).y + 18f;
            LayoutElement le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = Mathf.Max(44f, h);
            CanvasGroup cg = bg.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            notes.Add(new Note { go = bg.gameObject, cg = cg, age = -(life - 4f) });
            while (notes.Count > 5) { Destroy(notes[0].go); notes.RemoveAt(0); }
        }

        void UpdateNotes()
        {
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                Note n = notes[i];
                n.age += Time.unscaledDeltaTime;
                float a = n.age < 0f ? 1f : n.age < 0.2f ? n.age / 0.2f : n.age < 3.6f ? 1f : 1f - (n.age - 3.6f) / 0.6f;
                if (n.age < 0f && n.cg.alpha < 1f) a = Mathf.Min(1f, n.cg.alpha + Time.unscaledDeltaTime * 5f);
                n.cg.alpha = Mathf.Clamp01(a);
                if (n.age > 4.2f) { Destroy(n.go); notes.RemoveAt(i); }
            }
        }

        // ------------------------------------------------------------------ documents (notes, clues)
        void BuildDocument()
        {
            docPanel = Window("Document", "", new Vector2(1180f, 860f), out RectTransform c);
            docTitle = Txt(c, "Title", "", 40f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(docTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(940f, 56f));

            Image frame = Img(c, "Image Frame", null, new Color(0f, 0f, 0f, 0.35f));
            Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 520f));
            docImageFrame = frame.gameObject;
            RectTransform holder = NewRect("Holder", frame.transform);
            Stretch(holder, 12f, 12f, 12f, 12f);
            docImage = NewRect("Image", holder).gameObject.AddComponent<RawImage>();
            docFitter = docImage.gameObject.AddComponent<AspectRatioFitter>();
            docFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            docImage.raycastTarget = false;

            docBody = Txt(c, "Body", "", 30f, Ink, TextAlignmentOptions.Top, noteFont);
            Place(docBody.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1000f, 120f));
            docBody.enableAutoSizing = true;
            docBody.fontSizeMax = 30f;
            docBody.fontSizeMin = 18f;

            Button close = Btn(c, "CLOSE", CloseModal, new Vector2(260f, 60f));
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(260f, 60f));
        }

        public void ShowDocument(string title, Texture image, string body)
        {
            docTitle.text = title ?? "";
            docBody.text = body ?? "";
            bool hasImage = image != null;
            docImageFrame.SetActive(hasImage);
            if (hasImage)
            {
                docImage.texture = image;
                docFitter.aspectRatio = image.height > 0 ? image.width / (float)image.height : 1f;
            }
            RectTransform br = docBody.rectTransform;
            br.sizeDelta = new Vector2(1000f, hasImage ? 120f : 560f);
            br.anchoredPosition = new Vector2(0f, hasImage ? 70f : 120f);
            docBody.fontSizeMax = hasImage ? 30f : 40f;
            OpenModal(docPanel);
        }

        // ------------------------------------------------------------------ pause & settings
        void BuildPause()
        {
            pausePanel = Window("Pause", "PAUSED", new Vector2(620f, 660f), out RectTransform c, false);
            VerticalLayoutGroup v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.spacing = 18f;
            v.padding = new RectOffset(0, 0, 20, 0);
            v.childControlHeight = false;
            v.childControlWidth = false;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = false;
            Vector2 s = new Vector2(420f, 72f);
            Btn(c, "RESUME", Resume, s);
            Btn(c, "SETTINGS", OpenSettings, s);
            Btn(c, "RESTART LEVEL", () => AskResetLevel(), s);
            Btn(c, "EXIT TO MAIN MENU", () => Confirm("EXIT TO MAIN MENU?",
                "Progress since the last checkpoint will be lost.\nYour last checkpoint stays saved.", () => { if (G != null) G.ExitToMenu(); }), s);
        }

        public void Pause()
        {
            if (IsPaused || G == null) return;
            IsPaused = true;
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
            confirmPanel.transform.SetAsLastSibling();
            SetPrompt(null);
            G.ApplyControls();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            settingsPanel.SetActive(false);
            pausePanel.SetActive(false);
            IsPaused = false;
            Time.timeScale = 1f;
            if (G != null) G.ApplyControls();
        }

        public void AskResetLevel()
        {
            string lv = G != null && !string.IsNullOrEmpty(G.SceneName) ? G.SceneName : "Level 2";
            Confirm("RESTART " + lv.ToUpperInvariant() + "?",
                lv + " goes back to its original state: items, puzzles, doors, lights and the checkpoint.\nOther levels' progress is not affected.",
                () => { if (G != null) G.ResetLevel(); });
        }

        void BuildSettings()
        {
            settingsPanel = Window("Settings", "SETTINGS", new Vector2(860f, 640f), out RectTransform c, false);
            float y = -10f;
            Slider(c, "MASTER VOLUME", 0f, 1f, () => CodeBreakerSettings.MasterVolume, v => CodeBreakerSettings.MasterVolume = v, v => Mathf.RoundToInt(v * 100) + "%", ref y);
            Slider(c, "MUSIC", 0f, 1f, () => CodeBreakerSettings.MusicVolume, v => CodeBreakerSettings.MusicVolume = v, v => Mathf.RoundToInt(v * 100) + "%", ref y);
            Slider(c, "SOUND EFFECTS", 0f, 1f, () => CodeBreakerSettings.SfxVolume, v => CodeBreakerSettings.SfxVolume = v, v => Mathf.RoundToInt(v * 100) + "%", ref y);
            Slider(c, "MOUSE SENSITIVITY", CodeBreakerSettings.MinSensitivity, CodeBreakerSettings.MaxSensitivity,
                () => CodeBreakerSettings.GetMouseSensitivity(CodeBreakerSettings.DefaultSensitivity),
                v => CodeBreakerSettings.MouseSensitivity = Mathf.Round(v), v => Mathf.RoundToInt(v).ToString(), ref y);
            Button back = Btn(c, "BACK", CloseSettings, new Vector2(300f, 66f));
            Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(300f, 66f));
        }

        readonly List<System.Action> settingsRefresh = new List<System.Action>();

        void Slider(RectTransform parent, string label, float min, float max, System.Func<float> get, System.Action<float> set, System.Func<float, string> fmt, ref float y)
        {
            TMP_Text l = Txt(parent, label, label, 26f, Ink, TextAlignmentOptions.MidlineLeft, headingFont);
            Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(300f, 60f));
            TMP_Text val = Txt(parent, label + " Value", "", 24f, Gold, TextAlignmentOptions.MidlineRight, headingFont);
            Place(val.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(90f, 60f));

            RectTransform s = NewRect(label + " Slider", parent);
            Place(s, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, y - 18f), new Vector2(340f, 24f));
            Image bg = Img(s, "Background", trackSprite, trackSprite != null ? Color.white : new Color(0.2f, 0.2f, 0.2f));
            Stretch(bg.rectTransform);
            RectTransform fillArea = NewRect("Fill Area", s);
            Stretch(fillArea, 6f, 4f, 6f, 4f);
            Image fill = Img(fillArea, "Fill", fillSprite, fillSprite != null ? Color.white : Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = Vector2.zero;
            RectTransform handleArea = NewRect("Handle Area", s);
            Stretch(handleArea, 12f, 0f, 12f, 0f);
            Image handle = Img(handleArea, "Handle", handleSprite != null ? handleSprite : circleSprite, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(36f, 36f);

            UnityEngine.UI.Slider sl = s.gameObject.AddComponent<UnityEngine.UI.Slider>();
            sl.fillRect = fill.rectTransform;
            sl.handleRect = handle.rectTransform;
            sl.targetGraphic = handle;
            sl.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            sl.minValue = min;
            sl.maxValue = max;
            sl.SetValueWithoutNotify(get());
            val.text = fmt(sl.value);
            sl.onValueChanged.AddListener(v => { set(v); val.text = fmt(v); });
            settingsRefresh.Add(() => { sl.SetValueWithoutNotify(get()); val.text = fmt(sl.value); });
            y -= 96f;
        }

        void OpenSettings()
        {
            foreach (System.Action a in settingsRefresh) a();
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
            confirmPanel.transform.SetAsLastSibling();
        }

        void CloseSettings()
        {
            settingsPanel.SetActive(false);
            CodeBreakerSettings.Save();
        }

        // ------------------------------------------------------------------ confirmation dialog
        void BuildConfirm()
        {
            confirmPanel = Window("Confirm", "", new Vector2(820f, 440f), out RectTransform c, false);
            confirmTitle = Txt(c, "Title", "", 38f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(confirmTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(700f, 56f));
            confirmBody = Txt(c, "Body", "", 26f, Ink, TextAlignmentOptions.Center);
            Place(confirmBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(700f, 160f));
            Button yes = Btn(c, "YES", () => { System.Action a = confirmYes; CloseConfirm(); a?.Invoke(); }, new Vector2(240f, 66f));
            Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 0f), new Vector2(240f, 66f));
            Button no = Btn(c, "NO", CloseConfirm, new Vector2(240f, 66f));
            Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 0f), new Vector2(240f, 66f));
        }

        void Confirm(string title, string body, System.Action onYes)
        {
            confirmTitle.text = title;
            confirmBody.text = body;
            confirmYes = onYes;
            confirmPanel.SetActive(true);
            confirmPanel.transform.SetAsLastSibling();
            UIUtil.ShowCursor();
        }

        void CloseConfirm()
        {
            confirmPanel.SetActive(false);
            confirmYes = null;
            if (G != null && !IsPaused && currentModal == null && !G.Completed) G.ApplyControls();
        }

        // ------------------------------------------------------------------ completion
        void BuildCompletion()
        {
            RectTransform overlay = NewRect("Completion", root);
            Stretch(overlay);
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.8f);
            if (vignetteSprite != null)
            {
                Image vg = Img(overlay, "Vignette", vignetteSprite, new Color(0f, 0f, 0f, 0.9f));
                Stretch(vg.rectTransform);
                vg.raycastTarget = false;
            }
            Image panel = Img(overlay, "Panel", panelSprite, panelSprite != null ? Color.white : Shade);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 680f));

            TMP_Text title = Txt(panel.transform, "Title", "CONGRATULATIONS!", 76f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(960f, 100f));
            title.characterSpacing = 6f;
            if (dividerSprite != null)
            {
                Image d = Img(panel.transform, "Divider", dividerSprite, Color.white);
                Place(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(620f, 26f));
            }
            completionBody = Txt(panel.transform, "Body", "", 30f, Ink, TextAlignmentOptions.Center);
            Place(completionBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(900f, 190f));

            RectTransform row = NewRect("Buttons", panel.transform);
            Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(980f, 80f));
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            Vector2 s = new Vector2(300f, 76f);
            Btn(row, "TRY AGAIN", OnTryAgain, s);
            nextLevelButton = Btn(row, "NEXT LEVEL", () => { if (G != null) G.GoToNextLevel(); }, s, out nextLevelLabel);
            Btn(row, "EXIT BACK TO HOME", () => { if (G != null) G.ExitToMenu(); }, s);
            completionPanel = overlay.gameObject;
            completionPanel.SetActive(false);
        }

        public void ShowCompletion(bool hasNextLevel) => ShowCompletion(hasNextLevel, null, null, null);

        // body / title / noNextLabel: optional level-specific text (null = Level 2 text).
        public void ShowCompletion(bool hasNextLevel, string body, string title, string noNextLabel)
        {
            if (currentModal != null) { currentModal.SetActive(false); currentModal = null; }
            if (IsPaused) Resume();
            string time = "";
            GameFlowManager gm = GameFlowManager.Instance;
            if (gm != null && gm.levelTimer != null) time = "\nTime: " + LevelTimer.Format(gm.levelTimer.ElapsedTime);
            completionBody.text = (body ?? "You unlocked the workstation and escaped Level 2.") + time +
                "\n<size=80%><color=#9A918A>" + (hasNextLevel ? "The next level is ready." : "More levels are on the way.") + "</color></size>";
            if (body != null && !hasNextLevel) completionBody.text = body + time;
            if (!string.IsNullOrEmpty(completionExtra)) completionBody.text += completionExtra;
            Transform t = completionPanel.transform.Find("Panel/Title");
            if (t != null && t.GetComponent<TMP_Text>() != null) t.GetComponent<TMP_Text>().text = title ?? "CONGRATULATIONS!";
            nextLevelButton.interactable = hasNextLevel;
            nextLevelLabel.text = hasNextLevel ? "NEXT LEVEL" : (noNextLabel ?? "MORE LEVELS COMING SOON");
            completionPanel.SetActive(true);
            completionPanel.transform.SetAsLastSibling();
            confirmPanel.transform.SetAsLastSibling();
            SetPrompt(null);
            UIUtil.ShowCursor();
        }

        void OnTryAgain()
        {
            if (G == null) return;
            if (G.TryAgainFromCheckpoint()) return;
            string lv = string.IsNullOrEmpty(G.SceneName) ? "Level 2" : G.SceneName;
            Confirm("NO CHECKPOINT YET", "No checkpoint has been saved for " + lv + ".\nStart " + lv + " from the beginning?", () => G.StartFromBeginning());
        }
    }
}
