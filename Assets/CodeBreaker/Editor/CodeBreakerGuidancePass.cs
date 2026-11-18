using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeBreaker.Level2;
using CodeBreaker.Level3;

// Code Breaker > Guidance Pass: the scene side of the "clearer clues" update for Level 2 and Level 3.
// (Objectives, room briefings and hints are in code: L2Game, L2LevelSession, L3Manager.)
// It only changes texts / labels and adds small text signs next to existing puzzle objects; nothing is
// removed and no puzzle answer changes. Level 1 is never opened. Writes Logs/CB_GuidancePass.txt.
public static class CodeBreakerGuidancePass
{
    public const string TexDir = "Assets/CodeBreaker/Level2/Textures/Guidance";
    const string MatDir = "Assets/CodeBreaker/Level2/Materials/Guidance";

    public const string FridgeNoteText =
        "Grandpa's kitchen rules:\n\nEvery jar is labelled like my programs:\n      name = value\n\n" +
        "The name on the left holds the value on the right.\nSolve the one you know first.\n\n- G.";

    static StringBuilder log;

    [MenuItem("Code Breaker/Guidance Pass/1. Apply Level 2 Clues (kitchen variables, corner doors)", false, 640)]
    static void Level2Menu()
    {
        log = new StringBuilder("GUIDANCE PASS  Level 2  " + System.DateTime.Now + "\n");
        if (!Open("Level 2")) { Write(); return; }
        Step("kitchen ingredients -> variables", Ingredients);
        Step("fridge note", FridgeNote);
        Step("corner cabinet doors", CornerDoors);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Guidance Pass/2. Apply Level 3 Clues", false, 641)]
    static void Level3Menu()
    {
        log = new StringBuilder("GUIDANCE PASS  Level 3  " + System.DateTime.Now + "\n");
        if (!Open("Level 3")) { Write(); return; }
        Step("fuse box rule sign", FuseBox);
        Step("PIN note: what cam means", PinNote);
        Step("Monitor 2: where the racks are", Monitor2);
        Step("emergency panel: fan before reboot", LeverPanel);
        Step("laptop sticky note: locker numbers", LaptopNote);
        Step("name tag pegboard layout", Pegboard);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Guidance Pass/3. Apply Ramos Locker Clues (Level 3)", false, 642)]
    static void RamosMenu()
    {
        log = new StringBuilder("GUIDANCE PASS  Ramos lockers  " + System.DateTime.Now + "\n");
        if (!Open("Level 3")) { Write(); return; }
        Step("locker cards: name = value", RamosCards);
        Step("nameplate above the lockers", RamosPlaque);
        Step("laptop: explained code + found values", RamosLaptop);
        Step("sticky note body", RamosSticky);
        Step("Level 3 clock: 20:00", Level3Clock);
        Save();
        Write();
    }

    [MenuItem("Code Breaker/Guidance Pass/1. Apply Level 2 Clues (kitchen variables, corner doors)", true)]
    [MenuItem("Code Breaker/Guidance Pass/2. Apply Level 3 Clues", true)]
    [MenuItem("Code Breaker/Guidance Pass/3. Apply Ramos Locker Clues (Level 3)", true)]
    static bool NotPlaying() => !Application.isPlaying;

    // Play mode check for the objective box: shows the longest objective + hint for a moment.
    [MenuItem("Code Breaker/Guidance Pass/Test: Long Objective (in Play mode)", false, 660)]
    static void LongObjective()
    {
        var g = L2Game.Instance;
        if (!Application.isPlaying || g == null || g.ui == null) { Debug.LogWarning("[Guidance Pass] enter Play mode in Level 2 or 3 first"); return; }
        g.ui.SetObjective("Swap the fuses the way Ramos's log says, then press TEST.",
            "Ramos's log page (inside the red locker) is a 3-line swap. You can carry one fuse at a time; temp is the spare socket.\nA second hint line to check the box keeps growing.");
    }

    // ------------------------------------------------------------------ helpers
    static void Step(string name, System.Action a)
    {
        log.AppendLine("---- " + name);
        try { a(); } catch (System.Exception e) { log.AppendLine("ERROR in " + name + ": " + e); }
    }

    static bool Open(string scene)
    {
        if (SceneManager.GetActiveScene().name == scene) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        string path = EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == scene);
        if (path == null) { log.AppendLine("ERROR scene not found " + scene); return false; }
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    static void Save()
    {
        Scene s = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(s);
        log.AppendLine(EditorSceneManager.SaveScene(s) ? "saved " + s.path : "ERROR could not save " + s.path);
        AssetDatabase.SaveAssets();
    }

    static void Write()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_GuidancePass.txt", log.ToString());
        Debug.Log("[Guidance Pass] done -> Logs/CB_GuidancePass.txt");
    }

    static Transform Find(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // A copy of 'source' showing 'tex' (saved as its own asset, so the original material is untouched).
    static Material TexturedCopy(Material source, Texture2D tex, string name)
    {
        EnsureFolder(MatDir);
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        else if (source != null && m.shader != source.shader) m.shader = source.shader;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        m.name = name;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D Tex(string file)
    {
        string p = TexDir + "/" + file;
        AssetDatabase.ImportAsset(p);
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        if (t == null) log.AppendLine("  ERROR texture missing " + p);
        return t;
    }

    static void SetString(Component c, string field, string value)
    {
        var so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.String) { log.AppendLine("  (no field " + field + " on " + c.GetType().Name + ")"); return; }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(c);
    }

    static string GetString(Component c, string field)
    {
        var p = new SerializedObject(c).FindProperty(field);
        return p != null && p.propertyType == SerializedPropertyType.String ? p.stringValue : null;
    }

    // A new text sign made from an existing TextMeshPro (same font, material and facing).
    static TextMeshPro Sign(Transform copyFrom, Transform parent, string name, string text, Vector3 localPos, Vector2 box, float maxSize, Color color)
    {
        Transform old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject go = Object.Instantiate(copyFrom.gameObject, parent);
        go.name = name;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (!(mb is TMP_Text)) Object.DestroyImmediate(mb);
        foreach (Collider c in go.GetComponents<Collider>()) Object.DestroyImmediate(c);
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        go.transform.localPosition = localPos;
        go.transform.localRotation = copyFrom.localRotation;
        var t = go.GetComponent<TextMeshPro>();
        t.text = text;
        t.rectTransform.sizeDelta = box;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.2f;
        t.fontSizeMax = maxSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    // ================================================================== Level 2
    // Option C: every ingredient's underside now holds a line of code (a variable) instead of a binary
    // number. The labels the game shows come from L2Ingredient.VariableLine; here the serialized label
    // and the printed underside texture are updated to match.
    static void Ingredients()
    {
        foreach (L2Ingredient ing in Object.FindObjectsByType<L2Ingredient>(FindObjectsInactive.Include))
        {
            string line = L2Ingredient.VariableLine(ing.ingredient);
            if (line == null) { log.AppendLine("  skipped " + ing.name); continue; }
            Undo.RecordObject(ing, "label");
            ing.label = line;
            EditorUtility.SetDirty(ing);
            Transform bottom = ing.transform.Find("BottomNumber");
            Texture2D tex = Tex("Var_" + ing.ingredient + ".png");
            if (bottom != null && tex != null)
            {
                var r = bottom.GetComponent<Renderer>();
                r.sharedMaterial = TexturedCopy(r.sharedMaterial, tex, "L2G_Var_" + ing.ingredient);
                EditorUtility.SetDirty(r);
                log.AppendLine("  " + ing.ingredient + ": underside now reads \"" + line + "\"");
            }
            else log.AppendLine("  " + ing.ingredient + ": label set to \"" + line + "\" (no BottomNumber quad found)");
        }
    }

    // The fridge note now explains how a variable works, in Grandpa's words.
    static void FridgeNote()
    {
        Transform fn = Find("Puzzle_Kitchen_FridgeNote");
        if (fn == null) { log.AppendLine("  no fridge note"); return; }
        var ex = fn.GetComponent<L2Examine>();
        Texture2D tex = Tex("FridgeNote_Variables.png");
        if (ex != null)
        {
            Undo.RecordObject(ex, "fridge note");
            ex.title = "Note on the Fridge";
            ex.body = FridgeNoteText;
            if (tex != null) ex.image = tex;
            EditorUtility.SetDirty(ex);
        }
        Renderer r = fn.GetComponentInChildren<Renderer>();
        if (r != null && tex != null)
        {
            r.sharedMaterial = TexturedCopy(r.sharedMaterial, tex, "L2G_FridgeNote_Variables");
            EditorUtility.SetDirty(r);
        }
        log.AppendLine("  fridge note text + picture updated (" + (r != null ? r.name : "no renderer") + ")");
    }

    // The two side doors at the kitchen corners (upper U6 left, base B5 left) reached so close to the
    // corner cabinet's door that they hit it as soon as they started to swing. Their free edge is
    // trimmed a little so they clear it (the hinge side stays where it is).
    static void CornerDoors()
    {
        foreach (var (n, target) in new[] { ("KIT_UpperCabinet_U6_Door_Left", 2.166f), ("KIT_BaseCabinet_B5_Door_Left", 1.97f) })
        {
            Transform t = Find(n);
            if (t == null) { log.AppendLine("  missing " + n); continue; }
            Bounds b = t.GetComponent<Renderer>().bounds;
            int w = b.size.x > b.size.z ? 0 : 2;
            float width = b.size[w];
            if (Mathf.Abs(width - target) < 0.01f) { log.AppendLine("  " + n + " already " + width.ToString("F3")); continue; }
            Vector3 worldW = Vector3.zero; worldW[w] = 1f;
            int li = 0; float best = 0f;
            for (int i = 0; i < 3; i++) { Vector3 ax = Vector3.zero; ax[i] = 1f; float d = Mathf.Abs(Vector3.Dot(t.rotation * ax, worldW)); if (d > best) { best = d; li = i; } }
            Undo.RecordObject(t, "trim door");
            Vector3 s = t.localScale; s[li] *= target / width; t.localScale = s;
            Physics.SyncTransforms();
            Bounds nb = t.GetComponent<Renderer>().bounds;
            log.AppendLine("  " + n + " width " + width.ToString("F3") + " -> " + nb.size[w].ToString("F3") + "  (hinge side " + (Mathf.Abs(nb.min[w] - b.min[w]) < 0.01f ? "min" : "max") + " kept: " +
                           V(b.min) + "-" + V(b.max) + " -> " + V(nb.min) + "-" + V(nb.max) + ")");
        }
    }

    static string V(Vector3 v) => v.ToString("F3");

    // ================================================================== Level 3
    static void FuseBox()
    {
        Transform fb = Find("FuseBox");
        Transform title = fb != null ? fb.Find("Title") : null;
        if (title == null) { log.AppendLine("  no fuse box title"); return; }
        Sign(title, fb, "RuleSign", "You can carry one fuse at a time.\n<size=85%>'temp' is an empty spare socket.</size>",
             new Vector3(-1.1f, 3.49f, -0.82f), new Vector2(3.0f, 0.34f), 1.3f, new Color(0.95f, 0.9f, 0.75f));
        log.AppendLine("  sign added under the display");
    }

    static void PinNote()
    {
        const string extra = "cam = the camera that saw the alarm";
        Transform pinText = Find("PinNoteText");
        if (pinText != null)
        {
            var t = pinText.GetComponent<TMP_Text>();
            if (!t.text.Contains("cam =")) t.text = "PIN = cam * 10 + (17 % 5)\n<size=60%>" + extra + "</size>";
            EditorUtility.SetDirty(t);
        }
        Transform pin = Find("PinNote");
        var ex = pin != null ? pin.GetComponent<L2Examine>() : null;
        if (ex != null) { Undo.RecordObject(ex, "pin"); ex.body = "PIN = cam * 10 + (17 % 5)\n\n" + extra + "."; EditorUtility.SetDirty(ex); }
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (!(mb is L3Keypad) || GetString(mb, "station") != "s3") continue;
            string code = GetString(mb, "code");
            if (code != null && !code.Contains("cam =")) SetString(mb, "code", code.Replace("PIN = cam * 10 + (17 % 5)", "PIN = cam * 10 + (17 % 5)   // " + extra));
        }
        log.AppendLine("  PIN note / safe keypad now say what cam is" + (pinText == null ? " (no PinNoteText!)" : ""));
    }

    static void Monitor2()
    {
        const string extra = "// The racks stand in the main server hall.";
        Transform mon = Find("Monitor2");
        if (mon == null) { log.AppendLine("  no Monitor2"); return; }
        var t = mon.Find("Text") != null ? mon.Find("Text").GetComponent<TMP_Text>() : null;
        if (t != null && !t.text.Contains("main server hall")) { t.text = t.text.Replace("// Enter online", "// Enter online\n" + extra); EditorUtility.SetDirty(t); }
        var ex = mon.GetComponent<L2Examine>();
        if (ex != null && !ex.body.Contains("main server hall")) { Undo.RecordObject(ex, "mon2"); ex.body += "\n" + extra; EditorUtility.SetDirty(ex); }
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (!(mb is L3Keypad) || GetString(mb, "station") != "c2") continue;
            string code = GetString(mb, "code");
            if (code != null && !code.Contains("main server hall")) SetString(mb, "code", code + "\n" + extra);
        }
        // the text shown after solving keeps the same extra line
        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (mb == null || mb.GetType().Name != "L3SetText") continue;
            string txt = GetString(mb, "text");
            if (txt != null && txt.StartsWith("MONITOR 2") && !txt.Contains("main server hall")) SetString(mb, "text", txt.Replace("// Enter online", "// Enter online\n" + extra));
        }
        log.AppendLine("  Monitor 2, its examine text and the console keypad mention the main hall");
    }

