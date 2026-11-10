using System.Collections;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Shows / hides objects. With a slide offset the first shown object slides out from
    // (position + slideFrom) to its own position, like paper or a keycard coming out of a printer.
    public class L3ShowObjects : L3Reaction
    {
        public GameObject[] show = new GameObject[0];
        public GameObject[] hide = new GameObject[0];
        [Tooltip("World offset the shown objects start from (zero = just appear)")]
        public Vector3 slideFrom = Vector3.zero;
        public float delay = 0f;
        public float duration = 1.2f;
        public AudioClip sound;
        public string message;

        public override void Run(bool restoring)
        {
            foreach (GameObject h in hide) if (h != null) h.SetActive(false);
            if (restoring || (delay <= 0f && slideFrom == Vector3.zero))
            {
                foreach (GameObject s in show) if (s != null) s.SetActive(true);
                if (!restoring) Effects();
                return;
            }
            StartCoroutine(Slide());
        }

        void Effects()
        {
            L2Game g = L2Game.Instance;
            if (g == null) return;
            if (sound != null && g.sfx != null) g.sfx.PlayAt(sound, transform.position, 0.8f);
            if (!string.IsNullOrEmpty(message)) g.Notify(message, L2NoteKind.Item);
        }

        IEnumerator Slide()
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Effects();
            int n = show.Length;
            var end = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (show[i] == null) continue;
                end[i] = show[i].transform.position;
                show[i].transform.position = end[i] + slideFrom;
                show[i].SetActive(true);
            }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                for (int i = 0; i < n; i++)
                    if (show[i] != null && show[i].activeSelf) show[i].transform.position = end[i] + slideFrom * (1f - k);
                yield return null;
            }
            for (int i = 0; i < n; i++)
                if (show[i] != null) show[i].transform.position = end[i];
        }
    }
}
