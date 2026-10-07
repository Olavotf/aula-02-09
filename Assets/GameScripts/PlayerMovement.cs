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

    [Header("Fall Damage")]
    [SerializeField] private float maxHealth = 100f;

    // Altura mínima para começar a tomar dano
    [SerializeField] private float minimumFallHeight = 4f;

    // Altura que mata instantaneamente
    [SerializeField] private float fatalFallHeight = 15f;

    // Dano por metro depois da altura mínima
    [SerializeField] private float fallDamagePerMeter = 10f;

    private float currentHealth;

    // Controle da queda
    private float fallStartHeight;
    private bool trackingFall;

    [Header("Jump Buffer")]
    [SerializeField] private float coyoteTime = 0.1f;
    private float coyoteTimer;

    [Header("Double Jump")]
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float flipRotationSpeed = 1440f;

    private int currentJumps;
    private bool isDoubleJumping;
    private float flipAccumulatedAngle;
    private bool hasCompletedFlip;

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
    private bool isDead;

    // Camera
    private Vector3 cameraVelocity;
    private float currentCameraDistance;

    private void Awake()
    {
        playerControls = new PlayerControls();

        rb = GetComponent<Rigidbody>();

        // O script controla a rotação do player
        rb.freezeRotation = true;

        CreateNoFrictionMaterial();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentCameraDistance = cameraDistance;
    }

    private void Start()
    {
        currentJumps = maxJumps;
        currentHealth = maxHealth;
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

    private void Update()
    {
        if (isDead)
            return;

        HandleCameraRotation();
        HandleCameraCollision();

        CheckGround();

        HandleDrag();
        HandleCoyoteTime();

        UpdateCameraPivotPosition();

        HandleFallTracking();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        HandleMovement();
        HandleJump();
        ApplyGravity();
        ApplyFlip();
    }

    private void LateUpdate()
    {
        if (cameraTransform == null || cameraPivot == null)
            return;

        UpdateCameraPosition();
    }

    // =========================================================
    // FALL DAMAGE
    // =========================================================

    private void HandleFallTracking()
    {
        if (isGrounded)
        {
            // Se estava caindo e acabou de tocar no chão
            if (trackingFall)
            {
                float fallDistance =
                    fallStartHeight - transform.position.y;

                ApplyFallDamage(fallDistance);

                trackingFall = false;
            }

            return;
        }

        // Está no ar
        if (rb.linearVelocity.y < 0f)
        {
            // Começou a descer
            if (!trackingFall)
            {
                trackingFall = true;

                // Guarda a altura onde começou a queda
                fallStartHeight = transform.position.y;
            }
        }
    }

    private void ApplyFallDamage(float fallDistance)
    {
        // Segurança contra valores negativos
        if (fallDistance <= 0f)
            return;

        // Queda pequena não causa dano
        if (fallDistance < minimumFallHeight)
        {
            Debug.Log(
                "Queda pequena: " +
                fallDistance +
                "m - sem dano."
            );

            return;
        }

        // Queda fatal
        if (fallDistance >= fatalFallHeight)
        {
            Debug.Log(
                "Queda fatal: " +
                fallDistance +
                "m"
            );

            Die();

            return;
        }

        // Calcula o dano
        float damage =
            (fallDistance - minimumFallHeight)
            * fallDamagePerMeter;

        TakeDamage(damage);

        Debug.Log(
            "Queda de " +
            fallDistance +
            "m | Dano: " +
            damage
        );
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;

        currentHealth =
            Mathf.Max(
                currentHealth,
                0f
            );

        Debug.Log(
            "Player recebeu " +
            damage +
            " de dano. Vida: " +
            currentHealth
        );

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // =========================================================
    // MORTE
    // =========================================================

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        currentHealth = 0f;

        Debug.Log("PLAYER MORREU!");

        // Para completamente o Rigidbody
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Para a física
        rb.isKinematic = true;

        // Desativa todos os colliders
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Esconde o personagem
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = false;
        }

        // Limpa os inputs
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;

        isJumping = false;
        isSprinting = false;
        isJumpCut = false;

        Debug.Log("O player MORREU!");
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        float currentSpeed =
            isSprinting ?
            sprintSpeed :
            walkSpeed;

        Vector3 forward =
            cameraPivot.forward;

        Vector3 right =
            cameraPivot.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        moveDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        Vector3 targetVelocity =
            moveDirection * currentSpeed;

        if (isGrounded)
        {
            Vector3 horizontalVelocity =
                new Vector3(
                    rb.linearVelocity.x,
                    0f,
                    rb.linearVelocity.z
                );

            Vector3 velocityChange =
                targetVelocity -
                horizontalVelocity;

            rb.AddForce(
                velocityChange * 10f,
                ForceMode.Force
            );
        }
        else
        {
            rb.AddForce(
                targetVelocity *
                10f *
                airMultiplier,
                ForceMode.Force
            );
        }

        LimitVelocity(currentSpeed);

        // Rotação do personagem
        if (moveDirection != Vector3.zero &&
            !isDoubleJumping)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    moveDirection
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.fixedDeltaTime /
                    rotationSmoothTime
                );
        }
    }

    private void LimitVelocity(float speed)
    {
        Vector3 horizontalVelocity =
            new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z
            );

        if (horizontalVelocity.magnitude > speed)
        {
            Vector3 limitedVelocity =
                horizontalVelocity.normalized *
                speed;

            rb.linearVelocity =
                new Vector3(
                    limitedVelocity.x,
                    rb.linearVelocity.y,
                    limitedVelocity.z
                );
        }
    }

    // =========================================================
    // JUMP
    // =========================================================

    private void HandleJump()
    {
        if (isJumping &&
            currentJumps > 0)
        {
            bool isDoubleJump =
                !isGrounded &&
                currentJumps < maxJumps;

            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            currentJumps--;

            // Double Jump
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

        // Jump Cut
        if (isJumpCut &&
            !isGrounded &&
            rb.linearVelocity.y > 0)
        {
            rb.linearVelocity =
                new Vector3(
                    rb.linearVelocity.x,
                    rb.linearVelocity.y * 0.5f,
                    rb.linearVelocity.z
                );

            isJumpCut = false;
        }
    }

    // =========================================================
    // FLIP
    // =========================================================

    private void ApplyFlip()
    {
        if (!isGrounded &&
            isDoubleJumping &&
            !hasCompletedFlip)
        {
            float angle =
                flipRotationSpeed *
                Time.fixedDeltaTime;

            transform.Rotate(
                Vector3.right,
                angle,
                Space.Self
            );

            flipAccumulatedAngle += angle;

            if (flipAccumulatedAngle >= 360f)
            {
                hasCompletedFlip = true;
            }
        }
    }

    // =========================================================
    // GRAVITY
    // =========================================================

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            bool isPressingDown =
                moveInput.y < -0.5f;

            float multiplier =
                gravityScale;

            if (rb.linearVelocity.y < 0)
            {
                multiplier =
                    gravityScale *
                    fallMultiplier;

                if (enableFastFall &&
                    isPressingDown)
                {
                    multiplier =
                        gravityScale *
                        fastFallMultiplier;
                }
            }
            else if (
                rb.linearVelocity.y > 0 &&
                isJumpCut)
            {
                multiplier =
                    gravityScale *
                    jumpCutMultiplier;
            }

            float gravityForce =
                Physics.gravity.y *
                multiplier *
                rb.mass;

            rb.AddForce(
                Vector3.down *
                Mathf.Abs(gravityForce),
                ForceMode.Force
            );

            if (rb.linearVelocity.y <
                -maxFallSpeed)
            {
                rb.linearVelocity =
                    new Vector3(
                        rb.linearVelocity.x,
                        -maxFallSpeed,
                        rb.linearVelocity.z
                    );
            }
        }
    }

    // =========================================================
    // GROUND CHECK
    // =========================================================

    private void CheckGround()
    {
        Vector3 spherePosition =
            transform.position -
            Vector3.up * 0.1f;

        isGrounded =
            Physics.SphereCast(
                spherePosition,
                groundCheckRadius,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundMask
            );

        if (isGrounded)
        {
            isJumpCut = false;

            currentJumps = maxJumps;

            // Termina o flip ao tocar no chão
            if (isDoubleJumping &&
                !hasCompletedFlip)
            {
                float remainingAngle =
                    360f -
                    flipAccumulatedAngle;

                transform.Rotate(
                    Vector3.right,
                    remainingAngle,
                    Space.Self
                );

                hasCompletedFlip = true;
            }

            isDoubleJumping = false;

            // Corrige rotação
            Vector3 currentEuler =
                transform.eulerAngles;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    currentEuler.y,
                    0f
                );
        }
    }

    // =========================================================
    // DRAG
    // =========================================================

    private void HandleDrag()
    {
        rb.linearDamping =
            isGrounded ?
            groundDrag :
            0f;
    }

    // =========================================================
    // COYOTE TIME
    // =========================================================

    private void HandleCoyoteTime()
    {
        if (isGrounded)
        {
            coyoteTimer =
                coyoteTime;
        }
        else
        {
            coyoteTimer -=
                Time.deltaTime;
        }
    }

    // =========================================================
    // CAMERA PIVOT
    // =========================================================

    private void UpdateCameraPivotPosition()
    {
        if (cameraPivot != null)
        {
            cameraPivot.position =
                transform.position +
                Vector3.up *
                cameraHeight;
        }
    }

    // =========================================================
    // CAMERA ROTATION
    // =========================================================

    private void HandleCameraRotation()
    {
        horizontalRotation +=
            lookInput.x *
            mouseSensitivity;

        verticalRotation -=
            lookInput.y *
            mouseSensitivity;

        verticalRotation =
            Mathf.Clamp(
                verticalRotation,
                minVerticalAngle,
                maxVerticalAngle
            );

        if (cameraPivot != null)
        {
            cameraPivot.rotation =
                Quaternion.Euler(
                    verticalRotation,
                    horizontalRotation,
                    0f
                );
        }
    }

    // =========================================================
    // CAMERA COLLISION
    // =========================================================

    private void HandleCameraCollision()
    {
        if (cameraPivot == null)
            return;

        Vector3 desiredPosition =
            cameraPivot.position -
            cameraPivot.forward *
            cameraDistance;

        Vector3 direction =
            (desiredPosition -
             cameraPivot.position).normalized;

        float distance =
            cameraDistance;

        if (Physics.SphereCast(
            cameraPivot.position,
            cameraCollisionRadius,
            direction,
            out RaycastHit hit,
            cameraDistance,
            collisionMask))
        {
            distance =
                Mathf.Max(
                    hit.distance -
                    cameraCollisionRadius,
                    cameraMinDistance
                );
        }

        currentCameraDistance =
            Mathf.Lerp(
                currentCameraDistance,
                distance,
                Time.deltaTime * 15f
            );
    }

    // =========================================================
    // CAMERA POSITION
    // =========================================================

    private void UpdateCameraPosition()
    {
        if (cameraTransform == null ||
            cameraPivot == null)
            return;

        Vector3 targetPosition =
            cameraPivot.position -
            cameraPivot.forward *
            currentCameraDistance;

        cameraTransform.position =
            Vector3.SmoothDamp(
                cameraTransform.position,
                targetPosition,
                ref cameraVelocity,
                cameraSmoothTime
            );

        cameraTransform.LookAt(
            cameraPivot
        );
    }

    // =========================================================
    // NO FRICTION MATERIAL
    // =========================================================

    private void CreateNoFrictionMaterial()
    {
        PhysicsMaterial noFriction =
            new PhysicsMaterial("NoFriction");

        noFriction.dynamicFriction = 0f;
        noFriction.staticFriction = 0f;

        noFriction.frictionCombine =
            PhysicsMaterialCombine.Minimum;

        noFriction.bounciness = 0f;

        noFriction.bounceCombine =
            PhysicsMaterialCombine.Minimum;

        Collider col =
            GetComponent<Collider>();

        if (col != null)
        {
            col.material = noFriction;
        }
        else
        {
            Debug.LogWarning(
                "Nenhum collider encontrado para aplicar o material sem atrito."
            );
        }
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void OnMovePerformed(
        InputAction.CallbackContext context)
    {
        if (isDead)
            return;

        moveInput =
            context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(
        InputAction.CallbackContext context)
    {
        moveInput =
            Vector2.zero;
    }

    private void OnJumpPerformed(
        InputAction.CallbackContext context)
    {
        if (isDead)
            return;

        if (isGrounded ||
            coyoteTimer > 0 ||
            currentJumps > 0)
        {
            isJumping = true;

            isJumpCut = false;

            coyoteTimer = 0;
        }
    }

    private void OnJumpCanceled(
        InputAction.CallbackContext context)
    {
        if (isDead)
            return;

        if (!isGrounded)
            isJumpCut = true;

        isJumping = false;
    }

    private void OnSprintPerformed(
        InputAction.CallbackContext context)
    {
        if (!isDead)
            isSprinting = true;
    }

    private void OnSprintCanceled(
        InputAction.CallbackContext context)
    {
        isSprinting = false;
    }

    private void OnLookPerformed(
        InputAction.CallbackContext context)
    {
        if (isDead)
            return;

        lookInput =
            context.ReadValue<Vector2>();
    }

    private void OnLookCanceled(
        InputAction.CallbackContext context)
    {
        lookInput =
            Vector2.zero;
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            isGrounded ?
            Color.green :
            Color.red;

        Vector3 spherePosition =
            transform.position -
            Vector3.up * 0.1f;

        Gizmos.DrawWireSphere(
            spherePosition -
            Vector3.up *
            groundCheckDistance,
            groundCheckRadius
        );

        if (cameraPivot != null)
        {
            Gizmos.color =
                Color.blue;

            Gizmos.DrawWireSphere(
                cameraPivot.position,
                cameraCollisionRadius
            );

            Gizmos.DrawLine(
                cameraPivot.position,
                cameraPivot.position -
                cameraPivot.forward *
                cameraDistance
            );
        }
    }
}

