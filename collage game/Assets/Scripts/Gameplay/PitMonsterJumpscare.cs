using System.Collections;
using UnityEngine;

// =========================================================================
// PitMonsterJumpscare - Zoonomaly Monster 8 Pit Trap & Jumpscare Controller
//
// Behavior:
// 1. Resides inside the pit on idle breathing animation.
// 2. When a player falls into the pit:
//    - Monster locks onto the player (smoothly rotates to face them).
//    - Waits 2 seconds while staring with ominous breathing.
//    - Performs the Jumpscare animation forward (non-looping) with a terrifying roar.
//    - Holds the peak end-frame of the jumpscare.
//    - Plays the jumpscare animation in reverse back to resting pose.
//    - Transitions smoothly back to idle breathing.
// 3. Smoothly invokes the terror screen effect when the player falls in.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Pit Monster Jumpscare")]
[DisallowMultipleComponent]
public class PitMonsterJumpscare : MonoBehaviour
{
    private static PitMonsterJumpscare _instance;
    public static PitMonsterJumpscare Instance => _instance;

    public enum JumpscareState
    {
        IdleBreathing,
        LockingOn,
        JumpscareForward,
        HoldingEndFrame,
        Reversing,
        Resetting
    }

    [Header("Current State")]
    [SerializeField] private JumpscareState _state = JumpscareState.IdleBreathing;
    public JumpscareState CurrentState => _state;

    [Header("Timing Configuration")]
    [Tooltip("Delay in seconds between locking onto the player and launching the jumpscare (User requirement: 2 seconds)")]
    public float stareDelayBeforeJumpscare = 2.0f;
    [Tooltip("How long to hold the horrifying peak end-frame before reversing")]
    public float holdEndFrameDuration = 1.0f;
    [Tooltip("Playback speed during the forward jumpscare lunge")]
    public float forwardJumpscareSpeed = 1.0f;
    [Tooltip("Playback speed during the reverse return (negative for reverse playback)")]
    public float reverseJumpscareSpeed = -1.1f;
    [Tooltip("Rotation tracking speed when actively facing the fallen player")]
    public float lockOnRotationSpeed = 8.5f;

    [Header("Audio SFX")]
    [SerializeField] private AudioClip proximityGrowlClip;
    [SerializeField] private AudioClip chaseRoarClip;
    [Tooltip("SFX played once the player falls into the pit chamber (chiiri-monster-15-337349 per annotation)")]
    [SerializeField] public AudioClip pitFallSfxClip;

    [Header("Monster Lighting")]
    [Tooltip("Dedicated point light illuminating Monster 8 in the dark pit")]
    [SerializeField] private Light _monsterFillLight;

    [Header("Pit Home Transform")]
    [SerializeField] private Vector3 _defaultPitPosition = new Vector3(58.8f, -9.75f, 50.0f);
    [SerializeField] private Quaternion _defaultPitRotation = Quaternion.Euler(0f, -90f, 0f);

    // References
    private Animator _animator;
    private AudioSource _audioSource;
    private AudioSource _growlAudioSource;
    private AudioSource _pitFallAudioSource;
    private Transform _targetPlayer;
    private Coroutine _jumpscareRoutine;
    private bool _hasTriggeredThisFall = false;

    private void Awake()
    {
        _instance = this;
        _animator = GetComponent<Animator>();
        EnsureAudio();
        EnsureMonsterLighting();
    }

    private void Start()
    {
        EnsureAudio();
        EnsureMonsterLighting();
        transform.position = _defaultPitPosition;
        transform.rotation = _defaultPitRotation;
        PlayIdle();
    }

    private void EnsureMonsterLighting()
    {
        if (_monsterFillLight == null)
        {
            Transform existing = transform.Find("MonsterPitIlluminationLight");
            if (existing != null)
            {
                _monsterFillLight = existing.GetComponent<Light>();
            }
        }

        if (_monsterFillLight == null)
        {
            GameObject lightObj = new GameObject("MonsterPitIlluminationLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.2f, 1.6f);
            _monsterFillLight = lightObj.AddComponent<Light>();
        }

        _monsterFillLight.type = LightType.Point;
        _monsterFillLight.range = 10.0f;
        _monsterFillLight.intensity = 3.6f;
        _monsterFillLight.color = new Color(1.0f, 0.94f, 0.82f);
        _monsterFillLight.shadows = LightShadows.None;
        _monsterFillLight.renderMode = LightRenderMode.ForcePixel;
    }

    private void EnsureAudio()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 3)
        {
            _audioSource = sources[0];
            _growlAudioSource = sources[1];
            _pitFallAudioSource = sources[2];
        }
        else if (sources.Length == 2)
        {
            _audioSource = sources[0];
            _growlAudioSource = sources[1];
            _pitFallAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            _audioSource = sources[0];
            _growlAudioSource = gameObject.AddComponent<AudioSource>();
            _pitFallAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _growlAudioSource = gameObject.AddComponent<AudioSource>();
            _pitFallAudioSource = gameObject.AddComponent<AudioSource>();
        }

