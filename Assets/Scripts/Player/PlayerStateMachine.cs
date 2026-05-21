using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Cerebro del jugador. Define qué puede y qué no puede hacer en cada momento.
/// Otros sistemas NO toman decisiones de estado — solo consultan propiedades
/// como CanShoot o HasIFrames, y llaman TransitionTo cuando corresponde.
/// </summary>
public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState { Moving, Dashing, TakingDamage, Dead }

    // Estado actual — solo legible desde fuera, modificable solo desde aquí
    public PlayerState CurrentState { get; private set; }

    // Evento que dispara cualquier cambio de estado.
    // UIManager, AnimatorManager, WeaponSystem lo escuchan.
    public static event Action<PlayerState, PlayerState> OnStateChanged;

    // ── Propiedades que otros sistemas consultan ─────────────────────────
    // WeaponSystem pregunta esto cada frame antes de disparar
    public bool CanShoot => CurrentState == PlayerState.Moving;

    // PlayerHealth pregunta esto antes de aplicar daño
    // Cubre tanto el dash como la ventana post-daño
    public bool HasIFrames => CurrentState == PlayerState.Dashing 
                              || _postDamageInvulnerable;

    [Header("Invulnerabilidad post-daño")]
    [Tooltip("Segundos de invulnerabilidad al volver de TakingDamage a Moving")]
    [SerializeField] private float postDamageInvulnerabilityTime = 0.8f;

    private bool _postDamageInvulnerable;
    private Coroutine _invulnerabilityCoroutine;

    private void Awake()
    {
        CurrentState = PlayerState.Moving;
    }

    /// <summary>
    /// Único punto de entrada para cambiar estado.
    /// Si la transición no es válida, simplemente no ocurre — sin excepciones.
    /// </summary>
    public void TransitionTo(PlayerState newState)
    {
        if (!IsTransitionValid(CurrentState, newState))
        {
            // Útil durante desarrollo para detectar transiciones incorrectas
            Debug.Log($"[PlayerStateMachine] Transición inválida: {CurrentState} → {newState}");
            return;
        }

        PlayerState previous = CurrentState;
        CurrentState = newState;

        OnStateChanged?.Invoke(previous, CurrentState);
        OnEnterState(newState, previous);
    }

    // Tabla de transiciones válidas.
    // Cada par (from, to) está explícitamente permitido o denegado.
    // Esto hace que las reglas sean legibles de un vistazo.
    private bool IsTransitionValid(PlayerState from, PlayerState to)
    {
        return (from, to) switch
        {
            // Desde Moving
            (PlayerState.Moving, PlayerState.Dashing)      => true,
            (PlayerState.Moving, PlayerState.TakingDamage) => true,
            (PlayerState.Moving, PlayerState.Dead)          => true,

            // Desde Dashing — i-frames bloquean daño
            (PlayerState.Dashing, PlayerState.Moving)       => true,
            (PlayerState.Dashing, PlayerState.TakingDamage) => false, // i-frames activos
            (PlayerState.Dashing, PlayerState.Dead)          => true,  // daño letal sí aplica

            // Desde TakingDamage — debe terminar la animación antes de actuar
            (PlayerState.TakingDamage, PlayerState.Moving)   => true,
            (PlayerState.TakingDamage, PlayerState.Dashing)  => false, // debe esperar
            (PlayerState.TakingDamage, PlayerState.Dead)      => true,

            // Dead es terminal — no hay salida
            (PlayerState.Dead, _) => false,

            _ => false
        };
    }

    private void OnEnterState(PlayerState newState, PlayerState previous)
    {
        switch (newState)
        {
            case PlayerState.Moving:
                // Si venimos de daño, activamos la ventana de invulnerabilidad
                if (previous == PlayerState.TakingDamage)
                    StartPostDamageInvulnerability();
                break;

            case PlayerState.TakingDamage:
                // La animación de daño debe llamar a TransitionTo(Moving) al terminar.
                // Si no tienes animación aún, puedes llamarla con un Invoke temporal.
                break;

            case PlayerState.Dead:
                // Los demás sistemas escuchan OnStateChanged — no hacemos nada aquí.
                break;
        }
    }

    private void StartPostDamageInvulnerability()
    {
        if (_invulnerabilityCoroutine != null)
            StopCoroutine(_invulnerabilityCoroutine);
        _invulnerabilityCoroutine = StartCoroutine(PostDamageInvulnerabilityRoutine());
    }

    private IEnumerator PostDamageInvulnerabilityRoutine()
    {
        _postDamageInvulnerable = true;
        yield return new WaitForSeconds(postDamageInvulnerabilityTime);
        _postDamageInvulnerable = false;
    }
}