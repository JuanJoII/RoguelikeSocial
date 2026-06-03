using System.Collections.Generic;
using UnityEngine;
using Supabase.Postgrest;
using Cysharp.Threading.Tasks;
using RoguelikeSocial.Assets.Scripts.Models;
using System;

/// <summary>
/// Única clase que habla con el sistema de Firebase de tu amigo.
/// No sabe cómo funciona Firebase — solo genera el JSON correcto
/// y llama los métodos que tu amigo expone.
///
/// CONTRATO CON EL AMIGO:
/// Él expone métodos que reciben strings JSON.
/// Tú generas esos strings con JsonUtility.
/// Si cambia la estructura, solo tocas las clases internas de este archivo.
/// </summary>
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("Supabase Config")]
    [SerializeField] private AuthManager authManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Estructuras de datos — acuerda estos campos con tu amigo (ya 🤓) ─────────

    [System.Serializable]
    private class RoomProgressData
    {
        public string roomId;
        public int bestScore;
        public string state; // "completed"
    }

    [System.Serializable]
    private class RunProgressPayload
    {
        public int level;
        public int lastRoom;
        public RoomProgressData[] rooms;
    }

    // ── API pública ───────────────────────────────────────────────────────

    public void SaveRunProgress(int level, int lastRoom, Dictionary<int, int> roomScores)
    {
        int totalScore = 0;
        RoomProgressData[] rooms = new RoomProgressData[roomScores.Count];
        int i = 0;

        foreach (var kvp in roomScores)
        {
            rooms[i++] = new RoomProgressData
            {
                roomId = $"level{level:D2}_room{kvp.Key:D2}",
                bestScore = kvp.Value,
                state = "completed"
            };
        }

        RunProgressPayload payload = new RunProgressPayload
        {
            level = level,
            lastRoom = lastRoom,
            rooms = rooms
        };

        string json = JsonUtility.ToJson(payload);

        // El amigo fue llamado 🤓
        // Reemplaza esto con la firma exacta que él te dé
        // DatabaseBridge.Instance.SaveProgress(json);
        SaveToSupabase(level, lastRoom, totalScore, json).Forget();

        Debug.Log($"[DataManager] Enviando a Firebase:\n{json}");
    }

    public void UnlockNextLevel(int completedLevel)
    {
        int nextLevel = completedLevel + 1;
        string json = JsonUtility.ToJson(new { levelToUnlock = nextLevel });

        // DatabaseBridge.Instance.UnlockLevel(json);
        Debug.Log($"[DataManager] Desbloqueando nivel {nextLevel}");
    }
    /// <summary>
    /// Descarga el progreso del jugador actual.
    /// El callback recibe username, totalScore y maxUnlockedLevel.
    /// </summary>
    public async UniTaskVoid FetchPlayerProgress(System.Action<string, int, int> onComplete)
    {
        // Tu amigo implementa la llamada a Supabase aquí. (el amigo la implemento 🤓)
        try
        {
            var currentUser = authManager.SupabaseClient.Auth.CurrentUser;
            if (currentUser == null)
            {
                Debug.LogError("No hay ningun usuario autenticado.");
                return;
            }

            var response = await authManager.SupabaseClient
                .From<PlayerProgressModel>()
                .Where(x => x.Id == currentUser.Id)
                .Single()
                .AsUniTask();

            if (response != null)
            {
                onComplete?.Invoke(response.Username, response.TotalScore, response.Level);
                Debug.Log($"Progreso del jugador '{response.Username}' cargado: Nivel {response.Level}, Puntuación {response.TotalScore}");
            }
            else
            {
                Debug.LogWarning("No se encontró progreso para el jugador actual.");
                onComplete?.Invoke("JugadorDesconocido", 0, 1); // Valores por defecto si no hay progreso
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error al cargar progreso del jugador: {ex.Message}");
            onComplete?.Invoke("JugadorDesconocido", 0, 1); // Valores por defecto en caso de error
        }
        // Por ahora un placeholder para que compile:
        Debug.Log("[DataManager] FetchPlayerProgress — conectar con Supabase.");

        // Simulación para desarrollo:
        onComplete?.Invoke("Jugador01", 4500, 2);
    }

    /// <summary>
    /// Descarga el ranking global de Supabase ordenando por puntaje total de mayor a menor.
    /// </summary>
    public void FetchRanking(Action<List<PlayerProgressModel>> onComplete)
    {
        FetchRankingAsync(onComplete).Forget();
    }

    private async UniTaskVoid FetchRankingAsync(Action<List<PlayerProgressModel>> onComplete)
    {
        try
        {
            // ── CONTROL DE SEGURIDAD ─────────────────────────────────────────────
            // Si no se asignó en el inspector, intentamos buscarlo en la escena
            if (authManager == null)
            {
                authManager = FindFirstObjectByType<AuthManager>(); // O FindObjectOfType en versiones viejas de Unity
            }

            // Si aún así no existe o el cliente no está instanciado, abortamos de forma segura
            // if (authManager == null || authManager.SupabaseClient == null)
            // {
            //     Debug.LogError("[DataManager] No se puede cargar el ranking porque AuthManager o SupabaseClient son nulos.");
            //     onComplete?.Invoke(new List<PlayerProgressModel>());
            //     return;
            // }

            if (!authManager.IsInitialized)
            {
                Debug.Log("[DataManager] Esperando a que Supabase termine de inicializarse...");
                await UniTask.WaitUntil(() => authManager.IsInitialized);
            }
            // Hacemos el GET ordenando descendentemente por la columna total_score
            var response = await authManager.SupabaseClient
                .From<PlayerProgressModel>()
                .Order(x => x.TotalScore, Constants.Ordering.Descending)
                .Limit(10) // Top 10 jugadores
                .Get()
                .AsUniTask();

            // Enviamos la lista de modelos directamente al callback
            onComplete?.Invoke(response.Models);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataManager] Error al descargar el Ranking: {ex.Message}");
            onComplete?.Invoke(new List<PlayerProgressModel>());
        }
    }

    private async UniTaskVoid SaveToSupabase(int level, int lastRoom, int totalScore, string payload)
    {
        try
        {
            var currentUser = authManager.SupabaseClient.Auth.CurrentUser;
            if (currentUser == null)
            {
                Debug.LogError("No hay ningun usuario autenticado.");
                return;
            }

            string username = currentUser.UserMetadata.ContainsKey("username") ? currentUser.UserMetadata["username"].ToString() : "Unknown";

            var progressData = new PlayerProgressModel
            {
                Id = currentUser.Id,
                Username = username,
                Level = level,
                LastRoom = lastRoom,
                TotalScore = totalScore,
                Payload = Newtonsoft.Json.JsonConvert.DeserializeObject(payload) // Guardamos el payload como un objeto JSON deserializado
            };

            await authManager.SupabaseClient.From<PlayerProgressModel>().Upsert(progressData).AsUniTask();
            Debug.Log("Progreso guardado exitosamente en Supabase.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error al guardar progreso en Supabase: {ex.Message}");
        }
    }
}
