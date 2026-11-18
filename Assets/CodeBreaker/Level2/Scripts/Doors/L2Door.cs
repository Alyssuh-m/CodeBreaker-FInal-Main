using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // A Level 2 door (on the door prefab root). Uses the prefab's own DoorPivot and DoorLeaf collider.
    //  - Exit doors start closed and locked; the matching key unlocks them (from the inventory or held).
    //  - Entrance doors start closed and unlocked: E opens / closes them.
    //  - The door won't close on the player (uses the prefab's InteractionZone box for the doorway).
    public class L2Door : L2Interactable
    {
        public Transform pivot;
        public Collider leafCollider;
        [Tooltip("The prefab's InteractionZone box: used only to know where the doorway is")]
        public BoxCollider doorwayZone;
        [Tooltip("DoorPivot local Y angle when closed / open")]
        public float closedAngle = 0f;
        public float openAngle = 270f;
        [Tooltip("Item id of the key (empty = no key needed)")]
        public string requiredKey;
        [Tooltip("Flag set the first time this door is opened (exit doors)")]
        public string exitFlag;
        public float swingTime = 0.9f;

        System.Collections.Generic.List<Collider> leafColliders;

        public bool Locked { get; private set; }
        public bool IsOpen { get; private set; }
        bool moving;

        string KeyName => L2Items.NameOf(requiredKey);
        Vector3 Pos => leafCollider != null ? leafCollider.bounds.center : transform.position + Vector3.up * 4f;

        void Start()
        {
            if (Game != null && Game.TryGetDoorState(name, out L2DoorEntry s))
            {
                Locked = s.locked && !string.IsNullOrEmpty(requiredKey);
                IsOpen = s.open && !Locked;
            }
            else
            {
                Locked = !string.IsNullOrEmpty(requiredKey);
                IsOpen = false;
            }
            SetAngle(IsOpen ? openAngle : closedAngle);
            Record();
        }

        void SetAngle(float y)
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, y, 0f);
        }

        void Record()
        {
            if (Game != null) Game.RecordDoor(name, Locked, IsOpen);
        }

        public override bool CanInteract => base.CanInteract && !moving;

        public override string Prompt
        {
            get
            {
                if (Locked) return Game != null && Game.HasItem(requiredKey) ? "Press E to Unlock" : "Locked  -  Press E to Try the Door";
                return IsOpen ? "Press E to Close" : "Press E to Open";
            }
        }

        public override bool AcceptsItem(string itemId) => Locked && itemId == requiredKey;

        public override bool UseItem(string itemId)
        {
            if (!AcceptsItem(itemId)) return false;
            Unlock();
            return true;
        }

        public override void Interact()
        {
            if (moving || Game == null) return;
            if (Locked)
            {
                if (Game.HasItem(requiredKey)) { Unlock(); return; }
                if (Game.sfx != null) Game.sfx.Locked(Pos);
                Game.Notify("It's locked. You need the " + KeyName + ".", L2NoteKind.Warning);
                return;
            }
            if (IsOpen && PlayerInDoorway())
            {
                Game.Notify("Step out of the doorway first.", L2NoteKind.Info);
                return;
            }
            StartCoroutine(Swing(!IsOpen));
        }

        void Unlock()
        {
            Locked = false;
            Game.RemoveItem(requiredKey);
            if (Game.sfx != null) Game.sfx.Unlock(Pos);
            Game.Notify("The door is now unlocked.", L2NoteKind.Success);
            Record();
            Game.RefreshObjective();
            StartCoroutine(Swing(true, 0.35f));
        }

        IEnumerator Swing(bool open, float delay = 0f)
        {
            moving = true;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            // The leaf stays solid while it swings (it used to be switched off, which let the player
            // walk through a door that was just starting to open). It pushes the player out of its
            // way; if the player is pinned against something, the door waits instead of passing
            // through them.
            if (leafCollider != null) leafCollider.enabled = true;
            if (Game != null && Game.sfx != null) Game.sfx.Door(open, Pos);

            CharacterController cc = Game != null && Game.player != null ? Game.player.GetComponent<CharacterController>() : null;
            if (leafColliders == null) leafColliders = DoorLeafPush.SolidColliders(leafCollider != null ? leafCollider.transform : pivot);
            float from = pivot != null ? pivot.localEulerAngles.y : 0f;
            float to = open ? openAngle : closedAngle;
            float t = 0f;
            while (t < swingTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (t + Time.deltaTime) / swingTime));
                float before = pivot != null ? pivot.localEulerAngles.y : 0f;
                SetAngle(Mathf.LerpAngle(from, to, k));
                if (DoorLeafPush.Resolve(leafColliders, cc)) t += Time.deltaTime;
                else
                {
                    SetAngle(before);   // pinned: wait for the player to step aside
                    Physics.SyncTransforms();
                }
                yield return null;
            }
            SetAngle(to);
            DoorLeafPush.Resolve(leafColliders, cc);

            IsOpen = open;
            moving = false;
            Record();

            if (open && !string.IsNullOrEmpty(exitFlag) && Game != null && !Game.Flag(exitFlag))
            {
                Game.SetFlag(exitFlag);
                Game.RefreshObjective();
            }
        }

        // Is the player standing in the doorway (where the closed leaf would be)?
        bool PlayerInDoorway()
        {
            if (doorwayZone == null || Game == null || Game.player == null) return false;
            Transform z = doorwayZone.transform;
            Vector3 local = z.InverseTransformPoint(Game.player.position);
            Vector3 half = doorwayZone.size * 0.5f;
            Vector3 c = doorwayZone.center;
            // shrink the zone along the door's normal so standing in front of the door is fine
            return Mathf.Abs(local.x - c.x) < half.x && Mathf.Abs(local.z - c.z) < half.z * 0.75f;
        }
    }
}
