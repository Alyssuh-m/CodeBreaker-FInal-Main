using UnityEngine;

namespace CodeBreaker.Level3
{
    // A drop of water falling from a leaking pipe joint into a small puddle, again and again.
    public class L3Drip : MonoBehaviour
    {
        public Transform drop;
        public Transform puddle;
        public float fallHeight = 8f;
        public Vector2 interval = new Vector2(1.2f, 2.6f);
        [Header("Sound (optional)")] public AudioSource sfx; public AudioClip dripClip; [Range(0f, 1f)] public float volume = 0.5f;
        Vector3 start; float t = -1f, wait, speed; Vector3 puddleScale;

        void Start()
        {
            if (drop != null) start = drop.position;
            if (puddle != null) puddleScale = puddle.localScale;
            wait = Random.Range(interval.x, interval.y);
        }

        void Update()
        {
            if (drop == null) return;
            if (t < 0f)
            {
                wait -= Time.deltaTime;
                drop.gameObject.SetActive(wait < 0.35f);                  // the drop swells at the joint first
                drop.position = start;
                if (wait <= 0f) { t = 0f; speed = 0f; }
                return;
            }
            speed += 9.8f * 3.75f * Time.deltaTime;                       // 1 m = 3.75 units
            drop.position += Vector3.down * speed * Time.deltaTime;
            t += Time.deltaTime;
            if (start.y - drop.position.y >= fallHeight)
            {
                drop.gameObject.SetActive(false);
                if (puddle != null) puddle.localScale = puddleScale * 1.06f;
                if (sfx != null && dripClip != null) { sfx.pitch = Random.Range(0.9f, 1.15f); sfx.PlayOneShot(dripClip, volume * CodeBreakerSettings.SfxVolume); }
                t = -1f; wait = Random.Range(interval.x, interval.y);
            }
            if (puddle != null) puddle.localScale = Vector3.Lerp(puddle.localScale, puddleScale, Time.deltaTime * 3f);
        }
    }
}
