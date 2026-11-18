using UnityEngine;

namespace CodeBreaker.Level2
{
    // TV remote on the coffee table. It starts without batteries: hold the AA Batteries and press E
    // on it (or drag them onto it in the inventory). After that, E opens the remote's keypad.
    public class L2Remote : L2Interactable
    {
        public L2TV tv;

        public bool HasBatteries => Game != null && Game.Flag("remote_batteries");

        public override string Prompt => HasBatteries ? "Press E to Use the Remote" : "Press E to Examine";
        public override bool WantsItem => !HasBatteries;
        public override bool AcceptsItem(string itemId) => !HasBatteries && itemId == L2Items.Batteries;

        public override bool UseItem(string itemId)
        {
            if (!AcceptsItem(itemId)) return false;
            Game.RemoveItem(L2Items.Batteries);
            Game.SetFlag("remote_batteries");
            if (Game.sfx != null) Game.sfx.Unlock(transform.position);
            Game.Notify("Batteries installed. The remote works now.", L2NoteKind.Success);
            Game.RefreshObjective();
            return true;
        }

        public override void Interact()
        {
            if (Game == null) return;
            if (!HasBatteries)
            {
                if (Game.sfx != null) Game.sfx.Failure();
                Game.Notify("The remote is dead. Its battery compartment is empty.", L2NoteKind.Warning);
                return;
            }
            if (Game.ui != null) Game.ui.OpenRemote(this);
        }
    }
}
