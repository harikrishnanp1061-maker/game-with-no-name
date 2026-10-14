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
    public float jumpHeight = 1.2f;
    public float gravity = -18.0f;

    [Header("Look Settings")]
    public float mouseSensitivity = 2.0f;
    public float maxLookAngle = 85.0f;

    [Header("Camera & Head Bob")]
    public Transform playerCamera;
    public float headBobFrequency = 1.6f;
    public float headBobAmplitude = 0.05f;

    private CharacterController controller;
    private Vector3 velocity;
    private float verticalRotation = 0.0f;
    private Vector3 defaultCameraLocalPos;
    private float bobTimer = 0.0f;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();

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
                playerCamera.SetParent(transform);
                playerCamera.localPosition = new Vector3(0, 1.7f, 0.15f);
                playerCamera.localRotation = Quaternion.identity;
            }
        }

        if (playerCamera != null)
        {
            defaultCameraLocalPos = playerCamera.localPosition;
        }

        // Lock cursor for FPS controls
        LockCursor();
    }

    void Update()
    {
        HandleCursorLock();
        HandleLook();
        HandleMovement();
        HandleHeadBob();
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
        // Press Escape to release mouse cursor
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }
#endif

        // Click into the screen to re-lock
        if (Cursor.lockState != CursorLockMode.Locked)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                LockCursor();
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                LockCursor();
            }
#endif
        }
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * 0.1f * mouseSensitivity;
            mouseX = delta.x;
            mouseY = delta.y;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif

        // Rotate body horizontally
        transform.Rotate(Vector3.up * mouseX);

        // Tilt camera vertically
        if (playerCamera != null)
        {
            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2.0f; // Small downward force to stay glued to ground/slopes
        }

        float moveX = 0f;
        float moveZ = 0f;
        bool isSprinting = false;
        bool jumpPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            isSprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#else
        moveX = Input.GetAxisRaw("Horizontal");
        moveZ = Input.GetAxisRaw("Vertical");
        isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jumpPressed = Input.GetKeyDown(KeyCode.Space);
#endif

        Vector3 moveInput = (transform.right * moveX + transform.forward * moveZ).normalized;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        controller.Move(moveInput * currentSpeed * Time.deltaTime);

        // Jump
        if (jumpPressed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        // Apply Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleHeadBob()
    {
        if (playerCamera == null) return;

        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);
        float speed = horizontalVelocity.magnitude;

        if (speed > 0.1f && isGrounded)
        {
            bobTimer += Time.deltaTime * headBobFrequency * (speed / walkSpeed);
            float bobOffset = Mathf.Sin(bobTimer) * headBobAmplitude;
            playerCamera.localPosition = new Vector3(
                defaultCameraLocalPos.x,
                defaultCameraLocalPos.y + bobOffset,
                defaultCameraLocalPos.z
            );
        }
        else
        {
            bobTimer = 0f;
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, defaultCameraLocalPos, Time.deltaTime * 6f);
        }
    }
}
