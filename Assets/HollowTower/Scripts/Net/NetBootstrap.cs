using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower.Net
{
    /// <summary>
    /// Starts and stops sessions (ported from the tutorial's NetworkManagerBootstrap). Made on demand and kept across
    /// scenes: a NetworkManager with UnityTransport listening on every interface (so phones' and laptops' Wi-Fi can
    /// reach it, port 7777), LAN discovery so the session shows up on the other machines, and the lobby object.
    /// </summary>
    public class NetBootstrap : MonoBehaviour
    {
        public const ushort GamePort = 7777;
        const string NameKey = "HollowTower_PlayerName";

        public static NetBootstrap Instance { get; private set; }
        public NetworkManager Manager { get; private set; }
        public UnityTransport Transport { get; private set; }
        public LanDiscovery Discovery { get; private set; }

        public event Action<string> Status;

        public static string PlayerName
        {
            get { return PlayerPrefs.GetString(NameKey, Environment.MachineName); }
            set { PlayerPrefs.SetString(NameKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>The bootstrap, made if there isn't one yet.</summary>
        public static NetBootstrap Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Network");
            DontDestroyOnLoad(go);
            go.SetActive(false);   // add everything before NetworkManager wakes up
            var b = go.AddComponent<NetBootstrap>();
            b.Transport = go.AddComponent<UnityTransport>();
            b.Manager = go.AddComponent<NetworkManager>();
            b.Manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = b.Transport,
                EnableSceneManagement = true,
                ConnectionApproval = false,
                TickRate = 30,
            };
            b.Discovery = go.AddComponent<LanDiscovery>();
            go.SetActive(true);
            var lobby = Resources.Load<GameObject>("Net/NetLobby");
            if (lobby != null) b.Manager.AddNetworkPrefab(lobby);
            b.Transport.MaxConnectAttempts = 4;
            b.Transport.ConnectTimeoutMS = 3000;
            b.Transport.DisconnectTimeoutMS = 60000;   // loading the tower freezes a machine for a while: don't drop it
            b.Transport.HeartbeatTimeoutMS = 1500;
            b.Manager.OnClientDisconnectCallback += b.OnDisconnect;
            return b;
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Host(string sessionName)
        {
            Transport.SetConnectionData("127.0.0.1", GamePort, "0.0.0.0");
            if (!Manager.StartHost()) { Say("Couldn't host - is port " + GamePort + " already in use?"); return false; }
            var prefab = Resources.Load<GameObject>("Net/NetLobby");
            if (prefab != null && NetLobby.Instance == null) Instantiate(prefab).GetComponent<NetworkObject>().Spawn();
            Discovery.StartBroadcasting(string.IsNullOrEmpty(sessionName) ? PlayerName + "'s game" : sessionName, GamePort, 1, NetLobby.MaxPlayers);
            Say("Hosting on " + LanDiscovery.ResolveLocalIPv4() + ":" + GamePort);
            return true;
        }

        public bool Join(string address, ushort port = GamePort)
        {
            if (string.IsNullOrWhiteSpace(address)) { Say("Type the host's IP address"); return false; }
            Transport.SetConnectionData(address.Trim(), port, "0.0.0.0");
            Say("Connecting to " + address.Trim() + "...");
            if (!Manager.StartClient()) { Say("Couldn't start connecting"); return false; }
            return true;
        }

        public void Leave()
        {
            Discovery.StopAll();
            if (Manager.IsListening) Manager.Shutdown();
            if (SceneManager.GetActiveScene().name != NetLobby.LobbyScene) SceneManager.LoadScene(NetLobby.LobbyScene);
        }

        void OnDisconnect(ulong id)
        {
            if (id != Manager.LocalClientId && Manager.IsServer) return;
            string reason = string.IsNullOrEmpty(Manager.DisconnectReason) ? "Lost the connection to the host" : Manager.DisconnectReason;
            Say(reason);
            Discovery.StopAll();
            if (!Manager.IsServer && SceneManager.GetActiveScene().name != NetLobby.LobbyScene) SceneManager.LoadScene(NetLobby.LobbyScene);
        }

        public void Say(string s) { Debug.Log("[Net] " + s); if (Status != null) Status(s); }
    }
}
