using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Third/first person player controller driven by the new Input System.
/// Input comes from the PlayerInput component (Behavior: Send Messages), so the same code works with
/// keyboard + mouse, a gamepad and the on-screen (touch) controls. Animation logic lives in the Animator.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    public enum CameraMode { ThirdPerson, FirstPerson }

    public const string GamepadScheme = "Gamepad";

    [Header("References")]
    [SerializeField] PlayerInput playerInput;
    [SerializeField] Animator animator;
    [SerializeField] Transform cameraPivot;
    [SerializeField] Camera playerCamera;
    [Tooltip("Left empty = auto-collect all renderers under the Animator")]
    [SerializeField] Renderer[] bodyRenderers;

    [Header("Movement")]
    [SerializeField] float walkSpeed = 2.5f;
    [SerializeField] float runSpeed = 5.5f;
    [SerializeField] float crouchSpeed = 1.4f;
    [SerializeField] float backwardMultiplier = 0.7f;
    [SerializeField] float airControl = 3f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float jumpHeight = 1.2f;

    [Header("Wall Jump")]
    [SerializeField] float wallJumpUpSpeed = 6.5f;
    [SerializeField] float wallJumpPushSpeed = 5f;
    [SerializeField] float wallStickTime = 0.25f;

    [Header("Combat")]
    [SerializeField] float attackDuration = 0.7f;
    [SerializeField] float attackMoveMultiplier = 0.25f;
    [SerializeField] int punchVariants = 3;
    [SerializeField] int kickVariants = 3;

    [Header("Camera")]
    [SerializeField] CameraMode startMode = CameraMode.ThirdPerson;
    [SerializeField] Vector3 thirdPersonOffset = new Vector3(0.5f, 0.3f, -3f);
    [SerializeField] Vector3 firstPersonOffset = new Vector3(0f, 0.05f, 0.2f);
    [SerializeField] float cameraSmooth = 12f;
    [SerializeField] float mouseSensitivity = 0.12f;
    [Tooltip("Degrees per second at full stick deflection (gamepad / on-screen stick)")]
    [SerializeField] float stickLookSpeed = 160f;
    [SerializeField] float minPitch = -70f;
    [SerializeField] float maxPitch = 80f;

    [Header("Crouch Collider")]
    [SerializeField] float standHeight = 1.8f;
    [SerializeField] float crouchHeight = 1.1f;

    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int JumpHash = Animator.StringToHash("Jump");
    static readonly int WallJumpHash = Animator.StringToHash("WallJump");
    static readonly int PunchHash = Animator.StringToHash("Punch");
    static readonly int KickHash = Animator.StringToHash("Kick");
    static readonly int PunchIndexHash = Animator.StringToHash("PunchIndex");
    static readonly int KickIndexHash = Animator.StringToHash("KickIndex");

    CharacterController controller;
    CameraMode mode;
    float yaw, pitch;
    float verticalVelocity;
    Vector3 horizontalVelocity;
    bool isCrouching;
    float attackTimer;
    Vector3 wallNormal;
    float lastWallTime = -10f;

    // Values written by the Input System callbacks, consumed in Update
    Vector2 moveInput;
    Vector2 lookInput;
    bool sprintHeld;
    bool jumpQueued, punchQueued, kickQueued, crouchQueued;

    bool menuOpen;
    bool touchMode;

    public CameraMode Mode => mode;
    public Vector2 MoveInput => moveInput;
    public Vector2 LookInput => lookInput;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public bool IsRunning { get; private set; }
    public bool IsCrouching => isCrouching;
    public bool MenuOpen => menuOpen;
    public string CurrentScheme => playerInput != null ? playerInput.currentControlScheme : string.Empty;

    public event Action<CameraMode> CameraModeChanged;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = animator != null ? animator.GetComponentsInChildren<Renderer>() : new Renderer[0];
        if (animator != null) animator.applyRootMotion = false;
        yaw = transform.eulerAngles.y;
    }

    void Start()
    {
        SetCameraMode(startMode, true);
        ApplyCursorState();
    }

    // ---------------- Input callbacks (PlayerInput -> Send Messages) ----------------
    // The method name is "On" + action name. Button actions send a message on press and on release.

    public void OnMove(InputValue value) => moveInput = value.Get<Vector2>();
    public void OnLook(InputValue value) => lookInput = value.Get<Vector2>();
    public void OnSprint(InputValue value) => sprintHeld = value.isPressed;

    public void OnJump(InputValue value) { if (value.isPressed) jumpQueued = true; }
    public void OnAttack(InputValue value) { if (value.isPressed) punchQueued = true; }
    public void OnKick(InputValue value) { if (value.isPressed) kickQueued = true; }
    public void OnCrouch(InputValue value) { if (value.isPressed) crouchQueued = true; }
    public void OnSwitchCamera(InputValue value) { if (value.isPressed) ToggleCameraMode(); }
    public void OnMenu(InputValue value) { if (value.isPressed) ToggleMenu(); }

    // ---------------- Public API (UI buttons, HUD) ----------------

    /// <summary>On-screen controls need a free cursor, so the HUD tells us when touch mode is on.</summary>
    public void SetTouchMode(bool enabled)
    {
        touchMode = enabled;
        if (enabled) menuOpen = false;
        ApplyCursorState();
    }

    public void ToggleMenu()
    {
        menuOpen = !menuOpen;
        ApplyCursorState();
    }

    /// <summary>Hook this to a UI Button's OnClick to switch TPP / FPP.</summary>
    public void ToggleCameraMode()
    {
        SetCameraMode(mode == CameraMode.ThirdPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson, false);
    }

    // ---------------- Update loop ----------------

    void Update()
    {
        bool active = !menuOpen;

        if (active) HandleLook();
        HandleMovement(active);
        ClearQueuedButtons();
    }

    void LateUpdate()
    {
        UpdateCameraPosition();
    }

    void HandleLook()
    {
        Vector2 delta;
        if (CurrentScheme == GamepadScheme)
        {
            // stick = rate of rotation, so scale by time
            delta = lookInput * (stickLookSpeed * Time.deltaTime);
        }
        else if (Cursor.lockState == CursorLockMode.Locked)
        {
            // mouse = per-frame delta, so no time scaling
            delta = lookInput * mouseSensitivity;
        }
        else
        {
            return;
        }

        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement(bool active)
    {
        Vector2 input = active ? Vector2.ClampMagnitude(moveInput, 1f) : Vector2.zero;

        bool grounded = controller.isGrounded;
        bool attacking = attackTimer > 0f;
        if (attacking) attackTimer -= Time.deltaTime;

        if (active && crouchQueued && grounded) SetCrouch(!isCrouching);

        // run: hold sprint, forward only, not crouching
        bool running = active && sprintHeld && input.y > 0.1f && !isCrouching && grounded && !attacking;
        IsRunning = running;

        float speed = isCrouching ? crouchSpeed : (running ? runSpeed : walkSpeed);
        if (input.y < -0.1f) speed *= backwardMultiplier;
        if (attacking) speed *= attackMoveMultiplier;

        Vector3 wish = (transform.right * input.x + transform.forward * input.y) * speed;

        if (grounded)
        {
            horizontalVelocity = wish;
            if (verticalVelocity < 0f) verticalVelocity = -2f;
        }
        else
        {
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, wish, airControl * Time.deltaTime);
        }

        // jump / wall jump
        if (active && jumpQueued && !attacking)
        {
            if (grounded && !isCrouching)
            {
                verticalVelocity = Mathf.Sqrt(2f * -gravity * jumpHeight);
                animator.SetTrigger(JumpHash);
            }
            else if (!grounded && Time.time - lastWallTime < wallStickTime)
            {
                verticalVelocity = wallJumpUpSpeed;
                horizontalVelocity = wallNormal * wallJumpPushSpeed;
                lastWallTime = -10f;
                animator.SetTrigger(WallJumpHash);
            }
        }

        // attacks
        if (active && grounded && !isCrouching && !attacking)
        {
            if (punchQueued)
            {
                animator.SetInteger(PunchIndexHash, UnityEngine.Random.Range(0, punchVariants));
                animator.SetTrigger(PunchHash);
                attackTimer = attackDuration;
            }
            else if (kickQueued)
            {
                animator.SetInteger(KickIndexHash, UnityEngine.Random.Range(0, kickVariants));
                animator.SetTrigger(KickHash);
                attackTimer = attackDuration;
            }
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // animator
        animator.SetFloat(MoveXHash, input.x, 0.1f, Time.deltaTime);
        animator.SetFloat(MoveYHash, input.y, 0.1f, Time.deltaTime);
        animator.SetBool(IsRunningHash, running);
        animator.SetBool(IsCrouchingHash, isCrouching);
        animator.SetBool(IsGroundedHash, controller.isGrounded);
    }

    void ClearQueuedButtons()
    {
        jumpQueued = punchQueued = kickQueued = crouchQueued = false;
    }

    void SetCrouch(bool crouch)
    {
        isCrouching = crouch;
        float h = crouch ? crouchHeight : standHeight;
        controller.height = h;
        controller.center = new Vector3(0f, h * 0.5f, 0f);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!controller.isGrounded && Mathf.Abs(hit.normal.y) < 0.2f)
        {
            wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            lastWallTime = Time.time;
        }
    }

    // ---------------- Camera ----------------

    void SetCameraMode(CameraMode newMode, bool instant)
    {
        mode = newMode;
        bool fpp = mode == CameraMode.FirstPerson;
        // In FPP the body is hidden but still casts its shadow
        foreach (var r in bodyRenderers)
            if (r != null)
                r.shadowCastingMode = fpp ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                                          : UnityEngine.Rendering.ShadowCastingMode.On;
        if (instant) UpdateCameraPosition(true);
        CameraModeChanged?.Invoke(mode);
    }

    void UpdateCameraPosition(bool instant = false)
    {
        if (playerCamera == null || cameraPivot == null) return;
        Transform cam = playerCamera.transform;
        if (cam.parent != cameraPivot) cam.SetParent(cameraPivot, false);

        Vector3 target = mode == CameraMode.FirstPerson ? firstPersonOffset : thirdPersonOffset;

        if (mode == CameraMode.ThirdPerson)
        {
            // pull the camera in if something is between pivot and camera
            Vector3 origin = cameraPivot.position;
            Vector3 desired = cameraPivot.TransformPoint(target);
            Vector3 dir = desired - origin;
            float dist = dir.magnitude;
            var hits = Physics.RaycastAll(origin, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = dist;
            foreach (var h in hits)
                if (!h.transform.IsChildOf(transform) && h.distance < best) best = h.distance;
            if (best < dist) target = cameraPivot.InverseTransformPoint(origin + dir / dist * Mathf.Max(0.3f, best - 0.2f));
        }

        cam.localPosition = instant ? target : Vector3.Lerp(cam.localPosition, target, cameraSmooth * Time.deltaTime);
        cam.localRotation = Quaternion.identity;
    }

    // Mouse look needs a locked cursor; menu and touch controls need a free one.
    void ApplyCursorState()
    {
        bool locked = !menuOpen && !touchMode && !Application.isMobilePlatform;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
