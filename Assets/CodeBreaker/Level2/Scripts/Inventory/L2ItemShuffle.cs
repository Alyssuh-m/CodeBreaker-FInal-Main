using System.Collections.Generic;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Randomized hiding places for the important free-standing items (photo pieces, batteries, star
    // figurine, UV flashlight). Puzzle rewards (the room keys) are NOT randomized: they stay behind their
    // puzzles so progression can't be bypassed.
    //
    //  - New playthrough of the level (no saved state): every item gets a random spot in its own room,
    //    never two items in the same spot. The arrangement is saved right away (level-start save).
    //  - Continue / Try Again (saved state): the exact saved arrangement is restored, never rerolled.
    //    Collected items stay collected.
    //  - The arrangement is stored as numbers in L2Game ("spot_<itemId>" = index into 'spots'), so it is
    //    part of every checkpoint save.
    //  - An item inside a closed drawer / cabinet can't be picked up; it becomes available when the
    //    container is opened (drawers carry it out with them).
    [DefaultExecutionOrder(-50)]
    public class L2ItemShuffle : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public string itemId;
            public string room = "living";
            public L2Pickup pickup;
        }

        public List<Entry> items = new List<Entry>();
        public List<L2HidingSpot> spots = new List<L2HidingSpot>();
        [Tooltip("Save the game whenever one of these items is collected (keeps Continue exact)")]
        public bool saveOnCollect = true;

        L2Game G => L2Game.Instance;
        readonly Dictionary<string, L2HidingSpot> assigned = new Dictionary<string, L2HidingSpot>();
        readonly HashSet<string> knownCollected = new HashSet<string>();
        bool ready;

        public static string Key(string itemId) => "spot_" + itemId;
        public L2HidingSpot SpotOf(string itemId) => assigned.TryGetValue(itemId, out L2HidingSpot s) ? s : null;

        void Start()
        {
            L2Game g = G;
            if (g == null) return;

            bool haveSaved = true;
            foreach (Entry e in items)
                if (e != null && e.pickup != null && !ValidSaved(e)) haveSaved = false;

            if (!haveSaved) Generate(new System.Random(System.Environment.TickCount ^ GetHashCode()));

            foreach (Entry e in items)
            {
                if (e == null || e.pickup == null) continue;
                int idx = g.GetInt(Key(e.itemId), -1);
                L2HidingSpot spot = idx >= 0 && idx < spots.Count ? spots[idx] : null;
                if (spot == null) continue;
                assigned[e.itemId] = spot;
                if (g.IsCollected(e.itemId))
                {
                    e.pickup.gameObject.SetActive(false);
                    knownCollected.Add(e.itemId);
                    continue;
                }
                Place(e.pickup.transform, spot);
            }

            g.Changed += OnChanged;
            ready = true;
            UpdateAvailability();

            // a fresh arrangement is saved at once, so Continue always gets this exact layout back
            if (g.LoadedState == null || !haveSaved) g.SaveProgress("Level start");
        }

        void OnDestroy()
        {
            if (G != null) G.Changed -= OnChanged;
        }

        bool ValidSaved(Entry e)
        {
            int idx = G.GetInt(Key(e.itemId), -1);
            return idx >= 0 && idx < spots.Count && spots[idx] != null && spots[idx].room == e.room && spots[idx].Accepts(e.itemId);
        }

        // One random spot per item, in its own room, all different.
        public void Generate(System.Random rnd)
        {
            var used = new HashSet<L2HidingSpot>();
            var order = new List<Entry>(items);
            // items with fewer options first, so nobody runs out of spots
            order.Sort((a, b) => Options(a, used).Count.CompareTo(Options(b, used).Count));
            foreach (Entry e in order)
            {
                if (e == null || e.pickup == null) continue;
                List<int> opts = Options(e, used);
                if (opts.Count == 0) { Debug.LogError("[Level 2] No free hiding spot for " + e.itemId); continue; }
                int pick = opts[rnd.Next(opts.Count)];
                used.Add(spots[pick]);
                G.SetInt(Key(e.itemId), pick);
            }
        }

        List<int> Options(Entry e, HashSet<L2HidingSpot> used)
        {
            var list = new List<int>();
            for (int i = 0; i < spots.Count; i++)
            {
                L2HidingSpot s = spots[i];
                if (s == null || used.Contains(s) || s.room != e.room || !s.Accepts(e.itemId)) continue;
                list.Add(i);
            }
            return list;
        }

        // Puts the item on the spot's floor (thin items laid flat) and lets a drawer carry it.
        void Place(Transform item, L2HidingSpot spot)
        {
            bool original = !string.IsNullOrEmpty(spot.onlyItem);
            if (original)
            {
                item.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
                return;
            }
            Bounds b = WorldBounds(item);
            if (spot.layFlat)
            {
                Vector3 s = b.size;
                float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                if (min < 0.1f && s.y > min + 0.001f)
                {
                    Vector3 axis = s.x <= s.z ? Vector3.forward : Vector3.right;   // tip the thin side down
                    item.rotation = Quaternion.AngleAxis(90f, axis) * item.rotation;
                    b = WorldBounds(item);
                }
            }
            Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            item.position += spot.transform.position + Vector3.up * 0.01f - bottom;
            if (spot.container != null && spot.container.kind == L2Openable.Kind.Drawer) spot.container.Attach(item);
        }

        static Bounds WorldBounds(Transform t)
        {
            bool any = false;
            Bounds b = new Bounds(t.position, Vector3.zero);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        void Update()
        {
            if (ready) UpdateAvailability();
        }

        // Only items in an open (or no) container can be picked up.
        void UpdateAvailability()
        {
            foreach (Entry e in items)
            {
                if (e == null || e.pickup == null || !e.pickup.gameObject.activeInHierarchy) continue;
                L2HidingSpot spot = SpotOf(e.itemId);
                bool open = spot == null || spot.IsOpen;
                if (e.pickup.enabled != open) e.pickup.enabled = open;
            }
        }

        void OnChanged()
        {
            L2Game g = G;
            if (!ready || g == null || !saveOnCollect) return;
            bool newOne = false;
            foreach (Entry e in items)
                if (e != null && g.IsCollected(e.itemId) && knownCollected.Add(e.itemId)) newOne = true;
            if (newOne) g.SaveProgress(null);
        }
    }
}
