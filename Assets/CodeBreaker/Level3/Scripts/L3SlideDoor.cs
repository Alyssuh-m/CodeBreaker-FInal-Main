using System.Collections;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The exit door: a solid leaf that slides sideways into the wall when the terminal is solved.
    public class L3SlideDoor : L3Reaction
    {
        public Transform leaf;
        public Vector3 openOffset = new Vector3(5.4f, 0f, 0f);
        public float duration = 2.2f;
        public AudioClip openSound;
        public Light statusLight;
        public Color lockedColor = new Color(1f, 0.15f, 0.1f), openColor = new Color(0.2f, 1f, 0.35f);

        Vector3 closed;
        bool open;

        void Awake()
        {
            if (leaf != null) closed = leaf.localPosition;
            if (statusLight != null) statusLight.color = lockedColor;
        }

        public override void Run(bool restoring)
        {
            if (open || leaf == null) return;
            open = true;
            if (statusLight != null) statusLight.color = openColor;
            if (restoring) { leaf.localPosition = closed + openOffset; return; }
            L2Game g = L2Game.Instance;
            if (g != null && g.sfx != null)
            {
                if (openSound != null) g.sfx.PlayAt(openSound, leaf.position, 1f);
                else g.sfx.Door(true, leaf.position);
            }
            StartCoroutine(Slide());
        }

        IEnumerator Slide()
        {
            yield return new WaitForSeconds(0.6f);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                leaf.localPosition = closed + openOffset * Mathf.SmoothStep(0f, 1f, t / duration);
                yield return null;
            }
            leaf.localPosition = closed + openOffset;
        }
    }
}
