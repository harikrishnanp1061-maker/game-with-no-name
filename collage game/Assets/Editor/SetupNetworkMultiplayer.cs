using System;
using System.IO;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlipFall.Networking;

public static class SetupNetworkMultiplayer
{
    private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

    [MenuItem("FLIP//FALL/Setup Network Multiplayer", false, 100)]
    public static void RunFullMultiplayerSetup()
    {
        Debug.Log("=== Starting FLIP//FALL Network Multiplayer Setup ===");

        // Step 1: Configure TutorialScene
        ConfigureTutorialScene();

        // Step 2: Build LobbyScene
        CreateLobbyScene();

        // Step 3: Configure Build Settings
        ConfigureBuildSettings();

        Debug.Log("=== FLIP//FALL Network Multiplayer Setup Complete! ===");
    }

    public static void ConfigureTutorialScene()
    {
        Scene currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != TutorialScenePath)
        {
            EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
        }

        // 1. Add NetworkGameManager
        var netGameMgrObj = GameObject.Find("NetworkGameManager");
        if (netGameMgrObj == null)
        {
            netGameMgrObj = new GameObject("NetworkGameManager");
            Undo.RegisterCreatedObjectUndo(netGameMgrObj, "Create NetworkGameManager");
        }

        var netGameMgr = netGameMgrObj.GetComponent<NetworkGameManager>();
        if (netGameMgr == null) netGameMgr = netGameMgrObj.AddComponent<NetworkGameManager>();

        var netObjMgr = netGameMgrObj.GetComponent<NetworkObject>();
        if (netObjMgr == null) netObjMgr = netGameMgrObj.AddComponent<NetworkObject>();

        // 2. Configure 4 player characters
        string[] playerNames = { "Player_1", "Player_2", "Player_3", "Player_4" };
        PlayerIdentity[] identities = { PlayerIdentity.Player1, PlayerIdentity.Player2, PlayerIdentity.Player3, PlayerIdentity.Player4 };

        for (int i = 0; i < playerNames.Length; i++)
        {
            GameObject pGo = GameObject.Find(playerNames[i]);
            if (pGo == null)
            {
                Debug.LogWarning($"[SetupNetworkMultiplayer] Could not find {playerNames[i]} in TutorialScene!");
                continue;
            }

            // Ensure NetworkObject
            var netObj = pGo.GetComponent<NetworkObject>();
            if (netObj == null) netObj = pGo.AddComponent<NetworkObject>();

            // Ensure OwnerNetworkTransform
            var ont = pGo.GetComponent<OwnerNetworkTransform>();
            if (ont == null) ont = pGo.AddComponent<OwnerNetworkTransform>();
            ont.Interpolate = true;

            // Ensure NetworkPlayerBridge
            var bridge = pGo.GetComponent<NetworkPlayerBridge>();
            if (bridge == null) bridge = pGo.AddComponent<NetworkPlayerBridge>();

            EditorUtility.SetDirty(pGo);
            Debug.Log($"[SetupNetworkMultiplayer] Configured {playerNames[i]} with NetworkObject, OwnerNetworkTransform, NetworkPlayerBridge.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[SetupNetworkMultiplayer] TutorialScene configured and saved successfully.");
    }

    public static void CreateLobbyScene()
    {
        Scene lobbyScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Camera & Lighting
        var camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camObj.tag = "MainCamera";
        var cam = camObj.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1.0f); // Dark industrial slate
        camObj.transform.position = new Vector3(0, 1, -10);

