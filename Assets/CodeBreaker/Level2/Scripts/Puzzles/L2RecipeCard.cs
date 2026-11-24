using UnityEngine;

namespace CodeBreaker.Level2
{
    // Grandma's recipe card in the recipe book: an incomplete function. The player fills in the blank
    // (the ingredient that comes in a carton of 6) with the multiple-choice code panel. Solving it
    // unlocks the oven timer keypad. Afterwards E just shows the completed card.
    public class L2RecipeCard : L2Interactable
    {
        public string solvedFlag = "recipe_complete";
        public string[] options = { "eggs", "rice", "coffee" };
        public int correct = 0;
        public Texture image;

        const string Blank = "<color=#E8C547>______</color>";

        bool Solved => Game != null && Game.Flag(solvedFlag);

        public override string Prompt => Solved ? "Press E to Read Grandma's Recipe" : "Press E to Complete Grandma's Recipe";

        public static string Code(string fill) =>
            "void BakeCake() {\n" +
            "    Add(flour);\n" +
            "    Add(sugar);\n" +
            "    Add(" + fill + ");   // the one that comes in a carton of 6\n" +
            "    Add(milk);\n" +
            "    oven.Set(flour, sugar, " + fill + ", milk);   // each one's value, in this order\n" +
            "}\n" +
            "// Grandpa wrote each value under its jar:  name = value";

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null || g.ui == null) return;
            if (g.sfx != null) g.sfx.Click();
            g.SetFlag("read_recipe");
            if (Solved)
            {
                g.ui.ShowDocument("Grandma's Recipe", image, Code("eggs"));
                return;
            }
            g.ui.OpenChoice("GRANDMA'S RECIPE", Code(Blank), options, Pick, "Fill in the blank: which ingredient completes the function?");
        }

        string Pick(int index)
        {
            L2Game g = Game;
            CodeBreaker.Database.DatabaseHooks.Level2Answer("kitchen_recipe", index >= 0 && index < options.Length ? options[index] : "?", index == correct);   // SQLite
            if (index == correct)
            {
                if (g != null)
                {
                    g.SetFlag(solvedFlag);
                    g.Notify("Recipe complete: Add(eggs). The oven timer is unlocked.", L2NoteKind.Success);
                    g.RefreshObjective();
                }
                return null;
            }
            return "Compile error: " + options[index] + " doesn't come in a carton of 6. Try again.";
        }
    }
}
