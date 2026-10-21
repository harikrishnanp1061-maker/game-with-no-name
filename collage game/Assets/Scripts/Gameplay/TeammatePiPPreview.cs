using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =========================================================================
// TeammatePiPPreview - Secondary Picture-in-Picture Preview Camera & Window
//
// Allows the player to view the game from another player's perspective
// (or via a 3rd-person chase cam / room security cam) in real-time while
// actively controlling the current character, enabling full visual inspection
// of character animations (jogging, sprinting, crouching, jumping, waking up).
//
// FEATURES:
// - Real-time secondary camera rendering to dedicated RenderTexture
// - View Modes:
//     1. Teammate POV (Player 1, 2, 3, or 4 - automatically excludes active player)
//     2. Third-Person Chase Cam (floats behind active player)
//     3. Room Surveillance Cam (wide high-angle room view)
// - Tracking Mode: Teammate camera smoothly aims at the active player, ensuring
//   the moving character is always framed regardless of where the teammate faces!
// - URP Mesh Hook: Temporarily forces active player's mesh to ShadowCastingMode.On
//   during the preview camera render, so the controlled player is 100% visible
//   in the preview window with zero clipping in their own first-person camera!
// - Hotkeys:
//     F6: Toggle Preview Window
//     F7: Cycle View Target
//     F8: Toggle Active Player Tracking
// =========================================================================
[AddComponentMenu("FLIP//FALL/Dev/Teammate PiP Preview")]
[DisallowMultipleComponent]
public class TeammatePiPPreview : MonoBehaviour
{
    private static TeammatePiPPreview _instance;
    public static TeammatePiPPreview Instance => _instance;

    public enum PiPViewType
    {
        Teammate,
        ThirdPersonChase,
        RoomSurveillance
    }

    [System.Serializable]
    public struct ViewOption
    {
        public PiPViewType viewType;
        public PlayerIdentity teammateIdentity;
        public string label;
        public Color accentColor;

        public ViewOption(PiPViewType type, PlayerIdentity id, string label, Color color)
        {
            this.viewType = type;
            this.teammateIdentity = id;
            this.label = label;
            this.accentColor = color;
        }
    }

    // -----------------------------------------------------------------
    // INSPECTOR CONFIGURATION
    // -----------------------------------------------------------------
    [Header("Window Display")]
    [Tooltip("Whether the secondary preview window is rendered on screen.")]
    [SerializeField] private bool _showPiP = true;

