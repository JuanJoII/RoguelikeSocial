using System;
using UnityEngine;

[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private int maxLives = 3;

    [Header("Feedback — Daño")]
    [SerializeField] private SoundData damageSFX;
    [SerializeField] private VFXData damageVFX;

    [Tooltip("Punto de origen del VFX de daño. " +
             "Crea un GameObject hijo en el centro del torso del jugador.")]
    [SerializeField] private Transform vfxOrigin;

    [Header("Feedback — Muerte")]
    [SerializeField] private SoundData deathSFX;

    public static event Action<int, int> OnDamaged;
    public static event Action OnPlayerDead;

    public int CurrentLives { get; private set; }
    public int MaxLives => maxLives;

    private PlayerStateMachine _stateMachine;

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
        CurrentLives = maxLives;
        
    }

    private void Start()
    {
        // Inicializa los corazones del HUD con el maxLives configurado
        // Lo hacemos en Start con Invoke para asegurar que UIManager ya existe
        Invoke(nameof(InitializeUI), 0.1f);
    }

    private void InitializeUI()
    {
        UIManager.Instance?.InitializeHearts(maxLives);
    }

    public void TakeDamage(int amount)
    {
        if (_stateMachine.HasIFrames) return;
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.Dead) return;
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.TakingDamage) return;

        CurrentLives = Mathf.Max(0, CurrentLives - amount);
        OnDamaged?.Invoke(CurrentLives, maxLives);

        if (CurrentLives <= 0)
            Die();
        else
            HandleDamageFeedback();
    }

    public void Heal(int amount)
    {
        if (_stateMachine.CurrentState == PlayerStateMachine.PlayerState.Dead) return;
        CurrentLives = Mathf.Min(maxLives, CurrentLives + amount);
        OnDamaged?.Invoke(CurrentLives, maxLives);
    }

    private void HandleDamageFeedback()
    {
        _stateMachine.TransitionTo(PlayerStateMachine.PlayerState.TakingDamage);

        // SFX — posición del jugador para que sea espacial
        if (damageSFX != null)
            AudioManager.Instance.PlaySFX(damageSFX, transform.position);

        // VFX — usamos vfxOrigin si está asignado, si no el centro del jugador
        if (damageVFX != null)
        {
            Vector3 origin = vfxOrigin != null ? vfxOrigin.position : transform.position;
            VFXPool.Instance.PlayVFX(damageVFX, origin, transform.rotation);
        }
    }

    private void Die()
    {
        _stateMachine.TransitionTo(PlayerStateMachine.PlayerState.Dead);

        if (deathSFX != null)
            AudioManager.Instance.PlaySFX(deathSFX, transform.position);

        // El VFX de muerte puede ser más elaborado — lo disparamos
        // en la posición del jugador orientado hacia arriba para que
        // se vea bien desde la cámara top-down
        if (damageVFX != null)
        {
            Vector3 origin = vfxOrigin != null ? vfxOrigin.position : transform.position;
            VFXPool.Instance.PlayVFX(damageVFX, origin, Quaternion.identity);
        }

        OnPlayerDead?.Invoke();
    }
}