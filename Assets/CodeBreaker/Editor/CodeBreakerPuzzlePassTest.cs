using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using CodeBreaker.Level2;
using CodeBreaker.Level3;

// Code Breaker > Puzzle Pass > Test: Next Step (Play mode only).
// Walks through the puzzle-pass features one step per click by calling the real game components
// (the same methods the player's E key / UI buttons call), logs what the game reports, and saves a
// screenshot of the Game view for each step to Logs/PuzzleTest_<n>.png. Log: Logs/CB_PuzzleTest.txt.
public static class CodeBreakerPuzzlePassTest
{
    static int step;
    static readonly StringBuilder log = new StringBuilder();
    static string lastScene;

    [MenuItem("Code Breaker/Puzzle Pass/Test: Next Step", false, 420)]
    static void Next()
    {
        L2Game g = L2Game.Instance;
        if (g == null) { Debug.LogWarning("Puzzle test: no L2Game in this scene"); return; }
        string scene = g.SceneName;
        if (scene != lastScene) { step = 0; log.Length = 0; lastScene = scene; }
        var steps = scene == "Level 3" ? Level3Steps(g) : Level2Steps(g);
        if (step >= steps.Count) { Line("== all steps done =="); Flush(); return; }
        Line("---- step " + (step + 1) + " / " + steps.Count);
        try { steps[step](); }
        catch (System.Exception e) { Line("EXCEPTION: " + e); }
        int shot = ++step;
        Shot("Logs/PuzzleTest_" + scene.Replace(" ", "") + "_" + shot + ".png", 1.6f);
    }

    [MenuItem("Code Breaker/Puzzle Pass/Test: Next Step", true)]
    static bool Playing() => Application.isPlaying;

    [MenuItem("Code Breaker/Puzzle Pass/Test: Restart Steps", false, 421)]
    static void Restart() { step = 0; log.Length = 0; lastScene = null; }

