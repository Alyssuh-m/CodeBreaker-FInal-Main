using UnityEngine;
using TMPro;

namespace CodeBreaker.Level3
{
    // Changes the text (and colour) of a screen in the level when a station is solved.
    public class L3SetText : L3Reaction
    {
        public TMP_Text target;
        [TextArea(2, 8)] public string text;
        public Color color = new Color(0.4f, 1f, 0.55f);

        public override void Run(bool restoring)
        {
            if (target == null) return;
            target.text = text;
            target.color = color;
        }
    }
}
