using System.Collections.Generic;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Phase 8: picks this playthrough's puzzle values and writes them into every clue, answer and
    // label of Level 3 before any puzzle script starts.
    //
    // Saving: the seed is stored in L2Game's numbers ("l3_seed"), so it travels inside every Level 3
    // checkpoint. L2Game restores those numbers in its Awake (order -200); this runs right after (-180),
    // so a loaded game always rebuilds exactly the same values. A loaded save from before Phase 8 has
    // no seed and keeps the original values (seed 0), so its progress still matches.
    //
    // The scene itself keeps the original values; they are only replaced while the game runs.
    [DefaultExecutionOrder(-180)]
    public class L3PuzzleValues : MonoBehaviour
    {
        public const string SeedKey = "l3_seed";

        [Tooltip("Off = new runs use the original values (lockers 8/2/5, CAM 4, PIN 42, exit 94 ...). A loaded save always keeps its own values.")]
        public bool randomize = true;
        [Tooltip("Editor only: print the answer key to the Console when Level 3 starts")]
        public bool logAnswerKeyInEditor = true;

        public L3PuzzleSet Set { get; private set; }
        public readonly List<string> Missing = new List<string>();   // objects that could not be found (for the test report)

        void Awake()
        {
            L2Game g = L2Game.Instance;
            int seed;
            if (g != null && g.HasInt(SeedKey)) seed = g.GetInt(SeedKey, 0);      // loaded game: same values as when it was saved
            else if (g != null && g.LoadedState != null) seed = 0;                 // save from before Phase 8: original values
            else seed = randomize ? NewSeed() : 0;                                 // new run
            if (g != null) g.SetInt(SeedKey, seed);
            Set = L3PuzzleSet.Generate(seed);
            L3Puzzle.Current = Set;
            Apply(Set);
#if UNITY_EDITOR
            if (logAnswerKeyInEditor) Debug.Log("[Level 3] " + Set.AnswerKey());
#endif
        }

        static int NewSeed()
        {
            int s = System.Guid.NewGuid().GetHashCode() & 0x7FFFFFFF;
            return s == 0 ? 1 : s;
        }

        // ------------------------------------------------------------------ apply to the scene
        Transform level;

        void Apply(L3PuzzleSet p)
        {
            Missing.Clear();
            GameObject root = GameObject.Find("Level3");
            level = root != null ? root.transform : transform.root;
            ApplyItems(p);
            ApplyMaintenance(p);
            ApplySecurity(p);
            ApplyControl(p);
            ApplyTerminal(p);
            ApplyStory(p);
        }

        static void ApplyItems(L3PuzzleSet p)
        {
            SetDesc(L2Items.KeycardRed, "Printed by the Maintenance Room generator. It reads:  MAINT: m = " + p.M);
            SetDesc(L2Items.KeycardBlue, "Found in the Security Room safe. It reads:  SEC: s = " + p.S);
            SetDesc(L2Items.KeycardGreen, "Printed by the Control Room after the reboot. It reads:  CTRL: c = " + p.C);
            SetDesc(L2Items.Tag2Fast, "A name tag from the pegboard:  2fast = " + p.tag2fast);
            SetDesc(L2Items.TagMyVar, "A name tag from the pegboard:  my var = " + p.tagMyVar);
            SetDesc(L2Items.TagClass, "A name tag from the pegboard:  class = " + p.tagClass);
            SetDesc(L2Items.TagFanSpeed, "A name tag from the pegboard:  fanSpeed = " + p.fanSpeed);
        }

        static void SetDesc(string id, string text)
        {
            L2ItemDef d = L2Items.Get(id);
            if (d != null) d.description = text;
        }

        void ApplyMaintenance(L3PuzzleSet p)
        {
            for (int i = 0; i < 3; i++)
            {
                L3Manager.RamosLockerValues[i] = p.lockers[i];
                Text("Maintenance/Locker" + (i + 1) + "/Inside/Number", "<size=38%>locker" + (i + 1) + " =</size>\n" + p.lockers[i]);
            }
            L3Keypad laptop = Keypad("Maintenance/Workbench/Laptop");
            if (laptop != null)
            {
                laptop.answer = p.LaptopAnswer.ToString();
                laptop.successDisplay = p.LaptopAnswer.ToString();
                laptop.successMessage = "locker2 kept " + p.LaptopAnswer + ". Copying a value doesn't link the boxes.";
            }
            Text("Maintenance/Pegboard/Tag_tag_2fast/Text", "2fast = " + p.tag2fast);
            Text("Maintenance/Pegboard/Tag_tag_myvar/Text", "my var = " + p.tagMyVar);
            Text("Maintenance/Pegboard/Tag_tag_class/Text", "class = " + p.tagClass);
            Text("Maintenance/Pegboard/Tag_tag_fanspeed/Text", "fanSpeed = " + p.fanSpeed);
            L3Keypad gen = Keypad("Maintenance/Generator/GeneratorKeypad");
            if (gen != null) { gen.answer = p.GeneratorCode; gen.successDisplay = p.GeneratorCode; }
            Text("Maintenance/GeneratorPrinter/Keycard_Red/Label", "MAINT: m = " + p.M);
        }

        void ApplySecurity(L3PuzzleSet p)
        {
            for (int i = 0; i < 6; i++)
                Text("Security/CameraWall/Feed_CAM" + (i + 1) + "/Overlay", "CAM " + (i + 1) + "\nmotion = " + p.motion[i] + "\ndoor = " + (p.doorOpen[i] ? "OPEN" : "CLOSED"));
            Transform wallT = Find("Security/CameraWall");
            L3CameraWall wall = wallT != null ? wallT.GetComponent<L3CameraWall>() : null;
            if (wall != null)
            {
                wall.correctIndex = p.cam;
                // the dark figure walks across the alarm camera's feed
                Transform holder = p.cam - 1 < wall.screens.Length ? wall.screens[p.cam - 1] : null;
                if (wall.figure != null && holder != null && wall.figure.parent != holder)
                {
                    Vector3 lp = wall.figure.localPosition; Quaternion lr = wall.figure.localRotation; Vector3 ls = wall.figure.localScale;
                    wall.figure.SetParent(holder, false);
                    wall.figure.localPosition = lp; wall.figure.localRotation = lr; wall.figure.localScale = ls;
                }
            }
            else Missing.Add("Security/CameraWall (L3CameraWall)");

            string printText = "LAST SHIFT LOG\n" + Pad(p.Line(0)) + "\n" + Pad(p.Line(1)) + "\n" + Pad(p.Line(2)) + "\n<size=70%>% = what's left over after dividing</size>";
            string printBody = "Three lines are missing their operator:\n\n" + p.Line(0) + "\n" + p.Line(1) + "\n" + p.Line(2) + "\n\n<size=80%>Legend:  % = what's left over after dividing</size>";
            Text("Security/SecurityPrinter/Printout/Text", printText);
            Examine("Security/SecurityPrinter/Printout", printBody);
            L3Keypad sym = Keypad("Security/SymbolCabinet/SymbolKeypad");
            if (sym != null) { sym.answer = p.Symbols; sym.successDisplay = p.SymbolsSpaced; }

            string mod = "(" + p.ModA + " % " + p.ModB + ")";
            Text("Security/SymbolCabinet/Inside/PinNote/PinNoteText", "PIN = cam * 10 + " + mod + "\n<size=60%>cam = the camera that saw the alarm</size>");
            Examine("Security/SymbolCabinet/Inside/PinNote", "PIN = cam * 10 + " + mod + "\n\ncam = the camera that saw the alarm.");
            L3Keypad safe = Keypad("Security/Safe/SafePanel/SafeKeypad");
            if (safe != null)
            {
                safe.code = "if ((isGuard || hasBadge) && !alarmOn) unlockSafe();\n\nPIN = cam * 10 + " + mod + "   // cam = the camera that saw the alarm\n// Enter the PIN";
                safe.answer = p.Pin.ToString();
                safe.successDisplay = p.Pin.ToString();
            }
            Text("Security/Safe/Inside/Keycard_Blue/Label", "SEC: s = " + p.S);
        }

        // keeps the printout's numbers right-aligned like the original (" 9 ? 3 = 27")
        static string Pad(string line) => line.Length > 0 && char.IsDigit(line[0]) && (line.Length < 2 || !char.IsDigit(line[1])) ? " " + line : line;

        void ApplyControl(L3PuzzleSet p)
        {
            // racks: status light, activity LEDs, glow and the "Check Server Rack" text
            Transform racks = Find("ServerRoom/Racks");
            if (racks != null)
            {
                Material gStatus = null, rStatus = null, gLed = null, rLed = null;
                foreach (Transform r in racks)
                {
                    Renderer s = r.Find("StatusLight") != null ? r.Find("StatusLight").GetComponent<Renderer>() : null;
                    Transform led = r.Find("Led");
                    Renderer l = led != null ? led.GetComponent<Renderer>() : null;
                    if (r.name.EndsWith("_Green")) { if (gStatus == null && s != null) gStatus = s.sharedMaterial; if (gLed == null && l != null) gLed = l.sharedMaterial; }
                    if (r.name.EndsWith("_Red")) { if (rStatus == null && s != null) rStatus = s.sharedMaterial; if (rLed == null && l != null) rLed = l.sharedMaterial; }
                }
                for (int n = 1; n <= 8; n++)
                {
                    Transform r = null;
                    foreach (Transform c in racks) if (c.name.StartsWith("Rack_" + n + "_")) { r = c; break; }
                    if (r == null) { Missing.Add("Rack " + n); continue; }
                    bool green = p.rackGreen[n - 1];
                    foreach (Transform c in r)
                    {
                        Renderer rr = c.GetComponent<Renderer>();
                        if (c.name == "StatusLight" && rr != null) rr.sharedMaterial = green ? gStatus : rStatus;
                        else if (c.name == "Led" && rr != null) rr.sharedMaterial = green ? gLed : rLed;
                        else if (c.name == "StatusGlow") { Light lt = c.GetComponent<Light>(); if (lt != null) lt.color = green ? new Color(0.2f, 1f, 0.35f) : new Color(1f, 0.15f, 0.1f); }
                    }
                    L2Examine ex = r.GetComponent<L2Examine>();
                    if (ex != null) ex.body = green ? "The status light on top is <color=#5FD383>GREEN</color>.\nThis server is online." : "The status light on top is <color=#F05A52>RED</color>.\nThis server is offline.";
                }
            }
            else Missing.Add("ServerRoom/Racks");

            L3Keypad console = Keypad("Control/ControlDesk/ConsoleKeypad");
            if (console != null)
            {
                console.answer = p.Online.ToString();
                console.successDisplay = "online = " + p.Online;
                console.successMessage = "Monitor 2: online = " + p.Online;
                L3SetText st = console.GetComponent<L3SetText>();
                if (st != null && st.text != null) st.text = System.Text.RegularExpressions.Regex.Replace(st.text, @"online = \d+</color>", "online = " + p.Online + "</color>");
            }
            Text("Control/ControlDesk/KeycardPrinterSlot/Keycard_Green/Label", "CTRL: c = " + p.C);
            Text("Control/CoolingUnit/Gauge/Reading", p.temp + " C");
            Examine("Control/CoolingUnit/Gauge", "The gauge reads " + p.temp + " C.");
        }

        void ApplyTerminal(L3PuzzleSet p)
        {
            L3Keypad term = Keypad("ServerRoom/CentralTerminal/TerminalKeypad");
            if (term != null)
            {
                term.code = "m = " + p.M + ";  s = " + p.S + ";  c = " + p.C + ";   // filled in by the keycards\n\nif (m > s && c != 0)\n    exitCode = m * 10 + s;\nelse\n    exitCode = 0;\n// Enter exitCode";
                term.answer = p.ExitCode.ToString();
                term.successDisplay = p.ExitCode.ToString();
                term.successMessage = "exitCode = " + p.ExitCode + ". ACCESS GRANTED. The exit door is opening!";
            }
            Slot("ServerRoom/CentralTerminal/CardSlot_RED", "m = " + p.M);
            Slot("ServerRoom/CentralTerminal/CardSlot_BLUE", "s = " + p.S);
            Slot("ServerRoom/CentralTerminal/CardSlot_GREEN", "c = " + p.C);
        }

        void ApplyStory(L3PuzzleSet p)
        {
            // Phase 6: the live figure feed uses the alarm camera; Phase 7: the notebook names it.
            var ev = FindFirstObjectByType<L3ProgressEvents>(FindObjectsInactive.Include);
            if (ev != null) ev.figureCam = p.cam - 1;
            foreach (L2Examine ex in FindObjectsByType<L2Examine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ex.name == "Story_Notebook" && ex.body != null)
                    ex.body = System.Text.RegularExpressions.Regex.Replace(ex.body, @"CAM \d", "CAM " + p.cam);
        }

        // ------------------------------------------------------------------ helpers
        Transform Find(string path)
        {
            Transform t = level != null ? level.Find(path) : null;
            return t;
        }

        void Text(string path, string text)
        {
            Transform t = Find(path);
            TMP_Text tx = t != null ? t.GetComponent<TMP_Text>() : null;
            if (tx == null) { Missing.Add(path); return; }
            tx.text = text;
        }

        void Examine(string path, string body)
        {
            Transform t = Find(path);
            L2Examine ex = t != null ? t.GetComponent<L2Examine>() : null;
            if (ex == null) { Missing.Add(path + " (examine)"); return; }
            ex.body = body;
        }

        L3Keypad Keypad(string path)
        {
            Transform t = Find(path);
            L3Keypad k = t != null ? t.GetComponent<L3Keypad>() : null;
            if (k == null) Missing.Add(path + " (keypad)");
            return k;
        }

        void Slot(string path, string value)
        {
            Transform t = Find(path);
            L3CardSlot s = t != null ? t.GetComponent<L3CardSlot>() : null;
            if (s == null) { Missing.Add(path + " (card slot)"); return; }
            s.valueText = value;
        }
    }
}
