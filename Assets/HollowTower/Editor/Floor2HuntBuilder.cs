using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using HollowTower.EditorTools;
using Random = System.Random;

namespace HollowTower
{
    /// <summary>
    /// Floor 2's hunt, built on top of the party dressing (the party builder runs this last; menu: Hollow Tower > Floor 2
    /// > Build Monsters, Hiding and Pits):
    /// - Broken floors. Floor 2 sits right on floor 1's rooms, so a hole goes through: fall in and you land in the room
    ///   below, hurt (HardLanding). Open holes everyone sees; hidden ones keep the boards drawn over them for everyone
    ///   but P2 (nobody can stand on them); and one corridor has fallen in completely, crossed by a plank walkway that
    ///   only P2 can see. Floor 1's ceiling gets the matching hole.
    /// - Hiding spots (PartyHideSpot) on every locker bank and tall cabinet, plus a cupboard in each party room that has
    ///   nowhere to hide.
    /// - The floor's NavMesh, then the party guests (PartyMonster): one or two per room and a couple in the corridors,
    ///   each in a party hat and busy with something that belongs to the room.
    /// Everything it changes outside Floor_2_Hunt is in a Floor2HuntRecord, so it can be rebuilt any number of times.
    /// </summary>
    public static class Floor2HuntBuilder
    {
        const string RootName = "Floor_2_Hunt";
        const string NavPath = "Assets/HollowTowerRemastered/Navigation/Floor2_NavMesh.asset";
        const string MeshDir = "Assets/HollowTower/Generated/Hunt";
        const float FloorY = 4.2f, SlabBottom = 4.0f, F1CeilY = 3.975f;

        struct PitSpec { public string name; public Rect rect; public bool hidden, bridge; }

        // world xz rects (x = col * 4, z = (24 - row) * 4); see the floor plan in LevelLayout
        static readonly PitSpec[] Designed =
        {
            new PitSpec { name = "Corridor_Collapse", rect = Rect.MinMaxRect(80.3f, 24.05f, 87.7f, 27.95f), bridge = true },
            new PitSpec { name = "Hidden_NorthCorridor", rect = Centred(42f, 70f, 2.6f), hidden = true },
            new PitSpec { name = "Hidden_EastCorridor", rect = Centred(118f, 46f, 2.6f), hidden = true },
            new PitSpec { name = "Hidden_WestCorridor", rect = Centred(26f, 38f, 2.4f), hidden = true },
            new PitSpec { name = "Hidden_Classroom", rect = Centred(82f, 58f, 2.6f), hidden = true },
            new PitSpec { name = "Hidden_Library", rect = Centred(134f, 58f, 2.6f), hidden = true },
            new PitSpec { name = "Open_ArtRoom", rect = Centred(38f, 38f, 2.4f) },
            new PitSpec { name = "Open_Classroom", rect = Centred(106f, 14f, 2.4f) },
        };

        static PitSpec[] Pits = Designed;   // this build's copy (the collapse may be slid along its corridor)

        static Rect Centred(float x, float z, float s) { return new Rect(x - s * 0.5f, z - s * 0.5f, s, s); }

        static Floor2HuntRecord rec;
        static Transform f2, f1;
        static Material plankMat, darkMat, hatMat0, toyMat;
        static int meshCount;

        [MenuItem("Hollow Tower/Floor 2/Build Monsters, Hiding and Pits")]
        public static void BuildMenu() { Debug.Log("[Floor2Hunt] " + Build()); }

