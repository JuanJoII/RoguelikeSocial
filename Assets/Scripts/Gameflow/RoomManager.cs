using System;
using UnityEngine;

/// <summary>
/// Director de la sala actual. Existe uno por nivel, no uno por sala.
/// Cuando el jugador entra a una sala nueva, RoomManager se reconfigura
/// para esa sala — no hay un RoomManager por sala.
///
/// RESPONSABILIDADES:
/// - Recibir la activación de sala desde RoomEntrance
/// - Elegir la config aleatoria del RoomContext
/// - Iniciar el WaveManager con esa config
/// - Llevar el contador de enemigos muertos vs total requerido
/// - Calcular el score al completar la sala
/// - Notificar al RunManager y al GameManager
///
/// LO QUE NO HACE:
/// - No spawea enemigos (WaveManager)
/// - No guarda en Firebase (DataManager vía RunManager)
/// - No toca UI (UIManager escucha sus eventos)
/// </summary>
public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    // UIManager y otros sistemas escuchan estos eventos
    public static event Action<int, int> OnEnemyCountChanged; // (muertos, total)
    public static event Action<int> OnRoomComplete;           // (score obtenido)

    [Header("Referencias")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private Transform playerTransform;

    // Estado de la sala actual
    private RoomConfigSO _currentConfig;
    private RoomContext _currentContext;
    private int _enemiesDefeated;
    private float _roomStartTime;
    private bool _roomActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        EnemyAI.OnEnemyDied += HandleEnemyDied;
        PlayerHealth.OnPlayerDead += HandlePlayerDead;
        BossBase.OnBossDead  += HandleBossDefeated; 
    }

    private void OnDisable()
    {
        EnemyAI.OnEnemyDied -= HandleEnemyDied;
        PlayerHealth.OnPlayerDead -= HandlePlayerDead;
        BossBase.OnBossDead  -= HandleBossDefeated;
    }
    private void HandleBossDefeated(BossBase boss)
    {
        OnBossDefeated();
    }
    /// <summary>
    /// Llamado por RoomEntrance cuando el jugador cruza el umbral.
    /// Es el punto de entrada de toda la lógica de sala.
    /// </summary>
    public void ActivateRoom(RoomContext context)
    {
        if (_roomActive)
            waveManager.StopWaves();

        _currentContext = context;
        _currentConfig = context.GetRandomConfig();
        if (_currentConfig == null) return;

        _enemiesDefeated = 0;
        _roomStartTime = Time.time;
        _roomActive = true;

        TopDownCamera.Instance.SetRoomBounds(
            context.boundsMin, context.boundsMax, context.useCameraBounds);

        RunManager.Instance.SetCurrentRoom(context.roomIndex);

        // Usamos el WaveManager del RoomContext, no el del Inspector
        // Este es el que el integrador configuró con los spawn points correctos
        WaveManager contextWaveManager = context.GetComponent<WaveManager>();

        if (contextWaveManager == null)
        {
            Debug.LogError($"[RoomManager] El RoomContext {context.roomIndex} " +
                           "no tiene WaveManager. Verifica el integrador.");
            return;
        }

        // Actualizamos la referencia activa
        waveManager = contextWaveManager;

        waveManager.Initialize(_currentConfig, playerTransform);
        waveManager.StartWaves();

        OnEnemyCountChanged?.Invoke(0, _currentConfig.totalEnemiesRequired);
    }

    private void HandleEnemyDied(EnemyAI enemy)
    {
        if (!_roomActive) return;

        _enemiesDefeated++;
        OnEnemyCountChanged?.Invoke(_enemiesDefeated, _currentConfig.totalEnemiesRequired);

        // ¿Completamos la sala?
        // En sala de boss este conteo no aplica — la sala
        // termina cuando el boss muere, no por enemigos normales
        if (!_currentConfig.isBossRoom &&
            _enemiesDefeated >= _currentConfig.totalEnemiesRequired)
        {
            CompleteRoom();
        }
    }

    /// <summary>
    /// Llamado externamente cuando el boss muere.
    /// El sistema de boss (que no programas tú) debe llamar esto.
    /// </summary>
    public void OnBossDefeated()
    {
        if (!_roomActive || !_currentConfig.isBossRoom) return;
        CompleteRoom();
    }

    private void CompleteRoom()
    {
        _roomActive = false;
        waveManager.StopWaves();

        float timeElapsed = Time.time - _roomStartTime;
        int lives = FindObjectOfType<PlayerHealth>()?.CurrentLives ?? 0;
        bool isBoss = _currentConfig.isBossRoom;

        int score = ScoreCalculator.Calculate(timeElapsed, lives, isBoss);

        // Guardamos el score en el RunManager (él decide si supera el mejor)
        RunManager.Instance.RegisterRoomScore(_currentContext.roomIndex, score);

        // Notificamos — UIManager muestra la pantalla de score
        // GameManager maneja la transición al siguiente estado
        OnRoomComplete?.Invoke(score);

        GameManager.Instance.TransitionTo(GameManager.GameState.RoomTransition);
    }

    private void HandlePlayerDead()
    {
        if (!_roomActive) return;
        _roomActive = false;
        waveManager.StopWaves();
    }
}