using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; 

public class PlayerConroller : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float punchForce = 10f;
    [SerializeField] private float kickForce = 15f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask attackableLayer;
    
    [Header("Camera Settings")]
    [SerializeField] private Camera camera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2f, -5f);
    [SerializeField] private float cameraSensitivity = 2f;
    private float mouseX;
    private float mouseY;
    private const float maxVerticalAngle = 80f;
    
    private CharacterController characterController;
    private bool isGrounded;
    private bool isStandartCast;
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 0.4f;
    [SerializeField] private float jumpForce = 2f;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    private Vector3 velocity;
    private Vector3 moveDirection;
    
    
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction standartAttackAction;
    private InputAction kickAction;
    
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private float landAnimationDuration = 0.3f;
    
    private float lastGroundedTime;
    private bool wasGrounded = true;
    private bool isLanding = false;
    
    void Awake() //запускается самым первым после создания объекта
    {
        CreateInputActions();
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        
        Transform cameraTransform = camera.transform;
        cameraTransform.position = transform.position + cameraOffset;
        cameraTransform.LookAt(transform.position + Vector3.up * 1.5f);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleInput();
        RotateCamera();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void UpdateAnimations()
    {
        if (animator == null)
        {
            Debug.Log("Animator is null");
            return;
        }
        
        CheckGround();
        
        // Получаем ввод движения для анимации
        Vector2 moveInput = moveAction.ReadValue<Vector2>();

        int direction = getDirection(moveInput);
    
        // Используем глобальные оси или нормализованный ввод для анимации
        float animationSpeed = moveInput.magnitude;
    
        // Обновляем параметры аниматора
        animator.SetInteger("direction", direction);
        animator.SetFloat("speed", animationSpeed);
        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("verticalVelocity", velocity.y);
        
        animator.SetBool("isStandartCast", isStandartCast);

        if (isStandartCast && animator.GetCurrentAnimatorStateInfo(0).IsName("StandartAttack") 
                           && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.9f)
        {
            isStandartCast = false;
        }
        
        // Определяем прыжок/падение
        if (!isGrounded)
        {
            if (velocity.y > 0.1f)
            {
                animator.SetBool("isJumping", true);
            }
            else if (velocity.y < -0.1f)
            {
                animator.SetBool("isJumping", false);
            }
        }
        else
        {
            animator.SetBool("isJumping", false);
        }
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
        
        // Standart attack Action
        standartAttackAction = playerMap.AddAction("Punch", type: InputActionType.Button);
        standartAttackAction.AddBinding("<Mouse>/leftButton");
        
        // Kick Action
        kickAction = playerMap.AddAction("Kick", type: InputActionType.Button);
        kickAction.AddBinding("<Mouse>/rightButton");
        
        // Включаем все действия
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        standartAttackAction.Enable();
        kickAction.Enable();
    }
    
    void HandleInput()
    {
        if (jumpAction.triggered && isGrounded)
        {
            Jump();
        }
        
        if (standartAttackAction.triggered)
            StandartAttack();
        
        if (kickAction.triggered)
            Kick();
    }

    void HandleMovement()
    {
        // Проверка земли
        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckDistance, groundLayer);
    
        // Если на земле и скорость падения отрицательная, обнуляем вертикальную скорость
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        // Получаем ввод движения
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        
        // Преобразуем ввод в локальное направление движения
        moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        
        // Применяем скорость
        characterController.Move(moveDirection * moveSpeed * Time.fixedDeltaTime);
    
        // Применяем гравитацию
        velocity.y -= gravity * Time.fixedDeltaTime;
    
        // Применяем вертикальное движение
        characterController.Move(velocity * Time.fixedDeltaTime);
    }

    void Jump()
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * 2f * gravity);
            Debug.Log("Jump!");
        }
    }

    void StandartAttack()
    {
        Debug.Log("StandartAttack! isStandartCast = " + isStandartCast);
        isStandartCast = true;
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
        
        camera.transform.position = Vector3.Lerp(camera.transform.position, cameraPosition, 10f * Time.deltaTime);
        camera.transform.LookAt(transform.position + Vector3.up * 1.5f);
        
        Vector3 playerRotation = transform.eulerAngles;
        playerRotation.y = mouseX;
        transform.rotation = Quaternion.Euler(playerRotation);
    }
    
    void CheckGround()
    {
        RaycastHit hit;
        bool previouslyGrounded = isGrounded;
    
        // Проверяем землю с помощью Raycast
        bool isGroundedAnimation = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            out hit,
            groundCheckDistance,
            groundLayer
        );
    
        // Если только что приземлились
        if (!previouslyGrounded && isGroundedAnimation && !isLanding)
        {
            StartCoroutine(PlayLandAnimation());
        }
    
        wasGrounded = isGroundedAnimation;
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

    // 0 - никуда
    // 1 - прямо
    // 2 - прямо-право
    // 3 - прааво
    // 4 - назд-право
    // 5 - назад
    // 6 - назад-лево
    // 7 - лево
    // 8 - прямо-лево
    private int getDirection(Vector2 moveInput)
    {
        float x = moveInput.x;
        float y = moveInput.y;

        if (y == 1)
            return 1;
        else if (x > 0 && y > 0)
            return 2;
        else if (x == 1)
            return 3;
        else if (y < 0 && x > 0)
            return 4;
        else if (y == -1)
            return 5;
        else if (y < 0 && x < 0)
            return 6;
        else if (x == -1)
            return 7;
        else if (y > 0 && x < 0)
            return 8;
        else
            return 0;
    }
}
