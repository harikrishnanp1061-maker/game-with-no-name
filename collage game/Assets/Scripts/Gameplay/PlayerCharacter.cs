using UnityEngine;

// =========================================================================
// PlayerCharacter - Physical Player Character in the World
//
// Represents one of the four cooperative players in FLIP//FALL.
// Every player physically exists in the world at all times.
// When the local player switches to this character, the camera & input
// move to THIS character's body position in the world.
// Inactive characters remain standing/idling at their current world location.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Player Character")]
[DisallowMultipleComponent]
public class PlayerCharacter : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private PlayerIdentity _identity = PlayerIdentity.Player1;

    [Header("Components")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private FirstPersonController _firstPersonController;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _cameraHolder;
    [SerializeField] private Transform _modelRoot;
    [SerializeField] private PlayerTorchAbility _torchAbility;

    [Header("State")]
    [SerializeField] private bool _isControlled = false;

    public PlayerIdentity Identity => _identity;
    public CharacterController CharacterController => _characterController;
    public FirstPersonController FirstPersonController => _firstPersonController;
    public Animator Animator => _animator;
    public Transform CameraHolder => _cameraHolder;
    public Transform ModelRoot => _modelRoot;
    public PlayerTorchAbility TorchAbility => _torchAbility;
    public bool IsControlled => _isControlled;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Initialize(PlayerIdentity id)
    {
        _identity = id;
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (_characterController == null) _characterController = GetComponent<CharacterController>();
        if (_firstPersonController == null) _firstPersonController = GetComponent<FirstPersonController>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        if (_torchAbility == null) _torchAbility = GetComponent<PlayerTorchAbility>();

        if (_cameraHolder == null)
        {
            Transform found = transform.Find("CameraHolder");
            if (found != null) _cameraHolder = found;
            else
            {
                GameObject holder = new GameObject("CameraHolder");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = Vector3.zero;
                _cameraHolder = holder.transform;
            }
        }

        if (_cameraHolder != null)
        {
            _cameraHolder.localPosition = Vector3.zero;
            _cameraHolder.localRotation = Quaternion.identity;
        }

        if (_modelRoot == null)
        {
            _modelRoot = transform.Find("Model_Animated");
        }

        if (_modelRoot != null)
        {
            var smrs = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                smr.updateWhenOffscreen = true;
            }
        }
    }

    private void Start()
    {
        UpdateMeshVisibility();
    }

    /// <summary>
    /// For true first person, the actively controlled character's mesh is set to ShadowsOnly
    /// so the player's own head/shoulders do not block the view, while still casting real shadows.
    /// Inactive teammate characters in the world remain 100% visible (ShadowCastingMode.On).
    /// </summary>
    public void UpdateMeshVisibility()
    {
        Transform model = _modelRoot != null ? _modelRoot : transform.Find("Model_Animated");
        if (model != null)
        {
            Renderer[] rends = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r.gameObject.name.Contains("Torch") || r.gameObject.name.Contains("Lens")) continue;
                r.shadowCastingMode = _isControlled 
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly 
                    : UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                smr.updateWhenOffscreen = true;
            }
        }
    }

    /// <summary>
    /// Temporarily overrides mesh visibility for external cameras (e.g. PiP Preview Camera).
    /// When forceVisible is true, sets shadowCastingMode to On so the active player's body
    /// and animations are fully rendered in the preview camera.
    /// When forceVisible is false, restores the default first-person ShadowsOnly state.
    /// </summary>
    public void SetForceMeshVisible(bool forceVisible)
    {
        Transform model = _modelRoot != null ? _modelRoot : transform.Find("Model_Animated");
        if (model != null)
        {
            Renderer[] rends = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                if (r.gameObject.name.Contains("Torch") || r.gameObject.name.Contains("Lens")) continue;
                r.shadowCastingMode = forceVisible 
                    ? UnityEngine.Rendering.ShadowCastingMode.On 
                    : (_isControlled ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On);
            }
        }
    }

    /// <summary>
    /// Activates or deactivates active local control of this character body.
    /// When active, camera and audio listener attach to this character's CameraHolder.
    /// </summary>
    public void SetControlled(bool controlled, Camera mainCam = null, AudioListener listener = null)
    {
        _isControlled = controlled;
        ResolveReferences();
        UpdateMeshVisibility();

        if (_firstPersonController != null)
        {
            _firstPersonController.IsControlled = controlled;
            _firstPersonController.CanMove = controlled;
        }

        if (controlled && mainCam != null && _cameraHolder != null)
        {
            // Move camera to this character's physical body in the world!
            mainCam.transform.SetParent(_cameraHolder, false);
            mainCam.transform.localPosition = new Vector3(0f, _firstPersonController != null ? _firstPersonController.standingCamY : 1.70f, 0.1f);
            mainCam.transform.localRotation = Quaternion.identity;

            if (_firstPersonController != null)
            {
                _firstPersonController.playerCamera = mainCam.transform;
                _firstPersonController.ResetLookAngles(0f, transform.eulerAngles.y);
            }

            if (listener != null)
            {
                listener.transform.SetParent(_cameraHolder, false);
                listener.transform.localPosition = Vector3.zero;
                listener.transform.localRotation = Quaternion.identity;
            }
        }

        if (!controlled && _firstPersonController != null)
        {
            // Detach camera reference so background coroutines on this character cannot steer the main camera
            _firstPersonController.playerCamera = null;

            // Zero out movement velocity so character stands still where left
            if (_animator != null)
            {
                _animator.SetFloat("Speed", 0f);
            }
        }
    }

    public void PlayWakeUp()
    {
        if (_animator != null)
        {
            _animator.ResetTrigger("WakeUp");
            _animator.SetTrigger("WakeUp");
            _animator.Play("WakeUp", 0, 0f);
        }

        if (_firstPersonController != null)
        {
            _firstPersonController.TriggerWakeUp();
        }
    }
}
