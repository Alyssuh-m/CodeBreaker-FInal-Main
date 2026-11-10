using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CodeBreaker.Level2
{
    // Level 2 programming-puzzle screens (built on first use):
    //   - fill-in-the-blank code panel with multiple-choice answers (Grandma's recipe)
    //   - cipher wheel (Study): rotate the inner alphabet ring left / right
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

        // ================================================================== cipher wheel
        GameObject cipherPanel;
        TMP_Text cipherOuter, cipherInner, cipherShift, cipherNote;
        int cipherOffset;

        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        void BuildCipher()
        {
            if (cipherPanel != null) return;
            cipherPanel = Window("Cipher Wheel", "CIPHER WHEEL", new Vector2(1500f, 760f), out RectTransform c);
            TMP_Text l1 = Txt(c, "OuterLabel", "CODED LETTER", 22f, Dim, TextAlignmentOptions.Left, headingFont);
            Place(l1.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(400f, 30f));
            cipherOuter = Txt(c, "Outer", "", 40f, Ink, TextAlignmentOptions.Center, headingFont);
            Place(cipherOuter.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(1380f, 60f));
            cipherOuter.textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text l2 = Txt(c, "InnerLabel", "PLAIN LETTER (inner ring)", 22f, Dim, TextAlignmentOptions.Left, headingFont);
            Place(l2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -120f), new Vector2(500f, 30f));
            cipherInner = Txt(c, "Inner", "", 40f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(cipherInner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -155f), new Vector2(1380f, 60f));
            cipherInner.textWrappingMode = TextWrappingModes.NoWrap;

            Button left = Btn(c, "<  TURN", () => TurnCipher(-1), new Vector2(240f, 70f));
            Place((RectTransform)left.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-330f, -260f), new Vector2(240f, 70f));
            Button right = Btn(c, "TURN  >", () => TurnCipher(1), new Vector2(240f, 70f));
            Place((RectTransform)right.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(330f, -260f), new Vector2(240f, 70f));
            cipherShift = Txt(c, "Shift", "", 36f, Ink, TextAlignmentOptions.Center, headingFont);
            Place(cipherShift.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(380f, 70f));

            cipherNote = Txt(c, "Note", "", 28f, Ink, TextAlignmentOptions.Center);
            Place(cipherNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1300f, 150f));
        }

        public void OpenCipherWheel()
        {
            BuildCipher();
            RefreshCipher();
            OpenModal(cipherPanel);
        }

        public int CipherShift => cipherOffset;

        public void TurnCipher(int dir)
        {
            cipherOffset = (cipherOffset + dir + 26) % 26;
            RefreshCipher();
        }

        void RefreshCipher()
        {
            var outer = new System.Text.StringBuilder();
            var inner = new System.Text.StringBuilder();
            for (int i = 0; i < 26; i++)
            {
                outer.Append(Alphabet[i]).Append(' ');
                inner.Append(Alphabet[(i - cipherOffset + 26) % 26]).Append(' ');
            }
            cipherOuter.text = outer.ToString().TrimEnd();
            cipherInner.text = inner.ToString().TrimEnd();
            cipherShift.text = "shift back " + cipherOffset;
            cipherNote.text = "Each coded letter (top) stands for the letter under it (bottom).\nTurn the inner ring by the right shift, then read SHW QDPH letter by letter.";
        }
    }
}
