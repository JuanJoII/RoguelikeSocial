using System.Collections;
using UnityEngine;

/// <summary>
/// Golem de Roble — Ataque de área.
///
/// COMPORTAMIENTO:
/// Se acerca al jugador. Cuando está en rango de melee,
/// activa la animación de golpe al suelo. El daño se aplica
/// en el Animation Event a mitad del clip, no al inicio.
/// Esto da al jugador tiempo para esquivar con el dash.
///
/// TELEGRAMA:
/// Antes de atacar se detiene brevemente — señal visual de que
/// el ataque viene. El VFX de advertencia aparece en el suelo
/// para que el jugador sepa el área de peligro.
/// </summary>
public class GolemOakBoss : BossBase
{
    [Header("Ataque de Área")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float areaRadius = 4f;
    [SerializeField] private int   attackDamage = 2;

    [Tooltip("Tiempo que el boss se detiene antes de atacar. " +
             "El jugador usa este tiempo para esquivar.")]
    [SerializeField] private float telegraphDuration = 0.6f;

    [Header("Feedback de Ataque")]
    [SerializeField] private VFXData telegraphVFX;  // círculo en el suelo antes del golpe
    [SerializeField] private VFXData impactVFX;     // explosión al golpear
    [SerializeField] private SoundData attackSound;

    [Header("Layer")]
    [SerializeField] private LayerMask playerLayer;

    private bool _attackOnCooldown;
    [SerializeField] private float attackCooldown = 3f;
    private float _cooldownTimer;

    protected override void Update()
    {
        base.Update();

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    protected override bool ShouldAttack()
    {
        if (_attackOnCooldown) return false;
        if (_cooldownTimer > 0f) return false;
        if (Player == null) return false;

        float distance = Vector3.Distance(transform.position, Player.position);
        return distance <= attackRange;
    }

    protected override void HandleAttacking()
    {
        // El boss se queda quieto durante el ataque
        // El movimiento lo controla la animación
    }

    /// <summary>
    /// Animation Event — llamado a mitad del clip de ataque
    /// cuando el puño toca el suelo.
    /// </summary>
    public override void ExecuteAttack()
    {
        // VFX de impacto en el punto de golpe
        VFXPool.Instance.PlayVFX(impactVFX, transform.position, Quaternion.identity);
        AudioManager.Instance.PlaySFX(attackSound, transform.position);

        // Detectamos al jugador en el área
        Collider[] hits = Physics.OverlapSphere(transform.position, areaRadius, playerLayer);
        foreach (Collider hit in hits)
        {
            if (hit.TryGetComponent<PlayerHealth>(out var health))
                health.TakeDamage(attackDamage);
        }

        _cooldownTimer = attackCooldown;
    }

    /// <summary>
    /// Animation Event — llamado al final del clip de ataque.
    /// Volvemos a perseguir al jugador.
    /// </summary>
    public void OnAttackAnimationEnd()
    {
        _attackOnCooldown = false;
        ReturnToChasing();
    }

    // Muestra el área de ataque en Scene View para calibrar areaRadius
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, areaRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}