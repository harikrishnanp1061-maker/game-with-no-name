using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Binds the staged BackgroundProps PBR textures onto persistent Materials and assigns
/// them to FBX model importers and active scene renderers.
///
/// Why this exists: Unity's FBX importer generates read-only internal sub-assets that
/// revert to flat grey upon reload. This binder creates persistent disk Materials in
/// Assets/ThirdParty/PolyhavenProps/Materials/, maps all PBR textures onto them, and
/// links them to scene renderers and FBX importers permanently.
/// </summary>
public static class PropTextureBinder
{
    const string ManifestPath = "Assets/Editor/PropTextureManifest.json";
    const string TextureRoot = "Assets/ThirdParty/PolyhavenProps/Textures/";
    const string PropRoot = "Assets/ThirdParty/PolyhavenProps/";
    const string MatFolder = "Assets/ThirdParty/PolyhavenProps/Materials";

    [MenuItem("Tools/Props/Bind PBR Textures")]
    public static void BindAll()
    {
        if (!File.Exists(ManifestPath))
        {
            Debug.LogError("[PropBinder] manifest not found at " + ManifestPath
                           + " - run stage_prop_textures.py first.");
            return;
        }

        var json = File.ReadAllText(ManifestPath);
        var manifest = JsonUtility.FromJson<Manifest>(json);
        if (manifest == null || manifest.items == null || manifest.items.Length == 0)
        {
            Debug.LogError("[PropBinder] manifest parsed to zero entries.");
            return;
        }

        // prefix -> kind -> project-relative texture path
        var grouped = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in manifest.items)
        {
            if (it == null || string.IsNullOrEmpty(it.prefix) || string.IsNullOrEmpty(it.kind))
                continue;
            if (!grouped.TryGetValue(it.prefix, out var maps))
                grouped[it.prefix] = maps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            maps[it.kind] = it.path;
        }

        // Aliases for FBX models whose material names differ from texture subfolder prefix
        if (grouped.TryGetValue("Barrel_01_explosive", out var barrelMaps))
        {
            grouped["Barrel_01"] = barrelMaps;
        }
        if (grouped.TryGetValue("wheelchair_01", out var wheelchairMaps))
        {
            grouped["lambert1"] = wheelchairMaps;
        }

        // 1. Ensure persistent .mat assets on disk
        var persistentMap = EnsurePersistentMaterials(grouped);

        // 2. Remap active scene renderers
        int sceneUpdated = RemapSceneRenderers(persistentMap);

