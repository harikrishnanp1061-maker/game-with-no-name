using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HollowTower.Net
{
    /// <summary>
    /// One of the four player bodies on the network (the tutorial's NetworkPlayerBridge, on HollowTower's player). The
    /// machine that owns it steers it and its position goes out through OwnerNetworkTransform; everyone else sees it
    /// walk (animated from how fast it moves), with its torch on or off. Death is the host's to confirm: whoever sees it
    /// first (the host for a monster, the owner for a fall) reports it, and every machine then plays the same death.
    /// </summary>
    [RequireComponent(typeof(RemasteredPlayerController))]
    public class NetBody : NetworkBehaviour
    {
        readonly NetworkVariable<bool> torch = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> winded = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<FixedString128Bytes> deathHow = new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        RemasteredPlayerController body;
        Vector3 lastPos;
        float speed;

        /// <summary>Out of breath (set by the owner's PlayerVitals; heard as panting on the other machines).</summary>
        public bool Winded { get { return winded.Value; } set { if (IsSpawned && IsOwner && winded.Value != value) winded.Value = value; } }

        public int Index { get { var p = PlayerParty.Instance; return p != null ? System.Array.IndexOf(p.players, body) : -1; } }

        /// <summary>This machine steers this body (always true offline).</summary>
        public static bool Mine(RemasteredPlayerController b)
        {
            if (!Net.Active || b == null) return true;
            var nb = b.GetComponent<NetBody>();
            return nb == null || !nb.IsSpawned || nb.IsOwner;
        }

        void Awake() { body = GetComponent<RemasteredPlayerController>(); }

        public override void OnNetworkSpawn()
        {
            dead.OnValueChanged += OnDead;
            Apply();
            lastPos = transform.position;
            if (dead.Value && !body.IsDead) OnDead(false, true);   // joined after they died
        }

        public override void OnNetworkDespawn() { dead.OnValueChanged -= OnDead; body.remote = false; }

        public override void OnGainedOwnership()
        {
            Apply();
            var party = PlayerParty.Instance;
            if (party != null && party.WantsBody(Index)) party.SwitchTo(Index, true);
        }

        public override void OnLostOwnership()
        {
            Apply();
            var party = PlayerParty.Instance;
            if (party != null && party.Current == body) party.LoseBody();
        }

        void Apply()
        {
            body.remote = IsSpawned && !IsOwner;
            if (body.remote && body.isControlled) body.SetControlled(false, null);
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (torch.Value != body.flashlightEnabled) torch.Value = body.flashlightEnabled;
                return;
            }
            // someone else's body: walk it at the speed it is moving, torch as they have it
            Vector3 d = transform.position - lastPos; lastPos = transform.position;
            float v = new Vector2(d.x, d.z).magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            speed = Mathf.Lerp(speed, v, Time.deltaTime * 10f);
            if (body.animator != null && !body.IsDead) body.animator.SetFloat("Speed", speed < 0.15f ? 0f : speed);
            body.flashlightEnabled = torch.Value;
            if (body.flashlightLight != null && !body.IsDead) body.flashlightLight.enabled = torch.Value && body.hasTorch;
        }

        // ---------------------------------------------------------------- death
        /// <summary>Called as a player's death plays out on this machine (PlayerParty.OnPlayerDied): make it the same
        /// everywhere.</summary>
        public static void Report(RemasteredPlayerController b, string how)
        {
            if (!Net.Active || b == null) return;
            var nb = b.GetComponent<NetBody>();
            if (nb == null || !nb.IsSpawned || nb.dead.Value) return;
            if (nb.IsServer) { nb.deathHow.Value = new FixedString128Bytes(how ?? ""); nb.dead.Value = true; }
            else if (nb.IsOwner) nb.DiedRpc(how ?? "");
        }

        [Rpc(SendTo.Server)]
        void DiedRpc(string how)
        {
            if (dead.Value) return;
            deathHow.Value = new FixedString128Bytes(how);
            if (!body.IsDead) { body.Die(); var party = PlayerParty.Instance; if (party != null) party.OnPlayerDied(body, how); }
            dead.Value = true;
        }

        void OnDead(bool was, bool now)
        {
            if (!now || body.IsDead) return;
            body.Die();
            var party = PlayerParty.Instance;
            if (party != null) party.OnPlayerDied(body, deathHow.Value.ToString());
        }
    }
}
