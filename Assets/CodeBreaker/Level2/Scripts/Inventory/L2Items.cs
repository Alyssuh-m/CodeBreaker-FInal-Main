using System.Collections.Generic;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // How an item behaves when the player presses Use / Equip in the inventory.
    public enum L2HoldKind
    {
        None,        // not held (e.g. photo pieces: "Use" opens the photo assembly)
        Hold,        // shown in the player's hand; look at a compatible object and press E
        Flashlight,  // shown in the player's hand; G turns it on / off
    }

    public class L2ItemDef
    {
        public string id;
        public string name;
        public string description;
        public string neededFor;               // shown as "Needed for: ..." in the inventory
        public L2HoldKind hold;
        public Vector3 viewEuler;              // orientation of the held model in first-person view
        public float viewSize = 0.55f;         // longest side of the held model (Unity units)
        public string back;                    // text written on the back (shown on pick-up and in the Notes journal)
    }

    // Every Level 2 inventory item, in one place.
    public static class L2Items
    {
        public const string Photo1 = "photo_1", Photo2 = "photo_2", Photo3 = "photo_3", Photo4 = "photo_4";
        public const string FamilyPhoto = "family_photo";
        public const string Batteries = "batteries";
        public const string StarFigurine = "star_figurine";
        public const string LivingKey = "living_key";
        public const string UVFlashlight = "uv_flashlight";
        public const string BedroomKey = "bedroom_key";
        public const string KitchenKey = "kitchen_key";

        public static readonly string[] PhotoPieces = { Photo1, Photo2, Photo3, Photo4 };

        static Dictionary<string, L2ItemDef> all;

        public static L2ItemDef Get(string id)
        {
            Build();
            return id != null && all.TryGetValue(id, out L2ItemDef d) ? d : null;
        }

        public static string NameOf(string id)
        {
            L2ItemDef d = Get(id);
            return d != null ? d.name : id;
        }

        public static bool IsPhotoPiece(string id) => System.Array.IndexOf(PhotoPieces, id) >= 0;

        // Any item that G switches on / off while it is equipped (UV flashlight, Level 3 flashlight).
        public static bool IsLight(string id)
        {
            L2ItemDef d = Get(id);
            return d != null && d.hold == L2HoldKind.Flashlight;
        }

        public const string Flashlight = "flashlight";

        static void Add(string id, string name, string desc, string neededFor, L2HoldKind hold, Vector3 viewEuler, float viewSize = 0.55f)
        {
            all[id] = new L2ItemDef { id = id, name = name, description = desc, neededFor = neededFor, hold = hold, viewEuler = viewEuler, viewSize = viewSize };
        }

        static void Build()
        {
            if (all != null) return;
            all = new Dictionary<string, L2ItemDef>();

            const string photoUse = "The Family Evening (collect all four pieces, then press Assemble)";
            Add(Photo1, "Torn Photo Piece (1/4)", "A torn corner of a family photograph. Found on top of the TV cabinet. On the back: clock = 1000", photoUse, L2HoldKind.None, Vector3.zero);
            all[Photo1].back = "clock = 1000   (binary)";
            Add(Photo2, "Torn Photo Piece (2/4)", "A torn piece of a family photograph. Found under the north sofa cushion. On the back: channel = 0011", photoUse, L2HoldKind.None, Vector3.zero);
            all[Photo2].back = "channel = 0011   (binary)";
            Add(Photo3, "Torn Photo Piece (3/4)", "A torn piece of a family photograph, rolled up inside the vase on the sideboard.", photoUse, L2HoldKind.None, Vector3.zero);
            Add(Photo4, "Torn Photo Piece (4/4)", "A torn corner of a family photograph. It was hidden behind the photo frame on the sideboard.", photoUse, L2HoldKind.None, Vector3.zero);
            Add(FamilyPhoto, "Family Photo",
                "The restored photograph: the family on the sofa, the wall clock showing 8:00 and the TV glowing blue on Channel 3.",
                "Clue: make the Living Room match the photo", L2HoldKind.None, Vector3.zero);
            Add(Batteries, "AA Batteries", "Two fresh AA batteries from the bottom shelf of the bookshelf.",
                "The TV remote on the coffee table", L2HoldKind.Hold, new Vector3(0f, 30f, 0f), 0.2f);
            Add(StarFigurine, "Star Figurine", "A small brass star on a plinth. Its base looks like it would fit a slot.",
                "The star-slot box on the bookshelf", L2HoldKind.Hold, new Vector3(0f, -20f, 0f), 0.26f);
            Add(LivingKey, "Living Room Key", "A brass key released from the blue book.",
                "Door: Living Room exit", L2HoldKind.Hold, new Vector3(0f, 0f, 60f), 0.24f);
            Add(UVFlashlight, "UV Flashlight",
                "An ultraviolet torch with an endless battery. Writing in invisible ink glows under its beam. Press G to switch it on or off while it is equipped.",
                "Revealing hidden writing", L2HoldKind.Flashlight, new Vector3(-6f, 180f, 0f), 0.42f);
            Add(BedroomKey, "Bedroom Key", "A small key found in drawer 5 of the jewellery box.",
                "Door: Bedroom exit", L2HoldKind.Hold, new Vector3(0f, 90f, 30f), 0.24f);
            Add(KitchenKey, "Kitchen Key", "A brass key that was hidden inside the oven.",
                "Door: Kitchen exit", L2HoldKind.Hold, new Vector3(0f, 90f, 30f), 0.24f);

            // ---------------- Level 3: Server Room
            Add(Flashlight, "Flashlight", "A heavy maintenance flashlight. Press G to switch it on or off while it is equipped.",
                "Seeing in the dark", L2HoldKind.Flashlight, new Vector3(-6f, 180f, 0f), 0.42f);
            Add(KeycardRed, "Red Keycard (MAINT)", "Printed by the Maintenance Room generator. It reads:  MAINT: m = 9",
                "The red slot of the central terminal", L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.3f);
            Add(KeycardBlue, "Blue Keycard (SEC)", "Found in the Security Room safe. It reads:  SEC: s = 4",
                "The blue slot of the central terminal", L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.3f);
            Add(KeycardGreen, "Green Keycard (CTRL)", "Printed by the Control Room after the reboot. It reads:  CTRL: c = 5",
                "The green slot of the central terminal", L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.3f);
            const string tagUse = "The red locker in the Maintenance Room (only a valid C# name opens it)";
            Add(Tag2Fast, "Name Tag \"2fast\"", "A name tag from the pegboard:  2fast = 3", tagUse, L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.26f);
            Add(TagMyVar, "Name Tag \"my var\"", "A name tag from the pegboard:  my var = 1", tagUse, L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.26f);
            Add(TagClass, "Name Tag \"class\"", "A name tag from the pegboard:  class = 7", tagUse, L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.26f);
            Add(TagFanSpeed, "Name Tag \"fanSpeed\"", "A name tag from the pegboard:  fanSpeed = 6", tagUse, L2HoldKind.Hold, new Vector3(0f, 0f, 0f), 0.26f);
            Add(Fuse2, "Fuse 2", "A cartridge fuse stamped with a big 2.", "A socket of the fuse box (x, y or temp)", L2HoldKind.Hold, new Vector3(0f, 0f, 70f), 0.22f);
            Add(Fuse9, "Fuse 9", "A cartridge fuse stamped with a big 9.", "A socket of the fuse box (x, y or temp)", L2HoldKind.Hold, new Vector3(0f, 0f, 70f), 0.22f);
        }

        public const string KeycardRed = "keycard_red", KeycardBlue = "keycard_blue", KeycardGreen = "keycard_green";
        public const string Tag2Fast = "tag_2fast", TagMyVar = "tag_myvar", TagClass = "tag_class", TagFanSpeed = "tag_fanspeed";
        public const string Fuse2 = "fuse_2", Fuse9 = "fuse_9";
    }
}
