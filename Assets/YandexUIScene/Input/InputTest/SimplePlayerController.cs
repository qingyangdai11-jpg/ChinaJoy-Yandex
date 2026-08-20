using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerIdentity))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float turnSpeed = 15f;

    private float speedBoostTimer = 0f;
    private float boostMultiplier = 1.5f;
    private float slowDownTimer = 0f;
    private float slowDownMultiplier = 0.5f;

    public float CurrentSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;
            if (speedBoostTimer > 0f)
            {
                multiplier *= boostMultiplier;
            }
            if (slowDownTimer > 0f)
            {
                multiplier *= slowDownMultiplier;
            }
            return multiplier;
        }
    }

    [Header("Lane Runner Settings")]
    public float laneWidth = 2f;
    public float laneChangeSpeed = 15f;
    
    private int targetLane = 1; // 0 = Left, 1 = Middle, 2 = Right
    private bool isHoldingLeft = false;
    private bool isHoldingRight = false;

    [Header("Jump Settings")]
    public float jumpForce = 6f;

    [Header("Slowdown Settings")]
    public float barrierSlowdownDuration = 0.5f;
    public float barrierSlowdownMultiplier = 0.5f;

    [Header("Animation Parameter Names")]
    public string speedParameterName = "Movement"; // Default parameter name in this project's Animator controller

    private Rigidbody rb;
    private Animator animator;

    // Input Actions (could be from PlayerInput component or created manually)
    private InputAction moveAction;
    private InputAction jumpAction;

    private PlayerInput playerInput;
    private bool usePlayerInputComponent;
    private Vector2 moveInput;
    private bool isJumpActionManual = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>(); // Look for Animator in child objects like Character_Warrior

        // Ensure Rigidbody is configured correctly for a character controller
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Try to get existing PlayerInput component to avoid capturing conflicts
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions.FindAction("Movement");
            jumpAction = playerInput.actions.FindAction("Jump");
 
            if (moveAction != null)
            {
                usePlayerInputComponent = true;
                Debug.Log("SimplePlayerController: Successfully linked to PlayerInput component actions.");
            }
        }
 
        if (!usePlayerInputComponent)
        {
            Debug.Log("SimplePlayerController: No PlayerInput component found or actions missing. Using manual C# bindings.");
 
            // Configure Movement Input Action (Vector 2 value type)
            moveAction = new InputAction("Move", type: InputActionType.Value, expectedControlType: "Vector2");
            
            // Add Gamepad left stick and D-Pad bindings
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddBinding("<Gamepad>/dpad");
            moveAction.AddBinding("<Joystick>/stick"); // Fallback for generic controllers
 
            // Add Keyboard WASD composite binding
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
 
            // Add Keyboard Arrow keys composite binding
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
 
        }
 
        // Setup manual jump action if not linked from PlayerInput component
        if (jumpAction == null)
        {
            jumpAction = new InputAction("Jump", type: InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth"); // Gamepad A Button
            isJumpActionManual = true;
        }

        // Register callbacks
        if (jumpAction != null) jumpAction.performed += OnJumpPerformed;
    }

    private void OnEnable()
    {
        // Manual actions must be enabled to receive input
        if (!usePlayerInputComponent)
        {
            if (moveAction != null) moveAction.Enable();
            //if (attackAction != null) attackAction.Enable();
        }
        if (isJumpActionManual && jumpAction != null)
        {
            jumpAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (!usePlayerInputComponent)
        {
            if (moveAction != null) moveAction.Disable();
        }
        if (isJumpActionManual && jumpAction != null)
        {
            jumpAction.Disable();
        }
        if (jumpAction != null) jumpAction.performed -= OnJumpPerformed;
    }

    private void Update()
    {
        float horizontalInput = 0f;

        // Read movement input in Update if in gameplay and not in UI selection state
        if ((SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused) ||
            (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay))
        {
            moveInput = Vector2.zero;
        }
        else
        {
            moveInput = moveAction.ReadValue<Vector2>();
            horizontalInput = moveInput.x;
        }

        // Discrete lane switching based on horizontal input trigger
        if (horizontalInput < -0.5f)
        {
            if (!isHoldingLeft)
            {
                if (targetLane > 0) targetLane--;
                isHoldingLeft = true;
            }
        }
        else
        {
            isHoldingLeft = false;
        }

        if (horizontalInput > 0.5f)
        {
            if (!isHoldingRight)
            {
                if (targetLane < 2) targetLane++;
                isHoldingRight = true;
            }
        }
        else
        {
            isHoldingRight = false;
        }

        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
        }
        if (slowDownTimer > 0f)
        {
            slowDownTimer -= Time.deltaTime;
        }

        // Feed movement speed to Animator if available
        if (animator != null)
        {
            bool isGameplay = (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState == GameFlowController.GameState.Gameplay);
            bool isPaused = (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused);
            float speedForAnim = (isGameplay && !isPaused) ? 1.0f : 0f;
            animator.SetFloat(speedParameterName, speedForAnim);
        }

        //// Debug Log to check active inputs in Console
        //if (horizontalInput != 0f)
        //{
        //    Debug.Log($"SimplePlayerController: Lane switch input detected. Target Lane: {targetLane}");
        //}
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (GameFlowController.Instance == null || GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay)
        {
            return;
        }

        // 1. Calculate forward speed (Z-axis)
        float currentSpeed = moveSpeed;
        if (speedBoostTimer > 0f)
        {
            currentSpeed *= boostMultiplier;
        }
        if (slowDownTimer > 0f)
        {
            currentSpeed *= slowDownMultiplier;
        }
        float newZ = rb.position.z + currentSpeed * Time.fixedDeltaTime;

        // 2. Calculate horizontal position (X-axis)
        float targetX = (targetLane - 1) * laneWidth;
        float newX = Mathf.Lerp(rb.position.x, targetX, laneChangeSpeed * Time.fixedDeltaTime);

        // 3. Update Rigidbody Position
        Vector3 targetPosition = new Vector3(newX, rb.position.y, newZ);
        rb.MovePosition(targetPosition);

        // 4. Calculate rotation (face forward, tilt slightly during lane change)
        float deltaX = targetX - rb.position.x;
        Vector3 movementDir;
        if (Mathf.Abs(deltaX) > 0.05f)
        {
            // Tilt towards movement direction
            movementDir = new Vector3(deltaX * 5f, 0f, currentSpeed).normalized;
        }
        else
        {
            // Straight forward
            movementDir = Vector3.forward;
        }
        Quaternion targetRotation = Quaternion.LookRotation(movementDir);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
    }

    public void ApplySpeedBoost(float duration, float multiplier = 1.5f)
    {
        speedBoostTimer = duration;
        boostMultiplier = multiplier;
        if (GameMechanicsManager.Instance != null && GameMechanicsManager.Instance.logSpeedChanges)
        {
            Debug.Log($"SimplePlayerController: Speed Boost applied for {duration} seconds with {multiplier}x speed!");
        }
    }

    public void ResetPosition()
    {
        targetLane = 1; // Center lane
        isHoldingLeft = false;
        isHoldingRight = false;
        slowDownTimer = 0f;
        
        if (rb != null)
        {
            rb.position = new Vector3(0f, rb.position.y, 0f);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        transform.position = new Vector3(0f, transform.position.y, 0f);
        transform.rotation = Quaternion.LookRotation(Vector3.forward);
        Debug.Log("[SimplePlayerController] Position reset to center lane at Z=0.");
    }

    public void ApplySlowDown(float duration, float multiplier = 0.5f)
    {
        slowDownTimer = duration;
        slowDownMultiplier = multiplier;
        if (GameMechanicsManager.Instance != null && GameMechanicsManager.Instance.logSpeedChanges)
        {
            Debug.Log($"SimplePlayerController: Slow Down applied for {duration} seconds with {multiplier}x speed!");
        }
    }



    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if ((SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused) ||
            (Time.frameCount <= SimpleUIManager.LastResumeFrame + 2 || Time.unscaledTime - SimpleUIManager.LastResumeTime < 0.25f) ||
            (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay))
        {
            return;
        }

        Jump();
    }

    private void Jump()
    {
        if (CheckGrounded())
        {
            // Apply upward force
            rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); // Clear vertical velocity first
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            Debug.Log("[SimplePlayerController] Player jumped!");

            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }
    }

    private bool CheckGrounded()
    {
        // 1. Vertical velocity check
        if (Mathf.Abs(rb.velocity.y) > 0.1f) return false;

        // 2. Short raycast down underneath the player
        Collider col = GetComponent<Collider>();
        float extentsY = 0.5f;
        if (col != null) extentsY = col.bounds.extents.y;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float dist = extentsY + 0.15f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, dist);
        foreach (var hit in hits)
        {
            if (hit.collider != col)
            {
                return true;
            }
        }
        return false;
    }
}
