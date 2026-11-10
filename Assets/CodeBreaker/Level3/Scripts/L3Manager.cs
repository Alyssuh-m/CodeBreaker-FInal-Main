using System.Collections.Generic;
using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Level 3 (Server Room) rules on top of the shared gameplay framework (L2Game + L2UI):
    //  - the 15:00 countdown: the Code Breaker session timer when the Game Session is present,
    //    otherwise its own clock (shown at the top of the screen) with a time-up screen;
    //  - wrong answers: red flash, buzzer, time penalty; hints after 2 wrong tries per station;
    //  - station "solved" flags, keycards, checkpoints (saved each time a keycard is collected);
    //  - objectives and the completion text.
    // Station state lives in L2Game flags / numbers, so it is saved with every checkpoint.
    [DefaultExecutionOrder(-150)]
    public class L3Manager : L2LevelRules
    {
        public static L3Manager Instance { get; private set; }

        [Tooltip("Countdown in seconds (15:00). Used by the level's own clock when there is no Game Session.")]
        public float timeLimit = 900f;
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

            GameFlowManager gm = GameFlowManager.Instance;
            ownClock = gm == null || gm.levelTimer == null;
            ownRemaining = timeLimit;
            if (g.LoadedState != null && g.HasInt("l3_time"))
                SetRemaining(g.GetInt("l3_time", Mathf.RoundToInt(timeLimit)));

            foreach (string id in g.Inventory) knownItems.Add(id);
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
            if (ownClock && !timeUp && !g.Completed)
            {
                ownRemaining -= Time.deltaTime;   // stops while paused (timeScale 0)
                if (ownRemaining <= 0f) { ownRemaining = 0f; TimeUp(); }
            }
            UpdateClockUI();
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
                hint = "Your keycards remember what you earned.";
                return "Answer the central terminal's question.";
            }
            int cards = KeycardsCollected();
            if (cards == 3)
            {
                hint = "Cards inserted: " + inserted + " / 3";
                return "Insert the three keycards into the central terminal.";
            }

            string red = g.IsCollected(L2Items.KeycardRed) ? "<color=#5FD383>RED OK</color>" : "RED: Maintenance (west)";
            string blue = g.IsCollected(L2Items.KeycardBlue) ? "<color=#5FD383>BLUE OK</color>" : "BLUE: Security (east)";
            string green = g.IsCollected(L2Items.KeycardGreen) ? "<color=#5FD383>GREEN OK</color>" : "GREEN: Control (north)";
            hint = red + "  ·  " + blue + "  ·  " + green;
            return "Collect the three keycards (" + cards + " / 3).";
        }

        public override string CompletionTitle => "LEVEL 3 COMPLETE!";
        public override string CompletionBody => "You escaped the Server Room.\nThe exit code was accepted and the lockdown is over.";
        public override string NoNextLevelLabel => "ALL LEVELS CLEARED";
    }
}