    static void LeverPanel()
    {
        Transform ep = Find("EmergencyPanel");
        Transform title = ep != null ? ep.Find("PanelText") : null;
        if (title == null) { log.AppendLine("  no emergency panel"); return; }
        Sign(title, ep, "FanReminder", "SET THE COOLING FAN BEFORE THE REBOOT", new Vector3(0f, 3.55f, -0.72f), new Vector2(5.0f, 0.45f), 1.6f, new Color(1f, 0.85f, 0.75f));
        log.AppendLine("  reminder added under the levers");
    }

    // The pegboard title stuck out over the top edge of the board, and the tags hung well below their
    // hooks. The title moves down inside the board and each tag hangs right under its hook.
    static void Pegboard()
    {
        Transform peg = Find("Pegboard");
        if (peg == null) { log.AppendLine("  no pegboard"); return; }
        Transform title = peg.Find("Title");
        if (title != null)
        {
            Undo.RecordObject(title, "title");
            title.localPosition = new Vector3(title.localPosition.x, 7.55f, title.localPosition.z);
            var t = title.GetComponent<TMP_Text>();
            if (t != null) { Undo.RecordObject(t.rectTransform, "title"); t.rectTransform.sizeDelta = new Vector2(5.6f, 1.2f); EditorUtility.SetDirty(t); }
        }
        float hookY = 6.75f;
        foreach (Transform c in peg)
            if (c.name == "Hook") { hookY = c.localPosition.y; break; }
        int n = 0;
        foreach (Transform c in peg)
        {
            if (!c.name.StartsWith("Tag_")) continue;
            Undo.RecordObject(c, "tag");
            // plate is 0.55 tall, centred on the tag: its top edge sits just under the hook
            c.position = peg.TransformPoint(new Vector3(peg.InverseTransformPoint(c.position).x, hookY - 0.28f, peg.InverseTransformPoint(c.position).z));
            // keep "name = value" on one line (fanSpeed = 6 used to wrap)
            Transform tx = c.Find("Text");
            var tt = tx != null ? tx.GetComponent<TMP_Text>() : null;
            if (tt != null)
            {
                Undo.RecordObject(tt, "tag text");
                tt.textWrappingMode = TextWrappingModes.NoWrap;
                tt.enableAutoSizing = true;
                tt.fontSizeMin = 0.2f;
                EditorUtility.SetDirty(tt);
            }
            n++;
        }
        log.AppendLine("  title moved inside the board; " + n + " tags now hang from their hooks (hook y " + hookY.ToString("F2") + ")");
    }

