using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airMultiplier = 0.4f;
    [SerializeField] private float rotationSmoothTime = 0.1f;

    [Header("Gravity & Fall Settings")]
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private float maxFallSpeed = 25f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float fastFallMultiplier = 3.5f;
    [SerializeField] private bool enableFastFall = true;
    [SerializeField] private float jumpCutMultiplier = 2f;

    [Header("Jump Buffer")]
    [SerializeField] private float coyoteTime = 0.1f;
    private float coyoteTimer;

    [Header("Double Jump")]
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float flipRotationSpeed = 1440f; // 1440 graus/segundo = 4 voltas/segundo
    private int currentJumps;
    private bool isDoubleJumping;
    private float flipAccumulatedAngle;
    private bool hasCompletedFlip; // Para controlar se já completou o giro

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private float cameraHeight = 2f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraSmoothTime = 0.1f;

    [Header("Camera Collision")]
    [SerializeField] private LayerMask collisionMask = -1;
    [SerializeField] private float cameraCollisionRadius = 0.2f;
    [SerializeField] private float cameraMinDistance = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private float groundCheckRadius = 0.3f;

    // Input System
    private PlayerControls playerControls;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isJumping;
    private bool isSprinting;
    private bool isJumpCut;

    // Components
    private Rigidbody rb;
    private Vector3 moveDirection;
    private float verticalRotation = 0f;
    private float horizontalRotation = 0f;
    private bool isGrounded;

    // Camera variables
    private Vector3 cameraVelocity;
    private float currentCameraDistance;

    private void Awake()
    {
        playerControls = new PlayerControls();
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        CreateNoFrictionMaterial();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentCameraDistance = cameraDistance;
    }

    private void CreateNoFrictionMaterial()
    {
        PhysicsMaterial noFriction = new PhysicsMaterial("NoFriction");
        noFriction.dynamicFriction = 0f;
        noFriction.staticFriction = 0f;
        noFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
        noFriction.bounciness = 0f;
        noFriction.bounceCombine = PhysicsMaterialCombine.Minimum;

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.material = noFriction;
        else
            Debug.LogWarning("Nenhum collider encontrado para aplicar o material sem atrito.");
    }

    private void OnEnable()
    {
        playerControls.Enable();
        playerControls.Movement.Move.performed += OnMovePerformed;
        playerControls.Movement.Move.canceled += OnMoveCanceled;
        playerControls.Movement.Jump.performed += OnJumpPerformed;
        playerControls.Movement.Jump.canceled += OnJumpCanceled;
        playerControls.Movement.Sprint.performed += OnSprintPerformed;
        playerControls.Movement.Sprint.canceled += OnSprintCanceled;
        playerControls.Movement.Look.performed += OnLookPerformed;
        playerControls.Movement.Look.canceled += OnLookCanceled;
    }

    private void OnDisable()
    {
        playerControls.Disable();
        playerControls.Movement.Move.performed -= OnMovePerformed;
        playerControls.Movement.Move.canceled -= OnMoveCanceled;
        playerControls.Movement.Jump.performed -= OnJumpPerformed;
        playerControls.Movement.Jump.canceled -= OnJumpCanceled;
        playerControls.Movement.Sprint.performed -= OnSprintPerformed;
        playerControls.Movement.Sprint.canceled -= OnSprintCanceled;
        playerControls.Movement.Look.performed -= OnLookPerformed;
        playerControls.Movement.Look.canceled -= OnLookCanceled;
    }

    private void Start()
    {
        currentJumps = maxJumps;
    }

    private void Update()
    {
        HandleCameraRotation();
        HandleCameraCollision();
        CheckGround();
        HandleDrag();
        HandleCoyoteTime();
        UpdateCameraPivotPosition();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
        ApplyGravity();
        ApplyFlip(); // Flip aplicado no FixedUpdate
    }

    private void LateUpdate()
    {
        UpdateCameraPosition();
    }

    private void HandleMovement()
    {
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 forward = cameraPivot.forward;
        Vector3 right = cameraPivot.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        moveDirection = forward * moveInput.y + right * moveInput.x;
        moveDirection.Normalize();

        Vector3 targetVelocity = moveDirection * currentSpeed;
        if (isGrounded)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 velocityChange = targetVelocity - horizontalVelocity;
            rb.AddForce(velocityChange * 10f, ForceMode.Force);
        }
        else
        {
            rb.AddForce(targetVelocity * 10f * airMultiplier, ForceMode.Force);
        }

        LimitVelocity(currentSpeed);

        // ⭐ Rotação: só vira o personagem se NÃO estiver no meio de um flip
        if (moveDirection != Vector3.zero && !isDoubleJumping)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                Time.fixedDeltaTime / rotationSmoothTime);
        }
    }

    private void LimitVelocity(float speed)
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (horizontalVelocity.magnitude > speed)
        {
            Vector3 limitedVelocity = horizontalVelocity.normalized * speed;
            rb.linearVelocity = new Vector3(limitedVelocity.x, rb.linearVelocity.y, limitedVelocity.z);
        }
    }

    private void HandleJump()
    {
        if (isJumping && currentJumps > 0)
        {
            bool isDoubleJump = !isGrounded && currentJumps < maxJumps;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            currentJumps--;

            if (isDoubleJump)
            {
                isDoubleJumping = true;
                hasCompletedFlip = false;
                flipAccumulatedAngle = 0f;
            }

            isJumping = false;
            isJumpCut = false;
            coyoteTimer = 0;
        }

        if (isJumpCut && !isGrounded && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f, rb.linearVelocity.z);
            isJumpCut = false;
        }
    }

    private void ApplyFlip()
    {
        if (!isGrounded && isDoubleJumping && !hasCompletedFlip)
        {
            // ⭐ Rotação em torno do eixo X local (mortal para frente)
            float angle = flipRotationSpeed * Time.fixedDeltaTime;
            transform.Rotate(Vector3.right, angle, Space.Self);
            flipAccumulatedAngle += angle;

            // Se já girou mais de 360°, marca como completo
            if (flipAccumulatedAngle >= 360f)
            {
                hasCompletedFlip = true;
                // Ajusta a rotação para múltiplos de 360 (evita desalinhamento)
                Vector3 euler = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(euler.x, euler.y, euler.z);
            }
        }
    }

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            bool isPressingDown = moveInput.y < -0.5f;
            float multiplier = gravityScale;

            if (rb.linearVelocity.y < 0)
            {
                multiplier = gravityScale * fallMultiplier;
                if (enableFastFall && isPressingDown)
                    multiplier = gravityScale * fastFallMultiplier;
            }
            else if (rb.linearVelocity.y > 0 && isJumpCut)
            {
                multiplier = gravityScale * jumpCutMultiplier;
            }

            float gravityForce = Physics.gravity.y * multiplier * rb.mass;
            rb.AddForce(Vector3.down * Mathf.Abs(gravityForce), ForceMode.Force);

            if (rb.linearVelocity.y < -maxFallSpeed)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -maxFallSpeed, rb.linearVelocity.z);
        }
    }

    private void HandleDrag()
    {
        rb.linearDamping = isGrounded ? groundDrag : 0f;
    }

    private void HandleCoyoteTime()
    {
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;
    }

    private void UpdateCameraPivotPosition()
    {
        if (cameraPivot != null)
            cameraPivot.position = transform.position + Vector3.up * cameraHeight;
    }

    private void HandleCameraRotation()
    {
        horizontalRotation += lookInput.x * mouseSensitivity;
        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
        if (cameraPivot != null)
            cameraPivot.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f);
    }

    private void HandleCameraCollision()
    {
        Vector3 desiredPosition = cameraPivot.position - cameraPivot.forward * cameraDistance;
        Vector3 direction = (desiredPosition - cameraPivot.position).normalized;
        float distance = cameraDistance;
        if (Physics.SphereCast(cameraPivot.position, cameraCollisionRadius, direction,
            out RaycastHit hit, cameraDistance, collisionMask))
        {
            distance = Mathf.Max(hit.distance - cameraCollisionRadius, cameraMinDistance);
        }
        currentCameraDistance = Mathf.Lerp(currentCameraDistance, distance, Time.deltaTime * 15f);
    }

    private void UpdateCameraPosition()
    {
        if (cameraTransform == null || cameraPivot == null) return;
        Vector3 targetPosition = cameraPivot.position - cameraPivot.forward * currentCameraDistance;
        cameraTransform.position = Vector3.SmoothDamp(cameraTransform.position, targetPosition,
            ref cameraVelocity, cameraSmoothTime);
        cameraTransform.LookAt(cameraPivot);
    }

    private void CheckGround()
    {
        Vector3 spherePosition = transform.position - Vector3.up * 0.1f;
        isGrounded = Physics.SphereCast(spherePosition, groundCheckRadius, Vector3.down,
            out RaycastHit hit, groundCheckDistance, groundMask);

        if (isGrounded)
        {
            isJumpCut = false;
            currentJumps = maxJumps;

            // Se estava no meio de um flip, finaliza a rotação
            if (isDoubleJumping && !hasCompletedFlip)
            {
                // Completa o giro restante para 360°
                float remainingAngle = 360f - flipAccumulatedAngle;
                transform.Rotate(Vector3.right, remainingAngle, Space.Self);
                hasCompletedFlip = true;
            }

            isDoubleJumping = false;

            // Alinha a rotação para evitar desalinhamento
            Vector3 currentEuler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 spherePosition = transform.position - Vector3.up * 0.1f;
        Gizmos.DrawWireSphere(spherePosition - Vector3.up * groundCheckDistance, groundCheckRadius);
        if (cameraPivot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(cameraPivot.position, cameraCollisionRadius);
            Gizmos.DrawLine(cameraPivot.position, cameraPivot.position - cameraPivot.forward * cameraDistance);
        }
    }

    // Input Handlers
    private void OnMovePerformed(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext context) => moveInput = Vector2.zero;

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (isGrounded || coyoteTimer > 0 || currentJumps > 0)
        {
            isJumping = true;
            isJumpCut = false;
            coyoteTimer = 0;
        }
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        if (!isGrounded)
            isJumpCut = true;
        isJumping = false;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context) => isSprinting = true;
    private void OnSprintCanceled(InputAction.CallbackContext context) => isSprinting = false;
    private void OnLookPerformed(InputAction.CallbackContext context) => lookInput = context.ReadValue<Vector2>();
    private void OnLookCanceled(InputAction.CallbackContext context) => lookInput = Vector2.zero;
}