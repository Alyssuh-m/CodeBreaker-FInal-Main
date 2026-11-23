using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Code Breaker > Fix 11 (Nov 23): Level 2 desk lamp, Level 3 wall fan and electrical cabinet signs.
// Every item writes Logs/CB_Fix11.txt (appended) and pictures to Logs/Fix11/.
public static class CodeBreakerFix11
{
    const string LogPath = "Logs/CB_Fix11.txt";
    static StringBuilder log;

    static void Begin(string title) { log = new StringBuilder("\n==== " + title + "  " + System.DateTime.Now + "\n"); }
    static void End()
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, log.ToString());
        Debug.Log("[Fix11] done -> " + LogPath);
    }

    // ================================================================== 1. Level 2 lamps report
    [MenuItem("Code Breaker/Fix 11/1. Level 2: lamps report + pictures", false, 950)]
    static void L2Lamps()
    {
        Begin("LEVEL 2 LAMPS");
        if (!OpenScene("Level 2")) { End(); return; }
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Where(x => x.name.Contains("Lamp")).OrderBy(x => x.name))
        {
            Vector3 lp = l.transform.position;
            log.AppendLine("LIGHT " + PathOf(l.transform) + " at " + V(lp) + " type " + l.type + " range " + l.range.ToString("F1"));
            foreach (Renderer r in renderers.Where(r => (r.bounds.center - lp).magnitude < 3.5f).OrderBy(r => (r.bounds.center - lp).magnitude))
            {
                Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject);
                Transform t = r.transform;
                log.AppendLine("   " + PathOf(t) + "\n      mesh " + (m != null ? m.name + " [" + AssetDatabase.GetAssetPath(m) + "]" : "none") +
                               (string.IsNullOrEmpty(prefab) ? "" : " prefab " + prefab) +
                               "\n      pos " + V(t.position) + " euler " + V(t.eulerAngles) + " localPos " + V(t.localPosition) + " localEuler " + V(t.localEulerAngles) +
                               " lossy " + V(t.lossyScale) + "\n      bounds " + V(r.bounds.center) + " size " + V(r.bounds.size) +
                               " mats " + string.Join(",", r.sharedMaterials.Select(x => x != null ? x.name : "null")));
            }
            string tag = l.name.Replace(" ", "_");
            Shot("L2_" + tag + "_a", lp + new Vector3(2.6f, 0.6f, -2.6f), lp + Vector3.down * 0.9f);
            Shot("L2_" + tag + "_b", lp + new Vector3(-2.6f, 0.6f, -2.6f), lp + Vector3.down * 0.9f);
        }
        End();
    }

    // ================================================================== 2. mesh islands of the study desk lamp
    [MenuItem("Code Breaker/Fix 11/2. Level 2: desk lamp mesh parts", false, 951)]
    static void LampIslands()
    {
        Begin("LEVEL 2 DESK LAMP PARTS");
        if (!OpenScene("Level 2")) { End(); return; }
        Transform lamp = FindUnder(Find("Level2/DecorativeProps"), "STU_DeskLamp");
        if (lamp == null) { log.AppendLine("STU_DeskLamp not found"); End(); return; }
        Mesh m = lamp.GetComponent<MeshFilter>().sharedMesh;
        log.AppendLine("mesh " + m.name + " verts " + m.vertexCount + " submeshes " + m.subMeshCount + " local bounds " + V(m.bounds.center) + " size " + V(m.bounds.size));
        foreach (var isl in Islands(m))
        {
            Bounds lb = isl.Value, wb = new Bounds(lamp.TransformPoint(lb.center), Vector3.zero);
            for (int i = 0; i < 8; i++) wb.Encapsulate(lamp.TransformPoint(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            log.AppendLine("  part " + isl.Key + ": local " + V(lb.center) + " size " + V(lb.size) + "  world " + V(wb.center) + " size " + V(wb.size));
        }
        End();
    }

    // connected vertex groups (by shared position) -> local bounds; key = "index (vertex count)"
    static List<KeyValuePair<string, Bounds>> Islands(Mesh m)
    {
        Vector3[] v = m.vertices; int[] tri = m.triangles;
        int[] parent = new int[v.Length];
        for (int i = 0; i < v.Length; i++) parent[i] = i;
        int F(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void U(int a, int b) { a = F(a); b = F(b); if (a != b) parent[a] = b; }
        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var k = Vector3Int.RoundToInt(v[i] * 10000f);
            if (byPos.TryGetValue(k, out int j)) U(i, j); else byPos[k] = i;
        }
        for (int t = 0; t < tri.Length; t += 3) { U(tri[t], tri[t + 1]); U(tri[t], tri[t + 2]); }
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < v.Length; i++) { int r = F(i); if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>(); l.Add(i); }
        var res = new List<KeyValuePair<string, Bounds>>();
        int n = 0;
        foreach (var g in groups.Values.OrderByDescending(g => g.Count))
        {
            var b = new Bounds(v[g[0]], Vector3.zero);
            foreach (int i in g) b.Encapsulate(v[i]);
            res.Add(new KeyValuePair<string, Bounds>(n++ + " (" + g.Count + " verts)", b));
        }
        return res;
    }

    // ================================================================== 3. fix the study desk lamp
    // STU_DeskLamp (one mesh from Level2_House_Decor.fbx) has 5 parts: base, stem, shade (outer + inner)
    // and a short arm piece that sits 0.6 m behind the lamp, on the wall. The stem stops beside the shade
    // with nothing joining them, so the shade floats. The fixed copy leaves out the stray piece and adds an
    // arm (same black metal) from the top of the stem to the top of the shade. The FBX is not changed:
    // the lamp in the scene uses the copy (Assets/CodeBreaker/Level2/Models/Fixed/STU_DeskLamp_Fixed.asset).
    const string LampMeshPath = "Assets/CodeBreaker/Level2/Models/Fixed/STU_DeskLamp_Fixed.asset";

    [MenuItem("Code Breaker/Fix 11/3. Level 2: fix study desk lamp", false, 952)]
    static void FixLamp()
    {
        Begin("LEVEL 2 DESK LAMP FIX");
        if (!OpenScene("Level 2")) { End(); return; }
        Backup("Assets/Scenes/Level 2.unity");
        Transform lamp = FindUnder(Find("Level2/DecorativeProps"), "STU_DeskLamp");
        if (lamp == null) { log.AppendLine("STU_DeskLamp not found"); End(); return; }
        MeshFilter mf = lamp.GetComponent<MeshFilter>();
        Mesh src = AssetDatabase.LoadAllAssetsAtPath("Assets/CodeBreaker/Level2/Models/Level2_House_Decor.fbx").OfType<Mesh>().FirstOrDefault(x => x.name == "STU_DeskLamp");
        if (src == null) { log.AppendLine("original mesh not found"); End(); return; }
        Shot("L2_desklamp_before", lamp.position + new Vector3(2.0f, 1.4f, -2.2f), lamp.position + new Vector3(0f, 0.9f, 0f));

        Vector3[] v = src.vertices; Vector3[] nrm = src.normals; Vector2[] uv = src.uv; int[] tri = src.triangles;
        var groups = Groups(src);   // vertex groups, largest first
        // classify the parts by their local bounds
        List<int> stray = null, stem = null, shade = null;
        foreach (var g in groups)
        {
            Bounds b = BoundsOf(v, g);
            if (b.center.y > 0.25f) stray = g;
            else if (b.size.z > 0.25f && b.size.x < 0.05f) stem = g;
            else if (b.center.z < -0.3f && (shade == null || b.size.x > BoundsOf(v, shade).size.x)) shade = g;
        }
        if (stray == null || stem == null || shade == null) { log.AppendLine("parts not recognised (stray " + (stray != null) + ", stem " + (stem != null) + ", shade " + (shade != null) + ")"); End(); return; }
        float stemTopZ = stem.Min(i => v[i].z), shadeTopZ = shade.Min(i => v[i].z);
        Vector3 p = Avg(v, stem.Where(i => v[i].z < stemTopZ + 0.004f));
        Vector3 q = Avg(v, shade.Where(i => v[i].z < shadeTopZ + 0.004f));
        log.AppendLine("stray piece: " + stray.Count + " verts at local " + V(BoundsOf(v, stray).center) + " (left out)");
        log.AppendLine("stem top " + V(p) + ", shade top " + V(q) + ", arm length " + (q - p).magnitude.ToString("F3") + " (local units)");

        var keep = new HashSet<int>(Enumerable.Range(0, v.Length).Except(stray));
        var map = new Dictionary<int, int>();
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nu = new List<Vector2>(); var nt = new List<int>();
        foreach (int i in keep.OrderBy(i => i)) { map[i] = nv.Count; nv.Add(v[i]); nn.Add(nrm.Length > 0 ? nrm[i] : Vector3.up); nu.Add(uv.Length > 0 ? uv[i] : Vector2.zero); }
        for (int t = 0; t < tri.Length; t += 3)
            if (keep.Contains(tri[t]) && keep.Contains(tri[t + 1]) && keep.Contains(tri[t + 2]))
            { nt.Add(map[tri[t]]); nt.Add(map[tri[t + 1]]); nt.Add(map[tri[t + 2]]); }
        // arm: a square bar from slightly inside the stem top to slightly inside the shade top
        float thick = Mathf.Max(0.012f, BoundsOf(v, stem).size.x * 0.8f);
        Vector3 dir = (q - p).normalized;
        AddBar(nv, nn, nu, nt, p - dir * thick * 0.5f, q + dir * thick * 0.25f, thick);
        // small knuckle where the stem and the arm meet
        AddBar(nv, nn, nu, nt, p + Vector3.forward * thick * 0.8f, p - Vector3.forward * thick * 0.8f, thick * 1.6f);

        var mesh = new Mesh { name = "STU_DeskLamp_Fixed" };
        mesh.SetVertices(nv); mesh.SetNormals(nn); mesh.SetUVs(0, nu); mesh.SetTriangles(nt, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        Directory.CreateDirectory(Path.GetDirectoryName(LampMeshPath));
        if (AssetDatabase.LoadAssetAtPath<Mesh>(LampMeshPath) != null) AssetDatabase.DeleteAsset(LampMeshPath);
        AssetDatabase.CreateAsset(mesh, LampMeshPath);
        AssetDatabase.SaveAssets();
        Undo.RecordObject(mf, "lamp mesh");
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(LampMeshPath);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
        log.AppendLine("lamp now uses " + LampMeshPath + " (" + mesh.vertexCount + " verts, was " + src.vertexCount + "), bounds " + V(lamp.GetComponent<Renderer>().bounds.center) + " size " + V(lamp.GetComponent<Renderer>().bounds.size));
        EditorSceneManager.MarkSceneDirty(lamp.gameObject.scene);
        EditorSceneManager.SaveScene(lamp.gameObject.scene);
        log.AppendLine("Level 2 saved");
        Shot("L2_desklamp_after", lamp.position + new Vector3(2.0f, 1.4f, -2.2f), lamp.position + new Vector3(0f, 0.9f, 0f));
        Shot("L2_desklamp_after_side", lamp.position + new Vector3(-2.4f, 1.3f, -1.6f), lamp.position + new Vector3(0f, 0.9f, 0f));
        Shot("L2_desklamp_after_wall", lamp.position + new Vector3(0.6f, 1.1f, -3.2f), lamp.position + new Vector3(0f, 0.9f, 1.2f));
        End();
    }

    static List<List<int>> Groups(Mesh m)
    {
        Vector3[] v = m.vertices; int[] tri = m.triangles;
        int[] parent = new int[v.Length];
        for (int i = 0; i < v.Length; i++) parent[i] = i;
        int F(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void U(int a, int b) { a = F(a); b = F(b); if (a != b) parent[a] = b; }
        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var k = Vector3Int.RoundToInt(v[i] * 10000f);
            if (byPos.TryGetValue(k, out int j)) U(i, j); else byPos[k] = i;
        }
        for (int t = 0; t < tri.Length; t += 3) { U(tri[t], tri[t + 1]); U(tri[t], tri[t + 2]); }
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < v.Length; i++) { int r = F(i); if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>(); l.Add(i); }
        return groups.Values.OrderByDescending(g => g.Count).ToList();
    }

    static Bounds BoundsOf(Vector3[] v, IEnumerable<int> idx)
    {
        bool first = true; Bounds b = default;
        foreach (int i in idx) { if (first) { b = new Bounds(v[i], Vector3.zero); first = false; } else b.Encapsulate(v[i]); }
        return b;
    }

    static Vector3 Avg(Vector3[] v, IEnumerable<int> idx)
    {
        Vector3 s = Vector3.zero; int n = 0;
        foreach (int i in idx) { s += v[i]; n++; }
        return n > 0 ? s / n : Vector3.zero;
    }

    // square bar from a to b (flat-shaded box, 24 vertices)
    static void AddBar(List<Vector3> v, List<Vector3> n, List<Vector2> u, List<int> t, Vector3 a, Vector3 b, float w)
    {
        Vector3 f = (b - a).normalized;
        Vector3 r = Vector3.Cross(f, Mathf.Abs(f.x) < 0.9f ? Vector3.right : Vector3.up).normalized * (w * 0.5f);
        Vector3 up = Vector3.Cross(r, f).normalized * (w * 0.5f);
        Vector3[] c =
        {
            a - r - up, a + r - up, a + r + up, a - r + up,
            b - r - up, b + r - up, b + r + up, b - r + up
        };
        int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 2, 3, 7, 6 }, new[] { 1, 2, 6, 5 }, new[] { 3, 0, 4, 7 } };
        Vector3 centre = (a + b) * 0.5f;
        foreach (var fc in faces)
        {
            int start = v.Count;
            Vector3 fn = Vector3.Cross(c[fc[1]] - c[fc[0]], c[fc[2]] - c[fc[0]]).normalized;
            Vector3 fcent = (c[fc[0]] + c[fc[1]] + c[fc[2]] + c[fc[3]]) * 0.25f;
            bool flip = Vector3.Dot(fn, fcent - centre) < 0f;
            if (flip) fn = -fn;
            for (int k = 0; k < 4; k++) { v.Add(c[fc[k]]); n.Add(fn); u.Add(Vector2.zero); }
            if (!flip) { t.Add(start); t.Add(start + 1); t.Add(start + 2); t.Add(start); t.Add(start + 2); t.Add(start + 3); }
            else { t.Add(start); t.Add(start + 2); t.Add(start + 1); t.Add(start); t.Add(start + 3); t.Add(start + 2); }
        }
    }

    // ================================================================== 4. Level 3 wall fan + electrical cabinet signs
    [MenuItem("Code Breaker/Fix 11/4. Level 3: fix wall fan + cabinet signs", false, 953)]
    static void FixL3()
    {
        Begin("LEVEL 3 FAN + CABINET SIGNS");
        if (!OpenScene("Level 3")) { End(); return; }
        Backup("Assets/Scenes/Level 3.unity");
        Transform env = Find("Level3/L3_Environment");
        Transform fan = FindUnder(env, "BrokenWallFan");
        Transform ep = FindUnder(env, "ElectricalPanel");
        if (fan != null) Shot("L3_fan_before", fan.position + new Vector3(1.2f, -0.6f, -5f), fan.position);
        if (ep != null) Shot("L3_cabinet_before", ep.position + new Vector3(3.4f, -0.2f, -0.6f), ep.position + new Vector3(0f, -0.3f, 0f));

        // fan: the three blades were different lengths (one snapped short) and not centred on the hub,
        // so it read as a broken shape. Now three equal blades, evenly spaced around the hub.
        Transform blades = fan != null ? fan.Find("Blades") : null;
        if (blades != null)
        {
            int i = 0;
            foreach (Transform bl in blades.Cast<Transform>().Where(x => x.name == "Blade").ToList())
            {
                Undo.RecordObject(bl, "blade");
                Vector3 before = bl.localScale;
                bl.localScale = new Vector3(0.42f, 1.2f, 0.04f);
                // pitch = twist around the blade's own length. The old 18 deg tilt around the fan's X axis
                // pushed the upward blade back into the housing, so only a stub of it showed.
                Quaternion around = Quaternion.Euler(0f, 0f, i * 120f);
                bl.localRotation = around * Quaternion.Euler(0f, 22f, 0f);
                bl.localPosition = around * new Vector3(0f, 0.68f, 0f);
                log.AppendLine("fan blade " + i + ": length " + before.y.ToString("F2") + " -> 1.20, angle " + (i * 120) + " deg");
                i++;
            }
            Transform hub = fan.Find("Hub");
            if (hub != null) { Undo.RecordObject(hub, "hub"); hub.localScale = new Vector3(0.36f, 0.07f, 0.36f); hub.localPosition = blades.localPosition + new Vector3(0f, 0f, -0.06f); }
        }
        else log.AppendLine("fan blades not found");

        // DANGER 415 V plate: its left third was hidden behind the brown down-pipe in front of the cabinet.
        // Smaller and moved right, onto the free part of the left door (between the pipe and the seam).
        Transform danger = ep != null ? ep.Find("Danger") : null;
        if (danger != null)
        {
            Undo.RecordObject(danger, "danger");
            danger.localPosition = new Vector3(-0.43f, 1.2f, -0.06f);
            danger.localScale = Vector3.one * 0.65f;
            Renderer pr = danger.GetComponentInChildren<Renderer>();
            Bounds b = pr.bounds;
            Vector3 lmin = ep.InverseTransformPoint(b.min), lmax = ep.InverseTransformPoint(b.max);
            log.AppendLine("DANGER plate on panel x " + Mathf.Min(lmin.x, lmax.x).ToString("F3") + ".." + Mathf.Max(lmin.x, lmax.x).ToString("F3") + " (pipe ends at -0.82, seam at -0.015)");
        }
        else log.AppendLine("Danger plate not found");

        // POWER MUST REMAIN OFF note: the text was struck through (<s>), which read as a glitch line.
        Transform note = FindUnder(env, "Note_PowerOff");
        var tmp = note != null ? note.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        if (tmp != null)
        {
            Undo.RecordObject(tmp, "note text");
            log.AppendLine("note text '" + tmp.text.Replace("\n", " | ") + "' -> 'POWER MUST | REMAIN OFF'");
            tmp.text = "POWER MUST\nREMAIN OFF";
            EditorUtility.SetDirty(tmp);
        }
        else log.AppendLine("note text not found");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        log.AppendLine("Level 3 saved");
        if (fan != null) Shot("L3_fan_after", fan.position + new Vector3(1.2f, -0.6f, -5f), fan.position);
        if (ep != null) Shot("L3_cabinet_after", ep.position + new Vector3(3.4f, -0.2f, -0.6f), ep.position + new Vector3(0f, -0.3f, 0f));
        if (ep != null) Shot("L3_cabinet_after_top", ep.position + new Vector3(3.0f, 0.9f, -0.5f), ep.position + new Vector3(0f, 0.7f, 0f));
        if (fan != null) Shot("L3_fan_after_side", fan.position + new Vector3(3.5f, -1.2f, -3.5f), fan.position);
        End();
    }

    static void Backup(string assetPath)
    {
        string dir = "CB_Backups/20261123_fix11";
        Directory.CreateDirectory(dir);
        string dst = Path.Combine(dir, Path.GetFileName(assetPath) + ".bak");
        if (!File.Exists(dst)) { File.Copy(assetPath, dst); log.AppendLine("backup " + dst); }
    }

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

    static Transform FindUnder(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static string PathOf(Transform t) { string s = t.name; while ((t = t.parent) != null) s = t.name + "/" + s; return s; }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static void Shot(string name, Vector3 from, Vector3 look, float fill = 3f)
    {
        Directory.CreateDirectory("Logs/Fix11");
        var go = new GameObject("CB_ShotCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        cam.transform.position = from; cam.transform.LookAt(look);
        var lgo = new GameObject("CB_ShotLight");
        var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = 14f; l.intensity = fill; l.shadows = LightShadows.None;
        lgo.transform.position = from + (look - from) * 0.3f + Vector3.up * 0.5f;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;
        cam.Render(); cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Logs/Fix11/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); Object.DestroyImmediate(lgo);
        log.AppendLine("  picture Logs/Fix11/" + name + ".png");
    }
}
