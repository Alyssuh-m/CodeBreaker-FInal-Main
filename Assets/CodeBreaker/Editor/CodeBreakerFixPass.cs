using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBreaker.Level2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Code Breaker: inspection + fix tools for the Lobby, Level 1, Level 2 and Level 3 review.
//
//   Backup Scenes              copies Assets/Scenes/*.unity to <project>/CB_Backups/<time>/ (outside Assets)
//   Write Scene Report         Logs/CB_SceneReport_<scene>.txt (renderers, colliders, openables, door swings, clipping)
//   Render Close-up Snapshots  Logs/Shots/*.png for the names in Logs/CB_ShotList.txt
//   Lobby - Remove Display Settings
//   Fix Props (open scene)     double-sided open shells, decor colliders, book placement, door swing limits, window view
//   Build Outdoor Window View  renders the garden panorama used behind the windows
public static class CodeBreakerFixPass
{
    const string Tag = "[Code Breaker Fix] ";
    const string ScenesDir = "Assets/Scenes";
    const string MatDir = "Assets/CodeBreaker/Level2/Materials/FixPass";
    const string ViewTexPath = "Assets/CodeBreaker/Level2/Materials/FixPass/L2_OutdoorView_Panorama.png";
    const string ViewMatPath = "Assets/CodeBreaker/Level2/Materials/FixPass/L2_Window_OutdoorView.mat";
    const string ViewShaderName = "CodeBreaker/Window Outdoor View";
    const string ColliderName = "CB_Collider";

    static string Logs => Path.Combine(Directory.GetCurrentDirectory(), "Logs");

    // ================================================================== backup
    [MenuItem("Code Breaker/Fix Pass/1. Backup Scenes", false, 290)]
    static void BackupScenes()
    {
        string dst = Path.Combine(Directory.GetCurrentDirectory(), "CB_Backups", System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dst);
        int n = 0;
        foreach (string f in Directory.GetFiles(ScenesDir, "*.unity"))
        {
            File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
            n++;
        }
        foreach (string f in new[] {
            "Assets/CodeBreaker/Level2/Scripts/Interaction/L2Openable.cs",
            "Assets/CodeBreaker/Scripts/GameFlowManager.cs",
            "Assets/CodeBreaker/Scripts/PlayerHealth.cs",
            "Assets/CodeBreaker/Scripts/AudioManager.cs",
            "Assets/CodeBreaker/Scripts/UI/CodeBreakerSettings.cs",
            "Assets/CodeBreaker/Scripts/UI/SettingsPanel.cs",
            "Assets/CodeBreaker/Scripts/UI/MainMenuController.cs",
            "Assets/CodeBreaker/Editor/CodeBreakerPolishTools.cs" })
            if (File.Exists(f)) { File.Copy(f, Path.Combine(dst, Path.GetFileName(f) + ".txt")); n++; }
        Debug.Log(Tag + "Backed up " + n + " files to " + dst);
    }

    // ================================================================== report
    [MenuItem("Code Breaker/Fix Pass/Write Scene Report (open scene)", false, 300)]
    static void WriteReportMenu() => WriteReport();

