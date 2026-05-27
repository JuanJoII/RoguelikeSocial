using System.Collections;
using UnityEngine;

/// <summary>
/// Golem de Fuego — Proyectiles en abanico.
///
/// COMPORTAMIENTO:
/// Mantiene distancia media con el jugador.
/// Cuando está en rango de disparo, se detiene y dispara
/// un abanico de proyectiles. La animación es rápida.
/// El jugador solo puede esquivar con i-frames del dash.
///
/// ABANICO:
/// Dispara N proyectiles distribuidos uniformemente en un ángulo.
/// Todos salen desde el mismo punto (muzzlePoint) en el mismo frame
/// cuando el Animation Event los activa.
/// </summary>
public class GolemFireBoss : BossBase
{
    [Header("Rango de disparo")]
    [SerializeField] private float minAttackRange = 4f;  // no se acerca más de esto
    [SerializeField] private float maxAttackRange = 12f; // fuera de esto persigue

    [Header("Abanico de proyectiles")]
    [SerializeField] private int   projectileCount = 5;
    [SerializeField] private float spreadAngle = 60f;    // ángulo total del abanico
    [SerializeField] private GameObject projectilePrefab; // prefab de bola de fuego
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private int   projectileDamage = 1;

    [Header("Feedback")]
    [SerializeField] private VFXData muzzleVFX;
    [SerializeField] private SoundData shootSound;

    [Header("Cooldown")]
    [SerializeField] private float attackCooldown = 2.5f;
    private float _cooldownTimer;

    protected override void Update()
    {
        base.Update();
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
    }

    protected override void HandleChasing()
    {
        if (Player == null) return;

        float distance = Vector3.Distance(transform.position, Player.position);
        Vector3 direction = (Player.position - transform.position).normalized;
        direction.y = 0f;

        // Mantiene distancia media — ni muy cerca ni muy lejos
        if (distance > maxAttackRange)
        {
            // Persigue normalmente
            Rb.MovePosition(Rb.position + direction * moveSpeed * Time.deltaTime);
        }
        else if (distance < minAttackRange)
        {
            // Se aleja del jugador para mantener distancia de disparo
            Rb.MovePosition(Rb.position - direction * moveSpeed * Time.deltaTime);
        }
        // En el rango medio: se queda quieto esperando atacar

        // Siempre mira al jugador
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                360f * Time.deltaTime);
    }

    protected override bool ShouldAttack()
    {
        if (_cooldownTimer > 0f) return false;
        if (Player == null) return false;

        float distance = Vector3.Distance(transform.position, Player.position);
        return distance <= maxAttackRange && distance >= minAttackRange;
    }

    protected override void HandleAttacking()
    {
        // Se queda quieto mientras dispara
        // La animación dura poco — retorna a Chasing por Animation Event
    }

    /// <summary>
    /// Animation Event — dispara el abanico de proyectiles.
    /// Puede llamarse múltiples veces en un clip para ráfagas.
    /// </summary>
    public override void ExecuteAttack()
    {
        if (muzzlePoint == null || Player == null) return;

        VFXPool.Instance.PlayVFX(muzzleVFX, muzzlePoint.position, muzzlePoint.rotation);
        AudioManager.Instance.PlaySFX(shootSound, transform.position);

        // Dirección aplanada en Y — el proyectil viaja paralelo al suelo
        // independientemente de la altura del muzzlePoint o del jugador
        Vector3 toPlayer = Player.position - muzzlePoint.position;
        toPlayer.y = 0f;
        Vector3 baseDirection = toPlayer.normalized;

        float angleStep = projectileCount > 1
            ? spreadAngle / (projectileCount - 1)
            : 0f;

        float startAngle = -spreadAngle * 0.5f;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;

            // Spawneamos el proyectil a la misma altura del jugador
            // para que no tenga que corregir su trayectoria en Y
            Vector3 spawnPos = new Vector3(
                muzzlePoint.position.x,
                Player.position.y,
                muzzlePoint.position.z);

            GameObject proj = Instantiate(
                projectilePrefab, spawnPos, Quaternion.LookRotation(direction));

            if (proj.TryGetComponent<BossProjectile>(out var bp))
                bp.Initialize(direction, projectileSpeed, projectileDamage);
        }

        _cooldownTimer = attackCooldown;
    }
    public void OnAttackAnimationEnd()
    {
        ReturnToChasing();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minAttackRange);
        Gizmos.DrawWireSphere(transform.position, maxAttackRange);
    }
}