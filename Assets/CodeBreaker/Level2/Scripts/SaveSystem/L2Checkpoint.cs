using UnityEngine;

namespace CodeBreaker.Level2
{
    // A checkpoint in the hallway / corridor just beyond a room's exit door.
    // It saves once the exit has been opened (requiredFlag) and the player walks into this area.
    // Loading the save puts the player back here, facing the same way as this transform.
    public class L2Checkpoint : MonoBehaviour
    {
        public int index = 1;
        public string label = "Hall B";
        [Tooltip("Flag that must be set first (the room's exit door was opened)")]
        public string requiredFlag = "exit_living";
        [Tooltip("Size of the trigger area in local space (Unity units)")]
        public Vector3 size = new Vector3(6f, 10f, 5f);
        [Tooltip("Off = only saved when a level script calls L2Game.ReachCheckpoint (e.g. Level 3 keycards)")]
        public bool autoTrigger = true;

        bool done;

        void Update()
        {
            L2Game g = L2Game.Instance;
            if (!autoTrigger || done || g == null || g.player == null) return;
            if (g.CheckpointIndex >= index) { done = true; return; }
            if (!g.Flag(requiredFlag)) return;

            Vector3 local = transform.InverseTransformPoint(g.player.position);
            if (Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f)
            {
                done = true;
                g.ReachCheckpoint(this);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 2f);
        }
    }
}