    [MenuItem("Code Breaker/Fix Pass/Write Reports (Level 1, 2, 3)", false, 301)]
    static void WriteAllReports()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = SceneManager.GetActiveScene().path;
        foreach (string s in new[] { "Level 1", "Level 2", "Level 3" })
        {
            EditorSceneManager.OpenScene(ScenesDir + "/" + s + ".unity", OpenSceneMode.Single);
            WriteReport();
        }
        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
    }

    static void WriteReport()
    {
        Physics.SyncTransforms();
        var scene = SceneManager.GetActiveScene();
        var sb = new StringBuilder();
        sb.AppendLine("SCENE " + scene.name);
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            sb.AppendLine("CAMERA " + PathOf(c.transform) + " near=" + c.nearClipPlane + " far=" + c.farClipPlane + " occl=" + c.useOcclusionCulling + " clear=" + c.clearFlags);
        foreach (LODGroup g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include))
            sb.AppendLine("LODGROUP " + PathOf(g.transform));
        sb.AppendLine("RENDER fog=" + RenderSettings.fog + " ambient=" + RenderSettings.ambientMode + " sky=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "none"));
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            if (l.type == LightType.Directional)
                sb.AppendLine("DIRLIGHT " + PathOf(l.transform) + " int=" + l.intensity + " on=" + (l.enabled && l.gameObject.activeInHierarchy));

        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (r is ParticleSystemRenderer) continue;
            Mesh m = MeshOf(r);
            Bounds b = r.bounds;
            string mats = string.Join(" | ", r.sharedMaterials.Select(Mat));
            bool col = r.GetComponentInParent<Collider>() != null || r.GetComponentInChildren<Collider>() != null;
            Collider own = r.GetComponent<Collider>();
            string shell = m != null && IsPropPath(PathOf(r.transform)) ? ShellIssues(m) : "";
            sb.AppendLine("R " + PathOf(r.transform) + (r.gameObject.activeInHierarchy ? "" : " [inactive]") + (r.enabled ? "" : " [off]") +
                          " mesh=" + (m != null ? m.name + "/" + (m.triangles.Length / 3) + "tri" : "-") + shell +
                          " c=" + V(b.center) + " s=" + V(b.size) + " shadow=" + r.shadowCastingMode +
                          " col=" + (own != null ? own.GetType().Name + (own.isTrigger ? "(trig)" : "") + (own.enabled ? "" : "(off)") : (col ? "parent/child" : "NONE")) +
                          " mats=[" + mats + "]");
        }

        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
        {
            if (c.GetComponent<Renderer>() != null) continue;
            Bounds b = c.bounds;
            sb.AppendLine("C " + PathOf(c.transform) + " " + c.GetType().Name + (c.isTrigger ? "(trig)" : "") + (c.enabled && c.gameObject.activeInHierarchy ? "" : "(off)") +
                          " c=" + V(b.center) + " s=" + V(b.size));
        }

        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (mb == null) continue;
            string ns = mb.GetType().Namespace ?? "";
            string n = mb.GetType().Name;
            if (!(ns.StartsWith("CodeBreaker") || n.StartsWith("L3") || n.StartsWith("L2"))) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            var fields = new StringBuilder();
            if (it.NextVisible(true))
                do
                {
                    if (it.name == "m_Script") continue;
                    if (it.propertyType == SerializedPropertyType.Generic && !it.isArray) continue;
                    fields.Append(it.name).Append('=').Append(Val(it)).Append(' ');
                } while (it.NextVisible(false));
            Collider c = mb.GetComponent<Collider>();
            sb.AppendLine("S " + n + " @ " + PathOf(mb.transform) + " pos=" + V(mb.transform.position) + (mb.enabled ? "" : " [disabled]") +
                          " col=" + (c != null ? c.GetType().Name + (c.isTrigger ? "(trig)" : "") : "NONE") + "  " + fields);
        }

        // How far every drawer / cabinet door can open before it hits something.
        var obstacles = CollectObstacles();
        foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
        {
            float lim = OpenLimit(o, obstacles, out string hit);
            sb.AppendLine("O " + PathOf(o.transform) + " kind=" + o.kind + " maxOpen=" + o.maxOpen.ToString("0.00") + " free=" + lim.ToString("0.00") + (hit != null ? "  BLOCKED by " + hit : ""));
        }

        // Room doors: does the leaf swing through furniture?
        foreach (L2Door d in Object.FindObjectsByType<L2Door>(FindObjectsInactive.Include))
            sb.AppendLine("D " + PathOf(d.transform) + " " + DoorSwingInfo(d, out _));

        // Props clipping into each other.
        foreach (string line in ClipScan()) sb.AppendLine(line);

        Directory.CreateDirectory(Logs);
        string file = Path.Combine(Logs, "CB_SceneReport_" + scene.name.Replace(" ", "_") + ".txt");
        File.WriteAllText(file, sb.ToString());
        Debug.Log(Tag + "Report written: " + file);
    }

    // Sweeps a room door's leaf box from closed to open. Returns the swing angle that is still free.
    static string DoorSwingInfo(L2Door d, out float freeAngle)
    {
        freeAngle = 0f;
        var leaf = d.leafCollider as BoxCollider;
        if (d.pivot == null || leaf == null) return "no pivot/leaf";
        Transform pv = d.pivot;
        Quaternion parentRot = pv.parent != null ? pv.parent.rotation : Quaternion.identity;
        Matrix4x4 leafInPivot = pv.worldToLocalMatrix * leaf.transform.localToWorldMatrix;
        Vector3 ls = leaf.transform.lossyScale;
        ls = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
        Vector3 half = Vector3.Max(Vector3.Scale(leaf.size * 0.5f, ls) - Vector3.one * 0.04f, Vector3.one * 0.01f);
        float total = Mathf.DeltaAngle(d.closedAngle, d.openAngle);

        Matrix4x4 PoseAt(float a)
        {
            Matrix4x4 pivotW = Matrix4x4.TRS(pv.position, parentRot * Quaternion.Euler(0f, a, 0f), pv.lossyScale);
            return pivotW * leafInPivot;
        }
        Collider[] Over(float a)
        {
            Matrix4x4 m = PoseAt(a);
            Vector3 c = m.MultiplyPoint3x4(leaf.center);
            Quaternion r = m.rotation;
            return Physics.OverlapBox(c, half, r, ~0, QueryTriggerInteraction.Ignore)
                .Where(x => !x.transform.IsChildOf(d.transform) && !(x is CharacterController)).ToArray();
        }
        var ignore = new HashSet<Collider>(Over(d.closedAngle));
        const int steps = 45;
        string hit = null;
        for (int i = 1; i <= steps; i++)
        {
            float a = d.closedAngle + total * i / steps;
            var o = Over(a).Where(x => !ignore.Contains(x)).ToArray();
            if (o.Length > 0) { hit = string.Join(", ", o.Select(x => PathOf(x.transform))); break; }
            freeAngle = total * i / steps;
        }
        return "swing=" + total.ToString("0") + " free=" + freeAngle.ToString("0") + (hit != null ? "  BLOCKED by " + hit : "");
    }

    // ------------------------------------------------------------------ accurate open limits
    // A sample of points on a mesh: its vertices plus points along every triangle edge, so a thin
    // part crossing a face is caught even when none of its corners is inside.
    static readonly Dictionary<Mesh, Vector3[]> sampleCache = new Dictionary<Mesh, Vector3[]>();

    static Vector3[] Samples(Mesh m)
    {
        if (sampleCache.TryGetValue(m, out var pts)) return pts;
        Vector3[] v = m.vertices;
        int[] t = m.triangles;
        var set = new HashSet<Vector3Int>();
        var list = new List<Vector3>();
        void Add(Vector3 p)
        {
            var k = new Vector3Int(Mathf.RoundToInt(p.x * 400f), Mathf.RoundToInt(p.y * 400f), Mathf.RoundToInt(p.z * 400f));
            if (set.Add(k)) list.Add(p);
        }
        foreach (Vector3 p in v) Add(p);
        for (int i = 0; i < t.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                Vector3 a = v[t[i + e]], b = v[t[i + (e + 1) % 3]];
                Add(Vector3.Lerp(a, b, 0.25f)); Add(Vector3.Lerp(a, b, 0.5f)); Add(Vector3.Lerp(a, b, 0.75f));
            }
        pts = list.ToArray();
        sampleCache[m] = pts;
        return pts;
    }

    class Obstacle
    {
        public MeshRenderer r;
        public Matrix4x4 w2l;
        public Bounds localInner;     // mesh bounds shrunk by the tolerance
        public Bounds world;
        public Vector3[] worldPts;
        public Vector3[] verts;
        public int[] tris;
        public Collider wall;         // set for wall colliders (tested with the physics box)
    }

    const float Tol = 0.03f;   // ~8 mm: parts may touch, not overlap

    static List<Obstacle> CollectObstacles()
    {
        Physics.SyncTransforms();
        var list = new List<Obstacle>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled) continue;
            Mesh m = MeshOf(r);
            if (m == null || !m.isReadable || m.triangles.Length == 0) continue;
            string p = PathOf(r.transform);
            if (p.StartsWith("Player") || r.GetComponentInParent<L2Door>() != null) continue;
            if (r.name.Contains("Floor") || r.name.Contains("Ceiling") || p.Contains("/Ceiling") || r.name.StartsWith("Wall") || r.name.Contains("_Wall_") || p.Contains("_Architecture/")) continue;
            if (r.GetComponent("TMPro.TMP_Text") != null) continue;
            Bounds lb = m.bounds;
            lb.Expand(-2f * Tol);
            Matrix4x4 l2w = r.transform.localToWorldMatrix;
            list.Add(new Obstacle
            {
                r = r,
                w2l = r.transform.worldToLocalMatrix,
                localInner = lb,
                world = r.bounds,
                worldPts = Samples(m).Select(v => l2w.MultiplyPoint3x4(v)).ToArray(),
                verts = m.vertices,
                tris = m.triangles,
            });
        }
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (c.isTrigger || !c.enabled) continue;
            string n = c.name;
            if (!(n.Contains("Wall") || n.Contains("Jamb")) || n.Contains("Clock")) continue;
            if (c.GetComponentInParent<L2Door>() != null) continue;
            list.Add(new Obstacle { wall = c, world = c.bounds });
        }
        return list;
    }

    // Sweeps the part along its opening path (drawer slide / door swing) and returns the last
    // fraction (0..1) where it does not pass into another object. Things it already touches when
    // closed (its own cabinet body, items lying in a drawer) are ignored, and for doors the strip
    // right at the hinge is left out (the hinge edge turning on the spot is not a collision).
    static float OpenLimit(L2Openable o, List<Obstacle> obstacles, out string hit)
    {
        hit = null;
        Transform pt = o.transform;
        BoxCollider box = o.GetComponent<BoxCollider>();
        Vector3 sls = pt.lossyScale;   // signed: mirrored doors (scale -1) must stay mirrored
        Vector3 ls = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(sls.x)), Mathf.Max(1e-4f, Mathf.Abs(sls.y)), Mathf.Max(1e-4f, Mathf.Abs(sls.z)));
        Bounds lb;
        // the part's own mesh (its trigger box can be bigger: it may also cover the space behind it)
        var ownMf = pt.GetComponent<MeshFilter>();
        if (ownMf != null && ownMf.sharedMesh != null && ownMf.sharedMesh.vertexCount > 0) lb = ownMf.sharedMesh.bounds;
        else if (box != null) lb = new Bounds(box.center, box.size);
        else
        {
            var rr = pt.GetComponentsInChildren<MeshRenderer>();
            if (rr.Length == 0) return 1f;
            lb = new Bounds(pt.InverseTransformPoint(rr[0].bounds.center), Vector3.zero);
            foreach (var r in rr) { lb.Encapsulate(pt.InverseTransformPoint(r.bounds.min)); lb.Encapsulate(pt.InverseTransformPoint(r.bounds.max)); }
        }
        // shrink by the tolerance (world units -> local)
        Vector3 shrink = new Vector3(Tol / ls.x, Tol / ls.y, Tol / ls.z);
        lb.SetMinMax(lb.min + shrink, lb.max - shrink);

        // hinge strip
        int wAxis = -1; float clear = 0f;
        if (o.kind == L2Openable.Kind.Door)
        {
            float ex = Mathf.Max(Mathf.Abs(lb.min.x), Mathf.Abs(lb.max.x)) * ls.x;
            float ez = Mathf.Max(Mathf.Abs(lb.min.z), Mathf.Abs(lb.max.z)) * ls.z;
            wAxis = ex >= ez ? 0 : 2;
            int tAxis = wAxis == 0 ? 2 : 0;
            float thick = lb.size[tAxis] * ls[tAxis] + 2f * Tol;
            clear = (thick + 0.06f) / ls[wAxis];
            Vector3 mn = lb.min, mx = lb.max;
            if (Mathf.Abs(mx[wAxis]) >= Mathf.Abs(mn[wAxis])) mn[wAxis] = Mathf.Max(mn[wAxis], clear);
            else mx[wAxis] = Mathf.Min(mx[wAxis], -clear);
            if (mx[wAxis] <= mn[wAxis]) return 1f;
            lb.SetMinMax(mn, mx);
        }

        // the part's own sample points (part-local), without the hinge strip
        var partPts = new List<Vector3>();
        Matrix4x4 pw2l = pt.worldToLocalMatrix;
        var partRenderers = ownMf != null && pt.GetComponent<MeshRenderer>() != null ? new[] { pt.GetComponent<MeshRenderer>() } : pt.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer r in partRenderers)
        {
            Mesh m = MeshOf(r);
            if (m == null || !m.isReadable) continue;
            Matrix4x4 toPart = pw2l * r.transform.localToWorldMatrix;
            foreach (Vector3 v in Samples(m))
            {
                Vector3 q = toPart.MultiplyPoint3x4(v);
                if (wAxis >= 0 && Mathf.Abs(q[wAxis]) < clear) continue;
                partPts.Add(q);
            }
        }

        Vector3 p0 = pt.position;
        Quaternion r0 = pt.rotation;
        Transform furniture = pt.parent != null ? pt.parent : pt;
        bool Mine(Transform t)
        {
            if (t.IsChildOf(furniture)) return true;
            if (o.attached != null) foreach (Transform a in o.attached) if (a != null && t.IsChildOf(a)) return true;
            return false;
        }

        const int steps = 50;
        // candidates: anything near the whole swept path
        Bounds sweep = PoseBounds(p0, r0, sls, lb);
        for (int i = 1; i <= steps; i++) sweep.Encapsulate(PoseBounds(Pos(o, p0, i / (float)steps), Rot(o, r0, i / (float)steps), sls, lb));
        var cands = obstacles.Where(x => x.world.Intersects(sweep) && !Mine(x.r != null ? x.r.transform : x.wall.transform)).ToList();
        // ignore what already overlaps the closed part
        var touching = new HashSet<Obstacle>(cands.Where(x => Overlaps(x, Matrix4x4.TRS(p0, r0, sls), r0, lb, partPts, ls)));
        cands = cands.Where(x => !touching.Contains(x)).ToList();

        float free = 0f;
        for (int i = 1; i <= steps; i++)
        {
            float k = i / (float)steps;
            Matrix4x4 M = Matrix4x4.TRS(Pos(o, p0, k), Rot(o, r0, k), sls);
            Bounds pb = PoseBounds(Pos(o, p0, k), Rot(o, r0, k), sls, lb);
            foreach (Obstacle x in cands)
            {
                if (!x.world.Intersects(pb)) continue;
                if (Overlaps(x, M, Rot(o, r0, k), lb, partPts, ls)) { hit = PathOf(x.r != null ? x.r.transform : x.wall.transform); return free; }
            }
            free = k;
        }
        return 1f;
    }

    static Vector3 Pos(L2Openable o, Vector3 p0, float k) => o.kind == L2Openable.Kind.Drawer ? p0 + o.slide * k : o.DoorPosition(p0, k);
    static Quaternion Rot(L2Openable o, Quaternion r0, float k) => o.kind == L2Openable.Kind.Door ? Quaternion.AngleAxis(o.openAngle * k, Vector3.up) * r0 : r0;

    static Bounds PoseBounds(Vector3 p, Quaternion r, Vector3 ls, Bounds lb)
    {
        Matrix4x4 M = Matrix4x4.TRS(p, r, ls);
        var b = new Bounds(M.MultiplyPoint3x4(lb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(M.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z)));
        return b;
    }

    static bool Overlaps(Obstacle x, Matrix4x4 M, Quaternion rot, Bounds lb, List<Vector3> partPts, Vector3 ls)
    {
        if (x.wall != null)
        {
            Vector3 c = M.MultiplyPoint3x4(lb.center);
            Vector3 half = Vector3.Scale(lb.extents, ls);
            foreach (Collider c2 in Physics.OverlapBox(c, half, rot, ~0, QueryTriggerInteraction.Ignore))
                if (c2 == x.wall) return true;
            return false;
        }
        Matrix4x4 inv = M.inverse;
        foreach (Vector3 w in x.worldPts)
            if (lb.Contains(inv.MultiplyPoint3x4(w))) return true;
        Vector3 dir = new Vector3(0.371f, 0.912f, 0.173f);
        Vector3 d = x.w2l.MultiplyVector(dir).normalized;
        foreach (Vector3 q in partPts)
        {
            Vector3 w = M.MultiplyPoint3x4(q);
            if (!x.world.Contains(w)) continue;
            Vector3 p = x.w2l.MultiplyPoint3x4(w);
            if (!x.localInner.Contains(p)) continue;
            int crossings = 0;
            for (int t = 0; t < x.tris.Length; t += 3)
                if (RayTri(p, d, x.verts[x.tris[t]], x.verts[x.tris[t + 1]], x.verts[x.tris[t + 2]])) crossings++;
            if ((crossings & 1) == 1) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ clipping scan
    // Pairs of props where vertices of one lie inside the (closed) mesh of the other.
    static List<string> ClipScan()
    {
        var outLines = new List<string>();
        var rs = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude)
            .Where(r => r.enabled && MeshOf(r) != null && MeshOf(r).isReadable && IsPropPath(PathOf(r.transform)))
            .ToArray();
        var info = rs.ToDictionary(r => r, r => (mesh: MeshOf(r), verts: Samples(MeshOf(r)), tris: MeshOf(r).triangles));
        var rawVerts = rs.ToDictionary(r => r, r => MeshOf(r).vertices);
        for (int i = 0; i < rs.Length; i++)
            for (int j = 0; j < rs.Length; j++)
            {
                if (i == j) continue;
                MeshRenderer a = rs[i], b = rs[j];
                if (!a.bounds.Intersects(b.bounds)) continue;
                if (a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform)) continue;
                if (a.transform.parent != null && a.transform.parent == b.transform.parent && !a.transform.parent.name.EndsWith("Decorations") && !a.transform.parent.name.EndsWith("Furniture")) continue;
                if (a.bounds.size.sqrMagnitude > b.bounds.size.sqrMagnitude * 4f) continue;   // only report the smaller one inside the bigger
                int inside = CountInside(a, info[a].verts, b, rawVerts[b], info[b].tris, out int total);
                if (inside >= 3 && inside >= total * 0.04f)
                    outLines.Add("X " + PathOf(a.transform) + "  INSIDE  " + PathOf(b.transform) + "  " + inside + "/" + total + " verts");
            }
        return outLines;
    }

    static bool IsPropPath(string p) =>
        !(p.Contains("_Architecture") || p.Contains("/Ceiling") || p.Contains("Level2_Colliders") || p.Contains("Hallway_Widening") || p.StartsWith("Player"));

    static int CountInside(Renderer a, Vector3[] av, Renderer b, Vector3[] bv, int[] bt, out int total)
    {
        Matrix4x4 aw = a.transform.localToWorldMatrix;
        Matrix4x4 bl = b.transform.worldToLocalMatrix;
        Bounds bb = MeshOf(b).bounds;
        bb.Expand(-0.01f);
        var seen = new HashSet<Vector3>();
        int inside = 0;
        total = 0;
        Vector3 dir = new Vector3(0.371f, 0.912f, 0.173f).normalized;
        foreach (Vector3 v in av)
        {
            Vector3 w = aw.MultiplyPoint3x4(v);
            // (av may be plain vertices or Samples(): both work)
            Vector3 key = new Vector3(Mathf.Round(w.x * 200f), Mathf.Round(w.y * 200f), Mathf.Round(w.z * 200f));
            if (!seen.Add(key)) continue;
            total++;
            Vector3 p = bl.MultiplyPoint3x4(w);
            if (!bb.Contains(p)) continue;
            Vector3 d = bl.MultiplyVector(dir).normalized;
            int crossings = 0;
            for (int t = 0; t < bt.Length; t += 3)
                if (RayTri(p, d, bv[bt[t]], bv[bt[t + 1]], bv[bt[t + 2]])) crossings++;
            if ((crossings & 1) == 1) inside++;
        }
        return inside;
    }

    static bool RayTri(Vector3 o, Vector3 d, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 e1 = v1 - v0, e2 = v2 - v0;
        Vector3 p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-9f) return false;
        float inv = 1f / det;
        Vector3 s = o - v0;
        float u = Vector3.Dot(s, p) * inv;
        if (u < 0f || u > 1f) return false;
        Vector3 q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(d, q) * inv;
        if (v < 0f || u + v > 1f) return false;
        return Vector3.Dot(e2, q) * inv > 1e-5f;
    }

    // Faces that vanish from some angles: open edges, neighbouring faces wound opposite ways, or a
    // part of the mesh that is inside-out (its faces point inwards).
    static string ShellIssues(Mesh m)
    {
        if (m == null || !m.isReadable) return "";
        Vector3[] v = m.vertices;
        int[] t = m.triangles;
        var id = new Dictionary<Vector3Int, int>();
        int[] remap = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
            if (!id.TryGetValue(k, out int n)) { n = id.Count; id[k] = n; }
            remap[i] = n;
        }
        var directed = new Dictionary<long, int>();
        var undirected = new Dictionary<long, int>();
        for (int i = 0; i < t.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = remap[t[i + e]], b = remap[t[i + (e + 1) % 3]];
                if (a == b) continue;
                long dk = ((long)a << 32) | (uint)b;
                long uk = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                directed.TryGetValue(dk, out int c1); directed[dk] = c1 + 1;
                undirected.TryGetValue(uk, out int c2); undirected[uk] = c2 + 1;
            }
        int open = undirected.Values.Count(c => c == 1);
        int flipped = directed.Values.Count(c => c > 1);
        // inside-out parts: connected pieces with negative volume
        var parent = new int[id.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = Find(remap[t[i]]), b = Find(remap[t[i + 1]]), c = Find(remap[t[i + 2]]);
            parent[b] = a; parent[Find(c)] = a;
        }
        var vol = new Dictionary<int, float>();
        for (int i = 0; i < t.Length; i += 3)
        {
            int root = Find(remap[t[i]]);
            vol.TryGetValue(root, out float s0);
            vol[root] = s0 + Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]])) / 6f;
        }
        var openRoots = new HashSet<int>();
        foreach (var kv in undirected)
            if (kv.Value == 1) openRoots.Add(Find((int)(kv.Key >> 32)));
        int neg = vol.Count(kv => kv.Value < -1e-7f && !openRoots.Contains(kv.Key));
        if (open == 0 && flipped == 0 && neg == 0) return "";
        return " SHELL(open=" + open + " flipped=" + flipped + " insideOut=" + neg + "/" + vol.Count + ")";
    }

    // Edges used by only one triangle (after welding vertices by position) = an open, shell-like mesh.
    static int OpenEdgeCount(Mesh m)
    {
        if (!m.isReadable) return 0;
        Vector3[] v = m.vertices;
        int[] t = m.triangles;
        var id = new Dictionary<Vector3Int, int>();
        int[] remap = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
            if (!id.TryGetValue(k, out int n)) { n = id.Count; id[k] = n; }
            remap[i] = n;
        }
        var edges = new Dictionary<long, int>();
        for (int i = 0; i < t.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = remap[t[i + e]], b = remap[t[i + (e + 1) % 3]];
                if (a == b) continue;
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                edges.TryGetValue(key, out int c);
                edges[key] = c + 1;
            }
        return edges.Values.Count(c => c == 1);
    }

    // ================================================================== snapshots
    [MenuItem("Code Breaker/Fix Pass/Render Close-up Snapshots (open scene)", false, 302)]
    static void Snapshots()
    {
        string listFile = Path.Combine(Logs, "CB_ShotList.txt");
        if (!File.Exists(listFile)) { Debug.LogWarning(Tag + "No shot list: " + listFile); return; }
        string outDir = Path.Combine(Logs, "Shots");
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);

        Camera main = Camera.main;
        var go = new GameObject("CB_ShotCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        if (main != null) cam.CopyFrom(main);
        cam.enabled = false;
        cam.fieldOfView = 60f;
        var rt = new RenderTexture(800, 450, 24);
        cam.targetTexture = rt;
        var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
        int count = 0;
        try
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (string raw in File.ReadAllLines(listFile))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                // name                      -> 8 views around it
                // name | dx,dy,dz | dist    -> one view from that direction
                // @ x,y,z | tx,ty,tz | label -> camera at x,y,z looking at tx,ty,tz
                string[] parts = line.Split('|').Select(s => s.Trim()).ToArray();
                var views = new List<(string tag, Vector3 pos, Vector3 look)>();
                string name;
                if (parts[0].StartsWith("@"))
                {
                    name = parts.Length > 2 ? parts[2] : "view";
                    views.Add(("at", Vec(parts[0].Substring(1)), Vec(parts[1])));
                }
                else
                {
                    name = parts[0];
                    Transform t = renderers.Select(r => r.transform).FirstOrDefault(x => x.name == name)
                                  ?? renderers.Select(r => r.transform).FirstOrDefault(x => x.name.Contains(name));
                    if (t == null) { Debug.LogWarning(Tag + "Shot target not found: " + name); continue; }
                    Bounds b = new Bounds(t.position, Vector3.zero); bool has = false;
                    foreach (var r in t.GetComponentsInChildren<Renderer>())
                    { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
                    float rad = Mathf.Max(b.extents.magnitude, 0.3f);
                    if (parts.Length >= 3)
                        views.Add(("custom", b.center + Vec(parts[1]).normalized * float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), b.center));
                    else
                        foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                        {
                            views.Add(("far", b.center + (dir + Vector3.up * 0.35f).normalized * (rad * 3.5f + 3f), b.center));
                            views.Add(("near", b.center + (dir + Vector3.up * 0.6f).normalized * (rad * 1.3f + 0.6f), b.center));
                        }
                }
                int vi = 0;
                foreach (var (tag, pos, look) in views)
                {
                    cam.transform.position = pos;
                    cam.transform.LookAt(look);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(outDir, Safe(name) + "_" + (vi++).ToString("00") + "_" + tag + ".png"), tex.EncodeToPNG());
                    count++;
                }
            }
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
        Debug.Log(Tag + count + " snapshots written to " + outDir);
    }

    // Every drawer / door whose name contains a line of Logs/CB_OpenShotList.txt is shown opened as far
    // as it goes (maxOpen), from the front and from the side. Nothing is changed in the scene.
    [MenuItem("Code Breaker/Fix Pass/Render Open-Drawer Snapshots (open scene)", false, 303)]
    static void OpenSnapshots()
    {
        string listFile = Path.Combine(Logs, "CB_OpenShotList.txt");
        if (!File.Exists(listFile)) { Debug.LogWarning(Tag + "No list: " + listFile); return; }
        var names = File.ReadAllLines(listFile).Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith("#")).ToArray();
        string outDir = Path.Combine(Logs, "OpenShots");
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
        Camera main = Camera.main;
        var go = new GameObject("CB_ShotCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        if (main != null) cam.CopyFrom(main);
        cam.enabled = false;
        cam.fieldOfView = 60f;
        var rt = new RenderTexture(800, 450, 24);
        cam.targetTexture = rt;
        var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
        int count = 0;
        try
        {
            foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
            {
                if (!names.Any(nm => o.name.Contains(nm) || PathOf(o.transform).Contains(nm))) continue;
                Transform pt = o.transform;
                Vector3 p0 = pt.position; Quaternion r0 = pt.rotation;
                float k = Mathf.Clamp01(o.maxOpen);
                pt.SetPositionAndRotation(Pos(o, p0, k), Rot(o, r0, k));
                Bounds b = new Bounds(pt.position, Vector3.one * 0.5f);
                foreach (var r in pt.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                b.Expand(1.5f);
                Vector3 outward = Outward(o, out Vector3 ow, out _) ? ow : -pt.forward;
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                float dist = b.extents.magnitude * 1.3f + 2f;
                var views = new[] { outward + Vector3.up * 0.4f, (outward + side * 1.0f + Vector3.up * 0.5f), Vector3.up + outward * 0.05f };
                int vi = 0;
                foreach (Vector3 dir in views)
                {
                    cam.transform.position = b.center + dir.normalized * dist;
                    cam.transform.LookAt(b.center);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(outDir, Safe(o.name) + "_" + (vi++) + ".png"), tex.EncodeToPNG());
                    count++;
                }
                pt.SetPositionAndRotation(p0, r0);
            }
        }
        finally
        {
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
        Debug.Log(Tag + count + " open-state snapshots written to " + outDir);
    }

    static Vector3 Vec(string s)
    {
        var d = s.Split(',').Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(d[0], d[1], d[2]);
    }

    static string Safe(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());

    // ================================================================== lobby
    [MenuItem("Code Breaker/Fix Pass/2. Lobby - Remove Display Settings", false, 320)]
    static void LobbyRemoveDisplaySettings()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string back = SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(ScenesDir + "/MainMenu.unity", OpenSceneMode.Single);
        SettingsPanel sp = Object.FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
        if (sp == null) { Debug.LogError(Tag + "No SettingsPanel in MainMenu"); return; }
        Transform panel = sp.transform.Find("Panel");
        if (panel == null) { Debug.LogError(Tag + "Settings Screen has no Panel"); return; }

        string[] remove = { "Fullscreen Label", "Fullscreen Toggle", "Resolution Label", "Resolution Dropdown", "Graphics Quality Label", "Graphics Quality Dropdown" };
        int removed = 0;
        foreach (string n in remove)
        {
            Transform t = panel.Find(n);
            if (t == null) continue;
            Undo.DestroyObjectImmediate(t.gameObject);
            removed++;
        }
        Undo.RecordObject(sp, "Remove display settings");
        sp.fullscreenToggle = null;
        sp.resolutionDropdown = null;
        sp.qualityDropdown = null;
        EditorUtility.SetDirty(sp);

        // Re-layout: 4 rows left, so the panel loses 3 rows (3 x 74) and the rows move down to keep the
        // same gap below the title. Buttons stay anchored to the bottom.
        var prt = (RectTransform)panel;
        if (removed > 0 && prt.sizeDelta.y > 800f)
        {
            Undo.RecordObject(prt, "Resize settings panel");
            prt.sizeDelta = new Vector2(prt.sizeDelta.x, prt.sizeDelta.y - 222f);
            foreach (string row in new[] { "Master Volume", "Music", "Sound Effects", "Mouse Sensitivity" })
                foreach (string part in new[] { " Label", " Slider", " Value" })
                {
                    var rt = panel.Find(row + part) as RectTransform;
                    if (rt == null) continue;
                    Undo.RecordObject(rt, "Move settings row");
                    rt.anchoredPosition += new Vector2(0f, -111f);
                }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(Tag + "Lobby settings: removed " + removed + " display objects, panel " + prt.sizeDelta.y + " tall. Saved MainMenu.");
        if (!string.IsNullOrEmpty(back) && back != scene.path) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
    }

    // ================================================================== props fix
    static bool dryRun;

    [MenuItem("Code Breaker/Fix Pass/3a. Preview Fix Props (no changes)", false, 329)]
    static void PreviewFixProps() { dryRun = true; try { RunFixProps(); } finally { dryRun = false; } }

    [MenuItem("Code Breaker/Fix Pass/3. Fix Props (open scene)", false, 330)]
    static void FixProps() { dryRun = false; RunFixProps(); }

    static void RunFixProps()
    {
        var scene = SceneManager.GetActiveScene();
        var log = new StringBuilder();
        log.AppendLine((dryRun ? "PREVIEW (nothing changed) " : "FIX ") + scene.name);
        Physics.SyncTransforms();
        int ds = MakeOpenShellsDoubleSided(log);
        int cols = AddDecorColliders(log);
        Physics.SyncTransforms();
        int moved = FixKnownClipping(log) + SettleOnSurfaces(log) + PullOutOfWalls(log);
        Physics.SyncTransforms();
        int limits = SetOpenLimits(log);
        int doors = LimitDoorSwings(log);
        int windows = ApplyWindowView(log);
        if (!dryRun)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        Directory.CreateDirectory(Logs);
        string file = "CB_" + (dryRun ? "Preview" : "Fix") + "Log_" + scene.name.Replace(" ", "_") + ".txt";
        File.WriteAllText(Path.Combine(Logs, file), log.ToString());
        Debug.Log(Tag + (dryRun ? "PREVIEW " : "") + scene.name + ": " + ds + " open shells double-sided, " + cols + " decor colliders, " + moved + " props moved, " +
                  limits + " drawers/doors limited, " + doors + " room door swings limited, " + windows + " window panes." + (dryRun ? "" : " Saved.") + " Details in Logs/" + file);
    }

    // Which way is "out of the furniture" for this part: away from the wall the furniture stands against.
    static bool Outward(L2Openable o, out Vector3 outward, out Vector3 n)
    {
        outward = n = Vector3.zero;
        var box = o.GetComponent<BoxCollider>();
        if (box == null) return false;
        Transform pt = o.transform;
        Vector3 center = pt.TransformPoint(box.center);
        // the part's thin horizontal axis = its front normal
        Vector3 sx = pt.right * Mathf.Abs(box.size.x * pt.lossyScale.x), sz = pt.forward * Mathf.Abs(box.size.z * pt.lossyScale.z);
        Vector3 thin = sx.magnitude < sz.magnitude ? sx : sz;
        thin.y = 0f;
        if (thin.sqrMagnitude < 1e-8f) return false;
        n = thin.normalized;
        float best = 0f;
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (c.isTrigger || !(c.name.Contains("Wall") && !c.name.Contains("Clock"))) continue;
            Bounds wb = c.bounds;
            if (wb.SqrDistance(center) > 6f * 6f) continue;
            Vector3 wn = wb.size.x < wb.size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(wn, center - wb.center) < 0f) wn = -wn;
            float a = Mathf.Abs(Vector3.Dot(wn, n));
            float dist = Mathf.Sqrt(wb.SqrDistance(center));
            float score = a - dist * 0.05f;
            if (a > 0.7f && score > best) { best = score; outward = wn; }
        }
        return outward != Vector3.zero;
    }

    // A drawer that slides into its cabinet, or a door that swings inwards, is a broken animation.
    static bool OpensInwards(L2Openable o, out string why)
    {
        why = null;
        if (!Outward(o, out Vector3 outward, out _)) return false;
        Transform pt = o.transform;
        if (o.kind == L2Openable.Kind.Drawer)
        {
            float d = Vector3.Dot(o.slide.normalized, outward);
            if (d < -0.5f) { why = "slides towards the wall (" + V(o.slide) + ")"; return true; }
            return false;
        }
        var box = o.GetComponent<BoxCollider>();
        Vector3 c = box.center;
        // free edge: the end of the box farthest from the hinge
        Vector3 lmin = box.center - box.size * 0.5f, lmax = box.center + box.size * 0.5f;
        float ex = Mathf.Max(Mathf.Abs(lmin.x), Mathf.Abs(lmax.x)), ez = Mathf.Max(Mathf.Abs(lmin.z), Mathf.Abs(lmax.z));
        Vector3 edge = ex >= ez ? new Vector3(Mathf.Abs(lmax.x) >= Mathf.Abs(lmin.x) ? lmax.x : lmin.x, c.y, c.z)
                                : new Vector3(c.x, c.y, Mathf.Abs(lmax.z) >= Mathf.Abs(lmin.z) ? lmax.z : lmin.z);
        Vector3 w0 = pt.TransformPoint(edge);
        Vector3 w1 = pt.position + Quaternion.AngleAxis(o.openAngle * 0.15f, Vector3.up) * (w0 - pt.position);
        Vector3 move = w1 - w0;
        if (move.sqrMagnitude < 1e-8f) return false;
        if (Vector3.Dot(move.normalized, outward) < -0.5f) { why = "swings towards the wall (openAngle " + o.openAngle + ")"; return true; }
        return false;
    }

    // ---- drawers and cabinet doors: stop them before they pass into something
    static int SetOpenLimits(StringBuilder log)
    {
        // wrong opening direction first
        foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
        {
            if (!OpensInwards(o, out string why)) continue;
            log.AppendLine("DIRECTION " + PathOf(o.transform) + " " + why + (dryRun ? " -> would be reversed" : " -> reversed"));
            if (dryRun) continue;
            Undo.RecordObject(o, "Opening direction");
            if (o.kind == L2Openable.Kind.Drawer) o.slide = -o.slide; else o.openAngle = -o.openAngle;
            EditorUtility.SetDirty(o);
        }
        var obstacles = CollectObstacles();
        int n = 0;
        foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
        {
            float lim = OpenLimit(o, obstacles, out string hit);
            float v = lim >= 0.999f ? 1f : Mathf.Floor(lim * 100f) / 100f;
            log.AppendLine("OPEN " + PathOf(o.transform) + " " + o.kind + " free=" + lim.ToString("0.00") + (hit != null ? " (stops at " + hit + ")" : "") +
                           (Mathf.Abs(o.maxOpen - v) > 0.001f ? "  maxOpen " + o.maxOpen.ToString("0.00") + " -> " + v.ToString("0.00") : ""));
            bool blocked = v < 0.12f && !IsContainer(o);
            if (blocked) log.AppendLine("  -> it can't open (something stands right in front of it) and holds nothing: E-interaction switched off");
            if (Mathf.Abs(o.maxOpen - v) <= 0.001f && !(blocked && o.enabled)) continue;
            if (v < 1f) n++;
            if (dryRun) continue;
            Undo.RecordObject(o, "Open limit");
            o.maxOpen = v;
            if (blocked) o.enabled = false;
            EditorUtility.SetDirty(o);
        }
        return n;
    }

    static bool IsContainer(L2Openable o)
    {
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (mb == null || mb == o) continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null &&
                    (it.objectReferenceValue == o || it.objectReferenceValue == o.transform || it.objectReferenceValue == o.gameObject))
                    return true;
        }
        return false;
    }

    // ---- small props sunk into the surface they stand on: lift them onto it
    static readonly string[] SettleNames = { "KeyBowl", "Flashlight", "Clipboard" };

    static int SettleOnSurfaces(StringBuilder log)
    {
        int n = 0;
        var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r => MeshOf(r) != null).ToArray();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!SettleNames.Contains(t.name)) continue;
            var own = t.GetComponentsInChildren<MeshRenderer>().Where(r => MeshOf(r) != null && MeshOf(r).triangles.Length > 0).ToArray();
            if (own.Length == 0) continue;
            Bounds b = own[0].bounds;
            foreach (var r in own) b.Encapsulate(r.bounds);
            MeshRenderer support = all.Where(r => !r.transform.IsChildOf(t) && r.bounds.max.y > b.min.y - 0.002f && r.bounds.max.y < b.min.y + 0.15f &&
                                                  r.bounds.min.x < b.center.x && r.bounds.max.x > b.center.x && r.bounds.min.z < b.center.z && r.bounds.max.z > b.center.z &&
                                                  r.bounds.size.x * r.bounds.size.z > b.size.x * b.size.z)
                                      .OrderByDescending(r => r.bounds.max.y).FirstOrDefault();
            if (support == null) continue;
            float lift = support.bounds.max.y - b.min.y + 0.004f;
            if (lift <= 0.004f) continue;
            log.AppendLine("LIFT " + PathOf(t) + " by " + lift.ToString("0.000") + " onto " + support.name);
            n++;
            if (dryRun) continue;
            Undo.RecordObject(t, "Lift prop");
            t.position += Vector3.up * lift;
            EditorUtility.SetDirty(t);
        }
        return n;
    }

    // ---- contents of lockers / wall cabinets that ended up inside the wall behind them
    static int PullOutOfWalls(StringBuilder log)
    {
        int n = 0;
        var walls = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude)
            .Where(c => !c.isTrigger && c.name.StartsWith("Wall_") && !c.name.Contains("Jamb")).ToArray();
        foreach (Transform inside in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (inside.name != "Inside" || inside.parent == null) continue;
            if (!TryMeshBounds(inside, out Bounds b)) continue;
            BoxCollider wall = walls.FirstOrDefault(w => w.bounds.Contains(b.center));
            if (wall == null) continue;
            Bounds wb = wall.bounds;
            int axis = wb.size.x < wb.size.z ? 0 : 2;
            // room side = where the cabinet's door is
            Transform door = inside.parent.Find("DoorPivot");
            float roomCoord = door != null ? door.position[axis] : inside.parent.position[axis];
            float sign = Mathf.Sign(roomCoord - wb.center[axis]);
            float face = wb.center[axis] + sign * wb.extents[axis];
            float back = sign > 0 ? b.min[axis] : b.max[axis];
            float delta = face + sign * 0.02f - back;
            if (delta * sign <= 0f) continue;
            Vector3 move = Vector3.zero;
            move[axis] = delta;
            log.AppendLine("PULL " + PathOf(inside) + " out of " + wall.name + " by " + V(move));
            n++;
            if (dryRun) continue;
            Undo.RecordObject(inside, "Pull out of wall");
            inside.position += move;
            EditorUtility.SetDirty(inside);
        }
        return n;
    }

    // World bounds of all meshes under t, active or not (inactive renderers report no bounds).
    static bool TryMeshBounds(Transform t, out Bounds b)
    {
        b = default;
        bool has = false;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null || m.vertexCount == 0) continue;
            Bounds lb = m.bounds;
            Matrix4x4 M = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = M.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z));
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            }
        }
        return has;
    }

    // ---- 1. open shells (lamp shades, bowls, vases ...) seen from inside / below vanish: render both faces
    static int MakeOpenShellsDoubleSided(StringBuilder log)
    {
        int n = 0;
        var clones = new Dictionary<Material, Material>();
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            string p = PathOf(r.transform);
            if (!IsPropPath(p)) continue;
            Mesh m = MeshOf(r);
            if (m == null || !m.isReadable) continue;
            Vector3 sz = m.bounds.size;
            if (Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)) < 0.02f) continue;   // flat quads (screens, posters) are meant to be one-sided
            string issue = ShellIssues(m);
            if (issue.Length == 0) continue;
            if (issue.Contains("flipped=") && !issue.Contains("flipped=0 "))
            {
                if (!fixedMeshes.Contains(m))
                {
                    fixedMeshes.Add(m);
                    int wrong = WrongWindingCount(m);
                    if (wrong > 0)
                    {
                        if (!dryRun) FixWinding(m);
                        log.AppendLine("MESH " + m.name + issue + " -> " + wrong + " triangle(s) facing the wrong way" + (dryRun ? " (would be turned round)" : " turned round"));
                    }
                }
                if (!dryRun) issue = ShellIssues(m);
            }
            // Only real openings (lamp shades, bowls, curtains ...) need both faces drawn.
            if (!issue.Contains("open=") || issue.Contains("open=0 ")) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null || !src.HasProperty("_Cull") || src.GetFloat("_Cull") == 0f) continue;
                if (!clones.TryGetValue(src, out Material dst))
                {
                    dst = dryRun ? src : DoubleSidedClone(src);
                    clones[src] = dst;
                }
                if (dst == null) continue;
                mats[i] = dst;
                changed = true;
            }
            if (!changed) continue;
            log.AppendLine("DOUBLE-SIDED " + p + issue);
            n++;
            if (dryRun) continue;
            Undo.RecordObject(r, "Double-sided");
            r.sharedMaterials = mats;
            if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            EditorUtility.SetDirty(r);
        }
        return n;
    }

    static readonly HashSet<Mesh> fixedMeshes = new HashSet<Mesh>();

    // Triangles whose winding disagrees with their vertex normals are culled from the side they are
    // meant to be seen from (they vanish depending on the angle). Count / turn them round.
    static int WrongWindingCount(Mesh m)
    {
        Vector3[] v = m.vertices, nr = m.normals;
        if (nr == null || nr.Length != v.Length) return 0;
        int bad = 0;
        for (int sm = 0; sm < m.subMeshCount; sm++)
        {
            int[] t = m.GetTriangles(sm);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (g.sqrMagnitude < 1e-14f) continue;
                if (Vector3.Dot(g, nr[t[i]] + nr[t[i + 1]] + nr[t[i + 2]]) < 0f) bad++;
            }
        }
        return bad;
    }

    static void FixWinding(Mesh m)
    {
        Vector3[] v = m.vertices, nr = m.normals;
        Undo.RecordObject(m, "Fix triangle winding");
        for (int sm = 0; sm < m.subMeshCount; sm++)
        {
            int[] t = m.GetTriangles(sm);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (g.sqrMagnitude < 1e-14f) continue;
                if (Vector3.Dot(g, nr[t[i]] + nr[t[i + 1]] + nr[t[i + 2]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            m.SetTriangles(t, sm);
        }
        EditorUtility.SetDirty(m);
    }

    // Reverses the triangles (and normals) of every connected piece of the mesh whose faces point inwards.
    static int TurnInsideOutPartsRightWayOut(Mesh m)
    {
        Vector3[] v = m.vertices;
        Vector3[] nrm = m.normals;
        var id = new Dictionary<Vector3Int, int>();
        int[] remap = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
            if (!id.TryGetValue(k, out int n)) { n = id.Count; id[k] = n; }
            remap[i] = n;
        }
        var parent = new int[id.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        var subTris = new List<int[]>();
        for (int sm = 0; sm < m.subMeshCount; sm++) subTris.Add(m.GetTriangles(sm));
        foreach (int[] t in subTris)
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = Find(remap[t[i]]), b = Find(remap[t[i + 1]]), c = Find(remap[t[i + 2]]);
                parent[b] = a; parent[Find(c)] = a;
            }
        var vol = new Dictionary<int, float>();
        foreach (int[] t in subTris)
            for (int i = 0; i < t.Length; i += 3)
            {
                int root = Find(remap[t[i]]);
                vol.TryGetValue(root, out float s0);
                vol[root] = s0 + Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]])) / 6f;
            }
        var und = new Dictionary<long, int>();
        foreach (int[] t in subTris)
            for (int i = 0; i < t.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = remap[t[i + e]], b = remap[t[i + (e + 1) % 3]];
                    if (a == b) continue;
                    long uk = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    und.TryGetValue(uk, out int c); und[uk] = c + 1;
                }
        var openRoots = new HashSet<int>();
        foreach (var kv in und) if (kv.Value == 1) openRoots.Add(Find((int)(kv.Key >> 32)));
        var bad = new HashSet<int>(vol.Where(kv => kv.Value < -1e-7f && !openRoots.Contains(kv.Key)).Select(kv => kv.Key));
        if (bad.Count == 0) return 0;
        string path = AssetDatabase.GetAssetPath(m);
        Undo.RecordObject(m, "Fix inside-out mesh");
        var flipV = new bool[v.Length];
        for (int sm = 0; sm < subTris.Count; sm++)
        {
            int[] t = subTris[sm];
            for (int i = 0; i < t.Length; i += 3)
            {
                if (!bad.Contains(Find(remap[t[i]]))) continue;
                (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                flipV[t[i]] = flipV[t[i + 1]] = flipV[t[i + 2]] = true;
            }
            m.SetTriangles(t, sm);
        }
        if (nrm != null && nrm.Length == v.Length)
        {
            for (int i = 0; i < nrm.Length; i++) if (flipV[i]) nrm[i] = -nrm[i];
            m.normals = nrm;
        }
        EditorUtility.SetDirty(m);
        return bad.Count;
    }

    static Material DoubleSidedClone(Material src)
    {
        if (src.name.EndsWith("_DoubleSided")) return src;
        EnsureFolder(MatDir);
        string path = MatDir + "/" + src.name + "_DoubleSided.mat";
        Material dst = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (dst == null)
        {
            dst = new Material(src) { name = src.name + "_DoubleSided" };
            AssetDatabase.CreateAsset(dst, path);
        }
        dst.SetFloat("_Cull", 0f);
        dst.doubleSidedGI = true;
        EditorUtility.SetDirty(dst);
        return dst;
    }

    // ---- 2. solid colliders for free-standing decor (lamps, plants, TV, bins ...) that has none
    static readonly string[] NoColliderWords =
        { "Window", "Curtain", "Painting", "Photo", "Clock", "Calendar", "Corkboard", "Whiteboard", "Towel", "Mirror",
          "CeilingLight", "PendantLight", "RangeHood", "Rug", "Carpet", "Poster", "Sticky", "SinkInnerWalls", "WallShelves",
          "Containers_WallShelf", "CoatHooks", "TableSetting", "Smoke", "Vent", "Countertop", "Jamb", "Wall_" };

    static int AddDecorColliders(StringBuilder log)
    {
        var solids = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude).Where(c => !c.isTrigger && c.enabled).ToArray();
        int n = 0;
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            string p = PathOf(r.transform);
            if (!IsPropPath(p) || !r.enabled) continue;
            if (NoColliderWords.Any(w => r.name.Contains(w))) continue;
            if (r.GetComponentInParent<L2Interactable>(true) != null) continue;                  // puzzle items / pick-ups keep their triggers
            if (r.GetComponentsInParent<Collider>(true).Any(c => !c.isTrigger)) continue;      // already solid
            if (r.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger)) continue;
            if (r.GetComponentInParent<L2Openable>(true) != null) continue;                    // moving parts
            Mesh m = MeshOf(r);
            if (m == null || !m.isReadable) continue;
            Bounds b = r.bounds;
            if (b.size.y < 0.25f || Mathf.Max(b.size.x, b.size.z) < 0.5f || Mathf.Min(b.size.x, b.size.z) < 0.15f) continue;
            if (b.min.y > 4.2f) continue;                                                       // above head height: nothing to bump into
            if (b.size.x * b.size.z > 40f) continue;                                            // big architecture-sized meshes
            if (CoveredBySolid(b, solids) > 0.6f) continue;                                     // e.g. duvet inside the bed collider

            if (dryRun) { log.AppendLine("COLLIDER " + p + " boxes=" + FittedBoxes(m).Count); n++; continue; }
            var go = new GameObject(ColliderName);
            Undo.RegisterCreatedObjectUndo(go, "Decor collider");
            go.transform.SetParent(r.transform, false);
            go.layer = r.gameObject.layer;
            go.isStatic = r.gameObject.isStatic;
            foreach (Bounds band in FittedBoxes(m))
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = band.center;
                bc.size = band.size;
            }
            log.AppendLine("COLLIDER " + p + " boxes=" + go.GetComponents<BoxCollider>().Length);
            n++;
        }
        return n;
    }

    static float CoveredBySolid(Bounds b, Collider[] solids)
    {
        float best = 0f, vol = Mathf.Max(1e-6f, b.size.x * b.size.y * b.size.z);
        foreach (Collider c in solids)
        {
            Bounds cb = c.bounds;
            if (!cb.Intersects(b)) continue;
            Vector3 lo = Vector3.Max(b.min, cb.min), hi = Vector3.Min(b.max, cb.max);
            Vector3 d = hi - lo;
            if (d.x <= 0 || d.y <= 0 || d.z <= 0) continue;
            best = Mathf.Max(best, d.x * d.y * d.z / vol);
        }
        return best;
    }

    // Splits the mesh into horizontal bands and fits a box to each (base / pole / shade of a lamp,
    // pot / leaves of a plant), merging bands of similar width.
    static List<Bounds> FittedBoxes(Mesh m)
    {
        Vector3[] v = m.vertices;
        int[] t = m.triangles;
        Bounds mb = m.bounds;
        const int bands = 6;
        float h = mb.size.y / bands;
        var boxes = new Bounds?[bands];
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            float y0 = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), y1 = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            for (int k = 0; k < bands; k++)
            {
                float lo = mb.min.y + k * h, hi = lo + h;
                if (y1 < lo || y0 > hi) continue;
                foreach (Vector3 p in new[] { a, b, c })
                {
                    var q = new Vector3(p.x, Mathf.Clamp(p.y, lo, hi), p.z);
                    if (boxes[k] == null) boxes[k] = new Bounds(q, Vector3.zero);
                    else { var bb = boxes[k].Value; bb.Encapsulate(q); boxes[k] = bb; }
                }
            }
        }
        var result = new List<Bounds>();
        for (int k = 0; k < bands; k++)
        {
            if (boxes[k] == null) continue;
            var b = boxes[k].Value;
            float lo = mb.min.y + k * h;
            b.SetMinMax(new Vector3(b.min.x, lo, b.min.z), new Vector3(b.max.x, lo + h, b.max.z));
            if (result.Count > 0)
            {
                var prev = result[result.Count - 1];
                bool similar = Mathf.Abs(prev.size.x - b.size.x) < 0.2f * Mathf.Max(prev.size.x, b.size.x) + 0.05f &&
                               Mathf.Abs(prev.size.z - b.size.z) < 0.2f * Mathf.Max(prev.size.z, b.size.z) + 0.05f &&
                               Mathf.Abs(prev.max.y - b.min.y) < 0.001f;
                if (similar) { prev.Encapsulate(b); result[result.Count - 1] = prev; continue; }
            }
            result.Add(b);
        }
        if (result.Count == 0) result.Add(mb);
        return result;
    }

    // ---- 3. props that clip into each other (book under the bedside lamp ...)
    static readonly string[] MoveIfClipping = { "BED_Book_OnBedsideTable" };

    static int FixKnownClipping(StringBuilder log)
    {
        int n = 0;
        var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r => MeshOf(r) != null && MeshOf(r).isReadable && IsPropPath(PathOf(r.transform))).ToArray();
        foreach (string name in MoveIfClipping)
        {
            MeshRenderer a = all.FirstOrDefault(r => r.name == name);
            if (a == null) continue;
            if (Relocate(a, all, log)) n++;
        }
        return n;
    }

    // Moves a small prop to the nearest free spot on the surface it stands on.
    static bool Relocate(MeshRenderer a, MeshRenderer[] all, StringBuilder log)
    {
        Transform t = a.transform;
        Bounds ab = a.bounds;
        // the support: the biggest renderer right under the prop's base
        MeshRenderer support = all.Where(r => r != a && !r.transform.IsChildOf(t) && Mathf.Abs(r.bounds.max.y - ab.min.y) < 0.06f &&
                                              r.bounds.min.x < ab.center.x && r.bounds.max.x > ab.center.x &&
                                              r.bounds.min.z < ab.center.z && r.bounds.max.z > ab.center.z)
                                  .OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).FirstOrDefault();
        if (support == null) { log.AppendLine("MOVE " + a.name + ": no support found"); return false; }
        Bounds top = support.bounds;
        var near = all.Where(r => r != a && r != support && !r.transform.IsChildOf(t) && r.bounds.Intersects(new Bounds(top.center, top.size + new Vector3(0.2f, 3f, 0.2f)))).ToArray();
        if (Clear(a, near)) { log.AppendLine("MOVE " + a.name + ": already clear"); return false; }

        Vector3 p0 = t.position;
        Quaternion r0 = t.rotation;
        Vector3 bestP = p0; Quaternion bestR = r0; float bestD = float.MaxValue;
        float[] angles = { 0f, 90f, -90f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 75f, -75f };
        const float step = 0.04f;
        foreach (float ang in angles)
        {
            Quaternion r = Quaternion.AngleAxis(ang, Vector3.up) * r0;
            for (float x = top.min.x; x <= top.max.x; x += step)
                for (float z = top.min.z; z <= top.max.z; z += step)
                {
                    Vector3 p = new Vector3(x, p0.y, z);
                    float d = (new Vector2(x, z) - new Vector2(p0.x, p0.z)).magnitude + Mathf.Abs(ang) * 0.002f;
                    if (d >= bestD) continue;
                    t.SetPositionAndRotation(p, r);
                    Bounds nb = a.bounds;
                    if (nb.min.x < top.min.x + 0.03f || nb.max.x > top.max.x - 0.03f || nb.min.z < top.min.z + 0.03f || nb.max.z > top.max.z - 0.03f) continue;
                    if (!Clear(a, near)) continue;
                    bestD = d; bestP = p; bestR = r;
                }
        }
        t.SetPositionAndRotation(p0, r0);
        if (bestD == float.MaxValue) { log.AppendLine("MOVE " + a.name + ": no free spot on " + support.name); return false; }
        if (dryRun) { log.AppendLine("MOVE " + PathOf(t) + " " + V(p0) + " -> " + V(bestP) + " turned " + Quaternion.Angle(r0, bestR).ToString("0") + " deg on " + support.name); return true; }
        Undo.RecordObject(t, "Move prop");
        t.SetPositionAndRotation(bestP, bestR);
        EditorUtility.SetDirty(t);
        log.AppendLine("MOVE " + PathOf(t) + " " + V(p0) + " -> " + V(bestP) + " turned " + Quaternion.Angle(r0, bestR).ToString("0") + " deg on " + support.name);
        return true;
    }

    static bool Clear(MeshRenderer a, MeshRenderer[] near)
    {
        Mesh am = MeshOf(a);
        foreach (MeshRenderer b in near)
        {
            if (!a.bounds.Intersects(b.bounds)) continue;
            Mesh bm = MeshOf(b);
            if (CountInside(a, Samples(am), b, bm.vertices, bm.triangles, out _) > 0) return false;
            if (CountInside(b, Samples(bm), a, am.vertices, am.triangles, out _) > 0) return false;
        }
        return true;
    }

    // ---- 4. room doors that swing through furniture: stop the swing just before it
    static int LimitDoorSwings(StringBuilder log)
    {
        int n = 0;
        foreach (L2Door d in Object.FindObjectsByType<L2Door>(FindObjectsInactive.Include))
        {
            string info = DoorSwingInfo(d, out float free);
            float total = Mathf.DeltaAngle(d.closedAngle, d.openAngle);
            log.AppendLine("DOOR " + PathOf(d.transform) + " " + info);
            if (Mathf.Abs(free) >= Mathf.Abs(total) - 0.01f) continue;
            if (Mathf.Abs(free) < 75f) { log.AppendLine("  !! free swing under 75 deg, left unchanged (needs a look)"); continue; }
            if (dryRun) { n++; continue; }
            Undo.RecordObject(d, "Limit door swing");
            d.openAngle = d.closedAngle + free - Mathf.Sign(total) * 2f;
            EditorUtility.SetDirty(d);
            log.AppendLine("  openAngle -> " + d.openAngle.ToString("0.#"));
            n++;
        }
        return n;
    }

    // ---- 5. windows: a real outdoor view instead of the flat picture
    static int ApplyWindowView(StringBuilder log)
    {
        Material view = AssetDatabase.LoadAssetAtPath<Material>(ViewMatPath);
        if (view == null) { log.AppendLine("WINDOW: no outdoor view material yet (run Build Outdoor Window View first)"); return 0; }
        int n = 0;
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("L2_Window_DaylightView")) { mats[i] = view; changed = true; }
            if (!changed) continue;
            if (dryRun) { log.AppendLine("WINDOW " + PathOf(r.transform)); n++; continue; }
            Undo.RecordObject(r, "Window view");
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
            log.AppendLine("WINDOW " + PathOf(r.transform));
            n++;
        }
        return n;
    }

    // ================================================================== outdoor view
    [MenuItem("Code Breaker/Fix Pass/4. Build Outdoor Window View", false, 340)]
    static void BuildOutdoorView()
    {
        EnsureFolder(MatDir);
        Shader sh = Shader.Find(ViewShaderName);
        if (sh == null) { Debug.LogError(Tag + "Shader '" + ViewShaderName + "' not found"); return; }

        Texture2D pano = RenderGardenPanorama(2048, 1024);
        File.WriteAllBytes(ViewTexPath, pano.EncodeToPNG());
        Object.DestroyImmediate(pano);
        AssetDatabase.ImportAsset(ViewTexPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(ViewTexPath);
        ti.mipmapEnabled = false;
        ti.wrapModeU = TextureWrapMode.Repeat;
        ti.wrapModeV = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();

        Material m = AssetDatabase.LoadAssetAtPath<Material>(ViewMatPath);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, ViewMatPath); }
        m.shader = sh;
        m.SetTexture("_Pano", AssetDatabase.LoadAssetAtPath<Texture2D>(ViewTexPath));
        m.SetFloat("_Exposure", 0.9f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Debug.Log(Tag + "Outdoor window view built: " + ViewTexPath);
    }

    // Builds a small garden far below the level, renders it in 6 directions and folds that into a
    // 360-degree panorama (sky drawn procedurally). Everything built is removed again afterwards.
    static Texture2D RenderGardenPanorama(int W, int H)
    {
        Vector3 origin = new Vector3(0f, -6000f, 0f);
        var root = new GameObject("CB_GardenCapture") { hideFlags = HideFlags.DontSave };
        var camGo = new GameObject("CB_GardenCamera") { hideFlags = HideFlags.DontSave };
        var sunGo = new GameObject("CB_GardenSun") { hideFlags = HideFlags.DontSave };
        var disabledLights = new List<Light>();
        bool fog = RenderSettings.fog;
        var ambMode = RenderSettings.ambientMode;
        var ambProbe = RenderSettings.ambientProbe;
        var faces = new List<(Texture2D tex, Vector3 f, Vector3 r, Vector3 u)>();
        const float fov = 100f;
        const int faceRes = 1024;
        Color skyKey = new Color(1f, 0f, 1f);
        try
        {
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && l.enabled) { l.enabled = false; disabledLights.Add(l); }
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(0.66f, 0.71f, 0.78f));
            RenderSettings.ambientProbe = sh;

            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sunGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            BuildGarden(root.transform, origin);

            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyKey;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 4000f;
            cam.fieldOfView = fov;
            cam.aspect = 1f;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            camGo.transform.position = origin + Vector3.up * 6.5f;
            var rt = new RenderTexture(faceRes, faceRes, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Vector3[] fw = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            Vector3[] up = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.back, Vector3.forward };
            for (int i = 0; i < 6; i++)
            {
                camGo.transform.rotation = Quaternion.LookRotation(fw[i], up[i]);
                cam.Render();   // first render warms up the pipeline (the very first one can come out empty)
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(faceRes, faceRes, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, faceRes, faceRes), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                faces.Add((tex, camGo.transform.forward, camGo.transform.right, camGo.transform.up));
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(sunGo);
            foreach (Light l in disabledLights) if (l != null) l.enabled = true;
            RenderSettings.fog = fog;
            RenderSettings.ambientMode = ambMode;
            RenderSettings.ambientProbe = ambProbe;
        }

        // fold the faces into an equirectangular panorama
        float tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        var px = faces.Select(f => f.tex.GetPixels32()).ToArray();
        var outTex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var outPx = new Color[W * H];
        Vector3 sunDir = -(Quaternion.Euler(55f, -35f, 0f) * Vector3.forward);
        for (int j = 0; j < H; j++)
        {
            float lat = ((j + 0.5f) / H - 0.5f) * Mathf.PI;
            for (int i = 0; i < W; i++)
            {
                float lon = ((i + 0.5f) / W - 0.5f) * 2f * Mathf.PI;
                Vector3 d = new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
                int best = 0; float bd = -2f;
                for (int k = 0; k < 6; k++) { float dd = Vector3.Dot(d, faces[k].f); if (dd > bd) { bd = dd; best = k; } }
                var fc = faces[best];
                float u = 0.5f + 0.5f * Vector3.Dot(d, fc.r) / (bd * tanHalf);
                float v = 0.5f + 0.5f * Vector3.Dot(d, fc.u) / (bd * tanHalf);
                Color sky = SkyColor(d, sunDir);
                outPx[j * W + i] = SampleKeyed(px[best], faceRes, u, v, sky);
            }
        }
        foreach (var f in faces) Object.DestroyImmediate(f.tex);
        outTex.SetPixels(outPx);
        outTex.Apply();
        return outTex;
    }

    static Color SampleKeyed(Color32[] p, int res, float u, float v, Color sky)
    {
        float x = Mathf.Clamp(u * res - 0.5f, 0f, res - 1.001f), y = Mathf.Clamp(v * res - 0.5f, 0f, res - 1.001f);
        int x0 = (int)x, y0 = (int)y;
        float fx = x - x0, fy = y - y0;
        Color c00 = Key(p[y0 * res + x0], sky), c10 = Key(p[y0 * res + x0 + 1], sky);
        Color c01 = Key(p[(y0 + 1) * res + x0], sky), c11 = Key(p[(y0 + 1) * res + x0 + 1], sky);
        return Color.Lerp(Color.Lerp(c00, c10, fx), Color.Lerp(c01, c11, fx), fy);
    }

    static Color Key(Color32 c, Color sky) => (c.r > 245 && c.g < 12 && c.b > 245) ? sky : (Color)c;

    static Color SkyColor(Vector3 d, Vector3 sunDir)
    {
        float h = Mathf.Clamp01(d.y);
        Color horizon = new Color(0.86f, 0.9f, 0.93f), zenith = new Color(0.36f, 0.58f, 0.86f);
        Color c = Color.Lerp(horizon, zenith, Mathf.Pow(h, 0.55f));
        // soft clouds
        if (d.y > 0.02f)
        {
            Vector3 q = d / Mathf.Max(0.12f, d.y) * 1.6f;   // project onto a cloud layer
            float n = Fbm(q.x + 13.1f, q.z - 7.3f);
            float cover = Mathf.SmoothStep(0.52f, 0.78f, n) * Mathf.SmoothStep(0.02f, 0.25f, d.y);
            c = Color.Lerp(c, new Color(0.97f, 0.97f, 0.98f), cover * 0.85f);
        }
        float s = Mathf.Max(0f, Vector3.Dot(d, sunDir));
        c += new Color(1f, 0.92f, 0.75f) * (Mathf.Pow(s, 64f) * 0.6f + Mathf.Pow(s, 8f) * 0.08f);
        if (d.y < 0f) c = Color.Lerp(horizon, new Color(0.42f, 0.5f, 0.36f), Mathf.Clamp01(-d.y * 4f));   // below the horizon (hidden by the ground anyway)
        c.a = 1f;
        return c;
    }

    static float Fbm(float x, float y)
    {
        float s = 0f, a = 0.5f;
        for (int i = 0; i < 5; i++) { s += a * Noise(x, y); x *= 2.03f; y *= 2.03f; a *= 0.5f; }
        return s;
    }

    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        float Hs(int a, int b) { uint n = (uint)(a * 374761393 + b * 668265263); n = (n ^ (n >> 13)) * 1274126177u; return ((n ^ (n >> 16)) & 0xffff) / 65535f; }
        float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hs(xi, yi), Hs(xi + 1, yi), u), Mathf.Lerp(Hs(xi, yi + 1), Hs(xi + 1, yi + 1), u), v);
    }

    static void BuildGarden(Transform root, Vector3 o)
    {
        var rnd = new System.Random(1234);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material M(Color c, float smooth = 0.15f) { var m = new Material(lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); return m; }

        GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.hideFlags = HideFlags.DontSave;
            g.transform.SetParent(root, false);
            g.transform.SetPositionAndRotation(o + pos, rot ?? Quaternion.identity);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        Material grass = M(new Color(0.36f, 0.52f, 0.24f), 0.05f);
        Material grass2 = M(new Color(0.42f, 0.58f, 0.27f), 0.05f);
        Material path = M(new Color(0.7f, 0.66f, 0.58f), 0.1f);
        Material wood = M(new Color(0.9f, 0.88f, 0.82f), 0.2f);   // painted fence
        Material bark = M(new Color(0.33f, 0.24f, 0.17f), 0.05f);
        Material[] leaves = { M(new Color(0.2f, 0.42f, 0.18f), 0.1f), M(new Color(0.26f, 0.48f, 0.2f), 0.1f), M(new Color(0.18f, 0.36f, 0.2f), 0.1f), M(new Color(0.33f, 0.5f, 0.2f), 0.1f) };
        Material hedge = M(new Color(0.16f, 0.34f, 0.16f), 0.05f);
        Material[] flowers = { M(new Color(0.9f, 0.35f, 0.4f)), M(new Color(0.95f, 0.85f, 0.3f)), M(new Color(0.75f, 0.5f, 0.9f)), M(new Color(0.98f, 0.98f, 0.95f)) };
        Material hills = M(new Color(0.42f, 0.56f, 0.5f), 0.0f);
        Material hillsFar = M(new Color(0.58f, 0.68f, 0.72f), 0.0f);
        Material wall = M(new Color(0.85f, 0.8f, 0.72f), 0.1f), roof = M(new Color(0.55f, 0.27f, 0.22f), 0.1f), roof2 = M(new Color(0.35f, 0.36f, 0.4f), 0.1f);

        // lawn
        Prim(PrimitiveType.Plane, Vector3.zero, new Vector3(400f, 1f, 400f), grass);
        for (int i = 0; i < 40; i++)   // mottled lawn patches
            Prim(PrimitiveType.Cylinder, new Vector3(R(-140, 140), 0.02f, R(-140, 140)), new Vector3(R(10, 30), 0.02f, R(10, 30)), grass2);
        // stone path leading away on the south side
        for (int i = 0; i < 14; i++)
            Prim(PrimitiveType.Cylinder, new Vector3(R(-1.5f, 1.5f) + 8f, 0.05f, -22f - i * 4.2f), new Vector3(2.6f, 0.08f, 2.2f), path);

        // picket fence around the garden (radius ~ 40 units = 11 m)
        const float fenceR = 42f;
        for (int i = 0; i < 160; i++)
        {
            float a = i / 160f * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * fenceR, 0f, Mathf.Sin(a) * fenceR);
            Quaternion r = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
            Prim(PrimitiveType.Cube, p + Vector3.up * 1.9f, new Vector3(0.45f, 3.8f, 0.18f), wood, r);
        }
        for (int k = 0; k < 2; k++)
            for (int i = 0; i < 64; i++)
            {
                float a0 = i / 64f * Mathf.PI * 2f, a1 = (i + 1) / 64f * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * fenceR, p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * fenceR;
                Vector3 mid = (p0 + p1) * 0.5f + Vector3.up * (1.2f + k * 1.6f);
                Prim(PrimitiveType.Cube, mid, new Vector3(0.12f, 0.3f, (p1 - p0).magnitude + 0.1f), wood, Quaternion.LookRotation(p1 - p0));
            }
        // hedges and flower beds just inside the fence
        for (int i = 0; i < 26; i++)
        {
            float a = R(0, Mathf.PI * 2f);
            float rr = R(33f, 39f);
            Vector3 p = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
            Prim(PrimitiveType.Sphere, p + Vector3.up * 1.4f, new Vector3(R(4, 7), R(3, 4.5f), R(4, 7)), hedge);
            for (int f = 0; f < 6; f++)
                Prim(PrimitiveType.Sphere, p + new Vector3(R(-3, 3), R(0.4f, 1f), R(-3, 3)) + (p.normalized * -3f), Vector3.one * R(0.5f, 0.9f), flowers[rnd.Next(flowers.Length)]);
        }
        // trees: inside the garden, beyond the fence and a wood further out
        for (int i = 0; i < 120; i++)
        {
            float a = R(0, Mathf.PI * 2f);
            float rr = i < 40 ? R(55f, 120f) : R(120f, 380f);
            Vector3 p = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
            float s = R(0.8f, 1.5f) * (rr > 110f ? 1.4f : 1f);
            Material lf = leaves[rnd.Next(leaves.Length)];
            if (rnd.NextDouble() < 0.3)
            {   // conifer
                Prim(PrimitiveType.Cylinder, p + Vector3.up * 4f * s, new Vector3(0.9f, 4f, 0.9f) * s, bark);
                for (int k = 0; k < 4; k++)
                    Prim(PrimitiveType.Sphere, p + Vector3.up * (8f + k * 4.2f) * s, new Vector3(9f - k * 2f, 6f, 9f - k * 2f) * s, leaves[2]);
            }
            else
            {   // broadleaf
                float th = R(7f, 10f) * s;
                Prim(PrimitiveType.Cylinder, p + Vector3.up * th * 0.5f, new Vector3(1.1f * s, th * 0.5f, 1.1f * s), bark);
                int blobs = rnd.Next(3, 6);
                for (int k = 0; k < blobs; k++)
                    Prim(PrimitiveType.Sphere, p + new Vector3(R(-3, 3), th + R(1, 5), R(-3, 3)) * s, Vector3.one * R(7f, 10f) * s, lf);
            }
        }
        // a few neighbouring houses beyond the trees
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f + R(-0.2f, 0.2f);
            float rr = R(150f, 230f);
            Vector3 p = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
            Quaternion r = Quaternion.LookRotation(-p.normalized);
            float w = R(30f, 45f), h = R(16f, 22f);
            Prim(PrimitiveType.Cube, p + Vector3.up * h * 0.5f, new Vector3(w, h, 26f), wall, r);
            Material rm = i % 2 == 0 ? roof : roof2;
            Prim(PrimitiveType.Cube, p + Vector3.up * (h + 3.5f), new Vector3(w + 3f, 1.2f, 19f), rm, r * Quaternion.Euler(28f, 0, 0) * Quaternion.Euler(0, 0, 0));
            Prim(PrimitiveType.Cube, p + Vector3.up * (h + 3.5f), new Vector3(w + 3f, 1.2f, 19f), rm, r * Quaternion.Euler(-28f, 0, 0));
        }
        // rolling hills on the horizon
        for (int i = 0; i < 22; i++)
        {
            float a = i / 22f * Mathf.PI * 2f + R(-0.1f, 0.1f);
            float rr = R(1400f, 1900f);
            Vector3 p = new Vector3(Mathf.Cos(a) * rr, -20f, Mathf.Sin(a) * rr);
            Prim(PrimitiveType.Sphere, p, new Vector3(R(500, 900), R(110, 220), R(400, 600)), i % 3 == 0 ? hillsFar : hills);
        }
    }

    // ================================================================== helpers
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Mesh MeshOf(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        return Readable(mf != null ? mf.sharedMesh : (r is SkinnedMeshRenderer sk ? sk.sharedMesh : null));
    }

    // The generated room meshes are saved without Read/Write, so their vertices can't be read by
    // these tools. Switch it on (editor-side setting only; nothing changes in how they look).
    static readonly HashSet<string> madeReadable = new HashSet<string>();

    static Mesh Readable(Mesh m)
    {
        if (m == null || m.isReadable) return m;
        string path = AssetDatabase.GetAssetPath(m);
        if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter mi)
        {
            if (madeReadable.Add(path)) { mi.isReadable = true; mi.SaveAndReimport(); }
            return m;
        }
        var so = new SerializedObject(m);
        var prop = so.FindProperty("m_IsReadable");
        if (prop != null && !prop.boolValue)
        {
            prop.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(m);
            madeReadable.Add(path + "#" + m.name);
        }
        return m;
    }

    static string Val(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Float: return p.floatValue.ToString("0.###");
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue ? "1" : "0";
            case SerializedPropertyType.String: return "\"" + p.stringValue + "\"";
            case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
            case SerializedPropertyType.Vector3: return V(p.vector3Value);
            case SerializedPropertyType.ObjectReference: return p.objectReferenceValue != null ? p.objectReferenceValue.name : "null";
            default: return p.isArray ? "[" + p.arraySize + "]" : "?";
        }
    }

    static string Mat(Material m)
    {
        if (m == null) return "null";
        string s = m.name + "{" + (m.shader != null ? m.shader.name : "?");
        if (m.HasProperty("_Surface")) s += " surf=" + m.GetFloat("_Surface");
        if (m.HasProperty("_Cull")) s += " cull=" + m.GetFloat("_Cull");
        s += "}";
        return s;
    }

    static string V(Vector3 v) => "(" + v.x.ToString("0.##") + "," + v.y.ToString("0.##") + "," + v.z.ToString("0.##") + ")";

    public static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
