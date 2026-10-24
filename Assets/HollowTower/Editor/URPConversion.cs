using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// HDRP -> URP conversion for the remastered map: HDRP/Lit materials, scene lights and cameras.
    /// HDRP types are looked up by name and material values are read from the serialized data,
    /// so this keeps compiling (and the material pass keeps working) once the HDRP package is gone.
    /// </summary>
    public static class URPConversion
    {
        // HDRP emission is in nits and goes through camera exposure, weighted by _EmissiveExposureWeight
        // (0 = ignores exposure). URP has no exposure, so the converted value is what HDRP fed the tonemapper
        // at ReferenceEV, which is where HorrorVolumeProfile's auto exposure settles in a lit corridor.
        public const float ReferenceEV = 5f;
        // Keeps lamps and ceiling panels bright enough to bloom without blowing out the frame.
        public const float MaxEmission = 6f;
        // Emission maps cover whole ceiling panels, so they get a lower limit than lamp bulbs.
        public const float MaxMappedEmission = 2.5f;

        // URP intensity per candela: the 312 lm / 140 degree ceiling spots land at about 3.4,
        // the same range as collage game's TutorialScene lights.
        public const float CandelaToURP = 0.045f;
        public const float MaxSceneLight = 5f;
        // Matches collage game's Torch_SpotLight.
        public const float FlashlightIntensity = 20f;

        const string HDLightType = "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData";
        const string HDCameraType = "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData";

        [MenuItem("Hollow Tower/URP/Convert HDRP Materials", priority = 40)]
        static void ConvertMaterialsMenu() { Debug.Log(ConvertMaterials()); }

        [MenuItem("Hollow Tower/URP/Convert Scene Lights and Cameras", priority = 41)]
        static void ConvertSceneMenu() { Debug.Log(ConvertLights() + "\n" + ConvertCameras()); }

        // ---------------------------------------------------------------- materials

        public static string ConvertMaterials(string folder = "Assets")
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return "URP Lit shader not found.";
            var log = new StringBuilder();
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue; // model sub-assets are rebuilt by their importers
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;
                var saved = new SavedProperties(m);
                if (!IsHDRPLit(m, saved)) continue;
                ConvertMaterial(m, lit, saved);
                log.AppendLine("  " + path + Describe(m));
                count++;
            }
            AssetDatabase.SaveAssets();
            return "Converted " + count + " HDRP materials to URP Lit:\n" + log;
        }

        static bool IsHDRPLit(Material m, SavedProperties s)
        {
            if (m.shader != null && m.shader.name.StartsWith("HDRP/")) return true;
            // Shader already gone (HDRP removed): recognise HDRP/Lit by its saved properties.
            bool broken = m.shader == null || m.shader.name == "Hidden/InternalErrorShader";
            return broken && s.HasTexture("_BaseColorMap") && s.HasFloat("_SurfaceType");
        }

        static void ConvertMaterial(Material m, Shader lit, SavedProperties s)
        {
            m.shader = lit;
            m.shaderKeywords = Array.Empty<string>();

            s.GetTexture("_BaseColorMap", out var baseMap, out var scale, out var offset);
            var baseColor = s.GetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetTexture("_MainTex", baseMap);
            m.SetTextureScale("_MainTex", scale);
            m.SetTextureOffset("_MainTex", offset);
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_Color", baseColor);

            s.GetTexture("_NormalMap", out var normal, out _, out _);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", s.GetFloat("_NormalScale", 1f));

            // HDRP mask map: R metallic, G occlusion, A smoothness. URP reads metallic/smoothness from
            // _MetallicGlossMap (R/A) and occlusion from _OcclusionMap (G), so the same texture feeds both.
            s.GetTexture("_MaskMap", out var mask, out _, out _);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Metallic", s.GetFloat("_Metallic", 0f));
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetTexture("_OcclusionMap", mask);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetFloat("_Smoothness", mask != null ? s.GetFloat("_SmoothnessRemapMax", 1f) : s.GetFloat("_Smoothness", 0.5f));
            m.SetFloat("_OcclusionStrength", mask != null ? Mathf.Clamp01(s.GetFloat("_AORemapMax", 1f) - s.GetFloat("_AORemapMin", 0f)) : 1f);

            s.GetTexture("_EmissiveColorMap", out var emissionMap, out _, out _);
            var emission = s.GetColor("_EmissiveColor", Color.black);
            float nits = emission.maxColorComponent;
            if (nits > 0f)
            {
                float weight = s.GetFloat("_EmissiveExposureWeight", 1f);
                float exposure = 1f / (1.2f * Mathf.Pow(2f, ReferenceEV));
                float limit = emissionMap != null ? MaxMappedEmission : MaxEmission;
                float target = Mathf.Min(nits * (weight * exposure + (1f - weight)), limit);
                emission *= target / nits;
            }
            emission.a = 1f;
            m.SetTexture("_EmissionMap", emissionMap);
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = nits > 0f ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            // Surface options. HDRP blend modes: 0 alpha, 1 additive, 4 premultiply; URP: 0 alpha, 1 premultiply, 2 additive.
            bool transparent = s.GetFloat("_SurfaceType", 0f) >= 0.5f;
            int hdBlend = Mathf.RoundToInt(s.GetFloat("_BlendMode", 0f));
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            m.SetFloat("_Blend", hdBlend == 1 ? 2f : hdBlend == 4 ? 1f : 0f);
            m.SetFloat("_AlphaClip", s.GetFloat("_AlphaCutoffEnable", 0f) >= 0.5f ? 1f : 0f);
            m.SetFloat("_Cutoff", s.GetFloat("_AlphaCutoff", 0.5f));
            m.SetFloat("_Cull", s.GetFloat("_DoubleSidedEnable", 0f) >= 0.5f ? 0f : 2f);
            m.SetFloat("_ReceiveShadows", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.renderQueue = -1;

            BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(m);
        }

        static string Describe(Material m)
        {
            var e = m.GetColor("_EmissionColor");
            return (e.maxColorComponent > 0f ? "  emission " + e.maxColorComponent.ToString("0.##") : "")
                 + "  [" + string.Join(" ", m.shaderKeywords) + "]";
        }

        /// <summary>Reads m_SavedProperties directly, so values survive a missing or swapped shader.</summary>
        class SavedProperties
        {
            readonly Dictionary<string, SerializedProperty> textures = new Dictionary<string, SerializedProperty>();
            readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();

            public SavedProperties(Material m)
            {
                var so = new SerializedObject(m);
                var tex = so.FindProperty("m_SavedProperties.m_TexEnvs");
                for (int i = 0; i < tex.arraySize; i++)
                {
                    var e = tex.GetArrayElementAtIndex(i);
                    textures[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").Copy();
                }
                var fl = so.FindProperty("m_SavedProperties.m_Floats");
                for (int i = 0; i < fl.arraySize; i++)
                {
                    var e = fl.GetArrayElementAtIndex(i);
                    floats[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
                }
                var col = so.FindProperty("m_SavedProperties.m_Colors");
                for (int i = 0; i < col.arraySize; i++)
                {
                    var e = col.GetArrayElementAtIndex(i);
                    colors[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
                }
            }

            public bool HasTexture(string name) => textures.TryGetValue(name, out var p) && p.FindPropertyRelative("m_Texture").objectReferenceValue != null;
            public bool HasFloat(string name) => floats.ContainsKey(name);
            public float GetFloat(string name, float fallback) => floats.TryGetValue(name, out var v) ? v : fallback;
            public Color GetColor(string name, Color fallback) => colors.TryGetValue(name, out var v) ? v : fallback;

            public void GetTexture(string name, out Texture tex, out Vector2 scale, out Vector2 offset)
            {
                tex = null; scale = Vector2.one; offset = Vector2.zero;
                if (!textures.TryGetValue(name, out var p)) return;
                tex = p.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
                scale = p.FindPropertyRelative("m_Scale").vector2Value;
                offset = p.FindPropertyRelative("m_Offset").vector2Value;
            }
        }

        // ---------------------------------------------------------------- lights

        /// <summary>
        /// Swaps HDAdditionalLightData for UniversalAdditionalLightData and rescales the physical HDRP intensity.
        /// A light without UniversalAdditionalLightData is treated as HDRP-authored (some were built without
        /// HDRP data), and gets that component here, so running it twice is harmless.
        /// </summary>
        public static string ConvertLights()
        {
            var hdType = FindType(HDLightType);
            var log = new StringBuilder();
            int converted = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.TryGetComponent<UniversalAdditionalLightData>(out _)) continue;
                Undo.RecordObject(l, "Convert light to URP");
                float before = l.intensity;
                var unit = l.lightUnit;
                float urp = IsFlashlight(l) ? FlashlightIntensity : Mathf.Min(CandelaOf(l) * CandelaToURP, MaxSceneLight);
                var hd = hdType != null ? l.GetComponent(hdType) : null;
                if (hd != null) Undo.DestroyObjectImmediate(hd);
                Undo.AddComponent<UniversalAdditionalLightData>(l.gameObject);
                l.intensity = urp;
                EditorUtility.SetDirty(l);
                converted++;
                if (converted <= 400) log.AppendLine("  " + l.name + " (" + l.type + ") " + before + " " + unit + " -> " + urp.ToString("0.###"));
            }
            return "Converted " + converted + " lights:\n" + log;
        }

        /// <summary>HDRP light intensity expressed in candela (lux for directional lights).</summary>
        static float CandelaOf(Light l)
        {
            if (l.type == LightType.Directional) return l.intensity;
            switch (l.lightUnit)
            {
                case LightUnit.Lumen:
                    if (l.type == LightType.Spot && l.enableSpotReflector)
                        return l.intensity / (2f * Mathf.PI * (1f - Mathf.Cos(0.5f * l.spotAngle * Mathf.Deg2Rad)));
                    return l.intensity / (4f * Mathf.PI);
                case LightUnit.Lux: return l.intensity * l.luxAtDistance * l.luxAtDistance;
                case LightUnit.Ev100: return Mathf.Pow(2f, l.intensity - 3f);
                default: return l.intensity; // Candela
            }
        }

        static bool IsFlashlight(Light l)
        {
            if (l.type != LightType.Spot) return false;
            var controller = FindType("HollowTower.RemasteredPlayerController");
            return controller != null && l.GetComponentInParent(controller, true) != null;
        }

        // ---------------------------------------------------------------- cameras

        /// <summary>Swaps HDAdditionalCameraData for UniversalAdditionalCameraData with post-processing on.</summary>
        public static string ConvertCameras()
        {
            var hdType = FindType(HDCameraType);
            var log = new StringBuilder();
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                var hd = hdType != null ? cam.GetComponent(hdType) : null;
                bool hadHD = hd != null;
                var aa = AntialiasingMode.None;
                bool dithering = false, stopNaN = false;
                LayerMask volumeMask = 1;
                if (hadHD)
                {
                    string hdAA = GetField(hd, "antialiasing")?.ToString() ?? "";
                    if (hdAA.Contains("Temporal")) aa = AntialiasingMode.TemporalAntiAliasing;
                    else if (hdAA.Contains("Subpixel")) aa = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    else if (hdAA.Contains("Fast")) aa = AntialiasingMode.FastApproximateAntialiasing;
                    dithering = GetField(hd, "dithering") is bool d && d;
                    stopNaN = GetField(hd, "stopNaNs") is bool n && n;
                    if (GetField(hd, "volumeLayerMask") is LayerMask vm) volumeMask = vm;
                    Undo.DestroyObjectImmediate(hd);
                }
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(cam.gameObject);
                Undo.RecordObject(data, "Convert camera to URP");
                Undo.RecordObject(cam, "Convert camera to URP");
                data.renderPostProcessing = true;
                data.antialiasing = aa;
                data.dithering = dithering;
                data.stopNaN = stopNaN;
                data.volumeLayerMask = volumeMask;
                cam.allowHDR = true;
                EditorUtility.SetDirty(data);
                EditorUtility.SetDirty(cam);
                log.AppendLine("  " + cam.name + ": post-processing on, AA " + aa + ", dithering " + dithering + ", volume mask " + volumeMask.value + (hadHD ? " (HDRP data removed)" : ""));
            }
            return "Cameras:\n" + log;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A component type by full name, or null when its package/assembly isn't loaded.</summary>
        static Type FindType(string fullName)
        {
            foreach (var t in TypeCache.GetTypesDerivedFrom<Component>())
                if (t.FullName == fullName) return t;
            return null;
        }

        static object GetField(object o, string name)
        {
            var t = o.GetType();
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (f != null) return f.GetValue(o);
            var p = t.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            return p != null && p.CanRead ? p.GetValue(o) : null;
        }
    }
}
