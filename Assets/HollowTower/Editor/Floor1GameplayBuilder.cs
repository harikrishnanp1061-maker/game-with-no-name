using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Builds floor 1's co-op obstacles from Floor1Design: real pit shafts (with the hidden ones covered by the original
    /// floor slab that only P2 sees through), P2-only stepping tiles and girder, rubble barricades, P1's false walls,
    /// jammed doors, the dark Cold Storage with the fuse cell, and the stairwell fuse socket. Everything it changes outside
    /// its own Floor_1_Gameplay object is written to a Floor1BuildRecord, and a rebuild first puts all of that back,
    /// so it can be run as often as needed. Menu: Hollow Tower > Floor 1 > Build Co-op Obstacles.
    /// </summary>
    public static class Floor1GameplayBuilder
    {
        const string RootName = "Floor_1_Gameplay";
        const string MeshDir = "Assets/HollowTowerRemastered/Props/Generated/Floor1";
        const string MatDir = "Assets/HollowTowerRemastered/Materials/Gameplay";
        const float ShaftBottom = -4.2f;

        static Floor1BuildRecord rec;
        static Transform floorRoot;
        static Material floorMat, wallMat, concrete, steel, wood, dark;
        static int meshCount;

        [MenuItem("Hollow Tower/Floor 1/Build Co-op Obstacles")]
        public static void BuildMenu() { Debug.Log("[Floor1] " + Build(true)); }

        [MenuItem("Hollow Tower/Floor 1/Remove Co-op Obstacles")]
        public static void RemoveMenu() { Revert(CollegeDresser.FloorRoot(0)); EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); }

        public static string Build(bool redress)
        {
            floorRoot = CollegeDresser.FloorRoot(0);
            if (floorRoot == null) return "Floor_1_Ground not found";
            Revert(floorRoot);
            string dressed = redress ? CollegeDresser.DressFloor(0, 20261023) : "kept dressing";

            EnsureFolder(MeshDir); EnsureFolder(MatDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            meshCount = 0;
            LoadMaterials();

            var root = new GameObject(RootName);
            root.transform.SetParent(floorRoot, false);
            rec = root.AddComponent<Floor1BuildRecord>();

            var pits = root.AddComponent<PitHazards>();
            pits.floorY = 0f;
            int holes = 0;
            foreach (var p in Floor1Design.Pits) holes += BuildPit(root.transform, p, pits);

            var rnd = new Random(1717);
            foreach (var b in Floor1Design.Barricades) BuildBarricade(root.transform, b, rnd);

            int walls = 0;
            foreach (var w in Floor1Design.SecretWalls) if (BuildSecretWall(root.transform, w)) walls++;

            int jams = 0;
            foreach (var j in Floor1Design.Jams)
            {
                var door = FindDoor(j.door);
                if (door == null) { Debug.LogWarning("[Floor1] no door at " + j.door); continue; }
                Lock(door, j.message);
                if (j.boards) BoardUp(root.transform, door, rnd);
                jams++;
            }

            var stairDoor = FindDoor(Floor1Design.StairDoor);
            if (stairDoor != null) Lock(stairDoor, Floor1Design.StairLockedMessage);
            var socket = BuildSocket(root.transform, stairDoor);
            var fuse = BuildFuse(root.transform);
            int darkLamps = DarkenRoom(root.transform, Floor1Design.DarkRoom);

            var stairs = new GameObject("Stairs_Target").transform;
            stairs.SetParent(root.transform, false);
            stairs.position = new Vector3(14f, 1f, 84f);
            var obj = root.AddComponent<FloorObjective>();
            obj.fuse = fuse; obj.socket = socket; obj.stairs = stairs;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return dressed + "\n" + Floor1Design.Pits.Count + " pits (" + holes + " holes), " + Floor1Design.Barricades.Length + " barricades, " + walls + " false walls, " +
                   jams + " jammed doors, stair door " + (stairDoor != null ? "locked" : "MISSING") + ", fuse + socket, " + darkLamps + " lamps dimmed";
        }

        // ------------------------------------------------------------------ undo
        public static void Revert(Transform floor)
        {
            if (floor == null) return;
            var old = floor.Find(RootName);
            if (old == null) return;
            var r = old.GetComponent<Floor1BuildRecord>();
            if (r != null)
            {
                foreach (var go in r.deactivated) if (go != null) go.SetActive(true);
                foreach (var c in r.disabledColliders) if (c != null) c.enabled = true;
                foreach (var rr in r.disabledRenderers) if (rr != null) rr.enabled = true;
                foreach (var c in r.addedComponents) if (c != null) Object.DestroyImmediate(c);
                for (int i = 0; i < r.lockedDoors.Count; i++)
                    if (r.lockedDoors[i] != null)
                    {
                        r.lockedDoors[i].isLocked = false;
                        r.lockedDoors[i].lockedMessage = i < r.doorMessages.Count ? r.doorMessages[i] : "LOCKED - KEY OR FUSE REQUIRED";
                    }
            }
            Object.DestroyImmediate(old.gameObject);
        }

        static void Deactivate(GameObject go) { if (go != null && go.activeSelf) { go.SetActive(false); rec.deactivated.Add(go); } }
        static void DisableCollider(Collider c) { if (c != null && c.enabled) { c.enabled = false; rec.disabledColliders.Add(c); } }

        static void Lock(InteractiveDoor door, string message)
        {
            rec.lockedDoors.Add(door);
            rec.doorMessages.Add(door.lockedMessage);
            door.isLocked = true;
            door.isOpen = false;
            door.lockedMessage = message;
            EditorUtility.SetDirty(door);
        }

        // ------------------------------------------------------------------ materials and meshes
        static void LoadMaterials()
        {
            var slab = FindSlab(new Vector2Int(10, 6));
            floorMat = slab != null ? slab.GetComponent<Renderer>().sharedMaterial : null;
            var wall = FindWall(new Vector3(48f, 2f, 38f), false);
            wallMat = wall != null ? wall.GetComponent<Renderer>().sharedMaterial : null;
            concrete = Load("Assets/HollowTowerRemastered/Materials/Mat_Remastered_Concrete044.mat") ?? Mat("Mat_PitConcrete", new Color(0.32f, 0.31f, 0.3f), 0f, 0.15f);
            steel = Load("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_FrameDarkSteel.mat") ?? Mat("Mat_Girder", new Color(0.2f, 0.21f, 0.22f), 0.8f, 0.35f);
            wood = Load("Assets/HollowTowerRemastered/Materials/Mat_Remastered_AgedWood.mat") ?? Mat("Mat_Planks", new Color(0.4f, 0.3f, 0.2f), 0f, 0.2f);
            dark = Mat("Mat_PitDark", new Color(0.035f, 0.035f, 0.04f), 0f, 0.1f);
        }

        static Material Load(string path) { return AssetDatabase.LoadAssetAtPath<Material>(path); }

        static Material Mat(string name, Color c, float metal, float smooth, Color? emission = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                // URP keeps _EMISSION on only for materials flagged emissive (BaseShaderGUI re-derives the keyword)
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        /// <summary>
        /// A box with UVs taken from world position / 4 m (the floor and wall textures repeat once per 4 m cell), so a
        /// piece cut out of a floor slab or a wall shows the texture at the same scale as the slab it came from.
        /// </summary>
        static GameObject WorldBox(Transform parent, string name, Vector3 center, Vector3 size, Material m, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var mesh = new Mesh { name = name + "_" + (meshCount++) };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 h = size * 0.5f;
            // each face: normal, two in-plane axes (u, v)
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nn in normals)
            {
                Vector3 a = Mathf.Abs(nn.y) > 0.5f ? Vector3.right : (Mathf.Abs(nn.x) > 0.5f ? Vector3.forward : Vector3.right);
                Vector3 b = Mathf.Abs(nn.y) > 0.5f ? Vector3.forward : Vector3.up;
                if (Vector3.Dot(Vector3.Cross(a, b), nn) < 0f) a = -a;   // keep the winding facing out
                int baseIndex = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float su = (k == 1 || k == 2) ? 1f : -1f, sv = k >= 2 ? 1f : -1f;
                    Vector3 p = Vector3.Scale(nn, h) + Vector3.Scale(a * su, h) + Vector3.Scale(b * sv, h);
                    v.Add(p); n.Add(nn);
                    Vector3 w = center + p;
                    float u = Vector3.Dot(w, a.x + a.y + a.z > 0 ? a : -a), vv = Vector3.Dot(w, b);
                    uv.Add(new Vector2(u / 4f, vv / 4f));
                }
                t.AddRange(new[] { baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3 });   // clockwise seen from outside
            }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshDir + "/" + mesh.name + ".asset");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        static GameObject Cube(Transform parent, string name, Vector3 center, Vector3 size, Material m, Vector3 euler, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static PartyPerception Perceive(GameObject go, PartyPerception.Rule rule, int player)
        {
            var p = go.AddComponent<PartyPerception>();
            p.rule = rule; p.player = player; p.syncColliders = false;
            return p;
        }

        // ------------------------------------------------------------------ scene lookups
        static GameObject FindSlab(Vector2Int cell)
        {
            var slabs = floorRoot.Find("Slabs_And_Ceilings");
            var c = Floor1Design.CellRect(cell.x, cell.y).center;
            foreach (Transform s in slabs)
                if (s.name.StartsWith("FloorSlab") && Mathf.Abs(s.position.x - c.x) < 0.1f && Mathf.Abs(s.position.z - c.y) < 0.1f) return s.gameObject;
            return null;
        }

        static GameObject FindWall(Vector3 centre, bool alongX, string prefix = "Wall_")
        {
            var walls = floorRoot.Find("Walls_And_Doors");
            string name = prefix + (alongX ? "H" : "V");
            foreach (Transform w in walls)
                if (w.name.StartsWith(name) && Mathf.Abs(w.position.x - centre.x) < 0.1f && Mathf.Abs(w.position.z - centre.z) < 0.1f) return w.gameObject;
            return null;
        }

        static InteractiveDoor FindDoor(Vector2 at)
        {
            foreach (var d in floorRoot.GetComponentsInChildren<InteractiveDoor>(true))
            {
                var lintel = d.transform.parent != null ? d.transform.parent.Find("Door_Lintel") : null;
                var p = lintel != null ? lintel.position : d.transform.position;
                if (Mathf.Abs(p.x - at.x) < 1.2f && Mathf.Abs(p.z - at.y) < 1.2f) return d;
            }
            return null;
        }

        // ------------------------------------------------------------------ pits
        static int BuildPit(Transform root, Floor1Design.Pit pit, PitHazards hazards)
        {
            var g = new GameObject("Pit_" + pit.name).transform;
            g.SetParent(root, false);

            foreach (var cell in pit.cells)
            {
                var slab = FindSlab(cell);
                if (slab == null) continue;
                if (pit.hidden)
                {
                    // the original slab stays as the fake floor: everyone but P2 sees it, nobody can stand on it
                    DisableCollider(slab.GetComponent<Collider>());
                    rec.addedComponents.Add(Perceive(slab, PartyPerception.Rule.HiddenFrom, 1));
                }
                else Deactivate(slab);
            }

            // floor left standing inside the pit: P2-only over a hidden pit, plain to see over an open one
            foreach (var s in pit.solid)
            {
                var tile = WorldBox(g, "Tile", new Vector3(s.center.x, -0.1f, s.center.y), new Vector3(s.width - 0.02f, 0.2f, s.height - 0.02f), floorMat);
                if (pit.hidden) Perceive(tile, PartyPerception.Rule.VisibleOnlyTo, 1);
                // a pillar under each tile holds it up out of the shaft
                Cube(g, "Pillar", new Vector3(s.center.x, (ShaftBottom - 0.2f) * 0.5f, s.center.y), new Vector3(Mathf.Min(0.35f, s.width * 0.4f), -ShaftBottom - 0.2f, Mathf.Min(0.35f, s.height * 0.4f)), concrete, Vector3.zero);
            }

            // the shaft: walls just outside the dug-out area, a floor of rubble and rebar at the bottom
            var r = pit.shaft;
            float hgt = -ShaftBottom - 0.2f, yMid = (ShaftBottom - 0.2f) * 0.5f;
            Cube(g, "ShaftWall_W", new Vector3(r.xMin - 0.15f, yMid, r.center.y), new Vector3(0.3f, hgt, r.height + 0.6f), concrete, Vector3.zero);
            Cube(g, "ShaftWall_E", new Vector3(r.xMax + 0.15f, yMid, r.center.y), new Vector3(0.3f, hgt, r.height + 0.6f), concrete, Vector3.zero);
            Cube(g, "ShaftWall_S", new Vector3(r.center.x, yMid, r.yMin - 0.15f), new Vector3(r.width, hgt, 0.3f), concrete, Vector3.zero);
            Cube(g, "ShaftWall_N", new Vector3(r.center.x, yMid, r.yMax + 0.15f), new Vector3(r.width, hgt, 0.3f), concrete, Vector3.zero);
            Cube(g, "ShaftFloor", new Vector3(r.center.x, ShaftBottom - 0.1f, r.center.y), new Vector3(r.width + 0.6f, 0.2f, r.height + 0.6f), dark, Vector3.zero);
            var rnd = new Random(pit.name.GetHashCode());
            int chunks = Mathf.RoundToInt(r.width * r.height * 0.9f);
            for (int i = 0; i < chunks; i++)
            {
                float s = (float)(0.15 + rnd.NextDouble() * 0.45);
                Cube(g, "Rubble", new Vector3(r.xMin + (float)rnd.NextDouble() * r.width, ShaftBottom + s * 0.3f, r.yMin + (float)rnd.NextDouble() * r.height),
                     new Vector3(s, s * 0.6f, s * (float)(0.7 + rnd.NextDouble() * 0.6)), concrete, new Vector3((float)rnd.NextDouble() * 40f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 40f));
            }
            for (int i = 0; i < chunks / 2; i++)
            {
                float len = (float)(0.6 + rnd.NextDouble() * 0.9);
                var bar = Cube(g, "Rebar", Vector3.zero, new Vector3(0.025f, len, 0.025f), steel, new Vector3((float)(rnd.NextDouble() - 0.5) * 30f, 0f, (float)(rnd.NextDouble() - 0.5) * 30f));
                bar.transform.position = new Vector3(r.xMin + 0.2f + (float)rnd.NextDouble() * (r.width - 0.4f), ShaftBottom + len * 0.45f, r.yMin + 0.2f + (float)rnd.NextDouble() * (r.height - 0.4f));
            }

            // the chasm's girder: only P2 sees it (with the chasm), everyone can walk on it
            if (pit.name == "Chasm")
            {
                var gir = new GameObject("Girder_P2");
                gir.transform.SetParent(g, false);
                float x0 = r.xMin - 0.35f, x1 = r.xMax + 0.35f, z = Floor1Design.GirderZ, w = Floor1Design.GirderHalfWidth * 2f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float bz = z + side * (w * 0.5f - 0.08f);
                    Cube(gir.transform, "BeamTop", new Vector3((x0 + x1) * 0.5f, -0.05f, bz), new Vector3(x1 - x0, 0.025f, 0.16f), steel, Vector3.zero);
                    Cube(gir.transform, "BeamWeb", new Vector3((x0 + x1) * 0.5f, -0.25f, bz), new Vector3(x1 - x0, 0.4f, 0.025f), steel, Vector3.zero);
                    Cube(gir.transform, "BeamBottom", new Vector3((x0 + x1) * 0.5f, -0.45f, bz), new Vector3(x1 - x0, 0.025f, 0.16f), steel, Vector3.zero);
                }
                var prnd = new System.Random(5);
                for (float px = x0 + 0.15f; px < x1 - 0.1f; px += 0.32f)
                    Cube(gir.transform, "Plank", new Vector3(px, 0.0f, z + (float)(prnd.NextDouble() - 0.5) * 0.04f), new Vector3(0.28f, 0.05f, w + 0.04f), wood, new Vector3(0f, (float)(prnd.NextDouble() - 0.5) * 4f, 0f));
                var col = gir.AddComponent<BoxCollider>();
                col.center = new Vector3((r.xMin + r.xMax) * 0.5f, -0.1f, z);
                col.size = new Vector3(r.width + 0.2f, 0.26f, w);
                Perceive(gir, PartyPerception.Rule.VisibleOnlyTo, 1);
            }

            foreach (var h in pit.holes) hazards.holes.Add(new PitHazards.Hole { rect = h.rect, hidden = h.hidden });
            return pit.holes.Count;
        }

        // ------------------------------------------------------------------ barricades
        static void BuildBarricade(Transform root, Vector2Int cell, Random rnd)
        {
            var r = Floor1Design.CellRect(cell.x, cell.y);
            var g = new GameObject("Barricade_" + cell.x + "_" + cell.y);
            g.transform.SetParent(root, false);
            g.transform.position = new Vector3(r.center.x, 0f, r.center.y);
            var blocker = g.AddComponent<BoxCollider>();   // the whole cell, wall to wall and floor to ceiling
            blocker.center = new Vector3(0f, 2f, 0f);
            blocker.size = new Vector3(r.width, 4f, r.height);

            // a heap of concrete built up in layers: wall to wall on the floor, narrowing towards a peak near the ceiling
            for (int layer = 0; layer < 6; layer++)
            {
                float ext = 1.9f - layer * 0.28f, y0 = layer * 0.4f;
                for (int i = 0, n = 18 - layer * 2; i < n; i++)
                {
                    float px = (float)(rnd.NextDouble() * 2 - 1) * ext, pz = (float)(rnd.NextDouble() * 2 - 1) * ext;
                    float s = (float)(0.55 + rnd.NextDouble() * 0.75) * (1f - layer * 0.08f);
                    Cube(g.transform, "Chunk", g.transform.position + new Vector3(px, y0 + s * 0.22f, pz), new Vector3(s, s * 0.5f, s * (float)(0.6 + rnd.NextDouble() * 0.7)), concrete,
                         new Vector3((float)rnd.NextDouble() * 40f - 20f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 40f - 20f));
                }
            }
            // fallen ceiling panels and a beam leaning from the ceiling down onto the heap
            for (int i = 0; i < 2; i++)
                Cube(g.transform, "CeilingPanel", g.transform.position + new Vector3((float)(rnd.NextDouble() - 0.5) * 1.5f, 1.4f + i * 0.5f, (float)(rnd.NextDouble() - 0.5) * 1.5f),
                     new Vector3(3.2f, 0.12f, 1.4f), concrete, new Vector3((float)(rnd.NextDouble() - 0.5) * 60f, (float)rnd.NextDouble() * 180f, (float)(rnd.NextDouble() - 0.5) * 40f));
            Cube(g.transform, "FallenBeam", g.transform.position + new Vector3(0.3f, 2.2f, 0f), new Vector3(0.3f, 4.6f, 0.22f), steel, new Vector3(0f, (float)rnd.NextDouble() * 180f, 48f));
            for (int i = 0; i < 3; i++)
            {
                float len = (float)(0.8 + rnd.NextDouble() * 1.4);
                Cube(g.transform, "Cable", g.transform.position + new Vector3((float)(rnd.NextDouble() - 0.5) * 3f, 3.9f - len * 0.5f, (float)(rnd.NextDouble() - 0.5) * 3f),
                     new Vector3(0.02f, len, 0.02f), dark, new Vector3((float)(rnd.NextDouble() - 0.5) * 20f, 0f, (float)(rnd.NextDouble() - 0.5) * 20f));
            }
        }

        // ------------------------------------------------------------------ false walls (P1)
        static bool BuildSecretWall(Transform root, Floor1Design.SecretWall sw)
        {
            var wall = FindWall(sw.wallCentre, sw.alongX);
            if (wall == null) { Debug.LogWarning("[Floor1] no wall at " + sw.wallCentre); return false; }
            var baseboard = FindWall(sw.wallCentre, sw.alongX, "Baseboard_");
            Vector3 c = new Vector3(sw.wallCentre.x, 0f, sw.wallCentre.z);
            Vector3 along = sw.alongX ? Vector3.right : Vector3.forward;   // direction the wall runs
            Vector3 across = sw.alongX ? Vector3.forward : Vector3.right;  // through the wall
            const float W = 4f, T = 0.25f, open = 1.8f, openH = 2.6f;

            var g = new GameObject(sw.name);
            g.transform.SetParent(root, false);
            g.transform.position = c;
            var door = g.AddComponent<HiddenWallDoor>();
            door.seer = 0;
            door.hideOnOpen = baseboard != null ? new[] { wall, baseboard } : new[] { wall };
            var trigger = g.AddComponent<BoxCollider>();   // lets the interactor find the false wall through the plain wall
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, openH * 0.5f, 0f);
            trigger.size = Vector3.Scale(along, new Vector3(open, 0, open)) + Vector3.Scale(across, new Vector3(0.7f, 0, 0.7f)) + Vector3.up * openH;

            // doorway left once it's open: posts either side and a lintel above, cut to the wall's texture scale
            var frame = new GameObject("OpenFrame");
            frame.transform.SetParent(g.transform, false);
            float post = (W - open) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 pc = c + along * side * (open * 0.5f + post * 0.5f) + Vector3.up * 2f;
                WorldBox(frame.transform, "Post", pc, Abs(along * post + across * T) + Vector3.up * 4f, wallMat);
                Cube(frame.transform, "Jamb", c + along * side * (open * 0.5f + 0.05f) + Vector3.up * openH * 0.5f, Abs(along * 0.1f + across * 0.33f) + Vector3.up * openH, steel, Vector3.zero);
            }
            WorldBox(frame.transform, "Lintel", c + Vector3.up * (openH + (4f - openH) * 0.5f), Abs(along * open + across * T) + Vector3.up * (4f - openH), wallMat);
            Cube(frame.transform, "Header", c + Vector3.up * (openH + 0.06f), Abs(along * (open + 0.15f) + across * 0.34f) + Vector3.up * 0.12f, steel, Vector3.zero);
            frame.SetActive(false);
            door.openFrame = frame;

            // the panel that drops into the floor when pushed
            var panel = WorldBox(g.transform, "Panel", c + Vector3.up * openH * 0.5f, Abs(along * open + across * T) + Vector3.up * openH, wallMat);
            panel.SetActive(false);
            door.panel = panel.transform;

            // what P1 sees: the doorway painted bright orange on both faces of the wall
            var glow = Mat("Mat_FalseWallOrange", new Color(1f, 0.42f, 0.05f), 0f, 0.3f, new Color(1.2f, 0.45f, 0.05f));
            var outline = new GameObject("Outline_P1");
            outline.transform.SetParent(g.transform, false);
            foreach (float face in new[] { -1f, 1f })
                Cube(outline.transform, "Paint", c + across * face * (T * 0.5f + 0.006f) + Vector3.up * openH * 0.5f, Abs(along * open + across * 0.01f) + Vector3.up * openH, glow, Vector3.zero);
            Perceive(outline, PartyPerception.Rule.VisibleOnlyTo, 0);
            door.outline = outline;
            return true;
        }

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        // ------------------------------------------------------------------ jammed doors
        static void BoardUp(Transform root, InteractiveDoor door, Random rnd)
        {
            var lintel = door.transform.parent.Find("Door_Lintel");
            Vector3 c = lintel != null ? new Vector3(lintel.position.x, 0f, lintel.position.z) : door.transform.position;
            bool vertical = door.transform.parent.name.StartsWith("Doorway_V");   // wall runs along z
            Vector3 along = vertical ? Vector3.forward : Vector3.right, across = vertical ? Vector3.right : Vector3.forward;
            var g = new GameObject("Boards");
            g.transform.SetParent(root, false);
            foreach (float face in new[] { -1f, 1f })
            {
                float[] heights = { 0.55f, 1.25f, 1.95f };
                for (int i = 0; i < heights.Length; i++)
                {
                    float tilt = (float)(rnd.NextDouble() - 0.5) * 22f;
                    var b = Cube(g.transform, "Plank", c + across * face * 0.21f + Vector3.up * heights[i], new Vector3(2.15f, 0.17f, 0.035f), wood, Vector3.zero);
                    b.transform.rotation = Quaternion.LookRotation(across) * Quaternion.Euler(0f, 0f, tilt);
                }
            }
        }

        // ------------------------------------------------------------------ fuse socket at the stairwell
        static FuseSocket BuildSocket(Transform root, InteractiveDoor stairDoor)
        {
            var p = Floor1Design.SocketPosition;
            var g = new GameObject("Stairwell_FuseSocket");
            g.transform.SetParent(root, false);
            g.transform.position = p;
            var housing = Mat("Mat_SocketHousing", new Color(0.26f, 0.28f, 0.27f), 0.6f, 0.4f);
            Cube(g.transform, "Housing", p + new Vector3(0.08f, 0f, 0f), new Vector3(0.16f, 0.62f, 0.44f), housing, Vector3.zero);
            Cube(g.transform, "Slot", p + new Vector3(0.161f, -0.05f, 0f), new Vector3(0.01f, 0.3f, 0.16f), dark, Vector3.zero);
            Cube(g.transform, "Plate", p + new Vector3(0.161f, 0.22f, 0f), new Vector3(0.01f, 0.08f, 0.3f), Mat("Mat_SocketPlate", new Color(0.75f, 0.62f, 0.15f), 0.3f, 0.4f), Vector3.zero);
            var lamp = Cube(g.transform, "StatusLamp", p + new Vector3(0.17f, 0.36f, 0f), new Vector3(0.05f, 0.05f, 0.05f), Mat("Mat_SocketLamp", new Color(0.3f, 0.05f, 0.04f), 0f, 0.6f, Color.black), Vector3.zero);
            var lightGo = new GameObject("StatusLight");
            lightGo.transform.SetParent(g.transform, false);
            lightGo.transform.position = p + new Vector3(0.35f, 0.36f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 2.2f; l.intensity = 0.8f; l.shadows = LightShadows.None;

            var installed = FuseModel(g.transform, p + new Vector3(0.17f, -0.17f, 0f), 0.24f);
            if (installed != null) installed.SetActive(false);

            var col = g.AddComponent<BoxCollider>();
            col.center = new Vector3(0.1f, 0f, 0f);
            col.size = new Vector3(0.3f, 0.7f, 0.55f);
            var socket = g.AddComponent<FuseSocket>();
            socket.door = stairDoor;
            socket.statusLamp = lamp.GetComponent<Renderer>();
            socket.statusLight = l;
            socket.installedFuse = installed;
            return socket;
        }

        // ------------------------------------------------------------------ the fuse cell
        static GameObject FuseModel(Transform parent, Vector3 bottomCentre, float height, Vector3? euler = null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/FuseCell/SM_FuseCell.fbx");
            if (src == null) { Debug.LogWarning("[Floor1] SM_FuseCell.fbx missing"); return null; }
            var go = (GameObject)Object.Instantiate(src);
            go.name = "FuseCell_Model";
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            var mat = Mat("Mat_FuseCell", new Color(0.18f, 0.32f, 0.2f), 0.55f, 0.55f, Color.black);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            var b = PropLibrary.RendererBounds(go);
            float s = height / Mathf.Max(0.001f, b.size.y);
            go.transform.localScale *= s;
            b = PropLibrary.RendererBounds(go);
            go.transform.position += bottomCentre - new Vector3(b.center.x, b.min.y, b.center.z);
            return go;
        }

        static PartyItem BuildFuse(Transform root)
        {
            var p = Floor1Design.FusePosition;
            // a steel bench against the north wall of the cold store
            var bench = new GameObject("Fuse_Bench");
            bench.transform.SetParent(root, false);
            bench.transform.position = p;
            var top = Mat("Mat_BenchSteel", new Color(0.4f, 0.42f, 0.43f), 0.75f, 0.45f);
            Cube(bench.transform, "Top", p + new Vector3(0f, 0.86f, 0f), new Vector3(1.5f, 0.05f, 0.65f), top, Vector3.zero, true);
            foreach (var lx in new[] { -0.7f, 0.7f })
                foreach (var lz in new[] { -0.28f, 0.28f })
                    Cube(bench.transform, "Leg", p + new Vector3(lx, 0.42f, lz), new Vector3(0.05f, 0.84f, 0.05f), top, Vector3.zero);
            Cube(bench.transform, "Shelf", p + new Vector3(0f, 0.2f, 0f), new Vector3(1.45f, 0.03f, 0.6f), top, Vector3.zero);

            var fuse = new GameObject("FuseCell");
            fuse.transform.SetParent(root, false);
            fuse.transform.position = p + new Vector3(0.25f, 0.885f, 0.05f);
            var model = FuseModel(fuse.transform, fuse.transform.position, 0.3f);
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
            item.itemId = "fuse_cell"; item.displayName = "Fuse cell"; item.needsTorch = true;
            item.glowLight = l;
            item.glowRenderers = model != null ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];
            return item;
        }

        // ------------------------------------------------------------------ the dark room
        /// <summary>Counts the room's lamps; their dimming and flicker are applied at runtime (FloorLightMood).</summary>
        static int DarkenRoom(Transform root, Rect room)
        {
            int n = 0;
            foreach (var l in floorRoot.Find("Lighting_Fixtures").GetComponentsInChildren<Light>(true))
            {
                var p = l.transform.position;
                if (room.Contains(new Vector2(p.x, p.z))) n++;
            }
            return n;
        }
    }
}
