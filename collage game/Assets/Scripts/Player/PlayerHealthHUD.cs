using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =========================================================================
// PlayerHealthHUD - canvas-based health bar bound to a PlayerCombatSystem.
//
// Auto-finds the combat system and the player camera in Awake, so it can be
// added to a canvas without any wiring. The red damage flash fades out over
// time. Press H in play mode to toggle it, F5 to force respawn.
// =========================================================================
[RequireComponent(typeof(Canvas))]
[DisallowMultipleComponent]
public class PlayerHealthHUD : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // AUTO-CONFIG (set from the inspector only if needed)
    // ---------------------------------------------------------------------
    [Header("References")]
    [SerializeField] private PlayerCombatSystem _combatSystem;
    [SerializeField] private Camera _targetCamera;

    [Header("Visual Config")]
    [SerializeField] private Color _fullColor = new Color(0.2f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color _lowColor = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color _emptyColor = new Color(0.35f, 0.05f, 0.05f, 1f);

    [Header("Damage Flash")]
    [SerializeField] private CanvasGroup _damageFlashGroup = null;
    [SerializeField] private float _flashStartAlpha = 0.55f;
    [SerializeField] private float _flashDuration = 0.4f;

    [Header("Text")]
    [SerializeField] private TMPro.TextMeshProUGUI _healthText;
    [SerializeField] private TMPro.TextMeshProUGUI _lowHealthWarning;
    [SerializeField] private float _lowHealthThreshold = 30f;

    [Header("Respawn")]
    [SerializeField] private bool _autoRespawn = false;
    [SerializeField] private float _respawnDelay = 2.0f;
    [SerializeField] private TMPro.TextMeshProUGUI _respawnText;

    // ---------------------------------------------------------------------
    // INTERNAL STATE
    // ---------------------------------------------------------------------
    private Image _healthBarImage;
    private RectTransform _canvasRect;
    private float _flashTimer;
    private Coroutine _respawnRoutine;
    private bool _initialized;

    // ---------------------------------------------------------------------
    // LIFECYCLE
    // ---------------------------------------------------------------------
    private void Awake()
    {
        SetupCanvasAndCamera();
        SetupUIElements();
        SetupReferences();
        BuildDamageFlash();
        _initialized = true;
    }

    private void OnEnable() { SubscribeToCombat(); }

    private void OnDisable()
    {
        UnsubscribeFromCombat();
        if (_respawnRoutine != null) { StopCoroutine(_respawnRoutine); _respawnRoutine = null; }
    }

    private void OnDestroy() { UnsubscribeFromCombat(); }

    private void Update()
    {
        UpdateDamageFlash();
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) ToggleVisibility();
            if (Keyboard.current.f5Key.wasPressedThisFrame && Application.isPlaying) ForceRespawn();
        }
#else
        if (Input.GetKeyDown(KeyCode.H)) ToggleVisibility();
        if (Input.GetKeyDown(KeyCode.F5) && Application.isPlaying) ForceRespawn();
