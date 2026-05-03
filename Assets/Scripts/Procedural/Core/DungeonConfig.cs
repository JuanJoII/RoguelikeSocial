using UnityEngine;

/// <summary>
/// Configuración BASE del dungeon: grilla, prefabs y semilla.
/// Los parámetros que cambian por dificultad (salas, pasillos) están en DungeonDifficultyConfig.
/// 
/// Crear via: Assets > Create > Dungeon > Config
/// </summary>
[CreateAssetMenu(fileName = "DungeonConfig", menuName = "Dungeon/Config")]
public class DungeonConfig : ScriptableObject
{
    // ─────────────────────────────────────────────
    // GRILLA (fija para todo el juego)
    // ─────────────────────────────────────────────
    [Header("Grid — Invariable por dificultad")]
    [Tooltip("Tamaño de cada celda en unidades Unity. TODO el sistema usa este valor.")]
    public int cellSize = 5;

    [Tooltip("Ancho del mapa en celdas")]
    public int gridWidth = 80;

    [Tooltip("Alto del mapa en celdas")]
    public int gridHeight = 80;

    // ─────────────────────────────────────────────
    // PREFABS
    // ─────────────────────────────────────────────
    [Header("Prefabs")]
    [Tooltip("Prefab de suelo. Se instancia en celdas Room/Corridor.")]
    public GameObject floorPrefab;

    [Tooltip("Prefab de pared. Se instancia en celdas Empty adyacentes al suelo.")]
    public GameObject wallPrefab;

    // ─────────────────────────────────────────────
    // SEMILLA
    // ─────────────────────────────────────────────
    [Header("Seed")]
    public int seed = 12345;

    [Tooltip("Si está activo, genera una semilla distinta en cada generación.")]
    public bool useRandomSeed = true;

    // ─────────────────────────────────────────────
    // DEBUG
    // ─────────────────────────────────────────────
    [Header("Debug")]
    public bool drawGizmos = true;
    public bool logStats   = true;
}