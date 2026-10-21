using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =========================================================================
// DevPlayerSwitchingPanel - In-game Developer Player Identity Switcher
//
// Allows testing the 4 cooperative player identities (Player 1 to Player 4)
// on a single laptop in Play Mode.
//
// SWITCHING BEHAVIOUR:
// - Switches the *logical identity* of the currently controlled player.
// - Controls the same physical FirstPersonController.
// - Does NOT reload the scene, teleport the player, or alter camera/movement.
// - Does NOT hide or alter any environment objects (doors, pits, walls).
//
// INPUT:
// - Direct UI mouse click (press Tab to unlock mouse, click buttons in top-left).
// - Quick Hotkeys: F1 (Player 1), F2 (Player 2), F3 (Player 3), F4 (Player 4).
// - Toggle panel visibility: F12 or Backquote (tilde), or Inspector checkbox.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Dev/Player Switching Panel")]
[DisallowMultipleComponent]
public class DevPlayerSwitchingPanel : MonoBehaviour
{
    // -----------------------------------------------------------------
    // INSPECTOR CONFIGURATION
    // -----------------------------------------------------------------
    [Header("Visibility")]
    [Tooltip("Whether the developer panel is rendered on screen during Play mode.")]
    [SerializeField] private bool _showPanel = true;

    [Tooltip("Shortcut key to toggle panel visibility on/off.")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F12;

    [Header("Panel Layout")]
    [SerializeField] private Vector2 _panelPosition = new Vector2(16f, 16f);
    [SerializeField] private Vector2 _panelSize = new Vector2(380f, 212f);

    // -----------------------------------------------------------------
    // STATE & STYLING
    // -----------------------------------------------------------------
    private Texture2D _pixelTex;
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _btnActiveStyle;
    private GUIStyle _btnInactiveStyle;
    private GUIStyle _statusStyle;
    private GUIStyle _hintStyle;
    private bool _stylesInitialized = false;

    private string _confirmationMessage = "";
    private float _confirmationTime = -10f;
    private const float ConfirmationDuration = 3.0f;

    public bool ShowPanel
    {
        get => _showPanel;
        set => _showPanel = value;
    }

    public Rect PanelRect => new Rect(_panelPosition.x, _panelPosition.y, _panelSize.x, _panelSize.y);

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        CreatePixelTexture();
    }

    private void OnEnable()
    {
        PlayerIdentityManager.OnIdentityChanged += HandleIdentityChanged;
    }

    private void OnDisable()
    {
        PlayerIdentityManager.OnIdentityChanged -= HandleIdentityChanged;
    }

    private void OnDestroy()
    {
        if (_pixelTex != null)
        {
            Destroy(_pixelTex);
            _pixelTex = null;
        }
    }

    private void HandleIdentityChanged(PlayerIdentity newId, PlayerIdentity oldId)
    {
        _confirmationMessage = $"Switched to {PlayerIdentityManager.GetDisplayName(newId)} (via {PlayerIdentityManager.LastChangeSource})";
        _confirmationTime = Time.time;
    }

    // -----------------------------------------------------------------
    // UPDATE - KEYBOARD SHORTCUTS (F1 - F4)
    // -----------------------------------------------------------------
    private void Update()
    {
        // Toggle panel display with toggle key or tilde
        HandleToggleShortcut();

        // Check F1 - F4 identity switching shortcuts
        HandleIdentityShortcuts();
    }

    private void HandleToggleShortcut()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f12Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                _showPanel = !_showPanel;
            }
        }
#else
        if (Input.GetKeyDown(_toggleKey) || Input.GetKeyDown(KeyCode.BackQuote))
        {
            _showPanel = !_showPanel;
        }
#endif
    }

    private void HandleIdentityShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame) SwitchIdentity(PlayerIdentity.Player1, "F1");
            else if (Keyboard.current.f2Key.wasPressedThisFrame) SwitchIdentity(PlayerIdentity.Player2, "F2");
            else if (Keyboard.current.f3Key.wasPressedThisFrame) SwitchIdentity(PlayerIdentity.Player3, "F3");
            else if (Keyboard.current.f4Key.wasPressedThisFrame) SwitchIdentity(PlayerIdentity.Player4, "F4");

            // Developer Teleport Teammates hotkey [T]
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                TeleportTeammates();
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.F1)) SwitchIdentity(PlayerIdentity.Player1, "F1");
        else if (Input.GetKeyDown(KeyCode.F2)) SwitchIdentity(PlayerIdentity.Player2, "F2");
        else if (Input.GetKeyDown(KeyCode.F3)) SwitchIdentity(PlayerIdentity.Player3, "F3");
        else if (Input.GetKeyDown(KeyCode.F4)) SwitchIdentity(PlayerIdentity.Player4, "F4");

        if (Input.GetKeyDown(KeyCode.T))
        {
            TeleportTeammates();
        }
