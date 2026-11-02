using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowTower.Net
{
    /// <summary>
    /// LAST BELL's start screen (the Lobby scene). Main page: your name, HOST (others on the same Wi-Fi see the game
    /// appear in their list), JOIN (tap a game in the list, or type the host's IP), PLAY SOLO (offline, F1-F4 to steer
    /// the others), and the 2-player switch (each player gets two players' abilities). Lobby page: the seats, ready
    /// flags, and for the host where to start and START.
    /// Test launches: -autohost &lt;level&gt; hosts and starts once someone has joined and is ready; -autojoin &lt;ip&gt;.
    /// </summary>
    public class LobbyMenu : MonoBehaviour
    {
        public const string GameName = "LAST BELL";

        string playerName, address = "192.168.", status = "";
        string startAt = NetLobby.DevStartLevel;
        int soloSize = 4;
        GUIStyle title, tag, bigBtn, btn, label, small, field, seatName;
        Texture2D white;
        Vector2 scroll;
        string autoStart;
        bool autoReady;

        void Start()
        {
            playerName = NetBootstrap.PlayerName;
            AudioListener.volume = PlayerPrefs.GetFloat("LastBell_Volume", 1f);
            if (!CinemaScreen.IntroDone) CinemaScreen.I.ShowIntro();
            // behind the menu: slow passes through the levels themselves (recorded in the game's own scenes)
            backdrop = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            backdropRT = new RenderTexture(1280, 720, 0);
            backdrop.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture; backdrop.targetTexture = backdropRT;
            backdrop.url = CinemaScreen.Url("lobby_backdrop.mp4"); backdrop.isLooping = true; backdrop.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            backdrop.Play();
            var b = NetBootstrap.Ensure();
            b.Status += s => status = s;
            b.Discovery.StartListening();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-autohost") { autoStart = args[i + 1]; b.Host(playerName + "'s game"); }
                if (args[i] == "-autojoin") { autoReady = true; b.Join(args[i + 1]); }
            }
        }

        void OnDestroy()
        {
            if (NetBootstrap.Instance != null && NetBootstrap.Instance.Discovery != null && !Net.Active) NetBootstrap.Instance.Discovery.StopListening();
        }

        void Update()
        {
            var lobby = NetLobby.Instance;
            if (lobby == null || !Net.Active) return;
            if (autoReady) foreach (var s in lobby.Seats) if (s.ClientId == Net.LocalId) { if (!s.Ready) lobby.ToggleReadyRpc(); autoReady = false; }
            if (autoStart != null && Net.Manager.IsServer && lobby.Seats.Count > 1 && lobby.CanStart) { lobby.StartGame(autoStart); autoStart = null; }
        }

        enum Page { Main, Solo, Multi, Settings, HowTo }
        string devStart;
        UnityEngine.Video.VideoPlayer backdrop; RenderTexture backdropRT;
        Page page = Page.Main;

        void Styles()
        {
            if (title != null) return;
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
            // everything inherits the LAST BELL skin (UiStyle.Use was called first)
            title = UiStyle.Text(48);
            tag = UiStyle.Text(16);
            bigBtn = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            label = new GUIStyle(UiStyle.Text(17, TextAnchor.MiddleLeft));
            small = new GUIStyle(UiStyle.Text(14, TextAnchor.UpperLeft)); small.normal.textColor = UiStyle.Dim;
            field = new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 4, 4) };
            seatName = new GUIStyle(UiStyle.Text(19, TextAnchor.MiddleLeft));
        }

        void OnGUI()
        {
            UiStyle.Use();
            Styles();
            float W = Screen.width, H = Screen.height;
            // black, with the faintest red breathing at the edges, like an emergency light down a hall
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, H), white);
            GUI.color = Color.white;
            if (backdrop != null && backdrop.frame > 0) GUI.DrawTexture(new Rect(0, 0, W, H), backdropRT, ScaleMode.ScaleAndCrop, false);
            GUI.color = new Color(0f, 0f, 0f, page == Page.Main && !Net.Active ? 0.35f : 0.55f);   // dim it under the slabs
            GUI.DrawTexture(new Rect(0, 0, W, H), white);
            GUI.color = Color.white;
            if (Net.Active) { Sub(NetLobby.Instance != null ? "THE LOBBY" : "CONNECTING...", () => { if (NetLobby.Instance != null) InLobby(); }); return; }
            switch (page)
            {
                case Page.Main: MainPage(W, H); break;
                case Page.Solo: Sub("SOLO MODE", SoloPage); break;
                case Page.Multi: Sub("MULTIPLAYER", Menu); break;
                case Page.Settings: Sub("SETTINGS", SettingsPage); break;
                case Page.HowTo: Sub("HOW TO PLAY", HowToPage); break;
            }
            if (!string.IsNullOrEmpty(status)) UiStyle.Label(new Rect(0, H - 34, W, 28), status, 14, TextAnchor.MiddleCenter, UiStyle.Dim);
        }

        /// <summary>The front page, laid out like the poster: the bracketed title, two big crooked slabs, two small, quit.</summary>
        void MainPage(float W, float H)
        {
            float u = Mathf.Min(W / 1800f, H / 1000f);
            float cx = W * 0.5f, top = H * 0.06f;
            var tr = new Rect(cx - 560 * u, top, 1120 * u, 210 * u);
            UiStyle.Title(tr, Mathf.RoundToInt(190 * u));
            UiStyle.Monster(new Rect(tr.xMax - 40 * u, tr.yMax - 120 * u, 150 * u, 112 * u));
            float bw = 1050 * u, bh = 150 * u, y = tr.yMax + 50 * u;
            var solo = new Rect(cx - bw * 0.5f, y, bw, bh);
            UiStyle.Claws(new Rect(solo.x + 30 * u, solo.y - 44 * u, 170 * u, 56 * u));
            UiStyle.Monster(new Rect(solo.xMax - 190 * u, solo.y - 96 * u, 128 * u, 96 * u), true);
            if (UiStyle.Button(solo, "SOLO MODE", Mathf.RoundToInt(78 * u), 1.6f)) page = Page.Solo;
            y += bh + 40 * u;
            if (UiStyle.Button(new Rect(cx - bw * 0.48f, y, bw * 0.96f, bh * 0.95f), "MULTIPLAYER", Mathf.RoundToInt(78 * u), -0.9f)) page = Page.Multi;
            y += bh + 44 * u;
            float sw = bw * 0.44f, sh = bh * 0.62f;
            var how = new Rect(cx + 20 * u, y, sw, sh);
            UiStyle.Monster(new Rect(how.x + sw * 0.55f, how.y - 70 * u, 90 * u, 68 * u));
            if (UiStyle.Button(new Rect(cx - sw - 20 * u, y, sw, sh), "SETTINGS", Mathf.RoundToInt(46 * u), 1.4f)) page = Page.Settings;
            if (UiStyle.Button(how, "HOW TO PLAY", Mathf.RoundToInt(46 * u), -0.7f)) page = Page.HowTo;
            y += sh + 40 * u;
            if (UiStyle.Button(new Rect(cx - bw * 0.25f, y, bw * 0.5f, sh * 0.9f), "QUIT", Mathf.RoundToInt(42 * u), 0.8f)) Application.Quit();
            UiStyle.Label(new Rect(0, H - 30, W, 24), "The bell rang hours ago. You're still inside.", 15, TextAnchor.MiddleCenter, UiStyle.Dim);
        }

        /// <summary>A sub-page: the title small at the top, a big crooked panel, BACK.</summary>
        void Sub(string heading, System.Action body)
        {
            float W = Screen.width, H = Screen.height;
            UiStyle.Title(new Rect(0, 14, W, 90), 76);
            float w = Mathf.Min(760f, W - 60f), x = (W - w) * 0.5f, y = 140f;
            var panel = new Rect(x - 24, y - 16, w + 48, H - y - 80);
            UiStyle.Panel(panel, -0.5f);
            UiStyle.Label(new Rect(x, y - 6, w, 44), heading, 30);
            GUILayout.BeginArea(new Rect(x, y + 46, w, panel.height - 90));
            body();
            GUILayout.EndArea();
            if (!Net.Active && UiStyle.Button(new Rect(W * 0.5f - 110, H - 70, 220, 52), "BACK", 22, 1.2f)) page = Page.Main;
        }

        void SoloPage()
        {
            GUILayout.Label("How many players? Offline, you steer every body yourself (F1-F4).", small);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            foreach (int n in new[] { 1, 2, 4 })
            {
                GUI.backgroundColor = soloSize == n ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                if (GUILayout.Button(n + (n == 1 ? " PLAYER" : " PLAYERS") + (soloSize == n ? "  <" : ""), bigBtn, GUILayout.Height(64))) soloSize = n;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label(soloSize == 1 ? "One body with every ability: sees hidden doors, pits and enemies, carries the torch, the compass and four slots."
                          : soloSize == 2 ? "Two bodies, each with two players' abilities." : "All four survivors, each seeing what the others can't.", small);
            GUILayout.Space(18);
            if (GUILayout.Button("START", bigBtn, GUILayout.Height(70)))
            {
                PlayerParty.Size = soloSize;
                string level = DevTools.Enabled && devStart != null ? devStart : NetLobby.DevStartLevel;
                if (level == NetLobby.FirstLevel) CinemaScreen.I.PlayCutscene(() => SceneManager.LoadScene(level), true);
                else { CinemaScreen.I.ShowLoading(); SceneManager.LoadScene(level); }
            }
            if (DevTools.Enabled)
            {
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Label("[DEV] start at:", small, GUILayout.Width(120));
                foreach (var l in new[] { NetLobby.FirstLevel, NetLobby.TowerLevel, NetLobby.CampusLevel })
                    if (GUILayout.Toggle((devStart ?? NetLobby.FirstLevel) == l, l == NetLobby.FirstLevel ? "Tutorial" : l == NetLobby.TowerLevel ? "Tower" : "Campus", btn, GUILayout.Height(30))) devStart = l;
                GUILayout.EndHorizontal();
            }
        }

        void SettingsPage()
        {
            GUILayout.Label("Volume", label);
            float v = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f, GUILayout.Height(24));
            if (Mathf.Abs(v - AudioListener.volume) > 0.001f) { AudioListener.volume = v; PlayerPrefs.SetFloat("LastBell_Volume", v); }
            GUILayout.Space(16);
            if (GUILayout.Button(Screen.fullScreen ? "FULL SCREEN: ON" : "FULL SCREEN: OFF", bigBtn, GUILayout.Height(56))) Screen.fullScreen = !Screen.fullScreen;
            GUILayout.Space(10);
            string[] q = QualitySettings.names;
            int qi = QualitySettings.GetQualityLevel();
            if (q.Length > 0 && GUILayout.Button("GRAPHICS: " + q[qi].ToUpper(), bigBtn, GUILayout.Height(56))) QualitySettings.SetQualityLevel((qi + 1) % q.Length, true);
            GUILayout.Space(10);
            GUILayout.Label("Your name (shown to friends)", label);
            string n = GUILayout.TextField(playerName, 24, field, GUILayout.Height(40));
            if (n != playerName) { playerName = n; NetBootstrap.PlayerName = n; }
        }

        void HowToPage()
        {
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("MOVE  WASD    LOOK  mouse    RUN  Shift    CROUCH  C    USE  E    DROP  G    TORCH  F (P4)", label);
            GUILayout.Space(8);
            GUILayout.Label("Four survivors, four gifts - nobody gets out alone, so talk:", label);
            for (int i = 0; i < 4; i++) GUILayout.Label("P" + (i + 1) + "  -  " + PlayerParty.Abilities[i], small);
            GUILayout.Space(8);
            GUILayout.Label("Monsters hunt by sight and sound. Hide, break their line of sight, and never sprint near them. Puzzles are in the world itself - books, mirrors, wheels, statues - and a wrong move is loud.", small);
            GUILayout.Space(8);
            GUILayout.Label("On the campus: the security control room watches every camera. One of you sits at the console while the others carry walkie-talkies - hold B to talk.", small);
            GUILayout.EndScrollView();
        }

        void Menu()
        {
            GUILayout.Label("Your name", label);
            string n = GUILayout.TextField(playerName, 24, field, GUILayout.Height(40));
            if (n != playerName) { playerName = n; NetBootstrap.PlayerName = n; }
            GUILayout.Space(12);
            if (GUILayout.Button("HOST A GAME", bigBtn, GUILayout.Height(62))) NetBootstrap.Instance.Host(playerName + "'s game");
            GUILayout.Label("Friends on the same Wi-Fi will see your game in their list.", small);
            GUILayout.Space(12);
            GUILayout.Label("JOIN A GAME", seatName);
            var found = NetBootstrap.Instance.Discovery.DiscoveredSessions;
            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(130));
            if (found.Count == 0) GUILayout.Label("Looking for games on this Wi-Fi...", small);
            foreach (var s in new List<LanSessionInfo>(found))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.HostName + "   (" + s.CurrentPlayers + "/" + s.MaxPlayers + ")", label);
                if (s.CurrentPlayers < s.MaxPlayers && GUILayout.Button("JOIN", btn, GUILayout.Width(100), GUILayout.Height(36))) NetBootstrap.Instance.Join(s.HostAddress, s.Port);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUILayout.Label("or host's IP:", label, GUILayout.Width(120));
            address = GUILayout.TextField(address, 40, field, GUILayout.Height(40));
            if (GUILayout.Button("JOIN", btn, GUILayout.Width(100), GUILayout.Height(40))) NetBootstrap.Instance.Join(address);
            GUILayout.EndHorizontal();
        }

        void InLobby()
        {
            var lobby = NetLobby.Instance;
            bool host = Net.Manager.IsServer;
            GUILayout.Label(host ? "Your game is open.  Friends join from their list, or with your IP: " + LanDiscovery.ResolveLocalIPv4() : "Joined - waiting for the host to start", label);
            GUILayout.Space(8);
            var seats = lobby.Seats;
            PlayerParty.TwoPlayer = lobby.TwoPlayer;
            for (int p = 0; p < lobby.Capacity; p++)
            {
                LobbySeat? seat = null;
                foreach (var s in seats) if (s.Player == p) seat = s;
                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(46));
                GUI.color = PlayerParty.Colors[p];
                GUILayout.Label("P" + (p + 1), bigBtn, GUILayout.Width(54));
                GUI.color = Color.white;
                GUILayout.BeginVertical();
                if (seat.HasValue)
                {
                    var s = seat.Value;
                    GUILayout.Label(s.Name + (s.IsHost ? "  (host)" : "") + (s.ClientId == Net.LocalId ? "  - you" : "") + (s.Ready ? "   READY" : "   not ready"), seatName);
                }
                else GUILayout.Label("(empty - anyone can switch to this player in game)", small);
                GUILayout.Label(PlayerParty.AbilityText(p), small);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("READY", btn, GUILayout.Height(40))) lobby.ToggleReadyRpc();
            if (GUILayout.Button("LEAVE", btn, GUILayout.Height(40), GUILayout.Width(120))) NetBootstrap.Instance.Leave();
            GUILayout.EndHorizontal();
            if (host)
            {
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                bool two = GUILayout.Toggle(lobby.TwoPlayer, " 2-player game", GUILayout.Width(150));
                if (two != lobby.TwoPlayer && (!two || seats.Count <= 2)) lobby.TwoPlayer = two;
                if (DevTools.Enabled)
                {
                    GUILayout.Label("[DEV] start at:", label, GUILayout.Width(130));
                    if (GUILayout.Toggle(startAt == NetLobby.FirstLevel, "Tutorial", GUILayout.Width(90))) startAt = NetLobby.FirstLevel;
                    if (GUILayout.Toggle(startAt == NetLobby.TowerLevel, "The tower", GUILayout.Width(100))) startAt = NetLobby.TowerLevel;
                    if (GUILayout.Toggle(startAt == NetLobby.CampusLevel, "Campus (3)", GUILayout.Width(110))) startAt = NetLobby.CampusLevel;
                }
                else startAt = NetLobby.FirstLevel;
                GUILayout.EndHorizontal();
                GUI.enabled = lobby.CanStart;
                if (GUILayout.Button(lobby.CanStart ? "START" : "Waiting for everyone to be ready", bigBtn, GUILayout.Height(54))) lobby.StartGame(startAt);
                GUI.enabled = true;
            }
        }
    }
}
