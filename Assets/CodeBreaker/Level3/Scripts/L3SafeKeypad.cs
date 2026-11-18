using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The switch safe: before the PIN is checked, the safe evaluates the real rule
    //   if ((isGuard || hasBadge) && !alarmOn) unlockSafe();
    // so any valid switch combination works.
    //  - all switches off:  "ACCESS DENIED: identity unknown"
    //  - alarmOn still ON:  siren, time penalty
    //  - wrong PIN:         time penalty (base keypad)
    public class L3SafeKeypad : L3Keypad
    {
        public L3ToggleSwitch isGuard, hasBadge, alarmOn;
        public AudioClip siren;

        [Header("Status before the PIN (found on the safe panel when empty)")]
        public TMP_Text statusText;
        public Light statusLight;
        string statusIdle;
        Color statusIdleColor, lightIdleColor;
        bool lastOk;

        protected override void Start()
        {
            base.Start();
            Transform panel = transform.parent;
            if (panel != null)
            {
                if (statusText == null) { Transform t = panel.Find("PanelTitle"); if (t != null) statusText = t.GetComponent<TMP_Text>(); }
                if (statusLight == null) { Transform t = panel.Find("AlarmGlow"); if (t != null) statusLight = t.GetComponent<Light>(); }
            }
            if (statusText != null) { statusIdle = statusText.text; statusIdleColor = statusText.color; }
            if (statusLight != null) lightIdleColor = statusLight.color;
            lastOk = !(CanOpen || Solved);
            ShowStatus();
        }

        void LateUpdate() => ShowStatus();

        // The switches are right: the panel turns green and says so, before the PIN is typed.
        void ShowStatus()
        {
            bool ok = CanOpen || Solved;
            if (ok == lastOk) return;
            lastOk = ok;
            if (statusText != null)
            {
                string dot = statusText.font != null && statusText.font.HasCharacter('\u00B7') ? " \u00B7 " : " - ";
                statusText.text = ok ? "IDENTITY OK" + dot + "ALARM OFF" : statusIdle;
                statusText.color = ok ? new Color(0.4f, 1f, 0.5f) : statusIdleColor;
            }
            if (statusLight != null) statusLight.color = ok ? new Color(0.2f, 1f, 0.35f) : lightIdleColor;
            if (ok && !Solved && Game != null && Time.timeSinceLevelLoad > 1f)
            {
                if (Game.sfx != null) Game.sfx.SoftClick();
                Game.Notify("The safe panel turns green: IDENTITY OK, ALARM OFF. Now the PIN.", L2NoteKind.Info);
            }
        }

        public bool CanOpen => (isGuard != null && isGuard.IsOn || hasBadge != null && hasBadge.IsOn) && !(alarmOn != null && alarmOn.IsOn);

        public override string KeypadCode
        {
            get
            {
                string s(L3ToggleSwitch t) => t != null && t.IsOn ? "<color=#6EF08A>ON</color>" : "<color=#BBBBBB>OFF</color>";
                return code + "\n\n<color=#9AA7B4>Switches now:</color>\n  isGuard  = " + s(isGuard) +
                       "\n  hasBadge = " + s(hasBadge) + "\n  alarmOn  = " + s(alarmOn);
            }
        }

        protected override bool PreCheck(out string message, out bool penalize)
        {
            penalize = false;
            message = null;
            if (CanOpen) return true;

            if (alarmOn != null && alarmOn.IsOn)
            {
                penalize = true;
                message = "SIREN! The alarm is still ON.  -" + Mathf.RoundToInt(penalty) + " s";
                L2Game g = Game;
                if (siren != null && g != null && g.sfx != null) g.sfx.PlayAt(siren, transform.position, 1f);
                return false;
            }
            message = "ACCESS DENIED: identity unknown";
            return false;
        }
    }
}
