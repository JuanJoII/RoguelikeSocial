using UnityEngine;

/// <summary>
/// Comportamiento de un proyectil individual.
///
/// CICLO DE VIDA:
/// 1. WeaponSystem llama Initialize() al sacarlo del pool
/// 2. El proyectil se mueve en Update
/// 3. Al colisionar o exceder el rango, se devuelve al pool
///
/// DAÑO:
/// Solo aplica daño si el enemigo es del tipo correcto.
/// Si no lo es, reproduce el VFX de bala incorrecta y se devuelve
/// al pool sin hacer daño — feedback claro para el jugador.
///
/// IMPORTANTE — EnemyTypeIdentifier:
/// Para que el proyectil sepa el tipo de un enemigo, ese enemigo
/// debe tener el componente EnemyTypeIdentifier en su collider.
/// Lo implementamos cuando hagamos el EnemyAI.
/// </summary>
public class ProjectileBase : MonoBehaviour
{
    private ProjectileConfig _config;
    private EnemyType _targetType;
    private Vector3 _spawnPosition;
    private bool _initialized;

    private void OnEnable()
    {
        // Al salir del pool guardamos la posición de origen para
        // calcular la distancia recorrida
        _spawnPosition = transform.position;
        _initialized = false;
    }

    /// <summary>
    /// Llamado por WeaponSystem inmediatamente después de sacar del pool.
    /// Sin esta llamada el proyectil no se mueve ni hace daño.
    /// </summary>
    public void Initialize(ProjectileConfig config, EnemyType targetType)
    {
        _config = config;
        _targetType = targetType;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        // Movimiento: siempre hacia adelante en la dirección que fue orientado
        transform.position += transform.forward * _config.speed * Time.deltaTime;

        // Rango máximo: si el proyectil viajó demasiado, lo devolvemos al pool
        // Esto evita que proyectiles perdidos vivan indefinidamente
        float distanceTraveled = Vector3.Distance(_spawnPosition, transform.position);
        if (distanceTraveled >= _config.maxRange)
            ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_initialized) return;

        // Si golpeamos algo que no es enemigo (pared, suelo, obstáculo)
        // simplemente nos devolvemos al pool sin hacer daño
        if (!other.TryGetComponent<EnemyTypeIdentifier>(out var typeId))
        {
            ReturnToPool();
            return;
        }

        if (typeId.EnemyType == _targetType)
        {
            if (other.TryGetComponent<EnemyAI>(out var enemy))
                enemy.TakeDamage(_config.damage);

            VFXPool.Instance.PlayVFX(_config.hitVFX, transform.position, Quaternion.identity);
            AudioManager.Instance.PlaySFX(_config.hitSound, transform.position);
        }
        else
        {
            VFXPool.Instance.PlayVFX(_config.wrongTypeVFX, transform.position, Quaternion.identity);
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        _initialized = false;
        ObjectPool.Instance.ReturnProjectile(gameObject, _targetType);
    }
}
