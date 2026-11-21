using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using CodeBreaker.Level2;
using Object = UnityEngine.Object;

namespace CodeBreaker.Level3.EditorTools
{
    // Save / hint / unlock feature: one play-mode test that walks through every scenario with the real
    // menu buttons, the real scene loads and the real save file. The player's save file, score file and
    // the Level 3 "completed" setting are backed up first and put back at the end.
    // Trigger: a file named "run_shutest" in <project>/Level3_Phase2/ while Play mode runs.
    public static partial class Level3EnvironmentPass
    {
        static string DirShu => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SaveHint_Test");
        static ProgressTest progressTest;

        // Edit mode: writes the current save file (and its time) to SaveHint_Test/current_save.txt.
        static void DumpSave()
        {
            Directory.CreateDirectory(DirShu);
            string f = SaveSystem.FilePath;
            string text = File.Exists(f) ? "modified " + File.GetLastWriteTime(f).ToString("o") + "\n" + File.ReadAllText(f) : "NO SAVE FILE";
            File.WriteAllText(Path.Combine(DirShu, "current_save.txt"), text);
        }

        // Keeps a copy of the player's own files the first time a test runs (never overwritten later).
        static void KeepOriginals()
        {
            string dir = Path.Combine(DirShu, "original_files");
            if (Directory.Exists(dir)) return;
            Directory.CreateDirectory(dir);
            if (File.Exists(SaveSystem.FilePath)) File.Copy(SaveSystem.FilePath, Path.Combine(dir, "codebreaker_save.json"));
            if (File.Exists(JsonScoreStore.FilePath)) File.Copy(JsonScoreStore.FilePath, Path.Combine(dir, "codebreaker_scores.json"));
            File.WriteAllText(Path.Combine(dir, "level3_completed_setting.txt"), PlayerPrefs.HasKey("CodeBreaker.Level3.Completed") ? PlayerPrefs.GetInt("CodeBreaker.Level3.Completed").ToString() : "(not set)");
        }

        static void StartProgressTest()
        {
            if (progressTest != null) return;
            Directory.CreateDirectory(DirShu);
            KeepOriginals();
            progressTest = new ProgressTest();
            EditorApplication.update += ProgressTick;
        }

        static void ProgressTick()
        {
            if (progressTest == null || !EditorApplication.isPlaying) { EditorApplication.update -= ProgressTick; progressTest = null; return; }
            if (progressTest.Step()) { EditorApplication.update -= ProgressTick; progressTest = null; }
        }

        class ProgressTest
        {
            readonly StringBuilder r = new StringBuilder();
            IEnumerator run; float waitUntil; int fails, passes;
            readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            int errors; readonly List<string> errorLines = new List<string>();
            string saveBackup, scoreBackup; bool hadSave, hadScore, hadPref; int prefBackup;
            const string Pref = "CodeBreaker.Level3.Completed";

            public ProgressTest() { Application.logMessageReceived += OnLog; run = Run(); }

            void OnLog(string msg, string stack, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (msg.Contains("generators.ai.unity.com") || msg.Contains("UserNotInOrganization")) return;
                errors++; if (errorLines.Count < 15) errorLines.Add(type + ": " + msg.Split('\n')[0]);
            }

            void Ok(string what, bool ok) { r.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what); if (ok) passes++; else fails++; }
            void Say(string s) => r.AppendLine(s);
            void Cap(string n) => ScreenCapture.CaptureScreenshot(Path.Combine(DirShu, n + ".png"));

            public bool Step()
            {
                if (!EditorApplication.isPlaying) return true;
                if (Time.realtimeSinceStartup < waitUntil) return false;
                // runs nested steps (yield return SomeRoutine()) to the end before going on
                if (stack.Count == 0) stack.Push(run);
                try
                {
                    while (stack.Count > 0)
                    {
                        IEnumerator top = stack.Peek();
                        if (!top.MoveNext()) { stack.Pop(); continue; }
                        if (top.Current is IEnumerator inner) { stack.Push(inner); continue; }
                        if (top.Current is float f) waitUntil = Time.realtimeSinceStartup + f;
                        return false;
                    }
                }
                catch (Exception e) { Say("EXCEPTION in the test: " + e.Message + "\n" + e.StackTrace); fails++; }
                return Done();
            }

