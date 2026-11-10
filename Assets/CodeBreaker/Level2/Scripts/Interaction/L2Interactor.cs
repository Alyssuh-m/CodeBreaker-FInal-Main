using UnityEngine;

namespace CodeBreaker.Level2
{
    // On the player camera. Every frame it casts a ray from the centre of the screen and finds the
    // interactable the player is looking at, shows the prompt, and handles E (interact / use held
    // item) and F (pick up).
    //
    // What blocks the ray: walls, door jambs, floors and door leaves. Furniture colliders do NOT
    // block it, so props standing on shelves, inside drawers or under the bed can still be reached
    // (their own trigger colliders are what the ray hits).
    public class L2Interactor : MonoBehaviour
    {
        [Tooltip("Interaction distance in Unity units (1 m = 3.75 units)")]
        public float range = 9.5f;

        public L2Interactable Current { get; private set; }

        readonly RaycastHit[] hits = new RaycastHit[48];

        public static bool IsBlocker(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            string n = c.name;
            return n.Contains("Wall") || n.Contains("Jamb") || n.Contains("Floor") || n.Contains("Ceiling") || n == "DoorLeaf";
        }

        void Update()
        {
            L2Game g = L2Game.Instance;
            if (g == null) return;

            if (g.InteractionsBlocked)
            {
                SetCurrent(null, g);
                return;
            }

            L2Interactable target = FindTarget(g);
            SetCurrent(target, g);
            if (target == null) return;

            string held = g.Held;
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (held != null && target.AcceptsItem(held))
                {
                    target.UseItem(held);
                }
                else if (held != null && target.WantsItem)
                {
                    g.Notify("The " + L2Items.NameOf(held) + " doesn't work here.", L2NoteKind.Warning);
                    if (g.sfx != null) g.sfx.Failure();
                }
                else if (target.UsesPickupKey)
                {
                    g.Notify("Press F to pick it up.", L2NoteKind.Info);
                }
                else
                {
                    target.Interact();
                }
            }
            else if (Input.GetKeyDown(KeyCode.F) && target.UsesPickupKey)
            {
                target.Interact();
            }
        }

        void SetCurrent(L2Interactable t, L2Game g)
        {
            Current = t;
            if (g.ui == null) return;
            if (t == null) { g.ui.SetPrompt(null); return; }
            string held = g.Held;
            g.ui.SetPrompt(held != null && t.AcceptsItem(held) ? "Press E to Use " + L2Items.NameOf(held) : t.Prompt);
        }

        // The nearest usable interactable along the view ray that isn't hidden behind a wall.
        // When a container's trigger encloses a pick-up (e.g. a key in an open drawer) the pick-up wins.
        L2Interactable FindTarget(L2Game g)
        {
            var ray = new Ray(transform.position, transform.forward);
            int n = Physics.RaycastNonAlloc(ray, hits, range, ~0, QueryTriggerInteraction.Collide);
            if (n <= 0) return null;
            System.Array.Sort(hits, 0, n, Comparer.Instance);

            L2Interactable best = null;
            float bestDist = 0f;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (g.player != null && c.transform.IsChildOf(g.player)) continue;

                L2Interactable it = c.GetComponentInParent<L2Interactable>();
                if (it != null && it.CanInteract)
                {
                    if (best == null)
                    {
                        best = it;
                        bestDist = hits[i].distance;
                        if (it.UsesPickupKey) return best;
                        continue;
                    }
                    if (it != best && it.UsesPickupKey && hits[i].distance - bestDist < 1.2f) return it;
                    continue;
                }
                if (IsBlocker(c)) break;
                if (best != null && hits[i].distance - bestDist > 1.2f) break;
            }
            return best;
        }

        class Comparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly Comparer Instance = new Comparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        // Is the point visible from the camera (no wall / door / floor in between)?
        public static bool HasLineOfSight(Vector3 from, Vector3 to, Transform ignoreRoot)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.01f) return true;
            RaycastHit[] all = Physics.RaycastAll(from, d / dist, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in all)
            {
                if (ignoreRoot != null && h.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (IsBlocker(h.collider)) return false;
            }
            return true;
        }
    }
}
