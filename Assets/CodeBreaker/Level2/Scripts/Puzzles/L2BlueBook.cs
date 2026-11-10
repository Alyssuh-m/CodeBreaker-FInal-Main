using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // The blue book on the bookshelf. When the star box opens, it tilts out and the Living Room Key
    // drops onto the shelf in front of it, where it can be picked up.
    public class L2BlueBook : L2Interactable
    {
        public Transform book;
        public GameObject key;
        [Tooltip("Where the key lands (world space)")]
        public Vector3 keyRestPosition;
        public Vector3 keyRestEuler = new Vector3(90f, 0f, 0f);
        public float tiltAngle = -22f;

        public bool Released => Game != null && Game.Flag("book_released");

        public override string Prompt => "Press E to Examine";
        public override bool CanInteract => base.CanInteract;

        void Start()
        {
            if (key != null) key.SetActive(false);
            if (Released) Release(false);
        }

        public override void Interact()
        {
            if (Game == null) return;
            Game.Notify(Released ? "The blue book has tipped forward on the shelf." : "A blue book. It's stuck fast, as if something holds it shut.", L2NoteKind.Info);
        }

        public void Release(bool animate)
        {
            if (Game != null) Game.SetFlag("book_released");
            StartCoroutine(Tilt(animate));
        }

        IEnumerator Tilt(bool animate)
        {
            Transform b = book != null ? book : transform;
            Bounds bb = BoundsOf(b);
            Vector3 hinge = new Vector3(bb.center.x, bb.min.y, bb.min.z);
            if (animate)
            {
                if (Game != null && Game.sfx != null) Game.sfx.Drawer(b.position);
                float done = 0f;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    float a = Mathf.SmoothStep(0f, tiltAngle, t / 0.5f);
                    b.RotateAround(hinge, Vector3.right, a - done);
                    done = a;
                    yield return null;
                }
                b.RotateAround(hinge, Vector3.right, tiltAngle - done);
            }
            else b.RotateAround(hinge, Vector3.right, tiltAngle);

            if (key != null && (Game == null || !Game.IsCollected(L2Items.LivingKey)))
            {
                key.transform.SetParent(transform.parent, true);
                key.transform.SetPositionAndRotation(keyRestPosition, Quaternion.Euler(keyRestEuler));
                key.SetActive(true);
                if (animate && Game != null) Game.Notify("Something drops out of the blue book!", L2NoteKind.Success);
            }
        }

        static Bounds BoundsOf(Transform t)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.1f);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
