using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace CodeBreaker.Level3.EditorTools
{
    // Level 3 environment pass (Phase 2: main server hall).
    // Adds new decoration ONLY under "Level3/L3_Environment/Phase2_MainHall". Running it again deletes and
    // rebuilds that group, so it never stacks duplicates. The only existing object it touches is the
    // hall floor's material (Floor_Main -> raised-floor tile material); the original is restored by
    // "Undo Phase 2" below. Racks, lights, puzzles, colliders and gameplay objects are never changed.
    [InitializeOnLoad]
    public static partial class Level3EnvironmentPass
    {
        // Command file trigger (so the tool can also be run without clicking the menu):
        // put a file named "run_build", "run_capture" or "run_undo" in <project>/Level3_Phase2/.
        static double nextPoll;
        static Level3EnvironmentPass() { EditorApplication.update += Poll; }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling) return;
            nextPoll = EditorApplication.timeSinceStartup + 2.0;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase2");
            if (!Directory.Exists(dir)) return;
            if (EditorApplication.isPlaying)
            {
                string pt = Path.Combine(dir, "run_playtest");
                if (File.Exists(pt) && test == null) { File.Delete(pt); test = new PlayTest(dir); EditorApplication.update += TestTick; }
                string pt3 = Path.Combine(dir, "run_playtest3");
                if (File.Exists(pt3)) { File.Delete(pt3); StartPlayTest3(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase3")); }
                string pt4 = Path.Combine(dir, "run_playtest4");
                if (File.Exists(pt4)) { File.Delete(pt4); StartPlayTest4(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase4")); }
                string pt5 = Path.Combine(dir, "run_playtest5");
                if (File.Exists(pt5)) { File.Delete(pt5); StartPlayTest5(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase5")); }
                string pt6 = Path.Combine(dir, "run_playtest6");
                if (File.Exists(pt6)) { File.Delete(pt6); StartPlayTest6(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase6")); }
                string pt7 = Path.Combine(dir, "run_playtest7");
                if (File.Exists(pt7)) { File.Delete(pt7); StartPlayTest7(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase7")); }
                string pt8 = Path.Combine(dir, "run_playtest8");
                if (File.Exists(pt8)) { File.Delete(pt8); StartPlayTest8(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase8")); }
                string pt9 = Path.Combine(dir, "run_playtest9");
                if (File.Exists(pt9)) { File.Delete(pt9); StartPlayTest9(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase9")); }
                string pt10 = Path.Combine(dir, "run_playtest10");
                if (File.Exists(pt10)) { File.Delete(pt10); StartPlayTest10(null); }
                string pf10 = Path.Combine(dir, "run_perf10");
                if (File.Exists(pf10)) { File.Delete(pf10); StartPerf10(); }
                string pb10 = Path.Combine(dir, "run_perfbase10");
                if (File.Exists(pb10)) { File.Delete(pb10); StartPerfBase10(); }
                string shu = Path.Combine(dir, "run_shutest");
                if (File.Exists(shu)) { File.Delete(shu); StartProgressTest(); }
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string cmd in new[] { "run_build", "run_capture", "run_undo", "run_build3", "run_capture3", "run_undo3", "run_build4", "run_capture4", "run_undo4", "run_build5", "run_undo5", "run_build6", "run_undo6", "run_build7", "run_capture7", "run_undo7", "run_dump8", "run_build8", "run_undo8", "run_check8", "run_build9", "run_undo9", "run_check10", "run_dumpsave" })
            {
                string f = Path.Combine(dir, cmd);
                if (!File.Exists(f)) continue;
                File.Delete(f);
                if (cmd == "run_build") Build();
                else if (cmd == "run_capture") Capture();
                else if (cmd == "run_undo") UndoPhase2();
                else if (cmd == "run_build3") Build3();
                else if (cmd == "run_capture3") Capture3();
                else if (cmd == "run_undo3") UndoPhase3();
                else if (cmd == "run_build4") Build4();
                else if (cmd == "run_capture4") Capture4();
                else if (cmd == "run_undo4") UndoPhase4();
                else if (cmd == "run_build5") Build5();
                else if (cmd == "run_undo5") UndoPhase5();
                else if (cmd == "run_build6") Build6();
                else if (cmd == "run_undo6") UndoPhase6();
                else if (cmd == "run_build7") Build7();
                else if (cmd == "run_capture7") Capture7();
                else if (cmd == "run_undo7") UndoPhase7();
                else if (cmd == "run_dump8") Dump8();
                else if (cmd == "run_build8") Build8();
                else if (cmd == "run_check8") Check8();
                else if (cmd == "run_undo8") UndoPhase8();
                else if (cmd == "run_build9") Build9();
                else if (cmd == "run_check10") Check10();
                else if (cmd == "run_dumpsave") DumpSave();
                else UndoPhase9();
                File.WriteAllText(Path.Combine(dir, "done_" + cmd + ".txt"), System.DateTime.Now.ToString("o"));
                return;
            }
        }

        const string ScenePath = "Assets/Scenes/Level 3.unity";
        const string Root = "Assets/CodeBreaker/Level3";
        const string TexDir = Root + "/Textures/Env";
        const string MatDir = Root + "/Materials/Env";
        const string MeshDir = Root + "/Meshes/Env";
        const string GroupName = "Phase2_MainHall";
        const string FloorOriginalMat = Root + "/Materials/L3_Floor_ServerTile.mat";

        const float H = 11.25f;      // ceiling (3 m)
        const float Tile = 2.25f;    // raised floor tile (60 cm)
        const float Half = 22.5f;    // hall half size
        const float DoorH = 9f;

        static readonly Color Red = new Color(0.78f, 0.12f, 0.09f), Blue = new Color(0.12f, 0.35f, 0.85f), Green = new Color(0.12f, 0.62f, 0.25f);
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

        // ================================================================== menu
        [MenuItem("Code Breaker/Level 3/Phase 2 - Build Main Hall")]
        public static void Build()
        {
            if (!OpenLevel3()) return;
            log.Length = 0; mats.Clear();
            EnsureFolders();
            ImportTextures();

            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform g = new GameObject(GroupName).transform; g.SetParent(env, false);

            List<RackPair> pairs = FindRackPairs(level);
            log.AppendLine("Rack pairs found: " + pairs.Count);

            RaisedFloor(level, g);
            AisleLighting(g, pairs);
            FloorLines(g);
            CableTrays(g, pairs);
            GlassTiles(g);
            Curtains(g);

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            AssetDatabase.SaveAssets();
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase2", "build_report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, log.ToString());
            Debug.Log("[Level 3 Env] Phase 2 built and saved.\n" + log);
        }

        [MenuItem("Code Breaker/Level 3/Phase 2 - Undo (remove Main Hall pass)")]
        public static void UndoPhase2()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform grp = level != null ? level.Find("L3_Environment/" + GroupName) : null;
            if (grp != null) Object.DestroyImmediate(grp.gameObject);
            Renderer floor = FindFloor(level);
            Material orig = AssetDatabase.LoadAssetAtPath<Material>(FloorOriginalMat);
            if (floor != null && orig != null) floor.sharedMaterial = orig;
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 2 removed; hall floor material restored.");
        }

        [MenuItem("Code Breaker/Level 3/Phase 2 - Capture Screenshots")]
        public static void Capture()
        {
            if (!OpenLevel3()) return;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase2");
            Directory.CreateDirectory(dir);
            var shots = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("01_hall_from_entrance", new Vector3(0f, 6.4f, -21f), new Vector3(0f, 3.5f, 8f), 72f),
                ("02_hall_from_north", new Vector3(0f, 6.4f, 21f), new Vector3(0f, 3.5f, -8f), 72f),
                ("03_cold_aisle", new Vector3(-4.5f, 5.6f, -11.5f), new Vector3(-11f, 1.5f, -4.5f), 62f),
                ("04_hot_aisle", new Vector3(-4.5f, 5.6f, 5f), new Vector3(-11f, 3f, -1f), 62f),
                ("05_floor_lines", new Vector3(0f, 10.6f, -19f), new Vector3(0f, 0f, 4f), 78f),
                ("06_cable_trays", new Vector3(-3f, 5f, -17f), new Vector3(-11f, 10f, 2f), 70f),
                ("07_glass_tiles", new Vector3(9.5f, 5.2f, -10.5f), new Vector3(5.6f, 0f, -5.6f), 58f),
                ("08_curtain_west", new Vector3(-13f, 6f, -2f), new Vector3(-22f, 4.5f, 0f), 60f),
                ("09_racks_overview", new Vector3(19f, 7.5f, -19f), new Vector3(0f, 2f, 3f), 70f),
                ("10_glass_closeup", new Vector3(5.6f, 4.2f, -9.2f), new Vector3(5.6f, 0f, -5.6f), 45f),
            };
            foreach (var s in shots) Shot(dir, s.name, s.pos, s.look, s.fov);
            Debug.Log("[Level 3 Env] Captures written to " + dir);
        }

        // ================================================================== play-mode self test
        // Runs inside Play mode (trigger file "run_playtest"). Walks the player through each curtain by
        // teleporting in small steps, toggles a keycard flag in memory only (never saved) to check the
        // status screen, samples the cracked-tile flicker, then writes playtest_report.txt.
        static PlayTest test;
        static void TestTick()
        {
            if (test == null || !EditorApplication.isPlaying) { EditorApplication.update -= TestTick; test = null; return; }
            if (test.Step()) { EditorApplication.update -= TestTick; test = null; }
        }

        class PlayTest
        {
            readonly string dir; readonly System.Text.StringBuilder r = new System.Text.StringBuilder();
            int phase, frame; double t0; Vector3 startPos; Quaternion startRot;
            L3StripCurtain[] curtains; int ci; float maxSwing; float minI = 99f, maxI = -1f;
            public PlayTest(string d) { dir = d; t0 = EditorApplication.timeSinceStartup; }

            public bool Step()
            {
                var g = CodeBreaker.Level2.L2Game.Instance;
                if (g == null || g.player == null) { r.AppendLine("FAIL: L2Game/player not found"); return Finish(); }
                frame++;
                switch (phase)
                {
                    case 0:
                        startPos = g.player.position; startRot = g.player.rotation;
                        curtains = Object.FindObjectsByType<L3StripCurtain>();
                        r.AppendLine("Curtains found: " + curtains.Length);
                        phase = 1; frame = 0; ci = 0; maxSwing = 0f; return false;
                    case 1:
                        if (ci >= curtains.Length) { phase = 2; frame = 0; return false; }
                        {
                            Transform c = curtains[ci].transform;
                            float k = Mathf.Clamp01(frame / 90f);                        // ~1.5 s at 60 fps
                            Vector3 p = c.position + c.forward * Mathf.Lerp(3.5f, -3.5f, k);
                            p.y = startPos.y;
                            g.Teleport(p, Quaternion.LookRotation(-c.forward, Vector3.up));
                            foreach (Transform s in curtains[ci].strips)
                            {
                                float a = Mathf.Abs(Mathf.DeltaAngle(0f, s.localEulerAngles.x));
                                maxSwing = Mathf.Max(maxSwing, a);
                            }
                            if (frame > 130)
                            {
                                r.AppendLine("  " + c.name + ": max strip swing while walking through = " + maxSwing.ToString("0.0") + " deg " + (maxSwing > 10f ? "(PASS)" : "(FAIL)"));
                                ci++; frame = 0; maxSwing = 0f;
                            }
                        }
                        return false;
                    case 2:
                        g.Teleport(startPos, startRot);
                        var term = Object.FindFirstObjectByType<L3Terminal>();
                        if (term == null || term.screen == null) { r.AppendLine("FAIL: terminal screen not found"); phase = 3; return false; }
                        term.Refresh();
                        string before = term.screen.text;
                        bool had = g.Flag("card_red_in");
                        g.SetFlag("card_red_in");
                        term.Refresh();
                        string after = term.screen.text;
                        if (!had) g.SetFlag("card_red_in", false);
                        term.Refresh();
                        r.AppendLine("Status screen before: " + (before.Contains("SYSTEM LOCKDOWN") ? "SYSTEM LOCKDOWN shown" : "MISSING header") + ", MAINTENANCE " + (before.Contains("MAINTENANCE KEYCARD") && before.Contains("EMPTY") ? "EMPTY" : "?"));
                        r.AppendLine("Status screen with red card flag (memory only, not saved): " + (after.Contains("[ INSERTED ]") ? "MAINTENANCE shows INSERTED (PASS)" : "no change (FAIL)"));
                        r.AppendLine("Flag restored to " + g.Flag("card_red_in") + " (was " + had + ")");
                        phase = 3; frame = 0; return false;
                    case 3:
                        Light red = null;
                        foreach (var f in Object.FindObjectsByType<L3FlickerLight>()) red = f.target;
                        if (red == null) { r.AppendLine("FAIL: flicker light not found"); return Finish(); }
                        minI = Mathf.Min(minI, red.intensity); maxI = Mathf.Max(maxI, red.intensity);
                        if (frame > 180)
                        {
                            r.AppendLine("Cracked tile red light over 3 s: intensity " + minI.ToString("0.00") + " .. " + maxI.ToString("0.00") + (maxI - minI > 0.5f ? " (flickering, PASS)" : " (FAIL)"));
                            return Finish();
                        }
                        return false;
                }
                return Finish();
            }

            bool Finish()
            {
                r.AppendLine("Lights in scene: " + Object.FindObjectsByType<Light>().Length);
                r.AppendLine("Test time: " + (EditorApplication.timeSinceStartup - t0).ToString("0.0") + " s");
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Play test:\n" + r);
                return true;
            }
        }

        // ================================================================== scene helpers
        static bool OpenLevel3()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Level 3 Env] Stop Play mode first."); return false; }
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return true;
        }

        static Transform FindLevelRoot()
        {
            foreach (GameObject r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "Level3") return r.transform;
            return null;
        }

        static Renderer FindFloor(Transform level)
        {
            if (level == null) return null;
            foreach (Renderer r in level.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Floor_Main") return r;
            return null;
        }

        class RackPair { public float x, front, back; public List<Transform> racks = new List<Transform>(); }

        static List<RackPair> FindRackPairs(Transform level)
        {
            var list = new List<RackPair>();
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Rack_") || t.parent == null || t.parent.name != "Racks") continue;
                Renderer body = t.Find("RackBody") != null ? t.Find("RackBody").GetComponent<Renderer>() : null;
                if (body == null) continue;
                Bounds b = body.bounds;
                RackPair p = list.Find(q => Mathf.Abs(q.x - Mathf.Sign(b.center.x) * 11f) < 0.1f && Mathf.Abs(q.front - b.min.z) < 0.5f);
                if (p == null) { p = new RackPair { x = Mathf.Sign(b.center.x) * 11f, front = b.min.z, back = b.max.z }; list.Add(p); }
                p.racks.Add(t);
                p.front = Mathf.Min(p.front, b.min.z); p.back = Mathf.Max(p.back, b.max.z);
            }
            foreach (RackPair p in list)
            {
                float sx = 0f; foreach (Transform r in p.racks) sx += r.position.x;
                if (p.racks.Count > 0) p.x = sx / p.racks.Count;
            }
            return list;
        }

        // ================================================================== 1. raised floor
        static void RaisedFloor(Transform level, Transform g)
        {
            Renderer floor = FindFloor(level);
            Material m = Lit("M_L3_RaisedFloor", new Color(0.46f, 0.48f, 0.52f), 0.32f, 0f, Tex("T_L3_RaisedFloorTile"), new Vector2(20f, 20f));
            if (floor != null)
            {
                log.AppendLine("Floor_Main material: " + (floor.sharedMaterial != null ? floor.sharedMaterial.name : "none") + " -> M_L3_RaisedFloor (20 x 20 tiles of 60 cm)");
                floor.sharedMaterial = m;
            }
            else log.AppendLine("WARNING: Floor_Main not found, raised floor material not applied.");
        }

        static float CellCenter(int k) => -Half + (k + 0.5f) * Tile;
        static int CellOf(float v) => Mathf.FloorToInt((v + Half) / Tile);

        // ================================================================== 2. hot / cold aisles
        static void AisleLighting(Transform g, List<RackPair> pairs)
        {
            Transform a = Group("AisleLighting", g, Vector3.zero);
            Material vent = Lit("M_L3_VentTile", new Color(0.46f, 0.48f, 0.52f), 0.35f, 0.2f, Tex("T_L3_VentTile"), Vector2.one);
            Material fog = FogMat();
            Material shimmer = ShaderMat("M_L3_HeatShimmer", "CodeBreaker/Heat Shimmer");
            shimmer.SetFloat("_Strength", 0.0022f); shimmer.SetFloat("_Speed", 0.55f); shimmer.SetColor("_Tint", new Color(1f, 0.62f, 0.32f, 0.04f));
            EditorUtility.SetDirty(shimmer);
            Material grille = Lit("M_L3_RackRearGrille", new Color(0.09f, 0.09f, 0.1f), 0.35f, 0.6f, Tex("T_L3_TrayMesh"), Vector2.one, null, true, false, true);
            Material exhaust = Lit("M_L3_ExhaustGlow", new Color(0.05f, 0.03f, 0.02f), 0.2f, 0f, null, Vector2.one, new Color(0.26f, 0.09f, 0.02f));
            Material coldKick = Lit("M_L3_ColdKickStrip", new Color(0.05f, 0.08f, 0.12f), 0.4f, 0f, null, Vector2.one, new Color(0.15f, 0.42f, 1.1f));

            int lights = 0;
            foreach (RackPair p in pairs)
            {
                string tag = (p.x < 0 ? "W" : "E") + (p.front > 2f ? "N" : "S");
                // ---- cold aisle (front): perforated vent tiles, blue kick strip, low blue light, floor mist
                Transform c = Group("ColdAisle_" + tag, a, new Vector3(p.x, 0f, p.front));
                int row = CellOf(p.front - 0.01f);
                foreach (int k in new[] { CellOf(p.x - 1.2f), CellOf(p.x + 1.2f) })
                {
                    GameObject v = MeshObj("VentTile", c, QuadXZ(Tile - 0.02f, Tile - 0.02f, 1f, 1f), vent);
                    v.transform.position = new Vector3(CellCenter(k), 0.006f, CellCenter(row));
                }
                foreach (Transform r in p.racks)
                {
                    GameObject kick = Box("ColdKickStrip", c, Vector3.zero, new Vector3(2.0f, 0.08f, 0.04f), coldKick);
                    kick.transform.position = new Vector3(r.position.x, 0.25f, p.front - 0.03f);
                }
                Light cl = PointLight(c, "ColdAisleLight", new Vector3(p.x, 1.3f, p.front - 1.4f), new Color(0.35f, 0.6f, 1f), 9f, 7.5f); lights++;
                float[] heights = { 0.18f, 0.5f, 0.95f };
                for (int i = 0; i < heights.Length; i++)
                {
                    GameObject f = MeshObj("FloorMist_" + i, c, FogMesh(6.8f, 3.8f), fog);
                    f.transform.position = new Vector3(p.x + (i - 1) * 0.35f, heights[i], p.front - 1.7f - i * 0.15f);
                    f.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
                    f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }

                // ---- hot aisle (back): rear grilles with an amber exhaust glow, warm light, heat shimmer
                Transform h = Group("HotAisle_" + tag, a, new Vector3(p.x, 0f, p.back));
                foreach (Transform r in p.racks)
                {
                    GameObject glow = Box("ExhaustGlow", h, Vector3.zero, new Vector3(2.0f, 6.4f, 0.02f), exhaust);
                    glow.transform.position = new Vector3(r.position.x, 3.6f, p.back + 0.012f);
                    GameObject gr = MeshObj("RearGrille", h, QuadXY(2.1f, 6.6f, 0.5f, 14f, 44f), grille);
                    gr.transform.position = new Vector3(r.position.x, 3.6f, p.back + 0.035f);
                    gr.transform.rotation = Quaternion.LookRotation(Vector3.back);
                }
                Light hl = PointLight(h, "HotAisleLight", new Vector3(p.x, 4.6f, p.back + 1.7f), new Color(1f, 0.52f, 0.2f), 9f, 8.5f); lights++;
                GameObject sh = MeshObj("HeatShimmer", h, QuadXY(5.6f, 7.2f, 0f, 1f, 1f), shimmer);
                sh.transform.position = new Vector3(p.x, 0.25f, p.back + 0.75f);
                sh.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            log.AppendLine("Hot/cold aisles: " + pairs.Count + " pairs, " + lights + " new point lights (no shadows), 3 mist layers per cold aisle, 1 shimmer quad per hot aisle");
        }

        // ================================================================== 3. floor navigation lines
        static void FloorLines(Transform g)
        {
            Transform f = Group("FloorNavigationLines", g, Vector3.zero);
            const float y0 = 0.012f;
            Lane(f, "Maintenance_Red", Red, y0, new[] { new Vector3(-1.2f, 0, -9.2f), new Vector3(-1.2f, 0, 1f), new Vector3(-25.5f, 0, 1f) },
                 "MAINTENANCE", new Vector3(-16.5f, 0, 2.35f), Vector3.left);
            Lane(f, "Control_Green", Green, y0 + 0.002f, new[] { new Vector3(0f, 0, -9.2f), new Vector3(0f, 0, 25.5f) },
                 "CONTROL ROOM", new Vector3(1.4f, 0, 16f), Vector3.forward);
            Lane(f, "Security_Blue", Blue, y0 + 0.004f, new[] { new Vector3(1.2f, 0, -9.2f), new Vector3(1.2f, 0, 1f), new Vector3(25.5f, 0, 1f) },
                 "SECURITY", new Vector3(16.5f, 0, 2.35f), Vector3.right);
            log.AppendLine("Floor lines: red -> Maintenance (west door), green -> Control (north door), blue -> Security (east door); start behind the central terminal");
        }

        const bool PaintedWingNames = false;

        static void Lane(Transform parent, string name, Color col, float y, Vector3[] pts, string label, Vector3 labelPos, Vector3 labelDir)
        {
            Transform l = Group(name, parent, Vector3.zero);
            Material paint = Lit("M_L3_FloorLine_" + name.Split('_')[1], col, 0.25f, 0f, Tex("T_L3_FloorPaint"), Vector2.one, col * 0.35f, true);
            Material chev = Lit("M_L3_FloorChevron_" + name.Split('_')[1], Color.Lerp(col, Color.white, 0.15f), 0.25f, 0f, Tex("T_L3_Chevron"), Vector2.one, col * 0.5f, true);
            const float w = 0.42f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1], d = (b - a).normalized;
                float len = Vector3.Distance(a, b) + w;
                GameObject seg = MeshObj("Line_" + i, l, QuadXZ(w, len, 1f, len / 3f), paint);
                seg.transform.position = (a + b) * 0.5f + Vector3.up * (y + i * 0.0015f);
                seg.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
                // chevrons every 4.5 units, pointing toward the wing
                for (float s = 2.2f; s < len - 1.5f; s += 4.5f)
                {
                    GameObject cv = MeshObj("Chevron", l, QuadXZ(0.85f, 0.85f, 1f, 1f), chev);
                    cv.transform.position = a + d * s + Vector3.up * (y + 0.006f);
                    cv.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
                }
            }
            // painted wing names on the floor were removed (the coloured lines and door signs guide the player)
            if (!PaintedWingNames) return;
            var go = new GameObject("Label_" + label);
            go.transform.SetParent(l, false);
            go.transform.position = labelPos + Vector3.up * (y + 0.01f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, labelDir);
            var t = go.AddComponent<TextMeshPro>();
            t.text = label;
            t.fontSize = 6f;
            t.characterSpacing = 8f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(col.r * 1.15f, col.g * 1.15f, col.b * 1.15f, 0.8f);
            t.rectTransform.sizeDelta = new Vector2(9f, 1.6f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            TMP_FontAsset font = FindSceneFont();
            if (font != null) t.font = font;
        }

        static TMP_FontAsset FindSceneFont()
        {
            Transform level = FindLevelRoot();
            if (level == null) return null;
            TextMeshPro any = null;
            foreach (TextMeshPro t in level.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (t.font == null) continue;
                if (t.transform.parent != null && t.transform.parent.name.StartsWith("Sign_")) return t.font;
                if (any == null) any = t;
            }
            return any != null ? any.font : null;
        }

        // ================================================================== 4. overhead cable trays
        static void CableTrays(Transform g, List<RackPair> pairs)
        {
            Transform t = Group("CableTrays", g, Vector3.zero);
            Material mesh = Lit("M_L3_TrayMesh", new Color(0.86f, 0.66f, 0.12f), 0.45f, 0.55f, Tex("T_L3_TrayMesh"), Vector2.one, null, true, false, true);
            Material rail = Lit("M_L3_TrayRail", new Color(0.86f, 0.66f, 0.12f), 0.5f, 0.6f);
            Material strut = Lit("M_L3_Strut", new Color(0.32f, 0.33f, 0.35f), 0.45f, 0.75f);
            Material rod = Lit("M_L3_ThreadedRod", new Color(0.55f, 0.56f, 0.58f), 0.55f, 0.85f);
            Material[] cable =
            {
                Lit("M_L3_Cable_Black", new Color(0.05f, 0.05f, 0.055f), 0.42f),
                Lit("M_L3_Cable_Grey", new Color(0.25f, 0.26f, 0.28f), 0.4f),
                Lit("M_L3_Cable_Blue", new Color(0.08f, 0.2f, 0.55f), 0.45f),
                Lit("M_L3_Cable_Yellow", new Color(0.8f, 0.62f, 0.08f), 0.45f),
                Lit("M_L3_Cable_Orange", new Color(0.8f, 0.3f, 0.06f), 0.45f),
            };

            const float inner = Half - 0.45f - 0.05f;   // wall inner face
            Tray(t, "Tray_West", new Vector3(-11f, 9.85f, -inner), new Vector3(-11f, 9.85f, inner), mesh, rail, strut, rod, cable, 0);
            Tray(t, "Tray_East", new Vector3(11f, 9.85f, -inner), new Vector3(11f, 9.85f, inner), mesh, rail, strut, rod, cable, 1);
            Tray(t, "Tray_Cross", new Vector3(-inner, 10.35f, 2.5f), new Vector3(inner, 10.35f, 2.5f), mesh, rail, strut, rod, cable, 2);

            // drops from the trays into the top of every rack
            Transform drops = Group("RackDrops", t, Vector3.zero);
            int n = 0;
            foreach (RackPair p in pairs)
                foreach (Transform r in p.racks)
                {
                    float side = Mathf.Sign(r.position.x - p.x);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 top = new Vector3(p.x + side * (0.25f + k * 0.18f), 9.88f, p.back - 0.9f + k * 0.2f);
                        Vector3 bottom = new Vector3(r.position.x + k * 0.2f - 0.1f, 7.5f, p.back - 0.9f + k * 0.2f);
                        Cylinder("Drop", drops, top, bottom, 0.055f + k * 0.01f, cable[(n + k) % cable.Length]);
                    }
                    n++;
                }

            // a few loose cables sagging out of the trays (all kept above head height)
            Transform loose = Group("LooseCables", t, Vector3.zero);
            Sag(loose, "Loose_1", new Vector3(-11.82f, 9.95f, -15f), new Vector3(-11.82f, 9.95f, -10.2f), 8.45f, 0.06f, cable[0]);
            Sag(loose, "Loose_2", new Vector3(11.82f, 9.95f, 13.6f), new Vector3(11.82f, 9.95f, 18.2f), 8.7f, 0.05f, cable[1]);
            Sag(loose, "Loose_3", new Vector3(-4.2f, 10.45f, 3.32f), new Vector3(0.8f, 10.45f, 3.32f), 9.0f, 0.045f, cable[3]);
            Sag(loose, "Loose_4", new Vector3(15f, 10.45f, 1.68f), new Vector3(18.5f, 10.45f, 1.68f), 9.1f, 0.05f, cable[4]);
            Dangle(loose, "Dangling_End", new Vector3(-10.2f, 9.9f, -6.1f), 8.35f, 0.05f, cable[2]);
            log.AppendLine("Cable trays: west + east runs above the rack rows (y 9.85), cross run (y 10.35); " + n + " rack drops, 5 loose/dangling cables (lowest point y 8.35 = 2.2 m)");
        }

        static void Tray(Transform parent, string name, Vector3 a, Vector3 b, Material mesh, Material rail, Material strut, Material rod, Material[] cable, int seed)
        {
            Vector3 d = (b - a).normalized; float len = Vector3.Distance(a, b);
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(d, Vector3.up));
            const float w = 1.6f, hgt = 0.38f, cell = 0.19f;
            GameObject bottom = MeshObj("Bottom", t, QuadXZ(w, len, w / cell, len / cell), mesh);
            bottom.transform.localPosition = Vector3.zero;
            foreach (int s in new[] { -1, 1 })
            {
                GameObject side = MeshObj("Side", t, QuadXY(len, hgt, 0f, len / cell, hgt / cell), mesh);
                side.transform.localPosition = new Vector3(s * w * 0.5f, 0f, 0f);
                side.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                Box("TopRail", t, new Vector3(s * w * 0.5f, hgt, 0f), new Vector3(0.05f, 0.05f, len), rail);
                Box("BottomRail", t, new Vector3(s * w * 0.5f, 0.0f, 0f), new Vector3(0.05f, 0.05f, len), rail);
            }
            // supports every 4.5 units: unistrut under the tray + two threaded rods to the ceiling
            for (float z = -len * 0.5f + 1.5f; z < len * 0.5f - 1f; z += 4.5f)
            {
                Box("Strut", t, new Vector3(0f, -0.08f, z), new Vector3(w + 0.5f, 0.12f, 0.12f), strut);
                foreach (int s in new[] { -1, 1 })
                {
                    Vector3 lo = t.TransformPoint(new Vector3(s * (w * 0.5f + 0.15f), -0.08f, z));
                    Cylinder("Rod", t, lo, new Vector3(lo.x, H, lo.z), 0.025f, rod);
                }
            }
            // cables lying in the tray
            var rnd = new System.Random(seed * 17 + 3);
            float[] lane = { -0.55f, -0.33f, -0.12f, 0.1f, 0.3f, 0.52f };
            for (int i = 0; i < lane.Length; i++)
            {
                float r = 0.07f + (float)rnd.NextDouble() * 0.05f;
                Vector3 p0 = t.TransformPoint(new Vector3(lane[i], r + 0.01f, -len * 0.5f));
                Vector3 p1 = t.TransformPoint(new Vector3(lane[i] + ((float)rnd.NextDouble() - 0.5f) * 0.08f, r + 0.01f, len * 0.5f));
                Cylinder("Cable", t, p0, p1, r, cable[(i + seed) % cable.Length]);
            }
            Cylinder("CableTop", t, t.TransformPoint(new Vector3(-0.2f, 0.25f, -len * 0.5f)), t.TransformPoint(new Vector3(0.15f, 0.25f, len * 0.5f)), 0.09f, cable[(seed + 2) % cable.Length]);
        }

        // ================================================================== 5. see-through floor tiles
        static void GlassTiles(Transform g)
        {
            Transform t = Group("GlassFloorTiles", g, Vector3.zero);
            Shader sh = Shader.Find("CodeBreaker/Underfloor Window");
            if (sh == null) { log.AppendLine("ERROR: Underfloor Window shader missing"); return; }
            Material clear = UnderfloorMat("M_L3_GlassTile", sh, false);
            Material cracked = UnderfloorMat("M_L3_GlassTile_Cracked", sh, true);
            Mesh q = QuadXZ(Tile - 0.03f, Tile - 0.03f, 1f, 1f);
            Vector3[] spots = { new Vector3(CellCenter(7), 0, CellCenter(6)), new Vector3(CellCenter(8), 0, CellCenter(15)) };
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject go = MeshObj("GlassTile_" + (i + 1), t, q, clear);
                go.transform.position = spots[i] + Vector3.up * 0.007f;
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            GameObject c = MeshObj("GlassTile_Cracked", t, q, cracked);
            c.transform.position = new Vector3(CellCenter(12), 0.007f, CellCenter(7));
            c.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Light red = PointLight(t, "CrackedTile_RedLight", c.transform.position + Vector3.up * 0.35f, new Color(1f, 0.1f, 0.05f), 4f, 3.6f);
            var fl = red.gameObject.AddComponent<L3FlickerLight>();
            fl.target = red; fl.baseIntensity = red.intensity; fl.mirrorRenderer = c.GetComponent<Renderer>(); fl.mirrorProperty = "_RedOn";
            log.AppendLine("Glass floor tiles: 2 clear + 1 cracked with a flickering red light underneath (1 new point light)");
        }

        static Material UnderfloorMat(string name, Shader sh, bool crackedTile)
        {
            Material m = Asset(name, sh);
            m.SetTexture("_MainTex", Tex("T_L3_Underfloor"));
            m.SetTexture("_LedMask", Tex("T_L3_UnderfloorLeds"));
            m.SetTexture("_Crack", Tex("T_L3_GlassCrack"));
            m.SetFloat("_Depth", 1.4f);
            m.SetFloat("_TileSize", Tile);
            m.SetFloat("_Brightness", 1.1f);
            m.SetFloat("_CrackAmount", crackedTile ? 1f : 0f);
            m.SetFloat("_RedOn", crackedTile ? 0.6f : 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================== 6. plastic strip curtains
        static void Curtains(Transform g)
        {
            Transform t = Group("StripCurtains", g, Vector3.zero);
            Material pvc = Lit("M_L3_CurtainPVC", new Color(0.6f, 0.72f, 0.78f, 0.11f), 0.9f, 0f, null, Vector2.one, null, false, true, true);
            Material bar = Lit("M_L3_CurtainBar", new Color(0.4f, 0.41f, 0.43f), 0.5f, 0.8f);
            const float face = Half - 0.45f - 0.04f;
            Curtain(t, "Curtain_Maintenance", new Vector3(-face, 0f, 0f), Vector3.right, pvc, bar);
            Curtain(t, "Curtain_Security", new Vector3(face, 0f, 0f), Vector3.left, pvc, bar);
            Curtain(t, "Curtain_Control", new Vector3(0f, 0f, face), Vector3.back, pvc, bar);
            log.AppendLine("Strip curtains: Maintenance, Security and Control doorways (hall side), no colliders, sway when walked through");
        }

        static void Curtain(Transform parent, string name, Vector3 pos, Vector3 intoHall, Material pvc, Material bar)
        {
            Transform c = new GameObject(name).transform;
            c.SetParent(parent, false);
            c.SetPositionAndRotation(pos, Quaternion.LookRotation(intoHall, Vector3.up));
            Box("MountBar", c, new Vector3(0f, DoorH + 0.08f, 0f), new Vector3(6.3f, 0.16f, 0.14f), bar);
            const int count = 14; const float width = 0.56f, step = 0.43f, len = DoorH - 0.12f;
            Mesh m = QuadXY(width, len, 1f, 1f, 1f);
            var strips = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                float x = -step * (count - 1) * 0.5f + i * step;
                Transform pivot = new GameObject("Strip_" + i).transform;
                pivot.SetParent(c, false);
                pivot.localPosition = new Vector3(x, DoorH, (i % 2) * 0.03f);
                GameObject s = MeshObj("PVC", pivot, m, pvc);
                s.transform.localPosition = Vector3.zero;
                s.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                strips[i] = pivot;
            }
            var sc = c.gameObject.AddComponent<L3StripCurtain>();
            sc.strips = strips;
        }

        // ================================================================== meshes
        static Mesh QuadXZ(float w, float l, float uvU, float uvV)
        {
            string key = string.Format("L3E_QuadXZ_{0:0.###}x{1:0.###}_{2:0.##}x{3:0.##}", w, l, uvU, uvV);
            return CachedMesh(key, () =>
            {
                var m = new Mesh { name = key };
                m.vertices = new[] { new Vector3(-w / 2, 0, -l / 2), new Vector3(w / 2, 0, -l / 2), new Vector3(-w / 2, 0, l / 2), new Vector3(w / 2, 0, l / 2) };
                m.uv = new[] { new Vector2(0, 0), new Vector2(uvU, 0), new Vector2(0, uvV), new Vector2(uvU, uvV) };
                m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                m.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
                m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                m.RecalculateBounds();
                return m;
            });
        }

        // Vertical quad in local XY facing -Z. pivot01: 0 = pivot at the bottom edge, 1 = pivot at the top edge.
        static Mesh QuadXY(float w, float h, float pivot01, float uvU, float uvV)
        {
            string key = string.Format("L3E_QuadXY_{0:0.###}x{1:0.###}_p{2:0.##}_{3:0.##}x{4:0.##}", w, h, pivot01, uvU, uvV);
            return CachedMesh(key, () =>
            {
                float y0 = -pivot01 * h, y1 = y0 + h;
                var m = new Mesh { name = key };
                m.vertices = new[] { new Vector3(-w / 2, y0, 0), new Vector3(w / 2, y0, 0), new Vector3(-w / 2, y1, 0), new Vector3(w / 2, y1, 0) };
                m.uv = new[] { new Vector2(0, 0), new Vector2(uvU, 0), new Vector2(0, uvV), new Vector2(uvU, uvV) };
                m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                m.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
                m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                m.RecalculateBounds();
                return m;
            });
        }

        // Flat mist sheet: a grid whose vertex alpha fades to zero at the borders.
        static Mesh FogMesh(float w, float l)
        {
            string key = string.Format("L3E_Fog_{0:0.##}x{1:0.##}", w, l);
            return CachedMesh(key, () =>
            {
                const int nx = 10, nz = 6;
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
                for (int j = 0; j <= nz; j++)
                    for (int i = 0; i <= nx; i++)
                    {
                        float u = i / (float)nx, s = j / (float)nz;
                        v.Add(new Vector3((u - 0.5f) * w, 0f, (s - 0.5f) * l));
                        uv.Add(new Vector2(u * 2f, s));
                        float a = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(s * Mathf.PI);
                        col.Add(new Color(1f, 1f, 1f, a * a));
                    }
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int k = j * (nx + 1) + i;
                        tri.AddRange(new[] { k, k + nx + 1, k + 1, k + 1, k + nx + 1, k + nx + 2 });
                    }
                var m = new Mesh { name = key };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tri, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            });
        }

        static Mesh Tube(string key, List<Vector3> pts, float r)
        {
            const int sides = 8;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 d = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                Vector3 side = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(side, d);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 o = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(pts[i] + o * r); n.Add(o); uv.Add(new Vector2(s / (float)sides, i));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + sides + 1;
                    tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            return SaveMesh(key, m);
        }

        static void Sag(Transform parent, string name, Vector3 a, Vector3 b, float lowY, float r, Material m)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 p = Vector3.Lerp(a, b, t);
                float sag = 4f * t * (1f - t);
                p.y = Mathf.Lerp(a.y, b.y, t) - sag * (Mathf.Lerp(a.y, b.y, 0.5f) - lowY);
                pts.Add(p);
            }
            GameObject go = MeshObj(name, parent, Tube("L3E_" + name, pts, r), m);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        static void Dangle(Transform parent, string name, Vector3 top, float tipY, float r, Material m)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                pts.Add(new Vector3(top.x + Mathf.Sin(t * 2.2f) * 0.25f, Mathf.Lerp(top.y, tipY, t), top.z + t * t * 0.35f));
            }
            GameObject go = MeshObj(name, parent, Tube("L3E_" + name, pts, r), m);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Box("Connector", go.transform, pts[pts.Count - 1] + Vector3.down * 0.08f, new Vector3(0.14f, 0.2f, 0.1f), Lit("M_L3_Strut", new Color(0.32f, 0.33f, 0.35f), 0.45f, 0.75f));
        }

        static Mesh CachedMesh(string key, System.Func<Mesh> make)
        {
            string path = MeshDir + "/" + key + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            return SaveMesh(key, make());
        }

        static Mesh SaveMesh(string key, Mesh m)
        {
            string path = MeshDir + "/" + key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ================================================================== objects
        static Transform Group(string name, Transform parent, Vector3 pos)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            return t;
        }

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            go.isStatic = false;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Cylinder(string name, Transform parent, Vector3 a, Vector3 b, float r, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, true);
            g.transform.position = (a + b) * 0.5f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            Vector3 ws = new Vector3(r * 2f, Vector3.Distance(a, b) * 0.5f, r * 2f);
            Vector3 ps = parent.lossyScale;
            g.transform.localScale = new Vector3(ws.x / ps.x, ws.y / ps.y, ws.z / ps.z);
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static Light PointLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ================================================================== materials & textures
        static void EnsureFolders()
        {
            foreach (string d in new[] { MatDir, MeshDir })
            {
                string[] parts = d.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }
        }

        static void ImportTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                bool changed = false;
                bool alpha = p.Contains("Mesh") || p.Contains("Crack") || p.Contains("Fog") || p.Contains("Paint") || p.Contains("Chevron") || p.Contains("Hazard") || p.Contains("Oil") || p.Contains("Spark") || p.Contains("Steam") || p.Contains("Clock");
                bool data = p.Contains("Leds");
                if (ti.alphaIsTransparency != alpha) { ti.alphaIsTransparency = alpha; changed = true; }
                if (ti.sRGBTexture == data) { ti.sRGBTexture = !data; changed = true; }
                if (ti.wrapMode != TextureWrapMode.Repeat && !data) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
                if (data && ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
                if (data && ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; changed = true; }
                if (!data && ti.anisoLevel < 4) { ti.anisoLevel = 4; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + name + ".png");

        static Material Asset(string name, Shader sh)
        {
            if (mats.TryGetValue(name, out Material cached)) return cached;
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            mats[name] = m;
            return m;
        }

        static Material ShaderMat(string name, string shader)
        {
            Shader sh = Shader.Find(shader);
            if (sh == null) { log.AppendLine("ERROR: shader " + shader + " not found"); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
            return Asset(name, sh);
        }

        static Material Lit(string name, Color c, float smooth, float metal = 0f, Texture2D tex = null, Vector2? tiling = null,
                            Color? emission = null, bool alphaClip = false, bool transparent = false, bool doubleSided = false)
        {
            // Self-lit parts (strips, bulbs, LEDs, screens) use URP Unlit: Lit emission did not render reliably here.
            if (emission.HasValue && tex == null && !alphaClip && !transparent)
            {
                Material u = Asset(name, Shader.Find("Universal Render Pipeline/Unlit"));
                Color e = emission.Value;
                u.SetColor("_BaseColor", new Color(e.r, e.g, e.b, 1f));
                u.SetFloat("_Cull", doubleSided ? 0f : 2f);
                EditorUtility.SetDirty(u);
                return u;
            }
            Material m = Asset(name, Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling ?? Vector2.one); }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            if (alphaClip)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
                m.SetOverrideTag("RenderType", "TransparentCutout");
            }
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material FogMat()
        {
            Material m = ShaderMat("M_L3_FloorMist", "CodeBreaker/Floor Mist");
            m.SetTexture("_BaseMap", Tex("T_L3_FogNoise"));
            m.SetColor("_BaseColor", new Color(0.62f, 0.78f, 1f, 0.16f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================== capture
        static void Shot(string dir, string name, Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("TmpEnvCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }
}
