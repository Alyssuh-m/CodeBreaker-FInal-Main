using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // One socket of the fuse box (x, y or temp). E takes the fuse out; holding a fuse and pressing E
    // on an empty socket puts it in.
    public class L3FuseSocket : L2Interactable
    {
        public string label = "x";
        public Transform anchor;
        [HideInInspector] public L3FuseBox box;
        [HideInInspector] public int index;

        int Content => box != null ? box.Get(index) : 0;

        public override bool CanInteract => base.CanInteract && box != null && !box.Solved;

        public override string Prompt => Content != 0
            ? "Press E to Take Fuse " + Content + " from socket " + label
            : "Socket " + label + " (empty)";

        public override bool AcceptsItem(string itemId) =>
            box != null && !box.Solved && (itemId == L2Items.Fuse2 || itemId == L2Items.Fuse9);

        public override void Interact()
        {
            if (box == null) return;
            if (Content != 0) box.Take(index);
            else if (Game != null)
                Game.Notify(box.CarryingFuse
                    ? "Hold the fuse (V, then Equip) and press E on this socket to put it in."
                    : "Socket " + label + " is empty.", L2NoteKind.Info);
        }

        public override bool UseItem(string itemId) => box != null && box.Place(index, itemId);
    }
}
