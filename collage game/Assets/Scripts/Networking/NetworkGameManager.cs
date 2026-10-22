using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FlipFall.Networking
{
    /// <summary>
    /// Handles in-game network synchronization for TutorialScene.
    /// Manages player character ownership assignment based on lobby roles,
    /// safe spawn placement in Area 1 Crash Room, and client disconnect handling.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FLIP//FALL/Networking/Network Game Manager")]
    public class NetworkGameManager : NetworkBehaviour
    {
        private static NetworkGameManager _instance;
        public static NetworkGameManager Instance => _instance;

        [Header("Spawn Points (Area 1 Crash Room)")]
        [SerializeField] private Vector3 _spawnPosP1 = new Vector3(-6.0f, 0.05f, 44.0f);
        [SerializeField] private Vector3 _spawnRotP1 = new Vector3(0f, 45f, 0f);

        [SerializeField] private Vector3 _spawnPosP2 = new Vector3(5.5f, 0.05f, 42.5f);
        [SerializeField] private Vector3 _spawnRotP2 = new Vector3(0f, 325f, 0f);

        [SerializeField] private Vector3 _spawnPosP3 = new Vector3(-5.5f, 0.05f, 57.0f);
        [SerializeField] private Vector3 _spawnRotP3 = new Vector3(0f, 142f, 0f);

        [SerializeField] private Vector3 _spawnPosP4 = new Vector3(6.0f, 0.05f, 56.5f);
        [SerializeField] private Vector3 _spawnRotP4 = new Vector3(0f, 222f, 0f);

        // Cached character references
        private readonly Dictionary<PlayerIdentity, PlayerCharacter> _playerCharacters = new Dictionary<PlayerIdentity, PlayerCharacter>();

        private void Awake()
        {
            _instance = this;
            FindPlayerCharacters();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_instance == this) _instance = null;
        }

        public override void OnNetworkSpawn()
        {
            FindPlayerCharacters();

            if (IsServer)
            {
                Debug.Log("[NetworkGameManager] Server spawned in TutorialScene. Assigning character ownerships...");
                AssignPlayerOwnerships();
                PositionAllCharacters();

                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnect;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnect;
            }
        }

        private void FindPlayerCharacters()
        {
            _playerCharacters.Clear();
            PlayerCharacter[] found = FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pc in found)
            {
                if (pc != null)
                {
                    _playerCharacters[pc.Identity] = pc;
                }
            }
        }

        // =====================================================================
        // SERVER: OWNERSHIP & SPAWNING
        // =====================================================================

        /// <summary>
        /// Assigns ownership of the 4 scene PlayerCharacters to the clients according to lobby data.
        /// </summary>
        public void AssignPlayerOwnerships()
        {
            if (!IsServer) return;

            GameLobbyManager lobby = GameLobbyManager.Instance;
            if (lobby == null)
            {
                Debug.LogWarning("[NetworkGameManager] No GameLobbyManager found. Defaulting host to Player 1.");
                return;
            }

            foreach (var playerSlot in lobby.Players)
            {
                ulong targetClient = playerSlot.ClientId;
                PlayerIdentity targetIdentity = playerSlot.Identity;

                if (_playerCharacters.TryGetValue(targetIdentity, out PlayerCharacter character) && character != null)
                {
                    NetworkObject netObj = character.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        if (netObj.OwnerClientId != targetClient)
                        {
                            Debug.Log($"[NetworkGameManager] Transferring ownership of {targetIdentity} to Client {targetClient} ('{playerSlot.PlayerName}')");
                            netObj.ChangeOwnership(targetClient);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enforces initial positions and safe orientations for all characters in Area 1 Crash Room.
        /// </summary>
        public void PositionAllCharacters()
        {
            if (!IsServer) return;

            if (_playerCharacters.TryGetValue(PlayerIdentity.Player1, out PlayerCharacter p1))
                PositionCharacter(p1, _spawnPosP1, _spawnRotP1);

            if (_playerCharacters.TryGetValue(PlayerIdentity.Player2, out PlayerCharacter p2))
                PositionCharacter(p2, _spawnPosP2, _spawnRotP2);

            if (_playerCharacters.TryGetValue(PlayerIdentity.Player3, out PlayerCharacter p3))
                PositionCharacter(p3, _spawnPosP3, _spawnRotP3);

            if (_playerCharacters.TryGetValue(PlayerIdentity.Player4, out PlayerCharacter p4))
                PositionCharacter(p4, _spawnPosP4, _spawnRotP4);
        }

        private void PositionCharacter(PlayerCharacter pc, Vector3 pos, Vector3 euler)
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

        private void HandleServerClientDisconnect(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkGameManager] Client {clientId} disconnected during gameplay.");

            // Find which character was owned by this client and take ownership back to server
            foreach (var kvp in _playerCharacters)
            {
                PlayerCharacter pc = kvp.Value;
                if (pc == null) continue;

                NetworkObject netObj = pc.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned && netObj.OwnerClientId == clientId)
                {
                    Debug.Log($"[NetworkGameManager] Reclaiming server ownership of disconnected character: {kvp.Key}");
                    netObj.ChangeOwnership(NetworkManager.ServerClientId);

                    // Ensure character stops moving and remains standing in place
                    pc.SetControlled(false);
                    if (pc.FirstPersonController != null)
                    {
                        pc.FirstPersonController.IsControlled = false;
                        pc.FirstPersonController.CanMove = false;
                    }
                    if (pc.Animator != null)
                    {
                        pc.Animator.SetFloat("Speed", 0f);
                    }
                    break;
                }
            }
        }
    }
}
