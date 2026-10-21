using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// NarrationMultiPlayerTrigger - Multi-Player Cooperative Narration Zone
//
// Triggers a narration voice line once a minimum number of player characters
// (e.g. 2 players) have entered this zone.
//
// Used for:
// - Situation 3: At least 2 players cross first hidden passage ("CROSS_FIRST_HIDDEN_DOOR")
// - Situation 4: At least 2 players past monster room hidden wall ("PAST_MONSTER_ROOM")
//
// Includes periodic sweeps over MultiPlayerCharacterManager to seamlessly detect
// players entering via normal locomotion or dev teleportation ([T]).
// =========================================================================
[RequireComponent(typeof(Collider))]
[AddComponentMenu("FLIP//FALL/Gameplay/Narration MultiPlayer Trigger")]
public class NarrationMultiPlayerTrigger : MonoBehaviour
{
    [Header("Narration Configuration")]
    [Tooltip("Unique ID matching a registered narration in NarrationManager.")]
    [SerializeField] private string _narrationId = "CROSS_FIRST_HIDDEN_DOOR";

    [Tooltip("Minimum number of players that must enter/cross the zone to trigger the narration.")]
    [Range(1, 4)]
    [SerializeField] private int _requiredPlayerCount = 2;

    [Tooltip("If true, retains players who have passed through the zone cumulatively rather than requiring them to stand in the zone simultaneously.")]
    [SerializeField] private bool _cumulativeCrossing = true;

    [Tooltip("Only trigger once per game session.")]
    [SerializeField] private bool _triggerOnceOnly = true;

    [Tooltip("Disable collider after successfully triggering.")]
    [SerializeField] private bool _disableColliderOnTriggered = true;

    [Header("Debug Status (Read-Only)")]
    [SerializeField] private int _currentPlayersInZone = 0;
    [SerializeField] private int _currentPlayersCrossed = 0;
    [System.NonSerialized] private bool _hasTriggered = false;

    private readonly HashSet<PlayerCharacter> _playersInZone = new HashSet<PlayerCharacter>();
    private readonly HashSet<PlayerIdentity> _playersWhoCrossed = new HashSet<PlayerIdentity>();
    private Collider _collider;
    private float _nextSweepTime = 0f;
    private const float SWEEP_INTERVAL = 0.2f;

    public string NarrationId => _narrationId;
    public int RequiredPlayerCount => _requiredPlayerCount;
    public int PlayersInZoneCount => _playersInZone.Count;
    public int PlayersCrossedCount => _playersWhoCrossed.Count;
    public bool HasTriggered => _hasTriggered;
    public bool CumulativeCrossing => _cumulativeCrossing;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }

        // Always initialize runtime state cleanly on Awake
        _hasTriggered = false;
        _currentPlayersInZone = 0;
        _currentPlayersCrossed = 0;
        _playersInZone.Clear();
        _playersWhoCrossed.Clear();
    }

    private void Start()
    {
        // If this narration was already played earlier in session, disable immediately
        if (_triggerOnceOnly && NarrationManager.Instance != null && NarrationManager.Instance.HasPlayed(_narrationId))
        {
            _hasTriggered = true;
            if (_disableColliderOnTriggered && _collider != null)
            {
                _collider.enabled = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered && _triggerOnceOnly) return;

        PlayerCharacter pc = other.GetComponentInParent<PlayerCharacter>();
        if (pc != null)
        {
            _playersInZone.Add(pc);
            _playersWhoCrossed.Add(pc.Identity);
            _currentPlayersInZone = _playersInZone.Count;
            _currentPlayersCrossed = _playersWhoCrossed.Count;
            CheckThreshold();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCharacter pc = other.GetComponentInParent<PlayerCharacter>();
        if (pc != null)
        {
            _playersInZone.Remove(pc);
            _currentPlayersInZone = _playersInZone.Count;
            // Note: _playersWhoCrossed is retained when _cumulativeCrossing is true
        }
    }

    private void Update()
    {
        if (_hasTriggered && _triggerOnceOnly) return;

        if (Time.time >= _nextSweepTime)
        {
            _nextSweepTime = Time.time + SWEEP_INTERVAL;
            SweepPlayers();
        }
    }

    /// <summary>
    /// Periodic sweep over all player characters to handle teleportation ([T])
    /// or physics edge-cases.
    /// </summary>
    public void SweepPlayers()
    {
        if (_hasTriggered && _triggerOnceOnly) return;
        if (_collider == null) _collider = GetComponent<Collider>();
        if (_collider == null) return;

        var manager = MultiPlayerCharacterManager.Instance;
        if (manager == null) return;

        Bounds triggerBounds = _collider.bounds;
        bool countChanged = false;

        foreach (PlayerCharacter player in manager.AllPlayers)
        {
            if (player == null) continue;

            bool isInside = triggerBounds.Contains(player.transform.position);
            if (!isInside && player.CharacterController != null)
            {
                isInside = triggerBounds.Intersects(player.CharacterController.bounds);
            }

            if (isInside)
            {
                if (_playersInZone.Add(player)) countChanged = true;
                if (_playersWhoCrossed.Add(player.Identity)) countChanged = true;
            }
            else
            {
                if (_playersInZone.Remove(player)) countChanged = true;
            }
        }

        if (countChanged)
        {
            _currentPlayersInZone = _playersInZone.Count;
            _currentPlayersCrossed = _playersWhoCrossed.Count;
            CheckThreshold();
        }
    }

    private void CheckThreshold()
    {
        if (_hasTriggered && _triggerOnceOnly) return;

        int effectiveCount = _cumulativeCrossing ? _playersWhoCrossed.Count : _playersInZone.Count;

        if (effectiveCount >= _requiredPlayerCount)
        {
            _hasTriggered = true;
            Debug.Log($"[NarrationMultiPlayerTrigger] Required player count reached ({effectiveCount}/{_requiredPlayerCount}, cumulative={_cumulativeCrossing}) for '{_narrationId}'. Firing narration!");

            if (NarrationManager.Instance != null)
            {
                NarrationManager.Instance.TriggerNarration(_narrationId);
            }

            if (_disableColliderOnTriggered && _collider != null)
            {
                _collider.enabled = false;
            }
        }
    }

    public void Configure(string narrationId, int requiredPlayerCount, bool cumulativeCrossing = true)
    {
        _narrationId = narrationId;
        _requiredPlayerCount = Mathf.Clamp(requiredPlayerCount, 1, 4);
        _cumulativeCrossing = cumulativeCrossing;
    }

    public void ResetTrigger()
    {
        _hasTriggered = false;
        _playersInZone.Clear();
        _playersWhoCrossed.Clear();
        _currentPlayersInZone = 0;
        _currentPlayersCrossed = 0;
        if (_collider != null) _collider.enabled = true;
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = _hasTriggered ? new Color(0.3f, 0.9f, 0.3f, 0.25f) : new Color(0.2f, 0.7f, 1.0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = _hasTriggered ? new Color(0.3f, 0.9f, 0.3f, 0.9f) : new Color(0.2f, 0.7f, 1.0f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
