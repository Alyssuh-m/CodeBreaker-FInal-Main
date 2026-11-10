using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3.EditorTools
{
    // Code Breaker > Level 3 > Build Server Room
    // Builds the whole Level 3 scene from primitives and the project's own materials / fonts / sounds:
    // main Server Room (8 racks, central terminal, exit door), Maintenance (west), Security (east) and
    // Control (north) rooms with every puzzle station, lights, the player (copied from Level 2) and the
    // shared gameplay managers. Running it again rebuilds everything it made (idempotent).
    public static class Level3ServerRoomBuilder
    {
        const string ScenePath = "Assets/Scenes/Level 3.unity";
        const string Level2Scene = "Assets/Scenes/Level 2.unity";
        const string Root = "Assets/CodeBreaker/Level3";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string AudioDir = Root + "/Audio";
        const string IconDir = Root + "/Resources/L2Icons";

        const float H = 11.25f;      // ceiling height (3 m)
        const float T = 0.9f;        // wall thickness
        const float DoorH = 9f;      // doorway height
        const float Gap = 3f;        // half width of a doorway

        static StringBuilder log;
        static Transform level;
        static TMP_FontAsset fontHead, fontNote;

        // materials
        static Material mFloor, mWall, mCeil, mTrim, mMetalGray, mMetalBlack, mSteel, mMesh, mPlasticBlack, mRubber,
            mRed, mBlue, mGreen, mYellow, mPaper, mSticky, mWood, mScreen, mScreenOff, mBlack,
            eGreen, eRed, eBlue, eWhite, eAmber, eRedDim, eLampOff, mDiffuser;

        static AudioClip aHum, aSiren, aRumble, aPrint, aWrong, aCorrect, aHorror, aAmbience, aDoorHeavy;

        static readonly Dictionary<string, Transform> iconProtos = new Dictionary<string, Transform>();

        [MenuItem("Code Breaker/Level 3/Build Server Room")]
        public static void Build()
        {
            log = new StringBuilder();
            log.AppendLine("Level 3 Server Room build  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            iconProtos.Clear();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            foreach (GameObject r in scene.GetRootGameObjects())
                if (r.name == "Level3" || r.name == "Player" || r.name == "Level 3 Volume")
                    Object.DestroyImmediate(r);

            EnsureFolder(MatDir); EnsureFolder(TexDir); EnsureFolder(AudioDir); EnsureFolder(IconDir);
            fontHead = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CodeBreaker/UI/Fonts/Oswald-Medium SDF.asset");
            fontNote = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CodeBreaker/UI/Fonts/SpecialElite-Regular SDF.asset");
            Materials();
            Sounds();

            level = new GameObject("Level3").transform;
            SceneManager.MoveGameObjectToScene(level.gameObject, scene);

            Shell();
            Signs();
            L3RoomPower mainPower = MainRoom(out L3Keypad terminalKeypad, out L3Terminal terminal);
            L3RoomPower maint = Maintenance();
            Security();
            L3RoomPower control = Control();
            ExitDoor(terminalKeypad);

            // ---------------- managers
            Transform gp = Group("Gameplay", level, Vector3.zero, Vector3.back);
            var gmGo = new GameObject("Level 3 Game");
            gmGo.transform.SetParent(gp, false);
            L2Game game = gmGo.AddComponent<L2Game>();
            L2Audio audio = gmGo.AddComponent<L2Audio>();
            L2HeldItem held = gmGo.AddComponent<L2HeldItem>();
            L2UI ui = gmGo.AddComponent<L2UI>();
            L3Manager man = gmGo.AddComponent<L3Manager>();
            game.ui = ui; game.sfx = audio; game.heldView = held; game.rules = man;
            audio.doorOpen = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Wood Door Pack/Audio/Door_Open.wav");
            audio.doorClose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Wood Door Pack/Audio/Door_Close.wav");
            audio.success = aCorrect;
            ui.headingFont = fontHead; ui.bodyFont = fontHead; ui.noteFont = fontNote;
            ui.panelSprite = LoadSprite("CB_Panel"); ui.buttonSprite = LoadSprite("CB_Button"); ui.accentSprite = LoadSprite("CB_Accent");
            ui.dividerSprite = LoadSprite("CB_Divider"); ui.vignetteSprite = LoadSprite("CB_Vignette"); ui.trackSprite = LoadSprite("CB_ProgressTrack");
            ui.fillSprite = LoadSprite("CB_ProgressFill"); ui.handleSprite = LoadSprite("CB_SliderHandle");
            man.timeLimit = 900f;
            man.wrongClip = aWrong; man.correctClip = null; man.horrorClip = aHorror;

            var amb = new GameObject("Ambience");
            amb.transform.SetParent(gp, false);
            AudioSource ambSrc = amb.AddComponent<AudioSource>();
            ambSrc.clip = aAmbience; ambSrc.loop = true; ambSrc.playOnAwake = true; ambSrc.volume = 0.22f; ambSrc.spatialBlend = 0f;
            man.ambience = ambSrc;

            var cps = new List<L2Checkpoint>();
            string[] labels = { "Server Room (1 keycard)", "Server Room (2 keycards)", "Server Room (3 keycards)" };
            for (int i = 0; i < 3; i++)
            {
                var cpGo = new GameObject("Checkpoint_" + (i + 1));
                cpGo.transform.SetParent(gp, false);
                cpGo.transform.SetPositionAndRotation(new Vector3(0f, 2f, -16f), Quaternion.identity);
                var cp = cpGo.AddComponent<L2Checkpoint>();
                cp.index = i + 1; cp.label = labels[i]; cp.requiredFlag = ""; cp.autoTrigger = false;
                cps.Add(cp);
            }
            game.checkpoints = cps.ToArray();
            man.keycardCheckpoints = cps.ToArray();

            // ---------------- player
            Transform player = CopyPlayer(scene);
            if (player != null)
            {
                Camera cam = player.GetComponentInChildren<Camera>(true);
                game.player = player; game.playerCamera = cam; held.cam = cam;
                if (cam != null && cam.GetComponent<L2Interactor>() == null) cam.gameObject.AddComponent<L2Interactor>();
                CharacterController cc = player.GetComponent<CharacterController>();
                float lift = 2f;
                if (cc != null) lift = -(cc.center.y - cc.height * 0.5f) * player.lossyScale.y + 0.05f;
                player.SetPositionAndRotation(new Vector3(0f, lift, -17.5f), Quaternion.identity);
                log.AppendLine("Player copied from Level 2, spawn (0, " + lift.ToString("0.00") + ", -17.5) facing north");
            }
            else log.AppendLine("ERROR: Player not found in Level 2.");

            // ---------------- environment
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.14f, 0.17f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.05f);
            var vol = new GameObject("Level 3 Volume");
            SceneManager.MoveGameObjectToScene(vol, scene);
            Volume v = vol.AddComponent<Volume>();
            v.isGlobal = true;
            v.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/CodeBreaker/Settings/CodeBreaker_HorrorVolumeProfile.asset");

            RenderIcons();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Level3_BuildReport.txt", log.ToString());
            Debug.Log("[Level 3] Server Room built.\n" + log);
        }

        // ================================================================== shell
        static void Shell()
        {
            Transform s = Group("Shell", level, Vector3.zero, Vector3.back);
            // main server room 45 x 45
            Floor(s, "Main", -22.5f, 22.5f, -22.5f, 22.5f, mFloor);
            WallX(s, "Wall_Main_N", 22.5f, -22.95f, 22.95f, -Gap, Gap);
            WallX(s, "Wall_Main_S", -22.5f, -22.95f, 22.95f, -Gap, Gap);
            WallZ(s, "Wall_Main_W", -22.5f, -22.95f, 22.95f, -Gap, Gap);
            WallZ(s, "Wall_Main_E", 22.5f, -22.95f, 22.95f, -Gap, Gap);
            // maintenance (west)
            Floor(s, "Maintenance", -60f, -22.5f, -11.25f, 11.25f, mFloor);
            WallX(s, "Wall_Maint_N", 11.25f, -60.45f, -22.5f);
            WallX(s, "Wall_Maint_S", -11.25f, -60.45f, -22.5f);
            WallZ(s, "Wall_Maint_W", -60f, -11.7f, 11.7f);
            // security (east)
            Floor(s, "Security", 22.5f, 60f, -11.25f, 11.25f, mFloor);
            WallX(s, "Wall_Sec_N", 11.25f, 22.5f, 60.45f);
            WallX(s, "Wall_Sec_S", -11.25f, 22.5f, 60.45f);
            WallZ(s, "Wall_Sec_E", 60f, -11.7f, 11.7f);
            // control (north)
            Floor(s, "Control", -16.9f, 16.9f, 22.5f, 45f, mFloor);
            WallZ(s, "Wall_Ctrl_W", -16.9f, 22.5f, 45.45f);
            WallZ(s, "Wall_Ctrl_E", 16.9f, 22.5f, 45.45f);
            WallX(s, "Wall_Ctrl_N", 45f, -17.35f, 17.35f);
            // exit corridor (south)
            Floor(s, "ExitCorridor", -4f, 4f, -34f, -22.5f, mFloor);
            WallZ(s, "Wall_Exit_W", -4f, -34.45f, -22.5f);
            WallZ(s, "Wall_Exit_E", 4f, -34.45f, -22.5f);
            WallX(s, "Wall_Exit_S", -34f, -4.45f, 4.45f);
            log.AppendLine("Shell: main 45x45, maintenance west, security east, control north, exit corridor south");
        }

        static void Floor(Transform p, string name, float x0, float x1, float z0, float z1, Material m)
        {
            Box("Floor_" + name, p, new Vector3((x0 + x1) / 2f, -0.25f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.5f, z1 - z0), m);
            Box("Ceiling_" + name, p, new Vector3((x0 + x1) / 2f, H + 0.25f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.5f, z1 - z0), mCeil);
        }

        // Wall along x at depth z, from x0 to x1, with optional doorway gaps (pairs).
        static void WallX(Transform p, string name, float z, float x0, float x1, params float[] gaps)
        {
            float a = x0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                Box(name, p, new Vector3((a + gaps[i]) / 2f, H / 2f, z), new Vector3(gaps[i] - a, H, T), mWall);
                Box(name + "_Lintel", p, new Vector3((gaps[i] + gaps[i + 1]) / 2f, (DoorH + H) / 2f, z), new Vector3(gaps[i + 1] - gaps[i], H - DoorH, T), mWall);
                Box(name + "_JambTrim", p, new Vector3(gaps[i], DoorH / 2f, z), new Vector3(0.3f, DoorH, T + 0.12f), mTrim, false);
                Box(name + "_JambTrim", p, new Vector3(gaps[i + 1], DoorH / 2f, z), new Vector3(0.3f, DoorH, T + 0.12f), mTrim, false);
                a = gaps[i + 1];
            }
            Box(name, p, new Vector3((a + x1) / 2f, H / 2f, z), new Vector3(x1 - a, H, T), mWall);
        }

        static void WallZ(Transform p, string name, float x, float z0, float z1, params float[] gaps)
        {
            float a = z0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                Box(name, p, new Vector3(x, H / 2f, (a + gaps[i]) / 2f), new Vector3(T, H, gaps[i] - a), mWall);
                Box(name + "_Lintel", p, new Vector3(x, (DoorH + H) / 2f, (gaps[i] + gaps[i + 1]) / 2f), new Vector3(T, H - DoorH, gaps[i + 1] - gaps[i]), mWall);
                Box(name + "_JambTrim", p, new Vector3(x, DoorH / 2f, gaps[i]), new Vector3(T + 0.12f, DoorH, 0.3f), mTrim, false);
                Box(name + "_JambTrim", p, new Vector3(x, DoorH / 2f, gaps[i + 1]), new Vector3(T + 0.12f, DoorH, 0.3f), mTrim, false);
                a = gaps[i + 1];
            }
            Box(name, p, new Vector3(x, H / 2f, (a + z1) / 2f), new Vector3(T, H, z1 - a), mWall);
        }

        static void Signs()
        {
            Transform s = Group("Signs", level, Vector3.zero, Vector3.back);
            Sign(s, "MAINTENANCE", new Vector3(-22.0f, 10.1f, 0f), Vector3.right, new Color(1f, 0.7f, 0.3f));
            Sign(s, "SECURITY", new Vector3(22.0f, 10.1f, 0f), Vector3.left, new Color(0.45f, 0.65f, 1f));
            Sign(s, "CONTROL ROOM", new Vector3(0f, 10.1f, 22.0f), Vector3.back, new Color(0.45f, 1f, 0.55f));
            Sign(s, "EXIT", new Vector3(0f, 10.1f, -22.0f), Vector3.forward, new Color(1f, 0.25f, 0.2f));
        }

        static void Sign(Transform p, string text, Vector3 pos, Vector3 toward, Color c)
        {
            Transform g = Group("Sign_" + text, p, pos, toward);
            Box("SignPlate", g, new Vector3(0f, 0f, 0.02f), new Vector3(6.2f, 1.4f, 0.08f), mPlasticBlack, false);
            Text3D(g, "Text", text, new Vector3(0f, 0f, -0.04f), new Vector2(6f, 1.2f), 9f, c, fontHead);
        }

        // ================================================================== main server room
        static L3RoomPower MainRoom(out L3Keypad termKeypad, out L3Terminal terminal)
        {
            Transform m = Group("ServerRoom", level, Vector3.zero, Vector3.back);
            Transform lights = Group("Lights", m, Vector3.zero, Vector3.back);

            // blue wall lights on both side walls
            foreach (float z in new[] { -16f, -9f, 9f, 16f })
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float x = side * (22.5f - T / 2f - 0.08f);
                    Box("BlueWallLight", lights, new Vector3(x, 8.4f, z), new Vector3(0.16f, 0.45f, 3.2f), eBlue, false);
                    NewLight(lights, "BlueWallLight_Point", new Vector3(side * 20.6f, 8.2f, z), new Color(0.2f, 0.45f, 1f), 6f, 16f);
                }
            }
            foreach (float z in new[] { -8f, 8f })
            {
                Box("CeilingPanel", lights, new Vector3(0f, H - 0.05f, z), new Vector3(3f, 0.1f, 1.2f), mDiffuser, false);
                NewLight(lights, "CeilingLight", new Vector3(0f, H - 0.8f, z), new Color(0.75f, 0.85f, 1f), 4.5f, 28f);
            }

            // racks: 4 pairs, 5 green and 3 red
            Transform racks = Group("Racks", m, Vector3.zero, Vector3.back);
            var pairs = new (float x, float z, bool a, bool b)[]
            {
                (-11f, 8f, true, false),   // NW: G R
                (11f, 8f, true, true),     // NE: G G
                (-11f, -3f, false, true),  // SW: R G
                (11f, -3f, true, false),   // SE: G R
            };
            int n = 0, green = 0;
            foreach (var p in pairs)
                foreach (int k in new[] { 0, 1 })
                {
                    n++;
                    bool isGreen = k == 0 ? p.a : p.b;
                    if (isGreen) green++;
                    Rack(racks, n, new Vector3(p.x + (k == 0 ? -1.3f : 1.3f), 0f, p.z), isGreen);
                }
            log.AppendLine("Racks: " + n + " (" + green + " green, " + (n - green) + " red)");

            // central terminal (faces south, the way the player comes in)
            Transform t = Group("CentralTerminal", m, new Vector3(0f, 0f, -11f), Vector3.back);
            Box("TerminalDesk_Body", t, new Vector3(0f, 1.4f, 0f), new Vector3(8f, 2.8f, 3f), mMetalBlack);
            Box("TerminalDesk_Top", t, new Vector3(0f, 2.9f, 0f), new Vector3(8.2f, 0.2f, 3.2f), mMetalGray, false);
            Box("MainScreen_Frame", t, new Vector3(0f, 5.5f, 0.95f), new Vector3(5.6f, 3.4f, 0.3f), mPlasticBlack);
            Box("MainScreen_Stand", t, new Vector3(0f, 3.5f, 1.0f), new Vector3(0.5f, 1.4f, 0.3f), mPlasticBlack, false);
            Quad("MainScreen", t, new Vector3(0f, 5.5f, 0.79f), new Vector2(5.3f, 3.1f), mScreen);
            TextMeshPro screen = Text3D(t, "MainScreen_Text", "INSERT KEYCARDS", new Vector3(0f, 5.5f, 0.76f), new Vector2(5f, 2.9f), 2.6f, new Color(0.55f, 0.9f, 1f), fontNote, TextAlignmentOptions.TopLeft);
            NewLight(t, "ScreenGlow", t.TransformPoint(new Vector3(0f, 5.4f, -1.2f)), new Color(0.4f, 0.75f, 1f), 1.2f, 7f);
            terminal = t.gameObject.AddComponent<L3Terminal>();
            terminal.screen = screen;

            string[] cards = { L2Items.KeycardRed, L2Items.KeycardBlue, L2Items.KeycardGreen };
            string[] names = { "RED", "BLUE", "GREEN" };
            string[] flags = { "card_red_in", "card_blue_in", "card_green_in" };
            string[] values = { "m = 9", "s = 4", "c = 5" };
            Material[] mats = { mRed, mBlue, mGreen };
            for (int i = 0; i < 3; i++)
            {
                Transform sl = Group("CardSlot_" + names[i], t, t.TransformPoint(new Vector3(-2.6f + i * 1.3f, 3.0f, -0.8f)), Vector3.back);
                Box("SlotBody", sl, new Vector3(0f, 0.15f, 0f), new Vector3(1f, 0.3f, 0.75f), mats[i], false);
                Box("SlotSlit", sl, new Vector3(0f, 0.31f, 0f), new Vector3(0.7f, 0.02f, 0.08f), mBlack, false);
                Text3D(sl, "Label", names[i], new Vector3(0f, 0.15f, -0.39f), new Vector2(1f, 0.3f), 1.6f, Color.white, fontHead);
                GameObject inserted = Box("InsertedCard", sl, new Vector3(0f, 0.55f, 0f), new Vector3(0.62f, 0.5f, 0.03f), mats[i], false);
                var slot = sl.gameObject.AddComponent<L3CardSlot>();
                slot.displayName = names[i] + " Slot";
                slot.cardId = cards[i]; slot.colorName = names[i]; slot.insertedFlag = flags[i]; slot.valueText = values[i];
                slot.insertedVisual = inserted; slot.terminal = terminal;
                Trigger(sl.gameObject, new Vector3(0f, 0.3f, 0f), new Vector3(1.15f, 0.9f, 1f));
                inserted.SetActive(false);
            }

            termKeypad = KeypadObject(t, "Terminal Keypad", t.TransformPoint(new Vector3(2.4f, 3.0f, -0.6f)), Vector3.back, true);
            termKeypad.station = "term";
            termKeypad.title = "CENTRAL TERMINAL";
            termKeypad.code = "m = 9;  s = 4;  c = 5;   // filled in by the keycards\n\nif (m > s && c != 0)\n    exitCode = m * 10 + s;\nelse\n    exitCode = 0;\n// Enter exitCode";
            termKeypad.answer = "94";
            termKeypad.maxLength = 4;
            termKeypad.hint = "Check the condition first, then do the math in order.";
            termKeypad.penalty = 15f;
            termKeypad.requiredFlags = new[] { "card_red_in", "card_blue_in", "card_green_in" };
            termKeypad.lockedMessage = "The terminal keypad is locked. Insert all three keycards first.";
            termKeypad.successDisplay = "94";
            termKeypad.successMessage = "exitCode = 94. ACCESS GRANTED. The exit door is opening!";
            termKeypad.verb = "Use";
            terminal.keypad = termKeypad;

            // flashlight on the desk
            Transform fl = Group("Flashlight", t, t.TransformPoint(new Vector3(-3.5f, 3.15f, 0.3f)), Vector3.back);
            GameObject body = Cyl("Body", fl, new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.45f, 0.22f), mMetalBlack);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject head = Cyl("Head", fl, new Vector3(0f, 0f, -0.5f), new Vector3(0.34f, 0.12f, 0.34f), mSteel);
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject lens = Cyl("Lens", fl, new Vector3(0f, 0f, -0.63f), new Vector3(0.28f, 0.01f, 0.28f), eWhite);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var flp = fl.gameObject.AddComponent<L2Pickup>();
            flp.itemId = L2Items.Flashlight; flp.displayName = "Flashlight";
            Trigger(fl.gameObject, new Vector3(0f, 0f, -0.2f), new Vector3(0.6f, 0.6f, 1.4f));
            iconProtos[L2Items.Flashlight] = fl;

            // floor papers beside the terminal
            Paper(m, "Paper_Keycards", new Vector3(-6f, 0.03f, -14f), 20f, "Your keycards remember what you earned.");
            Paper(m, "Paper_Servers", new Vector3(6f, 0.03f, -14.5f), -15f, "The servers don't lie. Count their lights.");

            // server hum (starts with the reboot)
            var hum = new GameObject("ServerHum");
            hum.transform.SetParent(m, false);
            hum.transform.position = new Vector3(0f, 6f, 3f);
            AudioSource hs = hum.AddComponent<AudioSource>();
            hs.clip = aHum; hs.loop = true; hs.playOnAwake = false; hs.volume = 0.35f; hs.spatialBlend = 0.6f; hs.maxDistance = 80f;
            var power = hum.AddComponent<L3RoomPower>();
            power.poweredFlag = "power_servers";
            power.humLoop = hs;
            power.flickerWhileDim = false;
            return power;
        }

        static void Rack(Transform p, int n, Vector3 pos, bool green)
        {
            Transform r = Group("Rack_" + n + (green ? "_Green" : "_Red"), p, pos, Vector3.back);
            Box("RackBody", r, new Vector3(0f, 3.75f, 0f), new Vector3(2.4f, 7.5f, 3.6f), mMetalBlack);
            Box("RackFrontMesh", r, new Vector3(0f, 3.6f, -1.82f), new Vector3(2.1f, 6.6f, 0.04f), mMesh, false);
            for (int u = 0; u < 7; u++)
                Box("Unit", r, new Vector3(0f, 1.0f + u * 0.9f, -1.79f), new Vector3(1.9f, 0.6f, 0.02f), mPlasticBlack, false);
            // the status light everyone counts: a big bar at the top of the front, plus a small glow
            Box("StatusLight", r, new Vector3(0f, 7.15f, -1.86f), new Vector3(1.6f, 0.3f, 0.06f), green ? eGreen : eRed, false);
            for (int u = 0; u < 7; u++)
                Box("Led", r, new Vector3(0.75f, 1.0f + u * 0.9f, -1.82f), new Vector3(0.08f, 0.08f, 0.02f), green ? eGreen : eRedDim, false);
            NewLight(r, "StatusGlow", r.TransformPoint(new Vector3(0f, 7.1f, -2.4f)), green ? new Color(0.2f, 1f, 0.35f) : new Color(1f, 0.15f, 0.1f), 1.6f, 4f);
            var ex = r.gameObject.AddComponent<L2Examine>();
            ex.displayName = "Server Rack";
            ex.title = "SERVER RACK " + n;
            ex.body = green ? "The status light on top is <color=#5FD383>GREEN</color>.\nThis server is online." : "The status light on top is <color=#F05A52>RED</color>.\nThis server is offline.";
            ex.verb = "Check Server Rack";
        }

        static void Paper(Transform p, string name, Vector3 pos, float yaw, string text)
        {
            var g = new GameObject(name);
            g.transform.SetParent(p, false);
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Box("Sheet", g.transform, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.02f, 2.1f), mPaper, false);
            TextMeshPro tx = Text3D(g.transform, "Text", text, new Vector3(0f, 0.02f, 0f), new Vector2(1.4f, 1.8f), 1.6f, new Color(0.12f, 0.1f, 0.1f), fontNote);
            tx.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ex = g.AddComponent<L2Examine>();
            ex.displayName = "Paper"; ex.title = "A NOTE ON THE FLOOR"; ex.body = "\"" + text + "\""; ex.verb = "Read the Paper";
            Trigger(g, new Vector3(0f, 0.3f, 0f), new Vector3(1.9f, 0.8f, 2.4f));
        }

        // ================================================================== maintenance (west): variables
        static L3RoomPower Maintenance()
        {
            Transform m = Group("Maintenance", level, Vector3.zero, Vector3.back);
            Transform lights = Group("Lights", m, Vector3.zero, Vector3.back);
            var ls = new List<Light>();
            var glow = new List<Renderer>();
            foreach (float x in new[] { -50f, -34f })
            {
                glow.Add(Box("CeilingLamp", lights, new Vector3(x, H - 0.05f, 0f), new Vector3(3f, 0.1f, 1.2f), mDiffuser, false).GetComponent<Renderer>());
                ls.Add(NewLight(lights, "CeilingLight", new Vector3(x, H - 0.8f, 0f), new Color(1f, 0.86f, 0.68f), 9f, 32f));
            }
            var power = m.gameObject.AddComponent<L3RoomPower>();
            power.poweredFlag = "power_maintenance";
            power.lights = ls.ToArray();
            power.glow = glow.ToArray();
            power.dimFactor = 0.22f;
            power.powerOnSound = aRumble;

            // ---- Station 1: three labelled lockers (west wall) + laptop on the workbench
            int[] cards = { 8, 2, 5 };
            for (int i = 0; i < 3; i++)
            {
                Transform g = Group("Locker" + (i + 1), m, new Vector3(-60f + T / 2f, 0f, -5f + i * 5f), Vector3.right);
                Transform door = Cabinet(g, new Vector3(0f, 5.4f, 0f), new Vector3(3.2f, 3.6f, 1.8f), mMetalGray, mMetalGray, out Transform inside);
                Text3D(g, "Label", "locker" + (i + 1), new Vector3(0f, 7.75f, -0.05f), new Vector2(3f, 0.8f), 5f, new Color(0.95f, 0.9f, 0.75f), fontHead);
                Text3D(door, "DoorLabel", "locker" + (i + 1), new Vector3(1.6f, 0.9f, -0.06f), new Vector2(2.6f, 0.6f), 3.5f, new Color(0.15f, 0.15f, 0.15f), fontHead);
                Box("Handle", door, new Vector3(2.85f, 0f, -0.12f), new Vector3(0.12f, 0.6f, 0.12f), mSteel, false);
                Box("NumberCard", inside, new Vector3(0f, -0.2f, 0.55f), new Vector3(1.4f, 1.9f, 0.04f), mPaper, false);
                Text3D(inside, "Number", cards[i].ToString(), new Vector3(0f, -0.2f, 0.52f), new Vector2(1.3f, 1.8f), 14f, new Color(0.1f, 0.1f, 0.1f), fontHead);
                var o = door.gameObject.AddComponent<L2Openable>();
                o.kind = L2Openable.Kind.Door; o.label = "locker" + (i + 1); o.openAngle = 105f; o.displayName = "locker" + (i + 1);
                Trigger(door.gameObject, new Vector3(1.6f, 0f, -0.05f), new Vector3(3.2f, 3.6f, 0.3f));
            }

            Transform bench = Group("Workbench", m, new Vector3(-44f, 0f, 11.25f - T / 2f), Vector3.back);
            Box("BenchTop", bench, new Vector3(0f, 3.3f, -1.4f), new Vector3(7.5f, 0.2f, 2.6f), mWood);
            foreach (float lx in new[] { -3.5f, 3.5f })
                foreach (float lz in new[] { -0.3f, -2.5f })
                    Box("BenchLeg", bench, new Vector3(lx, 1.6f, lz), new Vector3(0.2f, 3.2f, 0.2f), mMetalBlack, false);
            Box("BenchShelf", bench, new Vector3(0f, 0.9f, -1.4f), new Vector3(7.2f, 0.12f, 2.3f), mMetalGray, false);
            // laptop
            Transform lap = Group("Laptop", bench, bench.TransformPoint(new Vector3(0.9f, 3.4f, -1.5f)), Vector3.back);
            Box("Base", lap, new Vector3(0f, 0.04f, 0f), new Vector3(1.6f, 0.08f, 1.1f), mPlasticBlack, false);
            Transform lid = Group("Lid", lap, lap.TransformPoint(new Vector3(0f, 0.08f, 0.55f)), Vector3.back);
            lid.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Box("LidShell", lid, new Vector3(0f, 0.5f, 0.02f), new Vector3(1.6f, 1.0f, 0.05f), mPlasticBlack, false);
            Quad("LidScreen", lid, new Vector3(0f, 0.5f, -0.01f), new Vector2(1.45f, 0.88f), mScreen);
            Text3D(lid, "LidText", "locker2 = locker1;\nlocker1 = locker3;\n// What is in locker2 now?", new Vector3(0f, 0.5f, -0.02f), new Vector2(1.35f, 0.8f), 1.2f, new Color(0.55f, 0.9f, 1f), fontNote, TextAlignmentOptions.Left);
            L3Keypad laptop = lap.gameObject.AddComponent<L3Keypad>();
            laptop.displayName = "Laptop";
            laptop.station = "m1"; laptop.title = "RAMOS'S LAPTOP";
            laptop.code = "locker2 = locker1;\nlocker1 = locker3;\n// What is in locker2 now?";
            laptop.answer = "8"; laptop.maxLength = 3;
            laptop.hint = "Run the lines top to bottom. Once copied, a value stays put.";
            laptop.successDisplay = "8"; laptop.successMessage = "locker2 kept 8. Copying a value doesn't link the boxes.";
            laptop.verb = "Use";
            Trigger(lap.gameObject, new Vector3(0f, 0.5f, 0.2f), new Vector3(1.8f, 1.2f, 1.4f));
            // sticky note next to the laptop
            StickyNote(bench, "StickyNote_Laptop", bench.TransformPoint(new Vector3(-1.2f, 3.42f, -1.6f)), "Copying a value doesn't link the boxes.");

            // ---- Station 2: pegboard with name tags (north wall, left) + red locker (south wall)
            Transform peg = Group("Pegboard", m, new Vector3(-54f, 0f, 11.25f - T / 2f), Vector3.back);
            Box("Board", peg, new Vector3(0f, 6.2f, -0.1f), new Vector3(6.4f, 4.2f, 0.15f), mWood, false);
            Text3D(peg, "Title", "NAME TAGS", new Vector3(0f, 8.0f, -0.2f), new Vector2(5f, 0.6f), 4f, new Color(0.15f, 0.12f, 0.1f), fontHead);
            var tagDefs = new (string id, string text)[]
            {
                (L2Items.Tag2Fast, "2fast = 3"), (L2Items.TagMyVar, "my var = 1"),
                (L2Items.TagClass, "class = 7"), (L2Items.TagFanSpeed, "fanSpeed = 6"),
            };
            for (int i = 0; i < 4; i++)
            {
                float x = -2.25f + i * 1.5f;
                Cyl("Hook", peg, new Vector3(x, 6.75f, -0.3f), new Vector3(0.08f, 0.2f, 0.08f), mSteel).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Transform tag = Group("Tag_" + tagDefs[i].id, peg, peg.TransformPoint(new Vector3(x, 6.2f, -0.3f)), Vector3.back);
                Box("Plate", tag, Vector3.zero, new Vector3(1.35f, 0.55f, 0.05f), mYellow, false);
                Text3D(tag, "Text", tagDefs[i].text, new Vector3(0f, 0f, -0.035f), new Vector2(1.25f, 0.45f), 2.2f, new Color(0.1f, 0.1f, 0.1f), fontNote);
                var pk = tag.gameObject.AddComponent<L2Pickup>();
                pk.itemId = tagDefs[i].id; pk.displayName = "Name Tag";
                Trigger(tag.gameObject, Vector3.zero, new Vector3(1.4f, 0.75f, 0.45f));
                iconProtos[tagDefs[i].id] = tag;
            }

            Transform rl = Group("RedLocker", m, new Vector3(-50f, 0f, -11.25f + T / 2f), Vector3.forward);
            Transform rdoor = Cabinet(rl, new Vector3(0f, 4.25f, 0f), new Vector3(3.6f, 8.4f, 2.4f), mRed, mRed, out Transform rin);
            Box("Handle", rdoor, new Vector3(3.25f, 0f, -0.12f), new Vector3(0.14f, 0.9f, 0.14f), mSteel, false);
            Box("NotePaper", rdoor, new Vector3(1.8f, 1.6f, -0.06f), new Vector3(2.2f, 1.3f, 0.02f), mPaper, false);
            Text3D(rdoor, "NoteText", "\"Only a name C# will accept can open me.\"", new Vector3(1.8f, 1.6f, -0.08f), new Vector2(2.0f, 1.1f), 2.2f, new Color(0.12f, 0.1f, 0.1f), fontNote);
            Box("Bolt", rdoor, new Vector3(3.3f, -0.6f, -0.15f), new Vector3(0.6f, 0.25f, 0.2f), mSteel, false);
            var rdo = rdoor.gameObject.AddComponent<L2Openable>();
            rdo.kind = L2Openable.Kind.Door; rdo.label = "Red Locker"; rdo.openAngle = 105f; rdo.displayName = "Red Locker";
            Trigger(rdoor.gameObject, new Vector3(1.8f, 0f, -0.05f), new Vector3(3.6f, 8.4f, 0.3f));
            rdo.enabled = false;
            // log page inside
            GameObject log1 = Box("LogPage", rin, new Vector3(0f, 0.6f, 0.95f), new Vector3(1.8f, 2.3f, 0.03f), mPaper, false);
            Text3D(log1.transform.parent, "LogPageText", "RAMOS - LOG\n\ntemp = x;\nx = y;\ny = temp;", new Vector3(0f, 0.6f, 0.92f), new Vector2(1.6f, 2.1f), 2.4f, new Color(0.1f, 0.1f, 0.12f), fontNote).transform.SetParent(log1.transform, true);
            var logEx = log1.AddComponent<L2Examine>();
            logEx.displayName = "Log Page"; logEx.title = "RAMOS'S LOG PAGE"; logEx.verb = "Read the Log Page";
            logEx.body = "temp = x;\nx = y;\ny = temp;";
            Trigger(log1, Vector3.zero, new Vector3(1.2f, 1.2f, 6f));
            log1.SetActive(false);
            // name tag reader on the wall beside the locker
            Transform reader = Group("NameTagReader", rl, rl.TransformPoint(new Vector3(2.9f, 5.2f, 0f)), Vector3.forward);
            Box("ReaderBody", reader, new Vector3(0f, 0f, -0.15f), new Vector3(1.6f, 2f, 0.3f), mMetalBlack, false);
            Quad("ReaderScreen", reader, new Vector3(0f, 0.25f, -0.31f), new Vector2(1.35f, 1f), mScreen);
            TextMeshPro rtext = Text3D(reader, "ReaderText", "LOCKED\nINSERT NAME TAG", new Vector3(0f, 0.25f, -0.32f), new Vector2(1.3f, 0.95f), 2f, new Color(1f, 0.75f, 0.3f), fontHead);
            Text3D(reader, "ReaderLabel", "NAME TAG READER", new Vector3(0f, -0.62f, -0.31f), new Vector2(1.5f, 0.4f), 1.6f, new Color(0.8f, 0.8f, 0.8f), fontHead);
            var lockGo = new GameObject("RedLocker_Lock");
            lockGo.transform.SetParent(rl, false);
            var tagLock = lockGo.AddComponent<L3NameTagLock>();
            tagLock.displayName = "Red Locker"; tagLock.screen = rtext;
            var bc1 = lockGo.AddComponent<BoxCollider>(); bc1.isTrigger = true; bc1.center = new Vector3(0f, 4.25f, -2.55f); bc1.size = new Vector3(3.6f, 8.4f, 0.35f);
            var bc2 = lockGo.AddComponent<BoxCollider>(); bc2.isTrigger = true; bc2.center = new Vector3(2.9f, 5.2f, -0.3f); bc2.size = new Vector3(1.8f, 2.2f, 0.6f);
            var openRed = lockGo.AddComponent<L3OpenPart>(); openRed.part = rdo;
            var showLog = lockGo.AddComponent<L3ShowObjects>(); showLog.show = new[] { log1 };
            tagLock.onSolved = new L3Reaction[] { openRed, showLog };

            // ---- Station 3: fuse box (south wall)
            Transform fb = Group("FuseBox", m, new Vector3(-37f, 0f, -11.25f + T / 2f), Vector3.forward);
            Box("BoxBody", fb, new Vector3(0f, 5.4f, -0.4f), new Vector3(5.4f, 4.2f, 0.8f), mMetalGray, false);
            Text3D(fb, "Title", "FUSE BOX", new Vector3(0f, 7.15f, -0.82f), new Vector2(4f, 0.5f), 3f, new Color(0.95f, 0.85f, 0.5f), fontHead);
            var box = fb.gameObject.AddComponent<L3FuseBox>();
            string[] sl = { "x", "y", "temp" };
            var sockets = new L3FuseSocket[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -1.6f + i * 1.6f;
                Transform so = Group("Socket_" + sl[i], fb, fb.TransformPoint(new Vector3(x, 5.6f, -0.82f)), Vector3.forward);
                Box("Recess", so, new Vector3(0f, 0f, 0.02f), new Vector3(0.8f, 1.5f, 0.04f), mBlack, false);
                Box("ClipTop", so, new Vector3(0f, 0.62f, -0.05f), new Vector3(0.5f, 0.12f, 0.1f), mSteel, false);
                Box("ClipBottom", so, new Vector3(0f, -0.62f, -0.05f), new Vector3(0.5f, 0.12f, 0.1f), mSteel, false);
                Text3D(so, "Label", sl[i], new Vector3(0f, 1.05f, -0.03f), new Vector2(1.4f, 0.5f), 3.2f, Color.white, fontHead);
                var anchor = new GameObject("Anchor").transform;
                anchor.SetParent(so, false);
                anchor.localPosition = new Vector3(0f, 0f, -0.12f);
                var sock = so.gameObject.AddComponent<L3FuseSocket>();
                sock.label = sl[i]; sock.anchor = anchor; sock.displayName = "Socket " + sl[i];
                Trigger(so.gameObject, new Vector3(0f, 0f, -0.2f), new Vector3(1.3f, 1.9f, 0.6f));
                sockets[i] = sock;
            }
            box.sockets = sockets;
            box.fuse2 = Fuse(fb, "Fuse_2", "2");
            box.fuse9 = Fuse(fb, "Fuse_9", "9");
            iconProtos[L2Items.Fuse2] = box.fuse2;
            iconProtos[L2Items.Fuse9] = box.fuse9;
            Quad("Display", fb, new Vector3(-1.1f, 4.0f, -0.81f), new Vector2(2.6f, 0.6f), mScreen);
            box.display = Text3D(fb, "DisplayText", "x:2  y:9  temp:--", new Vector3(-1.1f, 4.0f, -0.82f), new Vector2(2.5f, 0.55f), 2f, new Color(1f, 0.75f, 0.3f), fontHead);
            Transform test = Group("TestButton", fb, fb.TransformPoint(new Vector3(1.5f, 4.0f, -0.82f)), Vector3.forward);
            GameObject cap = Cyl("Cap", test, new Vector3(0f, 0f, -0.08f), new Vector3(0.5f, 0.08f, 0.5f), mRed);
            cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Text3D(test, "Label", "TEST", new Vector3(0f, -0.48f, 0f), new Vector2(1f, 0.35f), 2f, Color.white, fontHead);
            var tb = test.gameObject.AddComponent<L3Button>();
            tb.displayName = "TEST"; tb.verb = "Press"; tb.fuseBox = box; tb.cap = cap.transform;
            Trigger(test.gameObject, new Vector3(0f, 0f, -0.1f), new Vector3(0.9f, 0.9f, 0.6f));
            box.panelLight = NewLight(fb, "PanelLight", fb.TransformPoint(new Vector3(0f, 5.4f, -1.6f)), new Color(0.3f, 1f, 0.45f), 1.4f, 4f);
            box.panelLight.enabled = false;
            box.sparkLight = NewLight(fb, "SparkLight", fb.TransformPoint(new Vector3(0f, 5.6f, -1.2f)), new Color(1f, 0.9f, 0.6f), 4f, 6f);
            box.sparkLight.enabled = false;

            // ---- Final lock: generator (tall cabinet on the north wall) + printer
            Transform gen = Group("Generator", m, new Vector3(-31f, 0f, 11.25f - T / 2f), Vector3.back);
            Box("GeneratorBody", gen, new Vector3(0f, 4.1f, -1.6f), new Vector3(5f, 8.2f, 3f), mMetalGray);
            Box("Vents", gen, new Vector3(0f, 2.0f, -3.12f), new Vector3(4f, 2.2f, 0.04f), mMesh, false);
            Box("Stripe", gen, new Vector3(0f, 7.4f, -3.12f), new Vector3(5f, 0.3f, 0.04f), mYellow, false);
            Text3D(gen, "Title", "BACKUP GENERATOR", new Vector3(0f, 6.9f, -3.15f), new Vector2(4.6f, 0.6f), 3.4f, new Color(0.95f, 0.9f, 0.8f), fontHead);
            Transform riddle = Group("Riddle", gen, gen.TransformPoint(new Vector3(-1.2f, 5.0f, -3.12f)), Vector3.back);
            Box("Sheet", riddle, Vector3.zero, new Vector3(2.2f, 1.8f, 0.02f), mPaper, false);
            Text3D(riddle, "Text", "Start code: what locker2 kept, the value of the only valid name, and the fuse that moved into x.", new Vector3(0f, 0f, -0.02f), new Vector2(2f, 1.6f), 1.9f, new Color(0.1f, 0.1f, 0.12f), fontNote);
            var rEx = riddle.gameObject.AddComponent<L2Examine>();
            rEx.displayName = "Riddle"; rEx.title = "TAPED TO THE GENERATOR"; rEx.verb = "Read the Note";
            rEx.body = "\"Start code: what locker2 kept, the value of the only valid name, and the fuse that moved into x.\"";
            Trigger(riddle.gameObject, Vector3.zero, new Vector3(2.3f, 1.9f, 0.4f));
            L3Keypad gk = KeypadObject(gen, "Generator Keypad", gen.TransformPoint(new Vector3(1.4f, 5.0f, -3.1f)), Vector3.back, false);
            gk.station = "gen"; gk.title = "GENERATOR KEYPAD";
            gk.code = "\"Start code: what locker2 kept, the value of the only valid name, and the fuse that moved into x.\"\n\n// Enter the start code";
            gk.answer = "869"; gk.maxLength = 4;
            gk.hint = "Three digits: locker2's value, the valid name's value, then the fuse now in x.";
            gk.requiredFlags = new[] { "solved_m1", "solved_m2", "solved_m3" };
            gk.lockedMessage = "The generator keypad is dead. Finish Ramos's three jobs first: the laptop, the red locker and the fuse box.";
            gk.wrongMessage = "The generator coughs and dies.  -10 s";
            gk.successDisplay = "869"; gk.successMessage = "The generator rumbles to life! The lights come on and the printer starts.";
            gk.verb = "Use";
            Quad("GenScreen", gen, new Vector3(1.4f, 6.25f, -3.12f), new Vector2(1.6f, 0.6f), mScreen);
            gk.screen = Text3D(gen, "GenScreenText", "OFFLINE", new Vector3(1.4f, 6.25f, -3.13f), new Vector2(1.5f, 0.55f), 2f, new Color(1f, 0.4f, 0.3f), fontHead);
            gk.screenLocked = "OFFLINE"; gk.screenReady = "ENTER CODE"; gk.screenSolved = "RUNNING";
            // printer next to the generator
            Transform pr = Group("GeneratorPrinter", m, new Vector3(-26.4f, 0f, 11.25f - T / 2f - 1.2f), Vector3.back);
            Box("Stand", pr, new Vector3(0f, 1.5f, 0f), new Vector3(2f, 3f, 2f), mMetalBlack);
            Box("PrinterBody", pr, new Vector3(0f, 3.4f, 0.1f), new Vector3(1.7f, 0.8f, 1.4f), mPlasticBlack, false);
            Box("Tray", pr, new Vector3(0f, 3.05f, -0.75f), new Vector3(1.3f, 0.06f, 0.6f), mMetalGray, false);
            Text3D(pr, "Label", "PRINTER", new Vector3(0f, 3.5f, -0.62f), new Vector2(1.5f, 0.3f), 1.5f, new Color(0.8f, 0.8f, 0.8f), fontHead);
            Transform red = Keycard(pr, "Keycard_Red", pr.TransformPoint(new Vector3(0f, 3.12f, -0.8f)), mRed, "MAINT: m = 9", L2Items.KeycardRed);
            var printRed = pr.gameObject.AddComponent<L3ShowObjects>();
            printRed.show = new[] { red.gameObject }; printRed.slideFrom = pr.rotation * new Vector3(0f, 0f, 0.7f); printRed.delay = 1.5f; printRed.duration = 1.6f;
            printRed.sound = aPrint; printRed.message = "The printer ejects the RED keycard.";
            red.gameObject.SetActive(false);
            gk.onSolved = new L3Reaction[] { power, printRed };

            log.AppendLine("Maintenance: lockers 8/2/5, laptop (8), name tags + red locker (fanSpeed), fuse box (x=9,y=2), generator 869 -> RED keycard");
            return power;
        }

        static Transform Fuse(Transform p, string name, string digit)
        {
            Transform f = new GameObject(name).transform;
            f.SetParent(p, false);
            Cyl("Body", f, Vector3.zero, new Vector3(0.36f, 0.45f, 0.36f), mPaper);
            Cyl("CapTop", f, new Vector3(0f, 0.47f, 0f), new Vector3(0.38f, 0.06f, 0.38f), mSteel);
            Cyl("CapBottom", f, new Vector3(0f, -0.47f, 0f), new Vector3(0.38f, 0.06f, 0.38f), mSteel);
            Text3D(f, "Digit", digit, new Vector3(0f, 0f, -0.19f), new Vector2(0.4f, 0.6f), 4.5f, new Color(0.8f, 0.1f, 0.1f), fontHead);
            return f;
        }

        // ================================================================== security (east): operators
        static void Security()
        {
            Transform s = Group("Security", level, Vector3.zero, Vector3.back);
            Transform lights = Group("Lights", s, Vector3.zero, Vector3.back);
            foreach (float x in new[] { 33f, 50f })
            {
                Box("CeilingLamp", lights, new Vector3(x, H - 0.05f, 0f), new Vector3(3f, 0.1f, 1.2f), mDiffuser, false);
                NewLight(lights, "CeilingLight", new Vector3(x, H - 0.8f, 0f), new Color(0.8f, 0.88f, 1f), 8f, 30f);
            }

            // ---- Station 1: camera wall (north) + guard desk with sticky note and six buttons
            Transform w = Group("CameraWall", s, new Vector3(41f, 0f, 11.25f - T / 2f), Vector3.back);
            var wall = w.gameObject.AddComponent<L3CameraWall>();
            var feeds = new (int motion, string door)[] { (1, "OPEN"), (7, "CLOSED"), (3, "OPEN"), (5, "OPEN"), (0, "CLOSED"), (4, "CLOSED") };
            wall.screens = new Transform[6];
            wall.overlays = new TMP_Text[6];
            for (int i = 0; i < 6; i++)
            {
                float x = -4.7f + (i % 3) * 4.7f;
                float y = i < 3 ? 8.1f : 5.4f;
                Box("MonitorFrame", w, new Vector3(x, y, -0.3f), new Vector3(4.4f, 2.6f, 0.4f), mPlasticBlack);
                Transform holder = Group("Feed_CAM" + (i + 1), w, w.TransformPoint(new Vector3(x, y, -0.52f)), Vector3.back);
                Material feed = FeedMaterial(i);
                Quad("Screen", holder, Vector3.zero, new Vector2(4.1f, 2.3f), feed);
                wall.overlays[i] = Text3D(holder, "Overlay", "CAM " + (i + 1) + "\nmotion = " + feeds[i].motion + "\ndoor = " + feeds[i].door,
                    new Vector3(-0.9f, 0.1f, -0.02f), new Vector2(2.1f, 2f), 3f, new Color(0.9f, 1f, 0.9f), fontHead, TextAlignmentOptions.TopLeft);
                wall.screens[i] = holder;
                if (i == 3)
                {
                    GameObject fig = Quad("DarkFigure", holder, new Vector3(-1.4f, -0.2f, -0.03f), new Vector2(0.35f, 1.3f), mBlack);
                    GameObject figHead = Quad("Head", fig.transform, new Vector3(0f, 0.62f, 0f), new Vector2(0.75f, 0.2f), mBlack);
                    figHead.transform.localScale = new Vector3(1f, 0.25f, 1f);
                    wall.figure = fig.transform;
                    wall.figureFrom = new Vector3(-1.4f, -0.2f, -0.03f);
                    wall.figureTo = new Vector3(2.3f, -0.2f, -0.03f);
                    fig.SetActive(false);
                }
            }
            wall.screenGlow = NewLight(w, "ScreenGlow", w.TransformPoint(new Vector3(0f, 6.6f, -2.5f)), new Color(0.6f, 0.85f, 1f), 1.2f, 10f);

            Transform desk = Group("GuardDesk", s, new Vector3(41f, 0f, 5.6f), Vector3.back);
            Box("DeskTop", desk, new Vector3(0f, 2.9f, 0f), new Vector3(11f, 0.2f, 2.6f), mWood);
            Box("DeskBody", desk, new Vector3(0f, 1.4f, 0.5f), new Vector3(10.6f, 2.8f, 1.4f), mMetalBlack);
            Box("ButtonStrip", desk, new Vector3(-0.8f, 3.05f, -0.4f), new Vector3(9f, 0.1f, 1f), mPlasticBlack, false);
            Text3D(desk, "ButtonsLabel", "ALARM SOURCE", new Vector3(-0.8f, 3.11f, 0.25f), new Vector2(4f, 0.35f), 2f, new Color(0.9f, 0.9f, 0.9f), fontHead).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                Transform b = Group("CamButton_" + (i + 1), desk, desk.TransformPoint(new Vector3(-4.55f + i * 1.5f, 3.1f, -0.45f)), Vector3.back);
                GameObject c = Cyl("Cap", b, new Vector3(0f, 0.06f, 0f), new Vector3(0.6f, 0.06f, 0.6f), mRed);
                TextMeshPro lab = Text3D(b, "Number", (i + 1).ToString(), new Vector3(0f, 0.13f, 0f), new Vector2(0.5f, 0.5f), 3f, Color.white, fontHead);
                lab.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var btn = b.gameObject.AddComponent<L3Button>();
                btn.displayName = "Button CAM " + (i + 1); btn.cameraWall = wall; btn.cameraIndex = i + 1; btn.cap = c.transform;
                Trigger(b.gameObject, new Vector3(0f, 0.2f, 0f), new Vector3(1.1f, 0.6f, 1.1f));
            }
            StickyNote(desk, "StickyNote_Alarm", desk.TransformPoint(new Vector3(4.5f, 3.01f, -0.2f)), "alarm = (motion > 3) && (door == OPEN);");

            // ---- Station 2: printer (south wall) + symbol cabinet (east wall)
            Transform pr = Group("SecurityPrinter", s, new Vector3(34f, 0f, -11.25f + T / 2f), Vector3.forward);
            Box("PrinterDesk", pr, new Vector3(0f, 1.45f, -1.2f), new Vector3(4f, 2.9f, 2.4f), mMetalBlack);
            Box("PrinterBody", pr, new Vector3(0f, 3.35f, -1f), new Vector3(2.2f, 0.9f, 1.6f), mPlastic("L3_PrinterWhite", new Color(0.82f, 0.82f, 0.8f)), false);
            Box("PrinterTray", pr, new Vector3(0f, 2.98f, -2.05f), new Vector3(1.6f, 0.05f, 0.7f), mMetalGray, false);
            Text3D(pr, "Label", "SHIFT LOG PRINTER", new Vector3(0f, 3.45f, -1.82f), new Vector2(2f, 0.3f), 1.4f, new Color(0.15f, 0.15f, 0.15f), fontHead);
            var printer = pr.gameObject.AddComponent<L3Printer>();
            printer.displayName = "Printer"; printer.printSound = aPrint;
            var pbc = pr.gameObject.AddComponent<BoxCollider>(); pbc.isTrigger = true; pbc.center = new Vector3(0f, 3.4f, -1.1f); pbc.size = new Vector3(2.4f, 1.2f, 1.9f);
            Transform sheet = Group("Printout", pr, pr.TransformPoint(new Vector3(0f, 3.03f, -2.1f)), Vector3.forward);
            Box("Sheet", sheet, Vector3.zero, new Vector3(1.3f, 0.02f, 1.7f), mPaper, false);
            TextMeshPro st = Text3D(sheet, "Text", "LAST SHIFT LOG\n17 ? 5 = 2\n 9 ? 3 = 27\n 8 ? 2 = 4", new Vector3(0f, 0.02f, 0f), new Vector2(1.2f, 1.6f), 2.2f, new Color(0.1f, 0.1f, 0.12f), fontNote, TextAlignmentOptions.Left);
            st.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var pex = sheet.gameObject.AddComponent<L2Examine>();
            pex.displayName = "Printout"; pex.title = "LAST SHIFT LOG"; pex.verb = "Read the Printout";
            pex.body = "Three lines are missing their operator:\n\n17 ? 5 = 2\n 9 ? 3 = 27\n 8 ? 2 = 4";
            Trigger(sheet.gameObject, new Vector3(0f, 0.15f, 0f), new Vector3(1.5f, 0.5f, 1.9f));
            var pshow = sheet.gameObject.AddComponent<L3ShowObjects>();
            pshow.show = new[] { sheet.gameObject }; pshow.slideFrom = pr.rotation * new Vector3(0f, 0f, 0.9f); pshow.duration = 1.4f;
            printer.printout = pshow;
            // L3ShowObjects must live on an active object: move it to the printer
            Object.DestroyImmediate(pshow);
            pshow = pr.gameObject.AddComponent<L3ShowObjects>();
            pshow.show = new[] { sheet.gameObject }; pshow.slideFrom = pr.rotation * new Vector3(0f, 0f, 0.9f); pshow.duration = 1.4f;
            printer.printout = pshow;
            sheet.gameObject.SetActive(false);

            Transform cab = Group("SymbolCabinet", s, new Vector3(60f - T / 2f, 0f, 2.5f), Vector3.left);
            Transform cdoor = Cabinet(cab, new Vector3(0f, 5.2f, 0f), new Vector3(4.4f, 5.4f, 1.7f), mMetalGray, mMetalGray, out Transform cin);
            Box("Handle", cdoor, new Vector3(4.0f, 0f, -0.12f), new Vector3(0.12f, 0.8f, 0.12f), mSteel, false);
            Text3D(cdoor, "DoorLabel", "SECURITY CABINET", new Vector3(2.2f, 1.9f, -0.06f), new Vector2(3.6f, 0.5f), 2.4f, new Color(0.15f, 0.15f, 0.15f), fontHead);
            var co = cdoor.gameObject.AddComponent<L2Openable>();
            co.kind = L2Openable.Kind.Door; co.label = "Cabinet"; co.openAngle = 105f; co.displayName = "Cabinet";
            Trigger(cdoor.gameObject, new Vector3(2.2f, 0f, -0.05f), new Vector3(4.4f, 5.4f, 0.3f));
            co.enabled = false;
            GameObject ruleCard = Box("RuleCard", cin, new Vector3(-0.9f, 0.4f, 0.7f), new Vector3(1.8f, 1.3f, 0.03f), mPaper, false);
            Text3D(cin, "RuleCardText", "if ((isGuard || hasBadge) && !alarmOn) unlockSafe();", new Vector3(-0.9f, 0.4f, 0.68f), new Vector2(1.7f, 1.2f), 1.6f, new Color(0.1f, 0.1f, 0.12f), fontNote).transform.SetParent(ruleCard.transform, true);
            var rcEx = ruleCard.AddComponent<L2Examine>();
            rcEx.displayName = "Rule Card"; rcEx.title = "SWITCH RULE CARD"; rcEx.verb = "Read the Rule Card";
            rcEx.body = "if ((isGuard || hasBadge) && !alarmOn) unlockSafe();";
            Trigger(ruleCard, Vector3.zero, new Vector3(1f, 1f, 8f));
            GameObject pin = Box("PinNote", cin, new Vector3(1.0f, 0.4f, 0.7f), new Vector3(1.5f, 1.1f, 0.03f), mSticky, false);
            Text3D(cin, "PinNoteText", "PIN = cam * 10 + (17 % 5)", new Vector3(1.0f, 0.4f, 0.68f), new Vector2(1.4f, 1f), 1.7f, new Color(0.12f, 0.1f, 0.05f), fontNote).transform.SetParent(pin.transform, true);
            var pinEx = pin.AddComponent<L2Examine>();
            pinEx.displayName = "PIN Note"; pinEx.title = "A NOTE IN THE CABINET"; pinEx.verb = "Read the Note";
            pinEx.body = "PIN = cam * 10 + (17 % 5)";
            Trigger(pin, Vector3.zero, new Vector3(1f, 1f, 8f));
            ruleCard.SetActive(false); pin.SetActive(false);

            L3Keypad sym = KeypadObject(cab, "Symbol Keypad", cab.TransformPoint(new Vector3(3.3f, 5.2f, -0.05f)), Vector3.left, false);
            sym.station = "s2"; sym.title = "CABINET LOCK";
            sym.code = "The cabinet only takes operators.\nEnter the three missing symbols from the last shift log, in order.\n\nKeys:   +   -   *   /   %";
            sym.answer = "%*/"; sym.symbolKeys = true; sym.maxLength = 3;
            sym.hint = "% gives what is LEFT OVER after dividing.";
            sym.wrongMessage = "BZZT. The keypad buzzes and clears.  -10 s";
            sym.successDisplay = "% * /"; sym.successMessage = "The cabinet clicks open.";
            Quad("SymScreen", cab, new Vector3(3.3f, 6.45f, -0.06f), new Vector2(1.6f, 0.55f), mScreen);
            sym.screen = Text3D(cab, "SymScreenText", "? ? ?", new Vector3(3.3f, 6.45f, -0.07f), new Vector2(1.5f, 0.5f), 2f, new Color(1f, 0.75f, 0.3f), fontHead);
            sym.screenLocked = "? ? ?"; sym.screenReady = "? ? ?"; sym.screenSolved = "OPEN";
            var openCab = cab.gameObject.AddComponent<L3OpenPart>(); openCab.part = co;
            var showCab = cab.gameObject.AddComponent<L3ShowObjects>(); showCab.show = new[] { ruleCard, pin };
            sym.onSolved = new L3Reaction[] { openCab, showCab };

            // ---- Station 3: switch safe (south-east corner) with switches + PIN keypad
            Transform sf = Group("Safe", s, new Vector3(53f, 0f, -11.25f + T / 2f), Vector3.forward);
            Transform sdoor = Cabinet(sf, new Vector3(0f, 2.3f, 0f), new Vector3(4f, 4.4f, 3.2f), mMetalBlack, mSteel, out Transform sin);
            GameObject wheel = Cyl("Wheel", sdoor, new Vector3(2.6f, 0.3f, -0.18f), new Vector3(0.9f, 0.06f, 0.9f), mSteel);
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Text3D(sdoor, "Label", "SAFE", new Vector3(2f, 1.6f, -0.08f), new Vector2(2f, 0.6f), 3.5f, new Color(0.85f, 0.85f, 0.85f), fontHead);
            var so2 = sdoor.gameObject.AddComponent<L2Openable>();
            so2.kind = L2Openable.Kind.Door; so2.label = "Safe Door"; so2.openAngle = 100f; so2.displayName = "Safe";
            Trigger(sdoor.gameObject, new Vector3(2f, 0f, -0.05f), new Vector3(4f, 4.4f, 0.3f));
            so2.enabled = false;
            Transform blue = Keycard(sin, "Keycard_Blue", sin.TransformPoint(new Vector3(0f, -1.95f, 0.2f)), mBlue, "SEC: s = 4", L2Items.KeycardBlue);
            blue.gameObject.SetActive(false);

            Transform panel = Group("SafePanel", sf, sf.TransformPoint(new Vector3(0f, 6.3f, 0f)), Vector3.forward);
            Box("PanelBody", panel, new Vector3(0f, 0f, -0.15f), new Vector3(5.6f, 2.6f, 0.3f), mMetalGray, false);
            Text3D(panel, "PanelTitle", "SAFE CONTROL", new Vector3(0f, 1.0f, -0.32f), new Vector2(4f, 0.4f), 2.2f, new Color(0.95f, 0.9f, 0.8f), fontHead);
            string[] keys = { "isGuard", "hasBadge", "alarmOn" };
            var switches = new L3ToggleSwitch[3];
            for (int i = 0; i < 3; i++)
            {
                Transform sw = Group("Switch_" + keys[i], panel, panel.TransformPoint(new Vector3(-2.2f + i * 1.25f, -0.2f, -0.32f)), Vector3.forward);
                Box("Plate", sw, Vector3.zero, new Vector3(0.9f, 1.1f, 0.05f), mPlasticBlack, false);
                var leverPivot = new GameObject("Lever").transform;
                leverPivot.SetParent(sw, false);
                leverPivot.localPosition = new Vector3(0f, 0f, -0.05f);
                Box("Stick", leverPivot, new Vector3(0f, 0f, -0.25f), new Vector3(0.14f, 0.14f, 0.5f), mSteel, false);
                TextMeshPro stt = Text3D(sw, "State", keys[i] + "\nOFF", new Vector3(0f, 0.85f, -0.02f), new Vector2(1.2f, 0.6f), 1.6f, Color.white, fontHead);
                var tsw = sw.gameObject.AddComponent<L3ToggleSwitch>();
                tsw.key = keys[i]; tsw.startsOn = i == 2; tsw.lever = leverPivot; tsw.stateText = stt; tsw.displayName = keys[i];
                tsw.onAngle = -35f; tsw.offAngle = 35f;
                if (i == 2)
                {
                    GameObject lamp = Sphere("AlarmLamp", sw, new Vector3(0.62f, 0.35f, -0.1f), 0.28f, eRed);
                    tsw.lamp = lamp.GetComponent<Renderer>(); tsw.lampOn = eRed; tsw.lampOff = eLampOff; tsw.blinkWhenOn = true;
                }
                Trigger(sw.gameObject, new Vector3(0f, 0.1f, -0.2f), new Vector3(1.1f, 1.5f, 0.7f));
                switches[i] = tsw;
            }
            var safeKeypadGo = KeypadVisual(panel, "Safe Keypad", panel.TransformPoint(new Vector3(1.9f, -0.1f, -0.3f)), Vector3.forward);
            L3SafeKeypad sk = safeKeypadGo.AddComponent<L3SafeKeypad>();
            Trigger(safeKeypadGo, new Vector3(0f, 0f, -0.1f), new Vector3(1.2f, 1.5f, 0.5f));
            sk.displayName = "Safe Keypad";
            sk.station = "s3"; sk.title = "SAFE";
            sk.code = "if ((isGuard || hasBadge) && !alarmOn) unlockSafe();\n\nPIN = cam * 10 + (17 % 5)\n// Enter the PIN";
            sk.answer = "42"; sk.maxLength = 4;
            sk.hint = "! flips true to false. || needs only ONE side true.";
            sk.wrongMessage = "Wrong PIN.  -10 s";
            sk.successDisplay = "42"; sk.successMessage = "The safe unlocks! Something blue is inside.";
            sk.isGuard = switches[0]; sk.hasBadge = switches[1]; sk.alarmOn = switches[2]; sk.siren = aSiren;
            var openSafe = sf.gameObject.AddComponent<L3OpenPart>(); openSafe.part = so2;
            var showBlue = sf.gameObject.AddComponent<L3ShowObjects>(); showBlue.show = new[] { blue.gameObject };
            sk.onSolved = new L3Reaction[] { openSafe, showBlue };
            NewLight(panel, "AlarmGlow", panel.TransformPoint(new Vector3(0.6f, 0.2f, -1.2f)), new Color(1f, 0.15f, 0.1f), 0.8f, 4f);

            log.AppendLine("Security: camera wall (CAM 4), printer + symbol cabinet (%*/), switch safe (rule + PIN 42) -> BLUE keycard");
        }

        static Material FeedMaterial(int i)
        {
            string path = TexDir + "/L3_CamFeed_" + (i + 1) + ".png";
            var tex = new Texture2D(128, 72, TextureFormat.RGBA32, false);
            var rnd = new System.Random(17 + i * 31);
            float baseG = 0.16f + i * 0.025f;
            for (int y = 0; y < 72; y++)
                for (int x = 0; x < 128; x++)
                {
                    float v = baseG + (float)rnd.NextDouble() * 0.06f;
                    // a simple corridor / doorway shape per feed
                    bool floorBand = y < 18;
                    bool door = x > 40 + i * 9 && x < 58 + i * 9 && y < 52 && y >= 18;
                    if (floorBand) v *= 0.7f;
                    if (door) v *= (i % 2 == 0) ? 0.45f : 1.5f;
                    if (y % 4 == 0) v *= 0.85f;   // scanlines
                    tex.SetPixel(x, y, new Color(v * 0.85f, v, v * 0.9f, 1f));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return Unlit("L3_CamFeed_" + (i + 1), t, Color.white);
        }

        // ================================================================== control (north): multi-step logic
        static L3RoomPower Control()
        {
            Transform c = Group("Control", level, Vector3.zero, Vector3.back);
            Transform lights = Group("Lights", c, Vector3.zero, Vector3.back);
            var ls = new List<Light>();
            var glow = new List<Renderer>();
            foreach (float z in new[] { 29f, 39f })
            {
                glow.Add(Box("CeilingLamp", lights, new Vector3(0f, H - 0.05f, z), new Vector3(3f, 0.1f, 1.2f), mDiffuser, false).GetComponent<Renderer>());
                ls.Add(NewLight(lights, "CeilingLight", new Vector3(0f, H - 0.8f, z), new Color(0.85f, 0.92f, 1f), 8f, 28f));
            }
            NewLight(lights, "EmergencyLight", new Vector3(12f, 9.5f, 32f), new Color(1f, 0.12f, 0.08f), 1.2f, 14f);
            var power = c.gameObject.AddComponent<L3RoomPower>();
            power.poweredFlag = "power_control";
            power.lights = ls.ToArray();
            power.glow = glow.ToArray();
            power.dimFactor = 0.2f;
            power.powerOnSound = aRumble;

            // ---- desk with 3 monitors, console keypad, keycard printer slot and clipboard (north wall)
            Transform d = Group("ControlDesk", c, new Vector3(0f, 0f, 45f - T / 2f), Vector3.back);
            Box("DeskTop", d, new Vector3(0f, 2.9f, -2f), new Vector3(14f, 0.2f, 3f), mMetalGray);
            Box("DeskBody", d, new Vector3(0f, 1.4f, -1.2f), new Vector3(13.6f, 2.8f, 1.4f), mMetalBlack);
            string mon1 = "if (temp > 80)      fan = 3;\nelse if (temp > 60) fan = 2;\nelse                fan = 1;\n// Set the fan dial.";
            string mon2 = "online = 0;\nforeach (rack in ServerRoom)\n    if (rack.light == GREEN) online++;\n// Enter online";
            TextMeshPro t1 = Monitor(d, "Monitor1", new Vector3(-4.6f, 5.2f, -1.1f), "MONITOR 1\n\n" + mon1);
            TextMeshPro t2 = Monitor(d, "Monitor2", new Vector3(0f, 5.2f, -1.1f), "MONITOR 2\n\n" + mon2 + "\n\nonline = ?");
            Monitor(d, "Monitor3", new Vector3(4.6f, 5.2f, -1.1f), "SYSTEM STATUS\n\nMAIN SYSTEM: CRASHED\nCOOLING: MANUAL\nREBOOT: EMERGENCY PANEL");
            Examine(d.Find("Monitor1").gameObject, "Monitor 1", "MONITOR 1", mon1, "Read Monitor 1");
            Examine(d.Find("Monitor2").gameObject, "Monitor 2", "MONITOR 2", mon2, "Read Monitor 2");

            L3Keypad console = KeypadObject(d, "Console Keypad", d.TransformPoint(new Vector3(1.6f, 3.0f, -2.6f)), Vector3.back, true);
            console.station = "c2"; console.title = "CONSOLE: SERVERS ONLINE";
            console.code = mon2;
            console.answer = "5"; console.maxLength = 3;
            console.hint = "Only count the racks that pass the IF.";
            console.successDisplay = "online = 5"; console.successMessage = "Monitor 2: online = 5";
            var setMon2 = console.gameObject.AddComponent<L3SetText>();
            setMon2.target = t2; setMon2.text = "MONITOR 2\n\n" + mon2 + "\n\n<color=#6EF08A>online = 5</color>"; setMon2.color = new Color(0.55f, 0.9f, 1f);
            console.onSolved = new L3Reaction[] { setMon2 };

            Transform slot = Group("KeycardPrinterSlot", d, d.TransformPoint(new Vector3(-2f, 3.0f, -2.4f)), Vector3.back);
            Box("SlotBody", slot, new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.5f, 1.2f), mPlasticBlack, false);
            Box("Slit", slot, new Vector3(0f, 0.3f, -0.61f), new Vector3(1.1f, 0.06f, 0.02f), mBlack, false);
            Text3D(slot, "Label", "KEYCARD PRINTER", new Vector3(0f, 0.12f, -0.61f), new Vector2(1.5f, 0.2f), 1.2f, new Color(0.8f, 0.8f, 0.8f), fontHead);
            Transform greenCard = Keycard(slot, "Keycard_Green", slot.TransformPoint(new Vector3(0f, 0.08f, -0.95f)), mGreen, "CTRL: c = 5", L2Items.KeycardGreen);
            var printGreen = slot.gameObject.AddComponent<L3ShowObjects>();
            printGreen.show = new[] { greenCard.gameObject }; printGreen.slideFrom = slot.rotation * new Vector3(0f, 0f, 0.6f);
            printGreen.delay = 2f; printGreen.duration = 1.5f; printGreen.sound = aPrint; printGreen.message = "The console prints the GREEN keycard.";
            greenCard.gameObject.SetActive(false);

            Transform clip = Group("Clipboard", d, d.TransformPoint(new Vector3(5.6f, 3.02f, -2.3f)), Vector3.back);
            Box("Board", clip, Vector3.zero, new Vector3(1.6f, 0.05f, 2.1f), mWood, false);
            Box("Sheet", clip, new Vector3(0f, 0.03f, -0.05f), new Vector3(1.45f, 0.01f, 1.8f), mPaper, false);
            TextMeshPro ct = Text3D(clip, "Text", "REBOOT PROTOCOL\n1. If temp > 80, pull COOLING first.\n2. If online < 6, pull BACKUP next.\n3. REBOOT is always last.", new Vector3(0f, 0.04f, -0.05f), new Vector2(1.35f, 1.7f), 1.3f, new Color(0.1f, 0.1f, 0.12f), fontNote, TextAlignmentOptions.TopLeft);
            ct.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Examine(clip.gameObject, "Clipboard", "REBOOT PROTOCOL",
                "1. If temp > 80, pull COOLING first.\n2. If online < 6, pull BACKUP next (not enough servers to run alone).\n3. REBOOT is always last.", "Read the Clipboard");
            Trigger(clip.gameObject, new Vector3(0f, 0.2f, 0f), new Vector3(1.8f, 0.6f, 2.3f));

            // ---- cooling unit with gauge and fan dial (west wall)
            Transform cu = Group("CoolingUnit", c, new Vector3(-16.9f + T / 2f, 0f, 33f), Vector3.right);
            Box("UnitBody", cu, new Vector3(0f, 4.3f, -1.4f), new Vector3(4.2f, 8.6f, 2.8f), mMetalGray);
            Box("Grille", cu, new Vector3(0f, 2.2f, -2.82f), new Vector3(3.4f, 2.6f, 0.04f), mMesh, false);
            Text3D(cu, "Title", "COOLING UNIT", new Vector3(0f, 7.9f, -2.83f), new Vector2(3.8f, 0.5f), 3f, new Color(0.9f, 0.95f, 1f), fontHead);
            Transform gauge = Group("Gauge", cu, cu.TransformPoint(new Vector3(-0.9f, 6.2f, -2.82f)), Vector3.right);
            GameObject face = Cyl("Face", gauge, new Vector3(0f, 0f, -0.02f), new Vector3(1.5f, 0.03f, 1.5f), mPaper);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Text3D(gauge, "Reading", Degrees(86), new Vector3(0f, -0.05f, -0.07f), new Vector2(1.3f, 0.6f), 3.4f, new Color(0.8f, 0.1f, 0.1f), fontHead);
            Text3D(gauge, "Label", "TEMP", new Vector3(0f, 0.45f, -0.07f), new Vector2(1f, 0.3f), 1.6f, new Color(0.2f, 0.2f, 0.2f), fontHead);
            Examine(gauge.gameObject, "Gauge", "COOLING UNIT GAUGE", "The gauge reads " + Degrees(86) + ".", "Read the Gauge");
            Trigger(gauge.gameObject, Vector3.zero, new Vector3(1.6f, 1.6f, 0.4f));
            Transform dial = Group("FanDial", cu, cu.TransformPoint(new Vector3(1.0f, 6.2f, -2.82f)), Vector3.right);
            GameObject dface = Cyl("DialFace", dial, new Vector3(0f, 0f, -0.02f), new Vector3(1.3f, 0.03f, 1.3f), mPlasticBlack);
            dface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var knob = new GameObject("Knob").transform;
            knob.SetParent(dial, false);
            knob.localPosition = new Vector3(0f, 0f, -0.1f);
            Box("Pointer", knob, new Vector3(0f, 0.3f, 0f), new Vector3(0.14f, 0.6f, 0.1f), mRed, false);
            Text3D(dial, "Marks", "1     2     3", new Vector3(0f, 0.85f, -0.03f), new Vector2(1.4f, 0.3f), 1.8f, Color.white, fontHead);
            TextMeshPro dl = Text3D(dial, "Value", "FAN\n1", new Vector3(0f, -0.95f, -0.03f), new Vector2(1.2f, 0.6f), 1.8f, Color.white, fontHead);
            var dialC = dial.gameObject.AddComponent<L3Dial>();
            dialC.displayName = "Fan Dial"; dialC.knob = knob; dialC.label = dl; dialC.degreesPerStep = 60f;
            Trigger(dial.gameObject, Vector3.zero, new Vector3(1.5f, 2.2f, 0.5f));

            // ---- red emergency panel with 3 levers (east wall)
            Transform ep = Group("EmergencyPanel", c, new Vector3(16.9f - T / 2f, 0f, 33f), Vector3.left);
            Box("PanelBox", ep, new Vector3(0f, 5.4f, -0.35f), new Vector3(5.6f, 4.4f, 0.7f), mRed, false);
            TextMeshPro ptxt = Text3D(ep, "PanelText", "EMERGENCY REBOOT", new Vector3(0f, 7.15f, -0.72f), new Vector2(5f, 0.6f), 3f, new Color(1f, 0.35f, 0.3f), fontHead);
            var seq = ep.gameObject.AddComponent<L3LeverSequence>();
            seq.panelText = ptxt; seq.fanDial = dialC; seq.alarmClip = aSiren;
            seq.alarmLight = NewLight(ep, "AlarmLight", ep.TransformPoint(new Vector3(0f, 9.5f, -1.5f)), new Color(1f, 0.1f, 0.05f), 4f, 18f);
            string[] ln = { "COOLING", "BACKUP", "REBOOT" };
            var levers = new L3Lever[3];
            for (int i = 0; i < 3; i++)
            {
                Transform lv = Group("Lever_" + ln[i], ep, ep.TransformPoint(new Vector3(-1.7f + i * 1.7f, 5.0f, -0.72f)), Vector3.left);
                Box("Slot", lv, Vector3.zero, new Vector3(0.35f, 2.2f, 0.05f), mBlack, false);
                var handle = new GameObject("Handle").transform;
                handle.SetParent(lv, false);
                handle.localPosition = new Vector3(0f, 0f, -0.05f);
                Box("Arm", handle, new Vector3(0f, 0f, -0.55f), new Vector3(0.16f, 0.16f, 1.1f), mSteel, false);
                Box("Grip", handle, new Vector3(0f, 0f, -1.1f), new Vector3(0.5f, 0.22f, 0.22f), mPlasticBlack, false);
                Text3D(lv, "Label", ln[i], new Vector3(0f, 1.5f, -0.02f), new Vector2(1.6f, 0.4f), 2.2f, Color.white, fontHead);
                var lev = lv.gameObject.AddComponent<L3Lever>();
                lev.leverName = ln[i]; lev.sequence = seq; lev.handle = handle; lev.displayName = ln[i] + " lever";
                lev.upAngle = -40f; lev.downAngle = 40f;
                Trigger(lv.gameObject, new Vector3(0f, 0f, -0.6f), new Vector3(1.3f, 2.6f, 1.4f));
                levers[i] = lev;
            }
            seq.levers = levers;
            // success: lights back on, servers hum, console prints the GREEN keycard
            var humPower = level.Find("ServerRoom/ServerHum").GetComponent<L3RoomPower>();
            seq.onSolved = new L3Reaction[] { power, humPower, printGreen };

            log.AppendLine("Control: cooling gauge " + Degrees(86) + " + fan dial, monitors, console keypad (5), clipboard, levers COOLING>BACKUP>REBOOT -> GREEN keycard");
            return power;
        }

        static string Degrees(int v) => fontHead != null && fontHead.HasCharacter('°') ? v + "°C" : v + " C";

        static TextMeshPro Monitor(Transform d, string name, Vector3 pos, string text)
        {
            Transform g = Group(name, d, d.TransformPoint(pos), Vector3.back);
            Box("Frame", g, Vector3.zero, new Vector3(4.2f, 2.8f, 0.3f), mPlasticBlack, false);
            Box("Stand", g, new Vector3(0f, -1.7f, 0.1f), new Vector3(0.4f, 0.8f, 0.3f), mPlasticBlack, false);
            Quad("Screen", g, new Vector3(0f, 0f, -0.16f), new Vector2(3.95f, 2.55f), mScreen);
            Trigger(g.gameObject, Vector3.zero, new Vector3(4.2f, 2.8f, 0.6f));
            return Text3D(g, "Text", text, new Vector3(0f, 0f, -0.17f), new Vector2(3.75f, 2.4f), 1.6f, new Color(0.55f, 0.9f, 1f), fontNote, TextAlignmentOptions.TopLeft);
        }

        static void Examine(GameObject go, string display, string title, string body, string verb)
        {
            var ex = go.AddComponent<L2Examine>();
            ex.displayName = display; ex.title = title; ex.body = body; ex.verb = verb;
        }

        // ================================================================== exit
        static void ExitDoor(L3Keypad terminal)
        {
            Transform e = Group("Exit", level, Vector3.zero, Vector3.back);
            var doorRoot = new GameObject("ExitDoor").transform;
            doorRoot.SetParent(e, false);
            GameObject leaf = Box("DoorLeaf", doorRoot, new Vector3(0f, DoorH / 2f, -22.5f), new Vector3(6.2f, DoorH, 0.4f), mSteel);
            leaf.name = "DoorLeaf";
            Box("Stripe", leaf.transform, new Vector3(0f, -0.25f, -0.55f), new Vector3(0.95f, 0.06f, 0.2f), mYellow, false);
            var slide = doorRoot.gameObject.AddComponent<L3SlideDoor>();
            slide.leaf = leaf.transform;
            slide.openOffset = new Vector3(6.4f, 0f, 0f);
            slide.openSound = aDoorHeavy;
            slide.statusLight = NewLight(e, "ExitStatusLight", new Vector3(0f, 9.6f, -21.4f), new Color(1f, 0.15f, 0.1f), 2.5f, 8f);
            var ex = leaf.AddComponent<L2Examine>();
            ex.displayName = "Exit Door"; ex.title = "EXIT"; ex.verb = "Examine the Exit Door";
            ex.body = "A heavy sliding security door. It only opens for the central terminal's exit code.";
            terminal.onSolved = new L3Reaction[] { slide };

            var trig = new GameObject("ExitTrigger");
            trig.transform.SetParent(e, false);
            trig.transform.position = new Vector3(0f, 5f, -29.5f);
            trig.AddComponent<L3ExitTrigger>().size = new Vector3(8f, 12f, 8f);
            NewLight(e, "CorridorLight", new Vector3(0f, 10f, -29f), new Color(0.9f, 0.95f, 1f), 2f, 14f);
        }

        // ================================================================== reusable pieces
        // A hollow cabinet whose open side faces local -z. Returns the door pivot (hinge on the left);
        // 'inside' is a holder at the centre of the cabinet for things placed inside.
        static Transform Cabinet(Transform g, Vector3 c, Vector3 s, Material body, Material doorMat, out Transform inside)
        {
            float t = 0.12f;
            Box("Back", g, c + new Vector3(0f, 0f, s.z / 2f - t / 2f), new Vector3(s.x, s.y, t), body, false);
            Box("SideL", g, c + new Vector3(-s.x / 2f + t / 2f, 0f, 0f), new Vector3(t, s.y, s.z), body, false);
            Box("SideR", g, c + new Vector3(s.x / 2f - t / 2f, 0f, 0f), new Vector3(t, s.y, s.z), body, false);
            Box("Top", g, c + new Vector3(0f, s.y / 2f - t / 2f, 0f), new Vector3(s.x, t, s.z), body, false);
            Box("Bottom", g, c + new Vector3(0f, -s.y / 2f + t / 2f, 0f), new Vector3(s.x, t, s.z), body, false);
            var solid = new GameObject("CabinetCollider");
            solid.transform.SetParent(g, false);
            var bc = solid.AddComponent<BoxCollider>();
            bc.center = c + new Vector3(0f, 0f, 0.1f);
            bc.size = new Vector3(s.x, s.y, s.z - 0.2f);

            inside = new GameObject("Inside").transform;
            inside.SetParent(g, false);
            inside.localPosition = c;

            var pivot = new GameObject("DoorPivot").transform;
            pivot.SetParent(g, false);
            pivot.localPosition = c + new Vector3(-s.x / 2f, 0f, -s.z / 2f - 0.04f);
            Box("DoorPanel", pivot, new Vector3(s.x / 2f, 0f, 0f), new Vector3(s.x, s.y, 0.08f), doorMat, false);
            return pivot;
        }

        // A keypad box with a small screen and button grid; the L3Keypad goes on the returned object.
        static GameObject KeypadVisual(Transform parent, string name, Vector3 worldPos, Vector3 toward)
        {
            Transform k = Group(name.Replace(" ", ""), parent, worldPos, toward);
            Box("KeypadBody", k, new Vector3(0f, 0f, 0f), new Vector3(0.95f, 1.3f, 0.12f), mPlasticBlack, false);
            Box("KeypadScreen", k, new Vector3(0f, 0.42f, -0.065f), new Vector3(0.75f, 0.25f, 0.01f), eGreen, false);
            for (int r = 0; r < 4; r++)
                for (int col = 0; col < 3; col++)
                    Box("Key", k, new Vector3(-0.25f + col * 0.25f, 0.1f - r * 0.2f, -0.07f), new Vector3(0.18f, 0.14f, 0.03f), mRubber, false);
            return k.gameObject;
        }

        static L3Keypad KeypadObject(Transform parent, string name, Vector3 worldPos, Vector3 toward, bool onDesk)
        {
            GameObject go = KeypadVisual(parent, name, worldPos, toward);
            if (onDesk)
            {
                go.transform.position += Vector3.up * 0.35f;
                go.transform.rotation = go.transform.rotation * Quaternion.Euler(60f, 0f, 0f);
            }
            var kp = go.AddComponent<L3Keypad>();
            kp.displayName = name;
            Trigger(go, new Vector3(0f, 0f, -0.1f), new Vector3(1.2f, 1.5f, 0.5f));
            return kp;
        }

        static Transform Keycard(Transform parent, string name, Vector3 worldPos, Material m, string label, string itemId)
        {
            Transform k = Group(name, parent, worldPos, Vector3.back);
            Box("Card", k, Vector3.zero, new Vector3(0.85f, 0.03f, 0.55f), m, false);
            Box("Stripe", k, new Vector3(0f, 0.02f, 0.17f), new Vector3(0.85f, 0.01f, 0.1f), mBlack, false);
            TextMeshPro t = Text3D(k, "Label", label, new Vector3(0f, 0.025f, -0.05f), new Vector2(0.8f, 0.3f), 1.2f, Color.white, fontHead);
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var p = k.gameObject.AddComponent<L2Pickup>();
            p.itemId = itemId; p.displayName = L2Items.NameOf(itemId);
            Trigger(k.gameObject, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.5f, 0.8f));
            iconProtos[itemId] = k;
            return k;
        }

        static void StickyNote(Transform parent, string name, Vector3 worldPos, string text)
        {
            Transform n = Group(name, parent, worldPos, Vector3.back);
            Box("Paper", n, Vector3.zero, new Vector3(1.3f, 0.02f, 1.1f), mSticky, false);
            TextMeshPro t = Text3D(n, "Text", text, new Vector3(0f, 0.015f, 0f), new Vector2(1.2f, 1f), 1.4f, new Color(0.12f, 0.1f, 0.05f), fontNote);
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ex = n.gameObject.AddComponent<L2Examine>();
            ex.displayName = "Sticky Note"; ex.title = "STICKY NOTE"; ex.body = text; ex.verb = "Read the Sticky Note";
            Trigger(n.gameObject, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.5f, 1.2f));
        }

        // ================================================================== primitives
        static Quaternion Facing(Vector3 towardViewer) => Quaternion.LookRotation(-towardViewer, Vector3.up);

        // An empty group at a world position whose front (local -z) faces 'towardViewer'.
        static Transform Group(string name, Transform parent, Vector3 worldPos, Vector3 towardViewer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Facing(towardViewer));
            return go.transform;
        }

        static GameObject Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material m, bool solid = true)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localCenter;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = size;
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject Cyl(string name, Transform parent, Vector3 localCenter, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localCenter;
            g.transform.localScale = scale;
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject Sphere(string name, Transform parent, Vector3 localCenter, float d, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localCenter;
            g.transform.localScale = Vector3.one * d;
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject Quad(string name, Transform parent, Vector3 localCenter, Vector2 size, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localCenter;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static void Trigger(GameObject go, Vector3 center, Vector3 size)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = center;
            bc.size = size;
        }

        static TextMeshPro Text3D(Transform parent, string name, string text, Vector3 localPos, Vector2 box, float maxSize, Color c, TMP_FontAsset font,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            if (font != null) t.font = font;
            t.text = text;
            t.rectTransform.sizeDelta = box;
            t.enableAutoSizing = true;
            t.fontSizeMin = 0.2f;
            t.fontSizeMax = maxSize;
            t.color = c;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.richText = true;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        static Light NewLight(Transform parent, string name, Vector3 worldPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            // URP point lights fall off with distance squared; at this scale (1 m = 3.75 units) room
            // lights need a much higher intensity than small close-range glows.
            l.intensity = intensity * (range >= 12f ? 7f : 2f);
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ================================================================== player
        static Transform CopyPlayer(Scene target)
        {
            Scene s2 = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Additive);
            GameObject src = null;
            foreach (GameObject r in s2.GetRootGameObjects()) if (r.name == "Player") { src = r; break; }
            GameObject copy = null;
            if (src != null)
            {
                SceneManager.SetActiveScene(target);
                copy = Object.Instantiate(src);
                copy.name = "Player";
                SceneManager.MoveGameObjectToScene(copy, target);
            }
            EditorSceneManager.CloseScene(s2, true);
            SceneManager.SetActiveScene(target);
            return copy != null ? copy.transform : null;
        }

        // ================================================================== materials & sounds
        static void Materials()
        {
            mFloor = Lit("L3_Floor_ServerTile", new Color(0.16f, 0.17f, 0.19f), 0.35f);
            mWall = Lit("L3_Wall_Panel", new Color(0.21f, 0.22f, 0.24f), 0.2f);
            mCeil = Lit("L3_Ceiling", new Color(0.1f, 0.1f, 0.11f), 0.1f);
            mTrim = Lit("L3_Trim_Dark", new Color(0.08f, 0.08f, 0.09f), 0.3f, 0.5f);
            mMetalGray = L2Mat("L2_Metal_GrayPainted") ?? Lit("L3_MetalGray", new Color(0.42f, 0.44f, 0.46f), 0.4f, 0.6f);
            mMetalBlack = L2Mat("L2_Metal_Black") ?? Lit("L3_MetalBlack", new Color(0.06f, 0.06f, 0.07f), 0.4f, 0.7f);
            mSteel = L2Mat("L2_Metal_StainlessSteel") ?? Lit("L3_Steel", new Color(0.7f, 0.72f, 0.74f), 0.7f, 1f);
            mMesh = L2Mat("L2P_Metal_Mesh") ?? mMetalBlack;
            mPlasticBlack = L2Mat("L2_Plastic_Black") ?? Lit("L3_PlasticBlack", new Color(0.04f, 0.04f, 0.045f), 0.5f);
            mRubber = L2Mat("L2_Rubber_Dark") ?? mPlasticBlack;
            mRed = L2Mat("L2P_Plastic_Red") ?? Lit("L3_Red", new Color(0.7f, 0.08f, 0.06f), 0.4f);
            mBlue = L2Mat("L2P_Plastic_Blue") ?? Lit("L3_Blue", new Color(0.1f, 0.25f, 0.75f), 0.4f);
            mGreen = L2Mat("L2P_Plastic_Green") ?? Lit("L3_Green", new Color(0.1f, 0.6f, 0.2f), 0.4f);
            mYellow = L2Mat("L2P_Plastic_Yellow") ?? Lit("L3_Yellow", new Color(0.95f, 0.8f, 0.15f), 0.4f);
            mPaper = L2Mat("L2_Paper_White") ?? Lit("L3_Paper", new Color(0.92f, 0.91f, 0.87f), 0.1f);
            mSticky = L2Mat("L2_Paper_StickyNote") ?? Lit("L3_Sticky", new Color(1f, 0.9f, 0.35f), 0.1f);
            mWood = L2Mat("L2P_Wood_Light") ?? Lit("L3_Wood", new Color(0.5f, 0.38f, 0.25f), 0.3f);
            mDiffuser = L2Mat("L2_Light_Diffuser") ?? Lit("L3_Diffuser", Color.white, 0.2f, 0f, new Color(1.2f, 1.2f, 1.15f));
            mScreen = Unlit("L3_Screen_Dark", null, new Color(0.02f, 0.05f, 0.07f));
            mScreenOff = Unlit("L3_Screen_Off", null, new Color(0.01f, 0.01f, 0.012f));
            mBlack = Unlit("L3_Black", null, new Color(0.005f, 0.005f, 0.005f));
            eGreen = Lit("L3_Emissive_Green", new Color(0.1f, 0.6f, 0.2f), 0.5f, 0f, new Color(0.3f, 2.6f, 0.6f));
            eRed = Lit("L3_Emissive_Red", new Color(0.6f, 0.05f, 0.03f), 0.5f, 0f, new Color(2.8f, 0.25f, 0.15f));
            eRedDim = Lit("L3_Emissive_RedDim", new Color(0.4f, 0.04f, 0.03f), 0.5f, 0f, new Color(1.2f, 0.1f, 0.06f));
            eBlue = Lit("L3_Emissive_Blue", new Color(0.1f, 0.2f, 0.6f), 0.5f, 0f, new Color(0.3f, 0.8f, 3f));
            eWhite = Lit("L3_Emissive_White", Color.white, 0.5f, 0f, new Color(2f, 2f, 1.8f));
            eAmber = Lit("L3_Emissive_Amber", new Color(0.6f, 0.4f, 0.1f), 0.5f, 0f, new Color(2.4f, 1.4f, 0.2f));
            eLampOff = Lit("L3_Lamp_Off", new Color(0.18f, 0.03f, 0.03f), 0.6f);
        }

        static Material mPlastic(string name, Color c) => Lit(name, c, 0.45f);

        static Material L2Mat(string name)
        {
            foreach (string g in AssetDatabase.FindAssets(name + " t:Material", new[] { "Assets/CodeBreaker/Level2/Materials" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            return null;
        }

        static Material Lit(string name, Color c, float smooth, float metal = 0f, Color? emission = null)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Texture tex, Color c)
        {
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", c);
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CodeBreaker/UI/Sprites/" + name + ".png");

        static void Sounds()
        {
            aWrong = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Wrong_Answer_Horror_Hit.wav");
            aCorrect = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Correct_Answer_Chime.wav");
            aHorror = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Lectern_Open_Sting.wav");
            aAmbience = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Horror_Ambience_Loop.wav");
            aDoorHeavy = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Door_Open_Creak_Heavy.wav");

            const int R = 22050;
            aHum = Wav("L3_ServerHum.wav", 2f, R, t =>
                0.22f * Mathf.Sin(2f * Mathf.PI * 60f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 120f * t) + 0.05f * Mathf.Sin(2f * Mathf.PI * 240f * t));
            float sirenPhase = 0f;
            aSiren = Wav("L3_Siren.wav", 1.6f, R, t =>
            {
                float f = 650f + 350f * Mathf.Sin(2f * Mathf.PI * 1.25f * t);
                sirenPhase += 2f * Mathf.PI * f / R;
                float env = Mathf.Clamp01(t * 20f) * Mathf.Clamp01((1.6f - t) * 6f);
                return 0.4f * env * Mathf.Sign(Mathf.Sin(sirenPhase)) * 0.6f;
            });
            var rnd = new System.Random(7);
            float lp = 0f;
            aRumble = Wav("L3_GeneratorRumble.wav", 2.2f, R, t =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.02f;
                float env = Mathf.Clamp01(t * 2f) * Mathf.Clamp01((2.2f - t) * 1.5f);
                return env * (lp * 3f + 0.25f * Mathf.Sin(2f * Mathf.PI * 45f * t));
            });
            aPrint = Wav("L3_Printer.wav", 1.4f, R, t =>
            {
                float gate = Mathf.Repeat(t * 9f, 1f) < 0.6f ? 1f : 0.25f;
                float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((1.4f - t) * 10f);
                return 0.22f * env * gate * (Mathf.Sin(2f * Mathf.PI * 190f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 380f * t));
            });
        }

        static AudioClip Wav(string file, float seconds, int rate, System.Func<float, float> f)
        {
            string path = AudioDir + "/" + file;
            int n = Mathf.RoundToInt(seconds * rate);
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    float v = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
                    w.Write((short)(v * 32000f));
                }
                File.WriteAllBytes(path, ms.ToArray());
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ================================================================== inventory icons
        static void RenderIcons()
        {
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("L3 Icon Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.62f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.12f, 0.11f, 1f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.targetTexture = rt;
            var lightGo = new GameObject("L3 Icon Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            Vector3 stage = new Vector3(0f, -2000f, 0f);
            int made = 0;
            foreach (var kv in iconProtos)
            {
                GameObject v = L2HeldItem.BuildVisual(kv.Value, 1f, "Icon " + kv.Key);
                v.transform.position = stage;
                Bounds b = new Bounds(stage, Vector3.zero);
                bool any = false;
                foreach (Renderer r in v.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                Vector3 s = b.size;
                float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                Vector3 dir = s.y < max * 0.25f ? new Vector3(0.12f, 1f, -0.3f).normalized : new Vector3(0.3f, 0.35f, -1f).normalized;
                cam.transform.position = b.center + dir * 5f;
                cam.transform.rotation = Quaternion.LookRotation(-dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up);
                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                string path = IconDir + "/" + kv.Key + ".png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
                Object.DestroyImmediate(v);
                made++;
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            rt.Release();
            Object.DestroyImmediate(rt);
            log.AppendLine("Icons rendered: " + made + " -> " + IconDir);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
