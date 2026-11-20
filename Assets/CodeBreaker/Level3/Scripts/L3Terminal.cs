using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // The central terminal's main screen. Each inserted keycard fills in its value; the question
    // appears once all three cards are in. The answer is typed on the terminal keypad (L3Keypad).
    public class L3Terminal : MonoBehaviour
    {
        public TMP_Text screen;
        public L3Keypad keypad;

        void Start() => Refresh();

        void Update()
        {
            if (Time.frameCount % 20 == 0) Refresh();
        }

        public void Refresh()
        {
            L2Game g = L2Game.Instance;
            if (screen == null || g == null) return;
            bool r = g.Flag("card_red_in"), b = g.Flag("card_blue_in"), c = g.Flag("card_green_in");
            string m = r ? "9" : "?", s = b ? "4" : "?", cc = c ? "5" : "?";
            if (L3Manager.Instance != null && L3Manager.Instance.Solved("term"))
            {
                screen.text = "<color=#6EF08A>exitCode = 94\nACCESS GRANTED\nEXIT DOOR OPEN</color>";
                return;
            }
            if (r && b && c)
            {
                screen.text = "m = 9;  s = 4;  c = 5;\n\nif (m > s && c != 0)\n    exitCode = m * 10 + s;\nelse\n    exitCode = 0;\n// Enter exitCode";
                return;
            }
            // Lockdown screen: which card comes from where, and what each card fills in.
            // Main system status screen: SYSTEM LOCKDOWN + one row per keycard (EMPTY / INSERTED).
            // The keycard flags set by L3CardSlot stay the single source of truth.
            bool blink = Mathf.FloorToInt(Time.unscaledTime * 1.4f) % 2 == 0;
            screen.text =
                "<color=" + (blink ? "#FF3B2F" : "#8A1E18") + "><b>SYSTEM LOCKDOWN</b></color>\n" +
                "<size=58%><color=#7E97A6>KEYCARD STATUS  ·  INSERT ALL 3 TO UNLOCK THE EXIT</color></size>\n\n" +
                Row("MAINTENANCE", "#FF5050", r, "m = 9") + "\n" +
                Row("SECURITY", "#4F8BFF", b, "s = 4") + "\n" +
                Row("CONTROL", "#5FD383", c, "c = 5") + "\n\n" +
                "<size=62%><color=#7E97A6>Card values:   m = " + m + ";   s = " + s + ";   c = " + cc + ";</color></size>";
        }
    
        static string Row(string wing, string hex, bool inserted, string value) =>
            "<color=" + (inserted ? hex : "#59636B") + ">" + wing + " KEYCARD</color><pos=62%>" +
            (inserted ? "<color=" + hex + "><b>[ INSERTED ]</b></color>  <size=70%><color=#9FB3BF>" + value + "</color></size>"
                      : "<color=#59636B>[ EMPTY ]</color>");
    }
}
