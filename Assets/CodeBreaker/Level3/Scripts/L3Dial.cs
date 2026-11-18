using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The cooling unit's fan dial: E cycles 1 → 2 → 3 → 1. The reboot levers check it (fan == 3).
    // Feedback: on 3 the fan spins up (sound) and the gauge beside it turns green and reads COOLING.
    // The gauge keeps showing 86 C, because Monitor 1 and the reboot clipboard both work from
    // temp > 80 (a reading below 80 would contradict them).
    public class L3Dial : L2Interactable
    {
        public string key = "fan";
        public int min = 1, max = 3;
        public Transform knob;
        public float degreesPerStep = 60f;
        public TMP_Text label;
        public string lockedByFlag = "solved_c3";
        [Header("Gauge feedback (found next to the dial when empty)")]
        public int coolingValue = 3;
        public TMP_Text gaugeReading;
        public TMP_Text gaugeLabel;
        public L2Examine gaugeExamine;

        string readingText, labelText, examineBody;
        Color readingColor, labelColor;

        public int Value => Game != null ? Game.GetInt(key, min) : min;

        public override bool CanInteract => base.CanInteract && (Game == null || !Game.Flag(lockedByFlag));
        public override string Prompt => "Press E to Turn the Fan Dial  (now " + Value + ")";

        void Start()
        {
            Transform gauge = transform.parent != null ? transform.parent.Find("Gauge") : null;
            if (gauge != null)
            {
                if (gaugeReading == null) { Transform t = gauge.Find("Reading"); if (t != null) gaugeReading = t.GetComponent<TMP_Text>(); }
                if (gaugeLabel == null) { Transform t = gauge.Find("Label"); if (t != null) gaugeLabel = t.GetComponent<TMP_Text>(); }
                if (gaugeExamine == null) gaugeExamine = gauge.GetComponent<L2Examine>();
            }
            if (gaugeReading != null) { readingText = gaugeReading.text; readingColor = gaugeReading.color; }
            if (gaugeLabel != null) { labelText = gaugeLabel.text; labelColor = gaugeLabel.color; }
            if (gaugeExamine != null) examineBody = gaugeExamine.body;
            Show();
        }

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            int v = Value + 1;
            if (v > max) v = min;
            g.SetInt(key, v);
            if (g.sfx != null)
            {
                g.sfx.Switch(transform.position);
                if (v == coolingValue) g.sfx.Fan(transform.position);
            }
            if (v == coolingValue) g.Notify("The fan roars up to full speed. The gauge needle stops climbing.", L2NoteKind.Info);
            Show();
        }

        void Show()
        {
            int v = Value;
            if (knob != null) knob.localRotation = Quaternion.Euler(0f, 0f, -(v - min) * degreesPerStep + degreesPerStep);
            if (label != null) label.text = "FAN\n" + v;

            bool cooling = v == coolingValue;
            Color green = new Color(0.15f, 0.65f, 0.25f);
            if (gaugeReading != null && readingText != null)
            {
                gaugeReading.text = readingText;
                gaugeReading.color = cooling ? green : readingColor;
            }
            if (gaugeLabel != null && labelText != null)
            {
                gaugeLabel.text = cooling ? "COOLING" : labelText;
                gaugeLabel.color = cooling ? green : labelColor;
            }
            if (gaugeExamine != null && examineBody != null)
                gaugeExamine.body = cooling ? examineBody + "\nThe fan is on full and the needle has stopped climbing." : examineBody;
        }
    }
}
