// Code Breaker - Level 2 environment enhancement pass (doors, hallway widening, ceilings,
// lighting, props, puzzle props). Editor-only tool: run from the menu
//   Code Breaker > Level 2 > Build Environment Pass 2
// The build is idempotent: re-running deletes and rebuilds only the generated objects.
// It never edits gameplay scripts, tags, layers or the Player.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CodeBreaker.Level2Tools
{
    public static class Level2EnvironmentBuilder
    {
        const float S = 3.75f;                       // 1 m = 3.75 Unity units in this project
        const string ROOT = "Assets/CodeBreaker/Level2";
        const string SCENE = "Assets/Scenes/Level 2.unity";
        const string BACKUP = ROOT + "/Backups/Level 2 (before environment pass 2).unity";

        static StringBuilder log;
        static Dictionary<string, Material> mats;
        static Dictionary<string, GameObject> prefabs;
        static List<string> createdPrefabs;
        static int propCount, puzzleCount;
        static Transform gArch, gDoors, gFurn, gPuzzle, gDecor, gLight, gCeil, gEnv;

        // Blender metres (x, y, z-up)  ->  Unity units
        static Vector3 U(float x, float y, float z) { return new Vector3(x * S, z * S, y * S); }
        // Unity yaw so that an object's local -Z ("front") faces the given Blender plan direction
        static float Face(float dx, float dy) { return Mathf.Atan2(-dx, -dy) * Mathf.Rad2Deg; }
        static readonly float FACE_S = 0f, FACE_N = 180f, FACE_E = -90f, FACE_W = 90f;

        [MenuItem("Code Breaker/Level 2/Build Environment Pass 2")]
        public static void Build()
        {
            log = new StringBuilder();
            mats = new Dictionary<string, Material>();
            prefabs = new Dictionary<string, GameObject>();
            createdPrefabs = new List<string>();
            propCount = 0; puzzleCount = 0;

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SCENE)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            }
            log.AppendLine("Level 2 environment pass 2 - build report  (" + System.DateTime.Now + ")");
            EnsureFolder(ROOT + "/Backups"); EnsureFolder(ROOT + "/Prefabs"); EnsureFolder(ROOT + "/Prefabs/Props");
            EnsureFolder(ROOT + "/Meshes"); EnsureFolder(ROOT + "/Materials/Props"); EnsureFolder(ROOT + "/Textures/Props");
            if (!File.Exists(BACKUP))
            {
                AssetDatabase.CopyAsset(SCENE, BACKUP);
                log.AppendLine("Backup created: " + BACKUP);
            }
            else log.AppendLine("Backup already present (kept): " + BACKUP);

            ConfigureTextures();
            SetupHierarchy(scene);
            WidenHallways();
            BuildDoors();
            AdjustLighting();
            BuildCeilingFixtures();
            BuildLivingRoom();
            BuildBedroom();
            BuildKitchen();
            BuildStudy();
            BuildHallwayProps();
            Validate();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine();
            log.AppendLine("Props placed: " + propCount + "   Puzzle props: " + puzzleCount);
            log.AppendLine("Prefabs written (" + createdPrefabs.Count + "):");
            foreach (var p in createdPrefabs) log.AppendLine("  " + p);
            File.WriteAllText(ROOT + "/Level2_BuildReport.txt", log.ToString());
            AssetDatabase.Refresh();
            Debug.Log("[Level2Builder] Finished. Report: " + ROOT + "/Level2_BuildReport.txt\n" + log);
        }

        // ------------------------------------------------------------------ assets
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void ConfigureTextures()
        {
            AssetDatabase.Refresh();
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ROOT + "/Textures/Props" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                bool ch = false;
                if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; ch = true; }
                if (ti.anisoLevel < 4) { ti.anisoLevel = 4; ch = true; }
                if (ti.DoesSourceTextureHaveAlpha() && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ch = true; }
                if (ch) { ti.SaveAndReimport(); }
                n++;
            }
            log.AppendLine("Prop textures found: " + n);
        }

        static Material L2(string name)
        {
            Material m;
            if (mats.TryGetValue(name, out m)) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(ROOT + "/Materials/" + name + ".mat");
            if (m == null) { Debug.LogWarning("[Level2Builder] missing material " + name); m = PM("L2P_Fallback_" + name, new Color(0.7f, 0.7f, 0.7f)); }
            mats[name] = m;
            return m;
        }

        static Material PM(string name, Color c, float smooth = 0.3f, float metal = 0f, string tex = null,
                           bool clip = false, float emission = 0f, Color? emisColor = null, bool twoSided = false)
        {
            Material m;
            if (mats.TryGetValue(name, out m)) return m;
            string path = ROOT + "/Materials/Props/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex != null)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tex.StartsWith("Assets/") ? tex : ROOT + "/Textures/Props/L2P_T_" + tex + ".png");
                if (t == null) Debug.LogWarning("[Level2Builder] missing texture " + tex);
                m.SetTexture("_BaseMap", t);
                m.mainTexture = t;
            }
            if (clip)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (twoSided) m.SetFloat("_Cull", 0f);
            if (emission > 0f)
            {
                Color ec = (emisColor ?? Color.white) * emission;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", ec);
                if (tex != null) m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        // Textured paper / label material
        static Material TM(string tex, bool clip = false, float emission = 0f, float smooth = 0.15f)
        {
            return PM("L2P_" + tex, Color.white, smooth, 0f, tex, clip, emission, Color.white, clip);
        }

        // Box mesh in metres with metric UVs (so wall paint / wood grain is not stretched)
        static Mesh BoxMesh(Vector3 size, float texSize, bool rotUV)
        {
            string key = string.Format("L2P_Box_{0:0.###}x{1:0.###}x{2:0.###}_t{3:0.##}{4}", size.x, size.y, size.z, texSize, rotUV ? "_r" : "");
            string path = ROOT + "/Meshes/" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            Vector3 h = size * 0.5f;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nn in normals)
            {
                Vector3 ua, va;
                if (Mathf.Abs(nn.y) > 0.5f) { ua = Vector3.right; va = Vector3.forward; }
                else { ua = Vector3.Cross(Vector3.up, nn).normalized; va = Vector3.up; }
                if (rotUV) { var t = ua; ua = va; va = t; }
                float hu = Mathf.Abs(Vector3.Dot(h, ua)), hv = Mathf.Abs(Vector3.Dot(h, va)), hn = Mathf.Abs(Vector3.Dot(h, nn));
                Vector3 c = nn * hn;
                int b = v.Count;
                Vector3[] corners = { c - ua * hu - va * hv, c - ua * hu + va * hv, c + ua * hu + va * hv, c + ua * hu - va * hv };
                foreach (var p in corners)
                {
                    v.Add(p); n.Add(nn);
                    uv.Add(new Vector2(Vector3.Dot(p, ua) / texSize, Vector3.Dot(p, va) / texSize));
                }
                bool front = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), nn) > 0f;
                if (front) tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                else tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ scene helpers
        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static Transform FindInScene(string name)
        {
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                var t = FindDeep(r.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        static Transform Group(Transform parent, string name)
        {
            Transform t = parent != null ? parent.Find(name) : null;
            if (t == null)
            {
                var g = new GameObject(name);
                if (parent != null) g.transform.SetParent(parent, false);
                t = g.transform;
            }
            return t;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        static void DestroyNamed(Transform parent, string name)
        {
            var t = parent.Find(name);
            while (t != null) { Object.DestroyImmediate(t.gameObject); t = parent.Find(name); }
        }

        static void SetupHierarchy(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject root = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == "Level2") root = r;
            if (root == null) root = new GameObject("Level2");
            root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity; root.transform.localScale = Vector3.one;
            var lv = root.transform;
            gArch = Group(lv, "Architecture"); gDoors = Group(lv, "Doors"); gFurn = Group(lv, "Furniture");
            gPuzzle = Group(lv, "PuzzleProps"); gDecor = Group(lv, "DecorativeProps"); gLight = Group(lv, "Lighting");
            gCeil = Group(lv, "Ceiling"); gEnv = Group(lv, "Environment");
            MoveExisting("Level2_House_Structure", gArch);
            MoveExisting("Level2_Colliders", gArch);
            MoveExisting("Level2_House_Decor", gDecor);
            MoveExisting("Level2_Lighting", gLight);
            MoveExisting("Global Volume", gEnv);
            // generated content is rebuilt from scratch every run
            DestroyNamed(gArch, "Hallway_Widening");
            ClearChildren(gDoors); ClearChildren(gFurn); ClearChildren(gPuzzle); ClearChildren(gCeil);
            DestroyNamed(gDecor, "Added_Props");
            DestroyNamed(gLight, "Added_Lights");
            log.AppendLine("Hierarchy: Level2/{Architecture, Doors, Furniture, PuzzleProps, DecorativeProps, Lighting, Ceiling, Environment}");
        }

        static void MoveExisting(string name, Transform group)
        {
            Transform t = null;
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == name) t = r.transform;
            if (t == null) t = group.Find(name);
            if (t == null) { log.AppendLine("  WARNING: could not find " + name); return; }
            if (t.parent != group) { t.SetParent(group, true); log.AppendLine("  moved " + name + " -> " + group.name); }
        }

        // generic objects --------------------------------------------------------------
        static GameObject ArchBox(string name, Transform parent, float x0, float x1, float y0, float y1, float z0, float z1,
                                  Material m, float texSize, bool collider, bool rotUV = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = U((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            g.transform.localScale = Vector3.one * S;
            g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(new Vector3(x1 - x0, z1 - z0, y1 - y0), texSize, rotUV);
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) g.AddComponent<BoxCollider>();
            g.isStatic = true;
            return g;
        }

        // local-space (metres) box with metric UV
        static GameObject MBox(string name, Transform parent, Vector3 pos, Vector3 size, Material m, float texSize = 1f, bool rotUV = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, texSize, rotUV);
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }
        static GameObject Cube(string n, Transform p, Vector3 pos, Vector3 size, Material m) { return Prim(PrimitiveType.Cube, n, p, pos, size, m, Vector3.zero); }
        static GameObject Cube(string n, Transform p, Vector3 pos, Vector3 size, Material m, Vector3 e) { return Prim(PrimitiveType.Cube, n, p, pos, size, m, e); }
        static GameObject Cyl(string n, Transform p, Vector3 pos, float r, float h, Material m) { return Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(2 * r, h * 0.5f, 2 * r), m, Vector3.zero); }
        static GameObject Cyl(string n, Transform p, Vector3 pos, float r, float h, Material m, Vector3 e) { return Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(2 * r, h * 0.5f, 2 * r), m, e); }
        static GameObject Sph(string n, Transform p, Vector3 pos, Vector3 d, Material m) { return Prim(PrimitiveType.Sphere, n, p, pos, d, m, Vector3.zero); }
        static GameObject Quad(string n, Transform p, Vector3 pos, float w, float h, Material m, Vector3 e) { return Prim(PrimitiveType.Quad, n, p, pos, new Vector3(w, h, 1f), m, e); }
        // quad lying flat, texture top pointing away from the viewer (+Z)
        static GameObject FlatQuad(string n, Transform p, Vector3 pos, float w, float h, Material m, float yaw = 0f) { return Quad(n, p, pos, w, h, m, new Vector3(90f, yaw, 0f)); }

        // Prop root positioned in Blender metres; children are authored in local metres.
        static Transform Prop(string name, Transform parent, float x, float y, float z, float yaw)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = U(x, y, z);
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = Vector3.one * S;
            propCount++;
            return g.transform;
        }

        static Bounds RenderBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.1f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // Puzzle props: trigger collider (raycast-able for future interaction, never blocks the player)
        static Transform Puzzle(Transform t)
        {
            var b = RenderBounds(t);
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = t.InverseTransformPoint(b.center);
            Vector3 s = t.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) + Vector3.one * 0.01f;
            puzzleCount++;
            return t;
        }

        // Larger furniture: solid box collider so the player cannot walk through it
        static void Solid(Transform t)
        {
            var b = RenderBounds(t);
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = t.InverseTransformPoint(b.center);
            Vector3 s = t.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        // Reusable prop prefabs ------------------------------------------------------------
        delegate void PropBuilder(Transform root);

        static GameObject PrefabAsset(string key, PropBuilder build)
        {
            GameObject asset;
            if (prefabs.TryGetValue(key, out asset)) return asset;
            var tmp = new GameObject(key);
            tmp.transform.localScale = Vector3.one * S;
            build(tmp.transform);
            string path = ROOT + "/Prefabs/Props/" + key + ".prefab";
            asset = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            Object.DestroyImmediate(tmp);
            prefabs[key] = asset;
            createdPrefabs.Add(path);
            return asset;
        }

        static Transform Place(string key, PropBuilder build, string name, Transform parent, float x, float y, float z, float yaw)
        {
            var asset = PrefabAsset(key, build);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.position = U(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            propCount++;
            return go.transform;
        }

        // ------------------------------------------------------------------ hallways
        static readonly string[] hallFbxParts = {
            "HALL_Walls_Hallway_A", "HALL_Walls_Hallway_B", "HALL_Walls_Hallway_C",
            "HALL_Floor_Hallway_A", "HALL_Floor_Hallway_B", "HALL_Floor_Hallway_C",
            "HALL_Ceiling_Hallway_A", "HALL_Ceiling_Hallway_B", "HALL_Ceiling_Hallway_C",
            "HALL_Skirting_Hallway_A", "HALL_Skirting_Hallway_B", "HALL_Skirting_Hallway_C" };
        static readonly string[] hallOldColliders = { "Wall_03", "Wall_25", "Wall_06", "Wall_20", "Wall_09", "Wall_15" };

        static void MoveDecor(string name, float dx, float dy)
        {
            var t = FindInScene(name);
            if (t == null) { log.AppendLine("  WARNING decor not found: " + name); return; }
            PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);   // back to the imported position
            t.position += new Vector3(dx * S, 0f, dy * S);
            log.AppendLine(string.Format("  moved {0} by ({1:0.###}, {2:0.###}) m", name, dx, dy));
        }

        static void WidenHallways()
        {
            log.AppendLine();
            log.AppendLine("[Hallways] widened from 1.15 m to 1.60 m clear (0.225 m each side, centre lines and doorways unchanged)");
            foreach (var n in hallFbxParts)
            {
                var t = FindInScene(n);
                if (t != null) { t.gameObject.SetActive(false); log.AppendLine("  disabled (replaced) " + n); }
                else log.AppendLine("  WARNING missing " + n);
            }
            var cols = FindInScene("Level2_Colliders");
            foreach (var n in hallOldColliders)
            {
                var t = cols != null ? (cols.Find(n) ?? cols.Find("Collider_" + n) ?? FindDeep(cols, "Collider_" + n)) : null;
                if (t != null) { t.gameObject.SetActive(false); log.AppendLine("  disabled old collider " + n); }
                else log.AppendLine("  WARNING missing collider " + n);
            }
            const float d = 0.225f;
            MoveDecor("HALL_Photo_A", 0f, d);
            MoveDecor("HALL_Photo_B", 0f, -d);
            MoveDecor("HALL_CoatHooks", 0f, d);
            MoveDecor("HALL_Painting_Corridor", d, 0f);

            var hw = new GameObject("Hallway_Widening").transform; hw.SetParent(gArch, false);
            var ceilRoot = Group(gCeil, "Hallway_Ceilings");
            Material wall = L2("L2_Wall_CreamPaint"), floor = L2("L2_Floor_OakPlanks"), ceil = L2("L2_Ceiling_WhitePaint"), trim = L2("L2_Trim_WhitePaint");
            const float H = 2.9f, e = 0.005f;

            // Hall A  (Study <-> Kitchen), clear y 2.4489 .. 4.0495
            float ax0 = -6.091f, ax1 = -2.842f, ay0 = 2.4489f, ay1 = 4.0495f;
            BuildHall("HallA", hw, ceilRoot, wall, floor, ceil, trim, true, ax0, ax1, ay0, ay1, 2.6739f, 3.8245f, -6.331f, -2.602f);
            // Hall B  (Living <-> Bedroom), clear y -4.0423 .. -2.4501
            BuildHall("HallB", hw, ceilRoot, wall, floor, ceil, trim, true, ax0, ax1, -4.0423f, -2.4501f, -3.8173f, -2.6751f, -6.331f, -2.602f);
            // Corridor (Kitchen <-> Bedroom), clear x -0.8001 .. 0.8026
            float cx0 = -0.8001f, cx1 = 0.8026f, cy0 = -1.6226f, cy1 = 1.628f;
            ArchBox("Corridor_Wall_West", hw, cx0 - 0.24f, cx0, -1.8626f + e, 1.868f - e, 0f, H, wall, 2f, true);
            ArchBox("Corridor_Wall_East", hw, cx1, cx1 + 0.24f, -1.8626f + e, 1.868f - e, 0f, H, wall, 2f, true);
            ArchBox("Corridor_Jamb_Kitchen_W", hw, cx0, -0.5751f - e, 1.628f + e, 1.868f - e, 0f, H, wall, 2f, true);
            ArchBox("Corridor_Jamb_Kitchen_E", hw, 0.5776f + e, cx1, 1.628f + e, 1.868f - e, 0f, H, wall, 2f, true);
            ArchBox("Corridor_Jamb_Bedroom_W", hw, cx0, -0.5751f - e, -1.8626f + e, -1.6226f - e, 0f, H, wall, 2f, true);
            ArchBox("Corridor_Jamb_Bedroom_E", hw, 0.5776f + e, cx1, -1.8626f + e, -1.6226f - e, 0f, H, wall, 2f, true);
            ArchBox("Corridor_Floor", hw, cx0 - 0.24f, cx1 + 0.24f, cy0, cy1, -0.02f, 0f, floor, 2f, false);
            ArchBox("Corridor_Ceiling", ceilRoot, cx0 - 0.24f, cx1 + 0.24f, cy0, cy1, H, H + 0.02f, ceil, 2f, false);
            ArchBox("Corridor_Skirting_W", hw, cx0, cx0 + 0.015f, cy0, cy1, 0f, 0.1f, trim, 1f, false);
            ArchBox("Corridor_Skirting_E", hw, cx1 - 0.015f, cx1, cy0, cy1, 0f, 0.1f, trim, 1f, false);
            log.AppendLine("  new hallway shells: walls, door-side jambs, floors, ceilings, skirting + box colliders on walls/jambs");
        }

        static void BuildHall(string id, Transform hw, Transform ceilRoot, Material wall, Material floor, Material ceil, Material trim,
                              bool alongX, float x0, float x1, float y0, float y1, float openY0, float openY1, float bandW, float bandE)
        {
            const float H = 2.9f, e = 0.005f, T = 0.24f;
            ArchBox(id + "_Wall_South", hw, bandW + e, bandE - e, y0 - T, y0, 0f, H, wall, 2f, true);
            ArchBox(id + "_Wall_North", hw, bandW + e, bandE - e, y1, y1 + T, 0f, H, wall, 2f, true);
            // jambs that close the widened hall around the existing 1.15 m doorways
            ArchBox(id + "_Jamb_West_S", hw, bandW + e, x0 - e, y0, openY0 - e, 0f, H, wall, 2f, true);
            ArchBox(id + "_Jamb_West_N", hw, bandW + e, x0 - e, openY1 + e, y1, 0f, H, wall, 2f, true);
            ArchBox(id + "_Jamb_East_S", hw, x1 + e, bandE - e, y0, openY0 - e, 0f, H, wall, 2f, true);
            ArchBox(id + "_Jamb_East_N", hw, x1 + e, bandE - e, openY1 + e, y1, 0f, H, wall, 2f, true);
            ArchBox(id + "_Floor", hw, x0, x1, y0 - T, y1 + T, -0.02f, 0f, floor, 2f, false);
            ArchBox(id + "_Ceiling", ceilRoot, x0, x1, y0 - T, y1 + T, H, H + 0.02f, ceil, 2f, false);
            ArchBox(id + "_Skirting_S", hw, x0, x1, y0, y0 + 0.015f, 0f, 0.1f, trim, 1f, false);
            ArchBox(id + "_Skirting_N", hw, x0, x1, y1 - 0.015f, y1, 0f, 0.1f, trim, 1f, false);
        }

        // ------------------------------------------------------------------ doors
        const float DW = 1.15f, DH = 2.2f, DT = 0.24f, LT = 0.03f;

        static GameObject DoorPrefab(bool rightHinge)
        {
            string key = rightHinge ? "L2_Door_Wood_RightHinge" : "L2_Door_Wood_LeftHinge";
            GameObject asset;
            if (prefabs.TryGetValue(key, out asset)) return asset;
            Material wood = PM("L2P_Door_Oak", new Color(0.82f, 0.66f, 0.49f), 0.45f, 0f, ROOT + "/Textures/L2_T_Wood_Oak.png");
            Material frameM = L2("L2_Trim_WhitePaint");
            Material metal = L2("L2_Metal_Brass");
            Material dark = L2("L2_Plastic_Black");
            float h = rightHinge ? 1f : -1f;
            float Wl = DW - 2 * LT - 0.006f, Hl = DH - LT - 0.003f - 0.01f, lth = 0.04f;
            float zRoomFace = DT * 0.5f - 0.03f;          // room-side face of the closed leaf
            var root = new GameObject(key);
            root.transform.localScale = Vector3.one * S;
            var frame = new GameObject("DoorFrame").transform; frame.SetParent(root.transform, false);
            MBox("Lining_Left", frame, new Vector3(-DW / 2 + LT / 2, DH / 2, 0), new Vector3(LT, DH, DT - 0.004f), frameM);
            MBox("Lining_Right", frame, new Vector3(DW / 2 - LT / 2, DH / 2, 0), new Vector3(LT, DH, DT - 0.004f), frameM);
            MBox("Lining_Head", frame, new Vector3(0, DH - LT / 2, 0), new Vector3(DW - 2 * LT, LT, DT - 0.004f), frameM);
            float stopZ = zRoomFace - lth - 0.0075f;
            MBox("Stop_Left", frame, new Vector3(-DW / 2 + LT + 0.006f, (DH - LT) / 2, stopZ), new Vector3(0.012f, DH - LT, 0.015f), frameM);
            MBox("Stop_Right", frame, new Vector3(DW / 2 - LT - 0.006f, (DH - LT) / 2, stopZ), new Vector3(0.012f, DH - LT, 0.015f), frameM);
            MBox("Stop_Head", frame, new Vector3(0, DH - LT - 0.006f, stopZ), new Vector3(DW - 2 * LT, 0.012f, 0.015f), frameM);
            float hx = h * (DW / 2 - LT - 0.003f);
            foreach (float hy in new[] { 0.25f, 1.1f, 1.95f })
            {
                Cyl("Hinge_Barrel", frame, new Vector3(hx, hy, zRoomFace), 0.009f, 0.1f, metal);
                Cube("Hinge_Plate", frame, new Vector3(h * (DW / 2 - LT - 0.002f), hy, zRoomFace - 0.016f), new Vector3(0.003f, 0.09f, 0.028f), metal);
            }
            var pivot = new GameObject("DoorPivot").transform; pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(hx, 0f, zRoomFace);
            var leaf = MBox("DoorLeaf", pivot, new Vector3(-h * Wl / 2, 0.01f + Hl / 2, -lth / 2), new Vector3(Wl, Hl, lth), wood, 1f, true).transform;
            leaf.gameObject.AddComponent<BoxCollider>();
            float cy = 0.01f + Hl / 2;
            // shaker style raised panels, both faces
            foreach (float sz in new[] { 1f, -1f })
            {
                float fz = sz * (lth / 2 + 0.003f);
                foreach (float px in new[] { -0.241f, 0.241f })
                {
                    MBox("Panel_Upper", leaf, new Vector3(px, 1.568f - cy + 0.01f, fz), new Vector3(0.362f, 0.937f, 0.012f), wood, 1f, true);
                    MBox("Panel_Upper_Field", leaf, new Vector3(px, 1.568f - cy + 0.01f, fz + sz * 0.004f), new Vector3(0.30f, 0.875f, 0.012f), wood, 1f, true);
                    MBox("Panel_Lower", leaf, new Vector3(px, 0.585f - cy, fz), new Vector3(0.362f, 0.75f, 0.012f), wood, 1f, true);
                    MBox("Panel_Lower_Field", leaf, new Vector3(px, 0.585f - cy, fz + sz * 0.004f), new Vector3(0.30f, 0.69f, 0.012f), wood, 1f, true);
                }
                float lx = -h * (Wl / 2 - 0.065f), ly = 1.0f - cy;
                Cyl("Handle_Rosette", leaf, new Vector3(lx, ly, sz * (lth / 2 + 0.006f)), 0.028f, 0.012f, metal, new Vector3(90, 0, 0));
                Cyl("Handle_Neck", leaf, new Vector3(lx, ly, sz * (lth / 2 + 0.035f)), 0.008f, 0.05f, metal, new Vector3(90, 0, 0));
                Cube("Handle_Lever", leaf, new Vector3(lx + h * 0.055f, ly, sz * (lth / 2 + 0.06f)), new Vector3(0.12f, 0.018f, 0.02f), metal);
                Cube("Escutcheon", leaf, new Vector3(lx, ly - 0.12f, sz * (lth / 2 + 0.003f)), new Vector3(0.032f, 0.065f, 0.006f), metal);
                Cube("Keyhole", leaf, new Vector3(lx, ly - 0.125f, sz * (lth / 2 + 0.0065f)), new Vector3(0.008f, 0.02f, 0.002f), dark);
            }
            var zone = new GameObject("InteractionZone");
            zone.transform.SetParent(root.transform, false);
            var tz = zone.AddComponent<BoxCollider>();
            tz.isTrigger = true; tz.center = new Vector3(0, DH / 2, 0); tz.size = new Vector3(DW, DH, DT + 1.0f);
            tz.enabled = false;   // reserved for a future door/lock script; disabled so nothing reacts to it today
            string path = ROOT + "/Prefabs/" + key + ".prefab";
            asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            prefabs[key] = asset; createdPrefabs.Add(path);
            return asset;
        }

        static Transform PlaceDoor(string name, float x, float y, float yaw, bool rightHinge, float openDeg)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(DoorPrefab(rightHinge), gDoors);
            go.name = name;
            go.transform.position = U(x, y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var pivot = go.transform.Find("DoorPivot");
            pivot.localRotation = Quaternion.Euler(0f, (rightHinge ? 1f : -1f) * openDeg, 0f);
            log.AppendLine(string.Format("  {0}: {1} hinge, open {2} deg", name, rightHinge ? "right" : "left", openDeg));
            return go.transform;
        }

        static void BuildDoors()
        {
            log.AppendLine();
            log.AppendLine("[Doors] 6 wooden doors (frame lining, stops, 3 hinges, lever handles, keyholes, leaf collider) in the existing 1.15 x 2.2 m doorways");
            // yaw: local +Z points into the room the door belongs to
            PlaceDoor("Door_LivingRoom_Exit", -6.2114f, -3.2462f, -90f, false, 90f);
            PlaceDoor("Door_Bedroom_Entrance", -2.7217f, -3.2462f, 90f, false, 90f);
            PlaceDoor("Door_Bedroom_Exit", 0.00125f, -1.7426f, 180f, false, 90f);
            PlaceDoor("Door_Kitchen_Entrance", 0.00125f, 1.748f, 0f, false, 90f);
            PlaceDoor("Door_Kitchen_Exit", -2.7217f, 3.2492f, 90f, false, 90f);
            PlaceDoor("Door_Study_Entrance", -6.2114f, 3.2492f, -90f, true, 90f);
            log.AppendLine("  Doors are left open (DoorPivot rotated) so the current free-roam navigation still works;");
            log.AppendLine("  set DoorPivot Y rotation to 0 to close a door. InteractionZone trigger is present but disabled.");
        }

        // ------------------------------------------------------------------ lighting
        static void SetLight(string name, Color c, float intensity, float range)
        {
            var t = FindInScene(name);
            var l = t != null ? t.GetComponent<Light>() : null;
            if (l == null) { log.AppendLine("  WARNING light missing " + name); return; }
            l.color = c; l.intensity = intensity; l.range = range;
            log.AppendLine(string.Format("  {0}: colour ({1:0.00},{2:0.00},{3:0.00}) intensity {4} range {5}", name, c.r, c.g, c.b, intensity, range));
        }

        static void AddPointLight(string name, Transform parent, float x, float y, float z, Color c, float intensity, float range)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.position = U(x, y, z);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            log.AppendLine("  added " + name);
        }

        static void AdjustLighting()
        {
            log.AppendLine();
            log.AppendLine("[Lighting] per-room tuning (existing URP lights kept, no red/horror colours)");
            Color warm = new Color(1f, 0.83f, 0.64f), warmSoft = new Color(1f, 0.78f, 0.55f), neutral = new Color(1f, 0.97f, 0.92f), neutralCool = new Color(0.97f, 0.97f, 0.96f);
            SetLight("Light_Bedroom_Ceiling", warm, 26f, 32f);
            SetLight("Light_Bedroom_BedsideLamp_Left", warmSoft, 5f, 9f);
            SetLight("Light_Bedroom_BedsideLamp_Right", warmSoft, 5f, 9f);
            SetLight("Light_Living_Ceiling", new Color(1f, 0.87f, 0.72f), 30f, 34f);
            SetLight("Light_Living_FloorLamp", warmSoft, 6f, 10f);
            SetLight("Light_Living_TableLamp", warmSoft, 4.5f, 9f);
            SetLight("Light_Kitchen_Ceiling", neutral, 44f, 38f);
            SetLight("Light_Kitchen_Pendant", new Color(1f, 0.93f, 0.82f), 10f, 14f);
            SetLight("Light_Study_Ceiling", neutralCool, 34f, 34f);
            SetLight("Light_Study_DeskLamp", neutral, 6f, 8f);
            SetLight("Light_Study_FloorLamp", new Color(1f, 0.9f, 0.78f), 4f, 9f);
            Color hall = new Color(1f, 0.9f, 0.78f);
            SetLight("Light_Hallway_A", hall, 13f, 18f);
            SetLight("Light_Hallway_B", hall, 13f, 18f);
            SetLight("Light_Hallway_Corridor", hall, 13f, 18f);
            // fewer shadow-casting point lights (each costs 6 shadow maps): keep shadows on the 4 room ceiling lights only
            foreach (var n in new[] { "Light_Hallway_A", "Light_Hallway_B", "Light_Hallway_Corridor", "Light_Kitchen_Pendant" })
            {
                var lt = FindInScene(n);
                if (lt != null && lt.GetComponent<Light>() != null) lt.GetComponent<Light>().shadows = LightShadows.None;
            }
            log.AppendLine("  shadows disabled on hallway + pendant lights (shadow atlas was being down-scaled for 42 shadow maps)");
            var added = new GameObject("Added_Lights").transform; added.SetParent(gLight, false);
            AddPointLight("Light_Kitchen_UnderCabinet", added, 3.0f, 7.75f, 1.38f, neutral, 4f, 9f);
            AddPointLight("Light_Hallway_A_DoorFill", added, -5.6f, 3.25f, 2.6f, hall, 4f, 9f);
            AddPointLight("Light_Hallway_B_DoorFill", added, -3.3f, -3.25f, 2.6f, hall, 4f, 9f);
        }

        // ------------------------------------------------------------------ ceilings
        static void SmokeDetectorBuild(Transform r)
        {
            Cyl("Body", r, new Vector3(0, -0.015f, 0), 0.06f, 0.03f, L2("L2_Plastic_White"));
            Cyl("Vent", r, new Vector3(0, -0.032f, 0), 0.035f, 0.006f, PM("L2P_Plastic_Grey", new Color(0.75f, 0.75f, 0.74f)));
            Sph("LED", r, new Vector3(0.042f, -0.031f, 0), Vector3.one * 0.008f, PM("L2P_LED_Red", new Color(0.8f, 0.1f, 0.1f), 0.5f, 0f, null, false, 2f, new Color(1f, 0.1f, 0.05f)));
        }

        static void BuildCeilingFixtures()
        {
            var fx = Group(gCeil, "Ceiling_Fixtures");
            float[,] pts = { { 2.6f, -3.0f }, { -12.2f, -1.2f }, { -1.6f, 5.6f }, { -11.8f, 8.0f }, { -4.47f, 3.25f + 0.55f }, { -4.47f, -3.25f - 0.55f }, { 0f, 1.0f } };
            string[] names = { "SmokeDetector_Bedroom", "SmokeDetector_LivingRoom", "SmokeDetector_Kitchen", "SmokeDetector_Study", "SmokeDetector_HallA", "SmokeDetector_HallB", "SmokeDetector_Corridor" };
            for (int i = 0; i < names.Length; i++) Place("L2P_SmokeDetector", SmokeDetectorBuild, names[i], fx, pts[i, 0], pts[i, 1], 2.9f, 0f);
            log.AppendLine();
            log.AppendLine("[Ceiling] all rooms already had ceilings + light fixtures; hallway ceilings rebuilt for the wider halls; 7 smoke detectors added");
        }

        // ------------------------------------------------------------------ prop builders
        static void BookBuild(Transform r, Material cover, float w, float h, float d)
        {
            Cube("Cover", r, new Vector3(0, h / 2, 0), new Vector3(w, h, d), cover);
            Cube("Pages", r, new Vector3(0, h / 2, -0.003f), new Vector3(w - 0.006f, h - 0.008f, d - 0.002f), PM("L2P_Paper_Pages", new Color(0.95f, 0.93f, 0.86f), 0.1f));
        }
        static void MugBuild(Transform r, Material m)
        {
            Cyl("Body", r, new Vector3(0, 0.0475f, 0), 0.04f, 0.095f, m);
            Cyl("Coffee", r, new Vector3(0, 0.088f, 0), 0.036f, 0.004f, PM("L2P_Coffee", new Color(0.18f, 0.1f, 0.05f), 0.8f));
            Cube("Handle", r, new Vector3(0.048f, 0.05f, 0), new Vector3(0.018f, 0.055f, 0.012f), m);
        }
        static void CoasterBuild(Transform r) { Cyl("Coaster", r, new Vector3(0, 0.003f, 0), 0.045f, 0.006f, PM("L2P_Cork", new Color(0.70f, 0.55f, 0.38f), 0.1f)); }
        static void CandleBuild(Transform r, float h)
        {
            Cyl("Wax", r, new Vector3(0, h / 2, 0), 0.035f, h, PM("L2P_Candle_Wax", new Color(0.96f, 0.93f, 0.86f), 0.35f));
            Cyl("Wick", r, new Vector3(0, h + 0.006f, 0), 0.002f, 0.012f, L2("L2_Plastic_Black"));
        }
        static void FrameBuild(Transform r, Material photo, float w, float h)
        {
            Cube("Frame", r, new Vector3(0, h / 2, 0), new Vector3(w, h, 0.018f), L2("L2_Wood_Walnut"));
            Quad("Photo", r, new Vector3(0, h / 2, -0.0095f), w - 0.03f, h - 0.03f, photo, Vector3.zero);
            Cube("Stand", r, new Vector3(0, h * 0.35f, 0.04f), new Vector3(0.03f, h * 0.7f, 0.008f), L2("L2_Wood_Walnut"), new Vector3(-20f, 0, 0));
        }
        static void PillowBuild(Transform r, Material m, float w)
        {
            Sph("Pillow", r, new Vector3(0, w * 0.45f, 0), new Vector3(w, w * 0.9f, 0.16f), m);
        }
        static void GlowStarBuild(Transform r)
        {
            Quad("Star", r, new Vector3(0, -0.002f, 0), 0.11f, 0.11f, PM("L2P_GlowStar", new Color(0.85f, 1f, 0.7f), 0.2f, 0f, "GlowStar", true, 0.9f, new Color(0.7f, 1f, 0.55f), true), new Vector3(-90f, 0, 0));
        }
        static void MagnetBuild(Transform r, Material m) { Cyl("Magnet", r, new Vector3(0, 0, -0.005f), 0.018f, 0.01f, m, new Vector3(90, 0, 0)); }
        static void NoteQuad(Transform r, Material m, float w, float h) { Quad("Paper", r, new Vector3(0, 0, 0), w, h, m, Vector3.zero); }

        static Material Fabric(string n, Color c) { return PM("L2P_Fabric_" + n, c, 0.05f); }
        static Material Plastic(string n, Color c) { return PM("L2P_Plastic_" + n, c, 0.55f); }

        // Flat paper note lying on a surface (Blender coords, z = surface height)
        static Transform PaperOnSurface(string name, Transform parent, float x, float y, float z, float yaw, Material m, float w, float h)
        {
            var t = Prop(name, parent, x, y, z, yaw);
            FlatQuad("Paper", t, new Vector3(0, 0.0015f, 0), w, h, m);
            return t;
        }
        // Paper on a wall/vertical surface; yaw = facing direction
        static Transform PaperOnWall(string name, Transform parent, float x, float y, float z, float yaw, Material m, float w, float h)
        {
            var t = Prop(name, parent, x, y, z, yaw);
            Quad("Paper", t, Vector3.zero, w, h, m, Vector3.zero);
            return t;
        }

        // ------------------------------------------------------------------ LIVING ROOM
        static void BuildLivingRoom()
        {
            var pz = Group(gPuzzle, "LivingRoom_TheFamilyEvening");
            var dc = Group(Group(gDecor, "Added_Props"), "LivingRoom");
            Material black = L2("L2_Plastic_Black"), blue = L2("L2_Book_Blue"), brass = L2("L2_Metal_Brass"), walnut = L2("L2_Wood_Walnut");

            // --- puzzle: four torn photo pieces (show the wall clock at 8:00 and channel 3 on a blue TV)
            var p1 = Prop("Puzzle_Living_PhotoPiece_1_TVCabinet", pz, -13.86f, -4.06f, 0.52f, FACE_E + 15f);
            FlatQuad("Piece", p1, new Vector3(0, 0.0015f, 0), 0.10f, 0.075f, TM("PhotoPiece_1", true)); Puzzle(p1);
            var p2 = Prop("Puzzle_Living_PhotoPiece_2_UnderCushion", pz, -10.36f, -2.62f, 0.445f, FACE_W - 20f);
            FlatQuad("Piece", p2, new Vector3(0, 0.0015f, 0), 0.10f, 0.075f, TM("PhotoPiece_2", true)); Puzzle(p2);
            var p3 = Prop("Puzzle_Living_PhotoPiece_3_InVase", pz, -11.6f, -7.773f, 1.08f, FACE_N);
            Cyl("RolledPiece", p3, new Vector3(0, 0.05f, 0), 0.009f, 0.10f, TM("PhotoPiece_3", true, 0f, 0.1f), new Vector3(0, 0, 12f)); Puzzle(p3);
            var p4 = Prop("Puzzle_Living_PhotoPiece_4_BehindFrame", pz, -12.47f, -7.935f, 0.8f, FACE_N + 8f);
            Quad("Piece", p4, new Vector3(0.03f, 0.2f, 0), 0.10f, 0.075f, TM("PhotoPiece_4", true), new Vector3(0, 0, 10f)); Puzzle(p4);

            // --- puzzle: TV remote (no batteries) + batteries hidden on the bookshelf
            var rem = Prop("Puzzle_Living_TVRemote_NoBatteries", pz, -11.84f, -2.95f, 0.42f, FACE_W + 25f);
            Cube("Body", rem, new Vector3(0, 0.01f, 0), new Vector3(0.05f, 0.02f, 0.18f), black);
            FlatQuad("Buttons", rem, new Vector3(0, 0.0205f, 0), 0.046f, 0.176f, TM("Remote", false, 0f, 0.4f), 180f);
            Puzzle(rem);
            var bat = Prop("Puzzle_Living_Batteries_AA", pz, -7.96f, 0.30f, 0.098f, FACE_S + 10f);
            for (int i = 0; i < 2; i++)
            {
                Cyl("Battery", bat, new Vector3(-0.009f + i * 0.018f, 0.0085f, 0), 0.0085f, 0.05f, PM("L2P_Battery", new Color(0.15f, 0.35f, 0.75f), 0.6f, 0.2f), new Vector3(90, 0, 0));
                Cyl("Tip", bat, new Vector3(-0.009f + i * 0.018f, 0.0085f, 0.026f), 0.003f, 0.004f, PM("L2P_Metal_Silver", new Color(0.8f, 0.8f, 0.8f), 0.8f, 1f), new Vector3(90, 0, 0));
            }
            Puzzle(bat);

            // --- puzzle: shadow lamp with star cut-outs + star figurine on the console table
            var lamp = Prop("Puzzle_Living_StarShadowLamp", pz, -6.51f, -1.13f, 0.8f, FACE_W);
            Cyl("Base", lamp, new Vector3(0, 0.012f, 0), 0.06f, 0.024f, walnut);
            var shade = PM("L2P_StarShade", new Color(0.22f, 0.25f, 0.42f), 0.3f);
            Cyl("Shade", lamp, new Vector3(0, 0.12f, 0), 0.055f, 0.19f, shade);
            Sph("Bulb", lamp, new Vector3(0, 0.23f, 0), Vector3.one * 0.05f, PM("L2P_WarmGlow", new Color(1f, 0.85f, 0.6f), 0.3f, 0f, null, false, 1.5f, new Color(1f, 0.8f, 0.5f)));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Sph("StarHole", lamp, new Vector3(Mathf.Sin(a) * 0.056f, 0.13f, Mathf.Cos(a) * 0.056f), Vector3.one * 0.018f, PM("L2P_WarmGlow", Color.white));
            }
            Puzzle(lamp);
            var fig = Prop("Puzzle_Living_StarFigurine", pz, -6.51f, -1.72f, 0.8f, FACE_W);
            Cyl("Plinth", fig, new Vector3(0, 0.01f, 0), 0.03f, 0.02f, walnut);
            for (int i = 0; i < 5; i++)
                Cube("StarArm", fig, new Vector3(0, 0.07f, 0), new Vector3(0.016f, 0.09f, 0.012f), brass, new Vector3(0, 0, i * 72f));
            Puzzle(fig);

            // --- puzzle: star-shaped slot box on the bookshelf, blue book holding the door key
            var slot = Prop("Puzzle_Living_StarSlotBox", pz, -8.18f, 0.33f, 1.238f, FACE_S);
            Cube("Box", slot, new Vector3(0, 0.05f, 0), new Vector3(0.18f, 0.10f, 0.14f), walnut);
            FlatQuad("StarSlot", slot, new Vector3(0, 0.1005f, 0), 0.17f, 0.13f, TM("StarSlot", false, 0f, 0.3f));
            Puzzle(slot);
            var bb = Prop("Puzzle_Living_BlueBook", pz, -8.17f, 0.34f, 0.478f, FACE_S);
            BookBuild(bb, blue, 0.045f, 0.27f, 0.19f);
            Cube("GoldTitle", bb, new Vector3(0, 0.2f, -0.0955f), new Vector3(0.03f, 0.05f, 0.002f), brass);
            var key = new GameObject("Puzzle_Living_DoorKey (hidden inside book)").transform;
            key.SetParent(bb, false); key.localPosition = new Vector3(0, 0.12f, 0);
            Cyl("Bow", key, new Vector3(0, 0.05f, 0), 0.012f, 0.004f, brass, new Vector3(0, 0, 90));
            Cube("Shaft", key, new Vector3(0, 0, 0), new Vector3(0.004f, 0.08f, 0.004f), brass);
            key.gameObject.SetActive(false);
            Puzzle(bb);

            // --- false leads
            var fr = Prop("FalseLead_Living_RedBook", dc, -7.93f, 0.34f, 1.238f, FACE_S);
            BookBuild(fr, L2("L2_Book_Red"), 0.05f, 0.26f, 0.18f);
            PaperOnSurface("FalseLead_Living_TVGuide_Ch7", dc, -12.12f, -3.06f, 0.42f, FACE_W + 10f, TM("TVGuide"), 0.15f, 0.2f);

            // --- door note pinned on the wall right next to the living-room exit door (room side, always readable)
            var dn = PaperOnWall("Puzzle_Living_DoorNote", pz, -6.334f, -2.32f, 1.45f, FACE_W, TM("Note_LivingDoor"), 0.2f, 0.15f);
            Cyl("Pin", dn, new Vector3(0, 0.065f, -0.003f), 0.005f, 0.006f, PM("L2P_Plastic_Red", new Color(0.8f, 0.1f, 0.1f), 0.6f), new Vector3(90, 0, 0));
            Puzzle(dn);

            // --- decorative props
            Material terracotta = Fabric("Terracotta", new Color(0.72f, 0.38f, 0.25f)), cream = Fabric("Cream", new Color(0.9f, 0.86f, 0.76f)), sage = Fabric("Sage", new Color(0.55f, 0.62f, 0.5f));
            Place("L2P_Pillow_Terracotta", r => PillowBuild(r, terracotta, 0.42f), "Cushion_Sofa_North", dc, -10.27f, -2.52f, 0.44f, FACE_W + 70f);
            Place("L2P_Pillow_Cream", r => PillowBuild(r, cream, 0.42f), "Cushion_Sofa_South", dc, -10.27f, -3.98f, 0.44f, FACE_W - 75f);
            Place("L2P_Pillow_Sage", r => PillowBuild(r, sage, 0.38f), "Cushion_Armchair", dc, -12.3f, 0.2f, 0.44f, FACE_S);
            var throwB = Prop("ThrowBlanket_Armchair", dc, -11.95f, -0.02f, 0.44f, FACE_S);
            Cube("Drape", throwB, new Vector3(0, 0.08f, 0), new Vector3(0.05f, 0.34f, 0.62f), Fabric("Knit", new Color(0.82f, 0.74f, 0.6f)), new Vector3(0, 0, -12f));
            Place("L2P_Coaster", CoasterBuild, "Coaster_1", dc, -11.9f, -2.8f, 0.42f, 0f);
            Place("L2P_Coaster", CoasterBuild, "Coaster_2", dc, -12.15f, -2.82f, 0.42f, 0f);
            Material mugWhite = L2("L2_Ceramic_White"), mugBlue = L2("L2_Ceramic_Blue");
            Place("L2P_Mug_White", r => MugBuild(r, mugWhite), "Mug_CoffeeTable_1", dc, -11.9f, -2.8f, 0.426f, 30f);
            Place("L2P_Mug_Blue", r => MugBuild(r, mugBlue), "Mug_CoffeeTable_2", dc, -12.15f, -2.82f, 0.426f, -60f);
            var games = Prop("BoardGames_Stack", dc, -13.95f, -4.55f, 0f, FACE_E);
            string[] gt = { "Game_CastleQuest", "Game_IslandTraders", "Game_WordTiles" };
            for (int i = 0; i < 3; i++)
            {
                var gbox = new GameObject("BoardGame_" + gt[i]).transform; gbox.SetParent(games, false);
                gbox.localPosition = new Vector3(0, i * 0.075f, 0); gbox.localEulerAngles = new Vector3(0, i * 9f - 6f, 0);
                Cube("Box", gbox, new Vector3(0, 0.0365f, 0), new Vector3(0.30f, 0.073f, 0.30f), PM("L2P_GameBox_" + i, new Color(0.25f + 0.2f * i, 0.3f, 0.35f), 0.4f));
                FlatQuad("Lid", gbox, new Vector3(0, 0.0735f, 0), 0.3f, 0.3f, TM(gt[i], false, 0f, 0.4f));
            }
            PaperOnSurface("FalseLead_Living_MovieNightNote", dc, -13.95f, -4.55f, 0.225f, FACE_E - 15f, TM("Note_MovieNight"), 0.14f, 0.105f);
            var ctrl = Prop("GameController", dc, -13.95f, -2.44f, 0.52f, FACE_E + 15f);
            Cube("Body", ctrl, new Vector3(0, 0.015f, 0), new Vector3(0.15f, 0.03f, 0.065f), black);
            Sph("GripL", ctrl, new Vector3(-0.055f, 0.012f, 0.025f), new Vector3(0.05f, 0.03f, 0.06f), black);
            Sph("GripR", ctrl, new Vector3(0.055f, 0.012f, 0.025f), new Vector3(0.05f, 0.03f, 0.06f), black);
            Material wax = PM("L2P_Candle_Wax", new Color(0.96f, 0.93f, 0.86f));
            Place("L2P_Candle_Tall", r => CandleBuild(r, 0.16f), "Candle_Sideboard_1", dc, -12.0f, -7.76f, 0.8f, 0f);
            Place("L2P_Candle_Short", r => CandleBuild(r, 0.09f), "Candle_Sideboard_2", dc, -12.1f, -7.66f, 0.8f, 0f);
            Material photo = L2("L2_Photo_Print");
            Place("L2P_PhotoFrame_Small", r => FrameBuild(r, photo, 0.14f, 0.18f), "FamilyPhoto_Cabinet_1", dc, -6.55f, -6.85f, 1.85f, FACE_W - 10f);
            Place("L2P_PhotoFrame_Wide", r => FrameBuild(r, photo, 0.2f, 0.15f), "FamilyPhoto_Cabinet_2", dc, -6.55f, -6.35f, 1.85f, FACE_W + 12f);
            var basket = Prop("BlanketBasket", dc, -13.82f, -1.05f, 0f, 0f);
            Cyl("Basket", basket, new Vector3(0, 0.17f, 0), 0.2f, 0.34f, PM("L2P_Wicker", new Color(0.62f, 0.47f, 0.3f), 0.15f));
            Sph("Blanket", basket, new Vector3(0, 0.34f, 0), new Vector3(0.38f, 0.14f, 0.38f), Fabric("Knit", Color.white));
            Sph("BlanketFold", basket, new Vector3(0.08f, 0.38f, 0.05f), new Vector3(0.24f, 0.1f, 0.2f), terracotta);
            Solid(basket);
            var books = Prop("CoffeeTable_Books", dc, -12.0f, -3.55f, 0.44f, FACE_W + 5f);
            Cube("Book1", books, new Vector3(0, 0.015f, 0), new Vector3(0.26f, 0.03f, 0.2f), L2("L2_Book_Green"));
            Cube("Book2", books, new Vector3(0.01f, 0.042f, 0), new Vector3(0.22f, 0.024f, 0.17f), L2("L2_Book_Cream"), new Vector3(0, 8, 0));
            log.AppendLine();
            log.AppendLine("[Living Room - The Family Evening] 4 torn photo pieces (TV cabinet, under sofa cushion, rolled in vase, behind sideboard frame),");
            log.AppendLine("  remote without batteries, AA batteries on bookshelf, star shadow lamp + star figurine (console table), star-slot box,");
            log.AppendLine("  blue book with hidden key (inactive child), door note; false leads: red book, TV guide (Ch 7 circled), movie-night note");
        }

        // ------------------------------------------------------------------ BEDROOM
        static void BuildBedroom()
        {
            var pz = Group(gPuzzle, "Bedroom_LightsOut");
            var dc = Group(Group(gDecor, "Added_Props"), "Bedroom");
            Material black = L2("L2_Plastic_Black"), brass = L2("L2_Metal_Brass"), walnut = L2("L2_Wood_Walnut"), white = L2("L2_Plastic_White");

            var uv = Prop("Puzzle_Bedroom_UVFlashlight", pz, 1.24f, -7.57f, 0.55f, FACE_N + 70f);
            Cyl("Body", uv, new Vector3(0, 0.014f, 0), 0.013f, 0.13f, PM("L2P_Plastic_Purple", new Color(0.25f, 0.12f, 0.35f), 0.5f), new Vector3(90, 0, 0));
            Cyl("Head", uv, new Vector3(0, 0.016f, -0.075f), 0.017f, 0.03f, black, new Vector3(90, 0, 0));
            Cyl("Lens", uv, new Vector3(0, 0.016f, -0.091f), 0.014f, 0.003f, PM("L2P_UV_Lens", new Color(0.5f, 0.2f, 1f), 0.9f, 0f, null, false, 1.2f, new Color(0.55f, 0.2f, 1f)), new Vector3(90, 0, 0));
            Puzzle(uv);

            var moon = Prop("Puzzle_Bedroom_Mirror_MOON_UVText", pz, -2.572f, -5.05f, 1.45f, FACE_E);
            Quad("UVText", moon, Vector3.zero, 0.44f, 0.22f, PM("L2P_Mirror_MOON_Faint", new Color(0.34f, 0.36f, 0.43f), 0.8f, 0f, "Mirror_MOON", true, 0f, null, true), Vector3.zero);
            Puzzle(moon);

            var mb = Prop("Puzzle_Bedroom_MusicBox_Ballerina", pz, -2.42f, -6.95f, 0.95f, FACE_E);
            Cube("Box", mb, new Vector3(0, 0.035f, 0), new Vector3(0.18f, 0.07f, 0.12f), PM("L2P_Lacquer_Pink", new Color(0.86f, 0.62f, 0.68f), 0.7f));
            Cube("Lid", mb, new Vector3(0, 0.11f, 0.06f), new Vector3(0.18f, 0.008f, 0.12f), PM("L2P_Lacquer_Pink", Color.white), new Vector3(-75f, 0, 0));
            Quad("Lid_Mirror", mb, new Vector3(0, 0.11f, 0.052f), 0.15f, 0.1f, L2("L2_Mirror"), new Vector3(-75f + 180f, 0, 0));
            Cyl("Platform", mb, new Vector3(0, 0.075f, -0.01f), 0.025f, 0.006f, brass);
            Cyl("Ballerina_Leg", mb, new Vector3(0, 0.1f, -0.01f), 0.003f, 0.045f, PM("L2P_Porcelain", new Color(0.97f, 0.94f, 0.92f), 0.7f));
            Sph("Ballerina_Tutu", mb, new Vector3(0, 0.122f, -0.01f), new Vector3(0.045f, 0.012f, 0.045f), PM("L2P_Tutu", new Color(1f, 0.82f, 0.88f), 0.2f));
            Cyl("Ballerina_Body", mb, new Vector3(0, 0.14f, -0.01f), 0.006f, 0.03f, PM("L2P_Porcelain", Color.white));
            Sph("Ballerina_Head", mb, new Vector3(0, 0.162f, -0.01f), Vector3.one * 0.012f, PM("L2P_Porcelain", Color.white));
            Cube("Ballerina_Arms", mb, new Vector3(0, 0.15f, -0.01f), new Vector3(0.04f, 0.003f, 0.003f), PM("L2P_Porcelain", Color.white), new Vector3(0, 0, 20f));
            FlatQuad("DanceCard", mb, new Vector3(0.0f, 0.071f, -0.04f), 0.09f, 0.06f, TM("DanceCard"));
            Puzzle(mb);

            var stars = Prop("Puzzle_Bedroom_GlowStars_Number5", pz, 2.2f, -6.6f, 2.9f, 0f);
            int[,] cells = { { 0, 0 }, { 1, 0 }, { 2, 0 }, { 0, 1 }, { 0, 2 }, { 1, 2 }, { 2, 2 }, { 2, 3 }, { 0, 4 }, { 1, 4 }, { 2, 4 } };
            var starAsset = PrefabAsset("L2P_GlowStar", GlowStarBuild);
            for (int i = 0; i < cells.GetLength(0); i++)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(starAsset, stars);
                s.name = "GlowStar_" + i;
                s.transform.localPosition = new Vector3((cells[i, 0] - 1) * 0.26f, 0f, (cells[i, 1] - 2) * 0.26f);
                s.transform.localRotation = Quaternion.Euler(0, i * 23f, 0);
                s.transform.localScale = Vector3.one;
            }
            Puzzle(stars);

            var diary = Prop("Puzzle_Bedroom_Diary", pz, 2.55f, -6.35f, 0.66f, FACE_N + 160f);
            Cube("Cover", diary, new Vector3(0, 0.015f, 0), new Vector3(0.16f, 0.03f, 0.22f), PM("L2P_Leather_Plum", new Color(0.42f, 0.18f, 0.3f), 0.35f));
            Cube("Strap", diary, new Vector3(0.07f, 0.016f, 0), new Vector3(0.025f, 0.032f, 0.05f), brass);
            FlatQuad("DiaryNote", diary, new Vector3(-0.01f, 0.0315f, 0.01f), 0.14f, 0.105f, TM("Note_Diary"), 6f);
            Puzzle(diary);

            var box = Prop("Puzzle_Bedroom_MoonBox_DirectionLock", pz, 2.98f, -6.75f, 0f, FACE_E);
            Cube("Box", box, new Vector3(0, 0.06f, 0), new Vector3(0.36f, 0.12f, 0.26f), PM("L2P_Navy_Lacquer", new Color(0.12f, 0.15f, 0.32f), 0.5f));
            FlatQuad("MoonLid", box, new Vector3(0, 0.1205f, 0), 0.34f, 0.24f, TM("MoonBox", false, 0f, 0.4f));
            Quad("DirectionLock", box, new Vector3(0, 0.06f, -0.132f), 0.12f, 0.06f, TM("DirectionLock", false, 0f, 0.6f), Vector3.zero);
            Puzzle(box);

            var jb = Prop("Puzzle_Bedroom_JewelleryBox_5Drawers", pz, 4.69f, -4.1f, 0.9f, FACE_W);
            Cube("Body", jb, new Vector3(0, 0.1f, 0), new Vector3(0.24f, 0.2f, 0.15f), walnut);
            for (int i = 0; i < 5; i++)
            {
                Cube("Drawer_" + (i + 1), jb, new Vector3(0, 0.022f + i * 0.038f, -0.0765f), new Vector3(0.22f, 0.032f, 0.004f), PM("L2P_Wood_Light", new Color(0.78f, 0.62f, 0.45f), 0.4f));
                Sph("Knob_" + (i + 1), jb, new Vector3(0, 0.022f + i * 0.038f, -0.081f), Vector3.one * 0.01f, brass);
            }
            Puzzle(jb);

            // false leads
            PaperOnWall("FalseLead_Bedroom_StarChartPoster", dc, 1.75f, -1.866f, 1.6f, FACE_S, TM("StarChart", false, 0f, 0.3f), 0.6f, 0.8f);
            PaperOnSurface("FalseLead_Bedroom_DentistNote", dc, -2.32f, -5.0f, 0.76f, FACE_E + 10f, TM("Note_Dentist"), 0.076f, 0.076f);

            // decorative
            var clock = Prop("AlarmClock", dc, 3.505f, -7.6f, 0.55f, FACE_N + 20f);
            Cyl("Body", clock, new Vector3(0, 0.055f, 0), 0.045f, 0.04f, PM("L2P_Metal_Mint", new Color(0.55f, 0.75f, 0.7f), 0.6f, 0.3f), new Vector3(90, 0, 0));
            Quad("Face", clock, new Vector3(0, 0.055f, -0.0205f), 0.075f, 0.075f, TM("AlarmClock", false, 0f, 0.5f), Vector3.zero);
            Sph("BellL", clock, new Vector3(-0.03f, 0.1f, 0), Vector3.one * 0.03f, brass);
            Sph("BellR", clock, new Vector3(0.03f, 0.1f, 0), Vector3.one * 0.03f, brass);
            Cyl("LegL", clock, new Vector3(-0.025f, 0.012f, 0), 0.004f, 0.025f, brass); Cyl("LegR", clock, new Vector3(0.025f, 0.012f, 0), 0.004f, 0.025f, brass);
            var phone = Prop("Phone", dc, 0.93f, -7.6f, 0.55f, FACE_N - 10f);
            Cube("Body", phone, new Vector3(0, 0.004f, 0), new Vector3(0.072f, 0.008f, 0.15f), black);
            FlatQuad("Screen", phone, new Vector3(0, 0.0085f, 0), 0.066f, 0.14f, TM("PhoneScreen", false, 0.6f, 0.8f), 180f);
            var glasses = Prop("ReadingGlasses", dc, 1.05f, -7.86f, 0.55f, FACE_N + 30f);
            Cyl("LensL", glasses, new Vector3(-0.032f, 0.02f, 0), 0.022f, 0.003f, L2("L2_Glass_Dark"), new Vector3(90, 0, 0));
            Cyl("LensR", glasses, new Vector3(0.032f, 0.02f, 0), 0.022f, 0.003f, L2("L2_Glass_Dark"), new Vector3(90, 0, 0));
            Cube("Bridge", glasses, new Vector3(0, 0.03f, 0), new Vector3(0.02f, 0.003f, 0.003f), black);
            Cube("ArmL", glasses, new Vector3(-0.055f, 0.022f, 0.055f), new Vector3(0.003f, 0.003f, 0.11f), black);
            Cube("ArmR", glasses, new Vector3(0.055f, 0.022f, 0.055f), new Vector3(0.003f, 0.003f, 0.11f), black);
            var water = Prop("WaterGlass", dc, 3.14f, -7.58f, 0.55f, 0f);
            Cyl("Glass", water, new Vector3(0, 0.05f, 0), 0.03f, 0.1f, L2("L2_Glass_Jar"));
            Material rose = PM("L2P_Glass_Rose", new Color(0.95f, 0.7f, 0.75f), 0.9f), amber = PM("L2P_Glass_Amber", new Color(0.85f, 0.6f, 0.25f), 0.9f);
            var perf1 = Prop("Perfume_1", dc, 4.74f, -4.79f, 0.9f, 15f);
            Cube("Bottle", perf1, new Vector3(0, 0.04f, 0), new Vector3(0.05f, 0.08f, 0.03f), rose); Cyl("Cap", perf1, new Vector3(0, 0.09f, 0), 0.012f, 0.02f, brass);
            var perf2 = Prop("Perfume_2", dc, 4.62f, -4.8f, 0.9f, -10f);
            Cyl("Bottle", perf2, new Vector3(0, 0.035f, 0), 0.022f, 0.07f, amber); Sph("Cap", perf2, new Vector3(0, 0.08f, 0), Vector3.one * 0.025f, black);
            var hb = Prop("Hairbrush", dc, -2.36f, -5.38f, 0.76f, FACE_E + 40f);
            Cube("Handle", hb, new Vector3(0, 0.012f, 0.06f), new Vector3(0.025f, 0.015f, 0.12f), walnut);
            Cube("Head", hb, new Vector3(0, 0.015f, -0.04f), new Vector3(0.06f, 0.02f, 0.09f), walnut);
            var mk = Prop("MakeupTray", dc, -2.32f, -4.72f, 0.76f, FACE_E);
            Cube("Tray", mk, new Vector3(0, 0.006f, 0), new Vector3(0.2f, 0.012f, 0.12f), L2("L2_Ceramic_White"));
            Cyl("Lipstick", mk, new Vector3(-0.06f, 0.035f, 0), 0.008f, 0.05f, PM("L2P_Lipstick", new Color(0.6f, 0.1f, 0.15f), 0.7f));
            Cyl("Compact", mk, new Vector3(0.02f, 0.017f, 0.01f), 0.03f, 0.01f, brass);
            Cyl("Bottle", mk, new Vector3(0.07f, 0.04f, -0.02f), 0.015f, 0.06f, PM("L2P_Glass_Rose", Color.white));
            var lb = Prop("LaundryBasket", dc, 4.5f, -3.2f, 0f, 0f);
            Cyl("Basket", lb, new Vector3(0, 0.24f, 0), 0.22f, 0.48f, PM("L2P_Plastic_White_Matte", new Color(0.92f, 0.92f, 0.9f), 0.2f));
            Sph("Clothes_1", lb, new Vector3(-0.05f, 0.48f, 0), new Vector3(0.28f, 0.12f, 0.25f), Fabric("Denim", new Color(0.25f, 0.33f, 0.5f)));
            Sph("Clothes_2", lb, new Vector3(0.07f, 0.5f, 0.05f), new Vector3(0.2f, 0.1f, 0.18f), Fabric("Cream", Color.white));
            Solid(lb);
            var sl = Prop("Slippers", dc, 1.12f, -5.5f, 0.012f, FACE_W + 20f);
            Material slip = Fabric("Slipper", new Color(0.75f, 0.68f, 0.78f));
            Sph("Left", sl, new Vector3(-0.055f, 0.025f, 0), new Vector3(0.09f, 0.05f, 0.26f), slip);
            Sph("Right", sl, new Vector3(0.055f, 0.025f, 0.03f), new Vector3(0.09f, 0.05f, 0.26f), slip);
            var cl = Prop("FoldedClothes_BedEnd", dc, 1.75f, -6.05f, 0.66f, FACE_N);
            Cube("Shirt", cl, new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.24f), Fabric("Sage", Color.white));
            Cube("Sweater", cl, new Vector3(0.01f, 0.06f, 0), new Vector3(0.28f, 0.04f, 0.22f), Fabric("Terracotta", Color.white));
            var sc = Prop("Suitcase_OnWardrobe", dc, 3.7f, -2.16f, 2.2f, FACE_S);
            Cube("Case", sc, new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.2f, 0.4f), PM("L2P_Suitcase", new Color(0.35f, 0.25f, 0.18f), 0.4f));
            Cube("Handle", sc, new Vector3(0, 0.1f, -0.205f), new Vector3(0.12f, 0.025f, 0.02f), black);
            Place("L2P_Pillow_Sage", r => PillowBuild(r, Fabric("Sage", Color.white), 0.38f), "Cushion_ReadingChair", dc, 4.12f, -5.55f, 0.44f, FACE_W);
            log.AppendLine();
            log.AppendLine("[Bedroom - Lights Out] UV flashlight (bedside), MOON UV text on vanity mirror, music box + ballerina dance card (up, right, right, down),");
            log.AppendLine("  11 glow-in-the-dark ceiling stars forming a 5 above the bed, diary note, moon box with direction lock under the bed, 5-drawer jewellery box;");
            log.AppendLine("  false leads: constellation poster, dentist note");
        }

        // ------------------------------------------------------------------ KITCHEN
        static void JarBuild(Transform r, Material label, Material number, float h, Material lid)
        {
            Cyl("Glass", r, new Vector3(0, h / 2, 0), 0.055f, h, L2("L2_Glass_Jar"));
            Cyl("Contents", r, new Vector3(0, h * 0.4f, 0), 0.05f, h * 0.78f, PM("L2P_Powder_White", new Color(0.95f, 0.94f, 0.9f), 0.05f));
            Cyl("Lid", r, new Vector3(0, h + 0.01f, 0), 0.058f, 0.02f, lid);
            Quad("Label", r, new Vector3(0, h * 0.5f, -0.0565f), 0.09f, 0.045f, label, Vector3.zero);
            Quad("BottomNumber", r, new Vector3(0, -0.0005f, 0), 0.07f, 0.07f, number, new Vector3(-90f, 0, 0));
        }

        static void BuildKitchen()
        {
            var pz = Group(gPuzzle, "Kitchen_GrandmasRecipe");
            var dc = Group(Group(gDecor, "Added_Props"), "Kitchen");
            Material steel = L2("L2_Metal_StainlessSteel"), black = L2("L2_Plastic_Black"), white = L2("L2_Plastic_White"), walnut = L2("L2_Wood_Walnut");
            Material wood = PM("L2P_Lid_Wood", new Color(0.7f, 0.55f, 0.38f), 0.3f, 0f, "Assets/CodeBreaker/Level2/Textures/L2_T_Wood_Oak.png");

            var rb = Prop("Puzzle_Kitchen_RecipeBook_WithCard", pz, 1.68f, 7.72f, 0.9f, FACE_S);
            Cube("PageL", rb, new Vector3(-0.08f, 0.012f, 0), new Vector3(0.16f, 0.02f, 0.23f), PM("L2P_Paper_Pages", Color.white), new Vector3(0, 0, 6f));
            Cube("PageR", rb, new Vector3(0.08f, 0.012f, 0), new Vector3(0.16f, 0.02f, 0.23f), PM("L2P_Paper_Pages", Color.white), new Vector3(0, 0, -6f));
            Cube("Cover", rb, new Vector3(0, 0.002f, 0), new Vector3(0.34f, 0.004f, 0.24f), L2("L2_Book_Red"));
            FlatQuad("RecipeCard", rb, new Vector3(0.08f, 0.031f, 0.01f), 0.14f, 0.096f, TM("RecipeCard"), -4f);
            Puzzle(rb);

            Material n4 = TM("Num_4", true), n1 = TM("Num_1", true), n6 = TM("Num_6", true), n2 = TM("Num_2", true);
            var flour = Prop("Puzzle_Kitchen_Jar_Flour_Bottom4", pz, 4.8f, 7.78f, 0.9f, FACE_S);
            JarBuild(flour, TM("Label_Flour"), n4, 0.2f, wood); Puzzle(flour);
            var sugar = Prop("Puzzle_Kitchen_Jar_Sugar_Bottom1", pz, 5.0f, 7.78f, 0.9f, FACE_S - 8f);
            JarBuild(sugar, TM("Label_Sugar"), n1, 0.17f, wood); Puzzle(sugar);
            var eggs = Prop("Puzzle_Kitchen_EggCarton_Bottom6", pz, 5.27f, 7.62f, 0.9f, FACE_S + 90f);
            Cube("Carton", eggs, new Vector3(0, 0.035f, 0), new Vector3(0.3f, 0.07f, 0.11f), PM("L2P_Cardboard_Egg", new Color(0.8f, 0.72f, 0.58f), 0.05f));
            FlatQuad("Label", eggs, new Vector3(0, 0.0705f, 0), 0.26f, 0.09f, TM("Label_Eggs"));
            Quad("BottomNumber", eggs, new Vector3(0, -0.0005f, 0), 0.07f, 0.07f, n6, new Vector3(-90f, 0, 0));
            Puzzle(eggs);
            var milk = Prop("Puzzle_Kitchen_MilkBottle_Bottom2", pz, 5.47f, 7.86f, 0.9f, FACE_S + 30f);
            Cyl("Bottle", milk, new Vector3(0, 0.1f, 0), 0.04f, 0.2f, PM("L2P_Milk", new Color(0.97f, 0.97f, 0.95f), 0.6f));
            Cyl("Neck", milk, new Vector3(0, 0.215f, 0), 0.022f, 0.03f, PM("L2P_Milk", Color.white));
            Cyl("Cap", milk, new Vector3(0, 0.235f, 0), 0.024f, 0.012f, PM("L2P_Plastic_Blue", new Color(0.15f, 0.35f, 0.75f), 0.6f));
            Quad("Label", milk, new Vector3(0, 0.1f, -0.0415f), 0.07f, 0.035f, TM("Label_Milk"), Vector3.zero);
            Quad("BottomNumber", milk, new Vector3(0, -0.0005f, 0), 0.06f, 0.06f, n2, new Vector3(-90f, 0, 0));
            Puzzle(milk);

            var scale = Prop("Puzzle_Kitchen_Scale_400g", pz, 4.2f, 7.62f, 0.9f, FACE_S);
            Cube("Base", scale, new Vector3(0, 0.015f, 0), new Vector3(0.2f, 0.03f, 0.22f), white);
            Cyl("Platform", scale, new Vector3(0, 0.036f, 0.01f), 0.085f, 0.012f, steel);
            Quad("Display", scale, new Vector3(0, 0.016f, -0.1105f), 0.08f, 0.022f, TM("ScaleDisplay", false, 0.35f, 0.6f), Vector3.zero);
            Puzzle(scale);

            var fn = PaperOnWall("Puzzle_Kitchen_FridgeNote", pz, 0.37f, 7.259f, 1.62f, FACE_S, TM("Note_Fridge"), 0.18f, 0.135f);
            Cyl("Magnet", fn, new Vector3(0, 0.06f, -0.005f), 0.015f, 0.01f, PM("L2P_Plastic_Yellow", new Color(0.95f, 0.8f, 0.15f), 0.6f), new Vector3(90, 0, 0));
            Puzzle(fn);

            var ot = Prop("Puzzle_Kitchen_OvenTimer_Display", pz, 3.75f, 7.384f, 0.86f, FACE_S);
            Cube("Bezel", ot, new Vector3(0, 0, 0.004f), new Vector3(0.15f, 0.06f, 0.008f), black);
            Quad("Display", ot, new Vector3(0, 0, -0.0005f), 0.12f, 0.045f, TM("OvenDisplay", false, 1.2f, 0.8f), Vector3.zero);
            for (int i = 0; i < 2; i++) Cyl("Knob", ot, new Vector3(-0.12f + i * 0.24f, 0, -0.008f), 0.018f, 0.018f, steel, new Vector3(90, 0, 0));
            Puzzle(ot);
            var okey = Prop("Puzzle_Kitchen_DoorKey_InsideOven (hidden)", pz, 3.75f, 7.72f, 0.25f, FACE_S);
            Cube("Shaft", okey, new Vector3(0, 0.002f, 0), new Vector3(0.08f, 0.004f, 0.006f), L2("L2_Metal_Brass"));
            Cyl("Bow", okey, new Vector3(-0.05f, 0.002f, 0), 0.014f, 0.004f, L2("L2_Metal_Brass"));
            Puzzle(okey);
            okey.gameObject.SetActive(false);

            // false leads
            PaperOnWall("FalseLead_Kitchen_ShoppingList", dc, 0.36f, 7.259f, 0.82f, FACE_S + 4f, TM("Note_Shopping"), 0.09f, 0.09f);
            PaperOnWall("FalseLead_Kitchen_Oven350Note", dc, 4.45f, 7.622f, 1.78f, FACE_S - 5f, TM("Note_Oven350"), 0.08f, 0.08f);
            PaperOnSurface("FalseLead_Kitchen_PizzaMenu", dc, 5.32f, 5.3f, 0.9f, FACE_W + 12f, TM("PizzaMenu"), 0.15f, 0.2f);

            // decorative
            Material magnetR = PM("L2P_Plastic_Red", Color.white), magnetG = PM("L2P_Plastic_Green", new Color(0.2f, 0.6f, 0.3f), 0.6f), magnetB = PM("L2P_Plastic_Blue", Color.white);
            Place("L2P_Magnet_Red", r => MagnetBuild(r, magnetR), "FridgeMagnet_1", dc, 0.15f, 7.262f, 1.75f, FACE_S);
            Place("L2P_Magnet_Green", r => MagnetBuild(r, magnetG), "FridgeMagnet_2", dc, 0.62f, 7.262f, 1.4f, FACE_S);
            Place("L2P_Magnet_Blue", r => MagnetBuild(r, magnetB), "FridgeMagnet_3", dc, 0.6f, 7.262f, 0.95f, FACE_S);
            Place("L2P_Magnet_Red", r => MagnetBuild(r, magnetR), "FridgeMagnet_4", dc, 0.36f, 7.262f, 0.87f, FACE_S);
            var pan = Prop("FryingPan", dc, 3.93f, 7.84f, 0.93f, FACE_S + 120f);
            Cyl("Pan", pan, new Vector3(0, 0.02f, 0), 0.11f, 0.04f, PM("L2P_Pan_Black", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.6f));
            Cube("Handle", pan, new Vector3(0, 0.03f, -0.19f), new Vector3(0.025f, 0.015f, 0.17f), black);
            var toaster = Prop("Toaster", dc, 3.3f, 7.8f, 0.9f, FACE_S);
            Cube("Body", toaster, new Vector3(0, 0.09f, 0), new Vector3(0.22f, 0.18f, 0.14f), steel);
            Cube("Slot1", toaster, new Vector3(0, 0.18f, -0.025f), new Vector3(0.16f, 0.003f, 0.02f), black);
            Cube("Slot2", toaster, new Vector3(0, 0.18f, 0.025f), new Vector3(0.16f, 0.003f, 0.02f), black);
            Cube("Lever", toaster, new Vector3(0.115f, 0.12f, 0), new Vector3(0.012f, 0.02f, 0.03f), black);
            var kettle = Prop("Kettle", dc, 4.5f, 7.86f, 0.9f, FACE_S + 30f);
            Cyl("Base", kettle, new Vector3(0, 0.01f, 0), 0.08f, 0.02f, black);
            Cyl("Body", kettle, new Vector3(0, 0.11f, 0), 0.07f, 0.18f, white);
            Cube("Handle", kettle, new Vector3(0, 0.13f, 0.085f), new Vector3(0.03f, 0.14f, 0.025f), black);
            Cube("Spout", kettle, new Vector3(0, 0.16f, -0.08f), new Vector3(0.03f, 0.03f, 0.05f), white, new Vector3(-30, 0, 0));
            var bread = Prop("BreadLoaf", dc, 1.2f, 7.7f, 0.92f, FACE_S + 10f);
            Sph("Loaf", bread, new Vector3(0, 0.05f, 0), new Vector3(0.24f, 0.11f, 0.12f), PM("L2P_Bread", new Color(0.78f, 0.52f, 0.25f), 0.15f));
            var towel = Prop("DishTowel_OvenHandle", dc, 3.75f, 7.33f, 0.62f, FACE_S);
            Cube("Towel", towel, Vector3.zero, new Vector3(0.24f, 0.36f, 0.008f), Fabric("Gingham", new Color(0.7f, 0.25f, 0.25f)));
            Cube("Fold", towel, new Vector3(0, 0.17f, 0.012f), new Vector3(0.24f, 0.05f, 0.01f), Fabric("Gingham", Color.white));
            var cereal = Prop("CerealBox_FridgeTop", dc, 0.22f, 7.75f, 1.85f, FACE_S - 5f);
            Cube("Box", cereal, new Vector3(0, 0.14f, 0), new Vector3(0.2f, 0.28f, 0.07f), PM("L2P_Cardboard_Orange", new Color(0.9f, 0.62f, 0.2f), 0.2f));
            Quad("Front", cereal, new Vector3(0, 0.17f, -0.0355f), 0.19f, 0.095f, TM("Label_Cereal"), Vector3.zero);
            var pasta = Prop("PastaBox_FridgeTop", dc, 0.52f, 7.75f, 1.85f, FACE_S + 6f);
            Cube("Box", pasta, new Vector3(0, 0.12f, 0), new Vector3(0.12f, 0.24f, 0.06f), PM("L2P_Cardboard_Blue", new Color(0.15f, 0.3f, 0.6f), 0.2f));
            Quad("Front", pasta, new Vector3(0, 0.15f, -0.0305f), 0.115f, 0.0575f, TM("Label_Pasta"), Vector3.zero);
            var rice = Prop("RiceBag", dc, 5.3f, 5.62f, 0.9f, FACE_W);
            Cube("Bag", rice, new Vector3(0, 0.11f, 0), new Vector3(0.16f, 0.22f, 0.09f), PM("L2P_Bag_Paper", new Color(0.92f, 0.9f, 0.84f), 0.1f));
            Quad("Front", rice, new Vector3(0, 0.12f, -0.0455f), 0.15f, 0.075f, TM("Label_Rice"), Vector3.zero);
            Material mugWhite = L2("L2_Ceramic_White"), mugBlue = L2("L2_Ceramic_Blue");
            Place("L2P_Mug_White", r => MugBuild(r, mugWhite), "Mug_Counter_1", dc, 5.3f, 6.3f, 0.9f, 60f);
            Place("L2P_Mug_Blue", r => MugBuild(r, mugBlue), "Mug_Counter_2", dc, 5.38f, 6.42f, 0.9f, -20f);
            var mitt = Prop("OvenMitt", dc, 5.32f, 7.2f, 0.9f, FACE_W + 30f);
            Sph("Mitt", mitt, new Vector3(0, 0.015f, 0), new Vector3(0.13f, 0.03f, 0.24f), Fabric("Gingham", Color.white));
            log.AppendLine();
            log.AppendLine("[Kitchen - Grandma's Recipe] recipe book + card (Flour, Sugar, Eggs, Milk), flour/sugar jars, egg carton, milk bottle with");
            log.AppendLine("  numbers on their bases (4, 1, 6, 2), scale reading 400 g, fridge note, oven timer display, key inside oven (inactive);");
            log.AppendLine("  false leads: shopping list, 'Oven 350' sticky note, pizza menu");
        }

        // ------------------------------------------------------------------ STUDY
        static void BuildStudy()
        {
            var pz = Group(gPuzzle, "Study_TheHackersDesk");
            var dc = Group(Group(gDecor, "Added_Props"), "Study");
            Material black = L2("L2_Plastic_Black"), steel = L2("L2_Metal_StainlessSteel"), brass = L2("L2_Metal_Brass"), walnut = L2("L2_Wood_Walnut");

            var login = Prop("Puzzle_Study_Monitor_LoginScreen", pz, -9.9f, 9.3085f, 1.055f, FACE_S);
            Quad("Screen", login, Vector3.zero, 0.5f, 0.25f, TM("LoginScreen", false, 0.9f, 0.85f), Vector3.zero);
            Puzzle(login);
            var pw = PaperOnWall("Puzzle_Study_StickyNote_Password", pz, -10.23f, 9.307f, 1.04f, FACE_S - 4f, TM("Note_MonitorPassword"), 0.076f, 0.076f);
            Puzzle(pw);
            var dog = Prop("Puzzle_Study_DogPhoto", pz, -9.48f, 9.5f, 0.75f, FACE_S + 15f);
            FrameBuild(dog, TM("DogPhoto", false, 0f, 0.3f), 0.12f, 0.16f); Puzzle(dog);
            var cw = Prop("Puzzle_Study_CipherWheel", pz, -9.25f, 4.86f, 0.75f, FACE_S);
            Cyl("OuterDisc", cw, new Vector3(0, 0.002f, 0), 0.075f, 0.004f, PM("L2P_Card", new Color(0.93f, 0.89f, 0.78f), 0.1f));
            FlatQuad("OuterFace", cw, new Vector3(0, 0.0045f, 0), 0.15f, 0.15f, TM("CipherOuter", true));
            Cyl("InnerDisc", cw, new Vector3(0, 0.006f, 0), 0.055f, 0.003f, PM("L2P_Card_Blue", new Color(0.77f, 0.82f, 0.9f), 0.1f));
            FlatQuad("InnerFace", cw, new Vector3(0, 0.0081f, 0), 0.11f, 0.11f, TM("CipherInner", true), 20f);
            Cyl("Rivet", cw, new Vector3(0, 0.009f, 0), 0.005f, 0.004f, brass);
            Puzzle(cw);
            var cn = PaperOnSurface("Puzzle_Study_CipherNote_SHWQDPH", pz, -8.8f, 4.82f, 0.75f, FACE_S - 8f, TM("Note_Cipher"), 0.14f, 0.105f);
            Puzzle(cn);
            var col = Prop("Puzzle_Study_DogCollar_TagBYTE", pz, -12.95f, 3.55f, 0.5f, FACE_E + 20f);
            Material red = PM("L2P_Collar_Red", new Color(0.7f, 0.12f, 0.12f), 0.4f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                Cube("CollarSeg_" + i, col, new Vector3(Mathf.Sin(a) * 0.065f, 0.006f, Mathf.Cos(a) * 0.065f), new Vector3(0.036f, 0.012f, 0.008f), red, new Vector3(0, i * 30f + 90f, 0));
            }
            Cube("Buckle", col, new Vector3(0, 0.008f, 0.066f), new Vector3(0.03f, 0.016f, 0.01f), steel);
            Cyl("Tag", col, new Vector3(0, 0.003f, -0.1f), 0.022f, 0.004f, brass);
            FlatQuad("TagText", col, new Vector3(0, 0.0055f, -0.1f), 0.042f, 0.042f, TM("CollarTag", true, 0f, 0.6f), 180f);
            Puzzle(col);
            var cert = Prop("Puzzle_Study_Certificate_Award2019", pz, -12.3f, 9.575f, 1.6f, FACE_S);
            Cube("Frame", cert, new Vector3(0, 0, 0.004f), new Vector3(0.44f, 0.34f, 0.02f), PM("L2P_Frame_Gold", new Color(0.75f, 0.6f, 0.3f), 0.6f, 0.6f));
            Quad("Certificate", cert, new Vector3(0, 0, -0.0065f), 0.4f, 0.3f, TM("Certificate", false, 0f, 0.4f), Vector3.zero);
            Puzzle(cert);

            // false leads
            PaperOnWall("FalseLead_Study_WiFiNote", dc, -7.5f, 9.552f, 1.3f, FACE_S + 5f, TM("Note_WiFi"), 0.076f, 0.076f);
            PaperOnSurface("FalseLead_Study_OldPasswordNote", dc, -9.9f, 8.88f, 0.75f, FACE_S - 12f, TM("Note_OldPassword"), 0.07f, 0.07f);

            // decorative
            var stap = Prop("Stapler", dc, -11.1f, 2.12f, 0.8f, FACE_N + 25f);
            Cube("Base", stap, new Vector3(0, 0.01f, 0), new Vector3(0.04f, 0.02f, 0.16f), black);
            Cube("Arm", stap, new Vector3(0, 0.03f, 0.005f), new Vector3(0.038f, 0.022f, 0.15f), PM("L2P_Plastic_Red", Color.white));
            var calc = Prop("Calculator", dc, -8.78f, 5.38f, 0.75f, FACE_S + 5f);
            Cube("Body", calc, new Vector3(0, 0.007f, 0), new Vector3(0.08f, 0.014f, 0.13f), PM("L2P_Plastic_Grey", Color.white));
            FlatQuad("Keys", calc, new Vector3(0, 0.0145f, 0), 0.076f, 0.124f, TM("Calculator", false, 0f, 0.4f));
            var hp = Prop("Headphones", dc, -11.38f, 2.12f, 0.8f, FACE_N);
            Cyl("CupL", hp, new Vector3(-0.08f, 0.04f, 0), 0.045f, 0.03f, black, new Vector3(0, 0, 90));
            Cyl("CupR", hp, new Vector3(0.08f, 0.04f, 0), 0.045f, 0.03f, black, new Vector3(0, 0, 90));
            for (int i = 0; i < 5; i++)
            {
                float a = (-60f + i * 30f) * Mathf.Deg2Rad;
                Cube("Band_" + i, hp, new Vector3(Mathf.Sin(a) * 0.09f, 0.04f + Mathf.Cos(a) * 0.09f, 0), new Vector3(0.05f, 0.012f, 0.03f), black, new Vector3(0, 0, -(-60f + i * 30f)));
            }
            var usb = Prop("USBDrives", dc, -9.52f, 9.28f, 0.75f, FACE_S + 30f);
            Cube("USB1", usb, new Vector3(-0.02f, 0.004f, 0), new Vector3(0.018f, 0.008f, 0.055f), PM("L2P_Plastic_Blue", Color.white));
            Cube("USB2", usb, new Vector3(0.02f, 0.004f, 0.01f), new Vector3(0.018f, 0.008f, 0.05f), PM("L2P_Plastic_Green", Color.white), new Vector3(0, 25f, 0));
            var org = Prop("DeskOrganizer_Pens", dc, -11.45f, 9.5f, 1.32f, 0f);
            Cyl("Cup", org, new Vector3(0, 0.05f, 0), 0.04f, 0.1f, walnut);
            for (int i = 0; i < 4; i++)
                Cyl("Pen_" + i, org, new Vector3(-0.015f + i * 0.01f, 0.1f, (i % 2) * 0.01f), 0.004f, 0.14f, i % 2 == 0 ? black : PM("L2P_Plastic_Blue", Color.white), new Vector3(i * 4f - 6f, 0, 6f));
            Material mugWhite = L2("L2_Ceramic_White");
            Place("L2P_Mug_White", r => MugBuild(r, mugWhite), "Mug_WorkTable", dc, -9.25f, 5.3f, 0.75f, 140f);
            var wb = Prop("Wastebasket", dc, -8.88f, 9.3f, 0f, 0f);
            Cyl("Bin", wb, new Vector3(0, 0.15f, 0), 0.13f, 0.3f, PM("L2P_Metal_Mesh", new Color(0.2f, 0.2f, 0.22f), 0.4f, 0.6f));
            Sph("Crumple_1", wb, new Vector3(0.02f, 0.3f, 0), Vector3.one * 0.07f, L2("L2_Paper_White"));
            Sph("Crumple_2", wb, new Vector3(-0.04f, 0.29f, 0.03f), Vector3.one * 0.06f, L2("L2_Paper_White"));
            var dbed = Prop("DogBed", dc, -7.0f, 4.4f, 0f, FACE_W);
            Cyl("Base", dbed, new Vector3(0, 0.05f, 0), 0.38f, 0.1f, Fabric("Charcoal", new Color(0.3f, 0.3f, 0.33f)));
            Cyl("Cushion", dbed, new Vector3(0, 0.09f, 0), 0.3f, 0.04f, Fabric("Cream", Color.white));
            Sph("Toy_Ball", dbed, new Vector3(0.15f, 0.13f, -0.1f), Vector3.one * 0.06f, PM("L2P_Plastic_Yellow", Color.white));
            var bowl = Prop("DogBowl", dc, -6.75f, 5.25f, 0f, 0f);
            Cyl("Bowl", bowl, new Vector3(0, 0.03f, 0), 0.09f, 0.06f, steel);
            Cyl("Water", bowl, new Vector3(0, 0.05f, 0), 0.075f, 0.004f, PM("L2P_Water", new Color(0.55f, 0.7f, 0.8f), 0.95f));
            log.AppendLine();
            log.AppendLine("[Study - The Hacker's Desk] login screen on monitor, sticky note (who I love most + proudest year), dog photo 'Byte',");
            log.AppendLine("  Caesar cipher wheel + note SHW QDPH (-> PET NAME), dog collar with tag BYTE, framed 'Programming Award 2019' certificate;");
            log.AppendLine("  false leads: WiFi note, crossed-out old password note.  Solution prepared: BYTE2019");
        }

        // ------------------------------------------------------------------ HALLWAYS
        static void BuildHallwayProps()
        {
            var dc = Group(Group(gDecor, "Added_Props"), "Hallways");
            Material walnut = L2("L2_Wood_Walnut"), black = L2("L2_Plastic_Black"), oak = L2("L2_Wood_Oak");
            var rack = Prop("ShoeRack_HallB", gFurn, -4.47f, -2.6f, 0f, FACE_S);
            Cube("Top", rack, new Vector3(0, 0.44f, 0), new Vector3(0.8f, 0.025f, 0.28f), oak);
            Cube("Shelf", rack, new Vector3(0, 0.2f, 0), new Vector3(0.78f, 0.02f, 0.26f), oak);
            Cube("SideL", rack, new Vector3(-0.39f, 0.22f, 0), new Vector3(0.02f, 0.44f, 0.28f), oak);
            Cube("SideR", rack, new Vector3(0.39f, 0.22f, 0), new Vector3(0.02f, 0.44f, 0.28f), oak);
            Material shoe = PM("L2P_Leather_Brown", new Color(0.35f, 0.22f, 0.14f), 0.45f), sneaker = PM("L2P_Sneaker", new Color(0.9f, 0.9f, 0.88f), 0.3f);
            for (int i = 0; i < 3; i++)
            {
                Sph("Shoe_L_" + i, rack, new Vector3(-0.27f + i * 0.25f, 0.245f, 0), new Vector3(0.09f, 0.07f, 0.25f), i == 1 ? sneaker : shoe);
                Sph("Shoe_R_" + i, rack, new Vector3(-0.17f + i * 0.25f, 0.245f, 0), new Vector3(0.09f, 0.07f, 0.25f), i == 1 ? sneaker : shoe);
            }
            Sph("Boot_L", rack, new Vector3(-0.2f, 0.06f, 0), new Vector3(0.1f, 0.1f, 0.26f), shoe);
            Sph("Boot_R", rack, new Vector3(-0.08f, 0.06f, 0), new Vector3(0.1f, 0.1f, 0.26f), shoe);
            Solid(rack);
            var us = Prop("UmbrellaStand_HallB", gFurn, -5.75f, -3.88f, 0f, 0f);
            Cyl("Stand", us, new Vector3(0, 0.25f, 0), 0.1f, 0.5f, PM("L2P_Ceramic_Grey", new Color(0.5f, 0.55f, 0.58f), 0.6f));
            Cyl("Umbrella", us, new Vector3(0.02f, 0.45f, 0), 0.03f, 0.75f, PM("L2P_Umbrella_Navy", new Color(0.12f, 0.15f, 0.3f), 0.3f), new Vector3(0, 0, -6f));
            Cube("UmbrellaHandle", us, new Vector3(0.06f, 0.84f, 0), new Vector3(0.06f, 0.02f, 0.02f), walnut);
            Solid(us);
            var con = Prop("ConsoleTable_HallA", gFurn, -4.47f, 3.88f, 0f, FACE_S);
            Cube("Top", con, new Vector3(0, 0.78f, 0), new Vector3(0.9f, 0.03f, 0.28f), walnut);
            foreach (float lx in new[] { -0.42f, 0.42f })
                foreach (float lz in new[] { -0.11f, 0.11f })
                    Cube("Leg", con, new Vector3(lx, 0.385f, lz), new Vector3(0.03f, 0.77f, 0.03f), walnut);
            Cube("Shelf", con, new Vector3(0, 0.2f, 0), new Vector3(0.84f, 0.02f, 0.24f), walnut);
            Solid(con);
            var bowl = Prop("KeyBowl", dc, -4.25f, 3.88f, 0.795f, 0f);
            Sph("Bowl", bowl, new Vector3(0, 0.02f, 0), new Vector3(0.18f, 0.06f, 0.18f), L2("L2_Ceramic_Terracotta"));
            var mail = Prop("Mail_Envelopes", dc, -4.68f, 3.86f, 0.795f, FACE_S + 8f);
            Cube("Env1", mail, new Vector3(0, 0.002f, 0), new Vector3(0.22f, 0.003f, 0.11f), L2("L2_Paper_White"));
            Cube("Env2", mail, new Vector3(0.02f, 0.005f, 0.01f), new Vector3(0.22f, 0.003f, 0.11f), PM("L2P_Paper_Kraft", new Color(0.78f, 0.65f, 0.45f), 0.1f), new Vector3(0, 12, 0));
            var vase = Prop("BudVase_HallA", dc, -4.88f, 3.9f, 0.795f, 0f);
            Cyl("Vase", vase, new Vector3(0, 0.08f, 0), 0.035f, 0.16f, L2("L2_Ceramic_Blue"));
            Cyl("Stem", vase, new Vector3(0, 0.2f, 0), 0.003f, 0.12f, L2("L2_Plant_Stem"));
            Sph("Flower", vase, new Vector3(0, 0.27f, 0), Vector3.one * 0.05f, PM("L2P_Flower_Yellow", new Color(0.95f, 0.8f, 0.25f), 0.3f));
            log.AppendLine();
            log.AppendLine("[Hallways] shoe rack + umbrella stand (Hall B), console table + key bowl, mail, bud vase (Hall A)");
        }

        // ------------------------------------------------------------------ validation
        static void Validate()
        {
            Physics.SyncTransforms();
            log.AppendLine();
            log.AppendLine("[Validation] player-sized capsule (r = 0.6 u) swept along the room-to-room route, triggers ignored:");
            float[,] route = {
                { -8.3f, -5.6f }, { -7.5f, -3.25f }, { -6.2f, -3.25f }, { -4.47f, -3.25f }, { -2.72f, -3.25f }, { -1.5f, -3.2f },
                { 0f, -2.6f }, { 0f, -1.74f }, { 0f, 0f }, { 0f, 1.75f }, { 0f, 3.2f }, { -1.5f, 3.25f }, { -2.72f, 3.25f },
                { -4.47f, 3.25f }, { -6.2f, 3.25f }, { -7.5f, 3.4f }, { -7.5f, 6.5f }, { -9.5f, 7.2f } };
            int blocked = 0, samples = 0;
            var hits = new HashSet<string>();
            for (int i = 0; i < route.GetLength(0) - 1; i++)
            {
                Vector3 a = U(route[i, 0], route[i, 1], 0), b = U(route[i + 1, 0], route[i + 1, 1], 0);
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.4f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                    samples++;
                    var cs = Physics.OverlapCapsule(p + Vector3.up * 0.8f, p + Vector3.up * 5.5f, 0.6f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var c in cs)
                    {
                        if (c.GetComponent<CharacterController>() != null) continue;
                        blocked++;
                        string nm = c.name;
                        if (c.transform.parent != null && c.transform.parent.parent != null) nm = c.transform.parent.parent.name + "/" + nm;
                        hits.Add(string.Format("{0} at ({1:0.00},{2:0.00})", nm, p.x / S, p.z / S));
                    }
                }
            }
            log.AppendLine("  samples " + samples + ", blocked " + blocked + (hits.Count > 0 ? "  by: " + string.Join(", ", hits) : "  -> route clear"));
            // door leaves vs furniture
            foreach (Transform d in gDoors)
            {
                var leaf = d.Find("DoorPivot/DoorLeaf");
                if (leaf == null) continue;
                var bc = leaf.GetComponent<BoxCollider>();
                var hitsD = Physics.OverlapBox(bc.bounds.center, bc.bounds.extents * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                var names = new List<string>();
                foreach (var h in hitsD) if (h != bc && !h.name.Contains("Wall_") && !h.name.Contains("Floor")) names.Add(h.name);
                log.AppendLine("  " + d.name + " open leaf overlaps: " + (names.Count == 0 ? "none" : string.Join(", ", names)));
            }
        }
    }
}
#endif