    [Tooltip("Shortcut key to toggle preview window.")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F6;

    [Tooltip("Shortcut key to cycle through preview views.")]
    [SerializeField] private KeyCode _cycleKey = KeyCode.F7;

    [Tooltip("Shortcut key to toggle tracking active player.")]
    [SerializeField] private KeyCode _trackKey = KeyCode.F8;

    [Header("Window Dimensions")]
    [SerializeField] private int _sizePresetIndex = 1; // 0=Small, 1=Medium, 2=Large
    [SerializeField] private Vector2 _windowPosition = new Vector2(-1f, -1f); // -1 = default top-right

    [Header("Camera & Tracking Settings")]
    [Tooltip("When viewing from a teammate, smoothly track and aim camera at the actively controlled player.")]
    [SerializeField] private bool _trackActivePlayer = true;

    [SerializeField] private float _trackAimSpeed = 10.0f;
    [SerializeField] private float _chaseFollowSpeed = 12.0f;

    [Header("Resolution & Quality")]
    [SerializeField] private int _renderWidth = 640;
    [SerializeField] private int _renderHeight = 360;

    // -----------------------------------------------------------------
    // RUNTIME STATE
    // -----------------------------------------------------------------
    private RenderTexture _renderTexture;
    private Camera _previewCamera;
    private GameObject _previewCamObj;

    private readonly List<ViewOption> _availableViews = new List<ViewOption>();
    private int _currentViewIndex = 0;

    private bool _isDragging = false;
    private Vector2 _dragOffset;

    // IMGUI textures & styles
    private Texture2D _pixelTex;
    private Texture2D _gridTex;
    private GUIStyle _headerTitleStyle;
    private GUIStyle _btnStyle;
    private GUIStyle _footerStyle;
    private GUIStyle _badgeStyle;
    private bool _stylesInitialized = false;

    // Public properties for external query & dev panel
    public bool ShowPiP
    {
        get => _showPiP;
        set
        {
            _showPiP = value;
            UpdateCameraEnabled();
        }
    }

    public bool TrackActivePlayer
    {
        get => _trackActivePlayer;
        set => _trackActivePlayer = value;
    }

    public ViewOption CurrentView
    {
        get
        {
            if (_availableViews.Count == 0) RefreshAvailableViews();
            int idx = Mathf.Clamp(_currentViewIndex, 0, Mathf.Max(0, _availableViews.Count - 1));
            return _availableViews.Count > 0 ? _availableViews[idx] : new ViewOption(PiPViewType.ThirdPersonChase, PlayerIdentity.Player1, "Chase Cam", Color.magenta);
        }
    }

    public string CurrentViewLabel => CurrentView.label;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureInstance()
    {
        if (_instance == null)
        {
            TeammatePiPPreview existing = UnityEngine.Object.FindAnyObjectByType<TeammatePiPPreview>();
            if (existing == null)
            {
                GameObject go = new GameObject("TeammatePiPPreview_Manager");
                go.AddComponent<TeammatePiPPreview>();
            }
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        CreatePixelTexture();
        EnsureRenderTexture();
        EnsurePreviewCamera();
        RefreshAvailableViews();
    }

    private void OnEnable()
    {
        PlayerIdentityManager.OnIdentityChanged += HandleIdentityChanged;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        Camera.onPreRender += OnLegacyPreRender;
        Camera.onPostRender += OnLegacyPostRender;
        UpdateCameraEnabled();
    }

    private void OnDisable()
    {
        PlayerIdentityManager.OnIdentityChanged -= HandleIdentityChanged;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        Camera.onPreRender -= OnLegacyPreRender;
        Camera.onPostRender -= OnLegacyPostRender;
        UpdateCameraEnabled();
    }

    private void OnDestroy()
    {
        if (_previewCamObj != null)
        {
            Destroy(_previewCamObj);
            _previewCamObj = null;
            _previewCamera = null;
        }

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        if (_pixelTex != null)
        {
            Destroy(_pixelTex);
            _pixelTex = null;
        }

        if (_gridTex != null)
        {
            Destroy(_gridTex);
            _gridTex = null;
        }

        if (_instance == this) _instance = null;
    }

    // -----------------------------------------------------------------
    // RESOURCE MANAGEMENT
    // -----------------------------------------------------------------
    private void EnsureRenderTexture()
    {
        if (_renderTexture == null || _renderTexture.width != _renderWidth || _renderTexture.height != _renderHeight)
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            _renderTexture = new RenderTexture(_renderWidth, _renderHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "TeammatePiP_RenderTexture",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 2,
                useMipMap = false
            };
            _renderTexture.Create();

            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = _renderTexture;
            }
        }
    }

    private void EnsurePreviewCamera()
    {
        if (_previewCamera != null) return;

        _previewCamObj = new GameObject("[TeammatePiP_Camera]");
        _previewCamObj.transform.SetParent(transform, false);

        _previewCamera = _previewCamObj.AddComponent<Camera>();
        _previewCamera.clearFlags = CameraClearFlags.Skybox;
        _previewCamera.fieldOfView = 75f;
        _previewCamera.nearClipPlane = 0.05f;
        _previewCamera.farClipPlane = 140f;
        _previewCamera.cullingMask = ~0; // All layers
        _previewCamera.depth = -10;
        _previewCamera.targetTexture = _renderTexture;

        // Ensure no audio listener exists on the preview camera
        AudioListener listener = _previewCamObj.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);

