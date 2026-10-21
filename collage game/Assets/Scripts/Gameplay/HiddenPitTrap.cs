using System;
using UnityEngine;

// =========================================================================
// HiddenPitTrap - detects the player entering a pit volume and hands the
// player back to the existing FlipFallPitReset, or respawns them itself when
// no reset is configured.
//
// Used for the hidden pit in Area02 (dev marker names contain HIDDEN_PIT /
// SecC). The important property for the greybox phase is that the trigger
// region is generated from this object's own bounds when no collider is
// present, so the existing grey gap works without any extra geometry: put
// this on a cube sitting at the lip and set triggerSize, or just let it size
// itself to the cube.
//
// Falling is also counted as a completed tutorial beat, so TutorialGameTracker
// can announce "hidden pit found" the first time it fires.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Hidden Pit Trap")]
[DisallowMultipleComponent]
public class HiddenPitTrap : MonoBehaviour
{
    /// <summary>
    /// Raised whenever the player falls into this pit, whether a reset
    /// consumed them or this component respawned them.
    /// </summary>
    public static event Action<HiddenPitTrap> OnPlayerFell;

    // -----------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------
    [Header("Spawn")]
    [Tooltip("Where the player is put back when no FlipFallPitReset handles the fall.")]
    [SerializeField] private Vector3 _spawnPoint = new Vector3(0f, 1.7f, 3f);
    [Tooltip("Optional explicit spawn transform; wins over spawnPoint when set.")]
    [SerializeField] private Transform _spawnPointOverride;
    [SerializeField] private float _spawnYaw = 0f;

    [Header("Trigger Region")]
    [Tooltip("Generate a BoxCollider trigger from these half-extents when this object has no collider. Sizing the box to the pit lip means no extra geometry is needed.")]
    [SerializeField] private bool _autoCreateTrigger = true;
    [SerializeField] private Vector3 _triggerSize = new Vector3(6f, 4f, 6f);
    [Tooltip("Offset of the generated trigger relative to this transform, usually below the lip.")]
    [SerializeField] private Vector3 _triggerOffset = new Vector3(0f, -1f, 0f);
    [Tooltip("If true the generated trigger also reacts to non-player objects (useful while debugging with a physics ball).")]
    [SerializeField] private bool _detectAnyObject = false;

    [Header("Detection")]
    [Tooltip("Explicit FlipFallPitReset to hand the player to. Left empty it looks for the nearest one in the scene.")]
    [SerializeField] private FlipFallPitReset _pitReset;
    [Tooltip("Also scan for a FlipFallPitReset anywhere in the scene when no explicit one is set.")]
    [SerializeField] private bool _searchForPitReset = true;
    [Tooltip("Safety net: respawn the player when they drop below this world Y. Leave off unless this trap is the only thing covering the map, because with several pits in the scene every trap would fight over the same respawn.")]
    [SerializeField] private bool _enableFallSafetyNet = false;
    [SerializeField] private float _fallYThreshold = -8f;

    [Header("Repeat")]
    [SerializeField] private bool _canReTrigger = true;
    [SerializeField] private float _reTriggerCooldown = 0.4f;

    [Header("Reporting")]
    [SerializeField] private int _fallCount = 0;

    [Header("Tag")]
    [SerializeField] private string _pitTag = "HiddenPit";

    // -----------------------------------------------------------------
    // PUBLIC STATE
    // -----------------------------------------------------------------
    public int FallCount { get { return _fallCount; } }
    public bool CanReTrigger { get { return _canReTrigger; } }

