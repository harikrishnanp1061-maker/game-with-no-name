using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 7.5f;
    public float crouchSpeed = 2.2f;
    public float jumpHeight = 1.25f;
    public float gravity = -18.0f;

    [Header("Air Momentum & Physics")]
    [Tooltip("Rate at which player can steer or adjust direction while airborne (m/s^2)")]
    public float airControlAcceleration = 12.0f;
    [Tooltip("Gentle horizontal air drag when letting go of navigation keys while airborne (m/s^2)")]
    public float airDragDeceleration = 1.5f;

    [Header("Crouch Settings")]
    public float standingHeight = 2.15f;
    public float crouchHeight = 1.35f;
    public float standingCamY = 1.82f;
    public float crouchCamY = 1.15f;
    public float crouchTransitionSpeed = 8f;
    public bool toggleCrouchWithC = true;

    [Header("Look Settings")]
    public float mouseSensitivity = 2.0f;
    public float maxLookAngle = 85.0f;

    [Header("Camera & Head Bob")]
    public Transform playerCamera;
    public float headBobFrequency = 1.6f;
    public float headBobAmplitude = 0.05f;

    [Header("Running Screen Sway")]
    [Tooltip("Add subtle natural camera sway roll when sprinting (ever so slightly)")]
    public bool enableRunningSway = true;
    public float runSwayFrequency = 8.5f;
    public float runSwayRollAmount = 0.95f;
    public float runSwayYawAmount = 0.35f;
    private float runSwayTimer = 0f;
    private float currentRunRoll = 0f;
    private float currentRunYaw = 0f;
    private bool isCurrentlySprinting = false;
    private bool isCurrentlyMoving = false;

    [Header("Fall Safety")]
    public float killPlaneY = -30f;
    public float respawnY = 0.5f;

    [SerializeField] private bool _isControlled = true;
    public bool IsControlled
    {
        get => _isControlled;
        set
        {
            _isControlled = value;
            if (!_isControlled)
            {
                if (movementAudioSource != null && movementAudioSource.isPlaying)
                {
                    movementAudioSource.Stop();
                }
                if (breathingAudioSource != null && breathingAudioSource.isPlaying)
                {
                    breathingAudioSource.volume = 0f;
                    breathingAudioSource.Stop();
                }
                distanceTraveled = 0f;
            }
        }
    }
    public bool CanMove = true;
    public bool CanLook = true;
    public float VerticalRotation => verticalRotation;

    public void SetVerticalRotation(float angle)
    {
        verticalRotation = Mathf.Clamp(angle, -maxLookAngle, maxLookAngle);
        if (playerCamera != null)
        {
            float currentRoll = playerCamera.localEulerAngles.z;
            playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, currentRoll);
        }
    }
    public Animator characterAnimator;
    public PlayerTorchAbility torchAbility;

    [Header("Stamina & Exhaustion Audio")]
    [Tooltip("Fast breathing when stamina is exhausted (hasin2004-breathing-fast-247449)")]
    public AudioClip fastBreathingClip;
    public float maxSprintStamina = 6.0f;
    public float staminaDrainRate = 1.0f;
    public float staminaRecoveryRate = 1.25f;

    [Header("Footsteps & Movement Audio")]
    [Tooltip("Continuous walking/jogging track (freesound_community-concrete-footsteps-6752)")]
    public AudioClip walkingAudioTrack;
    [Tooltip("Continuous running track (freesound_community-running-sounds-6003)")]
    public AudioClip runningAudioTrack;
    public AudioClip[] footstepClips;
    [Tooltip("Mild jogging / walking footsteps")]
    public AudioClip[] joggingFootstepClips;
    [Tooltip("Sprint / running footsteps")]
    public AudioClip[] runningFootstepClips;
    public AudioClip jumpClip;
    public AudioClip landClip;
    public float footstepWalkDistance = 1.8f;
    public float footstepSprintDistance = 1.4f;
    public float footstepCrouchDistance = 1.3f;

    private float currentStamina = 6.0f;
    private bool isExhausted = false;
    private float distanceTraveled = 0f;
    private bool wasGroundedLastFrame = true;
    private AudioSource movementAudioSource;
    private AudioSource breathingAudioSource;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 horizontalVelocity = Vector3.zero;
    private float verticalRotation = 0.0f;
    private Vector3 defaultCameraLocalPos;
    [HideInInspector] public Vector3 lastSafePosition;
    [HideInInspector] public bool hasLastSafe = false;
    private float bobTimer = 0.0f;
    private bool isGrounded;
    private bool isCrouching = false;
    private bool crouchToggleState = false;
    private float currentCamY = 1.82f;

    public bool IsCrouching => isCrouching;
    public bool IsGrounded => isGrounded;
    public CharacterController CharacterController => controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
        if (characterAnimator != null)
        {
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        if (torchAbility == null) torchAbility = GetComponent<PlayerTorchAbility>();
        EnsureAudioSources();

        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            smr.updateWhenOffscreen = true;
        }
    }

    void Start()
    {
        EnsureAudioSources();
        currentStamina = maxSprintStamina;
        currentCamY = standingCamY;

        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                playerCamera = cam.transform;
            }
            else if (Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }
        }

        if (playerCamera != null)
        {
            // Guard against test scripts elevating child camera
            Camera childCam = GetComponentInChildren<Camera>();
            if (childCam != null && childCam.transform != playerCamera && childCam.transform.parent == playerCamera)
            {
                childCam.transform.localPosition = Vector3.zero;
                childCam.transform.localRotation = Quaternion.identity;
            }

            defaultCameraLocalPos = new Vector3(0f, standingCamY, 0.1f);
            playerCamera.localRotation = Quaternion.identity;
        }

        verticalRotation = 0.0f;

        if (IsControlled)
        {
            LockCursor();
        }

        lastSafePosition = transform.position;
        lastSafePosition.y = respawnY;
        hasLastSafe = true;
    }

    public void ResetLookAngles(float pitch = 0f, float yaw = 0f)
    {
        verticalRotation = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        currentRunRoll = 0f;
        currentRunYaw = 0f;
        runSwayTimer = 0f;
        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }


    void Update()
    {
        if (IsControlled)
        {
            HandleCursorLock();
            HandleLook();
        }
        else
        {
            if (movementAudioSource != null && movementAudioSource.isPlaying)
            {
                movementAudioSource.Stop();
            }
            if (breathingAudioSource != null && breathingAudioSource.isPlaying)
            {
                breathingAudioSource.volume = 0f;
                breathingAudioSource.Stop();
            }
            distanceTraveled = 0f;
        }

        HandleCrouch();
        HandleMovement();
        HandleHeadBob();
        HandleRunningSway();
        HandleFallProtection();
        UpdateAnimator();
    }

    private void HandleCrouch()
    {
        if (IsControlled && CanMove)
        {
            bool holdCrouch = false;
            bool togglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                holdCrouch = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
                if (toggleCrouchWithC)
                {
                    if (Keyboard.current.cKey.wasPressedThisFrame) togglePressed = true;
                }
                else
                {
                    holdCrouch = holdCrouch || Keyboard.current.cKey.isPressed;
                }
            }
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonEast.wasPressedThisFrame) togglePressed = true;
                if (Gamepad.current.rightStickButton.isPressed) holdCrouch = true;
            }
