using UnityEngine;

/// <summary>
/// ScriptableObject que define un efecto visual y su duración en el pool.
/// Crea un asset via: VFX > VFXData
///
/// DURACIÓN:
/// El campo 'lifetime' es un fallback manual. Si el prefab tiene un ParticleSystem
/// en su raíz, VFXPool leerá su duración real automáticamente, así que no tienes
/// que acordarte de sincronizar este número cada vez que ajustes la partícula.
/// Solo necesitas cambiar 'lifetime' manualmente si el prefab NO tiene ParticleSystem
/// (por ejemplo, un VFX basado en animación o en un shader con tiempo fijo).
/// </summary>
[CreateAssetMenu(menuName = "VFX/VFXData")]
public class VFXData : ScriptableObject
{
    public GameObject prefab;

    [Tooltip("Duración de reserva. Se usa solo si el prefab no tiene ParticleSystem en su raíz.")]
    public float lifetime = 1f;

    /// <summary>
    /// Devuelve la duración real del efecto.
    /// Prioriza la duración del ParticleSystem del prefab para evitar desincronización.
    /// </summary>
    public float GetLifetime()
    {
        if (prefab == null) return lifetime;

        // Intentamos leer la duración directamente del ParticleSystem del prefab.
        // Esto significa que si ajustas la partícula en el editor, el pool
        // automáticamente respeta ese tiempo sin que tengas que tocar este ScriptableObject.
        ParticleSystem ps = prefab.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            // duration es la duración de un ciclo del sistema de partículas.
            // Si el sistema es looping no tiene sentido usarla, así que usamos lifetime como fallback.
            if (!ps.main.loop)
                return ps.main.duration + ps.main.startLifetime.constantMax;
        }

        return lifetime;
    }
}