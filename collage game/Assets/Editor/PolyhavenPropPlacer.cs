using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Places the PolyHaven props onto the elevator floors.
///
/// Scale is never guessed. Each asset is rotated onto Y, measured in WORLD
/// space, then scaled so its real height matches the target (targets are the
/// real metres from the asset manifests). The lowest point is always sunk to
/// the floor top, so nothing floats or clips through.
///
/// All measurement goes through localBounds + localToWorldMatrix, which Unity
/// keeps consistent with the local transform chain - so no matrix flushing is
/// ever needed. renderer.bounds, by contrast, is cached and lags by a frame.
/// </summary>
public static class PolyhavenPropPlacer
{
    const string FbxRoot = "Assets/ThirdParty/PolyhavenProps/";
    const string RootName = "Props";

    static int placedCount, missingCount, failedCount;

    [MenuItem("Tools/PolyHaven/Place Classroom Props")]
    public static void PlaceAll()
    {
        placedCount = missingCount = failedCount = 0;

        var existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

        var root = new GameObject(RootName);
        foreach (var floor in Floors())
            foreach (var spec in Plan(floor))
                Place(spec, floor, root.transform);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[PolyHaven] placed=" + placedCount +
                  " missingAsset=" + missingCount + " failed=" + failedCount);
    }

    static void Place(PropSpec s, FloorInfo floor, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxRoot + s.Name + ".fbx");
        if (prefab == null) { missingCount++; return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = s.Name;
        inst.transform.SetParent(parent, false);

        Transform meshRoot = FindMeshRoot(inst.transform);
        if (meshRoot == null) { UnityEngine.Object.DestroyImmediate(inst); failedCount++; return; }

        // local rotation/translation only - localToWorldMatrix stays consistent
        inst.transform.position = new Vector3(
            floor.Center.x + s.Fx * floor.HalfX, 0f,
            floor.Center.z + s.Fz * floor.HalfZ);
        inst.transform.localEulerAngles = new Vector3(0f, s.Ry, 0f);
        meshRoot.localEulerAngles = s.Rot;
        meshRoot.localScale = Vector3.one;

        Bounds b = WorldBounds(inst);
        if (b.size.sqrMagnitude <= 1e-10f)
        { UnityEngine.Object.DestroyImmediate(inst); failedCount++; return; }

        // scale so the measured world height equals the target height
        meshRoot.localScale *= (s.TargetH / b.size.y);
        b = WorldBounds(inst);

        // rest the lowest point on the floor (or at the mount height for fixtures)
        float restY = s.MountY > 0.001f ? floor.Top + s.MountY : floor.Top;
        inst.transform.position += Vector3.up * (restY - b.min.y);

        ApplyRedGlow(inst);
        placedCount++;
    }

    /// <summary>Closest ancestor of the meshes that still sits under the root.</summary>
    static Transform FindMeshRoot(Transform root)
    {
        var mfs = root.GetComponentsInChildren<MeshFilter>();
        if (mfs == null || mfs.Length == 0) return null;

        Transform t = mfs[0].transform;
        while (t != null && t != root)
        {
            bool hasMeshChild = false;
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).GetComponentInChildren<MeshFilter>() != null) hasMeshChild = true;
            if (!hasMeshChild) break;
            t = t.parent;
        }
        return t == null ? root : t;
    }

    /// <summary>
    /// World-space bounds from localBounds + localToWorldMatrix. Flush-free and
    /// always in sync with the local transform chain - unlike renderer.bounds,
    /// which is cached and lags by a frame.
    /// </summary>
    static Bounds WorldBounds(GameObject go)
    {
        float minx = 0, miny = 0, minz = 0, maxx = 0, maxy = 0, maxz = 0;
        bool any = false;

        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            Bounds lb = r.localBounds;
            if (lb.size.sqrMagnitude <= 1e-10f) continue;
            Matrix4x4 m = r.localToWorldMatrix;
            Vector3 c = lb.center, e = lb.extents;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 w = m.MultiplyPoint3x4(corner);
                if (!any)
                { minx = maxx = w.x; miny = maxy = w.y; minz = maxz = w.z; any = true; }
                else
                {
                    if (w.x < minx) minx = w.x; if (w.x > maxx) maxx = w.x;
                    if (w.y < miny) miny = w.y; if (w.y > maxy) maxy = w.y;
                    if (w.z < minz) minz = w.z; if (w.z > maxz) maxz = w.z;
                }
            }
        }

        if (!any) return new Bounds(Vector3.zero, Vector3.zero);
        return new Bounds(
            new Vector3((minx + maxx) * 0.5f, (miny + maxy) * 0.5f, (minz + maxz) * 0.5f),
            new Vector3(maxx - minx, maxy - miny, maxz - minz));
    }

    /// <summary>
    /// Turns a fixture's bulb surface into an emissive lamp glow.
    ///
    /// FBX exports rarely tag a bulb as emissive, so we look for a renderer that
    /// is actually named like a light-emitting surface (bulb / *_glass / tube).
    ///
    /// This used to fall back to "highest + smallest renderer is probably the
    /// bulb" and apply the glow unconditionally, which meant props with no light
    /// source at all - rocks, a dead tree, a chair - were painted solid orange
    /// and rendered as flat silhouettes. Only glow what is genuinely a lamp.
    /// </summary>
    static readonly string[] EmissivePartNames =
    {
        "bulb", "lamp_glass", "light_glass", "sconce_light", "tube", "emissive",
    };

    static void ApplyRedGlow(GameObject inst)
    {
        var rds = inst.GetComponentsInChildren<Renderer>();
        if (rds == null || rds.Length == 0) return;

        // Prefer an explicitly named light part.
        Renderer bulb = null;
        foreach (var rd in rds)
        {
            var n = rd.name.ToLowerInvariant();
            foreach (var p in EmissivePartNames)
            {
                if (!n.Contains(p)) continue;
                bulb = rd;
                break;
            }
            if (bulb != null) break;
        }

        // No named light part: this prop is not a lamp, so leave it alone.
        if (bulb == null) return;

        var mats = bulb.sharedMaterials;
        if (mats == null || mats.Length == 0) return;
        if (!mats[0].HasProperty("_EmissionColor")) return;

        var mat = mats[0];

        // If the asset ships an authored emissive map, drive the glow from it at
        // a modest intensity rather than blowing the surface out.
        if (mat.GetTexture("_EmissionMap") != null)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", new Color(1.6f, 1.3f, 0.9f, 1f));
            return;
        }

        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.AnyEmissive;
        mat.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.06f, 1f) * 3.2f);
    }

    // ------------------------------------------------------------------
    // FLOORS - discovered at runtime from their colliders, never hard-coded
    // ------------------------------------------------------------------
    struct FloorInfo
    {
        public string Name;
        public Vector3 Center;
        public float Top;
        public float HalfX, HalfZ;
    }

    static List<FloorInfo> Floors()
    {
        var list = new List<FloorInfo>();
        var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();

        // Floors sit deep in the hierarchy (TutorialScene/Area01_CrashRoom/
        // CrashRoom_Floor) and carry a MeshRenderer rather than a BoxCollider,
        // so walk every object and match the name suffix. Bounds come from
        // localBounds + localToWorldMatrix - consistent with the transform
        // chain, whereas renderer.bounds is cached and lags by a frame.
        foreach (var root in roots)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.EndsWith("_Floor")) continue;

                var rd = t.GetComponent<MeshRenderer>();
                if (rd == null || rd.localBounds.size.sqrMagnitude <= 1e-10f) continue;

                Bounds lb = rd.localBounds;
                Vector3 c = lb.center, e = lb.extents;
                float minx = 0, miny = 0, minz = 0, maxx = 0, maxy = 0, maxz = 0;
                bool any = false;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 w = rd.localToWorldMatrix.MultiplyPoint3x4(c + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z));
                    if (!any) { minx = maxx = w.x; miny = maxy = w.y; minz = maxz = w.z; any = true; }
                    else
                    {
                        if (w.x < minx) minx = w.x; if (w.x > maxx) maxx = w.x;
                        if (w.y < miny) miny = w.y; if (w.y > maxy) maxy = w.y;
                        if (w.z < minz) minz = w.z; if (w.z > maxz) maxz = w.z;
                    }
                }
                if (!any) continue;

                float halfX = (maxx - minx) * 0.5f * 0.80f;  // margin clears the walls
                float halfZ = (maxz - minz) * 0.5f * 0.80f;

                list.Add(new FloorInfo { Name = t.name,
                    Center = new Vector3((minx + maxx) * 0.5f, 0f, (minz + maxz) * 0.5f),
                    Top = maxy, HalfX = halfX, HalfZ = halfZ });
            }
        }
        list.Sort((a, b) => a.Top.CompareTo(b.Top));
        return list;
    }

    // ------------------------------------------------------------------
    // PLAN
    // Heights are real metres from the asset manifests.
    // Rx = asset is authored standing up Z.  Rz = authored standing up X.
    // fx/fz are fractions of each floor's half-extent.
    // mountY = lift above the floor top (hanging/wall/cabinet fixtures).
    // ------------------------------------------------------------------
    static List<PropSpec> Plan(FloorInfo f)
    {
        var L = new List<PropSpec>();
        const float Rx = -90f;
        const float Rz = 90f;

        float hang = f.Top + 2.30f;   // hanging lamps, ~0.2 m below the ceiling
        float wall = f.Top + 1.60f;   // wall fixtures, eye level

        foreach (var s in PropsFor(f, Rx, Rz, hang, wall)) L.Add(s);
        return L;
    }

    static IEnumerable<PropSpec> PropsFor(FloorInfo f, float Rx, float Rz, float hang, float wall)
    {
        PropSpec P(string n, Vector3 rot, float h, float fx, float fz, float ry, float mountY = 0f)
        {
            return new PropSpec(n, rot, h, f.Name, fx, fz, ry, mountY);
        }

        // ---- street furniture + vegetation ----
        yield return P("street_lamp_01", Vector3.zero, 3.2f, -0.82f, -0.82f, 30f);
        yield return P("dead_tree_trunk_02", new Vector3(0, 0, Rz), 2.6f, 0.84f, -0.78f, 12f);
        yield return P("rock_moss_set_01", new Vector3(0, 0, Rz), 1.35f, 0.80f, 0.80f, 0f);
        yield return P("stone_01", new Vector3(0, 0, Rz), 0.45f, -0.78f, 0.76f, 0f);

        // ---- hanging lamps ----
        yield return P("hanging_industrial_lamp", new Vector3(Rx, 0, 0), 1.15f, -0.30f, -0.30f, 0f, hang);
        yield return P("caged_hanging_light", new Vector3(Rx, 0, 0), 0.85f, 0.30f, 0.30f, 0f, hang);
        yield return P("security_light", new Vector3(Rx, 0, 0), 0.72f, 0.30f, -0.30f, 0f, hang);

        // ---- wall fixtures ----
        yield return P("industrial_wall_lamp", new Vector3(Rx, 0, 0), 0.45f, 0.93f, 0.0f, 0f, wall);
        yield return P("industrial_caged_sconce", new Vector3(Rx, 0, 0), 0.92f, -0.93f, 0.0f, 0f, wall);
        yield return P("wall_clock", new Vector3(90f, 0, 0), 0.45f, 0.0f, -0.94f, 0f, f.Top + 1.85f);
        yield return P("vintage_telephone_wall_clock", new Vector3(Rx, 0, 0), 0.78f, 0.55f, -0.94f, 0f, wall);
        yield return P("mounted_fluorescent_lights", new Vector3(90f, 0, 0), 0.75f, -0.05f, 0.94f, 0f, f.Top + 2.60f);

        // ---- furniture ----
        yield return P("wooden_table_02", new Vector3(Rx, 0, 0), 1.05f, 0.0f, 0.05f, 0f);
        yield return P("SchoolChair_01", new Vector3(Rx, 0, 0), 0.95f, 0.42f, 0.05f, 25f);
        yield return P("SchoolChair_01", new Vector3(Rx, 0, 0), 0.95f, -0.42f, 0.05f, -15f);
        yield return P("SchoolDesk_01", new Vector3(Rx, 0, 0), 0.88f, 0.66f, 0.24f, 5f);
        yield return P("GothicCabinet_01", new Vector3(Rx, 0, 0), 2.4f, -0.85f, 0.55f, 0f);
        yield return P("vintage_cabinet_01", new Vector3(Rx, 0, 0), 2.35f, 0.86f, 0.5f, 0f);
        yield return P("wooden_bookshelf_worn", new Vector3(Rx, 0, 0), 2.25f, 0.50f, 0.88f, 0f);
        yield return P("steel_frame_shelves_01", new Vector3(Rx, 0, 0), 2.35f, -0.50f, 0.88f, 0f);
        yield return P("wooden_display_shelves_01", new Vector3(Rx, 0, 0), 1.95f, 0.88f, 0.12f, 0f);
        yield return P("Shelf_01", new Vector3(Rx, 0, 0), 0.75f, -0.24f, 0.05f, 0f);

        // ---- clutter ----
        yield return P("Barrel_01", new Vector3(Rx, 0, 0), 1.15f, 0.72f, -0.55f, 0f);
        yield return P("power_box_01", new Vector3(Rx, 0, 0), 0.78f, -0.72f, -0.55f, 15f);
        yield return P("metal_toolbox", Vector3.zero, 0.45f, 0.55f, -0.30f, 30f);
        yield return P("utility_box_01", new Vector3(Rx, 0, 0), 1.05f, -0.55f, -0.30f, -20f);
        yield return P("cement_bag", Vector3.zero, 0.65f, 0.75f, 0.18f, 0f);
        yield return P("trashbag", new Vector3(Rx, 0, 0), 0.72f, -0.75f, 0.18f, 0f);
        yield return P("spray_paint_bottles", new Vector3(Rx, 0, 0), 0.28f, 0.62f, -0.15f, 0f);
        yield return P("standing_picture_frame_01", new Vector3(Rx, 0, 0), 0.65f, 0.30f, -0.55f, 0f);
        yield return P("hanging_picture_frame_01", new Vector3(Rx, 0, 0), 1.05f, -0.28f, -0.94f, 0f, wall);
        yield return P("standing_chalkboard_01", new Vector3(Rx, 0, 0), 1.85f, -0.12f, -0.90f, 0f);
        yield return P("projector_screen", new Vector3(Rx, 0, 0), 2.45f, 0.25f, -0.90f, 0f);
        yield return P("ladder_sectioned_01", new Vector3(Rx, 0, 0), 2.50f, 0.94f, -0.42f, 0f);
        yield return P("crowbar_01", new Vector3(0, 0, Rz), 0.85f, 0.28f, -0.15f, 0f);
        yield return P("vintage_flashlight", Vector3.zero, 0.30f, 0.18f, -0.05f, 0f);
        yield return P("fire_alarm", new Vector3(Rx, 0, 0), 0.22f, 0.95f, -0.75f, 0f, wall);

        // ---- ceiling cables & pipes ----
        yield return P("modular_industrial_pipes_01", new Vector3(Rx, 0, 0), 2.50f, 0.75f, 0.0f, 0f, f.Top + 2.05f);
        yield return P("modular_electric_cables", new Vector3(Rx, 0, 0), 0.95f, -0.75f, 0.0f, 0f, f.Top + 2.20f);

        // ---- floor-lying + desk clutter ----
        yield return P("water_manhole_cover", Vector3.zero, 0.95f, 0.35f, 0.40f, 0f);
        yield return P("office_notepads", Vector3.zero, 0.50f, 0.0f, 0.05f, 0f, f.Top + 1.08f);
        yield return P("vintage_stapler", Vector3.zero, 0.12f, 0.14f, 0.02f, 0f, f.Top + 1.09f);
        yield return P("stationery_supplies", new Vector3(Rx, 0, 0), 0.35f, -0.30f, 0.05f, 0f, f.Top + 1.09f);
        yield return P("chemistry_set", new Vector3(Rx, 0, 0), 0.55f, 0.30f, 0.05f, 0f, f.Top + 1.09f);
        yield return P("classic_laptop", new Vector3(Rx, 0, 0), 0.44f, -0.20f, 0.02f, 25f);
    }

    struct PropSpec
    {
        public string Name;
        public Vector3 Rot;
        public float TargetH;
        public string Floor;
        public float Fx, Fz, Ry;
        public float MountY;
        public PropSpec(string n, Vector3 rot, float h, string f, float fx, float fz, float ry, float mountY)
        {
            Name = n; Rot = rot; TargetH = h; Floor = f;
            Fx = fx; Fz = fz; Ry = ry; MountY = mountY;
        }
    }
}
