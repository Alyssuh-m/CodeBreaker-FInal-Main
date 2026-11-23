using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Code Breaker > Fix 10 (Nov 22): the ten issues from the screenshots.
//  1. Restore .meta files      - .meta files missing from the GitHub download were regenerated with new
//                                IDs, so Level 1's lecterns lost their prefabs ("Missing Prefab"). Copies
//                                the original .meta from the older project copy when the IDs differ.
//  2. Level 3 visual fixes      - note over the cabinet seam, binders inside the CRT, fan cage, floor
//                                words, short pipes. Saves Level 3.
//  3. Level 1 report            - every light and every renderer with a missing/broken material.
//  4. Level 2 drawer test       - Play mode, Level 2: the study filing cabinet's drawers.
// Every item writes Logs/CB_Fix10.txt (appended) and pictures to Logs/Fix10/.
public static class CodeBreakerFix10
{
    const string OldProject = "C:/Users/jonar/Downloads/CodeBreaker-FInal-Main-main/CodeBreaker-FInal-Main-main";
    const string LogPath = "Logs/CB_Fix10.txt";
    static StringBuilder log;

    static void Begin(string title) { log = new StringBuilder("\n==== " + title + "  " + System.DateTime.Now + "\n"); }
    static void End()
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, log.ToString());
        Debug.Log("[Fix10] done -> " + LogPath);
    }

    // ================================================================== 1. .meta restore
    [MenuItem("Code Breaker/Fix 10/1a. Meta files: report", false, 900)]
    static void MetaReport() => Meta(false);
    [MenuItem("Code Breaker/Fix 10/1b. Meta files: restore originals", false, 901)]
    static void MetaApply() => Meta(true);

    static string Guid(string metaText)
    {
        Match m = Regex.Match(metaText, @"^guid:\s*([0-9a-f]{32})", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : null;
    }

    static void Meta(bool apply)
    {
        Begin(apply ? "META RESTORE (apply)" : "META RESTORE (report)");
        string newAssets = Path.GetFullPath("Assets").Replace('\\', '/');
        string oldAssets = OldProject + "/Assets";
        if (!Directory.Exists(oldAssets)) { log.AppendLine("old project not found: " + oldAssets); End(); return; }

        // every GUID currently used by a meta in this project (to avoid duplicates)
        var metas = new List<string>();
        try { metas.AddRange(Directory.GetFiles(newAssets, "*.meta", SearchOption.AllDirectories)); }
        catch (System.Exception e) { log.AppendLine("scan error: " + e.Message); }
        var guidOwner = new Dictionary<string, string>();
        foreach (string m in metas) { try { string g = Guid(File.ReadAllText(m)); if (g != null) guidOwner[g] = m; } catch { } }

        // which GUIDs do scenes / prefabs / materials / assets of this project refer to?
        var referenced = new HashSet<string>();
        foreach (string ext in new[] { "*.unity", "*.prefab", "*.mat", "*.asset", "*.controller", "*.anim" })
        {
            string[] files;
            try { files = Directory.GetFiles(newAssets, ext, SearchOption.AllDirectories); } catch { continue; }
            foreach (string f in files)
            {
                string txt; try { txt = File.ReadAllText(f); } catch { continue; }
                if (txt.Length < 5 || !txt.StartsWith("%YAML")) continue;   // binary assets: skip
                foreach (Match mm in Regex.Matches(txt, @"guid: ([0-9a-f]{32})")) referenced.Add(mm.Groups[1].Value);
            }
        }

        string backup = Path.GetFullPath("CB_Backups/20261122_meta").Replace('\\', '/');
        int same = 0, restored = 0, skipped = 0, noOld = 0;
        foreach (string m in metas)
        {
            string rel = m.Replace('\\', '/').Substring(newAssets.Length + 1);
            string old = oldAssets + "/" + rel;
            if (!File.Exists(old)) { noOld++; continue; }
            string nt, ot;
            try { nt = File.ReadAllText(m); ot = File.ReadAllText(old); } catch { continue; }
            string ng = Guid(nt), og = Guid(ot);
            if (ng == null || og == null || ng == og) { same++; continue; }

            bool oldUsed = referenced.Contains(og), newUsed = referenced.Contains(ng);
            bool clash = guidOwner.TryGetValue(og, out string owner) && owner != m;
            string why = "refs old " + oldUsed + ", refs new " + newUsed + (clash ? ", OLD ID USED BY " + owner : "");
            if (clash || (newUsed && !oldUsed)) { skipped++; log.AppendLine("  SKIP    " + rel + "  (" + why + ")"); continue; }
            log.AppendLine((apply ? "  RESTORE " : "  DIFFERS ") + rel + "  " + ng + " -> " + og + "  (" + why + ")");
            if (apply)
            {
                string b = backup + "/" + rel;
                Directory.CreateDirectory(Path.GetDirectoryName(b));
                File.Copy(m, b, true);
                File.Copy(old, m, true);
            }
            restored++;
        }
        log.AppendLine("metas checked " + metas.Count + ": same " + same + ", not in old copy " + noOld + ", " + (apply ? "restored " : "would restore ") + restored + ", skipped " + skipped);
        if (apply && restored > 0)
        {
            log.AppendLine("backups of the replaced metas: " + backup);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }
        End();
    }

    // Missing references: every asset ID used by the game's scenes (and the prefabs / materials they
    // use), checked against this project. For each ID that no longer exists here, the older project
    // copy says which file it belonged to; the report lists "RESTORE <file> <old id> <current id>".
    // (Reads with the \\?\ prefix because this project's folder path is long enough to break normal
    // file reads deep inside asset packs.)
    [MenuItem("Code Breaker/Fix 10/1c. Missing references: report", false, 902)]
    static void MissingRefs()
    {
        Begin("MISSING REFERENCES");
        var oldMap = new Dictionary<string, string>();
        string oldAssets = OldProject + "/Assets";
        foreach (string m in Directory.GetFiles(oldAssets, "*.meta", SearchOption.AllDirectories))
        {
            string t = ReadLong(m); if (t == null) continue;
            string g = Guid(t); if (g != null) oldMap[g] = "Assets/" + m.Replace('\\', '/').Substring(oldAssets.Length + 1, m.Length - oldAssets.Length - 1 - 5);
        }
        log.AppendLine("old project: " + oldMap.Count + " asset ids");

        var queue = new Queue<(string file, string from)>();
        var seenFiles = new HashSet<string>();
        foreach (string sc in new[] { "Boot", "MainMenu", "Level 1", "Level 2", "Level 3" }) queue.Enqueue(("Assets/Scenes/" + sc + ".unity", "scene"));
        var reported = new HashSet<string>();
        int restore = 0, gone = 0;
        while (queue.Count > 0)
        {
            var (file, from) = queue.Dequeue();
            if (!seenFiles.Add(file)) continue;
            string txt = ReadLong(Path.GetFullPath(file));
            if (txt == null || !txt.StartsWith("%YAML")) continue;
            foreach (Match mm in Regex.Matches(txt, @"guid: ([0-9a-f]{32})"))
            {
                string g = mm.Groups[1].Value;
                if (g.StartsWith("0000000000000000") || g == "0000000000000000f000000000000000") continue;
                string here = AssetDatabase.GUIDToAssetPath(g);
                if (!string.IsNullOrEmpty(here))
                {
                    string ext = Path.GetExtension(here).ToLowerInvariant();
                    if (ext == ".prefab" || ext == ".mat" || ext == ".asset" || ext == ".controller") queue.Enqueue((here, file));
                    continue;
                }
                if (!reported.Add(g)) continue;
                if (oldMap.TryGetValue(g, out string rel))
                {
                    string cur = AssetDatabase.AssetPathToGUID(rel);
                    if (!string.IsNullOrEmpty(cur)) { restore++; log.AppendLine("RESTORE|" + rel + "|" + g + "|" + cur + "|used by " + file); }
                    else { gone++; log.AppendLine("NOFILE |" + rel + "|" + g + "|used by " + file); }
                }
                else { gone++; log.AppendLine("UNKNOWN|" + g + "|used by " + file); }
            }
        }
        log.AppendLine("files scanned " + seenFiles.Count + ", restorable " + restore + ", not restorable " + gone);
        End();
    }

    static string ReadLong(string path)
    {
        try { return File.ReadAllText(path); }
        catch
        {
            try { return File.ReadAllText(@"\\?\" + Path.GetFullPath(path).Replace('/', '\\')); } catch { return null; }
        }
    }

    // ================================================================== 2. Level 3 visual fixes
    [MenuItem("Code Breaker/Fix 10/2a. Level 3: pictures (before or after)", false, 910)]
    static void L3Pictures()
    {
        Begin("LEVEL 3 PICTURES");
        if (!OpenScene("Level 3")) { End(); return; }
        ShootL3(System.DateTime.Now.ToString("HHmmss"));
        End();
    }

    [MenuItem("Code Breaker/Fix 10/2b. Level 3: apply visual fixes", false, 911)]
    static void L3Apply()
    {
        Begin("LEVEL 3 VISUAL FIXES");
        if (!OpenScene("Level 3")) { End(); return; }
        Transform env = Find("Level3/L3_Environment");
        if (env == null) { log.AppendLine("L3_Environment not found"); End(); return; }

        // 1. POWER MUST REMAIN OFF note: it sat 5 mm behind the cabinet's door seam (black line through it)
        //    and over the handle. The left door is partly covered by the brown down-pipe, so the note goes
        //    on the right door, below the handle, 85% size so it fits between the seam and the cabinet edge.
        Transform ep = FindUnder(env, "ElectricalPanel");
        Transform note = FindUnder(env, "Note_PowerOff");
        if (ep != null && note != null)
        {
            Undo.RecordObject(note, "note");
            Vector3 before = note.position;
            note.position = ep.TransformPoint(new Vector3(0.6f, -0.9f, -0.065f));
            note.localScale = Vector3.one * 0.85f;
            // check: paper corners stay on the right door (local x 0.03..1.18) and below the handle (y < -0.27)
            Transform paper = note.Find("Paper");
            float minX = 9, maxX = -9, maxY = -9;
            if (paper != null)
                for (int cx = -1; cx <= 1; cx += 2) for (int cy = -1; cy <= 1; cy += 2)
                {
                    Vector3 c = ep.InverseTransformPoint(paper.TransformPoint(new Vector3(cx * 0.5f, cy * 0.5f, 0f)));
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
                }
            log.AppendLine("1. Note_PowerOff " + V(before) + " -> " + V(note.position) + " scale 0.85 (right door, below handle)");
            log.AppendLine("   paper on panel: x " + minX.ToString("0.000") + ".." + maxX.ToString("0.000") + " (door 0.015..1.2), top y " + maxY.ToString("0.000") + " (handle bottom -0.25)");
        }
        else log.AppendLine("1. ElectricalPanel / Note_PowerOff not found");

        // 2. binders were placed inside the left CRT (black one fully hidden, red one half inside)
        Transform office = FindUnder(env, "OfficeCorner");
        if (office != null)
        {
            float[] xs = { -2.88f, -2.655f, -2.43f };
            for (int i = 0; i < 3; i++)
            {
                Transform b = office.Find("Binder_" + i);
                if (b == null) { log.AppendLine("2. Binder_" + i + " missing"); continue; }
                Undo.RecordObject(b, "binder");
                Vector3 lp = b.localPosition, ls = b.localScale;
                b.localPosition = new Vector3(xs[i], lp.y, lp.z);
                b.localScale = new Vector3(0.21f, ls.y, ls.z);
                log.AppendLine("2. " + b.name + " x " + lp.x.ToString("F3") + " -> " + xs[i] + ", width " + ls.x.ToString("F2") + " -> 0.21");
            }
            Transform crt = office.Find("CRT_0");
            if (crt != null)
            {
                Bounds cb = crt.GetComponent<Renderer>().bounds;
                foreach (Transform b in office) if (b.name.StartsWith("Binder_"))
                    log.AppendLine("   " + b.name + " overlaps CRT_0: " + b.GetComponent<Renderer>().bounds.Intersects(cb));
            }
        }
        else log.AppendLine("2. OfficeCorner not found");

        // 3. fan: remove the mesh cage, add a small hub so the blades look finished
        Transform fan = FindUnder(env, "BrokenWallFan");
        if (fan != null)
        {
            Transform guard = fan.Find("Guard");
            if (guard != null) { Undo.DestroyObjectImmediate(guard.gameObject); log.AppendLine("3. fan cage (Guard) removed"); }
            else log.AppendLine("3. fan cage already gone");
            Transform blades = fan.Find("Blades");
            if (blades != null && fan.Find("Hub") == null)
            {
                GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hub.name = "Hub";
                Object.DestroyImmediate(hub.GetComponent<Collider>());
                hub.transform.SetParent(fan, false);
                hub.transform.localPosition = blades.localPosition + new Vector3(0f, 0f, -0.04f);
                hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                hub.transform.localScale = new Vector3(0.42f, 0.07f, 0.42f);
                Transform housing = fan.Find("Housing");
                if (housing != null) hub.GetComponent<Renderer>().sharedMaterial = housing.GetComponent<Renderer>().sharedMaterial;
                Undo.RegisterCreatedObjectUndo(hub, "hub");
                log.AppendLine("3. hub added at the blade centre (housing material)");
            }
        }
        else log.AppendLine("3. BrokenWallFan not found");

        // 4-6. painted wing words on the floor (lines and chevrons stay)
        Transform lines = FindUnder(env, "FloorNavigationLines");
        foreach (string w in new[] { "Label_CONTROL ROOM", "Label_SECURITY", "Label_MAINTENANCE" })
        {
            Transform t = lines != null ? FindUnder(lines, w) : null;
            if (t != null) { Undo.DestroyObjectImmediate(t.gameObject); log.AppendLine("4-6. floor word removed: " + w); }
            else log.AppendLine("4-6. " + w + " not found (already removed?)");
        }

        // 7-8. pipes on the Maintenance south wall: both stopped 0.65 short of the east wall (x -22.95),
        //      the red one also 1.55 short of the west wall (x -59.55). Ends now go just into the walls.
        Transform maint = FindUnder(env, "Phase3_Wings/Maintenance") ?? FindUnder(env, "Maintenance");
        if (maint != null)
        {
            Stretch(maint.Find("Pipe_Main"), null, -22.7f);     // west end stays at the down pipe elbow
            Stretch(maint.Find("Pipe_Fire"), -59.75f, -22.7f);
        }
        else log.AppendLine("7-8. Maintenance group not found");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        log.AppendLine("Level 3 saved");
        End();
    }

    // Cylinder along world X: move its west (smaller x) and/or east end, keep radius and height.
    static void Stretch(Transform pipe, float? west, float east)
    {
        if (pipe == null) { log.AppendLine("7-8. pipe missing"); return; }
        Vector3 axis = pipe.up;
        float half = pipe.lossyScale.y;
        Vector3 a = pipe.position - axis * half, b = pipe.position + axis * half;
        Vector3 w = a.x < b.x ? a : b, e = a.x < b.x ? b : a;
        Vector3 nw = west.HasValue ? new Vector3(west.Value, w.y, w.z) : w;
        Vector3 ne = new Vector3(east, e.y, e.z);
        Undo.RecordObject(pipe, "pipe");
        pipe.position = (nw + ne) * 0.5f;
        float parentY = pipe.parent != null ? pipe.parent.lossyScale.y : 1f;
        Vector3 ls = pipe.localScale;
        pipe.localScale = new Vector3(ls.x, (Vector3.Distance(nw, ne) * 0.5f) / parentY, ls.z);
        log.AppendLine("7-8. " + pipe.name + " x " + w.x.ToString("F2") + ".." + e.x.ToString("F2") + " -> " + nw.x.ToString("F2") + ".." + ne.x.ToString("F2"));
    }

    static void ShootL3(string tag)
    {
        var shots = new (string, Vector3, Vector3)[]
        {
            ("1_note",      new Vector3(-56.6f, 4.8f, -9.7f),  new Vector3(-59.3f, 4.6f, -9.6f)),
            ("2_crt",       new Vector3(42.4f, 4.6f, -5.4f),   new Vector3(42.0f, 3.7f, -9.9f)),
            ("3_fan",       new Vector3(-37f, 7.0f, 6.0f),     new Vector3(-37f, 8.1f, 10.7f)),
            ("4_control",   new Vector3(1.4f, 5f, 10.5f),      new Vector3(1.4f, 0f, 16f)),
            ("5_security",  new Vector3(11.5f, 5f, 2.35f),     new Vector3(16.5f, 0f, 2.35f)),
            ("6_maint",     new Vector3(-11.5f, 5f, 2.35f),    new Vector3(-16.5f, 0f, 2.35f)),
            ("7_pipes_east",new Vector3(-27.5f, 7.5f, -6.5f),  new Vector3(-23f, 9.3f, -10.5f)),
            ("8_pipe_west", new Vector3(-55.5f, 7.0f, -6.0f),  new Vector3(-59.4f, 9.0f, -10.5f)),
        };
        foreach (var (name, from, at) in shots) Shot("L3_" + name + "_" + tag, from, at);
    }

    // ================================================================== 3. Level 1 report
    [MenuItem("Code Breaker/Fix 10/3. Level 1: lights + broken materials report", false, 920)]
    static void L1Report()
    {
        Begin("LEVEL 1 REPORT");
        if (!OpenScene("Level 1")) { End(); return; }
        int broken = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                string problem = null;
                if (m == null) problem = "MISSING material (slot " + i + ")";
                else if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader") problem = "BROKEN shader on " + m.name;
                else if (!m.shader.isSupported) problem = "unsupported shader " + m.shader.name + " on " + m.name;
                else if (!m.shader.name.StartsWith("Universal Render Pipeline") && !m.shader.name.StartsWith("TextMeshPro") && !m.shader.name.StartsWith("Shader Graphs") && !m.shader.name.StartsWith("Sprites") && !m.shader.name.StartsWith("UI/"))
                    problem = "non-URP shader " + m.shader.name + " on " + m.name + " (" + AssetDatabase.GetAssetPath(m) + ")";
                if (problem == null) continue;
                broken++;
                log.AppendLine("  " + PathOf(r.transform) + "  " + problem + "  at " + V(r.bounds.center));
            }
        }
        log.AppendLine("renderers with material problems: " + broken);
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (PrefabUtility.IsPrefabAssetMissing(go)) log.AppendLine("  MISSING PREFAB: " + PathOf(go.transform));
            foreach (var c in go.GetComponents<Component>()) if (c == null) log.AppendLine("  MISSING SCRIPT on " + PathOf(go.transform));
        }
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        log.AppendLine("lights: " + lights.Length);
        foreach (Light l in lights)
            log.AppendLine("  " + PathOf(l.transform).PadRight(70) + " " + l.type + " on " + (l.enabled && l.gameObject.activeInHierarchy) +
                           " int " + l.intensity.ToString("F2") + " range " + l.range.ToString("F1") + " shadows " + l.shadows + " at " + V(l.transform.position));
        End();
    }

    // ================================================================== 3b. Level 1 shadow atlas
    // Level 1's four ceiling lamps are point lights with soft shadows at the "High" tier (1024 px per cube
    // face): 4 x 6 = 24 maps of 1024 px cannot fit the 2048 px atlas, so URP cut them all to 256 px and
    // logged "Reduced additional punctual light shadows resolution by 4". Fix: atlas 4096 and the lamps at
    // the "Medium" tier (512 px): 24 maps fit, and the shadows are twice as sharp as before.
    [MenuItem("Code Breaker/Fix 10/3b. Level 1: fix ceiling lamp shadow atlas", false, 921)]
    static void L1Shadows()
    {
        Begin("LEVEL 1 SHADOW ATLAS");
        var rp = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/PC_RPAsset.asset");
        if (rp != null)
        {
            var so = new SerializedObject(rp);
            var p = so.FindProperty("m_AdditionalLightsShadowmapResolution");
            if (p != null) { log.AppendLine("PC_RPAsset additional light shadow atlas " + p.intValue + " -> 4096"); p.intValue = 4096; so.ApplyModifiedProperties(); EditorUtility.SetDirty(rp); AssetDatabase.SaveAssets(); }
            else log.AppendLine("PC_RPAsset: atlas property not found");
        }
        else log.AppendLine("PC_RPAsset not found");

        if (!OpenScene("Level 1")) { End(); return; }
        int changed = 0, maps = 0;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type != LightType.Point || l.shadows == LightShadows.None) continue;
            maps += 6;
            var data = l.GetComponent("UniversalAdditionalLightData");
            if (data == null) { log.AppendLine("  " + PathOf(l.transform) + ": no URP light data"); continue; }
            var so = new SerializedObject(data);
            var t = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (t == null) { log.AppendLine("  " + PathOf(l.transform) + ": tier property not found"); continue; }
            log.AppendLine("  " + PathOf(l.transform) + " shadow tier " + t.intValue + " -> 1 (Medium, 512 px)");
            t.intValue = 1; so.ApplyModifiedProperties(); changed++;
        }
        log.AppendLine("shadowed point lights changed: " + changed + ", cube faces " + maps + " x 512 px = " + (maps * 512 * 512) + " px, atlas 4096 x 4096 = " + (4096 * 4096) + " px");
        if (changed > 0) { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); log.AppendLine("Level 1 saved"); }
        End();
    }

    // ================================================================== 4. Level 2 drawer test (Play mode)
    [MenuItem("Code Breaker/Fix 10/4. Level 2: study filing cabinet drawers (Play mode)", false, 930)]
    static void L2Drawers()
    {
        Begin("LEVEL 2 STUDY DRAWERS");
        if (!Application.isPlaying) { log.AppendLine("enter Play mode in Level 2 first"); End(); return; }
        Transform cab = Find("Level2/Architecture/Level2_Colliders/Collider_STU_FilingCabinet");
        if (cab == null) { log.AppendLine("Collider_STU_FilingCabinet not found"); End(); return; }
        Collider cc0 = cab.GetComponent<Collider>();
        Bounds cb = cc0 != null ? cc0.bounds : new Bounds(cab.position, Vector3.one);
        log.AppendLine("cabinet collider " + V(cb.center) + " size " + V(cb.size) + " trigger " + (cc0 != null && cc0.isTrigger) + " layer " + LayerMask.LayerToName(cab.gameObject.layer));
        GameObject p = GameObject.FindWithTag("Player");
        CharacterController pcc = p != null ? p.GetComponent<CharacterController>() : null;
        if (pcc == null) { log.AppendLine("player not found"); End(); return; }
        Camera cam = Camera.main;
        log.AppendLine("timeScale " + Time.timeScale + " time " + Time.time.ToString("F1") + ", camera " + (cam != null ? PathOf(cam.transform) + " y " + cam.transform.position.y.ToString("F2") + " near " + cam.nearClipPlane : "none") +
                       ", capsule world y " + (pcc.transform.position.y + pcc.center.y - pcc.height / 2f).ToString("F2") + ".." + (pcc.transform.position.y + pcc.center.y + pcc.height / 2f).ToString("F2") +
                       " cylinder top " + (pcc.transform.position.y + pcc.center.y + pcc.height / 2f - pcc.radius).ToString("F2") + " stepOffset " + pcc.stepOffset + " skin " + pcc.skinWidth);
        log.AppendLine("player layer " + LayerMask.LayerToName(p.layer) + " radius " + pcc.radius + " height " + pcc.height + " center " + V(pcc.center) + " at " + V(p.transform.position));
        var all = Object.FindObjectsByType<CodeBreaker.Level2.L2Openable>(FindObjectsInactive.Include);
        drawers = all.Where(o => (o.transform.position - cb.center).magnitude < 4f).OrderByDescending(o => o.transform.position.y).ToList();
        foreach (var o in drawers)
        {
            log.AppendLine("  " + PathOf(o.transform) + "  kind " + o.kind + " slide " + V(o.slide) + " maxOpen " + o.maxOpen + " y " + o.transform.position.y.ToString("F2") +
                           " layer " + LayerMask.LayerToName(o.gameObject.layer) + " playerCollides " + !Physics.GetIgnoreLayerCollision(p.layer, o.gameObject.layer));
            foreach (var c in o.GetComponentsInChildren<Collider>(true))
                log.AppendLine("     collider on " + c.name + " " + c.GetType().Name + " trigger " + c.isTrigger + " enabled " + c.enabled + " bounds " + V(c.bounds.center) + " size " + V(c.bounds.size));
            var mf = o.GetComponent<MeshFilter>();
            log.AppendLine("     mesh on itself: " + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " bounds " + V(mf.sharedMesh.bounds.size) : "none") + ", child meshes " + o.GetComponentsInChildren<MeshFilter>().Length + ", lossyScale " + V(o.transform.lossyScale));
        }
        // walk test: open each drawer, put the player 1.2 m in front of it and walk straight into it
        walkPlayer = pcc; walkHome = p.transform.position; walkIndex = 0; walkFrame = 0;
        EditorApplication.update -= WalkStep; EditorApplication.update += WalkStep;
    }

    static List<CodeBreaker.Level2.L2Openable> drawers;
    static CharacterController walkPlayer;
    static Vector3 walkHome, walkStart, walkClosed;
    static bool walkWait;
    static int walkIndex, walkFrame;

    static void Teleport(CharacterController cc, Vector3 pos)
    {
        cc.enabled = false; cc.transform.position = pos; cc.enabled = true; Physics.SyncTransforms();
    }

    static void WalkStep()
    {
        if (!Application.isPlaying || walkPlayer == null || drawers == null) { EditorApplication.update -= WalkStep; return; }
        if (walkIndex >= drawers.Count)
        {
            EditorApplication.update -= WalkStep;
            Teleport(walkPlayer, walkHome);
            End();
            return;
        }
        var d = drawers[walkIndex];
        Vector3 dir = d.slide; dir.y = 0f;
        if (d.kind != CodeBreaker.Level2.L2Openable.Kind.Drawer || dir.sqrMagnitude < 1e-6f) { walkIndex++; walkFrame = 0; return; }
        Vector3 n = dir.normalized;
        walkFrame++;
        if (walkFrame == 1)
        {
            Collider sd = d.GetComponents<Collider>().FirstOrDefault(c => !c.isTrigger);
            walkClosed = sd != null ? sd.bounds.center : d.transform.position;
            walkWait = true;
            if (!d.IsOpen) d.Interact();
        }
        else if (walkWait && walkFrame >= 2 && walkFrame < 400)
        {
            // wait until the drawer has finished sliding out (the slide uses game time)
            Collider sd = d.GetComponents<Collider>().FirstOrDefault(c => !c.isTrigger);
            if (sd != null && Vector3.Dot(sd.bounds.center, n) - Vector3.Dot(walkClosed, n) > d.slide.magnitude * d.maxOpen - 0.02f) { walkWait = false; walkFrame = 59; }
            else if (walkFrame == 399) { log.AppendLine("WALK " + d.name + ": drawer did not slide out (timeScale " + Time.timeScale + ")"); walkWait = false; walkFrame = 59; }
        }
        else if (walkFrame == 60)
        {
            Collider solid = d.GetComponents<Collider>().FirstOrDefault(c => !c.isTrigger);
            Bounds b = solid != null ? solid.bounds : new Bounds(d.transform.position, Vector3.zero);
            log.AppendLine("WALK " + d.name + " (y " + d.transform.position.y.ToString("F2") + "): open " + d.IsOpen + ", solid " + (solid != null) + " bounds " + V(b.center) + " size " + V(b.size));
            float front = Vector3.Dot(b.center, n) + Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z))));
            Vector3 lateral = b.center - n * Vector3.Dot(b.center, n);
            walkStart = lateral + n * (front + walkPlayer.radius + 1.2f);
            walkStart.y = walkHome.y;
            Teleport(walkPlayer, walkStart);
        }
        else if (walkFrame > 60 && walkFrame < 150)
        {
            walkPlayer.Move(-n * 0.04f + Vector3.down * 0.02f);
        }
        else if (walkFrame == 150)
        {
            Collider solid = d.GetComponents<Collider>().FirstOrDefault(c => !c.isTrigger);
            Bounds b = solid != null ? solid.bounds : new Bounds(d.transform.position, Vector3.zero);
            float front = Vector3.Dot(b.center, n) + Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z))));
            Vector3 pp = walkPlayer.transform.position;
            float gap = Vector3.Dot(pp, n) - front - walkPlayer.radius;
            bool inside = solid != null && Physics.ComputePenetration(walkPlayer, pp, walkPlayer.transform.rotation, solid, solid.transform.position, solid.transform.rotation, out _, out float depth) && depth > 0.01f;
            Bounds pb = walkPlayer.bounds;
            bool heightOverlap = pb.min.y < b.max.y && pb.max.y > b.min.y;
            Camera cam = Camera.main;
            bool camInside = cam != null && b.Contains(cam.transform.position);
            float camGap = cam != null ? Vector3.Dot(cam.transform.position, n) - front : 0f;
            log.AppendLine("     camera y " + (cam != null ? cam.transform.position.y.ToString("F2") : "-") + ", camera to drawer front " + camGap.ToString("F2") + " m, camera inside drawer " + camInside);
            log.AppendLine("     walked " + (Vector3.Dot(walkStart - pp, n)).ToString("F2") + " m toward it; player edge to drawer front " + gap.ToString("F2") +
                           " m (negative = went into/through it), penetrating " + inside + ", player y " + pb.min.y.ToString("F2") + ".." + pb.max.y.ToString("F2") + " drawer y " + b.min.y.ToString("F2") + ".." + b.max.y.ToString("F2") + " heightOverlap " + heightOverlap +
                           " -> " + (camGap < 0.1f ? "FAIL (camera went into / over the drawer)" : "PASS (stopped in front of the drawer)"));
            log.AppendLine("     head clearance blocker: " + (d.HeadClearance != null ? V(d.HeadClearance.bounds.center) + " size " + V(d.HeadClearance.bounds.size) + " enabled " + d.HeadClearance.enabled : "none"));
            if (d.IsOpen) d.Interact();
        }
        else if (walkFrame > 200) { walkIndex++; walkFrame = 0; }
    }

    [MenuItem("Code Breaker/Fix 10/Open Level 2", false, 940)]
    static void OpenL2() { Begin("OPEN LEVEL 2"); OpenScene("Level 2"); End(); }

    // ================================================================== helpers
    static bool OpenScene(string name)
    {
        string path = "Assets/Scenes/" + name + ".unity";
        if (SceneManager.GetActiveScene().path == path) return true;
        if (Application.isPlaying) { log.AppendLine("leave Play mode first"); return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { log.AppendLine("cancelled"); return false; }
        EditorSceneManager.OpenScene(path);
        return true;
    }

    static Transform Find(string path)
    {
        string[] parts = path.Split('/');
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
            if (t != null) return t;
        }
        return null;
    }

    static Transform FindUnder(Transform root, string nameOrPath)
    {
        if (root == null) return null;
        Transform direct = root.Find(nameOrPath);
        if (direct != null) return direct;
        string last = nameOrPath.Contains("/") ? nameOrPath.Substring(nameOrPath.LastIndexOf('/') + 1) : nameOrPath;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == last && (!nameOrPath.Contains("/") || PathOf(t).EndsWith(nameOrPath))) return t;
        return null;
    }

    static string PathOf(Transform t) { string s = t.name; while ((t = t.parent) != null) s = t.name + "/" + s; return s; }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static void Shot(string name, Vector3 from, Vector3 look)
    {
        Directory.CreateDirectory("Logs/Fix10");
        var go = new GameObject("CB_ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 55f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        cam.transform.position = from; cam.transform.LookAt(look);
        var lgo = new GameObject("CB_ShotLight");
        var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = 18f; l.intensity = 6f; l.shadows = LightShadows.None;
        lgo.transform.position = from + (look - from) * 0.35f;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;
        cam.Render(); cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Logs/Fix10/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); Object.DestroyImmediate(lgo);
        log.AppendLine("  picture Logs/Fix10/" + name + ".png");
    }
}
