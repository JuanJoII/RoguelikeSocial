using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool de efectos visuales. Reutiliza GameObjects de partículas para evitar
/// Instantiate/Destroy en runtime (que generan GC spikes en combate).
///
/// USO:
///   VFXPool.Instance.PlayVFX(miVFXData, transform.position, transform.rotation);
///
/// El pool crece bajo demanda: si todos los objetos de un tipo están en uso,
/// crea uno nuevo en lugar de fallar. Esto es intencional — es mejor un
/// alloc puntual que un VFX que no aparece.
/// </summary>
public class VFXPool : MonoBehaviour
{
    public static VFXPool Instance { get; private set; }

    // Cada VFXData tiene su propia Queue de GameObjects reutilizables.
    // La clave es el ScriptableObject mismo (por referencia), no un string,
    // lo que hace la búsqueda más rápida y elimina errores de typo.
    private Dictionary<VFXData, Queue<GameObject>> pools
        = new Dictionary<VFXData, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ════════════════════════════════════════════════════════════════════════
    // API PÚBLICA
    // ════════════════════════════════════════════════════════════════════════

    public void PlayVFX(VFXData data, Vector3 position, Quaternion rotation)
    {
        if (data == null)
        {
            Debug.LogWarning("[VFXPool] PlayVFX: VFXData es null.");
            return;
        }
        if (data.prefab == null)
        {
            Debug.LogWarning($"[VFXPool] '{data.name}': el prefab no está asignado.");
            return;
        }

        GameObject obj = GetFromPool(data);
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        // Usamos GetLifetime() en lugar del campo lifetime directamente.
        // Esto lee la duración real del ParticleSystem si existe, evitando
        // que el VFX se corte antes de terminar o permanezca activo de más.
        StartCoroutine(DespawnVFX(data, obj, data.GetLifetime()));
    }

    // ════════════════════════════════════════════════════════════════════════
    // POOL INTERNO
    // ════════════════════════════════════════════════════════════════════════

    private GameObject GetFromPool(VFXData data)
    {
        if (!pools.ContainsKey(data))
            pools[data] = new Queue<GameObject>();

        Queue<GameObject> queue = pools[data];

        // Descartamos objetos null que hayan sido destruidos (cambio de escena, etc.)
        // hasta encontrar uno válido o vaciar la cola.
        while (queue.Count > 0)
        {
            GameObject candidate = queue.Dequeue();
            if (candidate != null)
                return candidate;
            // Si era null lo ignoramos y seguimos buscando en la cola
        }

        // Cola vacía o todos eran null: creamos uno nuevo.
        // Todos los objetos del pool son hijos de este transform para:
        //   1. Mantener la jerarquía limpia (no flotan en la raíz de la escena)
        //   2. Que su ciclo de vida esté ligado al VFXPool, no a la escena
        return Instantiate(data.prefab, transform);
    }

    private IEnumerator DespawnVFX(VFXData data, GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);

        if (obj == null) yield break; // Fue destruido externamente, no hay nada que hacer

        obj.SetActive(false);

        if (!pools.ContainsKey(data))
            pools[data] = new Queue<GameObject>();

        pools[data].Enqueue(obj);
    }
}
