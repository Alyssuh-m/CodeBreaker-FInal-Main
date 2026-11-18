using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // glow stars (ceiling above the bed)
    // Bright only while the Bedroom lights are off; with the lights on they are pale stickers.
    // "Counting the stars" (flag "stars_seen", needed for jewellery-box drawer 5) is recorded when the
    // lights are off, the stars are glowing and the player looks up at them from inside the room.
    public class L2GlowStars : MonoBehaviour
    {
        public L2LightSwitch bedroomSwitch;
        public Renderer[] stars = new Renderer[0];
        [ColorUsage(false, true)] public Color glowColor = new Color(0.63f, 0.9f, 0.5f) * 3.2f;
        public Color dimBaseColor = new Color(0.55f, 0.62f, 0.5f);
        public Color darkBaseColor = new Color(0.85f, 1f, 0.7f);
        [Tooltip("How directly the player must look at the stars (degrees) and from how far (units)")]
        public float lookAngle = 30f;
        public float lookDistance = 24f;
        public float lookTime = 0.6f;

        MaterialPropertyBlock mpb;
        float level = -1f;
        float looked;

        public bool Glowing => level > 0.8f;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            Refresh(true);
        }

        void Update()
        {
            Refresh(false);
            CheckCounted();
        }

        void CheckCounted()
        {
            L2Game g = L2Game.Instance;
            if (g == null || g.Flag("stars_seen") || g.playerCamera == null || stars.Length == 0) return;
            bool seeing = false;
            if (Glowing)
            {
                Bounds b = stars[0].bounds;
                foreach (Renderer r in stars) if (r != null) b.Encapsulate(r.bounds);
                Transform c = g.playerCamera.transform;
                Vector3 d = b.center - c.position;
                seeing = d.magnitude < lookDistance && Vector3.Angle(c.forward, d) < lookAngle &&
                         L2Interactor.HasLineOfSight(c.position, b.center, g.player);
            }
            looked = seeing ? looked + Time.deltaTime : 0f;
            if (looked < lookTime) return;
            g.SetFlag("stars_seen");
            g.Notify("The glowing stars on the ceiling form a number...", L2NoteKind.Success);
            g.RefreshObjective();
        }

        void Refresh(bool force)
        {
            float target = bedroomSwitch != null && !bedroomSwitch.IsOn ? 1f : 0f;
            float next = force ? target : Mathf.MoveTowards(level, target, Time.deltaTime * 1.5f);
            if (!force && Mathf.Approximately(next, level)) return;
            level = next;
            foreach (Renderer r in stars)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", glowColor * level);
                mpb.SetColor("_BaseColor", Color.Lerp(dimBaseColor, darkBaseColor, level));
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
