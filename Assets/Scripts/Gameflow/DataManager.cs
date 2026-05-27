using System.Collections.Generic;
using UnityEngine;

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

    // ── Estructuras de datos — acuerda estos campos con tu amigo ─────────

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

        // Aquí llamas el método de tu amigo
        // Reemplaza esto con la firma exacta que él te dé
        // DatabaseBridge.Instance.SaveProgress(json);

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
    public void FetchPlayerProgress(System.Action<string, int, int> onComplete)
    {
        // Tu amigo implementa la llamada a Firebase aquí.
        // Por ahora un placeholder para que compile:
        Debug.Log("[DataManager] FetchPlayerProgress — conectar con Firebase.");

        // Simulación para desarrollo:
        onComplete?.Invoke("Jugador01", 4500, 2);
    }

    /// <summary>
    /// Descarga el ranking global ordenado por totalScore.
    /// El callback recibe la lista de entradas.
    /// </summary>
    public void FetchRanking(System.Action<System.Collections.Generic.List<RankingPanel.RankingEntry>> onComplete)
    {
        Debug.Log("[DataManager] FetchRanking — conectar con Firebase.");

        // Simulación para desarrollo:
        var fakeData = new System.Collections.Generic.List<RankingPanel.RankingEntry>
        {
            new RankingPanel.RankingEntry { username = "Jugador01", totalScore = 8200, maxUnlockedLevel = 3 },
            new RankingPanel.RankingEntry { username = "Jugador02", totalScore = 6100, maxUnlockedLevel = 2 },
            new RankingPanel.RankingEntry { username = "Jugador03", totalScore = 3400, maxUnlockedLevel = 1 }
        };

        onComplete?.Invoke(fakeData);
    }
}
