using System.Collections.Generic;
using System.Text;

namespace CodeBreaker.Level3
{
    // The values every Level 3 puzzle uses in this playthrough (Phase 8: randomised puzzles).
    // All of them come from ONE number, the seed, so the same seed always gives the same puzzles.
    // Seed 0 = the original ("classic") values: lockers 8/2/5, fanSpeed 6, CAM 4, 17 % 5, PIN 42,
    // 5 green racks, 86 C, exit code 94.
    //
    // Fixed on purpose (not randomised): the fuse box (fuses 2 and 9 are Level 2 item types, and the
    // RED card's m = 9 is the fuse that moved into x), the fan answer (3) and the lever order
    // (COOLING -> BACKUP -> REBOOT), which depend on temp > 80 and online < 6 staying true.
    public class L3PuzzleSet
    {
        public int seed;
        public int[] lockers = new int[3];                  // locker1, locker2, locker3
        public int tag2fast, tagMyVar, tagClass, fanSpeed;  // name tag values (fanSpeed is the valid one)
        public int fuseX = 9, fuseY = 2;                    // after the swap (fixed)
        public int cam;                                     // 1..6, the camera whose alarm is true
        public int[] motion = new int[6];
        public bool[] doorOpen = new bool[6];
        public char[] ops = new char[3];                    // the three missing operators, in printout order
        public int[] lineA = new int[3], lineB = new int[3], lineR = new int[3];
        public bool[] rackGreen = new bool[8];              // racks 1..8
        public int temp;                                    // gauge reading (always > 80)

        // ---- derived answers
        public int LaptopAnswer => lockers[0];                      // locker2 = locker1
        public string GeneratorCode => "" + lockers[0] + fanSpeed + fuseX;
        public int ModLine { get { for (int i = 0; i < 3; i++) if (ops[i] == '%') return i; return 0; } }
        public int ModA => lineA[ModLine];
        public int ModB => lineB[ModLine];
        public int ModR => lineR[ModLine];
        public string Symbols => new string(ops);                   // e.g. "%*/"
        public string SymbolsSpaced => ops[0] + " " + ops[1] + " " + ops[2];
        public int Pin => cam * 10 + ModR;
        public int Online { get { int n = 0; foreach (bool g in rackGreen) if (g) n++; return n; } }
        public int M => fuseX;                                      // RED card
        public int S => cam;                                        // BLUE card
        public int C => Online;                                     // GREEN card
        public int ExitCode => (M > S && C != 0) ? M * 10 + S : 0;

        public string Line(int i) => lineA[i] + " ? " + lineB[i] + " = " + lineR[i];
        public string LineSolved(int i) => lineA[i] + " " + ops[i] + " " + lineB[i] + " = " + lineR[i];

        public static readonly L3PuzzleSet Classic = MakeClassic();

        static L3PuzzleSet MakeClassic()
        {
            var p = new L3PuzzleSet { seed = 0 };
            p.lockers = new[] { 8, 2, 5 };
            p.tag2fast = 3; p.tagMyVar = 1; p.tagClass = 7; p.fanSpeed = 6;
            p.cam = 4;
            p.motion = new[] { 1, 7, 3, 5, 0, 4 };
            p.doorOpen = new[] { true, false, true, true, false, false };
            p.ops = new[] { '%', '*', '/' };
            p.lineA = new[] { 17, 9, 8 }; p.lineB = new[] { 5, 3, 2 }; p.lineR = new[] { 2, 27, 4 };
            p.rackGreen = new[] { true, false, true, true, false, true, true, false };
            p.temp = 86;
            return p;
        }

        // ------------------------------------------------------------------ generation
        public static L3PuzzleSet Generate(int seed)
        {
            if (seed == 0) return Classic;
            var rng = new System.Random(seed);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                L3PuzzleSet p = TryMake(rng, seed);
                if (Validate(p).Count == 0) return p;
            }
            return Classic;   // never expected: every attempt is checked, the classic set is always valid
        }

