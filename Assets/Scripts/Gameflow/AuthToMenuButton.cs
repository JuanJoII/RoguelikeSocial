using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Botón que lleva al jugador al menú principal después de autenticarse.
/// Asigna este script al botón y conéctalo desde el Inspector
/// o usa el evento OnClick del componente Button.
/// </summary>
[RequireComponent(typeof(Button))]
public class AuthToMenuButton : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "Level_00";

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(GoToMenu);
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene(menuSceneName);
    }
}