// Read-only verification tool: renders each Level 2 window from inside the room.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Level2WindowCapture
{
    [MenuItem("Code Breaker/Level 2/Capture Window Views")]
    public static void Capture()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Level 2.unity");
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level2_Inspect");
        Directory.CreateDirectory(dir);
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>())
        {
            var m = r.sharedMaterial;
            if (m == null || !m.name.Contains("OutdoorView")) continue;
            Bounds b = r.bounds;
            Vector3 thin = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            // pick the side facing the house centre (origin) as "inside"
            Vector3 toIn = Vector3.Dot(-b.center, thin) >= 0 ? thin : -thin;
            var go = new GameObject("TmpCapCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 70; cam.nearClipPlane = 0.05f;
            go.transform.position = b.center + toIn * 2.2f + Vector3.down * 0.1f;
            go.transform.rotation = Quaternion.LookRotation(-toIn);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "window_" + (n++) + "_" + r.name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
        Debug.Log("[Level2WindowCapture] " + n + " window captures written to " + dir);
    }
}
