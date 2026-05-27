using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guarda el estado del run activo en memoria.
/// Persiste entre escenas — sabe en qué nivel y sala está el jugador.
///
/// CUÁNDO GUARDA EN FIREBASE:
/// Solo cuando el jugador muere o completa un nivel completo.
/// No hay escrituras intermedias — evita problemas con conexión
/// variable en móvil y reduce costos de Firebase.
///
/// SCORES:
/// Solo reemplaza el score guardado si el nuevo es mayor.
/// DataManager hace la comparación antes de escribir.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    public int CurrentLevel { get; private set; }
    public int CurrentRoomIndex { get; private set; }

    // Scores obtenidos en este run — roomIndex → score
    private Dictionary<int, int> _runScores = new Dictionary<int, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDead += HandlePlayerDead;
        GameManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDead -= HandlePlayerDead;
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    public void StartRun(int level)
    {
        CurrentLevel = level;
        CurrentRoomIndex = 0;
        _runScores.Clear();
    }

    public void SetCurrentRoom(int roomIndex)
    {
        CurrentRoomIndex = roomIndex;
    }

    /// <summary>
    /// RoomManager llama esto al completar cada sala.
    /// Guardamos en memoria — no vamos a Firebase todavía.
    /// </summary>
    public void RegisterRoomScore(int roomIndex, int score)
    {
        // Si ya teníamos un score de esta sala en este run,
        // guardamos el mayor de los dos
        if (_runScores.ContainsKey(roomIndex))
            _runScores[roomIndex] = Mathf.Max(_runScores[roomIndex], score);
        else
            _runScores[roomIndex] = score;
    }

    private void HandlePlayerDead()
    {
        // Enviamos a Firebase los scores de las salas completadas en este run
        // El DataManager compara con los scores guardados antes de escribir
        DataManager.Instance.SaveRunProgress(CurrentLevel, CurrentRoomIndex, _runScores);
    }

    private void HandleStateChanged(GameManager.GameState previous,
                                    GameManager.GameState next)
    {
        if (next == GameManager.GameState.LevelComplete)
        {
            // Nivel completo — guardamos progreso y desbloqueamos el siguiente
            DataManager.Instance.SaveRunProgress(CurrentLevel, CurrentRoomIndex, _runScores);
            DataManager.Instance.UnlockNextLevel(CurrentLevel);
        }
    }
    public void RetryCurrentLevel()
    {
        // Reinicia el run desde la primera sala del nivel actual
        StartRun(CurrentLevel);

        // Carga la escena del nivel — ajusta el nombre según tu proyecto
        UnityEngine.SceneManagement.SceneManager.LoadScene($"Level_{CurrentLevel:D2}");
    }
}