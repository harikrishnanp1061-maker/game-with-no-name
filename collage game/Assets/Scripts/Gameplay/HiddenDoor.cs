using System;
using UnityEngine;

// =========================================================================
// HiddenDoor - a false wall that opens when the player presses E while
// looking at it. Used for the hidden door at z ~ 90 (NorthFalseWall) and
// anywhere else a wall needs to turn out to be a doorway.
//
// Two reveal approaches:
//   * Slide - the wall slides along local Y (or X) and stays there.
//   * Fade  - every renderer on the wall fades to invisible, and the collider
//     is disabled so the doorway is walkable. Best for the greybox phase.
//
// Reveal outline: when revealOutline is on, a thin translucent cyan shell is
// generated from the door's own mesh and only rendered within 2.5 m, so the
// door is findable without being obvious. It is a solo-testing affordance -
// turn it off for the co-op build.
//
// Opening the wall only hides the wall; it does not clear whatever is behind
// it. Use disableBlockers = true (or ClearBlocksBehind()) for a false wall
// that sits in front of solid geometry such as a blast door or a crate - that
// also hides the blocker renderers so the player sees a real hole instead of
// an invisible wall of solid air.
// =========================================================================
[RequireComponent(typeof(Collider))]
[AddComponentMenu("FLIP//FALL/Gameplay/Hidden Door")]
[DisallowMultipleComponent]
public class HiddenDoor : MonoBehaviour, IInteractable
{
    public enum OpenMode
    {
        Slide,
        Fade
    }

    public enum DoorState
    {
        Closed,
        Opening,
        Open
    }

    /// <summary>Raised once the door has finished opening.</summary>
    public static event Action<HiddenDoor> OnOpened;

    /// <summary>Raised when the door returns to closed (repeatable doors).</summary>
    public static event Action<HiddenDoor> OnClosed;

    // -----------------------------------------------------------------
    // CONFIG
    // -----------------------------------------------------------------
    [Header("Prompt")]
    [SerializeField] private string _promptClosed = "PUSH SECRET WALL";
    [SerializeField] private string _promptOpen = "WAY OPEN";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptColor = new Color(0.35f, 0.95f, 1f, 1f);

    [Header("Range")]
    [SerializeField] private float _interactionDistance = 3.5f;

    [Header("Opening")]
    [SerializeField] private OpenMode _openMode = OpenMode.Slide;
    [SerializeField] private float _slideDistance = 3.8f;
    [SerializeField] private bool _slideDown = true;
    [SerializeField] private bool _slideAlongX = false;
    [SerializeField] private float _fadeSpeed = 3.2f;
    [SerializeField] private float _slideSpeed = 4f;

    [Header("Persistence")]
    [SerializeField] private bool _opensOnce = true;
    [Tooltip("If true the door closes again once the player walks clear of it. Ignored when opensOnce is true.")]
    [SerializeField] private bool _closesWhenClear = false;
    [SerializeField] private float _closeDistance = 6f;

    [Header("Path Clearing")]
    [Tooltip("Clear the solid geometry behind the false wall when it opens. A false wall is usually just a skin in front of real geometry, so without this the doorway would fade to nothing and the player would still hit a wall.")]
    [SerializeField] private bool _disableBlockers = false;
    [Tooltip("Also hide the renderers of the blockers, so the hole is visible rather than a floating gap of solid air.")]
    [SerializeField] private bool _hideBlockers = true;
    [Tooltip("Optional explicit list of GameObjects to clear. Empty uses blockersRoot.")]
    [SerializeField] private GameObject[] _blockers;
    [SerializeField] private Transform _blockersRoot;

    [Header("Reveal Outline")]
    [SerializeField] private bool _revealOutline = true;
    [SerializeField] private float _revealRange = 2.5f;
    [SerializeField] private float _revealShellScale = 1.006f;
    [SerializeField] private float _revealPulseSpeed = 2.6f;
    [SerializeField] private float _revealMaxAlpha = 0.22f;
    [SerializeField] private float _revealMinAlpha = 0.04f;
    [ColorUsage(true, true)]
    [SerializeField] private Color _revealColor = new Color(0.2f, 1f, 1f, 1f);
    [Tooltip("Shader used for the generated shell. Leave empty to use a plain unlit transparent shader, created on the fly in the editor.")]
    [SerializeField] private Shader _revealShader;
    [Tooltip("Shader name tried when no shader asset is assigned.")]
    [SerializeField] private string _revealShaderName = "Universal Render Pipeline/Lit";
    [SerializeField] private float _revealStartDistance = 1.4f;

