using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

// Code Breaker: horror polish + startup / menu / loading UI.
// Every step is a menu item under  Code Breaker > Polish  and can be run again safely.
// Before a step changes scenes or materials it copies them to  <project>/Backups (Code Breaker Polish)/<time>/.
//
//   1. Apply Horror Environment     darker blood-stained walls + ceiling (new material copies), matte floor,
//                                   dimmer warm lamps, dark ambient + fog, horror post-processing (Levels 1-3)
//   2. Fit Lectern + Style Results  lectern question fits any screen; Congratulations screen gets
//                                   a framed panel + TRY AGAIN / NEXT LEVEL / EXIT TO HOME (Level 1)
//   3. Capture Level Images         renders each level from its player camera (source for loading screens)
//   4. Build Menus + Loading        Boot (logo) scene, MainMenu scene, level loading screen prefab,
//                                   Level Catalog, Build Settings order
public static class CodeBreakerPolishTools
{
    const string Tag = "[Code Breaker Polish] ";
    const string Root = "Assets/CodeBreaker";
    const string SpriteDir = Root + "/UI/Sprites";
    const string FontDir = Root + "/UI/Fonts";
    const string LoadingDir = Root + "/UI/Loading";
    const string CaptureDir = Root + "/UI/Loading/Captures";
    const string EnvTexDir = Root + "/Textures/Environment";
    const string EnvMatDir = Root + "/Materials/Environment";
    const string SettingsDir = Root + "/Settings";
    const string ResourcesDir = Root + "/Resources/CodeBreaker";
    const string ScenesDir = "Assets/Scenes";
    const string BootScene = ScenesDir + "/Boot.unity";
    const string MenuScene = ScenesDir + "/MainMenu.unity";
    static readonly string[] LevelScenes = { ScenesDir + "/Level 1.unity", ScenesDir + "/Level 2.unity", ScenesDir + "/Level 3.unity" };

    // palette
    static readonly Color Bone = new Color(0.89f, 0.85f, 0.79f);
    static readonly Color BoneDim = new Color(0.66f, 0.62f, 0.58f);
    static readonly Color Blood = new Color(0.62f, 0.07f, 0.05f);
    static readonly Color BloodDark = new Color(0.36f, 0.03f, 0.03f);
    static readonly Color Charcoal = new Color(0.07f, 0.06f, 0.06f);

    // ======================================================================================
    //  1. ENVIRONMENT
    // ======================================================================================
    [MenuItem("Code Breaker/Polish/1. Apply Horror Environment (Levels 1-3)", false, 200)]
    public static void ApplyHorrorEnvironment()
    {
        if (Application.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = SceneManager.GetActiveScene().path;

        var log = new StringBuilder("HORROR ENVIRONMENT\n");
        EnsureFolders();
        ImportEnvironmentOverlays();

        string[] scenes = LevelScenes.Where(File.Exists).ToArray();
        var matPaths = new List<string>();
        foreach (string s in scenes)
        {
            Scene sc = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
            foreach (Renderer r in AllInScene<Renderer>(sc))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && Classify(SourceOf(m)) != Surface.None) matPaths.Add(AssetDatabase.GetAssetPath(m));
        }
        Backup("environment", scenes.Concat(matPaths.Distinct()), log);

        var copies = new Dictionary<Material, Material>();
        foreach (string s in scenes)
        {
            Scene sc = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
            int swapped = 0;
            foreach (Renderer r in AllInScene<Renderer>(sc))
            {
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i] != null ? SourceOf(mats[i]) : null;   // a horror copy from an earlier run -> its original
                    if (m == null) continue;
                    Surface kind = Classify(m);
                    if (kind == Surface.None) continue;
                    if (!copies.TryGetValue(m, out Material copy))
                    {
                        copy = HorrorMaterial(m, kind, log);
                        copies[m] = copy;
                    }
                    if (copy != null && copy != mats[i]) { mats[i] = copy; changed = true; }
                    if (copy != null) swapped++;
                }
                if (changed) { Undo.RecordObject(r, "Horror materials"); r.sharedMaterials = mats; }
            }
            log.AppendLine(sc.name + ": " + swapped + " material slots now use the horror versions");

