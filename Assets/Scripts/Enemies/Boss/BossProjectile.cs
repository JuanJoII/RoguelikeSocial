using UnityEngine;

/// <summary>
/// Proyectil del boss — más simple que ProjectileBase.
/// No usa ObjectPool porque son pocos y esporádicos.
/// Se destruye al impactar o al superar el rango máximo.
/// </summary>
public class BossProjectile : MonoBehaviour
{
    [SerializeField] private float maxRange = 20f;
    [SerializeField] private VFXData hitVFX;
    [SerializeField] private SoundData hitSound;

    private Vector3 _direction;
    private float _speed;
    private int _damage;
    private Vector3 _spawnPosition;
    private bool _initialized;

    public void Initialize(Vector3 direction, float speed, int damage)
    {
        _direction     = direction;
        _speed         = speed;
        _damage        = damage;
        _spawnPosition = transform.position;
        _initialized   = true;
    }

    private void Update()
    {
        if (!_initialized) return;

        transform.position += _direction * _speed * Time.deltaTime;

        if (Vector3.Distance(_spawnPosition, transform.position) >= maxRange)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_initialized) return;

        if (other.TryGetComponent<PlayerHealth>(out var health))
        {
            health.TakeDamage(_damage);

            if (hitVFX != null)
                VFXPool.Instance.PlayVFX(hitVFX, transform.position, Quaternion.identity);
            if (hitSound != null)
                AudioManager.Instance.PlaySFX(hitSound, transform.position);

            Destroy(gameObject);
        }
        else if (!other.TryGetComponent<BossBase>(out _))
        {
            // Golpeó una pared u obstáculo — se destruye
            Destroy(gameObject);
        }
    }
}