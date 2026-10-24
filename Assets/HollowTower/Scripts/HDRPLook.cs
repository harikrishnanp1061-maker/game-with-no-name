using UnityEngine;
using UnityEngine.Rendering;
#if HOLLOWTOWER_HDRP
using UnityEngine.Rendering.HighDefinition;
#else
using UnityEngine.Rendering.Universal;
#endif

namespace HollowTower
{
#if HOLLOWTOWER_HDRP
    /// <summary>
    /// The game's HDRP look, built in code: physically based exposure, ACES, volumetric fog with
    /// light shafts, and - when the GPU supports DXR - ray-traced GI, reflections, AO and shadows
    /// (screen-space fallbacks otherwise). Also sets the camera up for TAA / DLSS.
    /// </summary>
    public static class HDRPLook
    {
        public static bool RayTracing { get; private set; }
        public static bool DLSS { get; private set; }
        /// <summary>Internal render resolution in percent (DLSS upscales to the screen).</summary>
        public static float RenderScale = 56f;
        /// <summary>F9 in game. Off by default to hold ~60 fps on a laptop RTX GPU.</summary>
        public static bool RayTracedFlashlightShadows = false;

        static ScreenSpaceReflection ssrRef;
        static ScreenSpaceAmbientOcclusion aoRef;
        static GlobalIllumination giRef;
        static bool rtAvailable;
        public static bool RayTracedEffects { get; private set; }

        /// <summary>F8 in game: switch between ray-traced and screen-space reflections / AO.</summary>
        public static void SetRayTracedEffects(bool on)
        {
            if (ssrRef == null) return;
            on &= rtAvailable;
            RayTracedEffects = on;
            ssrRef.tracing.Override(on ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            aoRef.rayTracing.Override(on);
        }

        public static Volume Create()
        {
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            RayTracing = SystemInfo.supportsRayTracing && asset != null &&
                         asset.currentPlatformRenderPipelineSettings.supportRayTracing;

            var go = new GameObject("HDRP Look (global volume)");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;

            // --- camera exposure: eyes adapt to the dark, but not all the way ---
            var exp = p.Add<Exposure>(true);
            exp.mode.Override(ExposureMode.AutomaticHistogram);
            exp.meteringMode.Override(MeteringMode.CenterWeighted);
            exp.limitMin.Override(0.6f);   // lets your eyes adapt further in dark rooms
            exp.limitMax.Override(9f);
            exp.compensation.Override(-0.4f);
            exp.adaptationMode.Override(AdaptationMode.Progressive);
            exp.adaptationSpeedDarkToLight.Override(1.2f);   // slow, so flickering lamps don't pump the whole screen
            exp.adaptationSpeedLightToDark.Override(0.7f);
            exp.histogramPercentages.Override(new Vector2(40f, 92f));

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var grade = p.Add<ColorAdjustments>(true);
            grade.saturation.Override(-18f);
            grade.contrast.Override(12f);
            grade.colorFilter.Override(new Color(0.94f, 0.97f, 1.0f));

            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(-6f);

            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.45f);

            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.75f);

            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.15f);
            bloom.scatter.Override(0.7f);
            bloom.threshold.Override(0.9f);

            p.Add<ChromaticAberration>(true).intensity.Override(0.06f);
            // No camera motion blur: it smears the view when you turn quickly.
            p.Add<MotionBlur>(true).intensity.Override(0f);

            // --- dusty air with light shafts ---
            var fog = p.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.meanFreePath.Override(90f);
            fog.quality.Override((int)ScalableSettingLevelParameter.Level.Low);
            fog.baseHeight.Override(-2f);
            fog.maximumHeight.Override(30f);
            fog.maxFogDistance.Override(200f);
            fog.enableVolumetricFog.Override(true);
            fog.albedo.Override(new Color(0.72f, 0.72f, 0.7f));
            fog.anisotropy.Override(0.55f);
            fog.depthExtent.Override(40f);
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.color.Override(new Color(0.02f, 0.022f, 0.028f));
            fog.denoisingMode.Override(FogDenoisingMode.Gaussian);

