using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// =========================================================================
// MonsterTerrorScreenEffect - Dynamic Aggravated Horror Visuals for FLIP//FALL
//
// Triggers an escalating psychological terror screen distortion on the active
// player whenever Entity 38 becomes aggravated (Chasing / Attacking) and the
// player is within the creature's notice / pursuit range.
//
// Blends:
// - Chromatic Aberration (lens color splitting)
// - Peripheral Vignette (sinister darkness creeping inwards)
// - Lens Distortion (spatial warping / tunnel dismorphism)
// - Adrenaline Heartbeat Pulsation
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Monster Terror Screen Effect")]
[DisallowMultipleComponent]
public class MonsterTerrorScreenEffect : MonoBehaviour
{
    private static MonsterTerrorScreenEffect _instance;
    public static MonsterTerrorScreenEffect Instance => _instance;

    [Header("Detection Settings")]
    [Tooltip("Maximum range within which agitated monster induces terror screen effect")]
    public float maxTerrorRange = 16.0f;
    [Tooltip("Distance at which terror reaches peak 100% intensity")]
    public float criticalTerrorRange = 5.0f;

    [Header("Intensity Dynamics")]
    [Tooltip("Speed at which screen effect kicks in when monster aggros or pit fall occurs (slow, non-snapping ramp)")]
    public float fadeInSpeed = 0.75f;
    [Tooltip("Speed at which screen effect clears when player escapes")]
    public float fadeOutSpeed = 0.85f;

    [Header("Adrenaline Heartbeat")]
    public float pulseSpeed = 8.0f;
    public float pulseAmplitude = 0.22f;

    [Header("Preview / Debug")]
    [Range(0f, 1f)] public float forceDebugIntensity = 0f;

    // Runtime state
    private float _currentIntensity = 0f;
    private float _targetIntensity = 0f;
    private float _pitTerrorTarget = 0f;
    private Volume _terrorVolume;
    private VolumeProfile _terrorProfile;

    private ChromaticAberration _chromaticAberration;
    private FilmGrain _filmGrain;
    private ColorAdjustments _colorAdjustments;
    private Bloom _bloom;
    private LensDistortion _lensDistortion;
    private Vignette _vignette;

    private Entity38EnemyAI _monster;
    private Transform _activePlayerTransform;

    public float CurrentIntensity => _currentIntensity;
    public bool IsTerrorActive => _currentIntensity > 0.01f;

    private Transform _pitFallenPlayer = null;

    /// <summary>
    /// Smoothly engages or disengages terror screen effect when player falls into a pit.
    /// Terror is tied to the specific fallen player body so other characters do not inherit it.
    /// </summary>
    public void SetPitTerror(bool active, float targetIntensity = 1.0f, Transform fallenPlayer = null)
    {
        if (active)
        {
            _pitTerrorTarget = Mathf.Clamp01(targetIntensity);
            _pitFallenPlayer = fallenPlayer != null ? fallenPlayer : _activePlayerTransform;
        }
        else
        {
            ResetTerrorImmediately();
        }
    }

    /// <summary>
    /// Instantly cancels all terror screen effects and resets volume weight to 0.
    /// Called when the player respawns on safe ground or when switching to an un-terrorized character.
    /// </summary>
    public void ResetTerrorImmediately()
    {
        _pitTerrorTarget = 0f;
        _pitFallenPlayer = null;
        _currentIntensity = 0f;
        _targetIntensity = 0f;
        if (_terrorVolume != null) _terrorVolume.weight = 0f;
        UpdatePostProcessingEffects(0f);
    }

    /// <summary>
    /// Called when switching active player character bodies on the local machine.
    /// Immediately re-evaluates terror for the newly active character so terror from
    /// a fallen or chased player never leaks into a safe teammate.
    /// </summary>
    public void OnActivePlayerSwitched(Transform newPlayerTransform)
    {
        _activePlayerTransform = newPlayerTransform;
        CalculateTargetIntensity();

        // Snap intensity directly to the new player's context: zero latency, zero cross-player bleed
        _currentIntensity = _targetIntensity;
        float visualWeight = _currentIntensity * _currentIntensity * (3f - 2f * _currentIntensity);
        if (_terrorVolume != null) _terrorVolume.weight = visualWeight;
        UpdatePostProcessingEffects(visualWeight);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Ensure room-appropriate terror range matches monster pursuit
        if (maxTerrorRange > 18.0f || maxTerrorRange < 10.0f) maxTerrorRange = 16.0f;
        if (criticalTerrorRange > 6.0f) criticalTerrorRange = 5.0f;

        SetupTerrorVolume();
    }

