using UnityEngine;

namespace CodeBreaker.Level2
{
    // Base class for everything the player can look at and use in Level 2.
    // The object (or one of its children) needs a collider; trigger colliders are fine and
    // don't block the player. L2Interactor on the camera finds it with a raycast.
    public abstract class L2Interactable : MonoBehaviour
    {
        [Tooltip("Name used in prompts and messages")]
        public string displayName = "";

        protected L2Game Game => L2Game.Instance;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        // Can the player use this right now?
        public virtual bool CanInteract => isActiveAndEnabled;

        // Pick-ups use F, everything else uses E.
        public virtual bool UsesPickupKey => false;

        // Text shown while the player looks at it.
        public virtual string Prompt => "Press E to Interact";

        public virtual void Interact() { }

        // Item use (held item + E, or dragging an inventory item onto this object's drop target).
        public virtual bool AcceptsItem(string itemId) => false;

        // True while this object is waiting for an item: a wrong held item then gets a
        // "doesn't work here" message instead of the normal interaction.
        public virtual bool WantsItem => false;

        // Returns true when the item was used (the puzzle state changed).
        public virtual bool UseItem(string itemId) => false;
    }
}
