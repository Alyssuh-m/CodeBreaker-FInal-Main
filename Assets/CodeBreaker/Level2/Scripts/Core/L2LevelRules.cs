using UnityEngine;

namespace CodeBreaker.Level2
{
    // Optional per-level rules for the shared gameplay framework (L2Game + L2UI).
    // Level 2 doesn't use one (its objectives are built into L2Game). Another level (e.g. Level 3)
    // puts a subclass in its scene and assigns it to L2Game.rules to supply its own objectives and
    // completion text.
    public abstract class L2LevelRules : MonoBehaviour
    {
        public abstract string Objective(L2Game game, out string hint);
        public virtual string CompletionTitle => "CONGRATULATIONS!";
        public virtual string CompletionBody => null;
        public virtual string NoNextLevelLabel => null;
    }

    // A keypad / code-entry puzzle shown by L2UI's code keypad screen.
    public interface ICodeKeypad
    {
        string KeypadTitle { get; }
        string KeypadCode { get; }        // the question / code shown beside the keypad (rich text)
        bool SymbolKeys { get; }          // true = + - * / %   false = digits 0-9
        int MaxLength { get; }
        string CurrentHint { get; }       // shown after enough wrong tries, else null
        bool Submit(string input, out string message);
    }
}
