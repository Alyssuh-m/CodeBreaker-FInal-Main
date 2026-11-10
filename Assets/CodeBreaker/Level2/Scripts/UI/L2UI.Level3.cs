using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CodeBreaker.Level2
{
    // Extra screens used by Level 3 (built on first use, so Level 2 never creates them):
    //   - a reusable code keypad (digits 0-9 or the operator keys + - * / %), with the question / code
    //     shown next to it, CLR / DEL / OK, a status line and a hint line;
    //   - a red full-screen flash for wrong answers;
    //   - a countdown clock (when the level runs its own timer) and a time-up screen;
    //   - a small "screen" window for terminals that only show text.
    public partial class L2UI
    {
        // ================================================================== code keypad
        GameObject codePanel;
        TMP_Text codeTitle, codeQuestion, codeDisplay, codeStatus, codeHint;
        RectTransform codeKeys;
        ICodeKeypad codeTarget;
        string codeEntry = "";
        bool codeBusy;
        bool codeSymbols;

        public bool CodeKeypadOpen => codePanel != null && currentModal == codePanel;
        public string CodeEntry => codeEntry;

        void BuildCodeKeypad()
        {
            if (codePanel != null) return;
            codePanel = Window("Code Keypad", "", new Vector2(1500f, 900f), out RectTransform c);

            codeTitle = Txt(c, "Title", "", 40f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(codeTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(1200f, 56f));
            codeTitle.characterSpacing = 4f;

            // left: the code / question on a dark terminal screen
            Image scr = Img(c, "Question Screen", null, new Color(0.02f, 0.05f, 0.08f, 1f));
            Place(scr.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -70f), new Vector2(820f, 640f));
            codeQuestion = Txt(scr.transform, "Code", "", 30f, new Color(0.62f, 0.9f, 1f), TextAlignmentOptions.TopLeft);
            Stretch(codeQuestion.rectTransform, 30f, 24f, 30f, 24f);
            codeQuestion.enableAutoSizing = true;
            codeQuestion.fontSizeMax = 32f;
            codeQuestion.fontSizeMin = 16f;
            codeQuestion.richText = true;

            // right: display + keys
            Image disp = Img(c, "Display", null, new Color(0.02f, 0.02f, 0.02f, 1f));
            Place(disp.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(520f, 110f));
            codeDisplay = Txt(disp.transform, "Text", "", 64f, new Color(0.3f, 1f, 0.45f), TextAlignmentOptions.Center, headingFont);
            Stretch(codeDisplay.rectTransform, 10f, 0f, 10f, 0f);
            codeDisplay.characterSpacing = 8f;
            codeDisplay.textWrappingMode = TextWrappingModes.NoWrap;
            codeDisplay.enableAutoSizing = true;
            codeDisplay.fontSizeMax = 64f;
            codeDisplay.fontSizeMin = 24f;

            codeKeys = NewRect("Keys", c);
            Place(codeKeys, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -196f), new Vector2(520f, 440f));

            codeStatus = Txt(c, "Status", "", 26f, Ink, TextAlignmentOptions.Center);
            Place(codeStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(1300f, 44f));
            codeHint = Txt(c, "Hint", "", 24f, Gold, TextAlignmentOptions.Center);
            Place(codeHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(1300f, 44f));
        }

        void BuildCodeKeys(bool symbols)
        {
            for (int i = codeKeys.childCount - 1; i >= 0; i--) Destroy(codeKeys.GetChild(i).gameObject);
            string[] keys = symbols
                ? new[] { "+", "-", "*", "/", "%", "DEL", "CLR", "", "OK" }
                : new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "OK", "", "DEL", "" };
            int cols = 3;
            float w = 150f, h = symbols ? 120f : 76f, gx = 170f, gy = symbols ? 140f : 88f;
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                if (k == "") continue;
                Button b = Btn(codeKeys, k, () => PressCodeKey(k), new Vector2(w, h), symbols && k.Length == 1 ? 56f : 36f);
                b.name = "Key " + k;
                int col = i % cols, row = i / cols;
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((col - 1) * gx, -row * gy), new Vector2(w, h));
            }
        }

        public void OpenCodeKeypad(ICodeKeypad target)
        {
            if (target == null) return;
            BuildCodeKeypad();
            if (codeSymbols != target.SymbolKeys || codeKeys.childCount == 0)
            {
                codeSymbols = target.SymbolKeys;
                BuildCodeKeys(codeSymbols);
            }
            codeTarget = target;
            codeEntry = "";
            codeBusy = false;
            codeTitle.text = target.KeypadTitle;
            codeQuestion.text = target.KeypadCode;
            codeStatus.text = target.SymbolKeys ? "Enter the missing operators in order, then OK." : "Enter the answer, then OK.";
            string hint = target.CurrentHint;
            codeHint.text = string.IsNullOrEmpty(hint) ? "" : "HINT: " + hint;
            RefreshCodeDisplay();
            OpenModal(codePanel, () => { codeTarget = null; });
        }

        void RefreshCodeDisplay()
        {
            codeDisplay.color = new Color(0.3f, 1f, 0.45f);
            int max = codeTarget != null ? Mathf.Max(1, codeTarget.MaxLength) : 4;
            codeDisplay.text = codeEntry.Length == 0 ? new string('_', Mathf.Min(max, 6)) : codeEntry;
        }

        // Also used by automated tests.
        public void PressCodeKey(string k)
        {
            if (codeBusy || codeTarget == null) return;
            if (G != null && G.sfx != null) G.sfx.Beep();
            if (k == "CLR") { codeEntry = ""; codeStatus.text = "Cleared."; RefreshCodeDisplay(); return; }
            if (k == "DEL") { if (codeEntry.Length > 0) codeEntry = codeEntry.Substring(0, codeEntry.Length - 1); RefreshCodeDisplay(); return; }
            if (k == "OK")
            {
                if (codeEntry.Length == 0) { codeStatus.text = "Type something first."; return; }
                StartCoroutine(SubmitCode());
                return;
            }
            if (codeEntry.Length < Mathf.Max(1, codeTarget.MaxLength)) codeEntry += k;
            RefreshCodeDisplay();
        }

        IEnumerator SubmitCode()
        {
            codeBusy = true;
            ICodeKeypad t = codeTarget;
            bool ok = t.Submit(codeEntry, out string message);
            if (ok)
            {
                codeDisplay.text = string.IsNullOrEmpty(message) ? "OK" : message;
                codeStatus.text = "<color=#5FD383>Correct!</color>";
                codeHint.text = "";
                yield return new WaitForSecondsRealtime(1.1f);
                codeBusy = false;
                if (currentModal == codePanel) CloseModal();
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    codeDisplay.color = Bad;
                    codeDisplay.text = "ERROR";
                    yield return new WaitForSecondsRealtime(0.16f);
                    codeDisplay.text = "";
                    yield return new WaitForSecondsRealtime(0.08f);
                }
                codeStatus.text = "<color=#F05A52>" + (string.IsNullOrEmpty(message) ? "Wrong answer." : message) + "</color>";
                string hint = t.CurrentHint;
                codeHint.text = string.IsNullOrEmpty(hint) ? "" : "HINT: " + hint;
                codeEntry = "";
                codeBusy = false;
                if (codeTarget != null) RefreshCodeDisplay();
            }
        }

        // ================================================================== info screen (terminal text, monitors)
        GameObject infoPanel;
        TMP_Text infoTitle, infoBody;

        void BuildInfo()
        {
            if (infoPanel != null) return;
            infoPanel = Window("Info Screen", "", new Vector2(1300f, 820f), out RectTransform c);
            infoTitle = Txt(c, "Title", "", 40f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(infoTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(1100f, 56f));
            Image scr = Img(c, "Screen", null, new Color(0.02f, 0.05f, 0.08f, 1f));
            Stretch(scr.rectTransform, 0f, 80f, 0f, 70f);
            infoBody = Txt(scr.transform, "Body", "", 32f, new Color(0.62f, 0.9f, 1f), TextAlignmentOptions.TopLeft);
            Stretch(infoBody.rectTransform, 30f, 24f, 30f, 24f);
            infoBody.enableAutoSizing = true;
            infoBody.fontSizeMax = 34f;
            infoBody.fontSizeMin = 16f;
            Button close = Btn(c, "CLOSE", CloseModal, new Vector2(260f, 60f));
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(260f, 60f));
        }

        public void ShowInfo(string title, string body)
        {
            BuildInfo();
            infoTitle.text = title ?? "";
            infoBody.text = body ?? "";
            OpenModal(infoPanel);
        }

        // ================================================================== red flash
        Image flashImage;
        float flashTime;
        Color flashColor;

        public void Flash(Color c, float duration = 0.6f)
        {
            if (flashImage == null)
            {
                flashImage = Img(root, "Screen Flash", null, Color.clear);
                Stretch(flashImage.rectTransform);
                flashImage.raycastTarget = false;
            }
            flashImage.transform.SetAsLastSibling();
            flashColor = c;
            flashTime = duration;
            StartCoroutine(FlashRoutine(duration));
        }

        public void FlashRed() => Flash(new Color(0.85f, 0.05f, 0.05f, 0.45f));

        IEnumerator FlashRoutine(float duration)
        {
            float t = duration;
            while (t > 0f && flashImage != null)
            {
                t -= Time.unscaledDeltaTime;
                Color c = flashColor;
                c.a *= Mathf.Clamp01(t / duration);
                flashImage.color = c;
                yield return null;
            }
            if (flashImage != null) flashImage.color = Color.clear;
        }

        // ================================================================== level clock (own countdown)
        TMP_Text clockText;
        Image clockBg;

        public void SetClock(string text, bool warning, float pulse = 1f)
        {
            if (clockText == null)
            {
                clockBg = Img(root, "Level Clock", null, new Color(0f, 0f, 0f, 0.5f));
                Place(clockBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(230f, 70f));
                clockBg.raycastTarget = false;
                clockText = Txt(clockBg.transform, "Text", "", 48f, Ink, TextAlignmentOptions.Center, headingFont);
                Stretch(clockText.rectTransform);
                clockText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            clockBg.gameObject.SetActive(text != null);
            if (text == null) return;
            clockText.text = text;
            clockText.color = warning ? Bad : Ink;
            clockText.rectTransform.localScale = Vector3.one * pulse;
        }

        // A short "-10s" popup under the clock (or top centre).
        public void ShowPenalty(string text)
        {
            TMP_Text t = Txt(root, "Penalty", text, 40f, Bad, TextAlignmentOptions.Center, headingFont);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(300f, 60f));
            StartCoroutine(FadeAndDestroy(t, 1.4f));
        }

        IEnumerator FadeAndDestroy(TMP_Text t, float life)
        {
            float a = life;
            Vector2 p = t.rectTransform.anchoredPosition;
            while (a > 0f && t != null)
            {
                a -= Time.unscaledDeltaTime;
                t.alpha = Mathf.Clamp01(a / life * 1.5f);
                t.rectTransform.anchoredPosition = p + new Vector2(0f, -(life - a) * 30f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        // ================================================================== time up (own countdown)
        GameObject timeUpPanel;

        public void ShowTimeUp(string body)
        {
            if (currentModal != null) { currentModal.SetActive(false); currentModal = null; }
            if (IsPaused) Resume();
            if (timeUpPanel == null)
            {
                RectTransform overlay = NewRect("Time Up", root);
                Stretch(overlay);
                overlay.gameObject.AddComponent<Image>().color = new Color(0.12f, 0f, 0f, 0.85f);
                Image panel = Img(overlay, "Panel", panelSprite, panelSprite != null ? Color.white : Shade);
                Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f));
                TMP_Text title = Txt(panel.transform, "Title", "TIME'S UP", 80f, Bad, TextAlignmentOptions.Center, headingFont);
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 100f));
                TMP_Text b = Txt(panel.transform, "Body", "", 30f, Ink, TextAlignmentOptions.Center);
                Place(b.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(860f, 170f));
                RectTransform row = NewRect("Buttons", panel.transform);
                Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(940f, 80f));
                HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 24f;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = false;
                h.childControlHeight = false;
                h.childForceExpandWidth = false;
                Vector2 s = new Vector2(290f, 76f);
                Btn(row, "TRY AGAIN", OnTryAgain, s);
                Btn(row, "RESTART LEVEL", () => { if (G != null) G.ResetLevel(); }, s);
                Btn(row, "EXIT TO HOME", () => { if (G != null) G.ExitToMenu(); }, s);
                timeUpPanel = overlay.gameObject;
            }
            timeUpPanel.transform.Find("Panel/Body").GetComponent<TMP_Text>().text = body;
            timeUpPanel.SetActive(true);
            timeUpPanel.transform.SetAsLastSibling();
            confirmPanel.transform.SetAsLastSibling();
            SetPrompt(null);
            UIUtil.ShowCursor();
        }
    }
}
