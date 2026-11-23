using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // A drawer, cabinet door or closet door on the existing furniture models. E opens / closes it
    // directly with a short smooth animation (no extra window). Each one works on its own.
    //  - Drawer: slides out along 'slide' (world space) and back.
    //  - Door:   swings 'openAngle' degrees around its own pivot, which in these models is the hinge.
    // Objects stuck on the part (e.g. notes and magnets on the fridge doors) move with it.
    // Open / closed is not saved: everything starts closed.
    // Collision: the part gets a solid box collider that matches its model and moves with it, so the
    // player can't walk through an open (or opening) door, drawer, fridge or pantry door. While it
    // moves it pushes the player out of its way (DoorLeafPush); if the player is pinned against
    // something it waits for that frame instead of passing through them.
    // It never passes through other things either: 'maxOpen' is how far it can open before it would
    // touch a wall, the bed or other furniture (measured in the editor by Code Breaker > Fix Pass >
    // Fix Props, which sweeps the part along its opening path against the meshes around it).
    public class L2Openable : L2Interactable
    {
        public enum Kind { Drawer, Door }

        public Kind kind = Kind.Drawer;
        [Tooltip("Word used in the prompt, e.g. Drawer, Cabinet, Wardrobe, Pantry, Fridge")]
        public string label = "Drawer";
        [Tooltip("Drawer: how far and which way it slides out (world space)")]
        public Vector3 slide = new Vector3(0f, 0f, -1f);
        [Tooltip("Door: swing angle around the vertical axis through the pivot (sign = opening direction)")]
        public float openAngle = 100f;
        public float duration = 0.45f;
        [Tooltip("Separate objects stuck on this part that should move with it")]
        public Transform[] attached = new Transform[0];
        [Range(0f, 1f)]
        [Tooltip("How far it opens (1 = the full slide / openAngle). Lower when something is in the way.")]
        public float maxOpen = 1f;
        [Tooltip("Door: where the hinge line is, relative to the pivot when closed (world space). Zero = turn around the pivot. " +
                 "Set to the front edge so the door's thickness doesn't swing into the cabinet next to it.")]
        public Vector3 hingeOffset = Vector3.zero;

        // Door pose at opening k (0..1) from the closed pose; used by the game and the editor tools.
        public Vector3 DoorPosition(Vector3 p0, float k) => p0 + hingeOffset - Quaternion.AngleAxis(openAngle * k, Vector3.up) * hingeOffset;

        public bool IsOpen { get; private set; }

        Vector3 closedPos;
        Quaternion closedRot;
        Vector3[] attPos;
        Quaternion[] attRot;
        float amount;          // 0 = closed, 1 = open
        Coroutine anim;

        public override string Prompt => (IsOpen ? "Press E to Close " : "Press E to Open ") + label + (!IsOpen && Searched ? "  (searched)" : "");

        // ---- "Empty" feedback for hiding places (drawers / cabinets that can hold a random item)
        // Opening one with nothing (left) inside says "Empty." and marks it as searched (saved); the
        // prompt then shows "(searched)". It always closes completely.
        bool? isHidingContainer;
        public bool Searched => Game != null && Game.Flag(SearchedFlag);
        string SearchedFlag => "searched:" + name + "@" + Mathf.RoundToInt(closedPos.x * 10f) + "," + Mathf.RoundToInt(closedPos.z * 10f);

        BoxCollider solid;
        CharacterController playerCC;
        readonly System.Collections.Generic.List<Collider> solidList = new System.Collections.Generic.List<Collider>();

        bool IsHidingContainer
        {
            get
            {
                if (isHidingContainer == null)
                {
                    isHidingContainer = false;
                    foreach (L2HidingSpot s in FindObjectsByType<L2HidingSpot>(FindObjectsInactive.Include))
                        if (s != null && s.container == this) { isHidingContainer = true; break; }
                }
                return isHidingContainer.Value;
            }
        }

        // True when an item that hasn't been collected yet is hidden in this container.
        bool HoldsItem()
        {
            L2ItemShuffle shuffle = FindAnyObjectByType<L2ItemShuffle>();
            L2Game g = Game;
            if (shuffle == null || g == null) return false;
            foreach (L2ItemShuffle.Entry e in shuffle.items)
            {
                if (e == null || string.IsNullOrEmpty(e.itemId) || g.IsCollected(e.itemId)) continue;
                L2HidingSpot spot = shuffle.SpotOf(e.itemId);
                if (spot != null && spot.container == this) return true;
            }
            return false;
        }

        void Start()
        {
            // solid collider matching the model (the existing trigger box stays for interaction)
            MeshFilter mf = GetComponent<MeshFilter>();
            Bounds b = default;
            bool has = false;
            if (mf != null && mf.sharedMesh != null) { b = mf.sharedMesh.bounds; has = true; }
            else has = ChildBounds(out b);   // e.g. Level 3 cabinet doors: the panel is a child of the hinge
            if (has)
            {
                solid = gameObject.AddComponent<BoxCollider>();
                solid.center = b.center;
                solid.size = b.size * 0.98f;
                solidList.Add(solid);
                all.Add(this);
            }
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerCC = p.GetComponent<CharacterController>();
            if (solid != null && kind == Kind.Drawer) AddHeadClearance();
        }

        // ---- head-height drawers (e.g. the top drawer of the study filing cabinet in Level 2)
        // The player's CharacterController ends well below the camera, so a drawer that opens above the
        // capsule but at eye level was never touched by it: the player walked "through" it with the
        // camera inside or over the drawer. For such a drawer an invisible box fills the space between the
        // drawer and the top of the player's body and moves with the drawer while it is open, so the
        // player stops at the drawer front like with the lower drawers. It is not part of the drawer
        // (so aiming below the open drawer still reaches the drawer under it) and is off while closed.
        BoxCollider headBlock;
        Vector3 headBlockClosed;

        void AddHeadClearance()
        {
            if (playerCC == null) return;
            Physics.SyncTransforms();
            Bounds db = solid.bounds;
            Transform pt = playerCC.transform;
            float capH = playerCC.height * Mathf.Abs(pt.lossyScale.y);
            float feet = playerCC.bounds.min.y;
            Camera cam = playerCC.GetComponentInChildren<Camera>();
            float eye = cam != null ? cam.transform.position.y - feet : capH;
            if (eye <= capH + 0.05f) return;                       // the body already reaches eye level

            // floor in front of the open drawer
            Vector3 probe = db.center + slide * Mathf.Clamp01(maxOpen);
            probe.y = db.min.y - 0.01f;
            float floor = float.NegativeInfinity;
            foreach (RaycastHit h in Physics.RaycastAll(probe, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<L2Openable>() != null || h.collider.transform.IsChildOf(pt)) continue;
                if (h.point.y > floor) floor = h.point.y;
            }
            if (float.IsNegativeInfinity(floor)) return;
            float bottom = db.min.y - floor;
            if (bottom < capH - 0.05f || bottom > eye + 0.25f) return;  // the body hits it already / it is above the head

            float y0 = floor + capH * 0.5f, y1 = db.min.y + 0.02f;
            var go = new GameObject(name + "_HeadClearance");
            Transform parent = transform.parent;
            if (parent != null && parent.GetComponentInParent<L2Interactable>() != null) parent = null;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(new Vector3(db.center.x, (y0 + y1) * 0.5f, db.center.z), Quaternion.identity);
            // SetParent(.., true) kept the world scale at 1, so the box size below is in world units
            go.layer = gameObject.layer;
            headBlock = go.AddComponent<BoxCollider>();
            headBlock.size = new Vector3(db.size.x, y1 - y0, db.size.z);
            headBlock.enabled = false;
            headBlockClosed = go.transform.position;
            solidList.Add(headBlock);
        }

        // Bounds (in this object's local space) of the visible door / drawer parts that are children of
        // this object. Text labels and other interactables are left out.
        bool ChildBounds(out Bounds b)
        {
            b = default;
            bool any = false;
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.transform == transform || f.sharedMesh == null) continue;
                if (f.GetComponent<TMPro.TMP_Text>() != null) continue;
                L2Interactable owner = f.GetComponentInParent<L2Interactable>();
                if (owner != null && owner != this) continue;
                Bounds mb = f.sharedMesh.bounds;
                Matrix4x4 m = toLocal * f.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return any && b.size.x > 0.01f && b.size.y > 0.01f;
        }

        // Every part with a solid box, so a swinging door stops against another open door / drawer
        // instead of passing through it.
        static readonly System.Collections.Generic.List<L2Openable> all = new System.Collections.Generic.List<L2Openable>();
        void OnDestroy()
        {
            all.Remove(this);
            if (headBlock != null) Destroy(headBlock.gameObject);
        }

        // Overlap of this part with every other part it touches right now.
        readonly System.Collections.Generic.Dictionary<L2Openable, float> overlapNow = new System.Collections.Generic.Dictionary<L2Openable, float>();
        void MeasureOverlaps(System.Collections.Generic.Dictionary<L2Openable, float> into)
        {
            into.Clear();
            if (solid == null) return;
            Bounds mine = solid.bounds;
            foreach (L2Openable o in all)
            {
                if (o == null || o == this || o.solid == null || !o.solid.enabled) continue;
                if (!mine.Intersects(o.solid.bounds)) continue;
                if (Physics.ComputePenetration(solid, solid.transform.position, solid.transform.rotation,
                        o.solid, o.solid.transform.position, o.solid.transform.rotation, out _, out float d) && d > 0f)
                    into[o] = d;
            }
        }

        // How much deeper this part has pushed into another part since the movement started. Parts that
        // already touched (e.g. two doors meeting at a seam while closed) only count once they get deeper.
        float DeeperThanAtStart(System.Collections.Generic.Dictionary<L2Openable, float> atStart)
        {
            MeasureOverlaps(overlapNow);
            float worst = 0f;
            foreach (var kv in overlapNow)
            {
                atStart.TryGetValue(kv.Key, out float was);
                worst = Mathf.Max(worst, kv.Value - was);
            }
            return worst;
        }

        void Awake()
        {
            closedPos = transform.position;
            closedRot = transform.rotation;
            int n = attached != null ? attached.Length : 0;
            attPos = new Vector3[n];
            attRot = new Quaternion[n];
            Quaternion inv = Quaternion.Inverse(closedRot);
            for (int i = 0; i < n; i++)
            {
                if (attached[i] == null) continue;
                attPos[i] = inv * (attached[i].position - closedPos);
                attRot[i] = inv * attached[i].rotation;
            }
        }

        // Makes an object (e.g. a hidden item placed in this drawer at runtime) move with this part.
        // Call it while the part is closed.
        public void Attach(Transform t)
        {
            if (t == null) return;
            int n = attached != null ? attached.Length : 0;
            var a = new Transform[n + 1];
            var p = new Vector3[n + 1];
            var r = new Quaternion[n + 1];
            for (int i = 0; i < n; i++) { a[i] = attached[i]; p[i] = attPos[i]; r[i] = attRot[i]; }
            Quaternion inv = Quaternion.Inverse(closedRot);
            a[n] = t;
            p[n] = inv * (t.position - closedPos);
            r[n] = inv * t.rotation;
            attached = a; attPos = p; attRot = r;
            if (amount > 0f) Apply(amount);
        }

        public override void Interact()
        {
            if (anim != null) StopCoroutine(anim);
            IsOpen = !IsOpen;
            L2Game g = Game;
            if (g != null && g.sfx != null) g.sfx.Drawer(transform.position);
            anim = StartCoroutine(Animate(IsOpen ? Mathf.Clamp01(maxOpen) : 0f));
            if (IsOpen && g != null && IsHidingContainer && !HoldsItem())
            {
                g.Notify(label + ": Empty.", L2NoteKind.Info);
                if (!Searched) g.SetFlag(SearchedFlag);
            }
        }

        IEnumerator Animate(float target)
        {
            float start = amount;
            float time = duration * Mathf.Abs(target - start);
            float t = 0f;
            var atStart = new System.Collections.Generic.Dictionary<L2Openable, float>();
            Physics.SyncTransforms();
            MeasureOverlaps(atStart);
            while (t < time)
            {
                float before = amount;
                Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (t + Time.deltaTime) / time))));
                Physics.SyncTransforms();
                if (DeeperThanAtStart(atStart) > 0.03f)
                {
                    // ran into another open door or drawer: stop against it
                    Apply(before);
                    Physics.SyncTransforms();
                    break;
                }
                if (solidList.Count == 0 || DoorLeafPush.Resolve(solidList, playerCC)) t += Time.deltaTime;
                else { Apply(before); Physics.SyncTransforms(); }   // the player is in the way: wait
                yield return null;
            }
            if (t >= time) Apply(target);
            if (solidList.Count > 0) DoorLeafPush.Resolve(solidList, playerCC);
            anim = null;
        }

        void Apply(float k)
        {
            amount = k;
            Vector3 pos = closedPos;
            Quaternion rot = closedRot;
            if (kind == Kind.Drawer) pos = closedPos + slide * k;
            else { rot = Quaternion.AngleAxis(openAngle * k, Vector3.up) * closedRot; pos = DoorPosition(closedPos, k); }
            transform.SetPositionAndRotation(pos, rot);

            for (int i = 0; i < attPos.Length; i++)
                if (attached[i] != null)
                    attached[i].SetPositionAndRotation(pos + rot * attPos[i], rot * attRot[i]);

            if (headBlock != null)
            {
                headBlock.transform.position = headBlockClosed + slide * k;
                headBlock.enabled = k > 0.001f;
            }
        }

        // For tests: the invisible head-height blocker of this drawer (null if it doesn't need one).
        public Collider HeadClearance => headBlock;
    }
}