#endif
    }

    // ---------------------------------------------------------------------
    // SETUP
    // ---------------------------------------------------------------------
    private void SetupCanvasAndCamera()
    {
        _canvasRect = GetComponent<RectTransform>();
        if (_canvasRect == null) _canvasRect = GetComponentInParent<RectTransform>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100);

        if (_targetCamera == null) _targetCamera = FindPlayerCamera();
        if (_targetCamera != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            canvas.worldCamera = _targetCamera;
        else if (_targetCamera == null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    private void SetupUIElements()
    {
        // Canvas should fill the screen.
        _canvasRect.anchorMin = Vector2.zero;
        _canvasRect.anchorMax = Vector2.one;
        _canvasRect.offsetMin = Vector2.zero;
        _canvasRect.offsetMax = Vector2.zero;

        // Health bar: bottom-left, 60% of width.
        GameObject barObj = new GameObject("HealthBar");
        barObj.transform.SetParent(transform, false);
        barObj.transform.position = Vector3.zero;
        _healthBarImage = barObj.AddComponent<Image>();
        _healthBarImage.type = Image.Type.Filled;
        _healthBarImage.fillMethod = Image.FillMethod.Horizontal;
        _healthBarImage.fillOrigin = 0; // top -> bar fills left to right
        _healthBarImage.fillAmount = 1f;
        _healthBarImage.color = _fullColor;

        RectTransform barRect = barObj.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.2f, 0.1f);
        barRect.anchorMax = new Vector2(0.8f, 0.15f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;

        // Border behind the bar.
        GameObject borderObj = new GameObject("HealthBorder");
        borderObj.transform.SetParent(transform, false);
        borderObj.transform.position = Vector3.zero;
        Image border = borderObj.AddComponent<Image>();
        border.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        RectTransform bRect = borderObj.GetComponent<RectTransform>();
        bRect.anchorMin = new Vector2(0.2f, 0.1f);
        bRect.anchorMax = new Vector2(0.8f, 0.15f);
        bRect.offsetMin = new Vector2(4f, 4f);
        bRect.offsetMax = new Vector2(-4f, -4f);

        // Health text: bottom-right.
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(transform, false);
        textObj.transform.position = Vector3.zero;
        _healthText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        _healthText.text = "100 / 100";
        _healthText.alignment = TMPro.TextAlignmentOptions.BottomRight;
        _healthText.fontSize = 22;
        _healthText.color = Color.white;

        RectTransform tRect = textObj.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.8f, 0.1f);
        tRect.anchorMax = new Vector2(1f, 0.2f);
        tRect.offsetMin = Vector2.zero;
        tRect.offsetMax = Vector2.zero;

        // Low-health warning: bottom-center.
        GameObject warnObj = new GameObject("LowHealthWarning");
        warnObj.transform.SetParent(transform, false);
        warnObj.transform.position = Vector3.zero;
        _lowHealthWarning = warnObj.AddComponent<TMPro.TextMeshProUGUI>();
        _lowHealthWarning.text = "LOW HEALTH";
        _lowHealthWarning.alignment = TMPro.TextAlignmentOptions.Center;
        _lowHealthWarning.fontSize = 24;
        _lowHealthWarning.color = new Color(1f, 0.2f, 0.1f, 1f);
        _lowHealthWarning.gameObject.SetActive(false);

        RectTransform wRect = warnObj.GetComponent<RectTransform>();
        wRect.anchorMin = new Vector2(0.3f, 0.02f);
        wRect.anchorMax = new Vector2(0.7f, 0.07f);
        wRect.offsetMin = Vector2.zero;
        wRect.offsetMax = Vector2.zero;

        // Respawn text: top-center, hidden until death.
        GameObject respawnObj = new GameObject("RespawnText");
        respawnObj.transform.SetParent(transform, false);
        respawnObj.transform.position = Vector3.zero;
        _respawnText = respawnObj.AddComponent<TMPro.TextMeshProUGUI>();
        _respawnText.text = "You were killed. Respawn in 2s...";
        _respawnText.alignment = TMPro.TextAlignmentOptions.Center;
        _respawnText.fontSize = 28;
        _respawnText.color = new Color(1f, 0.3f, 0.2f, 1f);
        _respawnText.gameObject.SetActive(false);

        RectTransform rRect = respawnObj.GetComponent<RectTransform>();
        rRect.anchorMin = new Vector2(0.2f, 0.85f);
        rRect.anchorMax = new Vector2(0.8f, 0.95f);
        rRect.offsetMin = Vector2.zero;
        rRect.offsetMax = Vector2.zero;
    }

    private void SetupReferences()
    {
        if (_combatSystem == null)
        {
            _combatSystem = FindAnyObjectByType<PlayerCombatSystem>();
            if (_combatSystem == null)
                Debug.LogError("[PlayerHealthHUD] No PlayerCombatSystem found in scene.");
        }
    }

    private void BuildDamageFlash()
    {
        GameObject flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(transform, false);
        flashObj.transform.position = Vector3.zero;

        _damageFlashGroup = flashObj.AddComponent<CanvasGroup>();
        _damageFlashGroup.alpha = 0f;

        Image flashImg = flashObj.AddComponent<Image>();
        flashImg.color = new Color(1f, 0.1f, 0.05f, 1f);

        RectTransform fRect = flashObj.GetComponent<RectTransform>();
        fRect.anchorMin = Vector2.zero;
        fRect.anchorMax = Vector2.one;
        fRect.offsetMin = Vector2.zero;
        fRect.offsetMax = Vector2.zero;

        // Draw first so the flash sits behind the health bar.
        flashObj.transform.SetSiblingIndex(0);
    }

    private Camera FindPlayerCamera()
    {
        Camera cam = Camera.main;
        if (cam != null) return cam;

        var fps = FindAnyObjectByType<FirstPersonController>();
        return fps != null && fps.playerCamera != null ? fps.playerCamera.GetComponent<Camera>() : null;
    }

    // ---------------------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------------------
    private void SubscribeToCombat()
    {
        if (_combatSystem == null) return;
        _combatSystem.OnHealthChanged += OnHealthChanged;
        _combatSystem.OnPlayerDied += OnPlayerDied;
    }

    private void UnsubscribeFromCombat()
    {
        if (_combatSystem == null) return;
        _combatSystem.OnHealthChanged -= OnHealthChanged;
        _combatSystem.OnPlayerDied -= OnPlayerDied;
    }

    private void OnHealthChanged(float current, float max)
    {
        if (!_initialized || _healthBarImage == null) return;

        float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        _healthBarImage.fillAmount = pct;
        _healthBarImage.color = Color.Lerp(_emptyColor, _fullColor, pct);

        if (_healthText != null)
            _healthText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(max);

        if (_lowHealthWarning != null)
            _lowHealthWarning.gameObject.SetActive(current <= _lowHealthThreshold && !(_combatSystem != null && _combatSystem.IsDead));
    }

    private void OnPlayerDied()
    {
        if (_damageFlashGroup != null)
        {
            _damageFlashGroup.alpha = _flashStartAlpha;
            _flashTimer = _flashDuration;
        }

        if (_respawnText != null) _respawnText.gameObject.SetActive(true);
        if (_lowHealthWarning != null) _lowHealthWarning.gameObject.SetActive(false);

        if (_autoRespawn && _respawnRoutine == null)
            _respawnRoutine = StartCoroutine(AutoRespawnRoutine());
    }

    // ---------------------------------------------------------------------
    // FLASH + RESPAWN
    // ---------------------------------------------------------------------
    private void UpdateDamageFlash()
    {
        if (_flashTimer <= 0f || _damageFlashGroup == null) return;
        _flashTimer -= Time.deltaTime;
        _damageFlashGroup.alpha = Mathf.Clamp01(_flashTimer / _flashDuration) * _flashStartAlpha;
        if (_flashTimer <= 0f) _damageFlashGroup.alpha = 0f;
    }

    private IEnumerator AutoRespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);
        ForceRespawn();
    }

    public void ForceRespawn()
    {
        if (_combatSystem == null) return;

        _combatSystem.ResetHealth();

        if (_respawnText != null) _respawnText.gameObject.SetActive(false);
        if (_respawnRoutine != null)
        {
            StopCoroutine(_respawnRoutine);
            _respawnRoutine = null;
        }

        var spawn = GameObject.Find("TutorialScene/TutorialSpawnPoint");
        if (spawn != null)
        {
            var cc = GetComponentInParent<CharacterController>();
            if (cc != null)
            {
                cc.transform.position = spawn.transform.position;
                cc.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }
    }

    public void ToggleVisibility()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = cg.alpha > 0.5f ? 0f : 1f;
        cg.blocksRaycasts = cg.interactable = cg.alpha > 0.5f;
    }
}
