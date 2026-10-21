using System;
using System.Collections;
using UnityEngine;

// =========================================================================
// PlayerWakeUpSequence - Cinematic Waking-Up Intro & Consciousness Fade
//
// At spawn in the Crash Room (Area 1), all 4 players start knocked out on
// the floor around the crashed elevator wreckage.
//
// When the scene begins:
// 1. Screen starts in a dizzy / unconscious fade (darkness + heavy vignette).
// 2. Camera starts low near the floor (simulating lying down).
// 3. All 4 characters play their waking-up animation (Anim_WakeUp_Stand).
// 4. Over ~3.5 seconds, consciousness returns: the dizzy darkness clears,
//    the camera smoothly rises to standing eye height (1.6m), and controls unlock.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Player WakeUp Sequence")]
public class PlayerWakeUpSequence : MonoBehaviour
{
    private static PlayerWakeUpSequence _instance;
    public static PlayerWakeUpSequence Instance => _instance;

    [Header("Timing")]
    [Tooltip("Total duration of the waking up sequence in seconds.")]
    [SerializeField] private float _wakeUpDuration = 2.8f;
    [Tooltip("Delay before starting the wake-up rise.")]
    [SerializeField] private float _initialBlackDelay = 0.0f;

    [Header("Camera Motion")]
    [Tooltip("Initial eye height while knocked out on the floor.")]
    [SerializeField] private float _lyingEyeHeight = 0.38f;
    [Tooltip("Normal standing eye height.")]
    [SerializeField] private float _standingEyeHeight = 1.82f;
    [Tooltip("Initial head tilt roll angle (degrees) while knocked out.")]
    [SerializeField] private float _initialRoll = 14.0f;

    [Header("Visual Fade")]
    [Tooltip("Color of unconscious darkness.")]
    [SerializeField] private Color _fadeColor = new Color(0.015f, 0.02f, 0.035f, 1.0f);

    [Header("Audio")]
    [Tooltip("Gasp/breath or disorientation sound as consciousness returns.")]
    [SerializeField] private AudioClip _wakeUpSound;
    private AudioSource _audioSource;

    // Runtime state
    private bool _isWakingUp = false;
    private float _wakeUpTimer = 0f;
    private float _fadeAlpha = 0.35f;
    private float _dizzyVignette = 0.85f;
    private float _wakeUpFinishedTime = -100f;
    private Texture2D _fadeTexture;
    private Texture2D _vignetteTexture;
    private GUIStyle _hudHintStyle;

    public bool IsWakingUp => _isWakingUp;
    public float FadeAlpha => _fadeAlpha;

    private void Awake()
    {
        _instance = this;
        CreateTextures();
        EnsureAudioSource();
    }

    private void EnsureAudioSource()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.0f;

#if UNITY_EDITOR
        if (_wakeUpSound == null)
        {
            _wakeUpSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Audio/SFX/Player/Player_WakeUp_Breath.wav");
        }
#endif
    }

    private void Start()
    {
        StartWakeUpSequence();
    }

    private void OnDestroy()
    {
        if (_fadeTexture != null) Destroy(_fadeTexture);
        if (_vignetteTexture != null) Destroy(_vignetteTexture);
        if (_instance == this) _instance = null;
    }

    public void StartWakeUpSequence()
    {
        StopAllCoroutines();
        StartCoroutine(WakeUpRoutine());
    }

    private FirstPersonController GetTargetFpc()
    {
        MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
        if (mgr != null && mgr.ActivePlayer != null && mgr.ActivePlayer.FirstPersonController != null)
        {
            return mgr.ActivePlayer.FirstPersonController;
        }
        var p1Obj = GameObject.Find("Player_1");
        if (p1Obj != null)
        {
            var p1Fpc = p1Obj.GetComponent<FirstPersonController>();
            if (p1Fpc != null) return p1Fpc;
        }
        return FindAnyObjectByType<FirstPersonController>();
    }

    private IEnumerator WakeUpRoutine()
    {
        _isWakingUp = true;
        _wakeUpTimer = 0f;
        _fadeAlpha = 0.35f;
        _dizzyVignette = 0.85f;

        MultiPlayerCharacterManager mgr = MultiPlayerCharacterManager.Instance;
        if (mgr != null)
        {
            mgr.TriggerAllWakeUp();
        }

        FirstPersonController fpc = GetTargetFpc();
        if (fpc != null)
        {
            fpc.CanMove = false;
            fpc.ResetLookAngles(0f, 0f);
        }

        EnsureAudioSource();
        if (_wakeUpSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_wakeUpSound, 0.9f);
        }

        // 1. Initial quick transition from floor perspective
        float elapsed = 0f;
        while (elapsed < _initialBlackDelay)
        {
            elapsed += Time.deltaTime;
            if (fpc == null) fpc = GetTargetFpc();
            if (fpc != null && fpc.playerCamera != null)
            {
                fpc.CanMove = false;
                Vector3 localPos = fpc.playerCamera.localPosition;
                localPos.x = 0f;
                localPos.y = _lyingEyeHeight;
                localPos.z = 0.1f;
                fpc.playerCamera.localPosition = localPos;
                fpc.playerCamera.localRotation = Quaternion.Euler(0f, 0f, _initialRoll);
            }
            yield return null;
        }

        // 2. Rising smoothly into consciousness
        float riseDuration = Mathf.Max(0.5f, _wakeUpDuration - _initialBlackDelay);
        elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / riseDuration);

            // Check if player presses any key to stand up immediately
            bool skip = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)
                    skip = true;
            }
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                skip = true;
#else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.C) || Input.GetMouseButtonDown(0))
                skip = true;
