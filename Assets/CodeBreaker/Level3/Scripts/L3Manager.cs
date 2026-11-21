using System.Collections.Generic;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Level 3 (Server Room) rules on top of the shared gameplay framework (L2Game + L2UI):
    //  - the 40:00 countdown: the Code Breaker session timer when the Game Session is present,
    //    otherwise its own clock (shown at the top of the screen) with a time-up screen;
    //  - wrong answers: red flash, buzzer, time penalty; hints after 2 wrong tries per station;
    //  - station "solved" flags, keycards, checkpoints (saved each time a keycard is collected);
    //  - objectives and the completion text.
    // Station state lives in L2Game flags / numbers, so it is saved with every checkpoint.
    [DefaultExecutionOrder(-150)]
    public class L3Manager : L2LevelRules
    {
        public static L3Manager Instance { get; private set; }

        [Tooltip("Countdown in seconds (40:00, set from LevelTimer). Used by the level's own clock when there is no Game Session.")]
        public float timeLimit = 2400f;
        public float warningTime = 60f;
        public int hintAfterWrong = 2;

        [Tooltip("Checkpoints saved after the 1st, 2nd and 3rd keycard (in that order)")]
        public L2Checkpoint[] keycardCheckpoints = new L2Checkpoint[0];

        [Header("Sounds (optional)")]
        public AudioClip wrongClip;
        public AudioClip correctClip;
        public AudioClip horrorClip;
        public AudioSource ambience;

        public const string PrefCompleted = "CodeBreaker.Level3.Completed";

        L2Game G => L2Game.Instance;
        bool ownClock;
        float ownRemaining;
        bool timeUp;
        readonly HashSet<string> knownItems = new HashSet<string>();
        bool started;
        float objectiveCheck;
        bool notesForgotten;

        public bool TimeIsUp => timeUp;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (G != null) { G.Saving -= OnSaving; G.Changed -= OnChanged; }
        }

        void Start()
        {
            L2Game g = G;
            if (g == null) { Debug.LogError("[Level 3] L2Game is missing."); return; }
            g.rules = this;
            g.Saving += OnSaving;
            g.Changed += OnChanged;

            if (LevelTimer.TryFixedLimit(gameObject.scene.name, out float fixedLimit)) timeLimit = fixedLimit;   // 40:00
            GameFlowManager gm = GameFlowManager.Instance;
            ownClock = gm == null || gm.levelTimer == null;
            ownRemaining = timeLimit;
            if (g.LoadedState != null && g.HasInt("l3_time"))
                SetRemaining(g.GetInt("l3_time", Mathf.RoundToInt(timeLimit)));

            foreach (string id in g.Inventory) knownItems.Add(id);
            if (g.ui != null) g.ui.AddPauseButton("HINT  (H)", () => { g.ui.Resume(); ShowHint(); });
            if (g.ui != null) g.ui.AddPauseButton("NOTES  (J)", () => { g.ui.Resume(); ShowJournal(); });
            started = true;
            g.RefreshObjective(false);
            UpdateClockUI();
        }

        // ------------------------------------------------------------------ clock
        public float Remaining
        {
            get
            {
                GameFlowManager gm = GameFlowManager.Instance;
                if (!ownClock && gm != null && gm.levelTimer != null) return gm.levelTimer.RemainingTime;
                return ownRemaining;
            }
        }

        void SetRemaining(float s)
        {
            GameFlowManager gm = GameFlowManager.Instance;
            if (!ownClock && gm != null && gm.levelTimer != null) gm.levelTimer.SetRemainingTime(s);
            else ownRemaining = Mathf.Clamp(s, 0f, timeLimit);
        }

        void Update()
        {
            L2Game g = G;
            if (g == null || !started) return;
            // Session Game Over (time ran out on the session clock): the attempt is over, so its notes go.
            GameFlowManager gm = GameFlowManager.Instance;
            if (!notesForgotten && gm != null && gm.IsGameOver) { notesForgotten = true; g.ForgetNotes(); }
            if (!timeUp && !g.Completed) TrackRamosLockers(g);
            if (ownClock && !timeUp && !g.Completed)
            {
                ownRemaining -= Time.deltaTime;   // stops while paused (timeScale 0)
                if (ownRemaining <= 0f) { ownRemaining = 0f; TimeUp(); }
            }
            UpdateClockUI();

            if (!timeUp && !g.Completed && !g.Paused && !g.ModalOpen && !g.InteractionsBlocked && (g.ui == null || !g.ui.IsTyping))
            {
                UpdateBriefing(g);
                objectiveCheck -= Time.deltaTime;
                if (objectiveCheck <= 0f) { objectiveCheck = 0.5f; g.RefreshObjective(); }   // follows the wing you are in
                if (Input.GetKeyDown(KeyCode.J)) ShowJournal();
                else if (Input.GetKeyDown(KeyCode.H)) ShowHint();
            }
        }

        // ------------------------------------------------------------------ notes journal (J)
        // Every station result is written down once it is solved, so nothing has to be remembered or
        // re-checked. Notes that were read are copied below. Saved automatically (it is built from flags).
        // Built from this playthrough's puzzle values (Phase 8: L3Puzzle.Current; the original values when not randomised).
        static (string station, string text)[] JournalLines
        {
            get
            {
                L3PuzzleSet p = L3Puzzle.Current;
                return new (string, string)[]
                {
                    ("m1",   "Laptop:  locker2 = " + p.LaptopAnswer),
                    ("m2",   "Red locker:  fanSpeed = " + p.fanSpeed + "  (the only valid name)"),
                    ("m3",   "Fuse box:  x = fuse " + p.fuseX),
                    ("gen",  "Generator:  start code " + p.GeneratorCode + "  ->  RED keycard  (m = " + p.M + ")"),
                    ("s1",   "Camera wall:  CAM " + p.cam),
                    ("s2",   "Symbol cabinet:  " + p.LineSolved(0) + ",  " + p.LineSolved(1) + ",  " + p.LineSolved(2)),
                    ("s3",   "Safe:  PIN " + p.Pin + "  ->  BLUE keycard  (s = " + p.S + ")"),
                    ("c2",   "Console:  online = " + p.Online),
                    ("c3",   "Reboot:  fan 3, COOLING -> BACKUP -> REBOOT  ->  GREEN keycard  (c = " + p.C + ")"),
                };
            }
        }

        public void ShowJournal()
        {
            L2Game g = G;
            if (g == null || g.ui == null) return;
            var sb = new System.Text.StringBuilder();
            int n = 0;
            foreach (var line in JournalLines)
                if (Solved(line.station)) { sb.Append(line.text).Append("\n"); n++; }
            if (n > 0) sb.Insert(0, "<color=#E8C547>Station results</color>\n").Append("\n");

            if (!Solved("m1") && RamosLockersSeen(g) > 0)
            {
                sb.Append("<color=#E8C547>Ramos's lockers</color>\n").Append(RamosLockerLine(g)).Append("\n\n");
                n++;
            }
            L2Examine.AppendReadNotes(g, sb, ref n);
            if (n == 0) sb.Append("Nothing yet. Station results and notes you read are written here automatically.");
            if (g.sfx != null) g.sfx.Click();
            g.ui.ShowInfo("NOTES  (J)", sb.ToString().TrimEnd());
        }

        // ------------------------------------------------------------------ Ramos's lockers (Maintenance, job 1)
        // Each locker is one of Ramos's variables: the name is on the door, the value on the card inside.
        // Opening a locker writes "lockerN = value" into the notes and onto his laptop screen. The flags
        // use the notes prefix, so a failed attempt forgets them like any other note.
        public static readonly string[] RamosLockerNames = { "locker1", "locker2", "locker3" };
        public static readonly int[] RamosLockerValues = { 8, 2, 5 };   // the cards in the lockers (set each run by L3PuzzleValues)
        static string LockerFlag(int i) => L2Examine.NoteFlagPrefix + "ramos_" + RamosLockerNames[i];

        L2Openable[] ramosLockers;
        float lockerCheck;

        void TrackRamosLockers(L2Game g)
        {
            lockerCheck -= Time.deltaTime;
            if (lockerCheck > 0f) return;
            lockerCheck = 0.25f;
            if (ramosLockers == null)
            {
                ramosLockers = new L2Openable[RamosLockerNames.Length];
                foreach (L2Openable o in FindObjectsByType<L2Openable>(FindObjectsInactive.Include))
                    for (int i = 0; i < RamosLockerNames.Length; i++)
                        if (o.label == RamosLockerNames[i]) ramosLockers[i] = o;
            }
            for (int i = 0; i < ramosLockers.Length; i++)
            {
                if (ramosLockers[i] == null || !ramosLockers[i].IsOpen || g.Flag(LockerFlag(i))) continue;
                g.SetFlag(LockerFlag(i));
                if (!Solved("m1"))
                {
                    int seen = RamosLockersSeen(g);
                    g.Notify(RamosLockerNames[i] + "'s card:  " + RamosLockerNames[i] + " = " + RamosLockerValues[i] +
                             (seen < RamosLockerNames.Length ? "   (" + seen + " of 3 lockers, saved in notes J)" : "   (all 3 found: try his laptop)"),
                             L2NoteKind.Item);
                    g.RefreshObjective();
                }
            }
        }

        public int RamosLockersSeen(L2Game g)
        {
            int n = 0;
            for (int i = 0; i < RamosLockerNames.Length; i++) if (g != null && g.Flag(LockerFlag(i))) n++;
            return n;
        }

        // "locker1 = 8     locker2 = ?     locker3 = ?"  (? = not opened yet in this playthrough)
        public string RamosLockerLine(L2Game g)
        {
            var parts = new List<string>();
            for (int i = 0; i < RamosLockerNames.Length; i++)
                parts.Add(RamosLockerNames[i] + " = " + (g != null && g.Flag(LockerFlag(i)) ? RamosLockerValues[i].ToString() : "?"));
            return string.Join("     ", parts);
        }

        // Keypad text can include {lockers}: replaced with the locker values found so far.
        public string ExpandCode(string code)
        {
            if (string.IsNullOrEmpty(code) || !code.Contains("{lockers}")) return code;
            L2Game g = G;
            string line = RamosLockersSeen(g) == 0
                ? "<color=#9A918A>(no lockers opened yet)</color>"
                : RamosLockerLine(g);
            return code.Replace("{lockers}", line);
        }

        void UpdateClockUI()
        {
            L2Game g = G;
            if (g == null || g.ui == null) return;
            if (!ownClock) { g.ui.SetClock(null, false); return; }
            bool warn = ownRemaining <= warningTime;
            float pulse = warn && !timeUp && !g.Completed ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.1f : 1f;
            g.ui.SetClock(LevelTimer.Format(ownRemaining), warn, pulse);
        }

        void TimeUp()
        {
            if (timeUp) return;
            timeUp = true;
            L2Game g = G;
            if (g == null) return;
            g.SetBlocked(true);
            if (g.FlashlightOn) g.SetFlashlight(false, false);
            if (g.sfx != null) g.sfx.Failure();
            notesForgotten = true;
            g.ForgetNotes();   // this attempt is over: the next one starts with an empty Notes journal
            if (g.ui != null) g.ui.ShowTimeUp("The lockdown sealed the Server Room for good.\nTry again from your last checkpoint, or restart Level 3.");
        }

        // Wrong answer: red flash, buzzer and a time penalty.
        public void Penalty(float seconds)
        {
            L2Game g = G;
            if (g == null || timeUp || g.Completed) return;
            if (seconds > 0f)
            {
                SetRemaining(Remaining - seconds);
                if (ownClock && ownRemaining <= 0f) TimeUp();
            }
            if (g.ui != null)
            {
                g.ui.FlashRed();
                if (seconds > 0f) g.ui.ShowPenalty("-" + Mathf.RoundToInt(seconds) + "s");
            }
            if (g.sfx != null)
            {
                g.sfx.Failure();
                if (wrongClip != null) g.sfx.Play(wrongClip, 0.6f);
            }
        }

        public void Good()
        {
            L2Game g = G;
            if (g == null || g.sfx == null) return;
            g.sfx.Success();
            if (correctClip != null) g.sfx.Play(correctClip, 0.6f);
        }

        // ------------------------------------------------------------------ stations
        public bool Solved(string station) => G != null && G.Flag("solved_" + station);

        public void Solve(string station)
        {
            L2Game g = G;
            if (g == null || Solved(station)) return;
            g.SetFlag("solved_" + station);
            g.RefreshObjective();
        }

        public int WrongCount(string station) => G != null ? G.GetInt("wrong_" + station, 0) : 0;

        public int AddWrong(string station)
        {
            if (G == null) return 0;
            int n = WrongCount(station) + 1;
            G.SetInt("wrong_" + station, n);
            return n;
        }

        public bool HintReady(string station) => WrongCount(station) >= hintAfterWrong;

        public string HintFor(string station, string hint) => HintReady(station) && !string.IsNullOrEmpty(hint) ? hint : null;

        // Shows the hint as a notification once it is unlocked (for stations without a keypad window).
        public void ShowHintIfReady(string station, string hint)
        {
            string h = HintFor(station, hint);
            if (h != null && G != null) G.Notify("HINT: " + h, L2NoteKind.Item);
        }

        // ------------------------------------------------------------------ keycards & checkpoints
        public static readonly string[] Keycards = { L2Items.KeycardRed, L2Items.KeycardBlue, L2Items.KeycardGreen };

        public int KeycardsCollected()
        {
            int n = 0;
            if (G != null) foreach (string k in Keycards) if (G.IsCollected(k)) n++;
            return n;
        }

        void OnChanged()
        {
            L2Game g = G;
            if (g == null || !started) return;

            // Auto-hold a freshly picked up name tag or the flashlight, so it can be used straight away.
            string newest = null;
            foreach (string id in g.Inventory)
                if (knownItems.Add(id)) newest = id;
            knownItems.RemoveWhere(id => !g.HasItem(id));
            if (newest != null && (newest.StartsWith("tag_") || newest == L2Items.Flashlight) && g.Held != newest)
                g.Equip(newest, true);

            int cards = KeycardsCollected();
            if (cards > g.CheckpointIndex && cards >= 1 && cards <= keycardCheckpoints.Length && keycardCheckpoints[cards - 1] != null)
                g.ReachCheckpoint(keycardCheckpoints[cards - 1]);
        }

        void OnSaving()
        {
            if (G != null) G.SetInt("l3_time", Mathf.CeilToInt(Remaining));
        }

        public void LevelFinished()
        {
            PlayerPrefs.SetInt(PrefCompleted, 1);
            PlayerPrefs.Save();
            if (G != null) G.CompleteLevel();
        }

        // ------------------------------------------------------------------ objectives & completion
        public override string Objective(L2Game g, out string hint)
        {
            hint = "";
            if (g.Completed || g.Flag("level_complete")) return "Level 3 complete!";
            if (Solved("term")) return "The exit is open. Get out!";

            int inserted = 0;
            foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) if (g.Flag(c)) inserted++;
            if (inserted == 3)
            {
                hint = "Each keycard filled in the value its wing earned. Check the if, then do the math.";
                return "Answer the central terminal's question.";
            }
            int cards = KeycardsCollected();
            if (cards == 3)
            {
                hint = "Cards inserted: " + inserted + " / 3. The slots are on the central terminal desk.";
                return "Insert the three keycards into the central terminal.";
            }

            // inside a wing whose keycard isn't collected yet: that wing's next step
            Step st = CurrentStep(g);
            if (st != null)
            {
                hint = st.hint;
                return st.objective;
            }

            string red = g.IsCollected(L2Items.KeycardRed) ? "<color=#5FD383>RED OK</color>" : "RED: Maintenance (west)";
            string blue = g.IsCollected(L2Items.KeycardBlue) ? "<color=#5FD383>BLUE OK</color>" : "BLUE: Security (east)";
            string green = g.IsCollected(L2Items.KeycardGreen) ? "<color=#5FD383>GREEN OK</color>" : "GREEN: Control (north)";
            hint = red + "  ·  " + blue + "  ·  " + green;
            if (!g.HasItem(L2Items.Flashlight) && !g.IsCollected(L2Items.Flashlight)) hint += "\nTake the flashlight from the terminal desk first.";
            return "Collect the three keycards (" + cards + " / 3).";
        }

        // ------------------------------------------------------------------ wings & steps
        // The level is split by position: Maintenance west of the main hall, Security east, Control north.
        public string WingOf(Vector3 p)
        {
            if (p.x < -22.5f) return "maint";
            if (p.x > 22.5f) return "sec";
            if (p.z > 22.5f) return "ctrl";
            return "main";
        }

        public string PlayerWing()
        {
            L2Game g = G;
            if (g == null || g.player == null) return "main";
            return WingOf(g.player.position);
        }

        public class Step
        {
            public string key, objective, hint;
            public string[] hints;   // H: where to look -> how it works -> (almost) the answer
            public Step(string key, string objective, string hint, params string[] hints) { this.key = key; this.objective = objective; this.hint = hint; this.hints = hints; }
        }

        // The next unsolved step of the wing the player is standing in (null in the main hall or when
        // that wing's keycard is already collected).
        public Step CurrentStep(L2Game g)
        {
            string wing = PlayerWing();
            if (wing == "maint" && !g.IsCollected(L2Items.KeycardRed)) return MaintStep(g);
            if (wing == "sec" && !g.IsCollected(L2Items.KeycardBlue)) return SecStep(g);
            if (wing == "ctrl" && !g.IsCollected(L2Items.KeycardGreen)) return CtrlStep(g);
            return null;
        }

        Step MaintStep(L2Game g)
        {
            if (!Solved("m1") && RamosLockersSeen(g) < RamosLockerNames.Length)
                return new Step("m1_lockers", "Open Ramos's three lockers (" + RamosLockersSeen(g) + "/3).",
                    "They stand on the west wall. The name on each door is a variable; the card inside is the number it holds.",
                    "Start at the three grey lockers on the west wall: locker1, locker2, locker3. Open each one and read the card inside. Every number you find is saved in your notes (J).",
                    "Ramos used his lockers as variables. The door gives the NAME (locker1), the card gives its VALUE. His laptop's code uses those same names.",
                    "Once all three are open, use the laptop on the workbench. Its screen lists the values you found next to his code.");
            if (!Solved("m1"))
                return new Step("m1", "Answer Ramos's laptop on the workbench.",
                    "His code uses the locker names. Swap each name for its number, then run the lines from top to bottom.",
                    "Read the laptop's code one line at a time, top to bottom. Your locker values are listed under the code.",
                    "'locker2 = locker1' means: put a COPY of locker1's number into locker2. Only the locker on the LEFT of '=' changes. Write all three lockers down and update them after each line.",
                    "Copying a value does not link the two lockers. After line 1, locker2 has its own number; line 2 changes locker1 only. Type what locker2 holds at the very end.");
            if (!Solved("m2"))
                return new Step("m2", "Open the red locker with a valid name tag.",
                    "Take a tag from the pegboard and use it on the red locker's reader. Only one name is legal in C#.",
                    "The pegboard title gives the rules: no starting digit, no spaces, not a C# keyword like class.",
                    "2fast starts with a digit, my var has a space, class is a keyword. Hold the remaining tag and use it on the NAME TAG READER.",
                    "fanSpeed is the only valid name. Use that tag on the reader. Remember its value for later.");
            if (!Solved("m3"))
                return new Step("m3", "Swap the fuses the way Ramos's log says, then press TEST.",
                    "Ramos's log page (inside the red locker) is a 3-line swap. You can carry one fuse at a time; temp is the spare socket.",
                    "Read Ramos's log page in the red locker: temp = x;  x = y;  y = temp;  Each line moves one fuse.",
                    "Take the fuse out of x and put it in temp. Then move y's fuse into x, then the fuse in temp into y.",
                    "When it's done, x holds the fuse that started in y, y holds the fuse that started in x, and temp is empty. Press TEST.");
            if (!Solved("gen"))
                return new Step("gen", "Start the backup generator.",
                    "Read the note taped to the generator. Every result you need is in your notes (J).",
                    "The start code has three digits, one from each of Ramos's jobs. Read the note taped to the generator.",
                    "Digit 1: what the laptop said locker2 kept. Digit 2: the value on the valid name tag. Digit 3: the fuse now in x.",
                    "Laptop answer, then the fanSpeed value, then the fuse in socket x, typed in that order.");
            return new Step("red", "Take the RED keycard from the printer.",
                "The printer next to the generator printed it.",
                "The printer next to the generator has printed the RED keycard. Pick it up.");
        }

        Step SecStep(L2Game g)
        {
            if (!Solved("s1"))
                return new Step("s1", "Find the camera that set off the alarm.",
                    "Read the sticky note on the guard desk, compare it with every camera feed, then press that camera's button.",
                    "The sticky note on the guard desk is the alarm rule. Each feed shows its motion value and whether its door is OPEN or CLOSED.",
                    "&& means BOTH sides must be true: motion must be above 3 AND the door must be OPEN.",
                    "Only one feed has motion above 3 and an OPEN door. Press the button with that camera's number. Remember the number.");
            if (!g.Flag("sec_printed"))
                return new Step("print", "Print the last shift log.",
                    "Use the printer on the south wall.",
                    "Use the shift log printer on the south wall and read the printout.");
            if (!Solved("s2"))
                return new Step("s2", "Open the security cabinet with the missing operators.",
                    "Three lines of the shift log are missing their operator. Enter the three symbols on the cabinet keypad, in order.",
                    "Three lines on the shift log are missing their operator (+  -  *  /  %). Find the one that makes each line true.",
                    ModHint(L3Puzzle.Current),
                    LinesHint(L3Puzzle.Current));
            if (!Solved("s3"))
                return new Step("s3", "Open the safe.",
                    "Set the switches so the rule card's if is true, then enter the PIN from the note. cam = the camera you flagged.",
                    "The cabinet holds a switch rule card and a PIN note. Both are needed for the safe on the south wall.",
                    "|| needs only ONE side true, ! flips true to false. So: isGuard or hasBadge ON, and alarmOn OFF.",
                    "PIN = cam * 10 + (" + L3Puzzle.Current.ModA + " % " + L3Puzzle.Current.ModB + "). cam is the number of the camera you flagged at the camera wall, and " + L3Puzzle.Current.ModA + " % " + L3Puzzle.Current.ModB + " is the leftover from the shift log.");
            return new Step("blue", "Take the BLUE keycard from the safe.",
                "The safe is open. The keycard is inside.",
                "The safe is open. Take the BLUE keycard from inside.");
        }

        static string ModHint(L3PuzzleSet p) =>
            "% gives what is LEFT OVER after dividing: " + p.ModA + " % " + p.ModB + " = " + p.ModR + " because " + p.ModA + " = " + (p.ModA / p.ModB) + " x " + p.ModB + " + " + p.ModR + ".";

        static string LinesHint(L3PuzzleSet p)
        {
            var parts = new List<string>();
            for (int i = 0; i < 3; i++)
                parts.Add(p.Line(i) + " needs " + (p.ops[i] == '%' ? "%" : p.ops[i] == '*' ? "a multiply" : "a divide"));
            return string.Join(". ", parts) + ". Enter them in that order.";
        }

        Step CtrlStep(L2Game g)
        {
            int fan = g.GetInt("fan", 1);
            if (!Solved("c2"))
                return new Step("c2", "Count the servers that are online.",
                    "Monitor 2's code counts racks with a GREEN light. The racks stand in the main server hall.",
                    "Read Monitor 2 on the control desk. It counts the server racks whose status light is GREEN.",
                    "The racks are in the main server hall (the big room). Walk along them and count only the green lights.",
                    "Every rack has one light on top: count the green ones, skip the red ones, then enter the total on the console keypad.");
            if (fan != 3)
                return new Step("fan", "Set the cooling fan for the current temperature.",
                    "Read the gauge on the cooling unit and Monitor 1. Then turn the fan dial.",
                    "Monitor 1 is an if / else-if chain. The temperature is on the gauge of the cooling unit.",
                    "An if / else-if chain stops at the FIRST condition that is true. Compare the gauge reading with 80 first.",
                    "The gauge is above 80, so the very first line applies. Turn the dial to that fan value.");
            if (!Solved("c3"))
                return new Step("c3", "Reboot the system with the emergency levers.",
                    "Follow the REBOOT PROTOCOL clipboard on the desk. A wrong order resets the levers.",
                    "The clipboard on the control desk is the reboot protocol. Each step has a condition. Check it.",
                    "Step 1 applies because the temperature is above 80. Step 2 applies because fewer than 6 servers are online. REBOOT is always last.",
                    "Pull COOLING first, then BACKUP, then REBOOT.");
            return new Step("green", "Take the GREEN keycard from the console.",
                "The keycard printer on the control desk printed it.",
                "The keycard printer on the control desk has printed the GREEN keycard. Pick it up.");
        }

        // ------------------------------------------------------------------ H: hints
        // Up to three hints for the current step, each a little more direct. Every new hint costs time.
        public float hintTimeCost = 15f;

        static readonly string[] TerminalHints =
        {
            "Each keycard put its value into the terminal's code: m, s and c.",
            "Check the if first: is m greater than s, and is c not zero? Both must be true.",
            "The condition is true, so exitCode = m * 10 + s. Work it out with the values on the screen.",
        };

        static readonly string[] InsertHints =
        {
            "All three keycards are yours. The central terminal stands in the middle of the main hall: insert each keycard into its slot.",
        };

        // The first wing (Maintenance, Security, Control) whose keycard is not collected yet, and its next step.
        public Step NextWingStep(L2Game g, out string wing)
        {
            wing = null;
            if (!g.IsCollected(L2Items.KeycardRed)) { wing = "maint"; return MaintStep(g); }
            if (!g.IsCollected(L2Items.KeycardBlue)) { wing = "sec"; return SecStep(g); }
            if (!g.IsCollected(L2Items.KeycardGreen)) { wing = "ctrl"; return CtrlStep(g); }
            return null;
        }

        static string WingWay(string wing) =>
            wing == "maint" ? "the Maintenance wing (west)" : wing == "sec" ? "the Security wing (east)" : wing == "ctrl" ? "the Control wing (north)" : "the main hall";

        public const string AllSolvedMessage = "You have already completed all puzzles in this level!";

        // Smart hints: always about the first unsolved puzzle.
        //  - all three keycards inserted: the central terminal;
        //  - standing in a wing whose keycard is still missing: that wing's next step;
        //  - anywhere else (main hall, or a finished wing): the next step of the first unfinished wing;
        //  - all keycards collected: insert them;  everything solved: a free "all done" message.
        // Each new hint for a step costs time; repeating the last one is free.
        public void ShowHint()
        {
            L2Game g = G;
            if (g == null || g.ui == null || timeUp) return;
            if (g.Completed || Solved("term"))
            {
                if (g.sfx != null) g.sfx.Click();
                g.ui.ShowInfo("HINT", AllSolvedMessage + "\n\n<color=#9A918A>No time was taken.</color>");
                return;
            }
            string key; string[] list; string title; string lead = "";
            int inserted = 0;
            foreach (string c in new[] { "card_red_in", "card_blue_in", "card_green_in" }) if (g.Flag(c)) inserted++;
            Step st = CurrentStep(g);
            if (inserted == 3) { key = "term"; list = TerminalHints; title = "CENTRAL TERMINAL"; }
            else if (st != null) { key = st.key; list = st.hints; title = WingTitle(PlayerWing()); }
            else if ((st = NextWingStep(g, out string wing)) != null)
            {
                key = st.key; list = st.hints; title = WingTitle(wing);
                lead = "<color=#E8C547>Next: " + st.objective + " Go to " + WingWay(wing) + ".</color>\n";
                if (!g.HasItem(L2Items.Flashlight) && !g.IsCollected(L2Items.Flashlight))
                    lead += "<color=#E8C547>Take the flashlight from the terminal desk first.</color>\n";
                lead += "\n";
            }
            else { key = "insert"; list = InsertHints; title = "CENTRAL TERMINAL"; }

            int used = g.GetInt("l3hint_" + key, 0);
            int index; string cost;
            if (used < list.Length)
            {
                index = used;
                g.SetInt("l3hint_" + key, used + 1);
                g.SetInt("l3hints_total", g.GetInt("l3hints_total", 0) + 1);
                if (hintTimeCost > 0f)
                {
                    SetRemaining(Remaining - hintTimeCost);
                    if (ownClock && ownRemaining <= 0f) { TimeUp(); return; }
                }
                cost = "<color=#9A918A>Hint " + (index + 1) + " of " + list.Length + "  ·  -" + Mathf.RoundToInt(hintTimeCost) + " s</color>";
            }
            else
            {
                index = list.Length - 1;
                cost = "<color=#9A918A>No more hints for this step (shown again for free).</color>";
            }
            if (g.sfx != null) g.sfx.Click();
            g.ui.ShowInfo("HINT  ·  " + title, lead + list[index] + "\n\n" + cost);
        }

        static string WingTitle(string wing) => wing == "maint" ? "MAINTENANCE" : wing == "sec" ? "SECURITY" : wing == "ctrl" ? "CONTROL" : "SERVER ROOM";

        // ------------------------------------------------------------------ wing briefings
        static readonly (string wing, string title, string text)[] Briefings =
        {
            ("main", "LOCKDOWN",
             "The server room sealed itself the moment you stepped in. The exit only opens for the central terminal.\n\n" +
             "It wants three keycards:\n  <color=#FF5050>RED</color>  from Maintenance (west)\n  <color=#4F8BFF>BLUE</color>  from Security (east)\n  <color=#5FD383>GREEN</color>  from Control (north)\n\n" +
             "Take the flashlight from the terminal desk. Some wings have no power.\n\n<color=#9A918A>H = hint (costs time)   ·   J = notes</color>"),
            ("maint", "MAINTENANCE",
             "Ramos's workshop. He never finished his last three jobs: his laptop, the red locker and the fuse box.\n\nFinish them, and the backup generator will start. The power is out in here.\n\n<color=#9A918A>Start with his three lockers on the west wall. His laptop asks about them.</color>"),
            ("sec", "SECURITY",
             "The guard post. Something set off the alarm before the lockdown.\n\nFind which camera saw it. The rest of the guard's paperwork leads to the safe."),
            ("ctrl", "CONTROL",
             "The control room. The main system has crashed and the servers are overheating.\n\nCool them, count what's still online, then reboot. The monitors and the clipboard on the desk explain how."),
        };

        float briefingCheck;
        string pendingWing;
        float pendingDelay;

        void UpdateBriefing(L2Game g)
        {
            briefingCheck -= Time.deltaTime;
            if (briefingCheck > 0f) return;
            briefingCheck = 0.4f;
            if (g.ui == null || g.ModalOpen || g.Paused || g.Completed || timeUp || SceneLoader.IsLoading) return;
            string wing = PlayerWing();
            if (g.Flag("brief_l3_" + wing)) { pendingWing = null; return; }
            if (pendingWing != wing) { pendingWing = wing; pendingDelay = wing == "main" ? 2.0f : 0.8f; return; }
            pendingDelay -= 0.4f;
            if (pendingDelay > 0f) return;
            foreach (var b in Briefings)
                if (b.wing == wing)
                {
                    g.SetFlag("brief_l3_" + wing);
                    if (g.sfx != null) g.sfx.Click();
                    g.ui.ShowInfo(b.title, b.text);
                    break;
                }
        }

        public override string CompletionTitle => "LEVEL 3 COMPLETE!";
        public override string CompletionBody => "You escaped the Server Room.\nThe exit code was accepted and the lockdown is over.";
        public override string NoNextLevelLabel => "ALL LEVELS CLEARED";
    }
}