    private void SetupTerrorVolume()
    {
        if (_terrorVolume == null)
        {
            _terrorVolume = gameObject.GetComponent<Volume>();
            if (_terrorVolume == null)
            {
                _terrorVolume = gameObject.AddComponent<Volume>();
            }
        }

        _terrorVolume.isGlobal = true;
        _terrorVolume.priority = 50.0f; // High priority override
        _terrorVolume.weight = 0f;

        if (_terrorProfile == null)
        {
            _terrorProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _terrorProfile.name = "MonsterTerrorRuntimeProfile";

            // 1. Chromatic Aberration - Heightened optical RGB fringe
            _chromaticAberration = _terrorProfile.Add<ChromaticAberration>(true);
            _chromaticAberration.active = true;
            _chromaticAberration.intensity.overrideState = true;
            _chromaticAberration.intensity.value = 0.85f;

            // 2. Film Grain - Gritty 35mm horror noise extending deep across midtones and screen center
            _filmGrain = _terrorProfile.Add<FilmGrain>(true);
            _filmGrain.active = true;
            _filmGrain.type.overrideState = true;
            _filmGrain.type.value = FilmGrainLookup.Thin1;
            _filmGrain.intensity.overrideState = true;
            _filmGrain.intensity.value = 0.65f;
            _filmGrain.response.overrideState = true;
            _filmGrain.response.value = 0.52f; // Reaches across screen center and midtones

            // 3. Color Adjustments - Severe oppressive desaturation and deep contrast plunge
            _colorAdjustments = _terrorProfile.Add<ColorAdjustments>(true);
            _colorAdjustments.active = true;
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.saturation.value = -45.0f; // Life-draining horror coldness
            _colorAdjustments.contrast.overrideState = true;
            _colorAdjustments.contrast.value = 35.0f; // Deep sinister shadows
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = -0.35f; // Screen center gloom

            // 4. Bloom - Wet glints and eerie hot edges
            _bloom = _terrorProfile.Add<Bloom>(true);
            _bloom.active = true;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 1.02f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = 0.35f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.65f;

            // 5. Lens Optical Tension - Heightened spatial dismorphism
            _lensDistortion = _terrorProfile.Add<LensDistortion>(true);
            _lensDistortion.active = true;
            _lensDistortion.intensity.overrideState = true;
            _lensDistortion.intensity.value = -0.14f;
            _lensDistortion.xMultiplier.overrideState = true;
            _lensDistortion.xMultiplier.value = 1.0f;
            _lensDistortion.yMultiplier.overrideState = true;
            _lensDistortion.yMultiplier.value = 1.0f;

            // 6. Deep Encroaching Vignette - Darkness creeping from edges far towards the center
            _vignette = _terrorProfile.Add<Vignette>(true);
            _vignette.active = true;
            _vignette.color.overrideState = true;
            _vignette.color.value = new Color(0.015f, 0.01f, 0.025f);
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0.32f; // Softened peripheral shading
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.50f; // Soft edge gradient without covering center view
            _vignette.rounded.overrideState = true;
            _vignette.rounded.value = false;
        }

        _terrorVolume.profile = _terrorProfile;
    }

    private void Update()
    {
        if (_monster == null || Time.frameCount % 30 == 0)
        {
            _monster = Object.FindAnyObjectByType<Entity38EnemyAI>();
        }

        UpdateActivePlayerReference();

        CalculateTargetIntensity();

        float speed = (_targetIntensity > _currentIntensity) ? fadeInSpeed : fadeOutSpeed;
        _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, Time.deltaTime * speed);

        // Smooth non-linear ease-in: prevents sudden snapping, ensures slow subtle emergence
        float visualWeight = _currentIntensity * _currentIntensity * (3f - 2f * _currentIntensity);
        _terrorVolume.weight = visualWeight;

