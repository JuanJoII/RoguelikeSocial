using System.Collections;
using UnityEngine;

/// <summary>
/// Pared que bloquea el paso entre salas.
/// Se crea automáticamente por el integrador en cada conexión.
/// Desaparece cuando la sala origen es completada.
///
/// La pared es un cubo simple. Tu amiga puede reemplazar el mesh
/// por algo más estético sin tocar este script.
/// </summary>
public class RoomWall : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.4f;

    private Renderer[] _renderers;
    private Collider _collider;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _collider  = GetComponent<Collider>();
    }

    public void Open()
    {
        StartCoroutine(FadeAndDisable());
    }

    private IEnumerator FadeAndDisable()
    {
        // Fade out de los materiales
        // Requiere que el material use un shader con alpha
        // (Standard con Rendering Mode: Fade, o URP/Lit con Surface: Transparent)
        float elapsed = 0f;

        // Cacheamos los materiales para no crear instancias cada frame
        Material[] mats = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            mats[i] = _renderers[i].material;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);

            foreach (Material mat in mats)
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
            }

            yield return null;
        }

        // Desactivamos el collider primero para que el jugador
        // pueda pasar incluso si el fade aún no terminó visualmente
        if (_collider != null)
            _collider.enabled = false;

        gameObject.SetActive(false);
    }
}