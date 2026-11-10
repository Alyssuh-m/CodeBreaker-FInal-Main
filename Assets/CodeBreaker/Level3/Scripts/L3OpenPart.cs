using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Unlocks a locked door / lid (an L2Openable that starts disabled) and swings it open.
    // Afterwards the player can open and close it normally with E.
    public class L3OpenPart : L3Reaction
    {
        public L2Openable part;
        public bool openNow = true;

        public override void Run(bool restoring)
        {
            if (part == null) return;
            part.enabled = true;
            if (openNow && !part.IsOpen) part.Interact();
        }
    }
}
