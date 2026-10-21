using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// MultiPlayerCharacterManager - 4-Player Cooperative Body Management
//
// In FLIP//FALL, all 4 player characters physically exist in the game world.
// At spawn (Area 1 Crash Room), all 4 are spaced out on the floor around the
// crumpled elevator cab like they fell and got knocked unconscious.
//
// The local player controls ONE character at a time.
// When switching to another player (F1 - F4 or dev panel):
// - Control & camera transfer to that character's ACTUAL PHYSICAL BODY in the world!
// - The character previously controlled stays standing right where they were!
// - When switching back, you return to where you left that character's body.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/MultiPlayer Character Manager")]
[DisallowMultipleComponent]
public class MultiPlayerCharacterManager : MonoBehaviour
{
    private static MultiPlayerCharacterManager _instance;
    public static MultiPlayerCharacterManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = UnityEngine.Object.FindAnyObjectByType<MultiPlayerCharacterManager>();
            }
            return _instance;
        }
    }

    [Header("Player Characters")]
    [SerializeField] private PlayerCharacter _player1;
    [SerializeField] private PlayerCharacter _player2;
    [SerializeField] private PlayerCharacter _player3;
    [SerializeField] private PlayerCharacter _player4;

    [Header("Spawn Room Positioning (Area 1 Crash Room)")]
    [Tooltip("Enforce spread-out spawn points outside the crashed elevator on startup")]
    [SerializeField] private bool _enforceSpawnPositions = true;
    [SerializeField] private Vector3 _spawnPosP1 = new Vector3(-6.0f, 0.05f, 44.0f);
    [SerializeField] private Vector3 _spawnRotP1 = new Vector3(0f, 45f, 0f);

    [SerializeField] private Vector3 _spawnPosP2 = new Vector3(5.5f, 0.05f, 42.5f);
    [SerializeField] private Vector3 _spawnRotP2 = new Vector3(0f, 325f, 0f);

    [SerializeField] private Vector3 _spawnPosP3 = new Vector3(-5.5f, 0.05f, 57.0f);
    [SerializeField] private Vector3 _spawnRotP3 = new Vector3(0f, 142f, 0f);

    [SerializeField] private Vector3 _spawnPosP4 = new Vector3(6.0f, 0.05f, 56.5f);
    [SerializeField] private Vector3 _spawnRotP4 = new Vector3(0f, 222f, 0f);

    [Header("Camera & Audio")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private AudioListener _audioListener;

    [Header("Initial State")]
    [SerializeField] private PlayerIdentity _startingIdentity = PlayerIdentity.Player1;

    // Fast lookup dictionary
    private readonly Dictionary<PlayerIdentity, PlayerCharacter> _playersById = new Dictionary<PlayerIdentity, PlayerCharacter>();
    private PlayerCharacter _activePlayer;

    public PlayerCharacter ActivePlayer
    {
        get
        {
            if (_activePlayer == null || !_activePlayer.gameObject.activeInHierarchy)
            {
                ResolveActivePlayerFallback();
            }
            return _activePlayer;
        }
    }

    private void ResolveActivePlayerFallback()
    {
        if (_playersById.Count == 0) RegisterPlayers();

        if (_playersById.TryGetValue(PlayerIdentityManager.CurrentIdentity, out PlayerCharacter current) && current != null)
        {
            _activePlayer = current;
            return;
        }

        foreach (var pc in _playersById.Values)
        {
            if (pc != null && pc.IsControlled)
            {
                _activePlayer = pc;
                return;
            }
        }

        if (_playersById.TryGetValue(PlayerIdentity.Player1, out PlayerCharacter p1) && p1 != null)
        {
            _activePlayer = p1;
        }
    }

    public PlayerCharacter Player1 => _player1;
    public PlayerCharacter Player2 => _player2;
    public PlayerCharacter Player3 => _player3;
    public PlayerCharacter Player4 => _player4;
    public Camera MainCamera => _mainCamera;
    public IEnumerable<PlayerCharacter> AllPlayers
    {
        get
        {
            if (_playersById.Count == 0) RegisterPlayers();
            return _playersById.Values;
        }
    }

    private void Awake()
    {
        _instance = this;
        ResolveCamera();
        RegisterPlayers();
        ApplySpawnPositions();
    }

    private void Start()
    {
        // Subscribe to identity changes
        PlayerIdentityManager.OnIdentityChanged += HandleIdentityChanged;

        // Activate starting player body
        SwitchToPlayer(_startingIdentity, force: true);

        // Situation 1: Tutorial Map Wake-Up Narration
        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.TriggerNarration("TUTORIAL_WAKEUP");
        }
    }

    private void OnDestroy()
    {
        PlayerIdentityManager.OnIdentityChanged -= HandleIdentityChanged;
        if (_instance == this) _instance = null;
    }

    private void ResolveCamera()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Camera[] allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (allCams.Length > 0) _mainCamera = allCams[0];
        }

        if (_mainCamera != null && _audioListener == null)
        {
            _audioListener = _mainCamera.GetComponent<AudioListener>();
            if (_audioListener == null) _audioListener = _mainCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    public void RegisterPlayers()
    {
        _playersById.Clear();

        if (_player1 != null) _playersById[PlayerIdentity.Player1] = _player1;
        if (_player2 != null) _playersById[PlayerIdentity.Player2] = _player2;
        if (_player3 != null) _playersById[PlayerIdentity.Player3] = _player3;
        if (_player4 != null) _playersById[PlayerIdentity.Player4] = _player4;

        // Auto-find if not assigned in Inspector
        PlayerCharacter[] found = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerCharacter p in found)
        {
            if (p != null)
            {
                _playersById[p.Identity] = p;
                if (p.Identity == PlayerIdentity.Player1) _player1 = p;
                else if (p.Identity == PlayerIdentity.Player2) _player2 = p;
                else if (p.Identity == PlayerIdentity.Player3) _player3 = p;
                else if (p.Identity == PlayerIdentity.Player4) _player4 = p;
            }
        }
    }

    public void ApplySpawnPositions()
    {
        if (!_enforceSpawnPositions) return;

        PositionPlayer(_player1, _spawnPosP1, _spawnRotP1);
        PositionPlayer(_player2, _spawnPosP2, _spawnRotP2);
        PositionPlayer(_player3, _spawnPosP3, _spawnRotP3);
        PositionPlayer(_player4, _spawnPosP4, _spawnRotP4);
    }

    private void PositionPlayer(PlayerCharacter pc, Vector3 pos, Vector3 euler)
    {
        if (pc == null) return;
        CharacterController cc = pc.CharacterController;
        if (cc != null) cc.enabled = false;
        pc.transform.position = pos;
        pc.transform.rotation = Quaternion.Euler(euler);
        if (cc != null)
        {
            cc.enabled = true;
            cc.Move(Vector3.zero);
        }
        if (pc.FirstPersonController != null)
        {
            pc.FirstPersonController.lastSafePosition = pos;
            pc.FirstPersonController.hasLastSafe = true;
            pc.FirstPersonController.ResetLookAngles(0f, euler.y);
        }
    }

    public PlayerCharacter GetPlayer(PlayerIdentity identity)
    {
        _playersById.TryGetValue(identity, out PlayerCharacter pc);
        return pc;
    }

    private void HandleIdentityChanged(PlayerIdentity newId, PlayerIdentity oldId)
    {
        SwitchToPlayer(newId);
    }

    /// <summary>
    /// Transfers active control and camera to target player character's body in the world.
    /// </summary>
    public void SwitchToPlayer(PlayerIdentity targetId, bool force = false)
    {
        ResolveCamera();
        RegisterPlayers();

        if (!_playersById.TryGetValue(targetId, out PlayerCharacter targetPlayer) || targetPlayer == null)
        {
            Debug.LogWarning($"[MultiPlayerCharacterManager] Cannot switch to {targetId}: Character not found in scene!");
            return;
        }

        if (_activePlayer == targetPlayer && !force) return;

        // Deactivate control on previous player (character remains standing where they are)
        if (_activePlayer != null && _activePlayer != targetPlayer)
        {
            _activePlayer.SetControlled(false);
            Debug.Log($"[MultiPlayerCharacterManager] Relinquished control of {_activePlayer.Identity} at position {_activePlayer.transform.position}");
        }

        // Activate control on new player body
        _activePlayer = targetPlayer;
        _activePlayer.SetControlled(true, _mainCamera, _audioListener);

        if (PlayerIdentityManager.CurrentIdentity != targetId)
        {
            PlayerIdentityManager.SetIdentity(targetId, "Body Transfer");
        }

        // Sanitize camera roll explicitly: prevent any residual terror/suffocation screen sway from bleeding over
        if (_mainCamera != null)
        {
            Vector3 camEuler = _mainCamera.transform.localEulerAngles;
            _mainCamera.transform.localRotation = Quaternion.Euler(camEuler.x, 0f, 0f);
        }

        // Immediately update terror screen effect for the newly active character
        if (MonsterTerrorScreenEffect.Instance != null)
        {
            MonsterTerrorScreenEffect.Instance.OnActivePlayerSwitched(targetPlayer.transform);
        }

        Debug.Log($"[MultiPlayerCharacterManager] Transferred control to {targetId}'s physical body at position {targetPlayer.transform.position}!");
    }


    /// <summary>
    /// Triggers waking up animation on all 4 characters at spawn with organic staggered delays.
    /// Player 1 wakes up first; Players 2, 3, and 4 wake up off-sync so players can witness
    /// teammates stirring and transitioning through the sleep-to-idle animation.
    /// </summary>
    public void TriggerAllWakeUp()
    {
        RegisterPlayers();
        StartCoroutine(StaggeredWakeUpRoutine());
    }

    private IEnumerator StaggeredWakeUpRoutine()
    {
        // Player 1 (initially active) wakes up first
        if (_player1 != null) _player1.PlayWakeUp();

        // Player 2 wakes up 1.5 seconds later (witnessed clearly by Player 1 as vision clears and head rises)
        yield return new WaitForSeconds(1.5f);
        if (_player2 != null) _player2.PlayWakeUp();

        // Player 3 wakes up 1.6 seconds later (at 3.1s)
        yield return new WaitForSeconds(1.6f);
        if (_player3 != null) _player3.PlayWakeUp();

        // Player 4 wakes up 1.6 seconds later (at 4.7s, standing with active flashlight)
        yield return new WaitForSeconds(1.6f);
        if (_player4 != null) _player4.PlayWakeUp();
    }

    /// <summary>
    /// Developer Feature: Teleports all other (inactive) teammates directly to the currently controlled player.
    /// Inactive characters are placed organically behind/beside the active player so they don't overlap.
    /// </summary>
    public void TeleportOtherPlayersToActive()
    {
        RegisterPlayers();
        if (_activePlayer == null) return;

        Vector3 activePos = _activePlayer.transform.position;
        Vector3 activeForward = _activePlayer.transform.forward;
        Vector3 activeRight = _activePlayer.transform.right;

        // Position teammates in a neat semi-circle behind the active player
        Vector3[] offsets = new Vector3[]
        {
            -activeForward * 1.5f - activeRight * 1.2f, // Left rear
            -activeForward * 2.0f,                      // Center rear
            -activeForward * 1.5f + activeRight * 1.2f   // Right rear
        };

        int offsetIdx = 0;
        foreach (var pc in AllPlayers)
        {
            if (pc == null || pc == _activePlayer) continue;

            Vector3 targetPos = activePos + offsets[offsetIdx % offsets.Length];
            targetPos.y = activePos.y; // Match height

            CharacterController cc = pc.CharacterController;
            if (cc != null) cc.enabled = false;

            pc.transform.position = targetPos;
            pc.transform.rotation = _activePlayer.transform.rotation;

            if (cc != null)
            {
                cc.enabled = true;
                cc.Move(Vector3.zero);
            }

            if (pc.FirstPersonController != null)
            {
                pc.FirstPersonController.lastSafePosition = targetPos;
                pc.FirstPersonController.hasLastSafe = true;
                pc.FirstPersonController.ResetLookAngles(0f, _activePlayer.transform.rotation.eulerAngles.y);
            }

            if (pc.Animator != null)
            {
                pc.Animator.Play("Idle", 0, 0f);
            }

            offsetIdx++;
        }

        Debug.Log($"[MultiPlayerCharacterManager] Teleported all teammates to {_activePlayer.Identity} at {activePos}!");
    }
}
