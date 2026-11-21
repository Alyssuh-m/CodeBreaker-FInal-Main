using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 9: final escape sequence. Builds "Phase9_Escape" (exit door frame strips, steam vents, green fill
    // lights, exit corridor light) and an L3EscapeSequence that wraps the terminal keypad's existing door reaction.
    // The keypad, its answer, the exit door (L3SlideDoor) and the exit trigger are the existing ones.
    public static partial class Level3EnvironmentPass
    {
        const string Group9 = "Phase9_Escape";

        [MenuItem("Code Breaker/Level 3/Phase 9 - Build Escape Sequence")]
        public static void Build9()
        {
            if (!OpenLevel3()) return;
            log.Length = 0; mats.Clear();
            EnsureFolders();
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(Group9);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform g = new GameObject(Group9).transform; g.SetParent(env, false);
            var seq = g.gameObject.AddComponent<L3EscapeSequence>();

            Transform tk = level.Find("ServerRoom/CentralTerminal/TerminalKeypad");
            seq.terminalKeypad = tk != null ? tk.GetComponent<L3Keypad>() : null;
            seq.events = level.GetComponentInChildren<L3ProgressEvents>(true);
            if (seq.terminalKeypad == null) log.AppendLine("WARNING: terminal keypad not found");
            if (seq.events == null) log.AppendLine("WARNING: Phase 6 events not found (siren / emergency light steps will be skipped)");

            // ---- exit door frame: red strips that turn green (hall side of the south wall)
            const float face = 22.5f - 0.45f - 0.04f;
            Material strip = Lit("M_L3_ExitFrameStrip", Color.white, 0.2f, 0f, null, Vector2.one, new Color(1f, 1f, 1f));
            Transform fr = Group("ExitFrame", g, Vector3.zero);
            var frame = new List<Renderer>();
            frame.Add(Box("Strip_Left", fr, new Vector3(-3.3f, 4.6f, -face), new Vector3(0.16f, 9.2f, 0.06f), strip).GetComponent<Renderer>());
            frame.Add(Box("Strip_Right", fr, new Vector3(3.3f, 4.6f, -face), new Vector3(0.16f, 9.2f, 0.06f), strip).GetComponent<Renderer>());
            frame.Add(Box("Strip_Top", fr, new Vector3(0f, 9.2f, -face), new Vector3(6.76f, 0.16f, 0.06f), strip).GetComponent<Renderer>());
            seq.exitFrame = frame.ToArray();
            seq.exitFrameLights = new[] { PointLight(fr, "FrameGlow_L", new Vector3(-3.3f, 3.5f, -face + 0.6f), new Color(1f, 0.12f, 0.08f), 2.5f, 6f),
                                          PointLight(fr, "FrameGlow_R", new Vector3(3.3f, 3.5f, -face + 0.6f), new Color(1f, 0.12f, 0.08f), 2.5f, 6f) };

            // ---- steam vents either side of the door (floor vents shoot up, ceiling vents blow down)
            Material grille = Lit("M_L3_VentGrille", new Color(0.18f, 0.18f, 0.19f), 0.4f, 0.8f);
            var steam = new List<ParticleSystem>();
            foreach (float x in new[] { -4.4f, 4.4f })
            {
                Box("FloorVent", g, new Vector3(x, 0.03f, -face + 0.7f), new Vector3(1.0f, 0.06f, 0.7f), grille);
                ParticleSystem up = Steam(g, "Steam_Floor" + (x < 0 ? "_L" : "_R"), new Vector3(x, 0.1f, -face + 0.7f),
                                          Quaternion.LookRotation(new Vector3(-Mathf.Sign(x) * 0.25f, 1f, 0.15f)), 3f, 3.2f);
                Burst(up);
                steam.Add(up);
                ParticleSystem down = Steam(g, "Steam_Top" + (x < 0 ? "_L" : "_R"), new Vector3(x * 0.8f, 9.6f, -face + 0.4f),
                                            Quaternion.LookRotation(new Vector3(-Mathf.Sign(x) * 0.3f, -1f, 0.35f)), 2.4f, 2.4f);
                Burst(down);
                steam.Add(down);
            }
            seq.steam = steam.ToArray();

            // ---- green fill lights for the hall (off until the sequence) and the exit corridor light
            seq.greenFill = new[] { PointLight(g, "GreenFill_South", new Vector3(0f, 9.5f, -12f), new Color(0.25f, 1f, 0.45f), 9f, 30f),
                                    PointLight(g, "GreenFill_North", new Vector3(0f, 9.5f, 8f), new Color(0.25f, 1f, 0.45f), 7f, 30f) };
            seq.corridorLight = PointLight(g, "ExitCorridorLight", new Vector3(0f, 8f, -28.5f), new Color(0.75f, 1f, 0.82f), 14f, 16f);

            seq.steamHiss = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Env/L3A_SteamHiss.wav");
            seq.sirenCut = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Env/L3A_RelayClick.wav");
            seq.lockRelease = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Env/L3A_MechanicalClank.wav");

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            int doorReactions = seq.terminalKeypad != null ? seq.terminalKeypad.onSolved.Length : 0;
            log.AppendLine("Escape sequence wraps the terminal keypad's existing on-solved reaction(s): " + doorReactions + " (the exit door, L3SlideDoor)");
            log.AppendLine("Terminal answer unchanged in the scene: " + (seq.terminalKeypad != null ? seq.terminalKeypad.answer : "?") + " (Phase 8 sets the run's value at runtime)");
            log.AppendLine("Exit frame strips (red -> green), 2 floor + 2 ceiling steam vents, 2 green hall fills, exit corridor light");
            log.AppendLine("Sounds: siren cut = L3A_RelayClick, lock release = L3A_MechanicalClank, steam = L3A_SteamHiss, door = existing heavy door sound");
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase9");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), log.ToString());
            Debug.Log("[Level 3 Env] Phase 9 built and saved.\n" + log);
        }

        static void Burst(ParticleSystem p)
        {
            var main = p.main;
            main.playOnAwake = false; main.loop = true; main.maxParticles = 140;
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.9f);
            main.startColor = new Color(0.88f, 0.92f, 0.96f, 0.34f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            var sh = p.shape; sh.angle = 18f; sh.radius = 0.25f;
        }

        [MenuItem("Code Breaker/Level 3/Phase 9 - Undo (remove Escape Sequence)")]
        public static void UndoPhase9()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform old = level != null ? level.Find("L3_Environment/" + Group9) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 9 removed (the door opens straight away again).");
        }

        // ================================================================== play-mode self test (Phase 9)
        class PlayTest9
        {
            readonly string dir; readonly StringBuilder r = new StringBuilder();
            int phase, fails; float timer, phaseStart; bool shot2, shot3, movedAside;
            L2Game g; L3Keypad term; L3EscapeSequence seq; L3ProgressEvents ev; L3SlideDoor door; Vector3 leaf0, home; Quaternion homeRot;
            int prefBackup; bool hadPref; string saveBackup; bool hadSave;
            bool sirenBefore, sirenStopped, sirenChecked; int maxSteam; Color frameEnd;
            public PlayTest9(string d) { dir = d; }
            void Cap(string n) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png"));
            void Res(string what, bool ok) { r.AppendLine(what + (ok ? " (PASS)" : " (FAIL)")); if (!ok) fails++; }
            Vector3 Aside() => new Vector3(14f, home.y, -8f);
            Vector3 Leaf => door != null && door.leaf != null ? door.leaf.localPosition : Vector3.zero;

            public bool Step()
            {
                timer = Time.time - phaseStart;          // game time: the sequence itself runs on game time
                switch (phase)
                {
                    case 0:
                        g = L2Game.Instance;
                        term = Object.FindFirstObjectByType<L3Terminal>() != null ? Object.FindFirstObjectByType<L3Terminal>().GetComponentInChildren<L3Keypad>(true) : null;
                        if (term == null) { GameObject t = GameObject.Find("Level3"); if (t != null) { Transform k = t.transform.Find("ServerRoom/CentralTerminal/TerminalKeypad"); if (k != null) term = k.GetComponent<L3Keypad>(); } }
                        seq = Object.FindFirstObjectByType<L3EscapeSequence>(); ev = Object.FindFirstObjectByType<L3ProgressEvents>(); door = Object.FindFirstObjectByType<L3SlideDoor>();
                        if (g == null || term == null || seq == null || door == null) { r.AppendLine("FAIL: missing game / terminal keypad / sequence / door"); fails++; return Done(); }
                        hadPref = PlayerPrefs.HasKey(L3Manager.PrefCompleted); prefBackup = PlayerPrefs.GetInt(L3Manager.PrefCompleted, 0);
                        hadSave = File.Exists(SaveSystem.FilePath); saveBackup = hadSave ? File.ReadAllText(SaveSystem.FilePath) : null;
                        r.AppendLine("Backed up the save file and the Level 3 'completed' preference (restored at the end).");
                        r.AppendLine("Terminal keypad answer this run: " + term.answer + "  (exit code = m * 10 + s with this run's values; 94 with the original values)");
                        r.AppendLine("Terminal keypad on-solved now: " + term.onSolved.Length + " reaction (" + (term.onSolved.Length > 0 && term.onSolved[0] != null ? term.onSolved[0].GetType().Name : "none") + ")");
                        home = g.player.position; homeRot = g.player.rotation; leaf0 = Leaf;
                        foreach (string b in new[] { "brief_l3_main", "brief_l3_maint", "brief_l3_sec", "brief_l3_ctrl" }) g.SetFlag(b);
                        foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) g.SetFlag(c);
                        g.Teleport(Aside(), homeRot);      // keep the player's body out of the exit camera shots
                        phase = 1; phaseStart = Time.time; return false;
                    case 1:                                  // emergency state is on
                        if (timer < 5f) return false;
                        sirenBefore = ev != null && ev.siren != null && ev.siren.isPlaying;
                        r.AppendLine("Before solving: emergency " + (ev != null && ev.EmergencyActive) + ", siren playing " + sirenBefore);
                        Shot(dir, "E0_before_exit_locked", new Vector3(0f, 6.4f, -15.5f), new Vector3(0f, 4.6f, -22.5f), 70f);
                        // door must stay shut and the exit must not complete before the answer
                        g.Teleport(new Vector3(0f, home.y, -29.5f), homeRot);
                        phase = 2; phaseStart = Time.time; return false;
                    case 2:
                        if (timer < 1.2f) return false;
                        Res("Before the answer: door closed " + ((Leaf - leaf0).magnitude < 0.01f) + ", level not completed from the exit area " + !g.Completed, (Leaf - leaf0).magnitude < 0.01f && !g.Completed);
                        g.Teleport(home, homeRot);
                        bool wrong = term.Submit("0", out string m0);
                        Res("Wrong code '0' rejected (" + m0 + "), sequence not started " + !seq.Ran, !wrong && !seq.Ran);
                        phase = 3; phaseStart = Time.time; return false;
                    case 3:
                        if (timer < 1f) return false;
                        bool ok = term.Submit(term.answer, out string msg);
                        Res("Correct code accepted: '" + msg + "', sequence started " + seq.Ran, ok && seq.Ran);
                        L3Terminal tt = Object.FindFirstObjectByType<L3Terminal>(); if (tt != null) tt.Refresh();
                        Res("Terminal screen: " + (tt != null && tt.screen != null && tt.screen.text.Contains("ACCESS GRANTED") ? "ACCESS GRANTED" : "missing"), tt != null && tt.screen != null && tt.screen.text.Contains("ACCESS GRANTED"));
                        Cap("E1_access_granted");
                        phase = 4; phaseStart = Time.time; return false;
                    case 4:
                        if (!movedAside && timer > 0.3f) { movedAside = true; g.Teleport(Aside(), homeRot); }
                        if (!sirenChecked && timer > 0.9f) { sirenChecked = true; sirenStopped = ev == null || ev.siren == null || !ev.siren.isPlaying; Res("Siren stopped at 0.9 s: " + sirenStopped, sirenStopped); }
                        foreach (ParticleSystem p in seq.steam) if (p != null) maxSteam = Mathf.Max(maxSteam, p.particleCount);
                        if (!shot2 && timer > 1.7f) { shot2 = true; Shot(dir, "E2_red_to_green", new Vector3(0f, 6.4f, -15.5f), new Vector3(0f, 4.6f, -22.5f), 70f); }
                        if (!shot3 && timer > 2.7f) { shot3 = true; Shot(dir, "E3_steam_door_opening", new Vector3(0f, 6.4f, -15.5f), new Vector3(0f, 4.6f, -22.5f), 70f); }
                        if (timer < 6.5f) return false;
                        Shot(dir, "E4_exit_open", new Vector3(0f, 6.4f, -15.5f), new Vector3(0f, 4.4f, -28f), 70f);
                        Res("Steam vented (max particles " + maxSteam + ")", maxSteam > 10);
                        Res("Door slid open: moved " + (Leaf - leaf0).magnitude.ToString("F2") + " of " + door.openOffset.magnitude.ToString("F2"), Mathf.Abs((Leaf - leaf0).magnitude - door.openOffset.magnitude) < 0.05f);
                        Res("Emergency lighting off, green state: emergency " + (ev != null && ev.EmergencyActive) + ", green fills on " + (seq.greenFill.Length > 0 && seq.greenFill[0].enabled), (ev == null || !ev.EmergencyActive) && seq.greenFill.Length > 0 && seq.greenFill[0].enabled);
                        // again: must not run twice
                        bool again = term.Submit(term.answer, out string m2);
                        Res("Second submit: already solved, sequence not restarted (door still fully open)", again && Mathf.Abs((Leaf - leaf0).magnitude - door.openOffset.magnitude) < 0.05f);
                        g.Teleport(new Vector3(0f, home.y, -29.5f), Quaternion.identity);
                        phase = 5; phaseStart = Time.time; return false;
                    case 5:
                        if (timer < 2f) return false;
                        Res("Walked through the exit: level completed " + g.Completed, g.Completed);
                        Cap("E5_level_complete");
                        phase = 6; phaseStart = Time.time; return false;
                    case 6:
                        if (timer < 1f) return false;
                        return Done();
                }
                return Done();
            }

            bool Done()
            {
                if (hadPref) PlayerPrefs.SetInt(L3Manager.PrefCompleted, prefBackup); else PlayerPrefs.DeleteKey(L3Manager.PrefCompleted);
                PlayerPrefs.Save();
                if (hadSave) File.WriteAllText(SaveSystem.FilePath, saveBackup); else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
                r.AppendLine("Save file and Level 3 'completed' preference restored.");
                r.AppendLine("RESULT: " + (fails == 0 ? "all checks passed" : fails + " check(s) failed"));
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 9 play test:\n" + r);
                return true;
            }
        }
        static PlayTest9 test9;
        static void Test9Tick()
        {
            if (test9 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test9Tick; test9 = null; return; }
            if (test9.Step()) { EditorApplication.update -= Test9Tick; test9 = null; }
        }
        static void StartPlayTest9(string dir) { if (test9 != null) return; Directory.CreateDirectory(dir); test9 = new PlayTest9(dir); EditorApplication.update += Test9Tick; }
    }
}
