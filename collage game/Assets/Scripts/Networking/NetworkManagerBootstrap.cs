using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlipFall.Networking
{
    /// <summary>
    /// Central bootstrap and lifecycle controller for Unity Netcode and UnityTransport.
    /// Manages host setup, client connection, transport configuration, and graceful disconnects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(UnityTransport))]
    [AddComponentMenu("FLIP//FALL/Networking/Network Manager Bootstrap")]
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        public const ushort DefaultGamePort = 7777;
        private const string PrefsPlayerNameKey = "FlipFall_PlayerName";

        private static NetworkManagerBootstrap _instance;
        public static NetworkManagerBootstrap Instance => _instance;

        [Header("References")]
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _transport;
        [SerializeField] private LanDiscovery _lanDiscovery;
        [SerializeField] private GameObject _lobbyManagerPrefab;

        [Header("Configuration")]
        [SerializeField] private ushort _gamePort = DefaultGamePort;

        // Runtime state
        private static string _localPlayerName = "";
        public static string LocalPlayerName
        {
            get
            {
                if (string.IsNullOrEmpty(_localPlayerName))
                {
                    _localPlayerName = PlayerPrefs.GetString(PrefsPlayerNameKey, Environment.MachineName);
                }
                return _localPlayerName;
            }
            set
            {
                _localPlayerName = value;
                PlayerPrefs.SetString(PrefsPlayerNameKey, value);
                PlayerPrefs.Save();
            }
        }

        // Connection status feedback
        public event Action<string> OnConnectionStatusMessage;
        public event Action OnClientConnectedSuccessfully;
        public event Action<string> OnClientConnectionFailed;

        public bool IsHost => _networkManager != null && _networkManager.IsHost;
        public bool IsClient => _networkManager != null && _networkManager.IsClient;
        public bool IsServer => _networkManager != null && _networkManager.IsServer;
        public bool IsListening => _networkManager != null && _networkManager.IsListening;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveComponents();
            ConfigureTransportDefaults();
        }

        private void Start()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback += HandleClientConnected;
                _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientConnectedCallback -= HandleClientConnected;
                _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
            if (_instance == this) _instance = null;
        }

        private void ResolveComponents()
        {
            if (_networkManager == null) _networkManager = GetComponent<NetworkManager>();
            if (_transport == null) _transport = GetComponent<UnityTransport>();
            if (_networkManager != null && _transport != null)
            {
                if (_networkManager.NetworkConfig != null && _networkManager.NetworkConfig.NetworkTransport == null)
                {
                    _networkManager.NetworkConfig.NetworkTransport = _transport;
                }
            }
            if (_lanDiscovery == null) _lanDiscovery = GetComponent<LanDiscovery>();
            if (_lanDiscovery == null) _lanDiscovery = gameObject.AddComponent<LanDiscovery>();
        }

        private void ConfigureTransportDefaults()
        {
            if (_transport != null)
            {
                // By default listen on all network interfaces (0.0.0.0) so Wi-Fi LAN clients can connect
                _transport.SetConnectionData("127.0.0.1", _gamePort, "0.0.0.0");
                _transport.MaxConnectAttempts = 4;
                _transport.ConnectTimeoutMS = 3000;
                _transport.DisconnectTimeoutMS = 5000;
                _transport.HeartbeatTimeoutMS = 1500;
            }

            if (_networkManager != null)
            {
                _networkManager.NetworkConfig.EnableSceneManagement = true;
                _networkManager.NetworkConfig.ConnectionApproval = false;
                _networkManager.NetworkConfig.TickRate = 30;

                if (_lobbyManagerPrefab != null && !_networkManager.NetworkConfig.Prefabs.Contains(_lobbyManagerPrefab))
                {
                    _networkManager.AddNetworkPrefab(_lobbyManagerPrefab);
                }
            }
        }

        // =====================================================================
        // HOSTING
        // =====================================================================

        /// <summary>
        /// Starts hosting a LAN game on this machine.
        /// Binds to 0.0.0.0 and starts UDP discovery broadcasts.
        /// </summary>
        public bool HostGame(string sessionName = null, string playerName = null)
        {
            ResolveComponents();

            if (!string.IsNullOrEmpty(playerName))
            {
                LocalPlayerName = playerName;
            }

            string hostSessionName = string.IsNullOrEmpty(sessionName)
                ? $"{LocalPlayerName}'s Game"
                : sessionName;

            try
            {
                // Ensure transport listens on all interfaces (0.0.0.0) so local Wi-Fi clients can join
                _transport.SetConnectionData("127.0.0.1", _gamePort, "0.0.0.0");

                bool success = _networkManager.StartHost();
                if (success)
                {
                    // Spawn GameLobbyManager across network
                    if (_lobbyManagerPrefab != null && GameLobbyManager.Instance == null)
                    {
                        var lobbyGo = Instantiate(_lobbyManagerPrefab);
                        var netObj = lobbyGo.GetComponent<NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.Spawn();
                        }
                    }

                    string localIp = LanDiscovery.ResolveLocalIPv4();
                    Debug.Log($"[NetworkManagerBootstrap] Started hosting on {localIp}:{_gamePort} as '{LocalPlayerName}'");
                    OnConnectionStatusMessage?.Invoke($"Hosting session on {localIp}:{_gamePort}");

                    // Advertise session over LAN
                    if (_lanDiscovery != null)
                    {
                        _lanDiscovery.StartBroadcasting(hostSessionName, _gamePort, currentPlayers: 1, maxPlayers: 4);
                    }
                    return true;
                }
                else
                {
                    string err = "Failed to start Host. Check if port 7777 is already in use.";
                    Debug.LogError($"[NetworkManagerBootstrap] {err}");
                    OnClientConnectionFailed?.Invoke(err);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string err = $"Host error: {ex.Message}";
                Debug.LogError($"[NetworkManagerBootstrap] {err}");
                OnClientConnectionFailed?.Invoke(err);
                return false;
            }
        }

        // =====================================================================
        // JOINING (DIRECT OR LAN DISCOVERY)
        // =====================================================================

        /// <summary>
        /// Joins an existing host by IPv4 address and port.
        /// </summary>
        public bool JoinGame(string hostAddress, ushort port = DefaultGamePort, string playerName = null)
        {
            ResolveComponents();

            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                string err = "Invalid IP address. Please enter a valid Host IP.";
                OnClientConnectionFailed?.Invoke(err);
                return false;
            }

            hostAddress = hostAddress.Trim();

            if (!string.IsNullOrEmpty(playerName))
            {
                LocalPlayerName = playerName;
            }

            try
            {
                // Configure transport to connect to the target host address
                _transport.SetConnectionData(hostAddress, port, "0.0.0.0");

                OnConnectionStatusMessage?.Invoke($"Connecting to {hostAddress}:{port}...");
                Debug.Log($"[NetworkManagerBootstrap] Connecting to host at {hostAddress}:{port} as '{LocalPlayerName}'...");

                bool success = _networkManager.StartClient();
                if (success)
                {
                    return true;
                }
                else
                {
                    string err = $"Failed to initiate client connection to {hostAddress}:{port}.";
                    Debug.LogError($"[NetworkManagerBootstrap] {err}");
                    OnClientConnectionFailed?.Invoke(err);
                    return false;
                }
            }
            catch (Exception ex)
            {
                string err = $"Join error: {ex.Message}";
                Debug.LogError($"[NetworkManagerBootstrap] {err}");
                OnClientConnectionFailed?.Invoke(err);
                return false;
            }
        }

        // =====================================================================
        // DISCONNECT
        // =====================================================================

        /// <summary>
        /// Disconnects from current game session and stops all advertising.
        /// </summary>
        public void Disconnect()
        {
            if (_lanDiscovery != null)
            {
                _lanDiscovery.StopAll();
            }

            if (_networkManager != null && _networkManager.IsListening)
            {
                _networkManager.Shutdown();
                Debug.Log("[NetworkManagerBootstrap] NetworkManager shutdown.");
            }

            OnConnectionStatusMessage?.Invoke("Disconnected from game.");

            // Return to LobbyScene if we are in TutorialScene
            if (SceneManager.GetActiveScene().name != GameLobbyManager.LobbySceneName)
            {
                SceneManager.LoadScene(GameLobbyManager.LobbySceneName);
            }
        }

        // =====================================================================
        // CALLBACKS
        // =====================================================================

        private void HandleClientConnected(ulong clientId)
        {
            if (clientId == _networkManager.LocalClientId)
            {
                Debug.Log($"[NetworkManagerBootstrap] Successfully connected to host! (LocalClientId: {clientId})");
                OnConnectionStatusMessage?.Invoke("Connected to lobby!");
                OnClientConnectedSuccessfully?.Invoke();

                // If we joined as client, inform GameLobbyManager of our chosen player name
                if (!IsHost && GameLobbyManager.Instance != null)
                {
                    GameLobbyManager.Instance.SetPlayerNameServerRpc(LocalPlayerName);
                }
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (clientId == _networkManager.LocalClientId)
            {
                string reason = _networkManager.DisconnectReason;
                if (string.IsNullOrEmpty(reason))
                {
                    reason = IsHost ? "Host session ended." : "Disconnected from host (network timeout or host closed).";
                }

                Debug.LogWarning($"[NetworkManagerBootstrap] Local client disconnected: {reason}");
                OnClientConnectionFailed?.Invoke(reason);

                // Stop discovery
                if (_lanDiscovery != null)
                {
                    _lanDiscovery.StopAll();
                }

                // If disconnected while in game scene, return to LobbyScene
                if (SceneManager.GetActiveScene().name != GameLobbyManager.LobbySceneName)
                {
                    SceneManager.LoadScene(GameLobbyManager.LobbySceneName);
                }
            }
            else
            {
                Debug.Log($"[NetworkManagerBootstrap] Remote client {clientId} disconnected.");
            }
        }
    }
}
