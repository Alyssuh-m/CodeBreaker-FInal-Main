using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CodeBreaker.Level2;

// Code Breaker > Realism Pass: scene-side fixes for the realism / collision bug list.
// Every step only repositions, re-aligns or adds to existing objects. Nothing is deleted. Meshes that
// are rebuilt (fridge body, range hood, laptop screen, dog bowl) are saved as NEW mesh assets under
// Assets/CodeBreaker/Level2/Meshes/RealismPass; the original FBX models are not modified.
// Each run writes Logs/CB_RealismPass.txt. Back up the scenes first (Fix Pass > 1. Backup Scenes).
public static class CodeBreakerRealismPass
{
    const string MeshDir = "Assets/CodeBreaker/Level2/Meshes/RealismPass";
    const string MatDir = "Assets/CodeBreaker/Level2/Materials/RealismPass";
    static StringBuilder log;

    // ================================================================== menus
    [MenuItem("Code Breaker/Realism Pass/1. Apply Level 2 Fixes", false, 610)]
    static void Level2Menu()
    {
        log = new StringBuilder("REALISM PASS  Level 2  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("hallway pictures + coat hooks", HallwayWallItems);
        Step("jewellery box", JewelleryBox);
        Step("music box mirror", MusicBoxMirror);
        Step("moon box surfaces", MoonBox);
        Step("pantry + bin", Pantry);
        Step("upper cabinet U4 corner door", CornerCabinet);
        Step("fridge interior + notes", Fridge);
        Step("range hood", RangeHood);
        Step("oven note, key, towel", Oven);
        Step("collar + mug", Collar);
        Step("laptop screen", Laptop);
        Step("dog bowl", DogBowl);
        Step("door frame colliders", Level2DoorFrames);
        Step("door-to-door swing conflicts", DoorPairs);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Realism Pass/2. Apply Level 1 Door Frame Colliders", false, 611)]
    static void Level1Menu()
    {
        log = new StringBuilder("REALISM PASS  Level 1  " + System.DateTime.Now + "\n");
        if (!Open("Level 1")) { Write(); return; }
        Step("door frame colliders", Level1DoorFrames);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Realism Pass/3. Start Screens (Level 2 + 3)", false, 612)]
    static void StartScreensMenu()
    {
        log = new StringBuilder("REALISM PASS  start screens  " + System.DateTime.Now + "\n");
        Step("start screens", StartScreens);
        Write();
    }

    // The first run of the door-pair step measured doors that touch at a seam while closed as
    // "crossing" and cut them to 0.30 (and raised one). This puts the measured limits back and runs the
    // corrected check.
    [MenuItem("Code Breaker/Realism Pass/4. Redo Door Pair Limits (Level 2)", false, 613)]
    static void RedoPairsMenu()
    {
        log = new StringBuilder("REALISM PASS  door pairs redo  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        var before = new Dictionary<string, float> {
            { "KIT_UpperCabinet_U6_Door_Left", 1f }, { "KIT_UpperCabinet_U4_Door_Left", 0.54f }, { "KIT_BaseCabinet_B5_Door_Left", 1f },
            { "KIT_BaseCabinet_Corner_Door_Left", 0.16f }, { "KIT_UpperCabinet_U6_Door_Right", 1f }, { "KIT_UpperCabinet_U5_Door_Left", 1f },
            { "KIT_BaseCabinet_B2_Door_Left", 0.72f }, { "KIT_BaseCabinet_Sink_Door_Left", 0.54f }, { "KIT_UpperCabinet_U1_Door_Left", 1f },
            { "KIT_Refrigerator_Door_Freezer", 1f }, { "KIT_BaseCabinet_B4_Door_Left", 0.48f } };
        foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
            if (before.TryGetValue(o.name, out float v)) { Undo.RecordObject(o, "restore"); o.maxOpen = v; EditorUtility.SetDirty(o); log.AppendLine("  restored " + o.name + " maxOpen " + v.ToString("F2")); }
        Step("door-to-door swing conflicts", DoorPairs);
        Save();
        Write();
    }

    // Kitchen cabinet doors turned around their BACK edge, so the thickness of the door swung into the
    // neighbouring cabinet / the neighbouring door (two doors hinged at the same seam went through each
    // other). Each kitchen door now turns around its front hinge edge like a real cabinet door, then
    // every kitchen door's opening limit is measured again and pairs are checked against each other.
    [MenuItem("Code Breaker/Realism Pass/8. Kitchen Door Hinges + Limits (Level 2)", false, 617)]
    static void HingesMenu()
    {
        log = new StringBuilder("REALISM PASS  kitchen hinges  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("front-edge hinges", KitchenHinges);
        Step("door-to-door swing conflicts", DoorPairs);
        Save();
        Write();
    }

    static void KitchenHinges()
    {
        var doors = Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include).Where(o => o.kind == L2Openable.Kind.Door && o.name.StartsWith("KIT_") && o.GetComponent<Renderer>() != null).ToList();
        foreach (L2Openable o in doors)
        {
            Bounds b = o.GetComponent<Renderer>().bounds;
            Transform body = o.transform.parent;
            Vector3 bc = body != null && body.GetComponent<Renderer>() != null ? body.GetComponent<Renderer>().bounds.center : b.center;
            int a = b.size.x < b.size.z ? 0 : 2;                         // thickness axis
            float outward = Mathf.Sign(b.center[a] - bc[a]);
            float front = outward > 0 ? b.max[a] : b.min[a];
            Vector3 off = Vector3.zero; off[a] = front - o.transform.position[a];
            Undo.RecordObject(o, "hinge");
            o.hingeOffset = off;
            o.maxOpen = 1f;
            EditorUtility.SetDirty(o);
        }
        Physics.SyncTransforms();
        foreach (L2Openable o in doors) Remeasure(o);
        log.AppendLine("  " + doors.Count + " kitchen doors now hinge on their front edge");
    }

    // Blind corners: the corner cabinet's door reached past the front of the side cabinet's door, so the
    // two doors were inside each other even when closed. The corner door now ends just before the side
    // door's front face (the part of the corner cabinet behind the side cabinet stays hidden, as in a
    // real "blind corner" cabinet). Then the limits are measured again.
    [MenuItem("Code Breaker/Realism Pass/9. Blind Corner Doors (Level 2)", false, 618)]
    static void BlindCornerMenu()
    {
        log = new StringBuilder("REALISM PASS  blind corners  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("blind corner doors", () =>
        {
            foreach (var (dn, bn) in new[] { ("KIT_UpperCabinet_U4_Door_Left", "KIT_UpperCabinet_U6_Door_Left"), ("KIT_BaseCabinet_Corner_Door_Left", "KIT_BaseCabinet_B5_Door_Left") })
            {
                Transform d = Find(dn), bl = Find(bn);
                if (d == null || bl == null) { log.AppendLine("  missing " + dn + " / " + bn); continue; }
                Bounds db = d.GetComponent<Renderer>().bounds, bb = bl.GetComponent<Renderer>().bounds;
                float hinge = d.position.x, end = bb.min.x - 0.04f, width = db.max.x - hinge;
                if (db.max.x > end + 0.001f)
                {
                    Undo.RecordObject(d, "blind corner");
                    Vector3 sc = d.localScale; sc.x *= (end - hinge) / width; d.localScale = sc;
                    log.AppendLine("  " + dn + " width " + width.ToString("F2") + " -> " + (end - hinge).ToString("F2") + " (ends before " + bn + ")");
                }
                else log.AppendLine("  " + dn + " already clear of " + bn);
                foreach (var o in new[] { d.GetComponent<L2Openable>(), bl.GetComponent<L2Openable>() }) { Undo.RecordObject(o, "limit"); o.maxOpen = 1f; }
            }
            Physics.SyncTransforms();
            foreach (string n in new[] { "KIT_UpperCabinet_U4_Door_Left", "KIT_UpperCabinet_U6_Door_Left", "KIT_BaseCabinet_Corner_Door_Left", "KIT_BaseCabinet_B5_Door_Left" })
            { Transform t = Find(n); if (t != null) Remeasure(t.GetComponent<L2Openable>()); }
        });
        Step("door-to-door swing conflicts", DoorPairs);
        Save();
        Write();
    }

    // The side cabinets' doors next to the corner (U6 upper, B5 base) were hinged AT the corner, so they
    // swung straight into the corner cabinet's door. Like real corner units, they are now hinged on
    // their other edge (same door, mirrored), so they open away from the corner.
    [MenuItem("Code Breaker/Realism Pass/10. Corner Side Doors Hinge Away (Level 2)", false, 619)]
    static void FlipHingeMenu()
    {
        log = new StringBuilder("REALISM PASS  corner hinges  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("flip hinges", () =>
        {
            foreach (string n in new[] { "KIT_UpperCabinet_U6_Door_Left", "KIT_BaseCabinet_B5_Door_Left" })
            {
                Transform t = Find(n);
                L2Openable o = t != null ? t.GetComponent<L2Openable>() : null;
                if (o == null) { log.AppendLine("  missing " + n); continue; }
                Bounds b = t.GetComponent<Renderer>().bounds;
                int w = b.size.x > b.size.z ? 0 : 2;                        // width axis (world)
                float e = Mathf.Sign(b.center[w] - t.position[w]);           // the door reaches this way from the hinge
                Vector3 worldW = Vector3.zero; worldW[w] = 1f;
                int li = 0; float best = 0f;
                for (int i = 0; i < 3; i++) { Vector3 ax = Vector3.zero; ax[i] = 1f; float d = Mathf.Abs(Vector3.Dot(t.rotation * ax, worldW)); if (d > best) { best = d; li = i; } }
                if (o.name.EndsWith("_Flipped")) continue;
                Undo.RecordObject(t, "flip"); Undo.RecordObject(o, "flip");
                Vector3 sc = t.localScale; sc[li] = -sc[li]; t.localScale = sc;
                Vector3 p = t.position; p[w] += e * b.size[w]; t.position = p;
                o.openAngle = -o.openAngle;
                o.maxOpen = 1f;
                EditorUtility.SetDirty(o);
                Physics.SyncTransforms();
                Bounds nb = t.GetComponent<Renderer>().bounds;
                log.AppendLine("  " + n + " hinge moved to the far edge; door still covers " + V(nb.min) + " - " + V(nb.max) + " (was " + V(b.min) + " - " + V(b.max) + "), openAngle " + o.openAngle);
            }
            foreach (string n in new[] { "KIT_UpperCabinet_U4_Door_Left", "KIT_UpperCabinet_U6_Door_Left", "KIT_UpperCabinet_U6_Door_Right", "KIT_BaseCabinet_Corner_Door_Left", "KIT_BaseCabinet_B5_Door_Left" })
            { Transform t = Find(n); if (t != null) { var o = t.GetComponent<L2Openable>(); Undo.RecordObject(o, "limit"); o.maxOpen = 1f; Remeasure(o); } }
        });
        Step("door-to-door swing conflicts", DoorPairs);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Realism Pass/11. Diagnose Door Pairs (read-only)", false, 620)]
    static void DiagPairs()
    {
        log = new StringBuilder("DIAG door pairs\n");
        foreach (var (an, bn) in new[] { ("KIT_UpperCabinet_U4_Door_Left", "KIT_UpperCabinet_U6_Door_Left"), ("KIT_BaseCabinet_B5_Door_Left", "KIT_BaseCabinet_Corner_Door_Left"), ("KIT_UpperCabinet_U6_Door_Right", "KIT_UpperCabinet_U5_Door_Left") })
        {
            var a = Find(an).GetComponent<L2Openable>(); var b = Find(bn).GetComponent<L2Openable>();
            log.AppendLine(an + " pos " + V(a.transform.position) + " lossy " + V(a.transform.lossyScale) + " hinge " + V(a.hingeOffset) + " ang " + a.openAngle);
            log.AppendLine(bn + " pos " + V(b.transform.position) + " lossy " + V(b.transform.lossyScale) + " hinge " + V(b.hingeOffset) + " ang " + b.openAngle);
            foreach (var (ka, kb) in new[] { (0f, 0f), (0.5f, 0f), (0f, 0.5f), (1f, 0f), (0f, 1f), (1f, 1f) })
            {
                Pose(a, ka, out Vector3 pa, out Quaternion ra); Pose(b, kb, out Vector3 pb, out Quaternion rb);
                MeshCollider ca = Probe(a), cb = Probe(b);
                ca.transform.SetPositionAndRotation(pa, ra); cb.transform.SetPositionAndRotation(pb, rb);
                Physics.SyncTransforms();
                bool hit = Physics.ComputePenetration(ca, pa, ra, cb, pb, rb, out Vector3 dir, out float d);
                log.AppendLine("   k " + ka + "/" + kb + " boundsA " + V(ca.bounds.min) + "-" + V(ca.bounds.max) + " boundsB " + V(cb.bounds.min) + "-" + V(cb.bounds.max) + " hit " + hit + " d " + d.ToString("F3") + " dir " + V(dir));
            }
        }
        foreach (var mc in probes.Values) if (mc != null) Object.DestroyImmediate(mc.gameObject);
        probes.Clear();
        Write();
    }

    // The corner pairs (U4 / U6 left, corner base / B5) can't both be fully open at once, but each one can
    // open fully while the other is closed. Their own limits go back to the measured ones; while playing,
    // a door that swings into another open door now stops against it (L2Openable).
    [MenuItem("Code Breaker/Realism Pass/12. Corner Pair Limits (Level 2)", false, 621)]
    static void CornerPairLimits()
    {
        log = new StringBuilder("REALISM PASS  corner pair limits  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("corner pairs", () =>
        {
            foreach (string n in new[] { "KIT_UpperCabinet_U4_Door_Left", "KIT_UpperCabinet_U6_Door_Left", "KIT_BaseCabinet_Corner_Door_Left", "KIT_BaseCabinet_B5_Door_Left" })
            { Transform t = Find(n); if (t != null) { var o = t.GetComponent<L2Openable>(); Undo.RecordObject(o, "limit"); o.maxOpen = 1f; Remeasure(o); } }
        });
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Realism Pass/4. Redo Door Pair Limits (Level 2)", true)]
    [MenuItem("Code Breaker/Realism Pass/1. Apply Level 2 Fixes", true)]
    [MenuItem("Code Breaker/Realism Pass/2. Apply Level 1 Door Frame Colliders", true)]
    [MenuItem("Code Breaker/Realism Pass/3. Start Screens (Level 2 + 3)", true)]
    static bool NotPlaying() => !Application.isPlaying;

    // ================================================================== helpers
    static void Step(string name, System.Action a)
    {
        log.AppendLine("---- " + name);
        try { a(); }
        catch (System.Exception e) { log.AppendLine("ERROR in " + name + ": " + e); }
    }

    static bool Open(string scene)
    {
        if (SceneManager.GetActiveScene().name == scene) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        string path = EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == scene);
        if (path == null) { log.AppendLine("ERROR scene not found " + scene); return false; }
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    static void Save()
    {
        Scene s = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(s);
        log.AppendLine(EditorSceneManager.SaveScene(s) ? "saved " + s.path : "ERROR could not save " + s.path);
        AssetDatabase.SaveAssets();
    }

    static void Write()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_RealismPass.txt", log.ToString());
        Debug.Log("[Realism Pass] " + log.ToString().Split('\n').Length + " lines -> Logs/CB_RealismPass.txt");
    }

    static Transform Find(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = FindIn(root.transform, name);
            if (t != null) return t;
        }
        return null;
    }

    static Transform FindIn(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { Transform r = FindIn(c, name); if (r != null) return r; }
        return null;
    }

    static string P(Transform t) => CodeBreakerFixPass.PathOf(t);
    static string V(Vector3 v) => v.ToString("F3");

    static Bounds RendererBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void Move(Transform t, Vector3 delta, string why)
    {
        if (delta.sqrMagnitude < 1e-8f) { log.AppendLine("  " + P(t) + " already fine (" + why + ")"); return; }
        Undo.RecordObject(t, why);
        t.position += delta;
        log.AppendLine("  moved " + P(t) + " by " + V(delta) + "  (" + why + ")");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        EnsureFolder(MeshDir);
        string path = MeshDir + "/" + name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(m, existing); existing.name = name; EditorUtility.SetDirty(existing); return existing; }
        m.name = name;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material Mat(string name, Color c, float smooth = 0.3f, Material copyFrom = null)
    {
        EnsureFolder(MatDir);
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = copyFrom != null ? new Material(copyFrom) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        if (copyFrom == null)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        }
        m.name = name;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SceneMat(string name)
    {
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name == name) return m;
        return null;
    }

    // A primitive without a collider, placed in WORLD space and then parented (keeps world pose).
    static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 worldPos, Quaternion worldRot, Vector3 worldSize, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name;
        g.transform.SetPositionAndRotation(worldPos, worldRot);
        g.transform.localScale = worldSize;
        if (parent != null) g.transform.SetParent(parent, true);
        if (m != null) g.GetComponent<MeshRenderer>().sharedMaterial = m;
        Undo.RegisterCreatedObjectUndo(g, name);
        return g.transform;
    }

    static Transform Existing(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) { Undo.DestroyObjectImmediate(t.gameObject); }   // rebuilt by this pass on a re-run
        return null;
    }

