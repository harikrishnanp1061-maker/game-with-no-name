using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Large, room-sized pieces that stand out in the open floor of big rooms and break the sightlines across them
    /// (furniture under dust sheets, stacked desks, exhibition panels, scaffolding, crate stacks, a boiler).
    /// Pivot rules as in ProcKit: bottom centre, front facing +Z.
    /// </summary>
    public static partial class ProcKit
    {
        // Pale cotton: dark sheets read as mounds of earth in the dim rooms, white ones read as covered furniture.
        static Material DustCloth(int i)
        {
            switch (i % 3)
            {
                case 0: return M("DustSheetWhite", new Color(0.84f, 0.83f, 0.79f), 0, 0.05f);
                case 1: return M("DustSheetOffWhite", new Color(0.78f, 0.76f, 0.7f), 0, 0.05f);
                default: return M("DustSheetLinen", new Color(0.8f, 0.75f, 0.64f), 0, 0.05f);
            }
        }
        static Material Plywood { get { return M("Plywood", new Color(0.62f, 0.49f, 0.33f), 0, 0.12f); } }
        static Material Timber { get { return M("Timber", new Color(0.56f, 0.44f, 0.29f), 0, 0.1f); } }
        static Material HoardingPaint(int i)
        {
            switch (i % 3)
            {
                case 0: return M("HoardingCream", new Color(0.68f, 0.66f, 0.58f), 0, 0.15f);
                case 1: return M("HoardingGreen", new Color(0.33f, 0.41f, 0.35f), 0, 0.15f);
                default: return M("HoardingGrey", new Color(0.52f, 0.53f, 0.52f), 0, 0.15f);
            }
        }
        static Material Tarp(int i)
        {
            return i % 2 == 0 ? M("TarpBlue", new Color(0.11f, 0.19f, 0.31f), 0, 0.35f) : M("TarpGreen", new Color(0.2f, 0.25f, 0.2f), 0, 0.3f);
        }
        static Material ScaffoldTube { get { return M("ScaffoldTube", new Color(0.48f, 0.5f, 0.51f), 0.8f, 0.4f, null, Tex + "RustedSteel_Normal.png"); } }
        static Material PlankWood { get { return M("ScaffoldPlank", new Color(0.45f, 0.35f, 0.22f), 0, 0.15f); } }
        static Material Cardboard { get { return M("Cardboard", new Color(0.5f, 0.38f, 0.24f), 0, 0.12f); } }
        static Material CrateWood { get { return M("CrateWood", new Color(0.42f, 0.33f, 0.22f), 0, 0.15f); } }

        // ------------------------------------------------------------------ smooth meshes for the big round things
        static Mesh smoothCylinder;
        /// <summary>24-sided cylinder with smooth sides, radius 0.5, y from -1 to 1 (drop-in for LowCylinder).</summary>
        static Mesh SmoothCylinder()
        {
            return SharedMesh(ref smoothCylinder, "SmoothCylinder", () =>
            {
                const int n = 24;
                var v = new List<Vector3>(); var nr = new List<Vector3>(); var t = new List<int>();
                for (int i = 0; i <= n; i++)
                {
                    float a = i * Mathf.PI * 2 / n;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    v.Add(d * 0.5f + Vector3.down); nr.Add(d);
                    v.Add(d * 0.5f + Vector3.up); nr.Add(d);
                }
                for (int i = 0; i < n; i++) { int b = i * 2; t.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 }); }
                foreach (var y in new[] { 1f, -1f })
                {
                    int c0 = v.Count;
                    v.Add(new Vector3(0, y, 0)); nr.Add(new Vector3(0, y, 0));
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * Mathf.PI * 2 / n;
                        v.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f)); nr.Add(new Vector3(0, y, 0));
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % n;
                        if (y > 0) t.AddRange(new[] { c0, b, a }); else t.AddRange(new[] { c0, a, b });
                    }
                }
                var m = new Mesh();
                m.SetVertices(v); m.SetNormals(nr); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            });
        }

        static GameObject CylS(Transform parent, Vector3 center, float radius, float height, Material m, Vector3? euler = null, string name = "Cyl")
        {
            var go = Cyl(parent, center, radius, height, m, euler, name);
            go.GetComponent<MeshFilter>().sharedMesh = SmoothCylinder();
            return go;
        }

        /// <summary>A round bar from a to b (local space of parent).</summary>
        static GameObject Tube(Transform parent, Vector3 a, Vector3 b, float radius, Material m, bool smooth = false, string name = "Tube")
        {
            var go = smooth ? CylS(parent, (a + b) * 0.5f, radius, (b - a).magnitude, m, null, name) : Cyl(parent, (a + b) * 0.5f, radius, (b - a).magnitude, m, null, name);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            return go;
        }

        static GameObject MeshPart(Transform parent, Mesh mesh, Material m, string name, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Instance of a library prop inside a builder, unpacked, its bottom centre moved to pos (builder space).</summary>
        static GameObject PropPart(RoomCtx c, GameObject root, string id, Vector3 pos, Quaternion rot)
        {
            var go = c.Make(id);
            if (go == null) return null;
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (var info in go.GetComponentsInChildren<PropInfo>(true)) Object.DestroyImmediate(info);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = rot;
            var b = PropLibrary.RendererBounds(go);   // the builder root sits at the origin, so world = builder space
            go.transform.localPosition = pos - new Vector3(b.center.x, b.min.y, b.center.z);
            return go;
        }

        static Bounds LocalBounds(GameObject root)
        {
            var b = PropLibrary.RendererBounds(root);
            b.center -= root.transform.position;
            return b;
        }

        // ------------------------------------------------------------------ dust sheet over stacked furniture
        /// <summary>
        /// Furniture pushed together and stacked, then covered with a dust sheet: a cloth surface draped over a few
        /// hidden blocks, with rounded edges, near-vertical sides that flare out at the floor, and wavy folds.
        /// </summary>
        public static GameObject DustSheet(RoomCtx c)
        {
            var root = Root("DustSheet");
            float W = c.Range(2.2f, 3.8f), D = c.Range(1.2f, 2.0f);
            var blocks = new List<Bounds>();
            int n = c.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                float bw = c.Range(0.6f, Mathf.Min(1.7f, W)), bd = c.Range(0.55f, D), h = c.Range(0.75f, 1.5f);
                float x = c.Range(-W * 0.5f + bw * 0.5f, W * 0.5f - bw * 0.5f), z = c.Range(-D * 0.5f + bd * 0.5f, D * 0.5f - bd * 0.5f);
                blocks.Add(new Bounds(new Vector3(x, h * 0.5f, z), new Vector3(bw, h, bd)));
            }
            for (int i = 0, tall = c.Range(1, 3); i < tall; i++)
            {
                var under = blocks[c.Range(0, n)];
                float bw = c.Range(0.4f, Mathf.Min(0.9f, under.size.x)), bd = c.Range(0.4f, Mathf.Min(0.8f, under.size.z));
                float top = Mathf.Min(2.55f, under.max.y + c.Range(0.45f, 1.0f));
                var p = new Vector3(under.center.x + c.Range(-1f, 1f) * (under.extents.x - bw * 0.5f), 0, under.center.z + c.Range(-1f, 1f) * (under.extents.z - bd * 0.5f));
                blocks.Add(new Bounds(new Vector3(p.x, top * 0.5f, p.z), new Vector3(bw, top, bd)));
            }

            const float step = 0.05f, foldAmp = 0.12f;
            float margin = 0.2f + 0.14f * 2.6f + foldAmp + 0.1f;
            float x0 = -W * 0.5f - margin, z0 = -D * 0.5f - margin;
            int nx = Mathf.CeilToInt((W + margin * 2) / step) + 1, nz = Mathf.CeilToInt((D + margin * 2) / step) + 1;
            var h0 = new float[nx, nz];
            float sx = c.Range(0f, 100f), sz = c.Range(0f, 100f);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = x0 + i * step, z = z0 + j * step;
                    // folds: the hem line wanders in and out, which also ripples the hanging sides
                    float fold = (Mathf.PerlinNoise(x * 2.3f + sx, z * 2.3f + sz) - 0.5f) * 2f * foldAmp;
                    float best = 0;
                    foreach (var b in blocks)
                    {
                        float dx = Mathf.Max(0, Mathf.Abs(x - b.center.x) - b.extents.x), dz = Mathf.Max(0, Mathf.Abs(z - b.center.z) - b.extents.z);
                        float flare = 0.2f + 0.14f * b.max.y;
                        float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dz * dz) + fold * Mathf.Clamp01(b.max.y)) / flare);
                        best = Mathf.Max(best, b.max.y * (1 - t * t * (3 - 2 * t)));
                    }
                    if (best > 0.08f) best += (Mathf.PerlinNoise(x * 7f + sz, z * 7f + sx) - 0.5f) * 0.035f;   // small wrinkles
                    h0[i, j] = best;
                }
            // soften the creases where the sheet runs from one block to the next
            var hs = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float s = 0; int k = 0;
                    for (int a = -1; a <= 1; a++)
                        for (int b = -1; b <= 1; b++)
                        {
                            int ii = i + a, jj = j + b;
                            if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) continue;
                            s += h0[ii, jj]; k++;
                        }
                    hs[i, j] = Mathf.Max(h0[i, j] * 0.4f + s / k * 0.6f, 0f);
                }
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = x0 + i * step, z = z0 + j * step;
                    verts.Add(new Vector3(x, Mathf.Max(hs[i, j], 0.004f), z));
                    uvs.Add(new Vector2(x, z));
                }
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, d = a + nx, e = d + 1;
                    if (hs[i, j] < 0.006f && hs[i + 1, j] < 0.006f && hs[i, j + 1] < 0.006f && hs[i + 1, j + 1] < 0.006f) continue;   // plain floor
                    tris.AddRange(new[] { a, d, e, a, e, b });
                }
            var mesh = new Mesh { name = "DustSheet" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshPart(root.transform, mesh, DustCloth(c.Range(0, 3)), "Sheet");
            float maxH = 0; foreach (var b in blocks) maxH = Mathf.Max(maxH, b.max.y);
            Solid(root, new Vector3(0, maxH * 0.5f, 0), new Vector3(W + 0.3f, maxH, D + 0.3f));
            return root;
        }

        // ------------------------------------------------------------------ desks and chairs stacked out of the way
        public static GameObject FurnitureStack(RoomCtx c)
        {
            var root = Root("FurnitureStack");
            string desk = c.Pick("SchoolDesk_01", "SchoolDesk_01", "wooden_table_02");
            string chair = desk == "wooden_table_02" ? c.Pick("dining_chair_02", "plastic_monobloc_chair_01") : c.Pick("SchoolChair_01", "SchoolChair_01", "plastic_monobloc_chair_01");
            var probe = PropPart(c, root, desk, Vector3.zero, Quaternion.identity);
            if (probe == null) { Object.DestroyImmediate(root); return null; }
            var db = PropLibrary.RendererBounds(probe);
            Object.DestroyImmediate(probe);
            float dw = db.size.x, dd = db.size.z, dh = db.size.y;
            int cols = Mathf.Clamp(Mathf.RoundToInt(c.Range(2.2f, 3.4f) / (dw + 0.05f)), 2, 4), rows = c.Chance(0.6f) ? 2 : 1;
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    var p = new Vector3((k - (cols - 1) * 0.5f) * (dw + 0.06f), 0, (r - (rows - 1) * 0.5f) * (dd + 0.08f)) + new Vector3(c.Range(-0.04f, 0.04f), 0, c.Range(-0.04f, 0.04f));
                    float yaw = (c.Chance(0.5f) ? 0 : 180) + c.Range(-4f, 4f);
                    PropPart(c, root, desk, p, Quaternion.Euler(0, yaw, 0));
                    if (c.Chance(0.55f))
                    {
                        // a second desk flipped onto it, tops together, and sometimes a chair on its side across the legs
                        PropPart(c, root, desk, p + new Vector3(c.Range(-0.05f, 0.05f), dh + 0.002f, c.Range(-0.05f, 0.05f)), Quaternion.Euler(180, yaw + c.Range(-6f, 6f), 0));
                        if (c.Chance(0.6f)) PropPart(c, root, chair, p + new Vector3(0, dh * 2f - 0.06f, 0), Quaternion.Euler(0, c.Range(0f, 360f), 90));
                    }
                    else
                        // chairs put up on the desk, seat down, legs in the air
                        PropPart(c, root, chair, p + new Vector3(c.Range(-0.05f, 0.05f), dh + 0.002f, 0), Quaternion.Euler(180, c.Range(0f, 360f), 0));
                }
            // a couple left leaning at the foot of the pile
            for (int i = 0, n = c.Range(0, 3); i < n; i++)
                PropPart(c, root, chair, new Vector3(c.Range(-1f, 1f) * cols * dw * 0.5f, 0, (rows * dd * 0.5f + 0.45f) * (c.Chance(0.5f) ? 1 : -1)), Quaternion.Euler(0, c.Range(0f, 360f), 0));
            var lb = LocalBounds(root);
            Solid(root, new Vector3(lb.center.x, lb.size.y * 0.5f, lb.center.z), new Vector3(lb.size.x, lb.size.y, lb.size.z));
            return root;
        }

        // ------------------------------------------------------------------ freestanding exhibition / notice panels
        public static GameObject DisplayPanels(RoomCtx c)
        {
            var root = Root("DisplayPanels");
            int n = c.Range(3, 6);
            float pw = 1.2f, ph = c.Range(2.05f, 2.35f), lean = c.Range(9f, 22f);
            var face = c.Pick(Fabric, Cork, M("PanelBoard", new Color(0.55f, 0.53f, 0.5f), 0, 0.2f), M("PanelBoardBlue", new Color(0.2f, 0.26f, 0.34f), 0, 0.15f));
            var p = Vector3.zero;
            var joints = new List<Vector3> { p };
            for (int i = 0; i < n; i++)
            {
                float yaw = (i % 2 == 0 ? lean : -lean) + c.Range(-4f, 4f);
                var rot = Quaternion.Euler(0, yaw, 0);
                var dir = rot * Vector3.right;
                var mid = p + dir * pw * 0.5f;
                var panel = Box(root.transform, mid + Vector3.up * (0.08f + ph * 0.5f), new Vector3(pw - 0.04f, ph, 0.04f), face, new Vector3(0, yaw, 0), "Panel");
                panel.AddComponent<BoxCollider>();
                Box(root.transform, mid + Vector3.up * (0.08f + ph + 0.015f), new Vector3(pw - 0.04f, 0.03f, 0.05f), Steel, new Vector3(0, yaw, 0), "Rail");
                // things pinned on both faces: posters, notices, the odd sheet gone missing
                foreach (var side in new[] { 1f, -1f })
                    for (int k = 0, m = c.Range(1, 3); k < m; k++)
                    {
                        if (c.Chance(0.2f)) continue;
                        var poster = Poster(c);
                        poster.transform.SetParent(root.transform, false);
                        poster.transform.localRotation = rot * Quaternion.Euler(0, side > 0 ? 0 : 180, c.Chance(0.2f) ? c.Range(-6f, 6f) : 0);
                        var off = dir * (m == 1 ? c.Range(-0.15f, 0.15f) : (k == 0 ? -0.27f : 0.27f));
                        poster.transform.localPosition = mid + off + Vector3.up * c.Range(1.35f, 1.6f) + rot * Vector3.forward * (0.021f * side);
                    }
                p += dir * pw;
                joints.Add(p);
            }
            foreach (var j in joints)
            {
                Cyl(root.transform, j + Vector3.up * (0.04f + (ph + 0.1f) * 0.5f), 0.025f, ph + 0.1f, Steel, null, "Post");
                Box(root.transform, j + Vector3.up * 0.015f, new Vector3(0.08f, 0.03f, 0.5f), Steel, new Vector3(0, c.Range(-10f, 10f), 0), "Foot");
            }
            return root;
        }

        // ------------------------------------------------------------------ abandoned renovation: temporary partition wall
        /// <summary>
        /// A stud wall put up for a renovation that never finished: plywood sheets on a timber frame, 2.5 m tall and
        /// 3.7-7.3 m long, painted on the front half the time (raw frame always showing at the back), sometimes with a
        /// short return at one end, a sheet taken off and leant against the back, and notices stapled to the front.
        /// </summary>
        public static GameObject Hoarding(RoomCtx c)
        {
            var root = Root("Hoarding");
            int sheets = c.Range(3, 7);
            var face = c.Chance(0.55f) ? HoardingPaint(c.Range(0, 3)) : null;
            float L = HoardingRun(c, root.transform, sheets, face, true);
            if (sheets <= 5 && c.Chance(0.45f))
            {
                // a short return at one end, running back from the face
                int rs = c.Range(1, 3);
                var ret = new GameObject("Return");
                ret.transform.SetParent(root.transform, false);
                float end = c.Chance(0.5f) ? 1 : -1;
                ret.transform.localPosition = new Vector3(end * (L * 0.5f - 0.05f), 0, -rs * 1.22f * 0.5f - 0.05f);
                ret.transform.localRotation = Quaternion.Euler(0, end * 90f, 0);
                HoardingRun(c, ret.transform, rs, face, false);
            }
            // leftovers at the foot of the wall
            if (c.Chance(0.5f))
                for (int i = 0, n = c.Range(1, 3); i < n; i++)
                    Cyl(root.transform, new Vector3(c.Range(-L * 0.4f, L * 0.4f), 0.11f, -0.3f - c.Range(0f, 0.25f)), 0.1f, 0.22f, Plastic(c.Range(0, 4)), null, "PaintTin");
            if (c.Chance(0.4f))
                for (int i = 0, n = c.Range(2, 5); i < n; i++)
                    Box(root.transform, new Vector3(c.Range(-L * 0.35f, L * 0.35f), 0.025f + i * 0.045f, -0.45f), new Vector3(c.Range(0.9f, 2.2f), 0.045f, 0.095f), Timber, new Vector3(0, c.Range(-8f, 8f), 0), "Offcut");
            var lb = LocalBounds(root);
            Solid(root, new Vector3(lb.center.x, lb.size.y * 0.5f, lb.center.z), lb.size);
            return root;
        }

        /// <summary>One straight run of the partition, centred on the parent's origin along X, faced towards +Z.</summary>
        static float HoardingRun(RoomCtx c, Transform parent, int sheets, Material face, bool allowGap)
        {
            const float sw = 1.22f, sh = 2.44f, th = 0.018f, stud = 0.045f, depth = 0.095f, lift = 0.045f;
            float L = sheets * sw;
            var timber = Timber;
            Box(parent, new Vector3(0, lift * 0.5f, 0), new Vector3(L, lift, depth), timber, null, "SolePlate");
            Box(parent, new Vector3(0, lift + sh - stud * 0.5f, 0), new Vector3(L, stud, depth), timber, null, "HeadPlate");
            for (int i = 0; i <= sheets * 2; i++)
            {
                float x = Mathf.Clamp(-L * 0.5f + i * sw * 0.5f, -L * 0.5f + stud * 0.5f, L * 0.5f - stud * 0.5f);
                Box(parent, new Vector3(x, lift + (sh - stud) * 0.5f, 0), new Vector3(stud, sh - stud, depth), timber, null, "Stud");
            }
            Box(parent, new Vector3(0, lift + sh * 0.5f, 0), new Vector3(L - 0.02f, stud, depth * 0.9f), timber, null, "Noggin");
            int missing = allowGap && sheets >= 4 && c.Chance(0.35f) ? c.Range(1, sheets - 1) : -1;
            float zf = depth * 0.5f + th * 0.5f;
            for (int i = 0; i < sheets; i++)
            {
                float x = -L * 0.5f + (i + 0.5f) * sw;
                if (i == missing)
                {
                    // taken off and leant against the back of the frame
                    float lean = c.Range(8f, 13f), back = Mathf.Sin(lean * Mathf.Deg2Rad) * sh;
                    Box(parent, new Vector3(x + c.Range(-0.3f, 0.3f), Mathf.Cos(lean * Mathf.Deg2Rad) * sh * 0.5f, -depth * 0.5f - th - back * 0.5f),
                        new Vector3(sw - 0.004f, sh, th), Plywood, new Vector3(lean, 0, 0), "LooseSheet");
                    continue;
                }
                Box(parent, new Vector3(x, lift + sh * 0.5f, zf), new Vector3(sw - 0.004f, sh, th), Plywood, null, "Sheet");
                if (face != null) Box(parent, new Vector3(x, lift + sh * 0.5f, zf + th * 0.5f + 0.001f), new Vector3(sw - 0.006f, sh - 0.004f, 0.002f), face, null, "Paint");
            }
            // notices stapled to the face
            float front = zf + th * 0.5f + (face != null ? 0.002f : 0f);
            for (int i = 0, n = c.Range(0, sheets); i < n; i++)
            {
                int s = c.Range(0, sheets);
                if (s == missing || c.Chance(0.35f)) continue;
                var poster = Poster(c);
                poster.transform.SetParent(parent, false);
                poster.transform.localRotation = Quaternion.Euler(0, 0, c.Chance(0.3f) ? c.Range(-5f, 5f) : 0);
                poster.transform.localPosition = new Vector3(-L * 0.5f + (s + 0.5f) * sw + c.Range(-0.25f, 0.25f), c.Range(1.3f, 1.7f), front);
            }
            return L;
        }

        // ------------------------------------------------------------------ abandoned renovation: scaffold tower with a tarp
        public static GameObject Scaffold(RoomCtx c)
        {
            var root = Root("Scaffold");
            int bays = c.Range(1, 3);
            float bay = 1.8f, d = 1.05f, L = bays * bay;
            float[] lifts = { 0.15f, 1.2f, 2.2f, 2.95f };
            var tube = ScaffoldTube;
            for (int i = 0; i <= bays; i++)
            {
                float x = -L * 0.5f + i * bay;
                foreach (var z in new[] { -d * 0.5f, d * 0.5f })
                {
                    Cyl(root.transform, new Vector3(x, 1.51f, z), 0.024f, 3.0f, tube, null, "Standard");
                    Box(root.transform, new Vector3(x, 0.005f, z), new Vector3(0.15f, 0.01f, 0.15f), Dark, null, "BasePlate");
                }
                foreach (var y in lifts) Tube(root.transform, new Vector3(x, y, -d * 0.5f - 0.05f), new Vector3(x, y, d * 0.5f + 0.05f), 0.022f, tube, false, "Transom");
            }
            foreach (var z in new[] { -d * 0.5f, d * 0.5f })
                foreach (var y in lifts) Tube(root.transform, new Vector3(-L * 0.5f - 0.08f, y, z), new Vector3(L * 0.5f + 0.08f, y, z), 0.022f, tube, false, "Ledger");
            for (int i = 0; i < bays; i++)
            {
                float xa = -L * 0.5f + i * bay, xb = xa + bay;
                bool flip = i % 2 == 1;
                Tube(root.transform, new Vector3(flip ? xb : xa, 0.15f, -d * 0.5f - 0.04f), new Vector3(flip ? xa : xb, 2.2f, -d * 0.5f - 0.04f), 0.02f, tube, false, "Brace");
                foreach (var y in new[] { 1.2f, 2.2f })
                {
                    if (y < 2f && c.Chance(0.5f)) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        if (c.Chance(0.08f)) continue;
                        Box(root.transform, new Vector3((xa + xb) * 0.5f + c.Range(-0.03f, 0.03f), y + 0.045f, -d * 0.5f + 0.17f + k * 0.235f), new Vector3(bay + 0.12f, 0.04f, 0.22f), PlankWood, new Vector3(0, c.Range(-1.5f, 1.5f), 0), "Board");
                    }
                }
                if (c.Chance(0.5f)) Cyl(root.transform, new Vector3((xa + xb) * 0.5f + c.Range(-0.6f, 0.6f), 2.2f + 0.065f + 0.11f, c.Range(-0.2f, 0.2f)), 0.1f, 0.22f, Plastic(c.Range(0, 4)), null, "PaintTin");
            }
            // tarp hung on the front face, tied along the top rail; it covers part of the tower and ripples
            int tb0 = c.Range(0, bays), tb1 = c.Chance(0.5f) ? bays : tb0 + 1;
            float tx0 = -L * 0.5f + tb0 * bay - 0.05f, tx1 = -L * 0.5f + tb1 * bay + 0.05f, top = 2.95f, bottom = c.Range(0.2f, 0.6f);
            int nu = Mathf.CeilToInt((tx1 - tx0) / 0.08f), nv = 20;
            float ph = c.Range(0f, 6f), zf = d * 0.5f + 0.05f;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int b0 = v.Count;
                for (int j = 0; j <= nv; j++)
                    for (int i = 0; i <= nu; i++)
                    {
                        float u = (float)i / nu, w = (float)j / nv;
                        float x = Mathf.Lerp(tx0, tx1, u), y = Mathf.Lerp(top, bottom + Mathf.Sin(u * 9f + ph) * 0.06f, w);
                        // ripples grow towards the loose bottom edge; between the ties along the rail the top sags a little
                        float tie = Mathf.Abs(Mathf.Sin(u * Mathf.PI * (tb1 - tb0) * 2));
                        float z = zf + (Mathf.Sin(x * 7f + ph) * 0.035f + Mathf.Sin(x * 2.3f + ph * 0.5f) * 0.05f) * (0.3f + w) + tie * 0.02f;
                        v.Add(new Vector3(x, y - (j == 0 ? 0 : tie * 0.04f), z + side * 0.003f));
                    }
                for (int j = 0; j < nv; j++)
                    for (int i = 0; i < nu; i++)
                    {
                        int a = b0 + j * (nu + 1) + i, b = a + 1, e = a + nu + 1, f = e + 1;
                        if (side == 0) t.AddRange(new[] { a, b, f, a, f, e }); else t.AddRange(new[] { a, f, b, a, e, f });
                    }
            }
            var mesh = new Mesh { name = "Tarp" };
            mesh.SetVertices(v); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshPart(root.transform, mesh, Tarp(c.Range(0, 2)), "Tarp");
            Solid(root, new Vector3(0, 1.5f, 0), new Vector3(L + 0.2f, 3.0f, d + 0.2f));
            return root;
        }

        // ------------------------------------------------------------------ crates and boxes stacked on pallets
        public static GameObject CrateStack(RoomCtx c)
        {
            var root = Root("CrateStack");
            int cols = c.Range(2, 4), rows = c.Range(1, 3);
            for (int r = 0; r < rows; r++)
                for (int k = 0; k < cols; k++)
                {
                    var p = new Vector3((k - (cols - 1) * 0.5f) * 1.25f, 0, (r - (rows - 1) * 0.5f) * 1.05f);
                    var pal = Pallet(c);
                    pal.transform.SetParent(root.transform, false);
                    pal.transform.localPosition = p;
                    pal.transform.localRotation = Quaternion.Euler(0, c.Range(-3f, 3f), 0);
                    Object.DestroyImmediate(pal.GetComponent<Collider>());
                    float y = 0.14f;
                    int layers = c.Range(2, 4);
                    for (int l = 0; l < layers && y < 2.1f; l++)
                    {
                        bool crate = c.Chance(0.55f);
                        var s = crate ? new Vector3(c.Range(0.9f, 1.15f), c.Range(0.55f, 0.8f), c.Range(0.75f, 0.95f)) : new Vector3(c.Range(0.45f, 0.6f), c.Range(0.35f, 0.5f), c.Range(0.4f, 0.55f));
                        int per = crate ? 1 : 2;
                        for (int q = 0; q < per; q++)
                        {
                            var cpos = p + new Vector3(per == 1 ? c.Range(-0.04f, 0.04f) : (q == 0 ? -0.29f : 0.29f), y + s.y * 0.5f, c.Range(-0.05f, 0.05f));
                            var e = new Vector3(0, c.Range(-5f, 5f), 0);
                            Box(root.transform, cpos, s, crate ? CrateWood : Cardboard, e, crate ? "Crate" : "Box");
                            if (crate)
                                foreach (var fy in new[] { -0.42f, 0.42f })
                                    Box(root.transform, cpos + Quaternion.Euler(e) * new Vector3(0, s.y * fy, 0), new Vector3(s.x + 0.02f, 0.06f, s.z + 0.02f), Wood, e, "Batten");
                            else
                                Box(root.transform, cpos + new Vector3(0, s.y * 0.5f + 0.001f, 0), new Vector3(0.06f, 0.002f, s.z + 0.004f), M("Tape", new Color(0.55f, 0.48f, 0.3f), 0, 0.5f), e, "Tape");
                        }
                        y += s.y + 0.005f;
                    }
                }
            var lb = LocalBounds(root);
            Solid(root, new Vector3(lb.center.x, lb.size.y * 0.5f, lb.center.z), lb.size);
            return root;
        }

        // ------------------------------------------------------------------ plant room machinery
        static Material BoilerPaint(int i)
        {
            switch (i % 3)
            {
                case 0: return M("BoilerGreen", new Color(0.16f, 0.24f, 0.18f), 0.3f, 0.35f, null, Tex + "RustedSteel_Normal.png");
                case 1: return M("BoilerRed", new Color(0.3f, 0.1f, 0.07f), 0.3f, 0.3f, null, Tex + "RustedSteel_Normal.png");
                default: return M("BoilerGrey", new Color(0.3f, 0.31f, 0.3f), 0.4f, 0.35f, null, Tex + "RustedSteel_Normal.png");
            }
        }

        /// <summary>A valve body on a pipe running along pipeAxis, with its handwheel out along wheelAxis.</summary>
        static void Valve(Transform root, Vector3 at, Vector3 pipeAxis, Vector3 wheelAxis)
        {
            var wheel = M("ValveRed", new Color(0.45f, 0.06f, 0.05f), 0.3f, 0.4f);
            var rot = Quaternion.FromToRotation(Vector3.up, wheelAxis.normalized);
            CylS(root, at, 0.1f, 0.16f, Steel, Quaternion.FromToRotation(Vector3.up, pipeAxis.normalized).eulerAngles, "ValveBody");
            var hub = at + wheelAxis.normalized * 0.14f;
            Tube(root, at, hub, 0.012f, Steel, false, "Stem");
            CylS(root, hub, 0.17f, 0.02f, wheel, rot.eulerAngles, "Wheel");
        }

        /// <summary>A fire-tube boiler on concrete saddles, with risers, a header and a flue, all joined up.</summary>
        public static GameObject Boiler(RoomCtx c)
        {
            var root = Root("Boiler");
            var paint = BoilerPaint(c.Range(0, 3));
            float L = c.Range(2.6f, 3.2f), R = c.Range(0.68f, 0.82f), cy = R + 0.35f;
            CylS(root.transform, new Vector3(0, cy, 0), R, L, paint, new Vector3(0, 0, 90), "Drum");
            for (float x = -L * 0.5f + 0.25f; x < L * 0.5f; x += L / 4f) CylS(root.transform, new Vector3(x, cy, 0), R + 0.012f, 0.05f, Steel, new Vector3(0, 0, 90), "Band");
            foreach (var sx in new[] { -0.32f, 0.32f }) Box(root.transform, new Vector3(L * sx, (cy - R * 0.6f) * 0.5f, 0), new Vector3(0.45f, cy - R * 0.6f, R * 1.7f), Concrete, null, "Saddle");
            // front: burner housing and a door
            Box(root.transform, new Vector3(L * 0.5f + 0.32f, cy - 0.1f, 0), new Vector3(0.6f, 0.75f, 0.7f), Dark, null, "Burner");
            CylS(root.transform, new Vector3(L * 0.5f + 0.02f, cy + R * 0.45f, 0), 0.22f, 0.06f, Steel, new Vector3(0, 0, 90), "Hatch");
            CylS(root.transform, new Vector3(L * 0.5f + 0.64f, cy + 0.12f, 0.2f), 0.06f, 0.02f, Porcelain, new Vector3(0, 0, 90), "Gauge");
            // risers to a header that runs along above the drum and drops back to the floor at both ends
            float hy = c.Range(2.55f, 2.85f), hz = c.Range(-0.15f, 0.15f);
            foreach (var x in new[] { -L * 0.25f, L * 0.2f })
            {
                Tube(root.transform, new Vector3(x, cy + R - 0.05f, hz), new Vector3(x, hy, hz), 0.07f, Steel, true, "Riser");
                CylS(root.transform, new Vector3(x, cy + R + 0.05f, hz), 0.11f, 0.05f, Steel, null, "Flange");
            }
            float ex = L * 0.5f + 0.25f;
            Tube(root.transform, new Vector3(-ex, hy, hz), new Vector3(ex, hy, hz), 0.08f, Steel, true, "Header");
            foreach (var x in new[] { -ex, ex })
            {
                CylS(root.transform, new Vector3(x, hy, hz), 0.1f, 0.2f, Steel, null, "Elbow");
                Tube(root.transform, new Vector3(x, hy, hz), new Vector3(x, 0.05f, hz), 0.07f, Steel, true, "Drop");
                CylS(root.transform, new Vector3(x, 0.03f, hz), 0.13f, 0.06f, Steel, null, "FloorFlange");
                Valve(root.transform, new Vector3(x, c.Range(1.1f, 1.4f), hz), Vector3.up, new Vector3(0, 0, c.Chance(0.5f) ? 1 : -1));
            }
            // flue from the back of the drum
            float fx = -L * 0.5f + 0.35f;
            Tube(root.transform, new Vector3(fx, cy + R - 0.1f, -R * 0.45f), new Vector3(fx, 3.0f, -R * 0.45f), 0.17f, Dark, true, "Flue");
            CylS(root.transform, new Vector3(fx, 3.02f, -R * 0.45f), 0.22f, 0.04f, Dark, null, "FlueCap");
            Solid(root, new Vector3(0.15f, 1.5f, 0), new Vector3(L + 1.0f, 3.0f, R * 2f + 0.2f));
            return root;
        }

        /// <summary>A tall water tank on legs with a ladder and an outlet pipe.</summary>
        public static GameObject Tank(RoomCtx c)
        {
            var root = Root("Tank");
            var paint = BoilerPaint(c.Range(0, 3));
            float R = c.Range(0.75f, 0.95f), H = c.Range(2.0f, 2.4f), legs = 0.35f;
            CylS(root.transform, new Vector3(0, legs + H * 0.5f, 0), R, H, paint, null, "Shell");
            CylS(root.transform, new Vector3(0, legs + H + 0.04f, 0), R * 0.92f, 0.08f, paint, null, "Lid");
            for (float y = legs + 0.3f; y < legs + H; y += H / 3f) CylS(root.transform, new Vector3(0, y, 0), R + 0.012f, 0.05f, Steel, null, "Band");
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                Box(root.transform, new Vector3(Mathf.Cos(a) * R * 0.8f, legs * 0.5f, Mathf.Sin(a) * R * 0.8f), new Vector3(0.08f, legs, 0.08f), Steel, null, "Leg");
            }
            // ladder up the side
            foreach (var sx in new[] { -0.2f, 0.2f }) Box(root.transform, new Vector3(sx, (legs + H) * 0.5f + 0.05f, R + 0.12f), new Vector3(0.04f, legs + H + 0.1f, 0.04f), Steel, null, "Stile");
            for (float y = 0.3f; y < legs + H; y += 0.3f) Tube(root.transform, new Vector3(-0.2f, y, R + 0.12f), new Vector3(0.2f, y, R + 0.12f), 0.012f, Steel, false, "Rung");
            Tube(root.transform, new Vector3(R - 0.05f, legs + 0.2f, 0), new Vector3(R + 0.5f, legs + 0.2f, 0), 0.06f, Steel, true, "Outlet");
            Tube(root.transform, new Vector3(R + 0.5f, legs + 0.2f, 0), new Vector3(R + 0.5f, 0.03f, 0), 0.06f, Steel, true, "OutletDrop");
            Valve(root.transform, new Vector3(R + 0.3f, legs + 0.2f, 0), Vector3.right, Vector3.up);
            var lb = LocalBounds(root);
            Solid(root, new Vector3(lb.center.x, lb.size.y * 0.5f, lb.center.z), lb.size);
            return root;
        }
    }
}
