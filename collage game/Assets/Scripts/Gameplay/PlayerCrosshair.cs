using UnityEngine;

/// <summary>
/// Renders a dynamic first-person crosshair at the center of the player's screen.
/// Automatically highlights and pulses when aiming at an interactable object.
/// </summary>
[AddComponentMenu("FLIP//FALL/Gameplay/Player Crosshair")]
[DisallowMultipleComponent]
public class PlayerCrosshair : MonoBehaviour
{
    [Header("Crosshair Sizing")]
    [SerializeField] private float _dotSize = 4f;
    [SerializeField] private float _lineLength = 6f;
    [SerializeField] private float _lineThickness = 2f;
    [SerializeField] private float _idleSpread = 5f;
    [SerializeField] private float _hoverSpread = 8f;

    [Header("Colors")]
    [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color _hoverColor = new Color(0.2f, 1.0f, 0.45f, 0.95f);
    [SerializeField] private Color _outlineColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private bool _drawOutline = true;

    [Header("Animation")]
    [SerializeField] private float _animSpeed = 12f;

    private float _currentSpread;
    private Color _currentColor;
    private Texture2D _pixelTex;

    private void Awake()
    {
        _currentSpread = _idleSpread;
        _currentColor = _idleColor;
        CreatePixelTexture();
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

    private void OnDestroy()
    {
        if (_pixelTex != null)
        {
            Destroy(_pixelTex);
            _pixelTex = null;
        }
    }

    private void Update()
    {
        bool hasTarget = InteractionManager.Instance != null && InteractionManager.Instance.HasTarget;
        float targetSpread = hasTarget ? _hoverSpread : _idleSpread;
        Color targetColor = hasTarget
            ? (InteractionManager.Instance.CurrentTarget != null ? InteractionManager.Instance.CurrentTarget.PromptColor : _hoverColor)
            : _idleColor;

        _currentSpread = Mathf.MoveTowards(_currentSpread, targetSpread, Time.unscaledDeltaTime * (_animSpeed * 1.5f));
        _currentColor = Color.Lerp(_currentColor, targetColor, Time.unscaledDeltaTime * _animSpeed);
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        CreatePixelTexture();
        if (_pixelTex == null) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // Center dot
        DrawRect(new Rect(cx - _dotSize * 0.5f, cy - _dotSize * 0.5f, _dotSize, _dotSize), _currentColor);

        // 4 directional ticks (Left, Right, Top, Bottom)
        float s = _currentSpread;
        float len = _lineLength;
        float th = _lineThickness;

        // Left tick
        DrawRect(new Rect(cx - s - len, cy - th * 0.5f, len, th), _currentColor);
        // Right tick
        DrawRect(new Rect(cx + s, cy - th * 0.5f, len, th), _currentColor);
        // Top tick
        DrawRect(new Rect(cx - th * 0.5f, cy - s - len, th, len), _currentColor);
        // Bottom tick
        DrawRect(new Rect(cx - th * 0.5f, cy + s, th, len), _currentColor);
    }

    private void DrawRect(Rect rect, Color color)
    {
        if (_drawOutline)
        {
            Color prevColor = GUI.color;
            GUI.color = _outlineColor;
            GUI.DrawTexture(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), _pixelTex);
            GUI.color = color;
            GUI.DrawTexture(rect, _pixelTex);
            GUI.color = prevColor;
        }
        else
        {
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixelTex);
            GUI.color = prevColor;
        }
    }
}
