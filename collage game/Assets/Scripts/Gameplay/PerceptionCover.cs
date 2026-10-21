using System;
using UnityEngine;

// =========================================================================
// PerceptionCover - Player-specific environmental perception in FLIP//FALL
//
// In FLIP//FALL, different player identities perceive the environment
// differently (asymmetric co-op perception).
//
// When attached to an object (such as a pit floor cover, a doorway false wall,
// or a hidden crossing bridge):
// - Synchronizes visual rendering (MeshRenderer) with the active player identity.
// - Synchronizes physical collision (Collider) so non-perceiving players do not
//   stand on invisible floors or walk through perceived solid walls.
// - Listens to PlayerIdentityManager.OnIdentityChanged for immediate runtime updates.
// =========================================================================
[ExecuteAlways]
[AddComponentMenu("FLIP//FALL/Gameplay/Perception Cover")]
[DisallowMultipleComponent]
public class PerceptionCover : MonoBehaviour
{
    public enum PerceptionRule
    {
        [Tooltip("Visible and collidable ONLY for the designated target player (hidden from the other 3).")]
        VisibleOnlyToTarget,

        [Tooltip("Hidden and non-collidable for the designated target player (visible and solid for the other 3).")]
        HiddenFromTarget
    }

    // -----------------------------------------------------------------
    // CONFIGURATION
    // -----------------------------------------------------------------
    [Header("Perception Rule")]
    [Tooltip("Whether this object is visible only to the target player, or hidden from them.")]
    [SerializeField] private PerceptionRule _rule = PerceptionRule.HiddenFromTarget;

    [Tooltip("The specific player identity this rule applies to.")]
    [SerializeField] private PlayerIdentity _targetPlayer = PlayerIdentity.Player2;

    [Header("Physics Sync")]
    [Tooltip("If true, colliders are enabled/disabled to match visibility so players do not walk on invisible floors or walk through perceived walls.")]
    [SerializeField] private bool _syncColliders = true;

    [Header("One-Way Transparency (Look Back)")]
    [Tooltip("If true, this cover behaves like a one-way mirror: visible when approaching, but transparent once passed through and looking back.")]
    [SerializeField] private bool _oneWayLookBack = false;

    public enum OneWayAxis
    {
        None,
        /// <summary>Player approaches from smaller X (West), passes through towards larger X (East). When at X > threshold, transparent.</summary>
        PositiveX,
        /// <summary>Player approaches from larger X (East), passes through towards smaller X (West). When at X < threshold, transparent.</summary>
        NegativeX,
        /// <summary>Player approaches from above (Y >= 0). When at Y < dropThreshold, transparent.</summary>
        VerticalDrop
    }

    [Tooltip("Axis the player travels along when passing through this cover.")]
    [SerializeField] private OneWayAxis _passThroughAxis = OneWayAxis.None;

    [Tooltip("World coordinate threshold marking when the player has crossed through to the other side.")]
    [SerializeField] private float _passThroughThreshold = 0f;

    [Tooltip("Optional drop threshold for pits (e.g. fallen into pit Y < -0.5f makes cover transparent from below).")]
    [SerializeField] private float _dropThresholdY = -0.5f;

    [Tooltip("Latched state indicating the player has successfully crossed through to the far side.")]
    [SerializeField] private bool _hasCrossed = false;
    public bool HasCrossed => _hasCrossed;

    public void ResetCrossedState()
    {
        _hasCrossed = false;
        RefreshPerception();
    }

    [Header("Explicit Targets (Optional)")]
    [Tooltip("Leave empty to auto-gather all Renderers on this GameObject and children.")]
    [SerializeField] private Renderer[] _renderers;

    [Tooltip("Leave empty to auto-gather all Colliders on this GameObject and children.")]
    [SerializeField] private Collider[] _colliders;

    // -----------------------------------------------------------------
    // PUBLIC ACCESSORS
    // -----------------------------------------------------------------
    public PerceptionRule Rule
    {
        get => _rule;
        set { _rule = value; RefreshPerception(); }
    }

