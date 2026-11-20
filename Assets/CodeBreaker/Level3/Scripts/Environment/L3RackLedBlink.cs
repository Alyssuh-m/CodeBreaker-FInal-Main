using UnityEngine;

namespace CodeBreaker.Level3
{
    // Gives the small activity LEDs on the server racks individual blink rhythms (some amber).
    // Only the small "Led" lights are touched; the big status light used by the puzzle is never changed.
    public class L3RackLedBlink : MonoBehaviour
    {
        public Renderer[] leds = new Renderer[0];
        [Range(0f, 1f)] public float amberChance = 0.15f;
        Color[] baseCol; float[] rate, phase, duty; bool[] state;
        MaterialPropertyBlock block;

        void Awake()
        {
            int n = leds.Length;
            baseCol = new Color[n]; rate = new float[n]; phase = new float[n]; duty = new float[n]; state = new bool[n];
            block = new MaterialPropertyBlock();
            for (int i = 0; i < n; i++)
            {
                Material m = leds[i] != null ? leds[i].sharedMaterial : null;
                baseCol[i] = m != null && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.green;
                if (Random.value < amberChance) baseCol[i] = new Color(2.2f, 1.2f, 0.15f);
                rate[i] = Random.Range(0.4f, 3.5f);
                phase[i] = Random.value;
                duty[i] = Random.Range(0.25f, 0.85f);
                state[i] = true;
            }
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < leds.Length; i++)
            {
                Renderer r = leds[i];
                if (r == null) continue;
                bool on = Mathf.Repeat(t * rate[i] + phase[i], 1f) < duty[i];
                if (on == state[i]) continue;
                state[i] = on;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", on ? baseCol[i] : baseCol[i] * 0.06f);
                r.SetPropertyBlock(block);
            }
        }

        public int LitCount() { int n = 0; foreach (bool s in state) if (s) n++; return n; }
    }
}
