using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The glTF props reference their textures as ordinary image assets, and Unity imported every one of them as an
    /// sRGB colour texture compressed to DXT1. For normal maps that means gamma-skewed normals squeezed into 4x4
    /// colour blocks, and for the metal/roughness/AO maps gamma-skewed (too glossy) values: together they make the
    /// props' shading look blocky and pixelated, most visibly on rows of chairs. This reads each glTF to learn what
    /// every image is used for and sets its importer accordingly (linear for normal and data maps, sRGB for colour),
    /// with high-quality compression and anisotropic filtering.
    /// </summary>
    public static class PropTextureFix
    {
        enum Use { Color, Data, Normal }

        [MenuItem("Hollow Tower/College Dressing/0. Fix Prop Texture Imports")]
        public static void FixMenu() { Debug.Log("[PropTextureFix] " + Fix()); }

        public static string Fix()
        {
            var uses = new Dictionary<string, Use>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { PropLibrary.SourceDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".gltf")) continue;
                Collect(path, uses);
            }
            int changed = 0, normals = 0, data = 0, colors = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in uses)
                {
                    var imp = AssetImporter.GetAtPath(kv.Key) as TextureImporter;
                    if (imp == null) continue;
                    if (kv.Value == Use.Normal) normals++; else if (kv.Value == Use.Data) data++; else colors++;
                    if (Apply(imp, kv.Value)) { imp.SaveAndReimport(); changed++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return uses.Count + " textures (" + normals + " normal, " + data + " linear data, " + colors + " colour), " + changed + " reimported";
        }

        static bool Apply(TextureImporter imp, Use use)
        {
            var before = EditorJsonUtility.ToJson(imp);
            // Normal maps stay Default (linear), not NormalMap: glTFast's URP shader graph decodes the raw RGB itself, and
            // the NormalMap type's channel packing makes it read garbage (desk tops render black, metal legs chalk white).
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = use == Use.Color;
            imp.mipmapEnabled = true;
            imp.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            imp.anisoLevel = 8;
            imp.filterMode = FilterMode.Trilinear;
            // BC7: DXT1's 4x4 blocks are what read as pixelation up close
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            return EditorJsonUtility.ToJson(imp) != before;
        }

        // Pulls image uris and the texture slots that use them out of the glTF json (no json library needed:
        // the files come from Poly Haven / the web pack and all share the same simple layout).
        static void Collect(string gltfPath, Dictionary<string, Use> uses)
        {
            string json = File.ReadAllText(gltfPath);
            string dir = Path.GetDirectoryName(gltfPath).Replace('\\', '/');
            var images = new List<string>();
            var imgBlock = Block(json, "\"images\"");
            if (imgBlock != null)
                foreach (Match m in Regex.Matches(imgBlock, "\"uri\"\\s*:\\s*\"([^\"]+)\""))
                    images.Add(dir + "/" + System.Uri.UnescapeDataString(m.Groups[1].Value));
            var texSource = new List<int>();
            var texBlock = Block(json, "\"textures\"");
            if (texBlock != null)
                foreach (Match m in Regex.Matches(texBlock, "\\{[^{}]*\\}"))
                {
                    var s = Regex.Match(m.Value, "\"source\"\\s*:\\s*(\\d+)");
                    texSource.Add(s.Success ? int.Parse(s.Groups[1].Value) : -1);
                }
            var matBlock = Block(json, "\"materials\"");
            if (matBlock == null) return;
            Mark(matBlock, "normalTexture", Use.Normal, images, texSource, uses);
            Mark(matBlock, "metallicRoughnessTexture", Use.Data, images, texSource, uses);
            Mark(matBlock, "occlusionTexture", Use.Data, images, texSource, uses);
            Mark(matBlock, "baseColorTexture", Use.Color, images, texSource, uses);
            Mark(matBlock, "emissiveTexture", Use.Color, images, texSource, uses);
        }

        static void Mark(string matBlock, string slot, Use use, List<string> images, List<int> texSource, Dictionary<string, Use> uses)
        {
            foreach (Match m in Regex.Matches(matBlock, "\"" + slot + "\"\\s*:\\s*\\{[^}]*\"index\"\\s*:\\s*(\\d+)"))
            {
                int t = int.Parse(m.Groups[1].Value);
                if (t >= texSource.Count) continue;
                int img = texSource[t];
                if (img < 0 || img >= images.Count) continue;
                Use prev;
                // a texture shared between slots stays colour only if every use is colour
                if (uses.TryGetValue(images[img], out prev) && prev != Use.Color && use == Use.Color) continue;
                uses[images[img]] = use;
            }
        }

        /// <summary>The bracketed array that follows a top-level key, e.g. "images": [ ... ].</summary>
        static string Block(string json, string key)
        {
            int k = json.IndexOf(key);
            if (k < 0) return null;
            int start = json.IndexOf('[', k);
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']' && --depth == 0) return json.Substring(start, i - start + 1);
            }
            return null;
        }
    }
}
