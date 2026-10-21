using System;
using System.Collections;
using UnityEngine;

// =========================================================================
// FuseSwitch - the GREEN FUSE in Area07 (Dark Fuse Room, next to the
// Electrical_Breaker_Cabinet).
//
// Three states:
//     Unlit   -> breaker not thrown, no power going to the exit elevator
//     Tripped -> player has thrown the breaker, mechanical travel in progress
//     Powered -> breaker closed, power confirmed, elevator is live
//
// When power is confirmed the switch looks for the object named
// "Working_Elevator", flips its `powered` bool, and raises the static
// OnPowerRestored event so ElevatorObjective (and any lights or audio that
// want to react) can listen without any inspector wiring.
//
// The switch tries to give itself the tag "Fuse" in Awake. Unity cannot add a
// new tag at runtime in a build, so when the tag is missing this logs a clear
// message instead of failing - see Editor/CreateGameplayTags.cs for the one
// click that adds it.
// =========================================================================
[RequireComponent(typeof(Collider))]
[AddComponentMenu("FLIP//FALL/Gameplay/Fuse Switch")]
[DisallowMultipleComponent]
public class FuseSwitch : MonoBehaviour
{
    public enum FuseState
    {
        Unlit,
        Tripped,
        Powered
    }

    // -----------------------------------------------------------------
    // STATIC EVENTS
    // -----------------------------------------------------------------
    /// <summary>Raised when any fuse restores power.</summary>
    public static event Action<FuseSwitch> OnPowerRestored;

    /// <summary>Raised when a fuse is reset or thrown back to Unlit.</summary>
    public static event Action<FuseSwitch> OnPowerLost;

    // -----------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------
    [Header("Prompt")]
    [SerializeField] private string _promptText = "TRIP FUSE";
    [SerializeField] private string _promptWhenPowered = "FUSE LIFTED";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptColor = new Color(0.45f, 1f, 0.4f, 1f);

    [Header("Range")]
    [SerializeField] private float _interactionDistance = 3.2f;

    [Header("State")]
    [SerializeField] private FuseState _fuseState = FuseState.Unlit;
    [Tooltip("Seconds the breaker takes to travel from Tripped to Powered.")]
    [SerializeField] private float _tripTime = 0.45f;

    [Header("Power Target")]
    [SerializeField] private string _elevatorName = "Working_Elevator";
    [SerializeField] private string _elevatorNameFallback = "Elevator";
    [Tooltip("Name of the bool field toggled on the elevator when no ElevatorObjective is attached.")]
    [SerializeField] private string _poweredFieldName = "powered";
    [Tooltip("Call ElevatorObjective.SetPowered() first, then the bool field as a fallback.")]
    [SerializeField] private bool _preferElevatorObjectiveApi = true;

    [Header("Light Burst")]
    [Tooltip("Light spiked when power is restored. Defaults to any Light on this object or its parents.")]
    [SerializeField] private Light _burstLight;
    [SerializeField] private float _burstIntensityMultiplier = 6f;
    [SerializeField] private float _burstDuration = 0.6f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _burstColor = new Color(0.7f, 1f, 0.5f, 1f);
    [Tooltip("Optional renderer whose emissive colour is boosted during the burst.")]
    [SerializeField] private Renderer _burstRenderer;
    [SerializeField] private string _emissionColorProperty = "_EmissionColor";

    [Header("Interaction Gates")]
    [Tooltip("Requires the player to be carrying nothing. Vacuous in the solo testing phase - override IsHandsFree to plug the real inventory in later.")]
    [SerializeField] private bool _requireEmptyHands = false;

    [Header("Tag")]
    [SerializeField] private string _fuseTag = "Fuse";

    // -----------------------------------------------------------------
    // PUBLIC STATE
    // -----------------------------------------------------------------
    /// <summary>Canonical power flag. Toggled on the elevator as well.</summary>
    public bool powered;

    /// <summary>Read-only alias of <see cref="powered"/>.</summary>
    public bool isPowered { get { return powered; } }

    public FuseState CurrentFuseState { get { return _fuseState; } }

    public string PromptText { get { return powered ? _promptWhenPowered : _promptText; } }
    public Color PromptColor { get { return _promptColor; } }
    public float InteractionDistance { get { return _interactionDistance; } }

    private GameObject _elevator;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        AssignFuseTag();
        ResolveBurstLight();

