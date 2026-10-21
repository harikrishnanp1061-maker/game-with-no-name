using System.Collections;
using UnityEngine;

// =========================================================================
// AtmosphereAudioManager - 6-Minute Monster Absence Horror Atmosphere Drone
//
// Rules:
// 1. If a player does not encounter a monster for 6 minutes (360s),
//    play the atmospheric horror drone SFX ONCE (strictly non-looping).
// 2. After that initial 6-minute trigger, repeat the SFX ONCE every 2 minutes (120s)
//    until a monster encounter occurs for the player.
// 3. The moment a monster encounter occurs for the player (proximity to Entity 38,
//    monster agitation/chase/attack, or pit jumpscare/terror), the audio immediately
//    fades out and the cycle timer completely resets (requiring another 6 minutes
//    of absence before any SFX plays again).
// =========================================================================
[AddComponentMenu("FLIP//FALL/Audio/Atmosphere Audio Manager")]
[DisallowMultipleComponent]
public class AtmosphereAudioManager : MonoBehaviour
{
    private static AtmosphereAudioManager _instance;
    public static AtmosphereAudioManager Instance => _instance;

    [Header("Audio Clip")]
    [Tooltip("Disturbing demonic atmosphere horror drone SFX (alesiadavina-disturbing-demonic-atmosphere-horror-drone-sfx-542636)")]
    [SerializeField] private AudioClip _demonicDroneClip;

    [Header("Encounter Timing")]
    [Tooltip("Time in seconds without monster encounter before first atmospheric horror SFX plays (6 minutes = 360s).")]
    [SerializeField] private float _tensionThresholdSeconds = 360.0f;

    [Tooltip("Interval in seconds between subsequent atmospheric horror SFX plays after the initial trigger (2 minutes = 120s).")]
    [SerializeField] private float _repeatIntervalSeconds = 120.0f;

    [Tooltip("Maximum volume for the ambient horror drone.")]
    [Range(0f, 1f)]
    [SerializeField] private float _targetVolume = 0.75f;

    [Tooltip("Fade out duration in seconds when monster is encountered.")]
    [SerializeField] private float _fadeOutDuration = 0.5f;

    [Tooltip("Distance in meters within which a player is considered to have encountered the monster.")]
    [SerializeField] private float _monsterEncounterDistance = 18.0f;

    [Header("Runtime State & Debug Info")]
    [SerializeField] private float _timeSinceLastEncounter = 0f;
    [SerializeField] private float _timeSinceLastPlay = 0f;
    [SerializeField] private float _timeUntilNextPlay = 360.0f;
    [SerializeField] private bool _initialPlayTriggered = false;
    [SerializeField] private bool _isAudioCurrentlyPlaying = false;
    [SerializeField] private bool _isMonsterCurrentlyEncountered = false;
    [SerializeField] private bool _forceTestPlayOnce = false;

    private AudioSource _audioSource;
    private Coroutine _fadeOutCoroutine;

    public float TimeSinceLastEncounter => _timeSinceLastEncounter;
    public float TimeUntilNextPlay => _timeUntilNextPlay;
    public bool InitialPlayTriggered => _initialPlayTriggered;
    public bool IsAudioPlaying => (_audioSource != null && _audioSource.isPlaying);

    private void Awake()
    {
        _instance = this;

        // Ensure 6-minute (360s) threshold and 2-minute (120s) interval even if old scene values were 300s
        if (_tensionThresholdSeconds <= 300.0f) _tensionThresholdSeconds = 360.0f;
        if (_repeatIntervalSeconds <= 0f) _repeatIntervalSeconds = 120.0f;
        if (_monsterEncounterDistance > 25.0f || _monsterEncounterDistance < 10.0f) _monsterEncounterDistance = 18.0f;

        EnsureAudioSource();
        ResetCycle();
    }

    private void Start()
    {
        EnsureAudioSource();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void EnsureAudioSource()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.loop = false; // STRICTLY NON-LOOPING: play once, then silence until next trigger
        _audioSource.spatialBlend = 0.0f; // 2D full immersion
        _audioSource.volume = _targetVolume;

        if (_demonicDroneClip == null)
        {
#if UNITY_EDITOR
            _demonicDroneClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/SFX/Atmosphere/Atmosphere_Demonic_Drone_5Min.mp3");
            if (_demonicDroneClip == null)
            {
                _demonicDroneClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/SFX/Atmosphere/alesiadavina-disturbing-demonic-atmosphere-horror-drone-sfx-542636.mp3");
            }
#endif
        }

        if (_demonicDroneClip != null)
        {
            _audioSource.clip = _demonicDroneClip;
        }
    }

