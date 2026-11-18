// Code Breaker - Level 2 verification shots (editor only). Renders a ceiling-less top view and
// eye-height views of every room to <project>/Level2_Captures/*.png. Changes nothing in the scene.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodeBreaker.Level2Tools
{
    public static class Level2VerifyCapture
    {
        const float S = 3.75f;
        static Vector3 U(float x, float y, float z) { return new Vector3(x * S, z * S, y * S); }

        [MenuItem("Code Breaker/Level 2/Capture Verification Shots")]
        public static void Capture()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Level2_Captures"));
            Directory.CreateDirectory(dir);
            var scene = EditorSceneManager.GetActiveScene();
            var log = new System.Text.StringBuilder();

            var camGo = new GameObject("__VerifyCam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);

            // hide the player model so it does not block eye-height shots
            var hidden = new List<GameObject>();
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == "Player" && r.activeSelf) { r.SetActive(false); hidden.Add(r); }

            // --- top view without ceilings
            var ceil = new List<GameObject>();
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(false))
                    if (t.name.Contains("Ceiling") || t.name.Contains("SmokeDetector") || t.name.Contains("GlowStar") || t.name.Contains("PendantLight") || t.name.Contains("RangeHood"))
                        if (t.gameObject.activeSelf && t.GetComponentInParent<Light>() == null) { ceil.Add(t.gameObject); }
            foreach (var g in ceil) g.SetActive(false);
            cam.orthographic = true;
            cam.orthographicSize = 10.0f * S;
            camGo.transform.position = U(-4.3f, 0.8f, 30f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Shoot(cam, Path.Combine(dir, "00_TopView.png"), 2400, 2160);
            // zoomed halls
            cam.orthographicSize = 3.2f * S;
            camGo.transform.position = U(-2.6f, 0f, 30f);
            Shoot(cam, Path.Combine(dir, "01_TopView_Halls.png"), 1600, 1600);
            foreach (var g in ceil) if (g != null) g.SetActive(true);
            cam.orthographic = false;

            var shots = new List<object[]> {
                new object[] { "10_Living_Overview", -7.6f, -5.4f, 1.7f, -12f, -2.5f, 0.6f },
                new object[] { "11_Living_ConsoleDoor", -9.5f, -2.0f, 1.6f, -6.4f, -2.6f, 1.0f },
                new object[] { "12_Living_Bookshelf", -8.6f, -2.2f, 1.4f, -8.4f, 0.4f, 0.9f },
                new object[] { "13_Living_CoffeeTable", -10.4f, -1.6f, 1.6f, -12.0f, -3.3f, 0.4f },
                new object[] { "14_Living_TVSideboard", -10.6f, -5.6f, 1.6f, -13.6f, -6.6f, 0.7f },
                new object[] { "20_HallB", -2.95f, -3.25f, 1.6f, -6.2f, -3.25f, 1.1f },
                new object[] { "21_Bedroom_Overview", -1.2f, -3.6f, 1.7f, 2.5f, -6.8f, 0.8f },
                new object[] { "22_Bedroom_Vanity", 0.4f, -5.6f, 1.6f, -2.5f, -5.6f, 1.1f },
                new object[] { "23_Bedroom_CeilingStars", 2.2f, -4.4f, 1.0f, 2.2f, -6.6f, 2.9f },
                new object[] { "24_Bedroom_Bedside", 2.2f, -5.4f, 1.5f, 2.2f, -7.7f, 0.6f },
                new object[] { "30_Corridor", 0.0f, -1.3f, 1.6f, 0.0f, 2.5f, 1.2f },
                new object[] { "31_Kitchen_Overview", -1.6f, 2.5f, 1.7f, 3.5f, 7.6f, 0.9f },
                new object[] { "32_Kitchen_Counter", 3.2f, 6.0f, 1.6f, 4.6f, 7.8f, 0.95f },
                new object[] { "33_Kitchen_Fridge", 1.0f, 5.6f, 1.6f, 0.4f, 7.3f, 1.2f },
                new object[] { "40_HallA", -2.95f, 3.25f, 1.6f, -6.2f, 3.25f, 1.1f },
                new object[] { "41_Study_Overview", -6.8f, 2.9f, 1.7f, -10.5f, 8.5f, 0.9f },
                new object[] { "42_Study_Desk", -9.9f, 7.7f, 1.5f, -9.9f, 9.3f, 0.95f },
                new object[] { "43_Study_WorkTable", -8.1f, 4.0f, 1.5f, -9.2f, 5.0f, 0.75f },
                new object[] { "44_Study_Cert", -11.2f, 5.0f, 1.6f, -12.6f, 7.5f, 1.2f },
            };
            foreach (var s in shots)
            {
                camGo.transform.position = U((float)s[1], (float)s[2], (float)s[3]);
                camGo.transform.LookAt(U((float)s[4], (float)s[5], (float)s[6]));
                Shoot(cam, Path.Combine(dir, (string)s[0] + ".png"), 1280, 800);
            }
            foreach (var g in hidden) g.SetActive(true);
            Object.DestroyImmediate(camGo);
            Debug.Log("[Level2Verify] captures written to " + dir);
        }

        static void Shoot(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
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
