using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Occasional positioned sounds (relay clicks, beeps, distant alarms...). Each entry owns one pooled
    // AudioSource that is moved to a random point before playing, so nothing is spawned at runtime and
    // the same sound never stacks on itself. Follows the SFX volume setting; silent while paused.
    public class L3AmbientOneShots : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public string label;
            public AudioClip[] clips = new AudioClip[0];
            public Vector3[] points = new Vector3[0];
            public Vector2 interval = new Vector2(5f, 12f);
            public Vector2 volume = new Vector2(0.25f, 0.35f);
            public Vector2 pitch = new Vector2(0.95f, 1.05f);
            public float minDistance = 2f, maxDistance = 20f;
            [HideInInspector] public AudioSource source;
            [HideInInspector] public float next;
            [HideInInspector] public int played;
        }

        public Entry[] entries = new Entry[0];

        void Start()
        {
            foreach (Entry e in entries)
            {
                var go = new GameObject("OneShot_" + e.label);
                go.transform.SetParent(transform, false);
                e.source = go.AddComponent<AudioSource>();
                e.source.playOnAwake = false;
                e.source.spatialBlend = 1f;
                e.source.rolloffMode = AudioRolloffMode.Linear;
                e.source.minDistance = e.minDistance;
                e.source.maxDistance = e.maxDistance;
                e.source.dopplerLevel = 0f;
                e.next = Time.time + Random.Range(e.interval.x * 0.5f, e.interval.y);
            }
        }

        void Update()
        {
            L2Game g = L2Game.Instance;
            if (g != null && (g.Paused || g.Completed)) return;
            foreach (Entry e in entries)
            {
                if (e.source == null || e.clips.Length == 0 || e.points.Length == 0 || Time.time < e.next) continue;
                e.next = Time.time + Random.Range(e.interval.x, e.interval.y);
                if (e.source.isPlaying) continue;
                e.source.transform.position = e.points[Random.Range(0, e.points.Length)];
                e.source.pitch = Random.Range(e.pitch.x, e.pitch.y);
                e.source.PlayOneShot(e.clips[Random.Range(0, e.clips.Length)], Random.Range(e.volume.x, e.volume.y) * CodeBreakerSettings.SfxVolume);
                e.played++;
            }
        }

        public int TotalPlayed() { int n = 0; foreach (Entry e in entries) n += e.played; return n; }
    }
}
