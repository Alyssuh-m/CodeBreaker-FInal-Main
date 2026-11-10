using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The cooling unit's fan dial: E cycles 1 → 2 → 3 → 1. No feedback by itself;
    // the reboot levers check it (fan == 3).
    public class L3Dial : L2Interactable
    {
        public string key = "fan";
        public int min = 1, max = 3;
        public Transform knob;
        public float degreesPerStep = 60f;
        public TMP_Text label;
        public string lockedByFlag = "solved_c3";

        public int Value => Game != null ? Game.GetInt(key, min) : min;

        public override bool CanInteract => base.CanInteract && (Game == null || !Game.Flag(lockedByFlag));
        public override string Prompt => "Press E to Turn the Fan Dial  (now " + Value + ")";

        void Start() => Show();

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            int v = Value + 1;
            if (v > max) v = min;
            g.SetInt(key, v);
            if (g.sfx != null) g.sfx.Switch(transform.position);
            Show();
        }

        void Show()
        {
            int v = Value;
            if (knob != null) knob.localRotation = Quaternion.Euler(0f, 0f, -(v - min) * degreesPerStep + degreesPerStep);
            if (label != null) label.text = "FAN\n" + v;
        }
    }
}
