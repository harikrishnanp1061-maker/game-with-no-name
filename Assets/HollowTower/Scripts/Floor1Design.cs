using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Where floor 1's co-op obstacles go. The route runs from the spawn elevator in Parking access (south-east), west
    /// along the south corridor to the fuse cell in Cold Storage, then north up the west corridor to the stairs
    /// (north-west), and every leg needs one of the four players:
    ///
    ///   1. South corridor, a short hidden-pit minefield (P2 sees which tiles are holes; the others see plain floor).
    ///   2. An 8 m hidden chasm (the others see plain floor), crossed by a girder only P2 can see (anyone can walk it).
    ///   3. Cold Storage's real doors are frozen shut; the way in is a false wall from Food Prep (P1 sees it). The room is
    ///      dim with failing lamps; the fuse cell only glows in P4's torch beam; P4's compass points to it.
    ///   4. West corridor, a visible pit with a narrow safe ledge, then a collapse that blocks the corridor: the way
    ///      round is a false wall from the Staff Locker Room into the Washrooms (P1).
    ///   5. A visible pit with a ledge near the stairs, and the stairwell lock needs the fuse cell.
    ///
    /// Barricades and jammed doors close the shortcuts round each obstacle. Coordinates are world metres on the
    /// remastered 4 m grid (column c spans x 4c..4c+4, row r spans z 4(23-r)..4(24-r)).
    /// </summary>
    public static class Floor1Design
    {
        public const float Cell = 4f;

        public static Rect CellRect(int col, int row) { return new Rect(col * Cell, (23 - row) * Cell, Cell, Cell); }

        public class Hole
        {
            public Rect rect;       // world xz
            public bool hidden;     // covered by a fake floor that only P2 sees through
            public Hole(Rect r, bool hidden) { rect = r; this.hidden = hidden; }
        }

        /// <summary>A pit: the area dug out under the floor (shaft) and the parts of it with no floor left (holes).</summary>
        public class Pit
        {
            public string name;
            public Rect shaft;
            public readonly List<Hole> holes = new List<Hole>();
            public readonly List<Rect> solid = new List<Rect>();   // floor left standing inside the shaft (tiles, ledges)
            public bool hidden;
            public readonly List<Vector2Int> cells = new List<Vector2Int>();   // grid cells whose slab it replaces
        }

        // Minefield: south corridor, columns 24-26 (x 96..108, z 24..28), 9 x 3 tiles of 1.333 m. 'P' = hole.
        // Lanes run south (index 0) to north (2). 3 holes in 27 tiles, with rows of solid floor between the hazards so a
        // lost player can stop and get directions (falling is death, and enemies will be chasing them too).
        static readonly string[] MinefieldColumns =
        {
            "...", "P..", "...", "...", "..P", "...", ".P.", "...", "..."
        };

        public const float GirderZ = 26.9f;       // chasm bridge centre line
        public const float GirderHalfWidth = 0.55f;   // a 1.1 m bridge of two beams and planks

        static List<Pit> pits;
        public static List<Pit> Pits { get { if (pits == null) BuildPits(); return pits; } }

        static void BuildPits()
        {
            pits = new List<Pit>();

            var mine = new Pit { name = "Minefield", shaft = Rect.MinMaxRect(96f, 24f, 108f, 28f), hidden = true };
            for (int c = 24; c <= 26; c++) mine.cells.Add(new Vector2Int(c, 17));
            float t = 4f / 3f;
            for (int i = 0; i < MinefieldColumns.Length; i++)
                for (int j = 0; j < 3; j++)
                {
                    var r = new Rect(96f + i * t, 24f + j * t, t, t);
                    if (MinefieldColumns[i][j] == 'P') mine.holes.Add(new Hole(r, true));
                    else mine.solid.Add(r);
                }
            pits.Add(mine);

            // Chasm: two cells of the south corridor (x 48..56); the girder strip is the only floor in it.
            var chasm = new Pit { name = "Chasm", shaft = Rect.MinMaxRect(48f, 24f, 56f, 28f), hidden = true };
            chasm.cells.Add(new Vector2Int(12, 17)); chasm.cells.Add(new Vector2Int(13, 17));
            chasm.holes.Add(new Hole(Rect.MinMaxRect(48f, 24f, 56f, GirderZ - GirderHalfWidth), true));
            chasm.holes.Add(new Hole(Rect.MinMaxRect(48f, GirderZ + GirderHalfWidth, 56f, 28f), true));
            pits.Add(chasm);

            // West corridor, cell (6,15): open hole with a 1.8 m ledge along the west wall.
            var ledge = new Pit { name = "LedgePit", shaft = Rect.MinMaxRect(25.9f, 32f, 28f, 36f) };
            ledge.cells.Add(new Vector2Int(6, 15));
            ledge.holes.Add(new Hole(Rect.MinMaxRect(25.9f, 32f, 28f, 36f), false));
            ledge.solid.Add(Rect.MinMaxRect(24f, 32f, 25.9f, 36f));
            pits.Add(ledge);

            // Near the stairs, cell (6,5): visible hole with a 1.5 m ledge along the east wall.
            var stairPit = new Pit { name = "StairPit", shaft = Rect.MinMaxRect(24f, 72f, 26.5f, 76f) };
            stairPit.cells.Add(new Vector2Int(6, 5));
            stairPit.holes.Add(new Hole(Rect.MinMaxRect(24f, 72f, 26.5f, 76f), false));
            stairPit.solid.Add(Rect.MinMaxRect(26.5f, 72f, 28f, 76f));
            pits.Add(stairPit);

            // The two older corridor pits off the route get a proper shaft too.
            foreach (var cell in new[] { new Vector2Int(17, 14), new Vector2Int(21, 6) })
            {
                var p = new Pit { name = "Pit_" + cell.x + "_" + cell.y, shaft = CellRect(cell.x, cell.y) };
                p.cells.Add(cell);
                p.holes.Add(new Hole(CellRect(cell.x, cell.y), false));
                pits.Add(p);
            }
        }

        /// <summary>Collapsed corridor cells that can't be passed.</summary>
        public static readonly Vector2Int[] Barricades = { new Vector2Int(29, 16), new Vector2Int(17, 16), new Vector2Int(6, 10) };

        public class SecretWall
        {
            public string name;
            public Vector3 wallCentre;     // centre of the wall segment it replaces (x, 2, z)
            public bool alongX;            // wall runs along X (an east-west wall between two rows)
        }

        /// <summary>False walls only P1 sees: Food Prep -> Cold Storage, Staff locker room -> Washrooms.</summary>
        public static readonly SecretWall[] SecretWalls =
        {
            new SecretWall { name = "FalseWall_ColdStorage", wallCentre = new Vector3(48f, 2f, 38f), alongX = false },
            new SecretWall { name = "FalseWall_Washrooms", wallCentre = new Vector3(10f, 2f, 52f), alongX = true },
        };

        public class Jam
        {
            public Vector2 door;       // doorway centre (x, z)
            public string message;
            public bool boards;        // planks nailed across it
        }

        public static readonly Jam[] Jams =
        {
            new Jam { door = new Vector2(106f, 28f), message = "JAMMED - IT WON'T BUDGE", boards = true },     // pump station (bypass round the minefield)
            new Jam { door = new Vector2(82f, 28f), message = "JAMMED - IT WON'T BUDGE", boards = true },      // dishwashing (bypass round the chasm)
            new Jam { door = new Vector2(58f, 28f), message = "FROZEN SHUT", boards = false },                  // cold storage
            new Jam { door = new Vector2(68f, 38f), message = "FROZEN SHUT", boards = false },                  // cold storage
            new Jam { door = new Vector2(10f, 0f), message = "CHAINED SHUT FROM OUTSIDE", boards = true },      // outer doors to the old annex
            new Jam { door = new Vector2(38f, 0f), message = "CHAINED SHUT FROM OUTSIDE", boards = true },
            new Jam { door = new Vector2(94f, 0f), message = "CHAINED SHUT FROM OUTSIDE", boards = true },
            new Jam { door = new Vector2(138f, 0f), message = "CHAINED SHUT FROM OUTSIDE", boards = true },
        };

        public static readonly Vector2 StairDoor = new Vector2(24f, 86f);
        public const string StairLockedMessage = "NO POWER - THE STAIRWELL LOCK NEEDS A FUSE CELL";
        /// <summary>Fuse socket on the west wall of the corridor, just south of the stair door (faces +X).</summary>
        public static readonly Vector3 SocketPosition = new Vector3(24.125f, 1.35f, 83.0f);

        /// <summary>Fuse cell: on a bench in the far (north-east) corner of the dark Cold Storage.</summary>
        public static readonly Vector3 FusePosition = new Vector3(65.4f, 0f, 46.85f);
        public static readonly Rect DarkRoom = Rect.MinMaxRect(48f, 28f, 68f, 48f);

        /// <summary>Areas the college dressing must leave empty on floor 1.</summary>
        public static IEnumerable<Rect> DressingExclusions()
        {
            foreach (var p in Pits) foreach (var c in p.cells) yield return CellRect(c.x, c.y);
            foreach (var b in Barricades) yield return CellRect(b.x, b.y);
            foreach (var w in SecretWalls)
                yield return w.alongX ? new Rect(w.wallCentre.x - 1.6f, w.wallCentre.z - 2.2f, 3.2f, 4.4f)
                                      : new Rect(w.wallCentre.x - 2.2f, w.wallCentre.z - 1.6f, 4.4f, 3.2f);
            yield return new Rect(SocketPosition.x - 0.2f, SocketPosition.z - 1.2f, 2.2f, 2.4f);
            yield return new Rect(FusePosition.x - 2.2f, FusePosition.z - 2.4f, 4.4f, 3.6f);
        }

        public static bool Excluded(Rect footprint)
        {
            foreach (var r in DressingExclusions()) if (r.Overlaps(footprint)) return true;
            return false;
        }
    }
}