    [Header("Colliders")]
    [SerializeField] private bool _disableColliderWhenOpen = true;

    [Header("Tag")]
    [SerializeField] private string _doorTag = "HiddenDoor";

    // -----------------------------------------------------------------
    // PUBLIC STATE
    // -----------------------------------------------------------------
    public DoorState CurrentDoorState { get { return _doorState; } }
    public bool IsOpen { get { return _doorState == DoorState.Open; } }
    public bool OpenedOnce { get { return _hasOpened; } }

    public string PromptText
    {
        get { return IsOpen ? _promptOpen : _promptClosed; }
    }

    public Color PromptColor { get { return _promptColor; } }
    public float InteractionDistance { get { return _interactionDistance; } }
    public string State => _doorState.ToString();

    // -----------------------------------------------------------------
    // PRIVATE STATE
    // -----------------------------------------------------------------
    [SerializeField] private DoorState _doorState = DoorState.Closed;
    [SerializeField] private bool _hasOpened = false;

    private bool _outlineBuilt;
    private bool _outlineVisible;
    private bool _beatFired;


    private Vector3 _closedLocalPosition;
    private Vector3 _openLocalPosition;
    private float _fadeAlpha;

    private Renderer[] _bodyRenderers;
    private Material[] _bodyMaterials;
    private Color[] _bodyBaseColors;
    private GameObject _outlineShell;
    private Material _outlineMaterial;
    private Collider[] _colliders;
    private Collider[] _blockerColliders;
    private Renderer[] _blockerRenderers;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        _closedLocalPosition = transform.localPosition;
        ApplyOpenModeDefaults();
        CacheRenderers();
        CacheColliders();
        CacheBlockers();

