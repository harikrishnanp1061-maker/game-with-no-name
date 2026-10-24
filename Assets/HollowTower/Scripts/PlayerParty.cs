using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The four players from the collage game tutorial, all physically in the level. As in the tutorial's
    /// MultiPlayerCharacterManager, the local player steers one body at a time: F1-F4 hands control and the camera to
    /// that body, the others stay standing where they were left, and T brings the other three to the controlled one.
    /// Only the controlled body carries the "Player" tag, so the level scripts that look the player up by tag (floor
    /// culling, lamp optimiser, exit elevator) follow it.
    ///
    /// Each player has one ability (from the collage game design): P1 sees false walls, P2 sees hidden pits, P3 sees
    /// enemies that are invisible to the others, P4 carries the torch and a compass and has four inventory slots
    /// instead of two. The HUD here shows who is who, the steered player's inventory ([G] drops, mouse wheel picks the
    /// slot), short messages and the fade used when someone falls into a pit. Dead players spectate instead of
    /// respawning (PlayerPartySpectate.cs).
    /// </summary>
    public partial class PlayerParty : MonoBehaviour
    {
        [Header("Players (1-4)")]
        public RemasteredPlayerController[] players = new RemasteredPlayerController[4];
        public int startingPlayer = 0;
        public Camera playerCamera;

        [Header("Keys")]
        public KeyCode teleportTeammatesKey = KeyCode.T;
        public KeyCode dropKey = KeyCode.G;

        public static PlayerParty Instance { get; private set; }

        /// <summary>The body the camera is in: the one being steered, or the teammate a dead player is watching.</summary>
        public static Transform Active { get { return Instance != null && Instance.ViewBody != null ? Instance.ViewBody.transform : null; } }

        public RemasteredPlayerController Current { get { return current; } }
        public int CurrentIndex { get { return currentIndex; } }

        /// <summary>Index (0-3) of the player being steered; 0 when there is no party.</summary>
        public static int ActiveIndex { get { return Instance != null && Instance.currentIndex >= 0 ? Instance.currentIndex : 0; } }
        /// <summary>Raised with the new index whenever control moves to another player.</summary>
        public static event System.Action<int> ActiveChanged;

        public static readonly string[] Abilities =
        {
            "Sees hidden doors", "Sees hidden pits", "Sees invisible enemies", "Torch, compass, 4 inventory slots"
        };

        /// <summary>Two-player game: P1 has P1's and P2's abilities, P2 has P3's and P4's; bodies 3 and 4 sit out.
        /// Set by the lobby (host's choice) or the solo menu before the level loads.</summary>
        public static bool TwoPlayer;

        /// <summary>Does body i have ability a (0 hidden doors, 1 hidden pits, 2 invisible enemies, 3 torch/compass/slots)?</summary>
        public static bool BodyHas(int i, int a)
        {
            if (!TwoPlayer) return i == a;
            return i == 0 ? a <= 1 : i == 1 ? a >= 2 : false;
        }

        /// <summary>What body i can do, for the HUD and lobby.</summary>
        public static string AbilityText(int i)
        {
            if (!TwoPlayer) return Abilities[i];
            return i == 0 ? Abilities[0] + " + " + Abilities[1].ToLower() : i == 1 ? Abilities[2] + " + " + Abilities[3].ToLower() : "(sits out)";
        }

        // two-player: P2's body gets P4's torch and pockets, bodies 3 and 4 are parked out of the way
        void ApplyTwoPlayer()
        {
            var p2 = players[1]; var p4 = players[3];
            if (p2 != null && p4 != null && p4.flashlightLight != null && !p2.hasTorch)
            {
                var src = p4.flashlightLight.transform;
                Transform parent = p2.transform;
                if (src.parent != null)
                    foreach (var t in p2.GetComponentsInChildren<Transform>(true)) if (t.name == src.parent.name) { parent = t; break; }
                var light = Instantiate(src.gameObject, parent);
                light.transform.localPosition = src.localPosition;
                light.transform.localRotation = src.localRotation;
                p2.flashlightLight = light.GetComponent<Light>();
                p2.hasTorch = true;
                p2.flashlightEnabled = true;
            }
            if (p2 != null) { var inv = p2.GetComponent<PlayerInventory>(); if (inv != null) inv.capacity = 4; }
            for (int i = 2; i < players.Length; i++)
            {
                var b = players[i];
                if (b == null) continue;
                b.SetFrozen(true);
                b.transform.position = new Vector3(b.transform.position.x, -60f, b.transform.position.z);   // under the map, out of everyone's way
                foreach (var r in b.GetComponentsInChildren<Renderer>()) r.enabled = false;
                if (b.flashlightLight != null) b.flashlightLight.enabled = false;
            }
        }

        // Same identity colours as the collage game's PlayerIdentityManager
        public static readonly Color[] Colors =
        {
            new Color(0.25f, 0.85f, 1.0f), new Color(1.0f, 0.75f, 0.20f), new Color(0.30f, 0.95f, 0.55f), new Color(0.85f, 0.45f, 1.0f)
        };

        RemasteredPlayerController current;
        int currentIndex = -1;
        float bannerTimer;
        string message;
        float messageTimer;
        float fade;
        GUIStyle chipStyle, bannerStyle, smallStyle;
        Texture2D white;

        void Awake()
        {
            Instance = this;
            if (playerCamera == null) playerCamera = Camera.main;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // in a session you start in your own lobby seat's body, once the host has handed it over
            if (Net.Net.Active && Net.NetLobby.Instance != null)
            {
                int seat = Net.NetLobby.Instance.PlayerOf(Net.Net.LocalId);
                if (seat >= 0) startingPlayer = seat;
            }
            if (Net.Net.Active && Net.NetLobby.Instance != null) TwoPlayer = Net.NetLobby.Instance.TwoPlayer;
            if (TwoPlayer) { ApplyTwoPlayer(); if (startingPlayer > 1) startingPlayer = 0; }
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && i != startingPlayer) players[i].SetControlled(false, null);
            wantedBody = startingPlayer;
            if (Net.NetBody.Mine(players[startingPlayer])) SwitchTo(startingPlayer, true);
        }

        int wantedBody = -1;
        /// <summary>This machine asked for body i and is waiting for the host to hand it over.</summary>
        public bool WantsBody(int i) { return i >= 0 && i == wantedBody; }

        /// <summary>The body being steered was taken back (its player rejoined the seat, or the host reclaimed it).</summary>
        public void LoseBody()
        {
            if (current != null) current.SetControlled(false, null);
            Flash("Your body was taken back - F1-F4 to pick a free player", 3f);
            current = null;
        }

        void Update()
        {
            if (InputWrap.Down(KeyCode.F1)) SwitchTo(0);
            else if (InputWrap.Down(KeyCode.F2)) SwitchTo(1);
            else if (InputWrap.Down(KeyCode.F3)) SwitchTo(2);
            else if (InputWrap.Down(KeyCode.F4)) SwitchTo(3);
            if (allDead && InputWrap.Down(KeyCode.R))
            {
                // in a session only the host restarts the level, for everyone
                if (Net.Net.IsAuthority) Net.NetLobby.LoadLevel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                else Flash("Waiting for the host to restart", 2f);
                return;
            }
            if (Spectating) UpdateSpectator();
            else
            {
                if (InputWrap.Down(teleportTeammatesKey) && !RemasteredMapHUD.IsFullMapOpen) TeleportTeammatesToActive();
                var inv = current != null ? current.GetComponent<PlayerInventory>() : null;
                if (inv != null && !RemasteredMapHUD.IsFullMapOpen && !current.IsFrozen)
                {
                    if (InputWrap.Down(dropKey)) inv.DropSelected();
                    float wheel = InputWrap.Scroll;
                    if (wheel > 0.1f) inv.Cycle(-1); else if (wheel < -0.1f) inv.Cycle(1);
                }
            }
            UpdateBlessing();
            UpdatePings();
            if (bannerTimer > 0f) bannerTimer -= Time.deltaTime;
            if (messageTimer > 0f) messageTimer -= Time.deltaTime;
        }

        /// <summary>A short line of text in the middle of the screen.</summary>
        public void Flash(string text, float seconds = 3f)
        {
            message = text;
            messageTimer = seconds;
        }

        /// <summary>Black overlay (0-1) for the player being steered, e.g. while falling into a pit.</summary>
        public void SetFade(float amount) { fade = Mathf.Clamp01(amount); }

        /// <summary>Moves control and the camera into player i's body, wherever it is standing; a dead player spectates.</summary>
        public void SwitchTo(int i, bool force = false)
        {
            if (i < 0 || i >= players.Length || players[i] == null) return;
            if (TwoPlayer && i > 1) { Flash("Two-player game: only P1 and P2", 2f); return; }
            if (i == currentIndex && !force) return;
            // in a session the host hands bodies out: ask, and switch when it says yes (NetBody / NetGame)
            if (!force && Net.Net.Active && Net.NetGame.Instance != null)
            {
                wantedBody = i;
                Net.NetGame.Instance.TakeRpc(i);
                return;
            }
            if (!Net.NetBody.Mine(players[i])) return;
            wantedBody = i;
            if (current != null && current != players[i] && !current.IsDead) current.SetControlled(false, null);
            foreach (var p in players)
                if (p != null)
                {
                    p.ViewedFromInside = false;
                    if (!p.IsDead) p.gameObject.tag = "Untagged";
                }
            current = players[i];
            currentIndex = i;
            if (current.IsDead)
            {
                if (watching[i] < 0 || players[watching[i]] == null || players[watching[i]].IsDead) watching[i] = NearestLiving(current.transform.position);
                AttachSpectator();
            }
            else current.SetControlled(true, playerCamera);

            // the tag moved: let the scripts that cached the old body find the new one
            RefreshTrackers();
            bannerTimer = 2.5f;
            if (ActiveChanged != null) ActiveChanged(i);
        }

        /// <summary>Puts the three teammates in a half circle behind the controlled player (the tutorial's [T]).</summary>
        public void TeleportTeammatesToActive()
        {
            if (current == null || current.IsDead) return;
            var t = current.transform;
            // the tutorial's half circle behind first, then beside and in front when a wall is in the way
            Vector3 f = t.forward, r = t.right;
            Vector3[] offsets = { -f * 1.5f - r * 1.2f, -f * 2.0f, -f * 1.5f + r * 1.2f, -r * 1.2f, r * 1.2f, f * 1.5f - r * 1.2f, f * 1.5f + r * 1.2f, f * 2.0f, -f * 1.0f, f * 1.0f };
            var used = new System.Collections.Generic.List<Vector3>();
            foreach (var p in players)
            {
                if (p == null || p == current || p.IsDead || !Net.NetBody.Mine(p)) continue;   // someone else's player isn't ours to move
                Vector3 target = t.position - f * 0.9f;
                foreach (var o in offsets)
                {
                    Vector3 c = t.position + o;
                    bool taken = false;
                    foreach (var u in used) if ((u - c).sqrMagnitude < 0.8f) taken = true;
                    if (!taken && IsClear(c)) { target = c; break; }
                }
                used.Add(target);
                var cc = p.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                p.transform.SetPositionAndRotation(target, t.rotation);
                if (cc != null) cc.enabled = true;
            }
        }

        /// <summary>Room for a standing player at pos, reachable in a straight line from the controlled one.</summary>
        bool IsClear(Vector3 pos)
        {
            Vector3 eye = current.transform.position + Vector3.up * 1.0f;
            if (Physics.Linecast(eye, pos + Vector3.up * 1.0f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && !IsPartyCollider(hit.collider)) return false;
            foreach (var c in Physics.OverlapCapsule(pos + Vector3.up * 0.4f, pos + Vector3.up * 1.5f, 0.36f, ~0, QueryTriggerInteraction.Ignore))
                if (!IsPartyCollider(c)) return false;
            // something to stand on (not a pit)
            return Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore);
        }

        bool IsPartyCollider(Collider c)
        {
            var pc = c.GetComponentInParent<RemasteredPlayerController>();
            return pc != null;
        }

        static int FloorOf(float y)
        {
            return Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / 4.2f), 0, 3);
        }

        // ------------------------------------------------------------------ HUD: who you are, and who else is out there
        void OnGUI()
        {
            if (players == null || current == null) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            if (chipStyle == null)
            {
                chipStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }

            if (fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);
            }

            // bottom-left: P1..P4 chips, the active one filled with its colour, plus the floor each teammate is on
            float x = 14f, y = Screen.height - 46f;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                bool on = i == currentIndex;
                var r = new Rect(x, y, 74f, 32f);
                var c = Colors[i];
                bool dead = players[i].IsDead;
                if (dead) c = Color.Lerp(c, new Color(0.35f, 0.35f, 0.35f), 0.6f);
                GUI.color = on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(r, white);
                GUI.color = on ? Color.black : c;
                string where = dead ? " DEAD" : on ? "" : " F" + (FloorOf(players[i].transform.position.y) + 1);
                GUI.Label(r, "[F" + (i + 1) + "] P" + (i + 1) + where, chipStyle);
                x += 80f;
            }
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(x + 4f, y, 200f, 32f), "[" + teleportTeammatesKey + "] bring teammates", chipStyle);
            GUI.color = new Color(Colors[currentIndex].r, Colors[currentIndex].g, Colors[currentIndex].b, 0.85f);
            GUI.Label(new Rect(14f, y - 24f, 320f, 22f), "P" + (currentIndex + 1) + ": " + AbilityText(currentIndex), chipStyle);

            if (Spectating) DrawSpectatorHud(); else DrawInventory();

            if (bannerTimer > 0f)
            {
                var c = Colors[currentIndex];
                GUI.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(bannerTimer));
                GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 40f), "PLAYER " + (currentIndex + 1) + (Spectating ? "  (dead - spectating)" : ""), bannerStyle);
                GUI.Label(new Rect(0f, Screen.height * 0.3f + 34f, Screen.width, 24f), AbilityText(currentIndex), smallStyle);
            }
            if (messageTimer > 0f && !string.IsNullOrEmpty(message))
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f * Mathf.Clamp01(messageTimer));
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.42f, 560f, 30f), white);
                GUI.color = new Color(1f, 0.95f, 0.85f, Mathf.Clamp01(messageTimer));
                GUI.Label(new Rect(Screen.width * 0.5f - 280f, Screen.height * 0.42f, 560f, 30f), message, smallStyle);
            }
            if (allDead) DrawGameOver();
            GUI.color = Color.white;
        }

        /// <summary>Bottom centre: the steered player's slots, the selected one outlined.</summary>
        void DrawInventory()
        {
            var inv = current.GetComponent<PlayerInventory>();
            if (inv == null) return;
            const float s = 54f, gap = 6f;
            float w = inv.capacity * s + (inv.capacity - 1) * gap;
            float x0 = Screen.width * 0.5f - w * 0.5f, y0 = Screen.height - s - 16f;
            for (int i = 0; i < inv.capacity; i++)
            {
                var r = new Rect(x0 + i * (s + gap), y0, s, s);
                bool sel = i == inv.Selected && i < inv.Items.Count;
                GUI.color = sel ? Colors[currentIndex] : new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), white);
                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(r, white);
                if (i < inv.Items.Count && inv.Items[i] != null)
                {
                    var item = inv.Items[i];
                    GUI.color = item.color;
                    GUI.DrawTexture(new Rect(r.x + 16f, r.y + 8f, s - 32f, s - 26f), white);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(r.x - 10f, r.y + s - 20f, s + 20f, 18f), item.displayName, chipStyle);
                }
            }
            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            if (inv.Items.Count > 0) GUI.Label(new Rect(x0 + w + 8f, y0 + s * 0.5f - 11f, 130f, 22f), "[" + dropKey + "] drop", chipStyle);
            GUI.color = Color.white;
        }
    }
}
