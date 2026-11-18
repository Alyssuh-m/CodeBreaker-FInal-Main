using System.Collections;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The lights of one room. Unpowered = dim and flickering now and then; powered = full brightness.
    // The powered state comes from a flag (saved), so it is restored when a checkpoint is loaded.
    public class L3RoomPower : L3Reaction
    {
        public string poweredFlag = "power_maintenance";
        public Light[] lights = new Light[0];
        [Tooltip("Brightness while unpowered (fraction of full)")]
        [Range(0f, 1f)] public float dimFactor = 0.18f;
        public bool flickerWhileDim = true;
        [Tooltip("Emissive panels / lamps that glow only when powered")]
        public Renderer[] glow = new Renderer[0];
        public AudioClip powerOnSound;
        public AudioSource humLoop;

        float[] full;
        bool powered;
        float nextFlicker;

        void Awake()
        {
            full = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) full[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        void Start()
        {
            L2Game g = L2Game.Instance;
            Apply(g != null && g.Flag(poweredFlag), 1f);
            nextFlicker = Time.time + Random.Range(2f, 6f);
        }

        public override void Run(bool restoring)
        {
            L2Game g = L2Game.Instance;
            if (g != null) g.SetFlag(poweredFlag);
            if (restoring) { Apply(true, 1f); return; }
            if (powerOnSound != null && g != null && g.sfx != null) g.sfx.PlayAt(powerOnSound, transform.position, 0.9f);
            StopAllCoroutines();
            StartCoroutine(PowerUp());
        }

        IEnumerator PowerUp()
        {
            float[] steps = { 0.9f, 0.1f, 0.7f, 0.05f, 1f, 0.3f, 1f };
            foreach (float s in steps)
            {
                Apply(true, s);
                yield return new WaitForSeconds(Random.Range(0.06f, 0.16f));
            }
            Apply(true, 1f);
        }

        void Apply(bool on, float k)
        {
            powered = on;
            float f = on ? k : dimFactor;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].intensity = full[i] * f;
            foreach (Renderer r in glow) if (r != null) r.enabled = on;
            if (humLoop != null)
            {
                if (on && !humLoop.isPlaying) humLoop.Play();
                if (!on && humLoop.isPlaying) humLoop.Stop();
            }
        }

        void Update()
        {
            if (powered || !flickerWhileDim || Time.time < nextFlicker) return;
            nextFlicker = Time.time + Random.Range(2.5f, 7f);
            StartCoroutine(Flicker());
        }

        IEnumerator Flicker()
        {
            for (int n = 0; n < 3 && !powered; n++)
            {
                for (int i = 0; i < lights.Length; i++)
                    if (lights[i] != null) lights[i].intensity = full[i] * dimFactor * Random.Range(0.1f, 2.2f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
            }
            if (!powered) Apply(false, 1f);
        }
    }
}
