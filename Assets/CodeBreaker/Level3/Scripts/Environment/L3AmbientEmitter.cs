using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // A looping 3D ambience source that follows the game's SFX volume setting (Master is applied globally
    // by AudioListener.volume) and quietens while the game is paused. Starts at a random point of the
    // loop so identical loops never play in phase.
    [RequireComponent(typeof(AudioSource))]
    public class L3AmbientEmitter : MonoBehaviour
    {
        [Range(0f, 1f)] public float baseVolume = 0.3f;
        [Range(0f, 1f)] public float pausedFactor = 0.35f;
        AudioSource src;

        void Start()
        {
            src = GetComponent<AudioSource>();
            if (src.clip == null) return;
            src.loop = true;
            src.time = Random.Range(0f, src.clip.length * 0.95f);
            Apply();
            src.Play();
        }

        void Update() => Apply();

        void Apply()
        {
            if (src == null) return;
            L2Game g = L2Game.Instance;
            float k = g != null && g.Paused ? pausedFactor : 1f;
            src.volume = baseVolume * CodeBreakerSettings.SfxVolume * k;
        }
    }
}
