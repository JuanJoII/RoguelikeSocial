using UnityEngine;

/// <summary>
/// Componente que vive en cada sala del nivel.
/// Es la única interfaz entre el sistema procedural y el gameplay.
///
/// Tu amiga genera la geometría — tú configuras este componente.
/// RoomManager lo lee al activarse la sala para obtener su configuración.
///
/// SETUP:
/// Agrega este componente al GameObject raíz de cada sala.
/// Llena possibleConfigs con los RoomConfigSO del nivel de dificultad
/// correspondiente. El sistema elige uno al azar al entrar.
///
/// ROOM BOUNDS:
/// Define los límites de la sala para que la cámara no salga de ella.
/// Son dos puntos en XZ — el mínimo y el máximo del área jugable.
/// </summary>
public class RoomContext : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("Índice único de esta sala en el nivel. " +
             "Coincide con el roomId que se guarda en Firebase.")]
    public int roomIndex;

    [Header("Configuración")]
    [Tooltip("Agrega varios RoomConfigSO para este nivel de dificultad. " +
             "El sistema elige uno al azar al entrar a la sala.")]
    public RoomConfigSO[] possibleConfigs;

    [Header("Cámara")]
    [Tooltip("¿La cámara debe limitarse al área de esta sala?")]
    public bool useCameraBounds = true;
    public Vector2 boundsMin;
    public Vector2 boundsMax;

    /// <summary>
    /// Devuelve una config aleatoria del array.
    /// Si el array está vacío, loguea un error claro en lugar de explotar.
    /// </summary>
    public RoomConfigSO GetRandomConfig()
    {
        if (possibleConfigs == null || possibleConfigs.Length == 0)
        {
            Debug.LogError($"[RoomContext] Sala {roomIndex} no tiene configs asignadas. " +
                           "Asigna al menos un RoomConfigSO en el Inspector.");
            return null;
        }

        return possibleConfigs[UnityEngine.Random.Range(0, possibleConfigs.Length)];
    }

    // Dibuja los bounds en la Scene View para facilitar la configuración
    private void OnDrawGizmosSelected()
    {
        if (!useCameraBounds) return;

        Gizmos.color = new UnityEngine.Color(0f, 1f, 1f, 0.3f);

        Vector3 center = new UnityEngine.Vector3(
            (boundsMin.x + boundsMax.x) * 0.5f,
            transform.position.y,
            (boundsMin.y + boundsMax.y) * 0.5f);

        Vector3 size = new UnityEngine.Vector3(
            boundsMax.x - boundsMin.x,
            1f,
            boundsMax.y - boundsMin.y);

        Gizmos.DrawCube(center, size);
        Gizmos.color = UnityEngine.Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }
}