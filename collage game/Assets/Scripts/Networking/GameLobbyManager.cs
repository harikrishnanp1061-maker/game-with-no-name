using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlipFall.Networking
{
    /// <summary>
    /// Replicated lobby state for a single player slot.
    /// </summary>
    public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
    {
        public ulong ClientId;
        public FixedString64Bytes PlayerName;
        public PlayerIdentity Identity; // Player1 = 1, Player2 = 2, Player3 = 3, Player4 = 4
        public bool IsReady;
        public bool IsHost;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref Identity);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsHost);
        }

        public bool Equals(LobbyPlayerData other)
        {
            return ClientId == other.ClientId &&
                   PlayerName.Equals(other.PlayerName) &&
                   Identity == other.Identity &&
                   IsReady == other.IsReady &&
                   IsHost == other.IsHost;
        }

        public override string ToString()
        {
            return $"[{Identity}] {PlayerName} (ClientId={ClientId}, Ready={IsReady}, Host={IsHost})";
        }
    }

    /// <summary>
    /// Manages the 4-player cooperative lobby state across the network.
    /// Host authoritative. Synchronizes player slots, names, identities, and ready states.
    /// Transitions all clients into TutorialScene when the host starts the game.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FLIP//FALL/Networking/Game Lobby Manager")]
    public class GameLobbyManager : NetworkBehaviour
    {
        public const int MaxLobbyPlayers = 4;
        public const string GameplaySceneName = "TutorialScene";
        public const string LobbySceneName = "LobbyScene";

        private static GameLobbyManager _instance;
        public static GameLobbyManager Instance => _instance;

        // Replicated list of players in the lobby (host authoritative)
        private NetworkList<LobbyPlayerData> _players;

        // Local events for UI updates
        public event Action OnLobbyStateChanged;
        public event Action<string> OnLobbyMessage;

        public IReadOnlyList<LobbyPlayerData> Players
        {
            get
            {
                var list = new List<LobbyPlayerData>();
                if (_players != null)
                {
                    for (int i = 0; i < _players.Count; i++)
                    {
                        list.Add(_players[i]);
                    }
                }
                return list;
            }
        }

        public int ConnectedPlayerCount => _players != null ? _players.Count : 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            _players = new NetworkList<LobbyPlayerData>(
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );
        }

        public override void OnNetworkSpawn()
        {
            _players.OnListChanged += HandlePlayersListChanged;

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleServerClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnected;

                // Add Host as Player 1 automatically
                string hostName = NetworkManagerBootstrap.LocalPlayerName;
                if (string.IsNullOrEmpty(hostName)) hostName = "Host (P1)";

                AddPlayerServer(NetworkManager.Singleton.LocalClientId, hostName, isHost: true);
            }

            OnLobbyStateChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (_players != null)
            {
                _players.OnListChanged -= HandlePlayersListChanged;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleServerClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnected;
            }

            OnLobbyStateChanged?.Invoke();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_instance == this) _instance = null;
        }

        private void HandlePlayersListChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
        {
            // Update LAN discovery broadcast player count if host
            if (IsServer && LanDiscovery.Instance != null && LanDiscovery.Instance.IsBroadcasting)
            {
                LanDiscovery.Instance.UpdatePlayerCount(_players.Count);
            }

            OnLobbyStateChanged?.Invoke();
        }

        // =====================================================================
        // SERVER: PLAYER MANAGEMENT
        // =====================================================================

        private void HandleServerClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            // Host was already added on spawn
            if (clientId == NetworkManager.ServerClientId) return;

            Debug.Log($"[GameLobbyManager] Client {clientId} connected to server.");
            // We request client name via RPC or add with default until Client sends their info
            AddPlayerServer(clientId, $"Player {clientId + 1}", isHost: false);
        }

        private void HandleServerClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            Debug.Log($"[GameLobbyManager] Client {clientId} disconnected.");
            RemovePlayerServer(clientId);
        }

        private void AddPlayerServer(ulong clientId, string playerName, bool isHost)
        {
            if (!IsServer) return;

            if (_players.Count >= MaxLobbyPlayers)
            {
                Debug.LogWarning($"[GameLobbyManager] Cannot add client {clientId}: Lobby is full ({MaxLobbyPlayers} players).");
                return;
            }

            // Assign lowest unused PlayerIdentity
            PlayerIdentity assignedIdentity = GetNextAvailableIdentity();

            var data = new LobbyPlayerData
            {
                ClientId = clientId,
                PlayerName = new FixedString64Bytes(playerName),
                Identity = assignedIdentity,
                IsReady = isHost, // Host is ready by default
                IsHost = isHost
            };

            _players.Add(data);
            Debug.Log($"[GameLobbyManager] Added player slot: {data}");
            OnLobbyMessage?.Invoke($"{playerName} joined as {assignedIdentity}.");
        }

        private void RemovePlayerServer(ulong clientId)
        {
            if (!IsServer) return;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    string removedName = _players[i].PlayerName.ToString();
                    PlayerIdentity freedId = _players[i].Identity;
                    _players.RemoveAt(i);
                    Debug.Log($"[GameLobbyManager] Removed {removedName} ({freedId}) from lobby.");
                    OnLobbyMessage?.Invoke($"{removedName} left the game.");
                    break;
                }
            }
        }

        private PlayerIdentity GetNextAvailableIdentity()
        {
            var used = new HashSet<PlayerIdentity>();
            for (int i = 0; i < _players.Count; i++)
            {
                used.Add(_players[i].Identity);
            }

            if (!used.Contains(PlayerIdentity.Player1)) return PlayerIdentity.Player1;
            if (!used.Contains(PlayerIdentity.Player2)) return PlayerIdentity.Player2;
            if (!used.Contains(PlayerIdentity.Player3)) return PlayerIdentity.Player3;
            if (!used.Contains(PlayerIdentity.Player4)) return PlayerIdentity.Player4;

            return PlayerIdentity.Player1;
        }

        // =====================================================================
        // CLIENT / HOST RPCS
        // =====================================================================

        /// <summary>
        /// Called by a newly connected client to set their desired display name.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SetPlayerNameServerRpc(string desiredName, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == senderId)
                {
                    var p = _players[i];
                    p.PlayerName = new FixedString64Bytes(string.IsNullOrEmpty(desiredName) ? $"Player {senderId + 1}" : desiredName);
                    _players[i] = p;
                    Debug.Log($"[GameLobbyManager] Updated name for client {senderId} to '{p.PlayerName}'");
                    break;
                }
            }
        }

        /// <summary>
        /// Called by a client or host to toggle their Ready status.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ToggleReadyServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == senderId)
                {
                    var p = _players[i];
                    p.IsReady = !p.IsReady;
                    _players[i] = p;
                    Debug.Log($"[GameLobbyManager] Client {senderId} ({p.Identity}) ready state changed to: {p.IsReady}");
                    break;
                }
            }
        }

        // =====================================================================
        // GAME START FLOW
        // =====================================================================

        /// <summary>
        /// Checks if all currently connected players are ready to begin.
        /// Requires at least 1 player, and every connected player must have IsReady = true.
        /// </summary>
        public bool CanStartGame()
        {
            if (_players == null || _players.Count == 0) return false;

            for (int i = 0; i < _players.Count; i++)
            {
                if (!_players[i].IsReady) return false;
            }
            return true;
        }

        /// <summary>
        /// Called by the Host when clicking 'Start Game'.
        /// Transitions all connected clients into TutorialScene.
        /// </summary>
        public void StartGame()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[GameLobbyManager] Only the host can start the game.");
                return;
            }

            if (!CanStartGame())
            {
                Debug.LogWarning("[GameLobbyManager] Cannot start game: Not all players are ready!");
                OnLobbyMessage?.Invoke("All connected players must be Ready to start!");
                return;
            }

            // Stop LAN advertising so new clients don't try joining in mid-match
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.StopBroadcasting();
            }

            Debug.Log($"[GameLobbyManager] Host starting game with {_players.Count} players. Transitioning to {GameplaySceneName}...");
            NetworkManager.Singleton.SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        }

        // =====================================================================
        // IDENTITY LOOKUP
        // =====================================================================

        /// <summary>
        /// Finds the assigned PlayerIdentity for a given ClientId.
        /// </summary>
        public bool TryGetIdentityForClient(ulong clientId, out PlayerIdentity identity)
        {
            if (_players != null)
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].ClientId == clientId)
                    {
                        identity = _players[i].Identity;
                        return true;
                    }
                }
            }
            identity = PlayerIdentity.Player1;
            return false;
        }

        /// <summary>
        /// Finds the player name for a given PlayerIdentity.
        /// </summary>
        public string GetPlayerName(PlayerIdentity identity)
        {
            if (_players != null)
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].Identity == identity)
                    {
                        return _players[i].PlayerName.ToString();
                    }
                }
            }
            return identity.ToString();
        }

        /// <summary>
        /// Finds the client ID assigned to a given PlayerIdentity.
        /// </summary>
        public bool TryGetClientForIdentity(PlayerIdentity identity, out ulong clientId)
        {
            if (_players != null)
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    if (_players[i].Identity == identity)
                    {
                        clientId = _players[i].ClientId;
                        return true;
                    }
                }
            }
            clientId = 0;
            return false;
        }
    }
}