        [MenuItem("Hollow Tower/Floor 2/Remove Monsters, Hiding and Pits")]
        public static void RemoveMenu() { Revert(); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

        static Transform Floor(string name)
        {
            var env = GameObject.Find("HollowTower_Remastered_Environment");
            return env != null ? env.transform.Find(name) : null;
        }

        public static void Revert()
        {
            var floor = Floor("Floor_2_School");
            var old = floor != null ? floor.Find(RootName) : null;
            if (old == null) return;
            var r = old.GetComponent<Floor2HuntRecord>();
            if (r != null)
            {
                foreach (var go in r.deactivated) if (go != null) go.SetActive(true);
                foreach (var c in r.disabledColliders) if (c != null) c.enabled = true;
                foreach (var c in r.addedComponents) if (c != null) Object.DestroyImmediate(c);
            }
            Object.DestroyImmediate(old.gameObject);
        }

        public static string Build()
        {
            f2 = Floor("Floor_2_School"); f1 = Floor("Floor_1_Ground");
            var partyGo = GameObject.Find("Player_Party");
            var party = partyGo != null ? partyGo.GetComponent<PlayerParty>() : null;
            if (f2 == null || f1 == null || party == null) return "floor 1, floor 2 or Player_Party missing";
            Revert();
            string imp = PartyMonsterImport.Build();

            if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/HollowTower/Generated", "Hunt");
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            meshCount = 0;
            plankMat = Mat("Mat_Hunt_Plank", new Color(0.42f, 0.3f, 0.2f), 0f, 0.25f);
            darkMat = Mat("Mat_Hunt_Splinter", new Color(0.16f, 0.11f, 0.08f), 0f, 0.15f);
            toyMat = Mat("Mat_Hunt_Toy", new Color(0.9f, 0.85f, 0.75f), 0f, 0.4f);

            var root = new GameObject(RootName);
            root.transform.SetParent(f2, false);
            rec = root.AddComponent<Floor2HuntRecord>();
            var rnd = new Random(4242);

            var report = new System.Text.StringBuilder();
            int holes = 0;
            Pits = (PitSpec[])Designed.Clone();
            for (int i = 0; i < Pits.Length; i++)
            {
                if (!Pits[i].bridge) { if (BuildPit(root.transform, Pits[i], rnd, report)) holes++; continue; }
                // the collapse slides along its corridor until it finds a stretch with no classroom door onto it
                foreach (float dx in new[] { 0f, 4f, -4f, 8f, -8f, 12f, -12f, 16f })
                {
                    var moved = Pits[i];
                    moved.rect.x += dx;
                    if (DoorIn(moved.rect) != null) continue;
                    Pits[i] = moved;
                    if (BuildPit(root.transform, moved, rnd, report)) holes++;
                    break;
                }
            }
            Physics.SyncTransforms();

            int cleared = ClearElevator();
            int spots = HideSpots(root.transform, rnd);
            string fuses = FuseCells(root, rnd);
            Physics.SyncTransforms();
            string nav = BakeNavMesh(root, party);
            int guests = Guests(root.transform, rnd);
            report.Append("\n  " + TowerItemsBuilder.Build());
            report.Append("\n  " + HollowTower.EditorTools.NetSetup.NetworkMonsters() + " monsters networked");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return imp + "; " + holes + "/" + Pits.Length + " broken floors; " + spots + " hiding spots; " + fuses + "; elevator: " + cleared + " props moved out of the way; " + nav + "; " + guests + " party guests" + report;
        }

        // ------------------------------------------------------------------ the exit elevator and its fuse cells
        // the cabin (moved down from the old floor 4) stands in the locker room's south-east corner, doors facing west
        static readonly Rect ElevatorRect = Rect.MinMaxRect(139.9f, 4.4f, 144f, 7.6f);

        static bool NearElevator(Vector3 p, float margin)
        {
            return p.x > ElevatorRect.xMin - margin - 1.5f && p.x < ElevatorRect.xMax + margin && p.z > ElevatorRect.yMin - margin && p.z < ElevatorRect.yMax + margin;
        }

        // lockers and party things standing where the cabin (and its doorway) now is
        static int ClearElevator()
        {
            int n = 0;
            foreach (var t in f2.GetComponentsInChildren<Transform>(true))
            {
                if (t == f2 || !t.gameObject.activeSelf || t.parent == null) continue;
                if (t.name.StartsWith("Floor_2_ExitElevator") || t.name == "Exit_Trigger_Zone" || t.IsChildOf(f2.Find("Floor_2_ExitElevator") ?? f2)) continue;
                var pn = t.parent.name;
                if (pn != "Floor_2_Party" && !pn.StartsWith("Party_") && !pn.StartsWith("Room_")) continue;
                var r = t.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                var b = r.bounds;
                if (b.max.y < FloorY + 0.05f || b.min.y > FloorY + 3f) continue;
                if (b.max.x > ElevatorRect.xMin - 1.2f && b.min.x < ElevatorRect.xMax && b.max.z > ElevatorRect.yMin && b.min.z < ElevatorRect.yMax) { Deactivate(t.gameObject); n++; }
            }
            return n;
        }

        static string FuseCells(GameObject root, Random rnd)
        {
            var plan = LevelLayout.Plans[1];
            Room dark = null, library = null;
            foreach (var room in plan.rooms)
            {
                if (room.name.Contains("Darkroom")) dark = room;
                if (room.name.Contains("Library")) library = room;
            }
            var cells = new List<PartyItem>();
            string where = "";

            // the photo darkroom: its lights are dead, and the cell only shows in P4's torch beam
            if (dark != null)
            {
                var rr = RoomRect(dark);
                var zone = new GameObject("DarkZone_" + dark.name).AddComponent<DarkZone>();
                zone.transform.SetParent(root.transform, false);
                zone.rect = rr; zone.floorY = FloorY;
                int lamps = 0;
                foreach (var l in f2.Find("Lighting_Fixtures").GetComponentsInChildren<Light>(true))
                {
                    Vector3 p = l.transform.position;
                    if (!rr.Contains(new Vector2(p.x, p.z))) continue;
                    Deactivate(l.gameObject);
                    var tube = l.transform.parent != null ? l.transform.parent.Find("Fluorescent_Tube") : null;
                    if (tube != null) Deactivate(tube.gameObject);
                    lamps++;
                }
                Vector3 at, facing;
                if (WallSpot(rr, rnd, 0.65f, 1.5f, out at, out facing) || BenchSpot(rr, rnd, Vector2.zero, out at, out facing))
                {
                    cells.Add(FuseOnBench(root.transform, "FuseCell_Darkroom", at, facing, true));
                    where += "darkroom (" + lamps + " lamps dead)";
                }
            }
            // the library: on a reading desk just past the hidden pit, so P2 has to talk them round it
            if (library != null)
            {
                var rr = RoomRect(library);
                Vector2 pit = Vector2.zero;
                foreach (var s in Pits) if (s.name == "Hidden_Library") pit = s.rect.center;
                Vector3 best = Vector3.zero, bestFacing = Vector3.forward; float bd = float.MaxValue;
                for (int i = 0; i < 12; i++)
                {
                    Vector3 at, facing;
                    if (!WallSpot(rr, rnd, 0.65f, 1.5f, out at, out facing)) continue;
                    float d = Vector2.Distance(new Vector2(at.x, at.z), pit);
                    if (d > 3.2f && d < bd) { bd = d; best = at; bestFacing = facing; }
                }
                if (bd == float.MaxValue)
                {
                    Vector3 at, facing;
                    if (BenchSpot(rr, rnd, pit, out at, out facing)) { best = at; bestFacing = facing; bd = Vector2.Distance(new Vector2(at.x, at.z), pit); }
                }
                if (bd < float.MaxValue)
                {
                    cells.Add(FuseOnBench(root.transform, "FuseCell_Library", best, bestFacing, false));
                    where += (where.Length > 0 ? ", " : "") + "library (" + bd.ToString("0.0") + " m from the hidden pit)";
                }
            }

            var obj = root.AddComponent<FloorObjective>();
            obj.floorIndex = 1;
            obj.cells = cells.ToArray();
            obj.elevator = f2.GetComponentInChildren<TowerExitElevator>(true);
            return cells.Count + " fuse cells: " + where + (obj.elevator == null ? " (NO ELEVATOR FOUND)" : "");
        }

        // somewhere on open floor with room for the bench and to stand at it (near "near", if given)
        static bool BenchSpot(Rect rr, Random rnd, Vector2 near, out Vector3 at, out Vector3 facing)
        {
            at = Vector3.zero; facing = Vector3.forward;
            float bestD = float.MaxValue; bool found = false;
            for (int i = 0; i < 300; i++)
            {
                var p = new Vector3(rr.xMin + 1f + (float)rnd.NextDouble() * (rr.width - 2f), FloorY + 2.5f, rr.yMin + 1f + (float)rnd.NextDouble() * (rr.height - 2f));
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - FloorY) > 0.1f) continue;
                if (OverHole(hit.point, 1.2f) || NearElevator(hit.point, 1f)) continue;
                var f = Quaternion.Euler(0f, rnd.Next(4) * 90f, 0f) * Vector3.forward;
                if (Physics.CheckBox(hit.point + Vector3.up * 0.6f, new Vector3(0.8f, 0.5f, 0.9f), Quaternion.LookRotation(f), ~0, QueryTriggerInteraction.Ignore)) continue;
                float d = near == Vector2.zero ? 0f : Mathf.Abs(Vector2.Distance(new Vector2(hit.point.x, hit.point.z), near) - 4f);
                if (d < bestD) { bestD = d; at = hit.point; facing = f; found = true; if (near == Vector2.zero) break; }
            }
            return found;
        }

