using System.Collections;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The six-monitor camera wall:  alarm = (motion > 3) && (door == OPEN);
    // The player presses the desk button of the camera that made alarm true (CAM 4).
    // The correct button zooms CAM 4's feed in on a dark figure that walks out of view.
    public class L3CameraWall : MonoBehaviour
    {
        public string station = "s1";
        public int correctIndex = 4;
        public Transform[] screens = new Transform[6];   // CAM 1..6 screen quads
        public TMP_Text[] overlays = new TMP_Text[6];
        public Transform figure;                          // dark silhouette on CAM 4's feed
        public Vector3 figureFrom, figureTo;              // local positions on the screen
        public Light screenGlow;
        [TextArea(1, 3)] public string hint = "&& needs BOTH sides true. Is 3 greater than 3?";
        public float penalty = 10f;
        public L3Reaction[] onSolved = new L3Reaction[0];

        L3Manager M => L3Manager.Instance;
        public bool Solved => M != null && M.Solved(station);

        Vector3 zoomFromPos, zoomFromScale;
        bool busy;

        void Start()
        {
            if (figure != null) figure.gameObject.SetActive(false);
            if (Solved) { ShowSolved(); L3Reaction.RunAll(onSolved, true); }
        }

        public void Press(int index)
        {
            L2Game g = L2Game.Instance;
            if (g == null || M == null || busy) return;
            if (Solved)
            {
                g.Notify("CAM " + correctIndex + " already flagged. Something was there...", L2NoteKind.Info);
                return;
            }
            if (index == correctIndex)
            {
                M.Solve(station);
                M.Good();
                StartCoroutine(Horror());
                L3Reaction.RunAll(onSolved, false);
                return;
            }
            M.AddWrong(station);
            M.Penalty(penalty);
            g.Notify("CAM " + index + ": alarm = false.  Wrong camera.", L2NoteKind.Warning);
            M.ShowHintIfReady(station, hint);
        }

        IEnumerator Horror()
        {
            busy = true;
            L2Game g = L2Game.Instance;
            Transform scr = Screen(correctIndex);
            Vector3 p0 = Vector3.zero, s0 = Vector3.one;
            if (scr != null) { p0 = scr.localPosition; s0 = scr.localScale; }
            Vector3 p1 = p0 + new Vector3(0f, 0f, -0.25f);
            Vector3 s1 = s0 * 1.35f;

            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                if (scr != null) { scr.localPosition = Vector3.Lerp(p0, p1, k); scr.localScale = Vector3.Lerp(s0, s1, k); }
                yield return null;
            }

            if (g != null && M != null && M.horrorClip != null && g.sfx != null) g.sfx.Play(M.horrorClip, 0.9f);
            if (figure != null)
            {
                figure.gameObject.SetActive(true);
                for (float t = 0f; t < 2.6f; t += Time.deltaTime)
                {
                    float k = t / 2.6f;
                    figure.localPosition = Vector3.Lerp(figureFrom, figureTo, k) + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 7f)) * 0.01f, 0f);
                    if (screenGlow != null) screenGlow.intensity = Random.value < 0.15f ? 0.2f : 1.2f;
                    yield return null;
                }
                figure.gameObject.SetActive(false);
            }
            if (screenGlow != null) screenGlow.intensity = 1.2f;
            if (g != null) g.Notify("Something walked out of CAM " + correctIndex + "'s view...", L2NoteKind.Warning);

            yield return new WaitForSeconds(0.6f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                if (scr != null) { scr.localPosition = Vector3.Lerp(p1, p0, k); scr.localScale = Vector3.Lerp(s1, s0, k); }
                yield return null;
            }
            ShowSolved();
            busy = false;
        }

        Transform Screen(int index) => index >= 1 && index <= screens.Length ? screens[index - 1] : null;

        void ShowSolved()
        {
            TMP_Text o = index4Overlay();
            if (o != null)
            {
                o.text = "CAM " + correctIndex + "\nmotion = 5\ndoor = OPEN\n<color=#FF4040>ALARM = TRUE</color>";
            }
        }

        TMP_Text index4Overlay() => correctIndex >= 1 && correctIndex <= overlays.Length ? overlays[correctIndex - 1] : null;
    }
}
