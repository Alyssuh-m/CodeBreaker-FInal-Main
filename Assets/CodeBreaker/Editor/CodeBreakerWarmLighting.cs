using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Code Breaker > Lighting > Apply Warm Lighting (Level 1 + Level 2)
// Lighting-only pass: warm brownish-orange indoor light, visible lamps, soft shadows, slightly dark
// but readable. Changes only lights, ambient / fog settings, each level's own post-processing
// profile, and a few new light fixtures (wall sconces, lectern lights). No layout, object, script or
// puzzle changes. Safe to run again (sets absolute values; rebuilds only what it created).
public static class CodeBreakerWarmLighting
{
    const string Level1 = "Assets/Scenes/Level 1.unity";
    const string Level2 = "Assets/Scenes/Level 2.unity";
    const string SettingsDir = "Assets/CodeBreaker/Settings";
    const string MatDir = "Assets/CodeBreaker/Settings/Lighting";
    const string Gen = "Warm Lighting (generated)";

    static StringBuilder log;

    [MenuItem("Code Breaker/Lighting/Apply Warm Lighting (Level 1 + Level 2)", false, 300)]
    public static void ApplyBoth()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        log = new StringBuilder("Warm lighting pass  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(SettingsDir, "Lighting");
        DoLevel1();
        DoLevel2();
        AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("WarmLighting_Report.txt", log.ToString());
        Debug.Log(log.ToString());
    }

    [MenuItem("Code Breaker/Lighting/Apply Warm Lighting - Level 1 only", false, 301)]
    public static void ApplyLevel1() { log = new StringBuilder(); if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(SettingsDir, "Lighting"); DoLevel1(); AssetDatabase.SaveAssets(); Debug.Log(log.ToString()); }

    [MenuItem("Code Breaker/Lighting/Apply Warm Lighting - Level 2 only", false, 302)]
    public static void ApplyLevel2() { log = new StringBuilder(); if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(SettingsDir, "Lighting"); DoLevel2(); AssetDatabase.SaveAssets(); Debug.Log(log.ToString()); }

    // ================================================================== Level 1
    // Cause of the darkness: one ceiling point light per 30x30 room, 11 units up (URP light falls off
    // with distance squared, so floors and corners got very little), a dim flat ambient, a black
    // directional light, and post-processing with -28 saturation, +14 contrast and a dark vignette.
    static void DoLevel1()
    {
        Scene s = EditorSceneManager.OpenScene(Level1);
        log.AppendLine("\n== Level 1 ==");

        // ambient: warm and brighter (no black corners), soft warm fog
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.53f, 0.38f, 0.27f);
        RenderSettings.reflectionIntensity = 0.35f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.004f;
        RenderSettings.fogColor = new Color(0.10f, 0.065f, 0.04f);
        log.AppendLine("Ambient (0.25,0.19,0.17) -> (0.53,0.38,0.27); fog density 0.008 -> 0.004 warm brown");

