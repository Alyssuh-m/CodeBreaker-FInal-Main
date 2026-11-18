using UnityEngine;

namespace CodeBreaker.Level2
{
    // An item the player collects with F. It disappears from the world and goes into the inventory.
    // Unique: once collected (also after loading a checkpoint) it never appears again, unless a puzzle
    // explicitly returns it (L2Game.ReturnItem puts it back where it was found).
    public class L2Pickup : L2Interactable
    {
        [Tooltip("Item id from L2Items")]
        public string itemId;
        [Tooltip("Optional flag set when collected")]
        public string flagOnCollect;

        Vector3 homePos;
        Quaternion homeRot;
        Transform homeParent;
        bool homeSet;

        public override bool UsesPickupKey => true;
        public override string Prompt => "Press F to Pick Up " + L2Items.NameOf(itemId);

        void Awake() => RememberHome();

        void RememberHome()
        {
            if (homeSet) return;
            homeSet = true;
            homeParent = transform.parent;
            homePos = transform.localPosition;
            homeRot = transform.localRotation;
        }

        void Start()
        {
            if (Game != null && Game.IsCollected(itemId)) gameObject.SetActive(false);
        }

        public override void Interact()
        {
            if (Game != null) Game.Collect(this);
        }

        // Back to where it was found, visible again.
        public void ReturnHome()
        {
            RememberHome();
            transform.SetParent(homeParent, false);
            transform.localPosition = homePos;
            transform.localRotation = homeRot;
            gameObject.SetActive(true);
        }
    }
}
