using UnityEngine;

namespace HollowTower
{
    [RequireComponent(typeof(CharacterController))]
    public class RemasteredPlayerController : MonoBehaviour
    {
        [Header("Locomotion Speeds")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.5f;
        public float crouchSpeed = 2.2f;
        public float jumpHeight = 1.25f;
        public float gravity = -18.0f;

        [Header("Heights & Dimensions")]
        public float standingHeight = 2.15f;
        public float crouchHeight = 1.35f;
        public float standingCamY = 1.82f;
        public float crouchCamY = 1.15f;

        [Header("Mouse Look")]
        public float mouseSensitivity = 2.5f;
        public float maxPitch = 85.0f;
        public float minPitch = -85.0f;

        [Header("Flashlight (Key F)")]
        [Tooltip("Only P4 carries a torch in the party; the others see by the building's own lights.")]
        public bool hasTorch = true;
        public bool flashlightEnabled = true;
        public KeyCode flashlightKey = KeyCode.F;
        public Light flashlightLight;

        [Header("Camera & View Modes")]
        public Transform cameraHolder;
        public Camera playerCamera;
        public KeyCode toggleViewKey = KeyCode.V;
        public bool isThirdPerson = false;
        public float thirdPersonDistance = 2.8f;
        public float thirdPersonHeight = 1.6f;

        [Header("Model & Animation")]
        public Animator animator;
        public GameObject characterModel;

        [Header("First-Person Body")]
        [Tooltip("Draw the character's own body in first person (the head is hidden so it never blocks the camera).")]
        public bool showBodyInFirstPerson = true;
        [Tooltip("How far the first-person camera sits in front of the body's centre line.")]
        public float firstPersonForward = 0.14f;

        [Header("Party")]
        [Tooltip("Off for the teammates the local player isn't steering: they stand where they were left (see PlayerParty).")]
        public bool isControlled = true;

        // Private movement state
        private CharacterController controller;
        private Vector3 velocity;
        private float pitch = 0.0f;
        private float yaw = 0.0f;
        private bool isCrouching = false;
        private bool crouchToggled = false;
        private float currentCamY = 1.82f;
        private float bobTimer = 0.0f;
        private Transform headBone;
        private Vector3 headBoneScale = Vector3.one;
        private LightShadows torchShadows = LightShadows.Soft;
        private bool frozen;

        // Where this body has recently stood on solid floor (for putting it back after a fall into a pit)
        private readonly Vector3[] safeSpots = new Vector3[24];
        private readonly float[] safeTimes = new float[24];
        private int safeCount, safeHead;
        private float nextSafeSample;
        private Vector3 spawnPosition;

        public bool IsFrozen { get { return frozen; } }
        /// <summary>Out of breath (PlayerVitals): shift does nothing until they have their wind back.</summary>
        [System.NonSerialized] public bool sprintBlocked;
        /// <summary>Running flat out this frame (sprint held, moving, not crouched or blocked).</summary>
        public bool IsSprinting { get; private set; }
        /// <summary>Another machine steers this body (NetBody): its position comes over the network, so no gravity or
        /// animation from here.</summary>
        [System.NonSerialized] public bool remote;
        public bool IsDead { get; private set; }
        /// <summary>A dead teammate is watching through this body's eyes: hide the head like in first person.</summary>
        public bool ViewedFromInside { get; set; }

        /// <summary>
        /// This player is dead (fell into a pit). The body stays where it lies and plays its death; the player carries on
        /// as a spectator (see PlayerParty), still with their ability.
        /// </summary>
        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            frozen = true;
            velocity = Vector3.zero;
            isControlled = false;
            ViewedFromInside = false;
            gameObject.tag = "Untagged";
            if (controller != null) controller.enabled = false;
            if (flashlightLight != null) flashlightLight.enabled = false;
            flashlightEnabled = false;
            if (headBone != null) headBone.localScale = headBoneScale;
            if (animator != null)
            {
                int death = Animator.StringToHash("Death");
                if (animator.HasState(0, death)) animator.CrossFade(death, 0.1f, 0);
                else animator.SetTrigger("Die");
            }
        }

