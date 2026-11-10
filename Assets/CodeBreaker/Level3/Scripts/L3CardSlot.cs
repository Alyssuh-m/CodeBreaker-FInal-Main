using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // A keycard slot on the central terminal (red, blue or green). Only the matching card fits,
    // each card goes in once, in any order. E inserts the card straight from the inventory.
    public class L3CardSlot : L2Interactable
    {
        public string cardId = L2Items.KeycardRed;
        public string colorName = "RED";
        public string insertedFlag = "card_red_in";
        public string valueText = "m = 9";
        public GameObject insertedVisual;
        public L3Terminal terminal;

        bool Inserted => Game != null && Game.Flag(insertedFlag);

        public override bool CanInteract => base.CanInteract && !Inserted;
        public override string Prompt => Game != null && Game.HasItem(cardId)
            ? "Press E to Insert the " + L2Items.NameOf(cardId)
            : colorName + " keycard slot (empty)";

        public override bool AcceptsItem(string itemId) => !Inserted && itemId != null && itemId.StartsWith("keycard_");

        void Start()
        {
            if (insertedVisual != null) insertedVisual.SetActive(Inserted);
        }

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null || Inserted) return;
            if (g.HasItem(cardId)) { Insert(); return; }

            bool otherCard = false;
            foreach (string id in g.Inventory) if (id.StartsWith("keycard_")) otherCard = true;
            g.Notify(otherCard
                ? "That card doesn't fit the " + colorName + " slot. It needs the " + colorName.ToLowerInvariant() + " keycard."
                : "The " + colorName + " slot is empty. It needs the " + colorName.ToLowerInvariant() + " keycard.", L2NoteKind.Info);
        }

        public override bool UseItem(string itemId)
        {
            L2Game g = Game;
            if (g == null || Inserted) return false;
            if (itemId != cardId)
            {
                if (g.HasItem(cardId)) { Insert(); return true; }
                g.Notify("The " + L2Items.NameOf(itemId) + " doesn't fit the " + colorName + " slot.", L2NoteKind.Warning);
                if (g.sfx != null) g.sfx.Locked(transform.position);
                return false;
            }
            Insert();
            return true;
        }

        void Insert()
        {
            L2Game g = Game;
            g.RemoveItem(cardId);
            g.SetFlag(insertedFlag);
            if (insertedVisual != null) insertedVisual.SetActive(true);
            if (g.sfx != null) g.sfx.Unlock(transform.position);
            g.Notify(colorName + " keycard accepted:  " + valueText, L2NoteKind.Success);
            if (terminal != null) terminal.Refresh();
            g.RefreshObjective();
        }
    }
}
