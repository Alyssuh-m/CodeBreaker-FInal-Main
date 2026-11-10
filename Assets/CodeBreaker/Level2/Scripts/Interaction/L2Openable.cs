using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // A drawer, cabinet door or closet door on the existing furniture models. E opens / closes it
    // directly with a short smooth animation (no extra window). Each one works on its own.
    //  - Drawer: slides out along 'slide' (world space) and back.
    //  - Door:   swings 'openAngle' degrees around its own pivot, which in these models is the hinge.
    // Objects stuck on the part (e.g. notes and magnets on the fridge doors) move with it.
    // Open / closed is not saved: everything starts closed. The part has no solid collider, so an
    // open drawer or door never traps the player.
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

        public bool IsOpen { get; private set; }

        Vector3 closedPos;
        Quaternion closedRot;
        Vector3[] attPos;
        Quaternion[] attRot;
        float amount;          // 0 = closed, 1 = open
        Coroutine anim;

        public override string Prompt => (IsOpen ? "Press E to Close " : "Press E to Open ") + label;

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

        public override void Interact()
        {
            if (anim != null) StopCoroutine(anim);
            IsOpen = !IsOpen;
            L2Game g = Game;
            if (g != null && g.sfx != null) g.sfx.Drawer(transform.position);
            anim = StartCoroutine(Animate(IsOpen ? 1f : 0f));
        }

        IEnumerator Animate(float target)
        {
            float start = amount;
            float time = duration * Mathf.Abs(target - start);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / time)));
                yield return null;
            }
            Apply(target);
            anim = null;
        }

        void Apply(float k)
        {
            amount = k;
            Vector3 pos = closedPos;
            Quaternion rot = closedRot;
            if (kind == Kind.Drawer) pos = closedPos + slide * k;
            else rot = Quaternion.AngleAxis(openAngle * k, Vector3.up) * closedRot;
            transform.SetPositionAndRotation(pos, rot);

            for (int i = 0; i < attPos.Length; i++)
                if (attached[i] != null)
                    attached[i].SetPositionAndRotation(pos + rot * attPos[i], rot * attRot[i]);
        }
    }
}
