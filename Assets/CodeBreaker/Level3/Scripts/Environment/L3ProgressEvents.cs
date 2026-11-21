using System.Collections;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Environment reactions to keycard progress. Source of truth: the existing flags card_red_in / card_blue_in /
    // card_green_in set by L3CardSlot. Each event runs once and records its own flag (l3evt_1..3, saved with the
    // normal checkpoints). After loading a save, already-reached events are restored silently (lasting state only:
    // emergency lighting / static feeds) and never replayed. Nothing here changes puzzles, doors or progression.
    //   1st card: hall lights flicker, the dark figure appears at the far end of a doorway/corridor, then vanishes.
    //   2nd card: hall lights fail, red emergency beacons start, a quiet siren loops (flashlight still works).
    //   3rd card: CCTV feeds drop to static; one feed (camera wall + hall relay monitor) shows the figure standing in
    //             the room the player just left, for several seconds, then the signal is lost.
    public class L3ProgressEvents : MonoBehaviour
    {
        [Header("Hall lighting")]
        public Light[] hallLights = new Light[0];
        public Renderer[] hallGlow = new Renderer[0];
        [Range(0f, 1f)] public float failedLevel = 0.12f;

        [Header("Dark figure")]
        public GameObject figure;
        public Transform[] figureSpots = new Transform[0];     // doorway / corridor ends in the hall (event 1)
        public AudioClip sting;

        [Header("Emergency (event 2)")]
        public GameObject emergencyRoot;
        public Transform[] beaconSpinners = new Transform[0];
        public AudioSource siren;
        [Range(0f, 1f)] public float sirenVolume = 0.12f;

        [Header("CCTV (event 3)")]
        public Renderer[] camScreens = new Renderer[0];
        public TMP_Text[] camOverlays = new TMP_Text[0];
        public int figureCam = 3;
        public Material staticMat;
        public Material feedMat;
        public Camera feedCam;
        public Renderer relayScreen;
        public TMP_Text relayLabel;
        public Transform[] wingFigureSpots = new Transform[3];   // Maintenance, Security, Control
        public Transform[] wingCamSpots = new Transform[3];
        public float feedSeconds = 8f;

        static readonly string[] CardFlags = { "card_red_in", "card_blue_in", "card_green_in" };
        static readonly string[] WingNames = { "MAINTENANCE", "SECURITY", "CONTROL" };
        float[] baseIntensity;
        bool[] cardWas = new bool[3];
        bool emergencyOn, started;
        int lastWing = 0;

        public bool EmergencyActive => emergencyOn;
        public int EventsReached { get; private set; }

        L2Game G => L2Game.Instance;

        void Awake()
        {
            baseIntensity = new float[hallLights.Length];
            for (int i = 0; i < hallLights.Length; i++) baseIntensity[i] = hallLights[i] != null ? hallLights[i].intensity : 0f;
            if (figure != null) figure.SetActive(false);
            if (emergencyRoot != null) emergencyRoot.SetActive(false);
            if (feedCam != null) feedCam.enabled = false;
        }

        IEnumerator Start()
        {
            yield return null;                                  // let L2Game restore a loaded checkpoint first
            L2Game g = G;
            if (g == null) yield break;
            for (int i = 0; i < 3; i++) cardWas[i] = g.Flag(CardFlags[i]);
            int count = Count(g);
            // already reached (loaded game): restore lasting state silently, never replay
            for (int k = 1; k <= count; k++) { g.SetFlag("l3evt_" + k); EventsReached = k; }
            bool escaped = L3Manager.Instance != null && L3Manager.Instance.Solved("term");   // Phase 9: lockdown already lifted
            if (count >= 2 && !escaped) SetEmergency(true, true);
            if (count >= 3) SetFeedsStatic(true);
            started = true;
        }

        static int Count(L2Game g) { int n = 0; foreach (string f in CardFlags) if (g.Flag(f)) n++; return n; }

        void Update()
        {
            L2Game g = G;
            if (g == null || !started) return;
            for (int i = 0; i < 3; i++)
            {
                bool now = g.Flag(CardFlags[i]);
                if (now && !cardWas[i]) lastWing = i;
                cardWas[i] = now;
            }
            int count = Count(g);
            for (int k = 1; k <= count; k++)
            {
                if (g.Flag("l3evt_" + k)) continue;
                g.SetFlag("l3evt_" + k);
                EventsReached = k;
                if (k == 1) StartCoroutine(Event1());
                else if (k == 2) StartCoroutine(Event2());
                else StartCoroutine(Event3());
                break;                                          // one event per frame
            }
            if (emergencyOn)
            {
                foreach (Transform b in beaconSpinners) if (b != null) b.Rotate(0f, 200f * Time.deltaTime, 0f, Space.Self);
                if (siren != null) siren.volume = sirenVolume * CodeBreakerSettings.SfxVolume * (g.Paused ? 0.35f : 1f);
            }
        }

        // ------------------------------------------------------------------ helpers
        void SetHall(float level, bool glow)
        {
            for (int i = 0; i < hallLights.Length; i++) if (hallLights[i] != null) hallLights[i].intensity = baseIntensity[i] * level;
            foreach (Renderer r in hallGlow) if (r != null) r.enabled = glow;
        }

        void Sting(float vol)
        {
            L2Game g = G;
            AudioClip c = sting != null ? sting : (L3Manager.Instance != null ? L3Manager.Instance.horrorClip : null);
            if (c != null && g != null && g.sfx != null) g.sfx.Play(c, vol);
        }

        // A spot the player can really see right now: inside the view, 6+ m away, clear line of sight to head and chest.
        // Returns null when none is visible (e.g. the player is still facing the terminal screen).
        Transform VisibleSpot()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Transform best = null; float bestDot = float.MinValue;
            foreach (Transform s in figureSpots)
            {
                if (s == null) continue;
                Vector3 head = s.position + Vector3.up * 6.2f, chest = s.position + Vector3.up * 4f;
                Vector3 vp = cam.WorldToViewportPoint(chest);
                if (vp.z < 6f || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.05f || vp.y > 1f) continue;
                if (!Clear(cam.transform.position, head) || !Clear(cam.transform.position, chest)) continue;
                float d = -vp.z;                                 // nearest visible spot wins
                if (d > bestDot) { bestDot = d; best = s; }
            }
            return best;
        }

        public string LastBlocker { get; private set; } = "";

        // Line of sight, ignoring the player's own colliders (the camera sits inside the player body).
        bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            Transform self = G != null && G.player != null ? G.player : null;
            foreach (RaycastHit h in Physics.RaycastAll(from, d.normalized, d.magnitude - 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (self != null && h.transform.IsChildOf(self)) continue;
                LastBlocker = h.collider.name + " @ " + h.point.ToString("F1");
                return false;
            }
            return true;
        }

        void ShowFigure(Vector3 pos, Vector3 lookAt)
        {
            if (figure == null) return;
            figure.transform.position = pos;
            Vector3 d = lookAt - pos; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) figure.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            figure.SetActive(true);
        }

        // ------------------------------------------------------------------ event 1
        // Lights flicker at once. The figure appears the first moment the player can actually see one of the
        // corridor/doorway spots (the main screen blocks the view while standing at the terminal), within 60 s.
        IEnumerator Event1()
        {
            yield return Flicker(2.4f);
            float waited = 0f;
            Transform spot = null;
            while (waited < 60f && spot == null)
            {
                spot = VisibleSpot();
                if (spot == null) { waited += Time.deltaTime; yield return null; }
            }
            if (spot == null) { if (!emergencyOn) SetHall(1f, true); yield break; }
            Camera cam = Camera.main;
            ShowFigure(spot.position, cam != null ? cam.transform.position : spot.position + spot.forward);
            Sting(0.85f);
            yield return Flicker(2.3f);
            if (figure != null) figure.SetActive(false);
            if (!emergencyOn) SetHall(1f, true);
        }

        IEnumerator Flicker(float seconds)
        {
            float start = Time.time;
            while (Time.time - start < seconds)
            {
                float lvl = Random.value < 0.45f ? Random.Range(0.02f, 0.25f) : Random.Range(0.7f, 1f);
                if (!emergencyOn) SetHall(lvl, lvl > 0.5f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
            }
            if (!emergencyOn) SetHall(1f, true);
        }

        // ------------------------------------------------------------------ event 2
        IEnumerator Event2()
        {
            for (int i = 0; i < 6; i++)
            {
                SetHall(i % 2 == 0 ? 0.05f : 0.6f, i % 2 == 1);
                yield return new WaitForSeconds(Random.Range(0.08f, 0.2f));
            }
            SetEmergency(true, false);
        }

        void SetEmergency(bool on, bool instant)
        {
            emergencyOn = on;
            SetHall(on ? failedLevel : 1f, !on);
            if (emergencyRoot != null) emergencyRoot.SetActive(on);
            if (siren != null)
            {
                if (on && !siren.isPlaying) { siren.volume = instant ? sirenVolume * CodeBreakerSettings.SfxVolume : 0f; siren.Play(); }
                if (!on) siren.Stop();
            }
        }

        // Used by the final escape sequence (Phase 9): stop the emergency state.
        public void EndEmergency()
        {
            emergencyOn = false;
            if (siren != null) siren.Stop();
            if (emergencyRoot != null) emergencyRoot.SetActive(false);
        }

        public void RestoreHallLights(float level) => SetHall(level, level > 0.5f);

        // ------------------------------------------------------------------ event 3
        IEnumerator Event3()
        {
            SetFeedsStatic(false);
            int w = Mathf.Clamp(lastWing, 0, 2);
            Transform fs = wingFigureSpots.Length > w ? wingFigureSpots[w] : null;
            Transform cs = wingCamSpots.Length > w ? wingCamSpots[w] : null;
            yield return new WaitForSeconds(1.2f);
            if (fs != null && cs != null && feedCam != null)
            {
                ShowFigure(fs.position, cs.position);
                feedCam.transform.SetPositionAndRotation(cs.position, Quaternion.LookRotation(fs.position + Vector3.up * 3.6f - cs.position, Vector3.up));
                feedCam.enabled = true;
                SetScreen(figureCam, feedMat);
                if (camOverlays.Length > figureCam && camOverlays[figureCam] != null) camOverlays[figureCam].text = "CAM " + (figureCam + 1) + "\n" + WingNames[w] + "\n<color=#FF4A3D>LIVE</color>";
                if (relayScreen != null) { relayScreen.sharedMaterial = feedMat; RemoveStatic(relayScreen); }
                if (relayLabel != null) relayLabel.text = "CCTV RELAY  ·  CAM " + (figureCam + 1) + "  ·  " + WingNames[w] + "  <color=#FF4A3D>● LIVE</color>";
                Sting(0.9f);
                yield return new WaitForSeconds(feedSeconds);
                if (figure != null) figure.SetActive(false);
                feedCam.enabled = false;
            }
            SetFeedsStatic(true);
        }

        void SetFeedsStatic(bool includeRelay)
        {
            for (int i = 0; i < camScreens.Length; i++)
            {
                SetScreen(i, staticMat);
                if (i < camOverlays.Length && camOverlays[i] != null) camOverlays[i].text = "CAM " + (i + 1) + "\n<color=#FF4A3D>NO SIGNAL</color>";
            }
            if (includeRelay && relayScreen != null)
            {
                relayScreen.sharedMaterial = staticMat;
                if (relayScreen.GetComponent<L3ScreenStatic>() == null) relayScreen.gameObject.AddComponent<L3ScreenStatic>();
                if (relayLabel != null) relayLabel.text = "CCTV RELAY  ·  <color=#FF4A3D>SIGNAL LOST</color>";
            }
        }

        void SetScreen(int i, Material m)
        {
            if (i < 0 || i >= camScreens.Length || camScreens[i] == null || m == null) return;
            camScreens[i].sharedMaterial = m;
            if (m == staticMat) { if (camScreens[i].GetComponent<L3ScreenStatic>() == null) camScreens[i].gameObject.AddComponent<L3ScreenStatic>(); }
            else RemoveStatic(camScreens[i]);
        }

        static void RemoveStatic(Renderer r)
        {
            var s = r.GetComponent<L3ScreenStatic>();
            if (s != null) Destroy(s);
            var block = new MaterialPropertyBlock();
            r.SetPropertyBlock(block);
        }
    }
}