    // ------------------------------------------------------------------ helpers
    static void Line(string s) { log.AppendLine(s); }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CB_PuzzleTest.txt", log.ToString());
    }

    static void Shot(string path, float delay)
    {
        double at = EditorApplication.timeSinceStartup + delay;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (EditorApplication.timeSinceStartup < at) return;
            EditorApplication.update -= cb;
            ScreenCapture.CaptureScreenshot(path);
            Line("screenshot " + path);
            // log the HUD text too
            L2Game g = L2Game.Instance;
            if (g != null && g.playerCamera != null) Line("camera " + g.playerCamera.transform.position + " fwd " + g.playerCamera.transform.forward);
            if (g != null) Line("objective: " + g.CurrentObjective(out string h) + "  |  hint: " + h.Replace("\n", " / "));
            Flush();
        };
        EditorApplication.update += cb;
    }

    // stand 'dist' in front of 'target' (along 'away'), looking at it
    static void Look(L2Game g, Vector3 target, Vector3 away, float dist)
    {
        away.y = 0f;
        away.Normalize();
        // stand on whichever side (away / -away) is open: the wall side is blocked right away
        float Free(Vector3 d) => Physics.Raycast(target + d * 0.4f + Vector3.up * 0.2f, d, out RaycastHit h, dist, ~0, QueryTriggerInteraction.Ignore) ? h.distance : dist;
        if (Free(-away) > Free(away) + 0.5f) away = -away;
        dist = Mathf.Min(dist, Mathf.Max(2f, Free(away) - 0.6f));
        Vector3 pos = target + away * dist;
        pos.y = g.player.position.y;
        Vector3 d = target - pos; d.y = 0f;
        g.Teleport(pos, Quaternion.LookRotation(d));
        if (g.playerCamera != null)
        {
            Vector3 to = target - g.playerCamera.transform.position;
            float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            Transform cam = g.playerCamera.transform;
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            // the player's mouse-look script keeps its own angles: set them too and freeze it (play mode only)
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (MonoBehaviour mb in g.player.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "MouseMovement") continue;
                var t = mb.GetType();
                t.GetField("xRotation", flags)?.SetValue(mb, pitch);
                t.GetField("yRotation", flags)?.SetValue(mb, Quaternion.LookRotation(d).eulerAngles.y);
                t.GetField("mouseSensitivity", flags)?.SetValue(mb, 0f);
            }
        }
        Line("player at " + g.player.position + " looking at " + target);
    }

    static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

    static void CloseUI(L2Game g)
    {
        if (g.ui != null && g.ModalOpen) g.ui.CloseModal();
    }

    static void Notes(L2Game g)
    {
        // last notifications are not readable from code; the journal / objective are logged instead
    }

    // ------------------------------------------------------------------ Level 2
    static List<System.Action> Level2Steps(L2Game g)
    {
        var s = new List<System.Action>();
        L2TV tv = Find<L2TV>();
        L2WallClock clock = Find<L2WallClock>();
        L2StarLamp lamp = Find<L2StarLamp>();
        L2StarSlot slot = Find<L2StarSlot>();

        s.Add(() =>   // 1. start: intro note + counter text
        {
            Line("intro flag set: " + g.Flag("intro_photo_note"));
        });
        s.Add(() =>   // 2. open an EMPTY hiding container
        {
            L2ItemShuffle sh = Find<L2ItemShuffle>();
            L2Openable target = null;
            foreach (L2HidingSpot sp in Object.FindObjectsByType<L2HidingSpot>(FindObjectsInactive.Include))
            {
                if (sp.room != "living" || sp.container == null) continue;
                bool used = false;
                foreach (var e in sh.items) if (sh.SpotOf(e.itemId) == sp) used = true;
                if (!used) { target = sp.container; break; }
            }
            if (target == null) { Line("no empty living-room container found"); return; }
            Look(g, target.transform.position, -target.transform.forward + Vector3.up * 0f, 4f);
            target.Interact();
            Line("opened " + target.name + "  searched=" + target.Searched + "  prompt now: " + target.Prompt);
            g.StartCoroutine(CloseLater(target));
        });
        s.Add(() =>   // 3. clock right, TV wrong -> lamp message
        {
            CloseUI(g);
            if (clock != null) { clock.Confirm(8, 0); Line("clock confirmed 8:00 -> IsCorrect=" + clock.IsCorrect + "  prompt: " + clock.Prompt); }
            if (tv != null) { tv.SetPower(true); tv.SetChannel(1); }
            if (lamp != null)
            {
                Look(g, lamp.transform.position + Vector3.up * 3f, Vector3.right, 8f);
                if (lamp.LampOn) lamp.Interact();
                lamp.Interact();
                Line("lamp on=" + lamp.LampOn + " shadow=" + lamp.ShadowActive);
            }
        });
        s.Add(() =>   // 4. TV to channel 3 -> blue tint + shadow + slot glow
        {
            if (tv != null)
            {
                tv.SetChannel(3);
                Light tint = tv.transform.Find("TV Blue Tint (runtime)")?.GetComponent<Light>();
                Line("tv blue=" + tv.IsBlue + "  tint light exists=" + (tint != null));
            }
            if (slot != null) Look(g, slot.transform.position, Vector3.right, 7f);
            Line("shadow active=" + (lamp != null && lamp.ShadowActive));
        });
        s.Add(() =>   // 5. slot glow check + hint without figurine
        {
            Light glow = slot != null ? slot.transform.Find("Star Slot Glow (runtime)")?.GetComponent<Light>() : null;
            Line("slot glow enabled=" + (glow != null && glow.enabled) + " intensity=" + (glow != null ? glow.intensity.ToString("0.00") : "-"));
        });
        s.Add(() =>   // 6. place the star -> key drops, TV static
        {
            if (!g.HasItem(L2Items.StarFigurine)) g.AddItem(L2Items.StarFigurine, false);
            bool ok = slot != null && slot.UseItem(L2Items.StarFigurine);
            Line("star placed=" + ok);
            if (tv != null) Look(g, tv.transform.position, Vector3.left + Vector3.forward * 0.2f, 9f);
        });
        s.Add(() =>   // 7. jewellery drawer 5 stuck message
        {
            L2JewelleryBox jb = Find<L2JewelleryBox>();
            if (jb != null) Line("drawer 5 -> \"" + jb.Toggle(5) + "\"  secrets=" + jb.SecretsFound() + "/3");
        });
        s.Add(() =>   // 8. oven: wrong code 4126 -> digits in right place
        {
            L2Oven oven = Find<L2Oven>();
            if (oven == null || g.ui == null) return;
            g.ui.OpenKeypad(oven);
            MethodInfo press = typeof(L2UI).GetMethod("PressKey", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (string k in new[] { "4", "1", "2", "6", "OK" }) press.Invoke(g.ui, new object[] { k });
            Line("typed 4126 on the oven (answer 4162)");
        });
        s.Add(() =>   // 9. kitchen ingredient -> journal
        {
            CloseUI(g);
            foreach (L2Ingredient ing in Object.FindObjectsByType<L2Ingredient>(FindObjectsInactive.Include)) g.SetFlag("seen_" + ing.ingredient.ToLowerInvariant());
            L2LevelSession.Instance.ShowJournal();
        });
        s.Add(() =>   // 10. study whiteboard
        {
            CloseUI(g);
            g.SetFlag("exit_living"); g.SetFlag("exit_bedroom"); g.SetFlag("exit_kitchen");
            g.RefreshObjective(false);
            Transform wb = GameObject.Find("STU_Whiteboard")?.transform;
            if (wb != null) Look(g, wb.position, Vector3.left, 8f);
            Line("whiteboard examine=" + (wb != null && wb.GetComponent<L2Examine>() != null));
        });
        s.Add(() =>   // 11. whiteboard close-up
        {
            Transform wb = GameObject.Find("STU_Whiteboard")?.transform;
            wb?.GetComponent<L2Examine>()?.Interact();
        });
        s.Add(() =>   // 12. picture note close-up
        {
            CloseUI(g);
            Transform n = GameObject.Find("Puzzle_Study_CipherNote_SHWQDPH")?.transform;
            n?.GetComponent<L2Examine>()?.Interact();
        });
        s.Add(() =>   // 13. picture wheel, unsolved
        {
            CloseUI(g);
            Find<L2CipherWheel>()?.Interact();
        });
        s.Add(() =>   // 14. picture wheel, solved
        {
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 6 && !g.ui.PictureRowCorrect(row); i++) g.ui.TurnWheel(row, 1);
            Line("picture_decoded=" + g.Flag("picture_decoded"));
        });
        s.Add(() =>   // 15. password variants
        {
            CloseUI(g);
            L2Workstation ws = Find<L2Workstation>();
            foreach (string p in new[] { "BYTE2019", "byte 2019", " Byte 2019 ", "BYTE2018", "" })
                Line("password \"" + p + "\" -> " + ws.Check(p));
            g.SetInt("terminal_checks", 2);
            g.ui.OpenPassword(ws);
        });
        s.Add(() =>   // 16. stuck helper (shortened to 4 s for the test)
        {
            CloseUI(g);
            L2LevelSession.Instance.stuckSeconds = 4f;
            Line("stuckSeconds set to 4 for the test; take the next step after ~6 s");
        });
        s.Add(() =>
        {
            Line("StuckNudge=" + g.StuckNudge);
            L2LevelSession.Instance.stuckSeconds = 180f;
        });
        return s;
    }

    static System.Collections.IEnumerator CloseLater(L2Openable o)
    {
        yield return new WaitForSeconds(0.9f);
        o.Interact();
        yield return new WaitForSeconds(0.7f);
        Line("closed " + o.name + " (should stay slightly ajar), prompt: " + o.Prompt);
    }

    // ------------------------------------------------------------------ Level 3
    static List<System.Action> Level3Steps(L2Game g)
    {
        var s = new List<System.Action>();
        L3FuseBox fb = Find<L3FuseBox>();
        L3Dial dial = Find<L3Dial>();
        L3SafeKeypad safe = Find<L3SafeKeypad>();
        L3LeverSequence seq = Find<L3LeverSequence>();

        s.Add(() =>   // 1. pegboard rule
        {
            Transform peg = GameObject.Find("Pegboard")?.transform;
            if (peg != null) Look(g, peg.position + Vector3.up * 7f, -peg.forward, 7f);
            Line("pegboard title: " + peg?.Find("Title")?.GetComponent<TMPro.TMP_Text>()?.text);
        });
        s.Add(() =>   // 2. fuse box live display, mid-swap
        {
            if (fb == null) return;
            Look(g, fb.transform.position + Vector3.up * 5f, -fb.transform.forward, 7f);
            Line("display start: " + fb.display.text);
            fb.Take(0); Line("take x -> " + fb.display.text);
            fb.Place(2, L2Items.Fuse2); Line("put in temp -> " + fb.display.text);
        });
        s.Add(() =>   // 3. finish swap + TEST
        {
            fb.Take(1); fb.Place(0, L2Items.Fuse9); Line("y -> x: " + fb.display.text);
            fb.Take(2); fb.Place(1, L2Items.Fuse2); Line("temp -> y: " + fb.display.text);
            fb.Test(); Line("fuse solved=" + fb.Solved);
        });
        s.Add(() =>   // 4. printer legend
        {
            L3Printer pr = Find<L3Printer>();
            if (pr == null) return;
            pr.Interact();
            Look(g, pr.transform.position + Vector3.up * 3f, -pr.transform.forward, 5f);
        });
        s.Add(() =>   // 5. printout close-up
        {
            Transform p = GameObject.Find("Printout")?.transform;
            p?.GetComponent<L2Examine>()?.Interact();
        });
        s.Add(() =>   // 6. fan dial to 3 -> gauge
        {
            CloseUI(g);
            if (dial == null) return;
            Look(g, dial.transform.position, -dial.transform.parent.forward, 6f);
            for (int i = 0; i < 3 && dial.Value != 3; i++) dial.Interact();
            Line("fan=" + dial.Value + " gauge label=" + dial.gaugeLabel?.text + " reading=" + dial.gaugeReading?.text);
        });
        s.Add(() =>   // 7. safe switches correct -> status
        {
            if (safe == null) return;
            Look(g, safe.transform.position, -safe.transform.parent.forward, 7f);
            if (safe.alarmOn.IsOn) safe.alarmOn.Interact();
            if (!safe.hasBadge.IsOn) safe.hasBadge.Interact();
            Line("CanOpen=" + safe.CanOpen + " status=" + safe.statusText?.text);
        });
        s.Add(() =>   // 8. levers step lights
        {
            if (seq == null) return;
            Look(g, seq.transform.position + Vector3.up * 6f, -seq.transform.forward, 8f);
            foreach (L3Lever l in seq.levers) if (l.leverName == "COOLING") l.Interact();
            Line("panel: " + seq.panelText.text);
        });
        s.Add(() =>
        {
            foreach (L3Lever l in seq.levers) if (l.leverName == "BACKUP") l.Interact();
            Line("panel: " + seq.panelText.text);
        });
        s.Add(() =>   // 10. journal
        {
            g.SetFlag("solved_m1"); g.SetFlag("solved_m2");
            L3Manager.Instance.ShowJournal();
        });
        return s;
    }
}
