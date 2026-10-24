using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>Box-built props that give the rooms their institutional character (pivot rules as in ProcKit).</summary>
    public static partial class ProcKit
    {
        static Material GlassClear
        {
            get
            {
                var m = M("GlassClear", new Color(0.55f, 0.62f, 0.65f), 0f, 0.95f);
                if (m.GetFloat("_Surface") != 1f)
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.65f, 0.16f));
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    m.SetFloat("_EnvironmentReflections", 0f);
                    m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    EditorUtility.SetDirty(m);
                }
                return m;
            }
        }
        static Material Gold { get { return M("Gold", new Color(0.7f, 0.55f, 0.22f), 0.9f, 0.6f); } }
        static Material Ink { get { return M("PrintInk", new Color(0.08f, 0.08f, 0.09f), 0f, 0.2f); } }
        static Material Soil { get { return M("SoilBed", new Color(0.62f, 0.52f, 0.42f), 0f, 0.05f, Tex + "Soil_Albedo.png", Tex + "Soil_Normal.png"); } }
        static Material Frosted { get { return M("FrostedWindow", new Color(0.32f, 0.36f, 0.4f), 0f, 0.7f, null, null, new Color(0.11f, 0.13f, 0.17f)); } }
        static Material Print(int i)
        {
            Color[] c = { new Color(0.55f, 0.12f, 0.1f), new Color(0.12f, 0.25f, 0.5f), new Color(0.15f, 0.4f, 0.2f), new Color(0.65f, 0.5f, 0.12f),
                          new Color(0.35f, 0.2f, 0.45f), new Color(0.1f, 0.45f, 0.45f) };
            int k = i % c.Length;
            return M("Print" + k, c[k], 0, 0.25f);
        }

        // ------------------------------------------------------------------ wall pieces (pivot = back face)
        /// <summary>A printed poster: coloured header, a picture block and lines of text.</summary>
        public static GameObject Poster(RoomCtx c)
        {
            var root = Root("Poster");
            bool land = c.Chance(0.3f);
            float w = land ? 0.84f : 0.59f, h = land ? 0.59f : 0.84f;
            var paper = Paper(c.Chance(0.7f) ? 0 : 1);
            Flat(root.transform, new Vector3(0, 0, 0.004f), new Vector3(w, 1, h), paper, Sheet(), new Vector3(90, 0, 0), "Sheet");
            var accent = Print(c.Range(0, 6));
            Flat(root.transform, new Vector3(0, h * 0.38f, 0.005f), new Vector3(w * 0.9f, 1, h * 0.14f), accent, Sheet(), new Vector3(90, 0, 0), "Header");
            Flat(root.transform, new Vector3(c.Range(-0.1f, 0.1f) * w, h * 0.08f, 0.005f), new Vector3(w * c.Range(0.45f, 0.8f), 1, h * 0.35f), Print(c.Range(0, 6)), Sheet(), new Vector3(90, 0, 0), "Picture");
            for (int i = 0; i < 5; i++)
                Flat(root.transform, new Vector3(-w * 0.05f, -h * 0.18f - i * h * 0.055f, 0.005f), new Vector3(w * c.Range(0.5f, 0.8f), 1, h * 0.015f), Ink, Sheet(), new Vector3(90, 0, 0), "Text");
            return root;
        }

        public static GameObject PeriodicTable(RoomCtx c)
        {
            var root = Root("PeriodicTable");
            float w = 1.6f, h = 1.0f;
            Box(root.transform, new Vector3(0, 0, 0.006f), new Vector3(w + 0.04f, h + 0.04f, 0.012f), Paper(0));
            float cell = w / 19f;
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 18; col++)
                {
                    bool present = row == 0 ? (col == 0 || col == 17) : row < 3 ? (col < 2 || col > 11) : true;
                    if (!present) continue;
                    int group = col < 2 ? 0 : col < 12 ? 1 : col < 16 ? 2 : col == 16 ? 3 : 4;
                    Flat(root.transform, new Vector3(-w * 0.5f + cell * (col + 1), h * 0.38f - row * cell * 1.05f, 0.0125f), new Vector3(cell * 0.86f, 1, cell * 0.86f), Print(group), Sheet(), new Vector3(90, 0, 0), "Element");
                }
            for (int col = 0; col < 14; col++)
                for (int row = 0; row < 2; row++)
                    Flat(root.transform, new Vector3(-w * 0.5f + cell * (col + 3.5f), -h * 0.33f - row * cell * 1.05f, 0.0125f), new Vector3(cell * 0.86f, 1, cell * 0.86f), Print(5), Sheet(), new Vector3(90, 0, 0), "Element");
            Flat(root.transform, new Vector3(0, h * 0.46f, 0.0125f), new Vector3(w * 0.5f, 1, 0.04f), Ink, Sheet(), new Vector3(90, 0, 0), "Title");
            return root;
        }

        public static GameObject WorldMap(RoomCtx c)
        {
            var root = Root("WorldMap");
            float w = 1.8f, h = 1.1f;
            Box(root.transform, new Vector3(0, 0, 0.008f), new Vector3(w + 0.06f, h + 0.06f, 0.016f), Wood);
            Flat(root.transform, new Vector3(0, 0, 0.0165f), new Vector3(w, 1, h), M("MapSea", new Color(0.32f, 0.45f, 0.55f), 0, 0.2f), Sheet(), new Vector3(90, 0, 0), "Sea");
            var land = M("MapLand", new Color(0.55f, 0.52f, 0.36f), 0, 0.2f);
            // rough continents: clusters of overlapping patches
            Vector2[] blobs = { new Vector2(-0.55f, 0.2f), new Vector2(-0.4f, -0.25f), new Vector2(0.0f, 0.25f), new Vector2(0.05f, -0.1f), new Vector2(0.4f, 0.2f), new Vector2(0.55f, -0.3f) };
            foreach (var b in blobs)
                for (int i = 0; i < 4; i++)
                    Flat(root.transform, new Vector3((b.x + c.Range(-0.08f, 0.08f)) * w, (b.y + c.Range(-0.08f, 0.08f)) * h, 0.017f + i * 0.0003f),
                        new Vector3(c.Range(0.08f, 0.2f) * w, 1, c.Range(0.08f, 0.2f) * h), land, Sheet(), new Vector3(90, 0, c.Range(-30f, 30f)), "Land");
            Box(root.transform, new Vector3(0, h * 0.5f + 0.05f, 0.03f), new Vector3(w + 0.1f, 0.03f, 0.03f), Wood, null, "Rail");
            return root;
        }

        /// <summary>A tall cloth banner with the college colours and an emblem.</summary>
        public static GameObject Banner(RoomCtx c)
        {
            var root = Root("Banner");
            var cloth = c.Chance(0.5f) ? M("BannerMaroon", new Color(0.32f, 0.06f, 0.08f), 0, 0.1f) : M("BannerNavy", new Color(0.07f, 0.1f, 0.26f), 0, 0.1f);
            Box(root.transform, new Vector3(0, 0, 0.01f), new Vector3(0.7f, 1.8f, 0.006f), cloth, new Vector3(0, 0, c.Chance(0.3f) ? c.Range(-3f, 3f) : 0));
            Cyl(root.transform, new Vector3(0, 0.92f, 0.02f), 0.012f, 0.82f, Gold, new Vector3(0, 0, 90), "Pole");
            Flat(root.transform, new Vector3(0, 0.35f, 0.0135f), new Vector3(0.36f, 1, 0.42f), Gold, Sheet(), new Vector3(90, 0, 45), "Emblem");
            Flat(root.transform, new Vector3(0, -0.35f, 0.0135f), new Vector3(0.5f, 1, 0.05f), Gold, Sheet(), new Vector3(90, 0, 0), "Stripe");
            return root;
        }

        /// <summary>Frosted window with a little light behind it and venetian blinds, some slats hanging loose.</summary>
        public static GameObject Window(RoomCtx c)
        {
            var root = Root("Window");
            float w = 1.5f, h = 1.5f;
            var frame = M("WindowFrame", new Color(0.42f, 0.42f, 0.4f), 0.4f, 0.4f);
            Box(root.transform, new Vector3(0, 0, 0.004f), new Vector3(w, h, 0.008f), Frosted, null, "Glass");
            Box(root.transform, new Vector3(0, h * 0.5f, 0.03f), new Vector3(w + 0.1f, 0.06f, 0.06f), frame);
            Box(root.transform, new Vector3(0, -h * 0.5f, 0.06f), new Vector3(w + 0.16f, 0.05f, 0.12f), frame, null, "Sill");
            Box(root.transform, new Vector3(-w * 0.5f, 0, 0.03f), new Vector3(0.06f, h, 0.06f), frame);
            Box(root.transform, new Vector3(w * 0.5f, 0, 0.03f), new Vector3(0.06f, h, 0.06f), frame);
            Box(root.transform, new Vector3(0, 0, 0.02f), new Vector3(0.04f, h, 0.03f), frame);
            Box(root.transform, new Vector3(0, h * 0.1f, 0.02f), new Vector3(w, 0.04f, 0.03f), frame);
            // blinds drawn to a random height, a few slats skewed
            float down = c.Range(0.15f, 0.9f);
            var slat = M("Blind", new Color(0.6f, 0.58f, 0.52f), 0, 0.3f);
            int slats = Mathf.FloorToInt(h * down / 0.06f);
            for (int i = 0; i < slats; i++)
            {
                bool loose = c.Chance(0.08f);
                Box(root.transform, new Vector3(0, h * 0.5f - 0.05f - i * 0.06f, 0.07f), new Vector3(w - 0.04f, 0.004f, 0.045f), slat,
                    new Vector3(c.Range(20f, 50f), 0, loose ? c.Range(-12f, 12f) : 0), "Slat");
            }
            Box(root.transform, new Vector3(0, h * 0.5f + 0.02f, 0.07f), new Vector3(w, 0.05f, 0.05f), slat, null, "Head");
            NoShadows(root);
            return root;
        }

        public static GameObject Pegboard(RoomCtx c)
        {
            var root = Root("Pegboard");
            Box(root.transform, new Vector3(0, 0, 0.01f), new Vector3(1.6f, 0.9f, 0.02f), M("Pegboard", new Color(0.55f, 0.45f, 0.32f), 0, 0.2f));
            for (int i = 0; i < c.Range(5, 11); i++)
            {
                float x = c.Range(-0.7f, 0.7f), y = c.Range(-0.3f, 0.3f);
                if (c.Chance(0.5f)) { Box(root.transform, new Vector3(x, y, 0.035f), new Vector3(0.03f, c.Range(0.2f, 0.35f), 0.02f), Steel, new Vector3(0, 0, c.Range(-10f, 10f)), "Tool"); Box(root.transform, new Vector3(x, y - 0.13f, 0.04f), new Vector3(0.07f, 0.06f, 0.03f), Plastic(c.Range(0, 4)), null, "Grip"); }
                else Box(root.transform, new Vector3(x, y, 0.035f), new Vector3(c.Range(0.1f, 0.25f), 0.04f, 0.02f), Steel, new Vector3(0, 0, c.Range(-5f, 5f)), "Tool");
            }
            return root;
        }

        public static GameObject ExitSign(RoomCtx c)
        {
            var root = Root("ExitSign");
            Box(root.transform, new Vector3(0, 0, 0.03f), new Vector3(0.4f, 0.15f, 0.06f), M("ExitBox", new Color(0.8f, 0.8f, 0.78f), 0, 0.4f));
            Box(root.transform, new Vector3(0, 0, 0.061f), new Vector3(0.36f, 0.11f, 0.002f), M("ExitGreen", new Color(0.05f, 0.3f, 0.12f), 0, 0.5f, null, null, new Color(0.08f, 0.55f, 0.2f)));
            return root;
        }

        /// <summary>Short wall shelf with a row of books.</summary>
        public static GameObject WallShelf(RoomCtx c)
        {
            var root = Root("WallShelf");
            float w = c.Range(0.9f, 1.4f);
            Box(root.transform, new Vector3(0, 0, 0.12f), new Vector3(w, 0.025f, 0.24f), Wood);
            foreach (var x in new[] { -w * 0.4f, w * 0.4f }) Box(root.transform, new Vector3(x, -0.07f, 0.02f), new Vector3(0.02f, 0.14f, 0.04f), Dark);
            var books = BookRow(c);
            books.transform.SetParent(root.transform, false);
            books.transform.localPosition = new Vector3(c.Range(-0.1f, 0.1f), 0.0125f, 0.12f);
            return root;
        }

        // ------------------------------------------------------------------ floor pieces
        /// <summary>Glass-fronted trophy cabinet with cups and plaques.</summary>
        public static GameObject TrophyCase(RoomCtx c)
        {
            var root = Root("TrophyCase");
            float w = c.Range(1.4f, 2.2f), h = 1.95f, d = 0.45f;
            var wood = M("CaseWood", new Color(0.22f, 0.13f, 0.08f), 0, 0.45f);
            Box(root.transform, new Vector3(0, 0.4f, 0), new Vector3(w, 0.8f, d), wood, null, "Base");
            Box(root.transform, new Vector3(0, h - 0.04f, 0), new Vector3(w, 0.08f, d), wood, null, "Top");
            Box(root.transform, new Vector3(-w * 0.5f + 0.03f, 1.38f, 0), new Vector3(0.06f, 1.16f, d), wood);
            Box(root.transform, new Vector3(w * 0.5f - 0.03f, 1.38f, 0), new Vector3(0.06f, 1.16f, d), wood);
            Box(root.transform, new Vector3(0, 1.38f, -d * 0.5f + 0.01f), new Vector3(w, 1.16f, 0.02f), M("CaseBack", new Color(0.18f, 0.06f, 0.07f), 0, 0.1f));
            Box(root.transform, new Vector3(0, 1.4f, 0), new Vector3(w - 0.1f, 0.02f, d - 0.06f), GlassClear, null, "Shelf");
            for (int shelf = 0; shelf < 2; shelf++)
            {
                float y = shelf == 0 ? 0.81f : 1.41f;
                for (float x = -w * 0.5f + 0.2f; x < w * 0.5f - 0.15f; x += c.Range(0.22f, 0.35f))
                {
                    if (c.Chance(0.2f)) continue;
                    if (c.Chance(0.6f))
                    {
                        float ch = c.Range(0.18f, 0.38f);
                        Box(root.transform, new Vector3(x, y + 0.03f, 0), new Vector3(0.09f, 0.06f, 0.09f), Dark, null, "Plinth");
                        Cyl(root.transform, new Vector3(x, y + 0.06f + ch * 0.35f, 0), 0.015f, ch * 0.7f, Gold, null, "Stem");
                        Cyl(root.transform, new Vector3(x, y + 0.06f + ch * 0.85f, 0), c.Range(0.04f, 0.07f), ch * 0.35f, Gold, null, "Cup");
                    }
                    else Box(root.transform, new Vector3(x, y + 0.11f, -0.1f), new Vector3(0.16f, 0.22f, 0.02f), wood, new Vector3(-12, 0, 0), "Plaque");
                }
            }
            if (!c.Chance(0.15f)) Box(root.transform, new Vector3(0, 1.38f, d * 0.5f - 0.01f), new Vector3(w - 0.08f, 1.14f, 0.008f), GlassClear, null, "Glass");
            Solid(root, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d));
            return root;
        }

        /// <summary>Table-height glass display case (museum, herbarium); its items are placed on it afterwards.</summary>
        public static GameObject Vitrine(RoomCtx c)
        {
            var root = Root("Vitrine");
            float w = c.Range(1.2f, 1.8f), d = 0.7f;
            var wood = M("CaseWood", new Color(0.22f, 0.13f, 0.08f), 0, 0.45f);
            foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
                    Box(root.transform, new Vector3(sx * (w * 0.5f - 0.04f), 0.4f, sz * (d * 0.5f - 0.04f)), new Vector3(0.05f, 0.8f, 0.05f), wood);
            Box(root.transform, new Vector3(0, 0.82f, 0), new Vector3(w, 0.05f, d), wood, null, "Bed");
            Box(root.transform, new Vector3(0, 0.845f, 0), new Vector3(w - 0.06f, 0.002f, d - 0.06f), M("Velvet", new Color(0.12f, 0.05f, 0.06f), 0, 0.05f), null, "Velvet");
            if (!c.Chance(0.15f))
            {
                Box(root.transform, new Vector3(0, 1.06f, d * 0.5f), new Vector3(w, 0.42f, 0.006f), GlassClear, null, "GlassF");
                Box(root.transform, new Vector3(0, 1.06f, -d * 0.5f), new Vector3(w, 0.42f, 0.006f), GlassClear, null, "GlassB");
                Box(root.transform, new Vector3(w * 0.5f, 1.06f, 0), new Vector3(0.006f, 0.42f, d), GlassClear, null, "GlassR");
                Box(root.transform, new Vector3(-w * 0.5f, 1.06f, 0), new Vector3(0.006f, 0.42f, d), GlassClear, null, "GlassL");
                Box(root.transform, new Vector3(0, 1.27f, 0), new Vector3(w, 0.006f, d), GlassClear, null, "GlassTop");
            }
            Solid(root, new Vector3(0, 0.64f, 0), new Vector3(w, 1.28f, d));
            return root;
        }

        public static GameObject Globe(RoomCtx c)
        {
            var root = Root("Globe");
            var wood = M("CaseWood", new Color(0.22f, 0.13f, 0.08f), 0, 0.45f);
            bool tall = c.Chance(0.5f);
            float baseH = tall ? 0.75f : 0.08f, r = tall ? 0.22f : 0.15f;
            if (tall) { Cyl(root.transform, new Vector3(0, 0.03f, 0), 0.2f, 0.06f, wood, null, "Foot"); Cyl(root.transform, new Vector3(0, baseH * 0.5f, 0), 0.025f, baseH, wood, null, "Post"); }
            else Cyl(root.transform, new Vector3(0, 0.03f, 0), 0.09f, 0.06f, wood, null, "Foot");
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = "Sphere";
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = new Vector3(0, baseH + r + 0.02f, 0);
            g.transform.localScale = Vector3.one * r * 2;
            g.transform.localRotation = Quaternion.Euler(0, 0, 23.5f);
            g.GetComponent<MeshRenderer>().sharedMaterial = M("GlobeSea", new Color(0.3f, 0.42f, 0.5f), 0, 0.5f);
            var ring = Cyl(root.transform, new Vector3(0, baseH + r + 0.02f, 0), r + 0.02f, 0.01f, Gold, new Vector3(90, 0, 23.5f), "Meridian");
            ring.transform.localScale = new Vector3((r + 0.02f) * 2, 0.005f, (r + 0.02f) * 2);
            return root;
        }

        /// <summary>Painting easel with a canvas, half finished.</summary>
        public static GameObject Easel(RoomCtx c)
        {
            var root = Root("Easel");
            var wood = M("EaselWood", new Color(0.45f, 0.33f, 0.2f), 0, 0.3f);
            Box(root.transform, new Vector3(-0.25f, 0.85f, 0.05f), new Vector3(0.035f, 1.75f, 0.035f), wood, new Vector3(-8, 0, -7));
            Box(root.transform, new Vector3(0.25f, 0.85f, 0.05f), new Vector3(0.035f, 1.75f, 0.035f), wood, new Vector3(-8, 0, 7));
            Box(root.transform, new Vector3(0, 0.8f, -0.25f), new Vector3(0.035f, 1.65f, 0.035f), wood, new Vector3(18, 0, 0));
            Box(root.transform, new Vector3(0, 0.78f, 0.12f), new Vector3(0.62f, 0.035f, 0.06f), wood, null, "Ledge");
            if (!c.Chance(0.2f))
            {
                float cw = c.Range(0.45f, 0.7f), chh = c.Range(0.5f, 0.75f);
                Box(root.transform, new Vector3(0, 0.8f + chh * 0.5f, 0.13f), new Vector3(cw, chh, 0.02f), Paper(0), new Vector3(-8, 0, 0), "Canvas");
                for (int i = 0; i < c.Range(1, 5); i++)
                    Flat(root.transform, new Vector3(c.Range(-cw * 0.3f, cw * 0.3f), 0.8f + chh * c.Range(0.25f, 0.75f), 0.142f + i * 0.0005f), new Vector3(cw * c.Range(0.2f, 0.6f), 1, chh * c.Range(0.15f, 0.5f)), Print(c.Range(0, 6)), Sheet(), new Vector3(82, 0, c.Range(-20f, 20f)), "Paint");
            }
            return root;
        }

        public static GameObject Lectern(RoomCtx c)
        {
            var root = Root("Lectern");
            var wood = M("CaseWood", new Color(0.22f, 0.13f, 0.08f), 0, 0.45f);
            Box(root.transform, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1.0f, 0.45f), wood);
            Box(root.transform, new Vector3(0, 1.1f, -0.02f), new Vector3(0.66f, 0.03f, 0.5f), wood, new Vector3(18, 0, 0), "Desk");
            Box(root.transform, new Vector3(0, 1.0f, 0.2f), new Vector3(0.6f, 0.2f, 0.06f), wood, null, "Front");
            Solid(root, new Vector3(0, 0.6f, 0), new Vector3(0.66f, 1.2f, 0.5f));
            return root;
        }

        public static GameObject Workbench(RoomCtx c)
        {
            var root = Root("Workbench");
            float w = c.Range(1.8f, 2.6f);
            Box(root.transform, new Vector3(0, 0.88f, 0), new Vector3(w, 0.06f, 0.8f), M("BenchTop", new Color(0.4f, 0.3f, 0.2f), 0, 0.25f), null, "Top");
            foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
                    Box(root.transform, new Vector3(sx * (w * 0.5f - 0.06f), 0.43f, sz * 0.33f), new Vector3(0.05f, 0.86f, 0.05f), Steel);
            Box(root.transform, new Vector3(0, 0.2f, 0), new Vector3(w - 0.1f, 0.03f, 0.7f), Steel, null, "Shelf");
            Solid(root, new Vector3(0, 0.46f, 0), new Vector3(w, 0.92f, 0.8f));
            return root;
        }

        /// <summary>Raised wooden planter bed filled with soil; plants are set into it afterwards.</summary>
        public static GameObject PlanterBed(RoomCtx c)
        {
            var root = Root("PlanterBed");
            float w = c.Range(1.8f, 3.2f), d = c.Range(0.8f, 1.1f), h = c.Range(0.45f, 0.7f);
            var wood = M("PlanterTimber", new Color(0.52f, 0.4f, 0.28f), 0, 0.2f);
            Box(root.transform, new Vector3(0, h * 0.5f, d * 0.5f), new Vector3(w, h, 0.05f), wood);
            Box(root.transform, new Vector3(0, h * 0.5f, -d * 0.5f), new Vector3(w, h, 0.05f), wood);
            Box(root.transform, new Vector3(w * 0.5f, h * 0.5f, 0), new Vector3(0.05f, h, d), wood);
            Box(root.transform, new Vector3(-w * 0.5f, h * 0.5f, 0), new Vector3(0.05f, h, d), wood);
            Box(root.transform, new Vector3(0, h - 0.06f, 0), new Vector3(w - 0.05f, 0.04f, d - 0.05f), Soil, null, "Soil");
            Box(root.transform, new Vector3(0, h * 0.4f, 0), new Vector3(w - 0.05f, h * 0.8f, d - 0.05f), Soil, null, "Fill");
            Solid(root, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d));
            return root;
        }

        public static GameObject Telescope(RoomCtx c)
        {
            var root = Root("Telescope");
            var dark = M("ScopeBlack", new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.5f);
            foreach (var a in new[] { 0f, 120f, 240f })
                Box(root.transform, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.22f, 0.55f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.22f), new Vector3(0.03f, 1.15f, 0.03f), Wood, new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * -12f, 0, Mathf.Sin(a * Mathf.Deg2Rad) * 12f));
            Cyl(root.transform, new Vector3(0, 1.25f, 0.1f), 0.07f, 1.1f, M("ScopeWhite", new Color(0.75f, 0.74f, 0.7f), 0.2f, 0.5f), new Vector3(60, 0, 0), "Tube");
            Cyl(root.transform, new Vector3(0, 1.04f, -0.25f), 0.02f, 0.12f, dark, new Vector3(60, 0, 0), "Eyepiece");
            return root;
        }

        public static GameObject FumeHood(RoomCtx c)
        {
            var root = Root("FumeHood");
            var body = M("HoodBody", new Color(0.6f, 0.6f, 0.58f), 0.2f, 0.4f);
            Box(root.transform, new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 0.75f), Laminate, null, "Base");
            Box(root.transform, new Vector3(0, 0.92f, 0), new Vector3(1.5f, 0.04f, 0.75f), EpoxyTop, null, "Top");
            Box(root.transform, new Vector3(-0.73f, 1.5f, 0), new Vector3(0.04f, 1.15f, 0.75f), body);
            Box(root.transform, new Vector3(0.73f, 1.5f, 0), new Vector3(0.04f, 1.15f, 0.75f), body);
            Box(root.transform, new Vector3(0, 1.5f, -0.36f), new Vector3(1.5f, 1.15f, 0.03f), body);
            Box(root.transform, new Vector3(0, 2.15f, 0), new Vector3(1.5f, 0.2f, 0.75f), body, null, "Hood");
            Box(root.transform, new Vector3(0, c.Range(1.35f, 1.85f), 0.37f), new Vector3(1.42f, 0.55f, 0.01f), GlassClear, null, "Sash");
            Solid(root, new Vector3(0, 1.12f, 0), new Vector3(1.5f, 2.25f, 0.75f));
            return root;
        }

        /// <summary>One step of lecture-hall seating: a wide platform the desks are set onto.</summary>
        public static GameObject TierPlatform(RoomCtx c, float w, float d, float h)
        {
            var root = Root("TierPlatform");
            Box(root.transform, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), M("TierFloor", new Color(0.33f, 0.24f, 0.18f), 0, 0.3f, null, null));
            Box(root.transform, new Vector3(0, h - 0.01f, d * 0.5f - 0.02f), new Vector3(w, 0.02f, 0.04f), M("StairNosing", new Color(0.55f, 0.5f, 0.35f), 0.6f, 0.4f), null, "Nosing");
            Solid(root, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d));
            return root;
        }

        public static GameObject SpecimenJar(RoomCtx c)
        {
            var root = Root("SpecimenJar");
            float h = c.Range(0.15f, 0.32f), r = c.Range(0.05f, 0.09f);
            Cyl(root.transform, new Vector3(0, h * 0.45f, 0), r * 0.92f, h * 0.88f, M("JarLiquid" + c.Range(0, 3), c.Chance(0.5f) ? new Color(0.45f, 0.42f, 0.2f) : new Color(0.3f, 0.35f, 0.25f), 0, 0.8f), null, "Liquid");
            Cyl(root.transform, new Vector3(0, h * 0.5f, 0), r, h, GlassClear, null, "Jar");
            Cyl(root.transform, new Vector3(0, h + 0.01f, 0), r * 0.9f, 0.02f, Dark, null, "Lid");
            return root;
        }
    }
}
