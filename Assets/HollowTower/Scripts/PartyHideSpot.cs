using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// A locker or cupboard a player can climb into with E (the floor 2 builder puts one on every locker bank and tall
    /// cabinet). Inside, the body is hidden and can't move, the torch goes off, and the view is the slatted dark of the
    /// door with a little room to look about; E again climbs out. Party guests can't sense a hidden player, but one that
    /// watched them climb in comes and drags them out.
    /// </summary>
    public class PartyHideSpot : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public static readonly List<PartyHideSpot> All = new List<PartyHideSpot>();
        public static event System.Action<PartyHideSpot, RemasteredPlayerController> Entered;

        public string label = "locker";
        [Tooltip("Where the body stands inside (world, on the floor).")]
        public Vector3 inside;
        [Tooltip("The way out (world, horizontal).")]
        public Vector3 facing = Vector3.forward;
        public float eyeHeight = 1.5f;

        public RemasteredPlayerController Occupant { get; private set; }
        public Vector3 ExitPosition { get { return new Vector3(inside.x, inside.y, inside.z) + facing.normalized * 0.95f + Vector3.up * 0.05f; } }

        Renderer[] hiddenRenderers;
        bool torchWas;
        int enteredFrame;
        float lookYaw, lookPitch;
        static Texture2D pixel;
        static GUIStyle style;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); if (Occupant != null) Release(Occupant, ExitPosition); }

        /// <summary>The spot this body is hiding in, or null.</summary>
        public static PartyHideSpot HiddenIn(RemasteredPlayerController body)
        {
            if (body == null) return null;
            foreach (var s in All) if (s.Occupant == body) return s;
            return null;
        }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (Occupant == body || HiddenIn(body) != null) return null;
            if (Occupant != null) return "Someone is already hiding in here";
            return "[E] Hide in the " + label;
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (Occupant != null || body == null || body.IsDead || body.IsFrozen || HiddenIn(body) != null) return;
            Net.NetWorld.Act(this, 1, player);   // the host decides: someone may have got in first, a guest may be watching
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            var party = PlayerParty.Instance;
            if (party == null || a < 0 || a >= party.players.Length || party.players[a] == null) return false;
            var body = party.players[a];
            if (action == 1)
            {
                if (Occupant != null || body.IsDead || HiddenIn(body) != null) return false;
                if (Net.Net.IsAuthority && body.IsFrozen) return false;
                Enter(body);
                return true;
            }
            if (Occupant != body) return false;
            Release(body, ExitPosition);   // 2: climbs out, 3: dragged out
            return true;
        }

        void Enter(RemasteredPlayerController body)
        {
            Occupant = body;
            enteredFrame = Time.frameCount;
            if (Entered != null) Entered(this, body);   // while they're still outside: whoever can see them now saw them get in
            lookYaw = lookPitch = 0f;
            body.SetFrozen(true);
            hiddenRenderers = body.GetComponentsInChildren<Renderer>();
            var shown = new List<Renderer>();
            foreach (var r in hiddenRenderers) if (r.enabled) { r.enabled = false; shown.Add(r); }
            hiddenRenderers = shown.ToArray();
            torchWas = body.flashlightLight != null && body.flashlightLight.enabled;
            if (body.flashlightLight != null) body.flashlightLight.enabled = false;
            if (Net.NetBody.Mine(body))   // whoever steers it moves it in; everyone else sees it arrive
            {
                body.transform.SetPositionAndRotation(inside, Quaternion.LookRotation(facing, Vector3.up));
                Aim(body);
            }
            Creak(false);
        }

        void Update()
        {
            if (Occupant == null) return;
            if (Occupant.IsDead) { Release(Occupant, Occupant.transform.position); return; }
            var party = PlayerParty.Instance;
            if (party == null || party.Current != Occupant) return;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                lookYaw = Mathf.Clamp(lookYaw + InputWrap.MouseDelta.x * Occupant.mouseSensitivity, -50f, 50f);
                lookPitch = Mathf.Clamp(lookPitch - InputWrap.MouseDelta.y * Occupant.mouseSensitivity, -35f, 35f);
            }
            Aim(Occupant);
            if (Time.frameCount > enteredFrame + 1 && InputWrap.Down(KeyCode.E)) Net.NetWorld.Act(this, 2, System.Array.IndexOf(party.players, Occupant));
        }

        void Aim(RemasteredPlayerController body)
        {
            if (body.cameraHolder == null) return;
            body.cameraHolder.localPosition = new Vector3(0f, eyeHeight, 0.1f);
            body.cameraHolder.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
        }

        /// <summary>A guest who saw them get in pulls them out (they die where they land).</summary>
        public void DragOut(RemasteredPlayerController body)
        {
            if (body != Occupant || PlayerParty.Instance == null) return;
            Net.NetWorld.Act(this, 3, System.Array.IndexOf(PlayerParty.Instance.players, body));
        }

        void Release(RemasteredPlayerController body, Vector3 at)
        {
            Occupant = null;
            if (hiddenRenderers != null) foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            hiddenRenderers = null;
            if (body == null) return;
            if (body.flashlightLight != null && !body.IsDead) body.flashlightLight.enabled = torchWas && body.flashlightEnabled;
            if (Net.NetBody.Mine(body)) body.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Vector3.up));
            if (!body.IsDead) { body.SetFrozen(false); Creak(true); }
        }

        void Creak(bool open)
        {
            var bank = SfxBank.Instance;
            if (bank != null) SfxBank.At(open ? bank.doorOpen : bank.doorClose, inside + Vector3.up * 1.2f, 0.35f, Random.Range(1.15f, 1.35f), 9f);
        }

        // the view from inside: dark, through the door's slats
        void OnGUI()
        {
            var party = PlayerParty.Instance;
            if (Occupant == null || party == null || party.Current != Occupant) return;
            if (pixel == null) { pixel = new Texture2D(1, 1); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); }
            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), pixel);
            GUI.color = new Color(0.02f, 0.02f, 0.025f, 0.96f);
            float slat = h / 9f;
            for (int i = 0; i < 9; i++) GUI.DrawTexture(new Rect(0, i * slat, w, slat * 0.62f), pixel);
            GUI.DrawTexture(new Rect(0, 0, w * 0.2f, h), pixel);
            GUI.DrawTexture(new Rect(w * 0.8f, 0, w * 0.2f, h), pixel);
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.color = new Color(0.85f, 0.85f, 0.9f, 0.85f);
            GUI.Label(new Rect(0, h - 60f, w, 30f), "Hiding - hold your breath.  [E] Get out", style);
            GUI.color = Color.white;
        }
    }
}
