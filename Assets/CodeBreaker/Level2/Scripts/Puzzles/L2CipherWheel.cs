namespace CodeBreaker.Level2
{
    // The cipher wheel on the study desk: E opens a simple wheel screen where the inner ring can be
    // turned. The shift is not given anywhere in the Study; the player uses channel = 3 (Living Room).
    public class L2CipherWheel : L2Interactable
    {
        public override string Prompt => "Press E to Use the Cipher Wheel";

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null || g.ui == null) return;
            if (g.sfx != null) g.sfx.Click();
            g.SetFlag("used_cipher_wheel");
            g.ui.OpenCipherWheel();
        }
    }
}