    static void LaptopNote()
    {
        Transform n = Find("StickyNote_Laptop");
        if (n == null) { log.AppendLine("  no laptop sticky note"); return; }
        const string text = "Ramos kept one number in each locker.\nCopying a value doesn't link the boxes.";
        var t = n.Find("Text") != null ? n.Find("Text").GetComponent<TMP_Text>() : null;
        if (t != null) { t.text = text; EditorUtility.SetDirty(t); }
        var ex = n.GetComponent<L2Examine>();
        if (ex != null) { Undo.RecordObject(ex, "sticky"); ex.body = text; EditorUtility.SetDirty(ex); }
        log.AppendLine("  sticky note now points at the lockers");
    }
    // ================================================================== Ramos's lockers (Level 3, job 1)
    // The puzzle and its answer stay the same (locker2 = locker1; locker1 = locker3; -> 8). What changes:
    // each card reads "lockerN = value", a nameplate ties the lockers to Ramos, and his laptop explains
    // how to read the code and lists the locker values the player has found (filled in by L3Manager).
    public const string RamosLaptopCode =
        "<color=#9A918A>// RAMOS - job 1 of 3. Finish it for me.\n" +
        "// My three lockers are my variables:\n" +
        "//    name  = the label on the door\n" +
        "//    value = the number on the card inside\n" +
        "// '=' puts a COPY of the value on the right\n" +
        "//    into the variable on the left.</color>\n\n" +
        "locker2 = locker1;\n" +
        "locker1 = locker3;\n\n" +
        "<color=#9A918A>// Run the lines in order, top to bottom.\n" +
        "// What number is in locker2 now?</color>\n\n" +
        "<color=#E8C547>Lockers you have opened:</color>\n{lockers}";

