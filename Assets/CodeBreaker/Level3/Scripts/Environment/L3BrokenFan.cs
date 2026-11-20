using UnityEngine;

namespace CodeBreaker.Level3
{
    // A damaged wall fan: spins up, catches on its broken blade, stutters, stops, starts again.
    public class L3BrokenFan : MonoBehaviour
    {
        public Transform blades;
        public float maxSpeed = 420f;     // degrees per second
        float speed, target, change;

        void Update()
        {
            if (blades == null) return;
            change -= Time.deltaTime;
            if (change <= 0f)
            {
                float r = Random.value;
                target = r < 0.25f ? 0f : r < 0.6f ? maxSpeed * 0.3f : maxSpeed;
                change = Random.Range(1.2f, 4f);
            }
            float a = blades.localEulerAngles.z % 120f;
            float catchDrag = a > 95f && a < 115f ? 0.35f : 1f;          // the bent blade scrapes
            speed = Mathf.MoveTowards(speed, target * catchDrag, 260f * Time.deltaTime);
            blades.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);
        }
    }
}
