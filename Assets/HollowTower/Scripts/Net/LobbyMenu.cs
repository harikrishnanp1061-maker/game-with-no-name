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
        string startAt = NetLobby.FirstLevel;
        bool soloTwoPlayer;
        GUIStyle title, tag, bigBtn, btn, label, small, field, seatName;
        Texture2D white;
        Vector2 scroll;
        string autoStart;
        bool autoReady;

        void Start()
        {
            playerName = NetBootstrap.PlayerName;
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

        void Styles()
        {
            if (title != null) return;
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
            title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tag = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            bigBtn = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            btn = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 18 };
            seatName = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        void Panel(Rect r, float a = 0.7f)
        {
            GUI.color = new Color(0.07f, 0.07f, 0.09f, a);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            Styles();
            // background: near-black with a slow red pulse, like an emergency light down a hall
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 0.8f);
            GUI.color = new Color(0.03f + 0.03f * pulse, 0.02f, 0.025f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = new Color(0.9f, 0.18f, 0.22f);
            GUI.Label(new Rect(0, 26, Screen.width, 80), GameName, title);
            GUI.color = new Color(0.8f, 0.78f, 0.75f, 0.8f);
            GUI.Label(new Rect(0, 100, Screen.width, 24), "The bell rang hours ago. You're still inside.", tag);
            GUI.color = Color.white;

            float w = Mathf.Min(600f, Screen.width - 40f), x = (Screen.width - w) * 0.5f, y = 140f;
            var area = new Rect(x, y, w, Screen.height - y - 20f);
            Panel(new Rect(area.x - 16, area.y - 10, area.width + 32, area.height + 10));
            GUILayout.BeginArea(area);
            if (Net.Active && NetLobby.Instance != null) InLobby();
            else if (Net.Active) GUILayout.Label("Connecting...", label);
            else Menu();
            GUILayout.FlexibleSpace();
            GUI.color = new Color(1f, 0.82f, 0.55f);
            GUILayout.Label(status, small);
            GUI.color = Color.white;
            GUILayout.EndArea();
        }

        void Menu()
        {
            GUILayout.Label("Your name", label);
            string n = GUILayout.TextField(playerName, 24, field, GUILayout.Height(32));
            if (n != playerName) { playerName = n; NetBootstrap.PlayerName = n; }
            GUILayout.Space(14);

            if (GUILayout.Button("HOST A GAME", bigBtn, GUILayout.Height(56))) NetBootstrap.Instance.Host(playerName + "'s game");
            GUILayout.Label("Friends on the same Wi-Fi will see your game in their list.", small);
            GUILayout.Space(12);

            GUILayout.Label("JOIN A GAME", seatName);
            var found = NetBootstrap.Instance.Discovery.DiscoveredSessions;
            scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(120));
            if (found.Count == 0) GUILayout.Label("  Looking for games on this Wi-Fi...", small);
            foreach (var s in new List<LanSessionInfo>(found))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.HostName + "   (" + s.CurrentPlayers + "/" + s.MaxPlayers + ")", label);
                if (s.CurrentPlayers < s.MaxPlayers && GUILayout.Button("JOIN", btn, GUILayout.Width(90), GUILayout.Height(30))) NetBootstrap.Instance.Join(s.HostAddress, s.Port);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUILayout.Label("or host's IP:", label, GUILayout.Width(100));
            address = GUILayout.TextField(address, 40, field, GUILayout.Height(30));
            if (GUILayout.Button("JOIN", btn, GUILayout.Width(90), GUILayout.Height(30))) NetBootstrap.Instance.Join(address);
            GUILayout.EndHorizontal();
            GUILayout.Space(14);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("PLAY SOLO", btn, GUILayout.Height(40))) { PlayerParty.TwoPlayer = soloTwoPlayer; SceneManager.LoadScene(NetLobby.FirstLevel); }
            soloTwoPlayer = GUILayout.Toggle(soloTwoPlayer, " 2 players", GUILayout.Width(110), GUILayout.Height(40));
            GUILayout.EndHorizontal();
            GUILayout.Label("Solo: no network - you steer every player yourself (F1-F4).", small);
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
                GUILayout.Label("Start at:", label, GUILayout.Width(70));
                if (GUILayout.Toggle(startAt == NetLobby.FirstLevel, "Tutorial", GUILayout.Width(90))) startAt = NetLobby.FirstLevel;
                if (GUILayout.Toggle(startAt == NetLobby.TowerLevel, "The tower", GUILayout.Width(100))) startAt = NetLobby.TowerLevel;
                GUILayout.EndHorizontal();
                GUI.enabled = lobby.CanStart;
                if (GUILayout.Button(lobby.CanStart ? "START" : "Waiting for everyone to be ready", bigBtn, GUILayout.Height(54))) lobby.StartGame(startAt);
                GUI.enabled = true;
            }
        }
    }
}