            // --- a dim night sky only feeds the ambient (there are no windows) ---
            var env = p.Add<VisualEnvironment>(true);
            env.skyType.Override((int)SkyType.Gradient);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            var sky = p.Add<GradientSky>(true);
            sky.top.Override(new Color(0.05f, 0.06f, 0.09f));
            sky.middle.Override(new Color(0.035f, 0.04f, 0.05f));
            sky.bottom.Override(new Color(0.02f, 0.02f, 0.02f));
            var ind = p.Add<IndirectLightingController>(true);
            ind.indirectDiffuseLightingMultiplier.Override(1f);

            var shadows = p.Add<HDShadowSettings>(true);
            shadows.maxShadowDistance.Override(60f);
            var contact = p.Add<ContactShadows>(true);
            contact.enable.Override(true);
            contact.length.Override(0.12f);
            contact.opacity.Override(0.9f);

            // --- reflections, ambient occlusion, global illumination ---
            var ssr = p.Add<ScreenSpaceReflection>(true);
            ssr.enabled.Override(true);
            var ssao = p.Add<ScreenSpaceAmbientOcclusion>(true);
            ssao.intensity.Override(1.1f);
            ssao.radius.Override(1.2f);
            var gi = p.Add<GlobalIllumination>(true);
            gi.enable.Override(true);

            ssrRef = ssr; aoRef = ssao; giRef = gi; rtAvailable = RayTracing; RayTracedEffects = RayTracing;
            if (RayTracing)
            {
                var rts = p.Add<RayTracingSettings>(true);
                rts.cullingMode.Override(RTASCullingMode.Sphere);
                rts.cullingDistance.Override(45f);

                ssr.tracing.Override(RayCastingMode.RayTracing);
                ssr.mode.Override(RayTracingMode.Performance);
                ssr.quality.Override((int)ScalableSettingLevelParameter.Level.Low);
                ssao.rayTracing.Override(true);
                ssao.quality.Override((int)ScalableSettingLevelParameter.Level.Low);
                // GI stays screen-space: low-quality ray-traced GI boiled and swirled on the walls.
                gi.tracing.Override(RayCastingMode.RayMarching);
                gi.quality.Override((int)ScalableSettingLevelParameter.Level.Medium);
            }
            else
            {
                ssr.tracing.Override(RayCastingMode.RayMarching);
                gi.tracing.Override(RayCastingMode.RayMarching);
                gi.quality.Override((int)ScalableSettingLevelParameter.Level.Medium);
            }
            return vol;
        }

        /// <summary>HDRP camera setup: TAA, DLSS when available, and every effect we use switched on.</summary>
        public static void SetupCamera(Camera cam)
        {
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 160f;
            var hd = cam.gameObject.GetComponent<HDAdditionalCameraData>();
            if (hd == null) hd = cam.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            hd.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = Color.black;
            hd.allowDynamicResolution = true;
            hd.allowDeepLearningSuperSampling = true;
            hd.deepLearningSuperSamplingUseCustomQualitySettings = false;
            DLSS = HDDynamicResolutionPlatformCapabilities.DLSSDetected;
            // Render at ~62% and let DLSS (or HDRP's temporal upscaler) rebuild full resolution.
            DynamicResolutionHandler.SetDynamicResScaler(() => RenderScale, DynamicResScalePolicyType.ReturnsPercentage);

            hd.customRenderingSettings = true;
            var mask = hd.renderingPathCustomFrameSettingsOverrideMask;
            var fs = hd.renderingPathCustomFrameSettings;
            System.Action<FrameSettingsField, bool> set = (f, on) => { mask.mask[(uint)f] = true; fs.SetEnabled(f, on); };
            set(FrameSettingsField.SSR, true);
            set(FrameSettingsField.SSAO, true);
            set(FrameSettingsField.SSGI, true);
            set(FrameSettingsField.Volumetrics, true);
            set(FrameSettingsField.ContactShadows, true);
            set(FrameSettingsField.ScreenSpaceShadows, true);
            set(FrameSettingsField.MotionVectors, true);
            set(FrameSettingsField.ObjectMotionVectors, true);
            set(FrameSettingsField.Decals, true);
            set(FrameSettingsField.RayTracing, RayTracing);
            hd.renderingPathCustomFrameSettingsOverrideMask = mask;
            hd.renderingPathCustomFrameSettings = fs;
        }
    }
