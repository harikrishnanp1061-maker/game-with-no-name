using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlipFall.Networking
{
    /// <summary>
    /// UI Controller for LobbyScene.unity.
    /// Manages the Main Menu (Host, LAN Browser, Direct IP) and Lobby Room (Player Slots, Ready, Start).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("FLIP//FALL/Networking/Lobby UI Controller")]
    public class LobbyUIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _lobbyRoomPanel;

        [Header("Main Menu UI")]
        [SerializeField] private TMP_InputField _playerNameInput;
        [SerializeField] private Button _hostGameButton;
        [SerializeField] private Button _refreshGamesButton;
        [SerializeField] private Transform _gamesListContent;
        [SerializeField] private GameObject _sessionEntryPrefab;
        [SerializeField] private TextMeshProUGUI _noGamesFoundText;

        [Header("Direct IP UI")]
        [SerializeField] private TMP_InputField _directIpInput;
        [SerializeField] private Button _directConnectButton;

        [Header("Lobby Room UI")]
        [SerializeField] private TextMeshProUGUI _sessionInfoText;
        [SerializeField] private LobbySlotUI[] _playerSlots; // 4 slots
        [SerializeField] private Button _toggleReadyButton;
        [SerializeField] private TextMeshProUGUI _toggleReadyButtonText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private TextMeshProUGUI _startGameButtonText;
        [SerializeField] private Button _leaveLobbyButton;

        [Header("Feedback Banner")]
        [SerializeField] private TextMeshProUGUI _statusBannerText;
        [SerializeField] private GameObject _statusBannerRoot;

        private float _statusTimer = 0f;

        private void Start()
        {
            // Set initial player name
            if (_playerNameInput != null)
            {
                _playerNameInput.text = NetworkManagerBootstrap.LocalPlayerName;
                _playerNameInput.onValueChanged.AddListener(OnPlayerNameChanged);
            }

            // Hook up buttons
            if (_hostGameButton != null) _hostGameButton.onClick.AddListener(OnHostClicked);
            if (_refreshGamesButton != null) _refreshGamesButton.onClick.AddListener(OnRefreshGamesClicked);
            if (_directConnectButton != null) _directConnectButton.onClick.AddListener(OnDirectConnectClicked);
            if (_toggleReadyButton != null) _toggleReadyButton.onClick.AddListener(OnToggleReadyClicked);
            if (_startGameButton != null) _startGameButton.onClick.AddListener(OnStartGameClicked);
            if (_leaveLobbyButton != null) _leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);

            // Hook up networking events
            if (NetworkManagerBootstrap.Instance != null)
            {
                NetworkManagerBootstrap.Instance.OnConnectionStatusMessage += ShowStatusMessage;
                NetworkManagerBootstrap.Instance.OnClientConnectionFailed += ShowErrorMessage;
                NetworkManagerBootstrap.Instance.OnClientConnectedSuccessfully += HandleConnected;
            }

            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.OnSessionsUpdated += UpdateDiscoveredSessionsUI;
                LanDiscovery.Instance.OnDiscoveryError += ShowErrorMessage;
                LanDiscovery.Instance.StartListening();
            }

            ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (NetworkManagerBootstrap.Instance != null)
            {
                NetworkManagerBootstrap.Instance.OnConnectionStatusMessage -= ShowStatusMessage;
                NetworkManagerBootstrap.Instance.OnClientConnectionFailed -= ShowErrorMessage;
                NetworkManagerBootstrap.Instance.OnClientConnectedSuccessfully -= HandleConnected;
            }

            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.OnSessionsUpdated -= UpdateDiscoveredSessionsUI;
                LanDiscovery.Instance.OnDiscoveryError -= ShowErrorMessage;
            }

            if (GameLobbyManager.Instance != null)
            {
                GameLobbyManager.Instance.OnLobbyStateChanged -= RefreshLobbyRoomUI;
                GameLobbyManager.Instance.OnLobbyMessage -= ShowStatusMessage;
            }
        }

        private void Update()
        {
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f && _statusBannerRoot != null)
                {
                    _statusBannerRoot.SetActive(false);
                }
            }
        }

        // =====================================================================
        // PANEL NAVIGATION
        // =====================================================================

        public void ShowMainMenu()
        {
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(true);
            if (_lobbyRoomPanel != null) _lobbyRoomPanel.SetActive(false);

            if (LanDiscovery.Instance != null && !LanDiscovery.Instance.IsListening)
            {
                LanDiscovery.Instance.StartListening();
            }
        }

        public void ShowLobbyRoom()
        {
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(false);
            if (_lobbyRoomPanel != null) _lobbyRoomPanel.SetActive(true);

            if (GameLobbyManager.Instance != null)
            {
                GameLobbyManager.Instance.OnLobbyStateChanged += RefreshLobbyRoomUI;
                GameLobbyManager.Instance.OnLobbyMessage += ShowStatusMessage;
                RefreshLobbyRoomUI();
            }
        }

        // =====================================================================
        // BUTTON HANDLERS
        // =====================================================================

        private void OnPlayerNameChanged(string newName)
        {
            NetworkManagerBootstrap.LocalPlayerName = newName.Trim();
        }

        private void OnHostClicked()
        {
            string pName = _playerNameInput != null ? _playerNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(pName)) pName = System.Environment.MachineName;

            ShowStatusMessage("Starting LAN Host...");
            bool ok = NetworkManagerBootstrap.Instance.HostGame(playerName: pName);
            if (ok)
            {
                ShowLobbyRoom();
            }
        }

        private void OnRefreshGamesClicked()
        {
            ShowStatusMessage("Searching LAN for sessions on UDP 47777...");
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.StopListening();
                LanDiscovery.Instance.StartListening();
            }
        }

        private void OnDirectConnectClicked()
        {
            string ip = _directIpInput != null ? _directIpInput.text.Trim() : "";
            if (string.IsNullOrEmpty(ip))
            {
                ShowErrorMessage("Please enter the Host's IP address (e.g. 192.168.1.50)");
                return;
            }

            string pName = _playerNameInput != null ? _playerNameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(pName)) pName = System.Environment.MachineName;

            ShowStatusMessage($"Connecting to {ip}:7777...");
            NetworkManagerBootstrap.Instance.JoinGame(ip, NetworkManagerBootstrap.DefaultGamePort, pName);
        }

        private void OnToggleReadyClicked()
        {
            if (GameLobbyManager.Instance != null)
            {
                GameLobbyManager.Instance.ToggleReadyServerRpc();
            }
        }

        private void OnStartGameClicked()
        {
            if (GameLobbyManager.Instance != null)
            {
                GameLobbyManager.Instance.StartGame();
            }
        }

        private void OnLeaveLobbyClicked()
        {
            ShowStatusMessage("Leaving lobby...");
            if (NetworkManagerBootstrap.Instance != null)
            {
                NetworkManagerBootstrap.Instance.Disconnect();
            }
            ShowMainMenu();
        }

        private void HandleConnected()
        {
            ShowLobbyRoom();
        }

        // =====================================================================
        // LOBBY ROOM UI REFRESH
        // =====================================================================

        public void RefreshLobbyRoomUI()
        {
            GameLobbyManager lobby = GameLobbyManager.Instance;
            if (lobby == null) return;

            var players = lobby.Players;
            bool isHost = NetworkManagerBootstrap.Instance != null && NetworkManagerBootstrap.Instance.IsHost;
            ulong localClientId = Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;

            // Session info header
            if (_sessionInfoText != null)
            {
                string localIp = LanDiscovery.ResolveLocalIPv4();
                _sessionInfoText.text = isHost
                    ? $"<b>HOSTING LAN SESSION</b> | Local IP: <color=#55FF88>{localIp}:{NetworkManagerBootstrap.DefaultGamePort}</color> | Players: {players.Count}/4"
                    : $"<b>CONNECTED TO SESSION</b> | Players: {players.Count}/4";
            }

            // Update 4 slot cards
            for (int i = 0; i < 4; i++)
            {
                PlayerIdentity targetId = (PlayerIdentity)(i + 1);
                if (_playerSlots != null && i < _playerSlots.Length && _playerSlots[i] != null)
                {
                    // Find player in slot
                    LobbyPlayerData? foundData = null;
                    for (int p = 0; p < players.Count; p++)
                    {
                        if (players[p].Identity == targetId)
                        {
                            foundData = players[p];
                            break;
                        }
                    }

                    _playerSlots[i].Bind(targetId, foundData, localClientId);
                }
            }

            // Update Ready button
            bool isLocalReady = false;
            for (int p = 0; p < players.Count; p++)
            {
                if (players[p].ClientId == localClientId)
                {
                    isLocalReady = players[p].IsReady;
                    break;
                }
            }

            if (_toggleReadyButtonText != null)
            {
                _toggleReadyButtonText.text = isLocalReady ? "CANCEL READY" : "READY UP";
            }
            if (_toggleReadyButton != null)
            {
                ColorBlock cb = _toggleReadyButton.colors;
                cb.normalColor = isLocalReady ? new Color(0.8f, 0.25f, 0.25f) : new Color(0.2f, 0.75f, 0.35f);
                _toggleReadyButton.colors = cb;
            }

            // Update Start Game button (Host only)
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(isHost);
                bool canStart = lobby.CanStartGame();
                _startGameButton.interactable = canStart;

                if (_startGameButtonText != null)
                {
                    int readyCount = 0;
                    for (int p = 0; p < players.Count; p++)
                    {
                        if (players[p].IsReady) readyCount++;
                    }
                    _startGameButtonText.text = canStart
                        ? "START GAME (ALL READY)"
                        : $"WAITING FOR READY ({readyCount}/{players.Count})";
                }
            }
        }

        // =====================================================================
        // LAN SESSIONS BROWSER
        // =====================================================================

        private void UpdateDiscoveredSessionsUI(List<LanSessionInfo> sessions)
        {
            if (_gamesListContent == null) return;

            // Clear old entries
            for (int i = _gamesListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_gamesListContent.GetChild(i).gameObject);
            }

            bool hasGames = sessions != null && sessions.Count > 0;
            if (_noGamesFoundText != null)
            {
                _noGamesFoundText.gameObject.SetActive(!hasGames);
            }

            if (!hasGames) return;

            foreach (var s in sessions)
            {
                CreateSessionEntry(s);
            }
        }

        private void CreateSessionEntry(LanSessionInfo session)
        {
            if (_gamesListContent == null) return;

            GameObject entry;
            if (_sessionEntryPrefab != null)
            {
                entry = Instantiate(_sessionEntryPrefab, _gamesListContent);
            }
            else
            {
                // Fallback default entry builder
                entry = new GameObject("SessionEntry", typeof(RectTransform), typeof(Image));
                entry.transform.SetParent(_gamesListContent, false);
                var img = entry.GetComponent<Image>();
                img.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            }

            var entryUI = entry.GetComponent<LanSessionEntryUI>();
            if (entryUI == null) entryUI = entry.AddComponent<LanSessionEntryUI>();

            entryUI.Setup(session, () =>
            {
                string pName = _playerNameInput != null ? _playerNameInput.text.Trim() : "";
                if (string.IsNullOrEmpty(pName)) pName = System.Environment.MachineName;

                ShowStatusMessage($"Joining {session.HostName} ({session.HostAddress}:{session.Port})...");
                NetworkManagerBootstrap.Instance.JoinGame(session.HostAddress, session.Port, pName);
            });
        }

        // =====================================================================
        // STATUS & ERROR BANNERS
        // =====================================================================

        public void ShowStatusMessage(string message)
        {
            if (_statusBannerText != null)
            {
                _statusBannerText.text = message;
                _statusBannerText.color = new Color(0.4f, 0.95f, 0.5f);
            }
            if (_statusBannerRoot != null) _statusBannerRoot.SetActive(true);
            _statusTimer = 5.0f;
        }

        public void ShowErrorMessage(string error)
        {
            if (_statusBannerText != null)
            {
                _statusBannerText.text = $"<color=#FF5555><b>Error:</b></color> {error}";
                _statusBannerText.color = new Color(1.0f, 0.4f, 0.4f);
            }
            if (_statusBannerRoot != null) _statusBannerRoot.SetActive(true);
            _statusTimer = 7.0f;
        }
    }

    /// <summary>
    /// UI component for a single player slot in the lobby room.
    /// </summary>
    [System.Serializable]
    public class LobbySlotUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _identityBadge;
        [SerializeField] private TextMeshProUGUI _roleDescription;
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _readyStatusText;
        [SerializeField] private Image _statusIndicator;
        [SerializeField] private GameObject _hostCrown;

        public void Bind(PlayerIdentity identity, LobbyPlayerData? playerData, ulong localClientId)
        {
            if (_identityBadge != null)
            {
                _identityBadge.text = $"PLAYER {(int)identity}";
                _identityBadge.color = GetBadgeColor(identity);
            }

            if (_roleDescription != null)
            {
                _roleDescription.text = GetRoleDescription(identity);
            }

            if (playerData.HasValue)
            {
                var data = playerData.Value;
                bool isMe = data.ClientId == localClientId;

                if (_playerNameText != null)
                {
                    _playerNameText.text = isMe ? $"<b>{data.PlayerName}</b> <size=80%>(YOU)</size>" : data.PlayerName.ToString();
                }

                if (_readyStatusText != null)
                {
                    _readyStatusText.text = data.IsReady ? "<color=#55FF88>READY</color>" : "<color=#FFAA44>NOT READY</color>";
                }

                if (_statusIndicator != null)
                {
                    _statusIndicator.color = data.IsReady ? new Color(0.25f, 0.9f, 0.35f) : new Color(0.95f, 0.6f, 0.15f);
                }

                if (_hostCrown != null)
                {
                    _hostCrown.SetActive(data.IsHost);
                }
            }
            else
            {
                // Empty slot
                if (_playerNameText != null) _playerNameText.text = "<i>Waiting for player...</i>";
                if (_readyStatusText != null) _readyStatusText.text = "<color=#666666>EMPTY</color>";
                if (_statusIndicator != null) _statusIndicator.color = new Color(0.3f, 0.3f, 0.35f, 0.5f);
                if (_hostCrown != null) _hostCrown.SetActive(false);
            }
        }

        private Color GetBadgeColor(PlayerIdentity id)
        {
            switch (id)
            {
                case PlayerIdentity.Player1: return new Color(0.35f, 0.75f, 1.0f);
                case PlayerIdentity.Player2: return new Color(1.0f, 0.65f, 0.2f);
                case PlayerIdentity.Player3: return new Color(0.4f, 0.95f, 0.45f);
                case PlayerIdentity.Player4: return new Color(0.95f, 0.85f, 0.3f);
                default: return Color.white;
            }
        }

        private string GetRoleDescription(PlayerIdentity id)
        {
            switch (id)
            {
                case PlayerIdentity.Player1: return "Leader / Perception Ability";
                case PlayerIdentity.Player2: return "Navigation / Architecture";
                case PlayerIdentity.Player3: return "Hazard Detection / Traps";
                case PlayerIdentity.Player4: return "Tactical Torch / Flashlight";
                default: return "";
            }
        }
    }

    /// <summary>
    /// UI entry for a discovered session in the LAN browser.
    /// </summary>
    public class LanSessionEntryUI : MonoBehaviour
    {
        public void Setup(LanSessionInfo session, System.Action onJoinClicked)
        {
            // Set size
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(0f, 54f);
            }

            // Remove existing children if re-used
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            // Title & IP Text
            var textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(0.75f, 1f);
            textRt.offsetMin = new Vector2(16f, 0f);
            textRt.offsetMax = new Vector2(0f, 0f);

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.text = $"<b>{session.HostName}</b>  <color=#888888>{session.HostAddress}:{session.Port}</color>  [<color=#55FF88>{session.CurrentPlayers}/{session.MaxPlayers}</color>]";

            // Join Button
            var btnObj = new GameObject("JoinButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(transform, false);
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.78f, 0.15f);
            btnRt.anchorMax = new Vector2(0.98f, 0.85f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            var btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.2f, 0.6f, 0.9f);

            var btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(() => onJoinClicked?.Invoke());

            // Button label
            var btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRt = btnTextObj.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            var btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
            btnTmp.fontSize = 16;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.text = "JOIN";
            btnTmp.color = Color.white;
        }
    }
}
