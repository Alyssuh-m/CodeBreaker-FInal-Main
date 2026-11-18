using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;
using CodeBreaker.Level2;

// Code Breaker > Puzzle Pass: the scene-side part of the puzzle clarity pass (Level 2 + Level 3).
// It only changes existing objects (texts, materials, an extra component) and adds one flat picture
// on the Study whiteboard. Nothing is deleted. Each item writes a log to Logs/CB_PuzzlePass.txt.
//
//  Level 2 (Study, "Picture Key on the Whiteboard"):
//   - the desk note shows pictures instead of SHW QDPH  (new material copy, the old one is untouched)
//   - the desk wheel shows pictures and words instead of the alphabet  (new material copies)
//   - the whiteboard shows the hand-drawn picture key (a flat decal quad on its writing surface)
//     and can be examined (L2Examine + trigger collider)
//  Level 3 (rule text on existing objects):
//   - pegboard title: what makes a valid C# name
//   - shift-log printout: "% = what's left over after dividing"
public static class CodeBreakerPuzzlePass
{
    const string PicDir = "Assets/CodeBreaker/Level2/Resources/PictureKey";
    const string MatDir = "Assets/CodeBreaker/Level2/Materials/PuzzlePass";

    public const string NoteTitle = "Picture Note";
    public const string NoteBody = "PASSWORD = [heart] + [calendar]\n[heart] = [paw]\n(the key is on the whiteboard)";
    public const string BoardBody = "Hand-drawn on the whiteboard:\n" +
                                    "heart = who I love      house = my street\n" +
                                    "star = lucky number      paw = my dog\n" +
                                    "calendar = my best year      cup = my coffee";

    // ------------------------------------------------------------------ menu
    [MenuItem("Code Breaker/Puzzle Pass/Apply Level 2 Study Picture Key", false, 400)]
    static void MenuLevel2()
    {
        var log = new StringBuilder("PUZZLE PASS  Level 2  " + System.DateTime.Now + "\n");
        if (!OpenScene("Level 2", log)) { Write(log); return; }
        ImportTextures(log);
        ApplyLevel2Study(log);
        Save(log);
        Write(log);
    }

    [MenuItem("Code Breaker/Puzzle Pass/Apply Level 3 Rule Text", false, 401)]
    static void MenuLevel3()
    {
        var log = new StringBuilder("PUZZLE PASS  Level 3  " + System.DateTime.Now + "\n");
        if (!OpenScene("Level 3", log)) { Write(log); return; }
        ApplyLevel3Text(log);
        Save(log);
        Write(log);
    }

    [MenuItem("Code Breaker/Puzzle Pass/Apply Level 2 Study Picture Key", true)]
    [MenuItem("Code Breaker/Puzzle Pass/Apply Level 3 Rule Text", true)]
    static bool NotPlaying() => !Application.isPlaying;