#endif
            if (skip) break;

            // Smooth cubic ease out
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            // Vision clears quickly so players immediately see surroundings
            _fadeAlpha = Mathf.Lerp(0.35f, 0.0f, ease);
            _dizzyVignette = Mathf.Lerp(0.85f, 0.0f, ease);

            // Interpolate camera eye height & head swaying into consciousness
            if (fpc == null) fpc = GetTargetFpc();
            if (fpc != null && fpc.playerCamera != null)
            {
                fpc.CanMove = false;
                float currentY = Mathf.Lerp(_lyingEyeHeight, _standingEyeHeight, ease);

                float swayEnvelope = (1f - ease);
                float swayPitch = Mathf.Sin(elapsed * 2.8f) * 3.5f * swayEnvelope;
                float swayYaw = Mathf.Cos(elapsed * 1.9f) * 3.5f * swayEnvelope;
                float swayRoll = Mathf.Sin(elapsed * 2.3f) * 3.0f * swayEnvelope;

                float roll = Mathf.Lerp(_initialRoll, 0f, ease) + swayRoll;

                Vector3 localPos = fpc.playerCamera.localPosition;
                localPos.y = currentY;
                localPos.x = Mathf.Sin(elapsed * 2.1f) * 0.05f * swayEnvelope;
                localPos.z = 0.1f;
                fpc.playerCamera.localPosition = localPos;
                fpc.playerCamera.localRotation = Quaternion.Euler(swayPitch, swayYaw, roll);
            }

            yield return null;
        }

        // 3. Finalize conscious state
        _fadeAlpha = 0.0f;
        _dizzyVignette = 0.0f;
        _isWakingUp = false;
        _wakeUpFinishedTime = Time.time;

        if (fpc == null) fpc = GetTargetFpc();
        if (fpc != null && fpc.playerCamera != null)
        {
            Vector3 localPos = fpc.playerCamera.localPosition;
            localPos.x = 0f;
            localPos.y = _standingEyeHeight;
            localPos.z = 0.1f;
            fpc.playerCamera.localPosition = localPos;

            fpc.ResetLookAngles(0f, 0f);
            fpc.CanMove = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("[PlayerWakeUpSequence] Players fully conscious and standing. Controls unlocked seamlessly!");
    }

    private void CreateTextures()
    {
        if (_fadeTexture == null)
        {
            _fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _fadeTexture.SetPixel(0, 0, Color.white);
            _fadeTexture.Apply();
        }

        if (_vignetteTexture == null)
        {
            int size = 128;
            _vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normDist = Mathf.Clamp01(dist / maxDist);
                    float alpha = Mathf.SmoothStep(0f, 1f, normDist);
                    _vignetteTexture.SetPixel(x, y, new Color(0.01f, 0.015f, 0.025f, alpha));
                }
            }
            _vignetteTexture.Apply();
        }
    }

    private void OnGUI()
    {
        CreateTextures();
        Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);

        // 1. Dizzy Vignette Overlay
        if (_dizzyVignette > 0.01f)
        {
            Color vigColor = GUI.color;
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 3.5f);
            GUI.color = new Color(0.02f, 0.04f, 0.08f, _dizzyVignette * 0.92f * pulse);
            GUI.DrawTexture(screenRect, _vignetteTexture);
            GUI.color = vigColor;
        }

        // 2. Unconscious Darkness Fade
        if (_fadeAlpha > 0.01f)
        {
            Color old = GUI.color;
            GUI.color = new Color(_fadeColor.r, _fadeColor.g, _fadeColor.b, _fadeAlpha);
            GUI.DrawTexture(screenRect, _fadeTexture);
            GUI.color = old;
        }

        // 3. Clear on-screen status feedback for the player
        float timeSinceFinish = Time.time - _wakeUpFinishedTime;
        if (_isWakingUp || timeSinceFinish < 3.5f)
        {
            if (_hudHintStyle == null)
            {
                _hudHintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }

            string msg;
            Color textColor;
            if (_isWakingUp)
            {
                msg = "⚡ Consciousness returning... [Press Space or WASD to stand up immediately]";
                textColor = new Color(0.85f, 0.92f, 1.0f, 0.90f);
            }
            else
            {
                float alpha = Mathf.Clamp01((3.5f - timeSinceFinish) / 1.0f);
                msg = "✓ Controls Ready: [WASD] Jog | [Shift] Run | [C] Toggle Crouch / [Ctrl] Hold | [Space] Jump";
                textColor = new Color(0.35f, 0.95f, 0.55f, alpha * 0.95f);
            }

            _hudHintStyle.normal.textColor = textColor;
            GUI.Label(new Rect(0, Screen.height - 48, Screen.width, 32), msg, _hudHintStyle);
        }
    }
}
