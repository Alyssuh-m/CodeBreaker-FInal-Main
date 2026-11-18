using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Oven timer keypad. Code 4162 (Flour 4, Sugar 1, Eggs 6, Milk 2) opens the oven door and
    // reveals the Kitchen Key. Wrong codes show an error and change nothing.
    public class L2Oven : L2Interactable
    {
        public string code = "4162";
        public Transform ovenDoor;
        public GameObject key;
        public Light ovenLight;
        public float doorAngle = -82f;
        [Tooltip("Flag that must be set before the keypad works (Grandma's recipe completed)")]
        public string requiredFlag = "recipe_complete";
        [Tooltip("Shown with the key when the oven opens (the note inside the oven door)")]
        public GameObject doorNote;

        public bool IsOpen => Game != null && Game.Flag("oven_open");

        public override string Prompt => IsOpen ? "The oven is open" : "Press E to Use the Oven Timer";
        public override bool CanInteract => base.CanInteract && !IsOpen;

        void Start()
        {
            if (key != null) key.SetActive(false);
            if (ovenLight != null) ovenLight.enabled = false;
            if (doorNote != null) doorNote.SetActive(false);
            if (IsOpen) StartCoroutine(Open(false));
        }

        public override void Interact()
        {
            if (Game == null) return;
            if (!string.IsNullOrEmpty(requiredFlag) && !Game.Flag(requiredFlag))
            {
                if (Game.sfx != null) Game.sfx.Locked(transform.position);
                Game.Notify("The oven timer is locked. Grandma's recipe card isn't complete yet.", L2NoteKind.Warning);
                return;
            }
            if (Game.ui != null) Game.ui.OpenKeypad(this);
        }

        // Called by the keypad interface.
        public bool Submit(string entered)
        {
            if (entered != code)
            {
                if (Game != null && Game.sfx != null) Game.sfx.Failure();
                return false;
            }
            if (Game != null)
            {
                Game.SetFlag("oven_open");
                if (Game.sfx != null) Game.sfx.Success();
                Game.Notify("The oven timer chimes and the oven door unlocks.", L2NoteKind.Success);
            }
            StartCoroutine(Open(true));
            return true;
        }

        IEnumerator Open(bool animate)
        {
            if (ovenDoor != null)
            {
                Renderer r = ovenDoor.GetComponent<Renderer>();
                Bounds b = r != null ? r.bounds : new Bounds(ovenDoor.position, Vector3.one);
                Vector3 hinge = new Vector3(b.center.x, b.min.y, b.min.z);
                if (animate)
                {
                    yield return new WaitForSeconds(0.4f);
                    if (Game != null && Game.sfx != null) Game.sfx.Door(true, b.center);
                    float done = 0f;
                    for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                    {
                        float a = Mathf.SmoothStep(0f, doorAngle, t / 0.8f);
                        ovenDoor.RotateAround(hinge, Vector3.right, a - done);
                        done = a;
                        yield return null;
                    }
                    ovenDoor.RotateAround(hinge, Vector3.right, doorAngle - done);
                }
                else ovenDoor.RotateAround(hinge, Vector3.right, doorAngle);
            }
            if (ovenLight != null) ovenLight.enabled = true;
            if (doorNote != null) doorNote.SetActive(true);
            if (key != null && (Game == null || !Game.IsCollected(L2Items.KitchenKey))) key.SetActive(true);
            if (animate && Game != null) { Game.Notify("There's a key inside the oven!", L2NoteKind.Success); Game.RefreshObjective(); }
        }
    }
}