    public PlayerIdentity TargetPlayer
    {
        get => _targetPlayer;
        set { _targetPlayer = value; RefreshPerception(); }
    }

    public bool SyncColliders
    {
        get => _syncColliders;
        set { _syncColliders = value; RefreshPerception(); }
    }

    public bool OneWayLookBack
    {
        get => _oneWayLookBack;
        set { _oneWayLookBack = value; RefreshPerception(); }
    }

    public OneWayAxis PassThroughAxis
    {
        get => _passThroughAxis;
        set { _passThroughAxis = value; RefreshPerception(); }
    }

    public float PassThroughThreshold
    {
        get => _passThroughThreshold;
        set { _passThroughThreshold = value; RefreshPerception(); }
    }

    public float DropThresholdY
    {
        get => _dropThresholdY;
        set { _dropThresholdY = value; RefreshPerception(); }
    }

    public bool IsCurrentlyVisible { get; private set; }

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        PlayerIdentityManager.OnIdentityChanged += HandleIdentityChanged;
        HiddenDoor.OnOpened += HandleDoorOpened;
        RefreshPerception();
    }

    private void OnDisable()
    {
        PlayerIdentityManager.OnIdentityChanged -= HandleIdentityChanged;
        HiddenDoor.OnOpened -= HandleDoorOpened;
    }

    private void HandleDoorOpened(HiddenDoor door)
    {
        if (door != null && door.gameObject == gameObject)
        {
            RefreshPerception();
        }
    }

    private void Start()
    {
        RefreshPerception();
    }

    private void HandleIdentityChanged(PlayerIdentity newIdentity, PlayerIdentity oldIdentity)
    {
        ApplyPerception(newIdentity);
    }

    public void Configure(PerceptionRule rule, PlayerIdentity targetPlayer, bool syncColliders = true)
    {
        _rule = rule;
        _targetPlayer = targetPlayer;
        _syncColliders = syncColliders;
        CacheComponents();
        RefreshPerception();
    }

    public void ConfigureOneWay(OneWayAxis axis, float threshold, float dropThresholdY = -0.5f)
    {
        _oneWayLookBack = (axis != OneWayAxis.None);
        _passThroughAxis = axis;
        _passThroughThreshold = threshold;
        _dropThresholdY = dropThresholdY;
        RefreshPerception();
    }

    public void CacheComponents()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        if (_colliders == null || _colliders.Length == 0)
        {
            _colliders = GetComponentsInChildren<Collider>(true);
        }
    }

    private Vector3 GetActiveCameraPosition()
    {
        Camera cam = Camera.main;
        if (cam != null) return cam.transform.position;

        var fpc = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        if (fpc != null && fpc.playerCamera != null) return fpc.playerCamera.position;

        return Vector3.zero;
    }

    private Vector3 GetActiveCameraForward()
    {
        Camera cam = Camera.main;
        if (cam != null) return cam.transform.forward;

        var fpc = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        if (fpc != null && fpc.playerCamera != null) return fpc.playerCamera.forward;

        return Vector3.forward;
    }

    public bool EvaluateOneWayPassedThrough(Vector3 camPos, Vector3 camForward)
    {
        // 1. If player has fallen down into a pit, they have seen through from below
        if (camPos.y < _dropThresholdY)
        {
            _hasCrossed = true;
            return true;
        }

        Vector3 center = transform.position;

        switch (_passThroughAxis)
        {
            case OneWayAxis.PositiveX:
            {
                // Has the player reached or passed the far threshold (East side)?
                if (camPos.x >= _passThroughThreshold)
                {
                    _hasCrossed = true;
                }

                if (_hasCrossed)
                {
                    // Latch state: while on the far side or anywhere in the pit crossing,
                    // keep it transparent! Only reset if player fully walks back onto the approach walkway.
                    float returnThreshold = center.x - Mathf.Abs(_passThroughThreshold - center.x) - 0.5f;
                    if (camPos.x < returnThreshold)
                    {
                        _hasCrossed = false;
                        return false;
                    }
                    return true;
                }
                else
                {
                    // Player has not crossed to far side yet.
                    // If camera is on the far half (X > center.x) and looking back West (camForward.x < -0.1f),
                    // they are looking from the other side: transparent!
                    if (camPos.x > center.x && camForward.x < -0.1f)
                    {
                        return true;
                    }
                    return camPos.x >= _passThroughThreshold;
                }
            }

            case OneWayAxis.NegativeX:
            {
                // Approached from East (> center), progresses towards West (< threshold)
                if (camPos.x <= _passThroughThreshold)
                {
                    _hasCrossed = true;
                }

                if (_hasCrossed)
                {
                    float returnThreshold = center.x + Mathf.Abs(center.x - _passThroughThreshold) + 0.5f;
                    if (camPos.x > returnThreshold)
                    {
                        _hasCrossed = false;
                        return false;
                    }
                    return true;
                }
                else
                {
                    if (camPos.x < center.x && camForward.x > 0.1f)
                    {
                        return true;
                    }
                    return camPos.x <= _passThroughThreshold;
                }
            }

            case OneWayAxis.VerticalDrop:
                return (camPos.y < _dropThresholdY);

            default:
                return false;
        }
    }

    private void Update()
    {
        if (!_oneWayLookBack) return;

        Vector3 camPos = GetActiveCameraPosition();
        if (camPos == Vector3.zero) return;
        Vector3 camForward = GetActiveCameraForward();

        bool hasPassedThrough = EvaluateOneWayPassedThrough(camPos, camForward);
        bool baseVisible = ShouldBeVisible(PlayerIdentityManager.CurrentIdentity);
        bool finalVisible = baseVisible && !hasPassedThrough;

        if (finalVisible != IsCurrentlyVisible)
        {
            SetVisibility(finalVisible);
        }
    }

    // -----------------------------------------------------------------
    // PERCEPTION EVALUATION
    // -----------------------------------------------------------------
    /// <summary>
    /// Forces perception evaluation based on the currently active identity.
    /// </summary>
    public void RefreshPerception()
    {
        CacheComponents();
        ApplyPerception(PlayerIdentityManager.CurrentIdentity);
    }

    /// <summary>
    /// Evaluates if this cover should be visible for a specific identity.
    /// </summary>
    public bool ShouldBeVisible(PlayerIdentity identity)
    {
        HiddenDoor door = GetComponent<HiddenDoor>();
        if (door != null && (door.IsOpen || door.CurrentDoorState == HiddenDoor.DoorState.Opening))
        {
            return false;
        }

        bool isTarget = (identity == _targetPlayer);

        switch (_rule)
        {
            case PerceptionRule.VisibleOnlyToTarget:
                return isTarget;

            case PerceptionRule.HiddenFromTarget:
                return !isTarget;

            default:
                return true;
        }
    }

    private void ApplyPerception(PlayerIdentity identity)
    {
        bool baseVisible = ShouldBeVisible(identity);
        bool visible = baseVisible;

        if (_oneWayLookBack)
        {
            Vector3 camPos = GetActiveCameraPosition();
            if (camPos != Vector3.zero)
            {
                Vector3 camForward = GetActiveCameraForward();
                bool hasPassedThrough = EvaluateOneWayPassedThrough(camPos, camForward);
                if (hasPassedThrough) visible = false;
            }
        }

        SetVisibility(visible);
    }

    private void SetVisibility(bool visible)
    {
        IsCurrentlyVisible = visible;

        // 1. Toggle Renderers
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].enabled = visible;
                }
            }
        }

        // 2. Toggle Colliders
        if (_syncColliders && _colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                {
                    _colliders[i].enabled = visible;
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComponents();
        if (!Application.isPlaying)
        {
            ApplyPerception(PlayerIdentityManager.CurrentIdentity);
        }
    }
#endif
}
