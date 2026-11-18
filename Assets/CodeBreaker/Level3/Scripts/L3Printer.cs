using UnityEngine;
using CodeBreaker.Level2;

namespace CodeBreaker.Level3
{
    // Security printer: E prints the last shift log (the three equations missing their operator).
    // The printout slides into the tray and can then be read with E.
    public class L3Printer : L2Interactable
    {
        public string printedFlag = "sec_printed";
        public L3ShowObjects printout;
        public AudioClip printSound;

        bool Printed => Game != null && Game.Flag(printedFlag);

        public override bool CanInteract => base.CanInteract && !Printed;
        public override string Prompt => "Press E to Print the Last Shift Log";

        void Start()
        {
            if (Printed && printout != null) printout.Run(true);
        }

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null || Printed) return;
            g.SetFlag(printedFlag);
            if (g.sfx != null)
            {
                if (printSound != null) g.sfx.PlayAt(printSound, transform.position, 0.8f);
                else g.sfx.Drawer(transform.position);
            }
            g.Notify("The printer whirs and prints the last shift log.", L2NoteKind.Info);
            if (printout != null) printout.Run(false);
        }
    }
}
