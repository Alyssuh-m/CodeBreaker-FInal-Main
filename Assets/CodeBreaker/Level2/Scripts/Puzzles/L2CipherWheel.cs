namespace CodeBreaker.Level2
{
    // The wheel on the study desk, now a PICTURE wheel: E opens a screen where each picture from the
    // desk note (heart, paw, calendar) is lined up with a word from the picture key on the whiteboard.
    // A tick shows each correct match; when all three are right the note is decoded
    // (flag "picture_decoded"): PASSWORD = who I love (my dog) + my best year.
    // The class name stays the same so the existing scene object keeps working.
    public class L2CipherWheel : L2Interactable
    {
        public override string Prompt => "Press E to Use the Picture Wheel";

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
