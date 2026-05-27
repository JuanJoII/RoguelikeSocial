using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Orquesta el menú principal 3D.
///
/// RESPONSABILIDADES:
/// - Cargar el progreso del jugador desde DataManager al iniciar
/// - Configurar el estado de cada puerta según el progreso
/// - Mostrar la info del jugador en el HUD del lobby
/// - Manejar el menú de pausa del lobby (salir + ranking)
/// - Cargar la escena del nivel seleccionado
///
/// SETUP EN UNITY:
/// Este script va en un GameObject vacío en la escena del menú.
/// Las puertas deben tener DoorInteraction configurado.
/// El jugador debe estar en la escena con su PlayerController activo.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    [Header("Referencias de escena")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private DoorInteraction[] doors; // asigna las 3 puertas en orden

    [Header("HUD del lobby")]
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI maxLevelText;

    [Header("Menú de pausa del lobby")]
    [SerializeField] private CanvasGroup lobbyPauseMenu;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeLobbyButton;
    [SerializeField] private Button rankingButton;
    [SerializeField] private Button exitButton;

    [Header("Ranking")]
    [SerializeField] private RankingPanel rankingPanel;

    [Header("Nombres de escenas")]
    [Tooltip("Nombres exactos de las escenas de cada nivel en Build Settings.")]
    [SerializeField] private string[] levelSceneNames = { "Level_01", "Level_02", "Level_03" };

    private bool _isPaused;

    // Datos del jugador — recibidos de DataManager
    private string _username;
    private int _totalScore;
    private int _maxUnlockedLevel;

    // ════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ConnectButtons();
        SetLobbyPauseVisible(false, instant: true);

        // Aseguramos que el tiempo corra al volver al menú
        // (puede haber quedado en 0 si el jugador pausó antes de morir)
        Time.timeScale = 1f;

        // Cargamos el progreso del jugador
        // DataManager llama OnProgressLoaded cuando tiene los datos
        DataManager.Instance.FetchPlayerProgress(OnProgressLoaded);
    }

    private void ConnectButtons()
    {
        pauseButton?.onClick.AddListener(ToggleLobbyPause);
        resumeLobbyButton?.onClick.AddListener(ToggleLobbyPause);
        rankingButton?.onClick.AddListener(OnRankingPressed);
        exitButton?.onClick.AddListener(OnExitPressed);
    }

    // ════════════════════════════════════════════════════════════════════
    // PROGRESO DEL JUGADOR
    // ════════════════════════════════════════════════════════════════════

    private void OnProgressLoaded(string username, int totalScore, int maxUnlockedLevel)
    {
        _username = username;
        _totalScore = totalScore;
        _maxUnlockedLevel = maxUnlockedLevel;

        UpdateHUD();
        ConfigureDoors();
    }

    private void UpdateHUD()
    {
        if (usernameText != null)
            usernameText.text = _username;

        if (totalScoreText != null)
            totalScoreText.text = _totalScore.ToString("N0");

        if (maxLevelText != null)
            maxLevelText.text = $"Nivel {_maxUnlockedLevel}";
    }

    private void ConfigureDoors()
    {
        // doors[0] = nivel 1, doors[1] = nivel 2, doors[2] = nivel 3
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null) continue;

            // El nivel i+1 está desbloqueado si maxUnlockedLevel >= i+1
            bool unlocked = (i + 1) <= _maxUnlockedLevel;
            doors[i].SetUnlocked(unlocked, playerTransform);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PAUSA DEL LOBBY
    // ════════════════════════════════════════════════════════════════════

    private void ToggleLobbyPause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;
        SetLobbyPauseVisible(_isPaused);

        // Si cerramos la pausa, también cerramos el ranking si está abierto
        if (!_isPaused)
            rankingPanel?.Hide();
    }

    private void SetLobbyPauseVisible(bool visible, bool instant = false)
    {
        if (lobbyPauseMenu == null) return;

        if (instant)
        {
            lobbyPauseMenu.alpha = visible ? 1f : 0f;
            lobbyPauseMenu.interactable = visible;
            lobbyPauseMenu.blocksRaycasts = visible;
            return;
        }

        // Fade simple — reutilizamos la misma lógica que el UIManager
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(lobbyPauseMenu, visible));
    }

    private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup group, bool fadeIn)
    {
        float start  = group.alpha;
        float target = fadeIn ? 1f : 0f;
        float elapsed = 0f;
        float duration = 0.25f;

        group.interactable   = false;
        group.blocksRaycasts = fadeIn;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        group.alpha = target;
        group.interactable = fadeIn;
    }

    // ════════════════════════════════════════════════════════════════════
    // BOTONES
    // ════════════════════════════════════════════════════════════════════

    private void OnRankingPressed()
    {
        rankingPanel?.Show();
    }

    private void OnExitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ════════════════════════════════════════════════════════════════════
    // CARGA DE NIVEL
    // ════════════════════════════════════════════════════════════════════

    public void LoadLevel(int levelIndex)
    {
        // levelIndex es 1-based — el jugador entra al nivel 1, 2 o 3
        if (levelIndex < 1 || levelIndex > levelSceneNames.Length)
        {
            Debug.LogError($"[MainMenuManager] levelIndex {levelIndex} fuera de rango.");
            return;
        }

        // Guardamos el nivel actual en RunManager antes de cargar
        RunManager.Instance.StartRun(levelIndex);

        Time.timeScale = 1f;
        SceneManager.LoadScene(levelSceneNames[levelIndex - 1]);
    }
}