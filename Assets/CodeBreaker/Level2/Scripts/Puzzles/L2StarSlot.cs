using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Star-slot box on the bookshelf. Accepts only the Star Figurine, and only while the star shadow
    // points at it (the lamp puzzle is solved). Placing it opens the box and the blue book releases
    // the Living Room Key.
    public class L2StarSlot : L2Interactable
    {
        public L2StarLamp lamp;
        public L2BlueBook blueBook;
        [Tooltip("Where the placed figurine stands")]
        public Transform slot;

        public bool Placed => Game != null && Game.Flag("star_placed");

        GameObject placedVisual;

        // Faint pulsing glow on the slot while the star shadow points at it (runtime light only).
        Light glow;

        public override string Prompt => Placed ? "Press E to Examine" : "Press E to Examine the Star Slot";
        public override bool WantsItem => !Placed;
        public override bool AcceptsItem(string itemId) => !Placed && itemId == L2Items.StarFigurine;

        void Start()
        {
            if (Placed) ShowFigurine();
            var go = new GameObject("Star Slot Glow (runtime)");
            go.transform.SetParent(transform, false);
            go.transform.position = (slot != null ? slot.position : transform.position) + Vector3.up * 0.45f;
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.45f);
            glow.range = 2.2f;
            glow.shadows = LightShadows.None;
            glow.intensity = 0f;
            glow.enabled = false;
        }

        void Update()
        {
            if (glow == null) return;
            bool on = !Placed && lamp != null && lamp.ShadowActive;
            glow.enabled = on;
            if (on) glow.intensity = 1.2f + 0.8f * Mathf.Sin(Time.time * 3f);
        }

        public override bool UseItem(string itemId)
        {
            if (!AcceptsItem(itemId)) return false;
            if (lamp == null || !lamp.ShadowActive)
            {
                if (Game.sfx != null) Game.sfx.Failure();
                Game.Notify("The slot sits in shadow and nothing lines up. Something has to point the way first.", L2NoteKind.Warning);
                return false;
            }
            Game.RemoveItem(L2Items.StarFigurine);
            Game.SetFlag("star_placed");
            ShowFigurine();
            if (Game.sfx != null) Game.sfx.Unlock(transform.position);
            Game.Notify("The star clicks into place. The box opens...", L2NoteKind.Success);
            StartCoroutine(ReleaseBook());
            return true;
        }

        IEnumerator ReleaseBook()
        {
            yield return new WaitForSeconds(0.8f);
            if (blueBook != null) blueBook.Release(true);
            Game.RefreshObjective();
        }

        void ShowFigurine()
        {
            if (placedVisual != null || Game == null) return;
            Transform proto = Game.GetPrototype(L2Items.StarFigurine);
            if (proto == null) return;
            placedVisual = L2HeldItem.BuildVisual(proto, 0.55f, "Placed Star Figurine");
            Transform at = slot != null ? slot : transform;
            placedVisual.transform.SetPositionAndRotation(at.position + Vector3.up * 0.26f, Quaternion.identity);
            placedVisual.transform.SetParent(transform, true);
        }

        public override void Interact()
        {
            if (Game == null) return;
            if (Placed) Game.Notify("The star figurine sits snugly in the slot.", L2NoteKind.Info);
            else Game.Notify("A small box with a star-shaped slot in its lid.", L2NoteKind.Info);
        }
    }
}
