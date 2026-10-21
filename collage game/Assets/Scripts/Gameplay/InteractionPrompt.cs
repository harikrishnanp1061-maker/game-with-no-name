using System;
using UnityEngine;

// =========================================================================
// InteractionPrompt - the "[E] TRIP FUSE" text at the bottom centre of the
// screen.
//
// Deliberately IMGUI rather than a Canvas or UI Toolkit panel: the prompt
// must be attachable to whatever object the builders create at runtime, and
// IMGUI needs no Canvas, no panel settings, no UXML asset and no extra scene
// wiring. It is driven entirely by InteractionManager, so it owns no input
// handling of its own.
//
// Attach it to anything - the player, an empty object, or the manager itself.
// If none is placed in the scene, InteractionManager creates one.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Interaction Prompt")]
[DisallowMultipleComponent]
public class InteractionPrompt : MonoBehaviour
{
    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    // -----------------------------------------------------------------
    // PUBLIC API - driven by InteractionManager
    // -----------------------------------------------------------------
    /// <summary>
    /// Shows the prompt for the given label and colour. Pass an empty label
    /// to hide it.
    /// </summary>
    public void Show(string label, Color color)
    {
        _targetLabel = label != null ? label : "";
        _targetColor = color;
        _hasPrompt = !string.IsNullOrEmpty(_targetLabel);
    }

    /// <summary>Hides the prompt.</summary>
    public void Hide()
    {
        _targetLabel = "";
        _hasPrompt = false;
    }

    public string CurrentLabel { get { return _targetLabel; } }
    public Color CurrentColor { get { return _displayColor; } }
    public bool IsVisible { get { return _alpha > 0.02f; } }

    // -----------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------
    [Header("Text")]
    [SerializeField] private string _keyLabel = "LMB / E";
    [SerializeField] private float _fontSize = 18f;
    [SerializeField] private TextAlignment _alignment = TextAlignment.Center;
    [SerializeField] private bool _uppercaseText = true;
    [ColorUsage(true, true)]
    [SerializeField] private Color _defaultColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private bool _drawKeyBracket = true;
    [SerializeField] private bool _drawOutline = true;
    [SerializeField] private int _outlineWidth = 2;

    [Header("Position")]
    [SerializeField] private float _bottomOffset = 92f;
    [SerializeField] private float _centerOffsetX = 0f;
    [SerializeField] private float _boxWidth = 0f;

    [Header("Fade")]
    [SerializeField] private float _fadeSpeed = 9f;
    [SerializeField] private float _minAlpha = 0f;
    [SerializeField] private float _maxAlpha = 1f;

    [Header("Font")]
    [Tooltip("Optional. Falls back to the built-in Arial font; if nothing is available the prompt stays hidden and logs a warning.")]
    [SerializeField] private Font _font;

    [Header("Behaviour")]
    [SerializeField] private bool _enabledPrompt = true;
    [SerializeField] private bool _logChanges = false;

    // -----------------------------------------------------------------
    // STATE
    // -----------------------------------------------------------------
    [SerializeField] private string _targetLabel = "";
    [SerializeField] private Color _targetColor;
    [SerializeField] private Color _displayColor;
    [SerializeField] private float _alpha = 0f;
    [SerializeField] private bool _hasPrompt = false;
    [SerializeField] private string _lastLoggedLabel = "";

