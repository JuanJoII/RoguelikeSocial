using UnityEngine;

/// <summary>
/// Trigger invisible en el umbral de cada puerta del menú.
/// Cuando el jugador lo atraviesa, carga el nivel correspondiente
/// si está desbloqueado. Si está bloqueado, solo reproduce sonido.
///
/// SETUP:
/// Coloca un BoxCollider trigger en el umbral de cada puerta.
/// Asigna el levelIndex y configura isUnlocked desde el MainMenuManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorInteraction : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Índice del nivel que carga esta puerta. 1, 2 o 3.")]
    [SerializeField] private int levelIndex;

    [Header("Objetos de la puerta")]
    [Tooltip("GameObject de la puerta abierta — se activa si está desbloqueada.")]
    [SerializeField] private GameObject doorOpenModel;

    [Tooltip("GameObject de la puerta cerrada — se activa si está bloqueada.")]
    [SerializeField] private GameObject doorClosedModel;

    [Header("Feedback de proximidad")]
    [SerializeField] private SoundData unlockedProximitySound;
    [SerializeField] private SoundData lockedProximitySound;
    [SerializeField] private float proximitySoundRadius = 3f;

    [Header("Feedback de entrada")]
    [SerializeField] private SoundData enterSound;

    // Estado — lo setea MainMenuManager al cargar el progreso
    private bool _isUnlocked;
    private bool _proximityPlayed;
    private Transform _player;

    private void Update()
    {
        if (_player == null) return;

        float distance = Vector3.Distance(transform.position, _player.position);
        bool inProximity = distance <= proximitySoundRadius;

        // Sonido de proximidad — solo se reproduce una vez por acercamiento
        if (inProximity && !_proximityPlayed)
        {
            _proximityPlayed = true;
            SoundData sound = _isUnlocked ? unlockedProximitySound : lockedProximitySound;
            if (sound != null)
                AudioManager.Instance.PlaySFX(sound, transform.position);
        }
        else if (!inProximity)
        {
            _proximityPlayed = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (!_isUnlocked)
        {
            // Bloqueada — no pasa nada visualmente, el sonido ya se reprodujo
            // en la proximidad. Si quieres un feedback adicional aquí puedes
            // agregar un VFX de "bloqueado" o un texto flotante.
            return;
        }

        if (enterSound != null)
            AudioManager.Instance.PlaySFX(enterSound, transform.position);

        MainMenuManager.Instance.LoadLevel(levelIndex);
    }

    /// <summary>
    /// Llamado por MainMenuManager al recibir el progreso de Firebase.
    /// </summary>
    public void SetUnlocked(bool unlocked, Transform player)
    {
        _isUnlocked = unlocked;
        _player = player;

        // Activamos el modelo correcto de la puerta
        doorOpenModel?.SetActive(unlocked);
        doorClosedModel?.SetActive(!unlocked);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, proximitySoundRadius);
    }
}