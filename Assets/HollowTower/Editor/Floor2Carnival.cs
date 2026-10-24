using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HollowTower
{
    /// <summary>
    /// The carnival half of the floor 2 party dressing: a carousel, a striped circus-tent ceiling, game booths, popcorn
    /// and cotton-candy carts, a prize shelf of teddy bears, jack-in-the-boxes, balloon arches over the doorways, and
    /// shiny foil balloons (round and star) that mirror a generated studio cubemap so they keep a mylar shine.
    /// </summary>
    public static partial class Floor2PartyBuilder
    {
        static Material[] foil;
        static Material popcorn, glass, black, fur, chrome;
        static Mesh star;

        static readonly Color[] FoilColors =
        {
            new Color(0.85f, 0.86f, 0.9f), new Color(1f, 0.78f, 0.35f), new Color(1f, 0.35f, 0.65f), new Color(0.35f, 0.6f, 1f),
            new Color(0.7f, 0.4f, 1f), new Color(1f, 0.25f, 0.25f), new Color(0.35f, 1f, 0.7f),
        };

        static void LoadCarnivalAssets()
        {
            foil = new Material[FoilColors.Length];
            // mirror-finish mylar; it reflects the FoilStudio cubemap (see FoilProbes), not the dim room
            for (int i = 0; i < FoilColors.Length; i++)
            {
                foil[i] = Mat("Mat_Foil_" + i, FoilColors[i], 1f, 0.9f);
                foil[i].DisableKeyword("_EMISSION");
                foil[i].SetColor("_EmissionColor", Color.black);
            }
            popcorn = Mat("Mat_Popcorn", new Color(1f, 0.93f, 0.7f), 0f, 0.2f);
            glass = Transparent(Mat("Mat_CartGlass", new Color(0.75f, 0.85f, 0.9f, 0.18f), 0f, 0.95f));
            black = Mat("Mat_PartyBlack", new Color(0.06f, 0.06f, 0.07f), 0.1f, 0.4f);
            fur = Mat("Mat_TeddyFur", new Color(0.62f, 0.42f, 0.25f), 0f, 0.05f);
            chrome = Mat("Mat_Chrome", new Color(0.9f, 0.9f, 0.92f), 1f, 0.9f);
            star = MeshAsset("FoilStar", StarMesh(5, 0.32f, 0.15f, 0.09f));
        }

        static Material Transparent(Material m)
        {
            // URP Lit switched to alpha-blended transparent, the same state the material inspector would set
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh StarMesh(int points, float outer, float inner, float depth)
        {
            // a puffy foil star: outline in the XY plane, front and back faces bulged out at the centre, sides joined
            var v = new List<Vector3>(); var t = new List<int>();
            int n = points * 2;
            v.Add(new Vector3(0f, 0f, -depth)); v.Add(new Vector3(0f, 0f, depth));   // 0 front centre, 1 back centre
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;
                float r = i % 2 == 0 ? outer : inner;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            for (int i = 0; i < n; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % n;
                t.AddRange(new[] { 0, b, a });   // front (toward -z)
                t.AddRange(new[] { 1, a, b });   // back
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ foil balloons
        static void FoilBalloon(Transform g, Vector3 c, Random rnd)
        {
            var mat = foil[rnd.Next(foil.Length)];
            Quaternion yaw = Quaternion.Euler(Lerp(-10f, 10f, rnd), Lerp(0f, 360f, rnd), Lerp(-10f, 10f, rnd));
            if (rnd.NextDouble() < 0.4)
                MeshObj(g, "FoilStar", star, c, yaw, Vector3.one * Lerp(0.9f, 1.1f, rnd), mat);
            else
                Prim(PrimitiveType.Sphere, g, "FoilBalloon", c, new Vector3(0.44f, 0.44f, 0.15f), mat, yaw.eulerAngles);
        }

        // A real room probe only mirrors dull walls and a green ceiling, so the foil looks like flat plastic. Mylar reads
        // as shiny because of hard bright/dark contrast, so the foil samples a generated "photo studio" cubemap (dark
        // surround, bright softbox strips, a warm floor bounce) instead. Forward+ picks probes by pixel position and
        // ignores per-renderer anchors, so the cubemap goes on tight high-importance boxes around the foil: one band
        // under each room's ceiling, one per corridor cluster and one per doorway arch.
        static int FoilProbes(Transform root)
        {
            var cube = StudioCube();
            var foilSet = new HashSet<Material>(foil);
            var groups = new Dictionary<Transform, Bounds>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!foilSet.Contains(r.sharedMaterial)) continue;
                Transform key = r.transform.parent;
                if (key.name == "Balloons" && key.parent != root) key = key.parent;   // room ceiling clusters share a band
                Bounds b;
                if (groups.TryGetValue(key, out b)) { b.Encapsulate(r.bounds); groups[key] = b; }
                else groups[key] = r.bounds;
            }
            foreach (var kv in groups)
            {
                var b = kv.Value;
                b.Expand(0.5f);
                var max = b.max; max.y = Mathf.Min(max.y, CeilY - 0.03f);   // stop short of the ceiling tiles
                b.SetMinMax(b.min, max);
                var go = new GameObject("FoilProbe");
                go.transform.SetParent(kv.Key, false);
                go.transform.position = b.center;
                var p = go.AddComponent<ReflectionProbe>();
                p.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                p.customBakedTexture = cube;
                p.size = b.size;
                p.boxProjection = false;
                p.blendDistance = 0f;
                p.importance = 100;
            }
            return groups.Count;
        }

        static Cubemap StudioCube()
        {
            const string path = AssetDir + "/FoilStudio.cubemap";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null)
            {
                const int N = 128;
                cube = new Cubemap(N, TextureFormat.RGBAHalf, true);
                var px = new Color[N * N];
                for (int f = 0; f < 6; f++)
                {
                    for (int y = 0; y < N; y++)
                        for (int x = 0; x < N; x++)
                        {
                            float u = 2f * (x + 0.5f) / N - 1f, v = 2f * (y + 0.5f) / N - 1f;
                            Vector3 d;
                            switch (f)
                            {
                                case 0: d = new Vector3(1f, -v, -u); break;
                                case 1: d = new Vector3(-1f, -v, u); break;
                                case 2: d = new Vector3(u, 1f, v); break;
                                case 3: d = new Vector3(u, -1f, -v); break;
                                case 4: d = new Vector3(u, -v, 1f); break;
                                default: d = new Vector3(-u, -v, -1f); break;
                            }
                            px[y * N + x] = Studio(d.normalized);
                        }
                    cube.SetPixels(px, (CubemapFace)f);
                }
                cube.Apply(true);
                AssetDatabase.CreateAsset(cube, path);
            }
            return cube;
        }

        static Color Studio(Vector3 d)
        {
            float az = Mathf.Atan2(d.z, d.x);
            float c = 0.05f + 0.06f * Mathf.Max(0f, d.y);                                     // dark surround
            c += 3.2f * Mathf.Exp(-Mathf.Pow((d.y - 0.12f) / 0.07f, 2f));                     // ring light near the horizon
            for (int i = 0; i < 5; i++)                                                         // tall softbox strips
            {
                float a = Mathf.DeltaAngle(az * Mathf.Rad2Deg, i * 72f + 15f) / 9f;
                c += (i % 2 == 0 ? 4f : 2.2f) * Mathf.Exp(-a * a) * Mathf.Clamp01(1f - Mathf.Abs(d.y - 0.35f) * 1.6f);
            }
            c += 2.5f * Mathf.Exp(-Mathf.Pow((d.y - 0.95f) / 0.08f, 2f));                     // overhead panel
            var col = new Color(c, c, c * 1.04f, 1f);
            if (d.y < -0.05f) col += new Color(0.35f, 0.24f, 0.13f, 0f) * Mathf.Clamp01(-d.y * 3f);   // wooden floor bounce
            return col;
        }

        // ------------------------------------------------------------------ reflection probes
        static void RoomProbe(Transform parent, Rect r)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(r.center.x, FloorY + 2f, r.center.y);
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            p.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            p.resolution = 128;
            p.boxProjection = true;
            p.size = new Vector3(r.width + 0.4f, CeilY - FloorY, r.height + 0.4f);
            p.intensity = 1.1f;
        }

        // ------------------------------------------------------------------ balloon arches over doorways
        static int DoorArches(Transform parent, Room room, FloorPlan plan, Random rnd)
        {
            int n = 0;
            foreach (int key in room.doors)
            {
                if (key < 0) continue;   // a door in the outer wall (an edge off the grid): no arch outside the building
                int cell = key / 2, row = cell / LevelLayout.Cols, col = cell % LevelLayout.Cols;
                if (row >= LevelLayout.Rows - (key % 2 == 0 ? 0 : 1) || col >= LevelLayout.Cols - (key % 2 == 0 ? 1 : 0)) continue;
                bool east = key % 2 == 0;   // edge between (row, col) and (row, col+1); else (row, col)/(row+1, col)
                Vector3 c; Vector3 into;
                if (east)
                {
                    c = new Vector3((col + 1) * 4f, FloorY, (23 - row) * 4f + 2f);
                    into = plan.zone[row, col] == room.id ? Vector3.left : Vector3.right;
                }
                else
                {
                    c = new Vector3(col * 4f + 2f, FloorY, (23 - row) * 4f);
                    into = plan.zone[row, col] == room.id ? Vector3.forward : Vector3.back;
                }
                Vector3 along = Vector3.Cross(Vector3.up, into).normalized;
                var g = new GameObject("BalloonArch").transform;
                g.SetParent(parent, false);
                const float halfW = 1.1f, spring = 1.6f;
                // up one side, over the top in a half circle, down the other
                var path = new List<Vector3>();
                for (int i = 0; i < 5; i++) path.Add(new Vector3(-halfW, 0.2f + i * (spring - 0.2f) / 5f, 0f));
                for (int i = 0; i <= 10; i++) { float a = Mathf.PI - Mathf.PI * i / 10f; path.Add(new Vector3(Mathf.Cos(a) * halfW, spring + Mathf.Sin(a) * halfW, 0f)); }
                for (int i = 4; i >= 0; i--) path.Add(new Vector3(halfW, 0.2f + i * (spring - 0.2f) / 5f, 0f));
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 p = c + into * 0.35f + along * path[i].x + Vector3.up * path[i].y;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 q = p + into * (k == 1 ? 0.14f : -0.06f) + along * ((k - 1) * 0.08f) + Vector3.up * (k == 1 ? 0.06f : 0f);
                        float s = Lerp(0.24f, 0.32f, rnd);
                        if ((i + k) % 3 == 0) Prim(PrimitiveType.Sphere, g, "ArchFoil", q, new Vector3(s, s, s), foil[(i + k) % foil.Length]);
                        else Prim(PrimitiveType.Sphere, g, "ArchBalloon", q, new Vector3(s, s * 1.1f, s), balloon[(i * 2 + k) % balloon.Length]);
                    }
                }
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ big pieces
        static void Carousel(Transform parent, Vector3 at, Random rnd)
        {
            var g = new GameObject("Carousel").transform;
            g.SetParent(parent, false);
            g.position = at;
            var spin = new GameObject("Turntable").transform;
            spin.SetParent(g, false);
            Local(g, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.12f, 0f), new Vector3(5.4f, 0.12f, 5.4f), black, true);
            Local(spin, PrimitiveType.Cylinder, "Deck", new Vector3(0f, 0.28f, 0f), new Vector3(5.1f, 0.05f, 5.1f), flat[1]);
            Local(spin, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.9f, 0f), new Vector3(0.5f, 1.6f, 0.5f), gold);
            // the striped canopy: a cone of alternating panels, and a scalloped rim with bulbs
            var roof = MeshObj(spin, "Canopy", cone, Vector3.zero, Quaternion.identity, Vector3.one, flat[0]);
            roof.transform.localPosition = new Vector3(0f, 3.15f, 0f); roof.transform.localScale = new Vector3(5.6f, 0.85f, 5.6f);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var rim = Local(spin, PrimitiveType.Cube, "Rim", new Vector3(Mathf.Cos(a) * 2.75f, 3.05f, Mathf.Sin(a) * 2.75f), new Vector3(1.1f, 0.32f, 0.05f), i % 2 == 0 ? flat[0] : white);
                rim.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                Local(spin, PrimitiveType.Sphere, "Bulb", new Vector3(Mathf.Cos(a) * 2.78f, 2.86f, Mathf.Sin(a) * 2.78f), Vector3.one * 0.09f, flame);
            }
            // horses on brass poles, each at its own height
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                var hp = new Vector3(Mathf.Cos(a) * 1.85f, 0f, Mathf.Sin(a) * 1.85f);
                Local(spin, PrimitiveType.Cylinder, "HorsePole", hp + Vector3.up * 1.65f, new Vector3(0.05f, 1.4f, 0.05f), gold);
                var horse = new GameObject("Horse").transform;
                horse.SetParent(spin, false);
                horse.localPosition = hp + Vector3.up * (1.05f + (i % 3) * 0.18f);
                horse.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);   // facing round the ring
                var coat = i % 2 == 0 ? white : flat[(i + 3) % flat.Length];
                Local(horse, PrimitiveType.Capsule, "Body", Vector3.zero, new Vector3(0.34f, 0.5f, 0.34f), coat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var neck = Local(horse, PrimitiveType.Capsule, "Neck", new Vector3(0f, 0.25f, 0.42f), new Vector3(0.16f, 0.24f, 0.16f), coat);
                neck.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
                Local(horse, PrimitiveType.Cube, "Head", new Vector3(0f, 0.42f, 0.6f), new Vector3(0.16f, 0.17f, 0.32f), coat);
                Local(horse, PrimitiveType.Cube, "Mane", new Vector3(0f, 0.36f, 0.4f), new Vector3(0.05f, 0.22f, 0.3f), gold);
                Local(horse, PrimitiveType.Cube, "Saddle", new Vector3(0f, 0.17f, -0.05f), new Vector3(0.36f, 0.06f, 0.32f), flat[i % flat.Length]);
                foreach (float lz in new[] { -0.3f, 0.3f })
                {
                    var leg = Local(horse, PrimitiveType.Cylinder, "Leg", new Vector3(0f, -0.28f, lz), new Vector3(0.07f, 0.22f, 0.07f), coat);
                    leg.transform.localRotation = Quaternion.Euler(lz > 0f ? -30f : 30f, 0f, 0f);   // mid-gallop
                }
            }
            spin.gameObject.AddComponent<PartySpinner>();
            g.rotation = Quaternion.Euler(0f, Lerp(0f, 360f, rnd), 0f);
        }

        /// <summary>A circus-tent ceiling: alternating red and white swags from the room's centre out to the walls.</summary>
        static void TentCeiling(Transform parent, Rect r)
        {
            var g = new GameObject("TentCeiling").transform;
            g.SetParent(parent, false);
            Vector3 top = new Vector3(r.center.x, CeilY - 0.05f, r.center.y);
            int strips = 24;
            for (int i = 0; i < strips; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / strips;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // run out to where this direction meets the room's edge
                float tx = Mathf.Abs(dir.x) > 0.001f ? (r.width * 0.5f - 0.2f) / Mathf.Abs(dir.x) : 999f;
                float tz = Mathf.Abs(dir.z) > 0.001f ? (r.height * 0.5f - 0.2f) / Mathf.Abs(dir.z) : 999f;
                float reach = Mathf.Min(tx, tz);
                Vector3 end = top + dir * reach + Vector3.down * 1.0f;
                Vector3 mid = (top + end) * 0.5f + Vector3.down * 0.25f;   // the cloth sags between centre and wall
                float width = 2f * Mathf.PI * reach / strips * 1.05f;
                foreach (var seg in new[] { new[] { top, mid }, new[] { mid, end } })
                {
                    Vector3 c = (seg[0] + seg[1]) * 0.5f;
                    float len = Vector3.Distance(seg[0], seg[1]);
                    var strip = Prim(PrimitiveType.Cube, g, "Swag", c, new Vector3(width * (seg[0] == top ? 0.55f : 1f), 0.01f, len), i % 2 == 0 ? flat[0] : white);
                    strip.transform.rotation = Quaternion.LookRotation(seg[1] - seg[0], Vector3.up);
                }
            }
            Prim(PrimitiveType.Sphere, g, "Crown", top + Vector3.down * 0.1f, new Vector3(0.6f, 0.4f, 0.6f), gold);
        }

        // ------------------------------------------------------------------ booths and carts
        static void StripedAwning(Transform g, Vector3 localFront, float width, float depth, float height, Material a, Material b)
        {
            int slats = Mathf.Max(4, Mathf.RoundToInt(width / 0.3f));
            for (int i = 0; i < slats; i++)
            {
                float x = -width * 0.5f + (i + 0.5f) * width / slats;
                var s = Local(g, PrimitiveType.Cube, "Awning", localFront + new Vector3(x, height, -depth * 0.5f), new Vector3(width / slats, 0.03f, depth), i % 2 == 0 ? a : b);
                s.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
                Local(g, PrimitiveType.Cube, "Scallop", localFront + new Vector3(x, height - 0.18f, 0.04f), new Vector3(width / slats, 0.2f, 0.02f), i % 2 == 0 ? a : b);
            }
        }

        static void Sign(Transform g, Vector3 localPos, string text, float size, Color color)
        {
            var go = new GameObject("Sign_" + text);
            go.transform.SetParent(g, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // reads from the booth's front (-z side of the text = +z of the booth)
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold; tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = textMat;
        }

        /// <summary>A game booth: counter, striped awning, a sign, and either a bottle pyramid or ring-toss pegs behind.</summary>
        static void GameBooth(Transform parent, Vector3 at, float yaw, bool rings, Random rnd)
        {
            var g = new GameObject(rings ? "RingTossBooth" : "BottleBooth").transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var red = flat[0];
            Local(g, PrimitiveType.Cube, "Counter", new Vector3(0f, 0.5f, 0.4f), new Vector3(2.6f, 1f, 0.5f), red, true);
            for (int i = 0; i < 6; i++) Local(g, PrimitiveType.Cube, "CounterStripe", new Vector3(-1.1f + i * 0.44f, 0.5f, 0.66f), new Vector3(0.2f, 0.98f, 0.02f), white);
            Local(g, PrimitiveType.Cube, "CounterTop", new Vector3(0f, 1.02f, 0.4f), new Vector3(2.7f, 0.05f, 0.6f), wood);
            foreach (float x in new[] { -1.3f, 1.3f }) Local(g, PrimitiveType.Cylinder, "Post", new Vector3(x, 1.25f, 0.6f), new Vector3(0.08f, 1.25f, 0.08f), white);
            Local(g, PrimitiveType.Cube, "Back", new Vector3(0f, 1.25f, -0.9f), new Vector3(2.6f, 2.5f, 0.06f), flat[5], true);
            StripedAwning(g, new Vector3(0f, 0f, 0.7f), 2.8f, 1.6f, 2.5f, red, white);
            Local(g, PrimitiveType.Cube, "SignBoard", new Vector3(0f, 2.95f, 0.72f), new Vector3(2f, 0.45f, 0.05f), flat[1]);
            Sign(g, new Vector3(0f, 2.95f, 0.75f), rings ? "RING TOSS" : "KNOCK 'EM DOWN", 0.06f, new Color(0.7f, 0.05f, 0.1f));
            if (rings)
            {
                for (int x = 0; x < 5; x++) for (int z = 0; z < 2; z++)
                {
                    var bp = new Vector3(-0.9f + x * 0.45f, 0f, -0.15f - z * 0.4f);
                    Local(g, PrimitiveType.Cylinder, "Peg", bp + Vector3.up * 1.3f, new Vector3(0.04f, 0.25f, 0.04f), gold);
                    Local(g, PrimitiveType.Cube, "PegBlock", bp + Vector3.up * 1.03f, new Vector3(0.3f, 0.06f, 0.3f), flat[(x + z) % flat.Length]);
                }
                Local(g, PrimitiveType.Cube, "PegTable", new Vector3(0f, 0.5f, -0.35f), new Vector3(2.3f, 1f, 0.9f), wood, true);
                for (int i = 0; i < 4; i++)   // rings left on the counter, one hooked on a peg
                {
                    var ring = Local(g, PrimitiveType.Cylinder, "Ring", new Vector3(-0.7f + i * 0.35f, 1.06f, 0.45f), new Vector3(0.22f, 0.008f, 0.22f), flat[(i + 2) % flat.Length]);
                    if (i == 3) { ring.transform.localPosition = new Vector3(0.45f, 1.2f, -0.15f); ring.transform.localRotation = Quaternion.Euler(20f, 0f, 0f); }
                }
            }
            else
            {
                Local(g, PrimitiveType.Cube, "Shelf", new Vector3(0f, 1.1f, -0.5f), new Vector3(2.2f, 0.05f, 0.45f), wood, true);
                Local(g, PrimitiveType.Cube, "ShelfLeg", new Vector3(0f, 0.55f, -0.5f), new Vector3(2f, 1.1f, 0.4f), flat[2], true);
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 3 - row; i++)
                    {
                        var bp = new Vector3(-0.55f + i * 0.2f + row * 0.1f + (row == 0 ? 0f : 0f), 1.22f + row * 0.24f, -0.5f);
                        Local(g, PrimitiveType.Cylinder, "Bottle", bp, new Vector3(0.1f, 0.11f, 0.1f), glass);
                        Local(g, PrimitiveType.Cylinder, "Bottle", bp + new Vector3(0.95f, 0f, 0f), new Vector3(0.1f, 0.11f, 0.1f), glass);
                    }
                Local(g, PrimitiveType.Sphere, "Ball", new Vector3(-0.8f, 1.12f, 0.45f), Vector3.one * 0.12f, white);
            }
            // prizes hanging from the awning frame
            for (int i = 0; i < 3; i++) Teddy(g, new Vector3(-0.9f + i * 0.9f, 1.9f, 0.55f), 0.55f, rnd);
        }

        static void Teddy(Transform parent, Vector3 local, float size, Random rnd)
        {
            var g = new GameObject("Teddy").transform;
            g.SetParent(parent, false);
            g.localPosition = local;
            g.localRotation = Quaternion.Euler(Lerp(-8f, 8f, rnd), 180f + Lerp(-15f, 15f, rnd), Lerp(-10f, 10f, rnd));
            g.localScale = Vector3.one * size;
            var coat = rnd.NextDouble() < 0.6 ? fur : flat[4];
            Local(g, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.25f, 0f), new Vector3(0.42f, 0.5f, 0.36f), coat);
            Local(g, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.65f, 0f), new Vector3(0.34f, 0.32f, 0.32f), coat);
            foreach (float x in new[] { -0.13f, 0.13f })
            {
                Local(g, PrimitiveType.Sphere, "Ear", new Vector3(x, 0.8f, 0f), new Vector3(0.12f, 0.12f, 0.06f), coat);
                Local(g, PrimitiveType.Sphere, "Eye", new Vector3(x * 0.55f, 0.69f, -0.15f), Vector3.one * 0.035f, black);
                Local(g, PrimitiveType.Sphere, "Arm", new Vector3(x * 1.9f, 0.32f, -0.03f), new Vector3(0.12f, 0.24f, 0.12f), coat);
                Local(g, PrimitiveType.Sphere, "Foot", new Vector3(x * 1.2f, 0.02f, -0.08f), new Vector3(0.14f, 0.1f, 0.18f), coat);
            }
            Local(g, PrimitiveType.Sphere, "Snout", new Vector3(0f, 0.6f, -0.14f), new Vector3(0.12f, 0.09f, 0.08f), white);
            Local(g, PrimitiveType.Cube, "Bow", new Vector3(0f, 0.48f, -0.16f), new Vector3(0.16f, 0.06f, 0.03f), flat[0]);
        }

        static void PopcornCart(Transform parent, Vector3 at, float yaw, bool cottonCandy, Random rnd)
        {
            var g = new GameObject(cottonCandy ? "CottonCandyCart" : "PopcornCart").transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var body = cottonCandy ? flat[4] : flat[0];
            Local(g, PrimitiveType.Cube, "Cabinet", new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.7f, 0.7f), body, true);
            Local(g, PrimitiveType.Cube, "Trim", new Vector3(0f, 0.92f, 0f), new Vector3(1.24f, 0.05f, 0.74f), gold);
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                var wheel = Local(g, PrimitiveType.Cylinder, "Wheel", new Vector3(x, 0.22f, 0.38f), new Vector3(0.4f, 0.03f, 0.4f), flat[1]);
                wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var hub = Local(g, PrimitiveType.Cylinder, "Hub", new Vector3(x, 0.22f, 0.4f), new Vector3(0.08f, 0.03f, 0.08f), chrome);
                hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (!cottonCandy)
            {
                // glass case heaped with popcorn
                Local(g, PrimitiveType.Cube, "Case", new Vector3(0f, 1.28f, 0f), new Vector3(1.0f, 0.7f, 0.6f), glass);
                for (int i = 0; i < 40; i++)
                    Local(g, PrimitiveType.Sphere, "Kernel", new Vector3(Lerp(-0.42f, 0.42f, rnd), 0.98f + Lerp(0f, 0.22f, rnd), Lerp(-0.25f, 0.25f, rnd)), Vector3.one * Lerp(0.07f, 0.11f, rnd), popcorn);
                for (int i = 0; i < 3; i++)
                {
                    var bag = Local(g, PrimitiveType.Cube, "Bag", new Vector3(-0.3f + i * 0.3f, 1.06f, 0.42f), new Vector3(0.14f, 0.24f, 0.1f), white);
                    Local(bag.transform, PrimitiveType.Cube, "Stripe", Vector3.zero, new Vector3(0.35f, 1.01f, 1.01f), flat[0]);
                }
            }
            else
            {
                Local(g, PrimitiveType.Cylinder, "Tub", new Vector3(0f, 1.05f, 0f), new Vector3(0.6f, 0.12f, 0.6f), chrome);
                for (int i = 0; i < 5; i++)
                {
                    var sp = new Vector3(-0.4f + i * 0.2f, 1.3f, 0.3f);
                    Local(g, PrimitiveType.Cylinder, "Stick", sp, new Vector3(0.015f, 0.18f, 0.015f), white);
                    Local(g, PrimitiveType.Sphere, "Floss", sp + Vector3.up * 0.25f, new Vector3(0.22f, 0.26f, 0.22f), i % 2 == 0 ? icing : flat[7]);
                }
            }
            foreach (float x in new[] { -0.55f, 0.55f }) Local(g, PrimitiveType.Cylinder, "Pole", new Vector3(x, 1.6f, -0.3f), new Vector3(0.04f, 0.7f, 0.04f), chrome);
            var roof = MeshObj(g, "Roof", cone, Vector3.zero, Quaternion.identity, Vector3.one, cottonCandy ? flat[7] : flat[0]);
            roof.transform.localPosition = new Vector3(0f, 2.3f, 0f); roof.transform.localScale = new Vector3(1.6f, 0.4f, 1.6f);
            Local(g, PrimitiveType.Cube, "SignBoard", new Vector3(0f, 2.2f, 0.62f), new Vector3(1.1f, 0.3f, 0.04f), white);
            Sign(g, new Vector3(0f, 2.2f, 0.645f), cottonCandy ? "COTTON CANDY" : "POPCORN", 0.032f, new Color(0.8f, 0.05f, 0.1f));
        }

        static void PrizeShelf(Transform parent, Vector3 at, float yaw, Random rnd)
        {
            var g = new GameObject("PrizeShelf").transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            Local(g, PrimitiveType.Cube, "Frame", new Vector3(0f, 1.1f, -0.25f), new Vector3(2.2f, 2.2f, 0.08f), flat[5], true);
            for (int s = 0; s < 3; s++)
            {
                Local(g, PrimitiveType.Cube, "Shelf", new Vector3(0f, 0.35f + s * 0.65f, 0f), new Vector3(2.2f, 0.05f, 0.5f), white, true);
                for (int i = 0; i < 4; i++) Teddy(g, new Vector3(-0.8f + i * 0.53f + Lerp(-0.05f, 0.05f, rnd), 0.38f + s * 0.65f, 0f), Lerp(0.5f, 0.65f, rnd), rnd);
            }
            Local(g, PrimitiveType.Cube, "Header", new Vector3(0f, 2.35f, -0.2f), new Vector3(2.2f, 0.35f, 0.06f), flat[1]);
            Sign(g, new Vector3(0f, 2.35f, -0.165f), "PRIZES!", 0.06f, new Color(0.7f, 0.05f, 0.15f));
        }

        static void JackInTheBox(Transform parent, Vector3 at, Random rnd)
        {
            var g = new GameObject("JackInTheBox").transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(at, Quaternion.Euler(0f, Lerp(0f, 360f, rnd), 0f));
            Local(g, PrimitiveType.Cube, "Box", new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), flat[2], true);
            foreach (var f in new[] { new Vector3(0f, 0.45f, 0.455f), new Vector3(0f, 0.45f, -0.455f) })
                Local(g, PrimitiveType.Cube, "Panel", f, new Vector3(0.7f, 0.7f, 0.01f), flat[1]);
            var lid = Local(g, PrimitiveType.Cube, "Lid", new Vector3(0f, 1.15f, -0.5f), new Vector3(0.9f, 0.04f, 0.9f), flat[2]);
            lid.transform.localRotation = Quaternion.Euler(-105f, 0f, 0f);
            for (int i = 0; i < 6; i++)   // the spring, coil by coil
                Local(g, PrimitiveType.Cylinder, "Coil", new Vector3(0f, 0.95f + i * 0.09f, 0f), new Vector3(0.28f, 0.012f, 0.28f), chrome);
            Local(g, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0f), new Vector3(0.42f, 0.44f, 0.42f), white);
            Local(g, PrimitiveType.Sphere, "Nose", new Vector3(0f, 1.6f, 0.21f), Vector3.one * 0.1f, flat[0]);
            foreach (float x in new[] { -0.08f, 0.08f }) Local(g, PrimitiveType.Sphere, "Eye", new Vector3(x, 1.7f, 0.18f), Vector3.one * 0.05f, black);
            Local(g, PrimitiveType.Cube, "Grin", new Vector3(0f, 1.5f, 0.19f), new Vector3(0.18f, 0.025f, 0.02f), flat[0]);
            Local(g, PrimitiveType.Sphere, "Ruff", new Vector3(0f, 1.42f, 0f), new Vector3(0.55f, 0.1f, 0.55f), flat[4]);
            var hat = MeshObj(g, "Hat", cone, Vector3.zero, Quaternion.identity, Vector3.one, flat[5]);
            hat.transform.localPosition = new Vector3(0f, 1.8f, 0f); hat.transform.localScale = new Vector3(0.3f, 0.42f, 0.3f); hat.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        }
    }
}