    // -----------------------------------------------------------------
    // PRIVATE STATE
    // -----------------------------------------------------------------
    [SerializeField] private float _lastFallTime = -999f;
    private Collider _triggerCollider;
    private bool _triggerOurs;
    private bool _triggerGenerated;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        if (_canReTrigger && _reTriggerCooldown < 0f) _reTriggerCooldown = 0.4f;
        if (_autoCreateTrigger) EnsureTriggerCollider();
        GameplayUtil.TrySetTag(gameObject, _pitTag);
    }

    private void Start()
    {
        if (_autoCreateTrigger) EnsureTriggerCollider();
    }

    private void Update()
    {
        PlayerSession player = GameplayUtil.FindPlayer();
        if (player == null) return;

        if (!GameplayUtil.IsLocalPlayer(player.Controller.gameObject)) return;

        // Safety net for falls the trigger misses: stepping off an uncollided
        // grey gap, or respawn racing the physics step. Off by default because
        // several pits in one scene would each try to respawn the same player.
        if (_enableFallSafetyNet && player.Position.y < _fallYThreshold)
        {
            RegisterFall();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        if (_detectAnyObject || GameplayUtil.IsLocalPlayer(other.gameObject))
        {
            RegisterFall();
        }
    }

    /// <summary>
    /// Forces a fall event. Used by the dev tools and by tests.
    /// </summary>
    public void TriggerFall()
    {
        RegisterFall();
    }

    // -----------------------------------------------------------------
    // RESPAWN
    // -----------------------------------------------------------------
    private void RegisterFall()
    {
        if (Time.time - _lastFallTime < _reTriggerCooldown) return;

        _lastFallTime = Time.time;
        _fallCount++;

        if (PlayerSuffocationPitEffect.Instance != null)
        {
            Vector3 respawnPos = _spawnPointOverride != null ? _spawnPointOverride.position : _spawnPoint;
            PlayerSuffocationPitEffect.Instance.TriggerPitSuffocation(respawnPos);
            OnPlayerFell?.Invoke(this);
            return;
        }

        FlipFallPitReset reset = ResolvePitReset();
        if (reset != null)
        {
            reset.ResetPlayer();
        }
        else
        {
            RespawnPlayer();
        }

        OnPlayerFell?.Invoke(this);

        Debug.Log("[HiddenPitTrap] Player fell into " + gameObject.name +
            " - " + (reset != null ? "FlipFallPitReset" : "local respawn") +
            "  (fall #" + _fallCount + ")");
    }

    private void RespawnPlayer()
    {
        PlayerSession player = GameplayUtil.FindPlayer();
        if (player == null) return;

        Transform target = player.Controller != null ? player.Controller.transform : null;
        if (target == null) return;

        Vector3 position = _spawnPointOverride != null
            ? _spawnPointOverride.position
            : _spawnPoint;
        target.position = position;
        target.rotation = Quaternion.Euler(0f, _spawnYaw, 0f);

        CharacterController cc = player.Controller != null
            ? player.Controller.GetComponent<CharacterController>()
            : null;
        if (cc != null)
        {
            // CharacterController keeps a stale internal position until it is
            // moved again; syncing it here stops the next Move() snapping back.
            cc.enabled = false;
            cc.enabled = true;
        }
    }

    /// <summary>Re-arms the trap so a fall can be counted again.</summary>
    public void Reset()
    {
        _lastFallTime = -999f;
        _fallCount = 0;
    }

    // -----------------------------------------------------------------
    // LOOKUPS
    // -----------------------------------------------------------------
    private FlipFallPitReset ResolvePitReset()
    {
        if (_pitReset != null) return _pitReset;

        if (!_searchForPitReset) return null;

        FlipFallPitReset[] resets =
            UnityEngine.Object.FindObjectsByType<FlipFallPitReset>(FindObjectsInactive.Include);
        if (resets == null) return null;

        FlipFallPitReset nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < resets.Length; i++)
        {
            if (resets[i] == null) continue;

            float d = Vector3.SqrMagnitude(resets[i].transform.position - transform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = resets[i];
            }
        }

        _pitReset = nearest;
        return _pitReset;
    }

    // -----------------------------------------------------------------
    // TRIGGER GEOMETRY
    // -----------------------------------------------------------------
    /// <summary>
    /// Guarantees a trigger collider exists covering the pit volume. Called
    /// from Awake and Start so it also works when the component is added at
    /// runtime, after the object already has a renderer.
    /// </summary>
    public void EnsureTriggerCollider()
    {
        if (_triggerGenerated) return;
        if (!_autoCreateTrigger) return;

        Collider existing = GetComponent<Collider>();
        if (existing != null)
        {
            _triggerCollider = existing;
            existing.isTrigger = true;
            existing.enabled = true;
            return;
        }

        GameObject holder = new GameObject("HiddenPitTrigger");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = _triggerOffset;
        holder.transform.localRotation = Quaternion.identity;

        BoxCollider box = holder.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.enabled = true;

        Vector3 size = _triggerSize;
        if (size.x <= 0f) size.x = 6f;
        if (size.y <= 0f) size.y = 4f;
        if (size.z <= 0f) size.z = 6f;
        box.size = size;
        box.center = Vector3.zero;

        // Keep the grey gap as-is: the trigger only reports, never blocks.
        holder.layer = 0;

        _triggerCollider = box;
        _triggerOurs = true;
        _triggerGenerated = true;

        if (Application.isPlaying)
        {
            Debug.Log("[HiddenPitTrap] Generated trigger " + size.x + " x " + size.z +
                " at " + holder.transform.position + ".");
        }
    }

    private void OnDestroy()
    {
        if (_triggerOurs && _triggerCollider != null)
        {
            Destroy(_triggerCollider.gameObject);
        }
    }
}