#endif
    }

    public void TeleportTeammates()
    {
        if (MultiPlayerCharacterManager.Instance != null)
        {
            MultiPlayerCharacterManager.Instance.TeleportOtherPlayersToActive();
            _confirmationMessage = "Teleported all teammates to current position! [T]";
            _confirmationTime = Time.time;
        }
    }

    public void SwitchIdentity(PlayerIdentity identity, string source)
    {
        if (PlayerIdentityManager.CurrentIdentity == identity)
        {
            _confirmationMessage = $"{PlayerIdentityManager.GetDisplayName(identity)} already active";
            _confirmationTime = Time.time;
            return;
        }

        PlayerIdentityManager.SetIdentity(identity, source);
    }

    // -----------------------------------------------------------------
    // ONGUI - DEVELOPER PANEL INTERFACE
    // -----------------------------------------------------------------
    private void OnGUI()
    {
        if (!_showPanel) return;

        CreatePixelTexture();
        InitStyles();

        PlayerIdentity current = PlayerIdentityManager.CurrentIdentity;
        Color activeColor = PlayerIdentityManager.GetIdentityColor(current);

        Rect panelRect = PanelRect;

        // 1. Draw Panel Background (Glassmorphic dark slate with crisp border)
        DrawBox(panelRect, new Color(0.05f, 0.07f, 0.11f, 0.94f), new Color(0.25f, 0.33f, 0.46f, 0.85f), 1);

        // 2. Accent top bar (colored to active player identity)
        DrawRect(new Rect(panelRect.x, panelRect.y, panelRect.width, 3f), activeColor);

        // 3. Header: "FLIP//FALL — DEV TOOLS"
        float paddingX = 12f;
        float curY = panelRect.y + 9f;

        Rect titleRect = new Rect(panelRect.x + paddingX, curY, panelRect.width - (paddingX * 2), 16f);
        GUI.Label(titleRect, "FLIP//FALL — DEV TOOLS", _headerStyle);
        curY += 20f;

        // 4. Subheader: "CURRENT PLAYER: PLAYER 1"
        Rect subHeaderRect = new Rect(panelRect.x + paddingX, curY, panelRect.width - (paddingX * 2), 19f);
        _subHeaderStyle.normal.textColor = activeColor;
        GUI.Label(subHeaderRect, $"CURRENT PLAYER: {PlayerIdentityManager.GetDisplayName(current).ToUpperInvariant()}", _subHeaderStyle);
        curY += 24f;

        // 5. 4 Player Buttons: [ PLAYER 1 ] [ PLAYER 2 ] [ PLAYER 3 ] [ PLAYER 4 ]
        float buttonSpacing = 6f;
        float totalButtonsWidth = panelRect.width - (paddingX * 2);
        float btnWidth = Mathf.Floor((totalButtonsWidth - (buttonSpacing * 3)) / 4f);
        float btnHeight = 28f;

        for (int i = 1; i <= 4; i++)
        {
            PlayerIdentity id = (PlayerIdentity)i;
            bool isCurrent = (id == current);
            float btnX = panelRect.x + paddingX + (i - 1) * (btnWidth + buttonSpacing);
            Rect btnRect = new Rect(btnX, curY, btnWidth, btnHeight);

            // Draw custom button background & border
            if (isCurrent)
            {
                // Active highlight: solid colored border + tinted fill + bright bold label
                Color fillColor = new Color(activeColor.r * 0.35f, activeColor.g * 0.35f, activeColor.b * 0.35f, 0.95f);
                DrawBox(btnRect, fillColor, activeColor, 2);
                _btnActiveStyle.normal.textColor = Color.white;

                string btnText = $"[ {PlayerIdentityManager.GetDisplayName(id).ToUpperInvariant()} ]";
                if (GUI.Button(btnRect, btnText, _btnActiveStyle))
                {
                    SwitchIdentity(id, "UI Button");
                }
            }
            else
            {
                // Inactive button: subtle dark fill + muted border + softer text
                Color btnBg = new Color(0.12f, 0.15f, 0.22f, 0.85f);
                Color btnBorder = new Color(0.26f, 0.32f, 0.44f, 0.65f);
                DrawBox(btnRect, btnBg, btnBorder, 1);
                _btnInactiveStyle.normal.textColor = new Color(0.72f, 0.78f, 0.88f, 1f);

                string btnText = PlayerIdentityManager.GetDisplayName(id).ToUpperInvariant();
                if (GUI.Button(btnRect, btnText, _btnInactiveStyle))
                {
                    SwitchIdentity(id, "UI Button");
                }
            }
        }
        curY += btnHeight + 8f;

        // 5b. Developer Teleport Teammates button
        Rect tpBtnRect = new Rect(panelRect.x + paddingX, curY, panelRect.width - (paddingX * 2), 20f);
        Color tpFill = new Color(0.12f, 0.22f, 0.32f, 0.90f);
        Color tpBorder = new Color(0.35f, 0.65f, 0.85f, 0.80f);
        DrawBox(tpBtnRect, tpFill, tpBorder, 1);
        _btnInactiveStyle.normal.textColor = new Color(0.65f, 0.88f, 1.0f, 1f);
        if (GUI.Button(tpBtnRect, "⚡ TELEPORT TEAMMATES HERE  [T]", _btnInactiveStyle))
        {
            TeleportTeammates();
        }
        curY += 23f;

        // 5c. Secondary Preview Window controls [F6 / F7]
        float pipSpacing = 5f;
        float totalPipW = panelRect.width - (paddingX * 2);
        float toggleW = Mathf.Floor((totalPipW - pipSpacing) * 0.68f);
        float cycleW = totalPipW - toggleW - pipSpacing;

        Rect pipToggleRect = new Rect(panelRect.x + paddingX, curY, toggleW, 20f);
        Rect pipCycleRect = new Rect(pipToggleRect.xMax + pipSpacing, curY, cycleW, 20f);

        bool isPiPOn = TeammatePiPPreview.Instance != null && TeammatePiPPreview.Instance.ShowPiP;
        string pipLabel = TeammatePiPPreview.Instance != null ? TeammatePiPPreview.Instance.CurrentViewLabel : "MONITOR";

        Color pipFill = isPiPOn ? new Color(0.10f, 0.24f, 0.18f, 0.92f) : new Color(0.12f, 0.15f, 0.22f, 0.85f);
        Color pipBorder = isPiPOn ? new Color(0.35f, 0.85f, 0.55f, 0.85f) : new Color(0.26f, 0.32f, 0.44f, 0.65f);
        DrawBox(pipToggleRect, pipFill, pipBorder, 1);
        _btnInactiveStyle.normal.textColor = isPiPOn ? new Color(0.55f, 1.0f, 0.75f, 1f) : new Color(0.72f, 0.78f, 0.88f, 1f);
        string toggleText = isPiPOn ? $"📹 PIP: ON [{pipLabel}] [F6]" : "📹 PIP PREVIEW: OFF [F6]";
        if (GUI.Button(pipToggleRect, toggleText, _btnInactiveStyle))
        {
            if (TeammatePiPPreview.Instance != null)
            {
                TeammatePiPPreview.Instance.ShowPiP = !TeammatePiPPreview.Instance.ShowPiP;
            }
        }

        DrawBox(pipCycleRect, new Color(0.15f, 0.18f, 0.26f, 0.85f), new Color(0.32f, 0.40f, 0.55f, 0.70f), 1);
        _btnInactiveStyle.normal.textColor = new Color(0.80f, 0.88f, 1.0f, 1f);
        if (GUI.Button(pipCycleRect, "CYCLE [F7]", _btnInactiveStyle))
        {
            if (TeammatePiPPreview.Instance != null)
            {
                TeammatePiPPreview.Instance.CycleNextView();
            }
        }
        curY += 24f;

        // 6. Active identity label: "Active identity: Player 1"
        Rect statusRect = new Rect(panelRect.x + paddingX, curY, panelRect.width - (paddingX * 2), 17f);
        GUI.Label(statusRect, $"Active identity: {PlayerIdentityManager.GetDisplayName(current)}", _statusStyle);
        curY += 19f;

        // 7. Small confirmation & shortcut hint line
        Rect hintRect = new Rect(panelRect.x + paddingX, curY, panelRect.width - (paddingX * 2), 16f);
        bool showConfirmation = (Time.time - _confirmationTime) < ConfirmationDuration && !string.IsNullOrEmpty(_confirmationMessage);

        if (showConfirmation)
        {
            _hintStyle.normal.textColor = activeColor;
            GUI.Label(hintRect, $"✓ {_confirmationMessage}", _hintStyle);
        }
        else
        {
            _hintStyle.normal.textColor = new Color(0.55f, 0.63f, 0.74f, 0.90f);
            GUI.Label(hintRect, "[WASD] Move | [Shift] Run | [T] Tp | [F6] PiP | [F7] Cam | [F1-F4] Switch", _hintStyle);
        }
    }

    // -----------------------------------------------------------------
    // IMGUI STYLING HELPERS
    // -----------------------------------------------------------------
    private void InitStyles()
    {
        if (_stylesInitialized && _headerStyle != null) return;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _headerStyle.normal.textColor = new Color(0.85f, 0.90f, 0.98f, 1f);

        _subHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _btnActiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _btnActiveStyle.normal.background = null;
        _btnActiveStyle.hover.background = null;
        _btnActiveStyle.active.background = null;

        _btnInactiveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        _btnInactiveStyle.normal.background = null;
        _btnInactiveStyle.hover.background = null;
        _btnInactiveStyle.active.background = null;

        _statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        _statusStyle.normal.textColor = new Color(0.90f, 0.94f, 1.0f, 1f);

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Italic,
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
        // Fill
        DrawRect(rect, fillColor);

        // Border
        if (borderWidth > 0 && borderColor.a > 0.01f)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, borderWidth), borderColor); // Top
            DrawRect(new Rect(rect.x, rect.y + rect.height - borderWidth, rect.width, borderWidth), borderColor); // Bottom
            DrawRect(new Rect(rect.x, rect.y, borderWidth, rect.height), borderColor); // Left
            DrawRect(new Rect(rect.x + rect.width - borderWidth, rect.y, borderWidth, rect.height), borderColor); // Right
        }
    }
}
