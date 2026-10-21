using System;
using System.Collections;
using UnityEngine;

// =========================================================================
// ElevatorObjective - the WORKING ELEVATOR in Area09, the exit / final
// objective.
//
// The elevator does nothing while the building is dark. Once FuseSwitch
// restores power (via its OnPowerRestored event, or SetPowered(true)) the
// elevator becomes "ready" - it pings, lights up and shows an interact
// prompt. Interacting completes the run and raises OnObjectiveComplete.
//
// Attach this to the object named Working_Elevator. All lookups are
// lazy and cached only for the lifetime of the play session.
// =========================================================================
[RequireComponent(typeof(Collider))]
[AddComponentMenu("FLIP//FALL/Gameplay/Elevator Objective")]
[DisallowMultipleComponent]
public class ElevatorObjective : MonoBehaviour
{
    /// <summary>Raised once when the elevator is interacted with after powering up.</summary>
    public static event Action<ElevatorObjective> OnObjectiveComplete;

    /// <summary>Raised when the elevator becomes powered and usable.</summary>
    public static event Action<ElevatorObjective> OnObjectiveReady;

    public enum ElevatorState
    {
        Dark,
        Ready,
        Completing,
        Complete
    }

    // -----------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------
    [Header("Prompt")]
    [SerializeField] private string _promptReady = "STEP INTO THE LIFT";
    [SerializeField] private string _promptDark = "NO POWER";
    [SerializeField] private string _promptDone = "LIFT DEPARTED";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptReadyColor = new Color(0.4f, 0.9f, 1f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptDarkColor = new Color(0.6f, 0.25f, 0.2f, 1f);

    [Header("Range")]
    [SerializeField] private float _interactionDistance = 4.5f;

    [Header("Power")]
    [SerializeField] private string _elevatorObjectName = "Working_Elevator";

    [Header("Arrival Ping")]
    [SerializeField] private float _pingDelay = 0.15f;
    [SerializeField] private float _pingVolume = 1f;
    [SerializeField] private AudioClip _arrivalClip;
    [SerializeField] private AudioClip _completeClip;

    [Header("Lights")]
    [SerializeField] private float _lightRampSpeed = 1.6f;
    [SerializeField] private float _darkIntensity = 0f;
    [SerializeField] private float _poweredIntensity = 2.2f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _poweredLightColor = new Color(0.75f, 0.9f, 1f, 1f);

    [Header("Cabin Floor Highlight")]
    [SerializeField] private Renderer _floorRenderer;
    [ColorUsage(true, true)]
    [SerializeField] private Color _floorReadyColor = new Color(0.3f, 0.8f, 1f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _floorDarkColor = new Color(0.05f, 0.06f, 0.08f, 1f);

    // -----------------------------------------------------------------
    // PUBLIC STATE
    // -----------------------------------------------------------------
    /// <summary>True when the elevator is powered and accepting the player.</summary>
    public bool powered;

    /// <summary>True once the objective has been completed.</summary>
    public bool complete;

    /// <summary>
    /// The elevator is usable: powered, not yet completed, and not mid-
    /// completion. This is the flag the tutorial tracker and HUD read.
    /// </summary>
    public bool ready
    {
        get { return powered && !complete && _state != ElevatorState.Completing; }
    }

    public ElevatorState CurrentElevatorState
    {
        get { return _state; }
    }

    public string PromptText
    {
        get
        {
            if (complete) return _promptDone;
            return powered ? _promptReady : _promptDark;
        }
    }

    public Color PromptColor
    {
        get
        {
            if (powered && !complete) return _promptReadyColor;
            return _promptDarkColor;
        }
    }

    public float InteractionDistance
    {
        get { return _interactionDistance; }
    }

    // -----------------------------------------------------------------
    // PRIVATE STATE
    // -----------------------------------------------------------------
    [SerializeField] private ElevatorState _state = ElevatorState.Dark;
    [SerializeField] private bool _lightRamped;

    private Light[] _lights;
    private float[] _baseLightIntensity;
    private bool[] _baseLightEnabled;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        if (string.IsNullOrEmpty(_elevatorObjectName))
        {
            _elevatorObjectName = gameObject.name;
        }

        CacheLights();
        ApplyVisuals(false);

        if (powered)
        {
            // Left over from a previous run.
            _state = ElevatorState.Ready;
        }

        Debug.Log("[ElevatorObjective] '" + _elevatorObjectName +
            "' attached at " + transform.position + ".");
    }

    private void OnEnable()
    {
        FuseSwitch.OnPowerRestored += HandlePowerRestored;
        FuseSwitch.OnPowerLost += HandlePowerLost;
    }

    private void OnDisable()
    {
        FuseSwitch.OnPowerRestored -= HandlePowerRestored;
        FuseSwitch.OnPowerLost -= HandlePowerLost;
    }

    // -----------------------------------------------------------------
    // POWER
    // -----------------------------------------------------------------
    /// <summary>
    /// External entry point: set the elevator's power state directly. Fires
    /// the ready event when it transitions from dark to powered.
    /// </summary>
    public void SetPowered(bool value)
    {
        if (powered == value) return;

        powered = value;

        if (value && !complete)
        {
            _state = ElevatorState.Ready;
            OnObjectiveReady?.Invoke(this);
            StartCoroutine(ArrivalPing());
            Debug.Log("[ElevatorObjective] Powered - the exit elevator is ready.");
        }
        else if (!value && _state != ElevatorState.Completing && !complete)
        {
            _state = ElevatorState.Dark;
            Debug.Log("[ElevatorObjective] Power lost.");
        }

        ApplyVisuals(value);
    }

    private void HandlePowerRestored(FuseSwitch fuse)
    {
        SetPowered(true);
    }

    private void HandlePowerLost(FuseSwitch fuse)
    {
        SetPowered(false);
    }

    // -----------------------------------------------------------------
    // INTERACTION
    // -----------------------------------------------------------------
    /// <summary>
    /// The elevator only accepts input when it is ready - unpowered
    /// elevators show the prompt but swallow the press, which reads as a
    /// dead objective.
    /// </summary>
    public bool CanInteract(PlayerSession player)
    {
        if (player == null || !player.IsActive) return false;
        if (!ready) return false;

        return Vector3.Distance(player.HeadPosition, transform.position) <= _interactionDistance;
    }

    public bool OnInteract(PlayerSession player)
    {
        if (!ready) return false;
        if (!CanInteract(player)) return false;

        StartCoroutine(CompleteRoutine(player));
        return true;
    }

    private IEnumerator CompleteRoutine(PlayerSession player)
    {
        _state = ElevatorState.Completing;

        if (_completeClip != null && _audio != null)
        {
            _audio.clip = _completeClip;
            _audio.Play();
        }

        if (_pingDelay > 0f) yield return new WaitForSeconds(_pingDelay);

        complete = true;
        _state = ElevatorState.Complete;

        OnObjectiveComplete?.Invoke(this);

        Debug.Log("[ElevatorObjective] Objective complete - " +
            GameplayUtil.SafeName(player) + " reached the exit elevator.");
    }

    /// <summary>
    /// Re-arms the objective so it can be replayed in the solo testing phase.
    /// Call FuseSwitch.Reset() too if you want the whole chain re-done.
    /// </summary>
    public void Reset()
    {
        complete = false;
        _state = powered ? ElevatorState.Ready : ElevatorState.Dark;
        _lightRamped = false;
        ApplyVisuals(powered);
    }

    // -----------------------------------------------------------------
    // AUDIO + VISUALS
    // -----------------------------------------------------------------
    private AudioSource _audio;

    private IEnumerator ArrivalPing()
    {
        if (_arrivalClip != null)
        {
            if (_audio == null) _audio = EnsureAudio();
            if (_audio != null)
            {
                _audio.clip = _arrivalClip;
                _audio.Play();
            }
            else
            {
                Debug.LogWarning("[ElevatorObjective] No arrival clip and no AudioSource available.");
            }
        }

        yield return new WaitForSeconds(_pingDelay);

        StartCoroutine(RampLights());
    }

    private IEnumerator RampLights()
    {
        if (_lights == null || _lights.Length == 0)
        {
            _lightRamped = true;
            yield break;
        }

        _lightRamped = false;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime * _lightRampSpeed);
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) continue;

