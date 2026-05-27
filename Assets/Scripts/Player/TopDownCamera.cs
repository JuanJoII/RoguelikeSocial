using UnityEngine;

/// <summary>
/// Cámara top-down fija que sigue al jugador con suavizado.
///
/// El offset define la posición relativa de la cámara respecto al jugador.
/// Para una vista estilo Hades: Y alto, Z negativo leve para dar perspectiva.
/// Ejemplo de offset: (0, 14, -4) con rotación de la cámara en (65, 0, 0).
///
/// Room bounds: cuando la sala es pequeña y no queremos que la cámara
/// salga de sus límites, activamos useBounds y configuramos los límites
/// en el RoomContext (la cámara los lee al entrar a cada sala).
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    public static TopDownCamera Instance { get; private set; }

    [Header("Seguimiento")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -4f);
    [SerializeField] private float followSpeed = 8f;

    [Header("Room Bounds")]
    [Tooltip("Actívalo para limitar la cámara al área de la sala actual")]
    [SerializeField] private bool useBounds;
    private Vector2 _boundsMin;
    private Vector2 _boundsMax;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // LateUpdate garantiza que la cámara se mueva DESPUÉS de que el jugador
    // se haya movido en Update, evitando jitter visual
    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        if (useBounds)
            desiredPosition = ClampToBounds(desiredPosition);

        // Lerp suaviza el seguimiento — a mayor followSpeed, más pegado al jugador
        transform.position = Vector3.Lerp(
            transform.position, desiredPosition, followSpeed * Time.deltaTime);
    }

    private Vector3 ClampToBounds(Vector3 pos)
    {
        // Clamp en XZ — el Y siempre se mantiene del offset
        pos.x = Mathf.Clamp(pos.x, _boundsMin.x + offset.x, _boundsMax.x + offset.x);
        pos.z = Mathf.Clamp(pos.z, _boundsMin.y + offset.z, _boundsMax.y + offset.z);
        return pos;
    }

    // ── API pública ───────────────────────────────────────────────────────

    public void SetTarget(Transform newTarget) => target = newTarget;

    /// <summary>
    /// Llama esto al entrar a una sala para actualizar los límites.
    /// RoomManager lo llama con los datos del RoomContext.
    /// </summary>
    public void SetRoomBounds(Vector2 min, Vector2 max, bool active)
    {
        _boundsMin = min;
        _boundsMax = max;
        useBounds = active;
    }

    /// <summary>
    /// Teletransporta la cámara al jugador sin suavizado.
    /// Útil al cargar una nueva sala para evitar que la cámara viaje visible.
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
    }
}
