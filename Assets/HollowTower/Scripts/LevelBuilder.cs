using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace HollowTower
{
    public enum EnemyKind { Walker, Runner, Brute, Ghost, Dog }

    public class SpawnPoint
    {
        public EnemyKind kind;
        public Vector3 position;
        public int floor;
    }

    public class TowerLight
    {
        public Light light;
        public HDAdditionalLightData hd;
        public int floor;
        public bool castsShadows;
        /// <summary>On the floor's mains (dies in a power cut). Emergency lamps run on batteries, torches on fire.</summary>
        public bool electric = true;
    }

    public class LevelInfo
    {
        public Vector3 playerStart;
        public float playerYaw;
        public readonly List<SpawnPoint> spawns = new List<SpawnPoint>();
        public readonly List<Pickup> fuses = new List<Pickup>();
        public readonly List<TowerLight> lights = new List<TowerLight>();
        public SecretElevator elevator;
        public SecretBookcase bookcase;
        public Bounds bounds;
        public Vector3 vaultCenter;
    }

    /// <summary>
    /// Builds the four storeys of the Halvorsen Tower from <see cref="LevelLayout"/>: slabs, ceilings,
    /// thin two-sided walls with doorways, themed furniture, lamps, ramps, the hidden service elevator,
    /// the library's secret bookcase into the Egypt "museum vault", plus enemies, items and wall hints.
    /// </summary>
    public static partial class LevelBuilder
    {
        public const float Cell = LevelLayout.Cell;
        public const float Story = LevelLayout.Story;
        public const float Slab = 0.3f;
        public const int Cols = LevelLayout.Cols;
        public const int Rows = LevelLayout.Rows;
        public const float WallH = Story - Slab;
        const float WT = 0.12f;            // wall thickness per side
        const float DoorW = 1.4f, DoorH = 2.3f;

        public static int FloorCount { get { return 4; } }
        public static FloorPlan Plan(int f) { return LevelLayout.Plans[Mathf.Clamp(f, 0, 3)]; }

        /// <summary>The Egypt vault: enemies can't enter, ghosts can't pass its walls.</summary>
        public static Bounds SafeZone { get; private set; }
        public static Transform AnnexRoot { get; private set; }
        public static bool InSafeZone(Vector3 p) { return AnnexRoot != null && SafeZone.Contains(p); }

        public static Vector3 CellCenter(int floor, int row, int col)
        {
            return new Vector3(col * Cell + Cell * 0.5f, floor * Story, (Rows - 1 - row) * Cell + Cell * 0.5f);
        }

        public static int FloorOf(float y) { return Mathf.Clamp(Mathf.FloorToInt((y + 1f) / Story), 0, 3); }

        public static Vector2Int CellOf(Vector3 p)
        {
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(p.x / Cell), 0, Cols - 1), Mathf.Clamp(Rows - 1 - Mathf.FloorToInt(p.z / Cell), 0, Rows - 1));
        }

        public static Room RoomAt(Vector3 p)
        {
            if (InSafeZone(p) || InAnnex(p) || p.z > Rows * Cell) return null;
            var c = CellOf(p);
            return Plan(FloorOf(p.y)).RoomAt(c.x, c.y);
        }

        public static bool Walkable(int f, int row, int col)
        {
            var p = Plan(f);
            return row >= 0 && col >= 0 && row < Rows && col < Cols && p.zone[row, col] >= 0 && !p.shaft[row, col] && !p.ramp[row, col]
                   && !p.pit[row, col] && p.rooms[p.zone[row, col]].type != RoomType.Elevator;
        }

        static Mats M;
        static Furnish F;
        static LevelInfo Info;
        static System.Random Rng;

        // ================= build =================

        public static LevelInfo Build(Transform root)
        {
            M = new Mats();
            Info = new LevelInfo();
            PowerGrid.Reset();
            F = new Furnish(M, Info);
            Rng = new System.Random(4242);
            AnnexRoot = null;
            fakeWall = null;
            egyptConverted.Clear();

            for (int f = 0; f < 4; f++)
            {
                var floorRoot = new GameObject("Floor " + (f + 1) + " (" + Plan(f).theme + ")").transform;
                floorRoot.SetParent(root, false);
                var shell = new GameObject("Shell").transform;
                shell.SetParent(floorRoot, false);
                BuildSlabs(f, shell);
                BuildWalls(f, shell);
                StaticMerge.Merge(shell, "F" + (f + 1) + "Shell");
                BuildRooms(f, floorRoot);
                BuildCorridorDressing(f, floorRoot);
            }

            // Roof over the top floor.
            float top = 4 * Story;
            Gfx.Box("Roof", root, new Vector3(Cols * Cell * 0.5f, top - Slab * 0.5f, Rows * Cell * 0.5f), new Vector3(Cols * Cell, Slab, Rows * Cell), M.concrete).isStatic = true;

            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(root, false);
            foreach (var s in LevelLayout.Stairs) BuildRamp(stairs, s);

            BuildVault(root);
            BuildAnnex(root);
            BuildNorthWing(root);
            BuildPits(root);
            PlaceOfficeModel(root);
            // The hotel hallway model rendered as a flat white box under HDRP, so the wing is furnished like the rest.
            PlaceGameplay(root);

            var start = Plan(3).playerStart;
            Info.playerStart = CellCenter(3, start.y, start.x) + Vector3.up * 0.1f;
            Info.playerYaw = Plan(3).playerYaw;
            // The tower, the vault to the east and the ground-floor annex to the south and west.
            Info.bounds = new Bounds(new Vector3(Cols * Cell * 0.5f + 5f, top * 0.5f, (Rows * Cell + 30f - 62f) * 0.5f),
                                     new Vector3(Cols * Cell + 100f, top + 4f, Rows * Cell + 96f));
            return Info;
        }

        // ================= shell =================

        static void BuildSlabs(int f, Transform parent)
        {
            var p = Plan(f);
            float baseY = f * Story;
            for (int r = 0; r < Rows; r++)
            {
                // Floor slab runs, grouped by finish.
                int c = 0;
                while (c < Cols)
                {
                    if (p.shaft[r, c] || p.pit[r, c]) { c++; continue; }
                    Material m = FloorMat(p, r, c);
                    int s = c;
                    while (c < Cols && !p.shaft[r, c] && !p.pit[r, c] && FloorMat(p, r, c) == m) c++;
                    float len = (c - s) * Cell;
                    Gfx.Box("Slab", parent, new Vector3(s * Cell + len * 0.5f, baseY - Slab * 0.5f, (Rows - 1 - r) * Cell + Cell * 0.5f),
                        new Vector3(len, Slab, Cell), m).isStatic = true;
                }
                // Ceiling panels (none where the floor above is open for a stairwell).
                c = 0;
                while (c < Cols)
                {
                    if (!HasCeiling(f, r, c)) { c++; continue; }
                    Material m = CeilMat(p, r, c);
                    int s = c;
                    while (c < Cols && HasCeiling(f, r, c) && CeilMat(p, r, c) == m) c++;
                    float len = (c - s) * Cell;
                    Gfx.Box("Ceiling", parent, new Vector3(s * Cell + len * 0.5f, baseY + WallH - 0.015f, (Rows - 1 - r) * Cell + Cell * 0.5f),
                        new Vector3(len, 0.03f, Cell), m, false).isStatic = true;
                }
            }
        }

        static bool HasCeiling(int f, int r, int c)
        {
            if (f < 3 && (Plan(f + 1).shaft[r, c] || Plan(f + 1).pit[r, c])) return false;   // stairwells, and the hole under a pit upstairs
            return true;
        }

        static Material FloorMat(FloorPlan p, int r, int c)
        {
            var room = p.RoomAt(c, r);
            return room != null ? M.FloorFor(room) : M.concrete;
        }

        static Material CeilMat(FloorPlan p, int r, int c)
        {
            var room = p.RoomAt(c, r);
            return room != null ? M.CeilingFor(room) : M.concrete;
        }

        enum EdgeKind { None, Solid, Door, Fake, Bookcase }

        static int ZoneOrOutside(FloorPlan p, int r, int c)
        {
            if (r < 0 || c < 0 || r >= Rows || c >= Cols) return -2;
            return p.zone[r, c];
        }

        /// <summary>Walls are built line by line; each side of a wall gets its own room's finish, merged into long runs.</summary>
        static void BuildWalls(int f, Transform parent)
        {
            var p = Plan(f);
            float baseY = f * Story;
            // Horizontal lines (constant z) between row b-1 (north) and row b (south).
            for (int b = 0; b <= Rows; b++)
            {
                float z = (Rows - b) * Cell;
                for (int side = 0; side < 2; side++)
                {
                    var run = new Run();
                    for (int c = 0; c < Cols; c++)
                    {
                        int zn = ZoneOrOutside(p, b - 1, c), zs = ZoneOrOutside(p, b, c);
                        EdgeKind k = Kind(p, zn, zs, b <= Rows ? LevelLayout.EdgeS(b - 1, c) : -1, false, 0, 0);   // b == 0 / Rows: doors in the north / south outer walls   // b == Rows: doors in the south outer wall
                        int mine = side == 0 ? zn : zs;
                        Material m = mine >= 0 && k != EdgeKind.None ? M.WallFor(p.rooms[mine]) : null;
                        float a0 = c * Cell, a1 = a0 + Cell;
                        Feed(run, k, m, a0, a1, parent, true, z + (side == 0 ? WT * 0.5f : -WT * 0.5f), baseY);
                        if (side == 0 && k == EdgeKind.Door) DoorDressing(parent, p, new Vector3((a0 + a1) * 0.5f, baseY, z), true, zn, zs);
                    }
                    Flush(run, Cols * Cell, parent, true, z + (side == 0 ? WT * 0.5f : -WT * 0.5f), baseY);
                }
            }
            // Vertical lines (constant x) between column a-1 (west) and column a (east), walked north to south.
            for (int a = 0; a <= Cols; a++)
            {
                float x = a * Cell;
                for (int side = 0; side < 2; side++)
                {
                    var run = new Run();
                    for (int r = 0; r < Rows; r++)
                    {
                        int zw = ZoneOrOutside(p, r, a - 1), ze = ZoneOrOutside(p, r, a);
                        EdgeKind k = Kind(p, zw, ze, a >= 1 && a < Cols ? LevelLayout.EdgeE(r, a - 1) : -1, true, a, r);
                        int mine = side == 0 ? zw : ze;
                        Material m = mine >= 0 && k != EdgeKind.None ? M.WallFor(p.rooms[mine]) : null;
                        float a0 = r * Cell, a1 = a0 + Cell;   // distance from the north edge
                        Feed(run, k, m, a0, a1, parent, false, x + (side == 0 ? -WT * 0.5f : WT * 0.5f), baseY);
                        if (side == 0 && k == EdgeKind.Door) DoorDressing(parent, p, new Vector3(x, baseY, Rows * Cell - (a0 + a1) * 0.5f), false, zw, ze);
                        if (side == 0 && k == EdgeKind.Fake) FakeWall(parent, new Vector3(x, baseY, Rows * Cell - (a0 + a1) * 0.5f));
                    }
                    Flush(run, Rows * Cell, parent, false, x + (side == 0 ? -WT * 0.5f : WT * 0.5f), baseY);
                }
            }
        }

        static EdgeKind Kind(FloorPlan p, int za, int zb, int key, bool vertical, int line, int row)
        {
            if (za == zb) return EdgeKind.None;
            if (za < 0 && zb < 0) return EdgeKind.None;
            if (key >= 0 && key == p.fakeWallEdge) return EdgeKind.Fake;
            if (p.doors.Contains(key)) return EdgeKind.Door;
            if (vertical && line == Cols && row == p.bookcaseRow) return EdgeKind.Bookcase;
            return EdgeKind.Solid;
        }

        class Run { public float start = -1f; public Material mat; }

        static void Feed(Run run, EdgeKind k, Material m, float a0, float a1, Transform parent, bool horizontal, float offset, float baseY)
        {
            if (m == null || k == EdgeKind.None || k == EdgeKind.Fake)
            {
                Flush(run, a0, parent, horizontal, offset, baseY);
                return;
            }
            if (run.mat != m) { Flush(run, a0, parent, horizontal, offset, baseY); run.start = a0; run.mat = m; }
            if (k == EdgeKind.Door || k == EdgeKind.Bookcase)
            {
                float w = k == EdgeKind.Bookcase ? 2.1f : DoorW, h = k == EdgeKind.Bookcase ? 2.45f : DoorH;
                float g0 = a0 + (Cell - w) * 0.5f, g1 = g0 + w;
                Flush(run, g0, parent, horizontal, offset, baseY);
                WallPiece(parent, horizontal, offset, g0, g1, baseY + h, baseY + WallH, m);   // lintel
                run.start = g1; run.mat = m;
            }
        }

        static void Flush(Run run, float end, Transform parent, bool horizontal, float offset, float baseY)
        {
            if (run.mat != null && end > run.start + 0.01f) WallPiece(parent, horizontal, offset, run.start, end, baseY, baseY + WallH, run.mat);
            run.mat = null;
            run.start = -1f;
        }

        /// <summary>A wall slab along a grid line. For vertical lines, a0/a1 are distances from the north edge.</summary>
        static void WallPiece(Transform parent, bool horizontal, float offset, float a0, float a1, float y0, float y1, Material m)
        {
            float len = a1 - a0, h = y1 - y0, mid = (a0 + a1) * 0.5f;
            Vector3 pos = horizontal ? new Vector3(mid, (y0 + y1) * 0.5f, offset) : new Vector3(offset, (y0 + y1) * 0.5f, Rows * Cell - mid);
            Vector3 size = horizontal ? new Vector3(len, h, WT) : new Vector3(WT, h, len);
            Gfx.Box("Wall", parent, pos, size, m).isStatic = true;
        }

        static void DoorDressing(Transform parent, FloorPlan p, Vector3 center, bool horizontal, int za, int zb)
        {
            // Frame around the opening, plus a door leaf left hanging open into the room.
            float depth = WT * 2f + 0.05f;
            Vector3 along = horizontal ? Vector3.right : Vector3.back;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 jamb = center + along * (s * (DoorW * 0.5f + 0.03f)) + Vector3.up * DoorH * 0.5f;
                Gfx.Box("Jamb", parent, jamb, horizontal ? new Vector3(0.06f, DoorH, depth) : new Vector3(depth, DoorH, 0.06f), M.trim, false);
            }
            Gfx.Box("DoorHead", parent, center + Vector3.up * (DoorH + 0.04f), horizontal ? new Vector3(DoorW + 0.12f, 0.08f, depth) : new Vector3(depth, 0.08f, DoorW + 0.12f), M.trim, false);

            var roomA = za >= 0 ? p.rooms[za] : null;
            var roomB = zb >= 0 ? p.rooms[zb] : null;
            if (Rng.NextDouble() < 0.3) return;                 // some doors were torn off
            // Open toward whichever side is a room rather than corridor.
            bool intoB = roomB != null && roomB.type != RoomType.Corridor;
            Vector3 normal = horizontal ? (intoB ? Vector3.back : Vector3.forward) : (intoB ? Vector3.right : Vector3.left);
            Material leafMat = (roomA != null && roomA.floor == 2) || (roomB != null && roomB.floor == 2) ? M.darkWood : (Rng.NextDouble() < 0.5 ? M.wood : M.metal);
            float angle = 80f + (float)Rng.NextDouble() * 25f;
            Vector3 hinge = center - along * (DoorW * 0.5f);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = (along * Mathf.Cos(rad) + normal * Mathf.Sin(rad)).normalized;
            var leaf = Gfx.Box("Door", parent, hinge + dir * (DoorW * 0.47f) + Vector3.up * (DoorH * 0.49f), new Vector3(DoorW * 0.94f, DoorH * 0.97f, 0.05f), leafMat, false);
            leaf.transform.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
        }

        static GameObject fakeWall;
        static void FakeWall(Transform parent, Vector3 center)
        {
            fakeWall = Gfx.Box("SuspiciousWall", parent.parent, center + Vector3.up * WallH * 0.5f, new Vector3(0.3f, WallH, Cell), M.concreteWall);
        }

        // ================= stairs =================

        static void BuildRamp(Transform root, StairSpec s)
        {
            float baseY = s.lowerFloor * Story;
            float lowX = s.climbWest ? (s.col0 + 3) * Cell : s.col0 * Cell;
            float highX = s.climbWest ? s.col0 * Cell : (s.col0 + 3) * Cell;
            float zCenter = (Rows - s.row0 - 1) * Cell;

            float run = Mathf.Abs(highX - lowX);
            float angle = Mathf.Atan2(Story, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + Story * Story) + 0.4f;
            var mid = new Vector3((lowX + highX) * 0.5f, baseY + Story * 0.5f, zCenter);
            var rot = Quaternion.Euler(0f, 0f, highX > lowX ? angle : -angle);
            Vector3 normal = rot * Vector3.up;

            var ramp = Gfx.Box("StairRamp", root, mid - normal * (Slab * 0.5f), new Vector3(length, Slab, 2f * Cell - 0.2f), M.concrete);
            ramp.transform.localRotation = rot;
            ramp.isStatic = true;
            int steps = 18;
            for (int i = 1; i < steps; i++)
            {
                float t = (float)i / steps;
                Vector3 pt = Vector3.Lerp(new Vector3(lowX, baseY, zCenter), new Vector3(highX, baseY + Story, zCenter), t);
                var strip = Gfx.Box("StepEdge", root, pt + normal * 0.01f, new Vector3(0.08f, 0.04f, 2f * Cell - 0.4f), M.steel, false);
                strip.transform.localRotation = rot;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float railZ = zCenter + side * (Cell - 0.2f);
                Vector3 a = new Vector3(lowX, baseY + 1f, railZ), b = new Vector3(highX, baseY + Story + 1f, railZ);
                var rail = Gfx.Box("Handrail", root, (a + b) * 0.5f, new Vector3(length, 0.06f, 0.06f), M.steel, false);
                rail.transform.localRotation = rot;
            }
        }

        // ================= rooms =================

        static List<Vector3> DoorPoints(FloorPlan p, Room room)
        {
            var pts = new List<Vector3>();
            float y = p.floor * Story;
            foreach (int key in room.doors) pts.Add(EdgeCenter(key, y));
            // Doors owned by neighbours also open into this room.
            foreach (int key in p.doors)
            {
                if (room.doors.Contains(key)) continue;
                int cell = key / 2, r = cell / Cols, c = cell % Cols;
                bool east = key % 2 == 0;
                int r2 = east ? r : r + 1, c2 = east ? c + 1 : c;
                if (room.Contains(c, r) || room.Contains(c2, r2)) pts.Add(EdgeCenter(key, y));
            }
            return pts;
        }

        public static Vector3 EdgeCenter(int key, float y)
        {
            if (key < 0) return new Vector3(((key - 1) / 2 + Cols) * Cell + Cell * 0.5f, y, Rows * Cell);   // a door in the north outer wall
            int cell = key / 2, r = cell / Cols, c = cell % Cols;
            if (key % 2 == 0) return new Vector3((c + 1) * Cell, y, (Rows - 1 - r) * Cell + Cell * 0.5f);
            return new Vector3(c * Cell + Cell * 0.5f, y, (Rows - 1 - r) * Cell);
        }

        static void BuildRooms(int f, Transform floorRoot)
        {
            var p = Plan(f);
            foreach (var room in p.rooms)
            {
                if (room.type == RoomType.Corridor || room.type == RoomType.Elevator) continue;
                var rr = new GameObject(room.name).transform;
                rr.SetParent(floorRoot, false);
                var doors = DoorPoints(p, room);


                if (room.type != RoomType.Stair) F.Room(room, rr, ExtraKeepOut(room, doors));
                RoomLights(room, rr, StyleFor(room));
                StaticMerge.Merge(rr, room.name);
            }
        }

        // The office model sits in the west part of the open-plan office on floor 4.
        const float OfficeModelX = 27.2f, OfficeModelZ = 63f;

        static List<Vector3> ExtraKeepOut(Room room, List<Vector3> doors)
        {
            if (room.name == "Open-plan office" && room.floor == 3)
            {
                // Keep a 7 x 9 m patch clear for the imported office furniture.
                for (float x = OfficeModelX - 3.5f; x <= OfficeModelX + 3.6f; x += 1.2f)
                    for (float z = OfficeModelZ - 4.4f; z <= OfficeModelZ + 4.4f; z += 1.2f)
                        doors.Add(new Vector3(x, room.floor * Story, z));
            }
            return doors;
        }

        enum LampStyle { Fluorescent, Warm, Dim, Industrial, Emergency }

        static LampStyle StyleFor(Room r)
        {
            switch (r.type)
            {
                case RoomType.HotelRoom: case RoomType.Suite: case RoomType.Lounge: case RoomType.Library: case RoomType.Lobby:
                case RoomType.Atrium: case RoomType.Waiting: case RoomType.Principal: return LampStyle.Warm;
                case RoomType.Boiler: case RoomType.Generator: case RoomType.Loading: case RoomType.Parking: case RoomType.Maintenance: case RoomType.Server: return LampStyle.Industrial;
                case RoomType.Storage: case RoomType.Janitor: case RoomType.Stair: case RoomType.ServiceHall: case RoomType.Records: return LampStyle.Dim;
                default: return r.floor == 2 && r.type != RoomType.Washroom && r.type != RoomType.Laundry ? LampStyle.Warm : LampStyle.Fluorescent;
            }
        }

        static void RoomLights(Room room, Transform parent, LampStyle style)
        {
            var R = room.rect;
            int nx = Mathf.Max(1, Mathf.RoundToInt(R.width / 2.6f)), nz = Mathf.Max(1, Mathf.RoundToInt(R.height / 2.6f));
            if (room.type == RoomType.Stair) { nx = 1; nz = 1; }
            float y = room.floor * Story;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float cx = (R.x + (i + 0.5f) * R.width / nx) * Cell;
                    float cz = (Rows - R.y - (j + 0.5f) * R.height / nz) * Cell;
                    if (room.type == RoomType.Stair)
                    {
                        // Light the landing, not the open shaft.
                        foreach (var spec in LevelLayout.Stairs)
                        {
                            if (spec.row0 != R.y) continue;
                            int col = spec.lowerFloor == room.floor ? spec.LowCol : (spec.lowerFloor + 1 == room.floor ? spec.HighCol : -1);
                            if (col >= 0 && room.Contains(col, R.y)) cx = col * Cell + Cell * 0.5f;
                        }
                    }
                    Lamp(parent, room.floor, new Vector3(cx, y, cz), style, room.id * 31 + i * 7 + j);
                }
        }

        /// <summary>A ceiling lamp over <paramref name="floorPos"/>, hanging from a ceiling <paramref name="height"/> m up (the tower's by default).</summary>
        static void Lamp(Transform parent, int floor, Vector3 floorPos, LampStyle style, int seed, float height = WallH)
        {
            var rnd = new System.Random(seed * 13 + floor * 1001);
            double roll = rnd.NextDouble();
            bool dead = roll < 0.18 && style != LampStyle.Emergency;
            bool flicker = !dead && roll < 0.26; // only a few lamps flicker, so the building doesn't strobe
            float ceilY = floorPos.y + height;
            // Mains fixtures get a per-floor material so a power cut can darken one floor's glow on its own.
            Material fx = style == LampStyle.Warm ? PowerGrid.FixtureFor(M.fixtureWarm, floor) : style == LampStyle.Emergency ? M.redLamp : PowerGrid.FixtureFor(M.fixtureCool, floor);
            var holder = new GameObject("Lamp").transform;
            holder.SetParent(parent, false);
            holder.position = new Vector3(floorPos.x, ceilY, floorPos.z);
            Renderer fixtureR;
            float drop = 0f;
            if (style == LampStyle.Warm)
            {
                // High ceilings (the chapel, the hall): a long chain so the globe hangs at a sensible height.
                drop = height > 5f ? height - 4.2f : 0.45f;
                Gfx.Prim(PrimitiveType.Cylinder, "Rod", holder, new Vector3(0f, -drop * 0.5f + 0.05f, 0f), new Vector3(0.02f, drop * 0.5f, 0.02f), M.brass, false);
                fixtureR = Gfx.Prim(PrimitiveType.Sphere, "Globe", holder, new Vector3(0f, -drop, 0f), new Vector3(0.38f, 0.3f, 0.38f), dead ? M.sheet : fx, false).GetComponent<Renderer>();
            }
            else if (style == LampStyle.Emergency)
                fixtureR = Gfx.Box("EmergencyLamp", holder, new Vector3(0f, -0.1f, 0f), new Vector3(0.35f, 0.16f, 0.35f), fx, false).GetComponent<Renderer>();
            else
            {
                Gfx.Box("Housing", holder, new Vector3(0f, -0.04f, 0f), new Vector3(1.3f, 0.06f, 0.36f), M.metal, false);
                fixtureR = Gfx.Box("Tube", holder, new Vector3(0f, -0.08f, 0f), new Vector3(1.2f, 0.03f, 0.28f), dead ? M.plasticGrey : fx, false).GetComponent<Renderer>();
                if (rnd.NextDouble() < 0.12) holder.localRotation = Quaternion.Euler(0f, 0f, 25f);   // hanging loose
            }
            fixtureR.shadowCastingMode = ShadowCastingMode.Off;
            if (dead) return;
            if (flicker) fixtureR.gameObject.AddComponent<NoMerge>();

            Color col; float inten, range;
            switch (style)
            {
                case LampStyle.Warm: col = new Color(1f, 0.8f, 0.58f); inten = 3.0f; range = Mathf.Max(10f, height + 3f); break;
                case LampStyle.Dim: col = new Color(1f, 0.85f, 0.7f); inten = 1.2f; range = 7f; break;
                case LampStyle.Industrial: col = new Color(1f, 0.75f, 0.5f); inten = 2.2f * Mathf.Max(1f, height / WallH); range = Mathf.Max(10f, height + 4f); break;
                case LampStyle.Emergency: col = new Color(1f, 0.08f, 0.04f); inten = 1.8f; range = 9f; break;
                default: col = new Color(0.86f, 0.93f, 1f); inten = 3.0f; range = 10f; break;
            }
            // Ceiling fixtures throw light downward: a wide spot needs one shadow view instead of a point light's six.
            var l = Gfx.SpotLight(holder, new Vector3(0f, -0.2f - Mathf.Max(0f, drop - 0.45f), 0f), Quaternion.Euler(90f, 0f, 0f), col, inten * Gfx.Lumens, range + 2f,
                style == LampStyle.Warm ? 160f : 150f, 0.45f, LightShadows.Soft);
            var lhd = l.GetComponent<HDAdditionalLightData>();
            if (lhd != null)
            {
                lhd.SetShadowResolutionLevel(1);
                lhd.volumetricDimmer = 0.15f;
                // Cached shadows: the static building is drawn into the shadow map once; only moving things redraw.
                lhd.shadowUpdateMode = ShadowUpdateMode.OnEnable;
                lhd.alwaysDrawDynamicShadows = true;
            }
            if (style == LampStyle.Emergency) l.gameObject.AddComponent<FlickerLight>().SetupPulse();
            else if (flicker)
            {
                l.gameObject.AddComponent<FlickerLight>().Setup(fixtureR, false);
                PowerGrid.RegisterFlickerFixture(fixtureR, floor);
            }
            Info.lights.Add(new TowerLight { light = l, hd = l.GetComponent<HDAdditionalLightData>(), floor = floor, castsShadows = true, electric = style != LampStyle.Emergency });
        }

        static void BuildCorridorDressing(int f, Transform floorRoot)
        {
            var p = Plan(f);
            var root = new GameObject("Corridor").transform;
            root.SetParent(floorRoot, false);
            var lamps = new GameObject("Corridor lamps").transform;
            lamps.SetParent(floorRoot, false);
            var rnd = new System.Random(77 + f);
            float y = f * Story;
            int n = 0;
            foreach (var cell in p.corridor.cells)
            {
                int c = cell.x, r = cell.y;
                if (p.pit[r, c]) continue;   // nothing on the ledge round a trap pit
                bool lamp = (r == 6 || r == 17 || (f == 2 && r == 2)) ? c % 3 == 1 : r % 3 == 1;
                if (lamp && HasCeiling(f, r, c))
                {
                    n++;
                    Lamp(lamps, f, CellCenter(f, r, c), n % 9 == 4 ? LampStyle.Emergency : (f == 2 ? LampStyle.Warm : LampStyle.Fluorescent), 500 + r * Cols + c);
                }
                // School corridors are lined with lockers; other floors get the odd bench or trolley.
                for (int d = 0; d < 4; d++)
                {
                    int dr = d == 0 ? -1 : d == 1 ? 1 : 0, dc = d == 2 ? -1 : d == 3 ? 1 : 0;
                    int nz = ZoneOrOutside(p, r + dr, c + dc);
                    if (nz == p.corridor.id) continue;
                    int key = dr == -1 ? LevelLayout.EdgeS(r - 1, c) : dr == 1 ? LevelLayout.EdgeS(r, c) : dc == -1 ? LevelLayout.EdgeE(r, c - 1) : LevelLayout.EdgeE(r, c);
                    if (p.doors.Contains(key)) continue;
                    Vector3 toWall = new Vector3(dc, 0f, -dr);
                    Vector3 basePos = CellCenter(f, r, c) + toWall * (Cell * 0.5f - 0.42f);
                    if (f == 1 && (r + c) % 2 == 0 && rnd.NextDouble() < 0.75)
                    {
                        // Two lockers side by side, the first one usable as a hiding spot.
                        Vector3 side = Vector3.Cross(Vector3.up, toWall);
                        HidingSpot.Create(HidingSpot.Kind.Locker, root, basePos - side * 0.53f, -toWall, M);
                        HidingSpot.Create(HidingSpot.Kind.Locker, root, basePos + side * 0.53f, -toWall, M);
                    }
                    else if (rnd.NextDouble() < 0.06)
                    {
                        Gfx.Box("Bench", root, basePos + Vector3.up * 0.23f + toWall * 0.15f, new Vector3(Mathf.Abs(toWall.x) > 0 ? 0.4f : 1.6f, 0.46f, Mathf.Abs(toWall.x) > 0 ? 1.6f : 0.4f), M.wood);
                    }
                    else if (rnd.NextDouble() < 0.06)
                    {
                        Gfx.Decal(root, CellCenter(f, r, c) + new Vector3((float)rnd.NextDouble() - 0.5f, 0.25f, (float)rnd.NextDouble() - 0.5f) * 2f, Vector3.down,
                            1.2f + (float)rnd.NextDouble(), 0.9f + (float)rnd.NextDouble(), Gfx.DecalKind.Blood, rnd.Next());
                    }
                    else if (rnd.NextDouble() < 0.08)
                    {
                        // Damp stains creeping up from the skirting, the odd bloody hand-smear higher up.
                        bool smear = rnd.NextDouble() < 0.3;
                        Vector3 wallPos = CellCenter(f, r, c) + toWall * (Cell * 0.5f - WT - 0.3f) + Vector3.up * (smear ? 1.3f : 0.6f);
                        Gfx.Decal(root, wallPos, toWall, smear ? 0.8f : 2.2f, smear ? 1.1f : 1.3f, smear ? Gfx.DecalKind.Blood : Gfx.DecalKind.Water, rnd.Next());
                    }
                }
            }
            StaticMerge.Merge(root, "F" + (f + 1) + "Corridor");
            StaticMerge.Merge(lamps, "F" + (f + 1) + "CorridorLamps");
        }

        // ================= specials =================

        static void BuildElevator(Transform root)
        {
            var p = Plan(0);
            if (fakeWall == null || p.elevatorCell.x < 0) return;
            Vector3 car = CellCenter(0, p.elevatorCell.y, p.elevatorCell.x);
            Info.elevator = SecretElevator.Create(root, fakeWall, car, M.elevator, M.steel);
        }

        static void BuildVault(Transform root)
        {
            var p = Plan(1);
            if (p.bookcaseRow < 0) return;
            float y = Story;
            float wallX = Cols * Cell;
            float z = (Rows - 1 - p.bookcaseRow) * Cell + Cell * 0.5f;
            AnnexRoot = new GameObject("Museum vault (annex)").transform;
            AnnexRoot.SetParent(root, false);

            // Secret bookcase in the library's east wall.
            Info.bookcase = SecretBookcase.Create(root, new Vector3(wallX - 0.26f, y, z), M);

            // A short stone passage behind it.
            const float passage = 4f;
            var pass = new GameObject("Passage").transform;
            pass.SetParent(AnnexRoot, false);
            Gfx.Box("PassageFloor", pass, new Vector3(wallX + passage * 0.5f, y - Slab * 0.5f, z), new Vector3(passage + 0.4f, Slab, 2.8f), M.stone);
            Gfx.Box("PassageCeiling", pass, new Vector3(wallX + passage * 0.5f, y + 3.2f, z), new Vector3(passage + 0.4f, 0.3f, 2.8f), M.stone);
            for (int s = -1; s <= 1; s += 2)
                Gfx.Box("PassageWall", pass, new Vector3(wallX + passage * 0.5f, y + 1.6f, z + s * 1.3f), new Vector3(passage + 0.4f, 3.4f, 0.3f), M.stone);
            var torch = Gfx.PointLight(pass, new Vector3(wallX + passage * 0.5f, y + 2.6f, z), new Color(1f, 0.6f, 0.3f), 1.6f, 8f, LightShadows.Soft);
            torch.gameObject.AddComponent<FlickerLight>().SetupFire(null);
            Info.lights.Add(new TowerLight { light = torch, hd = torch.GetComponent<HDAdditionalLightData>(), floor = 1, castsShadows = true, electric = false });

            var bounds = new Bounds(new Vector3(wallX + passage * 0.5f + 0.3f, y + 1.6f, z), new Vector3(passage, 3.2f, 2.6f));
            Vector3 entrance = new Vector3(wallX + passage, y, z);
            Vector3 vaultCenter = entrance + Vector3.right * 6f;

            var prefab = Resources.Load<GameObject>("Models/Egypt/Egypt");
            if (prefab != null)
            {
                var holder = new GameObject("Egypt chamber").transform;
                holder.SetParent(AnnexRoot, false);
                var inst = Object.Instantiate(prefab, holder, false);
                // Drop the outdoor parts (desert, entrance stairs); keep the halls and rooms.
                Bounds hall = new Bounds();
                bool haveHall = false;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    if (r.name.Contains("ground near") || b.min.z > 91.5f || b.size.magnitude > 260f) { r.gameObject.SetActive(false); continue; }
                    if (r.name == "first hallway") { hall = b; haveHall = true; }
                }
                Vector3 entryModel = haveHall ? new Vector3(hall.center.x, hall.min.y, hall.max.z) : new Vector3(62f, 13.5f, 91f);
                const float s = 0.22f;
                Quaternion rot = Quaternion.Euler(0f, -90f, 0f);   // model +Z points back toward the tower
                holder.localScale = Vector3.one * s;
                holder.localRotation = rot;
                holder.localPosition = entrance - rot * (entryModel * s) + Vector3.down * 0.02f;

                foreach (var r in inst.GetComponentsInChildren<Renderer>(false))
                {
                    ConvertEgyptMaterials(r);
                    r.shadowCastingMode = ShadowCastingMode.On;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && !r.name.StartsWith("glass") && !r.name.StartsWith("luys"))
                        r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    bounds.Encapsulate(r.bounds);
                }
                // Warm torchlight in the main spaces, a pair of stone seats where you can rest.
                var lightSpots = new List<Vector3>();
                foreach (var r in inst.GetComponentsInChildren<Renderer>(false))
                {
                    if (r.name == "first hallway" || r.name == "main room" || r.name == "middle room hallway" || r.name == "left room" || r.name == "right room")
                        lightSpots.Add(new Vector3(r.bounds.center.x, y + 2.8f, r.bounds.center.z));
                    if (r.name == "colum left" || r.name == "column right")
                        lightSpots.Add(new Vector3(r.bounds.center.x, y + 3.2f, r.bounds.center.z));
                    if (r.name == "main room") vaultCenter = new Vector3(r.bounds.center.x, y, r.bounds.center.z);
                }
                foreach (var ls in lightSpots)
                {
                    var l = Gfx.PointLight(AnnexRoot, ls, new Color(1f, 0.62f, 0.32f), 2.6f, 12f, LightShadows.Soft);
                    l.gameObject.AddComponent<FlickerLight>().SetupFire(null);
                    Info.lights.Add(new TowerLight { light = l, hd = l.GetComponent<HDAdditionalLightData>(), floor = 1, castsShadows = true, electric = false });
                }
            }
            else Debug.LogWarning("Hollow Tower: Egypt chamber model missing - the vault is just the passage.");

            // Seats: sit (E) to rest, heal and stay hidden.
            Physics.SyncTransforms();
            Vector3 seatA = vaultCenter + new Vector3(-0.9f, 0f, 1.6f), seatB = vaultCenter + new Vector3(0.9f, 0f, -1.6f);
            if (Physics.Raycast(seatA + Vector3.up * 2f, Vector3.down, out var hitA, 4f)) seatA.y = hitA.point.y;
            if (Physics.Raycast(seatB + Vector3.up * 2f, Vector3.down, out var hitB, 4f)) seatB.y = hitB.point.y;
            HidingSpot.Create(HidingSpot.Kind.Seat, AnnexRoot, seatA, Vector3.left, M);
            HidingSpot.Create(HidingSpot.Kind.Seat, AnnexRoot, seatB, Vector3.right, M);
            Info.vaultCenter = vaultCenter;

            bounds.Expand(new Vector3(0.2f, 1f, 0.2f));
            // Never let the safe zone reach back into the library.
            var min = bounds.min; min.x = Mathf.Max(min.x, wallX + 0.15f); bounds.min = min;
            SafeZone = bounds;
        }

        static readonly Dictionary<Material, Material> egyptConverted = new Dictionary<Material, Material>();
        static void ConvertEgyptMaterials(Renderer r)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null) continue;
                Material dst;
                if (!egyptConverted.TryGetValue(src, out dst))
                {
                    Texture tex = null;
                    string file = src.name.Replace(' ', '_');
                    tex = Resources.Load<Texture2D>("Models/Egypt/" + file + "DiffuseMap.tga") ?? Resources.Load<Texture2D>("Models/Egypt/" + file + ".tga");
                    if (tex == null)
                        foreach (var n in src.GetTexturePropertyNames()) { var t = src.GetTexture(n); if (t != null) { tex = t; break; } }
                    bool glassy = r.name.StartsWith("glass");
                    dst = glassy ? M.glass : Gfx.Lit(Color.white, 0.25f, 0f, tex as Texture2D);
                    if (!glassy && tex == null) dst.SetColor(Gfx.BaseColorId, new Color(0.72f, 0.62f, 0.45f));
                    egyptConverted[src] = dst;
                }
                mats[i] = dst;
            }
            r.sharedMaterials = mats;
        }

        static void PlaceOfficeModel(Transform root)
        {
            var prefab = Resources.Load<GameObject>("Models/Office");
            if (prefab == null) return;
            var holder = new GameObject("Office (dasy444, CC-BY 4.0)").transform;
            holder.SetParent(root, false);
            var inst = Object.Instantiate(prefab, holder, false);
            // Keep the furniture and floor debris; our own walls and ceiling replace the model's.
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (n.StartsWith("Plane.00")) { r.gameObject.SetActive(false); continue; }
                r.sharedMaterials = Gfx.FromGltf(r.sharedMaterials);
                r.shadowCastingMode = ShadowCastingMode.On;
                bool flat = n.StartsWith("Plane_") || n.StartsWith("node_0.018");
                if (!flat && r.bounds.size.y > 0.15f) r.gameObject.AddComponent<BoxCollider>();
            }
            holder.position = new Vector3(OfficeModelX - 0.49f, 3 * Story + 0.02f, OfficeModelZ - 0.03f);
        }

        /// <summary>The hotel hallway model has its own (lower) ceiling, so its lights hang just under it.</summary>
        static void HotelWingLights(Room wing, Transform parent)
        {
            float east = (wing.rect.x + wing.rect.width) * Cell - 0.4f;
            float cz = (Rows - 1 - (wing.rect.y + 2)) * Cell + Cell * 0.5f;
            float y = wing.floor * Story + 2.75f;
            Vector2[] spots = { new Vector2(-12f, 0f), new Vector2(-7f, 0f), new Vector2(-2.5f, 0f), new Vector2(-0.5f, -4.5f), new Vector2(-0.5f, 4.5f) };
            foreach (var s in spots)
            {
                var l = Gfx.PointLight(parent, new Vector3(east - 1.5f + s.x, y, cz + s.y), new Color(1f, 0.8f, 0.58f), 1.6f, 7f, LightShadows.Soft);
                if (Rng.NextDouble() < 0.4) l.gameObject.AddComponent<FlickerLight>().Setup(null, false);
                Info.lights.Add(new TowerLight { light = l, hd = l.GetComponent<HDAdditionalLightData>(), floor = wing.floor, castsShadows = true });
            }
        }

        static void PlaceHotelModel(Transform root)
        {
            var prefab = Resources.Load<GameObject>("Models/HotelHallway");
            var p = Plan(2);
            var wing = p.Find(RoomType.HotelWing);
            if (prefab == null || wing == null) return;
            var holder = new GameObject("Hotel hallway (mikeW, CC-BY-NC-ND)").transform;
            holder.SetParent(root, false);
            var inst = Object.Instantiate(prefab, holder, false);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                // Light-baked unlit material -> lit HDRP, keeping the baked texture as albedo so the torch works on it.
                var src = r.sharedMaterial;
                Texture tex = null;
                if (src != null)
                {
                    if (src.HasProperty("baseColorTexture")) tex = src.GetTexture("baseColorTexture");
                    if (tex == null) tex = src.mainTexture;
                }
                var lit = Gfx.Lit(new Color(1.15f, 1.15f, 1.15f), 0.2f, 0f, tex as Texture2D);
                r.sharedMaterial = lit;
                r.shadowCastingMode = ShadowCastingMode.On;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
            // East end of the T against the wing's east wall, centred north-south.
            float east = (wing.rect.x + wing.rect.width) * Cell - 0.4f;
            float cz = (Rows - 1 - (wing.rect.y + 2)) * Cell + Cell * 0.5f;   // main hall lines up with the west doorway
            holder.position = new Vector3(east - 1.5f, 2 * Story + 0.12f, cz);
        }

        // ================= enemies, items, objectives =================

        static Vector3 InsideDoor(FloorPlan p, Room room, float inward)
        {
            int key = room.doors.Count > 0 ? room.doors[0] : -1;
            Vector3 c = room.Center;
            if (key < 0) return c;
            Vector3 d = EdgeCenter(key, room.floor * Story);
            Vector3 dir = c - d; dir.y = 0f;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z)) dir = new Vector3(Mathf.Sign(dir.x), 0f, 0f); else dir = new Vector3(0f, 0f, Mathf.Sign(dir.z));
            return d + dir * inward;
        }

        static Vector3 RandomCellIn(Room room, System.Random rnd)
        {
            var cell = room.cells[rnd.Next(room.cells.Count)];
            return CellCenter(room.floor, cell.y, cell.x) + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f);
        }

        static void PlaceGameplay(Transform root)
        {
            var items = new GameObject("Items").transform;
            items.SetParent(root, false);
            var rnd = new System.Random(1234);

            // Fuses: one each on floors 4, 3 and 2, on a small table just inside a back room.
            PlaceFuse(items, Plan(3).Find("Server room"));
            PlaceFuse(items, Plan(2).Find("Laundry"));
            PlaceFuse(items, Plan(1).Find("Principal's office"));

            // One backup generator per floor, in a plant / service room (the ground floor has a real generator room).
            for (int f = 1; f < 4; f++) PlaceGenerator(items, f);   // floor 1's is in the annex security hall

            // Ammo + medkits, just inside random rooms (door areas are kept clear of furniture).
            for (int f = 0; f < 4; f++)
            {
                var p = Plan(f);
                var candidates = p.rooms.FindAll(r => r.type != RoomType.Corridor && r.type != RoomType.Stair && r.type != RoomType.Elevator && r.doors.Count > 0);
                for (int i = 0; i < 10 && candidates.Count > 0; i++)
                {
                    var room = candidates[rnd.Next(candidates.Count)];
                    candidates.Remove(room);
                    Pickup.Create(items, InsideDoor(p, room, 1.3f) + Vector3.up * 0.5f, i < 7 ? PickupKind.Ammo : PickupKind.Medkit).floor = f;
                }
            }

            // ...and in the floor 2 north wing.
            for (int i = 0; i < 6 && WingSpots.Count > 0; i++)
                Pickup.Create(items, WingSpots[rnd.Next(WingSpots.Count)] + Vector3.up * 0.5f, i < 4 ? PickupKind.Ammo : PickupKind.Medkit).floor = 1;

            // Ammo and medkits in the annex too.
            for (int i = 0; i < 9 && AnnexSpots.Count > 0; i++)
                Pickup.Create(items, AnnexSpots[rnd.Next(AnnexSpots.Count)] + Vector3.up * 0.5f, i < 6 ? PickupKind.Ammo : PickupKind.Medkit).floor = 0;

            // Enemies per floor: walker, runner, brute (boss), ghost. The bosses: 5 on floor 3, 11 on floor 2 (6 now, 5 more
            // when you take its fuse) and 20 on floor 1 (10 now, 10 more when the lift powers up) - see GameManager.SpawnWave.
            int[,] counts = { { 12, 6, BossesAtStart[0], 7 }, { 12, 6, BossesAtStart[1], 6 }, { 11, 5, BossesAtStart[2], 4 }, { 8, 2, BossesAtStart[3], 1 } };   // more chasers on floors 3, 2 and 1
            Vector3 start = CellCenter(3, Plan(3).playerStart.y, Plan(3).playerStart.x);
            for (int f = 0; f < 4; f++)
                for (int k = 0; k < 4; k++)
                    for (int i = 0; i < counts[f, k]; i++)
                    {
                        // On the ground floor about half of everything haunts the annex (never the security hall's bosses).
                        bool annex = f == 0 && (i % 2 == 1 || (EnemyKind)k == EnemyKind.Ghost && i % 3 != 0);
                        Vector3 pos = SpawnSpot(f, annex, start, f == 3 ? 22f : 0f, (EnemyKind)k == EnemyKind.Brute, rnd);
                        if (k == 3) pos += Vector3.up * 1.2f;
                        Info.spawns.Add(new SpawnPoint { kind = (EnemyKind)k, position = pos, floor = f });
                    }

            // Zombie dogs: one or two a floor, each starting beside a pack leader (the brute first, then a walker)
            // - they trot at its heel and run with it when it hunts you. Own random stream so the rest of the layout stays put.
            var dogRnd = new System.Random(4242);
            for (int f = 0; f < 4; f++)
            {
                var leaders = Info.spawns.FindAll(sp => sp.floor == f && sp.kind == EnemyKind.Brute);
                var walkers = Info.spawns.FindAll(sp => sp.floor == f && sp.kind == EnemyKind.Walker && (f != 3 || Vector3.Distance(sp.position, start) > 26f));
                if (walkers.Count > 0) leaders.Add(walkers[dogRnd.Next(walkers.Count)]);
                for (int i = 0; i < leaders.Count && i < 2; i++)
                {
                    Vector3 pos = leaders[i].position + new Vector3((float)dogRnd.NextDouble() * 2f - 1f, 0f, (float)dogRnd.NextDouble() * 2f - 1f) * 1.2f;
                    Info.spawns.Add(new SpawnPoint { kind = EnemyKind.Dog, position = pos, floor = f });
                }
            }

            // Wall writing left by the soldiers.
            Hint(Plan(3).Find("Stair lobby"), 'S', "THE PALE ONES HATE THE LIGHT\nHOLD YOUR TORCH ON THEM\nUNTIL THEY BURN");
            Hint(Plan(3).Find("Cubicle farm"), 'W', "3 FUSES\nSERVER ROOM - LAUNDRY - PRINCIPAL\nTHE LIFT NEEDS ALL THREE");
            Hint(Plan(2).Find("Hotel lounge"), 'W', "HIDE IN WARDROBES & STALLS\nTHEY FORGET YOU...\nIF THEY DIDN'T SEE YOU GET IN");
            Hint(Plan(1).Find("Library"), 'N', "THE LIBRARY HIDES A VAULT\nTHE GOLD-TRIMMED BOOKCASE\nTHE DEAD CANNOT FOLLOW YOU IN");
            Hint(Plan(1).Find("Art room"), 'W', "HURT? FIND THE VAULT\nBEHIND THE LIBRARY SHELVES\nSIT AND REST");
            Hint(Plan(0).Find("Service hallway"), 'N', "THE SERVICE LIFT IS IN THE\nSECURITY HALL - SOUTH ANNEX\nGO THROUGH PARKING ACCESS");
            Hint(Plan(0).Find("Atrium"), 'W', "THE MANSION OUT BACK\nHAS TWO STOREYS\nAND PLENTY OF WARDROBES");
            Hint(Plan(0).Find("Cafeteria"), 'W', "THE BIG ONE HEARS\nEVERYTHING\nCROUCH. DON'T RUN.");
        }

        /// <summary>Bosses on each floor at the start (floor 1 first); the waves bring floor 2 to 11 and floor 1 to 20.</summary>
        public static readonly int[] BossesAtStart = { 10, 6, 5, 0 };
        public static readonly int[] BossWaves = { 10, 5, 0, 0 };

        /// <summary>A random spot for an enemy: a room cell in the tower, or (ground floor) a spot in the annex.</summary>
        public static Vector3 SpawnSpot(int f, bool annex, Vector3 awayFrom, float minDist, bool boss, System.Random rnd)
        {
            var p = Plan(f);
            var rooms = p.rooms.FindAll(r => r.type != RoomType.Stair && r.type != RoomType.Elevator && r.type != RoomType.StairLobby);
            Vector3 pos = Vector3.zero;
            for (int tries = 0; tries < 30; tries++)
            {
                if (annex && f == 0 && AnnexSpots.Count > 0) pos = AnnexSpots[rnd.Next(AnnexSpots.Count)] + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f);
                else if (f == 1 && WingSpots.Count > 0 && rnd.NextDouble() < 0.3) pos = WingSpots[rnd.Next(WingSpots.Count)] + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f);
                else pos = RandomCellIn(rooms[rnd.Next(rooms.Count)], rnd);
                var cell = CellOf(pos);
                if (!InAnnex(pos) && pos.z < Rows * Cell && p.pit[cell.y, cell.x]) continue;
                // Upstairs, not right against the outer walls (the navmesh on an annex roof outside can be closer than the floor).
                if (f > 0 && !InWing(pos) && (pos.z < 3.5f || pos.x < 3.5f || pos.x > Cols * Cell - 3.5f)) continue;
                if (boss && SecurityHall.size.x > 0f && SecurityHall.Contains(pos + Vector3.up)) continue;   // the lift's corner stays clear
                if (Vector3.Distance(pos, awayFrom) >= minDist) break;
            }
            return pos;
        }

        static void PlaceFuse(Transform parent, Room room)
        {
            if (room == null) return;
            Vector3 p = InsideDoor(Plan(room.floor), room, 1.8f);
            Gfx.Box("FuseTable", parent, p + Vector3.up * 0.4f, new Vector3(0.9f, 0.8f, 0.6f), M.darkWood);
            var fuse = Pickup.Create(parent, p + Vector3.up * 1.15f, PickupKind.Fuse);
            fuse.floor = room.floor;
            Info.fuses.Add(fuse);
        }

        static void PlaceGenerator(Transform parent, int f)
        {
            var p = Plan(f);
            Room room = null;
            foreach (var t in new[] { RoomType.Generator, RoomType.Maintenance, RoomType.Boiler, RoomType.Janitor, RoomType.Storage, RoomType.Housekeeping, RoomType.Records })
            {
                room = p.rooms.Find(r => r.type == t && r.doors.Count > 0 && r.cells.Count >= 4);
                if (room != null) break;
            }
            if (room == null) room = p.rooms.Find(r => r.type == RoomType.Corridor);
            if (room == null) return;
            // Just inside the door, set to one side so it doesn't block the way in, facing the doorway.
            Vector3 door = InsideDoor(p, room, 0.1f), inside = InsideDoor(p, room, 1.9f);
            Vector3 inward = inside - door; inward.y = 0f;
            inward = inward.sqrMagnitude > 0.01f ? inward.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 pos = inside + side * 1.0f;
            Physics.SyncTransforms();
            if (Physics.CheckBox(pos + Vector3.up * 0.6f, new Vector3(0.6f, 0.5f, 0.6f), Quaternion.identity, Gfx.WorldMask, QueryTriggerInteraction.Ignore))
                pos = inside - side * 1.0f;
            Generator.Create(parent, pos, -inward, f);
        }

        static void Hint(Room room, char wall, string text)
        {
            if (room == null) return;
            var R = room.rect;
            float y = room.floor * Story + 1.8f;
            float x0 = R.x * Cell + WT + 0.02f, x1 = (R.x + R.width) * Cell - WT - 0.02f;
            float z1 = (Rows - R.y) * Cell - WT - 0.02f, z0 = (Rows - R.y - R.height) * Cell + WT + 0.02f;
            Vector3 pos, dir;
            switch (wall)
            {
                case 'N': pos = new Vector3((x0 + x1) * 0.5f + 1.5f, y, z1); dir = Vector3.forward; break;
                case 'S': pos = new Vector3((x0 + x1) * 0.5f + 1.5f, y, z0); dir = Vector3.back; break;
                case 'E': pos = new Vector3(x1, y, (z0 + z1) * 0.5f + 1f); dir = Vector3.right; break;
                default: pos = new Vector3(x0, y, (z0 + z1) * 0.5f + 1f); dir = Vector3.left; break;
            }
            var g = new GameObject("WallHint");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var tm = g.AddComponent<TextMesh>();
            var font = Gfx.BuiltinFont();
            if (font != null)
            {
                tm.font = font;
                g.GetComponent<MeshRenderer>().sharedMaterial = Gfx.UnlitGlow(font.material.mainTexture, new Color(1.6f, 0.05f, 0.03f), false);
            }
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.026f;
            tm.fontSize = 64;
            tm.color = new Color(0.75f, 0.03f, 0.02f);
            g.AddComponent<HintGlow>();
        }

        // ================= navigation =================

        public static void BakeNavMesh(Bounds bounds)
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            NavMeshBuilder.CollectSources(bounds, Gfx.WorldMask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            // The vault is cut out of the navmesh: the dead cannot follow you in.
            if (AnnexRoot != null)
                sources.RemoveAll(s => s.component != null && s.component.transform.IsChildOf(AnnexRoot));
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.9f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 40f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null) NavMesh.AddNavMeshData(data);
            else Debug.LogWarning("Hollow Tower: NavMesh build failed, zombies will not be able to walk.");
        }
    }
}
