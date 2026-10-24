using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The stairwell lock's power socket. The stair door stays locked ("no power") until a player carrying the fuse
    /// cell puts it in; then the status lamp turns green and the door unlocks and swings open. The lock only checks the
    /// cell: once the door is open it pops back out into the player's inventory, since every floor's fuse cell is needed
    /// to run the exit elevator on the last floor.
    /// </summary>
    public class FuseSocket : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public string itemId = "fuse_cell";
        public InteractiveDoor door;
        public Renderer statusLamp;
        public Light statusLight;
        public GameObject installedFuse;
        public Color offColor = new Color(0.9f, 0.08f, 0.05f);
        public Color onColor = new Color(0.15f, 1f, 0.35f);

        public bool Powered { get; private set; }
        public static event System.Action<FuseSocket> OnPowered;

        void Start() { if (!Powered) SetLamp(false); }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (Powered) return "Stairwell lock: powered";
            var inv = body.GetComponent<PlayerInventory>();
            if (inv != null && inv.Find(itemId) != null) return "[E] Insert the fuse cell";
            return "Stairwell lock: no power - needs a fuse cell";
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (Powered) return;
            Net.NetWorld.Act(this, 1, player);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            var party = PlayerParty.Instance;
            if (action != 1 || Powered || party == null || a < 0 || a >= party.players.Length || party.players[a] == null) return false;
            return Insert(party.players[a]);
        }

        bool Insert(RemasteredPlayerController body)
        {
            var inv = body.GetComponent<PlayerInventory>();
            var fuse = inv != null ? inv.Find(itemId) : null;
            if (fuse == null) return false;
            fuse.Consume();
            StartCoroutine(GiveBack(fuse, body));
            Powered = true;
            SetLamp(true);
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.socketPower, transform.position, 1f, 1f, 25f);
            if (installedFuse != null) installedFuse.SetActive(true);
            if (door != null)
            {
                door.isLocked = false;
                if (!door.isOpen) door.ToggleDoor();
            }
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("Stairwell powered - the way up is open");
            if (OnPowered != null) OnPowered(this);
            return true;
        }

        System.Collections.IEnumerator GiveBack(PartyItem fuse, RemasteredPlayerController body)
        {
            yield return new WaitForSeconds(1.2f);
            if (installedFuse != null) installedFuse.SetActive(false);
            var inv = body != null && !body.IsDead ? body.GetComponent<PlayerInventory>() : null;
            if (fuse.Return(inv)) { if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("The fuse cell pops back out - keep it, the elevator needs it"); }
            else fuse.PutDown(transform.position + transform.right * 0.6f + Vector3.down * transform.position.y, transform.rotation);
        }

        /// <summary>Back after a wipe at the stair checkpoint: powered and open, without the fuse ceremony.</summary>
        public void RestorePowered()
        {
            Powered = true;
            SetLamp(true);
            if (installedFuse != null) installedFuse.SetActive(false);
            if (door != null)
            {
                door.isLocked = false;
                if (!door.isOpen) door.ToggleDoor();
            }
        }

        void SetLamp(bool on)
        {
            var c = on ? onColor : offColor;
            if (statusLamp != null)
            {
                var block = new MaterialPropertyBlock();
                statusLamp.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c * 0.4f);
                block.SetColor("_EmissionColor", c * 2.5f);
                statusLamp.SetPropertyBlock(block);
            }
            if (statusLight != null) statusLight.color = c;
        }
    }
}
