using UnityEngine;

namespace CodeBreaker.Level2
{
    // A predefined place where a randomized item can be hidden (baked by Code Breaker > Level 2 >
    // Setup Gameplay). The transform's position is the floor of the spot (where the item's bottom goes).
    //  - container: the drawer / cabinet door that hides it. The item can only be picked up while it is
    //    open; a drawer carries the item with it when it slides out.
    //  - onlyItem: set for an item's original, hand-made hiding place (e.g. inside the vase); only that
    //    item may use it. Empty = any item of the same room.
    public class L2HidingSpot : MonoBehaviour
    {
        public string room = "living";
        public L2Openable container;
        public string onlyItem = "";
        [Tooltip("Lay thin items (photos, cards) flat in this spot")]
        public bool layFlat = true;

        public bool Accepts(string itemId) => string.IsNullOrEmpty(onlyItem) || onlyItem == itemId;
        public bool IsOpen => container == null || container.IsOpen;

        void OnDrawGizmos()
        {
            Gizmos.color = string.IsNullOrEmpty(onlyItem) ? new Color(1f, 0.6f, 0.1f) : new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, new Vector3(0.3f, 0.2f, 0.3f));
        }
    }
}
