using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Clear plastic strip curtain. Each strip hangs from its top (its pivot) and swings away from the
    // player when the player walks through, then settles with a damped spring. No physics, no colliders:
    // the player can never get stuck. Only updates while the player is close.
    public class L3StripCurtain : MonoBehaviour
    {
        public Transform[] strips = new Transform[0];
        [Tooltip("Half thickness of the zone (local z) in which a strip reacts")]
        public float reach = 1.4f;
        [Tooltip("Largest swing angle in degrees")]
        public float maxAngle = 55f;
        public float stiffness = 30f;
        public float damping = 4.5f;
        [Tooltip("Gentle idle sway (draft from the cooling) in degrees")]
        public float idleSway = 1.2f;

        float[] angle, velocity, phase;
        Quaternion[] rest;

        void Awake()
        {
            int n = strips.Length;
            angle = new float[n]; velocity = new float[n]; phase = new float[n]; rest = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                if (strips[i] != null) rest[i] = strips[i].localRotation;
                phase[i] = Random.Range(0f, 6.28f);
            }
        }

        void Update()
        {
            L2Game g = L2Game.Instance;
            Vector3 local = Vector3.one * 999f;
            if (g != null && g.player != null) local = transform.InverseTransformPoint(g.player.position);
            bool near = Mathf.Abs(local.z) < reach + 3f;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            for (int i = 0; i < strips.Length; i++)
            {
                Transform s = strips[i];
                if (s == null) continue;
                float target = Mathf.Sin(Time.time * 0.7f + phase[i]) * idleSway;
                if (near)
                {
                    float dx = Mathf.Abs(local.x - s.localPosition.x);
                    if (dx < 1.1f && Mathf.Abs(local.z) < reach)
                    {
                        // push the strip away from the player's side
                        float push = (1f - dx / 1.1f) * (1f - Mathf.Abs(local.z) / reach);
                        target = Mathf.Sign(local.z == 0f ? 1f : local.z) * maxAngle * Mathf.Clamp01(push * 1.6f);
                    }
                }
                velocity[i] += ((target - angle[i]) * stiffness - velocity[i] * damping) * dt;
                angle[i] += velocity[i] * dt;
                s.localRotation = rest[i] * Quaternion.Euler(angle[i], 0f, 0f);
            }
        }
    }
}
