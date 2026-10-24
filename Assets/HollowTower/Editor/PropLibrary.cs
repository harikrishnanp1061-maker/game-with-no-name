using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Wraps the imported glTF props (Poly Haven CC0, under Assets/HollowTowerRemastered/Props) in prefabs
    /// with a predictable pivot, so the dressing pass never has to trust the source pivots:
    /// floor and surface props sit on their lowest point, wall props on the centre of their back face,
    /// ceiling props on their top. Each prefab's front faces +Z and its scale is corrected to real-world size.
    /// </summary>
    public static class PropLibrary
    {
        public const string SourceDir = "Assets/HollowTowerRemastered/Props";
        public const string PrefabDir = "Assets/HollowTowerRemastered/Props/Prefabs";

        public enum Mount { Floor, Wall, Ceiling }

        class Fix
        {
            public float scale = 1f, yaw;
            public Mount mount = Mount.Floor;
            public bool skip, noCollider, split;   // split: the model is a set of separate items, one prefab each
        }

        // Per-asset corrections, found by measuring the imports and checking the contact sheet.
        static readonly Dictionary<string, Fix> Fixes = new Dictionary<string, Fix>
        {
            { "steel_frame_shelves_01", new Fix { scale = 0.1f } },          // authored 21 m tall
            { "sand_rocks_small_01", new Fix { scale = 0.3f, noCollider = true } },
            { "WoodenChair_01", new Fix { scale = 0.42f } },                 // authored 2.27 m tall
            { "modular_airduct_rectangular_01", new Fix { skip = true } },
            { "wooden_stool_02", new Fix { skip = true } },
            { "vintage_video_camera", new Fix { skip = true } },
            // wall mounted
            { "dartboard", new Fix { mount = Mount.Wall } },
            { "fancy_picture_frame_01", new Fix { mount = Mount.Wall } },
            { "hanging_picture_frame_01", new Fix { mount = Mount.Wall } },
            { "hanging_picture_frame_02", new Fix { mount = Mount.Wall } },
            { "fire_alarm", new Fix { mount = Mount.Wall } },
            { "wall_clock", new Fix { mount = Mount.Wall } },
            { "vintage_telephone_wall_clock", new Fix { mount = Mount.Wall } },
            { "industrial_wall_lamp", new Fix { mount = Mount.Wall } },
            { "industrial_caged_sconce", new Fix { mount = Mount.Wall } },
            { "power_box_01", new Fix { mount = Mount.Wall } },
            { "security_light", new Fix { mount = Mount.Wall } },
            { "security_camera_01", new Fix { mount = Mount.Wall } },
            { "korean_public_payphone_01", new Fix { mount = Mount.Wall } },
            { "modular_industrial_pipes_01", new Fix { skip = true } },     // a kit sheet of loose pipe pieces: reads as a display board, not plumbing
            { "modular_electric_cables", new Fix { skip = true } },         // the same: 49 loose cable pieces laid out side by side
            { "ornate_mirror_01", new Fix { mount = Mount.Wall } },
            // plant/bag sets laid out side by side in one model
            { "anthurium_botany_01", new Fix { split = true, noCollider = true } },
            { "calathea_orbifolia_01", new Fix { split = true, noCollider = true } },
            { "pachira_aquatica_01", new Fix { split = true } },
            { "fern_02", new Fix { split = true, noCollider = true } },
            { "nettle_plant", new Fix { split = true, noCollider = true } },
            { "moss_01", new Fix { skip = true } },
            { "dry_branches_medium_01", new Fix { split = true, noCollider = true } },
            { "compost_bags", new Fix { split = true } },
            { "gothic_statue", new Fix { skip = true } },
            { "hanging_picture_frame_03", new Fix { mount = Mount.Wall } },
            { "Chandelier_01", new Fix { mount = Mount.Ceiling } },
            // ceiling mounted
            { "caged_hanging_light", new Fix { mount = Mount.Ceiling } },
            { "hanging_industrial_lamp", new Fix { mount = Mount.Ceiling } },
            { "ceiling_fan", new Fix { mount = Mount.Ceiling } },
            { "pull_chain_light_socket", new Fix { mount = Mount.Ceiling } },
            { "mounted_fluorescent_lights", new Fix { mount = Mount.Ceiling } },
        };

        /// <summary>Source glTF path per prop id (file name without the Poly Haven "_1k" suffix).</summary>
        public static Dictionary<string, string> FindSources()
        {
            var map = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { SourceDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".gltf")) continue;
                var id = Path.GetFileNameWithoutExtension(path);
                if (id.EndsWith("_1k")) id = id.Substring(0, id.Length - 3);
                map[id] = path;
            }
            return map;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        [MenuItem("Hollow Tower/College Dressing/1. Build Prop Prefabs", priority = 70)]
        public static void BuildPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder(SourceDir, "Prefabs");
            int built = 0;
            foreach (var kv in FindSources())
            {
                Fix fix;
                if (!Fixes.TryGetValue(kv.Key, out fix)) fix = new Fix();
                if (fix.skip) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value);
                if (model == null) { Debug.LogWarning("[PropLibrary] could not load " + kv.Value); continue; }

                if (!fix.split) { built += Build(kv.Key, (GameObject)PrefabUtility.InstantiatePrefab(model), fix); continue; }
                // one prefab per item of the set: find the level where the hierarchy branches, keep one branch each time
                var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(probe, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var level = probe.transform;
                while (level.childCount == 1) level = level.GetChild(0);
                int parts = level.childCount;
                Object.DestroyImmediate(probe);
                for (int i = 0; i < parts; i++)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    var lv = inst.transform;
                    while (lv.childCount == 1) lv = lv.GetChild(0);
                    for (int k = lv.childCount - 1; k >= 0; k--) if (k != i) Object.DestroyImmediate(lv.GetChild(k).gameObject);
                    if (inst.GetComponentsInChildren<Renderer>().Length == 0) { Object.DestroyImmediate(inst); continue; }
                    built += Build(kv.Key + "_" + i, inst, fix);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PropLibrary] built " + built + " prop prefabs in " + PrefabDir);
        }

        static int Build(string name, GameObject inst, Fix fix)
        {
            var root = new GameObject(name);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localRotation = Quaternion.Euler(0f, fix.yaw, 0f);
            inst.transform.localScale = inst.transform.localScale * fix.scale;   // keep the model's own root scale (some are authored in cm)
            inst.transform.localPosition = Vector3.zero;

            var b = RendererBounds(inst);
            Vector3 pivot;
            switch (fix.mount)
            {
                case Mount.Wall: pivot = new Vector3(b.center.x, b.center.y, b.min.z); break;   // back face on the wall
                case Mount.Ceiling: pivot = new Vector3(b.center.x, b.max.y, b.center.z); break;
                default: pivot = new Vector3(b.center.x, b.min.y, b.center.z); break;
            }
            inst.transform.localPosition = -pivot;

            b = RendererBounds(inst);
            bool small = b.size.y < 0.3f && b.size.x < 0.6f && b.size.z < 0.6f;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (small) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
            if (fix.mount == Mount.Floor && !fix.noCollider && !small)
            {
                var col = root.AddComponent<BoxCollider>();
                col.center = b.center;
                col.size = b.size;
            }
            var marker = root.AddComponent<PropInfo>();
            marker.mount = (int)fix.mount;
            marker.size = b.size;
            marker.center = b.center;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return 1;
        }

        public static GameObject Load(string id)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + id + ".prefab");
        }

        /// <summary>Renders every prop prefab from the front-right (front = +Z) into one labelled-by-order sheet.</summary>
        public static string RenderContactSheet(string outPng, int tile = 200, int perRow = 10)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir });
            var names = new List<string>();
            foreach (var g in guids) names.Add(AssetDatabase.GUIDToAssetPath(g));
            names.Sort();
            int rows = (names.Count + perRow - 1) / perRow;
            var sheet = new Texture2D(perRow * tile, rows * tile, TextureFormat.RGB24, false);
            var pru = new PreviewRenderUtility();
            pru.camera.fieldOfView = 30f;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = new Color(0.42f, 0.44f, 0.48f);
            pru.lights[0].intensity = 1.3f;
            pru.lights[0].transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            pru.lights[1].intensity = 0.8f;
            pru.ambientColor = new Color(0.45f, 0.45f, 0.5f);
            var legend = new System.Text.StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(names[i]);
                var go = pru.InstantiatePrefabInScene(prefab);
                var b = RendererBounds(go);
                float rad = Mathf.Max(0.05f, b.extents.magnitude);
                float dist = rad / Mathf.Sin(pru.camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                var dir = new Vector3(0.55f, 0.45f, 1f).normalized;    // in front (+Z), right and above
                pru.camera.transform.position = b.center + dir * dist;
                pru.camera.transform.LookAt(b.center);
                pru.camera.nearClipPlane = dist * 0.05f;
                pru.camera.farClipPlane = dist * 4f;
                pru.BeginStaticPreview(new Rect(0, 0, tile, tile));
                pru.Render(true);
                var tex = pru.EndStaticPreview();
                var rt = RenderTexture.GetTemporary(tile, tile, 0);
                Graphics.Blit(tex, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tmp = new Texture2D(tile, tile, TextureFormat.RGB24, false);
                tmp.ReadPixels(new Rect(0, 0, tile, tile), 0, 0); tmp.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                int col = i % perRow, row = rows - 1 - i / perRow;
                sheet.SetPixels(col * tile, row * tile, tile, tile, tmp.GetPixels());
                Object.DestroyImmediate(tmp);
                Object.DestroyImmediate(go);
                legend.AppendLine(i + ": " + Path.GetFileNameWithoutExtension(names[i]));
            }
            pru.Cleanup();
            sheet.Apply();
            File.WriteAllBytes(outPng, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return legend.ToString();
        }
    }
}
