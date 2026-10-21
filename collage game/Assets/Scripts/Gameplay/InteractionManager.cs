using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// InteractionManager - finds the nearest interactable in front of the player
// every frame and drives the prompt.
//
// This is the only gameplay component that reads input, on top of
// FirstPersonController (which already owns movement, look and the cursor
// lock). Interaction goes through IInteractable, so the manager never knows
// what a fuse or a door actually is - FuseSwitch, HiddenDoor,
// ElevatorObjective and anything else implements the same contract.
//
// Selection combines two checks so the fuse works either way:
//   * camera-centre ray      -> "I am looking at it"
//   * proximity fallback     -> "it is right next to me"
// The better-scoring one wins. See Score().
//
// Attach it to any object in the scene. An InteractionPrompt is picked up if
// one exists, otherwise it is created here.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Interaction Manager")]
[DisallowMultipleComponent]
public class InteractionManager : MonoBehaviour
{
    // -----------------------------------------------------------------
    // STATIC
    // -----------------------------------------------------------------
    private static InteractionManager _instance;

    /// <summary>
    /// The most recently enabled manager, or null. More than one manager is
    /// supported, so this is useful for debug tooling rather than gameplay.
    /// </summary>
    public static InteractionManager Instance
    {
        get { return _instance; }
    }

    // -----------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------
    [Header("Player")]
    [SerializeField] private PlayerSession _player;
    [Tooltip("Camera the interaction ray is cast from. Defaults to the player's camera.")]
    [SerializeField] private Camera _raycastCamera;
    [Tooltip("Score against the camera ray. Keep on for accurate 'looking at it' selection.")]
    [SerializeField] private bool _useRaycast = true;
    [Tooltip("Also score by plain distance, so a fuse mounted on the floor still prompts when the player is standing over it.")]
    [SerializeField] private bool _useProximityFallback = true;

    [Header("Layers")]
    [SerializeField] private LayerMask _interactableLayers = ~0;
    [SerializeField] private LayerMask _excludeLayers = 0;
    [Tooltip("Fallback search radius when the interactable list is empty. Raise it if your interactables sit far from the player's head.")]
    [SerializeField] private float _defaultQueryRange = 5.5f;

    [Header("Prompt")]
    [SerializeField] private InteractionPrompt _prompt;
    [SerializeField] private bool _autoCreatePrompt = true;

    [Header("Debug")]
    [SerializeField] private bool _debugDraw = false;
    [Tooltip("Press this key in play mode to re-arm every tutorial beat without reloading.")]
    [SerializeField] private KeyCode _debugResetKey = KeyCode.F9;
    [SerializeField] private bool _logInteractions = true;

