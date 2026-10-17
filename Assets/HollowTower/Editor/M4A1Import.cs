using System.IO;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Import settings for the player's M4A1 (Resources/Models/M4A1, model by Umang Rank on Sketchfab).
    /// The source textures are 4K; the game only ever sees the gun up close at screen size, so they
    /// import at 2K. HDRP/Lit wants metallic + smoothness packed in one mask map, which "Build Mask"
    /// bakes from the separate metallic and roughness maps kept in M4A1Source/.
    /// </summary>
    [InitializeOnLoad]
    public class M4A1Import : AssetPostprocessor
    {
        const string Folder = "Assets/HollowTower/Resources/Models/M4A1/";
        const string MaskPath = Folder + "M4A1_Mask.png";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.EndsWith("_Normal.png")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_Mask.png")) ti.sRGBTexture = false;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
        }

        [MenuItem("Hollow Tower/M4A1/Build Mask Map", priority = 80)]
        public static void BuildMask()
        {
            const int size = 2048;
            var metal = Load("M4A1Source/LP_defaultPolygonShader_Metallic.png");
            var rough = Load("M4A1Source/LP_defaultPolygonShader_Roughness.png");
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float m = metal.GetPixelBilinear(u, v).r;
                    float r = rough.GetPixelBilinear(u, v).r;
                    // R metallic, G occlusion, B detail mask, A smoothness.
                    px[y * size + x] = new Color32((byte)(m * 255f), 255, 0, (byte)((1f - r) * 255f));
                }
            }
            mask.SetPixels32(px);
            File.WriteAllBytes(MaskPath, mask.EncodeToPNG());
            AssetDatabase.ImportAsset(MaskPath);
            Debug.Log("Hollow Tower: M4A1 mask map -> " + MaskPath);
        }

        static Texture2D Load(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.LoadImage(File.ReadAllBytes(path));
            return t;
        }

        const string ShotsKey = "HT.GunShots";

        static M4A1Import()
        {
            if (SessionState.GetBool(ShotsKey, false)) EditorApplication.update += WatchShots;
        }

        /// <summary>Batch entry point (pass -gunshots too): plays the game, saves Shots/gun_*.png, then quits.</summary>
        public static void GunShots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/HollowTower/Scenes/HollowTower.unity");
            SessionState.SetBool(ShotsKey, true);
            EditorApplication.update += WatchShots;
            EditorApplication.isPlaying = true;
        }

        static void WatchShots()
        {
            bool timeout = EditorApplication.timeSinceStartup > 600;
            if (!timeout && !Benchmark.Done) return;
            SessionState.SetBool(ShotsKey, false);
            if (!Benchmark.Done && !timeout) return;
            EditorApplication.update -= WatchShots;
            EditorApplication.Exit(Benchmark.Done ? 0 : 1);
        }

        /// <summary>Batch entry point: bake the mask, then log the model's layout (used to fit it in the hands).</summary>
        public static void SetupAndInspect()
        {
            AssetDatabase.Refresh();
            BuildMask();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "M4A1.fbx");
            var go = Object.Instantiate(prefab);
            foreach (var t in go.GetComponentsInChildren<Transform>())
            {
                var mf = t.GetComponent<MeshFilter>();
                var r = t.GetComponent<Renderer>();
                Debug.Log("M4A1 node " + t.name + " lpos=" + t.localPosition.ToString("F4") + " lrot=" + t.localEulerAngles.ToString("F1") +
                          " lscale=" + t.localScale.ToString("F4") +
                          (mf != null ? " mesh=" + mf.sharedMesh.name + " verts=" + mf.sharedMesh.vertexCount + " subs=" + mf.sharedMesh.subMeshCount + " mb=" + mf.sharedMesh.bounds : "") +
                          (r != null ? " wb=" + r.bounds.center.ToString("F4") + " size=" + r.bounds.size.ToString("F4") : ""));
                if (mf == null) continue;
                // Slice the mesh along its long axis (in world space) to see where the barrel, mag and stock are.
                var verts = mf.sharedMesh.vertices;
                var wb = r.bounds;
                int ax = wb.size.x >= wb.size.y && wb.size.x >= wb.size.z ? 0 : (wb.size.y >= wb.size.z ? 1 : 2);
                const int n = 20;
                var mn = new Vector3[n]; var mx = new Vector3[n];
                for (int i = 0; i < n; i++) { mn[i] = Vector3.one * 1e9f; mx[i] = Vector3.one * -1e9f; }
                foreach (var v in verts)
                {
                    var w = t.TransformPoint(v);
                    int s = Mathf.Clamp((int)((w[ax] - wb.min[ax]) / wb.size[ax] * n), 0, n - 1);
                    mn[s] = Vector3.Min(mn[s], w); mx[s] = Vector3.Max(mx[s], w);
                }
                for (int i = 0; i < n; i++) Debug.Log("M4A1 slice ax" + ax + " " + i + " min=" + mn[i].ToString("F3") + " max=" + mx[i].ToString("F3"));
            }
            EditorApplication.Exit(0);
        }
    }
}
