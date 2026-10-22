using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FlipFall.Networking
{
    /// <summary>
    /// Represents an active game session discovered on the local network.
    /// </summary>
    [Serializable]
    public class LanSessionInfo
    {
        public string HostName;
        public string HostAddress;
        public ushort Port;
        public int CurrentPlayers;
        public int MaxPlayers;
        public float LastSeenTime;

        public string Key => $"{HostAddress}:{Port}";

        public override string ToString()
        {
            return $"{HostName} ({CurrentPlayers}/{MaxPlayers}) @ {HostAddress}:{Port}";
        }
    }

    /// <summary>
    /// Handles LAN discovery via UDP broadcasting on port 47777.
    /// Broadcasts session announcements when hosting, and listens for available sessions on LAN.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FLIP//FALL/Networking/LAN Discovery")]
    public class LanDiscovery : MonoBehaviour
    {
        public const int DiscoveryPort = 47777;
        private const string MagicHeader = "FLIPFALL_LAN";
        private const float BroadcastInterval = 1.5f;
        private const float StaleSessionTimeout = 5.0f;

        private static LanDiscovery _instance;
        public static LanDiscovery Instance => _instance;

        // Events
        public event Action<List<LanSessionInfo>> OnSessionsUpdated;
        public event Action<string> OnDiscoveryError;

        // Discovered sessions
        private readonly Dictionary<string, LanSessionInfo> _discoveredSessions = new Dictionary<string, LanSessionInfo>();
        private readonly List<LanSessionInfo> _sessionListCache = new List<LanSessionInfo>();

        // Networking sockets
        private UdpClient _broadcastClient;
        private UdpClient _listenClient;
        private Thread _listenThread;
        private volatile bool _isListening = false;
        private volatile bool _isBroadcasting = false;

        private float _broadcastTimer = 0f;
        private string _sessionName = "FLIP//FALL Host";
        private ushort _gamePort = 7777;
        private int _currentPlayers = 1;
        private int _maxPlayers = 4;
        private string _cachedLocalIp = "";

        public bool IsBroadcasting => _isBroadcasting;
        public bool IsListening => _isListening;
        public IReadOnlyList<LanSessionInfo> DiscoveredSessions => _sessionListCache;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _cachedLocalIp = ResolveLocalIPv4();
        }

        private void Update()
        {
            // If broadcasting as host, send announcements periodically
            if (_isBroadcasting)
            {
                _broadcastTimer += Time.unscaledDeltaTime;
                if (_broadcastTimer >= BroadcastInterval)
                {
                    _broadcastTimer = 0f;
                    SendBroadcastPacket();
                }
            }

            // If listening, clean up stale sessions
            if (_isListening)
            {
                CleanStaleSessions();
            }
        }

        private void OnDestroy()
        {
            StopAll();
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit()
        {
            StopAll();
        }

        // =====================================================================
        // BROADCASTING (HOST)
        // =====================================================================

        /// <summary>
        /// Starts advertising this machine's session over LAN.
        /// </summary>
        public void StartBroadcasting(string sessionName, ushort gamePort, int currentPlayers = 1, int maxPlayers = 4)
        {
            _sessionName = string.IsNullOrEmpty(sessionName) ? Environment.MachineName : sessionName;
            _gamePort = gamePort;
            _currentPlayers = currentPlayers;
            _maxPlayers = maxPlayers;
            _cachedLocalIp = ResolveLocalIPv4();

            try
            {
                if (_broadcastClient == null)
                {
                    _broadcastClient = new UdpClient();
                    _broadcastClient.EnableBroadcast = true;
                }
                _isBroadcasting = true;
                _broadcastTimer = BroadcastInterval; // Send first beacon immediately
                Debug.Log($"[LanDiscovery] Started broadcasting session '{_sessionName}' at {_cachedLocalIp}:{_gamePort} via UDP {DiscoveryPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LanDiscovery] Failed to start UDP broadcaster: {ex.Message}");
                OnDiscoveryError?.Invoke($"Broadcast error: {ex.Message}");
            }
        }

        public void UpdatePlayerCount(int currentPlayers)
        {
            _currentPlayers = currentPlayers;
        }

        public void StopBroadcasting()
        {
            _isBroadcasting = false;
            try
            {
                if (_broadcastClient != null)
                {
                    _broadcastClient.Close();
                    _broadcastClient = null;
                }
            }
            catch { }
            Debug.Log("[LanDiscovery] Stopped broadcasting.");
        }

        private void SendBroadcastPacket()
        {
            if (_broadcastClient == null) return;

            try
            {
                // Format: FLIPFALL_LAN|HostName|HostIP|Port|CurrentPlayers|MaxPlayers
                string message = $"{MagicHeader}|{_sessionName}|{_cachedLocalIp}|{_gamePort}|{_currentPlayers}|{_maxPlayers}";
                byte[] bytes = Encoding.UTF8.GetBytes(message);

                IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                _broadcastClient.Send(bytes, bytes.Length, endpoint);
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[LanDiscovery] Socket exception broadcasting: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LanDiscovery] Error sending broadcast: {ex.Message}");
            }
        }

        // =====================================================================
        // LISTENING / SEARCHING (CLIENT)
        // =====================================================================

        /// <summary>
        /// Starts listening for game session announcements on LAN.
        /// </summary>
        public void StartListening()
        {
            if (_isListening) return;

            lock (_discoveredSessions)
            {
                _discoveredSessions.Clear();
                _sessionListCache.Clear();
            }

            try
            {
                _listenClient = new UdpClient();
                _listenClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listenClient.ExclusiveAddressUse = false;
                _listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                _isListening = true;
                _listenThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "FLIPFALL_LanDiscovery_Listener"
                };
                _listenThread.Start();
                Debug.Log($"[LanDiscovery] Listening for LAN sessions on UDP {DiscoveryPort}...");
            }
            catch (SocketException ex)
            {
                _isListening = false;
                Debug.LogError($"[LanDiscovery] Could not bind UDP listener on {DiscoveryPort}: {ex.Message}");
                OnDiscoveryError?.Invoke($"Firewall / Port conflict on UDP {DiscoveryPort}. Try Direct IP connect.");
            }
            catch (Exception ex)
            {
                _isListening = false;
                Debug.LogError($"[LanDiscovery] Error starting listener: {ex.Message}");
            }
        }

        public void StopListening()
        {
            _isListening = false;
            try
            {
                if (_listenClient != null)
                {
                    _listenClient.Close();
                    _listenClient = null;
                }
            }
            catch { }

            if (_listenThread != null && _listenThread.IsAlive)
            {
                try { _listenThread.Join(200); } catch { }
                _listenThread = null;
            }
            Debug.Log("[LanDiscovery] Stopped listening.");
        }

        private void ListenLoop()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (_isListening)
            {
                try
                {
                    if (_listenClient == null) break;
                    byte[] data = _listenClient.Receive(ref remoteEndPoint);
                    if (data == null || data.Length == 0) continue;

                    string message = Encoding.UTF8.GetString(data);
                    if (!message.StartsWith(MagicHeader)) continue;

                    string[] parts = message.Split('|');
                    if (parts.Length < 6) continue;

                    string hostName = parts[1];
                    string advertisedIp = parts[2];
                    if (!ushort.TryParse(parts[3], out ushort port)) port = 7777;
                    if (!int.TryParse(parts[4], out int currentPlayers)) currentPlayers = 1;
                    if (!int.TryParse(parts[5], out int maxPlayers)) maxPlayers = 4;

                    // Prefer sender IP if advertised IP is invalid or loopback
                    string senderIp = remoteEndPoint.Address.ToString();
                    string effectiveIp = (advertisedIp == "127.0.0.1" || string.IsNullOrEmpty(advertisedIp))
                        ? senderIp
                        : advertisedIp;

                    lock (_discoveredSessions)
                    {
                        string key = $"{effectiveIp}:{port}";
                        if (!_discoveredSessions.TryGetValue(key, out LanSessionInfo session))
                        {
                            session = new LanSessionInfo();
                            _discoveredSessions[key] = session;
                        }

                        session.HostName = hostName;
                        session.HostAddress = effectiveIp;
                        session.Port = port;
                        session.CurrentPlayers = currentPlayers;
                        session.MaxPlayers = maxPlayers;
                        session.LastSeenTime = (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
                    }
                }
                catch (SocketException)
                {
                    // Socket closed normally or timeout
                    break;
                }
                catch (Exception ex)
                {
                    if (_isListening)
                    {
                        Debug.LogWarning($"[LanDiscovery] Error reading packet: {ex.Message}");
                    }
                    break;
                }
            }
        }

        private void CleanStaleSessions()
        {
            float now = (float)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            bool changed = false;

            lock (_discoveredSessions)
            {
                List<string> staleKeys = null;
                foreach (var kvp in _discoveredSessions)
                {
                    if (now - kvp.Value.LastSeenTime > StaleSessionTimeout)
                    {
                        if (staleKeys == null) staleKeys = new List<string>();
                        staleKeys.Add(kvp.Key);
                    }
                }

                if (staleKeys != null)
                {
                    foreach (var key in staleKeys)
                    {
                        _discoveredSessions.Remove(key);
                        changed = true;
                    }
                }

                if (changed || _sessionListCache.Count != _discoveredSessions.Count)
                {
                    _sessionListCache.Clear();
                    _sessionListCache.AddRange(_discoveredSessions.Values);
                    OnSessionsUpdated?.Invoke(_sessionListCache);
                }
            }
        }

        public void StopAll()
        {
            StopBroadcasting();
            StopListening();
        }

        // =====================================================================
        // LOCAL IP HELPER
        // =====================================================================

        /// <summary>
        /// Retrieves the most likely routeable local IPv4 address on this machine.
        /// </summary>
        public static string ResolveLocalIPv4()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    // Connecting a UDP socket doesn't send any network packets,
                    // but allows the OS to determine the routing interface.
                    socket.Connect("8.8.8.8", 65530);
                    if (socket.LocalEndPoint is IPEndPoint ep)
                    {
                        return ep.Address.ToString();
                    }
                }
            }
            catch { }

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }
    }
}
