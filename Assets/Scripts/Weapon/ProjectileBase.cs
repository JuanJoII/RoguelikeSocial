using UnityEngine;

/// <summary>
/// Comportamiento de un proyectil individual.
///
/// FIX DE POOL:
/// _inUse es el flag de custodia. Se activa cuando el pool entrega
/// el proyectil y se desactiva cuando se devuelve. Todos los callbacks
/// de física lo verifican primero. Esto evita el bug de doble retorno
/// causado por callbacks de física retrasados de Unity.
///
/// La distinción entre _inUse e _initialized es intencional:
/// _initialized = el proyectil tiene config válida y puede moverse
/// _inUse = el proyectil está bajo custodia del WeaponSystem, no del pool
/// </summary>
public class ProjectileBase : MonoBehaviour
{
    private ProjectileConfig _config;
    private EnemyType _targetType;
    private Vector3 _spawnPosition;

    // Flag de inicialización — controla si el proyectil se mueve
    private bool _initialized;

    // Flag de custodia — controla si el proyectil puede ser retornado
    // Se setea desde el ObjectPool, no desde Initialize
    private bool _inUse;

    private void OnDisable()
    {
        // Cuando el pool desactiva el objeto, nos aseguramos
        // de que el estado quede limpio para la próxima vez
        _initialized = false;
        _inUse = false;
    }

    /// <summary>
    /// Llamado por ObjectPool justo ANTES de entregar el proyectil.
    /// Marca el proyectil como bajo custodia del WeaponSystem.
    /// Debe llamarse antes de Initialize para que los callbacks
    /// de física no puedan retornarlo mientras se configura.
    /// </summary>
    public void Acquire()
    {
        _inUse = true;
    }

    /// <summary>
    /// Llamado por WeaponSystem después de posicionar el proyectil.
    /// </summary>
    public void Initialize(ProjectileConfig config, EnemyType targetType)
    {
        _config = config;
        _targetType = targetType;
        _spawnPosition = transform.position;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || !_inUse) return;

        transform.position += transform.forward * _config.speed * Time.deltaTime;

        float distanceTraveled = Vector3.Distance(_spawnPosition, transform.position);
        if (distanceTraveled >= _config.maxRange)
            ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        // _inUse es la única verificación que importa aquí.
        // Si es false, este proyectil ya fue retornado al pool
        // y este callback es un residuo de física del frame anterior.
        if (!_inUse) return;

        // Ignoramos colisiones con otros proyectiles
        // (configura esto también en la Layer Collision Matrix)
        if (other.TryGetComponent<ProjectileBase>(out _)) return;

        // Si golpeamos algo que no es enemigo (pared, suelo, etc.)
        if (!other.TryGetComponent<EnemyTypeIdentifier>(out var typeId))
        {
            ReturnToPool();
            return;
        }

        // Enemigo del tipo correcto
        if (typeId.EnemyType == _targetType)
        {
            if (other.TryGetComponent<EnemyAI>(out var enemy))
                enemy.TakeDamage(_config.damage);

            if (_config.hitVFX != null)
                VFXPool.Instance.PlayVFX(_config.hitVFX, transform.position, Quaternion.identity);

            if (_config.hitSound != null)
                AudioManager.Instance.PlaySFX(_config.hitSound, transform.position);
        }
        else
        {
            // Tipo incorrecto — feedback visual sin daño
            if (_config.wrongTypeVFX != null)
                VFXPool.Instance.PlayVFX(
                    _config.wrongTypeVFX, transform.position, Quaternion.identity);
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        // Doble verificación — por si acaso dos callbacks
        // intentan retornar en el mismo frame
        if (!_inUse) return;

        _inUse = false;
        _initialized = false;

        ObjectPool.Instance.ReturnProjectile(gameObject, _targetType);
    }
}