    // -----------------------------------------------------------------
    // STATE
    // -----------------------------------------------------------------
    private IInteractable _currentTarget;
    public IInteractable CurrentTarget => _currentTarget;
    public bool HasTarget => _currentTarget != null;
    private readonly List<IInteractable> _candidates = new List<IInteractable>();
    private Collider[] _physicsHits = new Collider[64];
    private bool _promptReady;
    private bool _announcedChange;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        _instance = this;
        ResolvePrompt();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        ResolvePrompt();
        if (_prompt != null) _prompt.Show("", Color.white);
    }

    private void Update()
    {
        if (_debugResetKey != KeyCode.None && WasResetKeyPressed())
        {
            ResetAllBeats();
        }

        PlayerSession player = ResolvePlayer();
        if (player == null || !player.IsActive)
        {
            SetPrompt(null);
            return;
        }

        _currentTarget = FindBestInteractable(player);
        SetPrompt(_currentTarget);

        if (_currentTarget != null && _logInteractions && !_announcedChange)
        {
            Debug.Log("[InteractionManager] Focused: " + _currentTarget.PromptText);
            _announcedChange = true;
        }
        if (_currentTarget == null) _announcedChange = false;

        if (GameplayUtil.WasInteractPressed()) TryInteract(player);

        DrawDebug(player);
    }

    private void LateUpdate()
    {
        // Candidates are only gathered from what is currently in the physics
        // world, so a scene rebuilt mid-play still works. Refreshing every
        // frame would mean calling FindObjectsOfType continuously, so it is
        // left manual and exposed via RefreshInteractables().
        if (_refreshRequested)
        {
            _interactableCache = null;
            _refreshRequested = false;
        }
    }

    // -----------------------------------------------------------------
    // SELECTION
    // -----------------------------------------------------------------
    /// <summary>
    /// Picks the best interactable for this player, or null.
    /// </summary>
    public IInteractable FindBestInteractable(PlayerSession player)
    {
        if (player == null || !player.IsActive) return null;

        List<IInteractable> candidates = CollectCandidates(player);
        if (candidates.Count == 0) return null;

        Camera cam = ResolveCamera();
        Vector3 origin;
        Vector3 forward;

        if (cam != null)
        {
            origin = cam.transform.position;
            forward = cam.transform.forward;
        }
        else if (player.Controller != null)
        {
            origin = player.HeadPosition;
            forward = player.Controller.transform.forward;
        }
        else
        {
            origin = Vector3.zero;
            forward = Vector3.forward;
        }

        IInteractable best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            IInteractable candidate = candidates[i];
            if (candidate == null) continue;

            float score = Score(candidate, origin, forward);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private List<IInteractable> CollectCandidates(PlayerSession player)
    {
        _candidates.Clear();

        float range = QueryRange();
        LayerMask mask = _interactableLayers.value & ~_excludeLayers.value;
        if (mask == 0) return _candidates;

        if (_physicsHits == null || _physicsHits.Length < 64) _physicsHits = new Collider[64];

        int hitCount = Physics.OverlapSphereNonAlloc(player.HeadPosition, range,
            _physicsHits, mask, QueryTriggerInteraction.Collide);
        if (hitCount == 0) return _candidates;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _physicsHits[i];
            if (hit == null) continue;

            GameObject go = hit.gameObject;
            if (GameplayUtil.IsLocalPlayer(go)) continue;

            IInteractable interactable = FindInteractable(go);
            if (interactable == null) continue;
            if (!interactable.CanInteract(player)) continue;

            _candidates.Add(interactable);
        }

        return _candidates;
    }

    /// <summary>
    /// Largest interaction distance among every interactable in the scene,
    /// so a fuse at 4 m is not culled by another object's 2 m range. Falls
    /// back to _defaultQueryRange when the scene is still empty.
    /// </summary>
    private float QueryRange()
    {
        float maxRange = 0f;

        IInteractable[] all = FindInteractables();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].InteractionDistance > maxRange)
            {
                maxRange = all[i].InteractionDistance;
            }
        }

        if (maxRange <= 0f) maxRange = Mathf.Max(4f, _defaultQueryRange);
        return maxRange + 0.5f;
    }

    private IInteractable[] _interactableCache;
    private bool _refreshRequested;

    private IInteractable[] FindInteractables()
    {
        if (_interactableCache != null) return _interactableCache;

        // IInteractable is a plain interface, not a UnityEngine.Object, so it
        // cannot be a FindObjectsOfType type parameter. Walk the component list
        // instead and cast defensively.
        IInteractable[] found = new IInteractable[0];
        Component[] comps = UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include);
        if (comps != null)
        {
            List<IInteractable> hits = new List<IInteractable>();
            for (int i = 0; i < comps.Length; i++)
            {
                IInteractable hit = comps[i] as IInteractable;
                if (hit != null) hits.Add(hit);
            }
            found = hits.ToArray();
        }
        _interactableCache = found;
        return found;
    }

    /// <summary>
    /// Marks the interactable cache for rebuilding on the next frame. Call
    /// this when objects are added or removed at runtime.
    /// </summary>
    public void RefreshInteractables()
    {
        _refreshRequested = true;
    }

    private float Score(IInteractable target, Vector3 origin, Vector3 forward)
    {
        GameObject go = ResolveGameObject(target);
        if (go == null) return float.MaxValue;

        Vector3 targetPos = go.transform.position;
        if (target is ElevatorPowerSocket)
        {
            Transform s3 = go.transform.Find("Exterior_Front_Assembly/Front_RightPillar_Base/PowerModule_MainHousing/Socket_03");
            if (s3 != null) targetPos = s3.position;
        }

        Vector3 toTarget = targetPos - origin;
        float distance = toTarget.magnitude;
        if (distance > target.InteractionDistance) return float.MaxValue;

        float bestScore = float.MaxValue;

        if (_useRaycast)
        {
            Vector3 direction = toTarget.sqrMagnitude > 0f
                ? toTarget.normalized
                : Vector3.forward;
            float facing = Vector3.Dot(forward, direction);

            if (facing > 0.2f)
            {
                float along = Vector3.Dot(toTarget, forward);
                float offCentre = distance - along;
                bestScore = along + offCentre * 2.5f;
            }
        }

        if (_useProximityFallback)
        {
            bestScore = Mathf.Min(bestScore, distance * 0.5f);
        }

        return bestScore;
    }

    private GameObject ResolveGameObject(IInteractable target)
    {
        if (target is InteractableBase) return ((InteractableBase)target).gameObject;
        if (target is MonoBehaviour) return ((MonoBehaviour)target).gameObject;
        return null;
    }

    private IInteractable FindInteractable(GameObject go)
    {
        if (go == null) return null;

        IInteractable direct = go.GetComponent<IInteractable>();
        if (direct != null) return direct;

        IInteractable inParent = go.GetComponentInParent<IInteractable>();
        if (inParent != null) return inParent;

        IInteractable[] found = go.GetComponentsInChildren<IInteractable>(false);
        if (found != null && found.Length > 0) return found[0];

        return null;
    }

    // -----------------------------------------------------------------
    // INTERACTION
    // -----------------------------------------------------------------
    private void TryInteract(PlayerSession player)
    {
        IInteractable target = _currentTarget;
        if (target == null || !target.CanInteract(player))
        {
            target = FindBestInteractable(player);
        }

        if (target == null) return;

        bool consumed = target.OnInteract(player);
        if (consumed && _logInteractions)
        {
            Debug.Log("[InteractionManager] Interacted: " + target.PromptText +
                " by " + GameplayUtil.SafeName(player));
            _currentTarget = null;
        }
    }

    // -----------------------------------------------------------------
    // PROMPT
    // -----------------------------------------------------------------
    private void SetPrompt(IInteractable target)
    {
        if (!_promptReady) return;

        if (target == null)
        {
            _prompt.Hide();
            return;
        }

        _prompt.Show(target.PromptText, target.PromptColor);
    }

    private void ResolvePrompt()
    {
        if (_prompt != null)
        {
            _promptReady = true;
            return;
        }

        // Fully qualified: a `using System;` makes bare `Object` ambiguous
        // between UnityEngine.Object and the C# `object` keyword type.
        _prompt = UnityEngine.Object.FindAnyObjectByType<InteractionPrompt>();
        if (_prompt == null && _autoCreatePrompt)
        {
            _prompt = gameObject.AddComponent<InteractionPrompt>();
        }

        _promptReady = _prompt != null;
    }

    private Camera ResolveCamera()
    {
        if (_raycastCamera != null) return _raycastCamera;

        PlayerSession player = ResolvePlayer();
        if (player != null && player.Camera != null) return player.Camera;

        if (Camera.main != null) return Camera.main;

        Camera[] all = Camera.allCameras;
        return all != null && all.Length > 0 ? all[0] : null;
    }

    private PlayerSession ResolvePlayer()
    {
        if (_player != null) return _player;
        _player = GameplayUtil.FindPlayer();
        return _player;
    }

    private bool WasResetKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;

        switch (_debugResetKey)
        {
            case KeyCode.F1: return kb.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return kb.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return kb.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return kb.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return kb.f5Key.wasPressedThisFrame;
            case KeyCode.F6: return kb.f6Key.wasPressedThisFrame;
            case KeyCode.F7: return kb.f7Key.wasPressedThisFrame;
            case KeyCode.F8: return kb.f8Key.wasPressedThisFrame;
            case KeyCode.F9: return kb.f9Key.wasPressedThisFrame;
            case KeyCode.F10: return kb.f10Key.wasPressedThisFrame;
            case KeyCode.F11: return kb.f11Key.wasPressedThisFrame;
            case KeyCode.F12: return kb.f12Key.wasPressedThisFrame;
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            default: break;
        }
        return false;
#else
        return UnityEngine.Input.GetKeyDown(_debugResetKey);
#endif
    }

    private void DrawDebug(PlayerSession player)
    {
        if (!_debugDraw || player == null || _currentTarget == null) return;

        if (_currentTarget is MonoBehaviour)
        {
            Debug.DrawLine(player.HeadPosition,
                ((MonoBehaviour)_currentTarget).transform.position,
                _currentTarget.PromptColor, 0.5f);
        }
    }

    // -----------------------------------------------------------------
    // DEV HELPERS
    // -----------------------------------------------------------------
    /// <summary>
    /// Re-arms every tutorial beat in the scene. Bound to a debug key so the
    /// solo playtest loop never needs a reload.
    /// </summary>
    public static void ResetAllBeats()
    {
        ResetComponents<FuseSwitch>(c => c.Reset());
        ResetComponents<HiddenDoor>(c => c.Reset());
        ResetComponents<HiddenPitTrap>(c => c.Reset());
        ResetComponents<ElevatorObjective>(c => c.Reset());
        Debug.Log("[InteractionManager] All tutorial beats reset.");
    }

    private static void ResetComponents<T>(Action<T> action) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        if (all == null) return;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            try { action(all[i]); }
            catch (Exception) { }
        }
    }
}
