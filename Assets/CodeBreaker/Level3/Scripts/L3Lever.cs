using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // One lever of the red emergency panel (COOLING, BACKUP, REBOOT).
    public class L3Lever : L2Interactable
    {
        public string leverName = "COOLING";
        public L3LeverSequence sequence;
        public Transform handle;
        public float upAngle = -40f, downAngle = 40f;

        public bool Down { get; private set; }

        public override bool CanInteract => base.CanInteract && sequence != null && !sequence.Solved && !Down && !sequence.Busy;
        public override string Prompt => "Press E to Pull " + leverName;

        void Start() => SetDown(sequence != null && sequence.Solved);

        public override void Interact()
        {
            if (sequence == null) return;
            L2Game g = Game;
            if (g != null && g.sfx != null) g.sfx.Switch(transform.position);
            SetDown(true);
            sequence.Pull(this);
        }

        public void SetDown(bool down)
        {
            Down = down;
            if (handle != null) handle.localRotation = Quaternion.Euler(down ? downAngle : upAngle, 0f, 0f);
        }
    }
}
