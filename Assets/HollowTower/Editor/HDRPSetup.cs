using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// One-click HDRP configuration (what the HDRP Wizard's "Fix All" + "HDRP + DXR" tabs do),
    /// tuned for Hollow Tower: ray tracing, DLSS, volumetrics, decals, SSR/SSAO/SSGI fallbacks.
    /// The look itself (exposure, fog, ray-traced effects) is a Volume built at runtime in HDRPLook.cs.
    /// </summary>
    public static class HDRPSetup
    {
        const string AssetPath = "Assets/HollowTower/Settings/HollowTowerHDRP.asset";

        [MenuItem("Hollow Tower/Graphics/Configure HDRP + Ray Tracing", priority = 21)]
        public static void Configure()
        {
            // --- project basics ---
            PlayerSettings.colorSpace = ColorSpace.Linear;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowmaskMode = ShadowmaskMode.DistanceShadowmask;
                QualitySettings.renderPipeline = null; // use the default (graphics settings) asset everywhere
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(current, false);

            // --- HDRP asset ---
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetPath);
            if (asset == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                asset.name = "HollowTowerHDRP";
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var srp = typeof(HDRenderPipelineAsset).GetProperty("enableSRPBatcher", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (srp != null && srp.CanWrite) srp.SetValue(asset, true);

            var so = new SerializedObject(asset);
            const string s = "m_RenderPipelineSettings.";
            SetBool(so, s + "supportRayTracing", true);
            SetInt(so, s + "supportedRayTracingMode", 1); // performance mode only - it's a laptop GPU
            SetBool(so, s + "supportSSR", true);
            SetBool(so, s + "supportSSAO", true);
            SetBool(so, s + "supportSSGI", true);
            SetBool(so, s + "supportVolumetrics", true);
            SetBool(so, s + "supportDecals", true);
            SetBool(so, s + "supportMotionVectors", true);
            SetBool(so, s + "supportShadowMask", false);
            SetBool(so, s + "supportSubsurfaceScattering", false);
            SetBool(so, s + "supportWater", false);
            SetBool(so, s + "supportVolumetricClouds", false);
            SetBool(so, s + "supportCustomPass", true);
            SetBool(so, s + "hdShadowInitParams.supportScreenSpaceShadows", true);
            SetInt(so, s + "hdShadowInitParams.maxScreenSpaceShadowSlots", 4);
            SetInt(so, s + "hdShadowInitParams.maxShadowRequests", 96);
            SetInt(so, s + "hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution", 4096);
            // Upscaling: DLSS on NVIDIA, otherwise HDRP's own temporal upscaler.
            SetBool(so, s + "dynamicResolutionSettings.enabled", true);
            SetBool(so, s + "dynamicResolutionSettings.enableDLSS", true);
            SetInt(so, s + "dynamicResolutionSettings.dynResType", 1);            // hardware
            SetInt(so, s + "dynamicResolutionSettings.DLSSPerfQualitySetting", 1); // balanced
            SetBool(so, s + "dynamicResolutionSettings.DLSSUseOptimalSettings", true);
            SetFloat(so, s + "dynamicResolutionSettings.minPercentage", 58f);
            SetFloat(so, s + "dynamicResolutionSettings.maxPercentage", 100f);
            SetBool(so, s + "dynamicResolutionSettings.forceResolution", false);
            // GPU Resident Drawer: thousands of generated props are drawn through BatchRendererGroup with GPU occlusion culling.
            SetInt(so, s + "gpuResidentDrawerSettings.mode", 1);
            // GPU occlusion culling stays off: it made walls and props pop in and out (flicker) as you turned.
            SetBool(so, s + "gpuResidentDrawerSettings.enableOcclusionCullingInCameras", false);
            SetBool(so, s + "gpuResidentDrawerSettings.useDepthPrepassForOccluders", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            // BatchRendererGroup shader variants must be kept for the resident drawer.
            var gfx = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gfx != null && gfx.Length > 0)
            {
                var gso = new SerializedObject(gfx[0]);
                var brg = gso.FindProperty("m_BrgStripping");
                // Find the stored value Unity reads back as "Keep All".
                for (int v = 0; brg != null && v < 4; v++)
                {
                    brg.intValue = v;
                    gso.ApplyModifiedPropertiesWithoutUndo();
                    if (UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode.ToString() == "KeepAll") break;
                }
                Debug.Log("Hollow Tower: BatchRendererGroup variants = " + UnityEditor.Rendering.EditorGraphicsSettings.batchRendererGroupShaderStrippingMode);
            }

            GraphicsSettings.defaultRenderPipeline = asset;

            // --- global settings + default volume profile (internal HDRP helpers) ---
            var gs = typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings");
            if (gs != null) CallInternal(gs, "Ensure", new object[] { true });
            var volumeUtils = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.Rendering.HighDefinition.VolumeUtils", false))
                .FirstOrDefault(t => t != null);
            if (volumeUtils != null) CallInternal(volumeUtils, "EnsureDefaultVolumeProfile", null);

            // --- DirectX 12 for DXR ---
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D12, UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            AssetDatabase.SaveAssets();

            // Re-import the glTF / FBX models so their materials are generated for HDRP.
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/HollowTower/Resources/Models" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".glb") || p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("Hollow Tower: HDRP configured (ray tracing + DLSS). Restart the editor so it runs on DirectX 12.");
        }

        static void CallInternal(Type t, string method, object[] args)
        {
            var m = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                     .FirstOrDefault(x => x.Name == method && x.GetParameters().Length == (args?.Length ?? 0));
            if (m == null) { Debug.LogWarning("Hollow Tower: HDRP helper not found: " + t.Name + "." + method); return; }
            m.Invoke(null, args);
        }

        static SerializedProperty Find(SerializedObject so, string path)
        {
            var p = so.FindProperty(path);
            if (p == null) Debug.LogWarning("Hollow Tower: HDRP setting not found: " + path);
            return p;
        }
        static void SetBool(SerializedObject so, string path, bool v) { var p = Find(so, path); if (p != null) p.boolValue = v; }
        static void SetInt(SerializedObject so, string path, int v)
        {
            var p = Find(so, path);
            if (p == null) return;
            if (p.propertyType == SerializedPropertyType.Enum) p.enumValueFlag = v; else p.intValue = v;
        }
        static void SetFloat(SerializedObject so, string path, float v) { var p = Find(so, path); if (p != null) p.floatValue = v; }
    }

    /// <summary>Keeps imported art light: every texture under a Models folder is capped at 2048.</summary>
    public class ModelTextureCap : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            // The annex / pistol / wing textures (separate files next to their .gltf): compressed, mipmapped, sharp at angles.
            // The single-atlas scans (chapel, smoking room) get 4096 and high-quality BC7; the rest 2048.
            bool annex = assetPath.Contains("/Models/Annex/") || assetPath.Contains("/Models/Pistol/") || assetPath.Contains("/Textures/Wing/");
            if (annex)
            {
                string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                bool normal = file.EndsWith("_Normal");
                bool atlas = assetPath.Contains("/Chapel_tex/") || assetPath.Contains("/SmokingRoom_tex/");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !file.EndsWith("_MR");
                ti.mipmapEnabled = true;
                ti.anisoLevel = 8;
                ti.filterMode = FilterMode.Trilinear;
                ti.wrapMode = assetPath.Contains("/Textures/Wing/") ? TextureWrapMode.Repeat : ti.wrapMode;
                ti.maxTextureSize = atlas ? 4096 : 2048;
                ti.textureCompression = atlas || normal ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                ti.streamingMipmaps = false;
                return;
            }
            if (!assetPath.Contains("/Models/")) return;
            if (ti.maxTextureSize > 2048) ti.maxTextureSize = 2048;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.addCollider = false;     // colliders are added at runtime where needed
            mi.isReadable = true;       // runtime MeshColliders
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
        }
    }
}