        if (_fuseState == FuseState.Powered && !powered)
        {
            // Left over from a previous run - bring the elevator into sync
            // quietly so the two never disagree in the editor.
            ApplyPower(true, false);
        }
    }

    // -----------------------------------------------------------------
    // INTERACTION
    // -----------------------------------------------------------------
    /// <summary>
    /// Can this fuse be thrown right now?
    /// </summary>
    public bool CanInteract(PlayerSession player)
    {
        if (player == null || !player.IsActive) return false;
        if (powered) return false;
        if (_fuseState != FuseState.Unlit) return false;
        if (!IsHandsFree(player)) return false;

        return Vector3.Distance(player.HeadPosition, transform.position) <= _interactionDistance;
    }

    /// <summary>Main interaction entry point. Returns true when the press was consumed.</summary>
    public bool OnInteract(PlayerSession player)
    {
        if (!CanInteract(player)) return false;

        StartCoroutine(TripRoutine());
        return true;
    }

    /// <summary>
    /// Gate for "is the player carrying anything". Override to plug a real
    /// inventory in when co-op lands.
    /// </summary>
    protected virtual bool IsHandsFree(PlayerSession player)
    {
        return !_requireEmptyHands;
    }

    private IEnumerator TripRoutine()
    {
        SetFuseState(FuseState.Tripped);
        Debug.Log("[FuseSwitch] Breaker tripped.");

        if (_tripTime > 0f)
        {
            yield return new WaitForSeconds(_tripTime);
        }

        SetFuseState(FuseState.Powered);
        ApplyPower(true, true);
        Debug.Log("[FuseSwitch] Power restored - the exit elevator should be live.");
    }

    /// <summary>
    /// External entry point for editor tooling, a dev console, or save/load:
    /// sets power directly, skipping the trip animation but still firing the
    /// events and applying the light burst.
    /// </summary>
    public void SetPowered(bool value)
    {
        if (powered == value) return;

        if (value)
        {
            SetFuseState(FuseState.Powered);
        }
        else if (_fuseState != FuseState.Tripped)
        {
            SetFuseState(FuseState.Unlit);
        }

        ApplyPower(value, true);
    }

    /// <summary>
    /// Re-arms the fuse so the beat can be replayed. Used by the dev reset
    /// key and by TutorialGameTracker.ResetAll().
    /// </summary>
    public void Reset()
    {
        SetFuseState(FuseState.Unlit);
        ApplyPower(false, true);
        Debug.Log("[FuseSwitch] Reset to Unlit.");
    }

    private void SetFuseState(FuseState state)
    {
        _fuseState = state;
    }

    // -----------------------------------------------------------------
    // POWER PROPAGATION
    // -----------------------------------------------------------------
    private void ApplyPower(bool value, bool announce)
    {
        powered = value;

        if (_elevator == null) _elevator = GameplayUtil.FindByNameContains(
            _elevatorName, _elevatorNameFallback);

        if (_elevator != null)
        {
            if (_preferElevatorObjectiveApi)
            {
                ElevatorObjective objective =
                    _elevator.GetComponentInChildren<ElevatorObjective>();
                if (objective != null)
                {
                    objective.SetPowered(value);
                }
            }

            GameplayUtil.SetBoolField(_elevator, _poweredFieldName, value);
        }
        else
        {
            Debug.LogWarning("[FuseSwitch] No object matching '" + _elevatorName +
                "' / '" + _elevatorNameFallback +
                "' found - power event raised but no elevator was toggled.");
        }

        if (!announce) return;

        if (value)
        {
            if (_burstLight != null) StartCoroutine(LightBurst());
            OnPowerRestored?.Invoke(this);
        }
        else
        {
            OnPowerLost?.Invoke(this);
        }
    }

    private IEnumerator LightBurst()
    {
        if (_burstLight == null) yield break;

        if (_burstLight.intensity <= 0f) _burstLight.enabled = true;

        float baseIntensity = Mathf.Max(0.01f,
            _burstLight.intensity / Mathf.Max(0.01f, _burstIntensityMultiplier));
        Color baseColor = _burstLight.color;

        _burstLight.color = _burstColor;
        _burstLight.intensity = baseIntensity * _burstIntensityMultiplier;

        if (_burstRenderer != null) SetEmission(_burstRenderer, _burstColor * 4f);

        float elapsed = 0f;
        while (elapsed < _burstDuration)
        {
            elapsed += Time.deltaTime;
            float falloff = Mathf.Pow(1f - Mathf.Clamp01(elapsed / _burstDuration), 2f);
            _burstLight.intensity = Mathf.Lerp(baseIntensity,
                baseIntensity * _burstIntensityMultiplier, falloff);
            yield return null;
        }

        _burstLight.color = baseColor;
        _burstLight.intensity = baseIntensity;

        if (_burstRenderer != null) SetEmission(_burstRenderer, _burstColor * 0.1f);
    }

    private void SetEmission(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(_emissionColorProperty, color);
        renderer.SetPropertyBlock(block);
    }

    // -----------------------------------------------------------------
    // LOOKUPS
    // -----------------------------------------------------------------
    private void ResolveBurstLight()
    {
        if (_burstLight != null) return;

        Light own = GetComponent<Light>();
        if (own != null)
        {
            _burstLight = own;
            return;
        }

        Transform t = transform.parent;
        int guard = 0;
        while (t != null && guard < 6)
        {
            own = t.GetComponent<Light>();
            if (own != null)
            {
                _burstLight = own;
                return;
            }
            t = t.parent;
            guard++;
        }
    }

    /// <summary>
    /// Applies the "Fuse" tag when it exists. Unity cannot create a tag at
    /// runtime in a build, so a missing tag is reported rather than fatal.
    /// </summary>
    private void AssignFuseTag()
    {
        if (string.IsNullOrEmpty(_fuseTag)) return;
        if (gameObject.tag == _fuseTag) return;

        if (GameplayUtil.TrySetTag(gameObject, _fuseTag)) return;

        Debug.LogWarning("[FuseSwitch] Tag '" + _fuseTag +
            "' does not exist, so it was not applied. Run " +
            "Tools > FLIP//FALL > Create Gameplay Tags, or add it in " +
            "Project Settings > Tags and Layers. The fuse still works; tag " +
            "filtering is optional.");
    }
}
