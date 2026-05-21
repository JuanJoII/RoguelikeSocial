using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja movimiento y rotación del jugador usando el New Input System.
/// 
/// SETUP REQUERIDO:
/// 1. Agrega el componente PlayerInput a este GameObject
/// 2. Asígnale el Input Actions asset
/// 3. En Behavior, selecciona "Invoke Unity Events"
/// 4. Conecta los eventos Move, Look, Dash, SwitchBullet a los métodos de este script
/// 
/// Los OnScreenStick y OnScreenButton del Canvas se conectan al mismo
/// Input Actions asset — no requieren configuración adicional en código.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 30f;

    [Header("Rotación")]
    [SerializeField] private float rotationSpeed = 900f;

    [Header("Gravedad")]
    [SerializeField] private float gravity = -20f;

    // Referencias
    private CharacterController _cc;
    private PlayerStateMachine _stateMachine;

    // Input — guardamos el valor del frame, no lo leemos en Update
    // Los callbacks del Input System pueden llamarse fuera de Update,
    // así que guardamos el valor y lo consumimos en Update/FixedUpdate
    private Vector2 _moveInput;
    private Vector2 _lookInput;

    // Estado de movimiento
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;

    public Vector3 MoveDirection => _horizontalVelocity.normalized;

    // Control externo para el DashSystem
    public bool ExternalVelocityActive { get; private set; }
    private Vector3 _externalVelocity;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.Dead) return;

        if (ExternalVelocityActive)
        {
            _cc.Move(_externalVelocity * Time.deltaTime);
        }
        else
        {
            HandleMovement();
        }

        HandleRotation();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.TakingDamage)
        {
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, Vector3.zero, deceleration * Time.deltaTime);
            _cc.Move(_horizontalVelocity * Time.deltaTime);
            return;
        }

        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
        bool isMoving = inputDir.magnitude > 0.1f;

        Vector3 targetVelocity = isMoving ? inputDir.normalized * moveSpeed : Vector3.zero;
        float rate = isMoving ? acceleration : deceleration;

        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity, targetVelocity, rate * Time.deltaTime);

        _cc.Move(_horizontalVelocity * Time.deltaTime);
    }

    private void HandleRotation()
    {
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.Dead) return;
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.TakingDamage) return;

        if (_lookInput.magnitude > 0.1f)
        {
            Vector3 lookDir = new Vector3(_lookInput.x, 0f, _lookInput.y);
            Quaternion targetRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;
        _cc.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
    }

    // ── Callbacks del Input System ────────────────────────────────────────
    // Estos métodos se conectan en el PlayerInput component
    // mediante "Invoke Unity Events" en el Inspector.

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        _lookInput = context.ReadValue<Vector2>();
    }

    // Dash y SwitchBullet usan "performed" — solo el frame en que se presiona.
    // Los sistemas correspondientes están suscritos a estos eventos directamente.
    // PlayerController solo los recibe si necesita reaccionar al input,
    // pero DashSystem y WeaponSystem se suscriben por su cuenta al Input Actions.

    // ── API para sistemas externos ────────────────────────────────────────

    public void SetExternalVelocity(Vector3 velocity)
    {
        _externalVelocity = velocity;
        ExternalVelocityActive = true;
    }

    public void ClearExternalVelocity()
    {
        _externalVelocity = Vector3.zero;
        ExternalVelocityActive = false;
    }
}