        // Configure URP camera data if present
        var urpData = _previewCamObj.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urpData == null)
        {
            urpData = _previewCamObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        }
        if (urpData != null)
        {
            urpData.renderShadows = true;
            urpData.requiresColorTexture = false;
            urpData.requiresDepthTexture = false;
        }

        UpdateCameraEnabled();
    }

    private void UpdateCameraEnabled()
    {
        if (_previewCamera != null)
        {
            _previewCamera.enabled = _showPiP;
        }
    }

    // -----------------------------------------------------------------
    // VIEW MANAGEMENT & CYCLING
    // -----------------------------------------------------------------
    public void RefreshAvailableViews()
    {
        _availableViews.Clear();
        PlayerIdentity activeId = (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.ActivePlayer != null)
            ? MultiPlayerCharacterManager.Instance.ActivePlayer.Identity
            : PlayerIdentityManager.CurrentIdentity;

        // 1. Add all teammates other than the actively controlled player
        for (int i = 1; i <= 4; i++)
        {
            PlayerIdentity id = (PlayerIdentity)i;
            if (id == activeId) continue;

            string name = PlayerIdentityManager.GetDisplayName(id);
            Color col = PlayerIdentityManager.GetIdentityColor(id);
            string roleSuffix = id == PlayerIdentity.Player4 ? " (TORCH)" : "";
            _availableViews.Add(new ViewOption(PiPViewType.Teammate, id, $"{name.ToUpperInvariant()}{roleSuffix}", col));
        }

        // 2. Add 3rd-Person Follow / Chase Cam
        _availableViews.Add(new ViewOption(
            PiPViewType.ThirdPersonChase,
            activeId,
            "3RD-PERSON CHASE CAM",
            new Color(0.95f, 0.40f, 0.95f, 1f)
        ));

        // 3. Add High-Angle Room Surveillance Cam
        _availableViews.Add(new ViewOption(
            PiPViewType.RoomSurveillance,
            PlayerIdentity.Player1,
            "ROOM SURVEILLANCE CAM",
            new Color(1.0f, 0.82f, 0.35f, 1f)
        ));

        // Clamp or sanitize current index
        if (_currentViewIndex >= _availableViews.Count)
        {
            _currentViewIndex = 0;
        }
    }

    private void HandleIdentityChanged(PlayerIdentity newId, PlayerIdentity oldId)
    {
        // Refresh available views and ensure current view doesn't point to the new active player
        RefreshAvailableViews();
        if (_currentViewIndex >= _availableViews.Count)
        {
            _currentViewIndex = 0;
        }
    }

    public void CycleNextView()
    {
        if (_availableViews.Count == 0) RefreshAvailableViews();
        _currentViewIndex = (_currentViewIndex + 1) % _availableViews.Count;
    }

    public void CyclePreviousView()
    {
        if (_availableViews.Count == 0) RefreshAvailableViews();
        _currentViewIndex = (_currentViewIndex - 1 + _availableViews.Count) % _availableViews.Count;
    }

    public void ToggleTracking()
    {
        _trackActivePlayer = !_trackActivePlayer;
    }

    // -----------------------------------------------------------------
    // INPUT SHORTCUTS (F6, F7, F8)
    // -----------------------------------------------------------------
    private void Update()
    {
        HandleKeyboardShortcuts();
    }

    private void HandleKeyboardShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                ShowPiP = !ShowPiP;
            }

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                bool shift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                if (shift) CyclePreviousView();
                else CycleNextView();
            }

            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ToggleTracking();
            }
        }
#else
        if (Input.GetKeyDown(_toggleKey))
        {
            ShowPiP = !ShowPiP;
        }

        if (Input.GetKeyDown(_cycleKey))
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift) CyclePreviousView();
            else CycleNextView();
        }

        if (Input.GetKeyDown(_trackKey))
        {
            ToggleTracking();
        }
