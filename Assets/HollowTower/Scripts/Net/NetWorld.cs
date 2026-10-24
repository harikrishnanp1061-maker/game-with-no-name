using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace HollowTower.Net
{
    /// <summary>Something in the world whose changes every machine must see the same (a door, a hiding spot, an item...).
    /// NetApply makes the change happen here; it returns false if it can't (the door is locked, the spot is taken).</summary>
    public interface INetAction
    {
        bool NetApply(int action, int a, int b, Vector3 v);
    }

    /// <summary>Something purely to see (a spray-paint stroke): drawn at once where it was made, then shown to everyone
    /// else - nothing to decide, so it doesn't wait for the host.</summary>
    public interface INetShare
    {
        void NetShare(int a, Vector3 p, Vector3 n);
    }

    /// <summary>
    /// How the world stays the same on every machine. A change goes through Act: offline it simply happens; on the host
    /// it happens and is then told to every client; on a client it is asked of the host, which decides (it may say no:
    /// someone else got there first) and tells everyone. Objects are found by their place in the scene, which is the
    /// same on every machine (all load the same scene), so nothing needs registering. Noise also goes through here: the
    /// monsters live on the host, so a client's scream or slam has to reach it.
    /// </summary>
    public class NetWorld : NetworkBehaviour
    {
        static NetWorld instance;
        static readonly Dictionary<uint, INetAction> byKey = new Dictionary<uint, INetAction>();

        void Awake() { instance = this; }
        public override void OnDestroy() { base.OnDestroy(); if (instance == this) instance = null; byKey.Clear(); }

        /// <summary>Make a change to target, the same everywhere.</summary>
        public static void Act(INetAction target, int action, int a = 0, int b = 0, Vector3 v = default(Vector3))
        {
            if (target == null) return;
            if (!Net.Active || instance == null || !instance.IsSpawned) { target.NetApply(action, a, b, v); return; }
            uint key = Key(target);
            if (instance.IsServer) { if (target.NetApply(action, a, b, v)) instance.ApplyRpc(key, action, a, b, v); }
            else instance.AskRpc(key, action, a, b, v);
        }

        /// <summary>Show something already drawn here to every other machine.</summary>
        public static void Share(INetShare target, int a, Vector3 p, Vector3 n)
        {
            if (!Net.Active || instance == null || !instance.IsSpawned || target == null) return;
            uint key = ShareKey(target);
            if (instance.IsServer) instance.ShowRpc(key, a, p, n, NetworkManager.ServerClientId);
            else instance.ShareRpc(key, a, p, n);
        }

        [Rpc(SendTo.Server)]
        void ShareRpc(uint key, int a, Vector3 p, Vector3 n, RpcParams rpc = default)
        {
            ulong from = rpc.Receive.SenderClientId;
            var t = FindShare(key);
            if (t != null) t.NetShare(a, p, n);
            ShowRpc(key, a, p, n, from);
        }

        [Rpc(SendTo.NotServer)]
        void ShowRpc(uint key, int a, Vector3 p, Vector3 n, ulong from)
        {
            if (from == NetworkManager.LocalClientId) return;   // it drew its own
            var t = FindShare(key);
            if (t != null) t.NetShare(a, p, n);
        }

        static readonly Dictionary<uint, INetShare> shareKeys = new Dictionary<uint, INetShare>();
        static uint ShareKey(INetShare t)
        {
            var mb = t as MonoBehaviour;
            uint k = Hash("share|" + mb.GetType().Name + "|" + Path(mb.transform));
            shareKeys[k] = t;
            return k;
        }
        static INetShare FindShare(uint key)
        {
            INetShare t;
            if (shareKeys.TryGetValue(key, out t) && t as MonoBehaviour != null) return t;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { var s = mb as INetShare; if (s != null) ShareKey(s); }
            return shareKeys.TryGetValue(key, out t) ? t : null;
        }

        /// <summary>A sound monsters can hear, wherever it was made (the monsters only think on the host).</summary>
        public static void Noise(Vector3 pos, float radius)
        {
            if (!Net.IsClientOnly || instance == null || !instance.IsSpawned) { Hear(pos, radius); return; }
            instance.NoiseRpc(pos, radius);
        }

        static void Hear(Vector3 pos, float radius) { NeedleMonster.Hear(pos, radius); PartyMonster.HeardAt(pos, radius); }

        [Rpc(SendTo.Server)]
        void AskRpc(uint key, int action, int a, int b, Vector3 v)
        {
            var t = Find(key);
            if (t != null && t.NetApply(action, a, b, v)) ApplyRpc(key, action, a, b, v);
        }

        [Rpc(SendTo.NotServer)]
        void ApplyRpc(uint key, int action, int a, int b, Vector3 v)
        {
            var t = Find(key);
            if (t != null) t.NetApply(action, a, b, v);
            else Debug.LogWarning("[NetWorld] nothing here for " + key);
        }

        [Rpc(SendTo.Server)]
        void NoiseRpc(Vector3 pos, float radius) { Hear(pos, radius); }

        // ---------------------------------------------------------------- finding things by their place in the scene
        public static uint Key(INetAction t)
        {
            var mb = t as MonoBehaviour;
            uint k = Hash(mb.GetType().Name + "|" + Path(mb.transform));
            byKey[k] = t;
            return k;
        }

        static INetAction Find(uint key)
        {
            INetAction t;
            if (byKey.TryGetValue(key, out t) && t as MonoBehaviour != null) return t;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var a = mb as INetAction;
                if (a != null) Key(a);
            }
            return byKey.TryGetValue(key, out t) ? t : null;
        }

        static string Path(Transform t)
        {
            var parts = new List<string>();
            // root objects by name alone: ones made at runtime (PartyFloor...) may sit in a different order on each machine
            for (; t != null; t = t.parent) parts.Add(t.parent == null ? t.name : t.name + "#" + t.GetSiblingIndex());
            parts.Reverse();
            return string.Join("/", parts);
        }

        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }
    }
}
