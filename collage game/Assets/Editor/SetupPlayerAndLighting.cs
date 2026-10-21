using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SetupPlayerAndLighting
{
    [MenuItem("Tools/Setup Player & Atmosphere")]
    public static void Setup()
    {
        // 1. SETUP / CONFIGURE PLAYER
        GameObject existingPlayer = GameObject.Find("Player_FPS");
        if (existingPlayer != null)
        {
            Undo.DestroyObjectImmediate(existingPlayer);
        }

        GameObject player = new GameObject("Player_FPS");
        Undo.RegisterCreatedObjectUndo(player, "Create Player_FPS");
        player.transform.position = new Vector3(0f, 0.1f, -6.5f);
        player.transform.rotation = Quaternion.LookRotation(new Vector3(0.2f, 1.3f, 0.9f) - new Vector3(0f, 1.6f, -6.5f));

        // CharacterController
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.38f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.slopeLimit = 45f;
        cc.stepOffset = 0.35f;

        // Rough character proxy body (casts shadow on floor)
        GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyVisual.name = "Character_RoughBody";
        bodyVisual.transform.parent = player.transform;
        bodyVisual.transform.localPosition = new Vector3(0, 0.9f, 0);
        bodyVisual.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
        
        Collider bodyCol = bodyVisual.GetComponent<Collider>();
        if (bodyCol != null) Object.DestroyImmediate(bodyCol);

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        Material matBody = new Material(urpLit);
        matBody.name = "Mat_CharacterRough";
        matBody.color = new Color(0.22f, 0.25f, 0.28f);
        if (matBody.HasProperty("_BaseColor")) matBody.SetColor("_BaseColor", matBody.color);
        if (matBody.HasProperty("_Smoothness")) matBody.SetFloat("_Smoothness", 0.25f);
        bodyVisual.GetComponent<Renderer>().sharedMaterial = matBody;

        // Attach Camera to Player Head
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
        }

        mainCam.transform.parent = player.transform;
        mainCam.transform.localPosition = new Vector3(0, 1.65f, 0.12f);
        mainCam.transform.localRotation = Quaternion.identity;
        mainCam.nearClipPlane = 0.1f;
        mainCam.fieldOfView = 68f;

        // Attach FPS Controller
        FirstPersonController fpsCtrl = player.AddComponent<FirstPersonController>();
        fpsCtrl.playerCamera = mainCam.transform;
        fpsCtrl.walkSpeed = 4.2f;
        fpsCtrl.sprintSpeed = 7.2f;
        fpsCtrl.jumpHeight = 1.25f;
        fpsCtrl.gravity = -18f;
        fpsCtrl.mouseSensitivity = 2.2f;

        // 2. SETUP ENVIRONMENT LIGHTING (INDOOR ATMOSPHERE)
        // Directional Light tuned as dark shaft moonlight fill
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Light sunLight = null;
        foreach (Light l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                sunLight = l;
                break;
            }
        }

        if (sunLight == null)
        {
            GameObject sunObj = new GameObject("Directional Light");
            sunLight = sunObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
        }

        sunLight.transform.rotation = Quaternion.Euler(72f, 15f, 0f);
        sunLight.color = new Color(0.72f, 0.82f, 0.95f); // Cool shaft moonlight
        sunLight.intensity = 0.15f; // Dim fill so indoor dynamic lights dominate!
        sunLight.shadows = LightShadows.Soft;
        sunLight.shadowNearPlane = 0.2f;

        // Subterranean Ambient & Atmospheric Fog
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.08f, 0.10f, 0.14f);
        RenderSettings.ambientEquatorColor = new Color(0.05f, 0.06f, 0.08f);
        RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.03f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.016f;
        RenderSettings.fogColor = new Color(0.08f, 0.10f, 0.13f);

        // 3. POST-PROCESSING VOLUME (URP HIGH FIDELITY)
        Volume globalVol = Object.FindAnyObjectByType<Volume>();
        if (globalVol == null)
        {
            GameObject volObj = new GameObject("Global PostProcess Volume");
            globalVol = volObj.AddComponent<Volume>();
            globalVol.isGlobal = true;
        }

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "ElevatorRoomAtmosphereProfile";

        // Cinematic Bloom for dynamic lights
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 1.35f;
        bloom.threshold.value = 0.88f;
        bloom.scatter.value = 0.72f;

        // Tonemapping ACES
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.value = TonemappingMode.ACES;

        // Color Adjustments with high dynamic contrast
        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.value = 0.35f;
        colorAdj.contrast.value = 22f;
        colorAdj.saturation.value = 12f;

        // Vignette
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.32f;
        vignette.smoothness.value = 0.45f;

        globalVol.profile = profile;

        Debug.Log("[SetupPlayerAndLighting] First Person Controller and Room Dynamic Lighting Atmosphere successfully set up!");
    }
}
