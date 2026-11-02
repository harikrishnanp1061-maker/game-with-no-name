using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// A steel lockbox over a fuse cell. Its seams glow amber only while a torch beam is on it, so in a dark room it has
    /// to be found with the light. E opens its lock, a wiring panel generated from the box's seed (CircuitPuzzle) with a
    /// budget of turns (the built route's turns plus slack). Power the fuse and the lid swings open so the cell can be
    /// taken; run out of turns and the alarm goes off (PuzzleState). Built over the cells at load by PuzzleDirector.
    /// </summary>
    public class PuzzleBox : PuzzleState, IPartyInteractable
    {
        public PartyItem item;
        public int size = 4;
        public int minMoves = 4;   // route tiles turned out of place
        public int slack = 6;
        public Transform lidHinge;
        public Renderer[] seams;
        public Light glowLight;
        public float glowIntensity = 1.2f;
        public Color glowColor = new Color(1f, 0.62f, 0.18f);

        CircuitPuzzle board;
        int boardSeed;
        float glow, lidOpen;
        MaterialPropertyBlock block;
        Collider body;

        public bool Lit { get; private set; }

        public CircuitPuzzle Board
        {
            get
            {
                // kept per seed: closing the panel and opening it again doesn't hand back the switches used
                if (board == null || boardSeed != Seed) { board = new CircuitPuzzle(size, Seed, minMoves); boardSeed = Seed; }
                return board;
            }
        }

        public int Budget { get { return Board.Needed + slack; } }

        void Start()
        {
            body = GetComponent<Collider>();
            Lock(true);
        }

        /// <summary>The cell can't be seen, lit or taken while the box is shut.</summary>
        void Lock(bool shut)
        {
            if (item == null) return;
            foreach (var c in item.GetComponentsInChildren<Collider>(true)) c.enabled = !shut;
            item.enabled = !shut;
            if (shut && item.glowLight != null) item.glowLight.enabled = false;
            if (body != null) body.enabled = shut;
        }

        public string Prompt(int player, RemasteredPlayerController who)
        {
            if (Solved) return null;
            if (Jammed) return "The lockbox alarm is still ringing - " + Mathf.CeilToInt(JamLeft) + " s";
            return Lit ? "[E] Open the lockbox - something glows inside" : "[E] Open the lockbox";
        }

        public void Interact(int player, RemasteredPlayerController who) { Begin(player); }

        protected override void ShowPanel(int player) { PuzzlePanel.OpenCircuit(this, player, Board, Budget); }

        protected override void OnSolvedHere(int player)
        {
            Lock(false);
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("The lockbox clicks open - Player " + (player + 1) + " cracked it");
        }

        public override string Describe()
        {
            if (Seed == 0) return "Lockbox " + size + "x" + size + ": not generated yet (rolled on first use)";
            var b = Board;
            return "Lockbox wiring " + size + "x" + size + "  seed " + Seed + "  -  built route: " + b.Needed + " turns (budget " + Budget + ")  SOLVABLE";
        }

        protected override void Update()
        {
            base.Update();
            Lit = !Solved && PartyItem.TorchOn(transform.position + Vector3.up * 0.25f, transform);
            float target = Lit ? 1f : 0f;
            glow = Mathf.MoveTowards(glow, target, Time.deltaTime * (target > glow ? 4f : 2f));
            if (Jammed) glow = Mathf.PingPong(Time.time * 4f, 1f);   // the alarm flashes it red
            Color c = Jammed ? new Color(1f, 0.1f, 0.05f) : glowColor;
            if (glowLight != null)
            {
                glowLight.enabled = glow > 0.01f;
                glowLight.color = c;
                glowLight.intensity = glow * glowIntensity * Gfx.Lumens;
            }
            if (seams != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                foreach (var r in seams)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor(Gfx.EmissiveId, Gfx.ToNits(c * (0.05f + glow * 3f)));
                    r.SetPropertyBlock(block);
                }
            }
            if (Solved && lidHinge != null && lidOpen < 1f)
            {
                lidOpen = Mathf.MoveTowards(lidOpen, 1f, Time.deltaTime * 1.4f);
                lidHinge.localRotation = Quaternion.Euler(-110f * Mathf.SmoothStep(0f, 1f, lidOpen), 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ building one over a cell
        static Material steel, seam;

        /// <summary>A lockbox standing over item (the cell's position is its bottom centre).</summary>
        public static PuzzleBox Build(PartyItem item, Transform parent, string name, int size, int minMoves)
        {
            if (steel == null) steel = Gfx.Lit(new Color(0.16f, 0.17f, 0.18f), 0.45f, 0.7f);
            if (seam == null) seam = Gfx.Emissive(new Color(0.2f, 0.12f, 0.05f), new Color(0.05f, 0.03f, 0f));
            const float W = 0.5f, H = 0.42f, T = 0.025f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(item.transform.position, Quaternion.Euler(0f, item.transform.eulerAngles.y, 0f));
            var t = go.transform;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, H * 0.5f, 0f);
            col.size = new Vector3(W, H, W);

            Gfx.Box("Floor", t, new Vector3(0f, T * 0.5f, 0f), new Vector3(W, T, W), steel, false);
            Gfx.Box("Front", t, new Vector3(0f, H * 0.5f, W * 0.5f - T * 0.5f), new Vector3(W, H, T), steel, false);
            Gfx.Box("Back", t, new Vector3(0f, H * 0.5f, -W * 0.5f + T * 0.5f), new Vector3(W, H, T), steel, false);
            Gfx.Box("Left", t, new Vector3(-W * 0.5f + T * 0.5f, H * 0.5f, 0f), new Vector3(T, H, W), steel, false);
            Gfx.Box("Right", t, new Vector3(W * 0.5f - T * 0.5f, H * 0.5f, 0f), new Vector3(T, H, W), steel, false);
            var seams = new System.Collections.Generic.List<Renderer>();
            // a glowing seam round the box under the lid, and a keypad window on the front
            foreach (var s in new[] { new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f), new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f) })
            {
                bool z = s.x == 0f;
                var g = Gfx.Box("Seam", t, new Vector3(s.x * (W * 0.5f + 0.004f), H - 0.05f, s.z * (W * 0.5f + 0.004f)),
                                z ? new Vector3(W * 0.96f, 0.018f, 0.006f) : new Vector3(0.006f, 0.018f, W * 0.96f), seam, false);
                seams.Add(g.GetComponent<Renderer>());
            }
            seams.Add(Gfx.Box("Keypad", t, new Vector3(0f, H * 0.45f, W * 0.5f + 0.006f), new Vector3(0.16f, 0.12f, 0.008f), seam, false).GetComponent<Renderer>());

            var hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(0f, H, -W * 0.5f);
            Gfx.Box("Lid", hinge, new Vector3(0f, T * 0.5f, W * 0.5f), new Vector3(W + 0.01f, T, W + 0.01f), steel, false);

            var box = go.AddComponent<PuzzleBox>();
            box.item = item;
            box.size = size;
            box.minMoves = minMoves;
            box.lidHinge = hinge;
            box.seams = seams.ToArray();
            box.glowLight = Gfx.PointLight(t, new Vector3(0f, H + 0.25f, 0f), box.glowColor, 0f, 2.6f, LightShadows.None);
            box.glowLight.enabled = false;
            return box;
        }
    }
}