    // Box made of 6 outward faces, appended to a mesh being built.
    static void AddBox(List<Vector3> v, List<int> tri, List<Vector3> n, List<Vector2> uv, Vector3 min, Vector3 max)
    {
        Vector3[] c = {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z) };
        void Face(int a, int b, int cc, int d, Vector3 nn)
        {
            int s = v.Count;
            v.Add(c[a]); v.Add(c[b]); v.Add(c[cc]); v.Add(c[d]);
            for (int i = 0; i < 4; i++) n.Add(nn);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3);
        }
        Face(0, 3, 2, 1, Vector3.back);    // -z
        Face(5, 6, 7, 4, Vector3.forward); // +z
        Face(4, 7, 3, 0, Vector3.left);    // -x
        Face(1, 2, 6, 5, Vector3.right);   // +x
        Face(3, 7, 6, 2, Vector3.up);      // +y
        Face(4, 0, 1, 5, Vector3.down);    // -y
    }

    // ================================================================== 1. wall items
    // The hallways were widened (walls moved outwards), the pictures and hooks stayed where the old
    // wall was: they float ~0.85 units in front of the new wall. Each is pushed straight back onto
    // the wall surface behind it (found with a ray), keeping height and sideways position.
    static void HallwayWallItems()
    {
        foreach (string n in new[] { "HALL_Photo_A", "HALL_Photo_B", "HALL_CoatHooks", "HALL_Painting_Corridor" })
        {
            Transform t = Find(n);
            if (t == null) { log.AppendLine("  not found " + n); continue; }
            Bounds b = RendererBounds(t);
            int axis = b.size.x <= b.size.z ? 0 : 2;              // the thin horizontal axis = wall normal
            float half = b.extents[axis];
            float best = float.MaxValue; Vector3 bestDir = Vector3.zero; string hitName = null;
            foreach (float sgn in new[] { 1f, -1f })
            {
                Vector3 dir = Vector3.zero; dir[axis] = sgn;
                // several rays across the item; the wall is the nearest solid thing behind it
                foreach (Vector3 off in new[] { Vector3.zero, Vector3.up * b.extents.y * 0.6f, -Vector3.up * b.extents.y * 0.6f })
                {
                    if (Physics.Raycast(b.center + off, dir, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore) &&
                        (h.collider.name.Contains("Wall") || h.collider.name.Contains("Jamb")))
                    {
                        float d = h.distance - half;
                        if (d < best) { best = d; bestDir = dir; hitName = h.collider.name; }
                    }
                }
            }
            if (bestDir == Vector3.zero) { log.AppendLine("  no wall found behind " + n); continue; }
            Move(t, bestDir * (best - 0.004f), "flush on " + hitName);
        }
    }

    // ================================================================== 2. jewellery box
    // The 5 drawers were only flat front plates on a solid block: opened, they looked like a floating
    // handle. Each drawer now gets a real drawer box (sides, bottom, back) that slides with its front,
    // and a dark opening behind it so the empty slot reads as a hole. The box is settled flat on the
    // chest and kept clear of the wall behind it.
    static void JewelleryBox()
    {
        L2JewelleryBox jb = Object.FindAnyObjectByType<L2JewelleryBox>(FindObjectsInactive.Include);
        if (jb == null) { log.AppendLine("  no L2JewelleryBox"); return; }
        Transform root = jb.transform;
        Transform body = root.Find("Body");
        if (body == null) { log.AppendLine("  no Body"); return; }

        // settle on the surface below and pull away from the wall behind (back = away from the drawers)
        Bounds bb = body.GetComponent<Renderer>().bounds;
        Vector3 outDir = jb.slide.normalized;                        // drawers slide out this way
        Vector3 backDir = -outDir;
        float lift = 0f;
        var ownCols = root.GetComponentsInChildren<Collider>();
        foreach (Vector3 o in new[] { new Vector3(0, 0, 0) })
        {
            Vector3 from = bb.center + Vector3.up * 0.05f;
            var hits = Physics.RaycastAll(from, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore).Where(h => !ownCols.Contains(h.collider)).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) lift = hits[0].point.y - bb.min.y;
        }
        if (Mathf.Abs(lift) > 0.002f && Mathf.Abs(lift) < 0.5f) Move(root, Vector3.up * lift, "sit flat on the chest");
        bb = body.GetComponent<Renderer>().bounds;
        if (Physics.Raycast(bb.center, backDir, out RaycastHit wall, 2f, ~0, QueryTriggerInteraction.Ignore) && !ownCols.Contains(wall.collider))
        {
            float gap = wall.distance - Vector3.Scale(bb.extents, new Vector3(Mathf.Abs(backDir.x), 0, Mathf.Abs(backDir.z))).magnitude;
            if (gap < 0.02f) Move(root, outDir * (0.02f - gap), "back clear of " + wall.collider.name);
            else log.AppendLine("  back of the box is " + gap.ToString("F3") + " from " + wall.collider.name);
        }
        else log.AppendLine("  nothing directly behind the box");

        Material wood = SceneMat("L2P_Wood_Light") ?? body.GetComponent<Renderer>().sharedMaterial;
        Material dark = Mat("RP_DrawerCavity", new Color(0.09f, 0.06f, 0.045f), 0.05f);
        Material felt = SceneMat("L2P_Navy_Lacquer") ?? Mat("RP_Felt", new Color(0.12f, 0.14f, 0.3f), 0.1f);
        bb = body.GetComponent<Renderer>().bounds;
        float faceOut = Vector3.Dot(bb.center, outDir) + Mathf.Abs(Vector3.Dot(bb.extents, new Vector3(Mathf.Abs(outDir.x), Mathf.Abs(outDir.y), Mathf.Abs(outDir.z))));
        Vector3 side = Vector3.Cross(Vector3.up, outDir).normalized;     // across the drawer
        Quaternion rot = Quaternion.LookRotation(outDir, Vector3.up);    // z = out, x = side

        for (int i = 0; i < jb.drawers.Length; i++)
        {
            Transform front = jb.drawers[i];
            if (front == null) continue;
            Existing(front, "RP_DrawerBox");
            Bounds fb = front.GetComponent<Renderer>().bounds;
            float w = Mathf.Abs(Vector3.Dot(fb.size, side)) - 0.03f;
            float h = fb.size.y - 0.025f;
            float depth = Mathf.Max(0.3f, jb.slide.magnitude + 0.06f);
            float t = 0.012f;
            Vector3 backOfFront = fb.center - outDir * (Mathf.Abs(Vector3.Dot(fb.size, outDir)) * 0.5f);
            var holder = new GameObject("RP_DrawerBox").transform;
            holder.SetPositionAndRotation(backOfFront, rot);
            holder.SetParent(front, true);
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "drawer box");
            Vector3 c = backOfFront - outDir * depth * 0.5f;
            bool tray = jb.keyTray != null && i == jb.keyDrawer - 1;
            if (!tray) Prim(PrimitiveType.Cube, "Bottom", holder, c + Vector3.down * (h * 0.5f - t * 0.5f), rot, new Vector3(w, t, depth), wood);
            Prim(PrimitiveType.Cube, "SideA", holder, c + side * (w * 0.5f - t * 0.5f), rot, new Vector3(t, h, depth), wood);
            Prim(PrimitiveType.Cube, "SideB", holder, c - side * (w * 0.5f - t * 0.5f), rot, new Vector3(t, h, depth), wood);
            Prim(PrimitiveType.Cube, "Back", holder, backOfFront - outDir * (depth - t * 0.5f), rot, new Vector3(w, h, t), wood);
            if (!tray) Prim(PrimitiveType.Cube, "Lining", holder, c + Vector3.down * (h * 0.5f - t - 0.002f), rot, new Vector3(w - 2 * t, 0.004f, depth - 2 * t), felt);

            // dark opening on the body behind this drawer (hidden inside the front plate when closed)
            Transform old = root.Find("RP_Opening_" + (i + 1));
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Vector3 openPos = new Vector3(fb.center.x, fb.center.y, fb.center.z);
            openPos += outDir * (faceOut - Vector3.Dot(openPos, outDir) + 0.003f);
            Transform op = Prim(PrimitiveType.Cube, "RP_Opening_" + (i + 1), root, openPos, rot, new Vector3(w + 0.012f, h + 0.008f, 0.004f), dark);
            op.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            log.AppendLine("  drawer " + (i + 1) + ": box " + w.ToString("F2") + " x " + h.ToString("F2") + " x " + depth.ToString("F2") + " added");
        }
    }

    // ================================================================== 3. music box
    // The mirror on the inside of the lid was not lying on the lid: it stuck out at another angle
    // through the lid (the odd blue shape). It is fitted flat onto the lid's inner face, so it now
    // turns with the lid exactly.
    static void MusicBoxMirror()
    {
        L2MusicBox mb = Object.FindAnyObjectByType<L2MusicBox>(FindObjectsInactive.Include);
        if (mb == null || mb.lid == null || mb.lidMirror == null) { log.AppendLine("  music box parts missing"); return; }
        Transform lid = mb.lid, mir = mb.lidMirror;
        Vector3 ls = lid.lossyScale;
        // thinnest axis of the lid cube = its face normal
        Vector3[] axes = { lid.right, lid.up, lid.forward };
        float[] size = { Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z) };
        int th = size[0] <= size[1] && size[0] <= size[2] ? 0 : size[1] <= size[2] ? 1 : 2;
        int a1 = (th + 1) % 3, a2 = (th + 2) % 3;
        Vector3 boxCenter = mb.platform != null ? mb.platform.position : mb.transform.position;
        Vector3 n = axes[th];
        if (Vector3.Dot(boxCenter - lid.position, n) < 0f) n = -n;                // inner face = towards the box
        Undo.RecordObject(mir, "mirror");
        mir.position = lid.position + n * (size[th] * 0.5f + 0.003f);
        // a Quad shows its face along -forward: forward must point INTO the lid
        mir.rotation = Quaternion.LookRotation(-n, axes[a1]);
        Vector3 pls = mir.parent != null ? Abs(mir.parent.lossyScale) : Vector3.one;
        // quad width along whichever lid edge its right axis follows, 82 % of the lid
        bool rightOnA1 = Mathf.Abs(Vector3.Dot(mir.right, axes[a1])) > 0.5f;
        float wq = (rightOnA1 ? size[a1] : size[a2]) * 0.82f, hq = (rightOnA1 ? size[a2] : size[a1]) * 0.82f;
        mir.localScale = new Vector3(wq / pls.x, hq / pls.y, 1f);
        log.AppendLine("  mirror fitted on the lid's inner face at " + V(mir.position) + " size " + wq.ToString("F2") + " x " + hq.ToString("F2"));
    }

    // ================================================================== 4. moon box
    // The lock plate and the lid picture were exactly on the box surface (z-fighting: the blue box
    // showed through them in stripes). Both are lifted a hair off the surface.
    static void MoonBox()
    {
        L2MoonBox mb = Object.FindAnyObjectByType<L2MoonBox>(FindObjectsInactive.Include);
        if (mb == null) { log.AppendLine("  no moon box"); return; }
        Transform box = mb.transform.Find("Box");
        if (box == null) return;
        Bounds bb = box.GetComponent<Renderer>().bounds;
        foreach (string n in new[] { "DirectionLock", "MoonLid" })
        {
            Transform q = mb.transform.Find(n);
            if (q == null) continue;
            Vector3 nrm = -q.forward;                                     // a Quad faces -forward
            if (Vector3.Dot(q.position - bb.center, nrm) < 0f) nrm = -nrm;
            Vector3 c = q.GetComponent<Renderer>().bounds.center;
            float surface = Mathf.Abs(Vector3.Dot(bb.extents, new Vector3(Mathf.Abs(nrm.x), Mathf.Abs(nrm.y), Mathf.Abs(nrm.z))));
            float dist = Vector3.Dot(c - bb.center, nrm) - surface;
            Move(q, nrm * (0.004f - dist), "off the box surface");
        }
    }

    // ================================================================== 5. pantry
    // The pantry stood right next to the wall shelf, so its left door could only open ~46 degrees.
    // The pantry (with its collider and plate stack) and the bin next to it move 1.0 to the right,
    // then both pantry doors get their opening limit measured again.
    static void Pantry()
    {
        Transform pantry = Find("KIT_Pantry"), pc = Find("Collider_KIT_Pantry"), bin = Find("KIT_TrashBin"), bc = Find("Collider_KIT_TrashBin");
        if (pantry == null) { log.AppendLine("  no pantry"); return; }
        Vector3 d = new Vector3(1.0f, 0f, 0f);
        if (pantry.position.x > -7.5f) { log.AppendLine("  pantry already moved"); }
        else
        {
            if (bin != null) Move(bin, d, "make room for the pantry");
            if (bc != null) Move(bc, d, "bin collider");
            Move(pantry, d, "away from the wall shelf");
            if (pc != null) Move(pc, d, "pantry collider");
            // hiding spots / items inside the pantry move with it (they are children or attached)
            foreach (L2HidingSpot s in Object.FindObjectsByType<L2HidingSpot>(FindObjectsInactive.Include))
                if (s.container != null && s.container.transform.IsChildOf(pantry) && !s.transform.IsChildOf(pantry)) Move(s.transform, d, "hiding spot in the pantry");
        }
        Physics.SyncTransforms();
        foreach (L2Openable o in pantry.GetComponentsInChildren<L2Openable>(true)) Remeasure(o);
    }

    // FixPass sweep (mesh-accurate) for one part's opening limit
    static object obstacles;
    static void Remeasure(L2Openable o)
    {
        var t = typeof(CodeBreakerFixPass);
        obstacles = t.GetMethod("CollectObstacles", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var m = t.GetMethod("OpenLimit", BindingFlags.NonPublic | BindingFlags.Static);
        object[] args = { o, obstacles, null };
        float lim = (float)m.Invoke(null, args);
        float v = lim >= 0.999f ? 1f : Mathf.Floor(lim * 100f) / 100f;
        Undo.RecordObject(o, "open limit");
        log.AppendLine("  " + P(o.transform) + " maxOpen " + o.maxOpen.ToString("F2") + " -> " + v.ToString("F2") + (args[2] != null ? " (stops at " + args[2] + ")" : ""));
        o.maxOpen = v;
        EditorUtility.SetDirty(o);
    }

    // ================================================================== 6. corner cabinet
    // Upper cabinet U4's single wide door ran into the corner and through the side cabinet U6 (part of
    // the door was inside U6 even when closed, and it hit U6 when opened). The door now only covers
    // the part of U4 that is in front, ending at U6 (a normal "blind corner"); the hidden corner part
    // of U4 stays behind U6. Then its opening limit is measured again.
    static void CornerCabinet()
    {
        Transform door = Find("KIT_UpperCabinet_U4_Door_Left"), u6 = Find("KIT_UpperCabinet_U6");
        if (door == null || u6 == null) { log.AppendLine("  parts not found"); return; }
        Bounds db = door.GetComponent<Renderer>().bounds, u6b = u6.GetComponent<Renderer>().bounds;
        float hinge = door.position.x;
        float limit = u6b.min.x - 0.04f;                      // U6 front face
        float width = db.max.x - hinge;
        if (db.max.x <= limit + 0.001f) { log.AppendLine("  U4 door already clear of U6"); }
        else
        {
            float f = (limit - hinge) / width;
            Undo.RecordObject(door, "U4 door");
            Vector3 s = door.localScale; s.x *= f; door.localScale = s;
            log.AppendLine("  U4 door width " + width.ToString("F2") + " -> " + (limit - hinge).ToString("F2") + " (ends at U6's front face)");
        }
        Physics.SyncTransforms();
        Remeasure(door.GetComponent<L2Openable>());
    }

    // ================================================================== 7. fridge
    // The fridge body was a solid block: opening a door showed only a flat panel. The body mesh is
    // replaced by a hollow cabinet of the same size and material (open at the front, freezer and
    // fridge compartments), with a white lining, glass shelves and a few groceries inside.
    // The fridge's own sticky notes were sunk inside the door (and reached into the freezer door):
    // they are moved onto the front of the fridge door.
    static void Fridge()
    {
        Transform fr = Find("KIT_Refrigerator");
        if (fr == null) { log.AppendLine("  no fridge"); return; }
        Transform main = fr.Find("KIT_Refrigerator_Door_Main"), frz = fr.Find("KIT_Refrigerator_Door_Freezer");
        MeshFilter mf = fr.GetComponent<MeshFilter>();
        Bounds lb = mf.sharedMesh.name.StartsWith("RP_") ? mf.sharedMesh.bounds : mf.sharedMesh.bounds;
        // local frame: find which local axis points to the doors (front) and which is "up"
        Vector3 toDoor = fr.InverseTransformDirection((main.GetComponent<Renderer>().bounds.center - fr.GetComponent<Renderer>().bounds.center).normalized);
        int fa = Mathf.Abs(toDoor.x) > Mathf.Abs(toDoor.y) ? (Mathf.Abs(toDoor.x) > Mathf.Abs(toDoor.z) ? 0 : 2) : (Mathf.Abs(toDoor.y) > Mathf.Abs(toDoor.z) ? 1 : 2);
        Vector3 upL = fr.InverseTransformDirection(Vector3.up);
        int ua = Mathf.Abs(upL.x) > Mathf.Abs(upL.y) ? (Mathf.Abs(upL.x) > Mathf.Abs(upL.z) ? 0 : 2) : (Mathf.Abs(upL.y) > Mathf.Abs(upL.z) ? 1 : 2);
        int wa = 3 - fa - ua;
        float fs = Mathf.Sign(toDoor[fa]), us = Mathf.Sign(upL[ua]);
        Vector3 mn = lb.min, mx = lb.max;
        float scale = Mathf.Abs(fr.lossyScale.x);
        float t = 0.09f / scale;                                // wall thickness ~9 cm
        // divider height between the doors (world) -> local
        float divW = (main.GetComponent<Renderer>().bounds.max.y + frz.GetComponent<Renderer>().bounds.min.y) * 0.5f;
        float divL = fr.InverseTransformPoint(new Vector3(0, divW, 0) + Vector3.ProjectOnPlane(fr.position, Vector3.up)).y;
        divL = fr.InverseTransformPoint(new Vector3(fr.position.x, divW, fr.position.z))[ua];

        Vector3 Box(float a0, float a1, float u0, float u1, float w0, float w1, out Vector3 max)
        {
            Vector3 p = Vector3.zero, q = Vector3.zero;
            p[fa] = a0; q[fa] = a1; p[ua] = u0; q[ua] = u1; p[wa] = w0; q[wa] = w1;
            max = Vector3.Max(p, q);
            return Vector3.Min(p, q);
        }
        // front / back along fa
        float front = fs > 0 ? mx[fa] : mn[fa], back = fs > 0 ? mn[fa] : mx[fa];
        float inward = -fs;                                           // from the front towards the back
        float bottom = us > 0 ? mn[ua] : mx[ua], top = us > 0 ? mx[ua] : mn[ua];
        float upS = us;
        var v = new List<Vector3>(); var tri = new List<int>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
        void B(float a0, float a1, float u0, float u1, float w0, float w1) { Vector3 lo = Box(a0, a1, u0, u1, w0, w1, out Vector3 hi); AddBox(v, tri, nrm, uv, lo, hi); }
        B(back, back + fs * t, bottom, top, mn[wa], mx[wa]);                                   // back panel
        B(front, back, bottom, bottom + upS * t, mn[wa], mx[wa]);                                    // floor
        B(front, back, top, top - upS * t, mn[wa], mx[wa]);                                          // roof
        B(front, back, bottom, top, mn[wa], mn[wa] + t);                                             // side A
        B(front, back, bottom, top, mx[wa] - t, mx[wa]);                                             // side B
        B(front, back, divL - upS * t * 0.5f, divL + upS * t * 0.5f, mn[wa], mx[wa]);              // divider
        var hollow = new Mesh();
        hollow.SetVertices(v); hollow.SetNormals(nrm); hollow.SetUVs(0, uv); hollow.SetTriangles(tri, 0);
        hollow.RecalculateBounds(); hollow.RecalculateTangents();
        Undo.RecordObject(mf, "fridge mesh");
        if (!mf.sharedMesh.name.StartsWith("RP_")) log.AppendLine("  fridge body mesh " + mf.sharedMesh.name + " -> RP_Fridge_Hollow (same size and material)");
        mf.sharedMesh = SaveMesh(hollow, "RP_Fridge_Hollow");

        // interior: lining, shelves, groceries (children of the body, rebuilt on a re-run)
        Transform old = fr.Find("RP_Interior"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var interior = new GameObject("RP_Interior").transform;
        interior.SetParent(fr, false);
        Undo.RegisterCreatedObjectUndo(interior.gameObject, "fridge interior");
        Material white = Mat("RP_FridgeLining", new Color(0.93f, 0.95f, 0.96f), 0.6f);
        Material glass = Mat("RP_FridgeShelf", new Color(0.8f, 0.9f, 0.95f), 0.85f);
        Bounds wb = fr.GetComponent<Renderer>().bounds;
        Vector3 fwdW = fr.TransformDirection(Vector3.zero.With(fa, fs)).normalized;     // world out of the fridge
        Vector3 sideW = Vector3.Cross(Vector3.up, fwdW).normalized;
        float tw = t * scale;
        float frontW = Vector3.Dot(wb.center, fwdW) + Mathf.Abs(Vector3.Dot(wb.extents, Abs(fwdW)));
        float depthW = wb.size[Axis(fwdW)] - tw - 0.02f;
        float widthW = wb.size[Axis(sideW)] - 2 * tw - 0.01f;
        float floorY = wb.min.y + tw, roofY = wb.max.y - tw;
        Quaternion rot = Quaternion.LookRotation(fwdW, Vector3.up);
        Vector3 centerXZ = new Vector3(wb.center.x, 0, wb.center.z);
        Vector3 backPlane = centerXZ - fwdW * (Vector3.Dot(centerXZ, fwdW) - frontW) - fwdW * (depthW + 0.005f);
        Vector3 Mid(float y, float fromFront) => new Vector3(wb.center.x, y, wb.center.z) + fwdW * (frontW - Vector3.Dot(wb.center, fwdW) - fromFront);
        float midDepth = depthW * 0.5f;
        // lining panels (thin, just inside the walls)
        Prim(PrimitiveType.Cube, "Lining_Back", interior, Mid((floorY + roofY) * 0.5f, depthW + 0.002f), rot, new Vector3(widthW, roofY - floorY, 0.01f), white);
        Prim(PrimitiveType.Cube, "Lining_SideA", interior, Mid((floorY + roofY) * 0.5f, midDepth) + sideW * (widthW * 0.5f + 0.003f), rot, new Vector3(0.01f, roofY - floorY, depthW), white);
        Prim(PrimitiveType.Cube, "Lining_SideB", interior, Mid((floorY + roofY) * 0.5f, midDepth) - sideW * (widthW * 0.5f + 0.003f), rot, new Vector3(0.01f, roofY - floorY, depthW), white);
        Prim(PrimitiveType.Cube, "Lining_Floor", interior, Mid(floorY + 0.006f, midDepth), rot, new Vector3(widthW, 0.01f, depthW), white);
        Prim(PrimitiveType.Cube, "Lining_Roof", interior, Mid(roofY - 0.006f, midDepth), rot, new Vector3(widthW, 0.01f, depthW), white);
        Prim(PrimitiveType.Cube, "Lining_Divider", interior, Mid(divW, midDepth), rot, new Vector3(widthW, tw + 0.02f, depthW), white);
        // glass shelves in the fridge part
        float y1 = floorY + (divW - floorY) * 0.36f, y2 = floorY + (divW - floorY) * 0.68f;
        Prim(PrimitiveType.Cube, "Shelf_1", interior, Mid(y1, midDepth), rot, new Vector3(widthW - 0.02f, 0.03f, depthW - 0.05f), glass);
        Prim(PrimitiveType.Cube, "Shelf_2", interior, Mid(y2, midDepth), rot, new Vector3(widthW - 0.02f, 0.03f, depthW - 0.05f), glass);
        // groceries
        Material milk = SceneMat("L2_Plastic_White") ?? white;
        Material red = SceneMat("L2P_Plastic_Red") ?? Mat("RP_Red", new Color(0.7f, 0.12f, 0.1f));
        Material green = SceneMat("L2P_Plastic_Green") ?? Mat("RP_Green", new Color(0.2f, 0.5f, 0.2f));
        Material yellow = SceneMat("L2P_Plastic_Yellow") ?? Mat("RP_Yellow", new Color(0.9f, 0.75f, 0.2f));
        Prim(PrimitiveType.Cube, "Milk", interior, Mid(floorY + 0.4f, depthW * 0.45f) + sideW * widthW * 0.28f, rot, new Vector3(0.32f, 0.78f, 0.32f), milk);
        Prim(PrimitiveType.Cylinder, "Bottle", interior, Mid(floorY + 0.45f, depthW * 0.5f) - sideW * widthW * 0.25f, rot, new Vector3(0.22f, 0.45f, 0.22f), green);
        Prim(PrimitiveType.Cube, "Cheese", interior, Mid(y1 + 0.13f, depthW * 0.55f) + sideW * widthW * 0.15f, rot, new Vector3(0.45f, 0.22f, 0.3f), yellow);
        Prim(PrimitiveType.Sphere, "Apple_1", interior, Mid(y1 + 0.13f, depthW * 0.4f) - sideW * widthW * 0.2f, rot, Vector3.one * 0.24f, red);
        Prim(PrimitiveType.Sphere, "Apple_2", interior, Mid(y1 + 0.13f, depthW * 0.55f) - sideW * widthW * 0.05f, rot, Vector3.one * 0.24f, red);
        Prim(PrimitiveType.Cylinder, "Jar", interior, Mid(y2 + 0.17f, depthW * 0.5f) + sideW * widthW * 0.2f, rot, new Vector3(0.26f, 0.16f, 0.26f), glass);
        Prim(PrimitiveType.Cube, "IceTray", interior, Mid(divW + tw + 0.08f, depthW * 0.5f), rot, new Vector3(widthW * 0.6f, 0.12f, depthW * 0.6f), white);
        log.AppendLine("  fridge interior built (lining, 2 shelves, groceries)");

        // the fridge's own sticky notes: onto the front of the main door, below the freezer door
        Transform notes = main != null ? main.Find("KIT_FridgeNotes") : null;
        if (notes != null)
        {
            Bounds nb = notes.GetComponent<Renderer>().bounds, mdb = main.GetComponent<Renderer>().bounds;
            Vector3 d = Vector3.zero;
            float doorFront = Vector3.Dot(mdb.center, fwdW) + Mathf.Abs(Vector3.Dot(mdb.extents, Abs(fwdW)));
            float notesBack = Vector3.Dot(nb.center, fwdW) - Mathf.Abs(Vector3.Dot(nb.extents, Abs(fwdW)));
            d += fwdW * (doorFront - notesBack + 0.002f);
            float topLimit = mdb.max.y - 0.15f;
            if (nb.max.y > topLimit) d += Vector3.up * (topLimit - nb.max.y);
            Move(notes, d, "notes onto the front of the fridge door");
        }
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static int Axis(Vector3 d) { d = Abs(d); return d.x >= d.y && d.x >= d.z ? 0 : d.y >= d.z ? 1 : 2; }
    static Vector3 With(this Vector3 v, int i, float x) { v[i] = x; return v; }

    // ================================================================== 8. range hood
    // The hood ("water heater" above the stove) had missing and inside-out faces and touched the
    // cabinets on both sides. It is rebuilt as a closed canopy + chimney of the same height and
    // finish, a little narrower so it sits cleanly between the two cabinets.
    static void RangeHood()
    {
        Transform hood = Find("KIT_RangeHood");
        if (hood == null) { log.AppendLine("  no hood"); return; }
        MeshFilter mf = hood.GetComponent<MeshFilter>();
        MeshRenderer mr = hood.GetComponent<MeshRenderer>();
        Bounds wb = mr.bounds;
        if (mf.sharedMesh.name.StartsWith("RP_")) wb = mr.bounds;
        // neighbours left / right limit the width
        float left = wb.min.x, right = wb.max.x;
        foreach (string n in new[] { "KIT_UpperCabinet_U2", "KIT_UpperCabinet_U3" })
        {
            Transform c = Find(n); if (c == null) continue;
            Bounds cb = c.GetComponent<Renderer>().bounds;
            if (cb.center.x < wb.center.x) left = Mathf.Max(left, cb.max.x); else right = Mathf.Min(right, cb.min.x);
        }
        float width = (right - left) - 0.12f, cx = (left + right) * 0.5f;
        float backZ = wb.max.z, depth = wb.size.z;                          // against the back wall (+z)
        float yBottom = wb.min.y, yTop = wb.max.y;
        float canopyH = 0.55f, chimneyW = 0.9f, chimneyD = 0.75f;
        // build in WORLD space, then convert to the hood's local space
        var v = new List<Vector3>(); var tri = new List<int>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
        AddBox(v, tri, nrm, uv, new Vector3(cx - width / 2, yBottom, backZ - depth), new Vector3(cx + width / 2, yBottom + 0.12f, backZ));            // lip
        // sloped canopy: a frustum from the lip up to the chimney
        AddFrustum(v, tri, nrm, uv, new Vector3(cx, yBottom + 0.12f, backZ - depth / 2), width, depth,
                   new Vector3(cx, yBottom + canopyH, backZ - chimneyD / 2), chimneyW, chimneyD);
        AddBox(v, tri, nrm, uv, new Vector3(cx - chimneyW / 2, yBottom + canopyH, backZ - chimneyD), new Vector3(cx + chimneyW / 2, yTop, backZ));      // chimney
        Matrix4x4 w2l = hood.worldToLocalMatrix;
        for (int i = 0; i < v.Count; i++) { v[i] = w2l.MultiplyPoint3x4(v[i]); nrm[i] = w2l.MultiplyVector(nrm[i]).normalized; }
        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        Undo.RecordObject(mf, "hood"); Undo.RecordObject(mr, "hood");
        mf.sharedMesh = SaveMesh(m, "RP_RangeHood");
        Material steel = SceneMat("L2_Metal_StainlessSteel");
        if (steel != null) mr.sharedMaterial = steel;
        mr.shadowCastingMode = ShadowCastingMode.On;
        log.AppendLine("  hood rebuilt: width " + width.ToString("F2") + " between the cabinets, closed faces, " + (yTop - yBottom).ToString("F2") + " high");
    }

    static void AddFrustum(List<Vector3> v, List<int> tri, List<Vector3> n, List<Vector2> uv, Vector3 c0, float w0, float d0, Vector3 c1, float w1, float d1)
    {
        Vector3[] b = { c0 + new Vector3(-w0 / 2, 0, -d0 / 2), c0 + new Vector3(w0 / 2, 0, -d0 / 2), c0 + new Vector3(w0 / 2, 0, d0 / 2), c0 + new Vector3(-w0 / 2, 0, d0 / 2) };
        Vector3[] t = { c1 + new Vector3(-w1 / 2, 0, -d1 / 2), c1 + new Vector3(w1 / 2, 0, -d1 / 2), c1 + new Vector3(w1 / 2, 0, d1 / 2), c1 + new Vector3(-w1 / 2, 0, d1 / 2) };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4, s = v.Count;
            Vector3 nn = Vector3.Cross(t[i] - b[i], b[j] - b[i]).normalized;
            Vector3 mid = (b[i] + b[j] + t[i] + t[j]) / 4f;
            if (Vector3.Dot(nn, mid - (c0 + c1) / 2f) < 0f) nn = -nn;
            v.Add(b[i]); v.Add(b[j]); v.Add(t[j]); v.Add(t[i]);
            for (int k = 0; k < 4; k++) n.Add(nn);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            // winding so the face points along nn
            if (Vector3.Dot(Vector3.Cross(b[j] - b[i], t[j] - b[i]), nn) > 0f) { tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); tri.Add(s); tri.Add(s + 2); tri.Add(s + 3); }
            else { tri.Add(s); tri.Add(s + 2); tri.Add(s + 1); tri.Add(s); tri.Add(s + 3); tri.Add(s + 2); }
        }
    }

    // ================================================================== 9. oven
    // Towel moves with the door; the key (buried in the solid stove body) now lies on the inside of the
    // door, which is a flat shelf once the oven is open; the note sits squared-up above it.
    static void Oven()
    {
        L2Oven oven = Object.FindAnyObjectByType<L2Oven>(FindObjectsInactive.Include);
        if (oven == null || oven.ovenDoor == null) { log.AppendLine("  no oven"); return; }
        Transform door = oven.ovenDoor;
        Bounds db = door.GetComponent<Renderer>().bounds;
        Vector3 inner = Vector3.forward;                  // the door's inside faces the oven (+z); the door opens towards -z
        float innerFace = db.max.z;

        // The towel hangs on the door handle: it now belongs to the door, so it swings down with it
        // instead of staying in the air in front of the open oven.
        Transform towel = Find("DishTowel_OvenHandle");
        if (towel != null && towel.parent != door)
        {
            Undo.SetTransformParent(towel, door, "towel on the oven door");
            log.AppendLine("  towel now hangs from the oven door (moves with it)");
        }

        // The key was at the back of the oven, inside the solid stove body: invisible even with the door
        // open. It now lies on the inside of the oven door, which becomes a flat shelf when the oven opens.
        if (oven.key != null)
        {
            Transform k = oven.key.transform;
            Undo.SetTransformParent(k, door, "key on the oven door");
            Undo.RecordObject(k, "key");
            Bounds kb = RendererBounds(k);
            // turn the key so its thinnest side lies against the door
            MeshFilter kmf = k.GetComponentInChildren<MeshFilter>();
            if (kmf != null && kmf.sharedMesh != null)
            {
                Vector3 sz = Vector3.Scale(kmf.sharedMesh.bounds.size, Abs(kmf.transform.lossyScale));
                Vector3 thinLocal = sz.x <= sz.y && sz.x <= sz.z ? Vector3.right : sz.y <= sz.z ? Vector3.up : Vector3.forward;
                Vector3 thinWorld = kmf.transform.rotation * thinLocal;
                k.rotation = Quaternion.FromToRotation(thinWorld, Vector3.Dot(thinWorld, inner) >= 0 ? inner : -inner) * k.rotation;
            }
            Physics.SyncTransforms();
            kb = RendererBounds(k);
            float len = Mathf.Max(kb.size.x, kb.size.y, kb.size.z);
            if (len > 0.01f && len < 0.5f) { k.localScale *= 0.56f / len; Physics.SyncTransforms(); kb = RendererBounds(k); }
            Vector3 target = new Vector3(db.center.x + 0.15f, db.center.y - db.size.y * 0.05f, innerFace + kb.extents.z + 0.006f);
            k.position += target - kb.center;
            log.AppendLine("  kitchen key now lies on the inside of the oven door at " + V(k.position) + " (size " + V(kb.size) + ")");
        }

        // The note stays on the inside of the door, moved to the upper part (above the key) and squared up.
        if (oven.doorNote != null)
        {
            Transform n = oven.doorNote.transform;
            Undo.RecordObject(n, "note");
            Bounds nb = n.GetComponent<Renderer>().bounds;
            n.rotation = Quaternion.LookRotation(Vector3.back, Vector3.down);   // reads the right way up once the door is down
            n.position = new Vector3(db.center.x - 0.1f, db.max.y - nb.extents.y - db.size.y * 0.12f, innerFace + 0.006f);
            log.AppendLine("  note placed in the upper part of the inside of the oven door at " + V(n.position));
        }
    }

    // ================================================================== 10. collar
    // The red collar segments were turned 90 degrees (they pointed outwards like rays) and a mug stood
    // on top of the collar. The segments now follow the ring, and the mug moves to a free spot.
    static void Collar()
    {
        Transform col = Find("Puzzle_Study_DogCollar_TagBYTE");
        if (col == null) { log.AppendLine("  no collar"); return; }
        int n = 0;
        foreach (Transform s in col)
        {
            if (!s.name.StartsWith("CollarSeg_")) continue;
            Vector3 lp = s.localPosition;
            float a = Mathf.Atan2(lp.x, lp.z) * Mathf.Rad2Deg;
            Undo.RecordObject(s, "collar");
            s.localRotation = Quaternion.Euler(0f, a, 0f);
            n++;
        }
        log.AppendLine("  " + n + " collar segments turned to follow the ring");
        Transform mug = Find("STU_SideTable_Mug");
        if (mug != null)
        {
            Bounds cb = RendererBounds(col), mb = mug.GetComponent<Renderer>().bounds;
            // the table under the mug
            if (Physics.Raycast(mb.center, Vector3.down, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                Bounds table = h.collider.bounds;
                Vector3 best = mb.center; float bestScore = -1f;
                for (int i = 0; i <= 8; i++)
                    for (int j = 0; j <= 8; j++)
                    {
                        Vector3 p = new Vector3(Mathf.Lerp(table.min.x + mb.extents.x + 0.05f, table.max.x - mb.extents.x - 0.05f, i / 8f), mb.center.y, Mathf.Lerp(table.min.z + mb.extents.z + 0.05f, table.max.z - mb.extents.z - 0.05f, j / 8f));
                        var test = new Bounds(p, mb.size);
                        if (test.Intersects(cb)) continue;
                        float score = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(cb.center.x, cb.center.z));
                        if (score > bestScore) { bestScore = score; best = p; }
                    }
                if (bestScore > 0f) Move(mug, new Vector3(best.x - mb.center.x, 0f, best.z - mb.center.z), "off the collar, on " + h.collider.name);
            }
        }
    }

    // ================================================================== 11. laptop
    // The code screen of the laptop was a separate flat panel that did not sit on the lid. The screen
    // is put back onto the inner face of the lid (same size as the lid's bezel area, same picture).
    static void Laptop()
    {
        Transform lap = Find("STU_Laptop");
        if (lap == null) { log.AppendLine("  no laptop"); return; }
        MeshFilter mf = lap.GetComponent<MeshFilter>(); MeshRenderer mr = lap.GetComponent<MeshRenderer>();
        Mesh src = mf.sharedMesh;
        int screenSub = -1;
        for (int s = 0; s < src.subMeshCount && s < mr.sharedMaterials.Length; s++) if (mr.sharedMaterials[s] != null && mr.sharedMaterials[s].name.Contains("Screen")) screenSub = s;
        if (screenSub < 0) { log.AppendLine("  no screen sub-mesh"); return; }
        Mesh m = Object.Instantiate(src);
        Vector3[] v = m.vertices;
        int[] scr = m.GetTriangles(screenSub);
        var sv = scr.Distinct().ToArray();
        Vector3 sc = Vector3.zero; foreach (int i in sv) sc += v[i]; sc /= sv.Length;
        Vector3 sn = Vector3.Cross(v[scr[1]] - v[scr[0]], v[scr[2]] - v[scr[0]]).normalized;
        // lid = the body faces most parallel to the screen, overlapping it, biggest area
        int[] bodyT = m.GetTriangles(0);
        float bestArea = 0f; Vector3 lidN = sn, lidC = sc; var lidVerts = new List<Vector3>();
        var groups = new Dictionary<int, (float area, Vector3 c, Vector3 n, List<Vector3> pts)>();
        for (int i = 0; i < bodyT.Length; i += 3)
        {
            Vector3 a = v[bodyT[i]], b = v[bodyT[i + 1]], c = v[bodyT[i + 2]];
            Vector3 cr = Vector3.Cross(b - a, c - a);
            float area = cr.magnitude * 0.5f; if (area < 1e-6f) continue;
            Vector3 n = cr.normalized;
            if (Mathf.Abs(Vector3.Dot(n, sn)) < 0.85f) continue;
            float planeD = Vector3.Dot(n, a);
            int key = Mathf.RoundToInt(Vector3.Dot(n, a) * Mathf.Sign(Vector3.Dot(n, sn)) * 400f) * 2 + (Vector3.Dot(n, sn) > 0 ? 1 : 0);
            if (!groups.TryGetValue(key, out var g)) g = (0f, Vector3.zero, n, new List<Vector3>());
            g.area += area; g.c += (a + b + c) / 3f * area; g.pts.Add(a); g.pts.Add(b); g.pts.Add(c);
            groups[key] = g;
        }
        // the lid face closest to the screen plane with a sensible size
        float bestDist = float.MaxValue;
        foreach (var g in groups.Values)
        {
            if (g.area < 0.02f) continue;
            Vector3 c = g.c / g.area;
            float d = Mathf.Abs(Vector3.Dot(c - sc, sn));
            if (d < bestDist) { bestDist = d; lidC = c; lidN = g.n; lidVerts = g.pts; bestArea = g.area; }
        }
        if (lidVerts.Count == 0) { log.AppendLine("  lid face not found"); return; }
        // lidN is the outward normal of that lid face: the screen goes just outside it
        Vector3 u = Vector3.right;                          // the laptop's width axis
        Vector3 w = Vector3.Cross(lidN, u).normalized;
        float uMin = lidVerts.Min(p => Vector3.Dot(p - lidC, u)), uMax = lidVerts.Max(p => Vector3.Dot(p - lidC, u));
        float wMin = lidVerts.Min(p => Vector3.Dot(p - lidC, w)), wMax = lidVerts.Max(p => Vector3.Dot(p - lidC, w));
        Vector3 center = lidC + u * (uMin + uMax) * 0.5f + w * (wMin + wMax) * 0.5f + lidN * 0.0015f;
        float hu = (uMax - uMin) * 0.45f, hw = (wMax - wMin) * 0.44f;
        // old corners -> new corners (keep each vertex's UV by its quadrant)
        Vector3 oldW = Vector3.Cross(sn, u).normalized;
        foreach (int i in sv)
        {
            float su = Mathf.Sign(Vector3.Dot(v[i] - sc, u)), sw = Mathf.Sign(Vector3.Dot(v[i] - sc, oldW));
            if (Vector3.Dot(oldW, w) < 0f) sw = -sw;
            v[i] = center + u * su * hu + w * sw * hw;
        }
        m.vertices = v;
        m.RecalculateBounds();
        Undo.RecordObject(mf, "laptop");
        log.AppendLine("  screen moved from plane offset " + bestDist.ToString("F4") + " (local) onto the lid face (lid area " + bestArea.ToString("F3") + ")");
        mf.sharedMesh = SaveMesh(m, "RP_STU_Laptop");
    }

    // ================================================================== 12. dog bowl
    // The bowl was a plain flat cylinder. It gets a real bowl shape (flat base, flared wall, rolled
    // rim, hollow inside) in the same steel finish, with the water inside it.
    static void DogBowl()
    {
        Transform root = Find("DogBowl");
        Transform bowl = root != null ? root.Find("Bowl") : null, water = root != null ? root.Find("Water") : null;
        if (bowl == null) { log.AppendLine("  no dog bowl"); return; }
        Bounds b = bowl.GetComponent<Renderer>().bounds;
        float R = b.extents.x, H = b.size.y;
        Vector3 baseC = new Vector3(b.center.x, b.min.y, b.center.z);
        // profile (radius, height) from the outside bottom, over the rim, down the inside
        Vector2[] prof = {
            new Vector2(0f, 0f), new Vector2(R * 0.72f, 0f), new Vector2(R * 0.8f, H * 0.08f), new Vector2(R * 0.97f, H * 0.82f),
            new Vector2(R, H * 0.93f), new Vector2(R * 0.96f, H), new Vector2(R * 0.88f, H * 0.95f), new Vector2(R * 0.84f, H * 0.82f),
            new Vector2(R * 0.66f, H * 0.3f), new Vector2(R * 0.58f, H * 0.22f), new Vector2(0f, H * 0.22f) };
        const int seg = 40;
        var v = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j < prof.Length; j++) { v.Add(baseC + dir * prof[j].x + Vector3.up * prof[j].y); uv.Add(new Vector2(i / (float)seg, j / (float)(prof.Length - 1))); }
        }
        int rowN = prof.Length;
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < rowN - 1; j++)
            {
                int a = i * rowN + j, bI = (i + 1) * rowN + j;
                tri.Add(a); tri.Add(a + 1); tri.Add(bI); tri.Add(bI); tri.Add(a + 1); tri.Add(bI + 1);
            }
        Matrix4x4 w2l = bowl.worldToLocalMatrix;
        for (int i = 0; i < v.Count; i++) v[i] = w2l.MultiplyPoint3x4(v[i]);
        var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        // the profile runs outside-in: make sure normals of the outer wall point outwards
        Vector3 testN = bowl.TransformDirection(m.normals[rowN * 0 + 3]);
        if (Vector3.Dot(testN, Vector3.right) < 0f) { for (int i = 0; i < tri.Count; i += 3) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; } m.SetTriangles(tri, 0); m.RecalculateNormals(); }
        m.RecalculateBounds(); m.RecalculateTangents();
        Undo.RecordObject(bowl.GetComponent<MeshFilter>(), "bowl");
        bowl.GetComponent<MeshFilter>().sharedMesh = SaveMesh(m, "RP_DogBowl");
        if (water != null)
        {
            Undo.RecordObject(water, "water");
            Vector3 pls = water.parent.lossyScale;
            float wr = R * 0.78f * 2f;
            water.position = baseC + Vector3.up * (H * 0.62f);
            water.localScale = new Vector3(wr / pls.x, 0.006f / pls.y, wr / pls.z);
        }
        log.AppendLine("  dog bowl shaped (radius " + R.ToString("F2") + ", height " + H.ToString("F2") + ") with water inside");
    }

    // ================================================================== 13. door frames
    static void Level2DoorFrames()
    {
        int n = 0;
        foreach (L2Door d in Object.FindObjectsByType<L2Door>(FindObjectsInactive.Include))
        {
            Transform frame = d.transform.Find("DoorFrame");
            if (frame == null) continue;
            foreach (MeshRenderer r in frame.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponent<Collider>() != null) continue;
                if (r.name.Contains("Hinge")) continue;
                var bc = Undo.AddComponent<BoxCollider>(r.gameObject);   // fits the part's mesh
                n++;
            }
        }
        log.AppendLine("  " + n + " door-frame parts got a solid collider");
    }

    static void Level1DoorFrames()
    {
        int n = 0;
        foreach (DoorInteraction d in Object.FindObjectsByType<DoorInteraction>(FindObjectsInactive.Include))
        {
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (!mf.name.ToLower().Contains("frame") || mf.GetComponent<Collider>() != null || mf.sharedMesh == null) continue;
                if (d.doorPivot != null && mf.transform.IsChildOf(d.doorPivot)) continue;
                if (d.doorPivot == null || Vector3.Distance(mf.GetComponent<Renderer>().bounds.center, d.doorPivot.position) > 8f) continue;
                var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                n++;
                log.AppendLine("  frame collider: " + P(mf.transform));
            }
            // the door leaf must be solid (it may have been left as a trigger by the old swing code)
            foreach (Collider c in d.doorPivot.GetComponentsInChildren<Collider>(true))
                if (c.isTrigger) { Undo.RecordObject(c, "leaf"); c.isTrigger = false; log.AppendLine("  leaf collider made solid: " + P(c.transform)); }
        }
        log.AppendLine("  " + n + " Level 1 door frames got a mesh collider");
    }

    // ================================================================== 14. door pairs
    // Two doors next to each other (e.g. the two hinges at the seam between U5 and U6) could each
    // open fine on their own but went through each other when both were open. Every pair of nearby
    // doors is checked in all combinations of open states; where they would cross, both are limited
    // just enough to stay apart.
    static void DoorPairs()
    {
        var doors = Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include).Where(o => o.kind == L2Openable.Kind.Door && o.GetComponent<MeshFilter>() != null).ToList();
        int fixedPairs = 0;
        for (int i = 0; i < doors.Count; i++)
            for (int j = i + 1; j < doors.Count; j++)
            {
                L2Openable a = doors[i], b = doors[j];
                if (Vector3.Distance(a.transform.position, b.transform.position) > 6f) continue;
                if (!Conflict(a, b)) continue;
                float ma = a.maxOpen, mb = b.maxOpen;
                Undo.RecordObject(a, "pair"); Undo.RecordObject(b, "pair");
                for (int k = 0; k < 60 && Conflict(a, b); k++)
                {
                    // never opens a door further than before; never below 0.3 (still searchable)
                    if (a.maxOpen > 0.3f) a.maxOpen = Mathf.Max(0.3f, a.maxOpen - 0.02f);
                    if (b.maxOpen > 0.3f) b.maxOpen = Mathf.Max(0.3f, b.maxOpen - 0.02f);
                    if (a.maxOpen <= 0.3f && b.maxOpen <= 0.3f) break;
                }
                EditorUtility.SetDirty(a); EditorUtility.SetDirty(b);
                fixedPairs++;
                log.AppendLine("  " + a.name + " " + ma.ToString("F2") + "->" + a.maxOpen.ToString("F2") + "  &  " + b.name + " " + mb.ToString("F2") + "->" + b.maxOpen.ToString("F2") + (Conflict(a, b) ? "  (still touching!)" : ""));
            }
        log.AppendLine("  " + fixedPairs + " door pairs limited");
        foreach (var mc in probes.Values) if (mc != null) Object.DestroyImmediate(mc.gameObject);
        probes.Clear();
    }

    // Mesh-accurate test: a temporary convex collider of each door's own mesh, posed at an opening state.
    static readonly Dictionary<L2Openable, MeshCollider> probes = new Dictionary<L2Openable, MeshCollider>();
    static MeshCollider Probe(L2Openable o)
    {
        if (probes.TryGetValue(o, out var mc) && mc != null) return mc;
        var g = new GameObject("CB_PairProbe") { hideFlags = HideFlags.HideAndDontSave };
        g.transform.localScale = o.transform.lossyScale;
        mc = g.AddComponent<MeshCollider>();
        mc.sharedMesh = o.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;
        probes[o] = mc;
        return mc;
    }

    static void Pose(L2Openable o, float k, out Vector3 p, out Quaternion r)
    {
        p = o.DoorPosition(o.transform.position, k);
        r = Quaternion.AngleAxis(o.openAngle * k, Vector3.up) * o.transform.rotation;
    }

    static bool Touch(L2Openable a, float ka, L2Openable b, float kb)
    {
        Pose(a, ka, out Vector3 pa, out Quaternion ra); Pose(b, kb, out Vector3 pb, out Quaternion rb);
        MeshCollider ca = Probe(a), cb = Probe(b);
        ca.transform.SetPositionAndRotation(pa, ra); cb.transform.SetPositionAndRotation(pb, rb);
        Physics.SyncTransforms();
        return Physics.ComputePenetration(ca, pa, ra, cb, pb, rb, out _, out float d) && d > 0.02f;
    }

    static bool Conflict(L2Openable a, L2Openable b)
    {
        // a pair that already touches at the seam while closed only counts once a door is clearly open
        for (int s = 3; s <= 20; s++)
        {
            float k = s / 20f;
            if (Touch(a, k * a.maxOpen, b, b.maxOpen)) return true;
            if (Touch(a, a.maxOpen, b, k * b.maxOpen)) return true;
        }
        return false;
    }

    static bool ObbOverlap((Vector3 c, Vector3 e, Quaternion r) A, (Vector3 c, Vector3 e, Quaternion r) B)
    {
        Vector3[] ax = { A.r * Vector3.right, A.r * Vector3.up, A.r * Vector3.forward };
        Vector3[] bx = { B.r * Vector3.right, B.r * Vector3.up, B.r * Vector3.forward };
        var axes = new List<Vector3>(ax); axes.AddRange(bx);
        foreach (var p in ax) foreach (var q in bx) { Vector3 c = Vector3.Cross(p, q); if (c.sqrMagnitude > 1e-6f) axes.Add(c.normalized); }
        Vector3 d = B.c - A.c;
        foreach (Vector3 L in axes)
        {
            float ra = A.e.x * Mathf.Abs(Vector3.Dot(ax[0], L)) + A.e.y * Mathf.Abs(Vector3.Dot(ax[1], L)) + A.e.z * Mathf.Abs(Vector3.Dot(ax[2], L));
            float rb = B.e.x * Mathf.Abs(Vector3.Dot(bx[0], L)) + B.e.y * Mathf.Abs(Vector3.Dot(bx[1], L)) + B.e.z * Mathf.Abs(Vector3.Dot(bx[2], L));
            if (Mathf.Abs(Vector3.Dot(d, L)) > ra + rb) return false;
        }
        return true;
    }

    [MenuItem("Code Breaker/Realism Pass/7. Redo Oven Key + Note (Level 2)", false, 616)]
    static void RedoOvenMenu()
    {
        log = new StringBuilder("REALISM PASS  oven redo  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("oven note, key, towel", Oven);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Realism Pass/6. Dump Kitchen Doors (read-only)", false, 615)]
    static void DumpDoors()
    {
        var sb = new StringBuilder();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (!r.name.StartsWith("KIT_") || !(r.name.Contains("Cabinet") || r.name.Contains("Pantry") || r.name.Contains("Refrigerator"))) continue;
            var o = r.GetComponent<L2Openable>();
            sb.AppendLine(r.name + "  min " + V(r.bounds.min) + " max " + V(r.bounds.max) + "  pivot " + V(r.transform.position) +
                (o != null ? "  " + o.kind + " angle " + o.openAngle + " max " + o.maxOpen.ToString("F2") : ""));
        }
        File.WriteAllText("Logs/CB_KitchenDoors.txt", sb.ToString());
        Debug.Log("[Realism Pass] Logs/CB_KitchenDoors.txt");
    }

    // ================================================================== verification snapshots
    // Renders before/after evidence for every Level 2 fix into Logs/Shots/RP_*.png. Doors, drawers and the
    // oven are posed open only for the picture and put back afterwards; nothing is saved.
    [MenuItem("Code Breaker/Realism Pass/5. Verification Snapshots (Level 2)", false, 614)]
    static void VerifyMenu()
    {
        if (SceneManager.GetActiveScene().name != "Level 2") { Debug.LogWarning("[Realism Pass] open Level 2 first"); return; }
        Directory.CreateDirectory("Logs/Shots");
        foreach (var f in Directory.GetFiles("Logs/Shots", "RP_*.png")) File.Delete(f);
        var undo = new List<(Transform t, Vector3 p, Quaternion r, bool act)>();
        void Keep(Transform t) { if (t != null) undo.Add((t, t.position, t.rotation, t.gameObject.activeSelf)); }
        try
        {
            Vector3 C(string n) { Transform t = Find(n); return t != null ? RendererBounds(t).center : Vector3.zero; }
            // closed views first
            Shot("RP_01_HallPhotoA_side", C("HALL_Photo_A") + new Vector3(2.4f, 0.3f, -1.6f), C("HALL_Photo_A"));
            Shot("RP_02_HallPhotoB_side", C("HALL_Photo_B") + new Vector3(2.4f, 0.3f, 1.6f), C("HALL_Photo_B"));
            Shot("RP_03_CoatHooks_side", C("HALL_CoatHooks") + new Vector3(2.4f, 0.3f, -1.6f), C("HALL_CoatHooks"));
            Shot("RP_04_MusicBox", C("Lid_Mirror") + new Vector3(0.2f, 0.5f, 1.4f), C("Lid_Mirror") + Vector3.down * 0.2f);
            Shot("RP_05_MoonBox", C("DirectionLock") + new Vector3(-1.4f, 0.8f, 0.4f), C("DirectionLock"));
            Shot("RP_06_FridgeNotes", C("KIT_Refrigerator") + new Vector3(0f, 0.5f, -6f), C("KIT_Refrigerator"));
            Shot("RP_07_RangeHood", C("KIT_RangeHood") + new Vector3(-1.5f, -2.0f, -6f), C("KIT_RangeHood") + Vector3.down * 1.5f);
            Shot("RP_08_Collar_top", C("Puzzle_Study_DogCollar_TagBYTE") + new Vector3(0.3f, 1.4f, -0.6f), C("Puzzle_Study_DogCollar_TagBYTE"));
            Shot("RP_09_Laptop", C("STU_Laptop") + new Vector3(0.6f, 0.9f, -2.2f), C("STU_Laptop"));
            Shot("RP_10_Laptop_side", C("STU_Laptop") + new Vector3(2.2f, 0.5f, -0.3f), C("STU_Laptop"));
            Shot("RP_11_DogBowl", C("DogBowl") + new Vector3(0.8f, 1.2f, -1.4f), C("DogBowl"));

            // jewellery box with every drawer pulled out
            L2JewelleryBox jb = Object.FindAnyObjectByType<L2JewelleryBox>(FindObjectsInactive.Include);
            if (jb != null)
            {
                Vector3 jc = RendererBounds(jb.transform.Find("Body")).center;
                Shot("RP_12_Jewellery_closed", jc + new Vector3(-1.5f, 0.5f, 0.9f), jc);
                foreach (Transform t in jb.drawers.Concat(jb.knobs)) { Keep(t); if (t != null) t.position += jb.slide; }
                if (jb.keyTray != null) { Keep(jb.keyTray); jb.keyTray.position += jb.slide; }
                Shot("RP_13_Jewellery_open", jc + new Vector3(-1.5f, 0.6f, 0.9f), jc + new Vector3(-0.3f, 0f, 0f));
                Shot("RP_14_Jewellery_open_side", jc + new Vector3(-0.9f, 0.4f, -1.4f), jc + new Vector3(-0.3f, 0f, 0f));
            }

            // every kitchen door / drawer at its limit
            foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
            {
                if (!(o.name.StartsWith("KIT_"))) continue;
                Keep(o.transform);
                float k = Mathf.Clamp01(o.maxOpen);
                if (o.kind == L2Openable.Kind.Drawer) o.transform.position += o.slide * k;
                else o.transform.SetPositionAndRotation(o.DoorPosition(o.transform.position, k), Quaternion.AngleAxis(o.openAngle * k, Vector3.up) * o.transform.rotation);
            }
            Shot("RP_15_Pantry_open", C("KIT_Pantry") + new Vector3(0.5f, 1.5f, -7f), C("KIT_Pantry"));
            Shot("RP_16_Fridge_open", C("KIT_Refrigerator") + new Vector3(-2.5f, 0.6f, -6.5f), C("KIT_Refrigerator"));
            Shot("RP_17_Corner_U4_U6_open", C("KIT_UpperCabinet_U4") + new Vector3(-3.5f, -1.5f, -7f), C("KIT_UpperCabinet_U4"));
            Shot("RP_18_BackWall_open", C("KIT_RangeHood") + new Vector3(0f, -3.5f, -11f), C("KIT_RangeHood") + Vector3.down * 3f);
            Shot("RP_19_U5_U6_open", C("KIT_UpperCabinet_U6") + new Vector3(-7f, -1.5f, -2f), C("KIT_UpperCabinet_U6"));

            // oven open, as the game opens it
            L2Oven oven = Object.FindAnyObjectByType<L2Oven>(FindObjectsInactive.Include);
            if (oven != null && oven.ovenDoor != null)
            {
                Transform door = oven.ovenDoor;
                Keep(door);
                Bounds b = door.GetComponent<Renderer>().bounds;
                door.RotateAround(new Vector3(b.center.x, b.min.y, b.min.z), Vector3.right, oven.doorAngle);
                if (oven.key != null) { Keep(oven.key.transform); oven.key.SetActive(true); }
                if (oven.doorNote != null) { Keep(oven.doorNote.transform); oven.doorNote.SetActive(true); }
                Shot("RP_20_Oven_open", b.center + new Vector3(0.3f, 3.0f, -3.2f), b.center + new Vector3(0, -0.6f, -0.6f));
                Shot("RP_21_Oven_open_close", b.center + new Vector3(0.2f, 2.2f, -1.6f), b.center + new Vector3(0, -0.9f, -0.8f));
            }
        }
        finally
        {
            for (int i = undo.Count - 1; i >= 0; i--) { var u = undo[i]; u.t.SetPositionAndRotation(u.p, u.r); u.t.gameObject.SetActive(u.act); }
        }
        Debug.Log("[Realism Pass] verification snapshots written to Logs/Shots/RP_*.png");
    }

    static void Shot(string name, Vector3 from, Vector3 look)
    {
        var go = new GameObject("CB_VerifyCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        if (Camera.main != null) cam.CopyFrom(Camera.main);
        cam.enabled = false; cam.fieldOfView = 55f; cam.nearClipPlane = 0.02f;
        var rt = new RenderTexture(800, 450, 24); rt.antiAliasing = 4; cam.targetTexture = rt;
        cam.transform.position = from; cam.transform.LookAt(look);
        cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes("Logs/Shots/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    // ================================================================== start screens
    // Level 1's loading screen (its own screenshot as the background + title) is the reference.
    // Level 2 and Level 3 get a real view of their own environment as the background, and their
    // titles / descriptions are corrected in the Level Catalog.
    static void StartScreens()
    {
        const string catPath = "Assets/CodeBreaker/Resources/CodeBreaker/LevelCatalog.asset";
        string i2 = "Assets/CodeBreaker/UI/LoadingImages/Level2_StartScreen.png", i3 = "Assets/CodeBreaker/UI/LoadingImages/Level3_StartScreen.png";
        Sprite s2 = AssetDatabase.LoadAssetAtPath<Sprite>(i2) ?? Capture("Level 2", "Level2_StartScreen");
        Sprite s3 = AssetDatabase.LoadAssetAtPath<Sprite>(i3) ?? Capture("Level 3", "Level3_StartScreen");
        Object cat = AssetDatabase.LoadMainAssetAtPath(catPath);
        if (cat == null) { log.AppendLine("  no catalog at " + catPath); return; }
        var so = new SerializedObject(cat);
        SerializedProperty levels = so.FindProperty("levels");
        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty e = levels.GetArrayElementAtIndex(i);
            string scene = e.FindPropertyRelative("sceneName").stringValue;
            if (scene == "Level 2")
            {
                e.FindPropertyRelative("title").stringValue = "The Family House";
                e.FindPropertyRelative("description").stringValue = "LIVING ROOM, BEDROOM, KITCHEN, AND STUDY ROOM\nA quiet family home with every door locked. Solve each room's puzzle to find the key to the next one.\n\n<color=#9A2A22>20:00 on the clock.</color>";
                if (s2 != null) e.FindPropertyRelative("loadingImage").objectReferenceValue = s2;
            }
            else if (scene == "Level 3")
            {
                e.FindPropertyRelative("title").stringValue = "Server Room";
                if (s3 != null) e.FindPropertyRelative("loadingImage").objectReferenceValue = s3;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        log.AppendLine("  catalog: Level 2 = 'The Family House' (rooms listed, 20:00), Level 3 = 'Server Room', new backgrounds");
    }

    static Sprite Capture(string scene, string file)
    {
        if (!Open(scene)) return null;
        Camera main = Camera.main;
        if (main == null) { log.AppendLine("  no camera in " + scene); return null; }
        const int W = 1920, H = 1080;
        var go = new GameObject("CB_StartShot") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.enabled = false;
        cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = null;
        string dir = "Assets/CodeBreaker/UI/LoadingImages";
        EnsureFolder(dir);
        string path = dir + "/" + file + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        log.AppendLine("  captured " + path + " from the player's start view in " + scene);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
