using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Binds the staged BackgroundProps PBR textures onto the materials that Unity's
/// FBX importer generated for those same models.
///
/// Why this exists: the FBX files shipped without their image files, so the
/// importer created 2000+ materials that all fell back to flat grey. The
/// textures live in Assets/ThirdParty/PolyhavenProps/Textures/&lt;prefix&gt;/ and
/// are described by Assets/Editor/PropTextureManifest.json, which is keyed by
/// the same sub-asset prefix the importer used for material names
/// (standing_chalkboard_01_board, vintage_cabinet_01_a, ...).
///
/// Map routing:
///   diff      -> _BaseMap
///   nor_gl    -> _BumpMap   (authored OpenGL +Y, flipped to DirectX on import)
///   arm       -> packed AO/Roughness/Metallic, split across
///                _MetallicGlossMap / _OcclusionMap / roughness
///   rough     -> roughness channel only
///   emissive  -> _EmissionMap
///
/// Idempotent: re-running only touches materials that are actually missing a map.
/// </summary>
public static class PropTextureBinder
{
    const string ManifestPath = "Assets/Editor/PropTextureManifest.json";
    const string TextureRoot = "Assets/ThirdParty/PolyhavenProps/Textures/";
    const string PropRoot = "Assets/ThirdParty/PolyhavenProps/";

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

        BindMaterials(CollectAssetMaterials(grouped.Keys), grouped, out var aTouched, out var aUpdated);
        BindMaterials(CollectSceneMaterials(grouped.Keys), grouped, out var sTouched, out var sUpdated);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[PropBinder] manifest prefixes={0}\n"
            + "  asset materials : touched={1} updated={2}\n"
            + "  scene materials : touched={3} updated={4}",
            grouped.Count, aTouched, aUpdated, sTouched, sUpdated));
    }

    /// <summary>Every Material sub-asset the FBX importer produced.</summary>
    static List<Material> CollectAssetMaterials(IEnumerable<string> prefixes)
    {
        var wanted = new HashSet<string>(prefixes, StringComparer.OrdinalIgnoreCase);
        var list = new List<Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PropRoot }))
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            {
                if (o is Material mat && wanted.Contains(CleanName(mat.name)))
                    list.Add(mat);
            }
        }
        return list;
    }

    /// <summary>
    /// Materials actually referenced by renderers in the open scene.
    ///
    /// These are frequently non-persistent clones ("name (Instance)") that were
    /// baked into the scene, so editing the FBX sub-assets alone leaves the
    /// level still rendering flat grey. They have to be written and then
    /// serialized into the scene file.
    /// </summary>
    static List<Material> CollectSceneMaterials(IEnumerable<string> prefixes)
    {
        var wanted = new HashSet<string>(prefixes, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<Material>();
        var list = new List<Material>();

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    // Identity check de-dupes the many renderers that share a clone.
                    if (m == null || !seen.Add(m)) continue;
                    if (wanted.Contains(CleanName(m.name)))
                        list.Add(m);
                }
            }
        }
        return list;
    }

    /// <summary>Strips the " (Instance)" suffix Unity appends to clones.</summary>
    static string CleanName(string n)
    {
        if (n == null) return "";
        var p = n.IndexOf(" (Instance");
        return p > 0 ? n.Substring(0, p) : n;
    }

    /// <summary>Applies the maps to each material. Returns touched, updated.</summary>
    static int BindMaterials(List<Material> mats,
                            Dictionary<string, Dictionary<string, string>> grouped,
                            out int touched, out int updated)
        {
            touched = 0; updated = 0;

        foreach (var mat in mats)
        {
            var prefix = CleanName(mat.name);
            if (!grouped.TryGetValue(prefix, out var paths)) continue;

            touched++;
            var maps = LoadMaps(paths, out var hasNormal);
            bool changed = false;

            if (maps.diff != null && mat.GetTexture("_BaseMap") == null)
            {
                mat.SetTexture("_BaseMap", maps.diff);
                changed = true;
            }

            if (maps.normal != null && mat.GetTexture("_BumpMap") == null)
            {
                mat.SetTexture("_BumpMap", maps.normal);
                changed = true;
            }

            // ARM packs AO (R) / Roughness (G) / Metallic (B). URP Lit expects a
            // single metallicGlossMap with R=metallic, A=smoothness, so the ARM
            // image feeds the standard slot directly and occlusion uses the same
            // texture's red channel.
            if (maps.arm != null && mat.GetTexture("_MetallicGlossMap") == null)
            {
                mat.SetTexture("_MetallicGlossMap", maps.arm);
                mat.SetTexture("_OcclusionMap", maps.arm);
                changed = true;
            }
            else if (maps.rough != null && mat.GetTexture("_MetallicGlossMap") == null)
            {
                mat.SetTexture("_MetallicGlossMap", maps.rough);
                changed = true;
            }

            if (maps.emissive != null && mat.GetTexture("_EmissionMap") == null)
            {
                mat.SetTexture("_EmissionMap", maps.emissive);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(mat);
                // A non-persistent clone only survives if the scene records it.
                if (!EditorUtility.IsPersistent(mat))
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                updated++;
            }
        }
        return updated;
    }

    struct Maps
    {
        public Texture2D diff, normal, arm, rough, emissive;
    }

    static Maps LoadMaps(Dictionary<string, string> entry, out bool hasNormal)
    {
        var m = new Maps();
        m.diff = Load(entry, "diff");
        m.normal = Load(entry, "nor_gl");
        m.arm = Load(entry, "arm");
        m.rough = Load(entry, "rough");
        m.emissive = Load(entry, "emissive");
        hasNormal = m.normal != null;
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

    /// <summary>The manifest is written by stage_prop_textures.py as a flat list of
    /// {prefix, kind, path} records, which JsonUtility deserialises directly -
    /// no nested-dictionary or hand-rolled JSON parsing required.</summary>
    [Serializable] class ManifestItem
    {
        public string prefix;
        public string kind;
        public string path;
    }

    [Serializable] class Manifest
    {
        public ManifestItem[] items;
    }

    [MenuItem("Tools/Props/Report Untextured Materials")]
    public static void ReportUntextured()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = 0, untextured = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { PropRoot }))
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            {
                if (!(o is Material mat)) continue;
                total++;
                if (mat.GetTexture("_BaseMap") == null)
                {
                    untextured++;
                    names.Add(CleanName(mat.name));
                }
            }
        }

        Debug.Log(string.Format(
            "[PropBinder] {0}/{1} prop materials have no albedo.\nMost are glass/bulb sub-parts "
            + "that ship no texture; any others are real gaps:\n{2}",
            untextured, total, string.Join(", ", names)));
    }
}