using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; 

public class ThirdPersonControllerSimple : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float jumpForce = 8f;
    
    [Header("Attack Settings")]
    [SerializeField] private float punchForce = 10f;
    [SerializeField] private float kickForce = 15f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask attackableLayer;
    
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2f, -5f);
    [SerializeField] private float cameraSensitivity = 2f;
    
    [Header("Attack Visualization")]
    [SerializeField] private LineRenderer attackRangeVisualizer;
    [SerializeField] private int circleSegments = 36;
    [SerializeField] private float circleHeight = 0.5f;
    [SerializeField] private Color circleColor = new Color(1f, 0f, 0f, 0.3f);
    
    // Автоматически созданные Input Actions
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction punchAction;
    private InputAction kickAction;
    
    private Rigidbody rb;
    private bool isGrounded;
    private float mouseX;
    private float mouseY;
    private const float maxVerticalAngle = 80f;
    
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private float landAnimationDuration = 0.3f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private float lastGroundedTime;
    private bool wasGrounded = true;
    private bool isLanding = false;
    
    void Awake()
    {
        // Создаем Input Actions программно
        CreateInputActions();
    }
    
    void CreateInputActions()
    {
        // Создаем Action Map
        var playerMap = new InputActionMap("Player");
        
        // Move Action (WASD)
        moveAction = playerMap.AddAction("Move", type: InputActionType.Value);
        var moveComposite = moveAction.AddCompositeBinding("2DVector");
        moveComposite.With("Up", "<Keyboard>/w");
        moveComposite.With("Down", "<Keyboard>/s");
        moveComposite.With("Left", "<Keyboard>/a");
        moveComposite.With("Right", "<Keyboard>/d");
        
        // Look Action (Mouse)
        lookAction = playerMap.AddAction("Look", type: InputActionType.Value);
        lookAction.AddBinding("<Mouse>/delta");
        
        // Jump Action
        jumpAction = playerMap.AddAction("Jump", type: InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        
        // Punch Action
        punchAction = playerMap.AddAction("Punch", type: InputActionType.Button);
        punchAction.AddBinding("<Mouse>/leftButton");
        
        // Kick Action
        kickAction = playerMap.AddAction("Kick", type: InputActionType.Button);
        kickAction.AddBinding("<Mouse>/rightButton");
        
        // Включаем все действия
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        punchAction.Enable();
        kickAction.Enable();
    }
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
        
        cameraTransform.position = transform.position + cameraOffset;
        cameraTransform.LookAt(transform.position + Vector3.up * 1.5f);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Инициализация визуализатора атаки
        InitializeAttackVisualizer();
    }
    
    void Update()
    {
        HandleInput();
        RotateCamera();
        UpdateAttackVisualization();
        UpdateAnimations(); 
    }
    
    void UpdateAnimations()
    {
        if (animator == null) return;
    
        CheckGround();
    
        // Берем только горизонтальную скорость (игнорируем вертикальную)
        Vector3 horizontalVelocity = rb.linearVelocity;

        horizontalVelocity.y = 0;
        float currentSpeed = horizontalVelocity.magnitude;
    
        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("verticalVelocity", rb.linearVelocity.y);
        animator.SetFloat("speed", currentSpeed); // Используем горизонтальную скорость
    
        // Определяем прыжок/падение
        if (!isGrounded)
        {
            if (rb.linearVelocity.y > 0.1f)
            {
                animator.SetBool("isJumping", true);
            }
            else if (rb.linearVelocity.y < -0.1f)
            {
                animator.SetBool("isJumping", false);
            }
        }
        else
        {
            animator.SetBool("isJumping", false);
        }
    }
    
    void HandleInput()
    {
        // Прыжок
        if (jumpAction.triggered && isGrounded)
        {
            Jump();
        }
        
        // Удар рукой
        if (punchAction.triggered)
        {
            Punch();
        }
        
        // Удар ногой
        if (kickAction.triggered)
        {
            Kick();
        }
    }
    
    void FixedUpdate()
    {
        HandleMovement();
    }
    
    void HandleMovement()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
    
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
    
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
    
        Vector3 moveDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;
    
        // Текущая горизонтальная скорость
        Vector3 currentVelocity = rb.linearVelocity;

        if (currentVelocity.x < 2.1f || currentVelocity.x > -2.1f)
        {
            Debug.Log("SET currentVelocity.x = 0 AND currentVelocity.z = 0!!!!");
            currentVelocity.x = 0;
            currentVelocity.z = 0;
        }
        currentVelocity.y = 0;
    
        // Вычисляем силу для достижения целевой скорости
        Vector3 velocityDifference = targetVelocity - currentVelocity;
        Vector3 force = velocityDifference * 10f; // Множитель можно настроить

        
        Debug.Log("force = " + force + "; force.magnitude = " + force.magnitude);
        
        
        rb.AddForce(force, ForceMode.Acceleration);
    
        // Поворот персонажа
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
    
    void Jump()
    {
        if (isGrounded && !isLanding)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        
            // Запускаем анимацию прыжка
            if (animator != null)
            {
                animator.SetTrigger("Jump");
            }
        }
    }
    
    void Punch()
    {
        Debug.Log("Punch!");
        PerformAttack(punchForce, "Punch");
    }
    
    void Kick()
    {
        Debug.Log("Kick!");
        PerformAttack(kickForce, "Kick");
    }
    
    void PerformAttack(float force, string attackType)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position + Vector3.up * 0.5f,
            0.5f,
            transform.forward,
            attackRange,
            attackableLayer
        );
        
        foreach (RaycastHit hit in hits)
        {
            Rigidbody hitRb = hit.collider.GetComponent<Rigidbody>();
            if (hitRb != null)
            {
                Vector3 direction = (hit.transform.position - transform.position).normalized;
                direction.y = 0.3f;
                hitRb.AddForce(direction * force, ForceMode.Impulse);
                Debug.Log($"{attackType} hit: {hit.collider.name}");
            }
        }
    }
    
    void RotateCamera()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        
        mouseX += lookInput.x * cameraSensitivity * Time.deltaTime * 100f;
        mouseY -= lookInput.y * cameraSensitivity * Time.deltaTime * 100f;
        
        mouseY = Mathf.Clamp(mouseY, -maxVerticalAngle, maxVerticalAngle);
        
        Quaternion rotation = Quaternion.Euler(mouseY, mouseX, 0);
        Vector3 cameraPosition = transform.position + rotation * cameraOffset;
        
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, cameraPosition, 10f * Time.deltaTime);
        cameraTransform.LookAt(transform.position + Vector3.up * 1.5f);
        
        Vector3 playerRotation = transform.eulerAngles;
        playerRotation.y = mouseX;
        transform.rotation = Quaternion.Euler(playerRotation);
    }
    
    void CheckGround()
    {
        RaycastHit hit;
        bool previouslyGrounded = isGrounded;
    
        // Проверяем землю с помощью Raycast
        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            out hit,
            groundCheckDistance,
            groundLayer
        );
    
        // Если только что приземлились
        if (!previouslyGrounded && isGrounded && !isLanding)
        {
            StartCoroutine(PlayLandAnimation());
        }
    
        wasGrounded = isGrounded;
    }
    
    IEnumerator PlayLandAnimation()
    {
        isLanding = true;
    
        // Запускаем анимацию приземления
        if (animator != null)
        {
            animator.SetTrigger("isLanding");
        }
    
        // Ждем завершения анимации
        yield return new WaitForSeconds(landAnimationDuration);
    
        isLanding = false;
    }
    
    void OnDestroy()
    {
        // Очистка
        if (moveAction != null) moveAction.Disable();
        if (lookAction != null) lookAction.Disable();
        if (jumpAction != null) jumpAction.Disable();
        if (punchAction != null) punchAction.Disable();
        if (kickAction != null) kickAction.Disable();
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f + transform.forward * attackRange, 0.5f);
    }
    
    void InitializeAttackVisualizer()
    {
        // Создаем LineRenderer если его нет
        if (attackRangeVisualizer == null)
        {
            attackRangeVisualizer = gameObject.AddComponent<LineRenderer>();
        }
    
        attackRangeVisualizer.positionCount = circleSegments + 1;
        attackRangeVisualizer.useWorldSpace = true;
        attackRangeVisualizer.startWidth = 0.1f;
        attackRangeVisualizer.endWidth = 0.1f;
        attackRangeVisualizer.material = new Material(Shader.Find("Sprites/Default"));
        attackRangeVisualizer.startColor = circleColor;
        attackRangeVisualizer.endColor = circleColor;
        attackRangeVisualizer.enabled = true; // Всегда виден в игровом режиме
    }
    
    void UpdateAttackVisualization()
    {
        if (attackRangeVisualizer == null) return;
    
        // Создаем круг радиуса атаки
        Vector3 center = transform.position + Vector3.up * circleHeight + transform.forward * attackRange;
        float radius = 0.5f;
    
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * (360f / circleSegments) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
        
            Vector3 point = center + transform.right * x + transform.forward * z;
            attackRangeVisualizer.SetPosition(i, point);
        }
    
        // Добавляем линию от персонажа к кругу
        Vector3 playerPosition = transform.position + Vector3.up * circleHeight;
        Vector3 attackDirection = transform.forward * attackRange;
        attackRangeVisualizer.SetPosition(circleSegments / 4, playerPosition);
        attackRangeVisualizer.SetPosition(circleSegments / 4 + 1, center);
    }
}