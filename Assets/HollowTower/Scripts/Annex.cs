using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace HollowTower
{
    /// <summary>A named footprint of the ground-floor annex (for the map and the hints).</summary>
    public class AnnexArea
    {
        public string name;
        public Rect xz;          // world x / z
        public bool twoStorey;
        public int floor;        // 0 = ground-floor annex, 1 = the floor 2 north wing
    }

    /// <summary>
    /// The ground floor's south annex: everything south (and west) of the tower's south wall, all on floor 1.
    ///   - the mansion (two full storeys round a grand staircase - the "sub-storey") behind the atrium,
    ///   - the chapel (St Bartholomew-the-Less scan) behind the waiting room,
    ///   - the shrine rotunda down a passage from the generator room,
    ///   - the hospital corridor from parking access down to the security hall,
    ///   - the security hall: double height with a steel mezzanine, the floor's generator in the north-west corner
    ///     and the escape lift (needs all 3 fuses) behind a shutter in the south-east corner, the smoking room off it.
    /// </summary>
    public static partial class LevelBuilder
    {
        public static readonly List<AnnexArea> AnnexAreas = new List<AnnexArea>();
        /// <summary>Floor points inside the annex rooms (both mansion storeys) for enemies and items.</summary>
        public static readonly List<Vector3> AnnexSpots = new List<Vector3>();
        public static Bounds SecurityHall { get; private set; }
        public static Vector3 SecurityHallCenter { get { return SecurityHall.center - Vector3.up * SecurityHall.extents.y; } }

        /// <summary>
        /// After the navmesh is baked: some of the mansion's upper rooms have no stairs left (they were reached from the
        /// wing that was cut away). Drop the spots you can't walk to, and move any enemy or item that landed on one.
        /// </summary>
        public static void PruneUnreachableAnnex(LevelInfo level)
        {
            UnityEngine.AI.NavMeshHit from;
            if (!UnityEngine.AI.NavMesh.SamplePosition(CellCenter(0, 17, 20), out from, 2f, UnityEngine.AI.NavMesh.AllAreas)) return;
            var path = new UnityEngine.AI.NavMeshPath();
            System.Func<Vector3, bool> reachable = p =>
            {
                UnityEngine.AI.NavMeshHit h;
                return UnityEngine.AI.NavMesh.SamplePosition(p, out h, 1.5f, UnityEngine.AI.NavMesh.AllAreas) && Mathf.Abs(h.position.y - p.y) < 0.8f
                       && UnityEngine.AI.NavMesh.CalculatePath(from.position, h.position, UnityEngine.AI.NavMesh.AllAreas, path)
                       && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
            };
            int before = AnnexSpots.Count;
            AnnexSpots.RemoveAll(p => !reachable(p));
            if (AnnexSpots.Count == 0) return;
            var rnd = new System.Random(99);
            int moved = 0;
            foreach (var sp in level.spawns)
            {
                Vector3 feet = sp.kind == EnemyKind.Ghost ? sp.position + Vector3.down * 1.2f : sp.position;
                if (!InAnnex(feet) || reachable(feet)) continue;
                sp.position = AnnexSpots[rnd.Next(AnnexSpots.Count)] + (sp.kind == EnemyKind.Ghost ? Vector3.up * 1.2f : Vector3.zero);
                moved++;
            }
            foreach (var pk in Pickup.All)
            {
                if (pk == null || !InAnnex(pk.transform.position)) continue;
                Vector3 feet = pk.transform.position + Vector3.down * 0.5f;
                if (reachable(feet)) continue;
                pk.transform.position = AnnexSpots[rnd.Next(AnnexSpots.Count)] + Vector3.up * 0.5f;
                moved++;
            }
            Debug.Log("Hollow Tower annex: " + AnnexSpots.Count + " of " + before + " spots reachable; moved " + moved + " enemies / items");
        }

        /// <summary>The annex building (or hall) a point is in, for the map caption.</summary>
        public static string AnnexAreaAt(Vector3 p)
        {
            string best = "Annex";
            float area = float.MaxValue;
            foreach (var a in AnnexAreas)
                if (a.xz.Contains(new Vector2(p.x, p.z)) && a.xz.width * a.xz.height < area) { best = a.name; area = a.xz.width * a.xz.height; }
            return best;
        }

        /// <summary>South or west of the tower's footprint: the annex (always floor 1).</summary>
        public static bool InAnnex(Vector3 p) { return (p.z < -0.05f || p.x < -0.05f) && p.y < 10f; }

        /// <summary>Which floor a point is on, counting the two-storey annex (and its mezzanines) as floor 1.</summary>
        public static int FloorAt(Vector3 p) { return InAnnex(p) ? 0 : FloorOf(p.y); }

        // Doorways in the tower's south wall (see LevelLayout.Ground).
        const float ShrineDoorX = 7.5f, MansionDoorX = 37.5f, ChapelDoorX = 82.5f, HospitalDoorX = 103.5f;
        // Security hall footprint and height.
        const float HallX0 = 84f, HallX1 = 108f, HallZ1 = -32.2f, HallZ0 = -52.2f, HallH = 7.5f, Mezz = 3.6f;

        static readonly Dictionary<Material, Material> annexMats = new Dictionary<Material, Material>();

        static void BuildAnnex(Transform root)
        {
            AnnexAreas.Clear();
            AnnexSpots.Clear();
            annexMats.Clear();
            var annex = new GameObject("Annex (floor 1)").transform;
            annex.SetParent(root, false);

            // Mansion: front door 3 m south of the atrium, through a short vestibule.
            Passage(annex, "Mansion vestibule", new Vector3(MansionDoorX, 0f, 0f), Vector3.back, 3f, 3.2f, 3.3f, 2.3f, 2.5f, M.lobbyWall);
            var mansion = PlacePiece(annex, "Mansion", "Mansion (The Mansion Interiors)", new Vector3(MansionDoorX, 0f, -3f), Vector3.back, 1f);
            if (mansion != null) Dress(mansion, "Mansion", true, 5f, 18, LampStyle.Warm);

            // Chapel: its door is cut through a window bay that juts 0.66 m out of the nave wall.
            Passage(annex, "Chapel porch", new Vector3(ChapelDoorX, 0f, 0f), Vector3.back, 3f, 3.2f, 3.3f, 2.6f, 2.5f, M.lobbyWall);
            var chapel = PlacePiece(annex, "Chapel", "Chapel (St Bartholomew-the-Less, artfletch, CC-BY 4.0)", new Vector3(ChapelDoorX, 0f, -3.66f), Vector3.back, 1f);
            if (chapel != null) UnderFloor(annex, chapel);
            if (chapel != null) Dress(chapel, "Chapel", false, 3.5f, 3, LampStyle.Warm);

            // Hospital corridor down to the security hall.
            Passage(annex, "Hospital doorway", new Vector3(HospitalDoorX, 0f, 0f), Vector3.back, 1f, 3.6f, 3.2f, 3.3f, 2.7f, M.concreteWall);
            var hospital = PlacePiece(annex, "Hospital", "Hospital corridor (ETBENO, CC-BY 4.0)", new Vector3(HospitalDoorX, 0f, -1f), Vector3.back, 1f);
            if (hospital != null) UnderFloor(annex, hospital);
            if (hospital != null) Dress(hospital, "Hospital corridor", false, 4f, 3, LampStyle.Fluorescent);

            BuildSecurityHall(annex);

            // Smoking room off the hall's east wall.
            var smoking = PlacePiece(annex, "SmokingRoom", "Smoking room (The Hallwyl Museum, CC-BY 4.0)", new Vector3(HallX1 + 0.1f, 0f, -42f), Vector3.right, 1f);
            if (smoking != null) UnderFloor(annex, smoking);
            if (smoking != null) Dress(smoking, "Smoking room", false, 2.5f, 1, LampStyle.Warm);

            // Shrine: down a passage from the generator room, then west.
            Passage(annex, "Shrine passage", new Vector3(ShrineDoorX, 0f, 0f), Vector3.back, 19.5f, 3f, 3.2f, 0f, 0f, M.concreteWall, -16.5f);
            Passage(annex, "Shrine stub", new Vector3(6.05f, 0f, -16.5f), Vector3.left, 1.45f, 3f, 3.2f, 2.4f, 2.3f, M.concreteWall);
            var shrine = PlacePiece(annex, "Shrine", "Shrine (MinZiin, Sketchfab Standard)", new Vector3(4.6f, 0f, -16.5f), Vector3.left, 2f);
            if (shrine != null) UnderFloor(annex, shrine);
            if (shrine != null) Dress(shrine, "Shrine", false, 6f, 3, LampStyle.Dim);

            // A slab under the whole annex: nothing can fall into the void between the buildings.
            Gfx.Box("AnnexGround", annex, new Vector3(35f, -0.35f, -30f), new Vector3(160f, 0.5f, 62f), M.concrete).isStatic = true;
        }

        // ---------------- pieces ----------------

        /// <summary>
        /// Loads Resources/Models/Annex/<paramref name="res"/> (prepared in Blender with "Door" / "DoorIn" markers) and turns it
        /// so DoorIn - Door points along <paramref name="inward"/>, with Door on <paramref name="doorPos"/>.
        /// Colliders come from its "COL" mesh (decimated scans) or from its own meshes.
        /// </summary>
        static Transform PlacePiece(Transform parent, string res, string label, Vector3 doorPos, Vector3 inward, float scale)
        {
            var prefab = Resources.Load<GameObject>("Models/Annex/" + res);
            if (prefab == null) { Debug.LogWarning("Hollow Tower: annex model missing: " + res); return null; }
            var holder = new GameObject(label).transform;
            holder.SetParent(parent, false);
            holder.localScale = Vector3.one * scale;
            var inst = Object.Instantiate(prefab, holder, false);
            inst.name = res;
            Transform door = null, doorIn = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Door") door = t;
                else if (t.name == "DoorIn") doorIn = t;
            }
            if (door != null && doorIn != null)
            {
                Vector3 doorAt = door.position;                 // measured with the holder at the origin, unturned
                Vector3 d = doorIn.position - doorAt; d.y = 0f;
                float yaw = Vector3.SignedAngle(d, inward, Vector3.up);
                holder.rotation = Quaternion.Euler(0f, yaw, 0f);
                holder.position = doorPos - holder.rotation * doorAt;
            }
            else
            {
                Debug.LogWarning("Hollow Tower: " + res + " has no Door markers");
                holder.position = doorPos;
            }

            Transform col = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) if (t.name == "COL") col = t;
            int meshCols = 0, boxCols = 0;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (col != null && r.transform == col) continue;
                r.sharedMaterials = AnnexMaterials(r.sharedMaterials);
                r.shadowCastingMode = ShadowCastingMode.On;
                if (col != null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) { r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; meshCols++; }
                else if (r.bounds.size.y > 0.2f) { r.gameObject.AddComponent<BoxCollider>(); boxCols++; }
            }
            if (col != null)
            {
                var cr = col.GetComponent<Renderer>();
                if (cr != null) Object.Destroy(cr);
                var mf = col.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) { col.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; meshCols++; }
            }
            if (boxCols > 0) Debug.LogWarning("Hollow Tower: " + res + " meshes not readable, " + boxCols + " box colliders");
            holder.gameObject.AddComponent<AnnexPiece>();
            return holder;
        }

        static Material[] AnnexMaterials(Material[] src)
        {
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var s = src[i];
                if (s == null) { dst[i] = M.concrete; continue; }
                Material m;
                if (!annexMats.TryGetValue(s, out m))
                {
                    m = Gfx.FromGltf(s);
                    // The scans have their light baked into the colour: keep them out of the ray-traced GI's way by
                    // making them rough and non-metallic, so the torch and lamps read on them properly.
                    if (m != null && m != s)
                    {
                        m.SetFloat("_Metallic", 0f);
                        m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.3f));
                        // Decimated scans have the odd flipped triangle: draw both sides so they don't show as holes.
                        m.SetFloat("_DoubleSidedEnable", 1f);
                        UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
                    }
                    annexMats[s] = m;
                }
                dst[i] = m;
            }
            return dst;
        }

        // ---------------- procedural passages and the security hall ----------------

        /// <summary>
        /// An enclosed passage from <paramref name="start"/> (on the floor, in the tower's south wall) along <paramref name="dir"/>
        /// for <paramref name="length"/> m, with an end wall holding a doorway (<paramref name="openW"/> x <paramref name="openH"/>;
        /// 0 = no end wall). <paramref name="sideOpenAt"/> cuts a 3 m opening in the west side wall at that z.
        /// </summary>
        static void Passage(Transform parent, string name, Vector3 start, Vector3 dir, float length, float width, float height,
                            float openW, float openH, Material wall, float sideOpenAt = float.NaN)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = start + dir * (length * 0.5f);
            bool alongZ = Mathf.Abs(dir.z) > 0.5f;
            Vector3 Size(float along, float y, float across) { return alongZ ? new Vector3(across, y, along) : new Vector3(along, y, across); }

            Gfx.Box("Floor", root, mid + Vector3.down * Slab * 0.5f, Size(length, Slab, width + 0.4f), M.concrete).isStatic = true;
            Gfx.Box("Ceiling", root, mid + Vector3.up * (height + 0.1f), Size(length, 0.2f, width + 0.4f), M.ceilingPlaster).isStatic = true;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 wc = mid + side * (s * (width * 0.5f + 0.06f)) + Vector3.up * height * 0.5f;
                // The west wall (side with -x) of a north-south passage can hold a side opening.
                bool west = (side * s).x < -0.5f;
                if (!float.IsNaN(sideOpenAt) && west && alongZ)
                {
                    float z0 = start.z, z1 = start.z + dir.z * length;
                    float zt = Mathf.Max(z0, z1), zb = Mathf.Min(z0, z1);
                    float g0 = sideOpenAt + 1.5f, g1 = sideOpenAt - 1.5f;    // the gap
                    float x = wc.x;
                    if (zt - g0 > 0.05f) Gfx.Box("Wall", root, new Vector3(x, height * 0.5f, (zt + g0) * 0.5f), new Vector3(0.12f, height, zt - g0), wall).isStatic = true;
                    if (g1 - zb > 0.05f) Gfx.Box("Wall", root, new Vector3(x, height * 0.5f, (g1 + zb) * 0.5f), new Vector3(0.12f, height, g1 - zb), wall).isStatic = true;
                    continue;
                }
                Gfx.Box("Wall", root, wc, Size(length, height, 0.12f), wall).isStatic = true;
            }
            if (openW > 0f) EndWall(root, start + dir * length, dir, width + 0.24f, height, openW, openH, wall);
            else if (float.IsNaN(sideOpenAt)) { }
            else Gfx.Box("EndWall", root, start + dir * (length + 0.06f) + Vector3.up * height * 0.5f, Size(0.12f, height, width + 0.24f), wall).isStatic = true;

            // A dim lamp every 6 m.
            int lamps = Mathf.Max(1, Mathf.RoundToInt(length / 6f));
            for (int i = 0; i < lamps; i++)
                Lamp(root, 0, start + dir * ((i + 0.5f) * length / lamps), LampStyle.Dim, 900 + Mathf.RoundToInt(start.x * 7f + i), height);
            StaticMerge.Merge(root, name);
        }

        /// <summary>A wall across <paramref name="dir"/> at <paramref name="at"/> with a centred doorway.</summary>
        static void EndWall(Transform root, Vector3 at, Vector3 dir, float width, float height, float openW, float openH, Material wall)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            bool alongZ = Mathf.Abs(dir.z) > 0.5f;
            float jamb = (width - openW) * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = at + side * (s * (openW * 0.5f + jamb * 0.5f)) + Vector3.up * height * 0.5f;
                Gfx.Box("Jamb", root, c, alongZ ? new Vector3(jamb, height, 0.12f) : new Vector3(0.12f, height, jamb), wall).isStatic = true;
            }
            if (height - openH > 0.05f)
                Gfx.Box("Lintel", root, at + Vector3.up * (openH + (height - openH) * 0.5f), alongZ ? new Vector3(openW, height - openH, 0.12f) : new Vector3(0.12f, height - openH, openW), wall).isStatic = true;
        }

        static void HallWall(Transform root, Vector3 a, Vector3 b, float height, Material m, params Vector3[] gaps)
        {
            HallWallAt(root, a, b, 0f, height, m, gaps);
        }

        /// <summary>As HallWall, standing on a floor at <paramref name="baseY"/>.</summary>
        static void HallWallAt(Transform root, Vector3 a, Vector3 b, float baseY, float height, Material m, params Vector3[] gaps)
        {
            // Straight wall from a to b (axis-aligned) with doorway gaps given as (centre along the wall, width, height).
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float s0 = alongX ? Mathf.Min(a.x, b.x) : Mathf.Min(a.z, b.z), s1 = alongX ? Mathf.Max(a.x, b.x) : Mathf.Max(a.z, b.z);
            float fixedC = alongX ? a.z : a.x;
            var cuts = new List<Vector3>(gaps);
            cuts.Sort((p, q) => p.x.CompareTo(q.x));
            float cur = s0;
            System.Action<float, float, float, float> piece = (u0, u1, y0, y1) =>
            {
                if (u1 - u0 < 0.02f || y1 - y0 < 0.02f) return;
                float um = (u0 + u1) * 0.5f, ym = baseY + (y0 + y1) * 0.5f;
                Vector3 pos = alongX ? new Vector3(um, ym, fixedC) : new Vector3(fixedC, ym, um);
                Vector3 size = alongX ? new Vector3(u1 - u0, y1 - y0, 0.24f) : new Vector3(0.24f, y1 - y0, u1 - u0);
                Gfx.Box("Wall", root, pos, size, m).isStatic = true;
            };
            foreach (var g in cuts)
            {
                float g0 = g.x - g.y * 0.5f, g1 = g.x + g.y * 0.5f;
                piece(cur, g0, 0f, height);
                piece(g0, g1, g.z, height);   // lintel
                cur = g1;
            }
            piece(cur, s1, 0f, height);
        }

        static void BuildSecurityHall(Transform parent)
        {
            var root = new GameObject("Security hall").transform;
            root.SetParent(parent, false);
            var props = new GameObject("Hall props").transform;
            props.SetParent(root, false);
            float cx = (HallX0 + HallX1) * 0.5f, cz = (HallZ0 + HallZ1) * 0.5f, w = HallX1 - HallX0, d = HallZ1 - HallZ0;
            SecurityHall = new Bounds(new Vector3(cx, HallH * 0.5f, cz), new Vector3(w, HallH, d));
            AnnexAreas.Add(new AnnexArea { name = "Security hall", xz = new Rect(HallX0, HallZ0, w, d), twoStorey = true });

            Material wall = M.concreteWall, floor = M.serverFloor;
            Gfx.Box("Floor", root, new Vector3(cx, -Slab * 0.5f, cz), new Vector3(w + 0.5f, Slab, d + 0.5f), floor).isStatic = true;
            Gfx.Box("Ceiling", root, new Vector3(cx, HallH + 0.1f, cz), new Vector3(w + 0.5f, 0.2f, d + 0.5f), M.concrete).isStatic = true;
            HallWall(root, new Vector3(HallX0, 0f, HallZ1), new Vector3(HallX1, 0f, HallZ1), HallH, wall, new Vector3(HospitalDoorX, 3.3f, 2.7f));   // north: hospital
            HallWall(root, new Vector3(HallX0, 0f, HallZ0), new Vector3(HallX1, 0f, HallZ0), HallH, wall, new Vector3(104.5f, 3.0f, LevelBuilder.WallH)); // south: lift shutter
            HallWall(root, new Vector3(HallX0, 0f, HallZ0), new Vector3(HallX0, 0f, HallZ1), HallH, wall);                                                 // west
            HallWall(root, new Vector3(HallX1, 0f, HallZ0), new Vector3(HallX1, 0f, HallZ1), HallH, wall, new Vector3(-42f, 1.5f, 2.3f));                // east: smoking room
            // Yellow-black safety stripe round the hall at knee height.
            Gfx.Box("Stripe", root, new Vector3(cx, 0.5f, HallZ1 - 0.13f), new Vector3(w, 0.12f, 0.02f), M.trim, false);

            // ---- the mezzanine: a steel gallery along the west and south walls, a ramp up along the north wall ----
            Material steel = M.steel, grate = M.metal;
            float gx1 = HallX0 + 4f, gz1 = HallZ0 + 4f;
            Gfx.Box("GalleryWest", root, new Vector3((HallX0 + gx1) * 0.5f, Mezz - 0.12f, cz - 0.5f), new Vector3(gx1 - HallX0, 0.24f, d - 1f), grate).isStatic = true;
            Gfx.Box("GallerySouth", root, new Vector3((gx1 + 100f) * 0.5f, Mezz - 0.12f, (HallZ0 + gz1) * 0.5f), new Vector3(100f - gx1, 0.24f, gz1 - HallZ0), grate).isStatic = true;
            // Ramp from the floor at x 100 up to the gallery at x 88, against the north wall.
            float rz = HallZ1 - 1.45f, rx0 = 100f, rx1 = gx1, run = rx0 - rx1;
            float ang = Mathf.Atan2(Mezz, run) * Mathf.Rad2Deg, len = Mathf.Sqrt(run * run + Mezz * Mezz) + 0.3f;
            var ramp = Gfx.Box("MezzRamp", root, new Vector3((rx0 + rx1) * 0.5f, Mezz * 0.5f - 0.12f, rz), new Vector3(len, 0.24f, 2.6f), grate);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -ang);
            ramp.isStatic = true;
            // Posts under the gallery and railings on its open edges.
            for (float z = HallZ0 + 5f; z < HallZ1 - 3f; z += 4f) Gfx.Box("Post", root, new Vector3(gx1 - 0.15f, Mezz * 0.5f, z), new Vector3(0.2f, Mezz, 0.2f), steel).isStatic = true;
            for (float x = gx1 + 3f; x < 100f; x += 4f) Gfx.Box("Post", root, new Vector3(x, Mezz * 0.5f, gz1 - 0.15f), new Vector3(0.2f, Mezz, 0.2f), steel).isStatic = true;
            Rail(root, new Vector3(gx1 - 0.05f, Mezz, HallZ1 - 2.8f), new Vector3(gx1 - 0.05f, Mezz, gz1));
            Rail(root, new Vector3(gx1, Mezz, gz1 - 0.05f), new Vector3(100f, Mezz, gz1 - 0.05f));
            Rail(root, new Vector3(100f, Mezz, gz1), new Vector3(100f, Mezz, HallZ0));
            var rr = Rail(root, new Vector3(rx0, 0f, rz - 1.35f), new Vector3(rx1, Mezz, rz - 1.35f));
            AnnexSpots.Add(new Vector3(HallX0 + 2f, Mezz, cz));
            AnnexSpots.Add(new Vector3(94f, Mezz, HallZ0 + 2f));

            // The floor's generator under the gallery in the north-west corner...
            Generator.Create(root, new Vector3(HallX0 + 1.4f, 0f, HallZ1 - 4.2f), Vector3.right, 0);
            // ...and the escape lift behind a steel shutter in the opposite (south-east) corner.
            var shutter = Gfx.Box("LiftShutter", root.parent, new Vector3(104.5f, WallH * 0.5f, HallZ0), new Vector3(3.0f, WallH, 0.3f), M.rust);
            for (int i = 0; i < 9; i++)
                Gfx.Box("Slat", shutter.transform, new Vector3(0f, -0.45f + i * 0.11f, -0.6f), new Vector3(1f, 0.02f, 0.4f), M.steel, false);
            Vector3 car = new Vector3(104.5f, 0f, HallZ0 - Cell * 0.5f - 0.15f);
            Gfx.Box("CarSlab", root, car + Vector3.down * Slab * 0.5f, new Vector3(Cell + 0.4f, Slab, Cell + 0.4f), M.concrete).isStatic = true;
            Gfx.Box("CarRoof", root, car + Vector3.up * (WallH + 0.1f), new Vector3(Cell + 0.4f, 0.2f, Cell + 0.4f), M.concrete).isStatic = true;
            Info.elevator = SecretElevator.Create(parent, shutter, car, M.elevator, M.steel);
            // Hazard paint on the floor in front of the shutter and a sign above it.
            Gfx.Box("HazardFloor", root, new Vector3(104.5f, 0.01f, HallZ0 + 1.2f), new Vector3(3.2f, 0.02f, 1.6f), M.trim, false);
            AnnexHint(new Vector3(104.5f, 4.6f, HallZ0 + 0.14f), Vector3.back, "SERVICE LIFT\nNEEDS ALL 3 FUSES\nTHEN RUN");
            AnnexHint(new Vector3(HallX0 + 0.14f, 2.0f, HallZ1 - 6.5f), Vector3.left, "GENERATOR\nUNDER THE GALLERY");

            // A security desk with dead monitors in the middle, crates for cover, lockers along the walls.
            Gfx.Box("Desk", props, new Vector3(cx + 1f, 0.5f, cz + 1f), new Vector3(4f, 1f, 1.2f), M.darkWood);
            for (int i = 0; i < 4; i++)
                Gfx.Box("Monitor", props, new Vector3(cx - 0.5f + i, 1.25f, cz + 1.2f), new Vector3(0.7f, 0.5f, 0.08f), M.screen, false);
            var rnd = new System.Random(808);
            Vector3[] crates = { new Vector3(93f, 0f, -39f), new Vector3(97f, 0f, -45f), new Vector3(91f, 0f, -47f), new Vector3(102f, 0f, -40f), new Vector3(89f, 0f, -43f) };
            foreach (var c in crates)
            {
                float s = 1.1f + (float)rnd.NextDouble() * 0.4f;
                var b = Gfx.Box("Crate", props, c + Vector3.up * s * 0.5f, Vector3.one * s, M.cardboard);
                b.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 40f - 20f, 0f);
            }
            HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(HallX1 - 0.5f, 0f, -36f), Vector3.left, M);
            HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(HallX1 - 0.5f, 0f, -37.1f), Vector3.left, M);
            HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(HallX1 - 0.5f, 0f, -47f), Vector3.left, M);
            HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(HallX0 + 0.5f, Mezz, -44f), Vector3.right, M);
            HidingSpot.Create(HidingSpot.Kind.Locker, props, new Vector3(93f, Mezz, HallZ0 + 0.5f), Vector3.forward, M);
            HidingSpot.Create(HidingSpot.Kind.Desk, props, new Vector3(cx - 4f, 0f, cz - 4f), Vector3.forward, M);
            StaticMerge.Merge(props, "HallProps");

            // Industrial lamps hanging from the high ceiling, and two under the gallery.
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                    Lamp(root, 0, new Vector3(HallX0 + 6f + i * 7f, 0f, HallZ0 + 6f + j * 8f), LampStyle.Industrial, 1200 + i * 3 + j, HallH);
            Lamp(root, 0, new Vector3(HallX0 + 2f, 0f, HallZ1 - 8f), LampStyle.Emergency, 1300, Mezz - 0.25f);
            Lamp(root, 0, new Vector3(HallX0 + 2f, Mezz, HallZ0 + 10f), LampStyle.Dim, 1301, HallH - Mezz);

            for (int i = 0; i < 6; i++) AnnexSpots.Add(new Vector3(88f + i * 3.4f, 0f, -38f - (i % 3) * 4f));
            for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++) FillLight(root, 0, new Vector3(HallX0 + 5f + i * 8f, 3f, HallZ0 + 5f + j * 10f), 14f, 1.4f);
            FillLight(root, 0, new Vector3(HallX0 + 2f, Mezz + 2f, (HallZ0 + HallZ1) * 0.5f), 10f, 0.9f);
            StaticMerge.Merge(root, "SecurityHall");
        }

        static GameObject Rail(Transform root, Vector3 a, Vector3 b)
        {
            Vector3 mid = (a + b) * 0.5f + Vector3.up * 1.0f;
            Vector3 dir = b - a;
            var g = Gfx.Box("Rail", root, mid, new Vector3(0.06f, 0.06f, dir.magnitude), M.steel);
            g.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            // Mid rail (keeps you from slipping under) - its collider also stops you walking off the edge.
            var low = Gfx.Box("RailLow", root, (a + b) * 0.5f + Vector3.up * 0.5f, new Vector3(0.05f, 1.0f, dir.magnitude), M.steel);
            low.transform.rotation = g.transform.rotation;
            low.GetComponent<Renderer>().enabled = false;
            Gfx.Box("RailMid", root, (a + b) * 0.5f + Vector3.up * 0.55f, new Vector3(0.04f, 0.04f, dir.magnitude), M.steel, false).transform.rotation = g.transform.rotation;
            return g;
        }

        // ---------------- dressing the imported pieces ----------------

        /// <summary>
        /// Finds floor spots inside a placed piece (both storeys of the mansion), hangs lamps from its ceilings, scatters
        /// wardrobes against its walls and records spots for enemies and items.
        /// </summary>
        static void Dress(Transform piece, string name, bool twoStorey, float spacing, int wardrobes, LampStyle style)
        {
            Physics.SyncTransforms();
            Bounds b = new Bounds(piece.position, Vector3.zero);
            bool first = true;
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            AnnexAreas.Add(new AnnexArea { name = name, xz = new Rect(b.min.x, b.min.z, b.size.x, b.size.z), twoStorey = twoStorey });

            var dress = new GameObject(name + " dressing").transform;
            dress.SetParent(piece.parent, false);
            var spots = new List<Vector3>();
            int lamps = 0;
            int seed = name.GetHashCode() & 0xffff;
            for (float x = b.min.x + spacing * 0.5f; x < b.max.x; x += spacing)
                for (float z = b.min.z + spacing * 0.5f; z < b.max.z; z += spacing)
                {
                    if (!InAnnex(new Vector3(x, 0f, z))) continue;
                    Vector3 f; float headroom;
                    float origin = Mathf.Max(b.min.y, 0f) + 2.0f;   // under any ceiling (the lowest are about 2.4 m); floors are at y 0
                    bool ground = FloorSpot(new Vector3(x, origin, z), out f, out headroom);
                    if (ground) AddSpot(dress, spots, f, headroom, style, seed, ref lamps);
                    if (!twoStorey) continue;
                    // The storey above: just over the ground floor's ceiling (or from 6 m where the ground probe found nothing).
                    float up = ground && headroom > 3f && headroom < 6.5f ? f.y + headroom + 1.4f : 6.2f;
                    if (ground && headroom >= 6.5f) continue;   // double-height space (the foyer): no storey above here
                    Vector3 f2; float h2;
                    if (FloorSpot(new Vector3(x, up, z), out f2, out h2) && f2.y > 3f) AddSpot(dress, spots, f2, h2, style, seed, ref lamps);
                }
            AnnexSpots.AddRange(spots);

            // Wardrobes against walls near some of the spots.
            var rnd = new System.Random(seed);
            int placed = 0;
            for (int tries = 0; tries < spots.Count * 2 && placed < wardrobes && spots.Count > 0; tries++)
            {
                Vector3 f = spots[rnd.Next(spots.Count)];
                Vector3 dir = Quaternion.Euler(0f, rnd.Next(4) * 90f, 0f) * Vector3.forward;
                RaycastHit wh;
                if (!Physics.Raycast(f + Vector3.up * 1.2f, dir, out wh, 5f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(wh.normal.y) > 0.3f) continue;
                Vector3 n = new Vector3(wh.normal.x, 0f, wh.normal.z).normalized;
                Vector3 pos = new Vector3(wh.point.x, f.y, wh.point.z) + n * 0.42f;
                if (Physics.CheckBox(pos + Vector3.up * 1.15f, new Vector3(0.6f, 1.05f, 0.4f), Quaternion.LookRotation(n), Gfx.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, 0.6f, Gfx.WorldMask, QueryTriggerInteraction.Ignore) == false) continue;
                HidingSpot.Create(HidingSpot.Kind.Wardrobe, dress, pos, n, M);
                Physics.SyncTransforms();
                placed++;
            }
            Debug.Log(string.Format("Hollow Tower annex: {0} - {1} spots, {2} lamps, {3} wardrobes, bounds {4}", name, spots.Count, lamps, placed, b));
        }

        /// <summary>Red wall writing; <paramref name="towardWall"/> points from the room into the wall it's on.</summary>
        /// <summary>Is there a floor under <paramref name="from"/> inside a room (ceiling above, walls round about)?</summary>
        static bool FloorSpot(Vector3 from, out Vector3 f, out float headroom)
        {
            f = Vector3.zero; headroom = 0f;
            RaycastHit fh;
            if (!Physics.Raycast(from, Vector3.down, out fh, from.y + 0.5f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            if (fh.normal.y < 0.85f || fh.collider.name == "AnnexGround") return false;
            f = fh.point;
            RaycastHit ch;
            if (!Physics.Raycast(f + Vector3.up * 0.3f, Vector3.up, out ch, 14f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            headroom = ch.point.y - f.y;
            if (headroom < 2.3f) return false;
            int walls = 0;   // walls on at least three sides (a corridor can be open at its ends)
            for (int k = 0; k < 4; k++)
                if (Physics.Raycast(f + Vector3.up * 1.2f, Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward, 30f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) walls++;
            if (walls < 3) return false;
            return !Physics.CheckCapsule(f + Vector3.up * 0.5f, f + Vector3.up * 1.6f, 0.4f, Gfx.WorldMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// A solid slab just under a scanned room's floor: scans have holes, and you'd drop through them onto the bare
        /// ground outside. Its top sits 2 cm below the scan's floor, so it never shows unless there's a hole.
        /// </summary>
        static void UnderFloor(Transform parent, Transform piece)
        {
            Physics.SyncTransforms();
            Bounds b = new Bounds(piece.position, Vector3.zero);
            bool first = true;
            foreach (var r in piece.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            var slab = Gfx.Box("PieceFloor", parent, new Vector3(b.center.x, -0.05f - 0.15f, b.center.z), new Vector3(b.size.x - 0.6f, 0.3f, b.size.z - 0.6f), M.woodFloor);
            slab.isStatic = true;
        }

        /// <summary>A soft unshadowed light that keeps a room from going pitch black (on the mains: a power cut kills it).</summary>
        static void FillLight(Transform parent, int floor, Vector3 pos, float range, float intensity)
        {
            var l = Gfx.PointLight(parent, pos, new Color(1f, 0.86f, 0.7f), intensity, range, LightShadows.None);
            var hd = l.GetComponent<HDAdditionalLightData>();
            if (hd != null) hd.volumetricDimmer = 0f;
            Info.lights.Add(new TowerLight { light = l, hd = hd, floor = floor, castsShadows = false, electric = true });
        }

        static bool AddSpot(Transform dress, List<Vector3> spots, Vector3 f, float headroom, LampStyle style, int seed, ref int lamps)
        {
            spots.Add(f);
            FillLight(dress, 0, f + Vector3.up * Mathf.Min(headroom * 0.6f, 2.6f), Mathf.Clamp(headroom * 2.5f, 8f, 14f), 1.1f);
            if (lamps >= 60) return true;
            Lamp(dress, 0, f, style, seed + lamps * 17, Mathf.Min(headroom, 9f) - 0.02f);
            lamps++;
            return true;
        }

        static void AnnexHint(Vector3 pos, Vector3 towardWall, string text)
        {
            var g = new GameObject("WallHint");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(towardWall, Vector3.up);
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
            tm.characterSize = 0.03f;
            tm.fontSize = 64;
            tm.color = new Color(0.75f, 0.03f, 0.02f);
            g.AddComponent<HintGlow>();
        }

        // ---------------- trap pits ----------------

        /// <summary>
        /// Pits: a 2 x 2 m hole in the middle of a corridor cell with a 0.5 m ledge round it (too narrow for the dead,
        /// just wide enough to edge along). On the ground floor it's a spike pit; upstairs the floor gives way onto the floor below.
        /// Hazard tape and a red glow from inside make them easy to spot.
        /// </summary>
        static void BuildPits(Transform root)
        {
            var pr = new GameObject("Trap pits").transform;
            pr.SetParent(root, false);
            for (int f = 0; f < 2; f++)
                foreach (var cell in Plan(f).pits) BuildPit(pr, f, cell.y, cell.x);
        }

        static void BuildPit(Transform parent, int f, int row, int col)
        {
            Vector3 c = CellCenter(f, row, col);
            const float hole = 2f, ledge = (Cell - hole) * 0.5f;
            var root = new GameObject("Trap pit F" + (f + 1)).transform;
            root.SetParent(parent, false);
            var p = Plan(f);
            Material m = FloorMat(p, row, col);
            // Ledge strips round the hole.
            Gfx.Box("Ledge", root, c + new Vector3(0f, -Slab * 0.5f, (hole + ledge) * 0.5f), new Vector3(Cell, Slab, ledge), m).isStatic = true;
            Gfx.Box("Ledge", root, c + new Vector3(0f, -Slab * 0.5f, -(hole + ledge) * 0.5f), new Vector3(Cell, Slab, ledge), m).isStatic = true;
            Gfx.Box("Ledge", root, c + new Vector3((hole + ledge) * 0.5f, -Slab * 0.5f, 0f), new Vector3(ledge, Slab, hole), m).isStatic = true;
            Gfx.Box("Ledge", root, c + new Vector3(-(hole + ledge) * 0.5f, -Slab * 0.5f, 0f), new Vector3(ledge, Slab, hole), m).isStatic = true;
            // Hazard tape on the rim (no colliders: nothing to trip on).
            var tape = new Material[] { M.trim, M.plasticBlack };
            for (int s = 0; s < 4; s++)
            {
                Quaternion q = Quaternion.Euler(0f, s * 90f, 0f);
                for (int k = 0; k < 6; k++)
                {
                    Vector3 along = q * Vector3.right, out_ = q * Vector3.forward;
                    var t = Gfx.Box("Tape", root, c + out_ * (hole * 0.5f + 0.07f) + along * (-1.08f + k * 0.43f) + Vector3.up * 0.006f,
                        Vector3.one, tape[k % 2], false);
                    t.transform.rotation = q;
                    t.transform.localScale = new Vector3(0.43f, 0.012f, 0.12f);
                    t.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            // Broken boards hanging into the hole.
            for (int k = 0; k < 3; k++)
            {
                var plank = Gfx.Box("Splinter", root, c + new Vector3(-0.6f + k * 0.6f, -0.25f, 0.85f), new Vector3(0.18f, 0.04f, 0.7f), M.wood, false);
                plank.transform.rotation = Quaternion.Euler(35f + k * 8f, k * 12f, 0f);
            }
            float depth = f == 0 ? 2.4f : Story;
            if (f == 0)
            {
                // The pit itself, with spikes at the bottom.
                Vector3 b = c + Vector3.down * depth;
                Gfx.Box("PitFloor", root, b + Vector3.down * 0.15f, new Vector3(hole + 0.3f, 0.3f, hole + 0.3f), M.concrete).isStatic = true;
                Vector3 mid = c + Vector3.down * depth * 0.5f;
                Gfx.Box("PitWall", root, mid + Vector3.forward * (hole * 0.5f + 0.1f), new Vector3(hole + 0.4f, depth, 0.2f), M.concrete).isStatic = true;
                Gfx.Box("PitWall", root, mid + Vector3.back * (hole * 0.5f + 0.1f), new Vector3(hole + 0.4f, depth, 0.2f), M.concrete).isStatic = true;
                Gfx.Box("PitWall", root, mid + Vector3.right * (hole * 0.5f + 0.1f), new Vector3(0.2f, depth, hole + 0.4f), M.concrete).isStatic = true;
                Gfx.Box("PitWall", root, mid + Vector3.left * (hole * 0.5f + 0.1f), new Vector3(0.2f, depth, hole + 0.4f), M.concrete).isStatic = true;
                var rnd = new System.Random(row * 37 + col);
                for (int i = 0; i < 9; i++)
                {
                    var sp = Gfx.Prim(PrimitiveType.Cylinder, "Spike", root, b + new Vector3(-0.7f + (i % 3) * 0.7f, 0.3f, -0.7f + (i / 3) * 0.7f) + new Vector3((float)rnd.NextDouble() - 0.5f, 0f, (float)rnd.NextDouble() - 0.5f) * 0.25f,
                        new Vector3(0.05f, 0.3f, 0.05f), M.rust, false);
                    sp.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 16f - 8f, 0f, (float)rnd.NextDouble() * 16f - 8f);
                }
                Gfx.Decal(root, b + Vector3.up * 0.3f, Vector3.down, 1.8f, 1.8f, Gfx.DecalKind.Blood, row * 7 + col);
            }
            // A red glow from inside so the hole stands out in the dark.
            var glow = Gfx.PointLight(root, c + Vector3.down * Mathf.Min(1.2f, depth * 0.5f), new Color(1f, 0.12f, 0.05f), 0.5f, 4.5f, LightShadows.None);
            Info.lights.Add(new TowerLight { light = glow, hd = glow.GetComponent<HDAdditionalLightData>(), floor = f, castsShadows = false, electric = false });
            var pit = root.gameObject.AddComponent<TrapPit>();
            pit.Setup(c, hole, f, depth);
            StaticMerge.Merge(root, "Pit");
        }

    }

    /// <summary>Hides an annex piece's renderers unless you're on the ground floor near it (keeps the far-floor culling).</summary>
    public class AnnexPiece : MonoBehaviour
    {
        Renderer[] renderers;
        Bounds bounds;
        bool shown = true;
        float next;

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            bool first = true;
            foreach (var r in renderers) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            next = Random.value * 0.3f;
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.3f;
            var p = PlayerController.Instance;
            if (p == null) return;
            bool show = p.Floor == 0 && bounds.SqrDistance(p.transform.position) < 70f * 70f;
            if (show == shown) return;
            shown = show;
            foreach (var r in renderers) if (r != null) r.enabled = show;
        }
    }

    /// <summary>A trap pit: fall in and it hurts; on the ground floor press Space (or wait) to climb out.</summary>
    public class TrapPit : MonoBehaviour
    {
        public static readonly List<TrapPit> All = new List<TrapPit>();
        public Vector3 Center { get; private set; }
        public int Floor { get; private set; }
        public static int Falls { get; private set; }
        float half, depth, inPitSince = -1f, climbHeld;
        bool caught;

        public void Setup(Vector3 center, float hole, int floor, float d)
        {
            Center = center; half = hole * 0.5f; Floor = floor; depth = d;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(Vector3 p) { return Mathf.Abs(p.x - Center.x) < half && Mathf.Abs(p.z - Center.z) < half; }

        void Update()
        {
            var p = PlayerController.Instance;
            var gm = GameManager.Instance;
            if (p == null || p.IsDead || gm == null || gm.IsPaused) return;
            Vector3 pos = p.transform.position;
            bool over = Contains(pos);
            if (!caught && over && pos.y < Center.y - 0.6f && pos.y > Center.y - depth - 1f)
            {
                caught = true;
                Falls++;
                inPitSince = Time.time;
                climbHeld = 0f;
                NoiseSystem.Emit(Center, 16f);
                Sfx.At(Sfx.Hurt, pos, 1f, 0.9f);
                if (Floor == 0)
                {
                    p.TakeDamage(22f, Center + Vector3.down * depth);
                    gm.Toast("SPIKE PIT! Hold Space to climb out - the dead heard that.");
                }
                else
                {
                    p.TakeDamage(15f, Center);
                    gm.Toast("The rotten floor gave way! You fell to floor " + Floor + ".");
                }
            }
            if (caught && Floor == 0)
            {
                bool inside = over && pos.y < Center.y - 0.6f;
                if (!inside) { caught = false; return; }
                if (InputWrap.Held(KeyCode.Space) || Time.time - inPitSince > 6f) climbHeld += Time.deltaTime;
                if (climbHeld > 0.7f) ClimbOut(p);
            }
            else if (caught && Floor > 0 && pos.y < Center.y - depth - 0.5f) caught = false;
        }

        void ClimbOut(PlayerController p)
        {
            caught = false;
            Vector3 best = Center + Vector3.forward * 2.2f;
            for (int k = 0; k < 4; k++)
            {
                Vector3 c = Center + Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward * 2.3f;
                if (!Physics.CheckCapsule(c + Vector3.up * 0.5f, c + Vector3.up * 1.6f, 0.36f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) { best = c; break; }
            }
            p.Teleport(best + Vector3.up * 0.05f, p.Yaw, 0f);
            Sfx.At(Sfx.Step, best, 0.8f, 0.8f);
        }
    }
}
