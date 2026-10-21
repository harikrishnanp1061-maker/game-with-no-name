using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Player 4 Unique Ability: Tactical Torch Light (Flashlight).
/// - Wide beam spread (54°) with concentrated tactical core hotspot (26°).
/// - Enhanced base illumination (13.5 lux) with dynamic inverse-distance scaling (high intensity close-up, tapering off into distance).
/// - Subtle surface reflection bounce point light dynamically positioned on hit surfaces.
/// - When controlling Player 4: Handheld torch parents to active camera for full pitch/yaw tracking.
/// - When un-controlling Player 4: Mounts directly to Player_4 physical body at eye level, shining into the world.
/// - No dummy stand-in cylinders or proxy geometry.
/// </summary>
[ExecuteAlways]
[AddComponentMenu("FLIP//FALL/Gameplay/Player Torch Ability")]
[DisallowMultipleComponent]
public class PlayerTorchAbility : MonoBehaviour
{
    private static PlayerTorchAbility _instance;
    public static PlayerTorchAbility Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = UnityEngine.Object.FindAnyObjectByType<PlayerTorchAbility>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Torch Configuration")]
    [SerializeField] private float _range = 32f;
    [SerializeField] private float _spotAngle = 54f;
    [SerializeField] private float _innerSpotAngle = 24f;
    [SerializeField] private float _baseIntensity = 13.5f;
    [SerializeField] private float _baseCoreIntensity = 9.0f;
    [SerializeField] private Color _lightColor = new Color(0.96f, 0.98f, 1.0f);
    [SerializeField] private LightShadows _shadows = LightShadows.Soft;

    [Header("Surface Reflection & Close-up Falloff")]
    [Tooltip("How strongly close-up surfaces amplify perceived light intensity")]
    [SerializeField] private float _closeUpBoost = 8.0f;
    [Tooltip("Distance in meters within which light feels intensely concentrated")]
    [SerializeField] private float _closeUpDistance = 5.5f;
    [Tooltip("Max intensity for the soft surface reflection bounce light")]
    [SerializeField] private float _maxBounceIntensity = 2.2f;

    [Header("Input")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip _torchClickOnClip;
    [SerializeField] private AudioClip _torchClickOffClip;

    [Header("State")]
    [SerializeField] private bool _isTorchOn = true;

    private AudioSource _torchAudioSource;

    // Viewmodel / Held Torch
    private Transform _cameraHolder;
    private GameObject _handheldTorchRoot;
    private Light _handheldSpotLight;
    private Light _coreHotspotLight;
    private Light _surfaceBounceLight;
    private Renderer _handheldLensRenderer;
    private Material _lensOnMaterial;
    private Material _lensOffMaterial;

    public bool IsTorchOn => _isTorchOn;
    public float Range => _range;
    public float SpotAngle => _spotAngle;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
            return;
        }
        _instance = this;

