using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

/// <summary>
/// Manager global de audio. Persiste entre escenas (DontDestroyOnLoad).
/// 
/// ARQUITECTURA:
///   - SFX espaciales  → PlaySFX / PlayRandomSFX  (usan Object Pool, sin GC spikes)
///   - Música          → PlayMusic / StopMusic / FadeMusic
///   - Ambience        → PlayAmbience
///   - Voces           → PlayVoiceLine
///
/// CÓMO AGREGAR SONIDOS:
///   En lugar de agregar campos aquí, crea un asset SoundLibrary en el proyecto
///   (Audio > Sound Library) y referencíalo donde lo necesites.
///   Ejemplo: SoundLibrary_UI tiene "ui_hover", "ui_confirm", "ui_pause".
///   Uso: AudioManager.Instance.PlaySFX(uiSounds.Get("ui_hover"), transform.position);
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ─── Referencias de Inspector ───────────────────────────────────────────
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambienceSource;

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup sfxGroup;

    [SerializeField] private SoundData musica;
    [SerializeField] private SoundData ambiente;

    // ─── Object Pool ─────────────────────────────────────────────────────────
    // En lugar de crear y destruir un GameObject por sonido (lo que genera
    // Garbage Collection spikes en combate), mantenemos una cola de AudioSources
    // reutilizables. Cuando se necesita uno, se saca de la cola; cuando termina,
    // vuelve a la cola. Nunca se destruyen, solo se desactivan.
    [Header("Object Pool")]
    [Tooltip("Número de AudioSources pre-creados al inicio. Ajusta según la cantidad " +
             "de sonidos simultáneos esperados (hordas de enemigos, efectos de combate).")]
    [SerializeField] private int poolSize = 20;
    private Queue<AudioSource> _pool;

    // ─── Singleton ───────────────────────────────────────────────────────────
    public static AudioManager Instance { get; private set; }

    // ─── Fade ────────────────────────────────────────────────────────────────
    private Coroutine _fadeCoroutine;

    // ════════════════════════════════════════════════════════════════════════
    // INICIALIZACIÓN
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        transform.parent = null;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitPool();
    }

    private void Start()
    {
        PlayAmbience(ambiente);
        PlayMusic(musica);
    }

    /// <summary>
    /// Pre-crea los AudioSources del pool para evitar allocations en runtime.
    /// </summary>
    private void InitPool()
    {
        _pool = new Queue<AudioSource>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            _pool.Enqueue(CreatePooledSource());
        }
    }

    private AudioSource CreatePooledSource()
    {
        GameObject go = new GameObject("PooledAudioSource");
        go.transform.SetParent(transform); // Hijo del AudioManager para que no ensucien la jerarquía
        DontDestroyOnLoad(go);

        AudioSource source = go.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = sfxGroup;
        source.spatialBlend = 1f;
        source.minDistance = 1f;
        source.maxDistance = 20f;

        go.SetActive(false);
        return source;
    }

    private AudioSource GetFromPool()
    {
        // Si el pool está vacío (más sonidos simultáneos que poolSize), crea uno extra.
        // Esto evita que el juego falle; considera aumentar poolSize si esto ocurre seguido.
        if (_pool.Count == 0)
        {
            Debug.LogWarning("[AudioManager] Pool vacío: creando AudioSource extra. " +
                             "Considera aumentar poolSize en el Inspector.");
            return CreatePooledSource();
        }

        AudioSource source = _pool.Dequeue();
        source.gameObject.SetActive(true);
        return source;
    }

    private void ReturnToPool(AudioSource source)
    {
        source.Stop();
        source.clip = null;
        source.gameObject.SetActive(false);
        _pool.Enqueue(source);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SFX ESPACIALES
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reproduce un SFX en una posición del mundo con variación de pitch y volumen.
    /// Usa el Object Pool: no crea ni destruye GameObjects, sin GC spikes.
    /// </summary>
    public void PlaySFX(SoundData data, Vector3 position)
    {
        // Validación defensiva: si alguien olvida asignar un SoundData en el Inspector,
        // el juego no se cae — solo loguea una advertencia clara.
        if (data == null)
        {
            Debug.LogWarning("[AudioManager] PlaySFX: SoundData es null.");
            return;
        }
        if (data.clip == null)
        {
            Debug.LogWarning($"[AudioManager] PlaySFX: '{data.name}' no tiene AudioClip asignado.");
            return;
        }

        AudioSource source = GetFromPool();
        source.transform.position = position;
        source.clip = data.clip;

        float pitch  = data.pitch  + Random.Range(-data.randomPitchRange,  data.randomPitchRange);
        float volume = data.volume + Random.Range(-data.randomVolumeRange, data.randomVolumeRange);

        source.pitch  = Mathf.Clamp(pitch,  0.1f, 3f);
        source.volume = Mathf.Clamp01(volume);
        source.loop   = false;

        source.Play();

        // Devuelve al pool cuando el clip termina.
        // Dividir por pitch es importante: si el pitch es 2.0, el sonido dura la mitad.
        StartCoroutine(ReturnToPoolAfterPlay(source, data.clip.length / source.pitch));
    }

    private IEnumerator ReturnToPoolAfterPlay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay + 0.05f); // pequeño margen de seguridad
        ReturnToPool(source);
    }
    // ════════════════════════════════════════════════════════════════════════
    // MÚSICA
    // ════════════════════════════════════════════════════════════════════════

    public void PlayMusic(SoundData data)
    {
        if (musicSource == null || data == null || data.clip == null) return;

        musicSource.clip   = data.clip;
        musicSource.volume = data.volume;
        musicSource.pitch  = data.pitch;
        musicSource.loop   = data.loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource == null) return;
        musicSource.Stop();
    }

    /// <summary>
    /// Hace fade out de la música actual y fade in de la nueva.
    /// Útil para transiciones entre zonas o estados del juego.
    /// </summary>
    public void FadeToMusic(SoundData data, float fadeDuration = 1f)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeMusicCoroutine(data, fadeDuration));
    }

    private IEnumerator FadeMusicCoroutine(SoundData newMusic, float duration)
    {
        float startVolume = musicSource.volume;

        // Fade out
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }

        musicSource.Stop();
        if (newMusic != null && newMusic.clip != null)
        {
            musicSource.clip = newMusic.clip;
            musicSource.loop = newMusic.loop;
            musicSource.pitch = newMusic.pitch;
            musicSource.Play();

            // Fade in
            t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, newMusic.volume, t / duration);
                yield return null;
            }
            musicSource.volume = newMusic.volume;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // AMBIENTE Y VOCES
    // ════════════════════════════════════════════════════════════════════════

    public void PlayAmbience(SoundData data)
    {
        if (ambienceSource == null || data == null || data.clip == null) return;

        ambienceSource.clip   = data.clip;
        ambienceSource.volume = data.volume;
        ambienceSource.pitch  = data.pitch;
        ambienceSource.loop   = data.loop;
        ambienceSource.Play();
    }

    public void StopAmbience()
    {
        if (ambienceSource == null) return;
        ambienceSource.Stop();
    }
}

