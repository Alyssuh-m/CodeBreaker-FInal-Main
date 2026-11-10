using UnityEngine;

namespace CodeBreaker.Level2
{
    // Star shadow lamp on the console table. E switches it on / off.
    // The star projection on the bookshelf appears only while BOTH are true:
    //   the TV is on Channel 3 (blue)  AND  the wall clock has been confirmed at 8:00.
    // If either becomes false later, the star disappears again.
    public class L2StarLamp : L2Interactable
    {
        public L2TV tv;
        public L2WallClock clock;
        public Light lampLight;
        public Light starBeam;
        public GameObject starProjection;
        public Renderer bulb;

        public bool LampOn { get; private set; }
        public bool ShadowActive => LampOn && tv != null && tv.IsBlue && clock != null && clock.IsCorrect;

        bool lastShadow;

        public override string Prompt => LampOn ? "Press E to Switch the Lamp Off" : "Press E to Switch the Lamp On";

        void Start()
        {
            LampOn = Game != null && Game.Flag("lamp_on");
            lastShadow = ShadowActive;
            Refresh();
        }

        public override void Interact()
        {
            LampOn = !LampOn;
            if (Game != null)
            {
                Game.SetFlag("lamp_on", LampOn);
                if (Game.sfx != null) Game.sfx.Switch(transform.position);
                if (LampOn && !ShadowActive)
                    Game.Notify("The lamp glows, but the star on its shade throws only a blur. The room doesn't match the photo yet.", L2NoteKind.Info);
            }
            Refresh();
        }

        void Update()
        {
            bool s = ShadowActive;
            if (s == lastShadow) return;
            lastShadow = s;
            Refresh();
            if (s && Game != null)
            {
                if (Game.sfx != null) Game.sfx.Success();
                Game.Notify("A star-shaped shadow falls across the bookshelf.", L2NoteKind.Success);
                if (!Game.Flag("star_shadow_seen")) Game.SetFlag("star_shadow_seen");
                Game.RefreshObjective();
            }
        }

        void Refresh()
        {
            bool s = ShadowActive;
            if (lampLight != null) lampLight.enabled = LampOn;
            if (starBeam != null) starBeam.enabled = s;
            if (starProjection != null) starProjection.SetActive(s);
            if (bulb != null)
            {
                var mpb = new MaterialPropertyBlock();
                bulb.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", LampOn ? new Color(1f, 0.8f, 0.5f) * 2.5f : Color.black);
                bulb.SetPropertyBlock(mpb);
            }
        }
    }
}