    // ------------------------------------------------------------------ scenes
    static bool OpenScene(string sceneName, StringBuilder log)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.name == sceneName) { log.AppendLine("scene already open: " + active.path); return true; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { log.AppendLine("cancelled"); return false; }
        string path = EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);
        if (path == null) path = AssetDatabase.FindAssets("t:Scene " + sceneName).Select(AssetDatabase.GUIDToAssetPath)
                                              .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);
        if (path == null) { log.AppendLine("ERROR: scene " + sceneName + " not found"); return false; }
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        log.AppendLine("opened " + path);
        return true;
    }

    static void Save(StringBuilder log)
    {
        Scene s = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(s);
        bool ok = EditorSceneManager.SaveScene(s);
        AssetDatabase.SaveAssets();
        log.AppendLine(ok ? "saved " + s.path : "ERROR: could not save " + s.path);
    }

    static void Write(StringBuilder log)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_PuzzlePass.txt", log.ToString());
        Debug.Log(log.ToString());
    }

    static Transform FindDeep(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = FindDeep(root.transform, name);
            if (t != null) return t;
        }
        return null;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    // ------------------------------------------------------------------ textures & materials
    static void ImportTextures(StringBuilder log)
    {
        AssetDatabase.Refresh();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PicDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool changed = false;
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
            if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (ti.npotScale != TextureImporterNPOTScale.None && p.Contains("Whiteboard")) { ti.npotScale = TextureImporterNPOTScale.None; changed = true; }
            if (changed) { ti.SaveAndReimport(); log.AppendLine("import settings: " + p); }
        }
    }

    static Texture2D Tex(string file, StringBuilder log)
    {
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(PicDir + "/" + file + ".png");
        if (t == null) log.AppendLine("ERROR: texture missing " + PicDir + "/" + file + ".png");
        return t;
    }

    // A copy of 'src' with a new main texture, saved once under MatDir (the original stays untouched).
    static Material CopyWithTexture(Material src, string name, Texture tex)
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/CodeBreaker/Level2/Materials")) AssetDatabase.CreateFolder("Assets/CodeBreaker/Level2", "Materials");
            AssetDatabase.CreateFolder("Assets/CodeBreaker/Level2/Materials", "PuzzlePass");
        }
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", Vector2.one); m.SetTextureOffset("_BaseMap", Vector2.zero); }
        if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", Vector2.one); m.SetTextureOffset("_MainTex", Vector2.zero); }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------ Level 2: Study picture key
    public static void ApplyLevel2Study(StringBuilder log)
    {
        Texture2D noteTex = Tex("L2P_T_Note_Picture", log);
        Texture2D boardTex = Tex("L2P_T_WhiteboardPictureKey", log);
        Texture2D outerTex = Tex("L2P_T_PictureWheelOuter", log);
        Texture2D innerTex = Tex("L2P_T_PictureWheelInner", log);

        // 1. the desk note
        Transform note = FindDeep("Puzzle_Study_CipherNote_SHWQDPH");
        if (note == null) log.AppendLine("ERROR: Puzzle_Study_CipherNote_SHWQDPH not found");
        else
        {
            Transform paper = note.Find("Paper");
            Renderer pr = paper != null ? paper.GetComponent<Renderer>() : note.GetComponentInChildren<Renderer>();
            if (pr != null && noteTex != null)
            {
                Undo.RecordObject(pr, "Picture note");
                pr.sharedMaterial = CopyWithTexture(pr.sharedMaterial, "L2P_Note_Picture", noteTex);
                log.AppendLine("note paper -> L2P_Note_Picture");
            }
            var ex = note.GetComponent<L2Examine>();
            if (ex != null)
            {
                Undo.RecordObject(ex, "Picture note");
                ex.displayName = NoteTitle; ex.title = NoteTitle; ex.body = NoteBody; ex.verb = "Read";
                if (noteTex != null) ex.image = noteTex;
                EditorUtility.SetDirty(ex);
                log.AppendLine("note L2Examine text/image updated");
            }
            else log.AppendLine("WARNING: note has no L2Examine");
        }

        // 2. the wheel faces
        Transform wheel = FindDeep("Puzzle_Study_CipherWheel");
        if (wheel == null) log.AppendLine("ERROR: Puzzle_Study_CipherWheel not found");
        else
        {
            SwapFace(wheel, "OuterFace", "L2P_PictureWheelOuter", outerTex, log);
            SwapFace(wheel, "InnerFace", "L2P_PictureWheelInner", innerTex, log);
            var cw = wheel.GetComponent<L2CipherWheel>();
            if (cw != null) { Undo.RecordObject(cw, "Picture wheel"); cw.displayName = "Picture Wheel"; EditorUtility.SetDirty(cw); }
        }

        // 3. the whiteboard: picture key decal + examine
        Transform board = FindDeep("STU_Whiteboard");
        if (board == null) { log.AppendLine("ERROR: STU_Whiteboard not found"); return; }
        var br = board.GetComponent<MeshRenderer>();
        var mf = board.GetComponent<MeshFilter>();
        if (br == null || mf == null || mf.sharedMesh == null) { log.AppendLine("ERROR: whiteboard has no mesh"); return; }

        // the white writing surface = the sub-mesh that uses the whiteboard material
        Mesh mesh = mf.sharedMesh;
        int sub = -1;
        Material[] mats = br.sharedMaterials;
        for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
            if (mats[i] != null && mats[i].name.Contains("Whiteboard")) { sub = i; break; }
        Bounds local = sub >= 0 ? mesh.GetSubMesh(sub).bounds : mesh.bounds;
        Bounds face = TransformBounds(board.localToWorldMatrix, local);
        log.AppendLine("whiteboard surface (sub-mesh " + sub + ") world bounds c=" + face.center + " s=" + face.size);

        // thin axis = the surface normal; the room side = toward the study desk (the wheel)
        Vector3 s = face.size;
        int axis = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
        Vector3 n = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        Vector3 toward = (wheel != null ? wheel.position : face.center - n) - face.center;
        if (Vector3.Dot(toward, n) < 0f) n = -n;
        float width = axis == 2 ? s.x : s.z, height = axis == 1 ? s.z : s.y;
        // cover the whole writing surface (the board's old marker scribbles stay underneath)
        float w = width * 0.97f, h = height * 0.95f;

        Transform decal = board.Find("PictureKey_Decal");
        if (decal == null)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "PictureKey_Decal";
            Undo.RegisterCreatedObjectUndo(q, "Picture key decal");
            q.transform.SetParent(board, true);
            decal = q.transform;
            log.AppendLine("added child PictureKey_Decal on STU_Whiteboard");
        }
        decal.SetParent(null, true);   // size it in world units, then hang it back on the board
        decal.position = face.center + n * (s[axis] * 0.5f + 0.006f);
        decal.rotation = Quaternion.LookRotation(-n, Vector3.up);   // the quad's front faces the room
        decal.localScale = new Vector3(w, h, 1f);
        decal.SetParent(board, true);
        var dr = decal.GetComponent<MeshRenderer>();
        Material wbMat = sub >= 0 ? mats[sub] : null;
        if (boardTex != null) dr.sharedMaterial = CopyWithTexture(wbMat, "L2_Whiteboard_PictureKey", boardTex);
        dr.shadowCastingMode = ShadowCastingMode.Off;
        dr.receiveShadows = true;
        decal.gameObject.isStatic = false;
        log.AppendLine("decal size " + w.ToString("0.00") + " x " + h.ToString("0.00") + " at " + decal.position + " facing " + n);

        var bex = board.GetComponent<L2Examine>();
        if (bex == null) { bex = Undo.AddComponent<L2Examine>(board.gameObject); log.AppendLine("added L2Examine to STU_Whiteboard"); }
        bex.displayName = "Whiteboard"; bex.title = "PICTURE KEY"; bex.body = BoardBody; bex.verb = "Read the Whiteboard";
        bex.image = boardTex;
        EditorUtility.SetDirty(bex);

        if (board.GetComponents<Collider>().Length == 0)
        {
            var bc = Undo.AddComponent<BoxCollider>(board.gameObject);   // fits the mesh
            bc.isTrigger = true;
            log.AppendLine("added trigger BoxCollider to STU_Whiteboard (for E)");
        }
    }

    static void SwapFace(Transform wheel, string child, string matName, Texture tex, StringBuilder log)
    {
        Transform f = wheel.Find(child);
        Renderer r = f != null ? f.GetComponent<Renderer>() : null;
        if (r == null || tex == null) { log.AppendLine("WARNING: wheel face " + child + " not changed"); return; }
        Undo.RecordObject(r, "Picture wheel");
        r.sharedMaterial = CopyWithTexture(r.sharedMaterial, matName, tex);
        log.AppendLine("wheel " + child + " -> " + matName);
    }

    static Bounds TransformBounds(Matrix4x4 m, Bounds b)
    {
        Vector3 c = b.center, e = b.extents;
        var r = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            r.Encapsulate(m.MultiplyPoint3x4(p));
        }
        return r;
    }

    // ------------------------------------------------------------------ Level 3: rule text
    public static void ApplyLevel3Text(StringBuilder log)
    {
        Transform peg = FindDeep("Pegboard");
        Transform title = peg != null ? peg.Find("Title") : null;
        var tt = title != null ? title.GetComponent<TMP_Text>() : null;
        if (tt == null) log.AppendLine("ERROR: Pegboard/Title not found");
        else
        {
            Undo.RecordObject(tt, "Pegboard rule");
            Undo.RecordObject(title, "Pegboard rule");
            tt.text = CodeBreaker.Level3.EditorTools.Level3ServerRoomBuilder.PegboardTitle;
            tt.rectTransform.sizeDelta = CodeBreaker.Level3.EditorTools.Level3ServerRoomBuilder.PegboardTitleBox;
            Vector3 lp = title.localPosition;
            title.localPosition = new Vector3(lp.x, CodeBreaker.Level3.EditorTools.Level3ServerRoomBuilder.PegboardTitleY, lp.z);
            EditorUtility.SetDirty(tt);
            log.AppendLine("pegboard title -> valid-name rule");
        }

        Transform printout = FindDeep("Printout");
        if (printout == null) log.AppendLine("ERROR: Printout not found");
        else
        {
            Transform txt = printout.Find("Text");
            var pt = txt != null ? txt.GetComponent<TMP_Text>() : null;
            if (pt != null)
            {
                Undo.RecordObject(pt, "Printout legend");
                pt.text = CodeBreaker.Level3.EditorTools.Level3ServerRoomBuilder.PrintoutText;
                EditorUtility.SetDirty(pt);
                log.AppendLine("printout sheet text -> + % legend");
            }
            var ex = printout.GetComponent<L2Examine>();
            if (ex != null)
            {
                Undo.RecordObject(ex, "Printout legend");
                ex.body = CodeBreaker.Level3.EditorTools.Level3ServerRoomBuilder.PrintoutBody;
                EditorUtility.SetDirty(ex);
                log.AppendLine("printout L2Examine body -> + % legend");
            }
        }

        Transform fb = FindDeep("FuseBox");
        Transform disp = fb != null ? fb.Find("DisplayText") : null;
        var dt = disp != null ? disp.GetComponent<TMP_Text>() : null;
        if (dt != null) { Undo.RecordObject(dt, "Fuse display"); dt.text = "x = 2 | y = 9 | temp = --"; EditorUtility.SetDirty(dt); log.AppendLine("fuse display start text"); }
    }
}
