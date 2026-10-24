using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Dresses the remastered tower as an abandoned college: every room gets furniture and clutter for its type,
    /// picked from several arrangements and randomised per room, then a layer of decay (paper, fallen ceiling tiles,
    /// rubble, puddles, rubbish). Results go under "College_Dressing" in each floor root and can be cleared and rebuilt.
    ///
    /// Placement never trusts a model's pivot: every prop is measured after rotation and its lowest point is
    /// put on the floor (or on the surface below it, found by raycasting the host's real mesh).
    /// </summary>
    public static class CollegeDresser
    {
        public const float Cell = 4f;
        public const float Story = 4.2f;
        public const float CeilY = 3.95f;     // underside of the ceiling above a floor
        public const float WallInset = 0.2f;  // half wall + baseboard, measured from the cell edge
        public const string DressingName = "College_Dressing";
        static readonly string[] FloorRoots = { "Floor_1_Ground", "Floor_2_School", "Floor_3_Hotel", "Floor_4_Offices" };

        [MenuItem("Hollow Tower/College Dressing/2. Dress All Floors", priority = 71)]
        public static void DressAllMenu()
        {
            Debug.Log(DressAll(20261023));
        }

        [MenuItem("Hollow Tower/College Dressing/3. Clear Dressing", priority = 72)]
        public static void ClearAll()
        {
            for (int f = 0; f < 4; f++) { Clear(f); MeshMerge.ClearFloor(f); }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static string DressAll(int seed)
        {
            var sb = new System.Text.StringBuilder();
            for (int f = 0; f < 4; f++) { Clear(f); MeshMerge.ClearFloor(f); }
            ProcVariants.Reset();
            for (int f = 0; f < 4; f++) sb.AppendLine(DressFloor(f, seed));
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return sb.ToString();
        }

        public static Transform FloorRoot(int f)
        {
            var env = GameObject.Find("HollowTower_Remastered_Environment");
            return env != null ? env.transform.Find(FloorRoots[f]) : null;
        }

        public static void Clear(int f)
        {
            var root = FloorRoot(f);
            if (root == null) return;
            var old = root.Find(DressingName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        public static string DressFloor(int f, int seed)
        {
            Clear(f);
            MeshMerge.ClearFloor(f);
            RoomCtx.ok = RoomCtx.failInside = RoomCtx.failOverlap = RoomCtx.failScene = RoomCtx.failSpan = 0;
            var floorRoot = FloorRoot(f);
            if (floorRoot == null) return "floor " + (f + 1) + ": root not found";
            ProcKit.EnsureMaterials(f);
            var dressing = new GameObject(DressingName);
            dressing.transform.SetParent(floorRoot, false);
            Physics.SyncTransforms();

            var plan = LevelLayout.Plans[f];
            var lamps = new List<Vector2>();
            foreach (var t in floorRoot.GetComponentsInChildren<Transform>())
                if (t.name == "Lamp_Fixture") lamps.Add(new Vector2(t.position.x, t.position.z));

            int rooms = 0, objects = 0;
            var lastStyle = new Dictionary<string, int>();
            foreach (var room in plan.rooms)
            {
                if (room.type == RoomType.Stair || room.type == RoomType.Elevator) continue;
                var group = new GameObject("Room_" + room.name.Replace(' ', '_').Replace("'", ""));
                group.transform.SetParent(dressing.transform, false);
                var ctx = new RoomCtx(plan, room, f, new Random(seed * 31 + f * 1009 + room.id * 7919), group.transform, lamps, lastStyle);
                try
                {
                    if (room == plan.corridor) CollegeRecipes.Corridor(ctx);
                    else
                    {
                        CollegeRecipes.Dress(ctx);
                        CollegeRecipes.Decay(ctx);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[CollegeDresser] " + room.name + " (floor " + (f + 1) + "): " + e);
                }
                rooms++;
                objects += group.transform.childCount;
                MeshMerge.MergeRoom(group.transform, f);
                ctx.Finish();
                Physics.SyncTransforms();
            }
            return "floor " + (f + 1) + ": " + rooms + " rooms, " + objects + " props (ok " + RoomCtx.ok + ", inside " + RoomCtx.failInside + ", overlap " + RoomCtx.failOverlap +
                   ", scene " + RoomCtx.failScene + " last " + RoomCtx.LastSceneHit + ", span " + RoomCtx.failSpan + ")";
        }
    }

    public enum Pose { Upright, Wobble, SideLeft, SideRight, OnBack, OnFront, UpsideDown }

    /// <summary>A wall face inside a room, already trimmed back from corners and door openings.</summary>
    public class WallSeg
    {
        public Vector2 a, b;      // ends on the wall face (world xz)
        public Vector2 n;         // into the room
        public bool outer;        // on the building's outside wall (windows go here)
        public float Length { get { return Vector2.Distance(a, b); } }
        public Vector2 Tangent { get { return (b - a).normalized; } }
        public Vector2 Point(float t) { return Vector2.Lerp(a, b, t); }
    }

    public class DoorInfo
    {
        public Vector2 mid;   // centre of the opening on the wall line
        public Vector2 n;     // into the room
    }

    struct Occ
    {
        public Rect r;
        public float top;
    }

    /// <summary>One room being dressed, with the placement rules shared by every recipe.</summary>
    public class RoomCtx
    {
        public readonly FloorPlan plan;
        public readonly Room room;
        public readonly int floor;
        public readonly float y0;
        public readonly Random rnd;
        public readonly Transform group;
        public readonly bool isCorridor;
        public readonly List<WallSeg> walls = new List<WallSeg>();
        public readonly List<DoorInfo> doors = new List<DoorInfo>();
        public Rect interior;      // bounding box of the usable floor (world xz)
        public int mess;           // 0 tidy-abandoned .. 3 wrecked
        readonly List<Vector2> lamps;
        readonly Dictionary<string, int> lastStyle;
        readonly List<Occ> occupied = new List<Occ>();
        readonly List<Rect> keepClear = new List<Rect>();
        readonly List<Rect> wallMounts = new List<Rect>();   // x = tangent coordinate rect on (segment index, height)
        readonly List<int> wallMountSeg = new List<int>();
        float stack;               // tiny lift so overlapping flat items never z-fight
        public static int failInside, failOverlap, failScene, failSpan, ok;   // placement statistics for tuning
        public static string LastSceneHit = "";

        public RoomCtx(FloorPlan plan, Room room, int floor, Random rnd, Transform group, List<Vector2> lamps, Dictionary<string, int> lastStyle)
        {
            this.plan = plan; this.room = room; this.floor = floor; this.rnd = rnd; this.group = group;
            this.lamps = lamps; this.lastStyle = lastStyle;
            y0 = floor * CollegeDresser.Story;
            isCorridor = room == plan.corridor;
            mess = Weighted(new[] { 5f, 4f, 1.2f, 0.25f });   // mostly "left behind"; real damage is rare
            BuildWalls();
            if (isCorridor)
            {
                interior = new Rect(0, 0, LevelLayout.Cols * CollegeDresser.Cell, LevelLayout.Rows * CollegeDresser.Cell);
            }
            else
            {
                var R = room.rect;
                float c = CollegeDresser.Cell, i = CollegeDresser.WallInset;
                interior = Rect.MinMaxRect(R.x * c + i, (LevelLayout.Rows - R.y - R.height) * c + i,
                                           (R.x + R.width) * c - i, (LevelLayout.Rows - R.y) * c - i);
            }
            foreach (var d in doors)
            {
                // Swing and entry space in front of every doorway, plus a walkway towards the room centre.
                Vector2 t = new Vector2(Mathf.Abs(d.n.y), Mathf.Abs(d.n.x));
                keepClear.Add(RectAround(d.mid + d.n * 1.1f, t * 2.5f + new Vector2(Mathf.Abs(d.n.x), Mathf.Abs(d.n.y)) * 2.2f));
                if (!isCorridor)
                {
                    Vector2 c = interior.center;
                    float depth = Mathf.Abs(Vector2.Dot(c - d.mid, d.n));
                    keepClear.Add(RectAround(d.mid + d.n * depth * 0.5f, t * 1.3f + new Vector2(Mathf.Abs(d.n.x), Mathf.Abs(d.n.y)) * depth));
                }
            }
            // Elevator cabs: keep the cab and the landing in front of it clear.
            if (floor == 0) keepClear.Add(Rect.MinMaxRect(134.5f, 8.5f, 144f, 15.5f));
            if (floor == 3) keepClear.Add(Rect.MinMaxRect(134.5f, 2.5f, 144f, 9.5f));
        }

        // ------------------------------------------------------------------ randomness
        public float R01() { return (float)rnd.NextDouble(); }
        public float Range(float a, float b) { return a + (b - a) * R01(); }
        public int Range(int a, int bExclusive) { return rnd.Next(a, bExclusive); }
        public bool Chance(float p) { return rnd.NextDouble() < p; }
        public T Pick<T>(params T[] items) { return items[rnd.Next(items.Length)]; }
        public int Weighted(float[] w)
        {
            float sum = 0; foreach (var x in w) sum += x;
            float v = R01() * sum;
            for (int i = 0; i < w.Length; i++) { if (v < w[i]) return i; v -= w[i]; }
            return w.Length - 1;
        }
        /// <summary>Picks a layout style, avoiding the one used by the previous room of the same kind.</summary>
        public int Style(string kind, int count)
        {
            int last;
            lastStyle.TryGetValue(kind, out last);
            int s = rnd.Next(count);
            if (count > 1 && lastStyle.ContainsKey(kind) && s == last) s = (s + 1 + rnd.Next(count - 1)) % count;
            lastStyle[kind] = s;
            return s;
        }
        public Pose MessyPose(float tipChance)
        {
            if (!Chance(tipChance)) return Chance(0.3f) ? Pose.Wobble : Pose.Upright;
            return Pick(Pose.SideLeft, Pose.SideRight, Pose.OnBack, Pose.OnBack, Pose.OnFront);
        }
        public float Yaw(Vector2 facing) { return Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg; }

        // ------------------------------------------------------------------ geometry of the room
        static Rect RectAround(Vector2 c, Vector2 size) { return new Rect(c - size * 0.5f, size); }

        int ZoneAt(float x, float z)
        {
            int c = Mathf.FloorToInt(x / CollegeDresser.Cell);
            int r = LevelLayout.Rows - 1 - Mathf.FloorToInt(z / CollegeDresser.Cell);
            if (c < 0 || r < 0 || c >= LevelLayout.Cols || r >= LevelLayout.Rows) return -2;
            if (plan.pit[r, c] || plan.shaft[r, c] || plan.ramp[r, c]) return -3;
            return plan.zone[r, c];
        }

        void BuildWalls()
        {
            float C = CollegeDresser.Cell;
            var raw = new List<WallSeg>();
            foreach (var cell in room.cells)
            {
                int c = cell.x, r = cell.y;
                float x0 = c * C, x1 = (c + 1) * C, z1 = (LevelLayout.Rows - r) * C, z0 = z1 - C;
                // east, west, north, south neighbours: (edge key, wall line ends, inward normal)
                AddEdge(raw, c + 1, r, LevelLayout.EdgeE(r, c), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(-1, 0));
                AddEdge(raw, c - 1, r, c > 0 ? LevelLayout.EdgeE(r, c - 1) : -1, new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(1, 0));
                AddEdge(raw, c, r - 1, r > 0 ? LevelLayout.EdgeS(r - 1, c) : -1, new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(0, -1));
                AddEdge(raw, c, r + 1, r < LevelLayout.Rows - 1 ? LevelLayout.EdgeS(r, c) : LevelLayout.EdgeS(r, c), new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(0, 1));
            }
            // Merge collinear touching pieces that share a normal.
            raw.Sort((p, q) =>
            {
                int k = Key(p).CompareTo(Key(q));
                if (k != 0) return k;
                return Along(p, p.a).CompareTo(Along(q, q.a));
            });
            WallSeg cur = null;
            foreach (var s in raw)
            {
                if (cur != null && Key(cur) == Key(s) && cur.outer == s.outer && Mathf.Abs(Along(cur, cur.b) - Along(s, s.a)) < 0.01f) { cur.b = s.b; continue; }
                if (cur != null) walls.Add(cur);
                cur = new WallSeg { a = s.a, b = s.b, n = s.n, outer = s.outer };
            }
            if (cur != null) walls.Add(cur);
            // Move to the wall face and trim the ends clear of corners and door frames.
            foreach (var w in walls)
            {
                var t = w.Tangent;
                w.a += w.n * CollegeDresser.WallInset + t * CollegeDresser.WallInset;
                w.b += w.n * CollegeDresser.WallInset - t * CollegeDresser.WallInset;
            }
            walls.RemoveAll(w => w.Length < 0.5f || Vector2.Dot(w.b - w.a, w.Tangent) <= 0);
        }

        // Key groups pieces on the same line with the same normal; Along orders them along it.
        static string Key(WallSeg s) { return s.n.ToString() + (Mathf.Abs(s.n.x) > 0.5f ? s.a.x : s.a.y).ToString("F2"); }
        static float Along(WallSeg s, Vector2 p) { return Mathf.Abs(s.n.x) > 0.5f ? p.y : p.x; }

        void AddEdge(List<WallSeg> raw, int nc, int nr, int edgeKey, Vector2 a, Vector2 b, Vector2 n)
        {
            int z = (nc < 0 || nr < 0 || nc >= LevelLayout.Cols || nr >= LevelLayout.Rows) ? -1 : plan.zone[nr, nc];
            if (z == room.id) return;   // open floor inside the room
            bool door = edgeKey >= 0 && plan.doors.Contains(edgeKey);
            bool outside = z == -1;
            if (!door) { raw.Add(new WallSeg { a = a, b = b, n = n, outer = outside }); return; }
            Vector2 mid = (a + b) * 0.5f, t = (b - a).normalized;
            doors.Add(new DoorInfo { mid = mid, n = n });
            float half = 1.05f;  // 1.8 m opening plus frame
            raw.Add(new WallSeg { a = a, b = mid - t * half, n = n });
            raw.Add(new WallSeg { a = mid + t * half, b = b, n = n });
        }

        public WallSeg LongestWall()
        {
            WallSeg best = null;
            foreach (var w in walls) if (best == null || w.Length > best.Length) best = w;
            return best;
        }

        public WallSeg RandomWall(float minLength = 0.5f)
        {
            var list = walls.FindAll(w => w.Length >= minLength);
            return list.Count > 0 ? list[rnd.Next(list.Count)] : null;
        }

        public Vector2 RandomPoint(float margin = 0.3f)
        {
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector2(Range(interior.xMin + margin, interior.xMax - margin), Range(interior.yMin + margin, interior.yMax - margin));
                if (ZoneAt(p.x, p.y) == room.id) return p;
            }
            return interior.center;
        }

        // ------------------------------------------------------------------ validity
        bool InsideRoom(Rect fp)
        {
            if (floor == 0 && Floor1Design.Excluded(fp)) return false;   // pits, barricades, false walls, fuse and socket
            float i = CollegeDresser.WallInset - 0.02f;
            // Every sample, pushed out by the wall inset, must still be on this room's floor.
            for (int sx = 0; sx <= 2; sx++)
                for (int sz = 0; sz <= 2; sz++)
                {
                    float x = Mathf.Lerp(fp.xMin, fp.xMax, sx * 0.5f), z = Mathf.Lerp(fp.yMin, fp.yMax, sz * 0.5f);
                    float ox = sx == 0 ? -i : sx == 2 ? i : 0, oz = sz == 0 ? -i : sz == 2 ? i : 0;
                    if (ZoneAt(x, z) != room.id) return false;
                    if (ZoneAt(x + ox, z) != room.id || ZoneAt(x, z + oz) != room.id || ZoneAt(x + ox, z + oz) != room.id) return false;
                }
            return true;
        }

        /// <summary>Roughly how much of an area (0..1) is already covered by solid props.</summary>
        public float Coverage(Rect area)
        {
            float covered = 0;
            foreach (var o in occupied)
            {
                if (o.top <= y0 + 0.05f || !o.r.Overlaps(area)) continue;
                var i = Rect.MinMaxRect(Mathf.Max(o.r.xMin, area.xMin), Mathf.Max(o.r.yMin, area.yMin), Mathf.Min(o.r.xMax, area.xMax), Mathf.Min(o.r.yMax, area.yMax));
                covered += i.width * i.height;
            }
            return covered / Mathf.Max(0.01f, area.width * area.height);
        }

        /// <summary>True if the point is on this room's floor (not a pit, stair, other room or doorway zone).</summary>
        public bool IsFree(Vector2 p, float radius)
        {
            if (ZoneAt(p.x, p.y) != room.id) return false;
            var r = new Rect(p.x - radius, p.y - radius, radius * 2, radius * 2);
            foreach (var k in keepClear) if (k.Overlaps(r)) return false;
            return true;
        }

        bool Overlaps(Rect fp, float pad, float bottom, bool blocking)
        {
            var big = new Rect(fp.x - pad, fp.y - pad, fp.width + pad * 2, fp.height + pad * 2);
            foreach (var o in occupied) if (o.top > bottom + 0.01f && o.r.Overlaps(big)) return true;
            if (blocking) foreach (var k in keepClear) if (k.Overlaps(fp)) return true;
            return false;
        }

        bool HitsScene(Bounds b)
        {
            var half = new Vector3(b.extents.x * 0.95f, Mathf.Max(0.02f, b.extents.y * 0.9f), b.extents.z * 0.95f);
            var center = b.center;
            if (center.y - half.y < y0 + 0.06f) { float top = center.y + half.y; center.y = (y0 + 0.06f + top) * 0.5f; half.y = Mathf.Max(0.02f, (top - y0 - 0.06f) * 0.5f); }
            foreach (var c in Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(group.parent)) continue;   // other dressing is tracked in 'occupied'
                if (c.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                LastSceneHit = c.name + " @" + c.transform.position;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ spawning
        public GameObject Make(string id)
        {
            var prefab = PropLibrary.Load(id);
            if (prefab == null) { Debug.LogWarning("[CollegeDresser] missing prop " + id); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            return go;
        }

        GameObject Build(object what)
        {
            var id = what as string;
            if (id != null) return Make(id);
            var variant = what as ProcVariant;
            if (variant != null)
            {
                var inst = ProcVariants.Instantiate(variant, this, group);
                if (inst != null) inst.name = variant.key.Split('_')[0];
                return inst;
            }
            var fn = what as Func<RoomCtx, GameObject>;
            if (fn == null) return null;
            var go = fn(this);
            if (go != null) go.transform.SetParent(group, true);
            return go;
        }

        static Quaternion PoseRot(Pose p, RoomCtx c)
        {
            switch (p)
            {
                case Pose.Wobble: return Quaternion.Euler(c.Range(-4f, 4f), 0, c.Range(-4f, 4f));
                case Pose.SideLeft: return Quaternion.Euler(0, 0, 90);
                case Pose.SideRight: return Quaternion.Euler(0, 0, -90);
                case Pose.OnBack: return Quaternion.Euler(-90, 0, 0);
                case Pose.OnFront: return Quaternion.Euler(90, 0, 0);
                case Pose.UpsideDown: return Quaternion.Euler(180, 0, 0);
                default: return Quaternion.identity;
            }
        }

        void Settle(GameObject go, Vector2 xz, float baseY)
        {
            var b = PropLibrary.RendererBounds(go);
            go.transform.position += new Vector3(xz.x - b.center.x, baseY - b.min.y, xz.y - b.center.z);
        }

        static Rect Footprint(Bounds b) { return new Rect(b.min.x, b.min.z, b.size.x, b.size.z); }

        void Commit(GameObject go, Bounds b, bool solid)
        {
            occupied.Add(new Occ { r = Footprint(b), top = solid ? b.max.y : b.min.y + 0.001f });
            Physics.SyncTransforms();
        }

        /// <summary>Stands a prop (or procedural builder) on the floor at xz, lowest point on the floor.</summary>
        public GameObject Floor(object what, Vector2 xz, float yaw, Pose pose = Pose.Upright, bool blocking = true, float pad = 0.05f)
        {
            var go = Build(what);
            if (go == null) return null;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * PoseRot(pose, this);
            go.transform.position = new Vector3(xz.x, y0, xz.y);
            Settle(go, xz, y0 + 0.002f);
            var b = PropLibrary.RendererBounds(go);
            var fp = Footprint(b);
            bool flat = b.size.y < 0.05f;
            if (!InsideRoom(fp)) { failInside++; Object.DestroyImmediate(go); return null; }
            if (Overlaps(fp, flat ? 0f : pad, flat ? b.max.y : y0, blocking && !flat)) { failOverlap++; Object.DestroyImmediate(go); return null; }
            if (!flat && HitsScene(b)) { failScene++; Object.DestroyImmediate(go); return null; }
            ok++;
            if (flat)
            {
                // cycle through a few tiny lifts so overlapping sheets never z-fight, without ever visibly floating
                go.transform.position += Vector3.up * stack;
                stack = (stack + 0.0006f) % 0.0048f;
            }
            Commit(go, b, !flat);
            return go;
        }

        public GameObject FloorAnywhere(object what, Pose pose = Pose.Upright, bool blocking = true, int tries = 12, float pad = 0.05f)
        {
            for (int i = 0; i < tries; i++)
            {
                var go = Floor(what, RandomPoint(), Range(0f, 360f), pose, blocking, pad);
                if (go != null) return go;
            }
            return null;
        }

        /// <summary>Stands a prop with its back to a wall. t is the position along the wall (0..1).</summary>
        public GameObject AgainstWall(object what, WallSeg w, float t, Pose pose = Pose.Upright, float gap = 0.03f, float yawJitter = 0f, bool blocking = true, float lean = 0f)
        {
            if (w == null) return null;
            var go = Build(what);
            if (go == null) return null;
            float yaw = Yaw(w.n) + yawJitter;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(-lean, 0, 0) * PoseRot(pose, this);
            var p = w.Point(t);
            go.transform.position = new Vector3(p.x, y0, p.y);
            Settle(go, p, y0 + 0.002f);
            var b = PropLibrary.RendererBounds(go);
            // Slide in or out along the wall normal so the back of the bounds touches the wall face.
            float ext = Mathf.Abs(w.n.x) * b.extents.x + Mathf.Abs(w.n.y) * b.extents.z;
            Vector2 c = new Vector2(b.center.x, b.center.z);
            float want = Vector2.Dot(p, w.n) + gap + ext;
            Vector2 shift = w.n * (want - Vector2.Dot(c, w.n));
            go.transform.position += new Vector3(shift.x, 0, shift.y);
            b = PropLibrary.RendererBounds(go);
            var fp = Footprint(b);
            // Must fit along the wall piece (not over a door opening or past a corner).
            Vector2 tan = w.Tangent;
            float ta = Vector2.Dot(w.a, tan), tb = Vector2.Dot(w.b, tan);
            float halfT = Mathf.Abs(tan.x) * b.extents.x + Mathf.Abs(tan.y) * b.extents.z;
            float tc = Vector2.Dot(new Vector2(b.center.x, b.center.z), tan);
            bool flat = b.size.y < 0.05f;
            if (tc - halfT < Mathf.Min(ta, tb) - 0.05f || tc + halfT > Mathf.Max(ta, tb) + 0.05f) { failSpan++; Object.DestroyImmediate(go); return null; }
            if (!InsideRoom(fp)) { failInside++; Object.DestroyImmediate(go); return null; }
            if (Overlaps(fp, 0.03f, y0, blocking)) { failOverlap++; Object.DestroyImmediate(go); return null; }
            if (!flat && HitsScene(b)) { failScene++; Object.DestroyImmediate(go); return null; }
            ok++;
            Commit(go, b, true);
            return go;
        }

        /// <summary>Lines a wall with props, packing them along it with random gaps.</summary>
        public List<GameObject> AlongWall(WallSeg w, Func<object> next, float gapMin, float gapMax, float fill = 1f, Func<Pose> pose = null)
        {
            var list = new List<GameObject>();
            if (w == null) return list;
            float len = w.Length, pos = Range(0f, gapMax);
            while (pos < len - 0.3f)
            {
                var what = next();
                if (what == null) break;
                if (!Chance(fill)) { pos += Range(0.6f, 1.4f); continue; }
                var go = AgainstWall(what, w, Mathf.Clamp01((pos + 0.35f) / len), pose != null ? pose() : Pose.Upright, 0.03f, Range(-3f, 3f));
                if (go == null) { pos += 0.5f; continue; }
                var b = PropLibrary.RendererBounds(go);
                float width = Mathf.Abs(w.Tangent.x) * b.size.x + Mathf.Abs(w.Tangent.y) * b.size.z;
                // Re-centre on the running position so items pack from one end.
                Vector2 tan = w.Tangent;
                float tc = Vector2.Dot(new Vector2(b.center.x, b.center.z), tan) - Vector2.Dot(w.a, tan);
                float desired = pos + width * 0.5f;
                if (Mathf.Abs(desired - tc) > 0.02f && desired + width * 0.5f < len)
                {
                    Vector2 s = tan * (desired - tc);
                    go.transform.position += new Vector3(s.x, 0, s.y);
                    var nb = PropLibrary.RendererBounds(go);
                    occupied[occupied.Count - 1] = new Occ { r = Footprint(nb), top = nb.max.y };
                }
                list.Add(go);
                pos = desired + width * 0.5f + Range(gapMin, gapMax);
            }
            return list;
        }

        /// <summary>Hangs a wall-mounted prop (pivot = back face centre) at a height above the floor.</summary>
        public GameObject OnWall(object what, WallSeg w, float t, float height, float tilt = 0f)
        {
            if (w == null) return null;
            var go = Build(what);
            if (go == null) return null;
            // the wall line sits WallInset from the cell edge (it clears the baseboard); the plaster is 0.125 m from it
            var p = w.Point(t) - w.n * (CollegeDresser.WallInset - 0.125f - 0.004f);
            go.transform.rotation = Quaternion.Euler(0, Yaw(w.n), tilt);
            go.transform.position = new Vector3(p.x, y0 + height, p.y);
            var b = PropLibrary.RendererBounds(go);
            var fp = Footprint(b);
            int wi = walls.IndexOf(w);
            var span = new Rect(Vector2.Dot(new Vector2(b.min.x, b.min.z), w.Tangent), b.min.y, 0, b.size.y);
            float t0 = Mathf.Min(Vector2.Dot(new Vector2(b.min.x, b.min.z), w.Tangent), Vector2.Dot(new Vector2(b.max.x, b.max.z), w.Tangent));
            float t1 = Mathf.Max(Vector2.Dot(new Vector2(b.min.x, b.min.z), w.Tangent), Vector2.Dot(new Vector2(b.max.x, b.max.z), w.Tangent));
            span = Rect.MinMaxRect(t0 - 0.1f, b.min.y - 0.05f, t1 + 0.1f, b.max.y + 0.05f);
            bool clash = false;
            for (int i = 0; i < wallMounts.Count; i++) if (wallMountSeg[i] == wi && wallMounts[i].Overlaps(span)) clash = true;
            foreach (var o in occupied) if (o.top > b.min.y - 0.05f && o.r.Overlaps(fp)) clash = true;
            foreach (var k in keepClear) if (k.Overlaps(fp) && b.min.y < y0 + 2.4f) clash = true;
            if (floor == 0 && Floor1Design.Excluded(fp)) clash = true;
            float ta = Vector2.Dot(w.a, w.Tangent), tb = Vector2.Dot(w.b, w.Tangent);
            // test against the scene without the slice that touches the wall it hangs on
            var nb = b;
            var n3 = new Vector3(w.n.x, 0, w.n.y);
            float depthN = Mathf.Abs(w.n.x) * b.size.x + Mathf.Abs(w.n.y) * b.size.z;
            float trim = Mathf.Min(0.05f, depthN * 0.5f);
            nb.center += n3 * trim * 0.5f;
            nb.size -= new Vector3(Mathf.Abs(w.n.x), 0, Mathf.Abs(w.n.y)) * trim;
            if (clash || t0 < Mathf.Min(ta, tb) || t1 > Mathf.Max(ta, tb) || b.max.y > y0 + CollegeDresser.CeilY - 0.05f || HitsScene(nb))
            {
                Object.DestroyImmediate(go);
                return null;
            }
            wallMounts.Add(span); wallMountSeg.Add(wi);
            return go;
        }

        /// <summary>Hangs a ceiling prop (pivot = top centre) clear of the existing lamp fixtures.</summary>
        public GameObject OnCeiling(object what, Vector2 xz, float yaw = 0f, float droop = 0f)
        {
            foreach (var l in lamps) if (Vector2.Distance(l, xz) < 1.3f) return null;
            if (ZoneAt(xz.x, xz.y) != room.id) return null;
            var go = Build(what);
            if (go == null) return null;
            go.transform.rotation = Quaternion.Euler(droop, yaw, 0);
            go.transform.position = new Vector3(xz.x, y0 + CollegeDresser.CeilY, xz.y);
            return go;
        }

        /// <summary>
        /// Puts small props on a host's surfaces, found by raycasting its actual meshes, so items sit on the
        /// desk top or shelf boards rather than on the bounding box. levels > 1 also fills the shelves below.
        /// </summary>
        public List<GameObject> OnSurface(GameObject host, Func<object> next, int count, int levels = 1, Pose pose = Pose.Upright)
        {
            var placed = new List<GameObject>();
            if (host == null) return placed;
            var temp = new List<MeshCollider>();
            foreach (var mf in host.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            Physics.SyncTransforms();
            var hb = PropLibrary.RendererBounds(host);
            var onHost = new List<KeyValuePair<float, Rect>>();   // (surface height, footprint)
            for (int n = 0, tries = 0; n < count && tries < count * 8; tries++)
            {
                var what = next();
                if (what == null) break;
                var go = Build(what);
                if (go == null) break;
                go.transform.rotation = Quaternion.Euler(0, Range(0f, 360f), 0) * PoseRot(pose, this);
                var ib = PropLibrary.RendererBounds(go);
                float hx = ib.extents.x, hz = ib.extents.z;
                if (hx * 2 > hb.size.x * 0.9f || hz * 2 > hb.size.z * 0.9f) { Object.DestroyImmediate(go); continue; }
                float x = Range(hb.min.x + hx, hb.max.x - hx), z = Range(hb.min.z + hz, hb.max.z - hz);
                int level = levels <= 1 ? 0 : rnd.Next(levels);
                float startY = levels <= 1 ? hb.max.y + 0.3f : Mathf.Lerp(hb.min.y + 0.35f, hb.max.y + 0.3f, (level + 0.5f) / levels);
                float surf;
                if (!SurfaceBelow(host, x, z, hx, hz, startY, out surf) || surf < y0 + 0.08f)
                {
                    Object.DestroyImmediate(go);
                    continue;
                }
                // Room above the item (a shelf board higher up must not cut through it).
                RaycastHit up;
                if (Physics.Raycast(new Vector3(x, surf + 0.01f, z), Vector3.up, out up, ib.size.y + 0.02f) && up.collider.transform.IsChildOf(host.transform))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }
                var fp = new Rect(x - hx, z - hz, hx * 2, hz * 2);
                bool clash = false;
                foreach (var o in onHost) if (Mathf.Abs(o.Key - surf) < 0.05f && o.Value.Overlaps(fp)) clash = true;
                if (clash) { Object.DestroyImmediate(go); continue; }
                Settle(go, new Vector2(x, z), surf + 0.001f);
                onHost.Add(new KeyValuePair<float, Rect>(surf, fp));
                placed.Add(go);
                n++;
            }
            foreach (var mc in temp) Object.DestroyImmediate(mc);
            Physics.SyncTransforms();
            return placed;
        }

        /// <summary>Sets a prop onto a host's real surface at an exact spot (e.g. desks onto a lecture tier).</summary>
        public GameObject PlaceOn(GameObject host, object what, Vector2 xz, float yaw, Pose pose = Pose.Upright)
        {
            if (host == null) return null;
            var temp = new List<MeshCollider>();
            foreach (var mf in host.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
            Physics.SyncTransforms();
            var hb = PropLibrary.RendererBounds(host);
            GameObject go = null;
            float best = float.MinValue;
            foreach (var h in Physics.RaycastAll(new Vector3(xz.x, hb.max.y + 0.5f, xz.y), Vector3.down, hb.size.y + 1f))
                if (h.collider.transform.IsChildOf(host.transform) && h.point.y > best) best = h.point.y;
            if (best > float.MinValue)
            {
                go = Build(what);
                if (go != null)
                {
                    go.transform.rotation = Quaternion.Euler(0, yaw, 0) * PoseRot(pose, this);
                    go.transform.position = new Vector3(xz.x, best, xz.y);
                    Settle(go, xz, best + 0.002f);
                }
            }
            foreach (var mc in temp) Object.DestroyImmediate(mc);
            Physics.SyncTransforms();
            return go;
        }

        bool SurfaceBelow(GameObject host, float x, float z, float hx, float hz, float startY, out float surf)
        {
            surf = 0;
            float lo = float.MaxValue, hi = float.MinValue;
            var pts = new[] { new Vector2(x, z), new Vector2(x - hx, z - hz), new Vector2(x + hx, z - hz), new Vector2(x - hx, z + hz), new Vector2(x + hx, z + hz) };
            foreach (var p in pts)
            {
                float best = float.MinValue;
                foreach (var h in Physics.RaycastAll(new Vector3(p.x, startY, p.y), Vector3.down, startY - y0 + 0.1f))
                    if (h.collider.transform.IsChildOf(host.transform) && h.point.y > best) best = h.point.y;
                if (best == float.MinValue) return false;
                lo = Mathf.Min(lo, best); hi = Mathf.Max(hi, best);
            }
            if (hi - lo > 0.025f) return false;   // not flat under the whole item
            surf = hi;
            return true;
        }

        /// <summary>Drops the room group if nothing ended up in it.</summary>
        public void Finish()
        {
            if (group.childCount == 0) Object.DestroyImmediate(group.gameObject);
        }
    }
}
