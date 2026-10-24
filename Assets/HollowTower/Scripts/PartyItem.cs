using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Something that can be picked up into a player's inventory (E) and dropped again (G). With needsTorch on (the fuse
    /// cell in the dim Cold Storage) everyone can see and take it, but it only glows while P4's torch beam is on it.
    /// </summary>
    public class PartyItem : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public string itemId = "fuse_cell";
        public string displayName = "Fuse cell";
        public Color color = new Color(0.25f, 1f, 0.45f);
        public bool needsTorch = true;
        public Light glowLight;
        public Renderer[] glowRenderers;
        public float glowIntensity = 2.2f;

        public PlayerInventory Holder { get; private set; }
        public bool EverTaken { get; private set; }
        public bool Lit { get; private set; }
        float glow;
        MaterialPropertyBlock block;

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (Holder != null) return null;
            var inv = body.GetComponent<PlayerInventory>();
            if (inv != null && inv.IsFull) return "[E] Take " + displayName + "   (inventory full - [G] to drop something)";
            return "[E] Take " + displayName;
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (Prompt(player, body) == null) return;
            var inv = body.GetComponent<PlayerInventory>();
            if (inv == null || inv.IsFull) return;
            Net.NetWorld.Act(this, 1, player);   // the host decides who gets it if two grab at once
        }

        /// <summary>Put it on the floor here, the same on every machine (dropped, or left by a player who died).</summary>
        public void Drop(Vector3 floorPoint, float yaw) { Net.NetWorld.Act(this, 2, 0, Mathf.RoundToInt(yaw), floorPoint); }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            var party = PlayerParty.Instance;
            if (action == 1)
            {
                if (Holder != null || party == null || a < 0 || a >= party.players.Length || party.players[a] == null) return false;
                var inv = party.players[a].GetComponent<PlayerInventory>();
                if (inv == null || !inv.Add(this)) return false;
                Holder = inv;
                EverTaken = true;
                gameObject.SetActive(false);
                if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.itemPickup, transform.position, 0.8f);
                party.Flash("Player " + (a + 1) + " took the " + displayName.ToLower());
                return true;
            }
            if (action == 2)
            {
                if (Holder != null) Holder.Remove(this);
                PutDown(v, Quaternion.Euler(0f, b, 0f));
                return true;
            }
            return false;
        }

        public void PutDown(Vector3 floorPoint, Quaternion facing)
        {
            Holder = null;
            transform.SetPositionAndRotation(floorPoint, Quaternion.Euler(0f, facing.eulerAngles.y + 90f, 0f));
            gameObject.SetActive(true);
        }

        /// <summary>Straight into this inventory (handed back by a socket, or kept through a checkpoint respawn).</summary>
        public bool Return(PlayerInventory inv)
        {
            if (inv == null || !inv.Add(this)) return false;
            Holder = inv;
            EverTaken = true;
            gameObject.SetActive(false);
            return true;
        }

        /// <summary>Taken out of play for now (the fuse cell going into its socket).</summary>
        public void Consume()
        {
            if (Holder != null) Holder.Remove(this);
            Holder = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            Lit = InTorchBeam();
            float target = !needsTorch || Lit ? 1f : 0f;
            glow = Mathf.MoveTowards(glow, target, Time.deltaTime * (target > glow ? 4f : 2.5f));
            if (glowLight != null)
            {
                glowLight.enabled = glow > 0.01f;
                glowLight.intensity = glow * glowIntensity;
            }
            if (glowRenderers != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                foreach (var r in glowRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", color * (glow * 3f));
                    r.SetPropertyBlock(block);
                }
            }
        }

        /// <summary>Any torch in the party (P4's, or the one a dead P4 lends a teammate) shining on this, unblocked.</summary>
        bool InTorchBeam()
        {
            var party = PlayerParty.Instance;
            if (party == null) return false;
            Vector3 c = transform.position + Vector3.up * 0.12f;
            foreach (var l in party.Torches())
            {
                Vector3 d = c - l.transform.position;
                float dist = d.magnitude;
                if (dist > l.range) continue;
                if (Vector3.Angle(l.transform.forward, d) > l.spotAngle * 0.5f) continue;
                bool blocked = false;
                foreach (var h in Physics.RaycastAll(l.transform.position, d / dist, dist - 0.2f, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.GetComponentInParent<RemasteredPlayerController>() == null && !h.collider.transform.IsChildOf(transform)) { blocked = true; break; }
                if (!blocked) return true;
            }
            return false;
        }
    }
}