#else
            holdCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (toggleCrouchWithC)
            {
                if (Input.GetKeyDown(KeyCode.C)) togglePressed = true;
            }
            else
            {
                holdCrouch = holdCrouch || Input.GetKey(KeyCode.C);
            }
#endif

            if (togglePressed)
            {
                crouchToggleState = !crouchToggleState;
            }

            // Sprinting or jumping automatically breaks crouch toggle
            bool sprintHeld = false;
            bool jumpHit = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                sprintHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                jumpHit = Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#else
            sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            jumpHit = Input.GetKeyDown(KeyCode.Space);
#endif
            if (sprintHeld || jumpHit)
            {
                crouchToggleState = false;
            }

            isCrouching = holdCrouch || crouchToggleState;
        }
        else if (!IsControlled)
        {
            isCrouching = false;
            crouchToggleState = false;
        }

        // Smoothly interpolate controller height & center
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float targetCamY = isCrouching ? crouchCamY : standingCamY;

        if (controller != null)
        {
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        }

        currentCamY = Mathf.Lerp(currentCamY, targetCamY, Time.deltaTime * crouchTransitionSpeed);
        defaultCameraLocalPos = new Vector3(0f, currentCamY, 0.1f);
    }

    private void HandleMovement()
    {
        isGrounded = controller != null && controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2.0f; // Keep grounded on slopes
        }

        float moveX = 0f;
        float moveZ = 0f;
        bool isSprinting = false;
        bool jumpPressed = false;

        if (IsControlled && CanMove)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
                isSprinting = !isCrouching && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                jumpPressed = !isCrouching && Keyboard.current.spaceKey.wasPressedThisFrame;
            }
#else
            moveX = Input.GetAxisRaw("Horizontal");
            moveZ = Input.GetAxisRaw("Vertical");
            isSprinting = !isCrouching && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            jumpPressed = !isCrouching && Input.GetKeyDown(KeyCode.Space);
