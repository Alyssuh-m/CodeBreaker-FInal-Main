using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CodeBreaker.Level2
{
    // Level 2 programming-puzzle screens (built on first use):
    //   - fill-in-the-blank code panel with multiple-choice answers (Grandma's recipe)
    //   - picture wheel (Study): line up each picture from the desk note with a word from the whiteboard key
    //   - extra pause-menu buttons (HINT)
    public partial class L2UI
    {
        // ================================================================== pause menu buttons
        public void AddPauseButton(string label, System.Action onClick)
        {
            if (pausePanel == null) return;
            Transform content = pausePanel.transform.Find("Panel/Content");
            if (content == null || content.Find(label + " Button") != null) return;
            Button b = Btn(content, label, () => onClick?.Invoke(), new Vector2(420f, 72f));
            b.transform.SetSiblingIndex(1);   // right below RESUME
            FitPauseWindow(content);
        }

        // The pause window was built for 4 buttons. Each level adds its own (NOTES, HINT), so the window
        // grows to fit every button instead of letting the last one hang over the bottom edge.
        void FitPauseWindow(Transform content)
        {
            var panel = content.parent as RectTransform;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (panel == null || layout == null) return;
            float buttons = 0f; int n = 0;
            foreach (RectTransform child in content)
                if (child.gameObject.activeSelf) { buttons += child.sizeDelta.y; n++; }
            const float titleArea = 118f, bottomMargin = 40f, breathing = 34f;
            float needed = titleArea + bottomMargin + layout.padding.top + buttons + Mathf.Max(0, n - 1) * layout.spacing + breathing;
            if (needed > panel.sizeDelta.y) panel.sizeDelta = new Vector2(panel.sizeDelta.x, needed);
        }

        // ================================================================== multiple choice
        GameObject choicePanel;
        TMP_Text choiceTitle, choiceCode, choiceStatus;
        RectTransform choiceRow;
        System.Func<int, string> choiceCheck;   // returns null when correct, else the error message
        bool choiceBusy;

        void BuildChoice()
        {
            if (choicePanel != null) return;
            choicePanel = Window("Choice", "", new Vector2(1300f, 860f), out RectTransform c);
            choiceTitle = Txt(c, "Title", "", 40f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(choiceTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(1100f, 56f));
            Image scr = Img(c, "Code Screen", null, new Color(0.02f, 0.05f, 0.08f, 1f));
            Place(scr.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1150f, 440f));
            choiceCode = Txt(scr.transform, "Code", "", 34f, new Color(0.62f, 0.9f, 1f), TextAlignmentOptions.TopLeft);
            Stretch(choiceCode.rectTransform, 34f, 24f, 34f, 24f);
            choiceCode.enableAutoSizing = true;
            choiceCode.fontSizeMax = 34f;
            choiceCode.fontSizeMin = 16f;
            choiceRow = NewRect("Options", c);
            Place(choiceRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1150f, 80f));
            choiceStatus = Txt(c, "Status", "", 28f, Ink, TextAlignmentOptions.Center);
            Place(choiceStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1150f, 60f));
        }

        // check(index) returns null when the choice is right, otherwise the message to show.
        public void OpenChoice(string title, string code, string[] options, System.Func<int, string> check, string intro = "Choose the missing piece of code.")
        {
            BuildChoice();
            for (int i = choiceRow.childCount - 1; i >= 0; i--) Destroy(choiceRow.GetChild(i).gameObject);
            float w = 300f, gap = 30f;
            float x0 = -(options.Length - 1) * (w + gap) * 0.5f;
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                Button b = Btn(choiceRow, options[i], () => PickChoice(k), new Vector2(w, 76f), 32f);
                b.name = "Option " + options[i];
                Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0 + i * (w + gap), 0f), new Vector2(w, 76f));
            }
            choiceTitle.text = title;
            choiceCode.text = code;
            choiceStatus.text = intro;
            choiceCheck = check;
            choiceBusy = false;
            OpenModal(choicePanel, () => { choiceCheck = null; });
        }

        public bool ChoiceOpen => choicePanel != null && currentModal == choicePanel;

        // Also used by automated tests.
        public void PickChoice(int index)
        {
            if (choiceBusy || choiceCheck == null) return;
            string error = choiceCheck(index);
            if (error == null) StartCoroutine(ChoiceCorrect());
            else
            {
                if (G != null && G.sfx != null) G.sfx.Failure();
                choiceStatus.text = "<color=#F05A52>" + error + "</color>";
            }
        }

        IEnumerator ChoiceCorrect()
        {
            choiceBusy = true;
            if (G != null && G.sfx != null) G.sfx.Success();
            choiceStatus.text = "<color=#5FD383>Compiled successfully!</color>";
            yield return new WaitForSecondsRealtime(1.1f);
            choiceBusy = false;
            if (ChoiceOpen) CloseModal();
        }

        // ================================================================== picture wheel (Study)
        // The desk note is written in pictures: PASSWORD = [heart] + [calendar],  [heart] = [paw].
        // The whiteboard is the key. Each row turns through the six words from the key; a tick shows a
        // correct match. All three right = note decoded (flag "picture_decoded").
        GameObject cipherPanel;
        TMP_Text cipherNote;
        readonly TMP_Text[] wheelWord = new TMP_Text[3];
        readonly GameObject[] wheelTick = new GameObject[3];
        readonly int[] wheelSel = { 1, 0, 2 };

        public static readonly string[] PictureWords = { "who I love", "my street", "lucky number", "my dog", "my best year", "my coffee" };
        static readonly string[] PictureRows = { "heart", "paw", "calendar" };
        static readonly int[] PictureAnswer = { 0, 3, 4 };

        static Texture PicTex(string id) => Resources.Load<Texture2D>("PictureKey/" + id);

        RawImage RawImg(Transform parent, string name, Texture tex, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            RawImage r = NewRect(name, parent).gameObject.AddComponent<RawImage>();
            r.texture = tex;
            r.raycastTarget = false;
            if (tex == null) r.color = new Color(1f, 1f, 1f, 0f);
            Place(r.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, size);
            return r;
        }

        void BuildCipher()
        {
            if (cipherPanel != null) return;
            cipherPanel = Window("Picture Wheel", "PICTURE WHEEL", new Vector2(1500f, 820f), out RectTransform c);

            // the note itself (left)
            Texture note = PicTex("L2P_T_Note_Picture");
            Image frame = Img(c, "Note Frame", null, new Color(0f, 0f, 0f, 0.35f));
            Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(440f, 340f));
            RawImg(frame.transform, "Note", note, Vector2.zero, new Vector2(420f, 315f), new Vector2(0.5f, 0.5f));
            TMP_Text nl = Txt(c, "NoteLabel", "THE NOTE ON THE DESK", 20f, Dim, TextAlignmentOptions.Center, headingFont);
            Place(nl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -356f), new Vector2(440f, 30f));

            // one row per picture (right)
            for (int i = 0; i < 3; i++)
            {
                int row = i;
                float y = -70f - i * 130f;
                Image bg = Img(c, "Row " + i, null, new Color(1f, 1f, 1f, 0.05f));
                Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, y + 55f), new Vector2(880f, 116f));
                RawImg(bg.transform, "Picture", PicTex("icon_" + PictureRows[i]), new Vector2(70f, 0f), new Vector2(96f, 96f), new Vector2(0f, 0.5f));
                TMP_Text eq = Txt(bg.transform, "Equals", "=", 44f, Ink, TextAlignmentOptions.Center, headingFont);
                Place(eq.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 0f), new Vector2(50f, 70f));
                Button l = Btn(bg.transform, "<", () => TurnWheel(row, -1), new Vector2(80f, 76f), 36f);
                Place((RectTransform)l.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, 0f), new Vector2(80f, 76f));
                wheelWord[i] = Txt(bg.transform, "Word", "", 36f, Gold, TextAlignmentOptions.Center, headingFont);
                Place(wheelWord[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460f, 0f), new Vector2(360f, 76f));
                Button r = Btn(bg.transform, ">", () => TurnWheel(row, 1), new Vector2(80f, 76f), 36f);
                Place((RectTransform)r.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(690f, 0f), new Vector2(80f, 76f));
                wheelTick[i] = RawImg(bg.transform, "Tick", PicTex("icon_tick"), new Vector2(800f, 0f), new Vector2(70f, 70f), new Vector2(0f, 0.5f)).gameObject;
            }

            cipherNote = Txt(c, "Note Text", "", 28f, Ink, TextAlignmentOptions.Center);
            Place(cipherNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1380f, 150f));
            cipherNote.enableAutoSizing = true;
            cipherNote.fontSizeMax = 28f;
            cipherNote.fontSizeMin = 18f;
        }

        public void OpenCipherWheel()
        {
            BuildCipher();
            if (PictureDecoded) for (int i = 0; i < 3; i++) wheelSel[i] = PictureAnswer[i];
            RefreshCipher();
            OpenModal(cipherPanel);
        }

        bool PictureDecoded => G != null && G.Flag("picture_decoded");

        public bool PictureRowCorrect(int row) => wheelSel[row] == PictureAnswer[row];

        // Also used by automated tests.
        public void TurnWheel(int row, int dir)
        {
            if (row < 0 || row > 2 || PictureDecoded) return;
            wheelSel[row] = (wheelSel[row] + dir + PictureWords.Length) % PictureWords.Length;
            RefreshCipher();
            if (PictureRowCorrect(0) && PictureRowCorrect(1) && PictureRowCorrect(2) && G != null)
            {
                G.SetFlag("picture_decoded");
                if (G.sfx != null) G.sfx.Success();
                G.Notify("Picture note decoded: PASSWORD = who I love + my best year. Who I love = my dog.", L2NoteKind.Success);
                G.RefreshObjective();
                RefreshCipher();
            }
        }

        void RefreshCipher()
        {
            int right = 0;
            for (int i = 0; i < 3; i++)
            {
                bool ok = PictureRowCorrect(i);
                if (ok) right++;
                wheelWord[i].text = PictureWords[wheelSel[i]];
                wheelWord[i].color = ok ? Good : Gold;
                wheelTick[i].SetActive(ok);
            }
            if (PictureDecoded)
                cipherNote.text = "<color=#5FD383>DECODED</color>\nPASSWORD = who I love + my best year     ·     who I love = my dog";
            else
                cipherNote.text = "Use the picture key on the whiteboard. Turn each word until it matches its picture.\n<color=#9A918A>" + right + " of 3 matched</color>";
        }
    }
}