        var lightObj = new GameObject("Directional Light", typeof(Light));
        var light = lightObj.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.6f, 0.7f, 0.9f);
        light.intensity = 0.5f;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // 2. NetworkManager Persistent Core
        var netMgrObj = new GameObject("NetworkManager");
        var netMgr = netMgrObj.AddComponent<NetworkManager>();
        var transport = netMgrObj.AddComponent<UnityTransport>();
        var bootstrap = netMgrObj.AddComponent<NetworkManagerBootstrap>();
        var discovery = netMgrObj.AddComponent<LanDiscovery>();

        var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/LobbyManager.prefab");
        var soBootstrap = new SerializedObject(bootstrap);
        soBootstrap.FindProperty("_lobbyManagerPrefab").objectReferenceValue = lobbyPrefab;
        soBootstrap.ApplyModifiedProperties();

        transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
        netMgr.NetworkConfig.NetworkTransport = transport;
        netMgr.NetworkConfig.EnableSceneManagement = true;
        netMgr.NetworkConfig.TickRate = 30;
        if (lobbyPrefab != null)
        {
            netMgr.AddNetworkPrefab(lobbyPrefab);
        }

        // 3. EventSystem (New Input System)
        var esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // 4. UI Canvas
        var canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var lobbyUI = canvasObj.AddComponent<LobbyUIController>();

        // Build UI Visuals
        BuildLobbyVisuals(canvasObj.transform, lobbyUI);

        // Save LobbyScene
        EditorSceneManager.SaveScene(lobbyScene, LobbyScenePath);
        Debug.Log($"[SetupNetworkMultiplayer] Created and saved {LobbyScenePath} successfully.");
    }

    private static void BuildLobbyVisuals(Transform canvasRoot, LobbyUIController controller)
    {
        // Background Tint Overlay
        var bg = CreateUIObject("Background", canvasRoot);
        StretchFull(bg);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.035f, 0.045f, 0.07f, 0.96f);

        // Header Title
        var header = CreateText("Header_Title", canvasRoot, "FLIP // FALL", 52, FontStyles.Bold, new Color(0.95f, 0.95f, 1.0f));
        SetAnchors(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(700, 64));
        header.alignment = TextAlignmentOptions.Center;

        var subHeader = CreateText("Header_Subtitle", canvasRoot, "4-PLAYER CO-OP LAN MULTIPLAYER", 18, FontStyles.Normal, new Color(0.45f, 0.75f, 1.0f));
        SetAnchors(subHeader.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(700, 28));
        subHeader.alignment = TextAlignmentOptions.Center;

        // =====================================================================
        // MAIN MENU PANEL
        // =====================================================================
        var mainPanel = CreateUIObject("MainMenuPanel", canvasRoot);
        SetAnchors(mainPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1100, 720));

        // 1. Player Name Bar
        var nameBox = CreateUIObject("PlayerNameBox", mainPanel.transform);
        SetAnchors(nameBox.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(540, 50));
        var nameBoxImg = nameBox.AddComponent<Image>();
        nameBoxImg.color = new Color(0.1f, 0.13f, 0.18f, 0.9f);

        var nameLabel = CreateText("Label", nameBox.transform, "PLAYER NAME:", 15, FontStyles.Bold, new Color(0.7f, 0.8f, 0.95f));
        SetAnchors(nameLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80, 0), new Vector2(140, 40));

        var nameInput = CreateInputField("PlayerNameInput", nameBox.transform, "Enter your name...");
        SetAnchors(nameInput.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-180, 0), new Vector2(320, 38));

        // 2. Left Column: Host Game & Direct Connect
        var leftCol = CreateUIObject("LeftCol_HostAndDirect", mainPanel.transform);
        SetAnchors(leftCol.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.45f, 0.88f), Vector2.zero, Vector2.zero);

        var hostBox = CreateUIObject("HostBox", leftCol.transform);
        SetAnchors(hostBox.GetComponent<RectTransform>(), new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(460, 220));
        var hostBoxImg = hostBox.AddComponent<Image>();
        hostBoxImg.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

        var hostTitle = CreateText("Title", hostBox.transform, "HOST A NEW GAME", 22, FontStyles.Bold, new Color(0.35f, 0.8f, 1.0f));
        SetAnchors(hostTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(400, 32));
        hostTitle.alignment = TextAlignmentOptions.Center;

        var hostDesc = CreateText("Desc", hostBox.transform, "Host plays as Player 1 (Leader). Other 3 players join over your Wi-Fi network. No dedicated server or internet needed.", 14, FontStyles.Normal, new Color(0.75f, 0.8f, 0.9f));
        SetAnchors(hostDesc.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(400, 60));
        hostDesc.alignment = TextAlignmentOptions.Center;

        var hostBtn = CreateButton("HostButton", hostBox.transform, "HOST LAN GAME", new Color(0.18f, 0.55f, 0.92f));
        SetAnchors(hostBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 35), new Vector2(380, 48));

        // Direct Connect Box
        var directBox = CreateUIObject("DirectConnectBox", leftCol.transform);
        SetAnchors(directBox.GetComponent<RectTransform>(), new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(460, 220));
        var directBoxImg = directBox.AddComponent<Image>();
        directBoxImg.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

        var directTitle = CreateText("Title", directBox.transform, "DIRECT IP CONNECT", 20, FontStyles.Bold, new Color(1.0f, 0.75f, 0.3f));
        SetAnchors(directTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(400, 30));
        directTitle.alignment = TextAlignmentOptions.Center;

        var directDesc = CreateText("Desc", directBox.transform, "Use if college Wi-Fi blocks UDP broadcast. Enter Host's Wi-Fi IP address shown on host screen:", 13, FontStyles.Normal, new Color(0.75f, 0.8f, 0.9f));
        SetAnchors(directDesc.rectTransform, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(400, 40));
        directDesc.alignment = TextAlignmentOptions.Center;

        var directIpInput = CreateInputField("DirectIpInput", directBox.transform, "e.g. 192.168.1.71");
        SetAnchors(directIpInput.GetComponent<RectTransform>(), new Vector2(0.5f, 0.40f), new Vector2(0.5f, 0.40f), Vector2.zero, new Vector2(380, 38));

        var directBtn = CreateButton("DirectConnectButton", directBox.transform, "JOIN DIRECT IP", new Color(0.92f, 0.55f, 0.18f));
        SetAnchors(directBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(380, 44));

        // 3. Right Column: LAN Session Browser
        var rightCol = CreateUIObject("RightCol_LanBrowser", mainPanel.transform);
        SetAnchors(rightCol.GetComponent<RectTransform>(), new Vector2(0.52f, 0f), new Vector2(1f, 0.88f), Vector2.zero, Vector2.zero);
        var rightColImg = rightCol.AddComponent<Image>();
        rightColImg.color = new Color(0.07f, 0.09f, 0.13f, 0.95f);

        var browserHeader = CreateText("BrowserTitle", rightCol.transform, "AVAILABLE LAN SESSIONS", 20, FontStyles.Bold, new Color(0.4f, 0.95f, 0.6f));
        SetAnchors(browserHeader.rectTransform, new Vector2(0.04f, 1f), new Vector2(0.65f, 1f), new Vector2(0, -32), new Vector2(0, 32));
        browserHeader.alignment = TextAlignmentOptions.MidlineLeft;

        var refreshBtn = CreateButton("RefreshButton", rightCol.transform, "REFRESH", new Color(0.2f, 0.65f, 0.4f));
        SetAnchors(refreshBtn.GetComponent<RectTransform>(), new Vector2(0.96f, 1f), new Vector2(0.96f, 1f), new Vector2(-70, -32), new Vector2(130, 36));

        // Scroll view container for sessions
        var scrollObj = CreateUIObject("ScrollView", rightCol.transform);
        SetAnchors(scrollObj.GetComponent<RectTransform>(), new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
        var scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        var contentObj = CreateUIObject("Content", scrollObj.transform);
        var contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 300f);

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRt;

        var noGamesText = CreateText("NoGamesText", scrollObj.transform, "Listening for local sessions on UDP 47777...\n\nIf no games appear:\n1. Ensure Host laptop has clicked 'Host LAN Game'\n2. Allow Windows Firewall on Private network\n3. Or use Direct IP Connect on the left", 15, FontStyles.Normal, new Color(0.6f, 0.65f, 0.75f));
        StretchFull(noGamesText.gameObject);
        noGamesText.alignment = TextAlignmentOptions.Center;

        // =====================================================================
        // LOBBY ROOM PANEL
        // =====================================================================
        var roomPanel = CreateUIObject("LobbyRoomPanel", canvasRoot);
        SetAnchors(roomPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(1200, 720));
        roomPanel.SetActive(false);

        var sessionInfo = CreateText("SessionInfoText", roomPanel.transform, "HOSTING LAN SESSION | IP: 127.0.0.1:7777 | Players: 1/4", 18, FontStyles.Bold, new Color(0.85f, 0.9f, 1.0f));
        SetAnchors(sessionInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -25), new Vector2(1100, 32));
        sessionInfo.alignment = TextAlignmentOptions.Center;

        // 4 Player Slot Cards
        var slotsContainer = CreateUIObject("PlayerSlotsContainer", roomPanel.transform);
        SetAnchors(slotsContainer.GetComponent<RectTransform>(), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1160, 360));
        var hlg = slotsContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var slotUIs = new LobbySlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            slotUIs[i] = CreatePlayerSlotCard(slotsContainer.transform, i + 1);
        }

        // Room Controls Bar
        var roomBar = CreateUIObject("RoomControlsBar", roomPanel.transform);
        SetAnchors(roomBar.GetComponent<RectTransform>(), new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(1160, 80));

        var leaveBtn = CreateButton("LeaveButton", roomBar.transform, "LEAVE LOBBY", new Color(0.6f, 0.2f, 0.2f));
        SetAnchors(leaveBtn.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(120, 0), new Vector2(220, 54));

        var readyBtn = CreateButton("ToggleReadyButton", roomBar.transform, "READY UP", new Color(0.2f, 0.75f, 0.35f));
        SetAnchors(readyBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 58));

        var startBtn = CreateButton("StartGameButton", roomBar.transform, "START GAME (ALL READY)", new Color(0.18f, 0.55f, 0.95f));
        SetAnchors(startBtn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-170, 0), new Vector2(320, 58));

        // Status Feedback Banner (At bottom)
        var bannerRoot = CreateUIObject("StatusBanner", canvasRoot);
        SetAnchors(bannerRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(900, 44));
        var bannerImg = bannerRoot.AddComponent<Image>();
        bannerImg.color = new Color(0.08f, 0.1f, 0.15f, 0.92f);

        var bannerText = CreateText("BannerText", bannerRoot.transform, "Multiplayer ready. Connect over local Wi-Fi.", 15, FontStyles.Normal, new Color(0.5f, 0.9f, 0.6f));
        StretchFull(bannerText.gameObject);
        bannerText.alignment = TextAlignmentOptions.Center;

        // Wire references into LobbyUIController using SerializedObject
        var so = new SerializedObject(controller);
        so.FindProperty("_mainMenuPanel").objectReferenceValue = mainPanel;
        so.FindProperty("_lobbyRoomPanel").objectReferenceValue = roomPanel;
        so.FindProperty("_playerNameInput").objectReferenceValue = nameInput;
        so.FindProperty("_hostGameButton").objectReferenceValue = hostBtn;
        so.FindProperty("_refreshGamesButton").objectReferenceValue = refreshBtn;
        so.FindProperty("_gamesListContent").objectReferenceValue = contentObj.transform;
        so.FindProperty("_noGamesFoundText").objectReferenceValue = noGamesText;
        so.FindProperty("_directIpInput").objectReferenceValue = directIpInput;
        so.FindProperty("_directConnectButton").objectReferenceValue = directBtn;
        so.FindProperty("_sessionInfoText").objectReferenceValue = sessionInfo;
        so.FindProperty("_toggleReadyButton").objectReferenceValue = readyBtn;
        so.FindProperty("_toggleReadyButtonText").objectReferenceValue = readyBtn.GetComponentInChildren<TextMeshProUGUI>();
        so.FindProperty("_startGameButton").objectReferenceValue = startBtn;
        so.FindProperty("_startGameButtonText").objectReferenceValue = startBtn.GetComponentInChildren<TextMeshProUGUI>();
        so.FindProperty("_leaveLobbyButton").objectReferenceValue = leaveBtn;
        so.FindProperty("_statusBannerText").objectReferenceValue = bannerText;
        so.FindProperty("_statusBannerRoot").objectReferenceValue = bannerRoot;

        var slotsProp = so.FindProperty("_playerSlots");
        slotsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotUIs[i];
        }

        so.ApplyModifiedProperties();
    }

    private static LobbySlotUI CreatePlayerSlotCard(Transform parent, int slotNumber)
    {
        var card = CreateUIObject($"Slot_{slotNumber}", parent);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.09f, 0.11f, 0.16f, 0.95f);

        var badgeText = CreateText("IdentityBadge", card.transform, $"PLAYER {slotNumber}", 20, FontStyles.Bold, Color.white);
        SetAnchors(badgeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(240, 32));
        badgeText.alignment = TextAlignmentOptions.Center;

        var roleText = CreateText("RoleDesc", card.transform, "Role description", 13, FontStyles.Normal, new Color(0.7f, 0.75f, 0.85f));
        SetAnchors(roleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -68), new Vector2(250, 32));
        roleText.alignment = TextAlignmentOptions.Center;

        var nameText = CreateText("PlayerName", card.transform, "Waiting...", 18, FontStyles.Bold, new Color(0.95f, 0.95f, 1.0f));
        SetAnchors(nameText.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(240, 40));
        nameText.alignment = TextAlignmentOptions.Center;

        var indicatorObj = CreateUIObject("Indicator", card.transform);
        SetAnchors(indicatorObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(-60, 0), new Vector2(16, 16));
        var indImg = indicatorObj.AddComponent<Image>();
        indImg.color = Color.gray;

        var readyText = CreateText("ReadyStatus", card.transform, "EMPTY", 16, FontStyles.Bold, Color.gray);
        SetAnchors(readyText.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(20, 0), new Vector2(140, 28));
        readyText.alignment = TextAlignmentOptions.MidlineLeft;

        var hostCrown = CreateText("HostCrown", card.transform, "<color=#FFCC00>★ HOST</color>", 14, FontStyles.Bold, Color.yellow);
        SetAnchors(hostCrown.rectTransform, new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(160, 24));
        hostCrown.alignment = TextAlignmentOptions.Center;
        hostCrown.gameObject.SetActive(false);

        var slotComp = card.AddComponent<LobbySlotUI>();
        var so = new SerializedObject(slotComp);
        so.FindProperty("_identityBadge").objectReferenceValue = badgeText;
        so.FindProperty("_roleDescription").objectReferenceValue = roleText;
        so.FindProperty("_playerNameText").objectReferenceValue = nameText;
        so.FindProperty("_readyStatusText").objectReferenceValue = readyText;
        so.FindProperty("_statusIndicator").objectReferenceValue = indImg;
        so.FindProperty("_hostCrown").objectReferenceValue = hostCrown.gameObject;
        so.ApplyModifiedProperties();

        return slotComp;
    }

    public static void ConfigureBuildSettings()
    {
        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(LobbyScenePath, true),
            new EditorBuildSettingsScene(TutorialScenePath, true)
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[SetupNetworkMultiplayer] Build Settings configured: [0] LobbyScene, [1] TutorialScene.");
    }

    // =====================================================================
    // UI BUILDER HELPERS
    // =====================================================================

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, FontStyles style, Color color)
    {
        var go = CreateUIObject(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color bgColor)
    {
        var go = CreateUIObject(name, parent);
        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
        cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.25f);
        cb.disabledColor = new Color(0.3f, 0.3f, 0.35f, 0.5f);
        btn.colors = cb;

        var text = CreateText("Label", go.transform, label, 16, FontStyles.Bold, Color.white);
        StretchFull(text.gameObject);
        text.alignment = TextAlignmentOptions.Center;

        return btn;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder)
    {
        var go = CreateUIObject(name, parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

        var input = go.AddComponent<TMP_InputField>();

        var textObj = CreateText("Text", go.transform, "", 16, FontStyles.Normal, Color.white);
        StretchFull(textObj.gameObject);
        textObj.alignment = TextAlignmentOptions.MidlineLeft;
        textObj.rectTransform.offsetMin = new Vector2(12, 0);
        textObj.rectTransform.offsetMax = new Vector2(-12, 0);

        var placeObj = CreateText("Placeholder", go.transform, placeholder, 15, FontStyles.Italic, new Color(0.5f, 0.55f, 0.65f));
        StretchFull(placeObj.gameObject);
        placeObj.alignment = TextAlignmentOptions.MidlineLeft;
        placeObj.rectTransform.offsetMin = new Vector2(12, 0);
        placeObj.rectTransform.offsetMax = new Vector2(-12, 0);

        input.textViewport = go.GetComponent<RectTransform>();
        input.textComponent = textObj;
        input.placeholder = placeObj;

        return input;
    }

    private static void StretchFull(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
