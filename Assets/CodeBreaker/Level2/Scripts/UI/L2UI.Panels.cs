using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace CodeBreaker.Level2
{
    // Inventory and puzzle screens.
    public partial class L2UI
    {
        // ================================================================== inventory
        GameObject invPanel;
        RectTransform invGrid;
        Image invIcon;
        TMP_Text invName, invNeeded, invDesc, invStatus, invEmpty, dropLabel;
        Button invPrimary, invSecondary;
        TMP_Text invPrimaryLabel, invSecondaryLabel;
        L2DropSlot dropSlot;
        string selected;
        L2Interactable useTarget;
        readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        void BuildInventory()
        {
            invPanel = Window("Inventory", "INVENTORY", new Vector2(1500f, 860f), out RectTransform c);

            Image gridBg = Img(c, "Grid Area", null, new Color(0f, 0f, 0f, 0.3f));
            Place(gridBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(700f, 500f));
            invGrid = NewRect("Grid", gridBg.transform);
            Stretch(invGrid, 18f, 18f, 18f, 18f);
            GridLayoutGroup gl = invGrid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(152f, 146f);
            gl.spacing = new Vector2(12f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 4;
            invEmpty = Txt(gridBg.transform, "Empty", "Your inventory is empty.\nPick things up with F.", 26f, Dim, TextAlignmentOptions.Center);
            Stretch(invEmpty.rectTransform);

            // drop target: use an item on what you were looking at
            Image drop = Img(c, "Use On", null, new Color(0.18f, 0.16f, 0.14f, 0.9f));
            Place(drop.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 46f), new Vector2(700f, 140f));
            dropLabel = Txt(drop.transform, "Label", "", 24f, Ink, TextAlignmentOptions.Center);
            Stretch(dropLabel.rectTransform, 20f, 10f, 20f, 10f);
            dropSlot = drop.gameObject.AddComponent<L2DropSlot>();
            dropSlot.highlight = drop;
            dropSlot.accepts = id => useTarget != null && useTarget.CanInteract && useTarget.AcceptsItem(id);
            dropSlot.onAccepted = id =>
            {
                L2Interactable t = useTarget;
                CloseModal();
                if (t != null) t.UseItem(id);
            };
            dropSlot.onRejected = id =>
            {
                if (G != null && G.sfx != null) G.sfx.Failure();
                invStatus.text = useTarget == null
                    ? "<color=#F05A52>Look at an object before opening the inventory to use an item on it.</color>"
                    : "<color=#F05A52>The " + L2Items.NameOf(id) + " can't be used on the " + useTarget.DisplayName + ".</color>";
            };

            TMP_Text hint = Txt(c, "Hint", "Click an item to inspect it  ·  Drag it onto the box above to use it  ·  V or Esc to close", 20f, Dim, TextAlignmentOptions.Left);
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1000f, 34f));

            // details
            Image det = Img(c, "Details", null, new Color(0f, 0f, 0f, 0.3f));
            Place(det.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(660f, 646f));
            Image iconBg = Img(det.transform, "Icon Frame", null, new Color(0.12f, 0.11f, 0.1f, 1f));
            Place(iconBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(220f, 220f));
            invIcon = Img(iconBg.transform, "Icon", null, Color.white);
            Stretch(invIcon.rectTransform, 10f, 10f, 10f, 10f);
            invIcon.preserveAspect = true;
            invName = Txt(det.transform, "Name", "", 36f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(invName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -256f), new Vector2(600f, 48f));
            invNeeded = Txt(det.transform, "Needed", "", 22f, new Color(0.75f, 0.85f, 0.95f), TextAlignmentOptions.Center);
            Place(invNeeded.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -306f), new Vector2(600f, 56f));
            invDesc = Txt(det.transform, "Description", "", 24f, Ink, TextAlignmentOptions.Top);
            Place(invDesc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -366f), new Vector2(600f, 120f));
            invStatus = Txt(det.transform, "Status", "", 21f, Dim, TextAlignmentOptions.Center);
            Place(invStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(620f, 56f));

            invPrimary = Btn(det.transform, "USE", OnInvPrimary, new Vector2(290f, 66f), out invPrimaryLabel);
            Place((RectTransform)invPrimary.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 20f), new Vector2(290f, 66f));
            invSecondary = Btn(det.transform, "PUT AWAY", OnInvSecondary, new Vector2(290f, 66f), out invSecondaryLabel);
            Place((RectTransform)invSecondary.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(8f, 20f), new Vector2(290f, 66f));
        }

        Sprite IconFor(string id)
        {
            if (iconCache.TryGetValue(id, out Sprite s)) return s;
            s = Resources.Load<Sprite>("L2Icons/" + id);
            iconCache[id] = s;
            return s;
        }

        public void OpenInventory()
        {
            if (G == null) return;
            L2Interactor it = G.playerCamera != null ? G.playerCamera.GetComponent<L2Interactor>() : null;
            useTarget = it != null ? it.Current : null;
            if (useTarget != null && !useTarget.CanInteract) useTarget = null;
            if (selected == null || !G.HasItem(selected)) selected = G.Inventory.Count > 0 ? G.Inventory[0] : null;
            if (G.Held != null) selected = G.Held;
            OpenModal(invPanel);
            RefreshInventory();
        }

        void RefreshInventory()
        {
            L2Game g = G;
            if (g == null) return;
            for (int i = invGrid.childCount - 1; i >= 0; i--) Destroy(invGrid.GetChild(i).gameObject);
            invEmpty.gameObject.SetActive(g.Inventory.Count == 0);
            if (selected != null && !g.HasItem(selected)) selected = g.Inventory.Count > 0 ? g.Inventory[0] : null;

            foreach (string id in g.Inventory)
            {
                string itemId = id;
                bool isSel = id == selected;
                Image slot = Img(invGrid, id, buttonSprite, isSel ? new Color(1f, 0.9f, 0.6f) : new Color(0.55f, 0.52f, 0.5f));
                Button b = slot.gameObject.AddComponent<Button>();
                b.targetGraphic = slot;
                b.onClick.AddListener(() => { selected = itemId; if (G.sfx != null) G.sfx.Click(); RefreshInventory(); });
                Image ic = Img(slot.transform, "Icon", IconFor(id), Color.white);
                Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(100f, 100f));
                ic.preserveAspect = true;
                ic.raycastTarget = false;
                if (ic.sprite == null) ic.color = new Color(1f, 1f, 1f, 0.15f);
                TMP_Text n = Txt(slot.transform, "Name", L2Items.NameOf(id), 15f, Ink, TextAlignmentOptions.Center, headingFont);
                Place(n.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(144f, 34f));
                n.enableAutoSizing = true;
                n.fontSizeMin = 11f;
                n.fontSizeMax = 15f;
                if (g.Held == id)
                {
                    TMP_Text tag = Txt(slot.transform, "Held", L2Items.IsLight(g.Held) ? "EQUIPPED" : "IN HAND", 13f, Good, TextAlignmentOptions.TopRight, headingFont);
                    Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -6f), new Vector2(100f, 18f));
                }
                L2Draggable d = slot.gameObject.AddComponent<L2Draggable>();
                d.payload = id;
                d.dragLayer = root;
            }

            dropLabel.text = useTarget != null
                ? "USE ON:  <color=#E8C547>" + useTarget.DisplayName.ToUpperInvariant() + "</color>\n<size=80%><color=#A39C94>Drag an item here</color></size>"
                : "<color=#A39C94>To use an item on something, look at it first, then open the inventory\nand drag the item here (or press Use and then E on the object).</color>";

            ShowDetails(selected);
        }

        void ShowDetails(string id)
        {
            L2Game g = G;
            L2ItemDef d = L2Items.Get(id);
            bool has = d != null;
            invIcon.gameObject.SetActive(has);
            invPrimary.gameObject.SetActive(has);
            invSecondary.gameObject.SetActive(false);
            if (!has)
            {
                invName.text = "";
                invNeeded.text = "";
                invDesc.text = "";
                invStatus.text = "";
                return;
            }
            invIcon.sprite = IconFor(id);
            invIcon.color = invIcon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            invName.text = d.name;
            invNeeded.text = string.IsNullOrEmpty(d.neededFor) ? "" : "Needed for: " + d.neededFor;
            invDesc.text = d.description;
            invStatus.text = "";

            invPrimary.interactable = true;
            if (L2Items.IsPhotoPiece(id))
            {
                int have = 0;
                foreach (string p in L2Items.PhotoPieces) if (g.HasItem(p)) have++;
                invPrimaryLabel.text = "ASSEMBLE PHOTO";
                invStatus.text = have < 4 ? "Pieces found: " + have + " / 4" : "All four pieces found.";
            }
            else if (id == L2Items.FamilyPhoto)
            {
                invPrimaryLabel.text = "VIEW PHOTO";
            }
            else if (d.hold == L2HoldKind.Flashlight)
            {
                bool eq = g.Held == id;
                invPrimaryLabel.text = eq ? "UNEQUIP" : "EQUIP";
                if (eq)
                {
                    invSecondary.gameObject.SetActive(true);
                    invSecondaryLabel.text = g.FlashlightOn ? "TURN OFF (G)" : "TURN ON (G)";
                    invStatus.text = "Equipped  ·  light " + (g.FlashlightOn ? "<color=#B48CFF>ON</color>" : "OFF");
                }
            }
            else
            {
                bool inHand = g.Held == id;
                invPrimaryLabel.text = inHand ? "PUT AWAY" : "USE";
                invStatus.text = inHand ? "In your hand. Look at where it goes and press E." : "Use puts it in your hand.";
            }
        }

        void OnInvPrimary()
        {
            L2Game g = G;
            string id = selected;
            L2ItemDef d = L2Items.Get(id);
            if (g == null || d == null) return;

            if (L2Items.IsPhotoPiece(id))
            {
                int have = 0;
                foreach (string p in L2Items.PhotoPieces) if (g.HasItem(p)) have++;
                if (have < 4)
                {
                    if (g.sfx != null) g.sfx.Failure();
                    invStatus.text = "<color=#F05A52>You have " + have + " of 4 pieces. Find the rest first.</color>";
                    return;
                }
                CloseModal();
                OpenPhotoAssembly();
                return;
            }
            if (id == L2Items.FamilyPhoto)
            {
                CloseModal();
                ShowDocument("Family Photo", familyPhoto,
                    "The family on the sofa. The clock on the wall says <b>8:00</b> and the TV glows <b>blue</b> on <b>Channel 3</b>.");
                return;
            }
            if (g.Held == id) { g.Unequip(); RefreshInventory(); return; }

            g.Equip(id);
            if (d.hold == L2HoldKind.Hold) CloseModal();
            else RefreshInventory();
        }

        void OnInvSecondary()
        {
            if (G == null) return;
            if (L2Items.IsLight(G.Held)) G.SetFlashlight(!G.FlashlightOn, true);
            RefreshInventory();
        }

        // ================================================================== photo assembly
        GameObject photoPanel;
        RectTransform photoBoard, photoTray;
        TMP_Text photoStatus;
        readonly RawImage[] photoTiles = new RawImage[4];
        readonly Vector2 tileSize = new Vector2(300f, 225f);

        void BuildPhoto()
        {
            photoPanel = Window("Photo", "ASSEMBLE THE PHOTO", new Vector2(1500f, 860f), out RectTransform c);
            photoBoard = NewRect("Board", c);
            Place(photoBoard, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(tileSize.x * 2f + 8f, tileSize.y * 2f + 8f));
            Image bb = photoBoard.gameObject.AddComponent<Image>();
            bb.color = new Color(0.16f, 0.15f, 0.14f, 1f);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Image slot = Img(photoBoard, "Slot " + (i + 1), null, new Color(0.24f, 0.22f, 0.2f, 1f));
                Place(slot.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(4f + (i % 2) * tileSize.x, -4f - (i / 2) * tileSize.y), tileSize - new Vector2(4f, 4f));
                L2DropSlot ds = slot.gameObject.AddComponent<L2DropSlot>();
                ds.highlight = slot;
                ds.accepts = id => id == "piece_" + index;
                ds.onAccepted = id => PlacePiece(index, slot.rectTransform);
                ds.onRejected = id =>
                {
                    if (G != null && G.sfx != null) G.sfx.Failure();
                    photoStatus.text = "<color=#F05A52>That piece doesn't fit there.</color>";
                };
            }
            photoTray = NewRect("Tray", c);
            Place(photoTray, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -20f), new Vector2(620f, 470f));
            photoStatus = Txt(c, "Status", "", 26f, Ink, TextAlignmentOptions.Center);
            Place(photoStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1300f, 90f));
            Button close = Btn(c, "CLOSE", CloseModal, new Vector2(260f, 60f));
            Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(260f, 60f));
        }

        void OpenPhotoAssembly()
        {
            for (int i = photoTray.childCount - 1; i >= 0; i--) Destroy(photoTray.GetChild(i).gameObject);
            for (int i = 0; i < 4; i++)
            {
                if (photoTiles[i] != null) Destroy(photoTiles[i].gameObject);
                photoTiles[i] = null;
            }
            int[] order = { 2, 0, 3, 1 };   // shuffled
            for (int k = 0; k < 4; k++)
            {
                int i = order[k];
                RawImage r = NewRect("Piece " + (i + 1), photoTray).gameObject.AddComponent<RawImage>();
                r.texture = i < photoPieces.Length ? photoPieces[i] : null;
                Place(r.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((k % 2) * 310f, -(k / 2) * 240f), new Vector2(290f, 218f));
                L2Draggable d = r.gameObject.AddComponent<L2Draggable>();
                d.payload = "piece_" + i;
                d.dragLayer = root;
            }
            photoStatus.text = "Drag the four torn pieces into place.";
            OpenModal(photoPanel);
        }

        void PlacePiece(int index, RectTransform slot)
        {
            Transform src = photoTray.Find("Piece " + (index + 1));
            if (src == null) return;
            Destroy(src.gameObject);
            RawImage tile = NewRect("Tile " + (index + 1), slot).gameObject.AddComponent<RawImage>();
            tile.texture = index < photoPieces.Length ? photoPieces[index] : null;
            Stretch(tile.rectTransform);
            tile.raycastTarget = false;
            photoTiles[index] = tile;
            if (G != null && G.sfx != null) G.sfx.Click();

            int placed = 0;
            foreach (RawImage t in photoTiles) if (t != null) placed++;
            photoStatus.text = placed < 4 ? "Pieces in place: " + placed + " / 4" :
                "<color=#5FD383>The photo is whole again!</color> The wall clock shows <b>8:00</b> and the TV glows <b>blue</b> on <b>Channel 3</b>.";
            if (placed == 4 && G != null) G.AssemblePhoto();
        }

        // ================================================================== wall clock
        GameObject clockPanel;
        RectTransform clockHourHand, clockMinuteHand;
        TMP_Text clockDigital, clockStatus;
        L2WallClock clockTarget;
        int clockH, clockM;

        void BuildClock()
        {
            clockPanel = Window("Clock", "SET THE CLOCK", new Vector2(980f, 820f), out RectTransform c);
            Image face = Img(c, "Face", circleSprite, new Color(0.93f, 0.9f, 0.84f));
            Place(face.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200f, -10f), new Vector2(360f, 360f));
            for (int i = 0; i < 12; i++)
            {
                Image tick = Img(face.transform, "Tick", null, new Color(0.25f, 0.2f, 0.17f));
                RectTransform tr = tick.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.pivot = new Vector2(0.5f, -6.2f);
                tr.sizeDelta = new Vector2(i % 3 == 0 ? 8f : 4f, 24f);
                tr.anchoredPosition = Vector2.zero;
                tr.localRotation = Quaternion.Euler(0f, 0f, -30f * i);
                if (i % 3 == 0)
                {
                    TMP_Text num = Txt(face.transform, "Num", (i == 0 ? 12 : i).ToString(), 30f, new Color(0.25f, 0.2f, 0.17f), TextAlignmentOptions.Center, headingFont);
                    float a = i * 30f * Mathf.Deg2Rad;
                    Place(num.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 112f, new Vector2(60f, 40f));
                }
            }
            clockHourHand = Img(face.transform, "Hour Hand", null, new Color(0.15f, 0.12f, 0.1f)).rectTransform;
            clockHourHand.anchorMin = clockHourHand.anchorMax = new Vector2(0.5f, 0.5f);
            clockHourHand.pivot = new Vector2(0.5f, 0.08f);
            clockHourHand.sizeDelta = new Vector2(12f, 100f);
            clockMinuteHand = Img(face.transform, "Minute Hand", null, new Color(0.15f, 0.12f, 0.1f)).rectTransform;
            clockMinuteHand.anchorMin = clockMinuteHand.anchorMax = new Vector2(0.5f, 0.5f);
            clockMinuteHand.pivot = new Vector2(0.5f, 0.06f);
            clockMinuteHand.sizeDelta = new Vector2(7f, 150f);
            Image pin = Img(face.transform, "Pin", circleSprite, new Color(0.7f, 0.15f, 0.12f));
            Place(pin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            clockDigital = Txt(c, "Digital", "", 64f, Gold, TextAlignmentOptions.Center, headingFont);
            Place(clockDigital.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(230f, -30f), new Vector2(300f, 80f));

            Vector2 s = new Vector2(190f, 62f);
            TMP_Text hl = Txt(c, "Hour Label", "HOUR HAND", 22f, Dim, TextAlignmentOptions.Center, headingFont);
            Place(hl.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(230f, -130f), new Vector2(400f, 30f));
            Button hm = Btn(c, "HOUR -", () => StepClock(-1, 0), s);
            Place((RectTransform)hm.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(130f, -166f), s);
            Button hp = Btn(c, "HOUR +", () => StepClock(1, 0), s);
            Place((RectTransform)hp.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(330f, -166f), s);
            TMP_Text ml = Txt(c, "Minute Label", "MINUTE HAND", 22f, Dim, TextAlignmentOptions.Center, headingFont);
            Place(ml.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(230f, -246f), new Vector2(400f, 30f));
            Button mm = Btn(c, "MINUTE -", () => StepClock(0, -5), s);
            Place((RectTransform)mm.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(130f, -282f), s);
            Button mp = Btn(c, "MINUTE +", () => StepClock(0, 5), s);
            Place((RectTransform)mp.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(330f, -282f), s);

            clockStatus = Txt(c, "Status", "", 26f, Ink, TextAlignmentOptions.Center);
            Place(clockStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(860f, 70f));
            Button confirm = Btn(c, "CONFIRM", ConfirmClock, new Vector2(300f, 70f));
            Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(300f, 70f));
        }

        public void OpenClock(L2WallClock clock)
        {
            clockTarget = clock;
            clockH = clock.Hour <= 0 ? 12 : clock.Hour;
            clockM = clock.Minute;
            clockStatus.text = clock.IsCorrect ? "Currently set to " + clock.Hour + ":" + clock.Minute.ToString("00") + "." :
                "Move the hands, then press Confirm. Nothing changes until you confirm.";
            RefreshClock();
            OpenModal(clockPanel);
        }

        void StepClock(int h, int m)
        {
            clockH = ((clockH - 1 + h) % 12 + 12) % 12 + 1;
            clockM = ((clockM + m) % 60 + 60) % 60;
            clockStatus.text = "Not confirmed yet.";
            RefreshClock();
        }

        void RefreshClock()
        {
            clockDigital.text = clockH + ":" + clockM.ToString("00");
            clockMinuteHand.localRotation = Quaternion.Euler(0f, 0f, -clockM * 6f);
            clockHourHand.localRotation = Quaternion.Euler(0f, 0f, -((clockH % 12) * 30f + clockM * 0.5f));
        }

        void ConfirmClock()
        {
            if (clockTarget == null) return;
            clockTarget.Confirm(clockH, clockM);
            clockStatus.text = "<color=#5FD383>Confirmed.</color> The wall clock now reads " + clockH + ":" + clockM.ToString("00") + ".";
        }

        // ================================================================== TV remote
        GameObject remotePanel;
        TMP_Text remoteScreen;
        L2Remote remoteTarget;

        void BuildRemote()
        {
            remotePanel = Window("Remote", "TV REMOTE", new Vector2(620f, 900f), out RectTransform c);
            Image scr = Img(c, "Screen", null, new Color(0.05f, 0.08f, 0.07f));
            Place(scr.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(460f, 90f));
            remoteScreen = Txt(scr.transform, "Text", "", 32f, new Color(0.55f, 0.95f, 0.7f), TextAlignmentOptions.Center, headingFont);
            Stretch(remoteScreen.rectTransform, 10f, 4f, 10f, 4f);

            Button power = Btn(c, "POWER", () => RemotePower(), new Vector2(200f, 64f));
            Place((RectTransform)power.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(200f, 64f));
            ((Image)power.targetGraphic).color = new Color(1f, 0.6f, 0.55f);

            for (int n = 1; n <= 9; n++)
            {
                int ch = n;
                Button b = Btn(c, n.ToString(), () => RemoteChannel(ch), new Vector2(120f, 84f), 36f);
                int col = (n - 1) % 3, row = (n - 1) / 3;
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((col - 1) * 140f, -200f - row * 100f), new Vector2(120f, 84f));
            }
            Button down = Btn(c, "CH -", () => { if (remoteTarget != null && remoteTarget.tv != null) RemoteChannel(remoteTarget.tv.Channel <= 1 ? 9 : remoteTarget.tv.Channel - 1); }, new Vector2(190f, 70f));
            Place((RectTransform)down.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-105f, -510f), new Vector2(190f, 70f));
            Button up = Btn(c, "CH +", () => { if (remoteTarget != null && remoteTarget.tv != null) RemoteChannel(remoteTarget.tv.Channel >= 9 ? 1 : remoteTarget.tv.Channel + 1); }, new Vector2(190f, 70f));
            Place((RectTransform)up.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(105f, -510f), new Vector2(190f, 70f));
            TMP_Text hint = Txt(c, "Hint", "Press a number to change the channel.", 22f, Dim, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(520f, 40f));
        }

        public void OpenRemote(L2Remote remote)
        {
            remoteTarget = remote;
            RefreshRemote();
            OpenModal(remotePanel);
        }

        void RemotePower()
        {
            if (remoteTarget == null || remoteTarget.tv == null) return;
            remoteTarget.tv.SetPower(!remoteTarget.tv.On);
            if (G != null && G.sfx != null) G.sfx.Beep();
            RefreshRemote();
        }

        void RemoteChannel(int ch)
        {
            if (remoteTarget == null || remoteTarget.tv == null) return;
            if (!remoteTarget.tv.On) remoteTarget.tv.SetPower(true);
            remoteTarget.tv.SetChannel(ch);
            if (G != null && G.sfx != null) G.sfx.Beep();
            RefreshRemote();
        }

        void RefreshRemote()
        {
            L2TV tv = remoteTarget != null ? remoteTarget.tv : null;
            remoteScreen.text = tv == null ? "NO TV" : tv.On ? "CH " + tv.Channel + "  ·  " + L2TV.ChannelName(tv.Channel).ToUpperInvariant() : "TV OFF";
        }

        // ================================================================== directional lock (moon box)
        GameObject lockPanel;
        readonly TMP_Text[] lockBoxes = new TMP_Text[4];
        readonly Image[] lockBoxBg = new Image[4];
        TMP_Text lockStatus;
        readonly List<string> lockInput = new List<string>();
        L2MoonBox lockTarget;
        float lockFlash;
        Color lockFlashColor;
        bool lockBusy;

        void BuildLock()
        {
            lockPanel = Window("Direction Lock", "MOON BOX LOCK", new Vector2(900f, 860f), out RectTransform c);
            TMP_Text info = Txt(c, "Info", "A brass lock with four direction buttons. Enter a sequence of four moves.", 24f, Dim, TextAlignmentOptions.Center);
            Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(800f, 40f));
            for (int i = 0; i < 4; i++)
            {
                Image bg = Img(c, "Box " + (i + 1), null, new Color(0.16f, 0.15f, 0.14f));
                Place(bg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 170f, -60f), new Vector2(156f, 72f));
                lockBoxBg[i] = bg;
                lockBoxes[i] = Txt(bg.transform, "Text", "", 30f, Ink, TextAlignmentOptions.Center, headingFont);
                Stretch(lockBoxes[i].rectTransform);
            }
            Vector2 s = new Vector2(200f, 86f);
            AddArrow(c, "UP", "↑  UP", new Vector2(0f, -170f), s);
            AddArrow(c, "LEFT", "←  LEFT", new Vector2(-220f, -270f), s);
            AddArrow(c, "RIGHT", "RIGHT  →", new Vector2(220f, -270f), s);
            AddArrow(c, "DOWN", "↓  DOWN", new Vector2(0f, -370f), s);
            Button clear = Btn(c, "CLEAR", ClearLock, new Vector2(170f, 60f));
            Place((RectTransform)clear.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -282f), new Vector2(170f, 60f));
            lockStatus = Txt(c, "Status", "", 28f, Ink, TextAlignmentOptions.Center);
            Place(lockStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(800f, 70f));
        }

        void AddArrow(RectTransform parent, string dir, string label, Vector2 pos, Vector2 size)
        {
            Button b = Btn(parent, label, null, size, 32f);
            b.name = dir;
            Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, size);
            b.onClick.AddListener(() => PressLock(dir, (RectTransform)b.transform));
        }

        public void OpenDirectionLock(L2MoonBox box)
        {
            lockTarget = box;
            ClearLock();
            lockStatus.text = "Enter the sequence.";
            OpenModal(lockPanel);
        }

        void ClearLock()
        {
            if (lockBusy) return;
            lockInput.Clear();
            for (int i = 0; i < 4; i++) { lockBoxes[i].text = "-"; lockBoxBg[i].color = new Color(0.16f, 0.15f, 0.14f); }
        }

        void PressLock(string dir, RectTransform button)
        {
            if (lockBusy || lockTarget == null) return;
            StartCoroutine(Punch(button));
            if (G != null && G.sfx != null) G.sfx.Beep();
            int i = lockInput.Count;
            if (i >= 4) return;
            lockInput.Add(dir);
            lockBoxes[i].text = dir;
            lockBoxBg[i].color = new Color(0.32f, 0.29f, 0.2f);
            lockStatus.text = "Moves entered: " + lockInput.Count + " / 4";
            if (lockInput.Count == 4) StartCoroutine(CheckLock());
        }

        IEnumerator CheckLock()
        {
            lockBusy = true;
            yield return new WaitForSecondsRealtime(0.25f);
            bool ok = true;
            for (int i = 0; i < 4; i++) if (lockInput[i] != L2MoonBox.Code[i]) ok = false;
            lockFlash = 0.6f;
            lockFlashColor = ok ? Good : Bad;
            if (ok)
            {
                lockStatus.text = "<color=#5FD383>Click! The lock turns.</color>";
                yield return new WaitForSecondsRealtime(0.9f);
                lockBusy = false;
                L2MoonBox box = lockTarget;
                CloseModal();
                if (box != null) box.Solve();
            }
            else
            {
                if (G != null && G.sfx != null) G.sfx.Failure();
                lockStatus.text = "<color=#F05A52>The lock doesn't budge. Try again.</color>";
                yield return new WaitForSecondsRealtime(0.9f);
                lockBusy = false;
                ClearLock();
            }
        }

        void UpdateLockFlash()
        {
            lockFlash -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(lockFlash / 0.6f);
            for (int i = 0; i < 4; i++)
                lockBoxBg[i].color = Color.Lerp(new Color(0.32f, 0.29f, 0.2f), lockFlashColor, k);
        }

        IEnumerator Punch(RectTransform t)
        {
            for (float x = 0f; x < 0.12f; x += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.one * (1f - 0.08f * Mathf.Sin(x / 0.12f * Mathf.PI));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // ================================================================== jewellery box drawers
        GameObject drawerPanel;
        readonly TMP_Text[] drawerLabels = new TMP_Text[5];
        TMP_Text drawerStatus;
        L2JewelleryBox drawerTarget;

        void BuildDrawers()
        {
            drawerPanel = Window("Drawers", "JEWELLERY BOX", new Vector2(760f, 860f), out RectTransform c);
            TMP_Text info = Txt(c, "Info", "Five small drawers, numbered from the bottom (1) to the top (5).", 23f, Dim, TextAlignmentOptions.Center);
            Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(660f, 60f));
            for (int n = 5; n >= 1; n--)
            {
                int number = n;
                Button b = Btn(c, "DRAWER " + n, () => ToggleDrawer(number), new Vector2(480f, 76f), out TMP_Text label);
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f - (5 - n) * 92f), new Vector2(480f, 76f));
                drawerLabels[n - 1] = label;
            }
            drawerStatus = Txt(c, "Status", "", 26f, Ink, TextAlignmentOptions.Center);
            Place(drawerStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(660f, 80f));
        }

        public void OpenDrawers(L2JewelleryBox box)
        {
            drawerTarget = box;
            drawerStatus.text = "Choose a drawer.";
            RefreshDrawers();
            OpenModal(drawerPanel);
        }

        void ToggleDrawer(int n)
        {
            if (drawerTarget == null) return;
            string msg = drawerTarget.Toggle(n);
            bool key = n == drawerTarget.keyDrawer && drawerTarget.IsDrawerOpen(n) && G != null && !G.IsCollected(L2Items.BedroomKey);
            drawerStatus.text = key ? "<color=#5FD383>" + msg + "</color>\nClose this window and pick it up (F)." : msg;
            RefreshDrawers();
        }

        void RefreshDrawers()
        {
            for (int n = 1; n <= 5; n++)
                drawerLabels[n - 1].text = "DRAWER " + n + (drawerTarget != null && drawerTarget.IsDrawerOpen(n) ? "  ·  OPEN" : "");
        }

        // ================================================================== oven keypad
        GameObject keypadPanel;
        TMP_Text keypadDisplay, keypadStatus;
        string keypadEntry = "";
        L2Oven keypadTarget;
        bool keypadBusy;

        void BuildKeypad()
        {
            keypadPanel = Window("Keypad", "OVEN TIMER", new Vector2(620f, 920f), out RectTransform c);
            Image scr = Img(c, "Display", null, new Color(0.03f, 0.03f, 0.03f));
            Place(scr.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(420f, 110f));
            keypadDisplay = Txt(scr.transform, "Text", "", 72f, new Color(0.3f, 1f, 0.45f), TextAlignmentOptions.Center, headingFont);
            Stretch(keypadDisplay.rectTransform);
            keypadDisplay.characterSpacing = 10f;
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "OK" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                Button b = Btn(c, k, () => PressKey(k), new Vector2(130f, 90f), 36f);
                int col = i % 3, row = i / 3;
                Place((RectTransform)b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((col - 1) * 150f, -140f - row * 106f), new Vector2(130f, 90f));
            }
            keypadStatus = Txt(c, "Status", "", 24f, Ink, TextAlignmentOptions.Center);
            Place(keypadStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(520f, 70f));
        }

        public void OpenKeypad(L2Oven oven)
        {
            keypadTarget = oven;
            keypadEntry = "";
            keypadBusy = false;
            keypadStatus.text = "Enter a four-digit code, then OK.";
            RefreshKeypad();
            OpenModal(keypadPanel);
        }

        void RefreshKeypad()
        {
            string d = keypadEntry.PadRight(4, '_');
            keypadDisplay.color = new Color(0.3f, 1f, 0.45f);
            keypadDisplay.text = d.Substring(0, 2) + ":" + d.Substring(2, 2);
        }

        void PressKey(string k)
        {
            if (keypadBusy || keypadTarget == null) return;
            if (G != null && G.sfx != null) G.sfx.Beep();
            if (k == "CLR") { keypadEntry = ""; keypadStatus.text = "Cleared."; RefreshKeypad(); return; }
            if (k == "OK")
            {
                if (keypadEntry.Length < 4) { keypadStatus.text = "The code has four digits."; return; }
                StartCoroutine(SubmitKeypad());
                return;
            }
            if (keypadEntry.Length < 4) keypadEntry += k;
            RefreshKeypad();
        }

        IEnumerator SubmitKeypad()
        {
            keypadBusy = true;
            bool ok = keypadTarget.Submit(keypadEntry);
            if (ok)
            {
                keypadDisplay.text = "OPEN";
                keypadStatus.text = "<color=#5FD383>Correct!</color>";
                yield return new WaitForSecondsRealtime(1f);
                keypadBusy = false;
                CloseModal();
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    keypadDisplay.color = Bad;
                    keypadDisplay.text = "ERR";
                    yield return new WaitForSecondsRealtime(0.18f);
                    keypadDisplay.text = "";
                    yield return new WaitForSecondsRealtime(0.1f);
                }
                keypadStatus.text = "<color=#F05A52>Wrong code. Nothing happens.</color>";
                keypadEntry = "";
                RefreshKeypad();
                keypadBusy = false;
            }
        }

        // ================================================================== workstation password
        GameObject passwordPanel;
        TMP_InputField passwordInput;
        TMP_Text passwordStatus, passwordTitle, passwordUser;
        Image loginBar;
        GameObject loginBarRoot, passwordForm;
        L2Workstation passwordTarget;
        bool loggingIn;

        void BuildPassword()
        {
            passwordPanel = Window("Workstation", "", new Vector2(1100f, 700f), out RectTransform c);
            Image screen = Img(c, "Screen", null, new Color(0.04f, 0.08f, 0.16f, 1f));
            Stretch(screen.rectTransform, 0f, 0f, 0f, 0f);
            passwordTitle = Txt(screen.transform, "Title", "WORKSTATION LOCKED", 52f, new Color(0.75f, 0.85f, 1f), TextAlignmentOptions.Center, headingFont);
            Place(passwordTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(900f, 70f));
            passwordTitle.characterSpacing = 6f;

            passwordForm = NewRect("Form", screen.transform).gameObject;
            Stretch((RectTransform)passwordForm.transform);
            TMP_Text user = Txt(passwordForm.transform, "User", "Enter password to unlock", 26f, new Color(0.62f, 0.7f, 0.82f), TextAlignmentOptions.Center);
            Place(user.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(960f, 170f));
            user.enableAutoSizing = false;
            user.fontSize = 25f;
            passwordUser = user;

            Image field = Img(passwordForm.transform, "Input", null, new Color(0.92f, 0.94f, 0.98f, 1f));
            Place(field.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -285f), new Vector2(560f, 76f));
            RectTransform area = NewRect("Text Area", field.transform);
            Stretch(area, 18f, 6f, 18f, 6f);
            area.gameObject.AddComponent<RectMask2D>();
            TMP_Text ph = Txt(area, "Placeholder", "Password", 32f, new Color(0.45f, 0.48f, 0.55f), TextAlignmentOptions.MidlineLeft);
            Stretch(ph.rectTransform);
            ph.fontStyle = FontStyles.Italic;
            TMP_Text tx = Txt(area, "Text", "", 34f, new Color(0.08f, 0.1f, 0.15f), TextAlignmentOptions.MidlineLeft);
            Stretch(tx.rectTransform);
            tx.richText = false;
            tx.textWrappingMode = TextWrappingModes.NoWrap;
            passwordInput = field.gameObject.AddComponent<TMP_InputField>();
            passwordInput.textViewport = area;
            passwordInput.textComponent = tx;
            passwordInput.placeholder = ph;
            passwordInput.targetGraphic = field;
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.characterLimit = 24;
            passwordInput.customCaretColor = true;
            passwordInput.caretColor = new Color(0.1f, 0.12f, 0.2f);
            passwordInput.onSubmit.AddListener(_ => SubmitPassword());

            Button login = Btn(passwordForm.transform, "LOG IN", SubmitPassword, new Vector2(260f, 66f));
            Place((RectTransform)login.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -378f), new Vector2(260f, 66f));

            passwordStatus = Txt(screen.transform, "Status", "", 28f, Ink, TextAlignmentOptions.Center);
            Place(passwordStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(900f, 60f));

            Image barBg = Img(screen.transform, "Login Bar", null, new Color(1f, 1f, 1f, 0.12f));
            Place(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(600f, 18f));
            loginBar = Img(barBg.transform, "Fill", null, new Color(0.45f, 0.75f, 1f));
            loginBar.rectTransform.anchorMin = Vector2.zero;
            loginBar.rectTransform.anchorMax = new Vector2(0f, 1f);
            loginBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            loginBar.rectTransform.offsetMin = Vector2.zero;
            loginBar.rectTransform.offsetMax = Vector2.zero;
            loginBarRoot = barBg.gameObject;
        }

        public void OpenPassword(L2Workstation ws)
        {
            passwordTarget = ws;
            loggingIn = false;
            passwordTitle.text = "WORKSTATION LOCKED";
            passwordUser.alignment = TextAlignmentOptions.Top;
            passwordUser.text = "<color=#8FD3FF>" + L2Workstation.TerminalCode + "</color>";
            passwordForm.SetActive(true);
            loginBarRoot.SetActive(false);
            passwordStatus.text = "";
            passwordInput.text = "";
            OpenModal(passwordPanel);
            StartCoroutine(FocusPassword());
        }

        IEnumerator FocusPassword()
        {
            yield return null;
            if (passwordInput != null && passwordPanel.activeSelf)
            {
                passwordInput.Select();
                passwordInput.ActivateInputField();
            }
        }

        void SubmitPassword()
        {
            if (loggingIn || passwordTarget == null || !passwordPanel.activeSelf) return;
            string entry = passwordInput.text;
            if (string.IsNullOrEmpty(entry)) { StartCoroutine(FocusPassword()); return; }
            if (passwordTarget.Check(entry))
            {
                StartCoroutine(LoginSuccess());
                return;
            }
            if (G != null && G.sfx != null) G.sfx.Failure();
            passwordStatus.text = "<color=#F05A52>Incorrect password. Try again.</color>";
            passwordInput.text = "";
            StartCoroutine(FocusPassword());
        }

        IEnumerator LoginSuccess()
        {
            loggingIn = true;
            passwordForm.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            passwordTitle.text = "WELCOME BACK";
            passwordStatus.text = "<color=#8FD3FF>Logging in...</color>";
            loginBarRoot.SetActive(true);
            if (G != null && G.sfx != null) G.sfx.Success();
            for (float t = 0f; t < 1.8f; t += Time.unscaledDeltaTime)
            {
                loginBar.rectTransform.anchorMax = new Vector2(Mathf.SmoothStep(0f, 1f, t / 1.8f), 1f);
                yield return null;
            }
            loginBar.rectTransform.anchorMax = Vector2.one;
            passwordStatus.text = "<color=#5FD383>Workstation unlocked.</color>";
            yield return new WaitForSecondsRealtime(0.8f);
            L2Workstation ws = passwordTarget;
            CloseModal();
            loggingIn = false;
            if (ws != null) ws.UnlockAndFinish();
        }
    }
}
