using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The puzzle on screen, for the local player only: a wiring grid (a puzzle box) or a keypad with its note of
    /// clues (a lock). While it is open the player stands still with the mouse free, and the world carries on: the
    /// monsters keep moving, so solving it in a dark room is the frightening part. It closes on Esc or Close, when the
    /// steered player changes or dies, and when the puzzle is solved, failed, or re-scrambled by someone else.
    /// F10 shows the proof overlay: the seed, the verdict, and the turns each tile of the built route still needs.
    /// </summary>
    public class PuzzlePanel : MonoBehaviour
    {
        public static bool IsOpen { get { return (instance != null && instance.owner != null) || ExternalModal || GameMenu.Open; } }
        /// <summary>Another full-screen panel that should hold the player still with the mouse free (the surveillance console).</summary>
        public static bool ExternalModal;
        /// <summary>F10: show each puzzle's seed and the solver's proof that it can be solved.</summary>
        public static bool ShowProof;

        static PuzzlePanel instance;

        PuzzleState owner;
        int player, seed;
        CircuitPuzzle board;
        int budget;
        CodeLock code;
        PuzzleLock keypad;
        int[] guess = new int[CodeLock.Digits];
        System.Action onSolved;
        string message;
        float messageUntil;
        GUIStyle title, body, small, cell, digit, proofStyle;

        static PuzzlePanel Get()
        {
            if (instance == null) instance = new GameObject("PuzzlePanel").AddComponent<PuzzlePanel>();
            return instance;
        }

        public static void OpenCircuit(PuzzleState owner, int player, CircuitPuzzle board, int budget)
        {
            var p = Get();
            p.Close();
            p.owner = owner; p.player = player; p.seed = owner.Seed; p.board = board; p.budget = budget; p.code = null;
            p.Opened();
        }

        NeedleLock needle;
        int stage, misses;
        float needleT, pauseUntil, flashUntil;
        bool lastHit;

        public static void OpenNeedle(NeedleLock owner, int player, System.Action onSolved)
        {
            var p = Get();
            p.Close();
            p.owner = p.needle = owner; p.player = player; p.seed = owner.Seed; p.code = null; p.board = null;
            p.onSolved = onSolved; p.stage = 0; p.misses = 0; p.needleT = Random.value * 2f; p.pauseUntil = Time.time + 0.6f;
            p.Opened();
        }

        public static void OpenCodeLock(PuzzleLock owner, int player, System.Action onSolved)
        {
            var p = Get();
            p.Close();
            p.owner = p.keypad = owner; p.player = player; p.seed = owner.Seed; p.code = owner.Code; p.board = null;
            p.onSolved = onSolved;
            for (int i = 0; i < p.guess.Length; i++) p.guess[i] = 0;
            p.Opened();
        }

        void Opened()
        {
            message = null;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Close()
        {
            if (owner == null) return;
            owner = null; board = null; code = null; keypad = null; needle = null; onSolved = null;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (owner == null) return;
            var party = PlayerParty.Instance;
            if (party == null || party.CurrentIndex != player || party.Current == null || party.Current.IsDead || party.Spectating
                || owner.Solved || owner.Seed != seed || InputWrap.Down(KeyCode.Escape)) { Close(); return; }
            Cursor.lockState = CursorLockMode.None;   // nothing else gets to grab the mouse back while it's open
            Cursor.visible = true;
            if (needle != null)
            {
                if (Time.time >= pauseUntil) needleT += Time.deltaTime * needle.SpeedAt(stage) / 140f;
                if (InputWrap.Down(KeyCode.Space)) StopNeedle();
            }
        }

        /// <summary>Needle angle across the dial, -70..70, sweeping back and forth.</summary>
        float NeedleAngle { get { return (Mathf.PingPong(needleT, 1f) - 0.5f) * 140f; } }

        void StopNeedle()
        {
            if (needle == null || Time.time < pauseUntil) return;
            float c, w; needle.Band(stage, out c, out w);
            lastHit = Mathf.Abs(NeedleAngle - c) <= w * 0.5f;
            flashUntil = Time.time + 0.5f; pauseUntil = Time.time + 0.7f;
            var o = needle; int who = player;
            if (lastHit)
            {
                stage++;
                if (SfxBank.Instance != null) SfxBank.Flat(SfxBank.Instance.socketPower, 0.5f, 0.8f + stage * 0.15f);
                if (stage >= o.hitsNeeded) { var done = onSolved; Close(); o.ReportSolved(who); if (done != null) done(); }
                return;
            }
            misses++;
            o.ReportMiss(who);
            if (misses > o.missesAllowed) { Close(); o.ReportFailed(who); return; }
            message = o.missMessage + " (" + (o.missesAllowed - misses + 1) + " more and it's over)";
            messageUntil = Time.time + 2.5f;
        }

        void DrawNeedle(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 12f, r.width, 32f), needle.title, title);
            GUI.Label(new Rect(r.x + 24f, r.y + 46f, r.width - 48f, 50f), needle.instructions + "   Caught: " + stage + " / " + needle.hitsNeeded, body);
            var pivot = new Vector2(r.x + r.width * 0.5f, r.y + 400f);
            float radius = 210f;
            float c, w; needle.Band(Mathf.Min(stage, needle.hitsNeeded - 1), out c, out w);
            var m = GUI.matrix;
            // the dial: ticks from -70 to 70, red with the yellow band
            for (float a = -70f; a <= 70.01f; a += 2f)
            {
                bool inBand = Mathf.Abs(a - c) <= w * 0.5f;
                var col = inBand ? new Color(1f, 0.85f, 0.15f) : new Color(0.45f, 0.08f, 0.06f);
                GUI.matrix = m; GUIUtility.RotateAroundPivot(a, pivot);
                Solid(new Rect(pivot.x - 3.5f, pivot.y - radius, 7f, inBand ? 34f : 26f), col);
            }
            // the needle
            bool flash = Time.time < flashUntil;
            GUI.matrix = m; GUIUtility.RotateAroundPivot(NeedleAngle, pivot);
            Solid(new Rect(pivot.x - 2.5f, pivot.y - radius + 6f, 5f, radius - 6f), flash ? (lastHit ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.25f, 0.2f)) : new Color(0.92f, 0.94f, 0.96f));
            GUI.matrix = m;
            Solid(new Rect(pivot.x - 12f, pivot.y - 12f, 24f, 24f), new Color(0.6f, 0.62f, 0.66f));
            for (int i = 0; i < needle.hitsNeeded; i++)
                Solid(new Rect(r.x + r.width * 0.5f - needle.hitsNeeded * 18f + i * 36f + 6f, r.y + 420f, 24f, 24f), i < stage ? new Color(0.3f, 1f, 0.4f) : new Color(0.2f, 0.22f, 0.25f));
            GUI.backgroundColor = new Color(0.8f, 0.6f, 0.15f);
            if (GUI.Button(new Rect(r.x + r.width * 0.5f - 90f, r.y + 456f, 180f, 36f), "STOP  [SPACE]")) StopNeedle();
            GUI.backgroundColor = Color.white;
            if (ShowProof) GUI.Label(new Rect(r.x, r.y + 96f, r.width, 20f), "band " + c.ToString("F0") + " +- " + (w * 0.5f).ToString("F0") + "  needle " + NeedleAngle.ToString("F0"), small);
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.82f, 0.45f);
            body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft };
            body.normal.textColor = new Color(0.9f, 0.92f, 0.95f);
            small = new GUIStyle(body) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            small.normal.textColor = new Color(0.65f, 0.8f, 0.7f);
            cell = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cell.normal.textColor = new Color(0.2f, 0.9f, 1f);
            proofStyle = new GUIStyle(small) { fontSize = 11, wordWrap = false };
            digit = new GUIStyle(GUI.skin.box) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            digit.normal.textColor = new Color(0.4f, 1f, 0.55f);
        }

        void OnGUI()
        {
            UiStyle.Use();
            if (owner == null) return;
            Styles();
            GUI.depth = -10;
            float w = 560f, h = board != null ? 560f : 600f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            UiStyle.Panel(r, -0.5f);
            if (board != null) DrawCircuit(r); else if (code != null) DrawCodeLock(r); else if (needle != null) DrawNeedle(r);
            if (owner == null) return;   // solved or failed this frame

            if (message != null && Time.time < messageUntil)
                GUI.Label(new Rect(r.x, r.yMax - 92f, r.width, 24f), message, small);
            if (ShowProof) GUI.Label(new Rect(r.x + 6f, r.yMax - 66f, r.width - 12f, 22f), "[F10 proof] " + owner.Describe() + (board != null ? "  numbers = turns left on the route" : ""), proofStyle);
            if (GUI.Button(new Rect(r.x + r.width * 0.5f - 70f, r.yMax - 40f, 140f, 30f), "Close  [Esc]")) Close();
            // TEMPORARY dev aid: solve it outright (a lockbox also hands over its fuse cell)
            GUI.backgroundColor = new Color(0.85f, 0.35f, 0.9f);
            if (DevTools.Enabled && GUI.Button(new Rect(r.xMax - 128f, r.y + 10f, 118f, 26f), "[DEV] Skip")) DevSkip();
            GUI.backgroundColor = Color.white;
        }

        void DevSkip()
        {
            var o = owner;
            int who = player;
            var done = onSolved;
            Close();
            o.ReportSolved(who);
            if (done != null) done();
            var box = o as PuzzleBox;
            var party = PlayerParty.Instance;
            if (box == null || box.item == null || party == null || who < 0 || who >= party.players.Length) return;
            var body = party.players[who];
            var inv = body != null ? body.GetComponent<PlayerInventory>() : null;
            if (inv != null && inv.IsFull) { party.Flash("Lockbox opened - inventory full, the " + box.item.displayName.ToLower() + " is still inside", 3f); return; }
            box.item.Interact(who, body);
        }

        void DrawCircuit(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 12f, r.width, 32f), "FUSE BOX WIRING", title);
            int left = budget - board.Moves;
            GUI.Label(new Rect(r.x + 24f, r.y + 48f, r.width - 48f, 50f),
                "Turn the tiles until the power reaches the fuse. Left-click turns a tile clockwise, right-click turns it back.  Turns left before the alarm: " + left, body);
            int n = board.size;
            float cs = Mathf.Min(70f, 320f / n), gap = 4f;
            float gw = cs * n + gap * (n - 1);
            float gx = r.x + (r.width - gw) * 0.5f, gy = r.y + 112f;
            var powered = board.Powered();
            var live = new Color(1f, 0.74f, 0.2f);
            var dead = new Color(0.36f, 0.38f, 0.42f);

            // the power coming in on the left and the fuse waiting on the right
            float inY = gy + board.inRow * (cs + gap) + cs * 0.5f, outY = gy + board.outRow * (cs + gap) + cs * 0.5f;
            Solid(new Rect(gx - 46f, inY - cs * 0.11f, 46f, cs * 0.22f), live);
            Solid(new Rect(gx - 64f, inY - 16f, 22f, 32f), live);
            GUI.Label(new Rect(gx - 92f, inY - 34f, 60f, 18f), "POWER", small);
            bool done = board.IsSolved;
            Solid(new Rect(gx + gw, outY - cs * 0.11f, 46f, cs * 0.22f), done ? live : dead);
            Solid(new Rect(gx + gw + 42f, outY - 18f, 26f, 36f), done ? live : new Color(0.55f, 0.15f, 0.12f));
            GUI.Label(new Rect(gx + gw + 26f, outY - 38f, 60f, 18f), "FUSE", small);

            var e = Event.current;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    var cr = new Rect(gx + x * (cs + gap), gy + y * (cs + gap), cs, cs);
                    Solid(cr, new Color(0.1f, 0.11f, 0.12f));
                    Solid(new Rect(cr.x + 1f, cr.y + 1f, cr.width - 2f, cr.height - 2f), new Color(0.05f, 0.055f, 0.06f));
                    // the wire: a hub in the middle and an arm to each side it connects
                    var c = powered[i] ? live : dead;
                    float t = cs * 0.22f, mid = cs * 0.5f;
                    int sides = board.Sides(i);
                    Solid(new Rect(cr.x + mid - t * 0.5f, cr.y + mid - t * 0.5f, t, t), c);
                    if ((sides & CircuitPuzzle.N) != 0) Solid(new Rect(cr.x + mid - t * 0.5f, cr.y, t, mid), c);
                    if ((sides & CircuitPuzzle.S) != 0) Solid(new Rect(cr.x + mid - t * 0.5f, cr.y + mid, t, mid), c);
                    if ((sides & CircuitPuzzle.W) != 0) Solid(new Rect(cr.x, cr.y + mid - t * 0.5f, mid, t), c);
                    if ((sides & CircuitPuzzle.E) != 0) Solid(new Rect(cr.x + mid, cr.y + mid - t * 0.5f, mid, t), c);
                    if (ShowProof && board.TurnsLeft(i) > 0) GUI.Label(cr, board.TurnsLeft(i).ToString(), cell);
                    if (e.type == EventType.MouseDown && cr.Contains(e.mousePosition) && (e.button == 0 || e.button == 1))
                    {
                        e.Use();
                        TurnTile(i, e.button == 0);
                        if (owner == null) return;
                    }
                }
        }

        static void Solid(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void TurnTile(int i, bool clockwise)
        {
            board.TurnTile(i, clockwise);
            if (SfxBank.Instance != null) SfxBank.Flat(SfxBank.Instance.itemPickup, 0.35f, 1.6f + Random.value * 0.2f);
            var o = owner;
            if (board.IsSolved) { Close(); o.ReportSolved(player); return; }
            if (board.Moves >= budget) { Close(); o.ReportFailed(player); }
        }

        void DrawCodeLock(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 12f, r.width, 32f), "KEYPAD LOCK", title);
            if (keypad.hiddenCode)
                GUI.Label(new Rect(r.x + 24f, r.y + 56f, r.width - 48f, 150f), keypad.hiddenHint, body);
            else
            {
                GUI.Label(new Rect(r.x + 24f, r.y + 46f, r.width - 48f, 24f), "A note is taped beside the keypad:", body);
                float y = r.y + 74f;
                foreach (var c in code.clues)
                {
                    GUI.Label(new Rect(r.x + 36f, y, r.width - 60f, 24f), "-  " + c, body);
                    y += 26f;
                }
            }
            float dw = 84f, gap = 24f;
            float dx = r.x + (r.width - (dw * CodeLock.Digits + gap * (CodeLock.Digits - 1))) * 0.5f;
            float dy = r.y + 260f;
            for (int i = 0; i < CodeLock.Digits; i++)
            {
                float x = dx + i * (dw + gap);
                if (GUI.Button(new Rect(x, dy, dw, 32f), "+")) guess[i] = (guess[i] + 1) % 10;
                GUI.Box(new Rect(x, dy + 38f, dw, 84f), guess[i].ToString(), digit);
                if (GUI.Button(new Rect(x, dy + 128f, dw, 32f), "-")) guess[i] = (guess[i] + 9) % 10;
            }
            int triesLeft = keypad.TriesLeft;
            GUI.Label(new Rect(r.x, dy + 168f, r.width, 22f), triesLeft == 1 ? "Last try - a wrong code sets off the alarm" : triesLeft + " tries before the alarm", small);
            GUI.backgroundColor = new Color(0.25f, 0.6f, 0.35f);
            if (GUI.Button(new Rect(r.x + r.width * 0.5f - 90f, dy + 196f, 180f, 36f), "ENTER CODE")) Enter();
            GUI.backgroundColor = Color.white;
        }

        void Enter()
        {
            var o = owner;
            if (code.Check(guess))
            {
                var done = onSolved;
                Close();
                o.ReportSolved(player);
                if (done != null) done();
                return;
            }
            keypad.TriesLeft--;   // kept on the lock: closing and reopening doesn't give the tries back
            if (SfxBank.Instance != null) SfxBank.Flat(SfxBank.Instance.doorLocked, 0.8f);
            if (keypad.TriesLeft <= 0) { Close(); o.ReportFailed(player); return; }
            message = "Wrong code. The keypad buzzes.";
            messageUntil = Time.time + 2.5f;
        }
    }
}
