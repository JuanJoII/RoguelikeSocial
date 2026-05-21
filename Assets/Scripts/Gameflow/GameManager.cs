using System;
using UnityEngine;

/// <summary>
/// Máquina de estados global del juego.
/// Solo hace transiciones y dispara el evento OnStateChanged.
/// Ninguna lógica de gameplay vive aquí.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        MainMenu,
        Loading,
        InRoom,
        BossRoom,
        RoomTransition,
        Paused,
        PlayerDead,
        LevelComplete,
        GameOver
    }

    public GameState CurrentState { get; private set; }
    public static event Action<GameState, GameState> OnStateChanged;

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
        PlayerHealth.OnPlayerDead += () => TransitionTo(GameState.PlayerDead);
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDead -= () => TransitionTo(GameState.PlayerDead);
    }

    public void TransitionTo(GameState newState)
    {
        if (newState == CurrentState) return;

        GameState previous = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(previous, CurrentState);
    }
}
