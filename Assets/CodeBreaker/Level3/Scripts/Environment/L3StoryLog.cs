using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Ramos's maintenance log station beside the central terminal (Phase 7, optional story).
    // It reads the existing keycard flags (card_red_in / card_blue_in / card_green_in) and nothing else:
    // while fewer than three cards are in, the station is still "syncing" and shows Ramos's last
    // ordinary entry; once all three are in, the final entry appears. Reading it changes no puzzle
    // state and never blocks progress. Because it is driven by the saved card flags, a loaded game
    // shows the right state automatically.
    public class L3StoryLog : MonoBehaviour
    {
        public L2Examine log;
        public TMP_Text screen;
        public Renderer screenGlow;               // optional: the station screen quad
        public Light screenLight;

        [TextArea(3, 8)] public string syncingBody;
        [TextArea(4, 14)] public string finalBody;
        public string syncingTitle = "MAINTENANCE LOG STATION";
        public string finalTitle = "MAINTENANCE LOG  -  ENTRY 41";

        static readonly string[] CardFlags = { "card_red_in", "card_blue_in", "card_green_in" };
        int shownCount = -1;
        bool announced;

        void Update()
        {
            L2Game g = L2Game.Instance;
            if (g == null || log == null) return;
            int n = 0;
            foreach (string f in CardFlags) if (g.Flag(f)) n++;
            bool final = n >= 3;
            if (n != shownCount)
            {
                bool first = shownCount < 0;
                shownCount = n;
                log.title = final ? finalTitle : syncingTitle;
                log.body = final ? finalBody : (syncingBody ?? "").Replace("{n}", n.ToString());
                log.verb = final ? "Read the New Log Entry" : "Read the Maintenance Log";
                if (final && !first && !announced)
                {
                    announced = true;
                    g.Notify("A new entry appeared on the maintenance log station beside the terminal.", L2NoteKind.Info);
                }
                if (final) announced = true;
            }
            if (screen != null)
            {
                if (final)
                {
                    bool on = Mathf.Repeat(Time.time, 1.1f) < 0.75f;
                    screen.text = "MAINT. LOG\n<color=#FFB347>" + (on ? "1 NEW ENTRY" : " ") + "</color>\n<size=70%>R. RAMOS</size>";
                }
                else screen.text = "MAINT. LOG\n<size=80%>SYNCING...</size>\n<size=70%>CORE " + n + "/3</size>";
            }
            if (screenLight != null) screenLight.color = final ? new Color(1f, 0.7f, 0.35f) : new Color(0.45f, 0.75f, 1f);
        }
    }
}
