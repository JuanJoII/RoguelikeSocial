using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool genérico para proyectiles y enemigos.
/// Evita Instantiate/Destroy en runtime, eliminando GC spikes
/// en combate con oleadas de enemigos.
///
/// CATEGORÍAS:
/// - Proyectiles terrestres y aéreos (usados por WeaponSystem)
/// - Enemigos terrestres y aéreos (usados por WaveManager)
///
/// CÓMO FUNCIONA:
/// Al inicializarse pre-instancia N objetos de cada tipo,
/// los desactiva y los mete en su cola.
/// Get() saca uno de la cola y lo activa.
/// Return() lo desactiva y lo devuelve a la cola.
/// Si la cola está vacía, instancia uno nuevo y avisa con un warning
/// para que sepas que debes aumentar el tamaño del pool.
///
/// IMPORTANTE — SETUP EN INSPECTOR:
/// Asigna los prefabs y los tamaños iniciales.
/// Los prefabs de proyectil deben tener ProjectileBase.
/// Los prefabs de enemigo deben tener EnemyAI y EnemyTypeIdentifier.
/// </summary>
public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance { get; private set; }

    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        [Tooltip("Cantidad pre-instanciada al inicio. " +
                 "Ajusta según el máximo de objetos simultáneos esperados.")]
        public int initialSize = 20;
    }

    [Header("Proyectiles")]
    [SerializeField] private PoolConfig groundProjectilePool;
    [SerializeField] private PoolConfig airProjectilePool;

    [Header("Enemigos")]
    [SerializeField] private PoolConfig groundEnemyPool;
    [SerializeField] private PoolConfig airEnemyPool;

    // Colas separadas por tipo — nunca se mezclan
    private Queue<GameObject> _groundProjectiles;
    private Queue<GameObject> _airProjectiles;
    private Queue<GameObject> _groundEnemies;
    private Queue<GameObject> _airEnemies;

    // Carpetas en jerarquía para mantener la escena limpia
    private Transform _projectileContainer;
    private Transform _enemyContainer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateContainers();
        InitializePools();
    }

    private void CreateContainers()
    {
        // Agrupa los objetos del pool en la jerarquía para no
        // ensuciar la raíz de la escena con decenas de GameObjects
        _projectileContainer = new GameObject("[Pool] Projectiles").transform;
        _projectileContainer.SetParent(transform);

        _enemyContainer = new GameObject("[Pool] Enemies").transform;
        _enemyContainer.SetParent(transform);
    }

    private void InitializePools()
    {
        _groundProjectiles = CreatePool(groundProjectilePool, _projectileContainer);
        _airProjectiles    = CreatePool(airProjectilePool,    _projectileContainer);
        _groundEnemies     = CreatePool(groundEnemyPool,      _enemyContainer);
        _airEnemies        = CreatePool(airEnemyPool,         _enemyContainer);
    }

    private Queue<GameObject> CreatePool(PoolConfig config, Transform container)
    {
        var queue = new Queue<GameObject>(config.initialSize);

        if (config.prefab == null)
        {
            Debug.LogError($"[ObjectPool] Prefab no asignado en {name}. " +
                           "Asigna los prefabs en el Inspector.");
            return queue;
        }

        for (int i = 0; i < config.initialSize; i++)
        {
            GameObject obj = Instantiate(config.prefab, container);
            obj.SetActive(false);
            queue.Enqueue(obj);
        }

        return queue;
    }

    // ── API Proyectiles ───────────────────────────────────────────────────

    /// <summary>
    /// Solicita un proyectil del tipo indicado.
    /// El WeaponSystem lo posiciona e inicializa después de llamar esto.
    /// </summary>

    public GameObject GetProjectile(EnemyType targetType)
    {
        return targetType == EnemyType.Ground
            ? GetFromQueue(_groundProjectiles, groundProjectilePool,
                _projectileContainer, "GroundProjectile")
            : GetFromQueue(_airProjectiles, airProjectilePool,
                _projectileContainer, "AirProjectile");
    }

    /// <summary>
    /// Devuelve un proyectil al pool.
    /// ProjectileBase llama esto cuando colisiona o supera su rango máximo.
    /// </summary>
    public void ReturnProjectile(GameObject obj, EnemyType targetType)
    {
        obj.SetActive(false);

        if (targetType == EnemyType.Ground)
            _groundProjectiles.Enqueue(obj);
        else
            _airProjectiles.Enqueue(obj);
    }

    // ── API Enemigos ──────────────────────────────────────────────────────

    /// <summary>
    /// Solicita un enemigo del tipo indicado.
    /// WaveManager lo posiciona y asigna a un EnemyGroup después.
    /// </summary>
    public GameObject GetEnemy(EnemyType type)
    {
        return type == EnemyType.Ground
            ? GetFromQueue(_groundEnemies, groundEnemyPool, _enemyContainer, "GroundEnemy")
            : GetFromQueue(_airEnemies,    airEnemyPool,    _enemyContainer, "AirEnemy");
    }

    /// <summary>
    /// Devuelve un enemigo al pool.
    /// EnemyAI llama esto al morir, después de reproducir sus efectos.
    /// </summary>
    public void ReturnEnemy(GameObject obj, EnemyType type)
    {
        obj.SetActive(false);

        if (type == EnemyType.Ground)
            _groundEnemies.Enqueue(obj);
        else
            _airEnemies.Enqueue(obj);
    }

    // ── Lógica interna ────────────────────────────────────────────────────

private GameObject GetFromQueue(Queue<GameObject> queue, PoolConfig config,
                                Transform container, string label)
{
    // Limpiamos referencias null y objetos que
    // quedaron en estado zombie (activos pero en la cola)
    while (queue.Count > 0)
    {
        GameObject candidate = queue.Peek();

        if (candidate == null)
        {
            queue.Dequeue();
            continue;
        }

        // Si el objeto está activo es un zombie — ya fue adquirido
        // pero quedó en la cola por el bug de doble retorno.
        // Lo removemos de la cola sin tocarlo.
        if (candidate.activeInHierarchy)
        {
            queue.Dequeue();
            Debug.LogWarning($"[ObjectPool] Objeto zombie detectado en pool {label}. " +
                             "Fue ignorado. El fix de _inUse debería prevenir esto.");
            continue;
        }

        // Objeto válido — lo adquirimos
        queue.Dequeue();

        // Acquire ANTES de SetActive para que _inUse sea true
        // antes de que Unity procese física en el próximo frame
        if (candidate.TryGetComponent<ProjectileBase>(out var proj))
            proj.Acquire();

        candidate.SetActive(true);
        return candidate;
    }

    // Pool vacío — instanciamos uno nuevo
    Debug.LogWarning($"[ObjectPool] Pool de {label} vacío. " +
                     $"Considera aumentar initialSize (actual: {config.initialSize}).");

    GameObject extra = Instantiate(config.prefab, container);

    if (extra.TryGetComponent<ProjectileBase>(out var newProj))
        newProj.Acquire();

    extra.SetActive(true);
    return extra;
}
}