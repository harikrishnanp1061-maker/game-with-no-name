using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The zombie dog ships as a PS1 rip: 250 triangles and one 128x128 texture. This builds a nicer version
    /// next to it (Resources/Models/Zombies/Dog), which DogRig swaps in at runtime:
    ///  Dog_HQ.asset      - the mesh subdivided twice with Loop smoothing (rounder body), skin weights carried over.
    ///  Dog_BaseColor.png - the texture upscaled 8x (bicubic), sharpened, with procedural skin pores, veins and gore.
    ///  Dog_Normal.png    - a bump map derived from that detail.
    /// Run from the menu (Hollow Tower > Dog > Build HQ) or batch: -executeMethod HollowTower.EditorTools.DogImport.BuildHQ
    /// </summary>
    public static class DogImport
    {
        const string Folder = "Assets/HollowTower/Resources/Models/Zombies/Dog/";
        const int Levels = 0, Upscale = 8; // subdividing peels the open PS1 box pieces apart: keep its shape

        [MenuItem("Hollow Tower/Dog/Build HQ")]
        public static void BuildHQMenu() { BuildHQ(); }

        public static void BuildHQ()
        {
            Mesh src = null;
            Texture2D tex = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Folder + "Dog.glb"))
            {
                if (o is Mesh && src == null) src = (Mesh)o;
                if (o is Texture2D && tex == null) tex = (Texture2D)o;
            }
            if (src == null || tex == null) { Debug.LogError("DogImport: mesh or texture missing in Dog.glb"); Exit(1); return; }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Dog.glb");
            var smr = go != null ? go.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            var mesh = Object.Instantiate(src);
            if (smr != null) Debug.Log("DogImport: flipped " + OrientOutward(mesh, smr.bones) + " of " + mesh.triangles.Length / 3 + " triangles to face outward");
            for (int i = 0; i < Levels; i++) mesh = Subdivide(mesh);
            // Subdivided meshes get smooth shading; the plain PS1 mesh keeps the normals it was authored with.
            if (Levels > 0) SmoothNormals(mesh);
            mesh.name = "Dog_HQ";
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.DeleteAsset(Folder + "Dog_HQ.asset");
            AssetDatabase.CreateAsset(mesh, Folder + "Dog_HQ.asset");
            Debug.Log("DogImport: mesh " + src.vertexCount + " verts / " + src.triangles.Length / 3 + " tris -> " + mesh.vertexCount + " / " + mesh.triangles.Length / 3);

            Color[] baseCol; float[] height; int w, h;
            BuildTextures(tex, out baseCol, out height, out w, out h);
            Save(Folder + "Dog_BaseColor.png", baseCol, w, h);
            Save(Folder + "Dog_Normal.png", NormalFromHeight(height, w, h, 3.5f), w, h);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("DogImport: textures " + tex.width + "x" + tex.height + " -> " + w + "x" + h);
            Exit(0);
        }

        static void Exit(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }

        // ---------------- mesh: Loop subdivision on the position-welded surface, UVs / skinning per split vertex ----------------

        static Mesh Subdivide(Mesh m)
        {
            var V = m.vertices; var UV = m.uv; var T = m.triangles; var BW = m.boneWeights;
            int n = V.Length;

            // Weld by position: PS1 meshes split every UV seam.
            var weld = new int[n];
            var key = new Dictionary<Vector3Int, int>();
            var wpos = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(V[i].x * 1000f), Mathf.RoundToInt(V[i].y * 1000f), Mathf.RoundToInt(V[i].z * 1000f));
                int id;
                if (!key.TryGetValue(k, out id)) { id = wpos.Count; key[k] = id; wpos.Add(V[i]); }
                weld[i] = id;
            }
            int wn = wpos.Count;

            // Welded edges -> their opposite welded vertices (to tell interior from boundary edges).
            var edgeOpp = new Dictionary<long, List<int>>();
            var nbrs = new List<HashSet<int>>();
            for (int i = 0; i < wn; i++) nbrs.Add(new HashSet<int>());
            for (int t = 0; t < T.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = weld[T[t + e]], b = weld[T[t + (e + 1) % 3]], c = weld[T[t + (e + 2) % 3]];
                    if (a == b) continue;
                    long k = EKey(a, b);
                    List<int> l;
                    if (!edgeOpp.TryGetValue(k, out l)) edgeOpp[k] = l = new List<int>();
                    l.Add(c);
                    nbrs[a].Add(b); nbrs[b].Add(a);
                }
            var boundary = new bool[wn];
            foreach (var kv in edgeOpp)
                if (kv.Value.Count != 2) { boundary[(int)(kv.Key >> 32)] = true; boundary[(int)(kv.Key & 0xffffffff)] = true; }

            // New positions for the old (welded) vertices: Loop's rule, half strength so thin legs don't shrink away.
            var moved = new Vector3[wn];
            for (int i = 0; i < wn; i++)
            {
                int k = nbrs[i].Count;
                if (boundary[i] || k < 3) { moved[i] = wpos[i]; continue; }
                float beta = k == 3 ? 3f / 16f : 3f / (8f * k);
                Vector3 sum = Vector3.zero;
                foreach (int j in nbrs[i]) sum += wpos[j];
                Vector3 loop = wpos[i] * (1f - k * beta) + sum * beta;
                moved[i] = Vector3.Lerp(wpos[i], loop, 0.6f);
            }

            var nV = new List<Vector3>(); var nUV = new List<Vector2>(); var nBW = new List<BoneWeight>(); var nT = new List<int>();
            for (int i = 0; i < n; i++) { nV.Add(moved[weld[i]]); nUV.Add(UV[i]); nBW.Add(BW.Length == n ? BW[i] : new BoneWeight()); }
            // Edge midpoints, per split-vertex pair (so UV seams stay seams).
            var mid = new Dictionary<long, int>();
            System.Func<int, int, int> Mid = (i, j) =>
            {
                long k = EKey(i, j);
                int id;
                if (mid.TryGetValue(k, out id)) return id;
                int a = weld[i], b = weld[j];
                Vector3 p = (wpos[a] + wpos[b]) * 0.5f;
                List<int> opp;
                if (a != b && edgeOpp.TryGetValue(EKey(a, b), out opp) && opp.Count == 2)
                {
                    Vector3 loop = (wpos[a] + wpos[b]) * 0.375f + (wpos[opp[0]] + wpos[opp[1]]) * 0.125f;
                    p = Vector3.Lerp(p, loop, 0.6f);
                }
                id = nV.Count;
                nV.Add(p);
                nUV.Add((UV[i] + UV[j]) * 0.5f);
                nBW.Add(BW.Length == n ? Blend(BW[i], BW[j]) : new BoneWeight());
                mid[k] = id;
                return id;
            };
            for (int t = 0; t < T.Length; t += 3)
            {
                int a = T[t], b = T[t + 1], c = T[t + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nT.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }

            var o = new Mesh();
            o.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            o.SetVertices(nV);
            o.SetUVs(0, nUV);
            o.SetTriangles(nT, 0);
            if (BW.Length == n) o.boneWeights = nBW.ToArray();
            o.bindposes = m.bindposes;
            return o;
        }

        /// <summary>
        /// The PS1 mesh was drawn double-sided, so many triangles wind the wrong way. Wind each one so it faces
        /// away from the bone its vertices hang on (closest point on that bone's segment to its child), in bind space.
        /// </summary>
        static int OrientOutward(Mesh m, Transform[] bones)
        {
            var bp = m.bindposes; var V = m.vertices; var T = m.triangles; var BW = m.boneWeights;
            int nb = bp.Length;
            var head = new Vector3[nb]; var tail = new Vector3[nb];
            for (int i = 0; i < nb; i++) { head[i] = bp[i].inverse.MultiplyPoint3x4(Vector3.zero); tail[i] = head[i]; }
            for (int i = 0; i < nb && i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                for (int c = 0; c < bones[i].childCount; c++)
                {
                    int j = System.Array.IndexOf(bones, bones[i].GetChild(c));
                    if (j >= 0 && (head[j] - head[i]).sqrMagnitude > (tail[i] - head[i]).sqrMagnitude) tail[i] = head[j];
                }
            }
            System.Func<int, Vector3> Out = v =>
            {
                int b = BW.Length == V.Length ? BW[v].boneIndex0 : 0;
                Vector3 a = head[b], d = tail[b] - head[b];
                float t = d.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(V[v] - a, d) / d.sqrMagnitude) : 0f;
                return V[v] - (a + d * t);
            };
            int flipped = 0;
            for (int t = 0; t < T.Length; t += 3)
            {
                Vector3 fn = Vector3.Cross(V[T[t + 1]] - V[T[t]], V[T[t + 2]] - V[T[t]]);
                Vector3 o = Out(T[t]) + Out(T[t + 1]) + Out(T[t + 2]);
                // Cross(b-a, c-a) points out of a Unity front face.
                if (Vector3.Dot(fn, o) < 0f) { int x = T[t + 1]; T[t + 1] = T[t + 2]; T[t + 2] = x; flipped++; }
            }
            m.triangles = T;
            return flipped;
        }

        static long EKey(int a, int b) { if (a > b) { int t = a; a = b; b = t; } return ((long)a << 32) | (uint)b; }

        static BoneWeight Blend(BoneWeight x, BoneWeight y)
        {
            var w = new Dictionary<int, float>();
            System.Action<int, float> Add = (i, v) => { if (v <= 0f) return; float c; w.TryGetValue(i, out c); w[i] = c + v * 0.5f; };
            Add(x.boneIndex0, x.weight0); Add(x.boneIndex1, x.weight1); Add(x.boneIndex2, x.weight2); Add(x.boneIndex3, x.weight3);
            Add(y.boneIndex0, y.weight0); Add(y.boneIndex1, y.weight1); Add(y.boneIndex2, y.weight2); Add(y.boneIndex3, y.weight3);
            var l = new List<KeyValuePair<int, float>>(w);
            l.Sort((p, q) => q.Value.CompareTo(p.Value));
            float sum = 0f;
            for (int i = 0; i < l.Count && i < 4; i++) sum += l[i].Value;
            var b = new BoneWeight();
            for (int i = 0; i < l.Count && i < 4; i++)
            {
                float v = l[i].Value / Mathf.Max(1e-6f, sum);
                switch (i)
                {
                    case 0: b.boneIndex0 = l[i].Key; b.weight0 = v; break;
                    case 1: b.boneIndex1 = l[i].Key; b.weight1 = v; break;
                    case 2: b.boneIndex2 = l[i].Key; b.weight2 = v; break;
                    default: b.boneIndex3 = l[i].Key; b.weight3 = v; break;
                }
            }
            return b;
        }

        /// <summary>Normals averaged across UV seams (the PS1 mesh was faceted).</summary>
        static void SmoothNormals(Mesh m)
        {
            var V = m.vertices; var T = m.triangles;
            var acc = new Dictionary<Vector3Int, Vector3>();
            System.Func<Vector3, Vector3Int> K = v => new Vector3Int(Mathf.RoundToInt(v.x * 1000f), Mathf.RoundToInt(v.y * 1000f), Mathf.RoundToInt(v.z * 1000f));
            for (int t = 0; t < T.Length; t += 3)
            {
                Vector3 fn = Vector3.Cross(V[T[t + 1]] - V[T[t]], V[T[t + 2]] - V[T[t]]); // area-weighted
                for (int e = 0; e < 3; e++)
                {
                    var k = K(V[T[t + e]]);
                    Vector3 c; acc.TryGetValue(k, out c); acc[k] = c + fn;
                }
            }
            var N = new Vector3[V.Length];
            for (int i = 0; i < V.Length; i++) N[i] = acc[K(V[i])].normalized;
            m.normals = N;
        }

        // ---------------- textures ----------------

        static Color[] ReadPixels(Texture2D t)
        {
            var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var r = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false, false);
            r.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
            r.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var c = r.GetPixels();
            Object.DestroyImmediate(r);
            return c;
        }

        static void BuildTextures(Texture2D tex, out Color[] col, out float[] height, out int w, out int h)
        {
            int sw = tex.width, sh = tex.height;
            var s = ReadPixels(tex);
            w = sw * Upscale; h = sh * Upscale;
            col = new Color[w * h];
            height = new float[w * h];
            var up = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    up[y * w + x] = Bicubic(s, sw, sh, (x + 0.5f) / Upscale - 0.5f, (y + 0.5f) / Upscale - 0.5f);

            // Unsharp mask (the upscale is soft), then skin: pores, a fine wrinkle grain, veins and wet blood in the dark reds.
            var blur = Blur(up, w, h, 3);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = up[i] + (up[i] - blur[i]) * 0.9f;
                    float u = (float)x / w, v = (float)y / h;
                    float pores = Fbm(u * 180f, v * 180f, 3, 11);
                    float grain = Fbm(u * 60f, v * 60f, 4, 23);
                    float vein = 1f - Mathf.Abs(Fbm(u * 14f, v * 14f, 4, 37) * 2f - 1f);
                    vein = Mathf.Pow(Mathf.Clamp01(vein), 30f) * Mathf.Clamp01((Fbm(u * 5f, v * 5f, 2, 61) - 0.5f) * 4f); // sparse patches
                    float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                    float redness = Mathf.Clamp01((c.r - Mathf.Max(c.g, c.b)) * 3f);
                    float shade = 0.82f + 0.3f * grain - 0.12f * (pores > 0.62f ? 1f : 0f);
                    c *= shade;
                    c = Color.Lerp(c, new Color(0.22f, 0.05f, 0.07f), vein * 0.35f);           // dark veins
                    float blood = Mathf.Clamp01((Fbm(u * 9f, v * 9f, 3, 51) - 0.58f) * 6f) * redness;
                    c = Color.Lerp(c, new Color(0.35f, 0.02f, 0.02f), blood * 0.5f);             // fresh blood in the open wounds
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = 1f;
                    col[i] = c;
                    height[i] = lum * 0.5f + grain * 0.35f - (pores > 0.62f ? 0.08f : 0f) + vein * 0.12f;
                }
        }

        static Color Px(Color[] s, int w, int h, int x, int y) { x = Mathf.Clamp(x, 0, w - 1); y = Mathf.Clamp(y, 0, h - 1); return s[y * w + x]; }

        static float Cub(float t, float a, float b, float c, float d)
        {
            return b + 0.5f * t * (c - a + t * (2f * a - 5f * b + 4f * c - d + t * (3f * (b - c) + d - a)));
        }

        static Color Bicubic(Color[] s, int w, int h, float fx, float fy)
        {
            int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
            float tx = fx - x, ty = fy - y;
            var rows = new Color[4];
            for (int j = -1; j <= 2; j++)
            {
                Color a = Px(s, w, h, x - 1, y + j), b = Px(s, w, h, x, y + j), c = Px(s, w, h, x + 1, y + j), d = Px(s, w, h, x + 2, y + j);
                rows[j + 1] = new Color(Cub(tx, a.r, b.r, c.r, d.r), Cub(tx, a.g, b.g, c.g, d.g), Cub(tx, a.b, b.b, c.b, d.b), 1f);
            }
            var r = new Color(Cub(ty, rows[0].r, rows[1].r, rows[2].r, rows[3].r), Cub(ty, rows[0].g, rows[1].g, rows[2].g, rows[3].g), Cub(ty, rows[0].b, rows[1].b, rows[2].b, rows[3].b), 1f);
            r.r = Mathf.Clamp01(r.r); r.g = Mathf.Clamp01(r.g); r.b = Mathf.Clamp01(r.b);
            return r;
        }

        static Color[] Blur(Color[] s, int w, int h, int r)
        {
            var tmp = new Color[s.Length]; var o = new Color[s.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;
                    for (int k = -r; k <= r; k++) c += Px(s, w, h, x + k, y);
                    tmp[y * w + x] = c / (2 * r + 1);
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.clear;
                    for (int k = -r; k <= r; k++) c += Px(tmp, w, h, x, y + k);
                    o[y * w + x] = c / (2 * r + 1);
                }
            return o;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = x - xi, ty = y - yi;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Fbm(float x, float y, int oct, int seed)
        {
            float s = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < oct; i++) { s += ValueNoise(x, y, seed + i) * amp; norm += amp; x *= 2.03f; y *= 2.03f; amp *= 0.5f; }
            return s / norm;
        }

        static Color[] NormalFromHeight(float[] hgt, int w, int h, float strength)
        {
            var o = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float l = hgt[y * w + Mathf.Max(0, x - 1)], r = hgt[y * w + Mathf.Min(w - 1, x + 1)];
                    float d = hgt[Mathf.Max(0, y - 1) * w + x], u = hgt[Mathf.Min(h - 1, y + 1) * w + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    o[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return o;
        }

        static void Save(string path, Color[] c, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(c);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }
    }
}
