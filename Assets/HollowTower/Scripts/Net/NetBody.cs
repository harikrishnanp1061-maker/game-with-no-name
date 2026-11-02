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
        readonly NetworkVariable<bool> crouched = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> grounded = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<FixedString128Bytes> deathHow = new NetworkVariable<FixedString128Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        RemasteredPlayerController body;
        Vector3 lastPos, vel;
        float speed;
        CharacterAnimDriver driver;

        /// <summary>Out of breath (set by the owner's PlayerVitals; heard as panting on the other machines).</summary>
        public bool Winded { get { return winded.Value; } set { if (IsSpawned && IsOwner && winded.Value != value) winded.Value = value; } }

        /// <summary>Standing on something, as its owner last reported (for someone else's body).</summary>
        public bool Grounded { get { return grounded.Value; } }

        public int Index { get { var p = PlayerParty.Instance; return p != null ? System.Array.IndexOf(p.players, body) : -1; } }

        /// <summary>This machine steers this body (always true offline).</summary>
        public static bool Mine(RemasteredPlayerController b)
        {
            if (!Net.Active || b == null) return true;
            var nb = b.GetComponent<NetBody>();
            return nb == null || !nb.IsSpawned || nb.IsOwner;
        }

        void Awake()
        {
            body = GetComponent<RemasteredPlayerController>();
            if (body.animator != null) driver = body.animator.GetComponent<CharacterAnimDriver>();
        }

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
                if (crouched.Value != body.IsCrouching) crouched.Value = body.IsCrouching;
                if (grounded.Value != body.IsGrounded) grounded.Value = body.IsGrounded;
                return;
            }
            // someone else's body: walk it at the speed it is moving, torch as they have it
            Vector3 d = transform.position - lastPos; lastPos = transform.position;
            Vector3 v3 = d / Mathf.Max(Time.deltaTime, 0.001f); v3.y = 0f;
            vel = Vector3.Lerp(vel, v3, 1f - Mathf.Exp(-10f * Time.deltaTime));   // network ticks arrive unevenly
            speed = vel.magnitude;
            if (driver != null) driver.Drive(vel, grounded.Value, crouched.Value, body.hasTorch && torch.Value, body.IsDead);
            else if (body.animator != null && !body.IsDead) body.animator.SetFloat("Speed", speed < 0.15f ? 0f : speed);
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

        // ---------------------------------------------------------------- revive (dev)
        /// <summary>In a session: asks the host to bring b back at a spot, and every machine then revives it there. False
        /// when not in a session (the caller revives locally).</summary>
        public static bool RequestRevive(RemasteredPlayerController b, Vector3 at, Quaternion rot)
        {
            if (!Net.Active || b == null) return false;
            var nb = b.GetComponent<NetBody>();
            if (nb == null || !nb.IsSpawned) return false;
            nb.ReviveRequestRpc(at, rot);
            return true;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ReviveRequestRpc(Vector3 at, Quaternion rot)
        {
            if (!dead.Value && !body.IsDead) return;
            dead.Value = false;
            deathHow.Value = default;
            RevivedRpc(at, rot);
        }

        [Rpc(SendTo.Everyone)]
        void RevivedRpc(Vector3 at, Quaternion rot)
        {
            var party = PlayerParty.Instance;
            if (party != null) party.ReviveAt(Index, at, rot);
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
