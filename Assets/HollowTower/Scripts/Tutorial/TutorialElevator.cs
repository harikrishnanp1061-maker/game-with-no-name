using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The tutorial's way out: the working elevator is dead until the fuse cell from the dark room goes into its power
    /// module (E at the panel). Then anyone standing in the cab presses E and the doors close on everyone: the whole
    /// party goes down to the tower, waking up in floor 1's spawn elevator.
    /// </summary>
    public class TutorialElevator : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public string cellId = "fuse_cell";
        public Collider cab;              // the trigger box inside the elevator
        public Light glow;                // lights up once powered
        public GameObject installedCell;  // shown in the empty socket once the cell is in
        public bool Powered { get; private set; }
        bool leaving;
        float leaveAt;
        GUIStyle style;

        void Start() { if (installedCell != null) installedCell.SetActive(false); if (glow != null) glow.color = new Color(1f, 0.15f, 0.1f); }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (Powered) return null;
            var inv = body.GetComponent<PlayerInventory>();
            return inv != null && inv.Find(cellId) != null ? "[E] Put the fuse cell in the elevator's power module" : "Elevator power module - an empty socket for a fuse cell";
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (Powered) return;
            var inv = body.GetComponent<PlayerInventory>();
            if (inv != null && inv.Find(cellId) != null) Net.NetWorld.Act(this, 1, player);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            var party = PlayerParty.Instance;
            if (action == 1)
            {
                if (Powered || party == null || a < 0 || a >= party.players.Length || party.players[a] == null) return false;
                var inv = party.players[a].GetComponent<PlayerInventory>();
                var cell = inv != null ? inv.Find(cellId) : null;
                if (cell == null) return false;
                cell.Consume();
                Powered = true;
                if (installedCell != null) installedCell.SetActive(true);
                if (glow != null) glow.color = new Color(0.3f, 1f, 0.45f);
                if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.socketPower, transform.position, 1f, 1f, 25f);
                party.Flash("The elevator hums to life - everyone in, [E] to go down", 4f);
                return true;
            }
            if (action == 2)
            {
                if (!Powered || leaving) return false;
                leaving = true;
                leaveAt = Time.time + 2.5f;
                if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorClose, transform.position, 1f, 0.8f, 20f);
                if (party != null) party.Flash("Going down...", 3f);
                return true;
            }
            return false;
        }

        void Update()
        {
            var party = PlayerParty.Instance;
            if (leaving)
            {
                if (party != null) party.SetFade(Mathf.Clamp01(1f - (leaveAt - Time.time) / 2.5f));
                if (Time.time >= leaveAt && Net.Net.IsAuthority) { leaving = false; Net.NetLobby.LoadLevel(Net.NetLobby.TowerLevel); }
                return;
            }
            if (!Powered || party == null || party.Current == null || cab == null) return;
            if (cab.bounds.Contains(party.Current.transform.position + Vector3.up) && InputWrap.Down(KeyCode.E)) Net.NetWorld.Act(this, 2);
        }

        void OnGUI()
        {
            var party = PlayerParty.Instance;
            if (!Powered || leaving || party == null || party.Current == null || cab == null) return;
            if (!cab.bounds.Contains(party.Current.transform.position + Vector3.up)) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect((Screen.width - 420f) * 0.5f, Screen.height * 0.72f, 420f, 48f), "[ E ] GO DOWN TO THE TOWER", style);
        }
    }
}
