using System.Collections.Generic;
using HollowTower.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Floor 2's three in-world puzzles (each one of a kind in the game), each guarding a step to the exit:
    ///   - the library's secret bookcase (east wall): pull the four books the clue card names, in order - the fuse cell
    ///     is in the niche behind;
    ///   - the darkroom's light trap: turn mirrors on stands to bounce the enlarger's beam onto the print safe's
    ///     photocell - the other fuse cell is in the safe;
    ///   - the music room's floor piano: wind the music box and step its tune back on the pads, three rounds - it frees
    ///     the exit lift's override.
    /// Props in the way are hidden (and put back on a rebuild). Built from primitives, saved in the tower scene.
    /// </summary>
    public static class Floor2Puzzles
    {
        const string RootName = "Floor_2_Puzzles";
        const string MatDir = "Assets/HollowTowerRemastered/Materials/Gameplay";
        const float Y = LevelBuilder.Story + 0.2f;   // floor 2's floor surface (the slab's top)
        static Transform floor, root;
        static PuzzleBuildRecord rec;

        [MenuItem("Hollow Tower/Floor 2/Build Puzzles (bookcase, mirrors, floor piano)")]
        public static void Menu() { Debug.Log("[Floor2Puzzles] " + Build()); }

        public static string Build()
        {
            floor = CollegeDresser.FloorRoot(1);
            if (floor == null) return "Floor_2_School not found";
            var old = floor.Find(RootName);
            if (old != null)
            {
                var oldRec = old.GetComponent<PuzzleBuildRecord>();
                if (oldRec != null) foreach (var g in oldRec.hidden) if (g != null) g.SetActive(true);
                Object.DestroyImmediate(old.gameObject);
            }
            root = new GameObject(RootName).transform; root.SetParent(floor, false);
            rec = root.gameObject.AddComponent<PuzzleBuildRecord>();
            Physics.SyncTransforms();
            string a = Bookcase(), b = Mirrors(), c = Piano();
            EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
            return a + "; " + b + "; " + c;
        }

        // ------------------------------------------------------------------ helpers
        static Material Mat(string name, Color c, float smooth = 0.3f, float metal = 0f, Color? emission = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Cube(string n, Transform p, Vector3 localPos, Vector3 size, Material m, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = n;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false); go.transform.localPosition = localPos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Cyl(string n, Transform p, Vector3 localPos, float r, float h, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = n;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(p, false); go.transform.localPosition = localPos + Vector3.up * h * 0.5f; go.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Hides the room dressing standing in a box (world), recorded for the next rebuild.</summary>
        static int Hide(Bounds zone)
        {
            int n = 0;
            foreach (var gn in new[] { "College_Dressing", "Floor_2_Party" })
            {
                var g = floor.Find(gn);
                if (g == null) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>(false))
                {
                    if (!zone.Intersects(r.bounds) || r.bounds.size.x > 7f || r.bounds.size.z > 7f) continue;
                    var t = r.transform;
                    while (t.parent != null && t.parent != g && t.parent.parent != g) t = t.parent;
                    if (t.parent == g && t.position == Vector3.zero) continue;   // a room container
                    if (!t.gameObject.activeSelf) continue;
                    if (t.GetComponentInChildren<PartyMonster>() != null || t.GetComponentInChildren<PartyHideSpot>() != null) continue;
                    t.gameObject.SetActive(false); rec.hidden.Add(t.gameObject); n++;
                }
            }
            return n;
        }

        /// <summary>The rooms' litter is merged per material: what lay on a hidden shelf would float. Cut those triangles
        /// (off the floor, inside the zone) from a saved copy of the mesh.</summary>
        static int TrimLitter(Bounds zone)
        {
            int n = 0;
            var cd = floor.Find("College_Dressing");
            if (cd == null) return 0;
            foreach (var mf in cd.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.transform.parent == null || mf.transform.parent.name != "Litter_Merged") continue;
                var src = mf.sharedMesh;
                if (src == null || !src.isReadable) continue;
                var v = src.vertices; var tris = src.triangles; var keep = new List<int>(); var m = mf.transform.localToWorldMatrix; int cut = 0;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 c = m.MultiplyPoint3x4((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                    if (zone.Contains(c) && c.y > Y + 0.35f) { cut++; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                if (cut == 0) continue;
                var copy = Object.Instantiate(src); copy.name = src.name + "_PuzzleTrim"; copy.SetTriangles(keep, 0);
                string path = "Assets/HollowTowerRemastered/Generated/" + mf.transform.parent.parent.name + "_" + mf.name + "_ptrim.asset";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(copy, path);
                mf.sharedMesh = copy; n++;
            }
            return n;
        }

        static GameObject Find(string name)
        {
            foreach (var t in floor.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.gameObject;
            return null;
        }

        static TextMesh Text(Transform p, string text, Vector3 world, Vector3 facing, float height, Color c)
        {
            var go = CampusKit.Text(p, text, Vector3.zero, facing, height, c);
            go.transform.position = world;
            return go.GetComponent<TextMesh>();
        }

        /// <summary>The top of whatever stands at (x, z) on floor 2.</summary>
        static float Surface(float x, float z)
        {
            RaycastHit h;
            if (Physics.Raycast(new Vector3(x, Y + 2.5f, z), Vector3.down, out h, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return h.point.y;
            return Y;
        }

        // ------------------------------------------------------------------ the library's secret bookcase
        static string Bookcase()
        {
            var zone = new Bounds(new Vector3(142.3f, Y + 1.2f, 54f), new Vector3(3.4f, 2.4f, 3.8f));
            int hidden = Hide(zone) + TrimLitter(zone);
            var wood = Mat("Mat_PuzzleCaseWood", new Color(0.24f, 0.15f, 0.09f), 0.25f);
            var dull = new[] { Mat("Mat_Book_Brown", new Color(0.3f, 0.2f, 0.12f)), Mat("Mat_Book_Green", new Color(0.12f, 0.2f, 0.14f)), Mat("Mat_Book_Grey", new Color(0.22f, 0.22f, 0.22f)), Mat("Mat_Book_Wine", new Color(0.25f, 0.08f, 0.08f)) };
            string[] names = { "red", "blue", "green", "yellow", "purple", "orange", "white", "black" };
            Color[] cols = { new Color(0.85f, 0.08f, 0.06f), new Color(0.1f, 0.3f, 0.9f), new Color(0.1f, 0.7f, 0.2f), new Color(0.95f, 0.85f, 0.1f), new Color(0.5f, 0.15f, 0.65f), new Color(1f, 0.45f, 0.05f), new Color(0.92f, 0.92f, 0.9f), new Color(0.03f, 0.03f, 0.03f) };

            var puzzle = new GameObject("Library_SecretBookcase").AddComponent<BookshelfPuzzle>();
            puzzle.transform.SetParent(root, false);
            puzzle.transform.position = new Vector3(143.3f, Y, 52.8f);
            puzzle.title = "Library bookcase"; puzzle.solvedMessage = "The bookcase swings out from the wall - there's a fuse cell in the niche behind it!";
            // the case itself on a hinge at its back south corner; local x along the wall (+z world), local z out into the room
            var hinge = new GameObject("Case_Hinge").transform; hinge.SetParent(puzzle.transform, false); hinge.localRotation = Quaternion.Euler(0, -90, 0);
            const float W = 2.4f, D = 0.45f, H = 2.3f;
            Cube("Side", hinge, new Vector3(0.02f, H * 0.5f, D * 0.5f), new Vector3(0.04f, H, D), wood);
            Cube("Side", hinge, new Vector3(W - 0.02f, H * 0.5f, D * 0.5f), new Vector3(0.04f, H, D), wood);
            Cube("Back", hinge, new Vector3(W * 0.5f, H * 0.5f, 0.015f), new Vector3(W, H, 0.03f), wood);
            Cube("Top", hinge, new Vector3(W * 0.5f, H + 0.03f, D * 0.5f + 0.02f), new Vector3(W + 0.08f, 0.06f, D + 0.04f), wood);
            var block = hinge.gameObject.AddComponent<BoxCollider>(); block.center = new Vector3(W * 0.5f, H * 0.5f, D * 0.5f); block.size = new Vector3(W, H, D);
            float[] shelfY = { 0.08f, 0.62f, 1.16f, 1.7f };
            foreach (float sy in shelfY) Cube("Shelf", hinge, new Vector3(W * 0.5f, sy, D * 0.5f), new Vector3(W - 0.08f, 0.03f, D - 0.02f), wood);
            var rnd = new System.Random(77);
            var books = new List<Transform>(); var bookNames = new List<string>();
            // which shelf slot each bright book takes: spread out, two per shelf
            var special = new Dictionary<int, int>();   // slot index on its row -> colour
            int colour = 0;
            for (int row = 0; row < 4; row++)
            {
                float x = 0.06f; int slot = 0;
                int s1 = 3 + rnd.Next(6), s2 = s1 + 8 + rnd.Next(10);
                while (x < W - 0.12f)
                {
                    float bw = 0.035f + (float)rnd.NextDouble() * 0.04f, bh = 0.3f + (float)rnd.NextDouble() * 0.14f;
                    bool bright = (slot == s1 || slot == s2) && colour < names.Length;
                    var m = bright ? Mat("Mat_Book_" + names[colour], cols[colour], 0.45f) : dull[rnd.Next(dull.Length)];
                    var b = Cube(bright ? "Book_" + names[colour] : "Book", hinge, new Vector3(x + bw * 0.5f, shelfY[row] + 0.015f + bh * 0.5f, D * 0.5f + 0.02f), new Vector3(bw, bh, D - 0.12f), m, false);
                    if (!bright && rnd.NextDouble() < 0.08) b.transform.localRotation = Quaternion.Euler(0, 0, 6f);
                    if (bright)
                    {
                        var col = b.AddComponent<BoxCollider>(); col.size = new Vector3(1.6f, 1f, 1.3f); col.center = new Vector3(0, 0, 0.15f);   // a little proud of the others
                        var part = b.AddComponent<WorldPuzzlePart>(); part.puzzle = puzzle; part.index = books.Count;
                        books.Add(b.transform); bookNames.Add(names[colour]); colour++;
                    }
                    x += bw + 0.004f; slot++;
                }
            }
            puzzle.books = books.ToArray(); puzzle.colourNames = bookNames.ToArray();
            puzzle.bookcase = hinge;
            // the niche behind it, and the cell
            var niche = Mat("Mat_PuzzleNiche", new Color(0.06f, 0.05f, 0.05f));
            Cube("Niche_Back", puzzle.transform, new Vector3(0.52f, 1.0f, 1.2f), new Vector3(0.04f, 2f, 2.3f), niche);
            Cube("Niche_Shelf", puzzle.transform, new Vector3(0.28f, 0.95f, 1.2f), new Vector3(0.45f, 0.04f, 0.6f), wood);
            var cell = Find("FuseCell_Library");
            if (cell != null)
            {
                cell.transform.position = new Vector3(143.58f, Y + 0.98f, 54.0f);
                puzzle.reward = cell.GetComponent<PartyItem>();
                var bench = Find("FuseCell_Library_Bench"); if (bench != null) { bench.SetActive(false); rec.hidden.Add(bench); }
            }
            // the clue card on the librarian's counter
            float top = Surface(126f, 50f);
            var cardMat = Mat("Mat_PuzzleCard", new Color(0.88f, 0.84f, 0.72f), 0.1f);
            var card = Cube("Clue_Card", puzzle.transform, Vector3.zero, new Vector3(0.62f, 0.4f, 0.01f), cardMat);
            card.transform.position = new Vector3(126f, top + 0.2f, 50.05f); card.transform.rotation = Quaternion.Euler(-12, 0, 0);
            puzzle.clueCard = Text(puzzle.transform, "?", new Vector3(126f, top + 0.21f, 50.07f), Vector3.forward, 0.028f, new Color(0.12f, 0.08f, 0.05f));
            puzzle.clueCard.transform.rotation = Quaternion.Euler(-12, 180, 0);
            puzzle.clueCard.anchor = TextAnchor.MiddleCenter; puzzle.clueCard.characterSize = 0.0042f;
            Text(puzzle.transform, "LIBRARIAN - RETURNS", new Vector3(126f, top + 0.45f, 50.07f), Vector3.forward, 0.04f, new Color(0.75f, 0.7f, 0.6f)).transform.rotation = Quaternion.Euler(0, 180, 0);
            return "bookcase (" + books.Count + " bright books, " + hidden + " props moved off the wall)";
        }

        // ------------------------------------------------------------------ the darkroom's light trap
        static string Mirrors()
        {
            const float BeamH = 1.45f;
            var steel = Mat("Mat_PuzzleSteel", new Color(0.32f, 0.33f, 0.35f), 0.5f, 0.8f);
            var glass = Mat("Mat_PuzzleMirror", new Color(0.85f, 0.88f, 0.9f), 0.97f, 1f);
            var dark = Mat("Mat_PuzzleDark", new Color(0.05f, 0.05f, 0.05f), 0.3f);
            var puzzle = new GameObject("Darkroom_LightTrap").AddComponent<MirrorPuzzle>();
            puzzle.transform.SetParent(root, false); puzzle.transform.position = new Vector3(58f, Y, 38f);
            puzzle.title = "Darkroom light trap"; puzzle.solvedMessage = "The beam hits the photocell - the print safe clunks open. The fuse cell is inside!";
            // clear the beam's lanes and the stands' spots of anything tall
            int hidden = 0;
            foreach (var z in new[] { new Bounds(new Vector3(57f, Y + 1.45f, 37.5f), new Vector3(18f, 0.4f, 1.6f)), new Bounds(new Vector3(54.5f, Y + 1f, 39f), new Vector3(1.2f, 2f, 7.5f)),
                                      new Bounds(new Vector3(59f, Y + 1.45f, 42.5f), new Vector3(10f, 0.4f, 1.4f)), new Bounds(new Vector3(63.1f, Y + 1f, 36f), new Vector3(1f, 2f, 13f)) })
                hidden += Hide(z);
            var bench = Find("FuseCell_Darkroom_Bench"); if (bench != null) { bench.SetActive(false); rec.hidden.Add(bench); }

            // the enlarger on its column, its lamp shining east
            var enl = new GameObject("Enlarger").transform; enl.SetParent(puzzle.transform, false); enl.position = new Vector3(49.2f, Y, 37.5f);
            Cube("Base", enl, new Vector3(0, 0.03f, 0), new Vector3(0.6f, 0.06f, 0.6f), dark, true);
            Cube("Column", enl, new Vector3(-0.2f, 0.8f, 0), new Vector3(0.08f, 1.6f, 0.08f), steel);
            Cube("Head", enl, new Vector3(0f, BeamH, 0), new Vector3(0.4f, 0.32f, 0.3f), dark);
            Cyl("Lens", enl, new Vector3(0.22f, BeamH - 0.08f, 0), 0.07f, 0.16f, steel).transform.localRotation = Quaternion.Euler(0, 0, 90);
            var src = new GameObject("Beam_Source").transform; src.SetParent(enl, false); src.localPosition = new Vector3(0.32f, BeamH, 0); src.localRotation = Quaternion.LookRotation(Vector3.right);
            puzzle.source = src;
            var lineGo = new GameObject("Beam"); lineGo.transform.SetParent(puzzle.transform, false);
            var lr = lineGo.AddComponent<LineRenderer>();
            lr.widthMultiplier = 0.025f; lr.useWorldSpace = true; lr.numCapVertices = 2;
            var beamMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); beamMat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.85f));
            AssetDatabase.CreateAsset(beamMat, MatDir + "/Mat_PuzzleBeam.mat");
            lr.sharedMaterial = beamMat; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puzzle.beam = lr;
            var glow = new GameObject("Enlarger_Glow").AddComponent<Light>(); glow.transform.SetParent(enl, false); glow.transform.localPosition = new Vector3(0.6f, BeamH, 0);
            glow.type = LightType.Point; glow.range = 3f; glow.intensity = 3f; glow.color = new Color(1f, 0.95f, 0.8f);

            // the mirrors: three on the path (solution turns, in eighths), one decoy
            var spots = new[] { new Vector3(54.5f, 0, 37.5f), new Vector3(54.5f, 0, 42.5f), new Vector3(63.1f, 0, 42.5f), new Vector3(59f, 0, 38.6f) };
            var sol = new[] { 7, 3, 5, -1 };
            var mirrors = new List<Transform>();
            for (int i = 0; i < spots.Length; i++)
            {
                var stand = new GameObject("Mirror_Stand_" + i).transform; stand.SetParent(puzzle.transform, false); stand.position = spots[i] + Vector3.up * Y;
                Cyl("Foot", stand, Vector3.zero, 0.28f, 0.05f, dark);
                Cyl("Post", stand, Vector3.zero, 0.035f, BeamH - 0.25f, steel);
                var turn = new GameObject("Mirror").transform; turn.SetParent(stand, false); turn.localPosition = new Vector3(0, BeamH, 0);
                Cube("Frame", turn, new Vector3(0, 0, -0.02f), new Vector3(0.62f, 0.5f, 0.04f), dark);
                Cube("Glass", turn, new Vector3(0, 0, 0.002f), new Vector3(0.56f, 0.44f, 0.01f), glass);
                var surf = new GameObject("Mirror_Surface"); surf.transform.SetParent(turn, false);
                var sc = surf.AddComponent<BoxCollider>(); sc.size = new Vector3(0.56f, 0.44f, 0.02f); sc.isTrigger = true;
                var col = stand.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.7f, 0); col.size = new Vector3(0.5f, 1.4f, 0.5f);
                var part = stand.gameObject.AddComponent<WorldPuzzlePart>(); part.puzzle = puzzle; part.index = i;
                mirrors.Add(turn);
            }
            puzzle.mirrors = mirrors.ToArray(); puzzle.solution = sol;

            // the print safe in the south lane, its photocell up at the beam's height
            var safe = new GameObject("Print_Safe").transform; safe.SetParent(puzzle.transform, false); safe.position = new Vector3(63.1f, Y, 30.2f);
            Cube("Body", safe, new Vector3(0, 0.45f, 0), new Vector3(0.75f, 0.9f, 0.65f), steel, true);
            Text(safe, "PRINTS - LIGHT SENSITIVE", safe.position + new Vector3(0, 0.95f, 0.34f), Vector3.forward, 0.04f, new Color(0.8f, 0.75f, 0.5f));
            var lid = new GameObject("Safe_Door").transform; lid.SetParent(safe, false); lid.localPosition = new Vector3(-0.36f, 0.45f, 0.335f);
            Cube("Door", lid, new Vector3(0.36f, 0, 0), new Vector3(0.7f, 0.84f, 0.03f), Mat("Mat_PuzzleSafeDoor", new Color(0.4f, 0.4f, 0.42f), 0.5f, 0.7f));
            puzzle.rewardDoor = lid; puzzle.rewardDoorOpen = new Vector3(0, -110, 0);
            Cube("Cell_Post", safe, new Vector3(0, 0.9f + (BeamH - 0.9f) * 0.5f, 0), new Vector3(0.06f, BeamH - 0.9f, 0.06f), steel);
            var cell = Cube("Photocell", safe, new Vector3(0, BeamH, 0), new Vector3(0.18f, 0.18f, 0.18f), dark, false);
            var sensor = cell.AddComponent<BoxCollider>(); sensor.isTrigger = true; sensor.size = Vector3.one * 1.8f;
            puzzle.sensor = sensor;
            puzzle.sensorLamp = Cube("Photocell_Lamp", safe, new Vector3(0, BeamH + 0.12f, 0), new Vector3(0.08f, 0.05f, 0.08f), Mat("Mat_PuzzleLamp", Color.black, 0.3f, 0f, Color.black)).GetComponent<Renderer>();
            var fuse = Find("FuseCell_Darkroom");
            if (fuse != null) { fuse.transform.position = safe.position + new Vector3(0, 0.12f, 0.05f); puzzle.reward = fuse.GetComponent<PartyItem>(); }
            return "light trap (" + hidden + " props cleared from the beam's lanes)";
        }

        // ------------------------------------------------------------------ the music room's floor piano
        static string Piano()
        {
            int hidden = Hide(new Bounds(new Vector3(14.5f, Y + 0.8f, 13.6f), new Vector3(11.5f, 1.6f, 2.6f)));
            var puzzle = new GameObject("Party_FloorPiano").AddComponent<PianoPuzzle>();
            puzzle.transform.SetParent(root, false); puzzle.transform.position = new Vector3(14.5f, Y, 13.6f);
            puzzle.title = "Floor piano"; puzzle.solvedMessage = "The music box plays the whole birthday song... somewhere in the locker room the lift's override clicks free.";
            Color[] cols = { new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.55f, 0.08f), new Color(0.98f, 0.9f, 0.15f), new Color(0.2f, 0.85f, 0.25f), new Color(0.15f, 0.4f, 0.95f), new Color(0.6f, 0.2f, 0.85f) };
            string[] cn = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple" };
            var mat = Mat("Mat_PianoMat", new Color(0.08f, 0.08f, 0.08f), 0.4f);
            Cube("Piano_Mat", puzzle.transform, new Vector3(0, 0.01f, 0), new Vector3(10.4f, 0.02f, 2.1f), mat);
            var pads = new List<Renderer>();
            for (int i = 0; i < 6; i++)
            {
                var m = Mat("Mat_PianoPad_" + cn[i], cols[i] * 0.6f, 0.5f, 0f, Color.black);
                var pad = Cube("Pad_" + cn[i], puzzle.transform, new Vector3(-4.25f + i * 1.7f, 0.03f, 0), new Vector3(1.5f, 0.04f, 1.7f), m);
                var trig = pad.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.center = new Vector3(0, 12f, 0); trig.size = new Vector3(0.85f, 25f, 0.85f);
                var part = pad.AddComponent<WorldPuzzlePart>(); part.puzzle = puzzle; part.index = i; part.step = true;
                pads.Add(pad.GetComponent<Renderer>());
            }
            puzzle.pads = pads.ToArray(); puzzle.padColours = cols;
            // the music box on the party table
            float top = Surface(22f, 14f);
            var box = new GameObject("Music_Box").transform; box.SetParent(puzzle.transform, false); box.position = new Vector3(22f, top, 14f);
            var boxWood = Mat("Mat_MusicBox", new Color(0.55f, 0.18f, 0.25f), 0.5f);
            Cube("Box", box, new Vector3(0, 0.08f, 0), new Vector3(0.28f, 0.16f, 0.2f), boxWood);
            Cube("Lid", box, new Vector3(0, 0.17f, -0.09f), new Vector3(0.28f, 0.02f, 0.2f), boxWood).transform.localRotation = Quaternion.Euler(-55, 0, 0);
            Cyl("Ballerina", box, new Vector3(0, 0.16f, 0), 0.02f, 0.12f, Mat("Mat_PuzzleCard", new Color(0.88f, 0.84f, 0.72f), 0.1f));
            Cyl("Key", box, new Vector3(0.15f, 0.08f, 0), 0.015f, 0.08f, Mat("Mat_PuzzleSteel", new Color(0.32f, 0.33f, 0.35f), 0.5f, 0.8f)).transform.localRotation = Quaternion.Euler(0, 0, 90);
            var bc = box.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.12f, 0); bc.size = new Vector3(0.5f, 0.4f, 0.45f);
            var bp = box.gameObject.AddComponent<WorldPuzzlePart>(); bp.puzzle = puzzle; bp.index = PianoPuzzle.MusicBox;
            Text(puzzle.transform, "THE PARTY PIANO!", new Vector3(14.5f, Y + 0.02f, 15.2f), Vector3.up, 0.35f, new Color(1f, 0.9f, 0.4f)).transform.rotation = Quaternion.Euler(90, 0, 0);
            Text(puzzle.transform, "wind the music box - then dance its tune back!", new Vector3(14.5f, Y + 0.02f, 12.1f), Vector3.up, 0.16f, new Color(1f, 0.9f, 0.4f)).transform.rotation = Quaternion.Euler(90, 0, 0);
            return "floor piano (" + hidden + " props moved off the floor)";
        }
    }
}