                if (_poweredIntensity > 0f) l.enabled = true;
                l.intensity = Mathf.Lerp(_darkIntensity, _poweredIntensity, t);
                l.color = Color.Lerp(l.color, _poweredLightColor, t);
            }
            yield return null;
        }

        _lightRamped = true;
    }

    private AudioSource EnsureAudio()
    {
        if (_audio != null) return _audio;

        AudioSource source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = GetComponentInChildren<AudioSource>();
        }

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
        }

        source.playOnAwake = false;
        source.volume = _pingVolume;
        _audio = source;
        return source;
    }

    private void ApplyVisuals(bool isPowered)
    {
        if (_floorRenderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            _floorRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", isPowered ? _floorReadyColor : _floorDarkColor);
            block.SetColor("_Color", isPowered ? _floorReadyColor : _floorDarkColor);
            block.SetColor("_EmissionColor",
                isPowered ? _floorReadyColor * 0.6f : Color.black);
            _floorRenderer.SetPropertyBlock(block);
        }

        if (_lights != null && _lights.Length > 0)
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                Light l = _lights[i];
                if (l == null) continue;

                if (!isPowered)
                {
                    l.intensity = _darkIntensity;
                    if (_darkIntensity <= 0f) l.enabled = false;
                }
                else if (!_lightRamped)
                {
                    l.intensity = _darkIntensity;
                }
                else
                {
                    l.intensity = _poweredIntensity;
                }
            }
        }
    }

    private void CacheLights()
    {
        Light[] lights = GetComponentsInChildren<Light>(false);
        if (lights == null) lights = new Light[0];

        int count = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null) count++;
        }

        _lights = new Light[count];
        _baseLightIntensity = new float[count];
        _baseLightEnabled = new bool[count];

        int n = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            _lights[n] = lights[i];
            _baseLightIntensity[n] = lights[i].intensity;
            _baseLightEnabled[n] = lights[i].enabled;
            n++;
        }
    }

    private void OnDestroy()
    {
        // The audio source follows this GameObject's lifetime, so nothing to
        // release here. Keeping the method explicit so subclasses know they
        // do not need to clean up.
    }
}
