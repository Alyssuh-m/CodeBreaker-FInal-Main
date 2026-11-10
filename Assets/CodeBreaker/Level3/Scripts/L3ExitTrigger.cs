using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Beyond the exit door: walking in here after the terminal is solved completes Level 3.
    public class L3ExitTrigger : MonoBehaviour
    {
        public string requiredStation = "term";
        public Vector3 size = new Vector3(12f, 12f, 6f);
        bool done;

        void Update()
        {
            L2Game g = L2Game.Instance;
            L3Manager m = L3Manager.Instance;
            if (done || g == null || m == null || g.player == null || g.Completed) return;
            if (!m.Solved(requiredStation)) return;
            Vector3 local = transform.InverseTransformPoint(g.player.position);
            if (Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f)
            {
                done = true;
                m.LevelFinished();
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, size);
        }
    }
}
