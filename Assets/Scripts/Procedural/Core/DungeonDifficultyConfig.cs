using UnityEngine;

/// <summary>
/// Perfil de dificultad del dungeon.
/// Crear via: Assets > Create > Dungeon > Difficulty Config
/// 
/// Crea uno por nivel: Easy, Normal, Hard, Nightmare.
/// El DungeonGenerator selecciona cuál usar en tiempo de ejecución.
/// </summary>
[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "Dungeon/Difficulty Config")]
public class DungeonDifficultyConfig : ScriptableObject
{
    [Header("Identificación")]
    public string displayName = "Normal";

    // ─────────────────────────────────────────────
    // SALAS
    // ─────────────────────────────────────────────
    [Header("Salas — Cantidad")]
    [Tooltip("Cantidad mínima de salas generadas")]
    public int minRooms = 6;

    [Tooltip("Cantidad máxima de salas generadas")]
    public int maxRooms = 10;

    [Header("Salas — Tamaño (en celdas)")]
    [Tooltip("Ancho mínimo de sala en celdas")]
    public int minRoomWidth = 3;

    [Tooltip("Ancho máximo de sala en celdas")]
    public int maxRoomWidth = 6;

    [Tooltip("Alto mínimo de sala en celdas")]
    public int minRoomHeight = 3;

    [Tooltip("Alto máximo de sala en celdas")]
    public int maxRoomHeight = 6;

    [Header("Salas — Colocación")]
    [Tooltip("Separación mínima entre salas en celdas (0 = pegadas, ≥2 = con espacio)")]
    [Range(0, 4)]
    public int roomPadding = 1;

    [Tooltip("Intentos por sala antes de descartarla")]
    [Range(20, 200)]
    public int maxPlacementAttempts = 80;

    // ─────────────────────────────────────────────
    // PASILLOS
    // ─────────────────────────────────────────────
    [Header("Pasillos")]
    [Tooltip("Ancho del pasillo en celdas (1 = estrecho, 2-3 = normal)")]
    [Range(1, 4)]
    public int corridorWidth = 1;

    [Tooltip("Porcentaje de conexiones extra sobre el MST (0 = solo MST, 0.3 = 30% extra)")]
    [Range(0f, 0.5f)]
    public float extraConnectionRatio = 0.12f;

    // ─────────────────────────────────────────────
    // FÁBRICA: Presets predefinidos
    // ─────────────────────────────────────────────

    /// <summary>
    /// Devuelve un preset de dificultad sin necesidad de un asset.
    /// Útil para testing rápido o si no quieres crear assets manualmente.
    /// </summary>
    public static DungeonDifficultyConfig CreatePreset(DifficultyLevel level)
    {
        var cfg = CreateInstance<DungeonDifficultyConfig>();

        switch (level)
        {
            case DifficultyLevel.Easy:
                cfg.displayName          = "Easy";
                cfg.minRooms             = 5;
                cfg.maxRooms             = 8;
                cfg.minRoomWidth         = 4;
                cfg.maxRoomWidth         = 7;
                cfg.minRoomHeight        = 4;
                cfg.maxRoomHeight        = 7;
                cfg.roomPadding          = 2;   // Salas más separadas = pasillos más cortos al acercarlas es contradictorio — aquí separación da más espacio al jugador
                cfg.corridorWidth        = 2;   // Pasillos anchos (más fácil navegar)
                cfg.extraConnectionRatio = 0.25f; // Muchos ciclos = fácil orientarse
                cfg.maxPlacementAttempts = 100;
                break;

            case DifficultyLevel.Normal:
                cfg.displayName          = "Normal";
                cfg.minRooms             = 7;
                cfg.maxRooms             = 11;
                cfg.minRoomWidth         = 3;
                cfg.maxRoomWidth         = 6;
                cfg.minRoomHeight        = 3;
                cfg.maxRoomHeight        = 6;
                cfg.roomPadding          = 1;
                cfg.corridorWidth        = 1;
                cfg.extraConnectionRatio = 0.12f;
                cfg.maxPlacementAttempts = 80;
                break;

            case DifficultyLevel.Hard:
                cfg.displayName          = "Hard";
                cfg.minRooms             = 10;
                cfg.maxRooms             = 14;
                cfg.minRoomWidth         = 2;
                cfg.maxRoomWidth         = 5;
                cfg.minRoomHeight        = 2;
                cfg.maxRoomHeight        = 5;
                cfg.roomPadding          = 0;   // Salas pegadas = pasillos mínimos
                cfg.corridorWidth        = 1;
                cfg.extraConnectionRatio = 0.08f; // Pocos ciclos = más laberíntico
                cfg.maxPlacementAttempts = 60;
                break;

            case DifficultyLevel.Nightmare:
                cfg.displayName          = "Nightmare";
                cfg.minRooms             = 13;
                cfg.maxRooms             = 18;
                cfg.minRoomWidth         = 2;
                cfg.maxRoomWidth         = 4;
                cfg.minRoomHeight        = 2;
                cfg.maxRoomHeight        = 4;
                cfg.roomPadding          = 0;
                cfg.corridorWidth        = 1;
                cfg.extraConnectionRatio = 0.05f; // Casi solo MST = muy laberíntico
                cfg.maxPlacementAttempts = 50;
                break;
        }

        return cfg;
    }
}

/// <summary>
/// Niveles de dificultad disponibles.
/// </summary>
public enum DifficultyLevel
{
    Easy,
    Normal,
    Hard,
    Nightmare
}