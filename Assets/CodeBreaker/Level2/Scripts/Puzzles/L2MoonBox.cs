using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // moon box (under the bed)
    // Directional lock UP, RIGHT, RIGHT, DOWN. When solved the box slides out from under the bed,
    // its lid flips open and the note inside can be read.
    public class L2MoonBox : L2Interactable
    {
        public Transform lid;
        public GameObject interior;
        [Tooltip("How far the box slides out from under the bed (world space)")]
        public Vector3 slideOut = new Vector3(1.3f, 0f, 0f);
        [Tooltip("Front edge of the lid (local to this object), the lid flips around it")]
        public Vector3 hingeLocal;
        public Vector3 hingeAxis = Vector3.forward;
        public float lidAngle = -100f;

        public static readonly string[] Code = { "UP", "RIGHT", "RIGHT", "DOWN" };

        public bool IsOpen { get; private set; }
        Vector3 closedPos;
        bool animating;

        public override string Prompt => IsOpen ? "The box is open" : "Press E to Use the Lock";
        public override bool CanInteract => base.CanInteract && !IsOpen && !animating;

        void Start()
        {
            closedPos = transform.position;
            if (interior != null) interior.SetActive(false);
            if (Game != null && Game.Flag("moonbox_open")) OpenInstant();
        }

        public override void Interact()
        {
            if (Game != null && Game.ui != null) Game.ui.OpenDirectionLock(this);
        }

        public void Solve()
        {
            if (IsOpen || animating) return;
            if (Game != null)
            {
                Game.SetFlag("moonbox_open");
                if (Game.sfx != null) Game.sfx.Unlock(transform.position);
                Game.Notify("Click! The moon box opens.", L2NoteKind.Success);
                Game.RefreshObjective();
            }
            StartCoroutine(OpenRoutine());
        }

        void OpenInstant()
        {
            transform.position = closedPos + slideOut;
            if (lid != null) lid.RotateAround(transform.TransformPoint(hingeLocal), hingeAxis, lidAngle);
            if (interior != null) interior.SetActive(true);
            IsOpen = true;
        }

        IEnumerator OpenRoutine()
        {
            animating = true;
            if (Game != null && Game.sfx != null) Game.sfx.Drawer(transform.position);
            Vector3 from = transform.position, to = closedPos + slideOut;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.7f));
                yield return null;
            }
            transform.position = to;
            if (interior != null) interior.SetActive(true);
            float done = 0f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float a = Mathf.SmoothStep(0f, lidAngle, t / 0.6f);
                if (lid != null) lid.RotateAround(transform.TransformPoint(hingeLocal), hingeAxis, a - done);
                done = a;
                yield return null;
            }
            if (lid != null) lid.RotateAround(transform.TransformPoint(hingeLocal), hingeAxis, lidAngle - done);
            IsOpen = true;
            animating = false;
        }
    }
}