            bool Done()
            {
                Application.logMessageReceived -= OnLog;
                // put the player's own files back
                try
                {
                    if (hadSave) File.WriteAllText(SaveSystem.FilePath, saveBackup); else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
                    if (hadScore) File.WriteAllText(JsonScoreStore.FilePath, scoreBackup); else if (File.Exists(JsonScoreStore.FilePath)) File.Delete(JsonScoreStore.FilePath);
                    if (hadPref) PlayerPrefs.SetInt(Pref, prefBackup); else PlayerPrefs.DeleteKey(Pref);
                    PlayerPrefs.Save();
                    Say("\nRestored the save file, the score file and the Level 3 setting to what they were before the test.");
                }
                catch (Exception e) { Say("\nCOULD NOT RESTORE the backups: " + e.Message); fails++; }
                Say("\nErrors / exceptions logged by the game during the test: " + errors);
                foreach (string l in errorLines) Say("  " + l);
                Say("\nRESULT: " + passes + " passed, " + fails + " failed");
                File.WriteAllText(Path.Combine(DirShu, "progress_test.txt"), r.ToString());
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Level3_Phase2", "done_run_shutest.txt"), DateTime.Now.ToString("o"));
                return true;
            }

            // ------------------------------------------------------------------ helpers
            static string Active => SceneManager.GetActiveScene().name;
            IEnumerator WaitScene(string scene)
            {
                float end = Time.realtimeSinceStartup + 240f;   // the Editor runs slowly when it is not the front window
                while ((SceneLoader.IsLoading || Active != scene) && Time.realtimeSinceStartup < end) yield return 0.25f;
                if (Active != scene) throw new Exception("Scene " + scene + " did not load within 4 minutes (still in " + Active + ")");
                yield return 1.0f;
            }
            static SaveData Save => SaveSystem.Load();
            static string Raw => File.Exists(SaveSystem.FilePath) ? File.ReadAllText(SaveSystem.FilePath) : "";
            static MainMenuController Menu => Object.FindFirstObjectByType<MainMenuController>();
            static T Priv<T>(object o, string field)
            {
                if (o == null) throw new Exception("Priv: object is null (" + field + ")");
                for (Type t = o.GetType(); t != null; t = t.BaseType)
                {
                    FieldInfo fi = t.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (fi != null) return (T)fi.GetValue(o);
                }
                throw new Exception("Priv: no field " + field + " on " + o.GetType().Name);
            }
            static void Call(object o, string method, params object[] args) =>
                o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, args);
            static string Solved(SaveData d) => d == null ? "-" : string.Join(", ", d.solvedPuzzles);
            static string Levels(SaveData d) => d == null ? "-" : "[" + string.Join(", ", d.completedLevels) + "] unlocked " + d.highestUnlocked;

            List<Button> LevelButtons()
            {
                MainMenuController m = Menu;
                if (m == null) throw new Exception("MainMenuController not found (scene " + Active + ")");
                return Priv<List<Button>>(m, "levelButtons");
            }
            string ButtonText(Button b) { TMP_Text t = b.GetComponentInChildren<TMP_Text>(true); return t != null ? t.text : ""; }

            string HintText(L2Game g)
            {
                TMP_Text title = Priv<TMP_Text>(g.ui, "infoTitle"), body = Priv<TMP_Text>(g.ui, "infoBody");
                return (title != null ? title.text : "") + " | " + (body != null ? body.text : "");
            }

            IEnumerator ToMenu()
            {
                if (Active == "MainMenu") yield break;
                GameFlowManager gm = GameFlowManager.Instance;
                if (gm != null) gm.ExitToHome();
                else SceneLoader.Instance.LoadMainMenu();
                yield return WaitScene("MainMenu");
            }

