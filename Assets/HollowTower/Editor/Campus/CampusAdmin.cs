using System.Collections.Generic;
using HollowTower.Campus;
using UnityEngine;
using M = HollowTower.EditorTools.CampusMats;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The administration building (72 x 48 m, two storeys), where the final level starts. Ground floor: the elevator
    /// vestibule (the party arrives here from floor 2), a double-height reception atrium under a skylight with the grand
    /// staircase, open-plan offices and the records archive to the west, the seminar hall and staff lounge to the east.
    /// Upper floor: a mezzanine round the atrium, admissions and finance offices (west), the boardroom and the head
    /// administrator's suite (east) - whose keypad-locked office holds the main entrance key. The main entrance faces
    /// north onto the campus.
    /// </summary>
    public static class CampusAdmin
    {
        public const float H = 4.2f, Top = 8.4f, T = 0.3f, X0 = -36f, X1 = 36f, Z0 = -24f, Z1 = 24f;
        const float CeilG = 3.85f, CeilU = 8.25f;

        public static PartyItem Key;
        public static CampusKeyDoor MainDoor;
        public static readonly List<Vector3> Spawns = new List<Vector3>();

        static Transform root, shell, rooms, lights;
        public static Transform Root { get { return root; } }
        public static Transform Rooms { get { return rooms; } }
        public static Transform LightsGroup { get { return lights; } }
        static System.Random rnd;

        public static Transform Build(Transform parent)
        {
            rnd = new System.Random(1971);
            Spawns.Clear();
            root = new GameObject("Admin_Building").transform; root.SetParent(parent, false);
            shell = Group("Shell"); rooms = Group("Rooms"); lights = Group("Lights");
            Floors();
            ExteriorWalls();
            Roof();
            Portico();
            GroundWalls();
            UpperFloor();
            Elevator();
            Dress();
            Lighting();
            KeyAndDoors();
            return root;
        }

        static Transform Group(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }
        static float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        static bool Chance(float p) { return rnd.NextDouble() < p; }

        // ------------------------------------------------------------------ floors and ceilings
        static void Slab(string n, float x0, float x1, float z0, float z1, float y, Material floor, Material ceil)
        {
            CampusKit.Span(n + "_Floor", shell, new Vector3(x0, y - 0.3f, z0), new Vector3(x1, y, z1), floor);
            if (ceil != null) CampusKit.Span(n + "_Ceiling", shell, new Vector3(x0, y - 0.35f, z0), new Vector3(x1, y - 0.3f, z1), ceil, false);
        }

        static void Floors()
        {
            // ground
            Slab("Vestibule", -14, 14, Z0, -12, 0, M.TileVest, null);
            Slab("Atrium", -14, 14, -12, Z1, 0, M.TileLobby, null);
            Slab("Offices", X0, -14, 2, Z1, 0, M.Carpet, null);
            Slab("Records", X0, -14, Z0, 2, 0, M.ConcreteDark, null);
            Slab("Seminar", 14, X1, -2, Z1, 0, M.Carpet, null);
            Slab("Lounge", 14, X1, Z0, -2, 0, M.WoodFloor, null);
            // upper (the ground floor's ceiling is the underside); the atrium void and the stair well stay open
            Slab("Upper_West", X0, -14, Z0, Z1, H, M.Carpet, M.Ceiling);
            Slab("Upper_WestMezz", -14, -11, -9, Z1, H, M.TileLobby, M.Ceiling);
            Slab("Upper_South", -14, 14, Z0, -9, H, M.TileLobby, M.Ceiling);
            Slab("Upper_EastMezz", 11, 14, -9, Z1, H, M.TileLobby, M.Ceiling);
            Slab("Upper_Landing", 7.5f, 11, -9, 4, H, M.TileLobby, M.Ceiling);
            Slab("Upper_Boardroom", 14, X1, Z0, 4, H, M.WoodFloor, M.CeilingTiles);
            Slab("Upper_Secretary", 14, 24, 4, Z1, H, M.CarpetRed, M.CeilingTiles);
            Slab("Upper_Head", 24, X1, 4, Z1, H, M.WoodFloor, M.CeilingTiles);
        }

        static void Roof()
        {
            // one solid roof over the whole building, its ceiling under it (the atrium rises the full two storeys)
            CampusKit.Span("Roof", shell, new Vector3(X0, Top, Z0), new Vector3(X1, Top + 0.3f, Z1), M.Roof);
            CampusKit.Span("Ceiling_Top", shell, new Vector3(X0 + 0.15f, Top - 0.15f, Z0 + 0.15f), new Vector3(X1 - 0.15f, Top, Z1 - 0.15f), M.CeilingTiles, false);
            // coffered beams across the atrium ceiling, so the big ceiling isn't one flat sheet
            for (float x = -12; x <= 12.01f; x += 4f) CampusKit.Span("Atrium_Beam", shell, new Vector3(x - 0.2f, Top - 0.55f, -9), new Vector3(x + 0.2f, Top - 0.15f, Z1 - 0.15f), M.Concrete, false);
            for (float z = -8; z <= 22.01f; z += 6f) CampusKit.Span("Atrium_Beam", shell, new Vector3(-13.85f, Top - 0.5f, z - 0.15f), new Vector3(13.85f, Top - 0.15f, z + 0.15f), M.Concrete, false);
            // parapet and roof plant
            CampusKit.Span("Parapet", shell, new Vector3(X0 - 0.2f, Top, Z0 - 0.2f), new Vector3(X1 + 0.2f, Top + 1.0f, Z0 + 0.2f), M.Concrete);
            CampusKit.Span("Parapet", shell, new Vector3(X0 - 0.2f, Top, Z1 - 0.2f), new Vector3(X1 + 0.2f, Top + 1.0f, Z1 + 0.2f), M.Concrete);
            CampusKit.Span("Parapet", shell, new Vector3(X0 - 0.2f, Top, Z0 + 0.2f), new Vector3(X0 + 0.2f, Top + 1.0f, Z1 - 0.2f), M.Concrete);
            CampusKit.Span("Parapet", shell, new Vector3(X1 - 0.2f, Top, Z0 + 0.2f), new Vector3(X1 + 0.2f, Top + 1.0f, Z1 - 0.2f), M.Concrete);
            for (int i = 0; i < 5; i++)
            {
                float x = -30 + i * 6.5f, z = i % 2 == 0 ? -14 : -18;
                CampusKit.Box("RoofPlant", shell, new Vector3(x, Top + 1.2f, z), new Vector3(2.4f, 1.8f, 1.6f), M.MetalLight);
            }
            CampusKit.Box("RoofStairHouse", shell, new Vector3(-24, Top + 1.6f, 12), new Vector3(4f, 3.2f, 3.4f), M.Concrete);
        }

        // ------------------------------------------------------------------ exterior walls and windows
        static void ExteriorWalls()
        {
            var holesN = new List<CampusKit.Hole>(); var holesS = new List<CampusKit.Hole>();
            var holesW = new List<CampusKit.Hole>(); var holesE = new List<CampusKit.Hole>();
            var winN = new List<Vector4>(); var winS = new List<Vector4>(); var winW = new List<Vector4>(); var winE = new List<Vector4>();
            for (float c = X0 + 2; c < X1; c += 4)
            {
                bool entrance = Mathf.Abs(c) <= 5, curtain = Mathf.Abs(c) <= 12, shaft = Mathf.Abs(c) <= 3, back = Mathf.Abs(c + 10) < 0.1f;
                if (!entrance) Add(holesN, winN, c, 1.0f, 3.0f);
                if (!curtain) Add(holesN, winN, c, 5.2f, 7.4f);
                if (!shaft && !back) Add(holesS, winS, c, 1.0f, 3.0f);
                if (!shaft) Add(holesS, winS, c, 5.2f, 7.4f);
            }
            for (float c = Z0 + 2; c < Z1; c += 4)
            {
                Add(holesW, winW, c, 1.0f, 3.0f); Add(holesW, winW, c, 5.2f, 7.4f);
                Add(holesE, winE, c, 1.0f, 3.0f); Add(holesE, winE, c, 5.2f, 7.4f);
            }
            // the main entrance (doors and glass sidelights) and the atrium's tall north glazing
            holesN.Add(new CampusKit.Hole(-4.6f, 4.6f, 0f, 3.1f));
            holesN.Add(new CampusKit.Hole(-11f, 11f, 4.6f, 8.0f));
            holesS.Add(new CampusKit.Hole(-10.8f, -9.2f, 0f, 2.3f));   // the back door (chained shut)
            CampusKit.WallZ(shell, "Wall_North", Z1, X0, X1, 0, Top, T, M.BrickExt, holesN.ToArray());
            CampusKit.WallZ(shell, "Wall_South", Z0, X0, X1, 0, Top, T, M.BrickExt, holesS.ToArray());
            CampusKit.WallX(shell, "Wall_West", X0, Z0, Z1, 0, Top, T, M.BrickExt, holesW.ToArray());
            CampusKit.WallX(shell, "Wall_East", X1, Z0, Z1, 0, Top, T, M.BrickExt, holesE.ToArray());
            var win = new GameObject("Windows").transform; win.SetParent(shell, false);
            foreach (var w in winN) CampusKit.Window(win, false, Z1, w.x, w.y, w.z, w.w, M.Metal, M.Glass, Chance(0.15f));
            foreach (var w in winS) CampusKit.Window(win, false, Z0, w.x, w.y, w.z, w.w, M.Metal, M.Glass, Chance(0.15f));
            foreach (var w in winW) CampusKit.Window(win, true, X0, w.x, w.y, w.z, w.w, M.Metal, M.Glass, Chance(0.15f));
            foreach (var w in winE) CampusKit.Window(win, true, X1, w.x, w.y, w.z, w.w, M.Metal, M.Glass, Chance(0.15f));
            for (float x = -11; x < 11; x += 2.2f) CampusKit.Window(win, false, Z1, x, x + 2.2f, 4.6f, 8.0f, M.Metal, M.Glass, false);
            CampusKit.Window(win, false, Z1, -4.6f, -1.7f, 0.05f, 3.1f, M.Metal, M.Glass, false);
            CampusKit.Window(win, false, Z1, 1.7f, 4.6f, 0.05f, 3.1f, M.Metal, M.Glass, true);
            CampusKit.Span("Entrance_Transom", shell, new Vector3(-1.7f, 2.95f, Z1 - 0.1f), new Vector3(1.7f, 3.1f, Z1 + 0.1f), M.Metal);
            CampusKit.Box("BackDoor", shell, new Vector3(-10f, 1.15f, Z0 - 0.05f), new Vector3(1.6f, 2.3f, 0.08f), M.Metal);
            CampusKit.Box("BackDoor_Chain", shell, new Vector3(-10f, 1.1f, Z0 - 0.12f), new Vector3(1.2f, 0.06f, 0.04f), M.MetalLight, false);
            // facade bands: plinth, floor band, cornice
            foreach (var y in new[] { new Vector2(-0.1f, 0.6f), new Vector2(4.0f, 4.45f), new Vector2(8.3f, 8.75f) })
            {
                // proud of the wall's outer face (the walls are 0.3 thick, centred on the outline); the plinth band
                // breaks for the main entrance (and its glass sidelights) and the back door
                bool plinth = y.x < 0f;
                if (plinth)
                {
                    CampusKit.Span("Band", shell, new Vector3(X0 - 0.27f, y.x, Z1 + 0.15f), new Vector3(-4.6f, y.y, Z1 + 0.27f), M.Concrete, false);
                    CampusKit.Span("Band", shell, new Vector3(4.6f, y.x, Z1 + 0.15f), new Vector3(X1 + 0.27f, y.y, Z1 + 0.27f), M.Concrete, false);
                    CampusKit.Span("Band", shell, new Vector3(X0 - 0.27f, y.x, Z0 - 0.27f), new Vector3(-10.8f, y.y, Z0 - 0.15f), M.Concrete, false);
                    CampusKit.Span("Band", shell, new Vector3(-9.2f, y.x, Z0 - 0.27f), new Vector3(X1 + 0.27f, y.y, Z0 - 0.15f), M.Concrete, false);
                }
                else
                {
                    CampusKit.Span("Band", shell, new Vector3(X0 - 0.27f, y.x, Z1 + 0.15f), new Vector3(X1 + 0.27f, y.y, Z1 + 0.27f), M.Concrete, false);
                    CampusKit.Span("Band", shell, new Vector3(X0 - 0.27f, y.x, Z0 - 0.27f), new Vector3(X1 + 0.27f, y.y, Z0 - 0.15f), M.Concrete, false);
                }
                CampusKit.Span("Band", shell, new Vector3(X0 - 0.27f, y.x, Z0 - 0.15f), new Vector3(X0 - 0.15f, y.y, Z1 + 0.15f), M.Concrete, false);
                CampusKit.Span("Band", shell, new Vector3(X1 + 0.15f, y.x, Z0 - 0.15f), new Vector3(X1 + 0.27f, y.y, Z1 + 0.15f), M.Concrete, false);
            }
        }

        static void Add(List<CampusKit.Hole> holes, List<Vector4> wins, float c, float y0, float y1)
        {
            holes.Add(new CampusKit.Hole(c - 1.1f, c + 1.1f, y0, y1));
            wins.Add(new Vector4(c - 1.1f, c + 1.1f, y0, y1));
        }

        static void Portico()
        {
            var p = new GameObject("Portico").transform; p.SetParent(shell, false);
            CampusKit.Span("Canopy", p, new Vector3(-9, 4.1f, Z1), new Vector3(9, 4.55f, Z1 + 6.5f), M.Concrete);
            foreach (float x in new[] { -8f, -4.2f, 4.2f, 8f })
                CampusKit.Cyl("Column", p, new Vector3(x, 0, Z1 + 5.8f), 0.32f, 4.1f, M.Concrete);
            CampusKit.Span("Steps", p, new Vector3(-9, -0.05f, Z1), new Vector3(9, 0.12f, Z1 + 7f), M.Kerb);
            // the building's name over the doors
            CampusKit.Box("Sign_Board", p, new Vector3(0, 5.1f, Z1 + 6.6f), new Vector3(7.2f, 0.9f, 0.08f), M.Metal, false);
            CampusKit.Text(p, "ADMINISTRATION", new Vector3(0, 5.1f, Z1 + 6.66f), Vector3.forward, 0.55f, new Color(0.42f, 0.4f, 0.34f));
        }

        // ------------------------------------------------------------------ ground floor partitions
        static void GroundWalls()
        {
            var w = rooms;
            // atrium sides: an archway into the offices, double doors into the seminar hall
            CampusKit.WallX(w, "Wall_AtriumWest", -14, -12, Z1, 0, CeilG, 0.25f, M.Plaster, new CampusKit.Hole(8, 11, 0, 3.0f));
            CampusKit.WallX(w, "Wall_AtriumEast", 14, -12, Z1, 0, CeilG, 0.25f, M.Plaster, new CampusKit.Hole(8, 10.8f, 0, 2.6f));
            DoubleDoor(w, true, 14, 8, 2.8f, 2.6f);
            // vestibule: wide open into the atrium, doors to the records room and the lounge
            CampusKit.WallZ(w, "Wall_VestibuleNorth", -12, -14, 14, 0, CeilG, 0.25f, M.PlasterCool, new CampusKit.Hole(-6, 6, 0, 3.5f));
            CampusKit.WallX(w, "Wall_VestibuleWest", -14, Z0, -12, 0, CeilG, 0.25f, M.PlasterCool, new CampusKit.Hole(-18, -16.4f, 0, 2.3f));
            CampusKit.WallX(w, "Wall_VestibuleEast", 14, Z0, -12, 0, CeilG, 0.25f, M.PlasterCool, new CampusKit.Hole(-18, -16.4f, 0, 2.3f));
            Door(w, true, -14, -18, 1.6f, 2.3f, -95);
            Door(w, true, 14, -18, 1.6f, 2.3f, 95);
            // wings
            CampusKit.WallZ(w, "Wall_OfficesRecords", 2, X0, -14, 0, CeilG, 0.25f, M.Plaster, new CampusKit.Hole(-33.4f, -31.6f, 0, 2.5f), new CampusKit.Hole(-26, -24.4f, 0, 2.3f));
            Door(w, false, 2, -26, 1.6f, 2.3f, 95);
            CampusKit.WallZ(w, "Wall_SeminarLounge", -2, 14, X1, 0, CeilG, 0.25f, M.PlasterCool, new CampusKit.Hole(26, 27.6f, 0, 2.3f));
            Door(w, false, -2, 26, 1.6f, 2.3f, -95);
            // the office manager's glass box in the offices' north-west corner
            CampusKit.WallX(w, "Glass_Manager", -29, 15, Z1, 0, 0.9f, 0.12f, M.Metal);
            CampusKit.WallZ(w, "Glass_Manager", 15, X0, -29, 0, 0.9f, 0.12f, M.Metal, new CampusKit.Hole(-31, -30, 0, 0.9f));
            for (float z = 15; z < Z1 - 0.1f; z += 2.25f) CampusKit.Window(w, true, -29, z, Mathf.Min(z + 2.25f, Z1), 0.9f, CeilG, M.Metal, M.Glass, Chance(0.2f));
            for (float x = X0; x < -31f; x += 2.5f) CampusKit.Window(w, false, 15, x, x + 2.5f, 0.9f, CeilG, M.Metal, M.Glass, false);
            CampusKit.Span("Glass_Manager_Top", w, new Vector3(-31, 2.3f, 14.94f), new Vector3(-29, CeilG, 15.06f), M.Metal);
            // the seminar hall's low stage
            CampusKit.Span("Stage", w, new Vector3(31, 0, 0), new Vector3(X1, 0.5f, 22), M.Planks);
            CampusKit.Span("Stage_Step", w, new Vector3(30.4f, 0, 9.5f), new Vector3(31, 0.25f, 12.5f), M.Planks);
        }

        /// <summary>A single swinging door filling an opening (a0..a0+width along the wall), hinged at a0.</summary>
        static InteractiveDoor Door(Transform p, bool wallAlongZ, float at, float a0, float width, float height, float angle, Material mat = null)
        {
            // in a holder of its own: InteractiveDoor takes a hit on any sibling of its hinge as a press on the door,
            // so doors sharing a parent with the walls opened when you pressed E on a wall
            var holder = new GameObject("Door").transform; holder.SetParent(p, false);
            var hinge = new GameObject("Door_Hinge").transform;
            hinge.SetParent(holder, false);
            hinge.localPosition = wallAlongZ ? new Vector3(at, 0, a0) : new Vector3(a0, 0, at);
            Vector3 size = wallAlongZ ? new Vector3(0.05f, height - 0.02f, width - 0.06f) : new Vector3(width - 0.06f, height - 0.02f, 0.05f);
            Vector3 c = wallAlongZ ? new Vector3(0, height * 0.5f, width * 0.5f) : new Vector3(width * 0.5f, height * 0.5f, 0);
            CampusKit.Box("Door_Leaf", hinge, c, size, mat ?? M.DoorWood);
            Vector3 handle = wallAlongZ ? new Vector3(0, 1.0f, width - 0.15f) : new Vector3(width - 0.15f, 1.0f, 0);
            CampusKit.Box("Door_Handle", hinge, handle, new Vector3(0.12f, 0.04f, 0.12f), M.MetalLight, false);
            var d = hinge.gameObject.AddComponent<InteractiveDoor>();
            d.hingeTransform = hinge; d.openAngle = angle;
            if (Chance(0.25f)) { d.isOpen = false; }
            return d;
        }

        static void DoubleDoor(Transform p, bool wallAlongZ, float at, float a0, float width, float height)
        {
            Door(p, wallAlongZ, at, a0, width * 0.5f, height, 95);
            var right = Door(p, wallAlongZ, at, a0 + width, width * 0.5f, height, -95);
            // the right leaf hangs from the far post and faces back along the opening
            right.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }

        // ------------------------------------------------------------------ upper floor
        static void UpperFloor()
        {
            var w = rooms;
            float y0 = H, y1 = CeilU;
            // the wings' inner walls, with doors off the mezzanine
            CampusKit.WallX(w, "Wall_UpperWest", -14, Z0, Z1, y0, y1, 0.25f, M.Plaster,
                new CampusKit.Hole(10, 11.6f, y0, y0 + 2.3f), new CampusKit.Hole(-6, -4.4f, y0, y0 + 2.3f));
            Door(w, true, -14, 10, 1.6f, 2.3f, -95).transform.localPosition += Vector3.up * y0;
            Door(w, true, -14, -6, 1.6f, 2.3f, -95).transform.localPosition += Vector3.up * y0;
            CampusKit.WallZ(w, "Wall_AdmissionsFinance", 0, X0, -14, y0, y1, 0.25f, M.Plaster, new CampusKit.Hole(-26, -24.4f, y0, y0 + 2.3f));
            Door(w, false, 0, -26, 1.6f, 2.3f, 95).transform.localPosition += Vector3.up * y0;
            CampusKit.WallX(w, "Wall_UpperEast", 14, Z0, Z1, y0, y1, 0.25f, M.WoodPanel,
                new CampusKit.Hole(16, 17.6f, y0, y0 + 2.3f), new CampusKit.Hole(-8, -6.4f, y0, y0 + 2.3f));
            Door(w, true, 14, 16, 1.6f, 2.3f, 95).transform.localPosition += Vector3.up * y0;
            Door(w, true, 14, -8, 1.6f, 2.3f, 95).transform.localPosition += Vector3.up * y0;
            CampusKit.WallZ(w, "Wall_BoardroomSuite", 4, 14, X1, y0, y1, 0.25f, M.WoodPanel, new CampusKit.Hole(17.1f, 18.9f, y0, y0 + 2.5f));
            CampusKit.WallX(w, "Wall_SecretaryHead", 24, 4, Z1, y0, y1, 0.25f, M.WoodPanel, new CampusKit.Hole(12, 13.6f, y0, y0 + 2.3f));
            // the grand staircase: 24 steps up the atrium's east side, from the north end to the landing
            var stair = new GameObject("Grand_Staircase").transform; stair.SetParent(w, false);
            for (int i = 0; i < 24; i++)
            {
                float zt = 16 - 0.5f * i, top = 0.175f * (i + 1);
                CampusKit.Span("Step", stair, new Vector3(7.6f, 0, zt - 0.5f), new Vector3(10.9f, top, zt), M.Marble);
            }
            Railing(stair, new Vector3(7.55f, 0.175f, 16f), new Vector3(7.55f, H, 4f), true);
            Railing(stair, new Vector3(10.95f, 0.175f, 16f), new Vector3(10.95f, H, 4f), true);
            // round the void
            Railing(w, new Vector3(-11, H, -9), new Vector3(-11, H, Z1 - 0.2f), false);
            Railing(w, new Vector3(-11, H, -9), new Vector3(7.5f, H, -9), false);
            Railing(w, new Vector3(7.5f, H, -9), new Vector3(7.5f, H, 4), false);
            Railing(w, new Vector3(11, H, 4), new Vector3(11, H, Z1 - 0.2f), false);
        }

        /// <summary>A glass balustrade with a steel handrail from a to b (sloped for the stair).</summary>
        static void Railing(Transform p, Vector3 a, Vector3 b, bool sloped)
        {
            Vector3 d = b - a; float len = d.magnitude;
            Vector3 flat = new Vector3(d.x, 0, d.z);
            var rot = Quaternion.LookRotation(flat.normalized) * (sloped ? Quaternion.Euler(-Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg, 0, 0) : Quaternion.identity);
            Vector3 mid = (a + b) * 0.5f;
            CampusKit.Box("Rail_Top", p, mid + Vector3.up * 1.05f, new Vector3(0.07f, 0.07f, len), rot, M.MetalLight, false);
            var glass = CampusKit.Box("Rail_Glass", p, mid + Vector3.up * 0.55f, new Vector3(0.03f, 0.9f, len), rot, M.Glass, true);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 1.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector3 q = Vector3.Lerp(a, b, i / (float)(posts - 1));
                CampusKit.Box("Rail_Post", p, q + Vector3.up * 0.55f, new Vector3(0.05f, 1.1f, 0.05f), M.MetalLight, false);
            }
        }

        // ------------------------------------------------------------------ the arrival elevator
        /// <summary>The spawn elevator copied from the tower scene (CampusBuilder): the same cab the party rides in the
        /// other levels, set into the vestibule's south wall with its doors facing north.</summary>
        public static GameObject ElevatorUnit;

        static Bounds RendererBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Elevator()
        {
            var e = new GameObject("Arrival_Elevator").transform; e.SetParent(root, false);
            var arr = e.gameObject.AddComponent<CampusArrival>();   // the fade in and the floor's name; the cab does its doors
            arr.playChime = false;
            var unit = ElevatorUnit;
            if (unit == null)
            {
                Debug.LogWarning("[Campus] no spawn elevator to copy - the party starts in the vestibule");
                for (int i = 0; i < 4; i++) Spawns.Add(new Vector3(-1.5f + i, 0.06f, -18f));
                return;
            }
            unit.transform.SetParent(e, true);
            // its own cab lights were set for the tower's darker exposure: they blow out to white here
            foreach (var l in unit.GetComponentsInChildren<Light>(true)) l.intensity *= 0.35f;
            // mirror-smooth steel turns the flashlight into a white blaze: satin copies for this cab only
            var satin = new Dictionary<Material, Material>();
            foreach (var r in unit.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty("_Smoothness") || m.GetFloat("_Smoothness") < 0.9f) continue;
                    Material sm;
                    if (!satin.TryGetValue(m, out sm))
                    {
                        string mp = CampusKit.MatDir + "/CM_Elev_" + m.name + ".mat";
                        sm = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(mp);
                        if (sm == null) { sm = new Material(m); UnityEditor.AssetDatabase.CreateAsset(sm, mp); }
                        else sm.CopyPropertiesFromMaterial(m);
                        sm.SetFloat("_Smoothness", 0.5f);
                        if (sm.HasProperty("_Metallic")) sm.SetFloat("_Metallic", Mathf.Min(sm.GetFloat("_Metallic"), 0.7f));
                        UnityEditor.EditorUtility.SetDirty(sm);
                        satin[m] = sm;
                    }
                    mats[i] = sm;
                }
                r.sharedMaterials = mats;
            }
            var cab = unit.GetComponent<TowerSpawnElevator>();
            // whichever way it faced in the tower, turn it so the doors look north
            Bounds b = RendererBounds(unit);
            if (cab != null && cab.doorLeft != null && cab.doorRight != null)
            {
                Vector3 face = (cab.doorLeft.position + cab.doorRight.position) * 0.5f - b.center; face.y = 0f;
                unit.transform.RotateAround(b.center, Vector3.up, Vector3.SignedAngle(face, Vector3.forward, Vector3.up));
                cab.initialDelay = arr.fadeTime + 0.4f;
            }
            b = RendererBounds(unit);
            unit.transform.position += new Vector3(-b.center.x, -b.min.y, Z0 + 0.2f + b.extents.z - b.center.z);
            b = RendererBounds(unit);
            // a recess round it: side walls and a header up to the ceiling
            float hw = b.extents.x + 0.15f;
            CampusKit.WallX(e, "Elevator_Recess", -hw, Z0, b.max.z, 0, CeilG, 0.3f, M.PlasterCool);
            CampusKit.WallX(e, "Elevator_Recess", hw, Z0, b.max.z, 0, CeilG, 0.3f, M.PlasterCool);
            CampusKit.Span("Elevator_Header", e, new Vector3(-hw - 0.15f, b.max.y + 0.02f, b.max.z - 0.4f), new Vector3(hw + 0.15f, CeilG, b.max.z), M.PlasterCool);
            CampusKit.Box("Floor_Indicator", e, new Vector3(0, Mathf.Min(b.max.y + 0.3f, CeilG - 0.2f), b.max.z + 0.02f), new Vector3(0.5f, 0.16f, 0.03f), M.GlowGreen, false);
            Vector3 c = b.center;
            float fx = Mathf.Min(0.5f, b.extents.x - 0.45f), fz = Mathf.Min(0.45f, b.extents.z - 0.5f);
            Spawns.Add(new Vector3(c.x - fx, b.min.y + 0.3f, c.z - fz)); Spawns.Add(new Vector3(c.x + fx, b.min.y + 0.3f, c.z - fz));
            Spawns.Add(new Vector3(c.x - fx, b.min.y + 0.3f, c.z + fz)); Spawns.Add(new Vector3(c.x + fx, b.min.y + 0.3f, c.z + fz));
            Debug.Log("[Campus] spawn elevator placed: " + b.center.ToString("F2") + " size " + b.size.ToString("F2") + ", colliders " + unit.GetComponentsInChildren<Collider>(true).Length);
        }

        // ------------------------------------------------------------------ lights
        /// <summary>A ceiling strip light: steel housing and a tube; dead ones have a dark tube and no light.</summary>
        static void Strip(Vector3 pos, bool on, float flicker, Color c, float range = 9.5f, float intensity = 2.2f)
        {
            CampusKit.Box("Strip_Housing", lights, pos + Vector3.up * 0.05f, new Vector3(1.3f, 0.08f, 0.3f), M.MetalLight, false);
            var tube = CampusKit.Box("Strip_Tube", lights, pos - Vector3.up * 0.01f, new Vector3(1.2f, 0.04f, 0.18f), on ? M.GlowCold : M.Dark, false);
            // a point well below the fitting: the room lit bright, the ceiling with a soft glow rather than a hot spot
            if (on) CampusKit.Lamp(lights, pos - Vector3.up * 1.0f, c, range * 1.3f, intensity * 2.4f, new[] { tube.GetComponent<Renderer>() }, flicker);
        }

        static void Lighting()
        {
            var cold = new Color(0.78f, 0.86f, 1f); var warm = new Color(1f, 0.74f, 0.48f);
            float g = CeilG - 0.02f, u = CeilU - 0.02f;
            // vestibule: one good, one failing
            Strip(new Vector3(-6, g, -16), true, 0f, cold); Strip(new Vector3(6, g, -18), true, 0.3f, cold); Strip(new Vector3(-6, g, -21), true, 0f, cold); Strip(new Vector3(6, g, -14), true, 0f, cold);
            // atrium: pendants hanging from the roof, mostly out; reception's lamp still on
            CampusKit.Prop("light_hanging", lights, new Vector3(-4, Top - 2.6f, 4), 0, 1.6f, false);
            CampusKit.Prop("light_hanging", lights, new Vector3(4, Top - 2.6f, 12), 90, 1.6f, false);
            CampusKit.Lamp(lights, new Vector3(-4, Top - 2.9f, 4), warm, 16f, 2.6f, null, 0.15f);
            CampusKit.Lamp(lights, new Vector3(4, Top - 2.9f, 12), warm, 16f, 2.6f);
            CampusKit.Prop("light_hanging", lights, new Vector3(-6, Top - 2.6f, 18), 45, 1.6f, false);
            CampusKit.Lamp(lights, new Vector3(-6, Top - 2.9f, 18), warm, 14f, 2.2f);
            CampusKit.Lamp(lights, new Vector3(0, Top - 3.4f, -6), new Color(0.75f, 0.82f, 1f), 14f, 1.8f);   // spill under the gallery
            CampusKit.Lamp(lights, new Vector3(0, 1.6f, -3.2f), warm, 6f, 1.4f, null, 0.1f);
            // offices: rows of strips, a third dead
            for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++)
                    Strip(new Vector3(-32 + i * 7, g, 7 + j * 8), Chance(0.85f), Chance(0.2f) ? 0.4f : 0f, cold);
            // records: almost black, one lamp at the reading table
            Strip(new Vector3(-25, g, -14), true, 0.5f, cold); Strip(new Vector3(-30, g, -4), true, 0f, cold); Strip(new Vector3(-20, g, -20), true, 0f, cold);
            CampusKit.Lamp(lights, new Vector3(-31.2f, 1.2f, -2.5f), warm, 5f, 1.2f);
            // seminar hall: the projector's glow and one strip
            CampusKit.Lamp(lights, new Vector3(34.2f, 2.6f, 11), new Color(0.5f, 0.62f, 0.9f), 13f, 1.3f, null, 0.3f);
            Strip(new Vector3(22, g, 18), true, 0.2f, cold); Strip(new Vector3(22, g, 4), true, 0f, cold); Strip(new Vector3(28, g, 18), true, 0f, cold);
            // lounge: two strips still working, the floor lamp and the television
            Strip(new Vector3(22, g, -12), true, 0f, warm, 9f, 1.8f); Strip(new Vector3(30, g, -16), true, 0.15f, warm, 9f, 1.8f);
            CampusKit.Lamp(lights, new Vector3(30.2f, 1.5f, -20.5f), warm, 7f, 1.5f);
            CampusKit.Lamp(lights, new Vector3(20.5f, 1.0f, -14f), new Color(0.5f, 0.6f, 0.9f), 5f, 0.9f, null, 0.5f);
            // upper floor
            Strip(new Vector3(-25, u, 12), true, 0f, cold); Strip(new Vector3(-25, u, -12), true, 0.5f, cold);
            Strip(new Vector3(0, u, -18), true, 0.2f, cold);
            Strip(new Vector3(25, u, -10), true, 0f, cold); Strip(new Vector3(-25, u, 2), true, 0f, cold); Strip(new Vector3(-19, u, -19), true, 0f, cold); Strip(new Vector3(28, u, 12), true, 0f, warm, 8f, 1.6f);
            Strip(new Vector3(19, u, 14), true, 0f, warm, 7f, 1.2f);
            CampusKit.Lamp(lights, new Vector3(31.3f, H + 1.15f, 17.6f), new Color(0.6f, 1f, 0.6f), 5f, 1.1f);   // the head's desk lamp
            // the main entrance: lamps either side of the doors inside, a downlight over them, two under the portico
            var sconceL = CampusKit.Box("Entrance_Sconce", lights, new Vector3(-3.3f, 2.6f, Z1 - 0.2f), new Vector3(0.3f, 0.4f, 0.16f), M.GlowWarm, false);
            var sconceR = CampusKit.Box("Entrance_Sconce", lights, new Vector3(3.3f, 2.6f, Z1 - 0.2f), new Vector3(0.3f, 0.4f, 0.16f), M.GlowWarm, false);
            CampusKit.Lamp(lights, new Vector3(-3.3f, 2.5f, Z1 - 0.9f), warm, 8f, 1.6f, new[] { sconceL.GetComponent<Renderer>() }).important = true;
            CampusKit.Lamp(lights, new Vector3(3.3f, 2.5f, Z1 - 0.9f), warm, 8f, 1.6f, new[] { sconceR.GetComponent<Renderer>() }).important = true;
            CampusKit.Prop("light_hanging", lights, new Vector3(0, Top - 3.2f, Z1 - 3.5f), 90, 1.6f, false);
            CampusKit.Lamp(lights, new Vector3(0, Top - 3.6f, Z1 - 3.5f), warm, 14f, 2.6f).important = true;
            foreach (float x in new[] { -5f, 5f })
            {
                var under = CampusKit.Box("Portico_Downlight", lights, new Vector3(x, 4.05f, Z1 + 3.2f), new Vector3(0.4f, 0.06f, 0.4f), M.GlowWarm, false);
                CampusKit.Lamp(lights, new Vector3(x, 3.7f, Z1 + 3.2f), warm, 10f, 2.0f, new[] { under.GetComponent<Renderer>() }, x < 0 ? 0.25f : 0f).important = true;
            }
            // exit signs keep their green glow over the doors
            CampusKit.Box("ExitSign", lights, new Vector3(0, 3.2f, Z1 - 0.2f), new Vector3(0.8f, 0.25f, 0.06f), M.GlowExit, false);
            CampusKit.Box("ExitSign", lights, new Vector3(-13.85f, 2.6f, 9.5f), new Vector3(0.06f, 0.22f, 0.7f), M.GlowExit, false);
            CampusKit.Box("ExitSign", lights, new Vector3(13.85f, 2.9f, 9.4f), new Vector3(0.06f, 0.22f, 0.7f), M.GlowExit, false);
        }

        // ------------------------------------------------------------------ furnishing
        static GameObject P(string n, float x, float y, float z, float yaw, float s = 1f)
        {
            return CampusKit.Prop(n, rooms, new Vector3(x, y, z), yaw, s);
        }

        static void Clutter(float x0, float x1, float z0, float z1, float y, int n)
        {
            string[] bits = { "papers_scatter", "receipts", "office_notepads", "clipboard", "binder_notebook", "cardboard_box" };
            for (int i = 0; i < n; i++) P(bits[rnd.Next(bits.Length)], R(x0, x1), y + 0.01f, R(z0, z1), R(0, 360));
        }

        static void Dress()
        {
            // vestibule
            for (int i = 0; i < 4; i++) P("plastic_monobloc_chair", -11 + i * 0.75f, 0, -21.5f, 0);
            for (int i = 0; i < 3; i++) P("plastic_monobloc_chair", 9 + i * 0.75f, 0, -21.5f, 0);
            P("Vending", 12.6f, 0, -14.5f, -90); P("WaterFountain", -13.6f, 0, -14f, 90);
            P("NoticeBoard", -5, 1.2f, -23.8f, 0); P("WetFloorSign", 3.5f, 0, -15f, 30);
            P("metal_trash_can", -12.8f, 0, -22.8f, 0); P("potted_plant", 4, 0, -23.2f, 0); P("potted_plant", -4, 0, -23.2f, 0);
            Clutter(-12, 12, -23, -13, 0, 10);

            // atrium / reception
            P("Counter_7.6", 0, 0, -2.5f, 180);
            P("chair_office", -1.5f, 0, -3.6f, 0); P("chair_office", 1.6f, 0, -3.8f, 25);
            P("computer_retro", -1.6f, 0.95f, -2.6f, 180, 0.9f); P("computer_retro", 1.6f, 0.95f, -2.6f, 180, 0.9f);
            P("lamp_floor", 3.4f, 0, -3.6f, 0);
            P("sofa_sectional", -9, 0, 12, 90); P("CoffeeTable_01", -7.5f, 0, 13.5f, 0);
            P("ArmChair_01", -5, 0, 16, -120); P("ArmChair_01", -9.5f, 0, 18.5f, 180);
            P("sofa_sectional", 3, 0, 19.5f, 180); P("CoffeeTable_01", 4, 0, 17.6f, 0);
            for (int i = 0; i < 5; i++) P("plastic_monobloc_chair", -10 + i * 0.8f, 0, 2.5f, 180);
            P("potted_plant", -13, 0, 23, 0); P("potted_plant", 13, 0, 23, 0); P("potted_plant", -13, 0, -11, 0);
            P("NoticeBoard", -13.85f, 1.3f, 2, 90); P("NoticeBoard", -13.85f, 1.3f, 16, 90);
            P("TrophyCase", 13.6f, 0, 2, -90); P("TrophyCase", 13.6f, 0, -6, -90);
            P("DustSheet", -6, 0, 6, 20); P("statue_gargoyle", 0, 0, 10, 180, 1.4f);
            Clutter(-12, 6, -10, 22, 0, 18);

            // offices: cubicle desks in rows
            for (int r = 0; r < 3; r++) for (int c = 0; c < 4; c++)
                {
                    float x = -27 + c * 3.4f, z = 5 + r * 3.2f;
                    if (x < -28.5f && z > 14) continue;
                    P("CubiclePanel_1.6_1.9", x, 0, z + 1.4f, 0);
                    P("desk_corner", x, 0, z, 180);
                    if (!Chance(0.2f)) P("chair_office", x + R(-0.3f, 0.3f), 0, z - 0.9f, R(-40, 40) + (Chance(0.15f) ? 90 : 0));
                    if (Chance(0.7f)) P("computer_retro", x - 0.3f, 0.76f, z + 0.25f, 180, 0.8f);
                }
            P("Photocopier", -15.2f, 0, 20, -90); P("Photocopier", -15.2f, 0, 16.5f, -90);
            for (int i = 0; i < 6; i++) P("drawer_cabinet", -34 + i * 1.1f, 0, 23.4f, 180);
            P("metal_office_desk", -33, 0, 19.5f, 90); P("chair_office", -34.2f, 0, 19.5f, -90); P("computer_retro", -33, 0.76f, 19.8f, -90, 0.8f);
            P("WorldMap", -35.8f, 1.6f, 19.5f, 90);
            Clutter(-35, -15, 3, 23, 0, 24);

            // records: dense steel shelving
            for (int r = 0; r < 6; r++) for (int c = 0; c < 4; c++)
                {
                    float x = -34 + c * 4.6f, z = -21 + r * 3.2f;
                    if (x > -21 && z > -6) continue;
                    P(Chance(0.5f) ? "steel_frame_shelves" : "worn_metal_rack", x, 0, z, Chance(0.5f) ? 0 : 180);
                    if (Chance(0.5f)) P("paper_boxes", x + R(-1, 1), 0, z + 1.4f, R(0, 360));
                }
            P("shelf_computers", -15, 0, -22.6f, -90); P("shelf_computers", -15, 0, -20.5f, -90);
            P("wooden_table", -31, 0, -2.5f, 0); P("desk_lamp_arm", -31.4f, 0.78f, -2.5f, 30); P("WoodenChair", -31, 0, -3.6f, 10);
            Clutter(-35, -15, -23, 1, 0, 30);

            // seminar hall: rows of folding chairs facing the stage
            for (int r = 0; r < 9; r++) for (int c = 0; c < 12; c++)
                {
                    float x = 17 + r * 1.35f, z = 1.5f + c * 1.65f;
                    if (z > 9.4f && z < 12.4f) continue;   // the centre aisle
                    if (Chance(0.08f)) continue;
                    P("chair_folding_0", x, 0, z + R(-0.05f, 0.05f), -90 + R(-12, 12) + (Chance(0.05f) ? 70 : 0));
                }
            P("projector_screen", 35.6f, 0.5f, 11, -90, 1.3f);
            CampusKit.Box("Screen_Glow", rooms, new Vector3(35.75f, 3.0f, 11), new Vector3(0.04f, 2.2f, 3.6f), M.GlowScreen, false);
            P("Lectern", 32.5f, 0.5f, 6, -90); P("tool_cart", 25, 0, 11, 0); P("filmstrip_projector", 25, 0.9f, 11, 90);
            Clutter(16, 30, 0, 22, 0, 16);

            // staff lounge
            P("sofa_sectional", 24, 0, -9, 180); P("CoffeeTable_01", 22.5f, 0, -11, 0); P("modern_arm_chair", 19, 0, -11, 90);
            P("side_table", 20.5f, 0, -14.5f, 0); P("Television", 20.5f, 0.6f, -14.5f, 0);
            CampusKit.Box("TV_Glow", rooms, new Vector3(20.5f, 1.0f, -14.3f), new Vector3(0.6f, 0.35f, 0.02f), M.GlowScreen, false);
            P("Counter_3.6_0.65_0.92", 25, 0, -23.4f, 0); P("Counter_3.6_0.65_0.92", 29, 0, -23.4f, 0);
            P("Fridge", 34.6f, 0, -23.2f, 0); P("vintage_microwave", 24, 0.92f, -23.5f, 0); P("vintage_electric_kettle", 26.5f, 0.92f, -23.5f, 30);
            P("dining_table", 30, 0, -16, 0); for (int i = 0; i < 4; i++) P("dining_chair", 30 + (i % 2 == 0 ? -0.9f : 0.9f), 0, -16 + (i < 2 ? -0.5f : 0.5f), i % 2 == 0 ? 90 : -90);
            P("lamp_floor", 30.2f, 0, -20.5f, 0); P("vintage_suitcase", 33, 0, -5, 30); P("boombox", 22, 0.45f, -11, 160);
            P("book_encyclopedia_set", 23, 0.45f, -11.3f, 10); P("WallShelf", 35.8f, 1.5f, -10, -90); P("potted_plant", 15, 0, -3, 0);
            Clutter(15, 35, -23, -3, 0, 12);

            // upper: admissions (north-west) and finance (south-west)
            for (int i = 0; i < 4; i++) for (int j = 0; j < 2; j++)
                {
                    float x = -32 + i * 4.2f, z = 8 + j * 7;
                    P("metal_office_desk", x, H, z, 0); P("chair_office", x, H, z - 1, R(-30, 30));
                    if (Chance(0.6f)) P("computer_retro", x, H + 0.76f, z + 0.1f, 180, 0.8f);
                }
            for (int i = 0; i < 6; i++) P("drawer_cabinet", -35.4f, H, -2 - i * 1.1f, 90);
            for (int i = 0; i < 3; i++) { P("desk_corner", -30 + i * 5, H, -9, 0); P("chair_office", -30 + i * 5, H, -10, 0); }
            P("Photocopier", -15.3f, H, -20, -90);
            Clutter(-35, -15, -23, 23, H, 24);
            // the gallery over the vestibule
            P("Bench_2_0", -6, H, -23, 0); P("Bench_2_0", 6, H, -23, 0); P("potted_plant", 0, H, -23.3f, 0);
            P("Poster", -13.8f, H + 1.5f, -18, 90);
            // boardroom
            for (int i = 0; i < 3; i++) P("dining_table", 22 + i * 2.1f, H, -10, 90);
            for (int i = 0; i < 6; i++) { P("dining_chair", 21 + i * 1.3f, H, -11.2f, 0); P("dining_chair", 21 + i * 1.3f, H, -8.8f, 180); }
            P("projector_screen", 35.6f, H, -10, -90); P("WorldMap", 14.2f, H + 1.6f, -16, 90);
            // the secretary's office, outside the head's door
            P("desk_corner", 18, H, 10, 180); P("chair_office", 18, H, 8.8f, 0); P("computer_retro", 17.7f, H + 0.76f, 10.25f, 180, 0.8f);
            P("ArmChair_01", 22, H, 20, -90); P("ArmChair_01", 22, H, 17.5f, -90); P("potted_plant", 15, H, 23, 0);
            for (int i = 0; i < 3; i++) P("drawer_cabinet", 15 + i * 1.1f, H, 4.6f, 0);
            // the head administrator's office
            P("desk_corner", 31, H, 18, 180); P("chair_office", 31, H, 19.4f, 180);
            P("wooden_bookshelf_worn", 35.5f, H, 9, -90); P("wooden_bookshelf_worn", 35.5f, H, 13, -90);
            P("ArmChair_01", 28, H, 9, 30); P("ArmChair_01", 30.5f, H, 8, -20); P("CoffeeTable_01", 29.3f, H, 9.8f, 0);
            P("Globe", 34.5f, H, 22.5f, 0); P("GothicCabinet_01", 25, H, 23.2f, 180); P("marble_bust", 34.8f, H + 1.25f, 16.5f, -90);
            P("fancy_picture_frame", 35.8f, H + 1.6f, 18, -90); P("mantel_clock", 30.2f, H + 0.76f, 17.6f, 200);
            Clutter(25, 35, 5, 23, H, 8);
        }

        // ------------------------------------------------------------------ the key and the locked doors
        static void KeyAndDoors()
        {
            // the main entrance: two glass doors, kept locked until the key turns in them
            var door = new GameObject("Main_Entrance_Doors"); door.transform.SetParent(root, false);
            door.transform.localPosition = new Vector3(0, 0, Z1);
            var leaves = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var hinge = new GameObject(i == 0 ? "Leaf_L" : "Leaf_R").transform; hinge.SetParent(door.transform, false);
                hinge.localPosition = new Vector3(i == 0 ? -1.7f : 1.7f, 0, 0);
                float dir = i == 0 ? 1f : -1f;
                CampusKit.Box("Frame", hinge, new Vector3(dir * 0.85f, 1.45f, 0), new Vector3(1.66f, 2.9f, 0.06f), M.Metal);
                var g = CampusKit.Box("Glass", hinge, new Vector3(dir * 0.85f, 1.55f, 0.04f), new Vector3(1.3f, 2.3f, 0.02f), M.Glass, false);
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                CampusKit.Box("Bar", hinge, new Vector3(dir * 1.4f, 1.05f, -0.08f), new Vector3(0.05f, 0.9f, 0.05f), M.MetalLight, false);
                leaves[i] = hinge;
            }
            var chain = CampusKit.Box("Chain", door.transform, new Vector3(0, 1.05f, -0.14f), new Vector3(1.2f, 0.07f, 0.04f), M.MetalLight, false);
            MainDoor = door.AddComponent<CampusKeyDoor>();
            MainDoor.doorName = "main entrance doors"; MainDoor.requiredItem = "admin_key";
            MainDoor.lockedText = "Locked and chained - it needs the main entrance key";
            MainDoor.leaves = leaves; MainDoor.openAngles = new[] { -100f, 100f };
            MainDoor.hideWhenOpen = new[] { chain };   // the key opens the padlock: the chain comes off
            var col = door.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1.45f, 0); col.size = new Vector3(3.4f, 2.9f, 0.3f); col.isTrigger = false;

            // the head administrator's office door: a keypad, its code on a note taped beside it
            var hd = new GameObject("Head_Office_Door"); hd.transform.SetParent(root, false);
            hd.transform.localPosition = new Vector3(24, H, 12);
            var hinge2 = new GameObject("Leaf").transform; hinge2.SetParent(hd.transform, false);
            CampusKit.Box("Door_Leaf", hinge2, new Vector3(0, 1.15f, 0.8f), new Vector3(0.06f, 2.28f, 1.56f), M.DoorWood);
            CampusKit.Box("Door_Handle", hinge2, new Vector3(-0.06f, 1.0f, 1.4f), new Vector3(0.12f, 0.04f, 0.12f), M.MetalLight, false);
            CampusKit.Box("Keypad", hd.transform, new Vector3(-0.16f, 1.3f, -0.35f), new Vector3(0.04f, 0.22f, 0.14f), M.GlowRed, false);
            CampusKit.Box("Nameplate", hinge2, new Vector3(-0.045f, 1.75f, 0.8f), new Vector3(0.02f, 0.18f, 0.6f), M.MetalLight, false);   // on the door, swings with it
            var headDoor = hd.AddComponent<CampusKeyDoor>();
            headDoor.doorName = "head administrator's office"; headDoor.needsCode = true;
            headDoor.leaves = new[] { hinge2 }; headDoor.openAngles = new[] { 95f };
            var hcol = hd.AddComponent<BoxCollider>(); hcol.center = new Vector3(0, 1.15f, 0.8f); hcol.size = new Vector3(0.3f, 2.3f, 1.6f);
            var keypad = hd.AddComponent<Puzzles.PuzzleLock>(); keypad.title = "Head administrator's office";
            keypad.hiddenCode = true; keypad.tries = 3;
            keypad.hiddenHint = "No note. The head changed this code every week - and his secretary never could remember it. She'd have written it down somewhere close.";
            // the code: a yellow sticky note on the end of the secretary's filing cabinets, at knee height
            var note = new GameObject("Head_Code_Note"); note.transform.SetParent(root, false);
            var postIt = CampusKit.Flat("CM_PostIt", new Color(0.95f, 0.85f, 0.25f), 0.1f);
            CampusKit.Box("PostIt", note.transform, new Vector3(17.77f, H + 0.55f, 4.6f), new Vector3(0.004f, 0.11f, 0.11f), postIt, false);
            var nt = CampusKit.Text(note.transform, "000", new Vector3(17.775f, H + 0.545f, 4.6f), Vector3.right, 0.03f, new Color(0.15f, 0.1f, 0.25f));
            var ntm = nt.GetComponent<TextMesh>(); ntm.characterSize = 0.0035f; ntm.lineSpacing = 0.9f;
            var cn = note.AddComponent<Puzzles.CodeNote>(); cn.target = keypad; cn.text = ntm; cn.format = "HEAD\n{0}"; cn.separator = " ";

            // the key, on the head's desk
            var key = new GameObject("Main_Entrance_Key"); key.transform.SetParent(root, false);
            key.transform.localPosition = new Vector3(31.2f, H + 0.78f, 17.8f);
            var gold = CampusKit.Flat("CM_KeyGold", new Color(0.75f, 0.58f, 0.22f), 0.7f, 0.9f);
            gold.EnableKeyword("_EMISSION"); gold.SetColor("_EmissionColor", Color.black);
            var parts = new List<Renderer>();
            var ring = CampusKit.Cyl("Key_Bow", key.transform, Vector3.zero, 0.035f, 0.012f, gold, false); parts.Add(ring.GetComponent<Renderer>());
            parts.Add(CampusKit.Box("Key_Shaft", key.transform, new Vector3(0.075f, 0.006f, 0), new Vector3(0.1f, 0.01f, 0.014f), gold, false).GetComponent<Renderer>());
            parts.Add(CampusKit.Box("Key_Bit", key.transform, new Vector3(0.115f, 0.006f, 0.014f), new Vector3(0.02f, 0.01f, 0.02f), gold, false).GetComponent<Renderer>());
            parts.Add(CampusKit.Box("Key_Tag", key.transform, new Vector3(-0.06f, 0.004f, -0.02f), new Vector3(0.05f, 0.006f, 0.03f), M.CarpetRed, false).GetComponent<Renderer>());
            var kc = key.AddComponent<BoxCollider>(); kc.center = new Vector3(0.03f, 0.05f, 0); kc.size = new Vector3(0.4f, 0.15f, 0.3f);
            Key = key.AddComponent<PartyItem>();
            Key.itemId = "admin_key"; Key.displayName = "Main entrance key"; Key.color = new Color(1f, 0.8f, 0.35f); Key.needsTorch = true;   // like the fuse cells: it lights up in P4's torch beam
            Key.glowRenderers = parts.ToArray();
            var kl = new GameObject("Key_Glow").AddComponent<Light>(); kl.transform.SetParent(key.transform, false);
            kl.transform.localPosition = new Vector3(0, 0.3f, 0); kl.type = LightType.Point; kl.range = 2.2f; kl.intensity = 1.8f; kl.color = Key.color;
            Key.glowLight = kl; Key.glowIntensity = 1.8f;
            key.transform.localScale = Vector3.one * 2f;   // a big old master key: readable on the desk from the doorway

            // the security keycard, beside it: it opens the control room
            var card = new GameObject("Security_Keycard"); card.transform.SetParent(root, false);
            card.transform.localPosition = new Vector3(30.4f, H + 0.78f, 17.9f); card.transform.localRotation = Quaternion.Euler(0, 20, 0);
            var cardMat = CampusKit.Flat("CM_Keycard", new Color(0.85f, 0.85f, 0.8f), 0.4f);
            var cr = new List<Renderer>();
            cr.Add(CampusKit.Box("Card", card.transform, new Vector3(0, 0.003f, 0), new Vector3(0.086f, 0.004f, 0.054f), cardMat, false).GetComponent<Renderer>());
            cr.Add(CampusKit.Box("Stripe", card.transform, new Vector3(0, 0.006f, 0.012f), new Vector3(0.086f, 0.002f, 0.012f), M.CarpetRed, false).GetComponent<Renderer>());
            var cc2 = card.AddComponent<BoxCollider>(); cc2.center = new Vector3(0, 0.04f, 0); cc2.size = new Vector3(0.3f, 0.1f, 0.25f);
            var ci = card.AddComponent<PartyItem>();
            ci.itemId = "security_keycard"; ci.displayName = "Security keycard"; ci.color = new Color(0.9f, 0.4f, 0.35f); ci.needsTorch = true;
            ci.glowRenderers = cr.ToArray(); ci.glowIntensity = 1.4f;
            card.transform.localScale = Vector3.one * 2f;
        }
    }
}
