using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja la lógica completa del dash.
///
/// RESPONSABILIDADES:
/// - Leer el input de dash
/// - Validar que el estado permite dashear
/// - Rotar al jugador hacia la dirección de movimiento
/// - Aplicar velocidad externa al PlayerController
/// - Limpiar la velocidad cuando el dash termina
///
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerController))]
public class DashSystem : MonoBehaviour
{
    [Header("Dash")]
    [Tooltip("Velocidad durante el dash. Ajusta para que recorra la distancia visual correcta.")]
    [SerializeField] private float dashSpeed = 14f;

    [Tooltip("Qué tan rápido rota el jugador hacia la dirección del dash antes de ejecutarlo. " +
             "Valores muy altos = rotación casi instantánea.")]
    [SerializeField] private float preRotationSpeed = 2000f;

    [Tooltip("Tiempo máximo que el dash puede durar como seguridad. " +
             "Normalmente termina antes por el Animation Event. " +
             "Ponlo un poco mayor que la duración del clip de roll.")]
    [SerializeField] private float maxDashDuration = 0.6f;

    // Referencias
    private PlayerStateMachine _stateMachine;
    private PlayerController _controller;

    // Estado interno
    private Vector3 _dashDirection;
    private Coroutine _dashCoroutine;

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
        _controller   = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        PlayerStateMachine.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        PlayerStateMachine.OnStateChanged -= HandleStateChanged;
    }

    // ── Input del New Input System ────────────────────────────────────────
    // Conéctalo en el PlayerInput component → Invoke Unity Events → Dash

    public void OnDash(InputAction.CallbackContext context)
    {
        // performed = exactamente el frame en que se presiona
        // Ignoramos started y canceled
        if (!context.performed) return;

        // Solo dasheamos desde Moving — la StateMachine rechaza el resto
        if (_stateMachine.CurrentState != PlayerStateMachine.PlayerState.Moving) return;

        ExecuteDash();
    }

    // ── Lógica del dash ───────────────────────────────────────────────────

    private void ExecuteDash()
    {
        // Dirección: si el jugador se está moviendo, usa esa dirección.
        // Si está quieto, usa el frente del personaje.
        // Nunca dasheamos en Vector3.zero — eso congela al jugador en el aire.
        _dashDirection = _controller.MoveDirection.magnitude > 0.1f
            ? _controller.MoveDirection
            : transform.forward;

        // Rotamos al jugador hacia la dirección del dash ANTES de la animación.
        // Usamos RotateTowards en una coroutine breve para que se vea natural
        // en lugar de un snap instantáneo.
        if (_dashCoroutine != null) StopCoroutine(_dashCoroutine);
        _dashCoroutine = StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        // Fase 1: rotación hacia la dirección del dash
        // Dura hasta que la rotación esté lo suficientemente alineada
        // o hasta un límite de tiempo para no bloquear el dash
        Quaternion targetRotation = Quaternion.LookRotation(_dashDirection, Vector3.up);
        float rotationTimer = 0f;
        float maxRotationTime = 0.08f; // máximo 80ms esperando la rotación

        while (rotationTimer < maxRotationTime)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, preRotationSpeed * Time.deltaTime);

            rotationTimer += Time.deltaTime;

            // Si ya está suficientemente alineado, no esperamos más
            if (Quaternion.Angle(transform.rotation, targetRotation) < 5f) break;

            yield return null;
        }

        // Nos aseguramos de que la rotación sea exacta antes de empezar
        transform.rotation = targetRotation;

        // Fase 2: activamos el estado y la velocidad
        _stateMachine.TransitionTo(PlayerStateMachine.PlayerState.Dashing);
        _controller.SetExternalVelocity(_dashDirection * dashSpeed);

        // Fase 3: esperamos a que termine el dash
        // El dash termina cuando OnDashComplete() es llamado desde el
        // Animation Event, lo que transiciona la StateMachine de vuelta a Moving.
        // HandleStateChanged detecta ese cambio y limpia la velocidad.
        // El timer de seguridad evita que el jugador quede atrapado si
        // el Animation Event falla (clip no configurado, etc.)
        float safetyTimer = 0f;
        while (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.Dashing
               && safetyTimer < maxDashDuration)
        {
            safetyTimer += Time.deltaTime;
            yield return null;
        }

        // Si llegamos aquí por el timer de seguridad (no por el Animation Event),
        // forzamos el fin del dash para que el jugador no quede bloqueado.
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.Dashing)
        {
            Debug.LogWarning("[DashSystem] Dash terminado por timer de seguridad. " +
                             "Verifica que el Animation Event esté configurado en el clip.");
            ForceEndDash();
        }
    }

    private void HandleStateChanged(PlayerStateMachine.PlayerState previous,
                                    PlayerStateMachine.PlayerState next)
    {
        // Cuando volvemos a Moving desde Dashing, limpiamos la velocidad externa.
        // Esto cubre tanto el caso normal (Animation Event) como el caso de
        // seguridad (timer).
        if (previous == PlayerStateMachine.PlayerState.Dashing &&
            next == PlayerStateMachine.PlayerState.Moving)
        {
            _controller.ClearExternalVelocity();
        }
    }

    private void ForceEndDash()
    {
        _controller.ClearExternalVelocity();
        _stateMachine.TransitionTo(PlayerStateMachine.PlayerState.Moving);
    }
}