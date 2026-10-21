using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SetupHorrorVisuals
{
    private const string AlbedoPath = "Assets/Characters/Entity38/Textures/Entity38_Albedo.png";
    private const string MaskMapPath = "Assets/Characters/Entity38/Textures/Entity38_MetallicSmoothness_URP.png";
    private const string NormalPath = "Assets/Characters/Entity38/Textures/Entity38_Normal.png";
    private const string MatPath = "Assets/Characters/Entity38/Materials/Mat_Entity38_Needlelimbs.mat";
    private const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";

    [MenuItem("Tools/Apply Horror Visuals (Reference Re-creation)")]
    public static void ApplyAll()
    {
        Debug.Log("[HorrorVisuals] Starting visual style recreation...");

        // 1. CONFIGURE TEXTURE IMPORTERS
        ConfigureTextureImporters();

        // 2. CONFIGURE SKIN PBR MATERIAL
        ConfigureSkinMaterial();

        // 3. CONFIGURE CAMERA POST-PROCESSING
        ConfigureCameraPostProcessing();

        // 4. CONFIGURE POST-PROCESSING VOLUME (Chromatic Aberration, Film Grain, Color Grading, Bloom)
        ConfigurePostProcessingVolume();

        // 5. CONFIGURE DRAMATIC CINEMATIC LIGHTING
        ConfigureCinematicLighting();

        // 6. CONFIGURE ENTITY AI STANDOFF
        ConfigureEntityAI();

        // Save assets and scene
        AssetDatabase.SaveAssets();
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

        Debug.Log("[HorrorVisuals] Successfully applied all horror visual systems!");
    }

    private static void ConfigureTextureImporters()
    {
        // Normal Map
        TextureImporter normalImporter = AssetImporter.GetAtPath(NormalPath) as TextureImporter;
        if (normalImporter != null)
        {
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
                Debug.Log("[HorrorVisuals] Reimported Entity38_Normal as NormalMap.");
            }
        }

        // Mask Map (Linear / Non-sRGB)
        TextureImporter maskImporter = AssetImporter.GetAtPath(MaskMapPath) as TextureImporter;
        if (maskImporter != null)
        {
            if (maskImporter.sRGBTexture)
            {
                maskImporter.sRGBTexture = false;
                maskImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                maskImporter.SaveAndReimport();
                Debug.Log("[HorrorVisuals] Reimported Entity38_MetallicSmoothness_URP as Linear Mask.");
            }
        }

        // Albedo Map (sRGB Color)
        TextureImporter albedoImporter = AssetImporter.GetAtPath(AlbedoPath) as TextureImporter;
        if (albedoImporter != null)
        {
            if (!albedoImporter.sRGBTexture)
            {
                albedoImporter.sRGBTexture = true;
                albedoImporter.SaveAndReimport();
                Debug.Log("[HorrorVisuals] Reimported Entity38_Albedo as sRGB.");
            }
        }
    }

    private static void ConfigureSkinMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && mat.shader != urpLit)
            {
                mat.shader = urpLit;
            }
        }

        Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskMapPath);
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

        // A. Base Color: Dark charcoal-black, preserving subtle biological variations
        if (albedoTex != null)
        {
            mat.SetTexture("_BaseMap", albedoTex);
        }
        // Base color multiplier: clean neutral white so texture data displays directly
        mat.SetColor("_BaseColor", Color.white);

        // B. Metallic & Smoothness Mask
        // Red = Metallic (0.08), Green = Cavity Occlusion, Alpha = Smoothness (0.58 - 0.86 wet crests)
        if (maskTex != null)
        {
            mat.SetTexture("_MetallicGlossMap", maskTex);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        mat.SetFloat("_Metallic", 1.0f);
        mat.SetFloat("_Smoothness", 1.0f);
        if (mat.HasProperty("_SmoothnessTextureChannel"))
        {
            mat.SetFloat("_SmoothnessTextureChannel", 0f); // MetallicAlpha
        }

        // D. Normal Map: Subtle biological skin folds and wrinkles
        if (normalTex != null)
        {
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", 1.15f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // Clear any emissive or unwanted rim glows
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");

        EditorUtility.SetDirty(mat);
        Debug.Log("[HorrorVisuals] Skin Material Mat_Entity38_Needlelimbs successfully configured with URP Lit PBR.");

        // Apply to any character instances in scene
        GameObject character = GameObject.Find("Entity38_Needlelimbs");
        if (character != null)
        {
            foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.sharedMaterial = mat;
            }
            foreach (var mr in character.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.sharedMaterial = mat;
            }
        }
    }

    private static void ConfigureCameraPostProcessing()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = GameObject.Find("Main Camera");
            if (camObj != null) mainCam = camObj.GetComponent<Camera>();
        }

        if (mainCam != null)
        {
            var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            // CRITICAL: Enable post-processing pipeline on camera
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            EditorUtility.SetDirty(camData);
            EditorUtility.SetDirty(mainCam.gameObject);
            Debug.Log("[HorrorVisuals] Main Camera UniversalAdditionalCameraData configured with renderPostProcessing = true!");
        }
        else
        {
            Debug.LogWarning("[HorrorVisuals] Main Camera not found to configure post-processing.");
        }
    }

    private static void ConfigurePostProcessingVolume()
    {
        Volume volume = Object.FindAnyObjectByType<Volume>();
        if (volume == null)
        {
            GameObject volObj = new GameObject("Global PostProcess Volume");
            volume = volObj.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        volume.sharedProfile = profile;

        // 1. CHROMATIC ABERRATION (Subtle optical lens RGB fringe on silhouette & highlights)
        ChromaticAberration chrom;
        if (!profile.TryGet(out chrom))
        {
            chrom = profile.Add<ChromaticAberration>(true);
        }
        chrom.active = true;
        chrom.intensity.overrideState = true;
        chrom.intensity.value = 0.50f;

        // 2. FILM GRAIN (Gritty dark horror film texture)
        FilmGrain grain;
        if (!profile.TryGet(out grain))
        {
            grain = profile.Add<FilmGrain>(true);
        }
        grain.active = true;
        grain.type.overrideState = true;
        grain.type.value = FilmGrainLookup.Thin1;
        grain.intensity.overrideState = true;
        grain.intensity.value = 0.35f;
        grain.response.overrideState = true;
        grain.response.value = 0.78f; // Concentrated in darker regions

        // 3. COLOR ADJUSTMENTS (Cold, desaturated, deep shadows horror look)
        ColorAdjustments colorAdj;
        if (!profile.TryGet(out colorAdj))
        {
            colorAdj = profile.Add<ColorAdjustments>(true);
        }
        colorAdj.active = true;
        colorAdj.saturation.overrideState = true;
        colorAdj.saturation.value = -22f; // Cold desaturated look
        colorAdj.contrast.overrideState = true;
        colorAdj.contrast.value = 20f; // Deep, sinister shadows
        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = 0.05f;

        // 4. BLOOM (Restrained highlight response - wet glints only, no glowing paint/halo)
        Bloom bloom;
        if (!profile.TryGet(out bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.05f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.20f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.40f;

        // 5. VIGNETTE (Dark peripheral focus)
        Vignette vignette;
        if (!profile.TryGet(out vignette))
        {
            vignette = profile.Add<Vignette>(true);
        }
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.36f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.50f;

        // 6. TONEMAPPING
        Tonemapping tonemapping;
        if (!profile.TryGet(out tonemapping))
        {
            tonemapping = profile.Add<Tonemapping>(true);
        }
        tonemapping.active = true;
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;

        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(volume);
        Debug.Log("[HorrorVisuals] Post-processing Volume Profile configured with Chromatic Aberration, Film Grain, and Color Grading!");
    }

    private static void ConfigureCinematicLighting()
    {
        // Character is at ~(0, 0.05, -4.5).
        // The reference has a sharp, raking key light from upper-side that carves out the curved crest and shoulder
        // while the face/mouth cavity stays in deep, menacing shadow.
        
        // 1. Key Rake Light (High 3/4 side light)
        GameObject keyObj = GameObject.Find("Entity38_KeyRakeLight");
        if (keyObj == null)
        {
            keyObj = new GameObject("Entity38_KeyRakeLight");
        }
        Light keyLight = keyObj.GetComponent<Light>();
        if (keyLight == null) keyLight = keyObj.AddComponent<Light>();

        keyLight.type = LightType.Spot;
        keyLight.transform.position = new Vector3(-1.8f, 3.8f, -3.6f);
        keyLight.transform.LookAt(new Vector3(0.0f, 2.2f, -4.5f)); // Aim directly at creature skull crest
        keyLight.spotAngle = 48f;
        keyLight.innerSpotAngle = 24f;
        keyLight.range = 7.5f;
        keyLight.color = new Color(0.88f, 0.92f, 0.98f); // Cold specular key
        keyLight.intensity = 22f; // Punchy specular highlight on wet crest
        keyLight.shadows = LightShadows.Soft;

        // 2. Subtle Rim Kick Light (Back-side kicker)
        GameObject rimObj = GameObject.Find("Entity38_RimKickLight");
        if (rimObj == null)
        {
            rimObj = new GameObject("Entity38_RimKickLight");
        }
        Light rimLight = rimObj.GetComponent<Light>();
        if (rimLight == null) rimLight = rimObj.AddComponent<Light>();

        rimLight.type = LightType.Spot;
        rimLight.transform.position = new Vector3(2.2f, 2.8f, -5.6f);
        rimLight.transform.LookAt(new Vector3(0.1f, 1.8f, -4.5f)); // Aim at rear/side silhouette
        rimLight.spotAngle = 55f;
        rimLight.innerSpotAngle = 30f;
        rimLight.range = 6.0f;
        rimLight.color = new Color(0.75f, 0.80f, 0.85f); // Restrained cool rim
        rimLight.intensity = 8.5f; // Faint silhouette definition without glow halo
        rimLight.shadows = LightShadows.None;

        Debug.Log("[HorrorVisuals] Cinematic Key Rake and Rim Kick lights configured!");
    }

    private static void ConfigureEntityAI()
    {
        GameObject character = GameObject.Find("Entity38_Needlelimbs");
        if (character != null)
        {
            Entity38EnemyAI ai = character.GetComponent<Entity38EnemyAI>();
            if (ai != null)
            {
                ai.canAttack = false; // Player can freely inspect without damage/death!
                ai.attackRange = 2.0f; // Standoff distance
                EditorUtility.SetDirty(ai);
                Debug.Log("[HorrorVisuals] Entity38EnemyAI canAttack set to false for safe inspection.");
            }
        }
    }
}