        // Action audio source (jumpscare roar/screech)
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0.5f; // Strong audible punch
        _audioSource.minDistance = 3.0f;
        _audioSource.maxDistance = 40.0f;
        _audioSource.volume = 1.0f;

        // Proximity breathing growl (subtle continuous 3D loop in pit - localized to hidden pit)
        _growlAudioSource.playOnAwake = false;
        _growlAudioSource.loop = true;
        _growlAudioSource.spatialBlend = 1.0f;
        _growlAudioSource.minDistance = 2.0f;
        _growlAudioSource.maxDistance = 9.0f; // Attenuated so normal corridor pits do not hear it
        _growlAudioSource.volume = 0.45f;

        // Dedicated pit fall monster entry voice (chiiri-monster-15-337349)
        _pitFallAudioSource.playOnAwake = false;
        _pitFallAudioSource.loop = false;
        _pitFallAudioSource.spatialBlend = 0.35f; // Rich presence filling the pit chamber
        _pitFallAudioSource.minDistance = 3.5f;
        _pitFallAudioSource.maxDistance = 16.0f;
        _pitFallAudioSource.volume = 1.0f; // Boosted full volume per user annotation

#if UNITY_EDITOR
        if (chaseRoarClip == null)
        {
            chaseRoarClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Chase_Roar.mp3");
            if (chaseRoarClip == null)
                chaseRoarClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/dragon-studio-deep-sea-monster-roar-329857.mp3");
        }
        if (proximityGrowlClip == null)
        {
            proximityGrowlClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Lurking_Proximity.mp3");
            if (proximityGrowlClip == null)
                proximityGrowlClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/alesiadavina-horror-sound-lurking-horror-monster-189948.mp3");
        }
        if (pitFallSfxClip == null)
        {
            pitFallSfxClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/chiiri-monster-15-337349.mp3");
            if (pitFallSfxClip == null)
                pitFallSfxClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Pit_Chiiri_Fall.mp3");
        }
#endif

        if (proximityGrowlClip != null && _growlAudioSource.clip == null)
        {
            _growlAudioSource.clip = proximityGrowlClip;
            _growlAudioSource.Play();
        }

