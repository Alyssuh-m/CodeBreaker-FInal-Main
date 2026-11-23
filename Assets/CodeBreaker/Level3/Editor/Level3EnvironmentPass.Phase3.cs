using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 3: wing identity (Maintenance red-orange, Security blue, Control green).
    // Everything is added under "Level3/L3_Environment/Phase3_Wings" and rebuilt from scratch on every run.
    // Existing puzzle objects are never moved or edited. The only links to existing components:
    // new Maintenance work lamps are added to that wing's L3RoomPower light list (so they stay dim until
    // the generator runs, like the wing's own lights); Undo removes them again.
    public static partial class Level3EnvironmentPass
    {
        const string Group3 = "Phase3_Wings";
        static readonly Color Amber = new Color(1f, 0.45f, 0.15f), CBlue = new Color(0.3f, 0.55f, 1f), CGreen = new Color(0.3f, 1f, 0.45f);

        [MenuItem("Code Breaker/Level 3/Phase 3 - Build Wings")]
        public static void Build3()
        {
            if (!OpenLevel3()) return;
            log.Length = 0; mats.Clear();
            EnsureFolders();
            ImportTextures();
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            RemovePhase3(level);
            Transform g = new GameObject(Group3).transform; g.SetParent(env, false);

            MaintenanceWing(level, g);
            SecurityWing(g);
            ControlWing(g);

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            AssetDatabase.SaveAssets();
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase3");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), log.ToString());
            Debug.Log("[Level 3 Env] Phase 3 built and saved.\n" + log);
        }

        [MenuItem("Code Breaker/Level 3/Phase 3 - Undo (remove Wings pass)")]
        public static void UndoPhase3()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            RemovePhase3(level);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 3 removed.");
        }

        static void RemovePhase3(Transform level)
        {
            Transform old = level.Find("L3_Environment/" + Group3);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (L3RoomPower p in level.GetComponentsInChildren<L3RoomPower>(true))   // drop destroyed lamps from power lists
            {
                var keep = new List<Light>();
                foreach (Light l in p.lights) if (l != null) keep.Add(l);
                if (keep.Count != p.lights.Length) { p.lights = keep.ToArray(); EditorUtility.SetDirty(p); }
            }
        }

        [MenuItem("Code Breaker/Level 3/Phase 3 - Capture Screenshots")]
        public static void Capture3()
        {
            if (!OpenLevel3()) return;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase3");
            Directory.CreateDirectory(dir);
            Transform level = FindLevelRoot();
            if (level != null)
                foreach (ParticleSystem ps in level.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(3f, true, true);
            var shots = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("M1_maintenance_overview", new Vector3(-25f, 6.6f, -7.5f), new Vector3(-45f, 3.5f, 4f), 72f),
                ("M2_maintenance_south_wall", new Vector3(-47f, 6.2f, 5f), new Vector3(-44f, 5f, -10.8f), 70f),
                ("M3_maintenance_fan_generator", new Vector3(-36f, 6f, -2f), new Vector3(-35f, 7f, 10.8f), 66f),
                ("M4_maintenance_west", new Vector3(-44f, 6.2f, 0f), new Vector3(-59.5f, 5f, -6f), 66f),
                ("S1_security_overview", new Vector3(25f, 6.6f, -8f), new Vector3(45f, 4f, 6f), 72f),
                ("S2_security_desk", new Vector3(42f, 6f, -2f), new Vector3(42f, 3f, -10.8f), 66f),
                ("S3_security_badge_reader", new Vector3(30f, 5.4f, -2f), new Vector3(23f, 4.4f, -4.4f), 55f),
                ("C1_control_overview", new Vector3(0f, 6.6f, 24.5f), new Vector3(0f, 4.5f, 44f), 72f),
                ("C2_control_diagram", new Vector3(6f, 6.2f, 37f), new Vector3(-16.4f, 6.5f, 40.5f), 62f),
                ("C3_control_cooling_steam", new Vector3(-4f, 6.4f, 28f), new Vector3(-15f, 8f, 33f), 62f),
                ("C4_control_switches", new Vector3(-5f, 6.2f, 36f), new Vector3(16.4f, 5.5f, 38f), 62f),
            };
            foreach (var s in shots) Shot(dir, s.name, s.pos, s.look, s.fov);
            Debug.Log("[Level 3 Env] Phase 3 captures written to " + dir);
        }

        // ================================================================== MAINTENANCE (red-orange)
        static void MaintenanceWing(Transform level, Transform g)
        {
            Transform m = Group("Maintenance", g, Vector3.zero);
            L3RoomPower power = null;
            foreach (L3RoomPower p in level.GetComponentsInChildren<L3RoomPower>(true)) if (p.poweredFlag == "power_maintenance") power = p;

            Material metal = Lit("M_L3_PipeSteel", new Color(0.42f, 0.43f, 0.45f), 0.5f, 0.8f);
            Material rust = Lit("M_L3_PipeRust", new Color(0.36f, 0.2f, 0.12f), 0.3f, 0.5f);
            Material red = Lit("M_L3_PipeRed", new Color(0.55f, 0.08f, 0.06f), 0.45f, 0.3f);
            Material dark = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
            Material bulb = Lit("M_L3_WorkLampBulb", new Color(0.3f, 0.15f, 0.05f), 0.6f, 0f, null, Vector2.one, new Color(3.5f, 1.4f, 0.35f));
            Material cage = Lit("M_L3_CageMesh", new Color(0.18f, 0.18f, 0.19f), 0.4f, 0.7f, Tex("T_L3_TrayMesh"), Vector2.one, null, true, false, true);
            Material hazard = Lit("M_L3_HazardStripe", Color.white, 0.25f, 0f, Tex("T_L3_Hazard"), Vector2.one, null, true);
            Material oil = Lit("M_L3_OilStain", new Color(1f, 1f, 1f, 1f), 0.9f, 0f, Tex("T_L3_OilStain"), Vector2.one, null, false, true);
            Material water = Lit("M_L3_Water", new Color(0.1f, 0.13f, 0.16f, 0.55f), 0.95f, 0f, null, Vector2.one, null, false, true);
            Material board = Lit("M_L3_ToolBoard", Color.white, 0.2f, 0f, Tex("T_L3_ToolBoard"), Vector2.one);
            Material paper = Lit("M_L3_Paper", new Color(0.85f, 0.83f, 0.76f), 0.15f);
            Material yellow = Lit("M_L3_SignYellow", new Color(0.9f, 0.7f, 0.08f), 0.35f);

            // ---- work lamps (red-orange), dim until the generator runs
            int lamps = 0;
            foreach (var (pos, face) in new[] { (new Vector3(-43f, 8.3f, -10.55f), Vector3.forward), (new Vector3(-59.3f, 8.3f, 8.6f), Vector3.right), (new Vector3(-33.5f, 8.6f, 10.55f), Vector3.back) })
            {
                Transform l = Group("WorkLamp", m, pos);
                l.rotation = Quaternion.LookRotation(face, Vector3.up);
                Box("Bracket", l, new Vector3(0f, 0.2f, 0.25f), new Vector3(0.18f, 0.18f, 0.6f), dark);
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "Bulb";
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(l, false); b.transform.localPosition = new Vector3(0f, 0f, 0.6f); b.transform.localScale = Vector3.one * 0.38f;
                b.GetComponent<Renderer>().sharedMaterial = bulb;
                GameObject cg = MeshObj("Cage", l, QuadXY(0.6f, 0.6f, 0.5f, 3f, 3f), cage);
                cg.transform.localPosition = new Vector3(0f, 0f, 0.85f);
                Light lt = PointLight(l, "WorkLampLight", l.TransformPoint(new Vector3(0f, 0f, 1.1f)), Amber, 11f, 13f);
                if (power != null) { var list = new List<Light>(power.lights) { lt }; power.lights = list.ToArray(); EditorUtility.SetDirty(power); }
                lamps++;
            }

            // ---- pipes along the south wall, with brackets, a dripping joint and a steam leak
            const float pz = -10.3f;
            // both wall pipes run the whole wall: into the west corner and up to the east wall face (x -22.95)
            Cylinder("Pipe_Main", m, new Vector3(-59.25f, 9.6f, pz), new Vector3(-22.7f, 9.6f, pz), 0.28f, rust);
            Cylinder("Pipe_Down", m, new Vector3(-59.25f, 9.6f, pz), new Vector3(-59.25f, 0f, pz), 0.28f, rust);
            Cylinder("Pipe_Fire", m, new Vector3(-59.75f, 8.95f, -10.5f), new Vector3(-22.7f, 8.95f, -10.5f), 0.17f, red);
            for (float x = -57f; x > -24f; x -= 4.2f)
            {
                Box("PipeBracket", m, new Vector3(x, 9.3f, -10.62f), new Vector3(0.15f, 0.9f, 0.35f), dark);
                Box("PipeClamp", m, new Vector3(x, 9.6f, pz), new Vector3(0.12f, 0.66f, 0.66f), metal);
            }
            Cylinder("Joint_Leaking", m, new Vector3(-44.25f, 9.6f, pz), new Vector3(-43.75f, 9.6f, pz), 0.42f, metal);
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere); drop.name = "WaterDrop";
            Object.DestroyImmediate(drop.GetComponent<Collider>());
            drop.transform.SetParent(m, false); drop.transform.position = new Vector3(-44f, 9.25f, pz); drop.transform.localScale = new Vector3(0.09f, 0.13f, 0.09f);
            drop.GetComponent<Renderer>().sharedMaterial = water;
            GameObject puddle = MeshObj("Puddle", m, QuadXZ(1.6f, 1.1f, 1f, 1f), Lit("M_L3_Puddle", new Color(1f, 1f, 1f, 0.7f), 0.97f, 0f, Tex("T_L3_OilStain"), Vector2.one, null, false, true));
            puddle.transform.position = new Vector3(-44f, 0.008f, pz + 0.15f);
            var drip = m.gameObject.AddComponent<L3Drip>();
            drip.drop = drop.transform; drip.puddle = puddle.transform; drip.fallHeight = 9.2f;
            ParticleSystem steam = Steam(m, "SteamLeak", new Vector3(-27.5f, 9.6f, pz + 0.3f), Quaternion.LookRotation(new Vector3(0f, -0.35f, 1f)), 0.9f, 1.4f);

            // ---- hazard stripes on the floor (generator, fuse box, wing entrance), oil stains
            Hazard(m, hazard, new Vector3(-31f, 0f, 7.2f), 6.2f, 0.55f, 0f);
            Hazard(m, hazard, new Vector3(-37f, 0f, -9.5f), 6.2f, 0.55f, 0f);
            Hazard(m, hazard, new Vector3(-23.7f, 0f, 0f), 6.2f, 0.55f, 90f);
            foreach (var (p, s, yaw) in new[] { (new Vector3(-30.2f, 0f, 5.6f), 2.6f, 20f), (new Vector3(-32.6f, 0f, 4.4f), 1.4f, 110f), (new Vector3(-43.2f, 0f, 7.6f), 1.8f, 300f), (new Vector3(-36.4f, 0f, -8.2f), 1.2f, 45f) })
            {
                GameObject o = MeshObj("OilStain", m, QuadXZ(s, s * 0.8f, 1f, 1f), oil);
                o.transform.position = p + Vector3.up * 0.009f; o.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                o.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // ---- tool shadow board (one tool still hanging, the rest missing) on the south wall
            Transform tb = Group("ToolShadowBoard", m, new Vector3(-56.8f, 6f, -10.75f));
            tb.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            Box("Frame", tb, new Vector3(0f, 0f, 0.04f), new Vector3(4.7f, 4.7f, 0.1f), dark);
            GameObject face1 = MeshObj("Board", tb, QuadXY(4.5f, 4.5f, 0.5f, 1f, 1f), board);
            face1.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            Box("Hammer_Handle", tb, new Vector3(-0.58f, 0.54f, -0.08f), new Vector3(0.16f, 2.3f, 0.12f), Lit("M_L3_WoodHandle", new Color(0.42f, 0.28f, 0.15f), 0.3f));
            Box("Hammer_Head", tb, new Vector3(-0.54f, 1.61f, -0.1f), new Vector3(0.95f, 0.36f, 0.22f), metal);
            TextPlate(tb, "Label", "TOOL CHECK-OUT  ·  RETURN EVERY TOOL", new Vector3(0f, 2.55f, -0.06f), 4.4f, 0.35f, new Color(0.9f, 0.85f, 0.7f), 1.6f, true);

            // ---- electrical panel (west wall, south corner) + temperature warning sign
            Transform ep = Group("ElectricalPanel", m, new Vector3(-59.35f, 5.2f, -9.2f));
            ep.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            Box("Cabinet", ep, new Vector3(0f, 0f, 0.15f), new Vector3(2.4f, 3.6f, 0.35f), Lit("M_L3_PanelGrey", new Color(0.36f, 0.38f, 0.4f), 0.35f, 0.6f));
            Box("DoorSeam", ep, new Vector3(0f, 0f, -0.04f), new Vector3(0.03f, 3.4f, 0.02f), dark);
            Box("Handle", ep, new Vector3(0.35f, 0f, -0.07f), new Vector3(0.08f, 0.5f, 0.08f), metal);
            TextPlate(ep, "Danger", "<b>DANGER</b>\n415 V", new Vector3(-0.43f, 1.2f, -0.06f), 0.9f, 0.6f, Color.black, 1.5f, false, yellow);
            ep.Find("Danger").localScale = Vector3.one * 0.65f;   // fits the left door between the down-pipe and the seam
            Transform ts = Group("TempWarningSign", m, new Vector3(-43f, 7.2f, -10.78f));
            ts.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            TextPlate(ts, "Sign", "<b>WARNING</b>\nHIGH TEMPERATURE\n<size=70%>DO NOT TOUCH PIPES</size>", Vector3.zero, 3f, 1.6f, Color.black, 3f, false, yellow);

            // ---- broken wall fan (north wall, between the workbench and the generator)
            Transform fan = Group("BrokenWallFan", m, new Vector3(-37f, 8.1f, 10.75f));
            fan.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Cylinder("Housing", fan, fan.TransformPoint(new Vector3(0f, 0f, -0.05f)), fan.TransformPoint(new Vector3(0f, 0f, -0.6f)), 1.55f, dark);
            Transform blades = new GameObject("Blades").transform; blades.SetParent(fan, false); blades.localPosition = new Vector3(0f, 0f, -0.7f);
            for (int i = 0; i < 3; i++)
            {
                GameObject bl = Box("Blade", blades, Vector3.zero, new Vector3(0.42f, 1.2f, 0.04f), metal);   // three equal blades
                Quaternion around = Quaternion.Euler(0f, 0f, i * 120f);
                bl.transform.localRotation = around * Quaternion.Euler(0f, 22f, 0f);   // pitch around the blade's own length
                bl.transform.localPosition = around * new Vector3(0f, 0.68f, 0f);
            }
            // no wire cage over the fan (cleaner look); a small hub covers the blade roots instead
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Hub";
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.SetParent(fan, false);
            hub.transform.localPosition = blades.localPosition + new Vector3(0f, 0f, -0.06f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hub.transform.localScale = new Vector3(0.36f, 0.07f, 0.36f);
            hub.GetComponent<MeshRenderer>().sharedMaterial = dark;   // same material as the housing
            fan.gameObject.AddComponent<L3BrokenFan>().blades = blades;

            // ---- maintenance paperwork pinned beside the pegboard (decoration; Phase 7 adds readable story items)
            foreach (var (x, title) in new[] { (-50.1f, "WORK ORDER #4471\nCOOLING LOOP B\nAssigned: R. RAMOS\nStatus: <color=#A02020>OVERDUE</color>"), (-48.6f, "MAINT. LOG\nFuse x/y swap\nGenerator test\n<color=#A02020>NOT SIGNED OFF</color>") })
            {
                Transform pp = Group("MaintenanceReport", m, new Vector3(x, 5.6f, 10.77f));
                pp.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                TextPlate(pp, "Sheet", title, Vector3.zero, 1.25f, 1.6f, new Color(0.12f, 0.12f, 0.14f), 1.2f, false, paper);
            }
            log.AppendLine("Maintenance: " + lamps + " caged red-orange work lamps (added to the wing's power list, dim until the generator runs), " +
                           "rusty + fire pipes with brackets, dripping joint + puddle, steam leak, hazard stripes (generator, fuse box, entrance), " +
                           "4 oil stains, tool shadow board, electrical panel, high-temperature sign, broken stuttering wall fan, 2 maintenance reports");
        }

        static void Hazard(Transform p, Material m, Vector3 center, float len, float width, float yaw)
        {
            GameObject h = MeshObj("HazardStripe", p, QuadXZ(len, width, len / (width * 2f), 0.5f), m);
            h.transform.position = center + Vector3.up * 0.011f;
            h.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ================================================================== SECURITY (blue)
        static void SecurityWing(Transform g)
        {
            Transform s = Group("Security", g, Vector3.zero);
            Material cage = Lit("M_L3_CageMesh", new Color(0.18f, 0.18f, 0.19f), 0.4f, 0.7f, Tex("T_L3_TrayMesh"), Vector2.one, null, true, false, true);
            Material post = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
            Material desk = Lit("M_L3_OfficeDesk", new Color(0.24f, 0.25f, 0.27f), 0.35f, 0.4f);
            Material beige = Lit("M_L3_OfficeBeige", new Color(0.55f, 0.53f, 0.47f), 0.3f);
            Material black = Lit("M_L3_OfficeBlack", new Color(0.05f, 0.05f, 0.055f), 0.4f);
            Material stat = Asset("M_L3_StaticScreenUnlit", Shader.Find("Universal Render Pipeline/Unlit"));
            stat.SetTexture("_BaseMap", Tex("T_L3_StaticNoise")); stat.SetTextureScale("_BaseMap", new Vector2(1.4f, 1.1f));
            stat.SetColor("_BaseColor", new Color(0.62f, 0.78f, 1f)); EditorUtility.SetDirty(stat);
            Material paper = Lit("M_L3_Paper", new Color(0.85f, 0.83f, 0.76f), 0.15f);
            Material ledRed = Lit("M_L3_LedRed", new Color(0.2f, 0.02f, 0.02f), 0.5f, 0f, null, Vector2.one, new Color(3f, 0.2f, 0.1f));

            // ---- mesh security cage around the guard desk, with a wide opening in front (colliders on the panels)
            Transform c = Group("GuardCage", s, Vector3.zero);
            const float x0 = 33.5f, x1 = 48.5f, zf = 1.0f, zb = 10.8f, hgt = 8.6f;
            CagePanel(c, cage, post, new Vector3(x0, 0f, zf), new Vector3(38.5f, 0f, zf), hgt);
            CagePanel(c, cage, post, new Vector3(43.5f, 0f, zf), new Vector3(x1, 0f, zf), hgt);
            CagePanel(c, cage, post, new Vector3(x0, 0f, zf), new Vector3(x0, 0f, zb), hgt);
            CagePanel(c, cage, post, new Vector3(x1, 0f, zf), new Vector3(x1, 0f, zb), hgt);
            Box("GateHeader", c, new Vector3(41f, hgt, zf), new Vector3(5.2f, 0.18f, 0.18f), post);
            TextPlate(c, "CageSign", "<b>GUARD POST</b>\n<size=70%>AUTHORIZED PERSONNEL ONLY</size>", new Vector3(36f, 7.2f, zf - 0.08f), 3.4f, 1.2f, Color.white, 2f, false,
                      Lit("M_L3_SignBlue", new Color(0.08f, 0.2f, 0.55f), 0.35f), Quaternion.LookRotation(Vector3.forward));
            Light blue = PointLight(c, "MonitorFill", new Vector3(41f, 6.5f, 7.5f), CBlue, 6f, 12f);

            // ---- abandoned office corner on the south wall: desk, static monitors, keyboard, binders, papers, filing cabinet
            Transform o = Group("OfficeCorner", s, new Vector3(44f, 0f, -9.55f));
            Box("DeskTop", o, new Vector3(0f, 2.85f, 0f), new Vector3(6f, 0.16f, 2.3f), desk).AddComponent<BoxCollider>();
            foreach (float lx in new[] { -2.8f, 2.8f }) Box("DeskLeg", o, new Vector3(lx, 1.4f, 0f), new Vector3(0.15f, 2.8f, 2.1f), desk);
            for (int i = 0; i < 2; i++)
            {
                float mx = -1.4f + i * 2.6f;
                Box("CRT_" + i, o, new Vector3(mx, 3.85f, -0.35f), new Vector3(1.8f, 1.6f, 1.4f), beige);
                GameObject scr = MeshObj("StaticScreen", o, QuadXY(1.45f, 1.15f, 0.5f, 1f, 1f), stat);
                scr.transform.localPosition = new Vector3(mx, 3.9f, 0.36f);
                scr.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                scr.AddComponent<L3ScreenStatic>();
                Box("Keyboard_" + i, o, new Vector3(mx, 2.96f, 0.55f), new Vector3(1.5f, 0.06f, 0.5f), black);
            }
            Light scrGlow = PointLight(o, "StaticGlow", o.TransformPoint(new Vector3(0f, 4f, 1.6f)), new Color(0.5f, 0.7f, 1f), 3.5f, 6f);
            var colours = new[] { new Color(0.1f, 0.2f, 0.5f), new Color(0.5f, 0.1f, 0.08f), new Color(0.12f, 0.12f, 0.12f) };
            for (int i = 0; i < 3; i++)
                // beside the left monitor (CRT_0 spans x -2.3..-0.5), not inside it
                Box("Binder_" + i, o, new Vector3(-2.88f + i * 0.225f, 3.4f, -0.8f), new Vector3(0.21f, 0.95f, 0.75f), Lit("M_L3_Binder_" + i, colours[i], 0.35f));
            for (int i = 0; i < 4; i++)
            {
                GameObject pp = MeshObj("Paper", o, QuadXZ(0.6f, 0.8f, 1f, 1f), paper);
                pp.transform.localPosition = new Vector3(0.6f + i * 0.35f, 2.94f + i * 0.002f, 0.3f - i * 0.1f);
                pp.transform.localRotation = Quaternion.Euler(0f, -20f + i * 17f, 0f);
            }
            Transform fc = Group("FilingCabinet", s, new Vector3(39.3f, 0f, -9.75f));
            Box("Body", fc, new Vector3(0f, 2.3f, 0f), new Vector3(1.9f, 4.6f, 1.9f), Lit("M_L3_PanelGrey", new Color(0.36f, 0.38f, 0.4f), 0.35f, 0.6f)).AddComponent<BoxCollider>();
            for (int i = 0; i < 4; i++)
            {
                Box("Drawer", fc, new Vector3(0f, 0.6f + i * 1.1f, 0.96f), new Vector3(1.7f, 0.95f, 0.04f), Lit("M_L3_PanelGreyLight", new Color(0.45f, 0.47f, 0.49f), 0.35f, 0.6f));
                Box("Pull", fc, new Vector3(0f, 0.85f + i * 1.1f, 1.0f), new Vector3(0.6f, 0.08f, 0.06f), post);
            }

            // ---- RESTRICTED stencil on the hall side of the Security doorway
            Transform r = Group("RestrictedStencil", s, new Vector3(21.98f, 5.2f, -5.6f));
            r.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            var rt = Label(r, "Stencil", "<b>RESTRICTED</b>\n<size=55%>SECURITY PERSONNEL ONLY</size>", Vector3.zero, Quaternion.identity, 4.2f, 1.8f, new Color(0.92f, 0.9f, 0.82f, 0.85f), 3.2f);
            rt.characterSpacing = 10f;

            // ---- broken badge reader beside the Security door (wing side), sparking now and then
            Transform br = Group("BrokenBadgeReader", s, new Vector3(23.05f, 4.4f, -4.4f));
            br.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            Box("Body", br, new Vector3(0f, 0f, 0.05f), new Vector3(0.6f, 0.95f, 0.18f), black);
            Box("CardSlotDamaged", br, new Vector3(0f, -0.15f, -0.05f), new Vector3(0.42f, 0.06f, 0.04f), post);
            Box("Led", br, new Vector3(0f, 0.3f, -0.05f), new Vector3(0.12f, 0.06f, 0.03f), ledRed);
            Dangle(br, "Wires", br.TransformPoint(new Vector3(0.1f, -0.48f, 0f)), 2.9f, 0.03f, Lit("M_L3_Cable_Orange", new Color(0.8f, 0.3f, 0.06f), 0.45f));
            ParticleSystem sp = Sparks(br, br.TransformPoint(new Vector3(0f, -0.2f, -0.15f)));
            Light fl = PointLight(br, "SparkFlash", br.TransformPoint(new Vector3(0f, 0f, -0.6f)), new Color(1f, 0.75f, 0.4f), 0f, 5f);
            var spk = br.gameObject.AddComponent<L3Sparks>(); spk.sparks = sp; spk.flash = fl;

            // ---- CCTV cameras in two corners (slow sweep, follow the player when close)
            CctvCamera(s, new Vector3(59.2f, 9.7f, 10.45f), new Vector3(-1f, -0.45f, -1f), black, ledRed);
            CctvCamera(s, new Vector3(23.4f, 9.7f, -10.45f), new Vector3(1f, -0.45f, 1f), black, ledRed);
            log.AppendLine("Security: mesh guard cage (4 panels with colliders, 5-unit opening in front of the desk), blue monitor fill light, " +
                           "office corner (desk, 2 CRTs with static, keyboards, binders, papers, filing cabinet), RESTRICTED stencil at the hall doorway, " +
                           "broken badge reader with sparks + flash, 2 CCTV cameras (sweep / follow). The 6 puzzle camera feeds are untouched.");
        }

        static void CagePanel(Transform p, Material mesh, Material post, Vector3 a, Vector3 b, float h)
        {
            Vector3 d = b - a; float len = d.magnitude;
            Transform t = new GameObject("CagePanel").transform; t.SetParent(p, false);
            t.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(Vector3.Cross(Vector3.up, d.normalized), Vector3.up));
            GameObject q = MeshObj("Mesh", t, QuadXY(len, h, 0f, len / 0.45f, h / 0.45f), mesh);
            q.transform.localPosition = Vector3.zero;
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, h * 0.5f, 0f); bc.size = new Vector3(len, h, 0.12f);
            Box("Post", t, new Vector3(-len * 0.5f, h * 0.5f, 0f), new Vector3(0.14f, h, 0.14f), post);
            Box("Post", t, new Vector3(len * 0.5f, h * 0.5f, 0f), new Vector3(0.14f, h, 0.14f), post);
            Box("TopRail", t, new Vector3(0f, h, 0f), new Vector3(len, 0.12f, 0.12f), post);
            Box("MidRail", t, new Vector3(0f, h * 0.45f, 0f), new Vector3(len, 0.08f, 0.08f), post);
        }

        static void CctvCamera(Transform p, Vector3 pos, Vector3 look, Material body, Material led)
        {
            Transform c = Group("CCTV_Camera", p, pos);
            Box("Mount", c, new Vector3(0f, 0.25f, 0f), new Vector3(0.25f, 0.5f, 0.25f), body);
            Transform head = new GameObject("Head").transform; head.SetParent(c, false);
            head.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            Box("Body", head, new Vector3(0f, 0f, 0.35f), new Vector3(0.42f, 0.38f, 0.95f), Lit("M_L3_CctvWhite", new Color(0.75f, 0.75f, 0.72f), 0.4f));
            Box("Hood", head, new Vector3(0f, 0.22f, 0.45f), new Vector3(0.5f, 0.06f, 1.05f), body);
            Cylinder("Lens", head, head.TransformPoint(new Vector3(0f, 0f, 0.82f)), head.TransformPoint(new Vector3(0f, 0f, 0.9f)), 0.13f, body);
            Box("Led", head, new Vector3(0.14f, -0.1f, 0.84f), new Vector3(0.05f, 0.05f, 0.03f), led);
            c.gameObject.AddComponent<L3CameraPan>().head = head;
        }

        // ================================================================== CONTROL (green)
        static void ControlWing(Transform g)
        {
            Transform c = Group("Control", g, Vector3.zero);
            Material panel = Lit("M_L3_ControlPanel", new Color(0.07f, 0.08f, 0.085f), 0.45f, 0.5f);
            Material frame = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
            Material lampMat = Lit("M_L3_DiagramLamp", new Color(0.05f, 0.05f, 0.05f), 0.5f, 0f, null, Vector2.one, new Color(0.5f, 0.05f, 0.03f));
            Material lineMat = Lit("M_L3_DiagramLine", new Color(0.05f, 0.06f, 0.06f), 0.4f, 0f, null, Vector2.one, new Color(0.08f, 0.35f, 0.15f));
            Material screen = Lit("M_L3_GreenScreen", new Color(0.01f, 0.04f, 0.02f), 0.8f, 0f, null, Vector2.one, new Color(0.02f, 0.12f, 0.05f));
            Material toggle = Lit("M_L3_ToggleSilver", new Color(0.6f, 0.6f, 0.62f), 0.6f, 0.9f);

            // ---- green glow from the control desk screens (on the UPS: always on, low)
            Light deskGlow = PointLight(c, "ScreenGlow_Green", new Vector3(0f, 4.6f, 40.2f), CGreen, 5f, 11f);

            // ---- facility system diagram on the west wall (reads existing puzzle flags only)
            Transform d = Group("SystemDiagram", c, new Vector3(-16.38f, 6.5f, 40.6f));
            d.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);     // front faces +x (into the room)
            Box("Board", d, new Vector3(0f, 0f, 0.1f), new Vector3(7.2f, 5f, 0.2f), panel);
            Box("FrameTop", d, new Vector3(0f, 2.55f, 0f), new Vector3(7.4f, 0.12f, 0.3f), frame);
            Box("FrameBottom", d, new Vector3(0f, -2.55f, 0f), new Vector3(7.4f, 0.12f, 0.3f), frame);
            Label(d, "Title", "<b>FACILITY SYSTEMS</b>", new Vector3(0f, 2.05f, -0.02f), Quaternion.identity, 6.5f, 0.6f, new Color(0.7f, 0.95f, 0.75f), 2.2f);
            var defs = new (string label, string flag, string intKey, int val, Vector2 p)[]
            {
                ("GENERATOR", "solved_gen", null, 0, new Vector2(-2.6f, 0.9f)),
                ("POWER BUS", "solved_gen", null, 0, new Vector2(0f, 0.9f)),
                ("SECURITY NET", "solved_s3", null, 0, new Vector2(2.6f, 0.9f)),
                ("COOLING", null, "fan", 3, new Vector2(-2.6f, -0.7f)),
                ("SERVER COUNT", "solved_c2", null, 0, new Vector2(0f, -0.7f)),
                ("CORE REBOOT", "solved_c3", null, 0, new Vector2(2.6f, -0.7f)),
            };
            var nodes = new List<L3SystemDiagram.Node>();
            foreach (var n in defs)
            {
                // board front is local -z (facing into the room); local +x is the viewer's right
                Vector3 lp = new Vector3(n.p.x, n.p.y, -0.03f);
                GameObject lamp = Box("Node_" + n.label, d, lp, new Vector3(0.55f, 0.55f, 0.06f), lampMat);
                TextMeshPro t = Label(d, "NodeLabel", n.label, lp + new Vector3(0f, -0.55f, -0.01f), Quaternion.identity, 2.3f, 0.4f, new Color(1f, 0.35f, 0.3f), 1.2f);
                nodes.Add(new L3SystemDiagram.Node { label = n.label, flag = n.flag, intKey = n.intKey, intValue = n.val, lamp = lamp.GetComponent<Renderer>(), text = t });
            }
            foreach (var (a, b) in new[] { (0, 1), (1, 2), (1, 4), (3, 4), (4, 5) })
            {
                Vector3 pa = new Vector3(defs[a].p.x, defs[a].p.y, -0.015f), pb = new Vector3(defs[b].p.x, defs[b].p.y, -0.015f);
                Vector3 mid = (pa + pb) * 0.5f; Vector3 dd = pb - pa;
                GameObject ln = Box("Link", d, mid, new Vector3(Mathf.Abs(dd.x) + 0.06f, Mathf.Abs(dd.y) + 0.06f, 0.02f), lineMat);
            }
            TextMeshPro status = Label(d, "Status", "SYSTEM FAILURE", new Vector3(0f, -1.95f, -0.02f), Quaternion.identity, 6.5f, 0.5f, Color.white, 1.6f);
            Light diagGlow = PointLight(d, "DiagramGlow", d.TransformPoint(new Vector3(0f, 0f, -1.2f)), new Color(1f, 0.3f, 0.2f), 2.5f, 6f);
            var diag = d.gameObject.AddComponent<L3SystemDiagram>();
            diag.nodes = nodes.ToArray(); diag.statusLine = status; diag.glow = diagGlow;

            // ---- electrical switch panel (east wall, north part)
            Transform sw = Group("SwitchPanel", c, new Vector3(16.38f, 5.4f, 40.4f));
            sw.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            Box("Cabinet", sw, new Vector3(0f, 0f, 0.15f), new Vector3(4.8f, 3.6f, 0.3f), Lit("M_L3_PanelGrey", new Color(0.36f, 0.38f, 0.4f), 0.35f, 0.6f));
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 6; i++)
                {
                    float x = -1.9f + i * 0.76f, y = 0.6f - row * 1.3f;
                    Box("SwitchBase", sw, new Vector3(x, y, -0.04f), new Vector3(0.38f, 0.6f, 0.06f), frame);
                    GameObject lever = Box("Toggle", sw, new Vector3(x, y + ((i + row) % 3 == 0 ? -0.12f : 0.12f), -0.14f), new Vector3(0.1f, 0.28f, 0.14f), toggle);
                    lever.transform.localRotation = Quaternion.Euler((i + row) % 3 == 0 ? -25f : 25f, 0f, 0f);
                }
            Label(sw, "Title", "<b>DISTRIBUTION BOARD  DB-C</b>", new Vector3(0f, 1.45f, -0.02f), Quaternion.identity, 4.4f, 0.4f, new Color(0.92f, 0.9f, 0.8f), 1.2f);

            // ---- two server status displays (east wall, south part) and a warning banner above the desk
            for (int i = 0; i < 2; i++)
            {
                Transform sd = Group("StatusDisplay_" + i, c, new Vector3(16.38f, 6.2f, 24.6f + i * 3.4f));
                sd.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
                Box("Bezel", sd, new Vector3(0f, 0f, 0.08f), new Vector3(3f, 2f, 0.16f), frame);
                GameObject q = MeshObj("Screen", sd, QuadXY(2.7f, 1.7f, 0.5f, 1f, 1f), screen);
                q.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                string text = i == 0
                    ? "<color=#5FD383>NODE STATUS</color>\nCORE TEMP   <color=#FF4A3D>CRITICAL</color>\nUPS         <color=#E8C547>ON BATTERY</color>\nNETWORK     <color=#E8C547>DEGRADED</color>"
                    : "<color=#5FD383>EVENT LOG</color>\n<color=#FF4A3D>!! KERNEL PANIC</color>\n<color=#FF4A3D>!! COOLING FAULT</color>\n<color=#9FB3BF>.. awaiting manual reboot</color>";
                Label(sd, "Text", text, new Vector3(0f, 0f, -0.03f), Quaternion.identity, 2.5f, 1.55f, new Color(0.6f, 1f, 0.7f), 1.3f, TextAlignmentOptions.TopLeft);
            }
            Transform banner = Group("WarningBanner", c, new Vector3(0f, 9.55f, 44.5f));
            banner.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            TextPlate(banner, "Banner", "<b>WARNING:</b> SYSTEM HALTED  ·  MANUAL REBOOT REQUIRED", Vector3.zero, 9f, 0.8f, Color.black, 2f, false,
                      Lit("M_L3_SignYellow", new Color(0.9f, 0.7f, 0.08f), 0.35f));

            // ---- steam venting from the top of the cooling unit
            Steam(c, "CoolingSteam", new Vector3(-15.2f, 8.75f, 33f), Quaternion.LookRotation(Vector3.up), 1.2f, 1.0f);
            log.AppendLine("Control: green screen glow light, facility system diagram (6 nodes reading solved_gen / solved_s3 / fan==3 / solved_c2 / solved_c3; " +
                           "red flashing until fixed, then green + 'SYSTEM ONLINE'), distribution switch board, 2 status displays, warning banner, steam from the cooling unit");
        }

        // ================================================================== shared helpers
        static TextMeshPro Label(Transform parent, string name, string text, Vector3 localPos, Quaternion localRot, float w, float h, Color col, float size,
                                 TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text; t.color = col; t.alignment = align;
            t.enableAutoSizing = true; t.fontSizeMin = 0.3f; t.fontSizeMax = size;
            t.rectTransform.sizeDelta = new Vector2(w, h);
            t.textWrappingMode = TextWrappingModes.Normal;
            TMP_FontAsset font = FindSceneFont();
            if (font != null) t.font = font;
            return t;
        }

        // A plate (optional material) with text on its front. The group's forward points INTO the wall,
        // so the readable side is local -z.
        static void TextPlate(Transform parent, string name, string text, Vector3 localPos, float w, float h, Color textCol, float size, bool textOnly,
                              Material plate = null, Quaternion? rot = null)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = rot ?? Quaternion.identity;
            if (!textOnly && plate != null) Box("Plate", t, new Vector3(0f, 0f, 0.02f), new Vector3(w + 0.2f, h + 0.2f, 0.04f), plate);
            Label(t, "Text", text, new Vector3(0f, 0f, -0.01f), Quaternion.identity, w, h, textCol, size);
        }

        static ParticleSystem Steam(Transform parent, string name, Vector3 pos, Quaternion rot, float rate, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f; main.loop = true; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed * 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startColor = new Color(0.85f, 0.9f, 0.95f, 0.22f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;
            var em = ps.emission; em.rateOverTime = rate * 8f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.12f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat("M_L3_Steam", "T_L3_SteamPuff", false);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static ParticleSystem Sparks(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down + parent.forward * -0.6f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new Color(1f, 0.8f, 0.35f, 1f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.02f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2f;
            r.sharedMaterial = ParticleMat("M_L3_Spark", "T_L3_Spark", true);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static Material ParticleMat(string name, string tex, bool additive)
        {
            Material m = Asset(name, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", Tex(tex));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================== play-mode self test (Phase 3)
        class PlayTest3
        {
            readonly string dir; readonly System.Text.StringBuilder r = new System.Text.StringBuilder();
            int frame; float fanStart; bool fanRead; int sparksSeen; float minDrop = 999f, maxDrop = -999f;
            public PlayTest3(string d) { dir = d; }
            public bool Step()
            {
                var g = CodeBreaker.Level2.L2Game.Instance;
                if (g == null) { r.AppendLine("FAIL: L2Game not found"); return Done(); }
                frame++;
                var fan = Object.FindFirstObjectByType<L3BrokenFan>();
                var drip = Object.FindFirstObjectByType<L3Drip>();
                var spk = Object.FindFirstObjectByType<L3Sparks>();
                if (!fanRead && fan != null && fan.blades != null) { fanStart = fan.blades.localEulerAngles.z; fanRead = true; }
                if (drip != null && drip.drop != null && drip.drop.gameObject.activeSelf) { minDrop = Mathf.Min(minDrop, drip.drop.position.y); maxDrop = Mathf.Max(maxDrop, drip.drop.position.y); }
                if (spk != null && spk.sparks != null) sparksSeen = Mathf.Max(sparksSeen, spk.sparks.particleCount);
                if (frame == 600)
                {
                    r.AppendLine("Broken fan rotated: " + (fan != null && fan.blades != null && Mathf.Abs(Mathf.DeltaAngle(fanStart, fan.blades.localEulerAngles.z)) > 0.5f ? "yes (PASS)" : "no (check)"));
                    r.AppendLine("Water drop fell: y " + maxDrop.ToString("0.0") + " -> " + minDrop.ToString("0.0") + (maxDrop - minDrop > 3f ? " (PASS)" : " (check)"));
                    r.AppendLine("Badge reader sparks seen: " + sparksSeen + " particles at once " + (sparksSeen > 0 ? "(PASS)" : "(check: interval 3-8 s)"));
                    var diag = Object.FindFirstObjectByType<L3SystemDiagram>();
                    if (diag == null) { r.AppendLine("FAIL: system diagram missing"); return Done(); }
                    diag.SendMessage("Update");
                    string before = diag.statusLine != null ? diag.statusLine.text : "";
                    // simulate a finished reboot in memory only (never saved), then restore
                    bool had = g.Flag("solved_c3"); g.SetFlag("solved_c3");
                    typeof(L3SystemDiagram).GetField("check", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(diag, 0f);
                    diag.SendMessage("Update");
                    string after = diag.statusLine != null ? diag.statusLine.text : "";
                    if (!had) g.SetFlag("solved_c3", false);
                    typeof(L3SystemDiagram).GetField("check", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(diag, 0f);
                    diag.SendMessage("Update");
                    r.AppendLine("Diagram before: " + StripTags(before));
                    r.AppendLine("Diagram with solved_c3 (memory only): " + StripTags(after) + (after.Contains("ONLINE") ? " (PASS)" : " (FAIL)"));
                    r.AppendLine("solved_c3 restored to " + g.Flag("solved_c3"));
                    var power = new List<string>();
                    foreach (var p in Object.FindObjectsByType<L3RoomPower>()) power.Add(p.poweredFlag + ": " + p.lights.Length + " lights");
                    r.AppendLine("Power groups: " + string.Join(", ", power));
                    return Done();
                }
                return false;
            }
            static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<.*?>", "");
            bool Done()
            {
                r.AppendLine("Lights in scene: " + Object.FindObjectsByType<Light>().Length);
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 3 play test:\n" + r);
                return true;
            }
        }
        static PlayTest3 test3;
        static void Test3Tick()
        {
            if (test3 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test3Tick; test3 = null; return; }
            if (test3.Step()) { EditorApplication.update -= Test3Tick; test3 = null; }
        }
        static void StartPlayTest3(string dir) { if (test3 != null) return; test3 = new PlayTest3(dir); EditorApplication.update += Test3Tick; }
    }
}
