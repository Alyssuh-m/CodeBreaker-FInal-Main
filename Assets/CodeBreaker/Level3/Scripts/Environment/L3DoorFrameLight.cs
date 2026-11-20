using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Coloured light strips around a wing doorway. They show the wing colour until that wing's keycard
    // has been inserted at the central terminal (existing flag set by L3CardSlot), then fade smoothly to white.
    // Read only: never changes game state or door access.
    public class L3DoorFrameLight : MonoBehaviour
    {
        public string insertedFlag = "card_red_in";
        public Color wingColor = Color.red;
        public Color doneColor = new Color(0.9f, 0.95f, 1f);
        public Renderer[] strips = new Renderer[0];
        public Light glow;
        public float emission = 2.4f;
        public float fadeTime = 2.5f;
        [Tooltip("Brightness of the unlit strips")] public float baseBoost = 1.6f;
        float k = -1f;
        MaterialPropertyBlock block;

        void Awake() { block = new MaterialPropertyBlock(); }

        void Update()
        {
            L2Game g = L2Game.Instance;
            bool done = g != null && g.Flag(insertedFlag);
            float target = done ? 1f : 0f;
            if (k < 0f) k = target;                                   // loaded from a checkpoint: no fade
            else k = Mathf.MoveTowards(k, target, Time.deltaTime / fadeTime);
            Color c = Color.Lerp(wingColor, doneColor, Mathf.SmoothStep(0f, 1f, k));
            foreach (Renderer r in strips)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c * baseBoost);
                block.SetColor("_EmissionColor", c * emission);
                r.SetPropertyBlock(block);
            }
            if (glow != null) glow.color = c;
        }
    }
}
