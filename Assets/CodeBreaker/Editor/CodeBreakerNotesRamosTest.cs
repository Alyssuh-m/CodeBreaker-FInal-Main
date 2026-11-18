using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using CodeBreaker.Level2;
using CodeBreaker.Level3;

// Code Breaker > Notes & Ramos Test: Play-mode checks for the Notes journal reset and the Ramos
// locker puzzle. Each item drives the real game objects and appends to Logs/CB_NotesRamosTest.txt
// (pictures in Logs/Shots/NR_*.png). Nothing is saved to the scene.
public static class CodeBreakerNotesRamosTest
{
    const string LogPath = "Logs/CB_NotesRamosTest.txt";
    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static StringBuilder log;
    static float waitUntil;

    static void Run(string title, IEnumerator body)
    {
        if (!Application.isPlaying || L2Game.Instance == null) { Debug.LogWarning("[Notes/Ramos Test] enter Play mode in Level 2 or 3 first"); return; }
        log = new StringBuilder("\n==== " + title + "  (" + SceneManager.GetActiveScene().name + ")  " + System.DateTime.Now + "\n");
        stack.Clear();
        stack.Push(body);
        waitUntil = 0f;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!Application.isPlaying) { Stop("left play mode"); return; }
        if (Time.unscaledTime < waitUntil) return;
        while (true)
        {
            if (stack.Count == 0) { Stop("done"); return; }
            IEnumerator top = stack.Peek();
            bool more;
            try { more = top.MoveNext(); }
            catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); stack.Pop(); continue; }
            if (!more) { stack.Pop(); continue; }
            if (top.Current is IEnumerator sub) { stack.Push(sub); continue; }
            if (top.Current is float f) waitUntil = Time.unscaledTime + f;
            return;
        }
    }

    static void Stop(string why)
    {
        EditorApplication.update -= Tick;
        log.AppendLine("== " + why);
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, log.ToString());
        Debug.Log("[Notes/Ramos Test] " + why + " -> " + LogPath);
    }

    // ------------------------------------------------------------------ helpers
    static L2Game G => L2Game.Instance;

    static void Teleport(Vector3 feet, float yaw)
    {
        G.Teleport(feet + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
        Physics.SyncTransforms();
    }

    static HashSet<string> Flags() => (HashSet<string>)typeof(L2Game).GetField("flags", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(G);

    static int NoteFlagsInMemory() => Flags().Count(L2Game.IsNoteFlag);

    static string NoteFlagsInCheckpoint()
    {
        SaveData d = SaveSystem.Load();
        if (d == null || !SaveSystem.HasCheckpoint(d, G.SceneName)) return "no checkpoint";
        L2State s = JsonUtility.FromJson<L2State>(d.checkpointJson);
        return s.flags.Count(L2Game.IsNoteFlag) + " note flags, " + s.flags.Count(f => f.StartsWith("solved_")) + " solved, " + s.flags.Count + " flags total";
    }

    static string InfoText()
    {
        var f = typeof(L2UI).GetField("infoBody", BindingFlags.NonPublic | BindingFlags.Instance);
        var t = f != null ? f.GetValue(G.ui) as TMPro.TMP_Text : null;
        return t != null ? t.text : "(no info screen)";
    }

    static string InfoLayout()
    {
        var f = typeof(L2UI).GetField("infoScroll", BindingFlags.NonPublic | BindingFlags.Instance);
        var sr = f != null ? f.GetValue(G.ui) as UnityEngine.UI.ScrollRect : null;
        if (sr == null) return "(no scroll)";
        float view = ((RectTransform)sr.viewport).rect.height, content = sr.content.rect.height;
        return "text height " + content.ToString("F0") + " / screen " + view.ToString("F0") + (content > view ? "  -> scrolls" : "  -> fits");
    }

    static void Shot(string name, Vector3 from, Vector3 look)
    {
        Directory.CreateDirectory("Logs/Shots");
        var go = new GameObject("CB_TestCam");
        var cam = go.AddComponent<Camera>();
        if (Camera.main != null) cam.CopyFrom(Camera.main);
        cam.enabled = false; cam.fieldOfView = 55f; cam.nearClipPlane = 0.02f;
        var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt;
        cam.transform.position = from; cam.transform.LookAt(look);
        cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null;
        File.WriteAllBytes("Logs/Shots/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(go);
        log.AppendLine("  shot " + name);
    }

    static string Objective()
    {
        L3Manager m = L3Manager.Instance;
        if (m == null) { string o = G.CurrentObjective(out string h); return o + "  |  " + h; }
        L3Manager.Step st = m.CurrentStep(G);
        return st != null ? st.key + ": " + st.objective + "  |  " + st.hint : "(no wing step)";
    }

    static L2Openable Locker(int i) => Object.FindObjectsByType<L2Openable>(FindObjectsInactive.Include).FirstOrDefault(o => o.label == "locker" + i);
    static L3Keypad Laptop() => Object.FindObjectsByType<L3Keypad>(FindObjectsInactive.Include).FirstOrDefault(k => k.station == "m1");

    // ------------------------------------------------------------------ Ramos
    [MenuItem("Code Breaker/Notes & Ramos Test/1. Ramos: open the lockers (Level 3, Play mode)", false, 720)]
    static void Ramos1() => Run("RAMOS 1: lockers", Ramos1Body());

    static IEnumerator Ramos1Body()
    {
        L3Manager m = L3Manager.Instance;
        if (m == null) { log.AppendLine("not Level 3"); yield break; }
        if (G.ModalOpen) G.ui.CloseModal();
        L2Openable l2 = Locker(2);
        if (l2 == null) { log.AppendLine("lockers not found"); yield break; }
        Vector3 wall = l2.transform.parent.position;           // middle locker group (front faces +x)
        Teleport(new Vector3(wall.x + 6f, 0f, wall.z), -90f);
        yield return 1.2f;
        log.AppendLine("objective in Maintenance: " + Objective());
        Shot("NR_Lockers_Closed", new Vector3(wall.x + 9f, 6.5f, wall.z), new Vector3(wall.x, 6.5f, wall.z));
        for (int i = 1; i <= 3; i++)
        {
            L2Openable o = Locker(i);
            o.Interact();
            yield return 1.0f;
            log.AppendLine("  opened locker" + i + " -> seen " + m.RamosLockersSeen(G) + "/3, line: " + m.RamosLockerLine(G));
            log.AppendLine("    objective: " + Objective());
        }
        Shot("NR_Lockers_Open", new Vector3(wall.x + 9f, 6.5f, wall.z), new Vector3(wall.x, 5.6f, wall.z));
        Vector3 c1 = Locker(1).transform.parent.position;
        Shot("NR_Locker1_Card", new Vector3(c1.x + 4.5f, 5.3f, c1.z), new Vector3(c1.x, 5.2f, c1.z));
        L3Keypad lap = Laptop();
        log.AppendLine("laptop screen text:\n" + lap.KeypadCode);
        Shot("NR_Laptop_World", lap.transform.position + lap.transform.forward * -2.2f + Vector3.up * 1.4f, lap.transform.position + Vector3.up * 0.5f);
        Transform note = GameObject.Find("StickyNote_Laptop")?.transform;
        if (note != null) log.AppendLine("sticky note body: " + note.GetComponent<L2Examine>().body.Replace("\n", " / "));
        m.ShowJournal();
        yield return 0.5f;
        log.AppendLine("journal: " + InfoLayout() + "\n" + InfoText());
    }

    [MenuItem("Code Breaker/Notes & Ramos Test/2. Ramos: open the laptop (Play mode)", false, 721)]
    static void Ramos2() => Run("RAMOS 2: laptop", Ramos2Body());

    static IEnumerator Ramos2Body()
    {
        if (G.ModalOpen) G.ui.CloseModal();
        L3Keypad lap = Laptop();
        if (lap == null) { log.AppendLine("no laptop"); yield break; }
        lap.Interact();
        yield return 0.5f;
        log.AppendLine("keypad open: " + G.ui.CodeKeypadOpen + ", objective: " + Objective());
    }

    [MenuItem("Code Breaker/Notes & Ramos Test/3. Ramos: wrong answer 5, then 8 (Play mode)", false, 722)]
    static void Ramos3() => Run("RAMOS 3: answers", Ramos3Body());

    static IEnumerator Ramos3Body()
    {
        L3Manager m = L3Manager.Instance;
        if (!G.ui.CodeKeypadOpen) { Laptop().Interact(); yield return 0.5f; }
        float t0 = m.Remaining;
        G.ui.PressCodeKey("5"); G.ui.PressCodeKey("OK");
        yield return 1.5f;
        log.AppendLine("typed 5: solved " + m.Solved("m1") + ", time " + t0.ToString("F0") + " -> " + m.Remaining.ToString("F0"));
        G.ui.PressCodeKey("8"); G.ui.PressCodeKey("OK");
        yield return 1.5f;
        log.AppendLine("typed 8: solved " + m.Solved("m1") + ", objective now: " + Objective());
    }

    // ------------------------------------------------------------------ notes reset
    [MenuItem("Code Breaker/Notes & Ramos Test/4. Notes: read notes, save, then time up (Play mode)", false, 730)]
    static void Notes1() => Run("NOTES 1: read + fail", Notes1Body());

    static IEnumerator Notes1Body()
    {
        if (G.ModalOpen) G.ui.CloseModal();
        int read = 0;
        foreach (L2Examine ex in Object.FindObjectsByType<L2Examine>(FindObjectsInactive.Exclude))
        {
            ex.Interact();
            G.ui.CloseModal();
            read++;
        }
        log.AppendLine("read " + read + " notes -> note flags in memory: " + NoteFlagsInMemory());
        G.SaveProgress("notes test");
        log.AppendLine("checkpoint saved: " + NoteFlagsInCheckpoint());
        if (L3Manager.Instance != null) L3Manager.Instance.ShowJournal();
        else Object.FindFirstObjectByType<L2LevelSession>()?.ShowJournal();
        yield return 0.5f;
        log.AppendLine("journal while playing (" + InfoLayout() + "):\n" + InfoText());
        yield return 2.5f;   // time for a screenshot of the full journal
        G.ui.CloseModal();

        // the attempt fails: the level's own time-up
        MonoBehaviour rules = (MonoBehaviour)L3Manager.Instance ?? Object.FindFirstObjectByType<L2LevelSession>();
        MethodInfo timeUp = rules.GetType().GetMethod("TimeUp", BindingFlags.NonPublic | BindingFlags.Instance);
        timeUp.Invoke(rules, null);
        yield return 0.5f;
        log.AppendLine("after TIME UP -> note flags in memory: " + NoteFlagsInMemory() + "; checkpoint: " + NoteFlagsInCheckpoint());
        log.AppendLine("(now press TRY AGAIN on the screen, then run item 5)");
    }

    [MenuItem("Code Breaker/Notes & Ramos Test/5. Notes: check the journal after Try Again (Play mode)", false, 731)]
    static void Notes2() => Run("NOTES 2: after Try Again", Notes2Body());

    static IEnumerator Notes2Body()
    {
        if (G.ModalOpen) G.ui.CloseModal();
        log.AppendLine("loaded state: " + (G.LoadedState != null ? "checkpoint" : "fresh start") + "; note flags in memory: " + NoteFlagsInMemory() +
                       "; solved stations kept: " + Flags().Count(f => f.StartsWith("solved_")));
        if (L3Manager.Instance != null)
        {
            log.AppendLine("Level 3 clock: " + L3Manager.Instance.Remaining.ToString("F0") + " s left (limit " + L3Manager.Instance.timeLimit + ")");
            L3Manager.Instance.ShowJournal();
        }
        else Object.FindFirstObjectByType<L2LevelSession>()?.ShowJournal();
        yield return 0.5f;
        log.AppendLine("journal now (" + InfoLayout() + "):\n" + InfoText());
    }
    // ------------------------------------------------------------------ Level 1 lectern preview (view only)
    static int lecternQ = -1;

    [MenuItem("Code Breaker/Notes & Ramos Test/6. Lectern: show next question (Level 1, Play mode)", false, 740)]
    static void LecternNext()
    {
        if (!Application.isPlaying || RoomManager.Instance == null) { Debug.LogWarning("[Lectern] enter Play mode in Level 1"); return; }
        RoomManager rm = RoomManager.Instance;
        LecternQuestion lec = rm.CurrentRoom.lectern;
        lecternQ = (lecternQ + 1) % rm.questionBank.Count;
        lec.Close();
        lec.SetQuestion(rm.CurrentRoom.roomName, rm.questionBank.Get(lecternQ));
        typeof(LecternQuestion).GetMethod("Open", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lec, null);
        Debug.Log("[Lectern] question " + (lecternQ + 1) + ": font " + lec.questionText.fontSize.ToString("F1") + " (auto " + lec.questionText.enableAutoSizing + ")");
    }
    // ------------------------------------------------------------------ movement (jump / crouch) recorder
    // Records the player for 8 s while you press keys yourself (Space spam, hold Left Ctrl...).
    [MenuItem("Code Breaker/Notes & Ramos Test/7. Movement: record 8 s (Play mode)", false, 750)]
    static void MoveRecord()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Move] enter Play mode"); return; }
        log = new StringBuilder("\n==== MOVEMENT  (" + SceneManager.GetActiveScene().name + ")  " + System.DateTime.Now + "\n");
        stack.Clear(); stack.Push(MoveBody()); waitUntil = 0f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static IEnumerator MoveBody()
    {
        GameObject p = GameObject.FindWithTag("Player");
        var pm = p.GetComponent<PlayerMovement>();
        var cc = p.GetComponent<CharacterController>();
        Transform view = p.transform.Find("View Height");
        Camera cam = p.GetComponentInChildren<Camera>();
        log.AppendLine("t     feetY   grounded crouch  ccH    camY(world)  jumpsStarted");
        float t0 = Time.unscaledTime, lastY = 0f, peak = float.MinValue, floorY = float.MaxValue;
        int jumps = 0; bool rising = false;
        while (Time.unscaledTime - t0 < 8f)
        {
            float feet = cc.transform.TransformPoint(cc.center).y - cc.height * 0.5f * Mathf.Abs(cc.transform.lossyScale.y);
            float vy = (feet - lastY) / Mathf.Max(Time.deltaTime, 0.0001f);
            if (vy > 2f && !rising) { jumps++; rising = true; }
            if (vy < 0f) rising = false;
            peak = Mathf.Max(peak, feet); floorY = Mathf.Min(floorY, feet);
            lastY = feet;
            log.AppendLine((Time.unscaledTime - t0).ToString("F2").PadRight(6) + feet.ToString("F3").PadRight(8) + pm.IsGrounded.ToString().PadRight(9) + pm.IsCrouching.ToString().PadRight(8) +
                           cc.height.ToString("F2").PadRight(7) + (cam != null ? cam.transform.position.y.ToString("F3") : "-").PadRight(13) + jumps);
            yield return 0.1f;
        }
        log.AppendLine("jumps started: " + jumps + ", highest feet " + peak.ToString("F2") + ", lowest " + floorY.ToString("F2") + " (rise " + (peak - floorY).ToString("F2") + ")" +
                       ", view holder: " + (view != null ? "yes" : "NO"));
    }

    // Head-room test: a beam appears above the player (solid after 1.5 s, so crouch first), stays until
    // 4 s, then is removed. Standing up must wait until the beam is gone. Also logs walking speed.
    [MenuItem("Code Breaker/Notes & Ramos Test/8. Movement: beam above head (Play mode)", false, 751)]
    static void MoveBeam()
    {
        if (!Application.isPlaying) return;
        log = new StringBuilder("\n==== HEADROOM  (" + SceneManager.GetActiveScene().name + ")  " + System.DateTime.Now + "\n");
        stack.Clear(); stack.Push(BeamBody()); waitUntil = 0f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static IEnumerator BeamBody()
    {
        GameObject p = GameObject.FindWithTag("Player");
        var pm = p.GetComponent<PlayerMovement>();
        var cc = p.GetComponent<CharacterController>();
        Camera cam = p.GetComponentInChildren<Camera>();
        float feet = cc.transform.TransformPoint(cc.center).y - cc.height * 0.5f * Mathf.Abs(cc.transform.lossyScale.y);
        float eyeStand = cam.transform.position.y;
        float crouchEye = feet + (eyeStand - feet) * pm.crouchHeightFactor;
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "CB_TestBeam";
        beam.transform.position = new Vector3(p.transform.position.x, crouchEye + 0.35f + 0.25f, p.transform.position.z);
        beam.transform.localScale = new Vector3(3f, 0.5f, 3f);
        Collider col = beam.GetComponent<Collider>(); col.enabled = false;
        log.AppendLine("feet " + feet.ToString("F2") + ", standing eye " + eyeStand.ToString("F2") + ", crouched eye " + crouchEye.ToString("F2") + ", beam bottom " + (crouchEye + 0.35f).ToString("F2"));
        float t0 = Time.unscaledTime; Vector3 last = p.transform.position;
        while (Time.unscaledTime - t0 < 6f)
        {
            float t = Time.unscaledTime - t0;
            if (t > 1.5f && beam != null && !col.enabled) { col.enabled = true; log.AppendLine("  -- beam solid"); }
            if (t > 4f && beam != null) { Object.Destroy(beam); beam = null; log.AppendLine("  -- beam removed"); }
            Vector3 pos = p.transform.position; Vector3 d = pos - last; d.y = 0f; last = pos;
            log.AppendLine(t.ToString("F2").PadRight(6) + "crouch " + pm.IsCrouching.ToString().PadRight(6) + " ctrl " + Input.GetKey(KeyCode.LeftControl).ToString().PadRight(6) +
                           " eye " + cam.transform.position.y.ToString("F2") + "  speed " + (d.magnitude / 0.1f).ToString("F1"));
            yield return 0.1f;
        }
        if (beam != null) Object.Destroy(beam);
    }

    // Crouch-walk test: while it runs, S is the crouch key (Ctrl + a letter would trigger editor
    // shortcuts in Play mode). Hold S, then C+S, and compare the speeds. Restores Left Ctrl after.
    [MenuItem("Code Breaker/Notes & Ramos Test/9. Movement: crouch-walk speed, S = crouch (Play mode)", false, 752)]
    static void CrouchWalk()
    {
        if (!Application.isPlaying) return;
        log = new StringBuilder("\n==== CROUCH WALK  (" + SceneManager.GetActiveScene().name + ")  " + System.DateTime.Now + "\n");
        stack.Clear(); stack.Push(CrouchWalkBody()); waitUntil = 0f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static IEnumerator CrouchWalkBody()
    {
        GameObject p = GameObject.FindWithTag("Player");
        var pm = p.GetComponent<PlayerMovement>();
        var cc = p.GetComponent<CharacterController>();
        Camera cam = p.GetComponentInChildren<Camera>();
        KeyCode keep = pm.crouchKey;
        pm.crouchKey = KeyCode.S;   // holding S = walk back AND crouch
        float t0 = Time.unscaledTime; Vector3 last = p.transform.position;
        float maxStand = 0f, maxCrouch = 0f;
        while (Time.unscaledTime - t0 < 5f)
        {
            Vector3 pos = p.transform.position; Vector3 d = pos - last; d.y = 0f; last = pos;
            float sp = d.magnitude / 0.1f;
            if (pm.IsCrouching) maxCrouch = Mathf.Max(maxCrouch, sp); else maxStand = Mathf.Max(maxStand, sp);
            float feet = cc.transform.TransformPoint(cc.center).y - cc.height * 0.5f * Mathf.Abs(cc.transform.lossyScale.y);
            log.AppendLine((Time.unscaledTime - t0).ToString("F2").PadRight(6) + "crouch " + pm.IsCrouching.ToString().PadRight(6) +
                           " eye " + cam.transform.position.y.ToString("F2") + "  feet " + feet.ToString("F3") + "  speed " + sp.ToString("F1"));
            yield return 0.1f;
        }
        pm.crouchKey = keep;
        log.AppendLine("top speed standing " + maxStand.ToString("F1") + ", crouched " + maxCrouch.ToString("F1") + " (crouch key restored to " + keep + ")");
    }
    static void SetRem(object o) => o.GetType().GetMethod("SetRemaining", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, new object[] { 5f });

    // Puts 5 seconds on the clock so the level's normal time-out can be checked quickly.
    [MenuItem("Code Breaker/Notes & Ramos Test/10. Timer: 5 seconds left (Play mode)", false, 760)]
    static void TimerFive()
    {
        if (!Application.isPlaying) return;
        GameFlowManager gm = GameFlowManager.Instance;
        string scene = SceneManager.GetActiveScene().name;
        string limit = gm != null && gm.levelTimer != null ? gm.levelTimer.CurrentTimeLimit.ToString("F0") : "-";
        if (L3Manager.Instance != null) { Debug.Log("[Timer] " + scene + " own limit " + L3Manager.Instance.timeLimit + ", session limit " + limit); SetRem(L3Manager.Instance); }
        else if (Object.FindFirstObjectByType<L2LevelSession>() is L2LevelSession s2) { Debug.Log("[Timer] " + scene + " own limit " + s2.timeLimit + ", session limit " + limit); SetRem(s2); }
        else if (gm != null && gm.levelTimer != null) { Debug.Log("[Timer] " + scene + " session limit " + limit); gm.levelTimer.SetRemainingTime(5f); }
    }
    // Direction check: while it runs, turn with the mouse and hold W. Logs which object the mouse
    // turns, the angle between view and body, and the angle between the walk and the view.
    [MenuItem("Code Breaker/Notes & Ramos Test/11. Movement: W follows the view (Play mode)", false, 753)]
    static void DirCheck()
    {
        if (!Application.isPlaying) return;
        log = new StringBuilder("\n==== DIRECTION  (" + SceneManager.GetActiveScene().name + ")  " + System.DateTime.Now + "\n");
        stack.Clear(); stack.Push(DirBody()); waitUntil = 0f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static IEnumerator DirBody()
    {
        GameObject p = GameObject.FindWithTag("Player");
        Camera cam = p.GetComponentInChildren<Camera>();
        var mm = cam.GetComponent<MouseMovement>();
        var body = mm != null ? typeof(MouseMovement).GetField("playerBody", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mm) as Transform : null;
        log.AppendLine("mouse turns: " + (body != null ? body.name : "null") + (body == p.transform ? "  (the player: OK)" : "  (NOT the player)"));
        float t0 = Time.unscaledTime; Vector3 last = p.transform.position; float worst = 0f; int moving = 0;
        while (Time.unscaledTime - t0 < 7f)
        {
            Vector3 cf = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 bf = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
            Vector3 d = p.transform.position - last; d.y = 0f; last = p.transform.position;
            string walk = "-";
            if (d.magnitude > 0.05f) { float a = Vector3.Angle(d, cf); worst = Mathf.Max(worst, a); moving++; walk = a.ToString("F0") + " deg"; }
            log.AppendLine((Time.unscaledTime - t0).ToString("F1").PadRight(5) + " view yaw " + cam.transform.eulerAngles.y.ToString("F0").PadLeft(4) +
                           "  view-vs-body " + Vector3.Angle(cf, bf).ToString("F0").PadLeft(3) + " deg   walk-vs-view " + walk);
            yield return 0.2f;
        }
        log.AppendLine("moving samples " + moving + ", worst walk-vs-view angle " + worst.ToString("F0") + " deg (W should be ~0)");
    }
    // Footsteps: counts the level's footsteps for 6 s (walk, stop, crouch-walk with the keys yourself).
    [MenuItem("Code Breaker/Notes & Ramos Test/12. Footsteps: count for 6 s (Play mode)", false, 770)]
    static void StepsCheck()
    {
        if (!Application.isPlaying) return;
        log = new StringBuilder("\n==== FOOTSTEPS  (" + SceneManager.GetActiveScene().name + ")  " + System.DateTime.Now + "\n");
        stack.Clear(); stack.Push(StepsBody()); waitUntil = 0f;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static IEnumerator StepsBody()
    {
        var fs = Object.FindFirstObjectByType<LevelFootsteps>();
        AudioManager am = AudioManager.Instance;
        log.AppendLine("LevelFootsteps: " + (fs != null ? (fs.enabled ? "on" : "DISABLED (no clips)") : "missing") +
                       ", AudioManager: " + (am != null ? (am.Silenced ? "muted" : "active") : "none"));
        if (fs == null) yield break;
        GameObject p = GameObject.FindWithTag("Player");
        var pm = p.GetComponent<PlayerMovement>();
        float t0 = Time.unscaledTime; Vector3 last = p.transform.position; int lastCount = fs.StepCount;
        while (Time.unscaledTime - t0 < 6f)
        {
            Vector3 d = p.transform.position - last; d.y = 0f; last = p.transform.position;
            int n = fs.StepCount;
            log.AppendLine((Time.unscaledTime - t0).ToString("F1").PadRight(5) + " speed " + (d.magnitude / 0.25f).ToString("F1").PadLeft(5) +
                           "  crouch " + pm.IsCrouching.ToString().PadRight(6) + " steps +" + (n - lastCount) + " (total " + n + ")");
            lastCount = n;
            yield return 0.25f;
        }
    }
}