#else
    /// <summary>
    /// URP stand-in for the HDRP look above (kept behind HOLLOWTOWER_HDRP). Same API, so the game code
    /// is unchanged: ACES, grading, vignette, grain and bloom in a code-built Volume, a dim gradient
    /// ambient in place of HDRP's night sky, and no ray tracing or DLSS.
    /// </summary>
    public static class HDRPLook
    {
        public static bool RayTracing { get { return false; } }
        public static bool DLSS { get { return false; } }
        /// <summary>Internal render resolution in percent (always native under URP).</summary>
        public static float RenderScale = 100f;
        public static bool RayTracedFlashlightShadows = false;
        public static bool RayTracedEffects { get { return false; } }

        public static void SetRayTracedEffects(bool on) { }

        public static Volume Create()
        {
            var go = new GameObject("URP Look (global volume)");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var grade = p.Add<ColorAdjustments>(true);
            grade.postExposure.Override(1f);   // no auto exposure in URP; same compensation as the remastered map
            grade.saturation.Override(-18f);
            grade.contrast.Override(12f);
            grade.colorFilter.Override(new Color(0.94f, 0.97f, 1.0f));

            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(-6f);

            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.45f);

            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.75f);

            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.7f);
            bloom.threshold.Override(1.3f);

            p.Add<ChromaticAberration>(true).intensity.Override(0.06f);
            // No camera motion blur: it smears the view when you turn quickly.
            p.Add<MotionBlur>(true).intensity.Override(0f);

            // HDRP's dim gradient night sky only fed the ambient (there are no windows).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.05f, 0.06f, 0.09f).gamma;
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.04f, 0.05f).gamma;
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.02f).gamma;
            return vol;
        }

        /// <summary>URP camera setup: post-processing on, SMAA (URP has no DLSS).</summary>
        public static void SetupCamera(Camera cam)
        {
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 160f;
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
#endif

    /// <summary>
    /// Pain and hiding feedback as a high-priority local Volume: red vignette + chromatic split
    /// when hurt, a dark tunnel while hiding. (Replaces the old OnRenderImage effect.)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenFX : MonoBehaviour
    {
        Volume vol;
        Vignette vig;
        ChromaticAberration ca;
        ColorAdjustments grade;

        void Start()
        {
            var go = new GameObject("Damage FX (volume)");
            go.transform.SetParent(transform, false);
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;
            vig = p.Add<Vignette>(true);
            vig.color.Override(new Color(0.55f, 0f, 0f));
            vig.smoothness.Override(0.6f);
            vig.intensity.Override(0f);
            ca = p.Add<ChromaticAberration>(true);
            ca.intensity.Override(0f);
            grade = p.Add<ColorAdjustments>(true);
            grade.saturation.Override(0f);
            vol.weight = 0f;
        }

        void Update()
        {
            if (vol == null) return;
            var p = PlayerController.Instance;
            float damage = 0f, hidden = 0f;
            if (p != null)
            {
                damage = Mathf.Clamp01(1f - (Time.time - p.LastHurtTime) / 0.8f);
                damage = Mathf.Max(damage, Mathf.Clamp01(1f - p.Health / 45f) * 0.6f);
                hidden = p.IsHidden ? 1f : 0f;
            }
            float w = Mathf.Max(damage, hidden * 0.7f);
            vol.weight = Mathf.MoveTowards(vol.weight, w > 0.01f ? 1f : 0f, Time.deltaTime * 6f);
            vig.color.value = Color.Lerp(Color.black, new Color(0.6f, 0f, 0f), damage > hidden * 0.5f ? 1f : 0f);
            vig.intensity.value = Mathf.Max(damage * 0.55f, hidden * 0.5f);
            ca.intensity.value = damage * 0.9f;
            grade.saturation.value = -60f * damage - 25f * hidden;
        }
    }
}
