using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using CodeBreaker.Level2;

namespace CodeBreaker.Level2.EditorTools
{
    // Code Breaker > Level 2 > Setup Gameplay
    //
    // Wires the Level 2 gameplay into the existing scene without changing the environment:
    // adds the gameplay components to the existing doors and puzzle props, creates light switches,
    // checkpoints, the TV screen, star projection, moon-box interior, drawer tray and Bedroom Key,
    // generates item icons and a few textures, then saves the scene and writes a report.
    //
    // Safe to run again (idempotent). Run it again after "Build Environment Pass 2", because the
    // environment builder recreates the doors and puzzle props.
    public static class Level2GameplaySetup
    {
        const string Scene = "Assets/Scenes/Level 2.unity";
        const string Root = "Assets/CodeBreaker/Level2";
        const string GenDir = Root + "/Gameplay";
        const string IconDir = Root + "/Resources/L2Icons";
        const string Gen = "L2G_";   // prefix of everything this tool creates inside existing objects

        static StringBuilder log;
        static Transform level, gameplay;

        [MenuItem("Code Breaker/Level 2/Setup Gameplay")]
        public static void Run()
        {
            log = new StringBuilder();
            log.AppendLine("Level 2 gameplay setup  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != Scene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(Scene);
            }

            level = FindRoot("Level2");
            Transform player = FindRoot("Player");
            if (level == null || player == null) { Debug.LogError("[Level 2 Setup] Level2 or Player root not found."); return; }
            Camera cam = player.GetComponentInChildren<Camera>(true);

            EnsureFolder(GenDir);
            EnsureFolder(GenDir + "/Materials");
            EnsureFolder(GenDir + "/Textures");
            EnsureFolder(IconDir);

            // fresh gameplay root (everything created here is rebuilt each run)
            Transform old = level.Find("Gameplay");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            gameplay = new GameObject("Gameplay").transform;
            gameplay.SetParent(level, false);
            Physics.SyncTransforms();

            // ---------------- textures & materials
            Texture2D starTex = MakeStarTexture(GenDir + "/Textures/L2G_T_StarProjection.png");
            Texture2D moonTex = MakeUVMoonTexture(Root + "/Textures/Props/L2P_T_Mirror_MOON.png", GenDir + "/Textures/L2G_T_UV_MOON.png");
            Texture2D[] pieces = new Texture2D[4];
            for (int i = 0; i < 4; i++) pieces[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Props/L2P_T_PhotoPiece_" + (i + 1) + ".png");
            Texture2D familyPhoto = MakeFamilyPhoto(pieces, GenDir + "/Textures/L2G_T_FamilyPhoto.png");

            Material tvMat = UnlitMaterial(GenDir + "/Materials/L2G_TVScreen.mat", null, new Color(0.02f, 0.02f, 0.025f), false);
            Material starMat = UnlitMaterial(GenDir + "/Materials/L2G_StarProjection.mat", starTex, new Color(1f, 0.92f, 0.7f, 0.9f), true);
            Material uvMat = UnlitMaterial(GenDir + "/Materials/L2G_UVText.mat", moonTex, new Color(0.8f, 0.62f, 1f, 1f), true);
            Material plateMat = LoadMat("L2P_Plastic_White_Matte") ?? LoadMat("L2_Plastic_White");
            Material darkMat = LoadMat("L2_Plastic_Black");
            Material paperMat = LoadMat("L2P_Plastic_White_Matte") ?? plateMat;

            // ---------------- managers
            var gmGo = new GameObject("Level 2 Game");
            gmGo.transform.SetParent(gameplay, false);
            L2Game game = gmGo.AddComponent<L2Game>();
            L2Audio audio = gmGo.AddComponent<L2Audio>();
            L2HeldItem held = gmGo.AddComponent<L2HeldItem>();
            L2UI ui = gmGo.AddComponent<L2UI>();
            L2LevelSession session = gmGo.AddComponent<L2LevelSession>();
            session.timeLimit = 1200f;   // 20:00 (Inspector-adjustable)
            game.player = player;
            game.playerCamera = cam;
            game.ui = ui;
            game.sfx = audio;
            game.heldView = held;
            held.cam = cam;

            audio.doorOpen = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Wood Door Pack/Audio/Door_Open.wav");
            audio.doorClose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Free Wood Door Pack/Audio/Door_Close.wav");
            audio.success = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CodeBreaker/Audio/Correct_Answer_Chime.wav");

            ui.headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CodeBreaker/UI/Fonts/Oswald-Medium SDF.asset");
            ui.bodyFont = ui.headingFont;
            ui.noteFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CodeBreaker/UI/Fonts/SpecialElite-Regular SDF.asset");
            ui.panelSprite = LoadSprite("CB_Panel");
            ui.buttonSprite = LoadSprite("CB_Button");
            ui.accentSprite = LoadSprite("CB_Accent");
            ui.dividerSprite = LoadSprite("CB_Divider");
            ui.vignetteSprite = LoadSprite("CB_Vignette");
            ui.trackSprite = LoadSprite("CB_ProgressTrack");
            ui.fillSprite = LoadSprite("CB_ProgressFill");
            ui.handleSprite = LoadSprite("CB_SliderHandle");
            ui.photoPieces = pieces;
            ui.familyPhoto = familyPhoto;

            if (cam != null && cam.GetComponent<L2Interactor>() == null) cam.gameObject.AddComponent<L2Interactor>();
            log.AppendLine("Managers: Level2/Gameplay/Level 2 Game (L2Game, L2Audio, L2HeldItem, L2UI); L2Interactor on " + (cam != null ? cam.name : "NO CAMERA"));

            // ---------------- doors
            SetupDoor("Door_LivingRoom_Exit", L2Items.LivingKey, "exit_living");
            SetupDoor("Door_Bedroom_Entrance", null, null);
            SetupDoor("Door_Bedroom_Exit", L2Items.BedroomKey, "exit_bedroom");
            SetupDoor("Door_Kitchen_Entrance", null, null);
            SetupDoor("Door_Kitchen_Exit", L2Items.KitchenKey, "exit_kitchen");
            SetupDoor("Door_Study_Entrance", null, null);

            // ---------------- light switches
            var switches = new Dictionary<string, L2LightSwitch>();
            Transform swRoot = Group(gameplay, "LightSwitches");
            switches["living"] = MakeSwitch(swRoot, "living", "Living Room", new Vector3(-23.74f, 4.5f, LatchZ("Door_LivingRoom_Exit", 3f)), Vector3.left,
                plateMat, "Light_Living_Ceiling", "Light_Living_FloorLamp", "Light_Living_TableLamp");
            switches["hall_b"] = MakeSwitch(swRoot, "hall_b", "Hall B", new Vector3(-21.2f, 4.5f, -9.19f), Vector3.back,
                plateMat, "Light_Hallway_B", "Light_Hallway_B_DoorFill");
            switches["bedroom"] = MakeSwitch(swRoot, "bedroom", "Bedroom", new Vector3(-9.75f, 4.5f, LatchZ("Door_Bedroom_Entrance", 3f)), Vector3.right,
                plateMat, "Light_Bedroom_Ceiling", "Light_Bedroom_BedsideLamp_Left", "Light_Bedroom_BedsideLamp_Right");
            switches["corridor"] = MakeSwitch(swRoot, "corridor", "Corridor", new Vector3(3f, 4.5f, -4.2f), Vector3.left,
                plateMat, "Light_Hallway_Corridor");
            switches["kitchen"] = MakeSwitch(swRoot, "kitchen", "Kitchen", new Vector3(LatchX("Door_Kitchen_Entrance", 3f), 4.5f, 7.01f), Vector3.forward,
                plateMat, "Light_Kitchen_Ceiling", "Light_Kitchen_Pendant", "Light_Kitchen_UnderCabinet");
            switches["hall_a"] = MakeSwitch(swRoot, "hall_a", "Hall A", new Vector3(-12.3f, 4.5f, 9.19f), Vector3.forward,
                plateMat, "Light_Hallway_A", "Light_Hallway_A_DoorFill");
            switches["study"] = MakeSwitch(swRoot, "study", "Study", new Vector3(-23.74f, 4.5f, LatchZ("Door_Study_Entrance", 3f)), Vector3.left,
                plateMat, "Light_Study_Ceiling", "Light_Study_DeskLamp", "Light_Study_FloorLamp");

            // ---------------- darkness when an area's lights are off (room bounds = Blender layout x 3.75)
            L2AreaDarkness dark = gmGo.AddComponent<L2AreaDarkness>();
            dark.areas = new[]
            {
                Area("Living Room", switches["living"], -53.25f, -23.74f, -29.96f, 1.95f),
                Area("Hall B", switches["hall_b"], -22.84f, -10.65f, -15.15f, -9.19f),
                Area("Bedroom", switches["bedroom"], -9.75f, 18.38f, -29.78f, -6.98f),
                Area("Corridor", switches["corridor"], -3f, 3f, -6.98f, 7.01f),
                Area("Kitchen", switches["kitchen"], -9.75f, 21.08f, 7.01f, 30.08f),
                Area("Hall A", switches["hall_a"], -22.84f, -10.65f, 9.19f, 15.19f),
                Area("Study", switches["study"], -50.1f, -23.74f, 7.01f, 35.96f),
            };

            // ---------------- checkpoints (just beyond each exit)
            Transform cpRoot = Group(gameplay, "Checkpoints");
            float py = player.position.y;
            game.checkpoints = new[]
            {
                MakeCheckpoint(cpRoot, 1, "Hall B", "exit_living", new Vector3(-16.75f, py, -12.17f), 90f),
                MakeCheckpoint(cpRoot, 2, "Corridor", "exit_bedroom", new Vector3(0f, py, 0f), 0f),
                MakeCheckpoint(cpRoot, 3, "Hall A", "exit_kitchen", new Vector3(-16.75f, py, 12.18f), 270f),
            };

            Transform props = level.Find("PuzzleProps");
            SetupLiving(props.Find("LivingRoom_TheFamilyEvening"), tvMat, starMat);
            SetupBedroom(props.Find("Bedroom_LightsOut"), uvMat, switches["bedroom"], darkMat, paperMat, ui.noteFont);
            SetupKitchen(props.Find("Kitchen_GrandmasRecipe"));
            SetupStudy(props.Find("Study_TheHackersDesk"));
            SetupFalseLeads();
            SetupOpenables();
            FixKitchenCountertopCollider();
            FixVisuals();
            BuildHidingSpots(gmGo);

            // ---------------- icons
            RenderIcons(familyPhoto);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            log.AppendLine("Scene saved: " + scene.path);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Level2_GameplaySetupReport.txt"), log.ToString());
            Debug.Log("[Level 2 Setup] Done.\n" + log);
        }

        // ================================================================== rooms
        static void SetupLiving(Transform r, Material tvMat, Material starMat)
        {
            Transform g = Group(gameplay, "LivingRoom");
            Pickup(r, "Puzzle_Living_PhotoPiece_1_TVCabinet", L2Items.Photo1);
            Pickup(r, "Puzzle_Living_PhotoPiece_2_UnderCushion", L2Items.Photo2);
            Pickup(r, "Puzzle_Living_PhotoPiece_3_InVase", L2Items.Photo3);
            Pickup(r, "Puzzle_Living_PhotoPiece_4_BehindFrame", L2Items.Photo4);
            Pickup(r, "Puzzle_Living_Batteries_AA", L2Items.Batteries);
            Pickup(r, "Puzzle_Living_StarFigurine", L2Items.StarFigurine);

            // TV screen
            Transform tvMesh = FindDeep(level, "LIV_Television");
            L2TV tv = null;
            if (tvMesh != null)
            {
                Bounds b = tvMesh.GetComponent<Renderer>().bounds;
                var tvGo = new GameObject("TV (L2TV)");
                tvGo.transform.SetParent(g, false);
                tv = tvGo.AddComponent<L2TV>();
                // The screen sits exactly on the TV model's own screen surface (its "screen" sub-mesh),
                // not on the whole model's bounds (which include the stand and the thick back), and is
                // parented to the TV so it follows it.
                Transform oldScreen = tvMesh.Find("TV Screen");
                if (oldScreen != null) Object.DestroyImmediate(oldScreen.gameObject);
                Bounds sb = ScreenSubmeshBounds(tvMesh, b);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "TV Screen";
                q.transform.position = new Vector3(sb.max.x + 0.004f, sb.center.y, sb.center.z);
                q.transform.rotation = Quaternion.LookRotation(Vector3.left);   // visible side faces +x (into the room)
                q.transform.localScale = new Vector3(sb.size.z * 0.995f, sb.size.y * 0.99f, 1f);
                q.transform.SetParent(tvMesh, true);
                log.AppendLine("TV screen fitted to the screen surface: x " + sb.max.x.ToString("0.000") + ", " + sb.size.z.ToString("0.00") + " x " + sb.size.y.ToString("0.00"));
                var qr = q.GetComponent<MeshRenderer>();
                qr.sharedMaterial = tvMat;
                qr.shadowCastingMode = ShadowCastingMode.Off;
                tv.screen = qr;
                var glow = NewLight(tvGo.transform, "TV Glow", LightType.Point, q.transform.position + Vector3.right * 1.6f, new Color(0.3f, 0.45f, 1f), 3f, 16f);
                glow.enabled = false;
                tv.screenGlow = glow;
                log.AppendLine("TV screen created on LIV_Television");
            }
            else log.AppendLine("WARNING: LIV_Television not found");

            Transform remote = r.Find("Puzzle_Living_TVRemote_NoBatteries");
            var rem = Get<L2Remote>(remote, "TV Remote");
            if (rem != null) rem.tv = tv;

            // wall clock
            Transform clockT = FindDeep(level, "LIV_WallClock");
            L2WallClock clock = null;
            if (clockT != null)
            {
                FitCollider(clockT.gameObject, 0.4f);
                clock = Get<L2WallClock>(clockT, "Wall Clock");
                clock.hourHand = clockT.Find("LIV_WallClock_Hand_Hour");
                clock.minuteHand = clockT.Find("LIV_WallClock_Hand_Minute");
            }

            // blue book + key
            Transform bookT = r.Find("Puzzle_Living_BlueBook");
            var book = Get<L2BlueBook>(bookT, "Blue Book");
            if (book != null)
            {
                book.book = bookT;
                Transform keyT = bookT.Find("Puzzle_Living_DoorKey (hidden inside book)");
                if (keyT == null) keyT = FindDeep(r, "Puzzle_Living_DoorKey (hidden inside book)");
                if (keyT != null)
                {
                    keyT.gameObject.SetActive(false);
                    book.key = keyT.gameObject;
                    var p = GetOrAdd<L2Pickup>(keyT.gameObject);
                    p.itemId = L2Items.LivingKey;
                    p.displayName = "Living Room Key";
                    FitCollider(keyT.gameObject, 0.5f, true);
                }
                Bounds bb = RendererBounds(bookT);
                book.keyRestPosition = new Vector3(bb.center.x, bb.min.y + 0.04f, bb.min.z - 0.18f);
                book.keyRestEuler = new Vector3(90f, 0f, 0f);
            }

            // star lamp + projection
            Transform lampT = r.Find("Puzzle_Living_StarShadowLamp");
            var lamp = Get<L2StarLamp>(lampT, "Star Shadow Lamp");
            Transform slotT = r.Find("Puzzle_Living_StarSlotBox");
            if (lamp != null)
            {
                lamp.tv = tv;
                lamp.clock = clock;
                Transform bulb = lampT.Find("Bulb");
                lamp.bulb = bulb != null ? bulb.GetComponent<Renderer>() : null;
                Vector3 bulbPos = bulb != null ? bulb.position : lampT.position + Vector3.up * 0.8f;
                Transform oldLamp = lampT.Find(Gen + "Lamp Light");
                if (oldLamp != null) Object.DestroyImmediate(oldLamp.gameObject);
                lamp.lampLight = NewLight(lampT, Gen + "Lamp Light", LightType.Point, bulbPos, new Color(1f, 0.82f, 0.55f), 3f, 9f);
                lamp.lampLight.enabled = false;

                Transform shelf = FindDeep(level, "Collider_LIV_Bookshelf");
                float frontZ = shelf != null ? shelf.GetComponent<Collider>().bounds.min.z : 0.63f;
                Vector3 target = slotT != null ? new Vector3(slotT.position.x, slotT.position.y + 1.25f, frontZ - 0.03f) : new Vector3(-30.7f, 5.9f, 0.6f);
                var proj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(proj.GetComponent<Collider>());
                proj.name = "Star Projection";
                proj.transform.SetParent(g, false);
                proj.transform.position = target;
                proj.transform.rotation = Quaternion.identity;   // visible side faces -z (the room)
                proj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                var pr = proj.GetComponent<MeshRenderer>();
                pr.sharedMaterial = starMat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                proj.SetActive(false);
                lamp.starProjection = proj;

                var beam = NewLight(g, "Star Beam", LightType.Spot, bulbPos, new Color(1f, 0.85f, 0.6f), 10f, 16f);
                beam.transform.rotation = Quaternion.LookRotation(target - bulbPos);
                beam.spotAngle = 22f;
                beam.innerSpotAngle = 8f;
                beam.enabled = false;
                lamp.starBeam = beam;
                log.AppendLine("Star lamp: lamp light, star beam and projection at " + target);
            }

            var slot = Get<L2StarSlot>(slotT, "Star Slot Box");
            if (slot != null)
            {
                slot.lamp = lamp;
                slot.blueBook = book;
                slot.slot = slotT.Find("StarSlot");
            }

            Examine(r, "Puzzle_Living_DoorNote", "Note by the Door", "if (clock == 8 && tv.channel == 3) {\n    star.Shine(BLUE);\n}", "Read");
            BinaryChart(g);
        }

        static void SetupBedroom(Transform r, Material uvMat, L2LightSwitch bedroomSwitch, Material darkMat, Material paperMat, TMP_FontAsset noteFont)
        {
            Pickup(r, "Puzzle_Bedroom_UVFlashlight", L2Items.UVFlashlight);

            // UV mirror text
            Transform mirror = r.Find("Puzzle_Bedroom_Mirror_MOON_UVText");
            if (mirror != null)
            {
                var uv = GetOrAdd<L2UVReveal>(mirror.gameObject);
                Transform text = mirror.Find("UVText");
                uv.text = text != null ? text.GetComponent<Renderer>() : null;
                uv.revealMaterial = uvMat;
                if (uv.text != null) uv.text.enabled = false;   // hidden in the editor too
            }

            Examine(r, "Puzzle_Bedroom_Diary", "Diary", "moves = { ?, ?, ?, ? };   // follow the dancer\nstars = ?;   // count them in the dark\nopen(drawer[stars - 1]);   // arrays start at 0!", "Read", "read_diary");

            // glow stars
            Transform stars = r.Find("Puzzle_Bedroom_GlowStars_Number5");
            if (stars != null)
            {
                var gs = GetOrAdd<L2GlowStars>(stars.gameObject);
                gs.bedroomSwitch = bedroomSwitch;
                var list = new List<Renderer>();
                foreach (Renderer rr in stars.GetComponentsInChildren<Renderer>(true)) list.Add(rr);
                gs.stars = list.ToArray();
                log.AppendLine("Glow stars: " + list.Count + " linked to the Bedroom switch");
            }

            // music box
            Transform mb = r.Find("Puzzle_Bedroom_MusicBox_Ballerina");
            var music = Get<L2MusicBox>(mb, "Music Box");
            if (music != null)
            {
                music.lid = mb.Find("Lid");
                music.lidMirror = mb.Find("Lid_Mirror");
                music.platform = mb.Find("Platform");
                var dancer = new List<Transform>();
                foreach (Transform c in mb) if (c.name.StartsWith("Ballerina_")) dancer.Add(c);
                music.dancer = dancer.ToArray();
                Transform card = mb.Find("DanceCard");
                music.danceCard = card != null ? card.GetComponent<Renderer>() : null;
                music.danceCardImage = TexOf(card);
                Bounds box = RendererBounds(mb.Find("Box"));
                music.hingePoint = new Vector3(box.min.x, box.max.y, box.center.z);
                music.hingeAxis = Vector3.forward;
                if (music.lid != null)
                {
                    // the lid's thin axis is its local y; closing rotates it back to world up
                    float s = Vector3.SignedAngle(music.lid.up, Vector3.up, Vector3.forward);
                    music.openAngle = -s;
                    log.AppendLine("Music box lid open angle " + music.openAngle.ToString("0.0"));
                }
            }

            // moon box
            Transform moon = r.Find("Puzzle_Bedroom_MoonBox_DirectionLock");
            var mbx = Get<L2MoonBox>(moon, "Moon Box");
            if (mbx != null)
            {
                mbx.lid = moon.Find("MoonLid");
                Bounds box = RendererBounds(moon.Find("Box"));
                mbx.hingeLocal = moon.InverseTransformPoint(new Vector3(box.max.x, box.max.y, box.center.z));
                mbx.hingeAxis = Vector3.forward;
                mbx.lidAngle = -100f;
                mbx.slideOut = new Vector3(1.3f, 0f, 0f);

                Transform oldI = moon.Find(Gen + "Interior");
                if (oldI != null) Object.DestroyImmediate(oldI.gameObject);
                var interior = new GameObject(Gen + "Interior");
                interior.transform.SetParent(moon, true);
                var lining = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(lining.GetComponent<Collider>());
                lining.name = "Lining";
                lining.transform.SetParent(interior.transform, true);
                lining.transform.position = new Vector3(box.center.x, box.max.y + 0.006f, box.center.z);
                lining.transform.rotation = Quaternion.identity;
                lining.transform.localScale = Vector3.one;
                SetWorldScale(lining.transform, new Vector3(box.size.x * 0.9f, 0.01f, box.size.z * 0.92f));
                lining.GetComponent<Renderer>().sharedMaterial = darkMat;

                var note = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(note.GetComponent<Collider>());
                note.name = "Hint Note";
                note.transform.SetParent(interior.transform, true);
                note.transform.position = new Vector3(box.center.x, box.max.y + 0.016f, box.center.z);
                note.transform.rotation = Quaternion.identity;
                SetWorldScale(note.transform, new Vector3(0.52f, 0.006f, 0.72f));
                note.GetComponent<Renderer>().sharedMaterial = paperMat;

                var txt = new GameObject("Text");
                txt.transform.SetParent(interior.transform, true);
                var tmp = txt.AddComponent<TextMeshPro>();
                if (noteFont != null) tmp.font = noteFont;
                tmp.text = "Turn off the lights\nand look up.";
                tmp.fontSize = 1.6f;
                tmp.color = new Color(0.15f, 0.12f, 0.25f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.rectTransform.sizeDelta = new Vector2(0.7f, 0.4f);
                txt.transform.position = new Vector3(box.center.x, box.max.y + 0.021f, box.center.z);
                txt.transform.rotation = Quaternion.Euler(90f, -90f, 0f);   // lying flat, readable from the +x side
                SetWorldScale(txt.transform, Vector3.one);

                var ex = interior.AddComponent<L2Examine>();
                ex.displayName = "Note";
                ex.title = "Note in the Moon Box";
                ex.body = "Turn off the lights and look up.";
                ex.verb = "Read";
                ex.flagOnRead = "read_moon_note";
                var bc = interior.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.center = interior.transform.InverseTransformPoint(note.transform.position);
                bc.size = Abs(interior.transform.InverseTransformVector(new Vector3(0.7f, 0.2f, 0.9f)));
                interior.SetActive(false);
                mbx.interior = interior;
                log.AppendLine("Moon box: lock, slide-out, lid hinge and interior note created");
            }

            // jewellery box
            Transform jb = r.Find("Puzzle_Bedroom_JewelleryBox_5Drawers");
            var jew = Get<L2JewelleryBox>(jb, "Jewellery Box");
            if (jew != null)
            {
                jew.drawers = new Transform[5];
                jew.knobs = new Transform[5];
                for (int i = 0; i < 5; i++) { jew.drawers[i] = jb.Find("Drawer_" + (i + 1)); jew.knobs[i] = jb.Find("Knob_" + (i + 1)); }
                jew.slide = new Vector3(-0.42f, 0f, 0f);

                // one E target per drawer (no selection window): a thin trigger in front of each drawer front
                for (int i = jb.childCount - 1; i >= 0; i--)
                    if (jb.GetChild(i).name.StartsWith(Gen + "DrawerTarget_")) Object.DestroyImmediate(jb.GetChild(i).gameObject);
                for (int i = 0; i < 5; i++)
                {
                    Transform front = jew.drawers[i];
                    if (front == null) continue;
                    Bounds fb = front.GetComponent<Renderer>().bounds;
                    var target = new GameObject(Gen + "DrawerTarget_" + (i + 1));
                    target.transform.SetParent(jb, true);
                    target.transform.SetPositionAndRotation(new Vector3(fb.min.x - 0.04f, fb.center.y, fb.center.z), Quaternion.identity);
                    SetWorldScale(target.transform, Vector3.one);
                    var tc = target.AddComponent<BoxCollider>();
                    tc.isTrigger = true;
                    tc.size = new Vector3(0.12f, 0.135f, fb.size.z + 0.02f);
                    var jd = target.AddComponent<L2JewelleryDrawer>();
                    jd.box = jew;
                    jd.number = i + 1;
                    jd.displayName = "drawer[" + i + "]";

                    // array-index label on the drawer front (moves with the drawer)
                    var lab = new GameObject(Gen + "IndexLabel_" + i);
                    lab.transform.SetParent(front, true);
                    var lt = lab.AddComponent<TextMeshPro>();
                    if (noteFont != null) lt.font = noteFont;
                    lt.text = i.ToString();
                    lt.fontSize = 0.9f;
                    lt.color = new Color(0.95f, 0.85f, 0.45f);
                    lt.alignment = TextAlignmentOptions.Center;
                    lt.textWrappingMode = TextWrappingModes.NoWrap;
                    lt.rectTransform.sizeDelta = new Vector2(0.2f, 0.12f);
                    lab.transform.position = new Vector3(fb.min.x - 0.006f, fb.center.y, fb.center.z + fb.size.z * 0.28f);
                    lab.transform.rotation = Quaternion.LookRotation(Vector3.right);   // readable from -x (the room side)
                    SetWorldScale(lab.transform, Vector3.one);
                }
                Transform oldT = jb.Find(Gen + "Drawer5_Tray");
                if (oldT != null) Object.DestroyImmediate(oldT.gameObject);
                Transform d5 = jew.drawers[4];
                var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(tray.GetComponent<Collider>());
                tray.name = Gen + "Drawer5_Tray";
                tray.transform.SetParent(jb, true);
                tray.transform.position = d5.position + new Vector3(0.22f, -0.025f, 0f);
                tray.transform.rotation = Quaternion.identity;
                SetWorldScale(tray.transform, new Vector3(0.4f, 0.07f, 0.72f));
                tray.GetComponent<Renderer>().sharedMaterial = LoadMat("L2P_Navy_Lacquer") ?? LoadMat("L2_Plastic_Black");
                jew.keyTray = tray.transform;

                // Bedroom Key: a copy of the kitchen key, smaller, lying in the tray
                Transform kk = FindDeep(level, "Puzzle_Kitchen_DoorKey_InsideOven (hidden)");
                if (kk != null)
                {
                    GameObject key = Object.Instantiate(kk.gameObject);
                    key.name = "Puzzle_Bedroom_DoorKey (in drawer 5)";
                    foreach (var c in key.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(c);
                    key.transform.SetParent(tray.transform, true);
                    key.transform.position = tray.transform.position + new Vector3(0f, 0.045f, 0f);
                    key.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    SetWorldScale(key.transform, Vector3.one * 3.75f * 0.7f);
                    var p = key.AddComponent<L2Pickup>();
                    p.itemId = L2Items.BedroomKey;
                    p.displayName = "Bedroom Key";
                    FitCollider(key, 0.35f, true);
                    key.SetActive(false);
                    jew.key = key;
                }
                log.AppendLine("Jewellery box: 5 drawers, tray + Bedroom Key in drawer 5");
            }
        }

        static void SetupKitchen(Transform r)
        {
            Transform recipeT = r.Find("Puzzle_Kitchen_RecipeBook_WithCard");
            if (recipeT != null)
            {
                foreach (var old in recipeT.GetComponents<L2Examine>()) Object.DestroyImmediate(old);
                var rc = Get<L2RecipeCard>(recipeT, "Grandma's Recipe");
                rc.image = FindPropTexture(recipeT);
            }
            Ingredient(r, "Puzzle_Kitchen_Jar_Flour_Bottom4", "Flour", 4, "100 (binary)");
            Ingredient(r, "Puzzle_Kitchen_Jar_Sugar_Bottom1", "Sugar", 1, "1 (binary)");
            Ingredient(r, "Puzzle_Kitchen_EggCarton_Bottom6", "Eggs", 6, "110 (binary)");
            Ingredient(r, "Puzzle_Kitchen_MilkBottle_Bottom2", "Milk", 2, "10 (binary)");
            Examine(r, "Puzzle_Kitchen_Scale_400g", "Kitchen Scale", "The scale reads 400 g.", "Examine");
            Examine(r, "Puzzle_Kitchen_FridgeNote", "Note on the Fridge", "Follow the recipe, and the oven will remember.", "Read");

            Transform timer = r.Find("Puzzle_Kitchen_OvenTimer_Display");
            var oven = Get<L2Oven>(timer, "Oven Timer");
            if (oven != null)
            {
                oven.code = "4162";
                oven.ovenDoor = FindDeep(level, "KIT_Stove_OvenDoor");
                Transform key = r.Find("Puzzle_Kitchen_DoorKey_InsideOven (hidden)");
                if (key != null)
                {
                    var p = GetOrAdd<L2Pickup>(key.gameObject);
                    p.itemId = L2Items.KitchenKey;
                    p.displayName = "Kitchen Key";
                    FitCollider(key.gameObject, 0.45f, true);
                    key.gameObject.SetActive(false);
                    oven.key = key.gameObject;
                    oven.ovenLight = NewLight(gameplay, "Oven Light", LightType.Point, key.position + Vector3.up * 0.9f, new Color(1f, 0.75f, 0.45f), 1.5f, 4f);
                    oven.ovenLight.enabled = false;
                }
                oven.requiredFlag = "recipe_complete";
                oven.doorNote = OvenDoorNote(oven.ovenDoor);
                log.AppendLine("Oven: keypad 4162 (after the recipe blank), door " + (oven.ovenDoor != null ? "found" : "NOT FOUND") + ", door note " + (oven.doorNote != null));
            }
        }

        static void SetupStudy(Transform r)
        {
            Transform mon = r.Find("Puzzle_Study_Monitor_LoginScreen");
            var ws = Get<L2Workstation>(mon, "Computer");
            if (ws != null)
            {
                ws.password = "BYTE2019";
                Transform s = mon.Find("Screen");
                ws.screen = s != null ? s.GetComponent<Renderer>() : null;
                ws.unlockedScreen = LoadMat("L2_Screen_Monitor_Code");
            }
            Examine(r, "Puzzle_Study_StickyNote_Password", "Sticky Note", "My password is who I love most, followed by my proudest year.", "Read");
            Transform wheelT = r.Find("Puzzle_Study_CipherWheel");
            if (wheelT != null)
            {
                foreach (var old in wheelT.GetComponents<L2Examine>()) Object.DestroyImmediate(old);
                Get<L2CipherWheel>(wheelT, "Cipher Wheel");
            }
            Examine(r, "Puzzle_Study_CipherNote_SHWQDPH", "Cipher Note", "SHW  QDPH\nshift = ?", "Read");
            Examine(r, "Puzzle_Study_DogPhoto", "Dog Photo", "\"Byte - best boy\"", "Examine");
            Examine(r, "Puzzle_Study_DogCollar_TagBYTE", "Dog Collar", "The tag on the collar says: BYTE", "Examine");
            Examine(r, "Puzzle_Study_Certificate_Award2019", "Certificate", "Programming Award - 2019", "Read");
        }

        static void SetupFalseLeads()
        {
            Transform added = level.Find("DecorativeProps/Added_Props");
            Examine(added, "FalseLead_Living_TVGuide_Ch7", "TV Guide", "Tonight's TV guide. Channel 7 is circled.", "Read");
            Examine(added, "FalseLead_Living_MovieNightNote", "Note", "Movie night 9:30!", "Read");
            Examine(added, "FalseLead_Living_RedBook", "Red Book", "A red book. Nothing inside but pages of old recipes for success.", "Examine");
            Examine(added, "FalseLead_Bedroom_StarChartPoster", "Constellation Poster", "A poster of the night sky and its constellations.", "Examine");
            Examine(added, "FalseLead_Bedroom_DentistNote", "Note", "A reminder about a dentist appointment.", "Read");
            Examine(added, "AlarmClock", "Alarm Clock", "The alarm clock reads 7:00.", "Examine");
            Examine(added, "Phone", "Phone", "The lock screen shows 7:00.", "Examine");
            Examine(added, "FalseLead_Kitchen_Oven350Note", "Sticky Note", "Oven 350° / 45 min", "Read");
            Examine(added, "FalseLead_Kitchen_ShoppingList", "Shopping List", "A shopping list.", "Read");
            Examine(added, "FalseLead_Kitchen_PizzaMenu", "Pizza Menu", "A takeaway pizza menu with a phone number.", "Read");
            Examine(added, "FalseLead_Study_WiFiNote", "WiFi Note", "WiFi password: sunflower88", "Read");
            Examine(added, "FalseLead_Study_OldPasswordNote", "Old Note", "An old password, crossed out: admin1234", "Read");
            Examine(added, "Calculator", "Calculator", "The calculator display shows 2019.", "Examine");
            Examine(added, "DogBed", "Dog Bed", "A well-used dog bed, covered in short fur.", "Examine");
            Examine(added, "DogBowl", "Dog Bowl", "A dog's water bowl.", "Examine");
        }

        // ================================================================== drawers, cabinets, closets
        // Every furniture drawer / door in the house model gets L2Openable + a trigger box (no solid
        // collider, so nothing can trap the player). The oven door stays with the oven keypad puzzle.
        static void SetupOpenables()
        {
            Transform structure = FindDeep(level, "Level2_House_Structure");
            if (structure == null) { log.AppendLine("WARNING: Level2_House_Structure not found (openables skipped)"); return; }

            // separate props that may be stuck on a door or drawer front (fridge notes, magnets...)
            var props = new List<Transform>();
            Transform pp = level.Find("PuzzleProps");
            if (pp != null) foreach (Transform grp in pp) foreach (Transform p in grp) props.Add(p);
            Transform ap = level.Find("DecorativeProps/Added_Props");
            if (ap != null) foreach (Transform grp in ap) foreach (Transform p in grp) props.Add(p);

            int drawers = 0, doors = 0, attachedCount = 0;
            var perRoom = new SortedDictionaryLite();
            foreach (MeshRenderer r in structure.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.name;
                bool isDoor = n.Contains("_Door_") || n.EndsWith("_Door");
                bool isDrawer = n.Contains("_Drawer");
                if (!isDoor && !isDrawer) continue;
                if (n.StartsWith("HALL_")) continue;
                Transform part = r.transform;
                Renderer body = part.parent != null ? part.parent.GetComponent<Renderer>() : null;
                if (body == null) continue;

                Bounds pb = r.bounds, bb = body.bounds;
                Vector3 pivot = part.position;
                Vector3 outward = FacingOf(isDoor, pb, bb, pivot);

                L2Openable o = GetOrAdd<L2Openable>(part.gameObject);
                o.kind = isDoor ? L2Openable.Kind.Door : L2Openable.Kind.Drawer;
                string parent = part.parent.name;
                o.label = isDrawer ? "Drawer"
                        : n.Contains("Freezer") ? "Freezer"
                        : parent.Contains("Refrigerator") ? "Fridge"
                        : parent.Contains("Wardrobe") ? "Wardrobe"
                        : parent.Contains("Pantry") ? "Pantry"
                        : "Cabinet";
                o.displayName = parent.Replace("_", " ") + " " + o.label;

                if (isDoor)
                {
                    // swing the free edge outward, around the pivot (= the hinge in these models)
                    Vector3 v = Flat(pb.center - pivot);
                    float sign = Vector3.Dot(Quaternion.AngleAxis(90f, Vector3.up) * v, outward) >= 0f ? 1f : -1f;
                    o.openAngle = 100f * sign;
                    o.slide = Vector3.zero;
                    doors++;
                }
                else
                {
                    float depth = Mathf.Abs(Vector3.Dot(pb.size, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z))));
                    o.slide = outward * Mathf.Clamp(depth * 0.6f, 0.15f, 1.3f);
                    o.openAngle = 0f;
                    drawers++;
                }

                // click target = the part's own mesh box, as a trigger
                MeshFilter mf = part.GetComponent<MeshFilter>();
                BoxCollider bc = GetOrAdd<BoxCollider>(part.gameObject);
                bc.isTrigger = true;
                if (mf != null && mf.sharedMesh != null) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }

                // props stuck on the outside of this part move with it
                var att = new List<Transform>();
                Bounds near = pb;
                near.Expand(new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z)) * 0.7f + Vector3.one * 0.05f);
                foreach (Transform p in props)
                {
                    if (p == null || p.IsChildOf(part)) continue;
                    Bounds b = RendererBounds(p);
                    if (b.size == Vector3.zero) continue;
                    if (near.Contains(b.center) && !bb.Contains(b.center)) att.Add(p);
                }
                o.attached = att.ToArray();
                attachedCount += att.Count;
                if (att.Count > 0) log.AppendLine("  " + n + " carries: " + string.Join(", ", att.ConvertAll(t => t.name)));

