using Cysharp.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class AuthLoginUI : MonoBehaviour
{
    [Header("Referencias de la UI para login")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private Button loginButton;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Referencia al AuthManager")]
    [SerializeField] private AuthManager authManager;

    private void Start()
    {
        if (feedbackText != null) feedbackText.text = "";

        loginButton.onClick.AddListener(() => OnLoginButtonClicked().Forget());
    }

    private async UniTaskVoid OnLoginButtonClicked()
    {
        string email = emailInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            UpdateFeedback("Por favor, ingresa tu correo electrónico y contraseña.", Color.yellow);
            return;
        }

        SetFormState(false);
        UpdateFeedback("Iniciando sesión...", Color.white);

        bool success = await authManager.LoginAsync(email, password);
        if (success)
        {
            UpdateFeedback("¡Inicio de sesión exitoso!", Color.green);
            // Aquí puedes agregar lógica adicional, como cargar la siguiente escena o mostrar el menú principal
        }
        else
        {
            UpdateFeedback("Error al iniciar sesión. Verifica tus credenciales e inténtalo de nuevo.", Color.red);
            SetFormState(true);
        }
    }

    private void SetFormState(bool isEnabled)
    {
        loginButton.interactable = isEnabled;
        emailInputField.interactable = isEnabled;
        passwordInputField.interactable = isEnabled;
    }

    private void UpdateFeedback(string message, Color color)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = color;
        }
        return;
    }
}
