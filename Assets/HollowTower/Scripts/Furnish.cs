using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>Shared HDRP materials for the building (made once per level build).</summary>
    public class Mats
    {
        public Material concrete, officeCarpet, hotelCarpet, schoolLino, marble, washTile, wood, woodFloor, serverFloor, cafeteriaTile;
        public Material officeWall, hotelWall, schoolWall, lobbyWall, washWall, concreteWall, libraryWall, ceiling, ceilingPlaster;
        public Material darkWood, metal, steel, rust, plasticGrey, plasticBlack, fabricGreen, fabricRed, fabricBlue, leather,
                        sheet, porcelain, mirror, glass, screen, blackboard, whiteboard, paper, cardboard, rubber, brass, gold, stone;
        public Material[] books;
        public Material fixtureCool, fixtureWarm, redLamp, fire, led, blood, grime, elevator, locker, trim;

        public Mats()
        {
            var grime = Gfx.GrimeTexture(11, new Color(0.42f, 0.41f, 0.39f), new Color(0.16f, 0.15f, 0.14f), 6f, 0.35f, false);
            concrete = Gfx.LitWorld(Color.white, 0.08f, 0f, grime, 3f);
            concreteWall = Gfx.LitWorld(new Color(0.85f, 0.85f, 0.83f), 0.1f, 0f, grime, 3f);
            officeCarpet = Gfx.LitWorld(Color.white, 0.05f, 0f,
                Gfx.GrimeTexture(37, new Color(0.27f, 0.29f, 0.33f), new Color(0.11f, 0.11f, 0.12f), 9f, 0.45f, true, 1.5f), 2.4f);
            hotelCarpet = Gfx.LitWorld(Color.white, 0.05f, 0f,
                Gfx.PatternTexture(5, new Color(0.36f, 0.06f, 0.07f), new Color(0.22f, 0.04f, 0.05f), 6, false, 0.6f), 1.2f);
            schoolLino = Gfx.LitWorld(Color.white, 0.45f, 0f,
                Gfx.GrimeTexture(43, new Color(0.47f, 0.52f, 0.46f), new Color(0.22f, 0.24f, 0.21f), 7f, 0.35f, true, 1.2f), 2.4f);
            marble = Gfx.LitWorld(Color.white, 0.75f, 0f,
                Gfx.GrimeTexture(57, new Color(0.78f, 0.74f, 0.66f), new Color(0.48f, 0.44f, 0.38f), 3f, 0.25f, true, 1f), 3f);
            washTile = Gfx.LitWorld(Color.white, 0.8f, 0f,
                Gfx.GrimeTexture(63, new Color(0.82f, 0.84f, 0.84f), new Color(0.45f, 0.47f, 0.46f), 5f, 0.4f, true, 2f), 1.2f);
            cafeteriaTile = Gfx.LitWorld(Color.white, 0.55f, 0f,
                Gfx.GrimeTexture(67, new Color(0.62f, 0.55f, 0.45f), new Color(0.28f, 0.24f, 0.2f), 6f, 0.35f, true, 1.5f), 1.8f);
            serverFloor = Gfx.LitWorld(Color.white, 0.4f, 0.3f,
                Gfx.GrimeTexture(71, new Color(0.5f, 0.52f, 0.55f), new Color(0.25f, 0.26f, 0.28f), 8f, 0.2f, true, 2f), 2.4f);
            var woodTex = Gfx.PatternTexture(77, new Color(0.42f, 0.27f, 0.15f), new Color(0.3f, 0.18f, 0.1f), 8, true, 0.5f);
            woodFloor = Gfx.LitWorld(Color.white, 0.35f, 0f, woodTex, 2.5f);
            wood = Gfx.LitWorld(new Color(0.85f, 0.8f, 0.75f), 0.3f, 0f, woodTex, 1.5f);

            var paint = Gfx.GrimeTexture(23, new Color(0.62f, 0.62f, 0.58f), new Color(0.28f, 0.27f, 0.24f), 5f, 0.45f, false, 2f);
            officeWall = Gfx.LitWorld(Color.white, 0.12f, 0f, paint, 3f);
            hotelWall = Gfx.LitWorld(Color.white, 0.15f, 0f,
                Gfx.PatternTexture(83, new Color(0.62f, 0.55f, 0.42f), new Color(0.52f, 0.45f, 0.34f), 10, false, 0.35f), 1.5f);
            schoolWall = Gfx.LitWorld(new Color(0.75f, 0.9f, 0.78f), 0.15f, 0f, paint, 3f);
            lobbyWall = Gfx.LitWorld(Color.white, 0.4f, 0f,
                Gfx.GrimeTexture(91, new Color(0.7f, 0.64f, 0.55f), new Color(0.4f, 0.35f, 0.3f), 2f, 0.3f, true, 1f), 4f);
            washWall = washTile;
            libraryWall = Gfx.LitWorld(new Color(0.75f, 0.68f, 0.6f), 0.25f, 0f, woodTex, 2f);
            ceiling = Gfx.LitWorld(Color.white, 0.05f, 0f,
                Gfx.GrimeTexture(71, new Color(0.62f, 0.61f, 0.57f), new Color(0.3f, 0.29f, 0.27f), 5f, 0.5f, true, 2f), 2.4f);
            ceilingPlaster = Gfx.LitWorld(new Color(0.8f, 0.78f, 0.74f), 0.05f, 0f, paint, 4f);

            darkWood = Gfx.LitWorld(new Color(0.45f, 0.33f, 0.25f), 0.35f, 0f, woodTex, 1f);
            metal = Gfx.Lit(new Color(0.32f, 0.33f, 0.35f), 0.55f, 0.8f);
            steel = Gfx.Lit(new Color(0.6f, 0.62f, 0.64f), 0.7f, 1f);
            rust = Gfx.LitWorld(new Color(0.55f, 0.38f, 0.28f), 0.2f, 0.4f, grime, 1f);
            plasticGrey = Gfx.Lit(new Color(0.55f, 0.55f, 0.53f), 0.35f);
            plasticBlack = Gfx.Lit(new Color(0.05f, 0.05f, 0.055f), 0.45f);
            fabricGreen = Gfx.Lit(new Color(0.12f, 0.22f, 0.18f), 0.05f);
            fabricRed = Gfx.Lit(new Color(0.35f, 0.07f, 0.06f), 0.05f);
            fabricBlue = Gfx.Lit(new Color(0.12f, 0.15f, 0.25f), 0.05f);
            leather = Gfx.Lit(new Color(0.18f, 0.1f, 0.06f), 0.5f);
            sheet = Gfx.Lit(new Color(0.86f, 0.84f, 0.79f), 0.05f);
            porcelain = Gfx.Lit(new Color(0.85f, 0.86f, 0.84f), 0.85f);
            mirror = Gfx.Lit(new Color(0.8f, 0.82f, 0.85f), 0.98f, 1f);
            glass = Gfx.Transparent(new Color(0.6f, 0.7f, 0.75f, 0.18f), Color.black);
            screen = Gfx.Emissive(new Color(0.015f, 0.02f, 0.025f), new Color(0.0f, 0.006f, 0.01f));   // dead screens, faint glow
            blackboard = Gfx.Lit(new Color(0.06f, 0.12f, 0.09f), 0.15f);
            whiteboard = Gfx.Lit(new Color(0.85f, 0.86f, 0.86f), 0.8f);
            paper = Gfx.Lit(new Color(0.78f, 0.75f, 0.66f), 0.1f);
            cardboard = Gfx.Lit(new Color(0.45f, 0.33f, 0.2f), 0.1f);
            rubber = Gfx.Lit(new Color(0.08f, 0.08f, 0.08f), 0.2f);
            brass = Gfx.Lit(new Color(0.7f, 0.55f, 0.25f), 0.7f, 1f);
            gold = Gfx.Lit(new Color(0.9f, 0.7f, 0.3f), 0.8f, 1f);
            stone = Gfx.LitWorld(new Color(0.75f, 0.7f, 0.6f), 0.2f, 0f, grime, 1.5f);
            books = new Material[6];
            Color[] bc = { new Color(0.4f, 0.08f, 0.06f), new Color(0.08f, 0.18f, 0.3f), new Color(0.12f, 0.25f, 0.12f),
                           new Color(0.45f, 0.35f, 0.15f), new Color(0.2f, 0.2f, 0.22f), new Color(0.35f, 0.25f, 0.2f) };
            for (int i = 0; i < books.Length; i++) books[i] = Gfx.Lit(bc[i], 0.25f);

            fixtureCool = Gfx.Emissive(new Color(0.85f, 0.88f, 0.9f), new Color(9f, 9.5f, 10f));
            fixtureWarm = Gfx.Emissive(new Color(0.9f, 0.8f, 0.6f), new Color(10f, 7.5f, 4.5f));
            redLamp = Gfx.Emissive(new Color(0.5f, 0f, 0f), new Color(12f, 0.4f, 0.2f));
            fire = Gfx.Emissive(new Color(1f, 0.4f, 0f), new Color(25f, 9f, 1.5f));
            led = Gfx.Emissive(new Color(0.1f, 0.4f, 0.1f), new Color(0.2f, 3f, 0.4f));
            blood = Gfx.Lit(new Color(0.16f, 0.0f, 0.0f), 0.8f);
            this.grime = Gfx.Lit(new Color(0.03f, 0.03f, 0.025f), 0.0f);
            elevator = Gfx.Lit(new Color(0.55f, 0.55f, 0.6f), 0.8f, 1f);
            locker = Gfx.LitWorld(new Color(0.55f, 0.75f, 0.75f), 0.45f, 0.6f,
                Gfx.GrimeTexture(61, new Color(0.45f, 0.5f, 0.5f), new Color(0.25f, 0.26f, 0.25f), 8f, 0.3f, false), 1.5f);
            trim = Gfx.Lit(new Color(0.22f, 0.2f, 0.18f), 0.4f);
        }

        public Material FloorFor(Room r)
        {
            switch (r.type)
            {
                case RoomType.Washroom: case RoomType.Kitchen: case RoomType.Nurse: case RoomType.Laundry: return washTile;
                case RoomType.Server: return serverFloor;
                case RoomType.Library: case RoomType.Principal: case RoomType.Music: case RoomType.Suite: return woodFloor;
                case RoomType.Boiler: case RoomType.Generator: case RoomType.Loading: case RoomType.Parking: case RoomType.Maintenance:
                case RoomType.ServiceHall: case RoomType.Storage: case RoomType.Janitor: case RoomType.Stair: case RoomType.Elevator: return concrete;
                case RoomType.Lobby: case RoomType.Atrium: case RoomType.Waiting: case RoomType.Shop: return marble;
                case RoomType.Cafeteria: return cafeteriaTile;
            }
            switch (r.floor)
            {
                case 3: return officeCarpet;
                case 2: return r.type == RoomType.Linen || r.type == RoomType.Housekeeping || r.type == RoomType.Vending ? cafeteriaTile : hotelCarpet;
                case 1: return schoolLino;
                default: return r.type == RoomType.Corridor ? marble : concrete;
            }
        }

        public Material WallFor(Room r)
        {
            switch (r.type)
            {
                case RoomType.Washroom: case RoomType.Kitchen: case RoomType.Nurse: return washWall;
                case RoomType.Boiler: case RoomType.Generator: case RoomType.Loading: case RoomType.Parking: case RoomType.Maintenance:
                case RoomType.ServiceHall: case RoomType.Storage: case RoomType.Janitor: case RoomType.Stair: case RoomType.Elevator: case RoomType.Server: return concreteWall;
                case RoomType.Library: case RoomType.Principal: return libraryWall;
                case RoomType.Lobby: case RoomType.Atrium: case RoomType.Waiting: return lobbyWall;
            }
            switch (r.floor)
            {
                case 3: return officeWall;
                case 2: return hotelWall;
                case 1: return schoolWall;
                default: return officeWall;
            }
        }

        public Material CeilingFor(Room r)
        {
            if (r.floor == 2 || r.type == RoomType.Lobby || r.type == RoomType.Library) return ceilingPlaster;
            if (r.type == RoomType.Boiler || r.type == RoomType.Parking || r.type == RoomType.Loading || r.type == RoomType.Stair) return concrete;
            return ceiling;
        }
    }

    /// <summary>Furniture and fittings for each room type, built from primitives (merged into a few meshes afterwards).</summary>
    public class Furnish
    {
        readonly Mats M;
        readonly LevelInfo info;
        Transform root;
        System.Random rnd;
        readonly List<Vector4> keepOut = new List<Vector4>(); // x,z,radius, y
        float x0, x1, z0, z1, y;    // room interior (world)

        const float T = 0.14f;      // wall half-thickness margin

        public Furnish(Mats mats, LevelInfo levelInfo) { M = mats; info = levelInfo; }

        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        bool Chance(float p) { return rnd.NextDouble() < p; }

        GameObject B(string n, Vector3 pos, Vector3 size, Material m, bool col = true, float yaw = 0f)
        {
            var g = Gfx.Box(n, root, pos, size, m, col);
            if (yaw != 0f) g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return g;
        }

        GameObject Cyl(string n, Vector3 pos, Vector3 size, Material m, bool col = true)
        {
            return Gfx.Prim(PrimitiveType.Cylinder, n, root, pos, size, m, col);
        }

        bool Free(float x, float z, float rad)
        {
            foreach (var k in keepOut)
                if (Mathf.Abs(k.w - y) < 1f && (new Vector2(k.x - x, k.y - z)).magnitude < k.z + rad) return false;
            return true;
        }

        void Reserve(float x, float z, float rad) { keepOut.Add(new Vector4(x, z, rad, y)); }

        /// <summary>Dress one room. <paramref name="parent"/> should be a fresh per-room transform.</summary>
        public void Room(Room room, Transform parent, List<Vector3> doorPoints)
        {
            root = parent;
            rnd = new System.Random(9000 + room.floor * 1000 + room.id * 37);
            keepOut.Clear();
            var r = room.rect;
            x0 = r.x * LevelLayout.Cell + T; x1 = (r.x + r.width) * LevelLayout.Cell - T;
            z1 = (LevelLayout.Rows - r.y) * LevelLayout.Cell - T; z0 = (LevelLayout.Rows - r.y - r.height) * LevelLayout.Cell + T;
            y = room.floor * LevelLayout.Story;
            foreach (var d in doorPoints) Reserve(d.x, d.z, 1.6f);

            switch (room.type)
            {
                case RoomType.OpenOffice: OpenOffice(room); break;
                case RoomType.Office: Office(); break;
                case RoomType.Meeting: Meeting(); break;
                case RoomType.Server: Server(); break;
                case RoomType.Records: case RoomType.Copy: Records(room.type == RoomType.Copy); break;
                case RoomType.BreakRoom: BreakRoom(); break;
                case RoomType.Washroom: Washroom(); break;
                case RoomType.Storage: case RoomType.Janitor: case RoomType.Linen: case RoomType.Housekeeping: case RoomType.Mail: Storage(room.type); break;
                case RoomType.HotelRoom: case RoomType.Suite: HotelRoom(room.type == RoomType.Suite); break;
                case RoomType.Laundry: Laundry(); break;
                case RoomType.Lounge: case RoomType.HotelWing: Lounge(); break;
                case RoomType.Vending: Vending(); break;
                case RoomType.Classroom: case RoomType.ComputerLab: Classroom(room.type == RoomType.ComputerLab); break;
                case RoomType.Lab: case RoomType.ArtRoom: Lab(room.type == RoomType.ArtRoom); break;
                case RoomType.StaffRoom: StaffRoom(); break;
                case RoomType.Library: Library(); break;
                case RoomType.Nurse: Nurse(); break;
                case RoomType.Music: Music(); break;
                case RoomType.Principal: Office(); break;
                case RoomType.LockerRoom: LockerRoom(); break;
                case RoomType.Lobby: case RoomType.Atrium: case RoomType.Waiting: Lobby(room.type); break;
                case RoomType.Security: Security(); break;
                case RoomType.Cafeteria: Cafeteria(); break;
                case RoomType.Kitchen: Kitchen(); break;
                case RoomType.Boiler: case RoomType.Generator: Boiler(room.type == RoomType.Generator); break;
                case RoomType.Loading: case RoomType.Parking: case RoomType.Maintenance: Loading(); break;
                case RoomType.Shop: Shop(); break;
            }
            Debris(room);
        }

        // ---------------- helpers ----------------

        void Desk(float x, float z, float yaw, Material top, bool monitor, bool chair)
        {
            if (!Free(x, z, 0.9f)) return;
            Reserve(x, z, 0.9f);
            var g = new GameObject("Desk").transform;
            g.SetParent(root, false);
            g.localPosition = new Vector3(x, y, z);
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var old = root; root = g;
            B("Top", new Vector3(0f, 0.74f, 0f), new Vector3(1.5f, 0.05f, 0.75f), top);
            B("Leg", new Vector3(-0.7f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.7f), M.metal, false);
            B("Leg", new Vector3(0.7f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.7f), M.metal, false);
            B("Modesty", new Vector3(0f, 0.45f, 0.33f), new Vector3(1.35f, 0.5f, 0.03f), top, false);
            if (monitor)
            {
                bool fallen = Chance(0.2f);
                var mon = B("Monitor", new Vector3(R(-0.3f, 0.3f), fallen ? 0.82f : 1.0f, 0.15f), new Vector3(0.55f, 0.36f, 0.05f), M.plasticBlack, false);
                if (fallen) mon.transform.localRotation = Quaternion.Euler(80f, R(-30f, 30f), 0f);
                else
                {
                    B("Screen", new Vector3(mon.transform.localPosition.x, 1.0f, 0.123f), new Vector3(0.5f, 0.31f, 0.005f), M.screen, false);
                    B("Stand", new Vector3(mon.transform.localPosition.x, 0.8f, 0.2f), new Vector3(0.06f, 0.12f, 0.06f), M.plasticBlack, false);
                }
                B("Keyboard", new Vector3(R(-0.2f, 0.2f), 0.775f, -0.12f), new Vector3(0.42f, 0.02f, 0.14f), M.plasticGrey, false);
            }
            if (Chance(0.5f)) B("Papers", new Vector3(R(-0.5f, 0.5f), 0.772f, R(-0.2f, 0.2f)), new Vector3(0.21f, 0.015f, 0.29f), M.paper, false, R(0f, 90f));
            root = old;
            if (chair) Chair(g.TransformPoint(new Vector3(R(-0.2f, 0.2f), 0f, -0.75f)), yaw + R(-40f, 40f), Chance(0.15f));
        }

        void Chair(Vector3 pos, float yaw, bool toppled)
        {
            var g = new GameObject("Chair").transform;
            g.SetParent(root, false);
            g.localPosition = pos;
            g.localRotation = toppled ? Quaternion.Euler(-85f, yaw, 0f) * Quaternion.identity : Quaternion.Euler(0f, yaw, 0f);
            if (toppled) g.localPosition += Vector3.up * 0.25f;
            var old = root; root = g;
            B("Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.48f, 0.08f, 0.46f), M.fabricBlue, false);
            B("Back", new Vector3(0f, 0.78f, -0.22f), new Vector3(0.46f, 0.55f, 0.06f), M.fabricBlue, false);
            Cyl("Post", new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.2f, 0.05f), M.metal, false);
            B("Base", new Vector3(0f, 0.04f, 0f), new Vector3(0.55f, 0.04f, 0.08f), M.plasticBlack, false);
            B("Base", new Vector3(0f, 0.04f, 0f), new Vector3(0.08f, 0.04f, 0.55f), M.plasticBlack, false);
            root = old;
        }

        void Shelf(float x, float z, float yaw, float w, float h, float d, Material frame, Material[] fill, float fillChance)
        {
            if (!Free(x, z, w * 0.5f)) return;
            Reserve(x, z, w * 0.45f);
            var g = new GameObject("Shelf").transform;
            g.SetParent(root, false);
            g.localPosition = new Vector3(x, y, z);
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var old = root; root = g;
            B("Back", new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, 0.03f), frame);
            B("SideL", new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(0.04f, h, d), frame, false);
            B("SideR", new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(0.04f, h, d), frame, false);
            int levels = Mathf.Max(2, Mathf.RoundToInt(h / 0.42f));
            for (int i = 0; i <= levels; i++)
            {
                float sy = 0.05f + i * (h - 0.08f) / levels;
                B("Board", new Vector3(0f, sy, 0f), new Vector3(w, 0.03f, d), frame, false);
                if (i == levels || fill == null) continue;
                float cx = -w * 0.5f + 0.05f;
                while (cx < w * 0.5f - 0.1f)
                {
                    float bw = R(0.04f, 0.09f) * (fill.Length > 1 ? 1f : 4f);
                    if (Chance(fillChance))
                    {
                        float bh = R(0.22f, 0.32f);
                        var bk = B("Item", new Vector3(cx + bw * 0.5f, sy + bh * 0.5f + 0.015f, 0f), new Vector3(bw * 0.9f, bh, d * 0.75f), fill[rnd.Next(fill.Length)], false);
                        if (Chance(0.08f)) bk.transform.localRotation = Quaternion.Euler(0f, 0f, R(10f, 30f));
                    }
                    cx += bw;
                }
            }
            root = old;
        }

        /// <summary>Things standing along a wall: returns positions spaced along the four interior walls.</summary>
        IEnumerable<Vector4> AlongWalls(float spacing, float inset)
        {
            for (float x = x0 + spacing * 0.5f; x < x1 - spacing * 0.4f; x += spacing)
            {
                yield return new Vector4(x, z1 - inset, 180f, 0f);  // north wall, facing south
                yield return new Vector4(x, z0 + inset, 0f, 0f);    // south wall, facing north
            }
            for (float z = z0 + spacing * 0.5f; z < z1 - spacing * 0.4f; z += spacing)
            {
                yield return new Vector4(x0 + inset, z, 90f, 0f);   // west wall, facing east
                yield return new Vector4(x1 - inset, z, -90f, 0f);  // east wall, facing west
            }
        }

        void Hide(HidingSpot.Kind kind, float x, float z, float yaw)
        {
            if (!Free(x, z, 0.6f)) return;
            Reserve(x, z, 0.8f);
            HidingSpot.Create(kind, root, new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, M);
        }

        // ---------------- floor 4: offices ----------------

        void OpenOffice(Room room)
        {
            // Rows of desk pods with low partitions.
            float w = x1 - x0, d = z1 - z0;
            bool alongX = w >= d;
            for (float a = 3f; a < (alongX ? w : d) - 2f; a += 3.4f)
                for (float b = 2.6f; b < (alongX ? d : w) - 2f; b += 4.2f)
                {
                    float x = alongX ? x0 + a : x0 + b, z = alongX ? z1 - b : z1 - a;
                    if (!Free(x, z, 1.6f)) continue;
                    B("Partition", new Vector3(x, y + 0.6f, z), alongX ? new Vector3(3.1f, 1.2f, 0.05f) : new Vector3(0.05f, 1.2f, 3.1f), M.fabricGreen);
                    Desk(alongX ? x - 0.78f : x - 0.45f, alongX ? z + 0.45f : z - 0.78f, alongX ? 180f : 90f, M.wood, Chance(0.8f), true);
                    Desk(alongX ? x + 0.78f : x + 0.45f, alongX ? z - 0.45f : z + 0.78f, alongX ? 0f : -90f, M.wood, Chance(0.8f), true);
                    Reserve(x, z, 1.7f);
                }
            foreach (var p in AlongWalls(4f, 0.35f))
                if (Chance(0.35f)) Shelf(p.x, p.y, p.z, 0.9f, 1.3f, 0.45f, M.metal, new[] { M.paper, M.cardboard }, 0.6f);
        }

        void Office()
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            Desk(cx, cz + 0.5f, 180f, M.darkWood, true, true);
            Chair(new Vector3(cx + R(-0.4f, 0.4f), y, cz - 0.9f), 180f + R(-30f, 30f), Chance(0.2f));
            int n = 0;
            foreach (var p in AlongWalls(1.1f, 0.3f))
            {
                if (n > 6 || !Chance(0.4f)) continue;
                n++;
                if (Chance(0.5f)) { B("Cabinet", new Vector3(p.x, y + 0.7f, p.y), new Vector3(0.5f, 1.4f, 0.6f), M.metal, true, p.z); Reserve(p.x, p.y, 0.5f); }
                else Shelf(p.x, p.y, p.z, 1f, 1.8f, 0.35f, M.darkWood, M.books, 0.7f);
            }
            if (Chance(0.6f)) Plant(x0 + 0.5f, z1 - 0.5f);
        }

        void Meeting()
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            float len = Mathf.Min(Mathf.Max(x1 - x0, z1 - z0) - 3f, 8f);
            bool alongX = (x1 - x0) >= (z1 - z0);
            B("Table", new Vector3(cx, y + 0.75f, cz), alongX ? new Vector3(len, 0.06f, 1.4f) : new Vector3(1.4f, 0.06f, len), M.darkWood);
            B("TableBase", new Vector3(cx, y + 0.37f, cz), alongX ? new Vector3(len - 0.6f, 0.72f, 0.5f) : new Vector3(0.5f, 0.72f, len - 0.6f), M.darkWood, false);
            Reserve(cx, cz, 1.5f);
            for (float a = -len * 0.5f + 0.6f; a < len * 0.5f - 0.4f; a += 0.9f)
            {
                if (alongX)
                {
                    Chair(new Vector3(cx + a, y, cz - 1.05f), 0f + R(-20f, 20f), Chance(0.1f));
                    Chair(new Vector3(cx + a, y, cz + 1.05f), 180f + R(-20f, 20f), Chance(0.1f));
                }
                else
                {
                    Chair(new Vector3(cx - 1.05f, y, cz + a), 90f + R(-20f, 20f), Chance(0.1f));
                    Chair(new Vector3(cx + 1.05f, y, cz + a), -90f + R(-20f, 20f), Chance(0.1f));
                }
            }
            // Whiteboard and screen on the far walls.
            if (alongX) { B("Whiteboard", new Vector3(x0 + 0.03f, y + 1.5f, cz), new Vector3(0.03f, 1.1f, 2.2f), M.whiteboard, false); B("TV", new Vector3(x1 - 0.04f, y + 1.7f, cz), new Vector3(0.05f, 0.9f, 1.6f), M.screen, false); }
            else { B("Whiteboard", new Vector3(cx, y + 1.5f, z1 - 0.03f), new Vector3(2.2f, 1.1f, 0.03f), M.whiteboard, false); B("TV", new Vector3(cx, y + 1.7f, z0 + 0.04f), new Vector3(1.6f, 0.9f, 0.05f), M.screen, false); }
        }

        void Server()
        {
            for (float x = x0 + 1.4f; x < x1 - 1f; x += 1.9f)
                for (float z = z0 + 1.6f; z < z1 - 1.4f; z += 0.65f)
                {
                    if (!Free(x, z, 0.35f)) continue;
                    B("Rack", new Vector3(x, y + 1.0f, z), new Vector3(0.9f, 2.0f, 0.6f), M.plasticBlack);
                    for (int i = 0; i < 6; i++)
                        if (Chance(0.6f))
                            B("Led", new Vector3(x + 0.452f, y + 0.4f + i * 0.28f, z + R(-0.2f, 0.2f)), new Vector3(0.005f, 0.015f, 0.015f), Chance(0.8f) ? M.led : M.redLamp, false);
                }
            B("ACUnit", new Vector3(x1 - 0.5f, y + 1f, z1 - 0.8f), new Vector3(0.9f, 2f, 1.2f), M.plasticGrey);
            foreach (var p in AlongWalls(3f, 0.2f)) if (Chance(0.3f)) Cyl("Cable", new Vector3(p.x, y + 0.03f, p.y), new Vector3(0.4f, 0.02f, 0.4f), M.rubber, false);
        }

        void Records(bool copy)
        {
            for (float x = x0 + 1.2f; x < x1 - 1f; x += 1.6f)
                for (float z = z0 + 1.1f; z < z1 - 1f; z += 2.2f)
                    if (Chance(0.75f)) Shelf(x, z, 0f, 1.2f, 2.1f, 0.5f, M.metal, new[] { M.cardboard, M.paper }, 0.75f);
            if (copy)
            {
                B("Copier", new Vector3((x0 + x1) * 0.5f, y + 0.55f, z1 - 0.6f), new Vector3(1.0f, 1.1f, 0.7f), M.plasticGrey);
                B("CopierTop", new Vector3((x0 + x1) * 0.5f, y + 1.12f, z1 - 0.6f), new Vector3(0.9f, 0.05f, 0.6f), M.plasticBlack, false);
            }
            for (int i = 0; i < 10; i++)
                B("Paper", new Vector3(R(x0, x1), y + 0.01f, R(z0, z1)), new Vector3(0.21f, 0.005f, 0.29f), M.paper, false, R(0f, 360f));
        }

        void BreakRoom()
        {
            // Counter with sink along the north wall, tables in the middle.
            float len = Mathf.Min(x1 - x0 - 1f, 5f);
            float cx = (x0 + x1) * 0.5f;
            B("Counter", new Vector3(cx, y + 0.45f, z1 - 0.35f), new Vector3(len, 0.9f, 0.65f), M.wood);
            B("Worktop", new Vector3(cx, y + 0.92f, z1 - 0.35f), new Vector3(len + 0.04f, 0.04f, 0.68f), M.marble, false);
            B("Fridge", new Vector3(x0 + 0.5f, y + 0.95f, z1 - 0.45f), new Vector3(0.75f, 1.9f, 0.7f), M.porcelain);
            Reserve(cx, z1 - 0.35f, len * 0.5f);
            B("Microwave", new Vector3(cx + 0.6f, y + 1.08f, z1 - 0.4f), new Vector3(0.5f, 0.3f, 0.38f), M.plasticBlack, false);
            for (float x = x0 + 1.6f; x < x1 - 1.2f; x += 2.6f)
                for (float z = z0 + 1.6f; z < z1 - 2f; z += 2.6f)
                {
                    if (!Free(x, z, 1f)) continue;
                    Cyl("Table", new Vector3(x, y + 0.74f, z), new Vector3(1.0f, 0.02f, 1.0f), M.wood);
                    Cyl("Leg", new Vector3(x, y + 0.37f, z), new Vector3(0.08f, 0.37f, 0.08f), M.metal, false);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 120f + R(-20f, 20f);
                        var pos = new Vector3(x, y, z) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.85f;
                        Chair(pos, a + 180f, Chance(0.2f));
                    }
                    Reserve(x, z, 1.2f);
                }
        }

        // ---------------- shared: washrooms, storage ----------------

        void Washroom()
        {
            // Stalls along the longer wall (each one a hiding spot), sinks and mirrors opposite.
            bool alongX = (x1 - x0) >= (z1 - z0);
            int stalls = Mathf.Clamp(Mathf.FloorToInt(((alongX ? x1 - x0 : z1 - z0) - 2f) / 1.2f), 2, 6);
            float start = (alongX ? x0 : z0) + 1.3f;
            for (int i = 0; i < stalls; i++)
            {
                float a = start + i * 1.2f;
                if (alongX) Hide(HidingSpot.Kind.Stall, a, z1 - 0.95f, 180f);
                else Hide(HidingSpot.Kind.Stall, x0 + 0.95f, a, 90f);
            }
            for (int i = 0; i < Mathf.Min(stalls, 4); i++)
            {
                float a = start + 0.3f + i * 1.1f;
                Vector3 sink = alongX ? new Vector3(a, y + 0.82f, z0 + 0.32f) : new Vector3(x1 - 0.32f, y + 0.82f, a);
                if (!Free(sink.x, sink.z, 0.3f)) continue;
                B("Sink", sink, alongX ? new Vector3(0.55f, 0.15f, 0.45f) : new Vector3(0.45f, 0.15f, 0.55f), M.porcelain);
                B("Pipe", sink + Vector3.down * 0.4f, new Vector3(0.06f, 0.7f, 0.06f), M.steel, false);
                B("Mirror", alongX ? new Vector3(a, y + 1.55f, z0 + 0.02f) : new Vector3(x1 - 0.02f, y + 1.55f, a),
                    alongX ? new Vector3(0.6f, 0.8f, 0.01f) : new Vector3(0.01f, 0.8f, 0.6f), Chance(0.3f) ? M.grime : M.mirror, false);
            }
        }

        void Storage(RoomType t)
        {
            Material[] fill = t == RoomType.Linen || t == RoomType.Housekeeping ? new[] { M.sheet, M.paper } : new[] { M.cardboard, M.paper, M.plasticGrey };
            foreach (var p in AlongWalls(1.3f, 0.3f))
                if (Chance(0.6f)) Shelf(p.x, p.y, p.z, 1.2f, 2.0f, 0.5f, M.metal, fill, 0.7f);
            int boxes = (int)((x1 - x0) * (z1 - z0) / 4f);
            for (int i = 0; i < boxes; i++)
            {
                float x = R(x0 + 0.8f, x1 - 0.8f), z = R(z0 + 0.8f, z1 - 0.8f);
                if (!Free(x, z, 0.5f)) continue;
                float s = R(0.4f, 0.7f);
                B("Box", new Vector3(x, y + s * 0.5f, z), Vector3.one * s, M.cardboard, true, R(0f, 90f));
                if (Chance(0.4f)) B("Box", new Vector3(x, y + s + s * 0.35f, z), Vector3.one * s * 0.7f, M.cardboard, false, R(0f, 90f));
                Reserve(x, z, s * 0.7f);
            }
            if (t == RoomType.Linen || t == RoomType.Housekeeping)
            {
                float x = R(x0 + 1f, x1 - 1f), z = R(z0 + 1f, z1 - 1f);
                if (Free(x, z, 0.7f)) { B("Cart", new Vector3(x, y + 0.5f, z), new Vector3(1.0f, 0.8f, 0.55f), M.metal); B("Linen", new Vector3(x, y + 0.95f, z), new Vector3(0.9f, 0.15f, 0.5f), M.sheet, false); }
            }
            if (t == RoomType.Janitor) B("Bucket", new Vector3(x0 + 0.4f, y + 0.2f, z0 + 0.4f), new Vector3(0.35f, 0.4f, 0.35f), M.plasticGrey, false);
        }

        // ---------------- floor 3: hotel ----------------

        void HotelRoom(bool suite)
        {
            // Bed against the wall farthest from the door, nightstands, wardrobe (hiding spot), desk, armchair.
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            bool northFree = Free(cx, z1 - 1f, 1f);
            float bedYaw = northFree ? 180f : 0f;
            float bz = northFree ? z1 - 1.1f : z0 + 1.1f;
            int beds = suite ? 1 : (Chance(0.3f) && (x1 - x0) > 5f ? 2 : 1);
            for (int i = 0; i < beds; i++)
            {
                float bx = beds == 1 ? cx : cx + (i == 0 ? -1.3f : 1.3f);
                float bw = suite || beds == 1 ? 1.8f : 1.1f;
                var g = new GameObject("Bed").transform;
                g.SetParent(root, false); g.localPosition = new Vector3(bx, y, bz); g.localRotation = Quaternion.Euler(0f, bedYaw, 0f);
                var old = root; root = g;
                B("Frame", new Vector3(0f, 0.25f, 0f), new Vector3(bw, 0.5f, 2.1f), M.darkWood);
                B("Mattress", new Vector3(0f, 0.58f, 0.02f), new Vector3(bw - 0.05f, 0.18f, 2.0f), M.sheet, false);
                B("Duvet", new Vector3(0f, 0.68f, -0.3f), new Vector3(bw, 0.06f, 1.3f), Chance(0.25f) ? M.fabricRed : M.sheet, false, R(-6f, 6f));
                B("Pillow", new Vector3(0f, 0.74f, 0.8f), new Vector3(bw * 0.8f, 0.14f, 0.38f), M.sheet, false);
                B("Headboard", new Vector3(0f, 0.85f, 1.05f), new Vector3(bw + 0.1f, 1.1f, 0.08f), M.darkWood, false);
                root = old;
                if (Chance(0.35f)) Gfx.Decal(root, g.TransformPoint(new Vector3(R(-0.3f, 0.3f), 1.1f, R(-0.6f, 0.4f))), Vector3.down, R(0.5f, 0.9f), R(0.5f, 0.8f), Gfx.DecalKind.Blood, rnd.Next());
                Reserve(bx, bz, 1.3f);
                for (int s = -1; s <= 1; s += 2)
                {
                    float nx = bx + s * (bw * 0.5f + 0.35f);
                    if (nx < x0 + 0.3f || nx > x1 - 0.3f) continue;
                    B("Nightstand", new Vector3(nx, y + 0.3f, bz + (northFree ? 0.75f : -0.75f)), new Vector3(0.45f, 0.6f, 0.4f), M.darkWood);
                    Cyl("LampBase", new Vector3(nx, y + 0.68f, bz + (northFree ? 0.75f : -0.75f)), new Vector3(0.12f, 0.08f, 0.12f), M.brass, false);
                    Cyl("LampShade", new Vector3(nx, y + 0.85f, bz + (northFree ? 0.75f : -0.75f)), new Vector3(0.28f, 0.1f, 0.28f), M.sheet, false);
                }
            }
            // Wardrobe on a side wall - you can hide in it.
            float wz = northFree ? z0 + 1.6f : z1 - 1.6f;
            if (Chance(0.5f)) Hide(HidingSpot.Kind.Wardrobe, x0 + 0.42f, wz, 90f);
            else Hide(HidingSpot.Kind.Wardrobe, x1 - 0.42f, wz, -90f);
            // Writing desk + TV + armchair.
            float dz = northFree ? z0 + 0.5f : z1 - 0.5f;
            float dx = Chance(0.5f) ? x0 + 1.2f : x1 - 1.2f;
            if (Free(dx, dz, 0.8f))
            {
                B("Desk", new Vector3(dx, y + 0.38f, dz), new Vector3(1.2f, 0.76f, 0.5f), M.darkWood);
                B("TV", new Vector3(dx, y + 1.05f, dz), new Vector3(0.8f, 0.5f, 0.06f), M.plasticBlack, false, northFree ? 0f : 180f);
                Reserve(dx, dz, 0.8f);
            }
            float ax = dx < cx ? x1 - 0.7f : x0 + 0.7f;
            if (Free(ax, cz, 0.6f)) Armchair(ax, cz, dx < cx ? -90f : 90f, suite ? M.leather : M.fabricRed);
            if (suite)
            {
                Sofa(cx, cz - (northFree ? 1.5f : -1.5f), northFree ? 0f : 180f, M.leather);
                Plant(x0 + 0.5f, z0 + 0.5f);
                Plant(x1 - 0.5f, z0 + 0.5f);
            }
            B("Rug", new Vector3(cx, y + 0.005f, cz), new Vector3(R(1.8f, 2.6f), 0.01f, R(1.4f, 2f)), M.fabricRed, false, R(-10f, 10f));
        }

        void Laundry()
        {
            foreach (var p in AlongWalls(0.85f, 0.4f))
            {
                if (!Chance(0.55f) || !Free(p.x, p.y, 0.4f)) continue;
                Reserve(p.x, p.y, 0.45f);
                B("Washer", new Vector3(p.x, y + 0.45f, p.y), new Vector3(0.65f, 0.9f, 0.65f), M.porcelain, true, p.z);
                var door = Cyl("Door", new Vector3(p.x, y + 0.5f, p.y) + Quaternion.Euler(0f, p.z, 0f) * Vector3.forward * 0.33f,
                    new Vector3(0.42f, 0.01f, 0.42f), M.glass, false);
                door.transform.localRotation = Quaternion.Euler(0f, p.z, 0f) * Quaternion.Euler(90f, 0f, 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = R(x0 + 1.2f, x1 - 1.2f), z = R(z0 + 1.2f, z1 - 1.2f);
                if (!Free(x, z, 0.7f)) continue;
                B("Cart", new Vector3(x, y + 0.45f, z), new Vector3(0.9f, 0.7f, 0.6f), M.metal, true, R(0f, 90f));
                B("Laundry", new Vector3(x, y + 0.85f, z), new Vector3(0.8f, 0.2f, 0.5f), M.sheet, false, R(0f, 90f));
                Reserve(x, z, 0.7f);
            }
        }

        void Lounge()
        {
            // Bar counter along the north wall, sofa groups, piano-ish lump, rugs.
            float cx = (x0 + x1) * 0.5f;
            B("Bar", new Vector3(cx, y + 0.55f, z1 - 1.6f), new Vector3(6f, 1.1f, 0.7f), M.darkWood);
            B("BarTop", new Vector3(cx, y + 1.12f, z1 - 1.6f), new Vector3(6.2f, 0.05f, 0.8f), M.marble, false);
            Reserve(cx, z1 - 1.6f, 3.2f);
            Shelf(cx, z1 - 0.3f, 180f, 5f, 2.2f, 0.35f, M.darkWood, new[] { M.glass, M.brass, M.books[0] }, 0.6f);
            for (int i = -2; i <= 2; i++) Cyl("Stool", new Vector3(cx + i * 1.2f, y + 0.38f, z1 - 2.4f), new Vector3(0.38f, 0.38f, 0.38f), M.leather, false);
            for (float x = x0 + 2.5f; x < x1 - 2f; x += 4.5f)
                for (float z = z0 + 2.5f; z < z1 - 4f; z += 4.5f)
                {
                    if (!Free(x, z, 1.8f)) continue;
                    Sofa(x, z - 1.1f, 0f, Chance(0.5f) ? M.fabricGreen : M.leather);
                    Armchair(x - 1.3f, z + 0.3f, 120f, M.fabricRed);
                    Armchair(x + 1.3f, z + 0.3f, -120f, M.fabricRed);
                    B("CoffeeTable", new Vector3(x, y + 0.22f, z), new Vector3(1.1f, 0.44f, 0.6f), M.darkWood);
                    B("Rug", new Vector3(x, y + 0.005f, z), new Vector3(3.6f, 0.01f, 3f), M.fabricRed, false);
                    Reserve(x, z, 2f);
                }
            Plant(x0 + 0.5f, z0 + 0.5f); Plant(x1 - 0.5f, z0 + 0.5f); Plant(x0 + 0.5f, z1 - 0.5f);
        }

        void Vending()
        {
            float cx = (x0 + x1) * 0.5f;
            B("Vending", new Vector3(cx - 0.8f, y + 0.95f, z1 - 0.5f), new Vector3(1f, 1.9f, 0.8f), M.fabricRed);
            B("VendFront", new Vector3(cx - 0.8f, y + 1.1f, z1 - 0.9f), new Vector3(0.7f, 1.3f, 0.01f), M.screen, false);
            B("IceMachine", new Vector3(cx + 0.6f, y + 0.8f, z1 - 0.45f), new Vector3(0.9f, 1.6f, 0.7f), M.steel);
            Reserve(cx, z1 - 0.5f, 1.6f);
        }

        // ---------------- floor 2: school ----------------

        void Classroom(bool computers)
        {
            // Teacher's desk at the front (crouch under it to hide), blackboard, rows of pupil desks.
            bool alongX = (x1 - x0) >= (z1 - z0);
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            // Front is the west wall (or north wall for tall rooms).
            if (alongX)
            {
                B("Blackboard", new Vector3(x0 + 0.03f, y + 1.55f, cz), new Vector3(0.04f, 1.2f, Mathf.Min(4.5f, z1 - z0 - 1f)), M.blackboard, false);
                B("Chalkrail", new Vector3(x0 + 0.08f, y + 0.93f, cz), new Vector3(0.08f, 0.03f, Mathf.Min(4.5f, z1 - z0 - 1f)), M.wood, false);
                Hide(HidingSpot.Kind.Desk, x0 + 1.4f, cz + 1.2f, 90f);
                for (float x = x0 + 3.2f; x < x1 - 0.8f; x += 1.5f)
                    for (float z = z0 + 0.9f; z < z1 - 0.8f; z += 1.3f)
                        PupilDesk(x, z, 90f, computers);
            }
            else
            {
                B("Blackboard", new Vector3(cx, y + 1.55f, z1 - 0.03f), new Vector3(Mathf.Min(4.5f, x1 - x0 - 1f), 1.2f, 0.04f), M.blackboard, false);
                B("Chalkrail", new Vector3(cx, y + 0.93f, z1 - 0.08f), new Vector3(Mathf.Min(4.5f, x1 - x0 - 1f), 0.03f, 0.08f), M.wood, false);
                Hide(HidingSpot.Kind.Desk, cx + 1.2f, z1 - 1.4f, 180f);
                for (float z = z1 - 3.2f; z > z0 + 0.8f; z -= 1.5f)
                    for (float x = x0 + 0.9f; x < x1 - 0.8f; x += 1.3f)
                        PupilDesk(x, z, 180f, computers);
            }
            foreach (var p in AlongWalls(3f, 0.25f)) if (Chance(0.2f)) Shelf(p.x, p.y, p.z, 1.2f, 1.0f, 0.35f, M.wood, M.books, 0.6f);
        }

        void PupilDesk(float x, float z, float yaw, bool computer)
        {
            if (!Free(x, z, 0.5f)) return;
            Reserve(x, z, 0.45f);
            if (Chance(0.12f)) { B("TippedDesk", new Vector3(x, y + 0.3f, z), new Vector3(0.7f, 0.04f, 0.5f), M.wood, true, yaw + R(-40f, 40f)).transform.localRotation *= Quaternion.Euler(0f, 0f, 80f); return; }
            var g = new GameObject("PupilDesk").transform;
            g.SetParent(root, false); g.localPosition = new Vector3(x, y, z); g.localRotation = Quaternion.Euler(0f, yaw + R(-8f, 8f), 0f);
            var old = root; root = g;
            B("Top", new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 0.04f, 0.5f), M.wood);
            B("Frame", new Vector3(0f, 0.35f, 0.2f), new Vector3(0.65f, 0.68f, 0.03f), M.metal, false);
            B("Seat", new Vector3(0f, 0.42f, -0.45f), new Vector3(0.42f, 0.04f, 0.4f), M.wood, false);
            B("SeatBack", new Vector3(0f, 0.68f, -0.64f), new Vector3(0.4f, 0.32f, 0.03f), M.wood, false);
            B("SeatLeg", new Vector3(0f, 0.21f, -0.45f), new Vector3(0.35f, 0.42f, 0.03f), M.metal, false);
            if (computer) { B("Monitor", new Vector3(0f, 0.92f, 0.12f), new Vector3(0.45f, 0.32f, 0.04f), M.plasticBlack, false); B("Screen", new Vector3(0f, 0.92f, 0.098f), new Vector3(0.4f, 0.27f, 0.005f), M.screen, false); }
            else if (Chance(0.3f)) B("Book", new Vector3(R(-0.2f, 0.2f), 0.73f, 0f), new Vector3(0.2f, 0.03f, 0.27f), M.books[rnd.Next(M.books.Length)], false, R(0f, 40f));
            root = old;
        }

        void Lab(bool art)
        {
            for (float x = x0 + 2f; x < x1 - 1.5f; x += 3f)
                for (float z = z0 + 1.6f; z < z1 - 1.4f; z += 2.4f)
                {
                    if (!Free(x, z, 1.1f)) continue;
                    Reserve(x, z, 1.2f);
                    B("Bench", new Vector3(x, y + 0.45f, z), new Vector3(2.2f, 0.9f, 0.9f), art ? M.wood : M.plasticBlack);
                    B("BenchTop", new Vector3(x, y + 0.92f, z), new Vector3(2.3f, 0.04f, 1.0f), art ? M.wood : M.plasticBlack, false);
                    if (!art) { Cyl("Tap", new Vector3(x, y + 1.05f, z), new Vector3(0.04f, 0.12f, 0.04f), M.steel, false); B("Beaker", new Vector3(x + 0.5f, y + 1.0f, z + 0.2f), new Vector3(0.1f, 0.15f, 0.1f), M.glass, false); }
                    else B("Easel", new Vector3(x + R(-0.6f, 0.6f), y + 1.3f, z - 0.8f), new Vector3(0.7f, 0.9f, 0.04f), M.paper, false, R(-15f, 15f));
                    for (int k = -1; k <= 1; k += 2) Cyl("Stool", new Vector3(x + k * 0.6f, y + 0.33f, z - 0.75f), new Vector3(0.32f, 0.33f, 0.32f), M.wood, false);
                }
            foreach (var p in AlongWalls(1.4f, 0.3f)) if (Chance(0.35f)) Shelf(p.x, p.y, p.z, 1.2f, 1.9f, 0.4f, M.metal, new[] { M.glass, M.paper, M.cardboard }, 0.5f);
        }

        void StaffRoom()
        {
            BreakRoom();
            // A wall of staff lockers - hiding spots.
            for (float x = x0 + 0.6f; x < x0 + 5f && x < x1 - 0.6f; x += 1.05f) Hide(HidingSpot.Kind.Locker, x, z0 + 0.42f, 0f);
            Sofa(x1 - 1.4f, (z0 + z1) * 0.5f, -90f, M.fabricGreen);
        }

        void Library()
        {
            // Long rows of shelves with reading tables; the east wall hides the vault bookcase.
            for (float z = z1 - 2.2f; z > z0 + 3f; z -= 3f)
                for (float x = x0 + 1.4f; x < x1 - 3.2f; x += 2.2f)
                    Shelf(x, z, 0f, 2f, 2.4f, 0.45f, M.darkWood, M.books, 0.85f);
            foreach (var p in AlongWalls(2.1f, 0.25f))
                if (p.z == 90f || p.z == 180f || p.z == 0f) Shelf(p.x, p.y, p.z, 2f, 2.6f, 0.4f, M.darkWood, M.books, 0.8f);
            float tx = x1 - 2f;
            for (float z = z0 + 2.5f; z < z1 - 2f; z += 4f)
            {
                if (!Free(tx, z, 1f)) continue;
                B("ReadingTable", new Vector3(tx, y + 0.75f, z), new Vector3(1.2f, 0.06f, 2.2f), M.darkWood);
                B("TableBase", new Vector3(tx, y + 0.37f, z), new Vector3(0.4f, 0.74f, 1.8f), M.darkWood, false);
                Cyl("Lamp", new Vector3(tx, y + 0.9f, z), new Vector3(0.15f, 0.12f, 0.15f), M.brass, false);
                Reserve(tx, z, 1.2f);
                for (int k = -1; k <= 1; k += 2) { Chair(new Vector3(tx - 0.9f, y, z + k * 0.6f), 90f, Chance(0.15f)); }
            }
            for (int i = 0; i < 25; i++)
                B("FallenBook", new Vector3(R(x0, x1), y + 0.02f, R(z0, z1)), new Vector3(0.2f, 0.04f, 0.27f), M.books[rnd.Next(M.books.Length)], false, R(0f, 360f));
        }

        void Nurse()
        {
            float cx = (x0 + x1) * 0.5f;
            B("Cot", new Vector3(cx - 1.5f, y + 0.35f, z1 - 0.6f), new Vector3(2f, 0.15f, 0.8f), M.sheet);
            B("CotLegs", new Vector3(cx - 1.5f, y + 0.15f, z1 - 0.6f), new Vector3(1.9f, 0.3f, 0.7f), M.steel, false);
            B("Cabinet", new Vector3(cx + 1.5f, y + 0.9f, z1 - 0.3f), new Vector3(1f, 1.8f, 0.45f), M.porcelain);
            B("Curtain", new Vector3(cx, y + 1.3f, z1 - 1.2f), new Vector3(0.03f, 1.8f, 1.6f), M.fabricBlue, false);
        }

        void Music()
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            B("Piano", new Vector3(cx, y + 0.65f, z1 - 0.5f), new Vector3(1.6f, 1.3f, 0.65f), M.plasticBlack);
            Reserve(cx, z1 - 0.5f, 1f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 25f - 135f;
                var p = new Vector3(cx, y, cz) + Quaternion.Euler(0f, a, 0f) * Vector3.back * 3f;
                if (Free(p.x, p.z, 0.4f)) Chair(p, a, Chance(0.3f));
            }
            for (int i = 0; i < 5; i++) B("MusicStand", new Vector3(cx + R(-2f, 2f), y + 0.6f, cz + R(-1.5f, 1f)), new Vector3(0.45f, 0.03f, 0.3f), M.metal, false, R(0f, 360f));
        }

        void LockerRoom()
        {
            foreach (var p in AlongWalls(1.05f, 0.42f)) if (Chance(0.7f)) Hide(HidingSpot.Kind.Locker, p.x, p.y, p.z);
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            B("Bench", new Vector3(cx, y + 0.22f, cz), new Vector3(Mathf.Min(4f, x1 - x0 - 3f), 0.06f, 0.4f), M.wood);
            B("BenchLegs", new Vector3(cx, y + 0.1f, cz), new Vector3(Mathf.Min(3.6f, x1 - x0 - 3.4f), 0.2f, 0.3f), M.metal, false);
        }

        // ---------------- floor 1: ground ----------------

        void Lobby(RoomType t)
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            if (t == RoomType.Lobby)
            {
                // Reception desk, pillars, seating islands, a dead fountain.
                B("Reception", new Vector3(cx, y + 0.55f, z1 - 2.2f), new Vector3(6f, 1.1f, 0.9f), M.marble);
                B("ReceptionTop", new Vector3(cx, y + 1.12f, z1 - 2.2f), new Vector3(6.2f, 0.05f, 1f), M.darkWood, false);
                Reserve(cx, z1 - 2.2f, 3.2f);
                for (float x = x0 + 6f; x < x1 - 4f; x += 9f)
                {
                    Cyl("Pillar", new Vector3(x, y + 1.85f, cz - 1f), new Vector3(0.9f, 1.85f, 0.9f), M.marble);
                    Reserve(x, cz - 1f, 0.8f);
                }
                Cyl("FountainRim", new Vector3(cx, y + 0.3f, cz - 2f), new Vector3(3.2f, 0.3f, 3.2f), M.marble);
                Cyl("FountainWater", new Vector3(cx, y + 0.5f, cz - 2f), new Vector3(2.9f, 0.02f, 2.9f), M.grime, false);
                Reserve(cx, cz - 2f, 2f);
            }
            for (float x = x0 + 2.5f; x < x1 - 2f; x += 5f)
                for (float z = z0 + 2f; z < z1 - 2f; z += 4f)
                {
                    if (!Free(x, z, 1.4f)) continue;
                    Sofa(x, z, Chance(0.5f) ? 0f : 180f, Chance(0.5f) ? M.leather : M.fabricBlue);
                    B("Table", new Vector3(x, y + 0.2f, z + 1f), new Vector3(1f, 0.4f, 0.5f), M.darkWood);
                    Reserve(x, z, 1.4f);
                }
            Plant(x0 + 0.5f, z0 + 0.5f); Plant(x1 - 0.5f, z0 + 0.5f); Plant(x0 + 0.5f, z1 - 0.5f); Plant(x1 - 0.5f, z1 - 0.5f);
        }

        void Security()
        {
            float cx = (x0 + x1) * 0.5f;
            B("Console", new Vector3(cx, y + 0.45f, z1 - 0.5f), new Vector3(4f, 0.9f, 0.8f), M.plasticGrey);
            Reserve(cx, z1 - 0.5f, 2f);
            for (int i = 0; i < 6; i++)
            {
                float x = cx - 1.6f + (i % 3) * 1.6f, yy = y + 1.25f + (i / 3) * 0.55f;
                B("CCTV", new Vector3(x, yy, z1 - 0.15f), new Vector3(0.7f, 0.48f, 0.12f), M.plasticBlack, false);
                B("CCTVScreen", new Vector3(x, yy, z1 - 0.215f), new Vector3(0.62f, 0.4f, 0.005f), Chance(0.5f) ? M.screen : M.led, false);
            }
            Chair(new Vector3(cx, y, z1 - 1.4f), 0f, false);
            for (float x = x0 + 0.6f; x < x0 + 4f; x += 1.05f) Hide(HidingSpot.Kind.Locker, x, z0 + 0.42f, 0f);
        }

        void Cafeteria()
        {
            float cx = (x0 + x1) * 0.5f;
            B("Servery", new Vector3(cx, y + 0.5f, z1 - 1.2f), new Vector3(7f, 1f, 0.9f), M.steel);
            B("SneezeGuard", new Vector3(cx, y + 1.35f, z1 - 1.2f), new Vector3(7f, 0.6f, 0.02f), M.glass, false);
            Reserve(cx, z1 - 1.2f, 3.7f);
            for (float x = x0 + 2f; x < x1 - 1.5f; x += 3f)
                for (float z = z0 + 1.8f; z < z1 - 3.2f; z += 2.6f)
                {
                    if (!Free(x, z, 1.1f)) continue;
                    bool flipped = Chance(0.1f);
                    var t = B("Table", new Vector3(x, y + 0.74f, z), new Vector3(2.2f, 0.05f, 0.85f), M.plasticGrey);
                    if (flipped) { t.transform.localRotation = Quaternion.Euler(0f, R(0f, 40f), 85f); t.transform.localPosition += Vector3.down * 0.3f; }
                    else
                    {
                        B("TableLeg", new Vector3(x, y + 0.37f, z), new Vector3(1.8f, 0.72f, 0.1f), M.metal, false);
                        B("BenchN", new Vector3(x, y + 0.44f, z + 0.65f), new Vector3(2.1f, 0.05f, 0.32f), M.plasticGrey, false);
                        B("BenchS", new Vector3(x, y + 0.44f, z - 0.65f), new Vector3(2.1f, 0.05f, 0.32f), M.plasticGrey, false);
                        if (Chance(0.4f)) B("Tray", new Vector3(x + R(-0.7f, 0.7f), y + 0.78f, z), new Vector3(0.45f, 0.02f, 0.32f), M.fabricRed, false, R(-20f, 20f));
                    }
                    Reserve(x, z, 1.2f);
                }
        }

        void Kitchen()
        {
            foreach (var p in AlongWalls(1.0f, 0.4f))
            {
                if (!Free(p.x, p.y, 0.45f)) continue;
                Reserve(p.x, p.y, 0.5f);
                bool stove = Chance(0.25f);
                B(stove ? "Stove" : "Counter", new Vector3(p.x, y + 0.45f, p.y), new Vector3(0.95f, 0.9f, 0.7f), M.steel, true, p.z);
                if (stove) B("Hood", new Vector3(p.x, y + 2.1f, p.y), new Vector3(1f, 0.5f, 0.8f), M.steel, false, p.z);
            }
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            B("Island", new Vector3(cx, y + 0.45f, cz), new Vector3(1.2f, 0.9f, 4f), M.steel);
            Reserve(cx, cz, 2f);
            for (int i = 0; i < 6; i++) Cyl("Pot", new Vector3(cx + R(-0.4f, 0.4f), y + 0.98f, cz + R(-1.6f, 1.6f)), new Vector3(0.3f, 0.12f, 0.3f), M.steel, false);
        }

        void Boiler(bool generator)
        {
            for (float z = z1 - 2f; z > z0 + 1.5f; z -= 3.4f)
                for (float x = x0 + 1.8f; x < x1 - 1.2f; x += 3.2f)
                {
                    if (!Free(x, z, 1.2f)) continue;
                    Reserve(x, z, 1.3f);
                    if (generator)
                    {
                        B("Generator", new Vector3(x, y + 0.75f, z), new Vector3(2.2f, 1.5f, 1.1f), M.fabricGreen);
                        B("Exhaust", new Vector3(x + 0.8f, y + 2.4f, z), new Vector3(0.2f, 1.8f, 0.2f), M.rust, false);
                    }
                    else
                    {
                        var b = Cyl("Boiler", new Vector3(x, y + 1.0f, z), new Vector3(1.6f, 1.0f, 1.6f), M.rust);
                        Cyl("Flue", new Vector3(x, y + 2.8f, z), new Vector3(0.35f, 0.85f, 0.35f), M.rust, false);
                        B("Gauge", new Vector3(x, y + 1.3f, z - 0.81f), new Vector3(0.15f, 0.15f, 0.02f), M.brass, false);
                    }
                }
            // Pipes along the ceiling.
            float py = y + LevelLayout.Story - LevelBuilder.Slab - 0.35f;
            for (float x = x0 + 0.5f; x < x1; x += 1.6f)
                B("Pipe", new Vector3(x, py, (z0 + z1) * 0.5f), new Vector3(0.16f, 0.16f, z1 - z0), M.rust, false);
        }

        void Loading()
        {
            int n = (int)((x1 - x0) * (z1 - z0) / 5f);
            for (int i = 0; i < n; i++)
            {
                float x = R(x0 + 1f, x1 - 1f), z = R(z0 + 1f, z1 - 1f);
                if (!Free(x, z, 0.8f)) continue;
                Reserve(x, z, 0.9f);
                if (Chance(0.5f))
                {
                    B("Pallet", new Vector3(x, y + 0.07f, z), new Vector3(1.2f, 0.14f, 1f), M.wood, true, R(0f, 20f));
                    B("Crate", new Vector3(x, y + 0.6f, z), new Vector3(1.0f, 0.9f, 0.9f), M.cardboard, true, R(0f, 20f));
                }
                else
                {
                    Cyl("Drum", new Vector3(x, y + 0.45f, z), new Vector3(0.6f, 0.45f, 0.6f), Chance(0.5f) ? M.rust : M.fabricBlue);
                }
            }
        }

        void Shop()
        {
            for (float z = z1 - 1.5f; z > z0 + 1.5f; z -= 2.2f) Shelf((x0 + x1) * 0.5f, z, 0f, 2.4f, 1.6f, 0.5f, M.metal, new[] { M.books[0], M.books[1], M.books[3], M.paper }, 0.5f);
            B("Till", new Vector3(x0 + 0.8f, y + 0.5f, z0 + 1f), new Vector3(1.2f, 1f, 0.6f), M.darkWood);
        }

        // ---------------- small pieces ----------------

        void Sofa(float x, float z, float yaw, Material m)
        {
            if (!Free(x, z, 1f)) return;
            Reserve(x, z, 1.1f);
            var g = new GameObject("Sofa").transform;
            g.SetParent(root, false); g.localPosition = new Vector3(x, y, z); g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var old = root; root = g;
            B("Base", new Vector3(0f, 0.22f, 0f), new Vector3(2f, 0.44f, 0.85f), m);
            B("Back", new Vector3(0f, 0.6f, -0.35f), new Vector3(2f, 0.5f, 0.18f), m, false);
            B("ArmL", new Vector3(-0.95f, 0.5f, 0f), new Vector3(0.14f, 0.3f, 0.85f), m, false);
            B("ArmR", new Vector3(0.95f, 0.5f, 0f), new Vector3(0.14f, 0.3f, 0.85f), m, false);
            root = old;
        }

        void Armchair(float x, float z, float yaw, Material m)
        {
            if (!Free(x, z, 0.6f)) return;
            Reserve(x, z, 0.6f);
            var g = new GameObject("Armchair").transform;
            g.SetParent(root, false); g.localPosition = new Vector3(x, y, z); g.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var old = root; root = g;
            B("Base", new Vector3(0f, 0.22f, 0f), new Vector3(0.85f, 0.44f, 0.8f), m);
            B("Back", new Vector3(0f, 0.62f, -0.33f), new Vector3(0.85f, 0.5f, 0.15f), m, false);
            root = old;
        }

        void Plant(float x, float z)
        {
            if (!Free(x, z, 0.35f)) return;
            Reserve(x, z, 0.35f);
            Cyl("Pot", new Vector3(x, y + 0.25f, z), new Vector3(0.45f, 0.25f, 0.45f), M.cardboard, false);
            Gfx.Prim(PrimitiveType.Sphere, "DeadPlant", root, new Vector3(x, y + 0.85f, z), new Vector3(0.6f, 0.8f, 0.6f), M.fabricGreen, false);
        }

        void Debris(Room room)
        {
            int n = Mathf.Max(2, room.cells.Count / 3);
            for (int i = 0; i < n; i++)
            {
                float x = R(x0 + 0.3f, x1 - 0.3f), z = R(z0 + 0.3f, z1 - 0.3f);
                double roll = rnd.NextDouble();
                if (roll < 0.3) B("Paper", new Vector3(x, y + 0.006f, z), new Vector3(0.21f, 0.004f, 0.29f), M.paper, false, R(0f, 360f));
                else if (roll < 0.45) { float w = R(0.6f, 1.8f); Gfx.Decal(root, new Vector3(x, y + 0.25f, z), Vector3.down, w, w * R(0.7f, 1.2f), Gfx.DecalKind.Blood, rnd.Next()); }
                else if (roll < 0.58) { float w = R(1.4f, 3f); Gfx.Decal(root, new Vector3(x, y + 0.25f, z), Vector3.down, w, w * R(0.7f, 1.1f), Gfx.DecalKind.Grime, rnd.Next()); }
                else if (roll < 0.65 && Free(x, z, 0.3f))
                {
                    float s = R(0.15f, 0.45f);
                    B("Rubble", new Vector3(x, y + s * 0.3f, z), new Vector3(s, s * 0.6f, s * 1.1f), M.concrete, false, R(0f, 360f))
                        .transform.localRotation = Quaternion.Euler(R(0f, 30f), R(0f, 360f), R(0f, 30f));
                }
            }
        }
    }

    /// <summary>
    /// Merges the static primitives under a transform into one mesh per material, keeping colliders.
    /// Thousands of furniture boxes become a few hundred renderers, which keeps HDRP and the
    /// ray-tracing acceleration structure fast.
    /// </summary>
    public static class StaticMerge
    {
        /// <summary>True if something between <paramref name="t"/> and <paramref name="root"/> (exclusive) opts out of merging.</summary>
        static bool UnderNoMerge(Transform t, Transform root)
        {
            for (var x = t; x != null && x != root; x = x.parent)
                if (x.GetComponent<NoMerge>() != null) return true;
            return false;
        }

        public static void Merge(Transform root, string name)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var counts = new Dictionary<Material, int>();
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            var done = new List<MeshFilter>();
            foreach (var mf in filters)
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null || UnderNoMerge(mf.transform, root)) continue;
                var m = r.sharedMaterial;
                if (m == null || m.renderQueue >= 2501) continue; // keep transparent objects separate
                List<CombineInstance> list;
                if (!groups.TryGetValue(m, out list)) { list = new List<CombineInstance>(); groups[m] = list; counts[m] = 0; }
                int verts = mf.sharedMesh.vertexCount;
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                counts[m] += verts;
                done.Add(mf);
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = name + "_" + kv.Key.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var g = new GameObject(name + " (" + kv.Key.name + ")");
                g.transform.SetParent(root, false);
                g.isStatic = true;
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                mr.staticShadowCaster = true;   // goes into cached shadow maps
            }
            foreach (var mf in done)
            {
                var r = mf.GetComponent<MeshRenderer>();
                Object.Destroy(r);
                Object.Destroy(mf);
            }
        }
    }

    /// <summary>Marks a subtree that must keep its own renderers (animated doors, flickering lamps...).</summary>
    public class NoMerge : MonoBehaviour { }
}
