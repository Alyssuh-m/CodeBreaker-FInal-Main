using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodeBreaker.Level2
{
    public enum L2NoteKind { Info, Item, Success, Warning }

    // Level 2 game manager: inventory, puzzle flags, doors, lights, objectives, checkpoints,
    // saving / loading, reset, completion, and locking the player while a menu is open.
    //
    // Works with or without the persistent Code Breaker "Game Session" (GameFlowManager) from Level 1:
    // when it is present its timer is stopped at the end, its player-control switch is used, and
    // Exit to Home goes through it.
    [DefaultExecutionOrder(-200)]
    public class L2Game : MonoBehaviour
    {
        public static L2Game Instance { get; private set; }

        [Header("Scene references (filled by Code Breaker > Level 2 > Setup Gameplay)")]
        public Transform player;
        public Camera playerCamera;
        public L2UI ui;
        public L2Audio sfx;
        public L2HeldItem heldView;
        public L2Checkpoint[] checkpoints = new L2Checkpoint[0];
        [Tooltip("Optional: level-specific objectives and completion text (Level 3). Empty = Level 2 rules.")]
        public L2LevelRules rules;

        // Set by a level when gameplay must stop (e.g. its own countdown ran out).
        public bool ExternallyBlocked { get; private set; }
        public void SetBlocked(bool blocked) { ExternallyBlocked = blocked; ApplyControls(); }

        public bool Completed { get; private set; }
        public bool ModalOpen { get; private set; }
        public bool Paused => ui != null && ui.IsPaused;
        public string SceneName { get; private set; }
        public int CheckpointIndex => checkpoint;
        public L2State LoadedState { get; private set; }

        public string Held { get; private set; }
        public bool FlashlightOn { get; private set; }
        public IReadOnlyList<string> Inventory => inventory;

        // Raised whenever inventory / held item / flags change (UI refresh).
        public event System.Action Changed;

        readonly List<string> inventory = new List<string>();
        readonly HashSet<string> flags = new HashSet<string>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly HashSet<string> collected = new HashSet<string>();
        readonly Dictionary<string, L2DoorEntry> doorStates = new Dictionary<string, L2DoorEntry>();
        readonly Dictionary<string, bool> lightStates = new Dictionary<string, bool>();
        readonly Dictionary<string, Transform> prototypes = new Dictionary<string, Transform>();
        Behaviour[] controls = new Behaviour[0];
        int checkpoint;
        string checkpointLabel;
        string lastObjective;

        public bool InteractionsBlocked
        {
            get
            {
                if (ModalOpen || Paused || Completed || ExternallyBlocked || SceneLoader.IsLoading) return true;
                GameFlowManager gm = GameFlowManager.Instance;
                return gm != null && gm.InteractionsBlocked;
            }
        }

        // ------------------------------------------------------------------ startup
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SceneName = SceneManager.GetActiveScene().name;

            if (player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
            if (playerCamera == null) playerCamera = Camera.main;
            if (GetComponent<LevelFootsteps>() == null) gameObject.AddComponent<LevelFootsteps>();   // footsteps like Level 1
            FindControls();

            foreach (L2Pickup p in FindObjectsByType<L2Pickup>(FindObjectsInactive.Include))
                if (!string.IsNullOrEmpty(p.itemId)) prototypes[p.itemId] = p.transform;

            // Continue from a checkpoint? (Load Saved Games from the menu, or Try Again.)
            SaveData pending = SaveSystem.PendingLoad;
            bool resume = SaveSystem.ResumeCheckpoint || (pending != null && pending.sceneName == SceneName);
            resumed = resume;
            if (resume)
            {
                LevelCheckpoint cp = SaveSystem.GetCheckpoint(SaveSystem.Load(), SceneName);   // this level's own checkpoint
                if (cp != null)
                {
                    try { LoadedState = JsonUtility.FromJson<L2State>(cp.json); }
                    catch (System.Exception e) { Debug.LogWarning("[Level 2] Could not read the checkpoint: " + e.Message); }
                }
            }
            if (LoadedState != null) ReadState(LoadedState);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        bool resumed;
        List<string> knownSolved = new List<string>();
        float puzzleCheck;

        void Start()
        {
            // Opened from the menu (Continue / Select Level) without the Game Session: nobody else records
            // the level start, so the save points to this level from now on. (Opening the scene directly in
            // the Editor does not touch the save.)
            SaveData src = SaveSystem.PendingLoad;
            if (GameFlowManager.Instance == null && src != null && src.sceneName == SceneName &&
                LevelCatalog.Instance != null && LevelCatalog.Instance.Find(SceneName) != null)
            {
                SaveSystem.RecordLevelStart(SceneName,
                    src != null ? src.health : 100f, src != null && src.maxHealth > 0f ? src.maxHealth : 100f,
                    src != null ? src.wrongAttempts : 0, src != null ? src.totalPlayTime : 0f, resumed);
            }
            knownSolved = PuzzleBook.Solved(SceneName, Flag, GetInt);   // already solved when the level starts: no extra save

            // GameFlowManager has already used these during its scene-load step; clear them now.
            SaveSystem.ResumeCheckpoint = false;
            if (GameFlowManager.Instance == null && SaveSystem.PendingLoad != null && SaveSystem.PendingLoad.sceneName == SceneName)
                SaveSystem.PendingLoad = null;

            UIUtil.EnsureEventSystem();
            Time.timeScale = 1f;

            if (LoadedState != null)
            {
                L2Checkpoint cp = FindCheckpoint(LoadedState.checkpoint);
                if (cp != null) Teleport(cp.transform.position, cp.transform.rotation);
                if (!string.IsNullOrEmpty(LoadedState.held) && HasItem(LoadedState.held)) Equip(LoadedState.held, false);
                if (L2Items.IsLight(Held) && LoadedState.flashlightOn) SetFlashlight(true, false);
                Notify("Checkpoint loaded: " + (string.IsNullOrEmpty(checkpointLabel) ? SceneName : checkpointLabel) + ".", L2NoteKind.Info);
            }

            ApplyControls();
            RefreshObjective(false);
            RaiseChanged();
        }

        void FindControls()
        {
            var list = new List<Behaviour>();
            if (player != null)
                foreach (MonoBehaviour mb in player.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string n = mb.GetType().Name;
                    if (n == "PlayerMovement" || n == "MouseMovement") list.Add(mb);
                }
            controls = list.ToArray();
        }

        // Saves as soon as a puzzle is newly solved (once per puzzle), with the full level state.
        // Loading puts the player at the last checkpoint reached, with every solved puzzle still solved.
        void LateUpdate()
        {
            if (Completed || ExternallyBlocked) return;
            puzzleCheck -= Time.unscaledDeltaTime;
            if (puzzleCheck > 0f) return;
            puzzleCheck = 0.25f;
            List<string> now = PuzzleBook.Solved(SceneName, Flag, GetInt);
            bool newOne = false;
            foreach (string id in now) if (!knownSolved.Contains(id)) { newOne = true; break; }
            knownSolved = now;
            if (!newOne || SceneLoader.IsLoading) return;
            if (string.IsNullOrEmpty(checkpointLabel)) checkpointLabel = SceneName;
            SaveCheckpoint();
            PuzzleSaves++;
        }

        // Number of puzzle-solve saves this session (for the play tests).
        public int PuzzleSaves { get; private set; }

        void Update()
        {
            if (Completed || Paused) return;
            if (ui != null && ui.IsTyping) return;
            if (Input.GetKeyDown(KeyCode.G)) ToggleFlashlight();
        }

        // ------------------------------------------------------------------ player control / menus
        public void SetModal(bool open)
        {
            ModalOpen = open;
            ApplyControls();
        }

        public void ApplyControls()
        {
            bool enable = !ModalOpen && !Paused && !Completed && !ExternallyBlocked;
            GameFlowManager gm = GameFlowManager.Instance;
            if (gm != null)
            {
                if (enable && gm.InteractionsBlocked) enable = false;
                gm.SetControlsEnabled(enable);
            }
            else
            {
                foreach (Behaviour b in controls) if (b != null) b.enabled = enable;
            }

            if (enable)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else UIUtil.ShowCursor();
        }

        public void Teleport(Vector3 pos, Quaternion rot)
        {
            if (player == null) return;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = pos;
            player.rotation = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
            if (cc != null) cc.enabled = true;
        }

        // ------------------------------------------------------------------ notifications
        public void Notify(string text, L2NoteKind kind = L2NoteKind.Info)
        {
            if (ui != null) ui.Notify(text, kind);
            else Debug.Log("[Level 2] " + text);
        }

        void RaiseChanged() => Changed?.Invoke();

        // ------------------------------------------------------------------ flags & numbers
        public bool Flag(string f) => !string.IsNullOrEmpty(f) && flags.Contains(f);

        public void SetFlag(string f, bool on = true)
        {
            if (string.IsNullOrEmpty(f)) return;
            bool changed = on ? flags.Add(f) : flags.Remove(f);
            if (changed) RaiseChanged();
            if (changed && on) CodeBreaker.Database.DatabaseHooks.Level2Flag(SceneName, f);   // SQLite: Level 2 puzzle solved
        }

        public int GetInt(string key, int fallback) => ints.TryGetValue(key, out int v) ? v : fallback;
        public bool HasInt(string key) => ints.ContainsKey(key);
        public void SetInt(string key, int v) => ints[key] = v;

        // ------------------------------------------------------------------ inventory
        public bool HasItem(string id) => !string.IsNullOrEmpty(id) && inventory.Contains(id);
        public bool IsCollected(string id) => collected.Contains(id);

        public Transform GetPrototype(string id) => id != null && prototypes.TryGetValue(id, out Transform t) ? t : null;

        public void RegisterPrototype(string id, Transform t)
        {
            if (!string.IsNullOrEmpty(id) && t != null) prototypes[id] = t;
        }

        public void Collect(L2Pickup p)
        {
            if (p == null || string.IsNullOrEmpty(p.itemId)) return;
            // Only an item that is actually present in the world can be taken: hidden keys (inside the
            // book, the oven or a closed / locked drawer) can never be collected early.
            if (!p.gameObject.activeInHierarchy || !p.enabled) return;
            if (collected.Contains(p.itemId)) { p.gameObject.SetActive(false); return; }
            collected.Add(p.itemId);
            prototypes[p.itemId] = p.transform;
            if (!string.IsNullOrEmpty(p.flagOnCollect)) flags.Add(p.flagOnCollect);
            p.gameObject.SetActive(false);
            AddItem(p.itemId, true);
            CodeBreaker.Database.DatabaseHooks.ItemCollected(p.itemId);   // SQLite: clue / key / item / keycard

            L2ItemDef def = L2Items.Get(p.itemId);
            if (def != null && !string.IsNullOrEmpty(def.back))
                Notify("Something is written on the back: " + def.back, L2NoteKind.Item);

            if (L2Items.IsPhotoPiece(p.itemId))
            {
                int have = 0;
                foreach (string id in L2Items.PhotoPieces) if (HasItem(id)) have++;
                if (have == 4) Notify("All four photo pieces found. Open the inventory (V) to assemble them.", L2NoteKind.Success);
            }
            if (p.itemId == L2Items.UVFlashlight)
                Notify("Equip it from the inventory (V), then press G to switch it on.", L2NoteKind.Info);
        }

        public void AddItem(string id, bool notify)
        {
            if (string.IsNullOrEmpty(id) || inventory.Contains(id)) return;
            inventory.Add(id);
            if (notify)
            {
                if (sfx != null) sfx.Pickup();
                Notify(L2Items.NameOf(id) + " collected.", L2NoteKind.Item);
            }
            RaiseChanged();
            RefreshObjective();
        }

        public void RemoveItem(string id)
        {
            if (!inventory.Remove(id)) return;
            if (Held == id) Unequip(false);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ held item & flashlight
        public void Equip(string id, bool notify = true)
        {
            L2ItemDef d = L2Items.Get(id);
            if (d == null || d.hold == L2HoldKind.None || !HasItem(id)) return;
            if (Held == id) return;
            if (FlashlightOn) SetFlashlight(false, false);
            Held = id;
            if (heldView != null) heldView.Show(id);
            if (notify)
            {
                if (sfx != null) sfx.Click();
                Notify(d.hold == L2HoldKind.Flashlight
                    ? d.name + " equipped. Press G to switch it on or off."
                    : "Holding the " + d.name + ". Look at where it goes and press E.", L2NoteKind.Info);
            }
            RaiseChanged();
        }

        public void Unequip(bool notify = true)
        {
            if (Held == null) return;
            string was = Held;
            if (FlashlightOn) SetFlashlight(false, false);
            Held = null;
            if (heldView != null) heldView.Show(null);
            if (notify) Notify(L2Items.NameOf(was) + " put away.", L2NoteKind.Info);
            RaiseChanged();
        }

        public void ToggleFlashlight()
        {
            if (!L2Items.IsLight(Held))
            {
                foreach (string id in inventory)
                    if (L2Items.IsLight(id)) { Notify("Equip the " + L2Items.NameOf(id) + " from the inventory (V) first.", L2NoteKind.Info); break; }
                return;
            }
            SetFlashlight(!FlashlightOn, true);
        }

        public void SetFlashlight(bool on, bool fx)
        {
            FlashlightOn = on && L2Items.IsLight(Held);
            if (heldView != null) heldView.SetLight(FlashlightOn);
            if (fx && sfx != null) sfx.Click();
            RaiseChanged();
        }

        // ------------------------------------------------------------------ doors & lights (persistent states)
        public bool TryGetDoorState(string doorName, out L2DoorEntry e) => doorStates.TryGetValue(doorName, out e);

        public void RecordDoor(string doorName, bool locked, bool open)
        {
            doorStates[doorName] = new L2DoorEntry { name = doorName, locked = locked, open = open };
        }

        public bool TryGetLightState(string group, out bool on) => lightStates.TryGetValue(group, out on);

        public void RecordLight(string group, bool on) => lightStates[group] = on;

        // ------------------------------------------------------------------ objectives
        int PhotoPiecesFound()
        {
            int n = 0;
            foreach (string id in L2Items.PhotoPieces) if (HasItem(id) || collected.Contains(id)) n++;
            return n;
        }

        public string CurrentObjective(out string hint)
        {
            hint = "";
            if (rules != null) return rules.Objective(this, out hint);
            if (Completed || Flag("level_complete")) return "Level 2 complete!";

            // Step by step: the objective names the next concrete thing to do, the hint below it says
            // where to look (never the answer). StuckNudge adds one more pointer after ~3 minutes.
            if (!Flag("exit_living"))
            {
                if (HasItem(L2Items.LivingKey) || doorUnlocked("Door_LivingRoom_Exit"))
                    return "Unlock the Living Room exit.";
                if (!Flag("photo_assembled"))
                {
                    int found = PhotoPiecesFound();
                    if (found == 4) { hint = "Open the inventory (V) and press Assemble."; return "Assemble the family photo."; }
                    hint = "Photo pieces: " + found + " / 4. Search every drawer and cabinet in this room.";
                    if (StuckNudge) hint += "\nSearched places say \"Empty\". Check the ones you haven't opened yet.";
                    return "Find the 4 torn photo pieces.";
                }
                if (!Flag("star_shadow_seen"))
                {
                    if (!Flag("clock_confirmed_8"))
                    {
                        hint = "A photo piece says clock = 1000. The chart by the TV explains numbers written like that.";
                        if (StuckNudge) hint += "\nPress E on the wall clock to move its hands.";
                        return "Set the wall clock to the time in the photo.";
                    }
                    if (!(Flag("tv_on") && GetInt("tv_channel", 1) == 3))
                    {
                        bool batteries = Flag("remote_batteries");
                        hint = batteries ? "A photo piece says channel = 0011. Read it with the chart by the TV, then use the remote."
                                         : "A photo piece says channel = 0011. The remote is dead: its batteries are hidden in this room.";
                        return "Put the TV on the channel from the photo.";
                    }
                    hint = "The note by the door: if (clock == 8 && tv.channel == 3) the star shines.";
                    return "Switch on the star lamp.";
                }
                if (!Flag("star_placed"))
                {
                    if (!HasItem(L2Items.StarFigurine) && !IsCollected(L2Items.StarFigurine))
                    {
                        hint = "The star's shadow points at a slot on the bookshelf. Something star-shaped is hidden in this room.";
                        return "Find something star-shaped.";
                    }
                    hint = "Hold the star figurine and use it on the slot the shadow points to.";
                    return "Put the star where the shadow falls.";
                }
                hint = "The blue book on the shelf has come loose.";
                return "Take what the blue book was holding.";
            }

            if (!Flag("exit_bedroom"))
            {
                if (HasItem(L2Items.BedroomKey) || doorUnlocked("Door_Bedroom_Exit")) return "Unlock the Bedroom exit.";
                if (!Flag("read_diary")) { hint = "It's on the bed. It reads like a little program."; return "Read the diary."; }
                if (!Flag("moon_seen"))
                {
                    if (!HasItem(L2Items.UVFlashlight) && !IsCollected(L2Items.UVFlashlight))
                    {
                        hint = "Some writing here is in invisible ink. A UV flashlight is hidden in this room.";
                        return "Find the UV flashlight.";
                    }
                    hint = "Hold the UV flashlight (G switches it on) and shine it on something that reflects.";
                    return "Look for hidden writing with the UV light.";
                }
                if (!Flag("moonbox_open"))
                {
                    hint = Flag("dance_seen") ? "Enter the dancer's moves on the moon box lock, in the same order."
                                              : "Open the music box, wind it up and watch the dancer: every move is an arrow.";
                    return "Open the moon box with the dancer's moves.";
                }
                if (!Flag("stars_seen"))
                {
                    hint = "The note in the moon box: turn off the lights and look up.";
                    if (StuckNudge) hint += "\nSwitch off the bedroom light and look at the ceiling.";
                    return "Count the stars in the dark.";
                }
                hint = "open(drawer[stars - 1]);  arrays start at 0, so drawer[0] is the bottom drawer.";
                return "Open the right jewellery box drawer.";
            }

            if (!Flag("exit_kitchen"))
            {
                if (HasItem(L2Items.KitchenKey) || doorUnlocked("Door_Kitchen_Exit")) return "Unlock the Kitchen exit.";
                if (!Flag("recipe_complete"))
                {
                    if (!Flag("read_recipe")) { hint = "Her recipe book lies open on the counter."; return "Read Grandma's recipe."; }
                    hint = "One ingredient is missing from her function: the one that comes in a carton of 6.";
                    return "Complete Grandma's recipe.";
                }
                if (!Flag("oven_open"))
                {
                    int seen = 0;
                    foreach (string ing in new[] { "flour", "sugar", "eggs", "milk" }) if (Flag("seen_" + ing)) seen++;
                    if (seen < 4)
                    {
                        hint = "Checked: " + seen + " / 4. Lift the flour, sugar, eggs and milk and read underneath.";
                        return "Check under each ingredient.";
                    }
                    hint = "Each label is a variable: the name on the left holds the value on the right. Start with the one you already know.";
                    if (StuckNudge) hint += "\nThe recipe ends with oven.Set(flour, sugar, eggs, milk). Your notes (J) list every label.";
                    return "Work out each value and set the oven timer.";
                }
                hint = "The oven door is open.";
                return "Take what is inside the oven.";
            }

            int checks = GetInt("terminal_checks", 0);
            if (checks < 2)
            {
                hint = checks == 0 ? "It asks for something you found in the Living Room." : "It asks for something you counted in the Bedroom.";
                return "Answer the computer's checks.";
            }
            if (!Flag("picture_decoded"))
            {
                hint = StuckNudge ? "Use the picture wheel on the desk.\nThe whiteboard says what each picture means." : "Use the picture wheel. The whiteboard is the key.";
                return "Decode the picture note on the desk.";
            }
            hint = StuckNudge ? "Who do they love? The dog's collar has its name.\nThe best year hangs on the wall." : "Find who they love most and their proudest year.";
            return "Log in to the computer.";
        }

        // One step more specific, shown only after a long time without progress (never the answer).
        string BedroomNudge()
        {
            if (!Flag("moon_seen")) return "Shine the UV flashlight on something that reflects.";
            if (!Flag("moonbox_open")) return "Watch the music box dancer: her moves open the moon box.";
            if (!Flag("stars_seen")) return "The stars only shine in the dark.";
            return "";
        }

        // Set by L2LevelSession after about 3 minutes without progress; cleared on the next progress.
        public bool StuckNudge { get; set; }

        // Changes whenever something is found, read, solved or used (used to detect "stuck").
        public int ProgressStamp => flags.Count * 7 + inventory.Count * 131 + collected.Count * 1031 + ints.Count;

        bool doorUnlocked(string doorName) => doorStates.TryGetValue(doorName, out L2DoorEntry e) && !e.locked;

        public void RefreshObjective(bool announce = true)
        {
            string o = CurrentObjective(out string hint);
            if (ui != null) ui.SetObjective(o, hint);
            if (o != lastObjective)
            {
                bool first = lastObjective == null;
                lastObjective = o;
                if (announce && !first) Notify("New objective: " + o, L2NoteKind.Success);
            }
        }

        // ------------------------------------------------------------------ puzzles shared by several scripts
        public void AssemblePhoto()
        {
            if (Flag("photo_assembled")) return;
            foreach (string id in L2Items.PhotoPieces) inventory.Remove(id);
            flags.Add("photo_assembled");
            CodeBreaker.Database.DatabaseHooks.Level2Flag(SceneName, "photo_assembled");   // SQLite: photo puzzle solved
            AddItem(L2Items.FamilyPhoto, false);
            if (sfx != null) sfx.Success();
            Notify("Family Photo restored.", L2NoteKind.Success);
            RaiseChanged();
            RefreshObjective();
        }

        // ------------------------------------------------------------------ checkpoints
        L2Checkpoint FindCheckpoint(int index)
        {
            if (checkpoints != null)
                foreach (L2Checkpoint c in checkpoints) if (c != null && c.index == index) return c;
            return null;
        }

        public void ReachCheckpoint(L2Checkpoint cp)
        {
            if (cp == null || cp.index <= checkpoint) return;
            checkpoint = cp.index;
            checkpointLabel = cp.label;
            SaveCheckpoint();
            Notify("Checkpoint reached: " + cp.label + ". Progress saved.", L2NoteKind.Success);
        }

        L2State BuildState()
        {
            var s = new L2State
            {
                checkpoint = checkpoint,
                checkpointLabel = checkpointLabel,
                held = Held,
                flashlightOn = FlashlightOn,
                objective = lastObjective,
            };
            s.inventory.AddRange(inventory);
            s.flags.AddRange(flags);
            foreach (var kv in ints) s.ints.Add(new L2IntEntry { key = kv.Key, value = kv.Value });
            s.collected.AddRange(collected);
            foreach (L2Door d in FindObjectsByType<L2Door>())
                s.doors.Add(new L2DoorEntry { name = d.name, locked = d.Locked, open = d.IsOpen });
            foreach (L2LightSwitch l in FindObjectsByType<L2LightSwitch>())
                s.lights.Add(new L2LightEntry { group = l.group, on = l.IsOn });
            return s;
        }

        void ReadState(L2State s)
        {
            checkpoint = s.checkpoint;
            checkpointLabel = s.checkpointLabel;
            inventory.Clear();
            foreach (string id in s.inventory) if (!inventory.Contains(id)) inventory.Add(id);
            flags.Clear();
            foreach (string f in s.flags) flags.Add(f);
            ints.Clear();
            foreach (L2IntEntry e in s.ints) ints[e.key] = e.value;
            collected.Clear();
            foreach (string c in s.collected) collected.Add(c);
            doorStates.Clear();
            foreach (L2DoorEntry d in s.doors) doorStates[d.name] = d;
            lightStates.Clear();
            foreach (L2LightEntry l in s.lights) lightStates[l.group] = l.on;
        }

        // Saves the current state without moving to a new checkpoint (level start, item collected).
        // Loading it puts the player at the last checkpoint reached (or the level start).
        public void SaveProgress(string label)
        {
            if (Completed) return;
            if (!string.IsNullOrEmpty(label) && checkpoint == 0) checkpointLabel = SceneName + " (" + label + ")";
            if (string.IsNullOrEmpty(checkpointLabel)) checkpointLabel = SceneName;
            SaveCheckpoint();
        }

        // Raised just before a checkpoint is written, so a level can store extra numbers (e.g. its timer).
        public event System.Action Saving;

        void SaveCheckpoint()
        {
            Saving?.Invoke();
            SaveData prev = SaveSystem.Load();
            SaveData d = prev ?? SaveSystem.NewData();
            d.sceneName = SceneName;
            LevelCatalog cat = LevelCatalog.Instance;
            LevelCatalog.LevelInfo info = cat != null ? cat.Find(SceneName) : null;
            d.levelName = info != null && !string.IsNullOrEmpty(info.label)
                ? System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(info.label.ToLowerInvariant())
                : SceneName;

            GameFlowManager gm = GameFlowManager.Instance;
            if (gm != null && gm.playerHealth != null)
            {
                d.health = gm.playerHealth.CurrentHealth;
                d.maxHealth = gm.playerHealth.maxHealth;
                d.wrongAttempts = gm.playerHealth.WrongAttempts;
            }

            SaveSystem.SetCheckpoint(d, SceneName, checkpointLabel, JsonUtility.ToJson(BuildState()));
            SaveSystem.SetSolvedPuzzles(d, SceneName, PuzzleBook.Solved(SceneName, Flag, GetInt));
            SaveSystem.Save(d);
        }

        // ------------------------------------------------------------------ notes journal reset
        // Flags that only feed the Notes journal (J): notes read ("read:...") and ingredient labels
        // lifted in the Level 2 kitchen ("seen_..."). Puzzle progress never uses these prefixes.
        static readonly string[] NotePrefixes = { L2Examine.NoteFlagPrefix, "seen_" };

        public static bool IsNoteFlag(string f)
        {
            if (string.IsNullOrEmpty(f)) return false;
            foreach (string p in NotePrefixes) if (f.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        // Called when an attempt fails (time up / game over). That playthrough's notes are dropped from
        // memory and from the saved checkpoint, so Try Again, Continue or a restart opens an empty Notes
        // journal. Everything else in the checkpoint (solved puzzles, items, doors, timer) is kept as is.
        // It is never called while the player is still playing, so current notes are never lost.
        public void ForgetNotes()
        {
            flags.RemoveWhere(IsNoteFlag);
            SaveData d = SaveSystem.Load();
            LevelCheckpoint cp = SaveSystem.GetCheckpoint(d, SceneName);
            if (cp == null) return;
            try
            {
                L2State s = JsonUtility.FromJson<L2State>(cp.json);
                if (s == null || s.flags.RemoveAll(IsNoteFlag) == 0) return;
                SaveSystem.SetCheckpoint(d, SceneName, cp.label, JsonUtility.ToJson(s));
                SaveSystem.Save(d);
            }
            catch (System.Exception e) { Debug.LogWarning("[" + SceneName + "] Could not clear the notes in the checkpoint: " + e.Message); }
        }

        // ------------------------------------------------------------------ level end & restarts
        public void CompleteLevel()
        {
            if (Completed) return;
            flags.Add("level_complete");
            Completed = true;
            if (FlashlightOn) SetFlashlight(false, false);

            GameFlowManager gm = GameFlowManager.Instance;
            if (gm != null && gm.levelTimer != null) gm.levelTimer.StopTimer();

            // Mark the level completed and unlock the next one (only here, never when a level starts).
            if (LevelCatalog.Instance != null && LevelCatalog.Instance.Find(SceneName) != null)
            {
                SaveData d = SaveSystem.Load() ?? SaveSystem.NewData();
                if (gm != null && gm.playerHealth != null)
                {
                    d.health = gm.playerHealth.CurrentHealth;
                    d.maxHealth = gm.playerHealth.maxHealth;
                    d.wrongAttempts = gm.playerHealth.WrongAttempts;
                }
                SaveSystem.RecordLevelCompleted(d, SceneName);
                SaveSystem.Save(d);
            }
            // SQLite: Level 3 completion (Level 2 is saved by SqliteScoreStore, so the hook skips it)
            CodeBreaker.Database.DatabaseHooks.LevelCompleted(SceneName,
                CodeBreaker.Level3.L3Manager.Instance != null ? CodeBreaker.Level3.L3Manager.Instance.Remaining
                : gm != null && gm.levelTimer != null ? gm.levelTimer.RemainingTime : 0f);

            ApplyControls();
            RefreshObjective(false);
            if (sfx != null) sfx.Success();
            if (ui != null)
            {
                if (rules != null) ui.ShowCompletion(NextLevelScene() != null, rules.CompletionBody, rules.CompletionTitle, rules.NoNextLevelLabel);
                else ui.ShowCompletion(NextLevelScene() != null);
            }
        }

        // Takes an item out of the inventory and lets it exist in the world again (e.g. a fuse placed
        // into a socket can be picked up again later).
        public void ReleaseItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Held == id) Unequip(false);
            inventory.Remove(id);
            collected.Remove(id);
            RaiseChanged();
        }

        // Puts an item back where it was found (e.g. a wrong name tag returns to its hook).
        public void ReturnItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Held == id) Unequip(false);
            inventory.Remove(id);
            collected.Remove(id);
            Transform proto = GetPrototype(id);
            L2Pickup p = proto != null ? proto.GetComponent<L2Pickup>() : null;
            if (p != null) p.ReturnHome();
            RaiseChanged();
        }

        // The next level as listed in the Level Catalog (only when it is marked playable and is in Build Settings).
        public string NextLevelScene()
        {
            LevelCatalog cat = LevelCatalog.Instance;
            if (cat != null && cat.Find(SceneName) != null)
            {
                LevelCatalog.LevelInfo n = cat.NextAfter(SceneName);
                return LevelCatalog.CanLoad(n) ? n.sceneName : null;
            }
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next <= 0 || next >= SceneManager.sceneCountInBuildSettings) return null;
            return System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(next));
        }

        public bool HasSavedCheckpoint() => SaveSystem.HasCheckpoint(SaveSystem.Load(), SceneName);

        // Try Again: back to the latest checkpoint (exact saved state). Returns false if there is none.
        public bool TryAgainFromCheckpoint()
        {
            if (!HasSavedCheckpoint()) return false;
            SaveSystem.ResumeCheckpoint = true;
            if (!LoadScene(SceneName)) { SaveSystem.ResumeCheckpoint = false; return false; }
            return true;
        }

        // Reset Level: Level 2 only, back to its original state. Level 1 / session data is not touched.
        public void ResetLevel()
        {
            SaveData d = SaveSystem.Load();
            if (SaveSystem.HasCheckpoint(d, SceneName))
            {
                SaveSystem.ClearCheckpoint(d, SceneName);
                SaveSystem.Save(d);
            }
            SaveSystem.ResumeCheckpoint = false;
            LoadScene(SceneName);
        }

        public void StartFromBeginning()
        {
            SaveSystem.ResumeCheckpoint = false;
            LoadScene(SceneName);
        }

        public bool GoToNextLevel()
        {
            string next = NextLevelScene();
            return next != null && LoadScene(next);
        }

        public bool ExitToMenu()
        {
            Time.timeScale = 1f;
            GameFlowManager gm = GameFlowManager.Instance;
            if (gm != null) return gm.ExitToHome();
            return SceneLoader.Instance.LoadMainMenu();
        }

        bool LoadScene(string scene)
        {
            Time.timeScale = 1f;
            if (SceneLoader.IsLoading) return false;
            if (SceneLoader.Instance.LoadLevel(scene)) return true;
            if (!Application.CanStreamedLevelBeLoaded(scene)) return false;
            SceneManager.LoadScene(scene);
            return true;
        }
    }
}
