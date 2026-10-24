using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Simple built-from-boxes props the downloaded packs don't have (lockers, toilet stalls, lab benches,
    /// notice boards, vending machines...) plus decay pieces (paper, ceiling tiles, rubble, puddles).
    /// Floor builders put the pivot at the bottom centre with the front facing +Z;
    /// wall builders put it at the centre of the back face, with the item standing out along +Z.
    /// </summary>
    public static partial class ProcKit
    {
        public const string MatDir = "Assets/HollowTowerRemastered/Props/Proc";
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material ceilingTile;

        // ------------------------------------------------------------------ materials
        public static void EnsureMaterials(int floor)
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/HollowTowerRemastered/Props", "Proc");
            ceilingTile = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Mat_F" + (floor + 1) + "_Ceil.mat");
        }

        static Material M(string name, Color c, float metal = 0f, float smooth = 0.3f, string baseMap = null, string normal = null, Color? emission = null)
        {
            Material m;
            if (mats.TryGetValue(name, out m) && m != null) return m;
            string path = MatDir + "/Mat_" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Smoothness", smooth);
                if (baseMap != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
                if (normal != null)
                {
                    m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                    m.EnableKeyword("_NORMALMAP");
                }
                if (emission.HasValue)
                {
                    m.SetColor("_EmissionColor", emission.Value);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                m.enableInstancing = true;
                AssetDatabase.CreateAsset(m, path);
            }
            mats[name] = m;
            return m;
        }

        const string Tex = "Assets/HollowTowerRemastered/Textures/";
        static Material Existing(string name)
        {
            Material m;
            if (mats.TryGetValue(name, out m) && m != null) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/" + name + ".mat");
            mats[name] = m;
            return m;
        }

        public static Material Paper(int i)
        {
            switch (i % 5)
            {
                case 0: return M("Paper", new Color(0.82f, 0.8f, 0.74f), 0, 0.15f);
                case 1: return M("PaperOld", new Color(0.72f, 0.66f, 0.52f), 0, 0.1f);
                case 2: return M("PaperGrey", new Color(0.6f, 0.6f, 0.58f), 0, 0.1f);
                case 3: return M("PaperPink", new Color(0.78f, 0.55f, 0.55f), 0, 0.12f);
                default: return M("PaperBlue", new Color(0.52f, 0.62f, 0.75f), 0, 0.12f);
            }
        }
        static Material LockerPaint(int i)
        {
            string n = Tex + "RustedSteel_Normal.png";
            switch (i % 4)
            {
                case 0: return M("LockerBlue", new Color(0.16f, 0.25f, 0.34f), 0.55f, 0.35f, null, n);
                case 1: return M("LockerGreen", new Color(0.2f, 0.29f, 0.22f), 0.55f, 0.35f, null, n);
                case 2: return M("LockerGrey", new Color(0.36f, 0.37f, 0.38f), 0.6f, 0.35f, null, n);
                default: return M("LockerRust", new Color(0.55f, 0.42f, 0.34f), 0.4f, 0.25f, Tex + "RustedSteel_Albedo.png", n);
            }
        }
        static Material Dark { get { return M("DarkMetal", new Color(0.06f, 0.06f, 0.065f), 0.6f, 0.35f); } }
        static Material Steel { get { return M("Steel", new Color(0.55f, 0.56f, 0.57f), 0.85f, 0.45f, null, Tex + "RustedSteel_Normal.png"); } }
        static Material Porcelain { get { return M("Porcelain", new Color(0.72f, 0.71f, 0.66f), 0, 0.65f); } }
        static Material Wood { get { return Existing("Mat_Remastered_AgedWood") ?? M("Wood", new Color(0.35f, 0.24f, 0.15f)); } }
        static Material Concrete { get { return Existing("Mat_Remastered_Concrete044") ?? M("Concrete", new Color(0.4f, 0.4f, 0.38f)); } }
        static Material Laminate { get { return M("Laminate", new Color(0.48f, 0.46f, 0.42f), 0, 0.4f); } }
        static Material EpoxyTop { get { return M("EpoxyTop", new Color(0.05f, 0.05f, 0.05f), 0, 0.6f); } }
        static Material Fabric { get { return M("Fabric", new Color(0.25f, 0.28f, 0.32f), 0, 0.08f); } }
        static Material Mirror { get { return NoSky(M("MirrorPane", new Color(0.55f, 0.58f, 0.6f), 0f, 0.8f)); } }
        static Material Cork { get { return M("Cork", new Color(0.45f, 0.31f, 0.18f), 0, 0.1f); } }
        static Material Chalk { get { return M("Chalkboard", new Color(0.07f, 0.12f, 0.09f), 0, 0.2f); } }
        static Material WhiteBoard { get { return M("Whiteboard", new Color(0.75f, 0.75f, 0.73f), 0, 0.75f); } }
        static Material Appliance { get { return M("Appliance", new Color(0.7f, 0.69f, 0.64f), 0.1f, 0.45f); } }
        static Material Glass { get { return NoSky(M("DarkGlass", new Color(0.03f, 0.04f, 0.045f), 0.2f, 0.95f)); } }
        static Material Puddle { get { return Wet(NoSky(M("Puddle", new Color(0.015f, 0.015f, 0.015f), 0f, 0.97f))); } }

        // Puddles: a dark, glossy, see-through film, so the floor shows through and lights glint on it.
        static Material Wet(Material m)
        {
            if (m.GetFloat("_Surface") != 1f)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetColor("_BaseColor", new Color(0.02f, 0.025f, 0.03f, 0.38f));
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                m.SetShaderPassEnabled("DepthOnly", false);
                m.SetShaderPassEnabled("ShadowCaster", false);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        // Indoors there is no sky to reflect: without this, glossy surfaces mirror the blue skybox cubemap.
        static Material NoSky(Material m)
        {
            if (m.GetFloat("_EnvironmentReflections") != 0f)
            {
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(m);
            }
            return m;
        }
        static Material Plastic(int i)
        {
            switch (i % 4)
            {
                case 0: return M("PlasticRed", new Color(0.45f, 0.08f, 0.07f), 0, 0.45f);
                case 1: return M("PlasticBlue", new Color(0.1f, 0.18f, 0.38f), 0, 0.45f);
                case 2: return M("PlasticOrange", new Color(0.6f, 0.3f, 0.06f), 0, 0.45f);
                default: return M("PlasticGrey", new Color(0.3f, 0.3f, 0.3f), 0, 0.4f);
            }
        }
        static Material Book(int i)
        {
            Color[] c = { new Color(0.35f, 0.07f, 0.06f), new Color(0.08f, 0.13f, 0.3f), new Color(0.1f, 0.22f, 0.12f),
                          new Color(0.3f, 0.2f, 0.1f), new Color(0.08f, 0.08f, 0.08f), new Color(0.5f, 0.45f, 0.3f) };
            int k = i % c.Length;
            return M("Book" + k, c[k], 0, 0.25f);
        }
        static Material Mattress(int i)
        {
            switch (i % 3)
            {
                case 0: return M("Mattress", new Color(0.58f, 0.55f, 0.47f), 0, 0.1f);
                case 1: return M("MattressStained", new Color(0.48f, 0.42f, 0.31f), 0, 0.1f);
                default: return M("MattressBlue", new Color(0.3f, 0.36f, 0.45f), 0, 0.1f);
            }
        }
        static Material Cloth(int i)
        {
            Color[] c = { new Color(0.25f, 0.1f, 0.1f), new Color(0.12f, 0.15f, 0.22f), new Color(0.4f, 0.38f, 0.33f), new Color(0.15f, 0.2f, 0.12f) };
            int k = i % c.Length;
            return M("Cloth" + k, c[k], 0, 0.05f);
        }
        static Material Led(bool green)
        {
            return green ? M("LedGreen", new Color(0.05f, 0.2f, 0.05f), 0, 0.5f, null, null, new Color(0.1f, 0.6f, 0.1f))
                         : M("LedRed", new Color(0.2f, 0.03f, 0.03f), 0, 0.5f, null, null, new Color(0.7f, 0.05f, 0.03f));
        }

        // ------------------------------------------------------------------ primitive helpers
        static GameObject Root(string name) { return new GameObject(name); }

        static GameObject Box(Transform parent, Vector3 center, Vector3 size, Material m, Vector3? euler = null, string name = "Box")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        // Shared low-poly meshes (Unity's primitive cylinder has 88 vertices; these props never need that).
        static Mesh lowCylinder, disc, sheet;
        static Mesh SharedMesh(ref Mesh cache, string name, System.Func<Mesh> build)
        {
            if (cache != null) return cache;
            string path = MatDir + "/" + name + ".asset";
            cache = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cache == null) { cache = build(); cache.name = name; AssetDatabase.CreateAsset(cache, path); }
            return cache;
        }

        /// <summary>8-sided cylinder, radius 0.5, y from -1 to 1 (same size as Unity's primitive).</summary>
        static Mesh LowCylinder()
        {
            return SharedMesh(ref lowCylinder, "LowCylinder", () =>
            {
                const int n = 8;
                var v = new List<Vector3>(); var t = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                    var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                    var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                    int b = v.Count;
                    v.Add(p0 + Vector3.down); v.Add(p1 + Vector3.down); v.Add(p1 + Vector3.up); v.Add(p0 + Vector3.up);
                    t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                    int c = v.Count;   // caps
                    v.Add(Vector3.up); v.Add(p0 + Vector3.up); v.Add(p1 + Vector3.up);
                    t.AddRange(new[] { c, c + 2, c + 1 });
                    int d = v.Count;
                    v.Add(Vector3.down); v.Add(p0 + Vector3.down); v.Add(p1 + Vector3.down);
                    t.AddRange(new[] { d, d + 1, d + 2 });
                }
                var m = new Mesh();
                m.SetVertices(v); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>Flat 12-sided disc of radius 0.5 facing up, for puddles.</summary>
        static Mesh Disc()
        {
            return SharedMesh(ref disc, "Disc", () =>
            {
                const int n = 12;
                var v = new List<Vector3> { Vector3.zero }; var t = new List<int>();
                for (int i = 0; i < n; i++) v.Add(new Vector3(Mathf.Cos(i * Mathf.PI * 2 / n) * 0.5f, 0, Mathf.Sin(i * Mathf.PI * 2 / n) * 0.5f));
                for (int i = 0; i < n; i++) t.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
                var m = new Mesh();
                m.SetVertices(v); m.SetTriangles(t, 0);
                var nrm = new Vector3[v.Count]; for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
                m.normals = nrm; m.RecalculateBounds();
                return m;
            });
        }

        /// <summary>Flat 1 x 1 sheet facing up, for paper.</summary>
        static Mesh Sheet()
        {
            return SharedMesh(ref sheet, "Sheet", () =>
            {
                var m = new Mesh();
                m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
                m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                m.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                m.RecalculateBounds();
                return m;
            });
        }

        static GameObject Flat(Transform parent, Vector3 center, Vector3 size, Material m, Mesh mesh, Vector3? euler = null, string name = "Flat")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static GameObject Cyl(Transform parent, Vector3 center, float radius, float height, Material m, Vector3? euler = null, string name = "Cyl")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.GetComponent<MeshFilter>().sharedMesh = LowCylinder();
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = new Vector3(radius * 2, height * 0.5f, radius * 2);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static void Solid(GameObject root, Vector3 center, Vector3 size)
        {
            var c = root.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        static void NoShadows(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ reusable variants
        // Box-built props are baked into a few prefab variants and reused (see ProcVariants);
        // size parameters are rounded so similar requests share variants.
        public static ProcVariant Var(string key, int variants, System.Func<RoomCtx, GameObject> build)
        {
            return new ProcVariant { key = key, variants = variants, build = build };
        }
        static float Q(float v, float step) { return Mathf.Max(step, Mathf.Round(v / step) * step); }
        static string K(float v) { return v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
        public static ProcVariant LockerBankV(int count, int paint) { return Var("Lockers_" + count + "_" + paint, 3, c => LockerBank(c, count, paint)); }
        public static ProcVariant StallRowV(int count) { return Var("ToiletStalls_" + count, 3, c => StallRow(c, count)); }
        public static ProcVariant SinkCounterV(float len) { int b = Mathf.Max(1, Mathf.FloorToInt(len / 0.8f)); return Var("SinkCounter_" + b, 3, c => SinkCounter(c, b * 0.8f)); }
        public static ProcVariant PartitionV(float len, float height = 1.45f) { len = Q(len, 0.2f); return Var("CubiclePanel_" + K(len) + "_" + K(height), 1, c => Partition(c, len, height)); }
        public static ProcVariant LabBenchV(float len) { len = Q(len, 0.4f); return Var("LabBench_" + K(len), 2, c => LabBench(c, len)); }
        public static ProcVariant CounterV(float len, float depth = 0.7f, float height = 1.05f) { len = Q(len, 0.4f); return Var("Counter_" + K(len) + "_" + K(depth) + "_" + K(height), 1, c => Counter(c, len, depth, height)); }
        public static ProcVariant BenchV(float len) { len = Q(len, 0.4f); return Var("Bench_" + K(len), 1, c => Bench(c, len)); }
        public static ProcVariant BoardV(float w, bool chalk) { w = Q(w, 0.5f); return Var((chalk ? "Blackboard_" : "Whiteboard_") + K(w), 3, c => Board(c, w, chalk)); }
        public static ProcVariant CellBarsV(float len) { len = Q(len, 1f); return Var("CellBars_" + K(len), 1, c => CellBars(c, len)); }

        // ------------------------------------------------------------------ decay pieces
        public static GameObject PaperSheet(RoomCtx c)
        {
            var root = Root("Paper");
            bool crumpled = c.Chance(0.15f);
            if (crumpled) Box(root.transform, new Vector3(0, 0.03f, 0), Vector3.one * c.Range(0.05f, 0.08f), Paper(c.Range(0, 5)), new Vector3(c.Range(0f, 90f), c.Range(0f, 90f), c.Range(0f, 90f)));
            else Flat(root.transform, new Vector3(0, 0.0015f, 0), new Vector3(0.21f, 1f, 0.297f), Paper(c.Range(0, 5)), Sheet(), new Vector3(c.Range(-1.5f, 1.5f), 0, c.Range(-1.5f, 1.5f)), "Sheet");
            NoShadows(root);
            return root;
        }

        public static GameObject BookItem(RoomCtx c)
        {
            var root = Root("Book");
            float w = c.Range(0.15f, 0.24f), d = c.Range(0.11f, 0.17f), t = c.Range(0.02f, 0.05f);
            if (c.Chance(0.5f)) Box(root.transform, new Vector3(0, t * 0.5f, 0), new Vector3(w, t, d), Book(c.Range(0, 6)));
            else
            {
                // lying open: two halves at a shallow angle
                var m = Book(c.Range(0, 6));
                Box(root.transform, new Vector3(-w * 0.5f, 0.01f, 0), new Vector3(w, 0.012f, d), m, new Vector3(0, 0, 4));
                Box(root.transform, new Vector3(w * 0.5f, 0.01f, 0), new Vector3(w, 0.012f, d), m, new Vector3(0, 0, -4));
                Box(root.transform, new Vector3(0, 0.021f, 0), new Vector3(w * 1.9f, 0.004f, d * 0.95f), Paper(1));
            }
            NoShadows(root);
            return root;
        }

        public static GameObject BookStack(RoomCtx c)
        {
            var root = Root("BookStack");
            int n = c.Range(2, 7);
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float t = c.Range(0.025f, 0.05f);
                Box(root.transform, new Vector3(c.Range(-0.02f, 0.02f), y + t * 0.5f, c.Range(-0.02f, 0.02f)), new Vector3(c.Range(0.16f, 0.24f), t, c.Range(0.12f, 0.17f)), Book(c.Range(0, 6)), new Vector3(0, c.Range(-15f, 15f), 0));
                y += t;
            }
            return root;
        }

        /// <summary>A row of books standing upright, for shelves.</summary>
        public static GameObject BookRow(RoomCtx c)
        {
            var root = Root("BookRow");
            int n = c.Range(5, 14);
            float x = 0;
            for (int i = 0; i < n; i++)
            {
                float t = c.Range(0.025f, 0.055f), h = c.Range(0.18f, 0.27f);
                bool leaning = i == n - 1 && c.Chance(0.5f);
                Box(root.transform, new Vector3(x + t * 0.5f + (leaning ? 0.04f : 0), h * 0.5f - (leaning ? 0.02f : 0), 0), new Vector3(t, h, c.Range(0.14f, 0.18f)), Book(c.Range(0, 6)), leaning ? new Vector3(0, 0, -18) : (Vector3?)null);
                x += t + 0.002f;
            }
            foreach (Transform t in root.transform) t.localPosition -= new Vector3(x * 0.5f, 0, 0);
            return root;
        }

        public static GameObject CeilingTile(RoomCtx c)
        {
            var root = Root("FallenCeilingTile");
            var m = ceilingTile ?? Concrete;
            if (c.Chance(0.6f)) Box(root.transform, new Vector3(0, 0.008f, 0), new Vector3(0.6f, 0.016f, 0.6f), m);
            else
            {
                Box(root.transform, new Vector3(-0.15f, 0.008f, 0), new Vector3(0.3f, 0.016f, 0.6f), m, new Vector3(0, c.Range(-10f, 10f), 0));
                Box(root.transform, new Vector3(0.22f, 0.008f, 0.1f), new Vector3(0.28f, 0.016f, 0.35f), m, new Vector3(0, c.Range(10f, 40f), 0));
            }
            NoShadows(root);
            return root;
        }

        public static GameObject Rubble(RoomCtx c)
        {
            var root = Root("Rubble");
            int n = c.Range(3, 10);
            for (int i = 0; i < n; i++)
            {
                float s = c.Range(0.04f, 0.22f);
                var p = new Vector2(c.Range(-0.35f, 0.35f), c.Range(-0.35f, 0.35f));
                Box(root.transform, new Vector3(p.x, s * 0.3f, p.y), new Vector3(s, s * c.Range(0.4f, 0.9f), s * c.Range(0.6f, 1.2f)), Concrete,
                    new Vector3(c.Range(0f, 40f), c.Range(0f, 360f), c.Range(0f, 40f)));
            }
            return root;
        }

        public static GameObject PuddleDecal(RoomCtx c)
        {
            var root = Root("Puddle");
            int n = c.Range(1, 4);
            for (int i = 0; i < n; i++)
            {
                float r = c.Range(0.25f, 0.9f);
                Flat(root.transform, new Vector3(c.Range(-0.4f, 0.4f), 0.002f + i * 0.0004f, c.Range(-0.4f, 0.4f)), new Vector3(r * 2, 1f, r * c.Range(2.2f, 4.8f) * 0.5f), Puddle, Disc(), new Vector3(0, c.Range(0f, 180f), 0), "Pool");
            }
            NoShadows(root);
            return root;
        }

        /// <summary>A gap in the ceiling where tiles have dropped: dark void, dangling cables, maybe a tile hanging by one edge. Pivot = top.</summary>
        public static GameObject CeilingHole(RoomCtx c)
        {
            var root = Root("CeilingHole");
            var voidM = M("CeilingVoid", new Color(0.01f, 0.01f, 0.012f), 0, 0.05f);
            int tiles = c.Range(1, 4);
            for (int i = 0; i < tiles; i++)
                Box(root.transform, new Vector3((i % 2) * 0.6f - 0.3f * (tiles > 1 ? 1 : 0), -0.007f, (i / 2) * 0.6f), new Vector3(0.6f, 0.004f, 0.6f), voidM);
            int cables = c.Range(1, 5);
            for (int i = 0; i < cables; i++)
            {
                float len = c.Range(0.3f, 1.6f);
                var cab = Cyl(root.transform, new Vector3(c.Range(-0.3f, 0.3f), -len * 0.5f, c.Range(-0.2f, 0.4f)), 0.008f, len, M("Cable", new Color(0.04f, 0.04f, 0.04f), 0, 0.3f), new Vector3(c.Range(-12f, 12f), 0, c.Range(-12f, 12f)));
                cab.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (c.Chance(0.5f) && ceilingTile != null)
            {
                // a tile still hooked on one edge, swinging down
                var hinge = new GameObject("HangingTile").transform;
                hinge.SetParent(root.transform, false);
                hinge.localPosition = new Vector3(0, -0.01f, -0.3f);
                hinge.localRotation = Quaternion.Euler(c.Range(55f, 85f), 0, 0);
                Box(hinge, new Vector3(0, 0, 0.3f), new Vector3(0.58f, 0.015f, 0.58f), ceilingTile);
            }
            return root;
        }

        /// <summary>Old cast-iron radiator on short feet, to stand under a window or along a wall.</summary>
        public static GameObject Radiator(RoomCtx c)
        {
            var root = Root("Radiator");
            var paint = c.Chance(0.5f) ? M("RadiatorCream", new Color(0.55f, 0.52f, 0.44f), 0.3f, 0.3f, null, Tex + "RustedSteel_Normal.png")
                                       : M("RadiatorRust", new Color(0.5f, 0.38f, 0.3f), 0.4f, 0.25f, Tex + "RustedSteel_Albedo.png", Tex + "RustedSteel_Normal.png");
            int fins = c.Range(10, 26);
            float pitch = 0.065f, len = fins * pitch;
            for (int i = 0; i < fins; i++)
                Box(root.transform, new Vector3(-len * 0.5f + pitch * (i + 0.5f), 0.45f, 0.1f), new Vector3(0.045f, 0.62f, 0.16f), paint);
            Box(root.transform, new Vector3(0, 0.76f, 0.1f), new Vector3(len, 0.05f, 0.05f), paint);
            Box(root.transform, new Vector3(0, 0.15f, 0.1f), new Vector3(len, 0.05f, 0.05f), paint);
            foreach (var x in new[] { -len * 0.5f + 0.05f, len * 0.5f - 0.05f }) Box(root.transform, new Vector3(x, 0.06f, 0.1f), new Vector3(0.05f, 0.12f, 0.16f), paint);
            Cyl(root.transform, new Vector3(len * 0.5f + 0.04f, 0.2f, 0.06f), 0.015f, 0.4f, Steel, null, "Pipe");
            Solid(root, new Vector3(0, 0.4f, 0.1f), new Vector3(len, 0.8f, 0.18f));
            return root;
        }

        public static GameObject Plank(RoomCtx c)
        {
            var root = Root("Plank");
            float len = c.Range(1.2f, 2.6f);
            Box(root.transform, new Vector3(0, len * 0.5f, 0.015f), new Vector3(c.Range(0.1f, 0.2f), len, 0.025f), Wood);
            return root;
        }

        public static GameObject Tray(RoomCtx c)
        {
            var root = Root("Tray");
            Box(root.transform, new Vector3(0, 0.01f, 0), new Vector3(0.45f, 0.02f, 0.33f), Plastic(c.Range(0, 4)));
            NoShadows(root);
            return root;
        }

        public static GameObject ClothesPile(RoomCtx c)
        {
            var root = Root("Clothes");
            int n = c.Range(2, 5);
            for (int i = 0; i < n; i++)
                Box(root.transform, new Vector3(c.Range(-0.15f, 0.15f), 0.02f + i * 0.03f, c.Range(-0.15f, 0.15f)), new Vector3(c.Range(0.3f, 0.5f), 0.04f, c.Range(0.25f, 0.45f)), Cloth(c.Range(0, 4)), new Vector3(c.Range(-6f, 6f), c.Range(0f, 360f), c.Range(-6f, 6f)));
            return root;
        }

        public static GameObject Bucket(RoomCtx c)
        {
            var root = Root("Bucket");
            var m = Plastic(c.Range(0, 4));
            Cyl(root.transform, new Vector3(0, 0.15f, 0), 0.15f, 0.3f, m);
            Cyl(root.transform, new Vector3(0, 0.295f, 0), 0.13f, 0.01f, Puddle);
            if (c.Chance(0.6f)) Cyl(root.transform, new Vector3(0.05f, 0.75f, -0.1f), 0.014f, 1.35f, Wood, new Vector3(-12, 0, 8));
            return root;
        }

        // ------------------------------------------------------------------ furniture
        /// <summary>A bank of school/gym lockers; some doors hang open or are missing.</summary>
        public static GameObject LockerBank(RoomCtx c, int count, int paint)
        {
            var root = Root("Lockers");
            var body = LockerPaint(paint);
            float w = 0.38f, h = 1.85f, d = 0.45f, total = w * count;
            Box(root.transform, new Vector3(0, h * 0.5f, 0), new Vector3(total, h, d - 0.02f), body, null, "Body");
            Box(root.transform, new Vector3(0, 0.05f, d * 0.5f - 0.02f), new Vector3(total, 0.1f, 0.02f), Dark, null, "Kick");
            for (int i = 0; i < count; i++)
            {
                float x = -total * 0.5f + w * (i + 0.5f);
                Box(root.transform, new Vector3(x, h * 0.5f + 0.05f, d * 0.5f - 0.021f), new Vector3(w - 0.03f, h - 0.14f, 0.004f), Dark, null, "Inside");
                int state = c.Weighted(new[] { 6f, 2.5f, 1f });   // closed, open, missing
                if (state == 2) continue;
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(root.transform, false);
                hinge.localPosition = new Vector3(x - w * 0.5f + 0.015f, 0.12f, d * 0.5f - 0.01f);
                hinge.localRotation = Quaternion.Euler(0, state == 1 ? -c.Range(55f, 120f) : -c.Range(0f, 2f), 0);
                var dm = c.Chance(0.12f) ? LockerPaint(3) : body;
                Box(hinge, new Vector3(w * 0.5f - 0.015f, (h - 0.15f) * 0.5f, 0.01f), new Vector3(w - 0.035f, h - 0.15f, 0.018f), dm, null, "Door");
                for (int s = 0; s < 4; s++)
                    Box(hinge, new Vector3(w * 0.5f - 0.015f, h - 0.4f + s * 0.035f, 0.021f), new Vector3(w * 0.55f, 0.012f, 0.004f), Dark, null, "Vent");
                Box(hinge, new Vector3(w - 0.07f, 0.85f, 0.025f), new Vector3(0.025f, 0.12f, 0.02f), Steel, null, "Handle");
            }
            Solid(root, new Vector3(0, h * 0.5f, 0), new Vector3(total, h, d));
            return root;
        }

        /// <summary>A row of toilet stalls, 1.5 m deep, open side facing +Z.</summary>
        public static GameObject StallRow(RoomCtx c, int count)
        {
            var root = Root("ToiletStalls");
            float w = 0.95f, d = 1.5f, total = w * count;
            var panel = LockerPaint(c.Range(0, 3));
            for (int i = 0; i <= count; i++)
            {
                float x = -total * 0.5f + w * i;
                if (i > 0 && i < count && c.Chance(0.15f))
                {
                    // partition torn off and leaning
                    var p = Box(root.transform, new Vector3(x, 0.8f, d * 0.4f), new Vector3(0.03f, 1.6f, d * 0.9f), panel, new Vector3(0, 0, c.Range(8f, 18f)), "Partition");
                    p.AddComponent<BoxCollider>();
                    continue;
                }
                var part = Box(root.transform, new Vector3(x, 1.0f, d * 0.5f), new Vector3(0.03f, 1.6f, d), panel, null, "Partition");
                part.AddComponent<BoxCollider>();
            }
            for (int i = 0; i < count; i++)
            {
                float x = -total * 0.5f + w * (i + 0.5f);
                // toilet: tank against the back wall, bowl in front
                Box(root.transform, new Vector3(x, 0.75f, 0.12f), new Vector3(0.42f, 0.36f, 0.18f), Porcelain, null, "Tank");
                var bowl = Cyl(root.transform, new Vector3(x, 0.2f, 0.42f), 0.19f, 0.4f, Porcelain, null, "Bowl");
                bowl.transform.localScale = new Vector3(0.36f, 0.2f, 0.48f);
                Cyl(root.transform, new Vector3(x, 0.405f, 0.43f), 0.15f, 0.01f, Puddle, null, "Water").transform.localScale = new Vector3(0.28f, 0.005f, 0.38f);
                // front: pilasters and a door, which may be ajar, open, hanging or gone
                Box(root.transform, new Vector3(x - w * 0.5f + 0.09f, 1.0f, d), new Vector3(0.15f, 1.6f, 0.03f), panel, null, "Pilaster");
                int state = c.Weighted(new[] { 3f, 3f, 2f, 1f, 1.2f });
                if (state == 4) continue;
                var hinge = new GameObject("DoorHinge").transform;
                hinge.SetParent(root.transform, false);
                hinge.localPosition = new Vector3(x - w * 0.5f + 0.17f, 0.2f, d);
                float yaw = state == 0 ? 0 : state == 1 ? c.Range(20f, 45f) : state == 2 ? c.Range(80f, 100f) : c.Range(30f, 70f);
                hinge.localRotation = Quaternion.Euler(0, yaw, state == 3 ? c.Range(-12f, -6f) : 0);
                var door = Box(hinge, new Vector3(0.31f, 0.8f, 0), new Vector3(0.62f, 1.6f, 0.025f), panel, null, "Door");
                door.AddComponent<BoxCollider>();
            }
            Box(root.transform, new Vector3(0, 1.82f, d), new Vector3(total, 0.04f, 0.04f), Steel, null, "HeadRail");
            return root;
        }

        /// <summary>Washroom counter with basins and a mirror strip above (some panes broken out).</summary>
        public static GameObject SinkCounter(RoomCtx c, float len)
        {
            var root = Root("SinkCounter");
            int basins = Mathf.Max(1, Mathf.FloorToInt(len / 0.8f));
            len = basins * 0.8f;
            Box(root.transform, new Vector3(0, 0.41f, 0.25f), new Vector3(len, 0.82f, 0.48f), M("VanityBase", new Color(0.3f, 0.29f, 0.27f), 0, 0.3f), null, "Base");
            Box(root.transform, new Vector3(0, 0.84f, 0.28f), new Vector3(len, 0.05f, 0.56f), Laminate, null, "Top");
            Box(root.transform, new Vector3(0, 0.7f, 0.53f), new Vector3(len, 0.25f, 0.03f), Laminate, null, "Apron");
            for (int i = 0; i < basins; i++)
            {
                float x = -len * 0.5f + 0.8f * (i + 0.5f);
                var b = Cyl(root.transform, new Vector3(x, 0.83f, 0.3f), 0.2f, 0.12f, Porcelain, null, "Basin");
                b.transform.localScale = new Vector3(0.42f, 0.07f, 0.34f);
                Cyl(root.transform, new Vector3(x, 0.9f, 0.08f), 0.012f, 0.14f, Steel, null, "Tap");
                Cyl(root.transform, new Vector3(x, 0.96f, 0.13f), 0.01f, 0.12f, Steel, new Vector3(90, 0, 0), "Spout");
                int pane = c.Weighted(new[] { 5f, 2f, 1.5f });   // intact, cracked (tilted), missing
                if (pane == 2) continue;
                Box(root.transform, new Vector3(x, 1.55f, 0.012f), new Vector3(0.74f, 0.9f, 0.012f), Mirror,
                    pane == 1 ? new Vector3(c.Range(-3f, 3f), 0, c.Range(-4f, 4f)) : (Vector3?)null, "Mirror");
            }
            Solid(root, new Vector3(0, 0.43f, 0.28f), new Vector3(len, 0.86f, 0.56f));
            return root;
        }

        public static GameObject Urinal(RoomCtx c)
        {
            var root = Root("Urinal");
            Box(root.transform, new Vector3(0, 0.7f, 0.17f), new Vector3(0.36f, 0.6f, 0.3f), Porcelain);
            Box(root.transform, new Vector3(0, 0.66f, 0.29f), new Vector3(0.26f, 0.42f, 0.08f), Dark);
            Cyl(root.transform, new Vector3(0, 1.2f, 0.06f), 0.015f, 0.4f, Steel);
            return root;
        }

        /// <summary>Counter for receptions, serving lines and shops: front panel facing +Z, staff side behind.</summary>
        public static GameObject Counter(RoomCtx c, float len, float depth = 0.7f, float height = 1.05f)
        {
            var root = Root("Counter");
            Box(root.transform, new Vector3(0, height * 0.5f, 0), new Vector3(len, height, depth), Laminate, null, "Body");
            Box(root.transform, new Vector3(0, height + 0.02f, 0.03f), new Vector3(len + 0.06f, 0.04f, depth + 0.1f), Wood, null, "Top");
            Box(root.transform, new Vector3(0, 0.05f, depth * 0.5f + 0.005f), new Vector3(len, 0.1f, 0.01f), Dark, null, "Kick");
            Solid(root, new Vector3(0, (height + 0.04f) * 0.5f, 0), new Vector3(len, height + 0.04f, depth));
            return root;
        }

        public static GameObject LabBench(RoomCtx c, float len)
        {
            var root = Root("LabBench");
            Box(root.transform, new Vector3(0, 0.44f, 0), new Vector3(len, 0.88f, 0.72f), Laminate, null, "Cabinets");
            Box(root.transform, new Vector3(0, 0.9f, 0), new Vector3(len + 0.04f, 0.035f, 0.76f), EpoxyTop, null, "Top");
            for (float x = -len * 0.5f + 0.3f; x < len * 0.5f - 0.2f; x += 0.6f)
                Box(root.transform, new Vector3(x, 0.46f, 0.362f), new Vector3(0.56f, 0.76f, 0.004f), Dark, null, "DoorLine");
            if (c.Chance(0.6f))
            {
                Box(root.transform, new Vector3(len * 0.3f, 0.905f, 0), new Vector3(0.4f, 0.03f, 0.35f), Dark, null, "Sink");
                Cyl(root.transform, new Vector3(len * 0.3f, 1.05f, -0.2f), 0.012f, 0.3f, Steel, null, "Tap");
            }
            Solid(root, new Vector3(0, 0.46f, 0), new Vector3(len, 0.92f, 0.76f));
            return root;
        }

        public static GameObject Partition(RoomCtx c, float len, float height = 1.45f)
        {
            var root = Root("CubiclePanel");
            Box(root.transform, new Vector3(0, height * 0.5f + 0.05f, 0), new Vector3(len, height, 0.06f), Fabric);
            Box(root.transform, new Vector3(0, height + 0.06f, 0), new Vector3(len, 0.03f, 0.07f), Steel);
            Box(root.transform, new Vector3(-len * 0.5f + 0.15f, 0.03f, 0), new Vector3(0.06f, 0.06f, 0.5f), Steel);
            Box(root.transform, new Vector3(len * 0.5f - 0.15f, 0.03f, 0), new Vector3(0.06f, 0.06f, 0.5f), Steel);
            Solid(root, new Vector3(0, (height + 0.08f) * 0.5f, 0), new Vector3(len, height + 0.08f, 0.08f));
            return root;
        }

        public static GameObject ServerRack(RoomCtx c)
        {
            var root = Root("ServerRack");
            Box(root.transform, new Vector3(0, 1.0f, 0), new Vector3(0.6f, 2.0f, 1.0f), Dark, null, "Cabinet");
            int units = c.Range(4, 11);
            for (int i = 0; i < units; i++)
            {
                float y = 0.25f + i * 0.17f;
                Box(root.transform, new Vector3(0, y, 0.502f), new Vector3(0.48f, 0.12f, 0.01f), M("RackUnit", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.4f), null, "Unit");
                if (c.Chance(0.25f)) Box(root.transform, new Vector3(0.18f, y, 0.509f), new Vector3(0.012f, 0.012f, 0.006f), Led(c.Chance(0.5f)), null, "Led");
            }
            Solid(root, new Vector3(0, 1.0f, 0), new Vector3(0.6f, 2.0f, 1.0f));
            return root;
        }

        public static GameObject Vending(RoomCtx c)
        {
            var root = Root("VendingMachine");
            var body = Plastic(c.Range(0, 4));
            Box(root.transform, new Vector3(0, 0.92f, 0), new Vector3(0.9f, 1.84f, 0.8f), body, null, "Body");
            Box(root.transform, new Vector3(-0.1f, 1.1f, 0.403f), new Vector3(0.6f, 1.2f, 0.01f), Glass, null, "Window");
            for (int r = 0; r < 4; r++)
                for (int k = 0; k < 5; k++)
                    if (c.Chance(0.45f)) Box(root.transform, new Vector3(-0.35f + k * 0.12f, 0.62f + r * 0.27f, 0.36f), new Vector3(0.07f, 0.13f, 0.06f), Plastic(c.Range(0, 4)), null, "Item");
            Box(root.transform, new Vector3(0.3f, 1.2f, 0.405f), new Vector3(0.18f, 0.5f, 0.012f), Dark, null, "Panel");
            Box(root.transform, new Vector3(-0.1f, 0.3f, 0.405f), new Vector3(0.6f, 0.18f, 0.012f), Dark, null, "Flap");
            Solid(root, new Vector3(0, 0.92f, 0), new Vector3(0.9f, 1.84f, 0.8f));
            return root;
        }

        public static GameObject Fridge(RoomCtx c)
        {
            var root = Root("Fridge");
            Box(root.transform, new Vector3(0, 0.88f, 0), new Vector3(0.7f, 1.76f, 0.68f), Appliance);
            Box(root.transform, new Vector3(0, 1.2f, 0.342f), new Vector3(0.68f, 0.006f, 0.006f), Dark);
            Box(root.transform, new Vector3(0.28f, 1.45f, 0.36f), new Vector3(0.025f, 0.3f, 0.03f), Steel);
            Box(root.transform, new Vector3(0.28f, 0.95f, 0.36f), new Vector3(0.025f, 0.3f, 0.03f), Steel);
            Solid(root, new Vector3(0, 0.88f, 0), new Vector3(0.7f, 1.76f, 0.7f));
            return root;
        }

        public static GameObject Washer(RoomCtx c)
        {
            var root = Root("Washer");
            Box(root.transform, new Vector3(0, 0.43f, 0), new Vector3(0.62f, 0.86f, 0.6f), Appliance);
            var door = Cyl(root.transform, new Vector3(0, 0.42f, 0.305f), 0.2f, 0.02f, Glass, new Vector3(90, 0, 0));
            if (c.Chance(0.3f)) door.transform.localPosition += new Vector3(-0.15f, 0, 0.12f);
            Cyl(root.transform, new Vector3(0, 0.42f, 0.302f), 0.23f, 0.01f, Steel, new Vector3(90, 0, 0));
            Box(root.transform, new Vector3(0, 0.8f, 0.24f), new Vector3(0.6f, 0.08f, 0.12f), Dark);
            Solid(root, new Vector3(0, 0.43f, 0), new Vector3(0.62f, 0.86f, 0.62f));
            return root;
        }

        public static GameObject MattressItem(RoomCtx c)
        {
            var root = Root("Mattress");
            Box(root.transform, new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.16f, 1.95f), Mattress(c.Range(0, 3)), new Vector3(c.Range(-1f, 1f), 0, c.Range(-1f, 1f)));
            if (c.Chance(0.5f)) Box(root.transform, new Vector3(c.Range(-0.2f, 0.2f), 0.17f, -0.75f), new Vector3(0.5f, 0.1f, 0.32f), Mattress(0), new Vector3(0, c.Range(-20f, 20f), 0), "Pillow");
            Solid(root, new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.16f, 1.95f));
            return root;
        }

        public static GameObject BunkBed(RoomCtx c)
        {
            var root = Root("BunkBed");
            var frame = LockerPaint(c.Range(0, 4));
            float w = 0.95f, l = 2.0f;
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    Box(root.transform, new Vector3(sx * w * 0.5f, 0.9f, sz * l * 0.5f), new Vector3(0.04f, 1.8f, 0.04f), frame);
            foreach (var y in new[] { 0.35f, 1.35f })
            {
                Box(root.transform, new Vector3(0, y, 0), new Vector3(w, 0.04f, l), frame);
                if (y < 1f || c.Chance(0.75f))
                    Box(root.transform, new Vector3(c.Range(-0.03f, 0.03f), y + 0.1f, c.Range(-0.05f, 0.05f)), new Vector3(0.88f, 0.16f, 1.92f), Mattress(c.Range(0, 3)), new Vector3(0, c.Range(-4f, 4f), 0));
            }
            for (int i = 0; i < 4; i++) Box(root.transform, new Vector3(w * 0.5f + 0.02f, 0.55f + i * 0.25f, l * 0.5f - 0.25f), new Vector3(0.02f, 0.02f, 0.3f), frame);
            Box(root.transform, new Vector3(0, 1.62f, l * 0.5f), new Vector3(w, 0.04f, 0.03f), frame);
            Box(root.transform, new Vector3(0, 1.62f, -l * 0.5f), new Vector3(w, 0.04f, 0.03f), frame);
            Solid(root, new Vector3(0, 0.9f, 0), new Vector3(w, 1.8f, l));
            return root;
        }

        public static GameObject Bench(RoomCtx c, float len)
        {
            var root = Root("Bench");
            Box(root.transform, new Vector3(0, 0.44f, 0), new Vector3(len, 0.04f, 0.32f), Wood);
            foreach (var x in new[] { -len * 0.5f + 0.15f, len * 0.5f - 0.15f })
                Box(root.transform, new Vector3(x, 0.21f, 0), new Vector3(0.04f, 0.42f, 0.28f), Steel);
            Solid(root, new Vector3(0, 0.23f, 0), new Vector3(len, 0.46f, 0.32f));
            return root;
        }

        public static GameObject Pallet(RoomCtx c)
        {
            var root = Root("Pallet");
            for (int i = 0; i < 7; i++) Box(root.transform, new Vector3(-0.54f + i * 0.18f, 0.13f, 0), new Vector3(0.1f, 0.02f, 1.0f), Wood);
            foreach (var z in new[] { -0.45f, 0f, 0.45f }) Box(root.transform, new Vector3(0, 0.06f, z), new Vector3(1.2f, 0.1f, 0.09f), Wood);
            Solid(root, new Vector3(0, 0.07f, 0), new Vector3(1.2f, 0.14f, 1.0f));
            return root;
        }

        public static GameObject Pedestal(RoomCtx c)
        {
            var root = Root("Pedestal");
            float h = c.Range(0.8f, 1.15f);
            Box(root.transform, new Vector3(0, h * 0.5f, 0), new Vector3(0.5f, h, 0.5f), M("Plinth", new Color(0.62f, 0.6f, 0.56f), 0, 0.2f));
            Solid(root, new Vector3(0, h * 0.5f, 0), new Vector3(0.5f, h, 0.5f));
            return root;
        }

        public static GameObject Piano(RoomCtx c)
        {
            var root = Root("UprightPiano");
            var w = M("PianoWood", new Color(0.08f, 0.05f, 0.035f), 0, 0.55f);
            Box(root.transform, new Vector3(0, 0.62f, 0), new Vector3(1.48f, 1.24f, 0.55f), w);
            Box(root.transform, new Vector3(0, 0.72f, 0.38f), new Vector3(1.3f, 0.06f, 0.22f), Porcelain, null, "Keys");
            Box(root.transform, new Vector3(0, 0.76f, 0.38f), new Vector3(1.3f, 0.02f, 0.2f), w, new Vector3(c.Chance(0.5f) ? -70 : 0, 0, 0), "Lid");
            Solid(root, new Vector3(0, 0.62f, 0.05f), new Vector3(1.48f, 1.24f, 0.65f));
            return root;
        }

        public static GameObject Photocopier(RoomCtx c)
        {
            var root = Root("Photocopier");
            Box(root.transform, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 1.0f, 0.6f), Appliance);
            Box(root.transform, new Vector3(0, 1.03f, 0), new Vector3(0.6f, 0.06f, 0.55f), Dark, new Vector3(c.Chance(0.3f) ? -40 : 0, 0, 0), "Lid");
            Box(root.transform, new Vector3(0.38f, 0.85f, 0), new Vector3(0.18f, 0.03f, 0.3f), Appliance, null, "Tray");
            Solid(root, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 1.0f, 0.6f));
            return root;
        }

        public static GameObject MusicStand(RoomCtx c)
        {
            var root = Root("MusicStand");
            Cyl(root.transform, new Vector3(0, 0.55f, 0), 0.01f, 1.1f, Dark);
            Box(root.transform, new Vector3(0, 1.15f, 0.03f), new Vector3(0.48f, 0.3f, 0.01f), Dark, new Vector3(-20, 0, 0));
            foreach (var a in new[] { 0f, 120f, 240f }) Box(root.transform, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.15f, 0.02f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.15f), new Vector3(0.015f, 0.015f, 0.32f), Dark, new Vector3(0, a, 0));
            return root;
        }

        public static GameObject Rug(RoomCtx c)
        {
            var root = Root("Rug");
            Box(root.transform, new Vector3(0, 0.004f, 0), new Vector3(c.Range(1.4f, 2.4f), 0.008f, c.Range(1.0f, 1.7f)), Cloth(c.Range(0, 4)));
            NoShadows(root);
            return root;
        }

        public static GameObject WaterFountain(RoomCtx c)
        {
            var root = Root("WaterFountain");
            Box(root.transform, new Vector3(0, 0.85f, 0.18f), new Vector3(0.45f, 0.25f, 0.36f), Steel);
            Box(root.transform, new Vector3(0, 0.37f, 0.06f), new Vector3(0.3f, 0.74f, 0.12f), Steel);
            Solid(root, new Vector3(0, 0.5f, 0.18f), new Vector3(0.45f, 1.0f, 0.36f));
            return root;
        }

        public static GameObject CellBars(RoomCtx c, float len)
        {
            var root = Root("CellBars");
            int n = Mathf.FloorToInt(len / 0.14f);
            for (int i = 0; i <= n; i++)
            {
                if (i > n / 2 - 4 && i < n / 2 + 4) continue;   // open gate gap
                Cyl(root.transform, new Vector3(-len * 0.5f + i * len / n, 1.25f, 0), 0.015f, 2.5f, Dark);
            }
            Box(root.transform, new Vector3(0, 2.48f, 0), new Vector3(len, 0.05f, 0.05f), Dark);
            Box(root.transform, new Vector3(0, 1.1f, 0), new Vector3(len, 0.04f, 0.04f), Dark);
            return root;
        }

        // ------------------------------------------------------------------ wall pieces (pivot = back face)
        public static GameObject NoticeBoard(RoomCtx c)
        {
            var root = Root("NoticeBoard");
            float w = c.Range(0.8f, 1.6f), h = c.Range(0.6f, 1.0f);
            Box(root.transform, new Vector3(0, 0, 0.015f), new Vector3(w + 0.06f, h + 0.06f, 0.03f), Wood);
            Box(root.transform, new Vector3(0, 0, 0.032f), new Vector3(w, h, 0.006f), Cork);
            int n = c.Range(3, 12);
            for (int i = 0; i < n; i++)
            {
                float pw = c.Range(0.12f, 0.3f), ph = c.Range(0.15f, 0.3f);
                Flat(root.transform, new Vector3(c.Range(-w * 0.5f + pw * 0.5f, w * 0.5f - pw * 0.5f), c.Range(-h * 0.5f + ph * 0.5f, h * 0.5f - ph * 0.5f), 0.037f + i * 0.0006f),
                    new Vector3(pw, 1f, ph), Paper(c.Range(0, 5)), Sheet(), new Vector3(90, 0, c.Range(-10f, 10f)), "Notice");   // sheet faces up; +90 on X turns it to face +Z
            }
            NoShadows(root);
            return root;
        }

        public static GameObject Board(RoomCtx c, float w, bool chalk)
        {
            var root = Root(chalk ? "Blackboard" : "Whiteboard");
            float h = 1.2f;
            Box(root.transform, new Vector3(0, 0, 0.012f), new Vector3(w + 0.08f, h + 0.08f, 0.024f), chalk ? Wood : Steel);
            Box(root.transform, new Vector3(0, 0, 0.026f), new Vector3(w, h, 0.004f), chalk ? Chalk : WhiteBoard);
            Box(root.transform, new Vector3(0, -h * 0.5f - 0.05f, 0.05f), new Vector3(w, 0.025f, 0.08f), chalk ? Wood : Steel, null, "Tray");
            // smudged leftovers of writing
            int scrawls = c.Range(0, 7);
            var ink = chalk ? M("ChalkMark", new Color(0.55f, 0.56f, 0.52f), 0, 0.1f) : M("MarkerInk", new Color(0.08f, 0.1f, 0.2f), 0, 0.4f);
            for (int i = 0; i < scrawls; i++)
                Box(root.transform, new Vector3(c.Range(-w * 0.4f, w * 0.4f), c.Range(-h * 0.35f, h * 0.35f), 0.0285f), new Vector3(c.Range(0.15f, 0.7f), 0.012f, 0.001f), ink, new Vector3(0, 0, c.Range(-6f, 6f)), "Scrawl");
            NoShadows(root);
            return root;
        }

        public static GameObject MirrorPanel(RoomCtx c, float w)
        {
            var root = Root("Mirror");
            Box(root.transform, new Vector3(0, 0, 0.006f), new Vector3(w, 0.9f, 0.012f), Mirror, new Vector3(0, 0, c.Chance(0.3f) ? c.Range(-4f, 4f) : 0));
            return root;
        }
    }
}