        static L3PuzzleSet TryMake(System.Random rng, int seed)
        {
            var p = new L3PuzzleSet { seed = seed };
            // M1 lockers: three different digits 1..9
            int[] d = Distinct(rng, 3, 1, 9);
            p.lockers = d;
            // M2 name tags: four different digits 1..9
            int[] t = Distinct(rng, 4, 1, 9);
            p.tag2fast = t[0]; p.tagMyVar = t[1]; p.tagClass = t[2]; p.fanSpeed = t[3];

            // S1 camera wall: exactly one camera with motion > 3 AND door OPEN
            p.cam = rng.Next(1, 7);
            var decoys = new List<int>();
            for (int i = 0; i < 6; i++) if (i != p.cam - 1) decoys.Add(i);
            Shuffle(rng, decoys);
            p.motion[p.cam - 1] = rng.Next(4, 10); p.doorOpen[p.cam - 1] = true;
            p.motion[decoys[0]] = 3; p.doorOpen[decoys[0]] = true;                       // boundary: 3 is not > 3
            p.motion[decoys[1]] = rng.Next(4, 10); p.doorOpen[decoys[1]] = false;        // high motion, door closed
            p.motion[decoys[2]] = rng.Next(4, 10); p.doorOpen[decoys[2]] = false;
            p.motion[decoys[3]] = rng.Next(0, 3); p.doorOpen[decoys[3]] = true;          // door open, low motion
            p.motion[decoys[4]] = rng.Next(0, 4); p.doorOpen[decoys[4]] = false;

            // S2 shift log: one line each for %, * and /, in a random order
            var ops = new List<char> { '%', '*', '/' };
            Shuffle(rng, ops);
            for (int i = 0; i < 3; i++)
            {
                p.ops[i] = ops[i];
                if (ops[i] == '%') { int b = rng.Next(3, 8); int a = rng.Next(10, 30); p.lineA[i] = a; p.lineB[i] = b; p.lineR[i] = a % b; }
                else if (ops[i] == '*') { int a = rng.Next(2, 10); int b = rng.Next(2, 10); p.lineA[i] = a; p.lineB[i] = b; p.lineR[i] = a * b; }
                else { int b = rng.Next(2, 6); int q = rng.Next(2, 10); p.lineA[i] = b * q; p.lineB[i] = b; p.lineR[i] = q; }
            }

            // C1 racks: 3, 4 or 5 of the 8 racks are green (online < 6 keeps the lever order)
            int online = rng.Next(3, 6);
            var idx = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            Shuffle(rng, idx);
            for (int i = 0; i < 8; i++) p.rackGreen[i] = false;
            for (int i = 0; i < online; i++) p.rackGreen[idx[i]] = true;

            // C2 gauge: always above 80 (fan stays 3)
            p.temp = rng.Next(81, 98);
            return p;
        }

        static int[] Distinct(System.Random rng, int n, int min, int max)
        {
            var pool = new List<int>();
            for (int v = min; v <= max; v++) pool.Add(v);
            Shuffle(rng, pool);
            return pool.GetRange(0, n).ToArray();
        }

