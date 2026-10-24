using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// A floor's objective line (top centre) and P4's compass target; one per floor, and the one shown is the one for
    /// the floor the steered player is on. Floor 1: the fuse cell, then the stairwell socket, then the stairs. Floor 2:
    /// the two fuse cells hidden there, then the exit elevator (which wants floor 1's cell too).
    /// </summary>
    public class FloorObjective : MonoBehaviour
    {
        public int floorIndex = 0;
        public PartyItem fuse;
        public FuseSocket socket;
        public Transform stairs;
        public int compassOwner = 3;

        [Header("Text (floor 1 by default; the tutorial sets its own)")]
        public string findText = "Find the fuse cell to power the stairwell";
        public string carryText = "Bring the fuse cell to the stairwell lock (north-west)";
        public string poweredText = "Take the fuse cell up the stairs - the exit elevator needs it";
        public string socketLabel = "SOCKET", exitLabel = "STAIRS";

        [Header("Floor 2")]
        public PartyItem[] cells;
        public TowerExitElevator elevator;
        [Header("Tutorial")]
        public TutorialElevator tutorialLift;

        [Header("Stair checkpoint")]
        [Tooltip("The staircase room (world xz). Once a living player walks in with the stairwell powered, a wipe restarts the party here, fuse cell kept.")]
        public Rect checkpointRoom = Rect.MinMaxRect(8f, 80f, 24f, 88f);

        // survives the scene reload after a wipe (static), cleared when play mode starts
        static bool checkpointReached;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCheckpoint() { checkpointReached = false; }

        static readonly System.Collections.Generic.List<FloorObjective> all = new System.Collections.Generic.List<FloorObjective>();
        /// <summary>The objective of the floor the steered player (or the teammate a spectator watches) is on.</summary>
        public static FloorObjective Current
        {
            get
            {
                var view = PlayerParty.Active;
                int floor = view != null ? Mathf.Clamp(Mathf.FloorToInt((view.position.y + 0.5f) / 4.2f), 0, 1) : 0;
                FloorObjective any = null;
                foreach (var o in all) { if (o.floorIndex == floor) return o; any = o; }
                return any;
            }
        }
        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>Where P4's compass needle points (CompassModel).</summary>
        public Vector3? CompassTarget { get { string l; return Target(out l); } }

        void Start()
        {
            if (checkpointReached && floorIndex == 0) StartCoroutine(RespawnAtCheckpoint());
        }

        void Update()
        {
            if (checkpointReached || socket == null || !socket.Powered) return;
            var party = PlayerParty.Instance;
            if (party == null) return;
            foreach (var p in party.players)
            {
                if (p == null || p.IsDead) continue;
                Vector3 pos = p.transform.position;
                if (pos.y < 6f && checkpointRoom.Contains(new Vector2(pos.x, pos.z)))
                {
                    checkpointReached = true;
                    party.Flash("Checkpoint - if everyone dies, you start again here");
                    return;
                }
            }
        }

        System.Collections.IEnumerator RespawnAtCheckpoint()
        {
            yield return null;   // let the party take control of its bodies first
            var party = PlayerParty.Instance;
            if (party == null) yield break;
            if (socket != null) socket.RestorePowered();
            Vector2 c = checkpointRoom.center;
            for (int i = 0; i < party.players.Length; i++)
            {
                var p = party.players[i];
                if (p == null) continue;
                var cc = p.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                p.transform.SetPositionAndRotation(new Vector3(checkpointRoom.xMax - 1.5f, 0.1f, checkpointRoom.yMin + 1.5f + i * 1.6f), Quaternion.Euler(0f, -90f, 0f));
                if (cc != null) cc.enabled = true;
            }
            // the floor's fuse cell comes along: P4 (four slots) carries it, else whoever has room
            if (fuse != null && fuse.Holder == null)
            {
                bool given = false;
                for (int k = 3; k >= 0 && !given; k--)
                    if (party.players[k] != null) given = fuse.Return(party.players[k].GetComponent<PlayerInventory>());
            }
            party.Flash("Back at the stairwell checkpoint");
        }

        GUIStyle titleStyle, smallStyle, needleStyle;
        Texture2D white, ring;

        Vector3? Target(out string label)
        {
            if (floorIndex == 1)
            {
                // the nearest cell still lying somewhere, else the elevator
                var view = PlayerParty.Active;
                PartyItem best = null; float bd = float.MaxValue;
                if (cells != null)
                    foreach (var c in cells)
                    {
                        if (c == null || c.Holder != null || !c.gameObject.activeInHierarchy) continue;
                        float d = view != null ? Vector3.Distance(view.position, c.transform.position) : 0f;
                        if (d < bd) { bd = d; best = c; }
                    }
                if (best != null) { label = "FUSE"; return best.transform.position; }
                label = "ELEVATOR";
                return elevator != null ? elevator.transform.position : (Vector3?)null;
            }
            if ((socket != null && socket.Powered) || (tutorialLift != null && tutorialLift.Powered)) { label = exitLabel; return stairs != null ? stairs.position : (Vector3?)null; }
            if (fuse != null && fuse.Holder != null) { label = socketLabel; return socket != null ? socket.transform.position : tutorialLift != null ? tutorialLift.transform.position : (Vector3?)null; }
            label = "FUSE";
            return fuse != null ? fuse.transform.position : (Vector3?)null;
        }

        string Objective()
        {
            if (floorIndex == 1)
            {
                int found = 0;
                if (cells != null) foreach (var c in cells) if (c != null && c.EverTaken) found++;
                int total = cells != null ? cells.Length : 0;
                if (elevator != null && elevator.CellsIn >= elevator.cellsNeeded) return "The elevator has power - everyone in, [E] to escape";
                if (found < total) return "Find the fuse cells hidden on this floor (" + found + " / " + total + ") - hide when the music stops";
                return "Bring all three fuse cells to the exit elevator (locker room, south-east)";
            }
            if ((socket != null && socket.Powered) || (tutorialLift != null && tutorialLift.Powered)) return poweredText;
            if (fuse != null && fuse.Holder != null) return carryText;
            if (fuse != null && fuse.EverTaken) return "Pick the fuse cell back up";
            return findText;
        }

        void OnGUI()
        {
            var party = PlayerParty.Instance;
            if (party == null || party.Current == null || RemasteredMapHUD.IsFullMapOpen || Current != this) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            float cx = Screen.width * 0.5f;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(cx - 230f, 10f, 460f, 26f), white);
            GUI.color = new Color(1f, 0.92f, 0.7f, 1f);
            GUI.Label(new Rect(cx - 230f, 10f, 460f, 26f), Objective(), titleStyle);
            GUI.color = Color.white;

            // P4's compass is a model held in the hand now (CompassModel), not a dial on the screen
        }

        void DrawCompass(Vector2 c, float radius, Transform from, Vector3 target, string label)
        {
            if (ring == null) ring = MakeRing(128);
            var col = PlayerParty.Colors[compassOwner];
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(c.x - radius - 4f, c.y - radius - 4f, (radius + 4f) * 2f, (radius + 4f) * 2f), ring);
            GUI.color = new Color(col.r, col.g, col.b, 0.9f);
            GUI.DrawTexture(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), ring);

            Vector3 d = target - from.position;
            float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float rel = Mathf.DeltaAngle(from.eulerAngles.y, bearing);   // 0 = straight ahead

            // needle: a bar from the centre, rotated so "up" on screen is the way the player faces
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(rel, c);
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - 2f, c.y - radius + 6f, 4f, radius - 6f), white);
            GUI.DrawTexture(new Rect(c.x - 7f, c.y - radius + 4f, 14f, 6f), white);
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(c.x - 1.5f, c.y, 3f, radius * 0.45f), white);
            GUI.matrix = m;

            float dist = new Vector2(d.x, d.z).magnitude;
            string level = d.y > 2.5f ? "  ABOVE" : d.y < -2.5f ? "  BELOW" : "";
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(c.x - 60f, c.y + radius + 6f, 120f, 18f), white);
            GUI.color = col;
            GUI.Label(new Rect(c.x - 60f, c.y + radius + 6f, 120f, 18f), label + "  " + Mathf.RoundToInt(dist) + " m" + level, smallStyle);
            GUI.color = Color.white;
        }

        static Texture2D MakeRing(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 2.2f);
                    // tick marks at the four quarters
                    float ang = Mathf.Atan2(y + 0.5f - r, x + 0.5f - r) * Mathf.Rad2Deg;
                    if (d > r - 14f && d < r - 4f && Mathf.Abs(Mathf.DeltaAngle(ang, Mathf.Round(ang / 90f) * 90f)) < 2.5f) a = 1f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }
    }
}
