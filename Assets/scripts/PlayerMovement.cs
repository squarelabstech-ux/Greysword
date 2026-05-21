using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerMovement — Handles WASD/joystick movement with run/jump.
/// Modified from original to respect player death state.
///
/// SETUP:
/// - Attach to Player root. Requires CharacterController + PlayerInput.
/// - REMOVE the Rigidbody from the Player — it conflicts with CharacterController.
/// - Assign cameraTransform (Main Camera transform) in Inspector.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public CharacterController controller;
    public Transform cameraTransform;
    public Animator animator;

    public float walkSpeed      = 3f;
    public float runSpeed       = 6f;
    public float gravity        = -9.81f;
    public float jumpHeight     = 1.5f;
    public float turnSmoothTime = 0.1f;

    // ─── Input Actions ─────────────────────────────────────────────────────────

    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction attackAction;

    // ─── Private State ─────────────────────────────────────────────────────────

    private Vector3 velocity;
    private float   turnSmoothVelocity;
    private bool    isDead = false;

    // Animator param hashes
    private static readonly int HASH_IsWalking = Animator.StringToHash("isWalking");
    private static readonly int HASH_IsRunning = Animator.StringToHash("isRunning");
    private static readonly int HASH_IsJumping = Animator.StringToHash("isJumping");
    private static readonly int HASH_HasWeapon = Animator.StringToHash("hasWeapon");

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Q2 Answer: Auto-remove Rigidbody — it conflicts with CharacterController
        // and causes floating/jitter. Done in code so no manual Inspector step is needed.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log("[PlayerMovement] Removing Rigidbody — CharacterController handles physics.");
            Destroy(rb);
        }
    }

    void Start()
    {
        // Auto-assign references if not set in Inspector
        if (controller == null)
            controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Disable root motion so CharacterController handles movement
        if (animator != null)
            animator.applyRootMotion = false;

        // Fix floating: ensure CharacterController center is grounded
        if (controller != null)
        {
            controller.center = new Vector3(0f, controller.height / 2f, 0f);
            controller.skinWidth = 0.08f;
        }

        var playerInput = GetComponent<PlayerInput>();

        moveAction   = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction   = playerInput.actions["Jump"];

        // Attack action (optional — only if binding exists)
        try { attackAction = playerInput.actions["Attack"]; } catch { }

        moveAction.Enable();
        sprintAction.Enable();
        jumpAction.Enable();
        attackAction?.Enable();

        // Warp player to the home campfire (X=659.9 + 6, Y=1.6, Z=680.56 + 6) on start/restart
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = new Vector3(659.9f + 6f, 1.6f, 680.56f + 6f);
            controller.enabled = true;
            Debug.Log("[PlayerMovement] Warped player to home campfire (offset by +6 units X/Z) on start.");
        }

        // Listen for player death
        PlayerHealth.OnPlayerDeath += OnDied;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDeath -= OnDied;
    }

    void OnDied()
    {
        isDead = true;
        velocity = Vector3.zero;

        if (animator != null)
        {
            animator.SetBool(HASH_IsWalking, false);
            animator.SetBool(HASH_IsRunning, false);
        }
    }

    void Update()
    {
        if (isDead) return;
        if (!GameUIManager.IsGamePlaying || GameUIManager.IsGamePaused) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (MobileControls.Instance != null && MobileControls.Instance.isActiveAndEnabled && MobileControls.Instance.MoveInput != Vector2.zero)
        {
            input = MobileControls.Instance.MoveInput;
        }

        Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

        PlayerCombat combat = GetComponent<PlayerCombat>();
        bool isAttacking = combat != null && combat.isAttacking;

        if (isAttacking)
        {
            // Stop movement to prevent sliding
            direction = Vector3.zero;
        }

        bool isMoving  = direction.magnitude >= 0.1f;
        
        bool isRunning = sprintAction.IsPressed() && !isAttacking;
        if (MobileControls.Instance != null && MobileControls.Instance.isActiveAndEnabled)
        {
            isRunning = (sprintAction.IsPressed() || MobileControls.Instance.SprintInput) && !isAttacking;
        }

        if (isMoving)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg
                                + cameraTransform.eulerAngles.y;

            float angle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref turnSmoothVelocity,
                turnSmoothTime
            );

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            float   speed   = isRunning ? runSpeed : walkSpeed;
            controller.Move(moveDir.normalized * speed * Time.deltaTime);
        }

        // Grounded check
        bool isGrounded = controller.isGrounded || IsNearGround();

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
            if (animator != null) animator.SetBool(HASH_IsJumping, false);
        }

        bool wantsJump = jumpAction.WasPressedThisFrame();
        if (MobileControls.Instance != null && MobileControls.Instance.isActiveAndEnabled)
        {
            wantsJump = wantsJump || MobileControls.Instance.JumpInput;
        }

        if (wantsJump && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetBool(HASH_IsJumping, true);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Sync animator using properly configured Animator Controller
        if (animator != null)
        {
            animator.SetBool(HASH_IsWalking, isMoving && !isRunning);
            animator.SetBool(HASH_IsRunning, isMoving && isRunning);
            if (combat != null) animator.SetBool(HASH_HasWeapon, combat.hasWeapon);
        }
    }

    bool IsNearGround()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.3f);
    }
}