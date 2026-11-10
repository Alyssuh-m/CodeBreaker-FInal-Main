using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The reusable keypad puzzle (laptop 8, generator 869, symbol cabinet %*/, safe PIN 42,
    // rack count 5, central terminal 94). E opens the keypad window with the question beside it.
    // The answer is compared as text, so leading zeros and symbols work.
    //  - wrong: red flash, buzzer, time penalty; the hint appears after 2 wrong tries
    //  - solved: cannot be penalised again; its reactions run (and run again silently after loading)
    public class L3Keypad : L2Interactable, ICodeKeypad
    {
        [Header("Station")]
        public string station = "m1";
        public string title = "KEYPAD";
        [TextArea(3, 14)] public string code;
        public string answer = "8";
        public bool symbolKeys;
        public int maxLength = 4;
        [TextArea(1, 3)] public string hint;
        public float penalty = 10f;
        [TextArea(1, 3)] public string wrongMessage = "";

        [Header("Locked until")]
        public string[] requiredFlags = new string[0];
        [TextArea(1, 3)] public string lockedMessage = "It doesn't respond.";

        [Header("Solved")]
        public string successDisplay = "OK";
        [TextArea(1, 3)] public string successMessage = "";
        public string verb = "Use";
        public L3Reaction[] onSolved = new L3Reaction[0];

        [Header("World screen (optional)")]
        public TMP_Text screen;
        [TextArea(1, 6)] public string screenLocked = "";
        [TextArea(1, 6)] public string screenReady = "";
        [TextArea(1, 6)] public string screenSolved = "";

        protected L3Manager M => L3Manager.Instance;
        public bool Solved => M != null && M.Solved(station);

        public override string Prompt => Solved ? DisplayName + " — solved" : "Press E to " + verb + " " + DisplayName;

        public string KeypadTitle => title;
        public virtual string KeypadCode => code;
        public bool SymbolKeys => symbolKeys;
        public int MaxLength => maxLength;
        public string CurrentHint => M != null ? M.HintFor(station, hint) : null;

        protected virtual void Start()
        {
            if (Solved) L3Reaction.RunAll(onSolved, true);
            RefreshScreen();
        }

        public bool Unlocked
        {
            get
            {
                L2Game g = Game;
                if (g == null) return false;
                foreach (string f in requiredFlags)
                    if (!string.IsNullOrEmpty(f) && !g.Flag(f)) return false;
                return true;
            }
        }

        public virtual void RefreshScreen()
        {
            if (screen == null) return;
            string t = Solved ? screenSolved : Unlocked ? screenReady : screenLocked;
            if (!string.IsNullOrEmpty(t)) screen.text = t;
        }

        void Update()
        {
            // cheap: keeps the world screen in step with other stations (e.g. the generator waking up)
            if (screen != null && Time.frameCount % 15 == 0) RefreshScreen();
        }

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            if (Solved)
            {
                g.Notify(string.IsNullOrEmpty(successMessage) ? DisplayName + " is already solved." : successMessage, L2NoteKind.Info);
                return;
            }
            if (!Unlocked)
            {
                if (g.sfx != null) g.sfx.Locked(transform.position);
                g.Notify(lockedMessage, L2NoteKind.Warning);
                return;
            }
            if (g.sfx != null) g.sfx.Click();
            if (g.ui != null) g.ui.OpenCodeKeypad(this);
        }

        // Extra check before the answer is compared (the switch safe uses it). Return false to fail;
        // set penalize when the failure costs time.
        protected virtual bool PreCheck(out string message, out bool penalize)
        {
            message = null;
            penalize = false;
            return true;
        }

        public bool Submit(string input, out string message)
        {
            message = "";
            if (Solved) { message = successDisplay; return true; }
            if (M == null) return false;

            if (!PreCheck(out string pre, out bool penalize))
            {
                M.AddWrong(station);
                M.Penalty(penalize ? penalty : 0f);
                message = pre;
                Game.Notify(pre, L2NoteKind.Warning);
                return false;
            }

            string typed = (input ?? "").Trim();
            if (typed == answer)
            {
                M.Solve(station);
                M.Good();
                message = successDisplay;
                if (!string.IsNullOrEmpty(successMessage)) Game.Notify(successMessage, L2NoteKind.Success);
                L3Reaction.RunAll(onSolved, false);
                RefreshScreen();
                return true;
            }

            M.AddWrong(station);
            M.Penalty(penalty);
            message = string.IsNullOrEmpty(wrongMessage) ? "Wrong answer.  -" + Mathf.RoundToInt(penalty) + " s" : wrongMessage;
            return false;
        }
    }
}
