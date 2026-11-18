using UnityEngine;

namespace CodeBreaker.Level2
{
    // The Living Room TV. Controlled only through the remote. Channel 3 = blue screen.
    public class L2TV : MonoBehaviour
    {
        [Tooltip("Screen surface in front of the TV (unlit material)")]
        public Renderer screen;
        public Light screenGlow;
        public int blueChannel = 3;

        public bool On { get; private set; }
        public int Channel { get; private set; } = 1;
        public bool IsBlue => On && Channel == blueChannel;

        Material inst;
        static readonly Color Off = new Color(0.02f, 0.02f, 0.025f);

        // Puzzle feedback: Channel 3 gives the room a soft blue tint (a wide, faint light created at
        // runtime, nothing is added to the scene) and a quiet click. StaticBurst() is the short scare
        // when the Living Room Key drops.
        [Header("Feedback")]
        public float tintIntensity = 0.9f;
        public float tintRange = 26f;
        Light tint;
        float tintLevel;
        float staticUntil = -1f;
        bool wasBlue;

        void Start()
        {
            if (screen != null) { inst = new Material(screen.sharedMaterial); screen.sharedMaterial = inst; }
            L2Game g = L2Game.Instance;
            if (g != null)
            {
                On = g.Flag("tv_on");
                Channel = g.GetInt("tv_channel", 1);
            }
            wasBlue = IsBlue;
            tintLevel = IsBlue ? 1f : 0f;
            BuildTint();
            Refresh();
        }

        void BuildTint()
        {
            var go = new GameObject("TV Blue Tint (runtime)");
            go.transform.SetParent(transform, false);
            Vector3 at = screen != null ? screen.bounds.center : transform.position;
            Vector3 dir = screenGlow != null ? screenGlow.transform.position - at : Vector3.zero;
            go.transform.position = at + (dir.sqrMagnitude > 1e-4f ? dir.normalized * 2.5f : Vector3.up * 1.5f);
            tint = go.AddComponent<Light>();
            tint.type = LightType.Point;
            tint.color = new Color(0.35f, 0.5f, 1f);
            tint.range = tintRange;
            tint.shadows = LightShadows.None;
            tint.intensity = 0f;
            tint.enabled = false;
        }

        // A burst of static on the screen (with sound) for 'seconds', then back to the channel.
        public void StaticBurst(float seconds)
        {
            if (!On) return;
            staticUntil = Time.time + seconds;
            L2Game g = L2Game.Instance;
            if (g != null && g.sfx != null) g.sfx.Static(transform.position);
        }

        public void SetPower(bool on)
        {
            On = on;
            Save();
        }

        public void SetChannel(int ch)
        {
            Channel = Mathf.Clamp(ch, 1, 9);
            Save();
        }

        void Save()
        {
            L2Game g = L2Game.Instance;
            if (g != null)
            {
                g.SetFlag("tv_on", On);
                g.SetInt("tv_channel", Channel);
                g.RefreshObjective();
            }
            Refresh();
        }

        public static string ChannelName(int ch)
        {
            switch (ch)
            {
                case 1: return "News";
                case 2: return "Nature";
                case 3: return "Blue Screen";
                case 4: return "Cooking";
                case 5: return "No Signal";
                case 6: return "Cartoons";
                case 7: return "Late Movie";
                case 8: return "No Signal";
                default: return "Weather";
            }
        }

        Color ChannelColor(int ch)
        {
            switch (ch)
            {
                case 1: return new Color(0.55f, 0.5f, 0.42f);
                case 2: return new Color(0.2f, 0.55f, 0.25f);
                case 3: return new Color(0.08f, 0.28f, 1f);
                case 4: return new Color(0.8f, 0.42f, 0.15f);
                case 6: return new Color(0.85f, 0.3f, 0.65f);
                case 7: return new Color(0.75f, 0.2f, 0.12f);
                case 9: return new Color(0.25f, 0.65f, 0.7f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        bool IsStatic => On && (Channel == 5 || Channel == 8 || Time.time < staticUntil);
        bool burstDone;

        void Update()
        {
            if (IsStatic) { Refresh(); burstDone = false; }
            else if (!burstDone) { burstDone = true; Refresh(); }

            bool blue = IsBlue;
            if (blue && !wasBlue)
            {
                L2Game g = L2Game.Instance;
                if (g != null && g.sfx != null) g.sfx.SoftClick();
            }
            wasBlue = blue;
            if (tint != null)
            {
                tintLevel = Mathf.MoveTowards(tintLevel, blue && Time.time >= staticUntil ? 1f : 0f, Time.deltaTime * 1.5f);
                tint.intensity = tintIntensity * tintLevel;
                tint.enabled = tintLevel > 0.001f;
            }
        }

        void Refresh()
        {
            Color c = On ? ChannelColor(Channel) : Off;
            if (On && Time.time < staticUntil) c = new Color(0.6f, 0.6f, 0.62f);   // scare: grey snow
            if (IsStatic) c *= Random.Range(0.55f, 1f);
            if (inst != null) inst.SetColor("_BaseColor", c);
            if (screenGlow != null)
            {
                screenGlow.enabled = On;
                screenGlow.color = c;
            }
        }
    }
}
