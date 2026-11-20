using UnityEngine;

namespace CodeBreaker.Level3
{
    // Occasional electrical sparks from damaged equipment: a short particle burst plus a light flash.
    // Sparks never collide with or damage the player.
    public class L3Sparks : MonoBehaviour
    {
        public ParticleSystem sparks;
        public Light flash;
        public Vector2 interval = new Vector2(3f, 8f);
        public Vector2Int burst = new Vector2Int(8, 18);
        public float flashIntensity = 3f;
        [Header("Sound (optional)")] public AudioSource sfx; public AudioClip sparkClip; [Range(0f, 1f)] public float volume = 0.5f;
        float next, flashT;

        void Start() { next = Time.time + Random.Range(1f, interval.y); if (flash != null) flash.intensity = 0f; }

        void Update()
        {
            if (Time.time >= next)
            {
                next = Time.time + Random.Range(interval.x, interval.y);
                if (sparks != null) sparks.Emit(Random.Range(burst.x, burst.y + 1));
                flashT = 0.12f;
                if (sfx != null && sparkClip != null) { sfx.pitch = Random.Range(0.85f, 1.2f); sfx.PlayOneShot(sparkClip, volume * CodeBreakerSettings.SfxVolume); }
            }
            if (flash != null)
            {
                flashT -= Time.deltaTime;
                flash.intensity = flashT > 0f ? flashIntensity * Random.Range(0.5f, 1f) : 0f;
            }
        }
    }
}
