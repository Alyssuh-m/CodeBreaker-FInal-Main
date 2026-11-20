using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 6: progress-based horror events. Builds the 3D dark figure, red emergency beacons, a quiet siren loop,
    // a CCTV feed camera and a CCTV relay monitor beside the central terminal, and wires them to L3ProgressEvents.
    // Everything lives under "Level3/L3_Environment/Phase6_Events". Existing objects are only referenced
    // (hall lights / glow panels, camera-wall screens) and changed at runtime only, never in the saved scene.
    public static partial class Level3EnvironmentPass
    {
        const string Group6 = "Phase6_Events";

        [MenuItem("Code Breaker/Level 3/Phase 6 - Build Events")]
        public static void Build6()
        {
            if (!OpenLevel3()) return;
            log.Length = 0; mats.Clear();
            EnsureFolders();
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(Group6);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform g = new GameObject(Group6).transform; g.SetParent(env, false);
            var ev = g.gameObject.AddComponent<L3ProgressEvents>();

            // ---- hall lights + glowing panels (existing builder lights and the Phase 2 aisle lights)
            var lights = new List<Light>(); var glow = new List<Renderer>();
            Transform hallLights = level.Find("ServerRoom/Lights");
            if (hallLights != null)
                foreach (Transform t in hallLights)
                {
                    Light l = t.GetComponent<Light>(); if (l != null) lights.Add(l);
                    if (t.name == "BlueWallLight" || t.name == "CeilingPanel") { Renderer r = t.GetComponent<Renderer>(); if (r != null) glow.Add(r); }
                }
            Transform aisles = level.Find("L3_Environment/Phase2_MainHall/AisleLighting");
            if (aisles != null) foreach (Light l in aisles.GetComponentsInChildren<Light>(true)) lights.Add(l);
            ev.hallLights = lights.ToArray(); ev.hallGlow = glow.ToArray();

            // ---- 3D dark figure (faceless, tall, matte black; no collider, never blocks the player)
            GameObject fig = DarkFigure(g);
            ev.figure = fig;

            // ---- event 1 spots: doorways and far hall corners
            Transform spots = Group("FigureSpots_Hall", g, Vector3.zero);
            var hs = new List<Transform>();
            foreach (var (n, p, f) in new[] {
                ("MaintenanceDoor", new Vector3(-20.4f, 0f, 0f), Vector3.right), ("SecurityDoor", new Vector3(20.4f, 0f, 0f), Vector3.left),
                ("ControlDoor", new Vector3(0f, 0f, 20.4f), Vector3.back), ("CornerNW", new Vector3(-19.5f, 0f, 19.5f), new Vector3(1f, 0f, -1f)),
                ("CornerNE", new Vector3(19.5f, 0f, 19.5f), new Vector3(-1f, 0f, -1f)), ("CornerSW", new Vector3(-19.5f, 0f, -19.5f), new Vector3(1f, 0f, 1f)),
                ("CornerSE", new Vector3(19.5f, 0f, -19.5f), new Vector3(-1f, 0f, 1f)), ("SouthWallWest", new Vector3(-5f, 0f, -20.6f), new Vector3(0.3f, 0f, 1f)), ("SouthWallEast", new Vector3(5f, 0f, -20.6f), new Vector3(-0.3f, 0f, 1f)) })
            { Transform t = Group(n, spots, p); t.rotation = Quaternion.LookRotation(f, Vector3.up); hs.Add(t); }
            ev.figureSpots = hs.ToArray();

            // ---- event 2: emergency beacons + red fill + siren loop
            Transform em = Group("EmergencyLighting", g, Vector3.zero);
            Material dome = Lit("M_L3_BeaconDome", new Color(0.2f, 0.02f, 0.02f), 0.6f, 0f, null, Vector2.one, new Color(2.6f, 0.15f, 0.08f));
            Material dark = Lit("M_L3_DarkMetal", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.7f);
            var spinners = new List<Transform>();
            foreach (var (p, into) in new[] { (new Vector3(-21.85f, 9.2f, -12f), Vector3.left), (new Vector3(21.85f, 9.2f, 12f), Vector3.right),
                                               (new Vector3(-8.5f, 8.2f, 21.85f), Vector3.forward), (new Vector3(8.5f, 8.2f, 21.85f), Vector3.forward),
                                               (new Vector3(-12f, 9.2f, -21.85f), Vector3.back) })
            {
                Transform b = Group("Beacon", em, p);
                b.rotation = Quaternion.LookRotation(into, Vector3.up);
                Box("Bracket", b, new Vector3(0f, 0f, 0.1f), new Vector3(0.5f, 0.3f, 0.5f), dark);
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Sphere); d.name = "Dome";
                Object.DestroyImmediate(d.GetComponent<Collider>());
                d.transform.SetParent(b, false); d.transform.localPosition = new Vector3(0f, -0.25f, -0.25f); d.transform.localScale = new Vector3(0.45f, 0.5f, 0.45f);
                d.GetComponent<Renderer>().sharedMaterial = dome;
                Transform sp = new GameObject("Spinner").transform; sp.SetParent(b, false); sp.localPosition = new Vector3(0f, -0.25f, -0.25f);
                var lt = new GameObject("BeaconSpot").AddComponent<Light>();
                lt.transform.SetParent(sp, false); lt.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
                lt.type = LightType.Spot; lt.color = new Color(1f, 0.08f, 0.04f); lt.intensity = 45f; lt.range = 30f; lt.spotAngle = 55f; lt.shadows = LightShadows.None;
                spinners.Add(sp);
            }
            PointLight(em, "RedFill_South", new Vector3(0f, 9.5f, -9f), new Color(1f, 0.08f, 0.05f), 34f, 32f);
            PointLight(em, "RedFill_North", new Vector3(0f, 9.5f, 10f), new Color(1f, 0.08f, 0.05f), 34f, 32f);
            ev.emergencyRoot = em.gameObject; ev.beaconSpinners = spinners.ToArray();
            var sirenGo = new GameObject("SirenLoop"); sirenGo.transform.SetParent(g, false); sirenGo.transform.position = new Vector3(0f, 9f, 0f);
            AudioSource siren = sirenGo.AddComponent<AudioSource>();
            siren.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/L3_Siren.wav");
            siren.loop = true; siren.playOnAwake = false; siren.spatialBlend = 0.35f; siren.rolloffMode = AudioRolloffMode.Linear; siren.minDistance = 20f; siren.maxDistance = 80f; siren.pitch = 0.85f;
            ev.siren = siren; ev.sirenVolume = 0.12f;

            // ---- event 3: CCTV feed camera rendering into a render texture, camera-wall screens, relay monitor
            string rtPath = TexDir + "/RT_L3_CctvFeed.renderTexture";
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
            if (rt == null) { rt = new RenderTexture(512, 384, 24) { name = "RT_L3_CctvFeed" }; AssetDatabase.CreateAsset(rt, rtPath); }
            Material feed = Asset("M_L3_CctvFeed", Shader.Find("Universal Render Pipeline/Unlit"));
            feed.SetTexture("_BaseMap", rt); feed.SetColor("_BaseColor", new Color(0.78f, 0.95f, 0.85f)); EditorUtility.SetDirty(feed);
            Material stat = Asset("M_L3_StaticScreenUnlit", Shader.Find("Universal Render Pipeline/Unlit"));
            stat.SetTexture("_BaseMap", Tex("T_L3_StaticNoise")); stat.SetTextureScale("_BaseMap", new Vector2(1.4f, 1.1f));
            stat.SetColor("_BaseColor", new Color(0.62f, 0.78f, 1f)); EditorUtility.SetDirty(stat);
            var camGo = new GameObject("CctvFeedCamera"); camGo.transform.SetParent(g, false);
            Camera fc = camGo.AddComponent<Camera>();
            fc.targetTexture = rt; fc.fieldOfView = 40f; fc.nearClipPlane = 0.1f; fc.farClipPlane = 80f;
            fc.clearFlags = CameraClearFlags.SolidColor; fc.backgroundColor = Color.black; fc.depth = -20; fc.enabled = false;
            ev.feedCam = fc; ev.feedMat = feed; ev.staticMat = stat;

            var screens = new List<Renderer>(); var overlays = new List<TMP_Text>();
            Transform wall = null;
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true)) if (t.name == "CameraWall") { wall = t; break; }
            for (int i = 1; i <= 6 && wall != null; i++)
            {
                Transform h = wall.Find("Feed_CAM" + i);
                screens.Add(h != null && h.Find("Screen") != null ? h.Find("Screen").GetComponent<Renderer>() : null);
                overlays.Add(h != null && h.Find("Overlay") != null ? h.Find("Overlay").GetComponent<TMP_Text>() : null);
            }
            ev.camScreens = screens.ToArray(); ev.camOverlays = overlays.ToArray(); ev.figureCam = 3;

            Transform relay = Group("CctvRelayMonitor", g, new Vector3(-5.7f, 0f, -11.3f));
            Cylinder("Pole", relay, new Vector3(-5.7f, 0f, -11.0f), new Vector3(-5.7f, 3.6f, -11.0f), 0.09f, dark);
            Box("Base", relay, new Vector3(0f, 0.05f, 0.3f), new Vector3(1.2f, 0.1f, 1.2f), dark);
            Box("CRT", relay, new Vector3(0f, 4.4f, 0.3f), new Vector3(1.9f, 1.5f, 1.3f), Lit("M_L3_OfficeBeige", new Color(0.55f, 0.53f, 0.47f), 0.3f));
            Material standby = Lit("M_L3_RelayStandby", new Color(0.01f, 0.015f, 0.025f), 0.5f, 0f, null, Vector2.one, new Color(0.012f, 0.02f, 0.035f));
            GameObject scr = MeshObj("Screen", relay, QuadXY(1.55f, 1.15f, 0.5f, 1f, 1f), standby);
            scr.transform.localPosition = new Vector3(0f, 4.45f, -0.36f);
            TextMeshPro lab = Label(relay, "RelayLabel", "CCTV RELAY  ·  STANDBY", new Vector3(0f, 5.4f, -0.36f), Quaternion.identity, 2.6f, 0.3f, new Color(0.7f, 0.85f, 0.9f), 1f);
            ev.relayScreen = scr.GetComponent<Renderer>(); ev.relayLabel = lab;

            Transform wings = Group("WingFeedSpots", g, Vector3.zero);
            ev.wingFigureSpots = new[] { Group("Fig_Maintenance", wings, new Vector3(-30f, 0f, -3f)), Group("Fig_Security", wings, new Vector3(30f, 0f, -4f)), Group("Fig_Control", wings, new Vector3(5f, 0f, 30f)) };
            ev.wingCamSpots = new[] { Group("Cam_Maintenance", wings, new Vector3(-24.2f, 9.6f, 9.8f)), Group("Cam_Security", wings, new Vector3(23.6f, 9.6f, -10.2f)), Group("Cam_Control", wings, new Vector3(-15.6f, 9.6f, 23.6f)) };

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            AssetDatabase.SaveAssets();
            log.AppendLine("Events wired to card_red_in / card_blue_in / card_green_in (once each; l3evt_1..3 flags saved with checkpoints)");
            log.AppendLine("Hall lights controlled: " + lights.Count + " lights + " + glow.Count + " glowing panels (runtime only)");
            log.AppendLine("3D dark figure, 9 hall appearance spots (used the first moment one is really in the player's view), 3 wing spots for the CCTV feed");
            log.AppendLine("Emergency: 5 rotating red beacons (spot lights) + 2 red fill lights (off until event 2), siren loop (existing L3_Siren, quiet)");
            log.AppendLine("CCTV: camera-wall screens found: " + screens.FindAll(r => r != null).Count + "/6, feed camera -> RT_L3_CctvFeed, relay monitor beside the terminal");
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase6");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), log.ToString());
            Debug.Log("[Level 3 Env] Phase 6 built and saved.\n" + log);
        }

        [MenuItem("Code Breaker/Level 3/Phase 6 - Undo (remove Events pass)")]
        public static void UndoPhase6()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform old = level != null ? level.Find("L3_Environment/" + Group6) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 6 removed.");
        }

        static GameObject DarkFigure(Transform parent)
        {
            Material black = Asset("M_L3_DarkFigure", Shader.Find("Universal Render Pipeline/Lit"));
            black.SetColor("_BaseColor", new Color(0.012f, 0.012f, 0.015f)); black.SetFloat("_Smoothness", 0.05f); black.SetFloat("_Metallic", 0f);
            black.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(black);
            var root = new GameObject("DarkFigure3D"); root.transform.SetParent(parent, false);
            void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler)
            {
                GameObject p = GameObject.CreatePrimitive(type); p.name = name;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = pos; p.transform.localScale = scale; p.transform.localRotation = Quaternion.Euler(euler);
                var r = p.GetComponent<Renderer>(); r.sharedMaterial = black; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            Part(PrimitiveType.Capsule, "Torso", new Vector3(0f, 4.35f, 0f), new Vector3(1.35f, 1.6f, 0.75f), Vector3.zero);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0.05f, 6.45f, 0.05f), new Vector3(0.85f, 1.0f, 0.9f), new Vector3(0f, 0f, -8f));
            Part(PrimitiveType.Capsule, "Neck", new Vector3(0f, 5.85f, 0f), new Vector3(0.35f, 0.4f, 0.35f), Vector3.zero);
            Part(PrimitiveType.Capsule, "LegL", new Vector3(-0.38f, 1.5f, 0f), new Vector3(0.42f, 1.55f, 0.42f), Vector3.zero);
            Part(PrimitiveType.Capsule, "LegR", new Vector3(0.38f, 1.5f, 0f), new Vector3(0.42f, 1.55f, 0.42f), Vector3.zero);
            Part(PrimitiveType.Capsule, "ArmL", new Vector3(-0.9f, 3.55f, 0.05f), new Vector3(0.28f, 1.85f, 0.28f), new Vector3(0f, 0f, -5f));
            Part(PrimitiveType.Capsule, "ArmR", new Vector3(0.9f, 3.55f, 0.05f), new Vector3(0.28f, 1.85f, 0.28f), new Vector3(0f, 0f, 7f));
            // cold light behind the figure so it reads as a black silhouette against the lit floor and wall behind it
            var back = new GameObject("Backlight").AddComponent<Light>();
            back.transform.SetParent(root.transform, false); back.transform.localPosition = new Vector3(0f, 4.6f, -1.8f);
            back.type = LightType.Point; back.color = new Color(0.55f, 0.72f, 1f); back.intensity = 14f; back.range = 10f; back.shadows = LightShadows.None;
            root.SetActive(false);
            return root;
        }

        // ================================================================== play-mode self test (Phase 6)
        class PlayTest6
        {
            readonly string dir; readonly System.Text.StringBuilder r = new System.Text.StringBuilder();
            int phase; float timer; double last = -1; bool figSeen, e1Cap; float seenAt = -1f; Vector3 figPos; bool turned; Vector3 home; Quaternion homeRot; L3ProgressEvents ev;
            public PlayTest6(string d) { dir = d; }
            void Cap(string n) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, n + ".png"));
            public bool Step()
            {
                var g = CodeBreaker.Level2.L2Game.Instance;
                if (g == null || g.player == null) { r.AppendLine("FAIL: L2Game/player not found"); return Done(); }
                if (ev == null) ev = Object.FindFirstObjectByType<L3ProgressEvents>();
                if (ev == null) { r.AppendLine("FAIL: L3ProgressEvents missing"); return Done(); }
                double now = EditorApplication.timeSinceStartup; if (last >= 0) timer += (float)(now - last); last = now;
                if (ev.figure != null && ev.figure.activeSelf) figSeen = true;
                switch (phase)
                {
                    case 0:
                        home = g.player.position; homeRot = g.player.rotation;
                        foreach (string b in new[] { "brief_l3_main", "brief_l3_maint", "brief_l3_sec", "brief_l3_ctrl" }) g.SetFlag(b);
                        r.AppendLine("Before: events reached " + ev.EventsReached + ", emergency " + ev.EmergencyActive);
                        g.SetFlag("card_red_in"); phase = 1; timer = 0f; figSeen = false; return false;
                    case 1:
                        if (!turned && timer > 2.8f) { turned = true; g.Teleport(new Vector3(-17.5f, home.y, -14f), Quaternion.identity); r.AppendLine("Event 1: after the flicker the player walks toward the west aisle (Maintenance side) (figure appeared before turning: " + figSeen + ")"); }
                        if (figSeen && seenAt < 0f) { seenAt = timer; figPos = ev.figure.transform.position; }
                        if (!e1Cap && seenAt >= 0f && timer > seenAt + 0.35f) { e1Cap = true; Cap("E1_figure_and_flicker"); }
                        if (timer < 40f && !(e1Cap && !ev.figure.activeSelf && timer > seenAt + 1f)) return false;
                        r.AppendLine("Event 1 figure stood at " + figPos.ToString("F1") + " (in the player's view)" + (figSeen ? "" : "; last view blocker: " + ev.LastBlocker + ", camera " + (Camera.main != null ? Camera.main.transform.position.ToString("F1") : "none")));
                        float lvl = ev.hallLights.Length > 0 && ev.hallLights[0] != null ? ev.hallLights[0].intensity : -1f;
                        r.AppendLine("Event 1 (1st card): figure appeared " + figSeen + ", figure hidden again " + !ev.figure.activeSelf + ", flag l3evt_1 " + g.Flag("l3evt_1") +
                                     (figSeen && !ev.figure.activeSelf ? " (PASS)" : " (FAIL)"));
                        phase = 11; timer = 0f; return false;
                    case 11:
                        if (timer < 0.4f) return false;
                        g.Teleport(home, homeRot);
                        g.SetFlag("card_blue_in"); phase = 2; timer = 0f; return false;
                    case 2:
                        if (timer < 3.5f) return false;
                        Cap("E2_emergency_lighting");
                        bool sirenOn = ev.siren != null && ev.siren.isPlaying;
                        r.AppendLine("Event 2 (2nd card): emergency " + ev.EmergencyActive + ", beacons on " + (ev.emergencyRoot != null && ev.emergencyRoot.activeSelf) + ", siren playing " + sirenOn +
                                     (ev.EmergencyActive && sirenOn ? " (PASS)" : " (FAIL)"));
                        phase = 21; timer = 0f; return false;
                    case 21:
                        if (timer < 0.4f) return false;
                        g.SetFlag("card_green_in"); phase = 3; timer = 0f; figSeen = false; return false;
                    case 3:
                        if (timer < 2.6f) return false;
                        g.Teleport(new Vector3(-5.7f, home.y, -16.5f), Quaternion.identity);
                        phase = 31; timer = 0f; return false;
                    case 31:
                        if (timer < 0.6f) return false;
                        Cap("E3_relay_monitor_live_feed");
                        r.AppendLine("Event 3 (3rd card): feed camera rendering " + (ev.feedCam != null && ev.feedCam.enabled) + ", relay shows feed " + (ev.relayScreen != null && ev.relayScreen.sharedMaterial == ev.feedMat) +
                                     ", figure in wing " + figSeen);
                        phase = 32; timer = 0f; return false;
                    case 32:
                        if (timer < 0.3f) return false;
                        g.Teleport(new Vector3(41f, home.y, 2.5f), Quaternion.identity);
                        phase = 4; timer = 0f; return false;
                    case 4:
                        if (timer < 0.8f) return false;
                        Cap("E3_camera_wall");
                        phase = 5; timer = 0f; return false;
                    case 5:
                        if (timer < 8f) return false;
                        bool allStatic = true; foreach (var s in ev.camScreens) if (s != null && s.sharedMaterial != ev.staticMat) allStatic = false;
                        r.AppendLine("After the feed: all camera-wall screens static " + allStatic + ", relay static " + (ev.relayScreen.sharedMaterial == ev.staticMat) + ", feed camera off " + !ev.feedCam.enabled +
                                     ", figure hidden " + !ev.figure.activeSelf + (allStatic && !ev.feedCam.enabled && !ev.figure.activeSelf ? " (PASS)" : " (FAIL)"));
                        r.AppendLine("Events never repeat: l3evt_1/2/3 = " + g.Flag("l3evt_1") + "/" + g.Flag("l3evt_2") + "/" + g.Flag("l3evt_3") + " (each runs only when its flag is not yet set)");
                        g.Teleport(home, homeRot);
                        phase = 6; timer = 0f; return false;
                    case 6:
                        if (timer < 0.8f) return false;
                        Cap("E4_hall_after_events");
                        phase = 7; timer = 0f; return false;
                    case 7:
                        if (timer < 0.5f) return false;
                        foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) g.SetFlag(c, false);
                        r.AppendLine("Card flags cleared again (memory only; nothing saved during the test).");
                        return Done();
                }
                return Done();
            }
            bool Done()
            {
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 6 play test:\n" + r);
                return true;
            }
        }
        static PlayTest6 test6;
        static void Test6Tick()
        {
            if (test6 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test6Tick; test6 = null; return; }
            if (test6.Step()) { EditorApplication.update -= Test6Tick; test6 = null; }
        }
        static void StartPlayTest6(string dir) { if (test6 != null) return; Directory.CreateDirectory(dir); test6 = new PlayTest6(dir); EditorApplication.update += Test6Tick; }
    }
}
