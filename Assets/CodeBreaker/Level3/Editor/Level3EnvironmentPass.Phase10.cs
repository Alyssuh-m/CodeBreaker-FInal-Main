using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CodeBreaker.Level2;
using Object = UnityEngine.Object;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 10: final testing.
    //  - Check10 (edit mode): missing scripts / references, scene statistics, files changed outside Level 3.
    //  - PlayTest10 (play mode): a complete run of Level 3 from the start to the exit, using the same calls the
    //    player's E / F keys make (Interact / UseItem / keypad Submit), with this run's randomised values.
    public static partial class Level3EnvironmentPass
    {
        static string Dir10 => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase10");

        [MenuItem("Code Breaker/Level 3/Phase 10 - Static Checks")]
        public static void Check10()
        {
            if (!OpenLevel3()) return;
            var sb = new StringBuilder();
            Transform level = FindLevelRoot();
            var scene = level.gameObject.scene;
            int missing = 0; var missingList = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0) { missing += n; if (missingList.Count < 20) missingList.Add(PathOf(t)); }
                }
            sb.AppendLine("Missing scripts in Level 3 scene: " + missing + (missing == 0 ? " (PASS)" : " (FAIL) " + string.Join(", ", missingList)));

            // key references of the new systems
            var refs = new List<string>();
            void Need(bool ok, string what) { if (!ok) refs.Add(what); }
            var ev = level.GetComponentInChildren<L3ProgressEvents>(true);
            Need(ev != null, "L3ProgressEvents");
            if (ev != null) { Need(ev.figure != null, "events.figure"); Need(ev.siren != null && ev.siren.clip != null, "events.siren"); Need(ev.emergencyRoot != null, "events.emergencyRoot"); Need(ev.feedCam != null, "events.feedCam"); Need(ev.camScreens.Length == 6 && Array.TrueForAll(ev.camScreens, r => r != null), "events.camScreens"); }
            var esc = level.GetComponentInChildren<L3EscapeSequence>(true);
            Need(esc != null, "L3EscapeSequence");
            if (esc != null) { Need(esc.terminalKeypad != null, "escape.terminalKeypad"); Need(esc.events != null, "escape.events"); Need(esc.steam.Length == 4, "escape.steam"); Need(esc.steamHiss != null && esc.sirenCut != null && esc.lockRelease != null, "escape sounds"); }
            var story = level.GetComponentInChildren<L3StoryLog>(true);
            Need(story != null && story.log != null && story.screen != null, "L3StoryLog");
            Need(level.GetComponentInChildren<L3PuzzleValues>(true) != null, "L3PuzzleValues");
            foreach (var a in level.GetComponentsInChildren<L3AmbientEmitter>(true)) { var s = a.GetComponent<AudioSource>(); Need(s != null && s.clip != null, "ambient loop " + a.name); }
            foreach (var a in level.GetComponentsInChildren<L3AmbientOneShots>(true)) foreach (var e in a.entries) Need(e.clips.Length > 0 && Array.TrueForAll(e.clips, c => c != null), "one-shot " + e.label);
            int nullAudio = 0; foreach (var s in level.GetComponentsInChildren<AudioSource>(true)) if (s.clip == null && s.playOnAwake) nullAudio++;
            Need(nullAudio == 0, nullAudio + " AudioSources set to play without a clip");
            int nullMat = 0; foreach (var r in level.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m == null && !(r is ParticleSystemRenderer)) nullMat++;
            Need(nullMat == 0, nullMat + " renderer slots with no material");
            sb.AppendLine("Key references of the new systems: " + (refs.Count == 0 ? "all set (PASS)" : "missing: " + string.Join(", ", refs) + " (FAIL)"));

            // scene statistics
            int lights = 0, shadowLights = 0, rt = 0, ps = 0, audio = 0, renderers = 0;
            foreach (var l in level.GetComponentsInChildren<Light>(true)) { lights++; if (l.shadows != LightShadows.None) shadowLights++; }
            foreach (var r in level.GetComponentsInChildren<Renderer>(true)) renderers++;
            ps = level.GetComponentsInChildren<ParticleSystem>(true).Length;
            audio = level.GetComponentsInChildren<AudioSource>(true).Length;
            foreach (var c in level.GetComponentsInChildren<Camera>(true)) if (c.targetTexture != null) rt++;
            sb.AppendLine("Scene: " + renderers + " renderers, " + lights + " lights (" + shadowLights + " casting shadows), " + ps + " particle systems, " + audio + " audio sources, " + rt + " render-texture camera (off until the CCTV event)");
            var groups = new List<string>();
            Transform env = level.Find("L3_Environment");
            if (env != null) foreach (Transform t in env) groups.Add(t.name + " (" + t.GetComponentsInChildren<Transform>(true).Length + " objects)");
            sb.AppendLine("Environment groups: " + string.Join(", ", groups));

            // files changed since the Level 3 backup, outside Level 3
            DateTime backup = DateTime.MaxValue;
            string bdir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Backups", "2026-11-20 before Level 3 phases");
            if (Directory.Exists(bdir)) foreach (string f in Directory.GetFiles(bdir, "*", SearchOption.AllDirectories)) { DateTime w = File.GetLastWriteTime(f); if (w < backup) backup = w; }
            DateTime after = backup.AddMinutes(5);
            var outside = new List<string>(); var inside = new List<string>();
            foreach (string f in Directory.GetFiles(Application.dataPath, "*", SearchOption.AllDirectories))
            {
                if (f.EndsWith(".meta")) continue;
                if (File.GetLastWriteTime(f) < after) continue;
                string rel = "Assets" + f.Substring(Application.dataPath.Length).Replace('\\', '/');
                bool l3 = rel.StartsWith("Assets/CodeBreaker/Level3/") || rel == "Assets/Scenes/Level 3.unity";
                (l3 ? inside : outside).Add(rel);
            }
            sb.AppendLine("Backup taken: " + backup.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("Files changed since then OUTSIDE Level 3: " + (outside.Count == 0 ? "none (PASS: Level 1, Level 2 and shared scripts untouched)" : outside.Count + "\n  " + string.Join("\n  ", outside)));
            inside.Sort();
            sb.AppendLine("\nLevel 3 files created or changed since the backup (" + inside.Count + "):\n  " + string.Join("\n  ", inside));
            Directory.CreateDirectory(Dir10);
            File.WriteAllText(Path.Combine(Dir10, "static_checks.txt"), sb.ToString());
            Debug.Log("[Level 3 Env] Phase 10 static checks written.");
        }

        // ================================================================== full play-through (Phase 10)
        class PlayTest10
        {
            readonly StringBuilder r = new StringBuilder();
            IEnumerator run; float waitUntil; int fails, passes; bool finished;
            int errors; readonly List<string> errorLines = new List<string>();
            float frameTime; int frames; float fpsMin = 999f;
            int prefBackup; bool hadPref; string saveBackup; bool hadSave;
            L2Game g; L3Manager M; L3PuzzleSet P; Vector3 home; Quaternion homeRot;

            public PlayTest10() { Application.logMessageReceived += OnLog; run = Run(); }

            void OnLog(string msg, string stack, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (msg.Contains("generators.ai.unity.com") || msg.Contains("UserNotInOrganization")) return;   // Unity AI account messages, not the game
                errors++; if (errorLines.Count < 15) errorLines.Add(type + ": " + msg.Split('\n')[0]);
            }

            void Ok(string what, bool ok) { r.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what); if (ok) passes++; else fails++; }
            void Say(string s) => r.AppendLine(s);
            void Cap(string n) => ScreenCapture.CaptureScreenshot(Path.Combine(Dir10, n + ".png"));

            static T Find<T>(Func<T, bool> pred) where T : Object
            {
                foreach (T o in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (pred(o)) return o;
                return null;
            }
            static T At<T>(string path) where T : Component { GameObject r = GameObject.Find("Level3"); Transform t = r != null ? r.transform.Find(path) : null; return t != null ? t.GetComponent<T>() : null; }

            bool Pick(string item)
            {
                L2Pickup p = Find<L2Pickup>(x => x.itemId == item && x.isActiveAndEnabled);
                if (p == null) return false;
                p.Interact();
                return g.HasItem(item) || g.IsCollected(item);
            }

            void Go(Vector3 p) => g.Teleport(new Vector3(p.x, home.y, p.z), homeRot);

            public bool Step()
            {
                if (!EditorApplication.isPlaying) return true;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f && Time.frameCount != lastFrame) { lastFrame = Time.frameCount; frameTime += dt; frames++; if (frames > 30) fpsMin = Mathf.Min(fpsMin, 1f / dt); }
                if (Time.time < waitUntil) return false;
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception e) { Say("EXCEPTION in the test: " + e.Message); fails++; more = false; }
                if (!more) return Done();
                if (run.Current is float f) waitUntil = Time.time + f;
                return false;
            }
            int lastFrame;

            IEnumerator Run()
            {
                g = L2Game.Instance; M = L3Manager.Instance; P = L3Puzzle.Current;
                hadPref = PlayerPrefs.HasKey(L3Manager.PrefCompleted); prefBackup = PlayerPrefs.GetInt(L3Manager.PrefCompleted, 0);
                hadSave = File.Exists(SaveSystem.FilePath); saveBackup = hadSave ? File.ReadAllText(SaveSystem.FilePath) : null;
                Say("Level 3 full play-through  (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
                Say("Save file and the Level 3 'completed' preference were backed up and are restored at the end.\n");
                Say(P.AnswerKey());
                home = g.player.position; homeRot = g.player.rotation;
                foreach (string b in new[] { "brief_l3_main", "brief_l3_maint", "brief_l3_sec", "brief_l3_ctrl" }) g.SetFlag(b);
                float t0 = M.Remaining;

                // ---------------------------------------------------------------- start
                Say("START (main hall)");
                L3Terminal term = Object.FindFirstObjectByType<L3Terminal>(); term.Refresh();
                Ok("Status screen shows SYSTEM LOCKDOWN with 3 EMPTY slots", term.screen.text.Contains("LOCKDOWN") && CountOf(term.screen.text, "EMPTY") == 3);
                Ok("8 server racks present", Find<Transform>(x => x.name == "Racks") != null && Find<Transform>(x => x.name == "Racks").childCount == 8);
                Ok("Flashlight picked up", Pick(L2Items.Flashlight));
                M.ShowHint(); yield return 0.3f;
                Ok("H opens a hint (costs time: " + Mathf.RoundToInt(t0 - M.Remaining) + " s)", g.ui.AnyModalOpen && M.Remaining < t0);
                g.ui.CloseModal(); M.ShowJournal(); yield return 0.3f;
                Ok("J opens the notes journal", g.ui.AnyModalOpen); g.ui.CloseModal();
                Cap("01_start_terminal");

                // ---------------------------------------------------------------- maintenance
                Say("\nMAINTENANCE (RED)");
                Go(new Vector3(-40f, 0f, 0f)); yield return 1.0f;
                foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (o.label.StartsWith("locker") && !o.IsOpen) o.Interact();
                yield return 1.2f;
                Ok("Opened Ramos's 3 lockers, values noted (" + M.RamosLockerLine(g) + ")", M.RamosLockersSeen(g) == 3);
                L3Keypad laptop = At<L3Keypad>("Maintenance/Workbench/Laptop");
                Ok("Laptop: wrong answer rejected", !laptop.Submit(P.lockers[2].ToString(), out _));
                Ok("Laptop: " + P.LaptopAnswer + " accepted", laptop.Submit(P.LaptopAnswer.ToString(), out _) && M.Solved("m1"));
                Pick(L2Items.Tag2Fast); yield return 0.3f;
                L3NameTagLock nameLock = Find<L3NameTagLock>(x => true);
                Ok("Red locker: tag '2fast' rejected (returned to the pegboard)", !nameLock.UseItem(L2Items.Tag2Fast) && !g.HasItem(L2Items.Tag2Fast));
                yield return 0.5f;
                Pick(L2Items.TagFanSpeed); yield return 0.3f;
                Ok("Red locker: tag 'fanSpeed' accepted", nameLock.UseItem(L2Items.TagFanSpeed) && M.Solved("m2"));
                yield return 1.0f;
                L3FuseBox box = Object.FindFirstObjectByType<L3FuseBox>();
                L3Button test = Find<L3Button>(x => x.fuseBox != null);
                test.Interact(); yield return 0.5f;
                Ok("Fuse box: TEST before the swap fails", !M.Solved("m3"));
                L3FuseSocket sx = box.sockets[0], sy = box.sockets[1], st = box.sockets[2];
                sx.Interact(); yield return 0.2f; st.UseItem(L2Items.Fuse2); yield return 0.2f;     // temp = x
                sy.Interact(); yield return 0.2f; sx.UseItem(L2Items.Fuse9); yield return 0.2f;     // x = y
                st.Interact(); yield return 0.2f; sy.UseItem(L2Items.Fuse2); yield return 0.2f;     // y = temp
                test.Interact(); yield return 0.5f;
                Ok("Fuse box: temp = x; x = y; y = temp; then TEST (x = " + box.Get(0) + ", y = " + box.Get(1) + ")", M.Solved("m3"));
                L3Keypad gen = At<L3Keypad>("Maintenance/Generator/GeneratorKeypad");
                Ok("Generator: start code " + P.GeneratorCode + " accepted", gen.Submit(P.GeneratorCode, out _) && M.Solved("gen"));
                yield return 3.0f;
                Shot(Dir10, "02_maintenance_generator_running", new Vector3(-34f, 6.4f, -2f), new Vector3(-29f, 4f, 9f), 66f);
                Ok("RED keycard printed and picked up", Pick(L2Items.KeycardRed));
                yield return 1.0f;
                SaveData d1 = SaveSystem.Load();
                Ok("Checkpoint saved after the 1st keycard (" + (d1 != null ? d1.checkpointLabel : "none") + ")", d1 != null && SaveSystem.HasCheckpoint(d1, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name) && g.CheckpointIndex == 1);
                Ok("Inventory text matches the card: '" + L2Items.Get(L2Items.KeycardRed).description + "'", L2Items.Get(L2Items.KeycardRed).description.Contains("m = " + P.M));

                Go(home); yield return 0.8f;
                At<L3CardSlot>("ServerRoom/CentralTerminal/CardSlot_RED").Interact(); yield return 1.0f;
                term.Refresh();
                Ok("RED card inserted, status screen shows it", g.Flag("card_red_in") && term.screen.text.Contains("INSERTED") && term.screen.text.Contains("m = " + P.M));
                L3ProgressEvents ev = Object.FindFirstObjectByType<L3ProgressEvents>();
                Ok("Horror event 1 reached (flicker; the figure waits until a doorway is in view)", ev != null && ev.EventsReached >= 1);
                L3DoorFrameLight fRed = Find<L3DoorFrameLight>(x => x.insertedFlag == "card_red_in");
                yield return 3.0f;
                Ok("Maintenance door frame turned white" + (fRed == null ? " (frame not found)" : ""), fRed != null && DoorFrameWhite(fRed));

                // ---------------------------------------------------------------- security
                Say("\nSECURITY (BLUE)");
                Go(new Vector3(40f, 0f, -2f)); yield return 1.0f;
                L3CameraWall wall = Object.FindFirstObjectByType<L3CameraWall>();
                int wrongCam = P.cam % 6 + 1;
                Find<L3Button>(x => x.cameraWall != null && x.cameraIndex == wrongCam).Interact(); yield return 0.5f;
                Ok("Camera wall: CAM " + wrongCam + " rejected", !M.Solved("s1"));
                Find<L3Button>(x => x.cameraWall != null && x.cameraIndex == P.cam).Interact(); yield return 4.5f;
                Ok("Camera wall: CAM " + P.cam + " accepted (dark figure crosses the feed)", M.Solved("s1"));
                Shot(Dir10, "03_security_camera_wall", new Vector3(41f, 6.7f, 1.5f), new Vector3(41f, 6.7f, 11f), 62f);
                Find<L3Printer>(x => true).Interact(); yield return 2.0f;
                Ok("Shift log printed", g.Flag("sec_printed"));
                L3Keypad sym = At<L3Keypad>("Security/SymbolCabinet/SymbolKeypad");
                Ok("Cabinet: wrong symbols rejected", !sym.Submit("+-+", out _));
                Ok("Cabinet: " + P.Symbols + " accepted", sym.Submit(P.Symbols, out _) && M.Solved("s2"));
                yield return 1.0f;
                L3SafeKeypad safe = Object.FindFirstObjectByType<L3SafeKeypad>();
                Ok("Safe: PIN refused while the switches are wrong", !safe.Submit(P.Pin.ToString(), out string why) && !M.Solved("s3"));
                foreach (L3ToggleSwitch sw in Object.FindObjectsByType<L3ToggleSwitch>(FindObjectsSortMode.None))
                {
                    if (sw.key == "isGuard" && !sw.IsOn) sw.Interact();
                    if (sw.key == "alarmOn" && sw.IsOn) sw.Interact();
                }
                yield return 0.3f;
                Ok("Safe: isGuard ON + alarmOn OFF + PIN " + P.Pin + " accepted", safe.Submit(P.Pin.ToString(), out _) && M.Solved("s3"));
                yield return 2.0f;
                Ok("BLUE keycard picked up", Pick(L2Items.KeycardBlue));
                yield return 1.0f;
                Ok("Checkpoint saved after the 2nd keycard", g.CheckpointIndex == 2);

                Go(home); yield return 0.8f;
                At<L3CardSlot>("ServerRoom/CentralTerminal/CardSlot_BLUE").Interact(); yield return 4.0f;
                Ok("BLUE card inserted", g.Flag("card_blue_in"));
                Ok("Horror event 2: red emergency lighting + siren", ev.EmergencyActive && ev.siren.isPlaying);
                Shot(Dir10, "04_hall_emergency", new Vector3(-19f, 7.6f, -19f), new Vector3(2f, 3f, 6f), 74f);

                // ---------------------------------------------------------------- control
                Say("\nCONTROL (GREEN)");
                Go(new Vector3(0f, 0f, 33f)); yield return 1.0f;
                L3Keypad console = At<L3Keypad>("Control/ControlDesk/ConsoleKeypad");
                Ok("Console: wrong count rejected", !console.Submit((P.Online + 1).ToString(), out _));
                Ok("Console: online = " + P.Online + " accepted", console.Submit(P.Online.ToString(), out _) && M.Solved("c2"));
                L3Dial dial = Object.FindFirstObjectByType<L3Dial>();
                L3LeverSequence seq = Object.FindFirstObjectByType<L3LeverSequence>();
                L3Lever Lever(string n) => Array.Find(seq.levers, l => l != null && l.leverName == n);
                Lever("REBOOT").Interact(); yield return 2.5f;
                Ok("Levers: wrong order (REBOOT first) resets", !M.Solved("c3"));
                for (int i = 0; i < 3 && dial.Value != 3; i++) { dial.Interact(); yield return 0.2f; }
                Ok("Fan dial set to 3 (gauge " + P.temp + " C > 80)", dial.Value == 3);
                Lever("COOLING").Interact(); yield return 0.4f;
                Lever("BACKUP").Interact(); yield return 0.4f;
                Lever("REBOOT").Interact(); yield return 1.0f;
                Ok("Levers: COOLING -> BACKUP -> REBOOT, system online", M.Solved("c3"));
                yield return 2.5f;
                Shot(Dir10, "05_control_system_online", new Vector3(0f, 6.5f, 30f), new Vector3(0f, 5f, 44f), 70f);
                Ok("GREEN keycard picked up", Pick(L2Items.KeycardGreen));
                yield return 1.0f;
                Ok("Checkpoint saved after the 3rd keycard", g.CheckpointIndex == 3);

                Go(home); yield return 0.8f;
                At<L3CardSlot>("ServerRoom/CentralTerminal/CardSlot_GREEN").Interact(); yield return 3.0f;
                term.Refresh();
                Ok("GREEN card inserted, terminal shows the question with m = " + P.M + ", s = " + P.S + ", c = " + P.C, g.Flag("card_green_in") && term.screen.text.Contains("m = " + P.M + ";  s = " + P.S + ";  c = " + P.C));
                Ok("Horror event 3: CCTV feeds to static / live figure feed", ev.EventsReached == 3);
                L3StoryLog storyLog = Object.FindFirstObjectByType<L3StoryLog>();
                Ok("Ramos's final log entry is now available", storyLog != null && storyLog.log.body.Contains("They locked me in too"));
                Ok("Events never repeat (l3evt_1/2/3 set once)", g.Flag("l3evt_1") && g.Flag("l3evt_2") && g.Flag("l3evt_3"));
                Cap("06_terminal_question");
                yield return 9.0f;

                // ---------------------------------------------------------------- exit
                Say("\nEXIT");
                L3Keypad tk = At<L3Keypad>("ServerRoom/CentralTerminal/TerminalKeypad");
                Ok("Terminal: wrong exit code rejected", !tk.Submit("0", out _));
                Ok("Terminal: exit code " + P.ExitCode + " accepted (ACCESS GRANTED)", tk.Submit(P.ExitCode.ToString(), out _) && M.Solved("term"));
                yield return 1.0f;
                Ok("Siren stopped", !ev.siren.isPlaying);
                yield return 6.0f;
                L3SlideDoor door = Object.FindFirstObjectByType<L3SlideDoor>();
                Ok("Exit door open, red lighting gone", door.leaf.localPosition.x > 6f && !ev.EmergencyActive);
                Shot(Dir10, "07_exit_open", new Vector3(5f, 6.4f, -15f), new Vector3(0f, 4.4f, -26f), 70f);
                Go(new Vector3(0f, 0f, -29.5f)); yield return 2.0f;
                Ok("Walked out: Level 3 complete", g.Completed);
                Cap("08_level_complete");
                yield return 1.0f;
                Say("\nTime left on the 40:00 clock: " + LevelTimer.Format(M.Remaining) + " (wrong answers in this test cost time on purpose)");
            }

            static int CountOf(string s, string w) { int n = 0, i = 0; while ((i = s.IndexOf(w, i, StringComparison.Ordinal)) >= 0) { n++; i += w.Length; } return n; }

            static bool DoorFrameWhite(L3DoorFrameLight f)
            {
                Light l = f.glow;
                return l != null && l.color.r > 0.7f && l.color.g > 0.7f && l.color.b > 0.85f;
            }

            bool Done()
            {
                if (finished) return true;
                finished = true;
                Application.logMessageReceived -= OnLog;
                Say("\nCONSOLE: " + errors + " error(s) / exception(s) from the game during the run" + (errors == 0 ? " (PASS)" : " (FAIL)"));
                foreach (string e in errorLines) Say("  " + e);
                if (errors > 0) fails++; else passes++;
                float avg = frames > 0 ? frames / Mathf.Max(0.001f, frameTime) : 0f;
                Say("PERFORMANCE (inside the Unity editor, Game view 2560x1440): average " + avg.ToString("F0") + " fps over " + frames + " frames, slowest frame " + (fpsMin < 999f ? fpsMin.ToString("F0") : "?") + " fps");
                if (hadPref) PlayerPrefs.SetInt(L3Manager.PrefCompleted, prefBackup); else PlayerPrefs.DeleteKey(L3Manager.PrefCompleted);
                PlayerPrefs.Save();
                if (hadSave) File.WriteAllText(SaveSystem.FilePath, saveBackup); else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
                Say("Save file and preference restored.");
                Say("\nRESULT: " + passes + " passed, " + fails + " failed");
                File.WriteAllText(Path.Combine(Dir10, "playthrough_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 10 play-through:\n" + r);
                return true;
            }
        }
        // ------------------------------------------------------------------ frame-rate probe (play mode)
        static float perfStart = -1f, perfSum; static int perfFrames, perfLastFrame; static List<float> perfDts = new List<float>();
        static string perfLabel = "with all phases";
        static GameObject perfHidden;
        static void StartPerfBase10()
        {
            GameObject r = GameObject.Find("Level3"); Transform env = r != null ? r.transform.Find("L3_Environment") : null;
            if (env == null) return;
            perfHidden = env.gameObject; perfHidden.SetActive(false); perfLabel = "L3_Environment switched OFF (original Level 3 only)";
            StartPerf10(true);
        }
        static void StartPerf10() => StartPerf10(false);
        static void StartPerf10(bool keepLabel) { if (!keepLabel) perfLabel = "with all phases"; perfStart = -1f; perfSum = 0f; perfFrames = 0; perfDts.Clear(); EditorApplication.update -= Perf10Tick; EditorApplication.update += Perf10Tick; }
        static void Perf10Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Perf10Tick; return; }
            if (Time.frameCount == perfLastFrame) return;
            perfLastFrame = Time.frameCount;
            if (perfStart < 0f) { perfStart = Time.realtimeSinceStartup; return; }
            float dt = Time.unscaledDeltaTime; perfSum += dt; perfFrames++; perfDts.Add(dt);
            if (Time.realtimeSinceStartup - perfStart < 12f) return;
            EditorApplication.update -= Perf10Tick;
            perfDts.Sort(); float p99 = perfDts[Mathf.Min(perfDts.Count - 1, Mathf.FloorToInt(perfDts.Count * 0.99f))];
            if (perfHidden != null) { perfHidden.SetActive(true); perfHidden = null; }
            string line = "Frame-rate probe [" + perfLabel + "] (12 s, main hall, Game view " + Screen.width + "x" + Screen.height + ", editor focused = " + UnityEditorInternal.InternalEditorUtility.isApplicationActive + "): average " +
                          (perfFrames / perfSum).ToString("F0") + " fps, 1% low " + (1f / p99).ToString("F0") + " fps, " + perfFrames + " frames";
            Directory.CreateDirectory(Dir10);
            File.AppendAllText(Path.Combine(Dir10, "performance.txt"), line + "\n");
            Debug.Log("[Level 3 Env] " + line);
        }

        static PlayTest10 test10;
        static void Test10Tick()
        {
            if (test10 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test10Tick; test10 = null; return; }
            if (test10.Step()) { EditorApplication.update -= Test10Tick; test10 = null; }
        }
        static void StartPlayTest10(string dir) { if (test10 != null) return; Directory.CreateDirectory(Dir10); test10 = new PlayTest10(); EditorApplication.update += Test10Tick; }
    }
}