            ApplyLighting(sc, log);
            ApplyPostProcessing(sc, log);
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
        }
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(returnTo) && File.Exists(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log(Tag + log);
    }

    enum Surface { None, Wall, Ceiling, Floor }

    // For a "<name>_Horror" copy made by an earlier run, returns the original material it was made from.
    static Material SourceOf(Material m)
    {
        string p = AssetDatabase.GetAssetPath(m).Replace('\\', '/');
        if (!p.StartsWith(EnvMatDir) || !m.name.EndsWith("_Horror")) return m;
        string original = m.name.Substring(0, m.name.Length - "_Horror".Length);
        foreach (string guid in AssetDatabase.FindAssets(original + " t:Material", new[] { "Assets/Walls", "Assets/ALP_Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == original) return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        return m;
    }

    static Surface Classify(Material m)
    {
        string p = AssetDatabase.GetAssetPath(m).Replace('\\', '/');
        if (p.StartsWith(EnvMatDir)) return Surface.None;   // already a horror copy
        if (p == "Assets/Walls/Ceiling.mat") return Surface.Ceiling;
        if (p.StartsWith("Assets/Walls/")) return Surface.Wall;                     // Inner, Outer, Top
        if (p.StartsWith("Assets/ALP_Assets/Old Floor Linoleum FREE/")) return Surface.Floor;
        return Surface.None;
    }

    static void ImportEnvironmentOverlays()
    {
        foreach (string f in new[] { "CB_WallGrimeBlood.png", "CB_CeilingGrime.png" })
        {
            string p = EnvTexDir + "/" + f;
            if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
            if (ti.isReadable && ti.textureCompression == TextureImporterCompression.Uncompressed) continue;
            ti.textureType = TextureImporterType.Default;
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    // Copy of the original material (the original stays untouched) with a darkened, blood-stained texture.
    static Material HorrorMaterial(Material src, Surface kind, StringBuilder log)
    {
        string path = EnvMatDir + "/" + src.name + "_Horror.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(src);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = src.shader;
            m.CopyPropertiesFromMaterial(src);
        }
        m.name = src.name + "_Horror";

        Texture baseTex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture;
        Color baseColor = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;

        switch (kind)
        {
            case Surface.Wall:
            case Surface.Ceiling:
            {
                string overlayPath = EnvTexDir + (kind == Surface.Wall ? "/CB_WallGrimeBlood.png" : "/CB_CeilingGrime.png");
                Texture2D overlay = AssetDatabase.LoadAssetAtPath<Texture2D>(overlayPath);
                // dark, desaturated reddish-brown (matches the horror door)
                Color tint = kind == Surface.Wall ? new Color(0.84f, 0.70f, 0.63f) : new Color(0.72f, 0.62f, 0.57f);
                Texture2D baked = baseTex != null && overlay != null
                    ? BakeTexture(baseTex, overlay, tint, kind == Surface.Wall ? 0.35f : 0.4f, kind == Surface.Wall ? 0.8f : 0.55f,
                                  EnvTexDir + "/" + baseTex.name + (kind == Surface.Wall ? "_HorrorWall.png" : "_HorrorCeiling.png"))
                    : null;
                if (baked != null)
                {
                    SetTex(m, "_BaseMap", baked);
                    SetTex(m, "_MainTex", baked);
                }
                else SetCol(m, "_BaseColor", baseColor * tint);   // no texture: tint only
                SetFloat(m, "_Smoothness", kind == Surface.Wall ? 0.14f : 0.06f);
                SetFloat(m, "_Metallic", 0f);
                SetCol(m, "_SpecColor", new Color(0.08f, 0.07f, 0.07f));
                break;
            }
            case Surface.Floor:
                // The shine came from smoothness 0.5 reflecting the default sky (no reflection probes)
                // plus very bright lamps. A low smoothness gives a matte, worn floor.
                SetCol(m, "_BaseColor", new Color(baseColor.r * 0.74f, baseColor.g * 0.66f, baseColor.b * 0.62f, baseColor.a));
                SetFloat(m, "_Smoothness", 0.12f);
                SetFloat(m, "_Metallic", 0f);
                SetFloat(m, "_GlossMapScale", 0.12f);
                SetCol(m, "_SpecColor", new Color(0.1f, 0.09f, 0.09f));
                break;
        }
        EditorUtility.SetDirty(m);
        log.AppendLine("  " + src.name + " -> " + path + " (" + kind + ")");
        return m;
    }

    static void SetTex(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }
    static void SetCol(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
    static void SetFloat(Material m, string p, float f) { if (m.HasProperty(p)) m.SetFloat(p, f); }

    // base * tint (partly desaturated), then the grime / blood overlay blended on top.
    static Texture2D BakeTexture(Texture src, Texture2D overlay, Color tint, float desaturate, float overlayStrength, string outPath)
    {
        int w = src.width, h = src.height;
        float s = Mathf.Min(1f, 2048f / Mathf.Max(w, h));
        w = Mathf.Max(4, Mathf.RoundToInt(w * s)); h = Mathf.Max(4, Mathf.RoundToInt(h * s));

        var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0) { sRGB = true };
        RenderTexture rt = RenderTexture.GetTemporary(desc);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color32[] px = tex.GetPixels32();
        Color32[] ov = overlay.GetPixels32();
        int ow = overlay.width, oh = overlay.height;
        for (int y = 0; y < h; y++)
        {
            int oy = (int)((long)y * oh / h);
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color32 c = px[i];
                Color32 o = ov[oy * ow + (int)((long)x * ow / w)];
                float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                float lum = r * 0.3f + g * 0.59f + b * 0.11f;
                r = Mathf.Lerp(r, lum, desaturate) * tint.r;
                g = Mathf.Lerp(g, lum, desaturate) * tint.g;
                b = Mathf.Lerp(b, lum, desaturate) * tint.b;
                float a = o.a / 255f * overlayStrength;
                r = Mathf.Lerp(r, o.r / 255f, a);
                g = Mathf.Lerp(g, o.g / 255f, a);
                b = Mathf.Lerp(b, o.b / 255f, a);
                px[i] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), 255);
            }
        }
        tex.SetPixels32(px);
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(outPath);
        if (AssetImporter.GetAtPath(outPath) is TextureImporter ti)
        {
            ti.sRGBTexture = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }

    static void ApplyLighting(Scene scene, StringBuilder log)
    {
        // Dark, slightly warm ambient instead of the bluish default sky light; enough to still read the room.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.19f, 0.17f);
        RenderSettings.reflectionIntensity = 0.3f;   // indoor room: the sky must not show up in reflections
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.035f, 0.024f, 0.022f);

        int tuned = 0;
        foreach (Light l in AllInScene<Light>(scene))
        {
            if (l.type != LightType.Point) continue;
            string n = l.name + " " + (l.transform.parent != null ? l.transform.parent.name : "");
            if (n.Contains("Fire") || n.Contains("Burn")) continue;   // fire effects stay as they are
            // absolute values (not multipliers), so running this again changes nothing
            // (also matches the values of an earlier run, so it can be re-tuned)
            float target = Is(l.intensity, 40f, 27f, 34f) ? 37f
                         : Is(l.intensity, 22f, 15f, 19f) ? 21f
                         : Is(l.intensity, 7f, 5f, 6f) ? 6.5f : -1f;
            if (target < 0f) continue;
            Undo.RecordObject(l, "Horror lighting");
            l.intensity = target;
            l.color = new Color(1f, 0.8f, 0.6f);   // old, warm, slightly dirty bulbs
            tuned++;
        }
        log.AppendLine(scene.name + ": ambient/fog darkened, " + tuned + " lamps dimmed and warmed");
    }

    static bool Is(float v, params float[] any) => any.Any(a => Mathf.Approximately(v, a));

    static VolumeProfile horrorProfile;

    static VolumeProfile HorrorProfile(Volume existing)
    {
        string path = SettingsDir + "/CodeBreaker_HorrorVolumeProfile.asset";
        if (horrorProfile == null) horrorProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (horrorProfile == null)
        {
            horrorProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(horrorProfile, path);
            // start from the project's current look (deep copies, the original profile is not changed)
            if (existing != null && existing.sharedProfile != null)
                foreach (VolumeComponent c in existing.sharedProfile.components)
                {
                    if (c == null) continue;
                    VolumeComponent copy = Object.Instantiate(c);
                    copy.name = c.name;
                    horrorProfile.components.Add(copy);
                    AssetDatabase.AddObjectToAsset(copy, horrorProfile);
                }
        }

        ColorAdjustments ca = Get<ColorAdjustments>(horrorProfile);
        ca.postExposure.Override(0.25f);
        ca.contrast.Override(14f);
        ca.saturation.Override(-28f);
        ca.colorFilter.Override(new Color(1f, 0.9f, 0.86f));

        Vignette v = Get<Vignette>(horrorProfile);
        v.intensity.Override(0.34f);
        v.smoothness.Override(0.5f);
        v.color.Override(new Color(0.07f, 0f, 0f));

        FilmGrain fg = Get<FilmGrain>(horrorProfile);
        fg.type.Override(FilmGrainLookup.Medium3);
        fg.intensity.Override(0.22f);
        fg.response.Override(0.8f);

        EditorUtility.SetDirty(horrorProfile);
        return horrorProfile;
    }

    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet(out T c)) return c;
        c = p.Add<T>(false);
        c.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }

    static void ApplyPostProcessing(Scene scene, StringBuilder log)
    {
        Volume vol = AllInScene<Volume>(scene).FirstOrDefault(v => v.isGlobal);
        VolumeProfile profile = HorrorProfile(vol);
        if (vol == null)
        {
            GameObject go = new GameObject("Global Volume (Horror)");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Volume");
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
        }
        Undo.RecordObject(vol, "Horror volume");
        vol.sharedProfile = profile;

        int cams = 0;
        foreach (Camera cam in AllInScene<Camera>(scene))
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.renderPostProcessing) continue;
            Undo.RecordObject(data, "Post processing");
            data.renderPostProcessing = true;
            cams++;
        }
        log.AppendLine(scene.name + ": horror post-processing profile on '" + vol.name + "'" + (cams > 0 ? ", post-processing enabled on " + cams + " camera(s)" : ""));
    }

    // ======================================================================================
    //  2. LECTERN + RESULT SCREENS (Level 1)
    // ======================================================================================
    [MenuItem("Code Breaker/Polish/2. Fit Lectern + Style Result Screens (Level 1)", false, 201)]
    public static void FitLecternAndResults()
    {
        if (Application.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolders();
        ImportUIArt();
        Fonts f = LoadFonts();

        var log = new StringBuilder("LECTERN + RESULT SCREENS\n");
        Backup("lectern-results", new[] { LevelScenes[0] }, log);
        Scene sc = EditorSceneManager.OpenScene(LevelScenes[0], OpenSceneMode.Single);

        // ---- lectern question: scale with the screen, keep the page proportions
        foreach (LecternQuestion lq in AllInScene<LecternQuestion>(sc))
        {
            if (lq.questionCanvas == null) continue;
            Canvas canvas = lq.questionCanvas.GetComponentInParent<Canvas>(true);
            if (canvas == null) continue;
            ScaleWithScreen(canvas);
            Undo.RecordObject(canvas, "Order");
            canvas.sortingOrder = -1;   // the HUD (timer, health) stays visible on top of the page
            Transform page = canvas.transform.Find("Image");
            if (page != null)
            {
                UIFitToScreen fit = page.GetComponent<UIFitToScreen>();
                if (fit == null) fit = Undo.AddComponent<UIFitToScreen>(page.gameObject);
                RectTransform prt = (RectTransform)page;
                Image pimg = page.GetComponent<Image>();
                Vector2 size = prt.sizeDelta.x > 1f ? prt.sizeDelta : new Vector2(540, 720);
                if (pimg != null && pimg.sprite != null)
                {
                    // keep the page's own proportions (no stretching)
                    float aspect = pimg.sprite.rect.width / pimg.sprite.rect.height;
                    size = new Vector2(size.y * aspect, size.y);
                    Undo.RecordObject(pimg, "Page");
                    pimg.preserveAspect = true;
                }
                Undo.RecordObject(fit, "Fit");
                fit.designSize = size;
                fit.fill = 0.95f;
                fit.topReserve = 0.2f;   // the HUD timer + room label stay readable above the page
                Undo.RecordObject(prt, "Fit");
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                prt.anchoredPosition = Vector2.zero;
                prt.sizeDelta = size;
            }
            Transform bg = canvas.transform.Find("BG PANEL");
            if (bg != null && bg.GetComponent<Image>() is Image bgi)
            {
                Undo.RecordObject(bgi, "BG");
                bgi.color = new Color(0.03f, 0.015f, 0.015f, 0.82f);   // dim the room so the page stands out
                RectTransform brt = (RectTransform)bg;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            }
            log.AppendLine("Lectern '" + lq.name + "': canvas scales with screen size, page fits below the HUD without stretching");
        }

        // other overlay canvases that were Constant Pixel Size (interaction prompt etc.)
        foreach (Canvas c in AllInScene<Canvas>(sc))
        {
            if (c.renderMode == RenderMode.WorldSpace || !c.isRootCanvas) continue;
            CanvasScaler cs = c.GetComponent<CanvasScaler>();
            if (cs != null && cs.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                ScaleWithScreen(c);
                log.AppendLine("Canvas '" + c.name + "': Constant Pixel Size -> Scale With Screen Size (1920x1080)");
            }
        }

        // ---- result screens (Game Session canvas)
        GameFlowManager gm = AllInScene<GameFlowManager>(sc).FirstOrDefault();
        if (gm != null && gm.resultCanvas != null) StyleResultScreens(gm, f, log);
        else log.AppendLine("No Game Session / result canvas found in Level 1.");

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        Debug.Log(Tag + log);
    }

    static void ScaleWithScreen(Canvas c)
    {
        CanvasScaler cs = c.GetComponent<CanvasScaler>();
        if (cs == null) cs = Undo.AddComponent<CanvasScaler>(c.gameObject);
        Undo.RecordObject(cs, "Scaler");
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);   // identical to before at 1080p, scales everywhere else
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
    }

    static void StyleResultScreens(GameFlowManager gm, Fonts f, StringBuilder log)
    {
        Transform rc = gm.resultCanvas.transform;
        Canvas canvas = rc.GetComponent<Canvas>();
        if (canvas != null) ScaleWithScreen(canvas);
        Undo.RecordObject(gm, "Result screens");

        if (gm.resultBackdrop != null)
        {
            Undo.RecordObject(gm.resultBackdrop, "Backdrop");
            gm.resultBackdrop.color = new Color(0.02f, 0.005f, 0.005f, 0.8f);
            if (gm.resultBackdrop.transform.Find("Vignette") == null)
            {
                Image vg = Img("Vignette", gm.resultBackdrop.transform, Spr("CB_Vignette"), Color.white, false);
                Stretch(vg.rectTransform); vg.raycastTarget = false;
                Undo.RegisterCreatedObjectUndo(vg.gameObject, "Vignette");
            }
        }

        // framed panel for the full-screen results
        Image panel = rc.Find("Result Panel")?.GetComponent<Image>();
        if (panel == null)
        {
            panel = Img("Result Panel", rc, Spr("CB_Panel"), Color.white, true);
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Result Panel");
            Image div = Img("Divider", panel.transform, Spr("CB_Divider"), Color.white, false);
            Place(div.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 108), new Vector2(620, 58));
            div.raycastTarget = false;
        }
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1120, 540));
        panel.raycastTarget = true;
        panel.gameObject.SetActive(false);
        gm.resultPanel = panel.gameObject;

        // smaller panel for the short messages (YOU'RE ALIVE!, WRONG DOOR!, TRY AGAIN)
        Image msg = rc.Find("Message Panel")?.GetComponent<Image>();
        if (msg == null)
        {
            msg = Img("Message Panel", rc, Spr("CB_Panel"), new Color(1f, 1f, 1f, 0.92f), true);
            Undo.RegisterCreatedObjectUndo(msg.gameObject, "Message Panel");
        }
        Place(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 62), new Vector2(980, 250));
        msg.raycastTarget = false;
        msg.gameObject.SetActive(false);
        gm.messagePanel = msg.gameObject;

        // order: backdrop, panels, then the texts and buttons on top
        int idx = gm.resultBackdrop != null ? gm.resultBackdrop.transform.GetSiblingIndex() + 1 : 0;
        panel.transform.SetSiblingIndex(idx);
        msg.transform.SetSiblingIndex(idx + 1);

        if (gm.resultTitle != null)
        {
            Undo.RecordObject(gm.resultTitle, "Title");
            Undo.RecordObject(gm.resultTitle.rectTransform, "Title");
            if (f.title != null) gm.resultTitle.font = f.title;
            gm.resultTitle.fontSize = 74;
            gm.resultTitle.characterSpacing = 4;
            Place(gm.resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1060, 100));
            gm.resultTitle.alignment = TextAlignmentOptions.Center;
        }
        if (gm.resultDescription != null)
        {
            Undo.RecordObject(gm.resultDescription, "Desc");
            Undo.RecordObject(gm.resultDescription.rectTransform, "Desc");
            if (f.body != null) gm.resultDescription.font = f.body;
            gm.resultDescription.fontSize = 28;
            gm.resultDescription.lineSpacing = 8;
            Place(gm.resultDescription.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(940, 160));
            gm.resultDescription.alignment = TextAlignmentOptions.Center;
        }
        if (gm.tryAgainButton != null)   // the "PRESS R ..." line of Game Over
        {
            RectTransform br = gm.tryAgainButton.GetComponent<RectTransform>();
            Undo.RecordObject(br, "Press R");
            Place(br, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(640, 56));
            if (gm.tryAgainButton.GetComponent<Image>() is Image bi)
            {
                Undo.RecordObject(bi, "Press R");
                bi.sprite = Spr("CB_Button"); bi.type = Image.Type.Sliced; bi.color = Color.white;
            }
            TMP_Text label = gm.tryAgainButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && f.button != null) { Undo.RecordObject(label, "Press R"); label.font = f.button; label.fontSize = 26; label.characterSpacing = 6; }
        }

        // TRY AGAIN / NEXT LEVEL / EXIT TO HOME
        LevelCompleteMenu menu = rc.GetComponentInChildren<LevelCompleteMenu>(true);
        if (menu == null)
        {
            GameObject row = new GameObject("Level Complete Menu", typeof(RectTransform), typeof(CanvasGroup), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(rc, false);
            Undo.RegisterCreatedObjectUndo(row, "Level Complete Menu");
            HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 26; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            menu = row.AddComponent<LevelCompleteMenu>();
            menu.group = row.GetComponent<CanvasGroup>();
            menu.tryAgainButton = MakeButton(row.transform, "TRY AGAIN", f, 330, 78, 30);
            menu.nextLevelButton = MakeButton(row.transform, "NEXT LEVEL", f, 330, 78, 30);
            menu.exitHomeButton = MakeButton(row.transform, "EXIT TO HOME", f, 330, 78, 30);
            menu.nextLevelLabel = menu.nextLevelButton.GetComponentInChildren<TMP_Text>(true);
        }
        Place((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -205), new Vector2(1040, 78));
        menu.transform.SetAsLastSibling();
        menu.gameObject.SetActive(false);
        gm.levelCompleteMenu = menu;
        EditorUtility.SetDirty(gm);
        log.AppendLine("Result screens: framed panel + message panel, title in the horror font, Congratulations buttons TRY AGAIN / NEXT LEVEL / EXIT TO HOME wired to GameFlowManager");
    }

    // ======================================================================================
    //  3. CAPTURE LEVEL IMAGES
    // ======================================================================================
    // Rendered in Play mode (URP's post-processing only has its data there), HUD hidden.
    // The tool plays each level in turn, takes 4 views from the player camera and stops again.
    const string CaptureKey = "CodeBreakerPolish.CaptureIndex";
    const string CaptureReturnKey = "CodeBreakerPolish.CaptureReturn";

    [MenuItem("Code Breaker/Polish/3. Capture Level Images (for loading screens)", false, 202)]
    public static void CaptureLevelImages()
    {
        if (Application.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolders();
        SessionState.SetString(CaptureReturnKey, SceneManager.GetActiveScene().path);
        SessionState.SetBool(CaptureKey + ".hadSave", File.Exists(SaveSystem.FilePath));
        SessionState.SetString(CaptureKey + ".list", string.Join("|", LevelScenes.Where(File.Exists)));
        SessionState.SetInt(CaptureKey + ".done", -1);
        SessionState.SetInt(CaptureKey, 0);
        StartCapture(0);
    }

    [MenuItem("Code Breaker/Polish/3b. Capture Images of the Open Level Only", false, 202)]
    public static void CaptureOpenLevel()
    {
        if (Application.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolders();
        string current = SceneManager.GetActiveScene().path;
        SessionState.SetString(CaptureReturnKey, current);
        SessionState.SetBool(CaptureKey + ".hadSave", File.Exists(SaveSystem.FilePath));
        SessionState.SetString(CaptureKey + ".list", current);
        SessionState.SetInt(CaptureKey + ".done", -1);
        SessionState.SetInt(CaptureKey, 0);
        StartCapture(0);
    }

    static void StartCapture(int index)
    {
        string[] scenes = SessionState.GetString(CaptureKey + ".list", string.Join("|", LevelScenes))
                                      .Split('|').Where(File.Exists).ToArray();
        if (index >= scenes.Length)
        {
            SessionState.SetInt(CaptureKey, -1);
            // playing the levels for the pictures auto-saved; remove that save unless one existed before
            if (!SessionState.GetBool(CaptureKey + ".hadSave", true)) SaveSystem.Delete();
            AssetDatabase.Refresh();
            string back = SessionState.GetString(CaptureReturnKey, LevelScenes[0]);
            if (File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log(Tag + "LEVEL CAPTURES done: " + CaptureDir);
            return;
        }
        EditorSceneManager.OpenScene(scenes[index], OpenSceneMode.Single);
        captureStartTime = -1;
        EditorApplication.isPlaying = true;
    }

    static double captureStartTime = -1;

    [InitializeOnLoadMethod]
    static void HookCapture()
    {
        EditorApplication.update -= CaptureTick;
        EditorApplication.update += CaptureTick;
    }

    // Survives the domain reloads of entering / leaving Play mode: progress lives in SessionState.
    static void CaptureTick()
    {
        int index = SessionState.GetInt(CaptureKey, -1);
        if (index < 0) return;
        string doneKey = CaptureKey + ".done";
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // back in Edit mode after this level was captured -> next level
            if (SessionState.GetInt(doneKey, -1) == index)
            {
                SessionState.SetInt(doneKey, -1);
                SessionState.SetInt(CaptureKey, index + 1);
                StartCapture(index + 1);
            }
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || SessionState.GetInt(doneKey, -1) == index) return;
        if (captureStartTime < 0) { captureStartTime = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - captureStartTime < 2.0) return;   // let the level start up
        captureStartTime = double.MaxValue;   // once

        Scene sc = SceneManager.GetActiveScene();
        try
        {
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.renderMode != RenderMode.WorldSpace) c.enabled = false;   // no HUD in the picture
            Camera cam = FindPlayerCamera(sc);
            bool unfinished = Object.FindObjectsByType<LecternQuestion>(FindObjectsInactive.Include).Length == 0;
            if (cam == null) Debug.LogWarning(Tag + sc.name + ": no camera found");
            else if (unfinished)
            {
                // A level without rooms yet: an elevated view over its actual layout (the player may fall).
                Bounds b = new Bounds();
                bool any = false;
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (r is ParticleSystemRenderer || r.GetComponentInParent<Camera>() != null) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                float size = Mathf.Max(b.extents.x, b.extents.z);
                for (int i = 0; i < 4; i++)
                {
                    float yaw = 35f + 90f * i;
                    Quaternion rot = Quaternion.Euler(28f, yaw, 0f);
                    Vector3 pos = b.center - rot * Vector3.forward * size * 1.15f + Vector3.up * size * 0.1f;
                    Render(cam, pos, rot, CaptureDir + "/" + sc.name + "_" + (char)('a' + i) + ".png");
                }
                Debug.Log(Tag + sc.name + ": no rooms yet, 4 overview shots of the layout (" + b.size.ToString("F0") + ")");
            }
            else
            {
                Vector3 pos = cam.transform.position;
                Quaternion rot = cam.transform.rotation;
                float[] yaws = { 0f, -35f, 35f, 180f };
                for (int i = 0; i < yaws.Length; i++)
                    Render(cam, pos, Quaternion.Euler(0, yaws[i], 0) * Quaternion.Euler(0f, rot.eulerAngles.y, 0f),
                           CaptureDir + "/" + sc.name + "_" + (char)('a' + i) + ".png");
                Debug.Log(Tag + sc.name + ": 4 views from '" + cam.name + "' at " + pos.ToString("F1"));
            }
        }
        catch (Exception e) { Debug.LogError(Tag + "Capture failed in " + sc.name + ": " + e); }
        SessionState.SetInt(CaptureKey + ".done", index);
        EditorApplication.isPlaying = false;
    }

    static float Normalize(float a) => a > 180f ? a - 360f : a;

    static Camera FindPlayerCamera(Scene sc)
    {
        GameObject player = sc.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                              .Select(t => t.gameObject).FirstOrDefault(g => g.CompareTag("Player"));
        Camera cam = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        return cam != null ? cam : AllInScene<Camera>(sc).FirstOrDefault();
    }

    static void Render(Camera cam, Vector3 pos, Quaternion rot, string file)
    {
        Vector3 oldPos = cam.transform.position; Quaternion oldRot = cam.transform.rotation;
        RenderTexture oldTarget = cam.targetTexture;
        bool wasEnabled = cam.enabled, goActive = cam.gameObject.activeSelf;
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        bool hadPP = data != null && data.renderPostProcessing;
        if (data != null) data.renderPostProcessing = Application.isPlaying;   // PP only works in Play mode
        cam.gameObject.SetActive(true); cam.enabled = true;
        cam.transform.SetPositionAndRotation(pos, rot);

        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        try
        {
            cam.targetTexture = rt;
            cam.Render();
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "Camera.Render failed (" + e.Message + "), trying a render request.");
            cam.targetTexture = oldTarget;
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        }
        cam.targetTexture = oldTarget;

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        Color32 first = tex.GetPixel(0, 0);
        bool blank = tex.GetPixels32().All(c => c.r == first.r && c.g == first.g && c.b == first.b);
        if (blank) Debug.LogWarning(Tag + "Capture came out blank: " + file);
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release(); Object.DestroyImmediate(rt);

        if (data != null) data.renderPostProcessing = hadPP;
        cam.transform.SetPositionAndRotation(oldPos, oldRot);
        cam.enabled = wasEnabled; cam.gameObject.SetActive(goActive);
    }

    // ======================================================================================
    //  4. MENUS + LOADING SCREENS
    // ======================================================================================
    [MenuItem("Code Breaker/Polish/4. Build Menus + Loading Screens", false, 203)]
    public static void BuildMenusAndLoading()
    {
        if (Application.isPlaying) { Debug.LogWarning(Tag + "Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder("MENUS + LOADING SCREENS\n");
        EnsureFolders();
        ImportUIArt();
        Fonts f = LoadFonts();
        Backup("menus", new[] { BootScene, MenuScene, "ProjectSettings/EditorBuildSettings.asset" }.Where(File.Exists), log);

        LevelCatalog catalog = BuildCatalog(log);
        BuildLoadingPrefab(f, log);
        BuildBootScene(f, log);
        BuildMainMenuScene(f, log);
        UpdateBuildSettings(log);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        Debug.Log(Tag + log);
        _ = catalog;
    }

    static LevelCatalog BuildCatalog(StringBuilder log)
    {
        string path = ResourcesDir + "/LevelCatalog.asset";
        LevelCatalog cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
        bool isNew = cat == null;
        if (isNew)
        {
            cat = ScriptableObject.CreateInstance<LevelCatalog>();
            AssetDatabase.CreateAsset(cat, path);
            cat.mainMenuScene = "MainMenu";
            cat.levels = new List<LevelCatalog.LevelInfo>
            {
                new LevelCatalog.LevelInfo
                {
                    sceneName = "Level 1", label = "LEVEL 1", title = "The Holding Rooms", playable = true,
                    description = "Three sealed rooms stand between you and the exit. Read the question on each lectern, then choose a door. The wrong one leads into fire.\n<color=#9A2A22>3:00 on the clock.</color>",
                },
                new LevelCatalog.LevelInfo
                {
                    sceneName = "Level 2", label = "LEVEL 2", title = "The Roofless Halls", playable = false,
                    description = "Tall, stained walls and no ceiling above them: a maze of halls under a dead grey sky. Find the right door before time runs out.\n<color=#9A2A22>6:00 on the clock.</color>",
                },
                new LevelCatalog.LevelInfo
                {
                    sceneName = "Level 3", label = "LEVEL 3", title = "The Cell Block", playable = false,
                    description = "A block of sealed cells around a single lectern. Every room looks the same, and only one way leads out.",
                },
            };
            cat.loadingTips = new[]
            {
                "Each room's lectern holds the question. Stand close and press E to read it.",
                "A wrong door costs health, and the clock keeps running.",
                "The answer above each door changes every time a room gives you a new question.",
                "Press Tab during play to change the music and sound effects volume.",
                "Your progress is saved automatically every time you reach a new level.",
            };
        }
        // loading images: Assets/CodeBreaker/UI/Loading/Loading_<scene>.png (made from the captures)
        foreach (LevelCatalog.LevelInfo l in cat.levels)
        {
            Sprite s = LoadingSprite("Loading_" + l.sceneName);
            if (s != null) l.loadingImage = s;
            log.AppendLine("Catalog: " + l.sceneName + " (" + (l.playable ? "playable" : "coming soon") + ")  image: " + (l.loadingImage != null ? l.loadingImage.name : "MISSING"));
        }
        EditorUtility.SetDirty(cat);
        return cat;
    }

    // ---------------------------------------------------------------- loading screen prefab
    static void BuildLoadingPrefab(Fonts f, StringBuilder log)
    {
        GameObject root = new GameObject("LevelLoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas c = root.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 900;   // above every game canvas
        ScalerDefaults(root.GetComponent<CanvasScaler>());
        LevelLoadingScreen ls = root.AddComponent<LevelLoadingScreen>();
        ls.group = root.GetComponent<CanvasGroup>();

        Image black = Img("Black", root.transform, null, Color.black, false); Stretch(black.rectTransform);
        Image bg = Img("Level Image", root.transform, null, Color.white, false); Stretch(bg.rectTransform);
        AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = 16f / 9f;
        ls.background = bg;

        GameObject content = new GameObject("Level Content", typeof(RectTransform));
        content.transform.SetParent(root.transform, false); Stretch((RectTransform)content.transform);
        ls.levelContent = content;
        Image grad = Img("Bottom Shade", content.transform, Spr("CB_GradientBottom"), Color.white, false);
        grad.rectTransform.anchorMin = Vector2.zero; grad.rectTransform.anchorMax = new Vector2(1, 0.62f);
        grad.rectTransform.offsetMin = grad.rectTransform.offsetMax = Vector2.zero;
        Image left = Img("Left Shade", content.transform, Spr("CB_GradientLeft"), new Color(1, 1, 1, 0.75f), false);
        left.rectTransform.anchorMin = Vector2.zero; left.rectTransform.anchorMax = new Vector2(0.62f, 1);
        left.rectTransform.offsetMin = left.rectTransform.offsetMax = Vector2.zero;
        Image vig = Img("Vignette", content.transform, Spr("CB_Vignette"), Color.white, false); Stretch(vig.rectTransform);

        ls.levelLabel = Txt("Level Label", content.transform, "LEVEL 1", f.button, 30, Blood, TextAlignmentOptions.BottomLeft);
        ls.levelLabel.characterSpacing = 18;
        Corner(ls.levelLabel.rectTransform, new Vector2(0, 0), new Vector2(130, 400), new Vector2(900, 44));
        ls.levelTitle = Txt("Level Title", content.transform, "The Holding Rooms", f.title, 104, Bone, TextAlignmentOptions.BottomLeft);
        Corner(ls.levelTitle.rectTransform, new Vector2(0, 0), new Vector2(124, 280), new Vector2(1300, 130));
        Image div = Img("Divider", content.transform, Spr("CB_Divider"), Color.white, false);
        Corner(div.rectTransform, new Vector2(0, 0), new Vector2(60, 232), new Vector2(720, 66));
        ls.levelDescription = Txt("Level Description", content.transform, "", f.body, 28, BoneDim, TextAlignmentOptions.TopLeft);
        ls.levelDescription.lineSpacing = 10;
        Corner(ls.levelDescription.rectTransform, new Vector2(0, 0), new Vector2(130, 120), new Vector2(980, 120));
        ls.tipText = Txt("Tip", content.transform, "", f.body, 22, new Color(0.6f, 0.56f, 0.52f), TextAlignmentOptions.BottomRight);
        ls.tipText.fontStyle = FontStyles.Italic;
        Corner(ls.tipText.rectTransform, new Vector2(1, 0), new Vector2(-130, 120), new Vector2(700, 90));

        GameObject prog = new GameObject("Progress", typeof(RectTransform));
        prog.transform.SetParent(root.transform, false);
        RectTransform pr = (RectTransform)prog.transform;
        pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(0.5f, 0);
        pr.offsetMin = new Vector2(130, 56); pr.offsetMax = new Vector2(-130, 100);
        ls.progressRoot = prog;
        Image track = Img("Track", prog.transform, Spr("CB_ProgressTrack"), Color.white, true);
        track.rectTransform.anchorMin = new Vector2(0, 0); track.rectTransform.anchorMax = new Vector2(1, 0);
        track.rectTransform.pivot = new Vector2(0.5f, 0); track.rectTransform.sizeDelta = new Vector2(0, 16); track.rectTransform.anchoredPosition = Vector2.zero;
        Image fill = Img("Fill", track.transform, Spr("CB_ProgressFill"), Blood, true);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0.001f, 1);
        fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(-3, -3);
        ls.progressFill = fill.rectTransform;
        ls.loadingLabel = Txt("Loading Label", prog.transform, "LOADING", f.button, 22, BoneDim, TextAlignmentOptions.BottomLeft);
        ls.loadingLabel.characterSpacing = 14;
        Corner(ls.loadingLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 24), new Vector2(400, 30));
        ls.progressText = Txt("Percent", prog.transform, "0%", f.button, 22, BoneDim, TextAlignmentOptions.BottomRight);
        Corner(ls.progressText.rectTransform, new Vector2(1, 0), new Vector2(0, 24), new Vector2(200, 30));

        string path = ResourcesDir + "/LevelLoadingScreen.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.AppendLine("Loading screen prefab: " + path);
    }

    // ---------------------------------------------------------------- Boot scene (startup logo)
    static void BuildBootScene(Fonts f, StringBuilder log)
    {
        Scene sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera("Main Camera", Color.black, false);

        GameObject canvasGo = MakeCanvas("Startup Canvas", 0);
        Image black = Img("Black", canvasGo.transform, null, Color.black, false); Stretch(black.rectTransform);
        GameObject logoGo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup));
        logoGo.transform.SetParent(canvasGo.transform, false);
        Sprite logoSpr = Spr("CB_Logo");
        Image logo = Img("Logo Image", logoGo.transform, logoSpr, Color.white, false);
        logo.preserveAspect = true;
        Stretch(logo.rectTransform);
        float aspect = logoSpr != null ? logoSpr.rect.width / logoSpr.rect.height : 2.6f;
        Place((RectTransform)logoGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(460 * aspect, 460));

        StartupScreen ss = new GameObject("Startup Screen").AddComponent<StartupScreen>();
        ss.logoGroup = logoGo.GetComponent<CanvasGroup>();
        ss.logo = (RectTransform)logoGo.transform;
        ss.nextScene = "MainMenu";

        EditorSceneManager.SaveScene(sc, BootScene);
        log.AppendLine("Boot scene (black + logo): " + BootScene);
    }

    // ---------------------------------------------------------------- Main menu scene
    static void BuildMainMenuScene(Fonts f, StringBuilder log)
    {
        Scene sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera("Main Camera", Color.black, true);
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject canvasGo = MakeCanvas("Main Menu Canvas", 0);
        Transform cv = canvasGo.transform;
        MainMenuController mm = canvasGo.AddComponent<MainMenuController>();
        mm.rootGroup = canvasGo.AddComponent<CanvasGroup>();

        // background: a dark view of Level 1
        Image black = Img("Black", cv, null, Color.black, false); Stretch(black.rectTransform);
        Sprite bgSpr = LoadingSprite("MainMenu_Background");
        Image bg = Img("Background", cv, bgSpr, bgSpr != null ? Color.white : Charcoal, false); Stretch(bg.rectTransform);
        if (bgSpr != null)
        {
            AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = bgSpr.rect.width / bgSpr.rect.height;
        }
        Image shade = Img("Shade", cv, null, new Color(0.02f, 0.01f, 0.01f, 0.35f), false); Stretch(shade.rectTransform);
        Image vig = Img("Vignette", cv, Spr("CB_Vignette"), Color.white, false); Stretch(vig.rectTransform);
        foreach (Image i in new[] { black, bg, shade, vig }) i.raycastTarget = false;

        // ---- main screen: logo + the four options
        GameObject main = MakeScreen("Main Screen", cv);
        mm.mainPanel = main;
        Sprite logoSpr = Spr("CB_Logo");
        Image logo = Img("Logo", main.transform, logoSpr, Color.white, false);
        logo.preserveAspect = true; logo.raycastTarget = false;
        float la = logoSpr != null ? logoSpr.rect.width / logoSpr.rect.height : 2.6f;
        Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(330 * la, 330));
        logo.rectTransform.pivot = new Vector2(0.5f, 1f); logo.rectTransform.anchoredPosition = new Vector2(0, -60);

        Image panel = Img("Options Panel", main.transform, Spr("CB_Panel"), Color.white, true);
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(580, 450));
        VerticalLayoutGroup v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(48, 48, 46, 46); v.spacing = 20;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        mm.startButton = MakeButton(panel.transform, "START NEW GAME", f, 480, 74, 32);
        mm.loadButton = MakeButton(panel.transform, "LOAD SAVED GAMES", f, 480, 74, 32);
        mm.settingsButton = MakeButton(panel.transform, "SETTINGS", f, 480, 74, 32);
        mm.quitButton = MakeButton(panel.transform, "QUIT", f, 480, 74, 32);

        TMP_Text hint = Txt("Hint", main.transform, "<color=#8E1B17>ENTER</color>  SELECT        <color=#8E1B17>ESC</color>  BACK", f.button, 20, new Color(0.5f, 0.47f, 0.44f), TextAlignmentOptions.Center);
        hint.characterSpacing = 8;
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 42), new Vector2(900, 30));

        // ---- load saved games
        GameObject load = Dialog("Load Screen", cv, "LOAD SAVED GAMES", f, new Vector2(1060, 640), out Transform lp);
        mm.loadPanel = load;
        Image slot = Img("Save Slot", lp, Spr("CB_Button"), Color.white, true);
        Place(slot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(920, 220));
        mm.saveSlot = slot.gameObject;
        mm.saveSlotTitle = Txt("Slot Title", slot.transform, "AUTO-SAVE", f.button, 30, Bone, TextAlignmentOptions.TopLeft);
        mm.saveSlotTitle.characterSpacing = 4;
        Corner(mm.saveSlotTitle.rectTransform, new Vector2(0, 1), new Vector2(36, -26), new Vector2(840, 44));
        mm.saveSlotDetails = Txt("Slot Details", slot.transform, "", f.body, 24, BoneDim, TextAlignmentOptions.TopLeft);
        mm.saveSlotDetails.lineSpacing = 6;
        Corner(mm.saveSlotDetails.rectTransform, new Vector2(0, 1), new Vector2(36, -80), new Vector2(560, 130));
        mm.continueButton = MakeButton(slot.transform, "CONTINUE", f, 250, 70, 28);
        Corner((RectTransform)mm.continueButton.transform, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(250, 70));

        GameObject none = new GameObject("No Saves", typeof(RectTransform));
        none.transform.SetParent(lp, false);
        Place((RectTransform)none.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(920, 220));
        mm.noSavesMessage = none;
        TMP_Text nt = Txt("Title", none.transform, "No Saved Games Found", f.title, 58, Blood, TextAlignmentOptions.Center);
        Place(nt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 80));
        TMP_Text ns = Txt("Text", none.transform, "Start a new game. Your progress is saved automatically each time you reach a new level.", f.body, 24, BoneDim, TextAlignmentOptions.Center);
        Place(ns.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 80));
        mm.loadBackButton = MakeButton(lp, "BACK", f, 300, 70, 28);
        Place((RectTransform)mm.loadBackButton.transform, new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(300, 70));

        // ---- settings
        GameObject settings = Dialog("Settings Screen", cv, "SETTINGS", f, new Vector2(1100, 860), out Transform sp);
        mm.settingsPanel = settings;
        SettingsPanel spc = settings.AddComponent<SettingsPanel>();
        float y = 205, step = 74;
        spc.masterSlider = SliderRow(sp, "MASTER VOLUME", f, y, out spc.masterValue); y -= step;
        spc.musicSlider = SliderRow(sp, "MUSIC", f, y, out spc.musicValue); y -= step;
        spc.sfxSlider = SliderRow(sp, "SOUND EFFECTS", f, y, out spc.sfxValue); y -= step;
        spc.sensitivitySlider = SliderRow(sp, "MOUSE SENSITIVITY", f, y, out spc.sensitivityValue); y -= step;
        spc.fullscreenToggle = ToggleRow(sp, "FULLSCREEN", f, y); y -= step;
        spc.resolutionDropdown = DropdownRow(sp, "RESOLUTION", f, y); y -= step;
        spc.qualityDropdown = DropdownRow(sp, "GRAPHICS QUALITY", f, y);
        spc.resetButton = MakeButton(sp, "RESET AUDIO + MOUSE", f, 360, 66, 24);
        Place((RectTransform)spc.resetButton.transform, new Vector2(0.5f, 0f), new Vector2(-200, 64), new Vector2(360, 66));
        mm.settingsBackButton = MakeButton(sp, "BACK", f, 300, 66, 28);
        Place((RectTransform)mm.settingsBackButton.transform, new Vector2(0.5f, 0f), new Vector2(200, 64), new Vector2(300, 66));

        // ---- confirmations
        GameObject ng = Dialog("New Game Confirm", cv, "START A NEW GAME?", f, new Vector2(860, 420), out Transform ngp);
        mm.newGameConfirmPanel = ng;
        TMP_Text ngt = Txt("Text", ngp, "Your saved progress will be replaced by the new game.", f.body, 26, BoneDim, TextAlignmentOptions.Center);
        Place(ngt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(720, 80));
        mm.newGameYes = MakeButton(ngp, "START OVER", f, 300, 70, 28);
        Place((RectTransform)mm.newGameYes.transform, new Vector2(0.5f, 0f), new Vector2(-170, 60), new Vector2(300, 70));
        mm.newGameNo = MakeButton(ngp, "GO BACK", f, 300, 70, 28);
        Place((RectTransform)mm.newGameNo.transform, new Vector2(0.5f, 0f), new Vector2(170, 60), new Vector2(300, 70));

        GameObject q = Dialog("Quit Confirm", cv, "LEAVE THE ROOMS?", f, new Vector2(860, 420), out Transform qp);
        mm.quitConfirmPanel = q;
        TMP_Text qt = Txt("Text", qp, "Are you sure you want to quit Code Breaker?", f.body, 26, BoneDim, TextAlignmentOptions.Center);
        Place(qt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(720, 80));
        mm.quitYes = MakeButton(qp, "QUIT", f, 300, 70, 28);
        Place((RectTransform)mm.quitYes.transform, new Vector2(0.5f, 0f), new Vector2(-170, 60), new Vector2(300, 70));
        mm.quitNo = MakeButton(qp, "STAY", f, 300, 70, 28);
        Place((RectTransform)mm.quitNo.transform, new Vector2(0.5f, 0f), new Vector2(170, 60), new Vector2(300, 70));

        // ---- music
        AudioClip amb = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Horror_Ambience_Loop.wav");
        AudioSource music = new GameObject("Menu Music").AddComponent<AudioSource>();
        music.clip = amb; music.loop = true; music.playOnAwake = false; music.spatialBlend = 0f;
        mm.menuMusic = music;

        foreach (GameObject s in new[] { load, settings, ng, q }) s.SetActive(false);
        EditorSceneManager.SaveScene(sc, MenuScene);
        log.AppendLine("Main menu scene: " + MenuScene + (bgSpr == null ? "  (no background image yet: run step 3 and add MainMenu_Background.png)" : ""));
    }

    static void UpdateBuildSettings(StringBuilder log)
    {
        var list = EditorBuildSettings.scenes.Where(s => s.path != BootScene && s.path != MenuScene).ToList();
        list.Insert(0, new EditorBuildSettingsScene(MenuScene, true));
        list.Insert(0, new EditorBuildSettingsScene(BootScene, true));
        foreach (string l in LevelScenes)
            if (File.Exists(l) && list.All(s => s.path != l)) list.Add(new EditorBuildSettingsScene(l, true));
        EditorBuildSettings.scenes = list.ToArray();
        log.AppendLine("Build Settings: " + string.Join("  ->  ", list.Select(s => Path.GetFileNameWithoutExtension(s.path))));
    }

    // ---- testing helpers (Play mode)
    [MenuItem("Code Breaker/Polish/Test: Complete Current Level (Play mode)", false, 225)]
    static void TestCompleteLevel()
    {
        GameFlowManager gm = GameFlowManager.Instance;
        if (gm == null) { Debug.LogWarning(Tag + "No game session running."); return; }
        RoomManager rm = RoomManager.Instance;
        gm.LevelCompleted(rm != null ? rm.levelDisplayName : SceneManager.GetActiveScene().name, rm != null ? rm.rooms.Count : 0);
    }
    [MenuItem("Code Breaker/Polish/Test: Complete Current Level (Play mode)", true)] static bool VTestComplete() => Application.isPlaying;

    [MenuItem("Code Breaker/Polish/Delete Save File (testing)", false, 230)]
    static void DeleteSave()
    {
        SaveSystem.Delete();
        Debug.Log(Tag + "Save file deleted: " + SaveSystem.FilePath);
    }

    [MenuItem("Code Breaker/Polish/Open Boot Scene", false, 231)]
    static void OpenBoot()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() && File.Exists(BootScene))
            EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Single);
    }

    // ======================================================================================
    //  UI BUILDING BLOCKS
    // ======================================================================================
    class Fonts { public TMP_FontAsset title, button, body; }

    static Fonts LoadFonts()
    {
        return new Fonts
        {
            title = OrDefault(FontAsset("Creepster-Regular", 72, 8)),
            button = OrDefault(FontAsset("Oswald-Medium", 64, 7)),
            body = OrDefault(FontAsset("SpecialElite-Regular", 64, 7)),
        };
    }

    static TMP_FontAsset OrDefault(TMP_FontAsset f) => f != null ? f : TMP_Settings.defaultFontAsset;

    // TextMesh Pro font asset from a TTF in UI/Fonts (created once, then reused).
    static TMP_FontAsset FontAsset(string ttfName, int pointSize, int padding)
    {
        string assetPath = FontDir + "/" + ttfName + " SDF.asset";
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa != null) return fa;
        fa = null;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + ttfName + ".ttf");
        if (font == null) { Debug.LogWarning(Tag + "Font missing: " + ttfName); return null; }
        try
        {
            fa = TMP_FontAsset.CreateFontAsset(font, pointSize, padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fa.name = ttfName + " SDF";
            fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·—’", out string _);
            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            {
                fa.atlasTextures[0].name = ttfName + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            fa.material.name = ttfName + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning(Tag + "Could not create the font asset for " + ttfName + ": " + e.Message + " (using the default TMP font)");
            return null;
        }
    }

    static void ImportUIArt()
    {
        var borders = new Dictionary<string, Vector4>
        {
            { "CB_Panel", new Vector4(44, 44, 44, 44) },
            { "CB_Button", new Vector4(22, 22, 22, 22) },
            { "CB_ButtonGlow", new Vector4(60, 40, 60, 40) },
            { "CB_ProgressTrack", new Vector4(10, 10, 10, 10) },
            { "CB_ProgressFill", new Vector4(10, 10, 10, 10) },
            { "CB_Checkbox", new Vector4(8, 8, 8, 8) },
            { "CB_Accent", new Vector4(0, 14, 0, 14) },
        };
        foreach (string dir in new[] { SpriteDir, LoadingDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                                             .Where(x => x.EndsWith(".png") || x.EndsWith(".jpg")))
            {
                string p = file.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
                string n = Path.GetFileNameWithoutExtension(p);
                Vector4 b = borders.TryGetValue(n, out Vector4 bb) ? bb : Vector4.zero;
                if (ti.textureType == TextureImporterType.Sprite && ti.spriteBorder == b && !ti.mipmapEnabled) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spriteBorder = b;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
        }
    }

    static Sprite LoadingSprite(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(LoadingDir + "/" + name + ".jpg");
        if (s == null) s = AssetDatabase.LoadAssetAtPath<Sprite>(LoadingDir + "/" + name + ".png");
        return s;
    }

    static Sprite Spr(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/" + name + ".png");
        if (s == null) Debug.LogWarning(Tag + "Sprite missing: " + name);
        return s;
    }

    static void ScalerDefaults(CanvasScaler cs)
    {
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
    }

    static GameObject MakeCanvas(string name, int order)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        ScalerDefaults(go.GetComponent<CanvasScaler>());
        return go;
    }

    static void MakeCamera(string name, Color bg, bool listener)
    {
        GameObject go = new GameObject(name, typeof(Camera));
        go.tag = "MainCamera";
        Camera c = go.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = bg;
        c.cullingMask = 0;   // only UI is shown
        if (listener) go.AddComponent<AudioListener>();
    }

    static GameObject MakeScreen(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go;
    }

    // A full-screen dim layer + framed panel with a title and blood divider. 'body' is the panel.
    static GameObject Dialog(string name, Transform parent, string title, Fonts f, Vector2 size, out Transform body)
    {
        GameObject screen = MakeScreen(name, parent);
        Image dim = Img("Dim", screen.transform, null, new Color(0f, 0f, 0f, 0.55f), false); Stretch(dim.rectTransform);
        Image panel = Img("Panel", screen.transform, Spr("CB_Panel"), Color.white, true);
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), size);
        TMP_Text t = Txt("Title", panel.transform, title, f.title, 66, Bone, TextAlignmentOptions.Center);
        Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(size.x - 80, 90));
        Image div = Img("Divider", panel.transform, Spr("CB_Divider"), Color.white, false);
        Place(div.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -136), new Vector2(560, 52));
        div.raycastTarget = false;
        body = panel.transform;
        return screen;
    }

    static Button MakeButton(Transform parent, string text, Fonts f, float w, float h, int fontSize)
    {
        GameObject go = new GameObject(Title(text) + " Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(w, h);
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h; le.minHeight = h;
        Image img = go.GetComponent<Image>();
        img.sprite = Spr("CB_Button"); img.type = Image.Type.Sliced; img.color = Color.white;
        Button b = go.GetComponent<Button>();
        b.transition = Selectable.Transition.None;   // UIButtonFX does the hover / pressed look

        Image glow = Img("Glow", go.transform, Spr("CB_ButtonGlow"), new Color(0.6f, 0.05f, 0.03f, 0f), true);
        Stretch(glow.rectTransform); glow.rectTransform.offsetMin = new Vector2(-14, -10); glow.rectTransform.offsetMax = new Vector2(14, 10);
        glow.raycastTarget = false;
        Image accent = Img("Accent", go.transform, Spr("CB_Accent"), new Color(0.75f, 0.08f, 0.05f, 0f), true);
        accent.rectTransform.anchorMin = new Vector2(0, 0); accent.rectTransform.anchorMax = new Vector2(0, 1);
        accent.rectTransform.pivot = new Vector2(0, 0.5f);
        accent.rectTransform.offsetMin = new Vector2(4, 8); accent.rectTransform.offsetMax = new Vector2(16, -8);
        accent.raycastTarget = false;
        TMP_Text label = Txt("Label", go.transform, text, f.button, fontSize, Bone, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        label.characterSpacing = 7;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = fontSize;
        label.margin = new Vector4(18, 0, 18, 0);

        UIButtonFX fx = go.AddComponent<UIButtonFX>();
        fx.glow = glow; fx.accent = accent; fx.label = label;
        return b;
    }

    static TMP_Text RowLabel(Transform parent, string text, Fonts f, float y)
    {
        TMP_Text t = Txt(Title(text) + " Label", parent, text, f.button, 26, BoneDim, TextAlignmentOptions.MidlineLeft);
        t.characterSpacing = 6;
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-250, y), new Vector2(420, 56));
        return t;
    }

    static Slider SliderRow(Transform parent, string text, Fonts f, float y, out TMP_Text value)
    {
        RowLabel(parent, text, f, y);
        GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = Title(text) + " Slider";
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(170, y), new Vector2(380, 30));
        Slider s = go.GetComponent<Slider>();
        s.minValue = 0; s.maxValue = 1;
        Image bgI = go.transform.Find("Background").GetComponent<Image>();
        bgI.sprite = Spr("CB_ProgressTrack"); bgI.type = Image.Type.Sliced; bgI.color = Color.white;
        RectTransform bgR = bgI.rectTransform; bgR.anchorMin = new Vector2(0, 0.3f); bgR.anchorMax = new Vector2(1, 0.7f);
        Image fill = go.transform.Find("Fill Area/Fill").GetComponent<Image>();
        fill.sprite = Spr("CB_ProgressFill"); fill.type = Image.Type.Sliced; fill.color = Blood;
        RectTransform fa = (RectTransform)go.transform.Find("Fill Area");
        fa.anchorMin = new Vector2(0, 0.3f); fa.anchorMax = new Vector2(1, 0.7f);
        Image handle = go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>();
        handle.sprite = Spr("CB_SliderHandle"); handle.color = Color.white;
        handle.rectTransform.sizeDelta = new Vector2(30, 0);
        s.transition = Selectable.Transition.None;
        Image glow = Img("Glow", go.transform, Spr("CB_ButtonGlow"), new Color(0.6f, 0.05f, 0.03f, 0f), true);
        Stretch(glow.rectTransform); glow.rectTransform.offsetMin = new Vector2(-16, -14); glow.rectTransform.offsetMax = new Vector2(16, 14);
        glow.raycastTarget = false; glow.transform.SetAsFirstSibling();
        UIButtonFX fx = go.AddComponent<UIButtonFX>();
        fx.hoverScale = 1.0f; fx.pressedScale = 1.0f;
        fx.glow = glow; fx.selectOnHover = true;

        value = Txt(Title(text) + " Value", parent, "100%", f.button, 26, Bone, TextAlignmentOptions.MidlineRight);
        Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(430, y), new Vector2(110, 56));
        return s;
    }

    static Toggle ToggleRow(Transform parent, string text, Fonts f, float y)
    {
        RowLabel(parent, text, f, y);
        GameObject go = DefaultControls.CreateToggle(new DefaultControls.Resources());
        go.name = Title(text) + " Toggle";
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(170, y), new Vector2(380, 48));
        Toggle t = go.GetComponent<Toggle>();
        Image box = go.transform.Find("Background").GetComponent<Image>();
        box.sprite = Spr("CB_Checkbox"); box.type = Image.Type.Sliced; box.color = Color.white;
        RectTransform br = box.rectTransform; br.anchorMin = br.anchorMax = new Vector2(0, 0.5f); br.pivot = new Vector2(0, 0.5f);
        br.anchoredPosition = Vector2.zero; br.sizeDelta = new Vector2(44, 44);
        Image mark = box.transform.Find("Checkmark").GetComponent<Image>();
        mark.sprite = Spr("CB_Checkmark"); mark.color = Color.white;
        Stretch(mark.rectTransform);
        Text legacy = go.GetComponentInChildren<Text>(true);
        if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
        TMP_Text lab = Txt("Label", go.transform, "ON  /  OFF", f.body, 22, new Color(0.55f, 0.52f, 0.49f), TextAlignmentOptions.MidlineLeft);
        Place(lab.rectTransform, new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(300, 44));
        lab.rectTransform.pivot = new Vector2(0, 0.5f); lab.rectTransform.anchoredPosition = new Vector2(64, 0);
        lab.text = "Full screen window";
        t.transition = Selectable.Transition.None;
        UIButtonFX fx = go.AddComponent<UIButtonFX>();
        fx.hoverScale = 1f; fx.pressedScale = 0.98f; fx.label = lab;
        fx.labelNormal = new Color(0.62f, 0.58f, 0.55f);
        return t;
    }

    static TMP_Dropdown DropdownRow(Transform parent, string text, Fonts f, float y)
    {
        RowLabel(parent, text, f, y);
        var res = new TMP_DefaultControls.Resources
        {
            standard = Spr("CB_Button"),
            background = Spr("CB_Button"),
            inputField = Spr("CB_Button"),
            knob = Spr("CB_SliderHandle"),
            checkmark = Spr("CB_Checkmark"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
        };
        GameObject go = TMP_DefaultControls.CreateDropdown(res);
        go.name = Title(text) + " Dropdown";
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(225, y), new Vector2(490, 52));
        TMP_Dropdown dd = go.GetComponent<TMP_Dropdown>();
        foreach (Image i in go.GetComponentsInChildren<Image>(true))
        {
            if (i.sprite == res.standard || i.sprite == res.background) { i.type = Image.Type.Sliced; i.color = Color.white; }
        }
        Image arrow = go.transform.Find("Arrow")?.GetComponent<Image>();
        if (arrow != null) arrow.color = Blood;
        Image template = go.transform.Find("Template")?.GetComponent<Image>();
        if (template != null) { template.sprite = Spr("CB_Panel"); template.type = Image.Type.Sliced; template.color = new Color(1, 1, 1, 0.98f); template.pixelsPerUnitMultiplier = 2f; }
        Toggle item = go.GetComponentInChildren<Toggle>(true);
        if (item != null)
        {
            ((RectTransform)item.transform).sizeDelta = new Vector2(0, 40);
            Image ib = item.targetGraphic as Image;
            if (ib != null) { ib.sprite = null; ib.color = new Color(0, 0, 0, 0); }
            ColorBlock cb = item.colors;
            cb.normalColor = new Color(1, 1, 1, 0); cb.highlightedColor = new Color(0.55f, 0.06f, 0.04f, 0.85f);
            cb.selectedColor = new Color(0.45f, 0.05f, 0.03f, 0.7f); cb.pressedColor = Blood;
            item.colors = cb;
            if (ib != null) ib.color = Color.white;
            if (ib != null) ib.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }
        RectTransform content = go.transform.Find("Template/Viewport/Content") as RectTransform;
        if (content != null) content.sizeDelta = new Vector2(0, 40);
        RectTransform tmpl = go.transform.Find("Template") as RectTransform;
        if (tmpl != null) tmpl.sizeDelta = new Vector2(0, 260);
        foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (f.body != null) t.font = f.body;
            t.fontSize = 22; t.color = Bone;
        }
        dd.transition = Selectable.Transition.None;
        UIButtonFX fx = go.AddComponent<UIButtonFX>();
        fx.hoverScale = 1.015f; fx.label = dd.captionText;
        Image glow = Img("Glow", go.transform, Spr("CB_ButtonGlow"), new Color(0.6f, 0.05f, 0.03f, 0f), true);
        Stretch(glow.rectTransform); glow.rectTransform.offsetMin = new Vector2(-12, -8); glow.rectTransform.offsetMax = new Vector2(12, 8);
        glow.raycastTarget = false; glow.transform.SetAsFirstSibling();
        fx.glow = glow;
        return dd;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color, bool sliced)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image i = go.GetComponent<Image>();
        i.sprite = sprite;
        i.color = color;
        if (sliced && sprite != null && sprite.border != Vector4.zero) i.type = Image.Type.Sliced;
        return i;
    }

    static TMP_Text Txt(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // Anchored to a corner, pivot on the same corner (keeps a fixed margin from that screen edge).
    static void Corner(RectTransform rt, Vector2 corner, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = corner;
        rt.pivot = corner;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static string Title(string s) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    // ======================================================================================
    //  HELPERS
    // ======================================================================================
    static IEnumerable<T> AllInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    static void EnsureFolders()
    {
        foreach (string d in new[] { SpriteDir, FontDir, LoadingDir, CaptureDir, EnvTexDir, EnvMatDir, SettingsDir, ResourcesDir })
        {
            if (AssetDatabase.IsValidFolder(d)) continue;
            string parent = Path.GetDirectoryName(d).Replace('\\', '/');
            string leaf = Path.GetFileName(d);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                string gp = Path.GetDirectoryName(parent).Replace('\\', '/');
                AssetDatabase.CreateFolder(gp, Path.GetFileName(parent));
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    // Copies files (and their .meta) to <project>/Backups (Code Breaker Polish)/<time> <step>/ before changing them.
    static void Backup(string step, IEnumerable<string> assetPaths, StringBuilder log)
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        string dir = Path.Combine(project, "Backups (Code Breaker Polish)", DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + " " + step);
        int n = 0;
        foreach (string p in assetPaths)
        {
            string src = Path.Combine(project, p);
            if (!File.Exists(src)) continue;
            string dst = Path.Combine(dir, p);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
            if (File.Exists(src + ".meta")) File.Copy(src + ".meta", dst + ".meta", true);
            n++;
        }
        if (n > 0) log.AppendLine("Backup of " + n + " file(s): " + dir);
    }
}
