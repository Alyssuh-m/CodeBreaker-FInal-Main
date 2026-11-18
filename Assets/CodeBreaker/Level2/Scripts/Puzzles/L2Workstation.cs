using System.Collections;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Study computer: the final, cumulative challenge. Nothing is inserted; the player APPLIES values
    // learned in earlier rooms:
    //   check 1:  int channel = ?;            // the TV channel -> 3  (Living Room)
    //   check 2:  if (stars > 4) unlock();    // stars = ?      -> 5  (Bedroom)
    //   then the password screen:  password = who I love (the dog) + best year -> BYTE2019
    //   (any capitalisation, spaces ignored: "BYTE 2019" works too)
    // The pictures on the desk note are decoded with the picture key on the whiteboard.
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
            "// note: PASSWORD = [heart] + [calendar],  [heart] = [paw]\n" +
            "string love = Decode([heart]);     // see the picture key\n" +
            "int year = Decode([calendar]);     // ????\n" +
            "string password = love + year;";

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
            ? "> boot decoder.exe\n\nint channel = ?;   // the TV channel\n\n// Enter the TV channel you found in the Living Room."
            : "> boot decoder.exe\n\nint channel = 3;   // OK\nif (stars > 4) unlock();   // stars = ?\n\n// Enter how many stars you counted in the Bedroom.";

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
                message = stage == 0 ? "channel = 3" : "stars = 5";
                if (stage == 0) Game.Notify("Check 1 passed: channel = 3.", L2NoteKind.Success);
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
        // Case-insensitive; all spaces are ignored ("BYTE2019", "byte 2019" and " Byte 2019 " all work).
        public bool Check(string entered)
        {
            if (entered == null) return false;
            string a = Squash(entered), b = Squash(password);
            return a.Length > 0 && string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        static string Squash(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if (!char.IsWhiteSpace(c)) sb.Append(c);
            return sb.ToString();
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
