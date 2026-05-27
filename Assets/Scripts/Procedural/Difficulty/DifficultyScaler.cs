// Difficulty/DifficultyScaler.cs
using UnityEngine;

/// Calcula el presupuesto de amenaza final para una sala
/// combinando dificultad base, profundidad del dungeon y nivel del jugador.
public static class DifficultyScaler
{
    /// <param name="baseBudget">budget base del DifficultyConfig</param>
    /// <param name="roomBudgetMod">modificador del nodo (Combat=1, Elite=2, etc.)</param>
    /// <param name="depth">profundidad del dungeon (0 = primero, 5 = muy profundo)</param>
    /// <param name="playerLevel">nivel del jugador (1-20)</param>
    public static float CalculateThreatBudget(float baseBudget, float roomBudgetMod,
                                               int depth, int playerLevel)
    {
        // Escala lineal con profundidad: +15% por nivel de profundidad
        float depthMult = 1f + depth * 0.15f;

        // Escala con nivel del jugador: los jugadores de mayor nivel
        // enfrentan salas más llenas incluso en profundidades bajas
        float levelMult = 1f + (playerLevel - 1) * 0.05f; // +5% por nivel

        return baseBudget * roomBudgetMod * depthMult * levelMult;
    }

    /// Calcula el modificador de rareza del loot según profundidad
    public static float GetLootRarityBonus(int depth, int playerLevel)
    {
        return 1f + depth * 0.2f + (playerLevel - 1) * 0.03f;
    }

    /// Cuenta cuántos items de loot generar según tipo de sala y dificultad
    public static int GetLootDropCount(RoomType roomType, DungeonDifficultyConfig diff,
                                        int depth, System.Random rng)
    {
        (int min, int max) range = roomType switch
        {
            RoomType.Start    => (0, 0),
            RoomType.Combat   => (0, 1 + depth / 3),
            RoomType.Treasure => (2, 3 + depth / 2),
            RoomType.Elite    => (1, 2 + depth / 3),
            RoomType.Boss     => (3, 5 + depth),
            _                 => (0, 1)
        };
        return rng.Next(range.min, range.max + 1);
    }
}