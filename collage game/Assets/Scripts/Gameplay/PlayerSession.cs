using UnityEngine;

// =========================================================================
// PlayerSession - thin identity wrapper around the local player.
//
// FLIP//FALL is a 4-player co-op map, so every tutorial beat needs to know
// "whose turn is it". In the solo testing phase there is exactly one
// FirstPersonController, but the interface keeps the co-op work simple later:
// beats are claimed per PlayerSession, never globally by default.
//
// Deliberately has no physics or networking. It is just an addressable bag of
// references so the gameplay scripts do not have to re-find the controller,
// its camera and its head position every call.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Player Session")]
[DisallowMultipleComponent]
public class PlayerSession : MonoBehaviour
{
    // -----------------------------------------------------------------
    // STATIC LOOKUP - the one and only place the player is found.
    // -----------------------------------------------------------------
    private static PlayerSession _singleton;

    /// <summary>
    /// The local player, or null if none is present. Cached in Awake and
    /// cleared in OnDestroy so a destroyed player does not linger.
    /// </summary>
    public static PlayerSession Local
    {
        get { return _singleton; }
    }

    // -----------------------------------------------------------------
    // REFERENCES
    // -----------------------------------------------------------------
    [Header("References")]
    [Tooltip("The controller that owns this session. Defaults to the one on this object, else any FirstPersonController in the scene.")]
    [SerializeField] private FirstPersonController _controller;
    [Tooltip("Camera interaction raycasts are cast from. Defaults to the controller's playerCamera, else the first Camera under the controller.")]
    [SerializeField] private Camera _camera;

    [Header("Identity")]
    [Tooltip("Stable name for logs and events. Defaults to the controller's object name.")]
    [SerializeField] private string _playerName = "Player";
    [Tooltip("Player tag the rest of the scripts should filter by.")]
    [SerializeField] private string _playerTag = "Player";

    // -----------------------------------------------------------------
    // ACCESSORS
    // -----------------------------------------------------------------
    public FirstPersonController Controller
    {
        get
        {
            if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.ActivePlayer != null)
            {
                return MultiPlayerCharacterManager.Instance.ActivePlayer.FirstPersonController;
            }
            return _controller;
        }
    }

    public Camera Camera => Camera.main != null ? Camera.main : _camera;
    public string Name => string.IsNullOrEmpty(_playerName) ? gameObject.name : _playerName;
    public string PlayerTag => _playerTag;
    public PlayerIdentity Identity => PlayerIdentityManager.CurrentIdentity;

    public bool IsValid => Controller != null || this != null;

    public bool IsActive => gameObject.activeInHierarchy;

    public Vector3 Position => Controller != null ? Controller.transform.position : transform.position;

    public Vector3 HeadPosition => Position + Vector3.up * 1.6f;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        ResolveReferences();
        _singleton = this;
    }

    private void OnDestroy()
    {
        if (_singleton == this) _singleton = null;
    }

    /// <summary>
    /// Re-resolves the controller and camera. Call this after the player has
    /// been rebuilt or renamed; it also runs every time the component becomes
    /// active, so it survives AddComponentMenu placement before the controller
    /// exists.
    /// </summary>
    public void ResolveReferences()
    {
        ResolveController();
        if (string.IsNullOrEmpty(_playerName)) _playerName = "Player";
    }

    private void ResolveController()
    {
        if (_controller == null) _controller = GetComponent<FirstPersonController>();
        if (_controller == null) _controller = Object.FindAnyObjectByType<FirstPersonController>();

        if (_controller == null) return;

        if (string.IsNullOrEmpty(_playerName)) _playerName = _controller.gameObject.name;
        if (_camera == null) _camera = FindControllerCamera();
    }

    private Camera FindControllerCamera()
    {
        if (_controller == null) return null;

        if (_controller.playerCamera != null)
        {
            Camera cam = _controller.playerCamera.GetComponent<Camera>();
            if (cam != null) return cam;
        }

        Camera[] found = _controller.GetComponentsInChildren<Camera>();
        return found != null && found.Length > 0 ? found[0] : null;
    }
}
