using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Maneja el ciclo completo de oleadas en una sala.
///
/// FLUJO SALA NORMAL:
/// StartWaves → lanza oleadas terrestres y aéreas según config
/// → cada oleada: elige SpawnPoint → activa portal → espera delay
/// → saca enemigos del pool → asigna a EnemyGroup
/// → cuando grupo muere, decrementa contador de oleadas activas
/// → cuando todas las oleadas están lanzadas y el RoomManager
///   detecta el total de muertos, la sala termina
///
/// FLUJO SALA DE BOSS:
/// Las oleadas son infinitas — cuando los enemigos bajan del cap,
/// se lanza una nueva oleada hasta volver al cap.
/// La sala termina cuando el boss muere (RoomManager lo detecta).
///
/// SPAWN POINTS:
/// Son Transforms marcados en la sala. WaveManager los elige al azar.
/// Tu amiga los puede marcar o tú los colocas manualmente.
/// </summary>
public class WaveManager : MonoBehaviour
{
    // RoomManager y EnemyGroup se comunican a través de este evento
    public static Action<EnemyGroup> OnGroupDefeated;

    [Header("Spawn Points")]
    [Tooltip("Posiciones donde aparecen los portales de spawn. " +
             "Coloca al menos 3-4 en esquinas y bordes de la sala.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("VFX")]
    [SerializeField] private VFXData portalVFX;

    // Config activa — asignada por RoomManager
    private RoomConfigSO _config;
    private Transform _player;

    // Estado del boss room
    private int _activeEnemyCount;
    private bool _bossRoomActive;
    private Coroutine _bossRoomRoutine;

    public void Initialize(RoomConfigSO config, Transform player)
    {
        _config = config;
        _player = player;
        _activeEnemyCount = 0;
        _bossRoomActive = false;
    }

    public void StartWaves()
    {
        if (_config == null)
        {
            Debug.LogError("[WaveManager] StartWaves llamado sin config. " +
                           "Llama Initialize primero.");
            return;
        }

        if (_config.isBossRoom)
            StartBossRoom();
        else
            StartNormalRoom();
    }

    public void StopWaves()
    {
        _bossRoomActive = false;
        if (_bossRoomRoutine != null)
            StopCoroutine(_bossRoomRoutine);
        StopAllCoroutines();
    }

    // ── Sala normal ───────────────────────────────────────────────────────

    private void StartNormalRoom()
    {
        StartCoroutine(NormalRoomRoutine());
    }

    private IEnumerator NormalRoomRoutine()
    {
        // Lanzamos oleadas terrestres y aéreas intercaladas
        // según los conteos configurados en RoomConfigSO
        List<WaveConfigSO> wavePlan = BuildWavePlan();

        foreach (WaveConfigSO waveConfig in wavePlan)
        {
            yield return StartCoroutine(SpawnWave(waveConfig));
            yield return new WaitForSeconds(_config.timeBetweenWaves);
        }
    }

    /// <summary>
    /// Construye la lista de oleadas intercalando terrestres y aéreas.
    /// Si hay más oleadas de un tipo que de otro, las restantes se agregan al final.
    /// </summary>
    private List<WaveConfigSO> BuildWavePlan()
    {
        List<WaveConfigSO> plan = new List<WaveConfigSO>();

        Queue<WaveConfigSO> groundQueue = BuildWaveQueue(
            _config.groundWaveConfigs, _config.groundWaveCount);
        Queue<WaveConfigSO> airQueue = BuildWaveQueue(
            _config.airWaveConfigs, _config.airWaveCount);

        // Intercalamos: 1 terrestre, 1 aérea, 1 terrestre, 1 aérea...
        while (groundQueue.Count > 0 || airQueue.Count > 0)
        {
            if (groundQueue.Count > 0) plan.Add(groundQueue.Dequeue());
            if (airQueue.Count > 0)    plan.Add(airQueue.Dequeue());
        }

        return plan;
    }

    private Queue<WaveConfigSO> BuildWaveQueue(WaveConfigSO[] configs, int count)
    {
        var queue = new Queue<WaveConfigSO>();
        if (configs == null || configs.Length == 0) return queue;

        for (int i = 0; i < count; i++)
        {
            // Elige aleatoriamente de los configs disponibles para ese tipo
            WaveConfigSO chosen = configs[Random.Range(0, configs.Length)];
            queue.Enqueue(chosen);
        }

        return queue;
    }

    // ── Sala de boss ──────────────────────────────────────────────────────

    private void StartBossRoom()
    {
        _bossRoomActive = true;
        _bossRoomRoutine = StartCoroutine(BossRoomRoutine());
    }

    private IEnumerator BossRoomRoutine()
    {
        while (_bossRoomActive)
        {
            if (_activeEnemyCount < _config.bossRoomEnemyCap)
            {
                // Lanzamos una oleada para reponer hasta el cap
                WaveConfigSO config = GetRandomWaveConfig();
                if (config != null)
                    yield return StartCoroutine(SpawnWave(config));
            }

            // Chequeamos cada segundo si necesitamos más enemigos
            yield return new WaitForSeconds(1f);
        }
    }

    private WaveConfigSO GetRandomWaveConfig()
    {
        // En sala de boss alternamos aleatoriamente entre terrestres y aéreos
        bool useGround = _config.groundWaveConfigs?.Length > 0 &&
                         (Random.value > 0.5f || _config.airWaveConfigs?.Length == 0);

        WaveConfigSO[] pool = useGround
            ? _config.groundWaveConfigs
            : _config.airWaveConfigs;

        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }

    // ── Spawn de una oleada ───────────────────────────────────────────────

    private IEnumerator SpawnWave(WaveConfigSO waveConfig)
    {
        if (waveConfig?.enemyData == null) yield break;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[WaveManager] No hay SpawnPoints asignados.");
            yield break;
        }

        int enemyCount = Random.Range(waveConfig.minEnemies, waveConfig.maxEnemies + 1);

        // Elegimos un spawn point para esta oleada
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Portal VFX — feedback visual de dónde va a aparecer la oleada
        VFXPool.Instance.PlayVFX(portalVFX, spawnPoint.position, spawnPoint.rotation);

        // Esperamos antes de empezar a spawnear — el jugador ve el portal
        yield return new WaitForSeconds(waveConfig.portalDelay);

        // Creamos el grupo que va a gestionar esta oleada
        GameObject groupObj = new GameObject($"EnemyGroup_{waveConfig.name}");
        EnemyGroup group = groupObj.AddComponent<EnemyGroup>();

        List<EnemyAI> members = new List<EnemyAI>();

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject enemyObj = ObjectPool.Instance.GetEnemy(waveConfig.enemyData.enemyType);
            if (enemyObj == null) continue;

            // Pequeña variación en la posición para que no aparezcan
            // todos exactamente en el mismo punto
            Vector3 spawnPos = spawnPoint.position + new Vector3(
                Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));

            enemyObj.transform.position = spawnPos;

            if (enemyObj.TryGetComponent<EnemyAI>(out var enemyAI))
                members.Add(enemyAI);

            _activeEnemyCount++;

            yield return new WaitForSeconds(waveConfig.spawnInterval);
        }

        // Inicializamos el grupo con todos sus miembros
        group.Initialize(members);

        // Inicializamos cada enemigo con los datos y la referencia al grupo
        foreach (EnemyAI enemy in members)
            enemy.Initialize(waveConfig.enemyData, _player, group);

        // Escuchamos cuando este grupo sea derrotado para actualizar el contador
        OnGroupDefeated += HandleGroupDefeated;
    }

    private void HandleGroupDefeated(EnemyGroup group)
    {
        OnGroupDefeated -= HandleGroupDefeated;

        // Actualizamos el contador de enemigos activos en sala de boss
        // En sala normal esto no importa porque el RoomManager
        // lleva el conteo de muertos totales independientemente
        if (_bossRoomActive)
        {
            // Recalculamos cuántos quedan activos
            // En una implementación más robusta mantendríamos
            // un conteo por grupo, pero esto es suficiente para el proyecto
            _activeEnemyCount = Mathf.Max(0, _activeEnemyCount - 1);
        }
    }
}