        static void Shuffle<T>(System.Random rng, List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); T x = l[i]; l[i] = l[j]; l[j] = x; }
        }

        // ------------------------------------------------------------------ solvability checks
        // Every rule a playthrough must satisfy. An empty list = valid.
        public static List<string> Validate(L3PuzzleSet p)
        {
            var e = new List<string>();
            // M1
            if (p.lockers[0] == p.lockers[1] || p.lockers[0] == p.lockers[2] || p.lockers[1] == p.lockers[2]) e.Add("lockers not distinct");
            foreach (int v in p.lockers) if (v < 1 || v > 9) e.Add("locker value out of range");
            // M2
            var tags = new HashSet<int> { p.tag2fast, p.tagMyVar, p.tagClass, p.fanSpeed };
            if (tags.Count != 4) e.Add("tag values not distinct");
            if (p.fanSpeed < 1 || p.fanSpeed > 9) e.Add("fanSpeed out of range");
            // M4
            if (p.GeneratorCode.Length != 3) e.Add("generator code not 3 digits");
            // S1: exactly one alarm camera, and it is p.cam
            int alarms = 0, alarmCam = 0;
            for (int i = 0; i < 6; i++) if (p.motion[i] > 3 && p.doorOpen[i]) { alarms++; alarmCam = i + 1; }
            if (alarms != 1 || alarmCam != p.cam) e.Add("camera wall: " + alarms + " alarm cameras");
            // S2: each line has exactly one operator among + - * / % that makes it true, and it's the intended one
            var seen = new HashSet<char>();
            for (int i = 0; i < 3; i++)
            {
                seen.Add(p.ops[i]);
                int fits = 0; char which = ' ';
                foreach (char op in "+-*/%")
                    if (Apply(p.lineA[i], op, p.lineB[i], out int r) && r == p.lineR[i]) { fits++; which = op; }
                if (p.ops[i] == '/' && p.lineA[i] % p.lineB[i] != 0) e.Add("line " + (i + 1) + ": division not exact");
                if (fits != 1 || which != p.ops[i]) e.Add("line " + (i + 1) + " (" + p.Line(i) + "): " + fits + " operators fit");
            }
            if (seen.Count != 3 || !seen.Contains('%') || !seen.Contains('*') || !seen.Contains('/')) e.Add("shift log must use %, * and /");
            // S3
            if (p.ModR < 1) e.Add("% line result must be at least 1");
            if (p.Pin < 10 || p.Pin > 99) e.Add("PIN not 2 digits");
            // C1 / C3
            if (p.Online < 3 || p.Online > 5) e.Add("online must be 3..5 (lever step 2 needs online < 6)");
            // C2 / C3
            if (p.temp <= 80 || p.temp > 99) e.Add("temp must be above 80 (fan 3, COOLING first)");
            // exit
            if (!(p.M > p.S && p.C != 0)) e.Add("terminal condition would be false");
            if (p.ExitCode < 10 || p.ExitCode > 99) e.Add("exit code not 2 digits");
            return e;
        }

        // C# integer arithmetic, as a beginner would type it
        public static bool Apply(int a, char op, int b, out int r)
        {
            r = 0;
            switch (op)
            {
                case '+': r = a + b; return true;
                case '-': r = a - b; return true;
                case '*': r = a * b; return true;
                case '/': if (b == 0) return false; r = a / b; return true;
                case '%': if (b == 0) return false; r = a % b; return true;
            }
            return false;
        }

        public string AnswerKey()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Level 3 puzzle values (seed " + seed + (seed == 0 ? ", original values" : "") + ")");
            sb.AppendLine("  M1 lockers " + lockers[0] + "/" + lockers[1] + "/" + lockers[2] + "  -> laptop " + LaptopAnswer);
            sb.AppendLine("  M2 tags 2fast=" + tag2fast + " my var=" + tagMyVar + " class=" + tagClass + " fanSpeed=" + fanSpeed);
            sb.AppendLine("  M3 fuses x=" + fuseX + " y=" + fuseY + " (fixed)   M4 generator " + GeneratorCode);
            var cams = new StringBuilder();
            for (int i = 0; i < 6; i++) cams.Append(" CAM" + (i + 1) + ":" + motion[i] + (doorOpen[i] ? "/O" : "/C"));
            sb.AppendLine("  S1 CAM " + cam + "  (" + cams.ToString().Trim() + ")");
            sb.AppendLine("  S2 " + LineSolved(0) + ",  " + LineSolved(1) + ",  " + LineSolved(2) + "  -> " + Symbols);
            sb.AppendLine("  S3 PIN = " + cam + " * 10 + (" + ModA + " % " + ModB + ") = " + Pin);
            var racks = new StringBuilder();
            for (int i = 0; i < 8; i++) if (rackGreen[i]) racks.Append(i + 1).Append(' ');
            sb.AppendLine("  C1 online " + Online + " (green racks " + racks.ToString().Trim() + ")   C2 " + temp + " C -> fan 3   C3 COOLING, BACKUP, REBOOT");
            sb.AppendLine("  Cards m=" + M + " s=" + S + " c=" + C + "  -> exit code " + ExitCode);
            return sb.ToString();
        }
    }

    // The set in use right now (classic until Level 3's L3PuzzleValues picks this playthrough's set).
    public static class L3Puzzle
    {
        public static L3PuzzleSet Current = L3PuzzleSet.Classic;
    }
}
