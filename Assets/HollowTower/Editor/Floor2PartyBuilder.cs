using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HollowTower
{
    /// <summary>
    /// Turns floor 2 (the school floor) into the children's party: floor 4's floor, wall and ceiling materials, and party
    /// dressing on top of the school rooms — balloons against the ceiling with their strings hanging, bunting, a
    /// lettered banner per room, party tables with cake and hats, piles of presents, and a bouncy castle in a big room.
    /// The light and music cycle is PartyFloor (runtime). Everything is under Floor_2_Party plus a record of the
    /// swapped materials, so Remove restores the floor exactly.
    /// </summary>
    public static partial class Floor2PartyBuilder
    {
        const string RootName = "Floor_2_Party";
        const string AssetDir = "Assets/HollowTower/Generated/Party";
        const float FloorY = 4.2f, CeilY = 8.15f;
        static readonly string[] Banners = { "HAPPY BIRTHDAY!", "PARTY TIME!", "WELCOME FRIENDS", "LET'S PLAY!", "HAPPY BIRTHDAY!", "FUN FUN FUN" };
        static readonly Color[] Palette =
        {
            new Color(0.95f, 0.15f, 0.2f), new Color(1f, 0.82f, 0.1f), new Color(0.15f, 0.45f, 0.95f), new Color(0.2f, 0.8f, 0.35f),
            new Color(1f, 0.45f, 0.75f), new Color(0.6f, 0.3f, 0.9f), new Color(1f, 0.55f, 0.1f), new Color(0.2f, 0.85f, 0.9f),
        };

        static Material[] balloon, flat;
        static Material textMat, stringMat, white, wood, cake, icing, flame, gold, castleBase;
        static Mesh pennant, cone;
        static Font font;

        [MenuItem("Hollow Tower/Floor 2/Build Party Floor")]
        public static void BuildMenu() { Debug.Log("[Floor2] " + Build()); }

        [MenuItem("Hollow Tower/Floor 2/Remove Party Floor")]
        public static void RemoveMenu() { Remove(); Debug.Log("[Floor2] party floor removed"); }

        static Transform FloorRoot()
        {
            var env = GameObject.Find("HollowTower_Remastered_Environment");
            return env != null ? env.transform.Find("Floor_2_School") : null;
        }

        public static void Remove()
        {
            var f2 = FloorRoot();
            if (f2 == null) return;
            Floor2HuntBuilder.Revert();
            var root = f2.Find(RootName);
            if (root == null) return;
            var rec = root.GetComponent<Floor2PartyRecord>();
            if (rec != null)
                for (int i = 0; i < rec.swappedRenderers.Count; i++)
                {
                    var r = rec.swappedRenderers[i];
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    mats[rec.slotIndex[i]] = rec.originalMaterials[i];
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                }
            Object.DestroyImmediate(root.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(f2.gameObject.scene);
        }

        public static string Build()
        {
            var f2 = FloorRoot();
            if (f2 == null) return "Floor_2_School not found";
            Floor2HuntBuilder.Revert();   // it broke slabs and moved props this build is about to redo
            Remove();
            LoadAssets();
            var root = new GameObject(RootName);
            root.transform.SetParent(f2, false);
            var rec = root.AddComponent<Floor2PartyRecord>();
            int swapped = Retexture(f2, rec);

            Physics.SyncTransforms();
            var rnd = new Random(2024);
            var plan = LevelLayout.Plans[1];
            int rooms = 0, tables = 0, presents = 0, banners = 0, balloons = 0;
            bool castle = false;

            // the bouncy castle goes in the biggest room that has the floor space for it
            var byArea = new List<Room>(plan.rooms);
            byArea.Sort((a, b) => (b.rect.width * b.rect.height).CompareTo(a.rect.width * a.rect.height));
            foreach (var room in byArea)
            {
                if (!Partyable(room) || room == plan.corridor) continue;
                Vector3 spot;
                if (FindFloorSpace(RoomRect(room), new Vector2(5.2f, 5.2f), 3.8f, rnd, 40, out spot)) { BouncyCastle(root.transform, spot, rnd); castle = true; break; }
            }

            // the carousel in the next big room that fits it, and circus-tent ceilings over the two biggest rooms
            bool carousel = false;
            int tents = 0;
            foreach (var room in byArea)
            {
                if (!Partyable(room) || room == plan.corridor) continue;
                var rr = RoomRect(room);
                if (tents < 2) { TentCeiling(root.transform, rr); tents++; }
                Vector3 spot;
                if (!carousel && FindFloorSpace(rr, new Vector2(5.6f, 5.6f), 3.6f, rnd, 40, out spot)) { Carousel(root.transform, spot, rnd); carousel = true; Physics.SyncTransforms(); }
                if (carousel && tents >= 2) break;
            }

            int arches = 0, pieces = 0;
            foreach (var room in plan.rooms)
            {
                if (room == plan.corridor || !Partyable(room)) continue;
                var g = new GameObject("Party_" + room.name).transform;
                g.SetParent(root.transform, false);
                var r = RoomRect(room);
                rooms++;
                RoomProbe(g, r);
                arches += DoorArches(g, room, plan, rnd);

                // one carnival piece per room, taking turns through the set
                Vector3 spot;
                {
                    float yaw = rnd.Next(4) * 90f;
                    switch (pieces % 6)
                    {
                        case 0: if (FindFloorSpace(r, new Vector2(2.9f, 2.4f), 3.1f, rnd, 30, out spot)) { GameBooth(g, spot, yaw, true, rnd); pieces++; } break;
                        case 1: if (FindFloorSpace(r, new Vector2(1.8f, 1.8f), 2.6f, rnd, 30, out spot)) { PopcornCart(g, spot, yaw, false, rnd); pieces++; } break;
                        case 2: if (FindFloorSpace(r, new Vector2(2.3f, 0.9f), 2.6f, rnd, 30, out spot)) { PrizeShelf(g, spot, yaw, rnd); pieces++; } break;
                        case 3: if (FindFloorSpace(r, new Vector2(2.9f, 2.4f), 3.1f, rnd, 30, out spot)) { GameBooth(g, spot, yaw, false, rnd); pieces++; } break;
                        case 4: if (FindFloorSpace(r, new Vector2(1.8f, 1.8f), 2.6f, rnd, 30, out spot)) { PopcornCart(g, spot, yaw, true, rnd); pieces++; } break;
                        default: if (FindFloorSpace(r, new Vector2(1.2f, 1.2f), 2.1f, rnd, 30, out spot)) { JackInTheBox(g, spot, rnd); pieces++; } break;
                    }
                    Physics.SyncTransforms();
                }

                // balloons bunched against the ceiling, strings hanging down
                int clusters = Mathf.Clamp(Mathf.RoundToInt(r.width * r.height / 45f), 2, 9);
                for (int i = 0; i < clusters; i++)
                    balloons += CeilingBalloons(g, new Vector3(Lerp(r.xMin + 1.2f, r.xMax - 1.2f, rnd), 0f, Lerp(r.yMin + 1.2f, r.yMax - 1.2f, rnd)), rnd);

                // two strands of bunting corner to corner
                Bunting(g, new Vector3(r.xMin + 0.3f, CeilY - 0.35f, r.yMin + 0.3f), new Vector3(r.xMax - 0.3f, CeilY - 0.35f, r.yMax - 0.3f), 0.9f, rnd);
                Bunting(g, new Vector3(r.xMin + 0.3f, CeilY - 0.35f, r.yMax - 0.3f), new Vector3(r.xMax - 0.3f, CeilY - 0.35f, r.yMin + 0.3f), 0.9f, rnd);

                // a lettered banner high on the north wall
                if (r.width >= 8f) { Banner(g, new Vector3(r.center.x, FloorY + 3.2f, r.yMax - 0.14f), Vector3.back, Banners[banners % Banners.Length], rnd); banners++; }

                if (r.width * r.height >= 80f && FindFloorSpace(r, new Vector2(2.8f, 1.6f), 1.3f, rnd, 30, out spot)) { PartyTable(g, spot, rnd); tables++; }
                if (FindFloorSpace(r, new Vector2(1.4f, 1.4f), 1.4f, rnd, 20, out spot)) { Presents(g, spot, rnd); presents++; }
            }

            // corridors stay sparse: bunting across every third cell, the odd pair of balloons
            int strands = 0;
            for (int row = 0; row < LevelLayout.Rows; row++)
                for (int col = 0; col < LevelLayout.Cols; col++)
                {
                    int id = plan.zone[row, col];
                    if (id < 0 || plan.rooms[id] != plan.corridor || (col + row) % 3 != 0) continue;
                    bool ns = IsCorridor(plan, col, row - 1) || IsCorridor(plan, col, row + 1);
                    bool ew = IsCorridor(plan, col - 1, row) || IsCorridor(plan, col + 1, row);
                    if (ns && ew) continue;   // crossings: leave them clear
                    var c = Floor1Design.CellRect(col, row).center;
                    Vector3 a = ew ? new Vector3(c.x, CeilY - 0.3f, c.y - 1.85f) : new Vector3(c.x - 1.85f, CeilY - 0.3f, c.y);
                    Vector3 b = ew ? new Vector3(c.x, CeilY - 0.3f, c.y + 1.85f) : new Vector3(c.x + 1.85f, CeilY - 0.3f, c.y);
                    Bunting(root.transform, a, b, 0.45f, rnd);
                    strands++;
                    if (rnd.NextDouble() < 0.3) balloons += CeilingBalloons(root.transform, new Vector3(c.x + Lerp(-1f, 1f, rnd), 0f, c.y + Lerp(-1f, 1f, rnd)), rnd, 2);
                }

            int foilProbes = FoilProbes(root.transform);
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = t.GetComponentInParent<PartySpinner>() == null;   // the carousel turns
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(f2.gameObject.scene);
            string hunt = Floor2HuntBuilder.Build();
            return "hunt: " + hunt + "\nparty floor: " + swapped + " surfaces retextured to floor 4, " + rooms + " rooms dressed, " + balloons + " balloons, " + banners + " banners, " +
                   tables + " party tables, " + presents + " present piles, " + strands + " corridor bunting strands, " + arches + " door arches, " + pieces + " carnival pieces, " + tents + " tent ceilings, carousel " + (carousel ? "placed" : "NO ROOM") + ", bouncy castle " + (castle ? "placed" : "NO ROOM") + ", " + foilProbes + " foil probes";
        }

        static bool Partyable(Room r)
        {
            switch (r.type)
            {
                case RoomType.Stair: case RoomType.StairLobby: case RoomType.Washroom: case RoomType.Janitor: case RoomType.Storage: return false;
                default: return r.rect.width * r.rect.height >= 4;
            }
        }

        static bool IsCorridor(FloorPlan plan, int col, int row)
        {
            if (col < 0 || row < 0 || col >= LevelLayout.Cols || row >= LevelLayout.Rows) return false;
            int id = plan.zone[row, col];
            return id >= 0 && plan.rooms[id] == plan.corridor;
        }

        static Rect RoomRect(Room room)
        {
            var R = room.rect;
            return Rect.MinMaxRect(R.x * 4f + 0.2f, (24 - R.y - R.height) * 4f + 0.2f, (R.x + R.width) * 4f - 0.2f, (24 - R.y) * 4f - 0.2f);
        }

        static float Lerp(float a, float b, Random rnd) { return a + (b - a) * (float)rnd.NextDouble(); }

        // ------------------------------------------------------------------ retexture
        static int Retexture(Transform f2, Floor2PartyRecord rec)
        {
            // floor 4's planks, floor 4's painted brick made white for the walls, and the ceiling in the bricks' teal
            var f4Wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Mat_F4_Wall.mat");
            var f4Ceil = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Mat_F4_Ceil.mat");
            Color teal;
            var whiteBrick = WhitenedTexture(f4Wall.GetTexture("_BaseMap") as Texture2D, out teal);
            var wallMat = CopyMat(f4Wall, "Mat_Party_Wall");
            if (whiteBrick != null) wallMat.SetTexture("_BaseMap", whiteBrick);
            wallMat.SetColor("_BaseColor", new Color(0.96f, 0.95f, 0.93f));
            var ceilMat = CopyMat(f4Ceil, "Mat_Party_Ceil");
            ceilMat.SetColor("_BaseColor", teal);
            var floorMat = CopyMat(AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Mat_F4_Floor.mat"), "Mat_Party_Floor");
            const string tex = "Assets/HollowTowerRemastered/Textures/UserTextures/";
            Deepen(wallMat, tex + "walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG", 0.035f, 1.8f);
            Deepen(floorMat, tex + "floors/Planks021_2K-PNG/Planks021_2K-PNG", 0.02f, 1.6f);
            Deepen(ceilMat, tex + "ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG", 0.025f, 1.5f);
            var map = new Dictionary<string, Material>
            {
                { "Mat_F2_Floor", floorMat },
                { "Mat_F2_Wall", wallMat },
                { "Mat_F2_Ceil", ceilMat },
            };
            int n = 0;
            foreach (var part in new[] { "Slabs_And_Ceilings", "Walls_And_Doors" })
            {
                var t = f2.Find(part);
                if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material to;
                        if (mats[i] == null || !map.TryGetValue(mats[i].name, out to) || to == null) continue;
                        rec.swappedRenderers.Add(r); rec.originalMaterials.Add(mats[i]); rec.slotIndex.Add(i);
                        mats[i] = to; changed = true; n++;
                    }
                    if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
                }
            }
            return n;
        }

        static Material CopyMat(Material src, string name)
        {
            string path = AssetDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// More depth on a surface: its height map as parallax (bricks stand out of the mortar, planks out of their
        /// seams), a stronger normal map, and the real ambient-occlusion map in the crevices.
        /// </summary>
        static void Deepen(Material m, string prefix, float parallax, float bump)
        {
            var height = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Displacement.png");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_AmbientOcclusion.png");
            if (height != null)
            {
                m.SetTexture("_ParallaxMap", height);
                m.SetFloat("_Parallax", parallax);
                m.EnableKeyword("_PARALLAXMAP");
            }
            if (ao != null)
            {
                m.SetTexture("_OcclusionMap", ao);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            m.SetFloat("_BumpScale", bump);
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
        }

        /// <summary>A white copy of a coloured texture (its brightness pattern kept), and the texture's average colour.</summary>
        static Texture2D WhitenedTexture(Texture2D src, out Color average)
        {
            average = new Color(0.45f, 0.62f, 0.62f);
            if (src == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(src);
            var img = new Texture2D(2, 2);
            if (!img.LoadImage(System.IO.File.ReadAllBytes(srcPath))) return null;
            var px = img.GetPixels32();
            double r = 0, g = 0, b = 0, lum = 0;
            foreach (var c in px) { r += c.r; g += c.g; b += c.b; lum += c.r * 0.3 + c.g * 0.59 + c.b * 0.11; }
            int n = px.Length;
            average = new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255));
            float mean = (float)(lum / n);
            for (int i = 0; i < n; i++)
            {
                float l = px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f;
                // bricks stay near white; mortar and chips keep their darker shading
                byte v = (byte)Mathf.Clamp(235f + (l - mean) * 0.9f, 0f, 255f);
                px[i] = new Color32(v, v, (byte)Mathf.Max(0, v - 4), 255);
            }
            img.SetPixels32(px);
            string outPath = AssetDir + "/PaintedBricks_White.png";
            System.IO.File.WriteAllBytes(outPath, img.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        // ------------------------------------------------------------------ assets
        static void LoadAssets()
        {
            EnsureFolder(AssetDir);
            balloon = new Material[Palette.Length];
            flat = new Material[Palette.Length];
            for (int i = 0; i < Palette.Length; i++)
            {
                balloon[i] = Mat("Mat_Balloon_" + i, Palette[i], 0f, 0.5f);   // satin latex: blurs the foil probes instead of mirroring them
                flat[i] = Mat("Mat_PartyCloth_" + i, Palette[i] * 0.92f, 0f, 0.15f);
            }
            stringMat = Mat("Mat_BalloonString", new Color(0.92f, 0.92f, 0.9f), 0f, 0.2f);
            white = Mat("Mat_PartyWhite", new Color(0.95f, 0.94f, 0.92f), 0f, 0.3f);
            wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Mat_Remastered_AgedWood.mat") ?? Mat("Mat_PartyWood", new Color(0.45f, 0.32f, 0.2f), 0f, 0.25f);
            cake = Mat("Mat_CakeSponge", new Color(0.55f, 0.32f, 0.18f), 0f, 0.1f);
            icing = Mat("Mat_CakeIcing", new Color(1f, 0.78f, 0.88f), 0f, 0.55f);
            flame = Mat("Mat_CandleFlame", new Color(1f, 0.7f, 0.2f), 0f, 0f, new Color(3f, 1.6f, 0.3f));
            gold = Mat("Mat_RibbonGold", new Color(0.95f, 0.75f, 0.2f), 0.7f, 0.6f);
            castleBase = Mat("Mat_CastleVinyl", new Color(0.9f, 0.2f, 0.25f), 0f, 0.7f);
            pennant = MeshAsset("PennantTri", TriangleMesh());
            cone = MeshAsset("PartyHatCone", ConeMesh(14));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadCarnivalAssets();
            // the font's own material draws over everything (no depth test); this one sits in the world like paint
            string tp = AssetDir + "/Mat_BannerText.mat";
            textMat = AssetDatabase.LoadAssetAtPath<Material>(tp);
            if (textMat == null) { textMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(textMat, tp); }
            textMat.SetTexture("_BaseMap", font.material.mainTexture);
            textMat.SetColor("_BaseColor", Color.white);
            textMat.SetFloat("_AlphaClip", 1f); textMat.EnableKeyword("_ALPHATEST_ON");
            textMat.SetFloat("_Cutoff", 0.5f);
            textMat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(textMat);
        }

        static Material Mat(string name, Color c, float metal, float smooth, Color? emission = null)
        {
            string path = AssetDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Cull", 0f);   // pennants are single quads, seen from both sides
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh MeshAsset(string name, Mesh m)
        {
            string path = AssetDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Mesh TriangleMesh()
        {
            // a pennant hanging point-down from its top edge (origin at the top centre), facing +z and -z
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, -1f, 0f) };
            m.uv = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f) };
            m.triangles = new[] { 0, 1, 2 };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh ConeMesh(int sides)
        {
            var v = new List<Vector3> { new Vector3(0f, 1f, 0f) };
            var t = new List<int>();
            for (int i = 0; i <= sides; i++) { float a = i * Mathf.PI * 2f / sides; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f)); }
            for (int i = 1; i <= sides; i++) t.AddRange(new[] { 0, i + 1, i });
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ primitives
        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default(Vector3), bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static GameObject MeshObj(Transform parent, string name, Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ dressing pieces
        static int CeilingBalloons(Transform parent, Vector3 at, Random rnd, int count = 0)
        {
            if (count <= 0) count = 3 + rnd.Next(3);
            var g = new GameObject("Balloons").transform;
            g.SetParent(parent, false);
            // spread round a ring so neighbours touch at most, instead of passing through each other
            float ring = count * 0.58f / (2f * Mathf.PI), turn = Lerp(0f, Mathf.PI * 2f, rnd);
            for (int i = 0; i < count; i++)
            {
                float sx = (float)(0.3 + rnd.NextDouble() * 0.06), sy = sx * 1.22f;
                float ang = turn + i * Mathf.PI * 2f / count + Lerp(-0.15f, 0.15f, rnd);
                Vector3 c = new Vector3(at.x + Mathf.Cos(ang) * ring, CeilY - sy * 0.5f - 0.01f, at.z + Mathf.Sin(ang) * ring);
                var mat = balloon[rnd.Next(balloon.Length)];
                if (rnd.NextDouble() < 0.55) { c.y = CeilY - 0.27f; FoilBalloon(g, c, rnd); sy = 0.5f; }   // shiny foil, round or star
                else Prim(PrimitiveType.Sphere, g, "Balloon", c, new Vector3(sx, sy, sx), mat, new Vector3(Lerp(-8f, 8f, rnd), 0f, Lerp(-8f, 8f, rnd)));
                Prim(PrimitiveType.Cube, g, "Knot", c + Vector3.down * (sy * 0.5f + 0.015f), new Vector3(0.04f, 0.035f, 0.04f), mat);
                float len = Lerp(0.9f, 1.6f, rnd);
                Prim(PrimitiveType.Cube, g, "String", c + Vector3.down * (sy * 0.5f + 0.03f + len * 0.5f), new Vector3(0.006f, len, 0.006f), stringMat, new Vector3(Lerp(-3f, 3f, rnd), 0f, Lerp(-3f, 3f, rnd)));
            }
            return count;
        }

        static void Bunting(Transform parent, Vector3 a, Vector3 b, float sag, Random rnd)
        {
            var g = new GameObject("Bunting").transform;
            g.SetParent(parent, false);
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.36f));
            Vector3 dir = (b - a).normalized;
            Quaternion face = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            Vector3 prev = a;
            int start = rnd.Next(flat.Length);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
                // the cord, segment by segment
                Vector3 mid = (prev + p) * 0.5f;
                var cord = Prim(PrimitiveType.Cube, g, "Cord", mid, new Vector3(0.008f, 0.008f, Vector3.Distance(prev, p)), stringMat);
                cord.transform.rotation = Quaternion.LookRotation(p - prev);
                if (i < n) MeshObj(g, "Pennant", pennant, p, face, new Vector3(0.24f, 0.3f, 1f), flat[(start + i) % flat.Length]);
                prev = p;
            }
        }

        static void Banner(Transform parent, Vector3 centre, Vector3 facing, string text, Random rnd)
        {
            var g = new GameObject("Banner_" + text).transform;
            g.SetParent(parent, false);
            Vector3 along = Vector3.Cross(facing, Vector3.up).normalized;   // left to right for someone facing the wall
            float step = 0.5f;
            float w = (text.Length - 1) * step;
            Quaternion rot = Quaternion.LookRotation(-facing, Vector3.up);   // TextMesh reads correctly when its +z points away from the viewer
            Vector3 a = centre - along * (w * 0.5f + 0.4f) + Vector3.up * 0.15f, b = centre + along * (w * 0.5f + 0.4f) + Vector3.up * 0.15f;
            var cord = Prim(PrimitiveType.Cube, g, "Cord", (a + b) * 0.5f, new Vector3(0.01f, 0.01f, Vector3.Distance(a, b)), stringMat);
            cord.transform.rotation = Quaternion.LookRotation(b - a);
            for (int i = 0; i < text.Length; i++)
            {
                Vector3 p = centre + along * (i * step - w * 0.5f) + Vector3.up * 0.15f + facing * 0.01f;
                if (text[i] == ' ') continue;
                MeshObj(g, "Flag", pennant, p, rot, new Vector3(0.46f, 0.62f, 1f), flat[i % flat.Length]);
                var tgo = new GameObject("Letter_" + text[i]);
                tgo.transform.SetParent(g, false);
                tgo.transform.SetPositionAndRotation(p + Vector3.down * 0.22f + facing * 0.012f, rot);
                var tm = tgo.AddComponent<TextMesh>();
                tm.text = text[i].ToString(); tm.font = font; tm.fontSize = 64; tm.characterSize = 0.045f;
                tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white; tm.fontStyle = FontStyle.Bold;
                tgo.GetComponent<MeshRenderer>().sharedMaterial = textMat;
            }
        }

        static void PartyTable(Transform parent, Vector3 at, Random rnd)
        {
            var g = new GameObject("PartyTable").transform;
            g.SetParent(parent, false);
            float yaw = rnd.Next(2) * 90f;
            g.position = at; g.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cloth = flat[rnd.Next(flat.Length)];
            Local(g, PrimitiveType.Cube, "Top", new Vector3(0f, 0.74f, 0f), new Vector3(2.4f, 0.04f, 1.1f), wood, true);
            Local(g, PrimitiveType.Cube, "Cloth", new Vector3(0f, 0.765f, 0f), new Vector3(2.5f, 0.012f, 1.2f), cloth);
            Local(g, PrimitiveType.Cube, "ClothDrop_N", new Vector3(0f, 0.6f, 0.6f), new Vector3(2.5f, 0.32f, 0.012f), cloth);
            Local(g, PrimitiveType.Cube, "ClothDrop_S", new Vector3(0f, 0.6f, -0.6f), new Vector3(2.5f, 0.32f, 0.012f), cloth);
            foreach (float lx in new[] { -1.1f, 1.1f }) foreach (float lz in new[] { -0.45f, 0.45f })
                Local(g, PrimitiveType.Cube, "Leg", new Vector3(lx, 0.36f, lz), new Vector3(0.05f, 0.72f, 0.05f), wood);
            // the cake, slightly off centre, with candles
            Local(g, PrimitiveType.Cylinder, "Cake", new Vector3(0.2f, 0.86f, 0.05f), new Vector3(0.42f, 0.09f, 0.42f), cake);
            Local(g, PrimitiveType.Cylinder, "Icing", new Vector3(0.2f, 0.955f, 0.05f), new Vector3(0.44f, 0.012f, 0.44f), icing);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var cp = new Vector3(0.2f + Mathf.Cos(a) * 0.12f, 1.0f, 0.05f + Mathf.Sin(a) * 0.12f);
                Local(g, PrimitiveType.Cylinder, "Candle", cp, new Vector3(0.014f, 0.04f, 0.014f), flat[i % flat.Length]);
                Local(g, PrimitiveType.Sphere, "Flame", cp + Vector3.up * 0.05f, new Vector3(0.016f, 0.028f, 0.016f), flame);
            }
            // cups and plates down both sides, a few party hats, one knocked over
            for (int i = 0; i < 6; i++)
            {
                float x = -1f + i * 0.4f;
                if (Mathf.Abs(x - 0.2f) < 0.3f) continue;
                float side = i % 2 == 0 ? 0.35f : -0.35f;
                Local(g, PrimitiveType.Cylinder, "Plate", new Vector3(x, 0.775f, side), new Vector3(0.2f, 0.004f, 0.2f), white);
                Local(g, PrimitiveType.Cylinder, "Cup", new Vector3(x + 0.13f, 0.82f, side * 0.6f), new Vector3(0.06f, 0.05f, 0.06f), flat[(i + 2) % flat.Length]);
            }
            for (int i = 0; i < 3; i++)
            {
                var hp = new Vector3(-0.9f + i * 0.75f, 0.772f, (i % 2 == 0 ? -0.15f : 0.2f));
                bool tipped = i == 1;
                var hat = MeshObj(g, "PartyHat", cone, g.TransformPoint(hp + (tipped ? Vector3.up * 0.07f : Vector3.zero)), g.rotation * Quaternion.Euler(tipped ? 90f : 0f, Lerp(0f, 360f, rnd), 0f), new Vector3(0.15f, 0.22f, 0.15f), flat[(i + 4) % flat.Length]);
                hat.transform.SetParent(g, true);
            }
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f); col.size = new Vector3(2.5f, 0.9f, 1.2f);
        }

        static void Presents(Transform parent, Vector3 at, Random rnd)
        {
            var g = new GameObject("Presents").transform;
            g.SetParent(parent, false);
            g.position = at;
            float y = 0f;
            for (int i = 0; i < 4; i++)
            {
                float s = Lerp(0.3f, 0.55f, rnd) * (i == 0 ? 1.3f : 1f);
                Vector3 p = i == 0 || i == 3 ? Vector3.zero : new Vector3(i == 1 ? 0.55f : -0.5f, 0f, Lerp(-0.4f, 0.4f, rnd));
                float py = i == 3 ? y : 0f;   // the last one sits on the big one
                var box = Local(g, PrimitiveType.Cube, "Gift", p + Vector3.up * (py + s * 0.5f), new Vector3(s, s * 0.85f, s), flat[rnd.Next(flat.Length)], i == 0);
                box.transform.localRotation = Quaternion.Euler(0f, Lerp(0f, 90f, rnd), 0f);
                Local(box.transform, PrimitiveType.Cube, "RibbonA", Vector3.zero, new Vector3(1.02f, 1.02f, 0.14f), gold);
                Local(box.transform, PrimitiveType.Cube, "RibbonB", Vector3.zero, new Vector3(0.14f, 1.02f, 1.02f), gold);
                if (i == 0) y = s * 0.85f;
            }
        }

        static void BouncyCastle(Transform parent, Vector3 at, Random rnd)
        {
            var g = new GameObject("BouncyCastle").transform;
            g.SetParent(parent, false);
            g.position = at;
            var a = balloon[0]; var b = balloon[1]; var c = balloon[2];
            Local(g, PrimitiveType.Cube, "Floor", new Vector3(0f, 0.25f, 0f), new Vector3(4.6f, 0.5f, 4.6f), castleBase, true);
            // padded walls, alternating colours, sagging a little (the fan is off)
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, side * 90f, side == 1 ? 1.5f : 0f);
                for (int k = 0; k < 4; k++)
                {
                    if (side == 0 && (k == 1 || k == 2)) continue;   // the open front
                    Vector3 p = q * new Vector3(-1.725f + k * 1.15f, 1.35f, 2.15f);
                    var w = Local(g, PrimitiveType.Cube, "Wall", p, Vector3.one, k % 2 == 0 ? b : c, true);
                    w.transform.localRotation = q;
                    w.transform.localScale = new Vector3(1.1f, 1.7f - (k == 2 ? 0.12f : 0f), 0.35f);
                }
            }
            // corner towers with balls on top
            foreach (float x in new[] { -2.2f, 2.2f }) foreach (float z in new[] { -2.2f, 2.2f })
            {
                Local(g, PrimitiveType.Cylinder, "Tower", new Vector3(x, 1.6f, z), new Vector3(0.75f, 1.6f, 0.75f), a, true);
                Local(g, PrimitiveType.Sphere, "TowerTop", new Vector3(x, 3.3f, z), new Vector3(0.85f, 0.85f, 0.85f), b);
            }
            // the front arch and the step
            Local(g, PrimitiveType.Cube, "Arch", new Vector3(0f, 2.55f, -2.15f), new Vector3(2.3f, 0.5f, 0.4f), a);
            Local(g, PrimitiveType.Cube, "Step", new Vector3(0f, 0.12f, -2.75f), new Vector3(2.2f, 0.24f, 0.7f), c, true);
            g.rotation = Quaternion.Euler(0f, rnd.Next(4) * 90f, 0f);
        }

        static GameObject Local(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material m, bool collider = false)
        {
            var go = Prim(type, parent, name, Vector3.zero, Vector3.one, m, default(Vector3), collider);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            if (collider) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>A clear patch of floor (no colliders above the slab) of this size inside the rect.</summary>
        static bool FindFloorSpace(Rect r, Vector2 size, float height, Random rnd, int tries, out Vector3 spot)
        {
            spot = Vector3.zero;
            float hx = size.x * 0.5f + 0.3f, hz = size.y * 0.5f + 0.3f;
            if (r.width < hx * 2f || r.height < hz * 2f) return false;
            for (int i = 0; i < tries; i++)
            {
                var p = new Vector3(Lerp(r.xMin + hx, r.xMax - hx, rnd), FloorY, Lerp(r.yMin + hz, r.yMax - hz, rnd));
                var half = new Vector3(Mathf.Max(hx, hz), height * 0.5f, Mathf.Max(hx, hz));   // square, so either rotation fits
                if (!Physics.CheckBox(p + Vector3.up * (height * 0.5f + 0.06f), half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) { spot = p; return true; }
            }
            return false;
        }
    }
}
