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
        [Tooltip("Level 2 time limit in seconds (30:00 = 1800, set from LevelTimer). With the Game Session present its timer is used (and capped to this).")]
        public float timeLimit = 1800f;
        public float warningTime = 60f;

        [Header("Score")]
        public int pointsPerSecond = 10;
        public int hintPenalty = 250;
        public int levelNumber = 2;

        [Header("Hints per room (each list gets more direct)")]
        [TextArea(2, 4)] public string[] livingHints =
        {
            "Search the room: the four photo pieces, the batteries and the star figurine can be hidden anywhere here, inside drawers and cabinets too. The photo and the note by the door point to two values: the clock and the TV channel.",
            "Binary place values are 8 4 2 1. 1000 = 8 and 0011 = 2 + 1 = 3. Set the wall clock to 8 and put the TV on channel 3 (the remote needs batteries).",
            "When the star lamp shines, put the star figurine into the star slot on the bookshelf, then take the key from the blue book.",
        };
        [TextArea(2, 4)] public string[] bedroomHints =
        {
            "The diary is a little program: find the moves, count the stars, then open the right drawer. The UV flashlight is hidden somewhere in this room: check drawers and cabinets.",
            "Shine the UV flashlight on the mirror. The music box shows the moves: UP, RIGHT, RIGHT, DOWN opens the moon box. Then switch the lights off and count the stars on the ceiling.",
            "stars = 5 and arrays start at 0, so open drawer[5 - 1] = drawer[4]: the TOP drawer of the jewellery box.",
        };
        [TextArea(2, 4)] public string[] kitchenHints =
        {
            "Read Grandma's recipe card: one ingredient is missing. Which one comes in a carton of 6?",
            "Lift each ingredient to see the binary number underneath: 100 = 4, 1 = 1, 110 = 6, 10 = 2.",
            "Type the numbers in recipe order on the oven timer: 4 1 6 2.",
        };
        // Study hints for the picture-key puzzle. (Renamed from studyHints so the old cipher hints saved
        // in the scene are no longer used.)
        [TextArea(2, 4)] public string[] studyPictureHints =
        {
            "The terminal asks for values you learned earlier: the TV channel (Living Room) and the number of stars (Bedroom). The note on the desk is written in pictures.",
            "The whiteboard is the picture key: heart = who I love, paw = my dog, calendar = my best year. Match them on the picture wheel to decode the note.",
            "The heart is the dog: its collar says BYTE. The best year is on the certificate: 2019. Password: BYTE2019.",
        };

        [Header("Stuck helper")]
        [Tooltip("Seconds without any progress before the objective hint gets one step more specific")]
        public float stuckSeconds = 180f;

        L2Game G => L2Game.Instance;
        bool ownClock;
        float ownRemaining;
        bool timeUp;
        bool finished;
        int lastStamp = -1;
        string lastObjectiveKey;
        float stuckTimer;
        float objectiveCheck;
        float stampCheck;

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
            if (LevelTimer.TryFixedLimit(gameObject.scene.name, out float fixedLimit)) timeLimit = fixedLimit;   // 30:00
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
                if (g.ui != null) g.ui.AddPauseButton("NOTES  (J)", () => { g.ui.Resume(); ShowJournal(); });
                // the old one-line intro note is replaced by the room briefing (UpdateBriefing)
            }
        }

        // First thing in the Living Room: say what to look for (not where).
        System.Collections.IEnumerator IntroNote()
        {
            yield return new WaitForSeconds(2.5f);
            L2Game g = G;
            if (g == null || g.Completed || g.Flag("exit_living") || g.Flag("photo_assembled") || g.Flag("intro_photo_note")) yield break;
            g.SetFlag("intro_photo_note");
            g.Notify("The family photo is torn. The pieces must be somewhere in this room.", L2NoteKind.Info);
        }

        // About 3 minutes without progress: the objective hint gets one step more specific.
        void TrackStuck(L2Game g)
        {
            stampCheck -= Time.deltaTime;
            if (stampCheck > 0f) return;
            stampCheck = 1f;
            int stamp = g.ProgressStamp;
            string key = CurrentRoom();
            if (stamp != lastStamp || key != lastObjectiveKey)
            {
                lastStamp = stamp;
                lastObjectiveKey = key;
                stuckTimer = 0f;
                if (g.StuckNudge) { g.StuckNudge = false; g.RefreshObjective(false); }
                return;
            }
            stuckTimer += 1f;
            if (!g.StuckNudge && stuckTimer >= stuckSeconds)
            {
                g.StuckNudge = true;
                g.RefreshObjective(false);
                g.Notify("Stuck? The hint under your objective has been updated.", L2NoteKind.Info);
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
            TrackStuck(g);
            UpdateBriefing(g);
            objectiveCheck -= Time.deltaTime;
            if (objectiveCheck <= 0f) { objectiveCheck = 0.5f; g.RefreshObjective(); }   // step objectives follow every change
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
            g.ForgetNotes();   // this attempt is over: the next one starts with an empty Notes journal
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

        // The hint texts live here in code (the arrays above are kept only so old scene data still loads).
        // Each list goes from "where to look" to "how it works"; the last one is close to the answer.
        static readonly string[] LivingHintsV2 =
        {
            "Search every drawer and cabinet in this room for the four torn photo pieces, then assemble them in the inventory (V). The finished photo shows how this room looked that evening.",
            "Two photo pieces have writing on the back: clock = 1000 and channel = 0011. Those are binary numbers. The poster by the TV shows the place values 8 4 2 1: add up the places that have a 1. The TV remote needs batteries hidden in this room.",
            "Clock = 8:00 and TV = channel 3, then switch on the star lamp. Put the star figurine into the slot the star's shadow points to, and take the key from the blue book.",
        };
        static readonly string[] BedroomHintsV2 =
        {
            "Read the diary on the bed: it is a little program with three blanks. Find the UV flashlight hidden in this room. Something here only shows under UV light.",
            "Shine the UV flashlight on the mirror. Open and wind the music box: the dancer's moves (UP, RIGHT, RIGHT, DOWN) open the moon box. Its note says: lights off, look up.",
            "Count the glowing stars on the ceiling in the dark. open(drawer[stars - 1]) with arrays starting at 0: count from the bottom drawer, which is drawer[0].",
        };
        static readonly string[] KitchenHintsV2 =
        {
            "Read Grandma's recipe on the counter. One ingredient is missing: the one that comes in a carton of 6. Completing the recipe unlocks the oven timer.",
            "Lift the flour, sugar, eggs and milk and read underneath. Each label is a variable: \"milk = sugar * 2\" means milk holds two times whatever sugar holds. Only one label is a plain number. Start there.",
            "sugar is 1. Work out milk and flour from it, then eggs = flour + milk. Type the four values in the order oven.Set(flour, sugar, eggs, milk) asks for them.",
        };
        static readonly string[] StudyHintsV2 =
        {
            "The computer first checks what you learned: the TV channel (Living Room) and the number of stars (Bedroom). The note on the desk is written in pictures.",
            "The whiteboard is the picture key: heart = who I love, paw = my dog, calendar = my best year. Match them on the picture wheel to decode the note.",
            "The heart is the dog: its collar has its name. The best year is on the certificate on the wall. Type the name and the year together.",
        };

        string[] HintsFor(string room) =>
            room == "living" ? LivingHintsV2 : room == "bedroom" ? BedroomHintsV2 : room == "kitchen" ? KitchenHintsV2 : StudyHintsV2;

        // ------------------------------------------------------------------ room briefings
        // Shown once, a moment after the player first gets into each room (saved as a flag).
        static readonly (string room, string title, string text)[] Briefings =
        {
            ("living", "THE LIVING ROOM",
             "The door slammed shut behind you. Somebody tore the family photo apart and hid the pieces in this room.\n\nFind them and put the photo back together. It shows how this room looked that night.\n\n<color=#9A918A>H = hint (costs score)   ·   J = notes   ·   V = inventory</color>"),
            ("bedroom", "THE BEDROOM",
             "A child's bedroom. A diary lies open on the bed, written like a little program.\n\nDo what it says, one line at a time. Some of it can't be seen in normal light."),
            ("kitchen", "THE KITCHEN",
             "Grandma's kitchen. Her recipe book is still open on the counter, and the oven timer is locked.\n\nGrandpa labelled everything in here the way he wrote his programs."),
            ("study", "THE STUDY",
             "The last room. The computer on the desk guards the way out.\n\nIt will ask for things you already found in the other rooms. Your notes (J) remember them for you."),
        };

        float briefingCheck;

        void UpdateBriefing(L2Game g)
        {
            briefingCheck -= Time.deltaTime;
            if (briefingCheck > 0f) return;
            briefingCheck = 0.5f;
            if (g.ui == null || g.ModalOpen || g.Paused || g.Completed || SceneLoader.IsLoading) return;
            string room = CurrentRoom();
            if (g.Flag("brief_" + room)) { briefingFor = null; return; }
            if (room != "living" && !PlayerInRoom(room)) return;
            if (briefingFor != room) { briefingFor = room; briefingDelay = room == "living" ? 2.5f : 1.0f; return; }
            briefingDelay -= 0.5f;
            if (briefingDelay > 0f) return;
            foreach (var b in Briefings)
                if (b.room == room)
                {
                    g.SetFlag("brief_" + room);
                    if (g.sfx != null) g.sfx.Click();
                    g.ui.ShowInfo(b.title, b.text);
                    break;
                }
        }

        string briefingFor;
        float briefingDelay;

        // The previous room's exit is open; wait until the player has actually walked through it
        // (more than a few metres away from that door).
        bool PlayerInRoom(string room)
        {
            L2Game g = G;
            if (g == null || g.player == null) return true;
            string door = room == "bedroom" ? "Door_LivingRoom_Exit" : room == "kitchen" ? "Door_Bedroom_Exit" : "Door_Kitchen_Exit";
            if (!doorCache.TryGetValue(door, out L2Door d))
            {
                d = null;
                foreach (L2Door x in FindObjectsByType<L2Door>(FindObjectsInactive.Include)) if (x.name == door) { d = x; break; }
                doorCache[door] = d;
            }
            if (d == null) return true;
            Vector3 a = g.player.position, b = d.transform.position;
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b) > 4f;
        }

        readonly Dictionary<string, L2Door> doorCache = new Dictionary<string, L2Door>();

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

        // Smart hints: always about the FIRST puzzle that is not solved yet (PuzzleBook order), so a
        // solved puzzle never gets a hint. Each puzzle has up to two hints, the second more direct.
        // Each new hint costs score (counted per room, as before); repeating the last one is free.
        // When everything is solved, a free message says so.
        public const string AllSolvedMessage = "You have already completed all puzzles in this level!";

        static readonly Dictionary<string, string[]> PuzzleHints = new Dictionary<string, string[]>
        {
            { "living_photo", new[] {
                "Search every drawer and cabinet in this room for the four torn photo pieces.",
                "Once you have all four pieces, open the inventory (V) and press Assemble. The finished photo shows how this room looked that evening." } },
            { "living_starlamp", new[] {
                "Two photo pieces have writing on the back: clock = 1000 and channel = 0011. Those are binary numbers. The poster by the TV shows the place values 8 4 2 1: add up the places that have a 1. The TV remote needs batteries hidden in this room.",
                "Clock = 8:00 and TV = channel 3, then switch on the star lamp." } },
            { "living_star", new[] {
                "The star's shadow points at a slot on the bookshelf. Something star-shaped is hidden in this room.",
                "Hold the star figurine and use it on the slot the shadow points to." } },
            { "living_exit", new[] {
                "The blue book on the shelf has come loose. Take what it was holding and unlock the Living Room exit." } },
            { "bedroom_uv", new[] {
                "Read the diary on the bed: it is a little program. Find the UV flashlight hidden in this room. Something here only shows under UV light.",
                "Hold the UV flashlight (G switches it on) and shine it on the mirror." } },
            { "bedroom_moonbox", new[] {
                "Open and wind the music box, then watch the dancer: every move is an arrow.",
                "The dancer's moves (UP, RIGHT, RIGHT, DOWN) open the moon box. Enter them on its lock in that order." } },
            { "bedroom_stars", new[] {
                "The note in the moon box says: lights off, look up.",
                "Switch off the bedroom light and count the glowing stars on the ceiling." } },
            { "bedroom_exit", new[] {
                "open(drawer[stars - 1]) with arrays starting at 0: count from the bottom drawer, which is drawer[0].",
                "Open that jewellery box drawer, take the key and unlock the Bedroom exit." } },
            { "kitchen_recipe", new[] {
                "Read Grandma's recipe on the counter. One ingredient is missing: the one that comes in a carton of 6. Completing the recipe unlocks the oven timer." } },
            { "kitchen_oven", new[] {
                "Lift the flour, sugar, eggs and milk and read underneath. Each label is a variable: \"milk = sugar * 2\" means milk holds two times whatever sugar holds. Only one label is a plain number. Start there.",
                "sugar is 1. Work out milk and flour from it, then eggs = flour + milk. Type the four values in the order oven.Set(flour, sugar, eggs, milk) asks for them." } },
            { "kitchen_exit", new[] {
                "The oven door is open. Take what is inside and unlock the Kitchen exit." } },
            { "study_checks", new[] {
                "The computer first checks what you learned: the TV channel (Living Room) and the number of stars (Bedroom). Your notes (J) remember them." } },
            { "study_picture", new[] {
                "The note on the desk is written in pictures. The whiteboard is the picture key: heart = who I love, paw = my dog, calendar = my best year.",
                "Match the pictures on the picture wheel to decode the note." } },
            { "study_login", new[] {
                "The heart is the dog: its collar has its name. The best year is on the certificate on the wall. Type the name and the year together." } },
        };

        public void ShowHint()
        {
            L2Game g = G;
            if (g == null || g.ui == null) return;
            PuzzleBook.Puzzle next = g.Completed ? null : PuzzleBook.FirstUnsolved(g.SceneName, g.Flag, g.GetInt);
            if (next == null || !PuzzleHints.TryGetValue(next.key, out string[] list) || list.Length == 0)
            {
                if (g.sfx != null) g.sfx.Click();
                g.ui.ShowInfo("HINT", AllSolvedMessage + "\n\n<color=#9A918A>No score was taken.</color>");
                return;
            }
            string room = next.room;
            int used = g.GetInt("hintp_" + next.key, 0);
            int index;
            string costLine;
            if (used < list.Length)
            {
                index = used;
                g.SetInt("hintp_" + next.key, used + 1);
                g.SetInt("hints_" + room, g.GetInt("hints_" + room, 0) + 1);   // score counts hints per room
                costLine = "<color=#9A918A>Hint " + (index + 1) + " of " + list.Length + " for this puzzle  ·  -" + hintPenalty + " score</color>";
            }
            else
            {
                index = list.Length - 1;
                costLine = "<color=#9A918A>No more hints for this puzzle (shown again for free).</color>";
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
            bool binary = false;
            foreach (string id in L2Items.PhotoPieces)
            {
                L2ItemDef d = L2Items.Get(id);
                if (d != null && !string.IsNullOrEmpty(d.back) && g.IsCollected(id))
                {
                    sb.Append("<color=#E8C547>").Append(d.name).Append(" (back)</color>\n").Append(d.back).Append("\n\n");
                    if (d.back.ToLowerInvariant().Contains("binary")) binary = true;
                    n++;
                }
            }
            // ingredient numbers read in the Kitchen (saved, so they never need lifting again)
            var lifted = new StringBuilder();
            foreach (L2Ingredient ing in FindObjectsByType<L2Ingredient>(FindObjectsInactive.Include))
            {
                if (!g.Flag("seen_" + ing.ingredient.ToLowerInvariant())) continue;
                lifted.Append("Under the ").Append(ing.ingredient).Append(":   ").Append(ing.Underside).Append("\n");
            }
            if (lifted.Length > 0)
            {
                sb.Append("<color=#E8C547>Kitchen ingredients</color>\n").Append(lifted)
                  .Append("<color=#9A918A>Each label is a variable:  name = value.  The name on the left holds the value on the right.</color>\n\n");
                n++;
            }
            if (binary)
                sb.Append("<color=#9A918A>Binary: 8 4 2 1  ->  1000 = 8</color>\n\n");
            if (g.Flag("picture_decoded"))
            {
                sb.Append("<color=#E8C547>Picture note (decoded)</color>\nPASSWORD = who I love + my best year\nwho I love = my dog\n\n");
                n++;
            }
            L2Examine.AppendReadNotes(g, sb, ref n);
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
