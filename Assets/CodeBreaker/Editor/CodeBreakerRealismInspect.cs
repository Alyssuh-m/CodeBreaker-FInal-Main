using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Code Breaker > Realism Pass > Inspect (read-only): dumps details about a few objects and renders
// the oven with its door open. Nothing in the scene is changed (the oven door is put back).
public static class CodeBreakerRealismInspect
{
    [MenuItem("Code Breaker/Realism Pass/Inspect Level 2 Objects (read-only)", false, 600)]
    static void Inspect()
    {
        var log = new StringBuilder();
        foreach (string n in new[] { "STU_Laptop", "KIT_RangeHood", "KIT_Refrigerator", "KIT_Stove_Range", "KIT_Stove_OvenDoor", "KIT_FridgeNotes", "Puzzle_Bedroom_JewelleryBox_5Drawers", "Body", "Lid", "Lid_Mirror", "STU_SideTable_Mug", "Puzzle_Study_DogCollar_TagBYTE", "DogBowl", "KIT_Pantry", "Collider_KIT_Pantry", "KIT_TrashBin", "Collider_KIT_TrashBin" })
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(x => x.name == n))
            {
                log.AppendLine("== " + CodeBreakerFixPass.PathOf(t));
                log.AppendLine("  pos " + t.position.ToString("F3") + " rot " + t.eulerAngles.ToString("F1") + " lossy " + t.lossyScale.ToString("F3") + " right " + t.right.ToString("F2") + " up " + t.up.ToString("F2") + " fwd " + t.forward.ToString("F2"));
                var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh m = mf.sharedMesh;
                    log.AppendLine("  mesh " + m.name + " path=" + AssetDatabase.GetAssetPath(m) + " readable=" + m.isReadable + " verts=" + m.vertexCount + " sub=" + m.subMeshCount + " lb=" + m.bounds.center.ToString("F3") + "/" + m.bounds.size.ToString("F3"));
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        var sd = m.GetSubMesh(s);
                        string mat = mr != null && s < mr.sharedMaterials.Length && mr.sharedMaterials[s] != null ? mr.sharedMaterials[s].name : "-";
                        log.AppendLine("    sub" + s + " " + mat + " tris=" + sd.indexCount / 3 + " b=" + sd.bounds.center.ToString("F3") + "/" + sd.bounds.size.ToString("F3"));
                    }
                }
                if (mr != null) log.AppendLine("  world bounds " + mr.bounds.center.ToString("F3") + " / " + mr.bounds.size.ToString("F3"));
                foreach (Transform c in t) log.AppendLine("  child " + c.name + " pos " + c.position.ToString("F3") + " rot " + c.eulerAngles.ToString("F1") + (c.GetComponent<Renderer>() != null ? " b=" + c.GetComponent<Renderer>().bounds.center.ToString("F3") + "/" + c.GetComponent<Renderer>().bounds.size.ToString("F3") : ""));
                foreach (var co in t.GetComponents<Collider>()) log.AppendLine("  collider " + co.GetType().Name + " trig=" + co.isTrigger + " b=" + co.bounds.center.ToString("F3") + "/" + co.bounds.size.ToString("F3"));
            }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_RealismInspect.txt", log.ToString());

        // oven open shots
        var oven = Object.FindAnyObjectByType<CodeBreaker.Level2.L2Oven>(FindObjectsInactive.Include);
        if (oven != null && oven.ovenDoor != null)
        {
            Transform door = oven.ovenDoor;
            Vector3 p0 = door.position; Quaternion r0 = door.rotation;
            Renderer r = door.GetComponent<Renderer>();
            Bounds b = r != null ? r.bounds : new Bounds(door.position, Vector3.one);
            Vector3 hinge = new Vector3(b.center.x, b.min.y, b.min.z);
            door.RotateAround(hinge, Vector3.right, oven.doorAngle);
            bool keyWas = oven.key != null && oven.key.activeSelf;
            if (oven.key != null) oven.key.SetActive(true);
            bool noteWas = oven.doorNote != null && oven.doorNote.activeSelf;
            if (oven.doorNote != null) oven.doorNote.SetActive(true);
            Shots("Oven", b.center + new Vector3(0, 0.2f, 0.6f), new[] {
                b.center + new Vector3(0f, 2.2f, -2.4f), b.center + new Vector3(0f, 1.0f, -1.8f), b.center + new Vector3(1.2f, 1.6f, -1.8f), b.center + new Vector3(0f, 3.4f, -0.8f) });
            door.SetPositionAndRotation(p0, r0);
            if (oven.key != null) oven.key.SetActive(keyWas);
            if (oven.doorNote != null) oven.doorNote.SetActive(noteWas);
        }
        Debug.Log("[Realism Inspect] written Logs/CB_RealismInspect.txt and Logs/Shots/Oven_*.png");
    }

    static void Shots(string name, Vector3 look, Vector3[] from)
    {
        Directory.CreateDirectory("Logs/Shots");
        var go = new GameObject("CB_InspectCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        if (Camera.main != null) cam.CopyFrom(Camera.main);
        cam.enabled = false; cam.fieldOfView = 60f; cam.nearClipPlane = 0.02f;
        var rt = new RenderTexture(800, 450, 24); cam.targetTexture = rt;
        var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
        for (int i = 0; i < from.Length; i++)
        {
            cam.transform.position = from[i]; cam.transform.LookAt(look);
            cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); tex.Apply(); RenderTexture.active = null;
            File.WriteAllBytes("Logs/Shots/" + name + "_open_" + i + ".png", tex.EncodeToPNG());
        }
        cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
