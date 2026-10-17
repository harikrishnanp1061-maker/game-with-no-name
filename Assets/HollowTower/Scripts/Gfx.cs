using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace HollowTower
{
    /// <summary>
    /// Builds HDRP materials, procedural textures, primitive shapes and physically based lights at
    /// runtime, so the game's geometry can be generated from code.
    /// </summary>
    public static class Gfx
    {
        public const int EnemyLayer = 1;        // built-in "TransparentFX" layer, reused for enemies
        public const int PlayerLayer = 2;       // built-in "Ignore Raycast" layer
        public const int WorldMask = 1 << 0;    // walls, floors, props
        public const int ShootMask = (1 << 0) | (1 << EnemyLayer);

        /// <summary>The old "intensity 1" point light expressed in lumens.</summary>
        public const float Lumens = 520f;
        /// <summary>The old "emission 1" expressed in nits.</summary>
        public const float Nits = 140f;

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissiveId = Shader.PropertyToID("_EmissiveColor");

        static Shader lit, unlit;
        static Shader LitShader { get { if (lit == null) lit = Shader.Find("HDRP/Lit"); return lit; } }
        static Shader UnlitShader { get { if (unlit == null) unlit = Shader.Find("HDRP/Unlit"); return unlit; } }

        // ---------- Materials ----------

        /// <summary>Standard HDRP/Lit material. A texture made by GrimeTexture brings its normal map along.</summary>
        public static Material Lit(Color color, float smoothness = 0.15f, float metallic = 0f, Texture2D tex = null)
        {
            var m = new Material(LitShader);
            m.SetColor(BaseColorId, color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.SetTexture("_BaseColorMap", tex);
                Texture2D bump;
                if (NormalMaps.TryGetValue(tex, out bump))
                {
                    m.SetTexture("_NormalMap", bump);
                    m.SetFloat("_NormalScale", 1f);
                }
            }
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>
        /// Lit material whose texture is projected in world space (triplanar), so walls, floors and
        /// furniture of any size tile at a constant real-world scale.
        /// </summary>
        public static Material LitWorld(Color color, float smoothness, float metallic, Texture2D tex, float metres)
        {
            var m = Lit(color, smoothness, metallic, tex);
            m.SetFloat("_UVBase", 5f);                       // triplanar
            m.SetFloat("_TexWorldScale", 1f / Mathf.Max(0.05f, metres));
            m.SetVector("_UVMappingMask", new Vector4(1f, 0f, 0f, 0f));
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        public static Material Emissive(Color baseColor, Color emission)
        {
            var m = Lit(baseColor, 0.4f);
            SetEmission(m, emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        /// <summary>Sets emission on an HDRP material; colours are in the old "1 = bright" scale.</summary>
        public static void SetEmission(Material m, Color legacy)
        {
            HDMaterial.SetUseEmissiveIntensity(m, false);
            HDMaterial.SetEmissiveColor(m, ToNits(legacy));
        }

        /// <summary>Sets emission already expressed in nits (e.g. read back from a material).</summary>
        public static void SetEmissionNits(Material m, Color nits)
        {
            HDMaterial.SetUseEmissiveIntensity(m, false);
            HDMaterial.SetEmissiveColor(m, nits);
        }

        public static Color ToNits(Color legacy) { return new Color(legacy.r * Nits, legacy.g * Nits, legacy.b * Nits, 1f); }

        /// <summary>See-through glowing material (ghosts, glass).</summary>
        public static Material Transparent(Color color, Color emission)
        {
            var m = Lit(color, 0.6f);
            m.SetFloat("_SurfaceType", 1f);
            m.SetFloat("_BlendMode", 0f);                    // alpha
            m.SetFloat("_EnableBlendModePreserveSpecularLighting", 0f);
            m.SetFloat("_TransparentDepthPrepassEnable", 0f);
            m.SetFloat("_ZWrite", 0f);
            HDMaterial.SetSurfaceType(m, true);
            SetEmission(m, emission);
            return m;
        }

        /// <summary>Unlit glowing see-through material driven by a texture's alpha (wall writing, tracers).</summary>
        public static Material UnlitGlow(Texture tex, Color legacyEmission, bool additive)
        {
            var m = new Material(UnlitShader);
            m.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 1f));
            if (tex != null) m.SetTexture("_UnlitColorMap", tex);
            m.SetFloat("_SurfaceType", 1f);
            m.SetFloat("_BlendMode", additive ? 1f : 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_CullMode", 0f);
            m.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.SetSurfaceType(m, true);
            HDMaterial.SetUseEmissiveIntensity(m, false);
            HDMaterial.SetEmissiveColor(m, ToNits(legacyEmission));
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        static readonly Dictionary<Material, Material> gltfConverted = new Dictionary<Material, Material>();

        /// <summary>
        /// glTFast's shader graph can't build a ray-tracing pass on this HDRP version, so imported glTF
        /// materials are rebuilt as HDRP/Lit (base colour, normal map, metal/rough factors, emission, alpha).
        /// </summary>
        public static Material[] FromGltf(Material[] src)
        {
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = FromGltf(src[i]);
            return dst;
        }

        public static Material FromGltf(Material s)
        {
            if (s == null || s.shader.name.StartsWith("HDRP/")) return s;
            Material m;
            if (gltfConverted.TryGetValue(s, out m)) return m;
            m = new Material(LitShader) { name = s.name + " (HDRP)" };
            Color baseCol = s.HasProperty("baseColorFactor") ? s.GetColor("baseColorFactor") : Color.white;
            m.SetColor(BaseColorId, baseCol);
            if (s.HasProperty("baseColorTexture") && s.GetTexture("baseColorTexture") != null) m.SetTexture("_BaseColorMap", s.GetTexture("baseColorTexture"));
            if (s.HasProperty("normalTexture") && s.GetTexture("normalTexture") != null)
            {
                m.SetTexture("_NormalMap", s.GetTexture("normalTexture"));
                m.SetFloat("_NormalScale", s.HasProperty("normalTexture_scale") ? s.GetFloat("normalTexture_scale") : 1f);
            }
            float rough = s.HasProperty("roughnessFactor") ? s.GetFloat("roughnessFactor") : 0.8f;
            bool mrTex = s.HasProperty("metallicRoughnessTexture") && s.GetTexture("metallicRoughnessTexture") != null;
            m.SetFloat("_Smoothness", Mathf.Clamp01(1f - rough) * (mrTex ? 0.6f : 1f) + (mrTex ? 0.15f : 0f));
            m.SetFloat("_Metallic", s.HasProperty("metallicFactor") ? s.GetFloat("metallicFactor") * (mrTex ? 0.5f : 1f) : 0f);
            if (s.HasProperty("emissiveTexture") && s.GetTexture("emissiveTexture") != null)
            {
                m.SetTexture("_EmissiveColorMap", s.GetTexture("emissiveTexture"));
                HDMaterial.SetEmissiveColor(m, s.HasProperty("emissiveFactor") ? s.GetColor("emissiveFactor") * Nits : Color.black);
            }
            bool cutout = s.IsKeywordEnabled("_ALPHATEST_ON") || (s.renderQueue >= 2450 && s.renderQueue < 3000);
            bool blend = s.renderQueue >= 3000;
            if (cutout)
            {
                m.SetFloat("_AlphaCutoffEnable", 1f);
                m.SetFloat("_AlphaCutoff", s.HasProperty("alphaCutoff") ? s.GetFloat("alphaCutoff") : 0.5f);
                m.SetFloat("_DoubleSidedEnable", 1f);
            }
            if (s.IsKeywordEnabled("_DOUBLESIDED_ON") || (s.HasProperty("_CullMode") && s.GetFloat("_CullMode") == 0f)) m.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.ValidateMaterial(m);
            if (blend) HDMaterial.SetSurfaceType(m, true);
            gltfConverted[s] = m;
            return m;
        }

        /// <summary>Kept for API compatibility; world-projected materials don't need per-object tiling.</summary>
        public static void SetTiling(Renderer r, Vector2 tiling) { }

        // ---------- Procedural textures ----------

        /// <summary>Normal maps generated alongside each grime texture; Lit() picks them up automatically.</summary>
        static readonly Dictionary<Texture2D, Texture2D> NormalMaps = new Dictionary<Texture2D, Texture2D>();

        public static Texture2D GrimeTexture(int seed, Color light, Color dark, float scale, float stains, bool tiles,
                                             float bumpiness = 3f)
        {
            const int size = 512;
            var height = new float[size * size];
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 100f, oy = (float)rnd.NextDouble() * 100f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float n = TileNoise(u, v, scale, ox, oy) * 0.6f + TileNoise(u, v, scale * 4f, ox + 7, oy + 3) * 0.3f
                              + TileNoise(u, v, scale * 16f, ox + 13, oy + 11) * 0.1f;
                    float stain = Mathf.Clamp01((TileNoise(u, v, 2f, ox + 31, oy + 17) - (1f - stains)) * 4f);
                    Color c = Color.Lerp(light, dark, n);
                    c = Color.Lerp(c, dark * 0.45f, stain * 0.8f);
                    float hgt = 1f - n + TileNoise(u, v, scale * 32f, ox + 5, oy + 29) * 0.25f;
                    if (tiles)
                    {
                        float gx = Mathf.Abs((u * 4f) % 1f - 0.5f), gy = Mathf.Abs((v * 4f) % 1f - 0.5f);
                        if (gx > 0.485f || gy > 0.485f) { c *= 0.55f; hgt -= 0.6f; }
                    }
                    height[y * size + x] = hgt;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            NormalMaps[tex] = NormalFromHeight(height, size, bumpiness);
            return tex;
        }

        /// <summary>Simple striped / planked pattern (carpet, wallpaper, wood floor) with matching normal map.</summary>
        public static Texture2D PatternTexture(int seed, Color a, Color b, int stripes, bool planks, float grain)
        {
            const int size = 256;
            var height = new float[size * size];
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 50f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float band = (u * stripes) % 1f;
                    float n = TileNoise(u, v, 24f, ox, ox + 3) * grain;
                    // Long streaks are wood grain; on wallpaper and carpet they looked like swirling interference.
                    if (planks) n += TileNoise(u * 0.2f, v * 6f, 8f, ox + 9, ox) * grain * 0.5f;
                    Color c = Color.Lerp(a, b, planks ? Mathf.PerlinNoise(Mathf.Floor(u * stripes) * 3.7f, ox) : (band < 0.5f ? 0f : 1f));
                    c = Color.Lerp(c, c * 0.6f, n);
                    float h = 1f - n;
                    if (planks && (band < 0.03f || band > 0.97f)) { c *= 0.5f; h -= 0.5f; }
                    c.a = 1f;
                    px[y * size + x] = c;
                    height[y * size + x] = h;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            NormalMaps[tex] = NormalFromHeight(height, size, 2f);
            return tex;
        }

        static Texture2D NormalFromHeight(float[] h, int size, float strength)
        {
            // Linear texture, RGB = tangent-space normal; alpha 1 so UnpackNormal works on every platform.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int yu = (y + 1) % size, yd = (y + size - 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xr = (x + 1) % size, xl = (x + size - 1) % size;
                    float dx = (h[y * size + xr] - h[y * size + xl]) * strength;
                    float dy = (h[yu * size + x] - h[yd * size + x]) * strength;
                    var n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float TileNoise(float u, float v, float freq, float ox, float oy)
        {
            // Blend four samples so the texture wraps without seams.
            float a = Mathf.PerlinNoise(ox + u * freq, oy + v * freq);
            float b = Mathf.PerlinNoise(ox + (u - 1f) * freq, oy + v * freq);
            float c = Mathf.PerlinNoise(ox + u * freq, oy + (v - 1f) * freq);
            float d = Mathf.PerlinNoise(ox + (u - 1f) * freq, oy + (v - 1f) * freq);
            float ab = Mathf.Lerp(a, b, u);
            float cd = Mathf.Lerp(c, d, u);
            return Mathf.Lerp(ab, cd, v);
        }

        // ---------- Primitives ----------

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale,
                                      Material mat, bool keepCollider = true)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!keepCollider)
            {
                var col = g.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            return g;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
                                     bool keepCollider = true)
        {
            return Prim(PrimitiveType.Cube, name, parent, localPos, scale, mat, keepCollider);
        }

        // ---------- Lights (physical units) ----------

        static HDAdditionalLightData AddHD(GameObject g)
        {
            var hd = g.GetComponent<HDAdditionalLightData>();
            if (hd == null) hd = g.AddComponent<HDAdditionalLightData>();
            return hd;
        }

        /// <summary>Point light. <paramref name="intensity"/> uses the old scale (1 ~ a dim bulb) and is converted to lumens.</summary>
        public static Light PointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range,
                                       LightShadows shadows)
        {
            var g = new GameObject("Light");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            var hd = AddHD(g);
            l.lightUnit = LightUnit.Lumen;
            l.color = color;
            l.intensity = intensity * Lumens;
            l.range = range;
            l.shadows = shadows;
            l.shadowStrength = 0.85f;
            l.shapeRadius = 0.05f;
            hd.SetShadowResolutionLevel(0);
            hd.affectsVolumetric = true;
            hd.volumetricDimmer = 0.6f;
            return l;
        }

        /// <summary>Spot light in lumens with a reflector (flashlight, ceiling cans).</summary>
        public static Light SpotLight(Transform parent, Vector3 localPos, Quaternion localRot, Color color, float lumens,
                                      float range, float angle, float innerPercent, LightShadows shadows)
        {
            var g = new GameObject("Spot");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot;
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot;
            var hd = AddHD(g);
            l.lightUnit = LightUnit.Lumen;
            l.enableSpotReflector = true;
            l.color = color;
            l.intensity = lumens;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * innerPercent;
            l.shadows = shadows;
            l.shapeRadius = 0.03f;
            hd.affectsVolumetric = true;
            return l;
        }

        // ---------- HDRP decals ----------

        public enum DecalKind { Blood, Grime, Water }
        static Material[] bloodDecals, grimeDecals, waterDecals;

        /// <summary>Projects a decal (blood, grime, water stain) onto whatever is underneath / behind it.</summary>
        public static DecalProjector Decal(Transform parent, Vector3 pos, Vector3 projectDir, float width, float height, DecalKind kind, int seed)
        {
            if (bloodDecals == null) MakeDecalMaterials();
            var set = kind == DecalKind.Blood ? bloodDecals : kind == DecalKind.Grime ? grimeDecals : waterDecals;
            var g = new GameObject(kind + "Decal");
            g.SetActive(false); // DecalProjector warns in OnEnable if it has no material yet
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            var rnd = new System.Random(seed);
            g.transform.rotation = Quaternion.LookRotation(projectDir, Mathf.Abs(projectDir.y) > 0.9f ? Vector3.forward : Vector3.up)
                                   * Quaternion.Euler(0f, 0f, (float)rnd.NextDouble() * 360f);
            var d = g.AddComponent<DecalProjector>();
            d.material = set[rnd.Next(set.Length)];
            d.size = new Vector3(width, height, 0.6f);
            d.pivot = new Vector3(0f, 0f, 0.3f);
            d.fadeFactor = 0.75f + (float)rnd.NextDouble() * 0.25f;
            d.drawDistance = 40f;
            g.SetActive(true);
            return d;
        }

        static void MakeDecalMaterials()
        {
            bloodDecals = new Material[3];
            grimeDecals = new Material[2];
            waterDecals = new Material[2];
            for (int i = 0; i < 3; i++) bloodDecals[i] = DecalMat(SplatTexture(100 + i, true), new Color(0.32f, 0.01f, 0.01f, 1f), 0.85f);
            for (int i = 0; i < 2; i++) grimeDecals[i] = DecalMat(SplatTexture(200 + i, false), new Color(0.05f, 0.045f, 0.035f, 0.85f), 0.05f);
            for (int i = 0; i < 2; i++) waterDecals[i] = DecalMat(SplatTexture(300 + i, false), new Color(0.22f, 0.2f, 0.15f, 0.6f), 0.6f);
        }

        static Material DecalMat(Texture2D tex, Color col, float smooth)
        {
            var m = new Material(Shader.Find("HDRP/Decal"));
            m.SetTexture("_BaseColorMap", tex);
            m.SetColor("_BaseColor", col);
            m.SetFloat("_AffectAlbedo", 1f);
            m.SetFloat("_AffectNormal", 0f);
            m.SetFloat("_AffectSmoothness", 1f);
            m.SetFloat("_AffectAO", 0f);
            m.SetFloat("_AffectMetal", 0f);
            m.SetFloat("_DecalMaskMapBlueScale", smooth);
            m.SetFloat("_DecalBlend", 1f);
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Irregular blob with an alpha falloff: splatters (with droplets) or soft stains.</summary>
        static Texture2D SplatTexture(int seed, bool splatter)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 50f, oy = (float)rnd.NextDouble() * 50f;
            var drops = new List<Vector3>();
            if (splatter) for (int i = 0; i < 14; i++) drops.Add(new Vector3((float)rnd.NextDouble() * 0.8f + 0.1f, (float)rnd.NextDouble() * 0.8f + 0.1f, 0.015f + (float)rnd.NextDouble() * 0.04f));
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size - 0.5f, v = (float)y / size - 0.5f;
                    float ang = Mathf.Atan2(v, u);
                    float wobble = Mathf.PerlinNoise(ox + Mathf.Cos(ang) * 1.6f, oy + Mathf.Sin(ang) * 1.6f);
                    float radius = (splatter ? 0.22f : 0.3f) + wobble * (splatter ? 0.16f : 0.12f);
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((radius - r) / (splatter ? 0.03f : 0.18f));
                    float n = Mathf.PerlinNoise(ox + x * 0.05f, oy + y * 0.05f);
                    if (!splatter) a *= 0.55f + 0.45f * n;
                    foreach (var d in drops)
                    {
                        float dd = Vector2.Distance(new Vector2(u + 0.5f, v + 0.5f), new Vector2(d.x, d.y));
                        a = Mathf.Max(a, Mathf.Clamp01((d.z - dd) / 0.008f));
                    }
                    float shade = 0.75f + 0.25f * n;
                    px[y * size + x] = new Color(shade, shade, shade, a);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        public static void SetLayerRecursive(GameObject g, int layer)
        {
            g.layer = layer;
            foreach (Transform t in g.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static Font BuiltinFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return f;
        }
    }
}