        // ceiling lamps: warm amber, stronger, soft shadows on the main room lamps
        var roomCentres = new List<Vector3>();
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            Transform p = l.transform.parent;
            string parent = p != null ? p.name : "";
            if (l.name != "Point Light" || !parent.StartsWith("Ceiling Lamp")) continue;
            bool fire = parent.Contains("Fire Room");
            if (fire)
            {
                // the fire-trap rooms keep their own look; just slightly warmer
                l.color = new Color(1f, 0.82f, 0.5f);
                continue;
            }
            l.color = new Color(1f, 0.72f, 0.45f);
            l.intensity = 100f;
            l.range = 60f;
            bool trapCopy = parent.Contains("trap copy");
            l.shadows = trapCopy ? LightShadows.None : LightShadows.Soft;
            l.shadowStrength = 0.55f;
            l.shadowBias = 0.05f;
            l.shadowNormalBias = 0.4f;
            if (!trapCopy) roomCentres.Add(new Vector3(l.transform.position.x, 0f, l.transform.position.z));
        }
        log.AppendLine("Ceiling lamps: 37 -> 100, colour amber (1,0.72,0.45), soft shadows on " + roomCentres.Count + " main room lamps");

        // generated fixtures (rebuilt each run)
        GameObject lighting = Root(s, "Lighting");
        Transform gen = Fresh(lighting.transform, Gen);
        Material sconceGlow = Emissive("L1_SconceGlow", new Color(1f, 0.62f, 0.32f), 2.2f);
        Material sconceMetal = Lit("L1_SconceMetal", new Color(0.22f, 0.16f, 0.11f), 0.5f, 0.6f);

        int sconces = 0;
        foreach (Vector3 c in roomCentres) sconces += WallSconces(gen, c, 7.4f, sconceGlow, sconceMetal);
        log.AppendLine("Wall sconces added: " + sconces + " (warm, visible fixtures + small point lights)");

        // a soft warm pool on every lectern (the clue / question), no shadows
        int lect = 0;
        GameObject lecterns = Root(s, "Lecterns");
        if (lecterns != null)
            foreach (Transform t in lecterns.transform)
            {
                Bounds b = Bounds(t);
                if (b.size == Vector3.zero) continue;
                var go = new GameObject("Lectern Light - " + t.name);
                go.transform.SetParent(gen, false);
                go.transform.position = new Vector3(b.center.x, b.max.y + 5.5f, b.center.z);
                go.transform.rotation = Quaternion.LookRotation(Vector3.down);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.78f, 0.52f);
                l.intensity = 30f;
                l.range = 14f;
                l.spotAngle = 55f;
                l.innerSpotAngle = 25f;
                l.shadows = LightShadows.None;
                lect++;
            }
        log.AppendLine("Lectern lights added: " + lect);

        VolumeProfile prof = ProfileCopy("CodeBreaker_HorrorVolumeProfile.asset", "CodeBreaker_Level1_WarmProfile.asset");
        Post(prof, exposure: 0.55f, contrast: 10f, filter: new Color(1f, 0.86f, 0.70f), saturation: -8f,
            vignette: 0.30f, vignetteColor: new Color(0.09f, 0.045f, 0.02f), bloom: 0.35f, bloomThreshold: 0.95f, grain: 0.14f);
        AssignProfile(prof);
        log.AppendLine("Post: own profile CodeBreaker_Level1_WarmProfile (exposure 0.25->0.55, saturation -28->-8, contrast 14->10, warm filter, brown vignette 0.34->0.30)");

        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
    }

    // Two sconces per room on the walls with the most free space (away from doors and signs).
    static int WallSconces(Transform parent, Vector3 centre, float height, Material glow, Material metal)
    {
        Physics.SyncTransforms();
        Vector3 o = centre + Vector3.up * height;
        var cands = new List<(Vector3 point, Vector3 normal, float score)>();
        foreach (Vector3 dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            if (!Physics.Raycast(o, dir, out RaycastHit h, 25f, ~0, QueryTriggerInteraction.Ignore)) continue;
            // distance to the nearest door-ish object on this wall
            float nearest = 99f;
            foreach (Collider c in Physics.OverlapSphere(h.point, 7f, ~0, QueryTriggerInteraction.Collide))
            {
                string n = c.transform.root.name + "/" + c.name;
                if (n.Contains("Door") || n.Contains("Sign") || n.Contains("Lectern"))
                    nearest = Mathf.Min(nearest, Vector3.Distance(c.ClosestPoint(h.point), h.point));
            }
            cands.Add((h.point, h.normal, nearest));
        }
        cands.Sort((a, b) => b.score.CompareTo(a.score));
        int made = 0;
        for (int i = 0; i < cands.Count && made < 2; i++)
        {
            var (point, normal, score) = cands[i];
            if (score < 3f) continue;
            var root = new GameObject("Wall Sconce");
            root.transform.SetParent(parent, false);
            root.transform.position = point + normal * 0.05f;
            root.transform.rotation = Quaternion.LookRotation(normal);
            Part(root.transform, "Backplate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.05f), new Vector3(0.9f, 1.3f, 0.1f), metal);
            Part(root.transform, "Arm", PrimitiveType.Cube, new Vector3(0f, -0.2f, 0.3f), new Vector3(0.14f, 0.14f, 0.5f), metal);
            Part(root.transform, "Shade", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.6f), new Vector3(0.6f, 0.75f, 0.6f), glow);
            var lgo = new GameObject("Sconce Light");
            lgo.transform.SetParent(root.transform, false);
            lgo.transform.localPosition = new Vector3(0f, 0.1f, 1.1f);
            Light l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.66f, 0.38f);
            l.intensity = 18f;
            l.range = 16f;
            l.shadows = LightShadows.None;
            made++;
        }
        return made;
    }

    // ================================================================== Level 2
    // Level 2 was evenly bright and grey: a strong neutral flat ambient (0.62) washed out the lamps,
    // and the neutral sample post profile added no warmth or depth.
    static void DoLevel2()
    {
        Scene s = EditorSceneManager.OpenScene(Level2);
        log.AppendLine("\n== Level 2 ==");

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.32f, 0.24f);
        RenderSettings.ambientSkyColor = RenderSettings.ambientLight;
        RenderSettings.reflectionIntensity = 0.45f;
        RenderSettings.fog = false;
        log.AppendLine("Ambient (0.62,0.60,0.56) -> warm (0.42,0.32,0.24); the lamps now carry the light");

        // name -> (colour, intensity, range, soft shadows)
        var table = new Dictionary<string, (Color c, float i, float r, bool sh)>
        {
            // Living Room: warm and cosy
            { "Light_Living_Ceiling",            (new Color(1f, 0.76f, 0.50f), 52f, 36f, true) },
            { "Light_Living_FloorLamp",          (new Color(1f, 0.68f, 0.40f), 16f, 13f, false) },
            { "Light_Living_TableLamp",          (new Color(1f, 0.68f, 0.40f), 12f, 11f, false) },
            // Bedroom: warm and dim (it must still go properly dark with the switch off)
            { "Light_Bedroom_Ceiling",           (new Color(1f, 0.74f, 0.48f), 44f, 34f, true) },
            { "Light_Bedroom_BedsideLamp_Left",  (new Color(1f, 0.66f, 0.38f), 12f, 11f, false) },
            { "Light_Bedroom_BedsideLamp_Right", (new Color(1f, 0.66f, 0.38f), 12f, 11f, false) },
            // Kitchen: brightest, slightly cleaner warm white
            { "Light_Kitchen_Ceiling",           (new Color(1f, 0.84f, 0.64f), 66f, 40f, true) },
            { "Light_Kitchen_Pendant",           (new Color(1f, 0.74f, 0.48f), 18f, 15f, false) },
            { "Light_Kitchen_UnderCabinet",      (new Color(1f, 0.80f, 0.58f), 9f, 10f, false) },
            // Study: warm desk-lamp mood
            { "Light_Study_Ceiling",             (new Color(1f, 0.80f, 0.58f), 50f, 36f, true) },
            { "Light_Study_DeskLamp",            (new Color(1f, 0.70f, 0.42f), 16f, 11f, false) },
            { "Light_Study_FloorLamp",           (new Color(1f, 0.70f, 0.42f), 12f, 11f, false) },
            // halls: even warm light, a little dimmer than the rooms
            { "Light_Hallway_B",                 (new Color(1f, 0.74f, 0.48f), 22f, 20f, false) },
            { "Light_Hallway_B_DoorFill",        (new Color(1f, 0.74f, 0.48f), 7f, 10f, false) },
            { "Light_Hallway_A",                 (new Color(1f, 0.74f, 0.48f), 22f, 20f, false) },
            { "Light_Hallway_A_DoorFill",        (new Color(1f, 0.74f, 0.48f), 7f, 10f, false) },
            { "Light_Hallway_Corridor",          (new Color(1f, 0.74f, 0.48f), 22f, 20f, false) },
        };
        int n = 0;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (!table.TryGetValue(l.name, out var v)) continue;
            l.color = v.c;
            l.intensity = v.i;
            l.range = v.r;
            l.shadows = v.sh ? LightShadows.Soft : LightShadows.None;
            if (v.sh) { l.shadowStrength = 0.6f; l.shadowBias = 0.05f; l.shadowNormalBias = 0.4f; }
            n++;
        }
        log.AppendLine("Room / hall lights retuned: " + n + " (warm amber, lamps 2-3x stronger so they read as light sources)");

        VolumeProfile prof = ProfileCopy("CodeBreaker_HorrorVolumeProfile.asset", "CodeBreaker_Level2_WarmProfile.asset");
        Post(prof, exposure: 0.15f, contrast: 12f, filter: new Color(1f, 0.90f, 0.78f), saturation: 4f,
            vignette: 0.24f, vignetteColor: new Color(0.10f, 0.06f, 0.03f), bloom: 0.3f, bloomThreshold: 0.95f, grain: 0.08f);
        AssignProfile(prof);
        log.AppendLine("Post: own profile CodeBreaker_Level2_WarmProfile (warm filter, gentle contrast and vignette, light grain)");

        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
    }

    // ================================================================== helpers
    static GameObject Root(Scene s, string name)
    {
        foreach (GameObject g in s.GetRootGameObjects()) if (g.name == name) return g;
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, s);
        return go;
    }

    static Transform Fresh(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static Bounds Bounds(Transform t)
    {
        bool any = false;
        Bounds b = new Bounds();
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? b : new Bounds(t.position, Vector3.zero);
    }

    static Material Lit(string name, Color c, float smooth, float metal)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color c, float strength)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * strength);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static VolumeProfile ProfileCopy(string srcName, string dstName)
    {
        string dst = SettingsDir + "/" + dstName;
        VolumeProfile p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dst);
        if (p == null)
        {
            AssetDatabase.CopyAsset(SettingsDir + "/" + srcName, dst);
            AssetDatabase.ImportAsset(dst);
            p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dst);
        }
        return p;
    }

    static void Post(VolumeProfile p, float exposure, float contrast, Color filter, float saturation,
        float vignette, Color vignetteColor, float bloom, float bloomThreshold, float grain)
    {
        if (!p.TryGet(out ColorAdjustments ca)) ca = p.Add<ColorAdjustments>(true);
        ca.active = true;
        ca.postExposure.Override(exposure);
        ca.contrast.Override(contrast);
        ca.colorFilter.Override(filter);
        ca.saturation.Override(saturation);
        if (!p.TryGet(out Vignette vg)) vg = p.Add<Vignette>(true);
        vg.active = true;
        vg.intensity.Override(vignette);
        vg.smoothness.Override(0.55f);
        vg.color.Override(vignetteColor);
        if (!p.TryGet(out Bloom bl)) bl = p.Add<Bloom>(true);
        bl.active = true;
        bl.intensity.Override(bloom);
        bl.threshold.Override(bloomThreshold);
        if (!p.TryGet(out FilmGrain fg)) fg = p.Add<FilmGrain>(true);
        fg.active = grain > 0f;
        fg.intensity.Override(grain);
        if (!p.TryGet(out Tonemapping tm)) tm = p.Add<Tonemapping>(true);
        tm.active = true;
        tm.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(p);
        foreach (VolumeComponent c in p.components) EditorUtility.SetDirty(c);
    }

    static void AssignProfile(VolumeProfile p)
    {
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            if (v.isGlobal) { v.sharedProfile = p; EditorUtility.SetDirty(v); }
    }
}