                string room = n.StartsWith("BED_") ? "Bedroom" : n.StartsWith("KIT_") ? "Kitchen" : n.StartsWith("LIV_") ? "Living Room" : n.StartsWith("STU_") ? "Study" : "Other";
                perRoom.Add(room);
            }
            log.AppendLine("Openables: " + drawers + " drawers, " + doors + " doors (" + perRoom + "), " + attachedCount + " stuck-on props move with their part");
        }

        class SortedDictionaryLite
        {
            readonly Dictionary<string, int> d = new Dictionary<string, int>();
            public void Add(string k) => d[k] = d.TryGetValue(k, out int v) ? v + 1 : 1;
            public override string ToString()
            {
                var keys = new List<string>(d.Keys);
                keys.Sort();
                return string.Join(", ", keys.ConvertAll(k => k + " " + d[k]));
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // Which way a drawer / door faces (out of the furniture), snapped to a world axis.
        //  - Drawer: these models put the drawer's pivot on its front face, so pivot - centre points out.
        //  - Door:   the panel's thinnest horizontal side is its facing axis; it points away from the body.
        // (The body centre alone is unreliable: side-by-side doors and drawers sit off-centre.)
        public static Vector3 FacingOf(bool isDoor, Bounds part, Bounds body, Vector3 pivot)
        {
            if (!isDoor)
            {
                Vector3 f = Flat(pivot - part.center);
                if (f.sqrMagnitude > 1e-4f) return SnapAxis(f);
            }
            bool thinX = part.size.x <= part.size.z;
            float s = thinX ? Mathf.Sign(part.center.x - body.center.x) : Mathf.Sign(part.center.z - body.center.z);
            if (s == 0f) s = 1f;
            return thinX ? new Vector3(s, 0f, 0f) : new Vector3(0f, 0f, s);
        }

        static Vector3 SnapAxis(Vector3 v)
        {
            if (v.sqrMagnitude < 1e-6f) return Vector3.forward;
            return Mathf.Abs(v.x) >= Mathf.Abs(v.z) ? new Vector3(Mathf.Sign(v.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        // ================================================================== kitchen countertop collider
        // The countertop is L-shaped but had ONE box covering the whole rectangle around the L, which
        // blocked the open floor in front of the counters. It becomes two boxes, one per arm, at the
        // same height (the base cabinets keep their own colliders underneath).
        static void FixKitchenCountertopCollider()
        {
            Transform cols = FindDeep(level, "Level2_Colliders");
            Transform main = cols != null ? cols.Find("Collider_KIT_Countertop_Marble") : null;
            if (main == null) { log.AppendLine("WARNING: Collider_KIT_Countertop_Marble not found"); return; }
            const float y0 = 2.55f, y1 = 5.44f;
            SetBox(main, "back arm", new Vector3(2.81f, y0, 27.66f), new Vector3(21.09f, y1, 30.09f));
            Transform side = cols.Find("Collider_KIT_Countertop_Marble_SideArm");
            if (side == null)
            {
                side = new GameObject("Collider_KIT_Countertop_Marble_SideArm").transform;
                side.SetParent(cols, false);
                side.SetSiblingIndex(main.GetSiblingIndex() + 1);
                side.gameObject.isStatic = main.gameObject.isStatic;
                side.gameObject.layer = main.gameObject.layer;
            }
            SetBox(side, "side arm", new Vector3(18.66f, y0, 18.77f), new Vector3(21.09f, y1, 27.66f));
        }

        static void SetBox(Transform t, string what, Vector3 min, Vector3 max)
        {
            t.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
            t.localScale = Vector3.one;
            BoxCollider bc = GetOrAdd<BoxCollider>(t.gameObject);
            bc.isTrigger = false;
            bc.center = Vector3.zero;
            Vector3 ps = t.lossyScale;
            Vector3 size = max - min;
            bc.size = new Vector3(size.x / ps.x, size.y / ps.y, size.z / ps.z);
            log.AppendLine("Kitchen countertop collider (" + what + "): " + t.name + " x[" + min.x + "," + max.x + "] z[" + min.z + "," + max.z + "]");
        }

        // ================================================================== helpers: components
        static void SetupDoor(string name, string key, string exitFlag)
        {
            Transform d = FindDeep(level.Find("Doors"), name);
            if (d == null) { log.AppendLine("WARNING: door " + name + " not found"); return; }
            L2Door door = GetOrAdd<L2Door>(d.gameObject);
            door.displayName = name.Replace("Door_", "").Replace("_", " ") + " Door";
            door.pivot = d.Find("DoorPivot");
            Transform leaf = d.Find("DoorPivot/DoorLeaf");
            door.leafCollider = leaf != null ? leaf.GetComponent<Collider>() : null;
            Transform zone = d.Find("InteractionZone");
            door.doorwayZone = zone != null ? zone.GetComponent<BoxCollider>() : null;
            float current = door.pivot != null ? door.pivot.localEulerAngles.y : 270f;
            if (Mathf.Abs(Mathf.DeltaAngle(current, 0f)) > 10f) door.openAngle = current;   // the doors were left open (±90°)
            door.closedAngle = 0f;
            door.requiredKey = key ?? "";
            door.exitFlag = exitFlag ?? "";
            log.AppendLine("Door " + name + ": key=" + (key ?? "none") + " open=" + door.openAngle.ToString("0") + " closed=0");
        }

        static float LatchZ(string door, float offset)
        {
            Transform d = FindDeep(level.Find("Doors"), door);
            Transform p = d != null ? d.Find("DoorPivot") : null;
            if (d == null || p == null) return 0f;
            return d.position.z + Mathf.Sign(d.position.z - p.position.z) * offset;
        }

        static float LatchX(string door, float offset)
        {
            Transform d = FindDeep(level.Find("Doors"), door);
            Transform p = d != null ? d.Find("DoorPivot") : null;
            if (d == null || p == null) return 0f;
            return d.position.x + Mathf.Sign(d.position.x - p.position.x) * offset;
        }

        static L2LightSwitch MakeSwitch(Transform parent, string group, string area, Vector3 approx, Vector3 normal, Material mat, params string[] lightNames)
        {
            // snap onto the real wall surface
            Vector3 pos = approx;
            Vector3 from = approx + normal * 2.5f;
            foreach (RaycastHit h in SortedHits(from, -normal, 5f))
            {
                if (h.collider.isTrigger) continue;
                if (h.collider.name.Contains("Wall") || h.collider.name.Contains("Jamb")) { pos = h.point; break; }
            }

            var root = new GameObject("LightSwitch_" + area.Replace(" ", ""));
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal));

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.name = "Plate";
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            plate.transform.localScale = new Vector3(0.3f, 0.45f, 0.05f);
            plate.GetComponent<Renderer>().sharedMaterial = mat;

            var pivot = new GameObject("Rocker Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            var rocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(rocker.GetComponent<Collider>());
            rocker.name = "Rocker";
            rocker.transform.SetParent(pivot.transform, false);
            rocker.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            rocker.transform.localScale = new Vector3(0.1f, 0.17f, 0.05f);
            rocker.GetComponent<Renderer>().sharedMaterial = mat;

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0f, 0.1f);
            col.size = new Vector3(0.6f, 0.75f, 0.3f);

            var sw = root.AddComponent<L2LightSwitch>();
            sw.group = group;
            sw.areaName = area;
            sw.displayName = area + " Light Switch";
            sw.rocker = pivot.transform;
            var lights = new List<Light>();
            foreach (string n in lightNames)
            {
                Transform lt = FindDeep(level.Find("Lighting"), n);
                if (lt != null && lt.GetComponent<Light>() != null) lights.Add(lt.GetComponent<Light>());
                else log.AppendLine("WARNING: light " + n + " not found for " + area);
            }
            sw.lights = lights.ToArray();
            log.AppendLine("Switch " + area + " at " + pos.ToString("0.00") + " -> " + lights.Count + " lights");
            return sw;
        }

        static L2AreaDarkness.Area Area(string name, L2LightSwitch sw, float x0, float x1, float z0, float z1) =>
            new L2AreaDarkness.Area { name = name, lightSwitch = sw, min = new Vector3(x0, -1f, z0), max = new Vector3(x1, 12f, z1) };

        static L2Checkpoint MakeCheckpoint(Transform parent, int index, string label, string flag, Vector3 pos, float yaw)
        {
            var go = new GameObject("Checkpoint_" + index + "_" + label.Replace(" ", ""));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var cp = go.AddComponent<L2Checkpoint>();
            cp.index = index;
            cp.label = label;
            cp.requiredFlag = flag;
            cp.size = new Vector3(5.6f, 12f, 11f);
            log.AppendLine("Checkpoint " + index + " " + label + " at " + pos.ToString("0.00") + " yaw " + yaw);
            return cp;
        }

        static void Pickup(Transform room, string name, string item)
        {
            Transform t = room != null ? room.Find(name) : null;
            if (t == null) { log.AppendLine("WARNING: pickup " + name + " not found"); return; }
            var p = GetOrAdd<L2Pickup>(t.gameObject);
            p.itemId = item;
            p.displayName = L2Items.NameOf(item);
            if (t.GetComponentInChildren<Collider>(true) == null) FitCollider(t.gameObject, 0.4f, true);
        }

        // ================================================================== visual fixes
        // Bounds of the TV model's screen sub-mesh (the material whose name contains "Screen").
        static Bounds ScreenSubmeshBounds(Transform tv, Bounds fallback)
        {
            var mf = tv.GetComponent<MeshFilter>();
            var mr = tv.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || mr == null) return fallback;
            Mesh mesh = mf.sharedMesh;
            Vector3[] v = mesh.vertices;
            Matrix4x4 M = tv.localToWorldMatrix;
            for (int sm = 0; sm < mesh.subMeshCount && sm < mr.sharedMaterials.Length; sm++)
            {
                Material mat = mr.sharedMaterials[sm];
                if (mat == null || !mat.name.Contains("Screen")) continue;
                bool any = false;
                Bounds b = new Bounds();
                foreach (int i in mesh.GetTriangles(sm))
                {
                    Vector3 w = M.MultiplyPoint3x4(v[i]);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; }
                    else b.Encapsulate(w);
                }
                if (any) return b;
            }
            return fallback;
        }

        // Targeted fixes for visual bugs found in the Level 2 house (idempotent).
        static void FixVisuals()
        {
            // 1) White decor vase hanging half off the right end of the living-room sideboard
            //    (sideboard top ends at x -42.0, vase reached -41.34): move it to the free spot between
            //    the second candle and the photo frame.
            Transform vase = FindDeep(level, "LIV_DecorVase_3");
            if (vase != null)
            {
                Bounds vb = vase.GetComponent<Renderer>().bounds;
                if (vb.max.x > -42.05f)
                {
                    vase.position += new Vector3(-45.98f - vb.center.x, 0f, 0f);
                    log.AppendLine("Visual fix: LIV_DecorVase_3 moved back onto the sideboard top");
                }
            }

            // 2) Kitchen upper cabinets: the underside panel used pure black plastic, which read as a
            //    missing / see-through bottom. Give it the cabinet's own laminate (renderer slot only).
            Material laminate = LoadMat("L2_Cabinet_SageLaminate");
            int cab = 0;
            for (int i = 1; i <= 6; i++)
            {
                Transform u = FindDeep(level, "KIT_UpperCabinet_U" + i);
                Renderer r = u != null ? u.GetComponent<Renderer>() : null;
                if (r == null || laminate == null) continue;
                Material[] mats = r.sharedMaterials;
                bool changed = false;
                for (int k = 0; k < mats.Length; k++)
                    if (mats[k] != null && mats[k].name == "L2_Plastic_Black") { mats[k] = laminate; changed = true; }
                if (changed) { r.sharedMaterials = mats; cab++; }
            }
            log.AppendLine("Visual fix: kitchen upper cabinet undersides -> laminate on " + cab + " cabinets");

            // 3) Kitchen sink: the basin's side walls only face outwards, so from above they are invisible
            //    and the dark cabinet shows through as black slivers. Add inward-facing steel walls.
            Transform counter = FindDeep(level, "KIT_Countertop_Marble");
            if (counter != null)
            {
                Transform old = counter.Find(Gen + "SinkInnerWalls");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var walls = new GameObject(Gen + "SinkInnerWalls");
                walls.transform.SetParent(counter, true);
                Material steel = LoadMat("L2_Metal_StainlessSteel");
                float x0 = 7.43f, x1 = 10.58f, z0 = 28.18f, z1 = 29.72f, y0 = 2.55f, y1 = 3.37f, e = 0.004f;
                float ym = (y0 + y1) * 0.5f, h = y1 - y0;
                SinkWall(walls.transform, "Wall_Back", new Vector3((x0 + x1) * 0.5f, ym, z1 - e), Vector3.forward, new Vector2(x1 - x0, h), steel);
                SinkWall(walls.transform, "Wall_Front", new Vector3((x0 + x1) * 0.5f, ym, z0 + e), Vector3.back, new Vector2(x1 - x0, h), steel);
                SinkWall(walls.transform, "Wall_Left", new Vector3(x0 + e, ym, (z0 + z1) * 0.5f), Vector3.left, new Vector2(z1 - z0, h), steel);
                SinkWall(walls.transform, "Wall_Right", new Vector3(x1 - e, ym, (z0 + z1) * 0.5f), Vector3.right, new Vector2(z1 - z0, h), steel);
                log.AppendLine("Visual fix: sink basin inner walls added");
            }
        }

        // A quad whose visible side faces away from 'wallDir' (i.e. into the basin).
        static void SinkWall(Transform parent, string name, Vector3 pos, Vector3 wallDir, Vector2 size, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name;
            q.transform.position = pos;
            q.transform.rotation = Quaternion.LookRotation(wallDir);   // visible side = -forward = towards the basin centre
            q.transform.SetParent(parent, true);
            SetWorldScale(q.transform, new Vector3(size.x, size.y, 1f));
            q.GetComponent<Renderer>().sharedMaterial = m;
        }

        // ================================================================== randomized hiding spots
        static string RoomOf(Vector3 p)
        {
            if (p.x > -53.3f && p.x < -23.7f && p.z > -30f && p.z < 2f) return "living";
            if (p.x > -9.8f && p.x < 18.4f && p.z > -29.8f && p.z < -7f) return "bedroom";
            return null;
        }

        static readonly string[] RandomItems =
            { L2Items.Photo1, L2Items.Photo2, L2Items.Photo3, L2Items.Photo4, L2Items.Batteries, L2Items.StarFigurine, L2Items.UVFlashlight };

        static void BuildHidingSpots(GameObject gm)
        {
            // clean spots from earlier runs (drawer spots live under the drawers)
            foreach (L2HidingSpot old in Object.FindObjectsByType<L2HidingSpot>(FindObjectsInactive.Include))
                Object.DestroyImmediate(old.gameObject);
            Transform root = Group(gameplay, "HidingSpots");

            var shuffle = gm.AddComponent<L2ItemShuffle>();
            var pickups = new Dictionary<string, L2Pickup>();
            foreach (L2Pickup p in level.GetComponentsInChildren<L2Pickup>(true)) pickups[p.itemId] = p;

            // each item's own hand-made hiding place stays a valid option
            foreach (string id in RandomItems)
            {
                if (!pickups.TryGetValue(id, out L2Pickup p)) { log.AppendLine("WARNING: pickup " + id + " not found"); continue; }
                string room = RoomOf(RendererBounds(p.transform).center);
                var go = new GameObject(Gen + "HideSpot_Original_" + id);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(p.transform.position, p.transform.rotation);
                var s = go.AddComponent<L2HidingSpot>();
                s.room = room; s.onlyItem = id;
                shuffle.spots.Add(s);
                shuffle.items.Add(new L2ItemShuffle.Entry { itemId = id, room = room, pickup = p });
            }

            // drawers and cabinets of the Living Room and Bedroom (the jewellery box is a puzzle, not a hiding place)
            int made = 0;
            var openables = new List<L2Openable>(Object.FindObjectsByType<L2Openable>());
            openables.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (L2Openable o in openables)
            {
                Renderer pr = o.GetComponent<Renderer>();
                if (pr == null) continue;
                string room = RoomOf(pr.bounds.center);
                if (room == null) continue;
                if (!SpotFloor(o, out Vector3 floor)) { log.AppendLine("  no floor found in " + o.name); continue; }
                var go = new GameObject(Gen + "HideSpot_" + o.name);
                if (o.kind == L2Openable.Kind.Drawer) go.transform.SetParent(o.transform, true);
                else go.transform.SetParent(root, true);
                go.transform.position = floor;
                var s = go.AddComponent<L2HidingSpot>();
                s.room = room; s.container = o;
                shuffle.spots.Add(s);
                made++;
            }
            log.AppendLine("Randomized items: " + shuffle.items.Count + ", hiding spots: " + shuffle.spots.Count + " (" + made + " drawers / cabinets)");
        }

        // Finds the inside floor of a drawer (its bottom) or of the cabinet behind a door (lowest shelf
        // below the door's middle), by ray-casting against a temporary mesh collider.
        static bool SpotFloor(L2Openable o, out Vector3 floor)
        {
            floor = Vector3.zero;
            Renderer pr = o.GetComponent<Renderer>();
            Bounds b = pr.bounds;
            Transform meshT = o.transform;
            Vector3 origin;
            if (o.kind == L2Openable.Kind.Drawer)
            {
                origin = new Vector3(b.center.x, b.max.y - 0.02f, b.center.z);
            }
            else
            {
                Renderer body = o.transform.parent != null ? o.transform.parent.GetComponent<Renderer>() : null;
                if (body == null) return false;
                meshT = body.transform;
                Bounds cb = body.bounds;
                // the door's thin axis points out of the cabinet; step inside by half the cabinet depth
                Vector3 s = b.size;
                Vector3 outDir = s.x < s.z ? new Vector3(Mathf.Sign(b.center.x - cb.center.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(b.center.z - cb.center.z));
                float depth = Mathf.Abs(outDir.x) > 0f ? cb.size.x : cb.size.z;
                Vector3 inner = new Vector3(b.center.x, 0f, b.center.z) - outDir * (depth * 0.45f);
                origin = new Vector3(inner.x, Mathf.Min(b.center.y, cb.max.y - 0.15f), inner.z);
            }
            MeshFilter mf = meshT.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            var mc = meshT.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            bool ok = mc.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, 6f);
            Object.DestroyImmediate(mc);
            if (!ok || hit.normal.y < 0.7f) return false;
            floor = hit.point;
            return true;
        }

        // Binary place-value poster (8 4 2 1) on the TV wall of the Living Room, beside the TV.
        static void BinaryChart(Transform g)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Props/L2P_T_BinaryChart.png");
            Material m = UnlitMaterial(GenDir + "/Materials/L2G_BinaryChart.mat", tex, Color.white, false);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Puzzle_Living_BinaryChartPoster";
            go.transform.SetParent(g, false);
            go.transform.position = new Vector3(-53.25f + 0.03f, 6.4f, -21.2f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left);   // visible side faces +x (into the room)
            go.transform.localScale = new Vector3(2.0f, 2.5f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1f, 1f, 0.2f);
            var ex = go.AddComponent<L2Examine>();
            ex.displayName = "Binary Chart";
            ex.title = "Binary Numbers";
            ex.image = tex;
            ex.body = "Place values 8 4 2 1: add up the places that have a 1.";
            ex.verb = "Read the Poster";
            log.AppendLine("Binary chart poster on the Living Room TV wall");
        }

        // "password = petName + year;" on the inside of the oven door (shown when the oven opens).
        static GameObject OvenDoorNote(Transform ovenDoor)
        {
            if (ovenDoor == null) return null;
            Transform old = ovenDoor.Find(Gen + "OvenDoorNote");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Renderer dr = ovenDoor.GetComponent<Renderer>();
            Bounds b = dr != null ? dr.bounds : new Bounds(ovenDoor.position, Vector3.one);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Props/L2P_T_Note_OvenDoor.png");
            Material m = UnlitMaterial(GenDir + "/Materials/L2G_OvenDoorNote.mat", tex, Color.white, false);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = Gen + "OvenDoorNote";
            go.transform.position = new Vector3(b.center.x, b.center.y, b.max.z + 0.012f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.back);   // visible side faces +z (the oven inside)
            go.transform.SetParent(ovenDoor, true);
            SetWorldScale(go.transform, new Vector3(b.size.x * 0.55f, b.size.x * 0.275f, 1f));
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1f, 1f, 0.3f);
            var ex = go.AddComponent<L2Examine>();
            ex.displayName = "Scribbled Note";
            ex.title = "Note Inside the Oven Door";
            ex.image = tex;
            ex.body = "password = petName + year;";
            ex.verb = "Read";
            go.SetActive(false);
            return go;
        }

        static void Ingredient(Transform room, string name, string label, int number, string bottom)
        {
            Transform t = room.Find(name);
            var ing = Get<L2Ingredient>(t, label);
            if (ing == null) return;
            ing.ingredient = label;
            ing.number = number;
            ing.label = bottom;
        }

        static void Examine(Transform room, string name, string title, string body, string verb, string flag = null)
        {
            Transform t = room != null ? FindDeep(room, name) : null;
            if (t == null) { log.AppendLine("WARNING: examine target " + name + " not found"); return; }
            var ex = GetOrAdd<L2Examine>(t.gameObject);
            ex.displayName = title;
            ex.title = title;
            ex.body = body;
            ex.verb = verb;
            ex.flagOnRead = flag ?? "";
            ex.image = FindPropTexture(t);
            if (t.GetComponentInChildren<Collider>(true) == null) FitCollider(t.gameObject, 0.35f, true);
        }

        static T Get<T>(Transform t, string display) where T : L2Interactable
        {
            if (t == null) { log.AppendLine("WARNING: object for " + typeof(T).Name + " (" + display + ") not found"); return null; }
            T c = GetOrAdd<T>(t.gameObject);
            c.displayName = display;
            if (t.GetComponentInChildren<Collider>(true) == null) FitCollider(t.gameObject, 0.4f, true);
            return c;
        }

        // ================================================================== helpers: geometry
        static Transform FindRoot(string name)
        {
            foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == name) return g.transform;
            return null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
            return null;
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static Bounds RendererBounds(Transform t)
        {
            if (t == null) return new Bounds();
            Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();   // Unity's fake-null: never use ?? with components
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static void SetWorldScale(Transform t, Vector3 world)
        {
            Vector3 ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(world.x / ps.x, world.y / ps.y, world.z / ps.z);
        }

        // Trigger box around the object's renderers (at least 'minSize' Unity units per side).
        static void FitCollider(GameObject go, float minSize, bool trigger = true)
        {
            BoxCollider bc = GetOrAdd<BoxCollider>(go);
            bool wasActive = go.activeSelf;
            go.SetActive(true);   // renderer bounds are only valid on active objects
            Bounds b = RendererBounds(go.transform);
            go.SetActive(wasActive);
            Vector3 size = new Vector3(Mathf.Max(b.size.x, minSize), Mathf.Max(b.size.y, minSize), Mathf.Max(b.size.z, minSize));
            bc.center = go.transform.InverseTransformPoint(b.center);
            Vector3 local = Abs(go.transform.InverseTransformVector(size));
            bc.size = local;
            bc.isTrigger = trigger;
        }

        static List<RaycastHit> SortedHits(Vector3 from, Vector3 dir, float dist)
        {
            var list = new List<RaycastHit>(Physics.RaycastAll(from, dir, dist, ~0, QueryTriggerInteraction.Ignore));
            list.Sort((a, b) => a.distance.CompareTo(b.distance));
            return list;
        }

        static Light NewLight(Transform parent, string name, LightType type, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // ================================================================== helpers: assets
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material LoadMat(string name)
        {
            foreach (string g in AssetDatabase.FindAssets(name + " t:Material", new[] { Root + "/Materials" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
            }
            return null;
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CodeBreaker/UI/Sprites/" + name + ".png");

        static Texture FindPropTexture(Transform t)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                Texture tex = TexOfRenderer(r);
                if (tex != null && tex.name.StartsWith("L2P_T_")) return tex;
            }
            return null;
        }

        static Texture TexOf(Transform t) => t != null ? TexOfRenderer(t.GetComponent<Renderer>()) : null;

        static Texture TexOfRenderer(Renderer r)
        {
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_BaseMap")) return null;
            return r.sharedMaterial.GetTexture("_BaseMap");
        }

        static Material UnlitMaterial(string path, Texture tex, Color color, bool transparent)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D SavePng(Texture2D tex, string path, bool sprite)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite) ti.spriteImportMode = SpriteImportMode.Single;   // needed for Resources.Load<Sprite>
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = !sprite;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D MakeStarTexture(string path)
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0) ? 0.46f : 0.19f;
                pts[i] = new Vector2(0.5f + Mathf.Cos(a) * rad, 0.5f + Mathf.Sin(a) * rad);
            }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            if (InPoly(new Vector2((x + (sx + 0.5f) / 3f) / N, (y + (sy + 0.5f) / 3f) / N), pts)) inside++;
                    float d = Vector2.Distance(new Vector2(x, y) / N, new Vector2(0.5f, 0.5f));
                    float halo = Mathf.Clamp01(1f - d / 0.5f) * 0.18f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(inside / 9f, halo)));
                }
            tex.Apply();
            return SavePng(tex, path, false);
        }

        static bool InPoly(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            return c;
        }

        static Texture2D MakeUVMoonTexture(string src, string path)
        {
            var s = new Texture2D(2, 2);
            s.LoadImage(File.ReadAllBytes(src));
            var o = new Texture2D(s.width, s.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < s.height; y++)
                for (int x = 0; x < s.width; x++)
                {
                    Color c = s.GetPixel(x, y);
                    float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    float a = c.a < 0.1f ? 0f : Mathf.Clamp01((1f - min) * 4f) * c.a;
                    o.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            o.Apply();
            Object.DestroyImmediate(s);
            return SavePng(o, path, false);
        }

        static Texture2D MakeFamilyPhoto(Texture2D[] pieces, string path)
        {
            const int W = 512, H = 384;
            var o = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            var fill = new Color[W * 2 * H * 2];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color(0.96f, 0.95f, 0.92f, 1f);
            o.SetPixels(fill);
            for (int k = 0; k < 4; k++)
            {
                if (pieces[k] == null) continue;
                var s = new Texture2D(2, 2);
                s.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(pieces[k])));
                int ox = (k % 2) * W, oy = (1 - k / 2) * H;   // piece 1 top-left ... piece 4 bottom-right
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        Color c = s.GetPixelBilinear((x + 0.5f) / W, (y + 0.5f) / H);
                        Color bg = o.GetPixel(ox + x, oy + y);
                        o.SetPixel(ox + x, oy + y, Color.Lerp(bg, new Color(c.r, c.g, c.b, 1f), c.a));
                    }
                Object.DestroyImmediate(s);
            }
            o.Apply();
            return SavePng(o, path, false);
        }

        // ================================================================== item icons
        static void RenderIcons(Texture2D familyPhoto)
        {
            var protos = new Dictionary<string, Transform>();
            foreach (L2Pickup p in level.GetComponentsInChildren<L2Pickup>(true)) protos[p.itemId] = p.transform;

            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("L2 Icon Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.62f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.12f, 0.11f, 1f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.targetTexture = rt;
            var lightGo = new GameObject("L2 Icon Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var fillGo = new GameObject("L2 Icon Fill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.6f;
            fill.transform.rotation = Quaternion.Euler(-30f, 150f, 0f);

            Vector3 stage = new Vector3(0f, -2000f, 0f);
            int made = 0;
            foreach (var kv in protos)
            {
                GameObject v = L2HeldItem.BuildVisual(kv.Value, 1f, "Icon " + kv.Key);
                v.transform.position = stage;
                Bounds b = RendererBounds(v.transform);
                Vector3 s = b.size;
                Vector3 dir;
                float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (s.y < max * 0.25f) dir = new Vector3(0.12f, 1f, -0.25f).normalized;           // flat on a surface: from above
                else if (s.x < max * 0.25f) dir = new Vector3(1f, 0.25f, -0.15f).normalized;       // thin along x
                else if (s.z < max * 0.25f) dir = new Vector3(0.15f, 0.25f, -1f).normalized;       // thin along z
                else dir = new Vector3(0.65f, 0.55f, -0.75f).normalized;
                cam.transform.position = b.center + dir * 5f;
                cam.transform.rotation = Quaternion.LookRotation(-dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up);
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                SavePng(tex, IconDir + "/" + kv.Key + ".png", true);
                Object.DestroyImmediate(v);
                made++;
            }

            // Family Photo icon from the composed photo
            if (familyPhoto != null)
            {
                string src = AssetDatabase.GetAssetPath(familyPhoto);
                var s = new Texture2D(2, 2);
                s.LoadImage(File.ReadAllBytes(src));
                var o = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        float u = x / 255f, w = (y - 32) / 191f;
                        o.SetPixel(x, y, w < 0f || w > 1f ? new Color(0.13f, 0.12f, 0.11f, 1f) : s.GetPixelBilinear(u, w));
                    }
                o.Apply();
                Object.DestroyImmediate(s);
                SavePng(o, IconDir + "/" + L2Items.FamilyPhoto + ".png", true);
                made++;
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(fillGo);
            rt.Release();
            Object.DestroyImmediate(rt);
            log.AppendLine("Icons rendered: " + made + " -> " + IconDir);
        }
    }
}
