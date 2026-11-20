using UnityEngine;

namespace CodeBreaker.Level3
{
    // Irregular electrical flicker for a light, optionally mirrored on a renderer's material
    // (e.g. the red glow under the cracked floor tile: "_RedOn"). Cheap: one light, no allocations.
    public class L3FlickerLight : MonoBehaviour
    {
        public Light target;
        public Renderer mirrorRenderer;
        public string mirrorProperty = "_RedOn";
        public float baseIntensity = 1f;
        [Range(0f, 1f)] public float minLevel = 0.15f;
        public Vector2 holdTime = new Vector2(0.04f, 0.35f);
        [Tooltip("Chance that a step is a long dark gap")]
        [Range(0f, 1f)] public float gapChance = 0.12f;

        MaterialPropertyBlock block;
        float level = 1f, next;

        void Awake()
        {
            if (target == null) target = GetComponent<Light>();
            if (target != null && baseIntensity <= 0f) baseIntensity = target.intensity;
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (Time.time >= next)
            {
                bool gap = Random.value < gapChance;
                level = gap ? 0f : Random.Range(minLevel, 1f);
                next = Time.time + (gap ? Random.Range(0.3f, 0.9f) : Random.Range(holdTime.x, holdTime.y));
                if (target != null) target.intensity = baseIntensity * level;
                if (mirrorRenderer != null)
                {
                    mirrorRenderer.GetPropertyBlock(block);
                    block.SetFloat(mirrorProperty, level);
                    mirrorRenderer.SetPropertyBlock(block);
                }
            }
        }
    }
}
