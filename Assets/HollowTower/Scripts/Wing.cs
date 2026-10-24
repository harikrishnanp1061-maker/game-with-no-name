using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Floor 2's north wing: more school, built out past the tower's north wall (nothing below it but the dark).
    /// Two doorways (from classrooms 2B and 2D) lead to a long corridor lined with lockers; off it, a double-height
    /// gymnasium, three classrooms, a science store and a double-height auditorium with a stage. Desks, bleachers and
    /// seat rows give cover; lockers, teachers' desks and costume wardrobes give hiding places.
    /// </summary>
    public static partial class LevelBuilder
    {
        public const float WingZ0 = 72f, WingZ1 = 99f;
        const float WingX0 = 6f, WingX1 = 102f, WingCorrS = 75f, WingCorrN = 78.5f, WingTall = 7f;
        /// <summary>Floor spots in the north wing, for enemies and items.</summary>
        public static readonly List<Vector3> WingSpots = new List<Vector3>();

        /// <summary>In the floor 2 north wing.</summary>
        public static bool InWing(Vector3 p) { return p.z > WingZ0 + 0.05f && p.y > Story - 1f && p.y < Story * 2f - 0.5f; }

        static void BuildNorthWing(Transform root)
        {
            WingSpots.Clear();
            var wing = new GameObject("Floor 2 north wing").transform;
            wing.SetParent(root, false);
            var props = new GameObject("Wing props").transform;
            props.SetParent(wing, false);
            float y = Story;
            // Real photographed textures (from the mansion pack), projected in world space so they tile at true scale.
            Material wall = WingMat("41", 3.2f, 0.12f, Color.white), floor = WingMat("29", 2.4f, 0.35f, new Color(0.85f, 0.85f, 0.85f));
            Material planks = WingMat("44", 2.5f, 0.3f, Color.white), ceiling = WingMat("15", 3f, 0.05f, new Color(0.9f, 0.9f, 0.9f));
            wingWood = WingMat("113", 1.5f, 0.3f, Color.white);
            wingPanel = WingMat("106", 2.5f, 0.25f, Color.white);
            float[] doors = { 37.5f, 85.5f };   // the tower doorways (classrooms 2B and 2D)

            // Floor slab, roof, and the lower ceilings over the corridor and classrooms.
            Gfx.Box("WingFloor", wing, new Vector3((WingX0 + WingX1) * 0.5f, y - Slab * 0.5f, (WingZ0 + WingZ1) * 0.5f), new Vector3(WingX1 - WingX0 + 0.3f, Slab, WingZ1 - WingZ0), floor).isStatic = true;
            Gfx.Box("WingRoof", wing, new Vector3((WingX0 + WingX1) * 0.5f, y + WingTall + 0.1f, (WingCorrS + WingZ1) * 0.5f), new Vector3(WingX1 - WingX0 + 0.3f, 0.2f, WingZ1 - WingCorrS + 0.3f), M.concrete).isStatic = true;
            Gfx.Box("CorridorCeiling", wing, new Vector3((WingX0 + WingX1) * 0.5f, y + WallH, (WingCorrS + WingCorrN) * 0.5f), new Vector3(WingX1 - WingX0, 0.06f, WingCorrN - WingCorrS), ceiling, false).isStatic = true;

            // Passages from the tower's doorways to the corridor.
            foreach (float dx in doors)
            {
                Gfx.Box("PassageCeiling", wing, new Vector3(dx, y + WallH, (WingZ0 + WingCorrS) * 0.5f), new Vector3(3f, 0.06f, WingCorrS - WingZ0), ceiling, false);
                for (int s = -1; s <= 1; s += 2)
                    Gfx.Box("PassageWall", wing, new Vector3(dx + s * 1.56f, y + WallH * 0.5f, (WingZ0 + WingCorrS) * 0.5f), new Vector3(0.12f, WallH, WingCorrS - WingZ0), wall).isStatic = true;
            }
            // Corridor: south wall with the passage openings, north wall with the room doors.
            HallWallAt(wing, new Vector3(WingX0, 0f, WingCorrS), new Vector3(WingX1, 0f, WingCorrS), y, WallH, wall, new Vector3(doors[0], 3f, WallH), new Vector3(doors[1], 3f, WallH));
            var rooms = new[]
            {
                new { name = "Gymnasium", x0 = WingX0, x1 = 30f, tall = true, doors = new[] { 18f } },
                new { name = "Classroom 2L", x0 = 30f, x1 = 42f, tall = false, doors = new[] { 36f } },
                new { name = "Classroom 2M", x0 = 42f, x1 = 54f, tall = false, doors = new[] { 48f } },
                new { name = "Science store", x0 = 54f, x1 = 60f, tall = false, doors = new[] { 57f } },
                new { name = "Classroom 2N", x0 = 60f, x1 = 72f, tall = false, doors = new[] { 66f } },
                new { name = "Auditorium", x0 = 72f, x1 = WingX1, tall = true, doors = new[] { 80f, 94f } },
            };
            var gaps = new List<Vector3>();
            foreach (var r in rooms) foreach (float d in r.doors) gaps.Add(new Vector3(d, DoorW, DoorH));
            HallWallAt(wing, new Vector3(WingX0, 0f, WingCorrN), new Vector3(WingX1, 0f, WingCorrN), y, WingTall, wall, gaps.ToArray());
            // Outer walls and the partitions.
            HallWallAt(wing, new Vector3(WingX0, 0f, WingZ1), new Vector3(WingX1, 0f, WingZ1), y, WingTall, wall);
            HallWallAt(wing, new Vector3(WingX0, 0f, WingCorrS), new Vector3(WingX0, 0f, WingZ1), y, WingTall, wall);
            HallWallAt(wing, new Vector3(WingX1, 0f, WingCorrS), new Vector3(WingX1, 0f, WingZ1), y, WingTall, wall);
            for (int i = 1; i < rooms.Length; i++)
                HallWallAt(wing, new Vector3(rooms[i].x0, 0f, WingCorrN), new Vector3(rooms[i].x0, 0f, WingZ1), y, WingTall, wall);
            // The tower's own north wall closes the short gap between the passages (z 72..75) - add the corridor-end walls there.
            HallWallAt(wing, new Vector3(WingX0, 0f, WingZ0), new Vector3(WingX0, 0f, WingCorrS), y, WallH, wall);
            HallWallAt(wing, new Vector3(WingX1, 0f, WingZ0), new Vector3(WingX1, 0f, WingCorrS), y, WallH, wall);

            AnnexAreas.Add(new AnnexArea { name = "North wing corridor", xz = new Rect(WingX0, WingCorrS, WingX1 - WingX0, WingCorrN - WingCorrS), floor = 1 });

            // Corridor: lockers on the north wall between doors, lamps, wall writing.
            var rnd = new System.Random(2222);
            for (float x = WingX0 + 2f; x < WingX1 - 2f; x += 3f)
            {
                bool nearDoor = false;
                foreach (var g in gaps) if (Mathf.Abs(g.x - x) < 1.6f) nearDoor = true;
                foreach (float dx in doors) if (Mathf.Abs(dx - x) < 2.2f) nearDoor = true;
                if (!nearDoor && rnd.NextDouble() < 0.7)
                {
                    HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(x - 0.53f, y, WingCorrN - 0.45f), Vector3.back, M);
                    HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(x + 0.53f, y, WingCorrN - 0.45f), Vector3.back, M);
                }
            }
            for (float x = WingX0 + 4f; x < WingX1; x += 9f)
            {
                Lamp(wing, 1, new Vector3(x, y, (WingCorrS + WingCorrN) * 0.5f), (int)x % 4 == 2 ? LampStyle.Emergency : LampStyle.Fluorescent, 3000 + (int)x);
                FillLight(wing, 1, new Vector3(x + 4.5f, y + 2.4f, (WingCorrS + WingCorrN) * 0.5f), 9f, 0.9f);
                WingSpots.Add(new Vector3(x + 1.5f, y, (WingCorrS + WingCorrN) * 0.5f));
            }
            AnnexHint(new Vector3(30f, y + 1.8f, WingCorrS + 0.08f), Vector3.back, "THE NEW WING\nDEAD END - NO STAIRS\nHIDE IN THE LOCKERS");

            // The rooms.
            foreach (var r in rooms)
            {
                float h = r.tall ? WingTall : WallH;
                float cx = (r.x0 + r.x1) * 0.5f, cz = (WingCorrN + WingZ1) * 0.5f, w = r.x1 - r.x0, d = WingZ1 - WingCorrN;
                if (!r.tall) Gfx.Box("Ceiling", wing, new Vector3(cx, y + WallH, cz), new Vector3(w, 0.06f, d), ceiling, false).isStatic = true;
                else Gfx.Box("Ceiling", wing, new Vector3(cx, y + WingTall - 0.02f, cz), new Vector3(w, 0.04f, d), ceiling, false).isStatic = true;
                // Sports hall and auditorium: wooden floors.
                if (r.tall) Gfx.Box("Boards", wing, new Vector3(cx, y + 0.008f, cz), new Vector3(w - 0.1f, 0.016f, d - 0.1f), planks, false).isStatic = true;
                // A soft fill so no corner of a room is pitch black (it's on the mains: a power cut kills it too).
                for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(w / 10f)); i++)
                    FillLight(wing, 1, new Vector3(r.x0 + (i + 0.5f) * w / Mathf.Max(1, Mathf.RoundToInt(w / 10f)), y + h * 0.6f, cz), h > 5f ? 15f : 10f, h > 5f ? 1.8f : 1.0f);
                AnnexAreas.Add(new AnnexArea { name = r.name, xz = new Rect(r.x0, WingCorrN, w, d), floor = 1, twoStorey = r.tall });
                // Lamps on a grid.
                int nx = Mathf.Max(1, Mathf.RoundToInt(w / 6f)), nz = Mathf.Max(1, Mathf.RoundToInt(d / 7f));
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        Vector3 f = new Vector3(r.x0 + (i + 0.5f) * w / nx, y, WingCorrN + (j + 0.5f) * d / nz);
                        Lamp(wing, 1, f, r.tall ? LampStyle.Industrial : LampStyle.Fluorescent, 3100 + (int)r.x0 * 10 + i * 3 + j, h);
                        WingSpots.Add(f + new Vector3(1.2f, 0f, 1.2f));
                    }
                switch (r.name)
                {
                    case "Gymnasium": Gym(props, r.x0, r.x1, y, rnd); break;
                    case "Auditorium": Auditorium(props, r.x0, r.x1, y, rnd); break;
                    case "Science store":
                        for (int k = 0; k < 4; k++)
                            Gfx.Box("Shelf", props, new Vector3(r.x0 + 0.5f + (k % 2) * (w - 1f), y + 1.1f, WingCorrN + 5f + (k / 2) * 8f), new Vector3(0.6f, 2.2f, 4f), M.metal);
                        HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(cx, y, WingZ1 - 0.45f), Vector3.back, M);
                        break;
                    default: Classroom(props, r.x0, r.x1, y, rnd); break;
                }
            }
            StaticMerge.Merge(props, "WingProps");
            StaticMerge.Merge(wing, "NorthWing");
        }

        static Material wingWood, wingPanel;

        /// <summary>A world-projected material from Resources/Textures/Wing/wing_{id}c (+ its normal map).</summary>
        static Material WingMat(string id, float metres, float smooth, Color tint)
        {
            var tex = Resources.Load<Texture2D>("Textures/Wing/wing_" + id + "c");
            var m = Gfx.LitWorld(tint, smooth, 0f, tex, metres);
            var n = Resources.Load<Texture2D>("Textures/Wing/wing_" + id + "_Normal");
            if (n != null) { m.SetTexture("_NormalMap", n); m.SetFloat("_NormalScale", 1f); Gfx.Validate(m); }
            return m;
        }

        static void Classroom(Transform props, float x0, float x1, float y, System.Random rnd)
        {
            float cx = (x0 + x1) * 0.5f;
            // Rows of pupils' desks facing the blackboard on the north wall, a few knocked askew.
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                {
                    var desk = Gfx.Box("Desk", props, new Vector3(cx - 3f + col * 3f, y + 0.38f, WingCorrN + 4.5f + row * 3f), new Vector3(1.2f, 0.76f, 0.6f), wingWood);
                    if (rnd.NextDouble() < 0.3) desk.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 50f - 25f, 0f);
                }
            Gfx.Box("Blackboard", props, new Vector3(cx, y + 1.6f, WingZ1 - 0.08f), new Vector3(4f, 1.3f, 0.05f), M.blackboard, false);
            HidingSpot.Create(HidingSpot.Kind.Desk, props, new Vector3(cx - 2.5f, y, WingZ1 - 2.6f), Vector3.back, M);
            HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(x1 - 0.5f, y, WingZ1 - 1.2f), Vector3.left, M);
        }

        static void Gym(Transform props, float x0, float x1, float y, System.Random rnd)
        {
            // Bleachers along the north wall, vaulting boxes and mats, lockers by the door.
            for (int k = 0; k < 4; k++)
                Gfx.Box("Bleacher", props, new Vector3((x0 + x1) * 0.5f, y + (k + 1) * 0.225f, WingZ1 - 1f - (3 - k) * 0.9f), new Vector3(x1 - x0 - 4f, (k + 1) * 0.45f, 0.9f), wingWood);
            Gfx.Box("VaultBox", props, new Vector3(x0 + 7f, y + 0.6f, WingCorrN + 7f), new Vector3(1.6f, 1.2f, 0.9f), wingWood);
            Gfx.Box("VaultBox", props, new Vector3(x0 + 15f, y + 0.6f, WingCorrN + 9f), new Vector3(1.6f, 1.2f, 0.9f), wingWood);
            Gfx.Box("Mat", props, new Vector3(x0 + 11f, y + 0.08f, WingCorrN + 6f), new Vector3(3f, 0.16f, 2f), M.fabricBlue, false);
            for (int i = 0; i < 4; i++)
                HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(x0 + 0.5f, y, WingCorrN + 2f + i * 1.06f), Vector3.right, M);
        }

        static void Auditorium(Transform props, float x0, float x1, float y, System.Random rnd)
        {
            float cx = (x0 + x1) * 0.5f;
            // A stage across the north end (steps at both sides), rows of seats in front, costume wardrobes backstage.
            Gfx.Box("Stage", props, new Vector3(cx, y + 0.5f, WingZ1 - 2.5f), new Vector3(x1 - x0 - 6f, 1f, 5f), wingPanel);
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                    Gfx.Box("Step", props, new Vector3(cx + s * ((x1 - x0) * 0.5f - 2.2f), y + 0.17f + k * 0.33f, WingZ1 - 6f + k * 0.5f), new Vector3(2f, 0.34f + k * 0.0f, 0.5f), wingPanel);
            Gfx.Box("Curtain", props, new Vector3(cx, y + 3.8f, WingZ1 - 0.3f), new Vector3(x1 - x0 - 6f, 5.6f, 0.1f), M.fabricRed, false);
            for (int row = 0; row < 5; row++)
            {
                float z = WingCorrN + 3f + row * 2.2f;
                Gfx.Box("SeatRow", props, new Vector3(cx - 6.5f, y + 0.45f, z), new Vector3(9f, 0.9f, 0.7f), M.fabricRed);
                Gfx.Box("SeatRow", props, new Vector3(cx + 6.5f, y + 0.45f, z), new Vector3(9f, 0.9f, 0.7f), M.fabricRed);
            }
            HidingSpot.Create(HidingSpot.Kind.Wardrobe, props, new Vector3(x0 + 0.5f, y, WingZ1 - 6.5f), Vector3.right, M);
            HidingSpot.Create(HidingSpot.Kind.Wardrobe, props, new Vector3(x1 - 0.5f, y, WingZ1 - 6.5f), Vector3.left, M);
            HidingSpot.Create(HidingSpot.Kind.Wardrobe, props, new Vector3(x1 - 0.5f, y, WingCorrN + 3f), Vector3.left, M);
        }
    }
}