        // 3. Remap FBX importers so newly spawned props also use persistent materials
        RemapFbxImporters(persistentMap);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(string.Format(
            "[PropBinder] Completed: {0} persistent materials created/updated. {1} scene renderers bound.",
            persistentMap.Count, sceneUpdated));
    }

    /// <summary>
    /// Creates or updates persistent .mat files in Assets/ThirdParty/PolyhavenProps/Materials/
    /// </summary>
    static Dictionary<string, Material> EnsurePersistentMaterials(Dictionary<string, Dictionary<string, string>> grouped)
    {
        if (!Directory.Exists(MatFolder))
        {
            Directory.CreateDirectory(MatFolder);
            AssetDatabase.Refresh();
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var map = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in grouped)
        {
            var prefix = kvp.Key;
            var paths = kvp.Value;
            var matPath = $"{MatFolder}/{prefix}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool created = false;
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = prefix;
                AssetDatabase.CreateAsset(mat, matPath);
                created = true;
            }

            var maps = LoadMaps(paths);
            bool dirty = created;

            if (maps.diff != null && mat.GetTexture("_BaseMap") != maps.diff)
            {
                mat.SetTexture("_BaseMap", maps.diff);
                mat.SetColor("_BaseColor", Color.white);
                dirty = true;
            }

            if (maps.normal != null && mat.GetTexture("_BumpMap") != maps.normal)
            {
                mat.SetTexture("_BumpMap", maps.normal);
                dirty = true;
            }

            if (maps.arm != null)
            {
                if (mat.GetTexture("_MetallicGlossMap") != maps.arm)
                {
                    mat.SetTexture("_MetallicGlossMap", maps.arm);
                    mat.SetTexture("_OcclusionMap", maps.arm);
                    dirty = true;
                }
            }
            else if (maps.rough != null)
            {
                if (mat.GetTexture("_MetallicGlossMap") != maps.rough)
                {
                    mat.SetTexture("_MetallicGlossMap", maps.rough);
                    dirty = true;
                }
            }

            if (maps.emissive != null && mat.GetTexture("_EmissionMap") != maps.emissive)
            {
                mat.SetTexture("_EmissionMap", maps.emissive);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                dirty = true;
            }

            if (prefix.ToLower().Contains("glass"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", new Color(0.7f, 0.85f, 0.95f, 0.25f));
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetFloat("_Metallic", 0.05f);
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(mat);
            }

            map[prefix] = mat;
        }

        return map;
    }

    /// <summary>
    /// Remaps every Renderer in the scene so its materials point to persistent .mat assets.
    /// </summary>
    static int RemapSceneRenderers(Dictionary<string, Material> persistentMap)
    {
        int updated = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                bool dirty = false;

                for (int i = 0; i < shared.Length; i++)
                {
                    var cur = shared[i];
                    string matName = cur != null ? CleanName(cur.name) : "";
                    string objName = CleanName(r.gameObject.name);
                    string parentName = r.transform.parent != null ? CleanName(r.transform.parent.name) : "";

                    Material target = null;
                    if (!string.IsNullOrEmpty(matName) && persistentMap.TryGetValue(matName, out target))
                    {
                        // Direct match
                    }
                    else if (persistentMap.TryGetValue(objName, out target))
                    {
                        // Object name match
                    }
                    else if (!string.IsNullOrEmpty(parentName) && persistentMap.TryGetValue(parentName, out target))
                    {
                        // Parent name match
                    }
                    else
                    {
                        // Substring prefix match
                        foreach (var kvp in persistentMap)
                        {
                            if ((!string.IsNullOrEmpty(matName) && matName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                objName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                target = kvp.Value;
                                break;
                            }
                        }
                    }

                    if (target != null && shared[i] != target)
                    {
                        shared[i] = target;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    r.sharedMaterials = shared;
                    EditorUtility.SetDirty(r);
                    updated++;
                }
            }
        }
        return updated;
    }

    /// <summary>
    /// Remaps sub-assets in FBX files to persistent materials.
    /// </summary>
    static void RemapFbxImporters(Dictionary<string, Material> persistentMap)
    {
        var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { PropRoot });
        foreach (var guid in fbxGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            bool changed = false;
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var sub in subAssets)
            {
                if (sub is Material subMat)
                {
                    string clean = CleanName(subMat.name);
                    if (persistentMap.TryGetValue(clean, out var target))
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(subMat), target);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    /// <summary>Strips the " (Instance)" suffix Unity appends to clones.</summary>
    static string CleanName(string n)
    {
        if (n == null) return "";
        var p = n.IndexOf(" (Instance");
        return p > 0 ? n.Substring(0, p) : n;
    }

    struct Maps
    {
        public Texture2D diff, normal, arm, rough, emissive;
    }

    static Maps LoadMaps(Dictionary<string, string> entry)
    {
        var m = new Maps();
        m.diff = Load(entry, "diff");
        m.normal = Load(entry, "nor_gl");
        m.arm = Load(entry, "arm");
        m.rough = Load(entry, "rough");
        m.emissive = Load(entry, "emissive");
        return m;
    }

    static Texture2D Load(Dictionary<string, string> entry, string key)
    {
        if (!entry.TryGetValue(key, out var path)) return null;
        if (string.IsNullOrEmpty(path)) return null;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
            Debug.LogWarning("[PropBinder] texture missing at " + path);
        return tex;
    }

    [Serializable]
    class ManifestItem
    {
        public string prefix;
        public string kind;
        public string path;
    }

    [Serializable]
    class Manifest
    {
        public ManifestItem[] items;
    }
}