    private GUIStyle _labelStyle;
    private GUIStyle _outlineStyle;
    private Font _resolvedFont;
    private bool _styleBuilt;
    private bool _warnedAboutFont;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        _displayColor = _defaultColor;
        _targetColor = _defaultColor;
        if (_enabledPrompt && _maxAlpha <= 0f) _maxAlpha = 1f;
        if (_enabledPrompt && _fadeSpeed <= 0f) _fadeSpeed = 9f;
    }

    private void Update()
    {
        if (!_enabledPrompt) return;

        float target = _hasPrompt ? _maxAlpha : _minAlpha;
        _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime * _fadeSpeed);

        _displayColor = Color.Lerp(_displayColor, _targetColor,
            Time.unscaledDeltaTime * 12f);

        if (_logChanges && _targetLabel != _lastLoggedLabel)
        {
            _lastLoggedLabel = _targetLabel;
            Debug.Log("[InteractionPrompt] -> '" + _targetLabel + "'");
        }
    }

    private void OnGUI()
    {
        if (!_enabledPrompt) return;
        if (_alpha <= 0.01f) return;
        if (string.IsNullOrEmpty(_targetLabel)) return;

        BuildStyles();
        if (_resolvedFont == null) return;

        string label = BuildLabel();
        Rect rect = new Rect(
            _centerOffsetX,
            Mathf.Max(0f, Screen.height - _bottomOffset),
            Screen.width, _bottomOffset);

        float a = Mathf.Clamp01(_alpha);

        Color color = _displayColor;
        color.a = a;

        if (_boxWidth > 0f)
        {
            DrawPanel(new Rect(Screen.width * 0.5f - _boxWidth * 0.5f,
                Screen.height - _bottomOffset + 2f,
                _boxWidth, 30f), color);
        }

        if (_drawOutline && _outlineStyle != null)
        {
            Color outlineColor = new Color(0f, 0f, 0f, a * 0.85f);
            GUI.color = outlineColor;
            for (int i = -_outlineWidth; i <= _outlineWidth; i++)
            {
                for (int j = -_outlineWidth; j <= _outlineWidth; j++)
                {
                    if (i == 0 && j == 0) continue;
                    GUI.Label(rect, label, _outlineStyle);
                }
            }
        }

        GUI.color = color;
        if (_labelStyle != null)
        {
            GUI.Label(rect, label, _labelStyle);
        }
        GUI.color = Color.white;
    }

    // -----------------------------------------------------------------
    // HELPERS
    // -----------------------------------------------------------------
    private string BuildLabel()
    {
        string text = _targetLabel;
        if (_uppercaseText) text = text.ToUpperInvariant();

        if (_drawKeyBracket && !string.IsNullOrEmpty(_keyLabel)
            && text.IndexOf(_keyLabel, StringComparison.Ordinal) < 0)
        {
            return "[" + _keyLabel.ToUpperInvariant() + "] " + text;
        }
        return text;
    }

    private void DrawPanel(Rect rect, Color color)
    {
        Color bar = color;
        bar.a = color.a * 0.22f;
        GUI.color = bar;
        GUI.Box(rect, "");

        Color accent = color;
        accent.a = color.a * 0.85f;
        GUI.color = accent;
        GUI.Box(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), "");
        GUI.color = Color.white;
    }

    private void BuildStyles()
    {
        if (_styleBuilt && _resolvedFont != null) return;

        _resolvedFont = ResolveFont();
        if (_resolvedFont == null)
        {
            if (!_warnedAboutFont)
            {
                Debug.LogWarning("[InteractionPrompt] No font available - assign _font. " +
                    "The prompt stays hidden until then.");
                _warnedAboutFont = true;
            }
            return;
        }

        TextAnchor anchor = _alignment == TextAlignment.Center
            ? TextAnchor.MiddleCenter
            : (_alignment == TextAlignment.Left
                ? TextAnchor.MiddleLeft
                : TextAnchor.MiddleRight);

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = anchor,
            normal = new GUIStyleState(),
            fontStyle = FontStyle.Bold
        };
        _labelStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(_fontSize));
        _labelStyle.font = _resolvedFont;

        _outlineStyle = new GUIStyle(_labelStyle)
        {
            normal = new GUIStyleState()
        };
        _outlineStyle.font = _resolvedFont;

        _styleBuilt = true;
    }

    private Font ResolveFont()
    {
        if (_font != null) return _font;
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
