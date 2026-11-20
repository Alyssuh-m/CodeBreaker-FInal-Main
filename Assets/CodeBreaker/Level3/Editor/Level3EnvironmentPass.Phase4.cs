using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 4: wayfinding + small facility details. Everything goes under "Level3/L3_Environment/Phase4_Details".
    // Existing objects are only REFERENCED (rack LEDs for blinking, rack positions); nothing existing is edited.
    public static partial class Level3EnvironmentPass
    {
        const string Group4 = "Phase4_Details";

        [MenuItem("Code Breaker/Level 3/Phase 4 - Build Details")]
        public static void Build4()
        {
            if (!OpenLevel3()) return;
            log.Length = 0; mats.Clear();
            EnsureFolders();
            ImportTextures();
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(Group4);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform g = new GameObject(Group4).transform; g.SetParent(env, false);

            DoorFrames(g);
            RackDetails(level, g);
            FacilityDetails(g);
            WarningNotes(g);

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            AssetDatabase.SaveAssets();
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase4");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), log.ToString());
            Debug.Log("[Level 3 Env] Phase 4 built and saved.\n" + log);
        }

        [MenuItem("Code Breaker/Level 3/Phase 4 - Undo (remove Details pass)")]
        public static void UndoPhase4()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform old = level != null ? level.Find("L3_Environment/" + Group4) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 4 removed.");
        }

        [MenuItem("Code Breaker/Level 3/Phase 4 - Capture Screenshots")]
        public static void Capture4()
        {
            if (!OpenLevel3()) return;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase4");
            Directory.CreateDirectory(dir);
            var shots = new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("D1_doorframe_maintenance", new Vector3(-15f, 6.2f, 1.2f), new Vector3(-22.5f, 4.5f, -0.5f), 66f),
                ("D2_doorframe_security", new Vector3(15f, 6.2f, 1.2f), new Vector3(22.5f, 4.5f, -0.5f), 66f),
                ("D3_doorframe_control", new Vector3(5f, 6.2f, 12f), new Vector3(0f, 4.5f, 22f), 66f),
                ("D4_open_rack", new Vector3(17.5f, 5.6f, -10.5f), new Vector3(12.3f, 3f, -4.8f), 62f),
                ("D5_south_wall_safety", new Vector3(4.5f, 7.2f, -12.5f), new Vector3(-1f, 5.5f, -22.5f), 84f),
                ("D6_rack_notes_labels", new Vector3(-18f, 5.6f, 0.5f), new Vector3(-12f, 3.5f, -3.5f), 62f),
                ("D7_hall_overview", new Vector3(-19f, 7.6f, -19f), new Vector3(2f, 3f, 6f), 74f),
                ("D8_control_entrance_sign", new Vector3(-2f, 6f, 10f), new Vector3(-5.5f, 5.5f, 22f), 60f),
            };
            foreach (var s in shots) Shot(dir, s.name, s.pos, s.look, s.fov);
            Debug.Log("[Level 3 Env] Phase 4 captures written to " + dir);
        }

        // ================================================================== door-frame lighting
        static void DoorFrames(Transform g)
        {
            Transform d = Group("DoorFrameLights", g, Vector3.zero);
            const float face = Half - 0.45f - 0.06f;     // just proud of the hall-side wall face
            Frame(d, "Frame_Maintenance", new Vector3(-face, 0f, 0f), Vector3.right, new Color(1f, 0.28f, 0.12f), "card_red_in");
            Frame(d, "Frame_Security", new Vector3(face, 0f, 0f), Vector3.left, new Color(0.2f, 0.45f, 1f), "card_blue_in");
            Frame(d, "Frame_Control", new Vector3(0f, 0f, face), Vector3.back, new Color(0.2f, 1f, 0.35f), "card_green_in");
            log.AppendLine("Door frames: red/orange (Maintenance), blue (Security), green (Control) light strips + 1 glow light each; " +
                           "fade to white after that wing's keycard is inserted (reads card_red_in / card_blue_in / card_green_in)");
        }

        static void Frame(Transform parent, string name, Vector3 pos, Vector3 intoHall, Color col, string flag)
        {
            Transform f = new GameObject(name).transform;
            f.SetParent(parent, false);
            f.SetPositionAndRotation(pos, Quaternion.LookRotation(-intoHall, Vector3.up));   // forward points into the wall
            Material strip = Lit("M_L3_DoorFrameStrip", new Color(0.3f, 0.3f, 0.3f), 0.5f, 0f, null, Vector2.one, new Color(1f, 1f, 1f));
            Material housing = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
            var rs = new List<Renderer>();
            foreach (int s in new[] { -1, 1 })
            {
                Box("Housing", f, new Vector3(s * 3.32f, 4.65f, 0f), new Vector3(0.26f, 9.3f, 0.1f), housing);
                rs.Add(Box("Strip", f, new Vector3(s * 3.32f, 4.65f, -0.06f), new Vector3(0.12f, 9.1f, 0.04f), strip).GetComponent<Renderer>());
            }
            Box("Housing", f, new Vector3(0f, 9.4f, 0f), new Vector3(6.9f, 0.26f, 0.1f), housing);
            rs.Add(Box("Strip", f, new Vector3(0f, 9.4f, -0.06f), new Vector3(6.7f, 0.12f, 0.04f), strip).GetComponent<Renderer>());
            Light l = PointLight(f, "FrameGlow", f.TransformPoint(new Vector3(0f, 5f, -1.2f)), col, 4f, 7f);
            var dl = f.gameObject.AddComponent<L3DoorFrameLight>();
            dl.insertedFlag = flag; dl.wingColor = col; dl.strips = rs.ToArray(); dl.glow = l;
        }

        // ================================================================== racks: LED rhythms, labels, one open rack
        static void RackDetails(Transform level, Transform g)
        {
            Transform r = Group("RackDetails", g, Vector3.zero);
            var leds = new List<Renderer>();
            Transform openRack = null;
            var racks = new List<Transform>();
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Rack_") && t.parent != null && t.parent.name == "Racks") racks.Add(t);
            racks.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            Material plate = Lit("M_L3_LabelPlate", new Color(0.06f, 0.06f, 0.07f), 0.4f);
            foreach (Transform rack in racks)
            {
                foreach (Transform c in rack) if (c.name == "Led") { Renderer rr = c.GetComponent<Renderer>(); if (rr != null) leds.Add(rr); }
                if (rack.name == "Rack_8_Red") openRack = rack;
                Renderer body = rack.Find("RackBody") != null ? rack.Find("RackBody").GetComponent<Renderer>() : null;
                if (body == null) continue;
                Bounds b = body.bounds;
                Transform lab = Group("Label_" + rack.name, r, new Vector3(rack.position.x, 0.8f, b.min.z - 0.02f));
                lab.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                TextPlate(lab, "Plate", "SRV-R0" + rack.name.Substring(5, 1), Vector3.zero, 0.9f, 0.28f, new Color(0.85f, 0.88f, 0.9f), 1.2f, false, plate);
            }
            var blink = r.gameObject.AddComponent<L3RackLedBlink>();
            blink.leds = leds.ToArray();

            if (openRack != null)
            {
                Bounds b = openRack.Find("RackBody").GetComponent<Renderer>().bounds;
                Transform o = Group("OpenRack_R08", r, Vector3.zero);
                Material grille = Lit("M_L3_RackRearGrille", new Color(0.09f, 0.09f, 0.1f), 0.35f, 0.6f, Tex("T_L3_TrayMesh"), Vector2.one, null, true, false, true);
                Material frame = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
                Material drive = Lit("M_L3_DriveCaddy", new Color(0.18f, 0.19f, 0.2f), 0.5f, 0.7f);
                Material driveLed = Lit("M_L3_LedRed", new Color(0.2f, 0.02f, 0.02f), 0.5f, 0f, null, Vector2.one, new Color(3f, 0.2f, 0.1f));
                // hinge on the outer (east) edge of the front; the door hangs open ~110 degrees
                Transform hinge = Group("DoorHinge", o, new Vector3(b.max.x, 0f, b.min.z - 0.05f));
                hinge.rotation = Quaternion.Euler(0f, -110f, 0f);
                Box("DoorFrame", hinge, new Vector3(-1.1f, 3.5f, 0f), new Vector3(2.2f, 6.7f, 0.06f), frame);
                GameObject mesh = MeshObj("DoorGrille", hinge, QuadXY(1.95f, 6.3f, 0.5f, 13f, 42f), grille);
                mesh.transform.localPosition = new Vector3(-1.1f, 3.5f, -0.04f);
                Box("DoorHandle", hinge, new Vector3(-2.05f, 3.6f, -0.08f), new Vector3(0.06f, 0.8f, 0.08f), Lit("M_L3_PipeSteel", new Color(0.42f, 0.43f, 0.45f), 0.5f, 0.8f));
                // drives pulled half out of the front, two on the floor
                var outs = new (float x, float y, float pull)[] { (-0.45f, 1.9f, 0.55f), (0.35f, 3.7f, 0.85f), (-0.2f, 4.6f, 0.35f) };
                foreach (var dv in outs)
                {
                    Box("DrivePulled", o, new Vector3(openRack.position.x + dv.x, dv.y, b.min.z - dv.pull * 0.5f), new Vector3(0.62f, 0.2f, dv.pull + 0.3f), drive);
                    Box("DriveLed", o, new Vector3(openRack.position.x + dv.x + 0.22f, dv.y, b.min.z - dv.pull - 0.16f), new Vector3(0.05f, 0.05f, 0.02f), driveLed);
                }
                GameObject f1 = Box("DriveOnFloor", o, new Vector3(openRack.position.x - 0.3f, 0.11f, b.min.z - 1.4f), new Vector3(0.62f, 0.2f, 1.0f), drive);
                f1.transform.rotation = Quaternion.Euler(0f, 28f, 0f);
                GameObject f2 = Box("DriveOnFloor", o, new Vector3(openRack.position.x + 0.6f, 0.11f, b.min.z - 2.1f), new Vector3(0.62f, 0.2f, 1.0f), drive);
                f2.transform.rotation = Quaternion.Euler(0f, -63f, 6f);
            }
            log.AppendLine("Racks: " + leds.Count + " activity LEDs get their own blink rhythm (15% amber; puzzle status lights untouched), " + racks.Count +
                           " SRV label plates, rack R08 (red/offline) with its front door hanging open, 3 drives pulled out, 2 on the floor");
        }

        // ================================================================== facility details
        static void FacilityDetails(Transform g)
        {
            Transform f = Group("FacilityDetails", g, Vector3.zero);
            const float hallFace = Half - 0.45f - 0.02f;
            // stopped clocks (all at 03:47, the moment of the lockdown)
            Clock(f, new Vector3(-8f, 8.6f, -hallFace), Vector3.back);                  // hall, south wall
            Clock(f, new Vector3(-49.3f, 8.6f, 10.78f), Vector3.forward);             // maintenance, north wall
            Clock(f, new Vector3(-8f, 8.6f, Half + 0.45f + 0.02f), Vector3.back);       // control, south wall

            // fire extinguishers with signs
            Extinguisher(f, new Vector3(8.5f, 0f, -hallFace), Vector3.back);
            Extinguisher(f, new Vector3(-25.2f, 0f, -10.78f), Vector3.back);
            Extinguisher(f, new Vector3(9.5f, 0f, Half + 0.45f + 0.02f), Vector3.back);

            // fire-suppression control panel (hall, south wall)
            Transform p = Group("FireSuppressionPanel", f, new Vector3(-13f, 5.2f, -hallFace));
            p.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            Box("Cabinet", p, new Vector3(0f, 0f, 0.12f), new Vector3(2.5f, 3.1f, 0.25f), Lit("M_L3_PanelRed", new Color(0.5f, 0.06f, 0.05f), 0.4f, 0.3f));
            Sign(p, "Face", "T_L3_FireSuppressionPanel", Vector3.zero, 2.3f, 2.9f, false);

            // safety signs
            Sign(Facing(f, "Sign_NoFood", new Vector3(13.5f, 6.2f, -hallFace), Vector3.back), "Sign", "T_L3_Sign_NoFood", Vector3.zero, 1.5f, 1.9f, true);
            Sign(Facing(f, "Sign_Electrical", new Vector3(-hallFace, 6.2f, -12.5f), Vector3.left), "Sign", "T_L3_Sign_Electrical", Vector3.zero, 1.5f, 1.9f, true);
            Sign(Facing(f, "Sign_Electrical_Sec", new Vector3(59.53f, 6.4f, -8.4f), Vector3.right), "Sign", "T_L3_Sign_Electrical", Vector3.zero, 1.4f, 1.75f, true);
            Sign(Facing(f, "Sign_Authorised_Control", new Vector3(-5.6f, 5.6f, hallFace), Vector3.forward), "Sign", "T_L3_Sign_Authorised", Vector3.zero, 1.5f, 1.9f, true);

            // equipment manual binders and scattered documents on the hall floor
            Material paper = Lit("M_L3_Paper", new Color(0.85f, 0.83f, 0.76f), 0.15f);
            Material binder = Lit("M_L3_Binder_0", new Color(0.1f, 0.2f, 0.5f), 0.35f);
            GameObject bm = Box("ManualBinder", f, new Vector3(15.6f, 0.2f, 6.4f), new Vector3(1.0f, 0.38f, 1.3f), binder);
            bm.transform.rotation = Quaternion.Euler(0f, 23f, 0f);
            Transform lbl = Group("ManualLabel", f, new Vector3(15.6f, 0.4f, 6.4f));
            lbl.rotation = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, 23f, 0f) * Vector3.forward);
            Label(lbl, "Text", "<b>SRV-9000</b>\nEQUIPMENT MANUAL", Vector3.zero, Quaternion.identity, 0.85f, 0.5f, new Color(0.9f, 0.9f, 0.85f), 0.9f);
            var rnd = new System.Random(5);
            foreach (Vector3 at in new[] { new Vector3(14.5f, 0, 5.2f), new Vector3(16.8f, 0, 7.6f), new Vector3(-15.8f, 0, 12.4f), new Vector3(-6.4f, 0, 16.5f), new Vector3(6.8f, 0, -17.2f) })
            {
                GameObject s = MeshObj("Paper", f, QuadXZ(0.62f, 0.85f, 1f, 1f), paper);
                s.transform.position = at + Vector3.up * (0.014f + (float)rnd.NextDouble() * 0.004f);
                s.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            }
            log.AppendLine("Facility: 3 stopped clocks (03:47), 3 fire extinguishers with signs, FM-200 fire-suppression panel (SYSTEM DISABLED), " +
                           "NO FOOD OR DRINK sign, 2 high-voltage signs, AUTHORISED PERSONNEL ONLY sign at the Control door, equipment manual + 5 scattered papers");
        }

        static Transform Facing(Transform parent, string name, Vector3 pos, Vector3 intoWall)
        {
            Transform t = Group(name, parent, pos);
            t.rotation = Quaternion.LookRotation(intoWall, Vector3.up);
            return t;
        }

        // Textured sign whose readable face points out of the wall (local -z).
        static void Sign(Transform parent, string name, string tex, Vector3 localPos, float w, float h, bool backing)
        {
            Material m = Lit("M_L3_" + tex.Replace("T_L3_", ""), Color.white, 0.35f, 0f, Tex(tex), Vector2.one);
            if (backing) Box("Backing", parent, localPos + new Vector3(0f, 0f, 0.02f), new Vector3(w + 0.06f, h + 0.06f, 0.03f), Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f));
            GameObject q = MeshObj(name, parent, QuadXY(w, h, 0.5f, 1f, 1f), m);
            q.transform.localPosition = localPos + new Vector3(0f, 0f, -0.005f);
        }

        static void Clock(Transform parent, Vector3 pos, Vector3 intoWall)
        {
            Transform c = Facing(parent, "StoppedClock", pos, intoWall);
            Material rim = Lit("M_L3_ClockRim", new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.3f);
            Material hand = Lit("M_L3_ClockHand", new Color(0.05f, 0.05f, 0.05f), 0.3f);
            Cylinder("Rim", c, c.TransformPoint(new Vector3(0f, 0f, 0.02f)), c.TransformPoint(new Vector3(0f, 0f, -0.14f)), 0.95f, rim);
            Material face = Lit("M_L3_ClockFace", Color.white, 0.6f, 0f, Tex("T_L3_ClockFace"), Vector2.one, null, true);
            GameObject q = MeshObj("Face", c, QuadXY(1.8f, 1.8f, 0.5f, 1f, 1f), face);
            q.transform.localPosition = new Vector3(0f, 0f, -0.15f);
            // 03:47 -> hour hand 113.5 deg, minute hand 282 deg (clockwise from 12, seen from the front)
            foreach (var (angle, len, wid) in new[] { (113.5f, 0.45f, 0.08f), (282f, 0.68f, 0.05f) })
            {
                Transform pv = new GameObject("HandPivot").transform; pv.SetParent(c, false);
                pv.localPosition = new Vector3(0f, 0f, -0.18f);
                pv.localRotation = Quaternion.Euler(0f, 0f, angle);    // local -z faces the viewer, so +z rotation looks clockwise
                Box("Hand", pv, new Vector3(0f, len * 0.5f, 0f), new Vector3(wid, len, 0.02f), hand);
            }
        }

        static void Extinguisher(Transform parent, Vector3 wallBase, Vector3 intoWall)
        {
            Transform e = Facing(parent, "FireExtinguisher", wallBase, intoWall);
            Material red = Lit("M_L3_ExtinguisherRed", new Color(0.62f, 0.05f, 0.04f), 0.65f, 0.2f);
            Material black = Lit("M_L3_OfficeBlack", new Color(0.05f, 0.05f, 0.055f), 0.4f);
            Box("Bracket", e, new Vector3(0f, 2.6f, 0.0f), new Vector3(0.5f, 0.6f, 0.08f), black);
            Cylinder("Body", e, e.TransformPoint(new Vector3(0f, 1.05f, -0.45f)), e.TransformPoint(new Vector3(0f, 3.1f, -0.45f)), 0.36f, red);
            Cylinder("Valve", e, e.TransformPoint(new Vector3(0f, 3.1f, -0.45f)), e.TransformPoint(new Vector3(0f, 3.45f, -0.45f)), 0.12f, black);
            Box("Lever", e, new Vector3(0f, 3.5f, -0.6f), new Vector3(0.1f, 0.06f, 0.45f), black);
            Cylinder("Hose", e, e.TransformPoint(new Vector3(0.14f, 3.3f, -0.6f)), e.TransformPoint(new Vector3(0.32f, 1.6f, -0.82f)), 0.05f, black);
            Sign(e, "Sign", "T_L3_Sign_Extinguisher", new Vector3(0f, 5.0f, -0.01f), 1.0f, 1.25f, true);
        }

        // ================================================================== taped warning notes (sparingly)
        static void WarningNotes(Transform g)
        {
            Transform n = Group("WarningNotes", g, Vector3.zero);
            Note(n, "Note_DoNotReboot", new Vector3(-13.53f, 4.6f, 8f), Vector3.right, "<s>DO NOT REBOOT</s>\n<size=70%>ok now - R.</size>", 6f);
            Note(n, "Note_DoNotTouch", new Vector3(-13.53f, 4.2f, -3f), Vector3.right, "SYSTEM FAILURE\n<b>DO NOT TOUCH</b>", -4f);
            Note(n, "Note_PowerOff", new Vector3(-59.31f, 4.3f, -9.2f), Vector3.left, "<s>POWER MUST\nREMAIN OFF</s>", 3f);
            Note(n, "Note_Authorised", new Vector3(46f, 6.4f, 0.93f), Vector3.forward, "AUTHORIZED\nPERSONNEL ONLY", -5f);
            log.AppendLine("Warning notes: 4 taped notes (DO NOT REBOOT crossed out, SYSTEM FAILURE - DO NOT TOUCH, POWER MUST REMAIN OFF crossed out, AUTHORIZED PERSONNEL ONLY)");
        }

        static void Note(Transform parent, string name, Vector3 pos, Vector3 intoSurface, string text, float tilt)
        {
            Transform t = Facing(parent, name, pos, intoSurface);
            t.rotation = t.rotation * Quaternion.Euler(0f, 0f, tilt);
            Material paper = Lit("M_L3_NotePaper", new Color(0.92f, 0.9f, 0.82f), 0.1f);
            Material tape = Lit("M_L3_Tape", new Color(0.85f, 0.82f, 0.7f, 0.6f), 0.5f, 0f, null, Vector2.one, null, false, true);
            Box("Paper", t, new Vector3(0f, 0f, 0f), new Vector3(1.15f, 0.85f, 0.01f), paper);
            Box("Tape", t, new Vector3(0f, 0.42f, -0.008f), new Vector3(0.5f, 0.14f, 0.004f), tape);
            Label(t, "Text", text, new Vector3(0f, -0.03f, -0.012f), Quaternion.identity, 1.0f, 0.7f, new Color(0.08f, 0.08f, 0.1f), 1.4f);
        }

        // ================================================================== play-mode self test (Phase 4)
        class PlayTest4
        {
            readonly string dir; readonly System.Text.StringBuilder r = new System.Text.StringBuilder();
            int frame; int minLit = 999, maxLit = -1; Color before; bool toggled; float waited;
            public PlayTest4(string d) { dir = d; }
            public bool Step()
            {
                var g = CodeBreaker.Level2.L2Game.Instance;
                if (g == null) { r.AppendLine("FAIL: L2Game not found"); return Done(); }
                frame++;
                var blink = Object.FindFirstObjectByType<L3RackLedBlink>();
                if (blink == null) { r.AppendLine("FAIL: LED controller missing"); return Done(); }
                { int l = blink.LitCount(); minLit = Mathf.Min(minLit, l); maxLit = Mathf.Max(maxLit, l); }
                L3DoorFrameLight frame0 = null;
                foreach (var f in Object.FindObjectsByType<L3DoorFrameLight>()) if (f.insertedFlag == "card_red_in") frame0 = f;
                if (frame0 == null || frame0.glow == null) { r.AppendLine("FAIL: Maintenance door frame missing"); return Done(); }
                if (frame == 30) { before = frame0.glow.color; }
                if (frame == 31 && !g.Flag("card_red_in")) { g.SetFlag("card_red_in"); toggled = true; }
                if (frame > 31 && toggled)
                {
                    waited += Time.unscaledDeltaTime;
                    if (waited > 3.5f)
                    {
                        Color after = frame0.glow.color;
                        g.SetFlag("card_red_in", false); toggled = false;
                        r.AppendLine("Maintenance door frame before insertion: " + before + "  after (flag set in memory, 3.5 s fade): " + after +
                                     (after.b > 0.85f && before.b < 0.3f ? "  (PASS: red -> white)" : "  (FAIL)"));
                        r.AppendLine("card_red_in restored to " + g.Flag("card_red_in"));
                        r.AppendLine("Rack activity LEDs lit at the same time: between " + minLit + " and " + maxLit + " of " + blink.leds.Length +
                                     (maxLit - minLit > 5 ? " (PASS: independent rhythms)" : " (FAIL)"));
                        return Done();
                    }
                }
                return false;
            }
            bool Done()
            {
                r.AppendLine("Lights in scene: " + Object.FindObjectsByType<Light>().Length);
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 4 play test:\n" + r);
                return true;
            }
        }
        static PlayTest4 test4;
        static void Test4Tick()
        {
            if (test4 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test4Tick; test4 = null; return; }
            if (test4.Step()) { EditorApplication.update -= Test4Tick; test4 = null; }
        }
        static void StartPlayTest4(string dir) { if (test4 != null) return; Directory.CreateDirectory(dir); test4 = new PlayTest4(dir); EditorApplication.update += Test4Tick; }
    }
}
