using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CodeBreaker.Level2
{
    // Notes, clues and false leads: E opens a close-up with the object's own artwork and its text.
    // Examining never changes puzzle state (apart from an optional "read" flag used for objective hints).
    public class L2Examine : L2Interactable
    {
        public string title;
        [TextArea(2, 6)] public string body;
        [Tooltip("Close-up image (usually the prop's own texture)")]
        public Texture image;
        public string verb = "Examine";
        [Tooltip("Optional flag set the first time it is read")]
        public string flagOnRead;

        public override string Prompt => "Press E to " + verb;

        // Flags starting with this belong to the Notes journal only (see L2Game.ForgetNotes).
        public const string NoteFlagPrefix = "read:";

        // Set the first time the note is read (used by the Notes journal).
        public string ReadFlag => NoteFlagPrefix + name;

        public override void Interact()
        {
            L2Game g = Game;
            if (g == null) return;
            if (g.sfx != null) g.sfx.Click();
            if (g.ui != null) g.ui.ShowDocument(string.IsNullOrEmpty(title) ? DisplayName : title, image, body);
            g.SetFlag(ReadFlag);
            if (!string.IsNullOrEmpty(flagOnRead) && !g.Flag(flagOnRead))
            {
                g.SetFlag(flagOnRead);
                g.RefreshObjective();
            }
        }

        // ------------------------------------------------------------------ Notes journal (J)
        // Every note read in this playthrough, tidied up for the journal:
        //  - notes with the same text are listed once, their titles joined ("SERVER RACK 1, 3, 4");
        //  - notes with the same title are listed once, their lines joined;
        //  - sorted by title (numbers in natural order), so the page reads the same every time.
        public static void AppendReadNotes(L2Game g, StringBuilder sb, ref int count)
        {
            if (g == null) return;
            var byBody = new Dictionary<string, List<string>>();
            var bodyOrder = new List<string>();
            foreach (L2Examine ex in Object.FindObjectsByType<L2Examine>(FindObjectsInactive.Include))
            {
                if (!g.Flag(ex.ReadFlag)) continue;
                string t = string.IsNullOrEmpty(ex.title) ? ex.DisplayName : ex.title;
                string b = (ex.body ?? "").Trim();
                if (!byBody.TryGetValue(b, out List<string> titles)) { titles = new List<string>(); byBody[b] = titles; bodyOrder.Add(b); }
                if (!titles.Contains(t)) titles.Add(t);
            }

            var entries = new List<(string title, List<string> bodies)>();
            foreach (string b in bodyOrder)
            {
                List<string> titles = byBody[b];
                titles.Sort(NaturalCompare);
                string title = JoinTitles(titles);
                int at = entries.FindIndex(e => e.title == title);
                if (at >= 0) { if (!entries[at].bodies.Contains(b)) entries[at].bodies.Add(b); }
                else entries.Add((title, new List<string> { b }));
            }
            entries.Sort((a, b) => NaturalCompare(a.title, b.title));
            foreach (var e in entries)
            {
                e.bodies.Sort(NaturalCompare);
                sb.Append("<color=#E8C547>").Append(e.title).Append("</color>\n");
                foreach (string b in e.bodies) if (b.Length > 0) sb.Append(b).Append("\n");
                sb.Append("\n");
                count++;
            }
        }

        // "SERVER RACK 1", "SERVER RACK 3" -> "SERVER RACK 1, 3"
        static string JoinTitles(List<string> titles)
        {
            if (titles.Count == 1) return titles[0];
            int cut = titles[0].LastIndexOf(' ');
            string prefix = cut > 0 ? titles[0].Substring(0, cut + 1) : null;
            if (prefix != null && titles.TrueForAll(t => t.StartsWith(prefix) && t.Length > prefix.Length && t.IndexOf(' ', prefix.Length) < 0))
                return prefix + string.Join(", ", titles.ConvertAll(t => t.Substring(prefix.Length)));
            return string.Join("  ·  ", titles);
        }

        // Compares text with any run of digits compared by value ("RACK 2" before "RACK 10").
        public static int NaturalCompare(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    string na = a.Substring(si, i - si).TrimStart('0'), nb = b.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                    int c = string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                }
                else
                {
                    int c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }
}
