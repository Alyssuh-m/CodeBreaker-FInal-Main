using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 8: puzzle randomisation.
    //  - Build8: adds "Phase8_Randomizer" (L3PuzzleValues) under L3_Environment. The scene's own puzzle texts are not edited.
    //  - Check8: generates 1000 seeds and checks every solvability rule (L3PuzzleSet.Validate).
    //  - PlayTest8: in Play mode, checks the scene matches the values, then saves / reloads at several stages.
    public static partial class Level3EnvironmentPass
    {
        const string Group8 = "Phase8_Randomizer";

        static string PathOf(Transform t) { var sb = new StringBuilder(t.name); while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); } return sb.ToString(); }
        static string Esc(string s) => s == null ? "null" : s.Replace("\n", "\\n");

        [MenuItem("Code Breaker/Level 3/Phase 8 - Build Randomizer")]
        public static void Build8()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(Group8);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(Group8); go.transform.SetParent(env, false);
            var pv = go.AddComponent<L3PuzzleValues>();
            pv.randomize = true; pv.logAnswerKeyInEditor = true;
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase8");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), "Phase8_Randomizer added (L3PuzzleValues, randomize = true, answer key in the Editor console).\nScene puzzle texts unchanged (they hold the original values; replaced at runtime).\n");
            Debug.Log("[Level 3 Env] Phase 8 built and saved.");
        }

        [MenuItem("Code Breaker/Level 3/Phase 8 - Undo (remove Randomizer)")]
        public static void UndoPhase8()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform old = level != null ? level.Find("L3_Environment/" + Group8) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 8 removed (original values everywhere).");
        }

        public static void Dump8()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            var sb = new StringBuilder();
            foreach (var t in level.GetComponentsInChildren<TMP_Text>(true))
                if (!PathOf(t.transform).Contains("L3_Environment") && System.Text.RegularExpressions.Regex.IsMatch(t.text ?? "", "[0-9]"))
                    sb.AppendLine("TEXT " + PathOf(t.transform) + " = " + Esc(t.text));
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase8");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "scene_dump.txt"), sb.ToString());
        }

        [MenuItem("Code Breaker/Level 3/Phase 8 - Check 1000 Seeds")]
        public static void Check8()
        {
            var sb = new StringBuilder();
            int bad = 0;
            var camCount = new int[7]; var onlineCount = new int[6]; var orders = new Dictionary<string, int>();
            var exits = new HashSet<int>(); var pins = new HashSet<int>(); var gens = new HashSet<string>();
            int minTemp = 999, maxTemp = 0;
            List<string> classic = L3PuzzleSet.Validate(L3PuzzleSet.Classic);
            sb.AppendLine("Classic (seed 0) valid: " + (classic.Count == 0) + "  exit " + L3PuzzleSet.Classic.ExitCode + ", PIN " + L3PuzzleSet.Classic.Pin + ", generator " + L3PuzzleSet.Classic.GeneratorCode);
            var rng = new System.Random(20261120);
            for (int i = 0; i < 1000; i++)
            {
                int seed = i < 500 ? i + 1 : rng.Next(1, int.MaxValue);
                L3PuzzleSet p = L3PuzzleSet.Generate(seed);
                List<string> e = L3PuzzleSet.Validate(p);
                bool fellBack = p == L3PuzzleSet.Classic;
                if (e.Count > 0 || fellBack) { bad++; sb.AppendLine("seed " + seed + ": " + (fellBack ? "fell back to classic " : "") + string.Join("; ", e)); }
                // determinism: the same seed must give the same set
                if (L3PuzzleSet.Generate(seed).AnswerKey() != p.AnswerKey()) { bad++; sb.AppendLine("seed " + seed + ": not deterministic"); }
                camCount[p.cam]++; onlineCount[p.Online]++;
                orders[p.Symbols] = orders.TryGetValue(p.Symbols, out int c) ? c + 1 : 1;
                exits.Add(p.ExitCode); pins.Add(p.Pin); gens.Add(p.GeneratorCode);
                minTemp = Mathf.Min(minTemp, p.temp); maxTemp = Mathf.Max(maxTemp, p.temp);
            }
            sb.Insert(0, "Seeds checked: 1000   failures: " + bad + (bad == 0 ? "  (PASS)" : "  (FAIL)") + "\n");
            sb.AppendLine("Alarm camera  1..6: " + string.Join(" / ", new[] { camCount[1], camCount[2], camCount[3], camCount[4], camCount[5], camCount[6] }));
            sb.AppendLine("Servers online 3/4/5: " + onlineCount[3] + " / " + onlineCount[4] + " / " + onlineCount[5]);
            var ord = new List<string>(); foreach (var kv in orders) ord.Add(kv.Key + "=" + kv.Value);
            sb.AppendLine("Operator orders: " + string.Join("  ", ord));
            sb.AppendLine("Different exit codes: " + exits.Count + ", PINs: " + pins.Count + ", generator codes: " + gens.Count + ", gauge " + minTemp + ".." + maxTemp + " C");
            sb.AppendLine("\nThree example runs:");
            foreach (int s in new[] { 11, 222, 3333 }) sb.AppendLine(L3PuzzleSet.Generate(s).AnswerKey());
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase8");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "seed_check.txt"), sb.ToString());
            Debug.Log("[Level 3 Env] Phase 8 seed check:\n" + sb);
        }

        // ------------------------------------------------------------------ scene consistency (Play mode)
        static string T(string path) { GameObject r = GameObject.Find("Level3"); Transform t = r != null ? r.transform.Find(path) : null; TMP_Text x = t != null ? t.GetComponent<TMP_Text>() : null; return x != null ? x.text : null; }
        static T C<T>(string path) where T : Component { GameObject r = GameObject.Find("Level3"); Transform t = r != null ? r.transform.Find(path) : null; return t != null ? t.GetComponent<T>() : null; }

        static List<string> VerifyScene(L3PuzzleSet p)
        {
            var e = new List<string>();
            void Has(string what, string text, string expect) { if (text == null || !text.Contains(expect)) e.Add(what + ": expected '" + expect + "', found '" + Esc(text) + "'"); }
            void Eq(string what, string got, string expect) { if (got != expect) e.Add(what + ": expected " + expect + ", found " + got); }
            for (int i = 0; i < 3; i++) Has("locker" + (i + 1) + " card", T("Maintenance/Locker" + (i + 1) + "/Inside/Number"), "\n" + p.lockers[i]);
            for (int i = 0; i < 3; i++) Eq("RamosLockerValues[" + i + "]", L3Manager.RamosLockerValues[i].ToString(), p.lockers[i].ToString());
            Eq("laptop answer", C<L3Keypad>("Maintenance/Workbench/Laptop")?.answer, p.LaptopAnswer.ToString());
            Has("fanSpeed tag", T("Maintenance/Pegboard/Tag_tag_fanspeed/Text"), "fanSpeed = " + p.fanSpeed);
            Has("fanSpeed item", L2Items.Get(L2Items.TagFanSpeed).description, "fanSpeed = " + p.fanSpeed);
            Eq("generator answer", C<L3Keypad>("Maintenance/Generator/GeneratorKeypad")?.answer, p.GeneratorCode);
            int alarms = 0, alarmCam = 0;
            for (int i = 0; i < 6; i++)
            {
                string o = T("Security/CameraWall/Feed_CAM" + (i + 1) + "/Overlay") ?? "";
                var mm = System.Text.RegularExpressions.Regex.Match(o, @"motion = (\d+)\ndoor = (OPEN|CLOSED)");
                if (!mm.Success) { e.Add("CAM " + (i + 1) + " overlay unreadable: " + Esc(o)); continue; }
                if (int.Parse(mm.Groups[1].Value) > 3 && mm.Groups[2].Value == "OPEN") { alarms++; alarmCam = i + 1; }
            }
            if (alarms != 1 || alarmCam != p.cam) e.Add("camera wall overlays give " + alarms + " alarm cameras (CAM " + alarmCam + "), expected CAM " + p.cam);
            var wall = C<L3CameraWall>("Security/CameraWall");
            Eq("camera wall correctIndex", wall != null ? wall.correctIndex.ToString() : "none", p.cam.ToString());
            Eq("dark figure on feed", wall != null && wall.figure != null ? wall.figure.parent.name : "none", "Feed_CAM" + p.cam);
            for (int i = 0; i < 3; i++) Has("printout line " + (i + 1), T("Security/SecurityPrinter/Printout/Text"), p.Line(i));
            Eq("cabinet answer", C<L3Keypad>("Security/SymbolCabinet/SymbolKeypad")?.answer, p.Symbols);
            Has("PIN note", T("Security/SymbolCabinet/Inside/PinNote/PinNoteText"), "(" + p.ModA + " % " + p.ModB + ")");
            Eq("safe answer", C<L3Keypad>("Security/Safe/SafePanel/SafeKeypad")?.answer, p.Pin.ToString());
            Has("blue card", T("Security/Safe/Inside/Keycard_Blue/Label"), "s = " + p.S);
            int green = 0;
            GameObject root = GameObject.Find("Level3");
            Transform racks = root != null ? root.transform.Find("ServerRoom/Racks") : null;
            if (racks != null)
                foreach (Transform r in racks)
                {
                    Renderer s = r.Find("StatusLight") != null ? r.Find("StatusLight").GetComponent<Renderer>() : null;
                    if (s != null && s.sharedMaterial != null && s.sharedMaterial.name.Contains("Green")) green++;
                    var ex = r.GetComponent<L2Examine>();
                    bool lightGreen = s != null && s.sharedMaterial != null && s.sharedMaterial.name.Contains("Green");
                    if (ex != null && ex.body.Contains("GREEN") != lightGreen) e.Add(r.name + ": examine text and light disagree");
                }
            Eq("green rack lights", green.ToString(), p.Online.ToString());
            Eq("console answer", C<L3Keypad>("Control/ControlDesk/ConsoleKeypad")?.answer, p.Online.ToString());
            Has("gauge", T("Control/CoolingUnit/Gauge/Reading"), p.temp + " C");
            Has("green card", T("Control/ControlDesk/KeycardPrinterSlot/Keycard_Green/Label"), "c = " + p.C);
            Eq("terminal answer", C<L3Keypad>("ServerRoom/CentralTerminal/TerminalKeypad")?.answer, p.ExitCode.ToString());
            Has("terminal code", C<L3Keypad>("ServerRoom/CentralTerminal/TerminalKeypad")?.code, "m = " + p.M + ";  s = " + p.S + ";  c = " + p.C + ";");
            Eq("blue slot", C<L3CardSlot>("ServerRoom/CentralTerminal/CardSlot_BLUE")?.valueText, "s = " + p.S);
            var ev = Object.FindFirstObjectByType<L3ProgressEvents>();
            if (ev != null) Eq("Phase 6 figure camera", (ev.figureCam + 1).ToString(), p.cam.ToString());
            var pv = Object.FindFirstObjectByType<L3PuzzleValues>();
            if (pv != null && pv.Missing.Count > 0) e.Add("objects not found: " + string.Join(", ", pv.Missing));
            return e;
        }

        // ================================================================== play-mode self test (Phase 8)
        class PlayTest8
        {
            readonly string dir; readonly StringBuilder r = new StringBuilder();
            int stage; float timer; double last = -1; L2Game lastGame; string backup; bool hadSave;
            int seedA; string keyA; int fails;
            public PlayTest8(string d) { dir = d; }

            bool Ready(out L2Game g, out L3PuzzleValues pv)
            {
                g = L2Game.Instance; pv = Object.FindFirstObjectByType<L3PuzzleValues>();
                return g != null && pv != null && pv.Set != null && timer > 2.5f && !SceneLoader.IsLoading;
            }

            void Check(string label, L3PuzzleSet p)
            {
                List<string> e = VerifyScene(p);
                List<string> v = L3PuzzleSet.Validate(p);
                if (v.Count > 0) e.Add("set invalid: " + string.Join("; ", v));
                r.AppendLine(label + ": scene matches the values " + (e.Count == 0 ? "(PASS)" : "(FAIL)"));
                foreach (string x in e) r.AppendLine("    " + x);
                if (e.Count > 0) fails++;
            }

            void Shots(string tag)
            {
                Shot(dir, tag + "_camera_wall", new Vector3(41f, 6.7f, 1.5f), new Vector3(41f, 6.7f, 11f), 62f);
                Shot(dir, tag + "_name_tags", new Vector3(-54f, 6.5f, 6.8f), new Vector3(-54f, 6.4f, 11f), 50f);
                Shot(dir, tag + "_racks_west", new Vector3(-20f, 7.6f, -20f), new Vector3(-6f, 4.5f, 6f), 64f);
                Shot(dir, tag + "_racks_east", new Vector3(20f, 7.6f, -20f), new Vector3(6f, 4.5f, 6f), 64f);
            }

            public bool Step()
            {
                double now = EditorApplication.timeSinceStartup; if (last >= 0) timer += (float)(now - last); last = now;
                L2Game g; L3PuzzleValues pv;
                if (!Ready(out g, out pv)) return false;
                if (stage > 0 && ReferenceEquals(g, lastGame)) { if (timer > 60f) { r.AppendLine("FAIL: scene did not reload"); fails++; return Done(g); } return false; }
                lastGame = g;
                L3PuzzleSet p = pv.Set;
                switch (stage)
                {
                    case 0:
                        hadSave = File.Exists(SaveSystem.FilePath);
                        backup = hadSave ? File.ReadAllText(SaveSystem.FilePath) : null;
                        r.AppendLine("Save file backed up (" + (hadSave ? "existing save" : "no save yet") + "); it is restored at the end.");
                        seedA = p.seed; keyA = p.AnswerKey();
                        r.AppendLine("\nRUN A  (new run)\n" + keyA);
                        Check("Run A, level start", p);
                        Shots("A");
                        g.SaveProgress("Phase 8 test");
                        r.AppendLine("Saved at level start. Reloading with Try Again...");
                        g.TryAgainFromCheckpoint(); stage = 1; timer = 0f; return false;
                    case 1:
                        r.AppendLine("Reload 1 (level start): seed " + p.seed + (p.seed == seedA && p.AnswerKey() == keyA ? " same values (PASS)" : " DIFFERENT (FAIL)"));
                        if (p.seed != seedA) fails++;
                        Check("Reload 1", p);
                        foreach (string f in new[] { "solved_m1", "solved_m2", "solved_m3", "solved_gen", "solved_s1" }) g.SetFlag(f);
                        g.SaveProgress("Phase 8 test");
                        r.AppendLine("Solved the whole Maintenance wing + the camera wall (flags), saved, reloading...");
                        g.TryAgainFromCheckpoint(); stage = 2; timer = 0f; return false;
                    case 2:
                        bool kept = g.Flag("solved_gen") && g.Flag("solved_s1");
                        r.AppendLine("Reload 2 (mid-progress): seed " + p.seed + (p.seed == seedA ? " same values" : " DIFFERENT") + ", progress kept " + kept + (p.seed == seedA && kept ? " (PASS)" : " (FAIL)"));
                        if (p.seed != seedA || !kept) fails++;
                        Check("Reload 2", p);
                        foreach (string f in new[] { "solved_s2", "solved_s3", "card_red_in", "card_blue_in" }) g.SetFlag(f);
                        g.SaveProgress("Phase 8 test");
                        r.AppendLine("Solved Security + inserted 2 keycards (flags), saved, reloading...");
                        g.TryAgainFromCheckpoint(); stage = 3; timer = 0f; return false;
                    case 3:
                        bool kept2 = g.Flag("solved_s3") && g.Flag("card_blue_in");
                        L3Terminal term = Object.FindFirstObjectByType<L3Terminal>();
                        string scr = term != null && term.screen != null ? term.screen.text : "";
                        bool termOk = scr.Contains("m = " + p.M) && scr.Contains("s = " + p.S);
                        r.AppendLine("Reload 3 (2 keycards in): seed " + p.seed + (p.seed == seedA ? " same values" : " DIFFERENT") + ", progress kept " + kept2 + ", terminal shows m = " + p.M + " / s = " + p.S + " " + termOk + (p.seed == seedA && kept2 && termOk ? " (PASS)" : " (FAIL)"));
                        if (p.seed != seedA || !kept2 || !termOk) fails++;
                        Check("Reload 3", p);
                        // an old save (made before Phase 8) has no seed: remove it from the saved checkpoint
                        SaveData d = SaveSystem.Load();
                        L2State s = JsonUtility.FromJson<L2State>(d.checkpointJson);
                        int removed = s.ints.RemoveAll(x => x.key == L3PuzzleValues.SeedKey);
                        d.checkpointJson = JsonUtility.ToJson(s); SaveSystem.Save(d);
                        r.AppendLine("Simulated a save from before Phase 8 (seed removed from the checkpoint: " + removed + "), reloading...");
                        g.TryAgainFromCheckpoint(); stage = 4; timer = 0f; return false;
                    case 4:
                        bool classic = p.seed == 0 && p.AnswerKey() == L3PuzzleSet.Classic.AnswerKey() && g.Flag("solved_s3");
                        r.AppendLine("Reload 4 (old save): seed " + p.seed + ", original values " + (p.seed == 0) + ", progress kept " + g.Flag("solved_s3") + (classic ? " (PASS)" : " (FAIL)"));
                        if (!classic) fails++;
                        Check("Reload 4 (original values)", p);
                        g.StartFromBeginning();
                        r.AppendLine("Restarted Level 3 from the beginning (new run)...");
                        stage = 5; timer = 0f; return false;
                    case 5:
                        bool fresh = p.seed != seedA && p.seed != 0 && !g.Flag("solved_m1");
                        r.AppendLine("\nRUN B  (restart = new run): seed " + p.seed + ", different from run A " + (p.seed != seedA) + ", no progress " + !g.Flag("solved_m1") + (fresh ? " (PASS)" : " (FAIL)"));
                        if (!fresh) fails++;
                        r.AppendLine(p.AnswerKey());
                        Check("Run B, level start", p);
                        Shots("B");
                        return Done(g);
                }
                return Done(g);
            }

            bool Done(L2Game g)
            {
                if (hadSave) File.WriteAllText(SaveSystem.FilePath, backup);
                else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
                r.AppendLine("\nSave file restored to how it was before the test.");
                r.AppendLine("RESULT: " + (fails == 0 ? "all checks passed" : fails + " check(s) failed"));
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 8 play test:\n" + r);
                return true;
            }
        }
        static PlayTest8 test8;
        static void Test8Tick()
        {
            if (test8 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test8Tick; test8 = null; return; }
            if (test8.Step()) { EditorApplication.update -= Test8Tick; test8 = null; }
        }
        static void StartPlayTest8(string dir) { if (test8 != null) return; Directory.CreateDirectory(dir); test8 = new PlayTest8(dir); EditorApplication.update += Test8Tick; }
    }
}
