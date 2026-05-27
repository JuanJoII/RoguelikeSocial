using UnityEngine;

public class PauseManager : MonoBehaviour
{
    // Ya no necesitamos referencia al botón ni Start()
    // El botón llama TogglePause() directamente desde el Inspector

    public void TogglePause()
    {
        bool isPaused = GameManager.Instance.CurrentState
                        == GameManager.GameState.Paused;

        if (isPaused)
        {
            Time.timeScale = 1f;
            GameManager.Instance.TransitionTo(GameManager.GameState.InRoom);
        }
        else
        {
            if (GameManager.Instance.CurrentState != GameManager.GameState.InRoom &&
                GameManager.Instance.CurrentState != GameManager.GameState.BossRoom)
                return;

            Time.timeScale = 0f;
            GameManager.Instance.TransitionTo(GameManager.GameState.Paused);
        }
    }
}