        if (pitFallSfxClip != null && _pitFallAudioSource.clip == null)
        {
            _pitFallAudioSource.clip = pitFallSfxClip;
        }
    }

    public void PlayPitFallAudio()
    {
        EnsureAudio();

        AudioClip clipToPlay = pitFallSfxClip;
#if UNITY_EDITOR
        if (clipToPlay == null)
        {
            clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/chiiri-monster-15-337349.mp3");
            if (clipToPlay == null)
                clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Pit_Chiiri_Fall.mp3");
            pitFallSfxClip = clipToPlay;
        }
#endif

        if (clipToPlay != null)
        {
            if (_pitFallAudioSource != null)
            {
                _pitFallAudioSource.Stop();
                _pitFallAudioSource.clip = clipToPlay;
                _pitFallAudioSource.volume = 1.0f;
                _pitFallAudioSource.Play();
            }
            else if (_audioSource != null)
            {
                _audioSource.PlayOneShot(clipToPlay, 1.0f);
            }
            Debug.Log($"[PitMonsterJumpscare] Playing pit fall monster SFX ({clipToPlay.name}) at boosted volume!");
        }
        else
        {
            Debug.LogWarning("[PitMonsterJumpscare] pitFallSfxClip is null! Could not play pit fall sound.");
        }
    }

    private void Update()
    {
        // Detect player falling into pit corridor
        DetectPitFall();
    }

    private void LateUpdate()
    {
        // Actively track and face the player that fell down in the pit (post-animation evaluation)!
        UpdateFacingFallenPlayer();
    }

    public bool IsInsidePitChamber(Vector3 pos)
    {
        // Pit Monster is exclusively present in the Section C Hidden Pit (X in [54.0, 61.5], Z in [42, 58], Y < -1.5m).
        // Normal corridor pits (Pits 1, 2, 3 with X < 54.0) do NOT contain pit monsters per user specification.
        return (pos.x >= 54.0f && pos.x <= 61.5f && pos.z >= 42f && pos.z <= 58f && pos.y < -1.5f);
    }

    private Transform FindFallenPlayerInPit()
    {
        // Check active player first
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.ActivePlayer != null)
        {
            var ap = MultiPlayerCharacterManager.Instance.ActivePlayer;
            if (ap != null && ap.gameObject.activeInHierarchy && IsInsidePitChamber(ap.transform.position)) return ap.transform;
        }

        // Check all registered players
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.AllPlayers != null)
        {
            foreach (var pc in MultiPlayerCharacterManager.Instance.AllPlayers)
            {
                if (pc != null && pc.gameObject.activeInHierarchy && IsInsidePitChamber(pc.transform.position))
                {
                    return pc.transform;
                }
            }
        }

        // Check FirstPersonControllers
        var controllers = Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude);
        foreach (var c in controllers)
        {
            if (c != null && c.gameObject.activeInHierarchy && IsInsidePitChamber(c.transform.position))
            {
                return c.transform;
            }
        }

        return null;
    }

    private Transform GetActivePlayerTransform()
    {
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.ActivePlayer != null)
        {
            return MultiPlayerCharacterManager.Instance.ActivePlayer.transform;
        }
        var p = GameObject.FindWithTag("Player");
        if (p != null) return p.transform;
        var fps = Object.FindAnyObjectByType<FirstPersonController>();
        return fps != null ? fps.transform : null;
    }

    private void UpdateFacingFallenPlayer()
    {
        if (_targetPlayer == null || !_targetPlayer.gameObject.activeInHierarchy || !IsInsidePitChamber(_targetPlayer.position))
        {
            _targetPlayer = FindFallenPlayerInPit();
        }

        if (_targetPlayer != null && IsInsidePitChamber(_targetPlayer.position))
        {
            Vector3 targetDir = (_targetPlayer.position - transform.position);
            targetDir.y = 0;
            if (targetDir.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(targetDir.normalized);
                float angle = Quaternion.Angle(transform.rotation, lookRot);
                float turnSpeed = Mathf.Max(lockOnRotationSpeed * 15f, angle * lockOnRotationSpeed);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
            }
        }
    }

    private void DetectPitFall()
    {
        Transform fallenPlayer = FindFallenPlayerInPit();

        if (fallenPlayer != null)
        {
            _targetPlayer = fallenPlayer;
            if (!_hasTriggeredThisFall && _state == JumpscareState.IdleBreathing)
            {
                TriggerPitFall(fallenPlayer);
            }
        }
        else
        {
            Transform activePlayer = GetActivePlayerTransform();
            if (activePlayer != null && activePlayer.position.y >= -0.5f)
            {
                if (_hasTriggeredThisFall)
                {
                    ResetToPitHome();
                }
                _hasTriggeredThisFall = false;
            }
        }
    }

    /// <summary>
    /// Explicit trigger called when a player falls into the hidden pit.
    /// </summary>
    public void TriggerPitFall(Transform playerTrans)
    {
        if (playerTrans == null || !IsInsidePitChamber(playerTrans.position))
        {
            // Pit monsters are only present in the hidden pit. Ignore normal pit falls!
            return;
        }

        if (_jumpscareRoutine != null)
        {
            StopCoroutine(_jumpscareRoutine);
        }

        _hasTriggeredThisFall = true;
        _targetPlayer = playerTrans;

        // Position monster strictly in the hidden pit facing the player
        PositionMonsterForFallenPit(playerTrans.position);

        // Smoothly bring in the terror screen effect specifically for the fallen player!
        if (MonsterTerrorScreenEffect.Instance != null)
        {
            MonsterTerrorScreenEffect.Instance.SetPitTerror(true, 1.0f, playerTrans);
        }

        // Play the newly annotated pit fall monster SFX (chiiri-monster-15-337349)
        PlayPitFallAudio();

        _jumpscareRoutine = StartCoroutine(JumpscareSequenceRoutine());
    }

    private void PositionMonsterForFallenPit(Vector3 playerFallPos)
    {
        // Pit Monster is strictly stationed in the Hidden Pit (Section C, default home position)
        transform.position = _defaultPitPosition;

        Vector3 faceDir = (playerFallPos - transform.position);
        faceDir.y = 0;
        if (faceDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(faceDir.normalized);
        }
        else
        {
            transform.rotation = _defaultPitRotation;
        }
    }

    private IEnumerator JumpscareSequenceRoutine()
    {
        // Step 1: Lock on to player and stare for 2 seconds while idle breathing
        _state = JumpscareState.LockingOn;
        PlayIdle();

        float stareTimer = 0f;
        while (stareTimer < stareDelayBeforeJumpscare)
        {
            stareTimer += Time.deltaTime;
            UpdateFacingFallenPlayer();
            yield return null;
        }

        // Step 2: Trigger Jumpscare forward (non-looping)
        _state = JumpscareState.JumpscareForward;
        PlayJumpscareForward();

        // Play sudden terrifying jumpscare sound
        PlayJumpscareAudio();

        // Monitor forward jumpscare animation playback until peak end-frame
        // Monster8_Jumpscare is ~4.13s long
        float forwardElapsed = 0f;
        float expectedForwardDuration = 3.8f; // Peak reach of the lunge

        while (forwardElapsed < expectedForwardDuration)
        {
            forwardElapsed += Time.deltaTime;
            UpdateFacingFallenPlayer();
            yield return null;
        }

        // Step 3: Hold the peak end-frame of the jumpscare
        _state = JumpscareState.HoldingEndFrame;
        HoldJumpscareEndFrame();

        float holdElapsed = 0f;
        while (holdElapsed < holdEndFrameDuration)
        {
            holdElapsed += Time.deltaTime;
            UpdateFacingFallenPlayer();
            yield return null;
        }

        // Step 4: Play jumpscare in REVERSE back to start pose
        _state = JumpscareState.Reversing;
        PlayJumpscareReverse();

        float reverseElapsed = 0f;
        float expectedReverseDuration = expectedForwardDuration / Mathf.Abs(reverseJumpscareSpeed);

        while (reverseElapsed < expectedReverseDuration)
        {
            reverseElapsed += Time.deltaTime;
            UpdateFacingFallenPlayer();
            yield return null;
        }

        // Step 5: Transition back to Idle breathing
        PlayIdle();
        _state = JumpscareState.IdleBreathing;
        _jumpscareRoutine = null;
    }

    private void PlayIdle()
    {
        if (_animator != null)
        {
            _animator.SetBool("IsJumpscaring", false);
            _animator.SetFloat("AnimSpeed", 1.0f);
            _animator.CrossFade("Idle", 0.25f);
        }
    }

    private void PlayJumpscareForward()
    {
        if (_animator != null)
        {
            _animator.SetBool("IsJumpscaring", true);
            _animator.SetFloat("AnimSpeed", forwardJumpscareSpeed);
            _animator.Play("Jumpscare", 0, 0f);
        }
    }

    private void HoldJumpscareEndFrame()
    {
        if (_animator != null)
        {
            _animator.SetFloat("AnimSpeed", 0.0f); // Freeze animation on peak end-frame
        }
    }

    private void PlayJumpscareReverse()
    {
        if (_animator != null)
        {
            _animator.SetFloat("AnimSpeed", reverseJumpscareSpeed); // Reverse playback!
        }
    }

    private void PlayJumpscareAudio()
    {
        if (_audioSource != null)
        {
            // Play deep monstrous roar - attack screech sound effect removed specifically for pit monster
            if (chaseRoarClip != null)
            {
                _audioSource.PlayOneShot(chaseRoarClip, 1.0f);
            }
        }
    }

    public void ResetToPitHome()
    {
        if (_jumpscareRoutine != null)
        {
            StopCoroutine(_jumpscareRoutine);
            _jumpscareRoutine = null;
        }

        if (_pitFallAudioSource != null && _pitFallAudioSource.isPlaying)
        {
            _pitFallAudioSource.Stop();
        }

        transform.position = _defaultPitPosition;
        transform.rotation = _defaultPitRotation;
        PlayIdle();
        _state = JumpscareState.IdleBreathing;
        _hasTriggeredThisFall = false;

        if (MonsterTerrorScreenEffect.Instance != null)
        {
            MonsterTerrorScreenEffect.Instance.SetPitTerror(false);
            MonsterTerrorScreenEffect.Instance.ResetTerrorImmediately();
        }
    }
}
