using System.Collections;
using UnityEngine;

// =========================================================================
// PlayerSuffocationPitEffect - Pit Fall Dizziness, Suffocation & Pre-Pit Respawn
//
// When a player falls into any pit in Area 2 (or Area 3):
// 1. Dizziness effect immediately engulfs the screen (heavy pulsating vignette,
//    slight color desaturation, and slow camera sway).
// 2. The player dies slightly slowly over ~2 seconds as if suffocated in the depths.
// 3. Upon suffocation death, the player is respawned just before the pit they
//    fell down on (tutorial level checkpoint).
// 4. Screen clears back from dizziness to conscious and control is restored.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Player Suffocation Pit Effect")]
public class PlayerSuffocationPitEffect : MonoBehaviour
{
    private static PlayerSuffocationPitEffect _instance;
    public static PlayerSuffocationPitEffect Instance => _instance;

    [Header("Suffocation Settings")]
    [Tooltip("Time in seconds to slowly suffocate and die after falling in the pit (matches audio duration from 5s to end = 12.08s).")]
    [SerializeField] private float _suffocationDuration = 12.08f;
    [Tooltip("Time in seconds to fade back in after respawning.")]
    [SerializeField] private float _recoveryDuration = 1.0f;
    [Tooltip("Y threshold below which pit suffocation is automatically triggered.")]
    [SerializeField] private float _pitFallYThreshold = -1.5f;

    [Header("Visual Colors")]
    [SerializeField] private Color _suffocationVignetteColor = new Color(0.02f, 0.01f, 0.04f, 1.0f);
    [SerializeField] private Color _deathBlackoutColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

    [Header("Audio Settings")]
    [Tooltip("Impact thump when falling into the pit (universfield-game-character-fall-131797)")]
    [SerializeField] private AudioClip _fallImpactClip;
    [Tooltip("Suffocation struggle audio (voicebosch-pit-suffocation-death-from25s)")]
    [SerializeField] private AudioClip _suffocationClip;
    [Tooltip("Play from 5th second to end of audio per sfx_annotations.json.")]
    [SerializeField] private float _suffocationAudioStartTime = 5.0f;

    private AudioSource _audioSource;

    // State
    private bool _isSuffocating = false;
    private bool _isRecovering = false;
    private PlayerCharacter _suffocatingPlayer = null;
    private float _suffocateTimer = 0f;
    private float _overlayAlpha = 0f;
    private float _vignetteIntensity = 0f;
    private Vector3 _prePitSafePosition = new Vector3(16.5f, 0.1f, 50f);
    private Quaternion _prePitSafeRotation = Quaternion.Euler(0f, 90f, 0f);
    private bool _hasSafeCheckpoint = false;
    private bool _fellIntoHiddenPit = false;

    private Texture2D _vignetteTex;
    private Texture2D _solidTex;

    public bool IsSuffocating => _isSuffocating;
    public Vector3 PrePitSafePosition => _prePitSafePosition;

    private void Awake()
    {
        _instance = this;
        CreateTextures();
        EnsureAudioSource();
    }

    private void Start()
    {
        EnsureAudioSource();
        // Default safe checkpoint is the entrance walkway of Area 2
        _prePitSafePosition = new Vector3(16.5f, 0.1f, 50f);
        _prePitSafeRotation = Quaternion.Euler(0f, 90f, 0f);
        _hasSafeCheckpoint = true;
    }