            IEnumerator OpenLoadPanel()
            {
                Menu.loadButton.onClick.Invoke();
                yield return 0.6f;
            }

            void CloseBriefings(L2Game g)
            {
                foreach (string b in new[] { "brief_living", "brief_bedroom", "brief_kitchen", "brief_study", "brief_l3_main", "brief_l3_maint", "brief_l3_sec", "brief_l3_ctrl" }) g.SetFlag(b);
                if (g.ui != null && g.ui.AnyModalOpen) g.ui.CloseModal();
            }

            // ------------------------------------------------------------------ the test
            IEnumerator Run()
            {
                hadSave = File.Exists(SaveSystem.FilePath); saveBackup = hadSave ? File.ReadAllText(SaveSystem.FilePath) : null;
                hadScore = File.Exists(JsonScoreStore.FilePath); scoreBackup = hadScore ? File.ReadAllText(JsonScoreStore.FilePath) : null;
                hadPref = PlayerPrefs.HasKey(Pref); prefBackup = PlayerPrefs.GetInt(Pref, 0);
                Say("Save / hint / unlock test  (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
                Say("Your save file, score file and the Level 3 'completed' setting were backed up first.\n");

                // ============================================================ 1. older save files
                Say("1. OLDER SAVE FILES (made before this change)");
                File.Delete(SaveSystem.FilePath);
                if (File.Exists(JsonScoreStore.FilePath)) File.Delete(JsonScoreStore.FilePath);
                PlayerPrefs.DeleteKey(Pref);
                var oldState = new L2State { checkpoint = 1, checkpointLabel = "Hall B" };
                oldState.flags.AddRange(new[] { "photo_assembled", "star_shadow_seen", "brief_living" });
                string oldJson = "{\"version\":1,\"sceneName\":\"Level 2\",\"levelName\":\"Level 2\",\"health\":80.0,\"maxHealth\":100.0,\"wrongAttempts\":1,\"totalPlayTime\":95.0,\"savedAt\":\"2026-11-01T10:00:00\",\"checkpointScene\":\"Level 2\",\"checkpointLabel\":\"Hall B\",\"checkpointJson\":" + JsonQuote(JsonUtility.ToJson(oldState)) + "}";
                File.WriteAllText(SaveSystem.FilePath, oldJson);
                SaveData o = Save;
                Ok("Old Level 2 save loads (no new fields in the file)", o != null && o.sceneName == "Level 2");
                Ok("Level 1 counted as completed, Levels 1-2 unlocked: " + Levels(o), o != null && o.completedLevels.Contains("Level 1") && !o.completedLevels.Contains("Level 2") && o.highestUnlocked == 2);
                Ok("Solved puzzles read from the old checkpoint: " + Solved(o), o != null && o.solvedPuzzles.Contains("Level 2/living_photo") && o.solvedPuzzles.Contains("Level 2/living_starlamp") && !o.solvedPuzzles.Contains("Level 2/living_star"));
                Ok("Old checkpoint kept (Hall B)", o != null && SaveSystem.HasCheckpoint(o, "Level 2") && o.checkpointLabel == "Hall B");
                Ok("Loading did not rewrite the file", Raw == oldJson);
                PlayerPrefs.SetInt(Pref, 1);
                File.WriteAllText(SaveSystem.FilePath, oldJson.Replace("\"Level 2\"", "\"Level 3\"").Replace("\"checkpointJson\":" + JsonQuote(JsonUtility.ToJson(oldState)), "\"checkpointJson\":\"\""));
                o = Save;
                Ok("Old Level 3 save + Level 3 completed setting: all 3 completed, all unlocked: " + Levels(o), o != null && o.completedLevels.Count == 3 && o.highestUnlocked == 3);
                PlayerPrefs.DeleteKey(Pref);
                File.WriteAllText(SaveSystem.FilePath, "{ this is not json");
                Ok("Damaged save file: treated as no save, no crash", Save == null);
                File.WriteAllText(SaveSystem.FilePath, "{\"sceneName\":\"Level 9\"}");
                Ok("Save pointing to a missing level: treated as no save", Save == null);
                File.Delete(SaveSystem.FilePath);

                // ============================================================ 2. new game from the menu (no save)
                Say("\n2. NEW GAME (no save yet)");
                if (Active != "MainMenu") { SceneLoader.Instance.LoadMainMenu(); yield return WaitScene("MainMenu"); }
                yield return OpenLoadPanel();
                List<Button> lb = LevelButtons();
                Ok("Load Saved Games: 'No Saved Games Found' + SELECT LEVEL with 3 buttons", Menu.noSavesMessage.activeSelf && lb.Count == 3);
                Ok("No save: only Level 1 unlocked (" + ButtonText(lb[0]).Split('\n')[0] + " / Level 2 " + (lb[1].interactable ? "open" : "locked") + " / Level 3 " + (lb[2].interactable ? "open" : "locked") + ")", lb[0].interactable && !lb[1].interactable && !lb[2].interactable);
                Cap("01_menu_no_save");
                yield return 0.5f;
                Menu.Back();
                Menu.startButton.onClick.Invoke();   // no save: starts straight away
                yield return WaitScene("Level 1");
                SaveData d = Save;
                Ok("Level 1 started, auto-saved: " + Levels(d), d != null && d.sceneName == "Level 1" && d.completedLevels.Count == 0 && d.highestUnlocked == 1 && d.progressVersion == 2);

                // ============================================================ 3. Level 1 rooms
                Say("\n3. LEVEL 1: ROOM PROGRESS, CONTINUE, COMPLETION");
                RoomManager rm = RoomManager.Instance;
                Call(rm, "EnterNextRoom", 0);   // the correct door of Room 1
                yield return 0.5f;
                d = Save;
                Ok("Room 1 cleared -> saved: checkpoint '" + (d != null ? d.checkpointLabel : "") + "', solved " + Solved(d), d != null && d.checkpointLabel == "Room 2" && d.solvedPuzzles.Contains("Level 1/room1") && !d.solvedPuzzles.Contains("Level 1/room2"));
                Ok("Clearing a room does not unlock Level 2", d != null && d.highestUnlocked == 1 && d.completedLevels.Count == 0);
                float hpBefore = GameFlowManager.Instance.playerHealth.CurrentHealth;
                yield return ToMenu();
                yield return OpenLoadPanel();
                string details = Menu.saveSlotDetails.text;
                Ok("Load Saved Games shows the room checkpoint", details.Contains("Room 2") && details.Contains("1 / 3"));
                Cap("02_menu_level1_room2");
                yield return 0.5f;
                Menu.continueButton.onClick.Invoke();
                yield return WaitScene("Level 1");
                rm = RoomManager.Instance;
                Ok("Continue: back in Room 2, Room 1 stays cleared", rm.CurrentRoomIndex == 1 && rm.rooms[0].completed && !rm.rooms[1].completed);
                Ok("Continue: health restored (" + GameFlowManager.Instance.playerHealth.CurrentHealth + ")", Mathf.Approximately(GameFlowManager.Instance.playerHealth.CurrentHealth, hpBefore));
                yield return 1.2f;
                Cap("03_level1_continued_room2");
                yield return 0.5f;   // the screenshot is taken at the end of the frame
                Call(rm, "EnterNextRoom", 1);
                yield return 0.4f;
                Call(rm, "EnterNextRoom", 2);   // last room: the level is complete
                yield return 1.0f;
                d = Save;
                Ok("Level 1 finished -> completed, Level 2 unlocked: " + Levels(d), d != null && d.completedLevels.Contains("Level 1") && d.highestUnlocked == 2);
                Ok("Save now continues at Level 2 (start); Level 1 checkpoint removed", d != null && d.sceneName == "Level 2" && !SaveSystem.HasCheckpoint(d, "Level 1"));
                Ok("All 3 Level 1 puzzles recorded once each", d != null && CountPrefix(d, "Level 1/") == 3);

                // ============================================================ 4. menu after Level 1
                Say("\n4. MENU AFTER LEVEL 1");
                yield return ToMenu();
                yield return OpenLoadPanel();
                lb = LevelButtons();
                details = Menu.saveSlotDetails.text;
                Ok("Slot shows 'Completed Level 1 ... Next Level 2'", details.Contains("Completed  Level 1") && details.Contains("Next  Level 2"));
                Ok("Level 1 COMPLETED, Level 2 START, Level 3 LOCKED", ButtonText(lb[0]).Contains("COMPLETED") && lb[1].interactable && ButtonText(lb[1]).Contains("START") && !lb[2].interactable && ButtonText(lb[2]).Contains("LOCKED"));
                Cap("04_menu_after_level1");
                yield return 0.5f;
                Call(Menu, "OnSelectLevel", "Level 3");   // even calling it directly must not load a locked level
                yield return 2.0f;
                Ok("Locked Level 3 cannot be started", Active == "MainMenu" && !SceneLoader.IsLoading);

                // ============================================================ 5. Level 2: smart hints + save per puzzle
                Say("\n5. LEVEL 2: SMART HINTS, SAVE ON EACH PUZZLE, CONTINUE");
                lb[1].onClick.Invoke();
                yield return WaitScene("Level 2");
                L2Game g = L2Game.Instance; L2LevelSession ls = L2LevelSession.Instance;
                CloseBriefings(g); yield return 0.5f;
                d = Save;
                Ok("Level 2 opened from Select Level, save points to it", d != null && d.sceneName == "Level 2");
                ls.ShowHint(); yield return 0.3f;
                string h = HintText(g);
                Ok("Hint 1 is about the first unsolved puzzle (photo pieces)", h.Contains("LIVING") && h.Contains("photo pieces") && h.Contains("Hint 1 of 2"));
                g.ui.CloseModal();
                int saves0 = g.PuzzleSaves;
                g.SetFlag("photo_assembled");
                yield return 1.0f;
                d = Save;
                Ok("Photo assembled -> saved at once: " + Solved(d), g.PuzzleSaves == saves0 + 1 && d.solvedPuzzles.Contains("Level 2/living_photo"));
                yield return 1.0f;
                Ok("No duplicate save while nothing new is solved", g.PuzzleSaves == saves0 + 1 && CountPrefix(Save, "Level 2/living_photo") == 1);
                ls.ShowHint(); yield return 0.3f;
                h = HintText(g);
                Ok("Next hint skips the solved photo and goes to the clock / TV puzzle", h.Contains("binary") && !h.Contains("Search every drawer"));
                Cap("05_level2_hint_after_photo");
                yield return 0.5f;
                g.ui.CloseModal();
                foreach (string f in new[] { "star_shadow_seen", "star_placed", "exit_living", "moon_seen" }) g.SetFlag(f);
                yield return 1.0f;
                ls.ShowHint(); yield return 0.3f;
                h = HintText(g);
                Ok("Living Room done, UV solved: hint goes to the moon box in the Bedroom", h.Contains("BEDROOM") && h.Contains("music box"));
                g.ui.CloseModal();
                Ok("Solved so far: " + CountPrefix(Save, "Level 2/") + " Level 2 puzzles saved", CountPrefix(Save, "Level 2/") == 5);
                yield return ToMenu();
                yield return OpenLoadPanel();
                Ok("Load Saved Games: Level 2, 5 / 14 puzzles", Menu.saveSlotDetails.text.Contains("5 / 14"));
                Menu.continueButton.onClick.Invoke();
                yield return WaitScene("Level 2");
                g = L2Game.Instance; ls = L2LevelSession.Instance; CloseBriefings(g); yield return 0.5f;
                Ok("Continue: solved puzzles are still solved (photo, star, exit, UV)", g.Flag("photo_assembled") && g.Flag("star_placed") && g.Flag("exit_living") && g.Flag("moon_seen"));
                Ok("Continue: the unsolved moon box is still unsolved", !g.Flag("moonbox_open"));
                ls.ShowHint(); yield return 0.3f;
                Ok("After loading, the hint is still the moon box", HintText(g).Contains("music box"));
                g.ui.CloseModal();
                Ok("Level 2 not completed and Level 3 still locked before the end", !SaveSystem.IsCompleted(Save, "Level 2") && !SaveSystem.IsUnlocked(Save, "Level 3"));
                foreach (string f in new[] { "moonbox_open", "stars_seen", "exit_bedroom", "recipe_complete", "oven_open", "exit_kitchen", "picture_decoded" }) g.SetFlag(f);
                g.SetInt("terminal_checks", 2);
                yield return 1.0f;
                g.CompleteLevel();
                yield return 1.0f;
                d = Save;
                Ok("Level 2 finished -> completed, Level 3 unlocked: " + Levels(d), d.completedLevels.Contains("Level 2") && d.highestUnlocked == 3 && d.sceneName == "Level 3");
                Ok("All 14 Level 2 puzzles recorded once", CountPrefix(d, "Level 2/") == 14);
                ls.ShowHint(); yield return 0.3f;
                Ok("Hint after everything is solved: 'already completed all puzzles' (free)", HintText(g).Contains(L2LevelSession.AllSolvedMessage) && HintText(g).Contains("No score"));
                Cap("06_level2_all_solved_hint");
                yield return 0.5f;
                g.ui.CloseModal();

                // ============================================================ 6. Level 3: hints by wing, checkpoints per level
                Say("\n6. LEVEL 3: HINTS, SAVE PER PUZZLE, ONE CHECKPOINT PER LEVEL");
                yield return ToMenu();
                yield return OpenLoadPanel();
                lb = LevelButtons();
                Ok("Menu: all 3 levels unlocked now", lb[0].interactable && lb[1].interactable && lb[2].interactable);
                lb[2].onClick.Invoke();
                yield return WaitScene("Level 3");
                g = L2Game.Instance; L3Manager M = L3Manager.Instance; CloseBriefings(g); yield return 0.5f;
                M.ShowHint(); yield return 0.3f;
                h = HintText(g);
                Ok("Main hall hint points to the first unfinished wing (Maintenance lockers)", h.Contains("MAINTENANCE") && h.Contains("Next:") && h.Contains("west"));
                Cap("07_level3_hall_hint");
                yield return 0.5f;
                g.ui.CloseModal();
                M.Solve("m1");
                yield return 1.0f;
                d = Save;
                Ok("Laptop solved -> saved at once: " + Solved(d), d.solvedPuzzles.Contains("Level 3/m1") && SaveSystem.HasCheckpoint(d, "Level 3"));
                M.ShowHint(); yield return 0.3f;
                Ok("Next hint: the red locker name tag (m1 skipped)", HintText(g).Contains("red locker") || HintText(g).Contains("name tag"));
                g.ui.CloseModal();
                Vector3 home = g.player.position;
                g.Teleport(new Vector3(40f, home.y, 0f), g.player.rotation);   // into Security
                yield return 0.6f;
                M.ShowHint(); yield return 0.3f;
                Ok("Standing in Security: hint is that wing's first puzzle (camera alarm)", HintText(g).Contains("SECURITY") && HintText(g).Contains("sticky note"));
                g.ui.CloseModal();
                g.Teleport(home, g.player.rotation);
                yield return 0.6f;

                // replay Level 1 from the menu: Level 3's checkpoint must survive
                yield return ToMenu();
                yield return OpenLoadPanel();
                lb = LevelButtons();
                Ok("Menu: Level 3 shows CONTINUE (has a checkpoint)", ButtonText(lb[2]).Contains("CONTINUE"));
                Cap("08_menu_level3_in_progress");
                yield return 0.5f;
                lb[0].onClick.Invoke();
                yield return WaitScene("Level 1");
                d = Save;
                Ok("Replaying Level 1 keeps the Level 3 checkpoint", d.sceneName == "Level 1" && SaveSystem.HasCheckpoint(d, "Level 3") && d.solvedPuzzles.Contains("Level 3/m1"));
                Ok("Replaying Level 1 keeps it completed and everything unlocked", d.completedLevels.Contains("Level 1") && d.highestUnlocked == 3);
                yield return ToMenu();
                yield return OpenLoadPanel();
                LevelButtons()[2].onClick.Invoke();
                yield return WaitScene("Level 3");
                g = L2Game.Instance; M = L3Manager.Instance; CloseBriefings(g); yield return 0.5f;
                Ok("Back in Level 3: the laptop is still solved, the red locker isn't", M.Solved("m1") && !M.Solved("m2"));
                foreach (string s in new[] { "m2", "m3", "gen", "s1", "s2", "s3", "c2", "c3", "term" }) M.Solve(s);
                g.SetInt("fan", 3);
                yield return 1.0f;
                M.ShowHint(); yield return 0.3f;
                Ok("Every Level 3 puzzle solved: 'already completed all puzzles' (no time taken)", HintText(g).Contains(L3Manager.AllSolvedMessage));
                Cap("09_level3_all_solved_hint");
                yield return 0.5f;
                g.ui.CloseModal();
                M.LevelFinished();
                yield return 1.0f;
                d = Save;
                Ok("Level 3 finished -> all 3 levels completed: " + Levels(d), d.completedLevels.Count == 3);
                Ok("All 11 Level 3 puzzles recorded once", CountPrefix(d, "Level 3/") == 11);

                yield return ToMenu();
                yield return OpenLoadPanel();
                Ok("Slot shows 'Completed all 3 levels'", Menu.saveSlotDetails.text.Contains("all 3 levels"));
                Cap("10_menu_all_completed");
                yield return 0.5f;

                // ============================================================ 7. new game with a save
                Say("\n7. NEW GAME WITH AN EXISTING SAVE");
                string before = Raw;
                Menu.Back();
                Menu.startButton.onClick.Invoke();
                yield return 0.8f;
                Ok("Start New Game asks first (confirmation shown, nothing loaded)", Menu.newGameConfirmPanel.activeSelf && Active == "MainMenu");
                Cap("11_new_game_confirm");
                yield return 0.5f;
                Menu.newGameNo.onClick.Invoke();
                yield return 0.5f;
                Ok("Choosing No keeps the save exactly as it was", Raw == before);
                Menu.startButton.onClick.Invoke();
                yield return 0.5f;
                Menu.newGameYes.onClick.Invoke();
                yield return WaitScene("Level 1");
                d = Save;
                Ok("New game: Level 1, nothing completed, only Level 1 unlocked: " + Levels(d), d != null && d.sceneName == "Level 1" && d.completedLevels.Count == 0 && d.highestUnlocked == 1);
                Ok("New game: no old checkpoints or solved puzzles", d != null && d.solvedPuzzles.Count == 0 && d.levelCheckpoints.Count == 0 && string.IsNullOrEmpty(d.checkpointScene));
                yield return ToMenu();
                yield return OpenLoadPanel();
                lb = LevelButtons();
                Ok("Menu after New Game: Level 2 and 3 locked again", lb[0].interactable && !lb[1].interactable && !lb[2].interactable);
                Cap("12_menu_after_new_game");
                yield return 0.5f;
            }

            static int CountPrefix(SaveData d, string prefix)
            {
                int n = 0;
                if (d != null) foreach (string id in d.solvedPuzzles) if (id.StartsWith(prefix)) n++;
                return n;
            }

            static string JsonQuote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
