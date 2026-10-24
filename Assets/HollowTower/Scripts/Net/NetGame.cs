using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HollowTower.Net
{
    /// <summary>
    /// A level's session glue (one per gameplay scene, placed by the net setup): when the level starts the host hands
    /// each player's body to the machine sitting in that seat; the bodies nobody is playing stay the host's. Anyone can
    /// switch (F1-F4) to a body no other person is playing - one person can still run all four, as in solo - and when
    /// someone drops out their body goes back to the host where it stands.
    /// </summary>
    public class NetGame : NetworkBehaviour
    {
        public static NetGame Instance { get; private set; }
        // server: who is playing each body (absent = nobody, the host holds it)
        readonly Dictionary<int, ulong> claims = new Dictionary<int, ulong>();

        void Awake() { Instance = this; }
        public override void OnDestroy() { base.OnDestroy(); if (Instance == this) Instance = null; }

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            NetworkManager.OnClientDisconnectCallback += Dropped;
            var party = PlayerParty.Instance;
            var lobby = NetLobby.Instance;
            if (party == null) return;
            for (int i = 0; i < party.players.Length; i++)
            {
                var nb = party.players[i] != null ? party.players[i].GetComponent<NetBody>() : null;
                if (nb == null || !nb.IsSpawned) continue;
                ulong c;
                if (lobby != null && lobby.ClientOf(i, out c))
                {
                    claims[i] = c;
                    if (nb.OwnerClientId != c) nb.NetworkObject.ChangeOwnership(c);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= Dropped;
        }

        /// <summary>A machine asks to play body i.</summary>
        [Rpc(SendTo.Server)]
        public void TakeRpc(int i, RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            var party = PlayerParty.Instance;
            if (party == null || i < 0 || i >= party.players.Length || party.players[i] == null) return;
            ulong c;
            if (claims.TryGetValue(i, out c) && c != who && c != NetworkManager.ServerClientId)
            {
                RefusedRpc(i, NetLobby.Instance != null ? NetLobby.Instance.NameOf(i) : "someone", RpcTarget.Single(who, RpcTargetUse.Temp));
                return;
            }
            // a machine plays one body at a time: the one it left goes back to the host, free for anyone
            var left = new List<int>();
            foreach (var kv in claims) if (kv.Value == who && kv.Key != i) left.Add(kv.Key);
            foreach (int k in left)
            {
                claims.Remove(k);
                var old = party.players[k] != null ? party.players[k].GetComponent<NetBody>() : null;
                if (old != null && old.IsSpawned && old.OwnerClientId != NetworkManager.ServerClientId) old.NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
            }
            claims[i] = who;
            var nb = party.players[i].GetComponent<NetBody>();
            if (nb.OwnerClientId != who) nb.NetworkObject.ChangeOwnership(who);
            else GrantedRpc(i, RpcTarget.Single(who, RpcTargetUse.Temp));   // already theirs (the host's own bodies)
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void RefusedRpc(int i, string byName, RpcParams rpc)
        {
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("Player " + (i + 1) + " is being played by " + byName, 2.5f);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void GrantedRpc(int i, RpcParams rpc)
        {
            var party = PlayerParty.Instance;
            if (party != null) party.SwitchTo(i, true);
        }

        void Dropped(ulong client)
        {
            var party = PlayerParty.Instance;
            var gone = new List<int>();
            foreach (var kv in claims) if (kv.Value == client) gone.Add(kv.Key);
            foreach (int i in gone)
            {
                claims.Remove(i);
                var nb = party != null && party.players[i] != null ? party.players[i].GetComponent<NetBody>() : null;
                if (nb != null && nb.IsSpawned) nb.NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
            }
        }

        /// <summary>Is body i being played by another machine than this one?</summary>
        public bool TakenByOther(int i)
        {
            ulong c;
            return IsServer && claims.TryGetValue(i, out c) && c != NetworkManager.LocalClientId;
        }
    }
}
