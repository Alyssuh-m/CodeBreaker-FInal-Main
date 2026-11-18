using UnityEngine;

namespace CodeBreaker.Level2
{
    // One drawer of the jewellery box: E opens / closes it directly.
    // Sits on a small trigger box in front of the drawer; the box script does the work (and the
    // drawer 5 puzzle check).
    public class L2JewelleryDrawer : L2Interactable
    {
        public L2JewelleryBox box;
        [Range(1, 5)] public int number = 1;

        public override string Prompt =>
            (box != null && box.IsDrawerOpen(number) ? "Press E to Close drawer[" : "Press E to Open drawer[") + L2JewelleryBox.Index(number) + "]";

        public override void Interact()
        {
            if (box != null) box.Toggle(number);
        }
    }
}
