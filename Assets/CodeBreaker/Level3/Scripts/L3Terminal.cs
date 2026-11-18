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
            screen.text = "LOCKDOWN  ·  INSERT 3 KEYCARDS\n\n" +
                          (r ? "<color=#FF5050>[X] RED</color>    m = 9" : "[ ] RED    Maintenance (west)") + "\n" +
                          (b ? "<color=#4F8BFF>[X] BLUE</color>   s = 4" : "[ ] BLUE   Security (east)") + "\n" +
                          (c ? "<color=#5FD383>[X] GREEN</color>  c = 5" : "[ ] GREEN  Control (north)") + "\n\n" +
                          "<size=80%>Each card carries the value its wing earned:  m = " + m + ";  s = " + s + ";  c = " + cc + ";</size>";
        }
    }
}
