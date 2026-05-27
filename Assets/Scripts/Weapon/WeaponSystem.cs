using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja el disparo automático con auto-aim y cambio de tipo de bala.
///
/// RESPONSABILIDADES:
/// - Detectar el enemigo más cercano dentro del cono de visión
/// - Filtrar por tipo de enemigo según la bala activa
/// - Disparar automáticamente respetando la cadencia configurada
/// - Notificar al AnimatorManager cuándo hay un target activo
///
/// AUTO-AIM:
/// No snapea la rotación del jugador — solo selecciona el target.
/// El jugador rota con el joystick derecho y el sistema dispara
/// al enemigo más cercano que esté dentro del cono resultante.
///
/// DETECCIÓN:
/// OverlapSphere obtiene todos los colliders en rango.
/// Luego filtramos por ángulo y por tipo de enemigo.
/// Seleccionamos el más cercano entre los que pasan el filtro.
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerAnimatorManager))]
public class WeaponSystem : MonoBehaviour
{
    // Evento para que el UIManager sepa qué bala está activa
    public static event Action<EnemyType> OnBulletTypeChanged;

    [Header("Detección")]
    [Tooltip("Radio de la esfera de detección de enemigos.")]
    [SerializeField] private float detectionRadius = 10f;

    [Tooltip("Ángulo total del cono de apuntado en grados. " +
             "90 = 45 grados a cada lado del frente del jugador.")]
    [Range(10f, 180f)]
    [SerializeField] private float coneAngle = 90f;

    [Tooltip("Layer mask de los enemigos. Configura en Inspector " +
             "para que OverlapSphere no evalúe objetos irrelevantes.")]
    [SerializeField] private LayerMask enemyLayerMask;

    [Header("Cadencia")]
    [Tooltip("Segundos entre disparo y disparo.")]
    [SerializeField] private float fireRate = 0.25f;

    [Header("Proyectiles")]
    [SerializeField] private ProjectileConfig groundProjectileConfig;
    [SerializeField] private ProjectileConfig airProjectileConfig;

    [Header("Feedback")]
    [SerializeField] private Transform muzzlePoint; // punto de origen del proyectil
    [SerializeField] private SoundData shootSoundGround;
    [SerializeField] private SoundData shootSoundAir;
    [SerializeField] private VFXData muzzleVFX;

    // Referencias
    private PlayerStateMachine _stateMachine;
    private PlayerAnimatorManager _animatorManager;

    // Estado interno
    private EnemyType _activeBulletType = EnemyType.Ground;
    private float _fireCooldown;
    private Transform _currentTarget;

    public EnemyType ActiveBulletType => _activeBulletType;

    private void Awake()
    {
        _stateMachine   = GetComponent<PlayerStateMachine>();
        _animatorManager = GetComponent<PlayerAnimatorManager>();
    }

    private void Update()
    {
        if (!_stateMachine.CanShoot)
        {
            // Si no podemos disparar, aseguramos que el animator
            // sepa que no hay target activo
            _animatorManager.SetAiming(false);
            _currentTarget = null;
            return;
        }

        _currentTarget = FindBestTarget();

        // El animator activa la capa de apuntado solo cuando hay target
        _animatorManager.SetAiming(_currentTarget != null);

        // Reducimos el cooldown siempre, haya target o no
        // Así cuando aparece un enemigo disparamos de inmediato
        // en lugar de esperar el primer ciclo de cooldown
        if (_fireCooldown > 0f)
            _fireCooldown -= Time.deltaTime;

        if (_currentTarget != null && _fireCooldown <= 0f)
            Shoot();
    }

    // ── Detección ─────────────────────────────────────────────────────────

    private Transform FindBestTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, detectionRadius, enemyLayerMask);

        if (colliders.Length == 0) return null;

        Transform bestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            // Verificamos si está dentro del cono
            Vector3 dirToEnemy = (col.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToEnemy);
            if (angle > coneAngle * 0.5f) continue;

            // ¿Es un boss? Cualquier bala lo detecta como target válido
            if (col.TryGetComponent<BossBase>(out _))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = col.transform;
                }
                continue;
            }

            // ¿Es un enemigo común del tipo correcto?
            if (!col.TryGetComponent<EnemyTypeIdentifier>(out var typeId)) continue;
            if (typeId.EnemyType != _activeBulletType) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = col.transform;
            }
        }

        return bestTarget;
    }

    // ── Disparo ───────────────────────────────────────────────────────────

    private void Shoot()
    {
        _fireCooldown = fireRate;

        ProjectileConfig config = _activeBulletType == EnemyType.Ground
            ? groundProjectileConfig
            : airProjectileConfig;

        // Pedimos el proyectil al pool
        // ObjectPool lo implementamos cuando hagamos los enemigos
        GameObject projectileObj = ObjectPool.Instance.GetProjectile(_activeBulletType);
        if (projectileObj == null) return;

        // Posicionamos y orientamos hacia el target
        projectileObj.transform.position = muzzlePoint.position;
        Vector3 directionToTarget = (_currentTarget.position - muzzlePoint.position).normalized;
        projectileObj.transform.rotation = Quaternion.LookRotation(directionToTarget);

        // Inicializamos con la configuración correcta
        if (projectileObj.TryGetComponent<ProjectileBase>(out var projectile))
            projectile.Initialize(config, _activeBulletType);

        // Feedback
        SoundData shootSound = _activeBulletType == EnemyType.Ground
            ? shootSoundGround : shootSoundAir;

        AudioManager.Instance.PlaySFX(shootSound, muzzlePoint.position);
        VFXPool.Instance.PlayVFX(muzzleVFX, muzzlePoint.position, muzzlePoint.rotation);
    }

    // ── Cambio de bala ────────────────────────────────────────────────────

    public void OnSwitchBullet(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        _activeBulletType = _activeBulletType == EnemyType.Ground
            ? EnemyType.Flying
            : EnemyType.Ground;

        // Reseteamos el target para que el sistema busque uno nuevo
        // del tipo correcto en el próximo frame
        _currentTarget = null;

        OnBulletTypeChanged?.Invoke(_activeBulletType);

        // Feedback visual/sonoro del cambio
        // SoundData y VFXData del cambio de bala los puedes agregar
        // como campos serializados si los necesitas
    }

    // ── Debug visual en editor ────────────────────────────────────────────

    /// <summary>
    /// Dibuja el cono de detección en la Scene View.
    /// Solo visible en el editor, cero costo en build.
    /// Útil para calibrar coneAngle y detectionRadius en el Inspector.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Dibuja los bordes del cono
        Vector3 leftBound = Quaternion.Euler(0, -coneAngle * 0.5f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, coneAngle * 0.5f, 0) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, leftBound * detectionRadius);
        Gizmos.DrawRay(transform.position, rightBound * detectionRadius);
        Gizmos.DrawRay(transform.position, transform.forward * detectionRadius);

        // Dibuja una línea al target actual si existe
        if (_currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _currentTarget.position);
        }
    }
    
}
