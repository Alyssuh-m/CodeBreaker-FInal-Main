using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // A labelled toggle switch on the safe (isGuard, hasBadge, alarmOn). Each one is a bool,
    // saved as a number. An optional lamp shows the state (alarmOn blinks red while ON).
    public class L3ToggleSwitch : L2Interactable
    {
        public string key = "isGuard";
        public bool startsOn;
        public Transform lever;
        public float onAngle = -35f, offAngle = 35f;
        public TMP_Text stateText;
        public Renderer lamp;
        public Material lampOn, lampOff;
        public bool blinkWhenOn;
        public string lockedByFlag = "solved_s3";

        public bool IsOn
        {
            get { L2Game g = Game; return g != null ? g.GetInt("sw_" + key, startsOn ? 1 : 0) == 1 : startsOn; }
        }

        public override bool CanInteract => base.CanInteract && (Game == null || !Game.Flag(lockedByFlag));
        public override string Prompt => "Press E to Flip " + key + "  (now " + (IsOn ? "ON" : "OFF") + ")";

        void Start() => Show();

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            g.SetInt("sw_" + key, IsOn ? 0 : 1);
            if (g.sfx != null) g.sfx.Switch(transform.position);
            Show();
        }

        void Show()
        {
            bool on = IsOn;
            if (lever != null) lever.localRotation = Quaternion.Euler(on ? onAngle : offAngle, 0f, 0f);
            if (stateText != null)
            {
                stateText.text = key + "\n" + (on ? "ON" : "OFF");
                stateText.color = on ? new Color(0.45f, 1f, 0.5f) : new Color(0.75f, 0.75f, 0.75f);
            }
            if (lamp != null && !blinkWhenOn) lamp.sharedMaterial = on ? lampOn : lampOff;
        }

        void Update()
        {
            if (lamp == null || !blinkWhenOn) return;
            bool lit = IsOn && Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            lamp.sharedMaterial = lit ? lampOn : lampOff;
        }
    }
}
