using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuthRegisterUI : MonoBehaviour
{
    [Header("Referencias de la UI para registro")]
    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField usernameInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Referencia al AuthManager")]
    [SerializeField] private AuthManager authManager;

    private void Start()
    {
        if (feedbackText != null) feedbackText.text = "";

        registerButton.onClick.AddListener(() => OnRegisterButtonClicked().Forget());
    }

    private async UniTaskVoid OnRegisterButtonClicked()
    {
        string email = emailInputField.text;
        string username = usernameInputField.text;
        string password = passwordInputField.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            UpdateFeedback("Por favor, completa todos los campos para registrarte.", Color.yellow);
            return;
        }

        if (password.Length < 6)
        {
            UpdateFeedback("La contraseña debe tener al menos 6 caracteres.", Color.yellow);
            return;
        }

        SetFormState(false);
        UpdateFeedback("Registrando usuario...", Color.white);

        bool success = await authManager.RegisterAsync(email, username, password);
        if (success)
        {
            UpdateFeedback("¡Registro exitoso! Ahora puedes iniciar sesión.", Color.green);
            emailInputField.text = "";
            usernameInputField.text = "";
            passwordInputField.text = "";

            // Aquí puedes agregar lógica adicional, como limpiar los campos o redirigir al usuario a la pantalla de inicio de sesión
        }
        else
        {
            UpdateFeedback("Error al registrarse. Verifica tus datos e inténtalo de nuevo.", Color.red);
        }

        SetFormState(true);
    }

    private void SetFormState(bool isEnabled)
    {
        registerButton.interactable = isEnabled;
        emailInputField.interactable = isEnabled;
        usernameInputField.interactable = isEnabled;
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