    public const string RamosLaptopHint = "Write all three locker values down. After each line, change ONLY the locker on the left of '='.";

    static Transform RamosLocker(int i)
    {
        foreach (L2Openable o in Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (o.label == "locker" + i) return o.transform.parent;   // DoorPivot -> LockerN group
        return null;
    }

    static void RamosCards()
    {
        for (int i = 1; i <= 3; i++)
        {
            Transform g = RamosLocker(i);
            Transform num = g != null ? g.Find("Inside/Number") : null;
            var t = num != null ? num.GetComponent<TMP_Text>() : null;
            if (t == null) { log.AppendLine("  locker" + i + ": card text not found"); continue; }
            string digits = System.Text.RegularExpressions.Regex.Match(t.text, @"(\d+)\s*$").Groups[1].Value;
            if (digits.Length == 0) { log.AppendLine("  locker" + i + ": no number on the card (" + t.text + ")"); continue; }
            Undo.RecordObject(t, "card");
            t.text = "<size=38%>locker" + i + " =</size>\n" + digits;
            EditorUtility.SetDirty(t);
            log.AppendLine("  locker" + i + " card: locker" + i + " = " + digits);
        }
    }

    static void RamosPlaque()
    {
        Transform g = RamosLocker(2);
        Transform label = g != null ? g.Find("Label") : null;
        if (label == null) { log.AppendLine("  middle locker label not found"); return; }
        Vector3 p = label.localPosition + new Vector3(0f, 1.25f, 0f);
        TextMeshPro t = Sign(label, g, "RamosPlaque", "R. RAMOS  ·  MAINTENANCE\n<size=60%>my variables - do not touch</size>", p, new Vector2(9f, 1.4f), 4.2f, new Color(0.85f, 0.78f, 0.62f));
        log.AppendLine("  plaque at local y " + p.y.ToString("F2") + " (ceiling 11.25): " + (t != null));
    }

    static void RamosLaptop()
    {
        L3Keypad lap = Object.FindObjectsByType<L3Keypad>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(k => k.station == "m1");
        if (lap == null) { log.AppendLine("  laptop keypad (m1) not found"); return; }
        Undo.RecordObject(lap, "laptop");
        lap.code = RamosLaptopCode;
        lap.hint = RamosLaptopHint;
        EditorUtility.SetDirty(lap);
        Transform lid = lap.transform.Find("Lid/LidText");
        var t = lid != null ? lid.GetComponent<TMP_Text>() : null;
        if (t != null) { Undo.RecordObject(t, "lid"); t.text = "// RAMOS - job 1\nlocker2 = locker1;\nlocker1 = locker3;\n// What is in locker2 now?"; EditorUtility.SetDirty(t); }
        log.AppendLine("  laptop code/hint set; answer still " + lap.answer + "; lid text " + (t != null));
    }

    static void Level3Clock()
    {
        L3Manager m = Object.FindObjectsByType<L3Manager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (m == null) { log.AppendLine("  no L3Manager"); return; }
        Undo.RecordObject(m, "clock");
        m.timeLimit = 1200f;
        EditorUtility.SetDirty(m);
        log.AppendLine("  L3Manager.timeLimit = 1200 (20:00); session limit for Level 3 is fixed in LevelTimer.LimitFor");
    }

    static void RamosSticky()
    {
        Transform n = Find("StickyNote_Laptop");
        var ex = n != null ? n.GetComponent<L2Examine>() : null;
        if (ex == null) { log.AppendLine("  sticky note not found"); return; }
        Undo.RecordObject(ex, "sticky");
        ex.body = "Ramos kept one number in each locker.\nCopying a value doesn't link the boxes.\n\n" +
                  "(The names in his laptop's code are the names on his locker doors.)";
        EditorUtility.SetDirty(ex);
        log.AppendLine("  sticky note body explains the locker names");
    }
}
