using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Reboot levers: records the pull order. Correct: COOLING → BACKUP → REBOOT, and when REBOOT is
    // pulled the fan dial must be on 3 and the rack count (online = 5) must have been entered.
    //  - wrong order:        levers snap back up, red lights, time penalty
    //  - fan not on 3:       "OVERHEAT", levers reset, time penalty, if/else-if hint
    //  - rack count missing: "SERVER COUNT MISSING", levers reset
    public class L3LeverSequence : MonoBehaviour
    {
        public string station = "c3";
        public string[] expected = { "COOLING", "BACKUP", "REBOOT" };
        public L3Lever[] levers = new L3Lever[3];
        public L3Dial fanDial;
        public int requiredFan = 3;
        public string rackCountStation = "c2";
        public Light alarmLight;
        public TMP_Text panelText;
        public float penalty = 10f;
        [TextArea(1, 3)] public string overheatHint = "An if / else if chain stops at the FIRST true condition.";
        [TextArea(1, 3)] public string orderHint = "Follow the REBOOT PROTOCOL clipboard on the desk, step by step.";
        public AudioClip alarmClip;
        public L3Reaction[] onSolved = new L3Reaction[0];

        readonly List<string> pulled = new List<string>();
        L3Manager M => L3Manager.Instance;
        public bool Solved => M != null && M.Solved(station);
        public bool Busy { get; private set; }

        void Start()
        {
            if (alarmLight != null) alarmLight.enabled = false;
            if (Solved) L3Reaction.RunAll(onSolved, true);
            SetPanel(Solved ? "SYSTEM ONLINE" : "EMERGENCY REBOOT", Solved ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f));
        }

        void SetPanel(string t, Color c)
        {
            if (panelText == null) return;
            panelText.text = t;
            panelText.color = c;
        }

        public void Pull(L3Lever lever)
        {
            L2Game g = L2Game.Instance;
            if (g == null || M == null || Solved || Busy) return;
            pulled.Add(lever.leverName);
            int i = pulled.Count - 1;

            if (i >= expected.Length || pulled[i] != expected[i])
            {
                Fail("Wrong order! The levers snap back up.", true, orderHint, false);
                return;
            }
            if (lever.leverName != "REBOOT")
            {
                g.Notify(lever.leverName + " lever pulled.", L2NoteKind.Info);
                return;
            }

            int fan = fanDial != null ? fanDial.Value : 0;
            if (fan != requiredFan)
            {
                Fail("OVERHEAT! The servers cook as REBOOT starts. The levers reset.", true, overheatHint, true);
                return;
            }
            if (!M.Solved(rackCountStation))
            {
                Fail("SERVER COUNT MISSING. Enter how many servers are online first. The levers reset.", false, null, false);
                return;
            }

            M.Solve(station);
            M.Good();
            SetPanel("SYSTEM ONLINE", new Color(0.4f, 1f, 0.5f));
            g.Notify("Reboot complete. The lights flicker back on and the servers hum.", L2NoteKind.Success);
            L3Reaction.RunAll(onSolved, false);
        }

        void Fail(string message, bool penalize, string hint, bool hintNow)
        {
            L2Game g = L2Game.Instance;
            M.AddWrong(station);
            M.Penalty(penalize ? penalty : 0f);
            g.Notify(message, L2NoteKind.Warning);
            if (hintNow && !string.IsNullOrEmpty(hint)) g.Notify("HINT: " + hint, L2NoteKind.Item);
            else M.ShowHintIfReady(station, hint);
            if (alarmClip != null && g.sfx != null) g.sfx.PlayAt(alarmClip, transform.position, 0.9f);
            StartCoroutine(ResetLevers(message.StartsWith("OVERHEAT") ? "OVERHEAT" : message.StartsWith("SERVER") ? "COUNT MISSING" : "WRONG ORDER"));
        }

        IEnumerator ResetLevers(string panel)
        {
            Busy = true;
            SetPanel(panel, new Color(1f, 0.3f, 0.25f));
            for (int i = 0; i < 6; i++)
            {
                if (alarmLight != null) alarmLight.enabled = i % 2 == 0;
                yield return new WaitForSeconds(0.18f);
            }
            if (alarmLight != null) alarmLight.enabled = false;
            pulled.Clear();
            foreach (L3Lever l in levers) if (l != null) l.SetDown(false);
            SetPanel("EMERGENCY REBOOT", new Color(1f, 0.35f, 0.3f));
            Busy = false;
        }
    }
}