        static PartyItem FuseOnBench(Transform parent, string name, Vector3 at, Vector3 facing, bool needsTorch)
        {
            var bench = new GameObject(name + "_Bench").transform;
            bench.SetParent(parent, false);
            bench.position = at;
            bench.rotation = Quaternion.LookRotation(facing);
            var steel = Mat("Mat_Hunt_BenchSteel", new Color(0.4f, 0.42f, 0.43f), 0.75f, 0.45f);
            var top = Cube(bench, "Top", at + Vector3.up * 0.86f, new Vector3(1.4f, 0.05f, 0.6f), steel);
            top.transform.rotation = bench.rotation;
            top.AddComponent<BoxCollider>();
            foreach (var lx in new[] { -0.65f, 0.65f })
                foreach (var lz in new[] { -0.25f, 0.25f })
                    Cube(bench, "Leg", at + bench.rotation * new Vector3(lx, 0.42f, lz), new Vector3(0.05f, 0.84f, 0.05f), steel).transform.rotation = bench.rotation;

            var fuse = new GameObject(name);
            fuse.transform.SetParent(parent, false);
            fuse.transform.position = at + Vector3.up * 0.885f + bench.rotation * new Vector3(0.3f, 0f, 0f);
            GameObject model = null;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/FuseCell/SM_FuseCell.fbx");
            var fuseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Gameplay/Mat_FuseCell.mat");
            if (src != null)
            {
                model = Object.Instantiate(src);
                model.name = "FuseCell_Model";
                model.transform.SetParent(fuse.transform, false);
                if (fuseMat != null) foreach (var r in model.GetComponentsInChildren<Renderer>()) { var ms = new Material[r.sharedMaterials.Length]; for (int i = 0; i < ms.Length; i++) ms[i] = fuseMat; r.sharedMaterials = ms; }
                var b = PropLibrary.RendererBounds(model);
                model.transform.localScale *= 0.3f / Mathf.Max(0.001f, b.size.y);
                b = PropLibrary.RendererBounds(model);
                model.transform.position += fuse.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            }
            var col = fuse.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.17f, 0f);
            col.size = new Vector3(0.45f, 0.4f, 0.45f);
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(fuse.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.3f, 1f, 0.45f); l.range = 3.2f; l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
            var item = fuse.AddComponent<PartyItem>();
            item.itemId = "fuse_cell"; item.displayName = "Fuse cell"; item.needsTorch = needsTorch;
            item.glowLight = l;
            item.glowRenderers = model != null ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];
            return item;
        }

        // ------------------------------------------------------------------ broken floors
        static bool BuildPit(Transform root, PitSpec p, Random rnd, System.Text.StringBuilder report)
        {
            var r = p.rect;
            var door = DoorIn(r);
            if (door != null)
            {
                report.Append("\n  skipped " + p.name + ": a door at " + door.transform.position.ToString("0.0"));
                return false;
            }
            var g = new GameObject("Pit_" + p.name).transform;
            g.SetParent(root, false);

            // floor 2's slabs over the hole: cut away (hidden: left drawn but not solid), the rest of each cell rebuilt
            foreach (var slab in Slabs(f2, "FloorSlab", r))
            {
                var cell = CellOf(slab);
                var floorMat = slab.GetComponent<Renderer>().sharedMaterial;
                if (p.hidden)
                {
                    Disable(slab.GetComponent<Collider>());
                    Perceive(slab.gameObject, PartyPerception.Rule.HiddenFrom, true);
                }
                else Deactivate(slab.gameObject);
                foreach (var piece in Minus(cell, r))
                {
                    var go = WorldBox(g, "Floor", new Vector3(piece.center.x, FloorY - 0.1f, piece.center.y), new Vector3(piece.width, 0.2f, piece.height), floorMat, true);
                    if (p.hidden) Perceive(go, PartyPerception.Rule.VisibleOnlyTo, false);
                }
            }
            // floor 1's ceiling under it gets the same hole
            foreach (var slab in Slabs(f1, "CeilingSlab", r))
            {
                var cell = CellOf(slab);
                var ceilMat = slab.GetComponent<Renderer>().sharedMaterial;
                Deactivate(slab.gameObject);
                foreach (var piece in Minus(cell, r))
                    WorldBox(g, "Floor1Ceiling", new Vector3(piece.center.x, F1CeilY, piece.center.y), new Vector3(piece.width, 0.05f, piece.height), ceilMat, true);
            }
            // splintered board ends sticking out over the edge, and a dark rim (the floor's thickness)
            var edge = new GameObject("Edge").transform;
            edge.SetParent(g, false);
            Rim(edge, r);
            for (int i = 0; i < (int)((r.width + r.height) * 2.2f); i++)
            {
                float u = (float)rnd.NextDouble();
                int side = rnd.Next(4);
                Vector3 at; Vector3 into;
                if (side == 0) { at = new Vector3(Mathf.Lerp(r.xMin, r.xMax, u), FloorY - 0.04f, r.yMin); into = Vector3.forward; }
                else if (side == 1) { at = new Vector3(Mathf.Lerp(r.xMin, r.xMax, u), FloorY - 0.04f, r.yMax); into = Vector3.back; }
                else if (side == 2) { at = new Vector3(r.xMin, FloorY - 0.04f, Mathf.Lerp(r.yMin, r.yMax, u)); into = Vector3.right; }
                else { at = new Vector3(r.xMax, FloorY - 0.04f, Mathf.Lerp(r.yMin, r.yMax, u)); into = Vector3.left; }
                float len = 0.12f + (float)rnd.NextDouble() * 0.35f;
                var s = Cube(edge, "Splinter", at + into * len * 0.5f, new Vector3(0.11f, 0.035f, len), (i % 3 == 0) ? darkMat : plankMat);
                s.transform.rotation = Quaternion.LookRotation(into) * Quaternion.Euler((float)rnd.NextDouble() * 14f, ((float)rnd.NextDouble() - 0.5f) * 25f, 0f);
            }
            if (p.hidden) Perceive(edge.gameObject, PartyPerception.Rule.VisibleOnlyTo, false);

            // the collapsed corridor: a walkway of loose planks across it, which only P2 can see (everyone can walk it)
            if (p.bridge)
            {
                var bridge = new GameObject("Walkway_P2");
                bridge.transform.SetParent(g, false);
                float z = r.center.y;
                for (float x = r.xMin - 0.25f; x < r.xMax + 0.2f; x += 0.3f)
                    Cube(bridge.transform, "Plank", new Vector3(x, FloorY - 0.03f, z + ((float)rnd.NextDouble() - 0.5f) * 0.05f), new Vector3(0.26f, 0.05f, 1.05f), plankMat)
                        .transform.rotation = Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 5f, 0f);
                foreach (float side in new[] { -0.45f, 0.45f })
                    Cube(bridge.transform, "Joist", new Vector3(r.center.x, FloorY - 0.11f, z + side), new Vector3(r.width + 0.8f, 0.1f, 0.08f), darkMat);
                var col = bridge.AddComponent<BoxCollider>();
                col.center = new Vector3(r.center.x, FloorY - 0.05f, z);
                col.size = new Vector3(r.width + 0.6f, 0.1f, 1.0f);
                Perceive(bridge, PartyPerception.Rule.VisibleOnlyTo, false);
            }

            // anything of the party or the classroom standing over the hole would hang in the air: take it away
            int cleared = 0;
            foreach (var t in f2.GetComponentsInChildren<Transform>(true))
            {
                if (t == f2 || t.IsChildOf(root) || !t.gameObject.activeSelf) continue;
                if (t.name.StartsWith("FloorSlab") || t.name.StartsWith("CeilingSlab") || t.parent == null) continue;
                var pn = t.parent.name;
                if (pn != "Floor_2_Party" && !pn.StartsWith("Party_") && !pn.StartsWith("Room_")) continue;
                var rr = t.GetComponentInChildren<Renderer>();
                if (rr == null) continue;
                var b = rr.bounds;
                if (b.min.y > FloorY + 1.2f || b.max.y < FloorY - 0.1f) continue;   // ceiling balloons, wall boards stay
                if (b.center.x > r.xMin - 0.2f && b.center.x < r.xMax + 0.2f && b.center.z > r.yMin - 0.2f && b.center.z < r.yMax + 0.2f) { Deactivate(t.gameObject); cleared++; }
            }
            if (cleared > 0) report.Append("\n  " + p.name + ": moved " + cleared + " props off the hole");
            return true;
        }

        static InteractiveDoor DoorIn(Rect r)
        {
            foreach (var d in f2.GetComponentsInChildren<InteractiveDoor>(true))
            {
                Vector3 dp = d.transform.position;
                if (dp.x > r.xMin - 0.8f && dp.x < r.xMax + 0.8f && dp.y > 3.5f && dp.y < 8f && dp.z > r.yMin - 0.8f && dp.z < r.yMax + 0.8f) return d;
            }
            return null;
        }

        static void Rim(Transform parent, Rect r)
        {
            const float t = 0.04f, h = 0.2f;
            float y = FloorY - h * 0.5f;
            Cube(parent, "Rim", new Vector3(r.center.x, y, r.yMin + t * 0.5f), new Vector3(r.width, h, t), darkMat);
            Cube(parent, "Rim", new Vector3(r.center.x, y, r.yMax - t * 0.5f), new Vector3(r.width, h, t), darkMat);
            Cube(parent, "Rim", new Vector3(r.xMin + t * 0.5f, y, r.center.y), new Vector3(t, h, r.height), darkMat);
            Cube(parent, "Rim", new Vector3(r.xMax - t * 0.5f, y, r.center.y), new Vector3(t, h, r.height), darkMat);
        }

        static List<Transform> Slabs(Transform floor, string prefix, Rect r)
        {
            var list = new List<Transform>();
            foreach (Transform s in floor.Find("Slabs_And_Ceilings"))
            {
                if (!s.name.StartsWith(prefix) || !s.gameObject.activeSelf) continue;
                var c = CellOf(s);
                if (c.Overlaps(r)) list.Add(s);
            }
            return list;
        }

        static Rect CellOf(Transform slab)
        {
            var b = slab.GetComponent<Renderer>().bounds;
            return Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
        }

        // the parts of cell c outside hole h (up to four strips)
        static List<Rect> Minus(Rect c, Rect h)
        {
            var o = new List<Rect>();
            var i = Rect.MinMaxRect(Mathf.Max(c.xMin, h.xMin), Mathf.Max(c.yMin, h.yMin), Mathf.Min(c.xMax, h.xMax), Mathf.Min(c.yMax, h.yMax));
            if (i.width <= 0f || i.height <= 0f) { o.Add(c); return o; }
            if (i.yMin > c.yMin + 0.01f) o.Add(Rect.MinMaxRect(c.xMin, c.yMin, c.xMax, i.yMin));
            if (i.yMax < c.yMax - 0.01f) o.Add(Rect.MinMaxRect(c.xMin, i.yMax, c.xMax, c.yMax));
            if (i.xMin > c.xMin + 0.01f) o.Add(Rect.MinMaxRect(c.xMin, i.yMin, i.xMin, i.yMax));
            if (i.xMax < c.xMax - 0.01f) o.Add(Rect.MinMaxRect(i.xMax, i.yMin, c.xMax, i.yMax));
            return o;
        }

        // ------------------------------------------------------------------ hiding spots
        static int HideSpots(Transform root, Random rnd)
        {
            int n = 0;
            var dressing = f2.Find("College_Dressing");
            var taken = new List<Vector3>();
            foreach (var t in dressing.GetComponentsInChildren<Transform>(true))
            {
                string label = null;
                if (t.name == "Lockers") label = "locker";
                else if (t.name.StartsWith("vintage_cabinet_01") && t.name.Length <= 22 && t.Find("vintage_cabinet_01_body") != null) label = "cabinet";
                else if (t.name == "GothicCabinet_01") label = "wardrobe";
                if (label == null || !t.gameObject.activeInHierarchy) continue;
                if (MakeSpot(t.gameObject, label, true)) { n++; taken.Add(t.position); }
            }

            // a cupboard in every party room that has nowhere to hide yet
            var cupboardSrc = FindFirst(dressing, "vintage_cabinet_01");
            var plan = LevelLayout.Plans[1];
            foreach (var room in plan.rooms)
            {
                if (room == plan.corridor) continue;
                var rr = RoomRect(room);
                if (rr.width < 6f || rr.height < 6f) continue;
                bool has = false;
                foreach (var p in taken) if (rr.Contains(new Vector2(p.x, p.z))) { has = true; break; }
                if (has || cupboardSrc == null) continue;
                Vector3 at, facing;
                if (!WallSpot(rr, rnd, 0.75f, 2.1f, out at, out facing)) continue;
                var go = Object.Instantiate(cupboardSrc, root);
                go.name = "HideCupboard_" + room.name;
                go.transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Inverse(Quaternion.LookRotation(FrontOf(cupboardSrc))) * cupboardSrc.transform.rotation;
                Physics.SyncTransforms();
                var b = Bounds(go);
                go.transform.position += new Vector3(at.x - b.center.x, FloorY - b.min.y, at.z - b.center.z);
                Physics.SyncTransforms();
                if (MakeSpot(go, "cupboard", false)) { n++; taken.Add(go.transform.position); }
            }
            return n;
        }

        static GameObject FindFirst(Transform under, string prefix)
        {
            foreach (var t in under.GetComponentsInChildren<Transform>(true))
                if (t.name == prefix && t.Find(prefix + "_body") != null) return t.gameObject;
            return null;
        }

        // which way a cabinet faces in its own pose: the side its doors are on (the thin axis, toward the open room)
        static Vector3 FrontOf(GameObject go)
        {
            var b = Bounds(go);
            Vector3 axis = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            return OpenSide(b, axis);
        }

        static Vector3 OpenSide(Bounds b, Vector3 axis)
        {
            float half = Vector3.Scale(b.extents, axis).magnitude;
            float best = -1f; Vector3 dir = axis;
            foreach (var s in new[] { axis, -axis })
            {
                Vector3 from = new Vector3(b.center.x, FloorY + 1.0f, b.center.z) + s * (half + 0.05f);
                RaycastHit hit;
                float d = Physics.Raycast(from, s, out hit, 3f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 3f;
                if (d > best) { best = d; dir = s; }
            }
            return dir;
        }

        static bool MakeSpot(GameObject go, string label, bool record)
        {
            var b = Bounds(go);
            if (b.size.y < 1.6f) return false;
            Vector3 axis = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            Vector3 front = OpenSide(b, axis);
            var spot = go.AddComponent<PartyHideSpot>();
            spot.label = label;
            spot.inside = new Vector3(b.center.x, FloorY, b.center.z);
            spot.facing = front;
            spot.eyeHeight = Mathf.Min(1.55f, b.size.y - 0.3f);
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var c = go.AddComponent<BoxCollider>();
                c.center = go.transform.InverseTransformPoint(b.center);
                var size = go.transform.InverseTransformVector(b.size);   // a turned cabinet flips an axis negative
                c.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                if (record) rec.addedComponents.Add(c);
            }
            if (record) rec.addedComponents.Add(spot);
            return true;
        }

        static bool WallSpot(Rect rr, Random rnd, float depth, float width, out Vector3 at, out Vector3 facing)
        {
            at = Vector3.zero; facing = Vector3.forward;
            for (int tries = 0; tries < 60; tries++)
            {
                int side = rnd.Next(4);
                float u = 0.15f + (float)rnd.NextDouble() * 0.7f;
                float d = depth * 0.5f + 0.06f;
                switch (side)
                {
                    case 0: at = new Vector3(Mathf.Lerp(rr.xMin, rr.xMax, u), FloorY, rr.yMin + d); facing = Vector3.forward; break;
                    case 1: at = new Vector3(Mathf.Lerp(rr.xMin, rr.xMax, u), FloorY, rr.yMax - d); facing = Vector3.back; break;
                    case 2: at = new Vector3(rr.xMin + d, FloorY, Mathf.Lerp(rr.yMin, rr.yMax, u)); facing = Vector3.right; break;
                    default: at = new Vector3(rr.xMax - d, FloorY, Mathf.Lerp(rr.yMin, rr.yMax, u)); facing = Vector3.left; break;
                }
                Vector3 across = Vector3.Cross(Vector3.up, facing);
                var half = new Vector3(width * 0.5f, 1.0f, depth * 0.5f + 0.9f);   // its footprint plus room to get in
                var centre = at + Vector3.up * 1.15f + facing * 0.45f;
                if (Physics.CheckBox(centre, half, Quaternion.LookRotation(facing), ~0, QueryTriggerInteraction.Ignore)) continue;
                bool doorNear = false;
                foreach (var dr in f2.GetComponentsInChildren<InteractiveDoor>(true))
                    if (Vector3.Distance(new Vector3(dr.transform.position.x, FloorY, dr.transform.position.z), at) < 2.2f) { doorNear = true; break; }
                if (doorNear || OverHole(at, 1.2f) || NearElevator(at, 1f)) continue;
                return true;
            }
            return false;
        }

        static bool OverHole(Vector3 p, float margin)
        {
            foreach (var s in Pits)
                if (p.x > s.rect.xMin - margin && p.x < s.rect.xMax + margin && p.z > s.rect.yMin - margin && p.z < s.rect.yMax + margin) return true;
            return false;
        }

        // ------------------------------------------------------------------ navmesh
        static string BakeNavMesh(GameObject root, PlayerParty party)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (var d in f2.GetComponentsInChildren<InteractiveDoor>(true))
                if (!d.isLocked) markups.Add(new NavMeshBuildMarkup { root = d.transform, ignoreFromBuild = true });
            foreach (var p in party.players) if (p != null) markups.Add(new NavMeshBuildMarkup { root = p.transform, ignoreFromBuild = true });
            // floor 2 only: from floor 1's ceiling up to just under floor 2's ceiling
            var bounds = new Bounds(new Vector3(72f, 6.0f, 48f), new Vector3(150f, 4.2f, 100f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.32f;
            settings.agentHeight = 1.3f;
            settings.agentClimb = 0.35f;
            settings.agentSlope = 40f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return "NavMesh bake FAILED";
            AssetDatabase.DeleteAsset(NavPath);
            AssetDatabase.CreateAsset(data, NavPath);
            root.AddComponent<NavMeshDataLoader>().data = data;
            // so the guests can be placed now (in the editor) on what was just baked
            NavMesh.RemoveAllNavMeshData();
            NavMesh.AddNavMeshData(data);
            return "NavMesh from " + sources.Count + " colliders";
        }

        // ------------------------------------------------------------------ the guests
        static int Guests(Transform root, Random rnd)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PartyMonsterImport.Fbx);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PartyMonsterImport.ControllerPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PartyMonsterImport.MaterialPath);
            var cone = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HollowTower/Generated/Party/PartyHatCone.asset");
            if (fbx == null || ctrl == null) return 0;
            var hats = new List<Material>();
            for (int i = 1; i <= 6; i++) { var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTower/Generated/Party/Mat_PartyCloth_" + i + ".mat"); if (m != null) hats.Add(m); }
            var balloons = new List<Material>();
            for (int i = 0; i < 8; i++) { var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTower/Generated/Party/Mat_Balloon_" + i + ".mat"); if (m != null) balloons.Add(m); }
            var stringMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTower/Generated/Party/Mat_PartyWhite.mat");

            PartyMonsterSpots.Clear();
            var g = new GameObject("Party_Guests").transform;
            g.SetParent(root, false);
            var plan = LevelLayout.Plans[1];
            var dressing = f2.Find("College_Dressing");
            int n = 0;
            PartyMonster.Role[] roles = { PartyMonster.Role.Chaser, PartyMonster.Role.Swarm, PartyMonster.Role.Interceptor, PartyMonster.Role.Swarm, PartyMonster.Role.Chaser, PartyMonster.Role.Swarm, PartyMonster.Role.Chaser, PartyMonster.Role.Interceptor };

            foreach (var room in plan.rooms)
            {
                if (room == plan.corridor) continue;
                switch (room.type) { case RoomType.Stair: case RoomType.StairLobby: case RoomType.Washroom: case RoomType.Janitor: case RoomType.Storage: continue; }
                var rr = RoomRect(room);
                if (rr.width * rr.height < 60f) continue;
                if (n >= 16) break;   // a party, not an army: the swarm comes from them all waking at once
                int count = 1;
                for (int k = 0; k < count; k++)
                {
                    var act = ActivityFor(room, k, rnd);
                    Vector3 at; Quaternion rot;
                    if (act == PartyMonster.Activity.Drawing ? !BoardSpot(dressing, rr, out at, out rot) && !OpenSpot(rr, rnd, out at, out rot) : !OpenSpot(rr, rnd, out at, out rot)) continue;
                    if (act == PartyMonster.Activity.Drawing && !NearBoard(dressing, at)) act = PartyMonster.Activity.Balloon;
                    Spawn(g, fbx, ctrl, mat, cone, hats, balloons, stringMat, at, rot, act, roles[n % roles.Length], rnd, n);
                    n++;
                }
            }
            // two in the corridors, swaying to the music where the halls cross
            foreach (var c in new[] { new Vector3(26f, FloorY, 74f), new Vector3(118f, FloorY, 26f) })
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(c, out hit, 1.5f, NavMesh.AllAreas)) continue;
                Spawn(g, fbx, ctrl, mat, cone, hats, balloons, stringMat, hit.position, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), PartyMonster.Activity.Swaying, roles[n % roles.Length], rnd, n);
                n++;
            }
            return n;
        }

        static PartyMonster.Activity ActivityFor(Room room, int k, Random rnd)
        {
            string name = room.name.ToLower();
            if (name.Contains("music")) return PartyMonster.Activity.Swaying;
            if (name.Contains("library") || name.Contains("archive")) return rnd.Next(2) == 0 ? PartyMonster.Activity.Sitting : PartyMonster.Activity.Toy;
            if (name.Contains("art")) return PartyMonster.Activity.Drawing;
            if (name.Contains("classroom")) return k == 0 ? PartyMonster.Activity.Drawing : (PartyMonster.Activity)(rnd.Next(2) == 0 ? 0 : 2);
            var pick = new[] { PartyMonster.Activity.Balloon, PartyMonster.Activity.Toy, PartyMonster.Activity.Sitting, PartyMonster.Activity.Toy, PartyMonster.Activity.Swaying };
            return pick[rnd.Next(pick.Length)];
        }

        static bool BoardSpot(Transform dressing, Rect rr, out Vector3 at, out Quaternion rot)
        {
            at = Vector3.zero; rot = Quaternion.identity;
            foreach (var t in dressing.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Blackboard" && t.name != "Whiteboard") continue;
                // boards hang on the wall line itself, just outside the room's inset rect
                if (t.position.x < rr.xMin - 0.6f || t.position.x > rr.xMax + 0.6f || t.position.z < rr.yMin - 0.6f || t.position.z > rr.yMax + 0.6f) continue;
                var r = t.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                var b = r.bounds;
                Vector3 n = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
                if (Vector3.Dot(new Vector3(rr.center.x - b.center.x, 0f, rr.center.y - b.center.z), n) < 0f) n = -n;
                for (float off = 0f; off < 1.2f; off += 0.3f)
                {
                    var p = new Vector3(b.center.x, FloorY, b.center.z) + n * 0.6f + Vector3.Cross(Vector3.up, n) * off * (off % 0.6f < 0.01f ? 1f : -1f);
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(p, out hit, 0.35f, NavMesh.AllAreas) && !OverHole(hit.position, 1f))
                    {
                        at = hit.position; rot = Quaternion.LookRotation(-n);
                        return true;
                    }
                }
            }
            return false;
        }

        static bool NearBoard(Transform dressing, Vector3 at)
        {
            foreach (var t in dressing.GetComponentsInChildren<Transform>(true))
                if ((t.name == "Blackboard" || t.name == "Whiteboard") && Vector3.Distance(new Vector3(t.position.x, FloorY, t.position.z), at) < 1.6f) return true;
            return false;
        }

        static bool OpenSpot(Rect rr, Random rnd, out Vector3 at, out Quaternion rot)
        {
            at = Vector3.zero; rot = Quaternion.identity;
            for (int i = 0; i < 80; i++)
            {
                var p = new Vector3(rr.xMin + 1f + (float)rnd.NextDouble() * (rr.width - 2f), FloorY, rr.yMin + 1f + (float)rnd.NextDouble() * (rr.height - 2f));
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(p, out hit, 0.3f, NavMesh.AllAreas) || OverHole(hit.position, 1.2f) || NearElevator(hit.position, 1.5f)) continue;
                // room to sit with its legs out, and nothing over its head
                if (Physics.CheckBox(hit.position + Vector3.up * 0.75f, new Vector3(0.55f, 0.6f, 0.55f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                bool crowded = false;
                foreach (var m in PartyMonsterSpots) if (Vector3.Distance(m, hit.position) < 3f) { crowded = true; break; }
                if (crowded) continue;
                at = hit.position;
                Vector3 toCentre = new Vector3(rr.center.x, at.y, rr.center.y) - at;
                rot = Quaternion.LookRotation(toCentre.sqrMagnitude > 0.1f ? toCentre : Vector3.forward) * Quaternion.Euler(0f, ((float)rnd.NextDouble() - 0.5f) * 90f, 0f);
                return true;
            }
            return false;
        }

        static readonly List<Vector3> PartyMonsterSpots = new List<Vector3>();

        static void Spawn(Transform parent, GameObject fbx, RuntimeAnimatorController ctrl, Material mat, Mesh cone, List<Material> hats, List<Material> balloons, Material stringMat,
                          Vector3 at, Quaternion rot, PartyMonster.Activity act, PartyMonster.Role role, Random rnd, int index)
        {
            PartyMonsterSpots.Add(at);
            float height = 0.95f + (float)rnd.NextDouble() * 0.3f;
            var go = new GameObject("PartyGuest_" + index + "_" + act);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, rot);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (mat != null) { var ms = new Material[smr.sharedMaterials.Length]; for (int i = 0; i < ms.Length; i++) ms[i] = mat; smr.sharedMaterials = ms; }
            // size it from the real skin (the bounds in the file are loose): feet on the floor, head top at height
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in baked.vertices) { float y = smr.transform.TransformPoint(v).y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            Object.DestroyImmediate(baked);
            float s = height / Mathf.Max(0.001f, maxY - minY);
            model.transform.localScale = Vector3.one * s;
            model.transform.position += Vector3.up * (at.y - (at.y + (minY - at.y) * s));
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(smr.localBounds.center, smr.localBounds.size * 1.5f);   // the sat-down pose reaches out of the file's box

            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Transform head = null, hand = null;
            foreach (var t in model.GetComponentsInChildren<Transform>()) { if (t.name == "head") head = t; if (t.name == "hand_R_end") hand = t; }

            // a party hat, a little crooked, on top of the big head
            if (head != null && cone != null && hats.Count > 0)
            {
                var hat = new GameObject("PartyHat");
                hat.AddComponent<MeshFilter>().sharedMesh = cone;
                hat.AddComponent<MeshRenderer>().sharedMaterial = hats[rnd.Next(hats.Count)];
                hat.transform.position = at + Vector3.up * height * 0.955f + rot * Vector3.forward * height * 0.02f;
                hat.transform.rotation = rot * Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 18f, 0f, ((float)rnd.NextDouble() - 0.5f) * 26f);
                hat.transform.localScale = new Vector3(0.15f, 0.24f, 0.15f) * (height / 1.1f);
                hat.transform.SetParent(head, true);
            }

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.28f; agent.height = height; agent.baseOffset = 0f;
            agent.angularSpeed = 600f; agent.acceleration = 26f; agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            agent.avoidancePriority = 30 + rnd.Next(40);
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

            var pm = go.AddComponent<PartyMonster>();
            pm.role = role;
            pm.activity = act;
            pm.height = height;
            pm.animator = anim;
            pm.renderers = go.GetComponentsInChildren<Renderer>();
            pm.runSpeed = role == PartyMonster.Role.Interceptor ? 5.8f : role == PartyMonster.Role.Swarm ? 5.0f : 5.4f;
            pm.stillTime = 3.2f + (float)rnd.NextDouble() * 0.8f;

            if (act == PartyMonster.Activity.Balloon && balloons.Count > 0)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.name = "Balloon";
                b.transform.SetParent(go.transform, false);
                b.transform.position = (hand != null ? hand.position : at + Vector3.up * height) + Vector3.up * 0.75f;
                b.transform.localScale = new Vector3(0.32f, 0.38f, 0.32f);
                b.GetComponent<Renderer>().sharedMaterial = balloons[rnd.Next(balloons.Count)];
                var str = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(str.GetComponent<Collider>());
                str.name = "BalloonString";
                str.transform.SetParent(go.transform, false);
                str.transform.localScale = new Vector3(0.006f, 0.7f, 0.006f);
                str.GetComponent<Renderer>().sharedMaterial = stringMat;
                pm.balloon = b.transform; pm.balloonString = str.transform;
            }
            if (act == PartyMonster.Activity.Toy)
            {
                // a toy on the floor between its feet: a ball, a stack of blocks or a little drum
                var toy = new GameObject("Toy").transform;
                toy.SetParent(parent, false);
                toy.position = at + rot * Vector3.forward * height * 0.62f;
                int kind = rnd.Next(3);
                var c = balloons.Count > 0 ? balloons[rnd.Next(balloons.Count)] : toyMat;
                if (kind == 0) Prim(PrimitiveType.Sphere, toy, Vector3.up * 0.11f, Vector3.one * 0.22f, c);
                else if (kind == 1)
                    for (int i = 0; i < 3; i++) Prim(PrimitiveType.Cube, toy, new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.04f, 0.06f + i * 0.12f, 0f), Vector3.one * 0.12f, balloons.Count > 0 ? balloons[(index + i) % balloons.Count] : toyMat)
                        .transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 40f, 0f);
                else { Prim(PrimitiveType.Cylinder, toy, Vector3.up * 0.09f, new Vector3(0.26f, 0.09f, 0.26f), c); Prim(PrimitiveType.Cylinder, toy, Vector3.up * 0.185f, new Vector3(0.24f, 0.005f, 0.24f), toyMat); }
                pm.toy = toy;
            }
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        // ------------------------------------------------------------------ helpers
        static Rect RoomRect(Room room)
        {
            var R = room.rect;
            return Rect.MinMaxRect(R.x * 4f + 0.2f, (24 - R.y - R.height) * 4f + 0.2f, (R.x + R.width) * 4f - 0.2f, (24 - R.y) * 4f - 0.2f);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void Deactivate(GameObject go) { if (go != null && go.activeSelf) { go.SetActive(false); rec.deactivated.Add(go); } }
        static void Disable(Collider c) { if (c != null && c.enabled) { c.enabled = false; rec.disabledColliders.Add(c); } }

        static void Perceive(GameObject go, PartyPerception.Rule rule, bool record)
        {
            var p = go.AddComponent<PartyPerception>();
            p.rule = rule; p.player = 1; p.syncColliders = false;   // P2 sees the floor as it really is
            if (record) rec.addedComponents.Add(p);
        }

        static Material Mat(string name, Color c, float metal, float smooth)
        {
            string path = MeshDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A box with UVs from world position / 4 m, so a strip cut from a 4 m slab keeps the slab's texture scale.</summary>
        static GameObject WorldBox(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var mesh = new Mesh { name = name + "_" + (meshCount++) };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 h = size * 0.5f;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nn in normals)
            {
                Vector3 a = Mathf.Abs(nn.y) > 0.5f ? Vector3.right : (Mathf.Abs(nn.x) > 0.5f ? Vector3.forward : Vector3.right);
                Vector3 b = Mathf.Abs(nn.y) > 0.5f ? Vector3.forward : Vector3.up;
                if (Vector3.Dot(Vector3.Cross(a, b), nn) < 0f) a = -a;
                int baseIndex = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float su = (k == 1 || k == 2) ? 1f : -1f, sv = k >= 2 ? 1f : -1f;
                    Vector3 p = Vector3.Scale(nn, h) + Vector3.Scale(a * su, h) + Vector3.Scale(b * sv, h);
                    v.Add(p); n.Add(nn);
                    Vector3 w = center + p;
                    uv.Add(new Vector2(Vector3.Dot(w, a.x + a.y + a.z > 0 ? a : -a) / 4f, Vector3.Dot(w, b) / 4f));
                }
                t.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 });
            }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshDir + "/" + mesh.name + ".asset");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) go.AddComponent<BoxCollider>();
            go.isStatic = true;
            return go;
        }
    }
}