    private void Update()
    {
        EnsureAudioSource();
        _isAudioCurrentlyPlaying = (_audioSource != null && _audioSource.isPlaying);

        // Check if player is currently in an encounter with any monster
        bool encounterActive = CheckIfPlayerEncounteringMonster();
        _isMonsterCurrentlyEncountered = encounterActive;

        if (encounterActive)
        {
            // Monster encountered! If SFX is playing, immediately fade out and stop
            if (_audioSource != null && _audioSource.isPlaying)
            {
                FadeOutAudioImmediately();
                Debug.Log("[AtmosphereAudioManager] Monster encountered by player! Stopped atmosphere SFX.");
            }

            // Reset the entire cycle
            _timeSinceLastEncounter = 0f;
            _initialPlayTriggered = false;
            _timeSinceLastPlay = 0f;
            _timeUntilNextPlay = _tensionThresholdSeconds;
        }
        else
        {
            // No monster encountered: accumulate elapsed absence time
            _timeSinceLastEncounter += Time.deltaTime;

            if (!_initialPlayTriggered)
            {
                // Stage 1: Waiting for 6 minutes (360 seconds) without an encounter
                _timeUntilNextPlay = Mathf.Max(0f, _tensionThresholdSeconds - _timeSinceLastEncounter);

                if (_timeSinceLastEncounter >= _tensionThresholdSeconds)
                {
                    // Trigger ONCE at 6 minutes!
                    _initialPlayTriggered = true;
                    _timeSinceLastPlay = 0f;
                    _timeUntilNextPlay = _repeatIntervalSeconds;
                    PlaySfxOnce();
                    Debug.Log("[AtmosphereAudioManager] 6 minutes without monster encounter! Playing atmospheric horror SFX ONCE.");
                }
            }
            else
            {
                // Stage 2: After initial play, repeat ONCE every 2 minutes (120 seconds)
                _timeSinceLastPlay += Time.deltaTime;
                _timeUntilNextPlay = Mathf.Max(0f, _repeatIntervalSeconds - _timeSinceLastPlay);

                if (_timeSinceLastPlay >= _repeatIntervalSeconds)
                {
                    // Trigger ONCE every 2 minutes!
                    _timeSinceLastPlay = 0f;
                    _timeUntilNextPlay = _repeatIntervalSeconds;
                    PlaySfxOnce();
                    Debug.Log("[AtmosphereAudioManager] 2 minutes elapsed since last play with no monster encounter. Playing atmospheric horror SFX ONCE.");
                }
            }
        }

        // Test play inspector toggle
        if (_forceTestPlayOnce)
        {
            _forceTestPlayOnce = false;
            TestPlayOnce();
        }

        // F9 Dev Shortcut: Force test play SFX once
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
        {
            TestPlayOnce();
        }
#else
        if (Input.GetKeyDown(KeyCode.F9))
        {
            TestPlayOnce();
        }
#endif
    }

    private bool CheckIfPlayerEncounteringMonster()
    {
        // 1. Locate active player
        Transform activePlayerTrans = null;
        MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
        if (mgr != null && mgr.ActivePlayer != null)
        {
            activePlayerTrans = mgr.ActivePlayer.transform;
        }
        else
        {
            GameObject pObj = GameObject.FindWithTag("Player");
            if (pObj != null) activePlayerTrans = pObj.transform;
        }

        if (activePlayerTrans == null) return false;

        // 2. Check Entity 38 monster
        GameObject monsterObj = GameObject.Find("Entity38_Needlelimbs");
        if (monsterObj != null && monsterObj.activeInHierarchy)
        {
            float dist = Vector3.Distance(activePlayerTrans.position, monsterObj.transform.position);

            // Proximity encounter (inside room / near creature)
            if (dist <= _monsterEncounterDistance)
            {
                return true;
            }

            Entity38EnemyAI monsterAI = monsterObj.GetComponent<Entity38EnemyAI>();
            if (monsterAI != null)
            {
                // Active pursuit or attack targeting the player or close by
                if (monsterAI.IsAgitated && (monsterAI.TargetPlayer == activePlayerTrans || dist <= monsterAI.agitationRange + 2.0f))
                {
                    return true;
                }
            }
        }

        // 3. Check Pit Monster / jumpscare / terror effect
        if (MonsterTerrorScreenEffect.Instance != null && MonsterTerrorScreenEffect.Instance.IsTerrorActive)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Plays the atmospheric horror drone SFX ONCE without looping.
    /// </summary>
    public void PlaySfxOnce()
    {
        if (_fadeOutCoroutine != null)
        {
            StopCoroutine(_fadeOutCoroutine);
            _fadeOutCoroutine = null;
        }

        if (_demonicDroneClip == null) EnsureAudioSource();
        if (_demonicDroneClip == null || _audioSource == null) return;

        // Strictly non-looping 1-shot
        _audioSource.loop = false;
        _audioSource.clip = _demonicDroneClip;
        _audioSource.volume = _targetVolume;
        _audioSource.Play();
    }

    /// <summary>
    /// Smoothly fades out and stops the audio when a monster encounter begins.
    /// </summary>
    public void FadeOutAudioImmediately()
    {
        if (_audioSource == null || !_audioSource.isPlaying) return;

        if (_fadeOutCoroutine != null) StopCoroutine(_fadeOutCoroutine);
        _fadeOutCoroutine = StartCoroutine(FadeOutRoutine(_fadeOutDuration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVol = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_audioSource != null)
            {
                _audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
            }
            yield return null;
        }

        if (_audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.volume = _targetVolume;
        }
        _fadeOutCoroutine = null;
    }

    /// <summary>
    /// Explicitly resets the cycle timer back to 0 (e.g. from an external trigger or event).
    /// </summary>
    public void ResetCycle()
    {
        _timeSinceLastEncounter = 0f;
        _initialPlayTriggered = false;
        _timeSinceLastPlay = 0f;
        _timeUntilNextPlay = _tensionThresholdSeconds;
        FadeOutAudioImmediately();
    }

    /// <summary>
    /// Dev shortcut (F9 or ContextMenu) to test 1-shot playback.
    /// </summary>
    [ContextMenu("Test Play SFX Once (F9)")]
    public void TestPlayOnce()
    {
        Debug.Log("[AtmosphereAudioManager] Manually triggering 1-shot atmosphere SFX...");
        PlaySfxOnce();
    }

    /// <summary>
    /// Dev shortcut to test encounter reset.
    /// </summary>
    [ContextMenu("Simulate Encounter Reset")]
    public void SimulateEncounterReset()
    {
        Debug.Log("[AtmosphereAudioManager] Manually simulating monster encounter reset.");
        ResetCycle();
    }
}
