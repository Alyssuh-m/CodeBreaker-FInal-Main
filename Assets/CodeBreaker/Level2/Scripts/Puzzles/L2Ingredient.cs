using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Flour / Sugar / Eggs / Milk containers. E lifts the container and tips its bottom toward the
    // player so the number underneath can be read, then it is put back. Simple scripted motion,
    // no physics. Can be repeated any time.
    public class L2Ingredient : L2Interactable
    {
        public string ingredient = "Flour";
        public int number = 4;
        [Tooltip("What is written underneath (e.g. the binary 100). Empty = the number.")]
        public string label = "";
        public float liftHeight = 1.1f;
        public float tipAngle = 80f;
        public float holdTime = 2.6f;

        bool busy;
        Vector3 basePos;
        Quaternion baseRot;

        public override string Prompt => "Press E to Lift the " + ingredient + " (check underneath)";

        // Kitchen puzzle (variables): Grandpa labelled the underside of each ingredient with a line of
        // code. Only sugar holds a plain number; the others are worked out from it:
        //   sugar = 1  ->  milk = 1 * 2 = 2,  flour = 1 * 4 = 4,  eggs = 4 + 2 = 6
        // oven.Set(flour, sugar, eggs, milk) = 4 1 6 2 (the oven code is unchanged).
        // These lines replace the old binary labels, wherever the scene still has them.
        public static string VariableLine(string ingredient)
        {
            switch ((ingredient ?? "").ToLowerInvariant())
            {
                case "flour": return "flour = sugar * 4";
                case "sugar": return "sugar = 1";
                case "eggs": return "eggs = flour + milk";
                case "milk": return "milk = sugar * 2";
            }
            return null;
        }

        public string Underside => VariableLine(ingredient) ?? (string.IsNullOrEmpty(label) ? number.ToString() : label);
        public override bool CanInteract => base.CanInteract && !busy;

        void Awake()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
        }

        public override void Interact()
        {
            if (!busy) StartCoroutine(Lift());
        }

        IEnumerator Lift()
        {
            busy = true;
            L2Game g = Game;
            Vector3 toPlayer = Vector3.back;
            if (g != null && g.playerCamera != null)
            {
                toPlayer = g.playerCamera.transform.position - basePos;
                toPlayer.y = Mathf.Max(0f, toPlayer.y * 0.4f);
                if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.back;
                toPlayer.Normalize();
            }
            // tip so the bottom (local down) turns toward the player
            Quaternion full = Quaternion.FromToRotation(Vector3.down, toPlayer);
            Quaternion tipped = Quaternion.Slerp(Quaternion.identity, full, tipAngle / Mathf.Max(1f, Vector3.Angle(Vector3.down, toPlayer))) * baseRot;
            Vector3 up = basePos + Vector3.up * liftHeight;

            if (g != null && g.sfx != null) g.sfx.Drawer(basePos);
            yield return Move(basePos, up, baseRot, tipped, 0.45f);

            if (g != null)
            {
                string flag = "seen_" + ingredient.ToLowerInvariant();
                bool first = !g.Flag(flag);
                g.Notify("Underneath the " + ingredient + ":   " + Underside + (first ? "   (saved to Notes, J)" : ""), L2NoteKind.Info);
                if (!g.Flag("vars_explained"))
                {
                    g.SetFlag("vars_explained");
                    g.Notify("Grandpa's labels are variables: the name on the left holds the value on the right.", L2NoteKind.Item);
                }
                g.SetFlag(flag);
            }
            yield return new WaitForSeconds(holdTime);
            yield return Move(up, basePos, tipped, baseRot, 0.45f);
            busy = false;
        }

        IEnumerator Move(Vector3 p0, Vector3 p1, Quaternion r0, Quaternion r1, float time)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                transform.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            transform.SetPositionAndRotation(p1, r1);
        }
    }
}
