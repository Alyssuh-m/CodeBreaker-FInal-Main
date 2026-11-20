using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 7: Ramos's story. Optional readable objects built on the existing L2Examine system (E to read,
    // shown in the existing document panel, listed in the Notes journal once read). None of them is a puzzle,
    // none sets a puzzle flag, none blocks a path. Everything lives under "Level3/L3_Environment/Phase7_Story".
    public static partial class Level3EnvironmentPass
    {
        const string Group7 = "Phase7_Story";

        const string BadgeBody = "Ramos's ID badge, lying face up on the floor. Maintenance technician, night shift, access to every wing.\nThe clip is snapped, as if it was torn off.";

        const string MaintReportBody =
            "Technician on site: R. RAMOS (night shift)\n" +
            "Responsible for: generator, cooling loops, fuse panels, server hall checks every 2 hours.\n\n" +
            "Week 41 - Cooling loop B leaking again. Patched it. Replacement part (work order #4471) still not delivered.\n" +
            "Week 42 - Generator failed its self-test. Restarted it by hand.\n" +
            "Week 43 - Racks running hot. Told management the shutdown plan is not safe while cooling is down. No reply.\n" +
            "Week 44 - Shutdown confirmed for Friday night. I'm told to stay until the last rack is off.";

        const string IncidentBody =
            "INCIDENT #0091 - FACILITY SHUTDOWN\n\n" +
            "During the planned shutdown the cooling system failed and the core started an automatic LOCKDOWN. Every door sealed.\n\n" +
            "Staff evacuated: 11 of 12.\n" +
            "Unaccounted for: R. Ramos (maintenance). Last seen in the server hall, 23:52.\n" +
            "Door records show the exit was never opened from inside.\n\n" +
            "The hall cameras kept recording for 9 days. Flagged footage: \"a figure standing in the aisles. It does not move between frames.\"\n\n" +
            "Decision: site sealed. Search not authorised.";

        const string NotebookBody =
            "Day 1 - The doors won't open. Radio is dead. Someone will come.\n" +
            "Day 3 - Sleeping at the guard post. The cameras still work, so I watch them.\n" +
            "Day 5 - Someone on CAM 4, standing in an aisle. When I go out there, nobody.\n" +
            "Day 6 - It stands like me. Same height. Same jacket.\n" +
            "Day 8 - I stopped going out to check. Every night it is a little closer on the screen.\n" +
            "Day 9 - I left the keycards behind my three jobs, one in each wing. Whoever comes next can earn them back and put them in the terminal. I can't do it anymore.";

        const string MugBody = "A chipped mug: #1 NIGHT SHIFT.\nThe coffee inside dried into a black ring a long time ago.";

        const string PhotoBody = "A faded photo propped up at the back of Ramos's workbench. A man and a child at the beach.\nWritten underneath: \"come home safe - L.\"";

        const string LogSyncingBody =
            "Log sync: waiting for the core systems (keycards {n}/3).\n\n" +
            "Last cached entry - R. Ramos, Friday 22:10:\n\"Starting the shutdown checklist. Home by morning.\"";

        const string LogFinalBody =
            "ENTRY 41  -  R. RAMOS  -  timestamp corrupted\n\n" +
            "They locked me in too...\n\n" +
            "The shutdown sealed every door, and nobody came back for me. I stopped counting the days.\n" +
            "If you are reading this, you did what I couldn't. The terminal will ask for the exit code. Get out.\n\n" +
            "And if you see me on the cameras, don't stop. Don't look back. I'm not me anymore.";

        [MenuItem("Code Breaker/Level 3/Phase 7 - Build Story")]
        public static void Build7()
        {
            if (!OpenLevel3()) return;
            log.Length = 0; mats.Clear();
            EnsureFolders();
            ImportTextures();
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(Group7);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform g = new GameObject(Group7).transform; g.SetParent(env, false);

            Material paper = Lit("M_L3_Paper", new Color(0.85f, 0.83f, 0.76f), 0.15f);
            Material board = Lit("M_L3_ClipboardBoard", new Color(0.42f, 0.3f, 0.18f), 0.3f);
            Material clipM = Lit("M_L3_ClipMetal", new Color(0.6f, 0.62f, 0.64f), 0.6f, 0.9f);
            Material dark = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
            Color ink = new Color(0.1f, 0.1f, 0.13f);
            int placed = 0;

            // ---- A. ID badge on the hall floor, beside the cracked glass tile
            {
                Transform b = Group("Story_IDBadge", g, new Vector3(6.75f, 0.03f, -4.55f));
                b.rotation = Quaternion.Euler(0f, 24f, 0f);
                Material card = Lit("M_L3_RamosBadge", Color.white, 0.35f, 0f, Tex("T_L3_RamosBadge"), Vector2.one);
                Box("CardBack", b, new Vector3(0f, 0.008f, 0f), new Vector3(0.6f, 0.016f, 0.38f), Lit("M_L3_BadgePlastic", new Color(0.85f, 0.85f, 0.82f), 0.5f));
                GameObject face = MeshObj("CardFace", b, QuadXZ(0.58f, 0.36f, 1f, 1f), card);
                face.transform.localPosition = new Vector3(0f, 0.018f, 0f);
                Material strap = Lit("M_L3_LanyardRed", new Color(0.55f, 0.08f, 0.07f), 0.25f);
                GameObject s1 = Box("Lanyard_A", b, new Vector3(-0.05f, 0.006f, 0.62f), new Vector3(0.09f, 0.01f, 0.9f), strap); s1.transform.localRotation = Quaternion.Euler(0f, 14f, 0f);
                GameObject s2 = Box("Lanyard_B", b, new Vector3(0.32f, 0.006f, 1.02f), new Vector3(0.09f, 0.01f, 0.7f), strap); s2.transform.localRotation = Quaternion.Euler(0f, 62f, 0f);
                Box("BrokenClip", b, new Vector3(0f, 0.015f, 0.24f), new Vector3(0.1f, 0.025f, 0.12f), clipM);
                Examine7(b.gameObject, "ID Badge", "RAMOS'S ID BADGE", BadgeBody, Tex("T_L3_RamosBadge"), "Examine the ID Badge", new Vector3(0f, 0.3f, 0.3f), new Vector3(1.6f, 0.8f, 1.8f));
                View(b, new Vector3(6.2f, 4.6f, -8.2f));
                placed++;
            }

            // ---- B. Maintenance report: clipboard on the Maintenance south wall, near the entrance
            {
                Transform c = Facing(g, "Story_MaintenanceReport", new Vector3(-29.5f, 4.9f, -10.75f), Vector3.back);
                Clipboard(c, board, paper, clipM, "MAINTENANCE REPORT\n<size=70%>Night shift: R. RAMOS\nGenerator / Cooling / Fuses\nWk 44: shutdown Friday -\nstay until last rack is off</size>", ink);
                Examine7(c.gameObject, "Maintenance Report", "MAINTENANCE REPORT", MaintReportBody, null, "Read the Maintenance Report", new Vector3(0f, 0f, -0.2f), new Vector3(1.6f, 2.1f, 0.6f));
                View(c, new Vector3(-31.5f, 5.8f, -4.5f));
                placed++;
            }

            // ---- C. Incident report: pinned inside the Control room, beside the doorway
            {
                Transform c = Facing(g, "Story_IncidentReport", new Vector3(-8.2f, 5.2f, 23.02f), Vector3.back);
                Clipboard(c, board, paper, clipM, "<color=#A02020>INCIDENT REPORT</color>\n<size=70%>#0091  FACILITY SHUTDOWN\nLOCKDOWN - doors sealed\nUnaccounted for: R. RAMOS\nSite sealed</size>", ink);
                Examine7(c.gameObject, "Incident Report", "INCIDENT REPORT", IncidentBody, null, "Read the Incident Report", new Vector3(0f, 0f, -0.2f), new Vector3(1.6f, 2.1f, 0.6f));
                View(c, new Vector3(-6.5f, 5.8f, 29f));
                placed++;
            }

            // ---- E. personal objects: notebook on the Security office desk, mug + photo on Ramos's workbench
            Bounds officeDesk;
            if (FindBounds(level, "OfficeCorner", "DeskTop", out officeDesk))
            {
                Vector3 p = new Vector3(officeDesk.max.x - 0.45f, officeDesk.max.y, officeDesk.max.z - 0.5f);   // front-right corner, clear of the monitors
                Transform nb = Group("Story_Notebook", g, p);
                nb.rotation = Quaternion.Euler(0f, -14f, 0f);
                Box("Cover", nb, new Vector3(0f, 0.035f, 0f), new Vector3(0.55f, 0.07f, 0.75f), Lit("M_L3_NotebookCover", new Color(0.22f, 0.13f, 0.08f), 0.3f));
                Box("Pages", nb, new Vector3(0.015f, 0.035f, 0f), new Vector3(0.53f, 0.05f, 0.73f), paper);
                Box("Band", nb, new Vector3(0.18f, 0.036f, 0f), new Vector3(0.03f, 0.075f, 0.76f), Lit("M_L3_Band", new Color(0.05f, 0.05f, 0.05f), 0.3f));
                Cylinder("Pen", nb, nb.TransformPoint(new Vector3(-0.42f, 0.03f, -0.3f)), nb.TransformPoint(new Vector3(-0.42f, 0.03f, 0.25f)), 0.025f, Lit("M_L3_PenBlue", new Color(0.1f, 0.15f, 0.45f), 0.5f));
                Examine7(nb.gameObject, "Notebook", "RAMOS'S NOTEBOOK", NotebookBody, null, "Read Ramos's Notebook", new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.6f, 1.2f));
                View(nb, p + new Vector3(-0.8f, 2.8f, 3.2f));
                placed++;
            }
            else log.AppendLine("WARNING: Security office desk not found (Phase 3 not built?) - notebook skipped");

            Bounds bench;
            if (FindBounds(level, "Workbench", "BenchTop", out bench))
            {
                // pick the bench end farthest from the laptop and its sticky note (both are puzzle props)
                Vector3 lap = FindPos(level, "Laptop"), sticky = FindPos(level, "StickyNote_Laptop");
                float xa = bench.min.x + 0.8f, xb = bench.max.x - 0.8f;
                float da = Mathf.Min(Mathf.Abs(xa - lap.x), Mathf.Abs(xa - sticky.x)), db = Mathf.Min(Mathf.Abs(xb - lap.x), Mathf.Abs(xb - sticky.x));
                float endX = da >= db ? xa : xb, inward = da >= db ? 1f : -1f;
                float wallZ = bench.center.z + 0.65f * Mathf.Sign(11.25f - bench.center.z);      // toward the north wall
                float top = bench.max.y;

                Transform mug = Group("Story_Mug", g, new Vector3(endX, top, bench.center.z - 0.2f));
                Material ceramic = Lit("M_L3_MugCeramic", new Color(0.86f, 0.85f, 0.8f), 0.55f);
                Cylinder("Body", mug, mug.position, mug.position + Vector3.up * 0.36f, 0.15f, ceramic);
                Cylinder("Coffee", mug, mug.position + Vector3.up * 0.30f, mug.position + Vector3.up * 0.305f, 0.135f, Lit("M_L3_DriedCoffee", new Color(0.08f, 0.05f, 0.03f), 0.2f));
                Box("Handle", mug, new Vector3(0.17f, 0.18f, 0f), new Vector3(0.06f, 0.2f, 0.04f), ceramic);
                Box("Label", mug, new Vector3(0f, 0.2f, -0.148f), new Vector3(0.14f, 0.1f, 0.01f), Lit("M_L3_MugPrint", new Color(0.6f, 0.1f, 0.08f), 0.4f));
                Examine7(mug.gameObject, "Coffee Mug", "COFFEE MUG", MugBody, null, "Examine the Mug", new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.6f, 0.7f));

                Transform ph = Group("Story_Photo", g, new Vector3(endX + inward * 0.75f, top, wallZ));
                ph.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up) * Quaternion.Euler(10f, 0f, 0f);
                Box("Frame", ph, new Vector3(0f, 0.32f, 0.02f), new Vector3(0.56f, 0.66f, 0.04f), Lit("M_L3_PhotoFrame", new Color(0.16f, 0.11f, 0.07f), 0.35f));
                GameObject pic = MeshObj("Picture", ph, QuadXY(0.46f, 0.56f, 0f, 1f, 1f), Lit("M_L3_RamosPhoto", Color.white, 0.2f, 0f, Tex("T_L3_RamosPhoto"), Vector2.one));
                pic.transform.localPosition = new Vector3(0f, 0.04f, -0.005f);
                Examine7(ph.gameObject, "Photo", "OLD PHOTOGRAPH", PhotoBody, Tex("T_L3_RamosPhoto"), "Look at the Photo", new Vector3(0f, 0.3f, -0.1f), new Vector3(0.8f, 0.8f, 0.5f));
                View(mug, new Vector3(endX + inward * 0.5f, top + 2.2f, bench.center.z - 3.4f));
                placed += 2;
            }
            else log.AppendLine("WARNING: Maintenance workbench not found - mug and photo skipped");

            // ---- D. maintenance log station beside the terminal (mirror of the CCTV relay): final entry after 3 keycards
            {
                Transform st = Group("Story_LogStation", g, new Vector3(5.7f, 0f, -11.3f));
                Cylinder("Pole", st, new Vector3(5.7f, 0f, -11.0f), new Vector3(5.7f, 3.3f, -11.0f), 0.09f, dark);
                Box("Base", st, new Vector3(0f, 0.05f, 0.3f), new Vector3(1.2f, 0.1f, 1.2f), dark);
                Box("Shelf", st, new Vector3(0f, 3.3f, 0.1f), new Vector3(1.6f, 0.08f, 1.0f), dark);
                Box("Keyboard", st, new Vector3(0f, 3.38f, -0.15f), new Vector3(1.2f, 0.06f, 0.4f), Lit("M_L3_KeyboardBlack", new Color(0.05f, 0.05f, 0.06f), 0.4f));
                Box("Monitor", st, new Vector3(0f, 4.2f, 0.35f), new Vector3(1.5f, 1.15f, 0.9f), Lit("M_L3_OfficeBeige", new Color(0.55f, 0.53f, 0.47f), 0.3f));
                Material scrM = Lit("M_L3_LogScreen", new Color(0.01f, 0.015f, 0.02f), 0.5f, 0f, null, Vector2.one, new Color(0.02f, 0.03f, 0.04f));
                GameObject scr = MeshObj("Screen", st, QuadXY(1.2f, 0.88f, 0.5f, 1f, 1f), scrM);
                scr.transform.localPosition = new Vector3(0f, 4.22f, -0.11f);
                TextMeshPro t = Label(st, "ScreenText", "MAINT. LOG", new Vector3(0f, 4.22f, -0.12f), Quaternion.identity, 1.1f, 0.8f, new Color(0.6f, 0.85f, 1f), 1.6f);
                TextPlate(st, "Plate", "MAINTENANCE LOG", new Vector3(0f, 5.0f, -0.11f), 1.4f, 0.22f, new Color(0.75f, 0.8f, 0.8f), 1.2f, true);
                Light lt = PointLight(st, "ScreenLight", new Vector3(5.7f, 4.3f, -12.4f), new Color(0.45f, 0.75f, 1f), 1.2f, 3.5f);
                var ex = Examine7(st.gameObject, "Maintenance Log", "MAINTENANCE LOG STATION", LogSyncingBody.Replace("{n}", "0"), null, "Read the Maintenance Log", new Vector3(0f, 4.0f, 0.1f), new Vector3(1.8f, 2.2f, 1.4f));
                var sl = st.gameObject.AddComponent<L3StoryLog>();
                sl.log = ex; sl.screen = t; sl.screenGlow = scr.GetComponent<Renderer>(); sl.screenLight = lt;
                sl.syncingBody = LogSyncingBody; sl.finalBody = LogFinalBody;
                View(st, new Vector3(9.5f, 5.6f, -17f));
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            AssetDatabase.SaveAssets();
            log.AppendLine("Story objects placed: " + placed + " (badge, maintenance report, incident report, notebook, mug, photo, log station)");
            log.AppendLine("All use the existing L2Examine document panel; no puzzle flags, no colliders that block movement (trigger colliders only)");
            log.AppendLine("Log station: syncing text until card_red_in + card_blue_in + card_green_in, then the final entry 'They locked me in too...'");
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase7");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), log.ToString());
            Debug.Log("[Level 3 Env] Phase 7 built and saved.\n" + log);
        }

        [MenuItem("Code Breaker/Level 3/Phase 7 - Undo (remove Story pass)")]
        public static void UndoPhase7()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform old = level != null ? level.Find("L3_Environment/" + Group7) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 7 removed.");
        }

        [MenuItem("Code Breaker/Level 3/Phase 7 - Capture")]
        public static void Capture7()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform g = level != null ? level.Find("L3_Environment/" + Group7) : null;
            if (g == null) { Debug.LogError("[Level 3 Env] Phase 7 not built."); return; }
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase7");
            Directory.CreateDirectory(dir);
            int i = 1;
            foreach (Transform t in g)
            {
                Transform v = t.Find("ViewFrom");
                if (v == null) continue;
                Vector3 target = t.position + Vector3.up * (t.name == "Story_IDBadge" ? 0f : t.name == "Story_LogStation" ? 4.1f : 0.3f);
                float fov = t.name == "Story_IDBadge" || t.name == "Story_Mug" || t.name == "Story_Notebook" ? 34f : 44f;
                Shot(dir, "S" + i++ + "_" + t.name.Replace("Story_", ""), v.position, target, fov);
            }
            Debug.Log("[Level 3 Env] Phase 7 captures written to " + dir);
        }

        // ------------------------------------------------------------------ helpers
        static L2Examine Examine7(GameObject go, string display, string title, string body, Texture image, string verb, Vector3 trigCenter, Vector3 trigSize)
        {
            var ex = go.AddComponent<L2Examine>();
            ex.displayName = display; ex.title = title; ex.body = body; ex.image = image; ex.verb = verb;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true; bc.center = trigCenter; bc.size = trigSize;
            return ex;
        }

        static void View(Transform t, Vector3 worldPos)
        {
            var v = new GameObject("ViewFrom").transform;
            v.SetParent(t, false); v.position = worldPos;
        }

        static void Clipboard(Transform c, Material board, Material paper, Material clip, string text, Color ink)
        {
            Box("Board", c, new Vector3(0f, 0f, 0f), new Vector3(1.3f, 1.8f, 0.05f), board);
            Box("Paper", c, new Vector3(0f, -0.08f, -0.03f), new Vector3(1.16f, 1.5f, 0.01f), paper);
            Box("Clip", c, new Vector3(0f, 0.78f, -0.05f), new Vector3(0.6f, 0.18f, 0.06f), clip);
            Box("Nail", c, new Vector3(0f, 0.97f, 0.0f), new Vector3(0.05f, 0.05f, 0.12f), clip);
            Label(c, "Text", text, new Vector3(0f, -0.1f, -0.04f), Quaternion.identity, 1.04f, 1.3f, ink, 1.4f);
        }

        // Renderer bounds of a child named childName under any object named parentName.
        static bool FindBounds(Transform level, string parentName, string childName, out Bounds b)
        {
            b = default;
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != parentName) continue;
                Transform c = t.Find(childName);
                Renderer r = c != null ? c.GetComponent<Renderer>() : null;
                if (r != null) { b = r.bounds; return true; }
            }
            return false;
        }

        static Vector3 FindPos(Transform level, string name)
        {
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.position;
            return new Vector3(9999f, 0f, 9999f);
        }

        // ================================================================== play-mode self test (Phase 7)
        class PlayTest7
        {
            readonly string dir; readonly System.Text.StringBuilder r = new System.Text.StringBuilder();
            readonly List<L2Examine> items = new List<L2Examine>();
            int idx = -1, phase; float timer; double last = -1; L3StoryLog station;
            public PlayTest7(string d) { dir = d; }
            void Cap(string n) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png"));
            public bool Step()
            {
                var g = L2Game.Instance;
                if (g == null || g.ui == null) { r.AppendLine("FAIL: L2Game/UI not found"); return Done(); }
                double now = EditorApplication.timeSinceStartup; if (last >= 0) timer += (float)(now - last); last = now;
                switch (phase)
                {
                    case 0:
                        foreach (L2Examine ex in Object.FindObjectsByType<L2Examine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (ex.name.StartsWith("Story_")) items.Add(ex);
                        items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                        station = Object.FindFirstObjectByType<L3StoryLog>();
                        r.AppendLine("Story objects found: " + items.Count + " (" + string.Join(", ", items.ConvertAll(e => e.name.Replace("Story_", ""))) + ")");
                        foreach (var ex in items)
                        {
                            var bc = ex.GetComponent<BoxCollider>();
                            r.AppendLine("  " + ex.name + ": prompt '" + ex.Prompt + "', trigger collider " + (bc != null && bc.isTrigger) + ", interactable " + ex.CanInteract);
                        }
                        r.AppendLine("Keycards in at start: " + Cards(g) + "; station title now: '" + (station != null ? station.log.title : "missing") + "'");
                        phase = 1; timer = 0f; return false;
                    case 1:                                   // open each document in turn and capture the panel
                        if (timer < 0.6f) return false;
                        if (idx >= 0) g.ui.CloseModal();
                        idx++;
                        if (idx >= items.Count) { phase = 3; timer = 0f; return false; }
                        items[idx].Interact();
                        phase = 2; timer = 0f; return false;
                    case 2:
                        if (timer < 0.7f) return false;
                        Cap("DOC_" + items[idx].name.Replace("Story_", "") + (items[idx].name == "Story_LogStation" ? "_before_cards" : ""));
                        r.AppendLine("Opened " + items[idx].name + ": modal open " + g.ui.AnyModalOpen + ", read flag " + g.Flag(items[idx].ReadFlag));
                        phase = 1; timer = 0f; return false;
                    case 3:
                        foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) g.SetFlag(c);
                        phase = 4; timer = 0f; return false;
                    case 4:
                        if (timer < 1.0f) return false;
                        r.AppendLine("After all 3 keycards: station title '" + station.log.title + "', final text contains 'They locked me in too' " + station.log.body.Contains("They locked me in too") +
                                     (station.log.body.Contains("They locked me in too") ? " (PASS)" : " (FAIL)"));
                        station.log.Interact();
                        phase = 5; timer = 0f; return false;
                    case 5:
                        if (timer < 0.7f) return false;
                        Cap("DOC_LogStation_after_cards");
                        phase = 51; timer = 0f; return false;
                    case 51:
                        if (timer < 0.5f) return false;
                        g.ui.CloseModal();
                        foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) g.SetFlag(c, false);
                        phase = 6; timer = 0f; return false;
                    case 6:
                        if (timer < 0.5f) return false;
                        r.AppendLine("Cards removed again: station back to '" + station.log.title + "'");
                        foreach (var ex in items) g.SetFlag(ex.ReadFlag, false);
                        r.AppendLine("Read flags and card flags cleared again (memory only; nothing saved during the test).");
                        return Done();
                }
                return Done();
            }
            static int Cards(L2Game g) { int n = 0; foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) if (g.Flag(c)) n++; return n; }
            bool Done()
            {
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 7 play test:\n" + r);
                return true;
            }
        }
        static PlayTest7 test7;
        static void Test7Tick()
        {
            if (test7 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test7Tick; test7 = null; return; }
            if (test7.Step()) { EditorApplication.update -= Test7Tick; test7 = null; }
        }
        static void StartPlayTest7(string dir) { if (test7 != null) return; Directory.CreateDirectory(dir); test7 = new PlayTest7(dir); EditorApplication.update += Test7Tick; }
    }
}