#endif
    }

    // -----------------------------------------------------------------
    // CAMERA POSITIONING (LATE UPDATE)
    // -----------------------------------------------------------------
    private void LateUpdate()
    {
        if (!_showPiP || _previewCamera == null) return;

        ViewOption current = CurrentView;
        PlayerCharacter activePlayer = MultiPlayerCharacterManager.Instance != null 
            ? MultiPlayerCharacterManager.Instance.ActivePlayer 
            : null;

        switch (current.viewType)
        {
            case PiPViewType.Teammate:
                UpdateTeammateCamera(current.teammateIdentity, activePlayer);
                break;

            case PiPViewType.ThirdPersonChase:
                UpdateChaseCamera(activePlayer);
                break;

            case PiPViewType.RoomSurveillance:
                UpdateRoomSurveillanceCamera(activePlayer);
                break;
        }
    }

    private void UpdateTeammateCamera(PlayerIdentity teammateId, PlayerCharacter activePlayer)
    {
        PlayerCharacter teammate = MultiPlayerCharacterManager.Instance != null
            ? MultiPlayerCharacterManager.Instance.GetPlayer(teammateId)
            : null;

        if (teammate == null) return;

        float standingY = teammate.FirstPersonController != null 
            ? teammate.FirstPersonController.standingCamY 
            : 1.82f;

        Vector3 eyePos = teammate.transform.position + Vector3.up * standingY + teammate.transform.forward * 0.12f;
        _previewCamera.transform.position = eyePos;

        if (_trackActivePlayer && activePlayer != null)
        {
            // Aim directly at the active player's chest/torso
            Vector3 aimTarget = activePlayer.transform.position + Vector3.up * 1.15f;
            Vector3 lookDir = (aimTarget - eyePos).normalized;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(lookDir);
                _previewCamera.transform.rotation = Quaternion.Slerp(
                    _previewCamera.transform.rotation, 
                    desiredRot, 
                    Time.deltaTime * _trackAimSpeed
                );
            }
        }
        else
        {
            // Point in teammate's natural forward direction
            _previewCamera.transform.rotation = teammate.transform.rotation;
        }
    }

    private void UpdateChaseCamera(PlayerCharacter activePlayer)
    {
        if (activePlayer == null) return;

        Vector3 playerPos = activePlayer.transform.position;
        Vector3 forward = activePlayer.transform.forward;

        // Position 2.85m behind and 1.85m above the active character
        Vector3 idealCamPos = playerPos - forward * 2.85f + Vector3.up * 1.85f;

        _previewCamera.transform.position = Vector3.Lerp(
            _previewCamera.transform.position,
            idealCamPos,
            Time.deltaTime * _chaseFollowSpeed
        );

        Vector3 lookTarget = playerPos + Vector3.up * 1.15f;
        Vector3 lookDir = (lookTarget - _previewCamera.transform.position).normalized;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            _previewCamera.transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    private void UpdateRoomSurveillanceCamera(PlayerCharacter activePlayer)
    {
        // High-angle corner surveillance cam overlooking the Crash Room
        Vector3 roomCamPos = new Vector3(-8.5f, 6.2f, 38.5f);
        _previewCamera.transform.position = roomCamPos;

        if (activePlayer != null)
        {
            Vector3 target = activePlayer.transform.position + Vector3.up * 1.0f;
            Vector3 lookDir = (target - roomCamPos).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                _previewCamera.transform.rotation = Quaternion.Slerp(
                    _previewCamera.transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    Time.deltaTime * 6.0f
                );
            }
        }
        else
        {
            _previewCamera.transform.rotation = Quaternion.Euler(28f, 40f, 0f);
        }
    }

    // -----------------------------------------------------------------
    // URP / RENDER PIPELINE MESH HOOKS
    // -----------------------------------------------------------------
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == _previewCamera)
        {
            SetAllPlayerMeshesForPreview(true);
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam == _previewCamera)
        {
            SetAllPlayerMeshesForPreview(false);
        }
    }

    private void OnLegacyPreRender(Camera cam)
    {
        if (cam == _previewCamera)
        {
            SetAllPlayerMeshesForPreview(true);
        }
    }

    private void OnLegacyPostRender(Camera cam)
    {
        if (cam == _previewCamera)
        {
            SetAllPlayerMeshesForPreview(false);
        }
    }

    private void SetAllPlayerMeshesForPreview(bool forPreview)
    {
        if (MultiPlayerCharacterManager.Instance == null) return;
        foreach (var pc in MultiPlayerCharacterManager.Instance.AllPlayers)
        {
            if (pc != null)
            {
                pc.SetForceMeshVisible(forPreview);
            }
        }
    }

    // -----------------------------------------------------------------
    // ONGUI - PICTURE-IN-PICTURE INTERFACE
    // -----------------------------------------------------------------
    private void OnGUI()
    {
        CreatePixelTexture();
        InitStyles();

        // If PiP is hidden, draw a compact restore badge in top-right
        if (!_showPiP)
        {
            DrawMinimizedBadge();
            return;
        }

        if (_renderTexture == null) return;

        // Calculate window dimensions based on size preset
        float videoWidth, videoHeight;
        switch (_sizePresetIndex)
        {
            case 0: // Small
                videoWidth = 340f;
                videoHeight = 191f;
                break;
            case 2: // Large
                videoWidth = 580f;
                videoHeight = 326f;
                break;
            case 1: // Medium (Default)
            default:
                videoWidth = 460f;
                videoHeight = 259f;
                break;
        }

        float headerHeight = 28f;
        float footerHeight = 22f;
        float padding = 6f;
        float windowWidth = videoWidth + (padding * 2);
        float windowHeight = headerHeight + videoHeight + footerHeight + (padding * 2);

        // Default position: top right corner with 16px margin
        if (_windowPosition.x < 0 || _windowPosition.y < 0)
        {
            _windowPosition = new Vector2(Screen.width - windowWidth - 16f, 16f);
        }

        // Clamp window within screen bounds
        _windowPosition.x = Mathf.Clamp(_windowPosition.x, 0, Screen.width - windowWidth);
        _windowPosition.y = Mathf.Clamp(_windowPosition.y, 0, Screen.height - windowHeight);

        Rect windowRect = new Rect(_windowPosition.x, _windowPosition.y, windowWidth, windowHeight);
        Rect headerRect = new Rect(windowRect.x, windowRect.y, windowRect.width, headerHeight);

        // Dragging handling
        HandleWindowDragging(headerRect);

        ViewOption current = CurrentView;
        Color accentColor = current.accentColor;

        // 1. Draw Glassmorphic Slate Background & Crisp Border
        DrawBox(windowRect, new Color(0.04f, 0.06f, 0.10f, 0.95f), accentColor, 2);

        // 2. Accent Bar at top
        DrawRect(new Rect(windowRect.x, windowRect.y, windowRect.width, 3f), accentColor);

        // 3. Header Bar Elements
        DrawHeader(windowRect, headerHeight, padding, current, accentColor);

        // 4. Video Texture Rendering
        Rect videoRect = new Rect(windowRect.x + padding, windowRect.y + headerHeight + padding, videoWidth, videoHeight);
        GUI.DrawTexture(videoRect, _renderTexture, ScaleMode.ScaleToFit);

        // 5. Surveillance HUD Overlays on top of video feed
        DrawVideoHUDOverlays(videoRect, current, accentColor);

        // 6. Footer Telemetry & Shortcut hints
        Rect footerRect = new Rect(windowRect.x + padding, videoRect.yMax + 4f, videoWidth, footerHeight);
        DrawFooter(footerRect, accentColor);
    }

    private void HandleWindowDragging(Rect headerRect)
    {
        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && headerRect.Contains(evt.mousePosition))
        {
            _isDragging = true;
            _dragOffset = evt.mousePosition - _windowPosition;
            evt.Use();
        }
        else if (evt.type == EventType.MouseDrag && _isDragging)
        {
            _windowPosition = evt.mousePosition - _dragOffset;
            evt.Use();
        }
        else if (evt.type == EventType.MouseUp)
        {
            _isDragging = false;
        }
    }

    private void DrawHeader(Rect windowRect, float headerHeight, float padding, ViewOption current, Color accentColor)
    {
        float curX = windowRect.x + padding + 6f;
        float curY = windowRect.y + 5f;

        // Title with camera icon
        _headerTitleStyle.normal.textColor = accentColor;
        string title = $"📹 PIP: {current.label}";
        GUI.Label(new Rect(curX, curY, 210f, 20f), title, _headerTitleStyle);

        // Right side buttons
        float btnWidth = 24f;
        float btnHeight = 20f;
        float rightX = windowRect.xMax - padding - 6f - btnWidth;

        // Close button [ ✕ ]
        if (GUI.Button(new Rect(rightX, curY, btnWidth, btnHeight), "✕", _btnStyle))
        {
            ShowPiP = false;
        }
        rightX -= (btnWidth + 4f);

        // Size cycle button [ ⛶ ]
        if (GUI.Button(new Rect(rightX, curY, btnWidth, btnHeight), "⛶", _btnStyle))
        {
            _sizePresetIndex = (_sizePresetIndex + 1) % 3;
        }
        rightX -= (62f + 4f);

        // Tracking toggle button [ 🎯 Track: ON/OFF ] (only applicable to Teammates)
        if (current.viewType == PiPViewType.Teammate)
        {
            string trackLabel = _trackActivePlayer ? "🎯 TRK: ON" : "🎯 TRK: OFF";
            Color oldCol = _btnStyle.normal.textColor;
            _btnStyle.normal.textColor = _trackActivePlayer ? new Color(0.4f, 1.0f, 0.5f, 1f) : new Color(0.7f, 0.7f, 0.7f, 0.8f);
            if (GUI.Button(new Rect(rightX, curY, 62f, btnHeight), trackLabel, _btnStyle))
            {
                ToggleTracking();
            }
            _btnStyle.normal.textColor = oldCol;
        }
        else
        {
            GUI.Label(new Rect(rightX, curY, 62f, btnHeight), "FOLLOW", _footerStyle);
        }
        rightX -= (btnWidth + 4f);

        // Next button [ ▶ ]
        if (GUI.Button(new Rect(rightX, curY, btnWidth, btnHeight), "▶", _btnStyle))
        {
            CycleNextView();
        }
        rightX -= (btnWidth + 4f);

        // Previous button [ ◀ ]
        if (GUI.Button(new Rect(rightX, curY, btnWidth, btnHeight), "◀", _btnStyle))
        {
            CyclePreviousView();
        }
    }

    private void DrawVideoHUDOverlays(Rect videoRect, ViewOption current, Color accentColor)
    {
        // 1. Subtle dark border around video frame
        DrawBox(videoRect, Color.clear, new Color(0.18f, 0.24f, 0.32f, 0.8f), 1);

        // 2. Top-Left: LIVE FEED indicator with pulsing red dot
        float dotAlpha = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.0f);
        DrawBox(new Rect(videoRect.x + 8f, videoRect.y + 8f, 7f, 7f), new Color(1.0f, 0.15f, 0.15f, dotAlpha), Color.clear, 0);

        _badgeStyle.normal.textColor = new Color(1.0f, 0.90f, 0.90f, 0.92f);
        GUI.Label(new Rect(videoRect.x + 19f, videoRect.y + 4f, 80f, 16f), "LIVE FEED", _badgeStyle);

        // 3. Top-Right: Target ID Badge
        _badgeStyle.normal.textColor = accentColor;
        string badgeText = current.viewType == PiPViewType.Teammate 
            ? $"POV // {current.teammateIdentity}" 
            : current.label;
        GUI.Label(new Rect(videoRect.xMax - 130f, videoRect.y + 4f, 122f, 16f), badgeText, _badgeStyle);

        // 4. Subtle corner brackets (authentic surveillance monitor feel)
        Color bracketColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
        float bSize = 10f;
        // Top-left bracket
        DrawRect(new Rect(videoRect.x + 4f, videoRect.y + 4f, bSize, 1f), bracketColor);
        DrawRect(new Rect(videoRect.x + 4f, videoRect.y + 4f, 1f, bSize), bracketColor);
        // Top-right bracket
        DrawRect(new Rect(videoRect.xMax - 4f - bSize, videoRect.y + 4f, bSize, 1f), bracketColor);
        DrawRect(new Rect(videoRect.xMax - 5f, videoRect.y + 4f, 1f, bSize), bracketColor);
        // Bottom-left bracket
        DrawRect(new Rect(videoRect.x + 4f, videoRect.yMax - 5f, bSize, 1f), bracketColor);
        DrawRect(new Rect(videoRect.x + 4f, videoRect.yMax - 4f - bSize, 1f, bSize), bracketColor);
        // Bottom-right bracket
        DrawRect(new Rect(videoRect.xMax - 4f - bSize, videoRect.yMax - 5f, bSize, 1f), bracketColor);
        DrawRect(new Rect(videoRect.xMax - 5f, videoRect.yMax - 4f - bSize, 1f, bSize), bracketColor);
    }

    private void DrawFooter(Rect footerRect, Color accentColor)
    {
        PlayerCharacter active = MultiPlayerCharacterManager.Instance != null 
            ? MultiPlayerCharacterManager.Instance.ActivePlayer 
            : null;

        string telemetryText = "ACTIVE: — | SPEED: 0.0 m/s";
        if (active != null)
        {
            var cc = active.CharacterController;
            float speed = cc != null ? new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude : 0f;
            string motionLabel = "Idle";
            if (speed > 5.5f) motionLabel = "RUN";
            else if (speed > 0.3f) motionLabel = "JOG";

            string crouchLabel = "";
            if (active.FirstPersonController != null && cc != null && cc.height < 1.7f)
            {
                crouchLabel = " | CROUCH";
            }

            telemetryText = $"CTR: {active.Identity} | {motionLabel} ({speed:F1} m/s){crouchLabel}";
        }

        GUI.Label(new Rect(footerRect.x, footerRect.y, footerRect.width * 0.55f, footerRect.height), telemetryText, _footerStyle);

        // Right side hints
        string hints = "[F6] Hide | [F7] Cycle | [F8] Trk";
        GUI.Label(new Rect(footerRect.xMax - 170f, footerRect.y, 170f, footerRect.height), hints, _footerStyle);
    }

    private void DrawMinimizedBadge()
    {
        float badgeWidth = 135f;
        float badgeHeight = 26f;
        Rect badgeRect = new Rect(Screen.width - badgeWidth - 16f, 16f, badgeWidth, badgeHeight);

        Color bg = new Color(0.06f, 0.08f, 0.12f, 0.85f);
        Color border = new Color(0.28f, 0.36f, 0.48f, 0.70f);
        DrawBox(badgeRect, bg, border, 1);

        _btnStyle.normal.textColor = new Color(0.75f, 0.85f, 1.0f, 0.95f);
        if (GUI.Button(badgeRect, "📹 PREVIEW [F6]", _btnStyle))
        {
            ShowPiP = true;
        }
    }

    // -----------------------------------------------------------------
    // IMGUI STYLING HELPERS
    // -----------------------------------------------------------------
    private void InitStyles()
    {
        if (_stylesInitialized && _headerTitleStyle != null) return;

        _headerTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _btnStyle.normal.textColor = new Color(0.85f, 0.90f, 0.98f, 1f);

        _footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        _footerStyle.normal.textColor = new Color(0.65f, 0.72f, 0.82f, 0.90f);

        _badgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _stylesInitialized = true;
    }

    private void CreatePixelTexture()
    {
        if (_pixelTex == null)
        {
            _pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();
        }
    }

    private void DrawRect(Rect rect, Color color)
    {
        if (_pixelTex == null) CreatePixelTexture();
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _pixelTex);
        GUI.color = oldColor;
    }

    private void DrawBox(Rect rect, Color fillColor, Color borderColor, int borderWidth)
    {
        DrawRect(rect, fillColor);
        if (borderWidth > 0 && borderColor.a > 0.01f)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, borderWidth), borderColor); // Top
            DrawRect(new Rect(rect.x, rect.y + rect.height - borderWidth, rect.width, borderWidth), borderColor); // Bottom
            DrawRect(new Rect(rect.x, rect.y, borderWidth, rect.height), borderColor); // Left
            DrawRect(new Rect(rect.x + rect.width - borderWidth, rect.y, borderWidth, rect.height), borderColor); // Right
        }
    }
}
