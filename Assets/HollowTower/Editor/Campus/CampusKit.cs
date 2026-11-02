using System.Collections.Generic;
using HollowTower.Campus;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Building blocks for the final level (CampusBuilder): materials from the project's textures, boxes whose UVs are
    /// in metres (so bricks and tiles keep their real size on any wall), walls with door and window openings cut out,
    /// glazing, props found by name in the prop library or the converted campus models, and lamps. Meshes are shared
    /// between boxes of the same size and saved into one asset.
    /// </summary>
    public static class CampusKit
    {
        public const string Dir = "Assets/Campus";
        public const string MatDir = Dir + "/Materials";
        public const string GenDir = Dir + "/Generated";
        public const string MeshAsset = GenDir + "/CampusMeshes.asset";

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<Material, float> tileOf = new Dictionary<Material, float>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, GameObject> propCache = new Dictionary<string, GameObject>();
        public static readonly List<string> Missing = new List<string>();
        public static int BoxCount;

        public static void Begin()
        {
            Folder(Dir, "Materials"); Folder(Dir, "Generated");
            materials.Clear(); tileOf.Clear(); meshes.Clear(); propCache.Clear(); Missing.Clear(); BoxCount = 0;
            AssetDatabase.DeleteAsset(MeshAsset);
        }

        /// <summary>Saves every generated mesh into one asset (call before saving the scene).</summary>
        public static void End()
        {
            Mesh main = null;
            foreach (var m in meshes.Values)
            {
                if (main == null) { main = m; AssetDatabase.CreateAsset(m, MeshAsset); }
                else AssetDatabase.AddObjectToAsset(m, main);
            }
            AssetDatabase.SaveAssets();
        }

        public static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------ materials
        static Shader Lit { get { return Shader.Find("Universal Render Pipeline/Lit"); } }

        static Material MatAsset(string name)
        {
            Material m;
            if (materials.TryGetValue(name, out m)) return m;
            string path = MatDir + "/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = Lit;
            materials[name] = m;
            return m;
        }

        static void SmallTexture(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
        }

        static Texture2D LoadTex(string path, bool normal)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Missing.Add(path); return null; }
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A textured material; tile = metres one copy of the texture covers on a box.</summary>
        public static Material Tex(string name, string color, string normal, float tile, Color tint, float smooth = 0.2f, float metal = 0f)
        {
            var m = MatAsset(name);
            m.SetTexture("_BaseMap", LoadTex(color, false));
            m.SetColor("_BaseColor", tint);
            var n = LoadTex(normal, true);
            m.SetTexture("_BumpMap", n);
            if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            tileOf[m] = tile;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Flat(string name, Color c, float smooth = 0.3f, float metal = 0f)
        {
            var m = MatAsset(name);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.DisableKeyword("_NORMALMAP");
            m.enableInstancing = true;
            tileOf[m] = 1f;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Glow(string name, Color c, float strength)
        {
            var m = Flat(name, c * 0.3f, 0.5f);
            m.SetColor("_EmissionColor", c * strength);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Glass(string name, Color c, float smooth = 0.95f)
        {
            var m = Flat(name, c, smooth, 0f);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static float TileOf(Material m) { float t; return m != null && tileOf.TryGetValue(m, out t) ? t : 1f; }

        // ------------------------------------------------------------------ boxes
        static Mesh BoxMesh(Vector3 s, float tile)
        {
            string key = s.x.ToString("F2") + "_" + s.y.ToString("F2") + "_" + s.z.ToString("F2") + "_" + tile.ToString("F2");
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh)) return mesh;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            Vector3 h = s * 0.5f;
            // each face: normal, the two in-plane axes (u, v) and their sizes
            void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sv)
            {
                int b = v.Count;
                Vector3 c = Vector3.Scale(normal, h);
                Vector3 hu = u * su * 0.5f, hw = w * sv * 0.5f;
                v.Add(c - hu - hw); v.Add(c + hu - hw); v.Add(c + hu + hw); v.Add(c - hu + hw);
                for (int i = 0; i < 4; i++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su / tile, 0)); uv.Add(new Vector2(su / tile, sv / tile)); uv.Add(new Vector2(0, sv / tile));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            Face(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
            Face(Vector3.left, Vector3.back, Vector3.up, s.z, s.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
            Face(Vector3.down, Vector3.right, Vector3.back, s.x, s.z);
            Face(Vector3.forward, Vector3.left, Vector3.up, s.x, s.y);
            Face(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
            mesh = new Mesh { name = "Box_" + key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            meshes[key] = mesh;
            return mesh;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m, bool collider = true)
        {
            return Box(name, parent, center, size, Quaternion.identity, m, collider);
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Quaternion rot, Material m, bool collider = true)
        {
            if (size.x <= 0.001f || size.y <= 0.001f || size.z <= 0.001f) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, TileOf(m));
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) { var c = go.AddComponent<BoxCollider>(); c.size = size; }
            BoxCount++;
            return go;
        }

        /// <summary>A box spanning two corners (min, max) in the parent's space.</summary>
        public static GameObject Span(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool collider = true)
        {
            return Box(name, parent, (min + max) * 0.5f, max - min, m, collider);
        }

        public static GameObject Cyl(string name, Transform parent, Vector3 baseCentre, float radius, float height, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCentre + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (collider) { var c = go.AddComponent<MeshCollider>(); c.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; c.convex = true; }
            return go;
        }

        /// <summary>Lettering that sits in the world like paint (the font's own material draws over everything).
        /// pos is the centre; the text reads from the side its facing vector points to.</summary>
        public static GameObject Text(Transform parent, string text, Vector3 pos, Vector3 facing, float height, Color c)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var m = MatAsset("CM_Text_" + ColorUtility.ToHtmlStringRGB(c));
            m.shader = Shader.Find("Universal Render Pipeline/Unlit");
            m.SetTexture("_BaseMap", font.material.mainTexture);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_AlphaClip", 1f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(-facing, Vector3.up);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = height * 0.15f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white; tm.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>A one-level LOD group that stops drawing the object once it is under `ratio` of the screen height -
        /// the small props and the trees fade out at distance (the fog hides it).</summary>
        public static void Cull(GameObject go, float ratio)
        {
            if (go == null || go.GetComponent<LODGroup>() != null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var g = go.AddComponent<LODGroup>();
            // dissolve out (URP's dithered cross-fade) rather than pop, a little further into the fog than asked
            g.fadeMode = LODFadeMode.CrossFade; g.animateCrossFading = true;
            g.SetLODs(new[] { new LOD(ratio * 0.6f, rs) });
            g.RecalculateBounds();
        }

        // ------------------------------------------------------------------ walls
        public struct Hole
        {
            public float a0, a1, y0, y1;
            public Hole(float a0, float a1, float y0, float y1) { this.a0 = a0; this.a1 = a1; this.y0 = y0; this.y1 = y1; }
        }

        /// <summary>A wall in the x/y plane at z, from x0 to x1, with openings (a0..a1 along x, y0..y1 up).</summary>
        public static void WallZ(Transform p, string name, float z, float x0, float x1, float y0, float y1, float t, Material m, params Hole[] holes)
        {
            Wall(p, name, false, z, x0, x1, y0, y1, t, m, holes);
        }

        /// <summary>A wall in the z/y plane at x, from z0 to z1, with openings (a0..a1 along z).</summary>
        public static void WallX(Transform p, string name, float x, float z0, float z1, float y0, float y1, float t, Material m, params Hole[] holes)
        {
            Wall(p, name, true, x, z0, z1, y0, y1, t, m, holes);
        }

        static void Wall(Transform p, string name, bool alongZ, float at, float a0, float a1, float y0, float y1, float t, Material m, Hole[] holes)
        {
            // openings may overlap along the wall (a door under a tall window): cut the wall into strips at every
            // opening's edge, and fill each strip round whichever openings cross it; neighbours that match are merged
            var hs = new List<Hole>(holes ?? new Hole[0]);
            var cuts = new List<float> { a0, a1 };
            foreach (var h in hs) { cuts.Add(Mathf.Clamp(h.a0, a0, a1)); cuts.Add(Mathf.Clamp(h.a1, a0, a1)); }
            cuts.Sort();
            System.Func<float, float, List<Vector2>> fill = (b0, b1) =>
            {
                var gaps = new List<Vector2>();
                foreach (var h in hs) if (h.a0 <= b0 + 0.001f && h.a1 >= b1 - 0.001f) gaps.Add(new Vector2(Mathf.Max(h.y0, y0), Mathf.Min(h.y1, y1)));
                gaps.Sort((x, y) => x.x.CompareTo(y.x));
                var solid = new List<Vector2>();
                float cur = y0;
                foreach (var g in gaps)
                {
                    if (g.x > cur + 0.01f) solid.Add(new Vector2(cur, g.x));
                    cur = Mathf.Max(cur, g.y);
                }
                if (y1 > cur + 0.01f) solid.Add(new Vector2(cur, y1));
                return solid;
            };
            float runStart = a0; List<Vector2> run = null;
            System.Action<float> flush = end =>
            {
                if (run == null || end - runStart < 0.01f) return;
                foreach (var v in run)
                {
                    Vector3 min = alongZ ? new Vector3(at - t * 0.5f, v.x, runStart) : new Vector3(runStart, v.x, at - t * 0.5f);
                    Vector3 max = alongZ ? new Vector3(at + t * 0.5f, v.y, end) : new Vector3(end, v.y, at + t * 0.5f);
                    Span(name, p, min, max, m);
                }
            };
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float b0 = cuts[i], b1 = cuts[i + 1];
                if (b1 - b0 < 0.005f) continue;
                var f = fill(b0, b1);
                bool same = run != null && run.Count == f.Count;
                if (same) for (int k = 0; k < f.Count; k++) if (Mathf.Abs(run[k].x - f[k].x) > 0.001f || Mathf.Abs(run[k].y - f[k].y) > 0.001f) { same = false; break; }
                if (!same) { flush(b0); run = f; runStart = b0; }
            }
            flush(a1);
        }

        /// <summary>A window filling an opening: a frame and (unless broken) a pane of glass. Along z if alongZ.</summary>
        public static void Window(Transform p, bool alongZ, float at, float a0, float a1, float y0, float y1, Material frame, Material glass, bool broken)
        {
            float f = 0.07f, d = 0.12f;
            System.Action<string, float, float, float, float, float, Material, bool> slab = (nm, b0, b1, c0, c1, thick, mat, col) =>
            {
                Vector3 min = alongZ ? new Vector3(at - thick * 0.5f, c0, b0) : new Vector3(b0, c0, at - thick * 0.5f);
                Vector3 max = alongZ ? new Vector3(at + thick * 0.5f, c1, b1) : new Vector3(b1, c1, at + thick * 0.5f);
                var go = Span(nm, p, min, max, mat, col);
                if (go != null && mat == glass) go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            };
            slab("Frame", a0, a1, y0, y0 + f, d, frame, false);
            slab("Frame", a0, a1, y1 - f, y1, d, frame, false);
            slab("Frame", a0, a0 + f, y0, y1, d, frame, false);
            slab("Frame", a1 - f, a1, y0, y1, d, frame, false);
            slab("Mullion", (a0 + a1) * 0.5f - f * 0.5f, (a0 + a1) * 0.5f + f * 0.5f, y0, y1, d * 0.8f, frame, false);
            if (!broken) slab("Glass", a0 + f, a1 - f, y0 + f, y1 - f, 0.02f, glass, true);
            else slab("Glass", a0 + f, (a0 + a1) * 0.5f, y0 + f, y0 + (y1 - y0) * 0.35f, 0.02f, glass, false);   // a shard left in the corner
        }

        /// <summary>The converted campus models come with their materials embedded and opaque: give each one an editable
        /// copy, with alpha clipping (and both faces drawn) where its texture has a cut-out - leaves, grass cards - and a
        /// dark stone colour for the untextured statue.</summary>
        public static int FixModelMaterials()
        {
            Folder(MatDir, "Models");
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Campus/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) continue;
                string model = System.IO.Path.GetFileNameWithoutExtension(path);
                bool changed = false;
                // embedded materials get a copy and a remap; already remapped ones (a rebuild) are set up again in place
                var work = new List<KeyValuePair<string, Material>>();
                foreach (var kv in mi.GetExternalObjectMap())
                    if (kv.Key.type == typeof(Material) && kv.Value is Material) work.Add(new KeyValuePair<string, Material>(null, (Material)kv.Value));
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var src = o as Material;
                    if (src == null) continue;
                    string mp = MatDir + "/Models/" + model + "_" + string.Concat(src.name.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(' ', '_') + ".mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(mp);
                    if (copy == null) { copy = new Material(src); AssetDatabase.CreateAsset(copy, mp); }
                    else copy.CopyPropertiesFromMaterial(src);
                    work.Add(new KeyValuePair<string, Material>(src.name, copy));
                }
                foreach (var w in work)
                {
                    var m = w.Value;
                    m.shader = Lit;
                    // texture sets the converter wrote next to the model (<material>_base / _normal), or the model's
                    // only texture when its material lost the link to it
                    string folder = System.IO.Path.GetDirectoryName(path).Replace(System.IO.Path.DirectorySeparatorChar, '/');
                    // (a split model is named <folder>_N, its textures <folder>_<material>)
                    string setName = m.name.StartsWith(model + "_") ? System.IO.Path.GetFileName(folder) + m.name.Substring(model.Length) : m.name;
                    var setBase = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + m.name + "_base.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + setName + "_base.png");
                    if (setBase != null)
                    {
                        m.SetTexture("_BaseMap", setBase); m.SetColor("_BaseColor", Color.white);
                        var setNormal = AssetDatabase.GetAssetPath(setBase).Replace("_base.png", "_normal.png");
                        var nrm = LoadTex(AssetDatabase.LoadAssetAtPath<Texture2D>(setNormal) != null ? setNormal : null, true);
                        m.SetTexture("_BumpMap", nrm);
                        if (nrm != null) m.EnableKeyword("_NORMALMAP");
                        SmallTexture(AssetDatabase.GetAssetPath(setBase));
                    }
                    else if (m.GetTexture("_BaseMap") == null)
                    {
                        var images = new List<string>();
                        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                        {
                            string tp = AssetDatabase.GUIDToAssetPath(g);
                            if (!tp.Contains("_normal") && !tp.Contains("Normal") && !tp.Contains("_cutout")) images.Add(tp);
                        }
                        if (images.Count == 1) { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(images[0])); m.SetColor("_BaseColor", Color.white); }
                    }
                    if (model == "sofa_sectional")
                    {
                        // the sofa came untextured: CC0 leather (ambientCG Leather026), dark oxblood
                        m.SetTexture("_BaseMap", LoadTex("Assets/Campus/Textures/Leather026/Leather026_1K-JPG_Color.jpg", false));
                        m.SetTexture("_BumpMap", LoadTex("Assets/Campus/Textures/Leather026/Leather026_1K-JPG_NormalGL.jpg", true));
                        m.EnableKeyword("_NORMALMAP");
                        m.SetColor("_BaseColor", new Color(0.42f, 0.24f, 0.2f));
                        m.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
                        m.SetFloat("_Smoothness", 0.45f);
                    }
                    var tex = m.GetTexture("_BaseMap") as Texture2D;
                    if (tex != null)
                    {
                        // a leaf atlas whose alpha was lost, or kept in a separate opacity map: the converter's cut-out copy
                        string tp = AssetDatabase.GetAssetPath(tex);
                        string cutPath = tp.Substring(0, tp.LastIndexOf('.')) + "_cutout.png";
                        var cutTex = AssetDatabase.LoadAssetAtPath<Texture2D>(cutPath);
                        if (cutTex != null) { tex = cutTex; m.SetTexture("_BaseMap", tex); }
                    }
                    var ti = tex != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter : null;
                    bool cut = ti != null && ti.DoesSourceTextureHaveAlpha();
                    m.SetFloat("_Surface", 0f); m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetFloat("_ZWrite", 1f); m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f);
                    m.SetOverrideTag("RenderType", cut ? "TransparentCutout" : "Opaque");
                    m.SetFloat("_AlphaClip", cut ? 1f : 0f);
                    if (cut) { m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cutoff", 0.45f); m.SetFloat("_Cull", 0f); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; }
                    else { m.DisableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 2f); m.renderQueue = -1; }
                    if (cut && ti != null && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
                    if (model == "statue_gargoyle" && m.GetTexture("_BaseMap") == null) m.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.21f));
                    if (model.StartsWith("tree_") && m.name.EndsWith("_bark")) m.SetColor("_BaseColor", new Color(0.5f, 0.45f, 0.4f));
                    m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.35f));
                    // glTF leaves came with a white emission that made them glow at night; only lamp parts may glow
                    if (cut || !model.StartsWith("lamp")) { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; }
                    m.enableInstancing = true;
                    EditorUtility.SetDirty(m);
                    if (w.Key != null) { mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), w.Key), m); changed = true; }
                    n++;
                }
                if (changed) mi.SaveAndReimport();
            }
            return n;
        }

        // ------------------------------------------------------------------ props
        static readonly string[] PropRoots = { "Assets/Campus/Models", "Assets/HollowTowerRemastered/Props", "Assets/Tutorial/ThirdParty" };

        public static GameObject Find(string name)
        {
            GameObject g;
            if (propCache.TryGetValue(name, out g)) return g;
            string best = null;
            foreach (var guid in AssetDatabase.FindAssets(name, PropRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                bool asset = path.EndsWith(".prefab") || path.EndsWith(".fbx") || path.EndsWith(".gltf") || path.EndsWith(".glb");
                if (!asset) continue;
                if (string.Equals(file, name, System.StringComparison.OrdinalIgnoreCase)) { best = path; break; }
                if (best == null && file.StartsWith(name, System.StringComparison.OrdinalIgnoreCase)) best = path;
            }
            g = best != null ? AssetDatabase.LoadAssetAtPath<GameObject>(best) : null;
            if (g == null && !Missing.Contains(name)) Missing.Add(name);
            propCache[name] = g;
            return g;
        }

        static readonly Dictionary<Material, Material> grimeCache = new Dictionary<Material, Material>();
        /// <summary>Swaps a model's materials for dusty, faded copies (saved next to the originals, made once each):
        /// abandoned cars should not shine like a showroom. amount 0..1: how much colour and gloss is lost.</summary>
        public static void Grime(GameObject go, float amount)
        {
            if (go == null) return;
            Folder(MatDir, "Models");
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || m.name.EndsWith("_grime")) continue;
                    Material g;
                    if (!grimeCache.TryGetValue(m, out g) || g == null)
                    {
                        string path = MatDir + "/Models/" + string.Concat(m.name.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(' ', '_') + "_grime.mat";
                        g = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (g == null) { g = new Material(m) { name = m.name + "_grime" }; AssetDatabase.CreateAsset(g, path); }
                        else g.CopyPropertiesFromMaterial(m);
                        g.shader = Lit;
                        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                        float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                        Color dust = new Color(0.36f, 0.34f, 0.3f);
                        // faded towards a dusty grey, darker overall; glass and chrome lose their gloss
                        Color o = Color.Lerp(c, Color.Lerp(new Color(lum, lum, lum), dust, 0.5f), amount * 0.7f) * (1f - amount * 0.35f);
                        o.a = c.a;
                        g.SetColor("_BaseColor", o);
                        g.SetFloat("_Smoothness", (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.5f) * (1f - amount * 0.7f));
                        g.SetFloat("_Metallic", (m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f) * (1f - amount * 0.5f));
                        g.DisableKeyword("_EMISSION"); g.SetColor("_EmissionColor", Color.black);
                        g.enableInstancing = true;
                        EditorUtility.SetDirty(g);
                        grimeCache[m] = g;
                    }
                    mats[i] = g;
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Places a prop or model (bottom at pos, turned yaw degrees). Gets a box collider if it has none.</summary>
        public static GameObject Prop(string name, Transform parent, Vector3 pos, float yaw, float scale = 1f, bool collide = true)
        {
            var src = Find(name);
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            go.transform.localScale = go.transform.localScale * scale;
            if (collide && go.GetComponentInChildren<Collider>() == null)
            {
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    var c = go.AddComponent<BoxCollider>();
                    c.center = go.transform.InverseTransformPoint(b.center);
                    var sz = go.transform.InverseTransformVector(b.size);
                    c.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                }
            }
            return go;
        }

        // ------------------------------------------------------------------ lights
        /// <summary>URP point lights fall off with the square of distance: the values below are tuned by eye and scaled
        /// up here so the pools of light actually reach the ground at night.</summary>
        public const float LampGain = 7.5f;

        public static CampusLamp Lamp(Transform parent, Vector3 pos, Color c, float range, float intensity, Renderer[] heads = null,
                                      float flicker = 0f, bool powered = true, bool shadows = false, bool spotDown = false)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity * LampGain;
            if (spotDown)
            {
                // a ceiling fitting throws its light down: the floor and desks lit, not a hot patch on the ceiling
                l.type = LightType.Spot; l.spotAngle = 165f; l.innerSpotAngle = 100f; l.range *= 1.2f;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            var lamp = go.AddComponent<CampusLamp>();
            lamp.lightSource = l; lamp.color = c; lamp.heads = heads; lamp.flicker = flicker; lamp.powered = powered;
            return lamp;
        }

        /// <summary>Marks a finished area static (batched, occludes) - everything except what moves.</summary>
        public static void MarkStatic(GameObject root)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponentInParent<CampusKeyDoor>() != null || t.GetComponentInParent<CampusGate>() != null || t.GetComponentInParent<CampusArrival>() != null
                    || t.GetComponentInParent<InteractiveDoor>() != null || t.GetComponentInParent<PartyItem>() != null
                    || t.GetComponentInParent<HiddenWallDoor>() != null || t.GetComponentInParent<ServiceGate>() != null || t.GetComponentInParent<CarAlarm>() != null
                    || t.GetComponentInParent<Puzzles.WorldPuzzle>() != null || t.GetComponentInParent<Puzzles.WorldPuzzlePart>() != null || t.name.StartsWith("Clock_Pivot") || t.GetComponentInParent<PartyItem>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }
    }
}