        if (!GameplayUtil.TrySetTag(gameObject, _doorTag) && !string.IsNullOrEmpty(_doorTag))
        {
            Debug.LogWarning("[HiddenDoor] Tag '" + _doorTag +
                "' is missing, so it was not applied. Run " +
                "Tools > FLIP//FALL > Create Gameplay Tags. Tag filtering is optional.");
        }
    }

    private void Start()
    {
        if (_openMode == OpenMode.Fade)
        {
            if (!_hasOpened) ApplyFade(1f, false);
        }
    }

    private void Update()
    {
        UpdateOutlineVisibility();
        UpdateClosesWhenClear();

        switch (_doorState)
        {
            case DoorState.Opening:
                AnimateOpen();
                break;

            case DoorState.Open:
                // Fade is driven here; Slide is driven in AnimateOpen.
                if (_openMode == OpenMode.Fade && _bodyRenderers != null)
                {
                    float alpha = Mathf.MoveTowards(_fadeAlpha, 0f,
                        Time.deltaTime * _fadeSpeed);
                    if (!Mathf.Approximately(alpha, _fadeAlpha))
                    {
                        ApplyFade(alpha, false);
                    }

                    // The fade can take a few frames, so the beat only counts
                    // once the wall is actually gone.
                    if (alpha <= 0.001f && !_beatFired)
                    {
                        _beatFired = true;
                        OnOpened?.Invoke(this);
                    }
                }
                break;
        }

        if (_outlineVisible) PulseOutline();
    }

    private void OnDestroy()
    {
        if (_outlineShell != null) Destroy(_outlineShell);
        if (_outlineMaterial != null) Destroy(_outlineMaterial);
    }

    // -----------------------------------------------------------------
    // INTERACTION
    // -----------------------------------------------------------------
    public bool CanInteract(PlayerSession player)
    {
        if (player == null || !player.IsActive) return false;
        if (_doorState == DoorState.Opening || _doorState == DoorState.Open) return false;
        if (_hasOpened && _opensOnce) return false;

        return Vector3.Distance(player.HeadPosition, transform.position) <= _interactionDistance;
    }

    public bool OnInteract(PlayerSession player)
    {
        if (!CanInteract(player)) return false;

        Open(player);
        return true;
    }

    public void Configure(OpenMode mode = OpenMode.Slide, float slideDistance = 3.8f, bool slideDown = true, float slideSpeed = 3.2f, string promptClosed = "PUSH SECRET WALL")
    {
        _openMode = mode;
        _slideDistance = slideDistance;
        _slideDown = slideDown;
        _slideSpeed = slideSpeed;
        _promptClosed = promptClosed;
    }

    /// <summary>
    /// Opens the door. Called by the interaction manager; also usable from an
    /// event, a puzzle chain or the dev reset tools.
    /// </summary>
    public void Open(PlayerSession player)
    {
        if (_doorState == DoorState.Opening || _doorState == DoorState.Open) return;
        if (_hasOpened && _opensOnce) return;

        _hasOpened = true;
        _doorState = DoorState.Opening;
        _beatFired = false;
        Debug.Log("[HiddenDoor] " + gameObject.name + " opening.");

        if (_disableBlockers) DisableBlockers();
        if (_disableColliderWhenOpen && _colliders != null)
        {
            foreach (Collider c in _colliders)
            {
                if (c != null) c.enabled = false;
            }
        }

        _fadeAlpha = 1f;

        if (_openMode == OpenMode.Fade)
        {
            // The fade itself runs in Update(); it raises OnOpened once the
            // wall is fully invisible. Colliders are already disabled above,
            // so the doorway is walkable from the first frame.
            _doorState = DoorState.Open;
        }
        else
        {
            _openLocalPosition = CalculateOpenPosition();
        }
    }

    private Vector3 CalculateOpenPosition()
    {
        Vector3 dir = _slideAlongX
            ? new Vector3(_slideDown ? 1f : -1f, 0f, 0f)
            : new Vector3(0f, _slideDown ? -1f : 1f, 0f);
        return _closedLocalPosition + dir * _slideDistance;
    }

    private void AnimateOpen()
    {
        if (_openMode != OpenMode.Slide) return;

        Vector3 target = _openLocalPosition;
        Vector3 next = Vector3.MoveTowards(transform.localPosition, target,
            _slideSpeed * Time.deltaTime);
        transform.localPosition = next;

        if (Vector3.Distance(next, target) < 0.01f)
        {
            transform.localPosition = target;
            _doorState = DoorState.Open;
            OnOpened?.Invoke(this);
        }
    }

    /// <summary>
    /// Closes the door. Only meaningful for repeatable doors.
    /// </summary>
    public void Close()
    {
        if (_opensOnce) return;

        _hasOpened = false;
        RestoreWall();
        _doorState = DoorState.Closed;
        OnClosed?.Invoke(this);
    }

    /// <summary>
    /// Re-arms the door so the beat can be replayed in the solo testing phase.
    /// </summary>
    public void Reset()
    {
        _hasOpened = false;
        RestoreWall();
        _doorState = DoorState.Closed;
    }

    /// <summary>
    /// Puts the wall back exactly as it was built: visible, solid, and with any
    /// cleared blockers re-enabled. Everything "opening" touches has to come
    /// back through here, or a repeatable door would stay permanently open.
    /// </summary>
    private void RestoreWall()
    {
        if (_openMode == OpenMode.Slide)
        {
            transform.localPosition = _closedLocalPosition;
        }
        else
        {
            ApplyFade(1f, true);
        }

        if (_colliders != null)
        {
            foreach (Collider c in _colliders)
            {
                if (c != null) c.enabled = true;
            }
        }

        RestoreBlockers();
    }

    /// <summary>
    /// Repeats the beat: once the player walks clear of a repeatable door it
    /// closes again so the hidden-door loop can be tested over and over.
    /// </summary>
    private void UpdateClosesWhenClear()
    {
        if (!_closesWhenClear || _opensOnce) return;
        if (_doorState != DoorState.Open) return;
        if (_openMode == OpenMode.Fade && _fadeAlpha > 0.01f) return;

        PlayerSession player = GameplayUtil.FindPlayer();
        if (player == null) return;

        float dist = Vector3.Distance(player.HeadPosition, transform.position);
        if (dist > _closeDistance)
        {
            Reset();
        }
    }

    // -----------------------------------------------------------------
    // FADE
    // -----------------------------------------------------------------
    private void ApplyFade(float alpha, bool restoreOpacity)
    {
        _fadeAlpha = alpha;
        if (_bodyRenderers == null) return;

        bool fullyVisible = alpha >= 0.999f;
        bool fullyHidden = alpha <= 0.001f;

        for (int i = 0; i < _bodyRenderers.Length; i++)
        {
            Renderer r = _bodyRenderers[i];
            if (r == null) continue;

            // A renderer that was switched off while fading out has to be
            // switched back on before it can be seen again.
            if (restoreOpacity) r.enabled = true;

            Material m = _bodyMaterials[i];
            if (m != null)
            {
                m.SetColor("_Color", Color.Lerp(Color.white, _bodyBaseColors[i], alpha));
                m.SetFloat("_Alpha", alpha);
                m.SetFloat("_BaseAlpha", alpha);

                // Only flip to a transparent surface while there is actually
                // some transparency to show - the shared material goes back to
                // opaque once the wall is fully visible.
                if (!fullyVisible)
                {
                    if (m.renderQueue < 3000) m.renderQueue = 3001;
                    m.SetInt("_Surface", 1); // URP transparent surface
                }
            }

            if (fullyHidden && !fullyVisible)
            {
                if (!restoreOpacity) r.enabled = false;
            }
            else if (fullyVisible)
            {
                r.enabled = true;
            }
        }
    }

    // -----------------------------------------------------------------
    // REVEAL OUTLINE
    // -----------------------------------------------------------------
    private void UpdateOutlineVisibility()
    {
        if (!_outlineBuilt && _revealOutline) BuildOutline();
        if (_outlineShell == null) return;

        PlayerSession player = GameplayUtil.FindPlayer();
        if (player == null)
        {
            SetOutlineVisible(false);
            return;
        }

        float dist = Vector3.Distance(player.HeadPosition, transform.position);
        bool closed = _doorState == DoorState.Closed && !_hasOpened;
        SetOutlineVisible(closed && dist <= _revealRange);

        if (_outlineVisible && _outlineMaterial != null)
        {
            // Brighten as the player closes in instead of snapping on, so it
            // reads as a subtle shimmer rather than a marker.
            float inner = Mathf.Max(0f, _revealStartDistance);
            float t = Mathf.InverseLerp(_revealRange, inner, dist);
            float fade = Mathf.Lerp(_revealMinAlpha, _revealMaxAlpha, t);
            Color c = _revealColor;
            c.a = fade;
            _outlineMaterial.color = c;
        }
    }

    private void SetOutlineVisible(bool visible)
    {
        if (_outlineVisible == visible) return;
        _outlineVisible = visible;
        if (_outlineShell != null) _outlineShell.SetActive(visible);
    }

    private void PulseOutline()
    {
        if (_outlineMaterial == null) return;

        float t = (Mathf.Sin(Time.time * _revealPulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(_revealMinAlpha, _revealMaxAlpha, t);
        Color c = _revealColor;
        c.a = alpha;
        _outlineMaterial.color = c;
    }

    private void BuildOutline()
    {
        _outlineBuilt = true;

        Renderer source = null;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        if (renderers != null)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null && r.GetComponent<MeshFilter>() != null)
                {
                    source = r;
                    break;
                }
            }
        }

        Mesh mesh = null;
        if (source != null)
        {
            MeshFilter mf = source.GetComponent<MeshFilter>();
            mesh = mf != null ? mf.sharedMesh : null;
        }

        if (mesh == null) mesh = CreateBoxMesh();
        if (mesh == null) return;

        GameObject shell = new GameObject("RevealOutline");
        shell.transform.SetParent(transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localRotation = Quaternion.identity;
        shell.transform.localScale = Vector3.one * _revealShellScale;

        MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
        shellFilter.sharedMesh = mesh;

        MeshRenderer mr = shell.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateOutlineMaterial();

        if (mr.sharedMaterial == null)
        {
            Destroy(shell);
            return;
        }

        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.enabled = false;

        Collider killCollisions = shell.GetComponent<Collider>();
        if (killCollisions == null) killCollisions = shell.AddComponent<MeshCollider>();
        killCollisions.isTrigger = true;
        killCollisions.enabled = false;

        _outlineShell = shell;
        SetOutlineVisible(false);
    }

    private Material CreateOutlineMaterial()
    {
        Shader shader = _revealShader != null ? _revealShader : Shader.Find(_revealShaderName);
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        if (shader == null)
        {
            Debug.LogWarning("[HiddenDoor] Could not find any shader for the reveal outline; " +
                "assign _revealShader. The door still works without it.");
            return null;
        }

        Material m = new Material(shader);
        m.name = "FLIP_FALL_HiddenDoorReveal";
        m.SetColor("_Color", _revealColor);
        m.color = _revealColor;
        _outlineMaterial = m;
        return m;
    }

    private static Mesh CreateBoxMesh()
    {
        Mesh m = new Mesh();
        m.name = "FLIP_FALL_RevealBox";
        m.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
        };

        m.triangles = new int[]
        {
            0, 2, 1, 0, 3, 2,   // back
            5, 4, 7, 5, 7, 6,   // front
            4, 0, 3, 4, 3, 7,   // left
            1, 5, 6, 1, 6, 2,   // right
            3, 2, 6, 3, 6, 7,   // top
            4, 5, 1, 4, 1, 0    // bottom
        };

        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // -----------------------------------------------------------------
    // PATH CLEARING
    // -----------------------------------------------------------------
    private void CacheBlockers()
    {
        if (!_disableBlockers) return;

        System.Collections.Generic.List<Collider> colliders =
            new System.Collections.Generic.List<Collider>();
        System.Collections.Generic.List<Renderer> renderers =
            new System.Collections.Generic.List<Renderer>();

        System.Action<Transform> sweep = delegate (Transform root)
        {
            if (root == null) return;
            colliders.AddRange(root.GetComponentsInChildren<Collider>(true));
            if (_hideBlockers)
            {
                renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            }
        };

        if (_blockers != null)
        {
            foreach (GameObject blocker in _blockers)
            {
                if (blocker != null) sweep(blocker.transform);
            }
        }

        if (_blockersRoot != null) sweep(_blockersRoot);

        // Never touch our own meshes or any trigger collider - those belong to
        // the wall, and triggers must stay live for the pit/outline logic.
        System.Collections.Generic.List<Collider> keptColliders =
            new System.Collections.Generic.List<Collider>();
        foreach (Collider c in colliders)
        {
            if (c == null || c.isTrigger) continue;
            if (GameplayUtil.IsDescendantOf(c.transform, transform)) continue;
            keptColliders.Add(c);
        }

        System.Collections.Generic.List<Renderer> keptRenderers =
            new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (GameplayUtil.IsDescendantOf(r.transform, transform)) continue;
            keptRenderers.Add(r);
        }

        _blockerColliders = keptColliders.ToArray();
        _blockerRenderers = keptRenderers.ToArray();
    }

    private void DisableBlockers()
    {
        if (_blockerColliders == null || _blockerColliders.Length == 0) return;

        // Clear the colliders first - they are what actually stop the player.
        for (int i = 0; i < _blockerColliders.Length; i++)
        {
            Collider c = _blockerColliders[i];
            if (c == null || c.isTrigger) continue;
            c.enabled = false;
        }

        // Then make the geometry read as a hole, otherwise the player walks
        // through an invisible slab of solid air.
        if (!_hideBlockers) return;

        Renderer[] renderers = _blockerRenderers;
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r != null) r.enabled = false;
        }
    }

    /// <summary>
    /// Public helper so a puzzle chain or the dev tools can open the doorway
    /// without flipping individual colliders.
    /// </summary>
    public void ClearBlocksBehind()
    {
        DisableBlockers();
    }

    private void RestoreBlockers()
    {
        if (!_disableBlockers) return;

        if (_blockerColliders != null)
        {
            foreach (Collider c in _blockerColliders)
            {
                if (c != null && !c.isTrigger) c.enabled = true;
            }
        }

        if (_hideBlockers && _blockerRenderers != null)
        {
            foreach (Renderer r in _blockerRenderers)
            {
                if (r != null) r.enabled = true;
            }
        }
    }

    // -----------------------------------------------------------------
    // CACHING
    // -----------------------------------------------------------------
    private void CacheRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        if (renderers == null) renderers = new Renderer[0];

        _bodyRenderers = new Renderer[renderers.Length];
        _bodyMaterials = new Material[renderers.Length];
        _bodyBaseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            _bodyRenderers[i] = r;
            if (r == null) continue;

            Material[] mats = r.sharedMaterials;
            Material first = mats != null && mats.Length > 0 ? mats[0] : null;
            _bodyMaterials[i] = first;
            _bodyBaseColors[i] = first != null ? first.color : Color.white;
        }
    }

    private void CacheColliders()
    {
        _colliders = GetComponentsInChildren<Collider>(false);
    }

    private void ApplyOpenModeDefaults()
    {
        if (_fadeSpeed <= 0f) _fadeSpeed = 3.2f;
        if (_slideSpeed <= 0f) _slideSpeed = 4f;
        if (_slideDistance <= 0f) _slideDistance = 4f;
        if (_revealRange <= 0f) _revealRange = 2.5f;
    }
}
