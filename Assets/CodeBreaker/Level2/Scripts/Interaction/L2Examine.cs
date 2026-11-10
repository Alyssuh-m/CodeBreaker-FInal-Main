using UnityEngine;

namespace CodeBreaker.Level2
{
    // Notes, clues and false leads: E opens a close-up with the object's own artwork and its text.
    // Examining never changes puzzle state (apart from an optional "read" flag used for objective hints).
    public class L2Examine : L2Interactable
    {
        public string title;
        [TextArea(2, 6)] public string body;
        [Tooltip("Close-up image (usually the prop's own texture)")]
        public Texture image;
        public string verb = "Examine";
        [Tooltip("Optional flag set the first time it is read")]
        public string flagOnRead;

        public override string Prompt => "Press E to " + verb;

        // Set the first time the note is read (used by the Notes journal).
        public string ReadFlag => "read:" + name;

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            if (g.sfx != null) g.sfx.Click();
            if (g.ui != null) g.ui.ShowDocument(string.IsNullOrEmpty(title) ? DisplayName : title, image, body);
            g.SetFlag(ReadFlag);
            if (!string.IsNullOrEmpty(flagOnRead) && !g.Flag(flagOnRead))
            {
                g.SetFlag(flagOnRead);
                g.RefreshObjective();
            }
        }
    }
}
