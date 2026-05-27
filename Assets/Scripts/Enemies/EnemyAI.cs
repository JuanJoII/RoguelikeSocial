using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Comportamiento de un enemigo individual.
///
/// MOVIMIENTO — Steering Behaviors:
/// No usa NavMesh. En su lugar suma cuatro vectores cada frame:
///   1. Hacia el jugador (chase)
///   2. Hacia el centro del grupo (cohesión)
///   3. Alejarse de vecinos cercanos (separación)
///   4. Alejarse de paredes detectadas con raycast (wall avoidance)
///
/// Este enfoque es O(n) por vecinos cercanos, no O(n²),
/// porque solo chequeamos enemigos dentro del separationRadius
/// con un OverlapSphere pequeño, no todos contra todos.
///
/// ESTADOS:
/// Chasing → comportamiento normal de horda
/// Jumping → saltando sobre un enemigo bloqueante
/// Dead    → esperando ser devuelto al pool
///
/// CONTACTO CON JUGADOR:
/// OnTriggerStay aplica daño continuamente con un intervalo
/// para evitar que el jugador reciba múltiples golpes por frame.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyAI : MonoBehaviour
{
    // Evento global — RoomManager lo escucha para el contador
    public static event Action<EnemyAI> OnEnemyDied;

    private enum EnemyState { Chasing, Jumping, Dead }

    // Configuración — asignada por EnemyGroup al spawnear
    private EnemyDataSO _data;
    private Transform _player;
    private EnemyGroup _group;

    // Estado
    private EnemyState _currentState;
    private int _currentHealth;
    private float _jumpCooldownTimer;
    private float _damageCooldown;
    private const float DamageInterval = 5f; // segundos entre golpes de contacto

    // Layer mask para separación — solo detecta otros enemigos
    private static int _enemyLayer;
    private Rigidbody _rb;

    private void Awake()
    {
        _enemyLayer = LayerMask.GetMask("Enemy");
        _rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Llamado por EnemyGroup al sacar del pool.
    /// Sin Initialize el enemigo no hace nada — seguridad por diseño.
    /// </summary>
    public void Initialize(EnemyDataSO data, Transform player, EnemyGroup group)
    {
        _data = data;
        _player = player;
        _group = group;

        _currentHealth = data.maxHealth;
        _currentState = EnemyState.Chasing;
        _jumpCooldownTimer = 0f;
        _damageCooldown = 0f;
    }

    private void Update()
    {
        if (_currentState == EnemyState.Dead) return;
        if (_data == null || _player == null) return;

        if (_damageCooldown > 0f)
            _damageCooldown -= Time.deltaTime;

        if (_jumpCooldownTimer > 0f)
            _jumpCooldownTimer -= Time.deltaTime;

        if (_currentState == EnemyState.Chasing)
            HandleChasing();
    }

    // ── Steering Behaviors ────────────────────────────────────────────────

    private void HandleChasing()
    {
        Vector3 steering = CalculateSteering();

        // Normalizamos y aplicamos velocidad
        // Si steering es zero (fuerzas se cancelan perfectamente),
        // mantenemos la dirección anterior para evitar que el enemigo
        // se congele en medio de una oleada
        if (steering.magnitude > 0.01f)
        {
            Vector3 movement = steering.normalized * _data.moveSpeed * Time.deltaTime;
            _rb.MovePosition(_rb.position + movement);

            // Rotamos hacia donde nos movemos para que la animación sea coherente
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(steering.normalized, Vector3.up),
                360f * Time.deltaTime);
        }
    }

    private Vector3 CalculateSteering()
    {
        Vector3 chase     = CalculateChase()     * _data.chaseWeight;
        Vector3 cohesion  = CalculateCohesion()  * _data.cohesionWeight;
        Vector3 separation = CalculateSeparation() * _data.separationWeight;
        Vector3 wallAvoid = CalculateWallAvoidance() * _data.wallAvoidWeight;

        return chase + cohesion + separation + wallAvoid;
    }

    private Vector3 CalculateChase()
    {
        return (_player.position - transform.position).normalized;
    }

    private Vector3 CalculateCohesion()
    {
        // Nos movemos hacia el centro del grupo para mantener
        // la sensación de oleada unida
        Vector3 toCenter = _group.GroupCenter - transform.position;
        return toCenter.magnitude > 0.1f ? toCenter.normalized : Vector3.zero;
    }

    private Vector3 CalculateSeparation()
    {
        // OverlapSphere pequeño — solo vecinos inmediatos
        // Mucho más barato que checar todos los enemigos en escena
        Collider[] neighbors = Physics.OverlapSphere(
            transform.position, _data.separationRadius, _enemyLayer);

        if (neighbors.Length <= 1) return Vector3.zero; // solo yo mismo

        Vector3 separationForce = Vector3.zero;
        int count = 0;

        foreach (Collider neighbor in neighbors)
        {
            if (neighbor.gameObject == gameObject) continue;

            Vector3 awayFromNeighbor = transform.position - neighbor.transform.position;

            // La fuerza es inversamente proporcional a la distancia:
            // más cerca = más fuerte la separación
            float distance = awayFromNeighbor.magnitude;
            if (distance > 0.001f)
            {
                separationForce += awayFromNeighbor.normalized / distance;
                count++;
            }

            // Si hay un vecino muy cerca y podemos saltar, consideramos el salto
            if (distance < _data.separationRadius * 0.5f)
                TryJump();
        }

        return count > 0 ? (separationForce / count).normalized : Vector3.zero;
    }

    private Vector3 CalculateWallAvoidance()
    {
        // Lanzamos raycast hacia adelante y en diagonal
        // para detectar paredes con anticipación
        Vector3[] directions =
        {
            transform.forward,
            Quaternion.Euler(0, 45, 0)  * transform.forward,
            Quaternion.Euler(0, -45, 0) * transform.forward
        };

        Vector3 avoidance = Vector3.zero;

        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(transform.position, dir,
                out RaycastHit hit, _data.wallDetectionDistance,
                ~_enemyLayer)) // todo menos enemigos
            {
                // Nos alejamos de la normal de la pared
                // La fuerza aumenta cuanto más cerca estamos
                float proximity = 1f - (hit.distance / _data.wallDetectionDistance);
                avoidance += hit.normal * proximity;
            }
        }

        return avoidance;
    }

    // ── Salto sobre enemigos ──────────────────────────────────────────────

    private void TryJump()
    {
        if (_currentState != EnemyState.Chasing) return;
        if (_jumpCooldownTimer > 0f) return;
        if (UnityEngine.Random.value > _data.jumpProbability) return;

        StartCoroutine(JumpRoutine());
    }

    private IEnumerator JumpRoutine()
    {
        _currentState = EnemyState.Jumping;
        _jumpCooldownTimer = _data.jumpCooldown;

        // Desactivamos colisión con otros enemigos durante el salto
        // El jugador sigue pudiendo dañarnos — solo ignoramos a los
        // enemigos para cruzar por encima de ellos
        Physics.IgnoreLayerCollision(
            LayerMask.NameToLayer("Enemy"),
            LayerMask.NameToLayer("Enemy"), true);

        // Aquí el AnimatorManager del enemigo activaría la animación de salto
        // Por ahora dejamos un placeholder de tiempo
        // Cuando tengas el Animator del enemigo, reemplaza esto por un
        // Animation Event igual que hicimos con el dash del jugador
        yield return new WaitForSeconds(0.4f);

        Physics.IgnoreLayerCollision(
            LayerMask.NameToLayer("Enemy"),
            LayerMask.NameToLayer("Enemy"), false);

        if (_currentState != EnemyState.Dead)
            _currentState = EnemyState.Chasing;
    }

    // ── Daño y muerte ─────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (_currentState == EnemyState.Dead) return;

        _currentHealth -= amount;

        if (_currentHealth <= 0)
            Die();
        else
            AudioManager.Instance.PlaySFX(_data.hurtSound, transform.position);
    }

    private void Die()
    {
        _currentState = EnemyState.Dead;

        VFXPool.Instance.PlayVFX(_data.deathVFX, transform.position, Quaternion.identity);
        AudioManager.Instance.PlaySFX(_data.deathSound, transform.position);

        // Notificamos al grupo antes de devolver al pool
        // El grupo actualiza su lista y notifica al RoomManager
        _group?.ReportDeath(this);

        ObjectPool.Instance.ReturnEnemy(gameObject, _data.enemyType);
    }

    // ── Contacto con jugador ──────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (_currentState == EnemyState.Dead) return;
        if (_damageCooldown > 0f) return;

        if (other.TryGetComponent<PlayerHealth>(out var health))
        {
            health.TakeDamage(_data.contactDamage);
            _damageCooldown = DamageInterval;
        }
    }

    // ── Debug ─────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _data.separationRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position,
            transform.forward * _data.wallDetectionDistance);
    }
}