#endif
        }

        // Stamina & Exhaustion Logic (hasin2004-breathing-fast-247449)
        if (isExhausted)
        {
            isSprinting = false; // Cannot sprint while exhausted; must jog to recharge
        }

        if (isSprinting && (moveX != 0f || moveZ != 0f))
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isExhausted = true;
                isSprinting = false;
            }
        }
        else
        {
            if (currentStamina < maxSprintStamina)
            {
                currentStamina += staminaRecoveryRate * Time.deltaTime;
                if (isExhausted && currentStamina >= maxSprintStamina * 0.65f)
                {
                    isExhausted = false; // Recovered enough to sprint again
                }
            }
        }

        // Fast Breathing Audio for Exhaustion
        if (breathingAudioSource != null)
        {
            if (isExhausted && IsControlled)
            {
                if (!breathingAudioSource.isPlaying) breathingAudioSource.Play();
                breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, 0.85f, Time.deltaTime * 2.0f);
            }
            else
            {
                breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, 0f, Time.deltaTime * 1.5f);
                if (breathingAudioSource.volume <= 0.01f && breathingAudioSource.isPlaying)
                {
                    breathingAudioSource.Stop();
                }
            }
        }

        Vector3 moveInput = (transform.right * moveX + transform.forward * moveZ).normalized;
        float currentSpeed = walkSpeed;
        if (isCrouching) currentSpeed = crouchSpeed;
        else if (isSprinting) currentSpeed = sprintSpeed;

        isCurrentlySprinting = isSprinting && (moveX != 0f || moveZ != 0f) && isGrounded && IsControlled;
        isCurrentlyMoving = (moveX != 0f || moveZ != 0f) && isGrounded && IsControlled;

        if (controller != null && controller.enabled)
        {
            // Jump
            if (jumpPressed && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
                // Carry immediate sprinting/walking forward momentum into the jump
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    horizontalVelocity = moveInput * currentSpeed;
                }
                if (characterAnimator != null) characterAnimator.SetTrigger("Jump");
                if (jumpClip != null && movementAudioSource != null)
                {
                    movementAudioSource.pitch = 1.0f;
                    movementAudioSource.PlayOneShot(jumpClip, 0.6f);
                }
            }

            // Apply Gravity
            velocity.y += gravity * Time.deltaTime;

            // Horizontal Movement Physics: Responsive Ground Movement vs Airborne Momentum Preservation
            if (isGrounded)
            {
                // On the ground: immediate crisp directional response
                horizontalVelocity = moveInput * currentSpeed;
            }
            else
            {
                // In the air (jumping or falling):
                // If player is holding navigation keys, steer towards intended air direction
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    Vector3 targetAirVel = moveInput * currentSpeed;
                    horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetAirVel, airControlAcceleration * Time.deltaTime);
                }
                else
                {
                    // Player let go of navigation keys while airborne:
                    // DO NOT stop abruptly! Carry forward momentum with gentle realistic air drag
                    horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, airDragDeceleration * Time.deltaTime);
                }
            }

            // Single unified Move call combining horizontal momentum and vertical velocity
            Vector3 finalDisplacement = (horizontalVelocity + new Vector3(0f, velocity.y, 0f)) * Time.deltaTime;
            controller.Move(finalDisplacement);

            // Footsteps Audio - continuous audio tracks for walking/jogging and running at native pitch (1.0f)
            bool isMoving = isGrounded && isCurrentlyMoving && moveInput.sqrMagnitude > 0.01f;
            if (isMoving)
            {
                AudioClip targetTrack = isSprinting ? runningAudioTrack : walkingAudioTrack;

                // Fallback to legacy arrays if tracks aren't assigned directly
                if (targetTrack == null)
                {
                    if (isSprinting && runningFootstepClips != null && runningFootstepClips.Length > 0)
                        targetTrack = runningFootstepClips[0];
                    else if (joggingFootstepClips != null && joggingFootstepClips.Length > 0)
                        targetTrack = joggingFootstepClips[0];
                }

                if (targetTrack != null && movementAudioSource != null)
                {
                    // Strict requirement: Never speed up these audios! Always play at pitch 1.0f on respective actions
                    movementAudioSource.pitch = 1.0f;

                    float targetVol = isCrouching ? 0.16f : (isSprinting ? 0.44f : 0.28f);

                    if (movementAudioSource.clip != targetTrack)
                    {
                        movementAudioSource.clip = targetTrack;
                        movementAudioSource.loop = true;
                        movementAudioSource.volume = targetVol;
                        movementAudioSource.Play();
                    }
                    else
                    {
                        movementAudioSource.volume = Mathf.MoveTowards(movementAudioSource.volume, targetVol, Time.deltaTime * 4.0f);
                        if (!movementAudioSource.isPlaying)
                        {
                            movementAudioSource.UnPause();
                            if (!movementAudioSource.isPlaying)
                            {
                                movementAudioSource.Play();
                            }
                        }
                    }
                }
            }
            else
            {
                distanceTraveled = 0f;
                // Pause when movement stops or player is airborne (jumping/falling)
                if (movementAudioSource != null && movementAudioSource.isPlaying)
                {
                    movementAudioSource.Pause();
                }
            }

            // Landing sound
            if (isGrounded && !wasGroundedLastFrame && velocity.y < -2.5f)
            {
                if (landClip != null && movementAudioSource != null)
                {
                    movementAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                    movementAudioSource.PlayOneShot(landClip, 0.75f);
                }
            }
            wasGroundedLastFrame = isGrounded;
        }
    }

    private void EnsureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 2)
        {
            movementAudioSource = sources[0];
            breathingAudioSource = sources[1];
        }
        else if (sources.Length == 1)
        {
            movementAudioSource = sources[0];
            breathingAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
            breathingAudioSource = gameObject.AddComponent<AudioSource>();
        }

        movementAudioSource.playOnAwake = false;
        movementAudioSource.spatialBlend = 0.4f;

        breathingAudioSource.playOnAwake = false;
        breathingAudioSource.loop = true;
        breathingAudioSource.spatialBlend = 0.0f; // 2D player internal breath
        breathingAudioSource.volume = 0f;

