using System.Collections;
using UnityEngine;

/// <summary>
/// Alien — Movimiento rápido + ataque de contacto con telegrama.
///
/// COMPORTAMIENTO:
/// Se mueve rápido por la sala de forma errática para confundir.
/// Cuando decide atacar, se posiciona cerca del jugador,
/// la animación de ataque es lenta (telegrama claro),
/// y el daño se aplica al final de la animación.
///
/// MOVIMIENTO ERRÁTICO:
/// Cada X segundos cambia de objetivo de movimiento.
/// A veces va al jugador, a veces a un punto aleatorio de la sala.
/// Esto simula un movimiento impredecible sin pathfinding complejo.
/// </summary>
public class AlienBoss : BossBase
{
    [Header("Movimiento Errático")]
    [SerializeField] private float erraticMoveSpeed = 10f;

    [Tooltip("Cada cuántos segundos cambia el objetivo de movimiento.")]
    [SerializeField] private float directionChangeInterval = 1.2f;

    [Tooltip("Qué tan grande es la sala (radio desde el centro). " +
             "Para generar puntos aleatorios de movimiento.")]
    [SerializeField] private float roomRadius = 8f;

    [Tooltip("Probabilidad de ir al jugador vs punto aleatorio (0-1). " +
             "0 = siempre aleatorio, 1 = siempre al jugador.")]
    [Range(0f, 1f)]
    [SerializeField] private float chasePlayerProbability = 0.4f;

    [Header("Ataque de Contacto")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private int attackDamage = 2;
    [SerializeField] private SoundData attackSound;
    [SerializeField] private VFXData attackVFX;

    [Header("Cooldown")]
    [SerializeField] private float attackCooldown = 2f;
    private float _cooldownTimer;

    private Vector3 _currentMoveTarget;
    private float _directionChangeTimer;
    private bool _positioningToAttack;

    protected override void Awake()
    {
        base.Awake();
        moveSpeed = erraticMoveSpeed; // el alien usa su propia velocidad
    }

    public override void Initialize(Transform player)
    {
        base.Initialize(player);
        PickNewMoveTarget();
    }

    protected override void Update()
    {
        base.Update();

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        // Actualizamos el objetivo de movimiento errático
        if (CurrentState == BossState.Chasing)
        {
            _directionChangeTimer -= Time.deltaTime;
            if (_directionChangeTimer <= 0f)
                PickNewMoveTarget();
        }
    }

    protected override void HandleChasing()
    {
        if (Player == null) return;

        // Si va a atacar, se mueve directo al jugador
        Vector3 target = _positioningToAttack
            ? Player.position
            : _currentMoveTarget;

        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0f;

        Rb.MovePosition(Rb.position + direction * erraticMoveSpeed * Time.deltaTime);

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                720f * Time.deltaTime); // rota más rápido para verse ágil
    }

    protected override bool ShouldAttack()
    {
        if (_cooldownTimer > 0f) return false;
        if (Player == null) return false;

        float distance = Vector3.Distance(transform.position, Player.position);

        // Cuando está cerca, decide atacar y se posiciona
        if (distance <= attackRange)
        {
            _positioningToAttack = false;
            return true;
        }

        // Si el cooldown terminó, empieza a posicionarse para atacar
        if (_cooldownTimer <= 0f)
            _positioningToAttack = true;

        return false;
    }

    protected override void HandleAttacking()
    {
        // Se queda quieto — la animación de carga es el telegrama
        Rb.linearVelocity = Vector3.zero;
    }

    /// <summary>
    /// Animation Event — llamado al FINAL del clip de ataque (animación lenta).
    /// El jugador tuvo tiempo de esquivar con el dash durante la carga.
    /// </summary>
    public override void ExecuteAttack()
    {
        AudioManager.Instance.PlaySFX(attackSound, transform.position);

        if (Player == null) return;

        float distance = Vector3.Distance(transform.position, Player.position);
        if (distance <= attackRange)
        {
            if (Player.TryGetComponent<PlayerHealth>(out var health))
            {
                health.TakeDamage(attackDamage);
                VFXPool.Instance.PlayVFX(attackVFX, Player.position, Quaternion.identity);
            }
        }
        // Si el jugador esquivó con el dash, ya no está en rango — el ataque falla
    }

    public void OnAttackAnimationEnd()
    {
        _cooldownTimer = attackCooldown;
        _positioningToAttack = false;
        PickNewMoveTarget();
        ReturnToChasing();
    }

    private void PickNewMoveTarget()
    {
        _directionChangeTimer = directionChangeInterval;

        // Decide si perseguir al jugador o ir a un punto aleatorio
        bool chasePlayer = Random.value < chasePlayerProbability;

        if (chasePlayer && Player != null)
        {
            _currentMoveTarget = Player.position;
        }
        else
        {
            // Punto aleatorio dentro del radio de la sala
            Vector2 randomCircle = Random.insideUnitCircle * roomRadius;
            _currentMoveTarget = new Vector3(
                transform.position.x + randomCircle.x,
                transform.position.y,
                transform.position.z + randomCircle.y);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, roomRadius);

        // Muestra el objetivo actual de movimiento
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_currentMoveTarget, 0.3f);
    }
}