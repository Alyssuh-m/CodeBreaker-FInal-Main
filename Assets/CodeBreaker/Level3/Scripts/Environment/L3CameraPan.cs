using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // A wall-mounted CCTV camera that slowly sweeps; when the player is close it turns to follow them.
    public class L3CameraPan : MonoBehaviour
    {
        public Transform head;
        public float sweep = 35f;
        public float period = 9f;
        public float followRange = 14f;
        public float turnSpeed = 30f;
        [Header("Motor sound (optional, looping)")] public AudioSource motor; [Range(0f, 1f)] public float motorVolume = 0.35f;
        Quaternion lastRot; float motorLevel;
        Quaternion rest; float phase;

        void Start() { if (head != null) rest = head.localRotation; phase = Random.Range(0f, 6.28f); }

        void LateUpdate()
        {
            if (head == null || motor == null) return;
            float speed = Quaternion.Angle(lastRot, head.rotation) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastRot = head.rotation;
            motorLevel = Mathf.MoveTowards(motorLevel, Mathf.Clamp01((speed - 4f) / 20f), Time.deltaTime * 3f);
            motor.volume = motorLevel * motorVolume * CodeBreakerSettings.SfxVolume;
            if (motorLevel > 0.01f && !motor.isPlaying) motor.Play();
            if (motorLevel <= 0.01f && motor.isPlaying) motor.Stop();
        }

        void Update()
        {
            if (head == null) return;
            Quaternion want = rest * Quaternion.Euler(0f, Mathf.Sin(Time.time * 6.283f / period + phase) * sweep, 0f);
            L2Game g = L2Game.Instance;
            if (g != null && g.player != null && Vector3.Distance(g.player.position, head.position) < followRange)
            {
                Vector3 d = g.player.position + Vector3.up * 5.5f - head.position;
                if (d.sqrMagnitude > 0.01f) want = Quaternion.LookRotation(d, Vector3.up);
                head.rotation = Quaternion.RotateTowards(head.rotation, want, turnSpeed * Time.deltaTime);
                return;
            }
            head.localRotation = Quaternion.RotateTowards(head.localRotation, want, turnSpeed * Time.deltaTime);
        }
    }
}
