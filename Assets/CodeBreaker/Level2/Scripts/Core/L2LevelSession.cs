using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Level 2 extras on top of L2Game:
    //  - countdown: uses the Code Breaker session timer when the Game Session is present (Level 2 =
    //    timeLimit), otherwise its own clock at the top of the screen. Time up -> Retry / Quit screen.
    //  - hints (H, or HINT in the pause menu): 3 hints per room, each more direct; each one costs score.
    //  - notes journal (J): the text of every note the player has read (memory aid, never gates anything).
    //  - score on completion = seconds left x pointsPerSecond - hints x hintPenalty, saved via ScoreStores.
    [DefaultExecutionOrder(-100)]
    public class L2LevelSession : MonoBehaviour
    {
        public static L2LevelSession Instance { get; private set; }

        [Header("Timer")]
        [Tooltip("Level 2 time limit in seconds (20:00 = 1200). With the Game Session present its timer is used (and capped to this).")]
        public float timeLimit = 1200f;
        public float warningTime = 60f;

        [Header("Score")]
        public int pointsPerSecond = 10;
        public int hintPenalty = 250;
        public int levelNumber = 2;

        [Header("Hints per room (each list gets more direct)")]
        [TextArea(2, 4)] public string[] livingHints =
        {
            "The torn photo and the note by the door point to the same two values: the clock and the TV channel.",
            "Binary place values are 8 4 2 1. 1000 = 8 and 0011 = 2 + 1 = 3. Set the wall clock to 8 and put the TV on channel 3 (the remote needs batteries).",
            "When the star lamp shines, put the star figurine into the star slot on the bookshelf, then take the key from the blue book.",
        };
        [TextArea(2, 4)] public string[] bedroomHints =
        {
            "The diary is a little program: find the moves, count the stars, then open the right drawer.",
            "Shine the UV flashlight on the mirror. The music box shows the moves: UP, RIGHT, RIGHT, DOWN opens the moon box. Then switch the lights off and count the stars on the ceiling.",
            "stars = 5 and arrays start at 0, so open drawer[5 - 1] = drawer[4]: the TOP drawer of the jewellery box.",
        };
        [TextArea(2, 4)] public string[] kitchenHints =
        {
            "Read Grandma's recipe card: one ingredient is missing. Which one comes in a carton of 6?",
            "Lift each ingredient to see the binary number underneath: 100 = 4, 1 = 1, 110 = 6, 10 = 2.",
            "Type the numbers in recipe order on the oven timer: 4 1 6 2.",
        };
        [TextArea(2, 4)] public string[] studyHints =
        {
            "The terminal asks for values you learned earlier: the TV channel (Living Room) and the number of stars (Bedroom).",
            "Use the cipher wheel: shift each letter of SHW QDPH back by the channel number (3). S becomes P.",
            "SHW QDPH = PET NAME. The pet's name is on the dog collar (BYTE) and the proudest year is on the certificate (2019). Password: BYTE2019.",
        };

        L2Game G => L2Game.Instance;
        bool ownClock;
        float ownRemaining;
        bool timeUp;
        bool finished;

        public bool TimeIsUp => timeUp;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (GameFlowManager.TimeUpHandler == HandleSessionTimeUp) GameFlowManager.TimeUpHandler = null;
        }

        void Start()
        {
            GameFlowManager gm = GameFlowManager.Instance;
            ownClock = gm == null || gm.levelTimer == null;
            ownRemaining = timeLimit;
            if (!ownClock)
            {
                GameFlowManager.TimeUpHandler = HandleSessionTimeUp;
                if (gm.levelTimer.RemainingTime > timeLimit) gm.levelTimer.SetRemainingTime(timeLimit);
            }
            L2Game g = G;
            if (g != null)
            {
                g.Saving += OnSaving;
                if (g.LoadedState != null && g.HasInt("l2_time")) SetRemaining(g.GetInt("l2_time", Mathf.RoundToInt(timeLimit)));
                if (g.ui != null) g.ui.AddPauseButton("HINT  (H)", () => { g.ui.Resume(); ShowHint(); });
            }
        }

        // ------------------------------------------------------------------ timer
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

        void OnSaving()
        {
            if (G != null) G.SetInt("l2_time", Mathf.CeilToInt(Remaining));
        }

        void Update()
        {
            L2Game g = G;
            if (g == null) return;

            if (ownClock && !timeUp && !finished && !g.Completed)
            {
                ownRemaining -= Time.deltaTime;   // stops while paused (timeScale 0)
                if (ownRemaining <= 0f) { ownRemaining = 0f; TimeUp(); }
            }
            if (g.ui != null)
            {
                if (ownClock)
                {
                    bool warn = ownRemaining <= warningTime;
                    float pulse = warn && !timeUp && !g.Completed ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.1f : 1f;
                    g.ui.SetClock(LevelTimer.Format(ownRemaining), warn, pulse);
                }
                else g.ui.SetClock(null, false);
            }

            if (timeUp || g.Completed || g.Paused || g.ModalOpen || (g.ui != null && g.ui.IsTyping)) return;
            if (g.InteractionsBlocked) return;
            if (Input.GetKeyDown(KeyCode.H)) ShowHint();
            else if (Input.GetKeyDown(KeyCode.J)) ShowJournal();
        }

        bool HandleSessionTimeUp()
        {
            TimeUp();
            return true;   // Level 2 shows its own Retry / Quit screen instead of the session's Game Over
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
            if (g.ui != null) g.ui.ShowTimeUp("The house locked you in before you could escape.\nRetry from your last checkpoint, restart Level 2, or quit to the menu.");
        }

        // ------------------------------------------------------------------ hints
        public string CurrentRoom()
        {
            L2Game g = G;
            if (g == null) return "living";
            if (!g.Flag("exit_living")) return "living";
            if (!g.Flag("exit_bedroom")) return "bedroom";
            if (!g.Flag("exit_kitchen")) return "kitchen";
            return "study";
        }

        string[] HintsFor(string room) =>
            room == "living" ? livingHints : room == "bedroom" ? bedroomHints : room == "kitchen" ? kitchenHints : studyHints;

        static string RoomName(string room) =>
            room == "living" ? "Living Room" : room == "bedroom" ? "Bedroom" : room == "kitchen" ? "Kitchen" : "Study";

        public int HintsUsed()
        {
            L2Game g = G;
            if (g == null) return 0;
            int n = 0;
            foreach (string r in new[] { "living", "bedroom", "kitchen", "study" }) n += g.GetInt("hints_" + r, 0);
            return n;
        }

        public void ShowHint()
        {
            L2Game g = G;
            if (g == null || g.ui == null || g.Completed) return;
            string room = CurrentRoom();
            string[] list = HintsFor(room);
            if (list == null || list.Length == 0) return;
            int used = g.GetInt("hints_" + room, 0);
            int index;
            string costLine;
            if (used < list.Length)
            {
                index = used;
                g.SetInt("hints_" + room, used + 1);
                costLine = "<color=#9A918A>Hint " + (index + 1) + " of " + list.Length + "  ·  -" + hintPenalty + " score</color>";
            }
            else
            {
                index = list.Length - 1;
                costLine = "<color=#9A918A>No more hints for this room (shown again for free).</color>";
            }
            if (g.sfx != null) g.sfx.Click();
            g.ui.ShowInfo("HINT  ·  " + RoomName(room).ToUpperInvariant(), list[index] + "\n\n" + costLine);
        }

        // ------------------------------------------------------------------ journal
        public void ShowJournal()
        {
            L2Game g = G;
            if (g == null || g.ui == null) return;
            var sb = new StringBuilder();
            int n = 0;
            foreach (string id in L2Items.PhotoPieces)
            {
                L2ItemDef d = L2Items.Get(id);
                if (d != null && !string.IsNullOrEmpty(d.back) && g.IsCollected(id))
                {
                    sb.Append("<color=#E8C547>").Append(d.name).Append(" (back)</color>\n").Append(d.back).Append("\n\n");
                    n++;
                }
            }
            var seen = new HashSet<string>();
            foreach (L2Examine ex in FindObjectsByType<L2Examine>(FindObjectsInactive.Include))
            {
                if (!g.Flag(ex.ReadFlag)) continue;
                string title = string.IsNullOrEmpty(ex.title) ? ex.DisplayName : ex.title;
                string key = title + "|" + ex.body;
                if (!seen.Add(key)) continue;
                sb.Append("<color=#E8C547>").Append(title).Append("</color>\n").Append(ex.body).Append("\n\n");
                n++;
            }
            if (n == 0) sb.Append("Nothing yet. Notes you read are copied here automatically.");
            g.ui.ShowInfo("NOTES  (J)", sb.ToString().TrimEnd());
        }

        // ------------------------------------------------------------------ completion & score
        public int CurrentScore() => Mathf.Max(0, Mathf.FloorToInt(Remaining) * pointsPerSecond - HintsUsed() * hintPenalty);

        // Called by the workstation just before the level completes.
        public void FinishLevel()
        {
            if (finished) return;
            finished = true;
            float left = Remaining;
            int score = CurrentScore();
            int hints = HintsUsed();
            try
            {
                IScoreStore store = ScoreStores.Current;
                store.SaveScore(new ScoreRecord
                {
                    player_id = PlayerPrefs.GetString("CodeBreaker.PlayerId", "player1"),
                    level_number = levelNumber,
                    score = score,
                    time_remaining = Mathf.Round(left * 10f) / 10f,
                    saved_at = System.DateTime.Now.ToString("s"),
                });
                ProgressRecord p = store.LoadProgress() ?? new ProgressRecord();
                p.levels_completed = Mathf.Max(p.levels_completed, levelNumber);
                p.current_level = Mathf.Max(p.current_level, levelNumber + 1);
                p.last_saved = System.DateTime.Now.ToString("s");
                store.SaveProgress(p);
            }
            catch (System.Exception e) { Debug.LogWarning("[Level 2] Score not saved: " + e.Message); }

            if (G != null && G.ui != null)
                G.ui.completionExtra = "\nTime remaining: " + LevelTimer.Format(left) + "   ·   Hints used: " + hints + "\n<color=#E8C547>SCORE: " + score + "</color>";
            LastScore = score;
        }

        public int LastScore { get; private set; } = -1;
    }
}
