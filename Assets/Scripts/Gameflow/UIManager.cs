using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maneja todo el estado visual de la UI.
///
/// PRINCIPIO: Solo escucha eventos, nunca llama lógica de juego.
/// Si necesita que algo pase en el juego, llama a GameManager
/// o RunManager — nunca toca PlayerHealth, RoomManager, etc. directamente.
///
/// ANIMACIONES:
/// Todas las transiciones usan coroutines con interpolación manual.
/// Sin dependencias externas — funciona con cualquier versión de Unity.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── HUD ──────────────────────────────────────────────────────────────
    [Header("HUD — Vidas")]
    [Tooltip("Padre de los corazones. Debe tener Horizontal Layout Group.")]
    [SerializeField] private Transform heartsContainer;
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private Sprite heartFullSprite;
    [SerializeField] private Sprite heartEmptySprite;

    [Header("HUD — Bala activa")]
    [SerializeField] private Image bulletTypeIndicator;
    [SerializeField] private Sprite groundBulletSprite;
    [SerializeField] private Sprite airBulletSprite;

    [Header("HUD — Contador enemigos")]
    [SerializeField] private TextMeshProUGUI enemyCounterText;

    // ── Pantallas ─────────────────────────────────────────────────────────
    [Header("Pantalla — Sala completada")]
    [SerializeField] private CanvasGroup roomCompleteScreen;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Button continueButton;

    [Header("Pantalla — Muerte")]
    [SerializeField] private CanvasGroup deathScreen;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Pantalla — Pausa")]
    [SerializeField] private CanvasGroup pauseScreen;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseMainMenuButton;

    [Header("Boss Health Bar")]
    [SerializeField] private GameObject bossHealthBarContainer;
    [SerializeField] private Slider bossHealthBar;

    // ── Configuración de animaciones ──────────────────────────────────────
    [Header("Animaciones")]
    [Tooltip("Duración del fade in/out de pantallas completas.")]
    [SerializeField] private float screenFadeDuration = 0.3f;

    [Tooltip("Duración del score counting up en pantalla de sala completada.")]
    [SerializeField] private float scoreCountDuration = 1.2f;

    // ── Estado interno ────────────────────────────────────────────────────
    private List<Image> _heartImages = new List<Image>();
    private int _maxLives;
    private Coroutine _scoreCountCoroutine;

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
        // Todas las pantallas empiezan ocultas e interactuables = false
        SetScreenVisible(roomCompleteScreen, false, instant: true);
        SetScreenVisible(deathScreen, false, instant: true);
        SetScreenVisible(pauseScreen, false, instant: true);

        bossHealthBarContainer?.SetActive(false);

        ConnectButtons();
    }

    private void ConnectButtons()
    {
        continueButton?.onClick.AddListener(OnContinuePressed);
        retryButton?.onClick.AddListener(OnRetryPressed);
        mainMenuButton?.onClick.AddListener(OnMainMenuPressed);
        resumeButton?.onClick.AddListener(OnResumePressed);
        pauseMainMenuButton?.onClick.AddListener(OnMainMenuPressed);
    }

    private void OnEnable()
    {
        PlayerHealth.OnDamaged      += HandlePlayerDamaged;
        PlayerHealth.OnPlayerDead   += HandlePlayerDead;
        WeaponSystem.OnBulletTypeChanged += HandleBulletTypeChanged;
        RoomManager.OnEnemyCountChanged  += HandleEnemyCountChanged;
        RoomManager.OnRoomComplete       += HandleRoomComplete;
        GameManager.OnStateChanged       += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        PlayerHealth.OnDamaged           -= HandlePlayerDamaged;
        PlayerHealth.OnPlayerDead        -= HandlePlayerDead;
        WeaponSystem.OnBulletTypeChanged -= HandleBulletTypeChanged;
        RoomManager.OnEnemyCountChanged  -= HandleEnemyCountChanged;
        RoomManager.OnRoomComplete       -= HandleRoomComplete;
        GameManager.OnStateChanged       -= HandleGameStateChanged;
    }

    // ════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN DE CORAZONES
    // Llamado por PlayerHealth al inicio — crea los corazones dinámicamente
    // para que el número de vidas sea configurable sin tocar la UI
    // ════════════════════════════════════════════════════════════════════

    public void InitializeHearts(int maxLives)
    {
        _maxLives = maxLives;

        // Limpiamos los corazones previos si los hay
        foreach (Transform child in heartsContainer)
            Destroy(child.gameObject);

        _heartImages.Clear();

        for (int i = 0; i < maxLives; i++)
        {
            GameObject heartObj = Instantiate(heartPrefab, heartsContainer);
            Image heartImage = heartObj.GetComponent<Image>();
            heartImage.sprite = heartFullSprite;
            _heartImages.Add(heartImage);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // HANDLERS DE EVENTOS
    // ════════════════════════════════════════════════════════════════════

    private void HandlePlayerDamaged(int currentLives, int maxLives)
    {
        UpdateHearts(currentLives, maxLives);
    }

    private void HandlePlayerDead()
    {
        // La pantalla de muerte se muestra cuando el GameManager
        // llega al estado PlayerDead — no aquí directamente.
        // Esto evita que la pantalla aparezca antes de que la
        // animación de muerte del jugador termine.
    }

    private void HandleBulletTypeChanged(EnemyType newType)
    {
        if (bulletTypeIndicator == null) return;

        bulletTypeIndicator.sprite = newType == EnemyType.Ground
            ? groundBulletSprite
            : airBulletSprite;

        // Pequeño punch de escala para dar feedback del cambio
        StartCoroutine(PunchScale(bulletTypeIndicator.rectTransform, 1.3f, 0.15f));
    }

    private void HandleEnemyCountChanged(int defeated, int total)
    {
        if (enemyCounterText == null) return;
        enemyCounterText.text = $"{defeated} / {total}";
    }

    private void HandleRoomComplete(int score)
    {
        if (_scoreCountCoroutine != null)
            StopCoroutine(_scoreCountCoroutine);

        _scoreCountCoroutine = StartCoroutine(ShowRoomComplete(score));
    }

    private void HandleGameStateChanged(GameManager.GameState previous,
                                        GameManager.GameState next)
    {
        switch (next)
        {
            case GameManager.GameState.PlayerDead:
                StartCoroutine(ShowScreenDelayed(deathScreen, delay: 1.5f));
                break;

            case GameManager.GameState.Paused:
                SetScreenVisible(pauseScreen, true);
                break;

            case GameManager.GameState.InRoom:
            case GameManager.GameState.BossRoom:
                SetScreenVisible(pauseScreen, false, instant: true);
                SetScreenVisible(deathScreen, false, instant: true);
                SetScreenVisible(roomCompleteScreen, false, instant: true);
                bossHealthBarContainer?.SetActive(
                    next == GameManager.GameState.BossRoom);
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // BOSS HEALTH BAR
    // Tu amigo que programa el boss llama estos métodos
    // ════════════════════════════════════════════════════════════════════

    public void InitializeBossHealth(int maxHealth)
    {
        if (bossHealthBar == null) return;
        bossHealthBar.maxValue = maxHealth;
        bossHealthBar.value = maxHealth;
        bossHealthBarContainer?.SetActive(true);
    }

    public void UpdateBossHealth(int currentHealth)
    {
        if (bossHealthBar == null) return;
        StartCoroutine(AnimateBossHealthBar(currentHealth));
    }

    // ════════════════════════════════════════════════════════════════════
    // BOTONES
    // ════════════════════════════════════════════════════════════════════

    private void OnContinuePressed()
    {
        SetScreenVisible(roomCompleteScreen, false);
        GameManager.Instance.TransitionTo(GameManager.GameState.InRoom);
    }

    private void OnRetryPressed()
    {
        SetScreenVisible(deathScreen, false);
        // RunManager sabe el nivel actual y lo reinicia desde la primera sala
        RunManager.Instance.RetryCurrentLevel();
    }

    private void OnMainMenuPressed()
    {
        SetScreenVisible(deathScreen, false, instant: true);
        SetScreenVisible(pauseScreen, false, instant: true);
        GameManager.Instance.TransitionTo(GameManager.GameState.MainMenu);
    }

    private void OnResumePressed()
    {
        GameManager.Instance.TransitionTo(GameManager.GameState.InRoom);
    }

    // ════════════════════════════════════════════════════════════════════
    // ANIMACIONES
    // ════════════════════════════════════════════════════════════════════

    private void UpdateHearts(int currentLives, int maxLives)
    {
        for (int i = 0; i < _heartImages.Count; i++)
        {
            bool isFull = i < currentLives;
            _heartImages[i].sprite = isFull ? heartFullSprite : heartEmptySprite;

            // Punch de escala en el corazón que acaba de perderse
            if (!isFull && i == currentLives)
                StartCoroutine(PunchScale(_heartImages[i].rectTransform, 1.4f, 0.2f));
        }
    }

    /// <summary>
    /// Muestra u oculta una pantalla con fade.
    /// instant = true salta la animación, útil al inicializar.
    /// </summary>
    // Guardamos referencias a las coroutines activas por pantalla
// para poder detenerlas correctamente antes de iniciar una nueva
    private Coroutine _roomCompleteFade;
    private Coroutine _deathFade;
    private Coroutine _pauseFade;

    private void SetScreenVisible(CanvasGroup group, bool visible, bool instant = false)
    {
        if (group == null) return;

        // Determinamos qué coroutine corresponde a este grupo
        // y la detenemos antes de iniciar una nueva
        ref Coroutine fadeRef = ref GetFadeRef(group);

        if (fadeRef != null)
        {
            StopCoroutine(fadeRef);
            fadeRef = null;
        }

        if (instant)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            return;
        }

        fadeRef = StartCoroutine(FadeScreen(group, visible));
    }

    /// <summary>
    /// Devuelve la referencia a la coroutine correspondiente al CanvasGroup.
    /// Esto nos permite detener el fade correcto sin un diccionario.
    /// </summary>
    private ref Coroutine GetFadeRef(CanvasGroup group)
    {
        if (group == roomCompleteScreen) return ref _roomCompleteFade;
        if (group == deathScreen)        return ref _deathFade;
        if (group == pauseScreen)        return ref _pauseFade;

        // Fallback — nunca debería llegar aquí si los grupos están bien asignados
        Debug.LogWarning($"[UIManager] CanvasGroup {group.name} no tiene referencia de fade registrada.");
        return ref _pauseFade;
    }

    private IEnumerator FadeScreen(CanvasGroup group, bool fadeIn)
    {
        float startAlpha  = group.alpha;
        float targetAlpha = fadeIn ? 1f : 0f;
        float elapsed     = 0f;

        group.interactable   = false;
        group.blocksRaycasts = fadeIn;

        while (elapsed < screenFadeDuration)
        {
            elapsed    += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / screenFadeDuration);
            yield return null;
        }

        group.alpha          = targetAlpha;
        group.interactable   = fadeIn;
    }

    private IEnumerator ShowScreenDelayed(CanvasGroup screen, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SetScreenVisible(screen, true);
    }

    private IEnumerator ShowRoomComplete(int finalScore)
    {
        SetScreenVisible(roomCompleteScreen, true);

        // Contamos el score desde 0 hasta el valor final
        // Se siente más satisfactorio que aparecer el número de golpe
        float elapsed = 0f;
        int displayScore = 0;

        while (elapsed < scoreCountDuration)
        {
            elapsed += Time.deltaTime;
            displayScore = Mathf.RoundToInt(
                Mathf.Lerp(0, finalScore, elapsed / scoreCountDuration));
            scoreText.text = displayScore.ToString("N0"); // formato con separador de miles
            yield return null;
        }

        scoreText.text = finalScore.ToString("N0");
    }

    /// <summary>
    /// Escala un elemento hacia arriba y vuelve a su tamaño original.
    /// Útil para dar feedback sin animaciones costosas.
    /// </summary>
    private IEnumerator PunchScale(RectTransform target, float peakScale, float duration)
    {
        Vector3 originalScale = target.localScale;
        Vector3 bigScale = originalScale * peakScale;
        float half = duration * 0.5f;
        float elapsed = 0f;

        // Sube
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(originalScale, bigScale, elapsed / half);
            yield return null;
        }

        elapsed = 0f;

        // Baja
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(bigScale, originalScale, elapsed / half);
            yield return null;
        }

        target.localScale = originalScale;
    }

    private IEnumerator AnimateBossHealthBar(int targetHealth)
    {
        float startValue = bossHealthBar.value;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            bossHealthBar.value = Mathf.Lerp(startValue, targetHealth, elapsed / duration);
            yield return null;
        }

        bossHealthBar.value = targetHealth;
    }
}
