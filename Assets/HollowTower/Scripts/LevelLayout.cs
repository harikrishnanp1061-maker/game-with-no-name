using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    public enum RoomType
    {
        Corridor, Stair, StairLobby, Elevator, ServiceHall,
        // floor 4 - offices
        OpenOffice, Office, Meeting, Server, Records, Copy, BreakRoom,
        // floor 3 - hotel
        HotelWing, HotelRoom, Suite, Linen, Laundry, Lounge, Housekeeping, Vending,
        // floor 2 - school
        Classroom, Lab, ArtRoom, StaffRoom, Library, Nurse, Music, Principal, ComputerLab, LockerRoom,
        // floor 1 - ground
        Lobby, Security, Mail, Cafeteria, Kitchen, Boiler, Loading, Generator, Atrium, Shop, Waiting, Maintenance, Parking,
        // shared
        Washroom, Storage, Janitor,
    }

    /// <summary>A room (or the corridor network) on one floor. Rect is in grid cells: x = column, y = row (row 0 = north).</summary>
    public class Room
    {
        public int id;
        public string name;
        public string label;   // what the college calls it (see CollegeRoles); falls back to name
        public string role;    // what it is furnished as (see CollegeRoles)
        public string Label { get { return string.IsNullOrEmpty(label) ? name : label; } }
        public RoomType type;
        public int floor;
        public RectInt rect;
        public readonly List<Vector2Int> cells = new List<Vector2Int>(); // (col,row)
        public readonly List<int> doors = new List<int>();               // edge keys

        public Vector3 Center
        {
            get
            {
                return new Vector3((rect.x + rect.width * 0.5f) * LevelLayout.Cell, floor * LevelLayout.Story,
                                   (LevelLayout.Rows - rect.y - rect.height * 0.5f) * LevelLayout.Cell);
            }
        }
        public bool Contains(int col, int row) { return rect.Contains(new Vector2Int(col, row)); }
    }

    /// <summary>A ramp staircase between <see cref="lowerFloor"/> and the floor above.</summary>
    public class StairSpec
    {
        public int lowerFloor, col0, row0;  // first ramp column, top ramp row (ramp is 3 x 2 cells)
        public bool climbWest;              // true: low end east, climbs toward -X
        public int LowCol { get { return climbWest ? col0 + 3 : col0 - 1; } }   // landing on the lower floor
        public int HighCol { get { return climbWest ? col0 - 1 : col0 + 3; } }  // landing on the upper floor
    }

    public class FloorPlan
    {
        public int floor;
        public string theme;
        public readonly int[,] zone = new int[LevelLayout.Rows, LevelLayout.Cols];   // room id or -1 (solid)
        public readonly bool[,] shaft = new bool[LevelLayout.Rows, LevelLayout.Cols]; // open to the floor below
        public readonly bool[,] ramp = new bool[LevelLayout.Rows, LevelLayout.Cols];  // ramp up starts here
        /// <summary>Trap pit in the middle of this cell (a ledge runs round it).</summary>
        public readonly bool[,] pit = new bool[LevelLayout.Rows, LevelLayout.Cols];
        public readonly List<Vector2Int> pits = new List<Vector2Int>();   // (col,row)
        public readonly List<Room> rooms = new List<Room>();
        public readonly HashSet<int> doors = new HashSet<int>();
        public Room corridor;
        public Vector2Int playerStart = new Vector2Int(-1, -1);
        public float playerYaw;
        public int fakeWallEdge = -1;      // floor 1: wall hiding the elevator
        public Vector2Int elevatorCell = new Vector2Int(-1, -1);
        public int bookcaseRow = -1;        // floor 2: secret bookcase in the east outer wall

        public FloorPlan(int f)
        {
            floor = f;
            for (int r = 0; r < LevelLayout.Rows; r++)
                for (int c = 0; c < LevelLayout.Cols; c++) zone[r, c] = -1;
        }

        public Room RoomAt(int col, int row)
        {
            if (row < 0 || row >= LevelLayout.Rows || col < 0 || col >= LevelLayout.Cols) return null;
            int z = zone[row, col];
            return z >= 0 ? rooms[z] : null;
        }

        public Room Find(RoomType t) { foreach (var r in rooms) if (r.type == t) return r; return null; }
        public Room Find(string name) { foreach (var r in rooms) if (r.name == name) return r; return null; }
    }

    /// <summary>
    /// The four storeys of the Halvorsen Tower, laid out on a 36 x 24 grid of 3 m cells (108 x 72 m).
    /// Every floor shares the same corridor ring (rows 6 and 17, columns 6 and 29) and spine (column 17);
    /// rooms fill the blocks between and open onto the corridors through doorways.
    /// Walls are thin and sit on cell edges: wherever two different rooms meet, without a door.
    /// </summary>
    public static class LevelLayout
    {
        public const float Cell = 3f;
        public const float Story = 4f;
        public const int Cols = 36;
        public const int Rows = 24;

        public static readonly StairSpec[] Stairs =
        {
            new StairSpec { lowerFloor = 0, col0 = 2, row0 = 2, climbWest = true },   // 1 -> 2 (north-west)
            new StairSpec { lowerFloor = 1, col0 = 31, row0 = 20, climbWest = false }, // 2 -> 3 (south-east)
            new StairSpec { lowerFloor = 2, col0 = 2, row0 = 2, climbWest = true },   // 3 -> 4 (north-west)
        };

        // ---- edge keys ----
        /// <summary>Edge between (row, col) and (row, col+1).</summary>
        public static int EdgeE(int row, int col) { return (row * Cols + col) * 2; }
        /// <summary>Edge between (row, col) and (row+1, col).</summary>
        public static int EdgeS(int row, int col) { return (row * Cols + col) * 2 + 1; }

        static FloorPlan[] plans;
        public static FloorPlan[] Plans { get { if (plans == null) Build(); return plans; } }

        static FloorPlan P;

        static Room Add(string name, RoomType type, int c0, int r0, int w, int h)
        {
            var room = new Room { id = P.rooms.Count, name = name, type = type, floor = P.floor, rect = new RectInt(c0, r0, w, h) };
            P.rooms.Add(room);
            for (int r = r0; r < r0 + h; r++)
                for (int c = c0; c < c0 + w; c++)
                {
                    if (P.zone[r, c] >= 0) Debug.LogWarning("Hollow Tower layout: overlap at floor " + (P.floor + 1) + " r" + r + " c" + c + " (" + name + ")");
                    P.zone[r, c] = room.id;
                    room.cells.Add(new Vector2Int(c, r));
                }
            return room;
        }

        static void Corr(int c0, int r0, int w, int h)
        {
            if (P.corridor == null)
            {
                P.corridor = new Room { id = P.rooms.Count, name = "Corridor", type = RoomType.Corridor, floor = P.floor, rect = new RectInt(0, 0, Cols, Rows) };
                P.rooms.Add(P.corridor);
            }
            for (int r = r0; r < r0 + h; r++)
                for (int c = c0; c < c0 + w; c++)
                {
                    if (P.zone[r, c] >= 0) continue;
                    P.zone[r, c] = P.corridor.id;
                    P.corridor.cells.Add(new Vector2Int(c, r));
                }
        }

        /// <summary>Door on a room's side ('N','S','E','W'), <paramref name="i"/> cells from its north/west end.</summary>
        static void Door(Room room, char side, int i)
        {
            var R = room.rect;
            int key;
            switch (side)
            {
                case 'N': key = EdgeS(R.y - 1, R.x + i); break;
                case 'S': key = EdgeS(R.y + R.height - 1, R.x + i); break;
                case 'W': key = EdgeE(R.y + i, R.x - 1); break;
                default: key = EdgeE(R.y + i, R.x + R.width - 1); break;
            }
            P.doors.Add(key);
            room.doors.Add(key);
        }

        static void CommonCorridors()
        {
            Corr(1, 6, 34, 1);
            Corr(1, 17, 34, 1);
            Corr(6, 1, 1, 22);
            Corr(29, 1, 1, 22);
            Corr(17, 7, 1, 10);
        }

        /// <summary>Stair zone on this floor for each staircase touching it (ramp below, shaft above).</summary>
        static Room StairZone(StairSpec s)
        {
            bool lower = s.lowerFloor == P.floor;
            int c0 = lower ? Mathf.Min(s.col0, s.LowCol) : Mathf.Min(s.col0, s.HighCol);
            var z = Add(lower ? "Stairs up" : "Stairs down", RoomType.Stair, c0, s.row0, 4, 2);
            for (int r = s.row0; r < s.row0 + 2; r++)
                for (int c = s.col0; c < s.col0 + 3; c++)
                {
                    if (lower) P.ramp[r, c] = true; else P.shaft[r, c] = true;
                }
            return z;
        }

        static void Build()
        {
            plans = new FloorPlan[4];
            for (int f = 0; f < 4; f++)
            {
                P = plans[f] = new FloorPlan(f);
                switch (f)
                {
                    case 0: Ground(); break;
                    case 1: School(); break;
                    case 2: Hotel(); break;
                    default: Offices(); break;
                }
                CommonCorridors();
                Validate(P);
                CollegeRoles.Apply(P);
            }
            P = null;
        }

        // ================= Floor 4: abandoned offices (start) =================
        static void Offices()
        {
            P.theme = "Offices";
            var st = StairZone(Stairs[2]);                          // shaft from floor 3, landing col 1
            var lob = Add("Stair lobby", RoomType.StairLobby, 0, 4, 6, 2); Door(lob, 'N', 1); Door(lob, 'E', 0);
            var sup = Add("Supply room", RoomType.Storage, 0, 0, 6, 2); Door(sup, 'E', 1);
            var jan = Add("Janitor's closet", RoomType.Janitor, 5, 2, 1, 2); Door(jan, 'E', 0);

            // Subdivided North Offices (intimate small rooms)
            var o401 = Add("Office 401", RoomType.Office, 7, 0, 4, 6); Door(o401, 'S', 2); Door(o401, 'W', 2);
            var o402 = Add("Office 402", RoomType.Office, 11, 0, 3, 6); Door(o402, 'S', 1);
            var o403 = Add("Office 403", RoomType.Office, 14, 0, 3, 6); Door(o403, 'S', 1);
            var sec = Add("Security Control", RoomType.Security, 17, 0, 4, 6); Door(sec, 'S', 2);

            var ma = Add("Meeting room A", RoomType.Meeting, 21, 0, 4, 6); Door(ma, 'S', 1);
            var mb = Add("Meeting room B", RoomType.Meeting, 25, 0, 4, 6); Door(mb, 'S', 2); Door(mb, 'E', 2);
            var ex = Add("Executive office", RoomType.Office, 30, 0, 6, 3); Door(ex, 'W', 1);
            var rec = Add("Records room", RoomType.Records, 30, 3, 6, 3); Door(rec, 'W', 1); Door(rec, 'S', 2);

            var mw = Add("Men's washroom", RoomType.Washroom, 0, 7, 6, 4); Door(mw, 'E', 1);
            var ww = Add("Women's washroom", RoomType.Washroom, 0, 11, 6, 4); Door(ww, 'E', 1);
            var cp = Add("Copy room", RoomType.Copy, 0, 15, 6, 2); Door(cp, 'E', 0);

            // Subdivided Center-West Offices
            var cub = Add("Cubicle farm", RoomType.OpenOffice, 7, 7, 5, 6); Door(cub, 'N', 2); Door(cub, 'E', 2);
            var fin = Add("Finance Office", RoomType.Office, 12, 7, 5, 6); Door(fin, 'N', 2); Door(fin, 'E', 2);
            var br = Add("Break room", RoomType.BreakRoom, 7, 13, 5, 4); Door(br, 'S', 2);
            var ar = Add("Archive", RoomType.Records, 12, 13, 5, 4); Door(ar, 'E', 1);

            // Subdivided Center-East Conference & Offices
            var conf = Add("Conference hall", RoomType.Meeting, 18, 7, 6, 5); Door(conf, 'W', 2); Door(conf, 'N', 3);
            var brief = Add("Briefing Room B", RoomType.Meeting, 24, 7, 5, 5); Door(brief, 'E', 2); Door(brief, 'N', 2);
            var o1 = Add("Office 4A", RoomType.Office, 18, 12, 4, 5); Door(o1, 'S', 1);
            var o2 = Add("Office 4B", RoomType.Office, 22, 12, 4, 5); Door(o2, 'S', 1);
            var o3 = Add("Office 4C", RoomType.Office, 26, 12, 3, 5); Door(o3, 'E', 2);

            var srv = Add("Server room", RoomType.Server, 30, 7, 6, 5); Door(srv, 'W', 2);
            var it = Add("IT office", RoomType.Office, 30, 12, 6, 5); Door(it, 'W', 2);

            var sto = Add("Storage", RoomType.Storage, 0, 18, 6, 6); Door(sto, 'E', 1);
            // Subdivided South Offices
            var op2 = Add("Open-plan office 2", RoomType.OpenOffice, 7, 18, 5, 6); Door(op2, 'N', 2);
            var leg = Add("Legal Records", RoomType.Records, 12, 18, 5, 6); Door(leg, 'N', 2);
            var kit = Add("Kitchenette", RoomType.BreakRoom, 17, 18, 4, 6); Door(kit, 'N', 1);
            var mgr = Add("Manager's office", RoomType.Office, 21, 18, 4, 6); Door(mgr, 'N', 1);
            var dir = Add("Director's office", RoomType.Office, 25, 18, 4, 6); Door(dir, 'N', 2); Door(dir, 'E', 2);
            var wc = Add("Washroom", RoomType.Washroom, 30, 18, 6, 3); Door(wc, 'W', 1);
            var s2 = Add("Storage 2", RoomType.Storage, 30, 21, 6, 3); Door(s2, 'W', 1);

            P.playerStart = new Vector2Int(9, 2);   // in Office 401
            P.playerYaw = 90f;
        }

        // ================= Floor 3: hotel wing =================
        static void Hotel()
        {
            P.theme = "Hotel";
            var st = StairZone(Stairs[2]); Door(st, 'E', 0);       // ramp up to floor 4, landing col 5
            var lin = Add("Linen room", RoomType.Linen, 0, 0, 6, 2); Door(lin, 'E', 1);
            var r301 = Add("Room 301", RoomType.HotelRoom, 0, 4, 6, 2); Door(r301, 'E', 0);

            var wing = Add("Hotel wing", RoomType.HotelWing, 7, 0, 6, 6); Door(wing, 'W', 2); Door(wing, 'S', 5);
            Corr(13, 2, 16, 1);  // guest corridor through the north block
            string[] north = { "Room 302", "Room 303", "Room 304", "Room 305", "Room 306" };
            int[] nx = { 13, 16, 19, 22, 25 }, nw = { 3, 3, 3, 3, 4 };
            for (int i = 0; i < 5; i++) { var r = Add(north[i], RoomType.HotelRoom, nx[i], 0, nw[i], 2); Door(r, 'S', 1); }
            string[] south = { "Room 307", "Room 308", "Room 309", "Room 310" };
            for (int i = 0; i < 4; i++) { var r = Add(south[i], RoomType.HotelRoom, 13 + i * 4, 3, 4, 3); Door(r, 'N', 1); }

            var pent = Add("Penthouse suite", RoomType.Suite, 30, 0, 6, 4); Door(pent, 'W', 1);
            var r311 = Add("Room 311", RoomType.HotelRoom, 30, 4, 6, 2); Door(r311, 'W', 0);

            var wc = Add("Washrooms", RoomType.Washroom, 0, 7, 6, 4); Door(wc, 'E', 1);
            // Subdivided West Utility
            var lau = Add("Laundry", RoomType.Laundry, 0, 11, 6, 3); Door(lau, 'E', 1);
            var dry = Add("Linen Storage", RoomType.Storage, 0, 14, 6, 3); Door(dry, 'E', 1);

            // Subdivided Center Lounge into Intimate Hotel Rooms and Lounge
            var r325 = Add("Room 325", RoomType.HotelRoom, 7, 7, 5, 5); Door(r325, 'N', 2);
            var r326 = Add("Room 326", RoomType.HotelRoom, 12, 7, 5, 5); Door(r326, 'N', 2); Door(r326, 'E', 2);
            var lng = Add("Hotel lounge", RoomType.Lounge, 7, 12, 5, 5); Door(lng, 'S', 2); Door(lng, 'E', 2);
            var cigar = Add("Cigar Room", RoomType.Lounge, 12, 12, 5, 5); Door(cigar, 'S', 2); Door(cigar, 'E', 2);

            var a = Add("Room 312", RoomType.HotelRoom, 18, 7, 4, 5); Door(a, 'N', 1);
            var b = Add("Room 313", RoomType.HotelRoom, 22, 7, 4, 5); Door(b, 'N', 1);
            var c = Add("Room 314", RoomType.HotelRoom, 26, 7, 3, 5); Door(c, 'E', 2);
            var d = Add("Room 315", RoomType.HotelRoom, 18, 12, 4, 5); Door(d, 'S', 1);
            var e = Add("Room 316", RoomType.HotelRoom, 22, 12, 4, 5); Door(e, 'S', 1);
            var g = Add("Room 317", RoomType.HotelRoom, 26, 12, 3, 5); Door(g, 'E', 2);

            var hk = Add("Housekeeping", RoomType.Housekeeping, 30, 7, 6, 5); Door(hk, 'W', 2);
            var r318 = Add("Room 318", RoomType.HotelRoom, 30, 12, 6, 5); Door(r318, 'W', 2);

            var r319 = Add("Room 319", RoomType.HotelRoom, 0, 18, 6, 3); Door(r319, 'E', 1);
            var ice = Add("Ice & vending", RoomType.Vending, 0, 21, 6, 3); Door(ice, 'E', 1);
            string[] s3 = { "Room 320", "Room 321", "Room 322", "Room 323", "Room 324" };
            for (int i = 0; i < 5; i++) { var r = Add(s3[i], RoomType.HotelRoom, 7 + i * 4, 18, 4, 6); Door(r, 'N', 1); }
            var svc = Add("Service closet", RoomType.Janitor, 27, 18, 2, 6); Door(svc, 'N', 0);

            var up = StairZone(Stairs[1]); Door(up, 'N', 3);        // shaft from floor 2, landing col 34
            var sl = Add("Stair lobby", RoomType.StairLobby, 30, 18, 6, 2); Door(sl, 'N', 2); Door(sl, 'W', 0);
            var swc = Add("Staff washroom", RoomType.Washroom, 30, 22, 6, 2); Door(swc, 'W', 0);
        }

        // ================= Floor 2: school wing (+ secret vault) =================
        static void School()
        {
            P.theme = "School";
            var st = StairZone(Stairs[0]);                          // shaft from floor 1, landing col 1
            var lob = Add("Stair lobby", RoomType.StairLobby, 0, 4, 6, 2); Door(lob, 'N', 1); Door(lob, 'E', 0);
            var sup = Add("Supply closet", RoomType.Storage, 0, 0, 6, 2); Door(sup, 'E', 1);
            var jan = Add("Janitor's closet", RoomType.Janitor, 5, 2, 1, 2); Door(jan, 'E', 0);

            string[] cn = { "Classroom 2A", "Classroom 2B", "Classroom 2C", "Classroom 2D" };
            int[] cx = { 7, 12, 17, 23 }, cw = { 5, 5, 6, 6 };
            for (int i = 0; i < 4; i++) { var r = Add(cn[i], RoomType.Classroom, cx[i], 0, cw[i], 6); Door(r, 'S', 2); if (i == 3) Door(r, 'E', 2); }
            var staff = Add("Staff room", RoomType.StaffRoom, 30, 0, 6, 6); Door(staff, 'W', 2); Door(staff, 'S', 2);
            Door(P.Find("Classroom 2B"), 'N', 0);
            Door(P.Find("Classroom 2D"), 'N', 5);

            var bw = Add("Boys' washroom", RoomType.Washroom, 0, 7, 6, 4); Door(bw, 'E', 1);
            var gw = Add("Girls' washroom", RoomType.Washroom, 0, 11, 6, 4); Door(gw, 'E', 1);
            var nur = Add("Nurse's office", RoomType.Nurse, 0, 15, 6, 2); Door(nur, 'E', 0);

            // Subdivided Science & Art Labs
            var lab = Add("Science lab", RoomType.Lab, 7, 7, 5, 5); Door(lab, 'N', 2); Door(lab, 'E', 2);
            var bio = Add("Biology Lab", RoomType.Lab, 12, 7, 5, 5); Door(bio, 'N', 2); Door(bio, 'E', 2);
            var art = Add("Art room", RoomType.ArtRoom, 7, 12, 5, 5); Door(art, 'S', 2); Door(art, 'E', 2);
            var dark = Add("Photo Darkroom", RoomType.Storage, 12, 12, 5, 5); Door(dark, 'S', 2); Door(dark, 'E', 2);

            var e1 = Add("Classroom 2E", RoomType.Classroom, 18, 7, 6, 5); Door(e1, 'N', 2);
            var e2 = Add("Classroom 2F", RoomType.Classroom, 24, 7, 5, 5); Door(e2, 'N', 2);
            var e3 = Add("Classroom 2G", RoomType.Classroom, 18, 12, 6, 5); Door(e3, 'S', 2);
            var e4 = Add("Classroom 2H", RoomType.Classroom, 24, 12, 5, 5); Door(e4, 'S', 2);

            // Subdivided Library
            var lib = Add("Library", RoomType.Library, 30, 7, 6, 5); Door(lib, 'W', 2);
            var arch = Add("Archives Room", RoomType.Storage, 30, 12, 6, 5); Door(arch, 'W', 2);
            P.bookcaseRow = 11;

            var mus = Add("Music room", RoomType.Music, 0, 18, 6, 6); Door(mus, 'E', 2);
            var comp = Add("Computer lab", RoomType.ComputerLab, 7, 18, 6, 6); Door(comp, 'N', 2);
            var cj = Add("Classroom 2J", RoomType.Classroom, 13, 18, 6, 6); Door(cj, 'N', 2);
            var prin = Add("Principal's office", RoomType.Principal, 19, 18, 4, 6); Door(prin, 'N', 1);
            var ck = Add("Classroom 2K", RoomType.Classroom, 23, 18, 6, 6); Door(ck, 'N', 2);

            var dn = StairZone(Stairs[1]); Door(dn, 'W', 0);
            var sr = Add("Store room", RoomType.Storage, 30, 18, 6, 2); Door(sr, 'N', 2);
            var lr = Add("Locker room", RoomType.LockerRoom, 30, 22, 6, 2); Door(lr, 'W', 0);

            Pit(12, 6); Pit(23, 17); Pit(29, 12); Pit(17, 11);
        }

        static void Pit(int col, int row)
        {
            P.pit[row, col] = true;
            P.pits.Add(new Vector2Int(col, row));
        }

        // ================= Floor 1: ground floor (escape) =================
        static void Ground()
        {
            P.theme = "Ground";
            var st = StairZone(Stairs[0]); Door(st, 'E', 0);
            var mail = Add("Mail room", RoomType.Mail, 0, 0, 6, 2); Door(mail, 'E', 1);
            var sec = Add("Security office", RoomType.Security, 0, 4, 6, 2); Door(sec, 'E', 0);

            // Subdivided Lobby
            var lobby = Add("Main lobby", RoomType.Lobby, 7, 0, 8, 6); Door(lobby, 'S', 3); Door(lobby, 'W', 2);
            var info = Add("Information Desk", RoomType.Waiting, 15, 0, 4, 6); Door(info, 'S', 2);
            var hold = Add("Holding Cell", RoomType.Security, 19, 0, 4, 6); Door(hold, 'S', 2);
            var rcp = Add("Reception office", RoomType.Office, 23, 0, 6, 6); Door(rcp, 'S', 2); Door(rcp, 'E', 2);

            var hall = Add("Service hallway", RoomType.ServiceHall, 30, 0, 6, 2); Door(hall, 'W', 1);
            var mnt = Add("Maintenance room", RoomType.Maintenance, 30, 2, 6, 4); Door(mnt, 'W', 1);

            var wc = Add("Washrooms", RoomType.Washroom, 0, 7, 6, 4); Door(wc, 'E', 1);
            var slr = Add("Staff locker room", RoomType.LockerRoom, 0, 11, 6, 6); Door(slr, 'E', 2);

            // Subdivided Cafeteria & Kitchen
            var caf = Add("Cafeteria", RoomType.Cafeteria, 7, 7, 10, 5); Door(caf, 'N', 3); Door(caf, 'E', 2);
            var prep = Add("Food Prep", RoomType.Kitchen, 7, 12, 5, 5); Door(prep, 'S', 2);
            var cooler = Add("Cold Storage", RoomType.Storage, 12, 12, 5, 5); Door(cooler, 'S', 2); Door(cooler, 'E', 2);

            var kit = Add("Kitchen", RoomType.Kitchen, 18, 7, 6, 5); Door(kit, 'W', 2); Door(kit, 'N', 2);
            var dish = Add("Dishwashing", RoomType.Maintenance, 18, 12, 6, 5); Door(dish, 'W', 2); Door(dish, 'S', 2);

            var boil = Add("Boiler room", RoomType.Boiler, 24, 7, 5, 5); Door(boil, 'E', 2); Door(boil, 'N', 2);
            var pump = Add("Water Pump Station", RoomType.Maintenance, 24, 12, 5, 5); Door(pump, 'E', 2); Door(pump, 'S', 2);

            var load = Add("Loading dock", RoomType.Loading, 30, 7, 6, 10); Door(load, 'W', 2); Door(load, 'W', 7);

            var gen = Add("Generator room", RoomType.Generator, 0, 18, 6, 6); Door(gen, 'E', 2);
            // Subdivided Atrium & Waiting
            var atr = Add("Atrium", RoomType.Atrium, 7, 18, 5, 6); Door(atr, 'N', 2);
            var gal = Add("Sculpture Gallery", RoomType.Lounge, 12, 18, 5, 6); Door(gal, 'N', 2);
            var shop = Add("Gift shop", RoomType.Shop, 17, 18, 4, 6); Door(shop, 'N', 1);
            var wait = Add("Waiting room", RoomType.Waiting, 21, 18, 4, 6); Door(wait, 'N', 2);
            var con = Add("Consultation Room", RoomType.Waiting, 25, 18, 4, 6); Door(con, 'N', 2);
            var park = Add("Parking access", RoomType.Parking, 30, 18, 6, 6); Door(park, 'W', 2); Door(park, 'N', 2);

            // Doorways in the south outer wall into the annex:
            Door(gen, 'S', 2);
            Door(atr, 'S', 2);
            Door(wait, 'S', 2);
            Door(park, 'S', 4);

            // Spike pits dug through the floor of the corridors.
            Pit(21, 6); Pit(13, 17); Pit(6, 15); Pit(17, 14);
        }

        // ================= sanity checks =================
        static void Validate(FloorPlan p)
        {
            // Every room reaches the corridor network through doors (BFS over open edges).
            var seen = new HashSet<int>();
            var q = new Queue<int>();
            if (p.corridor == null) return;
            seen.Add(p.corridor.id); q.Enqueue(p.corridor.id);
            while (q.Count > 0)
            {
                int id = q.Dequeue();
                foreach (var cell in p.rooms[id].cells)
                {
                    int c = cell.x, r = cell.y;
                    TryVisit(p, c + 1, r, EdgeE(r, c), seen, q);
                    TryVisit(p, c - 1, r, EdgeE(r, c - 1), seen, q);
                    TryVisit(p, c, r + 1, EdgeS(r, c), seen, q);
                    TryVisit(p, c, r - 1, EdgeS(r - 1, c), seen, q);
                }
            }
            foreach (var room in p.rooms)
                if (!seen.Contains(room.id) && room.type != RoomType.Elevator)
                    Debug.LogWarning("Hollow Tower layout: floor " + (p.floor + 1) + " '" + room.name + "' is not reachable");
        }

        static void TryVisit(FloorPlan p, int c, int r, int edge, HashSet<int> seen, Queue<int> q)
        {
            if (c < 0 || r < 0 || c >= Cols || r >= Rows) return;
            int z = p.zone[r, c];
            if (z < 0 || seen.Contains(z)) return;
            if (!p.doors.Contains(edge)) return;
            seen.Add(z);
            q.Enqueue(z);
        }
    }
}
