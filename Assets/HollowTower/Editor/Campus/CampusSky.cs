using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The final level's night sky, painted into a latitude-longitude panorama (4096 x 2048) for Skybox/Panoramic: deep
    /// blue fading to the fog's colour at the horizon, faint green airglow and the orange light dome of a distant town low
    /// in the south-east, the Milky Way arching across with dark dust lanes and a brighter core, some 30,000 stars of every
    /// brightness (thicker along the Milky Way, a few tinted, the brightest with a glint), thin moonlit cloud that hides
    /// the stars behind it, and the moon - limb-darkened, with its seas - inside a soft glow and a faint ice halo, where
    /// the moonlight comes from. The noise is 3D on the sky's sphere, so there is no seam and no stretching at the top.
    /// </summary>
    public static class CampusSky
    {
        const int W = 4096, H = 2048;

        public static Material Build(Vector3 moonDir, Color horizon)
        {
            string texPath = CampusKit.GenDir + "/NightSky.png";
            var px = new Color[W * H];
            var cloud = new float[W * H];
            var rnd = new System.Random(77);
            // never pitch black: navy overhead, lighter towards the horizon, the fog colour at it
            Color zenith = new Color(0.024f, 0.036f, 0.078f), mid = new Color(0.042f, 0.06f, 0.115f);
            Vector3 md = moonDir.normalized;
            Vector3 galaxyN = new Vector3(0.55f, 0.45f, -0.7f).normalized;
            Vector3 galaxyCore = (Vector3.Cross(galaxyN, Vector3.up).normalized + Vector3.up * 0.35f).normalized;
            Vector3 town = new Vector3(0.6f, 0f, -0.8f).normalized;

            Color[] cloudCol; float[] cloudA;
            Clouds(md, horizon, out cloudCol, out cloudA);

            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float elev = (v - 0.5f) * Mathf.PI;
                float k = Mathf.Clamp01(Mathf.Sin(Mathf.Max(0f, elev)));
                for (int x = 0; x < W; x++)
                {
                    var d = Dir((x + 0.5f) / W, v);
                    Color c = elev < 0f ? horizon : k < 0.5f ? Color.Lerp(horizon, mid, k / 0.5f) : Color.Lerp(mid, zenith, (k - 0.5f) / 0.5f);
                    if (elev > -0.02f)
                    {
                        float up = Mathf.Max(0f, elev);
                        c += new Color(0.012f, 0.032f, 0.024f) * Mathf.Exp(-up * 9f);                                  // airglow
                        var flat = new Vector3(d.x, 0f, d.z);
                        float az = flat.sqrMagnitude > 1e-6f ? Mathf.Max(0f, Vector3.Dot(flat.normalized, town)) : 0f;
                        c += new Color(0.13f, 0.07f, 0.03f) * Mathf.Pow(az, 5f) * Mathf.Exp(-up * 12f);                // town glow

                        // the Milky Way: a soft band, brighter towards its core, cut by dark dust lanes
                        float gb = Vector3.Dot(d, galaxyN);
                        float band = Mathf.Exp(-(gb * gb) / 0.04f);
                        float core = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, galaxyCore)), 3f);
                        float mottle = Fbm(d * 7f, 5);
                        float dust = Mathf.Clamp01((Fbm(d * 11f + Vector3.one * 3f, 4) - 0.4f) * 3.5f) * Mathf.Exp(-(gb * gb) / 0.0064f);
                        float mw = band * (0.35f + 0.65f * mottle) * (0.6f + 1.2f * core) * (1f - 0.85f * dust);
                        c += Color.Lerp(new Color(0.15f, 0.17f, 0.24f), new Color(0.27f, 0.23f, 0.19f), core) * mw * Mathf.Clamp01(elev * 6f);

                        // moonlight in the air: a wide soft glow and a bright corona
                        float moonward = Mathf.Max(0f, Vector3.Dot(d, md));
                        c += new Color(0.09f, 0.11f, 0.17f) * Mathf.Pow(moonward, 5f);
                        c += new Color(0.16f, 0.18f, 0.24f) * Mathf.Pow(moonward, 40f);
                        c += new Color(0.3f, 0.32f, 0.38f) * Mathf.Pow(moonward, 400f);

                        // the clouds in front of it all (half resolution, filtered up)
                        float a; Color cc;
                        SampleClouds(cloudCol, cloudA, (x + 0.5f) / W, v, out cc, out a);
                        c = Color.Lerp(c, cc, a);
                        cloud[y * W + x] = a;
                    }
                    px[y * W + x] = c;
                }
            }

            Stars(px, cloud, rnd, galaxyN);
            Moon(px, md);

            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
            ti.maxTextureSize = 4096; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.sRGBTexture = true;
            ti.SaveAndReimport();

            string matPath = CampusKit.MatDir + "/CM_NightSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, matPath); }
            m.shader = Shader.Find("Skybox/Panoramic");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_Mapping", 1f); m.SetFloat("_ImageType", 0f); m.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ clouds
        const int CW = 2048, CH = 1024;

        /// <summary>The cloud layer at half resolution. Clouds sit on a plane overhead (a direction is projected onto it),
        /// so they shrink and crowd together towards the horizon - that is the depth of the sky. Two decks: broken, lumpy
        /// stratocumulus, the edges facing the moon lit silver and the bellies dark, opening into a clear gap round the
        /// moon; and high cirrus streaks glowing faintly in the moonlight. Low down they fade into the horizon haze.</summary>
        static void Clouds(Vector3 md, Color horizon, out Color[] col, out float[] alpha)
        {
            col = new Color[CW * CH]; alpha = new float[CW * CH];
            Vector2 moonQ = new Vector2(md.x, md.z) / (md.y + 0.1f);
            for (int y = CH / 2; y < CH; y++)
            {
                float v = (y + 0.5f) / CH;
                float elev = (v - 0.5f) * Mathf.PI;
                if (elev < 0.005f) continue;
                for (int x = 0; x < CW; x++)
                {
                    var d = Dir((x + 0.5f) / CW, v);
                    Vector2 q = new Vector2(d.x, d.z) / (d.y + 0.1f);
                    float moonward = Mathf.Max(0f, Vector3.Dot(d, md));
                    float nearMoon = Mathf.Pow(moonward, 18f);

                    // the lumpy deck, domain-warped so the clouds roll and billow
                    Vector2 w = q * 0.95f;
                    var warp = new Vector2(Fbm(new Vector3(w.x * 0.5f, w.y * 0.5f, 1.3f), 3) - 0.5f, Fbm(new Vector3(w.x * 0.5f, w.y * 0.5f, 7.9f), 3) - 0.5f) * 1.7f;
                    Vector2 wq = w + warp;
                    float dens = Fbm(new Vector3(wq.x, wq.y, 3.1f), 6);
                    float cover = 0.455f + nearMoon * 0.2f;                        // thinner round the moon: it shines through a gap
                    float a = Smooth(cover, cover + 0.17f, dens);
                    // light from the moon: denser towards it means in shadow, thinner means a lit edge
                    Vector2 toMoon = moonQ - q; toMoon = toMoon.sqrMagnitude > 1e-6f ? toMoon.normalized : Vector2.zero;
                    float densM = Fbm(new Vector3(wq.x + toMoon.x * 0.16f, wq.y + toMoon.y * 0.16f, 3.1f), 6);
                    float lit = Mathf.Clamp01((dens - densM) * 7f + 0.32f);
                    float edge = 1f - Smooth(0f, 0.55f, a);                        // thin edges let more moonlight through
                    Color belly = new Color(0.03f, 0.04f, 0.065f), silver = new Color(0.38f, 0.4f, 0.47f);
                    Color c = Color.Lerp(belly, silver, Mathf.Clamp01(lit * (0.45f + 0.55f * edge)));
                    c *= 0.65f + 0.9f * Mathf.Pow(moonward, 3f) + 2.2f * nearMoon;  // lit far brighter near the moon
                    c += new Color(0.02f, 0.03f, 0.05f);                           // skylight on the undersides

                    // high cirrus streaks above, stretched by the wind
                    Vector2 cq = q * 0.33f;
                    float ci = Fbm(new Vector3(cq.x * 0.7f + cq.y * 0.4f, (cq.y - cq.x * 0.3f) * 3.2f, 11.7f), 5);
                    float ca = Smooth(0.52f, 0.78f, ci) * 0.55f * (1f - a);
                    Color cc = new Color(0.1f, 0.115f, 0.15f) * (0.8f + 2.5f * Mathf.Pow(moonward, 6f));

                    float total = a + ca;
                    if (total > 0f) c = (c * a + cc * ca) / total;
                    // far away, near the horizon: thicker, hazier, the colour of the air
                    float far = Mathf.Exp(-elev * 7f);
                    c = Color.Lerp(c, horizon * 1.15f, far * 0.75f);
                    total = Mathf.Clamp01(total * (1f + far * 0.6f)) * Mathf.Clamp01(elev * 14f);
                    col[y * CW + x] = c; alpha[y * CW + x] = total;
                }
            }
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        static void SampleClouds(Color[] col, float[] alpha, float u, float v, out Color c, out float a)
        {
            float fx = u * CW - 0.5f, fy = v * CH - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, CH - 2);
            float tx = fx - x0, ty = Mathf.Clamp01(fy - y0);
            int xa = (x0 % CW + CW) % CW, xb = (xa + 1) % CW;
            int i00 = y0 * CW + xa, i10 = y0 * CW + xb, i01 = (y0 + 1) * CW + xa, i11 = (y0 + 1) * CW + xb;
            a = Mathf.Lerp(Mathf.Lerp(alpha[i00], alpha[i10], tx), Mathf.Lerp(alpha[i01], alpha[i11], tx), ty);
            c = Color.Lerp(Color.Lerp(col[i00], col[i10], tx), Color.Lerp(col[i01], col[i11], tx), ty);
        }

        // ------------------------------------------------------------------ stars
        static void Stars(Color[] px, float[] cloud, System.Random rnd, Vector3 galaxyN)
        {
            int placed = 0;
            for (int i = 0; i < 400000 && placed < 30000; i++)
            {
                int x = rnd.Next(W), y = H / 2 + 5 + rnd.Next(H / 2 - 10);
                var d = Dir((x + 0.5f) / W, (y + 0.5f) / H);
                float gb = Vector3.Dot(d, galaxyN);
                float want = 0.18f + 0.82f * Mathf.Exp(-(gb * gb) / 0.048f);
                // equal area: pixel rows near the zenith cover less of the sphere
                if (rnd.NextDouble() > want * Mathf.Cos(((y + 0.5f) / H - 0.5f) * Mathf.PI)) continue;
                placed++;
                float b = Mathf.Pow((float)rnd.NextDouble(), 8f) * 2.2f + 0.035f;
                float elev = ((y + 0.5f) / H - 0.5f) * 2f;
                b *= Mathf.Clamp01(elev * 5f) * (1f - cloud[y * W + x]);
                double t = rnd.NextDouble();
                var tint = t < 0.12 ? new Color(1f, 0.78f, 0.6f) : t < 0.3 ? new Color(0.75f, 0.85f, 1f) : Color.white;
                px[y * W + x] += tint * b;
                if (b > 0.45f)
                {
                    float g = b * 0.3f;
                    px[y * W + (x + 1) % W] += tint * g; px[y * W + (x + W - 1) % W] += tint * g;
                    px[(y + 1) * W + x] += tint * g; px[(y - 1) * W + x] += tint * g;
                }
                if (b > 1.0f)
                    for (int s = 2; s <= 4; s++)   // the brightest few: a faint cross glint
                    {
                        float g = b * 0.08f / s;
                        px[y * W + (x + s) % W] += tint * g; px[y * W + (x + W - s) % W] += tint * g;
                        if (y + s < H) px[(y + s) * W + x] += tint * g;
                        px[(y - s) * W + x] += tint * g;
                    }
            }
        }

        // ------------------------------------------------------------------ the moon
        static void Moon(Color[] px, Vector3 md)
        {
            float mu = 0.5f - Mathf.Atan2(md.z, md.x) / (2f * Mathf.PI), mv = 1f - Mathf.Acos(md.y) / Mathf.PI;
            int cx = Mathf.RoundToInt(mu * W), cy = Mathf.RoundToInt(mv * H);
            const int rad = 26;
            float squash = Mathf.Cos((mv - 0.5f) * Mathf.PI);   // longitude pixels shrink with elevation: keep it round
            for (int dy = -rad * 7; dy <= rad * 7; dy++)
                for (int dx = -rad * 10; dx <= rad * 10; dx++)
                {
                    int x = (cx + dx + W) % W, y = cy + dy;
                    if (y < 0 || y >= H) continue;
                    float sx = dx * squash;
                    float r = Mathf.Sqrt(sx * sx + dy * dy) / rad;
                    if (r <= 1f)
                    {
                        float limb = Mathf.Sqrt(1f - r * r);
                        var q = new Vector3(sx / rad, dy / (float)rad, limb);
                        float seas = Mathf.Clamp01((Fbm(q * 2.4f + Vector3.one * 8f, 4) - 0.45f) * 2.6f);
                        float craters = Fbm(q * 9f, 3) * 0.18f;
                        float lum = (0.55f + 0.45f * limb) * (1f - 0.42f * seas) * (0.92f - craters);
                        float edge = Mathf.Clamp01((1f - r) * rad * 0.8f);
                        px[y * W + x] = Color.Lerp(px[y * W + x], new Color(0.93f, 0.92f, 0.88f) * lum * 1.15f, edge);
                    }
                    else
                    {
                        float glow = Mathf.Exp(-(r - 1f) * 1.6f) * 0.16f + Mathf.Exp(-(r - 1f) * 0.35f) * 0.05f;
                        float ring = Mathf.Exp(-Mathf.Pow((r - 5.2f) / 0.5f, 2f)) * 0.025f;
                        px[y * W + x] += new Color(0.5f, 0.56f, 0.7f) * glow + new Color(0.6f, 0.62f, 0.7f) * ring;
                    }
                }
        }

        // ------------------------------------------------------------------ noise on the sphere
        static Vector3 Dir(float u, float v)
        {
            float lon = (0.5f - u) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
            return new Vector3(Mathf.Cos(lon) * Mathf.Sin(lat), Mathf.Cos(lat), Mathf.Sin(lon) * Mathf.Sin(lat));
        }

        static float Hash(int x, int y, int z)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
        }

        static float Noise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float fx = p.x - x, fy = p.y - y, fz = p.z - z;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float a = Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), fx), b = Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), fx);
            float c = Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), fx), e = Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, e, fy), fz);
        }

        static float Fbm(Vector3 p, int octaves)
        {
            float sum = 0f, amp = 0.5f;
            for (int i = 0; i < octaves; i++) { sum += Noise(p) * amp; p = p * 2.03f + new Vector3(17.1f, 9.3f, 4.7f); amp *= 0.5f; }
            return sum;
        }
    }
}
