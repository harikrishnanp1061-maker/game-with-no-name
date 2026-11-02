using System.Collections.Generic;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// A keypad lock with a 3-digit code and a note of clues scrawled beside it ("the digits add up to 14", "the first
    /// digit is bigger than the last"...). The code is picked first and only true clues are written; clues keep being
    /// added until a brute-force check over all 1000 codes leaves exactly one, then any clue that isn't needed for that
    /// is crossed out again. So every lock has one answer, and it can be worked out from the note alone. Pure logic (no
    /// Unity), the same on every machine for the same seed.
    /// </summary>
    public class CodeLock
    {
        public const int Digits = 3;
        public readonly int seed;
        public readonly int[] code = new int[Digits];
        public readonly List<string> clues = new List<string>();
        /// <summary>How many of the 1000 codes fit the clues: always 1 (the check behind "solvable").</summary>
        public int Solutions { get; private set; }

        struct Clue { public string text; public System.Func<int[], bool> fits; }

        static readonly string[] Place = { "first", "second", "third" };

        public CodeLock(int seed)
        {
            this.seed = seed;
            var rnd = new System.Random(seed);
            for (int tries = 0; tries < 50; tries++)
            {
                for (int i = 0; i < Digits; i++) code[i] = rnd.Next(10);
                var pool = Candidates(code, rnd);
                var chosen = new List<Clue>();
                int left = 1000;
                // add the clue that cuts the most codes (with a little luck in it) until one is left
                while (left > 1 && pool.Count > 0)
                {
                    int bestI = -1, bestLeft = left;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        chosen.Add(pool[i]);
                        int l = Count(chosen);
                        chosen.RemoveAt(chosen.Count - 1);
                        if (l < bestLeft || (l == bestLeft && bestI >= 0 && rnd.Next(3) == 0)) { bestLeft = l; bestI = i; }
                    }
                    if (bestI < 0) break;
                    chosen.Add(pool[bestI]);
                    pool.RemoveAt(bestI);
                    left = bestLeft;
                }
                if (left != 1) continue;
                // cross out what the others already tell you
                for (int i = chosen.Count - 1; i >= 0; i--)
                {
                    var c = chosen[i];
                    chosen.RemoveAt(i);
                    if (Count(chosen) != 1) chosen.Insert(i, c);
                }
                if (chosen.Count < 3 || chosen.Count > 6) continue;   // one clue ("add up to 27") gives it away
                Shuffle(chosen, rnd);
                foreach (var c in chosen) clues.Add(c.text);
                Solutions = Count(chosen);
                return;
            }
            // practically never: fall back to giving the code away digit by digit
            for (int i = 0; i < Digits; i++) clues.Add("The " + Place[i] + " digit is " + code[i]);
            Solutions = 1;
        }

        public bool Check(int[] guess)
        {
            for (int i = 0; i < Digits; i++) if (guess[i] != code[i]) return false;
            return true;
        }

        static int Count(List<Clue> clues)
        {
            int n = 0;
            var g = new int[Digits];
            for (int v = 0; v < 1000; v++)
            {
                g[0] = v / 100; g[1] = v / 10 % 10; g[2] = v % 10;
                bool ok = true;
                foreach (var c in clues) if (!c.fits(g)) { ok = false; break; }
                if (ok) n++;
            }
            return n;
        }

        static bool Prime(int d) { return d == 2 || d == 3 || d == 5 || d == 7; }

        /// <summary>Every kind of clue that is true of this code.</summary>
        static List<Clue> Candidates(int[] c, System.Random rnd)
        {
            var list = new List<Clue>();
            System.Action<string, System.Func<int[], bool>> add = (t, f) => list.Add(new Clue { text = t, fits = f });

            int sum = c[0] + c[1] + c[2];
            add("The digits add up to " + sum, g => g[0] + g[1] + g[2] == sum);
            for (int i = 0; i < Digits; i++)
            {
                int k = i;
                bool even = c[k] % 2 == 0;
                add("The " + Place[k] + " digit is " + (even ? "even" : "odd"), g => (g[k] % 2 == 0) == even);
                bool prime = Prime(c[k]);
                add("The " + Place[k] + " digit is " + (prime ? "" : "not ") + "a prime number", g => Prime(g[k]) == prime);
                if (c[k] >= 5) add("The " + Place[k] + " digit is 5 or more", g => g[k] >= 5);
                else add("The " + Place[k] + " digit is less than 5", g => g[k] < 5);
            }
            for (int i = 0; i < Digits; i++)
                for (int j = i + 1; j < Digits; j++)
                {
                    int a = i, b = j, d = c[j] - c[i];
                    if (d == 0) add("The " + Place[a] + " and " + Place[b] + " digits are the same", g => g[a] == g[b]);
                    else if (d > 0) add("The " + Place[b] + " digit is " + d + " more than the " + Place[a], g => g[b] - g[a] == d);
                    else add("The " + Place[a] + " digit is " + (-d) + " more than the " + Place[b], g => g[a] - g[b] == -d);
                    if (d != 0) add("The " + Place[a] + " digit is " + (d < 0 ? "bigger" : "smaller") + " than the " + Place[b], g => d < 0 ? g[a] > g[b] : g[a] < g[b]);
                }
            bool repeats = c[0] == c[1] || c[1] == c[2] || c[0] == c[2];
            add(repeats ? "One digit appears twice" : "No digit appears twice", g => (g[0] == g[1] || g[1] == g[2] || g[0] == g[2]) == repeats);
            // a digit that is in it, and one that isn't
            int inIt = c[rnd.Next(Digits)];
            add("There is a " + inIt + " somewhere in the code", g => g[0] == inIt || g[1] == inIt || g[2] == inIt);
            int notIn;
            do notIn = rnd.Next(10); while (notIn == c[0] || notIn == c[1] || notIn == c[2]);
            add("There is no " + notIn + " in the code", g => g[0] != notIn && g[1] != notIn && g[2] != notIn);
            return list;
        }

        static void Shuffle<T>(List<T> l, System.Random rnd)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); T t = l[i]; l[i] = l[j]; l[j] = t; }
        }
    }
}