        /// <summary>Stops all movement and collision (falling into a pit); the pit script moves the body itself.</summary>
        public void SetFrozen(bool on)
        {
            frozen = on;
            velocity = Vector3.zero;
            if (controller != null) controller.enabled = !on;
        }

        /// <summary>
        /// The newest solid-floor spot this body stood on at least minAge seconds ago (checked again now: not over a pit,
        /// floor underneath), else the oldest good one, else where it started the level.
        /// </summary>
        public Vector3 SafePosition(float minAge)
        {
            Vector3? fallback = null;
            for (int k = 0; k < safeCount; k++)
            {
                int i = (safeHead - 1 - k + safeSpots.Length) % safeSpots.Length;
                if (!IsSolidSpot(safeSpots[i])) continue;
                if (Time.time - safeTimes[i] >= minAge) return safeSpots[i];
                fallback = safeSpots[i];
            }
            return fallback ?? spawnPosition;
        }

        static bool IsSolidSpot(Vector3 p)
        {
            if (PitHazards.IsOverHole(p)) return false;
            return Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore);
        }

        void RecordSafeSpot()
        {
            if (Time.time < nextSafeSample || controller == null || !controller.enabled || !controller.isGrounded) return;
            nextSafeSample = Time.time + 0.2f;
            Vector3 p = transform.position;
            if (PitHazards.IsOverHole(p)) return;
            if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.7f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.collider.GetComponentInParent<RemasteredPlayerController>() != null) return;
            // don't fill the history with copies while standing still
            if (safeCount > 0 && (safeSpots[(safeHead - 1 + safeSpots.Length) % safeSpots.Length] - p).sqrMagnitude < 0.09f)
            {
                safeTimes[(safeHead - 1 + safeSpots.Length) % safeSpots.Length] = Mathf.Min(safeTimes[(safeHead - 1 + safeSpots.Length) % safeSpots.Length], Time.time);
                return;
            }
            safeSpots[safeHead] = p;
            safeTimes[safeHead] = Time.time;
            safeHead = (safeHead + 1) % safeSpots.Length;
            safeCount = Mathf.Min(safeCount + 1, safeSpots.Length);
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraHolder == null)
            {
                var ch = transform.Find("Camera_Holder");
                if (ch != null) cameraHolder = ch;
                else
                {
                    var newCh = new GameObject("Camera_Holder");
                    newCh.transform.SetParent(transform, false);
                    newCh.transform.localPosition = new Vector3(0f, standingCamY, 0f);
                    cameraHolder = newCh.transform;
                }
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                // with a party the camera is handed over by PlayerParty, so a teammate must not grab it here
                if (playerCamera == null && Camera.main != null && FindAnyObjectByType<PlayerParty>() == null)
                {
                    playerCamera = Camera.main;
                }
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            currentCamY = standingCamY;
            yaw = transform.eulerAngles.y;
            spawnPosition = transform.position;
            if (flashlightLight != null) torchShadows = flashlightLight.shadows;

            if (characterModel != null)
            {
                foreach (var t in characterModel.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.EndsWith(":Head") || t.name == "Head") { headBone = t; headBoneScale = t.localScale; break; }
                }
                if (showBodyInFirstPerson)
                {
                    foreach (var r in characterModel.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        void Start()
        {
            // Lock and hide cursor for smooth mouse look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Ensure CharacterController dimensions match
            controller.height = standingHeight;
            controller.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            controller.radius = 0.35f;
            controller.stepOffset = 0.45f;

            // The collage game locomotion controller starts in "Unconscious" (its wake-up intro);
            // this level has no intro, so start the character standing.
            int idleHash = Animator.StringToHash("Idle");
            if (animator != null && animator.HasState(0, idleHash))
            {
                animator.Play(idleHash, 0, 0f);
            }

            if (!hasTorch) flashlightEnabled = false;
            if (flashlightLight != null)
            {
                flashlightLight.enabled = flashlightEnabled;
            }
        }

        /// <summary>Hands this body to the local player (camera, "Player" tag, torch shadows) or lets it go.</summary>
        public void SetControlled(bool controlled, Camera cam)
        {
            isControlled = controlled;
            gameObject.tag = controlled ? "Player" : "Untagged";
            if (controlled)
            {
                playerCamera = cam;
                yaw = transform.eulerAngles.y;
                if (cameraHolder != null)
                {
                    cameraHolder.localPosition = new Vector3(0f, currentCamY, isThirdPerson ? 0f : firstPersonForward);
                    if (cam != null)
                    {
                        cam.transform.SetParent(cameraHolder, false);
                        cam.transform.localPosition = isThirdPerson ? new Vector3(0.35f, 0.2f, -thirdPersonDistance) : Vector3.zero;
                        cam.transform.localRotation = Quaternion.identity;
                    }
                }
            }
            else
            {
                playerCamera = null;
                velocity.x = velocity.z = 0f;
                if (headBone != null) headBone.localScale = headBoneScale;
            }
            // only the torch in the local player's hand casts shadows; teammates' torches just light the way
            if (flashlightLight != null)
                flashlightLight.shadows = controlled ? torchShadows : LightShadows.None;
        }

        void Update()
        {
            if (frozen)
            {
                if (animator != null && !IsDead) animator.SetFloat("Speed", 0f);
                return;
            }
            if (remote) return;
            RecordSafeSpot();
            if (!isControlled)
            {
                HandleIdle();
                UpdateAnimator();
                return;
            }
            HandleCursorLock();
            HandleLook();
            HandleMovement();
            HandleFlashlight();
            HandleViewToggle();
            UpdateAnimator();
        }

        void HandleCursorLock()
        {
            if (RemasteredMapHUD.IsFullMapOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            // Escape unlocks cursor, clicking re-locks
            if (InputWrap.Down(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (InputWrap.Down(KeyCode.Mouse0) && Cursor.lockState != CursorLockMode.Locked)
            {
                // Don't re-lock if clicking inside top HUD areas
                Vector2 mPos = InputWrap.MousePosition;
                bool overTopLeft = (mPos.x < 580f && mPos.y > Screen.height - 100f);
                bool overTopRight = (mPos.x > Screen.width - 270f && mPos.y > Screen.height - 260f);
                if (!overTopLeft && !overTopRight)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mouseX = InputWrap.MouseDelta.x * mouseSensitivity;
            float mouseY = InputWrap.MouseDelta.y * mouseSensitivity;

            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Rotate character body horizontally
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Rotate camera holder vertically
            if (cameraHolder != null)
            {
                if (!isThirdPerson)
                {
                    cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                    cameraHolder.localPosition = new Vector3(0f, currentCamY, firstPersonForward);
                    if (playerCamera != null && playerCamera.transform.parent != cameraHolder)
                    {
                        playerCamera.transform.SetParent(cameraHolder, false);
                        playerCamera.transform.localPosition = Vector3.zero;
                        playerCamera.transform.localRotation = Quaternion.identity;
                    }
                }
                else
                {
                    // Third person orbit
                    cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                    cameraHolder.localPosition = new Vector3(0f, thirdPersonHeight, 0f);
                    if (playerCamera != null)
                    {
                        if (playerCamera.transform.parent != cameraHolder)
                        {
                            playerCamera.transform.SetParent(cameraHolder, false);
                        }
                        Vector3 desiredPos = new Vector3(0.35f, 0.2f, -thirdPersonDistance);
                        playerCamera.transform.localPosition = desiredPos;
                        playerCamera.transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        void HandleMovement()
        {
            // Crouch input (C toggle or Left Ctrl hold)
            if (InputWrap.Down(KeyCode.C))
            {
                crouchToggled = !crouchToggled;
            }
            bool holdCrouch = InputWrap.Held(KeyCode.LeftControl) || InputWrap.Held(KeyCode.RightControl);
            bool sprint = (InputWrap.Held(KeyCode.LeftShift) || InputWrap.Held(KeyCode.RightShift)) && !sprintBlocked;

            if (sprint) crouchToggled = false; // Sprinting breaks crouch
            isCrouching = (holdCrouch || crouchToggled) && controller.isGrounded;

            // Interpolate character controller height and camera position
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            float targetCamY = isCrouching ? crouchCamY : standingCamY;

            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * 10f);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            currentCamY = Mathf.Lerp(currentCamY, targetCamY, Time.deltaTime * 10f);

            // WASD Input
            float h = InputWrap.Move.x;
            float v = InputWrap.Move.y;
            Vector3 inputDir = new Vector3(h, 0f, v).normalized;

            float currentSpeed = walkSpeed;
            if (isCrouching) currentSpeed = crouchSpeed;
            else if (sprint && inputDir.magnitude > 0.1f) currentSpeed = sprintSpeed;
            IsSprinting = !isCrouching && sprint && inputDir.magnitude > 0.1f;

            Vector3 move = Vector3.zero;
            if (inputDir.magnitude > 0.1f)
            {
                move = (transform.forward * inputDir.z + transform.right * inputDir.x) * currentSpeed;

                // Subtle head bob
                if (controller.isGrounded && !isThirdPerson)
                {
                    float bobFreq = sprint ? 12f : 8f;
                    float bobAmp = sprint ? 0.04f : 0.02f;
                    bobTimer += Time.deltaTime * bobFreq;
                    currentCamY += Mathf.Sin(bobTimer) * bobAmp * Time.deltaTime * 60f;
                }
            }

            // Gravity & Jumping
            if (controller.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -2.0f;

                if (InputWrap.Down(KeyCode.Space))
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
                    crouchToggled = false;
                    if (animator != null) animator.SetTrigger("Jump");
                }
            }
            else
            {
                velocity.y += gravity * Time.deltaTime;
            }

            // Apply movement to CharacterController
            controller.Move((move + velocity) * Time.deltaTime);
        }

        /// <summary>A teammate nobody is steering: no input, just gravity so it stays on the floor.</summary>
        void HandleIdle()
        {
            if (!controller.enabled) return;
            if (controller.isGrounded && velocity.y < 0f) velocity.y = -2.0f;
            else velocity.y += gravity * Time.deltaTime;
            controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
        }

        void HandleFlashlight()
        {
            if (hasTorch && InputWrap.Down(flashlightKey))
            {
                flashlightEnabled = !flashlightEnabled;
                if (SfxBank.Instance != null) SfxBank.At(flashlightEnabled ? SfxBank.Instance.torchOn : SfxBank.Instance.torchOff, transform.position + Vector3.up * 1.5f, 0.8f, 1f, 8f);
                if (flashlightLight != null)
                {
                    flashlightLight.enabled = flashlightEnabled;
                }
            }
        }

        void LateUpdate()
        {
            // Runs after the Animator: shrink the head in first person so the camera never sees inside it.
            if (headBone != null)
                headBone.localScale = ((!isControlled && !ViewedFromInside) || isThirdPerson || !showBodyInFirstPerson) ? headBoneScale : headBoneScale * 0.001f;
        }

        void HandleViewToggle()
        {
            if (InputWrap.Down(toggleViewKey))
            {
                isThirdPerson = !isThirdPerson;
                if (!isThirdPerson && playerCamera != null)
                {
                    playerCamera.transform.localPosition = Vector3.zero;
                    playerCamera.transform.localRotation = Quaternion.identity;
                }
            }
        }

        void UpdateAnimator()
        {
            if (animator == null) return;

            Vector3 horizontalVel = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
            float speed = horizontalVel.magnitude;

            animator.SetFloat("Speed", speed);
            animator.SetBool("IsGrounded", controller.isGrounded);
            animator.SetBool("IsCrouching", isCrouching);
            animator.SetBool("HasTorch", hasTorch && flashlightEnabled);
        }
    }
}
