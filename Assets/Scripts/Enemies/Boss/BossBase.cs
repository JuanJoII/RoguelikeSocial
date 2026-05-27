using System;
using UnityEngine;

/// <summary>
/// Clase base abstracta para todos los jefes.
/// Define el contrato común: vida, daño, muerte, estados.
///
/// HERENCIA:
/// Cada jefe hereda de esta clase e implementa los métodos abstractos.
/// BossBase no sabe cómo ataca cada jefe — solo sabe que puede atacar.
///
/// ESTADOS:
/// Idle     → el boss existe pero aún no ha comenzado el combate
/// Chasing  → persigue al jugador
/// Attacking → ejecutando su ataque específico
/// Dead     → murió, esperando animación de muerte
/// </summary>
public abstract class BossBase : MonoBehaviour
{
    // Evento desacoplado — RoomManager lo escucha
    // El boss no sabe que RoomManager existe
    public static event Action<BossBase> OnBossDead;

    public enum BossState { Idle, Chasing, Attacking, Dead }

    [Header("Stats — Base")]
    [SerializeField] protected int maxHealth = 100;
    [SerializeField] protected float moveSpeed = 3f;

    [Tooltip("Distancia a la que el boss detecta al jugador e inicia el combate.")]
    [SerializeField] protected float detectionRange = 15f;

    [Header("Feedback — Base")]
    [SerializeField] protected SoundData hurtSound;
    [SerializeField] protected SoundData deathSound;
    [SerializeField] protected VFXData deathVFX;

    // Estado
    public BossState CurrentState { get; protected set; }
    protected int CurrentHealth;
    protected Transform Player;
    protected Animator Anim;
    protected Rigidbody Rb;

    // Hashes de Animator — en la base porque todos los bosses los usan
    protected static readonly int AnimChasing   = Animator.StringToHash("IsChasing");
    protected static readonly int AnimAttacking = Animator.StringToHash("IsAttacking");
    protected static readonly int AnimDead      = Animator.StringToHash("IsDead");

    // ════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ════════════════════════════════════════════════════════════════════

    protected virtual void Awake()
    {
        Anim = GetComponent<Animator>();
        Rb   = GetComponent<Rigidbody>();
        CurrentHealth = maxHealth;
        CurrentState  = BossState.Idle;
    }

    /// <summary>
    /// Llamado por RoomManager cuando el jugador entra a la sala de boss.
    /// El boss no se activa solo — espera que el sistema lo inicie.
    /// </summary>
    public virtual void Initialize(Transform player)
    {
        Player = player;
        CurrentState = BossState.Chasing;
        Anim.SetBool(AnimChasing, true);

        // Notificamos al UIManager para que muestre la barra de vida
        UIManager.Instance.InitializeBossHealth(maxHealth);
    }

    // ════════════════════════════════════════════════════════════════════
    // UPDATE — MÁQUINA DE ESTADOS
    // ════════════════════════════════════════════════════════════════════

    protected virtual void Update()
    {
        if (CurrentState == BossState.Dead ||
            CurrentState == BossState.Idle) return;

        UpdateState();
    }

    private void UpdateState()
    {
        switch (CurrentState)
        {
            case BossState.Chasing:
                HandleChasing();
                CheckAttackCondition();
                break;

            case BossState.Attacking:
                // Cada hijo maneja su propio estado de ataque
                HandleAttacking();
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // MOVIMIENTO BASE
    // Los hijos pueden sobreescribir si necesitan movimiento especial
    // ════════════════════════════════════════════════════════════════════

    protected virtual void HandleChasing()
    {
        if (Player == null) return;

        Vector3 direction = (Player.position - transform.position).normalized;
        direction.y = 0f;

        Rb.MovePosition(Rb.position + direction * moveSpeed * Time.deltaTime);

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                360f * Time.deltaTime);
    }

    // ════════════════════════════════════════════════════════════════════
    // MÉTODOS ABSTRACTOS — CONTRATO QUE CADA HIJO DEBE IMPLEMENTAR
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ¿El boss debe atacar ahora? Cada hijo define su condición.
    /// Ej: el Golem de Roble ataca cuando está cerca,
    ///     el Golem de Fuego ataca a distancia.
    /// </summary>
    protected abstract bool ShouldAttack();

    /// <summary>
    /// Qué hace el boss durante el estado Attacking.
    /// Puede ser moverse mientras dispara, quedarse quieto, etc.
    /// </summary>
    protected abstract void HandleAttacking();

    /// <summary>
    /// Ejecuta el ataque específico del boss.
    /// Llamado desde Animation Events en el clip de ataque.
    /// </summary>
    public abstract void ExecuteAttack();

    // ════════════════════════════════════════════════════════════════════
    // LÓGICA COMPARTIDA
    // ════════════════════════════════════════════════════════════════════

    private void CheckAttackCondition()
    {
        if (!ShouldAttack()) return;

        CurrentState = BossState.Attacking;
        Anim.SetBool(AnimChasing, false);
        Anim.SetBool(AnimAttacking, true);
    }

    protected void ReturnToChasing()
    {
        if (CurrentState == BossState.Dead) return;

        CurrentState = BossState.Chasing;
        Anim.SetBool(AnimAttacking, false);
        Anim.SetBool(AnimChasing, true);
    }

    public virtual void TakeDamage(int amount)
    {
        if (CurrentState == BossState.Dead) return;

        CurrentHealth -= amount;
        UIManager.Instance.UpdateBossHealth(CurrentHealth);
        AudioManager.Instance.PlaySFX(hurtSound, transform.position);

        if (CurrentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        CurrentState = BossState.Dead;

        Anim.SetBool(AnimChasing,   false);
        Anim.SetBool(AnimAttacking, false);
        Anim.SetBool(AnimDead,      true);

        if (Rb != null)
        {
            Rb.linearVelocity = Vector3.zero;
            Rb.isKinematic = true;
        }

        VFXPool.Instance.PlayVFX(deathVFX, transform.position, Quaternion.identity);
        AudioManager.Instance.PlaySFX(deathSound, transform.position);

        // Notificamos — RoomManager escucha esto
        OnBossDead?.Invoke(this);
    }

    // Animation Event — llamado al final del clip de muerte
    // para destruir o desactivar el boss
    public virtual void OnDeathAnimationEnd()
    {
        gameObject.SetActive(false);
    }
}