using UnityEngine;

/// <summary>
/// Puente entre la lógica del juego y el Animator.
/// No toma decisiones — escucha eventos y traduce estado a parámetros.
///
/// RESPONSABILIDAD ÚNICA:
/// Sabe cómo hablarle al Animator. No sabe nada de gameplay.
///
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimatorManager : MonoBehaviour
{
    // Hashes en lugar de strings para comunicarse con el Animator.
    // Animator.StringToHash se calcula una vez en Awake y es ~3x más rápido
    // que pasar strings en cada Update. Con oleadas de enemigos, cada
    // microsegundo en Update importa.
    private static readonly int VelocityX   = Animator.StringToHash("VelocityX");
    private static readonly int VelocityZ   = Animator.StringToHash("VelocityZ");
    private static readonly int IsDashing   = Animator.StringToHash("IsDashing");
    private static readonly int TakeDamage  = Animator.StringToHash("TakeDamage");
    private static readonly int IsDeadParam = Animator.StringToHash("IsDead");
    private static readonly int IsAiming    = Animator.StringToHash("IsAiming");

    [Header("Suavizado de blend tree")]
    [Tooltip("Qué tan rápido transicionan las animaciones de movimiento. " +
             "Valores bajos = más suave pero con lag. 10-15 es un buen punto de partida.")]
    [SerializeField] private float velocityDampTime = 10f;

    private Animator _animator;
    private PlayerStateMachine _stateMachine;
    private PlayerController _controller;

    private void Awake()
    {
        _animator    = GetComponent<Animator>();
        _stateMachine = GetComponent<PlayerStateMachine>();
        _controller  = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        PlayerStateMachine.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        // Siempre desuscribirse para evitar memory leaks y errores
        // si el objeto se destruye mientras el evento aún existe
        PlayerStateMachine.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        // Solo actualizamos blend tree si estamos en un estado de movimiento.
        // Durante dash, daño o muerte el Animator ya tiene control del clip.
        if (_stateMachine.CurrentState != PlayerStateMachine.PlayerState.Moving) return;

        UpdateLocomotionBlend();
    }

    private void UpdateLocomotionBlend()
    {
        // Convertimos la velocidad de espacio mundo a espacio local del personaje.
        // Esto es lo que hace que las animaciones sean correctas sin importar
        // hacia dónde está rotado el personaje.
        Vector3 localVelocity = transform.InverseTransformDirection(_controller.MoveDirection);

        // MoveSpeed del PlayerController para escalar correctamente
        // Usamos Lerp en lugar de SetFloat directo para suavizar la transición
        // entre animaciones y evitar pops visuales
        float currentX = _animator.GetFloat(VelocityX);
        float currentZ = _animator.GetFloat(VelocityZ);

        float targetX = localVelocity.x;
        float targetZ = localVelocity.z;

        // Lerp manual en lugar del dampTime integrado de SetFloat
        // porque nos da más control sobre el comportamiento
        _animator.SetFloat(VelocityX, Mathf.Lerp(currentX, targetX, velocityDampTime * Time.deltaTime));
        _animator.SetFloat(VelocityZ, Mathf.Lerp(currentZ, targetZ, velocityDampTime * Time.deltaTime));
    }

    private void HandleStateChanged(PlayerStateMachine.PlayerState previous,
                                    PlayerStateMachine.PlayerState next)
    {
        switch (next)
        {
            case PlayerStateMachine.PlayerState.Dashing:
                // Trigger dispara la transición inmediata a la animación de dash.
                // El Animator Controller debe tener una transición desde Any State
                // con Has Exit Time = false para que sea instantánea.
                _animator.SetTrigger(IsDashing);
                break;

            case PlayerStateMachine.PlayerState.TakingDamage:
                _animator.SetTrigger(TakeDamage);
                break;

            case PlayerStateMachine.PlayerState.Dead:
                // Bool en lugar de trigger porque queremos que se quede en el
                // estado muerto indefinidamente, no que vuelva al estado anterior.
                _animator.SetBool(IsDeadParam, true);
                break;

            case PlayerStateMachine.PlayerState.Moving:
                // Al volver a Moving reseteamos los parámetros para que
                // el blend tree arranque desde cero sin valores residuales
                if (previous == PlayerStateMachine.PlayerState.Dashing)
                {
                    // Pequeño reset para evitar que el blend tree tenga
                    // valores de velocidad de antes del dash
                    _animator.SetFloat(VelocityX, 0f);
                    _animator.SetFloat(VelocityZ, 0f);
                }
                break;
        }
    }

    // ── API pública para WeaponSystem ─────────────────────────────────────

    /// <summary>
    /// WeaponSystem llama esto cuando hay un target en el cono de apuntado.
    /// Activa/desactiva la capa superior de apuntar.
    /// </summary>
    public void SetAiming(bool isAiming)
    {
        _animator.SetBool(IsAiming, isAiming);
    }

    // ── Llamado desde Animation Events ───────────────────────────────────

    /// <summary>
    /// Agrega un Animation Event al último frame de la animación de TakeDamage.
    /// Cuando el clip termina, el Animator llama este método automáticamente.
    /// Así la transición de vuelta a Moving está controlada por la animación,
    /// no por un timer arbitrario en código.
    /// </summary>
    public void OnTakeDamageAnimationEnd()
    {
        _stateMachine.TransitionTo(PlayerStateMachine.PlayerState.Moving);
    }

    /// <summary>
    /// Igual pero para el dash. Agrega un Animation Event al último frame
    /// de la animación de roll. El DashSystem también puede llamar esto
    /// si necesita terminar el dash antes de que termine la animación.
    /// </summary>
    public void OnDashAnimationEnd()
    {
        // El DashSystem escucha OnStateChanged para saber cuándo limpiar
        // la velocidad externa — aquí solo hacemos la transición de estado.
        _stateMachine.TransitionTo(PlayerStateMachine.PlayerState.Moving);
    }
}