    private void EnsureAudioSource()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.0f; // 2D direct into player ears

#if UNITY_EDITOR
        if (_fallImpactClip == null)
        {
            _fallImpactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/SFX/Player/Player_Pit_Fall_Impact.mp3");
            if (_fallImpactClip == null)
            {
                _fallImpactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/SFX/Player/universfield-game-character-fall-131797.mp3");
            }
        }
        if (_suffocationClip == null)
        {
            _suffocationClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/SFX/Player/voicebosch-pit-suffocation-death-from25s.mp3");
            if (_suffocationClip == null)
            {
                _suffocationClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/SFX/Player/Player_Pit_Suffocation_From25s.mp3");
            }
            _suffocationAudioStartTime = 5.0f;
        }
#endif
    }

    private void OnDestroy()
    {
        if (_vignetteTex != null) Destroy(_vignetteTex);
        if (_solidTex != null) Destroy(_solidTex);
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
        PlayerCharacter active = mgr != null ? mgr.ActivePlayer : null;

        if (active == null) return;

        // Track last safe ground position just before pits
        // (Walkway segments are around Y = 0 to 0.5 in Area 2, Z around 50)
        Vector3 pos = active.transform.position;
        CharacterController cc = active.CharacterController;

        if (!_isSuffocating && cc != null && cc.isGrounded && pos.y >= -0.1f)
        {
            // Only update checkpoint if in or approaching the parkour area (X > 12)
            if (pos.x > 12f)
            {
                _prePitSafePosition = pos;
                _prePitSafeRotation = active.transform.rotation;
                _hasSafeCheckpoint = true;
            }
        }

        // Detect falling into pit below threshold
        if (!_isSuffocating && !_isRecovering && pos.y < _pitFallYThreshold)
        {
            TriggerPitSuffocation(null, active.transform);
        }
    }

    /// <summary>
    /// Explicitly triggers pit suffocation from a pit trigger volume or fall detector.
    /// </summary>
    public void TriggerPitSuffocation(Vector3? explicitPrePitCheckpoint = null, Transform victim = null)
    {
        if (_isSuffocating) return;

        Transform fallenTrans = victim;
        if (fallenTrans == null)
        {
            MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
            if (mgr != null && mgr.ActivePlayer != null) fallenTrans = mgr.ActivePlayer.transform;
        }
        if (fallenTrans == null)
        {
            var p1 = UnityEngine.GameObject.Find("Player_1");
            if (p1 != null) fallenTrans = p1.transform;
        }

        Vector3 fallPos = fallenTrans != null ? fallenTrans.position : transform.position;

        // Hidden Pit is Section C (X: 54.0 to 61.5, FloorCover_HiddenPit at X: 57.75)
        _fellIntoHiddenPit = (fallPos.x >= 54.0f && fallPos.x <= 61.5f);

        if (explicitPrePitCheckpoint.HasValue)
        {
            _prePitSafePosition = explicitPrePitCheckpoint.Value;
            _hasSafeCheckpoint = true;
        }

        StartCoroutine(SuffocationRoutine(victim));
    }

    private IEnumerator SuffocationRoutine(Transform victim = null)
    {
        _isSuffocating = true;
        _suffocateTimer = 0f;

        MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
        PlayerCharacter active = mgr != null ? mgr.ActivePlayer : null;
        if (active == null && victim != null)
        {
            active = victim.GetComponent<PlayerCharacter>();
        }
        if (active == null)
        {
            var p1 = UnityEngine.GameObject.Find("Player_1");
            if (p1 != null) active = p1.GetComponent<PlayerCharacter>();
        }
        _suffocatingPlayer = active;
        FirstPersonController fpc = active != null ? active.FirstPersonController : null;

        if (fpc != null)
        {
            fpc.CanMove = true;  // Allow full movement control inside the pit per user request!
            fpc.CanLook = true;  // Retain camera/screen rotation so player can look around
        }

        Debug.Log("[PlayerSuffocationPitEffect] Player fell into pit! Suffocating in the dark... (HiddenPit=" + _fellIntoHiddenPit + ")");

        // Play Fall Impact thump (universfield-game-character-fall-131797)
        if (_fallImpactClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_fallImpactClip, 1.0f);
        }

        // Play Suffocation Heartbeat & Breathing audio (voicebosch-heart-and-breath-suspense-196083)
        if (_suffocationClip != null && _audioSource != null)
        {
            _audioSource.clip = _suffocationClip;
            _audioSource.time = _suffocationAudioStartTime;
            _audioSource.volume = 1.0f;
            _audioSource.loop = false;
            _audioSource.Play();
        }

        // Smoothly ramp in the terror screen effect specifically for the fallen player (only if falling into hidden pit)
        if (_fellIntoHiddenPit && MonsterTerrorScreenEffect.Instance != null && active != null)
        {
            MonsterTerrorScreenEffect.Instance.SetPitTerror(true, 1.0f, active.transform);
        }

        // Trigger Pit Monster Jumpscare ONLY when falling into the hidden pit
        if (_fellIntoHiddenPit && PitMonsterJumpscare.Instance != null && active != null)
        {
            PitMonsterJumpscare.Instance.TriggerPitFall(active.transform);
        }

        // 1. Slow suffocation over duration: dizziness and terror screen effect smoothly creep in
        // Play waking-up animation in reverse (Anim_Death_Collapse) timed so it finishes exactly when the player dies
        float deathAnimDuration = 3.52f; // 4.4s clip played at 1.25x speed
        bool deathAnimTriggered = false;

        float elapsed = 0f;
        while (elapsed < _suffocationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _suffocationDuration);

            // Trigger the reverse wake-up collapse animation only towards the end so it completes exactly at death
            if (!deathAnimTriggered && elapsed >= (_suffocationDuration - deathAnimDuration))
            {
                deathAnimTriggered = true;
                if (fpc != null)
                {
                    fpc.TriggerDeath();
                }
            }

            // Vignette smoothly creeps in from edges towards center
            _vignetteIntensity = Mathf.SmoothStep(0f, 1.0f, t);
            // Blackout only begins after jumpscare completes (~60% into suffocation duration)
            float blackoutT = Mathf.Clamp01((t - 0.55f) / 0.45f);
            _overlayAlpha = Mathf.Pow(blackoutT, 2.0f);

            // Apply suffocation camera sway ONLY if this player is currently the active controlled character
            MultiPlayerCharacterManager curMgr = MultiPlayerCharacterManager.Instance;
            bool isStillActive = (curMgr != null && curMgr.ActivePlayer == active && active != null && active.IsControlled);
            if (isStillActive && fpc != null && fpc.playerCamera != null && fpc.IsControlled)
            {
                float swayZ = Mathf.Cos(Time.time * 3f) * (2f + t * 4f);
                fpc.playerCamera.localRotation = Quaternion.Euler(fpc.VerticalRotation, 0f, swayZ);
            }

            yield return null;
        }

        // 2. Full blackout & Player Death
        _overlayAlpha = 1.0f;
        _vignetteIntensity = 1.0f;

        // Drop fuse cell at exact death position if player held it
        if (active != null && FuseCellPickup.PlayerHasFuseCell)
        {
            FuseCellPickup.DropAtPosition(active.transform.position);
        }

        yield return new WaitForSeconds(0.4f);

        // 3. Respawn just before the pit they fell down on!
        Vector3 targetRespawn = _hasSafeCheckpoint ? _prePitSafePosition : new Vector3(16.5f, 0.2f, 50f);
        targetRespawn.y = Mathf.Max(targetRespawn.y, 0.1f);

        if (active != null)
        {
            CharacterController cc = active.CharacterController;
            if (cc != null) cc.enabled = false;

            active.transform.position = targetRespawn;
            active.transform.rotation = _prePitSafeRotation;

            if (cc != null)
            {
                cc.enabled = true;
                cc.Move(Vector3.zero);
            }

            if (fpc != null && fpc.playerCamera != null)
            {
                fpc.playerCamera.localRotation = Quaternion.Euler(fpc.VerticalRotation, 0f, 0f);
            }

            if (fpc != null)
            {
                fpc.TriggerWakeUp();
            }
        }

        Debug.Log($"[PlayerSuffocationPitEffect] Player respawned before pit at {targetRespawn}!");

        // Situation 2: First Player Pit Fall Narration ONLY after respawning from the HIDDEN pit!
        if (_fellIntoHiddenPit && NarrationManager.Instance != null)
        {
            NarrationManager.Instance.TriggerNarration("FIRST_PIT_FALL");
        }
        _fellIntoHiddenPit = false;

        // Cancel all terror screen distortion immediately upon respawn!
        if (MonsterTerrorScreenEffect.Instance != null)
        {
            MonsterTerrorScreenEffect.Instance.SetPitTerror(false);
            MonsterTerrorScreenEffect.Instance.ResetTerrorImmediately();
        }

        if (PitMonsterJumpscare.Instance != null)
        {
            PitMonsterJumpscare.Instance.ResetToPitHome();
        }

        // 4. Recovering from dizziness to conscious
        _isSuffocating = false;
        _isRecovering = true;
        elapsed = 0f;

        while (elapsed < _recoveryDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _recoveryDuration);

            _overlayAlpha = Mathf.Lerp(1.0f, 0.0f, t);
            _vignetteIntensity = Mathf.Lerp(1.0f, 0.0f, t);
            if (_audioSource != null) _audioSource.volume = Mathf.Lerp(1.0f, 0.0f, t);

            yield return null;
        }

        if (_audioSource != null) _audioSource.Stop();

        _overlayAlpha = 0f;
        _vignetteIntensity = 0f;
        _isRecovering = false;
        _suffocatingPlayer = null;

        if (fpc != null)
        {
            fpc.CanMove = true;
            fpc.CanLook = true;
        }

        Debug.Log("[PlayerSuffocationPitEffect] Fully recovered. Ready to attempt parkour again!");
    }

    private void CreateTextures()
    {
        if (_solidTex == null)
        {
            _solidTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _solidTex.SetPixel(0, 0, Color.white);
            _solidTex.Apply();
        }

        if (_vignetteTex == null)
        {
            int size = 128;
            _vignetteTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normDist = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.SmoothStep(0f, 1f, normDist);
                    _vignetteTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            _vignetteTex.Apply();
        }
    }

    private void OnGUI()
    {
        if (_overlayAlpha <= 0.001f && _vignetteIntensity <= 0.001f) return;

        // Isolate suffocation UI: only render on screen if the active controlled character is the one in the pit
        MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
        PlayerCharacter active = mgr != null ? mgr.ActivePlayer : null;
        if (_suffocatingPlayer != null && active != null && active != _suffocatingPlayer)
        {
            return;
        }

        CreateTextures();
        Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);

        // 1. Suffocating Radial Vignette with Dizzy Pulse
        if (_vignetteIntensity > 0.01f)
        {
            Color old = GUI.color;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            GUI.color = new Color(_suffocationVignetteColor.r, _suffocationVignetteColor.g, _suffocationVignetteColor.b,
                _vignetteIntensity * 0.40f * pulse);
            GUI.DrawTexture(screenRect, _vignetteTex);
            GUI.color = old;
        }

        // 2. Blackout overlay
        if (_overlayAlpha > 0.01f)
        {
            Color old = GUI.color;
            GUI.color = new Color(_deathBlackoutColor.r, _deathBlackoutColor.g, _deathBlackoutColor.b, _overlayAlpha);
            GUI.DrawTexture(screenRect, _solidTex);
            GUI.color = old;
        }

        // 3. Subtle onscreen status during suffocation
        if (_isSuffocating)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.9f, 0.4f, 0.4f, Mathf.PingPong(Time.time * 2f, 0.8f) + 0.2f);
            GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 30), "SUFFOCATING IN THE DEPTHS...", style);
        }
    }
}
