using UnityEngine;
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
