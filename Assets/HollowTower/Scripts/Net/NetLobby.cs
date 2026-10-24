using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower.Net
{
    /// <summary>One lobby seat: who, which of the four players (0-3) they are, and whether they're ready.</summary>
    public struct LobbySeat : INetworkSerializable, IEquatable<LobbySeat>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public int Player;
        public bool Ready;
        public bool IsHost;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId); s.SerializeValue(ref Name); s.SerializeValue(ref Player); s.SerializeValue(ref Ready); s.SerializeValue(ref IsHost);
        }

        public bool Equals(LobbySeat o) { return ClientId == o.ClientId && Name.Equals(o.Name) && Player == o.Player && Ready == o.Ready && IsHost == o.IsHost; }
    }

    /// <summary>
    /// The lobby (the tutorial's GameLobbyManager): up to four people, each given the lowest free player slot (P1-P4,
    /// each with their ability), a name and a ready flag; the host starts when everyone is ready and the session moves
    /// to the tutorial together. Spawned by the host and kept for the whole session, so the levels know who is who.
    /// </summary>
    public class NetLobby : NetworkBehaviour
    {
        public const int MaxPlayers = 4;
        public const string LobbyScene = "Lobby";
        public const string FirstLevel = "Tutorial";
        public const string TowerLevel = "HollowTower_Remastered";

        public static NetLobby Instance { get; private set; }
        public event Action Changed;

        NetworkList<LobbySeat> seats;
        readonly NetworkVariable<bool> twoPlayer = new NetworkVariable<bool>(false);
        /// <summary>The host's choice: a two-player game (each of the two has two players' abilities).</summary>
        public bool TwoPlayer { get { return twoPlayer.Value; } set { if (IsServer) twoPlayer.Value = value; PlayerParty.TwoPlayer = value; } }
        public int Capacity { get { return twoPlayer.Value ? 2 : MaxPlayers; } }
        readonly Dictionary<ulong, string> pendingNames = new Dictionary<ulong, string>();   // a name that arrived before its seat

        public List<LobbySeat> Seats
        {
            get { var l = new List<LobbySeat>(); if (seats != null) for (int i = 0; i < seats.Count; i++) l.Add(seats[i]); return l; }
        }

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            seats = new NetworkList<LobbySeat>(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            seats.OnListChanged += OnSeats;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += Joined;
                NetworkManager.OnClientDisconnectCallback += Left;
                Seat(NetworkManager.LocalClientId, NetBootstrap.PlayerName, true);
            }
            else SetNameRpc(NetBootstrap.PlayerName);
            if (Changed != null) Changed();
        }

        public override void OnNetworkDespawn()
        {
            seats.OnListChanged -= OnSeats;
            if (NetworkManager != null) { NetworkManager.OnClientConnectedCallback -= Joined; NetworkManager.OnClientDisconnectCallback -= Left; }
        }

        public override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; }

        void OnSeats(NetworkListEvent<LobbySeat> e)
        {
            if (IsServer && LanDiscovery.Instance != null && LanDiscovery.Instance.IsBroadcasting) LanDiscovery.Instance.UpdatePlayerCount(seats.Count);
            if (Changed != null) Changed();
        }

        void Joined(ulong id) { if (id != NetworkManager.ServerClientId) Seat(id, "Player", false); }

        void Left(ulong id)
        {
            for (int i = 0; i < seats.Count; i++) if (seats[i].ClientId == id) { seats.RemoveAt(i); break; }
        }

        void Seat(ulong id, string name, bool host)
        {
            if (seats.Count >= Capacity) { NetworkManager.DisconnectClient(id, "The game is full"); return; }
            var used = new HashSet<int>();
            for (int i = 0; i < seats.Count; i++) used.Add(seats[i].Player);
            int p = 0; while (used.Contains(p)) p++;
            string n; if (pendingNames.TryGetValue(id, out n)) { name = n; pendingNames.Remove(id); }
            seats.Add(new LobbySeat { ClientId = id, Name = new FixedString64Bytes(name), Player = p, Ready = host, IsHost = host });
        }

        [Rpc(SendTo.Server)]
        public void SetNameRpc(string name, RpcParams rpc = default)
        {
            pendingNames[rpc.Receive.SenderClientId] = string.IsNullOrEmpty(name) ? "Player" : name;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].ClientId == rpc.Receive.SenderClientId) { var s = seats[i]; s.Name = new FixedString64Bytes(string.IsNullOrEmpty(name) ? "Player" : name); seats[i] = s; }
        }

        [Rpc(SendTo.Server)]
        public void ToggleReadyRpc(RpcParams rpc = default)
        {
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].ClientId == rpc.Receive.SenderClientId) { var s = seats[i]; s.Ready = !s.Ready; seats[i] = s; }
        }

        public bool CanStart
        {
            get { if (seats == null || seats.Count == 0) return false; for (int i = 0; i < seats.Count; i++) if (!seats[i].Ready) return false; return true; }
        }

        /// <summary>Host: everyone into the first level (the tutorial; the tower to skip it).</summary>
        public void StartGame(string level = FirstLevel)
        {
            if (!IsServer || !CanStart) return;
            if (LanDiscovery.Instance != null) LanDiscovery.Instance.StopBroadcasting();
            NetworkManager.SceneManager.LoadScene(level, LoadSceneMode.Single);
        }

        /// <summary>Host: everyone on to the next level together (the tutorial's elevator).</summary>
        public static void LoadLevel(string scene)
        {
            if (Net.Active && Net.Manager.IsServer) Net.Manager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
            else if (!Net.Active) SceneManager.LoadScene(scene);
        }

        /// <summary>Which of the four players (0-3) a machine is, or -1.</summary>
        public int PlayerOf(ulong clientId)
        {
            if (seats != null) for (int i = 0; i < seats.Count; i++) if (seats[i].ClientId == clientId) return seats[i].Player;
            return -1;
        }

        /// <summary>The machine playing player p, if anyone is.</summary>
        public bool ClientOf(int p, out ulong clientId)
        {
            if (seats != null) for (int i = 0; i < seats.Count; i++) if (seats[i].Player == p) { clientId = seats[i].ClientId; return true; }
            clientId = 0; return false;
        }

        public string NameOf(int p)
        {
            if (seats != null) for (int i = 0; i < seats.Count; i++) if (seats[i].Player == p) return seats[i].Name.ToString();
            return "P" + (p + 1);
        }
    }
}