        UpdatePostProcessingEffects(visualWeight);
    }

    private void UpdateActivePlayerReference()
    {
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.ActivePlayer != null)
        {
            _activePlayerTransform = MultiPlayerCharacterManager.Instance.ActivePlayer.transform;
            return;
        }

        if (_monster != null && _monster.TargetPlayer != null)
        {
            _activePlayerTransform = _monster.TargetPlayer;
            return;
        }

        FirstPersonController[] allFps = Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (var f in allFps)
        {
            if (f != null && f.IsControlled)
            {
                _activePlayerTransform = f.transform;
                return;
            }
        }

        var pObj = GameObject.FindWithTag("Player");
        if (pObj != null)
        {
            _activePlayerTransform = pObj.transform;
            return;
        }

        if (Camera.main != null)
        {
            _activePlayerTransform = Camera.main.transform;
        }
    }

    private void CalculateTargetIntensity()
    {
        if (forceDebugIntensity > 0f)
        {
            _targetIntensity = forceDebugIntensity;
            return;
        }

        if (_monster == null)
        {
            _monster = Object.FindAnyObjectByType<Entity38EnemyAI>();
        }

        if (_activePlayerTransform == null)
        {
            UpdateActivePlayerReference();
        }

        float monsterIntensity = 0f;
        if (_monster != null && _activePlayerTransform != null)
        {
            bool isAggravated = (_monster.CurrentState == Entity38EnemyAI.AIState.Chasing ||
                                 _monster.CurrentState == Entity38EnemyAI.AIState.Attacking);

            if (isAggravated)
            {
                float dist = Vector3.Distance(_monster.transform.position, _activePlayerTransform.position);
                if (dist <= maxTerrorRange)
                {
                    bool hasLOS = _monster.HasLineOfSightTo(_activePlayerTransform);
                    if (hasLOS || dist <= 4.5f)
                    {
                        float factor = Mathf.Clamp01((maxTerrorRange - dist) / Mathf.Max(0.1f, maxTerrorRange - criticalTerrorRange));
                        monsterIntensity = Mathf.Pow(factor, 0.85f);
                    }
                }
            }
        }

        // Pit terror applies ONLY when the active player is actually in the pit (Y < -1.5m)
        // or is the designated fallen player before respawning. Once on the walkway (Y >= -0.5m),
        // pit terror is strictly forced to 0!
        float effectivePitTerror = 0f;
        if (_pitTerrorTarget > 0f && _activePlayerTransform != null)
        {
            if (_activePlayerTransform.position.y < -1.5f || (_pitFallenPlayer != null && _activePlayerTransform == _pitFallenPlayer && _activePlayerTransform.position.y < -0.5f))
            {
                effectivePitTerror = _pitTerrorTarget;
            }
        }

        // Target intensity smoothly takes the maximum of creature pursuit terror and pit fall terror
        _targetIntensity = Mathf.Max(monsterIntensity, effectivePitTerror);
    }

    private void UpdatePostProcessingEffects(float weight)
    {
        if (_terrorVolume == null) return;

        if (weight > 0.005f)
        {
            float pulse = 1f + pulseAmplitude * Mathf.Sin(Time.time * pulseSpeed);

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.value = Mathf.Clamp01(0.85f * (0.85f + 0.15f * pulse));
            }

            if (_filmGrain != null)
            {
                _filmGrain.intensity.value = Mathf.Clamp01(0.65f * (0.9f + 0.1f * pulse));
            }

            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = -45.0f * (0.9f + 0.1f * pulse);
                _colorAdjustments.contrast.value = 35.0f * (0.9f + 0.1f * pulse);
                _colorAdjustments.postExposure.value = -0.35f * (0.9f + 0.1f * pulse);
            }

            if (_bloom != null)
            {
                _bloom.intensity.value = 0.35f * (0.9f + 0.1f * pulse);
            }

            if (_lensDistortion != null)
            {
                _lensDistortion.intensity.value = -0.14f * pulse;
            }

            if (_vignette != null)
            {
                _vignette.intensity.value = Mathf.Clamp01(0.32f * (0.9f + 0.1f * pulse));
                _vignette.smoothness.value = 0.50f;
            }
        }
    }

    private void OnDisable()
    {
        if (_terrorVolume != null)
        {
            _terrorVolume.weight = 0f;
        }
        _currentIntensity = 0f;
    }

    private void OnDestroy()
    {
        if (_terrorProfile != null)
        {
            Destroy(_terrorProfile);
        }
    }
}
