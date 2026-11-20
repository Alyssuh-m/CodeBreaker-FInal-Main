// Code Breaker: read-only inspection helper for Level 1 (exports renderer bounds + captures views).
// Menu: Code Breaker > Level 1 > Inspect (export + captures). Changes nothing in the scene.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeBreaker.Tools
{
    public static class Level1Inspect
    {
        const string Scene1 = "Assets/Scenes/Level 1.unity";

        static string Path(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        static string V(Vector3 v) { return string.Format("[{0:0.###},{1:0.###},{2:0.###}]", v.x, v.y, v.z); }

        [MenuItem("Code Breaker/Level 1/Inspect (export + captures)")]
        public static void Inspect()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != Scene1)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(Scene1, OpenSceneMode.Single);
            }
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Level1_Inspect"));
            Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.Append("[\n");
            bool first = true;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r is ParticleSystemRenderer) continue;
                var mf = r.GetComponent<MeshFilter>();
                var mats = new List<string>();
                foreach (var m in r.sharedMaterials) mats.Add(m != null ? m.name : "null");
                var t = r.transform;
                if (!first) sb.Append(",\n"); first = false;
                sb.AppendFormat("{{\"path\":\"{0}\",\"active\":{1},\"mesh\":\"{2}\",\"mats\":\"{3}\",\"min\":{4},\"max\":{5},\"pos\":{6},\"rot\":{7},\"scale\":{8},\"lscale\":{9},\"col\":{10}}}",
                    Path(t).Replace("\"", "'"), r.gameObject.activeInHierarchy ? "true" : "false",
                    mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "",
                    string.Join("|", mats), V(r.bounds.min), V(r.bounds.max), V(t.position), V(t.eulerAngles), V(t.lossyScale), V(t.localScale),
                    r.GetComponent<Collider>() != null ? "true" : "false");
            }
            sb.Append("\n]\n");
            File.WriteAllText(System.IO.Path.Combine(dir, "renderers.json"), sb.ToString());

            // ---- captures
            var camGo = new GameObject("__InspectCam"); camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f; cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.15f, 0.0f, 0.15f);

            Bounds all = new Bounds();
            bool init = false;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (!init) { all = r.bounds; init = true; } else all.Encapsulate(r.bounds);
            }
            File.AppendAllText(System.IO.Path.Combine(dir, "renderers.json"), "");
            File.WriteAllText(System.IO.Path.Combine(dir, "bounds.txt"), "center " + V(all.center) + " size " + V(all.size));

            // hide roof / ceilings for the top view
            var hidden = new List<GameObject>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                string n = r.name.ToLowerInvariant();
                bool roof = n.Contains("roof") || n.Contains("ceiling") || n.Contains("top");
                foreach (var m in r.sharedMaterials) if (m != null && (m.name.Contains("Ceiling") || m.name.Contains("Top_"))) roof = true;
                if (roof && r.gameObject.activeSelf) { r.gameObject.SetActive(false); hidden.Add(r.gameObject); }
            }
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(all.size.z, all.size.x * 9f / 16f) * 0.55f;
            camGo.transform.position = all.center + Vector3.up * (all.extents.y + 50f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Shoot(cam, System.IO.Path.Combine(dir, "top.png"), 2560, 1440);
            foreach (var g in hidden) g.SetActive(true);
            cam.orthographic = false;

            // eye-level views from every checkpoint, 8 directions
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                if (!t.name.EndsWith("Checkpoint")) continue;
                for (int i = 0; i < 8; i++)
                {
                    camGo.transform.position = t.position + Vector3.up * 1.0f;
                    camGo.transform.rotation = Quaternion.Euler(5f, i * 45f, 0f);
                    Shoot(cam, System.IO.Path.Combine(dir, t.name.Replace(" ", "_") + "_" + (i * 45) + ".png"), 960, 540);
                }
                File.AppendAllText(System.IO.Path.Combine(dir, "bounds.txt"), "\n" + t.name + " " + V(t.position));
            }
            Object.DestroyImmediate(camGo);
            Debug.Log("[Level1Inspect] wrote " + dir);
        }

        [MenuItem("Code Breaker/Level 1/Capture Corners")]
        public static void Corners()
        {
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Level1_Inspect/corners"));
            Directory.CreateDirectory(dir);
            var camGo = new GameObject("__InspectCam"); camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f; cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(1f, 0f, 1f);
            var lit = new GameObject("__InspectLight"); lit.hideFlags = HideFlags.HideAndDontSave;
            var l = lit.AddComponent<Light>(); l.type = LightType.Point; l.range = 60f; l.intensity = 6f;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    Vector3 c = new Vector3(215f + 30f * i, 6f, 125f + 30f * j);
                    lit.transform.position = c;
                    for (int k = 0; k < 4; k++)
                    {
                        camGo.transform.position = c;
                        camGo.transform.rotation = Quaternion.Euler(-12f, 45f + 90f * k, 0f);
                        Shoot(cam, System.IO.Path.Combine(dir, "cell_" + i + j + "_corner" + k + ".png"), 800, 450);
                    }
                }
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(lit);
            Debug.Log("[Level1Inspect] corners written to " + dir);
        }

        static void Shoot(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 2;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
#endif
