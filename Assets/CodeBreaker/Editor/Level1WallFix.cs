// Code Breaker: Level 1 wall connection fix (editor tool, run once; safe to re-run).
// Menu: Code Breaker > Level 1 > Fix Wall Connections
//
// What it does to every wall piece (materials Inner_Horror / Top_Horror / Outer_Horror):
//  1. Puts all pieces of the same wall line on ONE plane with ONE thickness (some were 2-10 cm off,
//     which showed as steps and dark seams where wall pieces meet).
//  2. Makes the lower and upper wall sections meet exactly (lower 0.75 below floor -> 10.75,
//     upper 10.75 -> into the roof) and pushes every wall top into the roof so no line of light
//     shows between wall and ceiling.
//  3. Extends wall ends into the perpendicular wall they touch, so corners and T-junctions close.
//  4. Gives every piece a box mesh with world-space UVs and one shared wall material, so the same
//     texture continues across the lower section, the upper section and the corners (before, each
//     piece stretched the texture differently, which is what made the sections look separate).
// Transforms and colliders stay on the same GameObjects; only the mesh, the collider size and the
// material change. Original materials are not modified.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeBreaker.Tools
{
    public static class Level1WallFix
    {
        const string Scene1 = "Assets/Scenes/Level 1.unity";
        const string Root = "Assets/CodeBreaker/Level1";
        const string MatPath = Root + "/Materials/L1_Wall_Horror_WorldUV.mat";
        const float TexSize = 12f;          // metres of wall per texture repeat (same everywhere)
        const float LowerBottom = -0.75f, LowerTop = 10.75f, WallTop = 14.3f, OuterBottom = -0.85f;
        const float InnerThickness = 1.2f, OuterThickness = 1.0f;

        class Piece
        {
            public MeshRenderer r; public string kind;   // lower / upper / outer
            public int thin;                               // 0 = x, 2 = z
            public Vector3 min, max;
            public int line;
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        [MenuItem("Code Breaker/Level 1/Fix Wall Connections")]
        public static void Fix()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != Scene1)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(Scene1, OpenSceneMode.Single);
            }
            EnsureFolder(Root + "/Materials"); EnsureFolder(Root + "/Meshes");
            var log = new StringBuilder("Level 1 wall fix report\n");

            // shared wall material (copy of Inner_Horror, 1x1 tiling, one colour)
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var pieces = new List<Piece>();
            Material inner = null;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                if (r.sharedMaterial == null) continue;
                string m = r.sharedMaterial.name;
                string kind = m == "Inner_Horror" ? "lower" : m == "Top_Horror" ? "upper" : m == "Outer_Horror" ? "outer" : null;
                if (kind == null && r.sharedMaterial == wallMat && wallMat != null)
                {
                    // already converted: recover the kind from the stored name
                    string n = r.GetComponent<MeshFilter>().sharedMesh != null ? r.GetComponent<MeshFilter>().sharedMesh.name : "";
                    kind = n.Contains("_lower_") ? "lower" : n.Contains("_upper_") ? "upper" : n.Contains("_outer_") ? "outer" : null;
                }
                if (kind == null) continue;
                if (m == "Inner_Horror" && inner == null) inner = r.sharedMaterial;
                Bounds b = r.bounds;
                var p = new Piece { r = r, kind = kind, min = b.min, max = b.max };
                p.thin = b.size.x < b.size.z ? 0 : 2;
                pieces.Add(p);
            }
            if (wallMat == null)
            {
                if (inner == null) { Debug.LogError("[Level1WallFix] Inner_Horror material not found"); return; }
                wallMat = new Material(inner) { name = "L1_Wall_Horror_WorldUV" };
                wallMat.SetTextureScale("_BaseMap", Vector2.one); wallMat.SetTextureOffset("_BaseMap", Vector2.zero);
                if (wallMat.HasProperty("_MainTex")) { wallMat.SetTextureScale("_MainTex", Vector2.one); wallMat.SetTextureOffset("_MainTex", Vector2.zero); }
                wallMat.SetColor("_BaseColor", new Color(0.70f, 0.69f, 0.69f, 1f));
                AssetDatabase.CreateAsset(wallMat, MatPath);
            }
            log.AppendLine("wall pieces: " + pieces.Count);

            // ---- 1. cluster wall lines (same thin axis, centre within 35 cm)
            var lineCenter = new List<float>(); var lineAxis = new List<int>(); var lineThick = new List<float>();
            foreach (var p in pieces)
            {
                float c = (p.min[p.thin] + p.max[p.thin]) * 0.5f;
                int found = -1;
                for (int i = 0; i < lineCenter.Count; i++)
                    if (lineAxis[i] == p.thin && Mathf.Abs(lineCenter[i] - c) < 0.35f) { found = i; break; }
                if (found < 0) { lineCenter.Add(c); lineAxis.Add(p.thin); lineThick.Add(p.kind == "outer" ? OuterThickness : InnerThickness); found = lineCenter.Count - 1; }
                p.line = found;
            }
            // target centre of a line = average of its pieces' centres (length weighted)
            for (int i = 0; i < lineCenter.Count; i++)
            {
                float sum = 0, w = 0;
                foreach (var p in pieces)
                    if (p.line == i)
                    {
                        int L = 2 - p.thin;
                        float len = p.max[L] - p.min[L];
                        sum += (p.min[p.thin] + p.max[p.thin]) * 0.5f * len; w += len;
                    }
                lineCenter[i] = Mathf.Round(sum / w * 100f) / 100f;
                log.AppendLine(string.Format("line {0}: axis {1} centre {2:0.00} thickness {3}", i, lineAxis[i] == 0 ? "x" : "z", lineCenter[i], lineThick[i]));
            }

            // ---- 2/3. new boxes
            int extended = 0;
            foreach (var p in pieces)
            {
                int a = p.thin, L = 2 - p.thin;
                float c = lineCenter[p.line], th = lineThick[p.line];
                Vector3 mn = p.min, mx = p.max;
                mn[a] = c - th * 0.5f; mx[a] = c + th * 0.5f;
                if (p.kind == "lower") { mn.y = LowerBottom; mx.y = LowerTop; }
                else if (p.kind == "upper") { mn.y = LowerTop; mx.y = WallTop; }
                else { mn.y = OuterBottom; mx.y = WallTop; }
                // close each end into the NEAREST perpendicular wall that it touches
                for (int end = 0; end < 2; end++)
                {
                    float e = end == 0 ? mn[L] : mx[L];
                    int best = -1; float bestD = 0.9f;
                    for (int i = 0; i < lineCenter.Count; i++)
                    {
                        if (lineAxis[i] != L) continue;
                        float d = Mathf.Abs(e - lineCenter[i]);
                        if (d >= bestD) continue;
                        bool spans = false;
                        foreach (var q in pieces)
                            if (q.line == i && q.min[a] - 0.7f <= c && q.max[a] + 0.7f >= c) { spans = true; break; }
                        if (spans) { best = i; bestD = d; }
                    }
                    if (best < 0) continue;
                    float pc = lineCenter[best], pth = lineThick[best];
                    if (end == 0 && mn[L] > pc - pth * 0.5f) { mn[L] = pc - pth * 0.5f; extended++; }
                    if (end == 1 && mx[L] < pc + pth * 0.5f) { mx[L] = pc + pth * 0.5f; extended++; }
                }
                Apply(p, mn, mx, wallMat, log);
            }
            log.AppendLine("wall ends closed into perpendicular walls: " + extended);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            string rep = Path.GetFullPath(Path.Combine(Application.dataPath, "../Level1_Inspect/wallfix_report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(rep));
            File.WriteAllText(rep, log.ToString());
            Debug.Log("[Level1WallFix] done. " + pieces.Count + " wall pieces fixed. Report: " + rep);
        }

        static void Apply(Piece p, Vector3 mn, Vector3 mx, Material wallMat, StringBuilder log)
        {
            Transform t = p.r.transform;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            Vector3 c = (mn + mx) * 0.5f, h = (mx - mn) * 0.5f;
            foreach (Vector3 n in normals)
            {
                Vector3 ua = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized;
                Vector3 va = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                float hu = Mathf.Abs(Vector3.Dot(h, ua)), hv = Mathf.Abs(Vector3.Dot(h, va)), hn = Mathf.Abs(Vector3.Dot(h, n));
                Vector3 fc = c + n * hn;
                Vector3[] w = { fc - ua * hu - va * hv, fc - ua * hu + va * hv, fc + ua * hu + va * hv, fc + ua * hu - va * hv };
                int b = verts.Count;
                foreach (Vector3 wp in w)
                {
                    verts.Add(t.InverseTransformPoint(wp));
                    // world-space UVs: horizontal along the wall, vertical = height
                    float u = Mathf.Abs(n.x) > 0.5f ? wp.z : wp.x;
                    float v = Mathf.Abs(n.y) > 0.5f ? wp.z : wp.y;
                    uvs.Add(new Vector2(u / TexSize, v / TexSize));
                }
                Vector3 nl = t.InverseTransformVector(n);
                bool front = Vector3.Dot(Vector3.Cross(verts[b + 1] - verts[b], verts[b + 2] - verts[b]), nl) > 0f;
                if (front) tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                else tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            string key = "L1W_" + p.kind + "_" + Mathf.Abs(PathOf(t).GetHashCode()).ToString("X8");
            string path = Root + "/Meshes/" + key + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = key;
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);

            p.r.GetComponent<MeshFilter>().sharedMesh = mesh;
            p.r.sharedMaterial = wallMat;
            var bc = p.r.GetComponent<BoxCollider>();
            if (bc != null) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            var mc = p.r.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = mesh;
            log.AppendLine(string.Format("{0} [{1}] min ({2:0.00},{3:0.00},{4:0.00}) max ({5:0.00},{6:0.00},{7:0.00})",
                PathOf(t), p.kind, mn.x, mn.y, mn.z, mx.x, mx.y, mx.z));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
