using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Study computer: the final, cumulative challenge. Nothing is inserted; the player APPLIES values
    // learned in earlier rooms:
    //   check 1:  int shift = channel;        // channel = ?   -> 3  (Living Room)
    //   check 2:  if (stars > 4) unlock();    // stars = ?     -> 5  (Bedroom)
    //   then the password screen:  password = petName + year  -> BYTE2019 (any capitalisation)
    // Wrong check answers show "Compile error" and can be retried. The correct password completes Level 2.
    public class L2Workstation : L2Interactable, ICodeKeypad
    {
        public string password = "BYTE2019";
        public Renderer screen;
        public Material unlockedScreen;
        public string[] checkAnswers = { "3", "5" };

        public bool Unlocked { get; private set; }

        public override string Prompt => "Press E to Use the Computer";
        public override bool CanInteract => base.CanInteract && !Unlocked;

        int Stage => Game != null ? Game.GetInt("terminal_checks", 0) : 0;   // 0, 1 = checks done so far, 2 = password
        bool switching;

        // Shown above the password field once both checks are done.
        public const string TerminalCode =
            "int shift = channel;   // 3\n" +
            "string pet = Decode(\"SHW QDPH\", shift);\n" +
            "int year = ????;   // proudest year\n" +
            "string password = petName + year;";

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null || g.ui == null || switching) return;
            if (Stage < 2) g.ui.OpenCodeKeypad(this);
            else g.ui.OpenPassword(this);
        }

        // ------------------------------------------------------------------ check questions (code keypad)
        public string KeypadTitle => "TERMINAL  ·  CHECK " + (Stage + 1) + " / 2";

        public string KeypadCode => Stage == 0
            ? "> boot decoder.exe\n\nint shift = channel;   // channel = ?\n\n// Enter the TV channel you found in the Living Room."
            : "> boot decoder.exe\n\nint shift = channel;   // 3  OK\nif (stars > 4) unlock();   // stars = ?\n\n// Enter how many stars you counted in the Bedroom.";

        public bool SymbolKeys => false;
        public int MaxLength => 2;
        public string CurrentHint => null;

        public bool Submit(string input, out string message)
        {
            int stage = Stage;
            if (stage >= 2) { message = "OK"; return true; }
            string want = checkAnswers[Mathf.Clamp(stage, 0, checkAnswers.Length - 1)];
            if ((input ?? "").Trim().TrimStart('0') == want)
            {
                Game.SetInt("terminal_checks", stage + 1);
                message = stage == 0 ? "shift = 3" : "stars = 5";
                if (stage == 0) Game.Notify("Check 1 passed: shift = channel = 3.", L2NoteKind.Success);
                else Game.Notify("Check 2 passed. The decoder is unlocked.", L2NoteKind.Success);
                StartCoroutine(Next());
                return true;
            }
            if (Game.sfx != null) Game.sfx.Failure();
            message = "Compile error. Try again.";
            return false;
        }

        IEnumerator Next()
        {
            switching = true;
            yield return new WaitForSecondsRealtime(1.3f);   // the keypad closes itself after a correct answer
            switching = false;
            L2Game g = Game;
            if (g == null || g.ui == null || g.Completed) yield break;
            if (Stage < 2) g.ui.OpenCodeKeypad(this);
            else g.ui.OpenPassword(this);
        }

        // ------------------------------------------------------------------ password
        // Case-insensitive; spaces around the entry are ignored.
        public bool Check(string entered)
        {
            if (entered == null) return false;
            return string.Equals(entered.Trim(), password, System.StringComparison.OrdinalIgnoreCase);
        }

        // Called by the password screen after its login animation.
        public void UnlockAndFinish()
        {
            if (Unlocked) return;
            Unlocked = true;
            if (screen != null && unlockedScreen != null) screen.sharedMaterial = unlockedScreen;
            if (L2LevelSession.Instance != null) L2LevelSession.Instance.FinishLevel();
            if (Game != null) Game.CompleteLevel();
        }
    }
}
