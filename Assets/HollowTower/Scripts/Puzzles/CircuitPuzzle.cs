using System.Collections.Generic;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The lockbox panel: a grid of wire tiles to turn until a wire runs from the power input on the left edge to the fuse
    /// output on the right. Made solvable by construction: a random self-avoiding route is laid from input to output, its
    /// tiles get the matching straights and corners, every other tile gets a random straight / corner / tee, and then the
    /// route's tiles are turned out of place. The turns that put the route back (the shorter way round for each tile) are
    /// the proof and set the budget; any other wiring that reaches the fuse counts too. Powered tiles light up as you go,
    /// which is what makes it gentler than the old Lights Out board. Pure logic, the same on every machine for a seed.
    /// </summary>
    public class CircuitPuzzle
    {
        // a tile's sides as bits; turning clockwise moves N to E, E to S, S to W, W to N
        public const int N = 1, E = 2, S = 4, W = 8;
        static readonly int[] DX = { 0, 1, 0, -1 }, DY = { -1, 0, 1, 0 };   // N E S W, y grows downwards

        public readonly int size;
        public readonly int seed;
        public readonly int inRow, outRow;
        /// <summary>The tile's sides before turning, and how many quarter turns clockwise it is turned now.</summary>
        public readonly int[] shape, turns;
        /// <summary>For tiles on the built route: the turn that puts it in place (-1 elsewhere).</summary>
        public readonly int[] answer;
        public readonly bool[] onRoute;
        public int Moves { get; private set; }
        /// <summary>Turns the built route needs from the scrambled board (the proof's count).</summary>
        public int Needed { get; private set; }

        public CircuitPuzzle(int size, int seed, int minWrong)
        {
            this.size = size;
            this.seed = seed;
            int n = size * size;
            shape = new int[n]; turns = new int[n]; answer = new int[n]; onRoute = new bool[n];
            var rnd = new System.Random(seed);
            inRow = rnd.Next(size);
            outRow = rnd.Next(size);

            // the route: a self-avoiding walk from the left column to the right one, a bit longer than straight across
            List<int> route = null;
            for (int tries = 0; tries < 400 && route == null; tries++)
            {
                var r = Walk(rnd);
                if (r != null && r.Count >= size + 2) route = r;
            }
            if (route == null) route = Straight();

            for (int i = 0; i < n; i++) answer[i] = -1;
            for (int k = 0; k < route.Count; k++)
            {
                int c = route[k];
                int from = k == 0 ? W : Side(c, route[k - 1]);
                int to = k == route.Count - 1 ? E : Side(c, route[k + 1]);
                int want = from | to;
                int baseShape = (want == (N | S) || want == (E | W)) ? (N | S) : (N | E);
                shape[c] = baseShape;
                for (int t = 0; t < 4; t++) if (Turn(baseShape, t) == want) { answer[c] = t; break; }
                onRoute[c] = true;
            }
            // the rest of the board: decoys
            int[] decoys = { N | S, N | E, N | E | S };
            for (int i = 0; i < n; i++)
                if (!onRoute[i]) { shape[i] = decoys[rnd.Next(decoys.Length)]; turns[i] = rnd.Next(4); }

            // scramble the route: at least minWrong of its tiles out of place, and the whole thing not already connected
            var order = new List<int>(route);
            for (int i = order.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            int wrong = System.Math.Min(System.Math.Max(minWrong, 1), route.Count);
            for (int k = 0; k < order.Count; k++)
            {
                int c = order[k];
                turns[c] = answer[c];
                if (k < wrong) turns[c] = (answer[c] + 1 + rnd.Next(3)) % 4;
                if (k < wrong && Turn(shape[c], turns[c]) == Turn(shape[c], answer[c])) turns[c] = (turns[c] + 1) % 4;   // a straight half-turned is still in place
            }
            if (IsSolved) { int c = route[route.Count / 2]; turns[c] = (turns[c] + 1) % 4; }
            Needed = CountNeeded();
        }

        // ------------------------------------------------------------------ playing
        public void TurnTile(int cell, bool clockwise)
        {
            turns[cell] = (turns[cell] + (clockwise ? 1 : 3)) % 4;
            Moves++;
        }

        /// <summary>The tile's sides as it stands now.</summary>
        public int Sides(int cell) { return Turn(shape[cell], turns[cell]); }

        /// <summary>Which tiles the power reaches from the input (following wires whose ends meet).</summary>
        public bool[] Powered()
        {
            var on = new bool[size * size];
            int start = inRow * size;
            if ((Sides(start) & W) == 0) return on;
            var q = new Queue<int>();
            on[start] = true; q.Enqueue(start);
            while (q.Count > 0)
            {
                int c = q.Dequeue();
                int x = c % size, y = c / size, s = Sides(c);
                for (int d = 0; d < 4; d++)
                {
                    if ((s & (1 << d)) == 0) continue;
                    int nx = x + DX[d], ny = y + DY[d];
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    int nc = ny * size + nx;
                    if (on[nc] || (Sides(nc) & (1 << ((d + 2) % 4))) == 0) continue;
                    on[nc] = true; q.Enqueue(nc);
                }
            }
            return on;
        }

        public bool IsSolved
        {
            get
            {
                int end = outRow * size + size - 1;
                return Powered()[end] && (Sides(end) & E) != 0;
            }
        }

        /// <summary>Turns each route tile still needs (the shorter way round), for the proof overlay.</summary>
        public int TurnsLeft(int cell)
        {
            if (!onRoute[cell] || Sides(cell) == Turn(shape[cell], answer[cell])) return 0;
            int best = 4;
            for (int t = 0; t < 4; t++)
                if (Turn(shape[cell], t) == Turn(shape[cell], answer[cell]))
                {
                    int d = ((t - turns[cell]) % 4 + 4) % 4;
                    best = System.Math.Min(best, System.Math.Min(d, 4 - d));
                }
            return best;
        }

        int CountNeeded() { int c = 0; for (int i = 0; i < size * size; i++) c += TurnsLeft(i); return c; }

        // ------------------------------------------------------------------ helpers
        public static int Turn(int sides, int quarterTurns)
        {
            int s = sides;
            for (int i = 0; i < (quarterTurns % 4 + 4) % 4; i++) s = ((s << 1) | (s >> 3)) & 15;
            return s;
        }

        int Side(int from, int to)
        {
            int fx = from % size, fy = from / size, tx = to % size, ty = to / size;
            if (tx > fx) return E;
            if (tx < fx) return W;
            return ty > fy ? S : N;
        }

        List<int> Walk(System.Random rnd)
        {
            var route = new List<int>();
            var used = new bool[size * size];
            int x = 0, y = inRow;
            route.Add(y * size + x); used[y * size + x] = true;
            for (int steps = 0; steps < size * size; steps++)
            {
                if (x == size - 1 && y == outRow) return route;
                var options = new List<int>();
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DY[d];
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size || used[ny * size + nx]) continue;
                    // lean towards the exit: a step right is twice as likely
                    options.Add(d);
                    if (d == 1) options.Add(d);
                }
                if (options.Count == 0) return null;
                int pick = options[rnd.Next(options.Count)];
                x += DX[pick]; y += DY[pick];
                route.Add(y * size + x); used[y * size + x] = true;
            }
            return null;
        }

        List<int> Straight()
        {
            var route = new List<int>();
            for (int x = 0; x < size; x++) route.Add(inRow * size + x);
            int y = inRow;
            while (y != outRow) { y += outRow > y ? 1 : -1; route.Add(y * size + size - 1); }
            return route;
        }
    }
}