        ValidateAndEnforceLightingParameters();
        PurgeLegacyStandIns();
        FindCameraHolder();
        EnsureHandheldTorch();
    }

    private void ValidateAndEnforceLightingParameters()
    {
        if (_spotAngle < 54f) _spotAngle = 54f;
        if (_innerSpotAngle < 24f) _innerSpotAngle = 24f;
        if (_range < 32f) _range = 32f;
        if (_baseIntensity < 13.5f) _baseIntensity = 13.5f;
        if (_baseCoreIntensity < 9.0f) _baseCoreIntensity = 9.0f;
        if (_closeUpBoost < 8.0f) _closeUpBoost = 8.0f;
        if (_closeUpDistance < 5.5f) _closeUpDistance = 5.5f;
        if (_maxBounceIntensity < 2.0f) _maxBounceIntensity = 2.2f;
    }

    private void OnEnable()
    {
        PlayerIdentityManager.OnIdentityChanged += HandleIdentityChanged;
        PurgeLegacyStandIns();
        EnsureHandheldTorch();
        UpdateTorchStateForCurrentIdentity();
    }

    private void OnDisable()
    {
        PlayerIdentityManager.OnIdentityChanged -= HandleIdentityChanged;
    }

    private void Start()
    {
        PurgeLegacyStandIns();
        EnsureHandheldTorch();
        UpdateTorchStateForCurrentIdentity();
    }

    public static void PurgeLegacyStandIns()
    {
        // Destroy any legacy cylinder proxy objects in the scene immediately
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            if (go.name.Contains("Player4_Teammate_StandIn") || go.name == "P4_Body" || go.name == "StandIn_Torch" || go.name == "P4_HeadHolder")
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorUtility.IsPersistent(go))
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
#else
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
#endif
            }
        }
    }

    private void FindCameraHolder()
    {
        Camera cam = null;
        if (PlayerIdentityManager.CurrentIdentity == PlayerIdentity.Player4 && Camera.main != null)
        {
            cam = Camera.main;
        }
        else
        {
            cam = GetComponentInChildren<Camera>();
            if (cam == null && Camera.main != null && Camera.main.transform.IsChildOf(transform))
            {
                cam = Camera.main;
            }
        }

        if (cam != null)
        {
            _cameraHolder = cam.transform;
        }
        else
        {
            Transform found = transform.Find("CameraHolder");
            _cameraHolder = found != null ? found : transform;
        }
    }

    public void EnsureHandheldTorch()
    {
        PurgeLegacyStandIns();
        FindCameraHolder();

        // Search for existing torch root
        Transform existing = null;
        Camera mainCam = Camera.main;
        if (mainCam != null) existing = mainCam.transform.Find("Player4_HandheldTorch");
        if (existing == null && _cameraHolder != null) existing = _cameraHolder.Find("Player4_HandheldTorch");
        if (existing == null)
        {
            Transform ch = transform.Find("CameraHolder");
            if (ch != null) existing = ch.Find("Player4_HandheldTorch");
        }

        Transform targetParent = (PlayerIdentityManager.CurrentIdentity == PlayerIdentity.Player4 && mainCam != null)
            ? mainCam.transform
            : (_cameraHolder != null ? _cameraHolder : transform);

        if (existing != null)
        {
            _handheldTorchRoot = existing.gameObject;
            if (_handheldTorchRoot.transform.parent != targetParent)
            {
                _handheldTorchRoot.transform.SetParent(targetParent, false);
                _handheldTorchRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.40f);
                _handheldTorchRoot.transform.localRotation = Quaternion.Euler(1.5f, -1.0f, 0f);
            }
            ResolveLightsAndMaterials();
            return;
        }

        _handheldTorchRoot = new GameObject("Player4_HandheldTorch");
        _handheldTorchRoot.transform.SetParent(targetParent, false);
        // Positioned lower-right in first-person camera view
        _handheldTorchRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.40f);
        _handheldTorchRoot.transform.localRotation = Quaternion.Euler(1.5f, -1.0f, 0f);

        CreateTorchMeshAndComponents();
        ResolveLightsAndMaterials();
    }

    private void CreateTorchMeshAndComponents()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        _lensOnMaterial = new Material(litShader);
        _lensOnMaterial.color = Color.white;
        if (_lensOnMaterial.HasProperty("_EmissionColor"))
        {
            _lensOnMaterial.EnableKeyword("_EMISSION");
            _lensOnMaterial.SetColor("_EmissionColor", new Color(3.5f, 3.5f, 3.5f));
        }

        _lensOffMaterial = new Material(litShader);
        _lensOffMaterial.color = new Color(0.2f, 0.2f, 0.22f);
        if (_lensOffMaterial.HasProperty("_EmissionColor"))
        {
            _lensOffMaterial.SetColor("_EmissionColor", Color.black);
        }

        Material metalMat = new Material(litShader);
        metalMat.color = new Color(0.18f, 0.20f, 0.22f);
        if (metalMat.HasProperty("_Smoothness")) metalMat.SetFloat("_Smoothness", 0.65f);
        if (metalMat.HasProperty("_Metallic")) metalMat.SetFloat("_Metallic", 0.85f);

        // 1. Torch Handle Cylinder
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Torch_Handle";
        handle.transform.SetParent(_handheldTorchRoot.transform, false);
        handle.transform.localPosition = new Vector3(0f, 0f, 0f);
        handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        handle.transform.localScale = new Vector3(0.038f, 0.08f, 0.038f);
        Collider hCol = handle.GetComponent<Collider>();
        if (hCol != null) DestroyImmediate(hCol);
        handle.GetComponent<Renderer>().sharedMaterial = metalMat;

        // 2. Torch Bezel / Head
        GameObject bezel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bezel.name = "Torch_Head";
        bezel.transform.SetParent(_handheldTorchRoot.transform, false);
        bezel.transform.localPosition = new Vector3(0f, 0f, 0.095f);
        bezel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bezel.transform.localScale = new Vector3(0.052f, 0.025f, 0.052f);
        Collider bCol = bezel.GetComponent<Collider>();
        if (bCol != null) DestroyImmediate(bCol);
        bezel.GetComponent<Renderer>().sharedMaterial = metalMat;

        // 3. Torch Glass Lens
        GameObject lensObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lensObj.name = "Torch_Lens";
        lensObj.transform.SetParent(_handheldTorchRoot.transform, false);
        lensObj.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        lensObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        lensObj.transform.localScale = new Vector3(0.046f, 0.003f, 0.046f);
        Collider lCol = lensObj.GetComponent<Collider>();
        if (lCol != null) DestroyImmediate(lCol);
        _handheldLensRenderer = lensObj.GetComponent<Renderer>();
        _handheldLensRenderer.sharedMaterial = _isTorchOn ? _lensOnMaterial : _lensOffMaterial;
    }

    private void ResolveLightsAndMaterials()
    {
        if (_handheldTorchRoot == null) return;
        ValidateAndEnforceLightingParameters();

        // 1. Primary Broad Spotlight
        Transform lightObj = _handheldTorchRoot.transform.Find("Torch_SpotLight");
        if (lightObj == null)
        {
            GameObject lo = new GameObject("Torch_SpotLight");
            lo.transform.SetParent(_handheldTorchRoot.transform, false);
            lo.transform.localPosition = new Vector3(0f, 0f, 0.125f);
            lo.transform.localRotation = Quaternion.identity;
            _handheldSpotLight = lo.AddComponent<Light>();
        }
        else
        {
            _handheldSpotLight = lightObj.GetComponent<Light>();
        }

        if (_handheldSpotLight != null)
        {
            _handheldSpotLight.type = LightType.Spot;
            _handheldSpotLight.range = _range;
            _handheldSpotLight.spotAngle = _spotAngle;
            _handheldSpotLight.innerSpotAngle = _innerSpotAngle;
            _handheldSpotLight.intensity = _baseIntensity;
            _handheldSpotLight.color = _lightColor;
            _handheldSpotLight.shadows = _shadows;
            _handheldSpotLight.bounceIntensity = 1.35f;
            _handheldSpotLight.shadowNearPlane = 0.1f;
            _handheldSpotLight.enabled = _isTorchOn;
        }

        // 2. Concentrated Core Hotspot Spotlight
        Transform coreObj = _handheldTorchRoot.transform.Find("Torch_CoreHotspot");
        if (coreObj == null)
        {
            GameObject co = new GameObject("Torch_CoreHotspot");
            co.transform.SetParent(_handheldTorchRoot.transform, false);
            co.transform.localPosition = new Vector3(0f, 0f, 0.125f);
            co.transform.localRotation = Quaternion.identity;
            _coreHotspotLight = co.AddComponent<Light>();
        }
        else
        {
            _coreHotspotLight = coreObj.GetComponent<Light>();
        }

        if (_coreHotspotLight != null)
        {
            _coreHotspotLight.type = LightType.Spot;
            _coreHotspotLight.range = 18f;
            _coreHotspotLight.spotAngle = 26f;
            _coreHotspotLight.innerSpotAngle = 10f;
            _coreHotspotLight.intensity = _baseCoreIntensity;
            _coreHotspotLight.color = _lightColor;
            _coreHotspotLight.shadows = LightShadows.None;
            _coreHotspotLight.bounceIntensity = 1.0f;
            _coreHotspotLight.enabled = _isTorchOn;
        }

        // 3. Subtle Surface Reflection Bounce Point Light
        Transform bounceObj = _handheldTorchRoot.transform.Find("Torch_SurfaceBounce");
        if (bounceObj == null)
        {
            GameObject bo = new GameObject("Torch_SurfaceBounce");
            bo.transform.SetParent(_handheldTorchRoot.transform, false);
            bo.transform.localPosition = new Vector3(0f, 0f, 1.0f);
            _surfaceBounceLight = bo.AddComponent<Light>();
        }
        else
        {
            _surfaceBounceLight = bounceObj.GetComponent<Light>();
        }

        if (_surfaceBounceLight != null)
        {
            _surfaceBounceLight.type = LightType.Point;
            _surfaceBounceLight.range = 3.2f;
            _surfaceBounceLight.intensity = 0.8f;
            _surfaceBounceLight.color = new Color(0.94f, 0.96f, 1.0f);
            _surfaceBounceLight.shadows = LightShadows.None;
            _surfaceBounceLight.bounceIntensity = 1.5f;
            _surfaceBounceLight.enabled = _isTorchOn;
        }

        Transform lens = _handheldTorchRoot.transform.Find("Torch_Lens");
        if (lens != null) _handheldLensRenderer = lens.GetComponent<Renderer>();

        if (_torchAudioSource == null)
        {
            _torchAudioSource = GetComponent<AudioSource>();
            if (_torchAudioSource == null) _torchAudioSource = gameObject.AddComponent<AudioSource>();
            _torchAudioSource.playOnAwake = false;
            _torchAudioSource.spatialBlend = 0.2f;
        }

#if UNITY_EDITOR
        if (_torchClickOnClip == null)
            _torchClickOnClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Torch_Click_On.wav");
        if (_torchClickOffClip == null)
            _torchClickOffClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Torch_Click_Off.wav");
#endif
    }

    private void Update()
    {
        // Toggle torch on/off with F key when playing as Player 4
        if (PlayerIdentityManager.CurrentIdentity == PlayerIdentity.Player4)
        {
            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
#else
            if (Input.GetKeyDown(_toggleKey))
            {
                togglePressed = true;
            }
#endif
            if (togglePressed)
            {
                ToggleTorch();
            }
        }
    }

    public void ToggleTorch()
    {
        SetTorchOn(!_isTorchOn);
    }

    public void SetTorchOn(bool state)
    {
        _isTorchOn = state;

        SetTorchLightsEnabled(_isTorchOn);

        if (_handheldLensRenderer != null && _lensOnMaterial != null && _lensOffMaterial != null)
        {
            _handheldLensRenderer.sharedMaterial = _isTorchOn ? _lensOnMaterial : _lensOffMaterial;
        }

        // Play mechanical switch click
        if (_torchAudioSource != null)
        {
            AudioClip click = _isTorchOn ? _torchClickOnClip : _torchClickOffClip;
            if (click != null) _torchAudioSource.PlayOneShot(click, 0.85f);
        }

        Debug.Log($"[PlayerTorchAbility] Player 4 Torch: {(_isTorchOn ? "ON" : "OFF")}");
    }

    private void SetTorchLightsEnabled(bool state)
    {
        if (_handheldSpotLight != null) _handheldSpotLight.enabled = state;
        if (_coreHotspotLight != null) _coreHotspotLight.enabled = state;
        if (_surfaceBounceLight != null) _surfaceBounceLight.enabled = state;
    }

    private void LateUpdate()
    {
        bool isP4 = (PlayerIdentityManager.CurrentIdentity == PlayerIdentity.Player4);

        if (isP4)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                if (_handheldTorchRoot == null)
                {
                    EnsureHandheldTorch();
                }

                if (_handheldTorchRoot != null && _handheldTorchRoot.transform.parent != cam.transform)
                {
                    _handheldTorchRoot.transform.SetParent(cam.transform, false);
                    _handheldTorchRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.40f);
                    _handheldTorchRoot.transform.localRotation = Quaternion.Euler(1.5f, -1.0f, 0f);
                }
            }
        }

        // Dynamic surface reflection bounce and close-up intensity modulation
        if (_isTorchOn && _handheldSpotLight != null && _handheldSpotLight.enabled)
        {
            UpdateSurfaceReflectionAndCloseUp();
        }
    }

    private void UpdateSurfaceReflectionAndCloseUp()
    {
        Vector3 origin = _handheldSpotLight.transform.position;
        Vector3 dir = _handheldSpotLight.transform.forward;

        int layerMask = ~LayerMask.GetMask("Ignore Raycast");
        if (Physics.Raycast(origin, dir, out RaycastHit hit, _range, layerMask, QueryTriggerInteraction.Ignore))
        {
            float dist = hit.distance;

            // 1. Close-up intensity scaling:
            // High intensity when shined at close-up objects, less when objects/surfaces are further away
            float closeFactor = Mathf.Clamp01(1.0f - (dist / _closeUpDistance));
            _handheldSpotLight.intensity = _baseIntensity + (closeFactor * _closeUpBoost);
            if (_coreHotspotLight != null)
            {
                _coreHotspotLight.intensity = _baseCoreIntensity + (closeFactor * (_closeUpBoost * 1.5f));
            }

            // 2. Subtle surface reflection bounce:
            if (_surfaceBounceLight != null)
            {
                _surfaceBounceLight.enabled = true;
                // Place slightly off the hit surface along the surface normal
                _surfaceBounceLight.transform.position = hit.point + hit.normal * 0.15f;

                // Subtle reflective bounce: stronger close-up, gently fading into distance
                float bounceFactor = Mathf.Clamp01(1.0f - (dist / 8.0f));
                _surfaceBounceLight.intensity = Mathf.Lerp(0.15f, _maxBounceIntensity, bounceFactor);
                _surfaceBounceLight.range = Mathf.Lerp(1.5f, 3.5f, bounceFactor);
            }
        }
        else
        {
            _handheldSpotLight.intensity = _baseIntensity;
            if (_coreHotspotLight != null) _coreHotspotLight.intensity = _baseCoreIntensity;
            if (_surfaceBounceLight != null) _surfaceBounceLight.enabled = false;
        }
    }

    private void HandleIdentityChanged(PlayerIdentity newId, PlayerIdentity oldId)
    {
        UpdateTorchStateForCurrentIdentity();
    }

    private Transform GetPlayer4CameraHolder()
    {
        GameObject p4 = GameObject.Find("Player_4");
        if (p4 != null)
        {
            Transform ch = p4.transform.Find("CameraHolder");
            if (ch != null) return ch;
            return p4.transform;
        }
        return transform.Find("CameraHolder") ?? transform;
    }

    public void UpdateTorchStateForCurrentIdentity()
    {
        PurgeLegacyStandIns();
        EnsureHandheldTorch();

        bool isP4 = (PlayerIdentityManager.CurrentIdentity == PlayerIdentity.Player4);

        if (isP4)
        {
            // Currently controlling Player 4: Handheld torch parents to Main Camera for first-person aim
            Camera cam = Camera.main;
            if (cam != null && _handheldTorchRoot != null)
            {
                if (_handheldTorchRoot.transform.parent != cam.transform)
                {
                    _handheldTorchRoot.transform.SetParent(cam.transform, false);
                    _handheldTorchRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.40f);
                    _handheldTorchRoot.transform.localRotation = Quaternion.Euler(1.5f, -1.0f, 0f);
                }
            }

            if (_handheldTorchRoot != null) _handheldTorchRoot.SetActive(true);
            SetTorchLightsEnabled(_isTorchOn);
        }
        else
        {
            // Controlling Player 1, 2, or 3:
            // Mount the torch directly on Player_4's body (CameraHolder) so Player 4 visibly holds and shines it in 3D!
            Transform p4Holder = GetPlayer4CameraHolder();
            if (p4Holder != null && _handheldTorchRoot != null)
            {
                _handheldTorchRoot.transform.SetParent(p4Holder, false);
                _handheldTorchRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.40f);
                _handheldTorchRoot.transform.localRotation = Quaternion.Euler(1.5f, -1.0f, 0f);
            }

            // Keep torch active in the world if left ON!
            if (_handheldTorchRoot != null) _handheldTorchRoot.SetActive(_isTorchOn);
            SetTorchLightsEnabled(_isTorchOn);
        }
    }

    // -------------------------------------------------------------------------
    // STATIC QUERY API FOR INTERACTIVE OBJECTS (e.g. FUSE CELL)
    // -------------------------------------------------------------------------

    public struct TorchBeamInfo
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float Range;
        public float SpotAngle;
        public float InnerSpotAngle;
        public float Intensity;
    }

    /// <summary>
    /// Collects all active Player 4 torches currently shining in the world
    /// (either handheld by active P4 or mounted on teammate Player_4 body).
    /// </summary>
    public static void GetActiveTorchBeams(List<TorchBeamInfo> outBeams)
    {
        if (outBeams == null) return;
        outBeams.Clear();

        PlayerTorchAbility inst = Instance;
        if (inst == null || !inst._isTorchOn) return;

        if (inst._handheldSpotLight != null && inst._handheldSpotLight.enabled)
        {
            outBeams.Add(new TorchBeamInfo
            {
                Origin = inst._handheldSpotLight.transform.position,
                Direction = inst._handheldSpotLight.transform.forward,
                Range = inst._handheldSpotLight.range,
                SpotAngle = inst._handheldSpotLight.spotAngle,
                InnerSpotAngle = inst._handheldSpotLight.innerSpotAngle,
                Intensity = inst._handheldSpotLight.intensity
            });
        }
    }

    private static readonly List<TorchBeamInfo> _cachedBeams = new List<TorchBeamInfo>(2);

    /// <summary>
    /// Checks if any active Player 4 torch is currently shining upon the specified target position.
    /// Performs angular spot cone check and physics raycast line-of-sight check.
    /// </summary>
    public static bool IsTorchShiningOn(Vector3 targetPos, float targetRadius, out float illuminationFactor)
    {
        illuminationFactor = 0f;
        GetActiveTorchBeams(_cachedBeams);

        if (_cachedBeams.Count == 0) return false;

        float bestFactor = 0f;

        for (int i = 0; i < _cachedBeams.Count; i++)
        {
            TorchBeamInfo beam = _cachedBeams[i];
            Vector3 toTarget = targetPos - beam.Origin;
            float dist = toTarget.magnitude;

            if (dist > beam.Range) continue;
            if (dist < 0.01f) continue;

            Vector3 dir = toTarget / dist;
            float angle = Vector3.Angle(beam.Direction, dir);
            float halfAngle = beam.SpotAngle * 0.5f;

            if (angle > halfAngle) continue;

            // Line of sight check: ensure no opaque non-trigger obstacles occlude the light
            Ray ray = new Ray(beam.Origin, dir);
            RaycastHit[] hits = Physics.RaycastAll(ray, dist);
            bool occluded = false;

            for (int h = 0; h < hits.Length; h++)
            {
                RaycastHit hit = hits[h];
                if (hit.collider == null) continue;
                if (hit.collider.isTrigger) continue; // Triggers (pickup volumes) don't block light

                string colName = hit.collider.gameObject.name;
                // Target's own colliders and player's own controller do not block light
                if (colName.Contains("FuseCell") || colName.Contains("Player")) continue;

                // Solid obstruction blocking line of sight
                if (hit.distance < (dist - targetRadius - 0.05f))
                {
                    occluded = true;
                    break;
                }
            }

            if (occluded) continue;

            // Angular attenuation: 1.0 inside inner angle, smoothly falls off to 0.0 at outer angle
            float halfInner = beam.InnerSpotAngle * 0.5f;
            float factor = (angle <= halfInner) ? 1.0f : Mathf.Clamp01(1.0f - (angle - halfInner) / (halfAngle - halfInner));

            // Distance attenuation
            float distFactor = Mathf.Clamp01(1.0f - (dist / beam.Range));
            float combined = factor * distFactor;

            if (combined > bestFactor) bestFactor = combined;
        }

        illuminationFactor = bestFactor;
        return bestFactor > 0.02f;
    }
}
