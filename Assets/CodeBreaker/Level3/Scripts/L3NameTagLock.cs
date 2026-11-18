using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The bolted red locker: "Only a name C# will accept can open me."
    // Hold a name tag and press E on the locker. fanSpeed opens it (value 6); any other tag shows
    // "INVALID NAME", goes back to its hook on the pegboard and costs time.
    public class L3NameTagLock : L2Interactable
    {
        public string station = "m2";
        public string correctTag = L2Items.TagFanSpeed;
        public TMP_Text screen;
        [TextArea(1, 3)] public string hint = "No starting digit, no spaces, no C# keywords.";
        public float penalty = 10f;
        public L3Reaction[] onSolved = new L3Reaction[0];

        L3Manager M => L3Manager.Instance;
        bool Solved => M != null && M.Solved(station);

        public override bool CanInteract => base.CanInteract && !Solved;
        public override string Prompt => "Press E to Read the Locker Note";
        public override bool AcceptsItem(string itemId) => !Solved && itemId != null && itemId.StartsWith("tag_");
        public override bool WantsItem => false;

        void Start()
        {
            if (Solved) L3Reaction.RunAll(onSolved, true);
            SetScreen(Solved ? "VALID NAME\nfanSpeed = 6" : "LOCKED\nINSERT NAME TAG", Solved ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.75f, 0.3f));
        }

        void SetScreen(string t, Color c)
        {
            if (screen == null) return;
            screen.text = t;
            screen.color = c;
        }

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            if (g.sfx != null) g.sfx.Click();
            bool hasTag = false;
            foreach (string id in g.Inventory) if (id.StartsWith("tag_")) hasTag = true;
            string body = "The red locker is bolted shut. A note is taped to it:\n\n\"Only a name C# will accept can open me.\"";
            if (hasTag) body += "\n\n<size=80%><color=#9A918A>Hold a name tag (V, then Equip) and press E on the locker.</color></size>";
            string h = M != null ? M.HintFor(station, hint) : null;
            if (h != null) body += "\n\n<size=80%><color=#E8C547>HINT: " + h + "</color></size>";
            if (g.ui != null) g.ui.ShowDocument("RED LOCKER", null, body);
        }

        public override bool UseItem(string itemId)
        {
            L2Game g = Game;
            if (g == null || M == null || Solved) return false;
            if (itemId == correctTag)
            {
                g.RemoveItem(itemId);
                M.Solve(station);
                M.Good();
                SetScreen("VALID NAME\nfanSpeed = 6", new Color(0.4f, 1f, 0.5f));
                g.Notify("fanSpeed is a valid C# name. The red locker unbolts (value 6).", L2NoteKind.Success);
                L3Reaction.RunAll(onSolved, false);
                return true;
            }

            SetScreen("INVALID NAME", new Color(1f, 0.3f, 0.25f));
            string tagName = L2Items.NameOf(itemId);
            g.ReturnItem(itemId);
            M.AddWrong(station);
            M.Penalty(penalty);
            g.Notify("INVALID NAME. The " + tagName + " snaps back to its hook.", L2NoteKind.Warning);
            M.ShowHintIfReady(station, hint);
            CancelInvoke(nameof(ResetScreen));
            Invoke(nameof(ResetScreen), 2.5f);
            return false;
        }

        void ResetScreen()
        {
            if (!Solved) SetScreen("LOCKED\nINSERT NAME TAG", new Color(1f, 0.75f, 0.3f));
        }
    }
}
