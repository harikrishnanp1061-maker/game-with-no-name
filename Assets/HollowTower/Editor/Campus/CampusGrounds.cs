using System.Collections.Generic;
using HollowTower.Campus;
using UnityEngine;
using M = HollowTower.EditorTools.CampusMats;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The campus at night, around the administration building (x -36..36, z -24..24; +z is north). North of it: the
    /// entrance plaza, then the main quad (fountain, lawns, Victorian lamps) between the library (west) and the lecture
    /// halls (east), closed off by the clock tower building, whose arch leads onto the avenue, the ring road, the car
    /// park and the sports field, and at the far north the main gate. West: the gardens (hedges, pond, gazebo) and the
    /// maintenance yard with the generator. East: the outdoor cafeteria and the student centre. A brick wall runs round
    /// it all. Every area can be reached two or three ways; the objective sends the party west for the power, then to the
    /// security booth by the gate.
    /// </summary>
    public static class CampusGrounds
    {
        public static CampusSwitch Generator, GateControl;
        public static CampusGate Gate;
        public static ServiceGate Service;
        public static Puzzles.ClockPuzzle Clock;
        public static Puzzles.StatuePuzzle Statues;

        /// <summary>A small item on the ground or in a niche (a crank handle, a key), lit by P4's torch like the others.</summary>
        static PartyItem Item(Transform parent, string name, Vector3 pos, string id, string display, Color col, System.Action<Transform, List<Renderer>> shape)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            var rs = new List<Renderer>();
            shape(go.transform, rs);
            var c = go.AddComponent<BoxCollider>(); c.center = new Vector3(0, 0.08f, 0); c.size = new Vector3(0.45f, 0.2f, 0.35f);
            var it = go.AddComponent<PartyItem>();
            it.itemId = id; it.displayName = display; it.color = col; it.needsTorch = true; it.glowRenderers = rs.ToArray(); it.glowIntensity = 1.5f;
            var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.transform.localPosition = new Vector3(0, 0.3f, 0);
            l.type = LightType.Point; l.range = 2f; l.intensity = 1.5f; l.color = col; it.glowLight = l;
            return it;
        }
        /// <summary>The service gate's x along the south wall (the far south-east corner).</summary>
        public const float ServiceX = 152f;

        const float WallW = -175f, WallE = 170f, WallS = -52f, WallN = 240f;

        static Transform root, ground, build, nature, props, lights, vehicles;
        static System.Random rnd;
        static readonly List<Rect> blocked = new List<Rect>();
        static readonly List<CampusLamp> generatorLamps = new List<CampusLamp>();
        static Material genGreen, fenceMesh, umbrella, stripe, WindowWarm;

        public static Transform Build(Transform parent)
        {
            rnd = new System.Random(4242);
            blocked.Clear(); generatorLamps.Clear(); Alarms.Clear(); carCount = 0;
            root = new GameObject("Campus_Grounds").transform; root.SetParent(parent, false);
            ground = Group("Ground"); build = Group("Buildings"); nature = Group("Nature"); props = Group("Props");
            lights = Group("Lights"); vehicles = Group("Vehicles");
            genGreen = CampusKit.Flat("CM_GeneratorGreen", new Color(0.16f, 0.22f, 0.15f), 0.35f, 0.4f);
            fenceMesh = CampusKit.Glass("CM_ChainLink", new Color(0.25f, 0.26f, 0.26f, 0.35f), 0.3f);
            umbrella = CampusKit.Flat("CM_Umbrella", new Color(0.34f, 0.12f, 0.1f), 0.1f);
            stripe = CampusKit.Flat("CM_BarrierStripe", new Color(0.6f, 0.12f, 0.08f), 0.3f);
            WindowWarm = CampusKit.Glow("CM_WindowWarm", new Color(1f, 0.7f, 0.4f), 0.9f);

            Block(-40, 40, -28, 28);   // the admin building itself

            Ground();
            Roads();
            Plaza();
            Quad();
            Library();
            LectureHalls();
            ClockTower();
            Avenue();
            CarPark();
            SportsField();
            Gardens();
            MaintenanceYard();
            Cafeteria();
            StudentCentre();
            GateAndWall();
            Trees();
            return root;
        }

        static Transform Group(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        static float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        static bool Chance(float p) { return rnd.NextDouble() < p; }
        static void Block(float x0, float x1, float z0, float z1) { blocked.Add(Rect.MinMaxRect(x0, z0, x1, z1)); }
        static bool Free(float x, float z, float r)
        {
            foreach (var b in blocked) if (x > b.xMin - r && x < b.xMax + r && z > b.yMin - r && z < b.yMax + r) return false;
            return true;
        }

        // ------------------------------------------------------------------ surfaces
        static GameObject Pave(string n, float x0, float x1, float z0, float z1, Material m, float top, bool block = true)
        {
            if (block) Block(x0, x1, z0, z1);
            return CampusKit.Span(n, ground, new Vector3(x0, top - 0.12f, z0), new Vector3(x1, top, z1), m);
        }

        static void Ground()
        {
            // the lawn round the admin building, not under it (its floors sit at the same height and would flicker)
            const float hx = 36.1f, hz = 24.1f;
            CampusKit.Span("Ground_Grass", ground, new Vector3(-220, -0.6f, -100), new Vector3(220, 0, -hz), M.Grass);
            CampusKit.Span("Ground_Grass", ground, new Vector3(-220, -0.6f, hz), new Vector3(220, 0, 340), M.Grass);
            CampusKit.Span("Ground_Grass", ground, new Vector3(-220, -0.6f, -hz), new Vector3(-hx, 0, hz), M.Grass);
            CampusKit.Span("Ground_Grass", ground, new Vector3(hx, -0.6f, -hz), new Vector3(220, 0, hz), M.Grass);
            // a few muddy, worn patches in the lawns
            for (int i = 0; i < 26; i++)
            {
                float x = R(-160, 160), z = R(-40, 230), w = R(3, 9), d = R(3, 9);
                if (!Free(x, z, 4)) continue;
                CampusKit.Span("Mud", ground, new Vector3(x, -0.1f, z), new Vector3(x + w, 0.006f + i * 0.0004f, z + d), M.Dirt, false);
            }
        }

        static void Dashes(bool alongX, float at, float a0, float a1, float top)
        {
            for (float a = a0 + 2; a < a1 - 3; a += 7)
            {
                Vector3 min = alongX ? new Vector3(a, top - 0.01f, at - 0.08f) : new Vector3(at - 0.08f, top - 0.01f, a);
                Vector3 max = alongX ? new Vector3(a + 3, top + 0.004f, at + 0.08f) : new Vector3(at + 0.08f, top + 0.004f, a + 3);
                CampusKit.Span("Road_Dash", ground, min, max, M.Paint, false);
            }
        }

        static void Roads()
        {
            const float top = 0.02f;
            Pave("Road_Ring", -112, 118, 144, 152, M.Asphalt, top);
            Pave("Road_West", -112, -104, -40, 144, M.Asphalt, top);
            Pave("Road_East", 110, 118, -40, 144, M.Asphalt, top);
            Pave("Road_South", -146, 118, -48, -40, M.Asphalt, top);
            Dashes(true, 148, -104, 110, top); Dashes(false, -108, -40, 144, top);
            Dashes(false, 114, -40, 144, top); Dashes(true, -44, -140, 110, top);
            // kerbs along the ring road
            CampusKit.Span("Kerb", ground, new Vector3(-104, 0, 143.8f), new Vector3(-8, 0.14f, 144.1f), M.Kerb);
            CampusKit.Span("Kerb", ground, new Vector3(8, 0, 143.8f), new Vector3(110, 0.14f, 144.1f), M.Kerb);
            // a bus stop on the ring road
            var stop = new GameObject("BusStop").transform; stop.SetParent(build, false);
            CampusKit.Span("Shelter_Roof", stop, new Vector3(-40, 2.5f, 152.4f), new Vector3(-34, 2.62f, 154.4f), M.MetalLight);
            CampusKit.Span("Shelter_Back", stop, new Vector3(-40, 0.1f, 154.3f), new Vector3(-34, 2.5f, 154.4f), M.Glass);
            foreach (float x in new[] { -39.9f, -34.1f }) CampusKit.Span("Shelter_Post", stop, new Vector3(x - 0.05f, 0, 152.5f), new Vector3(x + 0.05f, 2.5f, 152.6f), M.Metal);
            CampusKit.Prop("Bench_2.4_0", stop, new Vector3(-37, 0.02f, 153.8f), 180);
            Pave("BusStop_Pad", -41, -33, 152, 155, M.Paving, 0.035f);
            // dead traffic on the roads: a bus left across the ring road, cars abandoned on the verges
            CampusKit.Cull(CampusKit.Prop("bus_rusty", vehicles, new Vector3(-62, 0, 148), 71), 0.01f);
            Car(new Vector3(-108.5f, 0, 20), 8, M.CarBody2, false);
            Car(new Vector3(113, 0, 96), 175, M.CarBody, false);
            Car(new Vector3(40, 0, -44.6f), 96, M.CarBody3, true);
            Car(new Vector3(-90, 0, -43), -80, M.CarBody, false);
            // street furniture: hydrants along the verges, and the police roadblock from the night the college was sealed
            foreach (var h in new[] { new Vector2(-80, 142.9f), new Vector2(-30, 142.9f), new Vector2(40, 142.9f), new Vector2(92, 142.9f),
                                      new Vector2(-102.9f, 100), new Vector2(-102.9f, 30), new Vector2(108.9f, 60), new Vector2(108.9f, -10), new Vector2(-60, -38.9f), new Vector2(60, -38.9f) })
                CampusKit.Cull(CampusKit.Prop("fire_hydrant", props, new Vector3(h.x, 0.02f, h.y), R(0, 360)), 0.006f);
            var block = new GameObject("Roadblock").transform; block.SetParent(vehicles, false);
            for (int i = 0; i < 4; i++)
                CampusKit.Cull(CampusKit.Prop(i % 2 == 0 ? "concrete_road_barrier" : "concrete_road_barrier_02", block, new Vector3(-96f + i * 1.7f, 0.02f, 152.6f + (i == 3 ? 0.6f : 0)), i == 3 ? 25 : R(-6, 6)), 0.008f);
            CampusKit.Cull(CampusKit.Prop("car_Cop", block, new Vector3(-90.5f, 0.02f, 155.5f), 110), 0.012f);
            CampusKit.Cull(CampusKit.Prop("barrel_03", block, new Vector3(-98.2f, 0.02f, 153.4f), 0), 0.008f);
        }

        static void Plaza()
        {
            Pave("Plaza", -20, 20, 24, 40, M.Paving, 0.04f);
            Pave("Path_PlazaWest", -104, -20, 28, 32, M.Paving, 0.035f);
            Pave("Path_PlazaEast", 20, 110, 28, 32, M.Paving, 0.035f);
            Pave("Path_AdminWest", -40, -36, -24, 28, M.Paving, 0.035f);
            Pave("Path_AdminEast", 36, 40, -24, 28, M.Paving, 0.035f);
            Pave("Path_AdminSouth", -40, 40, -28, -24, M.Paving, 0.035f);
            Pave("Path_ToSouthRoad", -2, 2, -40, -28, M.Paving, 0.035f);
            // flagpoles, one torn flag left
            for (int i = -1; i <= 1; i++)
            {
                CampusKit.Cyl("Flagpole", props, new Vector3(i * 6, 0, 38), 0.07f, 11f, M.MetalLight);
                CampusKit.Cyl("Flagpole_Base", props, new Vector3(i * 6, 0, 38), 0.4f, 0.4f, M.Concrete);
            }
            CampusKit.Box("Flag_Torn", props, new Vector3(0.75f, 10.3f, 38), new Vector3(1.4f, 0.8f, 0.02f), Quaternion.Euler(0, 0, -8), M.CarBody, false);
            // a bicycle shelter by the plaza, three bikes' worth of racks left
            CampusKit.Span("BikeShelter_Roof", build, new Vector3(-19, 2.3f, 34), new Vector3(-12, 2.4f, 38), M.MetalLight);
            for (float x = -18.5f; x <= -12.4f; x += 6) CampusKit.Span("BikeShelter_Post", build, new Vector3(x, 0, 37.8f), new Vector3(x + 0.1f, 2.3f, 37.9f), M.Metal);
            for (float x = -18; x < -12.5f; x += 1.1f) CampusKit.Box("BikeRack", props, new Vector3(x, 0.4f, 36), new Vector3(0.05f, 0.8f, 1.0f), M.MetalLight, false);
            Bench(-10, 34, 90); Bench(10, 34, -90); Bin(-14, 26); Bin(14, 26);
            Victorian(-18, 39); Victorian(18, 39); Victorian(-18, 25.5f); Victorian(18, 25.5f);
            Victorian(-60, 33); Victorian(-86, 27); Victorian(56, 33); Victorian(84, 27);
            Victorian(-38.6f, 0); Victorian(38.6f, 0); Victorian(0, -26.4f);
        }

        // ------------------------------------------------------------------ the quad
        static void Quad()
        {
            const float top = 0.035f;
            Pave("Quad_PathSouth", -52, 52, 40, 44, M.Paving, top);
            Pave("Quad_PathNorth", -52, 52, 118, 122, M.Paving, top);
            Pave("Quad_PathWest", -52, -48, 44, 118, M.Paving, top);
            Pave("Quad_PathEast", 48, 52, 44, 118, M.Paving, top);
            Pave("Quad_PathCross_NS", -3, 3, 44, 118, M.Paving, top);
            Pave("Quad_PathCross_EW", -48, 48, 79, 85, M.Paving, top);
            Pave("Quad_FountainSquare", -10, 10, 72, 92, M.Paving, 0.04f);
            Pave("Path_ToLecture", 52, 58, 77, 87, M.Paving, top);
            Pave("Path_ToArch", -4, 4, 122, 124, M.Paving, top);

            // the fountain, dry but for a black pool of rainwater
            var f = new GameObject("Fountain").transform; f.SetParent(build, false); f.localPosition = new Vector3(0, 0.04f, 82);
            CampusKit.Cyl("Basin", f, Vector3.zero, 6f, 0.7f, M.Concrete);
            CampusKit.Cyl("Basin_Water", f, new Vector3(0, 0.62f, 0), 5.7f, 0.04f, M.Water, false);
            CampusKit.Cyl("Column", f, Vector3.zero, 0.55f, 2.6f, M.Concrete);
            CampusKit.Cyl("Bowl", f, new Vector3(0, 2.4f, 0), 1.9f, 0.35f, M.Concrete);
            CampusKit.Cyl("Finial", f, new Vector3(0, 2.75f, 0), 0.25f, 0.9f, M.Concrete);
            CampusKit.Cull(CampusKit.Prop("statue_gargoyle", f, new Vector3(0, 3.65f, 0), 180, 0.9f, false), 0.01f);
            Block(-6.5f, 6.5f, 75.5f, 88.5f);
            WatchersBuild(f);

            // benches round the fountain, lamps along every path
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f, r = 8.6f;
                Bench(Mathf.Sin(a * Mathf.Deg2Rad) * r, 82 + Mathf.Cos(a * Mathf.Deg2Rad) * r, a + 180);
            }
            for (float x = -44; x <= 44; x += 14)
            {
                if (Mathf.Abs(x) < 4) continue;
                Victorian(x, 45); Victorian(x, 117);
                if (Mathf.Abs(x) > 12) Victorian(x, 86);
            }
            for (float z = 52; z <= 112; z += 15) { Victorian(-47, z); Victorian(47, z); if (Mathf.Abs(z - 82) > 10) Victorian(4, z); }
            for (float z = 50; z < 116; z += 9) { if (Mathf.Abs(z - 82) < 6) continue; if (Chance(0.6f)) Bench(-4.3f, z, 90); if (Chance(0.4f)) Bench(4.3f, z + 3, -90); }
            for (int i = 0; i < 10; i++) Bin(R(-45, 45), Chance(0.5f) ? 45.5f : 116.5f);

            // the memorial on the west lawn and the landmark tree on the east one
            var mem = new GameObject("Memorial").transform; mem.SetParent(build, false); mem.localPosition = new Vector3(-26, 0, 64);
            CampusKit.Box("Plinth", mem, new Vector3(0, 0.3f, 0), new Vector3(3.2f, 0.6f, 3.2f), M.Kerb);
            CampusKit.Box("Obelisk", mem, new Vector3(0, 3.4f, 0), new Vector3(1.1f, 5.6f, 1.1f), M.Marble);
            CampusKit.Box("Obelisk_Tip", mem, new Vector3(0, 6.45f, 0), new Vector3(0.7f, 0.5f, 0.7f), Quaternion.Euler(0, 45, 0), M.Marble);
            CampusKit.Text(mem, "FOUNDED 1931", new Vector3(0, 1.6f, -0.57f), Vector3.back, 0.18f, new Color(0.18f, 0.17f, 0.15f));
            Block(-28, -24, 62, 66);
            Tree("tree_landmark", 28, 100, 1f, 1.0f);
            Block(24, 32, 96, 104);
            // low hedges framing the four lawns
            foreach (var sx in new[] { -1, 1 })
                foreach (var z in new[] { 46f, 115f })
                    CampusKit.Span("Hedge", nature, new Vector3(sx > 0 ? 8 : -46, 0, z), new Vector3(sx > 0 ? 46 : -8, 0.9f, z + 1.1f), M.Hedge);
            for (int i = 0; i < 14; i++) Tree(Broad(), R(-44, 44), R(48, 114), R(0.8f, 1.15f), 0.4f, 6f);
        }

        // ------------------------------------------------------------------ buildings
        /// <summary>A closed campus building: a brick block with rows of dark windows (a few broken, one or two with a
        /// cold light left on), cornice, parapet and roof; entrance on one face.</summary>
        static Transform Building(string name, float x0, float x1, float z0, float z1, float h, int storeys, Material wall,
                                  char doorFace, float doorAt, string sign)
        {
            var b = new GameObject(name).transform; b.SetParent(build, false);
            Block(x0 - 1, x1 + 1, z0 - 1, z1 + 1);
            CampusKit.Span("Shell", b, new Vector3(x0, 0, z0), new Vector3(x1, h, z1), wall);
            CampusKit.Span("Roof", b, new Vector3(x0 + 0.3f, h, z0 + 0.3f), new Vector3(x1 - 0.3f, h + 0.1f, z1 - 0.3f), M.Roof, false);
            CampusKit.Span("Parapet", b, new Vector3(x0 - 0.15f, h, z0 - 0.15f), new Vector3(x1 + 0.15f, h + 0.9f, z0 + 0.25f), M.Concrete, false);
            CampusKit.Span("Parapet", b, new Vector3(x0 - 0.15f, h, z1 - 0.25f), new Vector3(x1 + 0.15f, h + 0.9f, z1 + 0.15f), M.Concrete, false);
            CampusKit.Span("Parapet", b, new Vector3(x0 - 0.15f, h, z0 + 0.25f), new Vector3(x0 + 0.25f, h + 0.9f, z1 - 0.25f), M.Concrete, false);
            CampusKit.Span("Parapet", b, new Vector3(x1 - 0.25f, h, z0 + 0.25f), new Vector3(x1 + 0.15f, h + 0.9f, z1 - 0.25f), M.Concrete, false);
            CampusKit.Span("Plinth", b, new Vector3(x0 - 0.1f, 0, z0 - 0.1f), new Vector3(x1 + 0.1f, 0.6f, z1 + 0.1f), M.Concrete, false);
            for (int i = 0; i < 4; i++)
            {
                // the four faces: N, S, W, E
                char face = "NSWE"[i];
                bool alongX = face == 'N' || face == 'S';
                float at = face == 'N' ? z1 : face == 'S' ? z0 : face == 'W' ? x0 : x1;
                float outward = face == 'N' || face == 'E' ? 1f : -1f;
                float a0 = alongX ? x0 : z0, a1 = alongX ? x1 : z1;
                float sh = (h - 0.6f) / storeys;
                for (int s = 0; s < storeys; s++)
                    for (float a = a0 + 3; a < a1 - 2; a += 4)
                    {
                        if (face == doorFace && s == 0 && Mathf.Abs(a - doorAt) < 4.2f) continue;
                        float y = 0.6f + s * sh + sh * 0.3f, wh = sh * 0.55f;
                        float roll = (float)rnd.NextDouble();
                        Material m = roll < 0.03f ? M.GlowCold : roll < 0.07f ? WindowWarm : roll < 0.13f ? M.Black : M.GlassDark;
                        Vector3 c = alongX ? new Vector3(a, y + wh * 0.5f, at + outward * 0.03f) : new Vector3(at + outward * 0.03f, y + wh * 0.5f, a);
                        Vector3 size = alongX ? new Vector3(2.0f, wh, 0.05f) : new Vector3(0.05f, wh, 2.0f);
                        CampusKit.Box("Window", b, c, size, m, false);
                        Vector3 sc = alongX ? new Vector3(a, y - 0.06f, at + outward * 0.1f) : new Vector3(at + outward * 0.1f, y - 0.06f, a);
                        CampusKit.Box("Sill", b, sc, alongX ? new Vector3(2.3f, 0.12f, 0.2f) : new Vector3(0.2f, 0.12f, 2.3f), M.Concrete, false);
                    }
                // a band between the storeys
                for (int s = 1; s < storeys; s++)
                {
                    float y = 0.6f + s * sh;
                    // N/S bands run the full width, W/E ones stop short of them (no coplanar overlap at the corners)
                    Vector3 min = alongX ? new Vector3(x0 - 0.1f, y - 0.15f, at - 0.1f) : new Vector3(at - 0.1f, y - 0.15f, z0 + 0.1f);
                    Vector3 max = alongX ? new Vector3(x1 + 0.1f, y + 0.15f, at + 0.1f) : new Vector3(at + 0.1f, y + 0.15f, z1 - 0.1f);
                    CampusKit.Span("Band", b, min, max, M.Concrete, false);
                }
                if (face != doorFace) continue;
                // the entrance: chained double doors under a canopy, steps, a wall light, the name
                Vector3 dir = alongX ? new Vector3(0, 0, outward) : new Vector3(outward, 0, 0);
                Vector3 baseP = alongX ? new Vector3(doorAt, 0, at) : new Vector3(at, 0, doorAt);
                Vector3 side = alongX ? Vector3.right : Vector3.forward;
                CampusKit.Box("Entrance_Doors", b, baseP + dir * 0.04f + Vector3.up * 1.5f, alongX ? new Vector3(3.4f, 3f, 0.08f) : new Vector3(0.08f, 3f, 3.4f), M.GlassDark, false);
                CampusKit.Box("Entrance_Frame", b, baseP + dir * 0.06f + Vector3.up * 1.5f, alongX ? new Vector3(0.12f, 3f, 0.1f) : new Vector3(0.1f, 3f, 0.12f), M.Metal, false);
                CampusKit.Box("Entrance_Chain", b, baseP + dir * 0.12f + Vector3.up * 1.1f, alongX ? new Vector3(1.4f, 0.07f, 0.04f) : new Vector3(0.04f, 0.07f, 1.4f), M.MetalLight, false);
                Vector3 canopy = baseP + dir * 1.6f + Vector3.up * 3.5f;
                CampusKit.Box("Entrance_Canopy", b, canopy, alongX ? new Vector3(7f, 0.3f, 3.2f) : new Vector3(3.2f, 0.3f, 7f), M.Concrete);
                foreach (float s in new[] { -3.2f, 3.2f }) CampusKit.Cyl("Entrance_Column", b, baseP + dir * 2.9f + side * s, 0.25f, 3.4f, M.Concrete);
                CampusKit.Box("Entrance_Steps", b, baseP + dir * 1.8f + Vector3.up * 0.06f, alongX ? new Vector3(7.4f, 0.12f, 3.6f) : new Vector3(3.6f, 0.12f, 7.4f), M.Kerb);
                CampusKit.Box("Entrance_SignBoard", b, baseP + dir * 3.25f + Vector3.up * 4.05f, alongX ? new Vector3(6.4f, 0.75f, 0.06f) : new Vector3(0.06f, 0.75f, 6.4f), M.Metal, false);
                CampusKit.Text(b, sign, baseP + dir * 3.3f + Vector3.up * 4.05f, dir, 0.5f, new Color(0.42f, 0.4f, 0.34f));
                var head = CampusKit.Box("WallLight", b, baseP + dir * 0.15f + Vector3.up * 3.2f + side * 2.2f, new Vector3(0.3f, 0.2f, 0.3f), M.GlowWarm, false);
                CampusKit.Lamp(lights, baseP + dir * 1.0f + Vector3.up * 3.0f + side * 2.2f, new Color(1f, 0.76f, 0.5f), 10f, 1.6f, new[] { head.GetComponent<Renderer>() }, Chance(0.5f) ? 0.5f : 0f);
            }
            return b;
        }

        static void Library()
        {
            Building("Library", -100, -58, 46, 112, 12.5f, 3, M.BrickExt, 'E', 82, "LIBRARY");
            Pave("Library_Forecourt", -58, -52, 66, 98, M.Paving, 0.035f);
            for (int i = 0; i < 6; i++) CampusKit.Cyl("Library_Colonnade", build, new Vector3(-56.5f, 0, 68 + i * 5.6f), 0.4f, 7.5f, M.Concrete);
            CampusKit.Span("Library_Colonnade_Beam", build, new Vector3(-58, 7.5f, 67), new Vector3(-55.8f, 8.2f, 97), M.Concrete);
            Bench(-55, 72, 90); Bench(-55, 92, 90); Bin(-55.4f, 66.5f);
            // a book return chute and a reading garden on the north side
            CampusKit.Box("BookReturn", props, new Vector3(-57.2f, 0.6f, 100), new Vector3(0.8f, 1.2f, 0.7f), M.Metal);
            Pave("Library_ReadingGarden", -96, -64, 112, 118, M.Gravel, 0.03f);
            for (int i = 0; i < 4; i++) Bench(-90 + i * 8, 116, 180);
        }

        static void LectureHalls()
        {
            Building("Lecture_Halls", 58, 104, 44, 118, 13f, 3, M.BrickRed, 'W', 82, "LECTURE HALLS");
            // half-finished repairs on the north end: scaffold and hoarding left for years
            for (int i = 0; i < 4; i++) CampusKit.Prop("Scaffold_" + (i % 3), props, new Vector3(64 + i * 5, 0, 119.6f), 0);
            for (int i = 0; i < 5; i++) CampusKit.Prop("Hoarding_" + (i % 6), props, new Vector3(62 + i * 5.5f, 0, 123.5f), 0);
            CampusKit.Prop("cement_bag", props, new Vector3(70, 0, 121.5f), 30); CampusKit.Prop("Pallet_0", props, new Vector3(73, 0, 121.8f), 10);
            CampusKit.Prop("CrateStack_1", props, new Vector3(84, 0, 121.5f), 0);
            Bench(55, 72, -90); Bench(55, 92, -90); Bin(55.4f, 97);
            CampusKit.Prop("korean_public_payphone_01", props, new Vector3(57.6f, 0, 70), -90);
        }

        static void ClockTower()
        {
            var t = new GameObject("Clock_Tower").transform; t.SetParent(build, false);
            Building("Clock_Hall_West", -26, -4, 124, 142, 9f, 2, M.BrickExt, 'X', 0, "");
            Building("Clock_Hall_East", 4, 26, 124, 142, 9f, 2, M.BrickExt, 'X', 0, "");
            // the arch over the passage, and the passage itself
            CampusKit.Span("Arch_Bridge", t, new Vector3(-4, 4.6f, 124), new Vector3(4, 9f, 142), M.BrickExt);
            CampusKit.Span("Arch_Ceiling", t, new Vector3(-4, 4.4f, 124), new Vector3(4, 4.6f, 142), M.Concrete);
            Pave("Arch_Passage", -4, 4, 124, 142, M.Paving, 0.04f, false);
            CampusKit.Span("Arch_Face", t, new Vector3(-4.6f, 4.0f, 123.6f), new Vector3(4.6f, 4.6f, 124.2f), M.Concrete, false);
            CampusKit.Span("Arch_Face", t, new Vector3(-4.6f, 4.0f, 141.8f), new Vector3(4.6f, 4.6f, 142.4f), M.Concrete, false);
            var ph = CampusKit.Box("Arch_Lamp", t, new Vector3(0, 4.3f, 133), new Vector3(0.5f, 0.2f, 0.5f), M.GlowWarm, false);
            CampusKit.Lamp(lights, new Vector3(0, 3.8f, 133), new Color(1f, 0.7f, 0.45f), 12f, 1.8f, new[] { ph.GetComponent<Renderer>() }, 0.7f);
            CampusKit.Prop("NoticeBoard_3", props, new Vector3(-3.9f, 1.2f, 130), 90);
            // the tower, its clock stopped at seven minutes past three
            CampusKit.Span("Tower", t, new Vector3(-5, 9, 128), new Vector3(5, 28, 138), M.BrickExt);
            CampusKit.Span("Tower_Cornice", t, new Vector3(-5.3f, 28, 127.7f), new Vector3(5.3f, 28.6f, 138.3f), M.Concrete, false);
            foreach (var c in new[] { new Vector2(-4.6f, 128.4f), new Vector2(4.6f, 128.4f), new Vector2(-4.6f, 137.6f), new Vector2(4.6f, 137.6f) })
                CampusKit.Span("Belfry_Post", t, new Vector3(c.x - 0.4f, 28.6f, c.y - 0.4f), new Vector3(c.x + 0.4f, 33, c.y + 0.4f), M.BrickExt);
            CampusKit.Span("Belfry_Floor", t, new Vector3(-5, 28.4f, 128), new Vector3(5, 28.6f, 138), M.Concrete, false);
            CampusKit.Span("Belfry_Roof", t, new Vector3(-5.4f, 33, 127.6f), new Vector3(5.4f, 33.5f, 138.4f), M.Roof, false);
            CampusKit.Cyl("Bell", t, new Vector3(0, 29.6f, 133), 1.1f, 1.8f, M.Iron, false);
            for (int i = 0; i < 3; i++)
            {
                float w = 8.5f - i * 2.8f;
                CampusKit.Box("Spire", t, new Vector3(0, 34.2f + i * 1.3f, 133), new Vector3(w, 1.3f, w), Quaternion.Euler(0, i * 15f, 0), M.Roof, false);
            }
            CampusKit.Cyl("Spire_Tip", t, new Vector3(0, 38, 133), 0.08f, 3.5f, M.Iron, false);
            var hourPivots = new List<Transform>(); var minutePivots = new List<Transform>();
            foreach (var face in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 c = new Vector3(0, 23.5f, 133) + face * 5.02f;
                var disc = CampusKit.Cyl("Clock_Face", t, Vector3.zero, 2.4f, 0.06f, M.GlowClock, false);
                disc.transform.localPosition = c;
                disc.transform.localRotation = Quaternion.FromToRotation(Vector3.up, face);
                CampusKit.Box("Clock_Rim", t, c - face * 0.02f, Vector3.Scale(new Vector3(5.3f, 5.3f, 5.3f), new Vector3(1 - Mathf.Abs(face.x), 1, 1 - Mathf.Abs(face.z))) + face.Abs() * 0.05f, M.Iron, false);
                // hands on pivots at the face's centre (twelve o'clock at rest: the clock puzzle turns them)
                Quaternion q = Quaternion.LookRotation(face);
                var hp = new GameObject("Clock_PivotHour").transform; hp.SetParent(t, false); hp.localPosition = c + face * 0.06f; hp.localRotation = q;
                CampusKit.Box("Clock_HourHand", hp, new Vector3(0, 0.6f, 0), new Vector3(0.16f, 1.3f, 0.04f), M.Black, false);
                var mp = new GameObject("Clock_PivotMinute").transform; mp.SetParent(t, false); mp.localPosition = c + face * 0.08f; mp.localRotation = q;
                CampusKit.Box("Clock_MinuteHand", mp, new Vector3(0, 0.95f, 0), new Vector3(0.1f, 1.9f, 0.04f), M.Black, false);
                hourPivots.Add(hp); minutePivots.Add(mp);
            }
            CampusKit.Lamp(lights, new Vector3(0, 23.5f, 128), new Color(0.95f, 0.9f, 0.7f), 9f, 0.8f, null, 0.15f).important = false;

            // the clock's drive in the arch passage: a steel cabinet on the west wall with two big handwheels, and the
            // maintenance box beside it (the generator's crank handle is locked inside)
            var mech = new GameObject("Clock_Mechanism").transform; mech.SetParent(t, false); mech.localPosition = new Vector3(-3.6f, 0.04f, 136.5f);
            CampusKit.Box("Drive_Cabinet", mech, new Vector3(-0.15f, 1.1f, 0), new Vector3(0.5f, 2.2f, 2.2f), M.Iron);
            CampusKit.Text(mech, "TOWER CLOCK DRIVE", new Vector3(0.12f, 2.05f, 0), Vector3.right, 0.1f, new Color(0.75f, 0.7f, 0.55f));
            var clock = mech.gameObject.AddComponent<Puzzles.ClockPuzzle>();
            clock.title = "Clock tower"; clock.solvedMessage = "The tower strikes - the maintenance box springs open. The crank handle is inside.";
            var wheels = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var w = new GameObject(i == 0 ? "Wheel_Hours" : "Wheel_Minutes").transform; w.SetParent(mech, false);
                w.localPosition = new Vector3(0.22f, 1.25f, i == 0 ? 0.55f : -0.55f); w.localRotation = Quaternion.Euler(0, 90, 0);
                var rim = CampusKit.Cyl("Rim", w, Vector3.zero, 0.38f, 0.06f, M.Iron, false); rim.transform.localRotation = Quaternion.Euler(90, 0, 0);
                for (int k = 0; k < 3; k++) CampusKit.Box("Spoke", w, Vector3.zero, new Vector3(0.7f, 0.05f, 0.04f), Quaternion.Euler(0, 0, k * 60), M.Iron, false);
                CampusKit.Box("Handle", w, new Vector3(0.3f, 0, 0.08f), new Vector3(0.05f, 0.05f, 0.16f), M.DoorWood, false);
                CampusKit.Text(mech, i == 0 ? "HOURS" : "MINUTES", new Vector3(0.24f, 0.74f, i == 0 ? 0.55f : -0.55f), Vector3.right, 0.08f, new Color(0.8f, 0.75f, 0.6f));
                var col = w.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(0.8f, 0.8f, 0.3f);
                var part = w.gameObject.AddComponent<Puzzles.WorldPuzzlePart>(); part.puzzle = clock; part.index = i;
                wheels[i] = w;
            }
            clock.hourWheel = wheels[0]; clock.minuteWheel = wheels[1];
            clock.hourPivots = hourPivots.ToArray(); clock.minutePivots = minutePivots.ToArray();
            // the maintenance box: a small steel locker beside the drive, its door hinged on the left
            CampusKit.Box("Box_Back", mech, new Vector3(-0.1f, 0.8f, 1.6f), new Vector3(0.4f, 0.9f, 0.7f), M.MetalLight);
            var lid = new GameObject("Box_Door").transform; lid.SetParent(mech, false); lid.localPosition = new Vector3(0.12f, 0.8f, 1.95f);
            CampusKit.Box("Door", lid, new Vector3(0.0f, 0, -0.35f), new Vector3(0.03f, 0.88f, 0.7f), M.Metal);
            CampusKit.Text(lid, "MAINTENANCE", new Vector3(0.02f, 0.3f, -0.35f), Vector3.right, 0.05f, new Color(0.2f, 0.2f, 0.2f));
            clock.rewardDoor = lid; clock.rewardDoorOpen = new Vector3(0, -110, 0);
            // a caged work lamp over the drive, so it can be found in the dark passage
            var wl = CampusKit.Box("Drive_Lamp", mech, new Vector3(0.2f, 2.5f, 0.6f), new Vector3(0.2f, 0.14f, 0.2f), M.GlowWarm, false);
            CampusKit.Lamp(lights, mech.TransformPoint(new Vector3(0.6f, 2.2f, 0.6f)), new Color(1f, 0.75f, 0.45f), 6f, 1.6f, new[] { wl.GetComponent<Renderer>() }, 0.2f).important = true;
            clock.reward = Item(mech, "Crank_Handle", new Vector3(-0.05f, 0.55f, 1.6f), "crank_handle", "Generator crank handle", new Color(1f, 0.6f, 0.25f), (h, rs) =>
            {
                rs.Add(CampusKit.Box("Shaft", h, new Vector3(0, 0.03f, 0), new Vector3(0.4f, 0.04f, 0.04f), M.Iron, false).GetComponent<Renderer>());
                rs.Add(CampusKit.Box("Arm", h, new Vector3(0.18f, 0.03f, 0.1f), new Vector3(0.04f, 0.04f, 0.22f), M.Iron, false).GetComponent<Renderer>());
                rs.Add(CampusKit.Box("Grip", h, new Vector3(0.18f, 0.08f, 0.2f), new Vector3(0.05f, 0.12f, 0.05f), M.DoorWood, false).GetComponent<Renderer>());
            });
            Clock = clock;
        }

        static void Avenue()
        {
            Pave("Avenue", -5, 5, 142, 144, M.Asphalt, 0.02f);
            Pave("Avenue", -5, 5, 152, 226, M.Asphalt, 0.02f);
            Pave("Avenue_PavementW", -8, -5, 152, 226, M.Paving, 0.04f);
            Pave("Avenue_PavementE", 5, 8, 152, 226, M.Paving, 0.04f);
            Dashes(false, 0, 152, 226, 0.02f);
            CampusKit.Span("Kerb", ground, new Vector3(-5.15f, 0, 152), new Vector3(-4.95f, 0.14f, 226), M.Kerb);
            CampusKit.Span("Kerb", ground, new Vector3(4.95f, 0, 152), new Vector3(5.15f, 0.14f, 226), M.Kerb);
            for (float z = 158; z < 224; z += 16) { Victorian(-6.8f, z); Victorian(6.8f, z + 8); }
            for (float z = 156; z < 224; z += 8)
            {
                Tree(Chance(0.8f) ? Broad() : "tree_dead", -10.5f + R(-0.6f, 0.6f), z, R(0.85f, 1.05f), 0.4f);
                Tree(Chance(0.8f) ? Broad() : "tree_deadpine", 10.5f + R(-0.6f, 0.6f), z + 4, R(0.85f, 1.05f), 0.4f);
            }
        }

        // ------------------------------------------------------------------ the car park, the booth and the gate
        // detailed models (Quaternius "LowPoly Cars", CC0; Poly Haven covered_car, CC0): ordinary cars mostly, the odd
        // taxi, sports car or police car
        static readonly string[] CarModels = { "car_NormalCar1", "car_NormalCar2", "car_SUV", "car_NormalCar1", "car_NormalCar2", "car_SUV", "car_Taxi", "car_SportsCar", "car_SportsCar2", "car_Cop" };
        public static readonly List<CarAlarm> Alarms = new List<CarAlarm>();
        static int carCount;

        /// <summary>An abandoned car. Some still have a live battery (a blinking LED on the dash): their alarm can be set
        /// off to draw the giant (CarAlarm).</summary>
        static void Car(Vector3 pos, float yaw, Material body, bool tarp, float alarmChance = 0.3f)
        {
            var c = new GameObject(tarp ? "Car_Covered" : "Car").transform; c.SetParent(vehicles, false);
            bool flat = !tarp && Chance(0.3f);
            c.localPosition = pos;
            c.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(flat ? -1.6f : 0, 0, flat ? -2.4f : (Chance(0.2f) ? R(-1.5f, 1.5f) : 0));
            string model = tarp ? "covered_car" : CarModels[(carCount++ * 7 + rnd.Next(CarModels.Length)) % CarModels.Length];
            var m = CampusKit.Prop(model, c, Vector3.zero, 0, 1f, false);
            if (m == null) return;
            var rs = m.GetComponentsInChildren<Renderer>();
            Bounds bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            var col = c.gameObject.AddComponent<BoxCollider>();
            col.center = c.InverseTransformPoint(bb.center); col.size = new Vector3(bb.size.x > bb.size.z ? bb.size.z : bb.size.x, bb.size.y, bb.size.x > bb.size.z ? bb.size.x : bb.size.z);
            if (!tarp) CampusKit.Grime(m, 0.55f);   // years of dust and rain: no showroom paint
            if (flat) CampusKit.Prop(Chance(0.5f) ? "rusted_wheel_rim_01" : "rusted_wheel_rim_02", props, props.InverseTransformPoint(c.TransformPoint(new Vector3(1.6f, 0.08f, 0.9f))), R(0, 360), 1.6f, false);
            if (!tarp && Chance(alarmChance))
            {
                var alarm = c.gameObject.AddComponent<CarAlarm>();
                float len = col.size.z, wid = col.size.x, dash = col.center.y + col.size.y * 0.12f;
                alarm.led = CampusKit.Box("Alarm_LED", c, new Vector3(0.32f, dash, len * 0.12f), new Vector3(0.025f, 0.012f, 0.025f), M.GlowRed, false).GetComponent<Renderer>();
                var lights = new List<Light>(); var glass = new List<Renderer>();
                foreach (var corner in new[] { new Vector3(-wid * 0.42f, 0.72f, len * 0.5f), new Vector3(wid * 0.42f, 0.72f, len * 0.5f), new Vector3(-wid * 0.42f, 0.8f, -len * 0.5f), new Vector3(wid * 0.42f, 0.8f, -len * 0.5f) })
                {
                    glass.Add(CampusKit.Box("Indicator", c, corner, new Vector3(0.16f, 0.07f, 0.03f), M.Dark, false).GetComponent<Renderer>());
                    var l = new GameObject("Indicator_Light").AddComponent<Light>(); l.transform.SetParent(c, false);
                    l.transform.localPosition = corner + new Vector3(0, 0, Mathf.Sign(corner.z) * 0.4f);
                    l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.12f); l.range = 7f; l.intensity = 2.5f * CampusKit.LampGain; l.shadows = LightShadows.None; l.enabled = false;
                    lights.Add(l);
                }
                alarm.indicators = lights.ToArray(); alarm.indicatorGlass = glass.ToArray();
                Alarms.Add(alarm);
                c.name = "Car_LiveBattery";
            }
            else CampusKit.Cull(c.gameObject, 0.012f);
        }

        static void CarPark()
        {
            const float x0 = 14, x1 = 120, z0 = 156, z1 = 226;
            Pave("CarPark", x0, x1, z0, z1, M.Asphalt, 0.022f);
            Pave("CarPark_Entry", 60, 72, 152, 156, M.Asphalt, 0.022f);
            Pave("CarPark_ToAvenue", 8, 14, 200, 208, M.Asphalt, 0.022f);
            // stall rows facing each other across islands
            float[] rows = { 159, 177, 186, 204, 213 };
            bool[] facingNorth = { true, false, true, false, true };
            for (int r = 0; r < rows.Length; r++)
            {
                float zr = rows[r];
                for (float x = x0 + 4; x < x1 - 3; x += 2.7f)
                {
                    CampusKit.Span("Stall_Line", ground, new Vector3(x - 0.06f, 0.016f, zr), new Vector3(x + 0.06f, 0.026f, zr + 5.2f), M.Paint, false);
                    float cx = x + 1.35f;
                    if (cx > x1 - 3 || !Chance(0.42f)) continue;
                    float yaw = (facingNorth[r] ? 0 : 180) + R(-6, 6);
                    var mats = new[] { M.CarBody, M.CarBody2, M.CarBody3 };
                    Car(new Vector3(cx + R(-0.15f, 0.15f), 0.02f, zr + 2.6f + R(-0.2f, 0.2f)), yaw, mats[rnd.Next(3)], Chance(0.3f));
                }
            }
            // islands with sodium lamps between the facing rows
            foreach (float zi in new[] { 182.6f, 209.6f })
            {
                CampusKit.Span("Island", ground, new Vector3(x0 + 4, 0, zi - 0.6f), new Vector3(x1 - 4, 0.16f, zi + 0.6f), M.Kerb);
                for (float x = x0 + 8; x < x1 - 4; x += 13) Sodium(x, zi, Chance(0.2f));
            }
            Sodium(x0 + 2, 160, false); Sodium(x1 - 2, 160, Chance(0.5f)); Sodium(x1 - 2, 222, true); Sodium(40, 224.5f, false);
            // two buses left at the east end, one bay of the car park under a fallen tree
            CampusKit.Cull(CampusKit.Prop("bus_rusty", vehicles, new Vector3(112, 0, 172), 2), 0.01f);
            CampusKit.Cull(CampusKit.Prop("bus_rusty", vehicles, new Vector3(108.5f, 0, 197), -9), 0.01f);
            Log(52, 194, 70);
            CampusKit.Prop("old_tyre", props, new Vector3(30, 0, 182), 0); CampusKit.Prop("trashbag", props, new Vector3(88, 0, 157), 40);
            CampusKit.Prop("metal_trash_can", props, new Vector3(45, 0.02f, 183.6f), 0);
            Block(x0, x1, z0, z1);

            // the security booth by the gate
            var b = new GameObject("Security_Booth").transform; b.SetParent(build, false);
            float bx0 = 15, bx1 = 21, bz0 = 228.5f, bz1 = 233.5f, bh = 3f;
            Pave("Booth_Floor", bx0, bx1, bz0, bz1, M.TileVest, 0.06f);
            CampusKit.WallX(b, "Booth_Wall", bx0, bz0, bz1, 0, bh, 0.2f, M.Plaster, new CampusKit.Hole(229.2f, 230.4f, 0, 2.3f), new CampusKit.Hole(231.2f, 233f, 1.0f, 2.4f));
            CampusKit.WallX(b, "Booth_Wall", bx1, bz0, bz1, 0, bh, 0.2f, M.Plaster, new CampusKit.Hole(229.5f, 232.5f, 1.0f, 2.4f));
            CampusKit.WallZ(b, "Booth_Wall", bz0, bx0, bx1, 0, bh, 0.2f, M.Plaster, new CampusKit.Hole(16, 20, 1.0f, 2.4f));
            CampusKit.WallZ(b, "Booth_Wall", bz1, bx0, bx1, 0, bh, 0.2f, M.Plaster, new CampusKit.Hole(16, 20, 1.0f, 2.4f));
            CampusKit.Window(b, true, bx0, 231.2f, 233f, 1.0f, 2.4f, M.Metal, M.Glass, false);
            CampusKit.Window(b, true, bx1, 229.5f, 232.5f, 1.0f, 2.4f, M.Metal, M.Glass, true);
            CampusKit.Window(b, false, bz0, 16, 20, 1.0f, 2.4f, M.Metal, M.Glass, false);
            CampusKit.Window(b, false, bz1, 16, 20, 1.0f, 2.4f, M.Metal, M.Glass, false);
            CampusKit.Span("Booth_Roof", b, new Vector3(bx0 - 0.6f, bh, bz0 - 0.6f), new Vector3(bx1 + 0.6f, bh + 0.25f, bz1 + 0.6f), M.Concrete);
            CampusKit.Span("Booth_Ceiling", b, new Vector3(bx0, bh - 0.1f, bz0), new Vector3(bx1, bh, bz1), M.CeilingTiles, false);
            CampusKit.Text(b, "SECURITY", new Vector3(bx0 - 0.62f, bh + 0.12f, 231), Vector3.left, 0.22f, new Color(0.45f, 0.42f, 0.3f));
            CampusKit.Prop("desk_corner", b, new Vector3(18.6f, 0.06f, 232.4f), 0);
            CampusKit.Prop("chair_office", b, new Vector3(18.6f, 0.06f, 231.1f), 160);
            CampusKit.Prop("computer_retro", b, new Vector3(18.2f, 0.82f, 232.7f), 0, 0.8f);
            CampusKit.Prop("vintage_radio_transceiver", b, new Vector3(19.6f, 0.82f, 232.8f), 10);
            CampusKit.Prop("security_camera_01", b, new Vector3(20.8f, 2.6f, 233.3f), -135);
            CampusKit.Prop("papers_scatter", b, new Vector3(17, 0.07f, 230), 40);
            CampusKit.Prop("WallShelf_1", b, new Vector3(20.85f, 1.6f, 229.2f), -90);
            // the gate control: a grey steel cabinet with a big lever and two lamps
            var gc = new GameObject("Gate_Control"); gc.transform.SetParent(b, false); gc.transform.localPosition = new Vector3(16.6f, 0.06f, 233.05f);
            // a real electrical cabinet (Poly Haven utility_box_02); the box is only its collider now
            var cab = CampusKit.Box("Cabinet", gc.transform, new Vector3(0, 0.75f, 0), new Vector3(1.0f, 1.5f, 0.6f), M.MetalLight);
            cab.GetComponent<MeshRenderer>().enabled = false;
            CampusKit.Prop("utility_box_02", gc.transform, new Vector3(0, 0, 0.02f), 180, 1.32f, false);
            CampusKit.Box("Lever_Slot", gc.transform, new Vector3(0.2f, 1.1f, -0.31f), new Vector3(0.08f, 0.4f, 0.02f), M.Black, false);
            CampusKit.Box("Lever", gc.transform, new Vector3(0.2f, 1.3f, -0.42f), new Vector3(0.06f, 0.06f, 0.22f), stripe, false);
            var r1 = CampusKit.Box("Indicator", gc.transform, new Vector3(-0.25f, 1.3f, -0.31f), new Vector3(0.1f, 0.1f, 0.03f), M.GlowRed, false);
            var r2 = CampusKit.Box("Indicator", gc.transform, new Vector3(-0.25f, 1.1f, -0.31f), new Vector3(0.1f, 0.1f, 0.03f), M.GlowRed, false);
            CampusKit.Text(gc.transform, "MAIN GATE", new Vector3(0, 1.62f, -0.31f), Vector3.back, 0.09f, new Color(0.15f, 0.15f, 0.15f));
            GateControl = gc.AddComponent<CampusSwitch>();
            GateControl.prompt = "[E] Pull the lever - open the main gate";
            GateControl.doneMessage = "The gate motors grind - the main gate is opening";
            GateControl.requiresText = "The panel is dead - there is no power to the gate";
            GateControl.indicators = new[] { r1.GetComponent<Renderer>(), r2.GetComponent<Renderer>() };
            var tube = CampusKit.Box("Booth_Strip", b, new Vector3(18, bh - 0.14f, 231), new Vector3(1.2f, 0.05f, 0.2f), M.GlowCold, false);
            generatorLamps.Add(CampusKit.Lamp(lights, new Vector3(18, bh - 0.4f, 231), new Color(0.8f, 0.88f, 1f), 7f, 1.4f, new[] { tube.GetComponent<Renderer>() }, 0.15f, false));
            // the boom barrier across the car park's exit
            CampusKit.Box("Barrier_Post", props, new Vector3(13.5f, 0.55f, 210), new Vector3(0.35f, 1.1f, 0.35f), M.MetalLight);
            CampusKit.Box("Barrier_Arm", props, new Vector3(10.8f, 1.0f, 210), new Vector3(5.2f, 0.1f, 0.1f), Quaternion.Euler(0, 0, -4), stripe, false);
        }

        static void GateAndWall()
        {
            var g = new GameObject("Main_Gate").transform; g.SetParent(build, false);
            Pave("Gate_Plaza", -20, 14, 226, 240, M.Asphalt, 0.021f);
            Pave("Road_Out", -6, 6, 240, 330, M.Asphalt, 0.02f, false);
            var beacons = new List<CampusLamp>();
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * 7.6f;
                CampusKit.Span("Gate_Pillar", g, new Vector3(x - 0.9f, 0, WallN - 0.9f), new Vector3(x + 0.9f, 5.6f, WallN + 0.9f), M.BrickExt);
                CampusKit.Span("Gate_PillarCap", g, new Vector3(x - 1.1f, 5.6f, WallN - 1.1f), new Vector3(x + 1.1f, 6.0f, WallN + 1.1f), M.Concrete);
                var garg = CampusKit.Prop("statue_gargoyle", g, new Vector3(x, 6.0f, WallN), sx < 0 ? 200 : 160, 1.5f, false);
                var head = CampusKit.Box("Beacon", g, new Vector3(x - sx * 0.95f, 4.7f, WallN - 0.2f), new Vector3(0.12f, 0.34f, 0.34f), M.GlowRed, false);
                var l = CampusKit.Lamp(lights, new Vector3(x - sx * 1.4f, 4.6f, WallN - 0.6f), new Color(1f, 0.1f, 0.06f), 16f, 2.6f, new[] { head.GetComponent<Renderer>() });
                l.important = true; l.headGlow = 3f;
                beacons.Add(l);
            }
            // the iron arch with the college's name
            CampusKit.Span("Gate_Arch", g, new Vector3(-6.7f, 6.6f, WallN - 0.12f), new Vector3(6.7f, 6.85f, WallN + 0.12f), M.Iron);
            CampusKit.Span("Gate_Arch", g, new Vector3(-6.7f, 7.7f, WallN - 0.12f), new Vector3(6.7f, 7.95f, WallN + 0.12f), M.Iron);
            CampusKit.Text(g, "HOLLOWAY COLLEGE", new Vector3(0, 7.28f, WallN - 0.14f), Vector3.back, 0.6f, new Color(0.32f, 0.3f, 0.26f));
            CampusKit.Text(g, "HOLLOWAY COLLEGE", new Vector3(0, 7.28f, WallN + 0.14f), Vector3.forward, 0.6f, new Color(0.32f, 0.3f, 0.26f));
            // the two leaves, hung from the pillars, swinging outwards
            var gate = new GameObject("Gate_Leaves"); gate.transform.SetParent(g, false); gate.transform.localPosition = new Vector3(0, 0, WallN);
            var left = Leaf(gate.transform, -6.7f, 1f); var right = Leaf(gate.transform, 6.7f, -1f);
            Gate = gate.AddComponent<CampusGate>();
            Gate.leftLeaf = left; Gate.rightLeaf = right; Gate.beacons = beacons.ToArray();
            Gate.weldedShut = true;
            // someone sealed it from OUTSIDE: heavy chains wrapped round both leaves where they meet, a steel bar welded
            // across, scorched weld beads - visible through the bars, impossible to reach
            var seal = new GameObject("Gate_Welded").transform; seal.SetParent(g, false);
            for (int i = 0; i < 5; i++)
                CampusKit.Box("Chain", seal, new Vector3(0, 1.0f + i * 0.32f, WallN + 0.32f), new Vector3(1.4f, 0.07f, 0.09f), Quaternion.Euler(0, 0, (i % 2 == 0 ? 8 : -8)), M.Iron, false);
            CampusKit.Box("Padlock", seal, new Vector3(0.1f, 1.45f, WallN + 0.42f), new Vector3(0.16f, 0.2f, 0.08f), M.MetalLight, false);
            CampusKit.Box("Weld_Bar", seal, new Vector3(0, 2.6f, WallN + 0.3f), new Vector3(4.2f, 0.16f, 0.12f), M.Iron, false);
            CampusKit.Box("Weld_Bar", seal, new Vector3(0, 0.6f, WallN + 0.3f), new Vector3(4.2f, 0.16f, 0.12f), M.Iron, false);
            foreach (float y in new[] { 0.6f, 2.6f }) foreach (float x in new[] { -2f, 0f, 2f })
                    CampusKit.Box("Weld_Bead", seal, new Vector3(x, y, WallN + 0.24f), new Vector3(0.12f, 0.24f, 0.06f), M.Dark, false);
            // a steel plate welded over the join, bright scorch round its edges: plain from inside, through the bars
            CampusKit.Box("Weld_Plate", seal, new Vector3(0, 1.7f, WallN + 0.36f), new Vector3(1.3f, 2.9f, 0.05f), M.MetalLight, false);
            foreach (float y in new[] { 0.3f, 3.1f }) CampusKit.Box("Weld_Seam", seal, new Vector3(0, y, WallN + 0.33f), new Vector3(1.4f, 0.07f, 0.06f), M.GlowSodium, false);
            CampusKit.Text(seal, "KEEP OUT - BY ORDER", new Vector3(0, 2.2f, WallN + 0.33f), Vector3.back, 0.18f, new Color(0.75f, 0.15f, 0.1f));
            // the welders left their work lamp clamped to the arch outside, still burning: it lights the plate from behind
            var work = CampusKit.Box("Work_Lamp", seal, new Vector3(0.9f, 4.4f, WallN + 1.2f), new Vector3(0.3f, 0.22f, 0.22f), M.GlowWarm, false);
            var wl = CampusKit.Lamp(lights, new Vector3(0.9f, 3.9f, WallN + 1.6f), new Color(1f, 0.78f, 0.5f), 9f, 2.2f, new[] { work.GetComponent<Renderer>() }, 0.3f);
            wl.important = true;
            GateControl.opens = Gate;
            // the boundary wall, with invisible height on top so it can't be climbed
            var w = new GameObject("Boundary_Wall").transform; w.SetParent(build, false);
            WallRun(w, true, WallN, WallW, -8.5f); WallRun(w, true, WallN, 8.5f, WallE);
            WallRun(w, true, WallS, WallW, ServiceX - 3.2f); WallRun(w, true, WallS, ServiceX + 3.2f, WallE);
            ServiceGateBuild(w);
            WallRun(w, false, WallW, WallS, WallN); WallRun(w, false, WallE, WallS, WallN);
            // beyond the gate: the road out, hemmed in by the night
            Invisible(w, new Vector3(-14, 0, WallN), new Vector3(-13, 8, 272)); Invisible(w, new Vector3(13, 0, WallN), new Vector3(14, 8, 272));
            Invisible(w, new Vector3(-14, 0, 271), new Vector3(14, 8, 272));
            for (float z = 246; z < 330; z += 7) { Tree(Spruce(), R(-22, -12), z, R(0.9f, 1.2f), 0.3f); Tree(Spruce(), R(12, 22), z + 3, R(0.9f, 1.2f), 0.3f); }
            Sodium(-8, 262, false);
        }

        /// <summary>The old service gate in the south wall's far east end: brick piers, a single steel leaf on a
        /// magnetic lock released only from the control room, a little gatehouse, the lane out beyond.</summary>
        static void ServiceGateBuild(Transform wall)
        {
            var g = new GameObject("Service_Gate").transform; g.SetParent(build, false);
            float x = ServiceX, z = WallS;
            Pave("Service_Lane", 118, x + 4, -50.5f, -41f, M.Asphalt, 0.021f, false);
            Pave("Service_Out", x - 2.5f, x + 2.5f, z - 26f, z, M.Gravel, 0.02f, false);
            foreach (float sx in new[] { -1f, 1f })
            {
                float px = x + sx * 2.6f;
                CampusKit.Span("Service_Pier", g, new Vector3(px - 0.5f, 0, z - 0.5f), new Vector3(px + 0.5f, 3.4f, z + 0.5f), M.BrickRed);
                CampusKit.Span("Service_PierCap", g, new Vector3(px - 0.62f, 3.4f, z - 0.62f), new Vector3(px + 0.62f, 3.6f, z + 0.62f), M.Concrete);
            }
            // the leaf: a steel frame with mesh, hinged on the west pier, swinging out
            var hinge = new GameObject("Service_Leaf").transform; hinge.SetParent(g, false); hinge.localPosition = new Vector3(x - 2.1f, 0, z);
            CampusKit.Box("Leaf_Frame", hinge, new Vector3(2.1f, 1.45f, 0), new Vector3(4.2f, 2.7f, 0.08f), M.Iron, false);
            var mesh = CampusKit.Box("Leaf_Mesh", hinge, new Vector3(2.1f, 1.45f, 0.01f), new Vector3(3.9f, 2.4f, 0.02f), fenceMesh, false);
            mesh.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 5; i++) CampusKit.Box("Leaf_Bar", hinge, new Vector3(0.35f + i * 0.85f, 1.45f, 0.02f), new Vector3(0.05f, 2.6f, 0.05f), M.Iron, false);
            var lockBox = CampusKit.Box("MagLock", g, new Vector3(x + 2.05f, 1.3f, z + 0.12f), new Vector3(0.22f, 0.4f, 0.14f), M.MetalLight, false);
            var blocker = new GameObject("Service_Blocker"); blocker.transform.SetParent(g, false);
            blocker.transform.localPosition = new Vector3(x, 1.5f, z); var bc = blocker.AddComponent<BoxCollider>(); bc.size = new Vector3(4.4f, 3f, 0.4f);
            Invisible(wall, new Vector3(x - 2.2f, 3f, z - 0.3f), new Vector3(x + 2.2f, 7f, z + 0.3f));
            var beacons = new List<CampusLamp>();
            foreach (float sx in new[] { -1f, 1f })
            {
                var head = CampusKit.Box("Service_Beacon", g, new Vector3(x + sx * 2.6f, 3.75f, z - 0.1f), new Vector3(0.18f, 0.22f, 0.18f), M.GlowRed, false);
                var l = CampusKit.Lamp(lights, new Vector3(x + sx * 2.6f, 3.6f, z + 0.8f), new Color(1f, 0.1f, 0.06f), 10f, 1.8f, new[] { head.GetComponent<Renderer>() });
                l.important = true; beacons.Add(l);
            }
            CampusKit.Text(g, "SERVICE ENTRANCE - NO PUBLIC ACCESS", new Vector3(x, 3.0f, z + 0.55f), Vector3.forward, 0.16f, new Color(0.6f, 0.55f, 0.4f));
            CampusKit.Text(g, "MAGNETIC LOCK - RELEASED FROM SECURITY CONTROL", new Vector3(x + 2.05f, 1.75f, z + 0.32f), Vector3.forward, 0.06f, new Color(0.7f, 0.65f, 0.4f));
            // the gatehouse: a hut beside the lane, its window onto the gate
            float hx0 = x - 10f, hx1 = x - 6f, hz0 = z + 2f, hz1 = z + 6f;
            Pave("Gatehouse_Floor", hx0, hx1, hz0, hz1, M.Concrete, 0.05f);
            CampusKit.WallX(g, "Gatehouse_Wall", hx0, hz0, hz1, 0, 2.8f, 0.2f, M.BrickRed);
            CampusKit.WallX(g, "Gatehouse_Wall", hx1, hz0, hz1, 0, 2.8f, 0.2f, M.BrickRed, new CampusKit.Hole(hz0 + 1f, hz0 + 3f, 1f, 2.2f));
            CampusKit.WallZ(g, "Gatehouse_Wall", hz1, hx0, hx1, 0, 2.8f, 0.2f, M.BrickRed, new CampusKit.Hole(hx0 + 1.3f, hx0 + 2.7f, 0, 2.2f));
            CampusKit.WallZ(g, "Gatehouse_Wall", hz0, hx0, hx1, 0, 2.8f, 0.2f, M.BrickRed);
            CampusKit.Window(g, true, hx1, hz0 + 1f, hz0 + 3f, 1f, 2.2f, M.Metal, M.Glass, true);
            CampusKit.Span("Gatehouse_Roof", g, new Vector3(hx0 - 0.4f, 2.8f, hz0 - 0.4f), new Vector3(hx1 + 0.4f, 3.0f, hz1 + 0.4f), M.Concrete);
            CampusKit.Prop("metal_office_desk", g, new Vector3(hx0 + 2f, 0.05f, hz0 + 0.8f), 0, 0.8f);
            CampusKit.Prop("chair_office", g, new Vector3(hx0 + 2f, 0.05f, hz0 + 1.8f), 200);
            CampusKit.Prop("papers_scatter", g, new Vector3(hx0 + 1.2f, 0.06f, hz1 - 1f), 70);
            Block(hx0 - 1, hx1 + 1, hz0 - 1, hz1 + 1); Block(x - 4, x + 4, z, z + 10);
            var chain = new GameObject("Padlock_Chain"); chain.transform.SetParent(g, false);
            for (int i = 0; i < 4; i++) CampusKit.Box("Chain", chain.transform, new Vector3(x + 1.95f, 0.9f + i * 0.14f, z + 0.1f), new Vector3(0.5f, 0.04f, 0.06f), Quaternion.Euler(0, 0, i % 2 == 0 ? 10 : -10), M.Iron, false);
            CampusKit.Box("Padlock", chain.transform, new Vector3(x + 1.95f, 0.8f, z + 0.16f), new Vector3(0.1f, 0.13f, 0.05f), M.MetalLight, false);
            CampusKit.Text(chain.transform, "GROUNDSKEEPER'S KEY - ASK THE WATCHERS", new Vector3(x + 1.95f, 0.62f, z + 0.2f), Vector3.forward, 0.04f, new Color(0.85f, 0.75f, 0.45f));
            Service = g.gameObject.AddComponent<ServiceGate>();
            Service.padlock = chain; Service.padlocked = true;
            Service.leaf = hinge; Service.beacons = beacons.ToArray(); Service.blocker = bc;
            // E on the lock tells you where it is released from (a trigger the interactor can find)
            var ask = g.gameObject.AddComponent<BoxCollider>(); ask.center = new Vector3(x, 1.4f, z + 0.5f); ask.size = new Vector3(4.6f, 2.6f, 0.6f); ask.isTrigger = true;
            // beyond: a gravel lane into the trees, hemmed in
            Invisible(wall, new Vector3(x - 4f, 0, z - 30f), new Vector3(x - 3f, 8, z));
            Invisible(wall, new Vector3(x + 3f, 0, z - 30f), new Vector3(x + 4f, 8, z));
            Invisible(wall, new Vector3(x - 4f, 0, z - 31f), new Vector3(x + 4f, 8, z - 30f));
        }

        /// <summary>The watchers: four stone gargoyles on plinths round the quad, each turnable, a riddle on the fountain
        /// says what they must all watch; the fountain column's hatch holds the groundskeeper's key.</summary>
        static void WatchersBuild(Transform fountain)
        {
            var g = new GameObject("The_Watchers").transform; g.SetParent(build, false);
            var puzzle = g.gameObject.AddComponent<Puzzles.StatuePuzzle>();
            puzzle.title = "The watchers"; puzzle.solvedMessage = "Stone grinds in the fountain - a hatch drops open in its column. A key inside.";
            var spots = new[] { new Vector2(-17, 65), new Vector2(17, 65), new Vector2(-17, 99), new Vector2(17, 99) };
            var statues = new List<Transform>();
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = new Vector3(spots[i].x, 0, spots[i].y);
                CampusKit.Box("Watcher_Plinth", g, p + Vector3.up * 0.55f, new Vector3(1.3f, 1.1f, 1.3f), M.Kerb);
                var turn = new GameObject("Watcher_" + i).transform; turn.SetParent(g, false); turn.localPosition = p + Vector3.up * 1.1f;
                turn.localRotation = Quaternion.Euler(0, i * 90f, 0);
                var st = CampusKit.Prop("statue_gargoyle", turn, Vector3.zero, 0, 1.25f, false);
                if (st != null) st.transform.localRotation = Quaternion.Euler(0, 180, 0) * st.transform.localRotation;   // the model faces -z
                var col = turn.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.7f, 0); col.size = new Vector3(1.1f, 1.5f, 1.1f);
                var part = turn.gameObject.AddComponent<Puzzles.WorldPuzzlePart>(); part.puzzle = puzzle; part.index = i;
                statues.Add(turn);
                Block(p.x - 1, p.x + 1, p.z - 1, p.z + 1);
            }
            puzzle.statues = statues.ToArray();
            puzzle.targets = new[] { new Vector3(0, 0, 82), new Vector3(0, 0, 133), new Vector3(-79, 0, 79), new Vector3(81, 0, 81) };
            puzzle.riddles = new[] { "We watch where the water sleeps.", "We watch the hour that never comes.", "We watch over the silent pages.", "We watch where the lessons were shouted." };
            // the bronze plaque on the fountain's rim, facing the south path
            var plaqueMat = CampusKit.Flat("CM_Bronze", new Color(0.42f, 0.3f, 0.16f), 0.55f, 0.8f);
            CampusKit.Box("Plaque", fountain, new Vector3(0, 0.55f, -6.02f), new Vector3(1.4f, 0.5f, 0.04f), plaqueMat, false);
            puzzle.plaque = CampusKit.Text(fountain, "THE WATCHERS", new Vector3(0, 0.56f, -6.05f), Vector3.back, 0.07f, new Color(0.9f, 0.82f, 0.55f)).GetComponent<TextMesh>();
            // the hatch in the column (facing south), the key in the niche behind it
            var hatch = new GameObject("Fountain_Hatch").transform; hatch.SetParent(fountain, false); hatch.localPosition = new Vector3(-0.22f, 0.9f, -0.56f);
            CampusKit.Box("Hatch", hatch, new Vector3(0.22f, 0, 0), new Vector3(0.44f, 0.5f, 0.04f), M.Concrete, false);
            puzzle.rewardDoor = hatch; puzzle.rewardDoorOpen = new Vector3(0, 100, 0);
            puzzle.reward = Item(fountain, "Groundskeepers_Key", new Vector3(0, 0.75f, -0.45f), "gate_key", "Groundskeeper's key", new Color(1f, 0.8f, 0.3f), (h, rs) =>
            {
                var gold = CampusKit.Flat("CM_KeyGold", new Color(0.75f, 0.58f, 0.22f), 0.7f, 0.9f);
                rs.Add(CampusKit.Box("Bow", h, new Vector3(-0.05f, 0.01f, 0), new Vector3(0.05f, 0.012f, 0.05f), gold, false).GetComponent<Renderer>());
                rs.Add(CampusKit.Box("Shaft", h, new Vector3(0.03f, 0.01f, 0), new Vector3(0.12f, 0.01f, 0.014f), gold, false).GetComponent<Renderer>());
                rs.Add(CampusKit.Box("Tag", h, new Vector3(-0.1f, 0.006f, -0.02f), new Vector3(0.05f, 0.006f, 0.03f), M.Tarp, false).GetComponent<Renderer>());
            });
            Statues = puzzle;
        }

        static Transform Leaf(Transform parent, float hingeX, float extendsTowards)
        {
            var hinge = new GameObject(extendsTowards > 0 ? "Leaf_L" : "Leaf_R").transform; hinge.SetParent(parent, false);
            hinge.localPosition = new Vector3(hingeX, 0, 0);
            var leaf = CampusKit.Prop("gate_main", hinge, Vector3.zero, 0, 1f, false);
            if (leaf == null)
            {
                CampusKit.Box("Leaf_Fallback", hinge, new Vector3(extendsTowards * 3.35f, 2.2f, 0), new Vector3(6.7f, 4.4f, 0.1f), M.Iron);
                return hinge;
            }
            // whatever axis the model's length lies on, turn it along x and fit it between the pillar and the middle
            var rs = leaf.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (b.size.z > b.size.x) { leaf.transform.localRotation = Quaternion.Euler(0, 90, 0) * leaf.transform.localRotation; }
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float s = 6.65f / b.size.x;
            leaf.transform.localScale *= s;
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 want = hinge.position + new Vector3(extendsTowards * b.size.x * 0.5f, b.size.y * 0.5f, 0);
            leaf.transform.position += want - b.center;
            var c = hinge.gameObject.AddComponent<BoxCollider>();
            c.center = new Vector3(extendsTowards * 3.33f, 2.4f, 0); c.size = new Vector3(6.6f, 4.8f, 0.35f);
            return hinge;
        }

        static void WallRun(Transform p, bool alongX, float at, float a0, float a1)
        {
            Vector3 min = alongX ? new Vector3(a0, 0, at - 0.25f) : new Vector3(at - 0.25f, 0, a0);
            Vector3 max = alongX ? new Vector3(a1, 2.8f, at + 0.25f) : new Vector3(at + 0.25f, 2.8f, a1);
            CampusKit.Span("Wall", p, min, max, M.BrickExt);
            CampusKit.Span("Wall_Cap", p, min + new Vector3(alongX ? 0 : -0.08f, 2.8f, alongX ? -0.08f : 0), max + new Vector3(alongX ? 0 : 0.08f, 0.15f, alongX ? 0.08f : 0), M.Concrete, false);
            Invisible(p, min + Vector3.up * 2.8f, max + Vector3.up * 4f);
            for (float a = a0 + 8; a < a1 - 2; a += 8)
            {
                Vector3 c = alongX ? new Vector3(a, 1.6f, at) : new Vector3(at, 1.6f, a);
                CampusKit.Box("Wall_Pier", p, c, new Vector3(0.75f, 3.2f, 0.75f), M.BrickExt, false);
            }
        }

        static void Invisible(Transform p, Vector3 min, Vector3 max)
        {
            var go = new GameObject("Invisible_Wall"); go.transform.SetParent(p, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.AddComponent<BoxCollider>().size = max - min;
        }

        // ------------------------------------------------------------------ sports field
        static void SportsField()
        {
            const float x0 = -140, x1 = -40, z0 = 162, z1 = 226;
            Block(x0 - 2, x1 + 2, z0 - 2, z1 + 12);
            System.Action<float, float, float, float> line = (a, b, c, d) => CampusKit.Span("Field_Line", ground, new Vector3(a, -0.05f, b), new Vector3(c, 0.012f, d), M.Paint, false);
            line(x0, z0, x1, z0 + 0.12f); line(x0, z1 - 0.12f, x1, z1); line(x0, z0, x0 + 0.12f, z1); line(x1 - 0.12f, z0, x1, z1);
            line(-90.06f, z0, -89.94f, z1);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15 * Mathf.Deg2Rad;
                float cx = -90 + Mathf.Sin(a) * 9, cz = 194 + Mathf.Cos(a) * 9;
                CampusKit.Box("Field_Circle", ground, new Vector3(cx, -0.02f, cz), new Vector3(0.12f, 0.06f, 2.4f), Quaternion.Euler(0, i * 15 + 90, 0), M.Paint, false);
            }
            foreach (float gx in new[] { x0 + 0.5f, x1 - 0.5f })
            {
                CampusKit.Span("Goal_Post", props, new Vector3(gx - 0.06f, 0, 190.3f), new Vector3(gx + 0.06f, 2.44f, 190.4f), M.MetalLight);
                CampusKit.Span("Goal_Post", props, new Vector3(gx - 0.06f, 0, 197.6f), new Vector3(gx + 0.06f, 2.44f, 197.7f), M.MetalLight);
                CampusKit.Span("Goal_Bar", props, new Vector3(gx - 0.06f, 2.38f, 190.3f), new Vector3(gx + 0.06f, 2.5f, 197.7f), M.MetalLight, false);
                line(gx < -90 ? x0 : x1 - 16, 184, gx < -90 ? x0 + 16 : x1, 184.12f); line(gx < -90 ? x0 : x1 - 16, 203.88f, gx < -90 ? x0 + 16 : x1, 204);
                line(gx < -90 ? x0 + 15.88f : x1 - 16, 184, gx < -90 ? x0 + 16 : x1 - 15.88f, 204);
            }
            // bleachers along the north side, facing the field
            for (int s = 0; s < 5; s++)
                CampusKit.Span("Bleacher", build, new Vector3(-120, 0, z1 + 3 + s * 0.85f), new Vector3(-60, 0.45f * (s + 1), z1 + 3.85f + s * 0.85f), M.Concrete);
            CampusKit.Span("Bleacher_Back", build, new Vector3(-120, 0, z1 + 7.25f), new Vector3(-60, 3.4f, z1 + 7.45f), M.Concrete);
            for (int i = 0; i < 6; i++) CampusKit.Prop("plastic_monobloc_chair", props, new Vector3(R(-118, -62), 0.45f * (1 + rnd.Next(4)), z1 + 3.4f + 0.85f * rnd.Next(4)), R(150, 210));
            // floodlight towers, all dead but one that stutters
            foreach (var p in new[] { new Vector2(x0 - 3, z0 - 3), new Vector2(x1 + 3, z0 - 3), new Vector2(x0 - 3, z1 + 3), new Vector2(x1 + 3, z1 + 3) })
            {
                CampusKit.Span("Floodlight_Mast", build, new Vector3(p.x - 0.2f, 0, p.y - 0.2f), new Vector3(p.x + 0.2f, 15, p.y + 0.2f), M.MetalLight);
                bool live = p.x > -60 && p.y < 200;
                var head = CampusKit.Box("Floodlight_Head", build, new Vector3(p.x, 15.4f, p.y), new Vector3(2.4f, 1.2f, 0.4f), live ? M.GlowCold : M.Dark, false);
                head.transform.localRotation = Quaternion.LookRotation(new Vector3(-90 - p.x, 0, 194 - p.y).normalized) * Quaternion.Euler(20, 0, 0);
                if (live) CampusKit.Lamp(lights, new Vector3(p.x, 14.5f, p.y) + new Vector3(-90 - p.x, 0, 194 - p.y).normalized * 6f, new Color(0.8f, 0.86f, 1f), 45f, 2.2f, new[] { head.GetComponent<Renderer>() }, 0.6f);
            }
            CampusKit.Prop("dirty_football", props, new Vector3(-74, 0, 188), 0);
            Pave("Path_FieldToRing", -92, -88, 152, 162, M.Paving, 0.035f);
            Pave("Path_FieldToAvenue", -40, -8, 192, 196, M.Paving, 0.035f);
        }

        // ------------------------------------------------------------------ gardens and the maintenance yard
        static void Hedge(float x0, float x1, float z0, float z1)
        {
            CampusKit.Span("Hedge", nature, new Vector3(x0, 0, z0), new Vector3(x1, 1.5f, z1), M.Hedge);
            Block(x0, x1, z0, z1);
        }

        static void Gardens()
        {
            const float top = 0.03f;
            Pave("Garden_PathMain", -140, -136, -10, 122, M.Gravel, top);
            Pave("Garden_PathMid", -168, -112, 28, 32, M.Gravel, top + 0.004f);
            Pave("Garden_PathNorth", -168, -112, 88, 92, M.Gravel, top + 0.004f);
            Pave("Garden_PathSouth", -168, -112, -14, -10, M.Gravel, top);
            Pave("Garden_ToField", -140, -136, 122, 144, M.Gravel, top);
            // hedged beds between the paths, each with a gap to slip through
            float[] zs = { -10, 28, 32, 88, 92, 122 };
            for (int i = 0; i < zs.Length; i += 2)
            {
                float za = zs[i] + 1.5f, zb = zs[i + 1] - 1.5f;
                foreach (var side in new[] { new Vector2(-166, -142), new Vector2(-134, -114) })
                {
                    float xa = side.x, xb = side.y, mid = (za + zb) * 0.5f;
                    Hedge(xa, xb, za, za + 1); Hedge(xa, xb, zb - 1, zb);
                    Hedge(xa, xa + 1, za, mid - 2); Hedge(xa, xa + 1, mid + 2, zb);
                    Hedge(xb - 1, xb, za, mid - 2); Hedge(xb - 1, xb, mid + 2, zb);
                }
            }
            // the pond
            var pond = new GameObject("Pond").transform; pond.SetParent(build, false);
            CampusKit.Span("Pond_Water", pond, new Vector3(-160, -0.2f, 40), new Vector3(-146, 0.06f, 76), M.Water, false);
            CampusKit.Span("Pond_Bed", pond, new Vector3(-160, -0.6f, 40), new Vector3(-146, -0.15f, 76), M.Dirt, false);
            CampusKit.Span("Pond_Rim", pond, new Vector3(-160.6f, 0, 39.4f), new Vector3(-145.4f, 0.32f, 40), M.Kerb);
            CampusKit.Span("Pond_Rim", pond, new Vector3(-160.6f, 0, 76), new Vector3(-145.4f, 0.32f, 76.6f), M.Kerb);
            CampusKit.Span("Pond_Rim", pond, new Vector3(-160.6f, 0, 40), new Vector3(-160, 0.32f, 76), M.Kerb);
            CampusKit.Span("Pond_Rim", pond, new Vector3(-146, 0, 40), new Vector3(-145.4f, 0.32f, 76), M.Kerb);
            CampusKit.Span("Pond_Bridge", pond, new Vector3(-154.5f, 0.3f, 39), new Vector3(-151.5f, 0.45f, 77), M.Planks);
            Block(-161, -145, 39, 77);
            Bench(-162.5f, 50, 90); Bench(-143.5f, 66, -90);
            // the gazebo
            var gz = new GameObject("Gazebo").transform; gz.SetParent(build, false); gz.localPosition = new Vector3(-124, 0, 60);
            CampusKit.Cyl("Gazebo_Floor", gz, Vector3.zero, 4.2f, 0.35f, M.Planks);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45 * Mathf.Deg2Rad;
                if (i != 4) CampusKit.Span("Gazebo_Rail", gz, new Vector3(Mathf.Sin(a) * 3.6f - 0.6f, 0.35f, Mathf.Cos(a) * 3.6f - 0.05f) , new Vector3(Mathf.Sin(a) * 3.6f + 0.6f, 1.2f, Mathf.Cos(a) * 3.6f + 0.05f), M.DoorWood, false);
                CampusKit.Cyl("Gazebo_Post", gz, new Vector3(Mathf.Sin(a + 0.39f) * 3.9f, 0.35f, Mathf.Cos(a + 0.39f) * 3.9f), 0.12f, 2.9f, M.DoorWood, false);
            }
            CampusKit.Cyl("Gazebo_Roof", gz, new Vector3(0, 3.2f, 0), 4.7f, 0.25f, M.Roof, false);
            CampusKit.Cyl("Gazebo_Roof2", gz, new Vector3(0, 3.45f, 0), 3.2f, 0.4f, M.Roof, false);
            CampusKit.Cyl("Gazebo_Roof3", gz, new Vector3(0, 3.85f, 0), 1.6f, 0.45f, M.Roof, false);
            CampusKit.Prop("painted_wooden_bench", gz, new Vector3(0, 0.35f, 2.6f), 180);
            var gh = CampusKit.Box("Gazebo_Lantern", gz, new Vector3(0, 2.9f, 0), new Vector3(0.3f, 0.4f, 0.3f), M.GlowWarm, false);
            CampusKit.Lamp(lights, new Vector3(-124, 2.6f, 60), new Color(1f, 0.7f, 0.42f), 9f, 1.4f, new[] { gh.GetComponent<Renderer>() }, 0.4f);
            Block(-129, -119, 55, 65);
            Pave("Garden_ToGazebo", -136, -128, 58, 62, M.Gravel, top);
            // a statue at the head of the main path, planters and wild growth
            CampusKit.Box("Statue_Plinth", build, new Vector3(-138, 0.7f, 108), new Vector3(2.4f, 1.4f, 3.2f), M.Kerb);
            CampusKit.Cull(CampusKit.Prop("horse_statue_01", build, new Vector3(-138, 1.4f, 108), 90, 2.2f), 0.008f);
            Block(-140, -136, 106, 110);
            for (int i = 0; i < 8; i++) CampusKit.Prop("PlanterBed_" + (i % 6), props, new Vector3(-138 + (i % 2 == 0 ? -3.5f : 3.5f), 0, -6 + i * 4), 0);
            // lamps all along the garden paths, so the gardens read at night
            for (float z = -6; z < 120; z += 16) { Victorian(-134.6f, z); Victorian(-141.4f, z + 8); }
            for (float x = -162; x < -112; x += 16) { Victorian(x, 33.2f); Victorian(x + 8, 86.8f); Victorian(x + 4, -9.0f); }
            Victorian(-138, -20); Victorian(-143.5f, -38);
            for (int i = 0; i < 26; i++)
            {
                float x = R(-166, -112), z = R(-8, 120);
                string[] wild = { "nettle_plant_", "dry_branches_medium_01_", "fern_02_" };
                string n = wild[rnd.Next(3)];
                if (Free(x, z, 0.5f)) CampusKit.Cull(CampusKit.Prop(n + rnd.Next(3), nature, new Vector3(x, 0, z), R(0, 360), R(0.8f, 1.4f), false), 0.015f);
            }
            for (int i = 0; i < 10; i++) Tree(Chance(0.5f) ? "tree_crooked" : "tree_dead", R(-166, -112), R(-8, 120), R(0.8f, 1.2f), 0.4f, 3f);
            Log(-122, 100, 30); Log(-160, 10, 110);
            for (int i = 0; i < 16; i++) Bush(R(-166, -112), R(-8, 122));
        }

        static void MaintenanceYard()
        {
            const float x0 = -168, x1 = -146, z0 = -48, z1 = -22;
            Pave("Yard", x0, x1, z0, z1, M.ConcreteDark, 0.03f);
            Pave("Yard_ToGarden", -140, -136, -22, -14.02f, M.Gravel, 0.03f);
            // chain-link fence with two gaps: east onto the service road, north-east into the gardens
            var f = new GameObject("Yard_Fence").transform; f.SetParent(build, false);
            System.Action<bool, float, float, float> run = (alongX, at, a, b) =>
            {
                Vector3 min = alongX ? new Vector3(a, 0, at - 0.03f) : new Vector3(at - 0.03f, 0, a);
                Vector3 max = alongX ? new Vector3(b, 2.4f, at + 0.03f) : new Vector3(at + 0.03f, 2.4f, b);
                var m = CampusKit.Span("Fence_Mesh", f, min, max, fenceMesh);
                if (m != null) m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                CampusKit.Span("Fence_Rail", f, min + Vector3.up * 2.35f, max + Vector3.up * 0.06f, M.MetalLight, false);
                for (float p = a; p <= b + 0.01f; p += 3f)
                    CampusKit.Box("Fence_Post", f, alongX ? new Vector3(p, 1.25f, at) : new Vector3(at, 1.25f, p), new Vector3(0.07f, 2.5f, 0.07f), M.MetalLight, false);
            };
            run(true, z1, x0, -146f); run(true, z0, x0, x1);
            run(false, x0, z0, z1); run(false, x1, z0, -46); run(false, x1, -40, z1);
            Block(x0, x1, z0, z1);
            // the shed
            Building("Yard_Shed", -168, -160, -48, -40, 4f, 1, M.BrickRed, 'X', 0, "");
            // the generator: a big diesel set on a skid, its panel facing the yard
            var gen = new GameObject("Generator"); gen.transform.SetParent(build, false); gen.transform.localPosition = new Vector3(-156, 0.03f, -32);
            CampusKit.Box("Skid", gen.transform, new Vector3(0, 0.1f, 0), new Vector3(4.4f, 0.2f, 2.0f), M.Metal);
            CampusKit.Box("Housing", gen.transform, new Vector3(0, 1.1f, 0), new Vector3(4.0f, 1.8f, 1.7f), genGreen);
            CampusKit.Box("Grille", gen.transform, new Vector3(-2.01f, 1.1f, 0), new Vector3(0.04f, 1.3f, 1.3f), M.Dark, false);
            CampusKit.Cyl("Exhaust", gen.transform, new Vector3(1.4f, 2.0f, 0.4f), 0.12f, 1.2f, M.Iron, false);
            CampusKit.Box("Fuel_Tank", gen.transform, new Vector3(2.6f, 0.6f, 0), new Vector3(0.9f, 1.0f, 1.4f), M.MetalLight);
            CampusKit.Box("Panel", gen.transform, new Vector3(0.6f, 1.25f, -0.88f), new Vector3(1.0f, 0.7f, 0.06f), M.MetalLight, false);
            CampusKit.Box("Panel_Button", gen.transform, new Vector3(0.85f, 1.15f, -0.93f), new Vector3(0.14f, 0.14f, 0.05f), stripe, false);
            var i1 = CampusKit.Box("Indicator", gen.transform, new Vector3(0.35f, 1.4f, -0.92f), new Vector3(0.09f, 0.09f, 0.03f), M.GlowRed, false);
            CampusKit.Text(gen.transform, "DANGER - HIGH VOLTAGE", new Vector3(-0.8f, 1.6f, -0.86f), Vector3.back, 0.12f, new Color(0.5f, 0.38f, 0.1f));
            Generator = gen.AddComponent<CampusSwitch>();
            Generator.prompt = "[E] Prime and start the generator";
            Generator.doneMessage = "The generator coughs into life - the campus has power";
            Generator.humWhenOn = true;
            Generator.indicators = new[] { i1.GetComponent<Renderer>() };
            var needle = gen.AddComponent<Puzzles.NeedleLock>();
            needle.title = "GENERATOR IGNITION";
            needle.instructions = "Crank it: stop the pressure needle in the YELLOW band to catch the ignition, three times. Each miss BACKFIRES - loud.";
            needle.missMessage = "BANG - it backfires. Everything out there heard that.";
            needle.jamSeconds = 10f;
            Generator.needle = needle;
            Generator.requiredItem = "crank_handle";
            Generator.requiredItemText = "No crank handle - a tag on the generator: 'HANDLE LOCKED IN THE CLOCK TOWER DRIVE BOX - SET TOWER TO SHIFT CHANGE'";
            CampusKit.Text(gen.transform, "CRANK HANDLE: CLOCK TOWER DRIVE BOX", new Vector3(0.6f, 0.75f, -0.87f), Vector3.back, 0.08f, new Color(0.85f, 0.8f, 0.6f));
            Generator.prompt = "[E] Start the generator";
            CampusKit.Prop("power_box_01", gen.transform, new Vector3(-0.9f, 1.0f, -0.86f), 180);
            CampusKit.Prop("utility_box_01", gen.transform, new Vector3(2.6f, 0.0f, -1.3f), 180);
            CampusKit.Prop("portable_generator", props, new Vector3(-150, 0.03f, -26), 30);
            CampusKit.Prop("propane_tank", props, new Vector3(-166.5f, 0.03f, -24), 0); CampusKit.Prop("propane_tank", props, new Vector3(-165.7f, 0.03f, -24.2f), 40);
            for (int i = 0; i < 5; i++) CampusKit.Prop(Chance(0.5f) ? "Barrel_01" : "Barrel_02", props, new Vector3(-166.5f + (i % 3) * 0.75f, 0.03f, -30 - (i / 3) * 0.8f), R(0, 360));
            CampusKit.Prop("Pallet_1", props, new Vector3(-150, 0.03f, -44), 10); CampusKit.Prop("CrateStack_2", props, new Vector3(-158, 0.03f, -45.5f), 0);
            CampusKit.Prop("Tank_0", props, new Vector3(-148.5f, 0.03f, -36), 90); CampusKit.Prop("compost_bags_1", props, new Vector3(-160, 0.03f, -24), 0);
            CampusKit.Prop("hand_truck", props, new Vector3(-153, 0.03f, -27), 200); CampusKit.Prop("wooden_ladder", props, new Vector3(-159.6f, 0.03f, -39.7f), 0);
            // floodlights on poles, dead until the generator runs
            foreach (var p in new[] { new Vector3(-147, 0, -23), new Vector3(-167, 0, -36), new Vector3(-147, 0, -47) })
            {
                CampusKit.Cyl("Flood_Pole", build, p, 0.1f, 5.5f, M.MetalLight);
                var h = CampusKit.Box("Flood_Head", build, p + new Vector3(0, 5.5f, 0), new Vector3(0.6f, 0.4f, 0.3f), M.GlowCold, false);
                h.transform.localRotation = Quaternion.LookRotation(new Vector3(-157, 0, -35) - p) * Quaternion.Euler(30, 0, 0);
                generatorLamps.Add(CampusKit.Lamp(lights, p + new Vector3(0, 5.0f, 0) + (new Vector3(-157, 0, -35) - p).normalized * 1.5f, new Color(0.85f, 0.9f, 1f), 22f, 2.6f, new[] { h.GetComponent<Renderer>() }, 0f, false));
            }
            var warn = CampusKit.Box("Warning_Lamp", gen.transform, new Vector3(-1.6f, 2.1f, -0.7f), new Vector3(0.18f, 0.22f, 0.18f), M.GlowSodium, false);
            generatorLamps.Add(CampusKit.Lamp(lights, new Vector3(-157.6f, 2.5f, -32.6f), new Color(1f, 0.55f, 0.15f), 8f, 1.6f, new[] { warn.GetComponent<Renderer>() }, 0.2f, false));
            Sodium(-142, -38, true);
        }

        // ------------------------------------------------------------------ east: cafeteria and student centre
        static void Cafeteria()
        {
            Pave("Cafe_Terrace", 50, 102, -34, 22, M.Paving, 0.04f);
            Pave("Cafe_ToPlaza", 40, 50, -6, -2, M.Paving, 0.035f);
            Pave("Cafe_ToRoad", 102, 110, -6, -2, M.Paving, 0.035f);
            // the kiosk: a squat block with its shutters half down over the serving counter
            Building("Cafe_Kiosk", 86, 100, -30, -10, 4.4f, 1, M.Plaster, 'W', -20, "CAFETERIA");
            for (int i = 0; i < 3; i++)
            {
                float z = -27 + i * 3.4f;
                if (Mathf.Abs(z + 20) < 2.5f) continue;
                CampusKit.Box("Shutter", build, new Vector3(85.95f, 2.2f, z), new Vector3(0.08f, 1.6f, 2.8f), M.MetalLight, false);
                CampusKit.Prop("Counter_2.4_0.7_1.05_0", props, new Vector3(85.2f, 0.04f, z), 90);
            }
            CampusKit.Prop("CashRegister_01", props, new Vector3(85.2f, 1.09f, -23.6f), -90);
            CampusKit.Prop("CoffeeCart_01", props, new Vector3(82, 0.04f, -31), 30);
            CampusKit.Prop("Vending_2", props, new Vector3(86, 0.04f, -8.9f), 180); CampusKit.Prop("Vending_4", props, new Vector3(88, 0.04f, -8.9f), 180);
            CampusKit.Prop("metal_trash_can", props, new Vector3(84, 0.04f, -11), 0); CampusKit.Prop("trashbag", props, new Vector3(84.6f, 0.04f, -11.8f), 70);
            // the pergola, its string lights mostly dead
            var p = new GameObject("Pergola").transform; p.SetParent(build, false);
            for (float x = 56; x <= 80.1f; x += 6)
                for (float z = -26; z <= 14.1f; z += 8)
                    CampusKit.Span("Pergola_Post", p, new Vector3(x - 0.12f, 0.04f, z - 0.12f), new Vector3(x + 0.12f, 3.2f, z + 0.12f), M.DoorWood);
            for (float z = -26; z <= 14.1f; z += 8) CampusKit.Span("Pergola_Beam", p, new Vector3(55.6f, 3.2f, z - 0.1f), new Vector3(80.4f, 3.45f, z + 0.1f), M.DoorWood, false);
            for (float x = 56; x <= 80.1f; x += 1.5f) CampusKit.Span("Pergola_Slat", p, new Vector3(x - 0.05f, 3.45f, -26.4f), new Vector3(x + 0.05f, 3.6f, 14.4f), M.DoorWood, false);
            for (float z = -26; z <= 14.1f; z += 8)
                for (float x = 57; x < 80; x += 1.6f)
                {
                    float sag = Mathf.Sin((x - 56) / 6f * Mathf.PI) * 0.25f;
                    bool lit = Chance(0.35f);
                    CampusKit.Box("String_Bulb", p, new Vector3(x, 3.05f - Mathf.Abs(sag), z + 0.15f), new Vector3(0.09f, 0.12f, 0.09f), lit ? M.GlowWarm : M.Dark, false);
                }
            for (int i = 0; i < 6; i++)
                CampusKit.Lamp(lights, new Vector3(59 + (i % 3) * 9, 2.7f, -20 + (i / 3) * 24), new Color(1f, 0.68f, 0.38f), 10f, 1.2f, null, Chance(0.5f) ? 0.4f : 0f);
            // tables and chairs, some knocked over, a couple of umbrellas
            for (int i = 0; i < 12; i++)
            {
                float x = 58 + (i % 4) * 6.2f, z = -22 + (i / 4) * 12 + R(-1, 1);
                if (Chance(0.5f)) CampusKit.Prop("garden_set", props, new Vector3(x, 0.04f, z), R(0, 360));
                else CampusKit.Prop("wooden_picnic_table", props, new Vector3(x, 0.04f, z), R(-10, 10) + (Chance(0.5f) ? 90 : 0));
                if (Chance(0.35f))
                {
                    var ch = CampusKit.Prop("plastic_monobloc_chair", props, new Vector3(x + R(1.4f, 2.2f), 0.3f, z + R(-1, 1)), 0, 1f);
                    if (ch != null) ch.transform.localRotation = Quaternion.Euler(R(80, 95), R(0, 360), 0) * ch.transform.localRotation;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                float x = 90 + i * 4, z = 12;
                CampusKit.Cyl("Umbrella_Pole", props, new Vector3(x, 0.04f, z), 0.04f, 2.4f, M.MetalLight, false);
                CampusKit.Cyl("Umbrella", props, new Vector3(x, 2.35f, z), 1.5f, 0.12f, umbrella, false);
                CampusKit.Prop("garden_set", props, new Vector3(x, 0.04f, z), R(0, 360));
            }
            Victorian(51, -30); Victorian(51, 18); Victorian(101, 18);
            for (int i = 0; i < 6; i++) Bin(R(52, 100), Chance(0.5f) ? -33 : 21);
        }

        static void StudentCentre()
        {
            Building("Student_Centre", 132, 164, -30, 30, 9f, 2, M.BrickRed, 'W', 0, "STUDENT CENTRE");
            Pave("Path_ToStudentCentre", 118, 132, -3, 3, M.Paving, 0.035f);
            for (int i = 0; i < 6; i++) CampusKit.Prop("Lockers_4_0_" + (i % 3), props, new Vector3(131.6f, 0, -26 + i * 1.6f), -90);   // lockers moved out for a refit
            Bench(129, 8, -90); Bench(129, -8, -90); Bin(129.4f, 12);
            Victorian(124, 4); Victorian(124, -4);
            CampusKit.Prop("korean_public_payphone_01", props, new Vector3(131.5f, 0, 14), -90);
        }

        // ------------------------------------------------------------------ trees, bushes and the bits of furniture
        static void Tree(string model, float x, float z, float scale, float trunk, float avoid = 0f)
        {
            if (avoid > 0 && !Free(x, z, avoid)) return;
            var t = CampusKit.Prop(model, nature, new Vector3(x, 0, z), R(0, 360), scale, false);
            if (t == null) return;
            if (trunk > 0)
            {
                var c = t.AddComponent<CapsuleCollider>();
                c.radius = trunk / Mathf.Max(0.01f, t.transform.lossyScale.x); c.height = 4f / Mathf.Max(0.01f, t.transform.lossyScale.y);
                c.center = new Vector3(0, c.height * 0.5f, 0);
            }
            if (model.StartsWith("tree_oak") || model.StartsWith("tree_autumn") || model.StartsWith("tree_spruce") || model == "tree_landmark") Crown(t);
            CampusKit.Cull(t, 0.006f);
        }

        /// <summary>A tree's crown as cover from the giant: a trigger sphere on Ignore Raycast that only its eyes test
        /// (CampusGiant), so standing under a tree hides you and nothing walks into the leaves.</summary>
        static void Crown(GameObject tree)
        {
            var rs = tree.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var go = new GameObject("Crown_Cover"); go.transform.SetParent(nature, false);
            go.layer = 2;
            float radius = Mathf.Min(b.extents.x, b.extents.z) * 0.8f;
            go.transform.position = new Vector3(b.center.x, Mathf.Max(b.center.y + b.extents.y * 0.2f, 2.6f + radius * 0.5f), b.center.z);
            var c = go.AddComponent<SphereCollider>(); c.isTrigger = true; c.radius = radius;
        }

        /// <summary>One of the generated broadleaf trees (mostly green, some turning for autumn).</summary>
        static string Broad()
        {
            double r = rnd.NextDouble();
            return r < 0.3 ? "tree_oak_a" : r < 0.55 ? "tree_oak_b" : r < 0.75 ? "tree_oak_c" : "tree_autumn";
        }

        static string Spruce() { return rnd.NextDouble() < 0.55 ? "tree_spruce" : "tree_spruce_b"; }

        static void Bush(float x, float z)
        {
            if (!Free(x, z, 1f)) return;
            string[] b = { "bush_small", "bush_hero", "bush_alpha" };
            CampusKit.Cull(CampusKit.Prop(b[rnd.Next(3)], nature, new Vector3(x, 0, z), R(0, 360), R(0.8f, 1.3f), false), 0.015f);
        }

        static void Log(float x, float z, float yaw)
        {
            var l = CampusKit.Prop("log_fallen", nature, new Vector3(x, 0.35f, z), 0);
            if (l == null) return;
            l.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90, 0, 0) * l.transform.localRotation;
            CampusKit.Cull(l, 0.01f);
        }

        static void Bench(float x, float z, float yaw)
        {
            CampusKit.Cull(CampusKit.Prop("painted_wooden_bench", props, new Vector3(x, 0.035f, z), yaw), 0.02f);
        }

        static void Bin(float x, float z)
        {
            CampusKit.Cull(CampusKit.Prop("metal_trash_can", props, new Vector3(x, 0.035f, z), R(0, 360)), 0.02f);
        }

        /// <summary>A Victorian lamp post with a warm lantern; one in five is dead, some flicker.</summary>
        static void Victorian(float x, float z)
        {
            var post = CampusKit.Prop("lamp_victorian", props, new Vector3(x, 0.03f, z), R(0, 360), 1f, false);
            var col = new GameObject("Lamp_Post_Collider"); col.transform.SetParent(props, false);
            col.transform.localPosition = new Vector3(x, 1.5f, z); col.AddComponent<BoxCollider>().size = new Vector3(0.3f, 3f, 0.3f);
            bool dead = Chance(0.2f);
            var head = CampusKit.Box("Lantern_Glow", lights, new Vector3(x, 3.85f, z), new Vector3(0.26f, 0.36f, 0.26f), dead ? M.Dark : M.GlowWarm, false);
            if (post != null) CampusKit.Cull(post, 0.004f);
            if (dead) return;
            CampusKit.Lamp(lights, new Vector3(x, 3.75f, z), new Color(1f, 0.72f, 0.45f), 16f, 2.6f, new[] { head.GetComponent<Renderer>() }, Chance(0.25f) ? R(0.2f, 0.8f) : 0f);
        }

        /// <summary>A tall car-park lamp with an orange sodium head.</summary>
        static void Sodium(float x, float z, bool dead)
        {
            var post = CampusKit.Prop(Chance(0.5f) ? "lamp_parking_0" : "lamp_parking_1", props, new Vector3(x, 0.1f, z), R(0, 360), 1f, false);
            var col = new GameObject("Lamp_Post_Collider"); col.transform.SetParent(props, false);
            col.transform.localPosition = new Vector3(x, 2f, z); col.AddComponent<BoxCollider>().size = new Vector3(0.35f, 4f, 0.35f);
            if (post != null) CampusKit.Cull(post, 0.003f);
            var head = CampusKit.Box("Sodium_Glow", lights, new Vector3(x, 7.3f, z), new Vector3(0.5f, 0.12f, 0.3f), dead ? M.Dark : M.GlowSodium, false);
            if (dead) return;
            CampusKit.Lamp(lights, new Vector3(x, 6.8f, z), new Color(1f, 0.58f, 0.25f), 22f, 2.8f, new[] { head.GetComponent<Renderer>() }, Chance(0.3f) ? 0.5f : 0f);
        }

        static void Trees()
        {
            // the generator also brings these back: the yard floods and the booth's light
            Generator.lamps = generatorLamps.ToArray();
            GateControl.requires = Generator;
            // pines all round outside the wall make the skyline; inside, maples and dead trees between everything
            for (float x = WallW - 4; x < WallE + 4; x += 7)
            {
                if (Mathf.Abs(x - ServiceX) > 6f) Tree(Spruce(), x + R(-2, 2), WallS - R(5, 14), R(0.9f, 1.3f), 0f);
                if (Mathf.Abs(x) > 26) Tree(Spruce(), x + R(-2, 2), WallN + R(5, 16), R(0.9f, 1.3f), 0f);
            }
            for (float z = WallS; z < WallN + 4; z += 7)
            {
                Tree(Spruce(), WallW - R(5, 14), z + R(-2, 2), R(0.9f, 1.3f), 0f);
                Tree(Spruce(), WallE + R(5, 14), z + R(-2, 2), R(0.9f, 1.3f), 0f);
            }
            string[] inner = { "B", "B", "B", "B", "S", "S", "tree_dead", "tree_deadpine" };
            int placed = 0;
            for (int tries = 0; tries < 900 && placed < 170; tries++)
            {
                float x = R(WallW + 4, WallE - 4), z = R(WallS + 4, WallN - 6);
                if (!Free(x, z, 3.5f)) continue;
                string pick = inner[rnd.Next(inner.Length)];
                Tree(pick == "B" ? Broad() : pick == "S" ? Spruce() : pick, x, z, R(0.8f, 1.2f), 0.4f);
                placed++;
            }
            for (int tries = 0; tries < 600; tries++)
            {
                float x = R(WallW + 3, WallE - 3), z = R(WallS + 3, WallN - 4);
                Bush(x, z);
            }
            for (int i = 0; i < 6; i++) { float x = R(-100, 100), z = R(-46, 140); if (Free(x, z, 3)) Log(x, z, R(0, 180)); }
        }
    }

    static class VecExt
    {
        public static Vector3 Abs(this Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }
    }
}