#if UNITY_EDITOR
        if (fastBreathingClip == null)
        {
            fastBreathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Breathing_Exhausted.mp3");
            if (fastBreathingClip == null)
                fastBreathingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/hasin2004-breathing-fast-247449.mp3");
        }

        if (walkingAudioTrack == null)
        {
            walkingAudioTrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/freesound_community-concrete-footsteps-6752.mp3");
            if (walkingAudioTrack == null)
                walkingAudioTrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footsteps_Jogging.mp3");
        }

        if (runningAudioTrack == null)
        {
            runningAudioTrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/freesound_community-running-sounds-6003.mp3");
            if (runningAudioTrack == null)
                runningAudioTrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footsteps_Running.mp3");
        }

        if (footstepClips == null || footstepClips.Length == 0)
        {
            footstepClips = new AudioClip[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_01.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_02.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_03.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_04.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_05.wav"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Footstep_06.wav")
            };
        }

        if (jumpClip == null)
            jumpClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Jump.wav");

        if (landClip == null)
            landClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Player/Player_Land_Concrete.wav");
#endif

        if (fastBreathingClip != null && breathingAudioSource.clip == null)
        {
            breathingAudioSource.clip = fastBreathingClip;
        }
    }

    private void HandleLook()
    {
        if (!CanLook) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * 0.1f * mouseSensitivity;
            // Suppress spurious single-frame mouse delta jumps (e.g. on cursor lock / focus switch)
            if (delta.sqrMagnitude > 400.0f)
            {
                delta = Vector2.zero;
            }
            mouseX = delta.x;
            mouseY = delta.y;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif

        transform.Rotate(Vector3.up * mouseX);

        if (playerCamera != null)
        {
            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            playerCamera.localRotation = Quaternion.Euler(verticalRotation, currentRunYaw, currentRunRoll);
        }
    }

    private void HandleRunningSway()
    {
        if (playerCamera == null || !IsControlled)
        {
            currentRunRoll = 0f;
            currentRunYaw = 0f;
            runSwayTimer = 0f;
            return;
        }

        if (enableRunningSway && isCurrentlySprinting)
        {
            runSwayTimer += Time.deltaTime * runSwayFrequency;
            float targetRoll = Mathf.Sin(runSwayTimer) * runSwayRollAmount;
            float targetYaw = Mathf.Cos(runSwayTimer * 0.5f) * runSwayYawAmount;

            currentRunRoll = Mathf.Lerp(currentRunRoll, targetRoll, Time.deltaTime * 8.0f);
            currentRunYaw = Mathf.Lerp(currentRunYaw, targetYaw, Time.deltaTime * 8.0f);
        }
        else
        {
            runSwayTimer = 0f;
            currentRunRoll = Mathf.MoveTowards(currentRunRoll, 0f, Time.deltaTime * 6.0f);
            currentRunYaw = Mathf.MoveTowards(currentRunYaw, 0f, Time.deltaTime * 6.0f);
        }

        playerCamera.localRotation = Quaternion.Euler(verticalRotation, currentRunYaw, currentRunRoll);
    }


    private void HandleHeadBob()
    {
        if (playerCamera == null || !IsControlled) return;

        Vector3 horizontalVelocity = controller != null ? new Vector3(controller.velocity.x, 0, controller.velocity.z) : Vector3.zero;
        float speed = horizontalVelocity.magnitude;

        if (speed > 0.1f && isGrounded)
        {
            bobTimer += Time.deltaTime * headBobFrequency * (speed / walkSpeed);
            float bobOffset = Mathf.Sin(bobTimer) * headBobAmplitude;
            playerCamera.localPosition = new Vector3(
                defaultCameraLocalPos.x,
                currentCamY + bobOffset,
                defaultCameraLocalPos.z
            );
        }
        else
        {
            bobTimer = 0f;
            Vector3 targetPos = new Vector3(defaultCameraLocalPos.x, currentCamY, defaultCameraLocalPos.z);
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, targetPos, Time.deltaTime * 6f);
        }
    }

    private void HandleFallProtection()
    {
        if (controller != null && controller.isGrounded && transform.position.y >= -0.1f)
        {
            lastSafePosition = transform.position;
            hasLastSafe = true;
        }

        if (transform.position.y < killPlaneY)
        {
            Respawn();
        }
    }

    private bool isDying = false;

    private void Respawn()
    {
        Vector3 deathPos = hasLastSafe ? lastSafePosition : transform.position;
        if (FuseCellPickup.PlayerHasFuseCell)
        {
            FuseCellPickup.DropAtPosition(deathPos);
        }

        velocity = Vector3.zero;
        Vector3 target = hasLastSafe ? lastSafePosition : transform.position;
        target.y = respawnY;
        if (controller != null) controller.enabled = false;
        transform.position = target;
        if (controller != null)
        {
            controller.enabled = true;
            controller.Move(Vector3.zero);
        }
        Debug.Log($"[FirstPersonController] Player fell out of bounds - respawned at {target}");

        isDying = false;
        TriggerWakeUp();
    }

    private void UpdateAnimator()
    {
        if (characterAnimator == null) return;
        if (isDying) return;

        Vector3 horizontalVel = controller != null ? new Vector3(controller.velocity.x, 0, controller.velocity.z) : Vector3.zero;
        float speedMagnitude = horizontalVel.magnitude;

        bool isMoving = false;
        if (IsControlled)
        {
            isMoving = isCurrentlyMoving || speedMagnitude > 0.15f;
        }
        else
        {
            isMoving = speedMagnitude > 0.15f;
        }

        float animSpeed = 0f;
        if (isMoving)
        {
            if (IsControlled)
            {
                animSpeed = (isCurrentlySprinting && !isCrouching && isGrounded) ? 2.0f : 1.0f;
            }
            else
            {
                animSpeed = (speedMagnitude > (walkSpeed + 0.8f) && !isCrouching) ? 2.0f : 1.0f;
            }
        }

        bool hasTorch = (torchAbility != null && torchAbility.IsTorchOn);

        characterAnimator.SetFloat("Speed", animSpeed);
        characterAnimator.SetBool("IsCrouching", isCrouching);
        characterAnimator.SetBool("IsGrounded", isGrounded);
        characterAnimator.SetBool("HasTorch", hasTorch);
    }

    public void TriggerDeath()
    {
        isDying = true;
        if (characterAnimator != null)
        {
            characterAnimator.ResetTrigger("WakeUp");
            characterAnimator.ResetTrigger("Die");
            characterAnimator.SetTrigger("Die");
            characterAnimator.Play("Death", 0, 0f);
        }
    }

    public void TriggerWakeUp()
    {
        isDying = false;
        if (characterAnimator != null)
        {
            characterAnimator.ResetTrigger("Die");
            characterAnimator.ResetTrigger("WakeUp");
            characterAnimator.SetTrigger("WakeUp");
            characterAnimator.Play("WakeUp", 0, 0f);
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleCursorLock()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame))
        {
            if (Cursor.lockState == CursorLockMode.Locked) UnlockCursor();
            else LockCursor();
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (Cursor.lockState == CursorLockMode.Locked) UnlockCursor();
            else LockCursor();
        }
#endif

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Vector2 mousePos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) mousePos = Mouse.current.position.ReadValue();
#else
            mousePos = Input.mousePosition;
#endif
            bool overUI = mousePos.x <= 420 && (Screen.height - mousePos.y) <= 300;

#if ENABLE_INPUT_SYSTEM
            if (!overUI && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }
#else
            if (!overUI && Input.GetMouseButtonDown(0))
            {
                LockCursor();
            }
#endif
        }
    }
}
