using System;
using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// What goes in each room, by its college role (see HollowTower.CollegeRoles).
    /// The look is "left behind", not "wrecked": rooms keep their purpose and most furniture stays where it was,
    /// with chairs pushed back or turned, things slightly crooked, the odd item knocked over, a little paper,
    /// and only occasionally real damage. Large rooms are filled with large, meaningful structures
    /// (lecture tiers, library stacks, planter beds, exam grids) rather than clutter.
    /// </summary>
    public static class CollegeRecipes
    {
        // ------------------------------------------------------------------ prop groups
        static readonly string[] StudentChairs = { "SchoolChair_01", "SchoolChair_01", "SchoolChair_01", "plastic_monobloc_chair_01" };
        static readonly string[] OfficeChairs = { "modern_arm_chair_01", "dining_chair_02", "dining_chair_02" };
        static readonly string[] SoftSeats = { "ArmChair_01", "modern_arm_chair_01", "Ottoman_01", "Sofa_01", "sofa_02", "sofa_03" };
        static readonly string[] MetalShelves = { "steel_frame_shelves_01", "steel_frame_shelves_02", "steel_frame_shelves_03", "worn_metal_rack" };
        static readonly string[] Bookcases = { "wooden_bookshelf_worn", "wooden_bookshelf_worn", "Shelf_01", "painted_wooden_shelves" };
        static readonly string[] DeskClutter = { "binder_notebook", "clipboard", "office_notepads", "stationery_supplies", "vintage_stapler", "desk_lamp_arm_01", "alarm_clock_01", "standing_picture_frame_01", "standing_picture_frame_02", "mantel_clock_01" };
        static readonly string[] Bottles = { "bleach_bottle", "multi_cleaner_bottle", "plastic_bottle_gallon" };
        static readonly string[] Frames = { "fancy_picture_frame_01", "hanging_picture_frame_01", "hanging_picture_frame_02", "hanging_picture_frame_03" };
        static readonly string[] PottedPlants = { "potted_plant_01", "potted_plant_02", "potted_plant_04", "planter_pot_clay" };
        static readonly string[] BedPlants = { "anthurium_botany_01_0", "anthurium_botany_01_1", "anthurium_botany_01_2", "anthurium_botany_01_3", "anthurium_botany_01_4", "anthurium_botany_01_5",
                                               "calathea_orbifolia_01_0", "calathea_orbifolia_01_1", "calathea_orbifolia_01_2", "calathea_orbifolia_01_3", "calathea_orbifolia_01_4",
                                               "fern_02_0", "fern_02_1", "fern_02_2", "fern_02_3", "nettle_plant_0", "nettle_plant_2", "nettle_plant_4", "pachira_aquatica_01_0", "pachira_aquatica_01_1" };
        static readonly string[] DeadPlants = { "dry_branches_medium_01_0", "dry_branches_medium_01_1", "dry_branches_medium_01_2", "nettle_plant_1", "nettle_plant_3", "nettle_plant_5" };
        static readonly string[] Curios = { "binoculars", "magnifying_glass_01", "vintage_microscope", "horse_statue_01", "mantel_clock_01", "Camera_01", "alarm_clock_01", "chess_set", "retro_multimeter" };

        static object F(Func<RoomCtx, GameObject> f) { return f; }
        static object V(string key, int n, Func<RoomCtx, GameObject> f) { return ProcKit.Var(key, n, f); }
        static readonly object PaperO = F(ProcKit.PaperSheet);
        static readonly object BookO = F(ProcKit.BookItem);
        static readonly object BookStackO = F(ProcKit.BookStack);
        static readonly object BookRowO = F(ProcKit.BookRow);
        static readonly object TileO = F(ProcKit.CeilingTile);
        static readonly object RubbleO = F(ProcKit.Rubble);
        static readonly object PuddleO = F(ProcKit.PuddleDecal);
        static readonly object ClothesO = F(ProcKit.ClothesPile);
        static readonly object JarO = F(ProcKit.SpecimenJar);
        static readonly object TrayO = F(ProcKit.Tray);
        static readonly object RugO = F(ProcKit.Rug);
        static readonly object MattressO = V("MattressItem", 6, ProcKit.MattressItem);
        static readonly object NoticeO = V("NoticeBoard", 14, ProcKit.NoticeBoard);
        static readonly object PosterO = V("Poster", 16, ProcKit.Poster);
        static readonly object BannerO = V("Banner", 4, ProcKit.Banner);
        static readonly object WindowO = V("Window", 6, ProcKit.Window);
        static readonly object PeriodicO = V("PeriodicTable", 1, ProcKit.PeriodicTable);
        static readonly object MapO = V("WorldMap", 3, ProcKit.WorldMap);
        static readonly object PegO = V("Pegboard", 4, ProcKit.Pegboard);
        static readonly object ExitO = V("ExitSign", 1, ProcKit.ExitSign);
        static readonly object WallShelfO = V("WallShelf", 6, ProcKit.WallShelf);
        static readonly object TrophyO = V("TrophyCase", 6, ProcKit.TrophyCase);
        static readonly object VitrineO = V("Vitrine", 4, ProcKit.Vitrine);
        static readonly object GlobeO = V("Globe", 3, ProcKit.Globe);
        static readonly object EaselO = V("Easel", 8, ProcKit.Easel);
        static readonly object LecternO = V("Lectern", 1, ProcKit.Lectern);
        static readonly object WorkbenchO = V("Workbench", 4, ProcKit.Workbench);
        static readonly object PlanterO = V("PlanterBed", 6, ProcKit.PlanterBed);
        static readonly object TelescopeO = V("Telescope", 1, ProcKit.Telescope);
        static readonly object FumeHoodO = V("FumeHood", 3, ProcKit.FumeHood);
        static readonly object RadiatorO = V("Radiator", 8, ProcKit.Radiator);
        static readonly object FridgeO = V("Fridge", 2, ProcKit.Fridge);
        static readonly object VendingO = V("Vending", 6, ProcKit.Vending);
        static readonly object PianoO = V("Piano", 3, ProcKit.Piano);
        static readonly object StandO = V("MusicStand", 2, ProcKit.MusicStand);
        static readonly object PedestalO = V("Pedestal", 4, ProcKit.Pedestal);
        static readonly object BucketO = V("Bucket", 4, ProcKit.Bucket);
        static readonly object WasherO = V("Washer", 4, ProcKit.Washer);
        static readonly object RackO = V("ServerRack", 8, ProcKit.ServerRack);
        static readonly object CopierO = V("Photocopier", 3, ProcKit.Photocopier);
        static readonly object PalletO = V("Pallet", 3, ProcKit.Pallet);
        static readonly object FountainO = V("WaterFountain", 2, ProcKit.WaterFountain);
        static readonly object UrinalO = V("Urinal", 2, ProcKit.Urinal);
        static readonly object DustSheetO = V("DustSheet", 6, ProcKit.DustSheet);
        static readonly object StackO = V("FurnitureStack", 4, ProcKit.FurnitureStack);
        static readonly object PanelsO = V("DisplayPanels", 4, ProcKit.DisplayPanels);
        static readonly object ScaffoldO = V("Scaffold", 3, ProcKit.Scaffold);
        static readonly object CratesO = V("CrateStack", 4, ProcKit.CrateStack);
        static readonly object BoilerO = V("Boiler", 2, ProcKit.Boiler);
        static readonly object TankO = V("Tank", 2, ProcKit.Tank);
        static readonly object HoardingO = V("Hoarding", 6, ProcKit.Hoarding);

        static float Area(RoomCtx c) { return c.interior.width * c.interior.height; }
        static Vector2 Perp(Vector2 v) { return new Vector2(v.y, -v.x); }
        static Vector2 Jit(RoomCtx c, float s) { return new Vector2(c.Range(-s, s), c.Range(-s, s)); }

        /// <summary>"Left behind" pose: mostly upright, often a little off, rarely knocked over.</summary>
        static Pose Left(RoomCtx c, float tip = 0.03f) { return c.MessyPose(tip * (1 + c.mess)); }
        /// <summary>Yaw drift for things that were bumped or pushed back.</summary>
        static float Drift(RoomCtx c, float max = 8f) { return c.Chance(0.7f) ? c.Range(-max * 0.4f, max * 0.4f) : c.Range(-max, max); }

        /// <summary>A frame on the room aligned to one wall: u runs along it, v goes into the room from its face.</summary>
        class Frame
        {
            public Vector2 u, v;
            public float u0, u1, v0, v1;
            public Frame(RoomCtx c, WallSeg w)
            {
                u = w.Tangent; v = w.n;
                var r = c.interior;
                var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax) };
                u0 = float.MaxValue; u1 = float.MinValue; v1 = float.MinValue;
                foreach (var p in corners)
                {
                    u0 = Mathf.Min(u0, Vector2.Dot(p, u)); u1 = Mathf.Max(u1, Vector2.Dot(p, u));
                    v1 = Mathf.Max(v1, Vector2.Dot(p, v));
                }
                v0 = Vector2.Dot(w.a, v);
            }
            public float Width { get { return u1 - u0; } }
            public float Depth { get { return v1 - v0; } }
            public Vector2 At(float su, float sv) { return u * (u0 + su) + v * (v0 + sv); }
        }

        /// <summary>The wall to treat as the front: the longest inner (non-window) wall, else the longest.</summary>
        static WallSeg Front(RoomCtx c)
        {
            WallSeg best = null;
            foreach (var w in c.walls) if (!w.outer && (best == null || w.Length > best.Length)) best = w;
            return best ?? c.LongestWall();
        }
        static WallSeg Opposite(RoomCtx c, WallSeg w)
        {
            if (w == null) return null;
            WallSeg best = null;
            foreach (var x in c.walls) if (Vector2.Dot(x.n, w.n) < -0.9f && (best == null || x.Length > best.Length)) best = x;
            return best;
        }
        static List<WallSeg> Sides(RoomCtx c, WallSeg w) { return c.walls.FindAll(x => Mathf.Abs(Vector2.Dot(x.n, w.n)) < 0.1f); }

        // ------------------------------------------------------------------ entry
        public static void Dress(RoomCtx c)
        {
            if (c.walls.Count == 0) return;
            Landmarks(c);
            switch (c.room.role)
            {
                case "lecture": Lecture(c); break;
                case "classroom": Classroom(c, false); break;
                case "geography": Classroom(c, true); break;
                case "seminar": Seminar(c); break;
                case "exam": Exam(c); break;
                case "chemlab": Lab(c, 0); break;
                case "biolab": Lab(c, 1); break;
                case "physlab": Lab(c, 2); break;
                case "darkroom": Lab(c, 3); break;
                case "workshop": Workshop(c); break;
                case "computer": ComputerLab(c); break;
                case "library": Library(c); break;
                case "archive": Archive(c); break;
                case "reading": Reading(c); break;
                case "study": Study(c); break;
                case "art": Art(c); break;
                case "music": Music(c, false); break;
                case "practice": Music(c, true); break;
                case "staffroom": StaffRoom(c); break;
                case "dean": case "warden": Dean(c); break;
                case "professor": Professor(c); break;
                case "admin": case "bursar": case "enquiries": Counter(c); break;
                case "records": case "copy": Records(c, c.room.role == "copy"); break;
                case "workroom": Workroom(c); break;
                case "senate": Senate(c); break;
                case "meeting": Meeting(c); break;
                case "server": Server(c, false); break;
                case "it": Server(c, true); break;
                case "cctv": case "security": Security(c); break;
                case "infirmary": Infirmary(c); break;
                case "counselling": Counselling(c); break;
                case "lobby": case "waiting": Lobby(c); break;
                case "trophy": Trophy(c); break;
                case "museum": Museum(c); break;
                case "botany": case "greenhouse": Botany(c, c.room.role == "greenhouse"); break;
                case "herbarium": Herbarium(c); break;
                case "bookstore": Bookstore(c); break;
                case "cafeteria": Cafeteria(c); break;
                case "kitchen": case "scullery": case "coldstore": Kitchen(c); break;
                case "lockers": Lockers(c); break;
                case "washroom": case "showers": Washroom(c); break;
                case "dorm": Dorm(c); break;
                case "common": case "lounge": Lounge(c); break;
                case "laundry": Laundry(c); break;
                case "linen": case "housekeeping": case "janitor": case "storage": Storage(c); break;
                case "vending": Vending(c); break;
                case "plant": Plant(c); break;
                case "loading": case "parking": Loading(c); break;
                case "mail": Mail(c); break;
                default: Passage(c); break;
            }
            Windows(c);
        }

        // ------------------------------------------------------------------ landmarks
        /// <summary>The big pieces each kind of room may have standing in its open floor.</summary>
        static object[] LandmarksFor(string role)
        {
            switch (role)
            {
                case "classroom": case "geography": case "exam": case "seminar": case "cafeteria": return new[] { StackO, StackO, DustSheetO };
                case "library": case "archive": case "reading": case "study": case "herbarium": case "bookstore": return new[] { PanelsO, PanelsO, DustSheetO };
                case "museum": case "trophy": case "lobby": case "waiting": return new[] { ScaffoldO, PanelsO, DustSheetO };
                case "botany": case "greenhouse": return new[] { ScaffoldO };
                case "art": case "music": case "practice": return new[] { DustSheetO, DustSheetO, PanelsO };
                case "common": case "lounge": case "staffroom": case "senate": case "workroom": return new[] { DustSheetO, DustSheetO, StackO };
                case "dean": case "warden": case "professor": case "meeting": case "counselling": case "admin": case "bursar": case "enquiries": case "records":
                case "dorm": return new[] { DustSheetO };
                case "storage": case "linen": case "housekeeping": case "janitor": case "loading": case "parking": case "mail": case "laundry": case "workshop": return new[] { CratesO };
                case "plant": return new[] { BoilerO, TankO };
                default: return null;   // labs, washrooms, kitchens, server rooms, lecture tiers: already full of their own tall fittings
            }
        }

        /// <summary>
        /// One to four room-sized pieces (2-3 m tall, up to several metres long) standing in the open floor of big rooms,
        /// so the player can't take in the whole room from the doorway. Placed first, square to the walls, well clear of
        /// them and of each other; the room's own furniture then fills in around them.
        /// </summary>
        static void Landmarks(RoomCtx c)
        {
            var kinds = LandmarksFor(c.room.role);
            if (kinds == null) return;
            float a = Area(c);
            int want = a < 55 ? 0 : a < 110 ? 1 : a < 220 ? 2 : a < 400 ? 3 : 4;
            if (c.room.role == "plant") want = Mathf.Max(want, 1);
            // private rooms: one covered pile (two in the very big ones), not a field of them
            if (kinds.Length == 1 && kinds[0] == DustSheetO) want = Mathf.Min(want, a < 300 ? 1 : 2);
            var placed = new List<Vector2>();
            var screen = ScreenFor(c.room.role);
            if (screen != null && a >= 220) want -= Screens(c, screen, a >= 600 ? 3 : a >= 400 ? 2 : 1);
            if (kinds.Length == 1 && kinds[0] == ScaffoldO) want = Mathf.Min(want, 2);
            for (int tries = 0; placed.Count < want && tries < want * 14; tries++)
            {
                var p = c.RandomPoint(1.8f);
                bool open = true;
                foreach (var d in new[] { new Vector2(1.8f, 0), new Vector2(-1.8f, 0), new Vector2(0, 1.8f), new Vector2(0, -1.8f) })
                    if (!c.IsFree(p + d, 0.05f)) { open = false; break; }
                foreach (var q in placed) if (Vector2.Distance(p, q) < 5f) open = false;
                if (!open) continue;
                object what = c.room.role == "plant" && placed.Count == 0 ? BoilerO : c.Pick(kinds);
                if (c.Floor(what, p, c.Range(0, 4) * 90f + Drift(c, 4f), Pose.Upright, true, 0.4f) != null) placed.Add(p);
            }
        }

        /// <summary>The long pieces that may stand across the view in big rooms of this kind (null: none).</summary>
        static object[] ScreenFor(string role)
        {
            switch (role)
            {
                case "museum": case "trophy": case "reading": case "study": case "bookstore": case "herbarium": case "art": return new[] { PanelsO, HoardingO };
                case "classroom": case "geography": case "exam": case "seminar": case "cafeteria": case "lobby": case "waiting":
                case "music": case "common": case "lounge": case "staffroom": case "workroom": case "senate": case "meeting":
                case "professor": case "dean": case "warden": case "admin": case "bursar": case "enquiries": case "records": case "counselling": return new[] { HoardingO };
                default: return null;
            }
        }

        /// <summary>
        /// One to three long screens (a half-built partition wall, a run of display panels) standing square across the
        /// view from the main door, the first part-way in and beside the walkway, the second deeper in on the other
        /// side, so a big room only opens up as the player walks round them. A third one, in the very big rooms,
        /// stands across the view from the second door. Returns how many were placed.
        /// </summary>
        static int Screens(RoomCtx c, object[] kinds, int count)
        {
            // doors by how far their view runs into the room, longest first
            var views = new List<KeyValuePair<float, DoorInfo>>();
            foreach (var d in c.doors)
            {
                float dd = Mathf.Abs(d.n.x) > 0.5f ? (d.n.x > 0 ? c.interior.xMax - d.mid.x : d.mid.x - c.interior.xMin)
                                                   : (d.n.y > 0 ? c.interior.yMax - d.mid.y : d.mid.y - c.interior.yMin);
                if (dd >= 9f) views.Add(new KeyValuePair<float, DoorInfo>(dd, d));
            }
            if (views.Count == 0) return 0;
            views.Sort((p, q) => q.Key.CompareTo(p.Key));
            float side = c.Chance(0.5f) ? 1 : -1;
            int placed = 0;
            for (int k = 0; k < count; k++, side = -side)
            {
                bool second = k == 2;
                if (second && views.Count < 2) break;
                var door = views[second ? 1 : 0].Value;
                float depth = views[second ? 1 : 0].Key;
                var t = new Vector2(door.n.y, -door.n.x);
                float yaw = c.Yaw(-door.n);   // painted face and notices towards the door
                float frac = second || count == 1 ? c.Range(0.42f, 0.55f) : k == 0 ? c.Range(0.3f, 0.4f) : c.Range(0.62f, 0.75f);
                var kind = c.Pick(kinds);
                for (int tries = 0; tries < 14; tries++)
                {
                    // start at the walkway and step out until it fits beside it; the deeper one may stand right across
                    float lateral = side * ((k != 1 ? 1.2f : 0f) + tries * 0.5f + c.Range(0f, 0.3f));
                    var p = door.mid + door.n * (depth * frac + c.Range(-0.5f, 0.5f)) + t * lateral;
                    if (c.Floor(kind, p, yaw + Drift(c, 3f), Pose.Upright, true, 0.5f) != null) { placed++; break; }
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------ shared bits
        /// <summary>Windows along the outside walls, evenly spaced, with radiators under some of them.</summary>
        static void Windows(RoomCtx c)
        {
            foreach (var w in c.walls)
            {
                if (!w.outer || w.Length < 2f) continue;
                int n = Mathf.FloorToInt(w.Length / c.Range(3.2f, 4.2f));
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    if (c.OnWall(WindowO, w, t, 1.75f) != null && c.Chance(0.45f))
                        c.AgainstWall(RadiatorO, w, t, Pose.Upright, 0.05f);
                }
            }
        }

        /// <summary>Hangs things on the inner walls (count per 10 m of wall), each at a sensible height.</summary>
        static void Decorate(RoomCtx c, float per10m, params object[] items)
        {
            var inner = c.walls.FindAll(x => !x.outer && x.Length > 1.2f);
            if (inner.Count == 0) return;
            float total = 0;
            foreach (var w in inner) total += w.Length;
            int count = Mathf.Max(1, Mathf.RoundToInt(total / 10f * per10m));
            for (int i = 0, tries = 0; i < count && tries < count * 4; tries++)
                if (Hang(c, c.Pick(items), inner[c.Range(0, inner.Count)], c.Range(0.08f, 0.92f)) != null) i++;
        }

        static GameObject Hang(RoomCtx c, object what, WallSeg w, float t)
        {
            if (w == null) return null;
            var id = what as string;
            float h;
            if (id == "wall_clock" || id == "vintage_telephone_wall_clock") h = c.Range(2.3f, 2.6f);
            else if (id == "fire_alarm") h = 1.35f;
            else if (id == "korean_public_payphone_01") h = 1.3f;
            else if (id == "security_camera_01") h = 3.3f;
            else if (what == ExitO) h = 2.5f;
            else if (what == BannerO) h = 2.3f;
            else if (what == WallShelfO) h = c.Range(1.3f, 1.7f);
            else if (what == MapO || what == PeriodicO) h = 1.65f;
            else h = c.Range(1.45f, 1.75f);
            float tilt = 0;
            if ((id != null && Array.IndexOf(Frames, id) >= 0) || what == PosterO) tilt = c.Chance(0.25f + 0.1f * c.mess) ? c.Range(-7f, 7f) : 0;
            return c.OnWall(what, w, t, h, tilt);
        }

        static void Board(RoomCtx c, WallSeg w, bool chalk, float maxLen = 5f)
        {
            if (w == null || w.Length < 2f) return;
            c.OnWall(ProcKit.BoardV(Mathf.Min(maxLen, w.Length - 1f), chalk), w, 0.5f, 1.55f);
        }

        /// <summary>Lines a wall with items at a gentle density (fraction of the wall used).</summary>
        static void Line(RoomCtx c, WallSeg w, float fill, Func<object> next, Action<GameObject> after = null)
        {
            if (w == null) return;
            foreach (var go in c.AlongWall(w, next, 0.05f, 0.9f, fill, () => Left(c, 0.02f)))
                if (after != null) after(go);
        }

        static void Fill(RoomCtx c, GameObject host, int n, params object[] items)
        {
            if (host == null || n <= 0) return;
            c.OnSurface(host, () => c.Pick(items), n);
        }

        static void FillShelves(RoomCtx c, GameObject shelf, int n, params object[] items)
        {
            if (shelf == null || n <= 0 || shelf.transform.up.y < 0.9f) return;
            c.OnSurface(shelf, () => c.Pick(items), n, 5);
        }

        static void Papers(RoomCtx c, int n, Vector2? near = null)
        {
            for (int i = 0; i < n; i++)
            {
                var p = near.HasValue ? near.Value + Jit(c, 1.4f) : c.RandomPoint(0.6f);
                c.Floor(PaperO, p, c.Range(0f, 360f), Pose.Upright, false, 0f);
            }
        }

        /// <summary>Chairs round a table: tucked in, pushed back, or turned away as people left them.</summary>
        static void ChairsAround(RoomCtx c, GameObject table, string[] chairs, bool bothEnds = false)
        {
            if (table == null) return;
            var b = PropLibrary.RendererBounds(table);
            var center = new Vector2(b.center.x, b.center.z);
            bool longX = b.size.x >= b.size.z;
            float L = longX ? b.size.x : b.size.z, S = longX ? b.size.z : b.size.x;
            int per = Mathf.Max(1, Mathf.FloorToInt(L / 0.7f));
            string kind = c.Pick(chairs);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < per; i++)
                {
                    if (c.Chance(0.12f)) continue;
                    float along = -L * 0.5f + L * (i + 0.5f) / per;
                    float pushed = c.Chance(0.3f) ? c.Range(0.25f, 0.7f) : 0f;
                    float out1 = S * 0.5f + 0.42f + pushed;   // chair footprint just clear of the table edge
                    var off = longX ? new Vector2(along, side * out1) : new Vector2(side * out1, along);
                    var face = longX ? new Vector2(0, -side) : new Vector2(-side, 0);
                    c.Floor(kind, center + off, c.Yaw(face) + Drift(c, pushed > 0 ? 30f : 8f), Left(c, 0.02f));
                }
            if (bothEnds)
                for (int e = -1; e <= 1; e += 2)
                {
                    var dir = longX ? new Vector2(e, 0) : new Vector2(0, e);
                    c.Floor(kind, center + dir * (L * 0.5f + 0.35f), c.Yaw(-dir) + Drift(c), Left(c));
                }
        }

        /// <summary>A desk with its chair behind it, and a few things on top.</summary>
        static GameObject DeskWithChair(RoomCtx c, string desk, string chair, Vector2 p, Vector2 facing, int clutter, params object[] items)
        {
            var d = c.Floor(desk, p, c.Yaw(facing) + Drift(c, 5f), Left(c, 0.01f));
            if (d == null) return null;
            Fill(c, d, clutter, items.Length > 0 ? items : DeskClutter);
            float pushed = c.Chance(0.35f) ? c.Range(0.2f, 0.6f) : 0f;
            c.Floor(chair, p - facing * (0.75f + pushed), c.Yaw(facing) + Drift(c, pushed > 0 ? 35f : 10f), Left(c, 0.03f));
            return d;
        }

        static void LoungeGroup(RoomCtx c, Vector2 p)
        {
            var table = c.Floor(c.Pick("CoffeeTable_01", "side_table_01"), p, c.Range(0f, 360f), Pose.Upright);
            if (table == null) return;
            Fill(c, table, c.Range(0, 3), BookStackO, "chess_set", "tea_set_01", "plastic_bottle_gallon");
            int seats = c.Range(2, 4);
            float a0 = c.Range(0f, 360f);
            for (int i = 0; i < seats; i++)
            {
                float a = a0 + 360f / seats * i + c.Range(-15f, 15f);
                var dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad));
                c.Floor(c.Pick(SoftSeats), p + dir * c.Range(1.4f, 1.8f), c.Yaw(-dir) + Drift(c, 12f), Left(c, 0.01f));
            }
            if (c.Chance(0.6f)) c.Floor(RugO, p, c.Range(0f, 360f), Pose.Upright, false);
            if (c.Chance(0.5f)) c.Floor(c.Pick(PottedPlants[0], PottedPlants[1]), p + Jit(c, 2.6f), 0f, Left(c, 0.03f));
        }

        /// <summary>
        /// Repeats a furnished "unit" across a big room on a jittered grid (skipping doorways and filled spots),
        /// so a 20 m room reads as a shared office, a dormitory or a studio instead of one desk lost in a hall.
        /// </summary>
        public static int uSkip, uCover, uFree, uRun;   // unit statistics for tuning
        static void Units(RoomCtx c, float size, float skip, Action<Vector2, Vector2> unit)
        {
            var r = c.interior;
            int nx = Mathf.Max(1, Mathf.FloorToInt(r.width / size)), nz = Mathf.Max(1, Mathf.FloorToInt(r.height / size));
            float bw = r.width / nx, bh = r.height / nz;
            var face = c.Pick(new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (c.Chance(skip)) { uSkip++; continue; }
                    var area = new Rect(r.xMin + i * bw, r.yMin + j * bh, bw, bh);
                    if (c.Coverage(area) > 0.18f) { uCover++; continue; }
                    var p = area.center + Jit(c, Mathf.Min(bw, bh) * 0.08f);
                    if (!c.IsFree(p, 0.5f)) { uFree++; continue; }
                    uRun++;
                    unit(p, c.Chance(0.75f) ? face : c.Pick(new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1)));
                }
        }

        /// <summary>A freestanding run of bookcases back to back, used as a room divider in big offices.</summary>
        static void Divider(RoomCtx c, Vector2 p, Vector2 along, int n)
        {
            for (int k = 0; k < n; k++)
            {
                var q = p + along * (k - (n - 1) * 0.5f) * 1.4f;
                foreach (var side in new[] { 1f, -1f })
                {
                    var s = c.Floor(c.Pick("wooden_bookshelf_worn", "Shelf_01"), q + Perp(along) * side * 0.3f, c.Yaw(Perp(along) * side) + Drift(c, 2f), Pose.Upright, true, 0f);
                    FillShelves(c, s, c.Range(3, 8), BookRowO, BookRowO, BookStackO, "binder_notebook");
                }
            }
        }

        // ------------------------------------------------------------------ teaching rooms
        static void Lecture(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            Board(c, front, c.Chance(0.5f), 6f);
            c.AgainstWall("projector_screen", front, c.Chance(0.5f) ? 0.12f : 0.88f);
            c.Floor(LecternO, fr.At(fr.Width * c.Range(0.35f, 0.65f), 1.8f), c.Yaw(front.n) + Drift(c), Left(c, 0.01f));
            var desk = c.Floor("metal_office_desk", fr.At(fr.Width * 0.5f + c.Range(-2f, 2f), 2.6f), c.Yaw(front.n) + Drift(c, 4f), Pose.Upright);
            Fill(c, desk, c.Range(1, 3), "binder_notebook", "filmstrip_projector_8mm", "desk_lamp_arm_01", BookStackO);
            // raked seating: each tier a step higher (built up from the floor), desks and chairs set onto it
            float aisle = 1.4f, tierD = 1.45f, step = 0.18f, start = 4.2f;
            float width = fr.Width - aisle * 2f;
            int tiers = Mathf.Min(9, Mathf.FloorToInt((fr.Depth - start - 1.2f) / tierD));
            for (int t = 0; t < tiers; t++)
            {
                float sv = start + tierD * (t + 0.5f);
                float h = step * (t + 1);
                var plat = c.Floor(F(cc => ProcKit.TierPlatform(cc, width, tierD, h)), fr.At(fr.Width * 0.5f, sv), c.Yaw(-front.n), Pose.Upright, false, 0f);
                if (plat == null) continue;
                int seats = Mathf.FloorToInt(width / 0.95f);
                for (int k = 0; k < seats; k++)
                {
                    if (c.Chance(0.08f)) continue;
                    var p = fr.At(aisle + 0.95f * (k + 0.5f), sv - 0.2f);
                    c.PlaceOn(plat, "SchoolDesk_01", p + Jit(c, 0.03f), c.Yaw(-front.n) + Drift(c, 4f));
                    float pushed = c.Chance(0.25f) ? c.Range(0.1f, 0.25f) : 0f;
                    c.PlaceOn(plat, "SchoolChair_01", p + front.n * (0.5f + pushed), c.Yaw(-front.n) + Drift(c, pushed > 0 ? 30f : 8f), c.Chance(0.02f * (1 + c.mess)) ? Pose.OnBack : Pose.Upright);
                    if (c.Chance(0.06f)) c.PlaceOn(plat, c.Pick(PaperO, BookO, "binder_notebook"), p + Jit(c, 0.2f), c.Range(0f, 360f));
                }
            }
            foreach (var s in Sides(c, front)) if (!s.outer) Decorate(c, 0.6f, BannerO, PosterO, "wall_clock", NoticeO);
            Decorate(c, 0.2f, ExitO, "fire_alarm");
        }

        static void Classroom(RoomCtx c, bool geography)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            int style = c.Style("classroom", 3);
            Board(c, front, c.Chance(0.6f), 4.5f);
            DeskWithChair(c, c.Pick("metal_office_desk", "wooden_table_02"), "dining_chair_02", fr.At(fr.Width * c.Range(0.25f, 0.4f), 1.6f), -front.n, c.Range(1, 4), DeskClutter);
            if (geography) c.AgainstWall(GlobeO, front, c.Chance(0.5f) ? 0.85f : 0.15f);
            else if (c.Chance(0.4f)) c.AgainstWall("standing_chalkboard_01", front, c.Chance(0.5f) ? 0.9f : 0.1f);
            float startV = 3.4f, rowGap = c.Range(1.5f, 1.8f);
            int rows = Mathf.Min(8, Mathf.FloorToInt((fr.Depth - startV - 2.5f) / rowGap) + 1);
            if (geography || style == 2)
            {
                // tables of four
                for (int r = 0; r < Mathf.Max(1, rows / 2); r++)
                    for (float su = 2.5f; su < fr.Width - 2.5f; su += c.Range(3.4f, 4f))
                    {
                        var t = c.Floor("wooden_table_02", fr.At(su, startV + 0.5f + r * 3.4f), c.Yaw(fr.u) + Drift(c, 5f), Left(c, 0.01f));
                        Fill(c, t, geography ? 1 : c.Range(0, 2), geography ? new object[] { GlobeO, "binder_notebook", BookO } : new object[] { BookStackO, PaperO });
                        ChairsAround(c, t, StudentChairs);
                    }
            }
            else
            {
                // rows of single desks (style 0) or spaced pairs (style 1)
                int cols = Mathf.FloorToInt((fr.Width - 2.2f) / (style == 0 ? 1.15f : 1.6f));
                for (int r = 0; r < rows; r++)
                    for (int k = 0; k < cols; k++)
                    {
                        if (c.Chance(0.06f)) continue;
                        float su = 1.1f + (k + 0.5f) * (fr.Width - 2.2f) / cols;
                        DeskWithChair(c, "SchoolDesk_01", "SchoolChair_01", fr.At(su, startV + r * rowGap) + Jit(c, 0.05f), -front.n, c.Chance(0.12f) ? 1 : 0, BookO, PaperO, "binder_notebook");
                    }
            }
            var back = Opposite(c, front);
            if (back != null && !back.outer)
            {
                Line(c, back, 0.45f, () => c.Pick(Bookcases[0], Bookcases[1], "drawer_cabinet", "painted_wooden_cabinet"),
                    s => FillShelves(c, s, c.Range(3, 9), BookRowO, BookRowO, BookStackO, "binder_notebook", GlobeO));
                Decorate(c, 0.3f, NoticeO, PosterO);
            }
            foreach (var s in Sides(c, front)) if (!s.outer) Decorate(c, 0.5f, geography ? new[] { MapO, MapO, PosterO } : new[] { PosterO, PosterO, NoticeO, "wall_clock", PeriodicO });
            Decorate(c, 0.15f, "wall_clock", "fire_alarm");
        }

        static void Seminar(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            Board(c, front, false, 3.5f);
            c.AgainstWall("projector_screen", front, c.Chance(0.5f) ? 0.15f : 0.85f);
            float cx = fr.Width * 0.5f, depth = Mathf.Min(fr.Depth - 2.5f, 7f);
            for (float sv = 3f; sv < 3f + depth; sv += 1.6f)
                foreach (var side in new[] { -1f, 1f })
                {
                    var t = c.Floor("wooden_table_02", fr.At(cx + side * 2.4f, sv), c.Yaw(fr.v) + Drift(c, 3f), Left(c, 0.01f));
                    Fill(c, t, c.Range(0, 2), PaperO, "binder_notebook", BookO);
                    if (t != null) c.Floor(c.Pick(OfficeChairs), fr.At(cx + side * 3.2f, sv), c.Yaw(-fr.u * side) + Drift(c, 20f), Left(c));
                }
            for (float su = cx - 1.6f; su <= cx + 1.6f; su += 1.6f)
            {
                var t = c.Floor("wooden_table_02", fr.At(su, 3f + depth + 0.3f), c.Yaw(fr.u) + Drift(c, 3f), Pose.Upright);
                if (t != null) c.Floor(c.Pick(OfficeChairs), fr.At(su, 3f + depth + 1.1f), c.Yaw(-fr.v) + Drift(c, 20f), Left(c));
            }
            Units(c, 6f, 0.2f, (p, f) =>
            {
                var t = c.Floor(c.Pick("dining_table", "wooden_table_02"), p, c.Yaw(f) + Drift(c, 5f), Left(c, 0.01f));
                Fill(c, t, c.Range(0, 3), PaperO, "binder_notebook", BookStackO, "tea_set_01");
                ChairsAround(c, t, OfficeChairs);
            });
            Decorate(c, 0.5f, PosterO, NoticeO, "wall_clock", Frames[2]);
            if (Area(c) > 250) Line(c, Opposite(c, front), 0.4f, () => c.Pick(Bookcases), s => FillShelves(c, s, c.Range(3, 8), BookRowO, BookStackO));
        }

        /// <summary>A strict grid of single desks: the most unsettling room when empty.</summary>
        static void Exam(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            c.OnWall("wall_clock", front, 0.5f, 2.6f);
            Board(c, front, false, 3f);
            DeskWithChair(c, "wooden_table_02", "dining_chair_02", fr.At(fr.Width * 0.5f, 1.7f), -front.n, 2, "clipboard", PaperO, "alarm_clock_01");
            float gapU = 1.5f, gapV = 1.6f;
            int cols = Mathf.FloorToInt((fr.Width - 2f) / gapU), rows = Mathf.FloorToInt((fr.Depth - 4.5f) / gapV);
            float off = (fr.Width - cols * gapU) * 0.5f;
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    var p = fr.At(off + gapU * (k + 0.5f), 3.6f + r * gapV);
                    var d = c.Floor("SchoolDesk_01", p, c.Yaw(-front.n) + Drift(c, 2f), Pose.Upright);
                    if (d != null && c.Chance(0.55f)) c.OnSurface(d, () => PaperO, 1);
                    float pushed = c.Chance(0.12f) ? c.Range(0.2f, 0.5f) : 0f;
                    c.Floor("SchoolChair_01", p + front.n * (0.6f + pushed), c.Yaw(-front.n) + Drift(c, pushed > 0 ? 40f : 3f), c.Chance(0.01f * (1 + c.mess)) ? Pose.OnBack : Pose.Upright);
                }
            Decorate(c, 0.3f, NoticeO, "wall_clock");
        }

        static void Lab(RoomCtx c, int kind)   // 0 chemistry, 1 biology, 2 physics, 3 darkroom
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            Board(c, front, false, 3.5f);
            object[] bench = kind == 0 ? new object[] { "chemistry_set", "bunsen_burner", "bunsen_burner", "multi_cleaner_bottle", "binder_notebook", JarO }
                           : kind == 1 ? new object[] { "vintage_microscope", "industrial_microscope", "vintage_microscope", JarO, JarO, "magnifying_glass_01", "binder_notebook" }
                           : kind == 2 ? new object[] { "retro_multimeter", "circuit_board", "retro_multimeter", "binoculars", "magnifying_glass_01", "binder_notebook" }
                           : new object[] { TrayO, TrayO, "Camera_01", "plastic_container", "bleach_bottle" };
            for (float sv = 3.4f; sv < fr.Depth - 2.4f; sv += 3.2f)
                for (float su = 2.4f; su < fr.Width - 2.2f; su += 4.4f)
                {
                    float len = Mathf.Min(3.2f, fr.Width - su - 1.4f);
                    if (len < 1.6f) continue;
                    var b = c.Floor(ProcKit.LabBenchV(len), fr.At(su + len * 0.5f, sv), c.Yaw(-front.n), Pose.Upright);
                    if (b == null) continue;
                    Fill(c, b, c.Range(1, 4), bench);
                    var bb = PropLibrary.RendererBounds(b);
                    var mid = new Vector2(bb.center.x, bb.center.z);
                    for (int s = 0; s < 3; s++)
                        if (c.Chance(0.75f)) c.Floor(c.Pick("metal_stool_01", "wooden_stool_01"), mid + fr.u * (-len * 0.33f + s * len * 0.33f) + front.n * c.Range(0.6f, 0.9f), c.Range(0f, 360f), Left(c, 0.03f));
                }
            if (kind == 0) Line(c, Opposite(c, front), 0.5f, () => FumeHoodO, h => Fill(c, h, c.Range(0, 3), "chemistry_set", "bunsen_burner", JarO));
            if (kind == 2) { c.FloorAnywhere(TelescopeO, Pose.Upright); c.FloorAnywhere(GlobeO, Pose.Upright); }
            if (kind == 1) { var v = c.FloorAnywhere(VitrineO, Pose.Upright); Fill(c, v, c.Range(3, 7), JarO, JarO, "fern_02_0", "horse_statue_01", "magnifying_glass_01"); }
            foreach (var s in Sides(c, front))
                if (!s.outer) Line(c, s, 0.35f, () => c.Pick("steel_frame_shelves_02", "steel_frame_shelves_03", "drawer_cabinet"),
                    sh => FillShelves(c, sh, c.Range(3, 9), kind == 1 ? new object[] { JarO, JarO, BookRowO, "potted_plant_04" } : new object[] { Bottles[0], Bottles[1], Bottles[2], JarO, BookRowO, "plastic_container", "medical_box" }));
            Decorate(c, 0.5f, kind == 0 ? new object[] { PeriodicO, PosterO, PosterO, "fire_alarm" } : new object[] { PosterO, PosterO, NoticeO, "wall_clock" });
            c.AgainstWall("korean_fire_extinguisher_01", c.RandomWall(1f), c.Range(0.1f, 0.9f));
        }

        static void Workshop(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            for (float sv = 3f; sv < fr.Depth - 2f; sv += 3.4f)
                for (float su = 2.5f; su < fr.Width - 2f; su += 4.2f)
                {
                    var b = c.Floor(WorkbenchO, fr.At(su, sv), c.Yaw(-front.n) + Drift(c, 4f), Pose.Upright);
                    Fill(c, b, c.Range(1, 4), "bench_vice_01", "metal_toolbox", "retro_multimeter", "circuit_board", "spray_paint_bottles", "garden_gloves_01");
                    if (b != null && c.Chance(0.6f)) c.Floor("metal_stool_01", fr.At(su + c.Range(-0.6f, 0.6f), sv + 0.9f), c.Range(0f, 360f), Left(c));
                }
            foreach (var w in c.walls) if (!w.outer && w.Length > 3f) Hang(c, PegO, w, c.Range(0.2f, 0.8f));
            Line(c, Opposite(c, front), 0.5f, () => c.Pick("drill_press_01", "metal_tool_chest", "steel_frame_shelves_01", "tool_cart"),
                s => FillShelves(c, s, c.Range(2, 6), "metal_toolbox", "plastic_container", "spray_paint_bottles"));
            c.FloorAnywhere(c.Pick("wooden_ladder_02", "ladder_sectioned_01"), Pose.Upright);
            for (int i = 0; i < c.Range(1, 3); i++) c.FloorAnywhere(F(ProcKit.Plank), Pose.OnBack, false);
            Decorate(c, 0.4f, PosterO, NoticeO, "wall_clock");
            c.AgainstWall("korean_fire_extinguisher_01", c.RandomWall(1f), c.Range(0.1f, 0.9f));
        }

        static void ComputerLab(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            Board(c, front, false, 3.5f);
            for (float sv = 3.2f; sv < fr.Depth - 1.8f; sv += 2.3f)
                for (float su = 1.6f; su < fr.Width - 2.6f; su += 2.15f)
                {
                    var d = c.Floor("metal_office_desk", fr.At(su + 1f, sv), c.Yaw(-front.n) + Drift(c, 2f), Pose.Upright);
                    if (d == null) continue;
                    Fill(c, d, 2, "television_02");
                    Fill(c, d, c.Range(0, 2), "office_notepads", PaperO);
                    var bb = PropLibrary.RendererBounds(d);
                    foreach (var k in new[] { -0.5f, 0.5f })
                        c.Floor(c.Pick(OfficeChairs), new Vector2(bb.center.x, bb.center.z) + fr.u * k + front.n * c.Range(0.75f, 1.2f), c.Yaw(-front.n) + Drift(c, 30f), Left(c));
                }
            c.FloorAnywhere(RackO, Pose.Upright);
            Decorate(c, 0.4f, PosterO, NoticeO, "wall_clock", "security_camera_01");
        }

        // ------------------------------------------------------------------ books
        static void Library(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            // issue desk, card catalogue and reading tables at the front, then the stacks
            var counter = c.Floor(ProcKit.CounterV(3.2f), fr.At(fr.Width * 0.25f, 2.2f), c.Yaw(front.n), Pose.Upright);
            Fill(c, counter, c.Range(2, 4), BookStackO, "desk_lamp_arm_01", "vintage_stapler", "binder_notebook", "alarm_clock_01");
            c.Floor("vintage_wooden_drawer_01", fr.At(fr.Width * 0.6f, 1.6f), c.Yaw(front.n), Pose.Upright);
            for (int i = 0; i < 2; i++)
            {
                var t = c.Floor("dining_table", fr.At(fr.Width * (0.35f + 0.3f * i), 4.6f), c.Yaw(fr.u) + Drift(c, 3f), Pose.Upright);
                Fill(c, t, c.Range(2, 4), "desk_lamp_arm_01", BookStackO, BookO, "magnifying_glass_01");
                ChairsAround(c, t, new[] { "dining_chair_02" });
            }
            bool alongU = c.Chance(0.5f);
            float rowGap = 2.2f;
            float limitS = alongU ? fr.Depth - 1.4f : fr.Width - 1.4f, limitT = alongU ? fr.Width - 1.4f : fr.Depth - 1.4f;
            int row = 0;
            for (float s = alongU ? 7.2f : 1.8f; s < limitS; s += rowGap, row++)
                for (float t = alongU ? 1.6f : 7.2f; t < limitT; t += 1.4f)
                {
                    if (c.Chance(0.05f)) { t += 0.8f; continue; }   // a gap where a case was taken out
                    var p = alongU ? fr.At(t, s) : fr.At(s, t);
                    var face = alongU ? fr.v : fr.u;
                    bool fallen = c.Chance(0.012f * (1 + c.mess));
                    var sh = c.Floor("wooden_bookshelf_worn", p, c.Yaw(row % 2 == 0 ? face : -face) + Drift(c, 2f), fallen ? c.Pick(Pose.OnBack, Pose.OnFront) : Pose.Upright, true, 0.02f);
                    if (sh == null) continue;
                    if (fallen) { for (int i = 0; i < c.Range(4, 9); i++) c.Floor(BookO, p + Jit(c, 1.3f), c.Range(0f, 360f), Pose.Upright, false, 0f); }
                    else FillShelves(c, sh, c.Range(5, 10), BookRowO, BookRowO, BookRowO, BookStackO);
                    if (c.Chance(0.04f)) c.Floor(BookStackO, p + face * 0.9f + Jit(c, 0.3f), c.Range(0f, 360f), Pose.Upright, false);
                }
            c.FloorAnywhere("wooden_ladder_02", Pose.Upright);
            c.AgainstWall("vintage_grandfather_clock_01", c.RandomWall(1.5f), c.Range(0.1f, 0.9f));
            var ped = c.FloorAnywhere(PedestalO, Pose.Upright); Fill(c, ped, 1, "marble_bust_01");
            Decorate(c, 0.4f, Frames[0], Frames[3], BannerO, "wall_clock", NoticeO);
            for (int i = 0; i < c.Range(0, 2) + c.mess; i++) c.FloorAnywhere(BookO, Pose.Upright, false);
        }

        static void Archive(RoomCtx c)
        {
            var fr = new Frame(c, c.LongestWall());
            int row = 0;
            for (float sv = 1.8f; sv < fr.Depth - 1.4f; sv += 2.1f, row++)
                for (float su = 1.4f; su < fr.Width - 1.2f; su += 2.4f)
                {
                    var sh = c.Floor(c.Pick(MetalShelves), fr.At(su, sv), c.Yaw(row % 2 == 0 ? fr.v : -fr.v) + Drift(c, 2f), Pose.Upright, true, 0.02f);
                    FillShelves(c, sh, c.Range(4, 10), "cardboard_box_01", "binder_notebook", BookRowO, "plastic_crate_02", "book_encyclopedia_set_01");
                }
            Line(c, c.RandomWall(2f), 0.6f, () => "drawer_cabinet");
            DeskWithChair(c, "wooden_table_02", "dining_chair_02", c.RandomPoint(1.5f), c.Pick(new Vector2(1, 0), new Vector2(0, 1)), c.Range(1, 4), "magnifying_glass_01", "desk_lamp_arm_01", BookStackO, PaperO);
            Papers(c, c.Range(0, 4));
        }

        static void Reading(RoomCtx c)
        {
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.7f, () => "wooden_bookshelf_worn", s => FillShelves(c, s, c.Range(5, 10), BookRowO, BookRowO, BookStackO));
            var spots = new List<Vector2>();
            Units(c, 4.2f, 0.1f, (q, f) => spots.Add(q));
            foreach (var p in spots)
            {
                var t = c.Floor(c.Pick("side_table_01", "side_table_tall_01", "CoffeeTable_01"), p, c.Range(0f, 360f), Pose.Upright);
                if (t == null) continue;
                Fill(c, t, c.Range(1, 3), "desk_lamp_arm_01", BookO, BookStackO, "chess_set", "tea_set_01");
                for (int k = 0; k < c.Range(1, 3); k++)
                {
                    float a = c.Range(0f, 360f);
                    var dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad));
                    c.Floor(c.Pick("ArmChair_01", "modern_arm_chair_01"), p + dir * 1.1f, c.Yaw(-dir) + Drift(c, 20f), Left(c, 0.01f));
                }
            }
            c.FloorAnywhere(RugO, Pose.Upright, false);
            c.AgainstWall("vintage_grandfather_clock_01", c.RandomWall(1f), c.Range(0.1f, 0.9f));
            Decorate(c, 0.3f, Frames[0], Frames[1], "wall_clock");
        }

        static void Study(RoomCtx c)
        {
            var fr = new Frame(c, Front(c));
            for (float sv = 2f; sv < fr.Depth - 1.6f; sv += 2.4f)
                for (float su = 1.6f; su < fr.Width - 1.6f; su += 1.25f)
                {
                    // study carrels: a small desk with a low panel in front of it
                    var d = DeskWithChair(c, "small_wooden_table_01", c.Pick(StudentChairs), fr.At(su, sv), fr.v, c.Range(0, 3), "desk_lamp_arm_01", BookStackO, BookO, PaperO);
                    if (d == null) continue;
                    c.Floor(ProcKit.PartitionV(1.1f, 1.2f), fr.At(su, sv + 0.42f), c.Yaw(fr.v), Pose.Upright, true, 0f);
                }
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.35f, () => c.Pick(Bookcases), s => FillShelves(c, s, c.Range(3, 8), BookRowO, BookStackO));
            Decorate(c, 0.3f, NoticeO, "wall_clock", PosterO);
        }

        static void Art(RoomCtx c)
        {
            if (Area(c) > 200)
            {
                Units(c, 6.8f, 0.05f, (p, f) => { if (c.Chance(0.7f)) EaselCircle(c, p); else { var t = c.Floor("dining_table", p, c.Yaw(f), Left(c, 0.01f)); Fill(c, t, c.Range(2, 5), "spray_paint_bottles", "plastic_container", PaperO, "planter_pot_clay", "wicker_basket_01"); ChairsAround(c, t, new[] { "wooden_stool_01", "folding_wooden_stool" }); } });
                ArtWalls(c);
                return;
            }
            EaselCircle(c, c.interior.center + Jit(c, 1f));
            ArtWalls(c);
        }

        static void EaselCircle(RoomCtx c, Vector2 center)
        {
            var ped = c.Floor(PedestalO, center, c.Range(0f, 360f), Pose.Upright);
            Fill(c, ped, 1, "marble_bust_01", "horse_statue_01", "potted_plant_04", "planter_pot_clay");
            int easels = c.Range(6, 11);
            for (int i = 0; i < easels; i++)
            {
                float a = 360f / easels * i + c.Range(-10f, 10f);
                var dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad));
                var p = center + dir * c.Range(2.6f, 3.4f);
                c.Floor(EaselO, p, c.Yaw(-dir) + Drift(c, 10f), Left(c, 0.03f));
                if (c.Chance(0.7f)) c.Floor(c.Pick("wooden_stool_01", "folding_wooden_stool"), p + dir * 0.8f, c.Range(0f, 360f), Left(c));
            }
        }

        static void ArtWalls(RoomCtx c)
        {
            foreach (var w in c.walls)
                if (!w.outer && w.Length > 3f)
                {
                    var t = c.AgainstWall(c.Pick("wooden_table_02", "dining_table"), w, c.Range(0.2f, 0.8f));
                    Fill(c, t, c.Range(2, 5), "spray_paint_bottles", "plastic_container", "wicker_basket_01", PaperO, "planter_pot_clay");
                }
            for (int i = 0; i < c.Range(2, 6); i++) c.AgainstWall(c.Pick(Frames), c.RandomWall(1f), c.Range(0.05f, 0.95f), Pose.Upright, 0.03f, 0f, true, c.Range(8f, 15f));
            if (c.Chance(0.5f)) c.FloorAnywhere("spinning_wheel_01", Pose.Upright);
            Decorate(c, 0.6f, Frames[0], Frames[1], Frames[2], PosterO);
        }

        static void Music(RoomCtx c, bool practice)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            c.AgainstWall(PianoO, front, c.Range(0.15f, 0.4f));
            if (!practice)
            {
                var focus = fr.At(fr.Width * 0.5f, 2.2f);
                c.Floor(StandO, focus, c.Yaw(front.n), Pose.Upright);
                for (int ring = 0; ring < 3; ring++)
                    for (float a = -70f; a <= 70f; a += 17f)
                    {
                        if (c.Chance(0.12f)) continue;
                        var dir = (Vector2)(Quaternion.Euler(0, 0, a) * fr.v);
                        var p = focus + dir * (2.4f + ring * 1.5f);
                        c.Floor(c.Pick("SchoolChair_01", "dining_chair_02"), p, c.Yaw(-dir) + Drift(c, 12f), Left(c, 0.02f));
                        if (c.Chance(0.6f)) c.Floor(StandO, p - dir * 0.65f, c.Yaw(dir), Left(c, 0.04f));
                        if (c.Chance(0.06f)) c.Floor("Ukulele_01", p + Jit(c, 0.4f), c.Range(0f, 360f), Pose.OnBack, false);
                    }
            }
            else
            {
                c.Floor(F(cc => ProcKit.Bench(cc, 1f)), fr.At(fr.Width * 0.3f, 1.5f), c.Yaw(-front.n), Pose.Upright);
                c.FloorAnywhere(StandO, Pose.Upright);
                c.FloorAnywhere("Ukulele_01", Pose.OnBack, false);
            }
            Line(c, Opposite(c, front), 0.3f, () => c.Pick("Shelf_01", "drawer_cabinet"), s => FillShelves(c, s, c.Range(2, 6), BookRowO, "boombox", "cassette_player", "Ukulele_01"));
            Decorate(c, 0.5f, PosterO, Frames[1], "wall_clock", NoticeO);
        }

        // ------------------------------------------------------------------ offices and staff
        static void StaffRoom(RoomCtx c)
        {
            var kitchen = Front(c);
            float len = Mathf.Min(kitchen.Length - 1.4f, c.Range(2.6f, 4.2f));
            var counter = c.AgainstWall(ProcKit.CounterV(len, 0.65f, 0.92f), kitchen, c.Range(0.25f, 0.6f));
            Fill(c, counter, c.Range(3, 6), "vintage_microwave", "vintage_electric_kettle", "tea_set_01", "plastic_bottle_gallon", "multi_cleaner_bottle");
            c.AgainstWall(FridgeO, kitchen, 0.92f);
            int tables = Mathf.Clamp(Mathf.RoundToInt(Area(c) / 80f), 1, 4);
            for (int i = 0; i < tables; i++)
            {
                var t = c.FloorAnywhere("dining_table", Left(c, 0.01f), true, 12);
                Fill(c, t, c.Range(1, 3), "tea_set_01", BookO, PaperO, "alarm_clock_01");
                ChairsAround(c, t, new[] { "dining_chair_02" });
            }
            Units(c, 6.5f, 0.25f, (p, f) => { if (c.Chance(0.55f)) LoungeGroup(c, p); else { var t2 = c.Floor("dining_table", p, c.Yaw(f) + Drift(c, 5f), Left(c, 0.01f)); Fill(c, t2, c.Range(0, 3), "tea_set_01", BookO, PaperO); ChairsAround(c, t2, new[] { "dining_chair_02" }); } });
            Line(c, Opposite(c, kitchen), 0.35f, () => c.Pick("Shelf_01", "drawer_cabinet", "painted_wooden_cabinet"), s => FillShelves(c, s, c.Range(2, 6), BookRowO, "binder_notebook", "cardboard_box_01"));
            Decorate(c, 0.6f, NoticeO, NoticeO, "wall_clock", Frames[1], PosterO);
        }

        static void Dean(RoomCtx c)
        {
            var back = Front(c);
            var fr = new Frame(c, back);
            float dv = Mathf.Min(3f, fr.Depth * 0.35f);
            DeskWithChair(c, "metal_office_desk", "modern_arm_chair_01", fr.At(fr.Width * 0.5f, dv), -back.n, c.Range(3, 6), "desk_lamp_arm_01", "mantel_clock_01", "standing_picture_frame_01", "binder_notebook", BookStackO, "magnifying_glass_01");
            for (int k = -1; k <= 1; k += 2) c.Floor("dining_chair_02", fr.At(fr.Width * 0.5f + k * 0.6f, dv + 1.2f), c.Yaw(back.n) + Drift(c, 15f), Left(c, 0.01f));
            Line(c, back, 0.7f, () => c.Pick("wooden_bookshelf_worn", "vintage_cabinet_01", "GothicCabinet_01"), s => FillShelves(c, s, c.Range(4, 9), BookRowO, BookRowO, "book_encyclopedia_set_01", "mantel_clock_01"));
            LoungeGroup(c, c.RandomPoint(2.4f));
            c.Floor(RugO, fr.At(fr.Width * 0.5f, dv + 0.5f), c.Yaw(back.n), Pose.Upright, false);
            c.AgainstWall("vintage_grandfather_clock_01", c.RandomWall(1f), c.Range(0.1f, 0.9f));
            c.FloorAnywhere(GlobeO, Pose.Upright);
            var ped = c.FloorAnywhere(PedestalO, Pose.Upright); Fill(c, ped, 1, "marble_bust_01", "bronze_whale_statue");
            c.OnCeiling("Chandelier_01", c.interior.center + Jit(c, 1f));
            if (Area(c) > 150) Units(c, 7f, 0.3f, (p, f) => { if (c.Chance(0.5f)) LoungeGroup(c, p); else Divider(c, p, Perp(f), c.Range(2, 3)); });
            Decorate(c, 0.7f, Frames[0], Frames[0], Frames[3], BannerO, "ornate_mirror_01", "wall_clock");
            if (c.room.role == "warden") { c.FloorAnywhere("old_bed_frame", Pose.Upright); c.FloorAnywhere(c.Pick("painted_wooden_cabinet", "vintage_cabinet_01"), Pose.Upright); }
        }

        static void Professor(RoomCtx c)
        {
            var main = Front(c);
            var fr = new Frame(c, main);
            int style = c.Style("professor", 3);
            float dv = Mathf.Min(2.8f, fr.Depth * 0.4f);
            DeskWithChair(c, c.Pick("metal_office_desk", "wooden_table_02"), c.Pick("modern_arm_chair_01", "dining_chair_02"), fr.At(fr.Width * c.Range(0.35f, 0.65f), dv), -main.n, c.Range(3, 6),
                "desk_lamp_arm_01", BookStackO, BookStackO, "binder_notebook", PaperO, "standing_picture_frame_02", style == 1 ? "binoculars" : "magnifying_glass_01", style == 2 ? "circuit_board" : "office_notepads");
            c.Floor("dining_chair_02", fr.At(fr.Width * 0.5f + c.Range(-0.8f, 0.8f), dv + 1.3f), c.Yaw(main.n) + Drift(c, 25f), Left(c, 0.01f));
            Line(c, main, 0.75f, () => c.Pick("wooden_bookshelf_worn", "Shelf_01", "wooden_bookshelf_worn"), s => FillShelves(c, s, c.Range(5, 10), BookRowO, BookRowO, BookStackO, "binder_notebook"));
            var side = Sides(c, main).Find(x => !x.outer);
            Line(c, side, 0.3f, () => c.Pick("drawer_cabinet", "painted_wooden_cabinet"), s => Fill(c, s, c.Range(0, 2), "potted_plant_04", BookStackO, "mantel_clock_01"));
            if (style == 1) c.FloorAnywhere(TelescopeO, Pose.Upright);
            if (style == 2) c.FloorAnywhere(GlobeO, Pose.Upright);
            if (Area(c) > 150)
            {
                // a big room is a shared faculty office: more desks, bookcase dividers, a small sitting area
                Units(c, 5.2f, 0.12f, (p, f) =>
                {
                    int kind = c.Range(0, 4);
                    if (kind == 0) Divider(c, p, Perp(f), c.Range(2, 4));
                    else if (kind == 1) LoungeGroup(c, p);
                    else
                    {
                        DeskWithChair(c, c.Pick("metal_office_desk", "wooden_table_02"), c.Pick(OfficeChairs), p, f, c.Range(2, 5), "desk_lamp_arm_01", BookStackO, "binder_notebook", PaperO, "television_02", "standing_picture_frame_01");
                        var cab = c.Floor(c.Pick("drawer_cabinet", "painted_wooden_cabinet"), p - f * 1.8f + Perp(f) * 1.3f, c.Yaw(f), Left(c, 0.01f));
                        Fill(c, cab, c.Range(0, 2), "potted_plant_04", BookStackO);
                    }
                });
            }
            if (c.Chance(0.5f)) c.FloorAnywhere(c.Pick(PottedPlants[0], PottedPlants[1]), Left(c, 0.03f));
            for (int i = 0; i < c.Range(0, 3); i++) c.FloorAnywhere(BookStackO, Pose.Upright, false);
            Decorate(c, 0.6f, Frames[0], Frames[1], Frames[3], PosterO, NoticeO, "wall_clock", MapO);
        }

        /// <summary>Admissions, bursar, enquiries: a service counter, staff desks behind it, chairs for the queue in front.</summary>
        static void Counter(RoomCtx c)
        {
            var back = Front(c);
            var fr = new Frame(c, back);
            float cv = Mathf.Min(4f, fr.Depth * 0.4f);
            var counter = c.Floor(ProcKit.CounterV(Mathf.Clamp(fr.Width - 4f, 2f, 6f), 0.7f, 1.05f), fr.At(fr.Width * 0.5f, cv), c.Yaw(back.n), Pose.Upright);
            Fill(c, counter, c.Range(2, 5), "binder_notebook", "vintage_stapler", PaperO, "CashRegister_01", "clipboard", "office_notepads");
            for (float su = 2f; su < fr.Width - 2f; su += 2.6f)
                DeskWithChair(c, "metal_office_desk", c.Pick(OfficeChairs), fr.At(su, cv * 0.45f), back.n, c.Range(1, 4), "television_02", "binder_notebook", PaperO, "desk_lamp_arm_01");
            Line(c, back, 0.6f, () => "drawer_cabinet", s => Fill(c, s, c.Range(0, 2), "cardboard_box_01", "binder_notebook"));
            for (float su = 2f; su < fr.Width - 2f; su += 0.65f)
                if (c.Chance(0.7f)) c.Floor(c.Pick("dining_chair_02", "plastic_monobloc_chair_01"), fr.At(su, cv + 3f), c.Yaw(-back.n) + Drift(c, 8f), Left(c, 0.02f));
            if (c.Chance(0.6f)) c.FloorAnywhere(c.Pick(PottedPlants[0], PottedPlants[1]), Left(c));
            Decorate(c, 0.6f, NoticeO, NoticeO, PosterO, "wall_clock", BannerO);
        }

        static void Records(RoomCtx c, bool print)
        {
            var fr = new Frame(c, c.LongestWall());
            if (print) foreach (var w in c.walls) if (!w.outer) c.AgainstWall(CopierO, w, c.Range(0.2f, 0.8f));
            int row = 0;
            for (float sv = 1.8f; sv < fr.Depth - 1.4f; sv += 2.2f, row++)
                for (float su = 1.2f; su < fr.Width - 1f; su += 1.2f)
                {
                    if (c.Chance(0.1f)) { su += 0.8f; continue; }
                    var cab = c.Floor("drawer_cabinet", fr.At(su, sv), c.Yaw(row % 2 == 0 ? fr.v : -fr.v) + Drift(c, 2f), Pose.Upright, true, 0.01f);
                    if (cab != null && c.Chance(0.3f)) Fill(c, cab, 1, "cardboard_box_01", "binder_notebook");
                }
            var t = c.FloorAnywhere("wooden_table_02", Pose.Upright);
            Fill(c, t, c.Range(2, 5), "binder_notebook", PaperO, "clipboard", "cardboard_box_01");
            Papers(c, c.Range(0, 5));
            Decorate(c, 0.3f, NoticeO, "wall_clock");
        }

        static void Workroom(RoomCtx c)
        {
            var fr = new Frame(c, Front(c));
            float pitch = 3.2f;
            for (float sv = 2.2f; sv < fr.Depth - 2f; sv += pitch)
                for (float su = 1.8f; su < fr.Width - 1.8f; su += pitch)
                {
                    var cell = fr.At(su, sv);
                    c.Floor(ProcKit.PartitionV(pitch - 0.3f), cell + fr.v * (pitch * 0.5f - 0.1f), c.Yaw(fr.v), Pose.Upright, true, 0.01f);
                    c.Floor(ProcKit.PartitionV(pitch - 0.6f), cell + fr.u * (pitch * 0.5f - 0.1f), c.Yaw(fr.u), Pose.Upright, true, 0.01f);
                    DeskWithChair(c, "metal_office_desk", c.Pick(OfficeChairs), cell + fr.v * 0.6f, fr.v, c.Range(1, 4), "television_02", "desk_lamp_arm_01", "binder_notebook", PaperO, "standing_picture_frame_01", BookStackO);
                }
            c.FloorAnywhere(CopierO, Pose.Upright);
            Decorate(c, 0.4f, NoticeO, "wall_clock", PosterO);
        }

        static void Senate(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            // long ceremonial table of joined tables, chairs all round, lectern at the head
            float len = Mathf.Min(fr.Depth - 5f, 12f);
            for (float sv = 3.5f; sv < 3.5f + len; sv += 2.3f)
            {
                var t = c.Floor("dining_table", fr.At(fr.Width * 0.5f, sv), c.Yaw(fr.u), Pose.Upright, true, 0f);
                Fill(c, t, c.Range(1, 3), "binder_notebook", PaperO, "clipboard");
                ChairsAround(c, t, new[] { "dining_chair_02" });
            }
            c.Floor(LecternO, fr.At(fr.Width * 0.5f, 1.6f), c.Yaw(front.n), Pose.Upright);
            foreach (var s in Sides(c, front))
                if (!s.outer) Line(c, s, 0.75f, () => c.Chance(0.8f) ? "dining_chair_02" : (object)"side_table_tall_01");
            Units(c, 7f, 0.5f, (p, f) => LoungeGroup(c, p));
            c.Floor(RugO, fr.At(fr.Width * 0.5f, 3.5f + len * 0.5f), c.Yaw(fr.u), Pose.Upright, false);
            for (int i = 0; i < 3; i++) c.OnCeiling("Chandelier_01", fr.At(fr.Width * 0.5f + c.Range(-0.5f, 0.5f), 4f + i * len / 3f));
            Decorate(c, 0.8f, BannerO, BannerO, Frames[0], Frames[3], "wall_clock");
        }

        static void Meeting(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            var t = c.Floor("dining_table", fr.At(fr.Width * 0.5f, fr.Depth * 0.5f), c.Yaw(fr.v), Pose.Upright);
            Fill(c, t, c.Range(1, 4), "binder_notebook", PaperO, "clipboard", "tea_set_01");
            ChairsAround(c, t, new[] { "dining_chair_02", "modern_arm_chair_01" }, true);
            c.AgainstWall("projector_screen", front, 0.5f);
            Board(c, Sides(c, front).Find(x => !x.outer), false, 3f);
            if (Area(c) > 150) Units(c, 5.8f, 0.15f, (p, f) => { if (c.Chance(0.4f)) LoungeGroup(c, p); else { var t2 = c.Floor("wooden_table_02", p, c.Yaw(f), Left(c, 0.01f)); Fill(c, t2, c.Range(0, 3), PaperO, "binder_notebook", "tea_set_01"); ChairsAround(c, t2, new[] { "dining_chair_02" }); } });
            Decorate(c, 0.4f, Frames[2], "wall_clock", PosterO);
        }

        static void Server(RoomCtx c, bool helpdesk)
        {
            var fr = new Frame(c, c.LongestWall());
            if (!helpdesk)
            {
                int row = 0;
                for (float sv = 1.8f; sv < fr.Depth - 1.5f; sv += 2.6f, row++)
                    for (float su = 1.4f; su < fr.Width - 1.2f; su += 0.62f)
                        if (!c.Chance(0.08f)) c.Floor(RackO, fr.At(su, sv), c.Yaw(row % 2 == 0 ? fr.v : -fr.v), Pose.Upright, true, 0.005f);
            }
            else
            {
                for (float su = 2f; su < fr.Width - 2f; su += 2.6f)
                    DeskWithChair(c, "metal_office_desk", c.Pick(OfficeChairs), fr.At(su, 2.2f), -fr.v, c.Range(2, 4), "television_02", "Television_01", "circuit_board", "retro_multimeter", "binder_notebook");
                Line(c, Opposite(c, c.LongestWall()), 0.5f, () => c.Pick(MetalShelves), s => FillShelves(c, s, c.Range(4, 9), "cardboard_box_01", "circuit_board", "television_02", "plastic_container"));
                c.FloorAnywhere(RackO, Pose.Upright);
            }
            Decorate(c, 0.3f, "power_box_01", "security_camera_01", NoticeO);
        }

        static void Security(RoomCtx c)
        {
            var wall = Front(c);
            // monitor wall: shelves of CRTs behind a row of desks
            Line(c, wall, 0.7f, () => c.Pick("steel_frame_shelves_03", "worn_metal_rack"), s => FillShelves(c, s, c.Range(4, 8), "television_02", "television_02", "Television_01"));
            var fr = new Frame(c, wall);
            for (float su = 2f; su < Mathf.Min(fr.Width - 2f, 10f); su += 2.3f)
                DeskWithChair(c, "metal_office_desk", c.Pick(OfficeChairs), fr.At(su, 2.4f), -wall.n, c.Range(2, 4), "vintage_radio_transceiver", "clipboard", "vintage_flashlight", "alarm_clock_01", "binoculars");
            Line(c, Opposite(c, wall), 0.4f, () => "drawer_cabinet");
            c.AgainstWall(ProcKit.LockerBankV(5, 2), c.RandomWall(2.5f), c.Range(0.2f, 0.8f));
            Decorate(c, 0.5f, NoticeO, "wall_clock", MapO, "security_camera_01");
        }

        static void Infirmary(RoomCtx c)
        {
            var wall = Front(c);
            foreach (var bed in c.AlongWall(wall, () => c.Pick("vintage_day_bed", "old_bed_frame"), 0.9f, 1.4f, 0.9f, () => Pose.Upright))
            {
                if (bed.name.StartsWith("old_bed") && c.Chance(0.8f)) c.OnSurface(bed, () => MattressO, 1);
                var b = PropLibrary.RendererBounds(bed);
                // privacy screen beside each bed
                c.Floor(ProcKit.PartitionV(1.8f, 1.6f), new Vector2(b.center.x, b.center.z) + wall.Tangent * (b.extents.magnitude * 0.7f + 0.4f), c.Yaw(wall.Tangent) + Drift(c, 10f), Pose.Upright, true, 0f);
            }
            Line(c, Opposite(c, wall), 0.4f, () => c.Pick("painted_wooden_cabinet", "vintage_cabinet_01", "drawer_cabinet"), s => FillShelves(c, s, c.Range(2, 6), "medical_box", Bottles[0], Bottles[1], JarO));
            DeskWithChair(c, "metal_office_desk", "dining_chair_02", c.RandomPoint(2f), c.Pick(new Vector2(1, 0), new Vector2(0, -1)), c.Range(2, 4), "medical_box", "clipboard", "desk_lamp_arm_01", PaperO);
            c.FloorAnywhere("wheelchair_01", Left(c, 0.02f));
            c.FloorAnywhere("vintage_crutches_01", Left(c, 0.5f));
            Decorate(c, 0.5f, PosterO, NoticeO, "wall_clock");
        }

        static void Counselling(RoomCtx c)
        {
            var p = c.interior.center;
            c.Floor("ArmChair_01", p + new Vector2(1.1f, 0), c.Yaw(new Vector2(-1, 0)) + Drift(c, 15f), Pose.Upright);
            c.Floor("modern_arm_chair_01", p - new Vector2(1.1f, 0), c.Yaw(new Vector2(1, 0)) + Drift(c, 15f), Pose.Upright);
            var t = c.Floor("side_table_01", p + new Vector2(0, 0.8f), 0f, Pose.Upright);
            Fill(c, t, 1, "potted_plant_04", "alarm_clock_01");
            c.Floor(RugO, p, c.Range(0f, 360f), Pose.Upright, false);
            DeskWithChair(c, "wooden_table_02", "dining_chair_02", c.RandomPoint(1.6f), new Vector2(0, 1), c.Range(1, 3), "desk_lamp_arm_01", BookStackO, "standing_picture_frame_01");
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.2f, () => c.Pick("Shelf_01", "wooden_bookshelf_worn"), s => FillShelves(c, s, c.Range(3, 7), BookRowO, BookStackO, "potted_plant_04"));
            c.FloorAnywhere(c.Pick(PottedPlants[0], PottedPlants[1]), Pose.Upright);
            if (Area(c) > 120) Units(c, 6.5f, 0.3f, (p, f) => { if (c.Chance(0.5f)) LoungeGroup(c, p); else DeskWithChair(c, "wooden_table_02", "dining_chair_02", p, f, c.Range(1, 3), "desk_lamp_arm_01", BookStackO, PaperO); });
            Decorate(c, 0.5f, Frames[0], Frames[1], PosterO);
        }

        // ------------------------------------------------------------------ public spaces
        static void Lobby(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            var desk = c.Floor(ProcKit.CounterV(c.Range(3f, 4.4f)), fr.At(fr.Width * c.Range(0.3f, 0.7f), 2.4f), c.Yaw(front.n), Pose.Upright);
            Fill(c, desk, c.Range(2, 4), "television_02", "binder_notebook", "desk_lamp_arm_01", "office_notepads", PaperO);
            // short rows of waiting chairs, benches, plants; a trophy case and notices on the walls
            for (int i = 0; i < Mathf.Clamp(Mathf.RoundToInt(Area(c) / 70f), 2, 6); i++)
            {
                var p = c.RandomPoint(3f);
                var dir = c.Pick(new Vector2(1, 0), new Vector2(0, 1));
                for (int k = -2; k <= 2; k++)
                    if (c.Chance(0.85f)) c.Floor("dining_chair_02", p + Perp(dir) * k * 0.62f, c.Yaw(dir) + Drift(c, 6f), Left(c, 0.02f));
            }
            for (int i = 0; i < 3; i++) c.FloorAnywhere("painted_wooden_bench", Left(c, 0.01f));
            for (int i = 0; i < c.Range(2, 5); i++) c.FloorAnywhere(c.Pick(PottedPlants[0], PottedPlants[1], "pachira_aquatica_01_2", "pachira_aquatica_01_3"), Left(c, 0.04f));
            Units(c, 7f, 0.35f, (p, f) =>
            {
                switch (c.Range(0, 4))
                {
                    case 0: // benches back to back round a planter
                        c.Floor(PlanterO, p, c.Yaw(f), Pose.Upright);
                        c.Floor("painted_wooden_bench", p + Perp(f) * 1.3f, c.Yaw(Perp(f)), Pose.Upright);
                        c.Floor("painted_wooden_bench", p - Perp(f) * 1.3f, c.Yaw(-Perp(f)), Pose.Upright);
                        break;
                    case 1: LoungeGroup(c, p); break;
                    case 2: // freestanding notice boards
                        c.Floor("standing_chalkboard_01", p, c.Yaw(f) + Drift(c), Left(c, 0.02f));
                        c.Floor("standing_chalkboard_01", p + Perp(f) * 1.2f, c.Yaw(f) + Drift(c), Left(c, 0.02f));
                        break;
                    default: // potted trees
                        for (int k = 0; k < 3; k++) c.Floor(c.Pick("pachira_aquatica_01_2", "pachira_aquatica_01_4", "potted_plant_01"), p + Jit(c, 1.2f), c.Range(0f, 360f), Left(c, 0.02f));
                        break;
                }
            });
            c.AgainstWall(TrophyO, Opposite(c, front), c.Range(0.2f, 0.8f));
            Decorate(c, 0.6f, NoticeO, NoticeO, "korean_public_payphone_01", "wall_clock", BannerO, Frames[0], PosterO);
            Decorate(c, 0.2f, ExitO);
        }

        static void Trophy(RoomCtx c)
        {
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.6f, () => TrophyO);
            var fr = new Frame(c, c.LongestWall());
            for (float su = 3f; su < fr.Width - 2f; su += 3.5f)
            {
                var ped = c.Floor(PedestalO, fr.At(su, fr.Depth * 0.5f), c.Range(0f, 360f), Pose.Upright);
                Fill(c, ped, 1, "marble_bust_01", "horse_statue_01", "bronze_whale_statue", "mantel_clock_01");
            }
            for (int i = 0; i < 2; i++) c.FloorAnywhere("painted_wooden_bench", Pose.Upright);
            Units(c, 6f, 0.3f, (p, f) => { if (c.Chance(0.6f)) c.Floor(TrophyO, p, c.Yaw(f), Pose.Upright); else { var v = c.Floor(VitrineO, p, c.Yaw(f), Pose.Upright); Fill(c, v, c.Range(2, 4), "mantel_clock_01", "horse_statue_01", "american_football", "baseball_01", "magnifying_glass_01"); } });
            Decorate(c, 0.5f, BannerO, Frames[0], Frames[3]);
        }

        static void Museum(RoomCtx c)
        {
            var fr = new Frame(c, c.LongestWall());
            for (float sv = 2.8f; sv < fr.Depth - 2f; sv += 3.6f)
                for (float su = 2.8f; su < fr.Width - 2f; su += 3.8f)
                {
                    if (c.Chance(0.35f))
                    {
                        var ped = c.Floor(PedestalO, fr.At(su, sv), c.Range(0f, 360f), Pose.Upright);
                        Fill(c, ped, 1, "marble_bust_01", "bronze_whale_statue", "horse_statue_01", "planter_pot_clay");
                    }
                    else
                    {
                        var v = c.Floor(VitrineO, fr.At(su, sv), c.Yaw(fr.u) + Drift(c, 3f), Pose.Upright);
                        Fill(c, v, c.Range(2, 5), Curios[0], Curios[1], Curios[2], Curios[3], Curios[4], Curios[5], JarO, "dry_branches_medium_01_0");
                    }
                }
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.25f, () => c.Pick("vintage_cabinet_01", "GothicCabinet_01", TrophyO), s => FillShelves(c, s, c.Range(2, 5), "horse_statue_01", "mantel_clock_01", JarO, "binoculars"));
            for (int i = 0; i < 2; i++) c.FloorAnywhere("painted_wooden_bench", Pose.Upright);
            c.AgainstWall("vintage_grandfather_clock_01", c.RandomWall(1f), c.Range(0.1f, 0.9f));
            Decorate(c, 0.7f, Frames[0], Frames[1], Frames[2], Frames[3], MapO);
        }

        /// <summary>Planter beds in rows, overgrown or dried out, potting bench and shelves of pots.</summary>
        static void Botany(RoomCtx c, bool greenhouse)
        {
            var fr = new Frame(c, c.LongestWall());
            for (float sv = 2.6f; sv < fr.Depth - 2f; sv += c.Range(2.6f, 3.2f))
                for (float su = 2.6f; su < fr.Width - 2.4f; su += c.Range(3.8f, 4.6f))
                {
                    var bed = c.Floor(PlanterO, fr.At(su, sv), c.Yaw(fr.v) + Drift(c, 3f), Pose.Upright);
                    if (bed == null) continue;
                    bool dead = c.Chance(0.25f);
                    c.OnSurface(bed, () => dead ? c.Pick(DeadPlants) : c.Pick(BedPlants), c.Range(2, 6));
                    if (c.Chance(0.25f)) c.Floor(c.Pick("watering_can_metal_01", "trowel_01", "garden_gloves_01", "seeding_tray_01"), fr.At(su + c.Range(-1f, 1f), sv + 0.8f), c.Range(0f, 360f), Pose.Upright, false);
                }
            foreach (var w in c.walls)
                if (!w.outer) Line(c, w, greenhouse ? 0.5f : 0.3f, () => c.Pick("steel_frame_shelves_02", "steel_frame_shelves_03", "planter_box_03", "planter_box_02"),
                    s => FillShelves(c, s, c.Range(3, 8), "planter_pot_clay", "potted_plant_04", "seeding_tray_01", "watering_can_metal_01"));
            for (int i = 0; i < Mathf.RoundToInt(Area(c) / 40f); i++) c.FloorAnywhere(c.Pick(PottedPlants[0], PottedPlants[1], "pachira_aquatica_01_2", "pachira_aquatica_01_4", "pachira_aquatica_01_6"), Left(c, 0.04f));
            for (int i = 0; i < c.Range(1, 3); i++) c.FloorAnywhere(c.Pick("compost_bags_0", "compost_bags_1", "compost_bags_2"), Pose.Upright);
            for (int i = 0; i < 2; i++) c.FloorAnywhere("painted_wooden_bench", Pose.Upright);
            var potting = c.FloorAnywhere(WorkbenchO, Pose.Upright);
            Fill(c, potting, c.Range(2, 5), "planter_pot_clay", "seeding_tray_01", "trowel_01", "garden_gloves_01", "watering_can_metal_01");
            Decorate(c, 0.3f, PosterO, NoticeO);
        }

        static void Herbarium(RoomCtx c)
        {
            var fr = new Frame(c, c.LongestWall());
            for (float sv = 2.5f; sv < fr.Depth - 1.5f; sv += 2.6f)
                for (float su = 2.2f; su < fr.Width - 1.6f; su += 2.8f)
                {
                    var v = c.Floor(VitrineO, fr.At(su, sv), c.Yaw(fr.u) + Drift(c, 2f), Pose.Upright);
                    Fill(c, v, c.Range(2, 4), "fern_02_1", "fern_02_2", "dry_branches_medium_01_1", "nettle_plant_1", JarO, "magnifying_glass_01");
                }
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.6f, () => c.Pick("drawer_cabinet", "vintage_wooden_drawer_01", "steel_frame_shelves_02"), s => FillShelves(c, s, c.Range(2, 6), JarO, JarO, BookRowO, "potted_plant_04"));
            DeskWithChair(c, "wooden_table_02", "dining_chair_02", c.RandomPoint(1.5f), new Vector2(0, 1), c.Range(2, 4), "vintage_microscope", "magnifying_glass_01", "desk_lamp_arm_01", BookStackO, "fern_02_3");
            Decorate(c, 0.6f, PosterO, PosterO, Frames[2]);
        }

        static void Bookstore(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            var counter = c.AgainstWall(ProcKit.CounterV(c.Range(2.4f, 3.6f)), front, c.Range(0.2f, 0.8f));
            Fill(c, counter, c.Range(1, 3), "CashRegister_01", BookStackO, "alarm_clock_01");
            int row = 0;
            for (float sv = 3f; sv < fr.Depth - 1.5f; sv += 2.2f, row++)
                for (float su = 1.6f; su < fr.Width - 1.6f; su += 1.5f)
                {
                    var s = c.Floor(c.Pick("Shelf_01", "wooden_display_shelves_01", "wooden_bookshelf_worn"), fr.At(su, sv), c.Yaw(row % 2 == 0 ? fr.v : -fr.v) + Drift(c, 2f), Pose.Upright, true, 0.02f);
                    FillShelves(c, s, c.Range(3, 8), BookRowO, BookRowO, BookStackO, "standing_picture_frame_01", "mantel_clock_01");
                }
            for (int i = 0; i < c.Range(1, 4); i++) c.FloorAnywhere("cardboard_box_01", Pose.Upright);
            Decorate(c, 0.4f, PosterO, BannerO, "wall_clock");
        }

        static void Cafeteria(RoomCtx c)
        {
            var front = Front(c);
            var fr = new Frame(c, front);
            var counter = c.AgainstWall(ProcKit.CounterV(Mathf.Min(front.Length - 1f, c.Range(6f, 9f)), 0.8f, 0.95f), front, c.Range(0.35f, 0.65f));
            Fill(c, counter, c.Range(4, 8), "CashRegister_01", TrayO, TrayO, "plastic_container", "tea_set_01");
            for (float sv = 3.6f; sv < fr.Depth - 2f; sv += 3.2f)
                for (float su = 2.4f; su < fr.Width - 2.2f; su += 3.4f)
                {
                    var t = c.Floor("dining_table", fr.At(su, sv), c.Yaw(fr.u) + Drift(c, 4f), Left(c, 0.01f));
                    if (t == null) continue;
                    if (c.Chance(0.3f)) Fill(c, t, c.Range(1, 3), TrayO, "plastic_bottle_gallon");
                    ChairsAround(c, t, new[] { "plastic_monobloc_chair_01", "dining_chair_02" });
                }
            foreach (var w in c.walls) if (!w.outer && w != front) Line(c, w, 0.1f, () => c.Pick("metal_trash_can", VendingO));
            Decorate(c, 0.4f, NoticeO, PosterO, "wall_clock", BannerO);
        }

        static void Kitchen(RoomCtx c)
        {
            bool cold = c.room.role == "coldstore", scullery = c.room.role == "scullery";
            foreach (var w in c.walls)
            {
                if (w.outer || w.Length < 1.5f) continue;
                if (cold) Line(c, w, 0.85f, () => c.Pick(MetalShelves), s => FillShelves(c, s, c.Range(3, 8), "plastic_container", "plastic_crate_01", "cardboard_box_01", "plastic_bottle_gallon"));
                else if (scullery) c.AgainstWall(ProcKit.SinkCounterV(Mathf.Min(w.Length - 0.6f, 4f)), w, 0.5f);
                else Line(c, w, 0.7f, () => c.Chance(0.3f) ? (object)"electric_stove" : ProcKit.CounterV(c.Range(1.2f, 2.4f), 0.65f, 0.9f), s => Fill(c, s, c.Range(0, 3), "plastic_container", TrayO, "vintage_electric_kettle", "vintage_microwave"));
            }
            if (!cold)
                Units(c, 5.5f, 0.2f, (p, f) =>
                {
                    if (c.Chance(0.6f)) { var t = c.Floor(ProcKit.CounterV(2.4f, 0.9f, 0.92f), p, c.Yaw(f), Pose.Upright); Fill(c, t, c.Range(1, 4), "plastic_container", TrayO, Bottles[2], "vintage_electric_kettle"); }
                    else if (c.Chance(0.5f)) { var t = c.Floor(c.Pick("industrial_storage_cart", "tool_cart"), p, c.Yaw(f) + Drift(c, 15f), Left(c, 0.02f)); Fill(c, t, c.Range(1, 3), TrayO, "plastic_container"); }
                    else { var s2 = c.Floor(c.Pick(MetalShelves), p, c.Yaw(f), Pose.Upright); FillShelves(c, s2, c.Range(3, 7), "plastic_container", "plastic_crate_01", TrayO); }
                });
            else for (int i = 0; i < Mathf.RoundToInt(Area(c) / 40f); i++) c.FloorAnywhere(c.Pick(PalletO, "plastic_crate_01", "wooden_crate_02"), Pose.Upright);
        }

        static void Lockers(RoomCtx c)
        {
            int paint = c.Range(0, 3);
            foreach (var w in c.walls)
                if (!w.outer && w.Length > 1.6f)
                    c.AlongWall(w, () => ProcKit.LockerBankV(c.Range(4, 11), c.Chance(0.1f) ? 3 : paint), 0.4f, 2.2f, 0.85f);
            var fr = new Frame(c, c.LongestWall());
            for (float sv = 2.4f; sv < fr.Depth - 1.8f; sv += 2.6f)
                c.Floor(ProcKit.BenchV(c.Range(1.6f, 2.6f)), fr.At(fr.Width * c.Range(0.35f, 0.65f), sv), c.Yaw(fr.v) + Drift(c, 5f), Pose.Upright);
            for (int i = 0; i < c.Range(1, 4); i++) c.FloorAnywhere(c.Pick("american_football", "dirty_football", "baseball_bat", "rubber_boots", "baseball_01"), Pose.Upright, false);
            Decorate(c, 0.3f, NoticeO, "wall_clock", PosterO);
        }

        static void Washroom(RoomCtx c)
        {
            bool showers = c.room.role == "showers";
            bool men = c.room.label != null && c.room.label.StartsWith("Men");
            var stallWall = c.LongestWall();
            for (float t = 0.05f; t < 0.95f;)
            {
                int stalls = Mathf.Min(c.Range(3, 7), Mathf.FloorToInt((stallWall.Length * (0.96f - t)) / 0.95f));
                if (stalls < 1) break;
                float span = stalls * 0.95f / stallWall.Length;
                c.AgainstWall(ProcKit.StallRowV(stalls), stallWall, t + span * 0.5f, Pose.Upright, 0.01f);
                t += span + c.Range(0.06f, 0.15f);
            }
            var opposite = Opposite(c, stallWall) ?? c.RandomWall(2f);
            for (float t = 0.06f; t < 0.9f && opposite != null;)
            {
                float len = Mathf.Min(opposite.Length * (0.94f - t), c.Range(3.2f, 4.8f));
                if (len < 1.6f) break;
                c.AgainstWall(ProcKit.SinkCounterV(len), opposite, t + len * 0.5f / opposite.Length, Pose.Upright, 0.01f);
                t += len / opposite.Length + c.Range(0.08f, 0.2f);
            }
            if (showers)
            {
                var fr = new Frame(c, stallWall);
                for (int i = 0; i < Mathf.FloorToInt((fr.Width - 6f) / 1.4f); i++)
                    c.Floor(ProcKit.PartitionV(1.6f, 1.9f), fr.At(3f + i * 1.4f, fr.Depth * 0.5f), c.Yaw(fr.u), Pose.Upright, true, 0.01f);
            }
            if (men)
                foreach (var uw in Sides(c, stallWall))
                {
                    if (uw.outer || uw.Length < 2f) continue;
                    int n = Mathf.Min(c.Range(3, 7), Mathf.FloorToInt(uw.Length / 0.8f));
                    for (int i = 0; i < n; i++) c.OnWall(UrinalO, uw, 0.15f + i * 0.75f / uw.Length, 0f);
                }
            if (Area(c) > 200)
                Units(c, 6.5f, 0.3f, (p, f) =>
                {
                    int n = c.Range(3, 6);
                    if (c.Chance(0.6f))
                    {
                        // back-to-back block of stalls
                        c.Floor(ProcKit.StallRowV(n), p - f * 0.78f, c.Yaw(-f), Pose.Upright, true, 0.01f);
                        c.Floor(ProcKit.StallRowV(n), p + f * 0.78f, c.Yaw(f), Pose.Upright, true, 0.01f);
                    }
                    else
                    {
                        // sink island
                        c.Floor(ProcKit.SinkCounterV(3.2f), p - f * 0.3f, c.Yaw(-f), Pose.Upright, true, 0.01f);
                        c.Floor(ProcKit.SinkCounterV(3.2f), p + f * 0.3f, c.Yaw(f), Pose.Upright, true, 0.01f);
                    }
                });
            if (c.Chance(0.5f)) c.FloorAnywhere("WetFloorSign_01", Left(c, 0.1f));
            if (c.Chance(0.4f + 0.1f * c.mess)) c.FloorAnywhere(PuddleO, Pose.Upright, false);
            c.AgainstWall("metal_trash_can", c.RandomWall(1f), c.Range(0.1f, 0.9f));
        }

        // ------------------------------------------------------------------ residence
        static void Dorm(RoomCtx c)
        {
            int style = c.Style("dorm", 4);
            var walls = c.walls.FindAll(w => !w.outer && w.Length > 2.2f);
            walls.Sort((a, b) => b.Length.CompareTo(a.Length));
            int beds = Area(c) > 180 ? 2 : 1;
            if (Area(c) > 150)
            {
                // shared dormitory: a bed space per unit (bed, locker or wardrobe, a small desk)
                Units(c, 4.6f, 0.15f, (p, f) =>
                {
                    object bedKind = style == 2 ? V("BunkBed", 6, ProcKit.BunkBed) : c.Pick("old_bed_frame", "old_bed_frame", "vintage_day_bed");
                    var bed = c.Floor(bedKind, p, c.Yaw(f) + Drift(c, 4f), Pose.Upright);
                    if (bed == null) return;
                    if (bed.name.StartsWith("old_bed") && style != 3 && c.Chance(0.85f)) c.OnSurface(bed, () => MattressO, 1);
                    var ns = c.Floor(c.Pick("ClassicNightstand_01", "painted_wooden_nightstand"), p + Perp(f) * 0.9f - f * 0.6f, c.Yaw(f), Left(c, 0.01f));
                    Fill(c, ns, c.Range(0, 3), "alarm_clock_01", "desk_lamp_arm_01", BookO, "standing_picture_frame_02", "cassette_player");
                    if (c.Chance(0.6f)) c.Floor(c.Chance(0.5f) ? ProcKit.LockerBankV(1, 0) : (object)"painted_wooden_cabinet", p - Perp(f) * 1.2f - f * 0.6f, c.Yaw(f), Left(c, 0.01f));
                    if (c.Chance(0.35f)) c.Floor(c.Pick("vintage_suitcase", ClothesO, "rubber_boots", "Ukulele_01"), p + f * 1.3f + Jit(c, 0.4f), c.Range(0f, 360f), Pose.Upright, false);
                });
                beds = 0;
            }
            for (int i = 0; i < beds && i < walls.Count; i++)
            {
                var bed = c.AgainstWall(style == 2 ? V("BunkBed", 6, ProcKit.BunkBed) : c.Pick("old_bed_frame", "old_bed_frame", "vintage_day_bed"), walls[i], c.Range(0.15f, 0.4f), Pose.Upright, 0.04f);
                if (bed == null) continue;
                if (bed.name.StartsWith("old_bed") && style != 3) c.OnSurface(bed, () => MattressO, 1);
                var ns = c.AgainstWall(c.Pick("ClassicNightstand_01", "painted_wooden_nightstand"), walls[i], c.Range(0.45f, 0.6f));
                Fill(c, ns, c.Range(1, 3), "alarm_clock_01", "desk_lamp_arm_01", BookO, "standing_picture_frame_02");
            }
            var ward = c.AgainstWall(c.Pick("painted_wooden_cabinet", "vintage_cabinet_01", "steel_frame_shelves_02"), c.RandomWall(1.5f), c.Range(0.6f, 0.9f), Left(c, 0.01f));
            FillShelves(c, ward, c.Range(1, 4), ClothesO, BookRowO, "vintage_suitcase");
            var desk = c.AgainstWall(c.Pick("wooden_table_02", "small_wooden_table_01"), c.RandomWall(1.4f), c.Range(0.2f, 0.8f));
            if (desk != null)
            {
                Fill(c, desk, c.Range(2, 5), "classic_laptop", "desk_lamp_arm_01", BookStackO, PaperO, "cassette_player", "standing_picture_frame_01", "Camera_01");
                var bb = PropLibrary.RendererBounds(desk);
                var fwd = new Vector2(desk.transform.forward.x, desk.transform.forward.z);
                c.Floor(c.Pick("dining_chair_02", "SchoolChair_01"), new Vector2(bb.center.x, bb.center.z) + fwd * 0.75f, c.Yaw(-fwd) + Drift(c, 25f), Left(c, 0.02f));
            }
            if (c.Chance(0.6f)) c.FloorAnywhere(RugO, Pose.Upright, false);
            if (c.Chance(0.4f)) c.FloorAnywhere(c.Pick("potted_plant_04", "potted_plant_02", "boombox", "Ukulele_01"), Left(c, 0.1f));
            switch (style)
            {
                case 1: // left in a hurry: suitcase out, clothes on the floor
                    c.FloorAnywhere("vintage_suitcase", Pose.Upright);
                    for (int i = 0; i < c.Range(1, 3); i++) c.FloorAnywhere(ClothesO, Pose.Upright, false);
                    break;
                case 3: // moved out: stripped bed, boxes
                    for (int i = 0; i < c.Range(2, 5); i++) c.FloorAnywhere(c.Pick("cardboard_box_01", "plastic_crate_02"), Pose.Upright);
                    break;
            }
            Decorate(c, 0.8f, PosterO, PosterO, Frames[1], Frames[3], "dartboard", WallShelfO, "wall_clock");
        }

        static void Lounge(RoomCtx c)
        {
            Units(c, 5.5f, 0.15f, (p, f) =>
            {
                switch (c.Range(0, 5))
                {
                    case 0: { var t = c.Floor(c.Pick("small_wooden_table_01", "side_table_01"), p, c.Range(0f, 360f), Left(c, 0.01f)); Fill(c, t, 1, "chess_set", "tea_set_01", BookStackO); ChairsAround(c, t, new[] { "dining_chair_02" }); break; }
                    case 1: Divider(c, p, Perp(f), 2); break;
                    default: LoungeGroup(c, p); break;
                }
            });
            c.AgainstWall("Television_01", c.RandomWall(1.5f), c.Range(0.2f, 0.8f));
            for (int i = 0; i < 2; i++)
            {
                var t = c.FloorAnywhere(c.Pick("small_wooden_table_01", "side_table_01"), Pose.Upright);
                Fill(c, t, 1, "chess_set", "tea_set_01", BookStackO);
                ChairsAround(c, t, new[] { "dining_chair_02" });
            }
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.2f, () => c.Pick("wooden_bookshelf_worn", "Shelf_01", VendingO), s => FillShelves(c, s, c.Range(3, 7), BookRowO, BookStackO, "boombox", "chess_set"));
            Decorate(c, 0.6f, "dartboard", PosterO, NoticeO, Frames[0], "wall_clock", BannerO);
        }

        static void Laundry(RoomCtx c)
        {
            var w = Front(c);
            c.AlongWall(w, () => WasherO, 0.02f, 0.1f, 0.95f);
            var opp = Opposite(c, w);
            if (opp != null && !opp.outer) c.AlongWall(opp, () => WasherO, 0.02f, 0.2f, 0.8f);
            var t = c.FloorAnywhere("wooden_table_02", Pose.Upright);
            Fill(c, t, c.Range(1, 4), ClothesO, "wicker_basket_01", "bleach_bottle");
            for (int i = 0; i < c.Range(1, 4); i++) c.FloorAnywhere(c.Pick("wicker_basket_01", ClothesO), Pose.Upright, false);
            Decorate(c, 0.3f, NoticeO, "wall_clock");
        }

        static void Vending(RoomCtx c)
        {
            c.AlongWall(Front(c), () => VendingO, 0.1f, 0.5f, 0.9f);
            c.AgainstWall(FridgeO, c.RandomWall(1.5f), c.Range(0.2f, 0.8f));
            c.AgainstWall("metal_trash_can", c.RandomWall(1f), c.Range(0.1f, 0.9f));
            for (int i = 0; i < 2; i++) c.FloorAnywhere("bar_chair_round_01", Left(c, 0.05f));
            Decorate(c, 0.3f, PosterO, NoticeO);
        }

        // ------------------------------------------------------------------ service rooms
        static void Storage(RoomCtx c)
        {
            string role = c.room.role;
            foreach (var w in c.walls)
                if (!w.outer) Line(c, w, 0.7f, () => c.Pick(MetalShelves), s =>
                {
                    if (role == "linen" || role == "housekeeping") FillShelves(c, s, c.Range(3, 8), ClothesO, "wicker_basket_01", "throw_pillows_01", "cardboard_box_01");
                    else if (role == "janitor") FillShelves(c, s, c.Range(3, 8), Bottles[0], Bottles[1], Bottles[2], "spray_paint_bottles");
                    else FillShelves(c, s, c.Range(3, 9), "cardboard_box_01", "plastic_crate_01", "plastic_container", "binder_notebook", BookStackO);
                });
            var fr = new Frame(c, c.LongestWall());
            for (float sv = 3f; sv < fr.Depth - 2.6f; sv += 3f)
                for (float su = 2f; su < fr.Width - 2f; su += 1.4f)
                {
                    var s = c.Floor(c.Pick(MetalShelves), fr.At(su, sv), c.Yaw(fr.v) + Drift(c, 2f), Pose.Upright, true, 0.02f);
                    FillShelves(c, s, c.Range(3, 8), "cardboard_box_01", "plastic_crate_02", "plastic_container", "binder_notebook");
                }
            if (role == "janitor" || role == "housekeeping")
            {
                c.FloorAnywhere(BucketO, Pose.Upright);
                c.FloorAnywhere(c.Pick("plastic_broom", "wooden_broom", "WetFloorSign_01"), Left(c, 0.3f));
                c.FloorAnywhere(c.Pick("tool_cart", "industrial_storage_cart"), Pose.Upright);
            }
            if (c.Chance(0.4f)) c.FloorAnywhere(c.Pick("wooden_ladder", "ladder_sectioned_01"), Pose.Upright);
        }

        static void Plant(RoomCtx c)
        {
            foreach (var w in c.walls)
            {
                if (w.outer || w.Length < 1.5f) continue;
                if (c.Chance(0.6f)) c.OnWall("power_box_01", w, c.Range(0.1f, 0.9f), c.Range(1.4f, 1.8f));
                Line(c, w, 0.4f, () => c.Pick("utility_box_01", "utility_box_02", "steel_frame_shelves_01", "Barrel_01", "Barrel_02", "metal_tool_chest"));
            }
            var fr = new Frame(c, c.LongestWall());
            for (float su = 3f; su < fr.Width - 2f; su += c.Range(3f, 5f))
            {
                var p = fr.At(su, fr.Depth * c.Range(0.35f, 0.65f));
                c.Floor(c.Pick("portable_generator", "Barrel_01", "Barrel_02", "propane_tank"), p, c.Range(0f, 360f), Pose.Upright);
                if (c.Chance(0.5f)) c.Floor(c.Pick("Barrel_01", "Barrel_02"), p + Jit(c, 0.8f), c.Range(0f, 360f), Left(c, 0.05f));
            }
            var cart = c.FloorAnywhere("tool_cart", Pose.Upright);
            Fill(c, cart, c.Range(1, 3), "metal_toolbox", "vintage_flashlight", "spray_paint_bottles");
            if (c.Chance(0.5f)) c.FloorAnywhere(PuddleO, Pose.Upright, false);
            Decorate(c, 0.3f, "industrial_wall_lamp", "industrial_caged_sconce", NoticeO);
        }

        static void Loading(RoomCtx c)
        {
            for (int i = 0; i < Mathf.RoundToInt(Area(c) / 45f); i++)
            {
                var pal = c.FloorAnywhere(PalletO, Pose.Upright);
                if (pal != null && c.Chance(0.75f))
                    foreach (var l in c.OnSurface(pal, () => c.Pick("cardboard_box_01", "wooden_crate_01", "plastic_crate_01"), c.Range(1, 4)))
                        if (c.Chance(0.35f)) c.OnSurface(l, () => "cardboard_box_01", 1);
            }
            for (int i = 0; i < c.Range(1, 4); i++) c.FloorAnywhere(c.Pick("hand_truck", "industrial_storage_cart", "Barrel_01", "wooden_crate_02"), Pose.Upright);
            Decorate(c, 0.3f, "security_light", "industrial_wall_lamp", NoticeO);
        }

        static void Mail(RoomCtx c)
        {
            foreach (var w in c.walls) if (!w.outer) Line(c, w, 0.7f, () => c.Pick("Shelf_01", "wooden_display_shelves_01"), s => FillShelves(c, s, c.Range(4, 9), PaperO, "cardboard_box_01", "binder_notebook", BookStackO));
            var t = c.FloorAnywhere("wooden_table_02", Pose.Upright);
            Fill(c, t, c.Range(2, 5), "cardboard_box_01", PaperO, "vintage_stapler", "clipboard");
            for (int i = 0; i < c.Range(1, 4); i++) c.FloorAnywhere(c.Pick("cardboard_box_01", "plastic_crate_02"), Pose.Upright);
            Decorate(c, 0.4f, NoticeO, "wall_clock");
        }

        static void Passage(RoomCtx c)
        {
            if (c.Chance(0.5f)) c.FloorAnywhere("painted_wooden_bench", Pose.Upright);
            if (c.Chance(0.5f)) c.FloorAnywhere(c.Pick(PottedPlants[0], PottedPlants[1]), Pose.Upright);
            Decorate(c, 0.4f, NoticeO, PosterO, "fire_alarm", ExitO);
        }

        // ------------------------------------------------------------------ decay
        /// <summary>
        /// Signs of time passing, kept light: a little paper, and only in the messier rooms a fallen tile,
        /// a gap in the ceiling, a puddle under a leak. Most rooms get almost nothing.
        /// </summary>
        public static void Decay(RoomCtx c)
        {
            float a = Mathf.Clamp(Area(c) / 100f, 0.3f, 5f);
            Papers(c, Mathf.RoundToInt(c.mess * a * c.Range(0.5f, 1.5f)));
            if (c.mess >= 2)
            {
                int holes = Mathf.RoundToInt((c.mess - 1) * c.Range(0.5f, 1.5f));
                for (int i = 0; i < holes; i++)
                {
                    var p = c.RandomPoint(1f);
                    if (c.OnCeiling(V("CeilingHole", 12, ProcKit.CeilingHole), p, c.Pick(0f, 90f, 180f, 270f)) == null) continue;
                    for (int k = 0; k < c.Range(1, 3); k++) c.Floor(TileO, p + Jit(c, 0.7f), c.Range(0f, 360f), Pose.Upright, false, 0f);
                }
                if (c.Chance(0.4f)) c.FloorAnywhere(PuddleO, Pose.Upright, false);
            }
            if (c.mess >= 3 && c.Chance(0.6f))
            {
                var p = c.RandomPoint(1.2f);
                c.OnCeiling("mounted_fluorescent_lights", p, c.Range(0f, 360f), c.Range(20f, 45f));   // a fitting hanging off its mounts
                c.Floor(RubbleO, p + Jit(c, 0.5f), c.Range(0f, 360f), Pose.Upright, false, 0f);
            }
            if (c.Chance(0.03f * (1 + c.mess))) c.FloorAnywhere("street_rat", Pose.Upright, false);
        }

        // ------------------------------------------------------------------ corridors
        public static void Corridor(RoomCtx c)
        {
            object[] floorSide;
            switch (c.floor)
            {
                case 0: floorSide = new object[] { "painted_wooden_bench", TrophyO, "potted_plant_01", "potted_plant_02", FountainO, "korean_fire_extinguisher_01" }; break;
                case 1: floorSide = new object[] { "painted_wooden_bench", "korean_fire_extinguisher_01", FountainO, "potted_plant_02", "standing_chalkboard_01" }; break;
                case 2: floorSide = new object[] { "painted_wooden_bench", "potted_plant_01", "korean_fire_extinguisher_01", "ArmChair_01", FountainO }; break;
                default: floorSide = new object[] { "painted_wooden_bench", "potted_plant_02", "korean_fire_extinguisher_01", "drawer_cabinet", "vintage_grandfather_clock_01" }; break;
            }
            foreach (var w in c.walls)
            {
                if (w.outer)
                {
                    int n = Mathf.FloorToInt(w.Length / 4f);
                    for (int i = 0; i < n; i++) c.OnWall(WindowO, w, (i + 0.5f) / n, 1.75f);
                    continue;
                }
                float pos = c.Range(1f, 5f);
                while (pos < w.Length - 1f)
                {
                    float t = pos / w.Length;
                    if (c.floor == 1 && w.Length > 6f && c.Chance(0.3f))
                    {
                        int n = c.Range(4, 9);
                        var bank = c.AgainstWall(ProcKit.LockerBankV(n, c.Chance(0.1f) ? 3 : 0), w, Mathf.Clamp01((pos + n * 0.19f) / w.Length), Pose.Upright, 0.01f);
                        pos += bank != null ? n * 0.38f + c.Range(3f, 7f) : 2f;
                        continue;
                    }
                    if (c.Chance(0.35f))
                    {
                        var go = c.AgainstWall(c.Pick(floorSide), w, t, Pose.Upright, 0.03f, Drift(c, 4f));
                        if (go != null)
                        {
                            var b = PropLibrary.RendererBounds(go);
                            float depth = Mathf.Abs(w.n.x) * b.size.x + Mathf.Abs(w.n.y) * b.size.z;
                            if (depth > 0.75f) UnityEngine.Object.DestroyImmediate(go);   // keep the walkway clear
                        }
                    }
                    if (c.Chance(0.45f)) Hang(c, c.Pick(NoticeO, PosterO, PosterO, "wall_clock", Frames[1], BannerO, "fire_alarm", ExitO), w, Mathf.Clamp01(t + c.Range(-0.04f, 0.04f)));
                    pos += c.Range(5f, 11f);
                }
            }
            // a little paper here and there, and the odd gap in the ceiling
            float total = 0;
            foreach (var w in c.walls) total += w.Length;
            for (int i = 0; i < Mathf.RoundToInt(total / 60f); i++)
            {
                var w = c.RandomWall(2f);
                if (w == null) break;
                var p = w.Point(c.R01()) + w.n * c.Range(0.5f, 1.4f);
                if (c.Chance(0.75f)) { for (int k = 0; k < c.Range(1, 4); k++) c.Floor(PaperO, p + Jit(c, 0.8f), c.Range(0f, 360f), Pose.Upright, false, 0f); }
                else { c.OnCeiling(V("CeilingHole", 12, ProcKit.CeilingHole), p, c.Pick(0f, 90f)); c.Floor(TileO, p, c.Range(0f, 360f), Pose.Upright, false, 0f); }
            }
        }
    }
}
