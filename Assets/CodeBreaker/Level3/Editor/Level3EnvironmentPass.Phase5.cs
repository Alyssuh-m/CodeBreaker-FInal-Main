using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeBreaker.Level3.EditorTools
{
    // Phase 5: sound and atmosphere. New 3D ambience loops per area + an occasional-sound scheduler, all under
    // "Level3/L3_Environment/Phase5_Audio". Also gives the Phase 3 drip, sparks and CCTV cameras their sounds
    // (those objects belong to this tool; re-run Phase 5 after rebuilding Phase 3).
    // Existing level audio (horror ambience, server hum, siren/alarms, puzzle sounds) is not changed.
    public static partial class Level3EnvironmentPass
    {
        const string Group5 = "Phase5_Audio";
        const string AudioDir = Root + "/Audio/Env";

        [MenuItem("Code Breaker/Level 3/Phase 5 - Build Audio")]
        public static void Build5()
        {
            if (!OpenLevel3()) return;
            log.Length = 0;
            Transform level = FindLevelRoot();
            if (level == null) { Debug.LogError("[Level 3 Env] Level3 root not found."); return; }
            ImportAudio();
            Transform env = level.Find("L3_Environment");
            if (env == null) { env = new GameObject("L3_Environment").transform; env.SetParent(level, false); }
            Transform old = env.Find(Group5);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform g = new GameObject(Group5).transform; g.SetParent(env, false);
            int loops = 0;

            // ---- Maintenance: fluorescent buzz, ventilation fan, electrical hum, steam leak
            Transform m = Group("Maintenance", g, Vector3.zero);
            loops += Loop(m, "FluorescentBuzz", "L3A_FluorescentBuzz", new Vector3(-41f, 10f, 0f), 0.22f, 3f, 22f);
            loops += Loop(m, "VentFan", "L3A_VentFan", new Vector3(-37f, 8.1f, 10.2f), 0.3f, 2f, 14f);
            loops += Loop(m, "PanelHum", "L3A_ElectricalHum", new Vector3(-58.6f, 5.2f, -9.2f), 0.25f, 1f, 9f);
            loops += Loop(m, "SteamLeak", "L3A_SteamHiss", new Vector3(-27.5f, 9.6f, -10f), 0.28f, 1f, 10f);

            // ---- Security: radio static, monitor buzz, badge reader hum
            Transform s = Group("Security", g, Vector3.zero);
            loops += Loop(s, "RadioStatic", "L3A_RadioStatic", new Vector3(41f, 3.5f, 5.6f), 0.2f, 2f, 18f);
            loops += Loop(s, "CameraWallBuzz", "L3A_MonitorBuzz", new Vector3(41f, 6.5f, 10f), 0.17f, 2f, 14f);
            loops += Loop(s, "CrtBuzz", "L3A_MonitorBuzz", new Vector3(44f, 4f, -9.2f), 0.14f, 1f, 8f);
            loops += Loop(s, "BadgeReaderHum", "L3A_ElectricalHum", new Vector3(23.3f, 4.4f, -4.4f), 0.14f, 1f, 6f);

            // ---- Control: electrical hum, cooling steam, distant generator machinery
            Transform c = Group("Control", g, Vector3.zero);
            loops += Loop(c, "DeskHum", "L3A_ElectricalHum", new Vector3(0f, 4f, 42f), 0.2f, 2f, 16f);
            loops += Loop(c, "CoolingSteam", "L3A_SteamHiss", new Vector3(-15f, 9f, 33f), 0.26f, 1f, 10f);
            loops += Loop(c, "Machinery", "L3A_DistantMachinery", new Vector3(0f, 6f, 34f), 0.2f, 4f, 18f);

            // ---- Main hall: server fans at every rack pair (louder as you get close), low machinery
            Transform h = Group("MainHall", g, Vector3.zero);
            var rackPoints = new List<Vector3>();
            foreach (RackPair p in FindRackPairs(level))
            {
                loops += Loop(h, "ServerFans", "L3A_ServerFans", new Vector3(p.x, 4f, (p.front + p.back) * 0.5f), 0.34f, 2f, 14f);
                foreach (Transform r in p.racks) rackPoints.Add(r.position + Vector3.up * 5f);
            }
            loops += Loop(h, "HallMachinery", "L3A_DistantMachinery", new Vector3(0f, 8f, 0f), 0.1f, 8f, 30f);

            // ---- occasional sounds
            var os = g.gameObject.AddComponent<L3AmbientOneShots>();
            AudioClip siren = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/L3_Siren.wav");
            os.entries = new[]
            {
                Shots("RelayClicks", new[] { Clip("L3A_RelayClick") }, rackPoints.ToArray(), new Vector2(1.5f, 5f), new Vector2(0.25f, 0.4f), new Vector2(0.85f, 1.2f), 1f, 12f),
                Shots("HallClank", new[] { Clip("L3A_MechanicalClank") }, new[] { new Vector3(-18f, 9f, 18f), new Vector3(18f, 9f, -18f), new Vector3(0f, 10f, 14f) }, new Vector2(18f, 40f), new Vector2(0.25f, 0.35f), new Vector2(0.8f, 1.1f), 4f, 30f),
                Shots("DistantAlarm", new[] { siren }, new[] { new Vector3(0f, 8f, -32f) }, new Vector2(45f, 90f), new Vector2(0.1f, 0.14f), new Vector2(0.78f, 0.88f), 10f, 45f),
                Shots("SecurityBeeps", new[] { Clip("L3A_SecurityBeep") }, new[] { new Vector3(41f, 3.3f, 5.6f), new Vector3(23.3f, 4.4f, -4.4f) }, new Vector2(12f, 25f), new Vector2(0.25f, 0.35f), new Vector2(0.97f, 1.03f), 2f, 16f),
                Shots("CctvInterference", new[] { Clip("L3A_CctvInterference") }, new[] { new Vector3(41f, 6.5f, 10f), new Vector3(44f, 4f, -9.2f) }, new Vector2(9f, 20f), new Vector2(0.2f, 0.3f), new Vector2(0.9f, 1.1f), 2f, 14f),
                Shots("SystemWarning", new[] { Clip("L3A_SystemWarning") }, new[] { new Vector3(0f, 4f, 42f) }, new Vector2(14f, 28f), new Vector2(0.25f, 0.32f), new Vector2(1f, 1f), 3f, 20f),
                Shots("PanelClicks", new[] { Clip("L3A_PanelClick"), Clip("L3A_RelayClick") }, new[] { new Vector3(16f, 5.4f, 40.4f), new Vector3(-3f, 3.2f, 42f), new Vector3(-16f, 6.5f, 40.6f) }, new Vector2(5f, 12f), new Vector2(0.2f, 0.3f), new Vector2(0.9f, 1.15f), 1.5f, 12f),
            };

            // ---- sounds for the Phase 3 effects (drip, sparks, CCTV motors)
            int hooked = 0;
            foreach (L3Drip d in level.GetComponentsInChildren<L3Drip>(true))
            {
                d.sfx = HookSource(d.drop != null ? d.drop.parent : d.transform, "DripSfx", d.puddle != null ? d.puddle.position + Vector3.up * 0.2f : d.transform.position, 1f, 10f);
                d.dripClip = Clip("L3A_Drip"); d.volume = 0.45f; EditorUtility.SetDirty(d); hooked++;
            }
            foreach (L3Sparks sp in level.GetComponentsInChildren<L3Sparks>(true))
            {
                sp.sfx = HookSource(sp.transform, "SparkSfx", sp.transform.position, 1f, 9f);
                sp.sparkClip = Clip("L3A_Sparks"); sp.volume = 0.5f; EditorUtility.SetDirty(sp); hooked++;
            }
            foreach (L3CameraPan cp in level.GetComponentsInChildren<L3CameraPan>(true))
            {
                AudioSource a = HookSource(cp.transform, "MotorSfx", cp.transform.position, 1f, 10f);
                a.clip = Clip("L3A_CameraMotor"); a.loop = true;
                cp.motor = a; cp.motorVolume = 0.3f; EditorUtility.SetDirty(cp); hooked++;
            }

            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            log.AppendLine("Ambience loops: " + loops + " (3D, linear roll-off, follow the SFX volume setting, quieter while paused)");
            log.AppendLine("  Maintenance: fluorescent buzz, ventilation fan, panel hum, steam leak");
            log.AppendLine("  Security: radio static, camera-wall + CRT monitor buzz, badge reader hum");
            log.AppendLine("  Control: desk electrical hum, cooling steam, generator machinery");
            log.AppendLine("  Main hall: server fans at each of the 4 rack pairs (audible within 14 units = 3.7 m), low machinery");
            log.AppendLine("Occasional sounds: relay clicks at the racks, mechanical clanks, distant alarm (existing L3_Siren, quiet), security beeps, CCTV interference, control-room system warning, panel clicks");
            log.AppendLine("Effect sounds hooked: " + hooked + " (water drip, badge-reader sparks, CCTV camera motors)");
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase5");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "build_report.txt"), log.ToString());
            Debug.Log("[Level 3 Env] Phase 5 built and saved.\n" + log);
        }

        [MenuItem("Code Breaker/Level 3/Phase 5 - Undo (remove Audio pass)")]
        public static void UndoPhase5()
        {
            if (!OpenLevel3()) return;
            Transform level = FindLevelRoot();
            Transform old = level != null ? level.Find("L3_Environment/" + Group5) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (string n in new[] { "DripSfx", "SparkSfx", "MotorSfx" })
                foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
                    if (t != null && t.name == n) Object.DestroyImmediate(t.gameObject);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            EditorSceneManager.SaveScene(level.gameObject.scene);
            Debug.Log("[Level 3 Env] Phase 5 removed.");
        }

        static void ImportAudio()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                if (ai == null) continue;
                bool loop = !(p.Contains("Drip") || p.Contains("Click") || p.Contains("Beep") || p.Contains("Warning") || p.Contains("Interference") || p.Contains("Sparks") || p.Contains("Clank") || p.Contains("Motor"));
                var s = ai.defaultSampleSettings;
                var want = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                if (!ai.forceToMono || s.loadType != want)
                {
                    ai.forceToMono = true;
                    s.loadType = want;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.6f;
                    ai.defaultSampleSettings = s;
                    ai.SaveAndReimport();
                }
            }
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + name + ".wav");

        static int Loop(Transform parent, string name, string clip, Vector3 pos, float vol, float minD, float maxD)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            AudioSource a = go.AddComponent<AudioSource>();
            a.clip = Clip(clip);
            a.loop = true; a.playOnAwake = false;
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = minD; a.maxDistance = maxD; a.dopplerLevel = 0f;
            a.volume = vol;
            var e = go.AddComponent<L3AmbientEmitter>();
            e.baseVolume = vol;
            if (a.clip == null) log.AppendLine("WARNING: clip " + clip + " not found");
            return 1;
        }

        static L3AmbientOneShots.Entry Shots(string label, AudioClip[] clips, Vector3[] pts, Vector2 interval, Vector2 vol, Vector2 pitch, float minD, float maxD) =>
            new L3AmbientOneShots.Entry { label = label, clips = clips, points = pts, interval = interval, volume = vol, pitch = pitch, minDistance = minD, maxDistance = maxD };

        static AudioSource HookSource(Transform parent, string name, Vector3 pos, float minD, float maxD)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = minD; a.maxDistance = maxD; a.dopplerLevel = 0f;
            return a;
        }

        // ================================================================== play-mode self test (Phase 5)
        class PlayTest5
        {
            readonly string dir; readonly System.Text.StringBuilder r = new System.Text.StringBuilder();
            int phase; float timer; Vector3 home; Quaternion homeRot; float[] buf = new float[2048];
            float rmsFar, rmsNear, rmsMaint;
            public PlayTest5(string d) { dir = d; }

            float Rms()
            {
                AudioListener.GetOutputData(buf, 0);
                double s = 0; foreach (float v in buf) s += v * v;
                return (float)System.Math.Sqrt(s / buf.Length);
            }

            public bool Step()
            {
                var g = CodeBreaker.Level2.L2Game.Instance;
                if (g == null || g.player == null) { r.AppendLine("FAIL: L2Game/player not found"); return Done(); }
                timer += Time.unscaledDeltaTime;
                switch (phase)
                {
                    case 0:
                        home = g.player.position; homeRot = g.player.rotation;
                        int playing = 0, total = 0;
                        foreach (var e in Object.FindObjectsByType<L3AmbientEmitter>()) { total++; if (e.GetComponent<AudioSource>().isPlaying) playing++; }
                        r.AppendLine("Ambience loops playing: " + playing + " / " + total + (playing == total && total > 0 ? " (PASS)" : " (FAIL)"));
                        r.AppendLine("SFX volume setting: " + CodeBreakerSettings.SfxVolume.ToString("0.00") + ", master (AudioListener): " + AudioListener.volume.ToString("0.00"));
                        // far from everything: the spawn point area in front of the terminal
                        g.Teleport(new Vector3(0f, home.y, -19f), Quaternion.identity);
                        phase = 1; timer = 0f; return false;
                    case 1:
                        if (timer < 2f) return false;
                        rmsFar = Rms();
                        g.Teleport(new Vector3(-11f, home.y, -7.2f), Quaternion.identity);   // cold aisle of a rack pair
                        phase = 2; timer = 0f; return false;
                    case 2:
                        if (timer < 2f) return false;
                        rmsNear = Rms();
                        g.Teleport(new Vector3(-40f, home.y, 0f), Quaternion.identity);      // middle of Maintenance
                        phase = 3; timer = 0f; return false;
                    case 3:
                        if (timer < 2f) return false;
                        rmsMaint = Rms();
                        g.Teleport(home, homeRot);
                        r.AppendLine("Overall output level (RMS): spawn area " + rmsFar.ToString("0.0000") + ", next to racks " + rmsNear.ToString("0.0000") +
                                     ", Maintenance " + rmsMaint.ToString("0.0000"));
                        r.AppendLine(rmsNear > rmsFar * 1.15f ? "Server fans get louder near the racks (PASS)" : "Server fans not clearly louder near racks (check)");
                        phase = 4; timer = 0f; return false;
                    case 4:
                        if (timer < 20f) return false;
                        var os = Object.FindFirstObjectByType<L3AmbientOneShots>();
                        r.AppendLine("Occasional sounds played in ~26 s: " + (os != null ? os.TotalPlayed().ToString() : "n/a") + (os != null && os.TotalPlayed() > 2 ? " (PASS)" : " (check)"));
                        var drip = Object.FindFirstObjectByType<L3Drip>();
                        r.AppendLine("Drip sound hooked: " + (drip != null && drip.sfx != null && drip.dripClip != null));
                        var sp = Object.FindFirstObjectByType<L3Sparks>();
                        r.AppendLine("Spark sound hooked: " + (sp != null && sp.sfx != null && sp.sparkClip != null));
                        int motors = 0; foreach (var c in Object.FindObjectsByType<L3CameraPan>()) if (c.motor != null) motors++;
                        r.AppendLine("CCTV motor sounds hooked: " + motors);
                        r.AppendLine("Total AudioSources in scene: " + Object.FindObjectsByType<AudioSource>().Length);
                        return Done();
                }
                return Done();
            }
            bool Done()
            {
                File.WriteAllText(Path.Combine(dir, "playtest_report.txt"), r.ToString());
                Debug.Log("[Level 3 Env] Phase 5 play test:\n" + r);
                return true;
            }
        }
        static PlayTest5 test5;
        static void Test5Tick()
        {
            if (test5 == null || !EditorApplication.isPlaying) { EditorApplication.update -= Test5Tick; test5 = null; return; }
            if (test5.Step()) { EditorApplication.update -= Test5Tick; test5 = null; }
        }
        static void StartPlayTest5(string dir) { if (test5 != null) return; Directory.CreateDirectory(dir); test5 = new PlayTest5(dir); EditorApplication.update += Test5Tick; }
    }
}
