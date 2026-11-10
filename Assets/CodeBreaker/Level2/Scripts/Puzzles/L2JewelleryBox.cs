using UnityEngine;

namespace CodeBreaker.Level2
{
    // Jewellery box on the chest of drawers: five small drawers, numbered 1 (bottom) to 5 (top).
    // Each drawer is opened directly with E (L2JewelleryDrawer on the drawer front); there is no
    // separate selection window. Drawers 1-4 are empty.
    //
    // Drawer 5 holds the Bedroom Key and stays stuck until ALL Bedroom puzzles are solved:
    //   1. MOON revealed on the mirror with the UV flashlight   (flag "moon_seen")
    //   2. Moon box opened: UP, RIGHT, RIGHT, DOWN              (flag "moonbox_open")
    //   3. Glow stars counted with the Bedroom lights off       (flag "stars_seen")
    // The key object is only active while drawer 5 is open, so it can't be picked up any other way.
    public class L2JewelleryBox : L2Interactable
    {
        public Transform[] drawers = new Transform[5];   // drawer fronts 1..5
        public Transform[] knobs = new Transform[5];
        [Tooltip("Tray that slides out with drawer 5 (holds the key)")]
        public Transform keyTray;
        [Tooltip("The Bedroom Key pick-up (only reachable while its drawer is open)")]
        public GameObject key;
        public int keyDrawer = 5;
        [Tooltip("Slide direction / distance (world space)")]
        public Vector3 slide = new Vector3(-0.42f, 0f, 0f);
        public float slideTime = 0.3f;

        public static readonly string[] RequiredFlags = { "moon_seen", "moonbox_open", "stars_seen" };

        readonly bool[] open = new bool[5];
        readonly float[] amount = new float[5];
        Vector3[] drawerClosed, knobClosed;
        Vector3 trayClosed;

        // The box itself is not a target any more: its drawers are.
        public override bool CanInteract => false;

        void Awake()
        {
            drawerClosed = new Vector3[5];
            knobClosed = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                if (drawers[i] != null) drawerClosed[i] = drawers[i].position;
                if (knobs[i] != null) knobClosed[i] = knobs[i].position;
            }
            if (keyTray != null) trayClosed = keyTray.position;
        }

        void Start()
        {
            if (key != null) key.SetActive(false);
            if (Game != null && Game.Flag("drawer5_open") && KeyDrawerUnlocked) SetDrawer(keyDrawer, true, false, true);
        }

        void Update()
        {
            for (int i = 0; i < 5; i++)
            {
                float target = open[i] ? 1f : 0f;
                if (Mathf.Approximately(amount[i], target)) continue;
                amount[i] = Mathf.MoveTowards(amount[i], target, Time.deltaTime / Mathf.Max(0.01f, slideTime));
                Place(i);
            }
        }

        public bool KeyDrawerUnlocked
        {
            get
            {
                if (Game == null) return false;
                foreach (string f in RequiredFlags) if (!Game.Flag(f)) return false;
                return true;
            }
        }

        public bool IsDrawerOpen(int number) => number >= 1 && number <= 5 && open[number - 1];

        public override void Interact() { }

        // Called by a drawer's own E interaction (and by the old drawer window, which is no longer opened).
        // Drawers are labelled like array indexes: drawer[0] (bottom) .. drawer[4] (top).
        public static int Index(int number) => number - 1;

        public string Toggle(int number)
        {
            string label = "drawer[" + Index(number) + "]";
            bool now = !IsDrawerOpen(number);
            if (now && number == keyDrawer && !KeyDrawerUnlocked)
            {
                if (Game != null)
                {
                    if (Game.sfx != null) Game.sfx.Locked(transform.position);
                    Game.Notify(label + " is stuck. Something in this room is still unsolved.", L2NoteKind.Warning);
                }
                return label + " is stuck.";
            }

            SetDrawer(number, now, true, false);
            if (!now) return label + " closed.";
            if (number == keyDrawer)
            {
                if (Game != null && !Game.Flag("drawer5_open")) { Game.SetFlag("drawer5_open"); Game.RefreshObjective(); }
                if (Game != null && Game.IsCollected(L2Items.BedroomKey)) return label + ": empty now.";
                if (Game != null) Game.Notify("There's a small key inside " + label + "!", L2NoteKind.Success);
                return label + ": there's a small key inside!";
            }
            if (Game != null) Game.Notify(label + ": Empty.", L2NoteKind.Info);
            return label + ": empty.";
        }
        void SetDrawer(int number, bool isOpen, bool fx, bool instant)
        {
            int i = number - 1;
            if (i < 0 || i >= 5) return;
            open[i] = isOpen;
            if (instant) { amount[i] = isOpen ? 1f : 0f; Place(i); }
            if (number == keyDrawer && key != null)
                key.SetActive(isOpen && KeyDrawerUnlocked && (Game == null || !Game.IsCollected(L2Items.BedroomKey)));
            if (fx && Game != null && Game.sfx != null) Game.sfx.Drawer(transform.position);
        }

        void Place(int i)
        {
            Vector3 o = slide * Mathf.SmoothStep(0f, 1f, amount[i]);
            if (drawers[i] != null) drawers[i].position = drawerClosed[i] + o;
            if (knobs[i] != null) knobs[i].position = knobClosed[i] + o;
            if (i == keyDrawer - 1 && keyTray != null) keyTray.position = trayClosed + o;
        }
    }
}
